namespace CSVM.Tooling;

/// <summary>
/// The synthetic tree's invented mission scopes. <c>C1/PROBE1/zrdr/</c> holds the weather, sun and
/// net-table suites' records. Its <c>weather.json</c> has two zones under a cloud band inside the
/// flight envelope, its <c>net.json</c> one 16-entry free-for-all block. The Instant Action defs
/// <see cref="WriteInstantAction"/> writes are the one shipped mission name in the tree. A suite
/// gated on any other still skips rather than failing on a half-filled folder. Shapes:
/// <c>docs/formats/weather.md</c>, <c>docs/formats/net-spawns.md</c>.
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

    // The chapters whose Instant Action scope carries a def, the ones the menu's presets fly. Each
    // gets the same invented record, the unit tests' own. ⚠ Not C1: versus-spawn-rotation reads a
    // C1/IA1 folder as an install's spawn lists, and the empty stage's ring stands in without one.
    private static readonly string[] InstantActionChapters = { "C1B", "C3", "C4", "C5" };

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

    /// <summary>Writes each Instant Action chapter's <c>IA1/zrdr/ia.json</c> under
    /// <paramref name="tree"/>, which the menu reads for the ace and the zeppelins. It writes nothing
    /// else into that scope. Shape: <c>docs/formats/instant-action/</c>.</summary>
    public static void WriteInstantAction(SyntheticTree tree)
    {
        foreach (string chapter in InstantActionChapters)
        {
            tree.CopyFixture("ia/ia.zrd.json", $"{chapter}/IA1/zrdr/ia.json");
        }
    }
}
