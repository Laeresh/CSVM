using System;
using System.Collections.Generic;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The on-screen keyboard's rules, off-engine with the URL handler recorded. Only Game Mode on a
/// SteamOS device raises it, and a run without it does nothing. Only the owner that raised it
/// lowers it, by hiding it or by leaving its field.
/// </summary>
public sealed class ScreenKeyboardTests : IDisposable
{
    private readonly List<string> _urls = new();
    private readonly bool _available = ScreenKeyboard.Available;
    private readonly Action<string> _shell = ScreenKeyboard.Shell;

    public ScreenKeyboardTests()
    {
        ScreenKeyboard.Shell = _urls.Add;
        ScreenKeyboard.Available = true;
    }

    public void Dispose()
    {
        if (ScreenKeyboard.Shown is { } shown)
        {
            ScreenKeyboard.Hide(shown.Owner);
        }

        ScreenKeyboard.Shell = _shell;
        ScreenKeyboard.Available = _available;
    }

    [Theory]
    [InlineData("1", null, "gamescope", true)]
    [InlineData(null, "1", "gamescope", true)]
    [InlineData("1", "1", "GameScope", true)]
    [InlineData("1", null, "KDE", false)]
    [InlineData("1", null, null, false)]
    [InlineData(null, null, "gamescope", false)]
    [InlineData("0", null, "gamescope", false)]
    public void OnlyASteamOsDeviceInGameModeRaisesIt(string? deck, string? steamOs, string? desktop, bool expected)
    {
        var env = new Dictionary<string, string?>
        {
            ["SteamDeck"] = deck,
            ["SteamOS"] = steamOs,
            ["XDG_CURRENT_DESKTOP"] = desktop,
        };

        Assert.Equal(expected, ScreenKeyboard.Detect(name => env.GetValueOrDefault(name)));
    }

    [Fact]
    public void ARunWithoutItOpensNothing()
    {
        ScreenKeyboard.Available = false;

        Assert.False(ScreenKeyboard.Show(Field("menu", "name")));
        Assert.Null(ScreenKeyboard.Shown);
        Assert.Empty(_urls);
    }

    [Fact]
    public void ShowingOpensTheKeyboardForTheField()
    {
        var field = Field("menu", "name");

        Assert.True(ScreenKeyboard.Show(field));

        Assert.Same(field, ScreenKeyboard.Shown);
        Assert.Equal(new[] { ScreenKeyboard.OpenUrl }, _urls);
    }

    [Fact]
    public void ShowingAgainOpensItAgain()
    {
        ScreenKeyboard.Show(Field("menu", "name"));
        ScreenKeyboard.Show(Field("menu", "name"));

        Assert.Equal(new[] { ScreenKeyboard.OpenUrl, ScreenKeyboard.OpenUrl }, _urls);
    }

    [Fact]
    public void AnotherOwnerCannotLowerIt()
    {
        ScreenKeyboard.Show(Field("menu", "name"));

        ScreenKeyboard.Hide("chat");
        ScreenKeyboard.Follow("chat", null);

        Assert.NotNull(ScreenKeyboard.Shown);
        Assert.Equal(new[] { ScreenKeyboard.OpenUrl }, _urls);
    }

    [Fact]
    public void FollowingTheSameFieldKeepsItUp()
    {
        ScreenKeyboard.Show(Field("menu", "name"));

        ScreenKeyboard.Follow("menu", "name");

        Assert.NotNull(ScreenKeyboard.Shown);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("password")]
    public void LeavingTheFieldLowersIt(string? armed)
    {
        ScreenKeyboard.Show(Field("menu", "name"));

        ScreenKeyboard.Follow("menu", armed);

        Assert.Null(ScreenKeyboard.Shown);
        Assert.Equal(new[] { ScreenKeyboard.OpenUrl, ScreenKeyboard.CloseUrl }, _urls);
    }

    [Fact]
    public void HidingWhatIsAlreadyDownSendsNothing()
    {
        ScreenKeyboard.Hide("menu");

        Assert.Empty(_urls);
    }

    private static ScreenKeyboardField Field(string owner, string id) =>
        new(owner, id, "Name", () => "Ace");
}
