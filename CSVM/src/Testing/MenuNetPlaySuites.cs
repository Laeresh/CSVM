using System.Collections.Generic;
using System.Globalization;
using CSVM.Net;
using CSVM.UI.Boards;
using CSVM.UI.Menu;

namespace CSVM.Testing;

/// <summary>
/// The multiplayer door's own address seam, driven on the feature with no screen in front of it. A
/// hosting door copies the address a guest types. The key event the launcher reads as the copy is
/// the chord and nothing near it. The carrier is the shipped one bound to the loopback address,
/// so nothing here reaches a network or a firewall.
/// </summary>
internal static class MenuNetPlaySuites
{
    // How far the door's port is walked when a foreign process holds one, SuitePorts' door range.
    private const int PortsToTry = SuitePorts.DoorWalk;

    [Suite("menu-host-address",
        "a hosting door copies the address a guest types: a Ctrl+C key event is a copy chord where "
        + "Ctrl+V, a bare C and Ctrl+Shift+C are not, the copy puts a stand-in stable IPv6 address "
        + "bracketed with the walked port on a stand-in clipboard, and a door with no address seam "
        + "and no clipboard copies nothing")]
    internal static void TheHostCopiesItsAddress(TestContext ctx)
    {
        const string Stable = "2a04:6ec0:232:6640:feb1:ff80:9ed7:dd90";
        var copied = new List<string>();
        var door = new NetPlayFeature(
            (port, guests, bind) => EnetTransport.Host(port, guests, bind),
            (address, port) => EnetTransport.Join(address, port))
        {
            BindAddress = "127.0.0.1",
            Reach = { StableIpv6 = () => Stable, LanIpv4 = () => "192.168.178.20", CopyText = copied.Add },
        };
        try
        {
            int port = Host(ctx, door);
            if (port == 0)
            {
                return;
            }

            bool Chord(Godot.Key key, bool ctrl, bool shift = false) => MenuInput.IsCopyChord(
                new Godot.InputEventKey { Keycode = key, PhysicalKeycode = key, CtrlPressed = ctrl, ShiftPressed = shift, Pressed = true });
            ctx.Check(Chord(Godot.Key.C, ctrl: true) && Chord(Godot.Key.Insert, ctrl: true),
                $"Ctrl+C and Ctrl+Insert are copy chords");
            ctx.Check(!Chord(Godot.Key.V, ctrl: true) && !Chord(Godot.Key.C, ctrl: false) && !Chord(Godot.Key.C, ctrl: true, shift: true),
                $"ABLE-TO-FAIL CONTROL: Ctrl+V, a bare C and Ctrl+Shift+C are not");

            string dialled = new NetEndpoint(Stable, port).ToString();
            bool took = door.Reach.CopyGuestAddress();
            door.Step(0.016);
            ctx.Check(took && copied.Count == 1 && copied[0] == dialled,
                $"the copy puts the bracketed address on the clipboard ({took}, {string.Join(" | ", copied)})");
        }
        finally
        {
            door.Discard();
        }

        var mute = MenuSuiteHost.NetDoor();
        try
        {
            mute.StepPort(1);
            mute.OpenHost(1);
            ctx.Check(mute.IsHost && !mute.Reach.CopyGuestAddress(),
                $"ABLE-TO-FAIL CONTROL: a door with no address seam and no clipboard copies nothing ({mute.Stage}, {mute.Fault})");
        }
        finally
        {
            mute.Discard();
        }
    }

    // Opens a listen server, stepping the port past any a foreign process holds. Returns the port
    // that opened, or 0 when every port tried was taken, which is a machine this suite cannot measure on.
    private static int Host(TestContext ctx, NetPlayFeature door)
    {
        for (int i = 0; i < PortsToTry && door.Stage != NetDoorStage.Hosting; i++)
        {
            door.StepPort(1);
            door.OpenHost(1);
        }

        if (door.Stage != NetDoorStage.Hosting)
        {
            ctx.Check(false, $"no port in {PortsToTry.ToString(CultureInfo.InvariantCulture)} tries would open: {door.Fault}");
            return 0;
        }

        return door.Port;
    }
}
