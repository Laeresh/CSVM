using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Launch;
using CSVM.Net;
using CSVM.Spec;
using CSVM.UI.Screens;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The start of a network flight: nobody's simulation runs until every seated machine
/// has built its world. The rig is <see cref="NetCombatSuites"/>'s, one session per peer in one
/// process. A slow guest is a guest whose session is built only after the host has been stepped
/// on its own for a while.</summary>
internal static class NetStartSuites
{
    private const ulong HostSeed = 0xC0FFEE21UL;

    // How long the host is stepped alone before the slow guest builds, in sim steps. Three
    // seconds is well past anything a held start could round away.
    private const int SlowLoadSteps = 180;

    // How many steps a released start is given to reach every machine. A reliable word crosses
    // the latency link below in three.
    private const int ReleaseSteps = 30;

    // The most mission clock the host may have run when the slow guest's aeroplane first moves.
    // It is the latency there and back, with room to spare.
    private const float StartSkewSeconds = 0.25f;

    // How far an aeroplane may drift and still count as standing still, in metres.
    private const float StillMetres = 0.01f;

    // The waiting board's field: a host and two guests, one human seat each.
    private const int BoardField = 3;

    // Longer than any waiting line takes to chalk on, in seconds.
    private const double ChalkSeconds = 4.0;

    [Suite("net-start-together",
        "a Dogfight host and guests in one process whose guests build late: the host's mission "
        + "clock and aeroplane stand still while a guest is still loading, and start together "
        + "with the guest's once it has built, and a guest that drops out while loading releases "
        + "the host and the guest already waiting")]
    internal static void EveryoneStartsTogether(TestContext ctx)
    {
        var spec = NetCombatSuites.MatchSpec(ctx, out _);
        var ambient = NetCombatSuites.Ambient.Save();
        try
        {
            SlowGuest(ctx, spec);
            DroppedWhileLoading(ctx, spec);
        }
        finally
        {
            ambient.Restore();
        }
    }

    [Suite("net-start-board",
        "a Dogfight host and two guests in one process, loading one after another: each machine "
        + "already loaded writes the waiting line onto its load board a letter at a time, with the "
        + "count of players in out of all three, under a closed load gate; the count moves as the "
        + "first guest loads, rewriting only the count, and reaches that guest's board too; Esc "
        + "stands the leave question over the board and a No keeps the hold; a guest's Yes leaves "
        + "the start and the others start without it; the last guest, whose load completes the set, "
        + "writes nothing before its board comes down, and a host whose guest loaded during its build "
        + "writes nothing either; a pointer click on an answer answers it; and a host's Yes calls the "
        + "start off on every machine, one still loading included")]
    internal static void WaitingBoard(TestContext ctx)
    {
        var spec = NetCombatSuites.MatchSpec(ctx, out _);
        var ambient = NetCombatSuites.Ambient.Save();
        try
        {
            WaitingAndLeaving(ctx, spec);
            HostCallsOff(ctx, spec);
            HostLast(ctx, spec);
        }
        finally
        {
            ambient.Restore();
        }
    }

    // Steps every session, host first, until none holds its start, and says how many steps that
    // took. For a suite whose first reading needs a flight that runs, over a link with latency.
    internal static int UntilStarted(params GameSession[] sessions)
    {
        int steps = 0;
        for (; steps < SlowLoadSteps && sessions.Any(s => s.StartHeld); steps++)
        {
            Step(1, sessions);
        }

        return steps;
    }

    // The waiting line, the declined question, a guest leaving and the last guest's silent board.
    private static void WaitingAndLeaving(TestContext ctx, SessionSpec spec)
    {
        var mesh = LoopbackTransport.Mesh(BoardField, LoopbackConditions.Perfect, new Random(4305));
        mesh[1].Disconnect(2);
        var ends = new List<NetCombatSuites.Ends>();
        var boards = new List<LoadBoard>();
        var messages = ctx.Data.Messages;
        try
        {
            ends.Add(NetCombatSuites.Ends.Open(ctx, spec, mesh[0], isHost: true, HostSeed, NetCombatSuites.Roster(BoardField, spec)));
            ctx.Check(ends[0].Built, $"[board] the host builds ({ends[0].Built})");
            if (!ends[0].Built)
            {
                return;
            }

            Stand(ctx, boards, 1);
            string oneIn = LoadScreens.WaitingText(messages, (1, BoardField));
            Hold(ends, boards, messages, 1);
            string first = boards[0].Written;
            Hold(ends, boards, messages, StepsOf(0.3));
            string partway = boards[0].Written;
            ctx.Check(first.Length == 0 && partway.Length > 0 && partway.Length < oneIn.Length && oneIn.StartsWith(partway, StringComparison.Ordinal),
                $"[board] the host's line is chalked on a letter at a time: none on the first frame, a beginning of it 0.3 s on ('{first}', '{partway}')");
            Hold(ends, boards, messages, StepsOf(ChalkSeconds));
            Written(ctx, boards, oneIn);
            Declined(ctx, ends[0].Session, boards[0], messages);

            if (!Loads(ctx, spec, mesh, ends, boards))
            {
                return;
            }

            string twoIn = LoadScreens.WaitingText(messages, (2, BoardField));
            int shared = 0;
            while (shared < oneIn.Length && shared < twoIn.Length && oneIn[shared] == twoIn[shared])
            {
                shared++;
            }

            int shortest = Hold(ends, boards, messages, StepsOf(ChalkSeconds), watched: 1);
            ctx.Check(shortest >= shared && shortest < twoIn.Length,
                $"[board] the count moving rewrites only the count on the host's written line: it never falls below the {shared} letters the two lines share, and it is rewritten ({shortest} at the least)");
            Written(ctx, boards, twoIn);

            // The first guest answers Yes. What the launcher then does is drop its wire, which the
            // link closing here stands in for.
            var host = ends[0].Session;
            var leaver = ends[1].Session;
            bool asked = Ask(boards[1], leaver, messages) == LoadScreens.GuestLeaveQuestion;
            boards[1].Press(LoadKey.Accept);
            var left = Launcher.HoldLoadBoard(boards[1], leaver, messages, offerLeave: true);
            ctx.Check(asked && left == StartHoldFrame.Left,
                $"[leave] Esc on a guest's held board asks to leave for the games list, and Yes leaves the start ({asked}, {left})");
            mesh[1].Disconnect(0);
            ends[1].Close();
            Step(ReleaseSteps, host);
            ctx.Check(host.StartHeld && host.Wire.StartTally == (1, BoardField - 1),
                $"[leave] the host holds on for the guest still loading, counting the one that left out ({host.StartHeld}, {host.Wire.StartTally})");

            if (!Loads(ctx, spec, mesh, ends, boards))
            {
                return;
            }

            var last = ends[2].Session;
            int released = -1;
            for (int step = 0; step < ReleaseSteps && released < 0; step++)
            {
                Step(1, host, last);
                bool held = false;
                foreach (int m in new[] { 0, 2 })
                {
                    held |= Launcher.HoldLoadBoard(boards[m], ends[m].Session, messages, offerLeave: true) == StartHoldFrame.Held;
                    boards[m].Chalk(GameClock.FixedDt);
                }

                released = held ? -1 : step;
            }

            ctx.Check(released >= 0 && host.Wire.StartGate?.Release == NetStartRelease.Everyone,
                $"[leave] once the last guest has loaded, the host and it start without the one that left within {ReleaseSteps} steps ({released}, {host.Wire.StartGate?.Release})");
            ctx.Check(!last.Wire.StartWaitsOnOthers && boards[2].Written.Length == 0,
                $"[last] the last guest, whose own load completed the set, never begins the waiting line ('{boards[2].Written}')");
        }
        finally
        {
            Clear(ctx, ends, boards);
        }
    }

    // A host and one guest held for a second guest still loading. The host's Yes calls the start
    // off, and both guests take the word, the one still loading once its build ends.
    private static void HostCallsOff(TestContext ctx, SessionSpec spec)
    {
        var mesh = LoopbackTransport.Mesh(BoardField, LoopbackConditions.Perfect, new Random(4307));
        mesh[1].Disconnect(2);
        var ends = new List<NetCombatSuites.Ends>();
        var boards = new List<LoadBoard>();
        var messages = ctx.Data.Messages;
        try
        {
            ends.Add(NetCombatSuites.Ends.Open(ctx, spec, mesh[0], isHost: true, HostSeed, NetCombatSuites.Roster(BoardField, spec)));
            Stand(ctx, boards, 1);
            if (!ends[0].Built || !Loads(ctx, spec, mesh, ends, boards))
            {
                ctx.Check(false, $"[call off] the host and the first guest build");
                return;
            }

            Hold(ends, boards, messages, 2);
            var host = ends[0].Session;
            bool asked = Ask(boards[0], host, messages) == LoadScreens.HostLeaveQuestion;
            Click(boards[0], 0);
            var called = Launcher.HoldLoadBoard(boards[0], host, messages, offerLeave: true);
            ctx.Check(asked && called == StartHoldFrame.Cancelled,
                $"[call off] Esc on the host's held board asks to call the game off, and a click on Yes calls it off ({asked}, {called})");

            var guest = ends[1].Session;
            int heard = -1;
            for (int step = 0; step < ReleaseSteps && heard < 0; step++)
            {
                Step(1, guest);
                heard = Launcher.HoldLoadBoard(boards[1], guest, messages, offerLeave: true) == StartHoldFrame.Cancelled ? step : -1;
            }

            ends.Add(NetCombatSuites.Ends.Open(ctx, spec, mesh[2], isHost: false, HostSeed + 2, null));
            var late = ends[2].Built ? Launcher.HoldLoadBoard(null, ends[2].Session, messages, offerLeave: true) : StartHoldFrame.Released;
            ctx.Check(heard >= 0 && late == StartHoldFrame.Cancelled,
                $"[call off] the loaded guest takes the word within {ReleaseSteps} steps, and the guest still loading once it has built, so every machine goes back to the lobby ({heard}, {late})");
        }
        finally
        {
            Clear(ctx, ends, boards);
        }
    }

    // A host whose one guest built while the host was still building. The guest's loaded word, under
    // no round since no hold had reached it, waits on the host's link. The host's board writes
    // nothing, and the start goes once the guest answers the hold.
    private static void HostLast(TestContext ctx, SessionSpec spec)
    {
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(4309));
        var word = new byte[StartGateMessage.Size];
        StartGateMessage.LoadedWord(0).Write(word);
        mesh[1].Send(0, word, NetReliability.Reliable, NetChannels.Events);
        var ends = new List<NetCombatSuites.Ends>();
        var boards = new List<LoadBoard>();
        var messages = ctx.Data.Messages;
        try
        {
            ends.Add(NetCombatSuites.Ends.Open(ctx, spec, mesh[0], isHost: true, HostSeed, NetCombatSuites.Roster(2, spec)));
            Stand(ctx, boards, 1);
            if (!ends[0].Built)
            {
                ctx.Check(false, $"[host last] the host builds");
                return;
            }

            var host = ends[0].Session;
            bool waits = host.Wire.StartWaitsOnOthers;
            var tally = host.Wire.StartTally;
            Launcher.HoldLoadBoard(boards[0], host, messages, offerLeave: true);
            boards[0].Chalk(ChalkSeconds);
            if (!Loads(ctx, spec, mesh, ends, boards))
            {
                return;
            }

            int released = -1;
            for (int step = 0; step < ReleaseSteps && released < 0; step++)
            {
                Step(1, host, ends[1].Session);
                bool held = false;
                for (int m = 0; m < 2; m++)
                {
                    held |= Launcher.HoldLoadBoard(boards[m], ends[m].Session, messages, offerLeave: true) == StartHoldFrame.Held;
                    boards[m].Chalk(GameClock.FixedDt);
                }

                released = held ? -1 : step;
            }

            ctx.Check(!waits && tally == (2, 2) && boards[0].Written.Length == 0 && released >= 0,
                $"[host last] a host whose guest loaded during its build counts it in on its first held frame and writes nothing, and the start goes within {ReleaseSteps} steps ({waits}, {tally}, '{boards[0].Written}', {released})");
        }
        finally
        {
            Clear(ctx, ends, boards);
        }
    }

    // Esc on a held board, under a closed gate and over the waiting line. Hands back the question
    // that stands; the box must be drawn over the board.
    private static string Ask(LoadBoard board, GameSession session, Mech3.Messages messages)
    {
        Launcher.HoldLoadBoard(board, session, messages, offerLeave: true);
        board.Press(LoadKey.Back);
        bool drawn = board.Asking is { } standing
            && board.Shown?.Overlays.Any(p => p.Lines.FirstOrDefault()?.Text == standing) == true;
        return LoadGate.Closed && drawn ? board.Asking! : string.Empty;
    }

    // A pointer press two authored pixels inside the answer's strip, 0 for Yes and 1 for No.
    private static void Click(LoadBoard board, int answer)
    {
        var (x, y, _, _) = LoadScreens.AnswerAt(answer, (0f, 0f));
        board.Point(x + 2f, y + 2f, click: true);
    }

    // The focus mark's left edge, NaN where none is drawn.
    private static float MarkX(LoadBoard board) =>
        board.Shown?.Overlays.LastOrDefault()?.Fills.FirstOrDefault(f => f.Border)?.X ?? float.NaN;

    // Esc, the focus onto No and a click on No; Esc and then Esc; a click beside the box. The box
    // comes down on each answer, the hold goes on, and a click on nothing answers nothing.
    private static void Declined(TestContext ctx, GameSession host, LoadBoard board, Mech3.Messages messages)
    {
        bool asked = Ask(board, host, messages) == LoadScreens.HostLeaveQuestion;
        var standing = Launcher.HoldLoadBoard(board, host, messages, offerLeave: true);
        float yesMark = MarkX(board);
        board.Press(LoadKey.Right);
        float noMark = MarkX(board);
        float yesX = LoadScreens.AnswerAt(0, (0f, 0f)).X;
        float noX = LoadScreens.AnswerAt(1, (0f, 0f)).X;
        ctx.Check(yesMark < yesX && yesMark > yesX - 10f && noMark < noX && noMark > noX - 10f,
            $"[declined] the focused answer carries the menus' focus mark, moving from Yes to No with the arrow ({yesMark} by {yesX}, {noMark} by {noX})");
        Click(board, 1);
        var no = Launcher.HoldLoadBoard(board, host, messages, offerLeave: true);
        bool againAsked = Ask(board, host, messages).Length > 0;
        board.Press(LoadKey.Back);
        var back = Launcher.HoldLoadBoard(board, host, messages, offerLeave: true);
        Ask(board, host, messages);
        board.Point(5f, 5f, click: true);
        bool stillAsked = board.Asking != null;
        board.Press(LoadKey.Back);
        ctx.Check(asked && standing == StartHoldFrame.Held && no == StartHoldFrame.Held && back == StartHoldFrame.Held
                && againAsked && stillAsked && board.Asking == null && !board.LeaveConfirmed && host.StartHeld,
            $"[declined] the question stands over the board with the hold going on; a click on No or a second Esc takes it down and keeps the hold, and a click beside the box answers nothing ({asked}, {standing}, {no}, {againAsked}, {back}, {stillAsked}, {board.Asking})");
    }

    private static void Clear(TestContext ctx, List<NetCombatSuites.Ends> ends, List<LoadBoard> boards)
    {
        foreach (var board in boards)
        {
            ctx.Host.RemoveChild(board);
            board.QueueFree();
        }

        for (int i = ends.Count - 1; i >= 0; i--)
        {
            ends[i].Close();
        }
    }

    // One host, one guest built three seconds late over a link with latency.
    private static void SlowGuest(TestContext ctx, SessionSpec spec)
    {
        var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(0.05, 0.0, 0.0), new Random(4301));
        NetCombatSuites.Ends? host = null;
        NetCombatSuites.Ends? guest = null;
        try
        {
            host = NetCombatSuites.Ends.Open(ctx, spec, mesh[0], isHost: true, HostSeed, NetCombatSuites.Roster(2, spec));
            ctx.Check(host.Built, $"[slow guest] the host builds ({host.Built})");
            if (!host.Built || Own(host.Session) is not { } hostPlane || host.Session.Dogfight?.Match is not { } match)
            {
                return;
            }

            var hostFrom = hostPlane.GlobalPosition;
            Step(SlowLoadSteps, host.Session);
            float drift = hostPlane.GlobalPosition.DistanceTo(hostFrom);
            ctx.Check(match.Elapsed == 0f && drift < StillMetres,
                $"[slow guest] a host stepped {SlowLoadSteps} times while its guest loads runs no mission clock and flies nowhere ({match.Elapsed:0.00} s, {drift:0.00} m)");

            guest = NetCombatSuites.Ends.Open(ctx, spec, mesh[1], isHost: false, HostSeed + 1, null);
            ctx.Check(guest.Built, $"[slow guest] the guest builds late ({guest.Built})");
            if (!guest.Built || Own(guest.Session) is not { } guestPlane)
            {
                return;
            }

            ctx.Check(host.Session.Wire.StartWaitsOnOthers && !guest.Session.Wire.StartWaitsOnOthers,
                $"[slow guest] the host writes the waiting line for the guest still loading, and the guest, whose load completes the set off the host's hold word, writes none ({host.Session.Wire.StartWaitsOnOthers}, {guest.Session.Wire.StartWaitsOnOthers})");
            var guestFrom = guestPlane.GlobalPosition;
            float hostClockAtStart = -1f;
            for (int i = 0; i < ReleaseSteps && hostClockAtStart < 0f; i++)
            {
                host.Session._PhysicsProcess(GameClock.FixedDt);
                guest.Session._PhysicsProcess(GameClock.FixedDt);
                if (guestPlane.GlobalPosition.DistanceTo(guestFrom) >= StillMetres)
                {
                    hostClockAtStart = match.Elapsed;
                }
            }

            ctx.Check(hostClockAtStart >= 0f && hostClockAtStart <= StartSkewSeconds,
                $"[slow guest] the guest's aeroplane starts within {ReleaseSteps} steps of its build, with the host's mission clock at most {StartSkewSeconds:0.00} s in ({hostClockAtStart:0.00} s)");
            ctx.Check(hostPlane.GlobalPosition.DistanceTo(hostFrom) >= StillMetres,
                $"[slow guest] and the host's own aeroplane is flying by then");
            ctx.Check(host.Session.Wire.StartGate?.Release == NetStartRelease.Everyone && guest.Session.Wire.StartGate?.Release == NetStartRelease.Started,
                $"[slow guest] the host opened on every guest loaded and the guest on the host's word ({host.Session.Wire.StartGate?.Release}, {guest.Session.Wire.StartGate?.Release})");
        }
        finally
        {
            guest?.Close();
            host?.Close();
        }
    }

    // One host and two guests on a star, the second of which never builds and then drops.
    private static void DroppedWhileLoading(TestContext ctx, SessionSpec spec)
    {
        var mesh = LoopbackTransport.Mesh(3, LoopbackConditions.Perfect, new Random(4303));
        mesh[1].Disconnect(2);
        NetCombatSuites.Ends? host = null;
        NetCombatSuites.Ends? first = null;
        try
        {
            host = NetCombatSuites.Ends.Open(ctx, spec, mesh[0], isHost: true, HostSeed, NetCombatSuites.Roster(3, spec));
            first = NetCombatSuites.Ends.Open(ctx, spec, mesh[1], isHost: false, HostSeed + 1, null);
            ctx.Check(host.Built && first.Built, $"[dropped] the host and the first guest build ({host.Built}, {first.Built})");
            if (!host.Built || !first.Built || host.Session.Dogfight?.Match is not { } match
                || Own(first.Session) is not { } firstPlane)
            {
                return;
            }

            var firstFrom = firstPlane.GlobalPosition;
            Step(SlowLoadSteps, host.Session, first.Session);
            float drift = firstPlane.GlobalPosition.DistanceTo(firstFrom);
            ctx.Check(match.Elapsed == 0f && drift < StillMetres,
                $"[dropped] while the second guest is still loading, neither the host's mission clock nor the first guest's aeroplane moves ({match.Elapsed:0.00} s, {drift:0.00} m)");

            mesh[0].Disconnect(2);
            Step(ReleaseSteps, host.Session, first.Session);
            drift = firstPlane.GlobalPosition.DistanceTo(firstFrom);
            ctx.Check(match.Elapsed > 0f && drift >= StillMetres,
                $"[dropped] the second guest's link dropping releases the host and the first guest ({match.Elapsed:0.00} s, {drift:0.00} m)");
            ctx.Check(host.Session.Wire.StartGate?.Release == NetStartRelease.Left,
                $"[dropped] and the host names the drop as what released it ({host.Session.Wire.StartGate?.Release})");
        }
        finally
        {
            first?.Close();
            host?.Close();
        }
    }

    // The aeroplane this machine flies, read off its own seat's rig.
    private static Node3D? Own(GameSession session) =>
        session.Wire.Link is { LocalSeat: >= 0 } link && link.LocalSeat < session.SeatRigs.Count
            ? session.SeatRigs[link.LocalSeat].Controller
            : null;

    // The next guest builds, and every machine without a board yet stands one. False when the
    // guest did not build.
    private static bool Loads(
        TestContext ctx, SessionSpec spec, IReadOnlyList<LoopbackTransport> mesh, List<NetCombatSuites.Ends> ends, List<LoadBoard> boards)
    {
        int guest = ends.Count;
        ends.Add(NetCombatSuites.Ends.Open(ctx, spec, mesh[guest], isHost: false, HostSeed + (ulong)guest, null));
        ctx.Check(ends[guest].Built, $"[board] guest {guest} builds ({ends[guest].Built})");
        Stand(ctx, boards, ends.Count);
        return ends[guest].Built;
    }

    // Boards up to that many, each the Dogfight one the launcher would stand.
    private static void Stand(TestContext ctx, List<LoadBoard> boards, int count)
    {
        while (boards.Count < count)
        {
            var board = LoadBoard.Build(
                ctx.DataRoot, ctx.Data.Rimage, ctx.Zrdr, ctx.Data.Messages, false, "DEATHMATCH", null,
                briefing: LoadScreens.MultiplayerKey("C1", 'd'));
            ctx.Host.AddChild(board);
            boards.Add(board);
        }
    }

    // Steps every machine and writes its board as the launcher's frame does, then chalks it on by
    // the step. Answers the shortest line the first watched boards showed after any step.
    private static int Hold(
        List<NetCombatSuites.Ends> ends, List<LoadBoard> boards, Mech3.Messages messages, int steps, int watched = 0)
    {
        var sessions = ends.Select(e => e.Session).ToArray();
        int shortest = int.MaxValue;
        for (int i = 0; i < steps; i++)
        {
            Step(1, sessions);
            for (int s = 0; s < sessions.Length; s++)
            {
                Launcher.HoldLoadBoard(boards[s], sessions[s], messages);
                boards[s].Chalk(GameClock.FixedDt);
            }

            for (int s = 0; s < watched; s++)
            {
                shortest = Math.Min(shortest, boards[s].Written.Length);
            }
        }

        return shortest;
    }

    // Every board has the whole line written, on the board it draws, with the load gate still closed.
    private static void Written(TestContext ctx, List<LoadBoard> boards, string line)
    {
        for (int i = 0; i < boards.Count; i++)
        {
            string shown = boards[i].Shown?.Overlays.LastOrDefault()?.Lines.FirstOrDefault()?.Text ?? "-";
            ctx.Check(boards[i].Written == line && shown == line,
                $"[board] machine {i} writes '{line}' onto its board ('{boards[i].Written}', drawn '{shown}')");
        }

        ctx.Check(LoadGate.Closed, $"[board] the load gate stays closed under the waiting line");
    }

    // A whole number of fixed steps lasting at least that many seconds.
    private static int StepsOf(double seconds) => (int)Math.Ceiling(seconds / GameClock.FixedDt);

    // Every session through the same number of fixed steps, host first.
    private static void Step(int steps, params GameSession[] sessions)
    {
        for (int i = 0; i < steps; i++)
        {
            foreach (var session in sessions)
            {
                session._PhysicsProcess(GameClock.FixedDt);
            }
        }
    }
}
