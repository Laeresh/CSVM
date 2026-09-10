using System;
using System.Globalization;
using CSVM.Utils;
using Godot;

namespace CSVM.UI;

/// <summary>
/// The engine half of the boot sequence: the node the copyright card is drawn on, the clock its
/// holds run down, and the fade to black between the two logos. What the card is made of belongs
/// to <see cref="BootSequence.Card"/>; a <see cref="ComposedBoardView"/> draws it, so the art and
/// the strings resolve out of the extraction the way every other screen's do. It sits on the
/// launchscreen's own layer, under the films, and lives for the whole block.
/// <see cref="Play"/> is the one call a caller makes.
/// </summary>
public sealed partial class BootCard : Node
{
    private readonly ComposedBoard _card;
    private readonly string _dataRoot;

    private CanvasLayer? _layer;
    private ComposedBoardView? _view;
    private ColorRect? _cover;
    private Action? _then;
    private double _left;
    private double _span;
    private bool _fading;

    private BootCard(string dataRoot)
    {
        Name = "boot_card";
        _dataRoot = dataRoot;
        _card = BootSequence.Card(dataRoot);
    }

    /// <summary>Runs the whole boot sequence on <paramref name="host"/>: this node puts up the
    /// stills, <paramref name="film"/> plays the three movies, and <paramref name="then"/> runs
    /// after the last of them, by which point nothing of the sequence is left on screen.</summary>
    public static void Play(Node host, string dataRoot, BootSequence.PlayFilm film, Action then)
    {
        var card = new BootCard(dataRoot);
        host.AddChild(card);
        new BootSequence(film, card.Hold).Run(() =>
        {
            card.Close();
            then();
        });
    }

    /// <summary>Puts <paramref name="hold"/> on screen for <paramref name="seconds"/> and runs
    /// <paramref name="then"/> when it expires, or at once when a key or a left click ends it
    /// early. This has <see cref="BootSequence.ShowStill"/>'s shape and is what the sequence
    /// drives.</summary>
    public void Hold(BootHold hold, double seconds, Action then)
    {
        _then = then;
        _left = seconds;
        _span = seconds;
        _fading = hold == BootHold.Fade;
        if (hold == BootHold.Card)
        {
            // Neither of the card's two inks reads the palette, so which one it is handed cannot
            // reach the picture.
            _view?.Show(_card, BoardPalette.Paper, string.Empty, string.Empty);
        }

        Log.Info("ui", $"boot {hold} {seconds.ToString("0.###", CultureInfo.InvariantCulture)}s");
    }

    /// <inheritdoc/>
    public override void _Ready()
    {
        _view = ComposedBoardView.Build(_dataRoot);
        _cover = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _cover.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        // The launchscreen's own layer, because the card is the screen standing in front of the
        // launchscreen; a film at HudLayers.Cinema then covers it with nothing torn down.
        _layer = new CanvasLayer { Layer = HudLayers.Board, Name = "boot_card_layer" };
        _layer.AddChild(_view);
        _layer.AddChild(_cover);
        AddChild(_layer);
    }

    /// <inheritdoc/>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_then == null || !Skips(@event))
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        Advance();
    }

    /// <inheritdoc/>
    public override void _Process(double delta)
    {
        if (_then == null)
        {
            return;
        }

        _left -= delta;
        if (_fading && _cover != null)
        {
            float done = _span <= 0.0 ? 1f : (float)Math.Clamp(1.0 - (_left / _span), 0.0, 1.0);
            _cover.Color = new Color(0f, 0f, 0f, done);
        }

        if (_left <= 0.0)
        {
            Advance();
        }
    }

    // Any key or a left click, which is CinemaScreen.BootKeys read at a screen with no playback of
    // its own to ask. The stills take the films' set so the sequence has one rule.
    private static bool Skips(InputEvent @event) =>
        @event is InputEventKey { Pressed: true, Echo: false }
        or InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left };

    private void Advance()
    {
        var then = _then;
        _then = null;

        // A skipped fade still leaves the screen black, or the card comes back under the next wait
        // at whatever opacity the press caught it at.
        if (_fading && _cover != null)
        {
            _cover.Color = Colors.Black;
        }

        then?.Invoke();
    }

    private void Close()
    {
        _layer?.QueueFree();
        _layer = null;
        QueueFree();
    }
}
