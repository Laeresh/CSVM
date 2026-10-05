using Godot;

namespace CSVM.Flight.Camera;

/// <summary>
/// The stand-in headset's eyes: a pinned head pose in the tracking space, and each view's eye
/// transform and projection. The renderer asks the XR interface for both every frame. A view is
/// composed in Godot's OpenXR order: the XR origin, the recenter frame, then head and eye. Their
/// translations are in world-scaled metres, and view 0 is the left eye. Each eye is a symmetric
/// frustum, so there is no lens distortion and no canted display. Engine-free, so it unit-tests.
/// </summary>
public readonly struct XrSimEyes
{
    private readonly float _halfIpdM;
    private readonly float _fovDeg;

    /// <summary>A head turned <paramref name="yawDeg"/> right and <paramref name="pitchDeg"/> up,
    /// offset <paramref name="head"/> metres from the seated eye, eyes <paramref name="ipdMm"/>
    /// apart, each <paramref name="fovDeg"/> wide.</summary>
    public XrSimEyes(float yawDeg, float pitchDeg, Vector3 head, float ipdMm, float fovDeg)
    {
        // Yaw about world up first, then pitch about the turned head's own right axis.
        var basis = new Basis(Vector3.Up, Mathf.DegToRad(-yawDeg)) * new Basis(Vector3.Right, Mathf.DegToRad(pitchDeg));
        Head = new Transform3D(basis, head);
        _halfIpdM = ipdMm * 0.0005f;
        _fovDeg = fovDeg;
    }

    /// <summary>The pinned head in the tracking space, unscaled metres: what the head tracker
    /// reports and what a recenter takes.</summary>
    public Transform3D Head { get; }

    /// <summary>The head with its translation in world-scaled metres, as the interface reports
    /// its camera transform.</summary>
    public Transform3D Camera(double worldScale) => Scaled(Head, worldScale);

    /// <summary>Where <paramref name="view"/> (0 left, 1 right) sees from in the world, given the
    /// XR origin <paramref name="origin"/> and the recenter frame <paramref name="reference"/>.</summary>
    public Transform3D View(uint view, in Transform3D origin, in Transform3D reference, double worldScale)
    {
        var eye = new Transform3D(Basis.Identity, new Vector3(view == 0 ? -_halfIpdM : _halfIpdM, 0f, 0f));
        return origin * reference * Scaled(Head * eye, worldScale);
    }

    /// <summary>The eye's projection, column-major, the 16 numbers
    /// <c>XRInterfaceExtension._GetProjectionForView</c> returns.</summary>
    public double[] Projection(double aspect, double near, double far)
    {
        var p = Godot.Projection.CreatePerspective(_fovDeg, (float)aspect, (float)near, (float)far, true);
        return new double[]
        {
            p.X.X, p.X.Y, p.X.Z, p.X.W,
            p.Y.X, p.Y.Y, p.Y.Z, p.Y.W,
            p.Z.X, p.Z.Y, p.Z.Z, p.Z.W,
            p.W.X, p.W.Y, p.W.Z, p.W.W,
        };
    }

    private static Transform3D Scaled(in Transform3D t, double worldScale) =>
        new(t.Basis, t.Origin * (float)worldScale);
}
