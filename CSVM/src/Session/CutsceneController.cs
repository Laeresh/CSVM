using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using Godot;

namespace CSVM.Session;

/// <summary>
/// The host a cutscene definition's <c>CALLBACK</c> codes are dispatched to, and the session-side
/// state those codes describe: presentation on, the player out of flight, the world and the
/// objectives held, the AI parked, then one hard cut back to gameplay. The bars and the camera path
/// are the definition's own data (the <c>letterbox</c> node pinned to <c>camera1</c>); what lives
/// here is the state no definition can assert by itself. Decode:
/// docs/formats/anim-definitions/cutscenes.md.
/// </summary>
public sealed partial class CutsceneController : Node
{
    /// <summary>The gamez node a cutscene definition poses, and the pose every rig camera mirrors
    /// while one plays. Bodiless in the gamez, so the world build stands a marker in for it.</summary>
    public const string CameraNode = "camera1";

    /// <summary>The gamez node carrying the two black bars, activated by the shared
    /// <c>letterbox</c> definition and pinned to <see cref="CameraNode"/> every tick.</summary>
    public const string BarsNode = "letterbox";

    /// <summary>The code that marks a definition as the running cutscene: the original's host
    /// stores the animation raising it in the active-cutscene slot, which is what suspends the
    /// world.</summary>
    public const int CodeHoldsWorld = 20;

    /// <summary>The bespoke C1/M04 intro, and the one the other twelve story missions share. ⚠ The
    /// authored codes do NOT identify a cutscene on their own: Instant Action's own
    /// <c>player_setup</c> raises the same nine, and what the original does with them there is
    /// undecoded (docs/formats/anim-definitions/cutscenes.md).</summary>
    public static readonly string[] IntroAnims = { "mission_intro_animation", "generic_intro" };

    /// <summary>Raised whenever the world hold changes, so the session can suspend the mission
    /// director alongside its own per-step world update (code 20 stops both).</summary>
    public Action<bool>? WorldHeld;

    // The rest of the mission-script host's codes the intro definitions author. Each is the whole
    // message: the definition it sits in never qualifies it
    // (docs/formats/anim-definitions/cutscenes.md).
    private const int CodeHandoff = 1;
    private const int CodePresentation = 2;
    private const int CodeRestoreSystems = 10;
    private const int CodeOutOfFlight = 11;
    private const int CodeCamParamsFree = 666;
    private const int CodeCamParamsRestore = 667;
    private const int CodeParkAi = 913;
    private const int CodeRevealAi = 914;

    // ANIM_STATE's RUNNING, the value AnimRuntime.AnimStateOf reports while an instance is live.
    private const int AnimRunning = 2;

    // The two codes that reach no case in the original's own host either. Answered as named gaps so
    // a census can tell "authored and ignored" from "never authored".
    private static readonly int[] NamedGaps = { 14, 123 };

    // The gameplay end state, in the order C1/M04's own RESET_STATE authors it: control and chrome
    // back, the in-flight systems back, the AI revealed, the camera-parameter gate closed.
    private static readonly int[] RestoreCodes =
        { CodeHandoff, CodeRestoreSystems, CodeRevealAi, CodeCamParamsRestore };

    private readonly List<int> _codes = new();
    private readonly List<FlightController> _parked = new();
    private readonly HashSet<int> _gapsLogged = new();
    private readonly Dictionary<Camera3D, float> _fov = new();
    private AnimRuntime? _runtime;
    private IReadOnlyList<PlayerRig> _rigs = Array.Empty<PlayerRig>();
    private Func<IReadOnlyList<FlightController>>? _aiPlanes;
    private Node3D? _cutsceneCamera;
    private Node3D? _bars;
    private Node3D? _card;
    private Aabb _cardBox;

    // BL-452 watchdog state (WatchBars): the bars' visibility as of the last tick, and how many
    // times it has flipped in the current episode.
    private bool _lastBarsVisible;
    private int _barsFlipsThisEpisode;

    /// <summary>Constructs the host. It ticks LAST in the frame so the rig cameras take the pose
    /// this frame's animation advance put <c>camera1</c> in; a tick before that advance would show
    /// the bars, posed inside it, against a camera one frame behind them.</summary>
    public CutsceneController()
    {
        Name = "CutsceneController";
        ProcessPriority = 1000;
    }

    /// <summary>Whether a cutscene definition currently owns the session.</summary>
    public bool Playing { get; private set; }

    /// <summary>Whether the world and the objectives update are held (code 20). The session's drive
    /// paths read this; nothing clears it but the handoff or a skip.</summary>
    public bool HoldsWorld { get; private set; }

    /// <summary>Whether the chrome is hidden and the view is off the aircraft (code 2).</summary>
    public bool Presenting { get; private set; }

    /// <summary>Whether the player is out of flight: input suspended, the airframe pinned and
    /// undrawn, engine audio released (code 11).</summary>
    public bool OutOfFlight { get; private set; }

    /// <summary>Whether the AI vehicles are parked (code 913), the state code 914 reverses.</summary>
    public bool AiParked { get; private set; }

    /// <summary>Whether the camera-parameter profile is left to the definition (code 666). CSVM
    /// applies that profile once when a rig is built and never on a view change, so there is no
    /// live applier for this to suppress; the gate is tracked, not acted on.</summary>
    public bool CamParamsFree { get; private set; }

    /// <summary>Every hosted code in the order it was dispatched, which is what a suite asserts the
    /// shape of the cutscene by.</summary>
    public IReadOnlyList<int> Codes => _codes;

    /// <summary>The animation name that raised the first code, and whose end hands off.</summary>
    public string? Anim { get; private set; }

    /// <summary>Is this one of the definitions this host answers for? Only a story mission's intro
    /// plays as a cutscene here; every other definition's codes keep the answer they had.</summary>
    public static bool IsIntro(string? animName) =>
        animName != null && Array.IndexOf(IntroAnims, animName) >= 0;

    /// <summary>The world's animation runtime and the two nodes a cutscene definition drives. Run
    /// once the world is built, before any rig exists: the intro definitions start during the
    /// animation bootstrap, so their codes are hosted before there is anything to apply them to.
    /// </summary>
    public void BindWorld(AnimRuntime? runtime)
    {
        _runtime = runtime;
        if (runtime == null)
        {
            return;
        }

        _cutsceneCamera = First(runtime.FindNodes(CameraNode));
        _bars = First(runtime.FindNodes(BarsNode));
        // Measured once, before anything scales it: re-measuring a scaled card would read its own
        // last answer back and oscillate.
        _card = _bars != null && _bars.GetChildCount() > 0 ? _bars.GetChild(0) as Node3D : null;
        if (_card != null && CardBounds(_card) is { } box)
        {
            _cardBox = box;
        }

        GD.Print($"cutscene: {(_cutsceneCamera != null ? "camera1" : "NO camera1")}, " +
                 $"{(_card != null ? $"letterbox card {_cardBox.Size}" : "NO letterbox bars")}");
    }

    /// <summary>The session's rigs and its live AI aircraft, once both exist. Re-applies whatever
    /// state the codes already asked for, which is how a cutscene that started during the world
    /// build reaches the aircraft built after it.</summary>
    public void BindRigs(IReadOnlyList<PlayerRig> rigs, Func<IReadOnlyList<FlightController>> aiPlanes)
    {
        _rigs = rigs;
        _aiPlanes = aiPlanes;
        if (!Playing)
        {
            return;
        }

        ApplyPresentation(Presenting);
        ApplyOutOfFlight(OutOfFlight);
        if (AiParked)
        {
            ParkAi();
        }
    }

    /// <summary>The <c>CALLBACK</c> host itself: answers one authored code, returning whether this
    /// host acted on it. A code outside the cutscene vocabulary is declined, so the runtime's own
    /// two vehicle-death codes and every unknown one keep the answer they had.</summary>
    public bool Host(int code, string? animName)
    {
        if (!IsIntro(animName) && !(Playing && animName == Anim))
        {
            return false;
        }

        switch (code)
        {
            case CodeHoldsWorld:
            case CodePresentation:
            case CodeOutOfFlight:
            case CodeParkAi:
            case CodeRevealAi:
            case CodeCamParamsFree:
            case CodeCamParamsRestore:
            case CodeHandoff:
            case CodeRestoreSystems:
                break;
            default:
                if (Array.IndexOf(NamedGaps, code) < 0)
                {
                    return false;
                }

                break;
        }

        _codes.Add(code);
        if (!Playing)
        {
            Playing = true;
            Anim = animName;
            _barsFlipsThisEpisode = 0;
            _lastBarsVisible = _bars?.Visible ?? false;
            GD.Print($"cutscene: '{animName}' has the session");
        }

        Act(code);
        return true;
    }

    /// <inheritdoc/>
    public override void _Process(double delta) => Tick();

    /// <summary>One frame of the cutscene: every rig camera takes the pose the definition put
    /// <c>camera1</c> in, and the definition ending is the handoff. Cheap and inert when no
    /// cutscene is playing.</summary>
    public void Tick()
    {
        if (!Playing)
        {
            return;
        }

        MirrorCamera();
        FrameBars();
        WatchBars();
        if (_runtime != null && Anim != null && _runtime.AnimStateOf(Anim) != AnimRunning)
        {
            Restore("its definition ended");
        }
    }

    /// <summary>The player's skip. Force-stops the definition the way the original's state core
    /// does, then restores the gameplay state the definition's own RESET_STATE asserts, so the
    /// remaining beats being dropped cannot leave the mission held, hidden or unflyable.</summary>
    public bool Skip()
    {
        if (!Playing)
        {
            return false;
        }

        if (Anim != null)
        {
            _runtime?.Stop(Anim);
        }

        Restore("skipped");
        return true;
    }

    private static Node3D? First(IReadOnlyList<Node3D> found) => found.Count > 0 ? found[0] : null;

    // The card's extent in the bars root's own frame: one mesh on or under `g1`, whose AABB carries
    // both quads and the authored 7.5 m standoff.
    private static Aabb? CardBounds(Node3D card)
    {
        if (card is VisualInstance3D self)
        {
            return card.Transform * self.GetAabb();
        }

        foreach (var child in card.GetChildren())
        {
            if (child is VisualInstance3D mesh and Node3D inner)
            {
                return card.Transform * (inner.Transform * mesh.GetAabb());
            }
        }

        return null;
    }

    // BL-452: the bars are separately-called data (docs/formats/anim-definitions/cutscenes.md),
    // so this controller never turns them on or off itself except in Restore below. While Playing
    // is true they should transition AT MOST once, false->true, from the letterbox call site(s);
    // a second flip in either direction inside a live episode is exactly the reported flicker, and
    // costs one bool compare a tick to catch. Quiet unless it actually happens.
    private void WatchBars()
    {
        if (_bars == null || _bars.Visible == _lastBarsVisible)
        {
            return;
        }

        _lastBarsVisible = _bars.Visible;
        _barsFlipsThisEpisode++;
        if (_barsFlipsThisEpisode > 1 || !_bars.Visible)
        {
            GD.PrintErr($"cutscene: '{Anim}' letterbox bars flipped to {_bars.Visible} mid-episode " +
                        $"(flip #{_barsFlipsThisEpisode}) at t={Utils.GameClock.Current?.Time ?? 0.0:0.###} " +
                        "-- BL-452, the reported mid-cutscene flicker");
        }
    }

    // The gameplay end state the intro definitions author in their own RESET_STATE, raised here
    // because a CSVM RESET_STATE dispatch deliberately raises no callbacks and the original's
    // RESET_TIME schedule is undecoded. Also retracts the bars: nothing stops the letterbox
    // definition by name, in this engine or the original.
    private void Restore(string why)
    {
        GD.Print($"cutscene: '{Anim}' {why} at t={Utils.GameClock.Current?.Time ?? 0.0:0.##}, " +
                 $"handing off after {_codes.Count} code(s)");
        foreach (int code in RestoreCodes)
        {
            _codes.Add(code);
            Act(code);
        }

        if (_bars != null)
        {
            AnimRuntime.SetSubtreeActive(_bars, false);
        }

        // ⚠ Park the cutscene camera back at the origin. Other definitions pose against `camera1`
        // too (the bullethole decals), and a node left wherever the last beat put it would place
        // them there for the rest of the mission.
        if (_cutsceneCamera != null)
        {
            _cutsceneCamera.Transform = Transform3D.Identity;
        }

        foreach (var (camera, fov) in _fov)
        {
            camera.Fov = fov;
        }

        _fov.Clear();
        Playing = false;
        Anim = null;
    }

    private void Act(int code)
    {
        switch (code)
        {
            case CodeHoldsWorld:
                HoldsWorld = true;
                WorldHeld?.Invoke(true);
                break;
            case CodePresentation:
                Presenting = true;
                ApplyPresentation(true);
                break;
            case CodeOutOfFlight:
                OutOfFlight = true;
                ApplyOutOfFlight(true);
                break;
            case CodeParkAi:
                AiParked = true;
                ParkAi();
                break;
            case CodeRevealAi:
                AiParked = false;
                RevealAi();
                break;
            case CodeCamParamsFree:
                CamParamsFree = true;
                break;
            case CodeCamParamsRestore:
                CamParamsFree = false;
                break;
            case CodeHandoff:
                HoldsWorld = false;
                WorldHeld?.Invoke(false);
                Presenting = false;
                ApplyPresentation(false);
                OutOfFlight = false;
                ApplyOutOfFlight(false);
                break;
            case CodeRestoreSystems:
                foreach (var pilot in Pilots())
                {
                    pilot.Audio?.SetPaused(false);
                    pilot.SetPilotHudVisible(true);
                }

                break;
            default:
                if (_gapsLogged.Add(code))
                {
                    GD.Print($"cutscene: callback {code} is a named gap, reaching no case in the " +
                             "original's own host either");
                }

                break;
        }
    }

    // The chrome and the view target: CameraOwned is what silences the whole per-frame camera arm,
    // which is also what stops the cockpit rules being re-asserted over the cutscene every frame.
    private void ApplyPresentation(bool on)
    {
        foreach (var pilot in Pilots())
        {
            pilot.CameraOwned = on;
            pilot.SetPilotHudVisible(!on);
        }
    }

    // Out of flight: the airframe holds its pose, takes no input and is neither drawn nor hittable,
    // and its sound handles go with it. The original's node deactivation names its own `player`
    // node, which is not the airframe this engine builds for the pilot.
    private void ApplyOutOfFlight(bool on)
    {
        foreach (var pilot in Pilots())
        {
            pilot.Held = on;
            pilot.Inert = on;
            pilot.Audio?.SetPaused(on);
        }
    }

    private void ParkAi()
    {
        if (_aiPlanes == null)
        {
            return;
        }

        foreach (var ai in _aiPlanes())
        {
            if (!ai.Inert)
            {
                ai.Inert = true;
                _parked.Add(ai);
            }
        }
    }

    // Only what this controller parked comes back: an aircraft built inert for a later wave is not
    // this cutscene's to activate.
    private void RevealAi()
    {
        foreach (var ai in _parked)
        {
            ai.Inert = false;
        }

        _parked.Clear();
    }

    private void MirrorCamera()
    {
        if (_cutsceneCamera == null)
        {
            return;
        }

        var pose = _cutsceneCamera.GlobalTransform.Orthonormalized();
        foreach (var rig in _rigs)
        {
            rig.Camera.GlobalTransform = pose;
        }
    }

    // The bars are a fixed card 7.5 m in front of the eye, so what they cover is a question of
    // frame shape: the card IS the frame, which is the only reading under which the geometry is a
    // letterbox at all. Each camera takes the widest field of view the card still covers: its
    // authored height on a 4:3 pane, its width on anything wider than the 5:3 it was cut for.
    private void FrameBars()
    {
        if (_card == null || _rigs.Count == 0)
        {
            return;
        }

        float dist = Mathf.Abs(_cardBox.GetCenter().Z);
        float halfHeight = _cardBox.Size.Y * 0.5f;
        float halfWidth = _cardBox.Size.X * 0.5f;
        if (dist <= 0f || halfHeight <= 0f || halfWidth <= 0f)
        {
            return;
        }

        foreach (var rig in _rigs)
        {
            var size = rig.Camera.GetViewport().GetVisibleRect().Size;
            float aspect = size.Y > 0f ? size.X / size.Y : 1f;
            _fov.TryAdd(rig.Camera, rig.Camera.Fov);
            rig.Camera.Fov = Mathf.RadToDeg(
                2f * Mathf.Atan(Mathf.Min(halfHeight, halfWidth / aspect) / dist));
        }
    }


    private IEnumerable<FlightController> Pilots()
    {
        foreach (var rig in _rigs)
        {
            if (rig.Controller is { IsHumanPiloted: true } pilot)
            {
                yield return pilot;
            }
        }
    }
}
