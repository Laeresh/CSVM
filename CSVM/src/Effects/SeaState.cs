using System;
using System.Collections.Generic;
using System.Globalization;

namespace CSVM.Effects;

/// <summary>
/// One chapter's sea on the wave ocean: every value the ocean lab tunes, each defaulting to the
/// ocean's tune. The defaults generate the constants' shader text. The field table
/// (<see cref="Fields"/>) names each value with its range and lab group. Clamping holds every value
/// in range and the ungrouped swell below folding. The shipped file (<see cref="OceanSeas"/>) holds one per
/// chapter, and the generator (<see cref="OceanShader"/>) writes it into the shader. Pure, so it
/// unit-tests without a session.
/// </summary>
public sealed record SeaState
{
    /// <summary>The bound on <see cref="Sharpness"/> times <see cref="Height"/>. Each wave bunches the
    /// surface by Q k A, the sharpness over the wave count. At the base gain the sum folds a crest at
    /// 1. The sea-state grouping scales a wave by up to 1.8, so a grouped crest can fold sooner.</summary>
    public const float FoldLimit = 1f;

    /// <summary>The narrowest a coast ramp may be, in metres, so its smoothstep keeps its edges apart.</summary>
    public const float RampGap = 4f;

    /// <summary>The sea every chapter draws with nothing saved: the ocean's tune.</summary>
    public static readonly SeaState Default = new();

    /// <summary>Every tunable, in the order the lab lists them and the file writes them.</summary>
    public static readonly IReadOnlyList<Field> Fields = new Field[]
    {
        new("height", "Height", Swell, 0f, 2f, 0.01f, s => s.Height, (s, v) => s with { Height = v },
            "sharpness x height is held at 1.0, where an ungrouped crest folds; grouped crests fold sooner. Length does not move the fold"),
        new("length", "Length", Swell, 0.5f, 2f, 0.01f, s => s.Length, (s, v) => s with { Length = v }),
        new("wind", "Wind (deg)", Swell, 0f, 360f, 1f, s => s.WindDeg, (s, v) => s with { WindDeg = v }),
        new("sharpness", "Crest sharpness", Swell, 0.1f, 1f, 0.01f, s => s.Sharpness, (s, v) => s with { Sharpness = v }),
        new("bend_m", "Bend, long waves (m)", Bending, 0f, 150f, 1f, s => s.BendMetres, (s, v) => s with { BendMetres = v }),
        new("bend_rad", "Bend cap, short waves (rad)", Bending, 0f, 30f, 0.5f, s => s.BendRadians, (s, v) => s with { BendRadians = v }),
        new("detail_slope", "Detail slope", Bending, 0f, 0.2f, 0.005f, s => s.DetailSlope, (s, v) => s with { DetailSlope = v }),
        new("detail_drift", "Detail drift", Bending, 0f, 3f, 0.05f, s => s.DetailDrift, (s, v) => s with { DetailDrift = v },
            "steps by whole noise periods per csky_time wrap"),
        new("foam_threshold", "Threshold (sigma)", Foam, 0.3f, 3f, 0.05f, s => s.FoamThreshold, (s, v) => s with { FoamThreshold = v },
            "lower forms more foam"),
        new("foam_strength", "Strength", Foam, 0f, 0.5f, 0.01f, s => s.FoamStrength, (s, v) => s with { FoamStrength = v }),
        new("foam_patch", "Patch size", Foam, 0.25f, 4f, 0.05f, s => s.FoamPatch, (s, v) => s with { FoamPatch = v }),
        new("foam_cover", "Near cover", Foam, 0.1f, 0.6f, 0.01f, s => s.FoamCover, (s, v) => s with { FoamCover = v }),
        new("swell_from", "Swell ramp from (m)", Coast, 0f, OceanMaskRaster.ShoreReach - RampGap, 1f, s => s.SwellFrom, (s, v) => s with { SwellFrom = v }),
        new("swell_full", "Swell ramp full (m)", Coast, 16f, OceanMaskRaster.ShoreReach, 1f, s => s.SwellFull, (s, v) => s with { SwellFull = v },
            "the mask stops at 160 m; farther needs a rebake"),
        new("look_from", "Look ramp from (m)", Coast, OceanMask.Cell, OceanMaskRaster.ShoreReach - RampGap, 1f, s => s.LookFrom, (s, v) => s with { LookFrom = v },
            "at least one mask texel, which keeps the coast tile seam"),
        new("look_full", "Look ramp full (m)", Coast, 16f, OceanMaskRaster.ShoreReach, 1f, s => s.LookFull, (s, v) => s with { LookFull = v }),
        new("tint_r", "Tint red", Coast, 0.5f, 1.5f, 0.01f, s => s.TintR, (s, v) => s with { TintR = v }),
        new("tint_g", "Tint green", Coast, 0.5f, 1.5f, 0.01f, s => s.TintG, (s, v) => s with { TintG = v }),
        new("tint_b", "Tint blue", Coast, 0.5f, 1.5f, 0.01f, s => s.TintB, (s, v) => s with { TintB = v }),
        new("rough_near", "Roughness near", Coast, 0.02f, 0.8f, 0.01f, s => s.RoughNear, (s, v) => s with { RoughNear = v }),
        new("rough_far", "Roughness far", Coast, 0.02f, 0.8f, 0.01f, s => s.RoughFar, (s, v) => s with { RoughFar = v }),
    };

    // The lab's groups, in the order its panel shows them.
    private const string Swell = "Swell";
    private const string Bending = "Bending + detail";
    private const string Foam = "Foam";
    private const string Coast = "Coast + colour";

    /// <summary>TUNE. The swell's height, the shader's <c>wave_scale</c>, on the grid and in the normals.</summary>
    public float Height { get; init; } = 1f;

    /// <summary>A scale on every swell wave's length. Each wave's omega is rounded again to whole
    /// cycles per <c>csky_time</c> wrap.</summary>
    public float Length { get; init; } = 1f;

    /// <summary>TUNE. The wind the swell and the detail run with, degrees from +X toward +Z.</summary>
    public float WindDeg { get; init; } = 20f;

    /// <summary>TUNE. The crest sharpness, the Gerstner choppiness shared over the swell's waves.</summary>
    public float Sharpness { get; init; } = 0.55f;

    /// <summary>TUNE. How far the still field bends a long wave's crests, in metres.</summary>
    public float BendMetres { get; init; } = 60f;

    /// <summary>TUNE. The most a short wave's phase bends, in radians, so crossing crests bend apart.</summary>
    public float BendRadians { get; init; } = 14f;

    /// <summary>TUNE. The fine detail's slope per noise layer.</summary>
    public float DetailSlope { get; init; } = 0.08f;

    /// <summary>A scale on every detail layer's drift. A drift of zero holds the detail still.</summary>
    public float DetailDrift { get; init; } = 1f;

    /// <summary>TUNE. The standard deviations of the swell's Jacobian at which foam begins. It is full one
    /// deviation further, so a whitecap grows and fades as its crest passes.</summary>
    public float FoamThreshold { get; init; } = 1.3f;

    /// <summary>TUNE. The foam's brightness, the shader's <c>foam_strength</c>.</summary>
    public float FoamStrength { get; init; } = 0.12f;

    /// <summary>A scale on the patch field's cells, which decide where whitecaps can form.</summary>
    public float FoamPatch { get; init; } = 1f;

    /// <summary>TUNE. The share of a whitecap its sheet covers up close, at the foam strength over the
    /// share. Its mean is the far foam, so the sheet's fade-in moves little brightness.</summary>
    public float FoamCover { get; init; } = 0.3f;

    /// <summary>TUNE. Metres from the shore over which the swell's height fades in. It keeps a
    /// displaced crest from lifting through a coplanar coast layer.</summary>
    public float SwellFrom { get; init; } = 24f;

    /// <summary>TUNE. Where the swell reaches full height, at most the mask's reach.</summary>
    public float SwellFull { get; init; } = OceanMaskRaster.ShoreReach;

    /// <summary>TUNE. Metres from the shore over which the look (slope, chop, foam, texture) fades in. The
    /// ocean then matches the flat sheet where an opaque coast tile meets it. It clears the texel
    /// beside the coast one, which the linear filter blends into the boundary.</summary>
    public float LookFrom { get; init; } = 12f;

    /// <summary>TUNE. Where the look is full.</summary>
    public float LookFull { get; init; } = 64f;

    /// <summary>A multiplier on the open sea's colour, red channel. The shore keeps the sheet's own.</summary>
    public float TintR { get; init; } = 1f;

    /// <summary>The open sea's colour multiplier, green channel.</summary>
    public float TintG { get; init; } = 1f;

    /// <summary>The open sea's colour multiplier, blue channel.</summary>
    public float TintB { get; init; } = 1f;

    /// <summary>TUNE. The roughness up close, the shader's <c>rough_near</c>.</summary>
    public float RoughNear { get; init; } = 0.2f;

    /// <summary>TUNE. The roughness far off, the shader's <c>rough_far</c>.</summary>
    public float RoughFar { get; init; } = 0.3f;

    /// <summary>Whether the open sea's colour is tinted at all. An untinted sea writes no tint term.</summary>
    public bool Tinted => TintR != 1f || TintG != 1f || TintB != 1f;

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

    /// <summary>The <c>--debug-ocean</c> grammar: <c>open</c> and <c>&lt;field&gt;:&lt;number&gt;</c>,
    /// comma-separated. Keeps the tokens it understands, in order, and appends the rest to
    /// <paramref name="rejected"/> when one is supplied.</summary>
    public static string FilterOverrides(string spec, List<string>? rejected = null)
    {
        var kept = new List<string>();
        foreach (string token in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Equals("open", StringComparison.OrdinalIgnoreCase) || ParseToken(token) != null)
                kept.Add(token);
            else
                rejected?.Add(token);
        }

        return string.Join(",", kept);
    }

    /// <summary>Whether a <c>--debug-ocean</c> value asks for the lab's panel at launch.</summary>
    public static bool OverridesOpen(string? spec) =>
        spec != null && Array.Exists(spec.Split(',', StringSplitOptions.TrimEntries),
            t => t.Equals("open", StringComparison.OrdinalIgnoreCase));

    /// <summary>This sea with every <c>&lt;field&gt;:&lt;number&gt;</c> token of a filtered
    /// <c>--debug-ocean</c> value applied left to right, then clamped.</summary>
    public SeaState WithOverrides(string? spec)
    {
        var s = this;
        foreach (string token in (spec ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (ParseToken(token) is var (field, value))
                s = field.With(s, value);
        }

        return s.Clamped();
    }

    /// <summary>This sea with every value in its field's range and each coast ramp at least
    /// <see cref="RampGap"/> wide. Sharpness times height is at most <see cref="FoldLimit"/>, though
    /// grouped crests can still fold. A value that is not a number takes its default. Every path into
    /// the shader goes through here.</summary>
    public SeaState Clamped()
    {
        var s = this;
        foreach (var f in Fields)
        {
            float v = f.Get(s);
            float c = float.IsFinite(v) ? Math.Clamp(v, f.Min, f.Max) : f.Default;
            if (c != v)
                s = f.With(s, c);
        }

        if (s.Height * s.Sharpness > FoldLimit)
            s = s with { Height = FoldLimit / s.Sharpness };
        if (s.SwellFrom > s.SwellFull - RampGap)
            s = s with { SwellFrom = s.SwellFull - RampGap };
        if (s.LookFrom > s.LookFull - RampGap)
            s = s with { LookFrom = s.LookFull - RampGap };
        return s;
    }

    /// <summary>This sea as the file holds it: clamped, each changed field at <see cref="Field.Rounded"/>
    /// precision and each unchanged one at its default. A value that rounding carries past a bound
    /// <see cref="Clamped"/> holds is rounded toward the bound instead, so the file reads back with no clamp.</summary>
    public SeaState Written()
    {
        var clamped = Clamped();
        var s = clamped;
        foreach (var f in Fields)
            s = f.With(s, clamped.Differs(f) ? (float)Field.Rounded(f.Get(clamped)) : f.Default);

        // Every bound one field sets on another reads a field that only its range clamps, so one pass settles.
        var held = s.Clamped();
        foreach (var f in Fields)
        {
            float v = f.Get(s), c = f.Get(held);
            if (c != v)
                s = f.With(s, (float)Field.RoundedToward(c, up: c > v));
        }

        return s;
    }

    /// <summary>Whether <paramref name="field"/> differs from its default as the file writes it.</summary>
    public bool Differs(Field field) => Field.Rounded(field.Get(this)) != Field.Rounded(field.Default);

    /// <summary>The fields that differ from the defaults as <c>key:value</c>, or <c>defaults</c>.</summary>
    public string Describe()
    {
        var parts = new List<string>();
        foreach (var f in Fields)
        {
            if (Differs(f))
                parts.Add($"{f.Key}:{f.Format(f.Get(this))}");
        }

        return parts.Count == 0 ? "defaults" : string.Join(",", parts);
    }

    private static (Field Field, float Value)? ParseToken(string token)
    {
        int colon = token.IndexOf(':');
        if (colon <= 0 || Find(token[..colon].Trim()) is not { } field
            || !float.TryParse(token[(colon + 1)..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            || !float.IsFinite(value))
            return null;
        return (field, value);
    }

    /// <summary>One tunable: its file and flag key, its lab label and group, its range and slider step.
    /// It reads and sets its value, and carries a note the lab shows under its slider.</summary>
    public sealed record Field(string Key, string Label, string Group, float Min, float Max, float Step,
        Func<SeaState, float> Get, Func<SeaState, float, SeaState> With, string Note = "")
    {
        /// <summary>The value <see cref="SeaState.Default"/> holds.</summary>
        public float Default => Get(SeaState.Default);

        /// <summary>A value as the file writes it: four decimals, so a float's tail never reads as a change.</summary>
        public static double Rounded(float v) => Math.Round((double)v, 4);

        /// <summary>The four-decimal value nearest <paramref name="v"/> at or above it when
        /// <paramref name="up"/>, else at or below it, compared as the float the file reads back.</summary>
        public static double RoundedToward(float v, bool up)
        {
            double r = Rounded(v);
            if (up ? (float)r < v : (float)r > v)
                r = Math.Round(r + (up ? 1e-4 : -1e-4), 4);
            return r;
        }

        /// <summary>A value at the precision its slider steps in, invariant.</summary>
        public string Format(float v) =>
            v.ToString(Step >= 1f ? "0" : Step >= 0.1f ? "0.0" : Step >= 0.01f ? "0.00" : "0.000", CultureInfo.InvariantCulture);
    }
}
