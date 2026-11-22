using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSVM.Extraction;
using CSVM.Flight.Airframe;
using CSVM.Flight.Weapons;
using CSVM.Launch;
using CSVM.Mech3;
using CSVM.Session.Objectives;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The curated list in the modes that run no campaign director. C1's Instant Action table
/// flags the radio tower and its zeppelin match the two rearm bases. Each reaches the pilot's
/// cycles off the table's own keys, with no graph behind them. The world's other destructibles,
/// crates among them, sit in the pilot's structure scan and reach no cycle. C5's two match tables
/// flag their rearm nodes themselves, on the cycle each table picks, and Enhanced adds the
/// Deathmatch base to the Enemy cycle. The last arm is the binding rule, which a campaign launch
/// whose profile did not load also passes.</summary>
internal static class ModeTargetTableSuites
{
    private const string Chapter = "C1";

    // The radio tower, flagged `other_target` by six IA1 tables. ⚠ Read it in C1 only: the other
    // five chapters' worlds build no node of this name, so their tables offer nothing
    // (docs/org/targeting.md).
    private const string RadioTower = "ap_transmitter";

    // C5 has no rabaset1 building, so its tables flag the rearm nodes themselves. MP1 flags
    // rearm_node_1 other_target, where the other chapters' MP1 flag racmplx objective, and Enhanced
    // puts it on both cycles (docs/org/targeting.md).
    private const string C5 = "C5";
    private const string C5RearmBase = "rearm_node_1";
    private const string C5MovedNode = "rearm_node_2";

    private static readonly string[] RearmBases = { "zep_rearm_node_1", "zep_rearm_node_2" };
    private static readonly string[] MatchAirships = { "multiplayer1zep", "multiplayer2zep" };

    // Unflagged world objects of the same chapter: what a pool widened to every structure would put
    // on the cycle beside the flagged ones.
    private static readonly string[] Crates = { "crate10", "crate11", "crate12" };

    private static readonly string[] NoKeys = Array.Empty<string>();

    // MP2's objectives less the carried flags cs_flg_light1 and 2. The flag runtime places their
    // markers, so a feed read without one offers nothing for them.
    private static readonly string[] C5FlagMatchObjectives =
    {
        "ctf_1", "ctf_2", "cs_flag_1", "cs_flag_2", "rearm_node_1", "rearm_node_2",
    };

    [Suite("mode-target-table",
        "the mission target table of the director-free modes, read over C1's and C5's own worlds: the "
        + "Instant Action table's one other_target entry, the radio tower, reaches the pilot's "
        + "Non-Aircraft cycle and neither other cycle; the zeppelin match's two rearm bases reach "
        + "it too while the same table's two airships ride the Enemy cycle as objectives; each "
        + "cycle holds its own flagged half and nothing else; not one of the hundred-odd "
        + "destructibles in the same scan, the chapter's crates among them, reaches any cycle; "
        + "C5's Deathmatch table puts its rearm base on the Non-Aircraft cycle at the spot mp1.gw "
        + "moved the second node to, and its Capture the Flag table both rearm bases on the Enemy "
        + "cycle beside the flags; all of that under Faithful, and under Enhanced the same but for "
        + "C5's Deathmatch base riding the Enemy cycle as an objective too, so E and R both reach it; "
        + "and the binding rule itself, which a campaign launch flying without a director passes")]
    internal static void ModeTargetTable(TestContext ctx)
    {
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        ZrdrSet chapterZrdr = ctx.ChapterZrdr(Chapter);
        ctx.RequireData(chapterZrdr, $"{Chapter} zrdr");
        var messages = ctx.Data.Messages;

        var report = new StringBuilder();
        bool wasEnhanced = GraphicsMode.Enhanced;
        try
        {
            Mode(ctx, messages, report, Chapter, "IA1", NoKeys, new[] { RadioTower }, Crates);
            Mode(ctx, messages, report, Chapter, "MP3", MatchAirships, RearmBases, Crates);
            Mode(ctx, messages, report, C5, "MP1", NoKeys, new[] { C5RearmBase }, NoKeys,
                (world, pool) => C5BaseStandsWherePlaced(ctx, world, pool, report),
                enhancedBoth: new[] { C5RearmBase });
            Mode(ctx, messages, report, C5, "MP2", C5FlagMatchObjectives, NoKeys, NoKeys);
        }
        finally
        {
            GraphicsMode.Set(wasEnhanced);
        }

        WhoBindsTheTable(ctx, report);
        ctx.Note($"{Chapter}/IA1, {Chapter}/MP3, {C5}/MP1 and {C5}/MP2 offer their own targets.zrd flags with no director bound, and Enhanced adds {C5}/MP1's base to the Enemy cycle alone");
        ctx.WriteArtifact($"test-mode-target-table-{Chapter}.txt", report.ToString());
    }

    // Which sessions reach that feed at all. The director owns the channel when one exists, so a
    // campaign launch whose profile did not load flies director-free and still gets its mission's
    // table, where keying the rule on the launch left it with none.
    private static void WhoBindsTheTable(TestContext ctx, StringBuilder report)
    {
        var campaign = SessionSpec.Parse(new[]
        {
            "--campaign=csvm-golden:6", "--players=4",
            "--plane=player_bhawk,player_bhawk,player_bhawk,player_bhawk", "--det", "--mute",
        });
        ctx.Check(campaign.CampaignProfile != null && campaign.WorldMode,
            $"a --campaign= launch is a world flight that names a profile");
        ctx.Check(Binds(campaign, hasDirector: false),
            $"…and with no director behind it, because the profile did not load, it binds the mission's own table");
        ctx.Check(!Binds(campaign, hasDirector: true),
            $"…while the same launch with a director bound leaves the channel to the graph");

        var mode = SessionSpec.Parse(new[] { "--chapter=C1", "--mission=IA1" });
        ctx.Check(Binds(mode, hasDirector: false),
            $"Instant Action still binds it, the case the rule was written for");
        ctx.Check(!Binds(mode, hasDirector: false, stunting: true),
            $"…a stunt run does not, since it owns the same channel per pane");
        ctx.Check(!Binds(SessionSpec.Parse(new[] { "--stage=empty" }), hasDirector: false),
            $"…and the empty stage does not, having no mission to read a table from");
        ctx.Check(!Binds(mode, hasDirector: false, hasWorld: false) && !Binds(mode, hasDirector: false, rigs: 0),
            $"…and neither does a session with no world runtime or no rig to aim");
        report.AppendLine("binding: campaign without a director yes, with one no; "
            + "IA1 yes; stunt, empty stage, worldless and rigless no");
    }

    private static bool Binds(SessionSpec spec, bool hasDirector, bool stunting = false,
        bool hasWorld = true, int rigs = 1) =>
        ObjectiveReadouts.BindsMissionTargetTable(spec, hasDirector, stunting, hasWorld, rigs);

    // One mode's session: its own table, the director-free feed over the built world, and the
    // three cycles a pilot standing in it would walk. Faithful is the original's table, and
    // Enhanced differs only by `enhancedBoth` also riding the Enemy cycle.
    private static void Mode(TestContext ctx, Messages messages, StringBuilder report, string chapter,
        string mission, IReadOnlyList<string> objectives, IReadOnlyList<string> others,
        IReadOnlyList<string> crates, Action<TestWorld, TargetPool>? also = null,
        IReadOnlyList<string>? enhancedBoth = null)
    {
        ZrdrSet missionZrdr = ctx.MissionZrdr(chapter, mission);
        ctx.RequireData(missionZrdr, $"{chapter}/{mission} zrdr");
        var targets = MissionTargets.Load(missionZrdr, ctx.ChapterZrdr(chapter));

        ctx.WithWorld(chapter, collision: false, mission, world =>
        {
            // One feed for both modes, as a live switch keeps it.
            var sites = new ObjectiveSites(messages, targets, world.Runtime);

            // The pilot's own third pool, collected the way FlightController collects it. Nothing
            // reads it into the cycles; it is here to show what the curated list keeps out.
            var scan = new AimCandidateSet();
            scan.AddStructures(world.Runtime.Destructibles);

            var self = new FlightController { IsHumanPiloted = false, Team = AimAssist.PlayerTeam };
            try
            {
                foreach (bool enhanced in new[] { false, true })
                {
                    GraphicsMode.Set(enhanced);
                    string where = $"{chapter}/{mission} under {(enhanced ? "Enhanced" : "Faithful")}";
                    var offered = new List<AimCandidate>();
                    sites.Collect(offered);
                    var selection = new TargetSelection();
                    selection.Rebuild(scan, null, AimAssist.PlayerTeam, self, Vector3.Zero,
                        Basis.Identity, offered);
                    var pool = selection.Pool;
                    report.AppendLine($"{where}: {targets.Count} table entr(ies), "
                        + $"{offered.Count} site(s) offered, cycles enemy={pool.Enemy.Count} "
                        + $"ally={pool.Ally.Count} nonAircraft={pool.NonAircraft.Count}, beside "
                        + $"{scan.Structures.Count} destructible(s) in the same scan");

                    var both = enhanced ? enhancedBoth ?? NoKeys : NoKeys;
                    CheckFlagged(ctx, where, targets, pool, objectives, others, both);
                    CheckBothKeysReach(ctx, where, selection, both);
                    if (!enhanced)
                    {
                        CheckTheUnflaggedStayOff(ctx, where, world, scan, pool, crates, report);
                        also?.Invoke(world, pool);
                    }
                }
            }
            finally
            {
                self.Free();
            }
        });
    }

    // Enhanced's match rearm base answers both keys: Nearest Enemy/Objective (E) and Nearest
    // Non-Aircraft (R) each land on it.
    private static void CheckBothKeysReach(TestContext ctx, string where, TargetSelection selection,
        IReadOnlyList<string> both)
    {
        foreach (string key in both)
        {
            foreach (var cls in new[] { TargetClass.Enemy, TargetClass.NonAircraft })
            {
                selection.Nearest(cls);
                selection.Resolve(Vector3.Zero, Basis.Identity);
                ctx.Check(selection.Current is { } current
                    && string.Equals(current.Name, key, StringComparison.OrdinalIgnoreCase),
                    $"{where}: Nearest {cls} lands on '{key}', read '{selection.Current?.Name}'");
            }
        }
    }

    // mp1.gw moves rearm_node_2 onto rearm_node_1. A marker within the rearm radius of the moved node
    // was read after the script placed the world, not at the gamez's 4.7 km split.
    private static void C5BaseStandsWherePlaced(TestContext ctx, TestWorld world, TargetPool pool,
        StringBuilder report)
    {
        var moved = ObjectiveSites.ResolveTarget(world.Runtime, ObjectiveTarget.Parse(C5MovedNode));
        ctx.Check(moved != null && moved.IsVisibleInTree(), $"{C5}/MP1 builds and shows '{C5MovedNode}'");
        if (moved == null || Find(pool.NonAircraft, C5RearmBase) is not { } site)
        {
            return;
        }

        float apart = site.Position.DistanceTo(moved.GlobalPosition);
        report.AppendLine(FormattableString.Invariant(
            $"{C5}/MP1: the base's marker stands {apart:0.00} m from the moved {C5MovedNode}"));
        ctx.Check(apart * apart <= Flight.Modes.RearmBases.InitialRadiusSquared,
            $"…and the Non-Aircraft cycle's base stands within the rearm radius of it, {apart:0.00} m, where mp1.gw placed it");
    }

    // Each flagged key on the cycle its own flag picks, and on no other. A key in `both` also
    // rides the Enemy cycle as an objective.
    private static void CheckFlagged(TestContext ctx, string where, MissionTargets targets,
        TargetPool pool, IReadOnlyList<string> objectives, IReadOnlyList<string> others,
        IReadOnlyList<string> both)
    {
        foreach (string key in others)
        {
            bool alsoEnemy = both.Contains(key, StringComparer.OrdinalIgnoreCase);
            ctx.Check(targets.For(key).OtherTarget && !targets.For(key).Objective,
                $"{where}'s own table flags '{key}' other_target");
            ctx.Same(1, Named(pool.NonAircraft, key),
                $"…and the director-free feed puts it on the Non-Aircraft cycle, once");
            ctx.Same(alsoEnemy ? 1 : 0, Named(pool.Enemy, key),
                $"…and on the Enemy cycle {(alsoEnemy ? "too, once" : "not at all")}");
            ctx.Same(0, Named(pool.Ally, key), $"…nor on the Ally cycle");
            ctx.Check(Find(pool.NonAircraft, key) is
            { Objective: false, Kind: AimTargetKind.Structure } site
                && site.DisplayName.Length > 0,
                $"…labelled off the table's own description rather than the bare node name");
            if (alsoEnemy)
            {
                ctx.Check(Find(pool.Enemy, key) is { Objective: true, Kind: AimTargetKind.Structure },
                    $"…where it is an objective, sorting ahead of every aircraft as racmplx does");
            }
        }

        foreach (string key in objectives)
        {
            ctx.Check(targets.For(key).Objective,
                $"{where}'s own table flags '{key}' objective");
            ctx.Same(1, Named(pool.Enemy, key),
                $"…so the same feed rides it on the Enemy cycle instead, once");
            ctx.Same(0, Named(pool.NonAircraft, key),
                $"…and never on the Non-Aircraft one");
        }

        // No aeroplane and no emplacement is in this scan, so each cycle's whole content is the
        // table's own flagged half and the counts are exact.
        ctx.Same(others.Count, pool.NonAircraft.Count,
            $"{where}'s Non-Aircraft cycle is its table's other_target entries and nothing else");
        ctx.Same(objectives.Count + both.Count, pool.Enemy.Count,
            $"…and its Enemy cycle the table's objective entries and nothing else, plus [{string.Join(", ", both)}]");
    }

    // The whole point of a curated list: the world's other destructibles are in the pilot's scan
    // and stay off every cycle, which a pool reading that scan would undo.
    private static void CheckTheUnflaggedStayOff(TestContext ctx, string where, TestWorld world,
        AimCandidateSet scan, TargetPool pool, IReadOnlyList<string> crates, StringBuilder report)
    {
        int inScan = 0;
        foreach (string key in crates)
        {
            var node = ObjectiveSites.ResolveTarget(world.Runtime, ObjectiveTarget.Parse(key));
            ctx.Check(node != null, $"'{key}' is a node {where} actually builds");
            if (node == null)
            {
                continue;
            }

            if (scan.Structures.Any(c => ReferenceEquals(AnchorOf(c.Source), node)))
            {
                inScan++;
            }

            ctx.Same(0, Anchored(pool.NonAircraft, node) + Anchored(pool.Enemy, node)
                + Anchored(pool.Ally, node) + Named(pool.NonAircraft, key),
                $"…and reaches no cycle in {where}, the table never naming it");
        }

        // How many of them the mode's own world has switched on varies by mission variant, so the
        // count is reported rather than asserted; what is asserted is that none of the scan reaches
        // a cycle at all.
        report.AppendLine($"{where}: {inScan} of {crates.Count} crate(s) live in the "
            + $"structure scan, none of them on any cycle");
        ctx.Check(scan.Structures.Count > pool.NonAircraft.Count,
            $"{where} scans {scan.Structures.Count} destructible(s) against a Non-Aircraft cycle of {pool.NonAircraft.Count}");
        ctx.Same(0, scan.Structures.Count(c => AnchorOf(c.Source) is { } a
                && (Anchored(pool.NonAircraft, a) + Anchored(pool.Enemy, a) + Anchored(pool.Ally, a)) > 0),
            $"…and not one of that scan reaches a cycle, which is what widening the pool to it would undo");
    }

    private static Node3D? AnchorOf(object? source) =>
        source is DestructibleRegistry.Instance inst && GodotObject.IsInstanceValid(inst.Anchor)
            ? inst.Anchor : null;

    private static int Anchored(IReadOnlyList<TargetRef> cycle, Node3D node) =>
        cycle.Count(t => ReferenceEquals(AnchorOf(t.Source), node));

    private static int Named(IReadOnlyList<TargetRef> cycle, string name) =>
        cycle.Count(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    private static TargetRef? Find(IReadOnlyList<TargetRef> cycle, string name)
    {
        foreach (var target in cycle)
        {
            if (string.Equals(target.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return target;
            }
        }

        return null;
    }
}
