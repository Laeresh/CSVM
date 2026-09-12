using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>The pilot's three class keys over a session that holds all three classes at once:
/// C3/M01's live zeppelin, that chapter's own gun emplacements switched on, and hostile and
/// friendly aeroplanes around them, read by two panes flying 4 km apart. Each key walks its own
/// class and reaches every entry of it, a class change lands on the head of the class it changes
/// to, and neither pane's key moves the other pane's pick.</summary>
internal static class TargetClassCycleSuites
{
    private const string Chapter = "C3";
    private const string Mission = "M01";

    // C3/M01's two records: one is authored `deactivated` and wakes on OBJECTIVE39, the other
    // flies from the first frame and is the one whose parts this suite cycles.
    private const string LiveZep = "piratezep";

    // The two panes, 4 km apart along the mission's own axis, each with a hostile 300 m off its
    // own nose. Far enough apart that the same two hostiles sort into opposite orders, which is
    // how a shared pick would show itself.
    private static readonly Vector3 PaneGap = new(0f, 0f, 4000f);
    private static readonly Vector3 OffTheNose = new(0f, 0f, -300f);
    private static readonly Vector3 OffTheWing = new(-500f, 0f, 0f);

    [Suite("target-class-cycle",
        "the three class keys over C3/M01, which carries all three classes at once: the "
        + "Enemy/Objective key walks the hostile aeroplanes and wraps, the Ally key the friendly "
        + "ones, and the Non-Aircraft key the live zeppelin's own parts together with the "
        + "chapter's switched-on gun emplacements, every entry of it a structure or a turret and "
        + "never an aeroplane; a class change lands on the head of the cycle it changes to; "
        + "Target Nothing stays cleared through a rebuild until a class key; and two panes "
        + "4 km apart auto-acquire different hostiles, one pane's step leaving the other's pick "
        + "and the other's class alone")]
    internal static void TargetClassCycle(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, Chapter);
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, Chapter, Mission);
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, Chapter);
        ctx.RequireData(chapterZrdr, $"{Chapter} zrdr");
        ctx.RequireData(missionZrdr, $"{Chapter}/{Mission} zrdr");
        ctx.RequireData(texturesPath, $"{Chapter} textures");

        var zepDefs = Zeppelins.Load(missionZrdr);
        var nets = AiNets.Load(chapterZrdr);
        var turretDefs = TurretDefs.Load(ctx.ZrdrPath);
        var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
        if (!zepDefs.Any(d => d.Node == LiveZep))
        {
            throw new SuiteSkippedException($"{Chapter}/{Mission} carries no '{LiveZep}' record");
        }

        var report = new StringBuilder();
        ctx.WithWorld(Chapter, collision: false, Mission, world =>
        {
            var textures = new TextureArchive(texturesPath);
            ProjectilePool? live = null;
            ZeppelinRuntime? zeps = null;
            TurretEmplacementRuntime? emplacements = null;
            var switched = new List<Node3D>();
            var rigs = new List<FlightController>();
            try
            {
                var pool = live = new ProjectilePool(textures, null, null);
                ctx.Host.AddChild(pool);
                var runtime = zeps = new ZeppelinRuntime(zepDefs,
                    name => world.Runtime.FindNodes(name, null) is { Count: > 0 } hits ? hits[0] : null,
                    nets);
                ctx.Host.AddChild(runtime);
                runtime.WireDamage(world.Runtime);

                var parts = new List<AimCandidate>();
                runtime.CollectTargetParts(parts);
                ctx.Check(parts.Count > 0,
                    $"'{LiveZep}' offers its own parts to the target pool, count={parts.Count}");
                if (parts.Count == 0)
                {
                    return;
                }

                emplacements = new TurretEmplacementRuntime(turretDefs, weapons,
                    (pattern, scope) => world.Runtime.FindNodes(pattern, scope), pool,
                    world.Runtime.WorldRoot);
                foreach (var site in emplacements.Emplacements
                             .Where(t => !t.Alive).Select(t => t.Site).OfType<Node3D>().Distinct())
                {
                    world.Runtime.SetTargetActive(site, true);
                    switched.Add(site);
                }

                int liveGuns = emplacements.Emplacements.Count(t => t.Alive);
                ctx.Check(liveGuns > 0,
                    $"{Chapter} places gun emplacements the pilot can lock onto once their sites are on, alive={liveGuns} of {emplacements.Count}");

                // The two panes and the aeroplanes around them, anchored on the zeppelin the
                // non-aircraft cycle is built from so every class sits in one piece of airspace.
                var p1Pose = parts[0].Position + PaneGap;
                var p2Pose = p1Pose + PaneGap;
                var p1Self = Rig(rigs, AimAssist.PlayerTeam);
                var p2Self = Rig(rigs, AimAssist.PlayerTeam);
                var hostileA = Rig(rigs, InstantActionRuntime.EnemyTeam);
                var hostileB = Rig(rigs, InstantActionRuntime.EnemyTeam);
                var wingman = Rig(rigs, AimAssist.PlayerTeam);

                var scan = new AimCandidateSet();
                Offer(scan, p1Self, p1Pose);
                Offer(scan, p2Self, p2Pose);
                Offer(scan, hostileA, p1Pose + OffTheNose);
                Offer(scan, hostileB, p2Pose + OffTheNose);
                Offer(scan, wingman, p1Pose + OffTheWing);
                pool.CollectTurrets(scan);

                var p1 = new Pane(scan, parts, p1Self, p1Pose);
                var p2 = new Pane(scan, parts, p2Self, p2Pose);
                report.AppendLine($"{Chapter}/{Mission}: {parts.Count} zeppelin part(s), {liveGuns} emplacement(s), "
                    + $"cycles enemy={p1.Selection.Pool.Enemy.Count} ally={p1.Selection.Pool.Ally.Count} "
                    + $"nonAircraft={p1.Selection.Pool.NonAircraft.Count}");

                CheckCycleMembership(ctx, p1, parts.Count + liveGuns, hostileA, hostileB, p2Self, wingman);
                CheckEachKeyWalksItsClass(ctx, p1);
                CheckClassChangeLandsOnTheHead(ctx, p1);
                CheckClearStaysCleared(ctx, p1);
                CheckPanesAreIndependent(ctx, p1, p2);
                ctx.Note($"{Chapter}/{Mission}: three class cycles of {p1.Selection.Pool.Enemy.Count}/{p1.Selection.Pool.Ally.Count}/{p1.Selection.Pool.NonAircraft.Count}, each key walking its own");
            }
            finally
            {
                foreach (var site in switched)
                {
                    world.Runtime.SetTargetActive(site, false);
                }

                foreach (var rig in rigs)
                {
                    rig.Free();
                }

                emplacements?.Free();
                zeps?.Free();
                live?.Free();
                textures.Dispose();
            }
        });
        ctx.WriteArtifact($"test-target-class-cycle-{Chapter}-{Mission}.txt", report.ToString());
    }

    // Each cycle holds its own class and nothing else. The non-aircraft one is the claim the item
    // was filed on: the zeppelin's parts and the world's guns, and never an aeroplane.
    private static void CheckCycleMembership(TestContext ctx, Pane pane, int nonAircraftCount,
        object hostileA, object hostileB, object otherPane, object wingman)
    {
        var pool = pane.Selection.Pool;
        ctx.Check(pool.Enemy.Count == 2 && Holds(pool.Enemy, hostileA) && Holds(pool.Enemy, hostileB),
            $"the Enemy/Objective cycle holds the two hostile aeroplanes and nothing else, count={pool.Enemy.Count}");
        ctx.Check(pool.Ally.Count == 2 && Holds(pool.Ally, otherPane) && Holds(pool.Ally, wingman),
            $"the Ally cycle holds the wingman and the other pane's own aeroplane, count={pool.Ally.Count}");
        ctx.Check(pool.NonAircraft.Count == nonAircraftCount,
            $"the Non-Aircraft cycle holds every zeppelin part and every live emplacement, count={pool.NonAircraft.Count} of {nonAircraftCount}");
        ctx.Check(pool.NonAircraft.All(t => t.Kind is AimTargetKind.Structure or AimTargetKind.Turret),
            $"…each one a structure or a turret, never an aeroplane");
        ctx.Check(pool.NonAircraft.Any(t => t.Kind == AimTargetKind.Structure)
                  && pool.NonAircraft.Any(t => t.Kind == AimTargetKind.Turret),
            $"…and the one key reaches both kinds, the airship's parts and the guns on the ground");
    }

    // One key per class, each walking the whole of its own cycle and wrapping. This is what the
    // shipped keymap's T, Y and U do, dispatched in FlightController.StepTargeting.
    private static void CheckEachKeyWalksItsClass(TestContext ctx, Pane pane)
    {
        foreach (var cls in new[] { TargetClass.Enemy, TargetClass.Ally, TargetClass.NonAircraft })
        {
            pane.Home(cls);
            var expected = Sources(pane.Selection.Ordered);
            var walked = new List<object> { pane.Selection.Current!.Value.Source! };
            for (int i = 1; i < expected.Count; i++)
            {
                pane.Press(cls);
                walked.Add(pane.Selection.Current!.Value.Source!);
            }

            ctx.Check(expected.Count > 1 && walked.SequenceEqual(expected),
                $"the {cls} key walks all {expected.Count} of that cycle in the decoded order");
            pane.Press(cls);
            ctx.Check(ReferenceEquals(pane.Selection.Current!.Value.Source, expected[0]),
                $"…and the next press wraps back to its head");
        }
    }

    private static void CheckClassChangeLandsOnTheHead(TestContext ctx, Pane pane)
    {
        pane.Home(TargetClass.NonAircraft);
        var head = pane.Selection.Current!.Value.Source;
        pane.Home(TargetClass.Enemy);
        pane.Press(TargetClass.Enemy);
        pane.Press(TargetClass.NonAircraft);
        ctx.Check(ReferenceEquals(pane.Selection.Current!.Value.Source, head),
            $"a class change lands on the HEAD of the cycle it changes to, whatever index the pilot had reached in the one they left");
    }

    private static void CheckClearStaysCleared(TestContext ctx, Pane pane)
    {
        pane.Selection.Clear();
        pane.Rebuild();
        ctx.Check(pane.Selection.ActiveClass == null && pane.Selection.Current == null
                  && pane.Selection.Pool.Count == 0,
            $"Target Nothing empties the pool and STAYS empty through a rebuild, which is what keeps the clear cleared");
        pane.Press(TargetClass.NonAircraft);
        ctx.Check(pane.Selection.ActiveClass == TargetClass.NonAircraft
                  && pane.Selection.Current is { Kind: AimTargetKind.Structure or AimTargetKind.Turret },
            $"…and a class key is the way back in, on that key's own cycle");
    }

    private static void CheckPanesAreIndependent(TestContext ctx, Pane p1, Pane p2)
    {
        p1.Home(TargetClass.Enemy);
        p2.Home(TargetClass.Enemy);
        ctx.Check(p1.Selection.Current is { } a && p2.Selection.Current is { } b
                  && !a.IsSameTarget(b),
            $"each pane sorts the same two hostiles against its own pose and holds its own pick");

        var p2Was = p2.Selection.Current!.Value.Source;
        p1.Press(TargetClass.Enemy);
        ctx.Check(ReferenceEquals(p2.Selection.Current!.Value.Source, p2Was),
            $"…P1's step moves P1's marker and leaves P2's where it was");
        p2.Press(TargetClass.NonAircraft);
        ctx.Check(p2.Selection.ActiveClass == TargetClass.NonAircraft
                  && p1.Selection.ActiveClass == TargetClass.Enemy,
            $"…and P2 changing class leaves P1 on the cycle P1 chose");
    }

    private static List<object> Sources(IReadOnlyList<TargetRef> cycle)
    {
        var into = new List<object>(cycle.Count);
        foreach (var target in cycle)
        {
            into.Add(target.Source!);
        }

        return into;
    }

    private static bool Holds(IReadOnlyList<TargetRef> cycle, object source) =>
        cycle.Any(t => ReferenceEquals(t.Source, source));

    private static FlightController Rig(List<FlightController> owned, int team)
    {
        var rig = new FlightController { IsHumanPiloted = false, Team = team };
        owned.Add(rig);
        return rig;
    }

    private static void Offer(AimCandidateSet scan, FlightController rig, Vector3 at) =>
        scan.AddVehicle(at, Vector3.Zero, rig.Team, live: true, rig);

    // One pilot's pane: its own selection over the shared candidates, sorted against its own pose.
    // A press and the rebuild that publishes it are separate calls because the original's handler
    // steps the list the last frame built and the next frame's pass is what shows the result.
    private sealed class Pane
    {
        private readonly AimCandidateSet _scan;
        private readonly IReadOnlyList<AimCandidate> _parts;
        private readonly object _self;
        private readonly Vector3 _pose;

        public Pane(AimCandidateSet scan, IReadOnlyList<AimCandidate> parts, object self, Vector3 pose)
        {
            _scan = scan;
            _parts = parts;
            _self = self;
            _pose = pose;
            Rebuild();
        }

        public TargetSelection Selection { get; } = new();

        public void Rebuild() =>
            Selection.Rebuild(_scan, _parts, AimAssist.PlayerTeam, _self, _pose, Basis.Identity);

        // The cycle's head, whatever the pilot had reached: the decoded per-class Nearest, which
        // CSVM binds no key to. Here it is the setup step that makes a walk start at a known entry.
        public void Home(TargetClass cls)
        {
            Selection.Nearest(cls);
            Rebuild();
        }

        public void Press(TargetClass cls)
        {
            if (cls == TargetClass.Enemy)
            {
                Selection.NextEnemy();
            }
            else
            {
                Selection.Next(cls);
            }

            Rebuild();
        }
    }
}
