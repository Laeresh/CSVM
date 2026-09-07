using System;
using CSVM.Video;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Motion-compensated prediction against the sampling ISO 11172-2 defines: a whole-pel vector
/// copies, a half-pel one averages the samples it falls between, and an interpolating pass
/// averages that against what the destination already holds.
/// </summary>
public class MotionCompensationTests
{
    private const int PlaneWidth = 32;
    private const int PlaneHeight = 32;
    private const int BlockSize = 16;

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
