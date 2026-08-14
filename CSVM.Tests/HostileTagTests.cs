using CSVM.Flight;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The H22 hostile-marker tag (<see cref="VersusHud.HostileTag"/>), off-engine: the AI spawner's
/// "ai1_player_fury" naming reads back as "AI1". The selection itself
/// (<see cref="VersusHud.NearestHostile"/>) filters on live <see cref="FlightController"/>
/// sources, which are engine nodes, so its pins live in the <c>hostile-marker-hud</c> in-engine
/// suite instead.
/// </summary>
public class HostileTagTests
{
    [Theory]
    [InlineData("ai1_player_fury", "AI1")]
    [InlineData("ai12_player_bhawk", "AI12")]
    [InlineData("bandit", "BANDIT")]
    public void TagIsTheFirstNameSegmentUppercased(string name, string expected)
    {
        Assert.Equal(expected, VersusHud.HostileTag(name));
    }

    [Fact]
    public void AnEmptyNameFallsBackToAi()
    {
        Assert.Equal("AI", VersusHud.HostileTag(""));
    }

    [Fact]
    public void ALeadingUnderscoreKeepsTheWholeName()
    {
        // IndexOf 0 is not a cut: "_x" has no head segment to take.
        Assert.Equal("_X", VersusHud.HostileTag("_x"));
    }
}
