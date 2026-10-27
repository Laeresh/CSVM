using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Where forest mass is drawn under Rich, in square cells of <see cref="CellM"/> over the world node's area.
/// Each cell holds a canopy density from 0 to 1, the mean ground height under the canopy and the mean tree top.
/// Card forest bakes from the Rich stamps (<see cref="FromStamps"/>). Painted forest bakes from the ground
/// triangles picked as canopy (<see cref="FromPainted"/>). Cell 0 sits at the area's lowest X and Z, rows
/// along Z, as <see cref="VegetationMask"/>'s texels do. Original never bakes it; Mech3.md has the rules.
/// </summary>
public sealed class CanopyMask
{
    /// <summary>The cell edge in metres: three fir crowns across, 384 cells over a 12 km chapter.</summary>
    public const float CellM = 32f;

    /// <summary>The share of a cell under card crowns that counts as full forest. It is one fir crown (10 m
    /// across) per cell, the spacing C1's <c>terpat02</c> stamps its firs at.</summary>
    public const float FullCrownShare = 0.075f;

    /// <summary>The vegetation set a texture pick names to mark its ground as painted canopy. The set stays
    /// empty, so a pick to it stamps no card on the painted treetops.</summary>
    public const string CanopySet = "canopy";

    // Samples along each cell side for the painted bake, 8 m apart.
    private const int PaintSamples = 4;

    private readonly float[] _density;
    private readonly float[] _ground;
    private readonly float[] _top;

    // A painted bake's sample heights per covered cell, PaintSamples squared row by row, NaN off the canopy.
    private readonly Dictionary<int, float[]> _samples = new();

    private CanopyMask(float left, float top, float right, float bottom)
    {
        MinX = Math.Min(left, right);
        MinZ = Math.Min(top, bottom);
        Width = Math.Max(1, (int)MathF.Ceiling((Math.Max(left, right) - MinX) / CellM));
        Height = Math.Max(1, (int)MathF.Ceiling((Math.Max(top, bottom) - MinZ) / CellM));
        _density = new float[Width * Height];
        _ground = new float[Width * Height];
        _top = new float[Width * Height];
    }

    /// <summary>Cells along X.</summary>
    public int Width { get; }

    /// <summary>Cells along Z.</summary>
    public int Height { get; }

    /// <summary>The world X and Z of cell 0's lowest corner.</summary>
    public float MinX { get; }

    public float MinZ { get; }

    /// <summary>Per cell, row by row: how much of the cell the canopy covers, 0 to 1.</summary>
    public IReadOnlyList<float> Density => _density;

    /// <summary>Per cell: the mean ground height under the canopy, 0 where the density is.</summary>
    public IReadOnlyList<float> Ground => _ground;

    /// <summary>Per cell: the mean tree-top height. Painted canopy has no trees, so it is the ground there.</summary>
    public IReadOnlyList<float> Top => _top;

    /// <summary>Bakes from card stamps. Each kept stamp's crown, a disc as wide as its card, is split bilinearly
    /// over the four cells around it. A regular stamp lattice then reads even, whatever its phase to the grid.
    /// Density is the crown share over <see cref="FullCrownShare"/>, held at 1; ground and top are crown-weighted
    /// means. A stamp with a zero basis is one the brush mask dropped, and counts nothing.</summary>
    public static CanopyMask FromStamps(IEnumerable<(Transform3D Placement, float Width, float Height)> stamps,
        float left, float top, float right, float bottom)
    {
        var mask = new CanopyMask(left, top, right, bottom);
        var crowns = new float[mask._density.Length];
        foreach (var (placement, width, height) in stamps)
        {
            var o = placement.Origin;
            if (mask.CellAt(o.X, o.Z) < 0 || placement.Basis.Y.LengthSquared() == 0f)
                continue;
            float crown = MathF.PI * width * width / 4f;
            float u = ((o.X - mask.MinX) / CellM) - 0.5f, v = ((o.Z - mask.MinZ) / CellM) - 0.5f;
            int i0 = (int)MathF.Floor(u), j0 = (int)MathF.Floor(v);
            for (int k = 0; k < 4; k++)
            {
                int i = i0 + (k & 1), j = j0 + (k >> 1);
                if (i < 0 || j < 0 || i >= mask.Width || j >= mask.Height)
                    continue;
                float w = crown * (1f - MathF.Abs(u - i)) * (1f - MathF.Abs(v - j));
                int c = (j * mask.Width) + i;
                crowns[c] += w;
                mask._ground[c] += w * o.Y;
                mask._top[c] += w * (o.Y + height);
            }
        }

        for (int c = 0; c < crowns.Length; c++)
        {
            if (crowns[c] <= 0f)
                continue;
            mask._density[c] = Math.Min(1f, crowns[c] / (FullCrownShare * CellM * CellM));
            mask._ground[c] /= crowns[c];
            mask._top[c] /= crowns[c];
        }

        return mask;
    }

    /// <summary>Bakes from ground triangles painted as canopy, three world corners each. A cell's density is the
    /// share of its sample points under one, less where <paramref name="brush"/> thins it. Ground and top
    /// are the mean height of the highest triangle over each covered point.</summary>
    public static CanopyMask FromPainted(IReadOnlyList<Vector3> triangles, float left, float top, float right, float bottom,
        VegetationMask? brush = null)
    {
        var mask = new CanopyMask(left, top, right, bottom);
        int sw = mask.Width * PaintSamples, sh = mask.Height * PaintSamples;
        const float step = CellM / PaintSamples;
        var heights = new float[sw * sh];
        Array.Fill(heights, float.NaN);
        for (int t = 0; t + 2 < triangles.Count; t += 3)
            Fill(triangles[t], triangles[t + 1], triangles[t + 2], mask.MinX, mask.MinZ, step, sw, sh, heights);

        for (int j = 0; j < mask.Height; j++)
        {
            for (int i = 0; i < mask.Width; i++)
            {
                int covered = 0;
                float sum = 0f;
                var samples = new float[PaintSamples * PaintSamples];
                for (int sj = 0; sj < PaintSamples; sj++)
                {
                    for (int si = 0; si < PaintSamples; si++)
                    {
                        float y = samples[(sj * PaintSamples) + si] = heights[(((j * PaintSamples) + sj) * sw) + (i * PaintSamples) + si];
                        if (float.IsNaN(y))
                            continue;
                        covered++;
                        sum += y;
                    }
                }

                if (covered == 0)
                    continue;
                int c = (j * mask.Width) + i;
                mask._samples[c] = samples;
                float thin = brush == null ? 1f : Math.Min(1f, brush.At(mask.MinX + ((i + 0.5f) * CellM), mask.MinZ + ((j + 0.5f) * CellM)));
                mask._density[c] = thin * covered / (PaintSamples * PaintSamples);
                mask._ground[c] = mask._top[c] = sum / covered;
            }
        }

        return mask;
    }

    /// <summary>Whether <paramref name="texture"/> is picked as painted canopy in <paramref name="entry"/>.</summary>
    public static bool IsCanopy(VegetationEntry entry, string texture) =>
        string.Equals(entry.SetFor(texture)?.Name, CanopySet, StringComparison.OrdinalIgnoreCase);

    /// <summary>The cell holding a world X/Z, -1 off the area.</summary>
    public int CellAt(float x, float z)
    {
        int i = (int)MathF.Floor((x - MinX) / CellM), j = (int)MathF.Floor((z - MinZ) / CellM);
        return i < 0 || j < 0 || i >= Width || j >= Height ? -1 : (j * Width) + i;
    }

    /// <summary>The painted surface's height at a world X/Z, bilinear between the bake's sample points (8 m apart)
    /// that lie on the canopy. Off them, and on a stamp bake, the cell's mean ground; 0 off the area.</summary>
    public float SurfaceAt(float x, float z)
    {
        const float step = CellM / PaintSamples;
        float u = ((x - MinX) / step) - 0.5f, v = ((z - MinZ) / step) - 0.5f;
        int i0 = (int)MathF.Floor(u), j0 = (int)MathF.Floor(v);
        float sum = 0f, weight = 0f;
        for (int k = 0; k < 4; k++)
        {
            int i = i0 + (k & 1), j = j0 + (k >> 1);
            if (i < 0 || j < 0 || i >= Width * PaintSamples || j >= Height * PaintSamples
                || !_samples.TryGetValue(((j / PaintSamples) * Width) + (i / PaintSamples), out var cell))
                continue;
            float y = cell[((j % PaintSamples) * PaintSamples) + (i % PaintSamples)];
            if (float.IsNaN(y))
                continue;
            float w = (1f - MathF.Abs(u - i)) * (1f - MathF.Abs(v - j));
            sum += w * y;
            weight += w;
        }

        return weight > 1e-6f ? sum / weight : CellAt(x, z) is int c and >= 0 ? _ground[c] : 0f;
    }

    /// <summary>The share of a cell's sample points a painted bake found on the canopy, 0 on a stamp bake.</summary>
    public float PaintedShare(int cell) =>
        _samples.TryGetValue(cell, out var s) ? (float)s.Count(y => !float.IsNaN(y)) / s.Length : 0f;

    /// <summary>Whether the sample point nearest a world X/Z lies on the painted canopy.</summary>
    public bool IsPainted(float x, float z)
    {
        const float step = CellM / PaintSamples;
        int i = (int)MathF.Floor((x - MinX) / step), j = (int)MathF.Floor((z - MinZ) / step);
        return i >= 0 && j >= 0 && i < Width * PaintSamples && j < Height * PaintSamples
            && _samples.TryGetValue(((j / PaintSamples) * Width) + (i / PaintSamples), out var cell)
            && !float.IsNaN(cell[((j % PaintSamples) * PaintSamples) + (i % PaintSamples)]);
    }

    /// <summary>The density at a world X/Z, bilinear between cell centres and 0 off the area.</summary>
    public float DensityAt(float x, float z)
    {
        if (CellAt(x, z) < 0)
            return 0f;
        float u = ((x - MinX) / CellM) - 0.5f, v = ((z - MinZ) / CellM) - 0.5f;
        int i0 = Math.Clamp((int)MathF.Floor(u), 0, Width - 1), j0 = Math.Clamp((int)MathF.Floor(v), 0, Height - 1);
        int i1 = Math.Min(i0 + 1, Width - 1), j1 = Math.Min(j0 + 1, Height - 1);
        float fu = Math.Clamp(u - i0, 0f, 1f), fv = Math.Clamp(v - j0, 0f, 1f);
        float a = Mathf.Lerp(_density[(j0 * Width) + i0], _density[(j0 * Width) + i1], fu);
        float b = Mathf.Lerp(_density[(j1 * Width) + i0], _density[(j1 * Width) + i1], fu);
        return Mathf.Lerp(a, b, fv);
    }

    // Marks every sample point in the triangle's XZ shadow with its height there, keeping the higher layer.
    private static void Fill(Vector3 a, Vector3 b, Vector3 c, float x0, float z0, float step, int sw, int sh, float[] heights)
    {
        float d = ((b.Z - c.Z) * (a.X - c.X)) + ((c.X - b.X) * (a.Z - c.Z));
        if (Math.Abs(d) < 1e-6f)
            return;
        int iMin = Math.Max(0, (int)MathF.Ceiling(((Math.Min(a.X, Math.Min(b.X, c.X)) - x0) / step) - 0.5f));
        int iMax = Math.Min(sw - 1, (int)MathF.Floor(((Math.Max(a.X, Math.Max(b.X, c.X)) - x0) / step) - 0.5f));
        int jMin = Math.Max(0, (int)MathF.Ceiling(((Math.Min(a.Z, Math.Min(b.Z, c.Z)) - z0) / step) - 0.5f));
        int jMax = Math.Min(sh - 1, (int)MathF.Floor(((Math.Max(a.Z, Math.Max(b.Z, c.Z)) - z0) / step) - 0.5f));
        for (int j = jMin; j <= jMax; j++)
        {
            float pz = z0 + ((j + 0.5f) * step);
            for (int i = iMin; i <= iMax; i++)
            {
                float px = x0 + ((i + 0.5f) * step);
                float w0 = (((b.Z - c.Z) * (px - c.X)) + ((c.X - b.X) * (pz - c.Z))) / d;
                float w1 = (((c.Z - a.Z) * (px - c.X)) + ((a.X - c.X) * (pz - c.Z))) / d;
                float w2 = 1f - w0 - w1;
                if (w0 < 0f || w1 < 0f || w2 < 0f)
                    continue;
                float y = (w0 * a.Y) + (w1 * b.Y) + (w2 * c.Y);
                ref float held = ref heights[(j * sw) + i];
                if (float.IsNaN(held) || y > held)
                    held = y;
            }
        }
    }
}
