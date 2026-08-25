using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI;
using Godot;

namespace CSVM.Testing;

/// <summary>The suite over the mission-script host's airframe swap, callback codes 965 to 967: the
/// code CM02's own capture animation authors, driven against that mission's built world, with the
/// player's rig coming off the session's own roster so what is swapped is the flown aircraft.</summary>
internal static class AirframeSwapSuites
{
    // CM02's story position, and the airframe the swap is driven FROM. A Bloodhawk is the campaign's
    // own starting aircraft and shares none of the Balmoral's fit, so every reading below separates
    // the two airframes rather than reading the same numbers twice.
    private const int Cm02Seq = 1;
    private const string StartPlane = "player_bhawk";

    // Where the rig is flown from, and how close the replacement has to land to it. The tolerances
    // are a swap's, not a simulation's: the aircraft is rebuilt at the pose the outgoing one held,
    // so the two readings differ only by the spawn placement's own rounding.
    private const float PoseTolerance = 1f;
    private const float SpeedTolerance = 2f;

    /// <summary>Drives the swap CM02 authors against CM02's own built world: the code comes out of
    /// the mission's compiled definitions, the rig off the session's roster, and the replacement is
    /// read for the named airframe's own stock fit, hardpoint table and armour rather than the
    /// airframe it replaced.</summary>
    internal static void AirframeSwap(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        var mission = MissionOf(CampaignSequence.Load(ctx.ZrdrPath), Cm02Seq)
            ?? throw new SuiteSkippedException($"cm_sequence carries no story position {Cm02Seq}");
        string chapter = mission.ChapterFolder.ToUpperInvariant();
        string folder = mission.MissionFolder.ToUpperInvariant();
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, chapter), $"{chapter} textures");
        ctx.RequireData(SessionPaths.MissionZrdr(ctx.DataRoot, chapter, folder), $"{chapter}/{folder} zrdr");

        var report = new StringBuilder();
        report.AppendLine($"seq {Cm02Seq} -> {chapter}/{folder}");
        CheckTable(ctx, report);
        ctx.CutsceneRoots = true;
        try
        {
            ctx.WithWorld(chapter, collision: false, folder,
                world => Drive(ctx, world, chapter, folder, report));
        }
        finally
        {
            ctx.CutsceneRoots = false;
        }

        ctx.WriteArtifact($"test-airframe-swap-{chapter}-{folder}.txt", report.ToString());
        ctx.Note($"drove the swap {chapter}/{folder} authors against that mission's own world");
    }

    // The decoded table against the shipped stat data: each code's def/node pair has to be the pair
    // the airframe itself carries, or the swap would build one airframe and fit another's weapons.
    private static void CheckTable(TestContext ctx, StringBuilder report)
    {
        var seen = new List<int>();
        foreach (var entry in AirframeSwapCodes.Table)
        {
            var stats = PlaneStats.Load(ctx.ZrdrPath, entry.PlaneNode);
            report.AppendLine($"code {entry.Code}: '{entry.PlaneNode}' def '{stats.DefName}' " +
                $"(decoded '{entry.Def}')");
            ctx.Check(string.Equals(stats.DefName, entry.Def, StringComparison.OrdinalIgnoreCase),
                $"code {entry.Code}'s '{entry.PlaneNode}' is the shipped def '{entry.Def}' the decode names");
            ctx.Check(!seen.Contains(entry.Code), $"and code {entry.Code} names exactly one airframe");
            seen.Add(entry.Code);
        }

        ctx.Same(3, seen.Count, $"the host answers three swap codes, one per airframe the data names");
        ctx.Check(AirframeSwapCodes.For(11) == null,
            $"and a cutscene code that is not a swap names no airframe, so the two vocabularies stay apart");
    }

    private static void Drive(TestContext ctx, TestWorld world, string chapter, string folder,
        StringBuilder report)
    {
        var authored = SwapCallsIn(world.Session.Program);
        foreach (var (anim, code) in authored)
        {
            report.AppendLine($"'{anim}' authors callback {code} -> " +
                $"'{AirframeSwapCodes.For(code)!.Value.PlaneNode}'");
        }

        ctx.Check(authored.Count > 0,
            $"{chapter}/{folder} authors a swap callback of its own, so the mission needs one");
        var wanted = AirframeSwapCodes.For(authored[0].Code)!.Value;
        ctx.Check(string.Equals(wanted.PlaneNode, "player_balmoral", StringComparison.Ordinal),
            $"and the airframe it names is the Balmoral the mission's capture hands the player");

        var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, chapter));
        var pool = new ProjectilePool(textures, null, null);
        ctx.Host.AddChild(pool);
        var pane = new SubViewport();
        ctx.Host.AddChild(pane);
        var rig = new PlayerRig { Index = 0, Camera = ctx.Camera, HudParent = pane, Viewport = pane };
        var rigs = new[] { rig };
        FlightRoster? roster = null;
        var cutscene = new CutsceneController();
        ctx.Host.AddChild(cutscene);
        try
        {
            roster = BuildRoster(ctx, world, chapter, textures, pool, rigs);
            roster.BuildPlayers(rigs);
            var before = rig.Controller ?? throw new InvalidOperationException("no rig was built");
            RunSwap(ctx, world, roster, rig, before, cutscene, wanted, authored[0].Anim, pool, report);
        }
        finally
        {
            if (rig.Controller is { } live)
            {
                roster?.ClearMembership();
                live.Free();
            }

            cutscene.Free();
            pane.Free();
            pool.Free();
            textures.Dispose();
        }
    }

    // The swap itself, driven through the host the runtime dispatches a CALLBACK to, then read on
    // both sides: what the aircraft was, what it became, and what carried across.
    private static void RunSwap(TestContext ctx, TestWorld world, FlightRoster roster, PlayerRig rig,
        FlightController before, CutsceneController cutscene, AirframeSwapCode wanted, string anim,
        ProjectilePool pool, StringBuilder report)
    {
        cutscene.BindWorld(world.Runtime);
        cutscene.BindRigs(rigs: new[] { rig }, aiPlanes: Array.Empty<FlightController>);
        cutscene.HostDefinitions(new[] { anim });
        cutscene.SwapAirframe = node =>
        {
            roster.SwapPlayerAirframe(rig, node);
            return true;
        };

        var wasPos = before.WorldPosition;
        var wasNose = before.NoseDirection;
        float wasSpeed = before.WorldVelocity.Length();
        report.AppendLine(Describe("before", before));
        ctx.Check(before.Loadout != null && before.Damage != null,
            $"the flown aircraft arrives with a bound fit and a damage ledger to swap out of");
        string wasDef = before.Loadout!.Def.Def;
        ctx.Check(!string.Equals(wasDef, wanted.Def, StringComparison.OrdinalIgnoreCase),
            $"and it is not already '{wanted.Def}', so the swap has something to change");

        ctx.Check(cutscene.Host(wanted.Code, anim),
            $"the mission-script host answers callback {wanted.Code} rather than declining it");
        var after = rig.Controller ?? throw new InvalidOperationException("the swap built no aircraft");
        report.AppendLine(Describe("after", after));

        ctx.Check(!ReferenceEquals(before, after),
            $"the swap replaces the player's aircraft rather than editing the one that was flying");
        ctx.Check(!GodotObject.IsInstanceValid(before) || !before.IsInsideTree(),
            $"and the aircraft it replaced is out of the world, not left flying beside it");

        CheckAirframe(ctx, ctx.ZrdrPath, after, wanted, wasDef, report);
        ctx.Check(after.WorldPosition.DistanceTo(wasPos) < PoseTolerance,
            $"the replacement starts where the aircraft it replaced was flying");
        ctx.Check(after.NoseDirection.Dot(wasNose) > 0.99f,
            $"pointed the way it was pointed");
        ctx.Check(Mathf.Abs(after.WorldVelocity.Length() - wasSpeed) < SpeedTolerance,
            $"and at the speed it was flying, so the swap is not a respawn");
        ctx.Same(before.PlayerIndex, after.PlayerIndex,
            $"the pilot keeps their shooter id, which is what a round already in the air scores to");
        ctx.Same(1, pool.NearMissTargets.Count,
            $"and exactly one near-miss registration survives, so nothing of the old rig is left in the pool");

        // The cutscene flags codes 965 to 967 set are the player vehicle's own +0x91d/+0x91e pair
        // and the chrome off: the state code 11 and code 2 assert between them
        // (docs/formats/anim-definitions/cutscenes.md).
        ctx.Check(cutscene.OutOfFlight && cutscene.Presenting,
            $"the swap sets the cutscene flags, so the player is out of flight with the chrome down while the capture plays");
        ctx.Check(after.Held && after.Inert,
            $"and that state lands on the aircraft the swap built, not on the one it replaced");
    }

    // What the replacement is: the named airframe's own def, its own stock hardpoint table, and its
    // own armour pools. The comparison is against the airframe LEFT as much as against the data,
    // because a swap that changed the model and kept the previous fit would read right on its own.
    private static void CheckAirframe(TestContext ctx, string zrdrPath, FlightController after,
        AirframeSwapCode wanted, string wasDef, StringBuilder report)
    {
        var stats = PlaneStats.Load(zrdrPath, wanted.PlaneNode);
        var stock = PlaneDamage.For(stats);
        ctx.Check(after.Loadout is { } fit
                  && string.Equals(fit.Def.Def, wanted.Def, StringComparison.OrdinalIgnoreCase),
            $"the player is flying '{wanted.Def}', the def callback {wanted.Code} names");
        var loadout = after.Loadout!;
        int hardpoints = loadout.Hardpoints.Count;
        var wasStock = StockLoadouts.Load().For(wasDef);
        report.AppendLine($"hardpoints: {hardpoints} on '{wanted.Def}', " +
            $"{(wasStock?.Hardpoints?.Count ?? 0)} authored on '{wasDef}'");
        ctx.Check(hardpoints > 0, $"with '{wanted.Def}'s own hardpoint table bound to its pylons");
        ctx.Check(hardpoints != (wasStock?.Hardpoints?.Count ?? 0),
            $"and that table is the new airframe's, not the '{wasDef}' table the aircraft carried in");
        int full = 0;
        foreach (var hardpoint in loadout.Hardpoints)
        {
            full += hardpoint.Ammo == hardpoint.Capacity ? 1 : 0;
        }

        ctx.Same(hardpoints, full,
            $"every pylon arrives at its own capacity: the ordnance is the airframe's, so nothing of the previous count carries over");

        ctx.Check(after.Damage != null, $"the replacement carries a damage ledger");
        var damage = after.Damage!;
        report.AppendLine($"armour: whole {damage.WholeArmorMax:0.#}/{damage.WholeHealthMax:0.#} " +
            $"over {damage.Parts.Count} zone(s); the airframe's own is " +
            $"{stock.WholeArmorMax:0.#}/{stock.WholeHealthMax:0.#} over {stock.Parts.Count}");
        ctx.Check(Mathf.IsEqualApprox(damage.WholeArmorMax, stock.WholeArmorMax)
                  && Mathf.IsEqualApprox(damage.WholeHealthMax, stock.WholeHealthMax),
            $"whose armour and health pools are '{wanted.PlaneNode}'s own, off its own stat rows");
        ctx.Same(stock.Parts.Count, damage.Parts.Count,
            $"over that airframe's own damage zones, which is the ladder the capture's aircraft has to fly on");
        ctx.Check(Mathf.IsEqualApprox(damage.WorstFraction, 1f),
            $"and it starts undamaged, the original rebuilding the vehicle from the record rather than carrying the old hull across");
    }

    private static string Describe(string when, FlightController controller) =>
        $"{when}: def='{controller.Loadout?.Def.Def ?? "-"}' " +
        $"hardpoints={controller.Loadout?.Hardpoints.Count ?? 0} " +
        $"guns={controller.Loadout?.Guns.Count ?? 0} " +
        $"armour={controller.Damage?.WholeArmorMax ?? 0f:0.#} " +
        $"zones={controller.Damage?.Parts.Count ?? 0} " +
        $"pos={controller.WorldPosition} speed={controller.WorldVelocity.Length():0.#}";

    // Every definition in the mission's compiled program that raises one of the swap codes, with the
    // code it raises. Read out of the shipped data rather than named here: what the mission asks for
    // is the mission's own business, and this suite only drives it.
    private static List<(string Anim, int Code)> SwapCallsIn(AnimProgram program)
    {
        var found = new List<(string, int)>();
        foreach (var def in program.Defs)
        {
            if (def.AnimName is not { } anim)
            {
                continue;
            }

            foreach (var sequence in def.Sequences)
            {
                foreach (var ev in sequence.Events)
                {
                    if (!string.Equals(ev.Kind, "Callback", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int code = (int)(ev.Data.Num("value") ?? -1f);
                    if (AirframeSwapCodes.For(code) != null)
                    {
                        found.Add((anim, code));
                    }
                }
            }
        }

        return found;
    }

    private static FlightRoster BuildRoster(TestContext ctx, TestWorld world, string chapter,
        TextureArchive textures, ProjectilePool pool, IReadOnlyList<PlayerRig> rigs)
    {
        var spec = SessionSpec.Parse(new[] { $"--plane={StartPlane}" });
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var resources = new AircraftAssemblyResources
        {
            PlanesGamez = planesGamez,
            StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
            AiStatsFor = (plane, aiDef) => PlaneStats.LoadForAi(ctx.ZrdrPath, plane, aiDef),
            CamParamsFor = _ => new CamParams(),
            PaintRng = new RandomNumberGenerator(),
            ZrdrPath = ctx.ZrdrPath,
            StockLoadouts = StockLoadouts.Load(),
            WeaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null),
            WeaponMessages = Messages.Load(ctx.MessagesPath),
            Textures = textures,
            Shakes = ShakeDefs.Load(ctx.ZrdrPath),
        };
        return new FlightRoster(FlightRosterPolicy.From(spec),
            new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof")),
            new WorldEffectsFactory(spec, ctx.Host, () => Vector3.Zero), ctx.Host, resources,
            new FlightWorldBindings
            {
                Projectiles = pool,
                Gamez = planesGamez,
                ChapterZrdrPath = SessionPaths.ChapterZrdr(ctx.DataRoot, chapter),
            },
            new HumanRosterBindings
            {
                RigCount = rigs.Count,
                Rigs = rigs,
                PauseState = new PauseState(),
                MenuInputFor = _ => new MenuInput(),
                ExitSession = () => { },
            }, new SwapFlightStarts());
    }

    private static CampaignMission? MissionOf(IReadOnlyList<CampaignMission> missions, int seq)
    {
        foreach (var mission in missions)
        {
            if (mission.Seq == seq)
            {
                return mission;
            }
        }

        return null;
    }

    // One start, high enough over the mission's own terrain that the aircraft is flying rather than
    // resolving a ground contact on the frame the swap rebuilds it.
    private sealed class SwapFlightStarts : IFlightStarts
    {
        public IReadOnlyList<FlightStart> ChooseStarts(IReadOnlyList<SpawnPoint>? spawns,
            string missionZrdrPath, int spawnBase, int playerCount)
        {
            var starts = new FlightStart[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                var position = new Vector3(i * 60f, 1200f, 0f);
                starts[i] = new FlightStart(position, position + Vector3.Forward, 0.5f, 90f);
            }

            return starts;
        }
    }
}
