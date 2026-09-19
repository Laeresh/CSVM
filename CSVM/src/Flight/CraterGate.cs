namespace CSVM.Flight;

/// <summary>Which rule, if either, hands one ground strike to the crater sink.
/// <c>Faithful</c> is the original's own, and a carve taken through it suppresses the weapon's
/// impact animation as the original's AND does; <c>Optional</c> is the remake's Enhanced Graphics
/// carve, which adds a bowl and takes nothing away.</summary>
public enum CraterAsk
{
    /// <summary>Nothing is asked and the ground is left intact.</summary>
    None,

    /// <summary>The struck node carries <c>CAN_MODIFY</c> and the weapon carries <c>CRATER</c>.</summary>
    Faithful,

    /// <summary>The Rocket Craters option is on and the weapon is a rocket warhead.</summary>
    Optional,
}

/// <summary>
/// Whether a round's ground strike asks for a crater. The original's rule is the struck node's
/// <c>CAN_MODIFY</c> flag over a <c>CRATER</c> weapon, and no shipped node carries the flag, so a
/// played round never carves (docs/org/craters.md). The Enhanced Graphics page's Rocket Craters row
/// opens a second door, off unless the player turns it on, under which every rocket warhead's
/// ground burst carves. The decision is a pure function of the two rules so a unit reaches it with
/// no scene behind it; <see cref="Enabled"/> is the run's answer to the option, which
/// <c>ProjectilePool</c> passes in.
/// </summary>
public static class CraterGate
{
    /// <summary>Whether the Rocket Craters option is armed for this run. Off in a shipped default
    /// and under <c>--det</c>, which reads no saved option, so no pinned golden can see a carve;
    /// the VIDEO page's row and <c>--craters</c> are its two sources.</summary>
    public static bool Enabled { get; set; }

    /// <summary>Which rule, if either, this strike goes through: the weapon, whether the struck
    /// collider's node carries <c>CAN_MODIFY</c>, and whether the option is on. The faithful rule
    /// is tested first, so a stamped node keeps the original's suppressing carve whatever the
    /// option says.</summary>
    public static CraterAsk For(WeaponDef weapon, bool canModify, bool optionOn) =>
        canModify && weapon.Crater ? CraterAsk.Faithful
        : optionOn && weapon.IsRocket ? CraterAsk.Optional
        : CraterAsk.None;
}
