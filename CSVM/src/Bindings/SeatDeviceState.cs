using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Bindings;

/// <summary>This tick's hardware for one seat: the keyboard and the mouse as the platform reports
/// them, plus whichever pads the seat currently holds behind a single placeholder identity. A seat
/// reads a SET of pads while a binding names one device, so pad defaults are authored on a
/// placeholder (<see cref="DefaultBindings.AnyPad"/> and its per-site twins) and this answers for
/// that identity alone, ORing buttons and taking the largest-magnitude axis reading across the set.
/// ⚠ Pad reads go through <see cref="Pads.For"/>, never through a connection index from the device
/// registry. That gate is what <c>--no-pads</c> and an unfocused window act on, and it carries the
/// phantom-device policy (span the set, never <c>pads[0]</c>); a registry index would lose both and
/// would pin the seat to one pad.</summary>
public sealed class SeatDeviceState : IDeviceState
{
    private readonly DeviceId _seatPad;
    private readonly Func<int[]?> _seatDevices;
    private readonly bool _readsPads;
    private readonly List<int> _pads = new();

    /// <summary>A seat reading <paramref name="seatPad"/>'s bindings off the pads
    /// <paramref name="seatDevices"/> names, re-asked each <see cref="Refresh"/> because a seat's
    /// device list changes when a splitscreen player joins. Pass <paramref name="readsPads"/> false
    /// for the keyboard-half reader of a site that resolves both halves separately.</summary>
    public SeatDeviceState(DeviceId seatPad, Func<int[]?> seatDevices, bool readsPads = true)
    {
        _seatPad = seatPad;
        _seatDevices = seatDevices ?? throw new ArgumentNullException(nameof(seatDevices));
        _readsPads = readsPads;
    }

    /// <summary>Takes this tick's pad list once, before anything resolves. <see cref="Pads.For"/>
    /// re-reads the connected roster on every call, and a tick would otherwise ask it once per pad
    /// binding rather than once.</summary>
    public void Refresh()
    {
        _pads.Clear();
        if (!_readsPads)
            return;
        foreach (int pad in Pads.For(_seatDevices()))
        {
            _pads.Add(pad);
        }
    }

    // The keyboard and the mouse are read ungated here, unlike the pads. Godot polls joypads
    // regardless of window focus while key state is focus-scoped, which is why --no-pads exists with
    // no keyboard counterpart; a seat that must not read them mutes them on PlayerActions instead.
    public bool IsKeyDown(DeviceId device, int keyCode) =>
        device.Kind == DeviceKind.Keyboard && Input.IsKeyPressed((Key)keyCode);

    public bool IsMouseButtonDown(DeviceId device, int button) =>
        device.Kind == DeviceKind.Mouse && Input.IsMouseButtonPressed((MouseButton)button);

    public bool IsButtonDown(DeviceId device, int button)
    {
        if (device != _seatPad)
            return false;
        foreach (int pad in _pads)
        {
            if (Input.IsJoyButtonPressed(pad, (JoyButton)button))
                return true;
        }

        return false;
    }

    /// <summary>The largest-magnitude reading across this seat's pads, so an idle phantom device
    /// reads about zero and never masks the real stick.</summary>
    public float AxisValue(DeviceId device, int axis)
    {
        if (device != _seatPad)
            return 0f;
        float best = 0f;
        foreach (int pad in _pads)
        {
            float value = Input.GetJoyAxis(pad, (JoyAxis)axis);
            if (Mathf.Abs(value) > Mathf.Abs(best))
                best = value;
        }

        return best;
    }

    /// <summary>Always nothing: no default authors a hat and <see cref="BindingStore"/> rejects the
    /// token, because Godot reports a d-pad as four buttons (<see cref="DefaultBindings"/>).
    /// </summary>
    public HatDirection HatState(DeviceId device, int hat) => HatDirection.None;
}
