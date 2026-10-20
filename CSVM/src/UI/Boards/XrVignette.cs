using System;
using CSVM.Utils;
using Godot;

namespace CSVM.UI.Boards;

/// <summary>
/// The headset's optional tunnel vignette, engine-free: how far the edge of view darkens while the
/// aircraft turns the view. The rate is the XR origin's own turn per second, which head tracking
/// adds nothing to, and a cut is no turn. The strength rises with the rate between
/// <see cref="OnsetDegS"/> and <see cref="FullDegS"/>, closing the clear cone from
/// <see cref="OpenHalfAngleDeg"/> to the level's <see cref="ClosedHalfAngleDeg"/>. It eases in
/// quickly, holds until the turn settles (<see cref="SettleS"/>) and eases out slowly. Every number
/// is a proposal to TUNE at the headset (docs/architecture/UI.md).
/// </summary>
public sealed class XrVignette
{
    /// <summary>The turn rate under which nothing darkens, degrees a second: a gentle bank.</summary>
    public const float OnsetDegS = 20f;

    /// <summary>The turn rate at which the vignette is at full strength, degrees a second: a hard
    /// roll or a tight turn.</summary>
    public const float FullDegS = 90f;

    /// <summary>The clear cone's half-angle at zero strength, past the edge of any headset's eye, so
    /// a weak vignette starts at the rim.</summary>
    public const float OpenHalfAngleDeg = 60f;

    /// <summary>How many degrees the edge takes to go from clear to black.</summary>
    public const float FeatherDeg = 12f;

    /// <summary>Seconds the strength takes to follow a rising rate, as a time constant. Quick, so
    /// the tunnel is there when a roll starts.</summary>
    public const float AttackS = 0.1f;

    /// <summary>Seconds the strength takes to follow a falling rate. Slow, so the view does not
    /// pulse through a series of short rolls.</summary>
    public const float ReleaseS = 0.4f;

    /// <summary>Seconds the rate must stay at or under <see cref="OnsetDegS"/> before the strength
    /// may fall. Until then it only rises, so a manoeuvre whose rate dips and climbs again holds the
    /// tunnel instead of pumping it.</summary>
    public const float SettleS = 1f;

    private Basis? _last;
    private float _quietS = SettleS;

    /// <summary>The strength after the last <see cref="Step"/>, 0 open to 1 fully closed.</summary>
    public float Strength { get; private set; }

    /// <summary>The origin's turn rate the last <see cref="Step"/> measured, degrees a second.</summary>
    public float RateDegS { get; private set; }

    /// <summary>The clear cone's half-angle at full strength for <paramref name="level"/>, and the
    /// open angle when it is off.</summary>
    public static float ClosedHalfAngleDeg(VrVignette level) => level switch
    {
        VrVignette.Mild => 40f,
        VrVignette.Strong => 28f,
        _ => OpenHalfAngleDeg,
    };

    /// <summary>The strength a steady <paramref name="rateDegS"/> settles at: 0 below the onset,
    /// 1 at and past full, a smoothstep between.</summary>
    public static float TargetStrength(float rateDegS)
    {
        float t = Math.Clamp((rateDegS - OnsetDegS) / (FullDegS - OnsetDegS), 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    /// <summary>The clear cone's half-angle at <paramref name="strength"/> for
    /// <paramref name="level"/>.</summary>
    public static float HalfAngleDeg(VrVignette level, float strength) =>
        OpenHalfAngleDeg + ((ClosedHalfAngleDeg(level) - OpenHalfAngleDeg) * Math.Clamp(strength, 0f, 1f));

    /// <summary>The angle between two orientations, degrees.</summary>
    public static float TurnDeg(Basis from, Basis to)
    {
        var q = (to.Orthonormalized() * from.Orthonormalized().Transposed()).Orthonormalized().GetRotationQuaternion();
        return Mathf.RadToDeg(2f * Mathf.Acos(Math.Clamp(Math.Abs(q.W), 0f, 1f)));
    }

    /// <summary>One frame: the origin's <paramref name="origin"/> orientation <paramref name="dt"/>
    /// seconds after the last. Off, or on a <paramref name="cut"/>, it measures no turn; off it also
    /// drops the strength at once.</summary>
    public void Step(VrVignette level, Basis origin, float dt, bool cut)
    {
        bool measured = level != VrVignette.Off && !cut && dt > 0f && _last is not null;
        RateDegS = measured ? TurnDeg(_last!.Value, origin) / dt : 0f;
        _last = origin;
        if (level == VrVignette.Off)
        {
            Strength = 0f;
            return;
        }

        if (dt <= 0f)
        {
            return;
        }

        float target = TargetStrength(RateDegS);
        _quietS = RateDegS > OnsetDegS ? 0f : _quietS + dt;
        if (_quietS < SettleS)
        {
            target = Math.Max(target, Strength);
        }

        float tau = target > Strength ? AttackS : ReleaseS;
        Strength += (target - Strength) * (1f - MathF.Exp(-dt / tau));
    }
}
