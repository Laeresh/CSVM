using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>One crater dropped on a known point of C1's own ground, over the built world, and
/// measured where it lands. The struck node's mesh grows a private surface, the column over the
/// bowl drops to the decoded floor, and the decorations inside the radius fall. A second request
/// beside the first is refused by the clearance rule. Those requests go to the field directly,
/// since the original's own gate, the struck node's can_modify flag, stands on no shipped node.
/// The remake's carve option is the second door, read here as a live rocket on untouched ground
/// with the option off and then on.
/// The world is private, because a carved chapter must not ride into a later suite's census.</summary>
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
        "refused while the first one stays carved; a live wep_12 on the same ground first carves " +
        "nothing and plays its default scatter_effect, since no C1 node carries can_modify, and a " +
        "live rocket on untouched ground carves nothing with the carve option off and a " +
        "bowl with it on, its burst playing the same row either way and asking the scorch sink " +
        "on both legs, uncarved with the effect it played and as a carve with the option on")]
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

    // The enhanced scorch, driven end to end: real rockets dropped onto tagged plates through the
    // pool, with the sink composed the way GameSession composes it, so what is measured is the
    // decal the session would place rather than a flag. The pool, the cap and the fade are then
    // exercised directly, since a salvo of sixteen live rounds is not what this suite is about.
    [Suite("scorch-decals",
        "Enhanced Graphics only: a rocket hit on tarmac and one on grass each leave exactly one " +
        "scorch decal sized from the weapon's crater radius, a hit on water leaves none, a gun hit " +
        "leaves none, a hit that carved a bowl leaves a wider one, a second hit inside a live mark " +
        "refreshes it instead of stacking, the pool caps at 16 and recycles the oldest, every mark " +
        "is hidden once its life runs out, and the faithful presentation builds no field at all")]
    internal static void ScorchDecals(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"weapon definitions");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, Chapter);
        ctx.RequireData(texturesPath, $"{Chapter} textures");
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        if (!weapons.TryGet("wep_06", out var he))
        {
            ctx.Check(false, $"wep_06 resolves");
            return;
        }

        // The faithful path first: it is what every pinned golden renders, and the able-to-fail
        // control for everything below.
        ctx.Check(Effects.ScorchField.Create() == null,
            $"ABLE-TO-FAIL CONTROL: the faithful presentation builds no scorch field, so no pool, no decal and no texture");

        var textures = new TextureArchive(texturesPath);
        GraphicsMode.Resolve(GraphicsMode.EnhancedWord);
        try
        {
            Burn(ctx, textures, he);
        }
        finally
        {
            GraphicsMode.Resolve(GraphicsMode.Default);
            textures.Dispose();
        }
        ctx.Check(!GraphicsMode.Enhanced, $"the graphics setting is back on the faithful presentation");
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

        // Before any direct request: a played round on the same ground leaves it uncarved.
        LiveRound(ctx, world, first, before, report);

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

        // The option's own door, on ground neither carve has touched, so the clearance rule plays no
        // part in what it proves.
        int spare = -1;
        for (int i = 0; i < spots.Count && spare < 0; i++)
        {
            spare = spots[i].At.DistanceTo(first.At) >= SecondSpotM
                && spots[i].At.DistanceTo(far.At) >= SecondSpotM ? i : spare;
        }

        ctx.Check(spare >= 0, $"the scan found a third spot clear of both carves");
        if (spare >= 0)
        {
            OptionalCarve(ctx, world, spots[spare], field, space, report);
        }

        ctx.Note($"C1: one crater carved on {owner.Name}, {census} decorations flattened, the clearance refusing the repeat");
    }

    private static void Burn(TestContext ctx, TextureArchive textures, WeaponDef he)
    {
        const float Dt = 1f / 60f;
        var field = Effects.ScorchField.Create();
        ctx.Check(field != null, $"the enhanced presentation builds a scorch field");
        if (field == null)
        {
            return;
        }

        var plates = new List<StaticBody3D>();
        ProjectilePool? pool = null;
        try
        {
            ctx.Host.AddChild(field);
            int airstrip = SurfaceRegistry.IdForName("airstrip") ?? SurfaceRegistry.Default;
            var tarmac = Ground(ctx, plates, "scorch-tarmac", new Vector3(900f, 4000f, 900f), airstrip);
            var grass = Ground(ctx, plates, "scorch-grass", new Vector3(1600f, 4000f, 900f), SurfaceRegistry.Default);
            var water = Ground(ctx, plates, "scorch-water", new Vector3(2300f, 4000f, 900f), SurfaceRegistry.Water);

            var plays = new List<string>();
            var live = new ProjectilePool(textures, null, null)
            {
                EffectSink = (name, at, orient, ringOrient, ttl) => plays.Add(name),
                ScorchSink = (at, normal, effectName, carved) =>
                    GameSession.RegisterScorch(field, at, normal, effectName, carved),
            };
            pool = live;
            ctx.Host.AddChild(live);

            bool Drop(Vector3 onto)
            {
                plays.Clear();
                live.Spawn(he, new Transform3D(Basis.LookingAt(Vector3.Down, Vector3.Forward),
                    onto + new Vector3(0f, 20f, 0f)), Vector3.Zero);
                for (int i = 0; i < 120 && plays.Count == 0; i++)
                {
                    live.SimStep(Dt);
                }
                live.Clear();
                return plays.Count > 0;
            }

            ctx.Check(Drop(tarmac), $"the rocket reaches the tarmac plate and plays an impact effect");
            ctx.Same(1, field.LiveMarks, $"the tarmac hit leaves exactly one scorch");
            ctx.Check(Mathf.IsEqualApprox(field.RadiusOf(0), CraterShape.RimRadius * 0.35f, 1e-3f),
                $"sized from the weapon's crater radius, a burst that carved nothing at 0.35 of it (radius={field.RadiusOf(0):0.00} m)");
            var decal = field.NodeOf(0);
            ctx.Check(decal.Visible && Mathf.IsEqualApprox(decal.Size.X, field.RadiusOf(0) * 2f, 1e-3f)
                && Mathf.IsEqualApprox(decal.Size.Z, field.RadiusOf(0) * 2f, 1e-3f),
                $"the decal is drawn and its projection box spans the mark (size={decal.Size})");
            ctx.Check(decal.Modulate.A > 0.7f && decal.Modulate.A <= 1f,
                $"a fresh mark draws at its full darkness (alpha={decal.Modulate.A:0.00})");

            ctx.Check(Drop(grass), $"the rocket reaches the grass plate too");
            ctx.Same(2, field.LiveMarks, $"the grass hit leaves a second scorch");

            ctx.Check(Drop(water), $"the rocket reaches the water plate");
            ctx.Same(2, field.LiveMarks, $"and leaves no mark on it, since a projected decal on the sea reads wrong");

            GameSession.RegisterScorch(field, tarmac + new Vector3(200f, 0f, 0f), Vector3.Up, "3040slug_gunhit", carved: false);
            ctx.Same(2, field.LiveMarks, $"a gun hit carries no fireball and marks nothing even under Enhanced");

            GameSession.RegisterScorch(field, tarmac + new Vector3(300f, 0f, 0f), Vector3.Up, "he_ground_effect", carved: true);
            ctx.Same(3, field.LiveMarks, $"a hit that carved a bowl marks the ground it opened");
            ctx.Check(Mathf.IsEqualApprox(field.RadiusOf(2), CraterShape.RimRadius * 0.85f, 1e-3f),
                $"and rings the carve at 0.85 of the crater radius (radius={field.RadiusOf(2):0.00} m)");

            // The crater field's refusal, borrowed: a hit inside a live mark refreshes it.
            live.SimStep(Dt);
            field.Tick(5f);
            ctx.Check(field.AgeOf(0) > 0f, $"the first mark has aged");
            GameSession.RegisterScorch(field, tarmac + new Vector3(1f, 0f, 0f), Vector3.Up, "he_ground_effect", carved: false);
            ctx.Same(3, field.LiveMarks, $"a second burst inside a live mark refreshes it instead of stacking a decal on it");
            ctx.Check(Mathf.IsEqualApprox(field.AgeOf(0), 0f),
                $"and the refreshed mark starts its life over (age={field.AgeOf(0):0.00} s)");

            Cap(ctx, field, tarmac);
            Expire(ctx, field);
        }
        finally
        {
            pool?.Free();
            foreach (var plate in plates)
            {
                plate.Free();
            }
            ctx.Host.RemoveChild(field);
            field.Free();
        }
    }

    // The pool's own bound. The crater field keeps no count cap (its list only grows and the
    // refusal is what holds it down), so the scorch's cap is its own: marks past it recycle the
    // oldest decal rather than growing the pool.
    private static void Cap(TestContext ctx, Effects.ScorchField field, Vector3 near)
    {
        for (int i = 0; i < 30; i++)
        {
            field.Tick(0.5f);
            GameSession.RegisterScorch(field, near + new Vector3(0f, 0f, 60f + (i * 60f)), Vector3.Up,
                "he_ground_effect", carved: false);
        }
        ctx.Same(16, field.PooledNodes, $"the pool stops at its cap of 16 decal nodes");
        ctx.Same(16, field.LiveMarks, $"all sixteen stand, the thirty-third mark having taken the oldest slot");
        float oldest = 0f;
        for (int i = 0; i < field.PooledNodes; i++)
        {
            oldest = Mathf.Max(oldest, field.AgeOf(i));
        }
        ctx.Check(oldest < 16f * 0.5f,
            $"no mark older than the sixteen most recent survives, the oldest having been recycled (oldest={oldest:0.0} s)");
    }

    // The life: a mark holds its darkness, fades over the tail and is hidden, not left drawn.
    private static void Expire(TestContext ctx, Effects.ScorchField field)
    {
        float before = field.NodeOf(0).Modulate.A;
        field.Tick(70f);
        float faded = field.NodeOf(0).Modulate.A;
        ctx.Check(faded < before && faded > 0f,
            $"a mark fades over the tail of its life ({before:0.00} → {faded:0.00})");
        field.Tick(30f);
        ctx.Same(0, field.LiveMarks, $"every mark is gone once its life runs out");
        ctx.Check(!field.NodeOf(0).Visible, $"and its decal is hidden rather than left drawn");
        ctx.Same(16, field.PooledNodes, $"the pool keeps its nodes for the next burst rather than freeing them");
        ctx.Note($"scorch: one decal per burst, cap 16, {CraterShape.RimRadius * 0.35f:0.0} m on bare ground and {CraterShape.RimRadius * 0.85f:0.0} m over a carve");
    }

    private static Vector3 Ground(TestContext ctx, List<StaticBody3D> plates, string name, Vector3 at, int surfaceId)
    {
        var plate = CombatSuites.Plate(name, new Vector3(120f, 0.2f, 120f), at);
        plate.SetMeta(SceneBuilder.SurfaceIdMeta, surfaceId);
        ctx.Host.AddChild(plate);
        plates.Add(plate);
        return at;
    }

    // The carve option read where it decides: a live rocket warhead on untouched C1 ground, once
    // with it off and once on. The off leg is what every shipped run and every pinned golden sees.
    // The on leg carves although the struck node carries no can_modify stamp, which is the whole
    // of the option. It still plays its burst, because only the original's own AND suppresses a
    // weapon's impact row (docs/org/craters.md).
    private static void OptionalCarve(TestContext ctx, TestWorld world, (Vector3 At, StaticBody3D Body) spot,
        CraterField field, PhysicsDirectSpaceState3D space, StringBuilder report)
    {
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var rocket = weapons.All.FirstOrDefault(w =>
            w.IsRocket && !w.Crater && w.DetonationDotProduct is null && w.CannonSpread is not > 0f);
        ctx.Check(rocket != null, $"a plain rocket warhead that carries no CRATER exists in the data");
        if (rocket == null)
        {
            return;
        }

        ctx.Check(!SceneBuilder.CanModify(spot.Body), $"the spot's node carries no can_modify stamp ({spot.Body.GetParent()?.Name})");
        var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, Chapter));
        var plays = new List<(string Name, Vector3 At)>();
        var scorches = new List<(string? Effect, bool Carved)>();
        int asked = 0;
        var pool = new ProjectilePool(textures, null, null)
        {
            CraterSink = (at, struck) =>
            {
                asked++;
                return field.TryCarve(at, struck);
            },
            EffectSink = (name, at, orient, ring, ttl) => plays.Add((name, at)),
            ScorchSink = (at, normal, effectName, carved) => scorches.Add((effectName, carved)),
        };
        bool armed = CraterGate.Enabled;
        int before = field.Craters.Count;
        try
        {
            ctx.Host.AddChild(pool);
            CraterGate.Enabled = false;
            var off = Drop(pool, rocket, spot.At + new Vector3(0f, 30f, 0f), plays);
            report.AppendLine($"live {rocket.Id} with the option off: sink asked {asked}, "
                + $"{field.Craters.Count} craters, fx={off?.Name ?? "-"}");
            ctx.Same(0, asked, $"with the option off the gate refuses the rocket before the field is asked");
            ctx.Same(before, field.Craters.Count, $"and the ground is left intact, the behaviour every golden is pinned on");
            ctx.Check(off != null, $"the round still burst where it struck ({off?.Name ?? "no play"})");
            // The enhanced scorch is asked on either leg. Uncarved, the burst's own effect decides:
            // a fireball marks and this rocket's spark does not (GameSession.RegisterScorch).
            ctx.Check(scorches.Count == 1 && !scorches[0].Carved && scorches[0].Effect == off?.Name,
                $"the uncarved burst still asks the scorch sink, uncarved, with the effect it played ({(scorches.Count > 0 ? scorches[^1].Effect : "no ask")}, fireball={(scorches.Count > 0 && scorches[^1].Effect is { } burnt && EffectCatalogue.IsBurstLight(burnt))})");

            CraterGate.Enabled = true;
            var on = Drop(pool, rocket, spot.At + new Vector3(0f, 30f, 0f), plays);
            report.AppendLine($"live {rocket.Id} with the option on: sink asked {asked}, "
                + $"{field.Craters.Count} craters, fx={on?.Name ?? "-"}");
            ctx.Same(1, asked, $"with the option on the same round is handed to the field");
            ctx.Same(before + 1, field.Craters.Count, $"which carves, although no node here carries the stamp");
            ctx.Check(scorches.Count == 2 && scorches[1].Carved,
                $"and the scorch sink is told this hit cut a bowl, so the mark rings the carve");
            ctx.Check(on != null && on.Value.Name == off?.Name,
                $"and the burst plays the same row it played uncarved, since the option suppresses nothing ({on?.Name ?? "no play"} vs {off?.Name ?? "no play"})");
            float floor = ColumnY(space, spot.At, out _);
            report.AppendLine($"column over the optional impact: {spot.At.Y:0.00} before, {floor:0.00} after");
            ctx.Check(floor < spot.At.Y - 3f,
                $"the ground under the burst has dropped into the bowl before={spot.At.Y:0.00} after={floor:0.00}");
        }
        finally
        {
            CraterGate.Enabled = armed;
            pool.Free();
            textures.Dispose();
        }
    }

    // A live wep_12 dropped through a ProjectilePool whose sink is a real field on this world. No C1
    // node carries can_modify, so the gate refuses before the field is asked and the Choker plays its
    // default row. The control is a plate stamped with the flag, whose sink is asked and declines.
    private static void LiveRound(TestContext ctx, TestWorld world,
        (Vector3 At, StaticBody3D Body) spot, ArrayMesh before, StringBuilder report)
    {
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        if (!weapons.TryGet("wep_12", out var choker) || !choker.Crater)
        {
            ctx.Check(false, $"wep_12 resolves and carries CRATER");
            return;
        }
        int flagged = CountCanModify(world.Session.Root);
        ctx.Same(0, flagged, $"no collider of the built C1 world carries the can_modify stamp");

        var mesh = ((Node3D)WorldCollision.OwnerOf(spot.Body)).GetNodeOrNull<MeshInstance3D>("mesh");
        var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, Chapter));
        var liveField = new CraterField(world.Session.Root);
        var plays = new List<(string Name, Vector3 At)>();
        int asked = 0;
        bool declineAll = false;
        var pool = new ProjectilePool(textures, null, null)
        {
            CraterSink = (at, struck) =>
            {
                asked++;
                return !declineAll && liveField.TryCarve(at, struck);
            },
            EffectSink = (name, at, orient, ring, ttl) => plays.Add((name, at)),
        };
        StaticBody3D? plate = null;
        try
        {
            ctx.Host.AddChild(pool);
            var played = Drop(pool, choker, spot.At + new Vector3(0f, 30f, 0f), plays);
            report.AppendLine($"live wep_12 on {spot.Body.Name}: sink asked {asked}, "
                + $"{liveField.Craters.Count} craters, fx={played?.Name ?? "-"}");
            ctx.Same(0, asked, $"the gate refuses the round before the crater field is asked");
            ctx.Same(0, liveField.Craters.Count, $"a live wep_12 on C1 ground carves nothing");
            ctx.Check(ReferenceEquals(mesh?.Mesh, before),
                $"the struck node still holds SceneBuilder's shared mesh, uncut");
            ctx.Check(played is { Name: "scatter_effect" } p && p.At.DistanceTo(spot.At) < 2f,
                $"the Choker's ground burst plays its default row, scatter_effect at the impact ({(played is { } q ? $"{q.Name} at {q.At}" : "no play")})");

            // The control: the same round onto a node that does carry the flag reaches the sink.
            declineAll = true;
            var above = spot.At + new Vector3(0f, 1500f, 0f);
            plate = CombatSuites.Plate("crater-gate-control", new Vector3(60f, 0.2f, 60f), above);
            plate.SetMeta(SceneBuilder.CanModifyMeta, true);
            ctx.Host.AddChild(plate);
            var control = Drop(pool, choker, above + new Vector3(0f, 20f, 0f), plays);
            ctx.Same(1, asked, $"a round on a can_modify collider is handed to the sink");
            ctx.Check(control is { Name: "scatter_effect" },
                $"and, the sink declining, still plays its default row ({control?.Name ?? "no play"})");
        }
        finally
        {
            pool.Free();
            plate?.Free();
            textures.Dispose();
        }
    }

    private static (string Name, Vector3 At)? Drop(ProjectilePool pool, WeaponDef weapon, Vector3 above,
        List<(string Name, Vector3 At)> plays)
    {
        plays.Clear();
        pool.Spawn(weapon, new Transform3D(Basis.LookingAt(Vector3.Down, Vector3.Forward), above), Vector3.Zero);
        for (int i = 0; i < 120 && plays.Count == 0; i++)
        {
            pool.SimStep(1f / 60f);
        }
        pool.Clear();
        return plays.Count > 0 ? plays[0] : null;
    }

    private static int CountCanModify(Node node)
    {
        int count = SceneBuilder.CanModify(node) ? 1 : 0;
        foreach (var child in node.GetChildren())
        {
            count += CountCanModify(child);
        }
        return count;
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
