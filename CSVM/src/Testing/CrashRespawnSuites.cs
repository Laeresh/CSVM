using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Bindings;
using CSVM.Extraction;
using CSVM.Flight.Airframe;
using CSVM.Launch;
using CSVM.Net;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>A Dogfight seat's return from a crash that no round caused: flown into the ground or
/// into a structure. It comes back once, on the crash camera time a shot-down seat waits, or at
/// once on the respawn control, which skips that camera. A press held over several steps is still
/// one press, so it places the seat once rather than on every step the button stays down. In
/// flight the same control places nothing, in a local match as on a wire.</summary>
internal static class CrashRespawnSuites
{
    private const ulong Seed = 0xC2A5B0B0UL;

    private const int PaneSeat = 0;
    private const int GuestSeat = 1;
    private const int BotSeat = 1;
    private const int NetBotSeat = 2;

    // The chapter whose Stunt Race course the split screen race flies.
    private const string RaceChapter = "C1";

    // How high over the struck surface a dive starts, and how fast it flies straight in.
    private const float DiveHeight = 40f;
    private const float DiveSpeed = 100f;

    // The most steps a dive is given to reach the surface under it.
    private const int DiveSteps = 120;

    // How long the respawn control is held from the step after the crash. A quick tap on a pad
    // covers about this many steps at 60 per second, and a hitched frame runs as many at once.
    private const int PressSteps = 6;

    // How long a press is watched after it, for a second placement that must not come.
    private const int AfterSteps = 60;

    // How far past the crash camera time an unpressed return is watched.
    private const int PastDueSteps = 30;

    private static readonly string[] Airframes = { "player_pfighter", "player_fbrand" };

    [Suite("versus-local-crash-respawn",
        "a local Dogfight of one pane and a bot on MP1: the pane flown into the ground with no "
        + "input comes back once, on the crash camera time a shot-down seat waits; flown into the "
        + "rearm hall on the step after a rearm it comes back once on the same time; with the "
        + "respawn control held over the steps after a crash it comes back once, at once, not once "
        + "per step held; in flight the match pins the control off and a held press places nothing "
        + "(control: with the pin lifted it places the pane once); the bot's own crash comes back "
        + "once on the same time; two panes with no bots return once from a held press as well and "
        + "are pinned the same in flight; and a split screen stunt race and a split screen free "
        + "flight leave every pane's in-flight respawn live")]
    internal static void ALocalCrashReturnsOnce(TestContext ctx)
    {
        var bots = NetCombatSuites.MatchSpec(ctx, out _, "--vs-bots=1");
        var panes = NetCombatSuites.MatchSpec(ctx, out _, "--players=2");
        string raceZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, RaceChapter, SessionSpec.StuntRaceMission);
        ctx.RequireData(SessionPaths.ChapterGamez(ctx.DataRoot, RaceChapter), $"{RaceChapter} gamez");
        ctx.RequireData(raceZrdr, $"{RaceChapter}/{SessionSpec.StuntRaceMission} zrdr");
        var race = SessionSpec.FromMenu(SessionSpec.Parse(NetStuntRaceSuites.RaceArgs(ctx)), RaceChapter, Airframes,
            MenuMode.Stunt, missionType: DogfightMissionType.StuntRace);
        var free = SessionSpec.Parse(new[] { "--fly", $"--chapter={ctx.Chapter}", $"--mission={panes.Mission}", "--players=2", "--mute", "--no-pads" });
        var ambient = NetCombatSuites.Ambient.Save();
        try
        {
            var roster = Launcher.LocalVersusField(bots, ctx.MessagesPath);
            ctx.Check(roster is { Length: 2 } && roster[BotSeat].IsBot, $"the local launch seats a pane and a bot");
            if (roster != null)
            {
                Session(ctx, bots, transport: null, roster, "pane and bot", session => PaneAndBot(ctx, session));
            }

            Session(ctx, panes, transport: null, roster: null, "panes alone", session =>
            {
                const string cell = "panes alone";
                var peers = new[] { session };
                var pane = session.Rigs[0].Controller!;
                var other = session.Rigs[1].Controller!;
                ctx.Check(session.NetSeats.Count == 0 && session.Rigs.Count == 2,
                    $"[{cell}] two panes and no roster ({session.NetSeats.Count} seat(s), {session.Rigs.Count} pane(s))");
                Settle(peers, pane, other);
                Pressed(ctx, cell, peers, pane, () => Dive(pane, pane.WorldPosition, out _));
                ctx.Check(!other.AllowLiveRespawn, $"[{cell}] the match pins the second pane's in-flight respawn off too ({other.AllowLiveRespawn})");
                Settle(peers, pane, other);
                InFlightPinned(ctx, cell, peers, pane);
            });

            // The rule's other side: a mode that is not a Dogfight keeps the control live.
            Live(ctx, race, "split screen stunt race", session => session.Rigs.All(r => r.Controller?.Race != null));
            Live(ctx, free, "split screen free flight", session => session.Dogfight == null && session.Rigs.All(r => r.Controller?.Race == null));
        }
        finally
        {
            ambient.Restore();
        }
    }

    [Suite("net-crash-respawn",
        "a host and a guest over a clean loopback with a host bot on MP1: the host's own pane flown "
        + "into the ground with the respawn control held over the steps after the crash comes back "
        + "once on its granted entry, not once per step held, and so does the guest's own pane "
        + "crashed with the control held, asking the host once and placed once on both machines; "
        + "the host's pane crashed with no input comes back once on the crash camera time; and in "
        + "flight the same control places neither pane (control: with the pin lifted the host's "
        + "pane respawns once)")]
    internal static void ANetCrashReturnsOnce(TestContext ctx)
    {
        var spec = NetCombatSuites.MatchSpec(ctx, out _);
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(6411));
        var roster = new NetSeat[]
        {
            new() { PeerId = 0, SeatIndex = PaneSeat, FlownHere = true, Callsign = "host", PlaneNode = Airframes[0] },
            new() { PeerId = 1, SeatIndex = GuestSeat, Callsign = "guest", PlaneNode = Airframes[1] },
            NetSeats.Bot(0, NetBotSeat, "bot", Airframes[0]),
        };
        NetSeats.Validate(roster, hostPeer: 0);
        var ambient = NetCombatSuites.Ambient.Save();
        NetCombatSuites.Ends? host = null;
        NetCombatSuites.Ends? guest = null;
        try
        {
            host = NetCombatSuites.Ends.Open(ctx, spec, mesh[0], isHost: true, Seed, roster, Airframes);
            guest = NetCombatSuites.Ends.Open(ctx, spec, mesh[1], isHost: false, Seed + 1, null, Airframes);
            ctx.Check(host.Built && guest.Built, $"both sessions build in one process (host {host.Built}, guest {guest.Built})");
            if (!host.Built || !guest.Built)
            {
                return;
            }

            NetStartSuites.UntilStarted(host.Session, guest.Session);
            var peers = new[] { host.Session, guest.Session };
            var hostPane = host.Session.SeatRigs[PaneSeat].Controller!;
            var guestPane = guest.Session.SeatRigs[GuestSeat].Controller!;
            var guestCopy = host.Session.SeatRigs[GuestSeat].Controller!;
            ctx.Check(hostPane.RespawnRequest != null && guestPane.RespawnRequest != null,
                $"both panes ask for their return over the wire (host asks {hostPane.RespawnRequest != null}, guest asks {guestPane.RespawnRequest != null})");
            if (host.Session.SeatRigs[NetBotSeat].Controller?.Pilot?.Gunner is { } gunner)
            {
                gunner.AutoTarget = false;
                gunner.Target = null;
            }

            Settle(peers, hostPane, guestPane, host.Session.SeatRigs[NetBotSeat].Controller!);
            Unpressed(ctx, "host pane", peers, hostPane, () => Dive(hostPane, hostPane.WorldPosition, out _));
            Settle(peers, hostPane, guestPane);
            Pressed(ctx, "host pane", peers, hostPane, () => Dive(hostPane, hostPane.WorldPosition, out _));

            // The guest's return is the host's grant, so its copy on the host is placed with it.
            Settle(peers, hostPane, guestPane);
            int copyBefore = guestCopy.RespawnCount;
            int grantsBefore = guest.Session.Dogfight!.SpawnsTaken;
            Pressed(ctx, "guest pane", peers, guestPane, () =>
            {
                guestPane.DebugForceCrash();
                return guestPane.Crashed;
            });
            ctx.Check(guestCopy.RespawnCount - copyBefore == 1 && guest.Session.Dogfight.SpawnsTaken - grantsBefore == 1,
                $"[guest pane] placed from the host's grant, which places its copy of the guest once too ({guest.Session.Dogfight.SpawnsTaken - grantsBefore} grant(s) taken, {guestCopy.RespawnCount - copyBefore} placement(s) of the copy)");

            // In flight the control places nothing on a wire, on either machine's own pane.
            ctx.Check(!hostPane.AllowLiveRespawn && !guestPane.AllowLiveRespawn,
                $"the wire pins both panes' in-flight respawn off (host {hostPane.AllowLiveRespawn}, guest {guestPane.AllowLiveRespawn})");
            Settle(peers, hostPane, guestPane);
            InFlight(ctx, "host pane in flight", peers, hostPane, expected: 0);
            InFlight(ctx, "guest pane in flight", peers, guestPane, expected: 0);

            // ABLE-TO-FAIL CONTROL. The same press with the pin lifted respawns the host's pane. The
            // two readings above are then the pin's, not a press that never reached the seat.
            hostPane.AllowLiveRespawn = true;
            InFlight(ctx, "ABLE-TO-FAIL CONTROL: host pane, pin lifted", peers, hostPane, expected: 1);
            hostPane.AllowLiveRespawn = false;
        }
        finally
        {
            guest?.Close();
            host?.Close();
            ambient.Restore();
        }
    }

    // The local match with a bot. The pane crashes into the ground, then into the rearm hall just
    // after a rearm, then with a held press. Then a press in flight, refused, and the bot's crash.
    private static void PaneAndBot(TestContext ctx, GameSession session)
    {
        const string cell = "pane and bot";
        var peers = new[] { session };
        var pane = session.SeatRigs[PaneSeat].Controller!;
        var bot = session.SeatRigs[BotSeat].Controller!;
        ctx.Check(pane.RespawnRequest == null && pane.RespawnPlacement != null,
            $"[{cell}] the pane's return is placed by this machine's rotation");
        if (bot.Pilot?.Gunner is { } gunner)
        {
            gunner.AutoTarget = false;
            gunner.Target = null;
        }

        Settle(peers, pane, bot);
        Unpressed(ctx, "ground", peers, pane, () => Dive(pane, pane.WorldPosition, out _));

        // The rearm hall, entered and rearmed on one step, then flown into on the next ones.
        if (session.Dogfight?.RearmPlay is { } rearm && rearm.BaseAt(0) is { } baseAt)
        {
            Settle(peers, pane, bot);
            var inside = baseAt.Position + (Vector3.Down * 10f);
            pane.RespawnAt(inside, inside + (Vector3.Right * 100f));
            int rearms = rearm.Rearms;
            Step(1, peers);
            ctx.Check(rearm.Rearms == rearms + 1, $"[rearm hall] the pane is rearmed at the base ({rearm.Rearms - rearms} rearm(s))");
            string into = "-";
            Unpressed(ctx, "rearm hall", peers, pane, () => Dive(pane, baseAt.Position + (Vector3.Back * 30f), out into));
            ctx.Note($"[rearm hall] the dive struck {into}");
        }
        else
        {
            ctx.Check(false, $"[rearm hall] MP1 lists a rearm base in a local match");
        }

        Settle(peers, pane, bot);
        Pressed(ctx, "ground", peers, pane, () => Dive(pane, pane.WorldPosition, out _));

        // In flight the match pins the control off, as a wired one does.
        Settle(peers, pane, bot);
        InFlightPinned(ctx, cell, peers, pane);

        Settle(peers, pane, bot);
        Unpressed(ctx, "bot", peers, bot, () =>
        {
            bot.DebugForceCrash();
            return bot.Crashed;
        });
    }

    // One crash with nothing pressed: one placement, on the crash camera time.
    private static void Unpressed(TestContext ctx, string cell, GameSession[] peers, FlightController plane, Func<bool> crash)
    {
        if (!Crashes(ctx, cell, peers, plane, crash))
        {
            return;
        }

        int due = Mathf.RoundToInt(VersusDirector.RespawnDelay / GameClock.FixedDt);
        var (placements, first) = Watch(peers, plane, due + PastDueSteps, held: 0);
        ctx.Check(placements == 1 && first >= due - 1 && first <= due + 2,
            $"[{cell}] with nothing pressed it comes back once, after the crash camera time ({placements} placement(s), the first {first} step(s) after the crash against {due})");
    }

    // One crash with the respawn control held from the next step: one placement, at once.
    private static void Pressed(TestContext ctx, string cell, GameSession[] peers, FlightController plane, Func<bool> crash)
    {
        if (!Crashes(ctx, cell, peers, plane, crash))
        {
            return;
        }

        var (placements, first) = Watch(peers, plane, PressSteps + AfterSteps, held: PressSteps);
        ctx.Check(placements == 1 && !plane.Crashed && first >= 1 && first <= PressSteps,
            $"[{cell}] with the respawn control held over {PressSteps} steps after the crash it comes back once ({placements} placement(s), the first {first} step(s) after the crash)");
    }

    // A local match's pane in flight: pinned, so a held press places nothing. Then the same press
    // with the pin lifted places it once, so the zero is the pin's and not a press never read.
    private static void InFlightPinned(TestContext ctx, string cell, GameSession[] peers, FlightController pane)
    {
        ctx.Check(!pane.AllowLiveRespawn, $"[{cell}] the match pins the pane's in-flight respawn off ({pane.AllowLiveRespawn})");
        InFlight(ctx, $"{cell}, in flight", peers, pane, expected: 0);
        pane.AllowLiveRespawn = true;
        InFlight(ctx, $"ABLE-TO-FAIL CONTROL: {cell}, pin lifted", peers, pane, expected: 1);
        pane.AllowLiveRespawn = false;
    }

    // A local session of a mode that is not a Dogfight: every pane keeps its in-flight respawn.
    private static void Live(TestContext ctx, SessionSpec spec, string cell, Func<GameSession, bool> isMode)
    {
        Session(ctx, spec, transport: null, roster: null, cell, session =>
        {
            ctx.Check(session.Rigs.Count == 2 && isMode(session),
                $"[{cell}] two panes in the mode ({session.Rigs.Count} pane(s), dogfight {session.Dogfight != null})");
            ctx.Check(session.Rigs.All(r => r.Controller is { AllowLiveRespawn: true }),
                $"[{cell}] every pane's in-flight respawn stays live ({string.Join(", ", session.Rigs.Select(r => r.Controller?.AllowLiveRespawn.ToString() ?? "-"))})");
        });
    }

    // The respawn control held over steps of level flight. It places the aeroplane `expected` times.
    private static void InFlight(TestContext ctx, string cell, GameSession[] peers, FlightController plane, int expected)
    {
        var (placements, _) = Watch(peers, plane, PressSteps + AfterSteps, held: PressSteps);
        ctx.Check(!plane.Crashed && placements == expected,
            $"[{cell}] the respawn control held over {PressSteps} steps of flight places the aeroplane {expected} time(s) ({placements}, crashed={plane.Crashed})");
    }

    private static bool Crashes(TestContext ctx, string cell, GameSession[] peers, FlightController plane, Func<bool> crash)
    {
        if (!crash())
        {
            ctx.Check(false, $"[{cell}] a surface lies under the aeroplane to fly into");
            return false;
        }

        int steps = 0;
        while (!plane.Crashed && steps < DiveSteps)
        {
            Step(1, peers);
            steps++;
        }

        ctx.Check(plane.Crashed, $"[{cell}] the aeroplane crashes with no round fired ({steps} step(s), def {plane.LastCrashDef ?? "-"})");
        return plane.Crashed;
    }

    // Steps on with the respawn control down for the first `held` steps. Answers how many times the
    // aeroplane was placed, and on which step the first placement came.
    private static (int Placements, int First) Watch(GameSession[] peers, FlightController plane, int steps, int held)
    {
        int before = plane.RespawnCount;
        int first = -1;
        for (int step = 1; step <= steps; step++)
        {
            if (held > 0 && step == 1)
            {
                plane.HoldActionForTest(InputAction.Respawn, true);
            }

            if (held > 0 && step == held + 1)
            {
                plane.HoldActionForTest(InputAction.Respawn, false);
            }

            Step(1, peers);
            if (first < 0 && plane.RespawnCount > before)
            {
                first = step;
            }
        }

        if (held >= steps)
        {
            plane.HoldActionForTest(InputAction.Respawn, false);
        }

        return (plane.RespawnCount - before, first);
    }

    // Puts the aeroplane over the first surface under `over` and flies it straight in. Answers
    // false with no surface there. The warp resets the sweep, so no sweep spans the teleport.
    private static bool Dive(FlightController plane, Vector3 over, out string into)
    {
        var world = new GodotWorldQuery(plane);
        var from = new Vector3(over.X, 3000f, over.Z);
        if (!world.Ray(from, from + (Vector3.Down * 6000f), CollisionLayers.World, null, out var hit))
        {
            into = "-";
            return false;
        }

        into = hit.Collider is { } body ? $"{body.GetParent()?.Name}/{body.Name}" : "-";
        var dir = new Vector3(0.2f, -1f, 0f).Normalized();
        var at = hit.Position - (dir * DiveHeight);
        plane.WarpTo(at, 0f, DiveSpeed);
        plane.PlaceHeld(at, at + dir);
        plane.ReleaseHeld(dir * DiveSpeed, reseatWalk: false);
        return true;
    }

    // Puts every listed aeroplane 500 m over where it flies and waits out the spawn's collision
    // window. The next dive then meets the surface it is flown at.
    private static void Settle(GameSession[] peers, params FlightController[] planes)
    {
        foreach (var plane in planes)
        {
            var at = plane.WorldPosition + (Vector3.Up * 500f);
            plane.RespawnAt(at, at + (Vector3.Right * 100f));
            plane.ArmSpawnTimers();
        }

        Step(Mathf.CeilToInt(CollisionDamage.SpawnGrace / GameClock.FixedDt) + 10, peers);
    }

    private static void Session(TestContext ctx, SessionSpec spec, INetTransport? transport, NetSeat[]? roster,
        string cell, Action<GameSession> body)
    {
        var end = NetCombatSuites.Ends.Open(ctx, spec, transport, isHost: true, Seed, roster);
        try
        {
            ctx.Check(end.Built, $"[{cell}] the session builds");
            if (end.Built)
            {
                body(end.Session);
            }
        }
        finally
        {
            end.Close();
        }
    }

    private static void Step(int steps, GameSession[] peers)
    {
        for (int i = 0; i < steps; i++)
        {
            foreach (var session in peers)
            {
                session._PhysicsProcess(GameClock.FixedDt);
            }
        }
    }
}
