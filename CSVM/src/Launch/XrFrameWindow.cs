namespace CSVM.Launch;

/// <summary>One closed window of headset frames: how many, and their mean and worst wall interval.
/// <c>Late</c> counts the frames over one and a half refresh periods, <c>Missed</c> the refreshes
/// they skipped.</summary>
public readonly record struct XrFrameSummary(int Frames, double MeanMs, double MaxMs, int Late, int Missed,
    double RefreshHz);

/// <summary>
/// The headset's frame pacing, folded into wall-time windows from the main loop's own frame
/// intervals. Under OpenXR the main loop waits on the runtime's frame call. An interval longer
/// than the display period is then a refresh the headset showed without a new frame. Engine-free, so
/// the arithmetic unit-tests; <see cref="XrSession"/> feeds it and logs each window.
/// </summary>
public sealed class XrFrameWindow
{
    // An interval this many refresh periods long or more is a late frame. Half a period of slack
    // keeps the runtime's own scheduling jitter out of the count.
    private const double LateFactor = 1.5;

    private readonly double _windowMs;
    private double _periodMs;
    private int _frames;
    private double _sumMs;
    private double _maxMs;
    private int _late;
    private int _missed;

    /// <summary>A window closes once its frames span <paramref name="windowSeconds"/> of wall
    /// time, so a collapsing frame rate still reports on time.</summary>
    public XrFrameWindow(double windowSeconds = 1.0)
    {
        _windowMs = windowSeconds * 1000.0;
    }

    /// <summary>The display refresh the late and missed counts are judged against. Zero or less
    /// means unknown, and both counts then stay at zero.</summary>
    public double RefreshHz { get; private set; }

    /// <summary>The last window <see cref="Add"/> closed.</summary>
    public XrFrameSummary Last { get; private set; }

    /// <summary>Sets the refresh rate; the runtime may change it mid-session.</summary>
    public void SetRefreshHz(double hz)
    {
        RefreshHz = hz;
        _periodMs = hz > 0.0 ? 1000.0 / hz : 0.0;
    }

    /// <summary>Folds in one frame's wall interval. True when it closed the window, which is then
    /// <see cref="Last"/>; the next frame opens a new one.</summary>
    public bool Add(double frameMs)
    {
        _frames++;
        _sumMs += frameMs;
        if (frameMs > _maxMs)
        {
            _maxMs = frameMs;
        }

        if (_periodMs > 0.0 && frameMs >= LateFactor * _periodMs)
        {
            _late++;
            _missed += (int)System.Math.Round(frameMs / _periodMs) - 1;
        }

        if (_sumMs < _windowMs)
        {
            return false;
        }

        Last = new XrFrameSummary(_frames, _sumMs / _frames, _maxMs, _late, _missed, RefreshHz);
        _frames = 0;
        _sumMs = 0.0;
        _maxMs = 0.0;
        _late = 0;
        _missed = 0;
        return true;
    }
}
