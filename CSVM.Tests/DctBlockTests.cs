using System;
using System.Linq;
using CSVM.Video;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The 8x8 block: the scan order, the default quantiser matrices, the dequantisation rule, and
/// the integer inverse transform against the real-valued one ISO 11172-2 defines. The standard
/// permits a conformant decoder to differ from that definition, so the transform is checked
/// against the definition within a tolerance rather than against another decoder's output.
/// </summary>
public class DctBlockTests
{
    // The integer transform is an approximation the standard explicitly permits, so the check is
    // that it is the same transform rather than the same arithmetic. The bound is loose because
    // the scale factors are held to eight bits, which attenuates the topmost basis function by
    // about a fifth; the table those factors come from is checked exactly instead, and a wrong
    // constant or a transposed pass still overshoots this by a wide margin.
    private const int TransformTolerance = 2;
    private const double TransformRelativeTolerance = 0.2;

    /// <summary>The scan is the standard's diagonal walk, derived here from the rule rather than
    /// transcribed: each anti-diagonal is taken in turn, upward along one and downward along the
    /// next, so a single transposed entry breaks it. Checking the whole permutation is what
    /// catches a swap, which a spot check on the four corners cannot.</summary>
    [Fact]
    public void TheScanOrderIsTheStandardsDiagonalWalk()
    {
        Assert.Equal(DctBlock.Size, DctBlock.ZigZag.Length);
        var expected = new int[DctBlock.Size];
        int at = 0;
        for (int diagonal = 0; diagonal < 15; diagonal++)
        {
            int first = Math.Max(0, diagonal - 7);
            int last = Math.Min(7, diagonal);
            for (int step = first; step <= last; step++)
            {
                int x = (diagonal & 1) == 0 ? step : diagonal - step;
                expected[at++] = ((diagonal - x) * 8) + x;
            }
        }

        for (int index = 0; index < DctBlock.Size; index++)
        {
            Assert.Equal(expected[index], DctBlock.ZigZag[index]);
        }
    }

    /// <summary>Every entry of the two default quantiser matrices ISO 11172-2 tabulates, in
    /// raster order. The intra matrix is not symmetric about its diagonal, so there is no rule to
    /// derive it from and the standard's own numbers are written out here.</summary>
    [Fact]
    public void TheDefaultMatricesAreTheStandardsOwn()
    {
        int[] intra =
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

        for (int index = 0; index < DctBlock.Size; index++)
        {
            Assert.Equal(intra[index], DctBlock.IntraQuantMatrix[index]);
            Assert.Equal(16, DctBlock.NonIntraQuantMatrix[index]);
        }
    }

    // Every dequantised coefficient is odd, which is the standard's own mismatch control; the
    // non-intra form biases away from zero first, and both clip to the coded range.
    [Theory]
    [InlineData(4, 8, 16, true, 63)]
    [InlineData(4, 8, 16, false, 71)]
    [InlineData(-4, 8, 16, true, -63)]
    [InlineData(-4, 8, 16, false, -71)]
    [InlineData(1, 1, 8, true, 1)]
    [InlineData(0, 8, 16, true, 1)]
    [InlineData(2047, 31, 83, true, 2047)]
    [InlineData(-2047, 31, 83, true, -2048)]
    public void DequantizeFollowsTheStandard(int level, int scale, int matrix, bool intra, int expected) =>
        Assert.Equal(expected, DctBlock.Dequantize(level, scale, matrix, intra));

    // Each entry is the pair of scale factors the transform folds into a coefficient, held to
    // eight bits: 32 for the constant term and sqrt(2)cos(k*pi/16) times 32 for the rest.
    [Fact]
    public void ThePremultiplierIsTheTransformsOwnScaleFactors()
    {
        for (int v = 0; v < 8; v++)
        {
            for (int u = 0; u < 8; u++)
            {
                double expected = Math.Round(32.0 * ScaleFactor(u) * ScaleFactor(v));
                Assert.Equal(expected, DctBlock.Premultiplier[(v * 8) + u]);
            }
        }
    }

    [Fact]
    public void ADcOnlyBlockTransformsToOneFlatValue()
    {
        var block = new int[DctBlock.Size];
        block[0] = 8 * DctBlock.Premultiplier[0];
        DctBlock.InverseTransform(block);
        Assert.All(block, sample => Assert.Equal(1, sample));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TheTransformMatchesTheStandardsDefinition(int pattern)
    {
        int[] coefficients = Coefficients(pattern);
        double[] expected = ReferenceTransform(coefficients);

        var block = new int[DctBlock.Size];
        for (int i = 0; i < DctBlock.Size; i++)
        {
            block[i] = coefficients[i] * DctBlock.Premultiplier[i];
        }

        DctBlock.InverseTransform(block);
        double peak = expected.Max(Math.Abs);
        double tolerance = TransformTolerance + (TransformRelativeTolerance * peak);
        for (int i = 0; i < DctBlock.Size; i++)
        {
            Assert.True(
                Math.Abs(block[i] - expected[i]) <= tolerance,
                $"sample {i}: {block[i]} against {expected[i]:0.000}, tolerance {tolerance:0.000}");
        }
    }

    private static double ScaleFactor(int index) =>
        index == 0 ? 1.0 : Math.Sqrt(2.0) * Math.Cos(index * Math.PI / 16.0);

    // Four fixed coefficient sets: a lone DC, a lone high-frequency term, a low-frequency
    // corner, and a spread produced by a fixed sequence so the case is the same every run.
    private static int[] Coefficients(int pattern)
    {
        var coefficients = new int[DctBlock.Size];
        switch (pattern)
        {
            case 0:
                coefficients[0] = 1024;
                break;
            case 1:
                coefficients[63] = 255;
                break;
            case 2:
                coefficients[0] = 512;
                coefficients[1] = -129;
                coefficients[8] = 63;
                coefficients[9] = -31;
                break;
            default:
                int state = 7;
                for (int i = 0; i < DctBlock.Size; i++)
                {
                    state = ((state * 1103515245) + 12345) & 0x7fffffff;
                    coefficients[i] = ((state >> 16) % 101) - 50;
                }

                break;
        }

        return coefficients;
    }

    // The transform as ISO 11172-2 defines it, in double precision.
    private static double[] ReferenceTransform(int[] coefficients)
    {
        var samples = new double[DctBlock.Size];
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                double sum = 0.0;
                for (int v = 0; v < 8; v++)
                {
                    for (int u = 0; u < 8; u++)
                    {
                        double cu = u == 0 ? 1.0 / Math.Sqrt(2.0) : 1.0;
                        double cv = v == 0 ? 1.0 / Math.Sqrt(2.0) : 1.0;
                        sum += cu * cv * coefficients[(v * 8) + u]
                            * Math.Cos(((2 * x) + 1) * u * Math.PI / 16.0)
                            * Math.Cos(((2 * y) + 1) * v * Math.PI / 16.0);
                    }
                }

                samples[(y * 8) + x] = sum / 4.0;
            }
        }

        return samples;
    }
}
