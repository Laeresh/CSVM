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
/// The Multiplayer Games list on the engine-free shell over the layout fixture. An empty list says
/// Searching or why no game can be heard, with every plaque live and the cursor on Create Game.
/// Auto refresh starts checked, and a filled list asks again every five seconds only while it is.
/// Join by Code stands the Join code box, whose OK joins the code's written form through the code
/// opener after Player Information. With no master server or no WebRTC carrier the
/// plaque says why. The door's own reading of a typed code is checked beside it.
/// </summary>
[Trait("Tier", "Quick")]
public class OriginalGamesListTests
{
    private static readonly LoopbackConditions Clean = new(0.0, 0.0, 0.0);

    [Fact]
    public void AnEmptyListSaysSearchingWithNoBoxAndLeavesEveryPlaqueLive()
    {
        var shell = Page(LanDoor(new SampleSocket { Answering = false }));
        shell.StepNet(0.0);

        Assert.Empty(shell.Connection.Listed);
        Assert.Null(shell.Dialog);
        Assert.Equal("Searching ...", shell.Connection.Status);
        Assert.Contains(shell.Compose().Lines, line => line.Text == "Searching ...");
        Assert.Equal(OriginalConnectionScreen.CreateKey, shell.FocusedKey);
        foreach (string key in new[]
                 {
                     OriginalConnectionScreen.CreateKey, OriginalConnectionScreen.CodeKey, OriginalConnectionScreen.RefreshKey,
                     OriginalConnectionScreen.ExitKey, OriginalConnectionScreen.SortKey(0),
                 })
        {
            Assert.True(Row(shell, key).Enabled, key);
        }

        shell.Step(new MenuCommands { Accept = true });
        Assert.Equal(NetInfoPage.Game, shell.NetInfo.Page);
    }

    [Fact]
    public void WithNoSearchToOpenTheEmptyListSaysSoAndCreateGameStaysLive()
    {
        var end = LoopbackTransport.Mesh(1, Clean, new Random(4))[0];
        var shell = Page(new NetPlayFeature((_, _, _) => end, (_, _) => end));
        shell.StepNet(0.0);

        Assert.Equal("No local area network connection is detected.", shell.Connection.Status);
        Assert.Contains(shell.Compose().Lines, line => line.Text == shell.Connection.Status);
        Assert.Null(shell.Dialog);
        Assert.True(Row(shell, OriginalConnectionScreen.CreateKey).Enabled);
    }

    [Fact]
    public void AMasterServerThatDoesNotAnswerIsNamedOnTheEmptyList()
    {
        var end = LoopbackTransport.Mesh(1, Clean, new Random(5))[0];
        var door = new NetPlayFeature((_, _, _) => end, (_, _) => end)
        {
            Internet = { Master = new MasterDirectory(_ => Task.FromException<string>(new InvalidOperationException("refused"))) },
        };
        var shell = Page(door);
        shell.StepNet(0.0);

        Assert.Equal("The master server did not answer: refused.", shell.Connection.Status);
        Assert.Null(shell.Dialog);
        Assert.True(Row(shell, OriginalConnectionScreen.CreateKey).Enabled);
    }

    [Fact]
    public void AutoRefreshStartsCheckedAndAFilledListAsksAgainEveryFiveSecondsOnlyWhileChecked()
    {
        var lan = new SampleSocket();
        var shell = Page(LanDoor(lan));
        Assert.True(shell.Connection.AutoRefresh);
        shell.StepNet(0.0);
        Assert.Single(shell.Connection.Listed);
        Assert.Equal(OriginalConnectionScreen.GameKey(0), shell.FocusedKey);

        // The picked game survives each round, picked by its address.
        shell.Step(new MenuCommands { Accept = true });
        Assert.True(Row(shell, OriginalConnectionScreen.JoinKey).Enabled);
        int asked = lan.Queries;
        Run(shell, 4.9);
        Assert.Equal(asked, lan.Queries);
        Run(shell, 0.2);
        Assert.Equal(asked + 1, lan.Queries);
        Assert.True(Row(shell, OriginalConnectionScreen.JoinKey).Enabled);

        Press(shell, OriginalConnectionScreen.RefreshKey);
        Assert.False(shell.Connection.AutoRefresh);
        asked = lan.Queries;
        Run(shell, 12.0);
        Assert.Equal(asked, lan.Queries);
        Assert.Single(shell.Connection.Listed);
    }

    [Fact]
    public void TheFirstAnswersLeaveACursorThePlayerMoved()
    {
        var lan = new SampleSocket { Answering = false };
        var shell = Page(LanDoor(lan));
        shell.StepNet(0.0);
        ((IOriginalScreenHost)shell).FocusKey(OriginalConnectionScreen.CodeKey);
        lan.Answering = true;
        Run(shell, 1.5);

        Assert.Single(shell.Connection.Listed);
        Assert.Equal(OriginalConnectionScreen.CodeKey, shell.FocusedKey);
    }

    [Fact]
    public void JoinByCodeStandsTheCodeBoxWithTheCursorInIt()
    {
        var shell = Page(Door(new List<string>()));
        Press(shell, OriginalConnectionScreen.CodeKey);

        Assert.True(shell.Connection.CodeBoxOpen);
        Assert.Equal(
            new[] { OriginalConnectionScreen.CodeBoxKey, OriginalConnectionScreen.CodeOkKey, OriginalConnectionScreen.CodeCancelKey },
            shell.Rows.Select(row => row.Key));
        Assert.Equal(OriginalConnectionScreen.CodeBoxKey, shell.FocusedKey);
        Assert.True(shell.CapturingText);
        Assert.Contains(shell.Compose().Overlays.SelectMany(panel => panel.Lines), line => line.Text == OriginalConnectionScreen.CodeBoxHint);
    }

    [Fact]
    public void ACodeTypedWithoutItsDashInAnyCaseJoinsByItsWrittenForm()
    {
        var opened = new List<string>();
        var door = Door(opened);
        var shell = Page(door);
        Press(shell, OriginalConnectionScreen.CodeKey);

        shell.Step(new MenuCommands { Typed = "k7qx3m" });
        Assert.Equal("K7QX3M", shell.Connection.TypedCode);

        // Enter in the box is its OK, which answers Player Information first.
        shell.Step(new MenuCommands { Accept = true });
        Assert.False(shell.Connection.CodeBoxOpen);
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
    public void ABoxHoldingNoCodeWarnsOverTheBoxAndAsksNothing()
    {
        var opened = new List<string>();
        var shell = Page(Door(opened));
        Press(shell, OriginalConnectionScreen.CodeKey);

        shell.Step(new MenuCommands { Typed = "K7Q-X3" });
        shell.Step(new MenuCommands { Accept = true });

        Assert.Equal(OriginalConnectionScreen.CodeNotRecognized, shell.Dialog?.Message);
        Assert.True(shell.Connection.CodeBoxOpen);
        Assert.False(shell.NetInfo.IsOpen);
        Assert.Empty(opened);
    }

    [Fact]
    public void BackAndCancelTakeTheBoxDownOntoItsPlaque()
    {
        var shell = Page(Door(new List<string>()));
        Press(shell, OriginalConnectionScreen.CodeKey);
        shell.Step(new MenuCommands { Back = true });
        Assert.False(shell.Connection.CodeBoxOpen);
        Assert.Equal(OriginalScreen.Connection, shell.Screen);
        Assert.Equal(OriginalConnectionScreen.CodeKey, shell.FocusedKey);

        Press(shell, OriginalConnectionScreen.CodeKey);
        Press(shell, OriginalConnectionScreen.CodeCancelKey);
        Assert.False(shell.Connection.CodeBoxOpen);
        Assert.Equal(OriginalConnectionScreen.CodeKey, shell.FocusedKey);

        // ABLE-TO-FAIL CONTROL: Back on the list itself goes to the main menu and stops the search.
        shell.Step(new MenuCommands { Back = true });
        Assert.Equal(OriginalScreen.TopLevel, shell.Screen);
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
    public void WithNoMasterServerJoinByCodeStaysLiveAndSaysWhy()
    {
        var end = LoopbackTransport.Mesh(1, Clean, new Random(1))[0];
        var door = new NetPlayFeature((_, _, _) => end, (_, _) => end);
        var shell = Page(door);

        Assert.Equal(CoopDoorText.NoMasterServer, shell.Connection.CodeFault);
        Assert.True(Row(shell, OriginalConnectionScreen.CodeKey).Enabled);
        Press(shell, OriginalConnectionScreen.CodeKey);
        Assert.Equal(CoopDoorText.CodeJoinUnavailable(CoopDoorText.NoMasterServer), shell.Dialog?.Message);
        Assert.False(shell.Connection.CodeBoxOpen);
        Assert.False(door.JoinByCode("K7Q-X3M"));
    }

    [Fact]
    public void WithAMasterServerButNoWebRtcJoinByCodeSaysSo()
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
        Press(shell, OriginalConnectionScreen.CodeKey);
        Assert.Equal("WebRTC is missing or failed to start, so joining by code is unavailable.", shell.Dialog?.Message);
        Assert.False(shell.Connection.CodeBoxOpen);
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

    private static NetPlayFeature LanDoor(SampleSocket lan) =>
        new((_, _, _) => throw new InvalidOperationException("no host"), (_, _) => throw new InvalidOperationException("no join"), lan: (_, _) => lan)
        {
            Version = NetDoorAid.SampleVersion,
        };

    private static OriginalShell Page(NetPlayFeature door)
    {
        var shell = new OriginalShell(MenuLayoutReaderTests.OriginalLayout(), new FreeFlightFeature(), new PlayerSetupFeature(), _ => null);
        shell.StandInNetDoor(door);
        shell.Connection.OpenConnection();
        return shell;
    }

    private static void Press(OriginalShell shell, string key)
    {
        ((IOriginalScreenHost)shell).FocusKey(key);
        shell.Step(new MenuCommands { Accept = true });
    }

    private static void Run(OriginalShell shell, double seconds)
    {
        for (double t = 0.0; t < seconds - 1e-9; t += 0.1)
        {
            shell.StepNet(0.1);
        }
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

    // A LAN that answers each query with the aids' first sample game while it answers, counting the
    // queries it was sent.
    private sealed class SampleSocket : ILanSocket
    {
        private readonly Queue<(byte[] Bytes, string Address)> _inbox = new();

        public bool Answering { get; set; } = true;

        public int Queries { get; private set; }

        public void Send(string address, int port, ReadOnlySpan<byte> datagram)
        {
            if (!LanDiscovery.TryReadQuery(datagram, out uint token))
            {
                return;
            }

            Queries++;
            if (Answering)
            {
                var game = NetDoorAid.SampleGames[0];
                byte[] reply = new byte[LanDiscovery.Size];
                LanDiscovery.WriteReply(reply, token, game.Port, game.Advert, game.Version);
                _inbox.Enqueue((reply, game.Address));
            }
        }

        public byte[]? Receive(out string address, out int port)
        {
            port = LanDiscovery.Port;
            if (_inbox.Count == 0)
            {
                address = string.Empty;
                return null;
            }

            var (bytes, from) = _inbox.Dequeue();
            address = from;
            return bytes;
        }

        public void Dispose() => _inbox.Clear();
    }
}
