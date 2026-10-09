using System;
using System.Linq;
using CSVM.Flight.Hud;
using CSVM.UI.Boards;
using Godot;

namespace CSVM.Flight.Modes;

/// <summary>Per-pane versus match status line, every <c>--vs</c> mode (docs/architecture.md): time
/// remaining, this pane's own K/D and the leader, in <see cref="StuntRunHud"/>'s run-status slot.
/// It is the only in-flight readout of match time and score. Players find and mark each other
/// through <see cref="TargetHud"/>, as in the original; a kill goes to <see cref="HudMessages"/>.
/// </summary>
public sealed partial class VersusStatusLine : Control
{
    /// <summary>Which pane this draws in (0-based), whose own K/D the line reads.</summary>
    public int PlayerIndex;

    // The 1440p reference, scaled by HudMetrics: the same slot as StuntRunHud's run-status line.
    private const float RefStatusY = 100f;

    // The line's size, a chrome type scale rung in the same reference.
    private static readonly float RefStatusFont = ChromeType.InReference(ChromeSize.Readout, HudMetrics.ReferenceHeight);

    private static readonly Color HudBlue = new(0.55f, 0.78f, 1f);
    private static readonly Color Shadow = new(0f, 0f, 0f, 0.75f);

    private VersusMatch? _match;

    /// <summary>What sends the line away while it answers true: the pane's held scores, whose
    /// Original text runs across the top of the pane. Null keeps it.</summary>
    public Func<bool>? StatusHiddenWhile { get; set; }

    /// <summary>A seat's callsign when a bot flies it, else null. The line then names a bot
    /// leader by callsign and a person by player tag, as the death line does. Null names every seat
    /// by tag.</summary>
    public Func<int, string?>? BotName { get; set; }

    /// <summary>Whether the line draws this frame.</summary>
    public bool StatusShown => StatusHiddenWhile?.Invoke() != true;

    /// <summary>Binds the match and this pane's seat. Add to the HUD canvas; nothing needs feeding,
    /// the match's own state is always current.</summary>
    public static VersusStatusLine Build(VersusMatch match, int playerIndex)
    {
        return new VersusStatusLine
        {
            _match = match,
            PlayerIndex = playerIndex,
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
        };
    }

    /// <summary>The sole rank-1 seat's name, or TIED while nobody leads (including 0-0 before the
    /// first kill). A team match names pane <paramref name="playerIndex"/>'s team and its total,
    /// then the leading team. <paramref name="botName"/> is <see cref="BotName"/>.</summary>
    public static string LeaderText(VersusMatch match, int playerIndex, Func<int, string?>? botName)
    {
        ArgumentNullException.ThrowIfNull(match);
        if (match.Teamed)
        {
            int own = match.TeamOf(playerIndex);
            string mine = own > 0 ? $"{match.TeamName(own)} {match.TeamScoreOf(own)}   " : "";
            var teams = match.TeamStandings().Where(t => t.Rank == 1).ToList();
            return mine + (teams.Count == 1 ? $"LEADER {teams[0].Name}" : "LEADER TIED");
        }

        var leaders = match.Standings().Where(st => st.Rank == 1).ToList();
        if (leaders.Count != 1)
            return "LEADER TIED";
        int seat = leaders[0].PlayerIndex;
        return $"LEADER {botName?.Invoke(seat) ?? SplitScreen.PlayerTag(seat)}";
    }

    public override void _Process(double delta)
    {
        // Track the pane (resizable window / splitscreen layout) and repaint every frame, the
        // clock needs it.
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
        QueueRedraw();
    }

    public override void _Draw()
    {
        // Same zero-size guard as TargetHud: a draw can land before _Process has sized this pane.
        float s = Size.Y <= 0f ? 0f : HudMetrics.Scale(this);
        if (s <= 0f || _match is not { } match || !StatusShown)
            return;
        var font = ChromeType.Face(this);
        int statusFont = Mathf.Max(1, Mathf.RoundToInt(RefStatusFont * s * HudMetrics.StatusTextScale));
        DrawCentered(font, new Vector2(Size.X / 2f, RefStatusY * s), StatusLine(match), statusFont, HudBlue);
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{total / 60}:{total % 60:00}";
    }

    private string StatusLine(VersusMatch match)
    {
        string time = match.TimeLimit > 0f ? $"{FormatTime(match.TimeRemaining)}   " : "";
        string kd = $"K/D {match.KillsOf(PlayerIndex)}/{match.DeathsOf(PlayerIndex)}";
        return $"{time}{kd}   {LeaderText(match, PlayerIndex, BotName)}";
    }

    // Draws one horizontally-centred line at `anchor`.X, top-anchored
    // at .Y, with a 1 px drop shadow, MarkerDraw's Lines, single-line.
    private void DrawCentered(Font font, Vector2 anchor, string text, int fontSize, Color color)
    {
        float ascent = font.GetAscent(fontSize);
        float w = font.GetStringSize(text, HorizontalAlignment.Left, -1f, fontSize).X;
        var p = new Vector2(anchor.X - w / 2f, anchor.Y + ascent);
        DrawString(font, p + Vector2.One, text, HorizontalAlignment.Left, -1f, fontSize, Shadow);
        DrawString(font, p, text, HorizontalAlignment.Left, -1f, fontSize, color);
    }
}
