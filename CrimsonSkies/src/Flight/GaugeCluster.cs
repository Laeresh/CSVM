using System;
using System.Collections.Generic;
using CrimsonSkies.Mech3;
using Godot;

namespace CrimsonSkies.Flight;

/// <summary>
/// The original's cockpit gauges as a screen-space HUD (user request 2026-07-19):
/// altimeter (two needles + blinking LOW ALT), speedometer (needle + blinking
/// STALL) and the per-plane damage display (part fills + border bars in
/// green/yellow/red, blinking for a few seconds after a hit).
///
/// Everything is rebuilt from the game's own data. Each player plane carries a
/// 'gauges' subtree under its (otherwise skipped) cockpit in planes.zbd whose
/// meshes ARE the 2D dials — flat polygons in dial-local coords (x right, y up,
/// bezel radius 1): the face is a 12-gon mapping the whole dial texture
/// (altimeter/speedometer/&lt;plane&gt;_damage), each needle is a single textured quad
/// (needle.tif, pivot at the origin, tip +y — the taper and hub are painted in
/// the texture, no extra geometry), the lowalt_on/stallwarning_on overlays are
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
    public bool Stalled;
    /// <summary>Part HP source for the damage display: name → fraction (1 = pristine).
    /// Flight binds PlaneDamage, the damage lab binds its sliders. Null = all green.</summary>
    public Func<string, float>? PartFraction;

    // ---- tuning ----
    private const float LowAltAglM = 50f;     // LOW ALT below this height over ground (user spec)
    private const float WarnBlinkPeriod = 0.4f;  // s per on/off cycle of LOW ALT / STALL (TUNE)
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
    private static readonly Vector2 AltCenter = new(425.5f, 1108.5f);
    private const float AltRadius = 85f;
    private const float SpdCenterFromRight = 420f, SpdCenterY = 1108.5f, SpdRadius = 85f;
    private static readonly Vector2 DmgCenter = new(426.5f, 1299f);
    private const float DmgRadius = 85f;

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

    private readonly List<GaugePoly> _altFace = new();
    private readonly List<GaugePoly> _altWarn = new();  // lowalt_on (blinks)
    private GaugePoly? _altHundreds, _altThousands;
    private readonly List<GaugePoly> _spdFace = new();
    private readonly List<GaugePoly> _spdWarn = new();  // stallwarning_on (blinks)
    private GaugePoly? _spdNeedle;
    private readonly List<GaugePoly> _dmgFace = new();
    private readonly List<DamageZone> _zones = new();
    // color-variant textures for the zone swap (0 green / 1 yellow / 2 orange / 3 red)
    private readonly Texture2D?[] _hilite = new Texture2D?[4];
    private readonly Texture2D?[] _hatch = new Texture2D?[4];

    private double _time;

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
            }
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
    }

    /// <summary>A part took damage: its fill + border blink for the next few seconds
    /// (the original blinks even inside the green range — user-verified).</summary>
    public void OnPartDamage(string partName)
    {
        foreach (var z in _zones)
            if (z.Part.Equals(partName, StringComparison.OrdinalIgnoreCase))
                z.BlinkLeft = DamageBlinkTime;
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
                Tex = texName.Length > 0 ? FindGaugeTexture(textures, texName) : null,
                Priority = poly.Priority,
                TexName = texName,
            });
        }
        return result;
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

    /// <summary>The damage dial: its own mesh is the silhouette face; each *damage
    /// child is one zone — border bar ("hilite" texture) + hatch fill. Color
    /// thresholds come from the matching destroyable part's injure anims.</summary>
    private void ExtractDamageDial(GameZ gz, TextureArchive textures, GameZNode dial,
        IReadOnlyList<DestroyablePart> parts)
    {
        _dmgFace.AddRange(MeshPolys(gz, textures, dial));
        _dmgFace.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        foreach (int ci in dial.Children)
        {
            var child = gz.Nodes[ci];
            if (!child.Name.EndsWith("damage", StringComparison.OrdinalIgnoreCase))
                continue;
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

        // the four swap variants the cockpit.gw texture cycle names
        string[] hilite = { "greenhilite", "yellowhilite", "orangehilite", "redhilite" };
        string[] hatch = { "grn_hatchptrn", "yel_hatchptrn", "orng_hatchptrn", "red_hatchptrn" };
        for (int i = 0; i < 4; i++)
        {
            _hilite[i] = textures.Find(hilite[i]);
            _hatch[i] = textures.Find(hatch[i]);
        }
    }

    // The needle texture's shaft is a flat full-width slab (32×128, no alpha; the hub
    // box with its two black discs fills the tail rows), but the original renders a
    // slim pointer that tapers to a point at the tip (reference: the HUD screenshot
    // zooms). That shape is made engine-side — it is in neither the texture colors
    // nor the mesh/UVs — so the remake shapes it at load: shaft texels get an alpha
    // mask tapering linearly from the widest point near the hub to a point at the
    // tip; the hub rows stay fully opaque (an earlier black color-key erased the hub
    // discs — never key this texture). Profile constants are TUNE (eyeballed against
    // the user's zoomed original altimeter).
    private const float NeedleHubStartFrac = 76f / 128f; // shaft rows above, hub box below
    private const float NeedleMaxHalfFrac = 0.65f;       // widest half-width / texture half-width
    private const float NeedleTaperEndFrac = 0.9f;       // taper spans this much of the shaft

    private static readonly Dictionary<string, Texture2D?> ShapedNeedles = new(StringComparer.OrdinalIgnoreCase);

    private static Texture2D? FindGaugeTexture(TextureArchive textures, string texName)
    {
        var baseName = System.IO.Path.GetFileNameWithoutExtension(texName);
        bool isNeedle = baseName.Equals("needle", StringComparison.OrdinalIgnoreCase);
        if (isNeedle && ShapedNeedles.TryGetValue(baseName, out var cached))
            return cached;
        var tex = textures.Find(texName);
        if (!isNeedle || tex == null)
            return tex;

        var img = tex.GetImage();
        img.Convert(Image.Format.Rgba8);
        int w = img.GetWidth(), h = img.GetHeight();
        int hubStart = (int)(h * NeedleHubStartFrac);
        float cx = (w - 1) / 2f;
        float maxHalf = w / 2f * NeedleMaxHalfFrac;
        for (int y = 0; y < hubStart; y++)
        {
            float hw = maxHalf * Mathf.Min(1f, y / (hubStart * NeedleTaperEndFrac));
            var slab = img.GetPixel(w / 4, y); // a mid-slab texel left of the notch
            for (int x = 0; x < w; x++)
            {
                var c = img.GetPixel(x, y);
                // the texture's darker center notch would survive the taper as a
                // split "tweezer" tip — the original tip is solid, so fill the notch
                // with the shaft color (invisible at game scale anyway)
                if (Mathf.Abs(x - cx) <= 3f && c.R < slab.R - 0.05f)
                    c = slab;
                float a = Mathf.Clamp(hw - Mathf.Abs(x - cx) + 0.5f, 0f, 1f);
                img.SetPixel(x, y, new Color(c.R, c.G, c.B, c.A * a));
            }
        }
        img.GenerateMipmaps();
        var shaped = ImageTexture.CreateFromImage(img);
        ShapedNeedles[baseName] = shaped;
        return shaped;
    }

    // ---- drawing ----

    public override void _Process(double delta)
    {
        _time += delta;
        foreach (var z in _zones)
            z.BlinkLeft = Mathf.Max(0f, z.BlinkLeft - (float)delta);
        QueueRedraw();
    }

    private bool WarnPhaseOn => Mathf.PosMod((float)_time, WarnBlinkPeriod) < WarnBlinkPeriod * 0.5f;
    private bool DamagePhaseOn => Mathf.PosMod((float)_time, DamageBlinkPeriod) < DamageBlinkPeriod * 0.5f;

    /// <summary>The dials are measured off HUD.png as absolute 1440p-reference y coordinates, but
    /// they are really anchored to the BOTTOM of the screen (they sit 331 / 141 px up from it).
    /// Measuring from the bottom is identical to <c>refY · s</c> whenever s is the plain
    /// height ratio (single player), and is what keeps them on screen when a splitscreen pane
    /// draws them at a damped, larger-than-proportional scale (see <see cref="HudMetrics"/>).</summary>
    private static float FromBottom(float refY, float s, float viewportH) =>
        viewportH - (HudMetrics.ReferenceHeight - refY) * s;

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
        if (Stalled && WarnPhaseOn)
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
            int color = frac > z.YellowAt ? 0 : frac > z.OrangeAt ? 1 : frac > z.RedAt ? 2 : 3;
            foreach (var p in z.Border)
                DrawGaugePoly(p, dmgC, dmgR, 0f, _hilite[color], ZoneFlat[color]);
            foreach (var p in z.Fill)
                DrawGaugePoly(p, dmgC, dmgR, 0f, _hatch[color], ZoneFlat[color]);
        }
    }

    // flat zone tints when a color-variant png is missing from the archive
    private static readonly Color[] ZoneFlat =
    {
        new(0.25f, 0.9f, 0.2f), new(0.95f, 0.9f, 0.1f),
        new(0.95f, 0.55f, 0.05f), new(0.9f, 0.1f, 0.1f),
    };

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
}
