using System;
using System.Collections.Generic;
using CSVM.Flight.Hud;
using CSVM.UI.Boards;
using Godot;

namespace CSVM.Flight.Modes;

/// <summary>The stunt run's own readouts, one per pane: the run-status line (clock + zones
/// cleared), the intro banner, the zone-cleared flash and the all-complete banner. In a race the
/// line under the status line is the live leaderboard (<see cref="StuntRace.LeaderboardLine"/>).
/// A viewport-filling Control fed nothing per frame; it reads the run and the race.
/// ⚠ The Danger Zone marker itself is NOT here. A zone is an objective on the pilot's own target
/// cycle (<see cref="StuntMission.CollectTargets"/>). The target HUD draws it like every other
/// objective, so there is one marker style and one cycle.</summary>
public sealed partial class StuntRunHud : Control
{
    /// <summary>The time-attack race this pilot is flying in, or null in a solo run. Set, the
    /// leaderboard line draws and the all-zones-cleared banner reads this run against the
    /// pilot's best. Paired with <see cref="PlayerIndex"/>.</summary>
    public StuntRace? Race;

    /// <summary>Which player's pane this HUD draws in (0-based), picks their row out of
    /// <see cref="Race"/>.</summary>
    public int PlayerIndex;

    /// <summary>This pilot's start count, whose figure draws large in the middle of the pane while
    /// it stands. Null draws none.</summary>
    public StartCount? Count;

    // 1440p reference metrics (scaled by viewport height).
    private const float RefStatusY = 100f;   // run-status baseline y, just under the compass tape
    private const float RefRaceLineGap = 8f; // between the status line and the leaderboard line
    private const float IntroDuration = 5f;  // s the run-start line shows (fades the last second)
    private const float FlashDuration = 1.6f;// s a zone-cleared flash shows
    private const float GoFade = 0.4f;       // s GO takes to fade at the end of its stand

    // The status line's, the banners' and the count's sizes, chrome type scale rungs in the same reference.
    private static readonly float RefStatusFont = ChromeType.InReference(ChromeSize.Readout, HudMetrics.ReferenceHeight);
    private static readonly float RefBannerFont = ChromeType.InReference(ChromeSize.Note, HudMetrics.ReferenceHeight);
    private static readonly float RefCountFont = ChromeType.InReference(ChromeSize.Count, HudMetrics.ReferenceHeight);

    private static readonly Color HudBlue = MarkerDraw.HudBlue;
    private static readonly Color HudGreen = new(0.60f, 1f, 0.70f); // completion feedback

    private StuntMission _mission = null!;
    private float _flash;         // s left on the zone-cleared flash
    private string _flashText = "";

    /// <summary>What sends the status and leaderboard lines away while it answers true: the pane's
    /// held scores. Their Original text runs across the top of the pane. Null keeps them.</summary>
    public Func<bool>? StatusHiddenWhile { get; set; }

    /// <summary>Whether the status line and the leaderboard line draw this frame.</summary>
    public bool StatusShown => StatusHiddenWhile?.Invoke() != true;

    /// <summary>Set while this pane spectates the race: the racer it watches, or null on a free
    /// camera. Read afresh each frame, so the name follows the spectator's target key.</summary>
    public Func<Racer?>? WatchedRacer { get; set; }

    /// <summary>The line under the leaderboard naming the racer watched, or null for none.</summary>
    public string? WatchingLine => WatchedRacer?.Invoke() is { } racer ? $"WATCHING {racer.Callsign}" : null;

    /// <summary>The status lines' font size at HUD scale <paramref name="s"/>, never rounded down
    /// to 0, since a quarter-height 4P pane scales hard.</summary>
    public static int StatusFontSize(float s) =>
        Mathf.Max(1, Mathf.RoundToInt(RefStatusFont * s * HudMetrics.StatusTextScale));

    /// <summary>The top of status line <paramref name="line"/> (0 the run status, 1 the leaderboard,
    /// 2 the racer watched) at HUD scale <paramref name="s"/>, each line <paramref name="lineHeight"/> tall.</summary>
    public static float LineTop(int line, float s, float lineHeight) =>
        ((RefStatusY + (line * RefRaceLineGap)) * s) + (line * lineHeight);

    /// <summary>Binds the run and subscribes to the completion flash. Add to the HUD canvas;
    /// nothing needs feeding per frame.</summary>
    public static StuntRunHud Build(StuntMission mission)
    {
        var hud = new StuntRunHud
        {
            _mission = mission,
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
        };
        mission.ZoneCompleted += hud.OnZoneCompleted;
        return hud;
    }

    public override void _ExitTree() => _mission.ZoneCompleted -= OnZoneCompleted;

    public override void _Process(double delta)
    {
        // Track the viewport (resizable window) and repaint, the clock advances every frame.
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
        if (_flash > 0f)
            _flash -= (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        // A draw can land before _Process has sized us; Godot's font cache errors on zero size.
        float s = Size.Y <= 0f ? 0f : HudMetrics.Scale(this);
        if (s <= 0f)
            return;
        var font = ChromeType.Face(this);
        int statusFont = StatusFontSize(s);
        int bannerFont = Mathf.Max(1, Mathf.RoundToInt(RefBannerFont * s));
        float lineHeight = font.GetHeight(statusFont);
        float cx = Size.X / 2f;

        // Run-status: elapsed time + zones done, top-centre under the compass tape.
        bool status = StatusShown;
        if (status)
        {
            DrawLines(font, new Vector2(cx, LineTop(0, s, lineHeight)),
                new[] { $"{StuntMission.FormatTime(_mission.Elapsed)}    ZONES {_mission.CompletedCount}/{_mission.TotalCount}" },
                statusFont, HudBlue, topAnchored: true);
        }

        // The live leaderboard, the next line down, so it shares the status line's clear band
        // between the compass tape and the message slots.
        if (status && Race != null)
        {
            DrawLines(font, new Vector2(cx, LineTop(1, s, lineHeight)), new[] { Race.LeaderboardLine(PlayerIndex) },
                statusFont, HudBlue, topAnchored: true);
            // Centred on the pane's width like the leaderboard's leader name, so no name box scrolls.
            if (WatchingLine is { } watching)
            {
                DrawLines(font, new Vector2(cx, LineTop(2, s, lineHeight)), new[] { watching },
                    statusFont, HudBlue, topAnchored: true);
            }
        }

        // A spectating pane's middle belongs to the racer it watches: no banner, flash or count.
        if (WatchedRacer != null)
            return;

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
            DrawLines(font, new Vector2(cx, Size.Y * 0.26f), CompleteBanner(), bannerFont, HudGreen);
        else if (Race?.Of(PlayerIndex) is { InRun: false } && Race.Phase is StuntRacePhase.FinalRun or StuntRacePhase.Ended)
            DrawLines(font, new Vector2(cx, Size.Y * 0.26f), new[] { "TIME UP" }, bannerFont, HudBlue);

        // The start count's figure, centred; GO in the completion green, fading out at the end.
        if (Count?.Figure is { } figure)
        {
            int countFont = Mathf.Max(1, Mathf.RoundToInt(RefCountFont * s));
            bool go = !Count.Running;
            var color = go
                ? new Color(HudGreen, Mathf.Clamp((StartCount.GoSeconds - Count.FigureAge) / GoFade, 0f, 1f))
                : HudBlue;
            DrawLines(font, new Vector2(cx, Size.Y * 0.5f), new[] { figure }, countFont, color);
        }
    }

    private void OnZoneCompleted(StuntZone z)
    {
        _flash = FlashDuration;
        _flashText = z.Description.Length > 0 ? $"{z.Description} CLEARED" : "DANGER ZONE CLEARED";
    }

    // The banner shown in this player's pane once they have cleared every zone. Solo: the run is
    // simply over. In a race: this run against the pilot's best, and while the window is open,
    // that a held respawn flies another.
    private string[] CompleteBanner()
    {
        if (Race?.Of(PlayerIndex) is not { } me)
            return new[] { "ALL DANGER ZONES CLEARED", StuntMission.FormatTime(_mission.Elapsed) };
        var lines = new List<string>(3) { "RUN COMPLETE", StuntMission.FormatTime(_mission.Elapsed) };
        if (me.BestTime is { } best)
            lines.Add(best == _mission.Elapsed ? "BEST RUN" : $"BEST {StuntMission.FormatTime(best)}");
        lines.Add(Race.MayStartRun ? "hold respawn to fly again" : "TIME UP");
        return lines.ToArray();
    }

    private void DrawLines(Font font, Vector2 anchor, string[] lines, int fontSize, Color color,
        bool topAnchored = false) =>
        MarkerDraw.Lines(this, font, anchor, lines, fontSize, color, topAnchored);
}
