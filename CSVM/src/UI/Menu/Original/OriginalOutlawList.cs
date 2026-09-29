using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Net;
using CSVM.UI.Boards;

namespace CSVM.UI.Menu.Original;

/// <summary>The outlaw list's five sub-tabs, in the order its pane draws them.</summary>
public enum OutlawPage
{
    /// <summary>The eleven stock airframes, four rows at a time under a scroll bar.</summary>
    Airframes,

    /// <summary>The one nitro-boosted engines box.</summary>
    Engines,

    /// <summary>The five gun calibres.</summary>
    Guns,

    /// <summary>Outlaw All Ammo over the four ammunitions.</summary>
    Ammo,

    /// <summary>Outlaw All Rockets over the eleven rockets, four rows at a time.</summary>
    Rockets,
}

/// <summary>
/// The <see cref="NetPlaneRules"/> flag each outlaw list row writes and the string naming it, in
/// the original's row order (<c>docs/org/multiplayer-messages.md</c>, "The outlaw list
/// screen"). An ammunition or rocket row reads ticked while its page's Outlaw All is set, and a
/// click on it then changes nothing.
/// </summary>
public static class OutlawRows
{
    /// <summary>How many rows a scrolled page shows at once.</summary>
    public const int Window = 4;

    /// <summary>How many rows a page carries.</summary>
    public static int Count(OutlawPage page) => page switch
    {
        OutlawPage.Airframes => 11,
        OutlawPage.Engines => 1,
        OutlawPage.Guns => 5,
        OutlawPage.Ammo => 4,
        _ => 11,
    };

    /// <summary>The flag row <paramref name="row"/> of a page writes, or -1 past its rows.</summary>
    public static int Flag(OutlawPage page, int row) => row < 0 || row >= Count(page)
        ? -1
        : page switch
        {
            OutlawPage.Airframes => NetPlaneRules.AirframeFlag + row,
            OutlawPage.Engines => NetPlaneRules.NitroFlag,
            OutlawPage.Guns => NetPlaneRules.GunFlag + row,
            OutlawPage.Ammo => NetPlaneRules.AmmoFlag + row,
            _ => NetPlaneRules.RocketFlag + row,
        };

    /// <summary>The page's Outlaw All flag, or -1 on a page without that box.</summary>
    public static int AllFlag(OutlawPage page) => page switch
    {
        OutlawPage.Ammo => NetPlaneRules.AllAmmoFlag,
        OutlawPage.Rockets => NetPlaneRules.AllRocketsFlag,
        _ => -1,
    };

    /// <summary>The string that names row <paramref name="row"/> of a page.</summary>
    public static int NameId(OutlawPage page, int row) => page switch
    {
        OutlawPage.Airframes => 3000 + row,
        OutlawPage.Engines => 10135,
        OutlawPage.Guns => 3320 + row,
        OutlawPage.Ammo => 3350 + row,
        _ => 3380 + row,
    };

    /// <summary>Whether a page scrolls: Airframes and Rockets show four rows under a scroll bar,
    /// and the Guns page all five of its own.</summary>
    public static bool Scrolls(OutlawPage page) => page is OutlawPage.Airframes or OutlawPage.Rockets;

    /// <summary>How many rows a page shows at once.</summary>
    public static int Shown(OutlawPage page) => Scrolls(page) ? Window : Count(page);

    /// <summary>Whether a row draws ticked: its own flag, or its page's Outlaw All.</summary>
    public static bool Ticked(NetPlaneRules rules, OutlawPage page, int row) =>
        rules.Has(Flag(page, row)) || rules.Has(AllFlag(page));

    /// <summary>Whether a click on a row writes its flag. It does not while the page's Outlaw All is
    /// set, as the original's callbacks 5018 and 5064 read.</summary>
    public static bool Takes(NetPlaneRules rules, OutlawPage page, int row) =>
        Flag(page, row) >= 0 && !rules.Has(AllFlag(page));
}

/// <summary>
/// The Multiplayer Lobby's outlaw list, the pane Select... opens over the tab page while the player
/// list, Ready and the chat stay live. Every tick goes through
/// <see cref="DogfightLobby.SetOutlawed"/>, which starts a new round and sends the list to every
/// guest. Cancel puts back the list the host opened with; Accept keeps it. A guest's View... and a
/// Ready host's Select... open it read-only. The decode is in <c>docs/org/multiplayer-messages.md</c>.
/// </summary>
internal sealed class OriginalOutlawList
{
    /// <summary>The key prefix every one of the pane's rows carries.</summary>
    internal const string Prefix = "MPL_OUT_";

    /// <summary>Accept, the host's alone.</summary>
    internal const string AcceptKey = Prefix + "B_ACCEPT";

    /// <summary>Cancel, which closes the pane on every end.</summary>
    internal const string CancelKey = Prefix + "B_CANCEL";

    /// <summary>The Ammo and Rockets pages' half-size Outlaw All box.</summary>
    internal const string AllKey = Prefix + "C_ALL";

    /// <summary>The scroll bar's up arrow.</summary>
    internal const string UpKey = Prefix + "B_UP";

    /// <summary>The scroll bar's down arrow.</summary>
    internal const string DownKey = Prefix + "B_DOWN";

    private const string RowPrefix = Prefix + "C_ROW_";
    private const string TabPrefix = Prefix + "TAB_";
    private const string PaneArt = "MP_LOBBY_OUTLAWED.PNG";
    private const string BoxArt = "MP_B_READYCHECKBOX8STATES.PNG";
    private const string MediumArt = "MP_B_MEDIUM.PNG";
    private const string UpArt = "MP_B_SCROLLUP.PNG";
    private const string DownArt = "MP_B_SCROLLDOWN.PNG";
    private const string ThumbArt = "MP_B_SCROLLBAR.PNG";

    // The pane's corner, which is the tab page's, and the scroll bar the long pages carry.
    private const float PaneX = 314f;
    private const float PaneY = 26f;
    private const float ScrollX = PaneX + 393f;
    private const float ScrollY = PaneY + 104f;
    private const float ScrollHeight = 167f;
    private const float ArrowWidth = 16f;
    private const float ArrowHeight = 11f;

    private static readonly string[] TabArt =
        { "MP_LOBBY_TABMEDIUM.PNG", "MP_LOBBY_TABSMALL.PNG", "MP_LOBBY_TABSMALL.PNG", "MP_LOBBY_TABSMALL.PNG", "MP_LOBBY_TABSMALL.PNG" };

    private static readonly float[] TabX = { 46f, 127f, 200f, 268f, 336f };
    private static readonly float[] TabWidth = { 80f, 69f, 69f, 69f, 69f };
    private static readonly string[] TabNames = { "Airframes", "Engines", "Guns", "Ammo", "Rockets" };
    private static readonly string[] Airframes =
    {
        "Ford Hoplite", "Focke-Wulf Fw 193 Hellhound", "Bristol Type 140 Balmoral", "Hughes Bloodhawk",
        "Fairchild F6II Brigand", "Hughes P21-J MKIII Devastator", "Hughes-Lockheed Firebrand", "Curtiss-Wright J2 Fury",
        "McDonnell S2B Kestrel", "William & Colt Peacemaker 370", "Curtiss-Wright P2 Warhawk",
    };

    private static readonly string[] Ammunitions = { "Slugs", "Dum-Dum Bullets", "Armor-Piercing Bullets", "Explosive Bullets" };
    private static readonly string[] Rockets =
    {
        "Armor-Piercing Rockets", "High-Explosive Rockets", "Flak Rockets", "Sonic Rockets", "Flash Rockets",
        "Rear Flash Rockets", "Smoke Screen", "Choker Rockets", "Beeper Rockets", "Seeker Rockets", "Aerial Torpedoes",
    };

    // The pane scripts' colours: black labels, the picked sub-tab's red and the plaques' rollover blue.
    private static readonly BoardTint Black = new(0, 0, 0);
    private static readonly BoardTint Picked = new(142, 0, 0);
    private static readonly BoardTint Rollover = new(102, 207, 255);

    private readonly MultiplayerBoardText _text;
    private ulong _kept;
    private bool _held;
    private int _top;

    /// <summary>A pane writing in <paramref name="text"/>'s words and faces.</summary>
    internal OriginalOutlawList(MultiplayerBoardText text) => _text = text;

    /// <summary>The pane's own art, drawn where the tab page stands.</summary>
    internal static BoardPicture Pane => new(new BoardArt(BoardArtLibrary.Ui, PaneArt), PaneX, PaneY);

    /// <summary>Whether the pane stands over the tab page.</summary>
    internal bool IsOpen { get; private set; }

    /// <summary>The sub-tab showing.</summary>
    internal OutlawPage Page { get; private set; }

    /// <summary>The key of a page's row by its index in the whole page, not the window.</summary>
    internal static string RowKey(int row) => RowPrefix + row.ToString(CultureInfo.InvariantCulture);

    /// <summary>A sub-tab's key.</summary>
    internal static string TabKey(OutlawPage page) => TabPrefix + ((int)page).ToString(CultureInfo.InvariantCulture);

    /// <summary>Whether a key is one of the pane's rows.</summary>
    internal static bool Owns(string key) => key.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>Opens the pane on Airframes. A host keeps the list as it stands for Cancel, which is
    /// the original's callback 5050.</summary>
    internal void Open(DogfightLobby lobby)
    {
        IsOpen = true;
        Page = OutlawPage.Airframes;
        _top = 0;
        _held = lobby.IsHost;
        _kept = lobby.Rules.Outlawed;
    }

    /// <summary>Takes the pane down with the list as it stands.</summary>
    internal void Close()
    {
        IsOpen = false;
        _held = false;
    }

    /// <summary>Shows a sub-tab directly, the screenshot aids' door.</summary>
    internal void Show(OutlawPage page)
    {
        Page = page;
        _top = 0;
    }

    /// <summary>The pane's rows: the sub-tabs, the showing page's boxes, the scroll arrows, then
    /// Accept for the host and Cancel.</summary>
    internal void Widgets(DogfightLobby lobby, List<OriginalRow> rows)
    {
        for (int i = 0; i < TabX.Length; i++)
        {
            rows.Add(_text.Strip(TabKey((OutlawPage)i), TabArt[i], PaneX + TabX[i], PaneY + 63f, true, 1, TabWidth[i], 23f));
        }

        bool live = Live(lobby);
        var box = new BoardArt(BoardArtLibrary.Ui, BoxArt, 8);
        if (OutlawRows.AllFlag(Page) >= 0)
        {
            rows.Add(new OriginalRow(AllKey, string.Empty, OriginalRowKind.Radio, PaneX + 95f, PaneY + 86f, 200f, 13f, live, 1, box));
        }

        int count = OutlawRows.Count(Page);
        _top = Math.Clamp(_top, 0, Math.Max(0, count - OutlawRows.Shown(Page)));
        for (int row = _top; row < count && row < _top + OutlawRows.Shown(Page); row++)
        {
            var (x, y) = RowAt(Page, row - _top);
            rows.Add(new OriginalRow(RowKey(row), string.Empty, OriginalRowKind.Radio, x, y, 290f, 26f, live, 1, box));
        }

        if (OutlawRows.Scrolls(Page))
        {
            rows.Add(new OriginalRow(UpKey, string.Empty, OriginalRowKind.Button, ScrollX, ScrollY, ArrowWidth, ArrowHeight,
                _top > 0, 1, new BoardArt(BoardArtLibrary.Ui, UpArt, 4)));
            rows.Add(new OriginalRow(DownKey, string.Empty, OriginalRowKind.Button, ScrollX, ScrollY + ScrollHeight - ArrowHeight,
                ArrowWidth, ArrowHeight, _top + OutlawRows.Window < count, 1, new BoardArt(BoardArtLibrary.Ui, DownArt, 4)));
        }

        // A guest's pane has no Accept at all, and a Ready host's draws it greyed.
        if (lobby.IsHost)
        {
            rows.Add(_text.Strip(AcceptKey, MediumArt, PaneX + 200f, PaneY + 280f, !lobby.Ready, 1, 96f, 37f));
        }

        rows.Add(_text.Strip(CancelKey, MediumArt, PaneX + 300f, PaneY + 280f, true, 1, 96f, 37f));
    }

    /// <summary>The long pages' list window, for the pointer's wheel and thumb.</summary>
    internal void Lists(List<OriginalList> lists)
    {
        if (IsOpen && Window() is { } window)
        {
            lists.Add(new OriginalList(RowPrefix, window, top => _top = top));
        }
    }

    /// <summary>The pane's answer to one of its rows. A tick outside a live pane, or on a row its
    /// page's Outlaw All covers, changes nothing.</summary>
    internal void Activate(DogfightLobby? lobby, string key)
    {
        if (key.StartsWith(TabPrefix, StringComparison.Ordinal))
        {
            Show((OutlawPage)(OriginalWidgets.Indexed(key, TabPrefix) ?? 0));
            return;
        }

        switch (key)
        {
            case UpKey:
                _top = Math.Max(0, _top - 1);
                return;
            case DownKey:
                _top = Math.Min(Math.Max(0, OutlawRows.Count(Page) - OutlawRows.Window), _top + 1);
                return;
            case AcceptKey:
                Close();
                return;
            case CancelKey:
                Cancel(lobby);
                return;
        }

        if (lobby == null || !Live(lobby))
        {
            return;
        }

        var rules = lobby.Rules;
        if (key == AllKey)
        {
            int all = OutlawRows.AllFlag(Page);
            lobby.SetOutlawed(all, !rules.Has(all));
        }
        else if (OriginalWidgets.Indexed(key, RowPrefix) is { } row && OutlawRows.Takes(rules, Page, row))
        {
            int flag = OutlawRows.Flag(Page, row);
            lobby.SetOutlawed(flag, !rules.Has(flag));
        }
    }

    /// <summary>The pane's words and scroll bar beside its widgets.</summary>
    internal void ComposePage(BoardLayers layers)
    {
        layers.Lines.Add(_text.Line(10136, "Outlawed Components", PaneX + 15f, PaneY + 45f, 0f, Black));
        if (Window() is not { } window)
        {
            return;
        }

        // The scroll control's KF colour, 0xff282418, under the thumb.
        layers.Fills.Add(new BoardFill(ScrollX, window.TrackTop, ArrowWidth, window.TrackHeight, 0x28, 0x24, 0x18));
        layers.Pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, ThumbArt), window.ThumbX, window.ThumbY,
            Height: window.ThumbHeight));
    }

    /// <summary>One of the pane's widgets in its state, with the words the scripts write beside it.</summary>
    internal void ComposeWidget(DogfightLobby lobby, OriginalRow row, bool focused, bool pressed, BoardLayers layers)
    {
        if (row.Kind == OriginalRowKind.Radio)
        {
            ComposeBox(lobby.Rules, row, focused, pressed, layers);
            return;
        }

        int frame = !row.Enabled ? 0 : ComposedBoard.PlaqueFrame(row.Art!.Frames, focused, pressed);
        if (row.Key.StartsWith(TabPrefix, StringComparison.Ordinal))
        {
            int page = OriginalWidgets.Indexed(row.Key, TabPrefix) ?? 0;
            bool picked = page == (int)Page;
            layers.Pictures.Add(new BoardPicture(row.Art!, row.X, row.Y, picked ? 3 : frame));
            Label(row, 10130 + page, TabNames[page], picked ? Picked : Black, layers);
            return;
        }

        layers.Pictures.Add(new BoardPicture(row.Art!, row.X, row.Y, frame));
        if (row.Key is AcceptKey or CancelKey)
        {
            var tint = !row.Enabled ? MultiplayerBoardText.LabelDisabled
                : pressed ? MultiplayerBoardText.LabelPressed
                : focused ? Rollover
                : MultiplayerBoardText.LabelNormal;
            Label(row, row.Key == AcceptKey ? 10540 : 10541, row.Key == AcceptKey ? "Accept" : "Cancel", tint, layers);
        }
    }

    // Live only on the host while it is not Ready, which is the scripts' ($$OX$$) and (!GDA).
    private static bool Live(DogfightLobby lobby) => lobby.IsHost && !lobby.Ready;

    // Where the shown-th box of a page stands: the Guns page packs its five rows tighter.
    private static (float X, float Y) RowAt(OutlawPage page, int shown) => page switch
    {
        OutlawPage.Engines => (PaneX + 100f, PaneY + 150f),
        OutlawPage.Guns => (PaneX + 85f, PaneY + 110f + (32f * shown)),
        _ => (PaneX + 85f, PaneY + 120f + (36f * shown)),
    };

    private static string Fallback(OutlawPage page, int row) => page switch
    {
        OutlawPage.Airframes => Airframes[row],
        OutlawPage.Engines => "Outlaw Nitro-Boosted Engines",
        OutlawPage.Guns => $".{30 + (10 * row)}-cal.",
        OutlawPage.Ammo => Ammunitions[row],
        _ => Rockets[row],
    };

    private void Cancel(DogfightLobby? lobby)
    {
        // Callback 5051 puts the kept list back, only on a host that is not Ready.
        if (_held && lobby is { IsHost: true, Ready: false })
        {
            var now = lobby.Rules;
            for (int flag = 0; flag < NetPlaneRules.Flags; flag++)
            {
                bool kept = (_kept & (1UL << flag)) != 0;
                if (now.Has(flag) != kept)
                {
                    lobby.SetOutlawed(flag, kept);
                }
            }
        }

        Close();
    }

    private ListWindow? Window()
    {
        if (!OutlawRows.Scrolls(Page))
        {
            return null;
        }

        int count = OutlawRows.Count(Page);
        float track = ScrollHeight - (2f * ArrowHeight);
        float thumb = ListWindow.ThumbHeightFor(track, OutlawRows.Window, count, ArrowHeight);
        return new ListWindow(
            PaneX + 85f, ScrollY, ScrollX + ArrowWidth - (PaneX + 85f), ScrollHeight,
            ScrollX, ListWindow.ThumbYFor(ScrollY + ArrowHeight, track, thumb, _top, count - OutlawRows.Window), ArrowWidth, thumb,
            ScrollY + ArrowHeight, track, count, OutlawRows.Window, _top);
    }

    private void ComposeBox(NetPlaneRules rules, OriginalRow row, bool focused, bool pressed, BoardLayers layers)
    {
        int state = !row.Enabled ? 0 : pressed ? 3 : focused ? 2 : 1;
        if (row.Key == AllKey)
        {
            // The scripts draw this box at half size: scale(TD) = 50,50,100.
            bool all = rules.Has(OutlawRows.AllFlag(Page));
            layers.Pictures.Add(new BoardPicture(row.Art!, row.X, row.Y, (all ? 4 : 0) + state, Width: 24f, Height: 13f));
            int id = Page == OutlawPage.Ammo ? 10137 : 10138;
            layers.Lines.Add(_text.Line(id, Page == OutlawPage.Ammo ? "Outlaw All Ammo" : "Outlaw All Rockets",
                row.X + 40f, row.Y - 2f, 0f, Black));
            return;
        }

        int index = OriginalWidgets.Indexed(row.Key, RowPrefix) ?? 0;
        bool ticked = OutlawRows.Ticked(rules, Page, index);
        layers.Pictures.Add(new BoardPicture(row.Art!, row.X, row.Y, (ticked ? 4 : 0) + state));
        layers.Lines.Add(_text.Line(OutlawRows.NameId(Page, index), Fallback(Page, index), row.X + 60f, row.Y + 4f, 0f, Black));
    }

    private void Label(OriginalRow row, int id, string word, BoardTint tint, BoardLayers layers)
    {
        var face = _text.Regular(id);
        float size = face?.Pixels ?? MultiplayerBoardText.TextFallback;
        layers.Lines.Add(new BoardLine(_text.Word(id, word), row.X, row.Y + ((row.Height - size) / 2f) - 1f, row.Width, size,
            BoardInk.Row, -1, Justify: BoardJustify.Center, Face: face, Colour: tint));
    }
}
