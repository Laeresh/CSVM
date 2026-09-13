using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Which militia def each campaign enemy block resolves to, and the livery that def authors. The
/// spawner loads <see cref="PlaneStats.LoadForAi"/> with the block's own def name, so this table
/// decides the enemy's damage model, its <c>weapons</c> fit, its pilot vector and its paint
/// (docs/org/paint.md, docs/formats/vehicle.md). The resolution is a function of the mission and
/// the block alone: nothing here reads a spawn order or a running count.
/// </summary>
public class CampaignMilitiaLiveryTests
{
    // CM01 to CM24 in story order: the storage address, then each distinct
    // "<militia def>:<paint_pattern>" the mission's enemy aircraft resolve to with how many blocks
    // take it, ordinal-ascending by def name. Surface hulls and ground vehicles are not aircraft
    // and are absent; a def whose chain authors no paint_pattern reads ":-".
    private static readonly (string Address, string Enemies)[] EnemyDefsByMission =
    {
        ("C3/M01", "medkestrel:medusas x3"),
        ("C3/M05", "britbalmoral:british x3, britpeace:british x6"),
        ("C3/M02", ""),
        ("C3/M03", "britpeace:british x4"),
        ("C3/M04", "medbrigand:medusas x5, medkestrel:medusas x5"),
        ("C1C/M01", "bsfury:blckswan x1, rusdevastator:cccp x4"),
        ("C1/M02", "blakepeace_2:blake x4, bloodhawk:- x1"),
        ("C1B/M03", "blakepeace_2:blake x4"),
        ("C1/M04", "blakebloodhawk:blake x9, blakepeace_2:blake x5"),
        ("C1/M05", "bhatbrigand_2:blackhat x6"),
        ("C2/M02", "hkfirebrand:hollywd x5, secfury:studio x6, secgyro:studio x4"),
        ("C2/M01", "hkfirebrand:hollywd x4, secfury:studio x4, secgyro:studio x4"),
        ("C2/M03", "habloodhawk:hughes x5, hafury:hughes x6"),
        ("C2B/M04", "habloodhawk:hughes x4, hakestrel:hughes x6"),
        ("C2/M05", "blakepeace:blake x3, blakepeace_2:blake x1, blakepeace_3:blake x1"),
        ("C4/M01", "bhatbrigand:blackhat x3, bhatbrigand_2:blackhat x1, stihellhound:sactrust x4"),
        ("C4/M02", "bhatbrigand:blackhat x9, bhatbrigand_2:blackhat x1, bhatbrigand_5:blackhat x1, bhatgyro:blackhat x1"),
        ("C4/M03", "bhatbrigand:blackhat x6, bhatbrigand_2:blackhat x1, bhatbrigand_5:blackhat x1, bhatwarhawk:blackhat x11, bhatwarhawk_5:blackhat x1"),
        ("C4/M04", "bhatbrigand:blackhat x4, bhatbrigand_2:blackhat x1, bhatbrigand_5:blackhat x1, bhatgyro:blackhat x3, bhatwarhawk:blackhat x5, bhatwarhawk_5:blackhat x1"),
        ("C4/M05", "bhatbrigand:blackhat x5, bhatbrigand_5:blackhat x1, bhatwarhawk:blackhat x3, bhatwarhawk_5:blackhat x1"),
        ("C5/M01", "autogyro:- x1, bhatbrigand_5:blackhat x3, bhatwarhawk_5:blackhat x3"),
        ("C5/M02", "bhatbrigand_5:blackhat x14, stihellhound_5:sactrust x4"),
        ("C5/M03", "stihellhound_5:sactrust x12"),
        ("C5/M04", "bhatwarhawk_5:blackhat x6, stihellhound_5:sactrust x8"),
    };

    // The enemy aircraft an enemy generator launches, from the same missions' disabled parameter
    // blocks. Same resolution, same table shape; missions with no such template are absent.
    private static readonly (string Address, string Templates)[] GeneratorDefsByMission =
    {
        ("C3/M03", "britpeace:british x1"),
        ("C1C/M01", "bsfury:blckswan x1, rusdevastator:cccp x1"),
        ("C1/M02", "blakepeace_2:blake x2"),
        ("C1B/M03", "rusdevastator:cccp x1"),
        ("C1/M04", "blakepeace_2:blake x1"),
        ("C2B/M04", "habloodhawk:hughes x1"),
        ("C2/M05", "blakepeace:blake x1"),
        ("C4/M05", "bhatwarhawk:blackhat x1"),
        ("C5/M03", "germanhellhound:german x1"),
        ("C5/M04", "stihellhound_5:sactrust x1"),
    };

    private static string ZrdrPath =>
        SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"));

    /// <summary>The campaign's own enemy set, mission by mission. Every enemy aeroplane of every
    /// shipped mission resolves the militia def its block name carries, and all but two of those
    /// defs author a <c>paint_pattern</c>; the two that do not are base defs a mission fields
    /// directly, and they keep the shipped skins rather than being given invented colours.</summary>
    [ExtractedDataFact]
    public void EveryCampaignEnemyBlockResolvesItsMilitiaDefAndLivery()
    {
        var defs = VehicleDefs.Load(ZrdrPath);
        var patterns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var missions = CampaignSequence.Load(ZrdrPath);

        Assert.Equal(EnemyDefsByMission.Length, missions.Count);
        for (int i = 0; i < missions.Count; i++)
        {
            var (chapter, folder) = Address(missions[i]);
            Assert.Equal(EnemyDefsByMission[i].Address, $"{chapter}/{folder}");
            var plan = CampaignRosterPlan.Build(
                AiSkills.LoadRoster(SessionPaths.MissionZrdr(TestData.DataRoot!, chapter, folder)),
                defs, AiNets.Load(SessionPaths.ChapterZrdr(TestData.DataRoot!, chapter)));

            Assert.Equal(EnemyDefsByMission[i].Enemies, Table(plan.Spawns, patterns));
        }
    }

    /// <summary>The same resolution on the generator half: a mission's disabled parameter blocks
    /// are planned the way its live ones are, so an aircraft a generator launches carries the same
    /// militia def and the same livery as one standing on the roster at mission start.</summary>
    [ExtractedDataFact]
    public void EveryGeneratorTemplateResolvesItsMilitiaDefAndLivery()
    {
        var defs = VehicleDefs.Load(ZrdrPath);
        var patterns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var expected = GeneratorDefsByMission.ToDictionary(e => e.Address, e => e.Templates,
            StringComparer.OrdinalIgnoreCase);

        foreach (var mission in CampaignSequence.Load(ZrdrPath))
        {
            var (chapter, folder) = Address(mission);
            var templates = CampaignRosterPlan.GeneratorTemplates(
                SessionPaths.MissionZrdr(TestData.DataRoot!, chapter, folder),
                defs, AiNets.Load(SessionPaths.ChapterZrdr(TestData.DataRoot!, chapter)));

            string table = Table(templates.Values.ToList(), patterns);
            Assert.Equal(expected.TryGetValue($"{chapter}/{folder}", out var want) ? want : string.Empty, table);
        }
    }

    /// <summary>The two defs a campaign mission fields that author no livery at all, and the
    /// reason no colours are invented for them: the shipped data names the pattern nowhere. The
    /// Broadway Bomber is the same case one step further out, its masks shipping under no vehicle
    /// def at all, which is why no def name below can be written for it.</summary>
    [ExtractedDataFact]
    public void TheDefsThatAuthorNoLiveryStayUnpainted()
    {
        Assert.Null(PaintScheme.ForDef(ZrdrPath, "bloodhawk"));
        Assert.Null(PaintScheme.ForDef(ZrdrPath, "autogyro"));
        Assert.Null(PaintScheme.ForDef(ZrdrPath, "balmoral"));
        Assert.DoesNotContain(PaintScheme.LoadCatalog(ZrdrPath),
            s => s.Pattern.Contains("broadway", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The livery a militia def authors is the whole scheme, not the pattern alone: the
    /// Medusa Kestrels CM01 fields carry three colours and three decals out of their own def, and
    /// they are not the player militia's.</summary>
    [ExtractedDataFact]
    public void AMilitiaDefAuthorsItsColoursAndDecalsTooNotJustThePattern()
    {
        var medusa = PaintScheme.ForDef(ZrdrPath, "medkestrel");
        var fortune = PaintScheme.ForDef(ZrdrPath, "wingman");

        Assert.NotNull(medusa);
        Assert.Equal("medusas", medusa!.Pattern);
        Assert.Equal(new[] { 21, 14, 14 }, new[] { medusa.NoseDecal, medusa.TailDecal, medusa.WingDecal });
        Assert.NotEqual(medusa.Color1, medusa.Color2);
        Assert.NotNull(fortune);
        Assert.Equal(LiveryResolver.DefaultPattern, fortune!.Pattern);
        Assert.NotEqual(fortune.Pattern, medusa.Pattern);
    }

    // "<def>:<pattern> xN" per distinct militia def among a plan list's enemy aircraft, comma
    // separated in ordinal def order. Surface hulls carry no airframe and no livery, so they are
    // left out; so is everything on the player's own team, which draws the default pattern.
    private static string Table(IReadOnlyList<RosterSpawnPlan> plans, Dictionary<string, string> patterns)
    {
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var plan in plans)
        {
            if (plan.Surface || plan.Team == AimAssist.PlayerTeam || plan.AiDef is not { } def)
            {
                continue;
            }

            if (!patterns.TryGetValue(def, out var pattern))
            {
                pattern = PaintScheme.ForDef(ZrdrPath, def)?.Pattern ?? "-";
                patterns[def] = pattern;
            }

            string key = $"{def}:{pattern}";
            counts[key] = counts.TryGetValue(key, out int seen) ? seen + 1 : 1;
        }
        return string.Join(", ", counts.Select(e => $"{e.Key} x{e.Value}"));
    }

    private static (string Chapter, string Folder) Address(CampaignMission mission) =>
        (mission.ChapterFolder.ToUpperInvariant(), mission.MissionFolder.ToUpperInvariant());
}
