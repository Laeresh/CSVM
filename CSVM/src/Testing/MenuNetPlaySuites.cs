using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Net;
using CSVM.UI;
using CSVM.UI.Boards;
using CSVM.UI.Menu;
using CSVM.UI.Screens;

namespace CSVM.Testing;

/// <summary>
/// Built-in's multiplayer door, driven as a player drives it. The Mode screen's last row opens
/// the board. Its ten rows edit the port, the address and the game's and player's information,
/// and open a real socket.
/// The status line under them says what the socket is doing. Continue walks on to the Dogfight
/// map screen with one pilot seated, and backing out hangs up. The carrier is the shipped one
/// bound to the loopback address, so nothing here reaches a network or a firewall.
/// </summary>
internal static class MenuNetPlaySuites
{
    // How far the board's port is walked when a foreign process holds one, SuitePorts' door range.
    // Every Right on the port row is one step, so the walk below is the board's own gesture.
    private const int PortsToTry = SuitePorts.DoorWalk;

    private static readonly MenuCommands Accept = new() { Accept = true };
    private static readonly MenuCommands Back = new() { Back = true };
    private static readonly MenuCommands Down = new() { MoveY = 1 };
    private static readonly MenuCommands Up = new() { MoveY = -1 };
    private static readonly MenuCommands Right = new() { MoveX = 1 };

    [Suite("menu-net-door",
        "Built-in's multiplayer door driven as a player drives it: the Mode screen's last row "
        + "opens an eleven-row board, its cap and voice rows step inside their ranges, its listing row "
        + "flips Public and Private, the port row steps, "
        + "Host remembers the voice and opens a real ENet socket on the "
        + "loopback address and the status line reports it, Continue walks on to the Dogfight map "
        + "screen with one pilot seated, and Back off the board hangs up")]
    internal static void TheMultiplayerDoor(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var exits = new List<MenuExit>();
        var host = MenuSuiteHost.Bare(exits, ctx.DataRoot, out var seat);
        var menu = LaunchMenu.Build(ctx.ZrdrPath, ctx.DataRoot, host, seat.Input);
        var door = host.Features.Get<NetPlayFeature>();
        ctx.Host.AddChild(menu);
        string? options = MenuSuiteHost.ScratchOptions(ctx, "menu-net-door");
        try
        {
            menu.ShowMenu();
            OpenBoard(ctx, menu);
            InformationRows(ctx, menu, door);
            int port = HostAMatch(ctx, menu, door);
            if (port == 0)
            {
                return;
            }

            ctx.Check(CSVM.Utils.OptionsStore.UserOptions().Load().NetVoice == 1,
                $"Host remembers the chosen voice for the next session ({CSVM.Utils.OptionsStore.UserOptions().Load().NetVoice})");
            Continue(ctx, menu);
            HangUp(ctx, menu, door);
        }
        finally
        {
            door.Discard();
            ctx.Host.RemoveChild(menu);
            menu.QueueFree();
            CSVM.Utils.OptionsStore.DirectoryOverride = options;
        }
    }

    [Suite("menu-net-dogfight-launch",
        "a networked Dogfight with one local seat flies: the password row takes a letter while the "
        + "door is shut and refuses one once Host on Built-in's multiplayer board opens it, "
        + "Continue and the map lead to aircraft select, and the lone pilot's select and confirm "
        + "leave as one Dogfight launch exit carrying the host's wire, the two-seat minimum lifted")]
    internal static void TheLonePilotLaunches(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var exits = new List<MenuExit>();
        var host = MenuSuiteHost.Bare(exits, ctx.DataRoot, out var seat);
        var menu = LaunchMenu.Build(ctx.ZrdrPath, ctx.DataRoot, host, seat.Input);
        var door = host.Features.Get<NetPlayFeature>();
        ctx.Host.AddChild(menu);
        string? options = MenuSuiteHost.ScratchOptions(ctx, "menu-net-dogfight-launch");
        try
        {
            menu.ShowMenu();
            OpenBoard(ctx, menu);
            var key = new Godot.InputEventKey { Pressed = true, Keycode = Godot.Key.A, Unicode = 'a' };
            Walk(menu, 4);
            menu._UnhandledInput(key);
            ctx.Check(menu.ShownRowText.StartsWith("Password", StringComparison.Ordinal) && door.Password == "a",
                $"ABLE-TO-FAIL CONTROL: a shut door's password row takes a typed letter ({menu.ShownRowText}, '{door.Password}')");
            door.Password = "";
            Walk(menu, -4);
            if (HostAMatch(ctx, menu, door) == 0)
            {
                return;
            }

            Walk(menu, 2);
            menu._UnhandledInput(key);
            ctx.Check(door.Password.Length == 0, $"a hosting door's password row refuses it, the lobby holding the one it opened with ('{door.Password}')");
            Walk(menu, -2);
            menu.Drive(Accept);
            menu.Drive(Accept);
            ctx.Check(menu.ShownScreen == "Plane" && exits.Count == 0, $"Continue and the map reach aircraft select ({menu.ShownScreen}, {exits.Count})");
            menu.Drive(Accept);
            ctx.Check(exits.Count == 0, $"ABLE-TO-FAIL CONTROL: selecting the airframe alone does not launch ({exits.Count})");
            menu.Drive(Accept);
            ctx.Check(exits.Count == 1 && exits[0] is LaunchExit { Mode: CSVM.Spec.MenuMode.Versus, Seats.Count: 1, Net.IsHost: true },
                $"the lone pilot's confirm leaves as one Dogfight exit with the host's wire ({exits.Count}, {(exits.Count > 0 ? exits[0] : null)})");
        }
        finally
        {
            // The launch handed the wire to a session this suite never builds, so the suite closes it.
            if (exits.Count > 0 && exits[0] is LaunchExit { Net.Transport: IDisposable wire })
            {
                wire.Dispose();
            }

            door.Discard();
            ctx.Host.RemoveChild(menu);
            menu.QueueFree();
            CSVM.Utils.OptionsStore.DirectoryOverride = options;
        }
    }

    [Suite("menu-screen-keyboard",
        "Steam's on-screen keyboard on Built-in's multiplayer board, its URLs recorded: off a "
        + "SteamOS device a pad's Accept on the address row raises nothing, on one it raises the "
        + "keyboard there without leaving the row, the echo strip repeats the address and masks the "
        + "password, focus moving off the field lowers it and coming back alone raises nothing, and "
        + "a key's Enter in the field lowers it")]
    internal static void TheOnScreenKeyboard(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var exits = new List<MenuExit>();
        var host = MenuSuiteHost.Bare(exits, ctx.DataRoot, out var seat);
        var menu = LaunchMenu.Build(ctx.ZrdrPath, ctx.DataRoot, host, seat.Input);
        var door = host.Features.Get<NetPlayFeature>();
        var echo = new ScreenKeyboardEcho();
        ctx.Host.AddChild(menu);
        ctx.Host.AddChild(echo);
        var keyboard = new ScreenKeyboardRecorder();
        try
        {
            var pad = new MenuCommands { Accept = true, KeylessAccept = true };
            menu.ShowMenu();
            OpenBoard(ctx, menu);
            menu.Drive(Up);
            ctx.Check(menu.ShownRow == 1, $"Up from Host stands on the address row ({menu.ShownRow}, {menu.ShownRowText})");
            CSVM.Utils.ScreenKeyboard.Available = false;
            menu.Drive(pad);
            ctx.Check(keyboard.Urls.Count == 0, $"ABLE-TO-FAIL CONTROL: off a SteamOS device the pad's Accept raises nothing ({keyboard.Said})");
            CSVM.Utils.ScreenKeyboard.Available = true;

            menu.Drive(pad);
            ctx.Check(keyboard.Said == CSVM.Utils.ScreenKeyboard.OpenUrl && CSVM.Utils.ScreenKeyboard.Shown?.Id == "address",
                $"on one the pad's Accept raises the keyboard for the address ({keyboard.Said}, {CSVM.Utils.ScreenKeyboard.Shown?.Id})");
            ctx.Check(menu.ShownScreen == "Network" && menu.ShownRow == 1, $"and the press is spent there ({menu.ShownScreen}, {menu.ShownRow})");
            echo._Process(0);
            ctx.Check(echo.Line == $"Address:  {door.Address}_", $"the echo strip repeats the address with a caret ({echo.Line})");

            menu.Drive(Down);
            echo._Process(0);
            ctx.Check(keyboard.Urls.Count == 2 && keyboard.Urls[1] == CSVM.Utils.ScreenKeyboard.CloseUrl && echo.Line.Length == 0,
                $"the cursor leaving the field lowers it and the strip goes ({keyboard.Said}, '{echo.Line}')");
            menu.Drive(Up);
            ctx.Check(keyboard.Urls.Count == 2, $"coming back onto the field alone raises nothing ({keyboard.Said})");

            menu.Drive(pad);
            menu.Drive(Accept);
            ctx.Check(keyboard.Urls.Count == 4 && keyboard.Urls[3] == CSVM.Utils.ScreenKeyboard.CloseUrl && menu.ShownRow == 1,
                $"a key's Enter in the field lowers it ({keyboard.Said}, {menu.ShownRow})");

            door.Password = "abc";
            for (int i = 0; i < 5; i++)
            {
                menu.Drive(Down);
            }

            menu.Drive(pad);
            echo._Process(0);
            ctx.Check(CSVM.Utils.ScreenKeyboard.Shown?.Id == "password" && echo.Line == "Password:  ***_",
                $"the password raises it masked ({CSVM.Utils.ScreenKeyboard.Shown?.Id}, {echo.Line})");
        }
        finally
        {
            keyboard.Dispose();
            door.Discard();
            ctx.Host.RemoveChild(echo);
            echo.QueueFree();
            ctx.Host.RemoveChild(menu);
            menu.QueueFree();
        }
    }

    [Suite("menu-host-address",
        "a hosting door names the address a guest types and copies it: the Built-in board's status "
        + "line names a stand-in stable IPv6 address bracketed with the walked port and the LAN "
        + "address beside it, a Ctrl+C key event is a copy chord where Ctrl+V, a bare C and Ctrl+Shift+C "
        + "are not, the copy puts that bracketed address on a stand-in clipboard and the status line "
        + "says so, and a door with no clipboard copies nothing")]
    internal static void TheHostNamesAndCopiesItsAddress(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        const string Stable = "2a04:6ec0:232:6640:feb1:ff80:9ed7:dd90";
        const string Lan = "192.168.178.20";
        var copied = new List<string>();
        var door = new NetPlayFeature(
            (port, guests, bind) => EnetTransport.Host(port, guests, bind),
            (address, port) => EnetTransport.Join(address, port))
        {
            BindAddress = "127.0.0.1",
            StableIpv6 = () => Stable,
            LanIpv4 = () => Lan,
            CopyText = copied.Add,
        };
        var exits = new List<MenuExit>();
        var host = MenuSuiteHost.Bare(exits, ctx.DataRoot, out var seat, netDoor: door);
        var menu = LaunchMenu.Build(ctx.ZrdrPath, ctx.DataRoot, host, seat.Input);
        ctx.Host.AddChild(menu);
        try
        {
            menu.ShowMenu();
            OpenBoard(ctx, menu);
            int port = HostAMatch(ctx, menu, door);
            if (port == 0)
            {
                return;
            }

            string dialled = new NetEndpoint(Stable, port).ToString();
            string lan = new NetEndpoint(Lan, port).ToString();
            ctx.Check(menu.ShownDetail.Contains($"Guests type {dialled}, or {lan} on this network.", StringComparison.Ordinal)
                      && menu.ShownDetail.Contains($"{CoopDoorText.CopyPress} copies {dialled}.", StringComparison.Ordinal),
                $"the status line names the bracketed IPv6 address, the LAN address and the copy key ({menu.ShownDetail})");

            bool Chord(Godot.Key key, bool ctrl, bool shift = false) => MenuInput.IsCopyChord(
                new Godot.InputEventKey { Keycode = key, PhysicalKeycode = key, CtrlPressed = ctrl, ShiftPressed = shift, Pressed = true });
            ctx.Check(Chord(Godot.Key.C, ctrl: true) && Chord(Godot.Key.Insert, ctrl: true),
                $"Ctrl+C and Ctrl+Insert are copy chords");
            ctx.Check(!Chord(Godot.Key.V, ctrl: true) && !Chord(Godot.Key.C, ctrl: false) && !Chord(Godot.Key.C, ctrl: true, shift: true),
                $"ABLE-TO-FAIL CONTROL: Ctrl+V, a bare C and Ctrl+Shift+C are not");

            int revision = door.Revision;
            bool took = door.CopyGuestAddress();
            door.Step(0.016);
            ctx.Check(took && copied.Count == 1 && copied[0] == dialled,
                $"the copy puts the bracketed address on the clipboard ({took}, {string.Join(" | ", copied)})");
            ctx.Check(door.Revision > revision && menu.ShownDetail.Contains($"{dialled} is copied.", StringComparison.Ordinal),
                $"and the status line says so on the next step ({menu.ShownDetail})");
        }
        finally
        {
            door.Discard();
            ctx.Host.RemoveChild(menu);
            menu.QueueFree();
        }

        var mute = MenuSuiteHost.NetDoor();
        try
        {
            mute.StepPort(1);
            mute.OpenHost(1);
            ctx.Check(mute.IsHost && !mute.CopyGuestAddress() && CoopDoorText.HostAddressStatus(mute).Length == 0,
                $"ABLE-TO-FAIL CONTROL: a door with no address seam and no clipboard names nothing and copies nothing ({mute.Stage}, {mute.Fault})");
        }
        finally
        {
            mute.Discard();
        }
    }

    // Down (or Up, for a negative count) that many board rows.
    private static void Walk(LaunchMenu menu, int rows)
    {
        for (int i = 0; i < Math.Abs(rows); i++)
        {
            menu.Drive(rows > 0 ? Down : Up);
        }
    }

    // The Mode screen's last row, and what the board looks like before anything is open.
    private static void OpenBoard(TestContext ctx, LaunchMenu menu)
    {
        menu.Drive(Up);
        ctx.Check(menu.ShownRowText == LaunchMenu.NetworkRow,
            $"the Mode screen's last row is the multiplayer door ({menu.ShownRowText})");
        ctx.Check(menu.ShownDetail == "Host a Dogfight over the network, or join a Dogfight or a campaign by address.",
            $"and its description says what it is for ({menu.ShownDetail})");

        menu.Drive(Accept);
        ctx.Check(menu.ShownScreen == "Network" && menu.ShownRowCount == 11,
            $"Accept opens the board, eleven rows ({menu.ShownScreen}, {menu.ShownRowCount})");
        ctx.Check(menu.ShownHeading == "MULTIPLAYER"
                  && menu.ShownBreadcrumb == $"{LaunchMenu.NetworkRow}  ›  Map  ›  Aircraft",
            $"its heading and breadcrumb ({menu.ShownHeading}, {menu.ShownBreadcrumb})");
        ctx.Check(menu.ShownRow == 2 && menu.ShownRowText == "Host a match",
            $"a shut door opens under the cursor on Host ({menu.ShownRow}, {menu.ShownRowText})");
        ctx.Check(menu.ShownDetail.StartsWith("Host a match, or type an address", StringComparison.Ordinal),
            $"and the status line says nothing is open ({menu.ShownDetail})");
        ctx.Check(menu.ShownFooter.Contains("Type / Backspace  Address", StringComparison.Ordinal),
            $"the footer names the address field, whose letters the cursor keymap gives up ({menu.ShownFooter})");
    }

    // The board's own Game and Player Information rows stand under Continue. The cap steps from
    // the original's eight and stops at sixteen, and the voice steps through the seven. The cursor
    // ends back on Host.
    private static void InformationRows(TestContext ctx, LaunchMenu menu, NetPlayFeature door)
    {
        for (int i = 0; i < 3; i++)
        {
            menu.Drive(Down);
        }

        ctx.Check(menu.ShownRow == 5 && menu.ShownRowText.StartsWith("Game name", StringComparison.Ordinal),
            $"under Continue stands the game's name ({menu.ShownRow}, {menu.ShownRowText})");
        menu.Drive(Down);
        menu.Drive(Down);
        ctx.Check(menu.ShownRowText == "Max players     8", $"the cap opens on the original's eight ({menu.ShownRowText})");
        menu.Drive(Right);
        ctx.Check(door.MaxPlayers == 9 && menu.ShownRowText == "Max players     9", $"and Right steps it ({door.MaxPlayers})");
        for (int i = 0; i < 10; i++)
        {
            menu.Drive(Right);
        }

        ctx.Check(door.MaxPlayers == NetSeats.MaxPlayers, $"ABLE-TO-FAIL CONTROL: the cap stops at sixteen ({door.MaxPlayers})");
        door.PlayerName = "Laeresh";
        menu.Drive(Down);
        ctx.Check(menu.ShownRowText == "Callsign        Laeresh", $"the callsign row shows the door's callsign ({menu.ShownRowText})");
        menu.Drive(Down);
        ctx.Check(menu.ShownRowText == "Voice           Nathan Zachary", $"the voice opens on the list's first ({menu.ShownRowText})");
        menu.Drive(Right);
        ctx.Check(door.Voice == 1 && menu.ShownRowText == "Voice           Jack", $"and Right picks the next ({door.Voice}, {menu.ShownRowText})");
        menu.Drive(Down);
        ctx.Check(menu.ShownRowText == "Listing         Public" && !door.Private, $"the listing opens on a Dogfight's Public ({menu.ShownRowText})");
        menu.Drive(Right);
        bool flipped = door.Private && menu.ShownRowText == "Listing         Private";
        menu.Drive(Right);
        ctx.Check(flipped && !door.Private, $"and Right flips it to Private and back ({flipped}, {door.Private})");
        for (int i = 0; i < 3; i++)
        {
            menu.Drive(Down);
        }

        ctx.Check(menu.ShownRow == 2, $"the rows wrap back onto Host ({menu.ShownRow})");
    }

    // Opening the socket, and the port row that decides where. Returns the port that opened, or
    // 0 when every port tried was taken, which is a machine this suite cannot measure on.
    private static int HostAMatch(TestContext ctx, LaunchMenu menu, NetPlayFeature door)
    {
        // Up twice from Host is the port row, which is the board's own way to a free port.
        menu.Drive(Up);
        menu.Drive(Up);
        ctx.Check(menu.ShownRowText == $"Port            {NetPorts.Game.ToString(CultureInfo.InvariantCulture)}",
            $"the first row is the port, on the door's own default ({menu.ShownRowText})");
        menu.Drive(Right);
        ctx.Check(door.Port == NetPorts.Game + 1,
            $"and Right steps it ({door.Port.ToString(CultureInfo.InvariantCulture)})");

        for (int i = 0; i < PortsToTry; i++)
        {
            menu.Drive(Down);
            menu.Drive(Down);
            menu.Drive(Accept);
            if (door.Stage == NetDoorStage.Hosting)
            {
                break;
            }

            menu.Drive(Up);
            menu.Drive(Up);
            menu.Drive(Right);
        }

        if (door.Stage != NetDoorStage.Hosting)
        {
            ctx.Check(false, $"no port in {PortsToTry.ToString(CultureInfo.InvariantCulture)} tries would open: {door.Fault}");
            return 0;
        }

        string port = door.Port.ToString(CultureInfo.InvariantCulture);
        ctx.Check(door.IsHost && door.CanLaunch,
            $"Host opens a listen server on {port} and the launch gate with it");
        ctx.Check(menu.ShownRow == 4 && menu.ShownRowText == "Continue → Map",
            $"and the cursor moves on to the row that leaves ({menu.ShownRow}, {menu.ShownRowText})");
        ctx.Check(menu.ShownDetail.StartsWith($"Hosting on port {port}", StringComparison.Ordinal)
                  && menu.ShownDetail.Contains("link up", StringComparison.Ordinal)
                  && menu.ShownDetail.Contains("0 joined", StringComparison.Ordinal),
            $"the status line reports the port, the link and the field ({menu.ShownDetail})");

        // ABLE-TO-FAIL CONTROL. The fields belong to the player, not to the socket, so an open
        // door refuses to move the port under itself. A board that let this through would host
        // on one port and tell the player another. Down from the last row wraps onto the first.
        for (int i = 0; i < 7; i++)
        {
            menu.Drive(Down);
        }

        menu.Drive(Right);
        ctx.Check(door.Port.ToString(CultureInfo.InvariantCulture) == port,
            $"ABLE-TO-FAIL CONTROL: the port row will not move while the socket is open ({door.Port.ToString(CultureInfo.InvariantCulture)})");
        for (int i = 0; i < 4; i++)
        {
            menu.Drive(Down);
        }

        return door.Port;
    }

    // The way on: a network match is a Dogfight, so the mode is the door's to set and the map
    // screen is next. The local two-seat minimum does not apply, the opponent is elsewhere.
    private static void Continue(TestContext ctx, LaunchMenu menu)
    {
        menu.Drive(Accept);
        ctx.Check(menu.ShownScreen == "Chapter" && menu.ShownHeading == "SELECT MAP AND MATCH RULES",
            $"Continue walks on to the Dogfight map screen ({menu.ShownScreen}, {menu.ShownHeading})");
        ctx.Check(menu.ShownBreadcrumb.StartsWith("Dogfight", StringComparison.Ordinal),
            $"and the breadcrumb names the mode the wire flies ({menu.ShownBreadcrumb})");

        menu.Drive(Accept);
        ctx.Check(menu.ShownScreen == "Plane",
            $"the map leads to aircraft select as any Dogfight does ({menu.ShownScreen})");
        ctx.Check(!menu.ShownJoinHint.Contains("needs a fight", StringComparison.Ordinal),
            $"which no longer asks for a second local pilot ({menu.ShownJoinHint})");
        menu.Drive(Back);
        menu.Drive(Back);
    }

    // Backing off the board closes the socket. A listening socket behind an abandoned screen is
    // the one outcome a player cannot see and cannot undo anywhere else.
    private static void HangUp(TestContext ctx, LaunchMenu menu, NetPlayFeature door)
    {
        ctx.Check(menu.ShownScreen == "Network" && door.Stage == NetDoorStage.Hosting,
            $"back at the board with the socket still open ({menu.ShownScreen}, {door.Stage})");
        menu.Drive(Back);
        ctx.Check(menu.ShownScreen == "Mode" && door.Stage == NetDoorStage.Shut,
            $"Back hangs up and returns to the Mode screen ({menu.ShownScreen}, {door.Stage})");
    }
}
