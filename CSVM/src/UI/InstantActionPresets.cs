using System;
using System.Collections.Generic;

namespace CSVM.UI;

/// <summary>
/// The original's Instant Action Table of Contents: 19 named preset scenarios that fill the whole
/// setup screen when one is picked. Decoded from 19 records of 0x230 bytes at `0x0061b090`, applied
/// by `FUN_004102c0` over the live setup struct at `0x0064ab5c`; the record layout, the per-preset
/// configuration and the sentinel rules are in docs/formats/instant-action.md, "Table of Contents
/// presets". The table below is transcribed from that page by NAME rather than by the record's own
/// dropdown indices, so it stays diffable against the decode and cannot be silently invalidated by
/// a roster reordering — <see cref="Resolve"/> does the name-to-index step against
/// <see cref="LaunchMenu"/>'s own rosters, which are the screen's single source of order.
/// Presets fly STOCK airframes (a record's own two plane blocks are overwritten from the stock
/// table at `0x00619f58`), so nothing here depends on the hangar.
/// </summary>
public static class InstantActionPresets
{
    // What FUN_004102c0 substitutes for a wave whose militia/skill/aircraft are the out-of-range
    // "unset" sentinels (13, 3 and 11, each one past its dropdown's last row): militia 4, skill 1,
    // aircraft 5. The substitution is self-consistent because Fortune Hunter flies all eleven, so
    // the aircraft always resolves. It never reaches a flown mission — the mission builder
    // FUN_004175f0 skips any wave at 0 enemies — but it IS what a pilot inherits on raising the
    // count of an empty wave, so dropping it would be right in one state and wrong in the other.
    private const string UnsetMilitia = "Fortune Hunter";
    private const string UnsetAircraft = "Devastator";
    private const string UnsetSkill = "veteran";

    // The 19 presets in langui 3600-3618 order, transcribed from docs/formats/instant-action.md's
    // own table. A row is name, mission type, environment, player aircraft, wingman count, wingman
    // aircraft, then its waves in order. WingmanPlane is null wherever the count is 0: the record
    // holds a value at +0x84 there, but the decode does not report one for those presets and the
    // screen hides the field entirely at 0 wingmen, so there is nothing to apply.
    private static readonly Preset[] Table =
    {
        new("Girl Trouble", "dogfight_squadron", "Sky Haven", "Firebrand", 2, "Peacemaker",
            new Wave[] { new(4, "Medusa", "Kestrel", "veteran"), new(2, "Black Swan", "Fury", "ace") }),
        new("Sour Grapes", "stunt_flying", "Manhattan", "Bloodhawk", 0, null,
            new Wave[] { new(4, "Blake Aviation", "Bloodhawk", "veteran") }),
        new("Me and My Big Mouth", "dogfight_ace", "the ocean", "Autogyro", 0, null,
            Array.Empty<Wave>()),
        new("The Angry Luau", "zeppelin_run", "Hawaii", "Fury", 4, "Hellhound",
            new Wave[] { new(6, "Fortune Hunter", "Devastator", "ace") }),
        new("Hat Trick", "dogfight_squadron", "an airfield", "Brigand", 0, null,
            new Wave[] { new(3, "Black Hat", "Warhawk", "veteran") }),
        new("Seaside Show-Off", "stunt_flying", "the ocean", "Peacemaker", 2, "Kestrel",
            new Wave[] { new(4, "Hughes Aviation", "Fury", "veteran") }),
        new("Two to Tango", "dogfight_ace", "Sky Haven", "Hellhound", 0, null,
            Array.Empty<Wave>()),
        new("Death of the Gemini", "zeppelin_run", "the clouds", "Kestrel", 4, "Kestrel",
            new Wave[]
            {
                new(6, "Hughes Aviation", "Bloodhawk", "ace"),
                new(3, "Blake Aviation", "Peacemaker", "veteran"),
            }),
        new("Hares and Tortoises", "dogfight_squadron", "a movie studio", "Bloodhawk", 2, "Bloodhawk",
            new Wave[]
            {
                new(4, "Studio Security", "Autogyro", "novice"),
                new(4, "Black Hat", "Warhawk", "veteran"),
            }),
        new("Swan's Gauntlet", "stunt_flying", "an airfield", "Fury", 0, null,
            new Wave[] { new(4, "Black Swan", "Fury", "ace") }),
        new("Aloha, Ace!", "dogfight_ace", "Hawaii", "Devastator", 0, null,
            Array.Empty<Wave>()),
        new("Black Hats and Hoplites", "zeppelin_run", "Sky Haven", "Autogyro", 2, "Autogyro",
            new Wave[] { new(6, "Black Hat", "Brigand", "veteran") }),
        new("From Russia with Hate", "dogfight_squadron", "the clouds", "Fury", 2, "Peacemaker",
            new Wave[]
            {
                new(4, "Russian", "Devastator", "novice"),
                new(4, "Russian", "Devastator", "veteran"),
                new(2, "Russian", "Devastator", "ace"),
            }),
        new("Honor, Hollywood Style", "dogfight_ace", "a movie studio", "Brigand", 0, null,
            Array.Empty<Wave>()),
        new("Manhattan Tea Party", "zeppelin_run", "Manhattan", "Warhawk", 4, "Devastator",
            new Wave[]
            {
                new(6, "British", "Peacemaker", "veteran"),
                new(4, "Sacred Trust", "Hellhound", "ace"),
            }),
        new("Let's You, Me, and Him Fight", "dogfight_squadron", "the ocean", "Hellhound", 3, "Firebrand",
            new Wave[]
            {
                new(4, "Russian", "Devastator", "veteran"),
                new(4, "Fortune Hunter", "Kestrel", "ace"),
            }),
        new("The Longest New York Minute", "dogfight_ace", "Manhattan", "Brigand", 0, null,
            Array.Empty<Wave>()),
        new("Rocky Mountain Hijinks", "stunt_flying", "Sky Haven", "Peacemaker", 0, null,
            new Wave[] { new(6, "Sacred Trust", "Warhawk", "veteran") }),
        new("The Hollywood Brawl", "zeppelin_run", "a movie studio", "Devastator", 4, "Kestrel",
            new Wave[]
            {
                new(6, "Hollywood Knight", "Firebrand", "ace"),
                new(4, "Studio Security", "Autogyro", "ace"),
            }),
    };

    /// <summary>The 19 presets in the order the contents list shows them: their names are langui
    /// 3600 to 3618, and the list-fill callback writes the literal count at `0x0040c019`.</summary>
    public static IReadOnlyList<Preset> All => Table;

    /// <summary>Turns preset <paramref name="index"/> into the cursor positions the setup screens
    /// hold, resolved against <see cref="LaunchMenu"/>'s own rosters so a preset can never disagree
    /// with the list it is filling. The returned wave array is always <paramref name="waveSlots"/>
    /// long; slots the preset does not use carry 0 enemies and the substitution above. Throws
    /// <see cref="ArgumentException"/> on a name no roster holds, the same fail-loud policy
    /// <see cref="LaunchMenu.AircraftFor"/> applies to wizard-only data.</summary>
    public static Applied Resolve(int index, int waveSlots)
    {
        if (index < 0 || index >= Table.Length)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"there are {Table.Length} presets");

        var preset = Table[index];
        int environmentIndex = IndexOf(LaunchMenu.EnvironmentNames(), preset.Environment, "environment", preset.Name);
        string code = LaunchMenu.EnvironmentCodes()[environmentIndex];
        // The mission-type cursor indexes the FILTERED roster for that environment (Stunt Flying is
        // dropped where disallow_missions bars it), not the four-row master list.
        int missionTypeIndex = IndexOf(LaunchMenu.MissionTypeKeysFor(code), preset.MissionType, "mission type", preset.Name);

        var planes = LaunchMenu.PlaneNames();
        int playerPlaneIndex = IndexOf(planes, preset.PlayerPlane, "player aircraft", preset.Name);
        int? wingmanPlaneIndex = preset.WingmanPlane is { } wingman
            ? IndexOf(planes, wingman, "wingman aircraft", preset.Name)
            : null;

        var waves = new AppliedWave[waveSlots];
        for (int i = 0; i < waves.Length; i++)
        {
            var wave = i < preset.Waves.Length
                ? preset.Waves[i]
                : new Wave(0, UnsetMilitia, UnsetAircraft, UnsetSkill);
            int militiaIndex = IndexOf(LaunchMenu.MilitiaNames(), wave.Militia, "militia", preset.Name);
            waves[i] = new AppliedWave(
                wave.Count,
                militiaIndex,
                IndexOf(LaunchMenu.AircraftFor(wave.Militia), wave.Aircraft, $"{wave.Militia} aircraft", preset.Name),
                IndexOf(LaunchMenu.SkillKeys(), wave.Skill, "skill", preset.Name));
        }

        return new Applied(environmentIndex, missionTypeIndex, playerPlaneIndex,
            preset.NumWingmen, wingmanPlaneIndex, waves);
    }

    private static int IndexOf(string[] roster, string value, string what, string preset)
    {
        int i = Array.IndexOf(roster, value);
        if (i < 0)
            throw new ArgumentException($"preset '{preset}': '{value}' is not a known {what}");
        return i;
    }

    /// <summary>One wave of a preset: how many enemies, which militia flies it, which of that
    /// militia's aircraft, and at which skill. Names, not indices — see the type's own
    /// summary.</summary>
    public readonly record struct Wave(int Count, string Militia, string Aircraft, string Skill);

    /// <summary>One wave resolved to the cursor positions the wave editor holds.</summary>
    public readonly record struct AppliedWave(int Count, int MilitiaIndex, int AircraftIndex, int SkillIndex);

    /// <summary>One preset scenario as the decode reports it. <c>MissionType</c> is the `ia.json`
    /// `mission_type` key; <c>Environment</c> is the environment's display name;
    /// <c>WingmanPlane</c> is null exactly when <c>NumWingmen</c> is 0.</summary>
    public sealed record Preset(
        string Name,
        string MissionType,
        string Environment,
        string PlayerPlane,
        int NumWingmen,
        string? WingmanPlane,
        Wave[] Waves);

    /// <summary>A preset resolved against the live rosters. <c>WingmanPlaneIndex</c> is null when
    /// the preset flies no wingmen, in which case the screen's own wingman-aircraft cursor is left
    /// where it was rather than moved to a value the decode does not report.</summary>
    public sealed record Applied(
        int EnvironmentIndex,
        int MissionTypeIndex,
        int PlayerPlaneIndex,
        int NumWingmen,
        int? WingmanPlaneIndex,
        AppliedWave[] Waves);
}
