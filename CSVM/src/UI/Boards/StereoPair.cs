using System;
using Godot;

namespace CSVM.UI.Boards;

/// <summary>
/// A cutscene the theater screen plays as a stereo pair (<see cref="StereoPair"/>). It names the
/// camera the session poses on the authored one, and the scene depth that lands on the screen plane.
/// <c>FilmRows</c> is the gap between the letterbox bars, its top and height as fractions of the
/// picture, while they are up. The screen shows the surround over the bars.
/// </summary>
public readonly record struct TheaterCutscene(Camera3D Camera, float ConvergenceM, Vector2? FilmRows = null);

/// <summary>
/// The 3D-cinema pair a cutscene plays as on the theater screen, engine-free. Two cameras stand on
/// the authored camera, one interocular distance apart along its right axis, with its orientation.
/// Each frustum is shifted so the scene at the convergence depth lands on one screen point in both
/// eyes, on the screen plane. Nearer scene stands out of the screen and farther
/// scene recedes behind it, up to <see cref="ParallaxM"/> at infinity. The proposed convergence and
/// the depth it gives are in docs/architecture/UI.md on StereoPair. Pinned by
/// <c>CSVM.Tests/StereoPairTests.cs</c>.
/// </summary>
public static class StereoPair
{
    /// <summary>The letterbox card's depth ahead of the cutscene camera, the same in every chapter
    /// (docs/formats/anim-definitions/cutscenes.md). The convergence falls back on it when no card
    /// was measured.</summary>
    public const float CardDepthM = 7.5f;

    /// <summary>The convergence as a multiple of the card's depth. At 1 the card's frame stands on
    /// the screen plane, where the screen's mask over the bars frames the window. The scene lies
    /// behind it, and below 1 it deepens. TUNE at the headset.</summary>
    public const float ConvergenceOfCard = 1f;

    /// <summary>The eyes' separation until the runtime reports its eyes: a typical adult's.</summary>
    public const float DefaultInterocularM = 0.063f;

    /// <summary>The scene depth the pair converges at for a letterbox card <paramref name="cardDepthM"/>
    /// ahead of the camera, or <see cref="CardDepthM"/> when none was measured.</summary>
    public static float ConvergenceM(float cardDepthM) =>
        (cardDepthM > 0f ? cardDepthM : CardDepthM) * ConvergenceOfCard;

    /// <summary>Eye <paramref name="view"/>'s camera (0 left, 1 right) on the authored
    /// <paramref name="centre"/>, half <paramref name="interocularM"/> along its right axis. It
    /// keeps the authored orientation, so the two image planes stay parallel.</summary>
    public static Transform3D Eye(in Transform3D centre, int view, float interocularM)
    {
        var right = centre.Basis.X.Normalized();
        return new Transform3D(centre.Basis, centre.Origin + (right * (Side(view) * interocularM * 0.5f)));
    }

    /// <summary>Eye <paramref name="view"/>'s horizontal frustum offset, in Godot's units (metres
    /// at the near plane <paramref name="nearM"/>), that converges the pair at
    /// <paramref name="convergenceM"/>. Each eye's frustum then meets the authored camera's there.</summary>
    public static float FrustumOffsetM(int view, float interocularM, float nearM, float convergenceM) =>
        -Side(view) * interocularM * 0.5f * nearM / convergenceM;

    /// <summary>A frustum's height at the near plane <paramref name="nearM"/> for a vertical field
    /// of view of <paramref name="fovDeg"/>: the authored camera's framing, in frustum form.</summary>
    public static float FrustumHeightM(float fovDeg, float nearM) =>
        2f * nearM * MathF.Tan(Mathf.DegToRad(fovDeg) * 0.5f);

    /// <summary>Where a screen <paramref name="screenHeightM"/> tall would stand to span the
    /// vertical <paramref name="fovDeg"/> the picture was framed at.</summary>
    public static float OrthoDistanceM(float fovDeg, float screenHeightM) =>
        screenHeightM * 0.5f / MathF.Tan(Mathf.DegToRad(fovDeg) * 0.5f);

    /// <summary>The right eye's image of a point at <paramref name="depthM"/> less the left eye's,
    /// in metres across a screen <paramref name="screenHeightM"/> tall. Framed at
    /// <paramref name="fovDeg"/> and converged at <paramref name="convergenceM"/>. Zero at the
    /// convergence, positive (behind the screen) beyond it.</summary>
    public static float ParallaxM(float depthM, float interocularM, float convergenceM, float fovDeg, float screenHeightM) =>
        interocularM * OrthoDistanceM(fovDeg, screenHeightM) * ((1f / convergenceM) - (1f / depthM));

    /// <summary>How far away a point drawn with <paramref name="parallaxM"/> on a screen at
    /// <paramref name="screenDistanceM"/> appears to eyes <paramref name="interocularM"/> apart.
    /// Infinite once the parallax reaches the eyes' separation.</summary>
    public static float ApparentDistanceM(float parallaxM, float screenDistanceM, float interocularM) =>
        parallaxM >= interocularM ? float.PositiveInfinity : screenDistanceM * interocularM / (interocularM - parallaxM);

    private static float Side(int view) => view == 0 ? -1f : 1f;
}
