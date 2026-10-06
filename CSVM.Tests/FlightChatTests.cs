using System;
using System.Linq;
using CSVM.Flight.Hud;
using CSVM.Net;
using CSVM.Session.World;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The in-flight chat: the panel's five lines and ten seconds, the entry's typing rules, the
/// message on the wire, and the star's addressing. A team line reaches the machines flying the
/// typist's lobby team and no other, once per machine, and an all-chat reaches every machine once.
/// The console's <c>ejectflag</c> typed into the entry is no chat.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class FlightChatTests
{
    private const ulong Seed = 0xC4A7UL;

    [Fact]
    public void ThePanelKeepsFiveLinesAndShowsForTenSecondsAfterTheNewest()
    {
        var chat = new FlightChat();
        Assert.False(chat.Shown);
        for (int i = 1; i <= 6; i++)
        {
            chat.Post($"line {i}");
        }

        Assert.Equal(new[] { "line 2", "line 3", "line 4", "line 5", "line 6" }, chat.Lines);
        chat.Advance(FlightChat.ShowSeconds - 0.1f);
        Assert.True(chat.Shown);
        chat.Advance(0.2f);
        Assert.False(chat.Shown);

        // ABLE-TO-FAIL CONTROL: a hidden panel keeps its lines, and the next one shows them all again.
        chat.Post("line 7");
        Assert.True(chat.Shown);
        Assert.Equal(FlightChat.Depth, chat.Lines.Count);
        Assert.Equal("line 7", chat.Lines[^1]);
    }

    [Fact]
    public void TheEntryTakesPrintableCharactersUpToItsBufferAndSendsTheTrimmedLine()
    {
        var chat = new FlightChat();
        Assert.False(chat.Type('a'));
        chat.Open(toTeam: true, "To Team:");
        Assert.True(chat.Shown);
        Assert.False(chat.Type('\n'));
        foreach (char c in "  hi  ")
        {
            chat.Type(c);
        }

        chat.Erase();
        Assert.Equal("To Team: >   hi ", chat.EntryLine);
        Assert.Equal("hi", chat.Submit());
        Assert.False(chat.Typing);

        chat.Open(toTeam: false, "To All:");
        for (int i = 0; i < FlightChat.MaxTyped + 5; i++)
        {
            chat.Type('x');
        }

        Assert.Equal(FlightChat.MaxTyped, chat.Draft.Length);

        // ABLE-TO-FAIL CONTROL: a blank line closes the entry and sends nothing.
        chat.Cancel();
        chat.Open(toTeam: false, "To All:");
        chat.Type(' ');
        Assert.Null(chat.Submit());
        Assert.False(chat.Typing);
    }

    [Fact]
    public void AReceivedLineNamesItsSenderAndIsCutToEightyCharacters()
    {
        Assert.Equal("To All:  hello", FlightChat.Echo("To All:", "hello"));
        Assert.Equal("Lucy: hello", FlightChat.Received("Lucy", "hello"));
        string line = FlightChat.Received("Lucy", new string('y', 100));
        Assert.Equal("Lucy: " + new string('y', FlightChat.MaxReceived), line);
    }

    [Fact]
    public void TheMessageCarriesTheSeatTheTeamFlagAndAFullLine()
    {
        var buffer = new byte[FlightChatMessage.Size];
        string full = new('z', FlightChat.MaxReceived);
        foreach (var sent in new[] { new FlightChatMessage(3, true, "cover me"), new FlightChatMessage(0, false, full) })
        {
            Assert.Equal(FlightChatMessage.Size, sent.Write(buffer));
            Assert.True(FlightChatMessage.TryRead(buffer, out var got));
            Assert.Equal(sent, got);
        }

        Assert.Equal(0x66, (int)NetMessageType.FlightChat);
        Assert.Equal(NetReliability.Reliable, NetMessage.ReliabilityOf(NetMessageType.FlightChat));
        Assert.False(FlightChatMessage.TryRead(buffer.AsSpan(0, FlightChatMessage.Size - 1), out _));
    }

    [Fact]
    public void ATeamLineReachesTheTypistsTeamOnceAMachineAndNoOtherTeam()
    {
        var (sessions, links) = Field(teams: true);
        var (host, first, second) = (links[0], links[1], links[2]);

        Say(first, sessions, seat: 1, team: true, "on me");
        Assert.Equal(new[] { "Blue: on me" }, host.Chat.Lines);
        Assert.Equal(new[] { NetChatLink.TeamPromptKey + "  on me" }, first.Chat.Lines);
        Assert.Empty(second.Chat.Lines);

        // The host's own team line, to a guest flying two of the team's seats: one copy.
        Say(host, sessions, seat: 0, team: true, "break left");
        Assert.Equal(new[] { NetChatLink.TeamPromptKey + "  on me", "Red: break left" }, first.Chat.Lines);
        Assert.Empty(second.Chat.Lines);

        // ABLE-TO-FAIL CONTROL: the other team's line reaches its own machine alone.
        int relayed = sessions[0].Relayed;
        Say(second, sessions, seat: 2, team: true, "we are alone");
        Assert.Equal(2, host.Chat.Lines.Count);
        Assert.Equal(2, first.Chat.Lines.Count);
        Assert.Single(second.Chat.Lines);
        Assert.Equal(relayed, sessions[0].Relayed);
    }

    [Fact]
    public void AnAllChatReachesEveryMachineOnceAndASpoofedSeatNobody()
    {
        var (sessions, links) = Field(teams: true);
        Say(links[2], sessions, seat: 2, team: false, "gg");
        Assert.Equal(new[] { "Green: gg" }, links[0].Chat.Lines);
        Assert.Equal(new[] { "Green: gg" }, links[1].Chat.Lines);
        Assert.Single(links[2].Chat.Lines);

        // ABLE-TO-FAIL CONTROL: a guest's line under another machine's seat is dropped, not relayed.
        sessions[2].Send(sessions[2].HostPeer, new FlightChatMessage(1, false, "not me"), NetChannels.Events);
        Pump(sessions);
        Assert.Single(links[0].Chat.Lines);
        Assert.Single(links[1].Chat.Lines);
    }

    [Fact]
    public void TheTeamKeyOpensAnAllChatForASeatOnNoTeam()
    {
        var (sessions, links) = Field(teams: false);
        links[1].Open(1, team: true);
        Assert.False(links[1].Chat.ToTeam);
        Assert.Equal(NetChatLink.AllPromptKey, links[1].Chat.Prompt);
        links[1].Chat.Type('o');
        links[1].Submit();
        Pump(sessions);
        Assert.Single(links[2].Chat.Lines);

        // ABLE-TO-FAIL CONTROL: on a team, the same key opens a team line.
        var (_, teamed) = Field(teams: true);
        teamed[1].Open(1, team: true);
        Assert.True(teamed[1].Chat.ToTeam);
    }

    [Fact]
    public void TheKeysBelongToTheChatUntilEveryKeyPressedIntoItIsReleased()
    {
        var (sessions, links) = Field(teams: true);
        var link = links[1];
        Assert.False(link.TakeKey(Key.H, true, 'h'));
        Assert.False(link.HoldsKeyboard);

        link.Open(1, team: false);
        Assert.True(link.TakeKey(Key.H, true, 'h'));
        Assert.True(link.TakeKey(Key.H, false, '\0'));
        Assert.True(link.TakeKey(Key.I, true, 'i'));
        Assert.True(link.TakeKey(Key.Backspace, true, '\b'));
        Assert.True(link.TakeKey(Key.Backspace, false, '\0'));
        Assert.True(link.TakeKey(Key.I, true, 'i'));
        Assert.True(link.TakeKey(Key.Enter, true, '\r'));
        Assert.False(link.Chat.Typing);
        Pump(sessions);
        Assert.Equal(new[] { "Blue: hi" }, links[0].Chat.Lines);

        // Enter and I are still down, so the seat's keys stay idle until both come up.
        Assert.True(link.HoldsKeyboard);
        Assert.True(link.TakeKey(Key.Enter, false, '\0'));
        Assert.True(link.HoldsKeyboard);
        Assert.True(link.TakeKey(Key.I, false, '\0'));
        Assert.False(link.HoldsKeyboard);

        // ABLE-TO-FAIL CONTROL: Escape drops the line and sends nothing.
        link.Open(1, team: false);
        link.TakeKey(Key.X, true, 'x');
        link.TakeKey(Key.Escape, true, '\u001b');
        Pump(sessions);
        Assert.Single(links[0].Chat.Lines);
        Assert.Equal(1, link.LinesSent);
    }

    [Fact]
    public void TheConsolesEjectflagRunsForTheTypistAndLeavesNoChatLine()
    {
        var (sessions, links) = Field(teams: true);
        var ejected = new System.Collections.Generic.List<int>();
        links[1].EjectFlag = ejected.Add;
        links[1].Open(3, team: false);
        foreach (char c in "  ejectflag now ")
        {
            links[1].Chat.Type(c);
        }

        Assert.False(links[1].Submit());
        Pump(sessions);
        Assert.Equal(new[] { 3 }, ejected);
        Assert.Empty(links[1].Chat.Lines);
        Assert.Empty(links[0].Chat.Lines);
        Assert.Equal(0, links[1].LinesSent);

        // Outside Capture the Flag the hook is unset, and the command is still no chat line.
        links[2].Open(2, team: false);
        foreach (char c in NetChatLink.EjectFlagCommand)
        {
            links[2].Chat.Type(c);
        }

        Assert.False(links[2].Submit());
        Pump(sessions);
        Assert.Empty(links[0].Chat.Lines);

        // ABLE-TO-FAIL CONTROL: the compare is whole-word and case-sensitive, so these are chat.
        Assert.False(NetChatLink.IsEjectFlag("EjectFlag"));
        Assert.False(NetChatLink.IsEjectFlag("ejectflags"));
        Assert.False(NetChatLink.IsEjectFlag("please ejectflag"));
        Say(links[1], sessions, seat: 1, team: false, "EjectFlag");
        Assert.Equal(new[] { "Blue: EjectFlag" }, links[0].Chat.Lines);
        Assert.Equal(new[] { 3 }, ejected);
    }

    // The host on seat 0 and two guests, the first flying seats 1 and 3. With teams, seats 0, 1 and
    // 3 fly lobby team 1 and seat 2 team 2.
    private static (NetSession[] Sessions, NetChatLink[] Links) Field(bool teams)
    {
        var mesh = LoopbackTransport.Mesh(3, LoopbackConditions.Perfect, new Random(teams ? 86 : 87));
        int[] team = teams ? new[] { 1, 1, 2, 1 } : new[] { 0, 0, 0, 0 };
        string[] names = { "Red", "Blue", "Green", "Gold" };
        int[] peers = { 0, 1, 2, 1 };
        var roster = Enumerable.Range(0, 4).Select(seat => new NetSeat
        {
            PeerId = peers[seat],
            SeatIndex = seat,
            TeamId = team[seat],
            FlownHere = seat == 0,
            Callsign = names[seat],
        }).ToArray();
        var sessions = new[]
        {
            NetSession.Host(mesh[0], roster, Seed),
            NetSession.Guest(mesh[1]),
            NetSession.Guest(mesh[2]),
        };
        Pump(sessions);
        Assert.True(sessions.All(s => s.Joined));
        var links = sessions.Select(s => NetChatLink.Open(s, null)).ToArray();
        return (sessions, links);
    }

    private static void Say(NetChatLink link, NetSession[] sessions, int seat, bool team, string text)
    {
        link.Open(seat, team);
        foreach (char c in text)
        {
            link.Chat.Type(c);
        }

        Assert.True(link.Submit());
        Pump(sessions);
    }

    private static void Pump(NetSession[] sessions)
    {
        for (int i = 0; i < 4; i++)
        {
            foreach (var session in sessions)
            {
                session.Step(0.016);
            }
        }
    }
}
