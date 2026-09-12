using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>The HUD message stack the original posts a death into, asserted on a real aircraft
/// killed in C1: the four wording rules, the three colour arms, the slot geometry, and the five
/// seconds a line lives. Decode: docs/org/vehicleDamage.md "Death".</summary>
internal static class HudKillLineSuites
{
    // A 1440p pane, so the reference geometry reads back unscaled.
    private static readonly Vector2 Pane = new(2560f, 1440f);

    [Suite("hud-kill-line",
        "the kill line the original posts top-centre when a vehicle dies: a real Medusa Kestrel " +
        "shot down in C1 reads 'Medusa Kestrel was shot down' in the enemy colour, the other " +
        "three wording rules (the viewer's own name, a wingman with no name, a non-aeroplane " +
        "destroyed) read as decoded, the stack keeps four lines a fifth of the way down with an " +
        "18 px pitch, a newer line pushes the older ones down carrying their own remaining time, " +
        "a repeat refreshes rather than duplicates, and every line clears after five seconds")]
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
        ctx.Check(shotDown == "was shot down" && wingman == "Wingman was shot down"
                  && destroyed == "was destroyed",
            $"the three decoded rows read out of the string table: '{shotDown}' / '{wingman}' / '{destroyed}'");

        Wording(ctx, strings, shotDown, wingman, destroyed);
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

            // The session's own seam: the roster reports a death, the reading pane's stack takes
            // the line composed for that pane.
            roster.VehicleDowned += (victim, _) => HudMessages.PostKill(stack!, strings, victim,
                AimAssist.PlayerTeam, victimIsViewer: false, viewerName: "Nathan Zachary");

            var at = new Vector3(0f, 500f, 0f);
            ai = roster.SpawnAi(new AiSpawn("player_kestrel", at, at + Vector3.Forward,
                AiPilot.HoldingCourse(at, at + Vector3.Forward), Scheme: null,
                Team: InstantActionRuntime.EnemyTeam, AiDef: "medkestrel"));
            string name = PlaneRoster.PlaneDisplayName(ai.Stats!);
            ctx.Check(name == "Medusa Kestrel",
                $"the spawned aircraft carries the title the line names it by: '{name}'");
            ctx.Check(HudMessages.IsAeroplane(ai.Stats) && ai.Team == InstantActionRuntime.EnemyTeam,
                $"…as an aeroplane on an enemy team mode={ai.Stats?.VehicleMode ?? "<none>"} team={ai.Team}");

            stack.Clear();
            ai.DebugForceCrash(0);
            ctx.Check(ai.Crashed, $"the aircraft is down crashed={ai.Crashed}");
            ctx.Check(stack.LineAt(0) == $"Medusa Kestrel {shotDown}",
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
}
