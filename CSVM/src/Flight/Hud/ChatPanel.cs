using System;
using CSVM.Flight.Camera;
using Godot;

namespace CSVM.Flight.Hud;

/// <summary>
/// One pane's view of the machine's <see cref="FlightChat"/>. The held lines stand at the top left
/// in the original's <c>mpChat</c> green, with the entry line under them in the typist's pane. It
/// draws and nothing else. The keys belong to <c>Session.World.NetChatLink</c>, since a splitscreen
/// pane routes no input of its own. Decode: <c>docs/org/multiplayer-messages.md</c>
/// "In-flight chat".
/// </summary>
public sealed partial class ChatPanel : Control
{
    // The panel's box, set at 0x004a8404-0x004a842d: 10 px in from the top left, 300 by 100, in
    // display pixels at the original's 480-line mode. The five lines share the height,
    // which is the 20 px pitch. The mpChat def (extracted/zrdr/fonts.zrd.json: Arial, height -12,
    // weight 500, shadow true) is a 12 px cell. All three carry onto HudMetrics' 1440p reference by
    // the factor of three the message stack takes.
    private const float RefInset = 30f;
    private const float RefLinePitch = 60f;
    private const int RefFontSize = 36;

    // The mpChat def's colour, 0, 255, 0.
    private static readonly Color LineColor = new(0f, 1f, 0f);

    private FlightChat? _chat;
    private int _drawnPosts = -1;
    private bool _drawnShown;
    private string _drawnEntry = string.Empty;

    /// <summary>The chat this pane shows, or null for none.</summary>
    public FlightChat? Chat
    {
        get => _chat;
        set
        {
            _chat = value;
            QueueRedraw();
        }
    }

    /// <summary>Whether this pane's pilot is the one who types, so the entry line draws here. Only
    /// the seat that reads the keyboard can type a line.</summary>
    public bool ShowsEntry { get; set; }

    /// <summary>What hides the panel while it answers true: the pane's scores table, which the
    /// original's Display Scores handler clears the chat panel for. Null hides it never.</summary>
    public Func<bool>? HiddenWhile { get; set; }

    /// <summary>Whether the held lines draw this frame: the chat is up and nothing hides it.</summary>
    public bool LinesShown => _chat is { Shown: true } && HiddenWhile?.Invoke() != true;

    /// <summary>The panel for <paramref name="pane"/>: the entry where its seat reads the keyboard,
    /// and out of the way while that seat holds Display Scores. Every pane's chat is built here.
    /// </summary>
    public static ChatPanel ForPane(FlightChat chat, PlayerRig pane) => new()
    {
        Chat = chat,
        ShowsEntry = pane.Controller is { UseKeyboard: true },
        HiddenWhile = () => pane.Controller is { ScoresShown: true },
        MouseFilter = MouseFilterEnum.Ignore,
        FocusMode = FocusModeEnum.None,
    };

    public override void _Process(double delta)
    {
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
        if (_chat == null)
        {
            return;
        }

        // Redraw only on a change: a new line, the timer running out, a keystroke or the scores.
        string entry = ShowsEntry && _chat.Typing ? _chat.EntryLine : string.Empty;
        bool shown = LinesShown;
        if (_chat.Posted != _drawnPosts || shown != _drawnShown || entry != _drawnEntry)
        {
            _drawnPosts = _chat.Posted;
            _drawnShown = shown;
            _drawnEntry = entry;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        float s = !LinesShown || Size.Y <= 0f ? 0f : HudMetrics.Scale(this);
        if (s <= 0f)
        {
            return;
        }

        var font = GetThemeDefaultFont();
        int fontSize = Mathf.Max(1, Mathf.RoundToInt(RefFontSize * s * HudMetrics.StatusTextScale));
        var at = HudMetrics.ReadingBox(this).Position + (new Vector2(RefInset, RefInset) * s);
        at.Y += font.GetAscent(fontSize);
        foreach (var line in _chat!.Lines)
        {
            DrawLine(font, fontSize, at, line);
            at.Y += RefLinePitch * s;
        }

        if (ShowsEntry && _chat.Typing)
        {
            DrawLine(font, fontSize, at, _chat.EntryLine);
        }
    }

    // One line with the def's drop shadow, the same one-pixel offset the message stack draws.
    private void DrawLine(Font font, int fontSize, Vector2 baseline, string line)
    {
        DrawString(font, baseline + Vector2.One, line, HorizontalAlignment.Left, -1f, fontSize, MarkerDraw.Shadow);
        DrawString(font, baseline, line, HorizontalAlignment.Left, -1f, fontSize, LineColor);
    }
}
