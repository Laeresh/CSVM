using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Extraction;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Roster;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The campaign wingman's weapons: the hangar pick it flies over its AI def's own
/// <c>weapons</c> block. Each is read back off a live spawn through the real roster
/// (docs/org/aiPilot/aiWeapons.md, "The wingman's fit").</summary>
internal static class WingmanFitSuites
{
    // The Fury authors its own weapons block through fury; the Balmoral's chain authors none.
    private const string FuryNode = "player_fury";
    private const string FuryWingDef = "wfury";
    private const string BalmoralNode = "player_balmoral";
    private const string BalmoralWingDef = "wbalmoral";

    // A pick no stock fit and no AI def carries. Armour-piercing and explosive rounds go in the two
    // wing gun slots, and a choker rocket in the left wing's first cell.
    private const string ChokerRocket = "wep_12";

    [Suite("campaign-wingman-fit",
        "a campaign wingman flies the fit picked for it in the hangar: over the shipped data nearly every "
        + "wingman def authors a weapons block, a Fury wingman carrying a non-stock pick binds the picked "
        + "ammunition and rocket rather than its def's weapons, with the original's wingman gates on every "
        + "slot, the same spawn without a pick keeps the def's weapons, and a Balmoral wingman, whose chain "
        + "authors no block, binds the weapons it always did")]
    internal static void AWingmanFliesItsPickedFit(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequirePlane("player_fury");
        var defs = VehicleDefs.Load(ctx.ZrdrPath);
        WingmanDefCensus(ctx, defs);

        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        var spawned = new List<FlightController>();
        ProjectilePool? projectiles = null;
        try
        {
            var live = new ProjectilePool(textures, null, null);
            projectiles = live;
            ctx.Host.AddChild(live);

            var spec = SessionSpec.Parse(System.Array.Empty<string>());
            var liveries = new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof"));
            var stock = StockLoadouts.Load();
            var weapons = WeaponDefs.Load(ctx.ZrdrPath, null);
            var inputs = new AircraftAssemblyResources
            {
                PlanesGamez = planesGamez,
                StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
                AiStatsFor = (plane, aiDef) => PlaneStats.LoadForAi(ctx.ZrdrPath, plane, aiDef),
                PaintRng = new RandomNumberGenerator(),
                ZrdrPath = ctx.ZrdrPath,
                StockLoadouts = stock,
                WeaponDefs = weapons,
                Textures = textures,
                Shakes = ShakeDefs.Load(ctx.ZrdrPath),
            };
            var roster = new FlightRoster(FlightRosterPolicy.From(spec), liveries, null!, ctx.Host, inputs,
                new FlightWorldBindings { Projectiles = live, Gamez = planesGamez }, new HumanRosterBindings());

            FlightController Spawn(string node, string aiDef, LoadoutChoice? fit, float x)
            {
                var at = new Vector3(x, 500f, 0f);
                var rig = roster.SpawnAi(new AiSpawn(node, at, at + Vector3.Forward,
                    AiPilot.HoldingCourse(at, at + Vector3.Forward), Team: AimAssist.PlayerTeam,
                    AiDef: aiDef, Fit: fit, AttackRating: 5));
                spawned.Add(rig);
                return rig;
            }

            var pick = Pick();
            var own = Spawn(FuryNode, FuryWingDef, null, 0f);
            var fury = Spawn(FuryNode, FuryWingDef, pick, 200f);
            var balmoral = Spawn(BalmoralNode, BalmoralWingDef, pick, 400f);

            // The control: with no pick, the def's own block is what binds.
            var authored = defs.WeaponsOf(FuryWingDef).Select(s => s.WeaponId).ToArray();
            ctx.Check(Ids(own).OrderBy(i => i).SequenceEqual(authored.OrderBy(i => i)),
                $"ABLE-TO-FAIL CONTROL: a Fury wingman with no pick flies {FuryWingDef}'s own block [{string.Join(",", authored)}] ({Describe(own)})");

            FuryCarriesThePick(ctx, fury, stock, weapons);

            // The Balmoral took the stock-table branch before the pick outranked the def. Its
            // weapons are what that branch binds from the same pick on the same rig.
            var before = stock.For("pbalmoral") is { } balmoralStock
                ? Loadout.Bind(pick.ApplyTo(balmoralStock), balmoral, weapons)
                : null;
            ctx.Check(before != null && Ids(balmoral).SequenceEqual(Ids(before)),
                $"a Balmoral wingman, whose chain authors no weapons block, binds the weapons the stock-table branch gave it ({Describe(balmoral)})");
        }
        finally
        {
            foreach (var rig in spawned)
            {
                rig.Free();
            }

            projectiles?.Free();
            textures.Dispose();
        }
    }

    // The data behind the issue: which wingman defs author a weapons block, directly or up their
    // kind_of chain. Every one does but the Balmoral's.
    private static void WingmanDefCensus(TestContext ctx, VehicleDefs defs)
    {
        var wingmen = defs.Names
            .Where(n => string.Equals(defs.ModeOf(n), VehicleDefs.WingmanMode, System.StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var def in wingmen)
        {
            var block = defs.WeaponsOf(def);
            ctx.Note($"wingman def {def}: {(block.Count == 0 ? "no weapons block" : string.Join(", ", block.Select(s => Log.Format($"{s.WeaponId} x{s.Rounds} {s.RefireSeconds:0.##} s {s.MinRangeM:0}-{s.MaxRangeM:0} m"))))}");
        }

        ctx.Check(wingmen.Contains(FuryWingDef) && defs.WeaponsOf(FuryWingDef).Count > 0,
            $"ABLE-TO-FAIL CONTROL: {FuryWingDef} is a wingman def and inherits a weapons block ({defs.WeaponsOf(FuryWingDef).Count} entries)");
        ctx.Check(wingmen.Contains(BalmoralWingDef) && defs.WeaponsOf(BalmoralWingDef).Count == 0,
            $"{BalmoralWingDef} is the wingman def whose chain authors none ({defs.WeaponsOf(BalmoralWingDef).Count} entries)");
        var bare = wingmen.Where(d => defs.WeaponsOf(d).Count == 0).ToArray();
        ctx.Note($"{wingmen.Length - bare.Length} of {wingmen.Length} wingman defs carry a weapons block; none: {string.Join(", ", bare)}");
    }

    private static void FuryCarriesThePick(TestContext ctx, FlightController fury, StockLoadouts stock, WeaponDefs weapons)
    {
        var loadout = fury.Loadout;
        var furyStock = stock.For("pfury");
        if (loadout == null || furyStock == null)
        {
            ctx.Check(false, $"the picked Fury wingman is armed and the Fury has a stock fit ({loadout != null}, {furyStock != null})");
            return;
        }

        var wantGuns = furyStock.Guns
            .Select(g => StockLoadouts.GunWeaponId(g.Caliber, Pick().GunAmmoFor(g.Slot) ?? g.Ammo))
            .ToArray();
        ctx.Check(loadout.Guns.Select(g => g.Weapon.Id).SequenceEqual(wantGuns),
            $"the picked Fury wingman's guns fire the picked ammunition [{string.Join(",", wantGuns)}] ({Describe(fury)})");
        int pylon = Loadout.PylonForCell(0, furyStock.Hardpoints);
        var picked = loadout.Hardpoints.FirstOrDefault(h => h.Index == pylon);
        ctx.Check(picked?.Weapon.Id == ChokerRocket && loadout.Hardpoints.Count == furyStock.Hardpoints?.Count,
            $"its left wing's first pylon{pylon} carries the picked {ChokerRocket}, on the Fury's own {furyStock.Hardpoints?.Count} pylons ({picked?.Weapon.Id}, {loadout.Hardpoints.Count})");
        ctx.Check(loadout.Hardpoints.All(h => h.Ammo == (weapons.Get(h.Weapon.Id)?.ClusterSize ?? -1)),
            $"each pylon carries its weapon's CLUSTER_SIZE, the rounds the original's wingman build gives a slot");
        ctx.Check(loadout.Hardpoints.All(h => h.MinRangeM == Loadout.WingmanMinRangeM && h.MaxRangeM == Loadout.WingmanMaxRangeM
                                              && h.RefireSeconds == Loadout.WingmanOrdnanceRefireS)
                  && loadout.Guns.All(g => g.MinRangeM == Loadout.WingmanMinRangeM && g.MaxRangeM == Loadout.WingmanMaxRangeM),
            $"every slot takes the wingman gates, {Loadout.WingmanMinRangeM:0} to {Loadout.WingmanMaxRangeM:0} m and {Loadout.WingmanOrdnanceRefireS:0} s between rockets, not a def's tuple");
    }

    private static LoadoutChoice Pick()
    {
        var fit = new LoadoutChoice();
        fit.SetGunAmmo(1, "ap");
        fit.SetGunAmmo(2, "magnesium");
        fit.SetWingCell(0, ChokerRocket);
        return fit;
    }

    private static string[] Ids(FlightController rig) => rig.Loadout is { } loadout ? Ids(loadout) : System.Array.Empty<string>();

    private static string[] Ids(Loadout loadout) =>
        loadout.Guns.Select(g => g.Weapon.Id).Concat(loadout.Hardpoints.Select(h => h.Weapon.Id)).ToArray();

    private static string Describe(FlightController rig) =>
        rig.Loadout is { } loadout
            ? $"guns {string.Join(",", loadout.Guns.Select(g => g.Weapon.Id))}; pylons {string.Join(",", loadout.Hardpoints.Select(h => $"{h.Index}:{h.Weapon.Id}"))}"
            : "unarmed";
}
