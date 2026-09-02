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

    /// <summary>The aid value that opens the hangar's name screen on a fresh build.</summary>
    public const string PlaneNameAid = "plane-name";

    /// <summary>The aid value that opens the Plane Construction hub on its airframe tab.</summary>
    public const string PlaneConstructionAid = "plane-construction";

    /// <summary>The aid value that opens the hub's paint tab on a Fury in Fortune Hunters colours.</summary>
    public const string PlanePaintAid = "plane-paint";

    /// <summary>The aid value that opens the hub's construction totals page.</summary>
    public const string PlanePurchaseAid = "plane-purchase";

    /// <summary>The aid value that opens the hangar's inventory.</summary>
    public const string PlaneInventoryAid = "plane-inventory";

    // The aids' scratch build carries this name, so the shots read the same on every machine; it
    // is never committed by an aid.
    private const string AidPlaneName = "Sample Plane";

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
    private BoardPalette _hangarPalette = BoardPalette.Paper;
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

    /// <summary>The plane-construction screens' palette: the right page's authored black text with
    /// the title colour for headings, the tab bar's white labels with the standing tab in its
    /// disabled colour, the left page's white through the dialog ink.</summary>
    public static BoardPalette PaletteFor(OriginalHangarInks inks) => new(
        Row: ToColor(inks.Text),
        Focus: ToColor(inks.Title),
        Heading: ToColor(inks.Title),
        Detail: ToColor(inks.TabCurrent),
        LabelNormal: ToColor(inks.TabLabel),
        LabelRollover: ToColor(inks.TabLabel),
        LabelActivate: ToColor(inks.TabDepressed),
        Hint: ToColor(inks.Text));

    /// <summary>The roster both presentations pick from: the eleven stock airframes, then the
    /// saved customs, each flying its airframe's stock node.</summary>
    public static IReadOnlyList<MenuAircraft> Roster(IReadOnlyList<CustomPlaneDef> customs) =>
        OriginalRosters.Roster(customs);

    public void Activate(IMenuHost host, MenuReturnDestination destination)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
        var setup = host.Features.Get<PlayerSetupFeature>();
        if (_shell == null)
        {
            _devices = new MenuSeatDevices(_player1, setup);
            _shell = new OriginalShell(_layout, host.Features.Get<FreeFlightFeature>(), setup, Measure, _devices.FlightPads,
                instantAction: host.Features.Get<InstantActionFeature>(),
                hangar: host.Features.TryGet<HangarFeature>(out var hangar) ? hangar : null,
                planes: CustomPlaneStore.UserPlanes());
            _palette = PaletteFor(_shell.Inks);
            _paperPalette = PaletteFor(_shell.InstantActionInks);
            _hangarPalette = PaletteFor(_shell.HangarInks);
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
                case PlaneNameAid:
                    _shell.OpenHangar();
                    break;
                case PlaneConstructionAid:
                    _shell.OpenHangarTab(OriginalScreen.HangarAirframe, AidPlaneName);
                    break;
                case PlanePaintAid:
                    // The same pose as Built-in's paint aid: a Fury in Fortune Hunters colours with
                    // a nose decal chosen, so the two presentations' shots show one plane.
                    _shell.OpenHangarTab(OriginalScreen.HangarPaint, AidPlaneName);
                    if (host.Features.TryGet<HangarFeature>(out var paint) && paint.IsOpen)
                    {
                        paint.PickAirframe(7);
                        paint.AnswerDefaultsAsk(true);
                        paint.SetPattern(4);
                        paint.SetDecal(0, 40);
                    }

                    break;
                case PlanePurchaseAid:
                    _shell.OpenHangarTab(OriginalScreen.HangarPurchase, AidPlaneName);
                    break;
                case PlaneInventoryAid:
                    _shell.OpenHangarTab(OriginalScreen.HangarInventory, AidPlaneName);
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
        // Text capture is set before the poll: the name screen's letters must be text, not
        // cursor aliases, for the frame that reads them.
        _host.Seats[0].CapturingText = _shell.CapturingText;
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

        // And again after the frame, so a screen change this frame is what the next poll reads.
        _host.Seats[0].CapturingText = _shell.CapturingText;
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

        // Off screen nothing types, so a seat left capturing on the name screen is released.
        if (_host is { Seats.Count: > 0 } host)
        {
            host.Seats[0].CapturingText = false;
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
            // The Instant Action screen and the inventory are paper pages with authored black
            // text, the hub writes in its own inks, and the other screens write in the file-wide
            // inks over the dark top level.
            var palette = _shell.Screen is OriginalScreen.InstantAction or OriginalScreen.HangarInventory ? _paperPalette
                : _shell.IsHangarScreen ? _hangarPalette
                : _palette;
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
