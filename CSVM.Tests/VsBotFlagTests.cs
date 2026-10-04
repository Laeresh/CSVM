using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Net;
using CSVM.Spec;
using Xunit;

namespace CSVM.Tests;

/// <summary>The command line's bot seats. Both bot flags reach the spec with their refusals and
/// scope. <see cref="NetSeats.AddBots"/> seats the bots on a host's roster after its guests.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class VsBotFlagTests
{
    private const string Host = "--net-host=127.0.0.1:47600";

    [Fact]
    public void NamedEntriesComeFirstThenTheCountsDefaults()
    {
        var s = SessionSpec.Parse(new[]
        {
            "--vs", Host, "--vs-bots=2", "--vs-bot=player_fury:skill=ace:team=2:name=Red,:skill=novice",
        });

        // A null plane is Random and an empty callsign a draw, both the host's to make at launch.
        Assert.Equal(
            new[]
            {
                new VsBotEntry("player_fury", NetBotSkill.Ace, 2, "Red"),
                new VsBotEntry(null, NetBotSkill.Novice, 0, ""),
                new VsBotEntry(null, NetBotSkill.Veteran, 0, ""),
                new VsBotEntry(null, NetBotSkill.Veteran, 0, ""),
            },
            s.VsBots);
        Assert.DoesNotContain(s.Warnings, w => w.Message.Contains("--vs-bot", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("random")]
    [InlineData("RANDOM")]
    [InlineData("")]
    public void RandomOrNoPlaneAsksForRandom(string plane)
    {
        var s = SessionSpec.Parse(new[] { "--vs", Host, $"--vs-bot={plane}:skill=ace" });
        Assert.Equal(new VsBotEntry(null, NetBotSkill.Ace, 0, ""), Assert.Single(s.VsBots));
        Assert.DoesNotContain(s.Warnings, w => w.Message.Contains("--vs-bot", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("veteran", NetBotSkill.Veteran)]
    [InlineData("hardest", NetBotSkill.Ace)]
    [InlineData("0", NetBotSkill.Novice)]
    public void SkillTakesTheDifficultyVocabulary(string word, NetBotSkill want)
    {
        var s = SessionSpec.Parse(new[] { "--vs", Host, $"--vs-bot=:skill={word}" });
        Assert.Equal(want, Assert.Single(s.VsBots).Skill);
    }

    [Theory]
    [InlineData(":skill=godlike")]
    [InlineData(":team=99")]
    [InlineData(":team=red")]
    [InlineData("player_zeppelin")]
    [InlineData(":colour=blue")]
    public void AnUnreadablePartKeepsItsDefaultAndIsNamed(string entry)
    {
        var s = SessionSpec.Parse(new[] { "--vs", Host, $"--vs-bot={entry}" });
        Assert.Equal(new VsBotEntry(null, NetBotSkill.Veteran, 0, ""), Assert.Single(s.VsBots));
        Assert.Contains(s.Warnings, w => w.Category == "core" && w.Message.StartsWith("--vs-bot:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("three")]
    [InlineData("-1")]
    public void AnUnreadableCountAddsNone(string value)
    {
        var s = SessionSpec.Parse(new[] { "--vs", Host, $"--vs-bots={value}" });
        Assert.Empty(s.VsBots);
        Assert.Contains(s.Warnings, w => w.Message.StartsWith("--vs-bots:", StringComparison.Ordinal));
    }

    [Fact]
    public void TheLastBotListWins()
    {
        var s = SessionSpec.Parse(new[] { "--vs", Host, "--vs-bot=player_fury,player_kestrel", "--vs-bot=player_warhawk" });
        Assert.Equal("player_warhawk", Assert.Single(s.VsBots).Plane);
    }

    [Theory]
    [InlineData("--net-join=127.0.0.1:47600", "a guest flies the host's roster")]
    [InlineData(null, "a local match seats no bots")]
    public void OnlyANetworkHostSeatsBots(string? net, string why)
    {
        var args = new List<string> { "--vs", "--vs-bots=3" };
        if (net != null)
        {
            args.Add(net);
        }

        var s = SessionSpec.Parse(args);
        Assert.Empty(s.VsBots);
        Assert.Contains(s.Warnings, w => w.Category == "core" && w.Message.Contains(why, StringComparison.Ordinal));

        // ABLE-TO-FAIL CONTROL: the same flag on a host seats them.
        Assert.Equal(3, SessionSpec.Parse(new[] { "--vs", "--vs-bots=3", Host }).VsBots.Count);
    }

    [Fact]
    public void BotsNeedTheDogfight()
    {
        var s = SessionSpec.Parse(new[] { "--fly", Host, "--vs-bots=2" });
        Assert.Empty(s.VsBots);
        Assert.Contains(s.Warnings, w => w.Message.Contains("without --vs", StringComparison.Ordinal));
    }

    [Fact]
    public void TheFieldIsCutToTheSeatsThisMachineLeaves()
    {
        var s = SessionSpec.Parse(new[] { "--vs", Host, "--players=4", "--vs-bots=20" });
        Assert.Equal(NetSeats.MaxPlayers - 4, s.VsBots.Count);
        Assert.Contains(s.Warnings, w => w.Message.Contains("seating the first 12", StringComparison.Ordinal));
    }

    [Fact]
    public void BotsSitAfterTheGuestsOnTheHostsPeer()
    {
        var roster = new List<NetSeat>
        {
            new() { PeerId = 0, SeatIndex = 0, FlownHere = true, Callsign = "host" },
            new() { PeerId = 1, SeatIndex = 1, Callsign = "guest 1" },
        };
        int left = NetSeats.AddBots(roster, 0, new[]
        {
            ("player_fury", NetBotSkill.Ace, 2, "Red"),
            ("player_bhawk", NetBotSkill.Veteran, 0, "Bot 2"),
        });

        Assert.Equal(0, left);
        Assert.Equal(new[] { false, false, true, true }, roster.Select(s => s.IsBot));
        var red = roster[2];
        Assert.True(red.SeatIndex == 2 && red.PeerId == 0 && red.FlownHere && !red.HasPane);
        Assert.Equal(("player_fury", NetBotSkill.Ace, 2, "Red"), (red.PlaneNode, red.Skill, red.TeamId, red.Callsign));
        NetSeats.Validate(roster, hostPeer: 0);
    }

    [Fact]
    public void AFullFieldLeavesTheBotsOut()
    {
        var roster = new List<NetSeat> { new() { PeerId = 0, SeatIndex = 0, FlownHere = true, Callsign = "host" } };
        for (int peer = 1; roster.Count < NetSeats.MaxPlayers - 1; peer++)
        {
            roster.Add(new NetSeat { PeerId = peer, SeatIndex = roster.Count, Callsign = $"guest {peer}" });
        }

        var bots = Enumerable.Range(1, 3).Select(i => ("player_bhawk", NetBotSkill.Veteran, 0, SessionSpec.BotCallsign(i)));
        Assert.Equal(2, NetSeats.AddBots(roster, 0, bots));
        Assert.Equal(NetSeats.MaxPlayers, roster.Count);
        Assert.Equal("Bot 1", roster[^1].Callsign);
        NetSeats.Validate(roster, hostPeer: 0);
    }
}
