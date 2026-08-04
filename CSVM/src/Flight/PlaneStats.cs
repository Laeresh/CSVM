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
/// The pair is (hit points, armor) — armor is spent first (`BL-085`,
/// docs/formats/vehicle.md "The hp pair: armor + hit points"); a def with only one
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
    public string DefName = "";          // vehicle.json def, e.g. "pbloodhawk"
    public string NodeName = "";         // GameZ node, e.g. "player_bhawk"

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

    // player.json globals
    public float Gravity = PhysicsConstants.NomGravity; // nom_gravity — the game's arcade gravity, m/s²
    public float StallMag = 1.25f;

    // The near-miss cue's shipped accumulator (warning_shot_*) — see WarningShotCue for the units
    // question. The sound is a SOUND_GROUPS name (bullet_warning_sg → snd_bulletpass1-3), not a
    // sounds.json def, so it resolves through the group table like every other one.
    public float WarningShotMax = 2f;
    public float WarningShotDissipation = 2f;   // intensity per second
    public float WarningShotInterval = 1f;      // s between cues
    public string WarningShotSound = "bullet_warning_sg";

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

    /// <summary>The def-level 'injure_anims' (distinct from each part's): descending
    /// HP-fraction thresholds → whole-plane effect anims — [0.10 player_smoketrail]
    /// (the dying plane's dense_firetrail smoke) and [0.85 player_fuelleak]. Read as
    /// "any part's fraction crosses the threshold" (assumption — the exact original
    /// trigger is undecoded; a total-HP reading could never fire 0.10 before a
    /// critical part died at 75% total).</summary>
    public List<(float Frac, string Anim)> VehicleInjureAnims = new();

    public static PlaneStats Load(string zrdrPath, string planeNodeName)
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

        // Find the player def whose nodename matches (pbloodhawk for player_bhawk);
        // require player_airplane in the chain to skip the AI wingman variants.
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
        };
        stats.EngineSound = PropStr("engine_sound", stats.EngineSound);

        // def-level injure_anims: [frac, animName] pairs (smoke trail / fuel leak)
        foreach (var d in chain)
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

        // destroyable_parts: nearest def in the chain that has the block. Each part
        // is [name, hp, armor, flags…, "got_hit_anim", [anim, root], "injure_anims",
        // [[frac, anim, root], …]] — the pair is (hit points, armor); the two values
        // are equal for the stock player defs (AI variants and the armory diverge
        // them), and a def carrying only one float has no armor.
        foreach (var d in chain)
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
}
