using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.UI.Screens;

/// <summary>
/// What the window shows instead of the menu while the game files are not settled. That is the
/// picker when none were found and the optional copy into the data root. Then come the questions
/// owed once, about an old <c>extracted</c> folder and a missing patch archive. The state and each view's words and
/// buttons are <see cref="SourceFlow"/>'s. This lays them out and hands back to the launcher once
/// the flow is done. Keyboard, pad, mouse and touch all drive it through Godot's own focus
/// navigation; the folder and image picker is <see cref="InstallPicker"/>.
/// </summary>
public sealed partial class SourceScreen : CanvasLayer
{
    /// <summary>The owner the folder field raises the on-screen keyboard under.</summary>
    public const string KeyboardOwner = "no-game-data";

    // Authored against the launchscreen's own 720p metrics, like the corner stamp's 15. The title
    // reads at a glance, the body a couple of points above it.
    private const int TitleFontSize = 30;
    private const int BodyFontSize = 17;

    // Held off the window edge on all four sides, so a long install path wraps inside the screen
    // rather than against the frame.
    private const int EdgeMarginPx = 64;

    // A finger's width, so the buttons are touch targets on a handheld.
    private static readonly Vector2 ButtonSize = new(190, 46);

    private static readonly Color TitleColour = new(0.93f, 0.83f, 0.55f);
    private static readonly Color BodyColour = new(0.84f, 0.87f, 0.92f);
    private static readonly Color DimColour = new(0.58f, 0.62f, 0.69f);
    private static readonly Color NoticeColour = new(0.96f, 0.66f, 0.40f);

    private readonly List<Button> _buttons = new();

    private SourceFlow _flow = null!;
    private Action _continue = null!;
    private Action _quit = null!;
    private bool _continued;
    private SourceView? _shown;

    private Label _title = null!;
    private Label _body = null!;
    private Label _hint = null!;
    private Control _field = null!;
    private LineEdit _path = null!;
    private Button _chooseButton = null!;
    private Label _notice = null!;
    private Control _progress = null!;
    private ProgressBar _bar = null!;
    private Label _line = null!;
    private HBoxContainer _row = null!;

    /// <summary>Gets the flow this screen shows.</summary>
    public SourceFlow Flow => _flow;

    /// <summary>Gets the folder and image picker.</summary>
    public InstallPicker Picker { get; private set; } = null!;

    /// <summary>Gets the view's default press, the first of its buttons.</summary>
    public Button DefaultButton => _buttons[0];

    /// <summary>Gets the view the screen last laid out.</summary>
    public SourceView? Shown => _shown;

    /// <summary>Gets the title as it reads now.</summary>
    public string TitleText => _title.Text;

    /// <summary>Gets the body as it reads now.</summary>
    public string BodyText => _body.Text;

    /// <summary>Gets the folder field's text.</summary>
    public string PathText => _path.Text;

    /// <summary>Gets whether the folder field is shown.</summary>
    public bool FieldShown => _field.Visible;

    /// <summary>Gets the notice under the field or the buttons.</summary>
    public string NoticeText => _notice.Visible ? _notice.Text : string.Empty;

    /// <summary>Gets the progress bar's value, 0 to 100.</summary>
    public double BarValue => _bar.Value;

    /// <summary>Gets the labels of the view's buttons, in order, Quit included.</summary>
    public IReadOnlyList<string> ButtonLabels => _buttons.Select(b => b.Text).ToList();

    /// <summary>The screen over <paramref name="flow"/>, as one layer the caller parents. It ticks
    /// the flow itself. <paramref name="onContinue"/> runs once, when the flow is done;
    /// <paramref name="onQuit"/> is the Quit button.</summary>
    public static SourceScreen Build(SourceFlow flow, Action onContinue, Action onQuit)
    {
        // The launchscreen's own tier: this screen is what stands in its place, and nothing else
        // is drawn while it is up.
        var screen = new SourceScreen { Layer = HudLayers.Board, _flow = flow, _continue = onContinue, _quit = onQuit };
        screen.Compose();
        return screen;
    }

    /// <summary>The view's button labelled <paramref name="label"/>, or null.</summary>
    public Button? Button(string label) => _buttons.FirstOrDefault(b => b.Text == label);

    /// <summary>Presses the view's button labelled <paramref name="label"/>; false when there is none.</summary>
    public bool Press(string label)
    {
        if (Button(label) is not { } button)
        {
            return false;
        }

        button.EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }

    /// <summary>Opens the picker, starting at <paramref name="from"/> or the field's folder.</summary>
    public void OpenPicker(string? from = null)
    {
        if (_flow.View == SourceView.Asking)
        {
            Picker.Open(from ?? NearestFolder(_path.Text));
        }
    }

    /// <summary>A path the picker chose, taken as if the picker had returned it.</summary>
    public void TakePick(string path)
    {
        bool works = _flow.Pick(path);
        Refresh();
        (works ? DefaultButton : _chooseButton).GrabFocus();
    }

    // Focus can only be grabbed once the screen is in the tree, so the first layout is redone here.
    public override void _Ready()
    {
        _shown = null;
        Refresh();
    }

    public override void _Process(double delta)
    {
        _flow.Tick();
        Refresh();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // A running copy keeps the window: Esc or B asks it to stop instead of quitting under it.
        // A delete cannot stop part way, so Esc waits for it.
        if (@event.IsActionPressed("ui_cancel") && _flow.View is SourceView.Copying or SourceView.Deleting)
        {
            _flow.Cancel();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _ExitTree()
    {
        _flow.Cancel();
        ScreenKeyboard.Hide(KeyboardOwner);
    }

    // The nearest existing folder at or above a typed path, so the picker opens somewhere near it.
    private static string? NearestFolder(string typed)
    {
        string? dir = typed.Trim().Trim('"');
        while (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return string.IsNullOrEmpty(dir) ? null : dir;
    }

    private static Label Line(string text, int fontSize, Color colour)
    {
        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", colour);
        return label;
    }

    private static Button MakeButton(string text, Action pressed)
    {
        var button = new Button { Text = text, CustomMinimumSize = ButtonSize };
        button.Pressed += pressed;
        return button;
    }

    // One theme for the screen and its picker, so both read at the same size on a handheld.
    private static Theme ScreenTheme()
    {
        var theme = new Theme { DefaultFontSize = BodyFontSize };
        theme.SetStylebox("normal", "Button", Box(new Color(0.12f, 0.14f, 0.19f), new Color(0.32f, 0.36f, 0.44f), 1));
        theme.SetStylebox("hover", "Button", Box(new Color(0.18f, 0.21f, 0.28f), new Color(0.45f, 0.50f, 0.60f), 1));
        theme.SetStylebox("pressed", "Button", Box(new Color(0.24f, 0.22f, 0.16f), TitleColour, 2));
        theme.SetStylebox("focus", "Button", Box(new Color(0, 0, 0, 0), TitleColour, 2));
        theme.SetColor("font_color", "Button", BodyColour);
        theme.SetColor("font_focus_color", "Button", TitleColour);
        theme.SetColor("font_hover_color", "Button", new Color(1, 1, 1));
        theme.SetStylebox("normal", "LineEdit", Box(new Color(0.08f, 0.09f, 0.12f), new Color(0.32f, 0.36f, 0.44f), 1));
        theme.SetStylebox("focus", "LineEdit", Box(new Color(0, 0, 0, 0), TitleColour, 2));
        theme.SetStylebox("background", "ProgressBar", Box(new Color(0.08f, 0.09f, 0.12f), new Color(0.32f, 0.36f, 0.44f), 1));
        theme.SetStylebox("fill", "ProgressBar", Box(new Color(0.72f, 0.60f, 0.32f), new Color(0.72f, 0.60f, 0.32f), 0));
        return theme;
    }

    private static StyleBoxFlat Box(Color fill, Color border, int borderPx)
    {
        var box = new StyleBoxFlat { BgColor = fill, BorderColor = border };
        box.SetBorderWidthAll(borderPx);
        box.SetCornerRadiusAll(3);
        box.SetContentMarginAll(8);
        return box;
    }

    private void Compose()
    {
        var theme = ScreenTheme();
        var backdrop = new ColorRect { Color = new Color(0.04f, 0.05f, 0.07f) };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(backdrop);

        // The margin gives the wrapped lines a measure. A box on the bare full rect wraps against
        // the window edge, which reads as text falling off the screen.
        var margin = new MarginContainer { Theme = theme };
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach (string side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, EdgeMarginPx);
        }

        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        box.AddThemeConstantOverride("separation", 18);
        _title = Line(string.Empty, TitleFontSize, TitleColour);
        _body = Line(string.Empty, BodyFontSize, BodyColour);
        box.AddChild(_title);
        box.AddChild(_body);
        box.AddChild(Line($"Data folder:  {_flow.DataRoot}", BodyFontSize, DimColour));
        box.AddChild(_field = FieldView());
        box.AddChild(_progress = ProgressView());
        _notice = Line(string.Empty, BodyFontSize, NoticeColour);
        box.AddChild(_notice);
        _row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _row.AddThemeConstantOverride("separation", 16);
        box.AddChild(_row);
        box.AddChild(_hint = Line(string.Empty, BodyFontSize - 2, DimColour));
        margin.AddChild(box);
        AddChild(margin);

        Picker = InstallPicker.Create(theme);
        Picker.Chosen = TakePick;
        Picker.Canceled += () => _chooseButton.GrabFocus();
        var pickerHint = Line(
            "Controller: d-pad moves, A opens a folder, LB goes up a folder, Y chooses the folder shown, B closes.",
            BodyFontSize - 2, DimColour);

        // A wrapping label reports its height at a width of one character. The dialog grows to
        // its contents' minimum, so that would make it taller than the screen.
        pickerHint.AutowrapMode = TextServer.AutowrapMode.Off;
        Picker.GetVBox().AddChild(pickerHint);
        AddChild(Picker);
        Refresh();
    }

    private Control FieldView()
    {
        var view = new VBoxContainer();
        view.AddThemeConstantOverride("separation", 12);
        view.AddChild(Line("Crimson Skies install folder, game disc or disc image", BodyFontSize, DimColour));
        _path = new LineEdit
        {
            Text = _flow.InstallPath,
            PlaceholderText = "The folder that holds the ZBD and GOSDATA folders, a drive, or an .iso, .cue or .ccd file",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, ButtonSize.Y),
        };
        _path.TextChanged += text => _flow.InstallPath = text;
        _path.TextSubmitted += _ =>
        {
            ScreenKeyboard.Hide(KeyboardOwner);
            _flow.Use();
            Refresh();
        };
        _path.GuiInput += RaiseKeyboard;
        _path.FocusExited += () => ScreenKeyboard.Hide(KeyboardOwner);
        _chooseButton = MakeButton("Choose...", () => OpenPicker());
        var field = new HBoxContainer();
        field.AddThemeConstantOverride("separation", 12);
        field.AddChild(_path);
        field.AddChild(_chooseButton);
        view.AddChild(field);
        return view;
    }

    private Control ProgressView()
    {
        var view = new VBoxContainer();
        view.AddThemeConstantOverride("separation", 12);
        _bar = new ProgressBar { MinValue = 0, MaxValue = 100, CustomMinimumSize = new Vector2(0, 28) };
        _line = Line(string.Empty, BodyFontSize - 2, DimColour);
        _line.AutowrapMode = TextServer.AutowrapMode.Off;
        _line.ClipText = true;
        _line.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        view.AddChild(_bar);
        view.AddChild(_line);
        return view;
    }

    // A pad's A or a tap on the folder field raises the on-screen keyboard. Focus alone never does:
    // the d-pad crosses the field on its way to the buttons.
    private void RaiseKeyboard(InputEvent @event)
    {
        bool press = @event is InputEventJoypadButton { Pressed: true } && @event.IsActionPressed("ui_accept")
            || @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }
            || @event is InputEventScreenTouch { Pressed: true };
        if (press && ScreenKeyboard.Show(new ScreenKeyboardField(KeyboardOwner, "install-folder",
            "Crimson Skies install folder", () => _path.Text)))
        {
            _path.AcceptEvent();
        }
    }

    // Runs the launcher's continuation at most once, whichever way the screen is left.
    private void Continue()
    {
        if (_continued)
        {
            return;
        }

        _continued = true;
        _continue();
    }

    // The view's buttons from the flow's choices, Quit last while nothing is running.
    private void RebuildButtons(SourceView view)
    {
        foreach (var old in _buttons)
        {
            _row.RemoveChild(old);
            old.QueueFree();
        }

        _buttons.Clear();
        foreach (var choice in _flow.Choices)
        {
            _buttons.Add(MakeButton(choice.Label, () =>
            {
                choice.Press();
                Refresh();
            }));
        }

        if (view is not (SourceView.Copying or SourceView.Deleting))
        {
            _buttons.Add(MakeButton("Quit", () => _quit()));
        }

        foreach (var button in _buttons)
        {
            _row.AddChild(button);
        }
    }

    // Lays the screen out for the flow's view. A view change rebuilds the buttons and moves focus
    // to the first, so a pad or the Enter key always has something to press.
    private void Refresh()
    {
        var view = _flow.View;
        if (view == SourceView.Done)
        {
            Continue();
            return;
        }

        bool changed = _shown != view;
        _shown = view;
        if (changed)
        {
            RebuildButtons(view);
        }

        _title.Text = _flow.Title;
        _body.Text = _flow.Body;
        _field.Visible = view == SourceView.Asking;
        _progress.Visible = view is SourceView.Copying or SourceView.Deleting;
        _notice.Text = _flow.Notice ?? string.Empty;
        _notice.Visible = _flow.Notice != null;
        if (_path.Text != _flow.InstallPath)
        {
            _path.Text = _flow.InstallPath;
        }

        _bar.Value = view == SourceView.Deleting ? 0 : _flow.Fraction * 100;
        _line.Text = _flow.Cancelling ? "Cancelling..." : _flow.ProgressLine;
        _hint.Text = view switch
        {
            SourceView.Copying => "Esc or B cancels the copy.",
            SourceView.Deleting => "The delete runs to the end once started.",
            _ => "Arrow keys or d-pad to move, Enter or A to press, Esc to quit.",
        };
        if (changed && IsInsideTree() && _buttons.Count > 0)
        {
            DefaultButton.GrabFocus();
        }
    }
}
