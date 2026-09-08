using System;

namespace CSVM.Video;

/// <summary>
/// One entry of the layer II quantiser table (ISO 11172-3, table 3-B.4): how many levels a
/// subband sample is coded in, how many bits carry it, and whether one code word carries three
/// consecutive samples. <see cref="Levels"/> of zero is the subband no bits were allocated to,
/// which decodes to silence rather than to a sample.
/// </summary>
public readonly struct Layer2Quantizer
{
    internal Layer2Quantizer(int levels, int bits, bool grouped)
    {
        Levels = levels;
        Bits = bits;
        Grouped = grouped;
    }

    /// <summary>How many quantisation levels the sample is coded in, or zero for no allocation.</summary>
    public int Levels { get; }

    /// <summary>Bits in the code word, which carries one sample or three grouped ones.</summary>
    public int Bits { get; }

    /// <summary>Whether the code word carries three samples packed in base <see cref="Levels"/>.</summary>
    public bool Grouped { get; }

    /// <summary>Whether this subband carries samples at all.</summary>
    public bool IsAllocated => Levels != 0;
}

/// <summary>
/// The MPEG-1 audio layer II tables of ISO 11172-3 as data: the sample rate and bit rate the
/// frame header's indices name, the scale factor base, the four-step lookup from bit rate and
/// sample rate to a bit allocation table, and the quantiser table that allocation selects.
/// Only the MPEG-1 half is here, because the header parse rejects every other version before
/// reaching a table, and the two low-sample-rate rows of ISO 13818-3 are unreachable from it.
/// </summary>
public static class AudioLayer2Tables
{
    /// <summary>Subbands the polyphase filter bank splits the spectrum into.</summary>
    public const int SubbandCount = 32;

    /// <summary>Samples one layer II frame carries, per channel: 36 sub-blocks of 32.</summary>
    public const int SamplesPerFrame = 1152;

    // Sample rate by the header's two-bit index; index 3 is reserved and never reaches here.
    private static readonly int[] SampleRateData = { 44100, 48000, 32000 };

    // Bit rate in kbit/s by the header's four-bit index less one. Index 0 is the free format
    // and 15 is forbidden, and the header parse rejects both rather than indexing this.
    private static readonly int[] BitRateData =
    {
        32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384,
    };

    // 2^25 times 2 to the power of minus a third and minus two thirds: the three scale factors
    // a scale factor index interpolates between, before the shift its top two bits name.
    private static readonly int[] ScaleFactorBaseData = { 0x02000000, 0x01965FEA, 0x01428A30 };

    // Step one: bit rate per channel decides a class, from the bit rate index. The first row is
    // a single channel, the second a pair, where the same total rate buys half as much per one.
    private static readonly byte[] BitRateClassData =
    {
        0, 0, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        0, 0, 0, 0, 0, 0, 1, 1, 1, 2, 2, 2, 2, 2,
    };

    // Step two: class and sample rate select one of tables 3-B.2a to 3-B.2d, packed as the
    // subband limit in the low six bits and the allocation table's own index above them.
    private static readonly byte[] AllocationTableData =
    {
        8, 8, 12,
        27 | 64, 27 | 64, 27 | 64,
        30 | 64, 27 | 64, 30 | 64,
    };

    // Step three: how many bits carry one subband's allocation field, in the high nibble, and
    // which row of the step-four table that field indexes, in the low one. Row 0 is the
    // low-rate tables 3-B.2c and 3-B.2d, row 1 the high-rate 3-B.2a and 3-B.2b.
    private static readonly byte[] AllocationBitsData =
    {
        0x44, 0x44, 0x34, 0x34, 0x34, 0x34, 0x34, 0x34,
        0x34, 0x34, 0x34, 0x34, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,

        0x43, 0x43, 0x43, 0x42, 0x42, 0x42, 0x42, 0x42,
        0x42, 0x42, 0x42, 0x31, 0x31, 0x31, 0x31, 0x31,
        0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x31, 0x20,
        0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x00, 0x00,
    };

    // Step four: an allocation field's value, on the row step three named, becomes a quantiser
    // number. Zero means the subband carries no samples at all.
    private static readonly byte[] QuantizerIndexData =
    {
        0, 1, 2, 17, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 1, 2, 3, 4, 5, 6, 17, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 17,
        0, 1, 3, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17,
        0, 1, 2, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 17,
    };

    // Table 3-B.4, quantisers 1 to 17: levels, then the bits one code word spends. The three
    // that carry three samples in one word spend fewer bits than three separate ones would.
    private static readonly int[] QuantizerLevelData =
    {
        3, 5, 7, 9, 15, 31, 63, 127, 255, 511, 1023, 2047, 4095, 8191, 16383, 32767, 65535,
    };

    private static readonly byte[] QuantizerBitData =
    {
        5, 7, 3, 10, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
    };

    private static readonly bool[] QuantizerGroupedData =
    {
        true, true, false, true, false, false, false, false, false,
        false, false, false, false, false, false, false, false,
    };

    /// <summary>The three scale factors a scale factor index is built from.</summary>
    public static ReadOnlySpan<int> ScaleFactorBase => ScaleFactorBaseData;

    /// <summary>Sample rate in hertz for a header's sample rate index, or zero for the
    /// reserved one.</summary>
    public static int SampleRate(int index) =>
        index >= 0 && index < SampleRateData.Length ? SampleRateData[index] : 0;

    /// <summary>Bit rate in kbit/s for a header's bit rate index less one, or zero for the free
    /// format and the forbidden value, neither of which this decoder accepts.</summary>
    public static int BitRate(int index) =>
        index >= 0 && index < BitRateData.Length ? BitRateData[index] : 0;

    /// <summary>The bit rate class a stream of that many channels at that bit rate index falls
    /// in, which is what selects an allocation table.</summary>
    public static int BitRateClass(int channels, int bitRateIndex) =>
        BitRateClassData[((channels == 1 ? 0 : 1) * 14) + bitRateIndex];

    /// <summary>Which allocation table a class and sample rate select, as a row index into
    /// <see cref="AllocationBits"/>.</summary>
    public static int AllocationTableFor(int bitRateClass, int sampleRateIndex) =>
        AllocationTableData[(bitRateClass * 3) + sampleRateIndex] >> 6;

    /// <summary>How many subbands that class and sample rate code at all. The rest of the 32
    /// are silent, and no allocation field is coded for them.</summary>
    public static int SubbandLimitFor(int bitRateClass, int sampleRateIndex) =>
        AllocationTableData[(bitRateClass * 3) + sampleRateIndex] & 63;

    /// <summary>Bits the allocation field of that subband occupies, which varies down the
    /// spectrum because the upper subbands are coded coarsely.</summary>
    public static int AllocationBits(int table, int subband) =>
        AllocationBitsData[(table * SubbandCount) + subband] >> 4;

    /// <summary>The quantiser an allocation field's value selects, whose
    /// <see cref="Layer2Quantizer.Levels"/> is zero when the subband carries no samples.</summary>
    public static Layer2Quantizer QuantizerFor(int table, int subband, int allocation)
    {
        int row = AllocationBitsData[(table * SubbandCount) + subband] & 15;
        return Quantizer(QuantizerIndexData[(row * 16) + allocation]);
    }

    /// <summary>Quantiser 1 to 17 of table 3-B.4; zero is the unallocated subband.</summary>
    public static Layer2Quantizer Quantizer(int number) =>
        number == 0
            ? default
            : new Layer2Quantizer(
                QuantizerLevelData[number - 1],
                QuantizerBitData[number - 1],
                QuantizerGroupedData[number - 1]);
}
