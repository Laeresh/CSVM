using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Flight;
using CSVM.Utils;
using Godot;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The Original presentation: <see cref="OriginalShell"/> drawn through a <see cref="ComposedBoardView"/>
/// on the board layer, registered under <see cref="PresentationId.Original"/>. <see cref="Activate"/>
/// builds the layer on the first call, refreshes the shared roster from the saved-plane store and
/// stands the shell on the destination's screen on every call; <see cref="Tick"/> keeps the pad
/// roster in step, scans the join gesture on the sortie screens, polls every seat, maps a
/// pointer from window pixels into the authored space through <see cref="BoardFit"/>, steps the
/// shell per seat, requests its cues and hands its exit to the host. The OS pointer is hidden
/// while the presentation is on screen, since the shell draws the original's own.
/// </summary>
public sealed class OriginalPresentation : IMenuPresentation
{
    /// <summary>The aid value that opens the Free Flight screen.</summary>
    public const string FreeFlightAid = "free-flight";

    /// <summary>The aid value that opens the Dogfight screen.</summary>
    public const string DogfightAid = "dogfight";

    /// <summary>The aid value that opens the Options screen.</summary>
    public const string OptionsAid = "options";

    /// <summary>The aid value that opens the Instant Action screen.</summary>
    public const string InstantActionAid = "instant-action";

    private readonly Node _parent;
    private readonly string _dataRoot;
    private readonly MenuLayout _layout;
    private readonly string _aid;
    private readonly MenuInput _player1;
    private readonly Dictionary<string, (int Width, int Height)?> _sizes = new(StringComparer.OrdinalIgnoreCase);
    private int _debugJoin;
    private CanvasLayer? _layer;
    private ComposedBoardView? _view;
    private OriginalShell? _shell;
    private MenuSeatDevices? _devices;
    private IMenuHost? _host;
    private BoardPalette _palette = BoardPalette.Chalk;
    private BoardPalette _paperPalette = BoardPalette.Paper;
    private bool _shown;
    private bool _joiningOpen;

    /// <summary>A presentation drawing under <paramref name="parent"/> over the art beneath
    /// <paramref name="dataRoot"/>, composed from <paramref name="layout"/>, opening on
    /// <paramref name="aid"/> (an Original <c>--menu=</c> value or "") at every top-level show,
    /// binding pads over seat 0's poller <paramref name="player1"/>, and seating
    /// <paramref name="debugJoin"/> device-less players once, the screenshot aid.</summary>
    public OriginalPresentation(Node parent, string dataRoot, MenuLayout layout, string aid, MenuInput player1, int debugJoin = 0)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _dataRoot = dataRoot ?? throw new ArgumentNullException(nameof(dataRoot));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _aid = aid ?? string.Empty;
        _player1 = player1 ?? throw new ArgumentNullException(nameof(player1));
        _debugJoin = debugJoin;
    }

    public PresentationId Id => PresentationId.Original;

    /// <summary>The shell while built, for the suites that read the screen back.</summary>
    public OriginalShell? Shell => _shell;

    /// <summary>The board palette the shell's inks resolve to: list text in the file-wide
    /// disabled grey with the active white for the focused row, plaque labels in the paper
    /// button's own three colours.</summary>
    public static BoardPalette PaletteFor(OriginalInks inks) => new(
        Row: ToColor(inks.Disabled),
        Focus: ToColor(inks.Active),
        Heading: ToColor(inks.Active),
        Detail: ToColor(inks.Disabled),
        LabelNormal: ToColor(inks.LabelNormal),
        LabelRollover: ToColor(inks.LabelRollover),
        LabelActivate: ToColor(inks.LabelDepressed),
        Hint: ToColor(inks.Disabled));

    /// <summary>The Instant Action screen's palette: every text in the screen's own authored text
    /// colour over its paper background, the button labels in the paper buttons' own tail.</summary>
    public static BoardPalette PaletteFor(OriginalInstantActionInks inks) => new(
        Row: ToColor(inks.Text),
        Focus: ToColor(inks.Text),
        Heading: ToColor(inks.Text),
        Detail: ToColor(inks.Text),
        LabelNormal: ToColor(inks.LabelNormal),
        LabelRollover: ToColor(inks.LabelRollover),
        LabelActivate: ToColor(inks.LabelDepressed),
        Hint: ToColor(inks.Text));

    /// <summary>The roster both presentations pick from: the eleven stock airframes, then the
    /// saved customs, each flying its airframe's stock node.</summary>
    public static IReadOnlyList<MenuAircraft> Roster(IReadOnlyList<CustomPlaneDef> customs)
    {
        var stock = new List<(string Name, string Node)>(OriginalRosters.Airframes.Count);
        foreach (var airframe in OriginalRosters.Airframes)
        {
            stock.Add((airframe.Name, airframe.Node));
        }

        return PlayerSetupFeature.BuildRoster(stock, customs, PlanePickerRoster.AirframeNode);
    }

    public void Activate(IMenuHost host, MenuReturnDestination destination)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
        var setup = host.Features.Get<PlayerSetupFeature>();
        if (_shell == null)
        {
            _devices = new MenuSeatDevices(_player1, setup);
            _shell = new OriginalShell(_layout, host.Features.Get<FreeFlightFeature>(), setup, Measure, _devices.FlightPads,
                instantAction: host.Features.Get<InstantActionFeature>());
            _palette = PaletteFor(_shell.Inks);
            _paperPalette = PaletteFor(_shell.InstantActionInks);
        }

        if (_layer == null)
        {
            _layer = new CanvasLayer { Name = "original_menu", Layer = HudLayers.Board };
            _view = ComposedBoardView.Build(_dataRoot);
            _layer.AddChild(_view);
            _parent.AddChild(_layer);
        }

        // Re-read on every show, so a plane saved in the hangar or by another presentation is
        // offered without a restart; cursors past a shrunk roster come back onto it.
        setup.SetRoster(Roster(CustomPlaneStore.UserPlanes().List()));
        foreach (var seat in setup.Seats)
        {
            seat.Cursor = Math.Clamp(seat.Cursor, 0, Math.Max(0, setup.Roster.Count - 1));
        }

        _shell.ReturnToTopLevel();
        if (destination is CabinReturn or DebriefReturn)
        {
            // Original campaign is not built yet, so both campaign destinations land on the top
            // level; the log says so rather than the screen pretending otherwise.
            Log.Info("ui", $"original presentation: {destination.GetType().Name} mapped to the top level (no Original campaign screens yet)");
        }
        else
        {
            switch (_aid)
            {
                case FreeFlightAid:
                    _shell.Open(OriginalScreen.FreeFlight);
                    break;
                case DogfightAid:
                    _shell.Open(OriginalScreen.Dogfight);
                    break;
                case OptionsAid:
                    _shell.Open(OriginalScreen.Options);
                    break;
                case InstantActionAid:
                    _shell.OpenInstantAction();
                    break;
            }
        }

        DebugJoin(setup);
        foreach (var seat in host.Seats)
        {
            seat.Prime();
        }

        _devices!.Sync();
        _devices.PrimeJoins();
        _joiningOpen = JoiningOpen();
        _layer.Visible = true;
        _shown = true;
        Input.MouseMode = Input.MouseModeEnum.Hidden;
        Redraw();
    }

    public void Tick(float dt)
    {
        if (!_shown || _shell == null || _host == null || _devices == null || _host.Seats.Count == 0 || _view == null)
        {
            return;
        }

        bool changed = _devices.Sync();
        bool joining = JoiningOpen();
        if (joining && !_joiningOpen)
        {
            _devices.PrimeJoins();
        }

        _joiningOpen = joining;
        if (joining)
        {
            changed |= _devices.ScanJoins();
        }

        var size = _view.GetViewportRect().Size;
        var fit = BoardFit.For(size.X, size.Y);
        // The seat list is live and a Back can shorten it mid-loop, so the count is re-read.
        for (int i = 0; i < _host.Seats.Count; i++)
        {
            var commands = _host.Seats[i].Poll(dt);
            if (commands.Pointer is { } pointer)
            {
                commands = commands with
                {
                    Pointer = pointer with
                    {
                        X = (pointer.X - fit.OriginX) / fit.Scale,
                        Y = (pointer.Y - fit.OriginY) / fit.Scale,
                    },
                };
            }

            var step = _shell.StepSeat(i, commands);
            foreach (string cue in step.Cues)
            {
                _host.Audio.Cue(new MenuCue(cue));
            }

            if (step.Exit != null)
            {
                _host.Exit(step.Exit);
                return;
            }

            changed |= step.Changed;
        }

        if (changed)
        {
            Redraw();
        }
    }

    public void Hide()
    {
        _shown = false;
        if (_layer != null)
        {
            _layer.Visible = false;
        }

        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void Deactivate()
    {
        Hide();
        _host = null;
        if (_layer == null)
        {
            return;
        }

        _parent.RemoveChild(_layer);
        _layer.QueueFree();
        _layer = null;
        _view = null;
    }

    private static Color ToColor(MenuLayoutColor c) => new(c.R / 255f, c.G / 255f, c.B / 255f, 1f);

    // Joining is open on the two sortie screens, where a seat has an aircraft column to pick from.
    private bool JoiningOpen() => _shell is { Screen: OriginalScreen.FreeFlight or OriginalScreen.Dogfight };

    // --debug-join=N, once: N device-less seats with distinct cursors, the last one selected, so
    // the seat strip and the aircraft tags can be shot with one controller.
    private void DebugJoin(PlayerSetupFeature setup)
    {
        int extra = _debugJoin;
        _debugJoin = 0;
        for (int i = 0; i < extra; i++)
        {
            var seat = setup.Join(new MenuIdleSource());
            if (seat == null)
            {
                break;
            }

            seat.Cursor = (i + 1) % Math.Max(1, OriginalRosters.Airframes.Count);
            if (i == extra - 1)
            {
                setup.Select(seat);
            }
        }

        if (extra > 0)
        {
            Log.Info("ui", $"original presentation: --debug-join seated {setup.Seats.Count} players (the added ones have no device)");
        }
    }

    private void Redraw()
    {
        if (_shell != null && _view != null)
        {
            // The Instant Action screen is a paper page with authored black text; the other
            // screens write in the file-wide inks over the dark top level.
            var palette = _shell.Screen == OriginalScreen.InstantAction ? _paperPalette : _palette;
            _view.Show(_shell.Compose(), palette, string.Empty, string.Empty);
        }
    }

    // The strip's pixel size, which the layout does not carry and the hit rectangles need. Read
    // once per name; a file that is not there measures as null and the row keeps a fallback size.
    private (int Width, int Height)? Measure(string art)
    {
        if (_sizes.TryGetValue(art, out var cached))
        {
            return cached;
        }

        (int Width, int Height)? size = null;
        string path = OriginalAvailability.ArtPath(_dataRoot, art);
        if (File.Exists(path) && Image.LoadFromFile(path) is { } image && !image.IsEmpty())
        {
            size = (image.GetWidth(), image.GetHeight());
        }

        _sizes[art] = size;
        return size;
    }
}
