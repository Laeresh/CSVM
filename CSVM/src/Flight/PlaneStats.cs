using System;
using System.Collections.Generic;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>Clamped linear ramp between two (x, y) control points — the shape of
/// every throttle/speed→volume/pitch sound curve in player.json.</summary>
public readonly struct SoundCurve
{
    public readonly float MinX, MinY, MaxX, MaxY;

    public SoundCurve(float minX, float minY, float maxX, float maxY) =>
        (MinX, MinY, MaxX, MaxY) = (minX, minY, maxX, maxY);

    public float Eval(float x) => MaxX <= MinX
        ? MaxY
        : MinY + (MaxY - MinY) * Mathf.Clamp((x - MinX) / (MaxX - MinX), 0f, 1f);
}

/// <summary>One entry of a vehicle def's 'destroyable_parts' block:
/// a damageable airframe section — nose / tail / leftwing / rightwing for the
/// player planes — with its hit points, its armor pool, and state-change anims.
/// The pair is (hit points, armor) — armor is spent first
/// (docs/formats/vehicle.md "The hp pair: armor + hit points"); a def with only one
/// float carries no armor (`MaxArmor` stays 0), it is not duplicated from MaxHp.
/// 'critical' means the plane is destroyed when this part's HP reaches 0; the tail
/// additionally carries 'engine' (power loss on destruction — flight-handling
/// penalties are not modeled yet, recorded only). InjureAnims maps descending
/// HP fractions to anim names: the *_damage_green/yellow/red cockpit-indicator
/// cycle plus the pdpanelN torn-skin panel flips (DamageVisuals wires the panels).</summary>
public sealed class DestroyablePart
{
    public string Name = "";
    public float MaxHp = 20f;
    public float MaxArmor;
    public bool Critical;
    public bool Engine;
    public string? GotHitAnim;
    public List<(float Frac, string Anim)> InjureAnims = new();
}

/// <summary>One entry of a vehicle def's <c>turrets</c> block: which <c>ai.zrd</c> gunner row
/// (<see cref="Title"/>, a <c>MSG_TUR_*</c> key) drives which turret-rig subtree
/// (<see cref="Node"/>, e.g. <c>kestrel_turret1</c>). The block is keyed by VIEWPOINT —
/// <c>firstp</c> is the cockpit-view rig (player defs only), <c>thirdp</c> the external one —
/// which is what the titles' <c>_G1</c>/<c>_G3</c> suffixes select (docs/formats/turrets.md).</summary>
public sealed class TurretMount
{
    public string Title = "";
    public string Node = "";
    public bool FirstPerson;
}

/// <summary>
/// Flight parameters for one player aircraft, pulled from the zrdr extraction:
/// vehicle.json (per-plane 'dynamics' block, resolved through the 'kind_of'
/// inheritance chain, e.g. pbloodhawk → player_airplane → basic_airplane),
/// engines.json (stock engine power factor), player.json (global constants).
/// Units are meters/seconds (fd_speed 135 ≈ 302 mph matches the Bloodhawk's
/// published top speed; flight_ceiling 2500 and nom_gravity 20 agree).
/// </summary>
public sealed class PlaneStats
{
    /// <summary>The per-spawn jitter's half-width (<see cref="WithAiSpawnJitter"/>): the original's
    /// own immediate, an independent uniform ±5 % per slot.</summary>
    public const float AiSpawnJitterSpread = 0.05f;

    public string DefName = "";          // vehicle.json def, e.g. "pbloodhawk"
    public string NodeName = "";         // GameZ node, e.g. "player_bhawk"

    /// <summary>The AI def the damage trio came from ("bloodhawk"), or null on a player load
    /// (<see cref="Load"/>). Set only by <see cref="LoadForAi"/>, and deliberately NOT used as
    /// <see cref="DefName"/>: that name keys the stock-loadout table, which holds the eleven
    /// player defs alone, so swapping it would leave every AI plane unarmed.</summary>
    public string? AiDefName;

    // dynamics block
    public float PitchTorque = 2.4f;
    public float RollTorque = 6f;
    public float RudderTorque = 1.4f;
    public float ReturnRate = 3f;        // extra rate damping while the stick is centered
    public float AngMomentumDamp = 5f;
    public Vector3 RecInertia = new(0.8f, 0.6f, 1.3f); // reciprocal moments: x=pitch, y=yaw, z=roll
    public float FdSpeed = 113f;         // reference/max level-flight speed, m/s
    public float DragFactor = 1f;
    public float VehWeight = 3500f;
    public float RefArea = 335f;

    public float EnginePower = 1f;       // engines.json factor for the plane's stock engine
    public float FlightCeiling = 2500f;

    // The AI mode machine's range gates: vehicle.json 'attack' / 'return_range', both
    // authored once on basic_airplane and inherited install-wide (2000 / 1200).
    public float AiAttackRange = 2000f;
    public float AiReturnRange = 1200f;

    // The AI control law's per-axis output stage (docs/org/aiControlLaw.md): roll, pitch and yaw
    // commands are multiplied by these scales, then clamped to these limits. Fallbacks are the
    // def initialiser's own compiled defaults, which is what every airframe in this install flies on.
    // ⚠ A scale of 3.5 against a limit of 1.0 saturates near a 0.29 aim error, so this stage is
    // near-bang-bang, not proportional; that is decoded behaviour, not a bug.
    // ⚠ Do not mirror ai_emerg_input_*: its compiled defaults equal these and no roster authors it.
    public float AiInputScaleRoll = 3.5f;
    public float AiInputScalePitch = 3.5f;
    public float AiInputScaleYaw = 3.5f;
    public float AiInputLimitRoll = 1f;
    public float AiInputLimitPitch = 1f;
    public float AiInputLimitYaw = 1f;

    // rudder_tol: how much horizontal aim error justifies banking rather than ruddering
    // (docs/org/aiControlLaw.md). ⚠ A HIGHER value means MORE rudder, not less — clearing the
    // threshold is what selects the bank branch. At the 0.2 default any target ahead is banked
    // toward and the rudder is reserved for targets nearly dead astern; `autogyro` and `balmoral`
    // author 1.0, the ceiling the compared quantity can never exceed, which puts a lateral-dominant
    // error on the rudder even dead ahead. Only `balmoral` reaches this law (the autogyro is class 1).
    public float RudderTol = 0.2f;

    // player.json globals
    public float Gravity = PhysicsConstants.NomGravity; // nom_gravity — the game's arcade gravity, m/s²
    public float StallMag = 1.25f;

    // player.json flight globals, plumbed here instead of hardcoded (docs/org/flightModel.md).
    // Converted exactly as the original does: speeds × 0.44704, angles cosined where the
    // original cosines them, raw G otherwise. Fallbacks are the executable's compiled defaults;
    // this install's authored values differ — see the corrections table there.
    // ⚠ MaxAoaCos/HighG*/LowG*/HighSpeedPitchFade* are unread on purpose: this install authors
    // them out of the control limiters' reach. Do not implement any from the field names.
    public float LiftAccelRate = 1.2f;      // lift_accel_rate, 1/s — NOT converted (a rate, not a speed)
    public float LiftAoaCosLo = 0.98f;      // cos(liftAOAs[0]) — liftAOAs is degrees, cosined at load
    public float LiftAoaCosHi = 0.96f;      // cos(liftAOAs[1])
    public float MaxAoaCos = 0.85f;         // cos(maxAOA) — maxAOA is degrees, cosined at load
    public float HighGStart = 5f;           // highGs[0], plain G — NOT converted
    public float HighGMax = 9f;             // highGs[1], plain G
    public float LowGStart = -5f;           // lowGs[0], plain G
    public float LowGMax = -9f;             // lowGs[1], plain G
    public float TurnFadeIn = 10f * PhysicsConstants.MphToMs;   // turn_fade_in, m/s
    public float TurnFadeOut = 40f * PhysicsConstants.MphToMs;  // turn_fade_out, m/s
    public float YawLowSpeed = 0.05f;       // yaw_low_speed, dimensionless authority — NOT converted
    public float YawHighSpeed = 0.1f;       // yaw_high_speed, dimensionless authority
    public float YawFadeIn = 10f * PhysicsConstants.MphToMs;    // yaw_fade_in, m/s
    public float YawMax = 22.5f * PhysicsConstants.MphToMs;     // yaw_max, m/s
    public float YawFadeOut = 45f * PhysicsConstants.MphToMs;   // yaw_fade_out, m/s
    public float HighSpeedPitchFadeLo = 500f * PhysicsConstants.MphToMs;  // high_speed_pitch_fade[0], m/s
    public float HighSpeedPitchFadeHi = 600f * PhysicsConstants.MphToMs; // high_speed_pitch_fade[1], m/s
    // drag_fade_speed's own compiled fallback is undocumented in docs/org/flightModel.md (the decode
    // covers control authority's turn_*/yaw_* fades but not this key's mechanism); 40 mph mirrors the
    // unchanged turn_fade_out/yaw_fade_in pattern, not a read fallback — flag if this proves wrong.
    public float DragFadeSpeed = 40f * PhysicsConstants.MphToMs; // drag_fade_speed, m/s

    // Ground blow (docs/org/flightModel.md, "Ground blow"): the nose-forward probe that biases the
    // player's control response away from what it hits. Both are RAW SCALARS — groundblow_elev is a
    // length in METRES and needs no conversion, and it is the ray's length AND the falloff's
    // denominator, so it is not a trigger range. Fallbacks are the executable's compiled defaults;
    // this install authors 400 and 10.
    public float GroundBlowElev = 100f;     // groundblow_elev, m — ray length and falloff denominator
    public float GroundBlowMag = 1.5f;      // groundblow_mag, dimensionless
    // ⚠ C23: the AI path is a DIFFERENT law from the player term, not that term scaled by this —
    // a fixed push, linear in proximity, not dt-scaled (docs/org/flightModel.md "Ground blow").
    // FlightModel.GroundBlowTerm reads AiGroundBlow · GroundBlowMag as that factor (5.0 authored).
    // The compiled AI branch also cuts it ×0.15 for 2.5 s after a carrier drop and while stunned;
    // neither cut is modelled here (backlog.md BL-382).
    public float AiGroundBlow = 0.9f;       // ai_groundblow, dimensionless

    // The collision restitution ceiling (player.json's `crash` block, docs/org/flightModel.md's
    // "Collision response and bounce_factor"): a RAW SCALAR, and the ceiling on effective normal
    // restitution rather than the restitution itself — what a contact actually rebounds at is
    // f_lin · this, with f_lin the lever arm's rebound/spin partition (FlightModel's
    // BounceNormalSpeed). The fallback is the executable's compiled default, pre-set before the
    // block is looked up, so an absent `crash` block leaves it standing; this install authors 0.6.
    public float BounceFactor = 0.8f;       // bounce_factor, dimensionless

    // The collision damage pair's authored ranges (player.json's `crash` block, decode in
    // docs/org/flightModel.md's "Collision damage"): element 0 is the FLOOR and element 1 the
    // SCALE of max(scale · s³, floor), with s the impact cosine. Fallbacks are the executable's
    // compiled defaults, which this install's authored [50, 300] always replaces.
    public float CollideArmorFloor = 15f;
    public float CollideArmorScale = 200f;
    public float CollideHealthFloor = 15f;
    public float CollideHealthScale = 200f;

    // The near-miss cue's shipped accumulator (warning_shot_*) — see WarningShotCue for the units
    // question. The sound is a SOUND_GROUPS name (bullet_warning_sg → snd_bulletpass1-3), not a
    // sounds.json def, so it resolves through the group table like every other one.
    public float WarningShotMax = 2f;
    public float WarningShotDissipation = 2f;   // intensity per second
    public float WarningShotInterval = 1f;      // s between cues
    public string WarningShotSound = "bullet_warning_sg";

    // The gun aim assist (sticky_bullet_*, docs/org/aim-assist.md) — CatchupRate/ForgetInterval
    // feed B2's per-frame slot update, DistFactor B4's candidate scoring, Inaccuracy B5's launch
    // scatter. Fallbacks are the executable's own compiled defaults, not the shipped player.json
    // values — DistFactor's shipped 0.0 deletes the scan's distance term outright, where the
    // compiled fallback below does not.
    public float StickyBulletCatchupRate = 1f;      // sticky_bullet_catchup_rate, 1/s
    public float StickyBulletForgetInterval = 0.5f; // sticky_bullet_forget_interval, s
    public float StickyBulletDistFactor = 2.5e-4f;  // sticky_bullet_dist_factor, per metre
    // Held in RADIANS, as the original stores it: the parser multiplies the file's degrees by
    // pi/180 on the way in. The shipped 1.0 is a 1-degree cone.
    public float StickyBulletInaccuracy = Mathf.Pi / 180f;

    // sound (vehicle.json 'engine_sound' name + player.json curve blocks).
    // Engine curves run on throttle [0..1]; whine (the 'prop_sound' block — only
    // audible past fd_speed, i.e. a dive) and rattle run on speed/fd_speed.
    public string EngineSound = "snd_devastatorengine"; // basic_airplane default
    public SoundCurve EngineVolume = new(0.1f, 1f, 1f, 1f);
    public SoundCurve EnginePitch = new(0.1f, 0.6f, 1f, 1f);
    public string WhineSound = "snd_enginewhine"; // not named in the readers; the only pitch-shiftable candidate
    public SoundCurve WhineVolume = new(1f, 0f, 1.1f, 0.5f);
    public SoundCurve WhinePitch = new(1f, 0.65f, 1.2f, 1.25f);
    public string RattleSound = "snd_planeshake";
    public SoundCurve RattleVolume = new(1f, 0f, 1.2f, 1f);

    /// <summary>vehicle.json 'damaged_engine_sound' — null when a def carries none (none do; every
    /// plane inherits basic_airplane's single entry, verified install-wide). DamagedEngineGain reads
    /// the entry's two trailing floats (0.0, 1.0 for every plane) as a fade window over accumulated
    /// damage fraction (1 - worst part HP fraction): 0 gain at the low value, full gain at the high
    /// one — the same shape as every other engine-audio SoundCurve. The floats are otherwise
    /// undecoded; this reading is a TUNE candidate, not a confirmed original mechanic.</summary>
    public string? DamagedEngineSound;
    public SoundCurve DamagedEngineGain = new(0f, 0f, 1f, 1f);

    /// <summary>The plane's damageable sections ('destroyable_parts', nearest def in
    /// the kind_of chain). Empty when the def has none (damage model disabled).</summary>
    public List<DestroyablePart> DestroyableParts = new();

    /// <summary>The def-authored whole-vehicle pair ('armor'/'health', nearest def in the
    /// damage chain) — the AI base defs carry one (docs/formats/vehicle.md, fighters
    /// 64/64…100/100) and an AI load resolves it alongside an empty
    /// <see cref="DestroyableParts"/>, so the pair IS the whole model. No player def resolves
    /// either, so both stay null on a player load and <see cref="PlaneDamage"/> seeds the whole
    /// pair as the sum over parts instead.</summary>
    public float? VehicleArmor;

    public float? VehicleHealth;

    /// <summary>The def-level 'injure_anims': descending HP-fraction thresholds → whole-plane
    /// effect anims (docs/formats/vehicle.md). An AI load resolves the AI def's own ladder
    /// instead of the player's two-entry one below.
    /// ⚠ Read as "any part's fraction crosses the threshold" — the exact original trigger is
    /// undecoded.</summary>
    public List<(float Frac, string Anim)> VehicleInjureAnims = new();

    /// <summary>The def's <c>turrets</c> block — the host→gunner link the carried half of
    /// <c>ai.zrd</c> is looked up through (empty on the six non-turret airframes). Both viewpoint
    /// rigs are parsed; CSVM has no cockpit view, so only the <c>thirdp</c> entries are built.</summary>
    public List<TurretMount> TurretMounts = new();

    /// <summary>The player airframe as flown by a person: every property, including the damage
    /// model, resolves down the <c>player_airplane</c> chain.</summary>
    public static PlaneStats Load(string zrdrPath, string planeNodeName) =>
        LoadCore(zrdrPath, planeNodeName, forAi: false);

    /// <summary>The same airframe as flown by the AI. Everything except the damage model still
    /// resolves down the player chain (<see cref="DefName"/>, <see cref="TurretMounts"/> and the
    /// built model must stay there — the loadout table and the rig key off them). The damage
    /// model instead resolves down the AI def's own chain: an authored <c>armor</c>/<c>health</c>
    /// pair and no <c>destroyable_parts</c>, so an AI aircraft is zone-less.
    /// ⚠ The <c>r*</c> family carries parts but is the remote-player family; no roster spawns one.</summary>
    public static PlaneStats LoadForAi(string zrdrPath, string planeNodeName) =>
        LoadCore(zrdrPath, planeNodeName, forAi: true);

    /// <summary>The original's per-spawn dynamics jitter (docs/org/flightModel.md "The per-spawn
    /// jitter"). Returns a jittered COPY: the caller's object is the shared per-airframe cache.
    /// ⚠ Only the whole-vehicle pair scales, never per-part pools — <see cref="LoadForAi"/>'s
    /// zone-less pair takes the full effect; a player airframe's resolved sum is written out
    /// explicitly so <see cref="PlaneDamage"/> sees the scaled hull. <c>veh_weight</c>/<c>ref_area</c>
    /// are not among the jittered slots.</summary>
    public PlaneStats WithAiSpawnJitter(Random rng)
    {
        // Shallow: DestroyableParts / TurretMounts / VehicleInjureAnims are read-only after Load and
        // nothing below touches them, so the copy shares them with the cached original on purpose.
        var jittered = (PlaneStats)MemberwiseClone();
        jittered.VehicleHealth = (VehicleHealth ?? SumParts(static p => p.MaxHp)) * Factor(rng);
        jittered.VehicleArmor = (VehicleArmor ?? SumParts(static p => p.MaxArmor)) * Factor(rng);
        jittered.FdSpeed *= Factor(rng);
        jittered.EnginePower *= Factor(rng);
        jittered.DragFactor *= Factor(rng);
        jittered.PitchTorque *= Factor(rng);
        jittered.RollTorque *= Factor(rng);
        return jittered;
    }

    private static PlaneStats LoadCore(string zrdrPath, string planeNodeName, bool forAi)
    {
        var vehicleRoot = Zrdr.LoadFile(zrdrPath, "vehicle.json")[0] as List<object?>
            ?? throw new InvalidOperationException("vehicle.json: unexpected root shape");

        // defs alternate: name, property-list, name, property-list, ...
        var defs = new Dictionary<string, ZrdrDict>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        for (int i = 0; i + 1 < vehicleRoot.Count; i += 2)
            if (vehicleRoot[i] is string name && vehicleRoot[i + 1] is List<object?> props)
            {
                defs[name] = ZrdrDict.FromAlternating(props);
                order.Add(name);
            }

        List<ZrdrDict> Chain(string defName)
        {
            var chain = new List<ZrdrDict>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var cur = defName; cur != null && seen.Add(cur) && defs.TryGetValue(cur, out var d);)
            {
                chain.Add(d);
                cur = d.Str("kind_of")!;
            }
            return chain; // derived first, base last
        }

        // Requiring player_airplane in the chain is what makes the nodename match unique — the
        // AI and wingman variants share it. Runs for both flavours.
        string? found = null;
        List<ZrdrDict>? chain = null;
        foreach (var name in order)
        {
            var c = Chain(name);
            bool isPlayer = false;
            foreach (var d in c)
                if (string.Equals(d.Str("kind_of"), "player_airplane", StringComparison.OrdinalIgnoreCase))
                    isPlayer = true;
            if (!isPlayer)
                continue;
            foreach (var d in c)
                if (d.Has("nodename"))
                {
                    if (string.Equals(d.Str("nodename"), planeNodeName, StringComparison.OrdinalIgnoreCase))
                        found = name;
                    break; // nearest nodename in the chain decides
                }
            if (found != null) { chain = c; break; }
        }
        if (found == null || chain == null)
            throw new ArgumentException($"no player vehicle def with nodename '{planeNodeName}' in vehicle.json");

        // pfury -> fury; cannot be found by nodename since bare AI defs author none of their own.
        // Fails loud rather than silently falling back to the player chain.
        string? aiName = null;
        List<ZrdrDict>? aiChain = null;
        if (forAi)
        {
            aiName = found.StartsWith("p", StringComparison.OrdinalIgnoreCase) ? found[1..] : null;
            if (aiName == null || !defs.ContainsKey(aiName))
                throw new ArgumentException(
                    $"no AI vehicle def for player def '{found}' (looked for '{aiName ?? found}') in vehicle.json");
            aiChain = Chain(aiName);
        }

        // Where the damage model comes from: the AI chain on an AI load, the player chain otherwise.
        var damageChain = aiChain ?? chain;

        // property lookup: nearest def in the chain wins
        float Prop(string key, float fallback)
        {
            foreach (var d in chain)
                if (d.TryFloat(key, out var f))
                    return f;
            return fallback;
        }
        float Dyn(string key, float fallback, int index = 0)
        {
            foreach (var d in chain)
                if (d.Dict("dynamics") is { } dyn && dyn.TryFloat(key, out var f, index))
                    return f;
            return fallback;
        }
        string PropStr(string key, string fallback)
        {
            foreach (var d in chain)
                if (d.Str(key) is { } s)
                    return s;
            return fallback;
        }

        var stats = new PlaneStats
        {
            DefName = found,
            NodeName = planeNodeName,
            AiDefName = aiName,
            PitchTorque = Dyn("pitch_torque", 2.4f),
            RollTorque = Dyn("roll_torque", 6f),
            RudderTorque = Dyn("rudder_torque", 1.4f),
            ReturnRate = Dyn("return_rate", 3f),
            AngMomentumDamp = Dyn("ang_momentum_damp", 5f),
            RecInertia = new Vector3(
                Dyn("rec_moments_inertia", 0.8f, 0),
                Dyn("rec_moments_inertia", 0.6f, 1),
                Dyn("rec_moments_inertia", 1.3f, 2)),
            FdSpeed = Dyn("fd_speed", 113f),
            DragFactor = Dyn("drag_factor", 1f),
            VehWeight = Dyn("veh_weight", 3500f),
            RefArea = Dyn("ref_area", 335f),
            FlightCeiling = Prop("flight_ceiling", 2500f),
            AiAttackRange = Prop("attack", 2000f),
            AiReturnRange = Prop("return_range", 1200f),
            // Fallbacks are the def initialiser's compiled defaults, not guesses — see the fields.
            AiInputScaleRoll = Prop("ai_input_scale_roll", 3.5f),
            AiInputScalePitch = Prop("ai_input_scale_pitch", 3.5f),
            AiInputScaleYaw = Prop("ai_input_scale_yaw", 3.5f),
            AiInputLimitRoll = Prop("ai_input_limit_roll", 1f),
            AiInputLimitPitch = Prop("ai_input_limit_pitch", 1f),
            AiInputLimitYaw = Prop("ai_input_limit_yaw", 1f),
            RudderTol = Prop("rudder_tol", 0.2f),
        };
        stats.EngineSound = PropStr("engine_sound", stats.EngineSound);

        // The whole-vehicle pair, only when the chain actually authors it (AI defs do; player
        // chains carry neither key, and Prop's fallback would invent a pool).
        float? PropOpt(string key)
        {
            foreach (var d in damageChain)
                if (d.TryFloat(key, out var f))
                    return f;
            return null;
        }

        stats.VehicleArmor = PropOpt("armor");
        stats.VehicleHealth = PropOpt("health");

        // def-level injure_anims: [frac, animName] pairs (smoke trail / fuel leak). The player's
        // two-stage ladder, or the AI defs' own seven (eight on the balmoral).
        foreach (var d in damageChain)
        {
            if (d.List("injure_anims") is not { } injureList)
                continue;
            foreach (var item in injureList)
                if (item is List<object?> { Count: >= 2 } entry
                    && entry[0] is float frac && entry[1] is string anim)
                    stats.VehicleInjureAnims.Add((frac, anim));
            break;
        }

        // damaged_engine_sound: [[soundName, fadeStart, fadeEnd]] — see DamagedEngineSound's doc.
        foreach (var d in chain)
        {
            if (d.List("damaged_engine_sound") is not { Count: > 0 } dmgList)
                continue;
            if (dmgList[0] is List<object?> { Count: >= 3 } entry
                && entry[0] is string dmgName && entry[1] is float fadeStart && entry[2] is float fadeEnd)
            {
                stats.DamagedEngineSound = dmgName;
                stats.DamagedEngineGain = new SoundCurve(fadeStart, 0f, fadeEnd, 1f);
            }
            break;
        }

        // turrets: nearest def in the chain that has the block. Keyed by viewpoint
        // (firstp/thirdp), each entry an alternating [title, [MSG_TUR_*], node, [turretNode]].
        foreach (var d in chain)
        {
            if (d.Dict("turrets") is not { } turrets)
                continue;
            foreach (var (viewKey, firstPerson) in new[] { ("firstp", true), ("thirdp", false) })
            {
                if (turrets.List(viewKey) is not { } mounts)
                    continue;
                foreach (var item in mounts)
                {
                    if (item is not List<object?> entry)
                        continue;
                    var mount = ZrdrDict.FromAlternating(entry);
                    if (mount.Str("title") is { } title && mount.Str("node") is { } node)
                        stats.TurretMounts.Add(new TurretMount
                        {
                            Title = title,
                            Node = node,
                            FirstPerson = firstPerson,
                        });
                }
            }
            break;
        }

        // destroyable_parts schema: docs/formats/vehicle.md. An AI load resolves none — no
        // roster-named chain authors the block, so an AI aircraft is zone-less.
        foreach (var d in damageChain)
        {
            if (d.List("destroyable_parts") is not { } partsList)
                continue;
            foreach (var item in partsList)
            {
                if (item is not List<object?> p || p.Count == 0 || p[0] is not string partName)
                    continue;
                var part = new DestroyablePart { Name = partName };
                bool hpSet = false, armorSet = false;
                for (int i = 1; i < p.Count; i++)
                {
                    switch (p[i])
                    {
                        case float hp when !hpSet:
                            part.MaxHp = hp;
                            hpSet = true;
                            break;
                        case float armor when !armorSet:
                            part.MaxArmor = armor;
                            armorSet = true;
                            break;
                        case "critical":
                            part.Critical = true;
                            break;
                        case "engine":
                            part.Engine = true;
                            break;
                        case "got_hit_anim" when i + 1 < p.Count && p[i + 1] is List<object?> hit:
                            part.GotHitAnim = hit.Count > 0 ? hit[0] as string : null;
                            i++;
                            break;
                        case "injure_anims" when i + 1 < p.Count && p[i + 1] is List<object?> anims:
                            foreach (var a in anims)
                                if (a is List<object?> { Count: >= 2 } entry
                                    && entry[0] is float frac && entry[1] is string anim)
                                    part.InjureAnims.Add((frac, anim));
                            i++;
                            break;
                    }
                }
                stats.DestroyableParts.Add(part);
            }
            break;
        }

        // stock engine power factor: 'engine' prop → engines.json row [id, name, power]
        int engineId = (int)Prop("engine", 0f);
        foreach (var row in Zrdr.LoadFile(zrdrPath, "engines.json"))
            if (row is List<object?> { Count: >= 3 } r && r[0] is float id && (int)id == engineId && r[2] is float power)
                stats.EnginePower = power;

        // global flight constants + sound curves
        if (Zrdr.LoadFile(zrdrPath, "player.json")[0] is List<object?> playerList)
        {
            var player = ZrdrDict.FromAlternating(playerList);
            stats.Gravity = player.Float("nom_gravity", stats.Gravity);
            stats.StallMag = player.Float("stall_mag", stats.StallMag);
            stats.WarningShotMax = player.Float("warning_shot_max", stats.WarningShotMax);
            stats.WarningShotDissipation = player.Float("warning_shot_dissipation", stats.WarningShotDissipation);
            stats.WarningShotInterval = player.Float("warning_shot_interval", stats.WarningShotInterval);
            stats.WarningShotSound = player.Str("warning_shot_sound") ?? stats.WarningShotSound;
            stats.StickyBulletCatchupRate = player.Float("sticky_bullet_catchup_rate", stats.StickyBulletCatchupRate);
            stats.StickyBulletForgetInterval = player.Float("sticky_bullet_forget_interval", stats.StickyBulletForgetInterval);
            stats.StickyBulletDistFactor = player.Float("sticky_bullet_dist_factor", stats.StickyBulletDistFactor);
            // Degrees in the file, radians in the field — the original's own parse-time conversion.
            // ⚠ Do NOT reproduce the executable's missing-key bug here (its absent-inaccuracy branch
            // writes catchup_rate's global); the shipped player.json always carries the key.
            stats.StickyBulletInaccuracy = Mathf.DegToRad(
                player.Float("sticky_bullet_inaccuracy", Mathf.RadToDeg(stats.StickyBulletInaccuracy)));

            // Flight globals for the decoded model — see the field comments above for units and
            // fallback provenance. Not yet read by FlightModel.cs.
            const float mph = PhysicsConstants.MphToMs;
            stats.LiftAccelRate = player.Float("lift_accel_rate", stats.LiftAccelRate);
            stats.LiftAoaCosLo = player.TryFloat("liftAOAs", out var liftAoaLoDeg, 0)
                ? Mathf.Cos(Mathf.DegToRad(liftAoaLoDeg)) : stats.LiftAoaCosLo;
            stats.LiftAoaCosHi = player.TryFloat("liftAOAs", out var liftAoaHiDeg, 1)
                ? Mathf.Cos(Mathf.DegToRad(liftAoaHiDeg)) : stats.LiftAoaCosHi;
            stats.MaxAoaCos = player.TryFloat("maxAOA", out var maxAoaDeg, 0)
                ? Mathf.Cos(Mathf.DegToRad(maxAoaDeg)) : stats.MaxAoaCos;
            stats.HighGStart = player.Float("highGs", stats.HighGStart, 0);
            stats.HighGMax = player.Float("highGs", stats.HighGMax, 1);
            stats.LowGStart = player.Float("lowGs", stats.LowGStart, 0);
            stats.LowGMax = player.Float("lowGs", stats.LowGMax, 1);
            stats.TurnFadeIn = player.Float("turn_fade_in", stats.TurnFadeIn / mph) * mph;
            stats.TurnFadeOut = player.Float("turn_fade_out", stats.TurnFadeOut / mph) * mph;
            stats.YawLowSpeed = player.Float("yaw_low_speed", stats.YawLowSpeed);
            stats.YawHighSpeed = player.Float("yaw_high_speed", stats.YawHighSpeed);
            stats.YawFadeIn = player.Float("yaw_fade_in", stats.YawFadeIn / mph) * mph;
            stats.YawMax = player.Float("yaw_max", stats.YawMax / mph) * mph;
            stats.YawFadeOut = player.Float("yaw_fade_out", stats.YawFadeOut / mph) * mph;
            stats.HighSpeedPitchFadeLo = player.Float("high_speed_pitch_fade", stats.HighSpeedPitchFadeLo / mph, 0) * mph;
            stats.HighSpeedPitchFadeHi = player.Float("high_speed_pitch_fade", stats.HighSpeedPitchFadeHi / mph, 1) * mph;
            stats.DragFadeSpeed = player.Float("drag_fade_speed", stats.DragFadeSpeed / mph) * mph;
            // Raw scalars, metres already — no MPH conversion on any of the three.
            stats.GroundBlowElev = player.Float("groundblow_elev", stats.GroundBlowElev);
            stats.GroundBlowMag = player.Float("groundblow_mag", stats.GroundBlowMag);
            stats.AiGroundBlow = player.Float("ai_groundblow", stats.AiGroundBlow);
            // bounce_factor sits inside the `crash` block, beside the two damage ranges; a missing
            // block keeps the compiled fallback, which is the original's own parse order.
            if (player.Dict("crash") is { } crash)
            {
                stats.BounceFactor = crash.Float("bounce_factor", stats.BounceFactor);
                // Both ranges are [floor, scale] in that order, the original's own element order.
                stats.CollideArmorFloor = crash.Float("armor_damage_range", stats.CollideArmorFloor);
                stats.CollideArmorScale = crash.Float("armor_damage_range", stats.CollideArmorScale, 1);
                stats.CollideHealthFloor = crash.Float("health_damage_range", stats.CollideHealthFloor);
                stats.CollideHealthScale = crash.Float("health_damage_range", stats.CollideHealthScale, 1);
            }

            // curve blocks hold (x, y) pairs: min_* = ramp start, max_* = ramp end
            static SoundCurve Curve(ZrdrDict d, string minKey, string maxKey, SoundCurve fb) =>
                d.Has(minKey) && d.Has(maxKey)
                    ? new SoundCurve(d.Float(minKey, fb.MinX), d.Float(minKey, fb.MinY, 1),
                                     d.Float(maxKey, fb.MaxX), d.Float(maxKey, fb.MaxY, 1))
                    : fb;
            if (player.Dict("engine_sound") is { } eng)
            {
                stats.EngineVolume = Curve(eng, "min_throttle_volume", "max_throttle_volume", stats.EngineVolume);
                stats.EnginePitch = Curve(eng, "min_throttle_pitch", "max_throttle_pitch", stats.EnginePitch);
            }
            if (player.Dict("prop_sound") is { } prop)
            {
                stats.WhineVolume = Curve(prop, "min_speed_volume", "max_speed_volume", stats.WhineVolume);
                stats.WhinePitch = Curve(prop, "min_speed_pitch", "max_speed_pitch", stats.WhinePitch);
            }
            if (player.Dict("rattle") is { } rattle)
            {
                stats.RattleSound = rattle.Str("sound") ?? stats.RattleSound;
                stats.RattleVolume = new SoundCurve(
                    rattle.Float("speed_range", stats.RattleVolume.MinX),
                    rattle.Float("volume_range", stats.RattleVolume.MinY),
                    rattle.Float("speed_range", stats.RattleVolume.MaxX, 1),
                    rattle.Float("volume_range", stats.RattleVolume.MaxY, 1));
            }
        }
        return stats;
    }

    private static float Factor(Random rng) =>
        (float)((rng.NextDouble() * 2.0 - 1.0) * AiSpawnJitterSpread + 1.0);

    private float SumParts(Func<DestroyablePart, float> of)
    {
        float total = 0f;
        foreach (var part in DestroyableParts)
            total += of(part);
        return total;
    }
}
