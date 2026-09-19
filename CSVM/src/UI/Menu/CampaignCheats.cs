using System;

namespace CSVM.UI.Menu;

/// <summary>
/// What the original's four menu cheats leave switched on, carried by <see cref="CampaignFeature"/>
/// so both presentations read one answer. Three of them are typed into a screen through
/// <see cref="TypedCheat"/> and the fourth is a pilot name on the profile screen. The words, the
/// regions and the original's globals behind them are in <c>docs/formats/campaign-screens.md</c>.
/// <see cref="RevealAll"/> is the original's <c>fViewAll</c> and <see cref="AllowAll"/> its
/// <c>fAllowAll</c>. Both are process-wide there and both are kept for the rest of this menu session.
/// The mission pick and the pull-down that sets it belong to the open campaign and go with it.
/// </summary>
public sealed class CampaignCheats
{
    /// <summary>The cabin's word, which shows the mission pull-down.</summary>
    public const string MissionWord = "idaho";

    /// <summary>The table of contents' word, which opens the whole gallery.</summary>
    public const string GalleryWord = "ispy";

    /// <summary>The plane construction hub's word, which grants cash.</summary>
    public const string CashWord = "gimme";

    /// <summary>The pilot name that unlocks everything, compared case-insensitively the way the
    /// original's own <c>lstrcmpiA</c> at <c>0x00407a1c</c> compares it.</summary>
    public const string UnlockName = "crashcheat!";

    /// <summary>What one grant is worth.</summary>
    public const int CashGrant = 25000;

    /// <summary>The balance at or above which the grant refuses, the script's own test.</summary>
    public const int CashCeiling = 50000;

    /// <summary>The wallet the unlock writes, the one literal money constant in the image.</summary>
    public const int UnlockFunds = 250000;

    /// <summary>How far the gallery opens: <c>FUN_004061d0</c>'s own limit of 24 spreads once
    /// <c>fViewAll</c> is set, whatever the campaign position is.</summary>
    public const int RevealedMissions = 24;

    /// <summary>Whether the cabin shows its mission pull-down.</summary>
    public bool MissionListShown { get; private set; }

    /// <summary>The mission the pull-down stands on as a 1-based <c>cm_sequence</c> ordinal, or -1
    /// while there is none. NEXT MISSION launches it in place of the campaign's own position.</summary>
    public int MissionPick { get; private set; } = -1;

    /// <summary>Whether the gallery is open: every mission listed in the table of contents, every
    /// spread up to <see cref="RevealedMissions"/> reachable and no objective bit consulted.</summary>
    public bool RevealAll { get; private set; }

    /// <summary>Whether every airframe is offered whatever the campaign position is.</summary>
    public bool AllowAll { get; private set; }

    /// <summary>Whether <paramref name="name"/> is the unlocking pilot name.</summary>
    public static bool IsUnlockName(string name) =>
        string.Equals(name?.Trim(), UnlockName, StringComparison.OrdinalIgnoreCase);

    /// <summary>The cabin's word fired: the pull-down the script creates deactivated is activated.</summary>
    public void ShowMissionList() => MissionListShown = true;

    /// <summary>Moves the pick, a 1-based ordinal; anything outside the campaign clears it.</summary>
    public void PickMission(int ordinal) =>
        MissionPick = ordinal >= 1 && ordinal <= RevealedMissions ? ordinal : -1;

    /// <summary>The table of contents' word fired.</summary>
    public void Reveal() => RevealAll = true;

    /// <summary>The pilot name matched, which stays on for the rest of the session.</summary>
    public void AllowEverything() => AllowAll = true;

    /// <summary>Drops what belongs to the open campaign when it closes. The original's two globals
    /// are not among them: it never clears either short of the process exiting.</summary>
    public void CloseCampaign()
    {
        MissionListShown = false;
        MissionPick = -1;
    }
}
