using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CSVM.Flight.Hud;
using CSVM.Session.Launch;
using CSVM.Session.World;
using CSVM.Tooling;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The live graphics-mode switch and the live View Distance, driven through the one
/// sequence the launcher runs (<see cref="EnhancedLook.Switch"/>,
/// <see cref="EnhancedLook.ApplyViewDistance"/>) on whole flight sessions. A world switched away and
/// back must read as a fresh session in that mode. That covers the layers only one mode builds, the
/// clutter's cells and ranges, and the shader text on every material. It covers the Environment's
/// passes and the sun's shadow too. The pixels are the montage's, not this suite's.</summary>
internal static class GraphicsSwitchSuites
{
    // Physics steps between a switch and the reading. The world lights free or grow their omni pool
    // on a commit, so a reading taken before one would show the pool the switch left.
    private const int Steps = 3;

    [Suite("graphics-live-switch",
        "a whole flight session switched Enhanced to Original to Enhanced reads as a fresh Enhanced "
        + "session, and its Original half as a fresh Original one: the enhanced-only layers (scorch "
        + "field, volumetric banks, wind streaks, heat shimmer) are built or gone and the ground "
        + "shadow is the reverse, the clutter is one MultiMesh per kind on the faithful path and cells "
        + "with ranges under Enhanced, every world, clutter, cloud and streak material carries the "
        + "shader text a fresh build gives it, the rendered cloud puffs draw under Enhanced alone with "
        + "a fresh build's pool and tint, every alpha-plane texture, puffer atlas and painted skin holds the alpha depth a fresh build uploads (16 levels on the faithful path), the Environment's passes, tonemap and froxel fog and the "
        + "cockpit pass's copy match, the sun and the pass's light cast at the resolved shadow level "
        + "under Enhanced and not at all on the faithful path, and no omni is left lit there")]
    internal static void LiveSwitchRoundTrip(TestContext ctx)
    {
        RequireData(ctx);
        bool wasEnhanced = GraphicsMode.Enhanced;
        try
        {
            ViewDistance.Set(null);
            // One session at a time. The shader caches are process-wide, so a second session open
            // beside the first would read the text the first one's mode wrote.
            var original = Open(ctx, enhanced: false);
            Reading freshOriginal;
            try
            {
                if (!original.Built)
                {
                    ctx.Check(false, $"the Original session builds");
                    return;
                }
                freshOriginal = Read(original);
            }
            finally
            {
                original.Close();
            }

            var enhanced = Open(ctx, enhanced: true);
            try
            {
                if (!enhanced.Built)
                {
                    ctx.Check(false, $"the Enhanced session builds");
                    return;
                }

                var freshEnhanced = Read(enhanced);
                Switch(enhanced, false);
                var switchedOriginal = Read(enhanced);
                Switch(enhanced, true);
                var roundTrip = Read(enhanced);

                Same(ctx, "Original after a switch from Enhanced", freshOriginal, switchedOriginal);
                Same(ctx, "Enhanced after a round trip through Original", freshEnhanced, roundTrip);
                ctx.Check(freshEnhanced.Layers != freshOriginal.Layers && freshEnhanced.Shaders != freshOriginal.Shaders,
                    $"ABLE-TO-FAIL CONTROL: the two modes' fresh readings differ ({freshEnhanced.Layers} against {freshOriginal.Layers})");
                ctx.Check(freshOriginal.AlphaLevels is > 0 and <= 16 && freshEnhanced.AlphaLevels > 16,
                    $"ABLE-TO-FAIL CONTROL: the alpha-plane textures hold 16 alpha levels at most on the faithful path and more under Enhanced ({freshOriginal.AlphaLevels} against {freshEnhanced.AlphaLevels})");
                ctx.Check(freshEnhanced.Puffs.Length > 0 && freshOriginal.Puffs.Length == 0,
                    $"the rendered cloud puffs draw under Enhanced alone ({freshEnhanced.Puffs})");
                ctx.Check(freshEnhanced.SunCasts && !freshOriginal.SunCasts && !switchedOriginal.SunCasts,
                    $"the sun casts under Enhanced and not on the faithful path, switched or fresh");
                ctx.Check(switchedOriginal.Omnis == 0 && freshOriginal.Omnis == 0,
                    $"and no world omni stays lit on the faithful path ({switchedOriginal.Omnis} switched, {freshOriginal.Omnis} fresh)");
                ctx.WriteArtifact("test-graphics-live-switch.txt",
                    $"fresh original\n{freshOriginal}\nswitched original\n{switchedOriginal}\nfresh enhanced\n{freshEnhanced}\nround trip\n{roundTrip}\n");
            }
            finally
            {
                enhanced.Close();
            }
        }
        finally
        {
            Restore(wasEnhanced);
        }
    }

    [Suite("graphics-view-distance",
        "the Enhanced clutter cells follow a live View Distance change: moved from Normal to Very Far "
        + "the cells are cut and ranged as a session built at Very Far cuts them, farther than at "
        + "Normal, Unlimited leaves no cell a visibility range, and back at Normal the cells are the "
        + "first build's again")]
    internal static void ViewDistanceCells(TestContext ctx)
    {
        RequireData(ctx);
        bool wasEnhanced = GraphicsMode.Enhanced;
        try
        {
            ViewDistance.Set("veryfar");
            var built = Open(ctx, enhanced: true);
            CellReading veryFarBuilt;
            try
            {
                if (!built.Built)
                {
                    ctx.Check(false, $"the Very Far session builds");
                    return;
                }
                veryFarBuilt = Cells(built);
            }
            finally
            {
                built.Close();
            }

            ViewDistance.Set("normal");
            var live = Open(ctx, enhanced: true);
            try
            {
                if (!live.Built)
                {
                    ctx.Check(false, $"the Normal session builds");
                    return;
                }

                var normal = Cells(live);
                EnhancedLook.ApplyViewDistance("veryfar", live.Session);
                var veryFarLive = Cells(live);
                EnhancedLook.ApplyViewDistance("unlimited", live.Session);
                var unlimited = Cells(live);
                EnhancedLook.ApplyViewDistance("normal", live.Session);
                var normalAgain = Cells(live);

                ctx.Check(normal.Ranged > 0, $"the Normal build ranges its cells ({normal})");
                ctx.Check(veryFarLive.Text == veryFarBuilt.Text,
                    $"a live move to Very Far cuts the cells a Very Far build cuts ({veryFarLive} against {veryFarBuilt})");
                ctx.Check(veryFarLive.MaxRange > normal.MaxRange,
                    $"and reaches farther than Normal ({veryFarLive.MaxRange:0} m against {normal.MaxRange:0} m)");
                ctx.Check(unlimited.Ranged == 0 && unlimited.Cells > 0,
                    $"Unlimited leaves no cell a range ({unlimited})");
                ctx.Check(normalAgain.Text == normal.Text,
                    $"and back at Normal the cells are the first build's ({normalAgain} against {normal})");
            }
            finally
            {
                live.Close();
            }
        }
        finally
        {
            Restore(wasEnhanced);
        }
    }

    // The process back on the mode and fade the suite found, its shaders' text included.
    private static void Restore(bool wasEnhanced)
    {
        GraphicsMode.Set(wasEnhanced);
        ViewDistance.Set(null);
        Mech3.SceneBuilder.RegenerateShaders();
        EffectsLevel.RegisteredScaleSq = EnhancedLook.ClutterFadeScaleSq();
        RenderingServer.GlobalShaderParameterSet(EffectsLevel.ShaderParam, EffectsLevel.RegisteredScaleSq);
    }

    private static void RequireData(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, ctx.Chapter), $"{ctx.Chapter} textures");
        ctx.RequireData(SessionPaths.ChapterGamez(ctx.DataRoot, ctx.Chapter), $"{ctx.Chapter} gamez");
    }

    // The switch as the launcher makes it, then the steps a reading waits for.
    private static void Switch(Rig rig, bool enhanced)
    {
        EnhancedLook.Switch(enhanced, rig.Sun, rig.Env, EnhancedPasses.None, det: true, rig.Session, "graphics-live-switch");
        Step(rig);
    }

    private static void Step(Rig rig)
    {
        for (int i = 0; i < Steps; i++)
            rig.Session._PhysicsProcess(GameClock.FixedDt);
    }

    // One flight session in its own pane, lit as the launcher lights one. The sun and the
    // Environment are the launcher's, dressed by EnhancedLook when the session opens under Enhanced.
    private static Rig Open(TestContext ctx, bool enhanced)
    {
        GraphicsMode.Set(enhanced);
        Mech3.SceneBuilder.RegenerateShaders();
        EffectsLevel.RegisteredScaleSq = EnhancedLook.ClutterFadeScaleSq();
        RenderingServer.GlobalShaderParameterSet(EffectsLevel.ShaderParam, EffectsLevel.RegisteredScaleSq);
        var spec = SessionSpec.Parse(new[] { $"--chapter={ctx.Chapter}", "--players=1", "--mute", "--no-pads" });
        var pane = new SubViewport
        {
            Size = new Vector2I(640, 480),
            OwnWorld3D = true,
            World3D = new World3D(),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        var camera = new Camera3D { Fov = 60f, Far = 20000f };
        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-45, 150, 0),
            LightEnergy = WeatherRig.DefaultEnergies.Sun,
            ShadowEnabled = false,
        };
        if (enhanced)
            EnhancedLook.ApplySun(sun, true, EnhancedPasses.None);
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = new ProceduralSkyMaterial() },
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Colors.White,
            AmbientLightEnergy = WeatherRig.DefaultEnergies.Ambient,
        };
        if (enhanced)
            EnhancedLook.ApplyEnvironment(env, true, EnhancedPasses.None);
        pane.AddChild(camera);
        pane.AddChild(sun);
        ctx.Host.AddChild(pane);
        var session = new GameSession(spec, new LauncherContext
        {
            RepoRoot = ctx.RepoRoot,
            DataRoot = ctx.DataRoot,
            PlanesGamezPath = ctx.PlanesGamezPath,
            ZrdrPath = ctx.ZrdrPath,
            SoundsPath = ctx.SoundsPath,
            InterpPath = ctx.InterpPath,
            MessagesPath = ctx.MessagesPath,
            RofPath = System.IO.Path.Combine(ctx.DataRoot, "extracted", "rof"),
            ProbeRunner = new ProbeRunner(ctx.RepoRoot, ctx.DataRoot, ctx.ZrdrPath, ctx.SoundsPath,
                ctx.InterpPath, ctx.MessagesPath, ctx.PlanesGamezPath),
            CaptureDirector = new CaptureDirector(spec),
            MasterSeed = 1,
            Camera = camera,
            Orbit = new UI.Overlays.OrbitCamera(camera),
            Sun = sun,
            Env = env,
            MenuDriven = false,
            MenuPads = null,
            Presentation = UI.Menu.PresentationId.BuiltIn,
            ExitSession = () => { },
            RestartSession = () => { },
        });
        pane.AddChild(session);
        var rig = new Rig(pane, session, sun, env, session.StartSession());
        if (rig.Built)
            Step(rig);
        return rig;
    }

    // What one session reads as, each field a sorted, printable census so a mismatch names itself.
    private static Reading Read(Rig rig)
    {
        var layers = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var shaders = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var puffs = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var ranges = new List<string>();
        int omnis = 0, fogVolumes = 0, clutterMeshes = 0;
        CockpitOverlay? pass = null;
        Walk(rig.Session, node =>
        {
            string name = node.Name.ToString();
            if (name is "scorch_field" or "fog_volume_banks" or "wind_streaks" or "heat_shimmer" or "ground_shadows")
                layers[name] = layers.GetValueOrDefault(name) + 1;
            switch (node)
            {
                case OmniLight3D { Visible: true } omni when omni.IsInsideTree():
                    omnis++;
                    break;
                case FogVolume:
                    fogVolumes++;
                    break;
                case CockpitOverlay overlay:
                    pass = overlay;
                    break;
            }
            if (node is GeometryInstance3D geometry)
            {
                foreach (var material in Materials(geometry))
                {
                    Count(shaders, material.Shader.Code.GetHashCode().ToString("x8", CultureInfo.InvariantCulture));
                    if (material.Shader.Code.Contains("csky_cloud_puffs", StringComparison.Ordinal))
                        Count(puffs, PuffFlags(material));
                }
            }
            if (node is MultiMeshInstance3D mmi && UnderClutter(mmi))
            {
                clutterMeshes++;
                ranges.Add(mmi.VisibilityRangeEnd.ToString("0.##", CultureInfo.InvariantCulture));
            }
        });
        ranges.Sort(StringComparer.Ordinal);
        string alpha = AlphaDepth(rig, out int alphaLevels);
        return new Reading(
            Print(layers),
            $"{fogVolumes} fog volume(s), {clutterMeshes} clutter node(s), ranges {string.Join(",", ranges)}",
            Print(shaders),
            Print(puffs),
            alpha,
            alphaLevels,
            EnvFlags(rig.Env),
            SunFlags(rig.Sun),
            pass is { Env: { } passEnv } ? EnvFlags(passEnv) + " / " + (pass.Sun is { } light ? SunFlags(light) : "no light") : "no pass",
            rig.Sun.ShadowEnabled,
            omnis);
    }

    private static CellReading Cells(Rig rig)
    {
        var ranges = new List<float>();
        Walk(rig.Session, node =>
        {
            if (node is MultiMeshInstance3D mmi && UnderClutter(mmi))
                ranges.Add(mmi.VisibilityRangeEnd);
        });
        ranges.Sort();
        string text = string.Join(",", ranges.Select(r => r.ToString("0.##", CultureInfo.InvariantCulture)));
        return new CellReading(ranges.Count, ranges.Count(r => r > 0f), ranges.Count > 0 ? ranges.Max() : 0f, text);
    }

    private static bool UnderClutter(Node node)
    {
        for (var at = node.GetParent(); at != null; at = at.GetParent())
        {
            if (at.Name == "clutter")
                return true;
        }
        return false;
    }

    // Every shader material a drawn instance reaches. Two readings agree on the shader census only
    // where every material carries the text a fresh build writes.
    private static IEnumerable<ShaderMaterial> Materials(GeometryInstance3D geometry)
    {
        var found = new List<Material?> { geometry.MaterialOverride };
        if (geometry is MeshInstance3D mi && mi.Mesh is { } mesh)
        {
            for (int s = 0; s < mesh.GetSurfaceCount(); s++)
                found.Add(mi.GetSurfaceOverrideMaterial(s) ?? mesh.SurfaceGetMaterial(s));
        }
        else if (geometry is MultiMeshInstance3D { Multimesh.Mesh: { } shared })
        {
            for (int s = 0; s < shared.GetSurfaceCount(); s++)
                found.Add(shared.SurfaceGetMaterial(s));
        }
        return found.OfType<ShaderMaterial>().Where(m => m.Shader != null);
    }

    private static void Count(SortedDictionary<string, int> into, string key) =>
        into[key] = into.GetValueOrDefault(key) + 1;

    // A pooled cloud material's pool and tint, what DrawPool and CloudPuffs.Apply write on it.
    private static string PuffFlags(ShaderMaterial material) => string.Create(CultureInfo.InvariantCulture,
        $"{(material.GetShaderParameter("puff_tex").AsGodotObject() as Texture2DArray)?.GetLayers() ?? 0} layers tint {material.GetShaderParameter("puff_tint").AsColor().ToHtml()}");

    // Every alpha-plane texture's alpha histogram over its whole chain, read back from the GPU, and
    // each painted skin's and decal's alpha bytes. Both are what the renderer samples.
    private static string AlphaDepth(Rig rig, out int levels)
    {
        levels = 0;
        if (rig.Session.SessionTextures is not { } textures)
            return "no archive";
        var histogram = new long[256];
        foreach (var tex in textures.AlphaDepthFollowers)
            AddAlpha(tex.GetImage(), histogram, null);
        levels = histogram.Count(c => c > 0);
        var painted = new List<string>();
        var atlases = new List<string>();
        foreach (var atlas in Effects.Puffer.AtlasesOf(textures))
            atlases.Add(AlphaHash(atlas));
        atlases.Sort(StringComparer.Ordinal);
        foreach (var tex in Mech3.PlanePainter.LiveOver(textures).SelectMany(p => p.Painted))
            painted.Add(AlphaHash(tex));
        ulong histogramHash = 14695981039346656037UL;
        foreach (long count in histogram)
            histogramHash = (histogramHash ^ (ulong)count) * 1099511628211UL;
        return string.Create(CultureInfo.InvariantCulture,
            $"{textures.AlphaDepthFollowers.Count} texture(s) at {levels} alpha level(s), histogram {histogramHash:x16}; {atlases.Count} atlas(es): {string.Join(",", atlases)}; {painted.Count} painted: {string.Join(",", painted)}");
    }

    private static string AlphaHash(Texture2D texture)
    {
        ulong hash = 14695981039346656037UL;
        AddAlpha(texture.GetImage(), null, b => hash = (hash ^ b) * 1099511628211UL);
        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }

    private static void AddAlpha(Image? image, long[]? histogram, Action<byte>? each)
    {
        if (image == null)
            return;
        int stride = image.GetFormat() switch { Image.Format.Rgba8 => 4, Image.Format.La8 => 2, _ => 0 };
        if (stride == 0)
            return;
        var data = image.GetData();
        for (int i = stride - 1; i < data.Length; i += stride)
        {
            if (histogram != null)
                histogram[data[i]]++;
            each?.Invoke(data[i]);
        }
    }

    private static string EnvFlags(Godot.Environment env) => string.Create(CultureInfo.InvariantCulture,
        $"ssao {env.SsaoEnabled} {env.SsaoRadius:0.##}, ssr {env.SsrEnabled} {env.SsrMaxSteps}, glow {env.GlowEnabled} {env.GlowIntensity:0.##}, tonemap {env.TonemapMode} {env.TonemapAgxWhite:0.##}, froxel {env.VolumetricFogEnabled}, sky {env.Sky?.SkyMaterial?.GetType().Name}, reflected {env.ReflectedLightSource}");

    private static string SunFlags(DirectionalLight3D sun) => string.Create(CultureInfo.InvariantCulture,
        $"casts {sun.ShadowEnabled}, splits {sun.DirectionalShadowSplit1:0.###}/{sun.DirectionalShadowSplit2:0.###}/{sun.DirectionalShadowSplit3:0.###}, angular {sun.LightAngularDistance:0.##}, blur {sun.ShadowBlur:0.##}, bias {sun.ShadowBias:0.###}/{sun.ShadowNormalBias:0.###}, max {sun.DirectionalShadowMaxDistance:0}, specular {sun.LightSpecular:0.###}");

    private static string Print(SortedDictionary<string, int> counts) =>
        string.Join(" ", counts.Select(kv => $"{kv.Key}x{kv.Value}"));

    private static void Walk(Node node, Action<Node> visit)
    {
        visit(node);
        for (int i = 0, count = node.GetChildCount(); i < count; i++)
            Walk(node.GetChild(i), visit);
    }

    // Field by field, so a failure names the half that moved rather than one long line.
    private static void Same(TestContext ctx, string what, Reading fresh, Reading switched)
    {
        ctx.Check(fresh.Layers == switched.Layers, $"{what}: the mode's layers ({switched.Layers} against fresh {fresh.Layers})");
        ctx.Check(fresh.Clutter == switched.Clutter, $"{what}: the clutter's nodes and ranges ({switched.Clutter} against fresh {fresh.Clutter})");
        ctx.Check(fresh.Shaders == switched.Shaders, $"{what}: the shader text on every material");
        ctx.Check(fresh.Alpha == switched.Alpha, $"{what}: the alpha depth of every alpha-plane texture and painted skin ({switched.Alpha} against fresh {fresh.Alpha})");
        ctx.Check(fresh.Puffs == switched.Puffs, $"{what}: the rendered cloud puffs ({switched.Puffs} against fresh {fresh.Puffs})");
        ctx.Check(fresh.Env == switched.Env, $"{what}: the Environment ({switched.Env} against fresh {fresh.Env})");
        ctx.Check(fresh.Sun == switched.Sun, $"{what}: the sun ({switched.Sun} against fresh {fresh.Sun})");
        ctx.Check(fresh.Cockpit == switched.Cockpit, $"{what}: the cockpit pass ({switched.Cockpit} against fresh {fresh.Cockpit})");
    }

    private sealed record Reading(string Layers, string Clutter, string Shaders, string Puffs, string Alpha, int AlphaLevels, string Env, string Sun,
        string Cockpit, bool SunCasts, int Omnis)
    {
        public override string ToString()
        {
            var text = new StringBuilder();
            text.AppendLine($"  layers  {Layers}");
            text.AppendLine($"  clutter {Clutter}");
            text.AppendLine($"  env     {Env}");
            text.AppendLine($"  sun     {Sun}");
            text.AppendLine($"  cockpit {Cockpit}");
            text.AppendLine($"  omnis   {Omnis}");
            text.AppendLine($"  puffs   {Puffs}");
            text.AppendLine($"  alpha   {Alpha}");
            text.Append($"  shaders {Shaders}");
            return text.ToString();
        }
    }

    private sealed record CellReading(int Cells, int Ranged, float MaxRange, string Text)
    {
        public override string ToString() => string.Create(CultureInfo.InvariantCulture,
            $"{Cells} cell(s), {Ranged} ranged, farthest {MaxRange:0} m");
    }

    // One session, its pane and the lights the launcher would own for it.
    private sealed record Rig(SubViewport Pane, GameSession Session, DirectionalLight3D Sun,
        Godot.Environment Env, bool Built)
    {
        public void Close()
        {
            Session.Free();
            Pane.Free();
        }
    }
}
