using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CSVM.Bindings;

/// <summary>The keymap the game ships with, as data: one <see cref="ActionMap"/> per
/// <see cref="InputContext"/>, reproducing `docs/controls.md` binding for binding. It is the
/// fallback every load falls back to, per action, so a file that cannot be read costs the player
/// the actions it named and nothing else.
/// ⚠ No default is a hat binding, and nothing else may author one either. Godot reports a d-pad as
/// four buttons (`GodotDeviceState`), so a hat binding and a d-pad button binding would be one
/// physical control under two encodings that <see cref="ActionMap.SameControl"/> reads as
/// different, and two actions could hold it at once.
/// ⚠ A pad default names <see cref="AnyPad"/>, not a real pad. <see cref="MapFor"/> substitutes the
/// seat's own identity, because the shipped set cannot know a hardware string.</summary>
public static class DefaultBindings
{
    /// <summary>The stand-in device a shipped pad default is authored on, meaning "this seat's
    /// pad". No hardware reports it, so a map still holding it after <see cref="MapFor"/> resolves
    /// to nothing rather than to somebody else's pad.</summary>
    public static readonly DeviceId AnyPad = DeviceId.Joypad("*");

    // Menu navigation counts a stick deflection past this as a d-pad press (MenuInput), the
    // spectator camera gates its sticks here (SpectatorCamera), and a trigger driving a digital
    // action wants half its travel. The three numbers are the polling sites' own, kept so the
    // migration changes no behaviour; they are TUNE from here on.
    private const float MenuStickDeadzone = 0.5f;
    private const float CameraStickDeadzone = 0.18f;
    private const float TriggerThreshold = 0.5f;

    private static readonly ContextBuilder FlightDefaults = BuildFlight();
    private static readonly ContextBuilder MenuDefaults = BuildMenu();
    private static readonly ContextBuilder CameraDefaults = BuildCamera();

    /// <summary>The actions the shipped set deliberately leaves unbound, which is the list that
    /// keeps a migrating site from discovering a hole one call at a time.</summary>
    public static IReadOnlyList<InputAction> Unbound { get; } = new[]
    {
        InputAction.MenuJoin,
    };

    /// <summary>Which context owns that action. Every member of <see cref="InputAction"/> has
    /// exactly one, bound or not.</summary>
    public static InputContext ContextOf(InputAction action)
    {
        if (FlightDefaults.Actions.Contains(action))
            return InputContext.Flight;
        if (MenuDefaults.Actions.Contains(action))
            return InputContext.Menu;
        if (CameraDefaults.Actions.Contains(action))
            return InputContext.Camera;
        throw new System.ArgumentOutOfRangeException(
            nameof(action), action, "Every action belongs to one context; add the new one here.");
    }

    /// <summary>Every action that context owns, in enum order, including the ones it leaves
    /// unbound. A saved file lists all of them so an unbind survives a reload.</summary>
    public static IReadOnlyList<InputAction> ActionsIn(InputContext context) => Builder(context).Actions;

    /// <summary>A fresh default map for that context, with <paramref name="pad"/> put in place of
    /// <see cref="AnyPad"/>. Pass <c>default</c> for a seat with no pad: the pad rows stay in the
    /// map and resolve to nothing, so plugging one in later is a re-materialisation and not a
    /// rebuild of the player's keymap.</summary>
    public static ActionMap MapFor(InputContext context, DeviceId pad)
    {
        var template = Builder(context).Map;
        var map = new ActionMap();
        foreach (var action in template.BoundActions)
        {
            foreach (var binding in template.Bindings(action))
            {
                map.Add(action, Retarget(binding, pad));
            }
        }

        return map;
    }

    /// <summary>The same substitution one binding at a time, for a loader turning a stored
    /// <see cref="AnyPad"/> row into this seat's pad.</summary>
    public static Binding Retarget(Binding binding, DeviceId pad) =>
        binding.Device == AnyPad && pad.Kind == DeviceKind.Joypad ? binding with { Device = pad } : binding;

    private static ContextBuilder Builder(InputContext context) => context switch
    {
        InputContext.Flight => FlightDefaults,
        InputContext.Menu => MenuDefaults,
        _ => CameraDefaults,
    };

    // Flight, from docs/controls.md's flight table. The attitude signs follow FlightController's
    // own key pairs: S and Down pitch up, A and Left roll left, Q and the left shoulder yaw left.
    // The numpad snap-look diagonals each drive two actions at once, which the OR rule expresses
    // and four typed slots cannot.
    private static ContextBuilder BuildFlight()
    {
        var b = new ContextBuilder();
        b.Keys(InputAction.PitchUp, Key.S, Key.Down).Stick(InputAction.PitchUp, JoyAxis.LeftY, 1, 0f);
        b.Keys(InputAction.PitchDown, Key.W, Key.Up).Stick(InputAction.PitchDown, JoyAxis.LeftY, -1, 0f);
        b.Keys(InputAction.RollLeft, Key.A, Key.Left).Stick(InputAction.RollLeft, JoyAxis.LeftX, -1, 0f);
        b.Keys(InputAction.RollRight, Key.D, Key.Right).Stick(InputAction.RollRight, JoyAxis.LeftX, 1, 0f);
        b.Keys(InputAction.YawLeft, Key.Q).Buttons(InputAction.YawLeft, JoyButton.LeftShoulder);
        b.Keys(InputAction.YawRight, Key.E).Buttons(InputAction.YawRight, JoyButton.RightShoulder);
        b.Keys(InputAction.ThrottleUp, Key.Shift).Stick(InputAction.ThrottleUp, JoyAxis.TriggerRight, 1, 0f);
        b.Keys(InputAction.ThrottleDown, Key.Ctrl).Stick(InputAction.ThrottleDown, JoyAxis.TriggerLeft, 1, 0f);
        b.Keys(InputAction.FireGuns, Key.Space).Buttons(InputAction.FireGuns, JoyButton.B);
        b.Keys(InputAction.FireRockets, Key.F).Buttons(InputAction.FireRockets, JoyButton.A);
        b.Keys(InputAction.SelectGunGroup, Key.G).Buttons(InputAction.SelectGunGroup, JoyButton.DpadRight);
        b.Keys(InputAction.SelectOrdnance, Key.H).Buttons(InputAction.SelectOrdnance, JoyButton.DpadLeft);

        // The backward step of each selector, on two of the four function keys the original spends
        // on these cycles (docs/controls.md). No pad default: a flight pad has no free control
        // left, so a second pair is what the rebinding screen is for.
        b.Keys(InputAction.SelectGunGroupPrev, Key.F3);
        b.Keys(InputAction.SelectOrdnancePrev, Key.F4);
        b.Keys(InputAction.Nitro, Key.N).Buttons(InputAction.Nitro, JoyButton.X);
        b.Keys(InputAction.Respawn, Key.R).Buttons(InputAction.Respawn, JoyButton.Y);
        b.Keys(InputAction.AutoLand, Key.F9).Buttons(InputAction.AutoLand, JoyButton.LeftStick);
        b.Keys(InputAction.Pause, Key.P, Key.Escape).Buttons(InputAction.Pause, JoyButton.Start);
        b.Keys(InputAction.TargetNextEnemy, Key.T).Buttons(InputAction.TargetNextEnemy, JoyButton.DpadUp);
        b.Keys(InputAction.TargetNextAlly, Key.Y);
        b.Keys(InputAction.TargetNextNonAircraft, Key.U);
        b.Keys(InputAction.TargetNearest, Key.I);
        b.Keys(InputAction.TargetClear, Key.O);
        b.Keys(InputAction.CycleCockpitViews, Key.F8).Buttons(InputAction.CycleCockpitViews, JoyButton.DpadDown);
        b.Keys(InputAction.SelectChaseView, Key.F6).Buttons(InputAction.SelectChaseView, JoyButton.Back);
        // F7 is the original's own key for this camera ("Access Chase View", which its own decode
        // settles as the flyby rather than the following chase view); the pad has no spare button.
        b.Keys(InputAction.FlybyView, Key.F7);
        b.Keys(InputAction.LookUp, Key.Kp7, Key.Kp8, Key.Kp9);
        b.Keys(InputAction.LookDown, Key.Kp1, Key.Kp2, Key.Kp3);
        b.Keys(InputAction.LookLeft, Key.Kp7, Key.Kp4, Key.Kp1);
        b.Keys(InputAction.LookRight, Key.Kp9, Key.Kp6, Key.Kp3);
        b.Keys(InputAction.LookCenter, Key.Kp5);
        b.Keys(InputAction.LookBack, Key.Kp0).Buttons(InputAction.LookBack, JoyButton.RightStick);
        b.Stick(InputAction.LookAimUp, JoyAxis.RightY, -1, 0f);
        b.Stick(InputAction.LookAimDown, JoyAxis.RightY, 1, 0f);
        b.Stick(InputAction.LookAimLeft, JoyAxis.RightX, -1, 0f);
        b.Stick(InputAction.LookAimRight, JoyAxis.RightX, 1, 0f);

        // The first-person free look is the held right mouse button (docs/controls.md), the same
        // control the freecam look posture reads (SpectatorCamera).
        b.Mouse(InputAction.FreeLook, MouseButton.Right);
        return b;
    }

    // Menus and boards, from docs/controls.md's "Any mode" table and MenuInput's own reads. The
    // WASD and stick rows are the aliases that file records beside the arrows and the d-pad.
    private static ContextBuilder BuildMenu()
    {
        var b = new ContextBuilder();
        b.Keys(InputAction.MenuUp, Key.Up, Key.W).Buttons(InputAction.MenuUp, JoyButton.DpadUp)
            .Stick(InputAction.MenuUp, JoyAxis.LeftY, -1, MenuStickDeadzone);
        b.Keys(InputAction.MenuDown, Key.Down, Key.S).Buttons(InputAction.MenuDown, JoyButton.DpadDown)
            .Stick(InputAction.MenuDown, JoyAxis.LeftY, 1, MenuStickDeadzone);
        b.Keys(InputAction.MenuLeft, Key.Left, Key.A).Buttons(InputAction.MenuLeft, JoyButton.DpadLeft)
            .Stick(InputAction.MenuLeft, JoyAxis.LeftX, -1, MenuStickDeadzone);
        b.Keys(InputAction.MenuRight, Key.Right, Key.D).Buttons(InputAction.MenuRight, JoyButton.DpadRight)
            .Stick(InputAction.MenuRight, JoyAxis.LeftX, 1, MenuStickDeadzone);
        b.Keys(InputAction.MenuAccept, Key.Enter, Key.KpEnter, Key.Space)
            .Buttons(InputAction.MenuAccept, JoyButton.A);
        b.Keys(InputAction.MenuBack, Key.Escape).Buttons(InputAction.MenuBack, JoyButton.B);
        b.Buttons(InputAction.MenuStart, JoyButton.Start);
        b.Keys(InputAction.MenuLoadout, Key.L).Buttons(InputAction.MenuLoadout, JoyButton.Y);
        b.Keys(InputAction.MenuPresets, Key.P).Buttons(InputAction.MenuPresets, JoyButton.X);

        // Joining is any control on a pad no seat owns yet, which is a gesture rather than a
        // binding: every control it watches already belongs to another menu action here.
        b.Leave(InputAction.MenuJoin);
        return b;
    }

    // The spectator and anim-lab camera, from docs/controls.md's freecam table and
    // SpectatorCamera's own key pairs: E and U rise, Q and Z descend, and the IJKL look cluster
    // pitches the way the right stick does (I down, K up).
    private static ContextBuilder BuildCamera()
    {
        var b = new ContextBuilder();
        b.Keys(InputAction.CameraForward, Key.W, Key.Up)
            .Stick(InputAction.CameraForward, JoyAxis.LeftY, -1, CameraStickDeadzone);
        b.Keys(InputAction.CameraBack, Key.S, Key.Down)
            .Stick(InputAction.CameraBack, JoyAxis.LeftY, 1, CameraStickDeadzone);
        b.Keys(InputAction.CameraLeft, Key.A, Key.Left)
            .Stick(InputAction.CameraLeft, JoyAxis.LeftX, -1, CameraStickDeadzone);
        b.Keys(InputAction.CameraRight, Key.D, Key.Right)
            .Stick(InputAction.CameraRight, JoyAxis.LeftX, 1, CameraStickDeadzone);
        b.Keys(InputAction.CameraUp, Key.E, Key.U).Buttons(InputAction.CameraUp, JoyButton.RightShoulder);
        b.Keys(InputAction.CameraDown, Key.Q, Key.Z).Buttons(InputAction.CameraDown, JoyButton.LeftShoulder);
        b.Keys(InputAction.CameraBoost, Key.Shift)
            .Stick(InputAction.CameraBoost, JoyAxis.TriggerRight, 1, TriggerThreshold);
        b.Keys(InputAction.CameraSlow, Key.Ctrl)
            .Stick(InputAction.CameraSlow, JoyAxis.TriggerLeft, 1, TriggerThreshold);
        b.Keys(InputAction.CameraLockTarget, Key.F).Buttons(InputAction.CameraLockTarget, JoyButton.X);
        b.Keys(InputAction.CameraLookUp, Key.K)
            .Stick(InputAction.CameraLookUp, JoyAxis.RightY, -1, CameraStickDeadzone);
        b.Keys(InputAction.CameraLookDown, Key.I)
            .Stick(InputAction.CameraLookDown, JoyAxis.RightY, 1, CameraStickDeadzone);
        b.Keys(InputAction.CameraLookLeft, Key.J)
            .Stick(InputAction.CameraLookLeft, JoyAxis.RightX, -1, CameraStickDeadzone);
        b.Keys(InputAction.CameraLookRight, Key.L)
            .Stick(InputAction.CameraLookRight, JoyAxis.RightX, 1, CameraStickDeadzone);

        // The dolly shares the trigger pair with boost and slow at its own deadzone of zero, because
        // it is a rate the whole travel feeds while boost is a switch that wants half of it. The two
        // readings are separate actions rather than one, so neither has to carry the other's number.
        b.Stick(InputAction.CameraDollyOut, JoyAxis.TriggerRight, 1, 0f);
        b.Stick(InputAction.CameraDollyIn, JoyAxis.TriggerLeft, 1, 0f);
        return b;
    }

    // One context's shipped set while it is being written: the map, and the actions it owns in the
    // order they were first named. Bindings go in through ActionMap.Add rather than Assign because
    // some controls are deliberately on two actions (a snap-look diagonal, d-pad up in flight),
    // which the steal rule would undo.
    private sealed class ContextBuilder
    {
        private readonly List<InputAction> _actions = new();

        public ActionMap Map { get; } = new();

        public IReadOnlyList<InputAction> Actions => _actions;

        public ContextBuilder Keys(InputAction action, params Key[] keys)
        {
            Track(action);
            foreach (var key in keys)
            {
                Map.Add(action, new Binding(DeviceId.Keyboard, BindingControl.Key((int)key)));
            }

            return this;
        }

        public ContextBuilder Buttons(InputAction action, params JoyButton[] buttons)
        {
            Track(action);
            foreach (var button in buttons)
            {
                Map.Add(action, new Binding(AnyPad, BindingControl.Button((int)button)));
            }

            return this;
        }

        public ContextBuilder Stick(InputAction action, JoyAxis axis, int sign, float deadzone)
        {
            Track(action);
            Map.Add(action, new Binding(AnyPad, BindingControl.Axis((int)axis, sign, deadzone)));
            return this;
        }

        public ContextBuilder Mouse(InputAction action, MouseButton button)
        {
            Track(action);
            Map.Add(action, new Binding(DeviceId.Mouse, BindingControl.Mouse((int)button)));
            return this;
        }

        public ContextBuilder Leave(InputAction action)
        {
            Track(action);
            return this;
        }

        private void Track(InputAction action)
        {
            if (!_actions.Contains(action))
            {
                _actions.Add(action);
            }
        }
    }
}
