namespace CSVM.Mech3.Anim;

/// <summary>
/// The two live knobs on a launched <c>OBJECT_MOTION</c> body's arc, for matching our debris
/// against the original at the controls (<c>BL-022</c>). Both default to <b>1</b>, and at 1 they
/// are exact float no-ops — a default run, and every golden hash, is byte-identical to a build
/// without them.
///
/// <para><b>Why two.</b> They are not degenerate, which is what makes a by-eye match converge.
/// Scaling the launch speed by <c>k</c> moves apex height and throw as <c>k²</c> but hang time
/// only as <c>k</c>; scaling gravity by <c>g</c> moves all three as <c>1/g</c>. So moving both
/// together shrinks the arc at constant hang time, and moving one alone trades size against
/// duration. One slider could only ever find a compromise between the two.</para>
///
/// <para><b>What they do NOT scale.</b> <c>InheritedWorldVelocity</c> — the plane's momentum
/// carried into crash debris — is deliberately outside the launch scale: it is a measured
/// world quantity, not part of the authored launch, and <c>BL-122</c> owns its own
/// <c>WreckMomentum</c> TUNE. Scaling it here would silently move that item's number too.</para>
///
/// <para>⚠ <b>A value matched by eye also absorbs the <c>run_time</c> behaviour.</b> Six of
/// <c>m_build03</c>'s nine pieces are cut at 67–72 % of their arc while still climbing, and
/// <c>genx12</c>'s twelve run 3.7–5.2 s past their landing. Until that is settled, a matched
/// scalar is "the number that makes this def look right", not a decode, and it will not
/// generalise to defs with different authored run times.</para>
/// </summary>
public static class DebrisTune
{
    /// <summary>The shipped launch-speed scale: <b>0.65</b>, a TUNE settled at the controls
    /// (user, 2026-08-08) against the original's own `m_build03 destruction.mp4` — "throws the
    /// debris about the right amount". It is a JUDGED LOOK, not a decode: the authored speeds are
    /// censused and correct as read, and this scalar is the difference between what the data says
    /// and what the original renders.
    ///
    /// <para>It is deliberately not the measured value. A frame comparison over the same kill put
    /// our debris' rise at roughly 3× the original's, which would imply ~0.58 — but the two
    /// cameras were at different standoffs and the ratio was normalised by eye, so the
    /// measurement's own error bar comfortably spans 0.65. Where a measurement and the look
    /// disagree at that precision, the look wins (user's call, citing the map-extension
    /// precedent where the tooling's number was the wrong one).</para></summary>
    public const float DefaultLaunchScale = 0.65f;

    /// <summary>The shipped gravity scale: <b>1</b> — the authored <c>gravity.value</c> stands.
    /// The same sitting found the arcs' fall reads correctly once the launch is trimmed, so
    /// nothing here needs to move.</summary>
    public const float DefaultGravityScale = 1f;

    /// <summary>Multiplies the authored launch speed — <c>translation.initial</c> and
    /// <c>translation_range.initial</c> alike, plus their <c>delta</c> ramps — leaving any
    /// inherited world momentum alone.</summary>
    public static float LaunchScale { get; set; } = DefaultLaunchScale;

    /// <summary>Multiplies the authored <c>gravity.value</c>. Above 1 the arc is heavier and
    /// shorter-lived; below 1 it floats.</summary>
    public static float GravityScale { get; set; } = DefaultGravityScale;

    /// <summary>True when either knob is off the SHIPPED default — i.e. this session's debris is
    /// neither the tuned look nor anything anyone signed off. The logger and the damage lab use it
    /// to say so out loud, so an experimental run cannot be mistaken for the shipped one.</summary>
    public static bool IsTuned => LaunchScale != DefaultLaunchScale || GravityScale != DefaultGravityScale;

    /// <summary>True when both knobs sit at 1, i.e. the raw authored arc with no tune at all —
    /// the comparison state, not the shipped one.</summary>
    public static bool IsAuthored => LaunchScale == 1f && GravityScale == 1f;

    /// <summary>Back to the shipped tune.</summary>
    public static void Reset()
    {
        LaunchScale = DefaultLaunchScale;
        GravityScale = DefaultGravityScale;
    }

    /// <summary>Back to the raw authored arc — what the data says, untuned. For A/B against the
    /// shipped look, not a state to ship.</summary>
    public static void UseAuthored()
    {
        LaunchScale = 1f;
        GravityScale = 1f;
    }
}
