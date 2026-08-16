using System.Collections.Generic;
using CSVM.UI;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// The per-pane targeting HUD (<c>PLAN-targeting.md</c> C21): the tracked-hostile marker, split
/// out of <see cref="VersusHud"/> because it draws in EVERY flight session, not only
/// <c>--vs</c> (H22) — a Versus-only class was the wrong home for a feature every pane gets.
/// <see cref="Build"/> is unconditional, one per human pane, whether or not the session has a
/// <see cref="VersusMatch"/>; a <c>--vs</c> pane gets BOTH this and a <see cref="VersusHud"/>, so
/// an AI hostile spawned into a dogfight is still marked alongside the human opponents.
///
/// <para><see cref="HostilePool"/> is scanned each frame through the pool's one live aircraft
/// roster (<c>ProjectilePool.CollectAircraft</c>, the same list the AI gunners read) and the
/// NEAREST live AI aircraft becomes <see cref="TrackedHostile"/>. Humans carry no gunner, so there
/// is no D12 "the target" to mirror; nearest-hostile is the shipped rule, re-evaluated per frame,
/// which is VS mode's own no-lock behaviour. The player's own STICKY selection
/// (<see cref="TargetSelection"/>, <c>Targeting.Current</c>) is a separate mechanism this class
/// does not read yet — C22 draws that marker here.</para>
///
/// <para><c>--debug-markers</c> (<see cref="MarkAll"/>) widens the one marker to EVERY live
/// aircraft the pool lists, red for a hostile team and blue for this pane's own side
/// (<see cref="Own"/>), each tagged with its slant range. It is a watching aid for AI work, not a
/// gameplay feature: the shipped HUD marks exactly one hostile and the flag is off unless asked
/// for.</para>
///
/// <para>The edge-arrow + clock-bearing drawing (<see cref="DrawOpponent"/>,
/// <see cref="EdgePoint"/>, <see cref="ClockHour"/>, <see cref="DrawArrow"/>, <see cref="DrawTag"/>)
/// is copied verbatim from <see cref="VersusHud"/>'s own copy of it — the same relationship
/// <c>VersusHud</c> already has with <c>MarkerHud</c>, not a shared base class for the two
/// <see cref="Control"/>s. This is CSVM's own drawing shape for a tracked hostile, but the shape
/// is not invented: <c>OriginalScreenshots/HUD.png</c> shows the original's own targeting marker
/// doing exactly this off screen — a red arrow with the name and clock bearing on two lines below
/// it — decoded in <see href="../../docs/org/targeting.md">org/targeting.md</see>. C22 draws the
/// player's selected target in that same original shape (fixed-size brackets, the label always
/// below, two lines at the edge); this class's hostile tracker keeps its current one-line tag
/// until then.</para>
/// </summary>
public sealed partial class TargetHud : Control
{
    /// <summary>Which pane this draws in (0-based) — the identity <see cref="OwnTeam"/> falls
    /// back to with no aircraft bound.</summary>
    public int PlayerIndex;

    /// <summary>The pool whose registered aircraft the hostile tracker scans (H22). Null leaves
    /// the tracker off; set, <see cref="UpdateHostile"/> re-selects the nearest live AI aircraft
    /// every frame, so a runtime spawn (generators) is picked up and a downed hostile drops
    /// without extra plumbing.</summary>
    public ProjectilePool? HostilePool;

    /// <summary><c>--debug-markers</c>: mark EVERY live aircraft in <see cref="HostilePool"/>
    /// instead of the single nearest hostile: red for a hostile team, blue for this pane's own
    /// side, each with its tag and slant range. A watching aid while the AI is being worked on
    /// (which of six planes is the one that flew off), off by default and never a gameplay
    /// feature: the shipped marker is exactly one hostile.</summary>
    public bool MarkAll;

    /// <summary>This pane's own aircraft: the side every team test here runs against
    /// (<see cref="OwnTeam"/>), and the plane excluded from <see cref="MarkAll"/>'s sweep. Null
    /// marks everything the pool lists, which is what the suite wants and what a pane with no
    /// aircraft of its own (a spectator) should see.</summary>
    public FlightController? Own;

    // 1440p reference metrics (scaled by HudMetrics — matches MarkerHud's/VersusHud's calibration).
    private const int RefMarkerFont = 14;
    private const float RefEdgeMargin = 46f;  // keep edge markers this far off the screen border
    private const float RefArrowLen = 18f;
    private const float RefArrowHalf = 8f;
    private const float RefTextGap = 8f;
    private const float RefOnScreenLift = 22f; // gap above a plane's own projected point
    private const float RefStaggerStep = 18f;  // --debug-markers: gap between two edge tags on one bearing

    private static readonly Color HudBlue = new(0.55f, 0.78f, 1f);
    private static readonly Color HudRed = new(1f, 0.55f, 0.55f);
    private static readonly Color Shadow = new(0f, 0f, 0f, 0.75f);

    private readonly AimCandidateSet _hostileScan = new(); // rebuilt per frame, aircraft list only
    private readonly List<(FlightController Plane, bool Friendly)> _marks = new(); // --debug-markers

    private Camera3D _camera = null!;
    private FlightController? _hostile;
    private string _hostileTag = "AI";

    /// <summary>This pane's own world pose, fed every frame by FlightController — the tracked
    /// hostile's clock bearing reads off it, exactly like MarkerHud's PlanePos/HeadingDeg.</summary>
    public Vector3 PlanePos { get; set; }

    /// <summary>This pane's own nose heading, 0 = north (−Z) — see <see cref="PlanePos"/>.</summary>
    public float HeadingDeg { get; set; }

    /// <summary>This pane's tracked AI hostile, or null with none in the pool. Read by the
    /// suite; the marker draws off the same field.</summary>
    public FlightController? TrackedHostile => _hostile;

    /// <summary>The side this pane is on: <see cref="Own"/>'s own <see cref="FlightController.Team"/>
    /// field, falling back to the pilot-index derivation only when no aircraft is bound (a
    /// spectator, or a suite rig built without one).
    ///
    /// <para>⚠ Reading the FIELD is the whole point. This HUD used to derive
    /// <c>AimAssist.TeamOfPilot(PlayerIndex)</c> per call, which is right for P1 by coincidence
    /// (<c>TeamOfPilot(0)</c> == <see cref="AimAssist.PlayerTeam"/>) and wrong for everyone else the
    /// moment a mission sets teams explicitly: in Instant Action or <c>--coop</c> every human is
    /// team 1, so P2 derived team 2, marked its own wingmen hostile and skipped the real enemies as
    /// "own team". That is the wingman-in-the-marker bug.</para></summary>
    public int OwnTeam => Own?.Team ?? AimAssist.TeamOfPilot(PlayerIndex);

    /// <summary>Binds this pane's own camera (the marker projects through it) and, when
    /// <paramref name="pool"/> is non-null, the hostile tracker. Add to the HUD canvas;
    /// <see cref="PlanePos"/>/<see cref="HeadingDeg"/> every frame — nothing else needs feeding.
    /// One per human pane, in EVERY flight session (H22); it draws nothing until a hostile
    /// exists, so an AI-free session's HUD output is unchanged.</summary>
    public static TargetHud Build(int playerIndex, Camera3D camera, ProjectilePool pool)
    {
        return new TargetHud
        {
            PlayerIndex = playerIndex,
            _camera = camera,
            HostilePool = pool,
            MouseFilter = MouseFilterEnum.Ignore,
            FocusMode = FocusModeEnum.None,
        };
    }

    /// <summary>The nearest live hostile AI aircraft in <paramref name="scan"/>, or null. Pure
    /// over the candidate snapshot: live (a crashed plane is listed but not live), an
    /// AI-piloted <see cref="FlightController"/> (the human pane's hostile class: another
    /// human is a VS opponent, never a hostile here), and the engine's team gate (either side
    /// neutral rejects the pair).</summary>
    public static FlightController? NearestHostile(Vector3 ownPos, int ownTeam, AimCandidateSet scan)
    {
        FlightController? best = null;
        float bestD2 = float.MaxValue;
        foreach (var c in scan.Vehicles)
        {
            if (!c.Live || c.Team == AimAssist.NeutralTeam || ownTeam == AimAssist.NeutralTeam
                || c.Team == ownTeam)
                continue;
            if (c.Source is not FlightController { IsHumanPiloted: false } fc)
                continue;
            float d2 = ownPos.DistanceSquaredTo(c.Position);
            if (d2 < bestD2)
            {
                bestD2 = d2;
                best = fc;
            }
        }

        return best;
    }

    /// <summary>Every live aircraft in <paramref name="scan"/> paired with whether it is on
    /// <paramref name="ownTeam"/> (<c>--debug-markers</c>), skipping <paramref name="own"/>. The
    /// team test is the plain identity one, not the assist's: a neutral aircraft is nobody's
    /// friend, so it marks hostile rather than vanishing, which is what a debugging overlay
    /// wants. Pure over the snapshot, same contract as <see cref="NearestHostile"/>.</summary>
    public static void CollectMarks(int ownTeam, object? own, AimCandidateSet scan,
        List<(FlightController Plane, bool Friendly)> into)
    {
        foreach (var c in scan.Vehicles)
        {
            if (!c.Live || ReferenceEquals(c.Source, own))
                continue;
            if (c.Source is not FlightController fc)
                continue;
            into.Add((fc, c.Team == ownTeam && c.Team != AimAssist.NeutralTeam));
        }
    }

    /// <summary>A marked plane's current AI mode in the ENGINE's own vocabulary
    /// (<see cref="AiModeMachine.NameOf"/>: patrol / pursue / lay off / evade / evasive maneuver /
    /// stunned / avoid crash), or empty for anything without a mode machine (a human seat, or an
    /// AI flying bare orders). <c>--debug-markers</c> only; the shipped marker carries a tag and a
    /// bearing, nothing about the other pilot's state.</summary>
    public static string ModeSuffix(FlightController plane) =>
        plane.Pilot?.Machine is { } machine ? $"  {AiModeMachine.NameOf(machine.Mode)}" : "";

    /// <summary>The marker tag for a hostile: the controller name's first '_'-segment,
    /// uppercased ("ai1_player_fury" reads "AI1"), "AI" when the name yields nothing.</summary>
    public static string HostileTag(string name)
    {
        int cut = name.IndexOf('_');
        string head = cut > 0 ? name.Substring(0, cut) : name;
        return head.Length > 0 ? head.ToUpperInvariant() : "AI";
    }

    public override void _Process(double delta)
    {
        // Track the pane (resizable window / splitscreen layout) and repaint every frame.
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
        UpdateHostile();
        QueueRedraw();
    }

    /// <summary>Re-selects this pane's tracked hostile from the pool's live aircraft roster:
    /// nearest live AI aircraft to <see cref="PlanePos"/>, null with none. Called every
    /// <see cref="_Process"/>; public so the suite drives it without pumping frames. Logs the
    /// acquire/lose transitions only, so a steady chase stays quiet.</summary>
    public void UpdateHostile()
    {
        FlightController? next = null;
        if (HostilePool != null)
        {
            _hostileScan.Clear();
            HostilePool.CollectAircraft(_hostileScan);
            next = NearestHostile(PlanePos, OwnTeam, _hostileScan);
        }
        if (ReferenceEquals(next, _hostile))
            return;
        if (next != null)
        {
            _hostileTag = HostileTag(next.Name);
            Utils.Log.Info("flight",
                $"targeting hud: P{PlayerIndex + 1} tracking {next.Name} at {PlanePos.DistanceTo(next.WorldPosition):0} m");
        }
        else
        {
            // The tag was cached at acquire, so a freed hostile is never dereferenced here.
            Utils.Log.Info("flight", $"targeting hud: P{PlayerIndex + 1} lost {_hostileTag}");
        }
        _hostile = next;
    }

    public override void _Draw()
    {
        // Same zero-size guard as MarkerHud/VersusHud: a draw can land before _Process has sized
        // this pane.
        float s = Size.Y <= 0f ? 0f : HudMetrics.Scale(this);
        if (s <= 0f)
            return;
        var font = GetThemeDefaultFont();
        int markerFont = Mathf.Max(1, Mathf.RoundToInt(RefMarkerFont * s));

        // --debug-markers: every live aircraft at once, red hostile / blue own side, each with
        // its slant range. Replaces the single-hostile marker below rather than stacking on it,
        // so the tracked one is not drawn twice in two colours.
        if (MarkAll && HostilePool != null)
        {
            _marks.Clear();
            CollectMarks(OwnTeam, Own, _hostileScan, _marks);
            int stagger = 0;
            foreach (var (plane, friendly) in _marks)
            {
                if (!GodotObject.IsInstanceValid(plane) || !plane.IsInsideTree())
                    continue;
                var at = plane.GlobalPosition;
                DrawOpponent(font, at, friendly ? HudBlue : HudRed,
                    $"{HostileTag(plane.Name)} {PlanePos.DistanceTo(at):0} m{ModeSuffix(plane)}",
                    s, markerFont, stagger++);
            }

            return;
        }

        // The tracked AI hostile (H22): the same marker as a VS opponent used to be.
        // UpdateHostile ran this frame, so the reference is at most one scan old; the validity
        // guard covers a hostile freed between the scan and this draw.
        if (GodotObject.IsInstanceValid(_hostile) && _hostile is { InPlay: true } hostile
            && hostile.IsInsideTree())
            DrawOpponent(font, hostile.GlobalPosition, HudRed, _hostileTag, s, markerFont);
    }

    /// <summary>Screen-edge point along <paramref name="dir"/> from centre, inset by the margin —
    /// copied verbatim from <see cref="VersusHud"/>'s own copy of MarkerHud's EdgePoint.</summary>
    private static Vector2 EdgePoint(Vector2 center, Vector2 dir, float margin)
    {
        float hx = center.X - margin, hy = center.Y - margin;
        float tx = Mathf.Abs(dir.X) > 1e-4f ? hx / Mathf.Abs(dir.X) : float.MaxValue;
        float ty = Mathf.Abs(dir.Y) > 1e-4f ? hy / Mathf.Abs(dir.Y) : float.MaxValue;
        return center + dir * Mathf.Min(tx, ty);
    }

    /// <summary>One tracked hostile's marker: on screen, its tag floats just above the projected
    /// point; off screen (or behind), an edge arrow + "N o'clock" bearing — copied verbatim from
    /// <see cref="VersusHud"/>'s own copy of this, plus the <paramref name="stagger"/> step
    /// <c>--debug-markers</c> needs when several planes share one bearing.</summary>
    private void DrawOpponent(Font font, Vector3 pos, Color color, string tag, float s, int fontSize,
        int stagger = 0)
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
        // Several planes on one bearing put their tags on the same pixel (--debug-markers marks
        // six at once); step each one along the screen edge so all of them stay readable.
        var along = new Vector2(-dir.Y, dir.X) * (stagger * RefStaggerStep * s);
        DrawTag(font, edge - dir * (RefArrowLen + RefTextGap) * s + along,
            $"{tag}  {ClockHour(pos)} o'clock", color, fontSize);
    }

    /// <summary>Relative bearing of <paramref name="targetPos"/> from this pilot's own heading in
    /// clock hours (12 = ahead, 3 = right, 6 = behind, 9 = left) — copied verbatim from
    /// <see cref="VersusHud"/>'s own copy of MarkerHud.ClockHour.</summary>
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
}
