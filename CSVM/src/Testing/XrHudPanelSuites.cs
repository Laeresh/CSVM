using System.Collections.Generic;
using System.Globalization;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Launch;
using CSVM.Spec;
using CSVM.UI.Boards;
using Godot;

namespace CSVM.Testing;

/// <summary>The cockpit-locked HUD panel of a stereo session over the <c>--xr-sim</c> stand-in
/// interface, with no stereo viewport. It reads how a HUD control on the panel measures itself and
/// where the panel stands as the aircraft turns and the head moves. Module entries:
/// docs/architecture/Flight.md on XrHudPanel and HudMetrics.</summary>
internal static class XrHudPanelSuites
{
    // Two millimetres: the suite reads the plain world pose back, and float32 world coordinates 20 km
    // out step by two. The eye feed's own placement is read to a hundredth of a millimetre.
    private const float PoseTol = 0.002f;
    private const float FedTol = 1e-5f;

    // A rotated seat 20 km from the chapter origin, so no basis product is an exact 0 or 1.
    private static readonly Vector3 SeatAt = new(20000f, 300f, -4144f);
    private static readonly Basis Attitude = Basis.FromEuler(new Vector3(0.21f, -2.3f, 0.13f));

    /// <summary>Builds the panel over a rig on a live stand-in interface and a chrome layer on it.
    /// It then places the seat, moves the head, turns the aircraft, takes the chase pose and hides
    /// the layer, reading the panel after each step.</summary>
    [Suite("xr-hud-panel",
        "a HUD control on the stereo HUD panel lays out on the panel's 2560x1440 at scale 1 as on a 1440p screen while one on the root keeps the window's metrics, the panel stands 2 m ahead on the first-person axis in the XR origin's frame, a stand-in head turned and moved in the seat leaves it where it was, it turns with the aircraft and stands in the same place before the chase pose, it hides with no chrome layer drawing, and it draws between the world cards and the ring")]
    internal static void XrHudPanelFollowsTheSeat(TestContext ctx)
    {
        if (XRServer.PrimaryInterface != null)
        {
            ctx.Check(false, $"no XR interface is live before the suite, found {XRServer.PrimaryInterface.GetName()}");
            return;
        }

        var owned = new List<Node>();
        var sim = new XrSimInterface(XrSimSpec.Default);
        bool added = false;
        try
        {
            XRServer.AddInterface(sim);
            added = true;
            ctx.Check(sim.Initialize(), $"the stand-in interface initialises");
            var flat = new Camera3D { Name = "xr-hud-flat", Current = false };
            var origin = new XROrigin3D { Name = "xr-hud-origin" };
            var head = new XRCamera3D { Name = "xr-hud-head", Current = false };
            origin.AddChild(head);
            var root = ctx.Host.GetViewport();
            var panel = XrHudPanel.Build(() => origin.GlobalTransform, eyeFeed: false, root);
            owned.AddRange(new Node[] { flat, origin, panel });
            foreach (var node in owned)
            {
                ctx.Host.AddChild(node);
            }

            var rig = new XrRig(origin, head, flat, _ => null);
            var layer = new CanvasLayer { Name = "xr-hud-chrome", Layer = HudLayers.Hud };
            var probe = new Control { Name = "xr-hud-probe" };
            layer.AddChild(probe);
            panel.Surface.AddChild(layer);
            var flatLayer = new CanvasLayer { Name = "xr-hud-flat-chrome", Layer = HudLayers.Hud };
            var flatProbe = new Control { Name = "xr-hud-flat-probe" };
            flatLayer.AddChild(flatProbe);
            ctx.Host.AddChild(flatLayer);
            owned.Add(flatLayer);

            Metrics(ctx, probe, flatProbe);

            rig.Place(new Transform3D(Attitude, SeatAt), null, Attitude, null);
            panel.Refresh();
            ctx.Check(panel.Shown && panel.Surface.RenderTargetUpdateMode == SubViewport.UpdateMode.Always,
                $"with a chrome layer drawing the panel shows and its surface updates");
            var seat = origin.GlobalTransform;
            ctx.Check(InFrame(seat, panel.Quad.GlobalTransform, panel.Layout.Pose, PoseTol, out string seatOff),
                $"the panel stands at its pose in the XR origin's frame, {seatOff} off, {panel.Layout.Pose.Origin.Length():0.###} m ahead");
            var fed = panel.Layout.Placement(seat, eyeFeed: true);
            ctx.Check(InFrame(new Transform3D(seat.Basis, Vector3.Zero), fed, panel.Layout.Pose, FedTol, out string fedOff)
                && fed.Origin.Length() < 3f,
                $"under the eye feed the same pose carries no world translation ({fed.Origin.Length():0.###} m from the world origin, {fedOff} off)");

            // The player turns and leans in the seat: the head leaves the eye, the panel stays.
            var before = panel.Quad.GlobalTransform;
            var headBefore = head.GlobalTransform;
            sim.MoveHead(30f, -12f, new Vector3(0.05f, 0.03f, 0.10f));
            panel.Refresh();
            float headMoved = head.GlobalPosition.DistanceTo(headBefore.Origin);
            float headTurned = Mathf.RadToDeg(head.GlobalTransform.Basis.Z.AngleTo(headBefore.Basis.Z));
            ctx.Check(headMoved > 0.1f && headTurned > 20f,
                $"the stand-in head moves {Mm(headMoved)} and turns {headTurned.ToString("0.#", CultureInfo.InvariantCulture)} deg in the seat");
            ctx.Check(panel.Quad.GlobalTransform.IsEqualApprox(before),
                $"and the panel stays fixed in the aircraft's frame, {Mm(panel.Quad.GlobalPosition.DistanceTo(before.Origin))} off");

            // The aircraft rolls and turns: the panel goes with it.
            var turned = Attitude * Basis.FromEuler(new Vector3(0.3f, 0.8f, -0.6f));
            rig.Place(new Transform3D(turned, SeatAt + new Vector3(55f, -3f, 21f)), null, turned, null);
            panel.Refresh();
            float swung = Mathf.RadToDeg(panel.Quad.GlobalTransform.Basis.Z.AngleTo(before.Basis.Z));
            ctx.Check(InFrame(origin.GlobalTransform, panel.Quad.GlobalTransform, panel.Layout.Pose, PoseTol, out string turnOff) && swung > 20f,
                $"a turned aircraft turns the panel with it by {swung.ToString("0.#", CultureInfo.InvariantCulture)} deg, at the same pose in the seat ({turnOff} off)");

            // The rigid chase stands the origin on the chase pose: the panel hangs ahead of it.
            var chase = XrMount.Chase(SeatAt, turned, new Vector3(0f, 3f, 18f), XrChase.Rigid);
            origin.GlobalTransform = chase;
            panel.Refresh();
            ctx.Check(InFrame(chase, panel.Quad.GlobalTransform, panel.Layout.Pose, PoseTol, out string chaseOff),
                $"in the chase the panel stands at the same pose before the chase eye ({chaseOff} off)");

            layer.Visible = false;
            panel.Refresh();
            ctx.Check(!panel.Shown && panel.Surface.RenderTargetUpdateMode == SubViewport.UpdateMode.Disabled,
                $"with no chrome layer drawing (the crash camera hides the HUD) the panel hides and its surface stops");
            layer.Visible = true;

            var material = (ShaderMaterial)panel.Quad.MaterialOverride;
            ctx.Check(material.RenderPriority == HeadLockedQuads.HudPanelPriority
                && HeadLockedQuads.WorldMarkerPriority < material.RenderPriority
                && material.RenderPriority < HeadLockedQuads.MarkerRingPriority
                && material.Shader.Code.Contains("depth_test_disabled"),
                $"the panel draws at {material.RenderPriority}, over the world cards ({HeadLockedQuads.WorldMarkerPriority}) and under the ring ({HeadLockedQuads.MarkerRingPriority}), with no depth test, as the flat HUD draws over the cockpit");
        }
        finally
        {
            XRServer.ClearReferenceFrame();
            if (added)
            {
                sim.Uninitialize();
                XRServer.RemoveInterface(sim);
            }

            foreach (var node in owned)
            {
                node.QueueFree();
            }
        }
    }

    // A control on the panel measures the panel as its window; one on the root keeps the root's.
    private static void Metrics(TestContext ctx, Control probe, Control flatProbe)
    {
        var size = XrHudPanelLayout.SurfaceSize;
        ctx.Check(probe.GetViewportRect().Size == (Vector2)size,
            $"a HUD control on the panel lays out in its {size.X}x{size.Y} ({probe.GetViewportRect().Size})");
        ctx.Check(HudMetrics.Scale(probe) == 1f && HudMetrics.PaneFactor(probe) == 1f,
            $"at scale {HudMetrics.Scale(probe)} with pane factor {HudMetrics.PaneFactor(probe)}, the reference frame itself");
        ctx.Check(HudMetrics.ReadingBox(probe) == new Rect2(Vector2.Zero, size),
            $"its reading box is the whole panel ({HudMetrics.ReadingBox(probe)})");
        float rootH = flatProbe.GetTree().Root.Size.Y;
        ctx.Check(HudMetrics.Scale(flatProbe) == rootH / HudMetrics.ReferenceHeight
            && HudMetrics.WindowSize(flatProbe) == (Vector2)flatProbe.GetTree().Root.Size,
            $"a HUD control on the root keeps the window's scale {HudMetrics.Scale(flatProbe)} ({rootH} rows)");
    }

    // Whether `placed`, read in `frame`, is `pose`: its axes alike and its centre within `tol` metres.
    private static bool InFrame(in Transform3D frame, in Transform3D placed, in Transform3D pose, float tol, out string off)
    {
        var rel = frame.AffineInverse() * placed;
        float distance = rel.Origin.DistanceTo(pose.Origin);
        off = Mm(distance);
        return rel.Basis.IsEqualApprox(pose.Basis) && distance < tol;
    }

    private static string Mm(float metres) =>
        (metres * 1000f).ToString("0.###", CultureInfo.InvariantCulture) + " mm";
}
