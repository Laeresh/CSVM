namespace CSVM.Utils;

/// <summary>
/// Tap-vs-hold timing for a single button: a tap fires once (the caller acts on
/// <see cref="Press"/> itself), and holding past <c>initialDelay</c> makes <see cref="Tick"/>
/// start returning true every <c>repeatInterval</c> seconds until <see cref="Release"/>. A
/// zero <c>repeatInterval</c> fires every tick once the delay has elapsed, e.g. the pause
/// transport's "hold . to step every rendered frame", while a nonzero one is
/// <c>MenuInput</c>'s initial-delay/repeat-rate d-pad shape, which runs both of its cursor axes
/// through one of these via <c>MenuInput.StepAxis</c>.
/// </summary>
public sealed class HoldToRepeat(float initialDelay, float repeatInterval)
{
    private float _timer;
    private bool _held;

    /// <summary>Button went down this frame. The caller is responsible for its own immediate,
    /// once-per-press action; this only arms the repeat timer.</summary>
    public void Press()
    {
        _held = true;
        _timer = initialDelay;
    }

    /// <summary>Button went up: cancels any pending repeat so the next <see cref="Press"/>
    /// starts the delay fresh.</summary>
    public void Release()
    {
        _held = false;
    }

    /// <summary>Call once per frame while the button is held. Returns true on the frame a
    /// repeat should fire.</summary>
    public bool Tick(float dt)
    {
        if (!_held)
        {
            return false;
        }
        _timer -= dt;
        if (_timer > 0f)
        {
            return false;
        }
        _timer += repeatInterval;
        return true;
    }
}
