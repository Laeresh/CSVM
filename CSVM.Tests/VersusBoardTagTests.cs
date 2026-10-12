using CSVM.Flight.Modes;
using CSVM.Net;
using CSVM.UI.Screens;
using Xunit;

namespace CSVM.Tests;

/// <summary>The results board's tag cell and headline. A person keeps the seat's player tag; a seat
/// a computer pilot flies reads its callsign and the bot tag. The seat list is read by player index.
/// </summary>
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
    public void ABotsRowReadsItsCallsignAndTheBotTagWhilePeopleKeepTheirPlayerTags()
    {
        Assert.Equal("Crawford BOT", VersusBoard.RowTag(2, BotName));

        // ABLE-TO-FAIL CONTROL: a person's row, a seat past the list and a board given no seat list
        // read the plain player tag, never a callsign.
        Assert.Equal("P1", VersusBoard.RowTag(0, BotName));
        Assert.Equal("P2", VersusBoard.RowTag(1, BotName));
        Assert.Equal("P4", VersusBoard.RowTag(3, BotName));
        Assert.Equal("P3", VersusBoard.RowTag(2, null));
    }

    [Fact]
    public void ABotThatWinsIsNamedInTheHeadlineByItsCallsign()
    {
        var match = new VersusMatch(3, killTarget: 0, timeLimit: 60f);
        match.RegisterKill(2, 0);

        Assert.Equal("CRAWFORD WINS", VersusBoard.Title(match, BotName));

        // ABLE-TO-FAIL CONTROL: a person who wins keeps the player tag.
        var won = new VersusMatch(3, killTarget: 0, timeLimit: 60f);
        won.RegisterKill(1, 2);
        Assert.Equal("P2 WINS", VersusBoard.Title(won, BotName));
    }

    private static string? BotName(int seat) =>
        seat >= 0 && seat < Seats.Length && Seats[seat].IsBot ? Seats[seat].Callsign : null;
}
