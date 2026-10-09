using CSVM.Flight.Modes;
using CSVM.Net;
using Xunit;

namespace CSVM.Tests;

/// <summary>The versus status line's leader, named by the death line's rule: a bot by its
/// callsign, a person by the seat's player tag.</summary>
[Trait("Tier", "Quick")]
public sealed class VersusLeaderTextTests
{
    private static readonly NetSeat[] Seats =
    {
        new() { SeatIndex = 0, Callsign = "Zachary", FlownHere = true },
        new() { SeatIndex = 1, PeerId = 1, Callsign = "Lucy" },
        NetSeats.Bot(0, 2, "Crawford", "player_fury"),
    };

    [Fact]
    public void ABotThatLeadsIsNamedByItsCallsign()
    {
        var match = new VersusMatch(3, killTarget: 0, timeLimit: 60f);
        match.RegisterKill(2, 0);

        Assert.Equal("LEADER Crawford", VersusStatusLine.LeaderText(match, 0, seat => NetSeats.BotCallsign(Seats, seat)));

        // ABLE-TO-FAIL CONTROL: with no bot names the bot reads its player tag, as before.
        Assert.Equal("LEADER P3", VersusStatusLine.LeaderText(match, 0, null));
    }

    [Fact]
    public void APersonWhoLeadsKeepsThePlayerTagAndATieStaysTied()
    {
        var won = new VersusMatch(3, killTarget: 0, timeLimit: 60f);
        won.RegisterKill(1, 2);
        Assert.Equal("LEADER P2", VersusStatusLine.LeaderText(won, 0, seat => NetSeats.BotCallsign(Seats, seat)));

        var level = new VersusMatch(3, killTarget: 0, timeLimit: 60f);
        Assert.Equal("LEADER TIED", VersusStatusLine.LeaderText(level, 0, seat => NetSeats.BotCallsign(Seats, seat)));
    }
}
