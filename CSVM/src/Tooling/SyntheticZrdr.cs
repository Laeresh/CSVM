using System;
using System.Collections.Generic;
using CSVM.Mech3;

namespace CSVM.Tooling;

/// <summary>
/// The synthetic run's reader scopes, built in code. The root holds the invented records of the
/// stand-in aircraft, armament, effects, sounds, voice, campaign and briefing. The C1 chapter
/// scope is empty, handed to a flight for its speed cue. <c>C1/PROBE1</c> holds the
/// weather, sun and net-table suites' records, <c>C1/M09</c> the campaign's objectives note, and
/// each Instant Action chapter's <c>IA1</c> the menu's <c>ia</c> record. Every record is
/// hand-authored from <c>docs/formats/</c>. Engine-free.
/// </summary>
public static class SyntheticZrdr
{
    // The chapters whose Instant Action scope carries a def, the ones the menu's presets fly. ⚠ Not
    // C1: versus-spawn-rotation reads a C1/IA1 scope as an install's spawn lists, and the empty
    // stage's ring stands in without one.
    private static readonly string[] InstantActionChapters = { "C1B", "C3", "C4", "C5" };

    /// <summary>The root scope, what <c>ZBD/zrdr.zbd</c> is to an install.</summary>
    public static ZrdrSet Root() => new("synthetic zrdr.zbd", new[]
    {
        File("vehicle.zrd", Vehicle()),
        File("engines.zrd", Engines()),
        File("player.zrd", Player()),
        File("maneuvers.zrd", Maneuvers()),
        File("weapons.zrd", Weapons()),
        File("shakes.zrd", Shakes()),
        File("ai.zrd", Ai()),
        File("probe_world.zrd", ProbeWorld()),
        File("flame_ball.zrd", FlameBall()),
        File("pufftrails.zrd", PuffTrails()),
        File("cm_sequence.zrd", CampaignSequence()),
        File("Briefing.zrd", Briefing()),
        File("sounds.zrd", Sounds()),
        File("voice.zrd", Voice()),
    });

    /// <summary>A chapter's scope: C1's holds no readers, every other chapter has none.</summary>
    public static ZrdrSet Chapter(string chapter) =>
        string.Equals(chapter, SyntheticTextures.Chapter, StringComparison.OrdinalIgnoreCase)
            ? new ZrdrSet($"synthetic {chapter}/zrdr.zbd", Array.Empty<ZrdrFile>())
            : ZrdrSet.None;

    /// <summary>A mission's scope, <see cref="ZrdrSet.None"/> for a mission the run does not invent.</summary>
    public static ZrdrSet Mission(string chapter, string mission)
    {
        string label = $"synthetic {chapter}/{mission}/zrdr.zbd";
        if (Is(chapter, SyntheticMission.Chapter) && Is(mission, SyntheticMission.Mission))
        {
            return new ZrdrSet(label, new[] { File("weather.zrd", Weather()), File("net.zrd", Net()) });
        }

        if (Is(chapter, SyntheticMission.Chapter) && Is(mission, SyntheticMission.CampaignMission))
        {
            return new ZrdrSet(label, new[] { File("objectives.zrd", Objectives()) });
        }

        if (Is(mission, "IA1") && Array.Exists(InstantActionChapters, c => Is(c, chapter)))
        {
            return new ZrdrSet(label, new[] { File("ia.zrd", InstantAction()) });
        }

        return ZrdrSet.None;
    }

    private static bool Is(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static ZrdrFile File(string name, List<object?> root) => new(name, null, root);

    private static List<object?> L(params object?[] items) => new(items);

    private static List<object?> Vehicle() =>
        L(
            L(
                "player_airplane", L(
                    "flight_ceiling", L(2200f),
                    "engine_sound", L("snd_probe_engine"),
                    "cockpit_engine_sound", L("snd_probe_engine_cp"),
                    "damaged_engine_sound", L(L("snd_probe_engine_damaged", 0.6f, 0.9f))),
                "pprobe", L(
                    "kind_of", L("player_airplane"),
                    "nodename", L("probe_plane"),
                    "title", L("MSG_PROBE_PLANE"),
                    "engine", L(2f),
                    "fuel", L(90f),
                    "dynamics", L(
                        "pitch_torque", L(2f),
                        "roll_torque", L(5f),
                        "rudder_torque", L(1.2f),
                        "return_rate", L(2.5f),
                        "ang_momentum_damp", L(4.5f),
                        "rec_moments_inertia", L(0.9f, 0.7f, 1.1f),
                        "fd_speed", L(120f),
                        "drag_factor", L(1f),
                        "veh_weight", L(3000f),
                        "ref_area", L(300f)),
                    "destroyable_parts", L(
                        L(
                            "nose", 30f, 30f, "critical", "engine",
                            "injure_anims", L(L(0.25f, "pdpanel7", "probe_plane"))),
                        L("tail", 20f, 20f, "injure_anims", L(L(0.25f, "pdpanel8", "probe_plane"))),
                        L(
                            "leftwing", 25f, 25f,
                            "injure_anims", L(
                                L(0.6f, "pdpanel3", "probe_plane"),
                                L(0.35f, "pdpanel2", "probe_plane"),
                                L(0.1f, "pdpanel5", "probe_plane"))),
                        L(
                            "rightwing", 25f, 25f,
                            "injure_anims", L(
                                L(0.6f, "pdpanel4", "probe_plane"),
                                L(0.35f, "pdpanel1", "probe_plane"),
                                L(0.1f, "pdpanel6", "probe_plane")))),
                    "collision", L(
                        L(0f, 0f, -4.6f),
                        L(0f, -0.1f, 4.5f),
                        L(5f, -0.1f, 0.5f),
                        L(0f, 0.7f, -0.5f),
                        L(-5f, -0.1f, -0.5f),
                        L(0f, -0.7f, 3f))),
                "probe", L(
                    "title", L("MSG_PROBE_PLANE"),
                    "armor", L(60f),
                    "health", L(60f),
                    "dare_devil", L(5f),
                    "natural_touch", L(5f),
                    "sixth_sense", L(5f),
                    "dead_eye", L(5f),
                    "quick_draw", L(5f),
                    "steady_hand", L(5f),
                    "stun_recovery", L(5f),
                    "talker", L(5f),
                    "constitution", L(5f),
                    "weapons", L(
                        L("wep_probe_gun", 400f, 0.5f, 0f, 800f),
                        L("wep_probe_rocket", 4f, 3f, 150f, 1500f)),
                    "collision", L(L(0f, 0f, 0f))),
                "pprobefighter", L(
                    "kind_of", L("player_airplane"),
                    "nodename", L("player_pfighter"),
                    "title", L("MSG_PROBE_FIGHTER"),
                    "engine_sound", L("snd_probe_fighter_engine"),
                    "engine", L(3f),
                    "fuel", L(70f),
                    "dynamics", L(
                        "pitch_torque", L(2.7f),
                        "roll_torque", L(6.6f),
                        "rudder_torque", L(1.6f),
                        "return_rate", L(3.3f),
                        "ang_momentum_damp", L(5.4f),
                        "rec_moments_inertia", L(0.75f, 0.55f, 1.05f),
                        "fd_speed", L(135f),
                        "drag_factor", L(0.9f),
                        "veh_weight", L(2600f),
                        "ref_area", L(260f)),
                    "destroyable_parts", L(
                        L(
                            "nose", 22f, 22f, "critical", "engine",
                            "injure_anims", L(L(0.3f, "pdpanel7", "player_pfighter"))),
                        L("tail", 16f, 16f, "injure_anims", L(L(0.3f, "pdpanel8", "player_pfighter"))),
                        L(
                            "leftwing", 18f, 18f,
                            "injure_anims", L(
                                L(0.55f, "pdpanel3", "player_pfighter"),
                                L(0.3f, "pdpanel2", "player_pfighter"),
                                L(0.1f, "pdpanel5", "player_pfighter"))),
                        L(
                            "rightwing", 18f, 18f,
                            "injure_anims", L(
                                L(0.55f, "pdpanel4", "player_pfighter"),
                                L(0.3f, "pdpanel1", "player_pfighter"),
                                L(0.1f, "pdpanel6", "player_pfighter")))),
                    "collision", L(
                        L(0f, 0f, -3.5f),
                        L(0f, -0.1f, 3.6f),
                        L(6.8f, -0.2f, -0.2f),
                        L(0f, 0.8f, -0.2f),
                        L(-6.8f, -0.2f, -0.2f),
                        L(0f, -0.5f, 2f))),
                "probefighter", L(
                    "title", L("MSG_PROBE_FIGHTER"),
                    "mode", L("jet"),
                    "armor", L(45f),
                    "health", L(45f),
                    "dare_devil", L(7f),
                    "natural_touch", L(6f),
                    "sixth_sense", L(4f),
                    "dead_eye", L(6f),
                    "quick_draw", L(7f),
                    "steady_hand", L(4f),
                    "stun_recovery", L(6f),
                    "talker", L(3f),
                    "constitution", L(4f),
                    "weapons", L(
                        L("wep_probe_gun", 300f, 0.4f, 0f, 700f),
                        L("wep_probe_rocket", 2f, 4f, 200f, 1400f)),
                    "collision", L(L(0f, 0f, 0f))),
                "wingman", L(
                    "kind_of", L("probefighter"),
                    "mode", L("wingman"),
                    "dare_devil", L(3f),
                    "steady_hand", L(7f)),
                "pprobebrand", L(
                    "kind_of", L("player_airplane"),
                    "nodename", L("player_fbrand"),
                    "title", L("MSG_PROBE_BRAND"),
                    "turrets", L(
                        "thirdp", L(
                            L("title", L("MSG_TUR_PROBE_DORSAL_G3"), "node", L("probe_brand_turret1")))),
                    "engine_sound", L("snd_probe_brand_engine"),
                    "engine", L(31f),
                    "fuel", L(80f),
                    "dynamics", L(
                        "pitch_torque", L(2.3f),
                        "roll_torque", L(5.6f),
                        "rudder_torque", L(1.4f),
                        "return_rate", L(2.9f),
                        "ang_momentum_damp", L(4.9f),
                        "rec_moments_inertia", L(0.85f, 0.6f, 1.15f),
                        "fd_speed", L(128f),
                        "drag_factor", L(0.85f),
                        "veh_weight", L(2800f),
                        "ref_area", L(240f)),
                    "destroyable_parts", L(
                        L(
                            "nose", 26f, 26f, "critical", "engine",
                            "injure_anims", L(L(0.28f, "pdpanel7", "player_fbrand"))),
                        L("tail", 18f, 18f, "injure_anims", L(L(0.28f, "pdpanel8", "player_fbrand"))),
                        L(
                            "leftwing", 20f, 20f,
                            "injure_anims", L(
                                L(0.5f, "pdpanel3", "player_fbrand"),
                                L(0.3f, "pdpanel2", "player_fbrand"),
                                L(0.12f, "pdpanel5", "player_fbrand"))),
                        L(
                            "rightwing", 20f, 20f,
                            "injure_anims", L(
                                L(0.5f, "pdpanel4", "player_fbrand"),
                                L(0.3f, "pdpanel1", "player_fbrand"),
                                L(0.12f, "pdpanel6", "player_fbrand")))),
                    "collision", L(
                        L(0f, 0f, -4.5f),
                        L(0f, -0.1f, 4.7f),
                        L(5.4f, -0.2f, -0.3f),
                        L(0f, 0.7f, -0.3f),
                        L(-5.4f, -0.2f, -0.3f),
                        L(0f, -0.45f, 2.6f))),
                "probebrand", L(
                    "title", L("MSG_PROBE_BRAND"),
                    "mode", L("jet"),
                    "turrets", L(
                        "thirdp", L(
                            L("title", L("MSG_TUR_PROBE_DORSAL_G3"), "node", L("probe_brand_turret1")))),
                    "armor", L(50f),
                    "health", L(50f),
                    "dare_devil", L(6f),
                    "natural_touch", L(5f),
                    "sixth_sense", L(6f),
                    "dead_eye", L(5f),
                    "quick_draw", L(6f),
                    "steady_hand", L(5f),
                    "stun_recovery", L(5f),
                    "talker", L(4f),
                    "constitution", L(5f),
                    "weapons", L(
                        L("wep_probe_gun", 350f, 0.45f, 0f, 750f),
                        L("wep_probe_rocket", 2f, 5f, 180f, 1300f)),
                    "collision", L(L(0f, 0f, 0f))),
                "pprobeavenger", L(
                    "kind_of", L("player_airplane"),
                    "nodename", L("player_avenger"),
                    "title", L("MSG_PROBE_AVENGER"),
                    "engine_sound", L("snd_probe_avenger_engine"),
                    "engine", L(19f),
                    "fuel", L(110f),
                    "dynamics", L(
                        "pitch_torque", L(1.8f),
                        "roll_torque", L(4.4f),
                        "rudder_torque", L(1.1f),
                        "return_rate", L(2.2f),
                        "ang_momentum_damp", L(4.2f),
                        "rec_moments_inertia", L(1f, 0.8f, 1.25f),
                        "fd_speed", L(112f),
                        "drag_factor", L(1.1f),
                        "veh_weight", L(3600f),
                        "ref_area", L(340f)),
                    "destroyable_parts", L(
                        L(
                            "nose", 34f, 34f, "critical", "engine",
                            "injure_anims", L(L(0.22f, "pdpanel7", "player_avenger"))),
                        L("tail", 24f, 24f, "injure_anims", L(L(0.22f, "pdpanel8", "player_avenger"))),
                        L(
                            "leftwing", 28f, 28f,
                            "injure_anims", L(
                                L(0.6f, "pdpanel3", "player_avenger"),
                                L(0.35f, "pdpanel2", "player_avenger"),
                                L(0.1f, "pdpanel5", "player_avenger"))),
                        L(
                            "rightwing", 28f, 28f,
                            "injure_anims", L(
                                L(0.6f, "pdpanel4", "player_avenger"),
                                L(0.35f, "pdpanel1", "player_avenger"),
                                L(0.1f, "pdpanel6", "player_avenger")))),
                    "collision", L(
                        L(0f, 0f, -4f),
                        L(0f, -0.1f, 4.1f),
                        L(8.2f, -0.25f, -0.2f),
                        L(0f, 1f, -0.2f),
                        L(-8.2f, -0.25f, -0.2f),
                        L(0f, -0.6f, 2.3f))),
                "probeavenger", L(
                    "title", L("MSG_PROBE_AVENGER"),
                    "mode", L("jet"),
                    "armor", L(70f),
                    "health", L(70f),
                    "dare_devil", L(4f),
                    "natural_touch", L(4f),
                    "sixth_sense", L(5f),
                    "dead_eye", L(7f),
                    "quick_draw", L(4f),
                    "steady_hand", L(6f),
                    "stun_recovery", L(7f),
                    "talker", L(5f),
                    "constitution", L(7f),
                    "weapons", L(
                        L("wep_probe_gun", 450f, 0.55f, 0f, 850f),
                        L("wep_probe_rocket", 6f, 3f, 150f, 1600f)),
                    "collision", L(L(0f, 0f, 0f))),
                "pprobehawk", L(
                    "kind_of", L("player_airplane"),
                    "nodename", L("player_bhawk"),
                    "title", L("MSG_PROBE_HAWK"),
                    "engine_sound", L("snd_probe_hawk_engine"),
                    "engine", L(10f),
                    "fuel", L(75f),
                    "dynamics", L(
                        "pitch_torque", L(2.5f),
                        "roll_torque", L(6.1f),
                        "rudder_torque", L(1.5f),
                        "return_rate", L(3.1f),
                        "ang_momentum_damp", L(5.1f),
                        "rec_moments_inertia", L(0.8f, 0.58f, 1.08f),
                        "fd_speed", L(126f),
                        "drag_factor", L(0.95f),
                        "veh_weight", L(2750f),
                        "ref_area", L(275f)),
                    "destroyable_parts", L(
                        L(
                            "nose", 24f, 24f, "critical", "engine",
                            "injure_anims", L(L(0.27f, "pdpanel7", "player_bhawk"))),
                        L("tail", 17f, 17f, "injure_anims", L(L(0.27f, "pdpanel8", "player_bhawk"))),
                        L(
                            "leftwing", 19f, 19f,
                            "injure_anims", L(
                                L(0.52f, "pdpanel3", "player_bhawk"),
                                L(0.32f, "pdpanel2", "player_bhawk"),
                                L(0.11f, "pdpanel5", "player_bhawk"))),
                        L(
                            "rightwing", 19f, 19f,
                            "injure_anims", L(
                                L(0.52f, "pdpanel4", "player_bhawk"),
                                L(0.32f, "pdpanel1", "player_bhawk"),
                                L(0.11f, "pdpanel6", "player_bhawk")))),
                    "collision", L(
                        L(0f, 0f, -3.15f),
                        L(0f, -0.1f, 3.24f),
                        L(6.46f, -0.2f, -0.18f),
                        L(0f, 0.8f, -0.18f),
                        L(-6.46f, -0.2f, -0.18f),
                        L(0f, -0.5f, 1.8f))),
                "probehawk", L(
                    "title", L("MSG_PROBE_HAWK"),
                    "mode", L("jet"),
                    "armor", L(48f),
                    "health", L(48f),
                    "dare_devil", L(6f),
                    "natural_touch", L(7f),
                    "sixth_sense", L(5f),
                    "dead_eye", L(6f),
                    "quick_draw", L(5f),
                    "steady_hand", L(5f),
                    "stun_recovery", L(4f),
                    "talker", L(6f),
                    "constitution", L(5f),
                    "weapons", L(
                        L("wep_probe_gun", 380f, 0.42f, 0f, 760f),
                        L("wep_probe_rocket", 3f, 4f, 170f, 1350f)),
                    "collision", L(L(0f, 0f, 0f))),
                "wbloodhawk", L(
                    "kind_of", L("probehawk"),
                    "mode", L("wingman"),
                    "target_bias", L(-60f),
                    "struct_bias", L(-180f),
                    "dare_devil", L(4f),
                    "steady_hand", L(6f)),
                "pprobegyro", L(
                    "kind_of", L("player_airplane"),
                    "nodename", L("player_autogyro"),
                    "title", L("MSG_PROBE_GYRO"),
                    "engine_sound", L("snd_probe_gyro_engine"),
                    "engine", L(37f),
                    "fuel", L(60f),
                    "is_autogyro",
                    "spin_props_anim", L("agyro_rotors"),
                    "dynamics", L(
                        "pitch_torque", L(2.9f),
                        "roll_torque", L(4.9f),
                        "rudder_torque", L(21f),
                        "return_rate", L(1.7f),
                        "ang_momentum_damp", L(4.6f),
                        "rec_moments_inertia", L(0.7f, 0.9f, 0.95f),
                        "fd_speed", L(98f),
                        "drag_factor", L(1.25f),
                        "veh_weight", L(2100f),
                        "ref_area", L(230f)),
                    "destroyable_parts", L(
                        L(
                            "nose", 15f, 15f, "critical", "engine",
                            "injure_anims", L(L(0.32f, "pdpanel7", "player_autogyro"))),
                        L("tail", 14f, 14f, "injure_anims", L(L(0.32f, "pdpanel8", "player_autogyro"))),
                        L(
                            "leftwing", 15f, 15f,
                            "injure_anims", L(
                                L(0.58f, "pdpanel3", "player_autogyro"),
                                L(0.34f, "pdpanel2", "player_autogyro"),
                                L(0.13f, "pdpanel5", "player_autogyro"))),
                        L(
                            "rightwing", 15f, 15f,
                            "injure_anims", L(
                                L(0.58f, "pdpanel4", "player_autogyro"),
                                L(0.34f, "pdpanel1", "player_autogyro"),
                                L(0.13f, "pdpanel6", "player_autogyro")))),
                    "collision", L(
                        L(0f, 0f, -2.8f),
                        L(0f, -0.12f, 2.88f),
                        L(4.76f, -0.23f, -0.16f),
                        L(0f, 0.92f, -0.16f),
                        L(-4.76f, -0.23f, -0.16f),
                        L(0f, -0.58f, 1.6f))),
                "probegyro", L(
                    "title", L("MSG_PROBE_GYRO"),
                    "mode", L("jet"),
                    "spin_props_anim", L("agyro_rotors"),
                    "armor", L(38f),
                    "health", L(38f),
                    "dare_devil", L(4f),
                    "natural_touch", L(5f),
                    "sixth_sense", L(7f),
                    "dead_eye", L(4f),
                    "quick_draw", L(5f),
                    "steady_hand", L(6f),
                    "stun_recovery", L(5f),
                    "talker", L(7f),
                    "constitution", L(4f),
                    "weapons", L(
                        L("wep_probe_gun", 260f, 0.5f, 0f, 650f),
                        L("wep_probe_rocket", 2f, 5f, 160f, 1200f)),
                    "collision", L(L(0f, 0f, 0f))),
                "autogyro", L("kind_of", L("probegyro"), "title", L("MSG_PROBE_GYRO"), "talker", L(3f)),
                "pprobefury", L(
                    "kind_of", L("player_airplane"),
                    "nodename", L("player_fury"),
                    "title", L("MSG_PROBE_FURY"),
                    "engine_sound", L("snd_probe_fury_engine"),
                    "engine", L(16f),
                    "fuel", L(85f),
                    "dynamics", L(
                        "pitch_torque", L(2.2f),
                        "roll_torque", L(5.3f),
                        "rudder_torque", L(1.3f),
                        "return_rate", L(2.7f),
                        "ang_momentum_damp", L(4.7f),
                        "rec_moments_inertia", L(0.88f, 0.64f, 1.18f),
                        "fd_speed", L(131f),
                        "drag_factor", L(0.88f),
                        "veh_weight", L(3100f),
                        "ref_area", L(290f)),
                    "destroyable_parts", L(
                        L(
                            "nose", 34f, 8f, "critical", "engine",
                            "injure_anims", L(L(0.24f, "pdpanel7", "player_fury"))),
                        L("tail", 24f, 6f, "injure_anims", L(L(0.24f, "pdpanel8", "player_fury"))),
                        L(
                            "leftwing", 28f, 7f,
                            "injure_anims", L(
                                L(0.57f, "pdpanel3", "player_fury"),
                                L(0.33f, "pdpanel2", "player_fury"),
                                L(0.09f, "pdpanel5", "player_fury"))),
                        L(
                            "rightwing", 28f, 7f,
                            "injure_anims", L(
                                L(0.57f, "pdpanel4", "player_fury"),
                                L(0.33f, "pdpanel1", "player_fury"),
                                L(0.09f, "pdpanel6", "player_fury")))),
                    "collision", L(
                        L(0f, 0f, -3.85f),
                        L(0f, -0.1f, 3.96f),
                        L(7.14f, -0.19f, -0.22f),
                        L(0f, 0.76f, -0.22f),
                        L(-7.14f, -0.19f, -0.22f),
                        L(0f, -0.48f, 2.2f))),
                "probefury", L(
                    "title", L("MSG_PROBE_FURY"),
                    "mode", L("jet"),
                    "injure_anims", L(
                        L(0.93f, "random_remote_damage"),
                        L(0.8f, "random_remote_damage"),
                        L(0.6f, "random_remote_damage"),
                        L(0.36f, "pfsmoketrail"),
                        L(0.25f, "random_remote_damage")),
                    "armor", L(58f),
                    "health", L(58f),
                    "dare_devil", L(5f),
                    "natural_touch", L(6f),
                    "sixth_sense", L(6f),
                    "dead_eye", L(7f),
                    "quick_draw", L(6f),
                    "steady_hand", L(4f),
                    "stun_recovery", L(6f),
                    "talker", L(4f),
                    "constitution", L(6f),
                    "weapons", L(
                        L("wep_probe_gun", 420f, 0.48f, 0f, 820f),
                        L("wep_probe_rocket", 4f, 4f, 190f, 1450f)),
                    "collision", L(L(0f, 0f, 0f))),
                "pprobepeace", L(
                    "kind_of", L("player_airplane"),
                    "nodename", L("player_peacemaker"),
                    "title", L("MSG_PROBE_PEACE"),
                    "engine_sound", L("snd_probe_peace_engine"),
                    "engine", L(13f),
                    "fuel", L(95f),
                    "dynamics", L(
                        "pitch_torque", L(2.1f),
                        "roll_torque", L(4.7f),
                        "rudder_torque", L(1.25f),
                        "return_rate", L(2.6f),
                        "ang_momentum_damp", L(4.4f),
                        "rec_moments_inertia", L(0.92f, 0.72f, 1.2f),
                        "fd_speed", L(118f),
                        "drag_factor", L(1.05f),
                        "veh_weight", L(3300f),
                        "ref_area", L(315f)),
                    "destroyable_parts", L(
                        L(
                            "nose", 31f, 31f, "critical", "engine",
                            "injure_anims", L(L(0.23f, "pdpanel7", "player_peacemaker"))),
                        L("tail", 21f, 21f, "injure_anims", L(L(0.23f, "pdpanel8", "player_peacemaker"))),
                        L(
                            "leftwing", 26f, 26f,
                            "injure_anims", L(
                                L(0.62f, "pdpanel3", "player_peacemaker"),
                                L(0.36f, "pdpanel2", "player_peacemaker"),
                                L(0.12f, "pdpanel5", "player_peacemaker"))),
                        L(
                            "rightwing", 26f, 26f,
                            "injure_anims", L(
                                L(0.62f, "pdpanel4", "player_peacemaker"),
                                L(0.36f, "pdpanel1", "player_peacemaker"),
                                L(0.12f, "pdpanel6", "player_peacemaker")))),
                    "collision", L(
                        L(0f, 0f, -3.33f),
                        L(0f, -0.1f, 3.42f),
                        L(7.82f, -0.21f, -0.19f),
                        L(0f, 0.84f, -0.19f),
                        L(-7.82f, -0.21f, -0.19f),
                        L(0f, -0.53f, 1.9f))),
                "probepeace", L(
                    "title", L("MSG_PROBE_PEACE"),
                    "mode", L("jet"),
                    "armor", L(62f),
                    "health", L(62f),
                    "dare_devil", L(3f),
                    "natural_touch", L(5f),
                    "sixth_sense", L(6f),
                    "dead_eye", L(5f),
                    "quick_draw", L(4f),
                    "steady_hand", L(7f),
                    "stun_recovery", L(6f),
                    "talker", L(5f),
                    "constitution", L(6f),
                    "weapons", L(
                        L("wep_probe_gun", 470f, 0.52f, 0f, 880f),
                        L("wep_probe_rocket", 5f, 3f, 160f, 1550f)),
                    "collision", L(L(0f, 0f, 0f))),
                "pprobewarhawk", L(
                    "kind_of", L("player_airplane"),
                    "nodename", L("player_warhawk"),
                    "title", L("MSG_PROBE_WARHAWK"),
                    "engine_sound", L("snd_probe_warhawk_engine"),
                    "engine", L(34f),
                    "fuel", L(120f),
                    "dynamics", L(
                        "pitch_torque", L(1.7f),
                        "roll_torque", L(4.1f),
                        "rudder_torque", L(1f),
                        "return_rate", L(2.1f),
                        "ang_momentum_damp", L(4f),
                        "rec_moments_inertia", L(1.05f, 0.85f, 1.3f),
                        "fd_speed", L(108f),
                        "drag_factor", L(1.15f),
                        "veh_weight", L(3900f),
                        "ref_area", L(360f)),
                    "destroyable_parts", L(
                        L(
                            "nose", 36f, 36f, "critical", "engine",
                            "injure_anims", L(L(0.21f, "pdpanel7", "player_warhawk"))),
                        L("tail", 26f, 26f, "injure_anims", L(L(0.21f, "pdpanel8", "player_warhawk"))),
                        L(
                            "leftwing", 30f, 30f,
                            "injure_anims", L(
                                L(0.63f, "pdpanel3", "player_warhawk"),
                                L(0.37f, "pdpanel2", "player_warhawk"),
                                L(0.08f, "pdpanel5", "player_warhawk"))),
                        L(
                            "rightwing", 30f, 30f,
                            "injure_anims", L(
                                L(0.63f, "pdpanel4", "player_warhawk"),
                                L(0.37f, "pdpanel1", "player_warhawk"),
                                L(0.08f, "pdpanel6", "player_warhawk")))),
                    "collision", L(
                        L(0f, 0f, -4.38f),
                        L(0f, -0.12f, 4.5f),
                        L(8.84f, -0.24f, -0.25f),
                        L(0f, 0.96f, -0.25f),
                        L(-8.84f, -0.24f, -0.25f),
                        L(0f, -0.6f, 2.5f))),
                "probewarhawk", L(
                    "title", L("MSG_PROBE_WARHAWK"),
                    "mode", L("jet"),
                    "armor", L(74f),
                    "health", L(74f),
                    "dare_devil", L(4f),
                    "natural_touch", L(4f),
                    "sixth_sense", L(5f),
                    "dead_eye", L(6f),
                    "quick_draw", L(5f),
                    "steady_hand", L(7f),
                    "stun_recovery", L(7f),
                    "talker", L(4f),
                    "constitution", L(7f),
                    "weapons", L(
                        L("wep_probe_gun", 520f, 0.58f, 0f, 900f),
                        L("wep_probe_rocket", 6f, 3f, 140f, 1650f)),
                    "collision", L(L(0f, 0f, 0f))),
                "bhatwarhawk", L(
                    "kind_of", L("probewarhawk"),
                    "title", L("MSG_PROBE_WARHAWK"),
                    "weapons", L(
                        L("wep_probe_zeptorp", 6f, 3f, 320f, 950f),
                        L("wep_probe_gun", 560f, 0.5f, 1f, 880f))),
                "pprobekestrel", L(
                    "kind_of", L("player_airplane"),
                    "nodename", L("player_kestrel"),
                    "title", L("MSG_PROBE_KESTREL"),
                    "turrets", L(
                        "thirdp", L(
                            L("title", L("MSG_TUR_PROBE_REAR_G3"), "node", L("probe_kestrel_turret1")))),
                    "engine_sound", L("snd_probe_kestrel_engine"),
                    "engine", L(28f),
                    "fuel", L(100f),
                    "dynamics", L(
                        "pitch_torque", L(1.95f),
                        "roll_torque", L(4.6f),
                        "rudder_torque", L(1.15f),
                        "return_rate", L(2.3f),
                        "ang_momentum_damp", L(4.3f),
                        "rec_moments_inertia", L(0.98f, 0.78f, 1.22f),
                        "fd_speed", L(114f),
                        "drag_factor", L(1.08f),
                        "veh_weight", L(3450f),
                        "ref_area", L(330f)),
                    "destroyable_parts", L(
                        L(
                            "nose", 33f, 33f, "critical", "engine",
                            "injure_anims", L(L(0.22f, "pdpanel7", "player_kestrel"))),
                        L("tail", 23f, 23f, "injure_anims", L(L(0.22f, "pdpanel8", "player_kestrel"))),
                        L(
                            "leftwing", 27f, 27f,
                            "injure_anims", L(
                                L(0.61f, "pdpanel3", "player_kestrel"),
                                L(0.35f, "pdpanel2", "player_kestrel"),
                                L(0.11f, "pdpanel5", "player_kestrel"))),
                        L(
                            "rightwing", 27f, 27f,
                            "injure_anims", L(
                                L(0.61f, "pdpanel4", "player_kestrel"),
                                L(0.35f, "pdpanel1", "player_kestrel"),
                                L(0.11f, "pdpanel6", "player_kestrel")))),
                    "collision", L(
                        L(0f, 0f, -4.03f),
                        L(0f, -0.11f, 4.14f),
                        L(7.48f, -0.22f, -0.23f),
                        L(0f, 0.88f, -0.23f),
                        L(-7.48f, -0.22f, -0.23f),
                        L(0f, -0.55f, 2.3f))),
                "probekestrel", L(
                    "title", L("MSG_PROBE_KESTREL"),
                    "mode", L("jet"),
                    "turrets", L(
                        "thirdp", L(
                            L("title", L("MSG_TUR_PROBE_REAR_G3"), "node", L("probe_kestrel_turret1")))),
                    "armor", L(66f),
                    "health", L(66f),
                    "dare_devil", L(5f),
                    "natural_touch", L(4f),
                    "sixth_sense", L(7f),
                    "dead_eye", L(5f),
                    "quick_draw", L(6f),
                    "steady_hand", L(6f),
                    "stun_recovery", L(5f),
                    "talker", L(6f),
                    "constitution", L(6f),
                    "weapons", L(
                        L("wep_probe_gun", 440f, 0.5f, 0f, 860f),
                        L("wep_probe_rocket", 4f, 4f, 175f, 1500f)),
                    "collision", L(L(0f, 0f, 0f)))));

    private static List<object?> Engines() =>
        L(
            L(1f, "probe_engine_lo", 0.65f),
            L(2f, "probe_engine", 0.85f),
            L(3f, "probe_engine_hi", 1f),
            L(10f, "probe_hawk_engine", 0.95f),
            L(11f, "probe_hawk_engine_mid", 1f),
            L(12f, "probe_hawk_engine_top", 1.1f),
            L(13f, "probe_peace_engine", 1f),
            L(14f, "probe_peace_engine_mid", 1.08f),
            L(15f, "probe_peace_engine_top", 1.16f),
            L(16f, "probe_fury_engine", 1.02f),
            L(17f, "probe_fury_engine_mid", 1.1f),
            L(18f, "probe_fury_engine_top", 1.18f),
            L(19f, "probe_avenger_engine", 1.1f),
            L(20f, "probe_avenger_engine_mid", 1.2f),
            L(21f, "probe_avenger_engine_top", 1.3f),
            L(28f, "probe_kestrel_engine", 1.06f),
            L(29f, "probe_kestrel_engine_mid", 1.14f),
            L(30f, "probe_kestrel_engine_top", 1.24f),
            L(31f, "probe_brand_engine", 0.9f),
            L(32f, "probe_brand_engine_mid", 0.95f),
            L(33f, "probe_brand_engine_top", 1.05f),
            L(34f, "probe_warhawk_engine", 1.15f),
            L(35f, "probe_warhawk_engine_mid", 1.25f),
            L(36f, "probe_warhawk_engine_top", 1.35f),
            L(37f, "probe_gyro_engine", 0.7f),
            L(38f, "probe_gyro_engine_mid", 0.78f),
            L(39f, "probe_gyro_engine_top", 0.86f));

    private static List<object?> Player() =>
        L(
            L(
                "nom_gravity", L(16f),
                "stall_mag", L(1.1f),
                "crash", L(
                    "bounce_factor", L(0.5f),
                    "armor_damage_range", L(10f, 150f),
                    "health_damage_range", L(10f, 150f)),
                "min_ai_active_dist", L(1800f),
                "sticky_bullet_catchup_rate", L(4f),
                "sticky_bullet_forget_interval", L(1.2f),
                "sticky_bullet_dist_factor", L(0.0001f),
                "sticky_bullet_inaccuracy", L(0.8f),
                "voiceover_volume_limiter", L(0.45f),
                "warning_shot_sound", L("probe_warning_sg"),
                "bullet_hit_sound", L("probe_ricochet_sg"),
                "rattle", L(
                    "sound", L("snd_probe_rattle"),
                    "speed_range", L(1.05f, 1.3f),
                    "volume_range", L(0f, 1f)),
                "score_kill", L(2f),
                "score_suicide", L(-3f),
                "smokescreen_stun_range", L(450f),
                "smokescreen_stun_angle", L(160f),
                "smokescreen_stun_interval", L(4f),
                "ai_skill_parameters", L(
                    "daredevil_chance", L(0.3f, 0.9f),
                    "sixth_sense_chance", L(0.4f, 0.8f),
                    "sixth_sense_factor", L(0.95f, 1.05f),
                    "dead_eye_angle", L(5f, 1f),
                    "quick_draw_angle", L(45f, 80f),
                    "quick_draw_chance", L(0.1f, 0.5f),
                    "steady_hand_chance", L(0.6f, 0.1f),
                    "stun_recovery_interval", L(4f, 1f),
                    "talker_chance", L(0.3f, 0.9f),
                    "constitution_chance", L(0.4f, 0.9f))));

    private static List<object?> Maneuvers() =>
        L(
            "probe_level_hold", L("natural_touch", L(1f), "steps", L(L(1f, 0f, 0f, 0f), L(2f, 0f, 0f, 0f))),
            "probe_relative_yaw", L(
                "autogyro_allowed", null,
                "nitro", L(1f),
                "natural_touch", L(0f),
                "relative", null,
                "steps", L(L(0f, 0f, 40f, 0f))),
            "probe_seven_element", L(
                "bias", L(-0.5f),
                "natural_touch", L(6f),
                "steps", L(L(4.5f, 0f, 0f, 0f, 0.5f, 0f, 1f), L(0f, -30f, 0f, 0f))),
            "probe_stub", L("natural_touch", L(2f)));

    private static List<object?> Weapons() =>
        L(
            L(
                "VERSION", L(2f),
                "NO_AMMO_WARNING", L("snd_probe_emptyclip"),
                "BALLISTICS", L(
                    "wep_probe_gun", L(
                        "DESC", L("MSG_PROBE_GUN"),
                        "NAME", L("probegun"),
                        "CALIBER", L(30f),
                        "CLUSTER_SIZE", L(600f),
                        "AMMO_LIMIT", L(1200f),
                        "FIRE_RATE", L(9f),
                        "VELOCITY", L(700f),
                        "RANGE", L(1000f),
                        "GRAVITY", L(0f),
                        "CANNON_SPREAD", L(4f),
                        "ARMOR_DAMAGE", L(2f),
                        "HEALTH_DAMAGE", L(2f),
                        "CANNON", null,
                        "LOOPED_SOUND_NAME", L("snd_probe_gunloop"),
                        "FIRE", L("SOUND", L("snd_probe_fire")),
                        "IMPACT", L("default", L("SOUND", L("snd_probe_hit")))),
                    "wep_probe_rocket", L(
                        "DESC", L("MSG_PROBE_ROCKET"),
                        "NAME", L("proberocket"),
                        "PRIORITY", L(5f),
                        "CLUSTER_SIZE", L(2f),
                        "AMMO_LIMIT", L(8f),
                        "ROCKET", null, "HIGH_EXPLOSIVE", null,
                        "FIRE_RATE", L(1f),
                        "VELOCITY", L(160f),
                        "ACCELERATION", L(80f),
                        "RANGE", L(2500f),
                        "TURN_RATE", L(0.001f),
                        "LOCK_ON", L(1.5f),
                        "ARMOR_DAMAGE", L(40f),
                        "HEALTH_DAMAGE", L(40f),
                        "IMPACT_PROXIMITY", L(20f),
                        "DETONATION_DISTANCE", L(8f),
                        "FIRE", L("SOUND", L("snd_probe_launch")),
                        "IMPACT", L("default", L("SOUND", L("snd_probe_boom")))),
                    "wep_probe_torpedo", L(
                        "DESC", L("MSG_PROBE_TORPEDO"),
                        "NAME", L("probetorp"),
                        "PRIORITY", L(4f),
                        "CLUSTER_SIZE", L(1f),
                        "ROCKET", null, "TORPEDO", null, "TARGETABLE", null,
                        "FLYOUT_HEALTH", L(12f),
                        "PROJECTILE_BBOX", L(0f),
                        "DESTROY_ANIMATION", L("probe_torpedo_shotdown"),
                        "FLYOUT", L("MODEL", L("probe_torpbody"), "MODEL_ANIMATION", L("probe_torpedo_trail")),
                        "FIRE_RATE", L(1f),
                        "VELOCITY", L(50f),
                        "RANGE", L(1100f),
                        "RANGE_MINIMUM", L(240f, 0f),
                        "TURN_RATE", L(0.002f),
                        "LOCK_ON", L(2f),
                        "ARMOR_DAMAGE", L(120f),
                        "HEALTH_DAMAGE", L(150f),
                        "IMPACT_PROXIMITY", L(36f),
                        "FIRE", L("SOUND", L("snd_probe_launch")),
                        "IMPACT", L(
                            "default", L(
                                "ANIMATION", L("torpedo_ground_effect"),
                                "SOUND", L("snd_probe_boom")))),
                    "wep_probe_he", L(
                        "DESC", L("MSG_PROBE_HE"),
                        "NAME", L("probehe"),
                        "PRIORITY", L(5f),
                        "CLUSTER_SIZE", L(3f),
                        "ROCKET", null, "HIGH_EXPLOSIVE", null,
                        "FIRE_RATE", L(1f),
                        "VELOCITY", L(900f),
                        "RANGE", L(1400f),
                        "LOCK_ON", L(1.2f),
                        "ARMOR_DAMAGE", L(50f),
                        "HEALTH_DAMAGE", L(70f),
                        "IMPACT_PROXIMITY", L(18f),
                        "DETONATION_DISTANCE", L(6f),
                        "FIRE", L("SOUND", L("snd_probe_launch")),
                        "IMPACT", L(
                            "default", L(
                                "SURFACE_ANIMATION", L("he_ground_effect"),
                                "SOUND", L("snd_probe_boom")))),
                    "wep_probe_choker", L(
                        "DESC", L("MSG_PROBE_CHOKER"),
                        "NAME", L("probechoke"),
                        "PRIORITY", L(4f),
                        "CLUSTER_SIZE", L(2f),
                        "ROCKET", null,
                        "TANGLER", L("TIME", L(1.6f), "RADIUS", L(30f), "ENGINE_DEAD", L(4f, 11f)),
                        "FIRE_RATE", L(1f),
                        "VELOCITY", L(1100f),
                        "RANGE", L(1500f),
                        "ARMOR_DAMAGE", L(0f),
                        "HEALTH_DAMAGE", L(0f),
                        "FIRE", L("SOUND", L("snd_probe_launch")),
                        "IMPACT", L(
                            "default", L("ANIMATION", L("scatter_effect"), "SOUND", L("snd_probe_boom")))),
                    "wep_probe_flare", L(
                        "DESC", L("MSG_PROBE_FLARE"),
                        "NAME", L("probeflare"),
                        "PRIORITY", L(4f),
                        "CLUSTER_SIZE", L(2f),
                        "ROCKET", null,
                        "FIRE_RATE", L(1f),
                        "VELOCITY", L(1.2f),
                        "DETONATION_TIME", L(1.5f),
                        "DAMAGE", L(0f),
                        "FIRE", L("SOUND", L("snd_probe_launch")),
                        "IMPACT", L(
                            "default", L("ANIMATION", L("rear_flash_effect"), "SOUND", L("snd_probe_boom")))),
                    "wep_probe_sonic", L(
                        "DESC", L("MSG_PROBE_SONIC"),
                        "NAME", L("probesonic"),
                        "PRIORITY", L(4f),
                        "CLUSTER_SIZE", L(2f),
                        "ROCKET", null, "SONIC", null,
                        "FIRE_RATE", L(1f),
                        "VELOCITY", L(650f),
                        "RANGE", L(1050f),
                        "LOCK_ON", L(1.4f),
                        "ARMOR_DAMAGE", L(0f),
                        "HEALTH_DAMAGE", L(0f),
                        "IMPACT_PROXIMITY", L(34f),
                        "DETONATION_DISTANCE", L(34f),
                        "FIRE", L("SOUND", L("snd_probe_launch")),
                        "IMPACT", L(
                            "default", L("ANIMATION", L("sonic_ground_effect"), "SOUND", L("snd_probe_boom")))),
                    "wep_probe_flash", L(
                        "DESC", L("MSG_PROBE_FLASH"),
                        "NAME", L("probeflash"),
                        "PRIORITY", L(4f),
                        "CLUSTER_SIZE", L(2f),
                        "ROCKET", null, "FLASH", null,
                        "FIRE_RATE", L(1f),
                        "VELOCITY", L(700f),
                        "RANGE", L(1000f),
                        "LOCK_ON", L(1.4f),
                        "DAMAGE", L(0f),
                        "IMPACT_PROXIMITY", L(380f),
                        "DETONATION_DISTANCE", L(20f),
                        "FIRE", L("SOUND", L("snd_probe_launch")),
                        "IMPACT", L(
                            "default", L("ANIMATION", L("flash_effect"), "SOUND", L("snd_probe_boom")))),
                    "wep_probe_seeker", L(
                        "DESC", L("MSG_PROBE_SEEKER"),
                        "NAME", L("probeseek"),
                        "PRIORITY", L(5f),
                        "CLUSTER_SIZE", L(2f),
                        "ROCKET", null, "BEEPER_SEEKER", null,
                        "FIRE_RATE", L(1f),
                        "VELOCITY", L(300f),
                        "RANGE", L(6000f),
                        "LOCK_ON", L(1.5f),
                        "TURN_RATE", L(1.1f),
                        "ARMOR_DAMAGE", L(30f),
                        "HEALTH_DAMAGE", L(45f),
                        "IMPACT_PROXIMITY", L(15f),
                        "FIRE", L("SOUND", L("snd_probe_launch")),
                        "IMPACT", L(
                            "default", L("ANIMATION", L("ballflare.flt"), "SOUND", L("snd_probe_boom")),
                            "player", L("ANIMATION", L("large_fireball"), "SOUND", L("snd_probe_boom")))),
                    "wep_probe_beeper", L(
                        "DESC", L("MSG_PROBE_BEEPER"),
                        "NAME", L("probebeep"),
                        "PRIORITY", L(4f),
                        "CLUSTER_SIZE", L(2f),
                        "ROCKET", null,
                        "BEEPER", L("TIME", L(14f)),
                        "FIRE_RATE", L(1f),
                        "VELOCITY", L(960f),
                        "RANGE", L(1200f),
                        "LOCK_ON", L(1.2f),
                        "ARMOR_DAMAGE", L(0f),
                        "HEALTH_DAMAGE", L(0f),
                        "DETONATION_DISTANCE", L(14f),
                        "FIRE", L("SOUND", L("snd_probe_launch")),
                        "IMPACT", L(
                            "default", null,
                            "player", L("ANIMATION", L("large_fireball"), "SOUND", L("snd_probe_boom")))),
                    "wep_probe_flak", L(
                        "DESC", L("MSG_PROBE_FLAK"),
                        "NAME", L("probeflak"),
                        "PRIORITY", L(4f),
                        "CLUSTER_SIZE", L(2f),
                        "ROCKET", null,
                        "FIRE_RATE", L(1f),
                        "VELOCITY", L(800f),
                        "RANGE", L(1200f),
                        "LOCK_ON", L(1.2f),
                        "ARMOR_DAMAGE", L(20f),
                        "HEALTH_DAMAGE", L(25f),
                        "IMPACT_PROXIMITY", L(45f),
                        "DETONATION_DISTANCE", L(40f),
                        "FIRE", L("SOUND", L("snd_probe_launch")),
                        "IMPACT", L(
                            "default", L("ANIMATION", L("flak_effect"), "SOUND", L("snd_probe_boom")),
                            "player", L("ANIMATION", L("probe_flak_player")))),
                    "wep_probe_incendiary", L(
                        "DESC", L("MSG_PROBE_INCENDIARY"),
                        "NAME", L("probeinc"),
                        "PRIORITY", L(4f),
                        "CLUSTER_SIZE", L(2f),
                        "AMMO_LIMIT", L(100f),
                        "ROCKET", null,
                        "FIRE_RATE", L(1f),
                        "VELOCITY", L(280f),
                        "ACCELERATION", L(100f),
                        "RANGE", L(620f),
                        "TURN_RATE", L(0.001f),
                        "LOCK_ON_LEAD", L(5f, 9f),
                        "ARMOR_DAMAGE", L(15f),
                        "HEALTH_DAMAGE", L(30f),
                        "IMPACT_PROXIMITY", L(12f),
                        "FIRE", L("SOUND", L("snd_probe_launch")),
                        "IMPACT", L(
                            "default", L("ANIMATION", L("small_fireball"), "SOUND", L("snd_probe_boom")))),
                    "wep_probe_smoker", L(
                        "DESC", L("MSG_PROBE_SMOKER"),
                        "NAME", L("probesmoke"),
                        "PRIORITY", L(4f),
                        "CLUSTER_SIZE", L(2f),
                        "ROCKET", null, "REAR", null,
                        "SMOKE_SCREEN", L("TIME", L(6.5f)),
                        "FIRE_RATE", L(1f),
                        "VELOCITY", L(1f),
                        "DAMAGE", L(0f),
                        "FIRE", L("SOUND", L("snd_probe_launch"))),
                    "wep_probe_blastrocket", L(
                        "DESC", L("MSG_PROBE_BLAST_ROCKET"),
                        "NAME", L("probeblast"),
                        "PRIORITY", L(4f),
                        "CLUSTER_SIZE", L(2f),
                        "AMMO_LIMIT", L(6f),
                        "ROCKET", null, "HIGH_EXPLOSIVE", null,
                        "FIRE_RATE", L(1f),
                        "VELOCITY", L(620f),
                        "RANGE", L(2200f),
                        "ARMOR_DAMAGE", L(30f),
                        "HEALTH_DAMAGE", L(30f),
                        "IMPACT_PROXIMITY", L(66f),
                        "DETONATION_DISTANCE", L(11f),
                        "FIRE", L("SOUND", L("snd_probe_launch")),
                        "IMPACT", L("default", L("SOUND", L("snd_probe_boom")))),
                    "wep_probe_gunhit", L(
                        "DESC", L("MSG_PROBE_GUNHIT"),
                        "NAME", L("probestrike"),
                        "CALIBER", L(30f),
                        "CLUSTER_SIZE", L(300f),
                        "AMMO_LIMIT", L(600f),
                        "FIRE_RATE", L(6f),
                        "VELOCITY", L(650f),
                        "RANGE", L(900f),
                        "GRAVITY", L(0f),
                        "CANNON_SPREAD", L(2f),
                        "ARMOR_DAMAGE", L(3f),
                        "HEALTH_DAMAGE", L(3f),
                        "CANNON", null,
                        "FIRE", L("SOUND", L("snd_probe_fire")),
                        "IMPACT", L(
                            "default", L("ANIMATION", L("3040slug_gunhit"), "SOUND", L("snd_probe_hit")),
                            "player", L("ANIMATION", L("3040slug_gunhit"), "SOUND", L("snd_probe_hit")))),
                    "wep_30", L(
                        "DESC", L("MSG_PROBE_CAL30"),
                        "NAME", L("probecal30"),
                        "CALIBER", L(30f),
                        "CLUSTER_SIZE", L(500f),
                        "AMMO_LIMIT", L(1000f),
                        "FIRE_RATE", L(10f),
                        "VELOCITY", L(720f),
                        "RANGE", L(950f),
                        "GRAVITY", L(0f),
                        "CANNON_SPREAD", L(4f),
                        "ARMOR_DAMAGE", L(1.5f),
                        "HEALTH_DAMAGE", L(1.5f),
                        "CANNON", null,
                        "LOOPED_SOUND_NAME", L("snd_probe_gunloop"),
                        "FIRE", L("SOUND", L("snd_probe_fire")),
                        "IMPACT", L("default", L("SOUND", L("snd_probe_hit")))),
                    "wep_40", L(
                        "DESC", L("MSG_PROBE_CAL40"),
                        "NAME", L("probecal40"),
                        "CALIBER", L(40f),
                        "CLUSTER_SIZE", L(400f),
                        "AMMO_LIMIT", L(800f),
                        "FIRE_RATE", L(8f),
                        "VELOCITY", L(690f),
                        "RANGE", L(1000f),
                        "GRAVITY", L(0f),
                        "CANNON_SPREAD", L(4f),
                        "ARMOR_DAMAGE", L(2.5f),
                        "HEALTH_DAMAGE", L(2.5f),
                        "CANNON", null,
                        "LOOPED_SOUND_NAME", L("snd_probe_gunloop"),
                        "FIRE", L("SOUND", L("snd_probe_fire")),
                        "IMPACT", L("default", L("SOUND", L("snd_probe_hit")))),
                    "wep_50", L(
                        "DESC", L("MSG_PROBE_CAL50"),
                        "NAME", L("probecal50"),
                        "CALIBER", L(50f),
                        "CLUSTER_SIZE", L(300f),
                        "AMMO_LIMIT", L(600f),
                        "FIRE_RATE", L(7f),
                        "VELOCITY", L(660f),
                        "RANGE", L(1050f),
                        "GRAVITY", L(0f),
                        "CANNON_SPREAD", L(5f),
                        "ARMOR_DAMAGE", L(3.5f),
                        "HEALTH_DAMAGE", L(3.5f),
                        "CANNON", null,
                        "LOOPED_SOUND_NAME", L("snd_probe_gunloop"),
                        "FIRE", L("SOUND", L("snd_probe_fire")),
                        "IMPACT", L("default", L("SOUND", L("snd_probe_hit")))),
                    "wep_60", L(
                        "DESC", L("MSG_PROBE_CAL60"),
                        "NAME", L("probecal60"),
                        "CALIBER", L(60f),
                        "CLUSTER_SIZE", L(240f),
                        "AMMO_LIMIT", L(480f),
                        "FIRE_RATE", L(6f),
                        "VELOCITY", L(630f),
                        "RANGE", L(1100f),
                        "GRAVITY", L(0f),
                        "CANNON_SPREAD", L(5f),
                        "ARMOR_DAMAGE", L(4.5f),
                        "HEALTH_DAMAGE", L(4.5f),
                        "CANNON", null,
                        "LOOPED_SOUND_NAME", L("snd_probe_gunloop"),
                        "FIRE", L("SOUND", L("snd_probe_fire")),
                        "IMPACT", L("default", L("SOUND", L("snd_probe_hit")))),
                    "wep_70", L(
                        "DESC", L("MSG_PROBE_CAL70"),
                        "NAME", L("probecal70"),
                        "CALIBER", L(70f),
                        "CLUSTER_SIZE", L(180f),
                        "AMMO_LIMIT", L(360f),
                        "FIRE_RATE", L(5f),
                        "VELOCITY", L(600f),
                        "RANGE", L(1150f),
                        "GRAVITY", L(0f),
                        "CANNON_SPREAD", L(6f),
                        "ARMOR_DAMAGE", L(6f),
                        "HEALTH_DAMAGE", L(6f),
                        "CANNON", null,
                        "LOOPED_SOUND_NAME", L("snd_probe_gunloop"),
                        "FIRE", L("SOUND", L("snd_probe_fire")),
                        "IMPACT", L("default", L("SOUND", L("snd_probe_hit")))),
                    "wep_06", L(
                        "DESC", L("MSG_PROBE_PYLON_HE"),
                        "NAME", L("probepylonhe"),
                        "PRIORITY", L(5f),
                        "CLUSTER_SIZE", L(2f),
                        "AMMO_LIMIT", L(4f),
                        "ROCKET", null, "HIGH_EXPLOSIVE", null,
                        "FIRE_RATE", L(1f),
                        "VELOCITY", L(180f),
                        "ACCELERATION", L(70f),
                        "RANGE", L(2200f),
                        "TURN_RATE", L(0.001f),
                        "LOCK_ON", L(1.4f),
                        "ARMOR_DAMAGE", L(35f),
                        "HEALTH_DAMAGE", L(45f),
                        "IMPACT_PROXIMITY", L(18f),
                        "DETONATION_DISTANCE", L(7f),
                        "FIRE", L("SOUND", L("snd_probe_launch")),
                        "IMPACT", L("default", L("SOUND", L("snd_probe_boom")))),
                    "wep_probe_zeptorp", L(
                        "DESC", L("MSG_PROBE_ZEPTORP"),
                        "NAME", L("probezeptorp"),
                        "PRIORITY", L(4f),
                        "CLUSTER_SIZE", L(1f),
                        "ROCKET", null, "TORPEDO", null, "DAMAGES_ZEPPELIN", null,
                        "FIRE_RATE", L(1f),
                        "VELOCITY", L(70f),
                        "RANGE", L(1300f),
                        "RANGE_MINIMUM", L(260f, 0f),
                        "TURN_RATE", L(0.002f),
                        "LOCK_ON", L(1.5f),
                        "ARMOR_DAMAGE", L(140f),
                        "HEALTH_DAMAGE", L(170f),
                        "IMPACT_PROXIMITY", L(30f),
                        "FIRE", L("SOUND", L("snd_probe_launch")),
                        "IMPACT", L(
                            "default", L(
                                "ANIMATION", L("torpedo_ground_effect"),
                                "SOUND", L("snd_probe_boom")))))));

    private static List<object?> Shakes() =>
        L(
            "fire_bullet", L(
                "frequency", L(12f),
                "damp", L(10f),
                "sawtooth", L(1f),
                "magnitude_factor", L(0.0001f)),
            "bullet_impact", L(
                "frequency", L(9f),
                "damp", L(8f),
                "sawtooth", L(0f),
                "magnitude_factor", L(0.002f)),
            "missile_impact", L(
                "frequency", L(3f),
                "damp", L(5f),
                "sawtooth", L(0f),
                "magnitude_factor", L(0.0008f),
                "he_factor", L(1.5f)),
            "explosion", L(
                "frequency", L(2.5f),
                "damp", L(4f),
                "sawtooth", L(0f),
                "magnitude_factor", L(0.0005f)),
            "high_speed", L(
                "frequency", L(12f),
                "damp", L(10f),
                "sawtooth", L(1f),
                "min_speed", L(1.15f),
                "magnitude_quotient", L(90f)),
            "nitro", L("frequency", L(3f), "damp", L(4f), "sawtooth", L(1f), "magnitude", L(0.03f)));

    private static List<object?> Ai() =>
        L(
            L(
                "TURRET", L(
                    L(
                        "TITLE", L("MSG_TUR_PROBE_REAR_G3"),
                        "CREATE_STANDALONE", L(0f),
                        "ACTIVATED", L(1f),
                        "HEALTHY_NODE", L("probe_kestrel_turret1"),
                        "PARTS", L("pturret", "pgun", "pfirepoint"),
                        "WEAPON", L(
                            "NAME", L("wep_probe_gun"),
                            "AMMO", L(7000f),
                            "DETECTION_RANGE", L(520f),
                            "FIRE_RATE", L(0.35f)),
                        "INACCURACY", L(4.5f),
                        "ATTACK_INTERVAL", L(3.5f),
                        "BORED_INTERVAL", L(2.5f),
                        "PITCH", L(15f, 65f),
                        "YAW", L(125f, 255f),
                        "SOUNDS", L("CANNON", L("snd_probe_gunloop"))),
                    L(
                        "TITLE", L("MSG_TUR_PROBE_DORSAL_G3"),
                        "CREATE_STANDALONE", L(0f),
                        "ACTIVATED", L(1f),
                        "HEALTHY_NODE", L("probe_brand_turret1"),
                        "PARTS", L("pturret", "pgun", "pfirepoint"),
                        "WEAPON", L(
                            "NAME", L("wep_probe_gun"),
                            "AMMO", L(7000f),
                            "DETECTION_RANGE", L(460f),
                            "FIRE_RATE", L(0.3f)),
                        "INACCURACY", L(5f),
                        "ATTACK_INTERVAL", L(4.5f),
                        "BORED_INTERVAL", L(2f),
                        "PITCH", L(-20f, 70f),
                        "YAW", L(0f, 0f),
                        "SOUNDS", L("CANNON", L("snd_probe_gunloop"))),
                    L(
                        "TITLE", L("MSG_TUR_PROBE_AA"),
                        "ACTIVATED", L(0f),
                        "NODES", L(L("aagun**")),
                        "PARTS", L("turret", "gun", "firepoint"),
                        "WEAPON", L(
                            "NAME", L("wep_probe_gun"),
                            "AMMO", L(7000f),
                            "DETECTION_RANGE", L(780f),
                            "FIRE_RATE", L(0.8f, 1.6f)),
                        "INACCURACY", L(9f),
                        "ATTACK_INTERVAL", L(3f, 6f),
                        "BORED_INTERVAL", L(2f, 4f),
                        "PITCH", L(5f, 80f),
                        "YAW", L(0f, 0f),
                        "SOUNDS", L("CANNON", L("snd_probe_gunloop"))))));

    private static List<object?> ProbeWorld() =>
        L(
            L(
                "ANIMATION_DEFINITIONS", L(
                    "ANIMATION_LIST", L(
                        "ANIMATION_DEFINITION", L(
                            "NAME", L("probe_shed"),
                            "ACTIVATION", L("WEAPON_HIT"),
                            "HEALTH", L(60f),
                            "PERSIST_LOG", L("ON"),
                            "RESET_STATE", L(
                                "OBJECT_ACTIVE_STATE", L("NAME", L("healthy"), "STATE", L("ACTIVE")),
                                "OBJECT_ACTIVE_STATE", L("NAME", L("destroyed"), "STATE", L("INACTIVE"))),
                            "SEQUENCE_DEFINITION", L(
                                "NAME", L("probe_shed_down"),
                                "OBJECT_ACTIVE_STATE", L("NAME", L("healthy"), "STATE", L("INACTIVE")),
                                "OBJECT_ACTIVE_STATE", L("NAME", L("destroyed"), "STATE", L("ACTIVE")))),
                        "ANIMATION_DEFINITION", L(
                            "NAME", L("probe_tank"),
                            "ACTIVATION", L("WEAPON_OR_COLLIDE_HIT"),
                            "HEALTH", L(90f),
                            "PERSIST_LOG", L("ON"),
                            "RESET_STATE", L(
                                "OBJECT_ACTIVE_STATE", L("NAME", L("healthy"), "STATE", L("ACTIVE")),
                                "OBJECT_ACTIVE_STATE", L("NAME", L("destroyed"), "STATE", L("INACTIVE"))),
                            "SEQUENCE_DEFINITION", L(
                                "NAME", L("probe_tank_down"),
                                "OBJECT_ACTIVE_STATE", L("NAME", L("healthy"), "STATE", L("INACTIVE")),
                                "OBJECT_ACTIVE_STATE", L("NAME", L("destroyed"), "STATE", L("ACTIVE"))),
                            "DAMAGE_SEQUENCE", L(
                                "IF", L("ANIM_HEALTH", L(60f)),
                                "OBJECT_ROTATE_STATE", L("NAME", L("healthy"), "STATE", L(0f, 0f, 4f)),
                                "ENDIF"))))));

    private static List<object?> FlameBall() =>
        L(
            L(
                "ANIMATION_DEFINITIONS", L(
                    "ANIMATION_LIST", L(
                        "ANIMATION_DEFINITION", L(
                            "NAME", L("probe_flame_root"),
                            "ANIMATION_NAME", L("probe_flame_ball"),
                            "ACTIVATION", L("ON_CALL"),
                            "SEQUENCE_DEFINITION", L(
                                "NAME", L("probe_flame_burst"),
                                "PUFFER_STATE", L(
                                    "NAME", L("fierypuffer"),
                                    "ACTIVE_STATE", L("ACTIVE"),
                                    "AT_NODE", L("probe_flame_root"),
                                    "NUMBER", L(5f),
                                    "TIME_INTERVAL", L(0.15f),
                                    "SIZE_RANGE", L(1.5f, 3.5f),
                                    "LIFETIME_RANGE", L(0.4f, 0.75f),
                                    "GROWTH_FACTOR", L(2f),
                                    "MIN_RANDOM_VELOCITY", L(-2f, 1f, -2f),
                                    "MAX_RANDOM_VELOCITY", L(2f, 4f, 2f),
                                    "TEXTURE_SEQUENCE", L(
                                        L(0f, "smoke101"),
                                        L(0.4f, "smoke102"),
                                        L(0.8f, "smoke103"))),
                                "PUFFER_STATE", L("NAME", L("fierypuffer"), "ACTIVE_STATE", L("INACTIVE"))))))));

    private static List<object?> PuffTrails() =>
        L(
            L(
                "ANIMATION_DEFINITIONS", L(
                    "ANIMATION_LIST", L(
                        "ANIMATION_DEFINITION", L(
                            "NAME", L("probe_trail_root"),
                            "ANIMATION_NAME", L("probe_dense_trail"),
                            "ACTIVATION", L("ON_CALL"),
                            "SEQUENCE_DEFINITION", L(
                                "NAME", L("probe_trail_on"),
                                "PUFFER_STATE", L(
                                    "NAME", L("smokepuffer"),
                                    "ACTIVE_STATE", L("ACTIVE"),
                                    "AT_NODE", L("probe_trail_root"),
                                    "DISTANCE_INTERVAL", L(3f),
                                    "SIZE_RANGE", L(1f, 2.5f),
                                    "LIFETIME_RANGE", L(2f, 3.5f),
                                    "GROWTH_FACTOR", L(3f),
                                    "DEVIATION_DISTANCE", L(0.6f),
                                    "TEXTURES", L("smoke101", "smoke102", "smoke103"),
                                    "COLORS", L(
                                        L(0f, 200f, 150f, 110f, 0.8f),
                                        L(0.5f, 90f, 84f, 80f, 0.6f),
                                        L(1f, 40f, 40f, 40f, 0f))),
                                "PUFFER_STATE", L(
                                    "NAME", L("firepuffer"),
                                    "ACTIVE_STATE", L("ACTIVE"),
                                    "AT_NODE", L("probe_trail_root"),
                                    "DISTANCE_INTERVAL", L(0.8f),
                                    "SIZE_RANGE", L(0.6f, 1.4f),
                                    "LIFETIME_RANGE", L(0.3f, 0.6f),
                                    "DEVIATION_DISTANCE", L(0.3f),
                                    "TEXTURES", L("smoke103"))))))));

    private static List<object?> CampaignSequence() =>
        L(
            L(
                L(
                    "seq", L(0f),
                    "desc", L("Probe Sortie"),
                    "campaign", L(1f),
                    "mission", L(9f),
                    "area", L("probe"),
                    "wingman", L("false"))));

    private static List<object?> Briefing() =>
        L(
            L(
                "BRIEFINGDIALOG", L("PRIMITIVES", L()),
                "default", L("BACKGROUND_IMAGES", L(L("probe_loading", 0f, 0f))),
                "brief_c19", L(
                    "IMAGE_PATH", L("..\\data\\c1\\images"),
                    "BACKGROUND_IMAGES", L(L("probe_map", 0f, 0f)),
                    "SCRIPT", L(
                        "PlaySound", L(
                            "sound", L("snd_probe_briefing"),
                            "vol", L(1f),
                            "markers", L("true"),
                            "startmarker", L(0f)),
                        "Wait", L(2f),
                        "Pict", L(
                            "PROBEPIN1",
                            "bitmap", L("probe_pin"),
                            "at", L(400f, 300f),
                            "center", L("true")),
                        "Fade", L("PROBEPIN1", "start", L(0f), "end", L(1f), "duration", L(0.5f)),
                        "On", L("PROBEPIN1"),
                        "Wait", L(12f),
                        "Objective", L("PROBETEXT1", "index", L(0f)),
                        "On", L("PROBETEXT1")))));

    private static List<object?> Sounds() =>
        L(
            L(
                "SETS", L(
                    "probe_flight", L(
                        L(
                            "snd_probe_engine", "probe_engine.wav", "LOOPED", "3D", "FREQUENCY", "SFX",
                            "RANGE", L(120f, 900f)),
                        L(
                            "snd_probe_fighter_engine", "probe_fighter_engine.wav", "LOOPED", "3D",
                            "FREQUENCY", "SFX",
                            "RANGE", L(100f, 800f)),
                        L(
                            "snd_probe_brand_engine", "probe_brand_engine.wav", "LOOPED", "3D", "FREQUENCY",
                            "SFX",
                            "RANGE", L(110f, 850f)),
                        L(
                            "snd_probe_avenger_engine", "probe_avenger_engine.wav", "LOOPED", "3D",
                            "FREQUENCY", "SFX",
                            "RANGE", L(140f, 1000f)),
                        L(
                            "snd_probe_hawk_engine", "probe_hawk_engine.wav", "LOOPED", "3D", "FREQUENCY",
                            "SFX",
                            "RANGE", L(105f, 820f)),
                        L(
                            "snd_probe_gyro_engine", "probe_gyro_engine.wav", "LOOPED", "3D", "FREQUENCY",
                            "SFX",
                            "RANGE", L(80f, 700f)),
                        L(
                            "snd_probe_fury_engine", "probe_fury_engine.wav", "LOOPED", "3D", "FREQUENCY",
                            "SFX",
                            "RANGE", L(115f, 880f)),
                        L(
                            "snd_probe_peace_engine", "probe_peace_engine.wav", "LOOPED", "3D", "FREQUENCY",
                            "SFX",
                            "RANGE", L(125f, 920f)),
                        L(
                            "snd_probe_warhawk_engine", "probe_warhawk_engine.wav", "LOOPED", "3D",
                            "FREQUENCY", "SFX",
                            "RANGE", L(150f, 1050f)),
                        L(
                            "snd_probe_kestrel_engine", "probe_kestrel_engine.wav", "LOOPED", "3D",
                            "FREQUENCY", "SFX",
                            "RANGE", L(135f, 960f)),
                        L("snd_probe_engine_cp", "probe_engine_cp.wav", "LOOPED", "SFX", "VOLUME", L(0.8f)),
                        L(
                            "snd_probe_engine_damaged", "probe_engine_damaged.wav", "LOOPED", "3D", "SFX",
                            "RANGE", L(100f, 500f)),
                        L("snd_probe_rattle", "probe_rattle.wav", "LOOPED", "SFX", "VOLUME", L(0.6f)),
                        L("snd_propstart", "probe_propstart.wav", "3D", "SFX", "RANGE", L(40f, 400f))),
                    "probe_weapons", L(
                        L(
                            "snd_probe_gunloop", "probe_gunloop.wav", "LOOPED", "3D", "SFX",
                            "RANGE", L(25f, 180f)),
                        L("snd_probe_emptyclip", "probe_emptyclip.wav", "3D", "SFX", "RANGE", L(60f, 600f)),
                        L("snd_probe_fire", "probe_fire.wav", "3D", "SFX", "RANGE", L(30f, 300f)),
                        L("snd_probe_launch", "probe_launch.wav", "3D", "SFX", "RANGE", L(50f, 600f)),
                        L("snd_probe_hit", "probe_hit.wav", "3D", "SFX", "RANGE", L(20f, 250f)),
                        L(
                            "snd_probe_boom", "probe_boom.wav", "3D", "SFX",
                            "RANGE", L(150f, 1500f),
                            "VOLUME", L(0.9f)),
                        L("snd_probe_pass1", "probe_pass1.wav", "SFX"),
                        L("snd_probe_pass2", "probe_pass2.wav", "SFX"),
                        L("snd_probe_ricochet1", "probe_ricochet1.wav", "3D", "SFX", "RANGE", L(10f, 120f)),
                        L("snd_probe_ricochet2", "probe_ricochet2.wav", "3D", "SFX", "RANGE", L(10f, 120f)),
                        L("snd_probe_windowhit1", "probe_windowhit1.wav", "SFX"),
                        L("snd_probe_windowhit2", "probe_windowhit2.wav", "SFX")),
                    "probe_radio", L(
                        L("snd_probe_radio_line", "probe_radio_line.wav", "QUEUE", L(45f)),
                        L("snd_probe_radio_bark", "probe_radio_bark.wav", "QUEUE", L(0.5f)),
                        L("snd_probe_briefing", "probe_briefing.wav", "SFX")),
                    "probe_voice", L(
                        L("snd_id2_WA-Turret-A", "probe_vo2_WA-Turret-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_WA-HighDmg-A", "probe_vo2_WA-HighDmg-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_WA-Attack-A", "probe_vo2_WA-Attack-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_PR-DngrZn-A", "probe_vo2_PR-DngrZn-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L(
                            "snd_id2_PR-EnemyDwn-A", "probe_vo2_PR-EnemyDwn-A.wav", "PURGEABLE",
                            "QUEUE", L(0.5f)),
                        L("snd_id2_DI-LowDmg-A", "probe_vo2_DI-LowDmg-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_DI-MedDmg-A", "probe_vo2_DI-MedDmg-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_DI-HighDmg-A", "probe_vo2_DI-HighDmg-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_DA-Bail-A", "probe_vo2_DA-Bail-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_DA-NoBail-A", "probe_vo2_DA-NoBail-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_DE-Bail-A", "probe_vo2_DE-Bail-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_DE-NoBail-A", "probe_vo2_DE-NoBail-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_GL-AllyDwn-A", "probe_vo2_GL-AllyDwn-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L(
                            "snd_id2_GL-EnemyDwn-A", "probe_vo2_GL-EnemyDwn-A.wav", "PURGEABLE",
                            "QUEUE", L(0.5f)),
                        L("snd_id2_GL-PlyrDwn-A", "probe_vo2_GL-PlyrDwn-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L(
                            "snd_id2_TA-FailTail-A", "probe_vo2_TA-FailTail-A.wav", "PURGEABLE",
                            "QUEUE", L(0.5f)),
                        L("snd_id2_TA-FailShk-A", "probe_vo2_TA-FailShk-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_TA-SucShk-A", "probe_vo2_TA-SucShk-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_DS-Ally-A", "probe_vo2_DS-Ally-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L(
                            "snd_id2_DI-LowDmg-B", "probe_vo2_DI-LowDmg-B.wav", "PURGEABLE",
                            "QUEUE", L(0.5f),
                            "OPTIONAL"),
                        L("snd_id2_WA-Enemy-12L", "probe_vo2_WA-Enemy-12L.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_WA-Enemy-12", "probe_vo2_WA-Enemy-12.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_WA-Enemy-12H", "probe_vo2_WA-Enemy-12H.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_WA-Enemy-3L", "probe_vo2_WA-Enemy-3L.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_WA-Enemy-3", "probe_vo2_WA-Enemy-3.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_WA-Enemy-3H", "probe_vo2_WA-Enemy-3H.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_WA-Enemy-6L", "probe_vo2_WA-Enemy-6L.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_WA-Enemy-6", "probe_vo2_WA-Enemy-6.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_WA-Enemy-6H", "probe_vo2_WA-Enemy-6H.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_WA-Enemy-9L", "probe_vo2_WA-Enemy-9L.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_WA-Enemy-9", "probe_vo2_WA-Enemy-9.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id2_WA-Enemy-9H", "probe_vo2_WA-Enemy-9H.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id26_WA-Turret-A", "probe_vo26_WA-Turret-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L(
                            "snd_id26_WA-HighDmg-A", "probe_vo26_WA-HighDmg-A.wav", "PURGEABLE",
                            "QUEUE", L(0.5f)),
                        L("snd_id26_WA-Attack-A", "probe_vo26_WA-Attack-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id26_PR-DngrZn-A", "probe_vo26_PR-DngrZn-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L(
                            "snd_id26_PR-EnemyDwn-A", "probe_vo26_PR-EnemyDwn-A.wav", "PURGEABLE",
                            "QUEUE", L(0.5f)),
                        L("snd_id26_DI-LowDmg-A", "probe_vo26_DI-LowDmg-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id26_DI-MedDmg-A", "probe_vo26_DI-MedDmg-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L(
                            "snd_id26_DI-HighDmg-A", "probe_vo26_DI-HighDmg-A.wav", "PURGEABLE",
                            "QUEUE", L(0.5f)),
                        L("snd_id26_DA-Bail-A", "probe_vo26_DA-Bail-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id26_DA-NoBail-A", "probe_vo26_DA-NoBail-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id26_DE-Bail-A", "probe_vo26_DE-Bail-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id26_DE-NoBail-A", "probe_vo26_DE-NoBail-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L(
                            "snd_id26_GL-AllyDwn-A", "probe_vo26_GL-AllyDwn-A.wav", "PURGEABLE",
                            "QUEUE", L(0.5f)),
                        L(
                            "snd_id26_GL-EnemyDwn-A", "probe_vo26_GL-EnemyDwn-A.wav", "PURGEABLE",
                            "QUEUE", L(0.5f)),
                        L(
                            "snd_id26_GL-PlyrDwn-A", "probe_vo26_GL-PlyrDwn-A.wav", "PURGEABLE",
                            "QUEUE", L(0.5f)),
                        L(
                            "snd_id26_TA-FailTail-A", "probe_vo26_TA-FailTail-A.wav", "PURGEABLE",
                            "QUEUE", L(0.5f)),
                        L(
                            "snd_id26_TA-FailShk-A", "probe_vo26_TA-FailShk-A.wav", "PURGEABLE",
                            "QUEUE", L(0.5f)),
                        L("snd_id26_TA-SucShk-A", "probe_vo26_TA-SucShk-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L("snd_id26_DS-Ally-A", "probe_vo26_DS-Ally-A.wav", "PURGEABLE", "QUEUE", L(0.5f)),
                        L(
                            "snd_id26_DI-LowDmg-B", "probe_vo26_DI-LowDmg-B.wav", "PURGEABLE",
                            "QUEUE", L(0.5f),
                            "OPTIONAL")),
                    "probe_music", L(
                        L("snd_music_splash", "music_splash_probe.wav", "MUSIC"),
                        L("snd_probe_music_prebattle1", "music_prebattle_probe1.wav", "MUSIC"),
                        L("snd_probe_music_prebattle2", "music_prebattle_probe2.wav", "MUSIC"),
                        L("snd_probe_music_battle1", "music_battle_probe1.wav", "MUSIC", "LOOPED"),
                        L("snd_probe_music_battle2", "music_battle_probe2.wav", "MUSIC", "LOOPED"),
                        L("snd_probe_music_primaryobj1", "music_primaryobj_probe1.wav", "MUSIC"),
                        L("snd_probe_music_primaryobj2", "music_primaryobj_probe2.wav", "MUSIC"),
                        L("snd_probe_music_secondaryobj1", "music_secondaryobj_probe1.wav", "MUSIC"),
                        L("snd_probe_music_secondaryobj2", "music_secondaryobj_probe2.wav", "MUSIC"),
                        L("snd_probe_music_tertiaryobj1", "music_tertiaryobj_probe1.wav", "MUSIC"),
                        L("snd_probe_music_tertiaryobj2", "music_tertiaryobj_probe2.wav", "MUSIC"),
                        L("snd_probe_music_battlesuccess", "music_battlesuccess_probe.wav", "MUSIC"),
                        L("snd_probe_music_missionsuccess", "music_missionsuccess_probe.wav", "MUSIC"))),
                "SOUND_GROUPS", L(
                    L("probe_warning_sg", "DYNAMIC_WEIGHTS", 0.5f, L("snd_probe_pass1"), L("snd_probe_pass2")),
                    L(
                        "probe_ricochet_sg", "DYNAMIC_WEIGHTS", 0.5f,
                        L("snd_probe_ricochet1"),
                        L("snd_probe_ricochet2")),
                    L(
                        "window_hit_sg", "DYNAMIC_WEIGHTS", 0.5f,
                        L("snd_probe_windowhit1"),
                        L("snd_probe_windowhit2")),
                    L("snd_WA-Turret-A_id2_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id2_WA-Turret-A")),
                    L("snd_WA-HighDmg-A_id2_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id2_WA-HighDmg-A")),
                    L("snd_WA-Attack-A_id2_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id2_WA-Attack-A")),
                    L("snd_PR-DngrZn-A_id2_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id2_PR-DngrZn-A")),
                    L("snd_PR-EnemyDwn-A_id2_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id2_PR-EnemyDwn-A")),
                    L(
                        "snd_DI-LowDmg-A_id2_random", "DYNAMIC_WEIGHTS", 0.5f,
                        L("snd_id2_DI-LowDmg-A"),
                        L("snd_id2_DI-LowDmg-B")),
                    L("snd_DI-MedDmg-A_id2_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id2_DI-MedDmg-A")),
                    L("snd_DI-HighDmg-A_id2_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id2_DI-HighDmg-A")),
                    L("snd_DA-Bail-A_id2_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id2_DA-Bail-A")),
                    L("snd_DA-NoBail-A_id2_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id2_DA-NoBail-A")),
                    L("snd_DE-Bail-A_id2_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id2_DE-Bail-A")),
                    L("snd_DE-NoBail-A_id2_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id2_DE-NoBail-A")),
                    L("snd_GL-AllyDwn-A_id2_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id2_GL-AllyDwn-A")),
                    L("snd_GL-EnemyDwn-A_id2_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id2_GL-EnemyDwn-A")),
                    L("snd_GL-PlyrDwn-A_id2_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id2_GL-PlyrDwn-A")),
                    L("snd_TA-FailTail-A_id2_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id2_TA-FailTail-A")),
                    L("snd_TA-FailShk-A_id2_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id2_TA-FailShk-A")),
                    L("snd_TA-SucShk-A_id2_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id2_TA-SucShk-A")),
                    L("snd_DS-Ally-A_id2_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id2_DS-Ally-A")),
                    L("snd_WA-Turret-A_id26_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id26_WA-Turret-A")),
                    L("snd_WA-HighDmg-A_id26_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id26_WA-HighDmg-A")),
                    L("snd_WA-Attack-A_id26_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id26_WA-Attack-A")),
                    L("snd_PR-DngrZn-A_id26_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id26_PR-DngrZn-A")),
                    L("snd_PR-EnemyDwn-A_id26_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id26_PR-EnemyDwn-A")),
                    L(
                        "snd_DI-LowDmg-A_id26_random", "DYNAMIC_WEIGHTS", 0.5f,
                        L("snd_id26_DI-LowDmg-A"),
                        L("snd_id26_DI-LowDmg-B")),
                    L("snd_DI-MedDmg-A_id26_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id26_DI-MedDmg-A")),
                    L("snd_DI-HighDmg-A_id26_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id26_DI-HighDmg-A")),
                    L("snd_DA-Bail-A_id26_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id26_DA-Bail-A")),
                    L("snd_DA-NoBail-A_id26_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id26_DA-NoBail-A")),
                    L("snd_DE-Bail-A_id26_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id26_DE-Bail-A")),
                    L("snd_DE-NoBail-A_id26_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id26_DE-NoBail-A")),
                    L("snd_GL-AllyDwn-A_id26_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id26_GL-AllyDwn-A")),
                    L("snd_GL-EnemyDwn-A_id26_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id26_GL-EnemyDwn-A")),
                    L("snd_GL-PlyrDwn-A_id26_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id26_GL-PlyrDwn-A")),
                    L("snd_TA-FailTail-A_id26_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id26_TA-FailTail-A")),
                    L("snd_TA-FailShk-A_id26_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id26_TA-FailShk-A")),
                    L("snd_TA-SucShk-A_id26_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id26_TA-SucShk-A")),
                    L("snd_DS-Ally-A_id26_random", "DYNAMIC_WEIGHTS", 0.5f, L("snd_id26_DS-Ally-A")),
                    L(
                        "music_prebattle_sg", "MUSIC", "DYNAMIC_WEIGHTS", 0.5f,
                        L("snd_probe_music_prebattle1"),
                        L("snd_probe_music_prebattle2")),
                    L(
                        "music_battle_sg", "MUSIC", "DYNAMIC_WEIGHTS", 0.5f,
                        L("snd_probe_music_battle1"),
                        L("snd_probe_music_battle2")),
                    L(
                        "music_primaryobj_sg", "MUSIC", "DYNAMIC_WEIGHTS", 0.5f,
                        L("snd_probe_music_primaryobj1"),
                        L("snd_probe_music_primaryobj2")),
                    L(
                        "music_secondaryobj_sg", "MUSIC", "DYNAMIC_WEIGHTS", 0.5f,
                        L("snd_probe_music_secondaryobj1"),
                        L("snd_probe_music_secondaryobj2")),
                    L(
                        "music_tertiaryobj_sg", "MUSIC", "DYNAMIC_WEIGHTS", 0.5f,
                        L("snd_probe_music_tertiaryobj1"),
                        L("snd_probe_music_tertiaryobj2")),
                    L("music_battlesuccess_sg", "MUSIC", L("snd_probe_music_battlesuccess")),
                    L("music_missionsuccess_sg", "MUSIC", L("snd_probe_music_missionsuccess")))));

    private static List<object?> Voice() =>
        L(L(0f, 2f, 26f), L(1f, 2f), L(2f, 26f));

    private static List<object?> Weather() =>
        L(
            L(
                "CLOUD_COVER", L("TOP", 2600f, "BOTTOM", 2200f, "THICKNESS", 80f),
                "ZONE1", L(
                    "FOG_COLOR", L(0.55f, 0.6f, 0.7f),
                    "FOG_RANGES", L(600f, 2400f),
                    "FOG_ALTITUDE", L(0f, 1800f),
                    "CLIP_RANGES", L(2f, 3000f),
                    "SUNLIGHT_ACTIVE", L(1f),
                    "SUNLIGHT_ORIENTATION", L(-50f, 60f, 0f),
                    "SUNLIGHT_DIFFUSE", L(1.1f),
                    "SUNLIGHT_AMBIENT", L(0.3f),
                    "SUNLIGHT_COLOR_DIFFUSE", L(1f, 1f, 1f),
                    "SUNLIGHT_COLOR_AMBIENT", L(0.9f, 0.95f, 1f)),
                "ZONE2", L(
                    "FOG_COLOR", L(0.75f, 0.8f, 0.9f),
                    "FOG_RANGES", L(900f, 3600f),
                    "FOG_ALTITUDE", L(2000f, 4000f),
                    "CLIP_RANGES", L(2f, 4500f),
                    "SUNLIGHT_ACTIVE", L(1f),
                    "SUNLIGHT_ORIENTATION", L(-20f, 150f, 0f),
                    "SUNLIGHT_DIFFUSE", L(1.4f),
                    "SUNLIGHT_AMBIENT", L(0.35f),
                    "SUNLIGHT_COLOR_DIFFUSE", L(1f, 1f, 1f),
                    "SUNLIGHT_COLOR_AMBIENT", L(1f, 1f, 1f))));

    private static List<object?> Net() =>
        L(
            L(
                L(-450f, 420f, -450f, 45f),
                L(450f, 420f, 450f, -135f),
                L(450f, 420f, -450f, -45f),
                L(-450f, 420f, 450f, 135f),
                L(-150f, 520f, -450f, 15f),
                L(150f, 520f, 450f, -165f),
                L(450f, 520f, -150f, -75f),
                L(-450f, 520f, 150f, 105f),
                L(150f, 620f, -450f, -15f),
                L(-150f, 620f, 450f, 165f),
                L(450f, 620f, 150f, -105f),
                L(-450f, 620f, -150f, 75f),
                L(-150f, 720f, -150f, 45f),
                L(150f, 720f, 150f, -135f),
                L(150f, 720f, -150f, -45f),
                L(-150f, 720f, 150f, 135f)));

    private static List<object?> Objectives() =>
        L(L("OBJECTIVE1", L("IDENTITY", L("PRIMARY", 1f, "MSG_PROBE_OBJECTIVE"))));

    private static List<object?> InstantAction() =>
        L(
            "mission_type", L("dogfight_squadron"),
            "disallow_missions", L("ground_target"),
            "player_plane", L("Bloodhawk"),
            "num_wingmen", L(4f),
            "wingman_plane", L("Warhawk"),
            "zeppelin_type", L("cargo"),
            "cargo_zeppelin", L("probezep"),
            "passenger_zeppelin", L("probezep"),
            "military_zeppelin", L("probezep"),
            "group1", L(
                "num_enemies", L(6f),
                "enemy_name", L("MSG_PROBE_GROUP1"),
                "enemy_plane", L("Firebrand"),
                "enemy_skill", L("novice"),
                "enemy_accentID", L(27f)),
            "group2", L(
                "num_enemies", L(9f),
                "enemy_name", L("MSG_PROBE_GROUP2"),
                "enemy_plane", L("Fury"),
                "enemy_skill", L("veteran")),
            "group3", null,
            "group4", L(
                "num_enemies", L(2f),
                "enemy_name", L("MSG_PROBE_GROUP4"),
                "enemy_plane", L("Warhawk"),
                "enemy_skill", L("ace"),
                "enemy_accentID", L(12f)),
            "ace_name", L("MSG_PROBE_ACE_NAME"),
            "ace_plane", L("Peacemaker"),
            "ace_skill", L("ace"),
            "ace_stats", L(9f, 9f, 9f, 9f, 9f, 9f, 9f, 9f, 9f),
            "ace_accentID", L(24f),
            "ace_pattern", L("probepattern"),
            "ace_decal1", L(21f),
            "ace_decal2", L(3f),
            "ace_decal3", L(3f),
            "ace_color1", L(149f, 163f, 195f),
            "ace_color2", L(89f, 114f, 159f),
            "ace_color3", L(233f, 228f, 240f),
            "lives", L(3f));
}
