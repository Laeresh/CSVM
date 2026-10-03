using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Effects;
using CSVM.Extraction;
using CSVM.Flight.Camera;
using CSVM.Mech3;
using CSVM.Session.World;
using CSVM.Spec;
using Godot;

namespace CSVM.Testing;

/// <summary>Whether each view drawing the shared world wears the fog and the vertex light of its own
/// camera's zone, measured on drawn pixels. Two off-screen panes share one world, one camera under
/// the band and one above it, and a third stands in for a spyglass picture. The
/// single-view render is the control: with one rig the table publishes nothing and the pane must
/// draw the same bytes.</summary>
internal static class FogViewSuites
{
    private const string Chapter = "C3";

    private const string Mission = "MP1";

    private const int PaneEdge = 16;

    // Its zone1 and zone2 author a different SUNLIGHT, bearing included, either side of a band.
    private const string SunChapter = "C2";

    private const string SunMission = "MP2";

    private const int SunPaneEdge = 32;

    // Metres from each camera to its aircraft, inside every zone's fog near.
    private const float AircraftRange = 25f;

    // Byte units the aircraft's mean colour must move between the two zones' light.
    private const float MinLightShift = 8f;

    // Metres below each camera of the lit card floor every aircraft is seen against.
    private const float CardFloorDrop = 60f;

    // A lit cloud card's per-vertex term on an upward normal, through the accessor the clutter
    // cards call, at a quarter so neither zone clips.
    private const string CardShaderCode = """
        shader_type spatial;
        render_mode unshaded, cull_disabled, shadows_disabled;
        #include "res://shaders/csky_atmosphere.gdshaderinc"
        varying float v_light;
        void vertex() {
            v_light = csky_sun_vertex_light_at(vec3(0.0, 1.0, 0.0), CAMERA_POSITION_WORLD);
        }
        void fragment() {
            ALBEDO = vec3(0.25 * v_light);
        }
        """;

    // A SubViewport reads back as Rgb8, three bytes to the pixel.
    private const int Channels = 3;

    // How far an Rgb8 channel may sit from the authored fog colour. The colour goes to linear for
    // the shader and back to sRGB on the way out, which rounds.
    private const int Tolerance = 3;

    // The fog-colour test material: the atmosphere include's own accessors on a magenta surface,
    // so a fragment the fog does not cover shows at once.
    private const string ShaderCode = """
        shader_type spatial;
        render_mode unshaded, cull_disabled, shadows_disabled;
        #include "res://shaders/csky_atmosphere.gdshaderinc"
        void fragment() {
            vec3 fog_world = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
            float fog_amt = csky_fog_amount(fog_world, CAMERA_POSITION_WORLD);
            vec3 surface = vec3(1.0, 0.0, 1.0) * csky_world_light_at(CAMERA_POSITION_WORLD);
            ALBEDO = mix(surface, csky_fog_color_at(CAMERA_POSITION_WORLD), fog_amt);
        }
        """;

    [Suite("fog-per-view",
        "two splitscreen panes either side of C3/MP1's cloud band each draw the fog of their own "
        + "camera's zone, a spyglass picture standing beside the other pane's camera draws its own "
        + "pane's, and one pane alone draws the very bytes it draws beside the other")]
    internal static void FogPerView(TestContext ctx)
    {
        string zrdr = SessionPaths.MissionZrdr(ctx.DataRoot, Chapter, Mission);
        ctx.RequireData(zrdr, $"{Chapter}/{Mission} mission zrdr");
        var weatherState = WeatherState.Load(zrdr);
        ctx.Check(weatherState != null, $"{Chapter}/{Mission} carries a weather.json");
        if (weatherState == null)
        {
            return;
        }

        // The data the assertion stands on, read rather than assumed. The two zones differ in
        // colour, the band separates them, and the ground lies under both fog altitudes.
        var below = weatherState.Zone(weatherState.ZoneForState(1));
        var above = weatherState.Zone(weatherState.ZoneForState(2));
        ctx.Note($"zone1 fog {below.FogColor.ToHtml(false)} {below.FogNear:0}-{below.FogFar:0} m, zone2 fog {above.FogColor.ToHtml(false)} {above.FogNear:0}-{above.FogFar:0} m, band {weatherState.CloudBottom:0}-{weatherState.CloudTop:0} m");
        ctx.Check(!below.FogColor.IsEqualApprox(above.FogColor), $"the two zones author different fog colours");

        var world = new World3D();
        var paneA = Pane(world);
        var paneB = Pane(world);
        var disc = Pane(world);
        // Under the band, looking a degree down: the ground it sees is past both zones' fog far.
        var cameraA = Eye(new Vector3(0f, 300f, 0f), -1f);
        // Above the band's top, so no whiteout covers it, looking well down at the same ground.
        var cameraB = Eye(new Vector3(0f, weatherState.CloudTop + 500f, 0f), -45f);
        // The spyglass picture of pane B, standing a few metres from pane A's camera. There only
        // its own table entry keeps it on pane B's fog.
        var discEye = new Vector3(0f, 310f, 0f);
        var cameraDisc = Eye(discEye, -1f);
        paneA.AddChild(cameraA);
        paneB.AddChild(cameraB);
        disc.AddChild(cameraDisc);
        paneA.AddChild(Ground());
        var hudA = new Node { Name = "fog-view-hud-a" };
        var hudB = new Node { Name = "fog-view-hud-b" };
        var sun = new DirectionalLight3D { Name = "fog-view-sun" };
        // The rig's own world root, where a precipitation field would go: outside the panes' world.
        var root = new Node3D { Name = "fog-view-root" };
        var owned = new Node[] { paneA, paneB, disc, hudA, hudB, sun, root };
        foreach (var node in owned)
        {
            ctx.Host.AddChild(node);
        }
        try
        {
            var spec = SessionSpec.Parse(new[] { $"--chapter={Chapter}", $"--mission={Mission}" });
            var rigA = new PlayerRig { Index = 0, Camera = cameraA, HudParent = hudA };
            var rigB = new PlayerRig { Index = 1, Camera = cameraB, HudParent = hudB };
            var rigs = new List<PlayerRig> { rigA, rigB };
            var weather = new WeatherRig(spec, root, sun);
            weather.SetSpyglassEyes(rig => rig.Index == 1 ? discEye : null);
            weather.Build(zrdr, rigs, Array.Empty<HorizonZone>(), _ => { });
            // Both under the band first, so pane B's own edge has to carry it into zone2 below.
            // The build zone is zone2, which would otherwise hand it the right fog without one.
            var aboveBand = cameraB.Transform;
            cameraB.Transform = cameraA.Transform with { Origin = new Vector3(500f, 300f, 0f) };
            weather.Tick(rigs);
            ctx.Check(weather.FogOf(0) == weather.FogOf(1), $"two panes in one zone share one fog record");
            ctx.Same(0, FogViewTable.Views.Count, $"and publish no table, so both read the plain globals");
            var underBand = Centre(Shot(paneB));
            ctx.Check(Near(underBand, below.FogColor),
                $"pane B under the band draws zone1's fog {Bytes(below.FogColor)}, got {underBand}");
            cameraB.Transform = aboveBand;
            weather.Tick(rigs);
            ctx.Same(1, rigA.CameraWeatherState, $"pane A's camera under the band is in weather state 1");
            ctx.Same(2, rigB.CameraWeatherState, $"pane B's camera above it is in weather state 2");
            ctx.Check(weather.FogOf(0) != weather.FogOf(1), $"the two rigs hold different fog records");
            ctx.Same(3, FogViewTable.Views.Count, $"the table holds both panes and the picture");
            ctx.Same(2, FogViewTable.Nearest(FogViewTable.Views, discEye),
                $"the picture's eye matches its own entry, not pane A's camera beside it");

            byte[] a = Shot(paneA);
            byte[] b = Shot(paneB);
            byte[] d = Shot(disc);
            var pixelA = Centre(a);
            var pixelB = Centre(b);
            var pixelD = Centre(d);
            ctx.Note($"pane A {pixelA}, pane B {pixelB}, picture {pixelD}");
            ctx.Check(Near(pixelA, below.FogColor), $"pane A draws zone1's fog {Bytes(below.FogColor)}, got {pixelA}");
            ctx.Check(Near(pixelB, above.FogColor), $"pane B draws zone2's fog {Bytes(above.FogColor)}, got {pixelB}");
            ctx.Check(Near(pixelD, above.FogColor),
                $"pane B's picture draws zone2's fog beside pane A's camera {Bytes(above.FogColor)}, got {pixelD}");

            // The control: one rig, so every view agrees and the shaders read the plain globals.
            var alone = new List<PlayerRig> { rigA };
            weather.SetSpyglassEyes(_ => null);
            weather.Tick(alone);
            ctx.Same(0, FogViewTable.Views.Count, $"one view publishes no table");
            byte[] single = Shot(paneA);
            ctx.Same(0L, Differing(a, single), $"pane A alone draws the bytes it drew beside pane B");
        }
        finally
        {
            FogViewTable.Clear();
            foreach (var node in owned)
            {
                node.QueueFree();
            }
        }
    }

    [Suite("sun-per-view",
        "two splitscreen panes either side of C2/MP2's cloud band shade the faithful aircraft and a lit "
        + "card by the SUNLIGHT of their own camera's zone, a spyglass picture beside the other pane's camera by its "
        + "own pane's, and each pane draws the bytes it draws as the only view")]
    internal static void SunPerView(TestContext ctx)
    {
        string zrdr = SessionPaths.MissionZrdr(ctx.DataRoot, SunChapter, SunMission);
        ctx.RequireData(zrdr, $"{SunChapter}/{SunMission} mission zrdr");
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, SunChapter);
        ctx.RequireData(texturesPath, $"{SunChapter} textures");
        var weatherState = WeatherState.Load(zrdr);
        ctx.Check(weatherState is { HasCloudBand: true }, $"{SunChapter}/{SunMission} carries a weather.json with a cloud band");
        if (weatherState is not { HasCloudBand: true })
        {
            return;
        }

        // The data the assertion stands on: the two zones author a different SUNLIGHT, bearing included.
        var below = weatherState.Zone(weatherState.ZoneForState(1));
        var above = weatherState.Zone(weatherState.ZoneForState(2));
        ctx.Note($"zone1 SUNLIGHT {below.SunAmbient:0.##}/{below.SunDiffuse:0.##} pitch {Mathf.RadToDeg(below.SunOrientation.X):0}, zone2 {above.SunAmbient:0.##}/{above.SunDiffuse:0.##} pitch {Mathf.RadToDeg(above.SunOrientation.X):0}, band {weatherState.CloudBottom:0}-{weatherState.CloudTop:0} m");
        ctx.Check(below.SunOrientation != above.SunOrientation && below.SunAmbient != above.SunAmbient,
            $"the two zones author a different SUNLIGHT bearing and ambient");

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        var world = new World3D();
        var paneA = Pane(world, SunPaneEdge);
        var paneB = Pane(world, SunPaneEdge);
        var disc = Pane(world, SunPaneEdge);
        // Each camera looks down at its own aircraft from the same offset, so only the light differs.
        var eyeA = new Vector3(0f, MathF.Max(weatherState.CloudBottom - 500f, 100f), 0f);
        var eyeB = new Vector3(0f, weatherState.CloudTop + 500f, 0f);
        var cameraA = AircraftEye(eyeA);
        var cameraB = AircraftEye(eyeB);
        // Pane B's spyglass picture, two metres from pane A's camera and framing pane A's aircraft.
        var discEye = eyeA + new Vector3(2f, 0f, 0f);
        var cameraDisc = AircraftEye(discEye);
        paneA.AddChild(cameraA);
        paneB.AddChild(cameraB);
        disc.AddChild(cameraDisc);
        var builder = new PlaneBuilder(planesGamez, textures);
        var aircraftA = builder.Build(ctx.PlaneName);
        var aircraftB = builder.Build(ctx.PlaneName);
        aircraftA.Position = eyeA + (cameraA.Transform.Basis * new Vector3(0f, 0f, -AircraftRange));
        aircraftB.Position = eyeB + (cameraB.Transform.Basis * new Vector3(0f, 0f, -AircraftRange));
        paneA.AddChild(aircraftA);
        paneB.AddChild(aircraftB);
        paneA.AddChild(CardFloor(eyeA.Y - CardFloorDrop));
        paneB.AddChild(CardFloor(eyeB.Y - CardFloorDrop));
        var hudA = new Node { Name = "sun-view-hud-a" };
        var hudB = new Node { Name = "sun-view-hud-b" };
        var sun = new DirectionalLight3D { Name = "sun-view-sun" };
        var root = new Node3D { Name = "sun-view-root" };
        var owned = new Node[] { paneA, paneB, disc, hudA, hudB, sun, root };
        foreach (var node in owned)
        {
            ctx.Host.AddChild(node);
        }
        try
        {
            int faithful = WorldAndToolSuites.ShadersUnder(aircraftA)
                .Count(s => s.Code.Contains("csky_sun_rgb_at(CAMERA_POSITION_WORLD + light_origin", StringComparison.Ordinal));
            ctx.Check(faithful > 0, $"the aircraft draws the faithful per-vertex sun, through the per-view accessor shaders={faithful}");

            var spec = SessionSpec.Parse(new[] { $"--chapter={SunChapter}", $"--mission={SunMission}" });
            var rigA = new PlayerRig { Index = 0, Camera = cameraA, HudParent = hudA };
            var rigB = new PlayerRig { Index = 1, Camera = cameraB, HudParent = hudB };
            var weather = new WeatherRig(spec, root, sun);
            weather.SetSpyglassEyes(rig => rig.Index == 1 ? discEye : null);
            weather.Build(zrdr, new List<PlayerRig> { rigA, rigB }, Array.Empty<HorizonZone>(), _ => { });

            // Both panes in the flown zones, then their shots under the table.
            var both = new List<PlayerRig> { rigA, rigB };
            var aboveBand = cameraB.Transform;
            cameraB.Transform = cameraA.Transform with { Origin = eyeA + new Vector3(500f, 0f, 0f) };
            weather.Tick(both);
            cameraB.Transform = aboveBand;
            weather.Tick(both);
            ctx.Same(1, rigA.CameraWeatherState, $"pane A's camera under the band is in weather state 1");
            ctx.Same(2, rigB.CameraWeatherState, $"pane B's camera above it is in weather state 2");
            ctx.Check(weather.SunOf(0) != weather.SunOf(1), $"the two rigs hold different vertex light");
            ctx.Same(3, FogViewTable.Views.Count, $"the table holds both panes and the picture");
            ctx.Same(2, FogViewTable.Nearest(FogViewTable.Views, discEye),
                $"the picture's eye matches its own entry, not pane A's camera beside it");
            byte[] a2 = Shot(paneA);
            byte[] b2 = Shot(paneB);
            byte[] d2 = Shot(disc);

            // Pane A as the only view: the plain globals, lit by its own zone as player 1.
            weather.SetSpyglassEyes(_ => null);
            weather.Tick(new List<PlayerRig> { rigA });
            ctx.Same(0, FogViewTable.Views.Count, $"pane A alone publishes no table");
            byte[] a1 = Shot(paneA);

            // Pane B as the only view, its picture beside it: player 1 now, so its zone lights the plain globals.
            weather.SetSpyglassEyes(rig => rig.Index == 1 ? discEye : null);
            weather.Tick(new List<PlayerRig> { rigB });
            ctx.Same(0, FogViewTable.Views.Count, $"pane B and its own picture publish no table");
            ctx.Check(weather.FogGlobals == weather.FogOf(1), $"the plain globals follow pane B once it is the first rig");
            byte[] b1 = Shot(paneB);
            byte[] d1 = Shot(disc);

            var meanA = AircraftMean(a2);
            var meanB = AircraftMean(b2);
            ctx.Note($"aircraft mean colour: pane A {meanA}, pane B {meanB}; card floor A {Corner(a2)}, B {Corner(b2)}");
            ctx.Check(meanA.Pixels > 0 && meanB.Pixels > 0, $"each pane frames its aircraft A={meanA.Pixels} B={meanB.Pixels} pixels");
            float shift = MathF.Max(MathF.Abs(meanA.R - meanB.R), MathF.Max(MathF.Abs(meanA.G - meanB.G), MathF.Abs(meanA.B - meanB.B)));
            ctx.Check(shift >= MinLightShift, $"the two zones' SUNLIGHT shade the aircraft visibly differently shift={shift:0.0}");
            ctx.Same(0L, Differing(a2, a1), $"pane A beside pane B draws the bytes it draws alone");
            ctx.Same(0L, Differing(b2, b1), $"pane B beside pane A draws the bytes it draws as player 1");
            ctx.Same(0L, Differing(d2, d1), $"pane B's picture beside pane A's camera draws the bytes it draws beside its own");
            ctx.Check(a2.Length >= Channels && b2.Length >= Channels && (a2[0], a2[1], a2[2]) != (b2[0], b2[1], b2[2]),
                $"the lit card floor behind each aircraft takes its own pane's light A={Corner(a2)} B={Corner(b2)}");
        }
        finally
        {
            FogViewTable.Clear();
            foreach (var node in owned)
            {
                node.QueueFree();
            }
        }
    }

    private static SubViewport Pane(World3D world, int edge = PaneEdge) => new()
    {
        Size = new Vector2I(edge, edge),
        World3D = world,
        RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        RenderTargetClearMode = SubViewport.ClearMode.Always,
    };

    private static Camera3D Eye(Vector3 at, float pitchDegrees) => new()
    {
        Fov = 10f,
        Near = 1f,
        Far = 100000f,
        Current = true,
        Transform = new Transform3D(new Basis(Vector3.Right, Mathf.DegToRad(pitchDegrees)), at),
    };

    // Looking 30 degrees down, near enough to an aircraft that no zone's fog reaches it.
    private static Camera3D AircraftEye(Vector3 at) => new()
    {
        Fov = 40f,
        Near = 0.5f,
        Far = 1000f,
        Current = true,
        Transform = new Transform3D(new Basis(Vector3.Right, Mathf.DegToRad(-30f)), at),
    };

    // A level floor under one camera, wide enough to fill every pixel its aircraft leaves.
    private static MeshInstance3D CardFloor(float y) => new()
    {
        Mesh = new PlaneMesh { Size = new Vector2(4000f, 4000f) },
        MaterialOverride = new ShaderMaterial { Shader = new Shader { Code = CardShaderCode } },
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        Position = new Vector3(0f, y, 0f),
    };

    private static (int R, int G, int B) Corner(byte[] pixels) =>
        pixels.Length < Channels ? (-1, -1, -1) : (pixels[0], pixels[1], pixels[2]);

    // The mean colour of the pixels that differ from the corner's card floor, which only the
    // aircraft covers.
    private static (int Pixels, float R, float G, float B) AircraftMean(byte[] pixels)
    {
        if (pixels.Length < Channels)
        {
            return (0, 0f, 0f, 0f);
        }
        int count = 0;
        long r = 0, g = 0, b = 0;
        for (int i = 0; i + Channels <= pixels.Length; i += Channels)
        {
            if (pixels[i] == pixels[0] && pixels[i + 1] == pixels[1] && pixels[i + 2] == pixels[2])
            {
                continue;
            }
            count++;
            r += pixels[i];
            g += pixels[i + 1];
            b += pixels[i + 2];
        }
        return count == 0 ? (0, 0f, 0f, 0f) : (count, r / (float)count, g / (float)count, b / (float)count);
    }

    // A ground plane wide enough that every eye's centre ray lands on it.
    private static MeshInstance3D Ground() => new()
    {
        Mesh = new PlaneMesh { Size = new Vector2(400000f, 400000f) },
        MaterialOverride = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } },
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
    };

    // The pane's pixels after a forced draw, transforms pushed first (see ClutterCardDepthSuites).
    private static byte[] Shot(SubViewport pane)
    {
        Settle(pane);
        RenderingServer.ForceDraw();
        RenderingServer.ForceDraw();
        var img = pane.GetTexture()?.GetImage();
        return img == null || img.IsEmpty() ? Array.Empty<byte>() : img.GetData();
    }

    private static void Settle(Node node)
    {
        if (node is Node3D spatial)
        {
            spatial.ForceUpdateTransform();
        }
        for (int i = 0; i < node.GetChildCount(); i++)
        {
            Settle(node.GetChild(i));
        }
    }

    private static (int R, int G, int B) Centre(byte[] pixels)
    {
        int i = (((PaneEdge / 2) * PaneEdge) + (PaneEdge / 2)) * Channels;
        return pixels.Length < i + Channels ? (-1, -1, -1) : (pixels[i], pixels[i + 1], pixels[i + 2]);
    }

    private static (int R, int G, int B) Bytes(Color authored) =>
        (Mathf.RoundToInt(authored.R * 255f), Mathf.RoundToInt(authored.G * 255f), Mathf.RoundToInt(authored.B * 255f));

    private static bool Near((int R, int G, int B) pixel, Color authored)
    {
        var want = Bytes(authored);
        return Math.Abs(pixel.R - want.R) <= Tolerance && Math.Abs(pixel.G - want.G) <= Tolerance
            && Math.Abs(pixel.B - want.B) <= Tolerance;
    }

    private static long Differing(byte[] a, byte[] b)
    {
        if (a.Length == 0 || a.Length != b.Length)
        {
            return -1;
        }
        long changed = 0;
        for (int i = 0; i < a.Length; i++)
        {
            changed += a[i] != b[i] ? 1 : 0;
        }
        return changed;
    }
}
