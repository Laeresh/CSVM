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
