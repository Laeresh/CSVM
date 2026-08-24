using System.Globalization;

namespace CSVM.UI;

/// <summary>
/// Where a selected profile lands until the cabin screen itself ships: the cabin's four functions
/// as rows, with the profile's wallet and campaign position under them, and a working RETURN TO
/// MAIN MENU. The other three rows say so rather than pretending to route, so nothing here can be
/// mistaken for the cabin being built. Replacing this page is one line of
/// <see cref="CampaignFlow"/>'s registry.
/// </summary>
public sealed class CampaignCabinPlaceholderPage : CampaignPage
{
    // The cabin's own buttons, in the order the original's screen carries them; SAVE GAME is
    // deactivated at creation there and CHANGE MEMENTO is not shipped here.
    private static readonly string[] Rows =
    {
        "Next Mission",
        "Previous Missions",
        "Plane Construction",
        "Return to Main Menu",
    };

    /// <summary>Binds the page to its flow.</summary>
    public CampaignCabinPlaceholderPage(CampaignFlow flow)
        : base(flow)
    {
    }

    /// <inheritdoc/>
    public override CampaignScreen Screen => CampaignScreen.Cabin;

    /// <inheritdoc/>
    public override string Title => "CAMPAIGN CABIN";

    /// <inheritdoc/>
    public override int RowCount => Rows.Length;

    /// <summary>Opens on the row that works, so the placeholder is never a dead end.</summary>
    public override int OpeningRow => Rows.Length - 1;

    /// <inheritdoc/>
    public override string RowText(int row) => Rows[row];

    /// <inheritdoc/>
    public override string Detail(int row)
    {
        if (row == Rows.Length - 1)
        {
            return "Leaves the campaign for the main menu";
        }

        var profile = Flow.Profile;
        if (profile == null)
        {
            return "No player is selected";
        }

        string funds = profile.Funds.ToString("N0", CultureInfo.InvariantCulture);
        return $"{profile.Name}   ·   ${funds}   ·   {profile.MissionsCompleted} mission(s) completed";
    }

    /// <inheritdoc/>
    public override bool Accept(int row)
    {
        if (row == Rows.Length - 1)
        {
            Flow.Cancel();
            return true;
        }

        Flow.SetMessage($"{Rows[row]} has no screen yet.");
        return true;
    }
}
