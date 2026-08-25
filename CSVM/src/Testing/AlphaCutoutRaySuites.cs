using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>BL-477's instrument: what actually stands between a weapon ray and C3/M01's cargo
/// zeppelin's slung hydrogen tanks, measured rather than inferred. Casts a fixed sphere of rays at
/// <c>hydrogentank1</c>'s centre from the built world and names the first collider on each,
/// so the fore/aft asymmetry reported at the controls is a table of node names and counts.
/// The original's own rule is <c>docs/org/weaponRay.md</c>: its ray test reads no texture.</summary>
internal static class AlphaCutoutRaySuites
{
    private const string Chapter = "C3";
    private const string Mission = "M01";
    private const string Zeppelin = "cargozep1";
    private const string Tank = "hydrogentank1";

    // Outside cargozep1's own child bbox (z spans -336..352) and inside the front truss's
    // f_hi LOD band (range.max 800), which is the level the original tests at gun range.
    private const float CastRadius = 500f;

    private const int Azimuths = 36;

    private static readonly float[] Elevations = { -60f, -30f, 0f, 30f, 60f };

    internal static void AlphaCutoutRayCensus(TestContext ctx)
    {
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, Chapter, Mission);
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, Chapter);
        ctx.RequireData(missionZrdr, $"{Chapter}/{Mission} zrdr");
        ctx.RequireData(chapterZrdr, $"{Chapter} zrdr");

        var report = new StringBuilder();
        ctx.WithWorld(Chapter, collision: true, mission: Mission, world =>
        {
            var runtime = world.Session.Runtime;
            var defs = Zeppelins.Load(missionZrdr);
            var nets = AiNets.Load(chapterZrdr);
            ZeppelinRuntime? zeps = null;
            try
            {
                // Placing the zeppelins is what puts cargozep1 at its authored pose; the built
                // pose is wherever the gamez parked the template.
                zeps = new ZeppelinRuntime(defs,
                    name => runtime.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null, nets);
                var host = runtime.FindNodes(Zeppelin).FirstOrDefault();
                ctx.Check(host != null, $"the {Zeppelin} world node resolves in the {Chapter}/{Mission} world");
                if (host == null)
                    return;

                var tank = runtime.FindNodes(Tank, host).FirstOrDefault();
                ctx.Check(tank != null, $"{Tank} resolves under the {Zeppelin} subtree");
                if (tank == null)
                    return;

                var centre = SubtreeCentre(tank);
                ctx.Check(centre != null, $"{Tank}'s subtree carries mesh geometry to aim at");
                if (centre is not { } aimAt)
                    return;

                Census(ctx, world, host, tank, aimAt, report);
            }
            finally
            {
                zeps?.Free();
            }
        });

        ctx.WriteArtifact("test-alpha-cutout-ray-census.txt", report.ToString());
    }

    private static void Census(TestContext ctx, TestWorld world, Node3D host, Node3D tank,
        Vector3 aimAt, StringBuilder report)
    {
        // A moved static body's transform reaches the physics server on the next frame, which
        // never comes inside a suite; without this push every ray reads the colliders at the
        // BUILT pose and finds nothing where the meshes now are (verification INSTR-21).
        foreach (var n in Subtree(host))
        {
            if (n is Node3D n3d)
                n3d.ForceUpdateTransform();
        }

        var space = world.Stage.GetWorld3D().DirectSpaceState;
        var basis = host.GlobalTransform.Basis;
        var tankNodes = Subtree(tank).ToHashSet();

        var occluders = new Dictionary<string, int>();
        int reached = 0;
        int missed = 0;
        int zepBodies = Subtree(host).Count(n => n is StaticBody3D);
        int tankBodies = Subtree(tank).Count(n => n is StaticBody3D);
        int zepMeshes = Subtree(host).Count(n => n is MeshInstance3D);
        report.AppendLine($"{Zeppelin} subtree: {zepMeshes} mesh instance(s), {zepBodies} collider "
            + $"body/bodies; {Tank} subtree: {tankBodies}.");
        report.AppendLine($"{Chapter}/{Mission} {Zeppelin} -> {Tank}: {Azimuths} azimuths x "
            + $"{Elevations.Length} elevations at {CastRadius:0} m, first collider per ray.");
        report.AppendLine("azimuth 0 = from dead ahead of the nose (-Z), 180 = from astern (+Z).");
        report.AppendLine();
        foreach (float elevation in Elevations)
        {
            var row = new StringBuilder($"el {elevation,4:0}: ");
            for (int a = 0; a < Azimuths; a++)
            {
                float az = a * 360f / Azimuths;
                var outward = basis * Outward(az, elevation);
                var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
                    aimAt + (outward * CastRadius), aimAt, CollisionLayers.World));
                string name;
                if (hit.Count == 0)
                {
                    name = "(nothing)";
                    missed++;
                }
                else
                {
                    var collider = hit["collider"].Obj as Node;
                    name = OwnerName(collider);
                    if (collider != null && IsUnder(collider, tankNodes))
                        reached++;
                }

                occluders[name] = occluders.GetValueOrDefault(name) + 1;
                row.Append($"{az,3:0}={name} ");
            }

            report.AppendLine(row.ToString());
        }

        int total = Azimuths * Elevations.Length;
        report.AppendLine();
        report.AppendLine($"reached {Tank}: {reached}/{total}; nothing hit: {missed}/{total}");
        foreach (var (name, count) in occluders.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key))
            report.AppendLine($"  {count,4} x {name}");

        ctx.Check(reached > 0, $"some aspect reaches {Tank} at all, reached={reached}/{total}");
        ctx.Check(reached < total,
            $"and some aspect does not, so the census measures an asymmetry rather than open sky: reached={reached}/{total}");
        string worst = string.Join(", ", occluders.Where(kv => kv.Key != Tank)
            .OrderByDescending(kv => kv.Value).Take(4).Select(kv => $"{kv.Key} ({kv.Value})"));
        ctx.Note($"{reached} of {total} rays at {Tank} reach it; the rest stop on {worst}");
    }

    // Zeppelin-local: azimuth 0 points out along -Z (ahead of the nose), 180 along +Z (astern).
    private static Vector3 Outward(float azimuthDeg, float elevationDeg)
    {
        float az = Mathf.DegToRad(azimuthDeg);
        float el = Mathf.DegToRad(elevationDeg);
        return new Vector3(
            Mathf.Sin(az) * Mathf.Cos(el),
            Mathf.Sin(el),
            -Mathf.Cos(az) * Mathf.Cos(el)).Normalized();
    }

    // The gamez name of the nearest ancestor that carries one: a collider is a StaticBody3D child
    // of the built node, so its parent is the node the ray actually struck.
    private static string OwnerName(Node? collider)
    {
        for (var n = collider; n != null; n = n.GetParent())
        {
            if (n.HasMeta(AnimRuntime.NameMeta))
                return n.GetMeta(AnimRuntime.NameMeta).AsString();
        }

        return collider?.Name.ToString() ?? "(none)";
    }

    private static bool IsUnder(Node node, HashSet<Node> subtree)
    {
        for (var n = node; n != null; n = n.GetParent())
        {
            if (subtree.Contains(n))
                return true;
        }

        return false;
    }

    private static IEnumerable<Node> Subtree(Node root)
    {
        yield return root;
        foreach (var child in root.GetChildren())
        {
            foreach (var n in Subtree(child))
                yield return n;
        }
    }

    // Mesh-AABB centre in world space, because a node origin locates nothing reliably
    // (docs/formats/gamez.md).
    private static Vector3? SubtreeCentre(Node3D root)
    {
        Aabb? box = null;
        foreach (var n in Subtree(root))
        {
            if (n is not MeshInstance3D mi || mi.Mesh == null)
                continue;
            var world = mi.GlobalTransform * mi.GetAabb();
            box = box is { } b ? b.Merge(world) : world;
        }

        return box?.GetCenter();
    }
}
