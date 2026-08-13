using System.Collections.Generic;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>Which of the original's four candidate lists a survivor came from. The lists differ
/// only in what they hold — scoring is identical across all four, so this is for the caller and
/// the suite, never for the scorer.</summary>
public enum AimTargetKind
{
    /// <summary><c>VehicleList</c> — aircraft, plus the AI ground/sea vehicles M4 will add.</summary>
    Vehicle,

    /// <summary>Turrets. Empty until M4 builds them.</summary>
    Turret,

    /// <summary><c>MStructList</c> — the mission structures; CSVM's approximation is the
    /// destructible registry.</summary>
    Structure,

    /// <summary>Live proximity-fused ordnance in flight — the reason guns snap onto an incoming
    /// rocket by design.</summary>
    Ordnance,
}

/// <summary>One gun barrel's aim-assist state, held in plane-local space
/// (docs/org/aim-assist.md "Per-muzzle state" — the original's eight <c>0x24</c>-byte slots at
/// plane <c>+0x3a4</c>). Indexed by (gun group, muzzle) exactly as <see cref="FireControl"/>
/// already picks a muzzle — no re-derivation of the original's <c>weaponGroup·2+barrelToggle</c>
/// index is needed.</summary>
public struct GunAimSlot
{
    /// <summary>False until this barrel exists (mirrors the original's muzzle-handle field, 0 =
    /// unused); an inactive slot is skipped by <see cref="AimAssist.Tick"/> entirely.</summary>
    public bool Active;

    /// <summary>Plane-local unit vector: what the round is actually fired along. Lags
    /// <see cref="Target"/> by the catch-up slerp.</summary>
    public Vector3 Smoothed;

    /// <summary>Plane-local unit vector: what <see cref="Smoothed"/> is chasing — the candidate
    /// scan's winner once B4/B5 land, or <see cref="AimAssist.LocalForward"/> when the forget
    /// timer has unwound it.</summary>
    public Vector3 Target;

    /// <summary>Game-time seconds (<see cref="CSVM.Utils.GameClock.Time"/>) this slot was last
    /// touched — stamped by the forget reset here, and, once B5 lands, by every round that goes
    /// out (the original restamps it on every shot, which is why the forget timer measures time
    /// since the barrel last fired, not time since a lock was lost).</summary>
    public double LastUpdate;
}

/// <summary>One thing the assist may snap onto, in world space. Built per fire call by whoever
/// holds the live lists (<see cref="AimCandidateSet"/>); the scorer reads nothing else.</summary>
public struct AimCandidate
{
    /// <summary>World position the intercept is solved to.</summary>
    public Vector3 Position;

    /// <summary>World velocity, m/s. The scorer subtracts the shooter's before solving, since the
    /// engine's solver takes the RELATIVE velocity.</summary>
    public Vector3 Velocity;

    /// <summary>Team id (the engine's <c>+0x08</c>). Matching the shooter's rejects the pair, and
    /// so does <see cref="AimAssist.NeutralTeam"/> on EITHER side — see
    /// <see cref="AimAssist.TeamOfPilot"/> for the convention CSVM supplies, having no team model
    /// of its own.</summary>
    public int Team;

    /// <summary>False for a candidate the engine's vtable <c>+0x14</c> predicate would reject —
    /// dead, or not live yet (a crashed pilot, a destroyed structure).</summary>
    public bool Live;

    /// <summary>The per-target cone override (<c>+0x50</c>), a half-angle in RADIANS, or
    /// <see cref="AimAssist.NoConeOverride"/> to use the firing weapon's own cone. Every shipped
    /// entity is the latter.</summary>
    public float ConeOverride;

    /// <summary>What this candidate IS, handed back on the winning result so the caller can act on
    /// it, and matched against <see cref="AimScan.Self"/> for the self-rejection. Null where there
    /// is no object to name (a pooled round).</summary>
    public object? Source;
}

/// <summary>One fire call's scan context (the engine's scan struct): where the round leaves from,
/// how fast and how far it flies, what counts as in-cone, and who is firing. All world-space.</summary>
public struct AimScan
{
    /// <summary>World muzzle position the intercept is solved from.</summary>
    public Vector3 MuzzlePosition;

    /// <summary>The shooter's own world velocity, m/s — subtracted from each candidate's to give
    /// the solver the relative velocity it wants.</summary>
    public Vector3 ShooterVelocity;

    /// <summary>The plane's world forward axis. Alignment (and so the cone gate and the score) is
    /// measured against THIS, not against the muzzle axis.</summary>
    public Vector3 Forward;

    /// <summary>The shooter's team (<see cref="AimAssist.TeamOfPilot"/>).</summary>
    public int Team;

    /// <summary>The round's speed, m/s — the weapon's <c>VELOCITY</c>.</summary>
    public float Speed;

    /// <summary>The weapon's <c>RANGE²</c>: an intercept the round cannot reach inside it is
    /// rejected.</summary>
    public float RangeSquared;

    /// <summary>The weapon's acceptance cone (<see cref="AimAssist.WeaponConeCos"/>), overridden
    /// per candidate by <see cref="AimCandidate.ConeOverride"/>.</summary>
    public float ConeCos;

    /// <summary><c>sticky_bullet_dist_factor</c>, per metre. Ships at 0.0, which deletes the
    /// distance term outright — selection is then purely most-aligned, at any range inside
    /// <c>RANGE</c>. The executable's own default is 2.5e-4; the shipped data turns it off
    /// deliberately, so a tuning pass must not "restore" it.</summary>
    public float DistFactor;

    /// <summary>The shooter, matched by reference against each candidate's
    /// <see cref="AimCandidate.Source"/> so it never snaps onto itself. Null where the caller has
    /// no object to name (the suites).</summary>
    public object? Self;
}

/// <summary>The scan's winner: which list it came from, the intercept direction to fire along
/// (world), when the round would arrive, and the score it won on.</summary>
public struct AimScanResult
{
    /// <summary>False when nothing survived the gates — the "no target found" answer, which leaves
    /// the slot's target on the plane's forward axis.</summary>
    public bool Found;

    /// <summary>Which of the four lists the winner came from.</summary>
    public AimTargetKind Kind;

    /// <summary>World unit vector: the intercept direction the winner was scored on.</summary>
    public Vector3 Direction;

    /// <summary>Time of flight to the intercept, seconds.</summary>
    public float TimeOfFlight;

    /// <summary><c>alignment − distance × dist_factor</c>.</summary>
    public float Score;

    /// <summary>The winning candidate's <see cref="AimCandidate.Source"/>, or null.</summary>
    public object? Source;
}

/// <summary>The per-muzzle gun aim assist: slot state (above), the per-frame forget + catch-up
/// pass (B2), the constant-velocity intercept solver (B3, docs/org/aim-assist.md "The lead
/// solver — <c>FUN_00460e30</c>") and the candidate scan with its rejection gates and scorer
/// (B4, "Scoring one candidate"), so the whole assist is one testable unit that
/// <see cref="FlightController"/> calls into.</summary>
public static class AimAssist
{
    /// <summary>A candidate advertising no cone of its own (the engine's <c>+0x50 = −1.0f</c>, what
    /// every entity constructor writes): the firing weapon's <c>CANNON_SPREAD</c> cone applies.
    /// Any value <c>≥ 0</c> is an override, read as a half-angle in radians.</summary>
    public const float NoConeOverride = -1f;

    /// <summary>The unaffiliated team. The engine rejects a candidate whose team id matches the
    /// shooter's AND one where either side is 0, so this is "never a target", not "everyone's
    /// enemy".</summary>
    public const int NeutralTeam = 0;

    /// <summary>The team CSVM's world objects (destructibles) sit on: hostile to every pilot, since
    /// nothing in the world data makes them anyone's own. Well clear of any pilot team
    /// (<see cref="TeamOfPilot"/> = index + 1, at most 4).</summary>
    public const int WorldTeam = 100;

    /// <summary>The proximity fuse that puts a round in flight on the assist's ordnance list: the
    /// engine tests the def's <c>DETONATION_DISTANCE²</c> against 0.01, i.e. a fuse longer than
    /// 0.1 m — the 13 ordnance carriers in docs/formats/weapons.md. (The engine also admits a round
    /// whose def sets secondary-block flag <c>0x20</c>; CSVM does not read that block, so this is
    /// the whole test here.)</summary>
    public const float MinFuseDistance = 0.1f;

    /// <summary>Local forward — the "no target found" answer a slot's target unwinds to.</summary>
    public static readonly Vector3 LocalForward = new(0f, 0f, -1f);

    /// <summary>Two directions this close to parallel (or its negation) make Godot's
    /// <see cref="Vector3.Slerp"/> throw "Argument is not normalized": its rotation axis comes
    /// from a cross product that degenerates at 0° and 180° separation. <see cref="FlightModel"/>
    /// guards its own VelocityDir slerp the same way, for the same crash — a plane holding
    /// straight and level, or a gun line that has already caught up to its target, hits this
    /// every frame.</summary>
    private const float ParallelDot = 0.999f;

    /// <summary>The per-frame forget + catch-up pass, run once per active slot. Forget: past
    /// <paramref name="forgetInterval"/> seconds since the slot was last touched, the target
    /// unwinds to local forward. Catch-up: <see cref="GunAimSlot.Smoothed"/> slerps toward
    /// <see cref="GunAimSlot.Target"/> at <paramref name="catchupRate"/> per second, snapping
    /// outright once that covers the whole turn in one frame — any frame at or past
    /// <c>1 / catchupRate</c> seconds fully snaps the gun line, a real hitch-behaviour difference
    /// and not a rounding detail to smooth away.
    ///
    /// <para>Call this immediately BEFORE performing this tick's fire outcome — the original
    /// restamps <c>lastUpdate</c> on every round that goes out, so the pass must see the
    /// pre-shot state.</para></summary>
    public static void Tick(GunAimSlot[] slots, double now, float dt, float forgetInterval, float catchupRate)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].Active)
            {
                continue;
            }
            ref var slot = ref slots[i];
            if (slot.LastUpdate + forgetInterval < now)
            {
                slot.Target = LocalForward;
                slot.LastUpdate = now + forgetInterval;
            }
            float t = catchupRate * dt;
            if (t >= 1f)
            {
                slot.Smoothed = slot.Target;
                continue;
            }
            float dot = slot.Smoothed.Dot(slot.Target);
            slot.Smoothed = dot > ParallelDot
                ? (slot.Smoothed + (slot.Target - slot.Smoothed) * t).Normalized()
                : dot < -ParallelDot
                    ? slot.Target // degenerate opposite case: snap rather than crash on Slerp
                    : slot.Smoothed.Slerp(slot.Target, t).Normalized();
        }
    }

    /// <summary>The constant-velocity intercept solver (<c>FUN_00460e30</c>): given a muzzle
    /// position, the round's speed, a target position and the target's velocity RELATIVE to the
    /// shooter (target velocity minus the shooter's — the caller subtracts before calling), finds
    /// the direction to fire and the time of flight so a straight-line round launched now reaches
    /// where the target will be. Frame-agnostic — every input in the same space, world or
    /// plane-local, and the answer comes out in that space.
    ///
    /// <para>The round meets the target when <c>speed·t = |displacement + relVel·t|</c>, a
    /// quadratic in <c>t</c> whose leading coefficient is <c>|relVel|² − speed²</c> — which sits
    /// near zero whenever the target's closing speed is close to the round's, the common case.
    /// Substituting <c>u = 1/t</c> flips the roles of leading and constant coefficient, so the
    /// leading coefficient becomes <c>|displacement|²</c> instead, essentially never near zero for
    /// a real separation. That substitution, not the quadratic formula's own cancellation
    /// avoidance, is what "numerically stable" refers to here — see the trap on
    /// <c>docs/PLAN-sticky-bullets.md</c>'s B3 before "fixing" it.</para></summary>
    public static bool TryIntercept(Vector3 muzzlePos, float speed, Vector3 targetPos, Vector3 relVel,
        out Vector3 aimDir, out float t)
    {
        aimDir = Vector3.Zero;
        t = 0f;

        Vector3 displacement = targetPos - muzzlePos;
        float a = displacement.LengthSquared(); // u's leading coefficient — see the doc comment
        if (a < 1e-6f)
        {
            return false; // zero separation: nothing to aim at
        }
        float b = 2f * relVel.Dot(displacement);
        float c = relVel.LengthSquared() - speed * speed;
        float disc = b * b - 4f * a * c;
        if (disc < 0f)
        {
            return false; // no real root: the target outruns the round on every heading
        }

        // The stable (Citardauq) quadratic form: avoids subtracting near-equal quantities when b
        // and sqrt(disc) are close in magnitude, which the textbook (-b±sqrt(disc))/2a form does
        // not — do not replace this with the textbook formula (docs/PLAN-sticky-bullets.md B3's trap).
        float sq = Mathf.Sqrt(disc);
        float q = -0.5f * (b + (b >= 0f ? sq : -sq));
        float u1 = q / a;
        float u2 = Mathf.IsZeroApprox(q) ? float.NaN : c / q;

        // u = 1/t, so only a positive u is a forward-time solution; the larger positive root is
        // the smaller t — the earliest intercept.
        float u = u1 > 0f && u2 > 0f ? Mathf.Max(u1, u2) : u1 > 0f ? u1 : u2 > 0f ? u2 : float.NaN;
        if (float.IsNaN(u) || u <= 0f)
        {
            return false; // both roots non-positive: the target outran the round in this geometry
        }

        t = 1f / u;
        Vector3 dir = relVel + displacement / t;
        if (dir.LengthSquared() < 1e-12f)
        {
            t = 0f;
            return false;
        }
        aimDir = dir.Normalized();
        return true;
    }

    /// <summary>The assist's acceptance cone for one weapon, as a cosine in the positive-alignment
    /// convention (the engine stores <c>−cos</c> and tests both sides negated; docs/org/aim-assist.md
    /// "Sign convention" — do not mix halves of the two). <c>CANNON_SPREAD</c> is a HALF-angle in
    /// degrees, 6.0 on every stock gun. An absent key is the whole forward hemisphere, not a
    /// built-in cone: the def's secondary block is <c>calloc</c>ed, so the slot keeps 0.0 =
    /// <c>−cos(90°)</c>.</summary>
    public static float WeaponConeCos(WeaponDef weapon) =>
        Mathf.Cos(Mathf.DegToRad(weapon.CannonSpread ?? 90f));

    /// <summary>The cone one candidate is judged against: its own <c>+0x50</c> override (a
    /// half-angle in RADIANS) when it advertises one, else the firing weapon's. No shipped entity
    /// sets it — the only authored writer is the turret key <c>STICKINESS</c>, which ships zero
    /// times — but the branch is ported anyway, since dropping it is a silent behaviour change the
    /// moment a mission authors one (Decision 2 in docs/PLAN-sticky-bullets.md).</summary>
    public static float ConeCosFor(in AimCandidate candidate, float weaponConeCos) =>
        candidate.ConeOverride >= 0f ? Mathf.Cos(candidate.ConeOverride) : weaponConeCos;

    /// <summary>The team a pilot's things belong to. CSVM has NO team model at all (nothing in
    /// <c>src/</c> carries one), so the port needs a convention to run the engine's team gate
    /// against, and this is it: pilot N is team N+1, which makes every pane hostile to every other
    /// pane — what <c>--vs</c> is. A round nobody owns
    /// (<see cref="ProjectilePool.NoShooter"/>, and any other negative id) is
    /// <see cref="NeutralTeam"/>, so the engine's own "either side is 0 rejects the pair" rule
    /// leaves it untargetable.</summary>
    public static int TeamOfPilot(int shooterId) => shooterId >= 0 ? shooterId + 1 : NeutralTeam;

    /// <summary>The launch scatter (<c>FUN_00460940</c> → <c>FUN_004608a0</c>), the ONLY scatter the
    /// original applies to a player's round: build any perpendicular to the aim direction, roll it
    /// about the aim axis by a uniform angle, then rotate the aim direction about that perpendicular
    /// by <paramref name="inaccuracy"/> × a uniform [0,1).
    ///
    /// <para>⚠ The polar angle is uniform in <c>[0, inaccuracy]</c>, NOT uniform over the cone's
    /// solid angle — sampling the cap (the reflex when porting, and what
    /// <c>ProjectilePool.ApplySpread</c> does with its <c>sqrt(rand)</c>) puts noticeably more shots
    /// near the rim. Do not merge the two.</para></summary>
    public static Vector3 Scatter(Vector3 aimDir, float inaccuracy, RandomNumberGenerator rng)
    {
        if (inaccuracy <= 0f || aimDir.LengthSquared() < 1e-12f)
        {
            return aimDir;
        }
        Vector3 axis = aimDir.Normalized();
        // "Any perpendicular": cross with whichever world axis this direction is least aligned
        // with, so the cross never degenerates. Which one it is does not matter — the next line
        // rolls it to a uniform angle about the aim axis anyway.
        Vector3 seed = Mathf.Abs(axis.Y) < 0.9f ? Vector3.Up : Vector3.Right;
        Vector3 perp = axis.Cross(seed).Normalized().Rotated(axis, rng.Randf() * Mathf.Tau);
        return axis.Rotated(perp, rng.Randf() * inaccuracy).Normalized();
    }

    /// <summary>One round's launch direction, in world space — <c>FUN_004b6530</c>'s whole step
    /// order, which is asymmetric on purpose:
    /// <list type="number">
    /// <item>seed the slot's target with the plane's own forward axis (the "no target found"
    /// answer) and run the scan, whose winner replaces it;</item>
    /// <item>rotate that world direction into the slot's plane-local <see cref="GunAimSlot.Target"/>
    /// and restamp <see cref="GunAimSlot.LastUpdate"/> — which is why the forget timer measures time
    /// since this barrel last FIRED;</item>
    /// <item>rotate the slot's plane-local <see cref="GunAimSlot.Smoothed"/> back out to world as
    /// the direction actually fired — <b>this frame's scan result is not what goes out</b>, the
    /// smoothed value from previous frames is;</item>
    /// <item>scatter it by <paramref name="inaccuracy"/>.</item>
    /// </list>
    /// Firing this frame's scan result instead would remove the lag entirely and read as an
    /// aimbot; the lag is the feel.</summary>
    public static Vector3 FireDirection(ref GunAimSlot slot, in AimScan scan,
        AimCandidateSet candidates, Basis planeBasis, double now, float inaccuracy,
        RandomNumberGenerator rng, out AimScanResult found)
    {
        Vector3 world = Scan(scan, candidates, out found) ? found.Direction : scan.Forward;
        var toLocal = planeBasis.Orthonormalized().Transposed(); // inverse of a rotation basis
        slot.Target = (toLocal * world).Normalized();
        slot.LastUpdate = now;
        Vector3 fired = (planeBasis.Orthonormalized() * slot.Smoothed).Normalized();
        return Scatter(fired, inaccuracy, rng);
    }

    /// <summary>The candidate scan (<c>FUN_004b6530</c>'s scan half): one scorer over all four of
    /// the original's candidate lists, in its own order, returning the highest-scoring survivor or
    /// false. Every gate is the engine's, in the engine's order — self, not live, same team (or
    /// either side unaffiliated), no intercept, the intercept point out of the weapon's authored
    /// range, outside the acceptance cone — and survivors rank by
    /// <c>alignment − distance × dist_factor</c>, where alignment is the dot of the intercept
    /// direction with the plane's forward axis. The first survivor always beats the seed, exactly
    /// as the engine's <c>−FLT_MAX</c> best-score cell does.
    ///
    /// <para>Works in world space throughout, so <paramref name="scan"/>'s muzzle, velocities and
    /// forward axis are all world; B5 rotates the winning direction world→local into the slot's
    /// target field, which is where the plane-local half of the assist starts.</para></summary>
    public static bool Scan(in AimScan scan, AimCandidateSet candidates, out AimScanResult best)
    {
        best = default;
        float bestScore = float.MinValue; // the engine's −FLT_MAX best-score seed
        ScoreList(scan, candidates.Vehicles, AimTargetKind.Vehicle, ref best, ref bestScore);
        ScoreList(scan, candidates.Turrets, AimTargetKind.Turret, ref best, ref bestScore);
        ScoreList(scan, candidates.Structures, AimTargetKind.Structure, ref best, ref bestScore);
        ScoreList(scan, candidates.Ordnance, AimTargetKind.Ordnance, ref best, ref bestScore);
        return best.Found;
    }

    // One list's pass. The engine runs four byte-identical scorers differing only in the container
    // accessor, so there is one scorer here and four calls — a list CSVM has not built yet iterates
    // nothing rather than being absent (M4 wires its turrets in, and the pass is already here).
    private static void ScoreList(in AimScan scan, List<AimCandidate> list, AimTargetKind kind,
        ref AimScanResult best, ref float bestScore)
    {
        foreach (var candidate in list)
        {
            if (scan.Self != null && ReferenceEquals(candidate.Source, scan.Self))
            {
                continue; // the shooter never snaps onto itself
            }
            if (!candidate.Live)
            {
                continue; // the engine's vtable +0x14 predicate: dead, or not live yet
            }
            // Same team, or EITHER side unaffiliated, rejects the pair — team 0 is not a wildcard.
            if (candidate.Team == NeutralTeam || scan.Team == NeutralTeam || candidate.Team == scan.Team)
            {
                continue;
            }
            if (!TryIntercept(scan.MuzzlePosition, scan.Speed, candidate.Position,
                    candidate.Velocity - scan.ShooterVelocity, out var dir, out float t))
            {
                continue; // a target outrunning the round is simply not assisted
            }
            float reach = scan.Speed * t;
            if (reach * reach > scan.RangeSquared)
            {
                continue; // the round could not reach the intercept point inside authored RANGE
            }
            float alignment = dir.Dot(scan.Forward);
            if (alignment < ConeCosFor(candidate, scan.ConeCos))
            {
                continue;
            }
            float score = alignment - scan.MuzzlePosition.DistanceTo(candidate.Position) * scan.DistFactor;
            if (score <= bestScore)
            {
                continue;
            }
            bestScore = score;
            best = new AimScanResult
            {
                Found = true,
                Kind = kind,
                Direction = dir,
                TimeOfFlight = t,
                Score = score,
                Source = candidate.Source,
            };
        }
    }
}

/// <summary>Everything one fire call may snap onto, kept as the engine's four lists rather than one
/// merged list: the scan order is theirs, and a list CSVM has not built yet must be EMPTY, not
/// absent — M4 wiring turrets in is then one <c>AddTurret</c> call and not a rediscovery of this
/// item. Reused across fire calls; <see cref="Clear"/> between them.</summary>
public sealed class AimCandidateSet
{
    /// <summary>Aircraft, and the AI ground/sea vehicles M4 adds. CSVM's live
    /// <see cref="FlightController"/>s.</summary>
    public List<AimCandidate> Vehicles { get; } = new();

    /// <summary>Turrets. **Empty in every build today** — turrets are M4, and this list exists so
    /// that arrives as a wiring change rather than a scan change.</summary>
    public List<AimCandidate> Turrets { get; } = new();

    /// <summary>The mission structures. CSVM's analogue is
    /// <see cref="DestructibleRegistry"/>, which is an APPROXIMATION and recorded as one: it is the
    /// shootable-world-object list, where the original's is the <c>targets.zrd</c> mission-structure
    /// list. (<c>MissionTargets</c> is not the analogue at all — objective display strings, nothing
    /// damageable.)</summary>
    public List<AimCandidate> Structures { get; } = new();

    /// <summary>Live proximity-fused rounds in flight — a filter over the live
    /// <see cref="ProjectilePool"/>, not a structure of its own.</summary>
    public List<AimCandidate> Ordnance { get; } = new();

    public void Clear()
    {
        Vehicles.Clear();
        Turrets.Clear();
        Structures.Clear();
        Ordnance.Clear();
    }

    public void AddVehicle(Vector3 position, Vector3 velocity, int team, bool live, object? source,
        float coneOverride = AimAssist.NoConeOverride) =>
        Vehicles.Add(Make(position, velocity, team, live, source, coneOverride));

    public void AddTurret(Vector3 position, Vector3 velocity, int team, bool live, object? source,
        float coneOverride = AimAssist.NoConeOverride) =>
        Turrets.Add(Make(position, velocity, team, live, source, coneOverride));

    public void AddStructure(Vector3 position, int team, bool live, object? source,
        float coneOverride = AimAssist.NoConeOverride) =>
        Structures.Add(Make(position, Vector3.Zero, team, live, source, coneOverride));

    public void AddOrdnance(Vector3 position, Vector3 velocity, int team, object? source,
        float coneOverride = AimAssist.NoConeOverride) =>
        Ordnance.Add(Make(position, velocity, team, live: true, source, coneOverride));

    /// <summary>Every registered destructible that is not already destroyed, as structure
    /// candidates. <paramref name="team"/> defaults to <see cref="AimAssist.WorldTeam"/> — the
    /// world's shootables are nobody's own, so they are hostile to every pilot; CSVM has no team
    /// data to read this from. An anchor outside the tree is skipped rather than read (its global
    /// transform is meaningless until it is in one).</summary>
    public void AddStructures(DestructibleRegistry registry, int team = AimAssist.WorldTeam)
    {
        foreach (var inst in registry.All)
        {
            if (!inst.Anchor.IsInsideTree())
            {
                continue;
            }
            AddStructure(inst.Anchor.GlobalPosition, team,
                inst.Status != DestructibleRegistry.State.Destroyed, inst);
        }
    }

    private static AimCandidate Make(Vector3 position, Vector3 velocity, int team, bool live,
        object? source, float coneOverride) =>
        new()
        {
            Position = position,
            Velocity = velocity,
            Team = team,
            Live = live,
            ConeOverride = coneOverride,
            Source = source,
        };
}

