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
    /// gun-group step is the right d-pad as a button, and pausing reads three controls at once.
    /// </summary>
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
        Assert.Equal(3, map.Bindings(InputAction.Pause).Count);
    }

    /// <summary>The two backward weapon selectors ship on the keyboard alone: every control a
    /// flight pad has is already spoken for, so the rebinding screen is where a pad player finds
    /// their second pair. Recorded so a later edit cannot hand one a pad control by accident.
    /// </summary>
    [Fact]
    public void TheBackwardSelectors_ShipOnTheKeyboardOnly()
    {
        var map = DefaultBindings.MapFor(InputContext.Flight, Pad);

        Assert.Equal(
            new[] { new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.F3)) },
            map.Bindings(InputAction.SelectGunGroupPrev));
        Assert.Equal(
            new[] { new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.F4)) },
            map.Bindings(InputAction.SelectOrdnancePrev));
    }

    /// <summary>The spyglass toggle ships on both devices. The original's own Shift+S is
    /// unreachable (a binding is one control, and both halves are flight actions here), so the
    /// keyboard takes F2 for its camera 2 and the pad takes Misc1, the one control no other flight
    /// action holds. Recorded so a later edit cannot quietly leave a pad-only pilot without it.
    /// </summary>
    [Fact]
    public void TheSpyglassToggle_ShipsOnBothDevices()
    {
        var map = DefaultBindings.MapFor(InputContext.Flight, Pad);
        var bound = map.Bindings(InputAction.ToggleSpyglass);

        Assert.Equal(InputContext.Flight, DefaultBindings.ContextOf(InputAction.ToggleSpyglass));
        Assert.Contains(new Binding(DeviceId.Keyboard, BindingControl.Key((int)Key.F2)), bound);
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
}
