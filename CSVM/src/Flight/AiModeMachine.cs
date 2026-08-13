using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>The decoded AI mode vocabulary (M4 D11): the nine states the original engine's debug
/// readout dispatches on, in its own naming (docs/formats/ai-rosters.md "AI modes, engine-side").
/// There is no flee and no inactive mode in that dispatch.</summary>
public enum AiMode
{
    /// <summary>Flying the assigned net (B5) or holding course.</summary>
    Patrol,

    /// <summary>Chasing the target; the gunner (D14) fires in this mode.</summary>
    Pursue,

    /// <summary>The "let the player catch up" mode. Present in the dispatch as a first-class
    /// state; D15 lands the rubber-band behaviour inside it. Until then it steers and fires
    /// exactly like <see cref="Pursue"/>.</summary>
    LayOff,

    /// <summary>Broken off after a failed steady-hand test.</summary>
    Evade,

    /// <summary>Playing one maneuver-library program through <see cref="ManeuverExecutor"/>.</summary>
    EvasiveManeuver,

    /// <summary>Controls neutral for <c>stun_recovery_interval</c> after a failed sixth-sense test.</summary>
    Stunned,

    /// <summary>Terrain-closure override: climb out, everything else waits.</summary>
    AvoidCrash,

    /// <summary>Danger-zone approach. Never entered yet — see <see cref="AiModeMachine"/>.</summary>
    ApproachingDangerZone,

    /// <summary>Danger-zone run. Never entered yet — see <see cref="AiModeMachine"/>.</summary>
    NavigatingDangerZone,
}

/// <summary>The nine-mode AI state machine (M4 D11), owned by an <see cref="AiPilot"/> and stepped
/// once per sim tick from <see cref="AiPilot.Next"/>. The mode list is decoded (the engine's debug
/// readout dispatches on exactly these states); the transitions below are decoded where the plan
/// says so and NAMED AS INVENTED where they are not:
///
/// <para><b>Decoded:</b> pursue is entered when a target sits inside the activation/attack radius
/// (player.json's <c>min_ai_active_dist</c> and vehicle.json's <c>attack</c>, both 2000 m shipped —
/// the roster's own volume slots are 0.0 install-wide). A hit rolls the steady-hand test
/// (<c>steady_hand_chance</c>) and a FAILED test evades — the engine's own vocabulary ("Absorbed
/// %f damage; steady hand test failed. Evading."), never restated. A target's evasive maneuver
/// rolls the sixth-sense test (<c>sixth_sense_chance</c>) and a FAILED test stuns for
/// <c>stun_recovery_interval</c>. An evasive maneuver is an eligible library entry
/// (<see cref="Maneuver.EligibleFor"/>) played to <see cref="ManeuverExecutor.Done"/>, then back
/// to the prior mode.</para>
///
/// <para><b>Invented, named as such:</b> the evade behaviour beyond breaking off (timed run,
/// seeded heading scrambles away from the threat — the decode is thin past "Evading."); the
/// steady-hand roll is the flat shipped chance, the design's "damage weighted by accumulated
/// damage" arithmetic being undecoded; <c>return_range</c> read as a chase leash from the point
/// where pursuit began (which anchor the original uses is undecoded); the avoid-crash probe
/// geometry, cadence and climb-out; the ×3 signature-maneuver selection weight.</para>
///
/// <para><b>Enum-only:</b> the two danger-zone modes are never entered. Their entry conditions
/// are undecoded — the danger-zone gate data is the 4-extra net-tag system the F17 decode left
/// open, and <c>daredevil_chance</c> (the attempt roll) stays unwired until it exists. Do not
/// invent an entry condition.</para>
///
/// <para>Config fields are plain and mutable BY DESIGN (the mission-script rule:
/// <c>SET_AI_ATTACK_RADIUS</c> and friends rewrite them at runtime). All randomness is this
/// machine's own seeded stream, so a fixed-seed run transitions identically. Engine-free: the
/// terrain probe is an injected delegate and target state arrives as a snapshot, so the
/// transition table unit-tests without a scene tree (<c>AiModeMachineTests</c>).</para></summary>
public sealed class AiModeMachine
{
    /// <summary>Invented: how long a plain (maneuver-less) evade runs before returning.</summary>
    public const float EvadeDurationS = 8f;

    /// <summary>Invented: seconds between evade heading scrambles.</summary>
    public const float EvadeScrambleIntervalS = 2f;

    /// <summary>Invented: terrain-probe cadence, seconds.</summary>
    public const float ProbeIntervalS = 0.25f;

    /// <summary>Invented: terrain-probe lookahead, seconds of current velocity.</summary>
    public const float ProbeLookaheadS = 2.5f;

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
    /// <c>steady_hand_chance</c> (0.5 at rating 1 → 0.08 at 9; lower is the better pilot).
    /// The design's damage weighting on this roll is undecoded and not modelled.</summary>
    public float SteadyHandChance = 0.5f;

    /// <summary>Probability that the sixth-sense test PASSES (the pilot follows the target's
    /// maneuver) — <c>sixth_sense_chance</c> (0.45 at rating 1 → 0.71 at 9). A failure stuns.</summary>
    public float SixthSenseChance = 0.45f;

    /// <summary>How long a stun lasts — <c>stun_recovery_interval</c> (4.8 s at rating 1 →
    /// 0.6 s at 9).</summary>
    public float StunRecoveryIntervalS = 4.8f;

    /// <summary>The pilot's 1–9 <c>natural_touch</c>, compared directly against maneuver
    /// difficulty (no interpolation table, by design).</summary>
    public int NaturalTouch = 1;

    /// <summary>The maneuver library (D13), or null for a maneuver-less pilot (a failed
    /// steady-hand test then evades plainly instead).</summary>
    public IReadOnlyList<Maneuver>? Library;

    /// <summary>The roster's signature-maneuver names (<see cref="Maneuvers.SignatureNames"/>),
    /// weighted up <see cref="SignatureWeight"/>× during selection; null/empty = none.</summary>
    public IReadOnlyCollection<string>? SignatureManeuvers;

    /// <summary>World-only line-of-sight probe for the avoid-crash test (the host wires
    /// <c>FlightController.WorldBlocksLine</c>; tests inject a fake). Null = no terrain data,
    /// the mode is never entered.</summary>
    public Func<Vector3, Vector3, bool>? ProbeBlocked;

    private readonly Random _rng;

    private AiMode _returnMode = AiMode.Patrol;
    private AiMode? _lastTargetMode;
    private Vector3 _pursuitAnchor;
    private Vector3 _lastPos;
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
    /// position or null (no live target); <paramref name="targetMode"/> its own machine's mode
    /// when the target is an AI aircraft — the sixth-sense trigger watches it enter an evasive
    /// state. A human target reports null: detecting a human player's maneuver is undecoded, so
    /// the sixth-sense roll fires only against AI targets today.</summary>
    public AiMode Update(Vector3 pos, Vector3 velocity, Vector3? targetPos, AiMode? targetMode,
        float dt)
    {
        _lastPos = pos;
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

    /// <summary>Evade needs an initial course even when entered externally: away from the
    /// threat, offset randomly (invented behaviour — the decode is thin past "Evading.").</summary>
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

    /// <summary>Where a finished reaction goes back to: the prior mode when its conditions still
    /// hold, patrol otherwise.</summary>
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

    /// <summary>The terrain-closure override (invented probe, marked above): two world-only rays
    /// along the velocity lookahead, every <see cref="ProbeIntervalS"/>. Blocked → avoid crash
    /// (dropping a running maneuver); clear for <see cref="ClearProbesToExit"/> rounds → back.</summary>
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
        bool blocked = probe(pos, ahead) || probe(pos, ahead + Vector3.Down * ProbeDeckM);

        if (Mode == AiMode.AvoidCrash)
        {
            _clearProbes = blocked ? 0 : _clearProbes + 1;
            if (_clearProbes >= ClearProbesToExit)
                Transition(_returnMode, "clear of terrain");
        }
        else if (blocked && Mode != AiMode.Stunned)
        {
            if (Mode is AiMode.Pursue or AiMode.LayOff or AiMode.Patrol)
                _returnMode = Mode;
            Executor = null; // a running maneuver is abandoned to the override
            _clearProbes = 0;
            ClimbOutAltitude = pos.Y + ClimbOutM;
            Transition(AiMode.AvoidCrash, $"terrain inside {reach:0} m");
        }
    }

    /// <summary>An eligible library maneuver, signature entries weighted up, one seeded draw;
    /// null when no library is set or nothing passes the natural-touch cull.</summary>
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
        ModeChanged?.Invoke(from, to, reason);
    }
}
