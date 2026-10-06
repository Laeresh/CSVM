using Godot;

namespace CSVM.Flight.Camera;

/// <summary>How the headset's origin takes the pane's flat camera this frame. Each camera write in
/// <see cref="CameraController"/> names one, and <see cref="XrRig.Place()"/> reads it once a frame.
/// </summary>
public enum XrAnchor
{
    /// <summary>The flat camera's eye with its heading only. It serves a camera another owner drives
    /// (a cutscene, the spectator, photo mode) and a pose nobody made level.</summary>
    Level,

    /// <summary>A first-person seat: the eye at the aircraft's attitude, moved by the seated eye
    /// offset. The stereo interior is placed from exactly this pose.</summary>
    Seat,

    /// <summary>The flat camera's whole pose, which a headset write has already made the origin's:
    /// the rigid chase and a static camera's held heading.</summary>
    Camera,
}

/// <summary>Which frame the VR chase rides, <c>--xr-chase=</c>.</summary>
public enum XrChase
{
    /// <summary>Locked to the aircraft's whole attitude, the cockpit's own frame: the default.</summary>
    Rigid,

    /// <summary>The aircraft's heading only, so the horizon stays level. The heading reverses
    /// through any vertical flight, so a loop turns the whole view half a circle at once.</summary>
    Level,
}

/// <summary>
/// Where a headset's tracking origin sits in the world each frame: the XR origin the player's
/// tracked head moves about. In a first-person view it is the flat eye at the aircraft's attitude,
/// without the flat head pitch, since the headset supplies the look. The VR
/// chase is a fixed offset in the aircraft's frame (<see cref="Chase"/>). A camera nobody made level
/// keeps the flat eye with its heading only, so it never rolls or pitches the horizon. Engine-free,
/// so it unit-tests; <c>XrRig.cs</c> applies it.
/// </summary>
public static class XrMount
{
    // Below this horizontal length the eye looks straight up or down and has no heading of its own.
    private const float LevelEpsilon = 1e-4f;

    // A spot within this many metres of the track, seen from above, has no side to face it from,
    // so it faces the aircraft itself.
    private const float NearTrackM = 1f;

    /// <summary>The origin pose for a flat camera at <paramref name="eye"/>. Pass the aircraft's
    /// drawn attitude as <paramref name="firstPersonAttitude"/> while a first-person view of a live
    /// aircraft holds the camera, and null otherwise. A first-person origin moves by the plane's
    /// seated <paramref name="eyeOffset"/>, given in the aircraft's frame; the other views ignore it.
    /// </summary>
    public static Transform3D For(in Transform3D eye, Basis? firstPersonAttitude, Vector3 eyeOffset = default)
    {
        if (firstPersonAttitude is { } attitude)
        {
            var axes = attitude.Orthonormalized();
            return new Transform3D(axes, eye.Origin + (axes * eyeOffset));
        }

        return new Transform3D(Level(eye.Basis), eye.Origin);
    }

    /// <summary>The origin pose <paramref name="anchor"/> asks for, from the flat camera's
    /// <paramref name="eye"/>, the aircraft's drawn <paramref name="attitude"/> (read by
    /// <see cref="XrAnchor.Seat"/> alone) and the seated <paramref name="eyeOffset"/>.</summary>
    public static Transform3D For(in Transform3D eye, XrAnchor anchor, Basis attitude, Vector3 eyeOffset = default) =>
        anchor switch
        {
            XrAnchor.Seat => For(eye, attitude, eyeOffset),
            XrAnchor.Camera => new Transform3D(eye.Basis.Orthonormalized(), eye.Origin),
            _ => For(eye, null),
        };

    /// <summary>The VR chase pose: <paramref name="offset"/> in metres from the aircraft at
    /// <paramref name="planePos"/>, in the frame <paramref name="frame"/> takes from
    /// <paramref name="attitude"/>, facing along that frame. Nothing eases, so the aircraft holds one
    /// place in the view however it manoeuvres.</summary>
    public static Transform3D Chase(Vector3 planePos, Basis attitude, Vector3 offset, XrChase frame)
    {
        var axes = frame == XrChase.Rigid ? attitude.Orthonormalized() : Level(attitude);
        return new Transform3D(axes, planePos + (axes * offset));
    }

    /// <summary>The origin while a cutscene plays on the theater screen. It stands at the cutscene
    /// camera's <paramref name="eye"/>, so the sky centres where the stereo pair is drawn from. It
    /// faces the level <paramref name="heldHeading"/> taken as the cutscene took the camera.</summary>
    public static Transform3D Screened(Vector3 eye, Basis heldHeading) => new(heldHeading, eye);

    /// <summary>The level heading a static camera at <paramref name="eye"/> holds under a headset:
    /// toward the nearest point of the aircraft's track ahead, from <paramref name="aircraft"/> along
    /// the unit <paramref name="along"/>. A passing aircraft then crosses the view from one side to the
    /// other instead of ending behind the seat. A zero <paramref name="along"/> faces the aircraft.</summary>
    public static Basis Watch(Vector3 eye, Vector3 aircraft, Vector3 along)
    {
        var near = aircraft + (along * Mathf.Max(0f, (eye - aircraft).Dot(along)));
        var flat = new Vector3(near.X - eye.X, 0f, near.Z - eye.Z);
        if (flat.Length() < NearTrackM)
        {
            flat = new Vector3(aircraft.X - eye.X, 0f, aircraft.Z - eye.Z);
        }

        return flat.Length() < LevelEpsilon ? Basis.Identity : Basis.LookingAt(flat.Normalized(), Vector3.Up);
    }

    /// <summary>The heading-only basis of <paramref name="basis"/>: its forward (−Z) laid flat, up
    /// along world +Y. An eye looking straight up or down takes its heading from its own up axis,
    /// which then points along the view's top edge.</summary>
    public static Basis Level(Basis basis)
    {
        var forward = -basis.Z;
        var flat = new Vector3(forward.X, 0f, forward.Z);
        if (flat.Length() < LevelEpsilon)
        {
            var up = basis.Y;
            flat = new Vector3(up.X, 0f, up.Z) * (forward.Y < 0f ? 1f : -1f);
        }

        if (flat.Length() < LevelEpsilon)
        {
            return Basis.Identity;
        }

        return Basis.LookingAt(flat.Normalized(), Vector3.Up);
    }
}
