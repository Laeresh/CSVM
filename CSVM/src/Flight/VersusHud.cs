using System.Collections.Generic;
using System.Linq;
using CSVM.UI;
using Godot;

namespace CSVM.Flight;

/// <summary>Per-pane Dogfight HUD, <c>--vs</c> only (docs/architecture.md): a status line (time,
/// this pane's own K/D, the leader), a transient kill banner on every Downed report anywhere in the
/// match, and an edge-arrow + clock-bearing marker per living opponent in that opponent's identity
/// colour — the splitscreen answer to the original's radar, extending MarkerHud's visual language.
/// The single-target marker this shape came from is <see cref="TargetHud"/>'s, which draws in every
/// flight session rather than only <c>--vs</c>.
/// ⚠ Read opponent positions off <see cref="Rigs"/> and project through THIS pane's camera. Never
/// <c>AnimRuntime.PlayerPosition</c>: it is a P1-only singleton.</summary>
public sealed partial class VersusHud : Control
{
    /// <summary>Which pane this draws in (0-based) — this pane's own K/D, and the identity every
    /// opponent marker excludes.</summary>
    public int PlayerIndex;

    /// <summary>Every rig in the session (this player's own included — skipped when drawing
    /// markers), set once the whole field is built. Null in a solo <c>--vs --players=1</c>
    /// session — nothing to mark.</summary>
    public IReadOnlyList<PlayerRig>? Rigs;

    // 1440p reference metrics (scaled by HudMetrics — matches MarkerHud's calibration).
    private const int RefStatusFont = 19;
    private const int RefBannerFont = 24;
    private const int RefMarkerFont = 14;
    private const float RefStatusY = 100f;    // same slot as MarkerHud's run-status line
    private const float RefBannerYFrac = 0.30f;
    private const float BannerDuration = 3f;  // s the banner shows
    private const float BannerFadeTail = 0.6f; // s of that spent fading out
    private const float RefEdgeMargin = 46f;  // keep edge markers this far off the screen border
    private const float RefArrowLen = 18f;
    private const float RefArrowHalf = 8f;
    private const float RefTextGap = 8f;
    private const float RefOnScreenLift = 22f; // gap above a plane's own projected point

    private static readonly Color HudBlue = new(0.55f, 0.78f, 1f);
    private static readonly Color HudRed = new(1f, 0.55f, 0.55f);
    private static readonly Color Shadow = new(0f, 0f, 0f, 0.75f);

    private VersusMatch? _match;
    private Camera3D _camera = null!;
    private float _bannerTime;
    private string _bannerText = "";
    private Color _bannerColor = HudBlue;

    /// <summary>This pane's own world pose, fed every frame by FlightController — opponent clock
    /// bearings read off it, exactly like MarkerHud's PlanePos/HeadingDeg.</summary>
    public Vector3 PlanePos { get; set; }

    /// <summary>This pane's own nose heading, 0 = north (−Z) — see <see cref="PlanePos"/>.</summary>
    public float HeadingDeg { get; set; }

    /// <summary>Binds the match + this pane's own camera (opponent markers project through it).
    /// Add to the HUD canvas; <see cref="Rigs"/> is attached once the whole field is built, and
    /// <see cref="PlanePos"/>/<see cref="HeadingDeg"/> every frame — nothing else needs feeding,
    /// the match's own state is always current.</summary>
    public static VersusHud Build(VersusMatch match, int playerIndex, Camera3D camera)
    {
        return new VersusHud
        {
            _match = match,
            PlayerIndex = playerIndex,
            _camera = camera,
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

        if (_match is { } match)
            DrawCentered(font, new Vector2(cx, RefStatusY * s), StatusLine(match), statusFont, HudBlue);

        if (_bannerTime > 0f)
        {
            int bannerFont = Mathf.Max(1, Mathf.RoundToInt(RefBannerFont * s));
            float alpha = Mathf.Clamp(_bannerTime / BannerFadeTail, 0f, 1f);
            DrawCentered(font, new Vector2(cx, Size.Y * RefBannerYFrac), _bannerText, bannerFont,
                new Color(_bannerColor, alpha));
        }

        if (Rigs == null)
            return;
        int markerFont = Mathf.Max(1, Mathf.RoundToInt(RefMarkerFont * s));
        foreach (var opp in Rigs)
        {
            if (opp.Index == PlayerIndex || opp.Controller is not { Crashed: false } c)
                continue; // this pane's own seat, or an opponent out of the fight for now
            DrawOpponent(font, c.GlobalPosition, SplitScreen.PlayerColor(opp.Index),
                SplitScreen.PlayerTag(opp.Index), s, markerFont);
        }
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{total / 60}:{total % 60:00}";
    }

    // Screen-edge point along `dir` from centre, inset by the margin —
    // MarkerHud's EdgePoint verbatim.
    private static Vector2 EdgePoint(Vector2 center, Vector2 dir, float margin)
    {
        float hx = center.X - margin, hy = center.Y - margin;
        float tx = Mathf.Abs(dir.X) > 1e-4f ? hx / Mathf.Abs(dir.X) : float.MaxValue;
        float ty = Mathf.Abs(dir.Y) > 1e-4f ? hy / Mathf.Abs(dir.Y) : float.MaxValue;
        return center + dir * Mathf.Min(tx, ty);
    }

    private string StatusLine(VersusMatch match)
    {
        string time = match.TimeLimit > 0f ? $"{FormatTime(match.TimeRemaining)}   " : "";
        string kd = $"K/D {match.KillsOf(PlayerIndex)}/{match.DeathsOf(PlayerIndex)}";
        return $"{time}{kd}   {LeaderText(match)}";
    }

    // The sole rank-1 player's tag, or "—" while tied (including 0-0 before the first
    // kill — nobody leads yet).
    private string LeaderText(VersusMatch match)
    {
        var leaders = match.Standings().Where(st => st.Rank == 1).ToList();
        return leaders.Count == 1 ? $"LEADER {SplitScreen.PlayerTag(leaders[0].PlayerIndex)}" : "LEADER —";
    }

    // One opponent's marker: on screen, their tag floats just above the projected
    // point; off screen (or behind), an edge arrow + "N o'clock" bearing — MarkerHud's on-screen/
    // edge-arrow branch, one instance per opponent instead of one stunt zone. TargetHud keeps its
    // own copy of this for the selected target and --debug-markers.
    private void DrawOpponent(Font font, Vector3 pos, Color color, string tag, float s, int fontSize)
    {
        bool behind = _camera.IsPositionBehind(pos);
        Vector2 sp = _camera.UnprojectPosition(pos);
        float m = RefEdgeMargin * s;
        var inner = new Rect2(m, m, Size.X - 2f * m, Size.Y - 2f * m);
        if (!behind && inner.HasPoint(sp))
        {
            DrawTag(font, sp + new Vector2(0f, -RefOnScreenLift * s), tag, color, fontSize);
            return;
        }
        var center = Size / 2f;
        var dir = sp - center;
        if (behind)
            dir = -dir; // the projection of a point behind the camera is mirrored through centre
        if (dir.LengthSquared() < 1f)
            dir = Vector2.Down;
        dir = dir.Normalized();
        var edge = EdgePoint(center, dir, m);
        DrawArrow(edge, dir, RefArrowLen * s, RefArrowHalf * s, s, color);
        DrawTag(font, edge - dir * (RefArrowLen + RefTextGap) * s,
            $"{tag}  {ClockHour(pos)} o'clock", color, fontSize);
    }

    // Relative bearing of `targetPos` from this pilot's own heading in
    // clock hours (12 = ahead, 3 = right, 6 = behind, 9 = left) — MarkerHud.ClockHour over an
    // opponent instead of a danger zone.
    private int ClockHour(Vector3 targetPos)
    {
        var d = targetPos - PlanePos;
        float bearing = Mathf.RadToDeg(Mathf.Atan2(d.X, -d.Z)); // 0 = N (−Z), 90 = E (+X)
        float rel = Mathf.PosMod(bearing - HeadingDeg, 360f);
        int h = Mathf.RoundToInt(rel / 30f) % 12;
        return h == 0 ? 12 : h;
    }

    private void DrawArrow(Vector2 tip, Vector2 dir, float len, float half, float s, Color color)
    {
        var perp = new Vector2(-dir.Y, dir.X);
        var b1 = tip - dir * len + perp * half;
        var b2 = tip - dir * len - perp * half;
        var off = new Vector2(1.5f, 1.5f) * s;
        DrawColoredPolygon(new[] { tip + off, b1 + off, b2 + off }, Shadow);
        DrawColoredPolygon(new[] { tip, b1, b2 }, color);
    }

    private void DrawTag(Font font, Vector2 center, string text, Color color, int fontSize)
    {
        float w = font.GetStringSize(text, HorizontalAlignment.Left, -1f, fontSize).X;
        float h = font.GetHeight(fontSize);
        var p = new Vector2(center.X - w / 2f, center.Y - h / 2f + font.GetAscent(fontSize));
        DrawString(font, p + Vector2.One, text, HorizontalAlignment.Left, -1f, fontSize, Shadow);
        DrawString(font, p, text, HorizontalAlignment.Left, -1f, fontSize, color);
    }

    // Draws one horizontally-centred line at `anchor`.X, top-anchored
    // at .Y, with a 1 px drop shadow — MarkerHud's DrawLines, single-line.
    private void DrawCentered(Font font, Vector2 anchor, string text, int fontSize, Color color)
    {
        float ascent = font.GetAscent(fontSize);
        float w = font.GetStringSize(text, HorizontalAlignment.Left, -1f, fontSize).X;
        var p = new Vector2(anchor.X - w / 2f, anchor.Y + ascent);
        DrawString(font, p + Vector2.One, text, HorizontalAlignment.Left, -1f, fontSize, Shadow);
        DrawString(font, p, text, HorizontalAlignment.Left, -1f, fontSize, color);
    }
}
