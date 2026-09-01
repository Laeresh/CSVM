using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Utils;
using Godot;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The Original presentation: <see cref="OriginalShell"/> drawn through a <see cref="ComposedBoardView"/>
/// on the board layer, registered under <see cref="PresentationId.Original"/>. <see cref="Activate"/>
/// builds the layer on the first call and stands the shell on the destination's screen on every
/// call; <see cref="Tick"/> polls the host's first seat, maps its pointer from window pixels into
/// the authored space through <see cref="BoardFit"/>, steps the shell, requests its cues and hands
/// its exit to the host. The OS pointer is hidden while the presentation is on screen, since the
/// shell draws the original's own. Every screen scales as one 4:3 board; nothing here stretches.
/// </summary>
public sealed class OriginalPresentation : IMenuPresentation
{
    /// <summary>The aid value that opens the Free Flight screen.</summary>
    public const string FreeFlightAid = "free-flight";

    /// <summary>The aid value that opens the Options screen.</summary>
    public const string OptionsAid = "options";

    private readonly Node _parent;
    private readonly string _dataRoot;
    private readonly MenuLayout _layout;
    private readonly string _aid;
    private readonly Dictionary<string, (int Width, int Height)?> _sizes = new(StringComparer.OrdinalIgnoreCase);
    private CanvasLayer? _layer;
    private ComposedBoardView? _view;
    private OriginalShell? _shell;
    private IMenuHost? _host;
    private BoardPalette _palette = BoardPalette.Chalk;
    private bool _shown;

    /// <summary>A presentation drawing under <paramref name="parent"/> over the art beneath
    /// <paramref name="dataRoot"/>, composed from <paramref name="layout"/>, opening on
    /// <paramref name="aid"/> (an Original <c>--menu=</c> value or "") at every top-level show.</summary>
    public OriginalPresentation(Node parent, string dataRoot, MenuLayout layout, string aid)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _dataRoot = dataRoot ?? throw new ArgumentNullException(nameof(dataRoot));
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _aid = aid ?? string.Empty;
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

    public void Activate(IMenuHost host, MenuReturnDestination destination)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
        if (_shell == null)
        {
            _shell = new OriginalShell(_layout, host.Features.Get<FreeFlightFeature>(), Measure);
            _palette = PaletteFor(_shell.Inks);
        }

        if (_layer == null)
        {
            _layer = new CanvasLayer { Name = "original_menu", Layer = HudLayers.Board };
            _view = ComposedBoardView.Build(_dataRoot);
            _layer.AddChild(_view);
            _parent.AddChild(_layer);
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
                case OptionsAid:
                    _shell.Open(OriginalScreen.Options);
                    break;
            }
        }

        if (host.Seats.Count > 0)
        {
            host.Seats[0].Prime();
        }

        _layer.Visible = true;
        _shown = true;
        Input.MouseMode = Input.MouseModeEnum.Hidden;
        Redraw();
    }

    public void Tick(float dt)
    {
        if (!_shown || _shell == null || _host == null || _host.Seats.Count == 0 || _view == null)
        {
            return;
        }

        var commands = _host.Seats[0].Poll(dt);
        if (commands.Pointer is { } pointer)
        {
            var size = _view.GetViewportRect().Size;
            var fit = BoardFit.For(size.X, size.Y);
            commands = commands with
            {
                Pointer = pointer with
                {
                    X = (pointer.X - fit.OriginX) / fit.Scale,
                    Y = (pointer.Y - fit.OriginY) / fit.Scale,
                },
            };
        }

        var step = _shell.Step(commands);
        foreach (string cue in step.Cues)
        {
            _host.Audio.Cue(new MenuCue(cue));
        }

        if (step.Exit != null)
        {
            _host.Exit(step.Exit);
            return;
        }

        if (step.Changed)
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

    private void Redraw()
    {
        if (_shell != null && _view != null)
        {
            _view.Show(_shell.Compose(), _palette, string.Empty, string.Empty);
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
