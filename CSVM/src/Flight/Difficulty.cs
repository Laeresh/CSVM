using System;

namespace CSVM.Flight;

/// <summary>The difficulty setting, as the engine's own 0/1/2, and the one thing it does: scale an
/// enemy vehicle's armour and health maxima at spawn (docs/org/vehicleDamage.md "The difficulty
/// scale"). It reaches nothing else. The setting is readable in the executable only through
/// <c>FUN_00440710</c>, whose four callers are the roster spawn, the options screen, the settings
/// save pass and Instant Action's save/set/restore around that same spawn, so no AI skill,
/// accuracy or aggression is keyed to it.</summary>
public static class Difficulty
{
    /// <summary>Campaign "Normal", Instant Action "novice". The shipped default: the settings
    /// registration at <c>0x0043ff36</c> stores zero into the setting after creating it.</summary>
    public const int Normal = 0;

    /// <summary>Campaign "Hard", Instant Action "veteran". The unscaled tier.</summary>
    public const int Hard = 1;

    /// <summary>Campaign "Hardest", Instant Action "ace".</summary>
    public const int Hardest = 2;

    // The scale's own two constants, read at 0x0060802c and 0x006032dc: factor = 1 + k * 0.125.
    private const float Step = 0.125f;

    /// <summary>What an enemy vehicle's armour and health maxima are multiplied by at spawn:
    /// 0.75 / 1.0 / 1.25. ⚠ The low tier is 0.75, not 0.875 — the branch at <c>0x0047cb3b</c> loads
    /// <c>k = -2</c>, and the spread is a symmetric two <see cref="Step"/>s either side of 1.0. It
    /// applies only to a vehicle whose team differs from the player's, and only to the maxima; the
    /// per-spawn jitter runs after it, on the scaled pools.</summary>
    public static float EnemyDurabilityFactor(int difficulty) => 1f + K(difficulty) * Step;

    /// <summary>The factor one spawn actually takes. The engine's gate is "team differs from the
    /// player's", so a spawn carrying no team is scaled too: an AI rig's fallback team is its
    /// shooter id's band, never <see cref="AimAssist.PlayerTeam"/>. A per-spawn setting outranks the
    /// session's, which is the whole of what an Instant Action wave's skill does.
    /// ⚠ Neutral (team 0) is scaled as well. The test is inequality with the player's team, not
    /// hostility — <see cref="AimAssist.Hostile"/> is a different question and not this one.</summary>
    public static float FactorForSpawn(int? team, int? spawnDifficulty, int sessionDifficulty) =>
        team == AimAssist.PlayerTeam
            ? 1f
            : EnemyDurabilityFactor(spawnDifficulty ?? sessionDifficulty);

    /// <summary>The setting a name selects, taking both vocabularies: the campaign selector's
    /// <c>IDS_DIFFICULTY</c> (normal / hard / hardest) and Instant Action's <c>IDS_IA_DIFFICULTY</c>
    /// (novice / veteran / ace), which name the same three tiers. A bare 0, 1 or 2 is taken as
    /// itself. Null for anything else, so a caller can reject rather than silently pick a tier.</summary>
    public static int? Parse(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "normal" or "novice" or "0" => Normal,
        "hard" or "veteran" or "1" => Hard,
        "hardest" or "ace" or "2" => Hardest,
        _ => null,
    };

    /// <summary>The campaign selector's own label for a tier, for a log line or a menu row.</summary>
    public static string Label(int difficulty) => Clamp(difficulty) switch
    {
        Normal => "Normal",
        Hard => "Hard",
        _ => "Hardest",
    };

    /// <summary>The tier's word as the options file and the <c>--difficulty=</c> flag spell it,
    /// the campaign selector's label in lower case, so a saved value reads back through
    /// <see cref="Parse"/> and a player can copy it from the file onto a command line.</summary>
    public static string Word(int difficulty) => Label(difficulty).ToLowerInvariant();

    /// <summary>Any integer brought onto the three tiers, the way the engine's own setter is fed
    /// (<c>0 -> 0, 2 -> 2, anything else -> 1</c>).</summary>
    public static int Clamp(int difficulty) =>
        difficulty == Normal || difficulty == Hardest ? difficulty : Hard;

    // The multiplier's integer input, off the branch at 0x0047cb32-0x0047cb4e: -2 on the low tier,
    // +2 on the high one, and zero in the middle, where the engine skips the whole block.
    private static int K(int difficulty) => Clamp(difficulty) switch
    {
        Normal => -2,
        Hardest => 2,
        _ => 0,
    };
}
