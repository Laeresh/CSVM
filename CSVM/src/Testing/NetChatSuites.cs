using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Hud;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session.Launch;
using CSVM.Session.World;
using CSVM.Utils;

namespace CSVM.Testing;

/// <summary>The in-flight chat between whole sessions in one process, on
/// <see cref="NetCombatSuites"/>'s rig. The host and the first guest fly lobby team 1, the second
/// guest team 2. A line is typed through one machine's link and read off every machine's chat,
/// since the host's relay decides what reaches each.</summary>
internal static class NetChatSuites
{
    private const ulong HostSeed = 0xC0FFEE86UL;

    private const int SettleSteps = 20;

    private static readonly int[] Teams = { 1, 1, 2 };

    private static readonly Dictionary<int, string> TeamNames = new() { [1] = "Red Squadron", [2] = "Blue Angels" };

    [Suite("net-flight-chat",
        "three sessions in one process, the host and one guest on lobby team 1 and the other guest on "
        + "team 2: every machine opens one chat drawn in each local pane, the entry opening under the "
        + "original's To Team: prompt holds the keyboard seat's keys idle until the line is sent, a "
        + "team line reaches the typist's teammate and not the other team, the other team's line "
        + "reaches no machine but its own, and an all-chat reaches every machine once under the "
        + "typist's callsign")]
    internal static void ChatAcrossThreeMachines(TestContext ctx)
    {
        var spec = NetCombatSuites.MatchSpec(ctx, out _);
        var mesh = LoopbackTransport.Mesh(3, LoopbackConditions.Perfect, new Random(8601));
        var roster = NetCombatSuites.Roster(3).Select((seat, i) => seat with { TeamId = Teams[i] }).ToArray();
        var ambient = NetCombatSuites.Ambient.Save();
        var ends = new List<NetCombatSuites.Ends>();
        try
        {
            for (int i = 0; i < 3; i++)
            {
                ends.Add(NetCombatSuites.Ends.Open(ctx, spec, mesh[i], isHost: i == 0, HostSeed + (ulong)i,
                    i == 0 ? roster : null, teamNames: TeamNames));
            }

            ctx.Check(ends.All(e => e.Built), $"three sessions build in one process ({string.Join(", ", ends.Select(e => e.Built))})");
            if (!ends.All(e => e.Built))
            {
                return;
            }

            var peers = ends.Select(e => e.Session).ToArray();
            Lockstep(SettleSteps, peers);
            var strings = Messages.Load(ctx.MessagesPath);
            Panes(ctx, peers);
            Entry(ctx, peers, strings);
            TeamLine(ctx, peers, strings);
            AllChat(ctx, peers);
        }
        finally
        {
            foreach (var end in Enumerable.Reverse(ends))
            {
                end.Close();
            }

            ambient.Restore();
        }
    }

    // One chat per machine, drawn by the machine's one pane, whose pilot reads the keyboard and so
    // draws the entry too.
    private static void Panes(TestContext ctx, GameSession[] peers)
    {
        string reading = string.Join(", ", peers.Select(p => $"{(p.NetChat != null ? "chat" : "none")}/{p.ChatPanels.Count} panel(s)"));
        ctx.Check(peers.All(p => p.NetChat != null && p.ChatPanels.Count == 1
                                 && ReferenceEquals(p.ChatPanels[0].Chat, p.NetChat.Chat) && p.ChatPanels[0].ShowsEntry),
            $"every machine opens one chat, drawn in its pane with the entry ({reading})");
    }

    // The entry opens under the original's prompt and holds the typist's keys until the line leaves.
    private static void Entry(TestContext ctx, GameSession[] peers, Messages strings)
    {
        var guest = peers[1];
        var pilot = guest.SeatRigs[1].Controller!;
        ctx.Check(pilot.KeyboardHeld?.Invoke() == false, $"the guest's keyboard seat flies on its keys with no line open");
        guest.NetChat!.Open(1, team: true);
        ctx.Check(guest.NetChat.Chat is { Typing: true, ToTeam: true } && guest.NetChat.Chat.Prompt == strings.Get(NetChatLink.TeamPromptKey),
            $"the team key opens the entry under {strings.Get(NetChatLink.TeamPromptKey)} ({guest.NetChat.Chat.Prompt})");
        ctx.Check(pilot.KeyboardHeld?.Invoke() == true,
            $"ABLE-TO-FAIL CONTROL: and while it is open the seat reads its keyboard idle");
        guest.NetChat.Chat.Cancel();
        ctx.Check(pilot.KeyboardHeld?.Invoke() == false, $"and has its keys back once the line is dropped");
    }

    // A team line from the first guest: the host flies team 1 and shows it, the second guest does not.
    private static void TeamLine(TestContext ctx, GameSession[] peers, Messages strings)
    {
        Say(peers, 1, team: true, "on me");
        string want = FlightChat.Received(peers[0].NetSeats[1].Callsign, "on me");
        ctx.Check(Lines(peers[0]).SequenceEqual(new[] { want }),
            $"the host, on the typist's team, shows {want} once ({Reading(peers)})");
        ctx.Check(Lines(peers[1]).SequenceEqual(new[] { FlightChat.Echo(strings.Get(NetChatLink.TeamPromptKey), "on me") }),
            $"the typist's own panel shows its echo ({Reading(peers)})");
        ctx.Check(Lines(peers[2]).Count == 0, $"and the other team's machine shows nothing ({Reading(peers)})");

        int posted = peers[1].NetChat!.Chat.Posted;
        Say(peers, 2, team: true, "alone");
        ctx.Check(Lines(peers[0]).Count == 1 && Lines(peers[1]).Count == 1 && Lines(peers[2]).Count == 1
                  && peers[1].NetChat!.Chat.Posted == posted,
            $"ABLE-TO-FAIL CONTROL: team 2's line reaches no machine but its own ({Reading(peers)})");
    }

    // An all-chat from the second guest reaches both other machines once, named by its callsign.
    private static void AllChat(TestContext ctx, GameSession[] peers)
    {
        Say(peers, 2, team: false, "gg");
        string want = FlightChat.Received(peers[0].NetSeats[2].Callsign, "gg");
        ctx.Check(Lines(peers[0]).Count(l => l == want) == 1 && Lines(peers[1]).Count(l => l == want) == 1,
            $"an all-chat reaches the host and the other team's guest once each as {want} ({Reading(peers)})");
        ctx.Check(peers.All(p => p.NetChat!.Chat.Shown), $"and every machine's panel is up");
    }

    private static void Say(GameSession[] peers, int seat, bool team, string text)
    {
        var link = peers[seat].NetChat!;
        link.Open(seat, team);
        foreach (char c in text)
        {
            link.Chat.Type(c);
        }

        link.Submit();
        Lockstep(SettleSteps, peers);
    }

    private static IReadOnlyList<string> Lines(GameSession peer) => peer.NetChat!.Chat.Lines;

    private static string Reading(GameSession[] peers) =>
        string.Join(" | ", peers.Select((p, i) => $"m{i}: {string.Join(" / ", Lines(p))}"));

    // Every session through the same fixed steps, host first, the order a listen server runs in.
    private static void Lockstep(int steps, params GameSession[] sessions)
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
