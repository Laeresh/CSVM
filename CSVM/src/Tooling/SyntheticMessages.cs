using CSVM.Mech3;

namespace CSVM.Tooling;

/// <summary>
/// The synthetic run's message table, every value invented. Its keys are the ones the stand-in
/// records name and the ones the HUD, the match lines and the dock prompt read directly.
/// </summary>
public static class SyntheticMessages
{
    private static readonly (string Key, string Value)[] Entries =
    {
        ("MSG_PROBE_PLANE", "Probe Plane"),
        ("MSG_PROBE_GUN", "Probe Gun"),
        ("MSG_PROBE_ROCKET", "Probe Rocket"),
        ("MSG_UNKNOWN", "Unnamed probe"),
        ("MSG_CRASH", "Probe crashed"),
        ("MSG_SHOT_DOWN", "probe shot down"),
        ("MSG_WINGMAN_SHOT_DOWN", "Probe wingman lost"),
        ("MSG_DESTROYED", "probe destroyed"),
        ("MSG_PRESS_AUTOLAND", "Probe dock: press %1"),
        ("MSG_CLICK_AUTOLAND", "Probe dock: click %1"),
        ("MSG_PROBE_FIGHTER", "Probe Fighter"),
        ("MSG_PROBE_TORPEDO", "Probe torpedo"),
        ("MSG_PROBE_HE", "Probe burst rocket"),
        ("MSG_PROBE_CHOKER", "Probe choke rocket"),
        ("MSG_PROBE_FLARE", "Probe flare"),
        ("MSG_PROBE_SONIC", "Probe shock rocket"),
        ("MSG_PROBE_FLASH", "Probe dazzle rocket"),
        ("MSG_PROBE_SEEKER", "Probe homing rocket"),
        ("MSG_PROBE_BEEPER", "Probe marker rocket"),
        ("MSG_PROBE_FLAK", "Probe airburst rocket"),
        ("MSG_PROBE_INCENDIARY", "Probe motor rocket"),
        ("MSG_PROBE_SMOKER", "Probe smoke pot"),
        ("MSG_PROBE_BLAST_ROCKET", "Probe Blast Rocket"),
        ("MSG_TIME_EXPIRED", "Probe clock out"),
        ("MSG_MISSION_LOST", "Probe sortie failed"),
        ("MSG_MP_SUICIDE", "%1 probe self-loss"),
        ("MSG_DESTROYED_BY_X", "probe downed by %1"),
        ("MSG_MP_KILLED_X_TURRET", "Probe turret of %1 scored"),
        ("MSG_ONE_LIFE", "Probe: last life"),
        ("MSG_STATE_GAMEOVER", "Probe match over"),
        ("MSG_MP_ALL_ALONE", "Probe field cleared"),
        ("MSG_MP_REARMED", "Probe pad rearm"),
        ("MSG_X_YOUR", "Our"),
        ("MSG_X_ENEMY", "Rival"),
        ("MSG_MP_YOUR_BASE", "Probe home pad"),
        ("MSG_MP_ENEMY_BASE", "Probe rival pad"),
        ("MSG_MP_YOUR_FLAG_BASE", "Probe pennant on our pad"),
        ("MSG_MP_ENEMY_FLAG_BASE", "Probe pennant on rival pad"),
        ("MSG_FLAG_CAP_BY", "%1 probe pennant lifted by %2"),
        ("MSG_YOUR_FLAG_FLOAT", "Our probe pennant adrift"),
        ("MSG_ENEMY_FLAG_FLOAT", "Rival probe pennant adrift"),
        ("MSG_HOLDS_FLAG", "%1 flies %2 probe pennant"),
        ("MSG_PROBE_BRAND", "Probe Lancer"),
        ("MSG_PROBE_AVENGER", "Probe Bulwark"),
        ("MSG_PROBE_CAL30", "Probe light gun"),
        ("MSG_PROBE_CAL40", "Probe medium gun"),
        ("MSG_PROBE_CAL50", "Probe heavy gun"),
        ("MSG_PROBE_CAL60", "Probe cannon"),
        ("MSG_PROBE_CAL70", "Probe great cannon"),
        ("MSG_PROBE_PYLON_HE", "Probe pylon rocket"),
        ("MSG_PROBE_GUNHIT", "Probe Strike Gun"),
        ("MSG_PROBE_HAWK", "Probe Kite"),
        ("MSG_PROBE_GYRO", "Probe Whirl"),
        ("MSG_PROBE_FURY", "Probe Gale"),
        ("MSG_PROBE_PEACE", "Probe Warden"),
        ("MSG_PROBE_WARHAWK", "Probe Hauler"),
        ("MSG_PROBE_KESTREL", "Probe Heron"),
        ("MSG_PROBE_ZEPTORP", "Probe hull torpedo"),
        ("MSG_TUR_PROBE_REAR_G3", "Probe tail gunner"),
        ("MSG_TUR_PROBE_AA", "Probe ground gun"),
        ("MSG_TUR_PROBE_DORSAL_G3", "Probe dorsal gunner"),
    };

    /// <summary>The table, a fresh instance per call.</summary>
    public static Messages Table() => Messages.Of(Entries);
}
