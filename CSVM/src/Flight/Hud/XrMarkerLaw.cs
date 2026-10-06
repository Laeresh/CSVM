using Godot;

namespace CSVM.Flight.Hud;

/// <summary>
/// The geometry of the stereo flight markers (<see cref="XrMarkers"/>), engine-free. It holds the
/// angle one reference pixel of the flat HUD spans, and which marker stays in the world. A marker
/// past the ring's cone goes to the ring hanging on the head, on its bearing. A world card faces
/// the eyes. Module entry: docs/architecture/Flight.md.
/// </summary>
public static class XrMarkerLaw
{
    /// <summary>The ring's angular radius about the head's view axis, degrees. A marker further off
    /// the axis than this leaves the world for the ring. TUNE: the flat views' 5 percent inset sits
    /// 29.5 degrees above the cockpit view's centre and 21.3 above the chase view's.</summary>
    public const float RingDeg = 25f;

    /// <summary>The tangent one flat HUD reference pixel spans at the centre of a view
    /// <paramref name="fovVerticalDeg"/> tall. The reference frame's height covers the whole angle,
    /// as in the flat game.</summary>
    public static float TangentPerReferencePixel(float fovVerticalDeg) =>
        2f * Mathf.Tan(Mathf.DegToRad(fovVerticalDeg) * 0.5f) / HudMetrics.ReferenceHeight;

    /// <summary><paramref name="toPoint"/> (world axes) in the frame of a head with orthonormal
    /// <paramref name="head"/> axes: x right, y up, -z ahead.</summary>
    public static Vector3 InHead(in Basis head, Vector3 toPoint) =>
        new(head.X.Dot(toPoint), head.Y.Dot(toPoint), head.Z.Dot(toPoint));

    /// <summary>How far off the view axis a point in the head's frame lies, degrees; 180 dead
    /// astern.</summary>
    public static float OffAxisDeg(Vector3 inHead) =>
        Mathf.RadToDeg(Mathf.Atan2(new Vector2(inHead.X, inHead.Y).Length(), -inHead.Z));

    /// <summary>Whether a point in the head's frame is drawn in the world: within
    /// <paramref name="ringDeg"/> of the view axis.</summary>
    public static bool OnView(Vector3 inHead, float ringDeg = RingDeg) => OffAxisDeg(inHead) <= ringDeg;

    /// <summary>The bearing an off-view point is turned toward, in the head's view plane (x right,
    /// y up), unit length. A point dead ahead or astern points down, as the flat edge marker
    /// does.</summary>
    public static Vector2 RingDirection(Vector3 inHead)
    {
        var dir = new Vector2(inHead.X, inHead.Y);
        return dir.Length() <= 1e-4f * inHead.Length() ? new Vector2(0f, -1f) : dir.Normalized();
    }

    /// <summary>A world card's axes at <paramref name="at"/>. Z points toward <paramref name="eye"/>
    /// and y as near <paramref name="seatUp"/> as that allows. X completes a right-handed frame.
    /// <paramref name="fallbackUp"/> stands in where the eye looks along the seat's up.</summary>
    public static Basis Facing(Vector3 at, Vector3 eye, Vector3 seatUp, Vector3 fallbackUp)
    {
        var z = (eye - at).Normalized();
        var x = seatUp.Cross(z);
        if (x.LengthSquared() < 1e-8f)
        {
            x = fallbackUp.Cross(z);
        }

        x = x.Normalized();
        return new Basis(x, z.Cross(x), z);
    }

    /// <summary>How far a box of <paramref name="halfSize"/> reaches along <paramref name="dir"/>
    /// from its centre: the inward step that keeps a ring label clear of its arrow.</summary>
    public static float Reach(Vector2 dir, Vector2 halfSize) =>
        (Mathf.Abs(dir.X) * halfSize.X) + (Mathf.Abs(dir.Y) * halfSize.Y);
}
