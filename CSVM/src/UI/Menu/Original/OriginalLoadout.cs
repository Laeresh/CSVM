using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The Weapon Loadout screen, the shell's partial over the decoded <c>[@OrdinanceLayout@]</c>
/// section (the campaign's own ammo selection chrome) and one aeroplane's fit. Two doors open it,
/// each returning to the screen it came from: the Instant Action strip over the seat its radio pair
/// names (seat 0's <see cref="LoadoutChoice"/> or the feature's wingman fit) and the per-seat
/// picker's WEAPON LOADOUT over the picking seat's own <see cref="PlayerSeat.Fit"/>. The four
/// ammunition fields stand for the airframe's gun slots and the eight rocket fields for its pylons
/// (the left column pylons 1 to 4, the right 5 to 8), each over the stock table's option list; a
/// slot or pylon the airframe lacks draws no field. The fit is edited in place, ACCEPT LOADOUT keeps
/// the picks and CANCEL LOADOUT or Back restores the picks it opened on, the original's working copy
/// read onto a shared choice. Remake-only: the original reaches this section from the flight check.
/// </summary>
public sealed partial class OriginalShell
{
    /// <summary>The layout section the loadout screen is composed from.</summary>
    public const string LoadoutSection = "OrdinanceLayout";

    /// <summary>The screen's ACCEPT LOADOUT button, keeping the picks.</summary>
    public const string LoadoutAcceptKey = "OL_B_ACCEPT";

    /// <summary>The screen's CANCEL LOADOUT button, restoring the picks it opened on.</summary>
    public const string LoadoutCancelKey = "OL_B_CANCEL";

    /// <summary>The four ammunition fields, <c>OL_D_AMMO0..3</c>, one per gun slot.</summary>
    public const string LoadoutAmmoPrefix = "OL_D_AMMO";

    /// <summary>The eight rocket fields, <c>OL_D_ROCKETS0..7</c>, one per pylon.</summary>
    public const string LoadoutRocketPrefix = "OL_D_ROCKETS";

    // Text sizes against the section's 15-pixel item height and its text rows.
    private const float LoadoutTitleFont = 20f;
    private const float LoadoutHeadingFont = 15f;
    private const float LoadoutCaptionFont = 11f;
    private const float LoadoutItemFont = 12f;

    // The string ids the campaign's ammo page reads: the calibre words, the no-gun marker, and the
    // two description families indexed by option row (docs/formats/campaign-screens.md).
    private const int CalibreLabel = 3320;
    private const int NoGunLabel = 3315;
    private const int AmmoTitleLabel = 3350;
    private const int AmmoBodyLabel = 3370;
    private const int RocketTitleLabel = 3380;
    private const int RocketBodyLabel = 3410;

    private readonly string?[] _loadoutBefore = new string?[LoadoutChoice.MaxGunSlot + LoadoutChoice.MaxPylon];
    private LoadoutChoice? _loadoutFit;
    private LoadoutDef? _loadoutDef;
    private LoadoutOptions _loadoutOptions = new();
    private string? _loadoutNode;
    private string _loadoutName = string.Empty;
    // The seat whose own fit the screen is editing, set only by the per-seat picker's door and null
    // for the Instant Action strip's. It is what says the screen belongs to a walk in progress, so
    // the walk survives the trip and the return lands back on the picker rather than on Instant
    // Action.
    private PlayerSeat? _loadoutSeat;

    /// <summary>The fit the loadout screen is editing, or null while it is not showing.</summary>
    public LoadoutChoice? LoadoutFit => _loadoutFit;

    /// <summary>The seat the loadout screen is editing for, as its index, or -1 when it is not
    /// showing or was opened from the Instant Action screen instead.</summary>
    public int LoadoutSeat => _loadoutSeat is { } seat ? SeatIndex(seat) : -1;

    /// <summary>The stock node whose fit the loadout screen is editing, or null while it is not showing.</summary>
    public string? LoadoutNode => _loadoutNode;

    private UiStrings LoadoutStrings => _campaign?.Strings ?? _hangarModule?.Strings ?? UiStrings.Empty;

    /// <summary>Opens the Weapon Loadout for the seat the radio pair names: the wingmen's airframe
    /// and shared fit, or the picked pilot row (a build edits its airframe's stock fit) and seat
    /// 0's fit. The picks standing on entry are remembered for CANCEL.</summary>
    public void OpenLoadout()
    {
        if (_iaRadio == 1)
        {
            var wingman = _instantAction.WingmanPlane;
            BeginLoadout(wingman.Node, wingman.Name, _instantAction.WingmanFit, null);
        }
        else
        {
            var pilot = PilotPick();
            BeginLoadout(pilot.Node, PilotRowText(pilot), PilotFit, null);
        }
    }

    private static int OptionIndex(IReadOnlyList<LoadoutOption> options, string id)
    {
        for (int i = 0; i < options.Count; i++)
        {
            if (string.Equals(options[i].Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    // The fill-order entry a physical pylon occupies inside the def's hardpoint count, or -1 when
    // the airframe carries no hardpoint there.
    private static int PylonEntry(LoadoutDef? def, int pylon)
    {
        int count = def?.Hardpoints?.Count ?? 0;
        for (int i = 0; i < count && i < Loadout.PylonFillOrder.Length; i++)
        {
            if (Loadout.PylonFillOrder[i] == pylon)
            {
                return i;
            }
        }

        return -1;
    }

    private static string StockPylon(LoadoutDef def, int entry) =>
        def.Hardpoints != null && entry < def.Hardpoints.Stock.Length ? def.Hardpoints.Stock[entry] : LoadoutChoice.None;

    // The firable gun a slot mounts, or null: a turret slot is built inert, so a pick there would
    // change nothing, and the original's screen shows no field for it.
    private static GunSpec? GunFor(LoadoutDef? def, int slot)
    {
        if (def == null)
        {
            return null;
        }

        foreach (var gun in def.Guns)
        {
            if (gun.Slot == slot && !gun.Turret)
            {
                return gun;
            }
        }

        return null;
    }

    // The per-seat picker's WEAPON LOADOUT: the seat's own fit over the airframe it has selected,
    // named for the seat so the screen says whose loadout it is. Its own storage and no other's, so
    // one pilot's picks cannot reach another's aeroplane.
    private void OpenSeatLoadout(PlayerSeat seat)
    {
        var roster = _setup.Roster;
        if (!seat.Locked || seat.Cursor < 0 || seat.Cursor >= roster.Count)
        {
            return;
        }

        var row = roster[seat.Cursor];
        BeginLoadout(row.Node, $"P{SeatIndex(seat) + 1}  {row.Name}", seat.Fit, seat);
    }

    // The screen over one aeroplane's fit, whichever door opened it: the airframe's stock def and
    // the option lists, then the picks standing on entry, which CANCEL and Back restore.
    private void BeginLoadout(string node, string name, LoadoutChoice fit, PlayerSeat? seat)
    {
        _loadoutNode = node;
        _loadoutName = name;
        _loadoutFit = fit;
        _loadoutSeat = seat;
        var stock = _stock?.Invoke();
        _loadoutDef = stock?.ForModel(_loadoutNode);
        _loadoutOptions = stock?.Options ?? new LoadoutOptions();
        for (int slot = 1; slot <= LoadoutChoice.MaxGunSlot; slot++)
        {
            _loadoutBefore[slot - 1] = fit.GunAmmoFor(slot);
        }

        for (int pylon = 1; pylon <= LoadoutChoice.MaxPylon; pylon++)
        {
            _loadoutBefore[LoadoutChoice.MaxGunSlot + pylon - 1] = fit.PylonFor(pylon);
        }

        _iaOpen = null;
        Open(OriginalScreen.InstantActionLoadout);
    }

    // Leaves the screen for whichever one opened it: the picks stay on ACCEPT and go back to what
    // stood on entry otherwise, so the shared fit reads as a working copy. The focus lands on the
    // row that opened the screen, so a second visit is one press away.
    private void CloseLoadout(bool keep)
    {
        if (!keep && _loadoutFit is { } fit)
        {
            for (int slot = 1; slot <= LoadoutChoice.MaxGunSlot; slot++)
            {
                fit.SetGunAmmo(slot, _loadoutBefore[slot - 1]);
            }

            for (int pylon = 1; pylon <= LoadoutChoice.MaxPylon; pylon++)
            {
                fit.SetPylon(pylon, _loadoutBefore[LoadoutChoice.MaxGunSlot + pylon - 1]);
            }
        }

        bool seated = _loadoutSeat != null;
        DropLoadout();
        Open(seated ? OriginalScreen.SeatPlane : OriginalScreen.InstantAction);
        FocusKey(seated ? nameof(BoardButton.ChangeAmmo) : WeaponLoadoutKey);
    }

    // Forgets what the screen was editing without deciding where to go next, which is what a walk
    // losing its picking seat mid-edit needs: the fit belongs to a seat that has left.
    private void DropLoadout()
    {
        _loadoutFit = null;
        _loadoutDef = null;
        _loadoutNode = null;
        _loadoutSeat = null;
        _iaOpen = null;
    }

    // The rows: an open list's items alone while one is open, else the fields the airframe has
    // and the two buttons, all one column.
    private void BuildLoadoutRows(List<OriginalRow> rows)
    {
        var screen = _layout.Screen(LoadoutSection);
        if (screen == null || AddOpenListRows(screen, rows))
        {
            return;
        }

        for (int slot = 1; slot <= LoadoutChoice.MaxGunSlot; slot++)
        {
            if (GunFor(_loadoutDef, slot) != null)
            {
                AddDropdown(screen, rows, LoadoutAmmoPrefix + (slot - 1).ToString(System.Globalization.CultureInfo.InvariantCulture), 0);
            }
        }

        for (int pylon = 1; pylon <= LoadoutChoice.MaxPylon; pylon++)
        {
            if (PylonEntry(_loadoutDef, pylon) >= 0)
            {
                AddDropdown(screen, rows, LoadoutRocketPrefix + (pylon - 1).ToString(System.Globalization.CultureInfo.InvariantCulture), 0);
            }
        }

        AddStrip(screen, rows, LoadoutAcceptKey, OriginalRowKind.Button, true, 0);
        AddStrip(screen, rows, LoadoutCancelKey, OriginalRowKind.Button, true, 0);
    }

    // A field's list over the stock table's options: the standing pick is the fit's, else the
    // def's own stock value; a pick writes the option's id into the fit.
    private DropdownList? LoadoutDropdownFor(string key)
    {
        if (_loadoutFit is not { } fit || _loadoutDef is not { } def)
        {
            return null;
        }

        if (Indexed(key, LoadoutAmmoPrefix) is { } group && GunFor(def, group + 1) is { } gun)
        {
            var options = _loadoutOptions.GunAmmo;
            int slot = group + 1;
            return new DropdownList(Names(options, o => o.Label), OptionIndex(options, fit.GunAmmoFor(slot) ?? gun.Ammo), _ => true,
                i => fit.SetGunAmmo(slot, options[i].Id));
        }

        if (Indexed(key, LoadoutRocketPrefix) is { } cell && PylonEntry(def, cell + 1) is >= 0 and var entry)
        {
            var options = _loadoutOptions.PylonOrdnance;
            int pylon = cell + 1;
            return new DropdownList(Names(options, o => o.Label), OptionIndex(options, fit.PylonFor(pylon) ?? StockPylon(def, entry)), _ => true,
                i => fit.SetPylon(pylon, options[i].Id));
        }

        return null;
    }

    private MenuExit? ActivateLoadout(OriginalRow row)
    {
        int colon = row.Key.IndexOf(':');
        if (colon > 0)
        {
            string prefix = row.Key[..colon];
            if (DropdownFor(prefix) is { } list)
            {
                int index = int.Parse(row.Key[(colon + 1)..], System.Globalization.CultureInfo.InvariantCulture);
                if (list.Allowed(index))
                {
                    list.Select(index);
                }

                _iaOpen = null;
                FocusKey(prefix);
            }

            return null;
        }

        switch (row.Key)
        {
            case LoadoutAcceptKey:
                CloseLoadout(keep: true);
                return null;
            case LoadoutCancelKey:
                CloseLoadout(keep: false);
                return null;
        }

        if (row.Kind == OriginalRowKind.Dropdown && DropdownFor(row.Key) is { } open)
        {
            _iaOpen = row.Key;
            _focus[(int)_screen] = Math.Max(0, open.Current);
        }

        return null;
    }

    // The screen as drawn: the section's background, the airframe's two diagram frames, its text
    // rows with the gun captions filled in, the fields and buttons, the focused field's description
    // in its pane, and an open list as the overlay.
    private void ComposeLoadout(
        IReadOnlyList<OriginalRow> rows, int focus, List<BoardPicture> backdrop, List<BoardPicture> pictures,
        List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques, List<BoardPanel> overlays)
    {
        var screen = _layout.Screen(LoadoutSection);
        if (screen == null)
        {
            return;
        }

        AddPane(screen, backdrop, "OL_BACKGROUND");
        int airframe = _loadoutNode != null ? PlanePickerRoster.AirframeOf(_loadoutNode) ?? 0 : 0;
        foreach (string key in new[] { "OL_P_PLANETOPICON", "OL_P_PLANEFRTICON" })
        {
            if (screen.Widget(key) is { Art.Count: > 0 } diagram)
            {
                pictures.Add(new BoardPicture(new BoardArt(BoardArtLibrary.Ui, diagram.Art[0], Math.Max(1, diagram.Frames)),
                    diagram.Int("X"), diagram.Int("Y"), airframe));
            }
        }

        ComposeLoadoutText(screen, lines);
        IReadOnlyList<OriginalRow> widgets = rows;
        int widgetFocus = focus;
        int widgetPressed = _pressed;
        if (_iaOpen != null)
        {
            var closed = new List<OriginalRow>();
            BuildLoadoutRows(closed);
            widgets = closed;
            widgetFocus = -1;
            widgetPressed = -1;
            for (int i = 0; i < closed.Count; i++)
            {
                if (closed[i].Key == _iaOpen)
                {
                    widgetFocus = i;
                }
            }
        }

        for (int i = 0; i < widgets.Count; i++)
        {
            ComposeInstantActionRow(widgets[i], i == widgetFocus, i == widgetPressed, i, fills, lines, plaques, pictures, LoadoutItemFont);
        }

        if (widgetFocus >= 0 && widgetFocus < widgets.Count)
        {
            ComposeLoadoutDescription(screen, lines, widgets[widgetFocus].Key);
        }

        if (_iaOpen != null)
        {
            ComposeOpenList(screen, rows, focus, overlays, LoadoutItemFont);
        }
    }

    // The section's text rows at their authored places: the title and the panel headings as
    // authored, the plane-info row naming the fitted aircraft, and each gun caption as the slot's
    // calibre or the no-gun marker.
    private void ComposeLoadoutText(MenuLayoutScreen screen, List<BoardLine> lines)
    {
        var strings = LoadoutStrings;
        foreach (var widget in screen.Widgets)
        {
            if (widget.TypeCode != "T")
            {
                continue;
            }

            string text = widget.Text ?? string.Empty;
            float size = LoadoutHeadingFont;
            var ink = BoardInk.Heading;
            if (widget.Key == "OL_T_TITLE")
            {
                size = LoadoutTitleFont;
            }
            else if (widget.Key == "OL_T_PLANEINFO")
            {
                text = _loadoutName;
            }
            else if (Indexed(widget.Key, "OL_T_GunName") is { } group)
            {
                var gun = GunFor(_loadoutDef, group + 1);
                text = gun != null
                    ? strings.Text(CalibreLabel + Math.Clamp((gun.Caliber - 30) / 10, 0, 4), $" .{gun.Caliber}-cal.").Trim()
                    : strings.Text(NoGunLabel, "No Gun");
                size = LoadoutCaptionFont;
                ink = gun != null ? BoardInk.Row : BoardInk.Detail;
            }
            else if (widget.Key.EndsWith("CAPTION", StringComparison.Ordinal))
            {
                size = LoadoutCaptionFont;
                ink = BoardInk.Detail;
            }

            if (text.Length > 0)
            {
                lines.Add(new BoardLine(text, widget.Int("X"), widget.Int("Y"), widget.Int("Width"), size, ink, -1, false, Justify(widget)));
            }
        }
    }

    // The focused field's description in the pane beside its panel, the string table's title and
    // body for the standing option, wrapped to the pane's authored width.
    private void ComposeLoadoutDescription(MenuLayoutScreen screen, List<BoardLine> lines, string key)
    {
        if (_loadoutFit is not { } fit || _loadoutDef is not { } def)
        {
            return;
        }

        string pane;
        int index;
        int title;
        int body;
        IReadOnlyList<LoadoutOption> options;
        if (Indexed(key, LoadoutAmmoPrefix) is { } group && GunFor(def, group + 1) is { } gun)
        {
            pane = "OL_S_AMMODESC";
            options = _loadoutOptions.GunAmmo;
            index = OptionIndex(options, fit.GunAmmoFor(group + 1) ?? gun.Ammo);
            title = AmmoTitleLabel;
            body = AmmoBodyLabel;
        }
        else if (Indexed(key, LoadoutRocketPrefix) is { } cell && PylonEntry(def, cell + 1) is >= 0 and var entry)
        {
            pane = "OL_S_ROCKETDESC";
            options = _loadoutOptions.PylonOrdnance;
            index = OptionIndex(options, fit.PylonFor(cell + 1) ?? StockPylon(def, entry));
            title = RocketTitleLabel;
            body = RocketBodyLabel;
        }
        else
        {
            return;
        }

        if (index < 0 || screen.Widget(pane) is not { } box)
        {
            return;
        }

        var strings = LoadoutStrings;
        string words = strings.Text(title + index, options[index].Label);
        string detail = strings.Text(body + index, string.Empty);
        if (detail.Length > 0)
        {
            words = words + " - " + detail;
        }

        lines.Add(new BoardLine(words, box.Int("X") + 4f, box.Int("Y") + 4f, Math.Max(1, box.Int("Width", 172) - 8), LoadoutCaptionFont, BoardInk.Row));
    }
}
