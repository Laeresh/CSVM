using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// The wave ocean's shader text, generated from one sea (<see cref="SeaState"/>) with no engine object
/// touched. The swell and detail tables, the foam and the coast ramps are written as literals. The
/// defaults write the constants' text, held byte for byte by <c>OceanShaderTests</c>, so no golden
/// moves until a sea is saved. Every rate is rounded to whole cycles per <c>csky_time</c> wrap, so
/// the hourly rollover lands on an identical frame at any setting.
/// </summary>
public static class OceanShader
{
    /// <summary>The ship calm zones the shader carries, nearest the eye first.</summary>
    public const int MaxShips = 16;

    /// <summary>Remake-only rule: set by the look at the controls. The track entries each hull passes
    /// the wake (<see cref="OceanWakeTrack"/>): its position, a lead along its heading, then samples.</summary>
    public const int WakeSamples = 12;

    // Remake-only rule: set by the look. The wake's Kelvin waves are off until they are retuned. Off, the
    // shader draws a hull's foam trail and arm foam but no V of waves, in height or in shading.
    private const bool WakeWaves = false;

    // Remake-only rule: set by the look. A hull's wake scales its lengths by the square root of its length
    // over WakeRefHull. A small hull's V and trail are then not a ship's shrunk by its length alone.
    private const float WakeRefHull = 250f;

    // Remake-only rule: set by the look. The wake's height at the reference hull, at WakeRefSpeed and
    // over, in metres. The transverse waves fall off as R / (R + x) behind the bow, R being WakeVReach
    // scaled; the diverging ones as its square root. Both calm with the water's age over WakeCalmSecs.
    private const float WakeHeight = 1.6f;
    private const float WakeVReach = 400f;
    private const float WakeRefSpeed = 8f;
    private const float WakeCalmSecs = 20f;

    // Remake-only rule: set by the look. The wake's slope in the shading runs this many times its height's.
    // A V of long, low waves then reads over a rough sea without raising the water that far.
    private const float WakeSlopeGain = 3f;

    // Remake-only rule: a hull under WakeMinSpeed m/s draws no wake; it grows in over three times that.
    private const float WakeMinSpeed = OceanWakeTrack.HeadingSpeed;

    // Remake-only rule: set by the look. The Kelvin waves run at this share of their deep-water
    // length. A fast hull's V then reads from the air, not as a 100 m swell.
    private const float WakeLengthScale = 0.6f;

    // Remake-only rule: set by the look. The two Kelvin families' shares of the height. The diverging
    // waves rise by WakeCusp toward the cusp line at the V's edge, over WakeCuspWidth of the wedge.
    private const float WakeTransverse = 0.35f;
    private const float WakeDiverging = 0.7f;
    private const float WakeCusp = 1.5f;
    private const float WakeCuspWidth = 0.4f;

    // Remake-only rule: set by the look. The diverging waves shorten toward the track. They fade out
    // between the cusp's sec^2 of 1.5 and this, so the V's middle shows no fine grain.
    private const float WakeDivergingSec2 = 4f;

    // Remake-only rule: set by the look. A noise field on the water breaks the crests into groups.
    // These are its cell in metres on the reference hull (at least WakeGroupCellMin), its share of a
    // crest's height and its phase shift in radians.
    private const float WakeGroupCell = 40f;
    private const float WakeGroupGain = 0.9f;
    private const float WakeGroupPhase = 2.5f;
    private const float WakeGroupCellMin = 15f;

    // Remake-only rule: set by the look. A finer field on the water makes the crests irregular. Its cell
    // is in metres on the reference hull, with a least cell. It warps a crest's phase by up to the given
    // radians. A threshold, its band and bias given, breaks each crest into separate lengths.
    private const float WakeJitterCell = 40f;
    private const float WakeJitterCellMin = 12f;
    private const float WakeCrestJitter = 3f;
    private const float WakeCrestBreak = 0.4f;
    private const float WakeCrestKeep = 0.05f;

    // Remake-only rule: set by the look. The share of the wedge over which the V's edge fades in.
    private const float WakeEdge = 0.05f;

    // Remake-only rule: set by the look. Foam along the V's arms, broken up by the trail's own noise and
    // not by the crests. These are the band of the wedge either side of the cusp line, the foam's density
    // there and its reach in hulls.
    private const float WakeCuspFoamBand = 0.35f;
    private const float WakeCuspFoamCover = 0.9f;
    private const float WakeCuspFoamReach = 4f;

    // Remake-only rule: set by the look. The foam trail's half width grows from the half beam by
    // WakeFoamSpread metres per metre behind the stern, scaled as the hull's lengths are. It is dense
    // over WakeFoamCore of that width.
    // It ramps in over WakeFoamRamp of a half length ahead of the stern.
    private const float WakeFoamSpread = 0.06f;
    private const float WakeFoamCore = 0.55f;
    private const float WakeFoamRamp = 0.25f;

    // Remake-only rule: set by the look. The white the wake's foam reaches at its densest. Then the cell
    // of the noise that breaks it into patches on the reference hull, in metres.
    private const float WakeFoamWhite = 0.85f;
    private const float WakeFoamCell = 8f;

    // Remake-only rule: set by the look. On the reference hull the trail's foam thins by e over this many
    // metres behind the stern. Its cell is at least WakeFoamCellMin metres on any hull.
    private const float WakeFoamReach = 600f;
    private const float WakeFoamCellMin = 2f;

    // Remake-only rule: set by the look. The wake's foam churns: its warp field moves this many cells
    // a second of the water's age. Behind the stern it is fresh, at WakeFoamFresh density and full white,
    // and over WakeFoamFreshSecs falls to WakeFoamAged of the density and white.
    private const float WakeFoamChurn = 0.12f;
    private const float WakeFoamFreshSecs = 8f;
    private const float WakeFoamAged = 0.6f;
    private const float WakeFoamFresh = 1f;

    // The metres the newest segment runs on past the bow. The water beside the hull then reads its
    // distance behind the bow, not from the hull's centre.
    private const float WakeAheadMargin = 20f;

    // The shortest hull a wake's fall-off is measured in, in metres.
    private const float WakeMinHull = 10f;

    // TUNE. The fog amount by which the ocean shades wholly as the flat sheet. Low, so fog thick
    // enough to see covers no wave the sheet would not show.
    private const float FogSheetAt = 0.25f;

    // TUNE. The sea-state field that keeps the fixed waves from meeting on a lattice. Each wave reads
    // two noise fields along its own direction: one scales its height, one warps its phase. How far
    // the warp bends a crest is the sea's (SeaState.BendMetres, BendRadians). The warp's slope is
    // part of each wave's local wavelength, which the fades and the normals read. The field holds
    // still in world space, so the csky_time wrap lands on an identical frame.
    private const float GroupScale = 500f;
    private const float GroupGain = 0.8f;
    private const float PhaseScaleCoarse = 360f;
    private const float PhaseScaleFine = 90f;
    private const float PhaseFineShare = 0.5f;

    // The longest a warped wave counts as, in its own lengths, when the fades read it. The bound
    // keeps the tables' longest-first order valid for every later wave's local length.
    private const float WarpStretchMax = 1.5f;

    // TUNE. A finer field for the short waves, whose crests the coarse one leaves as a straight
    // grain. A wave feels it fully at FineFull metres and shorter, not at all from FineFrom up, so
    // the long swell keeps its shape.
    private const float FineGroupScale = 40f;
    private const float FinePhaseScale = 28f;
    private const float FineGain = 1.2f;
    private const float FineMetres = 5f;
    private const float FineFrom = 40f;
    private const float FineFull = 10f;

    // TUNE. The cells a detail layer's wavelength is taken as for the footprint fade. Gradient
    // noise puts most of its energy near two cells.
    private const float DetailWaveCells = 2f;

    // The cells along a detail layer's drift axis after which its noise repeats, so the drift
    // wraps with csky_time. At the shortest cell it is still hundreds of metres.
    private const int DetailPeriod = 1024;

    // TUNE. Pixels per wavelength over which a wave fades into the fragment normals. A wave drawn
    // at a few pixels per wavelength reads as a straight grain, not as water.
    private const float FootprintFadeFrom = 4f;
    private const float FootprintFadeFull = 9f;

    // TUNE. The foam gate's width in standard deviations of the swell's Jacobian, above the sea's
    // threshold. Wide, so a whitecap grows and fades as its crest passes instead of popping.
    private const float FoamGateWidth = 1f;

    // TUNE. How far the patch field moves a crest's threshold, in the same standard deviations.
    private const float FoamPatchShift = 1.2f;

    // TUNE. The foam's own shape. A breakup splits a crest's foam into whitecaps. Up close each
    // whitecap is a sheet with round holes FoamCellFine apart, longer along the wind; a coarse
    // noise thins it and warps the holes. All of it rides the water (the grid parameter), so a
    // passing crest uncovers it instead of dragging it along.
    private const float FoamBreakupCell = 12f;
    private const float FoamCellCoarse = 3.2f;
    private const float FoamCellFine = 1.4f;
    private const float FoamStretch = 1.8f;

    // TUNE. Foam's roughness, which it takes in the share it covers, so it reflects no sky.
    private const float FoamRoughness = 0.6f;

    // TUNE. The swell the vertex stage displaces: wavelengths of tens of metres, a crest of about
    // 1-2 m where several meet. Angles are offsets from the wind. The lengths step by about 1.3
    // with no common ratio, so no two waves dominate and cross on a visible lattice.
    private static readonly Wave[] Swell =
    {
        new(0f, 152f, 0.04f), new(31f, 117f, 0.042f), new(-24f, 93f, 0.045f), new(55f, 71f, 0.047f),
        new(-47f, 57f, 0.05f), new(14f, 44f, 0.052f), new(-71f, 34f, 0.052f), new(38f, 26f, 0.06f),
    };

    // TUNE. The fine detail the fragment normals add on top of the swell: gradient-noise layers, each
    // drifting along its own direction. Noise has no fixed crests, so crossing layers print no weave.
    // Cell in metres, longest first; speed in m/s, rounded so the csky_time wrap lands on a whole
    // DetailPeriod of cells.
    private static readonly Detail[] DetailLayers =
    {
        new(-38f, 6f, 2.2f), new(57f, 3.4f, 1.6f), new(-71f, 1.9f, 1.2f), new(14f, 1.05f, 0.9f),
        new(-20f, 0.58f, 0.6f),
    };

    // TUNE. The foam's patch field, which decides where whitecaps can form: two gradient-noise
    // octaves drifting with the wind. Cell in metres, weight, drift in m/s, rounded so the
    // csky_time wrap lands on a whole period of cells.
    private static readonly FoamPatch[] FoamPatches = { new(230f, 0.65f, 1.5f), new(71f, 0.35f, 1.5f) };

    /// <summary>The ocean's shader text for <paramref name="sea"/>, clamped first, drawn at priority
    /// <paramref name="level"/>. A <paramref name="zoned"/> sheet gets the zone gate.</summary>
    public static string Code(SeaState sea, int level, bool zoned) => ShaderCode(sea.Clamped(), level, zoned);

    /// <summary>The cycles a swell wave of <paramref name="length"/> metres runs per <c>csky_time</c>
    /// wrap: deep-water omega rounded to a whole number, at least one.</summary>
    public static float SwellCyclesPerWrap(float length)
    {
        float omega = Mathf.Sqrt(9.81f * (Mathf.Tau / length));
        return Mathf.Max(1f, Mathf.Round(omega * (float)ShaderTime.RolloverSecs / Mathf.Tau));
    }

    /// <summary>The whole noise periods a detail layer of <paramref name="cell"/> metres drifts per
    /// <c>csky_time</c> wrap at <paramref name="speed"/> m/s: one at least, unless held still.</summary>
    public static double DetailTurnsPerWrap(float cell, float speed) =>
        Math.Max(speed > 0f ? 1.0 : 0.0, Math.Round(speed * ShaderTime.RolloverSecs / (DetailPeriod * cell)));

    /// <summary>The whole periods of cells a foam patch octave of <paramref name="cell"/> metres
    /// drifts per <c>csky_time</c> wrap at <paramref name="speed"/> m/s, at least one.</summary>
    public static float PatchPeriodsPerWrap(float cell, float speed) =>
        (float)Math.Max(1.0, Math.Round(speed * ShaderTime.RolloverSecs / cell));

    /// <summary>The swell table's wavelengths as the sea scales them, longest first.</summary>
    public static IEnumerable<float> SwellLengths(SeaState sea)
    {
        foreach (var w in Swell)
            yield return w.Length * sea.Length;
    }

    private static string Literal(float v) => v.ToString("0.0#####", CultureInfo.InvariantCulture);

    // vec4(drift dir.x, dir.z, 1 / cell, drift in cells per second) per detail layer, and vec2
    // grouping weights, a golden angle on per layer. The drift is a whole number of DetailPeriods
    // per csky_time wrap, so the hourly rollover lands on an identical frame.
    private static void EmitDetail(StringBuilder sb, SeaState sea)
    {
        var l = new List<string>();
        var g = new List<string>();
        for (int i = 0; i < DetailLayers.Length; i++)
        {
            var d = DetailLayers[i];
            if (i > 0 && d.Cell >= DetailLayers[i - 1].Cell)
                throw new InvalidOperationException("ocean: the detail table must run longest first; the fragment's fade ends its sum on the first faded layer");
            float ang = Mathf.DegToRad(sea.WindDeg + d.AngleDeg);
            double turns = DetailTurnsPerWrap(d.Cell, d.Speed * sea.DetailDrift);
            float cellsPerSec = (float)(turns * DetailPeriod / ShaderTime.RolloverSecs);
            l.Add($"vec4({Literal(Mathf.Cos(ang))}, {Literal(Mathf.Sin(ang))}, {Literal(1f / d.Cell)}, {Literal(cellsPerSec)})");
            float u = Mathf.DegToRad(137.50776f * (i + Swell.Length));
            g.Add($"vec2({Literal(GroupGain * Mathf.Cos(u))}, {Literal(GroupGain * Mathf.Sin(u))})");
        }
        sb.AppendLine($"const vec4 DETAIL_L[{l.Count}] = vec4[{l.Count}]({string.Join(", ", l)});");
        sb.AppendLine($"const vec2 DETAIL_G[{g.Count}] = vec2[{g.Count}]({string.Join(", ", g)});");
    }

    // The patch field's octaves as shader terms. Each drifts one whole period of cells per csky_time
    // wrap, so the hourly rollover lands on an identical field.
    private static string FoamPatchTerms(SeaState sea)
    {
        var sb = new StringBuilder();
        foreach (var o in FoamPatches)
        {
            float cell = o.Cell * sea.FoamPatch;
            float period = PatchPeriodsPerWrap(cell, o.Speed);
            string perSec = $"({Literal(period)} / {Literal((float)ShaderTime.RolloverSecs)})";
            sb.Append(CultureInfo.InvariantCulture, $" + {Literal(o.Weight)} * foam_noise(pw * {Literal(1f / cell)} - vec2(csky_time * {perSec}, 0.0), {Literal(period)})");
        }
        return sb.ToString();
    }

    // vec4(dir.x, dir.z, k, A) and vec2(Q, omega) per wave. Omega is rounded to a whole number of
    // cycles per csky_time wrap, so the hourly rollover lands on an identical frame.
    // Also vec4 _MOD per wave: the height field's weights, then the phase field's. Each wave's weights
    // turn a golden angle on from the last, so no two waves follow the field alike. The _FINE
    // weights do the same on the fine field, scaled by how short the wave is.
    private static void EmitWaves(StringBuilder sb, string name, Wave[] waves, SeaState sea)
    {
        var a = new List<string>();
        var b = new List<string>();
        var m = new List<string>();
        var fine = new List<string>();
        for (int i = 1; i < waves.Length; i++)
        {
            if (waves[i].Length >= waves[i - 1].Length)
                throw new InvalidOperationException($"ocean: the {name} table must run longest first; the fragment's fade ends its sum on the first faded wave");
        }
        foreach (var w in waves)
        {
            float length = w.Length * sea.Length;
            float u = Mathf.DegToRad(137.50776f * m.Count);
            float rad = Mathf.Min(sea.BendRadians, Mathf.Tau / length * sea.BendMetres);
            m.Add($"vec4({Literal(GroupGain * Mathf.Cos(u))}, {Literal(GroupGain * Mathf.Sin(u))}, {Literal(rad * Mathf.Sin(u + 1f))}, {Literal(rad * Mathf.Cos(u + 1f))})");
            float s = Mathf.Clamp((FineFrom - length) / (FineFrom - FineFull), 0f, 1f);
            float fineRad = s * Mathf.Min(sea.BendRadians, Mathf.Tau / length * FineMetres);
            float v = u + 2f;
            fine.Add($"vec4({Literal(s * FineGain * Mathf.Cos(v))}, {Literal(s * FineGain * Mathf.Sin(v))}, {Literal(fineRad * Mathf.Sin(v + 1f))}, {Literal(fineRad * Mathf.Cos(v + 1f))})");
            float ang = Mathf.DegToRad(sea.WindDeg + w.AngleDeg);
            float k = Mathf.Tau / length;
            float amp = w.Steepness / k;
            float omega = Mathf.Tau * SwellCyclesPerWrap(length) / (float)ShaderTime.RolloverSecs;
            float q = sea.Sharpness / (k * amp * waves.Length);
            a.Add($"vec4({Literal(Mathf.Cos(ang))}, {Literal(Mathf.Sin(ang))}, {Literal(k)}, {Literal(amp)})");
            b.Add($"vec2({Literal(q)}, {Literal(omega)})");
        }
        sb.AppendLine($"const vec4 {name}_DKA[{waves.Length}] = vec4[{waves.Length}]({string.Join(", ", a)});");
        sb.AppendLine($"const vec2 {name}_QW[{waves.Length}] = vec2[{waves.Length}]({string.Join(", ", b)});");
        sb.AppendLine($"const vec4 {name}_MOD[{waves.Length}] = vec4[{waves.Length}]({string.Join(", ", m)});");
        sb.AppendLine($"const vec4 {name}_FINE[{waves.Length}] = vec4[{waves.Length}]({string.Join(", ", fine)});");
    }

    // The view-space scale that draws the ocean at priority level, in the bias shader's own step
    // (SceneBuilder.DepthBiasPerLevel), spelled as a shader literal.
    private static string DepthScale(int level) =>
        (1.0 - (level * (double)SceneBuilder.DepthBiasPerLevel)).ToString("0.0######", CultureInfo.InvariantCulture);

    // The open sea's colour: the texture's mean, times the sea's tint when it has one. An untinted
    // sea writes no tint term, so its text matches the defaults'.
    private static string OpenSeaColour(SeaState sea) =>
        "textureLod(albedo_tex, vec2(0.5), 16.0).rgb"
        + (sea.Tinted ? $" * vec3({Literal(sea.TintR)}, {Literal(sea.TintG)}, {Literal(sea.TintB)})" : "");

    private static string ShaderCode(SeaState sea, int level, bool zoned)
    {
        // The Jacobian's standard deviation below 1 at full swell. Each wave's crest bunches the
        // surface by Q k A = Sharpness / waves, so the share holds with the wave count.
        float jacSigma = sea.Sharpness / Mathf.Sqrt(2f * Swell.Length);
        float foamFull = sea.FoamThreshold + FoamGateWidth;
        var sb = new StringBuilder();
        sb.AppendLine("shader_type spatial;");
        sb.AppendLine("render_mode skip_vertex_transform, cull_disabled;");
        sb.AppendLine(SceneBuilder.TimeInclude);
        sb.AppendLine(SceneBuilder.AtmosphereInclude);
        sb.AppendLine(SceneBuilder.MipBiasInclude);
        sb.AppendLine("uniform sampler2D mask_tex : filter_linear, repeat_disable;");
        sb.AppendLine("uniform vec4 mask_rect;");
        sb.AppendLine("uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;");
        sb.AppendLine("uniform float tile_m = 32.0;");
        sb.AppendLine("uniform sampler2D tint_tex : source_color, filter_linear, repeat_disable;");
        sb.AppendLine("uniform sampler2D lift_tex : hint_default_black, filter_linear, repeat_disable;");
        sb.AppendLine("uniform float rough_near = 0.2;");
        sb.AppendLine("uniform float rough_far = 0.3;");
        sb.AppendLine("uniform float wave_scale = 1.0;");
        sb.AppendLine("uniform float foam_strength = 0.12;");
        // Per ship zone: centre X/Z and heading axis, then the half length, half width, margin, fade.
        sb.AppendLine($"uniform vec4 calm_axes[{MaxShips}];");
        sb.AppendLine($"uniform vec4 calm_sizes[{MaxShips}];");
        // Per ship slot, the wake's track (OceanWakeTrack.Write) and its bounds; a zero reach draws none.
        sb.AppendLine($"uniform vec4 wake_track[{MaxShips * WakeSamples}];");
        sb.AppendLine($"uniform vec4 wake_bounds[{MaxShips}];");
        // A sheet split over zone layers gets one grid per group, each keeping only its own texels.
        if (zoned)
        {
            sb.AppendLine("uniform sampler2D zone_tex : filter_nearest, repeat_disable;");
            sb.AppendLine("instance uniform float zone_index = 0.0;");
        }
        EmitWaves(sb, "SWELL", Swell, sea);
        EmitDetail(sb, sea);
        sb.AppendLine($"const float FOAM_JAC_SIGMA = {Literal(jacSigma)};");
        sb.AppendLine("varying vec2 v_param;");
        sb.AppendLine(@"
vec2 ocean_mask(vec2 p) {
    return textureLod(mask_tex, (p - mask_rect.xy) * mask_rect.zw, 0.0).rg;
}

// The swell height and the look left at the mask's shore distance g.
float shore_swell(float g) {
    return smoothstep(" + Literal(sea.SwellFrom / OceanMaskRaster.ShoreReach) + @", " + Literal(sea.SwellFull / OceanMaskRaster.ShoreReach) + @", g);
}

float shore_look(float g) {
    return smoothstep(" + Literal(sea.LookFrom / OceanMaskRaster.ShoreReach) + @", " + Literal(sea.LookFull / OceanMaskRaster.ShoreReach) + @", g);
}

// Two hashes in [0, 1] per integer cell (a PCG2D mix). It hashes the cell's integer coordinates
// exactly, so every cell sharing a corner hashes it alike. A float hash of a large coordinate rounds
// differently per call site and cuts the noise along straight lattice lines.
vec2 sea_hash(vec2 c) {
    uvec2 v = uvec2(ivec2(c)) * 1664525u + 1013904223u;
    v.x += v.y * 1664525u;
    v.y += v.x * 1664525u;
    v ^= v >> 16u;
    v.x += v.y * 1664525u;
    v.y += v.x * 1664525u;
    v ^= v >> 16u;
    return vec2(v) * (1.0 / 4294967295.0);
}

// Two smooth value-noise channels in [-1, 1].
vec2 sea_noise(vec2 p) {
    vec2 c = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(sea_hash(c), sea_hash(c + vec2(1.0, 0.0)), f.x),
        mix(sea_hash(c + vec2(0.0, 1.0)), sea_hash(c + vec2(1.0, 1.0)), f.x), f.y) * 2.0 - 1.0;
}

// sea_noise with its slope: d's columns are the two channels' change along x and along y.
vec2 sea_noise_d(vec2 p, out mat2 d) {
    vec2 c = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    vec2 du = 6.0 * f * (1.0 - f);
    vec2 a = sea_hash(c);
    vec2 k1 = sea_hash(c + vec2(1.0, 0.0)) - a;
    vec2 k2 = sea_hash(c + vec2(0.0, 1.0)) - a;
    vec2 k3 = sea_hash(c + vec2(1.0, 1.0)) - a - k1 - k2;
    d = mat2(2.0 * du.x * (k1 + k3 * u.y), 2.0 * du.y * (k2 + k3 * u.x));
    return (a + k1 * u.x + k2 * u.y + k3 * (u.x * u.y)) * 2.0 - 1.0;
}

// The grouping channels at p. Each field is turned off the grid's axes so the noise cells do not
// line up with each other.
vec2 sea_group(vec2 p) {
    return sea_noise(mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8)) * p * " + Literal(1f / GroupScale) + @");
}

// The phase (warp) channels at p in xy, and its fine octave alone in zw. The out matrix is the
// slope of xy per metre. A wave's local wavenumber is its own plus its phase weights times it.
vec4 sea_warp(vec2 p, out mat2 d) {
    const mat2 rc = mat2(vec2(0.6, -0.8), vec2(0.8, 0.6));
    const mat2 rf = mat2(vec2(0.28, 0.96), vec2(-0.96, 0.28));
    mat2 dc;
    mat2 df;
    vec2 c = sea_noise_d(rc * p * " + Literal(1f / PhaseScaleCoarse) + @" + vec2(17.0, 5.0), dc);
    vec2 f = sea_noise_d(rf * p * " + Literal(1f / PhaseScaleFine) + @" + vec2(-9.0, 31.0), df);
    d = dc * rc * " + Literal((1f - PhaseFineShare) / PhaseScaleCoarse) + @" + df * rf * " + Literal(PhaseFineShare / PhaseScaleFine) + @";
    return vec4(c * " + Literal(1f - PhaseFineShare) + @" + f * " + Literal(PhaseFineShare) + @", f);
}

// A wave's local wavenumber vector: its own plus the warp's slope along its phase weights. Its
// length is capped at WarpStretchMax times the wave's own, so the fades keep their order.
vec2 sea_wavevector(vec4 w, vec4 coarse_w, mat2 d) {
    vec2 k = w.z * w.xy + coarse_w.zw * d;
    return k * max(1.0, w.z / (" + Literal(WarpStretchMax) + @" * max(length(k), 1e-4)));
}

// The fine field at p: xy its grouping channels, zw its phase channels.
vec4 sea_fine(vec2 p) {
    vec2 g = sea_noise(mat2(vec2(0.96, -0.28), vec2(0.28, 0.96)) * p * " + Literal(1f / FineGroupScale) + @" + vec2(41.0, -23.0));
    vec2 f = sea_noise(mat2(vec2(-0.6, 0.8), vec2(-0.8, -0.6)) * p * " + Literal(1f / FinePhaseScale) + @" + vec2(-57.0, 13.0));
    return vec4(g, f);
}

// Two gradients in [-1, 1] per integer cell. The cell's x is taken modulo DetailPeriod, so a
// layer's noise repeats along its drift axis and its drift wraps with csky_time.
vec2 detail_hash(vec2 c) {
    return sea_hash(vec2(mod(c.x, " + DetailPeriod.ToString(CultureInfo.InvariantCulture) + @".0), c.y)) * 2.0 - 1.0;
}

// Gradient noise at q: x the value, yz its slope along q's axes.
vec3 detail_noise(vec2 q) {
    vec2 i = floor(q);
    vec2 f = fract(q);
    vec2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    vec2 du = 30.0 * f * f * (f * (f - 2.0) + 1.0);
    vec2 ga = detail_hash(i);
    vec2 gb = detail_hash(i + vec2(1.0, 0.0));
    vec2 gc = detail_hash(i + vec2(0.0, 1.0));
    vec2 gd = detail_hash(i + vec2(1.0, 1.0));
    float va = dot(ga, f);
    float vb = dot(gb, f - vec2(1.0, 0.0));
    float vc = dot(gc, f - vec2(0.0, 1.0));
    float vd = dot(gd, f - vec2(1.0, 1.0));
    float k = va - vb - vc + vd;
    return vec3(va + u.x * (vb - va) + u.y * (vc - va) + u.x * u.y * k,
        ga + u.x * (gb - ga) + u.y * (gc - ga) + u.x * u.y * (ga - gb - gc + gd) + du * (u.yx * k + vec2(vb, vc) - va));
}

// Gradient noise at q, about -0.7 to 0.7, smooth across its cells. The cell's x repeats every
// period cells. The half cell keeps the modulo exact, so no corner hashes differently in two cells.
float foam_noise(vec2 q, float period) {
    vec2 i = floor(q);
    vec2 f = fract(q);
    vec2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    vec2 x = vec2(i.x, i.x + 1.0);
    x -= period * floor((x + 0.5) / period);
    float a = dot(sea_hash(vec2(x.x, i.y)) * 2.0 - 1.0, f);
    float b = dot(sea_hash(vec2(x.y, i.y)) * 2.0 - 1.0, f - vec2(1.0, 0.0));
    float c = dot(sea_hash(vec2(x.x, i.y + 1.0)) * 2.0 - 1.0, f - vec2(0.0, 1.0));
    float d = dot(sea_hash(vec2(x.y, i.y + 1.0)) * 2.0 - 1.0, f - vec2(1.0, 1.0));
    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

// The holes in a sheet of foam: one round hole per jittered cell, each its own size. Returns the
// distance from q to the nearest hole's centre in that hole's radii, so under 1 inside a hole.
float foam_holes(vec2 q) {
    vec2 i = floor(q);
    vec2 f = fract(q);
    float s = 9.0;
    for (int y = -1; y <= 1; y++) {
        for (int x = -1; x <= 1; x++) {
            vec2 o = vec2(float(x), float(y));
            vec2 h = sea_hash(i + o);
            vec2 r = o + h * 0.8 + 0.1 - f;
            s = min(s, length(r) / (0.35 + 0.4 * fract(h.x * 7.31 + h.y * 3.17)));
        }
    }
    return s;
}

// Where whitecaps can form, about 0 to 1, at pw (metres along and across the wind).
float foam_patches(vec2 pw) {
    return 0.5" + FoamPatchTerms(sea) + @";
}

// One wave's height factor and phase shift from its weights on both fields.
vec2 sea_mod(vec4 coarse_w, vec4 fine_w, vec2 group, vec2 warp, vec4 fine) {
    return vec2(clamp(1.0 + dot(coarse_w.xy, group) + dot(fine_w.xy, fine.xy), 0.0, 1.8),
        dot(coarse_w.zw, warp) + dot(fine_w.zw, fine.zw));
}

// OceanCalmZone.Distance and Calm, per ship: flat within the margin of the box, full height a fade
// further out.
float ship_calm(vec2 p) {
    float a = 1.0;
    for (int i = 0; i < " + MaxShips.ToString(CultureInfo.InvariantCulture) + @"; i++) {
        vec2 q = p - calm_axes[i].xy;
        vec2 u = calm_axes[i].zw;
        vec2 e = max(abs(vec2(dot(q, u), dot(q, vec2(-u.y, u.x)))) - calm_sizes[i].xy, vec2(0.0));
        a = min(a, smoothstep(calm_sizes[i].z, calm_sizes[i].z + calm_sizes[i].w, length(e)));
    }
    return a;
}

// A wake's foam at p, about 0 to 1, where the trail's density is cover. The water's age churns the
// lumps and holes, and the foam thins as it ages. Read spread metres nearer the track, the pattern
// widens with the trail. Up close it breaks into the whitecaps' lace.
float wake_foam(vec2 p, vec2 spread, float age, float cover, float cell, float footprint) {
    vec2 q = (p - spread) / cell;
    float churn = age * " + Literal(WakeFoamChurn) + @";
    vec2 w = vec2(foam_noise(q * 0.5 + vec2(churn, 3.0), 65536.0), foam_noise(q * 0.5 + vec2(-7.0, -churn), 65536.0));
    float lumps = 0.5 + foam_noise(q + 1.6 * w, 65536.0) + 0.6 * foam_noise(q * 2.7 + 2.4 * w + vec2(13.0, 7.0), 65536.0);
    float fresh = exp(-age / " + Literal(WakeFoamFreshSecs) + @");
    float c = cover * mix(" + Literal(WakeFoamAged) + @", " + Literal(WakeFoamFresh) + @", fresh);
    float foam = smoothstep(0.75 - c, 1.05 - 0.6 * c, lumps) * min(c * 3.0, 1.0);
    float near = smoothstep(" + Literal(FootprintFadeFrom) + @", " + Literal(FootprintFadeFull) + @", " + Literal(FoamCellFine) + @" / footprint);
    if (near > 0.0 && foam > 0.0) {
        float s = foam_holes((p - spread) * " + Literal(1f / FoamCellFine) + @" + w * 2.0 + vec2(31.0, -17.0));
        float hole = 1.3 - 1.6 * foam;
        float edge = max(0.04, " + Literal(2f / FoamCellFine) + @" * footprint);
        foam = mix(foam, smoothstep(hole - edge, hole + edge + 0.35, s), near);
    }
    return foam * mix(" + Literal(WakeFoamAged) + @", 1.0, fresh);
}

// The ships' Kelvin wakes and foam trails at p: x the height, yz its slope, w the foam. The foam is
// drawn for a pixel of footprint metres, none at 0. A wave under fade.x metres long is left out, one over fade.y
// kept. The nearest point on a track gives the metres behind the bow, the side, age and speed.
vec4 ship_wake(vec2 p, vec2 fade, float footprint) {
    vec4 sum = vec4(0.0);
    for (int i = 0; i < " + MaxShips.ToString(CultureInfo.InvariantCulture) + @"; i++) {
        vec4 bounds = wake_bounds[i];
        vec2 o = p - bounds.xy;
        if (dot(o, o) >= bounds.z * bounds.z) {
            continue;
        }
        float best = 1e20;
        float s = 0.0;
        float side = 0.0;
        float age = 0.0;
        int at = 0;
        float at_t = 0.0;
        vec2 back = vec2(1.0, 0.0);
        vec2 half_size = calm_sizes[i].xy;
        int base = i * " + WakeSamples.ToString(CultureInfo.InvariantCulture) + @";
        for (int k = 0; k < " + (WakeSamples - 1).ToString(CultureInfo.InvariantCulture) + @"; k++) {
            vec4 a = wake_track[base + k];
            vec4 c = wake_track[base + k + 1];
            vec2 seg = c.xy - a.xy;
            float l2 = dot(seg, seg);
            if (l2 < 1e-4) {
                continue;
            }
            float ahead = k == 0 ? -(half_size.x + " + Literal(WakeAheadMargin) + @") * inversesqrt(l2) : 0.0;
            float t = clamp(dot(p - a.xy, seg) / l2, ahead, 1.0);
            vec2 q = a.xy + seg * t;
            float d2 = dot(p - q, p - q);
            if (d2 < best) {
                best = d2;
                back = seg * inversesqrt(l2);
                s = mix(a.z, c.z, t);
                side = dot(p - q, vec2(-back.y, back.x));
                age = mix(a.w, c.w, max(t, 0.0));
                at = k;
                at_t = max(t, 0.0);
            }
        }
        // The speed runs on across segments: the chord speed over this one and the one behind, blended
        // into this one and the one ahead. A step would step the waves' length, a seam.
        vec4 e0 = wake_track[base + max(at - 1, 0)];
        vec4 e1 = wake_track[base + at];
        vec4 e2 = wake_track[base + at + 1];
        vec4 e3 = wake_track[base + min(at + 2, " + (WakeSamples - 1).ToString(CultureInfo.InvariantCulture) + @")];
        float speed = mix((e2.z - e0.z) / max(e2.w - e0.w, 1e-3), (e3.z - e1.z) / max(e3.w - e1.w, 1e-3), at_t);
        float x = s + half_size.x;
        float life = 1.0 - clamp(age / " + Literal(OceanWakeTrack.LifeSecs) + @", 0.0, 1.0);
        float gain = smoothstep(" + Literal(WakeMinSpeed) + @", " + Literal(3f * WakeMinSpeed) + @", speed)
            * min(speed / " + Literal(WakeRefSpeed) + @", 1.0) * life * life;
        if (gain <= 0.0 || x <= 0.0) {
            continue;
        }
        float hull = max(2.0 * half_size.x, " + Literal(WakeMinHull) + @");
        float scale = sqrt(hull / " + Literal(WakeRefHull) + @");
        // The field that breaks the crests into groups, fixed on the water: x scales a crest's height,
        // y shifts its phase. Its cells scale with the hull, so a small hull's V breaks up as well.
        float group_cell = max(" + Literal(WakeGroupCell) + @" * scale, " + Literal(WakeGroupCellMin) + @");
        vec2 grp = 0.65 * sea_noise(p / group_cell + vec2(23.0, -41.0))
            + 0.35 * sea_noise(p * 2.7 / group_cell + vec2(-5.0, 19.0));
        // The transverse waves fall off as the inverse of the distance behind the bow, the diverging
        // ones slower. Both calm with the water's age.
        float reach = " + Literal(WakeVReach) + @" * scale;
        float near_bow = reach / (reach + x);
        float calm = exp(-age / " + Literal(WakeCalmSecs) + @");
        float height = " + Literal(WakeHeight) + @" * min(sqrt(scale), 1.0) * gain * max(1.0 + " + Literal(WakeGroupGain) + @" * grp.x, 0.0);
        // A finer field warps each crest's phase, so the spacing varies from crest to crest, and breaks
        // each crest into separate lengths along its line.
        vec2 jit = sea_noise(p / max(" + Literal(WakeJitterCell) + @" * scale, " + Literal(WakeJitterCellMin) + @") + vec2(-37.0, 11.0));
        float shift = " + Literal(WakeGroupPhase) + @" * grp.y + " + Literal(WakeCrestJitter) + @" * jit.x;
        float unbroken = smoothstep(-" + Literal(WakeCrestBreak) + @", " + Literal(WakeCrestBreak) + @", jit.y + " + Literal(WakeCrestKeep) + @");
        float ay = abs(side);
        float sy = side < 0.0 ? -1.0 : 1.0;
        // Kelvin's transverse and diverging directions here, as tan(theta): the roots t of
        // tan(alpha) = t / (1 + 2 t^2). Alpha is the point's angle off the track, real under 19.47 degrees.
        float ta = ay / x;
        float disc = 1.0 - 8.0 * ta * ta;
        float edge = smoothstep(0.0, " + Literal(WakeEdge) + @", disc);
        float k0 = 9.81 / (max(speed * speed, " + Literal(WakeMinSpeed * WakeMinSpeed) + @") * " + Literal(WakeLengthScale) + @");
        // Foam along the cusp lines, the V's arms, where both families meet. It does not follow the crests,
        // which would lay it in an evenly spaced row; the trail's noise breaks it up instead.
        float arm = 1.0 - smoothstep(0.0, " + Literal(WakeCuspFoamBand) + @", abs(disc));
        float arm_foam = gain * arm * " + Literal(WakeCuspFoamCover) + @" * smoothstep(-0.3, 0.3, grp.x) * sqrt(calm)
            * exp(-x / (" + Literal(WakeCuspFoamReach) + @" * hull));
        vec2 grad = vec2(0.0);
        if (" + (WakeWaves ? "edge > 0.0" : "false") + @") {
            float sq = sqrt(max(disc, 0.0));
            float tt = 2.0 * ta / (1.0 + sq);
            float td = (1.0 + sq) / max(4.0 * ta, 1e-3);
            float cusp = 1.0 + " + Literal(WakeCusp) + @" * (1.0 - smoothstep(0.0, " + Literal(WakeCuspWidth) + @", disc));
            vec2 tans = vec2(tt, td);
            vec2 shares = vec2(" + Literal(WakeTransverse) + @" * near_bow * calm, " + Literal(WakeDiverging) + @" * cusp * sqrt(near_bow * calm));
            for (int f = 0; f < 2; f++) {
                float tn = tans[f];
                float sec2 = 1.0 + tn * tn;
                float kn = k0 * sec2;
                // The diverging waves shorten toward the track and fade past the cusp's wavenumber. The
                // V's middle then carries the transverse waves alone, not a fine grain.
                float keep = smoothstep(fade.x, fade.y, 6.2831853 / kn)
                    * (f == 0 ? 1.0 : 1.0 - smoothstep(1.5, " + Literal(WakeDivergingSec2) + @", sec2));
                if (keep <= 0.0) {
                    continue;
                }
                float sec = sqrt(sec2);
                float ph = k0 * x * sec * (1.0 + tn * ta) + shift;
                float amp = height * edge * shares[f] * keep * unbroken;
                sum.x += amp * cos(ph);
                // The phase's slope is the wavenumber vector, along the track and to the side.
                grad -= amp * sin(ph) * kn * vec2(1.0, tn * sy) / sec;
            }
        }
        vec2 across = vec2(-back.y, back.x);
        sum.yz += grad.x * back + grad.y * across;
        if (footprint > 0.0) {
            float behind = s - half_size.x;
            float width = half_size.y + max(behind, 0.0) * " + Literal(WakeFoamSpread) + @" * scale;
            float trail = (1.0 - smoothstep(" + Literal(WakeFoamCore) + @" * width, width, ay))
                * smoothstep(-" + Literal(WakeFoamRamp) + @" * half_size.x, 0.0, behind) * gain * sqrt(life)
                * exp(-max(behind, 0.0) / (" + Literal(WakeFoamReach) + @" * scale));
            float cover = max(trail, arm_foam);
            if (cover > 0.0) {
                // The foam's age counts from the stern's passing, not the hull's middle.
                float churned = max(age - half_size.x / max(speed, " + Literal(WakeMinSpeed) + @"), 0.0);
                sum.w = max(sum.w, wake_foam(p, across * side * (1.0 - half_size.y / width), churned, cover, max(" + Literal(WakeFoamCell) + @" * scale, " + Literal(WakeFoamCellMin) + @"), footprint));
            }
        }
    }
    return sum;
}

void vertex() {
    // A spyglass disc shows the flat sea. Every vertex goes to one point behind the eye, so the
    // clipper drops each triangle before a fragment and no wave is summed.
    if (" + SceneBuilder.FlatSeaEye + @") {
        VERTEX = vec3(0.0, 0.0, 1.0);
        v_param = vec2(0.0);
    } else {
        vec2 p = VERTEX.xz + CAMERA_POSITION_WORLD.xz;
        float spacing = max(UV.x, 0.01);
        float shore = shore_swell(ocean_mask(p).g);
        float amp = shore * ship_calm(p) * wave_scale;
        // The wakes ride on the swell whatever the sea's height. The hulls calm only the swell, so
        // a wake stands tallest beside its own hull.
        vec3 disp = vec3(0.0, shore * ship_wake(p, vec2(4.0, 8.0) * spacing, 0.0).x, 0.0);
        vec2 group = sea_group(p);
        mat2 dwarp;
        vec4 warp = sea_warp(p, dwarp);
        vec4 fine = sea_fine(p);
        for (int i = 0; i < SWELL_DKA.length(); i++) {
            vec4 w = SWELL_DKA[i];
            float len = 6.2831853 / length(sea_wavevector(w, SWELL_MOD[i], dwarp));
            vec2 sm = sea_mod(SWELL_MOD[i], SWELL_FINE[i], group, warp.xy, fine);
            // Full height at eight grid steps per local wavelength, none at four. A wave the grid
            // cannot carry would crawl as the grid follows the eye.
            float g = amp * clamp(len / spacing * 0.25 - 1.0, 0.0, 1.0) * sm.x;
            float th = w.z * dot(w.xy, p) - SWELL_QW[i].y * csky_time + sm.y;
            disp.xz += w.xy * (SWELL_QW[i].x * w.w * g * cos(th));
            disp.y += w.w * g * sin(th);
        }
        v_param = p;
        // Over a harbour ramp the sea rises with the sheet it replaces, up to the quay it meets.
        float lift = textureLod(lift_tex, (p - mask_rect.xy) * mask_rect.zw, 0.0).r * " + Literal(OceanMask.RampTop) + @";
        vec3 world = vec3(p.x + disp.x, disp.y + lift, p.y + disp.z);
        VERTEX = (VIEW_MATRIX * vec4(world, 1.0)).xyz;
        // A priority level below the lowest base sheet. Every coplanar layer over or beside it
        // stays on top, the surf ring and C5's fog-gradient passes included.
        VERTEX *= " + DepthScale(level) + @";
        NORMAL = (VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz;
    }
}

void fragment() {
    vec2 p = v_param;
    vec2 m = ocean_mask(p);
    vec3 world = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
    // One-sided like the sheet it replaces: an eye below the surface sees through it.
    if (m.r < " + Literal(OceanMask.SeaThreshold) + @" || CAMERA_POSITION_WORLD.y < world.y) {
        discard;
    }" + (zoned ? @"
    float zone = textureLod(zone_tex, (p - mask_rect.xy) * mask_rect.zw, 0.0).r * 255.0;
    if (zone < " + Literal(OceanMaskRaster.AnyZone - 0.5f) + @" && abs(zone - zone_index) > 0.5) {
        discard;
    }" : "") + @"
    // A ship's zone and the shore's swell ramp flatten only the geometry. The normals keep the
    // swell and the fine detail, so the water there shades like the open sea. Only the short look
    // ramp below calms them. A calm patch would read as an artefact.
    float look = shore_look(m.g);
    float amp = wave_scale;
    vec2 fw = fwidth(p);
    float footprint = max(max(fw.x, fw.y), 0.001);
    vec3 n = vec3(0.0, 1.0, 0.0);
    // The horizontal displacement's slope (columns: along x, along z), for the foam's Jacobian.
    // Summed from the fragment's own waves, not the grid's, so the foam reads the same at every
    // distance the waves are drawn.
    vec2 bx = vec2(0.0);
    vec2 bz = vec2(0.0);
    // The warp's slope is in each wave's normal, as the vertex stage's fade reads it. The grouping
    // and fine fields' slopes are left out: they bend a crest by a few per cent at most.
    vec2 group = sea_group(p);
    mat2 dwarp;
    vec4 warp = sea_warp(p, dwarp);
    vec4 fine = sea_fine(p);
    // The tables run longest first (EmitWaves checks), and no local length passes WarpStretchMax
    // times the wave's own. Once that bound fades out, every later wave fades too.
    for (int i = 0; i < SWELL_DKA.length(); i++) {
        vec4 w = SWELL_DKA[i];
        float len = 6.2831853 / w.z;
        if (len * " + Literal(WarpStretchMax) + @" <= footprint * " + Literal(FootprintFadeFrom) + @") {
            break;
        }
        vec2 kv = sea_wavevector(w, SWELL_MOD[i], dwarp);
        float fade = smoothstep(" + Literal(FootprintFadeFrom) + @", " + Literal(FootprintFadeFull) + @", 6.2831853 / length(kv) / footprint);
        vec2 sm = sea_mod(SWELL_MOD[i], SWELL_FINE[i], group, warp.xy, fine);
        float f = amp * fade * sm.x;
        float th = w.z * dot(w.xy, p) - SWELL_QW[i].y * csky_time + sm.y;
        float ka = w.z * w.w * f;
        float s = sin(th);
        n.xz -= kv * (w.w * f * cos(th));
        n.y -= SWELL_QW[i].x * ka * s;
        float qs = SWELL_QW[i].x * w.w * f * s;
        bx += w.xy * (kv.x * qs);
        bz += w.xy * (kv.y * qs);
    }
    // The detail layers run longest first (EmitDetail checks), so the first faded layer ends the sum.
    // A layer's height is DetailSlope times its cell times the noise, the same slope at every cell
    // size. The sea-state grouping varies it by region.
    for (int i = 0; i < DETAIL_L.length(); i++) {
        vec4 l = DETAIL_L[i];
        float fade = smoothstep(" + Literal(FootprintFadeFrom) + @", " + Literal(FootprintFadeFull) + @", " + Literal(DetailWaveCells) + @" / l.z / footprint);
        if (fade <= 0.0) {
            break;
        }
        vec2 side = vec2(-l.y, l.x);
        vec2 q = vec2(dot(p, l.xy), dot(p, side)) * l.z - vec2(l.w * csky_time, 0.0);
        vec3 nd = detail_noise(q);
        float f = amp * fade * " + Literal(sea.DetailSlope) + @" * clamp(1.0 + dot(DETAIL_G[i], group), 0.2, 1.8);
        n.xz -= (l.xy * nd.y + side * nd.z) * f;
    }
    vec4 wake = ship_wake(p, vec2(" + Literal(FootprintFadeFrom) + @", " + Literal(FootprintFadeFull) + @") * footprint, footprint);
    n.xz -= wake.yz * " + Literal(WakeSlopeGain) + @";
    float fog_amt = csky_fog_amount(world, CAMERA_POSITION_WORLD);
    // How fully this fragment shades as the flat sheet does. Wholly at the shore, so no edge shows
    // against a coplanar coast tile. Rising with the fog, so the far sea fogs as the sheet does
    // instead of showing darker, wavier water through it.
    float sheet = max(1.0 - look, smoothstep(0.0, " + Literal(FogSheetAt) + @", fog_amt));
    n = normalize(mix(normalize(n), vec3(0.0, 1.0, 0.0), sheet));
    NORMAL = normalize((VIEW_MATRIX * vec4(n, 0.0)).xyz);
    float dist = distance(world.xz, CAMERA_POSITION_WORLD.xz);
    vec3 tint = texture(tint_tex, (p - mask_rect.xy) * mask_rect.zw).rgb;
    // The open sea takes the texture's mean and the light alone varies it: a tiled texture reads as
    // a lattice from the air. The last mip is the mean the GPU samples. At sheet = 1 the texture is
    // the sheet's own mapping, so no seam shows where the ocean meets a coast tile.
    vec3 tex_mean = " + OpenSeaColour(sea) + @";
    vec3 col = mix(tex_mean, csky_sample_albedo(albedo_tex, p / tile_m).rgb, sheet) * tint;
    // Foam forms where the swell bunches the surface, its Jacobian below 1, which rides each crest.
    // A drifting patch field decides where whitecaps can form and moves each crest's threshold.
    float bunch = (1.0 - ((1.0 - bx.x) * (1.0 - bz.y) - bz.x * bx.y)) / FOAM_JAC_SIGMA;
    float foam = 0.0;
    if (bunch > " + Literal(sea.FoamThreshold - (0.75f * FoamPatchShift)) + @") {
        vec2 wind = SWELL_DKA[0].xy;
        vec2 pw = vec2(dot(p, wind), dot(p, vec2(-wind.y, wind.x)));
        float patches = foam_patches(pw);
        float crest = smoothstep(" + Literal(sea.FoamThreshold) + @", " + Literal(foamFull) + @", bunch + (patches - 0.5) * " + Literal(FoamPatchShift) + @")
            * smoothstep(0.5, 0.75, patches);
        crest *= smoothstep(0.42, 0.62, 0.5 + foam_noise(pw * " + Literal(1f / FoamBreakupCell) + @", 65536.0));
        foam = crest;
        // Up close a whitecap breaks into patches whose mean is the crest's foam. They fade in with
        // the footprint as the detail layers do; their edge widens with it, so none shimmers.
        float near = smoothstep(" + Literal(FootprintFadeFrom) + @", " + Literal(FootprintFadeFull) + @", " + Literal(FoamCellFine) + @" / footprint);
        if (near > 0.0) {
            vec2 q = pw * vec2(" + Literal(1f / (FoamCellCoarse * FoamStretch)) + @", " + Literal(1f / FoamCellCoarse) + @");
            vec2 warp = vec2(foam_noise(q, 65536.0), foam_noise(q + vec2(17.0, 9.0), 65536.0));
            float cover = " + Literal(sea.FoamCover) + @" * crest * clamp(1.0 + 2.5 * warp.x, 0.0, 2.0);
            float s = foam_holes(q * " + Literal(FoamCellCoarse / FoamCellFine) + @" + warp * 1.2 + vec2(31.0, -17.0));
            float hole = 1.3 - 1.6 * cover;
            float edge = max(0.04, " + Literal(2f / FoamCellFine) + @" * footprint);
            float lump = smoothstep(hole - edge, hole + edge + 0.35, s) * min(cover * 20.0, 1.0);
            foam = mix(crest, lump * " + Literal(1f / sea.FoamCover) + @", near);
        }
    }
    float wake_foam = " + Literal(WakeFoamWhite) + @" * wake.w;
    float foam_mix = max(min(foam * foam_strength, 1.0), wake_foam) * (1.0 - sheet);
    col = mix(col, vec3(0.6, 0.65, 0.68), foam_mix);
    ALBEDO = col;
    METALLIC = 0.0;
    SPECULAR = " + Literal(SceneBuilder.WaterSpecular) + @";
    // Lost slope detail turns into roughness, so the far sea keeps a glossy sheen, not a mirror.
    // Foam is matte where it covers the water.
    ROUGHNESS = mix(mix(mix(rough_near, rough_far, smoothstep(150.0, 4000.0, dist)), " + Literal(SceneBuilder.WaterRoughness) + @", sheet), " + Literal(FoamRoughness) + @", foam_mix);
    FOG = vec4(csky_fog_color_at(CAMERA_POSITION_WORLD), fog_amt);
}");
        return sb.ToString();
    }

    // One Gerstner component: direction, wavelength in metres, steepness k*A.
    private readonly record struct Wave(float AngleDeg, float Length, float Steepness);

    // One fine-detail noise layer: drift direction off the wind, cell size in metres, drift in m/s.
    private readonly record struct Detail(float AngleDeg, float Cell, float Speed);

    // One octave of the foam's patch field: cell size in metres, weight, drift along the wind in m/s.
    private readonly record struct FoamPatch(float Cell, float Weight, float Speed);
}
