using System.IO;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Campaign;
using CSVM.Session.InstantAction;
using CSVM.Session.Roster;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The HUD message stack the original posts a death into, asserted on a real aircraft
/// killed in C1: the four wording rules, the three colour arms, the slot geometry, the five
/// seconds a line lives, and the two notices that are not a death (the local player's crash and
/// the mission clock's expiry pair). Decode: docs/org/vehicleDamage.md "The kill message".</summary>
internal static class HudKillLineSuites
{
    // The profile the store arm seats, the pilot the wording checks name throughout.
    private const string PilotName = "Nathan Zachary";

    // A 1440p pane, so the reference geometry reads back unscaled.
    private static readonly Vector2 Pane = new(2560f, 1440f);

    [Suite("hud-kill-line",
        "the kill line the original posts top-centre when a vehicle dies: a real Medusa Kestrel " +
        "shot down in C1 reads 'Medusa Kestrel was shot down' in the enemy colour, the other " +
        "three wording rules (the viewer's own name, a wingman with no name, a non-aeroplane " +
        "destroyed) read as decoded, the name a sortie outside a campaign prints for its own " +
        "death is the profile the store last used and an empty store keeps the fall-through, " +
        "the stack keeps four lines a fifth of the way down with an " +
        "18 px pitch, a newer line pushes the older ones down carrying their own remaining time, " +
        "a repeat refreshes rather than duplicates, every line clears after five seconds of sim " +
        "time (a halted clock keeps it, a slow fixed-step frame spends one step, and the damage " +
        "dial's post-hit blink runs on the same sim step), and the " +
        "same stack takes the crash notice in the player's own colour and the mission clock's " +
        "'Mission LOST!' over 'Time Expired' in the default one, and a hull flown into the world " +
        "posts that notice alone with no kill line behind it")]
    internal static void HudKillLine(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(ctx.MessagesPath, $"message table");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var strings = Messages.Load(ctx.MessagesPath);
        string shotDown = strings.Get(HudMessages.ShotDownKey);
        string wingman = strings.Get(HudMessages.WingmanKey);
        string destroyed = strings.Get(HudMessages.DestroyedKey);
        ctx.Check(Rows(strings, HudMessages.ShotDownKey, HudMessages.WingmanKey, HudMessages.DestroyedKey),
            $"the three decoded rows read out of the string table as three wordings: '{shotDown}' / '{wingman}' / '{destroyed}'");
        if (!ctx.SyntheticData)
        {
            ctx.Check(shotDown == "was shot down" && wingman == "Wingman was shot down"
                      && destroyed == "was destroyed",
                $"the three decoded rows read out of the string table: '{shotDown}' / '{wingman}' / '{destroyed}'");
        }

        Wording(ctx, strings, shotDown, wingman, destroyed);
        Pilot(ctx, strings, shotDown, wingman);
        Colours(ctx);
        Geometry(ctx);

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        ProjectilePool? pool = null;
        FlightController? ai = null;
        HudMessages? stack = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            pool = live;
            ctx.Host.AddChild(live);

            var spec = SessionSpec.Parse(System.Array.Empty<string>());
            var inputs = new AircraftAssemblyResources
            {
                PlanesGamez = planesGamez,
                StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
                AiStatsFor = (plane, aiDef) => PlaneStats.LoadForAi(ctx.ZrdrPath, plane, aiDef),
                PaintRng = new RandomNumberGenerator(),
                ZrdrPath = ctx.ZrdrPath,
                StockLoadouts = StockLoadouts.Load(),
                WeaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null),
                WeaponMessages = strings,
                Textures = textures,
                Shakes = ShakeDefs.Load(ctx.ZrdrPath),
            };
            // worldEffects null!: never dereferenced with no CrashProgram/WorldScene, so the
            // spawner's crash-runtime block is skipped.
            var roster = new FlightRoster(FlightRosterPolicy.From(spec),
                new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof")),
                null!, ctx.Host, inputs,
                new FlightWorldBindings { Projectiles = live, Gamez = planesGamez },
                new HumanRosterBindings());

            stack = new HudMessages();
            ctx.Host.AddChild(stack);
            Stack(ctx, stack);
            Notices(ctx, stack, strings);
            SimClock(ctx, stack);
            GaugeBlinkClock(ctx, planesGamez, textures);

            // The session's own seam: the roster reports a death, and a death the hull was spent in
            // puts the line composed for that pane into the reading pane's stack.
            roster.VehicleDowned += (victim, _) =>
            {
                if (HudMessages.WordsKillLine(victim))
                {
                    HudMessages.PostKill(stack!, strings, victim, AimAssist.PlayerTeam,
                        victimIsViewer: false, viewerName: "Nathan Zachary");
                }
            };

            // The install flies the Medusa Kestrel the line was reported on. Any other tree flies its
            // own default airframe under its first AI flavour, and the line names it by its title.
            var at = new Vector3(0f, 500f, 0f);
            ai = roster.SpawnAi(new AiSpawn(ctx.SyntheticData ? ctx.PlaneName : "player_kestrel", at,
                at + Vector3.Forward, AiPilot.HoldingCourse(at, at + Vector3.Forward), Scheme: null,
                Team: InstantActionRuntime.EnemyTeam, AiDef: ctx.SyntheticData ? null : "medkestrel"));
            string name = PlaneRoster.PlaneDisplayName(ai.Stats!);
            ctx.Check(name.Length > 0, $"the spawned aircraft carries a title the line names it by: '{name}'");
            if (!ctx.SyntheticData)
            {
                ctx.Check(name == "Medusa Kestrel",
                    $"the spawned aircraft carries the title the line names it by: '{name}'");
            }
            ctx.Check(HudMessages.IsAeroplane(ai.Stats) && ai.Team == InstantActionRuntime.EnemyTeam,
                $"…as an aeroplane on an enemy team mode={ai.Stats?.VehicleMode ?? "<none>"} team={ai.Team}");

            bool impact = false;
            ai.GroundImpact += _ => impact = true;
            ctx.Check(HudMessages.WordsKillLine(ai),
                $"a hull still in the fight is one a death would word a line for");

            stack.Clear();
            ai.DebugForceCrash(0);
            ctx.Check(ai.Crashed, $"the aircraft is down crashed={ai.Crashed}");
            ctx.Check(impact,
                $"…and raised the ground impact the crash notice hangs off impact={impact}");
            ctx.Check(!HudMessages.WordsKillLine(ai) && stack.LineAt(0) == null,
                $"…flown into the world, so it posts the notice alone: '{stack.LineAt(0) ?? "<none>"}'");

            // The same real aircraft as the death that does word a line: a hull spent in the air.
            HudMessages.PostKill(stack, strings, ai, AimAssist.PlayerTeam, victimIsViewer: false,
                viewerName: "Nathan Zachary");
            ctx.Check(stack.LineAt(0) == $"{name} {shotDown}",
                $"its death posts the decoded line: '{stack.LineAt(0) ?? "<none>"}'");
            ctx.Check(stack.SideAt(0) == HudMessages.Side.Enemy,
                $"…in the enemy colour arm side={stack.SideAt(0)}");
            ctx.Check(Mathf.IsEqualApprox(stack.LifeAt(0), HudMessages.LineLife),
                $"…for the decoded five seconds life={stack.LifeAt(0):0.00}");

            stack.Advance(HudMessages.LineLife - 0.1f);
            ctx.Check(stack.LineAt(0) != null,
                $"the line is still up a tenth of a second short of its life");
            stack.Advance(0.2f);
            ctx.Check(stack.LineAt(0) == null,
                $"…and gone once the five seconds are spent: '{stack.LineAt(0) ?? "<none>"}'");
        }
        finally
        {
            stack?.Free();
            ai?.Free();
            pool?.Free();
            textures.Dispose();
        }
    }

    // The four rules, in the order the death routine forks them.
    private static void Wording(TestContext ctx, Messages strings, string shotDown, string wingman,
        string destroyed)
    {
        string ship = HudMessages.KillLine(strings, aeroplane: false, "Trawler",
            victimIsViewer: false, viewerName: "Nathan Zachary", victimOnViewerTeam: true);
        ctx.Check(ship == $"Trawler {destroyed}",
            $"a victim that is not an aeroplane is destroyed whatever its team: '{ship}'");

        string self = HudMessages.KillLine(strings, aeroplane: true, "Kestrel",
            victimIsViewer: true, viewerName: "Nathan Zachary", victimOnViewerTeam: true);
        ctx.Check(self == $"Nathan Zachary {shotDown}",
            $"the reading pane's own aircraft is named by its pilot: '{self}'");

        string mate = HudMessages.KillLine(strings, aeroplane: true, "Medusa Kestrel",
            victimIsViewer: false, viewerName: "Nathan Zachary", victimOnViewerTeam: true);
        ctx.Check(mate == wingman,
            $"an aeroplane on that pane's team is the wingman line, with no name before it: '{mate}'");

        string foe = HudMessages.KillLine(strings, aeroplane: true, "Medusa Kestrel",
            victimIsViewer: false, viewerName: "Nathan Zachary", victimOnViewerTeam: false);
        ctx.Check(foe == $"Medusa Kestrel {shotDown}",
            $"any other aeroplane is named by its own title: '{foe}'");

        string nameless = HudMessages.KillLine(strings, aeroplane: true, "Kestrel",
            victimIsViewer: true, viewerName: null, victimOnViewerTeam: true);
        ctx.Check(nameless == wingman,
            $"an unset pilot name falls through to the team rule as the original's does: '{nameless}'");
    }

    // Where the name in the viewer's own line comes from outside a campaign: the profile the store
    // last used, which is the player the original's startup reads back. Over a scratch store of its
    // own, since no suite may touch user://Profiles.
    private static void Pilot(TestContext ctx, Messages strings, string shotDown, string wingman)
    {
        string dir = Path.Combine(ctx.ScratchDir, "hud-kill-line", "Profiles");
        try
        {
            var store = new CampaignProfileStore(dir);
            string none = HudMessages.KillLine(strings, aeroplane: true, "Kestrel",
                victimIsViewer: true, store.LastPlayedPilotName, victimOnViewerTeam: true);
            ctx.Check(store.LastPlayedPilotName == null && none == wingman,
                $"a store that has seated nobody names no pilot, so the player's own death keeps the team wording: '{none}'");

            store.Save(CampaignProfileDef.NewProfile(PilotName));
            store.RecordLastPlayed(PilotName);
            string named = HudMessages.KillLine(strings, aeroplane: true, "Kestrel",
                victimIsViewer: true, store.LastPlayedPilotName, victimOnViewerTeam: true);
            ctx.Check(store.LastPlayedPilotName == PilotName && named == $"{PilotName} {shotDown}",
                $"…and the profile last used is the name that line carries instead: '{named}'");

            Directory.Delete(store.DirFor(PilotName), recursive: true);
            ctx.Check(store.LastPlayedPilotName == null,
                $"…a record whose profile is gone names nobody: '{store.LastPlayedPilotName ?? "<none>"}'");
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    // The two lines that are not a death: the local player's crash notice and the mission clock's
    // pair. The Dogfight death lines follow, with and without a killer.
    private static void Notices(TestContext ctx, HudMessages stack, Messages strings)
    {
        string crash = strings.Get(HudMessages.CrashKey);
        string expired = strings.Get(HudMessages.TimeExpiredKey);
        string lost = strings.Get(HudMessages.MissionLostKey);
        ctx.Check(Rows(strings, HudMessages.CrashKey, HudMessages.TimeExpiredKey, HudMessages.MissionLostKey),
            $"the three notice rows read out of the string table as three wordings: '{crash}' / '{expired}' / '{lost}'");
        if (!ctx.SyntheticData)
        {
            ctx.Check(crash == "Fatal Crash!" && expired == "Time Expired" && lost == "Mission LOST!",
                $"the three notice rows read out of the string table: '{crash}' / '{expired}' / '{lost}'");
        }

        HudMessages.PostCrash(stack, strings);
        ctx.Check(stack.LineAt(0) == crash && stack.SideAt(0) == HudMessages.Side.Friendly,
            $"the crash notice posts in the player's own colour: '{stack.LineAt(0) ?? "<none>"}' side={stack.SideAt(0)}");
        stack.Clear();

        HudMessages.PostTimeExpired(stack, strings);
        ctx.Check(stack.LineAt(0) == lost && stack.LineAt(1) == expired,
            $"the clock's expiry reads lost over expired: '{stack.LineAt(0) ?? "<none>"}' / '{stack.LineAt(1) ?? "<none>"}'");
        ctx.Check(stack.SideAt(0) == HudMessages.Side.Neutral
                  && stack.SideAt(1) == HudMessages.Side.Neutral,
            $"…both in the stack's default colour side={stack.SideAt(0)}/{stack.SideAt(1)}");
        stack.Clear();

        // The Dogfight lines are the table's two templates filled with the pilots' names; the
        // install pins the shipped wordings too.
        ctx.Check(Rows(strings, HudMessages.SelfDestroyedKey, HudMessages.DestroyedByKey),
            $"the two Dogfight templates read out of the string table: '{strings.Get(HudMessages.SelfDestroyedKey)}' / '{strings.Get(HudMessages.DestroyedByKey)}'");
        string selfLoss = strings.Format(HudMessages.SelfDestroyedKey, "P2");
        string killedBy = strings.Format(HudMessages.DestroyedByKey, "P1");
        HudMessages.PostMatchKill(stack, strings, HudMessages.MatchDeath.NoKiller, "P2", null);
        ctx.Check(stack.LineAt(0) == selfLoss && selfLoss.Contains("P2") && stack.LineAt(1) == null,
            $"a Dogfight death no killer owns still words itself from its template, with no killer named: '{stack.LineAt(0) ?? "<none>"}'");
        if (!ctx.SyntheticData)
        {
            ctx.Check(stack.LineAt(0) == "P2 Self-Destroyed" && stack.LineAt(1) == null,
                $"a Dogfight death no killer owns still words itself, with no killer named: '{stack.LineAt(0) ?? "<none>"}'");
        }
        stack.Clear();

        HudMessages.PostMatchKill(stack, strings, HudMessages.MatchDeath.Killer, "P2", "P1");
        ctx.Check(stack.LineAt(0) == "P2" && stack.LineAt(1) == killedBy && killedBy.Contains("P1")
                  && stack.SideAt(0) == HudMessages.Side.Enemy && stack.SideAt(1) == HudMessages.Side.Enemy,
            $"a Dogfight kill reads the victim over its killer's template, both in the enemy arm: '{stack.LineAt(0) ?? "<none>"}' / '{stack.LineAt(1) ?? "<none>"}' side={stack.SideAt(0)}/{stack.SideAt(1)}");
        if (!ctx.SyntheticData)
        {
            ctx.Check(stack.LineAt(0) == "P2" && stack.LineAt(1) == "Destroyed by P1"
                      && stack.SideAt(0) == HudMessages.Side.Enemy && stack.SideAt(1) == HudMessages.Side.Enemy,
                $"a Dogfight kill reads the victim over its killer, both in the enemy arm: '{stack.LineAt(0) ?? "<none>"}' / '{stack.LineAt(1) ?? "<none>"}' side={stack.SideAt(0)}/{stack.SideAt(1)}");
        }
        stack.Clear();
    }

    // A frame ages the stack by the session clock's step, never by its wall delta. A halted clock
    // keeps a line whole; a fixed-step frame spends one step however long it took.
    private static void SimClock(TestContext ctx, HudMessages stack)
    {
        const float SlowFrame = HudMessages.LineLife * 2f;
        var saved = GameClock.Current;
        var clock = new GameClock { Mode = GameClock.RunMode.FixedStep, Halted = true };
        GameClock.Current = clock;
        try
        {
            stack.Post("held", HudMessages.Side.Enemy);
            clock.BeginFrame(SlowFrame);
            stack._Process(SlowFrame);
            ctx.Check(stack.LineAt(0) == "held" && Mathf.IsEqualApprox(stack.LifeAt(0), HudMessages.LineLife),
                $"a halted clock keeps the line whole through a {SlowFrame:0} s frame life={stack.LifeAt(0):0.000}");

            clock.Halted = false;
            clock.BeginFrame(SlowFrame);
            stack._Process(SlowFrame);
            ctx.Check(stack.LineAt(0) == "held"
                      && Mathf.IsEqualApprox(stack.LifeAt(0), HudMessages.LineLife - GameClock.FixedDt),
                $"…and a running fixed-step clock spends one sim step of it, not the frame's wall time life={stack.LifeAt(0):0.000}");

            // ABLE-TO-FAIL CONTROL: with no session clock the same frame falls back to its wall delta.
            GameClock.Current = null;
            stack._Process(SlowFrame);
            ctx.Check(stack.LineAt(0) == null,
                $"ABLE-TO-FAIL CONTROL: with no session clock the wall delta spends the line: '{stack.LineAt(0) ?? "<none>"}'");
        }
        finally
        {
            GameClock.Current = saved;
            stack.Clear();
        }
    }

    // The damage dial's post-hit blink on the same clock. A 0.2 s step from phase 0 lands in the
    // 0.32 s blink's dark half. A wall frame is ten periods and an eighth. Two of them would spend
    // the 5 s window. Two or three put a wall-timed phase in the lit half. So a wall-timed blink
    // window or blink phase each fails the check alone.
    private static void GaugeBlinkClock(TestContext ctx, GameZ planesGamez, TextureArchive textures)
    {
        const float WallFrame = 10f * GaugeCluster.DamageBlinkPeriod + GaugeCluster.DamageBlinkPeriod / 8f;
        var parts = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName).DestroyableParts;
        var cluster = GaugeCluster.Build(planesGamez, ctx.PlaneName, textures, parts);
        // A synthetic tree carries no cockpit art to build a cluster from.
        if (cluster == null)
        {
            ctx.Check(ctx.SyntheticData, $"a real gauge cluster was built for '{ctx.PlaneName}'");
            return;
        }
        var saved = GameClock.Current;
        var clock = new GameClock { Mode = GameClock.RunMode.FixedStep, Halted = true, Scale = 12f };
        GameClock.Current = clock;
        ctx.Host.AddChild(cluster);
        try
        {
            foreach (var part in parts)
            {
                cluster.OnPartDamage(part.Name);
            }
            bool Dark() => parts.Any(p => cluster.ZoneTier(p.Name) < 0);

            string State() => $"blinkLeft={cluster.BlinkLeftAt(parts[0].Name):0.000} "
                + $"phase={cluster.BlinkPhase:0.000} tier={cluster.ZoneTier(parts[0].Name)}";

            clock.BeginFrame(WallFrame);
            cluster._Process(WallFrame);
            clock.Halted = false;
            clock.BeginFrame(WallFrame);
            cluster._Process(WallFrame);
            ctx.Check(Dark(),
                $"after a halted and a running {WallFrame:0.00} s frame, the struck zone blinks in its dark half: one {clock.FrameDt:0.00} s sim step aged it, the wall time did not {State()}");

            clock.Halted = true;
            clock.BeginFrame(WallFrame);
            cluster._Process(WallFrame);
            ctx.Check(Dark(), $"…and a halted clock holds the blink where it is {State()}");

            // ABLE-TO-FAIL CONTROL: with no session clock a frame falls back to its wall delta.
            GameClock.Current = null;
            cluster._Process(2 * WallFrame);
            ctx.Check(!Dark(),
                $"ABLE-TO-FAIL CONTROL: with no session clock the wall delta spends the blink window {State()}");
        }
        finally
        {
            GameClock.Current = saved;
            cluster.Free();
        }
    }

    // The three colour arms, tested in the decoded order: the enemy test runs first, so a pane on
    // an enemy team reads its own losses on that arm rather than as friendly.
    private static void Colours(TestContext ctx)
    {
        ctx.Check(HudMessages.SideOf(InstantActionRuntime.EnemyTeam, AimAssist.PlayerTeam)
            == HudMessages.Side.Enemy, $"a team above the player's is the enemy colour");
        ctx.Check(HudMessages.SideOf(AimAssist.PlayerTeam, AimAssist.PlayerTeam)
            == HudMessages.Side.Friendly, $"the pane's own team is the friendly colour");
        ctx.Check(HudMessages.SideOf(AimAssist.NeutralTeam, AimAssist.PlayerTeam)
            == HudMessages.Side.Neutral, $"the neutral team takes the stack's default colour");
        ctx.Check(HudMessages.SideOf(InstantActionRuntime.EnemyTeam, InstantActionRuntime.EnemyTeam)
            == HudMessages.Side.Enemy,
            $"…and the enemy test outranks the own-team one, which is the order of the decoded fork");
    }

    // Top centre, a fifth down, 18 px between slots, all three carried onto the 1440p reference.
    private static void Geometry(TestContext ctx)
    {
        var first = HudMessages.SlotAnchor(Pane, 0, 1f);
        ctx.Check(Mathf.IsEqualApprox(first.X, 1280f) && Mathf.IsEqualApprox(first.Y, 288f),
            $"slot 0 sits at the pane's centre a fifth of the way down: {first}");
        var second = HudMessages.SlotAnchor(Pane, 1, 1f);
        ctx.Check(Mathf.IsEqualApprox(second.X, first.X)
                  && Mathf.IsEqualApprox(second.Y - first.Y, 54f),
            $"…with the next slot one 18 px pitch below it: {second}");
        var half = HudMessages.SlotAnchor(Pane, 1, 0.5f);
        ctx.Check(Mathf.IsEqualApprox(half.Y - first.Y, 27f),
            $"…and the pitch scales with the pane while the anchor stays a fraction of it: {half}");
    }

    // Push-down, dedup, the long-line split, and expiry, all without a frame loop.
    private static void Stack(TestContext ctx, HudMessages stack)
    {
        ctx.Same(4, HudMessages.Slots, $"the stack holds the decoded number of lines");
        stack.Post("first", HudMessages.Side.Enemy);
        ctx.Check(stack.LineAt(0) == "first" && stack.LineAt(1) == null,
            $"one posted line occupies slot 0 alone");

        stack.Advance(2f);
        stack.Post("second", HudMessages.Side.Friendly);
        ctx.Check(stack.LineAt(0) == "second" && stack.LineAt(1) == "first",
            $"a newer line takes slot 0 and pushes the older one down");
        ctx.Check(Mathf.IsEqualApprox(stack.LifeAt(1), HudMessages.LineLife - 2f)
                  && stack.SideAt(1) == HudMessages.Side.Enemy,
            $"…which keeps its own remaining time and colour life={stack.LifeAt(1):0.00} side={stack.SideAt(1)}");

        stack.Advance(1f);
        stack.Post("second", HudMessages.Side.Friendly);
        ctx.Check(stack.LineAt(1) == "first"
                  && Mathf.IsEqualApprox(stack.LifeAt(0), HudMessages.LineLife),
            $"re-posting the line already in slot 0 refreshes it rather than pushing a duplicate");

        for (int i = 0; i < 6; i++)
        {
            stack.Post($"line {i}", HudMessages.Side.Neutral);
        }
        ctx.Check(stack.LineAt(0) == "line 5" && stack.LineAt(3) == "line 2",
            $"a sixth line has pushed the first two off the bottom: slot 3 is '{stack.LineAt(3) ?? "<none>"}'");

        stack.Clear();
        ctx.Check(stack.LineAt(0) == null, $"Clear empties the stack");

        // 54 characters; the last space at or before 48 is the one at 44, so the head is 44 long.
        stack.Post("aaaa bbbb cccc dddd eeee ffff gggg hhhh iiii jjjj kkkk", HudMessages.Side.Enemy);
        ctx.Check(stack.LineAt(0) == "aaaa bbbb cccc dddd eeee ffff gggg hhhh iiii"
                  && stack.LineAt(1) == "jjjj kkkk",
            $"a line past 48 characters splits at its last space and reads top to bottom: '{stack.LineAt(0) ?? ""}' / '{stack.LineAt(1) ?? ""}'");

        stack.Advance(HudMessages.LineLife + 0.01f);
        stack.Post("after", HudMessages.Side.Enemy);
        ctx.Check(stack.LineAt(0) == "after" && stack.LineAt(1) == null,
            $"a spent slot 0 has nothing to push, so the next line enters alone");
        stack.Clear();
    }

    // Whether the table words every key, each differently. A key the table lacks reads back as
    // itself, which is the miss this tells apart from a row.
    private static bool Rows(Messages strings, params string[] keys)
    {
        var words = keys.Select(strings.Get).ToList();
        return words.Select((w, i) => w.Length > 0 && w != keys[i]).All(found => found)
            && words.Distinct(System.StringComparer.Ordinal).Count() == words.Count;
    }
}
