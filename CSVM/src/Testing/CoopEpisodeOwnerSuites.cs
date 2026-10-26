using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Extraction;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session.Campaign;
using CSVM.Session.Roster;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The episode owner with two humans flying. Over CM02's own capture, the airframe swap
/// the mission raises rebuilds the rig whose trigger started the episode, not player 1's. The
/// definition is played both ways, which is the able-to-fail control. Claimed by nobody, it swaps
/// the scripted player; claimed by the guest, it swaps the guest alone. The second suite reads the
/// same over CM07's hangar drop, which a range gate starts rather than a trigger row. The third reads
/// C4/M03's Blacke drop, which a looping <c>PLAYER_RANGE</c> gate starts.</summary>
internal static class CoopEpisodeOwnerSuites
{
    // CM02's story position, and the two airframes the humans fly in on. Different records on
    // purpose: every reading below tells the two rigs apart by what they are flying.
    private const int Cm02Seq = 1;
    private const string ScriptedPlane = "player_bhawk";
    private const string GuestPlane = "player_pfighter";
    private const string CapturePlanes = $"{ScriptedPlane},{GuestPlane}";

    private const float StepDt = 1f / 60f;

    // Long enough to outlast the wing walk's own 19.25 s motion, which is what the capture's called
    // definition raises its swap behind.
    private const float PlayBudgetS = 30f;

    // CM07's story position and the code its hangar drop raises. Neither human flies the Bloodhawk
    // that code names, so its swap is a change on either; the guest flies the reported Devastator.
    private const int Cm07Seq = 6;
    private const int HangarSwapCode = 965;
    private const string HangarPlanes = "player_fbrand,player_pfighter";

    // The two-end legs: each end's nearer and farther seat, both inside the 75 m band, and the link's seed.
    private const float HangarNearM = 10f;
    private const float HangarFarM = 40f;
    private const ulong HangarNetSeed = 0xC1020191UL;

    // Where the field waits while the drop is armed, well outside its 75 m band, and for how long.
    private const float HangarAwayM = 400f;
    private const float HangarArmS = 4f;

    // How far a staged aeroplane may sit off the marker it rides, and a held one off where it was.
    private const float StageToleranceM = 1f;

    // C4/M03's Blacke drop: the definition polling the 64 m PLAYER_RANGE gate, and the marker it polls on.
    private const string BlackeChapter = "C4";
    private const string BlackeMission = "M03";
    private const string BlackeGate = "blacke_drop";
    private const string BlackeMarker = "blacke_marker";

    private static readonly string[] HangarAirframes = HangarPlanes.Split(',');

    // How a two-end leg joins its ends: the shipped link, that link with the host's seat dropped, or none.
    private enum EndLink
    {
        Linked,
        SeatDropped,
        None,
    }

    [Suite("campaign-coop-episode-owner",
        "the episode owner over CM02's own capture with two humans flying: the mission's swap "
        + "rebuilds the rig whose trigger claimed the episode, on the record the code names, "
        + "while the other human keeps the aeroplane it was flying; the swapped pilot keeps "
        + "their own shooter id and the pool's registration under it, the other pilot's is "
        + "untouched, and the same capture claimed by nobody swaps the scripted player")]
    internal static void CampaignCoopEpisodeOwner(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireZrdrEntry(ctx.ZrdrPath, CampaignSequence.FileName);
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        var mission = MissionOf(CampaignSequence.Load(ctx.ZrdrPath), Cm02Seq)
            ?? throw new SuiteSkippedException($"cm_sequence carries no story position {Cm02Seq}");
        string chapter = mission.ChapterFolder.ToUpperInvariant();
        string folder = mission.MissionFolder.ToUpperInvariant();
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, chapter), $"{chapter} textures");
        ctx.RequireData(SessionPaths.MissionZrdr(ctx.DataRoot, chapter, folder), $"{chapter}/{folder} zrdr");

        var report = new StringBuilder();
        report.AppendLine($"seq {Cm02Seq} -> {chapter}/{folder}");
        ctx.CutsceneRoots = true;
        try
        {
            ctx.WithWorld(chapter, collision: false, folder,
                world => Drive(ctx, world, chapter, report));
        }
        finally
        {
            ctx.CutsceneRoots = false;
        }

        ctx.WriteArtifact($"test-campaign-coop-episode-owner-{chapter}-{folder}.txt", report.ToString());
        ctx.Note($"{chapter}/{folder}: the capture's swap follows the human whose trigger owns the episode");
    }

    [Suite("campaign-coop-hangar-owner",
        "the episode owner over CM07's own hangar drop with two humans flying, the drop armed by "
        + "its own caller and started by its EXECUTION_BY_RANGE gate as a flown session starts it: "
        + "the guest's rig crossing the hangar owns the episode on the host's machine, where it is "
        + "a copy in no pane, and on the guest's own; the 965 swap rebuilds that rig on the Blue "
        + "Streak's record and the film stages it on the player marker, while the scripted player "
        + "keeps the aeroplane it was flying and is not staged; CONTROL: the same crossing with the "
        + "range gate's claim unwired hands the episode, the swap and the staging to the scripted "
        + "player, the reported fault; and over two BUILT worlds joined by a 30 ms, 25 per cent "
        + "lossy loopback, with each end's field putting a different seat nearest the hangar, the "
        + "host decides the owner and the guest starts the drop on the host's word for that same "
        + "seat, P1 where the host's field puts P1 nearest and P2 where it puts P2 nearest and the "
        + "guest's own field does not; CONTROL: the same two fields with no link make the two ends' "
        + "owners differ, and the P2 crossing with the host's seat dropped on the wire leaves the "
        + "guest's episode to the scripted player, P1; and a trigger's own start that calls the drop "
        + "with P1 already in its band keeps the episode for the trigger's pilot, P2; CONTROL: the "
        + "same claim and call outside the trigger's bracket hand it to P1")]
    internal static void CampaignCoopHangarOwner(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireZrdrEntry(ctx.ZrdrPath, CampaignSequence.FileName);
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        var mission = MissionOf(CampaignSequence.Load(ctx.ZrdrPath), Cm07Seq)
            ?? throw new SuiteSkippedException($"cm_sequence carries no story position {Cm07Seq}");
        string chapter = mission.ChapterFolder.ToUpperInvariant();
        string folder = mission.MissionFolder.ToUpperInvariant();
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, chapter), $"{chapter} textures");
        ctx.RequireData(SessionPaths.MissionZrdr(ctx.DataRoot, chapter, folder), $"{chapter}/{folder} zrdr");

        var report = new StringBuilder();
        report.AppendLine($"seq {Cm07Seq} -> {chapter}/{folder}");
        ctx.CutsceneRoots = true;
        try
        {
            ctx.WithWorld(chapter, collision: false, folder, hostWorld =>
                ctx.WithWorld(chapter, collision: false, folder, guestWorld =>
                {
                    if (ReferenceEquals(hostWorld.Runtime, guestWorld.Runtime))
                    {
                        throw new SuiteSkippedException($"{chapter}/{folder} is this run's cached world, so a second build is the same one");
                    }

                    DriveHangar(ctx, hostWorld, guestWorld, chapter, report);
                }));
        }
        finally
        {
            ctx.CutsceneRoots = false;
        }

        ctx.WriteArtifact($"test-campaign-coop-hangar-owner-{chapter}-{folder}.txt", report.ToString());
        ctx.Note($"{chapter}/{folder}: the hangar drop follows the human who crossed its range gate");
    }

    [Suite("campaign-coop-blacke-owner",
        "the episode owner over C4/M03's Blacke drop, which a looping 64 m PLAYER_RANGE gate on its "
        + "marker starts, over two BUILT worlds joined by a 30 ms, 25 per cent lossy loopback with "
        + "both humans inside the gate and each end's field putting a different seat nearest: the "
        + "host's field puts the guest's seat, P2, nearest, so the host owns the drop for P2 and the "
        + "guest starts it on the host's pass for P2, a seat its own field does not put nearest; "
        + "each end claims the drop once, for that owner, while the gate's own loop keeps reading "
        + "the open verdict, and the owner holds through the film; CONTROL: the same crossing with the "
        + "host's seat dropped on the wire leaves the guest's episode to the scripted player, P1; "
        + "and over a looping code-raising gate built from definitions, which the shipped drop's "
        + "self-stopping gate cannot show, a host that closes and reopens the gate for the other "
        + "seat between two guest polls is matched by the guest claiming that reopening for the "
        + "same seat; CONTROL: with the host's openings uncounted the guest claims the first "
        + "opening alone")]
    internal static void CampaignCoopBlackeOwner(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(SessionPaths.MissionZrdr(ctx.DataRoot, BlackeChapter, BlackeMission), $"{BlackeChapter}/{BlackeMission} zrdr");
        var report = new StringBuilder();
        ctx.CutsceneRoots = true;
        try
        {
            foreach (var leg in new[]
            {
                new EndsLeg("two ends, linked, P2 nearest on the host", EndLink.Linked, 1, Loops: true),
                new EndsLeg("CONTROL: two ends, the host's seat dropped on the wire", EndLink.SeatDropped, 1),
            })
            {
                // Fresh worlds per leg. The gate's definition loops through the film until `bdplayer`
                // stops and invalidates it, and nothing in the mission re-arms it.
                ctx.WithWorld(BlackeChapter, collision: false, BlackeMission, hostWorld =>
                    ctx.WithWorld(BlackeChapter, collision: false, BlackeMission, guestWorld =>
                    {
                        if (ReferenceEquals(hostWorld.Runtime, guestWorld.Runtime))
                        {
                            throw new SuiteSkippedException($"{BlackeChapter}/{BlackeMission} is this run's cached world, so a second build is the same one");
                        }

                        var hostMarker = First(hostWorld.Runtime.FindNodes(BlackeMarker));
                        var guestMarker = First(guestWorld.Runtime.FindNodes(BlackeMarker));
                        ctx.Check(hostMarker != null && guestMarker != null, $"both worlds build the drop's '{BlackeMarker}'");
                        if (hostMarker == null || guestMarker == null)
                        {
                            return;
                        }

                        // Where the gate measures from: the marker's own origin.
                        var ends = new[]
                        {
                            (hostWorld, AnimRuntime.WorldTransform(hostMarker, out _).Origin),
                            (guestWorld, AnimRuntime.WorldTransform(guestMarker, out _).Origin),
                        };
                        CrossOnTwoEnds(ctx, ends, BlackeGate, null, leg, report);
                    }));
            }
        }
        finally
        {
            ctx.CutsceneRoots = false;
        }

        foreach (bool counted in new[] { true, false })
        {
            ReopenBetweenPolls(ctx, counted, report);
        }

        ctx.WriteArtifact($"test-campaign-coop-blacke-owner-{BlackeChapter}-{BlackeMission}.txt", report.ToString());
        ctx.Note($"{BlackeChapter}/{BlackeMission}: the Blacke drop follows the human the host's gate pass named");
    }

    internal static FlightController StageAi(FlightRoster roster, string name, string planeNode, Vector3 at)
    {
        var aim = at - Vector3.Forward;
        return roster.SpawnAi(new AiSpawn(planeNode, at, aim, AiPilot.HoldingCourse(at, aim),
            Team: AimAssist.PlayerTeam + 1, Inert: false, ShippedSkins: true, NodeName: name));
    }

    // With seats, every seat not flown here is built the way a session builds a guest's copy. Two
    // rosters in one process each need their own worldRoot. Godot renames a sibling whose name is
    // taken, and a roster finds its spawns by name.
    internal static FlightRoster BuildRoster(TestContext ctx, string chapter, TextureArchive textures,
        ProjectilePool pool, IReadOnlyList<PlayerRig> rigs, IReadOnlyList<Net.NetSeat>? seats = null,
        Node3D? worldRoot = null, string planes = CapturePlanes)
    {
        var spec = SessionSpec.Parse(new[] { $"--plane={planes}" });
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
            WeaponMessages = ctx.Data.Messages,
            Textures = textures,
            Shakes = ShakeDefs.Load(ctx.ZrdrPath),
        };
        return new FlightRoster(FlightRosterPolicy.From(spec),
            new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof")),
            new WorldEffectsFactory(spec, ctx.Host, () => Vector3.Zero), worldRoot ?? ctx.Host, resources,
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
                NetSeats = seats ?? Array.Empty<Net.NetSeat>(),
                PauseState = new PauseState(),
            }, new CoopStarts());
    }

    private static void Drive(TestContext ctx, TestWorld world, string chapter, StringBuilder report)
    {
        var call = SwapCallIn(world.Session.Program)
            ?? throw new SuiteSkippedException($"{chapter} carries no airframe-swap callback to own");
        var wanted = AirframeSwapCodes.For(call.Code)!.Value;
        report.AppendLine($"'{call.Root}-{call.Anim}' authors callback {call.Code} -> '{wanted.PlaneNode}'");
        ctx.Check(!string.Equals(wanted.PlaneNode, ScriptedPlane, StringComparison.OrdinalIgnoreCase)
                  && !string.Equals(wanted.PlaneNode, GuestPlane, StringComparison.OrdinalIgnoreCase),
            $"the mission's swap names '{wanted.PlaneNode}', which neither human is already flying");

        // A real enemy roster spawn under the capture definition's own root, which is the only
        // thing the swap reads it by.
        void StageCapture(FlightRoster roster, IReadOnlyList<FlightController> before) =>
            StageAi(roster, call.Root, wanted.PlaneNode, before[0].WorldPosition + (before[0].NoseDirection * 300f));
        WithTwoHumans(ctx, chapter, CapturePlanes, StageCapture,
            staged => Own(ctx, world, staged, wanted, call, guest: false, report));
        WithTwoHumans(ctx, chapter, CapturePlanes, StageCapture,
            staged => Own(ctx, world, staged, wanted, call, guest: true, report));
    }

    private static void DriveHangar(TestContext ctx, TestWorld world, TestWorld guestWorld, string chapter,
        StringBuilder report)
    {
        var call = SwapCallIn(world.Session.Program);
        ctx.Check(call is { Code: HangarSwapCode }, $"the mission's compiled program authors callback {HangarSwapCode}");
        if (call is not { Code: HangarSwapCode } drop)
        {
            return;
        }

        var wanted = AirframeSwapCodes.For(drop.Code)!.Value;
        var dropDef = world.Session.Program.ByAnimName(drop.Anim);
        var anchor = DropAnchor(world, drop.Anim);
        var guestAnchor = DropAnchor(guestWorld, drop.Anim);
        string? caller = AirframeSwapSuites.CallerOf(world, drop.Anim);
        report.AppendLine($"'{drop.Root}-{drop.Anim}' authors callback {drop.Code} -> '{wanted.PlaneNode}', armed by '{caller ?? "-"}'");
        ctx.Check(dropDef.Count > 0 && dropDef[0].ByRange && anchor != null && guestAnchor != null && caller != null,
            $"'{drop.Anim}' is range-gated on an anchor of its own in both worlds, and a definition of the mission calls it");
        if (anchor == null || guestAnchor == null || caller == null)
        {
            return;
        }

        ctx.Check(world.Runtime.RangeGates.HostDecides(dropDef[0]),
            $"'{drop.Anim}' raises cutscene codes, so its range gate is the host's to decide in network co-op");

        // The wired legs first, on the world as built; the control replays the drop after them.
        foreach (var (name, wired, onGuest) in new[]
        {
            ("the host's machine", true, false),
            ("the guest's machine", true, true),
            ("CONTROL: the range claim unwired, the host's machine", false, false),
        })
        {
            WithTwoHumans(ctx, chapter, HangarPlanes, null,
                staged => CrossTheHangar(ctx, world, staged, drop, wanted, caller, anchor,
                    new HangarLeg(name, wired, onGuest), report));
        }

        // Two machines whose fields disagree on the nearest seat: the host's word for either seat,
        // that word without its seat, then no link.
        var ends = new[] { (world, AnimRuntime.VisualOriginOf(anchor)), (guestWorld, AnimRuntime.VisualOriginOf(guestAnchor)) };
        foreach (var leg in new[]
        {
            new EndsLeg("two ends, linked", EndLink.Linked, 0),
            new EndsLeg("two ends, linked, P2 nearest on the host", EndLink.Linked, 1),
            new EndsLeg("CONTROL: two ends, the host's seat dropped on the wire", EndLink.SeatDropped, 1),
            new EndsLeg("CONTROL: two ends, no link", EndLink.None, 0),
        })
        {
            CrossOnTwoEnds(ctx, ends, drop.Anim, caller, leg, report);
        }

        // Last: a trigger's start marks its closure on the runtime for the rest of the world's life.
        foreach (bool inTrigger in new[] { true, false })
        {
            WithTwoHumans(ctx, chapter, HangarPlanes, null,
                staged => CallInsideTrigger(ctx, world, staged, caller, anchor, inTrigger, report));
        }
    }

    private static Node3D? DropAnchor(TestWorld world, string anim)
    {
        var defs = world.Session.Program.ByAnimName(anim);
        var anchors = defs.Count > 0 ? world.Runtime.AnchorsOf(defs[0]) : Array.Empty<Node3D?>();
        return anchors.Count > 0 ? anchors[0] : null;
    }

    // Both humans inside the drop's band at once, each end's field putting a different seat nearest,
    // as a lagging copy makes it. Linked, the host's word names the owner on both, which the guest's
    // own field would not. With the seat dropped, the guest falls back to the scripted player.
    // Unlinked, each end names its own. A null caller is a gate the bootstrap already has polling.
    private static void CrossOnTwoEnds(TestContext ctx, (TestWorld World, Vector3 Site)[] ends, string anim,
        string? caller, EndsLeg legSpec, StringBuilder report)
    {
        string leg = legSpec.Name;
        int named = legSpec.HostNearest;
        var pane = new SubViewport();
        ctx.Host.AddChild(pane);
        var seats = new PlayerRig[2][];
        var cutscenes = new CutsceneController[2];
        var triggers = new LandingApproachRuntime[2];
        var fields = new Vector3[2][];
        // Each end's range claims by seat, and the guest's reads of the host's verdict after its own claim.
        var claims = new[] { new List<int>(), new List<int>() };
        int pollsAfterClaim = 0;
        var saved = new (Func<int, string?, string?, bool>? Host, Func<IReadOnlyList<Vector3>>? Positions,
            Action<string>? Trigger, Action<string, int>? Range)[2];
        var savedClock = GameClock.Current;
        try
        {
            GameClock.Current = new GameClock { Mode = GameClock.RunMode.FixedStep };
            for (int e = 0; e < 2; e++)
            {
                var runtime = ends[e].World.Runtime;
                saved[e] = (runtime.CallbackHost, runtime.PlayerPositions, runtime.MissionTriggerOwner, runtime.RangeTriggerOwner);
                var own = seats[e] = new[]
                {
                    new PlayerRig { Index = 0, Camera = ctx.Camera, HudParent = pane, Viewport = pane },
                    new PlayerRig { Index = 1, Camera = ctx.Camera, HudParent = pane, Viewport = pane },
                };
                var cutscene = cutscenes[e] = new CutsceneController();
                ctx.Host.AddChild(cutscene);
                cutscene.BindWorld(runtime, ends[e].World.Session.Aircraft);
                cutscene.HostDefinitions(ends[e].World.Session.LandingCutsceneAnims);
                // Each machine's own pane is its own seat; on the guest the scripted player is the host's.
                cutscene.BindRigs(new[] { own[e] }, Array.Empty<FlightController>, e == 1 ? own[0] : null);
                runtime.CallbackHost = cutscene.Host;
                triggers[e] = new LandingApproachRuntime();
                triggers[e].Bind(runtime, ends[e].World.Session.Landings, cutscene, () => own);
                var claim = runtime.RangeTriggerOwner!;
                var claimed = claims[e];
                runtime.RangeTriggerOwner = (name, seat) =>
                {
                    claimed.Add(seat);
                    claim(name, seat);
                };
                var away = ends[e].Site + (Vector3.Up * HangarAwayM);
                var field = fields[e] = new[] { away, away };
                runtime.PlayerPositions = () => field;
            }

            var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(0.03, 0.01, 0.25), new Random(1910));
            var roster = new Net.NetSeat[]
            {
                new() { PeerId = 0, SeatIndex = 0, FlownHere = true, Callsign = "host", PlaneNode = HangarAirframes[0] },
                new() { PeerId = 1, SeatIndex = 1, Callsign = "guest", PlaneNode = HangarAirframes[1] },
            };
            var hostNet = NetSession.Host(mesh[0], roster, HangarNetSeed, null, HangarAirframes);
            var guestNet = NetSession.Guest(mesh[1], HangarAirframes);
            for (int i = 0; i < 200 && !guestNet.Joined; i++)
            {
                hostNet.Step(StepDt);
                guestNet.Step(StepDt);
            }

            if (legSpec.Link != EndLink.None)
            {
                NetPositionalStartLink.Open(hostNet, () => seats[0], triggers[0], null, ends[0].World.Runtime);
                NetPositionalStartLink.Open(guestNet, () => seats[1], triggers[1], null, ends[1].World.Runtime);
                var guestGates = ends[1].World.Runtime.RangeGates;
                var read = guestGates.HostVerdict!;
                guestGates.HostVerdict = gate =>
                {
                    pollsAfterClaim += claims[1].Count > 0 ? 1 : 0;
                    return read(gate);
                };
            }

            if (legSpec.Link == EndLink.SeatDropped)
            {
                var hostGates = ends[0].World.Runtime.RangeGates;
                var send = hostGates.Decided!;
                hostGates.Decided = (gate, passed, _) => send(gate, passed, null);
            }

            float now = 0f;
            var started = new[] { float.NaN, float.NaN };
            var owners = new PlayerRig?[2];
            var played = new bool[2];
            var moved = new bool[2];
            bool Step()
            {
                now += StepDt;
                GameClock.Current!.BeginFrame(StepDt);
                hostNet.Step(StepDt);
                guestNet.Step(StepDt);
                for (int e = 0; e < 2; e++)
                {
                    ends[e].World.Runtime.Advance(StepDt);
                    cutscenes[e].Tick();
                    if (cutscenes[e].Playing && !played[e])
                    {
                        played[e] = true;
                        started[e] = now;
                        owners[e] = cutscenes[e].EpisodeOwner;
                    }

                    moved[e] |= cutscenes[e].Playing && !ReferenceEquals(cutscenes[e].EpisodeOwner, owners[e]);
                }

                // Done once both have played and handed off.
                return played[0] && played[1] && !cutscenes[0].Playing && !cutscenes[1].Playing;
            }

            for (int e = 0; e < 2 && caller != null; e++)
            {
                ends[e].World.Runtime.Play(caller);
            }

            for (float t = 0f; t < HangarArmS; t += StepDt)
            {
                Step();
            }

            ctx.Check(!played[0] && !played[1], $"{leg}: '{caller ?? anim}' arms '{anim}' on both ends and starts nothing while both humans wait out of its band");

            // In the band on both ends at once: the leg's seat nearer on the host, the other on the guest.
            for (int e = 0; e < 2; e++)
            {
                var site = ends[e].Site;
                int nearest = e == 0 ? named : 1 - named;
                fields[e][0] = site + (Vector3.Right * (nearest == 0 ? HangarNearM : HangarFarM));
                fields[e][1] = site + (Vector3.Left * (nearest == 1 ? HangarNearM : HangarFarM));
            }

            for (float t = 0f; t < PlayBudgetS && !Step(); t += StepDt)
            {
            }

            string Seat(PlayerRig? rig) => rig == null ? "-" : $"P{rig.Index + 1}";
            string Claims(int e) => $"[{string.Join(",", claims[e].ConvertAll(s => $"P{s + 1}"))}]";
            report.AppendLine($"{leg}: host owner={Seat(owners[0])} at {started[0]:0.000}s, guest owner={Seat(owners[1])} at {started[1]:0.000}s; "
                + $"claims host {Claims(0)}, guest {Claims(1)}; guest reads of the verdict after its claim {pollsAfterClaim}");
            for (int e = 0; e < 2; e++)
            {
                var owner = owners[e];
                ctx.Check(!moved[e] && claims[e].Count <= 1 && claims[e].TrueForAll(s => s == owner?.Index),
                    $"{leg}: the {(e == 0 ? "host" : "guest")} claims the range start at most once, for its episode's owner, {Seat(owner)}, and the owner holds through the film (claims {Claims(e)})");
            }

            if (legSpec.Loops)
            {
                ctx.Check(pollsAfterClaim > 0,
                    $"{leg}: the gate's own loop reads the host's verdict {pollsAfterClaim} more time(s) after the guest's claim, and none of them claims again");
            }

            switch (legSpec.Link)
            {
                case EndLink.Linked:
                    ctx.Check(ReferenceEquals(owners[0], seats[0][named]) && ReferenceEquals(owners[1], seats[1][named]),
                        $"{leg}: the host decides the drop for its nearest seat, P{named + 1}, and the guest's episode belongs to that same seat, which its own field does not put nearest (host {Seat(owners[0])}, guest {Seat(owners[1])})");
                    break;
                case EndLink.SeatDropped:
                    ctx.Check(ReferenceEquals(owners[0], seats[0][named]) && ReferenceEquals(owners[1], seats[1][0]),
                        $"{leg}: the host owns the drop for P{named + 1}, and the guest, told no seat, leaves it to the scripted player, P1 (host {Seat(owners[0])}, guest {Seat(owners[1])})");
                    break;
                default:
                    ctx.Check(owners[0] != null && owners[1] != null && owners[0]!.Index != owners[1]!.Index,
                        $"{leg}: each end names its own nearest seat, so the owners differ (host {Seat(owners[0])}, guest {Seat(owners[1])})");
                    break;
            }

            if (legSpec.Link != EndLink.None)
            {
                ctx.Check(!float.IsNaN(started[1]) && started[1] > started[0],
                    $"…and the guest starts the drop on the host's pass, after the host, rather than on its own field");
            }
        }
        finally
        {
            for (int e = 0; e < 2; e++)
            {
                var runtime = ends[e].World.Runtime;
                (runtime.CallbackHost, runtime.PlayerPositions, runtime.MissionTriggerOwner, runtime.RangeTriggerOwner) = saved[e];
                runtime.RangeGates.Decided = null;
                runtime.RangeGates.HostVerdict = null;
                runtime.RangeGates.HostSeat = null;
                runtime.RangeGates.HostOpenings = null;
                triggers[e]?.Free();
                cutscenes[e]?.Free();
            }

            pane.Free();
            GameClock.Current = savedClock;
        }
    }

    // A looping code-raising gate built from definitions, since the shipped drop stops its own gate
    // after one film. Its callee ends at once, so a second opening finds it free to start. The host
    // opens it for P2, then closes and reopens it for P1 while the guest's runtime stands still.
    // Counted, the guest claims the reopening for P1 as the host does; uncounted, the guest's
    // name-only memory of the open gate is the control.
    private static void ReopenBetweenPolls(TestContext ctx, bool counted, StringBuilder report)
    {
        const string Marker = "reopen_marker";
        const string Gate = "reopen_gate";
        const string Film = "reopen_film";
        const float RadiusM = 50f;
        string leg = counted ? "a reopening between two guest polls" : "CONTROL: the same reopening with the host's openings uncounted on the guest";
        AnimEvent Ev(string kind, Dictionary<string, object?> data, string? offset = null, float time = 0f) =>
            new() { Kind = kind, StartOffset = offset, StartTime = time, Data = new AnimData(data) };
        AnimProgram Program()
        {
            var gate = new AnimDefinition { Name = Marker, AnimName = Gate };
            var poll = new AnimSequence { Name = "poll" };
            poll.Events.Add(Ev("If", new() { ["condition"] = new Dictionary<string, object?> { ["PlayerRange"] = (double)(RadiusM * RadiusM) } }));
            poll.Events.Add(Ev("CallAnimation", new() { ["name"] = Film }));
            poll.Events.Add(Ev("Endif", new()));
            poll.Events.Add(Ev("Loop", new() { ["Count"] = -1.0 }, "Sequence", 0.2f));
            gate.Sequences.Add(poll);
            var film = new AnimDefinition { AnimName = Film };
            var shot = new AnimSequence { Name = "shot" };
            shot.Events.Add(Ev("Callback", new() { ["value"] = 11.0 }));
            film.Sequences.Add(shot);
            return AnimProgram.FromDefinitions(new[] { gate, film });
        }

        var inside = Vector3.Up * 10f;
        var far = Vector3.Up * 2000f;
        var fields = new[] { new[] { far, far }, new[] { far, far } };
        var claims = new[] { new List<int>(), new List<int>() };
        var stages = new Node3D[2];
        var ends = new AnimRuntime[2];
        var savedClock = GameClock.Current;
        try
        {
            GameClock.Current = new GameClock { Mode = GameClock.RunMode.FixedStep };
            for (int e = 0; e < 2; e++)
            {
                var stage = stages[e] = new Node3D { Name = $"ReopenStage{e}" };
                stage.AddChild(new Node3D { Name = Marker });
                ctx.Host.AddChild(stage);
                var runtime = ends[e] = new AnimRuntime { AutoStart = false, ManualAdvance = true, SoundHandledElsewhere = true };
                ctx.Host.AddChild(runtime);
                runtime.Bind(stage, Program());
                var field = fields[e];
                var claimed = claims[e];
                runtime.PlayerPositions = () => field;
                runtime.CallbackHost = (_, _, _) => true;
                runtime.RangeTriggerOwner = (_, seat) => claimed.Add(seat);
            }

            var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(0.03, 0.01, 0.25), new Random(1970));
            var roster = new Net.NetSeat[]
            {
                new() { PeerId = 0, SeatIndex = 0, FlownHere = true, Callsign = "host", PlaneNode = HangarAirframes[0] },
                new() { PeerId = 1, SeatIndex = 1, Callsign = "guest", PlaneNode = HangarAirframes[1] },
            };
            var hostNet = NetSession.Host(mesh[0], roster, HangarNetSeed, null, HangarAirframes);
            var guestNet = NetSession.Guest(mesh[1], HangarAirframes);
            for (int i = 0; i < 200 && !guestNet.Joined; i++)
            {
                hostNet.Step(StepDt);
                guestNet.Step(StepDt);
            }

            NetPositionalStartLink.Open(hostNet, Array.Empty<PlayerRig>, null, null, ends[0]);
            NetPositionalStartLink.Open(guestNet, Array.Empty<PlayerRig>, null, null, ends[1]);
            if (!counted)
            {
                ends[1].RangeGates.HostOpenings = null;
            }

            void Run(float seconds, bool guestPolls)
            {
                for (float t = 0f; t < seconds; t += StepDt)
                {
                    GameClock.Current!.BeginFrame(StepDt);
                    hostNet.Step(StepDt);
                    guestNet.Step(StepDt);
                    ends[0].Advance(StepDt);
                    if (guestPolls)
                    {
                        ends[1].Advance(StepDt);
                    }
                }
            }

            foreach (var runtime in ends)
            {
                runtime.Play(Gate);
            }

            // P2 opens it on the host and both ends claim. With the guest standing still, P2 leaves
            // and P1 comes in. The guest polls again only after the reopening.
            fields[0][1] = inside;
            Run(1f, guestPolls: true);
            fields[0][1] = far;
            Run(0.6f, guestPolls: false);
            fields[0][0] = inside;
            Run(0.6f, guestPolls: false);
            Run(1f, guestPolls: true);

            string Seats(List<int> seats) => $"[{string.Join(",", seats.ConvertAll(s => $"P{s + 1}"))}]";
            report.AppendLine($"{leg}: claims host {Seats(claims[0])}, guest {Seats(claims[1])}");
            bool hostRight = claims[0].Count == 2 && claims[0][0] == 1 && claims[0][1] == 0;
            if (counted)
            {
                ctx.Check(hostRight && claims[1].Count == 2 && claims[1][0] == 1 && claims[1][1] == 0,
                    $"{leg}: the host claims P2's opening and then P1's, and the guest, polling only after the reopening, claims the same two (host {Seats(claims[0])}, guest {Seats(claims[1])})");
            }
            else
            {
                ctx.Check(hostRight && claims[1].Count == 1 && claims[1][0] == 1,
                    $"{leg}: the guest never sees the gate close, so it claims P2's opening alone while the host claims P1's too (host {Seats(claims[0])}, guest {Seats(claims[1])})");
            }
        }
        finally
        {
            for (int e = 0; e < 2; e++)
            {
                ends[e]?.Free();
                stages[e]?.Free();
            }

            GameClock.Current = savedClock;
        }
    }

    // One drop, armed with both humans outside its band and started by the guest flying onto the
    // hangar. The cutscene host is bound as one machine binds it: its own pane, and the other seat
    // as a copy in no pane. The session's trigger wiring claims the episode.
    private static void CrossTheHangar(TestContext ctx, TestWorld world, Staged staged, SwapCall drop,
        AirframeSwapCode wanted, string caller, Node3D anchor, HangarLeg leg, StringBuilder report)
    {
        var (roster, rigs, before, cutscene, _) = staged;
        var scripted = rigs[0];
        var guest = rigs[1];
        var expected = leg.Wired ? guest : scripted;
        var other = leg.Wired ? scripted : guest;
        var runtime = world.Runtime;
        var marker = world.Session.Aircraft?.PlayerMarker;
        var savedClock = GameClock.Current;
        var savedHost = runtime.CallbackHost;
        var savedPositions = runtime.PlayerPositions;
        var savedTrigger = runtime.MissionTriggerOwner;
        var savedRange = runtime.RangeTriggerOwner;
        var trigger = new LandingApproachRuntime();
        PlayerRig? ordered = null;
        try
        {
            var clock = new GameClock { Mode = GameClock.RunMode.Realtime };
            GameClock.Current = clock;
            cutscene.BindWorld(runtime, world.Session.Aircraft);
            cutscene.HostDefinitions(world.Session.LandingCutsceneAnims);
            // On a guest the scripted player is the host's seat, standing behind the guest's pane.
            cutscene.BindRigs(new[] { leg.OnGuest ? guest : scripted }, () => roster.AiAircraft,
                leg.OnGuest ? scripted : null);
            cutscene.SwapAirframe = order =>
            {
                ordered = order.Owner;
                return roster.RunSwap(order.Owner ?? scripted, order, handsOver: false);
            };
            runtime.CallbackHost = cutscene.Host;
            trigger.Bind(runtime, world.Session.Landings, cutscene, () => rigs);
            if (!leg.Wired)
            {
                runtime.RangeTriggerOwner = null;
            }

            runtime.PlayerPositions = () => new[] { scripted.Controller!.GlobalPosition, guest.Controller!.GlobalPosition };
            var site = AnimRuntime.VisualOriginOf(anchor);
            var waits = site + (Vector3.Right * HangarAwayM);
            scripted.Controller!.Activate(waits, waits + Vector3.Right);
            guest.Controller!.Activate(site + (Vector3.Left * HangarAwayM), site);
            runtime.Play(caller);
            for (float t = 0f; t < HangarArmS; t += StepDt)
            {
                clock.BeginFrame(StepDt);
                runtime.Advance(StepDt);
                cutscene.Tick();
            }

            ctx.Check(!cutscene.Playing,
                $"{leg.Name}: '{caller}' arms '{drop.Anim}' and starts nothing while both humans wait {HangarAwayM:0} m out");

            guest.Controller!.Activate(site, site + (Vector3.Forward * 100f));
            var held = other.Controller!.GlobalPosition;
            var hangar = new HangarReading();
            for (int i = 0; i < (int)(PlayBudgetS / StepDt); i++)
            {
                clock.BeginFrame(StepDt);
                runtime.Advance(StepDt);
                cutscene.Tick();
                if (!hangar.Read(cutscene, marker, other, held, world.Session.Aircraft))
                {
                    break;
                }
            }

            report.AppendLine($"{leg.Name}: owner=P{(hangar.Owner?.Index ?? -1) + 1} ordered=P{(ordered?.Index ?? -1) + 1} " +
                $"codes {string.Join(",", cutscene.Codes)}; staged {hangar.StagedFrames} frame(s), owner off the marker " +
                $"{hangar.OwnerOffM:0.###} m, other moved {hangar.OtherMovedM:0.###} m, flown stage on the owner={hangar.FlownIsOwner}; " +
                $"P1 '{before[0].Loadout?.Def.Def}' -> '{scripted.Controller?.Loadout?.Def.Def ?? "-"}', " +
                $"P2 '{before[1].Loadout?.Def.Def}' -> '{guest.Controller?.Loadout?.Def.Def ?? "-"}'");
            string who = $"P{expected.Index + 1}";
            ctx.Check(ReferenceEquals(hangar.Owner, expected),
                $"{leg.Name}: the guest crossing the hangar starts the drop, and the episode belongs to {who}");
            ctx.Check(ReferenceEquals(ordered, expected),
                $"…and the {drop.Code} swap order names {who}");
            ctx.Check(!ReferenceEquals(expected.Controller, before[expected.Index])
                      && string.Equals(expected.Controller?.Loadout?.Def.Def, wanted.Def, StringComparison.OrdinalIgnoreCase),
                $"…which is rebuilt on '{wanted.Def}', the Blue Streak's record");
            ctx.Check(ReferenceEquals(other.Controller, before[other.Index]),
                $"…while P{other.Index + 1} keeps the aeroplane it was flying");
            ctx.Check(hangar.StagedFrames > 0 && hangar.OwnerOffM <= StageToleranceM && hangar.FlownIsOwner,
                $"…the film stages {who} on the player marker ({hangar.StagedFrames} frame(s), at most {hangar.OwnerOffM:0.###} m off it) and stages its airframe as the flown one");
            ctx.Check(hangar.OtherMovedM <= StageToleranceM,
                $"…and P{other.Index + 1} stays where it was held rather than riding the marker ({hangar.OtherMovedM:0.###} m)");
        }
        finally
        {
            runtime.CallbackHost = savedHost;
            runtime.PlayerPositions = savedPositions;
            runtime.MissionTriggerOwner = savedTrigger;
            runtime.RangeTriggerOwner = savedRange;
            trigger.Free();
            GameClock.Current = savedClock;
        }
    }

    // The drop's caller started as a mission trigger, its claim naming P2 as the trigger's pilot, with
    // P1 already inside the drop's band. The in-band range start inside that start names no seat, so
    // P2 keeps the episode. The control makes the same claim and the same call outside the bracket.
    private static void CallInsideTrigger(TestContext ctx, TestWorld world, Staged staged, string caller,
        Node3D anchor, bool inTrigger, StringBuilder report)
    {
        var (roster, rigs, _, cutscene, _) = staged;
        var scripted = rigs[0];
        var pilot = rigs[1];
        var expected = inTrigger ? pilot : scripted;
        string leg = inTrigger ? "inside the trigger's start" : "CONTROL: the same claim and call outside the trigger's bracket";
        var runtime = world.Runtime;
        var savedClock = GameClock.Current;
        var savedHost = runtime.CallbackHost;
        var savedPositions = runtime.PlayerPositions;
        var savedTrigger = runtime.MissionTriggerOwner;
        var savedRange = runtime.RangeTriggerOwner;
        var trigger = new LandingApproachRuntime();
        try
        {
            var clock = new GameClock { Mode = GameClock.RunMode.Realtime };
            GameClock.Current = clock;
            cutscene.BindWorld(runtime, world.Session.Aircraft);
            cutscene.HostDefinitions(world.Session.LandingCutsceneAnims);
            cutscene.BindRigs(new[] { scripted }, () => roster.AiAircraft);
            cutscene.SwapAirframe = order => roster.RunSwap(order.Owner ?? scripted, order, handsOver: false);
            runtime.CallbackHost = cutscene.Host;
            trigger.Bind(runtime, world.Session.Landings, cutscene, () => rigs);

            // The claim a landing row's start makes for the human who flew it.
            Action<string> claim = anim => cutscene.Own(anim, pilot);
            runtime.MissionTriggerOwner = claim;
            runtime.PlayerPositions = () => new[] { scripted.Controller!.GlobalPosition, pilot.Controller!.GlobalPosition };
            var site = AnimRuntime.VisualOriginOf(anchor);
            scripted.Controller!.Activate(site, site + (Vector3.Forward * 100f));
            pilot.Controller!.Activate(site + (Vector3.Left * HangarAwayM), site);
            if (inTrigger)
            {
                runtime.PlayMissionTrigger(caller);
            }
            else
            {
                claim(caller);
                runtime.Play(caller);
            }

            PlayerRig? owner = null;
            string? episode = null;
            bool ended = false;
            for (int i = 0; i < (int)(PlayBudgetS / StepDt) && !ended; i++)
            {
                clock.BeginFrame(StepDt);
                runtime.Advance(StepDt);
                cutscene.Tick();
                if (cutscene.Playing && owner == null)
                {
                    owner = cutscene.EpisodeOwner;
                    episode = cutscene.Anim;
                }

                ended = owner != null && !cutscene.Playing;
            }

            report.AppendLine($"{leg}: owner=P{(owner?.Index ?? -1) + 1}, episode '{episode ?? "-"}', ended={ended}");
            ctx.Check(ReferenceEquals(owner, expected),
                $"{leg}: '{caller}' calls the drop with P1 in its band, and the episode belongs to P{expected.Index + 1}");
        }
        finally
        {
            runtime.CallbackHost = savedHost;
            runtime.PlayerPositions = savedPositions;
            runtime.MissionTriggerOwner = savedTrigger;
            runtime.RangeTriggerOwner = savedRange;
            trigger.Free();
            GameClock.Current = savedClock;
        }
    }

    // One episode, claimed by the guest or by nobody, played the way a flown session plays it: the
    // slot is written before the definition starts, the runtime dispatches the code to the host, and
    // the seam the session fills sends the swap to the rig the order names.
    private static void Own(TestContext ctx, TestWorld world, Staged staged, AirframeSwapCode wanted,
        SwapCall call, bool guest, StringBuilder report)
    {
        var (roster, rigs, before, cutscene, pool) = staged;
        var owner = guest ? rigs[1] : rigs[0];
        var other = guest ? rigs[0] : rigs[1];
        string who = guest ? "the guest (P2)" : "nobody";
        // Read off the outgoing aircraft BEFORE the swap frees it, since every reading below is
        // about what carried across and the aeroplane it carried from is gone by then.
        var was = new Flying(before[owner.Index], before[other.Index]);
        var savedClock = GameClock.Current;
        var savedHost = world.Runtime.CallbackHost;
        PlayerRig? ordered = null;
        try
        {
            var clock = new GameClock { Mode = GameClock.RunMode.Realtime };
            GameClock.Current = clock;
            // ⚠ The node table is indexed for the claimed leg alone, and that leg runs last: a leg
            // frees its aircraft on the way out, and a second indexing walk would then compare its
            // own keys against a disposed node. A session frees nothing mid-mission.
            cutscene.BindWorld(world.Runtime, guest ? world.Session.Aircraft : null);
            cutscene.HostDefinitions(ClosureOf(world, call.Anim));
            cutscene.BindRigs(rigs, () => roster.AiAircraft);
            // The session's own seam, and the whole subject of this suite: the order names the rig,
            // and an order naming none falls back to the scripted player's.
            cutscene.SwapAirframe = order =>
            {
                ordered = order.Owner;
                return roster.RunSwap(order.Owner ?? rigs[0], order, handsOver: false);
            };
            world.Runtime.CallbackHost = cutscene.Host;
            ctx.Check(ReferenceEquals(cutscene.EpisodeOwner, rigs[0]),
                $"with no trigger claimed, the host answers the scripted player as the episode owner");

            cutscene.Own(call.Anim, guest ? rigs[1] : null);
            world.Runtime.Play(call.Anim);
            bool swapped = Play(world, cutscene, owner, before[owner.Index]);

            report.AppendLine($"claimed by {who}: swapped={swapped} owner=" +
                $"P{(cutscene.EpisodeOwner?.Index ?? -1) + 1} ordered=" +
                $"P{(ordered?.Index ?? -1) + 1} codes {string.Join(",", cutscene.Codes)}");
            ctx.Check(swapped, $"playing '{call.Anim}' reaches callback {call.Code} through the runtime");
            ctx.Check(ReferenceEquals(cutscene.EpisodeOwner, owner),
                $"the episode claimed by {who} belongs to P{owner.Index + 1}");
            ctx.Check(ReferenceEquals(ordered, owner),
                $"…and the swap order carries that rig, so the seam is answered rather than guessed");
            CheckRigs(ctx, wanted, owner, other, before, was, report);
            CheckRegistrations(ctx, pool, was, report);
            if (guest)
            {
                CheckStaged(ctx, world.Session.Aircraft, owner, other, report);
            }
        }
        finally
        {
            world.Runtime.CallbackHost = savedHost;
            GameClock.Current = savedClock;
        }
    }

    // The owner flies the airframe the code named; the other human keeps their existing aeroplane.
    private static void CheckRigs(TestContext ctx, AirframeSwapCode wanted, PlayerRig owner,
        PlayerRig other, IReadOnlyList<FlightController> before, Flying was, StringBuilder report)
    {
        var after = owner.Controller;
        var stayed = other.Controller;
        report.AppendLine($"P{owner.Index + 1}: '{was.OwnerDef}' -> '{after?.Loadout?.Def.Def ?? "-"}'; " +
            $"P{other.Index + 1}: '{was.OtherDef}' -> '{stayed?.Loadout?.Def.Def ?? "-"}'");
        ctx.Check(after != null && !ReferenceEquals(after, before[owner.Index]),
            $"the owner's rig is rebuilt rather than the one that happened to be player 1");
        ctx.Check(string.Equals(after?.Loadout?.Def.Def, wanted.Def, StringComparison.OrdinalIgnoreCase),
            $"…on '{wanted.Def}', the record the code names");
        ctx.Check(ReferenceEquals(stayed, before[other.Index]),
            $"and P{other.Index + 1} is still flying the aircraft it was flying, not a rebuilt one");
        ctx.Check(stayed != null && GodotObject.IsInstanceValid(stayed) && stayed.IsInsideTree(),
            $"…still in the world, so the swap took one aeroplane out of the mission and no more");
        ctx.Same(was.OwnerId, after?.PlayerIndex ?? -1,
            $"the swapped pilot keeps their own shooter id, which is what a round already in the air scores to");
        ctx.Check(was.OwnerId != was.OtherId && (stayed?.PlayerIndex ?? -1) == was.OtherId,
            $"…and it is not the other human's, so the two pilots stay apart in the pool");
    }

    // The other thing the episode owner decides: which aeroplane a hookup definition resolves its
    // own hook, wings and mount offset off. It is the owner's replacement, not the aircraft player
    // one happens to be flying.
    private static void CheckStaged(TestContext ctx, AircraftStage? stage, PlayerRig owner,
        PlayerRig other, StringBuilder report)
    {
        report.AppendLine($"staged flown: " +
            $"'{(stage?.Flown is { } node ? AnimRuntime.NameOf(node) : "-")}'");
        ctx.Check(stage?.Flown != null && ReferenceEquals(stage.Flown, owner.Controller?.PlaneModel),
            $"the runtime's node table carries the episode owner's own airframe as the flown one");
        ctx.Check(!ReferenceEquals(stage?.Flown, other.Controller?.PlaneModel),
            $"…and not the other human's, which is the aeroplane a single-player mission would have staged");
    }

    // ⚠ The pool's shooter registrations are why the removal precedes the build in the roster's
    // swap: DetachRosterBindings drops the outgoing pilot's aircraft body and the replacement
    // registers its own under the same shooter id.
    private static void CheckRegistrations(TestContext ctx, ProjectilePool pool, Flying was,
        StringBuilder report)
    {
        var mine = pool.RigOfShooter(was.OwnerId);
        var theirs = pool.RigOfShooter(was.OtherId);
        bool Live(FlightController? rig) =>
            rig != null && GodotObject.IsInstanceValid(rig) && rig.IsInsideTree();
        report.AppendLine($"shooter ids: owner {(Live(mine) ? "live" : "missing")}, " +
            $"other {(Live(theirs) ? "live" : "missing")}");
        ctx.Check(Live(mine),
            $"the swapped pilot's shooter id resolves a live aircraft, so the replacement took the registration the outgoing rig gave up");
        ctx.Check(Live(theirs),
            $"and the human who kept their aeroplane keeps their own, untouched");
    }

    // The episode played out on a realtime clock, answering whether the owner's rig was replaced.
    // The whole budget is run rather than broken out of at the swap, so a second, later swap onto
    // the wrong rig would still be visible in the readings below.
    private static bool Play(TestWorld world, CutsceneController cutscene, PlayerRig owner,
        FlightController before)
    {
        bool swapped = false;
        for (int i = 0; i < (int)(PlayBudgetS / StepDt); i++)
        {
            GameClock.Current!.BeginFrame(StepDt);
            world.Runtime.Advance(StepDt);
            cutscene.Tick();
            swapped |= !ReferenceEquals(owner.Controller, before);
        }

        return swapped;
    }

    // Two humans off the session's own roster, flying different airframes, over the mission's built
    // world. Each leg gets its own, because a swap replaces the rig it ran on.
    private static void WithTwoHumans(TestContext ctx, string chapter, string planes,
        Action<FlightRoster, IReadOnlyList<FlightController>>? stage, Action<Staged> leg)
    {
        var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, chapter));
        var pool = new ProjectilePool(textures, null, null);
        ctx.Host.AddChild(pool);
        var pane = new SubViewport();
        ctx.Host.AddChild(pane);
        var rigs = new[]
        {
            new PlayerRig { Index = 0, Camera = ctx.Camera, HudParent = pane, Viewport = pane },
            new PlayerRig { Index = 1, Camera = ctx.Camera, HudParent = pane, Viewport = pane },
        };
        FlightRoster? roster = null;
        var cutscene = new CutsceneController();
        ctx.Host.AddChild(cutscene);
        try
        {
            roster = BuildRoster(ctx, chapter, textures, pool, rigs, planes: planes);
            roster.BuildPlayers(rigs);
            var before = new List<FlightController>();
            foreach (var rig in rigs)
            {
                before.Add(rig.Controller
                    ?? throw new InvalidOperationException($"rig {rig.Index} was not built"));
            }

            stage?.Invoke(roster, before);
            leg(new Staged(roster, rigs, before, cutscene, pool));
        }
        finally
        {
            // Only what this suite put on the host: the host is shared with every suite in the run.
            var live = new List<FlightController>();
            foreach (var rig in rigs)
            {
                if (rig.Controller is { } controller)
                {
                    live.Add(controller);
                }
            }

            var members = new List<FlightController>(roster?.AiAircraft ?? Array.Empty<FlightController>());
            roster?.ClearMembership();
            foreach (var plane in live)
            {
                plane.Free();
            }

            foreach (var ai in members)
            {
                ai.Free();
            }

            cutscene.Free();
            pane.Free();
            pool.Free();
            textures.Dispose();
        }
    }

    // The first definition in the mission's compiled program that raises a swap code, read out of
    // the shipped data rather than named here.
    private static SwapCall? SwapCallIn(AnimProgram program)
    {
        foreach (var def in program.Defs)
        {
            if (def.AnimName is not { } anim)
            {
                continue;
            }

            string root = def.RootName is { Length: > 0 } named ? named : def.Name;
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
                        return new SwapCall(anim, code, root);
                    }
                }
            }
        }

        return null;
    }

    // Every definition the capture can reach: what the host answers for, and the only way a called
    // definition's codes are hosted.
    private static IReadOnlyList<string> ClosureOf(TestWorld world, string anim)
    {
        var names = new List<string>();
        foreach (var def in world.Session.Program.Subset(new[] { anim }).Defs)
        {
            if (def.AnimName is { } name && !names.Contains(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    private static Node3D? First(IReadOnlyList<Node3D> nodes) => nodes.Count > 0 ? nodes[0] : null;

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

    // The definition that raises a swap code, the code, and the root node it is raised from.
    private readonly record struct SwapCall(string Anim, int Code, string Root);

    // What the two humans were flying, read before the swap frees the aeroplane it replaces.
    private readonly record struct Flying(int OwnerId, string OwnerDef, int OtherId, string OtherDef)
    {
        public Flying(FlightController owner, FlightController other)
            : this(owner.PlayerIndex, owner.Loadout?.Def.Def ?? "-",
                   other.PlayerIndex, other.Loadout?.Def.Def ?? "-")
        {
        }
    }

    // One hangar leg: whose machine binds the cutscene host, and whether the range claim is wired.
    private readonly record struct HangarLeg(string Name, bool Wired, bool OnGuest);

    // One two-end leg: how the ends are joined, and the seat the host's field puts nearest the gate.
    // The guest's field puts the other seat nearest. Loops: the gate's definition keeps polling it.
    private readonly record struct EndsLeg(string Name, EndLink Link, int HostNearest, bool Loops = false);

    // The actors one staged episode hands its leg: the session-shaped roster and its two rigs, what
    // each was flying before, the host, and the pool the registrations are counted in.
    private sealed record Staged(
        FlightRoster Roster,
        IReadOnlyList<PlayerRig> Rigs,
        IReadOnlyList<FlightController> Before,
        CutsceneController Cutscene,
        ProjectilePool Pool);

    // What one drop did, read a frame at a time: the owner it latched first. While the film holds
    // the pilots out of flight, it also reads the owner's distance off the marker and the other's
    // off its held spot.
    private sealed class HangarReading
    {
        private bool _played;

        public PlayerRig? Owner { get; private set; }

        public int StagedFrames { get; private set; }

        public float OwnerOffM { get; private set; }

        public float OtherMovedM { get; private set; }

        public bool FlownIsOwner { get; private set; } = true;

        // False once the episode has played and handed off, which ends the leg.
        public bool Read(CutsceneController cutscene, Node3D? marker, PlayerRig other, Vector3 held,
            AircraftStage? stage)
        {
            if (!cutscene.Playing)
            {
                return !_played;
            }

            _played = true;
            Owner ??= cutscene.EpisodeOwner;
            if (!cutscene.OutOfFlight || marker is not { Visible: true } || Owner?.Controller is not { } plane)
            {
                return true;
            }

            StagedFrames++;
            OwnerOffM = Mathf.Max(OwnerOffM, plane.GlobalPosition.DistanceTo(AnimRuntime.WorldTransform(marker, out _).Origin));
            OtherMovedM = Mathf.Max(OtherMovedM, other.Controller?.GlobalPosition.DistanceTo(held) ?? float.MaxValue);
            FlownIsOwner &= ReferenceEquals(stage?.Flown, plane.PlaneModel);
            return true;
        }
    }

    // The abreast pair a co-op campaign start places, high enough over the mission's own terrain
    // that both aircraft are flying rather than resolving a ground contact.
    private sealed class CoopStarts : IFlightStarts
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
