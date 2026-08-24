using System.Collections.Generic;
using CSVM.Session;

namespace CSVM.UI;

/// <summary>
/// The scrapbook's finished-missions list (<c>Campaign CAP-41 Previous Mission *.png</c>): one row
/// per mission the profile has completed, ordered by <c>seq</c>, then VIEW SELECTED, REPLAY
/// MISSION and RETURN TO CABIN. The shots show a row as the long mission name over its area and
/// the plane flown; VIEW SELECTED has no screen of its own to open (its whole job is naming which
/// row REPLAY MISSION acts on), so it is a no-op that leaves the detail line already showing.
/// </summary>
public sealed class CampaignPreviousMissionsPage : CampaignPage
{
    // The row a mission-row press marks as the one REPLAY MISSION and VIEW SELECTED act on. -1
    // until the player has picked one; the two buttons then fall back to the first finished
    // mission, so a press before ever selecting still does something sensible.
    private int _selected = -1;

    /// <summary>Binds the page to its flow.</summary>
    public CampaignPreviousMissionsPage(CampaignFlow flow)
        : base(flow)
    {
    }

    /// <inheritdoc/>
    public override CampaignScreen Screen => CampaignScreen.PreviousMissions;

    /// <inheritdoc/>
    public override string Title => "PREVIOUS MISSIONS";

    /// <inheritdoc/>
    public override int RowCount => Seqs().Count + 3;

    /// <inheritdoc/>
    public override string RowText(int row)
    {
        var seqs = Seqs();
        if (row < seqs.Count)
        {
            string name = Flow.Strings.Text(3450 + seqs[row], $"Mission {seqs[row] + 1}");
            return row == _selected ? "✓ " + name : name;
        }

        return (row - seqs.Count) switch
        {
            0 => "VIEW SELECTED",
            1 => "REPLAY MISSION",
            _ => "RETURN TO CABIN",
        };
    }

    /// <inheritdoc/>
    public override string Detail(int row)
    {
        var seqs = Seqs();
        if (row < seqs.Count)
        {
            return MissionLine(seqs[row]);
        }

        return (row - seqs.Count) switch
        {
            0 or 1 when SelectedSeq(seqs) is { } seq => MissionLine(seq),
            0 or 1 => "Pick a mission first",
            _ => "Back to the cabin",
        };
    }

    /// <inheritdoc/>
    public override bool Accept(int row)
    {
        var seqs = Seqs();
        if (row < seqs.Count)
        {
            _selected = row;
            return true;
        }

        switch (row - seqs.Count)
        {
            case 0: // VIEW SELECTED: the detail line above already says everything it would.
                return true;
            case 1: // REPLAY MISSION
                if (SelectedSeq(seqs) is not { } seq)
                {
                    Flow.SetMessage("Select a mission first.");
                    return true;
                }

                Flow.SetMission(seq);
                Flow.GoTo(CampaignScreen.Briefing);
                return true;
            default: // RETURN TO CABIN
                Flow.GoTo(CampaignScreen.Cabin);
                return true;
        }
    }

    // The finished seqs, in story order, per CampaignProgression.CompletedSeqs. Read fresh every
    // call rather than cached: a replay recorded through the briefing/flight-check screens must
    // show up here the next time this page draws.
    private List<int> Seqs() =>
        Flow.Profile is { } profile ? CampaignProgression.CompletedSeqs(profile) : new List<int>();

    // The row the two buttons act on: the player's own pick, or the first finished mission when
    // nothing has been picked yet.
    private int? SelectedSeq(List<int> seqs)
    {
        if (seqs.Count == 0)
        {
            return null;
        }

        int index = _selected >= 0 && _selected < seqs.Count ? _selected : 0;
        return seqs[index];
    }

    // Long name over area and the plane that flew the best-of record, the CAP-41 layout.
    private string MissionLine(int seq)
    {
        string area = Flow.Strings.Text(1220 + (seq / 5), "");
        string plane = Flow.Profile is { } profile && CampaignProgression.ResultOf(profile, seq) is { } result
            ? result.Best.PlaneName
            : "";
        return string.IsNullOrEmpty(area) && string.IsNullOrEmpty(plane)
            ? string.Empty
            : $"{area}   ·   {plane}";
    }
}
