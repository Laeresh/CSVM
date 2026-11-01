using Godot;

namespace CSVM.Flight.Modes;

/// <summary>
/// A course gate's scoring aperture, engine-free, standing vertical and facing
/// <see cref="CourseGate.Forward"/>. A pylon gate's is the rectangle between its course pylons, ground to
/// tops. A balloon ring's is a <see cref="RingSides"/>-gon of its diameter about its centre.
/// The polygon stays in the gate's own frame and the segment is carried into it, so motion allocates nothing.
/// </summary>
public sealed class CourseAperture
{
    /// <summary>The balloon ring polygon's vertex count. The inscribed polygon falls short of the
    /// circle by under 0.5 % of the radius, far inside a plane's size. The vertices are read only on a
    /// step that crosses the gate's plane, so the count costs nothing per step.</summary>
    public const int RingSides = 32;

    // The aperture in the gate's frame: the plane z = 0, the Normal the gate's forward (-Z).
    private readonly StuntGate _local;

    public CourseAperture(CourseGate gate)
    {
        Gate = gate;
        Rest = CourseMotion.Rest(gate);
        Vector3[] vertices;
        if (gate.Kind == CourseGateKind.PylonGate)
        {
            float half = gate.Width / 2f;
            vertices = new[]
            {
                new Vector3(-half, 0f, 0f), new Vector3(half, 0f, 0f),
                new Vector3(half, gate.Height, 0f), new Vector3(-half, gate.Height, 0f),
            };
        }
        else
        {
            float radius = gate.Diameter / 2f;
            vertices = new Vector3[RingSides];
            for (int i = 0; i < RingSides; i++)
            {
                float angle = Mathf.Tau * i / RingSides;
                vertices[i] = new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), 0f);
            }
        }

        _local = new StuntGate { Vertices = vertices, Center = Vector3.Zero, Normal = Vector3.Forward };
    }

    public CourseGate Gate { get; }

    /// <summary>The gate's authored world transform, its heading about Y at its position.</summary>
    public Transform3D Rest { get; }

    /// <summary>Whether the world segment passes through along the gate's forward (Forward) or
    /// against it (Reverse); the other way is refused. The motion is the gate's idle motion in its own
    /// frame, placed at <c>Rest * motion</c>, and the identity at rest. The t is how far along it crosses.</summary>
    public bool CrossedBy(Vector3 from, Vector3 to, GateDirection direction, Transform3D motion, out float t)
    {
        var toLocal = (Rest * motion).AffineInverse();
        var localFrom = toLocal * from;
        var localTo = toLocal * to;
        float along = _local.Normal.Dot(localTo - localFrom);
        bool rightWay = direction == GateDirection.Forward ? along > 0f : along < 0f;
        t = 0f;
        return rightWay && _local.CrossedBy(localFrom, localTo, out t);
    }
}
