using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>The decoded AI mode vocabulary: the nine states the original engine's debug
/// readout dispatches on, in its own naming (docs/formats/ai-rosters.md "AI modes, engine-side").
/// There is no flee and no inactive mode in that dispatch.</summary>
public enum AiMode
{
    /// <summary>Flying the assigned net or holding course.</summary>
    Patrol,

    /// <summary>Chasing the target; the gunner fires in this mode.</summary>
    Pursue,

    /// <summary>The rubber-band assist: a pursued AI lets its human pursuer catch up —
    /// course held, throttle eased by the decoded <c>sixth_sense_factor</c>, fire held.
    /// Disabled session-wide by <c>--no-assist</c> (<see cref="AiModeMachine.AssistEnabled"/>).</summary>
    LayOff,

    /// <summary>Broken off after a failed steady-hand test.</summary>
    Evade,

    /// <summary>Playing one maneuver-library program through <see cref="ManeuverExecutor"/>.</summary>
    EvasiveManeuver,

    /// <summary>Controls neutral for <c>stun_recovery_interval</c> after a failed sixth-sense test.</summary>
    Stunned,

    /// <summary>Obstacle-closure override (terrain or another aircraft dead ahead): climb out,
    /// everything else waits.</summary>
    AvoidCrash,

    /// <summary>Danger-zone approach. Never entered yet — see <see cref="AiModeMachine"/>.</summary>
    ApproachingDangerZone,

    /// <summary>Danger-zone run. Never entered yet — see <see cref="AiModeMachine"/>.</summary>
    NavigatingDangerZone,
}

/// <summary>The nine-mode AI state machine, owned by an <see cref="AiPilot"/> and stepped
/// once per sim tick from <see cref="AiPilot.Next"/>. The mode list and its vocabulary are
/// decoded (docs/formats/ai-rosters.md "AI modes, engine-side"); which transitions are decoded
/// and which are invented is recorded in docs/architecture.md's entry for this file, and each
/// invented constant below says so at its own declaration.
/// Config fields are plain and mutable BY DESIGN — mission scripts rewrite them at runtime
/// (<c>SET_AI_ATTACK_RADIUS</c> and friends). All randomness is this machine's own seeded
/// stream, so a fixed-seed run transitions identically. The obstacle probe is an injected
/// delegate and target state arrives as a snapshot, so the transition table unit-tests without
/// a scene tree (<c>AiModeMachineTests</c>).
/// ⚠ The two danger-zone modes are enum-only and never entered. Do not invent an entry
/// condition for them.</summary>
public sealed class AiModeMachine
{
    /// <summary>Invented: how long a plain (maneuver-less) evade runs before returning.</summary>
    public const float EvadeDurationS = 8f;

    /// <summary>Invented: seconds between evade heading scrambles.</summary>
    public const float EvadeScrambleIntervalS = 2f;

    /// <summary>Invented: obstacle-probe cadence, seconds.</summary>
    public const float ProbeIntervalS = 0.25f;

    /// <summary>Obstacle-probe lookahead, seconds of current velocity — decoded: the original's
    /// ray is the vehicle's own velocity scaled by 4.5 (<c>FUN_0041f810</c>), so 4.5 seconds of
    /// travel. Was an invented 2.5 until the reach was read out of the image.</summary>
    public const float ProbeLookaheadS = 4.5f;

    /// <summary>Invented: minimum probe length, metres (a slow plane still looks ahead).</summary>
    public const float ProbeMinLookaheadM = 120f;

    /// <summary>Invented: the second probe slants this far below the lookahead point, so shallow
    /// terrain under a level flight path still registers.</summary>
    public const float ProbeDeckM = 40f;

    /// <summary>Invented: the avoid-crash climb-out altitude gain, metres.</summary>
    public const float ClimbOutM = 250f;

    /// <summary>Invented: consecutive clear probe rounds before avoid-crash releases.</summary>
    public const int ClearProbesToExit = 4;

    /// <summary>Invented: selection weight multiplier for a roster-authored signature maneuver
    /// ("weighted up during selection" is decoded; the factor is not).</summary>
    public const int SignatureWeight = 3;

    /// <summary>Invented: a pursuing human this far behind has "fallen behind" — pursue eases
    /// into lay off past this gap. The mode and the intent are decoded; the distance is not.</summary>
    public const float LayOffEnterRangeM = 350f;

    /// <summary>Invented: the pursuer has "caught up" inside this gap — lay off returns to
    /// pursue (hysteresis against <see cref="LayOffEnterRangeM"/>).</summary>
    public const float LayOffCaughtUpRangeM = 250f;

    /// <summary>Invented: the pursued test's rear cone — the pursuer must sit within this
    /// half-angle of the AI's tail axis (its velocity, reversed).</summary>
    public const float LayOffRearConeDeg = 60f;

    /// <summary>Invented: the pursued test's chase cone — the pursuer's velocity must point
    /// within this half-angle of the line to the AI, i.e. it is actually chasing.</summary>
    public const float LayOffPursuerConeDeg = 30f;

    /// <summary>Invented: minimum lay-off dwell, seconds — an anti-chatter hold before any
    /// lay-off exit condition is honoured.</summary>
    public const float LayOffMinHoldS = 2f;

    /// <summary>Invented: the pursued geometry must hold continuously this long before pursue
    /// eases into lay off. Kills single-frame misfires in a turning fight, where the tail-axis
    /// test can pass for a moment mid-maneuver and the dwell would then latch it (user-reported
    /// 2026-08-14: an enemy behind the player appearing to slow down).</summary>
    public const float LayOffSustainS = 1.5f;

    /// <summary>Activation radius, metres — player.json's <c>min_ai_active_dist</c> (2000 shipped),
    /// the fallback for every roster whose own volume slots are unauthored (all of them).
    /// A target outside it is not ranked at all (the engine scores it 1e21).</summary>
    public float ActivationRange = 2000f;

    /// <summary>Attack radius, metres — vehicle.json's <c>attack</c> (2000 shipped, on
    /// <c>basic_airplane</c>, inherited install-wide). Pursue is entered when a target sits
    /// inside both this and <see cref="ActivationRange"/>.</summary>
    public float AttackRange = 2000f;

    /// <summary>Chase leash, metres — vehicle.json's <c>return_range</c> (1200 shipped). Our
    /// reading (the anchor is undecoded): pursuit is abandoned when the aircraft has strayed
    /// farther than this from where the pursuit began AND the target sits outside the
    /// activation radius.</summary>
    public float ReturnRange = 1200f;

    /// <summary>Probability that a hit's steady-hand test FAILS and the pilot evades —
    /// <c>steady_hand_chance</c> (0.5 → 0.08 over the pair; lower is the better pilot). The
    /// default here is the pair's raw low endpoint, not the rating-1 value (rating/9 interpolation
    /// puts rating 1 partway toward the high endpoint already — see <see cref="AiSkills.At"/>).
    /// The design's damage weighting on this roll is undecoded and not modelled.</summary>
    public float SteadyHandChance = 0.5f;

    /// <summary>Probability that the sixth-sense test PASSES (the pilot follows the target's
    /// maneuver) — <c>sixth_sense_chance</c> (0.45 → 0.71 over the pair). A failure stuns.</summary>
    public float SixthSenseChance = 0.45f;

    /// <summary>How long a stun lasts — <c>stun_recovery_interval</c> (4.8 s → 0.6 s over the
    /// pair).</summary>
    public float StunRecoveryIntervalS = 4.8f;

    /// <summary>The decoded ease-off factor applied while being pursued —
    /// <c>sixth_sense_factor</c> (0.994 → 1.07 over the pair): the fraction of the
    /// pursuer's speed a laying-off pilot flies at, so a poor pilot lets the player close and
    /// an ace pulls away. The constant is decoded; the speed-matching application point is our
    /// reading (<see cref="AiPilot"/>).</summary>
    public float SixthSenseFactor = 0.994f;

    /// <summary>The rubber-band assist switch: false (<c>--no-assist</c>) means
    /// <see cref="AiMode.LayOff"/> is never entered by <see cref="Update"/> — pursue only, the
    /// original's assist off. Default true, the original's behaviour. External
    /// <see cref="Enter"/> overrides (script/tests) are deliberately not gated.</summary>
    public bool AssistEnabled = true;

    /// <summary>The pilot's 1–9 <c>natural_touch</c>, compared directly against maneuver
    /// difficulty (no interpolation table, by design).</summary>
    public int NaturalTouch = 1;

    /// <summary>The maneuver library, or null for a maneuver-less pilot (a failed
    /// steady-hand test then evades plainly instead).</summary>
    public IReadOnlyList<Maneuver>? Library;

    /// <summary>The roster's signature-maneuver names (<see cref="Maneuvers.SignatureNames"/>),
    /// weighted up <see cref="SignatureWeight"/>× during selection; null/empty = none.</summary>
    public IReadOnlyCollection<string>? SignatureManeuvers;

    /// <summary>Line-of-sight probe for the avoid-crash test: static world plus other aircraft,
    /// never the caster's own body (docs/org/aiPilot.md "What the ray can hit"). Returns the
    /// struck body's name, or null for a clear line — the name lets the transition log say
    /// whether the override fired on terrain or another aircraft. The host wires
    /// <c>FlightController.AvoidCrashBlocksLine</c>; a null delegate means no world data and the
    /// mode is never entered.</summary>
    public Func<Vector3, Vector3, string?>? ProbeBlocked;

    private readonly Random _rng;

    private AiMode _returnMode = AiMode.Patrol;
    private AiMode? _lastTargetMode;
    private Vector3 _pursuitAnchor;
    private Vector3 _lastPos;
    private Vector3 _lastVelocity;
    private Vector3? _nose;
    private float _layOffHold;
    private float _pursuedFor;
    private float _stunRemaining;
    private float _evadeRemaining;
    private float _evadeScramble;
    private float _probeCooldown;
    private int _clearProbes;

    public AiModeMachine(Random rng)
    {
        _rng = rng;
    }

    /// <summary>Every transition, with the modes and a short reason — the observability seam
    /// (the session logs these in the engine's own mode vocabulary).</summary>
    public event Action<AiMode, AiMode, string>? ModeChanged;

    /// <summary>Every steady-hand / sixth-sense roll's outcome, phrased in the engine's own
    /// vocabulary (pass and fail alike, so a quiet run is distinguishable from a lucky one).</summary>
    public event Action<string>? RollLogged;

    /// <summary>The current mode. Transitions go through the machine; <see cref="Enter"/> is the
    /// external override (mission script, tests).</summary>
    public AiMode Mode { get; private set; } = AiMode.Patrol;

    /// <summary>The executor playing the current evasive maneuver; non-null exactly while
    /// <see cref="Mode"/> is <see cref="AiMode.EvasiveManeuver"/>.</summary>
    public ManeuverExecutor? Executor { get; private set; }

    /// <summary>Evade's current heading order, degrees (mission-data convention).</summary>
    public float EvadeHeadingDeg { get; private set; }

    /// <summary>Evade's current altitude order, metres.</summary>
    public float EvadeAltitude { get; private set; }

    /// <summary>Avoid-crash's climb-out altitude order (entry altitude + <see cref="ClimbOutM"/>).</summary>
    public float ClimbOutAltitude { get; private set; }

    /// <summary>Lay off's course order, degrees: the heading flown at entry, held so the pilot
    /// stays ahead of the pursuer instead of turning back into a head-on.</summary>
    public float LayOffHeadingDeg { get; private set; }

    /// <summary>Lay off's altitude order, metres: the entry altitude.</summary>
    public float LayOffAltitude { get; private set; }

    /// <summary>The engine's own name for a mode — the debug-readout vocabulary, verbatim.</summary>
    public static string NameOf(AiMode mode) => mode switch
    {
        AiMode.Patrol => "patrol",
        AiMode.Pursue => "pursue",
        AiMode.LayOff => "lay off",
        AiMode.Evade => "evade",
        AiMode.EvasiveManeuver => "evasive maneuver",
        AiMode.Stunned => "stunned",
        AiMode.AvoidCrash => "avoid crash",
        AiMode.ApproachingDangerZone => "approaching danger zone",
        AiMode.NavigatingDangerZone => "navigating danger zone",
        _ => mode.ToString(),
    };

    /// <summary>External mode override — the mission-script / test seam. Resets the overridden
    /// mode's own timers so a forced state behaves as if entered normally.</summary>
    public void Enter(AiMode mode, string reason = "ordered")
    {
        Transition(mode, reason);
    }

    /// <summary>One sim tick's transitions. <paramref name="targetPos"/> is the standing target's
    /// position or null; <paramref name="targetMode"/> is its own machine's mode when the target
    /// is an AI aircraft, for the sixth-sense trigger. A human target reports null: the
    /// sixth-sense roll fires only against AI targets today (undecoded for a human).
    /// <paramref name="targetVelocity"/>/<paramref name="targetIsHuman"/> feed the lay-off
    /// pursued test, extended only to a human-piloted pursuer.</summary>
    public AiMode Update(Vector3 pos, Vector3 velocity, Vector3? targetPos, AiMode? targetMode,
        float dt, Vector3? targetVelocity = null, bool targetIsHuman = false, Vector3? nose = null)
    {
        _lastPos = pos;
        _lastVelocity = velocity;
        _nose = nose;
        if (Mode == AiMode.Stunned)
        {
            // Nothing interrupts a stun: the pilot has no controls to react with.
            _stunRemaining -= dt;
            _lastTargetMode = targetMode;
            if (_stunRemaining <= 0f)
                Transition(_returnMode, "stun recovered");
            return Mode;
        }

        // The sixth-sense trigger (decoded vocabulary: "AI has been evaded"): the pursued
        // target broke into an evasive state since last tick. Rolled before the terrain
        // override so the roll is not masked by an avoid-crash frame.
        if (Mode is AiMode.Pursue or AiMode.LayOff
            && targetMode is AiMode.Evade or AiMode.EvasiveManeuver
            && _lastTargetMode is not (AiMode.Evade or AiMode.EvasiveManeuver))
        {
            NotifyTargetEvaded();
        }
        _lastTargetMode = targetMode;
        if (Mode == AiMode.Stunned)
            return Mode; // the roll above just stunned us; timers start next tick

        UpdateAvoidCrash(pos, velocity, dt);

        switch (Mode)
        {
            case AiMode.Patrol:
                if (targetPos is { } t
                    && pos.DistanceTo(t) <= Mathf.Min(ActivationRange, AttackRange))
                {
                    _pursuitAnchor = pos;
                    Transition(AiMode.Pursue, $"target at {pos.DistanceTo(t):0} m");
                }
                break;

            case AiMode.Pursue:
            case AiMode.LayOff:
                if (targetPos is not { } tp)
                {
                    Transition(AiMode.Patrol, "target lost");
                }
                else if (pos.DistanceTo(tp) > ActivationRange
                    && pos.DistanceTo(_pursuitAnchor) > ReturnRange)
                {
                    Transition(AiMode.Patrol, "beyond return range");
                }
                else
                {
                    UpdateLayOff(pos, velocity, tp, targetVelocity, targetIsHuman, dt);
                }
                break;

            case AiMode.Evade:
                _evadeRemaining -= dt;
                _evadeScramble -= dt;
                if (_evadeRemaining <= 0f)
                {
                    ReturnFromReaction(pos, targetPos);
                }
                else if (_evadeScramble <= 0f)
                {
                    _evadeScramble = EvadeScrambleIntervalS;
                    // Invented: an unpredictable 60–120° swing to a random side, seeded.
                    EvadeHeadingDeg = Mathf.Wrap(
                        EvadeHeadingDeg + RandomSign() * (60f + 60f * (float)_rng.NextDouble()),
                        -180f, 180f);
                }
                break;

            case AiMode.EvasiveManeuver:
                if (Executor is not { Done: false })
                {
                    Executor = null;
                    ReturnFromReaction(pos, targetPos);
                }
                break;
        }
        return Mode;
    }

    /// <summary>The hit path's entry (decoded reaction): rolls the steady-hand test on the
    /// absorbed damage; a FAILED test breaks off into an evasive maneuver when an eligible one
    /// exists, plain evade otherwise. <paramref name="threatDir"/> is a world-space hint toward
    /// the threat (the remake passes the impact offset — the shooter's position is not carried
    /// on the round); evade's first heading turns away from it.</summary>
    public void NotifyDamage(float absorbed, Vector3 threatDir)
    {
        if (Mode is AiMode.Stunned or AiMode.AvoidCrash)
            return; // no controls / emergency override — nothing to break off
        bool failed = _rng.NextDouble() < SteadyHandChance;
        RollLogged?.Invoke(FormattableString.Invariant(
            $"absorbed {absorbed:0.0} damage; steady hand test ")
            + (failed ? "failed. Evading." : "passed. Not evading."));
        if (!failed)
            return;
        if (Mode is AiMode.Pursue or AiMode.LayOff or AiMode.Patrol)
            _returnMode = Mode;
        if (Mode == AiMode.EvasiveManeuver)
            return; // already flying one out; let it finish
        if (PickManeuver() is { } maneuver)
        {
            Executor = new ManeuverExecutor(maneuver);
            Transition(AiMode.EvasiveManeuver,
                $"'{maneuver.Name}' natural touch {maneuver.Difficulty}/{NaturalTouch}");
        }
        else
        {
            StartPlainEvade(threatDir);
        }
    }

    /// <summary>The position-update entry (decoded vocabulary: "AI has been evaded"): rolls the
    /// sixth-sense test; a FAILED test stuns for <see cref="StunRecoveryIntervalS"/>. Called by
    /// <see cref="Update"/> when a pursued AI target enters an evasive state; public because
    /// the human-target trigger is undecoded and scripts/tests fire it directly.</summary>
    public void NotifyTargetEvaded()
    {
        if (Mode is not (AiMode.Pursue or AiMode.LayOff))
            return;
        bool passed = _rng.NextDouble() < SixthSenseChance;
        RollLogged?.Invoke("AI has been evaded. Sixth sense test " +
            (passed ? "passed." : "failed; AI now stunned."));
        if (passed)
            return;
        _returnMode = Mode;
        _stunRemaining = StunRecoveryIntervalS;
        Transition(AiMode.Stunned, FormattableString.Invariant($"for {StunRecoveryIntervalS:0.0} s"));
    }

    // Evade needs an initial course even when entered externally: away from the
    // threat, offset randomly (invented behaviour — the decode is thin past "Evading.").
    private void StartPlainEvade(Vector3 threatDir)
    {
        _evadeRemaining = EvadeDurationS;
        _evadeScramble = EvadeScrambleIntervalS;
        var away = -threatDir;
        EvadeHeadingDeg = Mathf.Wrap(
            (new Vector2(away.X, away.Z).LengthSquared() > 1e-4f
                ? AiPilot.HeadingDegOf(away)
                : 360f * (float)_rng.NextDouble())
            + RandomSign() * 30f * (float)_rng.NextDouble(),
            -180f, 180f);
        EvadeAltitude = _lastPos.Y + RandomSign() * 150f * (float)_rng.NextDouble();
        Transition(AiMode.Evade, "breaking off");
    }

    // Where a finished reaction goes back to: the prior mode when its conditions still
    // hold, patrol otherwise.
    private void ReturnFromReaction(Vector3 pos, Vector3? targetPos)
    {
        bool targetInRange = targetPos is { } t && pos.DistanceTo(t) <= ActivationRange;
        var back = _returnMode is AiMode.Pursue or AiMode.LayOff && targetInRange
            ? _returnMode
            : AiMode.Patrol;
        if (back == AiMode.Pursue || back == AiMode.LayOff)
            _pursuitAnchor = pos;
        Transition(back, "reaction complete");
    }

    // The rubber-band assist's transitions (decoded: the mode, its "let the player catch up"
    // intent, and sixth_sense_factor; the geometry is invented, named on the constants above).
    // Pursue eases into lay off when a chasing human target has fallen behind; lay off returns
    // when the pursuer catches up or stops chasing. AssistEnabled false never enters.
    private void UpdateLayOff(Vector3 pos, Vector3 velocity, Vector3 targetPos,
        Vector3? targetVelocity, bool targetIsHuman, float dt)
    {
        _layOffHold -= dt;
        float gap = pos.DistanceTo(targetPos);
        bool pursued = AssistEnabled && targetIsHuman
            && IsPursuedBy(pos, velocity, targetPos, targetVelocity);
        if (Mode == AiMode.Pursue)
        {
            // Entry needs the geometry SUSTAINED, not one passing frame: a turning fight can
            // satisfy the tail-axis test momentarily, and the dwell would latch the misfire.
            _pursuedFor = pursued && gap > LayOffEnterRangeM ? _pursuedFor + dt : 0f;
            if (_pursuedFor >= LayOffSustainS)
            {
                Transition(AiMode.LayOff, FormattableString.Invariant(
                    $"pursuer {gap:0} m behind for {_pursuedFor:0.0} s; easing off x{SixthSenseFactor:0.00}"));
            }
        }
        else if (!AssistEnabled)
        {
            Transition(AiMode.Pursue, "assist off");
        }
        else if (_layOffHold <= 0f)
        {
            if (gap < LayOffCaughtUpRangeM)
                Transition(AiMode.Pursue, FormattableString.Invariant($"pursuer caught up at {gap:0} m"));
            else if (!pursued)
                Transition(AiMode.Pursue, "no longer pursued");
        }
    }

    // The pursued test (invented geometry): the target sits within LayOffRearConeDeg of the tail
    // axis and its velocity points within LayOffPursuerConeDeg of the AI, i.e. it is chasing.
    // The tail axis is the NOSE when supplied, velocity only as fallback — mid-maneuver the two
    // diverge and velocity alone let the test pass for a frame with the enemy behind the player.
    private bool IsPursuedBy(Vector3 pos, Vector3 velocity, Vector3 targetPos,
        Vector3? targetVelocity)
    {
        var axis = _nose is { } n && n.LengthSquared() > 1e-4f ? n : velocity;
        if (targetVelocity is not { } tv || tv.LengthSquared() < 1e-4f
            || axis.LengthSquared() < 1e-4f)
            return false;
        var toTarget = targetPos - pos;
        if (toTarget.LengthSquared() < 1e-4f)
            return false;
        float behindCos = (-axis.Normalized()).Dot(toTarget.Normalized());
        if (behindCos < Mathf.Cos(Mathf.DegToRad(LayOffRearConeDeg)))
            return false;
        float chaseCos = tv.Normalized().Dot((-toTarget).Normalized());
        return chaseCos >= Mathf.Cos(Mathf.DegToRad(LayOffPursuerConeDeg));
    }

    // The obstacle-closure override (invented probe geometry, marked above): two rays
    // along the velocity lookahead, every ProbeIntervalS, seeing world and other
    // aircraft alike (decoded; the caster alone is excluded). Blocked → avoid crash (dropping a
    // running maneuver); clear for ClearProbesToExit rounds → back.
    private void UpdateAvoidCrash(Vector3 pos, Vector3 velocity, float dt)
    {
        if (ProbeBlocked is not { } probe)
            return;
        _probeCooldown -= dt;
        if (_probeCooldown > 0f)
            return;
        _probeCooldown = ProbeIntervalS;

        float speed = velocity.Length();
        var dir = speed > 1e-3f ? velocity / speed : Vector3.Forward;
        float reach = Mathf.Max(speed * ProbeLookaheadS, ProbeMinLookaheadM);
        var ahead = pos + dir * reach;
        string? struck = probe(pos, ahead) ?? probe(pos, ahead + Vector3.Down * ProbeDeckM);

        if (Mode == AiMode.AvoidCrash)
        {
            _clearProbes = struck is null ? _clearProbes + 1 : 0;
            if (_clearProbes >= ClearProbesToExit)
                Transition(_returnMode, "clear of obstacles");
        }
        else if (struck is not null && Mode != AiMode.Stunned)
        {
            if (Mode is AiMode.Pursue or AiMode.LayOff or AiMode.Patrol)
                _returnMode = Mode;
            Executor = null; // a running maneuver is abandoned to the override
            _clearProbes = 0;
            ClimbOutAltitude = pos.Y + ClimbOutM;
            Transition(AiMode.AvoidCrash, $"obstacle inside {reach:0} m ({struck})");
        }
    }

    // An eligible library maneuver, signature entries weighted up, one seeded draw;
    // null when no library is set or nothing passes the natural-touch cull.
    private Maneuver? PickManeuver()
    {
        if (Library is not { Count: > 0 } library)
            return null;
        var pool = new List<Maneuver>();
        foreach (var m in library)
        {
            if (!m.EligibleFor(NaturalTouch))
                continue;
            int weight = SignatureManeuvers != null
                && SignatureManeuvers.Contains(m.Name) ? SignatureWeight : 1;
            for (int i = 0; i < weight; i++)
                pool.Add(m);
        }
        return pool.Count > 0 ? pool[_rng.Next(pool.Count)] : null;
    }

    private float RandomSign() => _rng.Next(2) == 0 ? -1f : 1f;

    private void Transition(AiMode to, string reason)
    {
        if (to == Mode)
            return;
        var from = Mode;
        Mode = to;
        if (to != AiMode.EvasiveManeuver)
            Executor = null;
        if (to == AiMode.Stunned && _stunRemaining <= 0f)
            _stunRemaining = StunRecoveryIntervalS;
        if (to == AiMode.Evade && _evadeRemaining <= 0f)
        {
            _evadeRemaining = EvadeDurationS;
            _evadeScramble = EvadeScrambleIntervalS;
            if (EvadeAltitude <= 0f)
                EvadeAltitude = _lastPos.Y;
        }
        if (to == AiMode.AvoidCrash && ClimbOutAltitude <= 0f)
            ClimbOutAltitude = _lastPos.Y + ClimbOutM;
        if (to == AiMode.LayOff)
        {
            // The lay-off course: straight on from the entry velocity, at the entry altitude —
            // stay ahead of the pursuer rather than turning back into a head-on.
            _layOffHold = LayOffMinHoldS;
            if (new Vector2(_lastVelocity.X, _lastVelocity.Z).LengthSquared() > 1e-4f)
                LayOffHeadingDeg = AiPilot.HeadingDegOf(_lastVelocity);
            LayOffAltitude = _lastPos.Y;
        }
        _pursuedFor = 0f; // any transition restarts the sustained-pursuit clock
        ModeChanged?.Invoke(from, to, reason);
    }
}
