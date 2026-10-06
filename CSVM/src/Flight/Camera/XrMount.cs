using Godot;

namespace CSVM.Flight.Camera;

/// <summary>
/// Where the headset's tracking space is anchored in the world each frame: the XR origin the
/// player's tracked head moves about. In a first-person view it is the flat camera's eye at the
/// aircraft's own attitude. The headset supplies the look, so the flat view's fixed head pitch is
/// left out. Every other view keeps the flat camera's eye with its heading only. A chase swing or a
/// tumbling wreck then never rolls or pitches the player's horizon. Engine-free, so it unit-tests;
/// <c>XrRig.cs</c> applies it.
/// </summary>
public static class XrMount
{
    // Below this horizontal length the eye looks straight up or down and has no heading of its own.
    private const float LevelEpsilon = 1e-4f;

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
