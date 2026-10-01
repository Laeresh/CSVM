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

    // Every shader text a reading met, by its census key, for the mismatch artifact.
    private static readonly Dictionary<string, string> TextByKey = new(StringComparer.Ordinal);

    [Suite("graphics-live-switch",
        "a whole flight session switched Enhanced to Original to Enhanced reads as a fresh Enhanced "
        + "session, and its Original half as a fresh Original one: the enhanced-only layers (scorch "
        + "field, volumetric banks, wind streaks, heat shimmer) are built or gone and the ground "
        + "shadow is the reverse, the clutter is one MultiMesh per kind on the faithful path and cells "
        + "with ranges under Enhanced, a crater-flattened and a hidden stamp stay down through both switches with every drawn clutter buffer holding what the world last wrote, "
        + "the map edge's clutter copies carry ranges under Enhanced alone, every world, clutter, cloud and streak material carries the "
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
                var stamps = MoveStamps(enhanced);
                string movedHeld = StampsHeld(enhanced, stamps);
                Switch(enhanced, false);
                var switchedOriginal = Read(enhanced);
                string originalHeld = StampsHeld(enhanced, stamps);
                Switch(enhanced, true);
                var roundTrip = Read(enhanced);
                string roundTripHeld = StampsHeld(enhanced, stamps);
                ctx.Check(stamps != null && movedHeld.Length == 0 && originalHeld.Length == 0 && roundTripHeld.Length == 0,
                    $"a crater-flattened and a hidden stamp stay down through both switches, and every drawn clutter buffer holds every stamp as the world last wrote it ({stamps?.ToString() ?? "no clutter"}; {movedHeld}{originalHeld}{roundTripHeld})");

                Same(ctx, "Original after a switch from Enhanced", freshOriginal, switchedOriginal);
                Same(ctx, "Enhanced after a round trip through Original", freshEnhanced, roundTrip);
                WriteMismatchedTexts(ctx, freshOriginal, switchedOriginal, freshEnhanced, roundTrip);
                ctx.Check(freshEnhanced.Layers != freshOriginal.Layers && freshEnhanced.Shaders != freshOriginal.Shaders,
                    $"ABLE-TO-FAIL CONTROL: the two modes' fresh readings differ ({freshEnhanced.Layers} against {freshOriginal.Layers})");
                ctx.Check(freshOriginal.AlphaLevels is > 0 and <= 16 && freshEnhanced.AlphaLevels > 16,
                    $"ABLE-TO-FAIL CONTROL: the alpha-plane textures hold 16 alpha levels at most on the faithful path and more under Enhanced ({freshOriginal.AlphaLevels} against {freshEnhanced.AlphaLevels})");
                ctx.Check(freshEnhanced.EdgeRanged > 0 && freshOriginal.EdgeRanged == 0,
                    $"the map edge's clutter copies stop at their fade under Enhanced alone ({freshEnhanced.EdgeRanged} ranged against {freshOriginal.EdgeRanged})");
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
        + "first build's again; the map edge's clutter copies follow each move")]
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
                ctx.Check(veryFarLive.EdgeMax > normal.EdgeMax && normal.EdgeMax > 0f && unlimited.EdgeMax == 0f
                        && normalAgain.EdgeMax == normal.EdgeMax,
                    $"the map edge's clutter copies follow each move ({normal.EdgeMax:0}, {veryFarLive.EdgeMax:0}, {unlimited.EdgeMax:0}, {normalAgain.EdgeMax:0} m)");
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

    [Suite("graphics-shader-twins",
        "after the load warm-up has compiled the other mode's twin of every cache shader, a live "
        + "switch to Original and back makes no new shader and rewrites no shader's text, so Godot "
        + "compiles nothing new: it only moves each material onto the twin it already compiled, "
        + "and no drawn material is left on the other mode's twin, the cockpit gauges' own copies "
        + "and the fade twins included")]
    internal static void ShaderTwins(TestContext ctx)
    {
        RequireData(ctx);
        bool wasEnhanced = GraphicsMode.Enhanced;
        bool wasDrawn = Mech3.SceneBuilder.EnhancedDrawn;
        var rig = Open(ctx, enhanced: true);
        try
        {
            if (!rig.Built)
            {
                ctx.Check(false, $"the Enhanced session builds");
                return;
            }

            // What the launcher sets on the first Enhanced frame, which a suite never draws.
            Mech3.SceneBuilder.EnhancedDrawn = true;
            int warmed = Mech3.SceneBuilder.WarmOtherMode();
            ctx.Check(Mech3.SceneBuilder.OtherModeWarm,
                $"the warm-up leaves every cache shader with its Original twin ({warmed} made now)");
            int made = Mech3.SceneBuilder.TwinsMade;
            int rewrites = Mech3.SceneBuilder.TextRewrites;
            Switch(rig, false);
            int staleOriginal = StaleMaterials(rig);
            Switch(rig, true);
            int staleEnhanced = StaleMaterials(rig);
            ctx.Check(Mech3.SceneBuilder.TwinsMade == made,
                $"the two switches make no new shader ({Mech3.SceneBuilder.TwinsMade - made} made)");
            ctx.Check(Mech3.SceneBuilder.TextRewrites == rewrites,
                $"and rewrite no shader's text ({Mech3.SceneBuilder.TextRewrites - rewrites} rewritten)");
            ctx.Check(staleOriginal == 0 && staleEnhanced == 0,
                $"no drawn material stays on the other mode's twin ({staleOriginal} under Original, {staleEnhanced} under Enhanced)");
        }
        finally
        {
            rig.Close();
            Mech3.SceneBuilder.EnhancedDrawn = wasDrawn;
            Restore(wasEnhanced);
        }
    }

    [Suite("graphics-switch-cover",
        "a live switch under its cover runs in order: the flight is held and the cover is in the tree "
        + "before the switch runs, the switch waits for the cover's second frame, the hold stands "
        + "through a stall and a slow frame after it and drops with the cover once three frames settle; "
        + "the stall reaches the sim clock's accumulator as no step; a pause the player had up stands "
        + "after the cover drops; and with no pause state the clock is held and put back as it was")]
    internal static void SwitchCoverOrder(TestContext ctx)
    {
        var flying = CoverRun(ctx, paused: false);
        ctx.Check(flying.Order == "held,covered,work" && flying.WorkFrame == 2,
            $"the hold and the cover come first and the switch runs on the cover's second frame ({flying.Order} at frame {flying.WorkFrame})");
        ctx.Check(flying.HeldThroughStall && flying.Steps == 0,
            $"the clock stays held through the stall frames and takes no step from them ({flying.Steps} step(s))");
        ctx.Check(flying.Dropped && !flying.CoverInTree && !flying.HeldAfter && flying.StepsAfter == 1,
            $"the cover drops after three settled frames and the flight resumes one step at a time (dropped {flying.Dropped}, in tree {flying.CoverInTree}, held {flying.HeldAfter}, {flying.StepsAfter} step(s) next frame)");

        var paused = CoverRun(ctx, paused: true);
        ctx.Check(paused.Order == "held,covered,work" && paused.Dropped && paused.PausedAfter && paused.HeldAfter,
            $"over the pause sheet the same order runs, and the pause still stands after the cover drops (paused {paused.PausedAfter}, held {paused.HeldAfter})");

        foreach (bool wasHalted in new[] { false, true })
        {
            var clock = new GameClock { Mode = GameClock.RunMode.FixedAccum, Halted = wasHalted };
            var overlay = new ColorRect();
            var cover = SwitchCover.Begin(ctx.Host, overlay, null, clock, () => { }, "graphics-switch-cover");
            bool held = clock.Halted;
            while (!cover.Tick(5.0))
            {
            }
            ctx.Check(held && clock.Halted == wasHalted,
                $"with no pause state the cover holds the clock and puts it back {(wasHalted ? "halted" : "running")} (held {held}, after {clock.Halted})");
        }
    }

    // One cover over a pause state and a sim clock. It is ticked through a 3 s switch, a fast frame,
    // a 7.5 s variant build and the settled frames.
    private static CoverReading CoverRun(TestContext ctx, bool paused)
    {
        var pause = new Flight.Modes.PauseState();
        if (paused)
            pause.TryToggle(0);
        var clock = new GameClock { Mode = GameClock.RunMode.FixedAccum, Halted = pause.ClockHeld };
        var overlay = new ColorRect();
        var order = new List<string>();
        int frame = 0, workFrame = -1;
        SwitchCover? cover = null;
        cover = SwitchCover.Begin(ctx.Host, overlay, pause, clock, () =>
        {
            if (pause.ClockHeld && clock.Halted)
                order.Add("held");
            if (overlay.IsInsideTree() && cover!.Stage == SwitchCover.Phase.Showing)
                order.Add("covered");
            order.Add("work");
            workFrame = frame;
        }, "graphics-switch-cover");
        bool heldThroughStall = true;
        long stepsBefore = clock.Frame;
        foreach (double ms in new[] { 5.0, 5.0, 3000.0, 6.0, 7500.0, 5.0, 5.0, 5.0 })
        {
            frame++;
            clock.BeginFrame(ms / 1000.0);
            bool done = cover.Tick(ms);
            heldThroughStall &= done || (pause.ClockHeld && clock.Halted);
        }
        long steps = clock.Frame - stepsBefore;
        bool dropped = cover.Stage == SwitchCover.Phase.Done;
        clock.Halted = pause.ClockHeld;
        clock.BeginFrame(1.5 * GameClock.FixedDt);
        return new CoverReading(string.Join(",", order), workFrame, heldThroughStall, (int)steps, dropped,
            overlay.IsInsideTree(), pause.ClockHeld, pause.Paused, clock.Steps);
    }

    // Flattens one stamp of the largest clutter kind with a crater. Hides another through the kind's
    // index, as the activation does in flight.
    private static MovedStamps? MoveStamps(Rig rig)
    {
        if (ClutterRoot(rig.Session) is not { } root || rig.Session.Clutter?.ExportedKinds is not { } kinds)
            return null;
        var kind = kinds.Where(k => k.Instances != null).MaxBy(k => k.Instances!.InstanceCount);
        if (kind?.Instances is not { InstanceCount: > 2 } stamps)
            return null;
        int flattened = 0, hidden = stamps.InstanceCount / 2;
        var at = root.GlobalTransform * stamps.GetInstanceTransform(flattened).Origin;
        int killed = Mech3.ClutterCull.Destroy(root, Mech3.CraterShape.At(at, radius: 0.01f));
        var placed = stamps.GetInstanceTransform(hidden);
        stamps.SetInstanceTransform(hidden, new Transform3D(placed.Basis.Scaled(Vector3.Zero), placed.Origin));
        return new MovedStamps(kind, flattened, hidden, killed);
    }

    // What disagrees between the moved stamps, the kinds' indices and the drawn clutter buffers read
    // straight back from the renderer; empty when nothing does.
    private static string StampsHeld(Rig rig, MovedStamps? moved)
    {
        if (moved == null || ClutterRoot(rig.Session) is not { } root)
            return "no clutter; ";
        var problems = new StringBuilder();
        var stamps = moved.Kind.Instances!;
        if (stamps.GetInstanceTransform(moved.Flattened).Basis.Determinant() != 0f)
            problems.Append("the flattened stamp stands; ");
        if (stamps.GetInstanceTransform(moved.Hidden).Basis.Determinant() != 0f)
            problems.Append("the hidden stamp stands; ");
        int indexed = 0;
        foreach (var kind in rig.Session.Clutter!.ExportedKinds!)
        {
            for (int i = 0; kind.Instances != null && i < kind.Instances.InstanceCount; i++)
                indexed += kind.Instances.GetInstanceTransform(i).Basis.Determinant() == 0f ? 1 : 0;
        }
        int drawn = 0;
        Walk(root, node =>
        {
            if (node is MultiMeshInstance3D { Multimesh: { } mm })
            {
                for (int i = 0; i < mm.InstanceCount; i++)
                    drawn += mm.GetInstanceTransform(i).Basis.Determinant() == 0f ? 1 : 0;
            }
        });
        if (indexed != drawn || indexed < 2)
            problems.Append(CultureInfo.InvariantCulture, $"the indices hold {indexed} collapsed stamp(s) and the drawn buffers {drawn}; ");
        return problems.ToString();
    }

    private static Node3D? ClutterRoot(Node session)
    {
        Node3D? found = null;
        Walk(session, node => found ??= node is Node3D { Name: var name } n && name == "clutter" ? n : null);
        return found;
    }

    // Drawn materials whose cache shader is not the standing mode's twin.
    private static int StaleMaterials(Rig rig)
    {
        int stale = 0;
        Walk(rig.Session, node =>
        {
            if (node is not GeometryInstance3D geometry)
                return;
            foreach (var material in Materials(geometry))
            {
                if (Mech3.SceneBuilder.FamilyOf(material.Shader) != null
                    && !ReferenceEquals(Mech3.SceneBuilder.ForMode(material.Shader), material.Shader))
                {
                    stale++;
                }
            }
        });
        return stale;
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
        // The map edge's copies past the first corner, a window every reading of a session shares.
        // Built before the walk, so every reading's shader census meets the same nodes.
        rig.Session.EdgeExtender?.Update(rig.Session.EdgeExtender.BeyondCorner);
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
                    string key = material.Shader.Code.GetHashCode().ToString("x8", CultureInfo.InvariantCulture) + ":" + (Mech3.SceneBuilder.FamilyOf(material.Shader) ?? geometry.GetType().Name);
                    Count(shaders, key);
                    TextByKey[key] = material.Shader.Code;
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
        var edgeRanges = new List<float>();
        if (rig.Session.EdgeExtender is { } edge)
            edgeRanges.AddRange(edge.ClutterRanges());
        edgeRanges.Sort();
        string alpha = AlphaDepth(rig, out int alphaLevels);
        return new Reading(
            Print(layers),
            $"{fogVolumes} fog volume(s), {clutterMeshes} clutter node(s), ranges {string.Join(",", ranges)}; "
                + $"{edgeRanges.Count} edge clutter node(s), ranges {string.Join(",", edgeRanges.Select(r => r.ToString("0.##", CultureInfo.InvariantCulture)))}",
            edgeRanges.Count(r => r > 0f),
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
        float edgeMax = 0f;
        if (rig.Session.EdgeExtender is { } edge)
        {
            edge.Update(edge.BeyondCorner);
            edgeMax = edge.ClutterRanges().DefaultIfEmpty(0f).Max();
        }
        return new CellReading(ranges.Count, ranges.Count(r => r > 0f), ranges.Count > 0 ? ranges.Max() : 0f, text, edgeMax);
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

    // The text of every shader one reading of a pair holds and the other does not. A failure then
    // shows what differs rather than two hashes.
    private static void WriteMismatchedTexts(TestContext ctx, params Reading[] pairs)
    {
        var text = new StringBuilder();
        for (int i = 0; i + 1 < pairs.Length; i += 2)
        {
            var a = pairs[i].Shaders.Split(' ').ToHashSet(StringComparer.Ordinal);
            var b = pairs[i + 1].Shaders.Split(' ').ToHashSet(StringComparer.Ordinal);
            foreach (var entry in a.Except(b).Concat(b.Except(a)))
            {
                string key = entry[..entry.LastIndexOf('x')];
                text.AppendLine($"==== {entry} ({(a.Contains(entry) ? "fresh" : "switched")})").AppendLine(TextByKey.GetValueOrDefault(key, "?"));
            }
        }
        if (text.Length > 0)
            ctx.WriteArtifact("test-graphics-live-switch-shaders.txt", text.ToString());
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

    private sealed record Reading(string Layers, string Clutter, int EdgeRanged, string Shaders, string Puffs, string Alpha, int AlphaLevels, string Env, string Sun,
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

    private sealed record CoverReading(string Order, int WorkFrame, bool HeldThroughStall, int Steps, bool Dropped,
        bool CoverInTree, bool HeldAfter, bool PausedAfter, int StepsAfter);

    private sealed record MovedStamps(Mech3.ClutterBuilder.KindExport Kind, int Flattened, int Hidden, int Killed)
    {
        public override string ToString() => string.Create(CultureInfo.InvariantCulture,
            $"{Kind.Texture} stamp {Flattened} flattened with {Killed} killed, stamp {Hidden} hidden");
    }

    private sealed record CellReading(int Cells, int Ranged, float MaxRange, string Text, float EdgeMax)
    {
        public override string ToString() => string.Create(CultureInfo.InvariantCulture,
            $"{Cells} cell(s), {Ranged} ranged, farthest {MaxRange:0} m, edge farthest {EdgeMax:0} m");
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
