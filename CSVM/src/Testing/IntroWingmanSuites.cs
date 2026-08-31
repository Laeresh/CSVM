using System;
using System.Collections.Generic;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>The suite over the wingman a story-mission intro shows beside the player: C1/M04's
/// <c>mission_intro_animation</c> calls <c>playerdrop</c> and <c>playerthruclouds</c>, and each
/// of those calls its own <c>pfighterNN</c> definition, rooted on the aircraft archive's
/// <c>piratefighter</c>, which activates the prop and flies it on an SI script through the
/// cutscene camera's frame. Decode: docs/formats/anim-definitions/cutscenes.md.</summary>
internal static class IntroWingmanSuites
{
    private const string Chapter = "C1";
    private const string Mission = "M04";
    private const string IntroAnim = "mission_intro_animation";

    private const string PlaneNode = "player_bhawk";
    private const float StepDt = 1f / 60f;

    // The intro's hangar scenes run about 38 s before the launch on a Bloodhawk (scene1 15.9 s,
    // scene2 12.4 s, 1stperson 9.9 s), the launch 1.95 s and the dive 13 s.
    private const float DriveBudgetS = 62f;

    // The widest half-angle a frame can have: the camera's own FOV is authored per shot, and a
    // prop outside 60 degrees off the axis is off screen at any of them.
    private const float FrustumHalfAngleDeg = 60f;

    // A prop drawn at the world origin is an unbound event, not a wingman.
    private const float PosedMinM = 10f;

    private const int AnimRunning = 2;
    private const int AnimExecuted = 3;

    // The two exterior beats and the wingman definition each calls.
    private static readonly (string Beat, string Wingman)[] Beats =
    {
        ("playerdrop", "pfighter12"),
        ("playerthruclouds", "pfighter13"),
    };

    /// <summary>Drives C1/M04's shipped intro over its BUILT world and reads the staged
    /// <c>piratefighter</c> during the launch and dive beats: its definition ran, the prop is
    /// drawn, posed off the origin and inside the cutscene camera's frustum.</summary>
    [Suite("intro-wingmen",
        "the wingman C1/M04's shipped intro shows beside the player, over its BUILT world: "
        + "'playerdrop' and 'playerthruclouds' each run their 'pfighterNN' definition, and the "
        + "aircraft archive's 'piratefighter' prop is drawn, posed off the origin and inside the "
        + "cutscene camera's frustum through the launch and the dive")]
    internal static void IntroWingmen(TestContext ctx)
    {
        ctx.RequireData(SessionPaths.MissionZrdr(ctx.DataRoot, Chapter, Mission), $"{Chapter}/{Mission} zrdr");
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, Chapter), $"{Chapter} textures");
        ctx.RequireData(ctx.PlanesGamezPath, $"aircraft archive");

        var report = new StringBuilder();
        ctx.CutsceneRoots = true;
        try
        {
            ctx.WithWorld(Chapter, collision: false, Mission, world => Drive(ctx, world, report));
        }
        finally
        {
            ctx.CutsceneRoots = false;
        }

        ctx.WriteArtifact($"test-intro-wingmen-{Chapter}-{Mission}.txt", report.ToString());
        ctx.Note($"drove {Chapter}/{Mission}'s {IntroAnim} and read the wingman through both exterior beats");
    }

    private static void Drive(TestContext ctx, TestWorld world, StringBuilder report)
    {
        if (world.Session.Aircraft is not { } stage || stage.Prop is not { } prop
            || stage.PlayerMarker is not { } marker)
        {
            ctx.Check(false, $"the world build staged the intro's aircraft");
            return;
        }

        foreach (var (beat, wingman) in Beats)
        {
            ctx.Check(world.Session.Program.ByAnimName(beat).Count > 0, $"{Chapter}/{Mission} compiles '{beat}'");
            ctx.Check(world.Session.Program.ByAnimName(wingman).Count > 0, $"and its wingman definition '{wingman}'");
        }

        var camera = world.Runtime.FindNodes(CutsceneController.CameraNode);
        if (camera.Count == 0)
        {
            ctx.Check(false, $"the world build stood up '{CutsceneController.CameraNode}'");
            return;
        }

        var cutscene = new CutsceneController();
        ctx.Host.AddChild(cutscene);
        var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, world.Chapter));
        var pool = new ProjectilePool(textures, null, null);
        ctx.Host.AddChild(pool);
        try
        {
            var rig = BuildRig(ctx, world, pool);
            cutscene.BindWorld(world.Runtime, stage);
            cutscene.BindRigs(
                new[]
                {
                    new PlayerRig
                    {
                        Index = 0,
                        Camera = ctx.Camera,
                        HudParent = ctx.Host,
                        Controller = rig,
                    },
                },
                () => Array.Empty<FlightController>());
            world.Runtime.CallbackHost = cutscene.Host;
            cutscene.Host(CutsceneController.CodeOutOfFlight, IntroAnim);
            DriveIntro(ctx, world, cutscene, camera[0], prop, marker, report);
        }
        finally
        {
            cutscene.Free();
            pool.Free();
            textures.Dispose();
        }
    }

    private static void DriveIntro(TestContext ctx, TestWorld world, CutsceneController cutscene,
        Node3D camera, Node3D prop, Node3D marker, StringBuilder report)
    {
        var reads = new Dictionary<string, BeatRead>(StringComparer.OrdinalIgnoreCase);
        foreach (var (beat, _) in Beats)
        {
            reads[beat] = new BeatRead();
        }

        for (float t = 0f; t < DriveBudgetS; t += StepDt)
        {
            world.Runtime.Advance(StepDt);
            cutscene.Tick();
            foreach (var (beat, wingman) in Beats)
            {
                if (world.Runtime.AnimStateOf(beat) != AnimRunning)
                {
                    continue;
                }

                var read = reads[beat];
                if (read.First < 0f)
                {
                    read.First = t;
                }

                read.Last = t;
                read.Frames++;
                // Running or executed: pfighter12's only sequence dispatches its SI script and
                // ends, and the script goes on posing the prop after the instance is gone.
                int state = world.Runtime.AnimStateOf(wingman);
                if (state == AnimRunning || state == AnimExecuted)
                {
                    read.WingmanRunning++;
                }

                bool drawn = prop.IsVisibleInTree();
                var propAt = prop.GlobalPosition;
                bool posed = propAt.Length() > PosedMinM;
                float angle = AngleOffAxis(camera, propAt);
                if (drawn)
                {
                    read.PropDrawn++;
                }

                if (drawn && posed)
                {
                    read.PropPosed++;
                }

                if (drawn && posed && angle <= FrustumHalfAngleDeg)
                {
                    read.PropFramed++;
                }

                if (angle < read.BestAngle)
                {
                    read.BestAngle = angle;
                    read.PropAt = propAt;
                    read.CameraAt = camera.GlobalPosition;
                    read.MarkerAt = marker.GlobalPosition;
                }

                if (read.Frames == 1)
                {
                    read.PropStart = propAt;
                }

                read.MovedM = Mathf.Max(read.MovedM, propAt.DistanceTo(read.PropStart));
            }
        }

        foreach (var (beat, wingman) in Beats)
        {
            var read = reads[beat];
            report.AppendLine($"{beat}: ran {read.First:0.##}..{read.Last:0.##} s over {read.Frames} frame(s); "
                + $"{wingman} running or executed on {read.WingmanRunning}; prop drawn {read.PropDrawn}, posed {read.PropPosed}, "
                + $"framed {read.PropFramed}; moved {read.MovedM:0.#} m; best angle {read.BestAngle:0.#} deg "
                + $"with prop {read.PropAt}, camera {read.CameraAt}, player marker {read.MarkerAt}");
            ctx.Check(read.Frames > 0, $"the intro reached '{beat}'");
            if (read.Frames == 0)
            {
                continue;
            }

            ctx.Check(read.WingmanRunning > 0, $"'{beat}' ran its wingman definition '{wingman}'");
            ctx.Check(read.PropPosed > read.Frames / 2,
                $"the '{AircraftStage.PropNode}' prop is drawn and posed off the origin through most of '{beat}'");
            ctx.Check(read.PropFramed > read.Frames / 2,
                $"and inside the cutscene camera's frustum through most of it (best {read.BestAngle:0.#} deg off axis)");
            ctx.Check(read.MovedM > PosedMinM,
                $"and '{wingman}' flies it, {read.MovedM:0.#} m over the beat, rather than parking it");
        }
    }

    // The angle between the camera's forward axis and the ray to the point, 180 for a point
    // straight behind. Godot's camera looks down its local -Z.
    private static float AngleOffAxis(Node3D camera, Vector3 worldPoint)
    {
        var local = camera.GlobalTransform.AffineInverse() * worldPoint;
        if (local.Length() < 1e-3f)
        {
            return 0f;
        }

        return Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(-local.Normalized().Z, -1f, 1f)));
    }

    private static FlightController BuildRig(TestContext ctx, TestWorld world, ProjectilePool pool)
    {
        var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, world.Chapter));
        try
        {
            var model = new PlaneBuilder(GameZ.Load(ctx.PlanesGamezPath), textures).Build(PlaneNode);
            var rig = new FlightController
            {
                PlaneModel = model,
                Collider = PlaneCollider.Build(model),
                PlayerIndex = FlightRoster.ShooterIdBase,
                IsHumanPiloted = true,
                Projectiles = pool,
                UseKeyboard = false,
                PadDevices = Array.Empty<int>(),
                AllowPause = false,
                Team = AimAssist.PlayerTeam,
                Name = "IntroWingmanPlayer",
            };
            rig.AddChild(model);
            ctx.Host.AddChild(rig);
            rig.Setup(new FlightModel(PlaneStats.Load(ctx.ZrdrPath, PlaneNode)), null, new CamParams(),
                Vector3.Zero, Vector3.Forward, 0f, 0f);
            return rig;
        }
        finally
        {
            textures.Dispose();
        }
    }

    private sealed class BeatRead
    {
        public float First = -1f;
        public float Last = -1f;
        public int Frames;
        public int WingmanRunning;
        public int PropDrawn;
        public int PropPosed;
        public int PropFramed;
        public float BestAngle = 999f;
        public float MovedM;
        public Vector3 PropStart;
        public Vector3 PropAt;
        public Vector3 CameraAt;
        public Vector3 MarkerAt;
    }
}
