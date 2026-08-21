using System;
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

    /// <summary>When set, replaces keyboard input — used by automated screenshot runs. Each
    /// segment holds its input for its duration (seconds of sim time); the last segment holds
    /// forever, and a respawn restarts the sequence (deterministic runs). Such runs are
    /// unattended, so a crash auto-respawns after a short pause.</summary>
    public (FlightInput Input, float Duration)[]? HoldSegments;
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
    /// <summary>⚠ Nullable because null and empty are DIFFERENT bindings downstream
    /// (<see cref="FlightController.PadDevices"/>): null reads every connected pad, empty reads
    /// none. The default stays empty so a builder that says nothing arms nothing — an AI rig
    /// omitting it must not inherit the player's stick — but a single human has to be able to
    /// pass the null through, which a non-nullable field made impossible.</summary>
    public int[]? PadDevices = Array.Empty<int>();
    public bool UseKeyboard;
    public bool AllowPause;
    public bool Inert;
    public int? Team;
    public PlaneShake Shake = null!;
}

#pragma warning disable SA1202 // This partial declares the controller's internal construction seam.
public partial class FlightController
{
    private bool _constructionBound;

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
        UseKeyboard = build.UseKeyboard;
        PadDevices = build.PadDevices;
        AllowPause = build.AllowPause;
        Inert = build.Inert;
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
