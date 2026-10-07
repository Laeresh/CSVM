using CSVM.Flight.Camera;
using CSVM.Session.World;
using CSVM.UI.Boards;
using Godot;

namespace CSVM.Testing;

/// <summary>The world flare's placement against the screen rig's through Godot's own projection, the
/// cameras that draw its quads, and the stereo session's head-locked colour quads. Module entries:
/// docs/architecture/Session.md on WorldFlareRig, docs/architecture/UI.md on HeadLockedQuads.</summary>
internal static class StereoPaneSuites
{
    private const float PixelTol = 0.05f;

    /// <summary>Projects every element of the world rig through a live camera, at the chase and the
    /// cockpit field of view. Each must land where the screen rig draws it.</summary>
    [Suite("world-flare-pixels",
        "the world flare rig puts every element on the pixel and at the size the screen rig draws it, through a live Camera3D at the chase and the cockpit field of view on a rotated view")]
    internal static void WorldFlarePixels(TestContext ctx)
    {
        var camera = new Camera3D { Name = "world-flare-pixels", Far = 40000f };
        ctx.Host.AddChild(camera);
        try
        {
            Vector2 pane = camera.GetViewport().GetVisibleRect().Size;
            var eye = new Vector3(-6350f, 300f, -4144f);
            // A rotated view far from the origin, so no basis product is an exact 0 or 1.
            var view = new Transform3D(Basis.FromEuler(new Vector3(0.31f, -2.3f, 0.07f)), eye);
            camera.GlobalTransform = view;
            float worstPos = 0f, worstSize = 0f;
            int placed = 0;
            foreach (float vfov in new[] { CameraController.ExternalFovDeg, 64.37f })
            {
                camera.Fov = vfov;
                float tan = WorldFlareRig.TanHalfVertical(camera, pane);
                foreach (var sunLocal in new[] { new Vector3(0.05f, 0.08f, -1f), new Vector3(-0.3f, 0.12f, -1f), new Vector3(0.4f, -0.25f, -1f) })
                {
                    Vector3 sun = view * (sunLocal.Normalized() * 30000f);
                    var at = WorldFlareRig.Place(view, sun, tan, pane.X / pane.Y);
                    Vector2 sunPx = camera.UnprojectPosition(sun);
                    Vector2 centre = pane * 0.5f;
                    foreach (var e in WorldFlareRig.Elements)
                    {
                        Vector2 screenRig = sunPx + ((centre - sunPx) * e.Frac);
                        Vector3 c = WorldFlareRig.ElementCentre(at.Tangent, e.Frac, at.Depth);
                        Vector2 worldRig = camera.UnprojectPosition(view * c);
                        float half = WorldFlareRig.HalfSize(e.DiaPx, tan) * at.Depth;
                        float width = camera.UnprojectPosition(view * (c + new Vector3(half, 0f, 0f))).X
                            - camera.UnprojectPosition(view * (c - new Vector3(half, 0f, 0f))).X;
                        worstPos = Mathf.Max(worstPos, screenRig.DistanceTo(worldRig));
                        worstSize = Mathf.Max(worstSize, Mathf.Abs(width - (e.DiaPx * pane.Y / WorldFlareRig.RefHeight)));
                        placed++;
                    }
                }
            }

            ctx.Same(24, placed, $"elements placed over two fields of view and three suns");
            ctx.Check(worstPos < PixelTol, $"every element lands where the screen rig draws it, worst {worstPos:0.0000} px");
            ctx.Check(worstSize < PixelTol, $"and spans the screen rig's pixel width, worst {worstSize:0.0000} px off");
            ctx.Note($"worst element offset {worstPos:0.0000} px, worst width error {worstSize:0.0000} px");
        }
        finally
        {
            camera.QueueFree();
        }
    }

    /// <summary>Builds C3's world flare for one pane and for four, then reads every quad's layers
    /// against each pane camera's mask and each picture camera's.</summary>
    [Suite("world-flare-layers",
        "the world flare's quads, built from C3's own flare for one pane and for four, are drawn by their own pane's camera alone: never by another pane, never by any pane's spyglass picture flat or Enhanced, with or without a headset, and never by the Danger Zone photograph, each of which would place a flare for its own view")]
    internal static void WorldFlareLayers(TestContext ctx)
    {
        string texturesPath = Extraction.SessionPaths.ChapterTextures(ctx.DataRoot, "C3");
        ctx.RequireData(texturesPath, $"C3 textures");
        ctx.RequireData(ctx.InterpPath, $"interp.json");
        var root = new Node3D { Name = "world-flare-layers" };
        ctx.Host.AddChild(root);
        using var textures = new Mech3.TextureArchive(texturesPath);
        try
        {
            uint main = SplitScreen.OwnViewCullMask(SplitScreen.PaneCullMask(Mech3.ZoneGate.OpenCullMask(0xFFFFFu)), 0);
            Layers(ctx, root, textures, new[] { main }, "one pane");
            var panes = new uint[SplitScreen.MaxPlayers];
            for (int i = 0; i < panes.Length; i++)
            {
                panes[i] = SplitScreen.OwnViewCullMask(SplitScreen.PlayerCullMask(i), i);
            }

            Layers(ctx, root, textures, panes, "four panes");
            uint everyCameraLayer = SplitScreen.EveryCameraLayer;
            ctx.Check((everyCameraLayer & SpyglassView.DiscMask(main, SplitScreen.OwnAirframeLayer(0), enhanced: true)) != 0
                && (everyCameraLayer & Flight.Modes.DangerZonePhotograph.PhotoMask(main)) != 0,
                $"ABLE-TO-FAIL CONTROL: a quad on layer 1, where a single pane's flare stood, is in both pictures");
        }
        finally
        {
            root.Free();
        }
    }

    /// <summary>Drives the quads off scripted colours and reads what each draws and in what order.
    /// </summary>
    [Suite("head-locked-quads",
        "the stereo session's head-locked quads copy their flat layer's colour, hide when it is clear or its canvas layer is lowered, keep the flat layer order with the interior's glass between the whiteout and the flash, and only the whiteout is depth-tested at the seat clearance and skips the interior's stencil mark")]
    internal static void HeadLockedQuadsFollow(TestContext ctx)
    {
        var quads = HeadLockedQuads.Build();
        ctx.Host.AddChild(quads);
        var layer = new CanvasLayer();
        var rect = new ColorRect { Color = new Color(0.9f, 0.9f, 0.92f, 0.4f) };
        layer.AddChild(rect);
        ctx.Host.AddChild(layer);
        try
        {
            var flash = new Color(1f, 0f, 0f, 0f);
            quads.Follow(HeadLockedQuads.Tier.Whiteout, () => HeadLockedQuads.CanvasColour(rect));
            quads.Follow(HeadLockedQuads.Tier.Flash, () => flash);
            quads.Refresh();
            ctx.Check(quads.QuadOf(HeadLockedQuads.Tier.Whiteout).Visible
                && quads.ColourOf(HeadLockedQuads.Tier.Whiteout) == rect.Color,
                $"the whiteout quad shows the canvas colour {quads.ColourOf(HeadLockedQuads.Tier.Whiteout)}");
            var tint = ((ShaderMaterial)quads.QuadOf(HeadLockedQuads.Tier.Whiteout).MaterialOverride).GetShaderParameter("tint");
            var want = new Vector4(rect.Color.R, rect.Color.G, rect.Color.B, rect.Color.A);
            ctx.Check(tint.VariantType == Variant.Type.Vector4 && tint.AsVector4() == want,
                $"and hands it to the shader as the tint, a Vector4 so it is linearised once ({tint})");
            ctx.Check(!quads.QuadOf(HeadLockedQuads.Tier.Flash).Visible
                && !quads.QuadOf(HeadLockedQuads.Tier.SunWash).Visible
                && !quads.QuadOf(HeadLockedQuads.Tier.MissionEndFade).Visible,
                $"a clear or unfollowed tier draws no quad");

            flash = new Color(1f, 0f, 0f, 0.5f);
            layer.Visible = false;
            quads.Refresh();
            ctx.Check(quads.QuadOf(HeadLockedQuads.Tier.Flash).Visible, $"a flash that starts shows on the next refresh");
            ctx.Check(!quads.QuadOf(HeadLockedQuads.Tier.Whiteout).Visible,
                $"a lowered canvas layer (a presenting cutscene) hides the whiteout quad");

            int flare = HeadLockedQuads.WorldFlarePriority;
            int whiteout = HeadLockedQuads.PriorityOf(HeadLockedQuads.Tier.Whiteout);
            int flashP = HeadLockedQuads.PriorityOf(HeadLockedQuads.Tier.Flash);
            int hud = HeadLockedQuads.HudPanelPriority;
            int wash = HeadLockedQuads.PriorityOf(HeadLockedQuads.Tier.SunWash);
            int fade = HeadLockedQuads.PriorityOf(HeadLockedQuads.Tier.MissionEndFade);
            int glass = Mech3.SceneBuilder.XrInteriorBlendPriority;
            ctx.Check(flare + 3 < whiteout && whiteout < glass && glass < flashP && flashP < hud && hud < wash && wash < fade
                && fade <= RenderingServer.MaterialRenderPriorityMax,
                $"the draw order is HudLayers' own: flare {flare} < whiteout {whiteout} < interior glass {glass} < flash {flashP} < HUD panel {hud} < sun wash {wash} < fade {fade}");

            foreach (HeadLockedQuads.Tier tier in System.Enum.GetValues<HeadLockedQuads.Tier>())
            {
                var material = (ShaderMaterial)quads.QuadOf(tier).MaterialOverride;
                bool tested = !material.Shader.Code.Contains("depth_test_disabled");
                float distance = material.GetShaderParameter("distance_m").AsSingle();
                bool seat = tier == HeadLockedQuads.Tier.Whiteout;
                ctx.Check(tested == seat && (!seat || distance == HeadLockedQuads.SeatClearanceM),
                    $"{tier}: depth test {(tested ? "on" : "off")} at {distance} m");
                bool stencil = material.Shader.Code.Contains("stencil_mode read, compare_not_equal");
                ctx.Check(stencil == seat, $"{tier}: {(stencil ? "skips" : "ignores")} the interior's stencil mark");
                ctx.Check(material.RenderPriority == HeadLockedQuads.PriorityOf(tier), $"{tier} carries its priority");
            }
        }
        finally
        {
            quads.QueueFree();
            layer.QueueFree();
        }
    }

    // One flare per pane mask, each quad checked against every pane and picture mask.
    private static void Layers(TestContext ctx, Node3D parent, Mech3.TextureArchive textures, uint[] paneMasks, string label)
    {
        var root = new Node3D { Name = label.Replace(' ', '_') };
        parent.AddChild(root);
        var rigs = new System.Collections.Generic.List<PlayerRig>();
        for (int i = 0; i < paneMasks.Length; i++)
        {
            var horizon = new Node3D { Name = $"horizon{i + 1}" };
            horizon.AddChild(new Node3D { Name = "sun" });
            root.AddChild(horizon);
            var hud = new Node { Name = $"hud{i + 1}" };
            root.AddChild(hud);
            rigs.Add(new PlayerRig
            {
                Index = i,
                Camera = null!,
                HudParent = hud,
                Horizon = horizon,
                VisualLayer = paneMasks.Length > 1 ? SplitScreen.PlayerVisualLayer(i) : 0,
            });
        }

        var rig = new WorldFlareRig(Spec.SessionSpec.Parse(new[] { "--chapter=C3" }), null);
        rig.Build(rigs, textures, ctx.InterpPath, "C3", root);
        var quads = new System.Collections.Generic.List<(int Pane, MeshInstance3D Quad)>(rig.Quads());
        ctx.Check(rig.InstanceCount == paneMasks.Length && quads.Count == paneMasks.Length * WorldFlareRig.Elements.Count,
            $"{label}: C3 builds a flare per pane, {quads.Count} quads for {rig.InstanceCount} pane(s)");
        int stray = 0;
        foreach (var (owner, quad) in quads)
        {
            for (int pane = 0; pane < paneMasks.Length; pane++)
            {
                uint own = SplitScreen.OwnAirframeLayer(pane);
                bool drawn = (quad.Layers & paneMasks[pane]) != 0;
                bool pictured = (quad.Layers & Flight.Modes.DangerZonePhotograph.PhotoMask(paneMasks[pane])) != 0;
                foreach (bool enhanced in new[] { false, true })
                {
                    foreach (bool headset in new[] { false, true })
                    {
                        pictured |= (quad.Layers & SpyglassView.DiscMask(paneMasks[pane], own, enhanced, headset)) != 0;
                    }
                }

                if (drawn != (pane == owner) || pictured)
                {
                    stray++;
                    ctx.Note($"{label}: {quad.Name} on 0x{quad.Layers:X6} against pane {pane + 1} 0x{paneMasks[pane]:X6}: drawn {drawn}, in a picture {pictured}");
                }
            }
        }

        ctx.Check(quads.Count > 0 && stray == 0,
            $"{label}: every quad is drawn by its own pane alone and by no spyglass picture or photograph ({stray} stray reading(s))");
    }
}
