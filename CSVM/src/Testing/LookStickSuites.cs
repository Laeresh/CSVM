using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The suite over the right stick's look-around, flown through a real human rig with
/// <c>--look=</c> standing in for the stick: the chase camera and the first-person head take the
/// SAME deflection through one reader and one envelope, so both swing the same angle to the same
/// side of the aircraft, and both come back to their settled pose when the stick centres. The
/// stick is read on the camera step rather than through <c>FlightInput</c>, so this is the only
/// machine-driveable path to it. Envelope and mapping: <see cref="HeadLook"/>.</summary>
internal static class LookStickSuites
{
    // A partial deflection on purpose: a full one would pass an implementation that clamped
    // everything to the envelope's edge, and a partial one only passes a proportional map.
    private const float StickX = 0.6f;

    private const float StepDt = 1f / 60f;

    // Long enough for the head's exponential catch-up to arrive: the slower of the two decoded
    // rates is elevation's 3/s (tau ~ 0.33 s), so two seconds leaves ~0.25% of the swing.
    private const int SettleFrames = 120;

    // The chase camera writes its pose rigidly and the head arrives asymptotically, so the two
    // readings are compared at a degree rather than at float precision.
    private const float ToleranceDeg = 1.5f;

    // Where the stick's own swing has to land: StickX of the shared envelope.
    private static float ExpectedSwingDeg => StickX * HeadLook.PadLookYawMaxDeg;

    private static System.Globalization.CultureInfo Inv =>
        System.Globalization.CultureInfo.InvariantCulture;

    private static string StickArg => StickX.ToString("0.##", Inv);

    /// <summary>Flies one aircraft per view with the same pinned stick and reads what the camera
    /// did with it.</summary>
    [Suite("look-stick",
        "the right stick's look-around flown in both views with --look= standing in for the stick: the flag parses, clamps and survives a typo, and one deflection through one reader and one envelope swings the chase camera and the cockpit head the same angle to the same side of the aeroplane, each returning to its settled pose when the stick centres")]
    internal static void LookStick(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        ctx.RequireData(chapterZrdr, $"C1 zrdr");

        var spec = SessionSpec.Parse(new[] { "--fly", $"--look={StickArg},0" });
        ctx.Check(Mathf.Abs(spec.PinnedLook.X - StickX) < 1e-4f && spec.PinnedLook.Y == 0f,
            $"--look= parses into a right-stick deflection ({spec.PinnedLook.X:0.##}, {spec.PinnedLook.Y:0.##})");
        ctx.Check(SessionSpec.Parse(new[] { "--fly", "--look=9,9" }).PinnedLook.X == 1f,
            $"and clamps a past-full deflection into the stick's own [-1, 1]");
        ctx.Check(SessionSpec.Parse(new[] { "--fly", "--look=sideways" }).PinnedLook == Vector2.Zero,
            $"while a malformed pair leaves the stick centred rather than killing the run");

        var report = new StringBuilder();
        var chase = Fly(ctx, texturesPath, chapterZrdr, PilotViewMode.Chase, report);
        var cockpit = Fly(ctx, texturesPath, chapterZrdr, PilotViewMode.Cockpit, report);
        ctx.WriteArtifact("test-look-stick.txt", report.ToString());
        if (chase == null || cockpit == null)
        {
            ctx.Check(false, $"both views build an aircraft to fly");
            return;
        }

        Judge(ctx, "chase", chase.Value);
        Judge(ctx, "cockpit", cockpit.Value);
        // The request itself: one stick, one envelope, so the two views cannot answer it
        // differently. Read off the live cameras, not off the constants they were fed.
        ctx.Check(Mathf.Abs(chase.Value.SwingDeg - cockpit.Value.SwingDeg) < ToleranceDeg,
            $"the chase camera and the cockpit head swing the same angle for one stick position (chase {chase.Value.SwingDeg:0.#}°, cockpit {cockpit.Value.SwingDeg:0.#}°)");
        ctx.Note($"flew --look={StickArg},0 in both views and read the cameras it aimed");
    }

    private static void Judge(TestContext ctx, string view, in Swing swing)
    {
        ctx.Check(Mathf.Abs(swing.SwingDeg - ExpectedSwingDeg) < ToleranceDeg,
            $"{view}: a {StickX:0.##} stick swings {ExpectedSwingDeg:0.#}° of the shared envelope (read {swing.SwingDeg:0.#}°)");
        // Chase moves the CAMERA to the aircraft's right and looks back at it; the cockpit head
        // AIMS to the right. Opposite view vectors, the same side of the aeroplane, which is what
        // "the same" means between an orbit and a head.
        ctx.Check(swing.SideX > 0f,
            $"{view}: pushing the stick right puts the view on the aircraft's right (x={swing.SideX:0.###})");
        ctx.Check(swing.ReleasedDeg < ToleranceDeg,
            $"{view}: centring the stick returns the view to its settled pose (read {swing.ReleasedDeg:0.#}° off)");
    }

    // One flight: build a single human in the named view with the stick pinned, settle, read the
    // swing, then centre the stick and settle again to read what release leaves behind.
    private static Swing? Fly(TestContext ctx, string texturesPath, string chapterZrdr,
        PilotViewMode mode, StringBuilder report)
    {
        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        var pool = new ProjectilePool(textures, null, null);
        ctx.Host.AddChild(pool);
        var pane = new SubViewport();
        ctx.Host.AddChild(pane);
        var savedClock = GameClock.Current;
        var rig = new PlayerRig { Index = 0, Camera = ctx.Camera, HudParent = pane, Viewport = pane };
        FlightRoster? roster = null;
        try
        {
            var spec = SessionSpec.Parse(new[]
            {
                "--fly", $"--look={StickArg},0", $"--view={PilotView.Name(mode)}",
            });
            var rigs = new[] { rig };
            roster = BuildRoster(ctx, spec, planesGamez, textures, pool, chapterZrdr, rigs);
            roster.BuildPlayers(rigs);
            if (rig.Controller is not { } plane)
            {
                return null;
            }

            var clock = new GameClock { Mode = GameClock.RunMode.Realtime };
            GameClock.Current = clock;
            Settle(clock, plane);
            float swingDeg = SwingDegOf(mode, ctx.Camera, plane, out float sideX);
            // Release: the pin is the only stick in a headless run, so zeroing it IS letting go.
            plane.PinnedLook = Vector2.Zero;
            Settle(clock, plane);
            float releasedDeg = SwingDegOf(mode, ctx.Camera, plane, out _);
            report.AppendLine($"{PilotView.Name(mode)}: held {swingDeg:0.##}° side x={sideX:0.###}, " +
                $"released {releasedDeg:0.##}° (envelope {ExpectedSwingDeg:0.#}°)");
            return new Swing(swingDeg, sideX, releasedDeg);
        }
        finally
        {
            GameClock.Current = savedClock;
            var live = rig.Controller;
            roster?.ClearMembership();
            live?.Free();
            pane.Free();
            pool.Free();
            textures.Dispose();
        }
    }

    // The sim step and the camera step, in the order a live frame runs them: the camera reads the
    // drawn pose the sim step just left.
    private static void Settle(GameClock clock, FlightController plane)
    {
        for (int i = 0; i < SettleFrames; i++)
        {
            clock.BeginFrame(StepDt);
            plane._PhysicsProcess(StepDt);
            plane._Process(StepDt);
        }
    }

    // How far the view sits off the settled pose, about the aircraft's own up axis, and which side
    // of the aircraft it went to. Chase is read off the camera's POSITION (it orbits the plane);
    // first person off the camera's AIM (it turns in place). Both are read in the plane's frame,
    // so the aeroplane's own manoeuvring cancels out.
    private static float SwingDegOf(PilotViewMode mode, Camera3D camera, FlightController plane,
        out float sideX)
    {
        var toPlane = plane.GlobalBasis.Inverse();
        if (PilotView.IsFirstPerson(mode))
        {
            var aim = toPlane * -camera.GlobalTransform.Basis.Z;
            sideX = aim.X;
            // The nose is -Z, so this is the aim's bearing off the nose: the head's own azimuth.
            return Mathf.Abs(Mathf.RadToDeg(Mathf.Atan2(-aim.X, -aim.Z)));
        }

        var offset = toPlane * (camera.GlobalTransform.Origin - plane.WorldPosition);
        sideX = offset.X;
        // Behind is +Z, which is where the chase offset sits with the stick centred.
        return Mathf.Abs(Mathf.RadToDeg(Mathf.Atan2(offset.X, offset.Z)));
    }

    private static FlightRoster BuildRoster(TestContext ctx, SessionSpec spec, GameZ planesGamez,
        TextureArchive textures, ProjectilePool pool, string chapterZrdr, IReadOnlyList<PlayerRig> rigs)
    {
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
                ChapterZrdrPath = chapterZrdr,
            },
            new HumanRosterBindings
            {
                RigCount = rigs.Count,
                Rigs = rigs,
                PauseState = new PauseState(),
                MenuInputFor = _ => new UI.MenuInput(),
                ExitSession = () => { },
            }, new LevelStart());
    }

    // What one flight reports: how far the view swung off the settled pose while the stick was
    // held, which side of the aircraft it swung to, and how much of the swing survived release.
    private readonly record struct Swing(float SwingDeg, float SideX, float ReleasedDeg);

    // One aeroplane, airborne and level, with no world to fly into: the readings below are all
    // taken in the aircraft's own frame, so where it is does not matter, only that it flies.
    private sealed class LevelStart : IFlightStarts
    {
        public IReadOnlyList<FlightStart> ChooseStarts(IReadOnlyList<SpawnPoint>? spawns,
            string missionZrdrPath, int spawnBase, int playerCount)
        {
            var starts = new FlightStart[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                var position = new Vector3(i * 60f, 500f, 0f);
                starts[i] = new FlightStart(position, position + Vector3.Forward, 0.5f, 100f);
            }

            return starts;
        }
    }
}
