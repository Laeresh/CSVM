using System;

namespace CSVM.Flight;

/// <summary>
/// The decoded canopy-glass cadence: gun rounds landing on the pilot's own aeroplane are counted
/// over the shipped <c>warning_shot_interval</c>, and an interval that closed with at least one hit
/// may open one of the five <c>bullethole_anims</c> holes, whose def is what sounds
/// <c>window_hit_sg</c>. A hole opens once per sortie. Engine-free so the rule unit-tests without a
/// live node, the same split <see cref="WarningShotCue"/> uses.
/// Decode: <c>docs/org/weaponFire.md</c>, "The incoming-fire cues".
/// </summary>
public sealed class CanopyHoleCue
{
    /// <summary>The glass cue the five hole defs sound. Not a player.json field: the name is
    /// authored in the defs themselves (<c>cockpit_bulletholes.zrd</c>), unlike the ricochet's
    /// <c>bullet_hit_sound</c>.</summary>
    public const string WindowHitSound = "window_hit_sg";

    /// <summary>Holes a player def's <c>bullethole_anims</c> names, <c>bullet1</c>..<c>bullet5</c>,
    /// the same five on every player def.</summary>
    public const int HoleCount = 5;

    /// <summary>The chance an eligible interval opens a hole. Read, not chosen: the original draws
    /// <c>rand()/32768</c> and goes on only above 0.7 (<c>0x004b12ae</c>).</summary>
    public const float HoleChance = 0.3f;

    private readonly float _interval;
    private readonly bool[] _opened = new bool[HoleCount];
    private float _sinceInterval;
    private int _hits;

    public CanopyHoleCue(float interval) => _interval = interval;

    /// <summary>Holes still closed. The gate compares the airframe's health fraction against this
    /// share, so a pristine canopy needs damage before its first hole and each further one needs
    /// more.</summary>
    public int ClosedCount
    {
        get
        {
            int closed = 0;
            for (int i = 0; i < _opened.Length; i++)
                if (!_opened[i])
                    closed++;
            return closed;
        }
    }

    /// <summary>One gun round landed on this aeroplane. The interval decides whether it is heard;
    /// however many arrive meanwhile, an interval opens at most one hole.</summary>
    public void Register() => _hits++;

    /// <summary>Ages the interval and answers with the hole number (1..5) it opened, or null. The
    /// caller passes the whole-vehicle health fraction and the draw stream, so the rule itself owns
    /// no clock and no randomness.</summary>
    public int? Tick(float dt, float healthFraction, Random rng)
    {
        _sinceInterval += dt;
        if (_sinceInterval < _interval)
            return null;
        _sinceInterval = 0f;
        int hits = _hits;
        _hits = 0;
        int closed = ClosedCount;
        if (hits == 0 || closed == 0 || healthFraction >= (float)closed / HoleCount)
            return null;
        if (rng.NextDouble() <= 1.0 - HoleChance)
            return null;
        // The span is the closed count LESS ONE (the DEC at 0x004b12c5), so the last closed hole is
        // reached only on a maximal draw. Reproduced rather than corrected: it is the shipped bias.
        int pick = (int)(rng.NextDouble() * (closed - 1));
        int seen = 0;
        for (int i = 0; i < _opened.Length; i++)
        {
            if (_opened[i])
                continue;
            if (seen == pick)
            {
                _opened[i] = true;
                return i + 1;
            }
            seen++;
        }
        return null;
    }

    /// <summary>Back to a pristine canopy, which is what the <c>reset_bulletholes</c> spawn anim
    /// does for the decals.</summary>
    public void Reset()
    {
        Array.Clear(_opened, 0, _opened.Length);
        _sinceInterval = 0f;
        _hits = 0;
    }
}
