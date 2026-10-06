using System;
using CSVM.Flight.Camera;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight.Hud;

/// <summary>
/// Where the cockpit-locked HUD panel stands in the XR origin's frame and how finely it is drawn,
/// engine-free. The panel is the flat first-person pane set in front of the seat. It is a quad
/// showing <see cref="HudMetrics"/>' 2560x1440 reference frame, facing the seated eye
/// <see cref="DistanceM"/> away. Its axis lies <see cref="TiltDeg"/> under the seat's own, and it
/// spans <see cref="VerticalDeg"/> from the eye. The reasoning behind the defaults is in
/// docs/architecture/Flight.md on XrHudPanelLayout.
/// </summary>
public readonly record struct XrHudPanelLayout(float DistanceM, float VerticalDeg, float TiltDeg)
{
    /// <summary>The panel surface in texels: the HUD's reference frame. The HUD lays out on it as on
    /// a 1440p screen, at a scale of exactly 1.</summary>
    public static readonly Vector2I SurfaceSize = new(2560, (int)HudMetrics.ReferenceHeight);

    /// <summary>The proposed panel: 2 m ahead on the original's first-person axis, 4.7 degrees under
    /// the seat's, as tall as the flat Nose and Chase view. TUNE at the headset.</summary>
    public static readonly XrHudPanelLayout Default = new(2f, CameraController.ExternalFovDeg,
        Mathf.RadToDeg(PlaneBuilder.HeadPitchOffsetRad));

    /// <summary>The panel's axis turned off the seat's: about the seat's right axis by
    /// <see cref="TiltDeg"/>, negative looking down.</summary>
    public Basis Axis => new(Vector3.Right, Mathf.DegToRad(TiltDeg));

    /// <summary>The panel's centre in the XR origin's frame, its face toward the seated eye.</summary>
    public Transform3D Pose => new(Axis, Axis * new Vector3(0f, 0f, -DistanceM));

    /// <summary>The panel's height in metres, which spans <see cref="VerticalDeg"/> from the eye.</summary>
    public float HeightM => 2f * DistanceM * MathF.Tan(Mathf.DegToRad(VerticalDeg) * 0.5f);

    /// <summary>The panel's size in metres, at the surface's aspect.</summary>
    public Vector2 SizeM => new(HeightM * SurfaceSize.X / SurfaceSize.Y, HeightM);

    /// <summary>Surface rows per degree of view at the panel's centre.</summary>
    public float RowsPerDegree => SurfaceSize.Y / VerticalDeg;

    /// <summary>Where the surface point <paramref name="uv"/> stands in the XR origin's frame. (0, 0)
    /// is the top left and (1, 1) the bottom right.</summary>
    public Vector3 PointAt(Vector2 uv) =>
        Pose * new Vector3((uv.X - 0.5f) * SizeM.X, (0.5f - uv.Y) * SizeM.Y, 0f);

    /// <summary>The panel quad's global transform for an XR origin at <paramref name="origin"/>.
    /// Under the eye feed it is the panel's offset from the origin in the origin's axes, small
    /// numbers wherever the aircraft flies. Without it, the plain world pose.</summary>
    public Transform3D Placement(in Transform3D origin, bool eyeFeed) =>
        (eyeFeed ? new Transform3D(origin.Basis, Vector3.Zero) : origin) * Pose;
}
