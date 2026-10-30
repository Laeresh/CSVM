using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CSVM.Extraction;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Launch;
using CSVM.Mech3;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.Tooling;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The live graphics-mode switch, View Distance, Water Quality and Vegetation Quality on whole
/// flight sessions. Each runs through the one sequence the launcher runs (<see cref="EnhancedLook.Switch"/>,
/// <see cref="EnhancedLook.ApplyViewDistance"/>, <see cref="EnhancedLook.ApplyWaterQuality"/>,
/// <see cref="EnhancedLook.ApplyVegetationQuality"/>). A world switched away and
/// back must read as a fresh session in that mode. That covers the layers only one mode builds, the
/// clutter's cells and ranges, and the shader text on every material. It covers the Environment's
/// passes and the sun's shadow too. The pixels are the montage's, not this suite's.</summary>
internal static class GraphicsSwitchSuites
{
    // Physics steps between a switch and the reading. The world lights free or grow their omni pool
    // on a commit, so a reading taken before one would show the pool the switch left.
    private const int Steps = 3;

    // The chapter these suites build the wave ocean on. Effects.Ocean.Covers lists every chapter it covers.
    private const string OceanChapter = "C1B";

    // A sea chapter this suite opens at flat water quality, so no ocean builds over it. Its sheet still
    // carries the hide under Enhanced, so a switch a closed session left on would hole its whole sea.
    private const string FlatWaterChapter = "C2B";

    // The chapter the vegetation lab suite edits: every one of its clutter kinds is listed.
    private const string VegetationChapter = "C1";

    // C1's railway: its own polygons laid over the picked grassland and fields.
    private const string RailTexture = "gantry_tracks2.tif";

    // The mission whose freighter cruises the ocean chapter trailing its wake sheets (startanims).
    private const string WakeMission = "M03";

    // How far the lifted ocean may stand below a ramp: the bilinear lift's rounding, not a gap.
    private const float RampShortfall = 0.05f;

    // The call in the base sheet's fragment-stage discard. The include line alone never matches it.
    private const string HideCall = "csky_ocean_hides_sea(";

    // Where the hide and its mask globals are declared.
    private const string OceanIncludePath = "res://shaders/csky_ocean.gdshaderinc";

    // The retext probe's key, red under Original and green under Enhanced. The Original block is the
    // larger, so a stale compile also fails the Enhanced material's uniform buffer, as the world shaders did.
    private const string OriginalProbe = "shader_type spatial;\nrender_mode unshaded, cull_disabled;\n// graphics-retext-compiles\n"
        + "uniform vec4 probe_a = vec4(0.0);\nuniform vec4 probe_b = vec4(0.0);\nuniform vec4 probe_c = vec4(0.0);\n"
        + "void fragment() {\n    ALBEDO = vec3(1.0, 0.0, 0.0) + (probe_a.rgb + probe_b.rgb + probe_c.rgb) * 0.0;\n}\n";

    private const string EnhancedProbe = "shader_type spatial;\nrender_mode unshaded, cull_disabled;\n// graphics-retext-compiles\n"
        + "uniform vec4 probe_a = vec4(0.0);\n"
        + "void fragment() {\n    ALBEDO = vec3(0.0, 1.0, 0.0) + probe_a.rgb * 0.0;\n}\n";

    // The chapters whose base sheet rises off sea level: C2's harbour to a quay, C5's water to the
    // steinmann ship's hull.
    private static readonly string[] RampChapters = { "C2", "C5" };

    // Every shader text a reading met, by its census key, for the mismatch artifact.
    private static readonly Dictionary<string, string> TextByKey = new(StringComparer.Ordinal);

    // A 3 s switch, a fast frame, a 7.5 s variant build and the settled frames, in milliseconds.
    private static readonly double[] FastFrames = { 5.0, 5.0, 3000.0, 6.0, 7500.0, 5.0, 5.0, 5.0 };

    [Suite("graphics-live-switch",
        "a whole flight session switched Enhanced to Original to Enhanced reads as a fresh Enhanced "
        + "session, and its Original half as a fresh Original one: the enhanced-only layers (scorch "
        + "field, wind streaks, heat shimmer) are built or gone and the ground "
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

    [Suite("spyglass-sun",
        "under Enhanced the world sun sits on the layer every pane camera draws and the spyglass disc's "
        + "camera leaves out, and one shadowless copy on the disc's own layer, which no pane draws, "
        + "matches the sun's bearing, colour and energy; each disc, and no pane, carries the flat-sea "
        + "marker, and every pane, and no disc, draws the near vegetation models' layer: in a two-pane flight with one disc per pane, "
        + "at every Shadow Quality level including Off, across a zone crossing the weather rig lights, "
        + "and after a live switch to Original (copy freed, sun back on layer 1, each disc the pane's "
        + "view less its own airframe) and back; a fresh Original flight builds no copy and moves no layer")]
    internal static void SpyglassSunLayers(TestContext ctx)
    {
        RequireData(ctx);
        ZrdrSet crossZrdr = ctx.MissionZrdr("C2", "MP2");
        ctx.RequireData(crossZrdr, $"C2/MP2 mission zrdr");
        bool wasEnhanced = GraphicsMode.Enhanced;
        try
        {
            var original = Open(ctx, enhanced: false, players: 2);
            try
            {
                if (!original.Built)
                {
                    ctx.Check(false, $"the Original two-pane session builds");
                    return;
                }
                ctx.Check(original.Session.SpyglassSun == null && Count<SpyglassSun>(original.Session) == 0,
                    $"a fresh Original flight builds no spyglass sun");
                ctx.Check(original.Sun.Layers == 1u, $"and its sun stays on layer 1 (0x{original.Sun.Layers:X5})");
                DiscMasks(ctx, original, false, "fresh Original");
            }
            finally
            {
                original.Close();
            }

            var enhanced = Open(ctx, enhanced: true, players: 2);
            try
            {
                if (!enhanced.Built)
                {
                    ctx.Check(false, $"the Enhanced two-pane session builds");
                    return;
                }
                SunCopied(ctx, enhanced, "fresh Enhanced");
                DiscMasks(ctx, enhanced, true, "fresh Enhanced");
                foreach (string word in ShadowQualitySetting.Words)
                {
                    var plan = ShadowQualitySetting.PlanFor(word);
                    ShadowQualitySetting.ApplyTo(enhanced.Sun, plan, true, false, (_, _) => { });
                    var copy = enhanced.Session.SpyglassSun;
                    copy?._Process(0.0);
                    ctx.Check(copy != null && SpyglassSun.Matches(enhanced.Sun, copy) && enhanced.Sun.ShadowEnabled == plan.Cast,
                        $"Shadow Quality {word}: the copy matches the sun with no shadow of its own while the sun casts={enhanced.Sun.ShadowEnabled} ({Light(enhanced.Sun)} against {(copy != null ? Light(copy) : "no copy")})");
                }
                ShadowQualitySetting.ApplyTo(enhanced.Sun, ShadowQualitySetting.Sun, true, false, (_, _) => { });

                Switch(enhanced, false);
                ctx.Check(enhanced.Session.SpyglassSun == null && Count<SpyglassSun>(enhanced.Session) == 0,
                    $"switched to Original, the copy is freed");
                ctx.Check(enhanced.Sun.Layers == 1u, $"and the sun is back on layer 1 (0x{enhanced.Sun.Layers:X5})");
                DiscMasks(ctx, enhanced, false, "switched to Original");
                Switch(enhanced, true);
                SunCopied(ctx, enhanced, "switched back to Enhanced");
                DiscMasks(ctx, enhanced, true, "switched back to Enhanced");
            }
            finally
            {
                enhanced.Close();
            }

            ZoneCrossing(ctx, crossZrdr);
        }
        finally
        {
            Restore(wasEnhanced);
        }
    }

    [Suite("graphics-flare-switch",
        "a flat C3 flight built in Enhanced draws the world flare rig; switched live to Original it "
        + "drops that rig and draws the calibrated screen rig in its place, and switched back it draws "
        + "the world rig again, each with one flare per pane and no overlay left behind on the pane")]
    internal static void FlareFollowsSwitch(TestContext ctx)
    {
        RequireData(ctx);
        ctx.RequireData(ctx.ChapterZrdr("C3"), $"C3 zrdr");
        bool wasEnhanced = GraphicsMode.Enhanced;
        try
        {
            var rig = Open(ctx, enhanced: true, chapter: "C3", extra: "--fly");
            try
            {
                if (!rig.Built)
                {
                    ctx.Check(false, $"the Enhanced C3 session builds");
                    return;
                }

                int overlays = rig.Session.Rigs[0].WorldOverlays.Count;
                FlareRig(ctx, rig, world: true, overlays, "fresh Enhanced");
                Switch(rig, false);
                FlareRig(ctx, rig, world: false, overlays + 1, "switched to Original");
                Switch(rig, true);
                FlareRig(ctx, rig, world: true, overlays, "switched back to Enhanced");
            }
            finally
            {
                rig.Close();
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

    [Suite("graphics-water-quality",
        "the C1B wave ocean follows a live Water Quality change in an Enhanced flight: built into the "
        + "world's tree at waves, out of the tree after a live move to flat (its leaving resets the "
        + "switch the flat sheet's sea draws on), and built again by a move back to waves; a session "
        + "built at flat builds none, and --no-ocean still builds none at waves, live apply included")]
    internal static void WaterQualityOcean(TestContext ctx)
    {
        RequireData(ctx, OceanChapter);
        bool wasEnhanced = GraphicsMode.Enhanced;
        string launched = WaterQualitySetting.Word;
        try
        {
            WaterQualitySetting.Resolve(WaterQualitySetting.Waves, null, null);
            var live = Open(ctx, enhanced: true, chapter: OceanChapter);
            try
            {
                if (!live.Built)
                {
                    ctx.Check(false, $"the Enhanced C1B session builds");
                    return;
                }

                string built = OceanState(live);
                ctx.Check(live.Session.OceanBuilt && built == "1 in tree",
                    $"at waves the session builds the ocean into its tree ({built})");
                ApplyWater(live, WaterQualitySetting.Flat);
                string flat = OceanState(live);
                ctx.Check(!live.Session.OceanBuilt && flat == "0 in tree",
                    $"a live move to flat drops it and the sheet draws its sea again ({flat})");
                ApplyWater(live, WaterQualitySetting.Waves);
                string again = OceanState(live);
                ctx.Check(live.Session.OceanBuilt && again == "1 in tree",
                    $"and a move back to waves builds it again ({again})");
            }
            finally
            {
                live.Close();
            }

            WaterQualitySetting.Resolve(WaterQualitySetting.Flat, null, null);
            var flatBuild = Open(ctx, enhanced: true, chapter: OceanChapter);
            try
            {
                string none = OceanState(flatBuild);
                ctx.Check(flatBuild.Built && !flatBuild.Session.OceanBuilt && none == "0 in tree",
                    $"a session built at flat builds no ocean (built={flatBuild.Built}, {none})");
            }
            finally
            {
                flatBuild.Close();
            }

            WaterQualitySetting.Resolve(WaterQualitySetting.Waves, null, null);
            var door = Open(ctx, enhanced: true, chapter: OceanChapter, extra: new[] { "--no-ocean" });
            try
            {
                EnhancedLook.ApplyWaterQuality(door.Session);
                string skipped = OceanState(door);
                ctx.Check(door.Built && !door.Session.OceanBuilt && skipped == "0 in tree",
                    $"and --no-ocean still builds none at waves, through a live apply as well (built={door.Built}, {skipped})");
            }
            finally
            {
                door.Close();
            }
        }
        finally
        {
            WaterQualitySetting.Resolve(launched, null, null);
            Restore(wasEnhanced);
        }
    }

    [Suite("graphics-vegetation-quality",
        "the clutter's Rich vegetation follows a live Vegetation Quality change in an Enhanced flight: "
        + "on at rich, off after a live move to original and on again after a move back, off across a "
        + "live switch to Original with the word still rich and on again back in Enhanced; a live move to "
        + "original draws other transforms and no instance colour on the map and past its edge, and every "
        + "move back to Rich draws the first build's transforms and colours exactly, over the same sprite count; "
        + "a live re-bake from another seed draws other Rich stamps and one from the shipped entry the first build's")]
    internal static void VegetationQualityClutter(TestContext ctx)
    {
        RequireData(ctx);
        bool wasEnhanced = GraphicsMode.Enhanced;
        string launched = VegetationQualitySetting.Word;
        try
        {
            VegetationQualitySetting.Resolve(VegetationQualitySetting.Rich, null, null);
            var live = Open(ctx, enhanced: true);
            try
            {
                if (!live.Built || live.Session.Clutter is not { } clutter)
                {
                    ctx.Check(false, $"the Enhanced session builds with its clutter");
                    return;
                }

                int sprites = clutter.InstanceCount;
                var built = Cells(live);
                var builtDraw = DrawnStamps(live);
                ctx.Check(VegetationQualitySetting.RichOn && built.Cells > 0 && builtDraw.Colored > 0 && builtDraw.EdgeColored > 0,
                    $"a session built at rich draws the Rich variant over its clutter cells and past the map edge ({built}, {builtDraw})");
                ApplyVegetation(live, VegetationQualitySetting.Original);
                var original = DrawnStamps(live);
                ctx.Check(!VegetationQualitySetting.RichOn, $"a live move to original turns it off");
                ctx.Check(clutter.InstanceCount == sprites && original.Stamps.Count == builtDraw.Stamps.Count
                    && !original.Stamps.SequenceEqual(builtDraw.Stamps) && original.Colored == 0 && original.EdgeColored == 0,
                    $"and draws other transforms with no instance colour over the same stamps ({clutter.InstanceCount} of {sprites} sprites, {original} against {builtDraw})");
                ApplyVegetation(live, VegetationQualitySetting.Rich);
                var again = DrawnStamps(live);
                ctx.Check(VegetationQualitySetting.RichOn && Cells(live).Text == built.Text && again.Stamps.SequenceEqual(builtDraw.Stamps)
                    && again.EdgeColored == builtDraw.EdgeColored,
                    $"and a move back to rich draws the first build's transforms and colours exactly ({again})");
                var shipped = Mech3.VegetationSidecar.Load().For("C1");
                live.Session.ApplyVegetation(new Mech3.VegetationEntry { Seed = shipped.Seed + 1, Kinds = shipped.Kinds });
                Step(live);
                var reseeded = DrawnStamps(live);
                live.Session.ApplyVegetation(shipped);
                Step(live);
                var rebaked = DrawnStamps(live);
                ctx.Check(!reseeded.Stamps.SequenceEqual(builtDraw.Stamps) && rebaked.Stamps.SequenceEqual(builtDraw.Stamps)
                    && rebaked.EdgeColored == builtDraw.EdgeColored,
                    $"a live re-bake on another seed draws other Rich stamps, and one on the shipped entry draws the first build's again ({reseeded}, {rebaked})");
                Switch(live, enhanced: false);
                ctx.Check(!VegetationQualitySetting.RichOn && VegetationQualitySetting.Word == VegetationQualitySetting.Rich,
                    $"a live switch to Original turns it off with the word still {VegetationQualitySetting.Word}");
                Switch(live, enhanced: true);
                ctx.Check(VegetationQualitySetting.RichOn && Cells(live).Text == built.Text
                    && DrawnStamps(live).Stamps.SequenceEqual(builtDraw.Stamps),
                    $"and back in Enhanced it draws the first build's Rich stamps over its cells again");
            }
            finally
            {
                live.Close();
            }
        }
        finally
        {
            VegetationQualitySetting.Resolve(launched, null, null);
            Restore(wasEnhanced);
        }
    }

    [Suite("canopy-clumps",
        "the canopy clumps stand by default in a C1 Enhanced flight at rich: they stand over the canopy mask, the listed "
        + "cards' Rich material carries the handover band, and the map edge's cells copy them; a live move to original drops "
        + "the clumps, their edge copies and the band, and a move back builds the same chunks; a live switch to Original drops "
        + "them and back builds them; a lab re-bake builds new ones; the C1 clumps take the tiles shipped beside their models, "
        + "firtree1's among them, compose from the cards without them, and take a model pack's firtree1 tile under the same key "
        + "with its own tree count while the other species keep theirs, and stand no crown model; "
        + "a rich C3 flight builds them over its painted canopy alone, "
        + "fading in over their own band while the beach palms keep every distance; --canopy-clumps=off, or a session at original, builds none")]
    internal static void CanopyClumpsFollowRich(TestContext ctx)
    {
        RequireData(ctx);
        bool wasEnhanced = GraphicsMode.Enhanced;
        string launched = VegetationQualitySetting.Word;
        float shippedTrees = 0f;

        // A tile line reads "<species> in <n> slots from <source>": shipped means its own species' file beside the model.
        static bool ShippedTile(string tile) =>
            tile.EndsWith($"{Mech3.CanopyClumps.ShippedRoot}/C1/{tile[..tile.IndexOf(' ', StringComparison.Ordinal)]}.clump.png.bin", StringComparison.Ordinal);
        try
        {
            VegetationQualitySetting.Resolve(VegetationQualitySetting.Rich, null, null);
            var live = Open(ctx, true, 1, "C1");
            try
            {
                var built = Clumps(live);
                ctx.Check(live.Built && built.Count > 0 && built.Node != null && built.Node.IsInsideTree(),
                    $"a rich C1 flight builds its clumps by default ({built.Text})");
                ctx.Check(Handover(live) == "on" && EdgeClumps(live) > 0 && Handover(live, forest: false) == "off",
                    $"the cards hand over ({Handover(live)}), the grassland and hedge cards keep every distance ({Handover(live, forest: false)}), and the map edge copies the clumps ({EdgeClumps(live)} cell copies)");
                var shipped = live.Session.CanopyClumpField!;
                shippedTrees = shipped.TreesPerClump;
                ctx.Check(shipped.Models == null, $"C1's card forest stands no crown model: only painted canopy does");
                ctx.Check(shipped.Tiles.Any(t => t.StartsWith("firtree1 ", StringComparison.Ordinal)) && shipped.Tiles.All(ShippedTile),
                    $"the clumps take the firtree1 tile, and every tile is the one shipped beside its own species' model ({string.Join("; ", shipped.Tiles)})");
                var clutter = live.Session.Clutter!;
                var composed = Mech3.CanopyClumps.Build(clutter.Canopy()!, clutter.ExportedKinds!, clutter.Vegetation!, live.Session.SessionTextures!, _ => null)!;
                static byte[] Atlas(Node3D root) => ((ShaderMaterial)((MultiMeshInstance3D)root.GetChild(0)).MaterialOverride)
                    .GetShaderParameter("albedo_tex").As<Texture2D>().GetImage().GetData();
                ctx.Check(composed.Tiles.Count == 0 && !Atlas(composed.Root).AsSpan().SequenceEqual(Atlas(shipped.Root)),
                    $"without a tile they compose from the cards, another atlas ({composed.Clumps.Count} clumps at {composed.TreesPerClump:0.##} trees, against {built.Count} at {shipped.TreesPerClump:0.##})");
                composed.Root.Free();
                ApplyVegetation(live, VegetationQualitySetting.Original);
                ctx.Check(live.Session.CanopyClumps == null && !built.Node!.IsInsideTree() && EdgeClumps(live) == 0 && Handover(live) == "off",
                    $"a live move to original drops them, their edge copies and the band ({EdgeClumps(live)} copies, {Handover(live)})");
                ApplyVegetation(live, VegetationQualitySetting.Rich);
                var again = Clumps(live);
                ctx.Check(again.Count == built.Count && again.Text == built.Text && EdgeClumps(live) > 0 && Handover(live) == "on",
                    $"a move back to rich builds the same chunks and counts ({again.Text})");
                Switch(live, enhanced: false);
                ctx.Check(live.Session.CanopyClumps == null, $"a live switch to Original drops them");
                Switch(live, enhanced: true);
                var back = Clumps(live);
                ctx.Check(back.Text == built.Text, $"and a switch back to Enhanced builds them again");
                live.Session.ApplyVegetation(Mech3.VegetationSidecar.Load().For("C1"));
                Step(live);
                var rebaked = Clumps(live);
                ctx.Check(rebaked.Node != null && rebaked.Node != back.Node && rebaked.Text == built.Text && EdgeClumps(live) > 0 && Handover(live) == "on",
                    $"a lab re-bake builds new ones over the same mask ({EdgeClumps(live)} edge copies)");
            }
            finally
            {
                live.Close();
            }

            // A model pack's tile under the same key: the shipped image with other counts, so the rate moves with it.
            string pack = System.IO.Path.Combine(ctx.ScratchDir, "clump-pack");
            string packed = System.IO.Path.Combine(pack, "HDModels", "test", "C1", "firtree1.clump.png");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(packed)!);
            System.IO.File.WriteAllBytes(packed, Godot.FileAccess.GetFileAsBytes($"{Mech3.CanopyClumps.ShippedRoot}/C1/firtree1.clump.png.bin"));
            System.IO.File.WriteAllText(System.IO.Path.ChangeExtension(packed, ".json"), "{\"width_m\": 24, \"trees\": [4, 4, 4]}");
            var savedModels = HdModels.Current;
            using (var models = HdModels.Open(pack, "HDModels"))
            {
                HdModels.Current = models;
                var withPack = Open(ctx, true, 1, "C1");
                try
                {
                    var field = withPack.Session.CanopyClumpField;
                    string seen = field == null ? "no clumps" : $"{string.Join("; ", field.Tiles)} at {field.TreesPerClump:0.##} trees against {shippedTrees:0.##}";
                    ctx.Check(field != null && field.Tiles.Count(t => t.StartsWith("firtree1 ", StringComparison.Ordinal) && t.EndsWith(packed, StringComparison.Ordinal)) == 1
                        && field.Tiles.All(t => t.StartsWith("firtree1 ", StringComparison.Ordinal) || ShippedTile(t)) && field.TreesPerClump > shippedTrees,
                        $"a model pack's tile under the same key replaces the shipped one, the other species keep theirs, and its tree count sets the rate ({seen})");
                }
                finally
                {
                    withPack.Close();
                    HdModels.Current = savedModels;
                }
            }

            var c3 = Open(ctx, true, 1, "C3");
            try
            {
                var painted = Clumps(c3);
                var mask = c3.Session.Clutter?.Canopy();
                var band = (painted.Node?.GetChildren().OfType<MultiMeshInstance3D>().FirstOrDefault()?.MaterialOverride as ShaderMaterial)
                    ?.GetShaderParameter(Mech3.CanopyClumps.HandoverParam).AsVector2();
                ctx.Check(c3.Built && painted.Count > 0 && mask != null && c3.Session.CanopyClumpField!.Tiles.Count == 0,
                    $"a rich C3 flight builds clumps over its painted canopy, all composed with no tile shipped ({painted.Count})");
                ctx.Check(band == new Vector2(Mech3.CanopyClumps.PaintedNearM, Mech3.CanopyClumps.PaintedFarM) && Handover(c3) == "off",
                    $"they fade in over the painted band ({band}) and the beach palms keep every distance ({Handover(c3)})");
                int stray = painted.Node!.GetChildren().OfType<MultiMeshInstance3D>().Sum(m => Enumerable.Range(0, m.Multimesh.InstanceCount)
                    .Count(i => m.Multimesh.GetInstanceTransform(i).Origin is var o && mask!.CellAt(o.X, o.Z) is var c && (c < 0 || mask.Density[c] <= 0f)));
                ctx.Check(stray == 0, $"every clump stands in a painted cell ({stray} outside)");
            }
            finally
            {
                c3.Close();
            }

            var off = Open(ctx, true, 1, "C1", "--canopy-clumps=off");
            try
            {
                ctx.Check(off.Built && off.Session.CanopyClumps == null && EdgeClumps(off) == 0 && Handover(off) == "off",
                    $"--canopy-clumps=off builds none and leaves the cards whole ({Handover(off)})");
            }
            finally
            {
                off.Close();
            }

            VegetationQualitySetting.Resolve(VegetationQualitySetting.Original, null, null);
            var original = Open(ctx, true, 1, "C1");
            try
            {
                ctx.Check(original.Built && original.Session.CanopyClumps == null && EdgeClumps(original) == 0,
                    $"a session built at original builds none");
            }
            finally
            {
                original.Close();
            }
        }
        finally
        {
            VegetationQualitySetting.Resolve(launched, null, null);
            Restore(wasEnhanced);
        }
    }

    [Suite("ground-cover-shells",
        "the Ground Cover word's shells in a C1 Enhanced flight at rich: at off or cards no shell is built; at shells "
        + "they stand over the C1 entry's cover textures alone, never on a cleared mask texel (the landing "
        + "strips), with no collider, no shadow and no instance uniform, one MultiMesh of Layers instances a chunk ranged "
        + "inside the band; a live move to original or a switch to Original drops them and back builds the same; a live "
        + "move to cards drops them for the cards and back to shells builds the same; a session at original builds none")]
    internal static void GroundCoverShellsFollowRich(TestContext ctx)
    {
        RequireData(ctx);
        bool wasEnhanced = GraphicsMode.Enhanced;
        string launched = VegetationQualitySetting.Word, launchedCover = GroundCoverSetting.Word;
        static List<MultiMeshInstance3D> Chunks(Rig rig) =>
            rig.Session.GroundCover?.GetChildren().OfType<MultiMeshInstance3D>().Where(m => m.IsInsideTree() && !m.IsQueuedForDeletion()).ToList() ?? new();
        // A chunk is named <texture>_<i>_<j>.
        static string ChunkTexture(MultiMeshInstance3D m) => string.Join('_', m.Name.ToString().Split('_')[..^2]);
        static string Census(List<MultiMeshInstance3D> chunks) => string.Create(CultureInfo.InvariantCulture,
            $"{chunks.Count} chunks, {chunks.Sum(m => m.Multimesh.Mesh.GetFaces().Length / 3)} triangles a layer");
        try
        {
            VegetationQualitySetting.Resolve(VegetationQualitySetting.Rich, null, null);
            foreach (string word in new[] { GroundCoverSetting.Off, GroundCoverSetting.Cards })
            {
                GroundCoverSetting.Resolve(word, null, null);
                var plain = Open(ctx, true, 1, "C1");
                try
                {
                    ctx.Check(plain.Built && plain.Session.GroundCover == null, $"a rich C1 flight at {word} builds no shells");
                }
                finally
                {
                    plain.Close();
                }
            }

            GroundCoverSetting.Resolve(GroundCoverSetting.Shells, null, null);
            var live = Open(ctx, true, 1, "C1");
            try
            {
                var chunks = Chunks(live);
                string built = Census(chunks);
                var entry = live.Session.Clutter!.Vegetation!;
                ctx.Check(live.Built && chunks.Count > 0 && live.Session.GroundCoverCards == null, $"shells builds the shells and no cards ({built})");
                ctx.Check(chunks.All(m => entry.CoverFor(ChunkTexture(m)) != null),
                    $"every chunk draws a texture the entry's cover picks ({string.Join(", ", chunks.Select(ChunkTexture).Distinct())})");
                var (uniforms, _) = InstanceUniformNodes(live.Session.GroundCover!);
                float Far(MultiMeshInstance3D m) => Mech3.GroundCoverShells.Band(entry.CoverFor(ChunkTexture(m))!.Grows).Y;
                ctx.Check(uniforms == 0 && !HasCollider(live.Session.GroundCover!)
                    && chunks.All(m => m.CastShadow == GeometryInstance3D.ShadowCastingSetting.Off && m.Multimesh.InstanceCount == Mech3.GroundCoverShells.Layers
                        && m.VisibilityRangeEnd > Far(m) && m.VisibilityRangeEnd < Far(m) + Mech3.GroundCoverShells.ChunkM),
                    $"no chunk declares an instance uniform ({uniforms}), stands a collider or casts a shadow; each draws {Mech3.GroundCoverShells.Layers} layers, ranged {chunks.Min(m => m.VisibilityRangeEnd):0}-{chunks.Max(m => m.VisibilityRangeEnd):0} m");

                // Every piece's centre on the mask: none on a cleared texel.
                var mask = live.Session.Clutter!.Mask!;
                byte[] texels = mask.ToBytes();
                float texel = (mask.MaxX - mask.MinX) / mask.Side;
                byte TexelAt(Vector3 p) => texels[(Math.Clamp((int)MathF.Floor((p.Z - mask.MinZ) / texel), 0, mask.Side - 1) * mask.Side)
                    + Math.Clamp((int)MathF.Floor((p.X - mask.MinX) / texel), 0, mask.Side - 1)];
                var centres = chunks.SelectMany(m => Centres(m)).ToList();
                int cleared = centres.Count(p => TexelAt(p) == 0);
                ctx.Check(centres.Count > 0 && cleared == 0, $"no piece of {centres.Count} stands on a cleared mask texel ({cleared})");

                // The clearance is not empty of picked ground: the cut drops it.
                var gamez = live.Session.Clutter.Gamez;
                var picked = Mech3.VegetationGround.PaintedSurface(gamez, t => entry.CoverFor(t) != null);
                double bare = picked.Sum(t => Mech3.GroundCoverShells.Clip(new Vector2(t.A.At.X, t.A.At.Z), new Vector2(t.B.At.X, t.B.At.Z), new Vector2(t.C.At.X, t.C.At.Z),
                    new Vector2(mask.MinX, mask.MinZ), texel, (i, j) => i >= 0 && j >= 0 && i < mask.Side && j < mask.Side && texels[(j * mask.Side) + i] == 0).Count);
                ctx.Check(bare > 0, $"the strip clearance holds {bare:0} pieces of picked ground the shells leave bare");

                // A sample of pieces stands on a picked texture at its own height.
                int step = Math.Max(1, centres.Count / 60), off = 0;
                var sample = centres.Where((_, k) => k % step == 0).Take(60).ToList();
                foreach (var p in sample)
                {
                    bool on = Mech3.VegetationGround.LayersAt(gamez, p).Any(l => Math.Abs(l.Y - p.Y) < 0.5f && entry.CoverFor(l.Texture) != null);
                    off += on ? 0 : 1;
                }
                ctx.Check(off == 0, $"each of {sample.Count} sampled pieces lies on a picked texture at its height ({off} off)");

                // The railway laid over the picked ground takes no shell: no piece corner stands on a rail triangle,
                // where the uncut pieces stand.
                // A rail's side faces have no ground shadow, and a point in line with one would read as on it.
                var rails = Mech3.VegetationGround.Painted(gamez, tx => tx.Equals(RailTexture, StringComparison.OrdinalIgnoreCase)).Chunk(3)
                    .Where(r => MathF.Abs(new Vector2(r[1].X - r[0].X, r[1].Z - r[0].Z).Cross(new Vector2(r[2].X - r[0].X, r[2].Z - r[0].Z))) > 1e-3f).SelectMany(r => r).ToArray();
                var railLo = rails.Aggregate(rails[0], (m, v) => m.Min(v)) - (Vector3.One * 0.5f);
                var railHi = rails.Aggregate(rails[0], (m, v) => m.Max(v)) + (Vector3.One * 0.5f);
                bool OnRail(Vector3 p) => p.X >= railLo.X && p.X <= railHi.X && p.Z >= railLo.Z && p.Z <= railHi.Z && OnTriangles(rails, p);
                int cornersOnRail = chunks.Sum(m => ((ArrayMesh)m.Multimesh.Mesh).SurfaceGetArrays(0)[(int)Mesh.ArrayType.Vertex].AsVector3Array()
                    .Count(v => OnRail(m.Position + v)));
                int uncutOnRail = picked.Sum(t => Mech3.GroundCoverShells.Clip(new Vector2(t.A.At.X, t.A.At.Z), new Vector2(t.B.At.X, t.B.At.Z), new Vector2(t.C.At.X, t.C.At.Z),
                    new Vector2(mask.MinX, mask.MinZ), texel, (_, _) => true).Count(p => OnRail(p.Weights.Aggregate(Vector3.Zero, (s, w) => s + (t.A.At * w.X) + (t.B.At * w.Y) + (t.C.At * w.Z)) / p.Weights.Length)));
                ctx.Check(rails.Length > 0 && uncutOnRail > 0 && cornersOnRail == 0,
                    $"no shell stands on C1's railway ({RailTexture}, {rails.Length / 3} triangles): {cornersOnRail} piece corners on it, where {uncutOnRail} uncut pieces stand");

                ApplyVegetation(live, VegetationQualitySetting.Original);
                ctx.Check(live.Session.GroundCover == null && chunks.All(m => !m.IsInsideTree() || m.IsQueuedForDeletion()), $"a live move to original drops them");
                ApplyVegetation(live, VegetationQualitySetting.Rich);
                ctx.Check(Census(Chunks(live)) == built, $"a move back to rich builds the same ({Census(Chunks(live))})");
                Switch(live, enhanced: false);
                ctx.Check(live.Session.GroundCover == null, $"a live switch to Original drops them");
                Switch(live, enhanced: true);
                ctx.Check(Census(Chunks(live)) == built, $"and a switch back to Enhanced builds them again");
                ApplyGroundCover(live, GroundCoverSetting.Cards);
                ctx.Check(live.Session.GroundCover == null && live.Session.GroundCoverCards != null, $"a live move to cards drops the shells for the cards");
                ApplyGroundCover(live, GroundCoverSetting.Shells);
                ctx.Check(live.Session.GroundCoverCards == null && Census(Chunks(live)) == built, $"and back to shells builds the same shells");
            }
            finally
            {
                live.Close();
            }

            VegetationQualitySetting.Resolve(VegetationQualitySetting.Original, null, null);
            var original = Open(ctx, true, 1, "C1");
            try
            {
                ctx.Check(original.Built && original.Session.GroundCover == null, $"a session built at original builds none");
            }
            finally
            {
                original.Close();
            }
        }
        finally
        {
            GroundCoverSetting.Resolve(launchedCover, null, null);
            VegetationQualitySetting.Resolve(launched, null, null);
            Restore(wasEnhanced);
        }
    }

    [Suite("vegetation-models",
        "the near 3D models in a C1 Enhanced flight at rich: every listed card with a model shipped for C1 takes it and its "
        + "Rich card material the band, every other card none; no model node declares an instance uniform, and the build's "
        + "instance-uniform nodes stay under a quarter of Godot's buffer; the model cells stand outside the clutter root with "
        + "no collider, ranged inside the band's far edge, and draw exactly the card's Rich stamps; the model keeps the dither "
        + "cells the card gives up; every model cell and map edge copy draws on the models' layer alone, which no spyglass disc "
        + "draws, and a disc's camera reads the card past the band; a crater and an activation hide collapse a stamp's model with its card and a show restores "
        + "it; a live move to original and a live switch to Original draw none, and back draws the same; the map edge copies "
        + "them under rich alone; a live move to Off draws none and zeroes the card band, a move to Far ranges the same cells out to "
        + "900 m with the far band and back to Near restores them; a session at original draws none; a model pack's file under "
        + "the chapter key replaces the shipped one")]
    internal static void VegetationModelsNear(TestContext ctx)
    {
        RequireData(ctx, "C1");
        bool wasEnhanced = GraphicsMode.Enhanced;
        string launched = VegetationQualitySetting.Word;
        string launchedModels = Utils.VegetationModelsSetting.Word;
        string shipped = $"{Mech3.CanopyClumps.ShippedRoot}/C1/firtree1.glb.bin";
        try
        {
            VegetationQualitySetting.Resolve(VegetationQualitySetting.Rich, null, null);
            Utils.VegetationModelsSetting.Resolve(Utils.VegetationModelsSetting.Near, null, null);
            var live = Open(ctx, true, 1, "C1");
            try
            {
                if (!live.Built || live.Session.Clutter is not { ExportedKinds: { } kinds, ModelRoot: { } models } clutter
                    || ClutterRoot(live.Session) is not { } root)
                {
                    ctx.Check(false, $"the rich C1 session builds its clutter with a model root");
                    return;
                }
                var withModel = kinds.Where(k => k.Near != null).ToList();
                bool Ships(Mech3.ClutterBuilder.KindExport k) =>
                    Godot.FileAccess.FileExists($"{Mech3.CanopyClumps.ShippedRoot}/C1/{HdModels.NodeKey(k.Model)}.glb.bin");
                ctx.Check(withModel.Count > 0 && kinds.All(k => (k.Near != null) == (k.Rich?.Material is ShaderMaterial && Ships(k))),
                    $"every Rich card with a shipped model takes it, and no other ({string.Join(", ", withModel.Select(k => $"{k.Model}{(k.Added ? " added" : "")}"))})");
                string bands = string.Join(", ", kinds.Where(k => k.Rich?.Material is ShaderMaterial)
                    .Select(k => $"{k.Model}={((ShaderMaterial)k.Rich!.Material!).GetShaderParameter(Mech3.VegetationModels.BandParam).AsVector2()}").Distinct());
                ctx.Check(kinds.Where(k => k.Rich?.Material is ShaderMaterial).All(k => ((ShaderMaterial)k.Rich!.Material!)
                        .GetShaderParameter(Mech3.VegetationModels.BandParam).AsVector2() == (k.Near != null ? Mech3.VegetationModels.Band : Vector2.Zero)),
                    $"a modelled card's Rich material carries the band and every other card none, so it draws as before ({bands})");
                ctx.Check(!Under(models, "clutter") && models.IsInsideTree() && !HasCollider(models),
                    $"the models stand outside the clutter root, in the tree, with no collider");

                var drawn = ModelCells(models);
                float reach = Mech3.VegetationModels.FarM + Mech3.VegetationModels.CellM;
                ctx.Check(drawn.Count > 0 && drawn.All(m => m.VisibilityRangeEnd > Mech3.VegetationModels.FarM && m.VisibilityRangeEnd < reach),
                    $"{drawn.Count} model cells, each ranged past the band's far edge and short of {reach:0} m");
                var cards = withModel.SelectMany(k => k.Rich!.Placements).Select(Key).OrderBy(p => p).ToList();
                ctx.Check(ModelStamps(models).SequenceEqual(cards),
                    $"the models draw exactly the cards' Rich stamps ({cards.Count})");

                var material = (ShaderMaterial)drawn[0].MaterialOverride;
                string code = material.Shader.Code;
                string card = ((ShaderMaterial)withModel[0].Rich!.Material!).Shader.Code;
                ctx.Check(code.Contains("v_card = v_far * " + Mech3.ClutterBuilder.ModelShare, StringComparison.Ordinal)
                        && code.Contains("|| csky_clutter_dither_keep(FRAGCOORD.xy, v_card)", StringComparison.Ordinal)
                        && card.Contains("v_alpha *= " + Mech3.ClutterBuilder.ModelShare, StringComparison.Ordinal),
                    $"the card fades by the band's share and the model discards exactly the dither cells the card keeps");
                ctx.Check(drawn.All(m => m.Layers == Mech3.VegetationModels.Layer)
                        && Mech3.ClutterBuilder.ModelShare.StartsWith($"({Mech3.SceneBuilder.FlatSeaEye} ? 1.0 : ", StringComparison.Ordinal),
                    $"every model cell draws on the models' layer alone, which no spyglass disc draws, and a disc's camera reads the card past the band, so it draws whole there");

                var export = withModel[0];
                var stamps = export.Instances!;
                int victim = Enumerable.Range(0, export.Rich!.Placements.Count).First(i => export.Rich.Placements[i].Basis.Determinant() != 0f);
                var at = root.GlobalTransform * export.Rich.Placements[victim].Origin;
                int zeroBefore = ZeroModels(models);
                int killed = Mech3.ClutterCull.Destroy(root, Mech3.CraterShape.At(at, radius: 0.01f));
                int zeroCratered = ZeroModels(models);
                ctx.Check(killed >= 1 && zeroCratered == zeroBefore + killed && stamps.Moved(victim),
                    $"a crater collapses its stamps' models with their cards ({killed} killed, {zeroBefore} to {zeroCratered} collapsed models)");
                ApplyVegetation(live, VegetationQualitySetting.Original);
                ctx.Check(ModelCells(models).Count == 0, $"a live move to original draws no model");
                ApplyVegetation(live, VegetationQualitySetting.Rich);
                ctx.Check(ModelCells(models).Count == drawn.Count && ZeroModels(models) == zeroCratered,
                    $"a move back draws the same cells with the crater kept ({ModelCells(models).Count}, {ZeroModels(models)} collapsed)");
                stamps.Restore(victim);
                int zeroShown = ZeroModels(models);
                stamps.SetInstanceTransform(victim, new Transform3D(Basis.Identity.Scaled(Vector3.Zero), export.Rich.Placements[victim].Origin));
                ctx.Check(zeroShown == zeroCratered - 1 && ZeroModels(models) == zeroCratered,
                    $"a show restores the stamp's model and a hide collapses it again ({zeroShown}, {ZeroModels(models)})");
                stamps.Restore(victim);

                live.Session.EdgeExtender!.Update(live.Session.EdgeExtender.BeyondCorner);
                int edge = EdgeModels(live);
                int edgeOffLayer = 0;
                Walk(live.Session.EdgeExtender!, node =>
                {
                    if (node is MultiMeshInstance3D m && m.Name.ToString().EndsWith("_model", StringComparison.Ordinal) && m.Layers != Mech3.VegetationModels.Layer)
                        edgeOffLayer++;
                });
                ctx.Check(edgeOffLayer == 0, $"and every map edge model copy draws on that layer too ({edgeOffLayer} off it)");
                // Godot gives every node whose shader declares an instance uniform 16 slots of one global buffer.
                int capacity = ProjectSettings.GetSetting("rendering/limits/global_shader_variables/buffer_size").AsInt32() / 16;
                var (carriers, modelCarriers) = InstanceUniformNodes(live.Session);
                ctx.Note($"{drawn.Count} model cell(s) over {withModel.Count} kind(s), {edge} edge model node(s); {carriers} instance-uniform node(s) of {capacity}");
                ctx.Check(modelCarriers == 0 && carriers < capacity / 4,
                    $"no model node declares an instance uniform ({modelCarriers}), and the build's {carriers} such node(s) stay under a quarter of the buffer's {capacity}");
                Switch(live, enhanced: false);
                int edgeOriginal = EdgeModels(live);
                ctx.Check(ModelCells(models).Count == 0 && edge > 0 && edgeOriginal == 0,
                    $"the map edge copies the models under rich ({edge} nodes) and a live switch to Original draws none, there ({edgeOriginal}) or on the map");
                Switch(live, enhanced: true);
                ctx.Check(ModelCells(models).Count == drawn.Count && ModelStamps(models).SequenceEqual(cards) && EdgeModels(live) == edge,
                    $"and back in Enhanced the same cells and edge copies draw again");
                Models(live, Utils.VegetationModelsSetting.Off);
                ctx.Check(ModelCells(models).Count == 0 && EdgeModels(live) == 0 && CardBands(withModel).All(b => b == Vector2.Zero),
                    $"a live move to Off draws no model, on the map or past its edge, and every card's band is zero, so it draws as before");
                Models(live, Utils.VegetationModelsSetting.Far);
                var far = ModelCells(models);
                float farReach = Mech3.VegetationModels.FarFarM + Mech3.VegetationModels.CellM;
                ctx.Check(far.Count == drawn.Count && far.All(m => m.VisibilityRangeEnd > Mech3.VegetationModels.FarFarM && m.VisibilityRangeEnd < farReach)
                        && CardBands(withModel).All(b => b == new Vector2(Mech3.VegetationModels.FarNearM, Mech3.VegetationModels.FarFarM)) && EdgeModels(live) == edge,
                    $"a live move to Far draws the same cells ranged out past {Mech3.VegetationModels.FarFarM:0} m, with the far band on the cards");
                Models(live, Utils.VegetationModelsSetting.Near);
                ctx.Check(ModelCells(models).All(m => m.VisibilityRangeEnd < reach) && CardBands(withModel).All(b => b == Mech3.VegetationModels.Band)
                        && ModelStamps(models).SequenceEqual(cards),
                    $"and a move back to Near draws the first cells at the near band");
            }
            finally
            {
                live.Close();
            }

            VegetationQualitySetting.Resolve(VegetationQualitySetting.Original, null, null);
            var original = Open(ctx, true, 1, "C1");
            try
            {
                ctx.Check(original.Built && original.Session.Clutter?.ModelRoot is { } r && ModelCells(r).Count == 0,
                    $"a session built at original draws no model");
            }
            finally
            {
                original.Close();
            }

            // A model pack's file under the chapter key: the shipped bytes, so the import is known good.
            string pack = System.IO.Path.Combine(ctx.ScratchDir, "vegetation-model-pack");
            string packed = System.IO.Path.Combine(pack, "HDModels", "test", "C1", "firtree1.glb");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(packed)!);
            var saved = HdModels.Current;
            try
            {
                System.IO.File.WriteAllBytes(packed, Godot.FileAccess.GetFileAsBytes(shipped));
                using (var good = HdModels.Open(pack, "HDModels"))
                {
                    HdModels.Current = good;
                    var model = Mech3.VegetationModels.For("C1", "firtree1", null);
                    ctx.Check(model?.Source.EndsWith(packed, StringComparison.Ordinal) == true,
                        $"a pack's C1/firtree1.glb replaces the shipped model ({model?.Source ?? "none"})");
                    ctx.Check(Mech3.VegetationModels.For("C1", "no_such_card", null) == null && Mech3.VegetationModels.For("C5", "firtree1", null) == null,
                        $"a species or chapter with no model reads none");
                }
            }
            finally
            {
                HdModels.Current = saved;
            }
        }
        finally
        {
            Utils.VegetationModelsSetting.Resolve(launchedModels, null, null);
            VegetationQualitySetting.Resolve(launched, null, null);
            Restore(wasEnhanced);
        }
    }

    [Suite("canopy-models",
        "the near crown models over C3's painted canopy in an Enhanced flight at rich: they stand under the clumps' root, "
        + "outside the clutter root, with no collider and no instance uniform, every one on a painted point, drawn in cells "
        + "ranged past the band's far edge; the clumps keep only the dither cells the models give up over the band, the models "
        + "the rest, and the clumps stand held at one height at every distance; every crown cell draws on the models' layer alone, which no spyglass disc draws; "
        + "a live move to Off hides them, zeroes the clumps' band and lets them rise over their own, Far and Near draw the same nodes again; a live "
        + "move to original and a switch to Original draw none, and back the same crowns; a session at original draws none")]
    internal static void CanopyModelsOverPaintedCanopy(TestContext ctx)
    {
        RequireData(ctx, "C3");
        bool wasEnhanced = GraphicsMode.Enhanced;
        string launched = VegetationQualitySetting.Word;
        string launchedModels = VegetationModelsSetting.Word;
        try
        {
            VegetationQualitySetting.Resolve(VegetationQualitySetting.Rich, null, null);
            VegetationModelsSetting.Resolve(VegetationModelsSetting.Near, null, null);
            var live = Open(ctx, true, 1, "C3");
            try
            {
                if (!live.Built || live.Session.CanopyClumpField is not { Models: { } models } field || live.Session.Clutter?.Canopy() is not { } mask)
                {
                    ctx.Check(false, $"the rich C3 flight builds its clumps with crown models");
                    return;
                }
                var crowns = field.Crowns;
                var cells = ModelCells(models);
                ctx.Check(crowns.Count > 0 && cells.Sum(m => m.Multimesh.InstanceCount) == crowns.Count && models.IsInsideTree()
                        && models.GetParent() == field.Root && !UnderClutter(models) && !HasCollider(models),
                    $"{crowns.Count} crown models stand in {cells.Count} cells under the clumps' root, outside the clutter root, with no collider");
                int off = crowns.Count(c => !mask.IsPainted(c.At.X, c.At.Z) || MathF.Abs(c.At.Y - mask.SurfaceAt(c.At.X, c.At.Z)) > 0.01f);
                ctx.Check(off == 0, $"every crown stands on the painted surface ({off} off it)");
                foreach (var (name, at) in new[] { ("A7", new Vector2(-5439f, -3077f)), ("low", new Vector2(-5450f, -3550f)) })
                    ctx.Note($"{crowns.Count(c => new Vector2(c.At.X, c.At.Z).DistanceTo(at) < Mech3.CanopyClumps.ModelNearM)} crowns inside {Mech3.CanopyClumps.ModelNearM:0} m of the {name} view");
                // Every node under the crown models counts, whatever its name.
                var (uniforms, _) = InstanceUniformNodes(models);
                // A cell's half diagonal takes its height too, which a cliff stretches past half the cell's edge.
                float reach = Mech3.CanopyClumps.ModelFarM + (1.5f * Mech3.VegetationModels.CellM);
                ctx.Check(uniforms == 0 && cells.All(m => m.VisibilityRangeEnd > Mech3.CanopyClumps.ModelFarM && m.VisibilityRangeEnd < reach)
                        && cells.All(m => m.CastShadow == GeometryInstance3D.ShadowCastingSetting.Off),
                    $"no crown cell declares an instance uniform ({uniforms}), casts a shadow or ranges past {reach:0} m (ranges {cells.Min(m => m.VisibilityRangeEnd):0}-{cells.Max(m => m.VisibilityRangeEnd):0} m)");

                var model = (ShaderMaterial)cells[0].MaterialOverride;
                var clumps = (ShaderMaterial)field.Root.GetChildren().OfType<MultiMeshInstance3D>().First().MaterialOverride;
                Vector2 Band(ShaderMaterial m) => m.GetShaderParameter(Mech3.VegetationModels.BandParam).AsVector2();
                ctx.Check(Band(model) == Mech3.CanopyClumps.ModelBand && Band(clumps) == Mech3.CanopyClumps.ModelBand
                        && model.Shader.Code.Contains("v_far = 1.0;", StringComparison.Ordinal)
                        && model.Shader.Code.Contains("|| csky_clutter_dither_keep(FRAGCOORD.xy, v_card)", StringComparison.Ordinal)
                        && clumps.Shader.Code.Contains("v_model = " + Mech3.ClutterBuilder.ModelShare, StringComparison.Ordinal)
                        && clumps.Shader.Code.Contains("!csky_clutter_dither_keep(FRAGCOORD.xy, v_model)", StringComparison.Ordinal),
                    $"over {Mech3.CanopyClumps.ModelBand} the clumps keep the dither cells the models give up, and the models the rest");
                ctx.Check(cells.All(m => m.Layers == Mech3.VegetationModels.Layer),
                    $"every crown cell draws on the models' layer alone, which no spyglass disc draws, where the clumps keep every cell (ClutterBuilder.ModelShare)");
                float Held(ShaderMaterial m) => m.GetShaderParameter(Mech3.CanopyClumps.HeldParam).AsSingle();
                float heldShare = Mech3.CanopyClumps.HeldTopM / field.Clumps.Max(c => c.Height);
                ctx.Check(MathF.Abs(Held(clumps) - heldShare) < 1e-4f,
                    $"the clumps stand held at a {Mech3.CanopyClumps.HeldTopM:0.#} m top at every distance (share {Held(clumps):0.###} of {heldShare:0.###})");
                ctx.Check(clumps.Shader.Code.Contains($"float hold = {Mech3.SceneBuilder.FlatSeaEye} ? 0.0 : {Mech3.CanopyClumps.HeldParam};", StringComparison.Ordinal),
                    $"a spyglass disc, which draws no crown, reads no hold, so its clumps rise as with 3D Vegetation off");

                Models(live, VegetationModelsSetting.Off);
                ctx.Check(!models.Visible && Band(clumps) == Vector2.Zero && Held(clumps) == 0f && live.Session.CanopyClumpField?.Models == models,
                    $"a live move to Off hides the crowns, zeroes the clumps' band and lets them rise over their own, with no rebuild");
                Models(live, VegetationModelsSetting.Far);
                bool far = models.Visible && Band(clumps) == Mech3.CanopyClumps.ModelBand && Held(clumps) > 0f;
                Models(live, VegetationModelsSetting.Near);
                ctx.Check(far && models.Visible && Band(clumps) == Mech3.CanopyClumps.ModelBand && Held(clumps) > 0f && live.Session.CanopyClumpField?.Models == models,
                    $"Far and Near draw the same crowns over the same band");

                ApplyVegetation(live, VegetationQualitySetting.Original);
                ctx.Check(live.Session.CanopyClumpField == null && !models.IsInsideTree(), $"a live move to original draws no crown");
                ApplyVegetation(live, VegetationQualitySetting.Rich);
                var again = live.Session.CanopyClumpField?.Crowns;
                ctx.Check(again != null && again.SequenceEqual(crowns), $"a move back to rich stands the same crowns ({again?.Count ?? 0})");
                Switch(live, enhanced: false);
                ctx.Check(live.Session.CanopyClumpField == null, $"a live switch to Original draws none");
                Switch(live, enhanced: true);
                ctx.Check(live.Session.CanopyClumpField?.Crowns.SequenceEqual(crowns) == true && live.Session.CanopyClumpField.Models!.Visible,
                    $"and a switch back to Enhanced the same crowns");
            }
            finally
            {
                live.Close();
            }

            VegetationQualitySetting.Resolve(VegetationQualitySetting.Original, null, null);
            var original = Open(ctx, true, 1, "C3");
            try
            {
                ctx.Check(original.Built && original.Session.CanopyClumpField == null, $"a session built at original draws no crown");
            }
            finally
            {
                original.Close();
            }
        }
        finally
        {
            VegetationModelsSetting.Resolve(launchedModels, null, null);
            VegetationQualitySetting.Resolve(launched, null, null);
            Restore(wasEnhanced);
        }
    }

    [Suite("canopy-activation",
        "CM02's script-hidden islands carry no Enhanced vegetation in a C3 flight at rich: no canopy clump and no crown "
        + "model stands in a canopy cell painted only by a switched-off world node; switching those islands on builds "
        + "clumps and crowns over them, and switching them off again takes them away")]
    internal static void CanopyActivation(TestContext ctx)
    {
        RequireData(ctx, "C3");
        bool wasEnhanced = GraphicsMode.Enhanced;
        string launched = VegetationQualitySetting.Word;
        string launchedModels = VegetationModelsSetting.Word;
        try
        {
            VegetationQualitySetting.Resolve(VegetationQualitySetting.Rich, null, null);
            VegetationModelsSetting.Resolve(VegetationModelsSetting.Near, null, null);
            var live = Open(ctx, true, 1, "C3", "--mission=M02");
            try
            {
                if (!live.Built || live.Session.CanopyClumpField is not { } field || live.Session.Clutter is not { } clutter
                    || clutter.Vegetation is not { } entry || clutter.Gamez.FindByName("world1") is not { HasArea: true } w)
                {
                    ctx.Check(false, $"the rich C3 M02 flight builds its clumps");
                    return;
                }

                // The switched-off nodes read off the built world, and those holding canopy ground under them: the islands.
                Func<string, bool> canopy = t => CanopyMask.IsCanopy(entry, t);
                var holders = Mech3.VegetationGround.Holders(clutter.Gamez, canopy);
                var off = new HashSet<int>();
                var islands = new List<Node3D>();
                Walk(live.Session, node =>
                {
                    if (node is not Node3D { Visible: false } n || !n.HasMeta(AnimRuntime.IndexMeta))
                        return;
                    off.Add(n.GetMeta(AnimRuntime.IndexMeta).AsInt32());
                    bool holds = false;
                    Walk(n, d => holds |= d.HasMeta(AnimRuntime.IndexMeta) && holders.Contains(d.GetMeta(AnimRuntime.IndexMeta).AsInt32()));
                    if (holds && n.GetParent() is Node3D parent && parent.IsVisibleInTree())
                        islands.Add(n);
                });
                CanopyMask Bake(Func<GameZNode, bool>? prune) => CanopyMask.FromPainted(
                    Mech3.VegetationGround.Painted(clutter.Gamez, canopy, switchedOff: prune), w.AreaLeft, w.AreaTop, w.AreaRight, w.AreaBottom);
                var every = Bake(null);
                var shown = Bake(n => off.Contains(n.Index));
                bool OnHidden(Vector3 at) => every.CellAt(at.X, at.Z) is int c and >= 0 && every.PaintedShare(c) > 0f && shown.PaintedShare(c) == 0f;
                (int Clumps, int Crowns) Over(Mech3.CanopyClumps.Field f) => (f.Clumps.Count(c => OnHidden(c.Base)), f.Crowns.Count(c => OnHidden(c.At)));

                int hiddenCells = Enumerable.Range(0, every.Width * every.Height).Count(c => every.PaintedShare(c) > 0f && shown.PaintedShare(c) == 0f);
                ctx.Check(islands.Count > 0 && hiddenCells > 0,
                    $"CM02's script switches off {islands.Count} island(s) holding canopy, {hiddenCells} canopy cells painted by them alone");
                var (clumps, crowns) = Over(field);
                ctx.Check(clumps == 0 && crowns == 0,
                    $"at mission start no clump ({clumps} of {field.Clumps.Count}) and no crown model ({crowns} of {field.Crowns.Count}) stands over hidden canopy alone");

                foreach (var island in islands)
                    AnimRuntime.SetSubtreeActive(island, true);
                live.Session.FollowGroundSwitch();
                var on = live.Session.CanopyClumpField;
                var (clumpsOn, crownsOn) = on != null ? Over(on) : (0, 0);
                ctx.Check(clumpsOn > 0 && crownsOn > 0,
                    $"switched on, the islands carry {clumpsOn} clump(s) and {crownsOn} crown model(s) over their canopy");

                foreach (var island in islands)
                    AnimRuntime.SetSubtreeActive(island, false);
                live.Session.FollowGroundSwitch();
                var again = live.Session.CanopyClumpField;
                var (clumpsOff, crownsOff) = again != null ? Over(again) : (-1, -1);
                ctx.Check(clumpsOff == 0 && crownsOff == 0 && again!.Clumps.Count == field.Clumps.Count && again.Crowns.Count == field.Crowns.Count,
                    $"switched off again, they carry none ({clumpsOff} clumps, {crownsOff} crowns) and the field is the first build's size ({again?.Clumps.Count} clumps)");
            }
            finally
            {
                live.Close();
            }
        }
        finally
        {
            VegetationModelsSetting.Resolve(launchedModels, null, null);
            VegetationQualitySetting.Resolve(launched, null, null);
            Restore(wasEnhanced);
        }
    }

    [Suite("ground-cover-cards",
        "the Ground Cover word's cards on C1 at rich: at off nothing builds; at cards, the launch default, the cells "
        + "around a crop field, a meadow and the strip clearance's edge hold crop and grass cards on the entry's cover "
        + "textures alone (read back through the world's own layers), none on a bare mask texel, under their own root with "
        + "no collider, no shadow cast or received, no instance uniform; a live move to original and a switch to Original "
        + "drop it, and back builds it; a live move to off drops it and back to cards builds it again")]
    internal static void GroundCoverCards(TestContext ctx)
    {
        RequireData(ctx, "C1");
        bool wasEnhanced = GraphicsMode.Enhanced;
        string launched = VegetationQualitySetting.Word, launchedCover = GroundCoverSetting.Word;
        var gamez = ctx.ChapterGamez("C1")!;
        try
        {
            VegetationQualitySetting.Resolve(VegetationQualitySetting.Rich, null, null);
            GroundCoverSetting.Resolve(GroundCoverSetting.Off, null, null);
            var off = Open(ctx, true, 1, "C1");
            try
            {
                bool any = false;
                Walk(off.Session, n => any |= n.Name == "ground_cover");
                ctx.Check(off.Built && off.Session.GroundCoverCards == null && off.Session.GroundCover == null && !any, $"at off a rich C1 flight builds no ground cover");
            }
            finally
            {
                off.Close();
            }

            ctx.Check(GroundCoverSetting.Default == GroundCoverSetting.Cards, $"cards is the default word");
            GroundCoverSetting.Resolve(null, null, null);
            var live = Open(ctx, true, 1, "C1");
            try
            {
                if (!live.Built || live.Session.GroundCoverCards is not { } field || live.Session.Clutter?.Mask is not { } mask
                    || live.Session.Clutter.Vegetation is not { } entry)
                {
                    ctx.Check(false, $"the default word builds the cover on a rich C1 flight over the shipped mask");
                    return;
                }
                foreach (var (name, x, z) in new[] { ("crop field", -640f, -4482f), ("meadow", -4890f, -6018f), ("strip edge", -6090f, -4494f) })
                {
                    int bareBefore = field.Bare;
                    field.Update(new[] { new Vector3(x, 140f, z) }, int.MaxValue);
                    var cards = field.Draws.SelectMany(m => Enumerable.Range(0, m.Multimesh.InstanceCount)
                        .Select(i => (Cover: (int)m.GetMeta("cover"), At: m.Position + m.Multimesh.GetInstanceTransform(i).Origin))).ToList();
                    int onBare = cards.Count(c => mask.TexelAt(c.At.X, c.At.Z) == 0);
                    var sample = cards.Where((_, i) => i % Math.Max(1, cards.Count / 60) == 0).ToList();
                    var offPick = sample.Where(c => Mech3.VegetationGround.Decorating(Mech3.VegetationGround.LayersAt(gamez, c.At + (Vector3.Up * 0.05f)))
                        is not { } layer || entry.CoverFor(layer.Texture) == null).ToList();
                    ctx.Note($"{name}: {field.Cells} cells, {field.Draws.Count()} draws, crop {cards.Count(c => c.Cover == 0)} and grass {cards.Count(c => c.Cover == 1)} cards, {field.Bare - bareBefore} lattice points bare under the mask");
                    ctx.Check(cards.Count > 0 && onBare == 0 && offPick.Count == 0,
                        $"at the {name} every card stands on picked ground ({offPick.Count} of {sample.Count} sampled off it{(offPick.Count > 0 ? $", first at {offPick[0].At}" : "")}) and none on a bare mask texel ({onBare})");
                }
                ctx.Check(field.Bare > 0, $"the strip clearance's bare texels grew nothing on {field.Bare} lattice points of picked ground");
                var (uniforms, _) = InstanceUniformNodes(field.Root);
                ctx.Check(field.Root.GetParent() != null && !UnderClutter(field.Root) && !HasCollider(field.Root) && uniforms == 0
                        && field.Draws.All(m => m.CastShadow == GeometryInstance3D.ShadowCastingSetting.Off && m.VisibilityRangeEnd > Mech3.GroundCover.Band((Mech3.GroundCover.Cover)(int)m.GetMeta("cover")).Y
                            && ((ShaderMaterial)m.MaterialOverride).Shader.Code.Contains("shadows_disabled", StringComparison.Ordinal)),
                    $"the cover stands outside the clutter root with no collider, no shadow cast or received, no instance uniform ({uniforms}), each ranged past its band");

                var root = field.Root;
                ApplyVegetation(live, VegetationQualitySetting.Original);
                ctx.Check(live.Session.GroundCoverCards == null && !root.IsInsideTree(), $"a live move to original drops the cover");
                ApplyVegetation(live, VegetationQualitySetting.Rich);
                ctx.Check(live.Session.GroundCoverCards != null, $"a move back to rich builds it again");
                Switch(live, enhanced: false);
                ctx.Check(live.Session.GroundCoverCards == null, $"a live switch to Original drops it");
                Switch(live, enhanced: true);
                ctx.Check(live.Session.GroundCoverCards != null, $"and a switch back to Enhanced builds it again");
                ApplyGroundCover(live, GroundCoverSetting.Off);
                ctx.Check(live.Session.GroundCoverCards == null && live.Session.GroundCover == null, $"a live move to off drops it");
                ApplyGroundCover(live, GroundCoverSetting.Cards);
                ctx.Check(live.Session.GroundCoverCards != null && live.Session.GroundCover == null, $"and back to cards builds it again");
            }
            finally
            {
                live.Close();
            }
        }
        finally
        {
            GroundCoverSetting.Resolve(launchedCover, null, null);
            VegetationQualitySetting.Resolve(launched, null, null);
            Restore(wasEnhanced);
        }
    }

    [Suite("graphics-ocean-switch",
        "the C1B wave ocean follows a live graphics switch at water quality waves: an Enhanced build "
        + "stands one ocean and every sea-level base sheet material carries the hide, which reads "
        + "that ocean's mask and steps aside only at the coverage the ocean discards below, and neither "
        + "the grid nor the hide acts for a spyglass disc's camera; "
        + "switched to Original the ocean leaves the tree, the mask globals return to their no-sea "
        + "defaults and the sheet carries a fresh Original "
        + "build's text, with no hide; back to Enhanced exactly one ocean stands and the sheet "
        + "carries a fresh Enhanced build's text; an Original build switched to Enhanced builds the "
        + "ocean; a closed session leaves no ocean in the tree, and a following Enhanced C2B session "
        + "at flat water quality builds none while its sheet still carries the hide")]
    internal static void OceanFollowsSwitch(TestContext ctx)
    {
        RequireData(ctx, OceanChapter);
        RequireData(ctx, FlatWaterChapter);
        bool wasEnhanced = GraphicsMode.Enhanced;
        string launched = WaterQualitySetting.Word;
        var report = new StringBuilder();
        try
        {
            ViewDistance.Set(null);
            WaterQualitySetting.Resolve(WaterQualitySetting.Waves, null, null);
            SeaReading freshOriginal, originalToEnhanced;
            Effects.Ocean? builtLive;
            var original = Open(ctx, enhanced: false, chapter: OceanChapter);
            try
            {
                if (!original.Built)
                {
                    ctx.Check(false, $"the Original C1B session builds");
                    return;
                }
                freshOriginal = Sea(original);
                Switch(original, true);
                originalToEnhanced = Sea(original);
                builtLive = FirstOcean(ctx.Host);
            }
            finally
            {
                original.Close();
            }
            ctx.Check(freshOriginal.Oceans == 0 && freshOriginal.Materials > 0 && freshOriginal.Drawn > 0 && freshOriginal.Hidden == 0,
                $"an Original C1B build stands no ocean and its base sheet carries no hide ({freshOriginal})");
            ctx.Check(originalToEnhanced.Oceans == 1 && originalToEnhanced.Hidden == originalToEnhanced.Materials,
                $"switched to Enhanced it builds the ocean and hides the sheet ({originalToEnhanced})");
            Left(ctx, builtLive, "the Original build switched to Enhanced");
            report.AppendLine($"fresh original\n{freshOriginal.Print()}\noriginal switched to enhanced\n{originalToEnhanced.Print()}");

            var enhanced = Open(ctx, enhanced: true, chapter: OceanChapter);
            Effects.Ocean? builtFresh;
            try
            {
                if (!enhanced.Built)
                {
                    ctx.Check(false, $"the Enhanced C1B session builds");
                    return;
                }
                var freshEnhanced = Sea(enhanced);
                builtFresh = FirstOcean(ctx.Host);
                ctx.Check(freshEnhanced.Oceans == 1 && enhanced.Session.OceanBuilt && freshEnhanced.Materials > 0
                        && freshEnhanced.Drawn > 0 && freshEnhanced.Hidden == freshEnhanced.Materials,
                    $"an Enhanced C1B build stands one ocean and every base sheet material hides its sea-level fragments ({freshEnhanced})");
                HideReadsTheMask(ctx, enhanced, builtFresh);
                ctx.Check(originalToEnhanced.Census == freshEnhanced.Census,
                    $"the Original build switched to Enhanced carries a fresh Enhanced build's base sheet text");
                ctx.Check(freshEnhanced.Census != freshOriginal.Census,
                    $"ABLE-TO-FAIL CONTROL: the two modes' fresh base sheet texts differ");

                Switch(enhanced, false);
                var switchedOriginal = Sea(enhanced);
                ctx.Check(switchedOriginal.Oceans == 0 && !enhanced.Session.OceanBuilt && OceansUnder(ctx.Host) == 0
                        && builtFresh is { } dropped && (!GodotObject.IsInstanceValid(dropped) || !dropped.IsInsideTree()),
                    $"switched to Original the ocean leaves the tree ({switchedOriginal}, {OceansUnder(ctx.Host)} under the host)");
                ctx.Check(switchedOriginal.Hidden == 0 && switchedOriginal.Census == freshOriginal.Census,
                    $"and the base sheet carries a fresh Original build's text, with no hide ({switchedOriginal.Hidden} hiding)");
                ctx.Check(Effects.OceanMask.Live == null,
                    $"and the hide's mask globals hold their no-sea defaults again");

                Switch(enhanced, true);
                var roundTrip = Sea(enhanced);
                var rebuilt = FirstOcean(ctx.Host);
                ctx.Check(roundTrip.Oceans == 1 && OceansUnder(ctx.Host) == 1 && rebuilt != null && !ReferenceEquals(rebuilt, builtFresh),
                    $"back to Enhanced the ocean is built again, exactly once ({roundTrip}, {OceansUnder(ctx.Host)} under the host)");
                ctx.Check(roundTrip.Hidden == roundTrip.Materials && roundTrip.Census == freshEnhanced.Census,
                    $"and the base sheet carries a fresh Enhanced build's text, hidden again ({roundTrip.Hidden} of {roundTrip.Materials} hiding)");
                ctx.Check(Effects.OceanMask.Live != null,
                    $"and the rebuilt ocean hands its mask to the hide again");
                report.AppendLine($"fresh enhanced\n{freshEnhanced.Print()}\nswitched original\n{switchedOriginal.Print()}\nround trip\n{roundTrip.Print()}");
                builtFresh = rebuilt;
            }
            finally
            {
                enhanced.Close();
            }
            Left(ctx, builtFresh, "the round-tripped Enhanced session");
            ctx.Check(Effects.OceanMask.Live == null,
                $"and with the ocean gone the hide's mask globals hold their no-sea defaults");

            WaterQualitySetting.Resolve(WaterQualitySetting.Flat, null, null);
            var flat = Open(ctx, enhanced: true, chapter: FlatWaterChapter);
            try
            {
                var sea = flat.Built ? Sea(flat) : null;
                ctx.Check(flat.Built && !flat.Session.OceanBuilt
                        && sea is { Oceans: 0 } && OceansUnder(ctx.Host) == 0,
                    $"a following Enhanced {FlatWaterChapter} session at flat water builds no ocean (built={flat.Built}, {sea?.ToString() ?? "no reading"}, {OceansUnder(ctx.Host)} under the host)");
                ctx.Check(sea is { Materials: > 0, Drawn: > 0 } && sea.Hidden == sea.Materials,
                    $"and its drawn sheet carries the hide, so a switch left on would hole its sea ({sea?.ToString() ?? "no reading"})");
                report.AppendLine($"flat water {FlatWaterChapter}\n{sea?.Print() ?? "no reading"}");
            }
            finally
            {
                flat.Close();
            }
            ctx.WriteArtifact("test-graphics-ocean-switch.txt", report.ToString());
        }
        finally
        {
            WaterQualitySetting.Resolve(launched, null, null);
            Restore(wasEnhanced);
        }
    }

    [Suite("ocean-lab",
        "the ocean lab stands in an Enhanced --freecam C1B session and nowhere in a --fly one: opened, "
        + "a length edit rewrites the standing ocean's shader text and a height edit only its uniform, "
        + "with one ocean standing throughout; an ocean rebuilt by a switch to Original and back draws "
        + "the edited sea, a reset draws the defaults, and a revert to the shipped sea writes the text the "
        + "session opened with")]
    internal static void OceanLabEdits(TestContext ctx)
    {
        RequireData(ctx, OceanChapter);
        bool wasEnhanced = GraphicsMode.Enhanced;
        string launched = WaterQualitySetting.Word;
        var shipped = Effects.OceanSeas.Load().For(OceanChapter);

        // Edits that differ from the shipped sea, whatever the lab saved there.
        float length = shipped.Length == 1.5f ? 1.6f : 1.5f;
        float height = shipped.Height == 0.8f ? 0.9f : 0.8f;
        try
        {
            WaterQualitySetting.Resolve(WaterQualitySetting.Waves, null, null);
            var freecam = Open(ctx, enhanced: true, chapter: OceanChapter, extra: "--freecam");
            try
            {
                var labs = Labs(freecam.Session);
                var ocean = FirstOcean(freecam.Session);
                if (!freecam.Built || labs.Count != 1 || ocean == null)
                {
                    ctx.Check(false, $"the Enhanced --freecam C1B session builds one ocean lab over one ocean (built={freecam.Built}, {labs.Count} lab(s), ocean {(ocean != null ? "standing" : "missing")})");
                    return;
                }
                var lab = labs[0];
                ctx.Check(!lab.IsOpen && lab.Edited == shipped && lab.Saved == shipped && ocean.Sea == shipped,
                    $"the lab starts closed on the shipped {OceanChapter} sea {shipped.Describe()} (lab {lab.Edited.Describe()}, ocean {ocean.Sea.Describe()})");
                string opened = ocean.ShaderText;
                lab.Toggle();
                ctx.Check(lab.IsOpen, $"Toggle opens the panel");

                lab.Set("length", length);
                Step(freecam);
                string lengthened = ocean.ShaderText;
                ctx.Check(lengthened != opened && ocean.Sea.Length == length && OceansUnder(freecam.Session) == 1
                        && ReferenceEquals(FirstOcean(freecam.Session), ocean),
                    $"a length edit rewrites the standing ocean's shader text, and the same one ocean stands ({OceansUnder(freecam.Session)} in the tree, sea {ocean.Sea.Describe()})");

                lab.Set("height", height);
                Step(freecam);
                float waveScale = Grid(ocean)?.GetShaderParameter("wave_scale").AsSingle() ?? -1f;
                ctx.Check(ocean.ShaderText == lengthened && Mathf.IsEqualApprox(waveScale, height),
                    $"a height edit sets wave_scale alone and compiles nothing (wave_scale {waveScale.ToString("0.###", CultureInfo.InvariantCulture)})");

                Switch(freecam, false);
                bool droppedInOriginal = OceansUnder(freecam.Session) == 0;
                Switch(freecam, true);
                var rebuilt = FirstOcean(freecam.Session);
                ctx.Check(droppedInOriginal && rebuilt != null && !ReferenceEquals(rebuilt, ocean)
                        && rebuilt.Sea == lab.Edited && rebuilt.ShaderText == lengthened,
                    $"an ocean rebuilt by a switch to Original and back draws the edited sea (dropped={droppedInOriginal}, rebuilt sea {rebuilt?.Sea.Describe() ?? "none"})");

                lab.ResetToDefaults();
                Step(freecam);
                ctx.Check(rebuilt?.Sea == Effects.SeaState.Default && lab.Edited == Effects.SeaState.Default,
                    $"a reset draws the defaults (sea {rebuilt?.Sea.Describe() ?? "none"})");

                lab.RevertToSaved();
                Step(freecam);
                ctx.Check(rebuilt?.ShaderText == opened && rebuilt.Sea == shipped,
                    $"a revert to the shipped sea writes the text the session opened with (sea {rebuilt?.Sea.Describe() ?? "none"})");
            }
            finally
            {
                freecam.Close();
            }

            var fly = Open(ctx, enhanced: true, chapter: OceanChapter, extra: "--fly");
            try
            {
                ctx.Check(fly.Built && fly.Session.OceanBuilt && Labs(fly.Session).Count == 0,
                    $"a --fly C1B session stands its ocean and builds no ocean lab (built={fly.Built}, ocean={fly.Session.OceanBuilt}, {Labs(fly.Session).Count} lab(s))");
            }
            finally
            {
                fly.Close();
            }
        }
        finally
        {
            WaterQualitySetting.Resolve(launched, null, null);
            Restore(wasEnhanced);
        }
    }

    [Suite("vegetation-added-stamps",
        "a C3 Enhanced flight at vegetation quality rich adds palms on the transition textures its entry picks, every one "
        + "standing on a picked triangle, about the set's density under the neutral mask and none counted as forest; a live move to "
        + "original and a live switch to Original draw none of them, and a move back draws the same again; a pick of another "
        + "texture, a density edit and a Clear stroke each place them again live, the stroke dropping those under it alone, and the "
        + "shipped entry gives the first stamps back exactly; a pick to the canopy set stamps none; no added stamp stands on C1's "
        + "railway, a polygon laid over its picked ground; C5 builds no added stamp")]
    internal static void VegetationAddedStamps(TestContext ctx)
    {
        const string Chapter = "C3";
        RequireData(ctx, Chapter);
        RequireData(ctx, VegetationChapter);
        RequireData(ctx, "C5");
        bool wasEnhanced = GraphicsMode.Enhanced;
        string launched = VegetationQualitySetting.Word;
        var gamez = ctx.ChapterGamez(Chapter)!;
        try
        {
            VegetationQualitySetting.Resolve(VegetationQualitySetting.Rich, null, null);
            var c3 = Open(ctx, enhanced: true, chapter: Chapter);
            try
            {
                if (!c3.Built || c3.Session.Clutter is not { ExportedKinds: { } kinds, Mask: { } mask, Vegetation: { } shipped } clutter)
                {
                    ctx.Check(false, $"the Enhanced {Chapter} flight builds with its clutter, mask and vegetation entry");
                    return;
                }
                var opening = AddedOf(kinds);
                var picked = shipped.Picks.Where(p => Mech3.VegetationScatter.Stamped(shipped, p.Texture) != null).Select(p => p.Texture).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var ground = Mech3.VegetationGround.Painted(gamez, picked.Contains);
                var origins = opening.SelectMany(k => k.Rich.Select((xf, i) => (xf, Hidden: clutter.Activation?.IsHidden(k.Index, i) == true)))
                    .Where(s => s.xf.Basis.Determinant() != 0f && !s.Hidden).Select(s => s.xf.Origin).ToList();
                int off = origins.Where((_, i) => i % 25 == 0).Count(o => !OnTriangles(ground, o));
                var models = kinds.Where(k => k.Added).Select(k => k.Model).Distinct().OrderBy(m => m).ToList();
                double share = clutter.AddedKept / (double)Math.Max(1, clutter.AddedCandidates);
                ctx.Check(picked.Count == 2 && origins.Count > 0 && off == 0 && models.SequenceEqual(new[] { "palmtree1.flt", "palmtree2.flt", "palmtree3.flt" }),
                    $"the flight adds {origins.Count} palms ({string.Join(", ", models)}) on {string.Join(", ", picked)}, {off} of a sample of {(origins.Count + 24) / 25} off a picked triangle");
                ctx.Check(share > 0.45 && share < 0.55 && kinds.Where(k => k.Added).All(k => !k.Mass) && Handover(c3) == "off",
                    $"the neutral mask keeps {clutter.AddedKept} of {clutter.AddedCandidates} candidates and none counts as forest ({Handover(c3)})");
                int drawn = AddedDrawn(c3);
                ctx.Same(origins.Count, drawn, $"rich draws every kept added stamp");

                ApplyVegetation(c3, VegetationQualitySetting.Original);
                ctx.Same(0, AddedDrawn(c3), $"a live move to original draws none of them");
                ApplyVegetation(c3, VegetationQualitySetting.Rich);
                ctx.Same(drawn, AddedDrawn(c3), $"and a move back draws them again");
                Switch(c3, enhanced: false);
                ctx.Same(0, AddedDrawn(c3), $"a live switch to Original draws none of them");
                Switch(c3, enhanced: true);
                ctx.Check(AddedDrawn(c3) == drawn && SameAdded(AddedOf(kinds), opening), $"and back in Enhanced the same ones");

                var palms = shipped.SetFor(picked.First())!;
                c3.Session.ApplyVegetation(shipped.WithPick("cliff1_sandtrans.tif", palms.Models));
                Step(c3);
                int withBeach = clutter.AddedCandidates;
                c3.Session.ApplyVegetation(shipped.WithSet(palms.Name, palms.Density * 2f, palms.Mass));
                Step(c3);
                int doubled = clutter.AddedCandidates;
                ctx.Check(withBeach > opening.Sum(k => k.Rich.Length) && doubled > 1.8 * opening.Sum(k => k.Rich.Length) && AddedDrawn(c3) > drawn,
                    $"a pick of cliff1_sandtrans and a doubled density each place them again ({withBeach} and {doubled} candidates against {opening.Sum(k => k.Rich.Length)})");
                c3.Session.ApplyVegetation(shipped);
                Step(c3);
                ctx.Check(SameAdded(AddedOf(kinds), opening) && AddedDrawn(c3) == drawn, $"the shipped entry gives the first added stamps back exactly");

                const float Radius = 150f;
                float texel = (mask.MaxX - mask.MinX) / mask.Side;
                var at = origins[origins.Count / 2];
                var brush = mask.Clone();
                brush.Stroke(at.X, at.Z, Radius, 1f, Mech3.VegetationBrush.Clear);
                c3.Session.ApplyVegetationMask(brush);
                Step(c3);
                var cleared = AddedOf(kinds);
                int inside = 0, wrong = 0;
                for (int k = 0; k < opening.Count; k++)
                {
                    for (int i = 0; i < opening[k].Rich.Length; i++)
                    {
                        float d = Flat(opening[k].Rich[i].Origin, at);
                        bool stands = cleared[k].Rich[i].Basis.Determinant() != 0f;
                        if (d < Radius - (2f * texel))
                        {
                            inside += opening[k].Rich[i].Basis.Determinant() != 0f ? 1 : 0;
                            wrong += stands ? 1 : 0;
                        }
                        else if (d > Radius + (2f * texel))
                        {
                            wrong += cleared[k].Rich[i] == opening[k].Rich[i] ? 0 : 1;
                        }
                    }
                }
                ctx.Check(cleared.Count == opening.Count && inside > 0 && wrong == 0 && AddedDrawn(c3) <= drawn - inside,
                    $"a Clear stroke drops the {inside} added palms under it and moves no other ({wrong} wrong)");
                c3.Session.ApplyVegetationMask(mask);
                Step(c3);
                ctx.Check(SameAdded(AddedOf(kinds), opening), $"the neutral mask gives them back exactly");

                c3.Session.ApplyVegetation(new Mech3.VegetationEntry
                {
                    Seed = shipped.Seed,
                    Mask = shipped.Mask,
                    Kinds = shipped.Kinds,
                    Sets = shipped.Sets,
                    Picks = shipped.Picks.Select(p => p with { Set = Mech3.CanopyMask.CanopySet }).ToList(),
                });
                Step(c3);
                ctx.Check(clutter.AddedCandidates == 0 && !kinds.Any(k => k.Added) && AddedDrawn(c3) == 0,
                    $"a pick to the canopy set stamps none ({clutter.AddedCandidates} candidates)");
                c3.Session.ApplyVegetation(shipped);
                Step(c3);
            }
            finally
            {
                c3.Close();
            }

            var c1 = Open(ctx, enhanced: true, chapter: VegetationChapter);
            try
            {
                if (!c1.Built || c1.Session.Clutter is not { ExportedKinds: { } c1Kinds })
                {
                    ctx.Check(false, $"the Enhanced {VegetationChapter} flight builds with its clutter");
                }
                else
                {
                    var c1Gamez = ctx.ChapterGamez(VegetationChapter)!;
                    var rails = Mech3.VegetationGround.Painted(c1Gamez, t => t.Equals(RailTexture, StringComparison.OrdinalIgnoreCase));
                    var onRails = AddedOf(c1Kinds).SelectMany(k => k.Rich).Where(xf => OnTriangles(rails, xf.Origin)).ToList();
                    int standing = onRails.Count(xf => xf.Basis.Determinant() != 0f);
                    ctx.Check(rails.Length > 0 && onRails.Count > 0 && standing == 0,
                        $"no added stamp stands on {VegetationChapter}'s railway ({RailTexture}, {rails.Length / 3} triangles): {standing} of the {onRails.Count} candidates on it stand");
                }
            }
            finally
            {
                c1.Close();
            }

            var c5 = Open(ctx, enhanced: true, chapter: "C5");
            try
            {
                ctx.Check(c5.Built && c5.Session.Clutter is { Vegetation: null, AddedCandidates: 0 } city && city.ExportedKinds?.Any(k => k.Added) != true
                        && AddedDrawn(c5) == 0,
                    $"C5 has no vegetation entry and builds no added stamp");
            }
            finally
            {
                c5.Close();
            }
        }
        finally
        {
            VegetationQualitySetting.Resolve(launched, null, null);
            Restore(wasEnhanced);
        }
    }

    [Suite("vegetation-lab",
        "the vegetation lab stands in an Enhanced --freecam C1 session at vegetation quality rich and nowhere in a --fly "
        + "one, opened on the session's entry and the shipped mask, which drops no original stamp: a tint edit of one kind re-bakes that kind's Rich colours "
        + "alone and the drawn stamps follow, a seed edit rerolls every listed kind, and a pick names the texture under a "
        + "stamp, adds stamps of the kind there and moves no original one, which a revert drops; a Clear stroke zero-scales the listed Rich stamps inside its radius and no other, which "
        + "draw nothing under rich and draw again after a live move to original, and a stamp it dropped stays hidden "
        + "through the activation's hide and show and through a crater, across a live move both ways; a cleared map hides "
        + "every copy past its edge; a Neutral stroke and a revert each draw the first build's stamps exactly; through the "
        + "pane's input, with no world collider, the brush ring stands on the ground under the cursor at the brush radius and "
        + "hides off the brush, over the panel, the sky and a closed lab, and a held Clear stroke drops the stamps under it; Save "
        + "writes the edit and the mask to a temp copy and leaves the shipped files alone")]
    internal static void VegetationLabEdits(TestContext ctx)
    {
        RequireData(ctx, VegetationChapter);
        bool wasEnhanced = GraphicsMode.Enhanced;
        string launched = VegetationQualitySetting.Word;
        var report = new StringBuilder();
        try
        {
            VegetationQualitySetting.Resolve(VegetationQualitySetting.Rich, null, null);
            var freecam = Open(ctx, enhanced: true, chapter: VegetationChapter, extra: "--freecam");
            try
            {
                var labs = NodesOf<UI.Labs.VegetationLab>(freecam.Session);
                if (!freecam.Built || labs.Count != 1 || freecam.Session.Clutter is not { ExportedKinds: { } kinds, Mask: { } mask })
                {
                    ctx.Check(false, $"the Enhanced --freecam {VegetationChapter} session builds one vegetation lab over its clutter and mask (built={freecam.Built}, {labs.Count} lab(s))");
                    return;
                }
                var lab = labs[0];
                var shipped = Mech3.VegetationSidecar.Load().For(VegetationChapter);
                byte[] shippedMask = Godot.FileAccess.GetFileAsBytes($"res://data/{shipped.Mask}");
                bool IsShipped(Mech3.VegetationMask m) => shipped.Mask.Length == 0 ? m.IsNeutral() : m.ToBytes().SequenceEqual(shippedMask);
                ctx.Check(!lab.IsOpen && EntryText(lab.Saved) == EntryText(shipped) && ReferenceEquals(lab.Mask, mask) && IsShipped(mask),
                    $"the lab starts closed on the shipped {VegetationChapter} entry and the clutter's own mask, the shipped one");
                lab.Toggle();
                var opening = RichOf(kinds);
                var originals = kinds.Select(k => k.Placements.ToArray()).ToList();
                var openingDraw = DrawnStamps(freecam);
                int originalKinds = kinds.Count(k => !k.Added), openingCandidates = freecam.Session.Clutter!.AddedCandidates;
                int openingKept = freecam.Session.Clutter.AddedKept;
                int originalsDropped = Enumerable.Range(0, originalKinds).Sum(k => opening[k]?.Xf.Count(x => x.Basis.Determinant() == 0f) ?? 0);
                ctx.Check(originalsDropped == 0, $"the shipped mask drops no original stamp ({originalsDropped} dropped)");
                // A model no shipped set names, so a tint edit re-bakes one kind alone.
                int edited = Enumerable.Range(0, originalKinds).Where(k => opening[k] != null && !kinds.Any(a => a.Added && a.Model == kinds[k].Model))
                    .MaxBy(k => opening[k]!.Value.Xf.Length);
                string model = kinds[edited].Model;

                var watch = System.Diagnostics.Stopwatch.StartNew();
                lab.Set(model, "tint", lab.Edited.Find(model)!.Tint == 0.3f ? 0.4f : 0.3f);
                long rebakeMs = watch.ElapsedMilliseconds;
                Step(freecam);
                var tinted = RichOf(kinds);
                bool tintAlone = Enumerable.Range(0, kinds.Count).All(k => k == edited
                    ? tinted[k]!.Value.Xf.SequenceEqual(opening[k]!.Value.Xf) && !tinted[k]!.Value.Colors.SequenceEqual(opening[k]!.Value.Colors)
                    : SameRich(tinted[k], opening[k]));
                ctx.Check(tintAlone && !DrawnStamps(freecam).Stamps.SequenceEqual(openingDraw.Stamps),
                    $"a tint edit of {model} re-bakes its Rich colours alone, and the drawn stamps follow (re-bake {rebakeMs} ms)");
                lab.SetSeed(lab.Saved.Seed + 1);
                Step(freecam);
                var reseeded = RichOf(kinds);
                ctx.Check(Enumerable.Range(0, kinds.Count).All(k => opening[k] == null || !SameRich(reseeded[k], tinted[k])),
                    $"a seed edit rerolls every listed kind");
                lab.RevertToSaved();
                Step(freecam);
                ctx.Check(SameRich(RichOf(kinds), opening) && DrawnStamps(freecam).Stamps.SequenceEqual(openingDraw.Stamps),
                    $"a revert draws the first build's Rich stamps exactly");

                // Two texels inside and outside the stroke, where the bilinear sample is wholly cleared or wholly neutral.
                const float Radius = 300f;
                float texel = (mask.MaxX - mask.MinX) / mask.Side;
                // The stroke stands where the shipped mask is neutral, so a Neutral stroke over it gives the shipped mask back.
                bool NeutralAround(Vector3 p)
                {
                    float reach = Radius + (2f * texel);
                    for (float dz = -reach; dz <= reach; dz += texel / 2f)
                    {
                        for (float dx = -reach; dx <= reach; dx += texel / 2f)
                        {
                            if (mask.At(p.X + dx, p.Z + dz) != 1f)
                                return false;
                        }
                    }
                    return true;
                }
                int first = Array.FindIndex(opening[edited]!.Value.Xf, xf => xf.Basis.Determinant() != 0f && NeutralAround(xf.Origin));
                var at = opening[edited]!.Value.Xf[first].Origin;
                watch.Restart();
                string? texture = lab.Pick(at, new[] { model });
                long pickMs = watch.ElapsedMilliseconds;
                Step(freecam);
                int pickedAdded = freecam.Session.Clutter!.AddedKept - openingKept;
                ctx.Check(texture != null && lab.Edited.SetFor(texture)?.Models.Contains(model) == true && pickedAdded > 0
                        && SameRich(RichOf(kinds).Take(originalKinds).ToList(), opening.Take(originalKinds).ToList()),
                    $"a pick under a {model} stamp gives {texture ?? "nothing"} that kind, adds {pickedAdded} stamps of it and moves no original one (pick and re-bake {pickMs} ms)");
                lab.RevertToSaved();
                Step(freecam);
                ctx.Check(kinds.Count == opening.Count && freecam.Session.Clutter.AddedCandidates == openingCandidates && SameRich(RichOf(kinds), opening),
                    $"a revert drops the pick's added stamps ({kinds.Count} kinds against {opening.Count}, {freecam.Session.Clutter.AddedCandidates} candidates against {openingCandidates})");

                lab.Paint(at, Mech3.VegetationBrush.Clear, Radius, 1f);
                watch.Restart();
                lab.ApplyMask();
                long maskMs = watch.ElapsedMilliseconds;
                Step(freecam);
                var cleared = RichOf(kinds);
                int inside = 0, wrong = 0;
                for (int k = 0; k < kinds.Count; k++)
                {
                    if (opening[k] is not { } open)
                    {
                        wrong += SameRich(cleared[k], null) ? 0 : 1;
                        continue;
                    }
                    var now = cleared[k]!.Value;
                    wrong += now.Colors.SequenceEqual(open.Colors) ? 0 : 1;
                    for (int i = 0; i < open.Xf.Length; i++)
                    {
                        float d = Flat(open.Xf[i].Origin, at);
                        if (d < Radius - (2f * texel))
                        {
                            inside++;
                            wrong += now.Xf[i].Basis.Determinant() == 0f && now.Xf[i].Origin == open.Xf[i].Origin ? 0 : 1;
                        }
                        else if (d > Radius + (2f * texel))
                        {
                            wrong += now.Xf[i] == open.Xf[i] ? 0 : 1;
                        }
                    }
                }
                var richDraw = DrawnStamps(freecam);
                ctx.Check(inside > 0 && wrong == 0 && DrawnIn(richDraw, at, Radius - (2f * texel)) == 0,
                    $"a Clear stroke zero-scales the {inside} listed Rich stamps inside its radius and no other, which draw nothing ({wrong} wrong, mask re-bake {maskMs} ms)");
                ApplyVegetation(freecam, VegetationQualitySetting.Original);
                int originalDrawn = DrawnIn(DrawnStamps(freecam), at, Radius - (2f * texel));
                ctx.Check(originalDrawn > 0, $"after a live move to original the stamps there draw again ({originalDrawn})");
                ApplyVegetation(freecam, VegetationQualitySetting.Rich);

                ActivationThroughDrop(ctx, freecam, kinds, at, Radius - (2f * texel), report);

                lab.Paint(at, Mech3.VegetationBrush.Neutral, Radius, 1f);
                lab.ApplyMask();
                Step(freecam);
                ctx.Check(IsShipped(mask) && SameRich(RichOf(kinds), opening) && DrawnStamps(freecam).Stamps.SequenceEqual(openingDraw.Stamps),
                    $"a Neutral stroke over it draws the first build's Rich stamps exactly");

                var centre = new Vector3((mask.MinX + mask.MaxX) / 2f, 0f, (mask.MinZ + mask.MaxZ) / 2f);
                lab.Paint(centre, Mech3.VegetationBrush.Clear, mask.MaxX - mask.MinX, 1f);
                lab.ApplyMask();
                Step(freecam);
                var edge = freecam.Session.EdgeExtender;
                edge?.Update(edge.BeyondCorner);
                var (copies, hiddenCopies) = edge?.ClutterCopyCensus() ?? (0, 0);
                ctx.Check(copies > 0 && hiddenCopies == copies,
                    $"a cleared map hides every copy past its edge, each inheriting its source's drop ({hiddenCopies} of {copies} hidden)");
                lab.RevertToSaved();
                Step(freecam);
                ctx.Check(lab.Mask is { } reverted && IsShipped(reverted) && SameRich(RichOf(kinds), opening)
                        && DrawnStamps(freecam).Stamps.SequenceEqual(openingDraw.Stamps),
                    $"a revert after a stroke draws the first build's Rich stamps exactly, its mask the shipped one again");

                ScreenStroke(ctx, freecam, lab, kinds, opening, at, Radius, 2f * texel, report);
                CraterThroughDrop(ctx, freecam, lab, kinds, at, Radius, Radius - (2f * texel), report);
                ctx.Check(Enumerable.Range(0, kinds.Count).All(k => kinds[k].Placements.SequenceEqual(originals[k])),
                    $"no edit moves an Original placement");
                SaveToCopy(ctx, lab.Saved, model, at, mask, report);
                report.AppendLine(CultureInfo.InvariantCulture, $"re-bake {rebakeMs} ms, pick and re-bake {pickMs} ms, mask re-bake {maskMs} ms, stroke at ({at.X:0}, {at.Z:0}) on {texture}");
            }
            finally
            {
                freecam.Close();
            }

            var fly = Open(ctx, enhanced: true, chapter: VegetationChapter, extra: "--fly");
            try
            {
                ctx.Check(fly.Built && NodesOf<UI.Labs.VegetationLab>(fly.Session).Count == 0,
                    $"a --fly {VegetationChapter} session builds no vegetation lab (built={fly.Built})");
            }
            finally
            {
                fly.Close();
            }
            ctx.WriteArtifact("test-vegetation-lab.txt", report.ToString());
        }
        finally
        {
            VegetationQualitySetting.Resolve(launched, null, null);
            Restore(wasEnhanced);
        }
    }

    [Suite("ocean-harbour-ramp",
        "an Enhanced flight at water quality waves over C2's harbour ramps and C5's water rising to the "
        + "steinmann ship takes each base-sheet ramp off sea level as open sea, which the sheet's hide "
        + "clears whole, so no flat patch stands over the waves; the calm ocean rises with each ramp, never "
        + "standing below it, so no gap opens under the quay or hull it meets; every base-sheet triangle over "
        + "the sea is cleared wherever it lies, so none draws or casts a shadow onto the ocean; and a sheet "
        + "raised off a sea texel no ramp covers still draws")]
    internal static void OceanHarbourRamp(TestContext ctx)
    {
        foreach (string chapter in RampChapters)
            RequireData(ctx, chapter);
        bool wasEnhanced = GraphicsMode.Enhanced;
        string launched = WaterQualitySetting.Word;
        try
        {
            ViewDistance.Set(null);
            WaterQualitySetting.Resolve(WaterQualitySetting.Waves, null, null);
            foreach (string chapter in RampChapters)
            {
                var rig = Open(ctx, enhanced: true, chapter: chapter);
                try
                {
                    var mask = Effects.OceanMask.Live;
                    if (!rig.Built || !rig.Session.OceanBuilt || mask == null || rig.Session.WorldScene == null)
                    {
                        ctx.Check(false, $"the Enhanced {chapter} session builds its ocean (built={rig.Built}, ocean={rig.Session.OceanBuilt})");
                        continue;
                    }
                    var sheet = SheetOverSea(rig, mask);
                    ctx.Check(sheet.Ramps > 0 && mask.RampTriangles >= sheet.Ramps && sheet.RampMissed == 0,
                        $"{chapter}: the ramps are open sea the hide clears whole ({sheet.Ramps} ramp triangle(s) drawn, {mask.RampTriangles} in the mask, {sheet.RampMissed} sample(s) on them left standing{sheet.Examples})");
                    ctx.Check(sheet.Ramps > 0 && sheet.RampShortfall < RampShortfall,
                        $"{chapter}: the calm ocean rises with each ramp, so no gap opens under what it meets (the ocean stands at most {sheet.RampShortfall.ToString("0.000", CultureInfo.InvariantCulture)} m below a ramp)");
                    ctx.Check(sheet.Sampled > 0 && sheet.Missed == 0,
                        $"{chapter}: every base-sheet triangle over the sea is cleared, so none draws or casts onto the ocean ({sheet.Sampled} sample(s) over the sea, {sheet.Missed} left standing, {sheet.Casting} in shadow-casting instances{sheet.Examples})");
                    var flat = sheet.FlatSea;
                    ctx.Check(flat is { } at && mask.ClearsSheet(at) && !mask.ClearsSheet(at + new Vector3(0f, 1f, 0f)),
                        $"ABLE-TO-FAIL CONTROL: {chapter}: off a ramp the hide clears the sheet at sea level and keeps it 1 m up (at {flat?.ToString() ?? "no flat sea sample"})");
                }
                finally
                {
                    rig.Close();
                }
            }
        }
        finally
        {
            WaterQualitySetting.Resolve(launched, null, null);
            Restore(wasEnhanced);
        }
    }

    [Suite("ocean-wake",
        "under Enhanced at water quality waves the C1B M03 freighter's wake sheets move onto the flat-sea "
        + "layer, its turbulence sheet with them, which no pane draws and a spyglass disc does, while the hull holds a wake slot on the sim "
        + "clock, and with no sim clock every sheet keeps its own drawn layers; the ocean draws the wake from "
        + "the hull's track: held still for 20 s of sim time the hull draws none, moved at 10 m/s for 20 s it "
        + "draws one, and a rebuilt ocean driven over the same sim times on a fresh clock publishes the "
        + "same wake uniforms bit for bit; a live move to flat water, a live switch to Original and a "
        + "session built under Original leave every sheet on its own drawn layers")]
    internal static void OceanWake(TestContext ctx)
    {
        RequireData(ctx, OceanChapter);
        ctx.RequireData(ctx.MissionZrdr(OceanChapter, WakeMission), $"{OceanChapter}/{WakeMission} zrdr");
        bool wasEnhanced = GraphicsMode.Enhanced;
        string launched = WaterQualitySetting.Word;
        var launchedClock = GameClock.Current;
        try
        {
            WaterQualitySetting.Resolve(WaterQualitySetting.Waves, null, null);
            var rig = Open(ctx, enhanced: true, chapter: OceanChapter, extra: new[] { "--freecam", $"--mission={WakeMission}" });
            // The session's own clock: what GameClock.Current held before the build depends on run order.
            var sessionClock = rig.Session.SimClock;
            try
            {
                var ocean = FirstOcean(rig.Session);
                var sheets = ocean?.WakeSheets.OfType<MeshInstance3D>().Where(s => GodotObject.IsInstanceValid(s) && s.IsVisibleInTree()).ToList() ?? new();
                var hull = sheets.Count > 0 ? sheets[0].GetParent()?.GetParent() as Node3D : null;
                if (!rig.Built || ocean == null || hull == null || sessionClock == null)
                {
                    ctx.Check(false, $"the Enhanced {OceanChapter}/{WakeMission} session stands an ocean over a ship with drawn wake sheets, on a sim clock (built={rig.Built}, ocean={(ocean != null)}, {sheets.Count} sheet(s), clock={(sessionClock != null)})");
                    return;
                }
                GameClock.Current = null;
                ocean._Process(0.0);
                ctx.Check(sheets.All(PaneDraws),
                    $"with no sim clock the ocean draws no wake, and every sheet keeps its own drawn layers ({SheetLayers(sheets)})");
                GameClock.Current = sessionClock;
                ocean._Process(0.0);
                ctx.Check(sheets.All(SteppedAside),
                    $"on the sim clock, with '{hull.Name}' holding a wake slot, its {sheets.Count} drawn wake sheets sit on the flat-sea layer alone, which no pane draws ({SheetLayers(sheets)})");
                var turbulence = sheets.Where(s => s.GetParent()?.Name.ToString().Contains("turb", StringComparison.OrdinalIgnoreCase) == true).ToList();
                ctx.Check(turbulence.Count > 0 && turbulence.All(SteppedAside),
                    $"…its turbulence sheet steps aside with them ({turbulence.Count} sheet(s): {SheetLayers(turbulence)})");

                var start = hull.GlobalPosition;
                var first = DriveWake(ocean, hull, start);
                ctx.Check(first.Standing == 0 && first.Moving == 1,
                    $"held still for 20 s '{hull.Name}' draws no wake ({first.Standing} drawn), moved at 10 m/s for 20 s it draws one ({first.Moving} drawn, {first.Arc.ToString("0.#", CultureInfo.InvariantCulture)} m of track)");

                ApplyWater(rig, WaterQualitySetting.Flat);
                ctx.Check(sheets.All(PaneDraws),
                    $"a live move to flat water puts every sheet back on its own drawn layers ({SheetLayers(sheets)})");
                ApplyWater(rig, WaterQualitySetting.Waves);
                var rebuilt = FirstOcean(rig.Session);
                hull.GlobalPosition = start;
                var second = rebuilt != null && !ReferenceEquals(rebuilt, ocean) ? DriveWake(rebuilt, hull, start) : null;
                ctx.Check(second != null && first.Track.Length > 0 && first.Track.SequenceEqual(second.Track) && first.Bounds.SequenceEqual(second.Bounds),
                    $"a rebuilt ocean driven over the same sim times on a fresh clock publishes the same wake uniforms bit for bit (rebuilt={(rebuilt != null && !ReferenceEquals(rebuilt, ocean))})");
                bool aside = sheets.All(SteppedAside);
                GameClock.Current = sessionClock;
                Switch(rig, false);
                ctx.Check(aside && sheets.All(PaneDraws) && OceansUnder(rig.Session) == 0,
                    $"a live switch to Original drops the ocean and puts every sheet it held aside back on its own drawn layers (aside before={aside}; {SheetLayers(sheets)})");
                hull.GlobalPosition = start;
            }
            finally
            {
                GameClock.Current = sessionClock;
                rig.Close();
            }
            var original = Open(ctx, enhanced: false, chapter: OceanChapter, extra: new[] { "--freecam", $"--mission={WakeMission}" });
            try
            {
                var sheets = new List<MeshInstance3D>();
                var names = new Dictionary<Material, string>();
                foreach (var (material, texture) in original.Session.WorldScene?.TexturedMaterials ?? Array.Empty<(ShaderMaterial, string)>())
                    names[material] = texture;
                Walk(original.Session, node =>
                {
                    if (node is MeshInstance3D mi && mi.IsVisibleInTree() && Materials(mi).Any(m => names.TryGetValue(m, out var t)
                            && (t.Contains("wakefront", StringComparison.OrdinalIgnoreCase) || t.Contains("wakeback", StringComparison.OrdinalIgnoreCase) || t.Contains("turb", StringComparison.OrdinalIgnoreCase))))
                        sheets.Add(mi);
                });
                ctx.Check(original.Built && FirstOcean(original.Session) == null && sheets.Count > 0 && sheets.Any(s => s.GetParent()?.Name.ToString().Contains("turb", StringComparison.OrdinalIgnoreCase) == true)
                        && sheets.All(PaneDraws),
                    $"ABLE-TO-FAIL CONTROL: an Original {OceanChapter}/{WakeMission} build stands no ocean and draws its {sheets.Count} wake and turbulence sheet(s) on their own layers ({SheetLayers(sheets)})");
            }
            finally
            {
                original.Close();
            }
        }
        finally
        {
            GameClock.Current = launchedClock;
            WaterQualitySetting.Resolve(launched, null, null);
            Restore(wasEnhanced);
        }
    }

    [Suite("graphics-shader-twins",
        "after the load warm-up has compiled the other mode's twin of every cache shader, a live "
        + "switch to Original and back makes no new shader and rewrites no shader's text, so Godot "
        + "compiles nothing new: it only moves each material onto the twin it already compiled, "
        + "and no drawn material is left on the other mode's twin, the cockpit gauges' own copies "
        + "and the fade twins included; under either mode the cache keys whose texts agree draw "
        + "one shader between them")]
    internal static void ShaderTwins(TestContext ctx)
    {
        RequireData(ctx);
        bool wasEnhanced = GraphicsMode.Enhanced;
        bool wasDrawn = Mech3.ShaderTwins.EnhancedDrawn;
        var rig = Open(ctx, enhanced: true);
        try
        {
            if (!rig.Built)
            {
                ctx.Check(false, $"the Enhanced session builds");
                return;
            }

            // What the launcher sets on the first Enhanced frame, which a suite never draws.
            Mech3.ShaderTwins.EnhancedDrawn = true;
            int warmed = Mech3.ShaderTwins.WarmOtherMode();
            ctx.Check(Mech3.ShaderTwins.OtherModeWarm,
                $"the warm-up leaves every cache shader with its Original twin ({warmed} made now)");
            int made = Mech3.ShaderTwins.Made;
            int rewrites = Mech3.ShaderTwins.TextRewrites;
            Switch(rig, false);
            int staleOriginal = StaleMaterials(rig);
            var (originalShaders, originalTexts) = DrawnKeyShaders(rig);
            Switch(rig, true);
            int staleEnhanced = StaleMaterials(rig);
            var (enhancedShaders, enhancedTexts) = DrawnKeyShaders(rig);
            ctx.Check(originalShaders == originalTexts && enhancedShaders == enhancedTexts && originalTexts > 0,
                $"keys whose texts agree draw one shader between them ({originalShaders} shaders for {originalTexts} texts under Original, {enhancedShaders} for {enhancedTexts} under Enhanced)");
            ctx.Check(Mech3.ShaderTwins.Made == made,
                $"the two switches make no new shader ({Mech3.ShaderTwins.Made - made} made)");
            ctx.Check(Mech3.ShaderTwins.TextRewrites == rewrites,
                $"and rewrite no shader's text ({Mech3.ShaderTwins.TextRewrites - rewrites} rewritten)");
            ctx.Check(staleOriginal == 0 && staleEnhanced == 0,
                $"no drawn material stays on the other mode's twin ({staleOriginal} under Original, {staleEnhanced} under Enhanced)");
        }
        finally
        {
            rig.Close();
            Mech3.ShaderTwins.EnhancedDrawn = wasDrawn;
            Restore(wasEnhanced);
        }
    }

    [Suite("graphics-retext-compiles",
        "a key whose Original shader drew red and nobody wears any more, rewritten in place by a "
        + "first switch to Enhanced before an Enhanced frame has drawn while that draw's pipeline "
        + "compiles still wait in a busy worker pool, stays pinned on a material of its own; after "
        + "one TAA frame builds Godot's advanced variants, a fresh material on the key draws the "
        + "Enhanced text, and the rewrite and draws print no engine error (no free_rid off the render "
        + "thread). Only a process that has drawn no TAA frame before can show the draw fail; the pin "
        + "check fails in any")]
    internal static void RetextCompiles(TestContext ctx)
    {
        bool wasEnhanced = GraphicsMode.Enhanced;
        bool wasDrawn = Mech3.ShaderTwins.EnhancedDrawn;
        var host = new Node { Name = "retext_probe" };
        ctx.Host.AddChild(host);
        ShaderMaterial? fresh = null;
        var busy = new List<long>();
        try
        {
            GraphicsMode.Set(false);
            Mech3.ShaderTwins.Regenerate();
            Mech3.ShaderTwins.EnhancedDrawn = false;
            RenderingServer.ForceSync();
            int? errorsBefore = TestHarness.EngineErrorsSoFar();

            var key = Mech3.ShaderTwins.Make(() => GraphicsMode.Enhanced ? EnhancedProbe : OriginalProbe,
                "retext-probe", "retext-probe");
            var (view, quad) = ProbeView(host);
            var worn = Mech3.ShaderTwins.Follow(new ShaderMaterial(), key);
            var original = worn.Shader;
            // A busy pool keeps the draw's pipeline compiles queued through the rewrite, as a loaded
            // shard's does; a plain Code assignment then prints free_rid errors.
            busy = HoldWorkerPool(150);
            quad.MaterialOverride = worn;
            var drawnOriginal = ProbePixel(view);
            // The key keeps no wearer, so the rewrite queues no material update of Godot's own.
            quad.MaterialOverride = null;
            worn.Dispose();

            GraphicsMode.Set(true);
            Mech3.ShaderTwins.Regenerate();
            bool rewritten = ReferenceEquals(key.Current, original);
            bool pinned = Mech3.ShaderTwins.IsPinned(original);
            AdvancedFrame(host);

            fresh = Mech3.ShaderTwins.Follow(new ShaderMaterial(), key);
            quad.MaterialOverride = fresh;
            var drawnEnhanced = ProbePixel(view);
            RenderingServer.ForceSync();
            int? errorsAfter = TestHarness.EngineErrorsSoFar();

            ctx.Check(rewritten, $"the first switch to Enhanced rewrites the unworn Original shader in place");
            ctx.Check(pinned, $"and pins the rewritten shader on a material of its own");
            ctx.Check(drawnOriginal.R > 0.7f && drawnOriginal.G < 0.3f,
                $"ABLE-TO-FAIL CONTROL: the Original text draws red ({drawnOriginal})");
            ctx.Check(drawnEnhanced.G > 0.7f && drawnEnhanced.R < 0.3f,
                $"after a TAA frame a fresh material on the key draws the Enhanced text, green ({drawnEnhanced})");
            ctx.Check(errorsBefore != null && errorsAfter == errorsBefore,
                $"with no engine error ({(errorsAfter ?? 0) - (errorsBefore ?? 0)} new line(s), log {(errorsBefore != null ? "read" : "missing")})");
        }
        finally
        {
            foreach (long task in busy)
                WorkerThreadPool.WaitForTaskCompletion(task);
            host.Free();
            fresh?.Dispose();
            Mech3.ShaderTwins.EnhancedDrawn = wasDrawn;
            Restore(wasEnhanced);
        }
    }

    [Suite("graphics-twins-world-release",
        "the shader cache stops following a test world's materials, a fade copy made after the build "
        + "included, once the harness destroys that world, and keeps following the cached chapter "
        + "world's, its fade copy held only from C# included, through the release a session build runs; "
        + "the harness's release between suites drops a shader no material wears and keeps a worn one")]
    internal static void TwinsWorldRelease(TestContext ctx)
    {
        ShaderMaterial? cachedSource = null, cachedFade = null, privateSource = null, privateFade = null;
        bool privateTracked = false;
        try
        {
            ctx.WithWorld(ctx.Chapter, collision: false, world =>
            {
                cachedSource = FadeSource(world.Stage);
                cachedFade = cachedSource != null ? Mech3.ShaderTwins.FadeCopy(cachedSource) : null;
            });
            ctx.WithPrivateWorld(ctx.Chapter, collision: false, world =>
            {
                privateSource = FadeSource(world.Stage);
                privateFade = privateSource != null ? Mech3.ShaderTwins.FadeCopy(privateSource) : null;
                privateTracked = privateSource != null && privateFade != null
                    && Mech3.ShaderTwins.IsTracked(privateSource) && Mech3.ShaderTwins.IsTracked(privateFade);
            });
            if (cachedSource == null || cachedFade == null || privateSource == null || privateFade == null)
            {
                ctx.Check(false, $"each {ctx.Chapter} world holds a tracked material that takes a fade copy");
                return;
            }
            int heldFromCs = cachedFade.GetReferenceCount();
            Mech3.ShaderTwins.ReleaseUnused();

            ctx.Check(privateTracked, $"ABLE-TO-FAIL CONTROL: the private world's material and its fade copy are followed while it stands");
            ctx.Check(!Mech3.ShaderTwins.IsTracked(privateSource) && !Mech3.ShaderTwins.IsTracked(privateFade),
                $"once the harness destroys that world, neither is followed");
            ctx.Check(heldFromCs <= 1, $"CONTROL: the cached world's fade copy is held only from C# (reference count {heldFromCs})");
            ctx.Check(Mech3.ShaderTwins.IsTracked(cachedSource) && Mech3.ShaderTwins.IsTracked(cachedFade),
                $"the cached world's material and its fade copy are still followed after the release a session build runs");
        }
        finally
        {
            cachedFade?.Dispose();
            privateFade?.Dispose();
        }
        ReleasesUnworn(ctx);
    }

    [Suite("world-merge",
        "an Enhanced flight session draws its static world's opaque surfaces merged by shared node frame "
        + "and material, and every material's placed-world vertices once: the same count as on the faithful "
        + "path, which draws every node itself with no merged mesh in the tree, and the same after a "
        + "switch back; a member a visibility change, a name query or a direct release reaches draws its "
        + "own whole mesh again and the count holds")]
    internal static void WorldMergeDraws(TestContext ctx)
    {
        RequireData(ctx);
        bool wasEnhanced = GraphicsMode.Enhanced;
        var rig = Open(ctx, enhanced: true);
        try
        {
            if (!rig.Built || rig.Session.WorldMerge is not { } merge)
            {
                ctx.Check(false, $"the Enhanced flight session builds with a world merge");
                return;
            }
            int groups = merge.GroupCount;
            ctx.Check(merge.Merged && groups > 0 && merge.SurfaceCount >= 2 * groups,
                $"the merge stands: {merge.SurfaceCount} surface(s) of {merge.MemberCount} node(s) in {groups} mesh(es)");
            var merged = PlacedVertices(rig);

            Switch(rig, false);
            var original = PlacedVertices(rig);
            ctx.Check(!merge.Merged && merge.GetChildCount() == 0,
                $"on the faithful path no merged mesh is in the tree ({merge.GetChildCount()} child(ren))");
            ctx.Check(merged.Text == original.Text,
                $"every material draws as many placed-world vertices merged as each node does itself ({merged} against {original})");
            ctx.Check(merged.Nodes < original.Nodes,
                $"ABLE-TO-FAIL CONTROL: the merge draws them in fewer surface draws ({merged.Nodes} against {original.Nodes})");

            Switch(rig, true);
            var back = PlacedVertices(rig);
            ctx.Check(merge.Merged && merge.GroupCount == groups && back.Text == original.Text,
                $"a switch back draws the kept merge again ({merge.GroupCount} mesh(es), {back})");

            int members = merge.MemberCount;
            var hidden = merge.MemberMeshes.First();
            var owner = (Node3D)hidden.GetParent();
            owner.Visible = false;
            owner.Visible = true;
            var claimed = merge.MemberMeshes.First();
            var claimedOwner = (Node3D)claimed.GetParent();
            int found = merge.Runtime?.FindNodes(claimedOwner.GetMeta(Mech3.AnimRuntime.NameMeta).AsString()).Count ?? 0;
            var direct = merge.MemberMeshes.First();
            Mech3.WorldMerge.Release((Node3D)direct.GetParent());
            var released = PlacedVertices(rig);
            ctx.Check(merge.MemberCount <= members - 3 && !merge.MemberMeshes.Contains(hidden)
                    && !merge.MemberMeshes.Contains(claimed) && !merge.MemberMeshes.Contains(direct),
                $"a visibility change, a name query ({found} found) and a direct release each release their node ({members} to {merge.MemberCount} member(s))");
            ctx.Check(released.Text == original.Text,
                $"and the released nodes draw their whole meshes, every vertex still once ({released})");
        }
        finally
        {
            rig.Close();
            Restore(wasEnhanced);
        }
    }

    [Suite("graphics-switch-cover",
        "a live switch under its cover runs in order: the flight is held and the cover is in the tree "
        + "before the switch runs, the switch waits for the cover's second frame, the hold stands "
        + "through a stall and a slow frame after it and drops with the cover once three frames settle, "
        + "on a machine whose ordinary frame is slow as well; "
        + "the stall reaches the sim clock's accumulator as no step; a pause the player had up stands "
        + "after the cover drops; with no pause state the clock is held and put back as it was; and the "
        + "load warm-up's hidden TAA frame is raised once by an Original process alone")]
    internal static void SwitchCoverOrder(TestContext ctx)
    {
        var flying = CoverRun(ctx, paused: false, FastFrames);
        ctx.Check(flying.Order == "held,covered,work" && flying.WorkFrame == 2,
            $"the hold and the cover come first and the switch runs on the cover's second frame ({flying.Order} at frame {flying.WorkFrame})");
        ctx.Check(flying.HeldThroughStall && flying.Steps == 0,
            $"the clock stays held through the stall frames and takes no step from them ({flying.Steps} step(s))");
        ctx.Check(flying.Dropped && !flying.CoverInTree && !flying.HeldAfter && flying.StepsAfter == 1,
            $"the cover drops after three settled frames and the flight resumes one step at a time (dropped {flying.Dropped}, in tree {flying.CoverInTree}, held {flying.HeldAfter}, {flying.StepsAfter} step(s) next frame)");

        var paused = CoverRun(ctx, paused: true, FastFrames);
        var slow = CoverRun(ctx, paused: false, new[] { 150.0, 150.0, 4000.0, 150.0, 6000.0, 150.0, 150.0, 150.0 });
        ctx.Check(slow.Dropped && slow.HeldThroughStall && !slow.HeldAfter,
            $"on a machine whose ordinary frame takes 150 ms the cover still drops after three such frames, held through the stalls (dropped {slow.Dropped})");
        ctx.Check(paused.Order == "held,covered,work" && paused.Dropped && paused.PausedAfter && paused.HeldAfter,
            $"over the pause sheet the same order runs, and the pause still stands after the cover drops (paused {paused.PausedAfter}, held {paused.HeldAfter})");

        AdvancedVariantFrame(ctx);

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

    // Able to fail: a release that drops a worn shader leaves its material off its key. One that
    // drops nothing keeps the unworn key's shader alive. It releases with no sync or pool drain, as
    // freeing a drawn shader waits for its queued pipeline compiles (analysis/shard-memory/FINDINGS.md).
    private static void ReleasesUnworn(TestContext ctx)
    {
        var worn = Mech3.ShaderTwins.Make(() => OriginalProbe.Replace("retext", "release-worn", StringComparison.Ordinal),
            "release-probe", "release-worn");
        var unworn = Mech3.ShaderTwins.Make(() => OriginalProbe.Replace("retext", "release-unworn", StringComparison.Ordinal),
            "release-probe", "release-unworn");
        var material = Mech3.ShaderTwins.Follow(new ShaderMaterial(), worn);
        try
        {
            ulong unwornId = InstanceIdOf(unworn);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            int released = Mech3.ShaderTwins.ReleaseUnworn();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            bool unwornFreed = !GodotObject.IsInstanceIdValid(unwornId);
            ctx.Check(worn.Has(GraphicsMode.Enhanced) && ReferenceEquals(material.Shader, worn.Current),
                $"the release keeps a shader a material wears ({released} released)");
            ctx.Check(!unworn.Has(GraphicsMode.Enhanced) && unwornFreed,
                $"and drops one nothing wears, which the collector then frees (freed {unwornFreed})");
            int made = Mech3.ShaderTwins.Made;
            ctx.Check(unworn.Current.Code.Contains("release-unworn", StringComparison.Ordinal) && Mech3.ShaderTwins.Made == made + 1,
                $"a dropped key makes its shader again on next use");
        }
        finally
        {
            material.Dispose();
        }
    }

    // In a frame of its own, so no stack slot of the caller keeps the shader's wrapper alive.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ulong InstanceIdOf(Mech3.ModeShader key) => key.Current.GetInstanceId();

    // A small pane of its own world, no TAA, whose quad fills the camera's view.
    private static (SubViewport View, MeshInstance3D Quad) ProbeView(Node host)
    {
        var view = new SubViewport
        {
            Size = new Vector2I(64, 64),
            OwnWorld3D = true,
            World3D = new World3D(),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            RenderTargetClearMode = SubViewport.ClearMode.Always,
        };
        view.AddChild(new Camera3D { Current = true });
        var quad = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(4f, 4f) }, Position = new Vector3(0f, 0f, -1f) };
        view.AddChild(quad);
        host.AddChild(view);
        quad.ForceUpdateTransform();
        return (view, quad);
    }

    // The pane's centre after a forced draw. Twice, so a material set this frame has its update and
    // its pipeline before the read.
    private static Color ProbePixel(SubViewport view)
    {
        RenderingServer.ForceDraw();
        RenderingServer.ForceDraw();
        var image = view.GetTexture()?.GetImage();
        return image == null || image.IsEmpty() ? new Color(0f, 0f, 0f, 0f) : image.GetPixel(32, 32);
    }

    // Fills Godot's worker pool with sleeps of ms each, so a pipeline compile a draw queues waits
    // behind them. The caller waits each task out once.
    private static List<long> HoldWorkerPool(int ms) => Enumerable.Range(0, 2 * OS.GetProcessorCount())
        .Select(_ => WorkerThreadPool.AddTask(Callable.From(() => System.Threading.Thread.Sleep(ms)))).ToList();

    // One drawn TAA frame, which is what has Godot build its advanced variants for every shader alive.
    private static void AdvancedFrame(Node host)
    {
        var view = new SubViewport
        {
            Size = new Vector2I(256, 256),
            OwnWorld3D = true,
            World3D = new World3D(),
            UseTaa = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        view.AddChild(new Camera3D { Current = true });
        host.AddChild(view);
        RenderingServer.ForceDraw();
        view.Free();
    }

    // The load warm-up's hidden TAA frame: raised once by an Original process that has drawn no
    // Enhanced frame, and never by an Enhanced one. Freed before it draws, so this shard builds no
    // advanced variants.
    private static void AdvancedVariantFrame(TestContext ctx)
    {
        bool wasEnhanced = GraphicsMode.Enhanced;
        bool wasDrawn = Mech3.ShaderTwins.EnhancedDrawn;
        var host = new Node();
        ctx.Host.AddChild(host);
        try
        {
            GraphicsMode.Set(true);
            Mech3.ShaderTwins.EnhancedDrawn = false;
            bool underEnhanced = EnhancedLook.WarmAdvancedVariants(host);
            GraphicsMode.Set(false);
            bool first = EnhancedLook.WarmAdvancedVariants(host);
            var view = host.GetChildCount() == 1 ? host.GetChild(0) as SubViewport : null;
            bool again = EnhancedLook.WarmAdvancedVariants(host);
            ctx.Check(!underEnhanced && first && !again && view is { UseTaa: true } && Mech3.ShaderTwins.EnhancedDrawn,
                $"an Original process with no Enhanced frame raises one hidden TAA viewport, once (Enhanced {underEnhanced}, first {first}, again {again}, TAA {view?.UseTaa})");
        }
        finally
        {
            host.Free();
            GraphicsMode.Set(wasEnhanced);
            Mech3.ShaderTwins.EnhancedDrawn = wasDrawn;
        }
    }

    // One cover over a pause state and a sim clock, ticked through the frames.
    private static CoverReading CoverRun(TestContext ctx, bool paused, double[] frames)
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
        foreach (double ms in frames)
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

    // The distinct cache shaders the session draws, and their distinct texts.
    private static (int Shaders, int Texts) DrawnKeyShaders(Rig rig)
    {
        var shaders = new HashSet<Shader>();
        Walk(rig.Session, node =>
        {
            if (node is GeometryInstance3D geometry)
            {
                foreach (var material in Materials(geometry))
                {
                    if (Mech3.ShaderTwins.IsKeyShader(material.Shader))
                        shaders.Add(material.Shader);
                }
            }
        });
        return (shaders.Count, shaders.Select(s => s.Code).Distinct(StringComparer.Ordinal).Count());
    }

    // Vertices drawn per material by the placed world's mesh instances and the merged meshes, visible
    // ones only. A surface drawn twice or lost moves its material's count.
    private static VertexReading PlacedVertices(Rig rig)
    {
        var counts = new SortedDictionary<ulong, long>();
        int nodes = 0;
        Walk(rig.Session, node =>
        {
            if (node is not MeshInstance3D { Mesh: ArrayMesh mesh } mi || !mi.IsVisibleInTree())
                return;
            bool placed = mi.Name == "mesh" && mi.GetParent() is Node3D owner && owner.HasMeta(Mech3.AnimRuntime.IndexMeta);
            if (!placed && mi.GetParent() is not Mech3.WorldMerge)
                return;
            for (int s = 0; s < mesh.GetSurfaceCount(); s++)
            {
                if (mesh.SurfaceGetMaterial(s) is { } material)
                {
                    nodes++;
                    ulong id = material.GetInstanceId();
                    counts[id] = counts.GetValueOrDefault(id) + mesh.SurfaceGetArrayLen(s);
                }
            }
        });
        var text = new StringBuilder();
        foreach (var (id, count) in counts)
            text.Append(CultureInfo.InvariantCulture, $"{id:x}:{count} ");
        return new VertexReading(nodes, counts.Count, counts.Values.Sum(), text.ToString());
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
                // A cache shader on a material that follows no key could not follow a switch either.
                bool loose = Mech3.ShaderTwins.IsKeyShader(material.Shader) && !Mech3.ShaderTwins.IsTracked(material);
                if (loose || Mech3.ShaderTwins.IsStale(material))
                    stale++;
            }
        });
        return stale;
    }

    // The process back on the mode, fade and anti-aliasing the suite found, its shaders' text included.
    // ⚠ Keep the anti-aliasing. A switch leaves the window's viewport on TAA. The next suite to draw
    // it then has Godot build every live shader's advanced variants, 5 GB in a shard.
    private static void Restore(bool wasEnhanced)
    {
        GraphicsMode.Set(wasEnhanced);
        ViewDistance.Set(null);
        Mech3.ShaderTwins.Regenerate();
        EffectsLevel.RegisteredScaleSq = EnhancedLook.ClutterFadeScaleSq();
        RenderingServer.GlobalShaderParameterSet(EffectsLevel.ShaderParam, EffectsLevel.RegisteredScaleSq);
        EnhancedLook.ReapplyWordSettings("a suite's restore");
    }

    // Which flare rig the session holds, with one flare per pane. The screen rig hangs two canvases on
    // a pane's overlays and the world rig one, so the count shows a dropped rig's left behind.
    private static void FlareRig(TestContext ctx, Rig rig, bool world, int overlays, string label)
    {
        var session = rig.Session;
        int panes = session.Rigs.Count;
        int worldCount = session.WorldFlare?.InstanceCount ?? -1, screenCount = session.ScreenFlare?.InstanceCount ?? -1;
        ctx.Check(world ? worldCount == panes && session.ScreenFlare == null : screenCount == panes && session.WorldFlare == null,
            $"{label}: the session draws the {(world ? "world" : "screen")} flare rig alone, one per pane (world {worldCount}, screen {screenCount}, {panes} pane(s))");
        int quads = session.WorldFlare?.Quads().Count(q => q.Quad.IsInsideTree()) ?? 0;
        ctx.Check(world ? quads > 0 : quads == 0, $"{label}: {quads} world flare quad(s) in the tree");
        int held = session.Rigs[0].WorldOverlays.Count;
        ctx.Check(held == overlays, $"{label}: pane 1 holds {held} world overlay(s), {overlays} expected");
    }

    // One copy for the whole session, matching the session's sun after one frame of its own. The
    // sun stands on the layer the disc leaves out.
    private static void SunCopied(TestContext ctx, Rig rig, string label)
    {
        var copy = rig.Session.SpyglassSun;
        copy?._Process(0.0);
        ctx.Check(copy is { } built && built.IsInsideTree() && Count<SpyglassSun>(rig.Session) == 1,
            $"{label}: one spyglass sun serves the session's {rig.Session.Rigs.Count} panes ({Count<SpyglassSun>(rig.Session)} found)");
        ctx.Check(copy != null && ReferenceEquals(copy.Source, rig.Sun) && SpyglassSun.Matches(rig.Sun, copy),
            $"{label}: it matches the sun's bearing, colour and energy with no shadow, on the disc's layer alone ({Light(rig.Sun)} against {(copy != null ? Light(copy) : "no copy")})");
        ctx.Check(rig.Sun.Layers == UI.Boards.SplitScreen.SunLayer && rig.Sun.ShadowEnabled,
            $"{label}: the sun casts, on its own layer (0x{rig.Sun.Layers:X5})");
    }

    // Every pane draws the sun's layer and not the copy's. Each disc, aimed as a frame aims it, draws
    // its pane's view less its airframe, with the copy in place of the sun under Enhanced.
    private static void DiscMasks(TestContext ctx, Rig rig, bool enhanced, string label)
    {
        var panes = rig.Session.Rigs;
        var views = new List<SpyglassView>();
        Walk(rig.Session, node =>
        {
            if (node is SpyglassView view)
                views.Add(view);
        });
        ctx.Check(panes.Count == 2 && views.Count == panes.Count,
            $"{label}: one disc per pane ({views.Count} disc(s), {panes.Count} pane(s))");
        foreach (var pane in panes)
        {
            uint mask = pane.Camera.CullMask;
            ctx.Check((mask & UI.Boards.SplitScreen.SpyglassSunLayer) == 0 && (mask & UI.Boards.SplitScreen.SunLayer) != 0,
                $"{label}: pane {pane.Index + 1} draws the sun's layer and not the copy's (0x{mask:X5})");
            ctx.Check((mask & UI.Boards.SplitScreen.FlatSeaLayer) == 0,
                $"{label}: pane {pane.Index + 1} does not carry the flat-sea marker, so it draws the wave ocean (0x{mask:X5})");
            ctx.Check((mask & Mech3.VegetationModels.Layer) != 0,
                $"{label}: pane {pane.Index + 1} draws the near vegetation models' layer (0x{mask:X5})");
        }
        foreach (var view in views)
        {
            view.Aim(Transform3D.Identity, 30f, 96);
            uint disc = view.DiscCullMask;
            view.Idle();
            bool fromPane = panes.Any(p =>
                disc == SpyglassView.DiscMask(p.Camera.CullMask, UI.Boards.SplitScreen.OwnAirframeLayer(p.Index), enhanced));
            bool sun = (disc & UI.Boards.SplitScreen.SunLayer) != 0;
            bool copy = (disc & UI.Boards.SplitScreen.SpyglassSunLayer) != 0;
            bool flat = (disc & UI.Boards.SplitScreen.FlatSeaLayer) != 0;
            ctx.Check(fromPane && sun != enhanced && copy == enhanced && flat == enhanced,
                $"{label}: a disc draws its pane's view less its own airframe, with {(enhanced ? "the copy in place of the sun and the flat-sea marker" : "the sun itself and no flat-sea marker")} (0x{disc:X5}: sun {sun}, copy {copy}, flat sea {flat})");
            ctx.Check(!enhanced || (disc & Mech3.VegetationModels.Layer) == 0,
                $"{label}: and under Enhanced draws no near vegetation model (0x{disc:X5})");
            ctx.Check((disc & UI.Boards.SplitScreen.WorldFlareBand) == 0
                && panes.Any(p => (p.Camera.CullMask & UI.Boards.SplitScreen.WorldFlareLayer(p.Index)) != 0),
                $"{label}: and leaves out the world flare, which its pane draws (0x{disc:X6})");
        }
    }

    // A zone the weather rig lights, crossed under Enhanced. C2/MP2's two zones disagree about the
    // bearing, so a copy that took the sun once at build would be left behind.
    private static void ZoneCrossing(TestContext ctx, ZrdrSet zrdr)
    {
        GraphicsMode.Set(true);
        var root = new Node3D { Name = "spyglass-sun-zone" };
        var sun = new DirectionalLight3D { Name = "spyglass-sun-zone-sun" };
        var camera = new Camera3D { Name = "spyglass-sun-zone-camera" };
        ctx.Host.AddChild(root);
        root.AddChild(sun);
        root.AddChild(camera);
        try
        {
            EnhancedLook.ApplySun(sun, true);
            var rigs = new List<Flight.Camera.PlayerRig> { new() { Index = 0, Camera = camera, HudParent = root } };
            var weather = new WeatherRig(SessionSpec.Parse(new[] { "--chapter=C2", "--mission=MP2" }), root, sun);
            weather.Build(zrdr, rigs, Array.Empty<Mech3.HorizonZone>(), _ => { });
            var copy = SpyglassSun.Build(sun);
            root.AddChild(copy);
            var below = Crossed(weather, rigs, Vector3.Zero, copy, sun);
            var inside = Crossed(weather, rigs, new Vector3(0f, 25000f, 0f), copy, sun);
            ctx.Same(1, below.State, $"a camera under C2/MP2's cloud band is in weather state 1");
            ctx.Same(2, inside.State, $"and one at 25,000 m is in state 2");
            ctx.Check(below.Beam.AngleTo(inside.Beam) > 0.6f,
                $"the crossing moved the sun by {Mathf.RadToDeg(below.Beam.AngleTo(inside.Beam)):0.#} degrees");
            ctx.Check(below.Matches && inside.Matches,
                $"and the copy matched the sun in both zones (below {below.Matches}, inside {inside.Matches}: {Light(sun)} against {Light(copy)})");
        }
        finally
        {
            root.QueueFree();
        }
    }

    // One crossing as a frame makes it: the rig resolves the camera's state and lights the zone,
    // then the copy takes its own frame.
    private static (Vector3 Beam, bool Matches, int State) Crossed(WeatherRig weather,
        List<Flight.Camera.PlayerRig> rigs, Vector3 at, SpyglassSun copy, DirectionalLight3D sun)
    {
        rigs[0].Camera.Position = at;
        weather.Tick(rigs);
        copy._Process(0.0);
        return (-copy.GlobalBasis.Z, SpyglassSun.Matches(sun, copy), rigs[0].CameraWeatherState);
    }

    private static int Count<T>(Node root)
        where T : Node
    {
        int count = 0;
        Walk(root, node =>
        {
            if (node is T)
                count++;
        });
        return count;
    }

    private static string Light(DirectionalLight3D light) => string.Create(CultureInfo.InvariantCulture,
        $"layers 0x{light.Layers:X5} casts {light.ShadowEnabled} energy {light.LightEnergy:0.###} colour {light.LightColor.ToHtml(false)} specular {light.LightSpecular:0.###} angular {light.LightAngularDistance:0.##} beam {-light.GlobalBasis.Z}");

    private static void RequireData(TestContext ctx, string? chapter = null)
    {
        chapter ??= ctx.Chapter;
        ctx.RequireData(ctx.Data.Planes, $"planes gamez");
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        ctx.RequireData(ctx.ChapterTextures(chapter), $"{chapter} textures");
        ctx.RequireData(ctx.ChapterGamez(chapter), $"{chapter} gamez");
    }

    // A Water Quality apply as the Options accept makes it, then the steps a reading waits for.
    private static void ApplyWater(Rig rig, string word)
    {
        WaterQualitySetting.Resolve(word, null, null);
        EnhancedLook.ApplyWaterQuality(rig.Session);
        Step(rig);
    }

    // A Vegetation Quality apply as the Options accept makes it, then the steps a reading waits for.
    private static void ApplyVegetation(Rig rig, string word)
    {
        VegetationQualitySetting.Resolve(word, null, null);
        EnhancedLook.ApplyVegetationQuality(rig.Session);
        Step(rig);
    }

    // The clump node standing in a session, its total instances, and its chunks with their counts.
    private static (Node3D? Node, int Count, string Text) Clumps(Rig rig)
    {
        var node = rig.Session.CanopyClumps;
        var chunks = node?.GetChildren().OfType<MultiMeshInstance3D>().Select(m => (m.Name.ToString(), m.Multimesh.InstanceCount)).ToList() ?? new();
        return (node, chunks.Sum(c => c.InstanceCount), string.Join(" ", chunks.Select(c => $"{c.Item1}={c.InstanceCount}")));
    }

    // The map edge's clump copies past the first corner, standing in the tree.
    private static int EdgeClumps(Rig rig)
    {
        if (rig.Session.EdgeExtender is not { } edge)
            return 0;
        edge.Update(edge.BeyondCorner);
        int count = 0;
        Walk(edge, node =>
        {
            if (node is MultiMeshInstance3D { Multimesh.InstanceCount: > 0 } && node.Name == Mech3.CanopyClumps.Field.CopyName
                && node.IsInsideTree() && !node.IsQueuedForDeletion())
                count++;
        });
        return count;
    }

    // Each shell piece's centre in world space: its corners share it in UV2, and its height is its corners' mean.
    private static IEnumerable<Vector3> Centres(MultiMeshInstance3D chunk)
    {
        var arrays = ((ArrayMesh)chunk.Multimesh.Mesh).SurfaceGetArrays(0);
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var centres = arrays[(int)Mesh.ArrayType.TexUV2].AsVector2Array();
        var seen = new Dictionary<Vector2, (float Y, int N)>();
        for (int v = 0; v < vertices.Length; v++)
        {
            var (y, n) = seen.GetValueOrDefault(centres[v]);
            seen[centres[v]] = (y + vertices[v].Y, n + 1);
        }
        return seen.Select(kv => chunk.Position + new Vector3(kv.Key.X, kv.Value.Y / kv.Value.N, kv.Key.Y));
    }

    // The near models' drawn cells under the model root, standing in the tree.
    private static List<MultiMeshInstance3D> ModelCells(Node root)
    {
        var cells = new List<MultiMeshInstance3D>();
        Walk(root, node =>
        {
            if (node is MultiMeshInstance3D m && m.IsInsideTree() && !m.IsQueuedForDeletion())
                cells.Add(m);
        });
        return cells;
    }

    // Every stamp the model cells draw in world space, by Key, sorted.
    private static List<string> ModelStamps(Node root) => ModelCells(root)
        .SelectMany(m => Enumerable.Range(0, m.Multimesh.InstanceCount).Select(i => m.Multimesh.GetInstanceTransform(i))
            .Select(xf => Key(new Transform3D(xf.Basis, m.Position + xf.Origin))))
        .OrderBy(k => k, StringComparer.Ordinal).ToList();

    // The model stamps collapsed to a zero basis: dropped by the mask, hidden or cratered.
    private static int ZeroModels(Node root) => ModelCells(root)
        .Sum(m => Enumerable.Range(0, m.Multimesh.InstanceCount).Count(i => m.Multimesh.GetInstanceTransform(i).Basis.Determinant() == 0f));

    // Where a stamp stands, to the centimetre. A hidden stamp keeps its place.
    private static string Key(Transform3D xf) => string.Create(CultureInfo.InvariantCulture,
        $"{Mathf.RoundToInt(xf.Origin.X * 100f)},{Mathf.RoundToInt(xf.Origin.Y * 100f)},{Mathf.RoundToInt(xf.Origin.Z * 100f)}");

    // The geometry nodes in the tree whose materials declare an instance uniform, and how many of them draw a near model.
    private static (int All, int Models) InstanceUniformNodes(Node root)
    {
        var declares = new Dictionary<Shader, bool>();
        bool Declares(Material? m)
        {
            for (; m != null; m = m.NextPass)
            {
                if (m is ShaderMaterial { Shader: { } s })
                {
                    if (!declares.TryGetValue(s, out bool d))
                        declares[s] = d = s.Code.Contains("instance uniform", StringComparison.Ordinal) || s.Code.Contains("csky_instance_uniforms", StringComparison.Ordinal);
                    if (d)
                        return true;
                }
            }
            return false;
        }
        int all = 0, models = 0;
        Walk(root, node =>
        {
            if (node is not GeometryInstance3D g || !g.IsInsideTree() || g.IsQueuedForDeletion())
                return;
            var mesh = g switch { MeshInstance3D mi => mi.Mesh, MultiMeshInstance3D mm => mm.Multimesh?.Mesh, _ => null };
            bool any = Declares(g.MaterialOverride) || Declares(g.MaterialOverlay);
            for (int s = 0; !any && mesh != null && s < mesh.GetSurfaceCount(); s++)
                any = Declares(mesh.SurfaceGetMaterial(s)) || (g is MeshInstance3D mi && Declares(mi.GetSurfaceOverrideMaterial(s)));
            if (!any)
                return;
            all++;
            if (node.Name.ToString().EndsWith("_model", StringComparison.Ordinal) || Under(node, "vegetation_models"))
                models++;
        });
        return (all, models);
    }

    // The map edge's near model copies standing shown in the tree.
    private static int EdgeModels(Rig rig)
    {
        int count = 0;
        if (rig.Session.EdgeExtender is { } edge)
        {
            Walk(edge, node =>
            {
                if (node is MultiMeshInstance3D { Visible: true } && node.Name.ToString().EndsWith("_model", StringComparison.Ordinal) && node.IsInsideTree() && !node.IsQueuedForDeletion())
                    count++;
            });
        }
        return count;
    }

    // A Ground Cover apply as the Options accept makes it, then the steps a reading waits for.
    private static void ApplyGroundCover(Rig rig, string word)
    {
        GroundCoverSetting.Resolve(word, null, null);
        EnhancedLook.FollowWordSettings(rig.Session);
        Step(rig);
    }

    // A 3D Vegetation apply as the Options accept makes it, then the steps a reading waits for.
    private static void Models(Rig rig, string word)
    {
        VegetationModelsSetting.Resolve(word, null, null);
        EnhancedLook.ApplyVegetationModels(rig.Session);
        Step(rig);
    }

    // The band each modelled card's Rich material carries.
    private static IEnumerable<Vector2> CardBands(IEnumerable<Mech3.ClutterBuilder.KindExport> kinds) =>
        kinds.Select(k => ((ShaderMaterial)k.Rich!.Material!).GetShaderParameter(Mech3.VegetationModels.BandParam).AsVector2());

    // Whether any collider stands under a node.
    private static bool HasCollider(Node root)
    {
        bool found = false;
        Walk(root, node => found |= node is CollisionObject3D or CollisionShape3D);
        return found;
    }

    // Whether every listed card's Rich material carries the handover band: on, off, or mixed.
    // The band on the cards the clumps take over: the original's and a forest set's. Another set's keep every distance.
    private static string Handover(Rig rig, bool forest = true)
    {
        var bands = rig.Session.Clutter?.ExportedKinds?.Where(k => k.Rich?.Material is ShaderMaterial && (!k.Added || k.Mass) == forest)
            .Select(k => ((ShaderMaterial)k.Rich!.Material!).GetShaderParameter(Mech3.CanopyClumps.HandoverParam).AsVector2())
            .Distinct().ToList() ?? new();
        return bands.Count != 1 ? $"mixed ({bands.Count})"
            : bands[0] == new Vector2(Mech3.CanopyClumps.HandoverNearM, Mech3.CanopyClumps.HandoverFarM) ? "on"
            : bands[0] == Vector2.Zero ? "off" : $"band {bands[0]}";
    }

    // The ocean nodes standing in the session's tree. Entering and leaving the tree sets the sheet's
    // switch. The server's getter for that errors outside the editor, so the tree is read instead.
    private static string OceanState(Rig rig) =>
        string.Create(CultureInfo.InvariantCulture, $"{OceansUnder(rig.Session)} in tree");

    // The ocean nodes standing in the tree under root. Read under the host, it also counts one
    // parented outside the session.
    private static int OceansUnder(Node root)
    {
        int count = 0;
        Walk(root, node =>
        {
            if (node is Effects.Ocean { } ocean && ocean.IsInsideTree())
                count++;
        });
        return count;
    }

    private static Effects.Ocean? FirstOcean(Node root)
    {
        Effects.Ocean? found = null;
        Walk(root, node => found ??= node as Effects.Ocean);
        return found;
    }

    private static List<UI.Labs.OceanLab> Labs(Node root)
    {
        var labs = new List<UI.Labs.OceanLab>();
        Walk(root, node =>
        {
            if (node is UI.Labs.OceanLab lab)
                labs.Add(lab);
        });
        return labs;
    }

    private static List<T> NodesOf<T>(Node root)
        where T : Node
    {
        var found = new List<T>();
        Walk(root, node =>
        {
            if (node is T t)
                found.Add(t);
        });
        return found;
    }

    private static string EntryText(Mech3.VegetationEntry entry) => Mech3.VegetationSidecar.WithEntry(null, VegetationChapter, entry);

    // Each export's Rich transforms and colours as baked now, null for a kind with no variant.
    private static List<(Transform3D[] Xf, Color[] Colors)?> RichOf(IReadOnlyList<Mech3.ClutterBuilder.KindExport> kinds) =>
        kinds.Select(k => k.Rich is { } r
            ? ((Transform3D[], Color[])?)(r.Placements.ToArray(), r.Colors?.ToArray() ?? Array.Empty<Color>())
            : null).ToList();

    // The added kinds' Rich transforms as baked now, each with its index among the exports.
    private static List<(int Index, string Model, Transform3D[] Rich)> AddedOf(IReadOnlyList<Mech3.ClutterBuilder.KindExport> kinds) =>
        kinds.Select((k, i) => (k, i)).Where(p => p.k.Added).Select(p => (p.i, p.k.Model, p.k.Rich!.Placements.ToArray())).ToList();

    private static bool SameAdded(List<(int Index, string Model, Transform3D[] Rich)> a, List<(int Index, string Model, Transform3D[] Rich)> b) =>
        a.Count == b.Count && a.Zip(b).All(p => p.First.Model == p.Second.Model && p.First.Rich.SequenceEqual(p.Second.Rich));

    // The added stamps the clutter draws now: those standing in their visible node's buffers.
    private static int AddedDrawn(Rig rig)
    {
        int drawn = 0;
        Walk(rig.Session, node =>
        {
            if (node is MultiMeshInstance3D { Multimesh: { } mm } mmi && Under(mmi, Mech3.ClutterBuilder.AddedRootName) && mmi.IsVisibleInTree() && !mmi.IsQueuedForDeletion())
            {
                for (int i = 0; i < mm.InstanceCount; i++)
                    drawn += mm.GetInstanceTransform(i).Basis.Determinant() != 0f ? 1 : 0;
            }
        });
        return drawn;
    }

    // Whether a point's XZ falls in any of the triangles' XZ shadows, three corners each.
    private static bool OnTriangles(Vector3[] corners, Vector3 p)
    {
        for (int t = 0; t + 2 < corners.Length; t += 3)
        {
            Vector2 a = new(corners[t].X, corners[t].Z), b = new(corners[t + 1].X, corners[t + 1].Z), c = new(corners[t + 2].X, corners[t + 2].Z), q = new(p.X, p.Z);
            float d0 = (b - a).Cross(q - a), d1 = (c - b).Cross(q - b), d2 = (a - c).Cross(q - c);
            if ((d0 >= -0.01f && d1 >= -0.01f && d2 >= -0.01f) || (d0 <= 0.01f && d1 <= 0.01f && d2 <= 0.01f))
                return true;
        }
        return false;
    }

    private static bool SameRich((Transform3D[] Xf, Color[] Colors)? a, (Transform3D[] Xf, Color[] Colors)? b) =>
        a is not { } x ? b is null : b is { } y && x.Xf.SequenceEqual(y.Xf) && x.Colors.SequenceEqual(y.Colors);

    private static bool SameRich(List<(Transform3D[] Xf, Color[] Colors)?> a, List<(Transform3D[] Xf, Color[] Colors)?> b) =>
        a.Count == b.Count && a.Zip(b).All(p => SameRich(p.First, p.Second));

    private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    // The drawn map stamps within a radius of a point that draw something.
    private static int DrawnIn(DrawnReading reading, Vector3 at, float radius) =>
        reading.Stamps.Count(s => s.Item4 != 0f && Flat(new Vector3(s.Item1, 0f, s.Item3), at) < radius);

    // A stamp the mask dropped, inside the cleared disk and not hidden by its node: (export, placement), or null.
    private static (int K, int I)? DroppedStamp(IReadOnlyList<Mech3.ClutterBuilder.KindExport> kinds,
        Mech3.ClutterActivation? activation, Vector3 at, float inner, Func<int, bool> wanted)
    {
        for (int k = 0; k < kinds.Count; k++)
        {
            if (kinds[k].Rich is not { } rich || kinds[k].Instances == null)
                continue;
            for (int i = 0; i < rich.Placements.Count; i++)
            {
                if (rich.Placements[i].Basis.Determinant() == 0f && Flat(rich.Placements[i].Origin, at) < inner
                    && activation?.IsHidden(k, i) != true && wanted(kinds[k].Owners[i]))
                    return (k, i);
            }
        }
        return null;
    }

    // A dropped stamp hidden with its node under rich stays hidden after a live move to original until the node shows.
    // A hide and show back under rich leaves it dropped.
    private static void ActivationThroughDrop(TestContext ctx, Rig rig, IReadOnlyList<Mech3.ClutterBuilder.KindExport> kinds,
        Vector3 at, float inner, StringBuilder report)
    {
        var activation = rig.Session.Clutter?.Activation;
        var owners = new Dictionary<int, Node3D>();
        Walk(rig.Session, node =>
        {
            if (node is Node3D n && n.HasMeta(Mech3.AnimRuntime.IndexMeta))
                owners.TryAdd(n.GetMeta(Mech3.AnimRuntime.IndexMeta).AsInt32(), n);
        });
        if (activation == null || DroppedStamp(kinds, activation, at, inner, o => owners.TryGetValue(o, out var n) && n.IsVisibleInTree()) is not { } found)
        {
            ctx.Check(false, $"a dropped stamp in the cleared disk has a shown stamping node (activation {(activation != null ? "bound" : "missing")})");
            return;
        }
        var (k, i) = found;
        var stamps = kinds[k].Instances!;
        var node = owners[kinds[k].Owners[i]];
        node.Visible = false;
        bool hiddenRich = activation.IsHidden(k, i) && stamps.Moved(i);
        ApplyVegetation(rig, VegetationQualitySetting.Original);
        bool hiddenOriginal = stamps.GetInstanceTransform(i).Basis.Determinant() == 0f;
        node.Visible = true;
        bool shownOriginal = stamps.GetInstanceTransform(i) == kinds[k].Placements[i];
        ApplyVegetation(rig, VegetationQualitySetting.Rich);
        node.Visible = false;
        node.Visible = true;
        bool droppedRich = stamps.GetInstanceTransform(i).Basis.Determinant() == 0f && !stamps.Moved(i) && !activation.IsHidden(k, i);
        ctx.Check(hiddenRich && hiddenOriginal && shownOriginal && droppedRich,
            $"a dropped {kinds[k].Model} stamp hides with its node '{node.Name}' under rich ({hiddenRich}), stays hidden after a live move to original ({hiddenOriginal}), draws when the node shows there ({shownOriginal}), and stays dropped through a hide and show back under rich ({droppedRich})");
        report.AppendLine(CultureInfo.InvariantCulture, $"activation: {kinds[k].Model} stamp {i} of node '{node.Name}'");
    }

    // The paint tool driven through the pane's input, in a session that builds no world collider.
    // The ring follows the hover at the brush radius and hides off the brush, the panel, the sky and a closed lab.
    // A held Clear stroke drops the listed Rich stamps under the cursor alone.
    private static void ScreenStroke(TestContext ctx, Rig rig, UI.Labs.VegetationLab lab, IReadOnlyList<Mech3.ClutterBuilder.KindExport> kinds,
        List<(Transform3D[] Xf, Color[] Colors)?> opening, Vector3 at, float radius, float margin, StringBuilder report)
    {
        int colliders = NodesOf<CollisionObject3D>(rig.Session).Count(c => (c.CollisionLayer & Flight.Airframe.CollisionLayers.World) != 0);
        var camera = rig.Pane.GetCamera3D();
        if (camera == null || ClutterRoot(rig.Session) is not { } root)
        {
            ctx.Check(false, $"the --freecam pane has a camera and a clutter root to aim the paint tool with");
            return;
        }
        var stamp = root.GlobalTransform * at;
        var screen = new Vector2(120f, 240f);
        // Steeply down, then slid along that pixel's ray until it passes through the stamp, left of the panel.
        camera.GlobalTransform = new Transform3D(Basis.FromEuler(new Vector3(Mathf.DegToRad(-75f), 0f, 0f)), Vector3.Zero);
        camera.GlobalPosition = stamp - (camera.ProjectRayNormal(screen) * 600f);
        void Push(InputEvent e) => rig.Pane.PushInput(e, true);
        void Move(Vector2 p) => Push(new InputEventMouseMotion { Position = p, GlobalPosition = p });
        bool RingAt(out Vector3 hit)
        {
            hit = lab.Ring?.GlobalPosition ?? Vector3.Zero;
            return lab.Ring is { Visible: true } r && Mathf.IsEqualApprox(r.Basis.Scale.X, radius) && Flat(hit, stamp) < 5f;
        }

        lab.Tool = UI.Labs.VegetationLab.PaintTool.Brush;
        lab.Brush = Mech3.VegetationBrush.Clear;
        lab.BrushRadius = radius;
        lab.BrushStrength = 1f;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Move(screen);
        long firstMs = watch.ElapsedMilliseconds;
        bool hovered = RingAt(out var ground);
        watch.Restart();
        const int Rays = 20;
        for (int i = 0; i < Rays; i++)
            Move(screen);
        double rayMs = watch.Elapsed.TotalMilliseconds / Rays;
        ctx.Check(colliders == 0 && hovered,
            $"with {colliders} world collider(s), as --freecam builds, a hover in Brush stands the ring on the ground under the cursor at the brush radius ({Flat(ground, stamp):0.0} m from the aimed stamp; first ray with the triangle build {firstMs} ms, then {rayMs:0.00} ms a ray)");

        Push(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = screen, GlobalPosition = screen });
        Move(screen);
        Push(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = screen, GlobalPosition = screen });
        Step(rig);
        var centre = root.GlobalTransform.AffineInverse() * ground;
        var now = RichOf(kinds);
        int dropped = 0, wrong = 0;
        for (int k = 0; k < kinds.Count; k++)
        {
            if (opening[k] is not { } open)
                continue;
            for (int i = 0; i < open.Xf.Length; i++)
            {
                float d = Flat(open.Xf[i].Origin, centre);
                bool zero = now[k]!.Value.Xf[i].Basis.Determinant() == 0f;
                if (d < radius - margin)
                    dropped += zero ? 1 : 0;
                if ((d < radius - margin && !zero) || (d > radius + margin && now[k]!.Value.Xf[i] != open.Xf[i]))
                    wrong++;
            }
        }
        ctx.Check(dropped > 0 && wrong == 0,
            $"a left-button stroke through the pane's input drops the {dropped} listed Rich stamps under the cursor and no other ({wrong} wrong)");

        Move(new Vector2(500f, 240f));
        bool overPanel = lab.Ring is { Visible: false };
        Move(screen);
        lab.Tool = UI.Labs.VegetationLab.PaintTool.Pick;
        Move(screen);
        bool offBrush = lab.Ring is { Visible: false };
        lab.Tool = UI.Labs.VegetationLab.PaintTool.Brush;
        camera.GlobalTransform = new Transform3D(Basis.FromEuler(new Vector3(Mathf.DegToRad(80f), 0f, 0f)), camera.GlobalPosition);
        Move(screen);
        bool overSky = lab.Ring is { Visible: false };
        string skyHit = overSky ? "" : $" at {lab.Ring?.GlobalPosition}";
        camera.GlobalTransform = new Transform3D(Basis.FromEuler(new Vector3(Mathf.DegToRad(-75f), 0f, 0f)), camera.GlobalPosition);
        Move(screen);
        bool back = RingAt(out _);
        lab.Toggle();
        bool closed = lab.Ring is { Visible: false };
        lab.Toggle();
        ctx.Check(overPanel && offBrush && overSky && back && closed,
            $"the ring hides over the panel ({overPanel}), off the brush ({offBrush}), over the sky ({overSky}{skyHit}) and with the lab closed ({closed}), and stands again on the ground ({back})");

        lab.Tool = UI.Labs.VegetationLab.PaintTool.Off;
        lab.RevertToSaved();
        Step(rig);
        report.AppendLine(CultureInfo.InvariantCulture, $"screen stroke: {colliders} world colliders, ground {ground} for stamp {stamp}, first ray {firstMs} ms, {rayMs:0.00} ms a ray, {dropped} dropped");
    }

    // A crater on a dropped stamp counts nothing and leaves it drawing nothing under rich, and flattens it for original.
    private static void CraterThroughDrop(TestContext ctx, Rig rig, UI.Labs.VegetationLab lab,
        IReadOnlyList<Mech3.ClutterBuilder.KindExport> kinds, Vector3 at, float radius, float inner, StringBuilder report)
    {
        lab.Paint(at, Mech3.VegetationBrush.Clear, radius, 1f);
        lab.ApplyMask();
        Step(rig);
        if (ClutterRoot(rig.Session) is not { } root || DroppedStamp(kinds, rig.Session.Clutter?.Activation, at, inner, _ => true) is not { } found)
        {
            ctx.Check(false, $"a cleared disk holds a dropped stamp to crater");
            return;
        }
        var (k, i) = found;
        var stamps = kinds[k].Instances!;
        var impact = root.GlobalTransform * kinds[k].Rich!.Placements[i].Origin;
        int killed = Mech3.ClutterCull.Destroy(root, Mech3.CraterShape.At(impact, radius: 0.01f));
        bool richHidden = stamps.GetInstanceTransform(i).Basis.Determinant() == 0f && stamps.Moved(i);
        ApplyVegetation(rig, VegetationQualitySetting.Original);
        bool originalHidden = stamps.GetInstanceTransform(i).Basis.Determinant() == 0f;
        ApplyVegetation(rig, VegetationQualitySetting.Rich);
        ctx.Check(killed == 0 && richHidden && originalHidden,
            $"a crater on a dropped {kinds[k].Model} stamp counts nothing ({killed}), flattens it under rich ({richHidden}) and keeps it flattened after a live move to original ({originalHidden})");
        report.AppendLine(CultureInfo.InvariantCulture, $"crater: {kinds[k].Model} stamp {i}, {killed} counted");
    }

    // Save from a lab of its own over a temp copy of the sidecar, so the shipped file and mask are never written.
    private static void SaveToCopy(TestContext ctx, Mech3.VegetationEntry saved, string model, Vector3 at, Mech3.VegetationMask area, StringBuilder report)
    {
        string source = ProjectSettings.GlobalizePath("res://data/vegetation.json");
        string shippedMask = ProjectSettings.GlobalizePath($"res://data/{Mech3.VegetationMask.PathFor(VegetationChapter)}");
        byte[] sourceBytes = System.IO.File.ReadAllBytes(source);
        bool maskBefore = System.IO.File.Exists(shippedMask);
        string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"csvm-vegetation-lab-{System.Environment.ProcessId}");
        string copy = System.IO.Path.Combine(dir, "vegetation.json");
        System.IO.Directory.CreateDirectory(dir);
        System.IO.File.Copy(source, copy, overwrite: true);
        var mask = Mech3.VegetationMask.Neutral(area.MinX, area.MinZ, area.MaxX, area.MaxZ, area.Side);
        var lab = new UI.Labs.VegetationLab(VegetationChapter, saved, _ => { }, copy, mask, _ => { });
        try
        {
            float tint = saved.Find(model)!.Tint == 0.3f ? 0.4f : 0.3f;
            lab.Set(model, "tint", tint);
            lab.Paint(at, Mech3.VegetationBrush.Clear, 100f, 1f);
            string? written = lab.Save();
            var back = Mech3.VegetationSidecar.Parse(System.IO.File.ReadAllText(copy)).For(VegetationChapter);
            string maskFile = System.IO.Path.Combine(dir, back.Mask);
            bool maskWritten = back.Mask == Mech3.VegetationMask.PathFor(VegetationChapter) && System.IO.File.Exists(maskFile)
                && Mech3.VegetationMask.FromBytes(System.IO.File.ReadAllBytes(maskFile), area.MinX, area.MinZ, area.MaxX, area.MaxZ).At(at.X, at.Z) == 0f;
            ctx.Check(written == copy && back.Find(model)?.Tint == tint && maskWritten,
                $"Save writes the {model} tint and the painted mask into the temp copy (written {written ?? "nothing"}, mask {back.Mask})");
            ctx.Check(System.IO.File.ReadAllBytes(source).SequenceEqual(sourceBytes) && System.IO.File.Exists(shippedMask) == maskBefore,
                $"and leaves the shipped sidecar and mask alone");
            report.AppendLine(CultureInfo.InvariantCulture, $"save: {copy}, mask {maskFile}");
        }
        finally
        {
            lab.Free();
            System.IO.Directory.Delete(dir, recursive: true);
        }
    }

    // The material every grid of the ocean shares.
    private static ShaderMaterial? Grid(Effects.Ocean ocean) =>
        ocean.GetNodeOrNull<MeshInstance3D>("OceanGrid")?.Mesh?.SurfaceGetMaterial(0) as ShaderMaterial;

    // A closed session's ocean has left the tree, which is what puts the sheet's switch back.
    private static void Left(TestContext ctx, Effects.Ocean? ocean, string what)
    {
        bool gone = ocean == null || !GodotObject.IsInstanceValid(ocean) || !ocean.IsInsideTree();
        ctx.Check(ocean != null && gone && OceansUnder(ctx.Host) == 0,
            $"closing {what} leaves no ocean in the tree (held ocean {(ocean == null ? "never built" : gone ? "gone" : "still in the tree")}, {OceansUnder(ctx.Host)} under the host)");
    }

    // The sheet's hide steps aside only where this ocean draws. Its mask is the one the hide reads,
    // and the include gates on the coverage below which the ocean's own fragment discards. Neither
    // the grid nor the hide acts for a spyglass disc's camera.
    private static void HideReadsTheMask(TestContext ctx, Rig rig, Effects.Ocean? ocean)
    {
        string sheet = "";
        foreach (var (material, texture) in rig.Session.WorldScene?.TexturedMaterials ?? Array.Empty<(ShaderMaterial, string)>())
        {
            if (Mech3.SceneBuilder.IsOceanBaseTexture(texture) && material.Shader?.Code is { } code
                && code.Contains(HideCall, StringComparison.Ordinal))
            {
                sheet = code;
                break;
            }
        }
        string threshold = Effects.OceanMask.SeaThreshold.ToString("0.0#", CultureInfo.InvariantCulture);
        string include = ResourceLoader.Load<ShaderInclude>(OceanIncludePath)?.Code ?? "";
        string grid = ocean?.GetNodeOrNull<MeshInstance3D>("OceanGrid")?.Mesh?.SurfaceGetMaterial(0) is ShaderMaterial { Shader: { } shader }
            ? shader.Code : "";
        ctx.Check(ocean != null && Effects.OceanMask.Live != null,
            $"the standing ocean hands its mask to the sheet's hide");
        ctx.Check(include.Contains($"csky_ocean_sea(world.xz) < {threshold}", StringComparison.Ordinal)
                && grid.Contains($"m.r < {threshold}", StringComparison.Ordinal),
            $"the sheet's hide steps aside at the coverage the ocean discards below, {threshold} (include {(include.Length > 0 ? "read" : "missing")}, grid shader {(grid.Length > 0 ? "read" : "missing")})");
        ctx.Check(include.Contains($"abs(zone - {Effects.OceanMaskRaster.SeamZone}.0)", StringComparison.Ordinal),
            $"and leaves a zone seam texel, which no grid draws, to the sheet");
        string hide = Effects.OceanMask.HideHeight.ToString("0.0#", CultureInfo.InvariantCulture);
        string top = Effects.OceanMask.RampTop.ToString("0.0#", CultureInfo.InvariantCulture);
        ctx.Check(include.Contains($"world.y <= -{hide} || world.y >= {top})", StringComparison.Ordinal)
                && include.Contains($"world.y >= {hide} && !csky_ocean_ramp(", StringComparison.Ordinal)
                && include.Contains($"r * 255.0 - {Effects.OceanMaskRaster.RampSea}.0", StringComparison.Ordinal),
            $"and clears the sheet within {hide} m of sea level, and a ramp's up to {top} m where its filter reads the ramp byte {Effects.OceanMaskRaster.RampSea}");
        ctx.Check(grid.Contains($"if ({Mech3.SceneBuilder.FlatSeaEye}) {{", StringComparison.Ordinal)
                && sheet.Length > 0 && sheet.Contains($"!{Mech3.SceneBuilder.FlatSeaEye} && {HideCall}", StringComparison.Ordinal),
            $"a spyglass disc's camera collapses the grid before its wave sum and keeps the sheet whole ({Mech3.SceneBuilder.FlatSeaEye}; sheet {(sheet.Length > 0 ? "read" : "missing")})");
    }

    // A hull held at start for 20 s of sim time, then moved along its own -Z at 10 m/s for 20 s.
    // A fresh clock runs at 30 frames a second, the ocean publishing each frame. Reads the wake
    // slots drawn after each half, and the uniforms the second half ends on.
    private static WakeDrive DriveWake(Effects.Ocean ocean, Node3D hull, Vector3 start)
    {
        var clock = new GameClock();
        GameClock.Current = clock;
        var heading = -hull.GlobalBasis.Z with { Y = 0f };
        heading = heading.Normalized();
        int standing = Run(t => start, 0.0);
        int moving = Run(t => start + (heading * (float)(10.0 * (t - 20.0))), 20.0);
        var material = Grid(ocean);
        var bounds = material?.GetShaderParameter("wake_bounds").AsVector4Array() ?? Array.Empty<Vector4>();
        var track = material?.GetShaderParameter("wake_track").AsVector4Array() ?? Array.Empty<Vector4>();
        return new WakeDrive(standing, moving, bounds.Sum(b => b.W), bounds, track);

        int Run(Func<double, Vector3> path, double from)
        {
            for (int i = 0; i < 600; i++)
            {
                clock.BeginFrame(1.0 / 30.0);
                hull.GlobalPosition = path(Math.Max(clock.Time, from));
                ocean._Process(1.0 / 30.0);
            }
            var drawn = Grid(ocean)?.GetShaderParameter("wake_bounds").AsVector4Array() ?? Array.Empty<Vector4>();
            return drawn.Count(b => b.Z > 0f);
        }
    }

    // Whether a pane camera draws the instance. A wake sheet that steps aside sits on the flat-sea layer alone.
    private static bool PaneDraws(VisualInstance3D instance) =>
        (instance.Layers & UI.Boards.SplitScreen.PaneCullMask(uint.MaxValue)) != 0;

    private static bool SteppedAside(VisualInstance3D instance) =>
        instance.Layers == UI.Boards.SplitScreen.FlatSeaLayer && !PaneDraws(instance);

    private static string SheetLayers(IEnumerable<MeshInstance3D> sheets) =>
        string.Join(", ", sheets.Select(s => string.Create(CultureInfo.InvariantCulture, $"{s.GetParent()?.Name}=0x{s.Layers:X}")));

    // Every drawn base-sheet triangle read at its centroid and near each corner. A sample over a sea
    // texel must be one the hide clears. A ramp rises off sea level to under OceanMask.RampTop, and
    // its shortfall is how far the lifted ocean stands below it.
    private static SheetReading SheetOverSea(Rig rig, Effects.OceanMask mask)
    {
        var scene = rig.Session.WorldScene!;
        var names = new Dictionary<Material, string>();
        foreach (var (material, texture) in scene.TexturedMaterials)
            names[material] = texture;
        int sampled = 0, missed = 0, casting = 0, ramps = 0, rampMissed = 0;
        float gap = 0f;
        Vector3? flat = null;
        var examples = new StringBuilder();
        Walk(rig.Session, node =>
        {
            if (node is not MeshInstance3D { Mesh: ArrayMesh mesh } mi || !mi.IsVisibleInTree() || UnderOcean(mi))
                return;
            var xf = mi.GlobalTransform;
            for (int s = 0; s < mesh.GetSurfaceCount(); s++)
            {
                var material = mi.GetSurfaceOverrideMaterial(s) ?? mesh.SurfaceGetMaterial(s);
                if (material == null || !names.TryGetValue(material, out var texture) || !Mech3.SceneBuilder.IsOceanBaseTexture(texture))
                    continue;
                var arrays = scene.SurfaceArrays(mesh, s);
                var v = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var ix = arrays[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil
                    ? Enumerable.Range(0, v.Length).ToArray() : arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                for (int t = 0; t + 2 < ix.Length; t += 3)
                {
                    var a = xf * v[ix[t]];
                    var b = xf * v[ix[t + 1]];
                    var c = xf * v[ix[t + 2]];
                    float minY = Math.Min(a.Y, Math.Min(b.Y, c.Y)), maxY = Math.Max(a.Y, Math.Max(b.Y, c.Y));
                    bool ramp = minY > -0.5f && minY < 0.5f && maxY >= Effects.OceanMask.HideHeight && maxY < Effects.OceanMask.RampTop;
                    ramps += ramp && (b - a).Cross(c - a).Length() > 1e-3f ? 1 : 0;
                    var centre = (a + b + c) / 3f;
                    var samples = new[] { centre, a.Lerp(centre, 0.15f), b.Lerp(centre, 0.15f), c.Lerp(centre, 0.15f) };
                    if (ramp)
                        samples = samples.Concat(new[] { a.Lerp(centre, 0.02f), b.Lerp(centre, 0.02f), c.Lerp(centre, 0.02f) }).ToArray();
                    foreach (var p in samples)
                    {
                        if (ramp)
                            gap = Math.Max(gap, p.Y - mask.LiftAt(p));
                        if (!mask.IsSea(p))
                            continue;
                        sampled++;
                        if (!ramp && maxY < 0.5f)
                            flat ??= p;
                        if (mask.ClearsSheet(p))
                            continue;
                        missed++;
                        rampMissed += ramp ? 1 : 0;
                        casting += mi.CastShadow != GeometryInstance3D.ShadowCastingSetting.Off ? 1 : 0;
                        if (missed <= 4)
                            examples.Append(string.Create(CultureInfo.InvariantCulture, $"; {mi.GetParent()?.Name}/{mi.Name} at ({p.X:0.0},{p.Y:0.00},{p.Z:0.0})"));
                    }
                }
            }
        });
        return new SheetReading(sampled, missed, casting, ramps, rampMissed, gap, flat, examples.ToString());
    }

    private static bool UnderOcean(Node node)
    {
        for (Node? n = node; n != null; n = n.GetParent())
        {
            if (n is Effects.Ocean)
                return true;
        }
        return false;
    }

    // The base sheet's materials as the world builder named them, by texture and shader text. Also
    // how many carry the hide and are drawn.
    private static SeaReading Sea(Rig rig)
    {
        var sheet = new HashSet<ShaderMaterial>();
        var census = new SortedDictionary<string, int>(StringComparer.Ordinal);
        int hidden = 0;
        foreach (var (material, texture) in rig.Session.WorldScene?.TexturedMaterials ?? Array.Empty<(ShaderMaterial, string)>())
        {
            if (!Mech3.SceneBuilder.IsOceanBaseTexture(texture) || !sheet.Add(material))
                continue;
            string code = material.Shader?.Code ?? "";
            hidden += code.Contains(HideCall, StringComparison.Ordinal) ? 1 : 0;
            Count(census, texture.ToLowerInvariant() + ":" + code.GetHashCode().ToString("x8", CultureInfo.InvariantCulture));
        }
        int drawn = 0;
        Walk(rig.Session, node =>
        {
            if (node is GeometryInstance3D geometry && geometry.IsVisibleInTree())
                drawn += Materials(geometry).Count(sheet.Contains);
        });
        return new SeaReading(OceansUnder(rig.Session), sheet.Count, hidden, drawn, Print(census));
    }

    // The switch as the launcher makes it, then the steps a reading waits for.
    private static void Switch(Rig rig, bool enhanced)
    {
        EnhancedLook.Switch(enhanced, rig.Sun, rig.Env, EnhancedPasses.None, rig.Session, "graphics-live-switch");
        Step(rig);
    }

    private static void Step(Rig rig)
    {
        for (int i = 0; i < Steps; i++)
            rig.Session._PhysicsProcess(GameClock.FixedDt);
    }

    // One flight session in its own pane, lit as the launcher lights one. The sun and the
    // Environment are the launcher's, dressed by EnhancedLook when the session opens under Enhanced.
    private static Rig Open(TestContext ctx, bool enhanced, int players = 1, string? chapter = null, params string[] extra)
    {
        GraphicsMode.Set(enhanced);
        Mech3.ShaderTwins.Regenerate();
        EffectsLevel.RegisteredScaleSq = EnhancedLook.ClutterFadeScaleSq();
        RenderingServer.GlobalShaderParameterSet(EffectsLevel.ShaderParam, EffectsLevel.RegisteredScaleSq);
        var spec = SessionSpec.Parse(new[] { $"--chapter={chapter ?? ctx.Chapter}", $"--players={players}", "--mute", "--no-pads" }.Concat(extra));
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
            EnhancedLook.ApplySun(sun, true);
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
        var paths = new BasePaths(
            RepoRoot: ctx.RepoRoot,
            DataRoot: ctx.DataRoot)
        { Game = ctx.Game, Data = ctx.Data };
        var session = new GameSession(spec, new LauncherContext
        {
            Decode = ctx.Decode,
            Paths = paths,
            ProbeRunner = new ProbeRunner(paths),
            CaptureDirector = new CaptureDirector(spec),
            MasterSeed = 1,
            Camera = camera,
            Orbit = new Flight.Camera.OrbitCamera(camera),
            Sun = sun,
            Env = env,
            MenuDriven = false,
            MenuPads = null,
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
            if (name is "scorch_field" or "wind_streaks" or "heat_shimmer" or "ground_shadows")
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
                    string key = material.Shader.Code.GetHashCode().ToString("x8", CultureInfo.InvariantCulture) + ":" + (Mech3.ShaderTwins.FamilyOf(material.Shader) ?? geometry.GetType().Name);
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

    // Every stamp the map's clutter cells draw, sorted, read off their buffers in world space. Each
    // carries its basis' X axis and its instance colour, white without one. Beside it, the cells
    // drawing an instance colour on the map and past its edge.
    private static DrawnReading DrawnStamps(Rig rig)
    {
        rig.Session.EdgeExtender?.Update(rig.Session.EdgeExtender.BeyondCorner);
        var stamps = new List<(float, float, float, float, float, float, float)>();
        int colored = 0, edgeColored = 0;
        Walk(rig.Session, node =>
        {
            if (node is not MultiMeshInstance3D { Multimesh: { } mm } mmi || mmi.IsQueuedForDeletion())
                return;
            if (Under(mmi, "map_edge"))
            {
                edgeColored += mm.UseColors ? 1 : 0;
                return;
            }
            if (!UnderClutter(mmi))
                return;
            colored += mm.UseColors ? 1 : 0;
            for (int i = 0; i < mm.InstanceCount; i++)
            {
                var xf = mm.GetInstanceTransform(i);
                var o = mmi.Position + xf.Origin;
                var c = mm.UseColors ? mm.GetInstanceColor(i) : Colors.White;
                stamps.Add((o.X, o.Y, o.Z, xf.Basis.X.X, c.R, c.G, c.B));
            }
        });
        stamps.Sort();
        return new DrawnReading(stamps, colored, edgeColored);
    }

    private static bool Under(Node node, string name)
    {
        for (var at = node.GetParent(); at != null; at = at.GetParent())
        {
            if (at.Name == name)
                return true;
        }
        return false;
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

    // The first followed material under root whose text takes a fade line, or null.
    private static ShaderMaterial? FadeSource(Node root)
    {
        ShaderMaterial? found = null;
        Walk(root, node =>
        {
            if (found == null && node is GeometryInstance3D geometry)
                found = Materials(geometry).FirstOrDefault(m => Mech3.ShaderTwins.IsTracked(m)
                    && Mech3.ShaderTwins.FadeCode(m.Shader.Code) != null);
        });
        return found;
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

    private sealed record SheetReading(int Sampled, int Missed, int Casting, int Ramps, int RampMissed, float RampShortfall, Vector3? FlatSea, string Examples);

    private sealed record SeaReading(int Oceans, int Materials, int Hidden, int Drawn, string Census)
    {
        public override string ToString() => string.Create(CultureInfo.InvariantCulture,
            $"{Oceans} ocean(s), {Materials} base sheet material(s), {Hidden} hiding, {Drawn} drawn surface(s)");

        public string Print() => $"  {this}\n  census {Census}";
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

    private sealed record DrawnReading(List<(float, float, float, float, float, float, float)> Stamps, int Colored, int EdgeColored)
    {
        public override string ToString() => string.Create(CultureInfo.InvariantCulture,
            $"{Stamps.Count} stamps drawn, {Colored} coloured cell(s), {EdgeColored} coloured edge kind(s)");
    }

    private sealed record VertexReading(int Nodes, int Materials, long Vertices, string Text)
    {
        public override string ToString() => string.Create(CultureInfo.InvariantCulture,
            $"{Vertices} vertices of {Materials} material(s) in {Nodes} surface draw(s)");
    }

    // The wake slots drawn after a hull stood still and after it moved, the track length, and the
    // uniforms published last.
    private sealed record WakeDrive(int Standing, int Moving, float Arc, Vector4[] Bounds, Vector4[] Track);

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
