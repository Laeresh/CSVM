using CSVM.Flight.Camera;
using CSVM.Session.World;
using CSVM.UI.Boards;
using Godot;

namespace CSVM.Testing;

/// <summary>The world flare's placement against the screen rig's through Godot's own projection,
/// and the stereo session's head-locked colour quads. Module entries: docs/architecture/Session.md
/// on WorldFlareRig, docs/architecture/UI.md on HeadLockedQuads.</summary>
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
            var tint = ((ShaderMaterial)quads.QuadOf(HeadLockedQuads.Tier.Whiteout).MaterialOverride).GetShaderParameter("tint").AsColor();
            ctx.Check(tint == rect.Color, $"and hands it to the shader as the tint ({tint})");
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
}
