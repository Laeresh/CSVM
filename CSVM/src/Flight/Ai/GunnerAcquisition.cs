using System;
using System.Collections.Generic;
using CSVM.Flight.Airframe;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Ai;

/// <summary>What the acquisition reads off its own aircraft for one tick. That is the shooter id its
/// log lines name, the team, the sim pose, the attack radius and the def's <c>struct_bias</c>. It
/// also carries the pools it sweeps and the ordnance the gasbag gate walks. Taken once per tick,
/// before either the hold or the sweep reads it.</summary>
public readonly record struct AcquiringShooter(
    int ShooterId,
    int Team,
    Vector3 Position,
    Vector3 Forward,
    float AttackRange,
    float StructBias,
    ProjectilePool? Pools,
    DestructibleRegistry? Structures,
    Loadout? Loadout,
    AiRocketeer? Rocketeer,
    bool InfiniteAmmo);

/// <summary>One AI aircraft's target acquisition. The decoded hold keeps a standing target while it
/// re-scores valid. Once it does not, the sweep ranks the VehicleList, the turrets and the structures
/// for one global minimum. The gasbag gate admits a gasbag only while the shooter's zeppelin ordnance
/// is ready. It runs on the sim step and shares no accumulator with forces, contact or damage. The
/// host supplies the shooter's view and fires on the target this leaves on the gunner
/// (docs/org/aiPilot.md "Target acquisition").</summary>
public sealed class GunnerAcquisition
{
    private const int RetargetLogCap = 8;   // switches logged per shooter; a flight of twelve re-scores all mission

    private readonly object _self;
    private readonly Func<AcquiringShooter> _shooter;
    private readonly AimCandidateSet _scan = new();          // the sweep's own scan, rebuilt per acquisition
    private readonly AimCandidateSet _rescoreScan = new();   // the aircraft-only walk the re-score's withdrawal reads
    private readonly List<RankedTargetCandidate> _candidates = new();
    private readonly List<object?> _sources = new();         // each candidate's source, by index
    // The names one non-aircraft candidate also answers to, refilled per candidate by
    // TargetPool.CollectOwners. A field, not a fresh list: the acquisition walks every pool.
    private readonly List<string> _biasOwners = new();
    private bool _loggedTarget;      // verification breadcrumb: the first acquisition logs once
    private int _retargetsLogged;

    /// <summary>Builds the acquisition for <paramref name="self"/>, which no pool ever offers back
    /// to it. <paramref name="shooter"/> answers the shooter's view at the moment of the call.</summary>
    public GunnerAcquisition(object self, Func<AcquiringShooter> shooter)
    {
        ArgumentNullException.ThrowIfNull(self);
        ArgumentNullException.ThrowIfNull(shooter);
        _self = self;
        _shooter = shooter;
    }

    /// <summary>The last sweep's ranked pool, one source per candidate in the order ranked: a
    /// <see cref="FlightController"/>, a <see cref="SurfaceVehicle"/>, a
    /// <see cref="TurretController"/> or a <see cref="DestructibleRegistry.Instance"/>.</summary>
    public IReadOnlyList<object?> RankedSources => _sources;

    /// <summary>How many structures the last sweep's scan admitted, before the team gate drops any.
    /// The decoded structure list is the narrower one, so only the scan shows what was offered.</summary>
    public int ScannedStructureCount => _scan.Structures.Count;

    /// <summary>One gunner tick: keep the standing target while the hold holds it, else re-acquire
    /// through the ranking when <see cref="AiGunner.AutoTarget"/> allows, leaving the pick on
    /// <see cref="AiGunner.Target"/>. True with the target's sim geometry when one is live. A
    /// <see cref="AiGunner.Disengaged"/> gunner drops its target and takes none.</summary>
    public bool Step(AiGunner gunner, out Vector3 position, out Vector3 velocity, out Vector3 forward)
    {
        ArgumentNullException.ThrowIfNull(gunner);
        if (gunner.Disengaged)
        {
            gunner.Target = null;
            position = velocity = forward = Vector3.Zero;
            return false;
        }

        var shooter = _shooter();
        if (Holds(gunner, shooter, out position, out velocity, out forward))
            return true;
        object? left = gunner.Target;
        TargetScore score = default;
        string how = "ranked";
        gunner.Target = gunner.AutoTarget ? Select(gunner, shooter, out score, out how) : null;
        if (!FlightController.TryTargetGeometry(gunner.Target, out position, out velocity, out forward,
                out bool live) || !live)
            return false;
        if (!_loggedTarget)
        {
            _loggedTarget = true;
            Log.Info("flight",
                $"ai gunner: shooter {shooter.ShooterId} targets {FlightController.TargetLabel(gunner.Target)} at {score.Distance:0} m ({how}: weight {score.Weight:0.0#} bias {score.Bias:0} rank {score.Rank:0}; gasbag ordnance {GasbagOrdnanceState(shooter)}, {_scan.Structures.Count} structure(s) in the scan)");
        }
        else if (_retargetsLogged < RetargetLogCap && !ReferenceEquals(left, gunner.Target))
        {
            // The switch is the thing the re-score exists for, so it is what gets logged.
            _retargetsLogged++;
            Log.Info("flight",
                $"ai gunner: shooter {shooter.ShooterId} leaves {FlightController.TargetLabel(left)} for {FlightController.TargetLabel(gunner.Target)} at {score.Distance:0} m ({how}: rank {score.Rank:0}; {_scan.Structures.Count} structure(s) in the scan)");
        }

        return true;
    }

    /// <summary>One sweep, as <see cref="Step"/> runs it once the hold lets go, past the hold. A
    /// ranked pick is taken through <see cref="AiGunner.TakeTarget"/>, which stamps the hold; an
    /// assigned one is only returned. <see cref="RankedSources"/> holds the pool it ranked.</summary>
    public object? Select(AiGunner gunner, out TargetScore score, out string how)
    {
        ArgumentNullException.ThrowIfNull(gunner);
        return Select(gunner, _shooter(), out score, out how);
    }

    // The name a rating_biases entry is matched against, read only when there is a list to match.
    // The ranking asks it per candidate per tick, and a Godot name read allocates (PERF-20).
    private static string BiasNameOf(object? source, AiGunner gunner) =>
        gunner.RatingBiases is { Count: > 0 } ? TargetPool.NameOf(source) : string.Empty;

    // The gasbag gate's verdict as a word. The decoded admission wants a DAMAGES_ZEPPELIN slot with
    // ammo whose two launch timers have run out (FUN_00420070, docs/org/aiPilot.md). Which condition
    // withheld the gasbags is what the log has to say.
    private static string GasbagOrdnanceState(in AcquiringShooter shooter)
    {
        if (shooter.Loadout is not { Hardpoints.Count: > 0 } fit)
            return "no pylons";
        if (shooter.Rocketeer is not { } rocketeer)
            return "no rocketeer";
        string state = "no gasbag pylon";
        for (int i = 0; i < fit.Hardpoints.Count; i++)
        {
            var hp = fit.Hardpoints[i];
            if (!hp.Weapon.DamagesZeppelin)
                continue;
            if (!hp.Armed(shooter.InfiniteAmmo))
                state = "gasbag pylon empty";
            else if (!rocketeer.SlotReady(i))
                state = "gasbag pylon locked";
            else
                return "ready";
        }
        return state;
    }

    // Whether this tick keeps the standing target. The decoded hold: a picked target is re-scored
    // every tick and kept while it scores valid. Once the hold runs out the pool is swept whole
    // (docs/org/aiPilot.md). A target written straight onto AiGunner.Target carries no rank
    // snapshot and keeps to the simpler rule that alive is enough. An assigned primary_target is
    // kept while it is inside the attack radius.
    private bool Holds(AiGunner gunner, in AcquiringShooter shooter, out Vector3 pos, out Vector3 vel,
        out Vector3 fwd)
    {
        if (!FlightController.TryTargetGeometry(gunner.Target, out pos, out vel, out fwd, out bool live)
            || !live)
            return false;
        float attack = shooter.AttackRange;
        // The assigned target is re-scored every tick in the original (FUN_0041fe10's primary arm),
        // so it stays only inside the attack volume. This is the range the promotion does not read.
        if (gunner.AutoTarget && gunner.IsPrimaryTarget(gunner.Target))
            return shooter.Position.DistanceSquaredTo(pos) <= attack * attack;
        if (!gunner.AutoTarget || !ReferenceEquals(gunner.TargetRankFor, gunner.Target))
            return true;
        if ((GameClock.Current?.Time ?? 0.0) >= gunner.TargetHoldUntil)
            return false;
        var rank = gunner.TargetRank;
        rank.Position = pos;   // the re-score runs on the live geometry; the bias terms stand
        rank.Velocity = vel;
        // ⚠ The withdrawal argument is CSVM's layer, not the decode. Evaluated last, and only for a
        // structure target, so the aircraft walk costs nothing on the ordinary aeroplane duel.
        return AiTargetRanking.KeepsStandingTarget(shooter.Position, shooter.Forward,
            attack, AiScorer.Jet,
            AiTargetRanking.AircraftFirst && rank.IsStructureClass && EnemyAircraftRanks(gunner, shooter),
            rank);
    }

    // Whether one live enemy aeroplane is in reach, the withdrawal's test, over the aircraft roster
    // alone: a hull is neither class the preference suppresses. It asks the two things
    // AiTargetRanking.SelectBest asks, the attack radius and an authored hard exclusion.
    private bool EnemyAircraftRanks(AiGunner gunner, in AcquiringShooter shooter)
    {
        if (shooter.Pools == null)
            return false;
        float attack = shooter.AttackRange;
        var ownPos = shooter.Position;
        _rescoreScan.Clear();
        shooter.Pools.CollectAircraft(_rescoreScan);
        foreach (var c in _rescoreScan.Vehicles)
        {
            if (!c.Live || c.Source is not FlightController fc || ReferenceEquals(fc, _self))
                continue;
            if (!AimAssist.Hostile(shooter.Team, c.Team)
                || ownPos.DistanceSquaredTo(c.Position) > attack * attack)
                continue;
            float bias = AiTargetRanking.ObjectiveBiasFor(
                fc.IsHumanPiloted ? AiTargetRanking.PlayerRole : BiasNameOf(c.Source, gunner),
                gunner.RatingBiases);
            if (bias < AiTargetRanking.NotRanked)
                return true;
        }

        return false;
    }

    // The sweep: the decoded ranking over the VehicleList, the turrets and the structures
    // (TargetVehicle/TargetTurret/TargetStruct), for one global minimum. It uses the aim assist's
    // roster and team gate. A live PrimaryTargetName is picked outright; its "player" token resolves
    // to the nearest human, which only an aeroplane can be. Decode: docs/org/aiPilot.md.
    // ⚠ Deconfliction (AiTargetRanking) stays zero outside a mission.
    private object? Select(AiGunner gunner, in AcquiringShooter shooter, out TargetScore score, out string how)
    {
        score = default;
        how = "ranked";
        if (shooter.Pools == null)
            return null;
        _scan.Clear();
        // ⚠ The whole VehicleList, not its aircraft half. The decoded sweep walks the one list that
        // holds the AI ground and sea vehicles beside the aircraft (docs/org/targeting.md).
        shooter.Pools.CollectVehicleList(_scan);
        shooter.Pools.CollectTurrets(_scan);
        if (shooter.Structures != null)
        {
            _scan.AddMissionStructures(shooter.Structures);
        }
        int ownTeam = shooter.Team;
        // ⚠ The attack volume, not the activation one. Both decoded scorers admit on the attack
        // cylinder, so a DEDG widening never reaches acquisition (docs/org/aiPilot.md).
        float attack = shooter.AttackRange;
        var ownPos = shooter.Position;
        var ownFwd = shooter.Forward;
        _candidates.Clear();
        _sources.Clear();
        object? primary = null;
        FlightController? nearestHuman = null;
        float nearestHumanDistSq = float.MaxValue;
        foreach (var c in _scan.Vehicles)
        {
            if (!c.Live || c.Source == null || ReferenceEquals(c.Source, _self))
                continue;
            if (c.Team == AimAssist.NeutralTeam || ownTeam == AimAssist.NeutralTeam || c.Team == ownTeam)
                continue;
            // ⚠ Read the source's TYPE, never gate on it. A hull is on this list as no
            // FlightController, so the terms that belong to an aeroplane do not apply to it.
            var fc = c.Source as FlightController;
            if (gunner.PrimaryTargetName is { Length: > 0 } wanted
                && ownPos.DistanceSquaredTo(c.Position) <= attack * attack)
            {
                if (primary == null
                    && string.Equals(TargetPool.NameOf(c.Source), wanted, StringComparison.OrdinalIgnoreCase))
                {
                    primary = c.Source; // a by-NAME assignment names one entry: first match is it
                }
                else if (fc is { IsHumanPiloted: true }
                    && wanted.Equals(AiTargetRanking.PlayerRole, StringComparison.OrdinalIgnoreCase))
                {
                    // "player" is a role, not a name; resolved ONCE per acquisition.
                    float d = ownPos.DistanceSquaredTo(c.Position);
                    if (d < nearestHumanDistSq)
                    {
                        nearestHumanDistSq = d;
                        nearestHuman = fc;
                    }
                }
            }

            // Wingman mode is the netless escort: a net demotes the mode to jet at spawn
            // (docs/org/aiPilot.md "Net assignment"). A hull flies neither, so both flags read false.
            bool human = fc is { IsHumanPiloted: true };
            _candidates.Add(new RankedTargetCandidate
            {
                Position = c.Position,
                Velocity = c.Velocity,
                IsPlayer = human,
                IsAircraft = fc != null,
                IsWingman = fc?.Pilot?.Escort != null,
                // target_bias is the CANDIDATE's own field on the vehicle arm. A hull carries no def
                // of its own here and spends nothing, as the shipped hull defs do.
                ClassBias = fc?.Stats?.AiTargetBias ?? 0f,
                ObjectiveBias = AiTargetRanking.ObjectiveBiasFor(
                    human ? AiTargetRanking.PlayerRole : BiasNameOf(c.Source, gunner),
                    gunner.RatingBiases),
                AlliedAttackers = AlliedAttackers(c.Source, ownTeam),
            });
            _sources.Add(c.Source);
        }

        // Turrets and structures: the other two pools, neither with a primary_target term. An
        // unauthored structure is neutral and never ranked; a gasbag passes only the ordnance gate.
        bool gasbagsAdmitted = GasbagOrdnanceState(shooter) == "ready";
        AddRankedNonAircraft(_scan.Turrets, isTurret: true, ownTeam, gunner, shooter.StructBias, gasbagsAdmitted);
        AddRankedNonAircraft(_scan.Structures, isTurret: false, ownTeam, gunner, shooter.StructBias, gasbagsAdmitted);

        bool byRole = primary == null && nearestHuman != null;
        primary ??= nearestHuman;
        if (primary != null)
        {
            // Log the assigned pick with its own rank inputs (informational, rank not consulted).
            int idx = _sources.IndexOf(primary);
            if (idx >= 0)
                score = AiTargetRanking.Score(ownPos, ownFwd, attack, AiScorer.Jet, _candidates[idx],
                    gunner.PlayersPreferred);
            how = byRole ? "primary target: nearest human" : "primary target";
            return primary;
        }

        // ⚠ Jet is asserted, not derived: the engine picks the scorer off the SHOOTER's own mode.
        // Deriving it would change what a mode plane or heli targets, a claim wanting its own evidence.
        int best = AiTargetRanking.SelectBest(ownPos, ownFwd, attack, AiScorer.Jet,
            AiTargetRanking.AircraftFirst, _candidates, out score, gunner.PlayersPreferred);
        if (best < 0)
            return null;
        // Only the ranked arm stamps the hold. An assigned primary_target wins outright at every
        // acquisition in the original, so it never reaches the hold.
        gunner.TakeTarget(_sources[best], _candidates[best], GameClock.Current?.Time ?? 0.0);
        return _sources[best];
    }

    // Files one turret or structure candidate into the shared rank pool, on the vehicle loop's team
    // gate and allied-attacker count (TargetTurret/TargetStruct). A gasbag is dropped at admission
    // unless the gate admitted gasbags, the original's FUN_0041f9c0 third argument.
    private void AddRankedNonAircraft(List<AimCandidate> pool, bool isTurret, int ownTeam,
        AiGunner gunner, float structBias, bool gasbagsAdmitted)
    {
        foreach (var c in pool)
        {
            if (!c.Live || c.Source == null || ReferenceEquals(c.Source, _self))
                continue;
            if (c.Team == AimAssist.NeutralTeam || ownTeam == AimAssist.NeutralTeam
                || c.Team == ownTeam)
                continue;
            // A carried turret's host is already ranked as a vehicle above; offering it again
            // here would put two entries on one silhouette. Mirrors TargetPool.Offer's guard.
            if (isTurret && !TargetPool.IsEmplacement(c.Source))
                continue;
            bool gasbag = c.Source is DestructibleRegistry.Instance { Gasbag: true };
            if (gasbag && !gasbagsAdmitted)
                continue;
            // A part answers to every name above it, which carries a roster's exclusion naming a
            // structure down onto its own engines and guns. Skipped with no list to match against.
            _biasOwners.Clear();
            if (gunner.RatingBiases is { Count: > 0 })
                TargetPool.CollectOwners(c.Source, _biasOwners);

            _candidates.Add(new RankedTargetCandidate
            {
                Position = c.Position,
                Velocity = c.Velocity,
                IsPlayer = false,
                IsStructureClass = true,
                IsGasbag = gasbag,
                // struct_bias is the SCORER's own field, spent on every turret and structure
                // candidate alike. It is read off the shooter and never off the candidate.
                ClassBias = structBias,
                ObjectiveBias = AiTargetRanking.ObjectiveBiasFor(
                    BiasNameOf(c.Source, gunner), _biasOwners,
                    gunner.RatingBiases, isTurret),
                AlliedAttackers = AlliedAttackers(c.Source, ownTeam),
            });
            _sources.Add(c.Source);
        }
    }

    // Allied gunners already on this candidate, the deconfliction input.
    private int AlliedAttackers(object source, int ownTeam)
    {
        int attackers = 0;
        foreach (var a in _scan.Vehicles)
        {
            if (a.Team == ownTeam && a.Source is FlightController ally
                && !ReferenceEquals(ally, _self)
                && ReferenceEquals(ally.Pilot?.Gunner?.Target, source))
                attackers++;
        }

        return attackers;
    }
}
