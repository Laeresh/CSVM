using System;
using CSVM.Video;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The layer II tables against ISO 11172-3's own, spelled out here rather than read back from
/// the module they check. A table that is wrong by one entry decodes to plausible noise instead
/// of to an error, and there is no reference decoder to compare output with, so this is where
/// the standard is actually enforced.
/// </summary>
public class AudioLayer2TableTests
{
    [Theory]
    [InlineData(0, 44100)]
    [InlineData(1, 48000)]
    [InlineData(2, 32000)]
    [InlineData(3, 0)]
    public void TheSampleRateIndexNamesTheStandardsThreeRates(int index, int expected) =>
        Assert.Equal(expected, AudioLayer2Tables.SampleRate(index));

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 32)]
    [InlineData(3, 64)]
    [InlineData(7, 128)]
    [InlineData(13, 384)]
    [InlineData(14, 0)]
    public void TheBitRateIndexNamesTheStandardsFourteenRates(int index, int expected) =>
        Assert.Equal(expected, AudioLayer2Tables.BitRate(index));

    /// <summary>The three bases are 2^25 times one, the cube root of one half, and its square,
    /// which is what makes each scale factor the one below it divided by the cube root of two.</summary>
    [Fact]
    public void TheScaleFactorBasesAreThePowersOfTwoTheStandardTabulates()
    {
        Assert.Equal(3, AudioLayer2Tables.ScaleFactorBase.Length);
        for (int index = 0; index < 3; index++)
        {
            double expected = Math.Pow(2.0, 25.0 - (index / 3.0));
            Assert.True(
                Math.Abs(AudioLayer2Tables.ScaleFactorBase[index] - expected) <= 1.0,
                $"base {index} is {AudioLayer2Tables.ScaleFactorBase[index]}, not {expected}");
        }
    }

    /// <summary>Table 3-B.2's selection rule: bit rate per channel and sample rate choose one of
    /// the four allocation tables, so 64 kbit/s buys a mono stream the high-rate table and a
    /// stereo stream the low-rate one. Both cases ship in the cinemas.</summary>
    [Theory]
    [InlineData(1, 3, 0, 27)]
    [InlineData(2, 3, 0, 8)]
    [InlineData(2, 7, 0, 27)]
    [InlineData(2, 11, 0, 30)]
    [InlineData(2, 11, 1, 27)]
    [InlineData(2, 11, 2, 30)]
    [InlineData(1, 0, 2, 12)]
    public void TheBitRateAndSampleRateChooseTheStandardsAllocationTable(
        int channels, int bitRateIndex, int sampleRateIndex, int expected)
    {
        int bitRateClass = AudioLayer2Tables.BitRateClass(channels, bitRateIndex);

        Assert.Equal(expected, AudioLayer2Tables.SubbandLimitFor(bitRateClass, sampleRateIndex));
        Assert.Equal(
            expected > 12 ? 1 : 0,
            AudioLayer2Tables.AllocationTableFor(bitRateClass, sampleRateIndex));
    }

    /// <summary>How many bits an allocation field spends, down the spectrum: four for the lowest
    /// subbands and fewer for each coarser group above them.</summary>
    [Fact]
    public void TheAllocationFieldWidthsAreTheStandardsGroups()
    {
        int[] lowRate = Widths(new[] { 4, 2 }, new[] { 3, 10 });
        int[] highRate = Widths(new[] { 4, 11 }, new[] { 3, 12 }, new[] { 2, 7 });

        for (int subband = 0; subband < 32; subband++)
        {
            Assert.Equal(lowRate[subband], AudioLayer2Tables.AllocationBits(0, subband));
            Assert.Equal(highRate[subband], AudioLayer2Tables.AllocationBits(1, subband));
        }
    }

    /// <summary>Table 3-B.4: how many levels each quantiser codes, how many bits carry one code
    /// word, and which three of the seventeen pack a code word with three samples.</summary>
    [Fact]
    public void TheQuantisersAreTheStandardsSeventeen()
    {
        int[] levels = { 3, 5, 7, 9, 15, 31, 63, 127, 255, 511, 1023, 2047, 4095, 8191, 16383, 32767, 65535 };
        int[] bits = { 5, 7, 3, 10, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

        Assert.False(AudioLayer2Tables.Quantizer(0).IsAllocated);
        for (int number = 1; number <= 17; number++)
        {
            Layer2Quantizer quantizer = AudioLayer2Tables.Quantizer(number);

            Assert.True(quantizer.IsAllocated);
            Assert.Equal(levels[number - 1], quantizer.Levels);
            Assert.Equal(bits[number - 1], quantizer.Bits);
            Assert.Equal(number == 1 || number == 2 || number == 4, quantizer.Grouped);
        }

        // A grouped word spends fewer bits than three separate ones would, and a plain one
        // spends exactly enough for its levels; nothing else would be a legal entry.
        for (int number = 1; number <= 17; number++)
        {
            Layer2Quantizer quantizer = AudioLayer2Tables.Quantizer(number);
            int packed = quantizer.Grouped ? quantizer.Levels * quantizer.Levels * quantizer.Levels
                : quantizer.Levels + 1;
            Assert.InRange(packed, 1 << (quantizer.Bits - 1), 1 << quantizer.Bits);
        }
    }

    /// <summary>Every reachable allocation field value of both tables, against the level ladder
    /// ISO 11172-3 gives its group of subbands. The four ladders differ from each other by which
    /// quantisers they leave out, which is the thing a spot check on the ends cannot see: the
    /// high-rate table's first three subbands skip the 5- and 15-level quantisers where the
    /// eight above them keep both. A subband past the end of a table carries no field.</summary>
    [Fact]
    public void EveryAllocationFieldValueSelectsTheStandardsQuantiser()
    {
        for (int table = 0; table < 2; table++)
        {
            for (int subband = 0; subband < 32; subband++)
            {
                (int width, int[] levels) = StandardRow(table, subband);
                Assert.Equal(width, AudioLayer2Tables.AllocationBits(table, subband));
                for (int allocation = 0; allocation < (1 << width); allocation++)
                {
                    Layer2Quantizer quantizer =
                        AudioLayer2Tables.QuantizerFor(table, subband, allocation);

                    Assert.Equal(levels[allocation], quantizer.Levels);
                    Assert.Equal(allocation != 0, quantizer.IsAllocated);
                }
            }
        }
    }

    /// <summary>The synthesis window is the standard's D coefficients multiplied by 32768, which
    /// is why every one of the 512 lands on a half. Its mirror about the middle is the standard's
    /// own symmetry, and a mistyped entry breaks it.</summary>
    [Fact]
    public void TheSynthesisWindowIsTheStandardsScaledDCoefficients()
    {
        ReadOnlySpan<float> window = AudioSubbandSynthesis.Window;

        Assert.Equal(512, window.Length);
        Assert.Equal(0.0f, window[0]);
        Assert.Equal(1.144989014, window[256] / 32768.0, 9);
        for (int index = 1; index < 512; index++)
        {
            Assert.Equal(window[index] * 2.0f, MathF.Round(window[index] * 2.0f));
            Assert.Equal(Math.Abs(window[index]), Math.Abs(window[512 - index]));
        }
    }

    // How wide a subband's allocation field is and what its values select, from ISO 11172-3's
    // tables 3-B.2a (the high-rate table, row 1 here) and 3-B.2c (the low rate one, row 0),
    // written as quantisation levels because that is the quantity the standard tabulates. Zero
    // is the subband no bits were allocated to. Table 0 codes twelve subbands and table 1
    // thirty; there is no field at all past those, which a width of zero says.
    private static (int Width, int[] Levels) StandardRow(int table, int subband)
    {
        int[] lowRate =
        {
            0, 3, 5, 9, 15, 31, 63, 127, 255, 511, 1023, 2047, 4095, 8191, 16383, 65535,
        };
        int[] highRateFine =
        {
            0, 3, 7, 15, 31, 63, 127, 255, 511, 1023, 2047, 4095, 8191, 16383, 32767, 65535,
        };
        int[] highRateMiddle =
        {
            0, 3, 5, 7, 9, 15, 31, 63, 127, 255, 511, 1023, 2047, 4095, 8191, 65535,
        };
        int[] highRateCoarse = { 0, 3, 5, 7, 9, 15, 31, 65535 };
        int[] highRateCoarsest = { 0, 3, 5, 65535 };
        int[] none = { 0 };

        if (table == 0)
        {
            return subband < 2 ? (4, lowRate)
                : subband < 12 ? (3, lowRate)
                : (0, none);
        }

        return subband < 3 ? (4, highRateFine)
            : subband < 11 ? (4, highRateMiddle)
            : subband < 23 ? (3, highRateCoarse)
            : subband < 30 ? (2, highRateCoarsest)
            : (0, none);
    }

    // A run-length spelling of one allocation table's field widths: each pair is a width and how
    // many consecutive subbands take it, and the subbands past the last pair carry no field.
    private static int[] Widths(params int[][] runs)
    {
        var widths = new int[32];
        int at = 0;
        foreach (int[] run in runs)
        {
            for (int count = 0; count < run[1]; count++)
            {
                widths[at++] = run[0];
            }
        }

        return widths;
    }
}
