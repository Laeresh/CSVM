using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using Godot;

namespace CSVM.Session;

/// <summary>The mid-mission cutscene trigger: every frame, with nothing already playing and the
/// player flying, each armed <c>landings.zrd</c> row tests the aircraft against its approach node's
/// condition volume, attitude cone and speed band, and the first row that passes starts its
/// animation with <see cref="CutsceneController"/> hosting it. An <c>auto</c> row offers the
/// auto-land instead of starting anything. Decode:
/// docs/formats/anim-definitions/cutscenes.md.</summary>
public sealed partial class LandingApproachRuntime : Node
{
    private readonly List<Bound> _bound = new();
    private AnimRuntime? _runtime;
    private CutsceneController? _cutscene;
    private Func<FlightController?>? _player;

    /// <summary>Constructs the trigger. It ticks before <see cref="CutsceneController"/>, whose
    /// own priority puts it last, so a row that fires this frame is hosted and mirrored in the
    /// same frame it started.</summary>
    public LandingApproachRuntime()
    {
        Name = "LandingApproaches";
        ProcessPriority = 999;
    }

    /// <summary>How many rows bound to a node this world built. Zero when the mission carries none
    /// of the chapter's approach animations, which is the original's own load-time rejection.
    /// </summary>
    public int Armed => _bound.Count;

    /// <summary>Whether an <c>auto</c> row passes right now: the original lights its auto-land
    /// prompt off exactly this, and never starts the animation on that row. Nothing draws the
    /// prompt or reads a key off it.</summary>
    public bool AutoLandOffered { get; private set; }

    /// <summary>The animation the last row to pass started, for a suite and the log.</summary>
    public string? LastStarted { get; private set; }

    /// <summary>Binds the resolved rows to the built world. Rows whose approach node the build
    /// never created are dropped, so <see cref="Armed"/> counts what can actually fire.</summary>
    public void Bind(
        AnimRuntime runtime,
        IReadOnlyList<LandingApproach> approaches,
        CutsceneController cutscene,
        Func<FlightController?> player)
    {
        _runtime = runtime;
        _cutscene = cutscene;
        _player = player;
        _bound.Clear();
        foreach (var approach in approaches)
        {
            var found = runtime.FindNodes(approach.Node);
            if (found.Count == 0)
            {
                continue;
            }

            var arm = runtime.FindNodes(LandingApproaches.ArmNode, found[0]);
            _bound.Add(new Bound(approach, found[0], arm.Count > 0 ? arm[0] : null));
        }

        if (_bound.Count > 0)
        {
            GD.Print($"landings: {_bound.Count} approach trigger(s) armed");
        }
    }

    /// <inheritdoc/>
    public override void _Process(double delta) => Tick();

    /// <summary>One frame of the trigger. Cheap and inert when the world armed no rows.</summary>
    public void Tick()
    {
        AutoLandOffered = false;
        if (_runtime == null || _bound.Count == 0 || _cutscene is not { Playing: false })
        {
            return;
        }

        // The original's own two guards, in its order: no cutscene running, and a player who is
        // still flying rather than pinned inside one.
        if (_player?.Invoke() is not { Held: false, Inert: false } plane)
        {
            return;
        }

        float speed = plane.WorldVelocity.Length();
        foreach (var bound in _bound)
        {
            if (!Passes(bound, plane, speed))
            {
                continue;
            }

            if (bound.Approach.Auto)
            {
                AutoLandOffered = true;
                continue;
            }

            Start(bound.Approach);
            return;
        }
    }

    private bool Passes(Bound bound, FlightController plane, float speed)
    {
        var approach = bound.Approach;
        if (bound.Arm is { Visible: false } || !approach.SpeedInBand(speed))
        {
            return false;
        }

        var frame = bound.Node.GlobalTransform;
        return LandingApproaches.AngleBetween(frame.Basis, plane.Attitude) <= approach.AngleRad
            && approach.Contains(frame.AffineInverse() * plane.WorldPosition);
    }

    // Starts the row's definition. The cutscene host is already registered for every definition
    // this chapter's table can reach, which is where the original's per-instance registration
    // lands (CutsceneController.HostDefinitions).
    private void Start(LandingApproach approach)
    {
        int started = _runtime!.Play(approach.Anim).Count;
        LastStarted = approach.Anim;
        GD.Print($"landings: '{approach.Node}' flown, started '{approach.Anim}' " +
                 $"({started} definition(s))");
    }

    private readonly record struct Bound(LandingApproach Approach, Node3D Node, Node3D? Arm);
}
