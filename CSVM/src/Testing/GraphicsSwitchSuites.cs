using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CSVM.Extraction;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Launch;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.Tooling;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The live graphics-mode switch, View Distance and Water Quality on whole flight sessions.
/// Each runs through the one sequence the launcher runs (<see cref="EnhancedLook.Switch"/>,
/// <see cref="EnhancedLook.ApplyViewDistance"/>, <see cref="EnhancedLook.ApplyWaterQuality"/>). A world switched away and
/// back must read as a fresh session in that mode. That covers the layers only one mode builds, the
/// clutter's cells and ranges, and the shader text on every material. It covers the Environment's
/// passes and the sun's shadow too. The pixels are the montage's, not this suite's.</summary>
internal static class GraphicsSwitchSuites
{
    // Physics steps between a switch and the reading. The world lights free or grow their omni pool
    // on a commit, so a reading taken before one would show the pool the switch left.
    private const int Steps = 3;

    // The one chapter the wave ocean covers (Effects.Ocean.Covers).
    private const string OceanChapter = "C1B";

    // A chapter whose whole-map wtr sheet the ocean does not cover. Its sheet carries the same
    // collapse under Enhanced, so a switch a closed session left on would hole its whole sea.
    private const string UncoveredSeaChapter = "C2B";

    // The base sheet's vertex-stage call. The include line alone never matches it.
    private const string HideCall = "csky_ocean_hides_sea(";

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
        + "matches the sun's bearing, colour and energy: in a two-pane flight with one disc per pane, "
        + "at every Shadow Quality level including Off, across a zone crossing the weather rig lights, "
        + "and after a live switch to Original (copy freed, sun back on layer 1, each disc the pane's "
        + "view less its own airframe) and back; a fresh Original flight builds no copy and moves no layer")]
    internal static void SpyglassSunLayers(TestContext ctx)
    {
        RequireData(ctx);
        string crossZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C2", "MP2");
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

    [Suite("graphics-ocean-switch",
        "the C1B wave ocean follows a live graphics switch at water quality waves: an Enhanced build "
        + "stands one ocean and every sea-level base sheet material carries the vertex collapse; "
        + "switched to Original the ocean leaves the tree and the sheet carries a fresh Original "
        + "build's text, with no collapse; back to Enhanced exactly one ocean stands and the sheet "
        + "carries a fresh Enhanced build's text; an Original build switched to Enhanced builds the "
        + "ocean; a closed session leaves no ocean in the tree, and a following Enhanced session on "
        + "a sea chapter the ocean does not cover builds none")]
    internal static void OceanFollowsSwitch(TestContext ctx)
    {
        RequireData(ctx, OceanChapter);
        RequireData(ctx, UncoveredSeaChapter);
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
                $"an Original C1B build stands no ocean and its base sheet carries no collapse ({freshOriginal})");
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
                    $"an Enhanced C1B build stands one ocean and every base sheet material collapses its sea-level vertices ({freshEnhanced})");
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
                    $"and the base sheet carries a fresh Original build's text, with no collapse ({switchedOriginal.Hidden} collapsing)");

                Switch(enhanced, true);
                var roundTrip = Sea(enhanced);
                var rebuilt = FirstOcean(ctx.Host);
                ctx.Check(roundTrip.Oceans == 1 && OceansUnder(ctx.Host) == 1 && rebuilt != null && !ReferenceEquals(rebuilt, builtFresh),
                    $"back to Enhanced the ocean is built again, exactly once ({roundTrip}, {OceansUnder(ctx.Host)} under the host)");
                ctx.Check(roundTrip.Hidden == roundTrip.Materials && roundTrip.Census == freshEnhanced.Census,
                    $"and the base sheet carries a fresh Enhanced build's text, hidden again ({roundTrip.Hidden} of {roundTrip.Materials} collapsing)");
                report.AppendLine($"fresh enhanced\n{freshEnhanced.Print()}\nswitched original\n{switchedOriginal.Print()}\nround trip\n{roundTrip.Print()}");
                builtFresh = rebuilt;
            }
            finally
            {
                enhanced.Close();
            }
            Left(ctx, builtFresh, "the round-tripped Enhanced session");

            var uncovered = Open(ctx, enhanced: true, chapter: UncoveredSeaChapter);
            try
            {
                var sea = uncovered.Built ? Sea(uncovered) : null;
                ctx.Check(!Effects.Ocean.Covers(UncoveredSeaChapter) && uncovered.Built && !uncovered.Session.OceanBuilt
                        && sea is { Oceans: 0 } && OceansUnder(ctx.Host) == 0,
                    $"a following Enhanced {UncoveredSeaChapter} session builds no ocean (built={uncovered.Built}, {sea?.ToString() ?? "no reading"}, {OceansUnder(ctx.Host)} under the host)");
                ctx.Check(sea is { Materials: > 0, Drawn: > 0 } && sea.Hidden == sea.Materials,
                    $"and its drawn sheet carries the collapse, so a switch left on would hole its sea ({sea?.ToString() ?? "no reading"})");
                report.AppendLine($"uncovered {UncoveredSeaChapter}\n{sea?.Print() ?? "no reading"}");
            }
            finally
            {
                uncovered.Close();
            }
            ctx.WriteArtifact("test-graphics-ocean-switch.txt", report.ToString());
        }
        finally
        {
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

    // The process back on the mode and fade the suite found, its shaders' text included.
    private static void Restore(bool wasEnhanced)
    {
        GraphicsMode.Set(wasEnhanced);
        ViewDistance.Set(null);
        Mech3.ShaderTwins.Regenerate();
        EffectsLevel.RegisteredScaleSq = EnhancedLook.ClutterFadeScaleSq();
        RenderingServer.GlobalShaderParameterSet(EffectsLevel.ShaderParam, EffectsLevel.RegisteredScaleSq);
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
            ctx.Check(fromPane && sun != enhanced && copy == enhanced,
                $"{label}: a disc draws its pane's view less its own airframe, with {(enhanced ? "the copy in place of the sun" : "the sun itself")} (0x{disc:X5}: sun {sun}, copy {copy})");
        }
    }

    // A zone the weather rig lights, crossed under Enhanced. C2/MP2's two zones disagree about the
    // bearing, so a copy that took the sun once at build would be left behind.
    private static void ZoneCrossing(TestContext ctx, string zrdr)
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
            EnhancedLook.ApplySun(sun, true, EnhancedPasses.None);
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
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, chapter), $"{chapter} textures");
        ctx.RequireData(SessionPaths.ChapterGamez(ctx.DataRoot, chapter), $"{chapter} gamez");
    }

    // A Water Quality apply as the Options accept makes it, then the steps a reading waits for.
    private static void ApplyWater(Rig rig, string word)
    {
        WaterQualitySetting.Resolve(word, null, null);
        EnhancedLook.ApplyWaterQuality(rig.Session);
        Step(rig);
    }

    // The ocean nodes standing in the session's tree. Entering and leaving the tree sets the sheet's
    // switch. The server's getter for that errors outside the editor, so the tree is read instead.
    private static string OceanState(Rig rig)
    {
        int inTree = 0;
        Walk(rig.Session, node =>
        {
            if (node is Effects.Ocean { } ocean && ocean.IsInsideTree())
                inTree++;
        });
        return string.Create(CultureInfo.InvariantCulture, $"{inTree} in tree");
    }

    // The ocean nodes standing anywhere under the host, so one parented outside the session counts.
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

    // A closed session's ocean has left the tree, which is what puts the sheet's switch back.
    private static void Left(TestContext ctx, Effects.Ocean? ocean, string what)
    {
        bool gone = ocean == null || !GodotObject.IsInstanceValid(ocean) || !ocean.IsInsideTree();
        ctx.Check(ocean != null && gone && OceansUnder(ctx.Host) == 0,
            $"closing {what} leaves no ocean in the tree (held ocean {(ocean == null ? "never built" : gone ? "gone" : "still in the tree")}, {OceansUnder(ctx.Host)} under the host)");
    }

    // The base sheet's materials as the world builder named them, by texture and shader text. Also
    // how many carry the collapse and are drawn.
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
        int oceans = 0;
        Walk(rig.Session, node =>
        {
            if (node is Effects.Ocean { } ocean && ocean.IsInsideTree())
                oceans++;
        });
        return new SeaReading(oceans, sheet.Count, hidden, drawn, Print(census));
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
            Orbit = new Flight.Camera.OrbitCamera(camera),
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

    private sealed record SeaReading(int Oceans, int Materials, int Hidden, int Drawn, string Census)
    {
        public override string ToString() => string.Create(CultureInfo.InvariantCulture,
            $"{Oceans} ocean(s), {Materials} base sheet material(s), {Hidden} collapsing, {Drawn} drawn surface(s)");

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

    private sealed record VertexReading(int Nodes, int Materials, long Vertices, string Text)
    {
        public override string ToString() => string.Create(CultureInfo.InvariantCulture,
            $"{Vertices} vertices of {Materials} material(s) in {Nodes} surface draw(s)");
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
