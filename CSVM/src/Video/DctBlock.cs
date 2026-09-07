using System;

namespace CSVM.Video;

/// <summary>
/// The 8x8 coefficient block of ISO 11172-2: the zig-zag scan order, the two default quantiser
/// matrices, the dequantisation rule, and the inverse DCT that turns coefficients back into
/// samples. The transform is the integer one every MPEG-1 decoder uses in place of the
/// standard's real-valued definition, so its output is within the mismatch the standard permits
/// rather than exact; nothing here may be tested by comparing pixels against another decoder.
/// Coefficients reach <see cref="InverseTransform"/> premultiplied by <see cref="Premultiplier"/>.
/// </summary>
public static class DctBlock
{
    /// <summary>Coefficients in one block, 8 by 8.</summary>
    public const int Size = 64;

    // Scan position to raster index. A block's coefficients arrive along this diagonal walk,
    // which is what puts the runs of zeroes the run/level codes compress at the end.
    private static readonly byte[] ZigZagData =
    {
        0,  1,  8, 16,  9,  2,  3, 10,
        17, 24, 32, 25, 18, 11,  4,  5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13,  6,  7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63,
    };

    // The standard's default intra matrix, used unless a sequence header carries its own.
    private static readonly byte[] IntraQuantMatrixData =
    {
        8, 16, 19, 22, 26, 27, 29, 34,
        16, 16, 22, 24, 27, 29, 34, 37,
        19, 22, 26, 27, 29, 34, 34, 38,
        22, 22, 26, 27, 29, 34, 37, 40,
        22, 26, 27, 29, 32, 35, 40, 48,
        26, 27, 29, 32, 35, 40, 48, 58,
        26, 27, 29, 34, 38, 46, 56, 69,
        27, 29, 35, 38, 46, 56, 69, 83,
    };

    // The standard's default non-intra matrix: flat 16, a plain quantiser step.
    private static readonly byte[] NonIntraQuantMatrixData =
    {
        16, 16, 16, 16, 16, 16, 16, 16,
        16, 16, 16, 16, 16, 16, 16, 16,
        16, 16, 16, 16, 16, 16, 16, 16,
        16, 16, 16, 16, 16, 16, 16, 16,
        16, 16, 16, 16, 16, 16, 16, 16,
        16, 16, 16, 16, 16, 16, 16, 16,
        16, 16, 16, 16, 16, 16, 16, 16,
        16, 16, 16, 16, 16, 16, 16, 16,
    };

    // The scale factors this particular integer transform folds into its input, round(32 * s(u)
    // * s(v)) with s(0) = 1 and s(k) = sqrt(2) * cos(k * pi / 16). InverseTransform assumes them,
    // so a coefficient that skips the premultiply comes out at the wrong amplitude.
    private static readonly byte[] PremultiplierData =
    {
        32, 44, 42, 38, 32, 25, 17,  9,
        44, 62, 58, 52, 44, 35, 24, 12,
        42, 58, 55, 49, 42, 33, 23, 12,
        38, 52, 49, 44, 38, 30, 20, 10,
        32, 44, 42, 38, 32, 25, 17,  9,
        25, 35, 33, 30, 25, 20, 14,  7,
        17, 24, 23, 20, 17, 14,  9,  5,
        9, 12, 12, 10,  9,  7,  5,  2,
    };

    /// <summary>Scan position to raster index within the block.</summary>
    public static ReadOnlySpan<byte> ZigZag => ZigZagData;

    /// <summary>The default intra quantiser matrix, in raster order.</summary>
    public static ReadOnlySpan<byte> IntraQuantMatrix => IntraQuantMatrixData;

    /// <summary>The default non-intra quantiser matrix, in raster order.</summary>
    public static ReadOnlySpan<byte> NonIntraQuantMatrix => NonIntraQuantMatrixData;

    /// <summary>The per-coefficient scale <see cref="InverseTransform"/> expects folded in.</summary>
    public static ReadOnlySpan<byte> Premultiplier => PremultiplierData;

    /// <summary>Turns one decoded run/level pair back into a coefficient: scale by the quantiser
    /// and the matrix entry, force the result odd, and clip to the standard's range. The oddify
    /// step is the standard's own, and dropping it drifts the reconstruction over a whole
    /// picture rather than failing outright.</summary>
    public static int Dequantize(int level, int quantizerScale, int quantMatrixValue, bool intra)
    {
        level <<= 1;
        if (!intra)
        {
            level += level < 0 ? -1 : 1;
        }

        level = (level * quantizerScale * quantMatrixValue) >> 4;
        if ((level & 1) == 0)
        {
            level -= level > 0 ? 1 : -1;
        }

        return Math.Clamp(level, -2048, 2047);
    }

    /// <summary>Transforms 64 premultiplied coefficients into 64 samples in place, columns then
    /// rows. The samples are the residual an intra block writes and a predicted block adds, both
    /// still needing a clamp into 0 to 255.</summary>
    public static void InverseTransform(Span<int> block)
    {
        for (int i = 0; i < 8; ++i)
        {
            TransformColumn(block, i);
        }

        for (int i = 0; i < Size; i += 8)
        {
            TransformRow(block, i);
        }
    }

    // The odd-looking constants are 8-bit fixed point: 473 is 2cos(pi/8), 362 is sqrt(2) and
    // 196 is 2sin(pi/8). The column pass keeps the extra 8 bits of headroom that the row pass
    // then rounds away.
    private static void TransformColumn(Span<int> block, int i)
    {
        int b1 = block[(4 * 8) + i];
        int b3 = block[(2 * 8) + i] + block[(6 * 8) + i];
        int b4 = block[(5 * 8) + i] - block[(3 * 8) + i];
        int tmp1 = block[(1 * 8) + i] + block[(7 * 8) + i];
        int tmp2 = block[(3 * 8) + i] + block[(5 * 8) + i];
        int b6 = block[(1 * 8) + i] - block[(7 * 8) + i];
        int b7 = tmp1 + tmp2;
        int m0 = block[(0 * 8) + i];
        int x4 = (((b6 * 473) - (b4 * 196) + 128) >> 8) - b7;
        int x0 = x4 - ((((tmp1 - tmp2) * 362) + 128) >> 8);
        int x1 = m0 - b1;
        int x2 = ((((block[(2 * 8) + i] - block[(6 * 8) + i]) * 362) + 128) >> 8) - b3;
        int x3 = m0 + b1;
        int y3 = x1 + x2;
        int y4 = x3 + b3;
        int y5 = x1 - x2;
        int y6 = x3 - b3;
        int y7 = -x0 - (((b4 * 473) + (b6 * 196) + 128) >> 8);
        block[(0 * 8) + i] = b7 + y4;
        block[(1 * 8) + i] = x4 + y3;
        block[(2 * 8) + i] = y5 - x0;
        block[(3 * 8) + i] = y6 - y7;
        block[(4 * 8) + i] = y6 + y7;
        block[(5 * 8) + i] = x0 + y5;
        block[(6 * 8) + i] = y3 - x4;
        block[(7 * 8) + i] = y4 - b7;
    }

    private static void TransformRow(Span<int> block, int i)
    {
        int b1 = block[4 + i];
        int b3 = block[2 + i] + block[6 + i];
        int b4 = block[5 + i] - block[3 + i];
        int tmp1 = block[1 + i] + block[7 + i];
        int tmp2 = block[3 + i] + block[5 + i];
        int b6 = block[1 + i] - block[7 + i];
        int b7 = tmp1 + tmp2;
        int m0 = block[0 + i];
        int x4 = (((b6 * 473) - (b4 * 196) + 128) >> 8) - b7;
        int x0 = x4 - ((((tmp1 - tmp2) * 362) + 128) >> 8);
        int x1 = m0 - b1;
        int x2 = ((((block[2 + i] - block[6 + i]) * 362) + 128) >> 8) - b3;
        int x3 = m0 + b1;
        int y3 = x1 + x2;
        int y4 = x3 + b3;
        int y5 = x1 - x2;
        int y6 = x3 - b3;
        int y7 = -x0 - (((b4 * 473) + (b6 * 196) + 128) >> 8);
        block[0 + i] = (b7 + y4 + 128) >> 8;
        block[1 + i] = (x4 + y3 + 128) >> 8;
        block[2 + i] = (y5 - x0 + 128) >> 8;
        block[3 + i] = (y6 - y7 + 128) >> 8;
        block[4 + i] = (y6 + y7 + 128) >> 8;
        block[5 + i] = (x0 + y5 + 128) >> 8;
        block[6 + i] = (y3 - x4 + 128) >> 8;
        block[7 + i] = (y4 - b7 + 128) >> 8;
    }
}
