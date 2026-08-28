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

/// <summary>CM02's capture as the objectives see it: the player takes the last live bomber of
/// group 5 through the 967 wing-walk swap, so the group must go on reading one live member (the
/// player now flies it) and the <c>DEDG [5, 0]</c> objective that naps the instant loss must stay
/// incomplete. Driven over C3/M05's own roster through the session's <see cref="FlightRoster"/>
/// and its own objective graph, with the swap run the way the mission-script host runs it. The
/// same swap re-points the wingman escorting the player onto the rebuilt rig and keeps the
/// director's death hook on it, so both are read here as well.</summary>
internal static class CaptureGroupSuites
{
    private const string Chapter = "C3";
    private const string Mission = "M05";

    // The bomber the capture animation is rooted on, and the group its three blocks author.
    private const string CaptureRoot = "britbalmoral_1";
    private const int BomberGroup = 5;
    private const int CaptureCode = 967;

    private const float StepDt = 1f / 60f;

    // Long enough past the swap to show the group holding at one, and short of the 20 s nap the
    // completed DEDG would raise the instant loss on.
    private const float HoldSeconds = 5f;

    // The player's death is a crash already on the ground, so the lost ending is due at once.
    private const float DeathWindowSeconds = 2f;

    internal static void CaptureGroup(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, Chapter, Mission);
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, Chapter);
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, Chapter);
        ctx.RequireData(missionZrdr, $"{Chapter}/{Mission} zrdr");
        ctx.RequireData(chapterZrdr, $"{Chapter} zrdr");
        ctx.RequireData(texturesPath, $"{Chapter} textures");

        CampaignMission? found = null;
        foreach (var m in CampaignSequence.Load(ctx.ZrdrPath))
        {
            if (m.ChapterFolder.Equals(Chapter, StringComparison.OrdinalIgnoreCase)
                && m.MissionFolder.Equals(Mission, StringComparison.OrdinalIgnoreCase))
            {
                found = m;
            }
        }
        if (found is not { } mission)
        {
            throw new SuiteSkippedException($"{Chapter}/{Mission} is not in cm_sequence");
        }

        var script = ObjectiveScript.Load(missionZrdr);
        var report = new StringBuilder();
        var chain = CheckAuthored(ctx, script, report);
        var skills = AiSkills.Load(ctx.ZrdrPath);
        var director = CampaignDirector.Create(script, mission, CampaignProfileDef.NewProfile("Zachary"), null);
        ctx.WithWorld(Chapter, collision: false, Mission, world =>
            Drive(ctx, world, director, chain, skills, missionZrdr, chapterZrdr, texturesPath, report));

        ctx.WriteArtifact($"test-campaign-capture-group-{Chapter}-{Mission}.txt", report.ToString());
        ctx.Note($"{Chapter}/{Mission}: the captured bomber's group reads one with the player flying it, so the instant loss stays asleep");
    }

    // The chain as the mission authors it: a DEDG over group 5 at most one, a DEDG at zero, and
    // the nap the latter puts on an INSTANTLOSS objective. Read off the script, not restated.
    private static Chain CheckAuthored(TestContext ctx, ObjectiveScript script, StringBuilder report)
    {
        int shootTwo = 0, wipedOut = 0, instantLoss = 0;
        float napSeconds = 0f;
        foreach (var def in script.Objectives)
        {
            // Two objectives author the at-most-one read: the dormant PRIMARY the briefing names
            // and the awake fan-out (OBJECTIVE63), which is the one a fresh graph can complete.
            if (def.Dedg is { Group: BomberGroup, Max: 1 } && !def.BeginDormant)
            {
                shootTwo = def.Number;
            }
            else if (def.Dedg is { Group: BomberGroup, Max: 0 })
            {
                wipedOut = def.Number;
                if (def.NapWhenComplete is { } nap)
                {
                    instantLoss = nap.Target;
                    napSeconds = nap.Seconds;
                }
            }
        }

        report.AppendLine($"authored: shoot-two OBJECTIVE{shootTwo}, wiped-out OBJECTIVE{wipedOut} naps OBJECTIVE{instantLoss} {napSeconds:0} s");
        ctx.Check(shootTwo > 0 && wipedOut > 0,
            $"{Chapter}/{Mission} authors a DEDG over group {BomberGroup} at most one (OBJECTIVE{shootTwo}) and another at zero (OBJECTIVE{wipedOut})");
        var lossDef = instantLoss >= 1 && instantLoss <= script.Objectives.Count ? script.Objectives[instantLoss - 1] : null;
        ctx.Check(lossDef is { InstantLoss: true },
            $"and the wiped-out one naps an INSTANTLOSS objective (OBJECTIVE{instantLoss}), which is the loss the swap has to keep asleep");
        ctx.Check(napSeconds > HoldSeconds,
            $"whose nap ({napSeconds:0} s) outlasts the {HoldSeconds:0} s this suite holds the swapped state for");
        return new Chain(shootTwo, wipedOut, instantLoss);
    }

    private static void Drive(TestContext ctx, TestWorld world, CampaignDirector director, Chain chain,
        AiSkills skills, string missionZrdr, string chapterZrdr, string texturesPath, StringBuilder report)
    {
        var textures = new TextureArchive(texturesPath);
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var pool = new ProjectilePool(textures, null, null);
        ctx.Host.AddChild(pool);
        var pane = new SubViewport();
        ctx.Host.AddChild(pane);
        var rig = new PlayerRig { Index = 0, Camera = ctx.Camera, HudParent = pane, Viewport = pane };
        var rigs = new[] { rig };
        FlightRoster? roster = null;
        try
        {
            roster = BuildRoster(ctx, planesGamez, textures, pool, rigs);
            roster.BuildPlayers(rigs);
            var before = rig.Controller ?? throw new InvalidOperationException("no rig was built");
            var flightRoster = roster;

            string what = director.BuildRoster(new CampaignDirector.RosterInputs
            {
                ChapterZrdrPath = chapterZrdr,
                MissionZrdrPath = missionZrdr,
                ZrdrPath = ctx.ZrdrPath,
                MinAiActiveDist = skills.MinAiActiveDist,
                PlayerAirframe = roster.FlyingAirframeOf(0),
                Player = () => rig.Controller,
                NetTrailers = new NetTrailerTargets(
                    () => rig.Controller?.WorldPosition ?? Vector3.Zero,
                    name => world.Runtime.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null),
                FindNodes = name => world.Runtime.FindNodes(name),
                Spawn = (plan, pos, look, pilot) => flightRoster.SpawnAi(
                    CampaignRosterPlan.SpawnFor(plan, pos, look, pilot)),
                Rng = new Random(1),
            });
            report.AppendLine($"build summary suffix: '{what}'");

            director.Attach(new CampaignDirector.WorldInputs
            {
                Runtime = world.Runtime,
                Sounds = world.Runtime.Sounds,
                Projectiles = pool,
                ListenerPosition = () => rig.Controller?.WorldPosition ?? Vector3.Zero,
                PlayerAircraft = () => rig.Controller,
                Aircraft = () => flightRoster.AiAircraft,
                Rng = new Random(1),
            });

            var bombers = Bombers(director.Roster, report);
            ctx.Check(bombers.Count >= 3 && bombers.ContainsKey(CaptureRoot),
                $"the roster carries group {BomberGroup}'s bombers, '{CaptureRoot}' among them: [{string.Join(",", bombers.Keys)}]");
            ctx.Check(director.Roster.TryGetValue(AirframeHandover.WingmanName, out var wingman)
                      && wingman.Pilot?.Escort is { } escort && ReferenceEquals(escort.Leader, before),
                $"'{AirframeHandover.WingmanName}' escorts the player's aircraft before the swap");
            if (bombers.Count < 3 || wingman == null)
            {
                return;
            }

            ShootTwo(ctx, director, chain, bombers, report);
            var after = Capture(ctx, director, roster, rig, before, wingman, report);
            if (after == null)
            {
                return;
            }

            Hold(ctx, director, chain, rig, wingman, report);
            Die(ctx, director, rig, report);
        }
        finally
        {
            var live = rig.Controller;
            var members = new List<FlightController>(roster?.AiAircraft ?? Array.Empty<FlightController>());
            roster?.ClearMembership();
            live?.Free();
            foreach (var ai in members)
            {
                ai.Free();
            }

            pane.Free();
            pool.Free();
            textures.Dispose();
        }
    }

    // Group 5's members, read off the group the roster phase stamped on each rig.
    private static Dictionary<string, FlightController> Bombers(
        IReadOnlyDictionary<string, FlightController> roster, StringBuilder report)
    {
        var bombers = new Dictionary<string, FlightController>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, rig) in roster)
        {
            if (rig.Group == BomberGroup)
            {
                bombers[name] = rig;
            }
        }

        report.AppendLine($"group {BomberGroup}: [{string.Join(",", bombers.Keys)}]");
        return bombers;
    }

    // Two of the three down through the debug kill, which the DEDG walk sees exactly as a shot
    // down: the shoot-two objective completes and the wiped-out one does not.
    private static void ShootTwo(TestContext ctx, CampaignDirector director, Chain chain,
        Dictionary<string, FlightController> bombers, StringBuilder report)
    {
        foreach (var (name, bomber) in bombers)
        {
            if (!name.Equals(CaptureRoot, StringComparison.OrdinalIgnoreCase))
            {
                bomber.DebugForceCrash();
            }
        }

        for (int i = 0; i < 30; i++)
        {
            director.Step(StepDt);
        }

        int? live = director.GroupLiveCount(BomberGroup);
        report.AppendLine($"two down: group {BomberGroup} counts {live?.ToString() ?? "-"}, " +
            $"OBJECTIVE{chain.ShootTwo} complete={director.Graph?.CompletedOf(chain.ShootTwo)}, " +
            $"OBJECTIVE{chain.WipedOut} complete={director.Graph?.CompletedOf(chain.WipedOut)}");
        ctx.Same(1, live ?? -1, $"with two bombers down, group {BomberGroup} counts the one left");
        ctx.Check(director.Graph?.CompletedOf(chain.ShootTwo) == true,
            $"and OBJECTIVE{chain.ShootTwo} (at most one) completes");
        ctx.Check(director.Graph?.CompletedOf(chain.WipedOut) == false,
            $"while OBJECTIVE{chain.WipedOut} (wiped out) does not");
    }

    // The swap itself, the whole order the host raises for 967, rooted on the last bomber.
    private static FlightController? Capture(TestContext ctx, CampaignDirector director, FlightRoster roster,
        PlayerRig rig, FlightController before, FlightController wingman, StringBuilder report)
    {
        var order = new AirframeSwapOrder(AirframeSwapCodes.For(CaptureCode)!.Value, CaptureRoot);
        var result = roster.RunSwap(rig, order, handsOver: true);
        var after = rig.Controller;
        ctx.Check(result.Swapped && after != null && !ReferenceEquals(after, before),
            $"the capture swap rebuilds the player's rig");
        if (after == null)
        {
            return null;
        }

        ctx.Check(result.Hidden != null && result.Hidden.Inert,
            $"and hides '{CaptureRoot}', the aircraft the player took");
        int? live = director.GroupLiveCount(BomberGroup);
        report.AppendLine($"swapped: player group={after.Group?.ToString() ?? "-"}, group {BomberGroup} counts {live?.ToString() ?? "-"}, " +
            $"wingman leader={(wingman.Pilot?.Escort?.Leader is { } l ? (ReferenceEquals(l, after) ? "new rig" : ReferenceEquals(l, before) ? "OLD rig" : "other") : "none")}");
        ctx.Same(BomberGroup, after.Group ?? -1,
            $"the rebuilt rig carries group {BomberGroup}, the captured bomber's own (the +0x388 copy)");
        ctx.Same(1, live ?? -1,
            $"so group {BomberGroup} still counts one: the hidden bomber is gone and the player stands in for it");
        ctx.Check(wingman.Pilot?.Escort is { } escort && ReferenceEquals(escort.Leader, after),
            $"'{AirframeHandover.WingmanName}' now escorts the rebuilt rig rather than the freed one");
        return after;
    }

    // Five seconds of the swapped state: the wingman flies its escort on the new leader with no
    // exception, the group holds at one, and the wiped-out objective stays incomplete, so the
    // instant loss it would nap awake never wakes.
    private static void Hold(TestContext ctx, CampaignDirector director, Chain chain, PlayerRig rig,
        FlightController wingman, StringBuilder report)
    {
        Exception? thrown = null;
        int steps = (int)(HoldSeconds / StepDt);
        for (int i = 0; i < steps && thrown == null; i++)
        {
            try
            {
                rig.Controller?.SimStep(StepDt);
                wingman.SimStep(StepDt);
                director.Step(StepDt);
            }
            catch (Exception e)
            {
                thrown = e;
            }
        }

        int? live = director.GroupLiveCount(BomberGroup);
        report.AppendLine($"held {HoldSeconds:0} s: exception={(thrown == null ? "none" : thrown.GetType().Name)}, " +
            $"group {BomberGroup} counts {live?.ToString() ?? "-"}, OBJECTIVE{chain.WipedOut} complete={director.Graph?.CompletedOf(chain.WipedOut)}, " +
            $"OBJECTIVE{chain.InstantLoss} state={director.Graph?.StateOf(chain.InstantLoss)}, ended={director.Result != null}");
        ctx.Check(thrown == null,
            $"the wingman and the director step {HoldSeconds:0} s past the swap with no exception ({thrown?.GetType().Name ?? "none"}: {thrown?.Message ?? ""})");
        ctx.Check(wingman.InPlay,
            $"'{AirframeHandover.WingmanName}' is in play, flying the aeroplane the player left");
        ctx.Same(1, live ?? -1, $"group {BomberGroup} still counts one after {HoldSeconds:0} s");
        ctx.Check(director.Graph?.CompletedOf(chain.WipedOut) == false,
            $"OBJECTIVE{chain.WipedOut} stays incomplete, so the {HoldSeconds:0} s window raises no instant loss");
        ctx.Check(director.Result == null, $"and the mission is still running");
    }

    // The player's death in the new rig still reaches the director: the hook moved with the swap.
    private static void Die(TestContext ctx, CampaignDirector director, PlayerRig rig, StringBuilder report)
    {
        rig.Controller?.DebugForceCrash();
        float elapsed = 0f, endedAt = -1f;
        while (elapsed < DeathWindowSeconds && endedAt < 0f)
        {
            director.Step(StepDt);
            elapsed += StepDt;
            if (director.Result != null)
            {
                endedAt = elapsed;
            }
        }

        report.AppendLine($"death: ended at {endedAt:0.00} s outcome={director.Result?.Outcome.ToString() ?? "-"}");
        ctx.Check(director.Result is { Outcome: MissionOutcome.Lost },
            $"losing the rebuilt rig ends the mission lost, so the death hook followed the swap: {director.Result?.Outcome.ToString() ?? "still running"}");
    }

    private static FlightRoster BuildRoster(TestContext ctx, GameZ planesGamez, TextureArchive textures,
        ProjectilePool pool, IReadOnlyList<PlayerRig> rigs)
    {
        var spec = SessionSpec.Parse(new[] { "--plane=player_bhawk" });
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
                ChapterZrdrPath = SessionPaths.ChapterZrdr(ctx.DataRoot, Chapter),
            },
            new HumanRosterBindings
            {
                RigCount = rigs.Count,
                Rigs = rigs,
                PauseState = new PauseState(),
                MenuInputFor = _ => new MenuInput(),
                ExitSession = () => { },
            }, new HighStarts());
    }

    private readonly record struct Chain(int ShootTwo, int WipedOut, int InstantLoss);

    // One start, high over the mission's terrain, so the rig is flying when the swap rebuilds it.
    private sealed class HighStarts : IFlightStarts
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
