using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// Per-mission atmosphere from the mission's own <c>weather.json</c> (a zrdr reader, same
/// archive as ia.json/objectives.json — see <see cref="SpawnPoints"/>). Feeds the remake's
/// distance fog, the cloud-band whiteout, and the precipitation field.
///
/// <para>weather.json layout (validated on C1/IA1): a single alternating dict with blocks
/// <c>VIEWING_RANGE</c>, <c>WIND</c>, <c>CLOUD_COVER</c>, and per-zone <c>ZONE<i>n</i></c>
/// (each with an <c>SW_*</c> software-renderer twin we ignore). Fog is per zone (which zone a
/// mission shows isn't in these readers — the remake picks it via <c>--sky-zone</c>, default
/// zone2 = night); the cloud band and wind are global.</para>
///
/// <para><see cref="CameraWeatherState"/> is the binary's per-frame camera zone 1/2/3
/// (<c>PLAN-weather-decompile-match</c> A2, <c>FUN_0042ee40</c>), computed from a camera position
/// against this mission's <c>CLOUD_COVER</c> band plus the chapter's fog volumes; published each
/// frame by <c>WeatherRig.Tick</c> but consumed by nothing yet.</para>
///
/// <para><b>The zone names are per chapter, not a fixed pair.</b> C1–C4 ship
/// <c>ZONE1</c>+<c>ZONE2</c>, but all 8 C5 missions ship <c>ZONE1</c>+<b><c>ZONE3</c></b> — so
/// the zone table is read from whatever <c>ZONE*</c> keys the file carries, and
/// <see cref="ResolveZone"/> falls the default back to the file's first zone where the
/// requested one is absent. Without that fallback, C5's `zone2` lookup misses and every C5 flight
/// silently renders with <see cref="NoFog"/>: no fog and <c>WorldLight</c> 1 = fullbright.</para>
///
/// <para>The <c>CLOUD_COVER</c> and <c>WIND</c> blocks pair each key with a bare scalar (e.g.
/// <c>"TOP", 1124</c>), not a list, so <see cref="ZrdrDict.FromAlternating"/> — which expects
/// list values — can't read them; they're walked as raw key/next-element pairs here. The
/// per-zone blocks use list values throughout, so they go through the normal dict.</para>
/// </summary>
public sealed class WeatherState
{
    // A no-op fog (nothing fades) for missions/zones without a FOG_RANGES: near/far so far out
    // that smoothstep is 0 across the whole world. WorldLight 1 = fullbright (no darkening).
    private const float SunIncidence = 0.46f;
    private const float MinWorldLight = 0.15f;

    // The original lights the baked-vertex world by the mission's SUNLIGHT (weather.json's
    // per-zone SUNLIGHT_AMBIENT + SUNLIGHT_DIFFUSE·(N·L_sun)); we render the world fullbright,
    // so we're missing it. Averaged over the predominantly up-facing world (ground + cloud
    // deck) that directional term collapses to a single per-mission brightness scalar
    // AMBIENT + DIFFUSE·<incidence>. SunIncidence is that average up-facing sun incidence — one
    // TUNE calibrated to the C1/IA1 reference (A=0.25,D=1.2 → 0.80, matching the original's
    // deck 210→169 and terrain →~57). It then self-scales the scene from the data: C1B night
    // (0.15,0.6)→0.42, C1C day (0.6,2.0)→clamp 1.0. MinWorldLight floors it off pure black.
    // (This is the data-driven half; the gamma-space modulate is the other half.)
    // Confirmed against original footage at 0.426/0.784/clamp-1.0 (CAP-11 matched-pose A/B,
    // 2026-08-07; git log --grep=BL-110). Known exemptions in the original, not yet ours:
    // water is unmodulated (BL-304); night cloud sprites are moonlit directionally (BL-325).
    private static readonly ZoneFog NoFog = new(new Color(0.69f, 0.69f, 0.69f), 1e8f, 1e9f, 1e8f, 1e9f, 1e9f, 1f);

    private readonly Dictionary<string, ZoneFog> _zones = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _zoneNames = new(); // file order — ResolveZone's fallback order

    public enum PrecipKind { Rain, Snow }

    /// <summary>The whiteout cloud band, metres of altitude. <see cref="CloudBottom"/>/<see
    /// cref="CloudTop"/> are where sight is clear; the fully-opaque core is <see
    /// cref="CloudThickness"/> deep and centred on the band's midpoint (see
    /// <see cref="WhiteoutAmount"/>).</summary>
    public float CloudTop { get; private set; }

    public float CloudBottom { get; private set; }

    public float CloudThickness { get; private set; }

    public bool HasCloudBand => CloudTop > CloudBottom;

    /// <summary>The cloud band's midpoint: the altitude the fully-opaque core is centred on
    /// (<see cref="WhiteoutAmount"/>) and the altitude the deck's ceiling/floor regime flips at
    /// (<c>WeatherRig.Tick</c>, A7). One spelling for both, because the flip is unobservable
    /// only for as long as it stays inside that core. Meaningless without
    /// <see cref="HasCloudBand"/>.</summary>
    public float CloudBandCentre => (CloudTop + CloudBottom) * 0.5f;

    /// <summary>The whiteout core's BOTTOM edge — <c>CloudBandCentre - CloudThickness/2</c>,
    /// precomputed by the binary at world init (<c>FUN_004735b0</c> into <c>0071c2d0</c>) and
    /// checked every frame by <c>FUN_0042ee40</c>: camera altitude at/above this is camera state
    /// 2. Deliberately a THIRD spelling, distinct from <see cref="CloudBandCentre"/> (the
    /// deck-regime ceiling/floor flip, <c>WeatherRig.DeckRegime</c>, A7) and from
    /// <see cref="CloudBottom"/> (the visual band's own floor) — the three sit within 80 m of each
    /// other in C1 but the binary computes state-2 and the deck flip as two separate thresholds
    /// (Decision 1, <c>PLAN-weather-decompile-match</c>). Meaningless without
    /// <see cref="HasCloudBand"/> (see <see cref="CameraWeatherState"/>, which guards it).</summary>
    public float CloudCoreBottom => CloudBandCentre - (CloudThickness * 0.5f);

    /// <summary>The cloud band's own colours from CLOUD_COVER's <c>TOP_COLOR</c>/<c>BOTTOM_COLOR</c>,
    /// when the mission carries them (integer-RGB in the data — normalized by
    /// <see cref="ParseColor"/>). Null when absent (C1/IA1 has neither). Consumed by
    /// <see cref="WhiteoutColor"/>, which documents what they are and are not.</summary>
    public Color? CloudTopColor { get; private set; }

    public Color? CloudBottomColor { get; private set; }

    /// <summary>The <c>WIND</c> block: a steady base velocity (m/s) plus the three parameters of
    /// the horizontal random-walk gust laid over it. Consumed by <c>WeatherRig</c>, which builds
    /// the mission's <see cref="CSVM.Effects.WorldWind"/> from these four and publishes the
    /// resulting per-frame vector to every puffer through <c>EffectAmbience</c>
    /// (<c>PLAN-puffer-engine-deltas</c> B6).
    ///
    /// <para>⚠ It still does not move the cloud clutter — that is the authored <c>fogvol.zrd</c>
    /// geometry (docs/formats/fogvol.md), static world geometry, and no reader says wind touches
    /// it. Wind's one decoded consumer is the puffer particle system.</para>
    ///
    /// <para>Every one of the install's 53 <c>weather.zrd.json</c> files authors all four keys
    /// with identical values: <c>STATIC_VELOCITY (0, 2, 0)</c>, <c>RANDOM_MAX_SPEED 10</c>,
    /// <c>RANDOM_ACCEL 5</c>, <c>RANDOM_ANG_VEL 5</c>.</para></summary>
    public Vector3 WindStatic { get; private set; }

    public float WindRandomMaxSpeed { get; private set; }

    public float WindRandomAccel { get; private set; }

    /// <summary>The gust's turn rate in DEGREES per second, exactly as the key spells it — the
    /// binary converts on the way into its global (<c>FUN_004bc680</c>:
    /// <c>FUN_005506d0(value * 0.017453292)</c>), and so does
    /// <see cref="CSVM.Effects.WorldWind"/>, which is where the conversion belongs.</summary>
    public float WindRandomAngVel { get; private set; }

    /// <summary>The mission's precipitation, or null when the weather.json has no TYPE block.</summary>
    public PrecipData? Precip { get; private set; }

    /// <summary>The zones this mission's weather.json actually defines, lowercased and in file
    /// order ("zone1", "zone2" — or "zone1", "zone3" in C5). The <c>SW_*</c> software-renderer
    /// twins are excluded. Empty only if the file carries no <c>ZONE*</c> block at all.</summary>
    public IReadOnlyList<string> ZoneNames => _zoneNames;

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
            w.WindRandomAngVel = ScalarAfter(wind, "RANDOM_ANG_VEL");
        }
        foreach (var zone in ZoneKeys(inner))
            if (dict.Dict(zone) is { } z)
            {
                w._zoneNames.Add(zone.ToLowerInvariant());
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
                w._zones[zone] = new ZoneFog(color, near, far, low, high, clip, WorldLightFactor(z));
                var zf = w._zones[zone];
                // The fields, not the record: a composite ToString() renders its own floats in
                // the current culture, so it would escape Log's invariant formatting outright.
                Log.Info("world", $"weather zone zone={zone} fog_color={zf.FogColor.ToHtml(false)} fog_near={zf.FogNear:0.###} fog_far={zf.FogFar:0.###} fog_low={zf.FogLow:0.###} fog_high={zf.FogHigh:0.###} clip_far={zf.ClipFar:0.###} world_light={zf.WorldLight:0.###}");
            }
        w.ParsePrecip(inner);
        return w;
    }

    /// <summary>Swaps a zone whose horizon subtree is empty for the one zone that has geometry —
    /// the geometry half of <see cref="ResolveZone(string, IReadOnlyList{HorizonZone})"/>, public
    /// on its own only so a mission with no weather.json still gets the correction.
    ///
    /// <para>Deliberately narrow: it fires only when the request <b>is</b> one of the horizon's
    /// zones <b>and</b> builds nothing <b>and</b> exactly one sibling does build something. Every
    /// other shape keeps the request:</para>
    /// <list type="bullet">
    /// <item>the request draws a dome (C1, C1C, C2B, C4 — untouched);</item>
    /// <item>the request is not a zone this horizon has at all (C5, which ships zone3/zone1) —
    /// that is <see cref="ResolveZone(string)"/>'s fallback to the weather file's first zone, and
    /// it must stay there: the two orders disagree (C5's weather.json lists ZONE1 first, its
    /// horizon lists zone3 first), so picking the horizon's first zone would render one zone's sky
    /// under another's fog;</item>
    /// <item>no zone or more than one zone builds geometry — ambiguous, so the request stands and
    /// the choice remains the open fidelity question (<c>BL-100</c>, C1 and C4).</item>
    /// </list></summary>
    public static string PreferPopulatedHorizonZone(string zone, IReadOnlyList<HorizonZone> horizonZones)
    {
        bool requestedIsAZone = false;
        foreach (var z in horizonZones)
            if (z.Name.Equals(zone, StringComparison.OrdinalIgnoreCase))
            {
                if (z.BuildsGeometry)
                    return zone;
                requestedIsAZone = true;
            }
        if (!requestedIsAZone)
            return zone;
        string? only = null;
        foreach (var z in horizonZones)
        {
            if (!z.BuildsGeometry)
                continue;
            if (only != null)
                return zone;   // more than one candidate — not decidable from the geometry
            only = z.Name;
        }
        return only ?? zone;
    }

    /// <summary>The zone to actually render, given the one the caller asked for: the request
    /// itself when this mission defines it, otherwise the first zone the file defines (and the
    /// request unchanged when the file defines none, so the caller still gets <see cref="NoFog"/>).
    ///
    /// <para>This is what makes the <c>zone2</c> default safe on C5, which ships zone1+zone3 and
    /// would otherwise fall through to <see cref="NoFog"/> — no fog and no sunlight model at all.
    /// The default stays <c>zone2</c> (a user decision): which zone a mission actually
    /// flies is in no reader file, so it is settled per chapter by A/B against the original.
    ///
    /// <para><b>C5 = <c>zone1</c>, confirmed by playtest.</b> The fallback already
    /// lands there, so this is not a special case — but it is deliberate rather than accidental, and
    /// it is stable: all 8 C5 missions list <c>ZONE1</c> before <c>ZONE3</c>, so every one of
    /// them resolves to <c>zone1</c>. Do not "fix" the fallback into picking <c>zone3</c>; the
    /// user flew C5/IA1 in the original and can see across the city, which its 50–250 m fog and
    /// 300 m clip would make impossible. C1–C4 all define <c>zone2</c> and resolve to themselves;
    /// their A/B is still open (C1 has conflicting script evidence).
    /// See docs/formats/weather.md.</para></summary>
    public string ResolveZone(string requested) =>
        _zones.ContainsKey(requested) || _zoneNames.Count == 0 ? requested : _zoneNames[0];

    /// <summary>The zone a given camera weather state wears: state <i>n</i> asks for
    /// <c>zone<i>n</i></c> and goes through <see cref="ResolveZone(string)"/>'s file fallback —
    /// the binary's <c>FUN_00472ea0</c>, which indexes <c>ZONE1</c>–<c>ZONE3</c> straight off the
    /// state (<c>PLAN-weather-decompile-match</c> B11).
    ///
    /// <para>The fallback is what keeps this safe on data that does not author the requested
    /// zone: C5 flying into a volume asks for <c>zone3</c> and gets it, but a hypothetical state-2
    /// flip in a mission with no <c>ZONE2</c> lands on the file's first zone instead of
    /// <see cref="NoFog"/> — no fog and fullbright, which is what an unguarded lookup would
    /// render. Deliberately NOT routed through
    /// <see cref="ResolveZone(string, IReadOnlyList{HorizonZone})"/>: the horizon correction owns
    /// the DOME's zone (one per flight, resolved at build), and the fog zone changes underneath it
    /// every time the camera crosses the cloud core — B14 owns reconciling the two.</para></summary>
    public string ZoneForState(int cameraState) =>
        ResolveZone("zone" + cameraState.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>The zone to render when the chapter's own <c>horizon</c> subtree gets a say:
    /// <see cref="ResolveZone(string)"/> first, then <see cref="PreferPopulatedHorizonZone"/>,
    /// and the correction is taken only if <b>this</b> mission also defines fog for it — so the
    /// sky and the fog stay the same zone, which is the whole invariant the pair has
    /// (docs/formats/weather.md).
    ///
    /// <para>This is what fixes C1B, C2 and C3, whose <c>horizon/zone2</c> is a bare marker: the
    /// <c>zone2</c> request resolved to itself (they <i>do</i> define <c>ZONE2</c> fog), so the
    /// dome built with zero meshes and the fog came from the wrong zone — C3's night-blue on a
    /// mission its own data lights at diffuse 1.5, C1B's 1128–1256 m band under a 10000–11000 m
    /// one. Nothing on disk names the zone a mission flies (searched exhaustively),
    /// so the horizon's own contents are the evidence, not a lookup table.</para></summary>
    public string ResolveZone(string requested, IReadOnlyList<HorizonZone> horizonZones)
    {
        var zone = ResolveZone(requested);
        var preferred = PreferPopulatedHorizonZone(zone, horizonZones);
        return preferred.Equals(zone, StringComparison.OrdinalIgnoreCase) || _zones.ContainsKey(preferred)
            ? preferred
            : zone;
    }


    /// <summary>Fog for a zone ("zone1"/"zone2"/"zone3"); a no-op fog if the zone is absent.
    /// Callers should pass a <see cref="ResolveZone"/> result rather than a raw request.</summary>
    public ZoneFog Fog(string zone) => _zones.TryGetValue(zone, out var z) ? z : NoFog;

    /// <summary>Whiteout opacity 0..1 at a given altitude: a symmetric trapezoid across the
    /// cloud band (user-observed in-game). Clear sight (0) at BOTTOM and TOP, ramping
    /// linearly to a fully-opaque core (1 — the plane is no longer visible) that is THICKNESS
    /// deep and centred on the band's midpoint. THICKNESS is the depth of that opaque core, not
    /// an edge transition — so C1/IA1 (970–1124, ±30) is clear at 970/1124 and total in
    /// 1032–1062, with linear ramps between.</summary>
    public float WhiteoutAmount(float altitude)
    {
        if (!HasCloudBand || altitude <= CloudBottom || altitude >= CloudTop)
            return 0f;
        float mid = CloudBandCentre;
        float coreHalf = CloudThickness * 0.5f;       // half-depth of the opaque core, centred on mid
        float dist = MathF.Abs(altitude - mid);
        if (dist <= coreHalf)
            return 1f;
        float ramp = (CloudTop - CloudBottom) * 0.5f - coreHalf; // core edge → band edge
        return ramp > 1e-3f ? Mathf.Clamp(1f - (dist - coreHalf) / ramp, 0f, 1f) : 1f;
    }

    /// <summary>The colour the whiteout paints at a given altitude, or null when the mission
    /// authors no <c>CLOUD_COVER</c> colour and the caller should keep its own default.
    /// <see cref="CloudBottomColor"/> at the band's floor lerping to <see cref="CloudTopColor"/>
    /// at its ceiling.</summary>
    /// <remarks>
    /// <para>Authored data, and the target is not a judgement call: the original's C4 veil
    /// measures a flat 192 and C4 authors <c>TOP_COLOR</c>/<c>BOTTOM_COLOR</c> = 192,192,192
    /// (CAP-12's C4 take). Ours painted a hardcoded 0.95 white there — measured 242 in-cloud
    /// against the original's 192, now 192 exactly.</para>
    /// <para>⚠ The <b>lerp</b> is inferred and this install cannot falsify it. Of the four
    /// chapters whose band you can reach (C1 970–1124, C1C 1055–1110, C2B 924–1124, C4
    /// 1000–1100) only C4 authors colours and its pair is EQUAL, so every blend rule renders the
    /// same picture. The one chapter that would discriminate is C5 (top 220, bottom 64) and its
    /// band sits at 9950–10150 m — unreachable, so its values may never have been checked by
    /// their own authors either. C1B/C3 are likewise 10 km up.</para>
    /// <para>These are NOT deck-mesh face tints, whatever the names suggest: three of the four
    /// colour-carrying chapters (C1B, C3, C5) have no <c>CloudDeck</c> mesh at all
    /// (<c>WorldBuilder</c>'s coverage table), so there is nothing there to tint. Nor are they a
    /// skydome grade: the dome's colour is authored per vertex and anchored on the zone's
    /// <c>FOG_COLOR</c> (<see cref="Mech3.WorldBuilder.BuildHorizon"/>), and it reproduces the
    /// original's without any tint stage.</para>
    /// </remarks>
    public Color? WhiteoutColor(float altitude)
    {
        if (CloudTopColor is not { } top)
            return CloudBottomColor;
        if (CloudBottomColor is not { } bottom)
            return top;
        if (!HasCloudBand)
            return top;
        return bottom.Lerp(top, Mathf.Clamp((altitude - CloudBottom) / (CloudTop - CloudBottom), 0f, 1f));
    }

    /// <summary>The camera's per-frame weather state (<c>FUN_0042ee40</c>, camera state 1/2/3 —
    /// <c>PLAN-weather-decompile-match</c> A2): 1 by default; 2 when this mission authors a
    /// <c>CLOUD_COVER</c> band and <paramref name="cameraPosition"/>'s altitude is at/above
    /// <see cref="CloudCoreBottom"/> (the whiteout core's bottom edge, NOT the band centre and
    /// NOT <see cref="CloudBottom"/>); 3 when <paramref name="fogZoneArmed"/> and the camera sits
    /// inside any of <paramref name="volumes"/> — the AUTHORED shape
    /// (<c>FogVolumeBox.Contains</c>'s exact half-space test), not the volume's AABB.
    ///
    /// <para>State 3 takes precedence over state 2, matching the binary's assignment order (it
    /// computes state 3 after state 2, so an in-volume camera is always state 3 regardless of
    /// altitude) — this never actually arbitrates in shipped data, since only C5 arms
    /// <paramref name="fogZoneArmed"/> and its band sits at 9950-10150 m, far above any C5
    /// volume.</para>
    ///
    /// <para>Exposed but consumed by nothing yet — <c>WeatherRig.Tick</c> publishes it per camera
    /// each frame; wiring a consumer is left to later items in the same plan
    /// (B11/B12/C21/C22).</para></summary>
    public int CameraWeatherState(
        Vector3 cameraPosition, bool fogZoneArmed, IReadOnlyList<FogVolumeBox> volumes)
    {
        int state = HasCloudBand && cameraPosition.Y >= CloudCoreBottom ? 2 : 1;
        if (fogZoneArmed)
            foreach (var volume in volumes)
                if (volume.Contains(cameraPosition))
                    return 3;
        return state;
    }

    private static float WorldLightFactor(ZrdrDict zone)
    {
        float diffuse = zone.List("SUNLIGHT_DIFFUSE") is { Count: >= 1 } sd && sd[0] is float dv ? dv : 1f;
        float ambient = zone.List("SUNLIGHT_AMBIENT") is { Count: >= 1 } sa && sa[0] is float av ? av : 0f;
        return Mathf.Clamp(ambient + diffuse * SunIncidence, MinWorldLight, 1f);
    }

    // The file's own per-zone block names, in FILE ORDER — "ZONE1", "ZONE2" in C1–C4;
    // "ZONE1", "ZONE3" in all 8 C5 missions (the whole reason this isn't a hardcoded pair;
    // see docs/formats/weather.md). File order is what ResolveZone falls back along, so it
    // must come from this raw walk over `inner` rather than from ZrdrDict, which is a
    // Dictionary and does not preserve it.
    //
    // "ZONE" + digits only: the SW_ZONE* twins are the software-renderer variants
    // (SUNLIGHT_ACTIVE 0, ambient 1.0) and must never enter the selectable set. Matching on
    // the "ZONE" prefix alone would be enough today, but the digit test also keeps any future
    // ZONE-prefixed sibling block out.
    private static List<string> ZoneKeys(List<object?> inner)
    {
        var keys = new List<string>();
        for (int i = 0; i + 1 < inner.Count; i++)
            if (inner[i] is string s && inner[i + 1] is List<object?>
                && s.StartsWith("ZONE", StringComparison.OrdinalIgnoreCase)
                && s.Length > 4 && char.IsDigit(s[4])
                && !keys.Exists(k => k.Equals(s, StringComparison.OrdinalIgnoreCase)))
                keys.Add(s);
        return keys;
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
    // These are DX7 sRGB framebuffer values; GameSession converts them to linear for the shader.
    private static Color? ParseColor(List<object?>? list)
    {
        if (list is not { Count: >= 3 } || list[0] is not float r || list[1] is not float g || list[2] is not float b)
            return null;
        float scale = r > 1f || g > 1f || b > 1f ? 1f / 255f : 1f;
        return new Color(r * scale, g * scale, b * scale);
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

    /// <summary>Distance fog for one day/night zone: haze toward <see cref="FogColor"/> between
    /// <see cref="FogNear"/> and <see cref="FogFar"/> metres of **horizontal** view distance —
    /// the original's fog volume is a vertical cylinder around the camera, not a sphere
    /// (user-diagnosed) — scaled by an altitude fade from <c>FOG_ALTITUDE</c>: full
    /// fog below <see cref="FogLow"/>, none above <see cref="FogHigh"/>, so the cloud deck /
    /// sky overhead stays clear. The fade is by FRAGMENT altitude, settled at the controls of the
    /// original in C2 — the only chapter whose flown band (256–1024 m) is inside the flight
    /// envelope (<c>PLAN-overcast-match</c> B13, docs/formats/weather.md).
    /// ⚠ The old corroboration "zone1's 970→1047 is exactly cloud-band bottom → whiteout centre"
    /// is RETIRED: C1 flies zone2, whose band is 4000→5000 m — above the 2,500 m flight ceiling,
    /// i.e. night fog at every flyable altitude — so that identity lives in a zone C1 never flies
    /// (B12). It is real and unexplained, not evidence.
    /// The ramp between near and far is LINEAR, per the gamez world node's own
    /// <c>fog_state == 1</c> (B15). <see cref="ClipFar"/> is the original's hard far clip (informational —
    /// our far plane is much larger; the fog is what hides distant terrain, matching the
    /// original's short view distance).</summary>
    public readonly record struct ZoneFog(Color FogColor, float FogNear, float FogFar, float FogLow, float FogHigh, float ClipFar, float WorldLight);

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
}
