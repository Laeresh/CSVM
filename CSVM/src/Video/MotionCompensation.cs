using System;

namespace CSVM.Video;

/// <summary>
/// Motion-compensated prediction for one macroblock of one plane: fetches the block a motion
/// vector points at in a reference picture and either writes it or averages it into what is
/// already there. Vectors are in half-pel units, so an odd component averages the two or four
/// neighbouring samples the standard specifies. A vector whose block would fall outside the
/// plane predicts nothing at all, which is how a corrupt stream degrades instead of throwing.
/// </summary>
public static class MotionCompensation
{
    /// <summary>Predicts one square block into <paramref name="destination"/> at the macroblock
    /// grid position. <paramref name="blockSize"/> is 16 for luma and 8 for chroma, and a chroma
    /// vector is the luma one halved. With <paramref name="interpolate"/> the prediction is
    /// averaged with what the destination already holds, which is how a bidirectional macroblock
    /// combines its two references. Both planes are <paramref name="planeWidth"/> wide.</summary>
    public static void Predict(
        ReadOnlySpan<byte> source,
        Span<byte> destination,
        int planeWidth,
        int blockRow,
        int blockColumn,
        int blockSize,
        int motionHorizontal,
        int motionVertical,
        bool interpolate)
    {
        int sourceColumn = (blockColumn * blockSize) + (motionHorizontal >> 1);
        int sourceRow = (blockRow * blockSize) + (motionVertical >> 1);
        int planeHeight = source.Length / planeWidth;
        if (!Fits(sourceColumn, sourceRow, planeWidth, planeHeight, blockSize)
            || !Fits(blockColumn * blockSize, blockRow * blockSize, planeWidth, destination.Length / planeWidth, blockSize))
        {
            return;
        }

        // Half-pel sampling averages with the sample to the right, the one below, or both. At
        // the far edge of the plane that neighbour is outside it, which the standard forbids a
        // vector from reaching; the offset then stays zero, so the edge sample repeats.
        int right = (motionHorizontal & 1) == 0 ? 0 : 1;
        int down = (motionVertical & 1) == 0 ? 0 : planeWidth;
        int lastRight = sourceColumn + blockSize < planeWidth ? right : 0;
        int lastDown = sourceRow + blockSize < planeHeight ? down : 0;

        int sourceIndex = (sourceRow * planeWidth) + sourceColumn;
        int destinationIndex = ((blockRow * planeWidth) + blockColumn) * blockSize;
        int scan = planeWidth - blockSize;
        for (int y = 0; y < blockSize; y++)
        {
            int rowDown = y == blockSize - 1 ? lastDown : down;
            for (int x = 0; x < blockSize; x++)
            {
                int columnRight = x == blockSize - 1 ? lastRight : right;
                int sample = (source[sourceIndex]
                    + source[sourceIndex + columnRight]
                    + source[sourceIndex + rowDown]
                    + source[sourceIndex + rowDown + columnRight] + 2) >> 2;
                destination[destinationIndex] = (byte)(interpolate
                    ? (destination[destinationIndex] + sample + 1) >> 1
                    : sample);
                sourceIndex++;
                destinationIndex++;
            }

            sourceIndex += scan;
            destinationIndex += scan;
        }
    }

    private static bool Fits(int column, int row, int planeWidth, int planeHeight, int blockSize) =>
        column >= 0 && row >= 0 && column + blockSize <= planeWidth && row + blockSize <= planeHeight;
}
