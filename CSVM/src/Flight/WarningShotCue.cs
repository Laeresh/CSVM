using Godot;

namespace CSVM.Flight;

/// <summary>
/// The shipped near-miss accumulator (player.json <c>warning_shot_max</c> /
/// <c>_dissipation</c> / <c>_interval</c>), lifted out of <see cref="FlightController"/> so it
/// unit-tests without a live node — the same split <see cref="WeaponCursor"/> uses. One round
/// passing close accrues 1.0, saturating at <c>max</c>, draining at <c>dissipation</c> per
/// second, cue re-triggering no faster than <c>interval</c>.
/// ⚠ The three values ship but their units do not; this reading is chosen. The
/// <see cref="PassRadius"/> tune is explained at its own declaration.</summary>
public sealed class WarningShotCue
{
    /// <summary>How close a round's swept segment must pass to the aircraft to count, in metres.
    /// TUNE — chosen, not read: roughly one wingspan out, so a pass that reads as frightening
    /// sounds and a round crossing the sky two hundred metres off does not (<c>BL-230</c>).
    /// The sound def's <c>RANGE [20,200]</c> is a 3D falloff window, NOT a trigger radius — never
    /// read one as the other.</summary>
    public const float PassRadius = 15f;

    private readonly float _max;
    private readonly float _dissipation;
    private readonly float _interval;

    private float _intensity;
    private float _sinceCue;

    public WarningShotCue(float max, float dissipation, float interval)
    {
        _max = max;
        _dissipation = dissipation;
        _interval = interval;
        _sinceCue = interval;   // the first pass of a sortie sounds immediately
    }

    /// <summary>The live accumulator, 0..max. Nothing reads it yet beyond the log breadcrumb and
    /// the tests — it is the decoded half of the shipped block, kept so a later battle-state or
    /// HUD consumer has it rather than re-deriving one.</summary>
    public float Intensity => _intensity;

    /// <summary>Whether the interval has elapsed since the last cue — <see cref="Register"/> would
    /// sound on a pass right now.</summary>
    public bool Ready => _sinceCue >= _interval;

    /// <summary>Distance from point <paramref name="p"/> to the segment <paramref name="a"/>→
    /// <paramref name="b"/>. The round's whole step must be tested, not its endpoints: a gun round
    /// covers ~8 m per 60 Hz frame, so a per-frame point test misses most passes outright.</summary>
    public static float SegmentPointDistance(Vector3 a, Vector3 b, Vector3 p)
    {
        var seg = b - a;
        float lenSq = seg.LengthSquared();
        if (lenSq <= 1e-12f)
            return (p - a).Length();
        float t = Mathf.Clamp((p - a).Dot(seg) / lenSq, 0f, 1f);
        return (p - (a + seg * t)).Length();
    }

    /// <summary>Drains the accumulator and ages the re-trigger gate by one sim step.</summary>
    public void Tick(float dt)
    {
        _sinceCue += dt;
        if (_intensity > 0f)
            _intensity = Mathf.Max(0f, _intensity - _dissipation * dt);
    }

    /// <summary>One round passed close. Accrues intensity (saturating at max) and reports whether
    /// the cue should sound — false while the interval since the last one has not elapsed, however
    /// many rounds arrive meanwhile.</summary>
    public bool Register(float amount = 1f)
    {
        _intensity = Mathf.Min(_max, _intensity + amount);
        if (_sinceCue < _interval)
            return false;
        _sinceCue = 0f;
        return true;
    }
}
