using CSVM.Flight.Modes;
using CSVM.Net;
using Xunit;

namespace CSVM.Tests;

/// <summary>The versus status line's leader, named as the kill line names a seat: by its callsign,
/// a person's nickname or a bot's. A seat with none keeps its player tag.</summary>
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

        Assert.Equal("LEADER Crawford", VersusStatusLine.LeaderText(match, 0, seat => NetSeats.SeatName(Seats, seat)));

        // ABLE-TO-FAIL CONTROL: with no seat names the bot reads its player tag.
        Assert.Equal("LEADER P3", VersusStatusLine.LeaderText(match, 0, null));
    }

    [Fact]
    public void APersonWhoLeadsIsNamedByTheirNicknameAndATieStaysTied()
    {
        var won = new VersusMatch(3, killTarget: 0, timeLimit: 60f);
        won.RegisterKill(1, 2);
        Assert.Equal("LEADER Lucy", VersusStatusLine.LeaderText(won, 0, seat => NetSeats.SeatName(Seats, seat)));

        var level = new VersusMatch(3, killTarget: 0, timeLimit: 60f);
        Assert.Equal("LEADER TIED", VersusStatusLine.LeaderText(level, 0, seat => NetSeats.SeatName(Seats, seat)));
    }

    [Fact]
    public void ALocalSplitscreenSeatWithNoNicknameKeepsItsPlayerTag()
    {
        var panes = NetSeats.LocalPanes(2);
        var won = new VersusMatch(2, killTarget: 0, timeLimit: 60f);
        won.RegisterKill(1, 0);
        Assert.Equal("LEADER P2", VersusStatusLine.LeaderText(won, 0, seat => NetSeats.SeatName(panes, seat)));

        // A seat whose callsign is empty, or past the roster, falls back to the tag too.
        var blank = new[] { new NetSeat { SeatIndex = 0, FlownHere = true }, new NetSeat { SeatIndex = 1, FlownHere = true } };
        Assert.Equal("LEADER P2", VersusStatusLine.LeaderText(won, 0, seat => NetSeats.SeatName(blank, seat)));
        Assert.Null(NetSeats.SeatName(Seats, 3));
    }
}
