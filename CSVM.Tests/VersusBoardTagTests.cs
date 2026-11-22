using CSVM.Flight.Modes;
using CSVM.Net;
using CSVM.UI.Screens;
using Xunit;

namespace CSVM.Tests;

/// <summary>The results board's tag cell and headline. A seat reads its callsign, a person's
/// nickname or a bot's, and a bot's carries the bot tag. A seat with none keeps its player tag.
/// The seat list is read by player index.</summary>
[Trait("Tier", "Quick")]
public sealed class VersusBoardTagTests
{
    private static readonly NetSeat[] Seats =
    {
        new() { SeatIndex = 0, Callsign = "Zachary", FlownHere = true },
        new() { SeatIndex = 1, PeerId = 1, Callsign = "Lucy" },
        NetSeats.Bot(0, 2, "Crawford", "player_fury"),
    };

    [Fact]
    public void ABotsRowReadsItsCallsignAndTheBotTagWhilePeopleReadTheirNicknames()
    {
        Assert.Equal("Crawford BOT", VersusBoard.RowTag(2, Seats));
        Assert.Equal("Zachary", VersusBoard.RowTag(0, Seats));
        Assert.Equal("Lucy", VersusBoard.RowTag(1, Seats));

        // ABLE-TO-FAIL CONTROL: a seat past the list and a board given no seat list read the plain
        // player tag, never a callsign.
        Assert.Equal("P4", VersusBoard.RowTag(3, Seats));
        Assert.Equal("P3", VersusBoard.RowTag(2, null));
    }

    [Fact]
    public void TheWinnerIsNamedInTheHeadlineByCallsign()
    {
        var match = new VersusMatch(3, killTarget: 0, timeLimit: 60f);
        match.RegisterKill(2, 0);
        Assert.Equal("CRAWFORD WINS", VersusBoard.Title(match, Seats));

        var won = new VersusMatch(3, killTarget: 0, timeLimit: 60f);
        won.RegisterKill(1, 2);
        Assert.Equal("LUCY WINS", VersusBoard.Title(won, Seats));

        // ABLE-TO-FAIL CONTROL: a board given no seat list names the winner by player tag.
        Assert.Equal("P2 WINS", VersusBoard.Title(won));
    }
}
