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
    /// <summary>Distance fog for one day/night zone: linear haze toward <see cref="FogColor"/>
    /// between <see cref="FogNear"/> and <see cref="FogFar"/> metres of view distance.
    /// <see cref="ClipFar"/> is the original's hard far clip (informational — our far plane is
    /// much larger; the fog is what hides distant terrain, matching the original's short view
    /// distance).</summary>
    public readonly record struct ZoneFog(Color FogColor, float FogNear, float FogFar, float ClipFar);

    private readonly Dictionary<string, ZoneFog> _zones = new(StringComparer.OrdinalIgnoreCase);

    // A no-op fog (nothing fades) for missions/zones without a FOG_RANGES: near/far so far out
    // that smoothstep is 0 across the whole world.
    private static readonly ZoneFog NoFog = new(new Color(0.69f, 0.69f, 0.69f), 1e8f, 1e9f, 1e9f);

    /// <summary>The whiteout cloud band, metres of altitude. <see cref="CloudBottom"/>/<see
    /// cref="CloudTop"/> are where sight is clear; the fully-opaque core is <see
    /// cref="CloudThickness"/> deep and centred on the band's midpoint (see
    /// <see cref="WhiteoutAmount"/>).</summary>
    public float CloudTop { get; private set; }
    public float CloudBottom { get; private set; }
    public float CloudThickness { get; private set; }
    public bool HasCloudBand => CloudTop > CloudBottom;

    /// <summary>Steady wind (m/s) plus the random-gust bounds — the drift source for the
    /// future ambient cloud puffs (parsed now so the loader is complete; unused this milestone).</summary>
    public Vector3 WindStatic { get; private set; }
    public float WindRandomMaxSpeed { get; private set; }
    public float WindRandomAccel { get; private set; }

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
                var color = z.List("FOG_COLOR") is { Count: >= 3 } fc
                    && fc[0] is float r && fc[1] is float g && fc[2] is float b
                        ? new Color(r, g, b)
                        : NoFog.FogColor;
                (float near, float far) = z.List("FOG_RANGES") is { Count: >= 2 } fr
                    && fr[0] is float n && fr[1] is float f
                        ? (n, f)
                        : (NoFog.FogNear, NoFog.FogFar);
                float clip = z.List("CLIP_RANGES") is { Count: >= 2 } cr && cr[1] is float c ? c : NoFog.ClipFar;
                w._zones[zone] = new ZoneFog(color, near, far, clip);
            }
        return w;
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
}
