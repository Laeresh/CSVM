using System;
using System.Collections.Generic;
using System.IO;
using CrimsonSkies.Mech3;
using Godot;

namespace CrimsonSkies.Flight;

/// <summary>
/// Per-mission atmosphere from the mission's own <c>weather.json</c> (a zrdr reader, same
/// archive as ia.json/objectives.json — see <see cref="SpawnPoints"/>). Feeds the remake's
/// distance fog, the cloud-band whiteout, and (later) wind-driven ambient cloud puffs.
///
/// <para>weather.json layout (validated on C1/IA1): a single alternating dict with blocks
/// <c>VIEWING_RANGE</c>, <c>WIND</c>, <c>CLOUD_COVER</c>, and per-zone <c>ZONE1</c>/<c>ZONE2</c>
/// (each with an <c>SW_*</c> software-renderer twin we ignore). Fog is per zone (which zone a
/// mission shows isn't in these readers — the remake picks it via <c>--sky-zone</c>, default
/// zone2 = night); the cloud band and wind are global.</para>
///
/// <para>The <c>CLOUD_COVER</c> and <c>WIND</c> blocks pair each key with a bare scalar (e.g.
/// <c>"TOP", 1124</c>), not a list, so <see cref="ZrdrDict.FromAlternating"/> — which expects
/// list values — can't read them; they're walked as raw key/next-element pairs here. The
/// per-zone blocks use list values throughout, so they go through the normal dict.</para>
/// </summary>
public sealed class WeatherState
{
    /// <summary>Distance fog for one day/night zone: haze toward <see cref="FogColor"/> between
    /// <see cref="FogNear"/> and <see cref="FogFar"/> metres of **horizontal** view distance —
    /// the original's fog volume is a vertical cylinder around the camera, not a sphere
    /// (user-diagnosed 2026-07-17) — scaled by an altitude fade from <c>FOG_ALTITUDE</c>: full
    /// fog below <see cref="FogLow"/>, none above <see cref="FogHigh"/>, so the cloud deck /
    /// sky overhead stays clear. C1/IA1 corroborates: zone1's 970→1047 is exactly cloud-band
    /// bottom → whiteout-band centre (fog hands over to the whiteout while climbing into the
    /// overcast); zone2's 4000→5000 sits above the 2500 m flight ceiling (night fog at every
    /// flyable altitude). <see cref="ClipFar"/> is the original's hard far clip (informational —
    /// our far plane is much larger; the fog is what hides distant terrain, matching the
    /// original's short view distance).</summary>
    public readonly record struct ZoneFog(Color FogColor, float FogNear, float FogFar, float FogLow, float FogHigh, float ClipFar);

    private readonly Dictionary<string, ZoneFog> _zones = new(StringComparer.OrdinalIgnoreCase);

    // A no-op fog (nothing fades) for missions/zones without a FOG_RANGES: near/far so far out
    // that smoothstep is 0 across the whole world.
    private static readonly ZoneFog NoFog = new(new Color(0.69f, 0.69f, 0.69f), 1e8f, 1e9f,1e8f,1e9f, 1e9f);

    /// <summary>The whiteout cloud band, metres of altitude. <see cref="CloudBottom"/>/<see
    /// cref="CloudTop"/> are where sight is clear; the fully-opaque core is <see
    /// cref="CloudThickness"/> deep and centred on the band's midpoint (see
    /// <see cref="WhiteoutAmount"/>).</summary>
    public float CloudTop { get; private set; }
    public float CloudBottom { get; private set; }
    public float CloudThickness { get; private set; }
    public bool HasCloudBand => CloudTop > CloudBottom;

    /// <summary>The cloud deck's face tints from CLOUD_COVER's <c>TOP_COLOR</c>/<c>BOTTOM_COLOR</c>,
    /// when the mission carries them (integer-RGB in the data — normalized by
    /// <see cref="ParseColor"/>). Null when absent (C1/IA1 has neither). Decoded now for the
    /// night-brightness deck-tint calibration; unused this milestone.</summary>
    public Color? CloudTopColor { get; private set; }
    public Color? CloudBottomColor { get; private set; }

    /// <summary>Steady wind (m/s) plus the random-gust bounds — the drift source for the
    /// future ambient cloud puffs (parsed now so the loader is complete; unused this milestone).</summary>
    public Vector3 WindStatic { get; private set; }
    public float WindRandomMaxSpeed { get; private set; }
    public float WindRandomAccel { get; private set; }

    public enum PrecipKind { Rain, Snow }

    /// <summary>The mission's precipitation, from the bare-scalar block at the end of
    /// weather.json (after <c>SHADOW_ANGLES</c>). Only some missions carry one — C4 (SNOW),
    /// C1C/C2B (RAIN); C1/C5 IA1 have none. Consumed by <see cref="Effects.Precipitation"/>.
    /// <list type="bullet">
    /// <item><see cref="Color"/> — the particle tint (integer-RGB in the data, e.g.
    /// <c>[128,128,128]</c> → mid-gray; normalized by <see cref="ParseColor"/>).</item>
    /// <item><see cref="WindDir"/> (degrees) / <see cref="WindVel"/> — the precipitation's own
    /// horizontal drift (separate from the cloud <c>WIND</c> block above).</item>
    /// <item><see cref="Gravity"/> — the fall-rate multiplier in the data's own units (SNOW 1,
    /// RAIN 3 — rain falls faster); mapped to a metres/second fall speed by the renderer's TUNE
    /// scale.</item>
    /// <item><see cref="Particles"/> — a density hint (RAIN carries <c>PARTICLES 100</c>; SNOW
    /// omits it → 0, the renderer substitutes a default).</item>
    /// <item><see cref="AlphaGradient"/> — the <c>[0.5, 0.0]</c> pair; <c>.X</c> is the peak
    /// opacity (the field is quite translucent).</item>
    /// </list></summary>
    public readonly record struct PrecipData(
        PrecipKind Kind, Color Color, float WindDir, float WindVel,
        float Gravity, int Particles, Vector2 AlphaGradient);

    /// <summary>The mission's precipitation, or null when the weather.json has no TYPE block.</summary>
    public PrecipData? Precip { get; private set; }

    /// <summary>Fog for a zone ("zone1"/"zone2"); a no-op fog if the zone is absent.</summary>
    public ZoneFog Fog(string zone) => _zones.TryGetValue(zone, out var z) ? z : NoFog;

    /// <summary>Whiteout opacity 0..1 at a given altitude: a symmetric trapezoid across the
    /// cloud band (user-observed in-game 2026-07-16). Clear sight (0) at BOTTOM and TOP, ramping
    /// linearly to a fully-opaque core (1 — the plane is no longer visible) that is THICKNESS
    /// deep and centred on the band's midpoint. THICKNESS is the depth of that opaque core, not
    /// an edge transition — so C1/IA1 (970–1124, ±30) is clear at 970/1124 and total in
    /// 1032–1062, with linear ramps between.</summary>
    public float WhiteoutAmount(float altitude)
    {
        if (!HasCloudBand || altitude <= CloudBottom || altitude >= CloudTop)
            return 0f;
        float mid = (CloudTop + CloudBottom) * 0.5f;
        float coreHalf = CloudThickness * 0.5f;       // half-depth of the opaque core, centred on mid
        float dist = MathF.Abs(altitude - mid);
        if (dist <= coreHalf)
            return 1f;
        float ramp = (CloudTop - CloudBottom) * 0.5f - coreHalf; // core edge → band edge
        return ramp > 1e-3f ? Mathf.Clamp(1f - (dist - coreHalf) / ramp, 0f, 1f) : 1f;
    }

    /// <summary>Loads the flown mission's weather.json (a zrdr zip or unpacked dir). Null if
    /// the file is absent (some folders are multiplayer-only, etc.).</summary>
    public static WeatherState? Load(string missionZrdrPath)
    {
        List<object?> root;
        try
        {
            root = Zrdr.LoadFile(missionZrdrPath, "weather.json");
        }
        catch (IOException)
        {
            return null;
        }
        if (root.Count == 0 || root[0] is not List<object?> inner)
            return null;
        var dict = ZrdrDict.FromAlternating(inner);

        var w = new WeatherState();
        if (dict.List("CLOUD_COVER") is { } cc)
        {
            w.CloudTop = ScalarAfter(cc, "TOP");
            w.CloudBottom = ScalarAfter(cc, "BOTTOM");
            w.CloudThickness = ScalarAfter(cc, "THICKNESS");
            w.CloudTopColor = ParseColor(ListAfter(cc, "TOP_COLOR"));
            w.CloudBottomColor = ParseColor(ListAfter(cc, "BOTTOM_COLOR"));
        }
        if (dict.List("WIND") is { } wind)
        {
            w.WindStatic = Vec3After(wind, "STATIC_VELOCITY");
            w.WindRandomMaxSpeed = ScalarAfter(wind, "RANDOM_MAX_SPEED");
            w.WindRandomAccel = ScalarAfter(wind, "RANDOM_ACCEL");
        }
        foreach (var zone in new[] { "ZONE1", "ZONE2" })
            if (dict.Dict(zone) is { } z)
            {
                var color = ParseColor(z.List("FOG_COLOR")) ?? NoFog.FogColor;
                (float near, float far) = z.List("FOG_RANGES") is { Count: >= 2 } fr
                    && fr[0] is float n && fr[1] is float f
                        ? (n, f)
                        : (NoFog.FogNear, NoFog.FogFar);
                (float low, float high) = z.List("FOG_ALTITUDE") is { Count: >= 2 } fa
                    && fa[0] is float l && fa[1] is float h
                        ? (l, h)
                        : (NoFog.FogNear, NoFog.FogFar);
                float clip = z.List("CLIP_RANGES") is { Count: >= 2 } cr && cr[1] is float c ? c : NoFog.ClipFar;
                w._zones[zone] = new ZoneFog(color, near, far,low, high, clip);
            }
        w.ParsePrecip(inner);
        return w;
    }

    // The precipitation block (TYPE SNOW|RAIN, COLOR, WIND_DIR, WIND_VEL, GRAVITY, [PARTICLES],
    // ALPHA_GRADIENT) sits at the END of the root dict, after SHADOW_ANGLES, as bare-scalar
    // top-level siblings — so it's walked raw from `inner` (like CLOUD_COVER/WIND), not via the
    // dict (which drops a key's value when it's a bare scalar rather than a list). The keys are
    // unique at inner's top level (FOG_COLOR/SUNLIGHT_* live inside the nested zone sub-lists,
    // which the flat walkers never descend into), so each first-match is the right one.
    private void ParsePrecip(List<object?> inner)
    {
        string? type = StringAfter(inner, "TYPE");
        PrecipKind kind;
        if (string.Equals(type, "RAIN", StringComparison.OrdinalIgnoreCase)) kind = PrecipKind.Rain;
        else if (string.Equals(type, "SNOW", StringComparison.OrdinalIgnoreCase)) kind = PrecipKind.Snow;
        else return; // no TYPE (most missions) or an unknown type → no precipitation
        Precip = new PrecipData(
            kind,
            ParseColor(ListAfter(inner, "COLOR")) ?? new Color(0.5f, 0.5f, 0.5f),
            WindDir: ScalarAfter(inner, "WIND_DIR"),
            WindVel: ScalarAfter(inner, "WIND_VEL"),
            Gravity: ScalarAfter(inner, "GRAVITY"),
            Particles: (int)ScalarAfter(inner, "PARTICLES"), // absent (SNOW) → 0
            AlphaGradient: Vec2After(inner, "ALPHA_GRADIENT"));
    }

    // key → the immediately following scalar in a flat alternating list
    // (CLOUD_COVER: "TOP", 1124, "BOTTOM", 970, …).
    private static float ScalarAfter(List<object?> list, string key)
    {
        for (int i = 0; i + 1 < list.Count; i++)
            if (list[i] is string s && s.Equals(key, StringComparison.OrdinalIgnoreCase)
                && list[i + 1] is float f)
                return f;
        return 0f;
    }

    // key → the immediately following string (precipitation: "TYPE", "SNOW", …). Null if absent.
    private static string? StringAfter(List<object?> list, string key)
    {
        for (int i = 0; i + 1 < list.Count; i++)
            if (list[i] is string s && s.Equals(key, StringComparison.OrdinalIgnoreCase)
                && list[i + 1] is string v)
                return v;
        return null;
    }

    // key → the immediately following 2-vector (precipitation: "ALPHA_GRADIENT", [0.5, 0.0]).
    private static Vector2 Vec2After(List<object?> list, string key)
    {
        for (int i = 0; i + 1 < list.Count; i++)
            if (list[i] is string s && s.Equals(key, StringComparison.OrdinalIgnoreCase)
                && list[i + 1] is List<object?> v && v.Count >= 2
                && v[0] is float x && v[1] is float y)
                return new Vector2(x, y);
        return Vector2.Zero;
    }

    // key → the immediately following 3-vector (WIND: "STATIC_VELOCITY", [0, 2, 0], …).
    private static Vector3 Vec3After(List<object?> list, string key)
    {
        for (int i = 0; i + 1 < list.Count; i++)
            if (list[i] is string s && s.Equals(key, StringComparison.OrdinalIgnoreCase)
                && list[i + 1] is List<object?> v && v.Count >= 3
                && v[0] is float x && v[1] is float y && v[2] is float z)
                return new Vector3(x, y, z);
        return Vector3.Zero;
    }

    // key → the immediately following list value in a flat alternating block
    // (CLOUD_COVER: "TOP_COLOR", [192, 192, 192], …). Null if the key is absent.
    private static List<object?>? ListAfter(List<object?> list, string key)
    {
        for (int i = 0; i + 1 < list.Count; i++)
            if (list[i] is string s && s.Equals(key, StringComparison.OrdinalIgnoreCase)
                && list[i + 1] is List<object?> v)
                return v;
        return null;
    }

    // A weather.json colour triple, normalized to 0..1. Two encodings coexist: normalized
    // floats (C1 FOG_COLOR 0.69) and integer RGB (C4 FOG_COLOR [192,192,192]; every
    // TOP_COLOR/BOTTOM_COLOR). Any component strictly > 1 means the whole triple is 0–255 and
    // is divided by 255 — verified unambiguous across all weather.json (the only 1.0-bearing
    // colour is a float sky-fog [0.80,0.84,1.0], whose max is exactly 1, so it stays a float).
    // These are DX7 sRGB framebuffer values; PlaneViewer converts them to linear for the shader.
    private static Color? ParseColor(List<object?>? list)
    {
        if (list is not { Count: >= 3 } || list[0] is not float r || list[1] is not float g || list[2] is not float b)
            return null;
        float scale = r > 1f || g > 1f || b > 1f ? 1f / 255f : 1f;
        return new Color(r * scale, g * scale, b * scale);
    }
}
