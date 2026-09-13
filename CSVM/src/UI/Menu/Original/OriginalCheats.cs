namespace CSVM.UI.Menu.Original;

/// <summary>
/// The three typed cheats of the Original presentation, the shell's partial over the
/// <c>gui_char</c> bodies of <c>PASSENGERCABIN.SCRIPT</c>, <c>SCRAPBOOK_TOC.SCRIPT</c> and
/// <c>PLANECONSTRUCTION.SCRIPT</c>. Each screen owns one <see cref="TypedCheat"/> and the authored
/// region a left click has to land in to give it the keyboard; no row of any screen lies inside
/// one of those regions, so the arm is read off the pointer before the hit test the way the
/// credits screen's own hidden line is. What a completed word switches on is
/// <see cref="CampaignCheats"/>, which both presentations read. The words, the regions and the
/// script lines behind them are in <c>docs/formats/campaign-screens.md</c>.
/// </summary>
public sealed partial class OriginalShell
{
    // PASSENGERCABIN.SCRIPT's own region = 5,336 to 85,479, the strip of cabin wall left of the
    // buttons. Inclusive on all four edges, which no reference shot can tell apart from exclusive.
    private const float CabinCheatLeft = 5f;
    private const float CabinCheatTop = 336f;
    private const float CabinCheatRight = 85f;
    private const float CabinCheatBottom = 479f;

    // SCRAPBOOK_TOC.SCRIPT's region = 42,426 to 136,523, under the contents list's own left edge.
    private const float ContentsCheatLeft = 42f;
    private const float ContentsCheatTop = 426f;
    private const float ContentsCheatRight = 136f;
    private const float ContentsCheatBottom = 523f;

    // PLANECONSTRUCTION.SCRIPT sets its region off the cash figure PX_T_CASH rather than authoring
    // one: x, y - 15 to x + Width, y + Height, which is 615,10 to 735,80 for the shipped row.
    private const float HubCheatLeft = 615f;
    private const float HubCheatTop = 10f;
    private const float HubCheatRight = 735f;
    private const float HubCheatBottom = 80f;

    private readonly TypedCheat _cabinCheat = new(CampaignCheats.MissionWord);
    private readonly TypedCheat _contentsCheat = new(CampaignCheats.GalleryWord);
    private readonly TypedCheat _hubCheat = new(CampaignCheats.CashWord);

    /// <summary>Whether a typed cheat holds the keyboard, which is what makes the seat's letters
    /// text rather than menu commands while one is being typed.</summary>
    public bool TypingCheat => _dialog == null && ScreenCheat is { Armed: true };

    // The latch of the screen showing, or null on every screen that carries none.
    private TypedCheat? ScreenCheat
    {
        get
        {
            if (_screen == OriginalScreen.CampaignCabin)
            {
                return _cabinCheat;
            }

            if (_screen == OriginalScreen.CampaignPreviousMissions)
            {
                return _contentsCheat;
            }

            return IsHub ? _hubCheat : null;
        }
    }

    private static bool Inside(MenuPointer pointer, float left, float top, float right, float bottom) =>
        pointer.X >= left && pointer.X <= right && pointer.Y >= top && pointer.Y <= bottom;

    // Whether the pointer stands inside the showing screen's cheat region.
    private bool InCheatRegion(MenuPointer pointer) => _screen switch
    {
        OriginalScreen.CampaignCabin => Inside(
            pointer, CabinCheatLeft, CabinCheatTop, CabinCheatRight, CabinCheatBottom),
        OriginalScreen.CampaignPreviousMissions => Inside(
            pointer, ContentsCheatLeft, ContentsCheatTop, ContentsCheatRight, ContentsCheatBottom),
        _ => IsHub && Inside(pointer, HubCheatLeft, HubCheatTop, HubCheatRight, HubCheatBottom),
    };

    // The screens' own lbutton_update: the PRIMARY button, since the secondary one belongs to the
    // credits line alone. A click inside the region takes the keyboard and a click anywhere else
    // gives it up, which is what focus does. The buffer survives either, because the script's own
    // string variable does.
    private bool ArmCheat(MenuPointer pointer)
    {
        if (ScreenCheat is not { } cheat || !pointer.Clicked)
        {
            return false;
        }

        return InCheatRegion(pointer) ? cheat.Arm() : cheat.Disarm();
    }

    // One frame of typing into an armed latch. The characters never reach the screen's own edit box
    // while this holds, since the script moved the caret here.
    private bool TypeCheat(MenuCommands commands)
    {
        if (ScreenCheat is not { Armed: true } cheat || _dialog != null)
        {
            return false;
        }

        bool changed = false;
        foreach (char c in commands.Typed)
        {
            changed = true;
            if (cheat.Type(c))
            {
                FireCheat(cheat);
            }
        }

        return changed;
    }

    // A completed word. The cabin shows its mission pull-down, the table of contents opens the
    // gallery, and the hub grants cash through the wallet the build is already priced against.
    private void FireCheat(TypedCheat cheat)
    {
        if (_campaign?.Cheats is not { } cheats)
        {
            return;
        }

        if (cheat == _cabinCheat)
        {
            cheats.ShowMissionList();
        }
        else if (cheat == _contentsCheat)
        {
            cheats.Reveal();
        }
        else if (_hangar?.Wallet is CampaignWallet wallet)
        {
            wallet.GrantCheatCash();
        }
    }

    // The mission NEXT MISSION launches, as a cm_sequence index: the pull-down's pick while
    // anything stands in the cabin's buffer, else the campaign's own position. Reading the buffer
    // empties it, so the next press is the ordinary one until the word is typed again.
    private int CheatedMission(int ordinary)
    {
        if (_campaign?.Cheats is not { } cheats || !_cabinCheat.Typed)
        {
            return ordinary;
        }

        _cabinCheat.ClearBuffer();
        return cheats.MissionPick > 0 ? cheats.MissionPick - 1 : ordinary;
    }

    // Every latch back to rest, which opening a screen does: the original creates each of these
    // widgets afresh every time it builds the screen they stand on.
    private void ResetCheats()
    {
        _cabinCheat.Reset();
        _contentsCheat.Reset();
        _hubCheat.Reset();
    }
}
