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
/// (BL-401). <c>PilotName</c> is the block's slot-20 key, the readout's name (docs/org/targeting.md).
/// <c>InitHealth</c>/<c>Armor</c> override the hull pools pre-difficulty-scale (docs/org/vehicleDamage.md).</summary>
public readonly record struct AiSpawn(string PlaneName, Vector3 Position, Vector3 LookAt, AiPilot Pilot,
    PaintScheme? Scheme = null, int? Team = null, bool Inert = false, bool ShippedSkins = false,
    string? AiDef = null, LoadoutChoice? Fit = null, int? AttackRating = null, bool Nitro = false,
    AiSkillVector? RosterSkills = null, string? NodeName = null, string? PilotName = null,
    // Overrides the session difficulty for this one spawn, which is all an Instant Action wave's
    // skill is (Flight.Difficulty); null takes the session's.
    int? Difficulty = null, float? InitHealth = null, float? Armor = null);

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
    private readonly string _zrdrPath;
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
        _zrdrPath = aircraft.ZrdrPath;
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
                    BindSessionSinks(controller);
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

    /// <summary>Puts one player into a different airframe without ending the mission: the rig's
    /// aircraft is rebuilt from <paramref name="planeNode"/>'s own record, at the pose it was flying,
    /// in <paramref name="scheme"/> (<paramref name="shippedSkins"/> says a null one is that rig's
    /// own reading, not the ordinary player paint) and on <paramref name="build"/> in place of the
    /// stock fit. ⚠ Rounds already airborne ride the shared pool under this pilot's unchanged
    /// shooter id. Decode: cutscenes.md.</summary>
    public FlightController SwapPlayerAirframe(PlayerRig rig, string planeNode, PaintScheme? scheme = null,
        bool shippedSkins = false, CustomPlaneDef? build = null)
    {
        ArgumentNullException.ThrowIfNull(rig);
        if (_players == null)
            throw new InvalidOperationException("an airframe swap needs a flight-start policy");
        if (rig.Controller is not { } outgoing)
            throw new InvalidOperationException("an airframe swap needs an aircraft to swap out of");

        // The sim pose, not the node's: the node lags it by the render interpolation, and a swap
        // that read the drawn frame would put the replacement a frame behind where it was flying.
        var start = new FlightStart(outgoing.WorldPosition,
            outgoing.WorldPosition + outgoing.NoseDirection, outgoing.Throttle,
            outgoing.WorldVelocity.Length());

        // The paint stream is shared with every AI spawn still to come, so the replacement's own
        // livery draw is rolled back afterwards: a mission handing the player an airframe must not
        // change what the next wave is painted.
        var assemblerState = _players.CaptureState();

        // ⚠ Order is forced. DetachRosterBindings drops every near-miss registration carrying this
        // pilot's shooter id and the replacement registers its own under the same id, so the
        // outgoing aircraft goes first. A build that throws then leaves the rig aircraft-less.
        RemoveController(outgoing);
        rig.Controller = null;
        try
        {
            _players.Assemble(rig.Index, rig, _ => { },
                new AirframeSwapRequest(planeNode, start, scheme, shippedSkins, build));
        }
        finally
        {
            _players.RestoreState(assemblerState);
        }

        var controller = rig.Controller
            ?? throw new InvalidOperationException($"airframe swap to '{planeNode}' built no aircraft");
        BindSessionSinks(controller);
        Log.Info("flight", $"airframe swap: P{rig.Index + 1} is now flying '{planeNode}'");
        return controller;
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

    /// <summary>The airframe and paint one human rig is flying. Read before a swap replaces it: the
    /// mission-script hand-over gives the aeroplane the player is leaving to another pilot.</summary>
    internal FlyingAirframe? FlyingAirframeOf(int rigIndex) => _players?.Flying(rigIndex);

    /// <summary>The live AI aircraft carrying <paramref name="name"/>, or null. Roster blocks name
    /// their own spawns, so this is the same identity a definition's root node names.
    /// </summary>
    internal FlightController? AiNamed(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        foreach (var ai in _ai)
        {
            if (string.Equals(ai.Name.ToString(), name, StringComparison.OrdinalIgnoreCase))
            {
                return ai;
            }
        }

        return null;
    }

    /// <summary>The whole of one mission-script airframe swap, as codes 965 to 967 raise it: the
    /// rig rebuilt on the named airframe, the capture animation's own aircraft hidden with what is
    /// left of its hull and its own livery carried onto the new one (967), and the aeroplane the
    /// player just left handed to <see cref="AirframeHandover.WingmanName"/> off the nose when
    /// <paramref name="handsOver"/> says this mission resolves that name. What the capture half hid
    /// comes back in the <see cref="AirframeSwapResult"/>.</summary>
    internal AirframeSwapResult RunSwap(PlayerRig rig, AirframeSwapOrder order, bool handsOver)
    {
        ArgumentNullException.ThrowIfNull(rig);
        if (rig.Controller is not { } outgoing)
        {
            return default;
        }

        // Measured off the hull the player is LEAVING, before the rebuild tears it down. Both are
        // whole-vehicle currents, which is what the original's four per-section reads sum to.
        var leaving = FlyingAirframeOf(rig.Index);
        float armorLeft = outgoing.Damage?.WholeArmor ?? 0f;
        float healthLeft = outgoing.Damage?.WholeHealth ?? 0f;
        var wasAt = outgoing.WorldPosition;
        var wasNose = outgoing.NoseDirection;
        var captured = AirframeHandover.CarriesCapturedDamage(order.Airframe)
            ? AiNamed(order.CaptureRoot)
            : null;
        // The captured rig's own scheme rides the rebuild (undecoded in the executable, so this is
        // the user's own controls reading) with its ShippedSkins reading, since a real enemy spawn
        // resolves to no scheme at all and null must beat default; 965 draws its shipped skins.
        var build = order.Airframe.AwardAirframe is { } awardAirframe
            ? CampaignProgression.AwardBuild(awardAirframe)
            : null;
        var replacement = SwapPlayerAirframe(rig, order.Airframe.PlaneNode, captured?.Scheme,
            captured?.ShippedSkins ?? order.Airframe.ShippedSkins, build);
        RepointHolders(outgoing, replacement);
        var hidden = CarryCapturedDamage(captured, rig.Controller?.Damage);
        if (captured != null && AirframeHandover.CarriesCapturedGroup(order.Airframe))
        {
            replacement.Group = captured.Group;
        }

        if (handsOver)
        {
            HandOverOutgoing(leaving, wasAt, wasNose, armorLeft, healthLeft);
        }

        return new AirframeSwapResult(true, hidden);
    }

    // Code 967's other half: the aircraft the capture animation belongs to goes out of the world,
    // and the fractions left of ITS hull scale the new airframe's zones, so the player inherits
    // the Balmoral they shot at rather than a pristine one.
    private static FlightController? CarryCapturedDamage(FlightController? captured, PlaneDamage? fresh)
    {
        if (captured?.Damage is not { } hull || fresh == null)
        {
            return null;
        }

        float armor = hull.WholeArmorMax > 0f ? hull.WholeArmor / hull.WholeArmorMax : 1f;
        float health = hull.WholeHealthMax > 0f ? hull.WholeHealth / hull.WholeHealthMax : 1f;
        captured.Inert = true;
        fresh.ScalePools(armor, health);
        Log.Info("flight", $"airframe swap: '{captured.Name}' hidden, its hull (armour {armor * 100f:0}%, structure {health * 100f:0}%) carried onto the player's");
        return captured;
    }

    // Step 2's re-point walk: every AI pilot holding the aircraft the player just left, as its
    // escort leader or its standing quarry, holds the replacement instead. The outgoing node is
    // freed by the rebuild, so a holder left on it steers on a disposed object every frame.
    private void RepointHolders(FlightController outgoing, FlightController replacement)
    {
        int repointed = 0;
        foreach (var ai in _ai)
        {
            if (ai.Pilot is not { } pilot)
            {
                continue;
            }

            if (pilot.Escort is { } escort && ReferenceEquals(escort.Leader, outgoing))
            {
                escort.Leader = replacement;
                repointed++;
            }

            if (pilot.Gunner is { } gunner && ReferenceEquals(gunner.Target, outgoing))
            {
                gunner.Target = replacement;
                repointed++;
            }
        }

        if (repointed > 0)
        {
            Log.Info("flight", $"airframe swap: {repointed} AI reference(s) to the outgoing aircraft re-pointed onto the replacement");
        }
    }

    // The session-wide sinks a human controller takes after assembly rather than during it. One
    // place, because an airframe swap builds a replacement that has to end up bound to exactly what
    // the initial build bound.
    private void BindSessionSinks(FlightController controller)
    {
        controller.SmokeScreens = _human.SmokeScreens;
        controller.PauseState = _human.PauseState;
        controller.TargetSubParts = _targetSubParts;
        controller.TargetObjectives = _targetObjectives;
    }

    // Step 5: the aeroplane the player just left is given to wingman_4, placed off the nose with
    // the sums measured off that aeroplane, and revealed. The block is authored deactivated, so
    // until this call it is a built-inert aircraft nobody can see.
    private void HandOverOutgoing(FlyingAirframe? leaving, Vector3 wasAt, Vector3 wasNose,
        float armor, float health)
    {
        if (AiNamed(AirframeHandover.WingmanName) is not { } wingman)
        {
            Log.Info("flight", $"airframe swap: no '{AirframeHandover.WingmanName}' in this mission's roster, so the outgoing aeroplane is not handed over");
            return;
        }

        var (position, lookAt) = AirframeHandover.Placement(wasAt, wasNose);
        wingman.Activate(position, lookAt);
        wingman.Damage?.SetWholePools(armor, health);
        Log.Info("flight", $"airframe swap: '{wingman.Name}' takes the player's '{leaving?.PlaneNode ?? "?"}' {AirframeHandover.RangeM:0} m off the nose at {AirframeHandover.BearingDeg:0} deg, armour {armor:0.#} structure {health:0.#}");
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
