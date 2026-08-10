using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.UI;

/// <summary>
/// The full-screen colour wash an <c>FBFX_COLOR_FROM_TO</c> event authors: a close HE, AP or flak
/// burst ramping the whole picture from one RGBA to another over the event's run time. One ramp at
/// a time, painted into every rendered view.
///
/// <para><b>One state, N views.</b> The original keeps a single frame-buffer-effect object
/// (<c>crimson.exe</c> 0x9c8a98) whose colour and alpha the handler simply overwrites, so a second
/// burst landing mid-wash replaces the first outright rather than compositing with it — that is the
/// composition rule here too (<see cref="Play"/> replaces whatever is running). The original is
/// single-view, so "the whole picture" is unambiguous there; in splitscreen each pane IS a rendered
/// picture, so the one ramp is painted into each pane's own viewport rather than across the window.
/// That keeps the 2 px gutters and the empty 3P quadrant — which are not part of any picture — out
/// of it, and it is the same shape the cloud whiteout already uses.</para>
///
/// <para><b>Under the HUD.</b> <see cref="HudLayers.WorldOverlay"/>, beside the whiteout: this is a
/// world-picture effect, and unlike the lens flare's sun wash there is no footage saying it whitens
/// the instruments. The launchscreen and the scoreboards sit at <see cref="HudLayers.Board"/> and
/// are unreachable by it.</para>
/// </summary>
public sealed partial class ScreenFlash : Node
{
    private readonly List<ColorRect> _rects = new();
    private readonly List<CanvasLayer> _layers = new();

    private Color _from;
    private Color _to;
    private float _runTime;
    private float _elapsed;
    private bool _running;

    /// <summary>The colour currently washed over the picture, or a fully transparent one when no
    /// ramp is running. Read by the <c>fbfx-flash</c> suite.</summary>
    public Color Current { get; private set; } = new(0f, 0f, 0f, 0f);

    /// <summary>Whether a ramp is running.</summary>
    public bool Running => _running;

    /// <summary>Builds one hidden overlay per rendered view. <paramref name="hudParents"/> is each
    /// rig's <c>HudParent</c> — the window root with one player, the pane's SubViewport with
    /// several.</summary>
    public static ScreenFlash Build(IEnumerable<Node> hudParents)
    {
        var flash = new ScreenFlash { Name = "screen_flash" };
        foreach (var parent in hudParents)
        {
            // Hidden until a ramp runs, so a session that never sees a close burst renders exactly
            // what it rendered before this existed.
            var canvas = new CanvasLayer
            {
                Layer = HudLayers.WorldOverlay,
                Name = "screen_flash",
                Visible = false,
            };
            var rect = new ColorRect
            {
                Color = new Color(0f, 0f, 0f, 0f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            canvas.AddChild(rect);
            parent.AddChild(canvas);
            flash._layers.Add(canvas);
            flash._rects.Add(rect);
        }
        return flash;
    }

    /// <summary>Starts a ramp, replacing whatever was running. <paramref name="runTime"/> is the
    /// event's authored run time; a non-positive one shows nothing.</summary>
    public void Play(Color from, Color to, float runTime)
    {
        if (runTime <= 0f)
            return;
        _from = from;
        _to = to;
        _runTime = runTime;
        _elapsed = 0f;
        _running = true;
        Apply(from);
    }

    public override void _Process(double delta)
    {
        if (!_running)
            return;
        // Sim time, so a halt freezes the wash and --det reproduces it frame for frame.
        _elapsed += GameClock.Current?.FrameDt ?? (float)delta;
        if (_elapsed >= _runTime)
        {
            // The original re-arms its frame-buffer effect for the current frame only, every tick
            // the handler runs; once the event completes nothing re-arms it and the wash is simply
            // gone. So the ramp does not hold its `to` colour — it ends.
            _running = false;
            Apply(new Color(0f, 0f, 0f, 0f));
            foreach (var layer in _layers)
                layer.Visible = false;
            return;
        }
        Apply(_from.Lerp(_to, _elapsed / _runTime));
    }

    private void Apply(Color c)
    {
        // Per-channel clamp, as the handler does before it packs the pixel: the reader's own
        // range assertions do not cover every channel, so an out-of-range author cannot bleed.
        c = c.Clamp();
        Current = c;
        bool visible = c.A > 0f;
        for (int i = 0; i < _rects.Count; i++)
        {
            _rects[i].Color = c;
            _layers[i].Visible = visible;
        }
    }
}
