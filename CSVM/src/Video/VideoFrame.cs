using System;

namespace CSVM.Video;

/// <summary>
/// One decoded picture: three 8-bit planes in the 4:2:0 layout MPEG-1 codes, plus the moment it
/// is to be shown, in seconds on the container's clock. The planes are padded out to whole
/// macroblocks, so a row is <see cref="LumaStride"/> wide while only <see cref="Width"/> of it
/// is picture; <see cref="WriteRgba"/> is the conversion any caller that wants pixels uses.
/// A decoder hands out the same three frames over and over, so a frame is only valid until the
/// next one is asked for.
/// </summary>
public sealed class VideoFrame
{
    internal VideoFrame(int width, int height, int lumaStride, int lumaHeight, int chromaStride, int chromaHeight)
    {
        Width = width;
        Height = height;
        LumaStride = lumaStride;
        ChromaStride = chromaStride;
        Luma = new byte[lumaStride * lumaHeight];
        Cb = new byte[chromaStride * chromaHeight];
        Cr = new byte[chromaStride * chromaHeight];
    }

    /// <summary>Picture width, which is what the sequence header declares.</summary>
    public int Width { get; }

    /// <summary>Picture height, which is what the sequence header declares.</summary>
    public int Height { get; }

    /// <summary>Samples per luma row, rounded up to whole macroblocks.</summary>
    public int LumaStride { get; }

    /// <summary>Samples per chroma row, half the luma stride.</summary>
    public int ChromaStride { get; }

    /// <summary>When this picture is to be shown, in seconds on the container's clock.</summary>
    public double Time { get; internal set; }

    /// <summary>The luma plane, one byte per sample.</summary>
    public byte[] Luma { get; }

    /// <summary>The blue-difference chroma plane, one sample per 2x2 luma quad.</summary>
    public byte[] Cb { get; }

    /// <summary>The red-difference chroma plane, one sample per 2x2 luma quad.</summary>
    public byte[] Cr { get; }

    /// <summary>The picture as 8-bit RGBA, four bytes per pixel, rows tightly packed.</summary>
    public byte[] ToRgba()
    {
        var pixels = new byte[Width * Height * 4];
        WriteRgba(pixels, Width * 4);
        return pixels;
    }

    /// <summary>Converts the picture to 8-bit RGBA at that row stride, opaque throughout.
    /// The colour matrix is BT.601, which is what MPEG-1 content is authored against. An odd
    /// width or height leaves its last column or row untouched, since the conversion walks the
    /// chroma plane and each chroma sample covers a 2x2 quad.</summary>
    public void WriteRgba(Span<byte> destination, int stride)
    {
        int columns = Width >> 1;
        int rows = Height >> 1;
        for (int row = 0; row < rows; row++)
        {
            int chromaIndex = row * ChromaStride;
            int lumaIndex = row * 2 * LumaStride;
            int destinationIndex = row * 2 * stride;
            for (int column = 0; column < columns; column++)
            {
                int cr = Cr[chromaIndex] - 128;
                int cb = Cb[chromaIndex] - 128;
                int r = (cr * 104597) >> 16;
                int g = ((cb * 25674) + (cr * 53278)) >> 16;
                int b = (cb * 132201) >> 16;
                PutPixel(destination, destinationIndex, lumaIndex, r, g, b);
                PutPixel(destination, destinationIndex + 4, lumaIndex + 1, r, g, b);
                PutPixel(destination, destinationIndex + stride, lumaIndex + LumaStride, r, g, b);
                PutPixel(destination, destinationIndex + stride + 4, lumaIndex + LumaStride + 1, r, g, b);
                chromaIndex += 1;
                lumaIndex += 2;
                destinationIndex += 8;
            }
        }
    }

    private static byte Clamp(int value) => (byte)(value < 0 ? 0 : value > 255 ? 255 : value);

    // The luma scale and the 16 offset are BT.601's studio range, so a Y of 16 is black and 235
    // is white; the three chroma terms are already scaled by the caller.
    private void PutPixel(Span<byte> destination, int at, int lumaIndex, int r, int g, int b)
    {
        int y = ((Luma[lumaIndex] - 16) * 76309) >> 16;
        destination[at] = Clamp(y + r);
        destination[at + 1] = Clamp(y - g);
        destination[at + 2] = Clamp(y + b);
        destination[at + 3] = 255;
    }
}
