using System;
using Godot;

namespace CSVM.Flight.Modes;

/// <summary>What a cut course pylon draws at one moment. Heights are metres above its base. The stump
/// is drawn up to <see cref="StumpTopM"/>, its top slumped down by <see cref="SagM"/>. The falling top
/// is the part from <see cref="CutM"/> to <see cref="TopM"/>, dropped by <see cref="DropM"/>, tipped
/// outward by <see cref="TiltRad"/>. It is gone once its foot reaches the ground.</summary>
public readonly record struct PylonCutLook(float StumpTopM, float SagM, float CutM, float TopM, float DropM, float TiltRad)
{
    public bool TopShown => DropM < CutM && TopM > CutM;
}

/// <summary>
/// A course pylon's cut as a pure function of its height, the cut and the seconds since it. The top
/// above the cut tips outward and falls, vanishing as it lands. The stump slumps, holds, then regrows to the full
/// height, whole again at <see cref="RegrowSeconds"/>. Ages come from the sim clock as doubles, so
/// the shader clock's wrap never reaches them. Engine-free; every time and share is a look, tuned at
/// the controls.
/// </summary>
public static class CoursePylonCut
{
    /// <summary>Seconds from the cut until the course pylon is whole.</summary>
    public const double RegrowSeconds = 3.0;

    /// <summary>Seconds the stump stands at the cut before it regrows.</summary>
    public const float HoldSeconds = 0.5f;

    /// <summary>Seconds the stump takes to slump to its full sag.</summary>
    public const float SagInSeconds = 0.3f;

    /// <summary>The stump's deepest sag as a share of its height.</summary>
    public const float SagShare = 0.15f;

    /// <summary>Seconds the falling top takes to tip fully outward.</summary>
    public const float TipSeconds = 1.2f;

    /// <summary>The falling top's fall, metres per second squared, and its full outward tip.</summary>
    public const float FallAccel = 9.81f;

    public const float MaxTiltDeg = 70f;

    /// <summary>A clip height beyond any course pylon: the stump's top while whole, and a fresh cut's top.</summary>
    public const float NoClipM = 1.0e6f;

    /// <summary>A whole course pylon: nothing clipped, no sag, no falling top.</summary>
    public static readonly PylonCutLook Whole = new(NoClipM, 0f, NoClipM, NoClipM, 0f, 0f);

    /// <summary>The look <paramref name="age"/> seconds after a cut at <paramref name="cut"/>, whose
    /// falling top reaches up to <paramref name="top"/>. Whole before the cut and from
    /// <see cref="RegrowSeconds"/> on.</summary>
    public static PylonCutLook At(float height, float cut, float top, double age)
    {
        if (!(age >= 0.0 && age < RegrowSeconds))
        {
            return Whole;
        }

        float t = (float)age;
        float grow = Mathf.SmoothStep(HoldSeconds, (float)RegrowSeconds, t);
        float sag = SagShare * cut * Mathf.SmoothStep(0f, SagInSeconds, t) * (1f - grow);
        float drop = Math.Min(0.5f * FallAccel * t * t, cut);
        float tip = Math.Min(t / TipSeconds, 1f);
        return new PylonCutLook(cut + ((height - cut) * grow), sag, cut, top, drop, Mathf.DegToRad(MaxTiltDeg) * tip * tip);
    }

    /// <summary>The cut and falling top's reach for a touch at <paramref name="touchM"/>, given the
    /// look at that moment. A cut course pylon is cut again within what it has regrown, so nothing fallen falls
    /// twice. A touch above its regrown stump drops nothing.</summary>
    public static (float Cut, float Top) Recut(float height, float touchM, PylonCutLook now) =>
        (Math.Clamp(touchM, 0f, Math.Min(height, now.StumpTopM)), now.StumpTopM);
}
