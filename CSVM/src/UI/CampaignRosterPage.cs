using CSVM.Session;

namespace CSVM.UI;

/// <summary>
/// The player-profile screen (<c>Campaign Player Profile.png</c>, <c>CAMPAIGN.SCRIPT</c>): a name
/// field over the roster, then CONTINUE, DELETE PLAYER and CANCEL. CONTINUE creates the named
/// profile or continues the one that exists and opens the cabin, which is the original's own commit
/// path from the button, from Enter in the box and from a double-click on a roster row alike. A
/// roster row selects on the first confirm and continues on a second, the launchscreen's
/// double-enter idiom, so no press does two things at once. Deleting is a separate confirmed stage
/// and takes only the profile's own directory, never a hangar plane.
/// </summary>
public sealed class CampaignRosterPage : CampaignPage
{
    private readonly CampaignTextEntry _entry = new();

    // The delete confirm's own stage, drawn over the list rather than beside it, so the press that
    // removes a profile names the profile it removes.
    private bool _confirming;

    // The roster row the player picked, "" for none. A pick fills the name field, so the field is
    // what every action below actually reads.
    private string _selected = string.Empty;

    /// <summary>Binds the page to its flow.</summary>
    public CampaignRosterPage(CampaignFlow flow)
        : base(flow)
    {
    }

    /// <inheritdoc/>
    public override CampaignScreen Screen => CampaignScreen.Roster;

    /// <summary>The original's screen carries no title text of its own; this names it for the
    /// board chrome, which heads every screen.</summary>
    public override string Title => "SELECT PLAYER";

    /// <inheritdoc/>
    public override CampaignTextEntry? TextEntry => _entry;

    /// <summary>The name field, one row per stored profile, then the three buttons. The confirm
    /// stage replaces all of it with its two answers.</summary>
    public override int RowCount => _confirming ? 2 : Flow.Roster.Count + 4;

    /// <inheritdoc/>
    public override string Footer
    {
        get
        {
            if (_confirming)
            {
                return "↑↓  Choose       Enter / A  Answer       Esc / B  Keep";
            }

            return _entry.Active
                ? "Type a name       ←→  Letter       ↑↓  Add / remove       Enter / A  Continue       Esc / B  Done"
                : "↑↓  Choose       Enter / A  Select       Esc / B  Back";
        }
    }

    private int ContinueRow => Flow.Roster.Count + 1;

    private int DeleteRow => Flow.Roster.Count + 2;

    /// <inheritdoc/>
    public override string RowText(int row)
    {
        if (_confirming)
        {
            return row == 0 ? $"Delete {_entry.Text}" : $"Keep {_entry.Text}";
        }

        if (row == 0)
        {
            return "Name:  " + (_entry.Display.Length > 0 ? _entry.Display : "(none)");
        }

        if (row <= Flow.Roster.Count)
        {
            string name = Flow.Roster[row - 1];
            return name == _selected ? "✓ " + name : name;
        }

        return row == ContinueRow ? "CONTINUE" : row == DeleteRow ? "DELETE PLAYER" : "CANCEL";
    }

    /// <summary>The three plaques the panel carries. The delete confirm replaces the whole screen
    /// with its two answers, so it draws no button at all and the answers stay list rows.</summary>
    public override BoardButtonRef Button(int row)
    {
        if (_confirming)
        {
            return BoardButtonRef.None;
        }

        if (row == ContinueRow)
        {
            return new BoardButtonRef(BoardButton.Continue);
        }

        if (row == DeleteRow)
        {
            return new BoardButtonRef(BoardButton.DeletePlayer);
        }

        return row == DeleteRow + 1 ? new BoardButtonRef(BoardButton.CancelProfile) : BoardButtonRef.None;
    }

    /// <inheritdoc/>
    public override string Detail(int row)
    {
        if (_confirming)
        {
            return Flow.Strings.Text(201,
                "Are you sure you want to delete this player and all associated saved games?");
        }

        if (row == 0)
        {
            return _entry.Active
                ? "Letters, digits and spaces, up to 32 characters"
                : "Enter / A to type a player name";
        }

        if (row <= Flow.Roster.Count)
        {
            return Flow.Roster[row - 1] == _selected
                ? "Enter / A again to fly this player's campaign"
                : "Enter / A selects this player";
        }

        return row == ContinueRow ? "Creates the named player, or continues the one that exists"
            : row == DeleteRow ? "Removes the named player and its campaign; hangar planes stay"
            : "Back to the main menu";
    }

    /// <inheritdoc/>
    public override bool Accept(int row)
    {
        if (_confirming)
        {
            return AcceptConfirm(row);
        }

        if (row == 0)
        {
            if (!_entry.Active)
            {
                _entry.Arm();
                return true;
            }

            return Continue();
        }

        if (row <= Flow.Roster.Count)
        {
            string name = Flow.Roster[row - 1];
            if (name == _selected)
            {
                return Continue();
            }

            _selected = name;
            _entry.Set(name);
            return true;
        }

        if (row == ContinueRow)
        {
            return Continue();
        }

        if (row == DeleteRow)
        {
            return BeginDelete();
        }

        Flow.Cancel();
        return true;
    }

    /// <inheritdoc/>
    public override bool Back()
    {
        if (_confirming)
        {
            _confirming = false;
            Flow.FocusRow(DeleteRow);
            return true;
        }

        if (_entry.Active)
        {
            _entry.Disarm();
            return true;
        }

        return false;
    }

    // The screen's one commit path: validate the name in the original's own words, create the
    // profile when it is new, and open the cabin on it.
    private bool Continue()
    {
        string name = _entry.Text.Trim();
        if (name.Length == 0)
        {
            Flow.SetMessage(Flow.Strings.Text(200, "You must enter a player name."));
            return true;
        }

        if (!CampaignTextEntry.Valid(name))
        {
            Flow.SetMessage(Refusal(name));
            return true;
        }

        var profile = Flow.Store.Load(name);
        if (profile == null)
        {
            if (Flow.Roster.Count >= CampaignFlow.MaxProfiles)
            {
                string full = Flow.Strings.Format(202, CampaignFlow.MaxProfiles);
                Flow.SetMessage(full.Length > 0
                    ? full
                    : $"Crimson Skies supports only {CampaignFlow.MaxProfiles} active players. " +
                      "To add a new player, delete a player from the list.");
                return true;
            }

            profile = CampaignProfileDef.NewProfile(name);
            Flow.Store.Save(profile);
            Flow.RefreshRoster();
        }

        _entry.Disarm();
        _selected = name;
        Flow.SelectProfile(profile);
        return true;
    }

    // Which refusal a rejected name earns: too long has its own string (langui 212), anything else
    // is the character rule (707). Both are the original's, and both fall back to their own words.
    private string Refusal(string name)
    {
        if (name.Length <= CampaignTextEntry.MaxLength)
        {
            return Flow.Strings.Text(707,
                "Your player name is limited to alphabetic and numeric characters and spaces.");
        }

        string limit = Flow.Strings.Format(212, CampaignTextEntry.MaxLength);
        return limit.Length > 0
            ? limit
            : $"Your player name is limited to {CampaignTextEntry.MaxLength} characters.";
    }

    // Opens the confirm, on a profile that exists. The cursor lands on the keep answer: the press
    // that follows a mistaken DELETE PLAYER must not be the one that destroys a campaign.
    private bool BeginDelete()
    {
        string name = _entry.Text.Trim();
        if (name.Length == 0 || Flow.Store.Load(name) == null)
        {
            Flow.SetMessage($"There is no player named \"{name}\".");
            return true;
        }

        _confirming = true;
        Flow.FocusRow(1);
        return true;
    }

    // The confirm's own press. Deleting removes the profile's directory alone, so the planes the
    // profile referenced by name are still in the hangar afterwards.
    private bool AcceptConfirm(int row)
    {
        if (row == 0)
        {
            string name = _entry.Text.Trim();
            Flow.Store.Delete(name);
            Flow.RefreshRoster();
            if (_selected == name)
            {
                _selected = string.Empty;
            }

            _entry.Set(string.Empty);
        }

        _confirming = false;
        Flow.FocusRow(0);
        return true;
    }
}
