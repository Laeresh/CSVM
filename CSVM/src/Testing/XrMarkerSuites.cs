using System.Collections.Generic;
using System.Globalization;
using CSVM.Flight.Hud;
using CSVM.Flight.Weapons;
using CSVM.UI.Boards;
using Godot;

namespace CSVM.Testing;

/// <summary>The stereo flight markers' placement, read off <see cref="XrMarkers.Place"/> with no
/// stereo viewport. World cards stand at their points, facing the head with the seat's up and
/// holding the flat HUD's angle. The head ring's arrows lie on each off-view marker's bearing.
/// Module entry: docs/architecture/Flight.md on XrMarkers.</summary>
internal static class XrMarkerSuites
{
    // The flat cockpit view's vertical angle, which sizes every card here.
    private const float CockpitFovDeg = 64.3698f;

    /// <summary>Places a pipper, an on-view selection, a far tag and three off-view marks. The head
    /// is turned, 20 km from the origin. Every card is read back.</summary>
    [Suite("xr-markers",
        "a stereo session's flight markers stand at their world points facing the head with the seat's up and the flat HUD's angle, a far one keeps its bearing at the clamp, and an off-view one hangs on the head ring as an arrow along its bearing with its label inside the ring")]
    internal static void XrMarkersPlace(TestContext ctx)
    {
        var markers = XrMarkers.Build(null);
        ctx.Host.AddChild(markers);
        try
        {
            // A head turned and pitched, the seat rolled, far from the origin, so no product is exact.
            var seat = Basis.FromEuler(new Vector3(0.12f, -2.3f, 0.21f));
            var headBasis = seat * Basis.FromEuler(new Vector3(-0.2f, 0.35f, 0f));
            var head = new Transform3D(headBasis, new Vector3(20000f, 310f, -4144f));
            Vector3 ahead = -headBasis.Z, right = headBasis.X, up = headBasis.Y;
            var pipperAt = head.Origin + (ahead * 450f) + (up * 6f);
            var boxAt = head.Origin + (ahead * 300f) + (right * 40f);
            var farAt = head.Origin + (ahead * 30000f);
            var rightAt = head.Origin + (right * 300f) + (ahead * 20f);
            var asternAt = head.Origin - (ahead * 400f);
            var leftBehindAt = head.Origin - (ahead * 200f) - (right * 150f);
            var target = TargetRef.ForStructure(default, TargetClass.Enemy, "site", category: "Destroy",
                displayName: "Radio Tower");
            var marks = new List<WorldMark>
            {
                new(WorldMarkStyle.Selected, boxAt, TargetHud.HudRed, "", target, true, 0),
                new(WorldMarkStyle.Tagged, farAt, TargetHud.HudRed, "AI1", default, false, 0),
                new(WorldMarkStyle.Tagged, rightAt, TargetHud.HudGreen, "Fury", default, false, 0),
                new(WorldMarkStyle.Versus, asternAt, SplitScreen.PlayerColor(1), "P2", default, false, 0),
                new(WorldMarkStyle.Tagged, leftBehindAt, TargetHud.HudRed, "AI2", default, false, 0),
            };
            var pipperTex = Utils.TextureUpload.Create(Image.CreateEmpty(8, 8, false, Image.Format.Rgba8));
            markers.Place(head, seat.Y, CockpitFovDeg, ThemeDB.FallbackFont, head.Origin, 0f,
                new XrMarkers.Pipper(pipperAt, pipperTex), marks);
            var shown = markers.Shown;
            int world = 0, ring = 0;
            foreach (var card in shown)
            {
                if (card.Ring)
                    ring++;
                else
                    world++;
            }

            ctx.Check(world == 3 && ring == 6,
                $"the pipper, the selection and the far tag stand in the world, three marks hang on the ring as an arrow and a label each ({world} world, {ring} ring)");
            if (world != 3 || ring != 6)
            {
                return;
            }

            float perRef = XrMarkerLaw.TangentPerReferencePixel(CockpitFovDeg);
            WorldCard(ctx, "pipper", shown[0], pipperAt, head, seat.Y, perRef);
            ctx.Check(Mathf.Abs(shown[0].SizeRef.X - 40f) < 0.01f,
                $"the pipper card is the flat pipper's 40 reference pixels ({shown[0].SizeRef.X:0.###})");
            WorldCard(ctx, "selection", shown[1], boxAt, head, seat.Y, perRef);
            ctx.Check(shown[1].Key.Contains("Radio Tower") && shown[1].Key.Contains("[Destroy]"),
                $"the selection's card carries its label block ({shown[1].Key})");
            var clamped = head.Origin + ((farAt - head.Origin).Normalized() * XrMarkers.MaxDistanceM);
            WorldCard(ctx, "far tag", shown[2], clamped, head, seat.Y, perRef);

            // Ring arrows follow each mark's bearing in the head's view plane.
            RingArrow(ctx, "3 o'clock", shown[3], shown[4], new Vector2(1f, 0f), perRef);
            RingArrow(ctx, "dead astern", shown[5], shown[6], new Vector2(0f, -1f), perRef);
            var inHead = XrMarkerLaw.InHead(headBasis, leftBehindAt - head.Origin);
            RingArrow(ctx, "behind left", shown[7], shown[8], new Vector2(inHead.X, inHead.Y).Normalized(), perRef);
            ctx.Check(shown[4].Key.Contains("Fury  ") && shown[4].Key.Contains(" o'clock"),
                $"a ring label carries the clock bearing ({shown[4].Key})");

            // The head turning in place turns no world card: each faces the head's point, not its view.
            var turned = new Transform3D(headBasis * Basis.FromEuler(new Vector3(0.12f, -0.15f, 0.4f)), head.Origin);
            var before = shown[1].Pose;
            markers.Place(turned, seat.Y, CockpitFovDeg, ThemeDB.FallbackFont, head.Origin, 0f,
                new XrMarkers.Pipper(pipperAt, pipperTex), new List<WorldMark> { marks[0] });
            var after = markers.Shown[1].Pose;
            ctx.Check(after.IsEqualApprox(before),
                $"a head pitched, turned and rolled 0.12/-0.15/0.4 rad in place leaves the selection's card as it was ({after.Basis.Z.Normalized().Dot(before.Basis.Z.Normalized()):0.000000})");

            // A frame with nothing to mark hides every card.
            markers.Place(head, seat.Y, CockpitFovDeg, ThemeDB.FallbackFont, head.Origin, 0f, null,
                new List<WorldMark>());
            int visible = 0;
            foreach (var child in markers.GetChildren())
            {
                if (child is MeshInstance3D { Visible: true })
                    visible++;
            }

            ctx.Check(markers.Shown.Count == 0 && visible == 0,
                $"a frame with no markers shows no card ({markers.Shown.Count} placed, {visible} quads visible)");
        }
        finally
        {
            markers.QueueFree();
        }
    }

    /// <summary>A real targeting HUD's spyglass answered by the stereo host for a head 20 km out, a
    /// selection 300 m to its right. Each gate carries its control.</summary>
    [Suite("xr-spyglass",
        "a stereo session's spyglass: the selection on the head ring brings the picture up, rendered once at the ring's size on a disc inside its arrow with the label stepped past it; the disc stays put in the head's frame as the head turns, a held subject survives the HUD's own frame step, the picture's camera leaves out the headset's layers that the panes keep, and looking at the target, disarming it or hiding the HUD takes it down")]
    internal static void XrSpyglass(TestContext ctx)
    {
        var markers = XrMarkers.Build(null);
        ctx.Host.AddChild(markers);
        var subject = new Node3D { Name = "xr-spyglass-subject" };
        ctx.Host.AddChild(subject);
        var hud = TargetHud.Build(0, ctx.Camera, null!);
        ctx.Host.AddChild(hud);
        try
        {
            var seat = Basis.FromEuler(new Vector3(0.05f, -2.3f, 0.1f));
            var head = new Transform3D(seat, new Vector3(20000f, 310f, -4144f));
            var at = head.Origin + (seat.X * 300f) + (-seat.Z * 2f);
            subject.GlobalPosition = at;
            hud.Size = new Vector2(2560f, 1440f);
            hud.PlanePos = head.Origin;
            hud.RenderPose = head;
            var target = TargetRef.ForAircraft(new AimCandidate { Live = true, Source = subject, Position = at },
                TargetClass.Enemy, subject.Name, "Fury");
            var marks = new List<WorldMark> { new(WorldMarkStyle.Selected, at, TargetHud.HudRed, "", target, false, 0) };
            hud.MarksInWorld = true;

            // Up: the selection hangs on the ring, armed and 300 m out, inside the 1750 m engage gate.
            var picture = XrMarkers.AnswerSpyglass(hud, true, marks, head);
            ctx.Check(picture != null && hud.PictureLive && hud.Picture is { } view
                    && view.Size == new Vector2I(XrMarkerLaw.SpyglassSide, XrMarkerLaw.SpyglassSide),
                $"a selection 89.6 degrees off the view brings the picture up at the ring's {XrMarkerLaw.SpyglassSide} texels square ({hud.Picture?.Size})");
            if (picture == null)
            {
                return;
            }

            uint pane = ctx.Camera.CullMask;
            uint mask = hud.Picture!.DiscCullMask;
            uint headset = SplitScreen.HeadsetLayer | SplitScreen.HeadOverlayLayer;
            ctx.Check((pane & headset) == headset && (mask & headset) == 0
                    && mask == (Flight.Camera.SpyglassView.DiscMask(pane, SplitScreen.OwnAirframeLayer(0), Utils.GraphicsMode.Enhanced) & ~headset),
                $"the picture's camera leaves out the headset's two layers the pane draws (0x{mask:X5} against 0x{pane:X5}) and nothing else");
            ctx.Check(Flight.Camera.SpyglassView.DiscMask(pane, SplitScreen.OwnAirframeLayer(0), false)
                    == (pane & ~SplitScreen.OwnAirframeLayer(0) & ~SplitScreen.WorldFlareBand),
                $"CONTROL: the flat picture's mask is the pane's less the own airframe and the world flare");

            // The HUD's own frame step leaves a stereo host's held subject alone.
            hud._Process(0.0);
            ctx.Check(hud.PictureLive && hud.DiscShown, $"the targeting HUD's frame step keeps the picture the stereo host answered");
            hud.MarksInWorld = false;
            hud._Process(0.0);
            ctx.Check(!hud.PictureLive, $"CONTROL: the flat frame step answers the gate itself, and with no selection of its own drops it");
            hud.MarksInWorld = true;
            picture = XrMarkers.AnswerSpyglass(hud, true, marks, head);

            float perRef = XrMarkerLaw.TangentPerReferencePixel(CockpitFovDeg);
            markers.Place(head, seat.Y, CockpitFovDeg, ThemeDB.FallbackFont, head.Origin, 0f, null, marks, picture);
            var shown = markers.Shown;
            ctx.Check(shown.Count == 3 && shown[2].Key == XrMarkers.SpyglassKey && markers.Disc.Visible
                    && ReferenceEquals(markers.Disc.Showing, picture),
                $"the ring holds the arrow, the label and the picture's disc ({shown.Count} placed, disc {markers.Disc.Visible})");
            if (shown.Count != 3)
            {
                return;
            }

            var bearing = new Vector2(1f, 0f);
            float radius = XrMarkerLaw.SpyglassRadiusRef;
            float metresPerRef = perRef * XrMarkers.RingDistanceM;
            var tail = shown[0].ViewCentre;
            var want = tail + new Vector3(-radius * metresPerRef, 0f, 0f);
            var disc = shown[2];
            ctx.Check(disc.ViewCentre.DistanceTo(want) < 1e-5f && Mathf.Abs(disc.MetresPerRef - metresPerRef) < 1e-9f
                    && Mathf.Abs(disc.SizeRef.X - (2f * radius)) < 1e-4f,
                $"the disc sits one radius inside the arrow's tail on the 3 o'clock bearing, {Mm(disc.ViewCentre.DistanceTo(want))} off, {XrMarkerLaw.SpyglassDeg(CockpitFovDeg):0.00} degrees across");
            float labelIn = (tail - shown[1].ViewCentre).Dot(new Vector3(bearing.X, bearing.Y, 0f)) / metresPerRef;
            ctx.Check(labelIn > (2f * radius) + (shown[1].SizeRef.X * 0.5f),
                $"the label steps past the picture's inner rim ({labelIn:0.0} reference pixels in, the disc {2f * radius:0.0} across)");

            // Head-locked: a head turned 10 degrees away and leaning keeps the disc in its frame.
            var turned = new Transform3D(seat * new Basis(Vector3.Up, Mathf.DegToRad(10f)), head.Origin + (seat.X * 0.08f));
            markers.Place(turned, seat.Y, CockpitFovDeg, ThemeDB.FallbackFont, head.Origin, 0f, null, marks,
                XrMarkers.AnswerSpyglass(hud, true, marks, turned));
            ctx.Check(markers.Shown.Count == 3 && markers.Shown[2].ViewCentre.DistanceTo(disc.ViewCentre) < 1e-4f,
                $"a head turned 10 degrees and leaning 80 mm keeps the disc at its place in the head's frame, so it turns with the head ({Mm(markers.Shown.Count == 3 ? markers.Shown[2].ViewCentre.DistanceTo(disc.ViewCentre) : -1f)})");

            // Down: looking at the target, disarmed, or the HUD hidden.
            var facing = new Transform3D(seat * new Basis(Vector3.Up, Mathf.DegToRad(-85f)), head.Origin);
            var looked = XrMarkers.AnswerSpyglass(hud, true, marks, facing);
            markers.Place(facing, seat.Y, CockpitFovDeg, ThemeDB.FallbackFont, head.Origin, 0f, null, marks, looked);
            ctx.Check(looked == null && !hud.PictureLive && !markers.Disc.Visible,
                $"the head turned onto the target puts its box in the world and takes the picture down");
            hud.SpyglassOn = false;
            ctx.Check(XrMarkers.AnswerSpyglass(hud, true, marks, head) == null && !hud.PictureLive,
                $"disarmed, the selection on the ring shows no picture");
            hud.SpyglassOn = true;
            ctx.Check(XrMarkers.AnswerSpyglass(hud, true, marks, head) != null && hud.PictureLive,
                $"CONTROL: re-armed, it comes back on the same pose");
            ctx.Check(XrMarkers.AnswerSpyglass(hud, false, marks, head) == null && !hud.PictureLive,
                $"a hidden targeting HUD releases the picture");
            markers.Place(head, seat.Y, CockpitFovDeg, ThemeDB.FallbackFont, head.Origin, 0f, null, marks, null);
            ctx.Check(!markers.Disc.Visible && markers.Shown.Count == 2,
                $"with no picture the ring keeps the arrow and the label and hides the disc");
        }
        finally
        {
            hud.QueueFree();
            subject.QueueFree();
            markers.QueueFree();
        }
    }

    // A world card centred on `at` with its z toward the head. Its y is the seat's up as near as the
    // facing allows, and a reference pixel spans the flat view's angle at its distance.
    private static void WorldCard(TestContext ctx, string what, in XrMarkers.Placed card, Vector3 at,
        in Transform3D head, Vector3 seatUp, float perRef)
    {
        var pose = card.Pose;
        float off = pose.Origin.DistanceTo(at);
        var z = pose.Basis.Z.Normalized();
        var toHead = (head.Origin - pose.Origin).Normalized();
        var y = pose.Basis.Y.Normalized();
        var upInPlane = (seatUp - (seatUp.Dot(toHead) * toHead)).Normalized();
        float distance = head.Origin.DistanceTo(pose.Origin);
        float wantScale = perRef * distance;
        float scale = pose.Basis.X.Length();
        // The card's angle against the flat HUD's at the view centre, for its whole width.
        float cardDeg = Mathf.RadToDeg(2f * Mathf.Atan(card.SizeRef.X * 0.5f * scale / distance));
        float flatDeg = Mathf.RadToDeg(2f * Mathf.Atan(card.SizeRef.X * 0.5f * perRef));
        ctx.Check(off < 0.01f && z.Dot(toHead) > 0.999999f && y.Dot(upInPlane) > 0.999999f
                && Mathf.Abs(scale / wantScale - 1f) < 1e-4f && Mathf.Abs(cardDeg - flatDeg) < 1e-4f,
            $"the {what} card stands {Mm(off)} off its point, faces the head ({z.Dot(toHead):0.000000}), keeps the seat's up ({y.Dot(upInPlane):0.000000}) and spans {cardDeg:0.0000}° where the flat HUD spans {flatDeg:0.0000}°");
    }

    // A ring arrow on the ring along `bearing` and turned onto it. Its label sits inside the ring on
    // the same bearing, both in the head's frame at the ring's distance.
    private static void RingArrow(TestContext ctx, string what, in XrMarkers.Placed arrow, in XrMarkers.Placed label,
        Vector2 bearing, float perRef)
    {
        float ringTan = Mathf.Tan(Mathf.DegToRad(XrMarkerLaw.RingDeg));
        var want = new Vector3(bearing.X * ringTan * XrMarkers.RingDistanceM, bearing.Y * ringTan * XrMarkers.RingDistanceM,
            -XrMarkers.RingDistanceM);
        float off = arrow.ViewCentre.DistanceTo(want);
        float turned = arrow.Axis.Dot(bearing);
        var inward = new Vector2(label.ViewCentre.X - arrow.ViewCentre.X, label.ViewCentre.Y - arrow.ViewCentre.Y);
        ctx.Check(off < 1e-5f && turned > 0.99999f && inward.Normalized().Dot(-bearing) > 0.9f
                && Mathf.Abs(arrow.MetresPerRef - (perRef * XrMarkers.RingDistanceM)) < 1e-9f,
            $"the {what} arrow sits on the ring {Mm(off)} off, turned {turned:0.00000} onto its bearing, with its label inside the ring ({inward.Normalized().Dot(-bearing):0.000})");
    }

    private static string Mm(float metres) =>
        (metres * 1000f).ToString("0.###", CultureInfo.InvariantCulture) + " mm";
}
