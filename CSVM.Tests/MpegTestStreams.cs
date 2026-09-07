using System.Collections.Generic;

namespace CSVM.Tests;

/// <summary>
/// Builds MPEG-1 streams bit by bit from the syntax of ISO 11172-1 and 11172-2, so the decoder
/// tests have input that is not a trimmed piece of the player's install. The pictures are the
/// smallest legal ones that still exercise a real path: an intra picture whose blocks carry only
/// a DC difference, and a predicted picture whose macroblocks carry a zero motion vector and no
/// coefficients, which is a copy of the picture before it.
/// </summary>
internal static class MpegTestStreams
{
    /// <summary>The luma value every picture these streams carry decodes to. The first block of
    /// each picture codes a DC difference of one against the 128 a slice starts predicting
    /// from, and every later block repeats that prediction.</summary>
    public const int ExpectedLuma = 129;

    /// <summary>The chroma value every picture decodes to, which is the neutral prediction a
    /// slice starts with.</summary>
    public const int ExpectedChroma = 128;

    private const int SequenceHeaderCode = 0xB3;
    private const int SequenceEndCode = 0xB7;
    private const int PictureStartCode = 0x00;
    private const int FirstSliceCode = 0x01;

    /// <summary>A video elementary stream of intra pictures, all identical.</summary>
    public static byte[] IntraPictures(int width, int height, int frameRateCode, int pictureCount) =>
        VideoStream(width, height, frameRateCode, pictureCount, false);

    /// <summary>A video elementary stream whose first picture is intra and whose others predict
    /// it with a zero motion vector, so all of them decode to the same samples.</summary>
    public static byte[] IntraThenPredicted(int width, int height, int frameRateCode, int pictureCount) =>
        VideoStream(width, height, frameRateCode, pictureCount, true);

    /// <summary>Wraps elementary streams in a system stream: one pack, one video packet and, when
    /// audio bytes are given, one audio packet. Timestamps are on the 90 kHz system clock.</summary>
    public static byte[] SystemStream(byte[] video, long videoClock, byte[]? audio = null, long audioClock = 0)
    {
        var writer = new BitWriter();
        writer.StartCode(0xBA);
        writer.Write(0x2, 4);
        writer.Write(0, 3);
        writer.Write(1, 1);
        writer.Write(0, 15);
        writer.Write(1, 1);
        writer.Write(0, 15);
        writer.Write(1, 1);
        writer.Write(1, 1);
        writer.Write(50, 22);
        writer.Write(1, 1);

        WritePacket(writer, 0xE0, video, videoClock);
        if (audio != null)
        {
            WritePacket(writer, 0xC0, audio, audioClock);
        }

        writer.StartCode(0xB9);
        return writer.ToArray();
    }

    /// <summary>Packs a string of '0' and '1' into bytes, most significant bit first and padded
    /// with zeroes, so a test can spell a code out of the standard's own table.</summary>
    public static byte[] Bits(string bits)
    {
        var writer = new BitWriter();
        foreach (char bit in bits)
        {
            writer.Write(bit == '1' ? 1 : 0, 1);
        }

        return writer.ToArray();
    }

    private static byte[] VideoStream(int width, int height, int frameRateCode, int pictureCount, bool predict)
    {
        var writer = new BitWriter();
        writer.StartCode(SequenceHeaderCode);
        writer.Write(width, 12);
        writer.Write(height, 12);
        writer.Write(1, 4);
        writer.Write(frameRateCode, 4);
        writer.Write(0x3ffff, 18);
        writer.Write(1, 1);
        writer.Write(0, 10);
        writer.Write(0, 1);
        writer.Write(0, 1);
        writer.Write(0, 1);

        int macroblocks = ((width + 15) >> 4) * ((height + 15) >> 4);
        for (int picture = 0; picture < pictureCount; picture++)
        {
            bool predicted = predict && picture > 0;
            WritePicture(writer, picture, macroblocks, predicted);
        }

        writer.StartCode(SequenceEndCode);
        return writer.ToArray();
    }

    private static void WritePicture(BitWriter writer, int temporalReference, int macroblocks, bool predicted)
    {
        writer.StartCode(PictureStartCode);
        writer.Write(temporalReference, 10);
        writer.Write(predicted ? 2 : 1, 3);
        writer.Write(0xffff, 16);
        if (predicted)
        {
            writer.Write(0, 1);
            writer.Write(1, 3);
        }

        writer.StartCode(FirstSliceCode);
        writer.Write(8, 5);
        writer.Write(0, 1);
        for (int macroblock = 0; macroblock < macroblocks; macroblock++)
        {
            writer.Write(1, 1);
            if (predicted)
            {
                WritePredictedMacroblock(writer);
            }
            else
            {
                WriteIntraMacroblock(writer, macroblock == 0);
            }
        }
    }

    // Type 0x08 is forward motion with no coefficients, and the motion code for zero is a
    // single set bit, so the macroblock is a copy of the one at the same place in the reference.
    private static void WritePredictedMacroblock(BitWriter writer)
    {
        writer.Write(0x1, 3);
        writer.Write(1, 1);
        writer.Write(1, 1);
    }

    private static void WriteIntraMacroblock(BitWriter writer, bool carryDcDifference)
    {
        writer.Write(1, 1);
        for (int block = 0; block < 6; block++)
        {
            bool luma = block < 4;
            if (carryDcDifference && block == 0)
            {
                writer.Write(0x0, 2);
                writer.Write(1, 1);
            }
            else if (luma)
            {
                writer.Write(0x4, 3);
            }
            else
            {
                writer.Write(0x0, 2);
            }

            writer.Write(0x2, 2);
        }
    }

    private static void WritePacket(BitWriter writer, int streamId, byte[] payload, long clock)
    {
        writer.StartCode(streamId);
        writer.Write(payload.Length + 5, 16);
        writer.Write(0x2, 4);
        writer.Write((int)((clock >> 30) & 0x07), 3);
        writer.Write(1, 1);
        writer.Write((int)((clock >> 15) & 0x7fff), 15);
        writer.Write(1, 1);
        writer.Write((int)(clock & 0x7fff), 15);
        writer.Write(1, 1);
        foreach (byte value in payload)
        {
            writer.Write(value, 8);
        }
    }

    // A most-significant-bit-first bit writer. Every start code aligns first, which is what the
    // standard requires of them and what lets a payload be appended byte by byte.
    private sealed class BitWriter
    {
        private readonly List<byte> _bytes = new List<byte>();
        private int _partial;
        private int _partialBits;

        public void Write(int value, int count)
        {
            for (int bit = count - 1; bit >= 0; bit--)
            {
                _partial = (_partial << 1) | ((value >> bit) & 1);
                _partialBits++;
                if (_partialBits == 8)
                {
                    _bytes.Add((byte)_partial);
                    _partial = 0;
                    _partialBits = 0;
                }
            }
        }

        public void StartCode(int code)
        {
            Align();
            Write(0x000001, 24);
            Write(code, 8);
        }

        public byte[] ToArray()
        {
            Align();
            return _bytes.ToArray();
        }

        private void Align()
        {
            while (_partialBits != 0)
            {
                Write(0, 1);
            }
        }
    }
}
