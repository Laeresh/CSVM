using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Net;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.UI.Menu;
using CSVM.UI.Menu.BuiltIn;
using CSVM.UI.Menu.Original;

namespace CSVM.Testing;

/// <summary>
/// The Multiplayer Lobby's Game Scores page past its ten rows, on one Original host alone over the
/// loopback. The lobby lands a sixteen-line race and a sixteen-line Dogfight in turn. Its arrows, the
/// wheel and the pad drive the page's scroll bar.
/// </summary>
internal static class MenuOriginalScoresSuites
{
    private const float Dt = 1f / 60f;
    private const string Loopback = "127.0.0.1";

    [Suite("menu-original-lobby-scores",
        "The lobby's Game Scores past ten lines: a landed sixteen-pilot race draws ten rows under the "
        + "script's scroll bar at (+419, +66), whose down arrow, the wheel and the pad's walk and Accept "
        + "reach the sixteenth, and a landed sixteen-line Dogfight does the same, where a ten-line one "
        + "draws all ten with no bar. The chat pane's KG at (757, 368) scrolls thirty-odd lines by the "
        + "wheel, the pad and the thumb, following new lines at the bottom and holding once scrolled up, "
        + "where a three-line chat draws no bar")]
    internal static void TheLobbyScores(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        var lan = new LoopbackLan();
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(145));
        var door = new NetPlayFeature(
            (_, _, _) => mesh[0],
            (_, _) => throw new InvalidOperationException("the host does not join"),
            new RouterAccess(
                port => new UpnpPortMapResult(UpnpPortMapOutcome.Mapped, port, NetDoorAid.ExternalAddress, "suite"),
                _ => { }),
            lan.Bind)
        {
            BindAddress = Loopback,
            Lan = { SearchAddress = Loopback },
        };
        string? options = MenuSuiteHost.ScratchOptions(ctx, "menu-original-lobby-scores");
        MenuHost? host = null;
        try
        {
            var seat = new ScriptedSeat();
            host = Open(ctx, layout, door, seat);
            if (host?.Active is not OriginalPresentation { Shell: { } shell } || !HostTheLobby(ctx, host, seat, shell))
            {
                return;
            }

            ctx.Check(door.Dogfight != null, $"the hosted lobby holds a Dogfight lobby for its chat");
            if (door.Dogfight is { } lobby)
            {
                ScrollTheChat(ctx, host, seat, shell, lobby);
            }

            var race = Enumerable.Range(1, 16).Select(i => new RaceTableRow($"R{i}", "Fury", "0:10.0", "", "1/1")).ToList();
            ctx.Check(shell.Lobby.Land(Array.Empty<DogfightScore>(), race) && shell.Lobby.Tab == LobbyTab.Scores,
                $"a sixteen-pilot race lands the lobby on Game Scores ({shell.Lobby.Tab})");
            ScrollToTheSixteenth(ctx, host, seat, shell, "R");

            var match = Enumerable.Range(1, 16).Select(i => new DogfightScore($"D{i}", 20 - i, 1, 0)).ToList();
            ctx.Check(shell.Lobby.Land(match), $"a sixteen-line Dogfight lands the lobby on Game Scores");
            ScrollToTheSixteenth(ctx, host, seat, shell, "D");

            // ABLE-TO-FAIL CONTROL: ten lines fill the page, so the script keeps its control deactivated.
            ctx.Check(shell.Lobby.Land(match.Take(10).ToList()), $"a ten-line Dogfight lands");
            var board = shell.Compose();
            ctx.Check(Row(shell, OriginalLobbyScreen.ScoresUpKey) == null && Row(shell, OriginalLobbyScreen.ScoresDownKey) == null
                      && !board.Pictures.Any(p => p.Art.Name == "MP_B_SCROLLBAR.PNG" && p.X == OriginalRaceTable.ScrollBar(314f, 26f).X)
                      && Draws(board, "D1") && Draws(board, "D10"),
                $"ten lines draw all ten with no scroll bar");
        }
        finally
        {
            host?.Deactivate();
            door.Discard();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            CSVM.Utils.OptionsStore.DirectoryOverride = options;
        }
    }

    // A freshly landed page shows lines 1 to 10 under a bar whose up arrow is dead. Down presses
    // reach 16 and stop, the wheel goes back to the top, and the pad's walk and Accept go down again.
    private static void ScrollToTheSixteenth(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, string prefix)
    {
        var board = shell.Compose();
        var bar = OriginalRaceTable.ScrollBar(314f, 26f);
        var down = Row(shell, OriginalLobbyScreen.ScoresDownKey);
        ctx.Check(Row(shell, OriginalLobbyScreen.ScoresUpKey) is { Enabled: false } && down is { Enabled: true }
                  && down.X == bar.X && down.Y == bar.DownY,
            $"{prefix}: the scroll bar's arrows stand at the script's corner, the up one dead ({down?.X}, {down?.Y})");
        ctx.Check(Draws(board, $"{prefix}10") && !Draws(board, $"{prefix}11")
                  && board.Pictures.Any(p => p.Art.Name == "MP_B_SCROLLBAR.PNG" && p.X == bar.X),
            $"{prefix}: the page draws its first ten lines and the thumb");

        for (int i = 0; i < 8; i++)
        {
            ClickRow(ctx, host, seat, shell, OriginalLobbyScreen.ScoresDownKey);
        }

        board = shell.Compose();
        ctx.Check(Lines(board).Contains($"{prefix}16") && !Lines(board).Contains($"{prefix}6")
                  && Row(shell, OriginalLobbyScreen.ScoresDownKey) is { Enabled: false } && Row(shell, OriginalLobbyScreen.ScoresUpKey) is { Enabled: true },
            $"{prefix}: the down arrow reaches the sixteenth line and stops there ({string.Join(",", Lines(board).Where(l => l.StartsWith(prefix, StringComparison.Ordinal)))})");

        Press(host, seat, new MenuCommands { Pointer = Window(ctx, bar.X - 200f, bar.Y + 50f, -10) });
        board = shell.Compose();
        ctx.Check(Lines(board).Contains($"{prefix}1") && !Draws(board, $"{prefix}16"), $"{prefix}: the wheel over the page scrolls back to the top");

        for (int i = 0; i < 80 && shell.FocusedKey != OriginalLobbyScreen.ScoresDownKey; i++)
        {
            Press(host, seat, new MenuCommands { MoveY = 1, OnPad = true });
        }

        ctx.Check(shell.FocusedKey == OriginalLobbyScreen.ScoresDownKey, $"{prefix}: the pad's walk reaches the down arrow ({shell.FocusedKey})");
        Press(host, seat, new MenuCommands { Accept = true, KeylessAccept = true, OnPad = true });
        board = shell.Compose();
        ctx.Check(Lines(board).Contains($"{prefix}11") && !Lines(board).Contains($"{prefix}1"),
            $"{prefix}: and the pad's Accept on it moves the page one line down");
    }

    // The chat pane under its KG. Three lines fit with no bar, and thirty-two follow the newest. The
    // wheel reaches the oldest, which holds as a line arrives. The pad's Accept on the down arrow
    // steps one line, and a thumb drag to the foot follows again.
    private static void ScrollTheChat(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, DogfightLobby lobby)
    {
        var bar = OriginalRaceResults.SplitsBar;
        for (int i = 1; i <= 3; i++)
        {
            lobby.Say($"C{i}");
        }

        // ABLE-TO-FAIL CONTROL: three lines fit the pane, so the script keeps its control deactivated.
        var board = shell.Compose();
        ctx.Check(Row(shell, OriginalLobbyScreen.ChatUpKey) == null && Row(shell, OriginalLobbyScreen.ChatDownKey) == null
                  && !board.Pictures.Any(p => p.Art.Name == "MP_B_SCROLLBAR.PNG") && Lines(board).Contains("C1") && Lines(board).Contains("C3"),
            $"a three-line chat draws every line with no scroll bar");

        for (int i = 4; i <= 32; i++)
        {
            lobby.Say($"C{i}");
            Press(host, seat, MenuCommands.None);
        }

        board = shell.Compose();
        var down = Row(shell, OriginalLobbyScreen.ChatDownKey);
        ctx.Check(Lines(board).Contains("C32") && !Lines(board).Contains("C1") && down is { Enabled: false }
                  && down.X == bar.X && down.Y == bar.DownY && Row(shell, OriginalLobbyScreen.ChatUpKey) is { Enabled: true }
                  && board.Pictures.Any(p => p.Art.Name == "MP_B_SCROLLBAR.PNG" && p.X == bar.X),
            $"thirty-two lines follow the newest under the script's KG at ({down?.X}, {down?.Y}), its down arrow dead");

        Press(host, seat, new MenuCommands { Pointer = Window(ctx, 400f, 450f, -40) });
        lobby.Say("C33");
        Press(host, seat, MenuCommands.None);
        board = shell.Compose();
        ctx.Check(Lines(board).Contains("C1") && !Lines(board).Contains("C33") && Row(shell, OriginalLobbyScreen.ChatUpKey) is { Enabled: false },
            $"the wheel reaches the oldest kept line, which holds still as a new line arrives");

        for (int i = 0; i < 80 && shell.FocusedKey != OriginalLobbyScreen.ChatDownKey; i++)
        {
            Press(host, seat, new MenuCommands { MoveY = 1, OnPad = true });
        }

        Press(host, seat, new MenuCommands { Accept = true, KeylessAccept = true, OnPad = true });
        board = shell.Compose();
        ctx.Check(shell.FocusedKey == OriginalLobbyScreen.ChatDownKey && Lines(board).Contains("C2") && !Lines(board).Contains("C1"),
            $"the pad's walk reaches the down arrow and its Accept moves the chat one line down ({shell.FocusedKey})");

        var window = shell.Lists.FirstOrDefault(l => l.Key == "MPL_L_CHAT")?.Window;
        ctx.Check(window != null, $"the chat offers the pointer its window");
        if (window is { } w)
        {
            float x = w.ThumbX + (w.ThumbWidth / 2f);
            float y = w.ThumbY + (w.ThumbHeight / 2f);
            Press(host, seat, new MenuCommands { Pointer = Window(ctx, x, y, 0, pressed: true, clicked: true) });
            Press(host, seat, new MenuCommands { Pointer = Window(ctx, x, y + 200f, 0, pressed: true) });
            Press(host, seat, new MenuCommands { Pointer = Window(ctx, x, y + 200f, 0) });
        }

        lobby.Say("C34");
        Press(host, seat, MenuCommands.None);
        board = shell.Compose();
        ctx.Check(Lines(board).Contains("C34") && !Lines(board).Contains("C2"),
            $"a thumb drag to the foot follows the newest line again");
    }

    private static MenuHost? Open(TestContext ctx, MenuLayout layout, NetPlayFeature door, ScriptedSeat seat)
    {
        var registry = new PresentationRegistry();
        registry.Register(PresentationId.BuiltIn, () => new BuiltInPresentation(
            ctx.Host, ctx.ZrdrPath, ctx.DataRoot, string.Empty, new MenuInput { Keyboard = true }));
        registry.Register(PresentationId.Original, () => new OriginalPresentation(
            ctx.Host, ctx.DataRoot, layout, string.Empty, new MenuInput { Keyboard = true }));
        var host = new MenuHost(registry, new MenuSuiteHost.SilentMenuAudio(), _ => { });
        MenuSuiteHost.AddFeatures(host, ctx.DataRoot, netDoor: door);
        host.AddSeat(seat);
        host.Select(forceBuiltIn: false, cliOverride: "original");
        host.Show(MenuReturnDestination.TopLevel);
        ctx.Check(host.Active is OriginalPresentation { Shell.Screen: OriginalScreen.TopLevel }, $"the host shows Original on the top level");
        return host;
    }

    // The Connection page's Host, both network boxes answered through their OK.
    private static bool HostTheLobby(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell)
    {
        ClickRow(ctx, host, seat, shell, OriginalShell.MultiplayerKey);
        ClickRow(ctx, host, seat, shell, OriginalConnectionScreen.HostKey);
        shell.NetInfo.Draft.GameName = "Scores";
        ClickRow(ctx, host, seat, shell, OriginalNetInfoBox.OkKey);
        shell.NetInfo.Draft.Callsign = "Zachary";
        ClickRow(ctx, host, seat, shell, OriginalNetInfoBox.OkKey);
        Press(host, seat, MenuCommands.None);
        ctx.Check(shell.Screen == OriginalScreen.Lobby, $"Host opens the lobby ({shell.Screen})");
        return shell.Screen == OriginalScreen.Lobby;
    }

    private static void ClickRow(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, string key)
    {
        var row = Row(shell, key);
        ctx.Check(row != null, $"the showing screen carries {key} ({shell.Screen})");
        if (row == null)
        {
            return;
        }

        var size = ctx.Host.GetViewport().GetVisibleRect().Size;
        var fit = BoardFit.For(size.X, size.Y);
        float x = fit.X(row.X + Math.Min(5f, row.Width / 2f));
        float y = fit.Y(row.Y + Math.Min(5f, row.Height / 2f));
        Press(host, seat, new MenuCommands { Pointer = new MenuPointer(x, y, true, true, 0) });
        Press(host, seat, new MenuCommands { Pointer = new MenuPointer(x, y, false, false, 0) });
    }

    // A pointer frame at an authored point, carrying a wheel of that many rows.
    private static MenuPointer Window(TestContext ctx, float x, float y, int wheel, bool pressed = false, bool clicked = false)
    {
        var size = ctx.Host.GetViewport().GetVisibleRect().Size;
        var fit = BoardFit.For(size.X, size.Y);
        return new MenuPointer(fit.X(x), fit.Y(y), pressed, clicked, wheel);
    }

    private static void Press(MenuHost host, ScriptedSeat seat, MenuCommands commands)
    {
        seat.Enqueue(commands);
        host.Tick(Dt);
    }

    private static OriginalRow? Row(OriginalShell shell, string key) => shell.Rows.FirstOrDefault(row => row.Key == key);

    private static bool Draws(ComposedBoard board, string text) =>
        board.Lines.Any(line => line.Text.Contains(text, StringComparison.Ordinal));

    private static HashSet<string> Lines(ComposedBoard board) => board.Lines.Select(line => line.Text).ToHashSet();

    // A seat whose frames a suite writes, and which reads nothing of its own.
    private sealed class ScriptedSeat : IMenuInputSource
    {
        private readonly Queue<MenuCommands> _frames = new();

        public string DeviceLabel => "scripted";

        public bool CapturingText { get; set; }

        public void Enqueue(MenuCommands frame) => _frames.Enqueue(frame);

        public MenuCommands Poll(float dt) => _frames.Count > 0 ? _frames.Dequeue() : MenuCommands.None;

        public void Prime()
        {
        }
    }
}
