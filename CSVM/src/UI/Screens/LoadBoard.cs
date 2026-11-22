using System;
using System.Globalization;
using System.IO;
using System.Text;
using CSVM.Mech3;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.UI.Screens;

/// <summary>
/// The load screen drawn over the whole window while a session builds: the composition
/// <see cref="LoadScreens"/> makes, through the campaign boards' authored-pixel surface.
/// A build is one synchronous block that stalls the frame loop, so this board draws from inside
/// it. The build reports each step to <see cref="LoadProgress"/>, whose pump draws the fill and
/// the propeller. The board holds <see cref="LoadGate"/> closed while it stands, and chalks the
/// waiting line on from its own frames while held for the other machines.
/// ⚠ Install the pump nowhere but this node's own tree lifetime. A build with no screen over it
/// (every CLI launch bar <c>--debug-load</c>) must leave <see cref="LoadProgress.Current"/> null.
/// </summary>
public sealed partial class LoadBoard : Control
{
    // How long one letter of the waiting line takes to chalk. Chosen to read like a hand writing
    // on the board; the original draws its waiting row whole, so nothing is decoded here.
    private const double LetterSeconds = 0.06;

    private ComposedBoard _board = LoadScreens.Empty;
    private bool _campaign;
    private string _waiting = string.Empty;
    private int _written;
    private double _chalkClock;
    private LoadMotion _motion = LoadScreens.MotionFor(false, null);
    private BoardPalette _palette = BoardPalette.Paper;
    private string _dataRoot = string.Empty;
    private Mech3.TextureSet _rimage = Mech3.TextureSet.Empty;
    private ComposedBoardView? _view;
    private LoadProgress? _progress;
    private int _shots;

    /// <summary>Where each presented pump frame is photographed to (<c>--debug-load=</c>), empty
    /// for the ordinary launch that photographs nothing. A read back of the GPU per pump, which is
    /// why it is off unless a run asks for it.</summary>
    public string CaptureDir { get; set; } = string.Empty;

    /// <summary>The part of the waiting line chalked so far, empty on a board not held for the
    /// other machines.</summary>
    public string Written => _waiting[.._written];

    /// <summary>The board the view last drew, null before the board entered the tree.</summary>
    public ComposedBoard? Shown => _view?.Board;

    /// <summary>Builds the board for one launch. The campaign flag picks the paper sheet over the
    /// blackboard. The mission type names the Instant Action dialog the blackboard writes, null for a
    /// mode of ours. The subject is the heading a mode of ours takes in place of a dialog. The sheet
    /// is the campaign screen's whole content, null where the extraction could not answer for it.
    /// The briefing is a Dogfight's multiplayer dialog key, and the note a mode of ours's line under its heading.</summary>
    public static LoadBoard Build(
        string dataRoot, Mech3.TextureSet rimage, ZrdrSet zrdr, Messages messages, bool campaign, string subject,
        string? missionType, LoadSheet? sheet = null, string? briefing = null, string? note = null)
    {
        var board = new LoadBoard
        {
            _dataRoot = dataRoot,
            _rimage = rimage,
            _board = LoadScreens.For(
                campaign, subject, missionType, zrdr, messages, sheet, pumped: true,
                briefing: briefing, note: note),
            _motion = LoadScreens.MotionFor(campaign, sheet),
            _campaign = campaign,
            _palette = campaign ? BoardPalette.Paper : BoardPalette.Chalk,
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
        };
        board.SetAnchorsPreset(LayoutPreset.FullRect);
        return board;
    }

    /// <summary>Populates on entry rather than in <see cref="Build"/>: the view sizes itself off
    /// the viewport, which a node outside the tree cannot read. Takes the build's progress with
    /// it, so the screen is the pump and the pump dies with the screen. The moving layer opens on
    /// the cycle's first frame, since the composition leaves the still one to it.</summary>
    public override void _Ready()
    {
        var view = ComposedBoardView.Build(_dataRoot, _rimage);
        AddChild(view);
        _view = view;
        view.Show(_board, _palette, string.Empty, string.Empty);
        view.HoldMoving(LoadScreens.Moving(_motion, 0f, 0f, 0f, 0));
        _progress = new LoadProgress { Repaint = Repaint };
        LoadProgress.Current = _progress;
    }

    /// <summary>Closes the <see cref="LoadGate"/> for this board's tree lifetime, and stops every
    /// input event at the root window. The root is the one to stop, since a theater rehost moves the
    /// board into a surface of its own.</summary>
    public override void _EnterTree()
    {
        LoadGate.Close();
        GetTree().Root.GuiDisableInput = true;
    }

    /// <inheritdoc/>
    public override void _ExitTree()
    {
        LoadGate.Open();
        GetTree().Root.GuiDisableInput = LoadGate.Closed;
        if (ReferenceEquals(LoadProgress.Current, _progress))
        {
            LoadProgress.Current = null;
        }

        if (_progress is { } progress && progress.Trace.Count > 0)
        {
            Log.Info("ui", $"load screen: {Steps(progress)}");
        }
    }

    /// <inheritdoc/>
    public override void _Process(double delta)
    {
        // Track the window (resizable) so the artwork always covers it, the whole-window rule every
        // shared board follows.
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
        if (_written < _waiting.Length)
        {
            Chalk(delta);
        }
    }

    /// <summary>Writes <paramref name="text"/> as the waiting line, a letter at a time from
    /// <see cref="Chalk"/>. The letters it shares with the line already chalked stay, so a count
    /// that moves rewrites only the count. The frame loop runs again by now, so this redraws
    /// as any board does.</summary>
    public void Wait(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text == _waiting)
        {
            return;
        }

        int kept = 0;
        while (kept < _written && kept < text.Length && text[kept] == _waiting[kept])
        {
            kept++;
        }

        _waiting = text;
        _written = kept;
        Redraw();
    }

    /// <summary>Chalks the waiting line on by that many seconds. Takes its step from the caller,
    /// as <see cref="ComposedBoardView.AdvanceMovies"/> does, so a scripted run's board is a
    /// property of its frame count.</summary>
    public void Chalk(double seconds)
    {
        _chalkClock += seconds;
        double letters = Math.Floor(_chalkClock / LetterSeconds);
        if (letters < 1d)
        {
            return;
        }

        _written = (int)Math.Min(_waiting.Length, _written + letters);
        // A written line keeps no time over, so the next count starts from its first letter.
        _chalkClock = _written == _waiting.Length ? 0d : _chalkClock - (letters * LetterSeconds);
        Redraw();
    }

    // The build's own steps against wall time, one log line: which fraction each left the bar at,
    // the second of the build it arrived at, and a '-' where the screen was not drawn for it.
    private static string Steps(LoadProgress progress)
    {
        var line = new StringBuilder();
        line.Append(progress.Trace.Count).Append(" step(s), ")
            .Append(progress.Draws).Append(" draw(s), ")
            .Append(CultureInfo.InvariantCulture, $"longest gap {progress.LongestGap:0.000}s to @{progress.LongestGapEnd:0.000}s:");
        foreach (var step in progress.Trace)
        {
            line.Append(CultureInfo.InvariantCulture, $" {step.Step}={step.Fraction:0.00}@{step.Seconds:0.000}s");
            if (!step.Drawn)
            {
                line.Append('-');
            }
        }

        return line.ToString();
    }

    // One pumped repaint: the fill at the fraction the build has reached and the propeller frame
    // the wall clock is on, put on the moving layer and presented there, since the build owns the
    // loop until it returns and no queued redraw of ours would be flushed before it does.
    private void Repaint()
    {
        if (_view is not { } view || _progress is not { } progress)
        {
            return;
        }

        // Windows calls a window hung after 5 s without a message check. Input is dropped: no
        // handler may run inside the build.
        DisplayServer.ForceProcessAndDropEvents();
        var art = view.ArtSize(new BoardArt(BoardArtLibrary.Rimage, _motion.FillArt));
        view.PresentMoving(
            LoadScreens.Moving(_motion, art.X, art.Y, progress.Fraction, progress.Frame));
        Capture(progress);
    }

    // The frame just presented, read back off the GPU. The only way to see what a build's own
    // forced frames put on screen, since nothing yields between them.
    private void Capture(LoadProgress progress)
    {
        if (CaptureDir.Length == 0)
        {
            return;
        }

        Directory.CreateDirectory(CaptureDir);
        string name = string.Format(
            CultureInfo.InvariantCulture, "pump{0:D2}_{1:0.00}.png", _shots++, progress.Fraction);
        string path = Path.Combine(CaptureDir, name);
        if (GetViewport()?.GetTexture()?.GetImage() is not { } shot || shot.IsEmpty())
        {
            Log.Warn("ui", $"load screen: no image from the viewport for {path}");
            return;
        }

        var err = shot.SavePng(path);
        Log.Info(
            "ui",
            $"load screen: pump {name} fraction={progress.Fraction} draw={progress.Draws} save={err}");
    }

    private void Redraw() =>
        _view?.Show(LoadScreens.Written(_board, _campaign, Written), _palette, string.Empty, string.Empty);
}
