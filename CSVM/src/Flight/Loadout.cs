using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>The parsed <c>CSVM/data/stock_loadouts.json</c> — the 11 aircraft's default weapon
/// fit, hand-authored config (see <see href="../../docs/formats/loadouts.md">loadouts.md</see>).
/// Pure data: no plane binding, no weapon resolution — <see cref="Loadout.Bind"/> does that
/// against a built plane.</summary>
public sealed class StockLoadouts
{
    // ammo name -> the offset into a caliber's four consecutive wep ids (slug X0 .. magnesium X3).
    private static readonly Dictionary<string, int> AmmoIndex = new(StringComparer.OrdinalIgnoreCase)
    {
        ["slug"] = 0,
        ["dumdum"] = 1,
        ["ap"] = 2,
        ["magnesium"] = 3,
    };

    private readonly Dictionary<string, LoadoutDef> _byDef = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The committed config's default location (res://), independent of <c>--data-root</c>:
    /// it is engine config, not extracted game data. Kept as a res:// path and read through
    /// <see cref="Godot.FileAccess"/>, because in an exported build the file lives in the pck,
    /// where <c>GlobalizePath</c> + System.IO cannot reach it (B11, 2026-08-05).</summary>
    public static string DefaultPath => "res://data/stock_loadouts.json";

    public IReadOnlyDictionary<string, LoadoutDef> All => _byDef;

    /// <summary>A gun's stock weapon id: caliber N + ammo k → <c>wep_{N+k}</c> (the wep_30..73
    /// player matrix, each caliber's four ammo types consecutive). Stock ammo <c>slug</c> → <c>wep_N</c>.</summary>
    public static string GunWeaponId(int caliber, string ammo) =>
        $"wep_{caliber + (AmmoIndex.TryGetValue(ammo, out var k) ? k : 0)}";

    /// <summary>Loads the stock-loadout file (defaults to <see cref="DefaultPath"/>).</summary>
    public static StockLoadouts Load(string? path = null)
    {
        path ??= DefaultPath;
        var loadouts = new StockLoadouts();
        // res:// lives inside the pck in an exported build, where only Godot's own FileAccess
        // can read it; an explicit disk path (unit tests, tools) stays on System.IO, which the
        // xunit host can run without a Godot runtime.
        bool viaGodot = path.StartsWith("res://", StringComparison.Ordinal);
        if (viaGodot ? !Godot.FileAccess.FileExists(path) : !File.Exists(path))
        {
            GD.PushWarning($"stock loadouts: file not found, no loadouts loaded: {path}");
            return loadouts;
        }
        using var doc = JsonDocument.Parse(viaGodot ? Godot.FileAccess.GetFileAsBytes(path) : File.ReadAllBytes(path));
        if (!doc.RootElement.TryGetProperty("planes", out var planes) || planes.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"stock loadouts: no 'planes' object in {path}");
        }
        foreach (var plane in planes.EnumerateObject())
        {
            var body = plane.Value;
            var def = new LoadoutDef
            {
                Def = plane.Name,
                Model = Str(body, "model"),
                Display = Str(body, "display"),
            };
            if (body.TryGetProperty("guns", out var guns) && guns.ValueKind == JsonValueKind.Array)
            {
                foreach (var g in guns.EnumerateArray())
                {
                    var spec = new GunSpec
                    {
                        Slot = Int(g, "slot"),
                        Mount = Str(g, "mount"),
                        Caliber = Int(g, "caliber"),
                        Ammo = Str(g, "ammo"),
                        Turret = g.TryGetProperty("turret", out var t) && t.ValueKind == JsonValueKind.True,
                    };
                    if (g.TryGetProperty("markers", out var markers) && markers.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var m in markers.EnumerateArray())
                        {
                            if (m.GetString() is { } name)
                            {
                                spec.Markers.Add(name);
                            }
                        }
                    }
                    def.Guns.Add(spec);
                }
            }
            if (body.TryGetProperty("hardpoints", out var hp) && hp.ValueKind == JsonValueKind.Object)
            {
                def.Hardpoints = new HardpointSpec { Count = Int(hp, "count"), Stock = Str(hp, "stock") };
            }
            loadouts._byDef[def.Def] = def;
        }
        return loadouts;
    }

    /// <summary>The def's stock loadout, or null when the plane isn't in the file.</summary>
    public LoadoutDef? For(string defName) => _byDef.TryGetValue(defName, out var d) ? d : null;

    private static string Str(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static int Int(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
}

/// <summary>One plane's stock loadout, as authored — before binding to a model.</summary>
public sealed class LoadoutDef
{
    public string Def = "";       // "pbloodhawk"
    public string Model = "";     // "player_bhawk"
    public string Display = "";
    public List<GunSpec> Guns = new();
    public HardpointSpec? Hardpoints;
}

/// <summary>One authored gun slot (W1–W4).</summary>
public sealed class GunSpec
{
    public int Slot;
    public string Mount = "";
    public int Caliber;
    public string Ammo = "slug";
    public List<string> Markers = new();
    public bool Turret;           // an AI turret slot — parsed but built inert (M4)
}

/// <summary>The authored hardpoint block: pylon count + the stock ordnance id.</summary>
public sealed class HardpointSpec
{
    public int Count;
    public string Stock = "";
}

/// <summary>
/// A plane's stock loadout <b>bound to its built model</b>: each gun group's authored markers
/// resolved to live muzzle <see cref="Node3D"/>s and its caliber+ammo resolved to a
/// <see cref="WeaponDef"/>, each hardpoint bound to its pylon node — a live set of gun groups and
/// hardpoints with independent ammo counters, ready for the firing code (B16/B17) to draw from.
///
/// <para>Marker resolution is against the built tree's <c>cs_name</c> meta (as
/// <see cref="UI.MarkerOverlay"/> reads it). A named marker that is absent is a <b>loud error</b>
/// — a thrown exception naming the plane, slot and marker — never a silent skip, since a wrong
/// binding would silently fire a gun from nowhere.</para>
/// </summary>
public sealed class Loadout
{
    /// <summary>The original's hardpoint fill order — alternating wings, not sequential
    /// (`BL-294`/`PT-31`, user-observed at the controls against the weapon gauge's belt lights):
    /// a stock fit with fewer than 8 pylons leaves physical gaps rather than filling pylon1..N
    /// contiguously. <c>hp.Count</c> takes a PREFIX of this sequence.</summary>
    public static readonly int[] PylonFillOrder = { 1, 5, 2, 6, 3, 7, 4, 8 };

    private Loadout(LoadoutDef def, List<GunGroup> guns, List<Hardpoint> hardpoints)
    {
        Def = def;
        Guns = guns;
        Hardpoints = hardpoints;
    }

    public LoadoutDef Def { get; }
    public IReadOnlyList<GunGroup> Guns { get; }
    public IReadOnlyList<Hardpoint> Hardpoints { get; }

    /// <summary>The gun groups the player can actually fire (turret slots excluded — inert in M3).</summary>
    public IEnumerable<GunGroup> FirableGuns
    {
        get
        {
            foreach (var g in Guns)
            {
                if (!g.IsTurret)
                {
                    yield return g;
                }
            }
        }
    }

    /// <summary>Binds an authored loadout to a built plane and the weapon catalogue. Throws if a
    /// named marker is absent on the model or a resolved <c>wep_*</c> id is missing.</summary>
    public static Loadout Bind(LoadoutDef def, Node3D plane, WeaponDefs weapons)
    {
        var markerNodes = CollectMarkers(plane);

        var guns = new List<GunGroup>();
        foreach (var spec in def.Guns)
        {
            var weaponId = StockLoadouts.GunWeaponId(spec.Caliber, spec.Ammo);
            var weapon = weapons.Get(weaponId)
                ?? throw new InvalidOperationException(
                    $"loadout {def.Def} slot {spec.Slot} ({spec.Mount}): weapon '{weaponId}' " +
                    $"(caliber {spec.Caliber} + {spec.Ammo}) not in weapons.json");
            var muzzles = new List<Node3D>();
            foreach (var name in spec.Markers)
            {
                muzzles.Add(Resolve(markerNodes, name, def, $"slot {spec.Slot} ({spec.Mount})"));
            }
            int capacity = weapon.ClusterSize ?? 0;
            guns.Add(new GunGroup
            {
                Slot = spec.Slot,
                Mount = spec.Mount,
                Weapon = weapon,
                Muzzles = muzzles,
                Capacity = capacity,
                Ammo = capacity,
                IsTurret = spec.Turret,
            });
        }

        var hardpoints = new List<Hardpoint>();
        if (def.Hardpoints is { } hp && hp.Count > 0)
        {
            var weapon = weapons.Get(hp.Stock)
                ?? throw new InvalidOperationException(
                    $"loadout {def.Def}: hardpoint stock '{hp.Stock}' not in weapons.json");
            // Pylons bind via PylonFillOrder, not sequentially 1..N (BL-294) — count N takes the
            // sequence's first N entries, so a partial stock fit lands on both wings alternately
            // instead of piling onto one side.
            int perPylon = weapon.ClusterSize ?? 0;
            for (int i = 0; i < hp.Count; i++)
            {
                int pylonNumber = PylonFillOrder[i];
                var pylon = Resolve(markerNodes, $"pylon{pylonNumber}", def, "hardpoint");
                hardpoints.Add(new Hardpoint
                {
                    Index = pylonNumber,
                    Pylon = pylon,
                    Weapon = weapon,
                    Capacity = perPylon,
                    Ammo = perPylon,
                });
            }
        }

        return new Loadout(def, guns, hardpoints);
    }

    /// <summary>Synthesizes a lab loadout covering the airframe's <b>whole</b> marker rig — the
    /// four gun-group slots the reverse-index rule names (W1→firepoint(9−2n),(10−2n) for
    /// n=1..4 — <see href="../../docs/formats/markers.md">markers.md</see>, "Slot → firepoint
    /// binding"), each populated with whichever of its pair the airframe actually has (the
    /// Kestrel's W1 resolves to the lone centreline <c>firepoint7</c>), and one hardpoint per
    /// <c>pylonN</c> the rig carries — regardless of how few of either the stock fit binds. A slot
    /// the stock fit does name keeps its weapon, mount name and caliber; a slot it does not
    /// defaults to the stock's first gun weapon (<c>wep_30</c> when the plane has no stock guns at
    /// all) under a generic mount label. Every synthesized group is fireable (<c>IsTurret</c> is
    /// always false here, even for a slot stock marks as a turret) — a deliberate lab-only
    /// difference from stock, where the turret slot stays inert until M4. Runs the synthesized def
    /// through the same <see cref="Bind"/> every other loadout uses, so there stays exactly one
    /// bind path (and a marker the rig lacks still throws, never a silent skip).</summary>
    public static Loadout ForRig(Node3D plane, WeaponDefs weapons, LoadoutDef? stock)
    {
        var markerNodes = CollectMarkers(plane);
        var firepoints = new SortedSet<int>();
        var pylons = new SortedSet<int>();
        foreach (var name in markerNodes.Keys)
        {
            if (MarkerRig.Classify(name, out var kind, out int ord))
            {
                if (kind == MarkerRig.MarkerKind.Firepoint)
                {
                    firepoints.Add(ord);
                }
                else if (kind == MarkerRig.MarkerKind.Pylon)
                {
                    pylons.Add(ord);
                }
            }
        }

        GunSpec? firstStockGun = stock != null && stock.Guns.Count > 0 ? stock.Guns[0] : null;

        var def = new LoadoutDef
        {
            Def = stock?.Def ?? "",
            Model = stock?.Model ?? "",
            Display = stock?.Display ?? "",
        };

        for (int slot = 1; slot <= 4; slot++)
        {
            int lo = 9 - 2 * slot;   // slot1->7, slot2->5, slot3->3, slot4->1
            int hi = 10 - 2 * slot;  // slot1->8, slot2->6, slot3->4, slot4->2
            var markers = new List<string>();
            if (firepoints.Contains(lo))
            {
                markers.Add($"firepoint{lo}");
            }
            if (firepoints.Contains(hi))
            {
                markers.Add($"firepoint{hi}");
            }
            if (markers.Count == 0)
            {
                // Neither half of this slot's pair exists on the rig — nothing to synthesize.
                continue;
            }
            GunSpec? stockSpec = null;
            foreach (var g in stock?.Guns ?? new List<GunSpec>())
            {
                if (g.Slot == slot)
                {
                    stockSpec = g;
                    break;
                }
            }
            def.Guns.Add(new GunSpec
            {
                Slot = slot,
                Mount = stockSpec?.Mount ?? $"Gun Group {slot}",
                Caliber = stockSpec?.Caliber ?? firstStockGun?.Caliber ?? 30,
                Ammo = stockSpec?.Ammo ?? firstStockGun?.Ammo ?? "slug",
                Markers = markers,
                Turret = false,
            });
        }

        if (pylons.Count > 0)
        {
            // "wep_06" is the stock rocket every one of the 11 planes' hardpoints block names
            // (CSVM/data/stock_loadouts.json) — the fallback for the (never observed) case of a
            // plane with pylons but no stock hardpoints block at all.
            def.Hardpoints = new HardpointSpec
            {
                Count = pylons.Count,
                Stock = stock?.Hardpoints?.Stock is { Length: > 0 } s ? s : "wep_06",
            };
        }

        return Bind(def, plane, weapons);
    }

    private static Node3D Resolve(Dictionary<string, Node3D> markers, string name, LoadoutDef def, string where)
    {
        if (markers.TryGetValue(name, out var node))
        {
            return node;
        }
        throw new InvalidOperationException(
            $"loadout {def.Def} ({def.Model}) {where}: marker '{name}' not found on the built plane");
    }

    /// <summary>Builds a <c>cs_name → Node3D</c> map of the plane's marker nodes (firepoints,
    /// pylons, target) from the built tree — the same <c>cs_name</c> meta SceneBuilder stamps.</summary>
    private static Dictionary<string, Node3D> CollectMarkers(Node3D plane)
    {
        var map = new Dictionary<string, Node3D>(StringComparer.OrdinalIgnoreCase);
        void Walk(Node node)
        {
            foreach (var child in node.GetChildren())
            {
                if (child is Node3D n3d && n3d.HasMeta(AnimRuntime.NameMeta)
                    && MarkerRig.Classify(n3d.GetMeta(AnimRuntime.NameMeta).AsString(), out _, out _))
                {
                    // First-wins: marker names are unique per plane.
                    map.TryAdd(n3d.GetMeta(AnimRuntime.NameMeta).AsString(), n3d);
                }
                Walk(child);
            }
        }
        Walk(plane);
        return map;
    }
}

/// <summary>One live gun group: its resolved weapon, muzzle nodes, and an <b>independent</b> ammo
/// counter (the Balmoral's two .50 groups each carry their own — playtest-confirmed). Turret
/// groups are bound but inert in M3 (<see cref="IsTurret"/>). The <see cref="IGunSlot"/> face is
/// what <see cref="FireControl"/> fires through — the node-free slice of this class.</summary>
public sealed class GunGroup : IGunSlot
{
    public int Slot;
    public string Mount = "";
    public IReadOnlyList<Node3D> Muzzles = Array.Empty<Node3D>();
    public bool IsTurret;

    public WeaponDef Weapon { get; set; } = null!;

    public int Capacity { get; set; }  // CLUSTER_SIZE — the full per-group load

    public int Ammo { get; set; }      // mutable remaining rounds

    public int MuzzleCount => Muzzles.Count;

    public bool Empty => Ammo <= 0;
}

/// <summary>One live hardpoint (pylon): its resolved ordnance weapon and a per-pylon ammo counter
/// (rocket capacity = pylon count × CLUSTER_SIZE, per pylon — A9). The <see cref="IPylonSlot"/>
/// face is what <see cref="FireControl"/> launches through — the node-free slice of this class.</summary>
public sealed class Hardpoint : IPylonSlot
{
    public int Index;             // pylon number, 1-based
    public Node3D Pylon = null!;

    public WeaponDef Weapon { get; set; } = null!;

    public int Capacity { get; set; }  // CLUSTER_SIZE per pylon

    public int Ammo { get; set; }

    public bool Empty => Ammo <= 0;
}
