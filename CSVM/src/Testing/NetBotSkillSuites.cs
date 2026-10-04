using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Hangar;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session;
using CSVM.Session.Roster;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>A bot seat's tier on the production seam. The host arms each bot's pilot on a rolled
/// personality shifted by its tier. It builds the aeroplane on the seat path, so no tier reaches
/// the hull a person flies in the same plane.</summary>
internal static class NetBotSkillSuites
{
    private const string MpMission = "MP1";

    // Every seat flies one airframe, so a hull difference can only be the tier's.
    private const string Plane = "player_bhawk";

    [Suite("net-bot-skill",
        "a host's bot seats fly their tier on a rolled Instant Action personality and a person's "
        + "hull: a novice, an ace on lobby team 1 and a veteran in the pane's own plane are each "
        + "armed on one of the five personalities shifted by -2, +2 and 0, which the unshifted "
        + "reading does not match for the novice and the ace; every bot passes the hostility gate, "
        + "and each one's armour and health maxima and stock loadout equal the pane's, where the "
        + "enemy durability scale would have cut them")]
    internal static void BotSkillTiers(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, ctx.Chapter);
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, ctx.Chapter);
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, ctx.Chapter, MpMission);
        ctx.RequireData(texturesPath, $"{ctx.Chapter} textures");
        ctx.RequireData(chapterZrdr, $"{ctx.Chapter} zrdr");
        ctx.RequireData(missionZrdr, $"{ctx.Chapter}/{MpMission} zrdr");

        var spec = SessionSpec.Parse(new[]
        {
            "--vs", $"--chapter={ctx.Chapter}", $"--mission={MpMission}", "--players=1", "--spawn=0",
        });
        var picker = new SpawnPicker(spec);
        var table = picker.LoadSpawnList(missionZrdr, spec.Scenario);
        if (table is not { Count: >= 4 })
        {
            throw new SuiteSkippedException($"{ctx.Chapter}/{MpMission} authors no usable net.zrd table");
        }

        // Lobby team 1 is the team most easily read as the player's side, team id 1.
        var roster = new NetSeat[]
        {
            new() { PeerId = 1, SeatIndex = 0, FlownHere = true, Callsign = "host", PlaneNode = Plane },
            NetSeats.Bot(1, 1, "novice", Plane, NetBotSkill.Novice),
            NetSeats.Bot(1, 2, "ace", Plane, NetBotSkill.Ace, team: 1),
            NetSeats.Bot(1, 3, "veteran", Plane, NetBotSkill.Veteran),
        };
        NetSeats.Validate(roster, hostPeer: 1);

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        var pool = new ProjectilePool(textures, null, null);
        ctx.Host.AddChild(pool);
        var pane = new SubViewport();
        ctx.Host.AddChild(pane);
        var rigs = new List<PlayerRig> { new() { Index = 0, Camera = ctx.Camera, HudParent = pane, Viewport = pane } };
        for (int i = 1; i < roster.Length; i++)
        {
            rigs.Add(new() { Index = i, Camera = null!, HudParent = ctx.Host });
        }

        FlightRoster? flightRoster = null;
        try
        {
            var match = new VersusMatch(roster.Length, killTarget: 0, timeLimit: 0f);
            flightRoster = NewRoster(ctx, spec, planesGamez, textures, pool, table, picker,
                chapterZrdr, missionZrdr, roster, rigs, match);
            flightRoster.BuildPlayers(rigs);
            var person = rigs[0].Controller!;
            var bots = rigs.Skip(1).Select(rig => rig.Controller!).ToArray();
            var skills = AiSkills.Load(ctx.ZrdrPath);

            ctx.Check(bots.All(b => !b.IsHumanPiloted && b.Pilot is { Gunner: not null, Machine: not null }),
                $"every bot seat is flown by an armed AI pilot");
            ctx.Check(bots.All(b => Difficulty.AppliesTo(b.Team)) && bots[1].Team == AimAssist.LobbyTeam(1),
                $"and every bot passes the hostility gate, the lobby-team-1 bot included (teams {string.Join(", ", bots.Select(b => b.Team))})");

            for (int i = 0; i < bots.Length; i++)
            {
                var tier = roster[i + 1].Skill;
                var rows = RowsArmed(bots[i], tier, skills);
                ctx.Check(rows.Length >= 1,
                    $"seat {i + 1} ({tier}) is armed on a personality shifted by its tier (rows {string.Join(", ", rows)}; {Readout(bots[i])})");
                if (tier != NetBotSkill.Veteran)
                {
                    var unshifted = RowsArmed(bots[i], NetBotSkill.Veteran, skills);
                    ctx.Check(unshifted.Length == 0,
                        $"ABLE-TO-FAIL CONTROL: seat {i + 1} ({tier}) matches no unshifted personality (rows {string.Join(", ", unshifted)})");
                }
            }

            var hull = person.Damage!;
            foreach (var bot in bots)
            {
                var damage = bot.Damage!;
                bool parts = damage.Parts.Count == hull.Parts.Count && damage.Parts.All(p =>
                    hull.Parts.TryGetValue(p.Key, out var own)
                    && p.Value.Def.MaxHp == own.Def.MaxHp && p.Value.Def.MaxArmor == own.Def.MaxArmor);
                ctx.Check(damage.WholeArmorMax == hull.WholeArmorMax && damage.WholeHealthMax == hull.WholeHealthMax && parts,
                    $"seat {bot.PlayerIndex}'s hull is the person's: armour {damage.WholeArmorMax:0.#} / {hull.WholeArmorMax:0.#}, health {damage.WholeHealthMax:0.#} / {hull.WholeHealthMax:0.#}, {damage.Parts.Count} parts");
            }

            // ABLE-TO-FAIL CONTROL: the comparison sees the scale an AI spawn of the novice tier takes.
            var scaled = PlaneDamage.For(person.Stats!.WithEnemyDurability(Difficulty.EnemyDurabilityFactor(Difficulty.Normal)));
            ctx.Check(scaled.WholeHealthMax < hull.WholeHealthMax,
                $"ABLE-TO-FAIL CONTROL: the enemy scale would have cut the hull ({scaled.WholeHealthMax:0.#} against {hull.WholeHealthMax:0.#})");

            ctx.Check(bots.All(b => b.Loadout != null && b.Loadout.Hardpoints.Count == person.Loadout!.Hardpoints.Count
                                    && b.Loadout.FirableGuns.Count() == person.Loadout.FirableGuns.Count()),
                $"and every bot carries the stock loadout the pane does ({person.Loadout?.Hardpoints.Count} hardpoints)");
            ctx.Check(flightRoster.AiAircraft.Count == 0,
                $"no bot is one of the roster's AI ({flightRoster.AiAircraft.Count})");
        }
        finally
        {
            flightRoster?.ClearMembership();
            foreach (var rig in rigs)
            {
                rig.Controller?.Free();
            }

            pane.Free();
            pool.Free();
            textures.Dispose();
        }
    }

    // The personality rows whose ratings, shifted by tier, are what this bot's pilot was armed on.
    private static int[] RowsArmed(FlightController bot, NetBotSkill tier, AiSkills skills) =>
        Enumerable.Range(0, 5).Where(row => ArmedOn(bot, BotSeats.Ratings(BotSeats.Personality((uint)row), tier), skills)).ToArray();

    // Each value PreparePilot derives from a rating, recomputed off the same skill tables.
    private static bool ArmedOn(FlightController bot, AiSkillVector r, AiSkills skills)
    {
        var gunner = bot.Pilot!.Gunner!;
        var machine = bot.Pilot.Machine!;
        string? mode = bot.Stats!.VehicleMode;
        float dare = mode is null || mode.Equals(VehicleDefs.JetMode, StringComparison.OrdinalIgnoreCase)
            ? skills.At("daredevil_chance", r.DareDevil!.Value) : 0f;
        return gunner.DeadEyeAngleDeg == skills.DeadEyeAngleDeg(r.DeadEye!.Value)
            && gunner.QuickDrawAngleDeg == skills.QuickDrawAngleDeg(r.QuickDraw!.Value)
            && machine.NaturalTouch == r.NaturalTouch
            && machine.SixthSenseChance == skills.At("sixth_sense_chance", r.SixthSense!.Value)
            && machine.StunRecoveryIntervalS == skills.At("stun_recovery_interval", r.StunRecovery!.Value)
            && machine.SteadyHandExponent == AiModeMachine.ExponentFor(skills.At("steady_hand_chance", r.SteadyHand!.Value))
            && machine.DaredevilChance == dare;
    }

    private static string Readout(FlightController bot) =>
        FormattableString.Invariant($"dead-eye {bot.Pilot!.Gunner!.DeadEyeAngleDeg:0.00}, natural touch {bot.Pilot.Machine!.NaturalTouch}, sixth sense {bot.Pilot.Machine.SixthSenseChance:0.00}");

    private static FlightRoster NewRoster(TestContext ctx, SessionSpec spec, GameZ planesGamez,
        TextureArchive textures, ProjectilePool pool, List<SpawnPoint> table, SpawnPicker picker,
        string chapterZrdr, string missionZrdr, IReadOnlyList<NetSeat> roster, List<PlayerRig> rigs,
        VersusMatch match) =>
        new(FlightRosterPolicy.From(spec),
            new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof")),
            new WorldEffectsFactory(spec, ctx.Host, () => Vector3.Zero), ctx.Host,
            new AircraftAssemblyResources
            {
                PlanesGamez = planesGamez,
                StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
                AiStatsFor = (plane, aiDef) => PlaneStats.LoadForAi(ctx.ZrdrPath, plane, aiDef),
                CamParamsFor = _ => new CamParams(),
                PaintRng = new RandomNumberGenerator(),
                ZrdrPath = ctx.ZrdrPath,
                StockLoadouts = StockLoadouts.Load(),
                WeaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null),
                WeaponMessages = Messages.Load(ctx.MessagesPath),
                Textures = textures,
                Shakes = ShakeDefs.Load(ctx.ZrdrPath),
            },
            new FlightWorldBindings
            {
                Projectiles = pool,
                Gamez = planesGamez,
                ChapterZrdrPath = chapterZrdr,
                MissionZrdrPath = missionZrdr,
            },
            new HumanRosterBindings
            {
                RigCount = rigs.Count,
                NetSeats = roster,
                Rigs = rigs,
                SpawnList = table,
                SpawnBase = picker.ChooseSpawnBase(table),
                VersusMatch = match,
                PauseState = new PauseState(),
            }, picker);
}
