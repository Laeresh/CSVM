using System;
using System.Collections.Generic;
using CrimsonSkies.Mech3;
using Godot;

namespace CrimsonSkies.Flight;

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
    public float Gravity = 20f;          // nom_gravity — the game's arcade gravity, m/s²
    public float StallMag = 1.25f;

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
