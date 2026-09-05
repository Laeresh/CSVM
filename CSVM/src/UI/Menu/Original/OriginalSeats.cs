using System;
using System.Collections.Generic;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The Original shell's two sortie screens, Free Flight and the remake-only Dogfight, over the
/// shared player setup: the chapter column, the aircraft column as a window onto the shared
/// roster (the stock airframes, then the saved customs) with every seat's cursor tagged on it,
/// the seat strip, the join hint, BACK and FLY. Seat 0 drives the focus, the pointer, the
/// chapter and FLY; a later seat (a joined pad) walks its own cursor on the aircraft column,
/// selects with Accept, confirms with Accept again and leaves with Back from browsing. FLY is
/// seat 0's confirmation and the launch in one press, so it stands only once every other seat
/// has confirmed and, for Dogfight, a second seat has joined. Also the joining rule the
/// presentation reads (<see cref="JoiningOpen"/>) and the same seat strip over the campaign
/// boards once a second seat has joined. Nothing here is decoded.
/// </summary>
public sealed partial class OriginalShell
{
    /// <summary>The Dogfight door's key on the top level.</summary>
    public const string DogfightKey = "DOGFIGHT";

    /// <summary>How many aircraft rows the column shows at once; the window follows the focus.</summary>
    public const int AirframeWindow = 11;

    private const string AirframeKeyPrefix = "AIRFRAME:";

    // The Dogfight door under the Free Flight door, one plaque plus air below it.
    private const float DogfightDoorY = DoorY + 36f;

    // The seat strip under the chapter column, the aircraft tags at the column's right edge, the
    // hint between the two plaques, the scroll marks over and under the aircraft window.
    private const float SeatStripY = 468f;
    private const float SeatStripPitch = 14f;
    private const float SeatStripWidth = 360f;
    private const float SeatFont = 12f;
    private const float TagWidth = 60f;
    private const float HintX = 190f;
    private const float HintWidth = 420f;
    private const float HintY = 546f;
    private const float MarkSize = 12f;

    // The campaign boards' seat strip, in the desk margin above every board's clipboard. The top
    // left is the one band no campaign screen puts a plaque in: the book's tab sits at x 558 and
    // the briefing's buttons and every ACCEPT/FLY row sit at the bottom. Built-in's hint band
    // already claims the same rows for remake chrome. Four seats end at y 62.
    private const float CampaignStripX = 8f;
    private const float CampaignStripY = 6f;
    private const float CampaignStripPad = 3f;

    private int _pickedVersusChapter = -1;
    private int _airframeTop;

    /// <summary>The airframe seat 0 has selected, as its node, or null while browsing.</summary>
    public string? PickedAirframe
    {
        get
        {
            var roster = _setup.Roster;
            return Seat0 is { Locked: true } seat && seat.Cursor < roster.Count ? roster[seat.Cursor].Node : null;
        }
    }

    /// <summary>The Dogfight screen's picked chapter code, or null.</summary>
    public string? PickedDogfightChapter => _pickedVersusChapter >= 0 ? _chapters[_pickedVersusChapter].Code : null;

    /// <summary>The first roster row the aircraft column shows.</summary>
    public int AirframeTop => _airframeTop;

    /// <summary>Whether Start on a free pad joins a seat on the standing screen: the four screens
    /// that launch a flight. The two sortie screens give a joined seat an aircraft column, the
    /// flight check a check of its own; Instant Action opens so a second pilot can join before
    /// FLY MISSION rather than nowhere at all. Everywhere else a pad's Start does nothing.</summary>
    public bool JoiningOpen =>
        _screen is OriginalScreen.FreeFlight or OriginalScreen.Dogfight or OriginalScreen.InstantAction or OriginalScreen.CampaignFlightCheck;

    private bool IsSortie => _screen is OriginalScreen.FreeFlight or OriginalScreen.Dogfight;

    private MenuMode SortieMode => _screen == OriginalScreen.Dogfight ? MenuMode.Versus : MenuMode.Free;

    private PlayerSeat? Seat0 => _setup.Seats.Count > 0 ? _setup.Seats[0] : null;

    private int PickedChapterIndex
    {
        get => _screen == OriginalScreen.Dogfight ? _pickedVersusChapter : _pickedChapter;
        set
        {
            if (_screen == OriginalScreen.Dogfight)
            {
                _pickedVersusChapter = value;
            }
            else
            {
                _pickedChapter = value;
            }
        }
    }

    /// <summary>The row key of the roster's aircraft at <paramref name="index"/>.</summary>
    public static string AirframeKey(int index) => AirframeKeyPrefix + index;

    /// <summary>Applies one frame of one seat's commands. Seat 0's frame is <see cref="Step"/>;
    /// a later seat walks its own cursor on the aircraft column, selects and confirms with
    /// Accept, and leaves on Back while browsing (from any screen, as a guest may).</summary>
    public OriginalStep StepSeat(int index, MenuCommands commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (index == 0)
        {
            return Step(commands);
        }

        var seats = _setup.Seats;
        if (index < 0 || index >= seats.Count)
        {
            return new OriginalStep(Array.Empty<string>(), null, false);
        }

        var seat = seats[index];
        bool changed = false;
        if (!IsSortie)
        {
            // On the campaign's flight check a joined seat drives its own check, the frame it is
            // showing; anywhere else off the sortie screens a later seat can only leave.
            if (_screen == OriginalScreen.CampaignFlightCheck && _campaign?.Field.Current == index)
            {
                return Step(commands);
            }

            if (commands.Back)
            {
                changed = _setup.Unjoin(seat);
            }

            return new OriginalStep(Array.Empty<string>(), null, changed);
        }

        int count = _setup.Roster.Count;
        if (commands.MoveY != 0 && !seat.Locked && count > 0)
        {
            changed |= _setup.Browse(seat, (((seat.Cursor + commands.MoveY) % count) + count) % count);
        }

        if (commands.Accept)
        {
            changed |= seat.Locked ? _setup.Confirm(seat) : _setup.Select(seat);
        }
        else if (commands.Back)
        {
            changed |= _setup.Back(seat) == SeatBack.Browsing ? _setup.Unjoin(seat) : true;
        }

        return new OriginalStep(Array.Empty<string>(), null, changed);
    }

    private static bool TryAirframeIndex(string key, out int index)
    {
        index = -1;
        return key.StartsWith(AirframeKeyPrefix, StringComparison.Ordinal)
            && int.TryParse(key.AsSpan(AirframeKeyPrefix.Length), out index);
    }

    private static string SeatStatus(PlayerSeat seat, IReadOnlyList<MenuAircraft> roster)
    {
        string name = seat.Cursor < roster.Count ? roster[seat.Cursor].Name : string.Empty;
        return seat.Confirmed ? $"{name}  READY" : seat.Locked ? name : "choosing";
    }

    // The seat strip over a campaign board, drawn only once a second seat has joined so a solo
    // campaign shows the authored screen alone. No pick status: the campaign's picks are the
    // flight field's, so the strip says who is seated and, on the flight check, whose check shows.
    private BoardPanel? CampaignSeatPanel()
    {
        var seats = _setup.Seats;
        if (seats.Count < 2)
        {
            return null;
        }

        int current = _screen == OriginalScreen.CampaignFlightCheck && _campaign != null ? _campaign.Field.Current : -1;
        var lines = new List<BoardLine>(seats.Count);
        for (int i = 0; i < seats.Count; i++)
        {
            lines.Add(new BoardLine($"P{i + 1}  {seats[i].Source.DeviceLabel}", CampaignStripX,
                CampaignStripY + (i * SeatStripPitch), SeatStripWidth, SeatFont,
                i == current ? BoardInk.RowFocused : BoardInk.Detail));
        }

        var scrim = new BoardFill(CampaignStripX - CampaignStripPad, CampaignStripY - CampaignStripPad,
            SeatStripWidth + (2f * CampaignStripPad), (seats.Count * SeatStripPitch) + (2f * CampaignStripPad), 0, 0, 0, 0.45f);
        return new BoardPanel(new[] { scrim }, Array.Empty<BoardPicture>(), lines);
    }

    // The sortie screen's rows: the chapters and BACK in column 0, the aircraft window and FLY in
    // column 1. Rows outside the window keep their place in the column for the keyboard but are
    // neither drawn nor hit; the window slides so the focused row is always inside it.
    private void SortieRows(List<OriginalRow> rows)
    {
        var roster = _setup.Roster;
        int offset = _chapters.Count + 1;
        int focused = _focus[(int)_screen] - offset;
        if (focused >= 0 && focused < roster.Count)
        {
            if (focused < _airframeTop)
            {
                _airframeTop = focused;
            }
            else if (focused >= _airframeTop + AirframeWindow)
            {
                _airframeTop = focused - AirframeWindow + 1;
            }
        }

        _airframeTop = Math.Clamp(_airframeTop, 0, Math.Max(0, roster.Count - AirframeWindow));

        for (int i = 0; i < _chapters.Count; i++)
        {
            rows.Add(new OriginalRow(_chapters[i].Code, _chapters[i].Label, OriginalRowKind.ListRow,
                LeftColumnX, ListTop + (i * RowPitch), ListWidth, RowHeight, true, 0, null));
        }

        rows.Add(TextButton(BackKey, "BACK", LeftColumnX, PlaqueY, true, 0));
        for (int i = 0; i < roster.Count; i++)
        {
            bool visible = i >= _airframeTop && i < _airframeTop + AirframeWindow;
            rows.Add(new OriginalRow(AirframeKey(i), roster[i].Name, OriginalRowKind.ListRow,
                RightColumnX, ListTop + ((i - _airframeTop) * RowPitch), ListWidth, RowHeight, true, 1, null, visible));
        }

        var flySize = PlaqueSize();
        rows.Add(TextButton(FlyKey, "FLY", RightColumnX + ListWidth - flySize.Width, PlaqueY, FlyEnabled(), 1));
    }

    private bool FlyEnabled()
    {
        if (PickedChapterIndex < 0 || Seat0 is not { Locked: true })
        {
            return false;
        }

        var seats = _setup.Seats;
        if (seats.Count < PlayerSetupFeature.MinimumSeats(SortieMode))
        {
            return false;
        }

        for (int i = 1; i < seats.Count; i++)
        {
            if (!seats[i].Confirmed)
            {
                return false;
            }
        }

        return true;
    }

    private MenuExit? ActivateSortie(OriginalRow row)
    {
        switch (row.Key)
        {
            case BackKey:
                Open(OriginalScreen.TopLevel);
                return null;
            case FlyKey:
                return Fly();
        }

        if (row.Column == 0)
        {
            for (int i = 0; i < _chapters.Count; i++)
            {
                if (_chapters[i].Code == row.Key)
                {
                    PickedChapterIndex = i;
                    if (_screen == OriginalScreen.FreeFlight)
                    {
                        _free.SelectChapter(row.Key);
                    }
                }
            }

            return null;
        }

        // A click on another aircraft re-picks; the same row again changes nothing.
        if (TryAirframeIndex(row.Key, out int index) && Seat0 is { } seat)
        {
            if (seat.Locked && seat.Cursor == index)
            {
                return null;
            }

            if (seat.Locked)
            {
                _setup.Back(seat);
            }

            _setup.Browse(seat, index);
            _setup.Select(seat);
        }

        return null;
    }

    // FLY: seat 0's confirmation and the launch. Free Flight leaves through its feature with the
    // chapter handed over; Dogfight, the remake's own mode with no feature of its own, leaves
    // through the setup's exit.
    private MenuExit? Fly()
    {
        if (!FlyEnabled() || Seat0 is not { } seat)
        {
            return null;
        }

        _setup.Confirm(seat);
        string code = _chapters[PickedChapterIndex].Code;
        if (_screen == OriginalScreen.Dogfight)
        {
            return _setup.BuildExit(code, MenuMode.Versus, _flightDevices);
        }

        _free.SelectChapter(code);
        return _free.BuildExit(_setup.Choices(_flightDevices));
    }

    private bool IsPicked(OriginalRow row)
    {
        if (row.Column == 0)
        {
            return PickedChapterIndex >= 0 && row.Key == _chapters[PickedChapterIndex].Code;
        }

        return TryAirframeIndex(row.Key, out int index) && Seat0 is { Locked: true } seat && seat.Cursor == index;
    }

    // What the screen is waiting for, in the order a pilot resolves it.
    private string SortieHint()
    {
        var seats = _setup.Seats;
        if (PickedChapterIndex < 0)
        {
            return "Pick a map, then an aircraft";
        }

        if (Seat0 is not { Locked: true })
        {
            return "Pick an aircraft";
        }

        if (seats.Count < PlayerSetupFeature.MinimumSeats(SortieMode))
        {
            return "Dogfight needs a second seat: press START on a free pad to join";
        }

        for (int i = 1; i < seats.Count; i++)
        {
            if (!seats[i].Confirmed)
            {
                return $"Waiting for P{i + 1} to confirm (A again)";
            }
        }

        return seats.Count >= PlayerSetupFeature.MaxSeats
            ? "Four seats joined, the maximum. FLY when ready"
            : "FLY when ready, or press START on a free pad to join";
    }

    // The sortie screen's own words: the heading, the column labels, the seat strip, the aircraft
    // tags, the scroll marks, the hint and the controls line. The rows themselves are drawn by
    // Compose's row loop.
    private void ComposeSortie(IReadOnlyList<OriginalRow> rows, List<BoardLine> lines)
    {
        lines.Add(new BoardLine(_screen == OriginalScreen.Dogfight ? "DOGFIGHT" : "FREE FLIGHT",
            LeftColumnX, ListTop - 44f, 0f, HeadingFont, BoardInk.Heading));
        lines.Add(new BoardLine("MAP", LeftColumnX, ListTop - 20f, 0f, RowFont, BoardInk.Detail));
        lines.Add(new BoardLine("AIRCRAFT", RightColumnX, ListTop - 20f, 0f, RowFont, BoardInk.Detail));
        lines.Add(new BoardLine(
            "Up / Down  Choose       Left / Right  Column       Enter / A / Click  Pick       Esc / B  Back       START  Join",
            0f, FooterY, BoardFit.AuthoredWidth, FooterFont, BoardInk.Detail, -1, false, BoardJustify.Center));
        lines.Add(new BoardLine(SortieHint(), HintX, HintY, HintWidth, FooterFont, BoardInk.Detail, -1, false, BoardJustify.Center));

        var seats = _setup.Seats;
        var roster = _setup.Roster;
        for (int i = 0; i < seats.Count; i++)
        {
            lines.Add(new BoardLine($"P{i + 1}  {seats[i].Source.DeviceLabel}   {SeatStatus(seats[i], roster)}",
                LeftColumnX, SeatStripY + (i * SeatStripPitch), SeatStripWidth, SeatFont,
                seats[i].Locked ? BoardInk.RowFocused : BoardInk.Detail));
        }

        float markX = RightColumnX + ListWidth - 20f;
        if (_airframeTop > 0)
        {
            lines.Add(new BoardLine("▲", markX, ListTop - 16f, 16f, MarkSize, BoardInk.Detail, -1, false, BoardJustify.Center));
        }

        if (_airframeTop + AirframeWindow < roster.Count)
        {
            lines.Add(new BoardLine("▼", markX, ListTop + (AirframeWindow * RowPitch), 16f, MarkSize, BoardInk.Detail, -1, false, BoardJustify.Center));
        }

        // Later seats' cursors on the visible aircraft rows: the tag, a tick once selected, two
        // once confirmed.
        foreach (var row in rows)
        {
            if (!row.Visible || !TryAirframeIndex(row.Key, out int index))
            {
                continue;
            }

            var tags = new List<string>();
            bool locked = false;
            for (int i = 1; i < seats.Count; i++)
            {
                if (seats[i].Cursor != index)
                {
                    continue;
                }

                tags.Add($"P{i + 1}{(seats[i].Confirmed ? " ✓✓" : seats[i].Locked ? " ✓" : string.Empty)}");
                locked |= seats[i].Locked;
            }

            if (tags.Count > 0)
            {
                lines.Add(new BoardLine(string.Join("  ", tags), row.X + row.Width - TagWidth - 6f, row.Y + 3f, TagWidth,
                    SeatFont, locked ? BoardInk.RowFocused : BoardInk.Detail, -1, false, BoardJustify.Right));
            }
        }
    }
}
