namespace CSVM.Utils;

/// <summary>What one frame of a <see cref="TapHoldButton"/> decided.</summary>
public enum TapHold
{
    /// <summary>Nothing to do this frame.</summary>
    None,

    /// <summary>The button crossed the hold threshold while still down. Fires ONCE per press, on
    /// the frame it crosses, not on release.</summary>
    Hold,

    /// <summary>The button was released before the threshold.</summary>
    Tap,
}

/// <summary>One button carrying two actions, split by how long it is held. Feed it the button's
/// LEVEL each frame and it edge-detects, times and classifies; the caller only switches on the
/// answer. Wraps <see cref="HoldToRepeat"/> with a zero repeat interval rather than hand-rolling a
/// second timer. Pure and engine-free, so the decoding unit-tests even though a gamepad does not.
/// ⚠ The tap resolves on RELEASE, never on press. Firing the tap on press and the hold later means
/// every long press begins by performing the wrong action and visibly flickers a wrong selection
/// before correcting itself.</summary>
public sealed class TapHoldButton(float holdSeconds)
{
    /// <summary>The threshold every pad control carrying two actions splits on: down longer than
    /// this is a hold, shorter is a tap. ⚠ TUNE, ours and not the original's, which needs no
    /// threshold at all because it gives each direction its own key.</summary>
    public const float PadHoldSeconds = 0.25f;

    private readonly HoldToRepeat _hold = new(holdSeconds, 0f);
    private bool _down;
    private bool _fired;

    /// <summary>Advances one frame. <paramref name="down"/> is the button's level, not an edge.</summary>
    public TapHold Step(bool down, float dt)
    {
        bool wasDown = _down;
        _down = down;
        if (down && !wasDown)
        {
            _hold.Press();
            _fired = false;
        }

        if (down)
        {
            if (!_fired && _hold.Tick(dt))
            {
                _fired = true;
                return TapHold.Hold;
            }

            return TapHold.None;
        }

        if (!wasDown)
        {
            return TapHold.None;   // never pressed: a button that is simply up reports nothing
        }

        // Released. A press whose hold already fired is spent, so it does NOT also tap.
        _hold.Release();
        bool tapped = !_fired;
        _fired = false;
        return tapped ? TapHold.Tap : TapHold.None;
    }
}
