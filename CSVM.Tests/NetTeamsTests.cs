using System;
using System.Linq;
using CSVM.Net;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The team core and its two lobby messages. Free-form named teams are created, joined and left, and
/// a captain's leave or drop disbands its team. A new team takes the lowest free number. Every team
/// mode shares the launch checks.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class NetTeamsTests
{
    [Fact]
    public void ACreatorCaptainsItsTeamAndJoinersFollowInOrder()
    {
        var book = new NetTeamBook();
        var created = book.Create(10, "Red Skulls");
        Assert.Equal(new[] { NetTeamEventKind.Joined }, created.Select(e => e.Kind));
        Assert.Equal(1, book.TeamOf(10));
        Assert.True(book.IsCaptain(10));

        Assert.Single(book.Join(11, 1));
        Assert.Equal(1, book.TeamOf(11));
        Assert.False(book.IsCaptain(11));
        Assert.Equal(new[] { 10, 11 }, book.Find(1)!.Members);
        Assert.Equal("Red Skulls", book.Find(1)!.Name);

        // ABLE-TO-FAIL CONTROL: a member on a team cannot join another, and a missing team is no team.
        book.Create(12, "Blue");
        Assert.Empty(book.Join(11, 2));
        Assert.Empty(book.Join(13, 9));
        Assert.Equal(1, book.TeamOf(11));
        Assert.Equal(0, book.TeamOf(13));
    }

    [Fact]
    public void AMemberLeavesAloneButACaptainsLeaveDisbandsTheTeam()
    {
        var book = new NetTeamBook();
        book.Create(1, "A");
        book.Join(2, 1);
        book.Join(3, 1);

        var left = book.Leave(3);
        Assert.Equal(new[] { NetTeamEventKind.Left }, left.Select(e => e.Kind));
        Assert.Equal(1, book.Count);

        var disbanded = book.Leave(1);
        Assert.Equal(new[] { NetTeamEventKind.Left, NetTeamEventKind.Left, NetTeamEventKind.Disbanded }, disbanded.Select(e => e.Kind));
        Assert.Equal(0, book.Count);
        Assert.Equal(0, book.TeamOf(2));
        Assert.Empty(book.Leave(2));
    }

    [Fact]
    public void ANewTeamTakesTheLowestFreeNumber()
    {
        var book = new NetTeamBook();
        book.Create(1, "A");
        book.Create(2, "B");
        book.Create(3, "C");
        book.Leave(2);

        book.Create(4, "D");
        Assert.Equal(2, book.TeamOf(4));
        book.Create(5, "E");
        Assert.Equal(4, book.TeamOf(5));
    }

    [Fact]
    public void ABlankNameBecomesTheDefaultAndALongOneIsCut()
    {
        Assert.Equal(NetTeamBook.DefaultName, NetTeamBook.Kept("   "));
        Assert.Equal(NetTeamBook.DefaultName, NetTeamBook.Kept(null));
        Assert.Equal(NetTeamBook.NameChars, NetTeamBook.Kept(new string('x', 30)).Length);
        Assert.Equal("Aces", NetTeamBook.Kept("  Aces "));
    }

    [Fact]
    public void AMemberWhoIsGoneLeavesAndAGoneCaptainsTeamIsDisbanded()
    {
        var book = new NetTeamBook();
        book.Create(1, "A");
        book.Join(2, 1);
        book.Create(3, "B");
        book.Join(4, 2);

        var events = book.Keep(new[] { 1, 3 });
        Assert.Equal(1, book.TeamOf(1));
        Assert.Equal(0, book.TeamOf(2));
        Assert.Equal(2, book.TeamOf(3));
        Assert.Contains(events, e => e.Kind == NetTeamEventKind.Left && e.Member == 2);

        var boot = book.Keep(new[] { 1 });
        Assert.Contains(boot, e => e.Kind == NetTeamEventKind.Disbanded && e.Team == 2);
        Assert.Null(book.Find(2));
    }

    [Fact]
    public void AFreeForAllLaunchesUnlessRestrictAsksForTeams()
    {
        var alone = new (byte, int)[] { (0, 1), (0, 1), (0, 1) };
        Assert.Equal(TeamLaunchRefusal.None, NetTeamBook.Check(alone, false, 2, 4));
        Assert.Equal(TeamLaunchRefusal.TooFewTeams, NetTeamBook.Check(alone, true, 2, 4));
        Assert.Equal(TeamLaunchRefusal.None, NetTeamBook.Check(alone, true, 0, 4));
    }

    [Fact]
    public void RestrictBoundsTheTeamCount()
    {
        var three = new (byte, int)[] { (1, 1), (2, 1), (3, 1) };
        Assert.Equal(TeamLaunchRefusal.TooManyTeams, NetTeamBook.Check(three, true, 2, 2));
        Assert.Equal(TeamLaunchRefusal.TooFewTeams, NetTeamBook.Check(three, true, 4, 6));
        Assert.Equal(TeamLaunchRefusal.None, NetTeamBook.Check(three, true, 2, 4));
        Assert.Equal(TeamLaunchRefusal.None, NetTeamBook.Check(three, false, 0, 0));
    }

    [Fact]
    public void ATeamMatchNeedsTwoPlayersTwoTeamsAndEveryoneOnATeam()
    {
        Assert.Equal(TeamLaunchRefusal.NotEnoughPlayers, NetTeamBook.Check(new (byte, int)[] { (1, 1) }, false, 0, 16));
        Assert.Equal(TeamLaunchRefusal.TooFewTeams, NetTeamBook.Check(new (byte, int)[] { (1, 1), (1, 1) }, false, 0, 16));
        Assert.Equal(TeamLaunchRefusal.Teamless, NetTeamBook.Check(new (byte, int)[] { (1, 1), (2, 1), (0, 1) }, false, 0, 16));
    }

    [Fact]
    public void TeamsMayDifferByOnePlayerAndSplitscreenSeatsCount()
    {
        Assert.Equal(TeamLaunchRefusal.None, NetTeamBook.Check(new (byte, int)[] { (1, 1), (1, 1), (2, 1) }, false, 0, 16));
        Assert.Equal(TeamLaunchRefusal.Unbalanced, NetTeamBook.Check(new (byte, int)[] { (1, 1), (1, 1), (1, 1), (2, 1) }, false, 0, 16));

        // A machine flying three splitscreen seats on team 1 outweighs a lone pilot on team 2.
        Assert.Equal(TeamLaunchRefusal.Unbalanced, NetTeamBook.Check(new (byte, int)[] { (1, 3), (2, 1) }, false, 0, 16));
        Assert.Equal(TeamLaunchRefusal.None, NetTeamBook.Check(new (byte, int)[] { (1, 2), (2, 1) }, false, 0, 16));
    }

    [Fact]
    public void TheTeamActionAndTheTeamListRoundTrip()
    {
        var action = new byte[LobbyTeamActionMessage.Size];
        var sent = new LobbyTeamActionMessage(NetTeamAction.Create, 0, "Skull & Bones");
        Assert.Equal(LobbyTeamActionMessage.Size, sent.Write(action));
        Assert.True(LobbyTeamActionMessage.TryRead(action, out var got));
        Assert.Equal(sent, got);
        Assert.Equal(0x61, (int)NetMessageType.LobbyTeamAction);

        var list = new byte[LobbyTeamsMessage.Size];
        var teams = new LobbyTeamsMessage(new[] { new LobbyTeamName(1, "Red"), new LobbyTeamName(3, "Hölle") });
        Assert.Equal(LobbyTeamsMessage.Size, teams.Write(list));
        Assert.True(LobbyTeamsMessage.TryRead(list, out var read));
        Assert.Equal(teams, read);
        Assert.Equal("Hölle", read.Teams[1].Name);
        Assert.Equal(0x62, (int)NetMessageType.LobbyTeams);
        Assert.Equal(NetReliability.Reliable, NetMessage.ReliabilityOf(NetMessageType.LobbyTeams));

        // ABLE-TO-FAIL CONTROL: a cut list and an action's bytes are refused.
        Assert.False(LobbyTeamsMessage.TryRead(list.AsSpan(0, LobbyTeamsMessage.Size - 1), out _));
        Assert.False(LobbyTeamsMessage.TryRead(action, out _));
    }

    [Fact]
    public void TheWidestTeamNameSurvivesTheWire()
    {
        string wide = new('漢', 12);
        var list = new byte[LobbyTeamsMessage.Size];
        new LobbyTeamsMessage(new[] { new LobbyTeamName(1, wide) }).Write(list);
        Assert.True(LobbyTeamsMessage.TryRead(list, out var read));
        Assert.Equal(wide, read.Teams[0].Name);
    }
}
