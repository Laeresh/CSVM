using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Net;
using CSVM.Session.Roster;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.UI.Screens;
using static CSVM.UI.Menu.Original.OriginalLobbyScreen;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The local match screens, Free Flight and Dogfight, on the Multiplayer Lobby's layout with no
/// network behind them: no host, no guest and no wire. Mission Options carries the map and the
/// lobby's victory and lives rules, which a Dogfight edits and Free Flight greys. Select Plane is
/// seat 0's aircraft. The player list names every joined seat and, in a Dogfight, every join board
/// bot. FLY walks each further seat through the per-seat aircraft screen, as the lobby's Ready does,
/// and the walk's last confirm is the launch. Every rule here is remake-only
/// (<c>docs/org/menu-inventory.md</c>); the rows and the drawing are <see cref="OriginalLobbyScreen"/>'s.
/// </summary>
public sealed class OriginalLocalLobby : IOriginalScreenModule
{
    /// <summary>The hint while a Dogfight is short of its second pilot.</summary>
    public const string SecondPilotHint = "Dogfight needs a second seat or a bot, from the JOIN BOARD";

    /// <summary>The hint once the screen can launch.</summary>
    public const string ReadyHint = "LAUNCH! when ready";

    private readonly IOriginalScreenHost _host;
    private readonly OriginalLobbyScreen _lobby;
    private readonly PlayerSetupFeature _setup;
    private readonly FreeFlightFeature _free;
    private readonly IReadOnlyList<OriginalChapter> _chapters;
    private readonly Func<PlayerSeat, IReadOnlyList<int>> _flightDevices;
    private readonly Func<StockLoadouts?> _stock;
    private readonly Func<bool> _walkSeats;
    private string? _open;

    // The number box being typed into and what it holds so far. That may stand outside the box's
    // range on the way to two digits.
    private string? _typing;
    private string _draft = string.Empty;

    /// <summary>The local lobby over the shared setup and Free Flight features, drawn by
    /// <paramref name="lobby"/>. A launch with a further seat still to pick runs
    /// <paramref name="walkSeats"/>, which answers whether a walk began; its last confirm calls
    /// <see cref="Launch"/> again.</summary>
    public OriginalLocalLobby(
        IOriginalScreenHost host, OriginalLobbyScreen lobby, PlayerSetupFeature setup, FreeFlightFeature free,
        IReadOnlyList<OriginalChapter> chapters, Func<PlayerSeat, IReadOnlyList<int>> flightDevices,
        Func<StockLoadouts?> stock, Func<bool> walkSeats)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
        _setup = setup ?? throw new ArgumentNullException(nameof(setup));
        _free = free ?? throw new ArgumentNullException(nameof(free));
        _chapters = chapters ?? throw new ArgumentNullException(nameof(chapters));
        _flightDevices = flightDevices ?? throw new ArgumentNullException(nameof(flightDevices));
        _stock = stock ?? throw new ArgumentNullException(nameof(stock));
        _walkSeats = walkSeats ?? throw new ArgumentNullException(nameof(walkSeats));
    }

    /// <summary>The tab showing, Mission Options or Select Plane.</summary>
    public LobbyTab Tab { get; private set; } = LobbyTab.Mission;

    /// <summary>The dropdown whose list stands open, or null.</summary>
    public string? OpenDropdown => _open;

    /// <summary>Whether FLY stands: seat 0 on a roster row, and in a Dogfight a second pilot, a seat
    /// or a join board bot.</summary>
    public bool CanLaunch =>
        Seat0 is { } seat && seat.Cursor < _setup.Roster.Count && _setup.Pilots(Mode) >= PlayerSetupFeature.MinimumSeats(Mode);

    /// <summary>Whether seat 0's typed characters feed a Dogfight's number box.</summary>
    internal bool CapturingText =>
        _host.Screen == OriginalScreen.Dogfight && !_host.DialogOpen && _open == null
        && _host.FocusedKey is TimeKey or ScoreKey or LivesKey;

    private bool Versus => _host.Screen == OriginalScreen.Dogfight;

    private MenuMode Mode => Versus ? MenuMode.Versus : MenuMode.Free;

    private PlayerSeat? Seat0 => _setup.Seats.Count > 0 ? _setup.Seats[0] : null;

    // The showing screen's map, as an index into the chapters: its own pick, else the first.
    private int Map => Math.Max(0, IndexOf(PickedChapter(_host.Screen)));

    /// <summary>The map picked on <paramref name="screen"/>'s map box, each mode keeping its own, or
    /// null until one is. FLY flies the first chapter until then, the one the box shows.</summary>
    public string? PickedChapter(OriginalScreen screen) =>
        screen == OriginalScreen.Dogfight ? _setup.MatchChapter : _free.Chapter?.Code;

    /// <summary>Whether a screen is this module's.</summary>
    public bool Owns(OriginalScreen screen) => screen is OriginalScreen.FreeFlight or OriginalScreen.Dogfight;

    /// <summary>A top level door's way in: <paramref name="screen"/> on Mission Options, no list
    /// open.</summary>
    public void Enter(OriginalScreen screen)
    {
        Tab = LobbyTab.Mission;
        _open = null;
        _typing = null;
        _host.Open(screen);
    }

    /// <summary>The showing tab's rows, or the open list's items alone while one stands open.</summary>
    public void BuildRows(List<OriginalRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (_open is { } key && Items(key) is { } items && ClosedBox(key) is { } box)
        {
            OriginalDropLists.AddRows(DropOver(key, items, box, null), 0, rows,
                (art, width, height) => OriginalWidgets.StripSize(art, _host.Measure, width, height));
            return;
        }

        Widgets(rows);
    }

    /// <summary>An open list shows every item, as the lobby's do, so nothing scrolls.</summary>
    public void Lists(List<OriginalList> lists)
    {
    }

    /// <summary>A sideways press always moves the focus, the boxes' lists opening on Accept.
    /// ⚠ Do not step a box's value sideways as the lobby does. The map box is the one way across
    /// to the rules while FLY stands greyed, the lobby's way being its live Select Ammo tab.</summary>
    public bool StepSideways(IReadOnlyList<OriginalRow> rows, int focus, int direction) => false;

    /// <summary>Closes the open list and puts the focus back on its box.</summary>
    public bool CloseDropdown()
    {
        if (_open is not { } key)
        {
            return false;
        }

        _open = null;
        _host.FocusKey(key);
        return true;
    }

    /// <summary>The screen's answer to an activated row.</summary>
    public MenuExit? Activate(OriginalRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _typing = null;
        if (row.Key.IndexOf(':', StringComparison.Ordinal) is var colon and > 0)
        {
            string key = row.Key[..colon];
            if (int.TryParse(row.Key[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out int index))
            {
                Pick(key, index);
            }

            CloseDropdown();
            return null;
        }

        var options = _setup.MatchOptions;
        switch (row.Key)
        {
            case MissionTabKey:
                Tab = LobbyTab.Mission;
                return null;
            case PlaneTabKey:
                Tab = LobbyTab.Plane;
                return null;
            case TimeRadioKey:
                _setup.MatchOptions = options with { Victory = DogfightLobby.Toggled(options.Victory, DogfightVictory.Time) };
                return null;
            case ScoreRadioKey:
                _setup.MatchOptions = options with { Victory = DogfightLobby.Toggled(options.Victory, DogfightVictory.Score) };
                return null;
            case LimitedLivesKey:
                _setup.MatchOptions = options with { LimitedLives = !options.LimitedLives };
                return null;
            case AutoRespawnKey:
                _setup.MatchOptions = options with { AutoRespawn = !options.AutoRespawn };
                return null;
            case OriginalShell.FlyKey:
                return Launch();
            case OriginalShell.BackKey:
                _host.Open(OriginalScreen.TopLevel);
                return null;
        }

        if (row.Kind == OriginalRowKind.Dropdown && Items(row.Key) != null)
        {
            _open = row.Key;
            _host.FocusedRow = Math.Max(0, Current(row.Key));
        }

        return null;
    }

    /// <summary>Back closes an open list; otherwise the shell's own way back leaves for the top
    /// level, as BACK does.</summary>
    public bool Back() => CloseDropdown();

    /// <summary>FLY: the per-seat walk while a further seat has still to pick. Otherwise it is seat
    /// 0's confirmation and the launch on the map, with a Dogfight's rules. Free Flight leaves
    /// through its feature, a Dogfight through the setup's exit.</summary>
    public MenuExit? Launch()
    {
        if (!CanLaunch || Seat0 is not { } seat || _walkSeats())
        {
            return null;
        }

        _setup.Select(seat);
        _setup.Confirm(seat);
        string code = _chapters[Map].Code;
        if (Versus)
        {
            return _setup.BuildExit(code, MenuMode.Versus, _flightDevices);
        }

        _free.SelectChapter(code);
        return _free.BuildExit(_setup.Choices(_flightDevices));
    }

    /// <summary>The screen as drawn: the lobby's background and tab page, the player list, the hint
    /// in the chat pane's place and the widgets. An open list stands over all of it.</summary>
    public void Compose(IReadOnlyList<OriginalRow> rows, int focus, BoardLayers layers)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(layers);
        var widgets = new List<OriginalRow>();
        Widgets(widgets);
        string focused = _open ?? (focus >= 0 && focus < rows.Count && !_host.DialogOpen ? rows[focus].Key : string.Empty);
        int pressedAt = _host.DialogOpen || _open != null ? -1 : _host.PressedRow;
        string pressed = pressedAt >= 0 && pressedAt < rows.Count ? rows[pressedAt].Key : string.Empty;

        ComposeBackdrop(layers, Tab);
        ComposePlayers(layers);
        layers.Lines.Add(_lobby.PinnedLine(Hint()));
        var options = _setup.MatchOptions;
        foreach (var row in widgets)
        {
            _lobby.ComposeWidget(row, row.Key == focused, row.Key == pressed, Ticked(row.Key, options), layers);
        }

        if (Tab == LobbyTab.Mission)
        {
            _lobby.ComposeMissionHead(layers, Versus ? (int)DogfightMissionType.Deathmatch : null);
        }
        else if (Seat0Row() is { } plane)
        {
            // A saved build reads its own loadout over its airframe's stock, as the lobby's does.
            var stock = _stock()?.ForModel(plane.Node);
            var def = stock != null && plane.Custom is { } custom ? CustomPlaneBuild.LoadoutFor(custom, stock) : stock;
            _lobby.ComposePlane(plane.Custom?.Airframe ?? StockAirframes.IdOf(plane.Node) ?? 0, def, layers);
        }

        if (_open is { } open)
        {
            _lobby.ComposeOpenList(rows, focus, Current(open), open, layers);
        }
    }

    /// <summary>Typed digits and Backspace into a Dogfight's focused number box, as the lobby's
    /// boxes take them. A value inside the box's range is set as it is typed.</summary>
    internal bool TypeText(MenuCommands commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (!CapturingText || (commands.Typed.Length == 0 && !commands.Erase))
        {
            return false;
        }

        string key = _host.FocusedKey;
        _draft = Typed(_typing == key ? _draft : BoxValue(key), commands, key);
        _typing = key;
        if (int.TryParse(_draft, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
        {
            var options = _setup.MatchOptions;
            _setup.MatchOptions = key switch
            {
                TimeKey => DogfightLobby.WithTimeMinutes(options, value),
                ScoreKey => DogfightLobby.WithScore(options, value),
                LivesKey => DogfightLobby.WithLives(options, value),
                _ => options,
            } ?? options;
        }

        return true;
    }

    // Every widget of the showing tab: the tabs first, as the lobby's, so a tab keeps its place
    // across a switch. Then the map box and the rules, or Select Plane's box for seat 0, FLY and BACK.
    private void Widgets(List<OriginalRow> rows)
    {
        TabRows(rows, tab => tab <= (int)LobbyTab.Plane);
        var options = _setup.MatchOptions;
        bool rules = Versus;
        if (Tab == LobbyTab.Mission)
        {
            MapRows(rows, _chapters[Map].Label, Versus ? _lobby.TypeWord((int)DogfightMissionType.Deathmatch) : "Free Flight", true, false);
            VictoryRows(rows, options, BoxValue(TimeKey), BoxValue(ScoreKey), rules, rules);
            LivesRows(rows, options, BoxValue(LivesKey), rules);
        }
        else
        {
            rows.Add(PlaneRow(Seat0Row()?.Name ?? string.Empty, true));
        }

        rows.Add(_lobby.LaunchRow(OriginalShell.FlyKey, CanLaunch));
        rows.Add(_lobby.LeaveRow(OriginalShell.BackKey));
    }

    private string BoxValue(string key)
    {
        var options = _setup.MatchOptions;
        int value = key switch
        {
            TimeKey => options.TimeMinutes,
            ScoreKey => options.Score,
            _ => options.Lives,
        };

        return value.ToString(CultureInfo.InvariantCulture);
    }

    private int IndexOf(string? code)
    {
        for (int i = 0; i < _chapters.Count; i++)
        {
            if (_chapters[i].Code == code)
            {
                return i;
            }
        }

        return -1;
    }

    private MenuAircraft? Seat0Row() =>
        Seat0 is { } seat && seat.Cursor >= 0 && seat.Cursor < _setup.Roster.Count ? _setup.Roster[seat.Cursor] : null;

    // A dropdown's items: the map box the chapters, the plane box the whole roster. Null for any
    // other key.
    private IReadOnlyList<string>? Items(string key)
    {
        if (key == EnvironmentKey)
        {
            var maps = new string[_chapters.Count];
            for (int i = 0; i < maps.Length; i++)
            {
                maps[i] = _chapters[i].Label;
            }

            return maps;
        }

        if (key != PlaneKey)
        {
            return null;
        }

        var roster = _setup.Roster;
        var planes = new string[roster.Count];
        for (int i = 0; i < planes.Length; i++)
        {
            planes[i] = roster[i].Name;
        }

        return planes;
    }

    private int Current(string key) => key == EnvironmentKey ? Map : Seat0?.Cursor ?? 0;

    // A pick off a list. The map goes to the showing mode's own pick, Free Flight's being its
    // feature's. Seat 0's aircraft is its cursor, which FLY selects and confirms.
    private void Pick(string key, int index)
    {
        if (Items(key) is not { } items || index < 0 || index >= items.Count)
        {
            return;
        }

        if (key == EnvironmentKey)
        {
            if (Versus)
            {
                _setup.MatchChapter = _chapters[index].Code;
            }
            else
            {
                _free.SelectChapter(_chapters[index].Code);
            }

            return;
        }

        if (Seat0 is { } seat)
        {
            // Two steps back reach browsing from any stage: confirmed, then selected.
            _setup.Back(seat);
            _setup.Back(seat);
            _setup.Browse(seat, index);
        }
    }

    // The closed box an open list hangs under.
    private OriginalRow? ClosedBox(string key)
    {
        var closed = new List<OriginalRow>();
        Widgets(closed);
        return closed.Find(row => row.Key == key);
    }

    private string Hint()
    {
        if (_setup.Pilots(Mode) < PlayerSetupFeature.MinimumSeats(Mode))
        {
            return PlayerSetupFeature.MinimumSeats(Mode) > _setup.SeatLimit ? PlayerSetupFeature.VrOneSeat : SecondPilotHint;
        }

        return _setup.Seats.Count >= PlayerSetupFeature.MaxSeats
            ? "Four seats signed on, the maximum. " + ReadyHint
            : ReadyHint;
    }

    // The player list under the screen's title. Each joined seat shows its tag and device in red,
    // its aircraft once picked and its Ready mark once confirmed. A Dogfight's bot rows follow with
    // their tag. The list shows as many rows as the lobby's; the header counts every one.
    private void ComposePlayers(BoardLayers layers)
    {
        var seats = _setup.Seats;
        var bots = Versus ? _setup.Bots.Rows : Array.Empty<DogfightBot>();
        _lobby.ComposeListHead(layers, Versus ? "DOGFIGHT" : "FREE FLIGHT", seats.Count, bots.Count, PlayerSetupFeature.MaxSeats);
        var roster = _setup.Roster;
        int count = Math.Min(VisiblePlayers, seats.Count + bots.Count);
        for (int slot = 0; slot < count; slot++)
        {
            if (slot >= seats.Count)
            {
                var bot = bots[slot - seats.Count];
                _lobby.ComposePlayerRow(layers, slot, bot.Callsign, PlaneText(string.Empty, bot.Airframe, true), false, 0f, true, false);
                continue;
            }

            var seat = seats[slot];
            string plane = (slot == 0 || seat.Locked) && seat.Cursor < roster.Count ? roster[seat.Cursor].Name : string.Empty;
            _lobby.ComposePlayerRow(layers, slot, $"{SplitScreen.PlayerTag(slot)}  {seat.Source.DeviceLabel}", plane, true, 0f, false,
                seat.Confirmed);
        }
    }
}
