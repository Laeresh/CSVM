using System;

namespace CSVM.Mech3;

/// <summary>Where a painted-coast texture crosses from water to land, read off its authored pixels
/// along the one UV axis they vary along. Engine-free; the rules and the figures they reproduce are
/// in analysis/painted-coasts/FINDINGS.md.</summary>
/// <param name="AlongV">True when V runs across the coast, false for U.</param>
/// <param name="WaterAtOne">True when the water end is coordinate 1 of that axis, false for 0.</param>
/// <param name="Crossing">The coordinate along the axis at which the water meets the land.</param>
/// <param name="Fade">The width along the axis from the profile's 10 % point to its 90 % point.</param>
public readonly record struct CoastProfile(bool AlongV, bool WaterAtOne, float Crossing, float Fade)
{
    // Texels averaged at each end of the profile, so one noisy texel cannot pick the water end.
    private const int EndTexels = 8;

    // Texels in the box filter over the profile, so texel noise cannot place a crossing.
    private const int SmoothTexels = 5;

    /// <summary>The profile of <paramref name="texture"/> against the base sheet's mean colour
    /// <paramref name="water"/>. An alpha strip crosses where mean alpha passes 0.5 at its water end,
    /// the transparent end nearer <paramref name="water"/>. An opaque ramp's water end is the end
    /// nearer it; it crosses at the 10 % point of its profile normalised to its own ends.
    /// Null without a transparent end, or for an opaque texture whose ends match.</summary>
    public static CoastProfile? Of(TextureImage texture, (float R, float G, float B) water)
    {
        bool alpha = false;
        if (texture.HasAlphaChannel)
        {
            for (int i = 3; i < texture.Pixels.Length && !alpha; i += 4)
                alpha = texture.Pixels[i] < 128;
        }
        var cols = Means(texture, alongV: false);
        var rows = Means(texture, alongV: true);
        bool alongV = Variance(rows, alpha) > Variance(cols, alpha);
        var line = alongV ? rows : cols;
        int n = line.Length;
        int e = Math.Clamp(n / 4, 1, EndTexels);
        double[] start = EndMean(line, 0, e), end = EndMean(line, n - e, e);
        double startDist = Distance(start, water), endDist = Distance(end, water);

        bool waterAtOne;
        var t = new double[n];
        if (alpha)
        {
            bool startClear = start[3] < 127.5, endClear = end[3] < 127.5;
            if (!startClear && !endClear)
                return null;
            waterAtOne = endClear && (!startClear || endDist < startDist);
            for (int k = 0; k < n; k++)
                t[k] = line[waterAtOne ? n - 1 - k : k][3] / 255.0;
        }
        else
        {
            waterAtOne = endDist < startDist;
            for (int k = 0; k < n; k++)
                t[k] = Distance(line[waterAtOne ? n - 1 - k : k], water);
            double dw = 0, dl = 0;
            for (int k = 0; k < e; k++)
            {
                dw += t[k] / e;
                dl += t[n - 1 - k] / e;
            }
            if (Math.Abs(dl - dw) < 1.0)
                return null;
            for (int k = 0; k < n; k++)
                t[k] = (t[k] - dw) / (dl - dw);
        }

        var s = Smoothed(t);
        // An alpha strip fading at both edges rises to its opaque middle; only that water-side rise is read.
        int last = n - 1;
        if (alpha)
        {
            last = 0;
            for (int k = 1; k < n; k++)
                if (s[k] > s[last])
                    last = k;
        }
        if (Reach(s, 0.1, last) is not { } c10 || Reach(s, 0.5, last) is not { } c50)
            return null;
        double c90 = Reach(s, 0.9, last) ?? last;
        double cross = alpha ? c50 : c10;
        double coord = (cross + 0.5) / n;
        return new CoastProfile(alongV, waterAtOne, (float)(waterAtOne ? 1 - coord : coord), (float)((c90 - c10) / n));
    }

    /// <summary>The mean RGB of a texture's texels, the base sheet's colour the profile is read
    /// against.</summary>
    public static (float R, float G, float B) MeanColour(TextureImage texture)
    {
        int ch = texture.HasAlphaChannel ? 4 : 3;
        double r = 0, g = 0, b = 0;
        int count = texture.Pixels.Length / ch;
        for (int i = 0; i + 2 < texture.Pixels.Length; i += ch)
        {
            r += texture.Pixels[i];
            g += texture.Pixels[i + 1];
            b += texture.Pixels[i + 2];
        }
        return count == 0 ? (0f, 0f, 0f) : ((float)(r / count), (float)(g / count), (float)(b / count));
    }

    // The mean RGBA of each column (U) or row (V), alpha 255 where the texture has none.
    private static double[][] Means(TextureImage texture, bool alongV)
    {
        int w = texture.Width, h = texture.Height, ch = texture.HasAlphaChannel ? 4 : 3;
        int n = alongV ? h : w;
        double across = alongV ? w : h;
        var means = new double[n][];
        for (int k = 0; k < n; k++)
            means[k] = new double[4];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = ((y * w) + x) * ch;
                var m = means[alongV ? y : x];
                for (int c = 0; c < 4; c++)
                    m[c] += (c < ch ? texture.Pixels[i + c] : 255) / across;
            }
        }
        return means;
    }

    // How much a profile varies along its axis: the alpha's variance, or the RGB channels' summed.
    private static double Variance(double[][] line, bool alpha)
    {
        double total = 0;
        for (int c = alpha ? 3 : 0; c < (alpha ? 4 : 3); c++)
        {
            double mean = 0, sq = 0;
            foreach (var m in line)
                mean += m[c] / line.Length;
            foreach (var m in line)
                sq += (m[c] - mean) * (m[c] - mean) / line.Length;
            total += sq;
        }
        return total;
    }

    private static double[] EndMean(double[][] line, int from, int count)
    {
        var mean = new double[4];
        for (int k = from; k < from + count; k++)
        {
            for (int c = 0; c < 4; c++)
                mean[c] += line[k][c] / count;
        }
        return mean;
    }

    private static double Distance(double[] rgb, (float R, float G, float B) water)
    {
        double r = rgb[0] - water.R, g = rgb[1] - water.G, b = rgb[2] - water.B;
        return Math.Sqrt((r * r) + (g * g) + (b * b));
    }

    // A box filter clamped at the ends, as the analysis scripts smooth their profiles.
    private static double[] Smoothed(double[] t)
    {
        int n = t.Length, half = SmoothTexels / 2;
        var s = new double[n];
        for (int k = 0; k < n; k++)
        {
            for (int j = -half; j <= half; j++)
                s[k] += t[Math.Clamp(k + j, 0, n - 1)] / SmoothTexels;
        }
        return s;
    }

    // The fractional texel index from the water end where the profile first reaches f, up to last.
    // It is interpolated between texel centres, and null when the profile never reaches f.
    private static double? Reach(double[] s, double f, int last)
    {
        for (int k = 0; k <= last; k++)
        {
            if (s[k] >= f)
                return k == 0 ? 0 : k - 1 + ((f - s[k - 1]) / (s[k] - s[k - 1]));
        }
        return null;
    }
}
