using System;
using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Flight;

/// <summary>The resolved construction state one assembly module gives a flight controller before
/// it joins the tree. It is internal so callers cannot configure a live controller piecemeal.</summary>
internal sealed class FlightControllerBuild
{
    public int PlayerIndex;
    public bool IsHumanPiloted;
    public AiPilot? Pilot;

    /// <summary>When set, replaces keyboard input, used by automated screenshot runs. Each
    /// segment holds its input for its duration (seconds of sim time); the last segment holds
    /// forever, and a respawn restarts the sequence (deterministic runs). Such runs are
    /// unattended, so a crash auto-respawns after a short pause.</summary>
    public (FlightInput Input, float Duration)[]? HoldSegments;

    /// <summary>A stick of the caller's own, taking precedence over every other arm. It exists for
    /// a scripted profile that has to close a loop on the aircraft's own state, which a timed
    /// segment list cannot: the plant has no auto-level, so "push for N seconds" flies a different
    /// trajectory on every airframe while "push until level" flies the same one.</summary>
    public IFlightInputSource? InputSource;
    public Node3D PlaneModel = null!;
    public PropAnimator? Props;
    public WingLightBlinker? WingLights;
    public ControlSurfaceAnimator? Surfaces;
    public PlaneCollider? Collider;
    public PlaneDamage? Damage;
    public Func<Node?, float, bool>? CollideDamageSink;
    public Action<string, Vector3>? GrazeEffectSink;
    public SurfaceDefTable? TouchdownDefs;
    public ProjectilePool? Projectiles;

    /// <summary>The session's "where are the humans" snapshot, which selects the flight model's
    /// far-field plant. Null on every rig built without a session, leaving it near-field.</summary>
    public Func<IReadOnlyList<Vector3>>? HumanPositions;
    /// <summary>⚠ Nullable because null and empty are DIFFERENT bindings downstream
    /// (<see cref="FlightController.PadDevices"/>): null reads every connected pad, empty reads
    /// none. The default stays empty so a builder that says nothing arms nothing, an AI rig
    /// omitting it must not inherit the player's stick, but a single human has to be able to
    /// pass the null through, which a non-nullable field made impossible.</summary>
    public int[]? PadDevices = Array.Empty<int>();
    public bool UseKeyboard;

    /// <summary>The message table the pilot HUD words its auto-land prompt from. Null on a rig
    /// built without one, which leaves the prompt at its data-less stand-in.</summary>
    public Messages? Strings;
    public bool AllowPause;
    public bool Inert;
    public int? Team;
    public PlaneShake Shake = null!;
}

#pragma warning disable SA1202 // This partial declares the controller's internal construction seam.
public partial class FlightController
{
    private bool _constructionBound;

    /// <summary>This seat's live flight keymap, the object all three of its readers resolve through.
    /// Exposed so a suite can read what the seat is actually flying; change it in place through
    /// <see cref="LoadSavedKeymap"/>, never by replacing the reference.</summary>
    public Bindings.ActionMap FlightKeymap => _bindings.Map(Bindings.InputContext.Flight);

    /// <summary>Puts this seat on the keymap its player saved, so it flies what the rebinding screen
    /// wrote. Anything the file does not carry, or this build cannot read, stays at that action's
    /// shipped default, and under the launch gate no file is read at all
    /// (<see cref="Bindings.LaunchBindings"/>). A human rig calls it from <see cref="Bind"/> once
    /// <see cref="PlayerIndex"/> is known; an AI rig never reads a player's file.</summary>
    public void LoadSavedKeymap() =>
        FlightKeymap.Fill(Bindings.LaunchBindings.Map(
            PlayerIndex + 1, Bindings.InputContext.Flight, default, readsKeyboard: true));

    /// <summary>Consumes one complete assembly result before this controller joins the tree. A
    /// second bind or a bind after attachment is a construction error.</summary>
    internal void Bind(FlightControllerBuild build)
    {
        if (_constructionBound || IsInsideTree())
            throw new InvalidOperationException("a flight controller must be bound once before tree attachment");

        PlayerIndex = build.PlayerIndex;
        IsHumanPiloted = build.IsHumanPiloted;
        Pilot = build.Pilot;
        _holdSegments = build.HoldSegments;
        _suppliedInputSource = build.InputSource;
        PlaneModel = build.PlaneModel;
        Props = build.Props;
        WingLights = build.WingLights;
        Surfaces = build.Surfaces;
        Collider = build.Collider;
        Damage = build.Damage;
        CollideDamageSink = build.CollideDamageSink;
        GrazeEffectSink = build.GrazeEffectSink;
        TouchdownDefs = build.TouchdownDefs;
        Projectiles = build.Projectiles;
        HumanPositions = build.HumanPositions;
        UseKeyboard = build.UseKeyboard;
        PadDevices = build.PadDevices;
        AllowPause = build.AllowPause;
        Inert = build.Inert;
        // After PlayerIndex, which names the file, and only for a seat a person flies: an AI rig
        // would otherwise read a player's keymap once per aircraft in the mission.
        if (build.IsHumanPiloted)
            LoadSavedKeymap();
        // After the saved keymap, so the prompt names the control this seat will actually fly with
        // rather than the shipped default the constructor put there.
        _pilotHud.AutoLandPrompt = FlightHud.ComposeAutoLandPrompt(
            build.Strings, FlightKeymap.Bindings(Bindings.InputAction.AutoLand), UseKeyboard);
        if (build.Team is { } team)
            Team = team;

        _worldQuery = new GodotWorldQuery(this);
        // _holdSegments is assigned just above, so it is already final by the time this reads it;
        // resolving here rather than leaving it to the lazy InputSource fallback keeps the choice
        // next to the rest of this method's assignments.
        _inputSource = ResolveInputSource();

        Shake = build.Shake;
        var shakePivot = new Node3D { Name = "ShakePivot" };
        ShakePivot = shakePivot;
        AddChild(shakePivot);
        shakePivot.AddChild(build.PlaneModel);
        _constructionBound = true;
    }
}
#pragma warning restore SA1202
