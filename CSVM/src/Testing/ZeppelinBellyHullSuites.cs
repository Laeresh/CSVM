using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>Whether the multiplayer zeppelins' three belly rings stand over their own hull. The
/// census runs over the eight chapters' <c>MP3</c> worlds, the one shipped setup script that keeps
/// both hulls: every other mission switches them off, and a switched-off hull has no live colliders
/// for any ring to find.
/// ⚠ Scope every ray's answer to the two hulls, never to the world. Both of them, and every vehicle
/// no mission placed, sit at the same world origin, so an unscoped hit reads a neighbouring
/// zeppelin's skin as this one's.</summary>
internal static class ZeppelinBellyHullSuites
{
    // Far enough that a miss is a real absence rather than a short cast: the gondola underside
    // stands about 13 m over a belly ring.
    private const float ProbeReachM = 100f;

    // The multiplayer mode whose setup script leaves both hulls standing. Every other mission in
    // every chapter switches them off (docs/formats/interp.md).
    private const string LiveMission = "MP3";

    // What the report prints where a ray answered with nothing at all.
    private const string NoHit = "none";

    private static readonly string[] Chapters =
    {
        "C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5",
    };

    private static readonly string[] Hulls = { "multiplayer1zep", "multiplayer2zep" };

    // The three rings hanging off the gondola (`underneath`), whose cover is the hull straight
    // above them. The other eleven of the fourteen sit on the flanks and the nose.
    private static readonly string[] BellySites = { "ctur1", "ctur2", "ctur3" };

    [Suite("zeppelin-belly-hull",
        "the multiplayer zeppelins' three belly rings stand over their own hull: over all eight " +
        "chapters' MP3 worlds, the one shipped setup script that keeps both hulls, every belly " +
        "ring's upward ray meets a collider belonging to a multiplayer hull, while in the Instant " +
        "Action world that switches the same hulls off no ring meets anything of its own hull")]
    internal static void ZeppelinBellyHull(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, Chapters[0]);
        ctx.RequireData(texturesPath, $"{Chapters[0]} textures");

        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        var turretDefs = TurretDefs.Load(ctx.ZrdrPath);
        var report = new StringBuilder();
        var tally = new Tally();

        // One archive for every world: the pool exists so the emplacements resolve, and nothing
        // here fires a round, so no texture is ever looked up through it.
        var textures = new TextureArchive(texturesPath);
        try
        {
            // The state this reads as a missing collider in: the Instant Action build of the run's
            // own chapter, where both hulls are switched off and therefore not solid.
            ctx.WithWorld(ctx.Chapter, collision: true, world =>
                WithEmplacements(ctx, world, turretDefs, weapons, textures, runtime =>
                    CensusOff(ctx, world, runtime, report, tally)));

            foreach (string chapter in Chapters)
            {
                ctx.WithWorld(chapter, collision: true, LiveMission, world =>
                    WithEmplacements(ctx, world, turretDefs, weapons, textures, runtime =>
                        CensusLive(ctx, world, runtime, chapter, report, tally)));
            }
        }
        finally
        {
            textures.Dispose();
        }

        int liveHulls = Chapters.Length * Hulls.Length;
        ctx.Same(liveHulls, tally.LiveHulls,
            $"both multiplayer zeppelins stand in every one of the eight {LiveMission} worlds");
        ctx.Same(liveHulls * BellySites.Length, tally.RingsOverHull,
            $"every belly ring on every one of them stands over multiplayer-zeppelin hull");
        ctx.Same(Chapters.Length, tally.CoincidentHulls,
            $"the two hulls stand on each other in every one of them, both left at the world origin");
        ctx.Same(Hulls.Length, tally.HullsOff,
            $"{ctx.Chapter}/{ctx.Mission} switches both of them off instead");
        ctx.Same(0, tally.RingsOverHullWhileOff,
            $"and no belly ring on a switched-off hull meets anything of that hull");
        ctx.WriteArtifact("test-zeppelin-belly-hull.txt", report.ToString());
        ctx.Note($"{tally.RingsOverHull} belly ring(s) over hull across {Chapters.Length} {LiveMission} worlds; {tally.StrangersWhileOff} ring(s) on the switched-off hulls met a neighbour at the shared origin");
    }

    // The world's emplacement roster, torn down with the pool that owns it. The pool never ticks
    // here; it exists because an emplacement is built holding one.
    private static void WithEmplacements(TestContext ctx, TestWorld world, TurretDefs defs,
        WeaponDefs weapons, TextureArchive textures, System.Action<TurretEmplacementRuntime> body)
    {
        ProjectilePool? pool = null;
        TurretEmplacementRuntime? emplacements = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);
            emplacements = new TurretEmplacementRuntime(defs, weapons,
                (pattern, scope) => world.Runtime.FindNodes(pattern, scope), live,
                world.Runtime.WorldRoot);
            body(emplacements);
        }
        finally
        {
            emplacements?.Free();
            pool?.Free();
        }
    }

    // The Instant Action reading: both hulls switched off, so a belly ring's upward ray answers
    // with a neighbour parked at the same origin or with nothing, never with its own hull.
    private static void CensusOff(TestContext ctx, TestWorld world,
        TurretEmplacementRuntime runtime, StringBuilder report, Tally tally)
    {
        var space = ctx.Host.GetWorld3D().DirectSpaceState;
        foreach (string hullName in Hulls)
        {
            if (HullOf(ctx, world, ctx.Chapter, hullName) is not { } hull)
            {
                continue;
            }
            ctx.Check(!hull.IsVisibleInTree(),
                $"{ctx.Chapter}/{ctx.Mission} switches '{hullName}' off, so it is not solid");
            if (hull.IsVisibleInTree())
            {
                continue;
            }
            tally.HullsOff++;
            foreach (var ring in BellyRings(ctx, runtime, hull, ctx.Chapter, hullName))
            {
                var hit = ProbeUp(space, ring, new[] { hull });
                report.AppendLine($"{ctx.Chapter}/{ctx.Mission} {hullName} {SiteNameOf(ring)} off: hit={hit.Name} own={hit.OwnHull} at={hit.Distance:0.00} m");
                ctx.Check(!hit.OwnHull,
                    $"{ctx.Chapter} {hullName} {SiteNameOf(ring)} meets nothing of its switched-off hull, hit='{hit.Name}'");
                if (hit.OwnHull)
                {
                    tally.RingsOverHullWhileOff++;
                }
                else if (hit.Name != NoHit)
                {
                    tally.StrangersWhileOff++;
                }
            }
        }
    }

    // The multiplayer reading: the hulls are the mode's own furniture, so every belly ring must
    // find hull above it.
    private static void CensusLive(TestContext ctx, TestWorld world,
        TurretEmplacementRuntime runtime, string chapter, StringBuilder report, Tally tally)
    {
        var space = ctx.Host.GetWorld3D().DirectSpaceState;
        var standing = new List<Node3D>();
        var ringsByHull = new List<List<TurretController>>();
        foreach (string hullName in Hulls)
        {
            if (HullOf(ctx, world, chapter, hullName) is not { } hull)
            {
                continue;
            }
            ctx.Check(hull.IsVisibleInTree(), $"{chapter}/{LiveMission} keeps '{hullName}' standing");
            if (!hull.IsVisibleInTree())
            {
                continue;
            }
            tally.LiveHulls++;
            standing.Add(hull);
            ringsByHull.Add(BellyRings(ctx, runtime, hull, chapter, hullName));
        }

        for (int i = 0; i < standing.Count; i++)
        {
            foreach (var ring in ringsByHull[i])
            {
                var hit = ProbeUp(space, ring, standing);
                report.AppendLine($"{chapter}/{LiveMission} {standing[i].Name} {SiteNameOf(ring)} at={ring.WorldPosition}: hit={hit.Name} own={hit.OwnHull} at={hit.Distance:0.00} m");
                ctx.Check(hit.OwnHull,
                    $"{chapter} {standing[i].Name} {SiteNameOf(ring)} stands under multiplayer-zeppelin hull, hit='{hit.Name}' at {hit.Distance:0.00} m");
                if (hit.OwnHull)
                {
                    tally.RingsOverHull++;
                }
            }
        }

        // Why the ray cannot say WHICH of the two hulls it met: nothing in the multiplayer setup
        // places either, so both stand at the world origin with their rings in the same spots.
        if (ringsByHull.Count == Hulls.Length && Coincident(ringsByHull[0], ringsByHull[1]))
        {
            tally.CoincidentHulls++;
        }
    }

    // Whether two rosters of belly rings stand in the same places, ring for ring.
    private static bool Coincident(List<TurretController> a, List<TurretController> b)
    {
        if (a.Count != b.Count || a.Count == 0)
        {
            return false;
        }
        var second = b.ToDictionary(SiteNameOf, t => t.WorldPosition);
        return a.All(t => second.TryGetValue(SiteNameOf(t), out var p)
                          && p.DistanceTo(t.WorldPosition) < 0.01f);
    }

    private static Node3D? HullOf(TestContext ctx, TestWorld world, string chapter, string hullName)
    {
        var found = world.Runtime.FindNodes(hullName);
        ctx.Same(1, found.Count, $"{chapter} carries one '{hullName}'");
        return found.Count == 1 ? found[0] : null;
    }

    private static List<TurretController> BellyRings(TestContext ctx,
        TurretEmplacementRuntime runtime, Node3D hull, string chapter, string hullName)
    {
        var belly = runtime.Emplacements
            .Where(t => t.Site is { } site && hull.IsAncestorOf(site)
                        && BellySites.Contains(SiteNameOf(t)))
            .ToList();
        ctx.Same(BellySites.Length, belly.Count,
            $"{chapter} {hullName} carries its three belly rings");
        return belly;
    }

    // The ring's own upward line of sight, blind to its first MountSkirtM the way the shipped
    // sight-line rule is, so the ring's own rig is never the answer. Straight up in world space:
    // a multiplayer hull parks level, so the gondola skin is what stands over a belly ring.
    private static (string Name, bool OwnHull, float Distance) ProbeUp(
        PhysicsDirectSpaceState3D space, TurretController ring, IReadOnlyList<Node3D> hulls)
    {
        var from = ring.WorldPosition;
        var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
            from + (Vector3.Up * TurretController.MountSkirtM),
            from + (Vector3.Up * ProbeReachM), CollisionLayers.World));
        if (hit.Count == 0)
        {
            return (NoHit, false, float.PositiveInfinity);
        }
        var node = hit["collider"].Obj as Node;
        string name = node == null ? "?" : WorldCollision.OwnerOf(node).Name.ToString();
        bool own = node is Node3D solid
                   && hulls.Any(h => solid == h || h.IsAncestorOf(solid));
        return (name, own, from.DistanceTo((Vector3)hit["position"]));
    }

    // The world node an emplacement stands on, off the label the builder stamped: "TITLE@site".
    private static string SiteNameOf(TurretController ring) =>
        ring.Label[(ring.Label.IndexOf('@') + 1)..];

    // The run's counters, a class so a world-scoped closure can add to them.
    private sealed class Tally
    {
        public int LiveHulls;
        public int HullsOff;
        public int RingsOverHull;
        public int RingsOverHullWhileOff;
        public int StrangersWhileOff;
        public int CoincidentHulls;
    }
}
