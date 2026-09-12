using System;
using System.Collections.Generic;
using CSVM.Bindings;
using CSVM.Flight;
using CSVM.Mech3;
using Godot;

namespace CSVM.Testing;

/// <summary>The control prompt over a real seat: which device the auto-dock line names as the
/// seat's last real input moves from the pad to the keyboard and back, and what the line reads when
/// the active device has no binding for the action. The wording comes from the install's own
/// message table, so each check is on the whole composed line rather than on the selection alone.
/// ⚠ A headless run holds down no key and no stick, so the tick's two halves are supplied
/// (<c>ObserveDeviceForTest</c>) rather than polled. The seat's own poll, the prompt composition and
/// the recompose on a handover are the shipping ones.</summary>
internal static class PromptDeviceSuites
{
    private const string PressKey = "MSG_PRESS_AUTOLAND";

    [Suite("bindings-prompt-device",
        "the auto-dock prompt follows the device the seat last took input from: a seat that has "
        + "touched nothing names its key, a pad button hands the line to the stick button, a tick "
        + "with nothing held leaves it alone, a key press takes it back, and an auto-land the pad "
        + "has no binding for falls back to naming the key")]
    internal static void BindingsPromptDevice(TestContext ctx)
    {
        ctx.RequireData(ctx.MessagesPath, $"the install's message table");
        var strings = Messages.Load(ctx.MessagesPath);
        string onKey = strings.Format(PressKey, "F9");
        string onPad = strings.Format(PressKey, "Pad Left Stick");
        var rig = new FlightController
        {
            PlayerIndex = 0,
            UseKeyboard = true,
            PadDevices = Array.Empty<int>(),
        };
        try
        {
            rig.UseMessages(strings);
            ctx.Check(rig.PilotHud.AutoLandPrompt == onKey,
                $"a seat that has touched nothing names its key: '{rig.PilotHud.AutoLandPrompt}'");

            var padButton = OnPad(JoyButton.LeftStick);
            rig.ObserveDeviceForTest(new OneSide(), padButton);
            ctx.Check(rig.ActiveDeviceSide == DeviceSide.Pad && rig.PilotHud.AutoLandPrompt == onPad,
                $"the pad button hands the line to the pad control: '{rig.PilotHud.AutoLandPrompt}'");

            rig.ObserveDeviceForTest(new OneSide(), new OneSide());
            ctx.Check(rig.ActiveDeviceSide == DeviceSide.Pad && rig.PilotHud.AutoLandPrompt == onPad,
                $"…and a tick with nothing held leaves it there: '{rig.PilotHud.AutoLandPrompt}'");

            var key = new OneSide();
            key.Keys.Add((int)Key.F9);
            rig.ObserveDeviceForTest(key, new OneSide());
            ctx.Check(rig.ActiveDeviceSide == DeviceSide.Keyboard && rig.PilotHud.AutoLandPrompt == onKey,
                $"a key press takes the line back to the keyboard: '{rig.PilotHud.AutoLandPrompt}'");

            Fallback(ctx, rig, onKey);
        }
        finally
        {
            rig.Free();
        }
    }

    // The pad side active on an action the pad carries no binding for: the line names the other
    // device's control rather than going empty, which is what an unbound half has to read as. The
    // pad control held is another action's, so the side still moves; the observe recomposes the
    // prompt itself, over the binding list the unassign shortened.
    private static void Fallback(TestContext ctx, FlightController rig, string onKey)
    {
        foreach (var binding in new List<Binding>(rig.FlightKeymap.Bindings(InputAction.AutoLand)))
        {
            if (binding.Control.Kind != ControlKind.Key)
                rig.FlightKeymap.Unassign(InputAction.AutoLand, binding);
        }

        rig.ObserveDeviceForTest(new OneSide(), OnPad(JoyButton.B));
        ctx.Check(rig.ActiveDeviceSide == DeviceSide.Pad && rig.PilotHud.AutoLandPrompt == onKey,
            $"an auto-land the pad has no binding for names the key: '{rig.PilotHud.AutoLandPrompt}'");
    }

    // One pad button held, on the placeholder identity a seat's pad rows sit on.
    private static OneSide OnPad(JoyButton button)
    {
        var side = new OneSide();
        side.Buttons.Add((DefaultBindings.AnyPad, (int)button));
        return side;
    }

    // One side's hardware for a tick the suite writes itself: keys for the keyboard half, buttons
    // and axes for the pad half, nothing at all for the other side's reads.
    private sealed class OneSide : IDeviceState
    {
        public HashSet<int> Keys { get; } = new();

        public HashSet<(DeviceId Device, int Index)> Buttons { get; } = new();

        public Dictionary<(DeviceId Device, int Index), float> Axes { get; } = new();

        public bool IsKeyDown(DeviceId device, int keyCode) =>
            device == DeviceId.Keyboard && Keys.Contains(keyCode);

        public bool IsButtonDown(DeviceId device, int button) => Buttons.Contains((device, button));

        public bool IsMouseButtonDown(DeviceId device, int button) => false;

        public float AxisValue(DeviceId device, int axis) =>
            Axes.TryGetValue((device, axis), out float value) ? value : 0f;

        public HatDirection HatState(DeviceId device, int hat) => HatDirection.None;
    }
}
