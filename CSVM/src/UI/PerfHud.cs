using System.Globalization;
using Godot;

namespace CSVM.UI;

/// <summary>
/// The always-available frame-cost readout (key <b>F14</b>): frames per second, the current
/// frame's wall cost, and the worst frame in the last few seconds — a peak that spikes and
/// decays, so a hitch you felt leaves readable evidence on screen a moment later rather than
/// only an instantaneous number nobody was watching at the right instant
/// (PLAN-perf-hitches D10).
///
/// <para>Built once by <see cref="CSVM.Session.Launcher"/> — never per <c>GameSession</c> and
/// never per splitscreen pane, since fps/frame cost/GC are process-wide facts, not a per-pane
/// one — which is what makes it work at the launchscreen, in <c>--viewer</c>/<c>--freecam</c>
/// and in flight for free. Off by default and builds nothing until switched on, so the 11
/// golden screenshots stay byte-identical; <c>--debug-fps[=compact|full]</c> is the scripted
/// twin.</para>
///
/// <para>Fed the same raw <c>Stopwatch</c>-based frame cost <see cref="Utils.HitchMonitor"/>
/// ticks on, every frame, via <see cref="Tick"/> — never Godot's <c>delta</c>, for the reason
/// documented on that class. The current-cost term is fully unaveraged, matching the
/// instrument's own ethos: it is exactly this frame's cost, not a windowed mean that would bury
/// the hitch this readout exists to show.</para>
///
/// <para><b>Cycle: Off → Compact → Full → Off.</b> D10 lands Compact's actual content — the
/// fps/frame-cost/worst-frame trio above. Full is landed here as a distinct, selectable mode
/// (so <c>--debug-fps=full</c> parses and F14 cycles through it) but renders the same panel as
/// Compact for now; PLAN-perf-hitches D11 is what fills it with the per-frame split/count/
/// memory/GC terms, the breadcrumbs, and the rolling frame-time strip.</para>
/// </summary>
public sealed partial class PerfHud : Node
{
    // "A few seconds" per the Goal: long enough that a hitch you felt is still on screen a
    // moment later, short enough that the number still reads as "recent". UI cosmetic, not a
    // measurement threshold — a plain const, the same precedent as NodeLabels' RefreshInterval.
    private const double WorstHoldMs = 3000;

    // How often the label text is rebuilt. One short string either way, but there is no reason
    // to touch Label.Text sixty times a second for a number a human reads a few times a second.
    private const double RefreshIntervalMs = 150;

    // The font size this readout was sized at against HudMetrics.ReferenceHeight (1440p) — bigger
    // than the 12 px debug-overlay labels (NodeLabels/MarkerOverlay), since this one is meant to
    // stay legible at a glance during ordinary play, not just be read up close while debugging.
    private const float ReferenceFontSize = 26f;

    private CanvasLayer? _hudLayer;
    private Label? _hud;
    private Mode _mode = Mode.Off;
    private double _sinceRefreshMs;
    private double _lastFrameMs;
    private double _worstMs;
    private double _worstAgeMs;
    private bool _justRearmed;

    public PerfHud()
    {
        Name = "perf_hud";
    }

    public enum Mode { Off, Compact, Full }

    /// <summary>--debug-fps[=compact|full]: start switched on, for a scripted screenshot.</summary>
    public Mode InitialMode { get; init; } = Mode.Off;

    /// <summary>Parses the --debug-fps value. Absent value = Compact, the useful default.</summary>
    public static Mode ParseMode(string value) => value.Trim().ToLowerInvariant() switch
    {
        "full" => Mode.Full,
        "off" => Mode.Off,
        _ => Mode.Compact,
    };

    public override void _Ready()
    {
        if (InitialMode != Mode.Off)
            SetMode(InitialMode);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F14 })
        {
            SetMode(_mode switch
            {
                Mode.Off => Mode.Compact,
                Mode.Compact => Mode.Full,
                _ => Mode.Off,
            });
        }
    }

    /// <summary>Feeds one rendered frame's wall cost — the same raw value
    /// <see cref="Utils.HitchMonitor"/> ticks on, never Godot's <c>delta</c>. Runs
    /// unconditionally, Off or not: the worst-frame peak has to already be warm the instant
    /// someone presses F14, or the readout would have nothing to say about the hitch that made
    /// them look.</summary>
    public void Tick(double frameMs)
    {
        if (double.IsNaN(frameMs) || frameMs < 0)
            frameMs = 0;

        if (_justRearmed)
        {
            // The frame that closes over a session build/teardown is not a real hitch — the same
            // reason HitchMonitor drops its own baseline here. Seed the display only.
            _justRearmed = false;
            _lastFrameMs = frameMs;
            return;
        }

        _lastFrameMs = frameMs;
        if (frameMs >= _worstMs)
        {
            _worstMs = frameMs;
            _worstAgeMs = 0;
        }
        else
        {
            _worstAgeMs += frameMs;
            if (_worstAgeMs >= WorstHoldMs)
            {
                _worstMs = frameMs;
                _worstAgeMs = 0;
            }
        }

        if (_mode == Mode.Off)
            return;
        _sinceRefreshMs += frameMs;
        if (_sinceRefreshMs < RefreshIntervalMs)
            return;
        _sinceRefreshMs = 0;
        Refresh();
    }

    /// <summary>Drops the worst-frame peak and skips one frame of tracking — call alongside
    /// <see cref="Utils.HitchMonitor.Rearm"/> after anything that legitimately stalls the frame
    /// loop (a session build, a teardown), so that stall never reads as the worst recent
    /// frame.</summary>
    public void Rearm()
    {
        _worstMs = 0;
        _worstAgeMs = 0;
        _justRearmed = true;
    }

    private static string ModeLabel(Mode mode) => mode switch
    {
        Mode.Full => "full",
        _ => "compact",
    };

    private void SetMode(Mode mode)
    {
        _mode = mode;
        if (mode == Mode.Off)
        {
            if (_hudLayer != null)
                _hudLayer.Visible = false;
            return;
        }
        EnsureBuilt();
        _hudLayer!.Visible = true;
        _sinceRefreshMs = RefreshIntervalMs; // redraw on the next Tick rather than waiting out the interval
        Refresh();
        GD.Print($"[perf] fps readout: {_mode}");
    }

    /// <summary>Built lazily on first switch-on: a session that never presses F14 (or passes
    /// --debug-fps) adds no nodes at all, so nothing it renders can differ.</summary>
    private void EnsureBuilt()
    {
        if (_hudLayer != null)
            return;
        _hudLayer = new CanvasLayer { Layer = HudLayers.PerfReadout };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _hud = new Label { Text = "" };
        _hud.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _hud.OffsetLeft = 10;
        _hud.OffsetTop = 0;
        root.AddChild(_hud);
        _hudLayer.AddChild(root);
        AddChild(_hudLayer);
    }

    private void Refresh()
    {
        if (_hud == null)
            return;
        float scale = WindowScale();
        _hud.AddThemeFontSizeOverride("font_size", Mathf.Max(1, Mathf.RoundToInt(ReferenceFontSize * scale)));
        double fps = _lastFrameMs > 0 ? 1000.0 / _lastFrameMs : 0;
        _hud.Text = string.Create(CultureInfo.InvariantCulture,
            $"perf [F14]: {ModeLabel(_mode)} — {fps:0.0} fps  frame {_lastFrameMs:0.00} ms  worst {_worstMs:0.00} ms");
    }

    // HudMetrics.Scale damps by PaneFactor (sqrt of the pane's share of the window), which is
    // exactly wrong here: this readout draws once for the whole window regardless of splitscreen,
    // so it takes the plain window-height ratio instead (PLAN-perf-hitches D10's Approach).
    private float WindowScale()
    {
        float windowH = GetTree()?.Root?.Size.Y ?? Flight.HudMetrics.ReferenceHeight;
        return windowH / Flight.HudMetrics.ReferenceHeight;
    }
}
