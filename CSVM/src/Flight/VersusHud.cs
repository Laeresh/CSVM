using System.Linq;
using CSVM.UI;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// Per-pane Dogfight HUD (PLAN-vs-mode C23): one compact status line — remaining time (omitted
/// once <see cref="VersusMatch.TimeLimit"/> is disabled), this player's own kills/deaths, and the
/// current leader's tag — plus a transient "P2 DOWNED P3" banner on every Downed report anywhere
/// in the match (a plain "P3 DOWN" when the crash carried no killer). Sits in the same screen
/// slot MarkerHud's run-status line uses — Stunt and Versus are mutually exclusive modes, so the
/// two never compete for it.
///
/// <para>The status line pulls <see cref="VersusMatch"/> live each frame: its own bookkeeping is
/// already current by the time the HUD draws, so unlike MarkerHud there is no world pose to
/// project. The banner is pushed once per fact via <see cref="OnKill"/> — GameSession's own
/// Downed subscription, a second one never piggybacked on the scoring handler, broadcast to
/// every pane so the whole field sees who went down.</para>
/// </summary>
public sealed partial class VersusHud : Control
{
    /// <summary>Which pane this draws in (0-based) — this pane's own K/D.</summary>
    public int PlayerIndex;

    // 1440p reference metrics (scaled by HudMetrics — matches MarkerHud's calibration).
    private const int RefStatusFont = 19;
    private const int RefBannerFont = 24;
    private const float RefStatusY = 100f;    // same slot as MarkerHud's run-status line
    private const float RefBannerYFrac = 0.30f;
    private const float BannerDuration = 3f;  // s the banner shows
    private const float BannerFadeTail = 0.6f; // s of that spent fading out

    private static readonly Color HudBlue = new(0.55f, 0.78f, 1f);
    private static readonly Color HudRed = new(1f, 0.55f, 0.55f);
    private static readonly Color Shadow = new(0f, 0f, 0f, 0.75f);

    private VersusMatch _match = null!;
    private float _bannerTime;
    private string _bannerText = "";
    private Color _bannerColor = HudBlue;

    /// <summary>Binds the match. Add to the HUD canvas; nothing else needs feeding each frame —
    /// the match's own state is always current.</summary>
    public static VersusHud Build(VersusMatch match, int playerIndex)
    {
        return new VersusHud
        {
            _match = match,
            PlayerIndex = playerIndex,
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
        };
    }

    /// <summary>A Downed report anywhere in the match: killer named when it was a weapon kill, a
    /// plain "DOWN" otherwise (terrain/mid-air — no killer to name).</summary>
    public void OnKill(int? killer, int victim)
    {
        _bannerTime = BannerDuration;
        if (killer is int k)
        {
            _bannerText = $"{SplitScreen.PlayerTag(k)} DOWNED {SplitScreen.PlayerTag(victim)}";
            _bannerColor = SplitScreen.PlayerColor(k);
        }
        else
        {
            _bannerText = $"{SplitScreen.PlayerTag(victim)} DOWN";
            _bannerColor = HudRed;
        }
    }

    public override void _Process(double delta)
    {
        // Track the pane (resizable window / splitscreen layout) and repaint every frame — the
        // clock and the banner fade both need it.
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
        if (_bannerTime > 0f)
            _bannerTime -= (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        // Same zero-size guard as MarkerHud: a draw can land before _Process has sized this pane.
        float s = Size.Y <= 0f ? 0f : HudMetrics.Scale(this);
        if (s <= 0f)
            return;
        var font = GetThemeDefaultFont();
        int statusFont = Mathf.Max(1, Mathf.RoundToInt(RefStatusFont * s));
        float cx = Size.X / 2f;

        DrawCentered(font, new Vector2(cx, RefStatusY * s), StatusLine(), statusFont, HudBlue);

        if (_bannerTime > 0f)
        {
            int bannerFont = Mathf.Max(1, Mathf.RoundToInt(RefBannerFont * s));
            float alpha = Mathf.Clamp(_bannerTime / BannerFadeTail, 0f, 1f);
            DrawCentered(font, new Vector2(cx, Size.Y * RefBannerYFrac), _bannerText, bannerFont,
                new Color(_bannerColor, alpha));
        }
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{total / 60}:{total % 60:00}";
    }

    private string StatusLine()
    {
        string time = _match.TimeLimit > 0f ? $"{FormatTime(_match.TimeRemaining)}   " : "";
        string kd = $"K/D {_match.KillsOf(PlayerIndex)}/{_match.DeathsOf(PlayerIndex)}";
        return $"{time}{kd}   {LeaderText()}";
    }

    /// <summary>The sole rank-1 player's tag, or "—" while tied (including 0-0 before the first
    /// kill — nobody leads yet).</summary>
    private string LeaderText()
    {
        var leaders = _match.Standings().Where(st => st.Rank == 1).ToList();
        return leaders.Count == 1 ? $"LEADER {SplitScreen.PlayerTag(leaders[0].PlayerIndex)}" : "LEADER —";
    }

    /// <summary>Draws one horizontally-centred line at <paramref name="anchor"/>.X, top-anchored
    /// at .Y, with a 1 px drop shadow — MarkerHud's DrawLines, single-line.</summary>
    private void DrawCentered(Font font, Vector2 anchor, string text, int fontSize, Color color)
    {
        float ascent = font.GetAscent(fontSize);
        float w = font.GetStringSize(text, HorizontalAlignment.Left, -1f, fontSize).X;
        var p = new Vector2(anchor.X - w / 2f, anchor.Y + ascent);
        DrawString(font, p + Vector2.One, text, HorizontalAlignment.Left, -1f, fontSize, Shadow);
        DrawString(font, p, text, HorizontalAlignment.Left, -1f, fontSize, color);
    }
}
