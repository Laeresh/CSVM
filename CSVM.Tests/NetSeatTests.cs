using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Net;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The seat record and the roster rules every seat-indexed system is built against. The ceiling and
/// the table width are two separate numbers. A seat's colour is the original's authored dword where
/// one exists. A roster is numbered from zero with no gap, because the numbering IS the index into
/// the spawn walk, the score rows and the markers.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class NetSeatTests
{
    // The eight dwords at 00628eb4, the stored byte triplet read as red, green, blue. Restated
    // here rather than read off NetSeats, so a change to the table fails this and is judged.
    private static readonly uint[] Authored =
    {
        0x812D2D, 0x2D2D81, 0x2D812D, 0x81812D, 0x812D64, 0x66812D, 0x457C81, 0x662D81,
    };

    // The same eight as the executable holds them, little-endian dwords read straight off 00628eb4.
    private static readonly uint[] StoredDwords =
    {
        0x002D2D81, 0x00812D2D, 0x002D812D, 0x002D8181, 0x00642D81, 0x002D8166, 0x00817C45, 0x00812D66,
    };

    [Fact]
    public void TheStoredDwordsReadLowByteFirstAsRed()
    {
        // The original's reader (FUN_004b3660) takes the low byte as its first float, then AH, then
        // bits 16..23. The colour path packs the first float into a D3DCOLOR's red byte. So the low
        // byte is red and the order is the stored byte order.
        for (int seat = 0; seat < StoredDwords.Length; seat++)
        {
            uint dword = StoredDwords[seat];
            uint red = dword & 0xFF;
            uint green = (dword >> 8) & 0xFF;
            uint blue = (dword >> 16) & 0xFF;
            Assert.Equal((red << 16) | (green << 8) | blue, NetSeats.SeatColor(seat));
        }

        // ABLE-TO-FAIL CONTROL: the other reading, the dword's value taken as 0xRRGGBB, turns the
        // first pilot blue rather than red.
        Assert.NotEqual(StoredDwords[0] & 0xFFFFFFu, NetSeats.SeatColor(0));
        Assert.Equal(0x81u, NetSeats.SeatColor(0) >> 16);
    }

    [Fact]
    public void TheCeilingStandsBehindAWiderTable()
    {
        Assert.Equal(16, NetSeats.MaxPlayers);
        Assert.Equal(16, NetSeats.SeatCapacity);
        Assert.True(NetSeats.SeatCapacity >= NetSeats.MaxPlayers,
            "every seat-indexed table has to hold the whole ceiling");
    }

    [Fact]
    public void TheFirstEightColoursAreTheOriginalsOwn()
    {
        Assert.Equal(NetSeats.SeatCapacity, NetSeats.SeatColors.Count);
        for (int seat = 0; seat < Authored.Length; seat++)
        {
            Assert.Equal(Authored[seat], NetSeats.SeatColor(seat));
        }
    }

    [Fact]
    public void EverySeatHasAColourAndNoTwoShareOne()
    {
        var seen = new HashSet<uint>();
        for (int seat = 0; seat < NetSeats.SeatCapacity; seat++)
        {
            uint colour = NetSeats.SeatColor(seat);
            Assert.True(colour <= 0xFFFFFFu, $"seat {seat}'s colour {colour:x} is not an 0xRRGGBB value");
            Assert.True(seen.Add(colour), $"seat {seat} repeats colour {colour:x6}");
        }
    }

    [Fact]
    public void TheDerivedColoursAreTheStatedComplement()
    {
        for (int seat = Authored.Length; seat < NetSeats.SeatCapacity; seat++)
        {
            Assert.Equal(~Authored[seat - Authored.Length] & 0xFFFFFFu, NetSeats.SeatColor(seat));
        }
    }

    [Fact]
    public void ASeatOutsideTheTableIsARefusalRatherThanAWrapAround()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NetSeats.SeatColor(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => NetSeats.SeatColor(NetSeats.SeatCapacity));
    }

    [Fact]
    public void ASeatCarriesItsOwnColour()
    {
        var seat = new NetSeat { SeatIndex = 3, Callsign = "Paladin", FlownHere = true };
        Assert.Equal(NetSeats.SeatColor(3), seat.Color);
    }

    [Fact]
    public void ASeatIsTheDecodedRecordsFields()
    {
        var seat = new NetSeat
        {
            PeerId = 42,
            SeatIndex = 1,
            TeamId = 2,
            FlownHere = false,
            Callsign = "Nathan Zachary",
            PlaneNode = "player_bhawk",
            Livery = "red",
            Score = -3,
        };

        Assert.Equal(42, seat.PeerId);
        Assert.Equal(2, seat.TeamId);
        Assert.False(seat.FlownHere);
        Assert.Equal("Nathan Zachary", seat.Callsign);
        Assert.Equal("player_bhawk", seat.PlaneNode);
        Assert.Equal("red", seat.Livery);
        Assert.Equal(-3, seat.Score);
        // Signed, because the original's own scoring takes a point off a death with no killer.
        Assert.True(seat.Score < 0);
    }

    [Fact]
    public void AWellFormedRosterPasses()
    {
        NetSeats.Validate(Roster(2, locals: 1));
        NetSeats.Validate(Roster(NetSeats.MaxPlayers, locals: 1));
        // A host flying splitscreen panes beside its guests: several seats are local.
        NetSeats.Validate(Roster(4, locals: 3));
    }

    [Fact]
    public void ARosterPastTheCeilingIsRefused()
    {
        Assert.Throws<ArgumentException>(() => NetSeats.Validate(Roster(NetSeats.MaxPlayers + 1, locals: 1)));
        Assert.Throws<ArgumentException>(() => NetSeats.Validate(Array.Empty<NetSeat>()));
    }

    [Fact]
    public void ARosterWithAGapOrARepeatIsRefused()
    {
        var gap = Roster(3, locals: 1).ToList();
        gap[2] = gap[2] with { SeatIndex = 7 };
        Assert.Throws<ArgumentException>(() => NetSeats.Validate(gap));

        var repeat = Roster(3, locals: 1).ToList();
        repeat[2] = repeat[2] with { SeatIndex = 1 };
        Assert.Throws<ArgumentException>(() => NetSeats.Validate(repeat));
    }

    [Fact]
    public void ARosterThisMachineFliesNoneOfIsRefused()
    {
        Assert.Throws<ArgumentException>(() => NetSeats.Validate(Roster(3, locals: 0)));
    }

    [Fact]
    public void AHostsRosterSeatsTheHostAtZero()
    {
        NetSeats.Validate(Roster(3, locals: 1), hostPeer: 1);

        var guestFirst = Roster(3, locals: 1).ToList();
        guestFirst[0] = guestFirst[0] with { PeerId = 2 };
        guestFirst[1] = guestFirst[1] with { PeerId = 1 };
        Assert.Throws<ArgumentException>(() => NetSeats.Validate(guestFirst, hostPeer: 1));

        // ABLE-TO-FAIL CONTROL: a guest's rebuilt roster names no host peer and is not held to it.
        NetSeats.Validate(guestFirst);

        Assert.Equal(1, NetSeats.CoopField(1, new[] { "player_bhawk" }, new[] { (4, "player_fury", "Lucy") })[0].PeerId);
        Assert.Equal(1, NetSeats.Field(1, new[] { "player_bhawk" }, new[] { 4 }, "player_bhawk")[0].PeerId);
    }

    [Fact]
    public void ACoopFieldSeatsEachGuestInThePlaneItPickedUnderItsName()
    {
        var field = NetSeats.CoopField(1, new[] { "player_bhawk" }, new[] { (4, "player_fury", "Lucy"), (9, "player_warhawk", " ") });

        Assert.Equal(new[] { 1, 4, 9 }, field.Select(s => s.PeerId));
        Assert.Equal(new[] { true, false, false }, field.Select(s => s.FlownHere));

        // A guest that sent a name is called by it. ABLE-TO-FAIL CONTROL: one that sent none falls
        // back to its player number.
        Assert.Equal(new[] { "P1", "Lucy", "P3" }, field.Select(s => s.Callsign));
        Assert.Equal(new[] { "player_bhawk", "player_fury", "player_warhawk" }, field.Select(s => s.PlaneNode));

        // A player number stands in for a missing name, and the seat says so.
        Assert.Equal(new[] { true, false, true }, field.Select(s => s.Unnamed));

        // ABLE-TO-FAIL CONTROL: the versus field flies every guest in one plane and names it by peer.
        var versus = NetSeats.Field(1, new[] { "player_bhawk" }, new[] { 4 }, "player_bhawk");
        Assert.Equal("guest 4", versus[1].Callsign);
        Assert.Equal("player_bhawk", versus[1].PlaneNode);
        Assert.True(versus[1].Unnamed);
    }

    // A machine's further splitscreen seats are not peers of their own, so a nameless one keeps its
    // player number on the marker. Only a machine's first seat stands for a nameless peer.
    [Fact]
    public void OnlyAMachinesFirstSeatIsANamelessPeer()
    {
        var field = NetSeats.CoopField(1, new[] { "player_bhawk", "player_fury" },
            new[] { (4, "player_fury", "Lucy"), (4, "player_warhawk", ""), (9, "player_bhawk", ""), (9, "player_fury", "") },
            hostName: "Nathan");

        Assert.Equal(new[] { "Nathan", "P2", "Lucy", "P4", "P5", "P6" }, field.Select(s => s.Callsign));
        Assert.Equal(new[] { false, false, false, false, true, false }, field.Select(s => s.Unnamed));

        var versus = NetSeats.Field(1, new[] { "player_bhawk", "player_fury" }, new[] { 4 }, "player_bhawk");
        Assert.Equal(new[] { true, false, true }, versus.Select(s => s.Unnamed));
    }

    // A guest with two players at its machine takes two seats side by side, each in its own plane.
    [Fact]
    public void ACoopFieldSeatsAGuestsSeveralPlayersSideBySide()
    {
        var field = NetSeats.CoopField(1, new[] { "player_bhawk" },
            new[] { (4, "player_fury", "Lucy"), (4, "player_warhawk", ""), (9, "player_bhawk", "Ann") });

        Assert.Equal(new[] { 1, 4, 4, 9 }, field.Select(s => s.PeerId));
        Assert.Equal(new[] { 0, 1, 2, 3 }, field.Select(s => s.SeatIndex));
        Assert.Equal(new[] { "P1", "Lucy", "P3", "Ann" }, field.Select(s => s.Callsign));
        Assert.Equal(new[] { "player_bhawk", "player_fury", "player_warhawk", "player_bhawk" }, field.Select(s => s.PlaneNode));

        // ABLE-TO-FAIL CONTROL: a guest's seats parted by another's would leave one outside the run
        // its handshake names, so the field refuses them.
        Assert.Throws<ArgumentException>(() => NetSeats.CoopField(1, new[] { "player_bhawk" },
            new[] { (4, "player_fury", ""), (9, "player_bhawk", ""), (4, "player_warhawk", "") }));
    }

    [Fact]
    public void ALocalOrdinalCountsOnlyTheSeatsFlownHere()
    {
        var guestSide = new[]
        {
            new NetSeat { SeatIndex = 0, PeerId = 1, Callsign = "P1" },
            new NetSeat { SeatIndex = 1, PeerId = 2, FlownHere = true, Callsign = "P2" },
            new NetSeat { SeatIndex = 2, PeerId = 3, Callsign = "P3" },
            new NetSeat { SeatIndex = 3, PeerId = 2, FlownHere = true, Callsign = "P4" },
        };

        Assert.Equal(-1, NetSeats.LocalOrdinal(guestSide, 0));
        Assert.Equal(0, NetSeats.LocalOrdinal(guestSide, 1));
        Assert.Equal(1, NetSeats.LocalOrdinal(guestSide, 3));
        Assert.Equal(-1, NetSeats.LocalOrdinal(guestSide, 4));

        // ABLE-TO-FAIL CONTROL: offline there is no roster and every seat is its own ordinal.
        Assert.Equal(3, NetSeats.LocalOrdinal(Array.Empty<NetSeat>(), 3));
    }

    // Flown here is one claim and a pane another. A bot is flown on its host and has no pane; a
    // person at this machine has both; a seat flown elsewhere has neither.
    [Fact]
    public void ABotSeatIsFlownOnItsHostWithNoPane()
    {
        var bot = NetSeats.Bot(hostPeer: 1, seatIndex: 3, callsign: "  Baron von Richthofen  ", "player_fury", NetBotSkill.Ace, team: 2);

        Assert.Equal(1, bot.PeerId);
        Assert.Equal(3, bot.SeatIndex);
        Assert.Equal(2, bot.TeamId);
        Assert.True(bot.IsBot);
        Assert.True(bot.FlownHere);
        Assert.False(bot.HasPane);
        Assert.Equal(NetBotSkill.Ace, bot.Skill);
        Assert.Equal("player_fury", bot.PlaneNode);
        Assert.Equal(SeatRosterMessage.Carried("Baron von Richthofen"), bot.Callsign);

        // ABLE-TO-FAIL CONTROL: a person's seat flown here has a pane, one flown elsewhere has none.
        Assert.True(new NetSeat { FlownHere = true }.HasPane);
        Assert.False(new NetSeat { FlownHere = false }.HasPane);
        Assert.Equal(NetPilot.Human, new NetSeat().Pilot);
    }

    // Several seats share the host's peer: its own player, its splitscreen seats and its bots. The
    // host's roster and a guest's copy of it both pass.
    [Fact]
    public void AHostsRosterAdmitsBotsUnderItsOwnPeer()
    {
        var host = new[]
        {
            new NetSeat { PeerId = 1, SeatIndex = 0, FlownHere = true, Callsign = "P1" },
            new NetSeat { PeerId = 1, SeatIndex = 1, FlownHere = true, Callsign = "P2" },
            new NetSeat { PeerId = 4, SeatIndex = 2, Callsign = "guest" },
            NetSeats.Bot(1, 3, "bot one", "player_fury"),
            NetSeats.Bot(1, 4, "bot two", "player_bhawk", NetBotSkill.Novice),
        };
        NetSeats.Validate(host, hostPeer: 1);

        // The guest's copy: every seat but its own reached through the host, the bots included.
        var guest = host.Select(s => s with { PeerId = s.SeatIndex == 2 ? 4 : 1, FlownHere = s.SeatIndex == 2 }).ToArray();
        NetSeats.Validate(guest);

        // ABLE-TO-FAIL CONTROL: a full field of bots behind the host still fits the ceiling only.
        var full = new List<NetSeat> { host[0] };
        full.AddRange(Enumerable.Range(1, NetSeats.MaxPlayers - 1).Select(i => NetSeats.Bot(1, i, $"bot {i}", "player_fury")));
        NetSeats.Validate(full, hostPeer: 1);
        full.Add(NetSeats.Bot(1, NetSeats.MaxPlayers, "one too many", "player_fury"));
        Assert.Throws<ArgumentException>(() => NetSeats.Validate(full, hostPeer: 1));
    }

    [Fact]
    public void ABotAtSeatZeroOrOffTheHostIsRefused()
    {
        var human = new NetSeat { PeerId = 1, SeatIndex = 0, FlownHere = true, Callsign = "P1" };
        var botFirst = new[] { NetSeats.Bot(1, 0, "bot", "player_fury"), human with { SeatIndex = 1 } };
        Assert.Throws<ArgumentException>(() => NetSeats.Validate(botFirst));

        // A bot a guest's machine owns, or one a host copy reads as flown elsewhere.
        Assert.Throws<ArgumentException>(() => NetSeats.Validate(new[] { human, NetSeats.Bot(4, 1, "bot", "player_fury") }));
        Assert.Throws<ArgumentException>(() => NetSeats.Validate(new[] { human, NetSeats.Bot(1, 1, "bot", "player_fury") with { FlownHere = false } }));

        // A guest's copy that claims to fly the host's bot.
        var guestHuman = human with { FlownHere = false };
        var guestSeat = new NetSeat { PeerId = 4, SeatIndex = 1, FlownHere = true, Callsign = "guest" };
        Assert.Throws<ArgumentException>(() => NetSeats.Validate(new[] { guestHuman, guestSeat, NetSeats.Bot(1, 2, "bot", "player_fury") }));

        // ABLE-TO-FAIL CONTROL: the same bot owned and flown by the host passes.
        NetSeats.Validate(new[] { human, NetSeats.Bot(1, 1, "bot", "player_fury") }, hostPeer: 1);
    }

    // A pane ordinal indexes this machine's players, so a bot flown here takes none.
    [Fact]
    public void ALocalOrdinalSkipsABotFlownHere()
    {
        var host = new[]
        {
            new NetSeat { PeerId = 1, SeatIndex = 0, FlownHere = true, Callsign = "P1" },
            NetSeats.Bot(1, 1, "bot", "player_fury"),
            new NetSeat { PeerId = 1, SeatIndex = 2, FlownHere = true, Callsign = "P2" },
        };

        Assert.Equal(0, NetSeats.LocalOrdinal(host, 0));
        Assert.Equal(-1, NetSeats.LocalOrdinal(host, 1));
        Assert.Equal(1, NetSeats.LocalOrdinal(host, 2));
    }

    // A bot seat carries the host's peer, so a guest's leave must never take it. On the host the
    // bot is flown here; on a guest's copy it is the host's, which never leaves on a guest's word.
    [Fact]
    public void AGuestsLeaveTakesItsOwnSeatsAndNeverABot()
    {
        var host = new[]
        {
            new NetSeat { PeerId = 1, SeatIndex = 0, FlownHere = true, Callsign = "host" },
            new NetSeat { PeerId = 4, SeatIndex = 1, Callsign = "guest" },
            NetSeats.Bot(1, 2, "bot", "player_fury"),
            new NetSeat { PeerId = 4, SeatIndex = 3, Callsign = "guest P2" },
        };

        Assert.Equal(new[] { 1, 3 }, NetSeats.LeavingWith(host, 4));
        Assert.Empty(NetSeats.LeavingWith(host, 1));
        Assert.False(NetSeats.LeavesWithPeer(host[2]));

        // On a guest's copy the bot is the host's and flown elsewhere, and still never leaves.
        var copy = host[2] with { FlownHere = false };
        Assert.False(NetSeats.LeavesWithPeer(copy));

        // ABLE-TO-FAIL CONTROL: a person flown elsewhere under the host's peer would leave with it.
        Assert.True(NetSeats.LeavesWithPeer(copy with { Pilot = NetPilot.Human }));
    }

    private static IReadOnlyList<NetSeat> Roster(int count, int locals) =>
        Enumerable.Range(0, count)
            .Select(i => new NetSeat { PeerId = i + 1, SeatIndex = i, FlownHere = i < locals, Callsign = $"P{i + 1}" })
            .ToList();
}
