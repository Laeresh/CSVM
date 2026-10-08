using System.Collections.Generic;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Game Mode read off the environment Steam launches a run with: a SteamOS device under the
/// gamescope session, and nothing else.
/// </summary>
public sealed class SteamOsTests
{
    [Theory]
    [InlineData("1", null, "gamescope", true)]
    [InlineData(null, "1", "gamescope", true)]
    [InlineData("1", "1", "GameScope", true)]
    [InlineData("1", null, "KDE", false)]
    [InlineData("1", null, null, false)]
    [InlineData(null, null, "gamescope", false)]
    [InlineData("0", null, "gamescope", false)]
    public void OnlyASteamOsDeviceUnderGamescopeIsInGameMode(string? deck, string? steamOs, string? desktop, bool expected)
    {
        var env = new Dictionary<string, string?>
        {
            ["SteamDeck"] = deck,
            ["SteamOS"] = steamOs,
            ["XDG_CURRENT_DESKTOP"] = desktop,
        };

        Assert.Equal(expected, SteamOs.Detect(name => env.GetValueOrDefault(name)));
    }
}
