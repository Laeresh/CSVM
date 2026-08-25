using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>The aggregate facts the initial human-field build adds to the session summary.</summary>
public readonly record struct FlightRosterBuild(int MeshInstances, string SummarySuffix);

/// <summary>One AI aircraft's authored identity and launch facts. <c>AiDef</c> names the militia
/// variant it flies (<c>bhatwarhawk</c>); null takes the airframe's base def. <c>Fit</c> is a
/// menu-chosen loadout laid over the stock table's, set only by the wingman spawns: the stock-table
/// branch below also catches enemies flying player airframes, which must keep their own fit.
/// <c>Nitro</c> is the roster block's injector flag (<see cref="AiSkills.RosterNitro"/>).
/// <c>RosterSkills</c> is a roster block's own nine-slot pilot vector: a set slot outranks the
/// def's, an unset one falls through to the def and then to <c>AttackRating</c>.
/// <c>NodeName</c> is the identity the spawned node takes, and a caller that has an authored one
/// must pass it: <c>primary_target</c> and <c>rating_biases</c> are written against the roster
/// block's own name, so a spawn left on the fallback <c>ai{n}_{plane}</c> can match neither
/// (BL-401).</summary>
public readonly record struct AiSpawn(string PlaneName, Vector3 Position, Vector3 LookAt, AiPilot Pilot,
    PaintScheme? Scheme = null, int? Team = null, bool Inert = false, bool ShippedSkins = false,
    string? AiDef = null, LoadoutChoice? Fit = null, int? AttackRating = null, bool Nitro = false,
    AiSkillVector? RosterSkills = null, string? NodeName = null);

/// <summary>The session's aircraft set: builds the human field in deterministic player order and
/// introduces AI aircraft later for missions, waves, and generators. The roster is the assembly
/// seam; callers receive finished controllers and never configure one piecemeal.</summary>
public sealed class FlightRoster
{
    /// <summary>The first AI shooter id. Outside every human player index and the match roster.</summary>
    public const int ShooterIdBase = 100;

    private readonly HumanFlightAdapter? _players;
    private readonly AiFlightAssembler _aiAssembler;
    private readonly FlightWorldBindings _world;
    private readonly HumanRosterBindings _human;
    private readonly Action? _aiAssemblyFault;
    private readonly List<FlightController> _ai = new();
    private readonly IReadOnlyList<FlightController> _aiView;
    private readonly Dictionary<FlightController, AiSubscriptions> _aiSubscriptions = new();
    private IReadOnlyList<PlayerRig> _humans = Array.Empty<PlayerRig>();
    private Action<List<AimCandidate>>? _targetSubParts;
    private Action<List<AimCandidate>>? _targetObjectives;
    private int _spawned;

    internal FlightRoster(FlightRosterPolicy policy, LiveryResolver liveries,
        WorldEffectsFactory? worldEffects, Node3D worldRoot, AircraftAssemblyResources aircraft,
        FlightWorldBindings world, HumanRosterBindings human, IFlightStarts? starts = null,
        Action? aiAssemblyFault = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(liveries);
        ArgumentNullException.ThrowIfNull(worldRoot);
        ArgumentNullException.ThrowIfNull(aircraft);
        ArgumentNullException.ThrowIfNull(aircraft.PlanesGamez);
        ArgumentNullException.ThrowIfNull(aircraft.AiStatsFor);
        ArgumentNullException.ThrowIfNull(aircraft.PaintRng);
        ArgumentNullException.ThrowIfNull(aircraft.StockLoadouts);
        ArgumentNullException.ThrowIfNull(aircraft.WeaponDefs);
        ArgumentNullException.ThrowIfNull(aircraft.Textures);
        ArgumentNullException.ThrowIfNull(aircraft.Shakes);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(world.Projectiles);
        ArgumentNullException.ThrowIfNull(human);
        if (world.CrashProgram != null && world.WorldScene != null)
            ArgumentNullException.ThrowIfNull(worldEffects);
        if (starts != null)
        {
            ArgumentNullException.ThrowIfNull(worldEffects);
            ArgumentNullException.ThrowIfNull(aircraft.StatsFor);
            ArgumentNullException.ThrowIfNull(aircraft.CamParamsFor);
            ArgumentNullException.ThrowIfNull(aircraft.WeaponMessages);
            ArgumentNullException.ThrowIfNull(human.PauseState);
            ArgumentNullException.ThrowIfNull(human.MenuInputFor);
            ArgumentNullException.ThrowIfNull(human.ExitSession);
        }
        _world = world;
        _human = human;
        _aiAssemblyFault = aiAssemblyFault;
        _aiView = _ai.AsReadOnly();
        if (starts != null)
            _players = new HumanFlightAdapter(policy, liveries, starts, worldEffects!, worldRoot,
                aircraft, world, human);
        _aiAssembler = new AiFlightAssembler(policy, liveries, worldEffects, worldRoot,
            aircraft, world, human.RigCount);
    }

    public IReadOnlyList<PlayerRig> Humans => _humans;

    public IReadOnlyList<FlightController> AiAircraft => _aiView;

    internal AiSkills? AiSkills => _aiAssembler.Skills;

    /// <summary>Builds every human aircraft in ascending player order. That order is part of the
    /// interface: it fixes the shared livery stream and spawn-list wrap.</summary>
    public FlightRosterBuild BuildPlayers(IReadOnlyList<PlayerRig> rigs)
    {
        if (_players == null)
            throw new InvalidOperationException("human roster construction needs a flight-start policy");
        if (_humans.Count != 0)
            throw new InvalidOperationException("the human roster can only be built once");
        var assemblerState = _players.CaptureState();
        var attempted = new List<FlightController>();
        try
        {
            for (int pi = 0; pi < rigs.Count; pi++)
            {
                _players.Assemble(pi, rigs[pi], attempted.Add);
                if (rigs[pi].Controller is { } controller)
                {
                    controller.SmokeScreens = _human.SmokeScreens;
                    controller.PauseState = _human.PauseState;
                    controller.TargetSubParts = _targetSubParts;
                    controller.TargetObjectives = _targetObjectives;
                }
            }
            _humans = new List<PlayerRig>(rigs).AsReadOnly();
            return new FlightRosterBuild(_players.MeshInstances, _players.WhatSuffix);
        }
        catch
        {
            RollBackPlayers(rigs, attempted, assemblerState);
            throw;
        }
    }

    public void SetTargetSubParts(Action<List<AimCandidate>> source)
    {
        _targetSubParts = source;
        foreach (var rig in _humans)
            if (rig.Controller is { } controller)
                controller.TargetSubParts = source;
    }

    /// <summary>Binds the campaign mission's objective-site feed. Its own channel rather than a
    /// second assignment to <see cref="SetTargetSubParts"/>, which the zeppelin runtime already
    /// holds; the two feeds carry different mission flags and ride different cycles. Human panes
    /// only: an AI rig does no targeting of its own.</summary>
    public void SetTargetObjectives(Action<List<AimCandidate>> source)
    {
        _targetObjectives = source;
        foreach (var rig in _humans)
            if (rig.Controller is { } controller)
                controller.TargetObjectives = source;
    }

    /// <summary>Introduces one fully configured AI aircraft into the running session.</summary>
    public FlightController SpawnAi(AiSpawn spawn)
    {
        int index = _spawned;
        FlightController? attempted = null;
        var assemblerState = _aiAssembler.CaptureState(spawn);
        try
        {
            var controller = _aiAssembler.Assemble(spawn, index, created => attempted = created);
            _aiAssemblyFault?.Invoke();
            controller.SmokeScreens = _human.SmokeScreens;
            controller.TargetSubParts = _targetSubParts;
            Action<AiMode, AiMode, string>? modeChanged = null;
            Action<string>? rollLogged = null;
            if (spawn.Pilot.Machine is { } modes)
            {
                string tag = controller.Name;
                modeChanged = (from, to, why) => Log.Info("flight",
                    $"ai mode: {tag}: {AiModeMachine.NameOf(from)} -> {AiModeMachine.NameOf(to)} ({why})");
                rollLogged = line => Log.Info("flight", $"ai roll: {tag}: {line}");
                modes.ModeChanged += modeChanged;
                modes.RollLogged += rollLogged;
            }
            Action<int, int?> downed = (victim, killer) => Log.Info("flight",
                $"ai: {controller.Name} downed (shooter id {victim}, killer {killer?.ToString() ?? "none"})");
            controller.Downed += downed;
            _ai.Add(controller);
            _aiSubscriptions.Add(controller,
                new AiSubscriptions(spawn.Pilot.Machine, modeChanged, rollLogged, downed));
            _spawned++;
            return controller;
        }
        catch
        {
            if (attempted != null)
                RemoveController(attempted);
            _aiAssembler.RestoreState(spawn, assemblerState);
            throw;
        }
    }

    /// <summary>Releases the roster's non-node membership and controller bindings. The session
    /// subtree still owns and frees the aircraft nodes atomically.</summary>
    public void ClearMembership()
    {
        foreach (var rig in _humans)
        {
            if (rig.Controller is { } controller)
            {
                controller.DetachRosterBindings(_world.Projectiles);
                rig.Controller = null;
            }
        }
        foreach (var controller in _ai)
        {
            RemoveSubscriptions(controller);
            controller.DetachRosterBindings(_world.Projectiles);
        }
        _humans = Array.Empty<PlayerRig>();
        _ai.Clear();
        _aiSubscriptions.Clear();
        _targetSubParts = null;
        _targetObjectives = null;
        _spawned = 0;
    }

    private void RollBackPlayers(IReadOnlyList<PlayerRig> rigs,
        List<FlightController> attempted, HumanFlightAdapter.AssemblyState assemblerState)
    {
        for (int i = attempted.Count - 1; i >= 0; i--)
            RemoveController(attempted[i]);
        foreach (var rig in rigs)
            rig.Controller = null;
        _players!.RestoreState(assemblerState);
    }

    private void RemoveController(FlightController controller)
    {
        RemoveSubscriptions(controller);
        controller.DetachRosterBindings(_world.Projectiles);
        controller.GetParent()?.RemoveChild(controller);
        controller.QueueFree();
    }

    private void RemoveSubscriptions(FlightController controller)
    {
        if (!_aiSubscriptions.Remove(controller, out var subscriptions))
            return;
        if (subscriptions.Machine != null)
        {
            subscriptions.Machine.ModeChanged -= subscriptions.ModeChanged;
            subscriptions.Machine.RollLogged -= subscriptions.RollLogged;
        }
        controller.Downed -= subscriptions.Downed;
    }

    private sealed record AiSubscriptions(AiModeMachine? Machine,
        Action<AiMode, AiMode, string>? ModeChanged, Action<string>? RollLogged,
        Action<int, int?> Downed);
}
