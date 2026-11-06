using System;
using System.Collections.Generic;
using System.Globalization;

namespace CSVM.Effects;

/// <summary>
/// One weather zone's Enhanced lighting tune: every value the lighting lab sets, each defaulting to
/// what the Enhanced look draws with no entry. The multipliers and tints adjust the decoded zone,
/// so authored differences between missions survive. The plain shadow values replace the
/// <c>EnhancedLook</c> constants, whose defaults live here. The shipped file
/// (<c>Session/World/LightingTable</c>) holds a sparse block per chapter and mission zone, and
/// <c>WeatherRig</c> applies the resolved block on every zone apply. Pure, so it
/// unit-tests without a session. Decode of the zone light: docs/org/weather.md.
/// </summary>
public sealed record ZoneLighting
{
    /// <summary>The tune with nothing saved: the current Enhanced look.</summary>
    public static readonly ZoneLighting Default = new();

    /// <summary>Every tunable, in the order the lab lists them and the file writes them.</summary>
    public static readonly IReadOnlyList<Field> Fields = new Field[]
    {
        new("sun_energy", "Sun energy per diffuse", Sun, 0f, 4f, 0.01f, l => l.SunEnergy, (l, v) => l with { SunEnergy = v },
            "in place of x1.07; a night zone scales its capped diffuse"),
        new("sun_r", "Sun tint red", Sun, 0f, 2f, 0.01f, l => l.SunR, (l, v) => l with { SunR = v }),
        new("sun_g", "Sun tint green", Sun, 0f, 2f, 0.01f, l => l.SunG, (l, v) => l with { SunG = v }),
        new("sun_b", "Sun tint blue", Sun, 0f, 2f, 0.01f, l => l.SunB, (l, v) => l with { SunB = v }),
        new("sun_yaw", "Sun yaw offset (deg)", Sun, -180f, 180f, 0.5f, l => l.SunYaw, (l, v) => l with { SunYaw = v }),
        new("sun_pitch", "Sun pitch offset (deg)", Sun, -60f, 60f, 0.5f, l => l.SunPitch, (l, v) => l with { SunPitch = v },
            "positive raises the sun; the terrain bake follows only the saved bearing"),
        new("ambient_energy", "Ambient energy per authored", Ambient, 0f, 6f, 0.01f, l => l.AmbientEnergy, (l, v) => l with { AmbientEnergy = v },
            "in place of x1.8; a night zone scales its capped ambient"),
        new("ambient_r", "Ambient tint red", Ambient, 0f, 2f, 0.01f, l => l.AmbientR, (l, v) => l with { AmbientR = v }),
        new("ambient_g", "Ambient tint green", Ambient, 0f, 2f, 0.01f, l => l.AmbientG, (l, v) => l with { AmbientG = v }),
        new("ambient_b", "Ambient tint blue", Ambient, 0f, 2f, 0.01f, l => l.AmbientB, (l, v) => l with { AmbientB = v }),
        new("sky_r", "Sky + fog tint red", Sky, 0f, 2f, 0.01f, l => l.SkyR, (l, v) => l with { SkyR = v },
            "over FOG_COLOR; the night rule reads the authored colour"),
        new("sky_g", "Sky + fog tint green", Sky, 0f, 2f, 0.01f, l => l.SkyG, (l, v) => l with { SkyG = v }),
        new("sky_b", "Sky + fog tint blue", Sky, 0f, 2f, 0.01f, l => l.SkyB, (l, v) => l with { SkyB = v }),
        new("exposure", "Exposure", Tonemap, 0.25f, 4f, 0.01f, l => l.Exposure, (l, v) => l with { Exposure = v }),
        new("white", "AgX white", Tonemap, 1f, 16f, 0.1f, l => l.White, (l, v) => l with { White = v }),
        new("shadow_softness", "Softness", Shadows, 0f, 4f, 0.05f, l => l.ShadowSoftness, (l, v) => l with { ShadowSoftness = v },
            "scales the Shadow Quality tier's blur and sun size; off and --no-soft-shadows still win"),
        new("shadow_distance", "Max distance scale", Shadows, 0.1f, 4f, 0.05f, l => l.ShadowDistance, (l, v) => l with { ShadowDistance = v },
            "on the zone's pushed fog near"),
        new("shadow_bias", "Bias", Shadows, 0f, 0.5f, 0.005f, l => l.ShadowBias, (l, v) => l with { ShadowBias = v }),
        new("normal_bias", "Normal bias", Shadows, 0f, 5f, 0.05f, l => l.NormalBias, (l, v) => l with { NormalBias = v }),
        new("split1", "Split 1", Shadows, 0.01f, 1f, 0.005f, l => l.Split1, (l, v) => l with { Split1 = v }),
        new("split2", "Split 2", Shadows, 0.01f, 1f, 0.005f, l => l.Split2, (l, v) => l with { Split2 = v }),
        new("split3", "Split 3", Shadows, 0.01f, 1f, 0.005f, l => l.Split3, (l, v) => l with { Split3 = v }),
        new("fade_start", "Fade start", Shadows, 0f, 1f, 0.01f, l => l.FadeStart, (l, v) => l with { FadeStart = v }),
    };

    private const string Sun = "Sun";
    private const string Ambient = "Ambient";
    private const string Sky = "Sky + fog";
    private const string Tonemap = "Tonemap";
    private const string Shadows = "Shadows";

    /// <summary>⚠ TUNE. The sun's Godot energy per unit of authored <c>SUNLIGHT_DIFFUSE</c>. Anchored on the
    /// modal day zone (diffuse 1.5) landing on the 1.6 the faithful path hardcodes.</summary>
    public float SunEnergy { get; init; } = 1.07f;

    /// <summary>A multiplier on the decoded <c>SUNLIGHT</c> diffuse colour, red channel.</summary>
    public float SunR { get; init; } = 1f;

    /// <summary>The sun tint's green channel.</summary>
    public float SunG { get; init; } = 1f;

    /// <summary>The sun tint's blue channel.</summary>
    public float SunB { get; init; } = 1f;

    /// <summary>Degrees added to the decoded or dome-aimed bearing's yaw.</summary>
    public float SunYaw { get; init; }

    /// <summary>Degrees the bearing is raised by, positive toward the zenith.</summary>
    public float SunPitch { get; init; }

    /// <summary>⚠ TUNE. The ambient's Godot energy per unit of authored <c>SUNLIGHT_AMBIENT</c>. Anchored
    /// on the modal day zone (ambient 0.5) landing on the faithful path's 0.9.</summary>
    public float AmbientEnergy { get; init; } = 1.8f;

    /// <summary>A multiplier on the decoded ambient colour, red channel.</summary>
    public float AmbientR { get; init; } = 1f;

    /// <summary>The ambient tint's green channel.</summary>
    public float AmbientG { get; init; } = 1f;

    /// <summary>The ambient tint's blue channel.</summary>
    public float AmbientB { get; init; } = 1f;

    /// <summary>A multiplier on the decoded <c>FOG_COLOR</c>, red channel, for the fog and the reflected sky.</summary>
    public float SkyR { get; init; } = 1f;

    /// <summary>The sky tint's green channel.</summary>
    public float SkyG { get; init; } = 1f;

    /// <summary>The sky tint's blue channel.</summary>
    public float SkyB { get; init; } = 1f;

    /// <summary>The AgX tonemap's exposure. Neutral by default.</summary>
    public float Exposure { get; init; } = 1f;

    /// <summary>TUNE. The AgX white point, which recovers the horizon; AgX ignores the general white.</summary>
    public float White { get; init; } = 6f;

    /// <summary>A multiplier on the Shadow Quality tier's blur and angular distance.</summary>
    public float ShadowSoftness { get; init; } = 1f;

    /// <summary>A multiplier on the shadow max distance the zone derives from its pushed fog near.</summary>
    public float ShadowDistance { get; init; } = 1f;

    /// <summary>TUNE, judged at the controls. The sun's shadow bias. Lower values put dithered acne over
    /// lit surfaces at C1's 25 degree sun, and higher ones dissolve a hangar's shadow. The ground and
    /// the water cast no shadow map, so neither can band itself.</summary>
    public float ShadowBias { get; init; } = 0.05f;

    /// <summary>TUNE. The sun's normal bias, paired with <see cref="ShadowBias"/>.</summary>
    public float NormalBias { get; init; } = 1.25f;

    /// <summary>TUNE. The first cascade split as a fraction of the max distance, tighter than Godot's
    /// 0.1/0.2/0.5: a player reads shadows within a few hundred metres.</summary>
    public float Split1 { get; init; } = 0.12f;

    /// <summary>TUNE. The second cascade split.</summary>
    public float Split2 { get; init; } = 0.17f;

    /// <summary>TUNE. The third cascade split; the outer cascades carry a skyline into the haze.</summary>
    public float Split3 { get; init; } = 0.42f;

    /// <summary>TUNE. The fraction of the max distance where the last cascade starts fading, so a
    /// shadow dissolves into the haze rather than cutting off.</summary>
    public float FadeStart { get; init; } = 0.8f;

    /// <summary>Whether the bearing is offset at all. An unoffset bearing must stay bit-identical, since
    /// the terrain bake is keyed by it.</summary>
    public bool TurnsSun => SunYaw != 0f || SunPitch != 0f;

    /// <summary>The field named <paramref name="key"/>, or null.</summary>
    public static Field? Find(string key)
    {
        foreach (var f in Fields)
        {
            if (string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))
                return f;
        }

        return null;
    }

    /// <summary>This tune with every field of <paramref name="sparse"/> set over it, then clamped.</summary>
    public ZoneLighting Over(IReadOnlyDictionary<string, float>? sparse)
    {
        var l = this;
        if (sparse == null)
            return l;
        foreach (var (key, value) in sparse)
        {
            if (Find(key) is { } field)
                l = field.With(l, value);
        }

        return l.Clamped();
    }

    /// <summary>This tune with every value in its field's range; a value that is not a number takes its default.</summary>
    public ZoneLighting Clamped()
    {
        var l = this;
        foreach (var f in Fields)
        {
            float v = f.Get(l);
            float c = float.IsFinite(v) ? Math.Clamp(v, f.Min, f.Max) : f.Default;
            if (c != v)
                l = f.With(l, c);
        }

        return l;
    }

    /// <summary>The fields that differ from <paramref name="baseline"/> at file precision, by key, in
    /// <see cref="Fields"/> order. The sparse block the file writes.</summary>
    public Dictionary<string, float> Differences(ZoneLighting baseline)
    {
        var sparse = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in Fields)
        {
            if (Field.Rounded(f.Get(this)) != Field.Rounded(f.Get(baseline)))
                sparse[f.Key] = (float)Field.Rounded(f.Get(this));
        }

        return sparse;
    }

    /// <summary>The fields that differ from the defaults as <c>key:value</c>, or <c>defaults</c>.</summary>
    public string Describe()
    {
        var parts = new List<string>();
        foreach (var (key, value) in Differences(Default))
            parts.Add($"{key}:{Find(key)!.Format(value)}");
        return parts.Count == 0 ? "defaults" : string.Join(",", parts);
    }

    /// <summary>One tunable: its file and flag key, its lab label and group, its range and slider step.
    /// It reads and sets its value, and carries a note the lab shows under its slider.</summary>
    public sealed record Field(string Key, string Label, string Group, float Min, float Max, float Step,
        Func<ZoneLighting, float> Get, Func<ZoneLighting, float, ZoneLighting> With, string Note = "")
    {
        /// <summary>The value <see cref="ZoneLighting.Default"/> holds.</summary>
        public float Default => Get(ZoneLighting.Default);

        /// <summary>A value as the file writes it: four decimals, so a float's tail never reads as a change.</summary>
        public static double Rounded(float v) => Math.Round((double)v, 4);

        /// <summary>A value at the precision its slider steps in, invariant.</summary>
        public string Format(float v) =>
            v.ToString(Step >= 1f ? "0" : Step >= 0.1f ? "0.0" : Step >= 0.01f ? "0.00" : "0.000", CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// The <c>--debug-lighting</c> grammar, the lighting lab's scripted twin, comma-separated. Its tokens
/// are <c>zone:&lt;name&gt;</c>, <c>&lt;field&gt;:&lt;number&gt;</c> and <c>plane:&lt;name&gt;</c>.
/// Beside them sit <c>heading:</c>, <c>pitch:</c> and <c>bank:</c> in degrees, <c>off</c> and <c>open</c>.
/// Pure, so it unit-tests.
/// </summary>
public static class LightingDebug
{
    private static readonly string[] Words = { "zone", "plane" };
    private static readonly string[] Angles = { "heading", "pitch", "bank" };

    /// <summary>Keeps the tokens the lab reads, in order, and appends the rest to
    /// <paramref name="rejected"/> when one is supplied.</summary>
    public static string FilterTokens(string spec, List<string>? rejected = null)
    {
        var kept = new List<string>();
        foreach (string token in Tokens(spec))
        {
            if (Known(token))
                kept.Add(token);
            else
                rejected?.Add(token);
        }

        return string.Join(",", kept);
    }

    /// <summary>Whether the value carries the bare <paramref name="flag"/> (<c>open</c> or <c>off</c>).</summary>
    public static bool Has(string? spec, string flag) =>
        Array.Exists(Tokens(spec), t => t.Equals(flag, StringComparison.OrdinalIgnoreCase));

    /// <summary>The last <c>zone:</c> or <c>plane:</c> value, or null.</summary>
    public static string? Word(string? spec, string key)
    {
        string? found = null;
        foreach (string token in Tokens(spec))
        {
            if (Split(token) is var (k, v) && k.Equals(key, StringComparison.OrdinalIgnoreCase) && v.Length > 0)
                found = v;
        }

        return found;
    }

    /// <summary>The last <c>heading:</c>, <c>pitch:</c> or <c>bank:</c> value in degrees, or null.</summary>
    public static float? Angle(string? spec, string key)
    {
        float? found = null;
        foreach (string token in Tokens(spec))
        {
            if (Split(token) is var (k, v) && k.Equals(key, StringComparison.OrdinalIgnoreCase) && Number(v) is { } n)
                found = n;
        }

        return found;
    }

    /// <summary><paramref name="lighting"/> with every field token applied left to right, then clamped.</summary>
    public static ZoneLighting WithFields(ZoneLighting lighting, string? spec)
    {
        var l = lighting;
        foreach (string token in Tokens(spec))
        {
            if (Split(token) is var (k, v) && ZoneLighting.Find(k) is { } field && Number(v) is { } n)
                l = field.With(l, n);
        }

        return l.Clamped();
    }

    /// <summary>Whether the value sets any field.</summary>
    public static bool SetsFields(string? spec) =>
        Array.Exists(Tokens(spec), t => Split(t) is var (k, v) && ZoneLighting.Find(k) != null && Number(v) != null);

    private static string[] Tokens(string? spec) =>
        (spec ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool Known(string token)
    {
        if (token.Equals("open", StringComparison.OrdinalIgnoreCase) || token.Equals("off", StringComparison.OrdinalIgnoreCase))
            return true;
        if (Split(token) is not var (k, v))
            return false;
        if (Array.Exists(Words, w => w.Equals(k, StringComparison.OrdinalIgnoreCase)))
            return v.Length > 0;
        return (Array.Exists(Angles, a => a.Equals(k, StringComparison.OrdinalIgnoreCase)) || ZoneLighting.Find(k) != null)
               && Number(v) != null;
    }

    private static (string Key, string Value)? Split(string token)
    {
        int colon = token.IndexOf(':');
        return colon <= 0 ? null : (token[..colon].Trim(), token[(colon + 1)..].Trim());
    }

    private static float? Number(string text) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float n) && float.IsFinite(n) ? n : null;
}
