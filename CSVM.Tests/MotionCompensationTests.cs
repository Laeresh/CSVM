using System;
using System.Linq;
using CSVM.Video;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Motion-compensated prediction against the sampling ISO 11172-2 defines: a whole-pel vector
/// copies, a half-pel one averages the samples it falls between, and an interpolating pass
/// averages that against what the destination already holds. The last two tests take the other
/// half of the subject, how a vector reaches that sampling from the stream, by decoding a
/// picture whose motion fields are spelled out of the standard's own tables.
/// </summary>
public class MotionCompensationTests
{
    private const int PlaneWidth = 32;
    private const int PlaneHeight = 32;
    private const int BlockSize = 16;

    // The picture MpegTestStreams.IntraThenMotion builds, and the eight DC differences that give
    // each of its 8x8 luma blocks a value of its own. Every one has magnitude 8 to 15, which is
    // what makes all eight code in a four-bit field; the values they add up to are all different,
    // so a prediction that lands one sample out shows at every block boundary.
    private const int MotionPictureWidth = 32;
    private const int MotionPictureHeight = 16;
    private static readonly int[] LumaDcDeltas = { 15, -8, 12, -14, 9, -13, 11, -10 };

    [Fact]
    public void AWholePelVectorCopiesTheBlockItPointsAt()
    {
        byte[] source = Ramp();
        var destination = new byte[source.Length];
        MotionCompensation.Predict(source, destination, PlaneWidth, 0, 0, BlockSize, 4, 6, false);

        for (int y = 0; y < BlockSize; y++)
        {
            for (int x = 0; x < BlockSize; x++)
            {
                Assert.Equal(source[((y + 3) * PlaneWidth) + x + 2], destination[(y * PlaneWidth) + x]);
            }
        }
    }

    [Fact]
    public void AHalfPelHorizontalVectorAveragesTheTwoSamples()
    {
        byte[] source = Ramp();
        var destination = new byte[source.Length];
        MotionCompensation.Predict(source, destination, PlaneWidth, 0, 0, BlockSize, 1, 0, false);

        for (int y = 0; y < BlockSize; y++)
        {
            for (int x = 0; x < BlockSize; x++)
            {
                int at = (y * PlaneWidth) + x;
                Assert.Equal((source[at] + source[at + 1] + 1) >> 1, destination[at]);
            }
        }
    }

    [Fact]
    public void AHalfPelVerticalVectorAveragesTheTwoRows()
    {
        byte[] source = Ramp();
        var destination = new byte[source.Length];
        MotionCompensation.Predict(source, destination, PlaneWidth, 0, 0, BlockSize, 0, 1, false);

        for (int y = 0; y < BlockSize; y++)
        {
            for (int x = 0; x < BlockSize; x++)
            {
                int at = (y * PlaneWidth) + x;
                Assert.Equal((source[at] + source[at + PlaneWidth] + 1) >> 1, destination[at]);
            }
        }
    }

    [Fact]
    public void AVectorOddInBothAxesAveragesTheFourSamples()
    {
        byte[] source = Ramp();
        var destination = new byte[source.Length];
        MotionCompensation.Predict(source, destination, PlaneWidth, 0, 0, BlockSize, 1, 1, false);

        int corner = source[0] + source[1] + source[PlaneWidth] + source[PlaneWidth + 1] + 2;
        Assert.Equal(corner >> 2, destination[0]);
    }

    [Fact]
    public void InterpolationAveragesAgainstWhatIsAlreadyThere()
    {
        byte[] source = Ramp();
        var destination = new byte[source.Length];
        Array.Fill(destination, (byte)100);
        MotionCompensation.Predict(source, destination, PlaneWidth, 0, 0, BlockSize, 0, 0, true);

        Assert.Equal((100 + source[0] + 1) >> 1, destination[0]);
        Assert.Equal((100 + source[BlockSize - 1] + 1) >> 1, destination[BlockSize - 1]);
    }

    [Fact]
    public void TheSecondMacroblockLandsAtItsOwnPlaceInThePlane()
    {
        byte[] source = Ramp();
        var destination = new byte[source.Length];
        MotionCompensation.Predict(source, destination, PlaneWidth, 1, 1, BlockSize, 0, 0, false);

        int at = (BlockSize * PlaneWidth) + BlockSize;
        Assert.Equal(source[at], destination[at]);
        Assert.Equal(0, destination[0]);
    }

    // A vector the standard forbids, because the block it names is not wholly inside the plane.
    [Theory]
    [InlineData(64, 0)]
    [InlineData(0, 64)]
    [InlineData(-64, 0)]
    [InlineData(0, -64)]
    public void AVectorReachingOutsideThePlanePredictsNothing(int horizontal, int vertical)
    {
        byte[] source = Ramp();
        var destination = new byte[source.Length];
        MotionCompensation.Predict(
            source, destination, PlaneWidth, 0, 0, BlockSize, horizontal, vertical, false);

        Assert.All(destination, sample => Assert.Equal(0, sample));
    }

    /// <summary>A half-pel vector whose block ends flush with the last column of the plane has
    /// no sample to its right to average with, so the edge sample repeats and the last column is
    /// the reference's own. Reading the neighbour there would be a read past the plane.</summary>
    [Fact]
    public void AHalfPelBlockFlushWithTheLastColumnRepeatsTheEdgeSample()
    {
        byte[] source = Ramp();
        var destination = new byte[source.Length];
        MotionCompensation.Predict(source, destination, PlaneWidth, 0, 1, BlockSize, 1, 0, false);

        for (int x = 0; x < BlockSize; x++)
        {
            int at = BlockSize + x;
            int expected = x == BlockSize - 1 ? source[at] : (source[at] + source[at + 1] + 1) >> 1;
            Assert.Equal(expected, destination[at]);
        }
    }

    /// <summary>The same at the bottom of the plane, where the row below is outside it.</summary>
    [Fact]
    public void AHalfPelBlockFlushWithTheLastRowRepeatsTheEdgeSample()
    {
        byte[] source = Ramp();
        var destination = new byte[source.Length];
        MotionCompensation.Predict(source, destination, PlaneWidth, 1, 0, BlockSize, 0, 1, false);

        for (int y = 0; y < BlockSize; y++)
        {
            int at = (BlockSize + y) * PlaneWidth;
            int expected = y == BlockSize - 1
                ? source[at]
                : (source[at] + source[at + PlaneWidth] + 1) >> 1;
            Assert.Equal(expected, destination[at]);
        }
    }

    /// <summary>A vector one sample left of the plane at the second row. Its block starts at a
    /// flat offset that is inside the plane's buffer, so a guard that only range-checks that
    /// offset accepts it and predicts from the previous row's right-hand edge; the columns have
    /// to be checked on their own for this to be rejected.</summary>
    [Fact]
    public void AVectorLeftOfThePlaneIsRejectedRatherThanWrappedOntoTheRowAbove()
    {
        byte[] source = Ramp();
        var destination = new byte[source.Length];
        MotionCompensation.Predict(source, destination, PlaneWidth, 0, 0, BlockSize, -2, 2, false);

        Assert.All(destination, sample => Assert.Equal(0, sample));
    }

    /// <summary>A vector is a variable-length code, then <c>r_size</c> residual bits, then a
    /// difference against the previous macroblock's. Under an <c>f_code</c> of 2 the first
    /// macroblock's field reconstructs to two half-pel units, one sample right, and the second's
    /// takes the running vector back to zero. Reading the code without its residual would blur
    /// the block instead of moving it, and losing the difference would move both.</summary>
    [Fact]
    public void AVectorIsItsCodeItsResidualAndTheOneBeforeIt()
    {
        (byte[] reference, byte[] predicted) = DecodeMotionPictures(2, new[] { "0101", "0111" });

        for (int y = 0; y < MotionPictureHeight; y++)
        {
            int row = y * MotionPictureWidth;
            for (int x = 0; x < BlockSize; x++)
            {
                Assert.Equal(reference[row + x + 1], predicted[row + x]);
                Assert.Equal(reference[row + BlockSize + x], predicted[row + BlockSize + x]);
            }
        }
    }

    /// <summary>A vector past the range its <c>f_code</c> allows wraps into that range rather
    /// than saturating. Under an <c>f_code</c> of 1 that range is -16 to 15 half-pel units, so
    /// the second macroblock's code for 16 predicts from eight samples to its left; unwrapped it
    /// would point eight samples right, off the end of the plane, and predict nothing.</summary>
    [Fact]
    public void AVectorPastItsRangeWrapsIntoItRatherThanSaturating()
    {
        (byte[] reference, byte[] predicted) = DecodeMotionPictures(1, new[] { "1", "00000011000" });

        for (int y = 0; y < MotionPictureHeight; y++)
        {
            int row = y * MotionPictureWidth;
            for (int x = 0; x < BlockSize; x++)
            {
                Assert.Equal(reference[row + x], predicted[row + x]);
                Assert.Equal(reference[row + (BlockSize / 2) + x], predicted[row + BlockSize + x]);
            }
        }
    }

    // Decodes the two-picture fixture and returns both luma planes. The reference has to be
    // copied, because a decoder hands the same buffers out again on the next call.
    private static (byte[] Reference, byte[] Predicted) DecodeMotionPictures(
        int fCode, string[] horizontalMotionBits)
    {
        var movie = MpegMovie.FromBytes(MpegTestStreams.SystemStream(
            MpegTestStreams.IntraThenMotion(fCode, horizontalMotionBits, LumaDcDeltas), 0));

        byte[] reference = (byte[])Assert.IsType<VideoFrame>(movie.NextFrame()).Luma.Clone();
        byte[] predicted = (byte[])Assert.IsType<VideoFrame>(movie.NextFrame()).Luma.Clone();

        // The fixture's own premise: eight blocks whose values all differ, so a shift is visible.
        Assert.Equal(8, reference.Distinct().Count());
        Assert.Null(movie.NextFrame());
        return (reference, predicted);
    }

    // A plane whose every sample differs from its neighbours in both axes, so a vector that
    // lands one sample off cannot pass unnoticed.
    private static byte[] Ramp()
    {
        var plane = new byte[PlaneWidth * PlaneHeight];
        for (int y = 0; y < PlaneHeight; y++)
        {
            for (int x = 0; x < PlaneWidth; x++)
            {
                plane[(y * PlaneWidth) + x] = (byte)(((y * 7) + (x * 3)) & 0xff);
            }
        }

        return plane;
    }
}
