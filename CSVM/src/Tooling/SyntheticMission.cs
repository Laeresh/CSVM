namespace CSVM.Tooling;

/// <summary>
/// The synthetic tree's one invented mission scope, <c>C1/PROBE1/zrdr/</c>. Its hand-authored
/// <c>weather.json</c> has two zones with different sun bearings and fog, under a cloud band whose
/// opaque core sits inside the flight envelope. Its <c>net.json</c> is one 16-entry free-for-all
/// block. The weather, sun and net-table suites read it in place of a shipped mission. No shipped
/// mission name enters the tree, so a suite gated on one still skips rather than failing on a
/// half-filled folder. Shapes: <c>docs/formats/weather.md</c>, <c>docs/formats/net-spawns.md</c>.
/// </summary>
public static class SyntheticMission
{
    /// <summary>The chapter folder the invented mission sits in, the tree's only chapter.</summary>
    public const string Chapter = "C1";

    /// <summary>The invented mission's folder name, matching no shipped mission.</summary>
    public const string Mission = "PROBE1";

    /// <summary>The campaign's one story position's storage folder: campaign 1, mission 9, an
    /// address no shipped mission holds.</summary>
    public const string CampaignMission = "M09";

    private static readonly string[] Records = { "weather.json", "net.json" };

    // The campaign family as (fixture, path under extracted/): the sequence's one entry, the
    // briefing dialog holding that entry's state, and the mission's objectives note.
    private static readonly (string Fixture, string Target)[] CampaignRecords =
    {
        ("synthetic/zrdr/cm_sequence.json", "zrdr/cm_sequence.json"),
        ("synthetic/zrdr/Briefing.json", "zrdr/Briefing.json"),
        ($"synthetic/{Chapter}/{CampaignMission}/zrdr/objectives.json", $"{Chapter}/{CampaignMission}/zrdr/objectives.json"),
    };

    /// <summary>Writes the mission's zrdr scope under <paramref name="tree"/>.</summary>
    public static void WriteMission(SyntheticTree tree)
    {
        foreach (string record in Records)
        {
            tree.CopyFixture($"synthetic/{Chapter}/{Mission}/zrdr/{record}", $"{Chapter}/{Mission}/zrdr/{record}");
        }
    }

    /// <summary>Writes the campaign's sequence, its briefing and its objectives under
    /// <paramref name="tree"/>. Shapes: <c>docs/formats/campaign-sequence.md</c>.</summary>
    public static void WriteCampaign(SyntheticTree tree)
    {
        foreach (var (fixture, target) in CampaignRecords)
        {
            tree.CopyFixture(fixture, target);
        }
    }
}
