using System;
using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Extraction;
using CSVM.Flight.Camera;
using CSVM.Mech3;
using CSVM.Session.World;
using CSVM.Spec;
using Godot;

namespace CSVM.Testing;

/// <summary>Whether each view drawing the shared world wears the fog of its own camera's zone,
/// measured on drawn pixels. Two off-screen panes share one world, one camera under C3/MP1's cloud
/// band and one above it, and a third stands in for a spyglass picture. The single-view render is
/// the control: with one rig the table publishes nothing and the pane must draw the same bytes.
/// </summary>
internal static class FogViewSuites
{
    private const string Chapter = "C3";

    private const string Mission = "MP1";

    private const int PaneEdge = 16;

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

    private static SubViewport Pane(World3D world) => new()
    {
        Size = new Vector2I(PaneEdge, PaneEdge),
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
