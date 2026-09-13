using System.Collections.Generic;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using Godot;

namespace CSVM.Testing;

/// <summary>One crater dropped on a known point of C1's own ground, over the built world. The
/// carve is measured where it lands rather than in the abstract: the node the round struck gets a
/// private mesh one surface longer, the column over the bowl drops by about the decoded floor, the
/// decorations inside the radius stop standing, and a second request beside the first is refused by
/// the clearance rule. The world is private, because a carved chapter must not ride into a later
/// suite's census.</summary>
internal static class CraterSuites
{
    private const string Chapter = "C1";

    // The search window for a bombing spot: a grid over C1's forested middle, coarse enough to
    // finish in a few hundred rays and wide enough that two spots a crater apart are always found.
    private const float ScanMinX = -7600f, ScanMinZ = -6400f;
    private const float ScanStep = 200f;
    private const int ScanCells = 14;

    // How far apart the two spots must be: two footprints plus the clearance is 45 m, and this
    // leaves the second carve room to fail for its own reasons rather than for the refusal's.
    private const float SecondSpotM = 600f;

    // How far the ground under the rim may stand from the ground under the impact for the spot to
    // count as level.
    private const float FlatM = 1f;

    [Suite("crater-carve",
        "a CRATER weapon's ground strike on C1 carves the decoded bowl into the node it struck: a " +
        "7-vertex rim at radius 20, a floor 6 below the impact, the struck node's own mesh one " +
        "surface longer and un-shared, the column 3 m off centre dropping into the bowl, every " +
        "decoration inside the radius destroyed, and a second crater inside the 5 m clearance " +
        "refused while the first one stays carved")]
    internal static void CraterCarve(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        // The lab reads the ground through the one physics space, where a cached collidable world's
        // colliders would also stand and answer the scan.
        ctx.EvictCollidableWorlds();
        var report = new StringBuilder();
        ctx.WithPrivateWorld(Chapter, collision: true, world => Drop(ctx, world, report));
        ctx.WriteArtifact("test-crater-carve.txt", report.ToString());
    }

    private static void Drop(TestContext ctx, TestWorld world, StringBuilder report)
    {
        var space = ctx.Host.GetWorld3D().DirectSpaceState;
        var clutter = world.Session.Root.GetNodeOrNull<Node3D>("clutter");
        var spots = Spots(space);
        int pick = Populated(spots, clutter);
        int away = spots.Count > 0 ? Clear(spots, pick) : -1;
        report.AppendLine($"ground spots found: {spots.Count}, bombing #{pick}, second #{away}");
        ctx.Check(away >= 0, $"the scan found two terrain spots at least {SecondSpotM:0} m apart");
        if (away < 0)
        {
            return;
        }

        var first = spots[pick];
        var owner = (Node3D)WorldCollision.OwnerOf(first.Body);
        var mesh = owner.GetNodeOrNull<MeshInstance3D>("mesh");
        ctx.Check(mesh?.Mesh is ArrayMesh, $"the struck node {owner.Name} carries an ArrayMesh");
        if (mesh?.Mesh is not ArrayMesh before)
        {
            return;
        }
        int surfacesBefore = before.GetSurfaceCount();

        var shape = CraterShape.At(first.At);
        int census = ClutterCull.Within(clutter, shape);
        int facesBefore = ColliderFaces(first.Body);
        report.AppendLine($"impact ({first.At.X:0.0}, {first.At.Y:0.00}, {first.At.Z:0.0}) on "
            + $"{owner.Name}, {surfacesBefore} surfaces, {census} decorations inside the radius");

        // The rim law, read off the shape the field will carve rather than off a second copy of it.
        ctx.Same(CraterShape.RimPoints, shape.Rim.Count, $"the rim ring is laid with 7 vertices");
        float worstRadius = 0f;
        foreach (var v in shape.Rim)
        {
            var flat = new Vector2(v.X - first.At.X, v.Z - first.At.Z);
            worstRadius = Mathf.Max(worstRadius, Mathf.Abs(flat.Length() - CraterShape.RimRadius));
            worstRadius = Mathf.Max(worstRadius, Mathf.Abs(v.Y - first.At.Y));
        }
        ctx.Check(worstRadius < 1e-3f,
            $"every rim vertex stands at radius {CraterShape.RimRadius:0} at the impact's own height (worst error {worstRadius:0.0000})");
        ctx.Check(Mathf.IsEqualApprox(shape.Floor.Y, first.At.Y - (2f * CraterShape.BowlDepth)),
            $"the bowl's floor sits 6 m under the impact floor={shape.Floor.Y:0.00} impact={first.At.Y:0.00}");

        var field = new CraterField(world.Session.Root);
        var carved = field.Request(first.At, first.Body);
        report.AppendLine($"first request: {carved}");
        ctx.Check(carved == CraterField.Result.Carved, $"the first request carves carved={carved}");
        if (carved != CraterField.Result.Carved)
        {
            return;
        }

        ctx.Same(1, field.Craters.Count, $"the field holds the one crater it carved");
        ctx.Check(!ReferenceEquals(mesh.Mesh, before),
            $"the struck node now holds a mesh of its own, not SceneBuilder's shared one");
        ctx.Same(surfacesBefore + 1, ((ArrayMesh)mesh.Mesh).GetSurfaceCount(),
            $"the carved mesh carries one further surface, the bowl");
        ctx.Check(census > 0, $"the bombed spot had decorations on it to destroy census={census}");
        ctx.Same(0, ClutterCull.Within(clutter, shape),
            $"no decoration is left standing inside the radius (there were {census})");
        ctx.Check(field.DecorationsDestroyed == census,
            $"the field destroyed exactly the census it found destroyed={field.DecorationsDestroyed} census={census}");

        // The bowl through the colliders, which is the half a weapon and an aeroplane both read.
        var probe = first.At + new Vector3(3f, 0f, 0f);
        float after = ColumnY(space, probe, out string into);
        int facesAfter = ColliderFaces(first.Body);
        report.AppendLine($"collider faces on {first.Body.Name}: {facesBefore} before, "
            + $"{facesAfter} after, over {first.Body.GetChildCount()} shapes");
        ctx.Check(facesAfter > facesBefore,
            $"the struck body's trimesh was rebuilt around the bowl before={facesBefore} after={facesAfter}");
        report.AppendLine($"column 3 m off centre: {first.At.Y:0.00} before, {after:0.00} after, into {into}");
        foreach (float r in new[] { 0f, 6f, 12f, 18f, 24f })
        {
            float y = ColumnY(space, first.At + new Vector3(r, 0f, 0f), out string what);
            report.AppendLine($"  profile at radius {r:0}: {y:0.00} into {what}");
        }
        ctx.Check(after < first.At.Y - 3f,
            $"the ground 3 m off the crater centre has dropped into the bowl before={first.At.Y:0.00} after={after:0.00}");
        ctx.Check(after > first.At.Y - (2f * CraterShape.BowlDepth) - 1f,
            $"and has not dropped past the bowl's own floor after={after:0.00} floor={shape.Floor.Y:0.00}");
        ctx.Check(Mathf.IsEqualApprox(ColumnY(space, first.At, out _), shape.Floor.Y, 0.05f),
            $"the column over the centre reads the bowl's floor floor={shape.Floor.Y:0.00}");
        ctx.Check(ColumnY(space, first.At + new Vector3(CraterShape.RimRadius + 4f, 0f, 0f), out _)
            > first.At.Y - 1f,
            $"the ground a few metres outside the rim is untouched");

        // The refusal, which is what bounds a mission's crater count.
        var again = field.Request(first.At + new Vector3(10f, 0f, 0f), first.Body);
        report.AppendLine($"second request 10 m away: {again}");
        ctx.Check(again == CraterField.Result.Overlaps,
            $"a crater 10 m from a carved one is refused outright refused={again}");
        ctx.Same(1, field.Craters.Count, $"the refused request added nothing to the field");

        var far = spots[away];
        var beyond = field.Request(far.At, far.Body);
        report.AppendLine($"third request {SecondSpotM:0} m away: {beyond}");
        ctx.Check(beyond == CraterField.Result.Carved, $"a crater clear of the first one carves carved={beyond}");
        ctx.Same(2, field.Craters.Count, $"the field now holds two craters");

        // Permanence: nothing ages a crater out, so the first one is still recorded, still refuses
        // its own ground, and its bowl is still in the terrain after a later carve elsewhere.
        ctx.Check(field.Craters[0].Impact == first.At, $"the first crater is still the field's first record");
        ctx.Check(field.Request(first.At, first.Body) == CraterField.Result.Overlaps,
            $"the first crater still refuses a second bomb on the same ground");
        ctx.Check(ColumnY(space, probe, out _) < first.At.Y - 3f,
            $"the first bowl is still cut into the terrain after the second carve");
        ctx.Note($"C1: one crater carved on {owner.Name}, {census} decorations flattened, the clearance refusing the repeat");
    }

    private static float ColumnY(PhysicsDirectSpaceState3D space, Vector3 at, out string into)
    {
        var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
            at with { Y = at.Y + 400f }, at with { Y = at.Y - 400f }, CollisionLayers.World));
        into = hit.Count > 0 && hit["collider"].Obj is Node body
            ? $"{body.GetParent()?.Name}/{body.Name}" : "nothing";
        return hit.Count > 0 ? hit["position"].AsVector3().Y : float.NaN;
    }

    private static int ColliderFaces(StaticBody3D body)
    {
        int faces = 0;
        foreach (var child in body.GetChildren())
        {
            if (child is CollisionShape3D { Shape: ConcavePolygonShape3D trimesh })
            {
                faces += trimesh.GetFaces().Length / 3;
            }
        }
        return faces;
    }

    // Every bombable spot on the grid: a downward ray that lands on an untagged world collider (the
    // terrain class, "col") whose node carries a mesh. The caller picks which two it uses, because
    // the one that proves the decoration rule is the one with decorations standing on it.
    private static List<(Vector3 At, StaticBody3D Body)> Spots(PhysicsDirectSpaceState3D space)
    {
        var found = new List<(Vector3 At, StaticBody3D Body)>();
        for (int ix = 0; ix < ScanCells; ix++)
        {
            for (int iz = 0; iz < ScanCells; iz++)
            {
                var at = new Vector3(ScanMinX + (ix * ScanStep), 0f, ScanMinZ + (iz * ScanStep));
                var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                    at with { Y = 3000f }, at with { Y = -1000f }, CollisionLayers.World));
                if (hit.Count == 0 || hit["collider"].Obj is not StaticBody3D body
                    || body.Name != "col" || body.GetParent() is not Node3D node
                    || node.GetNodeOrNull<MeshInstance3D>("mesh")?.Mesh is not ArrayMesh)
                {
                    continue;
                }
                var point = hit["position"].AsVector3();
                if (Flat(space, point))
                {
                    found.Add((point, body));
                }
            }
        }
        return found;
    }

    // Level enough for the decoded depth to be the one the suite measures. The bowl's base is the
    // LOWEST ground its rim found, so on a slope the floor sits 6 m under the downhill rim rather
    // than under the impact, which is correct and unmeasurable in the same assertion.
    private static bool Flat(PhysicsDirectSpaceState3D space, Vector3 at)
    {
        foreach (var step in new[] { Vector3.Right, Vector3.Left, Vector3.Forward, Vector3.Back })
        {
            float y = ColumnY(space, at + (step * CraterShape.RimRadius), out _);
            if (float.IsNaN(y) || Mathf.Abs(y - at.Y) > FlatM)
            {
                return false;
            }
        }
        return true;
    }

    // The spot the crater is dropped on: the first one with decorations standing inside the radius,
    // so the census assertion measures a destruction rather than passing on an empty patch. Falls
    // back to the first ground found, which still proves the mesh and the refusal.
    private static int Populated(List<(Vector3 At, StaticBody3D Body)> spots, Node3D? clutter)
    {
        for (int i = 0; i < spots.Count; i++)
        {
            if (ClutterCull.Within(clutter, CraterShape.At(spots[i].At)) > 0)
            {
                return i;
            }
        }
        return 0;
    }

    // A second spot far enough out that the clearance rule plays no part in whether it carves.
    private static int Clear(List<(Vector3 At, StaticBody3D Body)> spots, int from)
    {
        for (int i = 0; i < spots.Count; i++)
        {
            if (spots[i].At.DistanceTo(spots[from].At) >= SecondSpotM)
            {
                return i;
            }
        }
        return -1;
    }
}
