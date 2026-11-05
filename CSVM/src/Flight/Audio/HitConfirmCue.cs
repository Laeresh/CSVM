using CSVM.Flight.Weapons;

namespace CSVM.Flight.Audio;

/// <summary>
/// The hit confirm's two rules: which strike earns one, and how often one may sound. A remake-only
/// cue, since the guns' own <c>player</c> impact rows bind no sound (docs/org/weaponImpact.md). The
/// cue itself is <see cref="FlightAudio.OnHitConfirm"/>, on the gunner's own flat mix.
/// </summary>
public sealed class HitConfirmCue
{
    // The sim time the last confirm sounded; the first strike always may.
    private double _last = double.NegativeInfinity;

    /// <summary>Whether a strike on an aircraft earns its gunner a confirm: a human pilot's gun round
    /// on an aircraft hostile to that pilot (<see cref="AimAssist.Hostile"/>). A bot seat, a friendly
    /// or a neutral earns none. Only a seat flown on this machine carries a <see cref="FlightAudio"/>
    /// to hear it on, and the caller plays it there.</summary>
    public static bool Earns(bool gunRound, bool gunnerHuman, int gunnerTeam, int struckTeam) =>
        gunRound && gunnerHuman && AimAssist.Hostile(gunnerTeam, struckTeam);

    /// <summary>Whether a confirm may sound at sim time <paramref name="now"/>, stamping it when it
    /// may: at least <paramref name="interval"/> seconds after the last one.</summary>
    public bool TryFire(double now, float interval)
    {
        if (now - _last < interval)
        {
            return false;
        }

        _last = now;
        return true;
    }
}
