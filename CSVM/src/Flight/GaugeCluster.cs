using System;
using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// The original's cockpit gauges as a screen-space HUD (a user request):
/// altimeter (two needles + blinking LOW ALT), speedometer (needle + blinking
/// STALL) and the per-plane damage display (part fills + border bars in
/// green/yellow/red, blinking for a few seconds after a hit).
///
/// Everything is rebuilt from the game's own data. Each player plane carries a
/// 'gauges' subtree under its (otherwise skipped) cockpit in planes.zbd whose
/// meshes ARE the 2D dials — flat polygons in dial-local coords (x right, y up,
/// bezel radius 1): the face is a 12-gon mapping the whole dial texture
/// (altimeter/speedometer/&lt;plane&gt;_damage), each needle is a single textured quad
/// (needle.tif, pivot at the origin, tip +y — the tapered pointer silhouette is the
/// texture's alpha channel, which only the rtexture-tier archives carry; the
/// weapon-gauge arrows are instead shaped by their 5-vertex mesh), the
/// lowalt_on/stallwarning_on overlays are
/// the lit warning window plus two red bezel slashes (redhilite), and each
/// damage zone (nosedamage/taildamage/leftwingdamage/rightwingdamage) is a
/// border bar (greenhilite) plus a part-shaped hatch fill (grn_hatchptrn, tiled
/// UVs) matching that plane's silhouette texture. The interp cockpit.gw script
/// recolors zones by swapping green/yellow/orange/red texture variants — we do
/// the same (orange unused: the reference shots show three states).
///
/// Only screen placement (measured in OriginalScreenshots/HUD.png, 2556×1440,
/// like CompassTape) and the value→angle scales are ours: altimeter 360° per
/// 1,000 ft (long) / 10,000 ft (short), speedometer ~0.72°/mph (measured off the
/// face texture's 0/100/200/300 label angles).
/// </summary>
public sealed partial class GaugeCluster : Control
{
    // ---- state fed by the FlightController ----
    public float AltitudeFt;               // above sea level (the dial is in feet)
    public float AglMeters = float.MaxValue; // above ground (physics ray) — LOW ALT
    public float SpeedMph;
    /// <summary>STALL lamp gate — the flight model's stall WARNING (0.30 fd), which lights well
    /// before the nose-drop the model flies at 0.25 fd.</summary>
    public bool StallWarning;
    /// <summary>Airspeed as a fraction of fd_speed (FlightModel.StallFraction): the STALL lamp's
    /// blink RATE ramps over it. Only read while StallWarning is set.</summary>
    public float StallFrac = 1f;
    /// <summary>Part HP source for the damage display: name → fraction (1 = pristine).
    /// Flight binds PlaneDamage, the damage lab binds its sliders. Null = all green.</summary>
    public Func<string, float>? PartFraction;

    /// <summary>The gun gauge's per-frame state (selected gun group). Null hides it (no loadout).</summary>
    public WeaponGauge? GunGauge;
    /// <summary>The missile gauge's per-frame state (selected ordnance type). Null hides it.</summary>
    public WeaponGauge? MissileGauge;

    // ---- tuning ----
    // A gun belt indicator's colour by remaining fraction: green healthy, yellow low, red empty.
    // Gun-only (BL-024): hardpoints/pylons never show this intermediate tier. TUNE.
    internal const float IndicatorLowFrac = 0.34f;
    // Weapon-gauge arrow sweep rate, shared by both gauges (BL-184, CAP-18: 168.7 ± 1.6 °/sim-s,
    // linear — the measured ~2-frame ease at each end is within noise and NOT a smoothstep).
    // Internal (not private) so the run-tests suite can assert the rate directly.
    internal const float ArrowSweepDegPerSimS = 168.7f;
    // The STALL lamp is a blink-RATE ramp (BL-148, CAP-06 + the two CAP-05 stall clips): brightness
    // is BINARY at every speed and the duty cycle 0.50, while the half-period shortens in proportion
    // to airspeed — 643 ms sim at the 0.30 fd threshold down to 296 ms at 0.15 fd. Fitted through
    // the origin over the five measured speed bins; the affine `5.9·V(mph) − 62` ms wall form fits
    // the same data just as well (the residual is one game frame either way, so the capture cannot
    // separate them) and was declined because it goes negative at low speed. Keyed to the fd
    // FRACTION rather than to mph so a slower airframe blinks at the same rate at its own threshold;
    // only the Bloodhawk (fd_speed 135 m/s) was filmed, so that generalisation is a judgement.
    // ⚠ SIM seconds — the wall figures are 1/1.390 of these and would blink 39% fast.
    // Not reproduced: the original toggles on integer 33.37 ms game frames, which is its frame rate
    // showing through the underlying continuous law, not part of the law.
    internal const float StallBlinkHalfPeriodPerFrac = 2.10f;  // sim s of half-period per unit fd fraction
    // The measurement spans 0.143–0.30 fd and neither fitted form extrapolates below ~43 mph, so the
    // law HOLDS at its deepest measured value rather than ramping on toward a strobe at zero speed.
    internal const float StallBlinkFracFloor = 0.15f;
    private const float LowAltAglM = 50f;     // LOW ALT below this height over ground (user spec)
    private const float WarnBlinkPeriod = 0.4f;  // s per on/off cycle of LOW ALT (TUNE) — the LOW ALT
                                                 // cue is a plain fixed blink, not a ramp
    private const float DamageBlinkTime = 5f;    // s a hit part blinks (user-observed in the original)
    private const float DamageBlinkPeriod = 0.32f; // s per on/off cycle of the hit part (TUNE)
    // Four color states (user-confirmed in the original: green/yellow/orange/red, the
    // full cockpit.gw cycle) over the data's three *_damage_* injure thresholds — each
    // threshold steps to the NEXT color: green above the "green" anim's 0.72, yellow
    // ≤ 0.72, orange ≤ 0.46, red ≤ 0.20 (red on a still-flying plane matches the
    // reference shot). Defaults when a part carries no such anims:
    private const float DefaultYellowAt = 0.72f, DefaultOrangeAt = 0.46f, DefaultRedAt = 0.20f;

    // Screen metrics measured in OriginalScreenshots/HUD.png (2556×1440) via dark-span
    // scans of the bezel rings, scaled by viewport height like CompassTape. All three
    // dials share one size (R 85); x anchors from the left edge except the speedometer
    // (from the right, mirroring the altimeter's margin).
    private const float AltRadius = 85f;
    private const float SpdCenterFromRight = 420f, SpdCenterY = 1108.5f, SpdRadius = 85f;
    private const float DmgRadius = 85f;
    // The two weapon gauges (measured off OriginalScreenshots/HUD.png): the ROCKETS dial sits one
    // dial-pitch (190.5 px, the alt→damage spacing) above the altimeter, the GUNS dial the same
    // above the speedometer. Same radius as the other dials.
    private const float MissileRadius = 85f;
    private const float GunCenterFromRight = 420f, GunCenterY = 918f, GunRadius = 85f;

    private static readonly Vector2 AltCenter = new(425.5f, 1108.5f);
    private static readonly Vector2 DmgCenter = new(426.5f, 1299f);
    private static readonly Vector2 MissileCenter = new(425.5f, 918f);

    // flat indicator tints when a colour-variant png is missing from the archive
    private static readonly Color[] IndicatorFlat =
    {
        new(0.2f, 0.9f, 0.2f), new(0.95f, 0.9f, 0.1f), new(0.9f, 0.15f, 0.15f),
    };

    // flat zone tints when a color-variant png is missing from the archive
    private static readonly Color[] ZoneFlat =
    {
        new(0.25f, 0.9f, 0.2f), new(0.95f, 0.9f, 0.1f),
        new(0.95f, 0.55f, 0.05f), new(0.9f, 0.1f, 0.1f),
    };

    private readonly List<GaugePoly> _altFace = new();
    private readonly List<GaugePoly> _altWarn = new();  // lowalt_on (blinks)
    private readonly List<GaugePoly> _spdFace = new();
    private readonly List<GaugePoly> _spdWarn = new();  // stallwarning_on (blinks)
    private readonly List<GaugePoly> _dmgFace = new();
    private readonly List<DamageZone> _zones = new();
    // color-variant textures for the zone swap (0 green / 1 yellow / 2 orange / 3 red)
    private readonly Texture2D?[] _hilite = new Texture2D?[4];
    private readonly Texture2D?[] _hatch = new Texture2D?[4];
    private readonly GaugeGeom _gunGaugeGeom = new();
    private readonly GaugeGeom _missileGaugeGeom = new();
    // Glyph atlas for the digit/type cycles: char → texture (zero..nine, A..Z; space/unknown = null).
    private readonly Dictionary<char, Texture2D?> _glyphs = new();
    // Belt-indicator colour variants (0 green / 1 yellow / 2 red) — the hilite bar and the light.
    private readonly Texture2D?[] _indHilite = new Texture2D?[3];
    private readonly Texture2D?[] _indLight = new Texture2D?[3];

    private GaugePoly? _altHundreds, _altThousands;
    private GaugePoly? _spdNeedle;
    private double _time;
    // Live sweep angle of each weapon-gauge pointer (degrees, same convention as DrawGaugePoly's
    // rotDeg); NaN means "not yet drawn" — the next update snaps to target instead of sweeping in
    // from zero.
    private float _gunArrowAngle = float.NaN;
    private float _missileArrowAngle = float.NaN;
    // The STALL lamp's blink, integrated rather than read off a clock: its period changes with
    // airspeed, so PosMod over accumulated time would jump the lamp mid-dwell whenever the rate
    // moved. _stallBlinkPhase is the fraction of the current half-period elapsed.
    private double _stallBlinkPhase;
    private float _stallDwellS;   // sim time the current dwell has run, for the toggle log
    private bool _stallLampOn = true;
    private bool _stallWarnPrev;  // last frame's StallWarning, so the crossing logs once

    /// <summary>Whether the lit STALL overlay draws this frame. Internal so the run-tests suite can
    /// read the blink out of a live cluster rather than re-deriving it.</summary>
    internal bool StallLampLit => StallWarning && _stallLampOn;

    private bool WarnPhaseOn => Mathf.PosMod((float)_time, WarnBlinkPeriod) < WarnBlinkPeriod * 0.5f;
    private bool DamagePhaseOn => Mathf.PosMod((float)_time, DamageBlinkPeriod) < DamageBlinkPeriod * 0.5f;

    /// <summary>Builds the cluster from the plane's 'gauges' subtree in planes.zbd and
    /// the chapter texture archive. Null when the subtree or its dial textures are
    /// missing (each miss is logged by the archive).</summary>
    public static GaugeCluster? Build(GameZ planes, string planeName, TextureArchive textures,
        IReadOnlyList<DestroyablePart> parts)
    {
        var root = planes.FindByName(planeName);
        if (root == null)
            return null;
        var gauges = FindDescendant(planes, root, "gauges");
        if (gauges == null)
        {
            GD.Print($"[gauges] '{planeName}' has no gauges subtree — HUD dials off");
            return null;
        }

        var cluster = new GaugeCluster
        {
            MouseFilter = MouseFilterEnum.Ignore,
            TextureRepeat = TextureRepeatEnum.Enabled, // the hatch fills tile their UVs
        };
        cluster.SetAnchorsPreset(LayoutPreset.FullRect);

        foreach (int ci in gauges.Children)
        {
            var dial = planes.Nodes[ci];
            switch (dial.Name.ToLowerInvariant())
            {
                case "altimeter":
                    cluster.ExtractInstrument(planes, textures, dial,
                        cluster._altFace, cluster._altWarn,
                        ("hundreds", p => cluster._altHundreds = p),
                        ("thousands", p => cluster._altThousands = p));
                    break;
                case "speedometer":
                    cluster.ExtractInstrument(planes, textures, dial,
                        cluster._spdFace, cluster._spdWarn,
                        ("speed", p => cluster._spdNeedle = p));
                    break;
                case "damageindicator":
                    cluster.ExtractDamageDial(planes, textures, dial, parts);
                    break;
                case "gungauge":
                    cluster.ExtractWeaponGauge(planes, textures, dial, cluster._gunGaugeGeom, "gg");
                    break;
                case "missilegauge":
                    cluster.ExtractWeaponGauge(planes, textures, dial, cluster._missileGaugeGeom, "mg");
                    break;
            }
        }

        // Load the shared digit/letter atlas + belt-indicator colour variants once, only if a weapon
        // gauge was actually found (all 11 flyable models carry both, but a lab plane may not).
        if (cluster._gunGaugeGeom.HasGeometry || cluster._missileGaugeGeom.HasGeometry)
        {
            cluster.LoadGaugeTextures(textures);
        }

        if (cluster._altFace.Count == 0 && cluster._spdFace.Count == 0 && cluster._dmgFace.Count == 0)
        {
            GD.Print($"[gauges] '{planeName}': no dial geometry extracted — HUD dials off");
            return null;
        }
        return cluster;
    }

    /// <summary>Restarts the warning/blink state (respawn).</summary>
    public void Reset()
    {
        foreach (var z in _zones)
            z.BlinkLeft = 0f;
        _gunArrowAngle = float.NaN;
        _missileArrowAngle = float.NaN;
        _stallBlinkPhase = 0.0;
        _stallDwellS = 0f;
        _stallLampOn = true;
        _stallWarnPrev = false;
    }

    /// <summary>A part took damage: its fill + border blink for the next few seconds
    /// (the original blinks even inside the green range — user-verified).</summary>
    public void OnPartDamage(string partName)
    {
        foreach (var z in _zones)
            if (z.Part.Equals(partName, StringComparison.OrdinalIgnoreCase))
                z.BlinkLeft = DamageBlinkTime;
    }

    public override void _Process(double delta)
    {
        _time += delta;
        float simDt = GameClock.Current?.FrameDt ?? (float)delta;
        if (GunGauge is { } gg)
            _gunArrowAngle = TweenArrow(_gunArrowAngle, TargetArrowAngle(_gunGaugeGeom.Positions, gg.Selected), simDt);
        if (MissileGauge is { } mg)
            _missileArrowAngle = TweenArrow(_missileArrowAngle, TargetArrowAngle(_missileGaugeGeom.Positions, mg.Selected), simDt);
        AdvanceStallLamp(simDt);
        foreach (var z in _zones)
            z.BlinkLeft = Mathf.Max(0f, z.BlinkLeft - (float)delta);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var vp = GetViewportRect().Size;
        float s = HudMetrics.Scale(this);

        // altimeter: long needle 360°/1,000 ft, short 360°/10,000 ft, 0 at the top
        var altC = new Vector2(AltCenter.X * s, FromBottom(AltCenter.Y, s, vp.Y));
        float altR = AltRadius * s;
        foreach (var p in _altFace)
            DrawGaugePoly(p, altC, altR);
        if (AglMeters < LowAltAglM && WarnPhaseOn)
            foreach (var p in _altWarn)
                DrawGaugePoly(p, altC, altR);
        float ft = Mathf.Max(0f, AltitudeFt);
        if (_altThousands != null)
            DrawGaugePoly(_altThousands, altC, altR, ft % 10000f / 10000f * 360f);
        if (_altHundreds != null)
            DrawGaugePoly(_altHundreds, altC, altR, ft % 1000f / 1000f * 360f);

        // speedometer: ~0.72°/mph (the face's 100-mph labels sit ~71.5° apart)
        var spdC = new Vector2(vp.X - SpdCenterFromRight * s, FromBottom(SpdCenterY, s, vp.Y));
        float spdR = SpdRadius * s;
        foreach (var p in _spdFace)
            DrawGaugePoly(p, spdC, spdR);
        if (StallWarning && _stallLampOn)
            foreach (var p in _spdWarn)
                DrawGaugePoly(p, spdC, spdR);
        if (_spdNeedle != null)
            DrawGaugePoly(_spdNeedle, spdC, spdR, Mathf.Max(0f, SpeedMph) * 0.72f);

        // damage display: face silhouette, then each zone's border bar + hatch fill
        // in its color; a freshly hit zone blinks (fill + border) for a few seconds
        var dmgC = new Vector2(DmgCenter.X * s, FromBottom(DmgCenter.Y, s, vp.Y));
        float dmgR = DmgRadius * s;
        foreach (var p in _dmgFace)
            DrawGaugePoly(p, dmgC, dmgR);
        foreach (var z in _zones)
        {
            if (z.BlinkLeft > 0f && !DamagePhaseOn)
                continue; // blink-off phase hides the whole zone (fill + outline)
            float frac = PartFraction?.Invoke(z.Part) ?? 1f;
            int color = DamageZoneColor(frac, z.YellowAt, z.OrangeAt, z.RedAt);
            foreach (var p in z.Border)
                DrawGaugePoly(p, dmgC, dmgR, 0f, _hilite[color], ZoneFlat[color]);
            foreach (var p in z.Fill)
                DrawGaugePoly(p, dmgC, dmgR, 0f, _hatch[color], ZoneFlat[color]);
        }

        // The two weapon gauges: the ROCKETS dial above the altimeter, the GUNS dial above the
        // speedometer (mirrored from the right edge). Each renders only when the FlightController is
        // feeding it — hidden in the static viewer, which carries no loadout.
        if (MissileGauge is { } mg && _missileGaugeGeom.HasGeometry)
        {
            var c = new Vector2(MissileCenter.X * s, FromBottom(MissileCenter.Y, s, vp.Y));
            DrawWeaponGauge(_missileGaugeGeom, mg, c, MissileRadius * s, isGun: false, _missileArrowAngle);
        }
        if (GunGauge is { } gg && _gunGaugeGeom.HasGeometry)
        {
            var c = new Vector2(vp.X - GunCenterFromRight * s, FromBottom(GunCenterY, s, vp.Y));
            DrawWeaponGauge(_gunGaugeGeom, gg, c, GunRadius * s, isGun: true, _gunArrowAngle);
        }
    }

    /// <summary>Half of the STALL lamp's blink period, in SIM seconds, at an airspeed of
    /// <paramref name="stallFrac"/> × fd_speed — proportional to speed, held flat below the deepest
    /// speed the capture reached. Duty is 0.50, so the full period is twice this. Internal so the
    /// run-tests suite can assert the law against CAP-06's two anchors directly.</summary>
    internal static float StallBlinkHalfPeriodS(float stallFrac) =>
        StallBlinkHalfPeriodPerFrac * Mathf.Max(stallFrac, StallBlinkFracFloor);

    // Guns: green > low > empty, indexing the 3 indicator colour variants. The low tier is
    // gun-only — see IndicatorLowFrac's comment. Internal (not private) so the run-tests suite can
    // assert both colour paths directly.
    internal static int GunIndicatorColor(float frac) => frac <= 0f ? 2 : frac <= IndicatorLowFrac ? 1 : 0;

    // Hardpoints/pylons: green > empty, no intermediate colour (confirmed against the original —
    // BL-024). Never reuse IndicatorLowFrac here.
    internal static int HardpointIndicatorColor(float frac) => frac <= 0f ? 2 : 0;

    // Damage zones: green > yellow > orange > red, over the zone's COMBINED armor+health fraction
    // (BL-085's PartState.Fraction) against thresholds MINED per-part from the data's own
    // *_damage_green/yellow/red injure_anims (never hand-authored — BL-173's refuted fix shape was
    // an armor-fraction/health-fraction ring split; the manual's four bands fall out of the shipped
    // combined-scale numbers instead, docs/formats/hud.md "Thresholds"). Crosses to the next
    // (worse) colour once frac drops TO OR BELOW its threshold, the same convention
    // DamageVisuals/DamageLab use for injure_anims thresholds. Internal so the run-tests suite can
    // assert the sequence directly.
    internal static int DamageZoneColor(float frac, float yellowAt, float orangeAt, float redAt) =>
        frac > yellowAt ? 0 : frac > orangeAt ? 1 : frac > redAt ? 2 : 3;

    // Internal (not private) so the run-tests suite can assert the sweep math directly.
    internal static float TargetArrowAngle(int positions, int selected) =>
        positions > 0 ? -(360f / positions) * selected : 0f;

    /// <summary>Advances a weapon-gauge arrow angle at most <see cref="ArrowSweepDegPerSimS"/> ×
    /// simDt toward target, routed the shortest way round (BL-184). NaN snaps instead of sweeping
    /// in from an undefined pose (gauge just appeared, or a respawn cleared it via Reset).</summary>
    internal static float TweenArrow(float current, float target, float simDt)
    {
        if (float.IsNaN(current))
            return target;
        float delta = Mathf.PosMod(target - current + 180f, 360f) - 180f;
        float maxStep = ArrowSweepDegPerSimS * simDt;
        return Mathf.Abs(delta) <= maxStep ? target : current + Mathf.Sign(delta) * maxStep;
    }

    /// <summary>Integrates the STALL lamp's blink one sim step. The lamp lights the instant the
    /// warning does and its phase restarts when the warning clears — the original shows no
    /// hysteresis at the threshold (on at 89.9/90.0 mph decelerating, 90.0/89.9 accelerating).
    /// Internal so the run-tests suite can drive it on its own clock.</summary>
    internal void AdvanceStallLamp(float simDt)
    {
        if (StallWarning != _stallWarnPrev)
        {
            // The threshold crossing itself, with the fraction it happened at — the other half of
            // what a scripted run needs to check the cue against the capture.
            Log.Debug("flight", $"stall warning {(StallWarning ? "on" : "off")} frac={StallFrac:0.000} mph={SpeedMph:0.0}");
            _stallWarnPrev = StallWarning;
        }
        if (!StallWarning)
        {
            _stallBlinkPhase = 0.0;
            _stallDwellS = 0f;
            _stallLampOn = true;
            return;
        }
        _stallDwellS += simDt;
        _stallBlinkPhase += simDt / StallBlinkHalfPeriodS(StallFrac);
        while (_stallBlinkPhase >= 1.0)
        {
            _stallBlinkPhase -= 1.0;
            _stallLampOn = !_stallLampOn;
            // The dwell that just ended, in sim ms — the one number the blink law is measured in,
            // so a run can be checked against CAP-06 without eyes on the lamp. Carries its own
            // times as values: the log has no timestamp column by design.
            Log.Debug("flight", $"stall lamp {(_stallLampOn ? "lit" : "dark")} dwell_ms={_stallDwellS * 1000f:0} frac={StallFrac:0.000} half_ms={StallBlinkHalfPeriodS(StallFrac) * 1000f:0}");
            _stallDwellS = 0f;
        }
    }

    // ---- extraction ----

    private static GameZNode? FindDescendant(GameZ gz, GameZNode from, string name)
    {
        var queue = new Queue<GameZNode>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var n = queue.Dequeue();
            if (n.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return n;
            foreach (int c in n.Children)
                queue.Enqueue(gz.Nodes[c]);
        }
        return null;
    }

    /// <summary>All flat polys of a node's mesh in dial-local coords. Node Local
    /// transforms are ignored on purpose: needles carry an arbitrary modeled rest
    /// rotation (we set the angle from the value), everything else is identity.</summary>
    private static List<GaugePoly> MeshPolys(GameZ gz, TextureArchive textures, GameZNode node)
    {
        var result = new List<GaugePoly>();
        if (node.MeshIndex < 0 || node.MeshIndex >= gz.Meshes.Count)
            return result;
        var mesh = gz.Meshes[node.MeshIndex];
        foreach (var poly in mesh.Polygons)
        {
            if (poly.VertexIndices.Count < 3)
                continue;
            var pts = new Vector2[poly.VertexIndices.Count];
            for (int i = 0; i < pts.Length; i++)
            {
                var v = mesh.Vertices[poly.VertexIndices[i]];
                pts[i] = new Vector2(v.X, v.Y);
            }
            var uvs = new Vector2[pts.Length];
            if (poly.UvCoords != null && poly.UvCoords.Count == pts.Length)
                for (int i = 0; i < pts.Length; i++)
                    uvs[i] = poly.UvCoords[i];
            string texName = poly.MaterialIndex >= 0 && poly.MaterialIndex < gz.Materials.Count
                ? gz.Materials[poly.MaterialIndex].TextureName ?? "" : "";
            result.Add(new GaugePoly
            {
                Points = pts,
                Uvs = uvs,
                Tex = texName.Length > 0 ? textures.Find(texName) : null,
                Priority = poly.Priority,
                TexName = texName,
            });
        }
        return result;
    }

    private static float CenterX(GaugePoly p)
    {
        float sum = 0f;
        foreach (var pt in p.Points)
        {
            sum += pt.X;
        }
        return p.Points.Length > 0 ? sum / p.Points.Length : 0f;
    }

    /// <summary>The trailing integer of a name like "ggindicator3" (→ 3); 0 if it ends in no digits.</summary>
    private static int TrailingInt(string name)
    {
        int i = name.Length;
        while (i > 0 && char.IsDigit(name[i - 1]))
        {
            i--;
        }
        return i < name.Length && int.TryParse(name[i..], out var n) ? n : 0;
    }

    /// <summary>The dials are measured off HUD.png as absolute 1440p-reference y coordinates, but
    /// they are really anchored to the BOTTOM of the screen (they sit 331 / 141 px up from it).
    /// Measuring from the bottom is identical to <c>refY · s</c> whenever s is the plain
    /// height ratio (single player), and is what keeps them on screen when a splitscreen pane
    /// draws them at a damped, larger-than-proportional scale (see <see cref="HudMetrics"/>).</summary>
    private static float FromBottom(float refY, float s, float viewportH) =>
        viewportH - (HudMetrics.ReferenceHeight - refY) * s;

    /// <summary>A count right-aligned into <paramref name="width"/> digit cells, space-padded
    /// (clamped 0..9999, the 4-cell readout's range).</summary>
    private static string FormatCount(int count, int width)
    {
        string s = Mathf.Clamp(count, 0, 9999).ToString();
        if (s.Length > width)
            s = s[^width..];
        return s.PadLeft(width);
    }

    /// <summary>A weapon name upper-cased, left-aligned and padded/truncated to the type readout's
    /// <paramref name="width"/> cells (the atlas is uppercase-only).</summary>
    private static string FormatType(string type, int width)
    {
        string s = type.ToUpperInvariant();
        if (s.Length > width)
            s = s[..width];
        return s.PadRight(width);
    }

    /// <summary>An altimeter/speedometer node: face polys from any unnamed child mesh
    /// (g784 …), warning overlays from *_on children, needles by exact child name.</summary>
    private void ExtractInstrument(GameZ gz, TextureArchive textures, GameZNode dial,
        List<GaugePoly> face, List<GaugePoly> warn,
        params (string Name, Action<GaugePoly> Set)[] needles)
    {
        foreach (int ci in dial.Children)
        {
            var child = gz.Nodes[ci];
            bool isNeedle = false;
            foreach (var (name, set) in needles)
                if (child.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    var polys = MeshPolys(gz, textures, child);
                    if (polys.Count > 0)
                        set(polys[0]); // a needle is a single quad
                    isNeedle = true;
                    break;
                }
            if (isNeedle)
                continue;
            var target = child.Name.EndsWith("_on", StringComparison.OrdinalIgnoreCase) ? warn : face;
            target.AddRange(MeshPolys(gz, textures, child));
        }
        face.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    /// <summary>The damage dial: each *damage child is one zone — border bar ("hilite" texture) +
    /// hatch fill — and everything else is the silhouette face. Color thresholds come from the
    /// matching destroyable part's injure anims.
    ///
    /// <para><b>Where the face lives differs per plane</b> (user-reported via a 4P
    /// screenshot): the Bloodhawk carries the dial's dark backing disc on the `damageindicator`
    /// node itself, but every other plane leaves that node mesh-less (`mesh_index` −1) and hangs
    /// the disc off an extra generically-named child (`g951`, `g927`, `g1156`, `g843`, …) — the
    /// same untextured 12-gon either way. Reading only the dial node's own mesh therefore drew a
    /// backing disc for the Bloodhawk and bare wireframe zones for all ten other aircraft. So any
    /// non-zone child counts toward the face, which is exactly the rule
    /// <see cref="ExtractInstrument"/> already uses for the other two dials.</para></summary>
    private void ExtractDamageDial(GameZ gz, TextureArchive textures, GameZNode dial,
        IReadOnlyList<DestroyablePart> parts)
    {
        _dmgFace.AddRange(MeshPolys(gz, textures, dial));

        foreach (int ci in dial.Children)
        {
            var child = gz.Nodes[ci];
            if (!child.Name.EndsWith("damage", StringComparison.OrdinalIgnoreCase))
            {
                _dmgFace.AddRange(MeshPolys(gz, textures, child));
                continue;
            }
            var zone = new DamageZone { Part = child.Name[..^"damage".Length] };
            foreach (var p in MeshPolys(gz, textures, child))
                (p.TexName.Contains("hilite", StringComparison.OrdinalIgnoreCase)
                    ? zone.Border : zone.Fill).Add(p);
            foreach (var part in parts)
                if (part.Name.Equals(zone.Part, StringComparison.OrdinalIgnoreCase))
                {
                    // each anim threshold steps to the NEXT color (see the constants)
                    foreach (var (frac, anim) in part.InjureAnims)
                    {
                        if (anim.EndsWith("_damage_green", StringComparison.OrdinalIgnoreCase))
                            zone.YellowAt = frac;
                        else if (anim.EndsWith("_damage_yellow", StringComparison.OrdinalIgnoreCase))
                            zone.OrangeAt = frac;
                        else if (anim.EndsWith("_damage_red", StringComparison.OrdinalIgnoreCase))
                            zone.RedAt = frac;
                    }
                    break;
                }
            _zones.Add(zone);
        }
        _dmgFace.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        // the four swap variants the cockpit.gw texture cycle names
        string[] hilite = { "greenhilite", "yellowhilite", "orangehilite", "redhilite" };
        string[] hatch = { "grn_hatchptrn", "yel_hatchptrn", "orng_hatchptrn", "red_hatchptrn" };
        for (int i = 0; i < 4; i++)
        {
            _hilite[i] = textures.Find(hilite[i]);
            _hatch[i] = textures.Find(hatch[i]);
        }
    }

    /// <summary>A weapon gauge (gungauge/missilegauge): the same circular-dial layout on all 11
    /// planes. Its named children are the 4-digit ammo readout (<c>4char_ammo</c>), the 6-char type
    /// name (<c>6char_type</c>), the belt lights (<c>{prefix}indicator0..</c>) and the pointer
    /// (<c>{prefix}arrow</c>); anything else (the generically-named face child <c>g815</c>/<c>g819</c>)
    /// is dial face — the same "unrecognised child = face" rule the damage dial needs. The digit/type
    /// glyphs and the indicator colours are texture cycles the FlightController drives via
    /// <see cref="WeaponGauge"/>; here we only extract the fixed geometry and the belt order.</summary>
    private void ExtractWeaponGauge(GameZ gz, TextureArchive textures, GameZNode dial, GaugeGeom geom, string prefix)
    {
        geom.Face.AddRange(MeshPolys(gz, textures, dial)); // -1 on every plane, but follows the face rule
        var byIndex = new SortedDictionary<int, List<GaugePoly>>();
        foreach (int ci in dial.Children)
        {
            var child = gz.Nodes[ci];
            string name = child.Name.ToLowerInvariant();
            if (name == "4char_ammo")
            {
                geom.Digits.AddRange(MeshPolys(gz, textures, child));
            }
            else if (name == "6char_type")
            {
                geom.TypeChars.AddRange(MeshPolys(gz, textures, child));
            }
            else if (name == prefix + "arrow")
            {
                var polys = MeshPolys(gz, textures, child);
                if (polys.Count > 0)
                {
                    geom.Arrow = polys[0]; // a single arrow-shaped polygon (5 verts, tip +y)
                }
            }
            else if (name.StartsWith(prefix + "indicator", StringComparison.Ordinal))
            {
                byIndex[TrailingInt(name)] = MeshPolys(gz, textures, child);
            }
            else
            {
                geom.Face.AddRange(MeshPolys(gz, textures, child)); // the generic face child
            }
        }
        geom.Digits.Sort((a, b) => CenterX(a).CompareTo(CenterX(b)));
        geom.TypeChars.Sort((a, b) => CenterX(a).CompareTo(CenterX(b)));
        int max = -1;
        foreach (int k in byIndex.Keys)
        {
            max = Math.Max(max, k);
        }
        for (int i = 0; i <= max; i++)
        {
            geom.Indicators.Add(byIndex.TryGetValue(i, out var polys) ? polys : new List<GaugePoly>());
        }
        geom.Positions = geom.Indicators.Count; // 4 (gun) or 8 (missile) — the arrow's step angle
        geom.Face.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    /// <summary>Loads the shared digit/letter atlas (the <c>zero.tif</c>…<c>nine.tif</c> + <c>A.tif</c>…
    /// <c>Z.tif</c> cycle both readouts index) and the belt-indicator colour variants
    /// (green/yellow/red). Space and unrepresented chars stay absent → drawn as a blank cell.</summary>
    private void LoadGaugeTextures(TextureArchive textures)
    {
        string[] digits = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };
        for (int d = 0; d < 10; d++)
        {
            _glyphs[(char)('0' + d)] = textures.Find(digits[d]);
        }
        for (char c = 'A'; c <= 'Z'; c++)
        {
            _glyphs[c] = textures.Find(c.ToString());
        }
        string[] hilite = { "greenhilite", "yellowhilite", "redhilite" };
        string[] light = { "greenindicator", "yellowindicator", "redindicator" };
        for (int i = 0; i < 3; i++)
        {
            _indHilite[i] = textures.Find(hilite[i]);
            _indLight[i] = textures.Find(light[i]);
        }
    }

    /// <summary>Draws one weapon gauge: the labelled face, the belt lights for the slots the plane
    /// actually has — guns step green/yellow/red by remaining fraction, hardpoints step green/red
    /// with no intermediate colour — the right-aligned digit count, the left-aligned type name,
    /// then the pointer at its animated sweep angle (tweened toward the selected belt slot in
    /// <see cref="_Process"/>; the readout above still snaps).</summary>
    private void DrawWeaponGauge(GaugeGeom geom, WeaponGauge state, Vector2 center, float radius, bool isGun,
        float arrowAngle)
    {
        foreach (var p in geom.Face)
            DrawGaugePoly(p, center, radius);
        for (int i = 0; i < geom.Indicators.Count; i++)
        {
            if (i >= state.Slots.Count)
                continue; // a belt position this airframe does not use stays dark
            int color = isGun ? GunIndicatorColor(state.Slots[i]) : HardpointIndicatorColor(state.Slots[i]);
            foreach (var p in geom.Indicators[i])
            {
                bool bar = p.TexName.Contains("hilite", StringComparison.OrdinalIgnoreCase);
                DrawGaugePoly(p, center, radius, 0f, bar ? _indHilite[color] : _indLight[color], IndicatorFlat[color]);
            }
        }
        DrawGlyphs(geom.Digits, FormatCount(state.Count, geom.Digits.Count), center, radius);
        DrawGlyphs(geom.TypeChars, FormatType(state.Type, geom.TypeChars.Count), center, radius);
        if (geom.Arrow != null && geom.Positions > 0)
            DrawGaugePoly(geom.Arrow, center, radius, arrowAngle);
    }

    /// <summary>Draws each fixed glyph quad with the atlas texture for its character; a space or an
    /// unrepresented char leaves that cell blank (the atlas has no space glyph — that IS the space).</summary>
    private void DrawGlyphs(List<GaugePoly> quads, string text, Vector2 center, float radius)
    {
        for (int i = 0; i < quads.Count && i < text.Length; i++)
        {
            if (_glyphs.TryGetValue(text[i], out var g) && g != null)
                DrawGaugePoly(quads[i], center, radius, 0f, g);
        }
    }

    /// <summary>Draws one extracted poly at a dial's screen center/radius, rotated
    /// clockwise by rotDeg about the dial center (needles). Dial-local y-up flips to
    /// screen y-down; an override texture substitutes the zone color variants (with
    /// a flat tint standing in when the variant png is missing).</summary>
    private void DrawGaugePoly(GaugePoly p, Vector2 center, float radius, float rotDeg = 0f,
        Texture2D? overrideTex = null, Color? missingTint = null)
    {
        var tex = overrideTex ?? p.Tex;
        int n = p.Points.Length;
        var pts = new Vector2[n];
        float rad = Mathf.DegToRad(rotDeg);
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        for (int i = 0; i < n; i++)
        {
            var v = p.Points[i];
            // clockwise-on-screen rotation in x-right/y-up dial coords
            float x = v.X * cos + v.Y * sin;
            float y = -v.X * sin + v.Y * cos;
            pts[i] = new Vector2(center.X + x * radius, center.Y - y * radius);
        }
        var colors = new Color[n];
        var flat = tex != null ? Colors.White
            : missingTint ?? new Color(0.85f, 0.85f, 0.8f);
        for (int i = 0; i < n; i++)
            colors[i] = flat;
        if (tex != null)
            DrawPolygon(pts, colors, p.Uvs, tex);
        else
            DrawPolygon(pts, colors);
    }

    /// <summary>Live state for one weapon gauge (gun or rocket), pushed by the FlightController each
    /// frame. Positions map 1:1 onto the gauge's belt indicators: <see cref="Slots"/>[i] drives
    /// <c>ggindicator</c>/<c>mgindicator</c> i (green &gt; low &gt; empty), the arrow points at
    /// <see cref="Selected"/>, <see cref="Count"/> fills the 4-digit readout and <see cref="Type"/>
    /// the 6-char name. Left null (the default) hides that gauge — the static viewer has no loadout.</summary>
    public sealed class WeaponGauge
    {
        public int Count;                                       // rounds shown in 4char_ammo (0..9999)
        public string Type = "";                                // 6char_type name (weapon NAME, upper-cased)
        public int Selected;                                    // 0-based belt position the arrow points at
        public IReadOnlyList<float> Slots = Array.Empty<float>(); // per-indicator ammo fraction 0..1
    }

    /// <summary>One flat gauge polygon extracted from the mesh: dial-local points
    /// (x right, y up, radius 1), normalized UVs, source texture, draw priority.</summary>
    private sealed class GaugePoly
    {
        public Vector2[] Points = Array.Empty<Vector2>();
        public Vector2[] Uvs = Array.Empty<Vector2>();
        public Texture2D? Tex;
        public int Priority;
        public string TexName = "";
    }

    private sealed class DamageZone
    {
        public string Part = "";               // "nose" / "tail" / "leftwing" / "rightwing"
        public List<GaugePoly> Border = new(); // the bezel-edge bar ("hilite")
        public List<GaugePoly> Fill = new();   // the part-shaped hatch overlay
        public float YellowAt = DefaultYellowAt, OrangeAt = DefaultOrangeAt, RedAt = DefaultRedAt;
        public float BlinkLeft;                // s of post-hit blinking remaining
    }

    /// <summary>One weapon gauge's extracted geometry (gungauge or missilegauge). All flat, dial-local
    /// like the other dials. The digit / type quads are ordered left→right; each indicator's polys sit
    /// at its parsed index (ggindicator3 → <see cref="Indicators"/>[3]); the arrow rotates about the
    /// centre to point at a belt position.</summary>
    private sealed class GaugeGeom
    {
        public readonly List<GaugePoly> Face = new();          // the labelled dial face (gungauge/missilegauge.tif + ring)
        public readonly List<GaugePoly> Digits = new();        // 4char_ammo quads, left→right
        public readonly List<GaugePoly> TypeChars = new();     // 6char_type quads, left→right
        public readonly List<List<GaugePoly>> Indicators = new(); // belt lights by index (0 = top, CCW)
        public GaugePoly? Arrow;                               // gg/mgarrow, rest points up (belt position 0)
        public int Positions;                                  // belt positions (4 gun / 8 missile) → arrow step
        public bool HasGeometry => Face.Count > 0 || Digits.Count > 0;
    }
}
