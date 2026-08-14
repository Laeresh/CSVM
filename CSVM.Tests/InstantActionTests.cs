using System;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The Instant Action reader (docs/formats/instant-action.md, PLAN-instant-action.md B6):
/// fixture units for the full record, the built-in defaults every optional key falls back to
/// (matching the original's own <c>FUN_00458ff0</c>/<c>FUN_00459390</c> reset-then-overlay),
/// the <c>dogfight_ace</c> zero-forcing rule, and the <c>--ia=</c> plain-JSON-object path — plus
/// golden counts over the install's 8 shipped chapters so a reader change moves a test instead
/// of silently drifting.
/// </summary>
public class InstantActionTests
{
    private static readonly string[] Chapters =
        { "C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5" };

    [Fact]
    public void TheFullRecordParsesFromTheFixture()
    {
        var def = InstantAction.Load(TestData.Fixture("ia"));

        Assert.Equal("dogfight_squadron", def.MissionType);
        Assert.Equal(new[] { "ground_target" }, def.DisallowMissions);
        Assert.Equal("Bloodhawk", def.PlayerPlane);
        Assert.Equal(4, def.NumWingmen);
        Assert.Equal("cargo", def.ZeppelinType);
        Assert.Equal("probezep", def.CargoZeppelinNode);
        Assert.Equal("probezep", def.PassengerZeppelinNode);
        Assert.Equal("probezep", def.MilitaryZeppelinNode);

        Assert.Equal(4, def.Waves.Count);
        // Clamped to 6 — the fixture authors 9.
        Assert.Equal(new InstantActionWave(6, "MSG_PROBE_GROUP1", "Firebrand", "novice"), def.Waves[0]);
        Assert.Equal(new InstantActionWave(6, "MSG_PROBE_GROUP2", "Fury", "veteran"), def.Waves[1]);
        // group3 authored as a bare "group3", null — reads as the empty/default wave.
        Assert.Equal(new InstantActionWave(0, "Blake Firebrand", "Firebrand", "veteran"), def.Waves[2]);
        Assert.Equal(new InstantActionWave(2, "MSG_PROBE_GROUP4", "Warhawk", "ace"), def.Waves[3]);

        Assert.Equal("MSG_PROBE_ACE_NAME", def.AceName);
        Assert.Equal("Peacemaker", def.AcePlane);
        Assert.Equal("ace", def.AceSkill);
        Assert.Equal(24, def.AceAccentId);
        Assert.Equal(9, def.AceStats.DareDevil);
        Assert.Equal(9, def.AceStats.Constitution);

        Assert.NotNull(def.AceLivery);
        var livery = def.AceLivery!;
        Assert.Equal("probepattern", livery.Pattern);
        Assert.Equal(21, livery.NoseDecal);
        Assert.Equal(3, livery.TailDecal);
        Assert.Equal(3, livery.WingDecal);
        Assert.Equal(PaintScheme.FromBytes(149, 163, 195), livery.Color1);
        Assert.Equal(PaintScheme.FromBytes(89, 114, 159), livery.Color2);
        Assert.Equal(PaintScheme.FromBytes(233, 228, 240), livery.Color3);

        Assert.Equal(3, def.Lives);
    }

    [Fact]
    public void EveryOptionalKeyFallsBackToTheOriginalsOwnDefault()
    {
        // dogfight_ace with a group1 authoring 6 enemies: the mission-type zero-forcing rule
        // must win over the authored count, exactly as the parser's own reset does.
        var def = InstantAction.Load(TestData.Fixture("ia-minimal"));

        Assert.Equal("dogfight_ace", def.MissionType);
        Assert.Empty(def.DisallowMissions);
        Assert.Equal("Devastator", def.PlayerPlane);   // player_plane/wingman_plane/ace_plane default
        Assert.Equal(0, def.NumWingmen);                // forced to 0 on dogfight_ace
        Assert.Null(def.ZeppelinType);                  // no decoded default for this key
        Assert.Equal("vostokzep", def.CargoZeppelinNode);
        Assert.Equal("vostokzep", def.PassengerZeppelinNode);
        Assert.Equal("vostokzep", def.MilitaryZeppelinNode);

        Assert.Equal(4, def.Waves.Count);
        foreach (var wave in def.Waves)
        {
            // Every wave count is forced to 0 on dogfight_ace, group1's authored 6 included.
            Assert.Equal(0, wave.NumEnemies);
        }
        Assert.Equal("MSG_PROBE_G1", def.Waves[0].EnemyName); // the authored fields still read
        Assert.Equal(new InstantActionWave(0, "Blake Firebrand", "Firebrand", "veteran"), def.Waves[1]);

        Assert.Equal("Marshall Bill Redmann", def.AceName);
        Assert.Equal("Devastator", def.AcePlane);
        Assert.Equal("veteran", def.AceSkill);
        Assert.Equal(-1, def.AceAccentId);
        Assert.Equal(5, def.AceStats.DareDevil);
        Assert.Equal(6, def.AceStats.NaturalTouch);
        Assert.Equal(9, def.AceStats.QuickDraw);
        Assert.Null(def.AceLivery); // no ace_pattern authored — nothing to paint

        Assert.Equal(1, def.Lives); // the INVENTED field's own default
    }

    [Fact]
    public void ANonAceMissionKeepsItsAuthoredWaveCounts()
    {
        // The zero-forcing rule is keyed on the MISSION TYPE, not on absence — a non-ace mission
        // with an authored wave keeps it even when num_wingmen/player_plane are unauthored (C2B's
        // own shape).
        var def = InstantAction.LoadFromJson(TestData.Fixture("ia-cli.json"));
        Assert.Equal("zeppelin_run", def.MissionType);
        Assert.Equal(3, def.Waves[0].NumEnemies);
    }

    [Fact]
    public void TheCliJsonObjectParsesThroughTheSameFieldNames()
    {
        var def = InstantAction.LoadFromJson(TestData.Fixture("ia-cli.json"));

        Assert.Equal("zeppelin_run", def.MissionType);
        Assert.Equal(new[] { "ground_target", "stunt_flying" }, def.DisallowMissions);
        Assert.Equal("Kestrel", def.PlayerPlane);
        Assert.Equal(2, def.NumWingmen);
        Assert.Equal("cargo", def.ZeppelinType);
        Assert.Equal("probezep", def.CargoZeppelinNode);

        Assert.Equal(new InstantActionWave(3, "MSG_PROBE_CLI_G1", "Brigand", "veteran"), def.Waves[0]);
        // group2 authored as a JSON null — same graceful default as a bare zrd flag.
        Assert.Equal(new InstantActionWave(0, "Blake Firebrand", "Firebrand", "veteran"), def.Waves[1]);
        // group3/group4 not authored at all.
        Assert.Equal(new InstantActionWave(0, "Blake Firebrand", "Firebrand", "veteran"), def.Waves[2]);

        Assert.Equal("MSG_PROBE_CLI_ACE", def.AceName);
        Assert.Equal("Hellhound", def.AcePlane);
        Assert.Equal(30, def.AceAccentId);
        Assert.NotNull(def.AceLivery);
        var livery = def.AceLivery!;
        Assert.Equal("probepattern", livery.Pattern);
        Assert.Equal(5, livery.NoseDecal);
        Assert.Equal(PaintScheme.FromBytes(10, 20, 30), livery.Color1);

        Assert.Equal(2, def.Lives);
    }

    [Fact]
    public void LoadFromJsonRejectsANonObjectRoot()
    {
        var path = TestData.TempDir();
        var file = System.IO.Path.Combine(path, "not-an-object.json");
        System.IO.File.WriteAllText(file, "[1, 2, 3]");
        Assert.Throws<System.IO.InvalidDataException>(() => InstantAction.LoadFromJson(file));
    }

    // ---- Goldens over the install ---------------------------------------------------------------

    [ExtractedDataFact]
    public void TheInstallWideCensusMatchesTheScopingTable()
    {
        // docs/formats/spawns.md's "Environment → chapter" / mission_type census, re-read this
        // session directly from every chapter's ia.zrd.json.
        var expected = new (string Chapter, string MissionType, string PlayerPlane, int NumWingmen,
            int[] Waves, string AcePlane, string AcePattern, int AceAccentId)[]
        {
            ("C1", "dogfight_squadron", "Bloodhawk", 3, new[] { 6, 5, 4, 3 }, "Peacemaker", "blake", 24),
            ("C1B", "stunt_flying", "Firebrand", 3, new[] { 4, 3, 3, 2 }, "Fury", "blckswan", 23),
            ("C1C", "zeppelin_run", "Fury", 3, new[] { 4, 3, 3, 2 }, "Firebrand", "hollywd", 26),
            ("C2", "stunt_flying", "Brigand", 3, new[] { 4, 4, 3, 2 }, "Bloodhawk", "hughes", 25),
            // C2B ships neither player_plane nor num_wingmen — both read the original's own default.
            ("C2B", "zeppelin_run", "Devastator", 0, new[] { 6, 5, 4, 3 }, "Devastator", "hollywd", 26),
            ("C3", "dogfight_squadron", "Hellhound", 3, new[] { 6, 5, 4, 3 }, "Kestrel", "medusas", 17),
            ("C4", "stunt_flying", "Peacemaker", 3, new[] { 6, 5, 4, 3 }, "Bloodhawk", "hughes", 31),
            ("C5", "stunt_flying", "Kestrel", 3, new[] { 6, 5, 4, 3 }, "Peacemaker", "broadway", 33),
        };
        Assert.Equal(Chapters.Length, expected.Length);

        foreach (var row in expected)
        {
            var def = InstantAction.Load(SessionPaths.MissionZrdr(TestData.DataRoot!, row.Chapter, "IA1"));
            Assert.Equal(row.MissionType, def.MissionType);
            Assert.Equal(row.PlayerPlane, def.PlayerPlane);
            Assert.Equal(row.NumWingmen, def.NumWingmen);
            Assert.Equal(4, def.Waves.Count);
            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(row.Waves[i], def.Waves[i].NumEnemies);
            }
            Assert.Equal(row.AcePlane, def.AcePlane);
            Assert.Equal(row.AceAccentId, def.AceAccentId);
            Assert.NotNull(def.AceLivery);
            Assert.Equal(row.AcePattern, def.AceLivery!.Pattern);
            // ace_stats is [9,9,9,9,9,9,9,9,9] in all 8 shipped chapters.
            Assert.Equal(9, def.AceStats.DareDevil);
            Assert.Equal(9, def.AceStats.Constitution);
            // None of the 8 chapters' own mission_type is dogfight_ace, so the zero-forcing rule
            // never fires on shipped data — only on a --ia= or wizard-built ace mission.
            Assert.NotEqual("dogfight_ace", def.MissionType);
        }
    }
}
