using System.Collections.Generic;
using CSVM.Mech3;
using Godot;

namespace CSVM.UI;

/// <summary>
/// The animation debugger (<c>--anim-lab</c>, PLAN-anim-debugger Wave 3): the chapter world as
/// a <b>quiet stage</b> — reset states and mission setup applied, nothing playing — with def
/// playback under transport controls, on a deterministic fixed-dt clock.
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
/// time scale 0.1×/0.25×/1×. <c>--play-anim=&lt;name&gt;</c> plays a def at launch and
/// auto-frames the orbit camera on its anchor (skipped when <c>--campos</c>/<c>--lookat</c>
/// placed the camera by hand).</para>
///
/// <para>Accepted v1 limits (the plan's Traps): pausing pauses the RUNTIME, not the renderer —
/// shader-time effects (UV scroll, GPU precipitation), the texture cycler, puffer sprite
/// animation and already-playing audio streams keep going. Determinism's boundary is the
/// runtime, so screenshot comparisons should frame runtime-driven subjects (doors, vehicles),
/// not water or puffers.</para>
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
    private bool _launched;      // first-frame housekeeping (the --play-anim auto-play) done
    private bool _paused;
    private float _timeScale = 1f;
    private float _accum;
    private int _steps;          // playhead = steps × FixedDt, zeroed on Play/Restart
    private string? _defName;    // the selected def (null until --play-anim / Play)
    private int _instances;      // how many (def, anchor) instances the last Play started
    private bool _stopped;       // S: _defName kept for the status line, playback torn down
    private bool _ambient;       // A pressed: ambient passes started

    /// <summary>Show the status readout (on by default; off for scripted screenshots).</summary>
    public bool ShowStatus = true;

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
        if (!ShowStatus)
        {
            return;
        }
        var layer = new CanvasLayer();
        AddChild(layer);
        _status = new Label
        {
            Position = new Vector2(12, 12),
            Modulate = new Color(0.75f, 0.9f, 1f),
        };
        _status.AddThemeFontSizeOverride("font_size", 14);
        layer.AddChild(_status);
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
        UpdateStatus();
    }

    private void Step()
    {
        _runtime.Advance(FixedDt);
        _steps++;
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
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Plays one def by ANIMATION_NAME the way a startanim starts (RESET_STATE, then
    /// Start per anchor — <see cref="AnimRuntime.Play"/>), from a re-pinned RNG and a zeroed
    /// playhead. Any def already playing is stopped (torn down) first.</summary>
    public void Play(string name)
    {
        if (_defName != null)
        {
            _runtime.Stop(_defName);
        }
        _runtime.Reseed();
        _steps = 0;
        _accum = 0f;
        var started = _runtime.Play(name);
        if (started.Count == 0)
        {
            GD.Print($"anim-lab: no definition named '{name}' among this program's " +
                     $"{_program.Defs.Count} defs — check --chapter/--mission " +
                     "(the Wave 4 picker will list them)");
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
    /// back at step 0 — the visually-identical replay the fixed clock + seed exist for.</summary>
    private void Restart()
    {
        if (_defName == null)
        {
            return;
        }
        _runtime.Stop(_defName);
        Play(_defName);
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

    private void UpdateStatus()
    {
        if (_status == null)
        {
            return;
        }
        string def = _defName == null ? "(none — --play-anim=<name>)"
            : _stopped ? $"{_defName} (stopped)"
            : $"{_defName} ({_instances} instance{(_instances == 1 ? "" : "s")})";
        _status.Text =
            $"anim-lab  t {_steps * FixedDt:0.00} s (step {_steps})  {_timeScale:0.##}×  " +
            $"{(_paused ? "PAUSED" : "PLAYING")}   seed {_seed}   " +
            $"ambient {(_ambient ? "on" : "off")}   def: {def}\n" +
            "[Space pause · . step · R restart · S stop · A ambient · 1/2/3 speed]";
    }
}
