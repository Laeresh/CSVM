using System;
using System.Collections.Generic;

namespace CSVM.Tests;

/// <summary>
/// Builds MPEG-1 streams bit by bit from the syntax of ISO 11172-1, 11172-2 and 11172-3, so the
/// decoder tests have input that is not a trimmed piece of the player's install. The pictures
/// are the smallest legal ones that still exercise a real path: an intra picture whose blocks
/// carry only a DC difference, and a predicted picture whose macroblocks carry a zero motion
/// vector and no coefficients. The sound frames allocate nothing but the lowest subband of the
/// first channel, which is enough to tell a decoded channel from a silent one.
/// </summary>
internal static class MpegTestStreams
{
    /// <summary>Layer II mode field: two channels coded apart.</summary>
    public const int ModeStereo = 0;

    /// <summary>Layer II mode field: two channels sharing the subbands above a bound.</summary>
    public const int ModeJointStereo = 1;

    /// <summary>Layer II mode field: one channel.</summary>
    public const int ModeMono = 3;

    /// <summary>The bit rate index of 64 kbit/s, which two of the ten cinemas carry.</summary>
    public const int BitRateIndex64 = 4;

    /// <summary>The bit rate index of 128 kbit/s.</summary>
    public const int BitRateIndex128 = 8;

    /// <summary>The sample rate index of 44.1 kHz, which all ten cinemas carry.</summary>
    public const int SampleRateIndex44100 = 0;
    /// <summary>The luma value every picture these streams carry decodes to. The first block of
    /// each picture codes a DC difference of one against the 128 a slice starts predicting
    /// from, and every later block repeats that prediction.</summary>
    public const int ExpectedLuma = 129;

    /// <summary>The chroma value every picture decodes to, which is the neutral prediction a
    /// slice starts with.</summary>
    public const int ExpectedChroma = 128;

    /// <summary>The sequence header's aspect ratio code for square pixels, which all ten
    /// cinemas carry.</summary>
    public const int SquarePixelCode = 1;

    /// <summary>Luma samples of the first of the eight blocks
    /// <see cref="IntraThenMotion"/> paints, before its own DC difference is applied. It is
    /// the neutral 128 a slice starts predicting from.</summary>
    public const int MotionFixtureBaseLuma = 128;

    /// <summary>Macroblocks across the picture <see cref="IntraThenMotion"/> builds, which is
    /// two of them side by side in one slice.</summary>
    public const int MotionFixtureMacroblocks = 2;

    private const int SequenceHeaderCode = 0xB3;
    private const int SequenceEndCode = 0xB7;
    private const int PictureStartCode = 0x00;
    private const int FirstSliceCode = 0x01;

    // Written from ISO 11172-3 rather than read from CSVM.Video, so a frame this builds and a
    // frame the decoder reads agree only where both match the standard.
    private static readonly int[] SampleRates = { 44100, 48000, 32000 };

    private static readonly int[] BitRates =
    {
        32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384,
    };

    // Bit rate per channel picks a class; the class and the sample rate pick a table, whose
    // subband limit is 8 or 12 at low rates and 27 or 30 at high ones.
    private static readonly int[] BitRateClasses =
    {
        0, 0, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        0, 0, 0, 0, 0, 0, 1, 1, 1, 2, 2, 2, 2, 2,
    };

    private static readonly int[] SubbandLimits = { 8, 8, 12, 27, 27, 27, 30, 27, 30 };

    // Bits in one subband's allocation field: the low-rate tables 3-B.2c and 3-B.2d first, the
    // high-rate 3-B.2a and 3-B.2b second.
    private static readonly int[] AllocationBits =
    {
        4, 4, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,

        4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 3, 3, 3, 3, 3,
        3, 3, 3, 3, 3, 3, 3, 2, 2, 2, 2, 2, 2, 2, 0, 0,
    };

    /// <summary>A video elementary stream of intra pictures, all identical.</summary>
    public static byte[] IntraPictures(int width, int height, int frameRateCode, int pictureCount) =>
        VideoStream(width, height, SquarePixelCode, frameRateCode, pictureCount, predict: false);

    /// <summary>A video elementary stream whose first picture is intra and whose others predict
    /// it with a zero motion vector, so all of them decode to the same samples.</summary>
    public static byte[] IntraThenPredicted(int width, int height, int frameRateCode, int pictureCount) =>
        VideoStream(width, height, SquarePixelCode, frameRateCode, pictureCount, predict: true);

    /// <summary>One intra picture whose sequence header carries those aspect ratio and frame
    /// rate codes, for checking the two tables the header indexes. Both are the raw field
    /// values, so zero and the reserved values can be built as well as the legal ones.</summary>
    public static byte[] SequenceWithCodes(int aspectCode, int frameRateCode) =>
        VideoStream(32, 16, aspectCode, frameRateCode, 1, predict: false);

    /// <summary>A 32x16 stream of an intra picture and a predicted one, for checking how a motion
    /// vector is reconstructed. Each of the eight luma blocks takes its own DC from
    /// <paramref name="lumaDcDeltas"/>, whose entries are of magnitude 8 to 15 so that all eight
    /// code in a four-bit field. Each macroblock's horizontal motion field is spelled out by the
    /// caller in <paramref name="horizontalMotionBits"/>, the code then
    /// <paramref name="fCode"/> - 1 residual bits; every vertical one is no change.</summary>
    public static byte[] IntraThenMotion(int fCode, string[] horizontalMotionBits, int[] lumaDcDeltas)
    {
        var writer = new BitWriter();
        writer.StartCode(SequenceHeaderCode);
        writer.Write(32, 12);
        writer.Write(16, 12);
        writer.Write(SquarePixelCode, 4);
        writer.Write(5, 4);
        writer.Write(0x3ffff, 18);
        writer.Write(1, 1);
        writer.Write(0, 10);
        writer.Write(0, 1);
        writer.Write(0, 1);
        writer.Write(0, 1);

        WriteMotionFixtureIntraPicture(writer, lumaDcDeltas);
        WriteMotionFixturePredictedPicture(writer, fCode, horizontalMotionBits);

        writer.StartCode(SequenceEndCode);
        return writer.ToArray();
    }

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

    /// <summary>An MPEG-1 audio layer II elementary stream of identical frames. Every subband is
    /// unallocated except, when <paramref name="excite"/> is set, the lowest one of the first
    /// channel, fed a constant code word under its coarsest quantiser. Its scale factors are
    /// coded under <paramref name="scaleFactorSelect"/>, the selection information, and
    /// <paramref name="scaleFactorIndices"/> holds exactly what that transmits: three for 0, two
    /// for 1 and 3, one for 2. Null is the single index zero, the loudest scale.</summary>
    public static byte[] Layer2Stream(
        int frameCount,
        int mode,
        int bitRateIndex,
        int sampleRateIndex,
        int modeExtension,
        bool excite,
        int scaleFactorSelect = 2,
        int[]? scaleFactorIndices = null)
    {
        int[] indices = scaleFactorIndices ?? new[] { 0 };
        var writer = new BitWriter();
        for (int frame = 0; frame < frameCount; frame++)
        {
            WriteLayer2Frame(
                writer, mode, bitRateIndex, sampleRateIndex, modeExtension, excite,
                scaleFactorSelect, indices);
        }

        return writer.ToArray();
    }

    /// <summary>Splits an elementary stream across that many audio packets of a system stream,
    /// which is what a real file does and what the demultiplexer has to join back.</summary>
    public static byte[] SystemStreamWithSplitAudio(
        byte[] video, byte[] audio, int audioPacketCount, long audioClock)
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

        WritePacket(writer, 0xE0, video, 0);
        int at = 0;
        for (int packet = 0; packet < audioPacketCount; packet++)
        {
            int remaining = audioPacketCount - packet;
            int length = (audio.Length - at + remaining - 1) / remaining;
            var payload = new byte[length];
            Array.Copy(audio, at, payload, 0, length);
            WritePacket(writer, 0xC0, payload, packet == 0 ? audioClock : 0);
            at += length;
        }

        writer.StartCode(0xB9);
        return writer.ToArray();
    }

    /// <summary>The subband limit ISO 11172-3 gives that mode, bit rate and sample rate, which
    /// is how many subbands carry an allocation field at all.</summary>
    public static int Layer2SubbandLimit(int mode, int bitRateIndex, int sampleRateIndex)
    {
        int channels = mode == ModeMono ? 1 : 2;
        int bitRateClass = BitRateClasses[((channels == 1 ? 0 : 1) * 14) + bitRateIndex - 1];
        return SubbandLimits[(bitRateClass * 3) + sampleRateIndex];
    }

    /// <summary>The frame length in bytes ISO 11172-3's formula gives that bit rate and sample
    /// rate, with no padding slot.</summary>
    public static int Layer2FrameSize(int bitRateIndex, int sampleRateIndex) =>
        144000 * BitRates[bitRateIndex - 1] / SampleRates[sampleRateIndex];

    // One frame: the 32-bit header with no check word, the allocation fields, the scale factor
    // selection and factors of whatever was allocated, then the twelve granules of samples.
    // Everything after that is the zero padding a frame is free to carry.
    private static void WriteLayer2Frame(
        BitWriter writer,
        int mode,
        int bitRateIndex,
        int sampleRateIndex,
        int modeExtension,
        bool excite,
        int scaleFactorSelect,
        int[] scaleFactorIndices)
    {
        int start = writer.ByteCount;
        writer.Write(0x7ff, 11);
        writer.Write(0x3, 2);
        writer.Write(0x2, 2);
        writer.Write(1, 1);
        writer.Write(bitRateIndex, 4);
        writer.Write(sampleRateIndex, 2);
        writer.Write(0, 1);
        writer.Write(0, 1);
        writer.Write(mode, 2);
        writer.Write(modeExtension, 2);
        writer.Write(0, 4);

        int channels = mode == ModeMono ? 1 : 2;
        int subbandLimit = Layer2SubbandLimit(mode, bitRateIndex, sampleRateIndex);
        int table = subbandLimit > 12 ? 1 : 0;
        int bound = mode == ModeJointStereo ? (modeExtension + 1) << 2 : (mode == ModeMono ? 0 : 32);
        bound = bound < subbandLimit ? bound : subbandLimit;

        for (int subband = 0; subband < bound; subband++)
        {
            for (int channel = 0; channel < channels; channel++)
            {
                writer.Write(excite && subband == 0 && channel == 0 ? 1 : 0, AllocationWidth(table, subband));
            }
        }

        for (int subband = bound; subband < subbandLimit; subband++)
        {
            writer.Write(excite && subband == 0 ? 1 : 0, AllocationWidth(table, subband));
        }

        if (excite)
        {
            writer.Write(scaleFactorSelect, 2);
            foreach (int index in scaleFactorIndices)
            {
                writer.Write(index, 6);
            }

            for (int granule = 0; granule < 12; granule++)
            {
                writer.Write(0, 5);
            }
        }

        writer.PadTo(start + Layer2FrameSize(bitRateIndex, sampleRateIndex));
    }

    private static int AllocationWidth(int table, int subband) => AllocationBits[(table * 32) + subband];

    // One intra picture of two macroblocks, each luma block carrying its own DC difference in a
    // four-bit field and each chroma block none. A negative difference is coded as the standard
    // spells it: the field's top bit clear, and the value one less than the difference's
    // distance below the field's own range.
    private static void WriteMotionFixtureIntraPicture(BitWriter writer, int[] lumaDcDeltas)
    {
        writer.StartCode(PictureStartCode);
        writer.Write(0, 10);
        writer.Write(1, 3);
        writer.Write(0xffff, 16);

        writer.StartCode(FirstSliceCode);
        writer.Write(8, 5);
        writer.Write(0, 1);
        for (int macroblock = 0; macroblock < MotionFixtureMacroblocks; macroblock++)
        {
            writer.Write(1, 1);
            writer.Write(1, 1);
            for (int block = 0; block < 4; block++)
            {
                int delta = lumaDcDeltas[(macroblock * 4) + block];
                writer.Write(0x6, 3);
                writer.Write(delta > 0 ? delta : delta + 15, 4);
                writer.Write(0x2, 2);
            }

            for (int block = 0; block < 2; block++)
            {
                writer.Write(0x0, 2);
                writer.Write(0x2, 2);
            }
        }
    }

    // One predicted picture whose macroblocks carry a forward vector and no coefficients. Type
    // 0x08 is the "motion forward only" code of the predictive macroblock table.
    private static void WriteMotionFixturePredictedPicture(BitWriter writer, int fCode, string[] horizontal)
    {
        writer.StartCode(PictureStartCode);
        writer.Write(1, 10);
        writer.Write(2, 3);
        writer.Write(0xffff, 16);
        writer.Write(0, 1);
        writer.Write(fCode, 3);

        writer.StartCode(FirstSliceCode);
        writer.Write(8, 5);
        writer.Write(0, 1);
        for (int macroblock = 0; macroblock < MotionFixtureMacroblocks; macroblock++)
        {
            writer.Write(1, 1);
            writer.Write(0x1, 3);
            foreach (char bit in horizontal[macroblock])
            {
                writer.Write(bit == '1' ? 1 : 0, 1);
            }

            writer.Write(1, 1);
        }
    }

    private static byte[] VideoStream(
        int width, int height, int aspectCode, int frameRateCode, int pictureCount, bool predict)
    {
        var writer = new BitWriter();
        writer.StartCode(SequenceHeaderCode);
        writer.Write(width, 12);
        writer.Write(height, 12);
        writer.Write(aspectCode, 4);
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

        public int ByteCount
        {
            get
            {
                Align();
                return _bytes.Count;
            }
        }

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

        public void PadTo(int byteCount)
        {
            Align();
            while (_bytes.Count < byteCount)
            {
                Write(0, 8);
            }
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
