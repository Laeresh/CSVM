using System;

namespace CSVM.Video;

/// <summary>
/// The MPEG-1 video variable-length code tables of ISO 11172-2, as data. Each table is a binary
/// tree flattened into consecutive (next, value) pairs, walked one bit at a time: a positive
/// next is the pair index the walk continues at, zero ends it and yields value, and -1 marks a
/// bit pattern the standard leaves unassigned. Pair index 0 is the root.
/// <see cref="MpegBitReader.ReadVlc"/> is the walk; <c>CSVM.Tests/VideoVlcTableTests.cs</c>
/// checks individual codes against the standard's own tables.
/// </summary>
public static class VideoVlcTables
{
    private static readonly int[] MacroblockAddressIncrementData =
    {
           2,   0,    0,   1,    4,   0,    6,   0,
           8,   0,   10,   0,    0,   3,    0,   2,
          12,   0,   14,   0,    0,   5,    0,   4,
          16,   0,   18,   0,    0,   7,    0,   6,
          20,   0,   22,   0,   24,   0,   26,   0,
          28,   0,   30,   0,   32,   0,   34,   0,
          36,   0,   38,   0,    0,   9,    0,   8,
          -1,   0,   40,   0,   -1,   0,   42,   0,
          44,   0,   46,   0,    0,  15,    0,  14,
           0,  13,    0,  12,    0,  11,    0,  10,
          48,   0,   50,   0,   52,   0,   54,   0,
          56,   0,   58,   0,   60,   0,   62,   0,
          64,   0,   -1,   0,   -1,   0,   66,   0,
          68,   0,   70,   0,   72,   0,   74,   0,
          76,   0,   78,   0,    0,  21,    0,  20,
           0,  19,    0,  18,    0,  17,    0,  16,
           0,  35,   -1,   0,   -1,   0,    0,  34,
           0,  33,    0,  32,    0,  31,    0,  30,
           0,  29,    0,  28,    0,  27,    0,  26,
           0,  25,    0,  24,    0,  23,    0,  22,
    };

    private static readonly int[] MacroblockTypeIntraData =
    {
           2,  0x00,    0,  0x01,   -1,  0x00,    0,  0x11,
    };

    private static readonly int[] MacroblockTypePredictiveData =
    {
           2,  0x00,    0,  0x0a,    4,  0x00,    0,  0x02,
           6,  0x00,    0,  0x08,    8,  0x00,   10,  0x00,
          12,  0x00,    0,  0x12,    0,  0x1a,    0,  0x01,
          -1,  0x00,    0,  0x11,
    };

    private static readonly int[] MacroblockTypeBidirectionalData =
    {
           2,  0x00,    4,  0x00,    6,  0x00,    8,  0x00,
           0,  0x0c,    0,  0x0e,   10,  0x00,   12,  0x00,
           0,  0x04,    0,  0x06,   14,  0x00,   16,  0x00,
           0,  0x08,    0,  0x0a,   18,  0x00,   20,  0x00,
           0,  0x1e,    0,  0x01,   -1,  0x00,    0,  0x11,
           0,  0x16,    0,  0x1a,
    };

    private static readonly int[] CodedBlockPatternData =
    {
           2,   0,    4,   0,    6,   0,    8,   0,
          10,   0,   12,   0,   14,   0,   16,   0,
          18,   0,   20,   0,   22,   0,   24,   0,
          26,   0,    0,  60,   28,   0,   30,   0,
          32,   0,   34,   0,   36,   0,   38,   0,
          40,   0,   42,   0,   44,   0,   46,   0,
           0,  32,    0,  16,    0,   8,    0,   4,
          48,   0,   50,   0,   52,   0,   54,   0,
          56,   0,   58,   0,   60,   0,   62,   0,
           0,  62,    0,   2,    0,  61,    0,   1,
           0,  56,    0,  52,    0,  44,    0,  28,
           0,  40,    0,  20,    0,  48,    0,  12,
          64,   0,   66,   0,   68,   0,   70,   0,
          72,   0,   74,   0,   76,   0,   78,   0,
          80,   0,   82,   0,   84,   0,   86,   0,
           0,  63,    0,   3,    0,  36,    0,  24,
          88,   0,   90,   0,   92,   0,   94,   0,
          96,   0,   98,   0,  100,   0,  102,   0,
         104,   0,  106,   0,  108,   0,  110,   0,
         112,   0,  114,   0,  116,   0,  118,   0,
           0,  34,    0,  18,    0,  10,    0,   6,
           0,  33,    0,  17,    0,   9,    0,   5,
          -1,   0,  120,   0,  122,   0,  124,   0,
           0,  58,    0,  54,    0,  46,    0,  30,
           0,  57,    0,  53,    0,  45,    0,  29,
           0,  38,    0,  26,    0,  37,    0,  25,
           0,  43,    0,  23,    0,  51,    0,  15,
           0,  42,    0,  22,    0,  50,    0,  14,
           0,  41,    0,  21,    0,  49,    0,  13,
           0,  35,    0,  19,    0,  11,    0,   7,
           0,  39,    0,  27,    0,  59,    0,  55,
           0,  47,    0,  31,
    };

    private static readonly int[] MotionVectorData =
    {
           2,   0,    0,   0,    4,   0,    6,   0,
           8,   0,   10,   0,    0,   1,    0,  -1,
          12,   0,   14,   0,    0,   2,    0,  -2,
          16,   0,   18,   0,    0,   3,    0,  -3,
          20,   0,   22,   0,   24,   0,   26,   0,
          -1,   0,   28,   0,   30,   0,   32,   0,
          34,   0,   36,   0,    0,   4,    0,  -4,
          -1,   0,   38,   0,   40,   0,   42,   0,
           0,   7,    0,  -7,    0,   6,    0,  -6,
           0,   5,    0,  -5,   44,   0,   46,   0,
          48,   0,   50,   0,   52,   0,   54,   0,
          56,   0,   58,   0,   60,   0,   62,   0,
          64,   0,   66,   0,    0,  10,    0, -10,
           0,   9,    0,  -9,    0,   8,    0,  -8,
           0,  16,    0, -16,    0,  15,    0, -15,
           0,  14,    0, -14,    0,  13,    0, -13,
           0,  12,    0, -12,    0,  11,    0, -11,
    };

    private static readonly int[] DctSizeLuminanceData =
    {
           2,   0,    4,   0,    0,   1,    0,   2,
           6,   0,    8,   0,    0,   0,    0,   3,
           0,   4,   10,   0,    0,   5,   12,   0,
           0,   6,   14,   0,    0,   7,   16,   0,
           0,   8,   -1,   0,
    };

    private static readonly int[] DctSizeChrominanceData =
    {
           2,   0,    4,   0,    0,   0,    0,   1,
           0,   2,    6,   0,    0,   3,    8,   0,
           0,   4,   10,   0,    0,   5,   12,   0,
           0,   6,   14,   0,    0,   7,   16,   0,
           0,   8,   -1,   0,
    };

    private static readonly int[] DctCoefficientData =
    {
           2,0x0000,    0,0x0001,    4,0x0000,    6,0x0000,
           8,0x0000,   10,0x0000,   12,0x0000,    0,0x0101,
          14,0x0000,   16,0x0000,   18,0x0000,   20,0x0000,
           0,0x0002,    0,0x0201,   22,0x0000,   24,0x0000,
          26,0x0000,   28,0x0000,   30,0x0000,    0,0x0003,
           0,0x0401,    0,0x0301,   32,0x0000,    0,0xffff,
          34,0x0000,   36,0x0000,    0,0x0701,    0,0x0601,
           0,0x0102,    0,0x0501,   38,0x0000,   40,0x0000,
          42,0x0000,   44,0x0000,    0,0x0202,    0,0x0901,
           0,0x0004,    0,0x0801,   46,0x0000,   48,0x0000,
          50,0x0000,   52,0x0000,   54,0x0000,   56,0x0000,
          58,0x0000,   60,0x0000,    0,0x0d01,    0,0x0006,
           0,0x0c01,    0,0x0b01,    0,0x0302,    0,0x0103,
           0,0x0005,    0,0x0a01,   62,0x0000,   64,0x0000,
          66,0x0000,   68,0x0000,   70,0x0000,   72,0x0000,
          74,0x0000,   76,0x0000,   78,0x0000,   80,0x0000,
          82,0x0000,   84,0x0000,   86,0x0000,   88,0x0000,
          90,0x0000,   92,0x0000,    0,0x1001,    0,0x0502,
           0,0x0007,    0,0x0203,    0,0x0104,    0,0x0f01,
           0,0x0e01,    0,0x0402,   94,0x0000,   96,0x0000,
          98,0x0000,  100,0x0000,  102,0x0000,  104,0x0000,
         106,0x0000,  108,0x0000,  110,0x0000,  112,0x0000,
         114,0x0000,  116,0x0000,  118,0x0000,  120,0x0000,
         122,0x0000,  124,0x0000,   -1,0x0000,  126,0x0000,
         128,0x0000,  130,0x0000,  132,0x0000,  134,0x0000,
         136,0x0000,  138,0x0000,  140,0x0000,  142,0x0000,
         144,0x0000,  146,0x0000,  148,0x0000,  150,0x0000,
         152,0x0000,  154,0x0000,    0,0x000b,    0,0x0802,
           0,0x0403,    0,0x000a,    0,0x0204,    0,0x0702,
           0,0x1501,    0,0x1401,    0,0x0009,    0,0x1301,
           0,0x1201,    0,0x0105,    0,0x0303,    0,0x0008,
           0,0x0602,    0,0x1101,  156,0x0000,  158,0x0000,
         160,0x0000,  162,0x0000,  164,0x0000,  166,0x0000,
         168,0x0000,  170,0x0000,  172,0x0000,  174,0x0000,
         176,0x0000,  178,0x0000,  180,0x0000,  182,0x0000,
           0,0x0a02,    0,0x0902,    0,0x0503,    0,0x0304,
           0,0x0205,    0,0x0107,    0,0x0106,    0,0x000f,
           0,0x000e,    0,0x000d,    0,0x000c,    0,0x1a01,
           0,0x1901,    0,0x1801,    0,0x1701,    0,0x1601,
         184,0x0000,  186,0x0000,  188,0x0000,  190,0x0000,
         192,0x0000,  194,0x0000,  196,0x0000,  198,0x0000,
         200,0x0000,  202,0x0000,  204,0x0000,  206,0x0000,
           0,0x001f,    0,0x001e,    0,0x001d,    0,0x001c,
           0,0x001b,    0,0x001a,    0,0x0019,    0,0x0018,
           0,0x0017,    0,0x0016,    0,0x0015,    0,0x0014,
           0,0x0013,    0,0x0012,    0,0x0011,    0,0x0010,
         208,0x0000,  210,0x0000,  212,0x0000,  214,0x0000,
         216,0x0000,  218,0x0000,  220,0x0000,  222,0x0000,
           0,0x0028,    0,0x0027,    0,0x0026,    0,0x0025,
           0,0x0024,    0,0x0023,    0,0x0022,    0,0x0021,
           0,0x0020,    0,0x010e,    0,0x010d,    0,0x010c,
           0,0x010b,    0,0x010a,    0,0x0109,    0,0x0108,
           0,0x0112,    0,0x0111,    0,0x0110,    0,0x010f,
           0,0x0603,    0,0x1002,    0,0x0f02,    0,0x0e02,
           0,0x0d02,    0,0x0c02,    0,0x0b02,    0,0x1f01,
           0,0x1e01,    0,0x1d01,    0,0x1c01,    0,0x1b01,
    };

    /// <summary>How many macroblocks to advance before the one now being decoded. The two
    /// reserved values are the standard's own: 34 is stuffing and 35 is an escape worth 33.</summary>
    public static ReadOnlySpan<int> MacroblockAddressIncrement => MacroblockAddressIncrementData;

    /// <summary>Which of the six blocks of a non-intra macroblock carry coefficients, as a
    /// six-bit mask read most significant block first.</summary>
    public static ReadOnlySpan<int> CodedBlockPattern => CodedBlockPatternData;

    /// <summary>One component of a motion vector, before the residual bits and the range
    /// wrap that <c>MpegVideoDecoder</c> applies.</summary>
    public static ReadOnlySpan<int> MotionVector => MotionVectorData;

    /// <summary>A run/level pair packed as (run &lt;&lt; 8) | level, unsigned, with the sign bit
    /// following in the stream. 0xffff is the escape that spells run and level out in full.</summary>
    public static ReadOnlySpan<int> DctCoefficient => DctCoefficientData;

    /// <summary>The bit count of an intra block's DC difference, keyed by plane: 0 luma,
    /// 1 and 2 the two chroma planes.</summary>
    public static ReadOnlySpan<int> DctSizeFor(int planeIndex) =>
        planeIndex == 0 ? DctSizeLuminanceData : DctSizeChrominanceData;

    /// <summary>The macroblock type table for a picture coding type: 1 intra, 2 predictive,
    /// 3 bidirectional. The decoded value is a bit set, not an ordinal.</summary>
    public static ReadOnlySpan<int> MacroblockTypeFor(int pictureType) => pictureType switch
    {
        1 => MacroblockTypeIntraData,
        2 => MacroblockTypePredictiveData,
        3 => MacroblockTypeBidirectionalData,
        _ => ReadOnlySpan<int>.Empty,
    };
}
