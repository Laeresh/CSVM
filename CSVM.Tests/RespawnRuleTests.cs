using CSVM.Flight.Airframe;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="RespawnRule"/>'s named rules and the wire's tightening, the whole of what decides a
/// seat's terms for flying again, plus the lifecycle reading a mode's rule. The in-game suites
/// check only that a launch hands each seat the rule these pin.
/// </summary>
public class RespawnRuleTests
{
    private const float Delay = 3f;

    [Fact]
    public void FreeFlightIsTheControlOnlyBackAtTheSpawnAndLiveInFlight()
    {
        Assert.Equal(new RespawnRule(null, false, ReturnPlacement.Spawn, true), RespawnRule.FreeFlight);
        Assert.Equal(RespawnRule.FreeFlight, new AircraftLifecycle().Rule);
    }

    [Fact]
    public void CampaignRefusesOnlyTheLiveRespawn()
    {
        Assert.Equal(RespawnRule.FreeFlight with { LiveRespawn = false }, RespawnRule.Campaign);
    }

    [Fact]
    public void ARaceBotReturnsUnattendedOnTheFixedDelay()
    {
        Assert.Equal(RespawnRule.FreeFlight with { AutoRespawnAfter = AircraftLifecycle.AutoRespawnDelay }, RespawnRule.RaceBot);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void InstantActionAllowsTheLiveRespawnOnlyInARace(bool race, bool live)
    {
        Assert.Equal(new RespawnRule(Delay, false, ReturnPlacement.Spawn, live), RespawnRule.InstantAction(Delay, race));
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    public void VersusWaitsOnFireOnlyForAHumanWithAutoRespawnOff(bool isBot, bool autoRespawn, bool waits)
    {
        Assert.Equal(new RespawnRule(Delay, waits, ReturnPlacement.Spawn, false), RespawnRule.Versus(Delay, isBot, autoRespawn));
    }

    [Fact]
    public void TheWireRefusesTheLiveRespawnButInARace()
    {
        Assert.False(RespawnRule.FreeFlight.OnTheWire(race: false).LiveRespawn);
        Assert.Equal(RespawnRule.FreeFlight, RespawnRule.FreeFlight.OnTheWire(race: true));
    }

    [Fact]
    public void TheWireNeverGrantsWhatTheModeRefused()
    {
        Assert.Equal(RespawnRule.Campaign, RespawnRule.Campaign.OnTheWire(race: true));
        var match = RespawnRule.Versus(Delay, isBot: false, autoRespawn: false);
        Assert.Equal(match, match.OnTheWire(race: true));
        Assert.Equal(match, match.OnTheWire(race: false));
    }

    [Fact]
    public void TheWireKeepsTheModesPlacementAndTimer()
    {
        var host = new ReturnPlacement.AskHost(() => { });
        var rule = RespawnRule.Versus(Delay, isBot: true, autoRespawn: true) with { Placement = host };

        var wired = rule.OnTheWire(race: false);

        Assert.Same(host, wired.Placement);
        Assert.Equal(Delay, wired.AutoRespawnAfter);
    }

    [Fact]
    public void AVersusBotSeatReturnsOnTheTimerWhereAHumanSeatWaitsForFire()
    {
        var human = new AircraftLifecycle { Rule = RespawnRule.Versus(Delay, isBot: false, autoRespawn: false) };
        var bot = new AircraftLifecycle { Rule = RespawnRule.Versus(Delay, isBot: true, autoRespawn: false) };
        human.Destroy("wreck_fall", null);
        bot.Destroy("wreck_fall", null);

        Assert.False(human.TickAutoRespawn(Delay + 0.1f, scriptedRun: false, () => false));
        Assert.True(bot.TickAutoRespawn(Delay + 0.1f, scriptedRun: false, () => false));
    }
}
