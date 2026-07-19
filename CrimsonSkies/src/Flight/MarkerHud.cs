using System.Collections.Generic;
using Godot;

namespace CrimsonSkies.Flight;

/// <summary>
/// The original's Stunt Flying objective marker (Milestone 2.5 item 2), rebuilt as a HUD Control
/// over the flight view. It guides the player to the active Danger Zone:
///  • on screen → the zone's text block floats at its projected position (name + live distance),
///    a small reticle marking the point;
///  • off screen / behind → the block clamps to the screen edge with an arrow pointing the
///    shortest way toward it, plus the "N o'clock" relative bearing — matching
///    OriginalScreenshots/C1 IA1 Cloudcoverage 1.png ("Danger Zone [Fly Through] - Train Tunnel
///    Mid / 7 o'clock").
/// Plus the run-status line (elapsed m:ss.t + zones done), the one-shot intro banner, an
/// all-complete banner, and a brief zone-cleared flash. The displayed target follows
/// StuntMission's auto-advance and the pilot's manual cycling (Tab / gamepad, in FlightController).
///
/// Follows the CompassTape/GaugeCluster pattern: a viewport-filling Control fed the plane pose
/// each frame, projecting through the live camera at _Draw time (no cached projection, so the
/// marker never lags the chase camera). Screen metrics scale by viewport height off the 1440p
/// reference; colours + sizes are TUNE. The circular live-camera objective inset next to the
/// original's marker is out of scope (backlog) — this draws the arrow + text only.
/// </summary>
public sealed partial class MarkerHud : Control
{
    /// <summary>The plane's world position this frame (target distance + clock bearing).</summary>
    public Vector3 PlanePos { get; set; }

    /// <summary>The plane nose heading, 0 = north (−Z), 90 = east (+X) — for the clock bearing.</summary>
    public float HeadingDeg { get; set; }

    private StuntMission _mission = null!;
    private Camera3D _camera = null!;
    private float _flash;         // s left on the zone-cleared flash
    private string _flashText = "";

    // 1440p reference metrics (scaled by viewport height).
    private const int RefMarkerFont = 15;   // the marker's small text block
    private const int RefStatusFont = 19;    // the run-status line
    private const int RefBannerFont = 26;    // intro / all-complete banners
    private const float RefStatusY = 100f;   // run-status baseline y, just under the compass tape
    private const float RefEdgeMargin = 46f; // keep edge markers this far off the screen border
    private const float RefArrowLen = 20f;   // arrowhead length
    private const float RefArrowHalf = 9f;   // arrowhead half-width
    private const float RefTextGap = 10f;    // gap from the projected point / arrow to the text
    private const float RefReticleR = 9f;    // on-screen target reticle radius
    private const float IntroDuration = 5f;  // s the run-start line shows (fades the last second)
    private const float FlashDuration = 1.6f;// s a zone-cleared flash shows

    private static readonly Color HudBlue = new(0.55f, 0.78f, 1f);
    private static readonly Color HudGreen = new(0.60f, 1f, 0.70f); // completion feedback
    private static readonly Color Shadow = new(0f, 0f, 0f, 0.75f);

    /// <summary>Binds the run + camera; subscribes to the completion flash. Add to the HUD canvas
    /// and feed <see cref="PlanePos"/>/<see cref="HeadingDeg"/> each frame (FlightController).</summary>
    public static MarkerHud Build(StuntMission mission, Camera3D camera)
    {
        var hud = new MarkerHud
        {
            _mission = mission,
            _camera = camera,
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
        };
        mission.ZoneCompleted += hud.OnZoneCompleted;
        return hud;
    }

    public override void _ExitTree() => _mission.ZoneCompleted -= OnZoneCompleted;

    private void OnZoneCompleted(StuntZone z)
    {
        _flash = FlashDuration;
        _flashText = z.Description.Length > 0 ? $"{z.Description} — CLEARED" : "DANGER ZONE CLEARED";
    }

    public override void _Process(double delta)
    {
        // Track the viewport (resizable window) and repaint — the marker moves every frame.
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
        if (_flash > 0f)
            _flash -= (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        // A draw can land before _Process has sized us to the viewport (and, in splitscreen,
        // before a pane has been laid out): every derived metric would be 0 and Godot's font
        // cache errors out on a zero size. Nothing to draw at zero height anyway.
        // The marker scales like the rest of the HUD: window height against the 1440p reference,
        // damped by this pane's share of it so a 4P quarter-pane marker stays readable (HudMetrics).
        float s = Size.Y <= 0f ? 0f : HudMetrics.Scale(this);
        if (s <= 0f)
            return;
        var font = GetThemeDefaultFont();
        // Never round a scaled font down to 0 — a quarter-height 4P pane scales hard.
        int markerFont = Mathf.Max(1, Mathf.RoundToInt(RefMarkerFont * s));
        int statusFont = Mathf.Max(1, Mathf.RoundToInt(RefStatusFont * s));
        int bannerFont = Mathf.Max(1, Mathf.RoundToInt(RefBannerFont * s));
        float cx = Size.X / 2f;

        // Run-status: elapsed time + zones done, top-centre under the compass tape.
        DrawLines(font, new Vector2(cx, RefStatusY * s),
            new[] { $"{StuntMission.FormatTime(_mission.Elapsed)}    ZONES {_mission.CompletedCount}/{_mission.TotalCount}" },
            statusFont, HudBlue, topAnchored: true);

        // One-shot intro banner (fades over the last second of its window).
        if (_mission.Elapsed < IntroDuration && !_mission.AllComplete && _mission.IntroLine.Length > 0)
        {
            float a = Mathf.Clamp(IntroDuration - _mission.Elapsed, 0f, 1f);
            DrawLines(font, new Vector2(cx, Size.Y * 0.26f), new[] { _mission.IntroLine },
                bannerFont, new Color(HudBlue, a));
        }

        // Brief zone-cleared flash (fades over its last 0.4 s).
        if (_flash > 0f)
            DrawLines(font, new Vector2(cx, Size.Y * 0.34f), new[] { _flashText }, bannerFont,
                new Color(HudGreen, Mathf.Clamp(_flash / 0.4f, 0f, 1f)));

        if (_mission.AllComplete)
        {
            DrawLines(font, new Vector2(cx, Size.Y * 0.26f),
                new[] { "ALL DANGER ZONES CLEARED", StuntMission.FormatTime(_mission.Elapsed) }, bannerFont, HudGreen);
            return;
        }

        var zone = _mission.ActiveZone;
        if (zone == null)
            return;

        // Project the zone through the live camera (at draw time, so it can't lag the chase cam).
        bool behind = _camera.IsPositionBehind(zone.Position);
        Vector2 sp = _camera.UnprojectPosition(zone.Position);
        float m = RefEdgeMargin * s;
        var inner = new Rect2(m, m, Size.X - 2f * m, Size.Y - 2f * m);
        bool onScreen = !behind && inner.HasPoint(sp);

        var lines = new List<string>(3);
        string head = MarkerHead(zone);
        if (head.Length > 0)
            lines.Add(head);
        lines.Add(zone.Description.Length > 0 ? zone.Description : zone.DzName);

        if (onScreen)
        {
            lines.Add(FormatDistance(PlanePos.DistanceTo(zone.Position)));
            DrawReticle(sp, RefReticleR * s);
            DrawLines(font, new Vector2(sp.X, sp.Y + (RefReticleR + RefTextGap) * s),
                lines.ToArray(), markerFont, HudBlue, topAnchored: true);
        }
        else
        {
            lines.Add($"{ClockHour(zone)} o'clock");
            var center = Size / 2f;
            var dir = sp - center;
            if (behind)
                dir = -dir; // the projection of a point behind the camera is mirrored through centre
            if (dir.LengthSquared() < 1f)
                dir = Vector2.Down;
            dir = dir.Normalized();
            var edge = EdgePoint(center, dir, m);
            DrawArrow(edge, dir, RefArrowLen * s, RefArrowHalf * s, s);
            DrawLinesClamped(font, edge - dir * (RefArrowLen + RefTextGap) * s, lines.ToArray(), markerFont, HudBlue);
        }
    }

    /// <summary>"Danger Zone [Fly Through] -" — the category/action prefix (the description goes on
    /// the next line), degrading gracefully if a part is absent.</summary>
    private static string MarkerHead(StuntZone z) =>
        z.Category.Length > 0 && z.Help.Length > 0 ? $"{z.Category} [{z.Help}] -"
        : z.Help.Length > 0 ? $"[{z.Help}] -"
        : z.Category.Length > 0 ? $"{z.Category} -"
        : "";

    /// <summary>Relative bearing of the zone from the plane's heading in clock hours (12 = ahead,
    /// 3 = right, 6 = behind, 9 = left) — the original's "N o'clock" suffix.</summary>
    private int ClockHour(StuntZone z)
    {
        var d = z.Position - PlanePos;
        float bearing = Mathf.RadToDeg(Mathf.Atan2(d.X, -d.Z)); // 0 = N (−Z), 90 = E (+X)
        float rel = Mathf.PosMod(bearing - HeadingDeg, 360f);
        int h = Mathf.RoundToInt(rel / 30f) % 12;
        return h == 0 ? 12 : h;
    }

    /// <summary>Distance in the HUD's imperial units (feet under a mile, miles above — matching the
    /// altimeter/speedometer; TUNE — the original's marker-distance unit is unverified).</summary>
    private static string FormatDistance(float meters)
    {
        float ft = meters * 3.28084f;
        return ft < 5280f ? $"{ft:0} FT" : $"{ft / 5280f:0.00} MI";
    }

    /// <summary>Screen-edge point along <paramref name="dir"/> from centre, inset by the margin.</summary>
    private Vector2 EdgePoint(Vector2 center, Vector2 dir, float margin)
    {
        float hx = Size.X / 2f - margin, hy = Size.Y / 2f - margin;
        float tx = Mathf.Abs(dir.X) > 1e-4f ? hx / Mathf.Abs(dir.X) : float.MaxValue;
        float ty = Mathf.Abs(dir.Y) > 1e-4f ? hy / Mathf.Abs(dir.Y) : float.MaxValue;
        return center + dir * Mathf.Min(tx, ty);
    }

    private void DrawArrow(Vector2 tip, Vector2 dir, float len, float half, float s)
    {
        var perp = new Vector2(-dir.Y, dir.X);
        var b1 = tip - dir * len + perp * half;
        var b2 = tip - dir * len - perp * half;
        var off = new Vector2(1.5f, 1.5f) * s;
        DrawColoredPolygon(new[] { tip + off, b1 + off, b2 + off }, Shadow);
        DrawColoredPolygon(new[] { tip, b1, b2 }, HudBlue);
        DrawLine(tip - dir * len, tip - dir * (len + 8f * s), HudBlue, 2f * s);
    }

    private void DrawReticle(Vector2 p, float r)
    {
        DrawArc(p, r + 1f, 0f, Mathf.Tau, 20, Shadow, 2.5f);
        DrawArc(p, r, 0f, Mathf.Tau, 20, HudBlue, 1.5f);
    }

    /// <summary>Draws centred lines (each horizontally centred at <paramref name="anchor"/>.X),
    /// with a 1 px drop shadow. Vertically the block is centred on <paramref name="anchor"/>.Y
    /// unless <paramref name="topAnchored"/>, in which case anchor.Y is its top.</summary>
    private void DrawLines(Font font, Vector2 anchor, string[] lines, int fontSize, Color color,
        bool topAnchored = false)
    {
        float lineH = font.GetHeight(fontSize);
        float ascent = font.GetAscent(fontSize);
        float y = topAnchored ? anchor.Y + ascent : anchor.Y - lines.Length * lineH / 2f + ascent;
        foreach (var line in lines)
        {
            float w = font.GetStringSize(line, HorizontalAlignment.Left, -1f, fontSize).X;
            var p = new Vector2(anchor.X - w / 2f, y);
            DrawString(font, p + Vector2.One, line, HorizontalAlignment.Left, -1f, fontSize, Shadow);
            DrawString(font, p, line, HorizontalAlignment.Left, -1f, fontSize, color);
            y += lineH;
        }
    }

    /// <summary>Like <see cref="DrawLines"/> (vertically centred) but keeps the whole block within
    /// the screen margins — the off-screen edge marker never spills off a corner.</summary>
    private void DrawLinesClamped(Font font, Vector2 center, string[] lines, int fontSize, Color color)
    {
        float lineH = font.GetHeight(fontSize);
        float totalH = lines.Length * lineH;
        float maxW = 0f;
        foreach (var line in lines)
            maxW = Mathf.Max(maxW, font.GetStringSize(line, HorizontalAlignment.Left, -1f, fontSize).X);
        float m = RefEdgeMargin * HudMetrics.Scale(this);
        center.X = Mathf.Clamp(center.X, m + maxW / 2f, Size.X - m - maxW / 2f);
        center.Y = Mathf.Clamp(center.Y, m + totalH / 2f, Size.Y - m - totalH / 2f);
        DrawLines(font, center, lines, fontSize, color);
    }
}
