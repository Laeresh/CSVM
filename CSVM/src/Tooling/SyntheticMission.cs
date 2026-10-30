namespace CSVM.Tooling;

/// <summary>
/// The synthetic run's invented mission addresses, whose records are <see cref="SyntheticZrdr"/>'s.
/// <c>C1/PROBE1</c> holds the weather, sun and net-table suites' records. Its <c>weather</c> has
/// two zones under a cloud band inside the flight envelope, its <c>net</c> one 16-entry block.
/// The Instant Action defs are the one shipped mission name the run
/// carries; a suite gated on any other skips. Shapes: <c>docs/formats/weather.md</c>,
/// <c>docs/formats/net-spawns.md</c>.
/// </summary>
public static class SyntheticMission
{
    /// <summary>The chapter folder the invented mission sits in, the run's only chapter.</summary>
    public const string Chapter = "C1";

    /// <summary>The invented mission's folder name, matching no shipped mission.</summary>
    public const string Mission = "PROBE1";

    /// <summary>The campaign's one story position's storage folder: campaign 1, mission 9, an
    /// address no shipped mission holds.</summary>
    public const string CampaignMission = "M09";
}
