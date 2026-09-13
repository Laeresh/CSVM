using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Bindings;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>The shipped default keymap: every action is either bound or on the deliberately-unbound
/// list, no default is a hat binding, a control belongs to one action inside a context apart from
/// the numpad snap-look diagonals, and a pad default lands on the seat's own pad.</summary>
public class DefaultBindingsTests
{
    private static readonly DeviceId Pad = DeviceId.Joypad("030000004c050000c405000000010000");

    // The controls the shipped set deliberately puts on two actions, which is what a binding list
    // expresses and the original's four typed slots cannot: the four numpad diagonals, each one key
    // driving two look directions (Kp7 is Look Up and Look Left).
    private static readonly Binding[] Shared =
    {
        new(DeviceId.Keyboard, BindingControl.Key((int)Key.Kp7)),
        new(DeviceId.Keyboard, BindingControl.Key((int)Key.Kp9)),
        new(DeviceId.Keyboard, BindingControl.Key((int)Key.Kp1)),
        new(DeviceId.Keyboard, BindingControl.Key((int)Key.Kp3)),
    };

    /// <summary>The original's shipped keyboard rows, action by action, for
    /// <see cref="TheFlightKeys_AreTheOriginalsOwnTable"/>. The rows it binds that this port has no
    /// action for (level off, bail out, the external cameras, the look modes) are absent rather than
    /// approximated, and the two actions it has no row for are pinned separately.</summary>
    public static TheoryData<InputAction, Key, KeyModifiers> TheOriginalsKeys => new()
    {
        { InputAction.PitchUp, Key.Down, KeyModifiers.None },
        { InputAction.PitchDown, Key.Up, KeyModifiers.None },
        { InputAction.RollLeft, Key.Left, KeyModifiers.None },
        { InputAction.RollRight, Key.Right, KeyModifiers.None },
        { InputAction.YawLeft, Key.Comma, KeyModifiers.None },
        { InputAction.YawRight, Key.Period, KeyModifiers.None },
        { InputAction.ThrottleUp, Key.Equal, KeyModifiers.None },
        { InputAction.ThrottleDown, Key.Minus, KeyModifiers.None },
        { InputAction.FireGuns, Key.Space, KeyModifiers.None },
        { InputAction.FireRockets, Key.X, KeyModifiers.None },
        { InputAction.SelectGunGroup, Key.F3, KeyModifiers.None },
        { InputAction.SelectGunGroupPrev, Key.F4, KeyModifiers.None },
        { InputAction.SelectOrdnance, Key.F5, KeyModifiers.None },
        { InputAction.SelectOrdnancePrev, Key.F6, KeyModifiers.None },
        { InputAction.TargetNextEnemy, Key.E, KeyModifiers.None },
        { InputAction.TargetPreviousEnemy, Key.E, KeyModifiers.Shift },
        { InputAction.TargetNearestEnemy, Key.E, KeyModifiers.Ctrl },
        { InputAction.TargetNextAlly, Key.W, KeyModifiers.None },
        { InputAction.TargetPreviousAlly, Key.W, KeyModifiers.Shift },
        { InputAction.TargetNearestAlly, Key.W, KeyModifiers.Ctrl },
        { InputAction.TargetNextNonAircraft, Key.R, KeyModifiers.None },
        { InputAction.TargetPreviousNonAircraft, Key.R, KeyModifiers.Shift },
        { InputAction.TargetNearestNonAircraft, Key.R, KeyModifiers.Ctrl },
        { InputAction.TargetNearest, Key.Q, KeyModifiers.None },
        { InputAction.TargetClear, Key.T, KeyModifiers.None },
        { InputAction.ToggleSpyglass, Key.S, KeyModifiers.Shift },
        { InputAction.FlybyView, Key.F7, KeyModifiers.None },
        { InputAction.CycleCockpitViews, Key.F8, KeyModifiers.None },
        { InputAction.LookCenter, Key.Kp5, KeyModifiers.None },
        { InputAction.LookBack, Key.Kp0, KeyModifiers.None },
        { InputAction.Nitro, Key.N, KeyModifiers.None },
        { InputAction.AutoLand, Key.A, KeyModifiers.None },
        { InputAction.Pause, Key.Escape, KeyModifiers.None },
    };

    /// <summary>The coverage gate: a migrating polling site must never discover a missing default
    /// one call at a time, so every member of the enum is accounted for here.</summary>
    [Fact]
    public void EveryAction_IsBoundOrDeliberatelyUnbound()
    {
        var missing = new List<InputAction>();
        foreach (var action in Enum.GetValues<InputAction>())
        {
            var map = DefaultBindings.MapFor(DefaultBindings.ContextOf(action), Pad);
            if (map.Bindings(action).Count == 0 && !DefaultBindings.Unbound.Contains(action))
            {
                missing.Add(action);
            }
        }

        Assert.Equal(Array.Empty<InputAction>(), missing.ToArray());
    }

    /// <summary>The unbound list carries no stale entry either: an action listed there really does
    /// ship with nothing bound to it.</summary>
    [Fact]
    public void UnboundList_NamesOnlyActionsWithNoBinding()
    {
        foreach (var action in DefaultBindings.Unbound)
        {
            var map = DefaultBindings.MapFor(DefaultBindings.ContextOf(action), Pad);
            Assert.Empty(map.Bindings(action));
        }

        Assert.Equal(new[] { InputAction.MenuJoin }, DefaultBindings.Unbound);
    }

    [Fact]
    public void EveryAction_BelongsToExactlyOneContext()
    {
        foreach (var action in Enum.GetValues<InputAction>())
        {
            var context = DefaultBindings.ContextOf(action);
            Assert.Contains(action, DefaultBindings.ActionsIn(context));
            foreach (var other in Enum.GetValues<InputContext>().Where(c => c != context))
            {
                Assert.DoesNotContain(action, DefaultBindings.ActionsIn(other));
            }
        }
    }

    /// <summary>Godot reports a d-pad as four buttons, so a hat default would be a second encoding
    /// of a control a button binding already names and two actions could hold one direction.
    /// </summary>
    [Fact]
    public void NoDefault_IsAHatBinding()
    {
        foreach (var binding in AllDefaults())
        {
            Assert.NotEqual(ControlKind.Hat, binding.Binding.Control.Kind);
        }
    }

    /// <summary>Inside a context the steal rule holds, so the map a rebinding screen edits has no
    /// pre-existing conflict to resolve. Two shipped exceptions are stated rather than discovered:
    /// the snap-look diagonals, and flight's d-pad up.</summary>
    [Fact]
    public void InsideAContext_OneControlDrivesOneAction()
    {
        foreach (var context in Enum.GetValues<InputContext>())
        {
            var map = DefaultBindings.MapFor(context, Pad);
            var owners = new Dictionary<Binding, InputAction>();
            foreach (var action in map.BoundActions)
            {
                foreach (var binding in map.Bindings(action))
                {
                    if (owners.TryGetValue(binding, out var first))
                    {
                        Assert.Contains(binding, Shared);
                        Assert.NotEqual(first, action);
                        continue;
                    }

                    owners[binding] = action;
                }
            }
        }
    }

    [Fact]
    public void PadDefaults_LandOnTheSeatsOwnPad()
    {
        var map = DefaultBindings.MapFor(InputContext.Flight, Pad);

        Assert.Contains(
            new Binding(Pad, BindingControl.Button((int)JoyButton.B)), map.Bindings(InputAction.FireGuns));
        Assert.Contains(
            new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.Space)), map.Bindings(InputAction.FireGuns));
    }

    /// <summary>A seat with no pad keeps the pad rows on the placeholder identity, which no hardware
    /// reports, so they resolve to nothing instead of reading somebody else's pad.</summary>
    [Fact]
    public void WithoutAPad_ThePlaceholderIdentityStays()
    {
        var map = DefaultBindings.MapFor(InputContext.Flight, default);
        var rocket = map.Bindings(InputAction.FireRockets);

        Assert.Contains(rocket, b => b.Device == DefaultBindings.AnyPad);
        Assert.DoesNotContain(rocket, b => b.Device.Kind == DeviceKind.None);
    }

    /// <summary>Spot checks against `docs/controls.md`: the throttle pair is the trigger pair, the
    /// gun-group step is the right d-pad as a button, and pausing reads the original's one key plus
    /// the pad's Start.</summary>
    [Fact]
    public void FlightDefaults_MatchTheControlsRecord()
    {
        var map = DefaultBindings.MapFor(InputContext.Flight, Pad);

        Assert.Contains(
            new Binding(Pad, BindingControl.Axis((int)JoyAxis.TriggerRight, 1, 0f)),
            map.Bindings(InputAction.ThrottleUp));
        Assert.Contains(
            new Binding(Pad, BindingControl.Button((int)JoyButton.DpadRight)),
            map.Bindings(InputAction.SelectGunGroup));
        Assert.Equal(2, map.Bindings(InputAction.Pause).Count);
    }

    /// <summary>The flight keymap is the original's shipped table, key for key
    /// (`docs/org/input.md`'s 64 rows from `FUN_004936c0`). Pinned row by row rather than spot
    /// checked, because the whole point of the table is that it is theirs and not this port's.
    /// </summary>
    [Theory]
    [MemberData(nameof(TheOriginalsKeys))]
    public void TheFlightKeys_AreTheOriginalsOwnTable(InputAction action, Key key, KeyModifiers modifiers)
    {
        var map = DefaultBindings.MapFor(InputContext.Flight, Pad);

        Assert.Contains(
            new Binding(DeviceId.Keyboard, BindingControl.Key((int)key, modifiers)), map.Bindings(action));
    }

    /// <summary>The nine absolute throttle settings are the digit row, 1 for idle through 9 for
    /// full, which is why no targeting action may take a digit any more.</summary>
    [Fact]
    public void TheDigitRow_IsTheThrottleEighths()
    {
        var map = DefaultBindings.MapFor(InputContext.Flight, Pad);

        for (int eighths = 0; eighths <= 8; eighths++)
        {
            var key = (Key)((int)Key.Key1 + eighths);
            Assert.Equal(
                new[] { new Binding(DeviceId.Keyboard, BindingControl.Key((int)key)) },
                map.Bindings(InputAction.ThrottleSet0 + eighths));
        }
    }

    /// <summary>A key and the same key under a modifier are two controls, so the pair drives two
    /// actions and neither steals from the other.</summary>
    [Fact]
    public void AModifiedKey_IsADifferentControlFromTheBareKey()
    {
        var map = DefaultBindings.MapFor(InputContext.Flight, Pad);
        var bare = new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.E));
        var shifted = new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.E, KeyModifiers.Shift));

        Assert.False(ActionMap.SameControl(bare, shifted));
        Assert.Equal(new[] { InputAction.TargetNextEnemy }, map.OwnersOf(bare));
        Assert.Equal(new[] { InputAction.TargetPreviousEnemy }, map.OwnersOf(shifted));
    }

    /// <summary>A bare key stands down under a modifier its own map holds it under, so Shift+E
    /// steps the cycle back without also stepping it forward. The gate is the map's rather than
    /// global: the free camera binds Shift alone and its bare keys must still fire under it.
    /// </summary>
    [Fact]
    public void AHeldModifier_SilencesTheBareKeyOnlyWhereTheMapContestsIt()
    {
        var flight = DefaultBindings.MapFor(InputContext.Flight, Pad);
        var state = new ModifierDevices();
        state.Keys.Add((int)Key.E);
        state.Keys.Add((int)Key.Shift);
        var snapshot = flight.Resolve(state);

        Assert.False(snapshot.Held(InputAction.TargetNextEnemy));
        Assert.True(snapshot.Held(InputAction.TargetPreviousEnemy));
        Assert.False(snapshot.Held(InputAction.TargetNearestEnemy));

        var camera = DefaultBindings.MapFor(InputContext.Camera, Pad);
        state.Keys.Clear();
        state.Keys.Add((int)Key.W);
        state.Keys.Add((int)Key.Shift);
        var moving = camera.Resolve(state);

        Assert.True(moving.Held(InputAction.CameraForward));
        Assert.True(moving.Held(InputAction.CameraBoost));
    }

    /// <summary>And a modified binding wants exactly its own modifiers: Ctrl+Shift+E is neither of
    /// the two rows, so holding both fires nothing on that key.</summary>
    [Fact]
    public void AModifiedBinding_WantsExactlyTheModifiersItNames()
    {
        var map = DefaultBindings.MapFor(InputContext.Flight, Pad);
        var state = new ModifierDevices();
        state.Keys.Add((int)Key.E);
        state.Keys.Add((int)Key.Shift);
        state.Keys.Add((int)Key.Ctrl);
        var snapshot = map.Resolve(state);

        Assert.False(snapshot.Held(InputAction.TargetNextEnemy));
        Assert.False(snapshot.Held(InputAction.TargetPreviousEnemy));
        Assert.False(snapshot.Held(InputAction.TargetNearestEnemy));
    }

    /// <summary>The four weapon selectors are the original's own keys, by the name its keybind page
    /// displays: F3/F4 the guns clockwise and counterclockwise, F5/F6 the rockets
    /// (`OriginalScreenshots/Keybinds Weapons.png`). Recorded key by key because the page's names
    /// are inverted against the message keys behind them, so a later edit taking the direction off
    /// `MSG_CMD_CANNON_PREV` would bind both pairs backwards.</summary>
    [Fact]
    public void TheFourWeaponSelectors_AreTheOriginalsOwnKeys()
    {
        var map = DefaultBindings.MapFor(InputContext.Flight, Pad);
        var expected = new Dictionary<InputAction, Key>
        {
            [InputAction.SelectGunGroup] = Key.F3,
            [InputAction.SelectGunGroupPrev] = Key.F4,
            [InputAction.SelectOrdnance] = Key.F5,
            [InputAction.SelectOrdnancePrev] = Key.F6,
        };

        foreach (var (action, key) in expected)
        {
            Assert.Contains(
                new Binding(DeviceId.Keyboard, BindingControl.Key((int)key)), map.Bindings(action));
        }
    }

    /// <summary>The two backward weapon selectors ship on the keyboard alone: the pad reaches that
    /// direction by holding the forward button, as the original's joystick reaches it by going
    /// round. Recorded so a later edit cannot hand one a pad control by accident, which would step
    /// the cursor twice for one press.</summary>
    [Fact]
    public void TheBackwardSelectors_ShipOnTheKeyboardOnly()
    {
        var map = DefaultBindings.MapFor(InputContext.Flight, Pad);

        Assert.Equal(
            new[] { new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.F4)) },
            map.Bindings(InputAction.SelectGunGroupPrev));
        Assert.Equal(
            new[] { new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.F6)) },
            map.Bindings(InputAction.SelectOrdnancePrev));
    }

    /// <summary>Selecting the chase view is this port's own action, so it takes F2, the one key of
    /// the original's function-key run left free (F1 is its View Help, F3 to F8 its selectors and
    /// views). Recorded so it is not quietly handed back a key the original spends on a weapon
    /// cycle or a camera.</summary>
    [Fact]
    public void TheChaseView_TakesTheOneFreeFunctionKey()
    {
        var map = DefaultBindings.MapFor(InputContext.Flight, Pad);

        Assert.Contains(
            new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.F2)),
            map.Bindings(InputAction.SelectChaseView));
        foreach (var key in new[] { Key.F1, Key.F3, Key.F4, Key.F5, Key.F6, Key.F7, Key.F8 })
        {
            Assert.DoesNotContain(
                new Binding(DeviceId.Keyboard, BindingControl.Key((int)key)),
                map.Bindings(InputAction.SelectChaseView));
        }
    }

    /// <summary>The other two directions of each target class are the original's own Shift and Ctrl
    /// on the class letter, and they ship on the keyboard alone: the pad keeps the cycle and the
    /// crosshairs. Recorded so a later edit cannot hand one a pad control or move it back onto the
    /// digit row, which the throttle eighths now own.</summary>
    [Fact]
    public void ThePerClassPreviousAndNearest_AreTheModifiedClassKeysAndKeyboardOnly()
    {
        var map = DefaultBindings.MapFor(InputContext.Flight, Pad);
        var expected = new Dictionary<InputAction, (Key Key, KeyModifiers Modifiers)>
        {
            [InputAction.TargetPreviousEnemy] = (Key.E, KeyModifiers.Shift),
            [InputAction.TargetPreviousAlly] = (Key.W, KeyModifiers.Shift),
            [InputAction.TargetPreviousNonAircraft] = (Key.R, KeyModifiers.Shift),
            [InputAction.TargetNearestEnemy] = (Key.E, KeyModifiers.Ctrl),
            [InputAction.TargetNearestAlly] = (Key.W, KeyModifiers.Ctrl),
            [InputAction.TargetNearestNonAircraft] = (Key.R, KeyModifiers.Ctrl),
        };

        foreach (var (action, control) in expected)
        {
            Assert.Equal(InputContext.Flight, DefaultBindings.ContextOf(action));
            Assert.Equal(
                new[]
                {
                    new Binding(
                        DeviceId.Keyboard, BindingControl.Key((int)control.Key, control.Modifiers)),
                },
                map.Bindings(action));
        }
    }

    /// <summary>The spyglass toggle ships on both devices, on the original's own Shift+S now that a
    /// binding carries the modifier. Misc1 is the pad's one free control, and the original spends a
    /// joystick button on this too, so a pad-only pilot is not left without it.</summary>
    [Fact]
    public void TheSpyglassToggle_ShipsOnBothDevices()
    {
        var map = DefaultBindings.MapFor(InputContext.Flight, Pad);
        var bound = map.Bindings(InputAction.ToggleSpyglass);

        Assert.Equal(InputContext.Flight, DefaultBindings.ContextOf(InputAction.ToggleSpyglass));
        Assert.Contains(
            new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.S, KeyModifiers.Shift)), bound);
        Assert.Contains(new Binding(Pad, BindingControl.Button((int)JoyButton.Misc1)), bound);
        Assert.Equal(2, bound.Count);
    }

    /// <summary>Free look is the held right mouse button, the one action the model had no kind for
    /// until <see cref="BindingControl.Mouse"/> existed.</summary>
    [Fact]
    public void FreeLook_IsTheRightMouseButton()
    {
        var map = DefaultBindings.MapFor(InputContext.Flight, Pad);

        Assert.Equal(
            new[] { new Binding(DeviceId.Mouse, BindingControl.Mouse((int)MouseButton.Right)) },
            map.Bindings(InputAction.FreeLook));
    }

    /// <summary>The whole seat: three contexts, each with its own live actions, and the keyboard
    /// gate reaching all of them.</summary>
    [Fact]
    public void Profile_CarriesEveryContextAndTheKeyboardGate()
    {
        var profile = BindingProfile.Defaults(Pad, readsKeyboard: false);

        foreach (var context in Enum.GetValues<InputContext>())
        {
            Assert.False(profile.Actions(context).ReadsKeyboard);
        }

        profile.ReadsKeyboard = true;
        Assert.True(profile.Actions(InputContext.Menu).ReadsKeyboard);
    }

    /// <summary>The two flight actions the original has no row for take keys it leaves free:
    /// Backspace is out of the flying hand's reach for the respawn, which throws the airframe away,
    /// and F2 is the gap in its function-key run for the chase view. Recorded so neither drifts onto
    /// a key the original spends.</summary>
    [Fact]
    public void ThePortsOwnFlightActions_TakeKeysTheOriginalLeavesFree()
    {
        var map = DefaultBindings.MapFor(InputContext.Flight, Pad);

        Assert.Contains(
            new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.Backspace)),
            map.Bindings(InputAction.Respawn));
        Assert.Contains(
            new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.F2)),
            map.Bindings(InputAction.SelectChaseView));
    }

    private static IEnumerable<(InputAction Action, Binding Binding)> AllDefaults()
    {
        foreach (var context in Enum.GetValues<InputContext>())
        {
            var map = DefaultBindings.MapFor(context, Pad);
            foreach (var action in map.BoundActions)
            {
                foreach (var binding in map.Bindings(action))
                {
                    yield return (action, binding);
                }
            }
        }
    }

    // A keyboard alone, which is all the modifier rule reads.
    private sealed class ModifierDevices : IDeviceState
    {
        public HashSet<int> Keys { get; } = new();

        public bool IsKeyDown(DeviceId device, int keyCode) =>
            device == DeviceId.Keyboard && Keys.Contains(keyCode);

        public bool IsButtonDown(DeviceId device, int button) => false;

        public bool IsMouseButtonDown(DeviceId device, int button) => false;

        public float AxisValue(DeviceId device, int axis) => 0f;

        public HatDirection HatState(DeviceId device, int hat) => HatDirection.None;
    }
}
