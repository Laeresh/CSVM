using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using Godot;

namespace CSVM.UI;

/// <summary>
/// The animation debugger (<c>--anim-lab</c>): the chapter world as a <b>quiet stage</b> — reset
/// states and mission setup applied, nothing playing — with def playback under transport
/// controls, on a deterministic fixed-dt clock (Wave 3), plus a filterable def <b>picker</b> and
/// an authored-vs-fired <b>timeline</b> (Wave 4, <see cref="AnimTimeline"/>).
///
/// <para>The session is built by <see cref="WorldSession"/> with <c>AutoStart=false</c> and a
/// pinned RNG seed, and the runtime's own <c>_Process</c> is disabled: this node owns the clock
/// and feeds <see cref="AnimRuntime.Advance"/> in exact 1/60 s steps, so pause = no calls, step
/// = one call, slow-mo = a scaled accumulator, and the render rate never changes simulation
/// results. In a scripted run (<c>--screenshot</c>) wall time is ignored entirely — every frame
/// advances exactly <c>timeScale × 1</c> step, so a <c>--frames</c>/<c>--shots</c> capture lands
/// on exact step counts. The RNG is re-pinned on every Play/Restart, so a seeded replay rolls
/// the same dice.</para>
///
/// <para>Transport keys: Space pause/resume · <c>.</c> step one frame (pauses first) · R restart
/// · S stop · A start ambient playback (ON_STARTUP defs + startanims; one-way in v1) · 1/2/3
/// time scale 0.1×/0.25×/1× · P toggle the picker. <c>--play-anim=&lt;name&gt;</c> plays a def
/// at launch and auto-frames the orbit camera on its anchor (skipped when
/// <c>--campos</c>/<c>--lookat</c> placed the camera by hand).</para>
///
/// <para>The picker and timeline are fed straight from the runtime's <c>OnEventDispatched</c> /
/// <c>OnInstanceStarted</c> / <c>OnInstanceFinished</c> hooks (Wave 2 B4). They are the whole
/// interactive UI, hidden in a scripted <c>--screenshot</c> so those shots stay byte-identical;
/// <c>--debug-anim-ui</c> forces them visible in a screenshot (the timeline-verification path,
/// like <c>--debug-livery</c>). The hooks are attached only when the UI is shown, so a plain
/// scripted run pays nothing for them.</para>
///
/// <para>Accepted v1 limits (the plan's Traps): pausing pauses the RUNTIME, not the renderer —
/// shader-time effects (UV scroll, GPU precipitation), the texture cycler, puffer sprite
/// animation and already-playing audio streams keep going. Determinism's boundary is the
/// runtime, so screenshot comparisons should frame runtime-driven subjects (doors, vehicles),
/// not water or puffers. The timeline tracks one instance of the played def (the first anchor)
/// plus its CALL_ANIMATION children; sibling defs sharing an ANIMATION_NAME and children started
/// after the A ambient toggle are not tracked.</para>
/// </summary>
public sealed partial class AnimLab : Node
{
    /// <summary>The simulation step: the runtime advances in exact 1/60 s steps whatever the
    /// render rate, so a playback's dispatch times are a function of the step count alone.</summary>
    public const float FixedDt = 1f / 60f;

    /// <summary>The pinned default RNG seed (<c>--seed=N</c> overrides). Any constant works;
    /// what matters is that every launch shares it, so two runs replay the same dice.</summary>
    public const int DefaultSeed = 1;

    // A hitch must not unwind as a burst of catch-up steps: a quarter second (15 steps) keeps
    // slow frames honest without turning a debugger breakpoint stall into fast-forward.
    private const float MaxAccum = 0.25f;

    private readonly AnimRuntime _runtime;
    private readonly AnimProgram _program;
    private readonly OrbitCamera _orbit;
    private readonly int _seed;
    private readonly string? _playOnLaunch;   // --play-anim=<name>
    private readonly bool _autoFrame;         // no --campos/--lookat: frame the played def
    private readonly bool _fixedFrameStep;    // --screenshot: exactly timeScale×1 step per frame
    // The session archives, kept open for the whole lab session (WorldSession.Options
    // .KeepArchivesOpen) so puffers/decals can be built at any playhead time. The lab owns
    // their disposal — every other mode closes them when the build scope ends.
    private readonly TextureArchive _textures;
    private readonly SoundArchive? _sounds;

    private Label? _status;
    private AnimTimeline? _timeline;
    private LineEdit? _filter;
    private ItemList? _list;
    private Control? _pickerPanel;
    // ItemList row → index into _program.Defs (the filtered view).
    private readonly List<int> _rows = new();

    private bool _launched;      // first-frame housekeeping (the --play-anim auto-play) done
    private bool _paused;
    private float _timeScale = 1f;
    private float _accum;
    private int _steps;          // playhead = steps × FixedDt, zeroed on Play/Restart
    private string? _defName;    // the selected def (null until --play-anim / Play)
    private int _instances;      // how many (def, anchor) instances the last Play started
    private bool _stopped;       // S: _defName kept for the status line, playback torn down
    private bool _ambient;       // A pressed: ambient passes started

    // Timeline tracking. Set up BEFORE AnimRuntime.Play, so the def's t=0 dispatches (which fire
    // synchronously inside Play) are attributed to the right lane.
    private AnimDefinition? _timelineDef;  // the specific def the timeline focuses on
    private Node3D? _timelineAnchor;       // captured from the first instance-start of _timelineDef
    private bool _anchorCaptured;
    private bool _trackChildren;           // CALL_ANIMATION children tracked (off once ambient runs)

    /// <summary>Show the interactive UI — status line, picker and timeline. On by default; off
    /// for a scripted <c>--screenshot</c> (kept byte-identical) unless <c>--debug-anim-ui</c>
    /// forces it on. When off, the runtime hooks are never attached.</summary>
    public bool ShowUi = true;

    public AnimLab(AnimRuntime runtime, AnimProgram program, OrbitCamera orbit,
        TextureArchive textures, SoundArchive? sounds,
        int seed, string? playAnim, bool autoFrame, bool fixedFrameStep)
    {
        _runtime = runtime;
        _program = program;
        _orbit = orbit;
        _textures = textures;
        _sounds = sounds;
        _seed = seed;
        _playOnLaunch = playAnim;
        _autoFrame = autoFrame;
        _fixedFrameStep = fixedFrameStep;
    }

    public override void _Ready()
    {
        if (!ShowUi)
        {
            return;
        }
        BuildUi();
        // Feed the picker/timeline from the runtime, not from --debug-anim log text. Attached
        // here (only when the UI is shown) so a plain scripted run leaves them null and the
        // runtime's null-conditional dispatch stays zero-cost.
        _runtime.OnEventDispatched = OnDispatch;
        _runtime.OnInstanceStarted = OnInstanceStarted;
        _runtime.OnInstanceFinished = OnInstanceFinished;
    }

    public override void _ExitTree()
    {
        // The lab owns the session archives (kept open so effects can build mid-session); the
        // session teardown or quit is when they finally close.
        _textures.Dispose();
        _sounds?.Dispose();
    }

    public override void _Process(double delta)
    {
        if (!_launched)
        {
            // Deferred to the first frame rather than done at construction, so the auto-frame
            // wins over StartSession's whole-world FrameCamera and the play start is
            // frame-exact for a scripted --frames/--shots run.
            _launched = true;
            if (_playOnLaunch != null)
            {
                Play(_playOnLaunch);
            }
        }
        if (!_paused)
        {
            // Scripted runs substitute the fixed step for wall time, so frame N is always step
            // N (at 1×) and a capture lands on exact step counts, run after run.
            _accum += (_fixedFrameStep ? FixedDt : (float)delta) * _timeScale;
            _accum = Mathf.Min(_accum, MaxAccum);
            while (_accum >= FixedDt)
            {
                _accum -= FixedDt;
                Step();
            }
        }
        _timeline?.SetPlayhead(PlayheadTime);
        UpdateStatus();
    }

    private float PlayheadTime => _steps * FixedDt;

    private void Step()
    {
        // _steps is bumped BEFORE Advance so that during the runtime's dispatch hooks the
        // playhead (_steps × dt) equals the runner's clock — which is incremented at the top of
        // Advance — and a fired mark lands at the time it actually fired. The t=0 events fired
        // by AnimRuntime.Play use dt=0 and see _steps=0, so they stamp at t=0, matching.
        _steps++;
        _runtime.Advance(FixedDt);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }
        switch (key.Keycode)
        {
            case Key.Space:
                _paused = !_paused;
                break;
            case Key.Period:
                // Stepping is a paused-state concept; pressing it while playing pauses first,
                // so one key reaches "frozen, advance one frame" from any state.
                if (_paused)
                {
                    Step();
                }
                else
                {
                    _paused = true;
                }
                break;
            case Key.R:
                Restart();
                break;
            case Key.S:
                StopPlayback();
                break;
            case Key.A:
                _runtime.StartAmbient();
                _ambient = true;
                // Ambient defs start their own instances; stop attributing new instance-starts
                // to the played def's CALL_ANIMATION cascade so they don't pollute the timeline.
                _trackChildren = false;
                break;
            case Key.Key1:
                _timeScale = 0.1f;
                break;
            case Key.Key2:
                _timeScale = 0.25f;
                break;
            case Key.Key3:
                _timeScale = 1f;
                break;
            case Key.P:
                if (_pickerPanel != null)
                {
                    _pickerPanel.Visible = !_pickerPanel.Visible;
                }
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Plays one def by ANIMATION_NAME the way a startanim starts (RESET_STATE, then
    /// Start per anchor — <see cref="AnimRuntime.Play"/>), from a re-pinned RNG and a zeroed
    /// playhead. The picker's overload focuses the timeline on the exact def picked; this entry
    /// (also <c>--play-anim</c>) focuses on the first def carrying the name.</summary>
    public void Play(string name) => Play(name, _program.ByAnimName(name).FirstOrDefault());

    private void Play(string name, AnimDefinition? focus)
    {
        if (_defName != null)
        {
            _runtime.Stop(_defName);
        }
        _runtime.Reseed();
        _steps = 0;
        _accum = 0f;
        _paused = false;
        // Set the timeline focus BEFORE Start: AnimRuntime.Play fires the def's t=0 events
        // synchronously, and the dispatch hooks need _timelineDef already in place to attribute
        // them. The anchor is captured from the first instance-start (which precedes any dispatch).
        _timelineDef = focus;
        _timelineAnchor = null;
        _anchorCaptured = false;
        _trackChildren = !_ambient;
        _timeline?.SetDef(_program, _timelineDef, "");
        var started = _runtime.Play(name);
        if (started.Count == 0)
        {
            GD.Print($"anim-lab: no definition named '{name}' among this program's " +
                     $"{_program.Defs.Count} defs — check --chapter/--mission (P lists them)");
            _timeline?.Clear();
            return;
        }
        _defName = name;
        _stopped = false;
        _instances = started.Count;
        GD.Print($"anim-lab: playing '{name}' — {started.Count} instance(s), seed {_seed}");
        if (_autoFrame)
        {
            FrameOn(started);
        }
    }

    /// <summary>Restart = <see cref="AnimRuntime.Stop"/> (tear down the def's live
    /// motions/puffers/lights/sounds) → re-pin the RNG → re-apply RESET_STATE → Start, playhead
    /// back at step 0 — the visually-identical replay the fixed clock + seed exist for. Keeps
    /// the same timeline focus def as the last Play.</summary>
    private void Restart()
    {
        if (_defName == null)
        {
            return;
        }
        Play(_defName, _timelineDef);
    }

    private void StopPlayback()
    {
        if (_defName == null || _stopped)
        {
            return;
        }
        _runtime.Stop(_defName);
        _stopped = true;
        GD.Print($"anim-lab: stopped '{_defName}'");
    }

    // ---- runtime → picker/timeline hooks -----------------------------------------------------

    private void OnDispatch(AnimRuntime.EventDispatch e)
    {
        if (_timelineDef == null)
        {
            return;
        }
        float t = PlayheadTime;
        if (ReferenceEquals(e.Def, _timelineDef)
            && (!_anchorCaptured || ReferenceEquals(e.Anchor, _timelineAnchor)))
        {
            _timeline?.AddPrimaryMark(e.Sequence, e.EventIndex, t);
            return;
        }
        // A sibling def sharing the ANIMATION_NAME is another template instance, not a child;
        // and once ambient is running its instances are not the played def's cascade.
        if (!_trackChildren || SameAnimName(e.Def, _timelineDef))
        {
            return;
        }
        _timeline?.AddChildMark(e.Def, e.Anchor, e.Sequence, e.EventIndex, t);
    }

    private void OnInstanceStarted(AnimDefinition def, Node3D? anchor)
    {
        if (_timelineDef == null)
        {
            return;
        }
        if (ReferenceEquals(def, _timelineDef))
        {
            if (!_anchorCaptured)
            {
                _timelineAnchor = anchor;
                _anchorCaptured = true;
                _timeline?.SetAnchorLabel(AnchorLabel(anchor));
            }
            return;
        }
        if (!_trackChildren || SameAnimName(def, _timelineDef))
        {
            return;
        }
        _timeline?.AddChild(def, anchor, PlayheadTime);
    }

    private void OnInstanceFinished(AnimDefinition def, Node3D? anchor)
    {
        if (_timelineDef == null)
        {
            return;
        }
        if (ReferenceEquals(def, _timelineDef) && ReferenceEquals(anchor, _timelineAnchor))
        {
            _timeline?.MarkPrimaryFinished();
        }
        else if (!SameAnimName(def, _timelineDef))
        {
            _timeline?.MarkChildFinished(def, anchor);
        }
    }

    private static bool SameAnimName(AnimDefinition a, AnimDefinition b) =>
        !string.IsNullOrEmpty(a.AnimName)
        && string.Equals(a.AnimName, b.AnimName, StringComparison.OrdinalIgnoreCase);

    private static string AnchorLabel(Node3D? anchor)
    {
        if (anchor == null)
        {
            return "(global)";
        }
        return anchor.HasMeta(AnimRuntime.NameMeta)
            ? anchor.GetMeta(AnimRuntime.NameMeta).AsString()
            : anchor.Name.ToString();
    }

    /// <summary>Frames the orbit camera on the played def: the first started instance whose
    /// anchor (or first resolved target node — <see cref="AnimRuntime.FrameTarget"/>) exists. A
    /// mesh-less target (an empty pivot/group node) gets a nominal 20 m box around its position,
    /// because a zero AABB would orbit the world origin instead.</summary>
    private void FrameOn(List<(AnimDefinition Def, Node3D? Anchor)> started)
    {
        foreach (var (def, anchor) in started)
        {
            if (_runtime.FrameTarget(def, anchor) is not { } node)
            {
                continue;
            }
            var aabb = OrbitCamera.MergedAabb(node);
            if (aabb.Size.LengthSquared() < 1e-4f)
            {
                aabb = new Aabb(node.GlobalPosition - Vector3.One * 10f, Vector3.One * 20f);
            }
            _orbit.Frame(aabb, null, null);
            return;
        }
        GD.Print($"anim-lab: '{_defName}' resolves no frameable node — camera left as-is");
    }

    // ---- UI ----------------------------------------------------------------------------------

    private void BuildUi()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        _status = new Label
        {
            Position = new Vector2(12, 12),
            Modulate = new Color(0.75f, 0.9f, 1f),
        };
        _status.AddThemeFontSizeOverride("font_size", 14);
        layer.AddChild(_status);

        BuildPicker(layer);
        BuildTimeline(layer);
    }

    private void BuildPicker(CanvasLayer layer)
    {
        var panel = new PanelContainer { SelfModulate = new Color(1, 1, 1, 0.85f) };
        panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        panel.Position = new Vector2(12, 56);
        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 8);
        }
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);

        var title = new Label { Text = $"DEF PICKER  ({_program.Defs.Count} defs)" };
        box.AddChild(title);
        box.AddChild(Small("type to filter · Enter plays · P hides"));

        _filter = new LineEdit
        {
            PlaceholderText = "filter (name / activation / anchor)…",
            CustomMinimumSize = new Vector2(320, 0),
        };
        _filter.TextChanged += _ => RefreshList();
        _filter.TextSubmitted += _ => PlayFirstFiltered();
        box.AddChild(_filter);

        _list = new ItemList { CustomMinimumSize = new Vector2(320, 360) };
        _list.ItemActivated += index => PlayRow((int)index);
        box.AddChild(_list);

        margin.AddChild(box);
        panel.AddChild(margin);
        layer.AddChild(panel);
        _pickerPanel = panel;
        RefreshList();
    }

    private void BuildTimeline(CanvasLayer layer)
    {
        _timeline = new AnimTimeline();
        // A bottom strip spanning the full width. Height fixed; lanes past it are clipped, which
        // is fine — the played defs of interest have a handful of sequences.
        _timeline.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _timeline.OffsetTop = -210;
        _timeline.OffsetBottom = 0;
        _timeline.OffsetLeft = 0;
        _timeline.OffsetRight = 0;
        layer.AddChild(_timeline);
    }

    // Rebuilds the ItemList from the filter text. Matches a case-insensitive substring against
    // the anim name, the activation and the anchor name — the three columns each row shows.
    private void RefreshList()
    {
        if (_list == null)
        {
            return;
        }
        string q = _filter?.Text.Trim() ?? "";
        _list.Clear();
        _rows.Clear();
        for (int i = 0; i < _program.Defs.Count; i++)
        {
            var d = _program.Defs[i];
            string anim = d.AnimName ?? "";
            string row = $"{(anim.Length > 0 ? anim : "(no anim-name)")}   ·  {d.Activation}   ·  @{(d.Name.Length > 0 ? d.Name : "—")}";
            if (q.Length > 0
                && !anim.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !d.Activation.Contains(q, StringComparison.OrdinalIgnoreCase)
                && !d.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            int idx = _list.AddItem(row);
            // A def with no ANIMATION_NAME can't be started through Play — dim it and note why.
            if (anim.Length == 0)
            {
                _list.SetItemDisabled(idx, true);
                _list.SetItemCustomFgColor(idx, new Color(0.5f, 0.5f, 0.55f));
            }
            _rows.Add(i);
        }
    }

    private void PlayFirstFiltered()
    {
        for (int row = 0; row < _rows.Count; row++)
        {
            if (!string.IsNullOrEmpty(_program.Defs[_rows[row]].AnimName))
            {
                PlayRow(row);
                return;
            }
        }
    }

    private void PlayRow(int row)
    {
        if (row < 0 || row >= _rows.Count)
        {
            return;
        }
        var def = _program.Defs[_rows[row]];
        if (string.IsNullOrEmpty(def.AnimName))
        {
            GD.Print($"anim-lab: '{def.Name}' has no ANIMATION_NAME — cannot play it directly");
            return;
        }
        Play(def.AnimName!, def);
        // Drop focus off the filter/list so the transport keys (Space/R/S/…) go to the lab, not
        // into the LineEdit, the moment a def is picked.
        GetViewport().GuiReleaseFocus();
    }

    private void UpdateStatus()
    {
        if (_status == null)
        {
            return;
        }
        string def = _defName == null ? "(none — pick one or --play-anim=<name>)"
            : _stopped ? $"{_defName} (stopped)"
            : $"{_defName} ({_instances} instance{(_instances == 1 ? "" : "s")})";
        _status.Text =
            $"anim-lab  t {_steps * FixedDt:0.00} s (step {_steps})  {_timeScale:0.##}×  " +
            $"{(_paused ? "PAUSED" : "PLAYING")}   seed {_seed}   " +
            $"ambient {(_ambient ? "on" : "off")}   def: {def}\n" +
            "[Space pause · . step · R restart · S stop · A ambient · 1/2/3 speed · P picker]";
    }

    private static Label Small(string text)
    {
        var label = new Label { Text = text, Modulate = new Color(1, 1, 1, 0.65f) };
        label.AddThemeFontSizeOverride("font_size", 11);
        return label;
    }
}
