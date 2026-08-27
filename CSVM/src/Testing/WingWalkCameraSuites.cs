using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The suite over CM02's capture cutscene framing: the mission's own capture definition
/// played through the runtime on a realtime clock, with the aircraft it belongs to spawned as a
/// roster block, reading where the definition puts <c>camera1</c>. The camera rides the wing walk's
/// moving frame, which the capture poses onto the captured aeroplane, so the reading separates a
/// shot on that aeroplane from one parked at the world origin. Decode:
/// docs/formats/anim-definitions/cutscenes.md.</summary>
internal static class WingWalkCameraSuites
{
    // CM02's story position and the airframe the player flies into the capture.
    private const int Cm02Seq = 1;
    private const string StartPlane = "player_bhawk";

    // The captured aeroplane's roster block, the animation the capture's approach trigger starts,
    // and the moving frame the wing walk's own definition drives.
    private const string CaptureBlock = "britbalmoral_1";
    private const string CaptureAnim = "ww_balmoral1";
    private const string WalkFrame = "wingwalk_parent";

    // The played leg's frame: past the wing walk's own 19.25 s motion, sampled once a second.
    private const float StepDt = 1f / 60f;
    private const float PlayBudgetS = 24f;
    private const int SamplesPerSecond = 60;

    // How far the shot may open from the aeroplane it is about, and how far the wing walk's frame
    // may land from that aeroplane. The authored camera scripts open ~30 m off the frame and the
    // frame is posed onto the aeroplane itself, so a composed shot reads well inside both and one
    // playing in world space reads thousands of metres out.
    private const float OpeningRangeM = 100f;
    private const float FrameToleranceM = 5f;

    // The wing walk's own authored motion, which is how long the shot lasts. A definition that
    // completes instantly satisfies every end-state check, so the episode's DURATION is asserted
    // beside the framing (INSTR-21).
    private const float WalkRunTimeS = 19.25f;
    private const float RunTimeToleranceS = 1f;

    /// <summary>Plays CM02's own capture definition over that mission's built world and reads the
    /// camera it poses: the shot has to sit on the captured aeroplane rather than at the world
    /// origin, which is where a wing-walk frame with no host to hang off lands.</summary>
    internal static void WingWalkCamera(TestContext ctx)
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

        ctx.WriteArtifact($"test-wingwalk-camera-{chapter}-{folder}.txt", report.ToString());
        ctx.Note($"played {chapter}/{folder}'s '{CaptureAnim}' and read the camera it poses");
    }

    private static void Drive(TestContext ctx, TestWorld world, string chapter, string folder,
        StringBuilder report)
    {
        var plan = PlanNamed(ctx, chapter, folder, CaptureBlock);
        if (plan == null)
        {
            ctx.Check(false, $"{chapter}/{folder}'s roster carries a '{CaptureBlock}' block for the capture to belong to");
            return;
        }

        report.AppendLine($"'{CaptureBlock}' authored at {plan.Position} yaw {plan.YawDeg:0.#} " +
            $"flying '{plan.PlaneNode}'");
        // ⚠ Far from the gamez origin on purpose: a shot posed off nothing lands at the origin, and
        // a subject spawned near it would read as correctly framed (DIAG-19).
        ctx.Check(plan.Position.Length() > OpeningRangeM * 10f,
            $"and authored far enough from the world origin that a shot parked there is not mistaken for a framed one");

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
            roster = BuildRoster(ctx, world, chapter, textures, pool, rigs, plan.Position);
            roster.BuildPlayers(rigs);
            var aim = plan.Position + Vector3.Forward;
            var captured = roster.SpawnAi(CampaignRosterPlan.SpawnFor(plan, plan.Position, aim,
                AiPilot.HoldingCourse(plan.Position, aim)));
            if (captured == null)
            {
                ctx.Check(false, $"the '{CaptureBlock}' block spawns an aircraft to capture");
                return;
            }

            int grafted = RosterMarkers.Attach(world.Gamez, world.Session.Builder.Scene,
                world.Runtime, CaptureBlock, captured);
            report.AppendLine($"spawned '{CaptureBlock}' at {captured.WorldPosition}, " +
                $"{grafted} marker graft(s)");
            report.AppendLine($"before: {Census(world)}");
            Play(ctx, world, rig, cutscene, roster, captured, report);
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

            cutscene.Free();
            pane.Free();
            pool.Free();
            textures.Dispose();
        }
    }

    // The capture as a flown session runs it: started through the mission-trigger seam the approach
    // table starts it with, on a realtime clock, with each aircraft stepping itself (INSTR-26).
    private static void Play(TestContext ctx, TestWorld world, PlayerRig rig,
        CutsceneController cutscene, FlightRoster roster, FlightController captured,
        StringBuilder report)
    {
        var savedClock = GameClock.Current;
        var savedHost = world.Runtime.CallbackHost;
        try
        {
            var clock = new GameClock { Mode = GameClock.RunMode.Realtime };
            GameClock.Current = clock;
            cutscene.BindWorld(world.Runtime);
            cutscene.HostDefinitions(ClosureOf(world, CaptureAnim));
            cutscene.BindRigs(new[] { rig }, () => roster.AiAircraft);
            cutscene.SwapAirframe = order => roster.RunSwap(rig, order, handsOver: true);
            world.Runtime.CallbackHost = cutscene.Host;
            var camera = First(world.Runtime.FindNodes(CutsceneController.CameraNode));
            ctx.Check(camera != null,
                $"the built world stands up the '{CutsceneController.CameraNode}' node the capture poses");
            var subject = captured.WorldPosition;
            world.Runtime.PlayMissionTrigger(CaptureAnim);

            // ⚠ Read only while the shot is COMPOSED, which is while `camera1` hangs under the wing
            // walk's frame. The episode's own end detaches it back to the world root inside the
            // last advance, and a sample past that reads the park rather than the shot.
            float openedAt = -1f;
            float framedAt = -1f;
            float composedTo = 0f;
            bool nearerTheOriginEver = false;
            int samples = 0;
            for (int i = 0; i < (int)(PlayBudgetS / StepDt); i++)
            {
                clock.BeginFrame(StepDt);
                world.Runtime.Advance(StepDt);
                // ⚠ Read the shot BEFORE the host ticks. The handoff parks `camera1` back under
                // the world root inside that tick, so a read after it takes the park for the shot
                // on the very step the episode ends.
                bool composed = cutscene.Playing && camera != null && IsUnder(camera, WalkFrame);
                var eye = camera?.GlobalTransform.Origin ?? Vector3.Zero;
                float frameOff = composed ? FrameOffset(world, subject) : -1f;
                cutscene.Tick();
                rig.Controller?._PhysicsProcess(StepDt);
                captured._PhysicsProcess(StepDt);
                if (!composed)
                {
                    continue;
                }

                float now = i * StepDt;
                composedTo = now;
                float off = eye.DistanceTo(subject);
                if (openedAt < 0f)
                {
                    openedAt = off;
                    framedAt = frameOff;
                }

                if (off > eye.Length())
                {
                    nearerTheOriginEver = true;
                }

                if (i % SamplesPerSecond != 0)
                {
                    continue;
                }

                samples++;
                report.AppendLine($"t={now,5:0.0} s camera1 {eye} " +
                    $"{off:0} m off the capture, {eye.Length():0} m off the world origin; " +
                    $"frame {FrameOrigin(world)}");
            }

            report.AppendLine($"after: {Census(world)}");
            report.AppendLine($"shot composed for {composedTo:0.##} s over {samples} sample(s), " +
                $"opened {openedAt:0} m off the capture with the walk frame {framedAt:0} m off it, " +
                $"nearer-the-origin-ever={nearerTheOriginEver}");
            ctx.Check(samples > 0, $"the played capture composes its shot inside the wing walk's own frame");
            ctx.Check(Mathf.Abs(composedTo - WalkRunTimeS) < RunTimeToleranceS,
                $"and holds it for the walk's own {WalkRunTimeS:0.##} s rather than completing in the frame it started (read {composedTo:0.##} s)");
            ctx.Check(framedAt >= 0f && framedAt < FrameToleranceM,
                $"the capture poses its '{WalkFrame}' onto the aeroplane it belongs to, within {FrameToleranceM:0} m of it");
            ctx.Check(openedAt >= 0f && openedAt < OpeningRangeM,
                $"so the shot opens within {OpeningRangeM:0} m of that aeroplane rather than at the world origin");
            ctx.Check(!nearerTheOriginEver,
                $"and never sits nearer the world origin than the aeroplane it is filming, which is the water the capture was seen over");
        }
        finally
        {
            world.Runtime.CallbackHost = savedHost;
            GameClock.Current = savedClock;
        }
    }

    private static string FrameOrigin(TestWorld world)
    {
        var found = world.Runtime.FindNodes(WalkFrame);
        if (found.Count == 0)
        {
            return "absent";
        }

        var parts = new List<string>();
        foreach (var node in found)
        {
            parts.Add($"{node.GlobalTransform.Origin}");
        }

        return string.Join(" ", parts);
    }

    private static bool IsUnder(Node3D node, string ancestorName)
    {
        for (var p = node.GetParent() as Node3D; p != null; p = p.GetParent() as Node3D)
        {
            if (string.Equals(AnimRuntime.NameOf(p), ancestorName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // How far the wing walk's frame sits from the aeroplane the capture belongs to; -1 when the
    // frame is not in the world at all, which is the reading before it was stood up.
    private static float FrameOffset(TestWorld world, Vector3 subject)
    {
        var found = world.Runtime.FindNodes(WalkFrame);
        return found.Count > 0 ? found[0].GlobalTransform.Origin.DistanceTo(subject) : -1f;
    }

    // What the capture's node table can actually reach: every name the definition addresses, with
    // how many nodes answer it and where the first one sits.
    private static string Census(TestWorld world)
    {
        var parts = new List<string>();
        foreach (string name in new[] { CaptureBlock, WalkFrame, "camera1", "player", "body", "hatch" })
        {
            var found = world.Runtime.FindNodes(name);
            parts.Add(found.Count == 0
                ? $"'{name}' 0"
                : $"'{name}' {found.Count} at {found[0].GlobalTransform.Origin}");
        }

        return string.Join(", ", parts);
    }

    // Every definition the capture can reach, its own plus the CALL_ANIMATION closure: what the
    // session's own host answers for, and the only way the called wing walk's codes are hosted.
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

    private static RosterSpawnPlan? PlanNamed(TestContext ctx, string chapter, string folder,
        string name)
    {
        var blocks = AiSkills.LoadRoster(SessionPaths.MissionZrdr(ctx.DataRoot, chapter, folder));
        var plan = CampaignRosterPlan.Build(blocks, VehicleDefs.Load(ctx.ZrdrPath),
            Array.Empty<AiNet>());
        foreach (var spawn in plan.Spawns)
        {
            if (string.Equals(spawn.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return spawn;
            }
        }

        return null;
    }

    private static Node3D? First(IReadOnlyList<Node3D> found) => found.Count > 0 ? found[0] : null;

    private static FlightRoster BuildRoster(TestContext ctx, TestWorld world, string chapter,
        TextureArchive textures, ProjectilePool pool, IReadOnlyList<PlayerRig> rigs, Vector3 near)
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
            }, new WingWalkStarts(near));
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

    // The player flown alongside the captured aeroplane rather than at the gamez origin, so the
    // camera reading below separates the two places instead of finding them the same.
    private sealed class WingWalkStarts : IFlightStarts
    {
        private readonly Vector3 _near;

        internal WingWalkStarts(Vector3 near) => _near = near;

        public IReadOnlyList<FlightStart> ChooseStarts(IReadOnlyList<SpawnPoint>? spawns,
            string missionZrdrPath, int spawnBase, int playerCount)
        {
            var starts = new FlightStart[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                var position = _near + new Vector3(i * 60f, 0f, 200f);
                starts[i] = new FlightStart(position, position + Vector3.Forward, 0.5f, 90f);
            }

            return starts;
        }
    }
}
