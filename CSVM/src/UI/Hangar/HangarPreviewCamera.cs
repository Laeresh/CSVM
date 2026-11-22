using System;

namespace CSVM.UI.Hangar;

/// <summary>
/// The hangar's live plane picture's orbit, engine-free. Its <see cref="Yaw"/> turns about the
/// vertical axis, 0 in front of the nose and positive toward +X. Its <see cref="Pitch"/> is the
/// height above the plane's horizontal plane. It turns on its own, swings to a focus and holds it,
/// follows a drag, and after either eases back into the turn. Every number is TUNE.
/// </summary>
public sealed class HangarPreviewCamera
{
    /// <summary>The turntable's rate, radians a second.</summary>
    public const float TurnRate = 0.3f;

    /// <summary>The pitch the turntable settles to, a little above the wings.</summary>
    public const float RestPitch = 0.3f;

    /// <summary>The yaw a fresh camera starts on, a three-quarter view off the nose.</summary>
    public const float StartYaw = 0.75f;

    /// <summary>How long a focus holds before the turntable resumes, seconds.</summary>
    public const float FocusHold = 4f;

    /// <summary>How long a released drag holds before the turntable resumes, seconds.</summary>
    public const float DragIdle = 1.5f;

    /// <summary>How far the pitch may go either way, short of straight up or down.</summary>
    public const float MaxPitch = 1.3f;

    // The rate a swing closes on its target, per second. Then how long the turn takes to reach
    // full speed again, so a resume never jerks.
    private const float Ease = 4f;
    private const float SpinUp = 1.5f;

    private float _hold;
    private float _spin = 1f;
    private float? _yawTo;
    private float _pitchTo;

    /// <summary>The camera's angle about the vertical axis, wrapped to one turn.</summary>
    public float Yaw { get; private set; } = StartYaw;

    /// <summary>The camera's height angle.</summary>
    public float Pitch { get; private set; } = RestPitch;

    /// <summary>Whether the turntable is running, rather than a focus or a drag holding.</summary>
    public bool Turning => _hold <= 0f;

    /// <summary>The angles <paramref name="focus"/> looks from, the side nearest <paramref name="yaw"/>
    /// where either side shows it. <paramref name="portSide"/> is the sign of X the left wing lies on.
    /// Null for <see cref="HangarPreviewFocus.None"/>.</summary>
    public static (float Yaw, float Pitch)? Aim(HangarPreviewFocus focus, float yaw, float portSide)
    {
        float side = MathF.Sign(Wrap(yaw)) is 0 ? 1f : MathF.Sign(Wrap(yaw));
        return focus switch
        {
            HangarPreviewFocus.LeftWing => (0.7f * portSide, -0.45f),
            HangarPreviewFocus.RightWing => (-0.7f * portSide, -0.45f),
            HangarPreviewFocus.Nose => (1.2f * side, 0.15f),
            HangarPreviewFocus.Tail => (2.3f * side, 0.2f),
            HangarPreviewFocus.Wing => (0.9f * side, 1f),
            _ => null,
        };
    }

    /// <summary>Swings to <paramref name="focus"/> and holds there for <see cref="FocusHold"/>.</summary>
    public void Focus(HangarPreviewFocus focus, float portSide)
    {
        if (Aim(focus, Yaw, portSide) is not { } aim)
        {
            return;
        }

        _yawTo = Yaw + Wrap(aim.Yaw - Yaw);
        _pitchTo = aim.Pitch;
        _hold = FocusHold;
        _spin = 0f;
    }

    /// <summary>Turns the camera by a drag's or a stick's angles, holding the turntable off until
    /// <see cref="DragIdle"/> after the last one.</summary>
    public void Orbit(float yaw, float pitch)
    {
        if (yaw == 0f && pitch == 0f)
        {
            return;
        }

        Yaw = Wrap(Yaw + yaw);
        Pitch = Math.Clamp(Pitch + pitch, -MaxPitch, MaxPitch);
        _yawTo = null;
        _hold = DragIdle;
        _spin = 0f;
    }

    /// <summary>One step of <paramref name="dt"/> seconds.</summary>
    public void Advance(float dt)
    {
        float k = 1f - MathF.Exp(-Ease * dt);
        if (_hold > 0f)
        {
            _hold -= dt;
            if (_yawTo is { } yawTo)
            {
                Yaw += (yawTo - Yaw) * k;
                Pitch += (_pitchTo - Pitch) * k;
            }

            return;
        }

        _yawTo = null;
        _spin = MathF.Min(1f, _spin + (dt / SpinUp));
        Yaw = Wrap(Yaw + (TurnRate * _spin * dt));
        Pitch += (RestPitch - Pitch) * k * 0.5f;
    }

    // An angle brought into (-pi, pi], so a swing takes the short way round.
    private static float Wrap(float angle) => MathF.IEEERemainder(angle, MathF.Tau);
}
