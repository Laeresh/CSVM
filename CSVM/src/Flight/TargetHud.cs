using System.Collections.Generic;
using System.Text;
using CSVM.Session;
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
/// <para><b>The shipped marker is the pilot's own sticky selection</b> (<see cref="Selected"/>,
/// C22), drawn in the original's shape: a fixed-size bracket box gated on the SELECTED GUN's reach
/// (<see cref="GunReaches"/>), the label always BELOW it, and off screen an edge arrow with the name
/// and clock bearing stacked beside it. Colour is the decoded <c>Target::GetColor</c>
/// (<see cref="MarkerColor"/>): red hostile, green friendly, blue non-destructive objective.</para>
///
/// <para><see cref="HostilePool"/> is scanned each frame through the pool's one live aircraft
/// roster (<c>ProjectilePool.CollectAircraft</c>, the same list the AI gunners read) and the
/// NEAREST live AI aircraft becomes <see cref="TrackedHostile"/>. That H22 marker is now a
/// FALLBACK: it draws only on a pane with no selection at all (no <see cref="TargetSelection"/>
/// bound, or the pilot pressed Target Nothing), because a pane that HAS a selection marks that one
/// target and nothing else (decision 11).</para>
///
/// <para><c>--debug-markers</c> (<see cref="MarkAll"/>) widens the one marker to EVERY live
/// aircraft the pool lists, red for a hostile team and blue for this pane's own side
/// (<see cref="Own"/>), each tagged with the FULL identity string (<see cref="DebugTag"/>, C23):
/// the hostile tag, the airframe type, slant range, health/armor as whole percentages with no
/// percent sign (omitted, not defaulted, where the source carries no figure), and the AI mode. It
/// is a watching aid for AI work, not a gameplay feature: the shipped HUD marks exactly one
/// hostile and the flag is off unless asked for.</para>
///
/// <para>The edge-arrow + clock-bearing drawing (<see cref="DrawOpponent"/>,
/// <see cref="EdgePoint"/>, <see cref="ClockHour"/>, <see cref="DrawArrow"/>, <see cref="DrawTag"/>)
/// is copied verbatim from <see cref="VersusHud"/>'s own copy of it — the same relationship
/// <c>VersusHud</c> already has with <c>MarkerHud</c>, not a shared base class for the two
/// <see cref="Control"/>s. This is CSVM's own drawing shape for a tracked hostile, but the shape
/// is not invented: <c>OriginalScreenshots/HUD.png</c> shows the original's own targeting marker
/// doing exactly this off screen — a red arrow with the name and clock bearing on two lines below
/// it — decoded in <see href="../../docs/org/targeting.md">org/targeting.md</see>. The selected
/// target's own edge tag (<see cref="DrawSelected"/>) draws those lines stacked, as the original
/// does; the hostile tracker and <c>--debug-markers</c> keep the one-line tag, which is where the
/// <see cref="RefStaggerStep"/> spacing still applies.</para>
/// </summary>
public sealed partial class TargetHud : Control
{
    /// <summary>Extra metres of authored gun range that keep the brackets on once they are on
    /// (C22). <b>TUNE, and ours rather than the original's</b> — the original re-runs its gate every
    /// frame with no memory, which strobes the box on a target sitting exactly at the reach
    /// boundary.</summary>
    public const float BracketHysteresis = 50f;

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

    // The selected target's marker: the original's own absolute pixel constants (0x00607a0c /
    // 0x00607a14 / 0x00607a10 and FUN_004574d0's label anchor), read at the 1440p reference and
    // scaled by HudMetrics like every other CSVM HUD element. The original never scales its box;
    // scaling it is the deliberate divergence docs/org/targeting.md records, because 20 x 16 fixed
    // pixels is nearly invisible on a modern display.
    private const float RefBracketHalfW = 10f;
    private const float RefBracketHalfH = 8f;
    private const float RefBracketArm = 4f;
    private const float RefBracketWidth = 2f;  // ours: the original draws 1 px line sprites
    private const float RefLabelGap = 3f;      // anchor = box bottom + 3
    private const float RefLabelPitch = 15f;   // the three lines' spacing
    private const float RefLabelBlock = 33f;   // the flip-above test: box bottom + 33 must still fit
    private const float RefLabelAbove = 30f;   // flipped, the block starts box top − 30

    private static readonly Color HudBlue = new(0.55f, 0.78f, 1f);
    private static readonly Color HudRed = new(1f, 0.55f, 0.55f);

    /// <summary>The friendly-target colour. The decode's own answer is a friendly is GREEN, not blue
    /// (docs/org/targeting.md "Colour"); this is that (0,255,0) in the HUD palette's desaturation,
    /// the same relationship <see cref="HudRed"/> has to the decoded (200,0,0).</summary>
    private static readonly Color HudGreen = new(0.6f, 1f, 0.6f);

    private static readonly Color Shadow = new(0f, 0f, 0f, 0.75f);

    /// <summary>The four objective categories the original draws RED, the rest blue
    /// (<c>MSG_OBJ_DESTROY</c> / <c>DISABLE</c> / <c>DISABLEENG</c> / <c>DAMAGE</c>).</summary>
    private static readonly string[] DestructiveCategories =
        { "Destroy", "Disable", "Disable Engines", "Damage" };

    private readonly List<string> _labelLines = new();  // rebuilt per draw

    private readonly AimCandidateSet _hostileScan = new(); // rebuilt per frame, aircraft list only
    private readonly List<(TargetRef Target, bool Friendly)> _marks = new(); // --debug-markers

    private Camera3D _camera = null!;
    private FlightController? _hostile;
    private string _hostileTag = "AI";
    private bool _bracketed;   // last frame's gate answer, for the hysteresis

    /// <summary>This pane's own world pose, fed every frame by FlightController — the tracked
    /// hostile's clock bearing reads off it, exactly like MarkerHud's PlanePos/HeadingDeg.</summary>
    public Vector3 PlanePos { get; set; }

    /// <summary>This pane's own nose heading, 0 = north (−Z) — see <see cref="PlanePos"/>.</summary>
    public float HeadingDeg { get; set; }

    /// <summary>This pane's tracked AI hostile, or null with none in the pool. Read by the
    /// suite; the fallback marker draws off the same field.</summary>
    public FlightController? TrackedHostile => _hostile;

    /// <summary>The pilot's own sticky selection, or null on a pane with no
    /// <see cref="TargetSelection"/> bound (a spectator, a suite rig). The shipped marker.</summary>
    public TargetRef? Selected => Own?.Targeting?.Current;

    /// <summary>Whether the brackets are currently drawn around <see cref="Selected"/> — the gun's
    /// reach gate plus its hysteresis, re-answered every <see cref="_Process"/>. Read by the
    /// suite.</summary>
    public bool Bracketed => _bracketed;

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

    /// <summary>Every live aircraft in <paramref name="scan"/>, wrapped as a <see cref="TargetRef"/>
    /// (C23) and paired with whether it is on <paramref name="ownTeam"/> (<c>--debug-markers</c>),
    /// skipping <paramref name="own"/>. The team test is the plain identity one, not the assist's: a
    /// neutral aircraft is nobody's friend, so it marks hostile rather than vanishing, which is what
    /// a debugging overlay wants.
    ///
    /// <para>Wrapping as a <see cref="TargetRef"/> — the same aircraft construction
    /// <see cref="TargetPool"/>'s Vehicle branch uses — is what lets <see cref="DebugTag"/> read
    /// health/armor as optional fields rather than a plane-specific read of its own: the moment this
    /// scan widens past aircraft, a turret or sub-part's <see cref="TargetRef.Health"/> is already
    /// the right shape (null, correctly).</para></summary>
    public static void CollectMarks(int ownTeam, object? own, AimCandidateSet scan,
        List<(TargetRef Target, bool Friendly)> into)
    {
        foreach (var c in scan.Vehicles)
        {
            if (!c.Live || ReferenceEquals(c.Source, own))
                continue;
            if (c.Source is not FlightController fc)
                continue;
            var dmg = fc.Damage;
            var cls = TargetRef.Classify(AimTargetKind.Vehicle, live: true, c.Team, ownTeam)
                ?? TargetClass.Enemy;
            var target = TargetRef.ForAircraft(c, cls, fc.Name,
                fc.Stats is { } stats ? PlaneRoster.PlaneDisplayName(stats) : null,
                dmg == null ? null : TargetRef.Fraction(dmg.WholeHealth, dmg.WholeHealthMax),
                dmg == null ? null : TargetRef.Fraction(dmg.WholeArmor, dmg.WholeArmorMax));
            into.Add((target, c.Team == ownTeam && c.Team != AimAssist.NeutralTeam));
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

    /// <summary><c>--debug-markers</c>' own tag (C23): <c>AI1 Fury 640 m H78 A91 pursue</c>. This is
    /// the one marker that keeps the FULL identity string — <paramref name="identity"/>
    /// (<see cref="HostileTag"/>), never collapsed to <paramref name="target"/>'s plane-type-alone
    /// label the way the shipped marker is (decision 10) — plus the plane type, the slant range,
    /// health then armor as whole percentages with NO percent sign (decision 12: two figures, health
    /// first, never a blended one), and finally <paramref name="modeSuffix"/>.
    ///
    /// <para>Health and armor are each omitted, not defaulted, when <paramref name="target"/> carries
    /// no figure for it (<see cref="TargetRef.Health"/>/<see cref="TargetRef.Armor"/> null) — a
    /// turret emplacement or a bare rig with no damage ledger has no such number to print, and
    /// <c>H100 A100</c> would be a number the game does not have.</para></summary>
    public static string DebugTag(string identity, in TargetRef target, float rangeM, string modeSuffix)
    {
        var tag = new StringBuilder(identity);
        if (target.DisplayName is { Length: > 0 } name)
        {
            tag.Append(' ').Append(name);
        }

        tag.Append(' ').Append(Mathf.RoundToInt(rangeM)).Append(" m");
        if (target.Health is { } health)
        {
            tag.Append(" H").Append(Mathf.RoundToInt(health * 100f));
        }

        if (target.Armor is { } armor)
        {
            tag.Append(" A").Append(Mathf.RoundToInt(armor * 100f));
        }

        tag.Append(modeSuffix);
        return tag.ToString();
    }

    /// <summary>The marker's colour, <c>Target::GetColor</c> (<c>FUN_004a5f40</c>) verbatim: an
    /// objective is red for the four destructive categories and blue for every other one, and
    /// anything without a category is red when the teams differ and both are non-zero, green
    /// otherwise.
    ///
    /// <para>⚠ A friendly is GREEN. Blue is the non-destructive objective (protect, escort), not the
    /// friendly — C22's own goal line said "blue for a friendly", which was written before A1
    /// decoded this, and the decode wins.</para></summary>
    public static Color MarkerColor(in TargetRef target, int ownTeam)
    {
        if (target.Category is { Length: > 0 } category)
        {
            foreach (var destructive in DestructiveCategories)
            {
                if (string.Equals(category, destructive, System.StringComparison.OrdinalIgnoreCase))
                {
                    return HudRed;
                }
            }

            return HudBlue;
        }

        return target.Team != ownTeam && target.Team != AimAssist.NeutralTeam
            && ownTeam != AimAssist.NeutralTeam
            ? HudRed
            : HudGreen;
    }

    /// <summary>The bracket gate's arithmetic (<c>FUN_004574d0</c>): solve the lead intercept the aim
    /// assist's own solver gives, then accept only when the round could still be travelling at that
    /// intercept inside the weapon's authored <paramref name="range"/>. No intercept (a target
    /// outrunning the round) is no brackets, at any distance.
    ///
    /// <para>⚠ The reach is measured along the MUZZLE VELOCITY — the round's own speed along the
    /// intercept direction PLUS the plane's velocity — where
    /// <see cref="AimAssist"/>'s own range gate uses the round speed alone. That difference is the
    /// original's: the HUD composes the vector, the assist does not.</para></summary>
    public static bool GunReaches(Vector3 muzzle, Vector3 shooterVel, float speed, float range,
        Vector3 targetPos, Vector3 targetVel)
    {
        if (!AimAssist.TryIntercept(muzzle, speed, targetPos, targetVel - shooterVel,
                out var dir, out float t))
        {
            return false;
        }

        float reach = (dir * speed + shooterVel).Length() * t;
        return reach <= range;
    }

    /// <summary>The marker's label lines, top to bottom — the original's three text elements
    /// (<c>FUN_004579e0</c>): the category line, the name, and the clock bearing. Empty-tolerant by
    /// construction, which is why an ordinary aircraft shows its name alone
    /// (<c>Targeting HUD Kestrel.png</c>) and a named objective shows both lines
    /// (<c>C1 M04 Zeppelin.png</c>) with no format of its own.
    ///
    /// <para>⚠ The category SLOT is kept, empty, under a box. The original's three lines sit at
    /// fixed offsets from the anchor and a blank one simply draws nothing, so an aircraft's name is
    /// the SECOND line's distance below the box, not the first's — closing that gap puts the name
    /// into the silhouette. There is no box to measure from at the screen edge, so the block is
    /// compacted there instead.</para></summary>
    /// <param name="bearing">The <c>N o'clock</c> line, or null on screen — see
    /// <see cref="DrawSelected"/> for why that line is the off-screen case only.</param>
    /// <param name="keepSlots">Whether an empty category line still occupies its slot.</param>
    public static void LabelLines(in TargetRef target, string? bearing, List<string> into,
        bool keepSlots = true)
    {
        string category = target.CategoryLine;
        if (category.Length > 0 || keepSlots)
        {
            into.Add(category);
        }

        if (target.DisplayName is { Length: > 0 } name)
        {
            into.Add(name);
        }

        if (bearing is { Length: > 0 })
        {
            into.Add(bearing);
        }
    }

    public override void _Process(double delta)
    {
        // Track the pane (resizable window / splitscreen layout) and repaint every frame.
        Position = Vector2.Zero;
        Size = GetViewportRect().Size;
        UpdateHostile();
        UpdateBrackets();
        QueueRedraw();
    }

    /// <summary>Re-answers the bracket gate for this frame's selection: the selected gun's reach
    /// (<see cref="FlightController.GunReachesTarget"/>), widened by
    /// <see cref="BracketHysteresis"/> while the brackets are already on. Called every
    /// <see cref="_Process"/>; public so the suite drives it without pumping frames. Kept out of
    /// <see cref="_Draw"/> deliberately — a hysteresis that advanced per repaint would depend on how
    /// often the pane redraws.</summary>
    public void UpdateBrackets()
    {
        var target = Selected;
        bool next = Own != null && target is { } t
            && Own.GunReachesTarget(t.Position, t.Velocity,
                _bracketed ? BracketHysteresis : 0f);
        if (next != _bracketed && target is { } shown)
        {
            // The transitions only, like UpdateHostile's: a steady chase inside gun range stays
            // quiet, and the line is what makes the range gate readable without a screenshot.
            Utils.Log.Info("flight",
                $"targeting hud: P{PlayerIndex + 1} brackets {(next ? "on" : "off")} {shown.DisplayName} at {PlanePos.DistanceTo(shown.Position):0} m");
        }

        _bracketed = next;
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

        // The shipped marker (C22): the pilot's own sticky selection, in the original's shape.
        // Drawn in BOTH modes, unlike the hostile tracker below — --debug-markers replaces that
        // tracker (same shape, two colours) but not this, and C24's golden wants the brackets, the
        // label and the debug string in one frame.
        bool selected = Selected is { } target && DrawSelected(font, target, s, markerFont);

        // --debug-markers: every live aircraft at once, red hostile / blue own side, each with
        // its slant range. Replaces the single-hostile marker below rather than stacking on it,
        // so the tracked one is not drawn twice in two colours.
        if (MarkAll && HostilePool != null)
        {
            _marks.Clear();
            CollectMarks(OwnTeam, Own, _hostileScan, _marks);
            int stagger = 0;
            foreach (var (mark, friendly) in _marks)
            {
                if (mark.Source is not FlightController plane
                    || !GodotObject.IsInstanceValid(plane) || !plane.IsInsideTree())
                    continue;
                var at = plane.GlobalPosition;
                DrawOpponent(font, at, friendly ? HudBlue : HudRed,
                    DebugTag(HostileTag(mark.Name), mark, PlanePos.DistanceTo(at),
                        ModeSuffix(plane)),
                    s, markerFont, stagger++);
            }

            return;
        }

        // The tracked AI hostile (H22), now a FALLBACK: a pane whose pilot has a selection marks
        // that one target and nothing else (decision 11), so this draws only where no selection
        // exists at all — a pane with no TargetSelection bound (a spectator, a suite rig) or one
        // whose pilot pressed Target Nothing.
        // UpdateHostile ran this frame, so the reference is at most one scan old; the validity
        // guard covers a hostile freed between the scan and this draw.
        if (!selected && GodotObject.IsInstanceValid(_hostile) && _hostile is { InPlay: true } hostile
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

    /// <summary>The selected target's marker, in the original's own shape: on screen, the fixed-size
    /// bracket box (when the gun reaches it) with the label block BELOW it; off screen, the edge
    /// arrow with the same label block plus the clock bearing, and no box — the original's box hides
    /// off screen because its near-plane test fails.
    ///
    /// <para>⚠ The clock line is the OFF-SCREEN case only. <c>FUN_004579e0</c> draws its third line
    /// unconditionally, but the string it prints comes out of <c>FUN_0049d940</c>'s off-screen pass,
    /// and <c>Targeting HUD Kestrel.png</c> shows a on-screen target with its name alone under it —
    /// so a bearing under every marker would be a line the original does not show.</para>
    ///
    /// <para>The label block's anchor is <c>FUN_004574d0</c>'s: three pixels under the box, flipping
    /// to thirty above it when the box sits within 33 pixels of the viewport's bottom edge. The
    /// anchor is computed from the box whether or not the box is drawn, so a target out of gun range
    /// keeps its label exactly where a bracketed one has it.</para></summary>
    /// <returns>False when the target has no source to draw (nothing was drawn), so the caller can
    /// fall back to the hostile tracker.</returns>
    private bool DrawSelected(Font font, in TargetRef target, float s, int fontSize)
    {
        if (target.Source == null)
        {
            return false;
        }

        var pos = target.Position;
        var color = MarkerColor(target, OwnTeam);
        bool behind = _camera.IsPositionBehind(pos);
        var sp = _camera.UnprojectPosition(pos);
        float m = RefEdgeMargin * s;
        var inner = new Rect2(m, m, Size.X - 2f * m, Size.Y - 2f * m);
        _labelLines.Clear();
        if (!behind && inner.HasPoint(sp))
        {
            if (_bracketed)
            {
                DrawBrackets(sp, color, s);
            }

            LabelLines(target, null, _labelLines);
            float below = sp.Y + (RefBracketHalfH + RefLabelGap) * s;
            float anchor = sp.Y + (RefBracketHalfH + RefLabelBlock) * s <= Size.Y
                ? below
                : sp.Y - (RefBracketHalfH + RefLabelAbove) * s;
            DrawLabelBlock(font, new Vector2(sp.X, anchor), color, s, fontSize);
            return true;
        }

        // Off screen: the edge arrow, and the label block stacked off its tail — DrawOpponent's own
        // geometry, with the tag broken onto its own lines the way HUD.png shows the original's.
        var center = Size / 2f;
        var dir = sp - center;
        if (behind)
            dir = -dir; // the projection of a point behind the camera is mirrored through centre
        if (dir.LengthSquared() < 1f)
            dir = Vector2.Down;
        dir = dir.Normalized();
        var edge = EdgePoint(center, dir, m);
        DrawArrow(edge, dir, RefArrowLen * s, RefArrowHalf * s, s, color);
        LabelLines(target, $"{ClockHour(pos)} o'clock", _labelLines, keepSlots: false);
        var tail = edge - dir * (RefArrowLen + RefTextGap) * s;
        // Centred on the tail rather than hung below it: an edge tag has no box to sit under, and
        // the original clamps its block into the viewport there for the same reason.
        DrawLabelBlock(font, tail - new Vector2(0f, (_labelLines.Count - 1) * RefLabelPitch * s / 2f),
            color, s, fontSize);
        return true;
    }

    /// <summary>The bracket box: six line sprites forming a <c>[ ]</c> pair around the projected
    /// point, 20 x 16 reference pixels with 4-pixel arms (<c>0x00607a0c</c>/<c>0x00607a14</c>/
    /// <c>0x00607a10</c>). Fixed size — scaled for resolution by <paramref name="s"/> and by nothing
    /// else, least of all by range.</summary>
    private void DrawBrackets(Vector2 at, Color color, float s)
    {
        // The same drop shadow the arrow and the tags carry, so the box stays readable over sky.
        Brackets(at + new Vector2(1.5f, 1.5f) * s, Shadow, s);
        Brackets(at, color, s);
    }

    private void Brackets(Vector2 at, Color color, float s)
    {
        float hw = RefBracketHalfW * s, hh = RefBracketHalfH * s, arm = RefBracketArm * s;
        float w = Mathf.Max(1f, RefBracketWidth * s);
        for (int side = 0; side < 2; side++)
        {
            float x = side == 0 ? at.X - hw : at.X + hw;
            var inward = new Vector2(side == 0 ? arm : -arm, 0f);
            var top = new Vector2(x, at.Y - hh);
            var bottom = new Vector2(x, at.Y + hh);
            DrawLine(top, bottom, color, w);
            DrawLine(top, top + inward, color, w);
            DrawLine(bottom, bottom + inward, color, w);
        }
    }

    /// <summary>The label lines stacked downward from <paramref name="anchor"/> at the original's
    /// 15-pixel pitch, each centred on it. The anchor is the block's first LINE (our text is centred
    /// vertically on the point it is given, where the original's is drawn from a baseline), which is
    /// the one place this geometry is ours rather than measured.</summary>
    private void DrawLabelBlock(Font font, Vector2 anchor, Color color, float s, int fontSize)
    {
        for (int i = 0; i < _labelLines.Count; i++)
        {
            if (_labelLines[i].Length == 0)
            {
                continue;   // a blank slot draws nothing and still holds its place
            }

            DrawTag(font, anchor + new Vector2(0f, i * RefLabelPitch * s), _labelLines[i], color,
                fontSize);
        }
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
