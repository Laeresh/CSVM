using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using Godot;

namespace CSVM.Session;

/// <summary>The rope-ladder switch flown against the built world: every frame outside a
/// cutscene it reads the flown aircraft's attitude and position, tests them against the mission's
/// <c>pickups.zrd</c> sensors, and lets <see cref="LadderSwitch"/> start the ladder definitions
/// as mission triggers. It also answers the definitions' settle <c>CALLBACK</c> ahead of the
/// cutscene host, which is where the original registers the switch on each definition.
/// Decode: docs/org/ladderSwitch.md.</summary>
public sealed partial class LadderSwitchRuntime : Node
{
    private readonly LadderSwitch _switch = new();
    private readonly List<PickupSpec> _pickups = new();
    private readonly Func<int, string?, string?, bool> _host;
    private Func<int, string?, string?, bool>? _inner;
    private AnimRuntime? _runtime;
    private CutsceneController? _cutscene;
    private Func<FlightController?>? _player;

    /// <summary>Constructs the switch. It ticks alongside the landings trigger, before the
    /// cutscene host, so a drop started this frame is hosted in the same frame.</summary>
    public LadderSwitchRuntime()
    {
        Name = "LadderSwitch";
        ProcessPriority = 999;
        _host = Host;
    }

    /// <summary>Where the ladder is, for a suite and the log.</summary>
    public LadderState State => _switch.State;

    /// <summary>The definition the switch last started, for a suite and the log.</summary>
    public string? LastStarted { get; private set; }

    /// <summary>Binds the switch to the built world and takes the runtime's <c>CALLBACK</c> host
    /// slot, chaining to whatever held it. A re-bind after the roster graft keeps the chain intact
    /// and resets the ladder to retracted, the original's load-time state.</summary>
    public void Bind(
        AnimRuntime runtime,
        CutsceneController cutscene,
        Func<FlightController?> player,
        IReadOnlyList<PickupSpec>? pickups)
    {
        _runtime = runtime;
        _cutscene = cutscene;
        _player = player;
        _pickups.Clear();
        if (pickups != null)
        {
            _pickups.AddRange(pickups);
        }
        _switch.Reset();
        LastStarted = null;
        if (runtime.CallbackHost != _host)
        {
            _inner = runtime.CallbackHost;
            runtime.CallbackHost = _host;
        }
    }

    /// <inheritdoc/>
    public override void _Process(double delta) => Tick();

    /// <summary>One frame of the switch. Inert without a bound world or sensors to test.</summary>
    public void Tick()
    {
        if (_runtime == null || _pickups.Count == 0 || _cutscene is not { Playing: false })
        {
            return;
        }
        if (_player?.Invoke() is not { Held: false, InPlay: true } plane)
        {
            return;
        }

        bool wanted = LadderSwitch.IsLevel(plane.Attitude) && InsideSensor(plane.WorldPosition);
        string? started = _switch.Step(wanted, Start);
        if (started != null)
        {
            LastStarted = started;
            GD.Print($"ladder: started '{started}' ({_switch.State})");
        }
    }

    private bool InsideSensor(Vector3 playerPosition)
    {
        foreach (var pickup in _pickups)
        {
            var sensors = _runtime!.FindNodes(pickup.Node);
            if (sensors.Count > 0 && sensors[0].Visible
                && LadderSwitch.WithinSensor(playerPosition, sensors[0].GlobalPosition, pickup.Radius))
            {
                return true;
            }
        }
        return false;
    }

    // A mission trigger, so the drop's OBJECT_ADD_CHILD can materialize the library rope ladder.
    private bool Start(string animName) => _runtime!.PlayMissionTrigger(animName).Count > 0;

    private bool Host(int code, string? animName, string? rootName) =>
        _switch.Settle(code, animName) || (_inner?.Invoke(code, animName, rootName) ?? false);
}
