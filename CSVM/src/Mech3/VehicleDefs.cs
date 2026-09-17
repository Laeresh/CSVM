using System;
using System.Collections.Generic;

namespace CSVM.Mech3;

/// <summary>
/// The <c>vehicle.json</c> def table read for what a roster spawn needs to know about a block
/// before an airframe is built: whether the def exists, which <c>mode</c> it resolves through
/// <c>kind_of</c>, which player airframe node its model is, and whether it derives from that
/// airframe's base def. <see cref="Flight.PlaneStats"/> is the full read of one def; this is the
/// index over all of them (docs/formats/vehicle.md "<c>mode</c>", docs/org/aiPilot.md).
/// </summary>
public sealed class VehicleDefs
{
    /// <summary>The <c>mode</c> every def without one inherits at the root.</summary>
    public const string JetMode = "jet";

    /// <summary>The <c>mode</c> that flies the escort law when the block is netless.</summary>
    public const string WingmanMode = "wingman";

    /// <summary>The <c>mode</c> of a surface vehicle: a hull on the water with no airframe, driven
    /// by the scripted-path law rather than the flight model (docs/org/flightModel.md).</summary>
    public const string ShipMode = "ship";

    /// <summary>The attack radius a def authoring no <c>attack</c> anywhere up its <c>kind_of</c>
    /// chain carries, metres: the def record's own constructed default, 160000 / -400 / +400 laid
    /// down at <c>FUN_00478a00</c> and copied onto the vehicle unconditionally at
    /// <c>FUN_00475820</c>. Both shipped hull defs take it, so 400 m is what a patrol boat's and a
    /// turret truck's scorer actually tests against (docs/org/aiPilot.md).</summary>
    public const float DefaultAttackRadiusM = 400f;

    private readonly Dictionary<string, ZrdrDict> _defs = new(StringComparer.OrdinalIgnoreCase);

    private VehicleDefs()
    {
    }

    /// <summary>Every def name, in file order.</summary>
    public IReadOnlyList<string> Names { get; private set; } = Array.Empty<string>();

    /// <summary>Loads the table from the shared zrdr scope's <c>vehicle.json</c>.</summary>
    public static VehicleDefs Load(string zrdrPath)
    {
        if (Zrdr.LoadFile(zrdrPath, "vehicle.json")[0] is not List<object?> root)
            throw new InvalidOperationException("vehicle.json: unexpected root shape");
        return FromRoot(root);
    }

    /// <summary>The table over an already-parsed alternating name/properties list, for tests
    /// and for callers that have the file open.</summary>
    public static VehicleDefs FromRoot(List<object?> root)
    {
        var defs = new VehicleDefs();
        var names = new List<string>();
        for (int i = 0; i + 1 < root.Count; i += 2)
        {
            if (root[i] is string name && root[i + 1] is List<object?> props)
            {
                defs._defs[name] = ZrdrDict.FromAlternating(props);
                names.Add(name);
            }
        }
        defs.Names = names;
        return defs;
    }

    /// <summary>The def a roster block spawns from: the block name with its trailing
    /// <c>_N</c> ordinals stripped until a def matches (<c>blakepeace_2_1</c> is the
    /// <c>blakepeace_2</c> def, <c>patrolboat_eg0</c> is <c>patrolboat</c>). Null when no
    /// prefix is a def.</summary>
    public string? DefForBlock(string blockName)
    {
        string name = blockName;
        while (true)
        {
            if (_defs.ContainsKey(name))
                return name;
            int cut = name.LastIndexOf('_');
            if (cut <= 0)
                return null;
            name = name[..cut];
        }
    }

    /// <summary>Whether the def exists.</summary>
    public bool Has(string def) => _defs.ContainsKey(def);

    /// <summary>The def's <c>mode</c>, the nearest authored one up its <c>kind_of</c> chain,
    /// <see cref="JetMode"/> when none is (the engine's own zero default). Null for an unknown
    /// def.</summary>
    public string? ModeOf(string def)
    {
        foreach (var d in Chain(def))
        {
            if (d.Str("mode") is { Length: > 0 } mode)
                return mode;
        }
        return Has(def) ? JetMode : null;
    }

    /// <summary>Whether <paramref name="def"/> is <paramref name="baseDef"/> or derives from it
    /// through <c>kind_of</c>.</summary>
    public bool DerivesFrom(string def, string baseDef)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (string? cur = def; cur != null && seen.Add(cur) && _defs.TryGetValue(cur, out var d);)
        {
            if (string.Equals(cur, baseDef, StringComparison.OrdinalIgnoreCase))
                return true;
            cur = d.Str("kind_of");
        }
        return false;
    }

    /// <summary>The player airframe node an AI def's model is built from, resolved the way
    /// <see cref="Flight.PlaneStats.LoadForAi"/> wants it named: the first ancestor whose
    /// <c>p</c>-prefixed twin is a player def (<c>wingman</c> → <c>devastator</c> →
    /// <c>pdevastator</c> → <c>player_pfighter</c>), else the nearest <c>nodename</c> up the chain
    /// mapped the same way (<c>bswingman</c> names <c>fury</c> → <c>pfury</c>). Null for a def
    /// with no player twin, which is every surface vehicle.</summary>
    public (string PlaneNode, string BaseDef)? AirframeFor(string def)
    {
        foreach (var ancestor in ChainNames(def))
        {
            if (PlayerNodeOf("p" + ancestor) is { } node)
                return (node, ancestor);
        }
        foreach (var d in Chain(def))
        {
            if (d.Str("nodename") is { Length: > 0 } modelName && PlayerNodeOf("p" + modelName) is { } node)
                return (node, modelName);
        }
        return null;
    }

    /// <summary>The inverse of <see cref="AirframeFor"/>: the base AI def behind a player
    /// airframe node (<c>player_pfighter</c> → <c>pdevastator</c> → <c>devastator</c>), or null
    /// when no <c>p</c>-prefixed player def names that node.</summary>
    public string? BaseDefForPlayerNode(string planeNode)
    {
        foreach (var name in Names)
        {
            if (name.StartsWith('p') && name.Length > 1
                && string.Equals(PlayerNodeOf(name), planeNode, StringComparison.OrdinalIgnoreCase)
                && Has(name[1..]))
                return name[1..];
        }
        return null;
    }

    /// <summary>The def's <c>start_anims</c>, the nearest authored list up the <c>kind_of</c>
    /// chain: the animations a spawned vehicle plays as it enters the world (a boat's wake).
    /// Empty when nothing up the chain authors one.</summary>
    public IReadOnlyList<string> StartAnimsOf(string def)
    {
        foreach (var d in Chain(def))
        {
            if (d.List("start_anims") is { } list)
            {
                var names = new List<string>();
                foreach (var entry in list)
                {
                    if (entry is string name && name.Length > 0)
                        names.Add(name);
                }
                return names;
            }
        }
        return Array.Empty<string>();
    }

    /// <summary>The def's <c>injure_anims</c> ladder, the nearest authored one up the chain:
    /// <c>(fraction, anim)</c> pairs, each played once as the vehicle's health falls through its
    /// fraction (docs/formats/vehicle.md). Empty when nothing up the chain authors one.</summary>
    public IReadOnlyList<(float Fraction, string Anim)> InjureAnimsOf(string def)
    {
        foreach (var d in Chain(def))
        {
            if (d.List("injure_anims") is { } list)
            {
                var ladder = new List<(float, string)>();
                foreach (var entry in list)
                {
                    if (entry is List<object?> { Count: >= 2 } pair && pair[0] is float f && pair[1] is string anim)
                        ladder.Add((f, anim));
                }
                return ladder;
            }
        }
        return Array.Empty<(float, string)>();
    }

    /// <summary>The def's <c>weapons</c> block, the nearest authored list up the <c>kind_of</c>
    /// chain, as the 5-tuples <c>[weapon_id, rounds, refire_s, min_range_m, max_range_m]</c>
    /// (docs/org/aiPilot/aiWeapons.md, "The weapon list, and who builds it"). ⚠ That builder runs
    /// for EVERY vehicle the def parser sees, a hull as much as an aeroplane, which is why this
    /// lives here and not on the aircraft path. Empty when nothing up the chain authors one.</summary>
    public IReadOnlyList<Flight.AiWeaponSlot> WeaponsOf(string def)
    {
        foreach (var d in Chain(def))
        {
            if (d.List("weapons") is not { } list)
                continue;
            var slots = new List<Flight.AiWeaponSlot>();
            foreach (var entry in list)
            {
                if (entry is List<object?> { Count: >= 5 } w && w[0] is string id
                    && w[1] is float rounds && w[2] is float refire
                    && w[3] is float minRange && w[4] is float maxRange)
                {
                    slots.Add(new Flight.AiWeaponSlot
                    {
                        WeaponId = id,
                        Rounds = (int)rounds,
                        RefireSeconds = refire,
                        MinRangeM = minRange,
                        MaxRangeM = maxRange,
                    });
                }
            }
            return slots;
        }
        return Array.Empty<Flight.AiWeaponSlot>();
    }

    /// <summary>The def's <c>attack</c>, the nearest authored one up the <c>kind_of</c> chain: the
    /// radius of the target-admission cylinder BOTH scorers test a candidate against, metres.
    /// Null when nothing up the chain authors one, which is the case for both shipped hull defs
    /// and leaves <see cref="DefaultAttackRadiusM"/> standing.
    /// ⚠ Never the def's <c>activation</c>, which gates whether the vehicle simulates at all and
    /// is six times larger on a hull (docs/org/aiPilot.md).</summary>
    public float? AttackOf(string def)
    {
        foreach (var d in Chain(def))
        {
            if (d.TryFloat("attack", out float attack))
                return attack;
        }
        return null;
    }

    private string? PlayerNodeOf(string playerDef) =>
        _defs.TryGetValue(playerDef, out var d) && d.Str("nodename") is { Length: > 0 } node ? node : null;

    private IEnumerable<ZrdrDict> Chain(string def)
    {
        foreach (var name in ChainNames(def))
            yield return _defs[name];
    }

    // Derived first, base last, cycle-safe.
    private IEnumerable<string> ChainNames(string def)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (string? cur = def; cur != null && seen.Add(cur) && _defs.TryGetValue(cur, out var d);)
        {
            yield return cur;
            cur = d.Str("kind_of");
        }
    }
}
