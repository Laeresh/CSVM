using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.UI.Screens;

/// <summary>
/// A strip across the top of the screen repeating the field Steam's on-screen keyboard types
/// into, with its label and a caret. The keyboard covers the lower half of the
/// screen, and a field drawn there would otherwise be typed into blind. Built once by
/// <see cref="CSVM.Launch.Launcher"/>; it shows whenever <see cref="ScreenKeyboard.Shown"/> names
/// a field that asks to be echoed, and reads that field's text again every frame.
/// </summary>
public sealed partial class ScreenKeyboardEcho : Node
{
    // Authored against the launchscreen's own 720p metrics, a little above its body text so the
    // line reads at arm's length on a handheld.
    private const float ReferenceFontSize = 24f;
    private const float ReferenceHeight = 720f;
    private const float ReferencePadPx = 12f;

    private static readonly Color BandColour = new(0.04f, 0.05f, 0.07f, 0.92f);
    private static readonly Color LabelColour = new(0.58f, 0.62f, 0.69f);
    private static readonly Color TextColour = new(0.93f, 0.83f, 0.55f);

    private CanvasLayer _layer = null!;
    private ColorRect _band = null!;
    private RichTextLabel _line = null!;
    private string _shown = string.Empty;

    public ScreenKeyboardEcho()
    {
        Name = "screen_keyboard_echo";
    }

    /// <summary>Gets the line as the strip draws it now, empty while it is hidden.</summary>
    public string Line => _layer.Visible ? _shown : string.Empty;

    /// <summary>The strip's words for <paramref name="field"/>: its label, then its text and a
    /// caret. A field with no label shows its text alone.</summary>
    public static string Compose(ScreenKeyboardField field)
    {
        System.ArgumentNullException.ThrowIfNull(field);
        string text = field.Text() + "_";
        return field.Label.Length == 0 ? text : field.Label + ":  " + text;
    }

    public override void _Ready()
    {
        _layer = new CanvasLayer { Layer = HudLayers.KeyboardEcho, Visible = false };
        _band = new ColorRect { Color = BandColour, MouseFilter = Control.MouseFilterEnum.Ignore };
        _line = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.Off,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _band.AddChild(_line);
        _layer.AddChild(_band);
        AddChild(_layer);
    }

    public override void _Process(double delta)
    {
        if (ScreenKeyboard.Shown is not { Echoed: true } field)
        {
            _layer.Visible = false;
            return;
        }

        string line = Compose(field);
        if (!_layer.Visible || line != _shown)
        {
            Layout(field, line);
        }

        _layer.Visible = true;
    }

    // Typed text is the player's own, so a bracket in it must not be read as markup.
    private static string Escape(string text) => text.Replace("[", "[lb]", System.StringComparison.Ordinal);

    // Sized from the window each time the words change, so a resize while typing is followed on the
    // next character. The label is dimmer than the text, which is what the player is checking.
    private void Layout(ScreenKeyboardField field, string line)
    {
        _shown = line;
        var size = _band.GetViewport().GetVisibleRect().Size;
        float scale = size.Y / ReferenceHeight;
        int font = Mathf.RoundToInt(ReferenceFontSize * scale);
        float pad = ReferencePadPx * scale;
        _line.AddThemeFontSizeOverride("normal_font_size", font);
        _line.Position = new Vector2(pad, pad);
        _line.Size = new Vector2(size.X - (2f * pad), font * 1.4f);
        _band.Position = Vector2.Zero;
        _band.Size = new Vector2(size.X, (font * 1.4f) + (2f * pad));
        string text = Escape(field.Text() + "_");
        _line.Text = field.Label.Length == 0
            ? $"[color=#{TextColour.ToHtml(false)}]{text}[/color]"
            : $"[color=#{LabelColour.ToHtml(false)}]{Escape(field.Label)}:[/color]  [color=#{TextColour.ToHtml(false)}]{text}[/color]";
    }
}
