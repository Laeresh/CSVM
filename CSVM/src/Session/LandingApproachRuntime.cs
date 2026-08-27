using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using Godot;

namespace CSVM.Session;

/// <summary>The mid-mission cutscene trigger: every frame, with nothing already playing and the
/// player flying, each armed <c>landings.zrd</c> row tests the aircraft against its approach node's
/// condition volume, attitude cone and speed band, and the first row that passes starts its
/// animation with <see cref="CutsceneController"/> hosting it, once per entry into that volume. An
/// <c>auto</c> row offers the auto-land instead of starting anything. Decode:
/// docs/formats/anim-definitions/cutscenes.md.</summary>
public sealed partial class LandingApproachRuntime : Node
{
    private readonly List<Bound> _bound = new();
    private readonly List<LandingApproach> _approaches = new();
    private readonly HashSet<int> _boundIds = new();
    private readonly List<PickupSpec> _pickups = new();
    private readonly HashSet<string> _startedPickups = new(StringComparer.OrdinalIgnoreCase);
    // Rows that have fired and whose condition has not stopped passing since. A cutscene ends with
    // the aircraft parked where the definition left it, which is still inside the volume that
    // started it, so without this the row re-fires on the frame after the handoff.
    private readonly HashSet<int> _latched = new();
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
    /// prompt off exactly this, and never starts the animation on that row by itself. The HUD draws
    /// the prompt off this flag, and the auto-land button starts the row's animation while it holds.</summary>
    public bool AutoLandOffered { get; private set; }

    /// <summary>The animation the last row to pass started, for a suite and the log.</summary>
    public string? LastStarted { get; private set; }

    /// <summary>Binds the resolved rows to the built world. Rows whose approach node the build
    /// never created are dropped, so <see cref="Armed"/> counts what can actually fire.</summary>
    public void Bind(
        AnimRuntime runtime,
        IReadOnlyList<LandingApproach> approaches,
        CutsceneController cutscene,
        Func<FlightController?> player,
        IReadOnlyList<PickupSpec>? pickups = null)
    {
        _runtime = runtime;
        _cutscene = cutscene;
        _player = player;
        _bound.Clear();
        _approaches.Clear();
        _approaches.AddRange(approaches);
        _boundIds.Clear();
        _pickups.Clear();
        if (pickups != null)
        {
            _pickups.AddRange(pickups);
        }
        _startedPickups.Clear();
        _latched.Clear();
        RefreshBindings();

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
        if (_runtime == null || _cutscene is not { Playing: false })
        {
            return;
        }
        if (RefreshBindings() > 0)
        {
            GD.Print($"landings: {_bound.Count} approach trigger(s) armed after world staging");
        }
        if (_bound.Count == 0)
        {
            return;
        }

        // The original's own two guards, in its order: no cutscene running, and a player who is
        // still flying rather than pinned inside one.
        if (_player?.Invoke() is not { Held: false, Inert: false } plane)
        {
            return;
        }

        StartPickupTiming(plane.WorldPosition);

        float speed = plane.WorldVelocity.Length();
        for (int i = 0; i < _bound.Count; i++)
        {
            var bound = _bound[i];
            if (!Passes(bound, plane, speed))
            {
                _latched.Remove(bound.Id);
                continue;
            }

            if (bound.Approach.Auto)
            {
                AutoLandOffered = true;
                // The same latch the manual row uses: a press starts it once per entry into the
                // sphere, and the cutscene guard above already gates this whole method.
                if (plane.AutoLandPressed() && _latched.Add(bound.Id))
                {
                    Start(bound.Approach);
                    return;
                }
                continue;
            }

            if (!_latched.Add(bound.Id))
            {
                continue;
            }

            Start(bound.Approach);
            return;
        }
    }

    private void StartPickupTiming(Vector3 playerPosition)
    {
        foreach (var pickup in _pickups)
        {
            if (_startedPickups.Contains(pickup.Node))
            {
                continue;
            }
            var sensors = _runtime!.FindNodes(pickup.Node);
            if (sensors.Count == 0 || !sensors[0].Visible
                || sensors[0].GlobalPosition.DistanceSquaredTo(playerPosition)
                    > pickup.Radius * pickup.Radius)
            {
                continue;
            }
            if (_runtime.Play(Pickups.TimingAnim).Count > 0)
            {
                _startedPickups.Add(pickup.Node);
            }
        }
    }

    private int RefreshBindings()
    {
        int added = 0;
        for (int i = 0; i < _approaches.Count; i++)
        {
            if (_boundIds.Contains(i))
            {
                continue;
            }

            var approach = _approaches[i];
            var found = _runtime!.FindNodes(approach.Node);
            if (found.Count == 0)
            {
                continue;
            }

            var arm = _runtime.FindNodes(LandingApproaches.ArmNode, found[0]);
            _bound.Add(new Bound(i, approach, found[0], arm.Count > 0 ? arm[0] : null));
            _boundIds.Add(i);
            added++;
        }
        return added;
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
        int started = _runtime!.PlayMissionTrigger(approach.Anim).Count;
        LastStarted = approach.Anim;
        GD.Print($"landings: '{approach.Node}' flown, started '{approach.Anim}' " +
                 $"({started} definition(s))");
    }

    private readonly record struct Bound(int Id, LandingApproach Approach, Node3D Node, Node3D? Arm);
}
