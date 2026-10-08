using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Hangar;
using CSVM.Launch;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

using static CSVM.Testing.BotSuiteHelper;

namespace CSVM.Testing;

/// <summary>A bot seat's world contacts take a person's rule. It grazes, bounces and spends the
/// pair as a person in the same plane does, where world AI is destroyed outright. The rig half
/// flies <c>graze-bounce</c>'s floor with bare rigs; the match half stages a local bot into MP1.
/// ⚠ <c>graze-bounce</c> pins the decoded AI rule and stays as it is; this suite is its bot twin.</summary>
internal static class BotContactSuites
{
    private const ulong Seed = 0xB07C0471UL;
    private const int BotSeat = 1;

    // A one-step rise in normal speed past this, in m/s, is the contact's rebound. A piloted bot's
    // own pull-up and ground blow move it under 1 m/s a step; a rebound moves it about 25.
    private const float ReboundStep = 5f;

    // The share of the incoming normal speed a rebound must return to count as a bounce, graze-bounce's
    // bar; the decoded restitution measures about 0.56.
    private const float ReboundShare = 0.3f;

    // How far the bot's rebound coefficient may stand off the person's on the same trajectory. The
    // contact arm sets the partition, and the two plants reach the floor on different poses.
    private const float ReboundTolerance = 0.1f;

    // The same, across every stock airframe. A bot the ground blow has pitched up strikes tail
    // first, where the person strikes nose first. The worst pair, the Hoplite entering at 6 m/s,
    // stands 0.09 apart.
    private const float SweepTolerance = 0.15f;

    // Steps flown past a contact before the survivor is read again.
    private const int AfterSteps = 20;

    // Steps after a rebound in which another one still belongs to the same graze. The sweep runs on
    // every other step, so this is three sweeps.
    private const int GrazeSteps = 6;

    private enum Pilot
    {
        Person,
        Bot,
        WorldAi,
    }

    [Suite("graze-bounce-bot",
        "a bot seat takes a person's contact rule: on graze-bounce's floor trajectory a person rig and "
        + "a bot rig (AI-piloted, AI force path) both graze, survive and rebound along the normal at "
        + "the same restitution, spending the pair; with the AI ground blow pitching it up before it "
        + "lands the bot still rebounds at e <= 1 and as the person does, for every stock airframe on "
        + "two trajectories; world AI on the same trajectory is still "
        + "destroyed outright by the decoded local_11 rule (control); and a bot that rams a parked "
        + "aeroplane bounces off it and flies on, where world AI on that ram gains no rebound")]
    internal static void GrazeBounceBot(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        ctx.RequirePlane(StockAirframes.Nodes.ToArray());

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        StaticBody3D? floor = null;
        FlightController? parked = null;
        try
        {
            FlightController Build(Pilot pilot, Vector3 at, Vector3 dir, float speed, string? planeName = null,
                bool groundBlow = false)
            {
                planeName ??= ctx.PlaneName;
                var plane = new PlaneBuilder(planesGamez, textures).Build(planeName);
                // Each rig loads its own stats: the ground blow switch below writes into them.
                var stats = PlaneStats.Load(ctx.ZrdrPath, planeName);
                var model = new FlightModel(stats, aiForcePath: pilot != Pilot.Person);
                var rig = new FlightController
                {
                    PlaneModel = plane,
                    Collider = PlaneCollider.Build(plane),
                    Damage = new PlaneDamage(stats.DestroyableParts),
                    PlayerIndex = (int)pilot,
                    IsHumanPiloted = pilot == Pilot.Person,
                    IsBotSeat = pilot == Pilot.Bot,
                    // ⚠ Pilot-less, as graze-bounce's rigs are: an AiPilot would fly its own course,
                    // and the rule would stop being the only difference reaching the contact.
                    UseKeyboard = false,
                    PadDevices = Array.Empty<int>(),
                    AllowPause = false,
                };
                rig.AddChild(plane);
                rig.Setup(model, pilot == Pilot.Person ? ctx.Camera : null, new CamParams(), at, at + dir);
                ctx.Host.AddChild(rig);
                model.Reset(at, Basis.LookingAt(dir, Vector3.Up), speed, 0f);
                model.VelocityDir = dir;
                // The AI plant's ground blow would turn the AI-path rigs off the trajectory before
                // the sweep, as graze-bounce notes. Only a rig that measures the blow keeps it.
                if (!groundBlow)
                    model.Stats.GroundBlowElev = 0f;
                return rig;
            }

            Contact Fly(Pilot pilot, Vector3 at, Vector3 dir, Vector3 normal, float speed, string? planeName = null,
                bool groundBlow = false)
            {
                var rig = Build(pilot, at, dir, speed, planeName, groundBlow);
                try
                {
                    return FlyInto(rig, normal, 900, () => rig.SimStep(GameClock.FixedDt));
                }
                finally
                {
                    rig.Free();
                }
            }

            // graze-bounce's floor: a 15 degree descent at 60 m/s, one pair off a full ledger.
            floor = CombatSuites.Plate("graze-floor", new Vector3(600f, 4f, 600f), new Vector3(0f, -2f, 0f));
            ctx.Host.AddChild(floor);
            var descent = new Vector3(0f, -Mathf.Sin(Mathf.DegToRad(15f)), -Mathf.Cos(Mathf.DegToRad(15f))).Normalized();
            var start = new Vector3(0f, 6f, 30f);
            var person = Fly(Pilot.Person, start, descent, Vector3.Up, 60f);
            var bot = Fly(Pilot.Bot, start, descent, Vector3.Up, 60f);
            var ai = Fly(Pilot.WorldAi, start, descent, Vector3.Up, 60f);
            // The AI ground blow on. The bot pitches away from the floor in the steps before it
            // lands and strikes tail first, as a piloted bot staged into MP1 does.
            var blowBot = Fly(Pilot.Bot, start, descent, Vector3.Up, 60f, groundBlow: true);

            // Every stock airframe on that trajectory and on the MP1 staging's (25 degrees at 80 m/s).
            var steep = new Vector3(0f, -Mathf.Sin(Mathf.DegToRad(25f)), -Mathf.Cos(Mathf.DegToRad(25f))).Normalized();
            var steepStart = new Vector3(0f, 4f, 30f);
            var outside = new List<string>();
            float worstBot = 0f, worstGap = 0f;
            int flown = 0;
            foreach (var node in StockAirframes.Nodes)
            {
                foreach (var (from, dir, speed) in new[] { (start, descent, 60f), (steepStart, steep, 80f) })
                {
                    var p = Fly(Pilot.Person, from, dir, Vector3.Up, speed, node);
                    var b = Fly(Pilot.Bot, from, dir, Vector3.Up, speed, node, groundBlow: true);
                    float gap = Mathf.Abs(b.Restitution - p.Restitution);
                    flown++;
                    worstBot = Mathf.Max(worstBot, b.Restitution);
                    worstGap = Mathf.Max(worstGap, gap);
                    if (!p.Contacted || !b.Contacted || b.Restitution > 1f || gap >= SweepTolerance)
                        outside.Add($"{node}@{speed:0}: bot {b.Restitution:0.00} person {p.Restitution:0.00} (in {b.NormalIn:0.0} out {b.NormalOut:0.0})");
                }
            }

            floor.Free();
            floor = null;

            ctx.Check(person.Contacted && !person.Crashed,
                $"the person rig grazes the floor and survives it (vn {-person.NormalIn:0.0} m/s, spent {person.HullSpent:0.0})");
            ctx.Check(bot.Contacted && !bot.Crashed && !bot.CrashedAfter,
                $"the bot rig grazes the same floor and survives it, {AfterSteps} steps on too (vn {-bot.NormalIn:0.0} m/s, crashed {bot.Crashed}/{bot.CrashedAfter})");
            float ePerson = person.Restitution;
            float eBot = bot.Restitution;
            ctx.Check(bot.Contacted && eBot > ReboundShare && Mathf.Abs(eBot - ePerson) < ReboundTolerance,
                $"the bot rebounds along the normal as the person does: e {eBot:0.00} against {ePerson:0.00} (in {bot.NormalIn:0.00} out {bot.NormalOut:0.00} m/s)");
            // ABLE-TO-FAIL: the impulse read with the ground blow's rotation in it returns 1.12 here.
            ctx.Check(blowBot.Contacted && !blowBot.Crashed && blowBot.Restitution <= 1f
                    && Mathf.Abs(blowBot.Restitution - ePerson) < ReboundTolerance,
                $"with the AI ground blow pitching it up before it lands, the bot still rebounds at most as fast as it came in and as the person does: e {blowBot.Restitution:0.00} against {ePerson:0.00} (in {blowBot.NormalIn:0.00} out {blowBot.NormalOut:0.00} m/s)");
            ctx.Check(outside.Count == 0,
                $"on both trajectories every stock airframe's bot rebounds at e <= 1 and within {SweepTolerance:0.00} of the person in the same plane ({flown} pairs, worst bot e {worstBot:0.00}, worst gap {worstGap:0.00}){(outside.Count > 0 ? ": " + string.Join("; ", outside) : string.Empty)}");
            ctx.Check(bot.HullSpent > 0f && Mathf.Abs(bot.HullSpent - person.HullSpent) < 0.5f * Mathf.Max(1f, person.HullSpent),
                $"the bot spends the contact's pair from its ledger as the person does ({bot.HullSpent:0.0} against {person.HullSpent:0.0})");
            // ABLE-TO-FAIL CONTROL. The decoded local_11 rule, untouched for world AI.
            ctx.Check(ai.Crashed && ai.HullSpent == 0f,
                $"ABLE-TO-FAIL CONTROL: world AI on the same trajectory is destroyed outright with its ledger unspent (crashed {ai.Crashed}, spent {ai.HullSpent:0.0})");
            ctx.Note($"floor graze: person e={ePerson:0.00}, bot e={eBot:0.00}, bot with ground blow e={blowBot.Restitution:0.00}, world AI crashed={ai.Crashed}");

            // A bot into a parked aeroplane's tail. It strikes an aeroplane, so the entity cut prices
            // it and no doom applies.
            Contact Ram(Pilot pilot)
            {
                var parkAt = new Vector3(0f, 300f, 0f);
                parked = Build(Pilot.WorldAi, parkAt, Vector3.Forward, 0f);
                var rammer = Build(pilot, parkAt + new Vector3(0.3f, 0.2f, 30f), Vector3.Forward, 60f);
                try
                {
                    return FlyInto(rammer, Vector3.Back, 120, () => rammer.SimStep(GameClock.FixedDt));
                }
                finally
                {
                    rammer.Free();
                    parked.Free();
                    parked = null;
                }
            }

            var ram = Ram(Pilot.Bot);
            ctx.Check(ram.Contacted && !ram.Crashed && !ram.CrashedAfter,
                $"a bot ramming a parked aeroplane bounces off it and flies on (in {ram.NormalIn:0.00} out {ram.NormalOut:0.00} m/s, crashed {ram.Crashed}/{ram.CrashedAfter}, spent {ram.HullSpent:0.0})");
            // ABLE-TO-FAIL CONTROL. World AI on the same line strikes and spends but never rebounds.
            var aiRam = Ram(Pilot.WorldAi);
            ctx.Check(!aiRam.Contacted && aiRam.HullSpent > 0f,
                $"ABLE-TO-FAIL CONTROL: world AI on the same ram spends the pair and gains no rebound (rebounded {aiRam.Contacted}, spent {aiRam.HullSpent:0.0})");
        }
        finally
        {
            parked?.Free();
            floor?.Free();
            textures.Dispose();
        }
    }

    [Suite("versus-local-bot-graze",
        "a local Dogfight's bot seat flown into MP1's terrain at a shallow descent grazes, rebounds and "
        + "flies on, spending the pair, as the pane in the same plane staged the same way does, at a "
        + "restitution of at most 1 and within 0.1 of the pane's; the "
        + "same bot staged again with its seat flag cleared, world AI's rule, is destroyed outright "
        + "on that contact (control)")]
    internal static void LocalBotGrazesTerrain(TestContext ctx)
    {
        var spec = NetCombatSuites.MatchSpec(ctx, out _, "--plane=player_fury", "--vs-bot=player_fury");
        var ambient = NetCombatSuites.Ambient.Save();
        var roster = Launcher.LocalVersusField(spec, ctx.MessagesPath);
        ctx.Check(roster is { Length: 2 } && roster[BotSeat].IsBot, $"the local launch seats a pane and a bot");
        if (roster == null)
        {
            ambient.Restore();
            return;
        }

        var end = NetCombatSuites.Ends.Open(ctx, spec, transport: null, isHost: true, Seed, roster);
        try
        {
            ctx.Check(end.Built, $"the session builds");
            if (!end.Built)
            {
                return;
            }

            var session = end.Session;
            var pane = session.SeatRigs[0].Controller!;
            var bot = session.SeatRigs[BotSeat].Controller!;
            ctx.Check(bot is { IsHumanPiloted: false, IsBotSeat: true, TakesPersonsContactRule: true, Pilot: not null },
                $"the bot is AI-piloted on a person's contact rule (human {bot.IsHumanPiloted}, bot seat {bot.IsBotSeat})");
            ctx.Check(pane is { IsHumanPiloted: true, IsBotSeat: false, TakesPersonsContactRule: true },
                $"ABLE-TO-FAIL CONTROL: the pane is a person's, not a bot seat (bot seat {pane.IsBotSeat})");
            if (bot.Pilot?.Gunner is { } gunner)
            {
                gunner.AutoTarget = false;
                gunner.Target = null;
            }

            if (FlatGround(bot) is not { } ground)
            {
                ctx.Check(false, $"a flat patch of MP1's ground lies within 2 km of the bot");
                return;
            }

            ctx.Note($"staging over {ground.Collider?.Name} at ({ground.Position.X:0},{ground.Position.Y:0},{ground.Position.Z:0}), normal y {ground.Normal.Y:0.000}");
            var person = Stage(session, pane, bot, pane, ground);
            ctx.Check(person.Contacted && !person.Crashed,
                $"the pane, staged the same way in the same plane, grazes and survives (in {person.NormalIn:0.00} out {person.NormalOut:0.00} m/s, spent {person.HullSpent:0.0})");
            var graze = Stage(session, pane, bot, bot, ground);
            ctx.Check(graze.Contacted && !graze.Crashed && !graze.CrashedAfter,
                $"the bot grazes MP1's ground and flies on, {AfterSteps} steps on too (vn {-graze.NormalIn:0.0} m/s, crashed {graze.Crashed}/{graze.CrashedAfter})");
            ctx.Check(graze.Contacted && graze.NormalOut > ReboundShare * -graze.NormalIn,
                $"and rebounds along the normal (in {graze.NormalIn:0.00} out {graze.NormalOut:0.00} m/s)");
            ctx.Check(graze.HullSpent > 0f,
                $"and spends the contact's pair from its ledger ({graze.HullSpent:0.0})");
            // ABLE-TO-FAIL: the ground blow pitches the bot tail-down onto the ground, and the impulse
            // read with that rotation in it returns 1.07 here.
            ctx.Check(graze.Contacted && person.Contacted && graze.Restitution <= 1f
                    && Mathf.Abs(graze.Restitution - person.Restitution) < ReboundTolerance,
                $"and rebounds at most as fast as it came in and as the pane does: e {graze.Restitution:0.00} against {person.Restitution:0.00}");
            ctx.Note($"rebound e: pane {person.Restitution:0.00}, bot {graze.Restitution:0.00}; spent pane {person.HullSpent:0.0}, bot {graze.HullSpent:0.0}");

            // ABLE-TO-FAIL CONTROL. The same bot with the seat flag cleared is world AI to the contact.
            bot.IsBotSeat = false;
            var doomed = Stage(session, pane, bot, bot, ground);
            bot.IsBotSeat = true;
            ctx.Check(doomed.Crashed && doomed.HullSpent == 0f,
                $"ABLE-TO-FAIL CONTROL: staged again on world AI's rule it is destroyed outright with its ledger unspent (crashed {doomed.Crashed}, spent {doomed.HullSpent:0.0})");
        }
        finally
        {
            end.Close();
            ambient.Restore();
        }
    }

    // Steps a rig until its normal speed turns around or it crashes, then AfterSteps more. Reads the
    // whole graze, not one step: the normal speed entering its first rebound and leaving its last.
    // A later rebound belongs to it when inside GrazeSteps of the one before. Also the health spent.
    private static Contact FlyInto(FlightController rig, Vector3 normal, int steps, Action step)
    {
        float hull = rig.Damage?.WholeHealth ?? 0f;
        float nIn = 0f, nOut = 0f;
        bool contacted = false;
        int sinceRebound = 0;
        for (int i = 0; i < steps && (!contacted || sinceRebound < GrazeSteps) && !rig.Crashed; i++)
        {
            var before = rig.WorldVelocity;
            step();
            var after = rig.WorldVelocity;
            sinceRebound++;
            if (after.Dot(normal) - before.Dot(normal) > ReboundStep)
            {
                if (!contacted)
                    nIn = before.Dot(normal);
                contacted = true;
                nOut = after.Dot(normal);
                sinceRebound = 0;
            }
        }

        bool crashed = rig.Crashed;
        float spent = hull - (rig.Damage?.WholeHealth ?? 0f);
        for (int i = 0; i < AfterSteps && !rig.Crashed; i++)
        {
            step();
        }

        return new Contact(nIn, nOut, contacted, crashed, rig.Crashed, spent);
    }

    // Lifts both seats clear and waits out the spawn's collision window. Then the staged seat goes
    // 4 m over the patch, nosed 25 degrees down at 80 m/s, and the session flies it in.
    private static Contact Stage(GameSession session, FlightController pane, FlightController bot,
        FlightController staged, RayReport ground)
    {
        Lift(pane);
        Lift(bot);
        int grace = Mathf.CeilToInt(CollisionDamage.SpawnGrace / GameClock.FixedDt) + 10;
        for (int i = 0; i < grace; i++)
        {
            session._PhysicsProcess(GameClock.FixedDt);
        }

        var dir = new Vector3(Mathf.Cos(Mathf.DegToRad(25f)), -Mathf.Sin(Mathf.DegToRad(25f)), 0f).Normalized();
        var at = ground.Position + (Vector3.Up * 4f) - (dir * 2f);
        // The warp resets the sweep's carried origin, so no sweep spans the teleport. The held
        // placement pitches the nose down, and the release flies it on that line.
        staged.WarpTo(at, 0f, 80f);
        staged.PlaceHeld(at, at + dir);
        staged.ReleaseHeld(dir * 80f, reseatWalk: false);
        return FlyInto(staged, ground.Normal, 120, () => session._PhysicsProcess(GameClock.FixedDt));
    }

    // A patch of ground flat enough that a shallow descent meets it as a graze, under the bot's
    // spawn or on a ring around it. Samples the patch and 30 m along the run, which is +X.
    private static RayReport? FlatGround(FlightController bot)
    {
        var world = new GodotWorldQuery(bot);
        var centre = bot.WorldPosition;
        for (int ring = 0; ring <= 8; ring++)
        {
            for (int k = 0; k < Math.Max(1, ring * 6); k++)
            {
                float angle = Mathf.Tau * k / Math.Max(1, ring * 6);
                var at = centre + (new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (ring * 250f));
                if (Down(world, at) is { } here && here.Normal.Y > 0.97f
                    && Down(world, at + (Vector3.Right * 30f)) is { } ahead && ahead.Normal.Y > 0.97f
                    && Mathf.Abs(ahead.Position.Y - here.Position.Y) < 1f)
                {
                    return here;
                }
            }
        }

        return null;
    }

    private static RayReport? Down(IWorldQuery world, Vector3 at)
    {
        var from = new Vector3(at.X, 3000f, at.Z);
        return world.Ray(from, from + (Vector3.Down * 6000f), CollisionLayers.World, null, out var report)
            ? report
            : null;
    }

    private readonly record struct Contact(float NormalIn, float NormalOut, bool Contacted, bool Crashed,
        bool CrashedAfter, float HullSpent)
    {
        // The normal speed the graze returned per m/s it came in at; zero with no contact.
        public float Restitution => NormalIn < 0f ? NormalOut / -NormalIn : 0f;
    }
}
