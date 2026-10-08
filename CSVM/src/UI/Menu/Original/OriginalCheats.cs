using System;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The three typed cheats of the Original presentation, over the <c>gui_char</c> bodies of
/// <c>PASSENGERCABIN.SCRIPT</c>, <c>SCRAPBOOK_TOC.SCRIPT</c> and <c>PLANECONSTRUCTION.SCRIPT</c>.
/// Each screen owns one <see cref="TypedCheat"/> and the authored region a left click has to land
/// in to give it the keyboard. No row of any screen lies inside one of those regions, so the shell
/// reads the arm off the pointer before its hit test. What a completed word switches on is
/// <see cref="CampaignCheats"/>, which both presentations read. The words, the regions and the
/// script lines behind them are in <c>docs/formats/campaign-screens.md</c>.
/// </summary>
public sealed class OriginalCheats
{
    // PASSENGERCABIN.SCRIPT's own region = 5,336 to 85,479, the strip of cabin wall left of the
    // buttons. Inclusive on all four edges, which no reference shot can tell apart from exclusive.
    private const float CabinLeft = 5f;
    private const float CabinTop = 336f;
    private const float CabinRight = 85f;
    private const float CabinBottom = 479f;

    // SCRAPBOOK_TOC.SCRIPT's region = 42,426 to 136,523, under the contents list's own left edge.
    private const float ContentsLeft = 42f;
    private const float ContentsTop = 426f;
    private const float ContentsRight = 136f;
    private const float ContentsBottom = 523f;

    // PLANECONSTRUCTION.SCRIPT sets its region off the cash figure PX_T_CASH rather than authoring
    // one: x, y - 15 to x + Width, y + Height, which is 615,10 to 735,80 for the shipped row.
    private const float HubLeft = 615f;
    private const float HubTop = 10f;
    private const float HubRight = 735f;
    private const float HubBottom = 80f;

    private readonly TypedCheat _cabin = new(CampaignCheats.MissionWord);
    private readonly TypedCheat _contents = new(CampaignCheats.GalleryWord);
    private readonly TypedCheat _hub = new(CampaignCheats.CashWord);
    private readonly OriginalCampaignScreen _campaign;
    private readonly OriginalHangarScreen? _hangar;

    /// <summary>The cheats over the two modules whose screens carry them: the campaign's cabin and
    /// contents, and the hangar's plane-construction hub where a hangar stands.</summary>
    public OriginalCheats(OriginalCampaignScreen campaign, OriginalHangarScreen? hangar)
    {
        _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
        _hangar = hangar;
    }

    // Whether the plane-construction hub is showing, which is the hangar module's own reading: the
    // cash figure PLANECONSTRUCTION.SCRIPT sets its region off stands on the tabs and the purchase
    // page and nowhere else. The module answers it; it learns nothing about the cheat.
    private bool OnHub => _hangar?.IsHub ?? false;

    /// <summary>Whether the latch of <paramref name="screen"/> holds the keyboard, which is what
    /// makes the seat's letters text rather than menu commands while one is being typed.</summary>
    public bool Typing(OriginalScreen screen) => Latch(screen) is { Armed: true };

    /// <summary>The screens' own <c>lbutton_update</c>: the PRIMARY button, since the secondary one
    /// belongs to the credits line alone. A click inside the region takes the keyboard and a click
    /// anywhere else gives it up, which is what focus does. The buffer survives either, because the
    /// script's own string variable does. Answers whether the latch changed.</summary>
    public bool Arm(OriginalScreen screen, MenuPointer pointer)
    {
        if (Latch(screen) is not { } cheat || !pointer.Clicked)
        {
            return false;
        }

        return InRegion(screen, pointer) ? cheat.Arm() : cheat.Disarm();
    }

    /// <summary>One frame of typing into the armed latch of <paramref name="screen"/>, answering
    /// whether anything was typed. The characters never reach the screen's own edit box while this
    /// holds, since the script moved the caret here.</summary>
    public bool Type(OriginalScreen screen, MenuCommands commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (Latch(screen) is not { Armed: true } cheat)
        {
            return false;
        }

        bool changed = false;
        foreach (char c in commands.Typed)
        {
            changed = true;
            if (cheat.Type(c))
            {
                Fire(cheat);
            }
        }

        return changed;
    }

    /// <summary>The mission NEXT MISSION launches, as a <c>cm_sequence</c> index: the pull-down's
    /// pick while anything stands in the cabin's buffer, else <paramref name="ordinary"/>. Reading
    /// the buffer empties it, so the next press is the ordinary one until the word is typed again.</summary>
    public int Mission(int ordinary)
    {
        if (_campaign.Cheats is not { } cheats || !_cabin.Typed)
        {
            return ordinary;
        }

        _cabin.ClearBuffer();
        return cheats.MissionPick > 0 ? cheats.MissionPick - 1 : ordinary;
    }

    /// <summary>Every latch back to rest, which opening a screen does. The original creates each of
    /// these widgets afresh every time it builds the screen they stand on.</summary>
    public void Reset()
    {
        _cabin.Reset();
        _contents.Reset();
        _hub.Reset();
    }

    private static bool Inside(MenuPointer pointer, float left, float top, float right, float bottom) =>
        pointer.X >= left && pointer.X <= right && pointer.Y >= top && pointer.Y <= bottom;

    // The latch of a screen, or null on every screen that carries none.
    private TypedCheat? Latch(OriginalScreen screen)
    {
        if (screen == OriginalScreen.CampaignCabin)
        {
            return _cabin;
        }

        if (screen == OriginalScreen.CampaignPreviousMissions)
        {
            return _contents;
        }

        return OnHub ? _hub : null;
    }

    // Whether the pointer stands inside the screen's cheat region.
    private bool InRegion(OriginalScreen screen, MenuPointer pointer) => screen switch
    {
        OriginalScreen.CampaignCabin => Inside(pointer, CabinLeft, CabinTop, CabinRight, CabinBottom),
        OriginalScreen.CampaignPreviousMissions => Inside(pointer, ContentsLeft, ContentsTop, ContentsRight, ContentsBottom),
        _ => OnHub && Inside(pointer, HubLeft, HubTop, HubRight, HubBottom),
    };

    // A completed word. The cabin shows its mission pull-down, the table of contents opens the
    // gallery, and the hub grants cash through the wallet the build is already priced against.
    private void Fire(TypedCheat cheat)
    {
        if (_campaign.Cheats is not { } cheats)
        {
            return;
        }

        if (cheat == _cabin)
        {
            cheats.ShowMissionList();
        }
        else if (cheat == _contents)
        {
            cheats.Reveal();
        }
        else if (_hangar?.OpenWallet is CampaignWallet wallet)
        {
            wallet.GrantCheatCash();
        }
    }
}
