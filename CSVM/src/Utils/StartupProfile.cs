using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace CSVM.Utils;

/// <summary>
/// The always-on startup timing report: one <c>[perf] startup …</c> line per session build,
/// split into the phases the build spends its time in. It only <i>reports</i> — no thresholds,
/// no verdicts, no comparisons; a comparison needs a warm-up protocol this class deliberately
/// does not own.
///
/// <para><b>The identity the line asserts</b> is
/// <c>total = boot + Σ(phases) + rest + first_frame</c>, so the numbers can be checked against
/// each other and against the process's own wall time:</para>
/// <list type="bullet">
/// <item><c>boot</c> — engine start → the session build starting: Godot's own init, arg parsing,
/// shader-global registration, lighting, the pad roster. On a session built from the
/// launchscreen it also contains however long the menu was up, which is why it is printed
/// separately rather than folded into <c>total</c>'s meaning.</item>
/// <item>the phases — see the vocabulary below; each is a leaf, never nested inside another, so
/// they sum without double counting.</item>
/// <item><c>rest</c> — the session build minus its phases: everything not carved out into a
/// phase. Not an error term; it is real work with no stopwatch on it.</item>
/// <item><c>first_frame</c> — the build ending → the first frame being on screen (measured at
/// the top of the second <c>_Process</c>, so the first draw, and the shader compilation in it,
/// falls inside). A run that quits during the build never renders and prints
/// <c>first_frame=none</c>; its build is closed at teardown instead, so its <c>rest</c> also
/// carries whatever the probe did after the world was up.</item>
/// </list>
///
/// <para><b>Phase vocabulary</b> (the keys a parser may rely on; a phase absent from a mode is
/// simply absent from the line): <c>gamez</c> · <c>textures</c> · <c>sounds</c> · <c>zrdr</c>
/// (every reader/interp JSON load) · <c>world</c> (WorldBuilder) · <c>clutter</c> ·
/// <c>anim</c> (AnimProgram load) · <c>bind</c> (AnimRuntime bind + bootstrap) ·
/// <c>prewarm</c> (sound decode) · <c>plane</c> (aircraft build + paint) · <c>weather</c>
/// (skydome + fog + cloud visuals) · <c>edge</c> (map-edge extender).</para>
///
/// <para>Measurement is <see cref="Stopwatch.GetTimestamp"/> pairs — two QPC reads per phase, a
/// couple of dozen per session — which is why this runs unconditionally rather than behind a
/// flag.</para>
/// </summary>
public sealed class StartupProfile
{
    /// <summary>The session currently being timed, or null when nothing is. The in-engine test
    /// harness builds worlds through the very same code and deliberately leaves this null, so its
    /// eight census worlds do not accumulate into one nonsense line.</summary>
    public static StartupProfile? Current { get; set; }

    private readonly List<string> _order = new();
    private readonly Dictionary<string, double> _phases = new(StringComparer.Ordinal);
    private readonly long _buildStart = Stopwatch.GetTimestamp();
    private readonly double _bootMs;
    private readonly string _mode;

    private long _buildEnd;
    private double _buildMs = -1;
    private double _firstFrameMs = -1;
    private int _framesSinceBuild;
    private bool _emitted;

    /// <param name="mode">The session shape (<c>fly</c>, <c>freecam</c>, …) — the same token the
    /// log file is named after.</param>
    /// <param name="bootMs">Milliseconds from engine start to this constructor.</param>
    public StartupProfile(string mode, double bootMs)
    {
        _mode = mode;
        _bootMs = bootMs;
    }

    /// <summary>What this session built, as one ready-formatted <c>key=value</c> fragment
    /// (<c>chapter=C1</c>, <c>plane=player_bhawk</c>) — the line's scenario identity.</summary>
    public string Subject { get; set; } = "";

    /// <summary>Opens a phase measurement; hand the returned mark to <see cref="Record"/>.</summary>
    public static long Mark() => Stopwatch.GetTimestamp();

    /// <summary>Adds the time since <paramref name="mark"/> to a phase of the session being timed.
    /// A no-op when no session is under measurement, so the shared build code carries the calls
    /// unconditionally. Repeated calls with the same name accumulate — the three
    /// <c>GameZ.Load</c>s of a flight session are one <c>gamez</c> figure.</summary>
    public static void Record(string phase, long mark)
    {
        Current?.Add(phase, MsSince(mark));
    }

    /// <summary>Adds an already-measured span to a phase.</summary>
    public void Add(string phase, double ms)
    {
        if (!_phases.ContainsKey(phase))
        {
            _order.Add(phase);
            _phases[phase] = 0;
        }
        _phases[phase] += ms;
    }

    /// <summary>Closes the session build; everything after this counts towards
    /// <c>first_frame</c>. Idempotent — the first call wins, so a build that returns through
    /// several paths cannot restart the clock.</summary>
    public void EndBuild()
    {
        if (_buildMs >= 0)
        {
            return;
        }
        _buildEnd = Stopwatch.GetTimestamp();
        _buildMs = MsBetween(_buildStart, _buildEnd);
    }

    /// <summary>Call once per rendered frame. The second frame after the build is the one that
    /// proves the first has been drawn, so that is where the line is emitted.</summary>
    public void Frame()
    {
        if (_emitted || _buildMs < 0)
        {
            return;
        }
        _framesSinceBuild++;
        if (_framesSinceBuild < 2)
        {
            return;
        }
        _firstFrameMs = MsSince(_buildEnd);
        Emit();
    }

    /// <summary>Writes the line, once. Called for its own sake only by the teardown path, which
    /// catches the runs that quit inside the build and never render a frame.</summary>
    public void Emit()
    {
        if (_emitted)
        {
            return;
        }
        _emitted = true;
        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }
        EndBuild();
        Log.Info("perf", $"startup {Format()}");
    }

    /// <summary>The line body — every <c>key=value</c> after <c>startup</c>. Pure and invariant
    /// (no Godot API, no current culture), so it is readable from a test host and a decimal point
    /// never turns into a field separator.</summary>
    public string Format()
    {
        double phaseSum = 0;
        foreach (double ms in _phases.Values)
        {
            phaseSum += ms;
        }
        double build = _buildMs < 0 ? 0 : _buildMs;
        double total = _bootMs + build + Math.Max(_firstFrameMs, 0);

        var sb = new StringBuilder();
        sb.Append("mode=").Append(_mode);
        if (Subject.Length > 0)
        {
            sb.Append(' ').Append(Subject);
        }
        Append(sb, "total", total);
        Append(sb, "boot", _bootMs);
        foreach (string phase in _order)
        {
            Append(sb, phase, _phases[phase]);
        }
        Append(sb, "rest", build - phaseSum);
        sb.Append(" first_frame=");
        if (_firstFrameMs < 0)
        {
            sb.Append("none");
        }
        else
        {
            sb.Append(Ms(_firstFrameMs));
        }
        return sb.ToString();
    }

    private static void Append(StringBuilder sb, string key, double ms)
    {
        sb.Append(' ').Append(key).Append('=').Append(Ms(ms));
    }

    private static string Ms(double ms) => ms.ToString("0.0", CultureInfo.InvariantCulture);

    private static double MsSince(long mark) => MsBetween(mark, Stopwatch.GetTimestamp());

    private static double MsBetween(long from, long to) => (to - from) * 1000.0 / Stopwatch.Frequency;
}
