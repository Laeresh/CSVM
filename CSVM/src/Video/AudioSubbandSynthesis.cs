using System;

namespace CSVM.Video;

/// <summary>
/// The layer II polyphase synthesis filter bank of ISO 11172-3 for one channel: 32 subband
/// samples in, 32 PCM samples out, carried across the 1024-sample history the standard's
/// windowing runs over. One instance per channel, because that history is a per-channel filter
/// state; <see cref="Reset"/> clears it, which is what makes a replay of the same stream produce
/// the same samples.
/// </summary>
public sealed class AudioSubbandSynthesis
{
    private const int HistorySize = 1024;

    // U carries the requantiser's and the window's combined fixed-point gain. The divisor is
    // negative because the requantiser subtracts each sample from the middle of its range and
    // so hands the filter bank an inverted signal.
    private const float SampleDivisor = -1090519040.0f;

    // ISO 11172-3's D window, every coefficient multiplied by 32768, which makes each one a
    // whole or half integer. The sign the standard states as a rule of its windowing is folded
    // in here, so the walk that reads it multiplies and adds with no case for the rule.
    private static readonly float[] WindowData =
    {
        0.0f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -1.0f,
        -1.0f, -1.0f, -1.0f, -1.5f, -1.5f, -2.0f, -2.0f, -2.5f,
        -2.5f, -3.0f, -3.5f, -3.5f, -4.0f, -4.5f, -5.0f, -5.5f,
        -6.5f, -7.0f, -8.0f, -8.5f, -9.5f, -10.5f, -12.0f, -13.0f,
        -14.5f, -15.5f, -17.5f, -19.0f, -20.5f, -22.5f, -24.5f, -26.5f,
        -29.0f, -31.5f, -34.0f, -36.5f, -39.5f, -42.5f, -45.5f, -48.5f,
        -52.0f, -55.5f, -58.5f, -62.5f, -66.0f, -69.5f, -73.5f, -77.0f,
        -80.5f, -84.5f, -88.0f, -91.5f, -95.0f, -98.0f, -101.0f, -104.0f,
        106.5f, 109.0f, 111.0f, 112.5f, 113.5f, 114.0f, 114.0f, 113.5f,
        112.0f, 110.5f, 107.5f, 104.0f, 100.0f, 94.5f, 88.5f, 81.5f,
        73.0f, 63.5f, 53.0f, 41.5f, 28.5f, 14.5f, -1.0f, -18.0f,
        -36.0f, -55.5f, -76.5f, -98.5f, -122.0f, -147.0f, -173.5f, -200.5f,
        -229.5f, -259.5f, -290.5f, -322.5f, -355.5f, -389.5f, -424.0f, -459.5f,
        -495.5f, -532.0f, -568.5f, -605.0f, -641.5f, -678.0f, -714.0f, -749.0f,
        -783.5f, -817.0f, -849.0f, -879.5f, -908.5f, -935.0f, -959.5f, -981.0f,
        -1000.5f, -1016.0f, -1028.5f, -1037.5f, -1042.5f, -1043.5f, -1040.0f, -1031.5f,
        1018.5f, 1000.0f, 976.0f, 946.5f, 911.0f, 869.5f, 822.0f, 767.5f,
        707.0f, 640.0f, 565.5f, 485.0f, 397.0f, 302.5f, 201.0f, 92.5f,
        -22.5f, -144.0f, -272.5f, -407.0f, -547.5f, -694.0f, -846.0f, -1003.0f,
        -1165.0f, -1331.5f, -1502.0f, -1675.5f, -1852.5f, -2031.5f, -2212.5f, -2394.0f,
        -2576.5f, -2758.5f, -2939.5f, -3118.5f, -3294.5f, -3467.5f, -3635.5f, -3798.5f,
        -3955.0f, -4104.5f, -4245.5f, -4377.5f, -4499.0f, -4609.5f, -4708.0f, -4792.5f,
        -4863.5f, -4919.0f, -4958.0f, -4979.5f, -4983.0f, -4967.5f, -4931.5f, -4875.0f,
        -4796.0f, -4694.5f, -4569.5f, -4420.0f, -4246.0f, -4046.0f, -3820.0f, -3567.0f,
        3287.0f, 2979.5f, 2644.0f, 2280.5f, 1888.0f, 1467.5f, 1018.5f, 541.0f,
        35.0f, -499.0f, -1061.0f, -1650.0f, -2266.5f, -2909.0f, -3577.0f, -4270.0f,
        -4987.5f, -5727.5f, -6490.0f, -7274.0f, -8077.5f, -8899.5f, -9739.0f, -10594.5f,
        -11464.5f, -12347.0f, -13241.0f, -14144.5f, -15056.0f, -15973.5f, -16895.5f, -17820.0f,
        -18744.5f, -19668.0f, -20588.0f, -21503.0f, -22410.5f, -23308.5f, -24195.0f, -25068.5f,
        -25926.5f, -26767.0f, -27589.0f, -28389.0f, -29166.5f, -29919.0f, -30644.5f, -31342.0f,
        -32009.5f, -32645.0f, -33247.0f, -33814.5f, -34346.0f, -34839.5f, -35295.0f, -35710.0f,
        -36084.5f, -36417.5f, -36707.5f, -36954.0f, -37156.5f, -37315.0f, -37428.0f, -37496.0f,
        37519.0f, 37496.0f, 37428.0f, 37315.0f, 37156.5f, 36954.0f, 36707.5f, 36417.5f,
        36084.5f, 35710.0f, 35295.0f, 34839.5f, 34346.0f, 33814.5f, 33247.0f, 32645.0f,
        32009.5f, 31342.0f, 30644.5f, 29919.0f, 29166.5f, 28389.0f, 27589.0f, 26767.0f,
        25926.5f, 25068.5f, 24195.0f, 23308.5f, 22410.5f, 21503.0f, 20588.0f, 19668.0f,
        18744.5f, 17820.0f, 16895.5f, 15973.5f, 15056.0f, 14144.5f, 13241.0f, 12347.0f,
        11464.5f, 10594.5f, 9739.0f, 8899.5f, 8077.5f, 7274.0f, 6490.0f, 5727.5f,
        4987.5f, 4270.0f, 3577.0f, 2909.0f, 2266.5f, 1650.0f, 1061.0f, 499.0f,
        -35.0f, -541.0f, -1018.5f, -1467.5f, -1888.0f, -2280.5f, -2644.0f, -2979.5f,
        3287.0f, 3567.0f, 3820.0f, 4046.0f, 4246.0f, 4420.0f, 4569.5f, 4694.5f,
        4796.0f, 4875.0f, 4931.5f, 4967.5f, 4983.0f, 4979.5f, 4958.0f, 4919.0f,
        4863.5f, 4792.5f, 4708.0f, 4609.5f, 4499.0f, 4377.5f, 4245.5f, 4104.5f,
        3955.0f, 3798.5f, 3635.5f, 3467.5f, 3294.5f, 3118.5f, 2939.5f, 2758.5f,
        2576.5f, 2394.0f, 2212.5f, 2031.5f, 1852.5f, 1675.5f, 1502.0f, 1331.5f,
        1165.0f, 1003.0f, 846.0f, 694.0f, 547.5f, 407.0f, 272.5f, 144.0f,
        22.5f, -92.5f, -201.0f, -302.5f, -397.0f, -485.0f, -565.5f, -640.0f,
        -707.0f, -767.5f, -822.0f, -869.5f, -911.0f, -946.5f, -976.0f, -1000.0f,
        1018.5f, 1031.5f, 1040.0f, 1043.5f, 1042.5f, 1037.5f, 1028.5f, 1016.0f,
        1000.5f, 981.0f, 959.5f, 935.0f, 908.5f, 879.5f, 849.0f, 817.0f,
        783.5f, 749.0f, 714.0f, 678.0f, 641.5f, 605.0f, 568.5f, 532.0f,
        495.5f, 459.5f, 424.0f, 389.5f, 355.5f, 322.5f, 290.5f, 259.5f,
        229.5f, 200.5f, 173.5f, 147.0f, 122.0f, 98.5f, 76.5f, 55.5f,
        36.0f, 18.0f, 1.0f, -14.5f, -28.5f, -41.5f, -53.0f, -63.5f,
        -73.0f, -81.5f, -88.5f, -94.5f, -100.0f, -104.0f, -107.5f, -110.5f,
        -112.0f, -113.5f, -114.0f, -114.0f, -113.5f, -112.5f, -111.0f, -109.0f,
        106.5f, 104.0f, 101.0f, 98.0f, 95.0f, 91.5f, 88.0f, 84.5f,
        80.5f, 77.0f, 73.5f, 69.5f, 66.0f, 62.5f, 58.5f, 55.5f,
        52.0f, 48.5f, 45.5f, 42.5f, 39.5f, 36.5f, 34.0f, 31.5f,
        29.0f, 26.5f, 24.5f, 22.5f, 20.5f, 19.0f, 17.5f, 15.5f,
        14.5f, 13.0f, 12.0f, 10.5f, 9.5f, 8.5f, 8.0f, 7.0f,
        6.5f, 5.5f, 5.0f, 4.5f, 4.0f, 3.5f, 3.5f, 3.0f,
        2.5f, 2.5f, 2.0f, 2.0f, 1.5f, 1.5f, 1.0f, 1.0f,
        1.0f, 1.0f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f,
    };

    private static readonly float[] DoubledWindowData = BuildDoubledWindow();

    private readonly float[] _history = new float[HistorySize];
    private readonly float[] _windowed = new float[AudioLayer2Tables.SubbandCount];
    private int _position;

    /// <summary>ISO 11172-3's D window, its 512 coefficients each multiplied by 32768.</summary>
    public static ReadOnlySpan<float> Window => WindowData;

    /// <summary>Clears the filter history, so the next samples carry nothing of the last ones.</summary>
    public void Reset()
    {
        Array.Clear(_history, 0, _history.Length);
        _position = 0;
    }

    /// <summary>Turns one sub-block of 32 subband samples into 32 consecutive PCM samples,
    /// nominally within plus or minus one. Loud material can leave a sample slightly outside
    /// that, so a caller feeding fixed-point hardware clamps.</summary>
    public void Synthesize(ReadOnlySpan<int> subbandSamples, Span<float> output)
    {
        _position = (_position - 64) & (HistorySize - 1);
        Transform(subbandSamples, _history, _position);

        Array.Clear(_windowed, 0, _windowed.Length);
        int windowIndex = 512 - (_position >> 1);
        int historyIndex = (_position % 128) >> 1;
        Accumulate(ref windowIndex, ref historyIndex);

        windowIndex -= 512 - 32;
        historyIndex = (128 - 32 + HistorySize) - historyIndex;
        Accumulate(ref windowIndex, ref historyIndex);

        for (int i = 0; i < AudioLayer2Tables.SubbandCount; i++)
        {
            output[i] = _windowed[i] / SampleDivisor;
        }
    }

    // The 512 coefficients laid down twice, so the windowing walk can run 512 entries past its
    // start without a wrap test on every step.
    private static float[] BuildDoubledWindow()
    {
        var doubled = new float[WindowData.Length * 2];
        WindowData.CopyTo(doubled, 0);
        WindowData.CopyTo(doubled, WindowData.Length);
        return doubled;
    }

    // The 32-point inverse cosine transform of the filter bank, factored into butterflies. The
    // 64 values it writes are one sub-block of the history the windowing then reads back.
    private static void Transform(ReadOnlySpan<int> s, float[] d, int dp)
    {
        float t01, t02, t03, t04, t05, t06, t07, t08, t09, t10, t11;
        float t12, t13, t14, t15, t16, t17, t18, t19, t20, t21, t22;
        float t23, t24, t25, t26, t27, t28, t29, t30, t31, t32, t33;

        t01 = (float)(s[0] + s[31]);
        t02 = (float)(s[0] - s[31]) * 0.500602998235f;
        t03 = (float)(s[1] + s[30]);
        t04 = (float)(s[1] - s[30]) * 0.505470959898f;
        t05 = (float)(s[2] + s[29]);
        t06 = (float)(s[2] - s[29]) * 0.515447309923f;
        t07 = (float)(s[3] + s[28]);
        t08 = (float)(s[3] - s[28]) * 0.53104259109f;
        t09 = (float)(s[4] + s[27]);
        t10 = (float)(s[4] - s[27]) * 0.553103896034f;
        t11 = (float)(s[5] + s[26]);
        t12 = (float)(s[5] - s[26]) * 0.582934968206f;
        t13 = (float)(s[6] + s[25]);
        t14 = (float)(s[6] - s[25]) * 0.622504123036f;
        t15 = (float)(s[7] + s[24]);
        t16 = (float)(s[7] - s[24]) * 0.674808341455f;
        t17 = (float)(s[8] + s[23]);
        t18 = (float)(s[8] - s[23]) * 0.744536271002f;
        t19 = (float)(s[9] + s[22]);
        t20 = (float)(s[9] - s[22]) * 0.839349645416f;
        t21 = (float)(s[10] + s[21]);
        t22 = (float)(s[10] - s[21]) * 0.972568237862f;
        t23 = (float)(s[11] + s[20]);
        t24 = (float)(s[11] - s[20]) * 1.16943993343f;
        t25 = (float)(s[12] + s[19]);
        t26 = (float)(s[12] - s[19]) * 1.48416461631f;
        t27 = (float)(s[13] + s[18]);
        t28 = (float)(s[13] - s[18]) * 2.05778100995f;
        t29 = (float)(s[14] + s[17]);
        t30 = (float)(s[14] - s[17]) * 3.40760841847f;
        t31 = (float)(s[15] + s[16]);
        t32 = (float)(s[15] - s[16]) * 10.1900081235f;

        t33 = t01 + t31;
        t31 = (t01 - t31) * 0.502419286188f;
        t01 = t03 + t29;
        t29 = (t03 - t29) * 0.52249861494f;
        t03 = t05 + t27;
        t27 = (t05 - t27) * 0.566944034816f;
        t05 = t07 + t25;
        t25 = (t07 - t25) * 0.64682178336f;
        t07 = t09 + t23;
        t23 = (t09 - t23) * 0.788154623451f;
        t09 = t11 + t21;
        t21 = (t11 - t21) * 1.06067768599f;
        t11 = t13 + t19;
        t19 = (t13 - t19) * 1.72244709824f;
        t13 = t15 + t17;
        t17 = (t15 - t17) * 5.10114861869f;
        t15 = t33 + t13;
        t13 = (t33 - t13) * 0.509795579104f;
        t33 = t01 + t11;
        t01 = (t01 - t11) * 0.601344886935f;
        t11 = t03 + t09;
        t09 = (t03 - t09) * 0.899976223136f;
        t03 = t05 + t07;
        t07 = (t05 - t07) * 2.56291544774f;
        t05 = t15 + t03;
        t15 = (t15 - t03) * 0.541196100146f;
        t03 = t33 + t11;
        t11 = (t33 - t11) * 1.30656296488f;
        t33 = t05 + t03;
        t05 = (t05 - t03) * 0.707106781187f;
        t03 = t15 + t11;
        t15 = (t15 - t11) * 0.707106781187f;
        t03 += t15;
        t11 = t13 + t07;
        t13 = (t13 - t07) * 0.541196100146f;
        t07 = t01 + t09;
        t09 = (t01 - t09) * 1.30656296488f;
        t01 = t11 + t07;
        t07 = (t11 - t07) * 0.707106781187f;
        t11 = t13 + t09;
        t13 = (t13 - t09) * 0.707106781187f;
        t11 += t13;
        t01 += t11;
        t11 += t07;
        t07 += t13;
        t09 = t31 + t17;
        t31 = (t31 - t17) * 0.509795579104f;
        t17 = t29 + t19;
        t29 = (t29 - t19) * 0.601344886935f;
        t19 = t27 + t21;
        t21 = (t27 - t21) * 0.899976223136f;
        t27 = t25 + t23;
        t23 = (t25 - t23) * 2.56291544774f;
        t25 = t09 + t27;
        t09 = (t09 - t27) * 0.541196100146f;
        t27 = t17 + t19;
        t19 = (t17 - t19) * 1.30656296488f;
        t17 = t25 + t27;
        t27 = (t25 - t27) * 0.707106781187f;
        t25 = t09 + t19;
        t19 = (t09 - t19) * 0.707106781187f;
        t25 += t19;
        t09 = t31 + t23;
        t31 = (t31 - t23) * 0.541196100146f;
        t23 = t29 + t21;
        t21 = (t29 - t21) * 1.30656296488f;
        t29 = t09 + t23;
        t23 = (t09 - t23) * 0.707106781187f;
        t09 = t31 + t21;
        t31 = (t31 - t21) * 0.707106781187f;
        t09 += t31;
        t29 += t09;
        t09 += t23;
        t23 += t31;
        t17 += t29;
        t29 += t25;
        t25 += t09;
        t09 += t27;
        t27 += t23;
        t23 += t19;
        t19 += t31;
        t21 = t02 + t32;
        t02 = (t02 - t32) * 0.502419286188f;
        t32 = t04 + t30;
        t04 = (t04 - t30) * 0.52249861494f;
        t30 = t06 + t28;
        t28 = (t06 - t28) * 0.566944034816f;
        t06 = t08 + t26;
        t08 = (t08 - t26) * 0.64682178336f;
        t26 = t10 + t24;
        t10 = (t10 - t24) * 0.788154623451f;
        t24 = t12 + t22;
        t22 = (t12 - t22) * 1.06067768599f;
        t12 = t14 + t20;
        t20 = (t14 - t20) * 1.72244709824f;
        t14 = t16 + t18;
        t16 = (t16 - t18) * 5.10114861869f;
        t18 = t21 + t14;
        t14 = (t21 - t14) * 0.509795579104f;
        t21 = t32 + t12;
        t32 = (t32 - t12) * 0.601344886935f;
        t12 = t30 + t24;
        t24 = (t30 - t24) * 0.899976223136f;
        t30 = t06 + t26;
        t26 = (t06 - t26) * 2.56291544774f;
        t06 = t18 + t30;
        t18 = (t18 - t30) * 0.541196100146f;
        t30 = t21 + t12;
        t12 = (t21 - t12) * 1.30656296488f;
        t21 = t06 + t30;
        t30 = (t06 - t30) * 0.707106781187f;
        t06 = t18 + t12;
        t12 = (t18 - t12) * 0.707106781187f;
        t06 += t12;
        t18 = t14 + t26;
        t26 = (t14 - t26) * 0.541196100146f;
        t14 = t32 + t24;
        t24 = (t32 - t24) * 1.30656296488f;
        t32 = t18 + t14;
        t14 = (t18 - t14) * 0.707106781187f;
        t18 = t26 + t24;
        t24 = (t26 - t24) * 0.707106781187f;
        t18 += t24;
        t32 += t18;
        t18 += t14;
        t26 = t14 + t24;
        t14 = t02 + t16;
        t02 = (t02 - t16) * 0.509795579104f;
        t16 = t04 + t20;
        t04 = (t04 - t20) * 0.601344886935f;
        t20 = t28 + t22;
        t22 = (t28 - t22) * 0.899976223136f;
        t28 = t08 + t10;
        t10 = (t08 - t10) * 2.56291544774f;
        t08 = t14 + t28;
        t14 = (t14 - t28) * 0.541196100146f;
        t28 = t16 + t20;
        t20 = (t16 - t20) * 1.30656296488f;
        t16 = t08 + t28;
        t28 = (t08 - t28) * 0.707106781187f;
        t08 = t14 + t20;
        t20 = (t14 - t20) * 0.707106781187f;
        t08 += t20;
        t14 = t02 + t10;
        t02 = (t02 - t10) * 0.541196100146f;
        t10 = t04 + t22;
        t22 = (t04 - t22) * 1.30656296488f;
        t04 = t14 + t10;
        t10 = (t14 - t10) * 0.707106781187f;
        t14 = t02 + t22;
        t02 = (t02 - t22) * 0.707106781187f;
        t14 += t02;
        t04 += t14;
        t14 += t10;
        t10 += t02;
        t16 += t04;
        t04 += t08;
        t08 += t14;
        t14 += t28;
        t28 += t10;
        t10 += t20;
        t20 += t02;
        t21 += t16;
        t16 += t32;
        t32 += t04;
        t04 += t06;
        t06 += t08;
        t08 += t18;
        t18 += t14;
        t14 += t30;
        t30 += t28;
        t28 += t26;
        t26 += t10;
        t10 += t12;
        t12 += t20;
        t20 += t24;
        t24 += t02;

        d[dp + 48] = -t33;
        d[dp + 49] = d[dp + 47] = -t21;
        d[dp + 50] = d[dp + 46] = -t17;
        d[dp + 51] = d[dp + 45] = -t16;
        d[dp + 52] = d[dp + 44] = -t01;
        d[dp + 53] = d[dp + 43] = -t32;
        d[dp + 54] = d[dp + 42] = -t29;
        d[dp + 55] = d[dp + 41] = -t04;
        d[dp + 56] = d[dp + 40] = -t03;
        d[dp + 57] = d[dp + 39] = -t06;
        d[dp + 58] = d[dp + 38] = -t25;
        d[dp + 59] = d[dp + 37] = -t08;
        d[dp + 60] = d[dp + 36] = -t11;
        d[dp + 61] = d[dp + 35] = -t18;
        d[dp + 62] = d[dp + 34] = -t09;
        d[dp + 63] = d[dp + 33] = -t14;
        d[dp + 32] = -t05;
        d[dp + 0] = t05;
        d[dp + 31] = -t30;
        d[dp + 1] = t30;
        d[dp + 30] = -t27;
        d[dp + 2] = t27;
        d[dp + 29] = -t28;
        d[dp + 3] = t28;
        d[dp + 28] = -t07;
        d[dp + 4] = t07;
        d[dp + 27] = -t26;
        d[dp + 5] = t26;
        d[dp + 26] = -t23;
        d[dp + 6] = t23;
        d[dp + 25] = -t10;
        d[dp + 7] = t10;
        d[dp + 24] = -t15;
        d[dp + 8] = t15;
        d[dp + 23] = -t12;
        d[dp + 9] = t12;
        d[dp + 22] = -t19;
        d[dp + 10] = t19;
        d[dp + 21] = -t20;
        d[dp + 11] = t20;
        d[dp + 20] = -t13;
        d[dp + 12] = t13;
        d[dp + 19] = -t24;
        d[dp + 13] = t24;
        d[dp + 18] = -t31;
        d[dp + 14] = t31;
        d[dp + 17] = -t02;
        d[dp + 15] = t02;
        d[dp + 16] = 0.0f;
    }

    // The windowing walk takes 32 coefficients out of every 64 and 32 history samples out of
    // every 128, which is the standard's decimation written as a stride rather than a test.
    private void Accumulate(ref int windowIndex, ref int historyIndex)
    {
        while (historyIndex < HistorySize)
        {
            for (int i = 0; i < AudioLayer2Tables.SubbandCount; i++)
            {
                _windowed[i] += DoubledWindowData[windowIndex++] * _history[historyIndex++];
            }

            historyIndex += 128 - 32;
            windowIndex += 64 - 32;
        }
    }
}
