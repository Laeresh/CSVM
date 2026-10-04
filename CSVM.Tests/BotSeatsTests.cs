using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session.Roster;
using CSVM.Spec;
using Xunit;

namespace CSVM.Tests;

/// <summary>What a host makes of its bots. The tier offsets a rolled personality, and a Random
/// plane resolves to a stock airframe the roster carries to a guest. Callsigns are drawn once each
/// from the shipped pilot names, within the Callsign box's 12 characters.</summary>
[Trait("Tier", "Quick")]
public sealed class BotSeatsTests
{
    private static readonly Messages Names = Messages.Parse("""
        { "entries": [
          { "key": "MSG_PLAYER_NAME", "id": 13000, "value": "Zachary" },
          { "key": "MSG_JACK_NAME", "id": 13001, "value": "Jack" },
          { "key": "MSG_BJOHN_NAME", "id": 13004, "value": "Big John" },
          { "key": "MSG_TEX_NAME", "id": 13005, "value": "Tex" },
          { "key": "MSG_GETAWAY_NAME", "id": 13011, "value": "Getaway Plane" },
          { "key": "MSG_SSCRAWFORD_NAME", "id": 13012, "value": "Show Stopper Crawford" },
          { "key": "MSG_SIRWINTHROP_NAME", "id": 13018, "value": "Sir Charles Emmett Winthrop" },
          { "key": "MSG_BJ_HOWARD_NAME", "id": 13022, "value": "Big John Howard" }
        ] }
        """);

    [Fact]
    public void AnAceFliesItsPersonalityTwoUpAndANoviceTwoDownClampedToZeroAndNine()
    {
        for (uint draw = 0; draw < 5; draw++)
        {
            var personality = BotSeats.Personality(draw);
            int[] authored = Slots(personality);
            Assert.Equal(authored.Select(r => Math.Min(9, r + 2)), Slots(BotSeats.Ratings(personality, NetBotSkill.Ace)));
            Assert.Equal(authored.Select(r => Math.Max(0, r - 2)), Slots(BotSeats.Ratings(personality, NetBotSkill.Novice)));
            Assert.Equal(authored, Slots(BotSeats.Ratings(personality, NetBotSkill.Veteran)));
        }

        // The first authored row, worked through: its steady hand of 1 floors at 0 for a novice.
        Assert.Equal(new[] { 7, 9, 7, 5, 4, 3, 6, 6, 6 }, Slots(BotSeats.Ratings(BotSeats.Personality(0), NetBotSkill.Ace)));
        Assert.Equal(new[] { 3, 5, 3, 1, 0, 0, 2, 2, 2 }, Slots(BotSeats.Ratings(BotSeats.Personality(0), NetBotSkill.Novice)));
        var high = new AiSkillVector { DareDevil = 8, NaturalTouch = 9 };
        Assert.Equal(9, BotSeats.Ratings(high, NetBotSkill.Ace).DareDevil);
        Assert.Equal(9, BotSeats.Ratings(high, NetBotSkill.Ace).NaturalTouch);
        Assert.Null(BotSeats.Ratings(high, NetBotSkill.Ace).DeadEye);
    }

    [Fact]
    public void ATierIsDifficultysOwnNumberAndTakesNoTeamGate()
    {
        Assert.Equal(Difficulty.Normal, (int)NetBotSkill.Novice);
        Assert.Equal(Difficulty.Hard, (int)NetBotSkill.Veteran);
        Assert.Equal(Difficulty.Hardest, (int)NetBotSkill.Ace);

        // ABLE-TO-FAIL CONTROL: the gated reading spares the player's side, which a bot's offset does not.
        Assert.Equal(5, Difficulty.SkillRatingForSpawn(5, Flight.Weapons.AimAssist.PlayerTeam, false, null, Difficulty.Hardest));
        Assert.Equal(7, Difficulty.ShiftRating(5, Difficulty.Hardest));
    }

    [Fact]
    public void EachPersonalityIsOneDrawInFive()
    {
        var counts = new int[5];
        var rows = Enumerable.Range(0, 5).Select(r => string.Join(",", Slots(BotSeats.Personality((uint)r)))).ToList();
        Assert.Equal(5, rows.Distinct().Count());
        for (uint draw = 0; draw < 10_000; draw++)
        {
            counts[rows.IndexOf(string.Join(",", Slots(BotSeats.Personality(draw))))]++;
        }

        Assert.All(counts, count => Assert.Equal(2_000, count));
        Assert.Equal(Enumerable.Repeat(4, 9), Slots(BotSeats.Personality(4)));
    }

    [Fact]
    public void RandomResolvesToAStockAirframeAndANamedPlaneIsKept()
    {
        var bots = Enumerable.Repeat(new VsBotEntry(null, NetBotSkill.Veteran, 0, ""), 40)
            .Append(new VsBotEntry("player_fury", NetBotSkill.Ace, 3, "Red")).ToArray();

        var seated = BotSeats.Resolve(bots, Array.Empty<string>(), Array.Empty<string>(), new Random(5));

        Assert.All(seated.Take(40), bot => Assert.Contains(bot.Plane, StockAirframes.Nodes));
        Assert.True(seated.Take(40).Select(bot => bot.Plane).Distinct().Count() > 5, "Random draws over the field, not one plane");
        Assert.Equal(("player_fury", NetBotSkill.Ace, 3, "Red"), seated[^1]);
    }

    [Fact]
    public void TheSameSeedSeatsTheSameField()
    {
        var bots = Enumerable.Repeat(new VsBotEntry(null, NetBotSkill.Veteran, 0, ""), 6).ToArray();
        var pool = BotSeats.CallsignPool(Names);

        Assert.Equal(BotSeats.Resolve(bots, Array.Empty<string>(), pool, new Random(9)),
            BotSeats.Resolve(bots, Array.Empty<string>(), pool, new Random(9)));
    }

    [Fact]
    public void ThePoolHoldsThePeoplesNamesCutToTheirLastWordsWithinTwelve()
    {
        var pool = BotSeats.CallsignPool(Names);

        // A long name drops words from the front, so it keeps its surname. Zachary is the
        // player's own character, and Getaway Plane names an aeroplane.
        Assert.Equal(new[] { "Jack", "Big John", "Tex", "Crawford", "Winthrop", "John Howard" }, pool);
        Assert.Equal("Abcdefghijkl", BotSeats.CutName("Q Abcdefghijklmnop"));
        Assert.Equal("Tex", BotSeats.CutName("  Tex "));
        Assert.Equal("Crawford", BotSeats.CutName("Show Stopper Crawford"));
        Assert.Equal("Black Swan", BotSeats.CutName("The Black Swan"));
    }

    [Fact]
    public void CallsignsAreDrawnOnceSkipThePeoplesNamesAndFallBackWhenSpent()
    {
        var pool = BotSeats.CallsignPool(Names);
        var bots = Enumerable.Repeat(new VsBotEntry(null, NetBotSkill.Veteran, 0, ""), 6)
            .Prepend(new VsBotEntry("player_fury", NetBotSkill.Ace, 0, "Crawford"))
            .ToArray();

        var seated = BotSeats.Resolve(bots, new[] { "P1", "TEX" }, pool, new Random(3));
        var names = seated.Select(bot => bot.Callsign).ToArray();

        Assert.Equal("Crawford", names[0]);
        Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain("Tex", names, StringComparer.OrdinalIgnoreCase);
        Assert.All(names, name => Assert.InRange(name.Length, 1, BotSeats.CallsignLimit));
        // Four pool names are free once Tex and Crawford are taken, so the last two bots take
        // the "Bot <n>" fallback by place.
        Assert.Equal(new[] { "Big John", "Jack", "John Howard", "Winthrop" }, names.Skip(1).Take(4).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(new[] { "Bot 6", "Bot 7" }, names.Skip(5));
    }

    [Fact]
    public void AHostsNamedCallsignIsCutAsTheBoxCutsIt()
    {
        var seated = BotSeats.Resolve(new[] { new VsBotEntry(null, NetBotSkill.Veteran, 0, "The Red Baroness") },
            Array.Empty<string>(), Array.Empty<string>(), new Random(1));

        Assert.Equal("The Red Baro", Assert.Single(seated).Callsign);
    }

    [ExtractedDataFact]
    public void TheShippedPoolSeatsAFullFieldOfDistinctNames()
    {
        var pool = BotSeats.CallsignPool(Messages.Load(Path.Combine(TestData.ExtractedRoot!, "messages.json")));

        Assert.Equal(28, pool.Count);
        Assert.Equal(pool.Count, pool.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(pool, name => Assert.InRange(name.Length, 1, BotSeats.CallsignLimit));
        Assert.Contains("Winthrop", pool);
        Assert.DoesNotContain("Zachary", pool);
        Assert.DoesNotContain(pool, name => name.Contains("Plane", StringComparison.Ordinal));
        Assert.True(pool.Count >= NetSeats.MaxPlayers - 1, "the pool names every bot of a field with one person");
    }

    // The guest's half of Random: it parses nothing and reads the host's resolved airframe and
    // drawn callsign off the roster, with the bot's tier.
    [Fact]
    public void AGuestReadsTheHostsResolvedPlaneOffTheRoster()
    {
        var host = new List<NetSeat>
        {
            new() { PeerId = 0, SeatIndex = 0, FlownHere = true, Callsign = "host", PlaneNode = "player_fury" },
            new() { PeerId = 1, SeatIndex = 1, Callsign = "guest", PlaneNode = "player_fury" },
        };
        var spec = SessionSpec.Parse(new[] { "--vs", "--net-host=127.0.0.1:47600", "--vs-bot=random:skill=ace" });
        var bots = BotSeats.Resolve(spec.VsBots, host.Select(s => s.Callsign), BotSeats.CallsignPool(Names), new Random(17));
        NetSeats.AddBots(host, 0, bots);

        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(41));
        var hostSession = NetSession.Host(mesh[0], host.ToArray(), 7UL, null, StockAirframes.Nodes);
        var guest = NetSession.Guest(mesh[1], StockAirframes.Nodes);
        guest.Step(0.016);

        Assert.True(guest.Joined);
        var copy = guest.Seats[2];
        Assert.True(copy is { IsBot: true, FlownHere: false, Skill: NetBotSkill.Ace });
        Assert.Contains(copy.PlaneNode, StockAirframes.Nodes);
        Assert.Equal(bots[0].Plane, copy.PlaneNode);
        Assert.Equal(bots[0].Callsign, copy.Callsign);
        Assert.Equal(hostSession.Seats[2].PlaneNode, copy.PlaneNode);

        // ABLE-TO-FAIL CONTROL: the guest's own spec seats no bot, so nothing on its side drew one.
        Assert.Empty(SessionSpec.Parse(new[] { "--vs", "--net-join=127.0.0.1:47600", "--vs-bot=random" }).VsBots);
    }

    private static int[] Slots(AiSkillVector v) => new[]
    {
        v.DareDevil!.Value, v.NaturalTouch!.Value, v.SixthSense!.Value, v.DeadEye!.Value, v.QuickDraw!.Value,
        v.SteadyHand!.Value, v.StunRecovery!.Value, v.Talker!.Value, v.Constitution!.Value,
    };
}
