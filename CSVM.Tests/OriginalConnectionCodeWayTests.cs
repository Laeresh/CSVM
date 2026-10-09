using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CSVM.Net;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The Connection page's Join by code way on the engine-free shell over the layout fixture. It
/// stands third, under Internet, with its own box. The box takes a code in either case, with or
/// without its dash. Connect joins the code's written form through the code opener after Player
/// Information. With no master server or no WebRTC carrier the way stands greyed and says why. The
/// door's own reading of a typed code is checked beside it.
/// </summary>
[Trait("Tier", "Quick")]
public class OriginalConnectionCodeWayTests
{
    private static readonly LoopbackConditions Clean = new(0.0, 0.0, 0.0);

    [Fact]
    public void TheCodeWayStandsThirdAndTheCursorWalksIntoItsBox()
    {
        var shell = Page(Door(new List<string>()));

        var keys = shell.Rows.Select(row => row.Key).ToList();
        Assert.Equal(
            new[]
            {
                OriginalConnectionScreen.LanKey, OriginalConnectionScreen.InternetKey, OriginalConnectionScreen.AddressKey,
                OriginalConnectionScreen.CodeKey, OriginalConnectionScreen.CodeBoxKey,
            },
            keys.Take(5));
        Assert.True(Row(shell, OriginalConnectionScreen.CodeKey).Enabled);
        Assert.True(Row(shell, OriginalConnectionScreen.CodeBoxKey).Enabled);
        Assert.Equal(string.Empty, shell.Connection.CodeFault);

        var walked = new List<string> { shell.FocusedKey };
        for (int step = 0; step < 4; step++)
        {
            shell.Step(new MenuCommands { MoveY = 1 });
            walked.Add(shell.FocusedKey);
        }

        Assert.Equal(keys.Take(5), walked);
        Assert.True(shell.CapturingText);
    }

    [Fact]
    public void ACodeTypedWithoutItsDashInAnyCaseJoinsByItsWrittenForm()
    {
        var opened = new List<string>();
        var door = Door(opened);
        var shell = Page(door);
        FocusCodeBox(shell);

        shell.Step(new MenuCommands { Typed = "k7qx3m" });
        Assert.Equal("K7QX3M", shell.Connection.TypedCode);
        Assert.Equal(OriginalConnectionScreen.CodeKey, shell.Connection.Way);

        // Enter in the box is its Connect, which answers Player Information first.
        shell.Step(new MenuCommands { Accept = true });
        Assert.Equal(NetInfoPage.Player, shell.NetInfo.Page);
        Assert.True(Row(shell, OriginalNetInfoBox.PlayerPasswordKey).Enabled);
        Assert.Empty(opened);
        shell.NetInfo.Draft.Callsign = "Nathan";
        shell.NetInfo.Activate(OriginalNetInfoBox.OkKey);

        Assert.Equal(new[] { "K7Q-X3M" }, opened);
        Assert.Equal(NetDoorStage.Joining, door.Stage);
        Assert.Equal("K7Q-X3M", door.JoinName);
        Assert.Equal("K7Q-X3M", door.LinkedTo);
        Assert.Equal(NetPlayFeature.DefaultAddress, door.Address);
    }

    [Fact]
    public void ABoxHoldingNoCodeRaisesTheBoxAndAsksNothing()
    {
        var opened = new List<string>();
        var shell = Page(Door(opened));
        FocusCodeBox(shell);

        shell.Step(new MenuCommands { Typed = "K7Q-X3" });
        shell.Step(new MenuCommands { Accept = true });

        Assert.Equal(OriginalConnectionScreen.CodeNotRecognized, shell.Dialog?.Message);
        Assert.False(shell.NetInfo.IsOpen);
        Assert.Empty(opened);
    }

    [Fact]
    public void TheCodeBoxTakesOnlyTheCodeAlphabetAndTheDashUpToItsLimit()
    {
        var shell = Page(Door(new List<string>()));
        var screen = shell.Connection;

        Assert.Equal(6, screen.TypeCode("k7q x3m!"));
        Assert.Equal("K7QX3M", screen.TypedCode);
        Assert.Equal(1, screen.TypeCode("-AB"));
        Assert.Equal("K7QX3M-", screen.TypedCode);
        Assert.Equal(OriginalConnectionScreen.CodeBoxLimit, screen.TypedCode.Length);

        // ABLE-TO-FAIL CONTROL: the four characters a code never uses are refused as typed.
        screen.ClearCode();
        Assert.Equal(0, screen.TypeCode("01IO"));
        Assert.Equal(string.Empty, screen.TypedCode);
    }

    [Theory]
    [InlineData("K7Q-X3M")]
    [InlineData("k7q-x3m")]
    [InlineData("K7QX3M")]
    [InlineData("  k7qx3m \r\n")]
    public void TheDoorJoinsATypedCodeByItsWrittenForm(string typed)
    {
        var opened = new List<string>();
        var door = Door(opened);

        Assert.True(door.JoinByCode(typed));
        Assert.Equal(new[] { "K7Q-X3M" }, opened);
        Assert.Equal(NetDoorStage.Joining, door.Stage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("K7Q-X3")]
    [InlineData("K7Q-X3MM")]
    [InlineData("K0Q-X3M")]
    [InlineData("K7QX-3M")]
    [InlineData("127.0.0.1")]
    public void TheDoorOpensNothingForTextThatIsNotACode(string typed)
    {
        var opened = new List<string>();
        var door = Door(opened);

        Assert.False(door.JoinByCode(typed));
        Assert.Empty(opened);
        Assert.Equal(NetDoorStage.Shut, door.Stage);
    }

    [Fact]
    public void WithNoMasterServerTheCodeWayIsGreyedAndSaysWhy()
    {
        var end = LoopbackTransport.Mesh(1, Clean, new Random(1))[0];
        var door = new NetPlayFeature((_, _, _) => end, (_, _) => end);
        var shell = Page(door);

        Assert.Equal(CoopDoorText.NoMasterServer, shell.Connection.CodeFault);
        Assert.False(Row(shell, OriginalConnectionScreen.CodeKey).Enabled);
        Assert.False(Row(shell, OriginalConnectionScreen.CodeBoxKey).Enabled);
        Assert.Contains(shell.Compose().Lines, line => line.Text == CoopDoorText.CodeJoinUnavailable(CoopDoorText.NoMasterServer));
        Assert.DoesNotContain(shell.Compose().Lines, line => line.Text == OriginalConnectionScreen.CodeWayDescription);
        Assert.False(door.JoinByCode("K7Q-X3M"));

        // The cursor passes over the greyed way: from the IP Address box it wraps to LAN TCP/IP.
        shell.Step(new MenuCommands { MoveY = 1 });
        shell.Step(new MenuCommands { MoveY = 1 });
        Assert.Equal(OriginalConnectionScreen.AddressKey, shell.FocusedKey);
        shell.Step(new MenuCommands { MoveY = 1 });
        Assert.Equal(OriginalConnectionScreen.LanKey, shell.FocusedKey);
    }

    [Fact]
    public void WithAMasterServerButNoWebRtcTheCodeWaySaysSo()
    {
        var end = LoopbackTransport.Mesh(1, Clean, new Random(2))[0];
        var door = new NetPlayFeature((_, _, _) => end, (_, _) => end)
        {
            Internet =
            {
                Master = new MasterDirectory(_ => Task.FromResult("{\"games\":[]}")),
                OpenCode = _ => end,
                WebRtcReady = false,
            },
        };
        var shell = Page(door);

        Assert.Equal(CoopDoorText.NoWebRtc, shell.Connection.CodeFault);
        Assert.False(Row(shell, OriginalConnectionScreen.CodeKey).Enabled);
        Assert.Equal(
            "WebRTC is missing or failed to start, so joining by code is unavailable.",
            CoopDoorText.CodeJoinUnavailable(shell.Connection.CodeFault));
    }

    // A guest door whose code opener records the code it was asked for and hands back a loopback end.
    private static NetPlayFeature Door(List<string> opened)
    {
        var mesh = LoopbackTransport.Mesh(2, Clean, new Random(3));
        return new NetPlayFeature((_, _, _) => mesh[0], (_, _) => throw new InvalidOperationException("no direct join"))
        {
            Internet =
            {
                Master = new MasterDirectory(_ => Task.FromResult("{\"games\":[]}")),
                OpenCode = code =>
                {
                    opened.Add(code);
                    return mesh[1];
                },
            },
        };
    }

    private static OriginalShell Page(NetPlayFeature door)
    {
        var shell = new OriginalShell(MenuLayoutReaderTests.OriginalLayout(), new FreeFlightFeature(), new PlayerSetupFeature(), _ => null);
        shell.StandInNetDoor(door);
        shell.Connection.OpenConnection();
        return shell;
    }

    private static void FocusCodeBox(OriginalShell shell)
    {
        for (int step = 0; step < 8 && shell.FocusedKey != OriginalConnectionScreen.CodeBoxKey; step++)
        {
            shell.Step(new MenuCommands { MoveY = 1 });
        }

        Assert.Equal(OriginalConnectionScreen.CodeBoxKey, shell.FocusedKey);
    }

    private static OriginalRow Row(OriginalShell shell, string key)
    {
        var rows = shell.NetInfo.IsOpen ? NetInfoRows(shell) : shell.Rows.ToList();
        return rows.Find(row => row.Key == key)!;
    }

    private static List<OriginalRow> NetInfoRows(OriginalShell shell)
    {
        var rows = new List<OriginalRow>();
        shell.NetInfo.Rows(rows);
        return rows;
    }
}
