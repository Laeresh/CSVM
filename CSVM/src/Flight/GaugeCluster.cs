using System;
using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// The original's cockpit gauges as a screen-space HUD: altimeter (two needles + blinking LOW
/// ALT), speedometer (needle + blinking STALL), and the per-plane damage display (part fills +
/// border bars, blinking after a hit). Everything is rebuilt from each plane's own 'gauges'
/// subtree in planes.zbd — see docs/formats/hud.md for the dial geometry, texture cycles and
/// scales. Only screen placement and the value→angle scales are ours, measured in
/// OriginalScreenshots/HUD.png like CompassTape.
/// </summary>
public sealed partial class GaugeCluster : Control
{
    // Gun belt indicator colour by remaining fraction: green/yellow/red. Gun-only; hardpoints/
    // pylons never show this tier. TUNE — see docs/formats/hud.md for the calibration and the
    // still-owed playtest. ⚠ Do not raise it toward 1/3; that lights the cue at nearly two-thirds full.
    public const float IndicatorLowFrac = 0.15f;
    // Weapon-gauge arrow sweep rate, shared by both gauges (measured off original-game footage:
    // 168.7 ± 1.6 °/sim-s, linear — the ~2-frame ease at each end is within noise and NOT a smoothstep).
    // Public so CSVM.Tests (GaugeArrowTweenTests) can assert the rate directly.
    public const float ArrowSweepDegPerSimS = 168.7f;

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
    // The hardpoint dial's belt-light ring is 8 positions on every airframe regardless of the
    // loadout's pylon count (user-confirmed against the original; markers.md) —
    // never derive it from the bound Hardpoints count. Indicator i is pylon i+1: the belt
    // light and arrow-target math both index by PYLON NUMBER, not by position in a compacted list.
    internal const int HardpointRingSize = 8;
    // The STALL lamp's blink-RATE ramp: half-period proportional to the fd fraction, held flat
    // below StallBlinkFracFloor. See docs/formats/hud.md for the measurement and its fit.
    // ⚠ SIM seconds — the wall figures are 1/1.390 of these and would blink 39% fast.
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
    // Live sweep state of each weapon-gauge pointer and the STALL lamp's blink — plain structs
    // so CSVM.Tests can drive them without a live Control.
    private ArrowSweep _gunArrow = new();
    private ArrowSweep _missileArrow = new();
    private StallLamp _stallLamp = new();

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

    // Guns: green > low > empty, indexing the 3 indicator colour variants. The low tier is
    // gun-only — see IndicatorLowFrac's comment. Public so CSVM.Tests (GaugeColoursTests) can
    // assert both colour paths directly.
    public static int GunIndicatorColor(float frac) => frac <= 0f ? 2 : frac <= IndicatorLowFrac ? 1 : 0;

    // Hardpoints/pylons: green > empty, no intermediate colour (confirmed against the
    // original). Never reuse IndicatorLowFrac here.
    public static int HardpointIndicatorColor(float frac) => frac <= 0f ? 2 : 0;

    // The colour of belt indicator i, including positions past the end of the loadout: an unfitted
    // slot reads RED, the same as a fitted-but-spent one. In the original every belt light on the
    // dial is lit — a plane with fewer guns/pylons than the dial has positions shows the surplus in
    // red, it does not leave them dark. Public so CSVM.Tests can assert that directly.
    public static int SlotIndicatorColor(IReadOnlyList<float> slots, int i, bool isGun) =>
        i >= slots.Count ? 2
        : isGun ? GunIndicatorColor(slots[i]) : HardpointIndicatorColor(slots[i]);

    // Damage zones: green > yellow > orange > red over the zone's combined armor+health fraction,
    // thresholds mined per-part from the data's own injure_anims — see docs/formats/hud.md
    // "Thresholds". ⚠ Do not re-shape this as a per-pool ring split; that reading is refuted.
    // Crosses to the next colour at or below threshold. Public for CSVM.Tests.
    public static int DamageZoneColor(float frac, float yellowAt, float orangeAt, float redAt) =>
        frac > yellowAt ? 0 : frac > orangeAt ? 1 : frac > redAt ? 2 : 3;

    // Public so CSVM.Tests (GaugeArrowTweenTests) can assert the sweep math directly.
    public static float TargetArrowAngle(int positions, int selected) =>
        positions > 0 ? -(360f / positions) * selected : 0f;

    /// <summary>Advances a weapon-gauge arrow angle at most <see cref="ArrowSweepDegPerSimS"/> ×
    /// simDt toward target, routed the shortest way round. NaN snaps instead of sweeping
    /// in from an undefined pose (gauge just appeared, or a respawn cleared it via Reset). Public
    /// so CSVM.Tests (GaugeArrowTweenTests) can assert the sweep directly.
    /// ⚠ Only the arrow sweeps — the readout digits/type name still snap on the sweep's first
    /// frame. Do not tween those too.</summary>
    public static float TweenArrow(float current, float target, float simDt)
    {
        if (float.IsNaN(current))
            return target;
        float delta = Mathf.PosMod(target - current + 180f, 360f) - 180f;
        float maxStep = ArrowSweepDegPerSimS * simDt;
        return Mathf.Abs(delta) <= maxStep ? target : current + Mathf.Sign(delta) * maxStep;
    }

    /// <summary>Half of the STALL lamp's blink period, in SIM seconds, at an airspeed of
    /// <paramref name="stallFrac"/> × fd_speed — proportional to speed, held flat below the deepest
    /// speed the capture reached. Duty is 0.50, so the full period is twice this. Public so
    /// CSVM.Tests (<c>StallWarningTests</c>) can assert the law against the capture's two measured
    /// anchors directly.</summary>
    public static float StallBlinkHalfPeriodS(float stallFrac) =>
        StallBlinkHalfPeriodPerFrac * Mathf.Max(stallFrac, StallBlinkFracFloor);

    /// <summary>Restarts the warning/blink state (respawn).</summary>
    public void Reset()
    {
        foreach (var z in _zones)
            z.BlinkLeft = 0f;
        _gunArrow.Reset();
        _missileArrow.Reset();
        _stallLamp.Set();
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
        // ⚠ Both animated cues advance on SIM dt, never the raw frame delta: their rates are
        // video-decoded in sim seconds, and the wall figures would run them 39% fast.
        float simDt = GameClock.Current?.FrameDt ?? (float)delta;
        if (GunGauge is { } gg)
            _gunArrow.Advance(TargetArrowAngle(_gunGaugeGeom.Positions, gg.Selected), simDt);
        if (MissileGauge is { } mg)
            _missileArrow.Advance(TargetArrowAngle(_missileGaugeGeom.Positions, mg.Selected), simDt);
        _stallLamp.Advance(StallWarning, StallFrac, SpeedMph, simDt);
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
        if (_stallLamp.Lit)
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
            DrawWeaponGauge(_missileGaugeGeom, mg, c, MissileRadius * s, isGun: false, _missileArrow.Angle);
        }
        if (GunGauge is { } gg && _gunGaugeGeom.HasGeometry)
        {
            var c = new Vector2(vp.X - GunCenterFromRight * s, FromBottom(GunCenterY, s, vp.Y));
            DrawWeaponGauge(_gunGaugeGeom, gg, c, GunRadius * s, isGun: true, _gunArrow.Angle);
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

    // All flat polys of a node's mesh in dial-local coords. Node Local
    // transforms are ignored on purpose: needles carry an arbitrary modeled rest
    // rotation (we set the angle from the value), everything else is identity.
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

    // The trailing integer of a name like "ggindicator3" (→ 3); 0 if it ends in no digits.
    private static int TrailingInt(string name)
    {
        int i = name.Length;
        while (i > 0 && char.IsDigit(name[i - 1]))
        {
            i--;
        }
        return i < name.Length && int.TryParse(name[i..], out var n) ? n : 0;
    }

    // The dials are measured off HUD.png as absolute 1440p-reference y coordinates, but
    // they are really anchored to the BOTTOM of the screen (they sit 331 / 141 px up from it).
    // Measuring from the bottom is identical to `refY · s` whenever s is the plain
    // height ratio (single player), and is what keeps them on screen when a splitscreen pane
    // draws them at a damped, larger-than-proportional scale (see HudMetrics).
    private static float FromBottom(float refY, float s, float viewportH) =>
        viewportH - (HudMetrics.ReferenceHeight - refY) * s;

    // A count right-aligned into `width` digit cells, space-padded
    // (clamped 0..9999, the 4-cell readout's range).
    private static string FormatCount(int count, int width)
    {
        string s = Mathf.Clamp(count, 0, 9999).ToString();
        if (s.Length > width)
            s = s[^width..];
        return s.PadLeft(width);
    }

    // A weapon name upper-cased, left-aligned and padded/truncated to the type readout's
    // `width` cells (the atlas is uppercase-only).
    private static string FormatType(string type, int width)
    {
        string s = type.ToUpperInvariant();
        if (s.Length > width)
            s = s[..width];
        return s.PadRight(width);
    }

    // An altimeter/speedometer node: face polys from any unnamed child mesh
    // (g784 …), warning overlays from *_on children, needles by exact child name.
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

    // The damage dial: each *damage child is one zone (border bar + hatch fill); everything else
    // is the silhouette face. Thresholds come from the matching part's injure anims.
    // ⚠ Face parenting differs per plane — see docs/formats/hud.md. Any non-zone child counts as
    // face, the same rule ExtractInstrument uses for the other two dials.
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

    // A weapon gauge (gungauge/missilegauge): identical layout on all 11 planes — see
    // docs/formats/hud.md. Named children are the ammo/type readouts, belt lights and pointer;
    // anything else is dial face, the same rule the damage dial uses. Extracts geometry and belt
    // order only; the glyph/colour cycles are driven live by FlightController via WeaponGauge.
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

    // Loads the shared digit/letter atlas (the `zero.tif`…`nine.tif` + `A.tif`…
    // `Z.tif` cycle both readouts index) and the belt-indicator colour variants
    // (green/yellow/red). Space and unrepresented chars stay absent → drawn as a blank cell.
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

    // Draws one weapon gauge: the labelled face, every belt light — guns step
    // green/yellow/red by remaining fraction, hardpoints step green/red with no intermediate
    // colour, and a position this airframe does not fit at all reads red like a spent one — the
    // right-aligned digit count, the left-aligned type name, then the pointer at its animated
    // sweep angle (tweened toward the selected belt slot in _Process; the readout
    // above still snaps).
    private void DrawWeaponGauge(GaugeGeom geom, WeaponGauge state, Vector2 center, float radius, bool isGun,
        float arrowAngle)
    {
        foreach (var p in geom.Face)
            DrawGaugePoly(p, center, radius);
        for (int i = 0; i < geom.Indicators.Count; i++)
        {
            int color = SlotIndicatorColor(state.Slots, i, isGun);
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

    // Draws each fixed glyph quad with the atlas texture for its character; a space or an
    // unrepresented char leaves that cell blank (the atlas has no space glyph — that IS the space).
    private void DrawGlyphs(List<GaugePoly> quads, string text, Vector2 center, float radius)
    {
        for (int i = 0; i < quads.Count && i < text.Length; i++)
        {
            if (_glyphs.TryGetValue(text[i], out var g) && g != null)
                DrawGaugePoly(quads[i], center, radius, 0f, g);
        }
    }

    // Draws one extracted poly at a dial's screen center/radius, rotated
    // clockwise by rotDeg about the dial center (needles). Dial-local y-up flips to
    // screen y-down; an override texture substitutes the zone color variants (with
    // a flat tint standing in when the variant png is missing).
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

    /// <summary>One weapon-gauge arrow's live sweep angle (degrees, same convention as
    /// <see cref="DrawGaugePoly"/>'s rotDeg). <see cref="Angle"/> starts NaN — "not yet drawn" — so
    /// the first <see cref="Advance"/> snaps to target instead of sweeping in from zero. A plain
    /// struct outside GaugeCluster's private fields so CSVM.Tests
    /// (<c>GaugeArrowTweenTests</c>) can drive it without constructing a live Control.</summary>
    public struct ArrowSweep
    {
        public float Angle = float.NaN;

        public ArrowSweep()
        {
        }

        /// <summary>Advances toward <paramref name="target"/> at <see cref="ArrowSweepDegPerSimS"/>,
        /// the shortest way round.</summary>
        public void Advance(float target, float simDt) => Angle = TweenArrow(Angle, target, simDt);

        /// <summary>Clears to NaN so the next <see cref="Advance"/> snaps instead of sweeping in
        /// (gauge just appeared, or a respawn).</summary>
        public void Reset() => Angle = float.NaN;
    }

    /// <summary>The STALL lamp's blink: binary brightness, duty 0.50, integrated on
    /// its own sim clock rather than read off a wall clock — a rate change mid-dwell shortens the
    /// remainder rather than jumping the lamp. A plain struct outside GaugeCluster's private fields
    /// so CSVM.Tests (<c>StallWarningTests</c>) can drive it without
    /// constructing a live Control.</summary>
    public struct StallLamp
    {
        // The fraction of the current half-period elapsed — see the class remark on why this is
        // integrated rather than PosMod'd over accumulated time.
        private double _phase;
        private float _dwellS;   // sim time the current dwell has run, for the toggle log
        private bool _lampOn;
        private bool _warnPrev;  // last-seen warning flag, so the crossing logs once

        public StallLamp() => Set();

        /// <summary>Whether the lit STALL overlay should draw this frame.</summary>
        public bool Lit { get; private set; }

        /// <summary>(Re)arms the lamp lit and clears the blink phase — construction and a respawn
        /// (<see cref="GaugeCluster.Reset"/>) both start here, so a respawn snaps.</summary>
        public void Set()
        {
            _phase = 0.0;
            _dwellS = 0f;
            _lampOn = true;
            _warnPrev = false;
            Lit = false;
        }

        /// <summary>Integrates one sim step. The lamp lights the instant <paramref name="warning"/>
        /// does and its phase restarts when it clears — the original shows no hysteresis at the
        /// threshold (on at 89.9/90.0 mph decelerating, 90.0/89.9 accelerating).
        /// <paramref name="frac"/> is StallFrac (only read while warned); <paramref name="mph"/> is
        /// for the crossing log only.</summary>
        public void Advance(bool warning, float frac, float mph, float simDt)
        {
            if (warning != _warnPrev)
            {
                // The threshold crossing itself, with the fraction it happened at — the other half
                // of what a scripted run needs to check the cue against the capture.
                Log.Debug("flight", $"stall warning {(warning ? "on" : "off")} frac={frac:0.000} mph={mph:0.0}");
                _warnPrev = warning;
            }
            if (!warning)
            {
                _phase = 0.0;
                _dwellS = 0f;
                _lampOn = true;
                Lit = false;
                return;
            }
            _dwellS += simDt;
            _phase += simDt / StallBlinkHalfPeriodS(frac);
            while (_phase >= 1.0)
            {
                _phase -= 1.0;
                _lampOn = !_lampOn;
                // The dwell that just ended, in sim ms — the one number the blink law is measured
                // in, so a run can be checked against the capture without eyes on the lamp. Carries its
                // own times as values: the log has no timestamp column by design.
                Log.Debug("flight", $"stall lamp {(_lampOn ? "lit" : "dark")} dwell_ms={_dwellS * 1000f:0} frac={frac:0.000} half_ms={StallBlinkHalfPeriodS(frac) * 1000f:0}");
                _dwellS = 0f;
            }
            Lit = _lampOn;
        }
    }

    /// <summary>Live state for one weapon gauge (gun or rocket), pushed by the FlightController each
    /// frame. Positions map 1:1 onto the gauge's belt indicators: <see cref="Slots"/>[i] drives
    /// <c>ggindicator</c>/<c>mgindicator</c> i (green &gt; low &gt; empty); an indicator past the end
    /// of <see cref="Slots"/> is a position the airframe does not fit and reads red. The arrow points at
    /// <see cref="Selected"/>, <see cref="Count"/> fills the 4-digit readout and <see cref="Type"/>
    /// the 6-char name. Left null (the default) hides that gauge — the static viewer has no loadout.</summary>
    public sealed class WeaponGauge
    {
        public int Count;                                       // rounds shown in 4char_ammo (0..9999)
        public string Type = "";                                // 6char_type name (weapon NAME, upper-cased)
        public int Selected;                                    // 0-based belt position the arrow points at
        public IReadOnlyList<float> Slots = Array.Empty<float>(); // per-indicator ammo fraction 0..1
    }

    // One flat gauge polygon extracted from the mesh: dial-local points
    // (x right, y up, radius 1), normalized UVs, source texture, draw priority.
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

    // One weapon gauge's extracted geometry (gungauge or missilegauge). All flat, dial-local
    // like the other dials. The digit / type quads are ordered left→right; each indicator's polys sit
    // at its parsed index (ggindicator3 → Indicators[3]); the arrow rotates about the
    // centre to point at a belt position.
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
