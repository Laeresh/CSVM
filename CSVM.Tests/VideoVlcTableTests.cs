using System;
using System.Collections.Generic;
using CSVM.Video;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The variable-length code tables against the codes ISO 11172-2 assigns them. There is no
/// reference decoder to compare pictures with, and a table entry that is wrong by one produces
/// plausible rubbish rather than an error, so the codes are checked one at a time here and the
/// shape of every table is checked as a whole.
/// </summary>
public class VideoVlcTableTests
{
    [Theory]
    [InlineData("1", 1)]
    [InlineData("011", 2)]
    [InlineData("010", 3)]
    [InlineData("0011", 4)]
    [InlineData("0010", 5)]
    [InlineData("00011", 6)]
    [InlineData("00010", 7)]
    [InlineData("0000111", 8)]
    [InlineData("0000110", 9)]
    [InlineData("00001011", 10)]
    [InlineData("00000110", 15)]
    [InlineData("0000010111", 16)]
    [InlineData("00000100011", 22)]
    [InlineData("00000011000", 33)]
    [InlineData("00000001111", 34)]
    [InlineData("00000001000", 35)]
    public void MacroblockAddressIncrementMatchesTheStandard(string code, int expected) =>
        Assert.Equal(expected, Decode(VideoVlcTables.MacroblockAddressIncrement, code));

    [Theory]
    [InlineData("1", 0x01)]
    [InlineData("01", 0x11)]
    public void IntraMacroblockTypeMatchesTheStandard(string code, int expected) =>
        Assert.Equal(expected, Decode(VideoVlcTables.MacroblockTypeFor(1), code));

    [Theory]
    [InlineData("1", 0x0a)]
    [InlineData("01", 0x02)]
    [InlineData("001", 0x08)]
    [InlineData("00011", 0x01)]
    [InlineData("00010", 0x1a)]
    [InlineData("00001", 0x12)]
    [InlineData("000001", 0x11)]
    public void PredictiveMacroblockTypeMatchesTheStandard(string code, int expected) =>
        Assert.Equal(expected, Decode(VideoVlcTables.MacroblockTypeFor(2), code));

    [Theory]
    [InlineData("10", 0x0c)]
    [InlineData("11", 0x0e)]
    [InlineData("010", 0x04)]
    [InlineData("011", 0x06)]
    [InlineData("0010", 0x08)]
    [InlineData("0011", 0x0a)]
    [InlineData("00011", 0x01)]
    [InlineData("00010", 0x1e)]
    [InlineData("000011", 0x1a)]
    [InlineData("000010", 0x16)]
    [InlineData("000001", 0x11)]
    public void BidirectionalMacroblockTypeMatchesTheStandard(string code, int expected) =>
        Assert.Equal(expected, Decode(VideoVlcTables.MacroblockTypeFor(3), code));

    [Theory]
    [InlineData("111", 60)]
    [InlineData("1101", 4)]
    [InlineData("1100", 8)]
    [InlineData("1011", 16)]
    [InlineData("1010", 32)]
    [InlineData("10011", 12)]
    [InlineData("10010", 48)]
    [InlineData("10001", 20)]
    [InlineData("10000", 40)]
    [InlineData("01111", 28)]
    [InlineData("01110", 44)]
    [InlineData("01101", 52)]
    [InlineData("01100", 56)]
    [InlineData("01011", 1)]
    [InlineData("01010", 61)]
    [InlineData("01001", 2)]
    [InlineData("01000", 62)]
    public void CodedBlockPatternMatchesTheStandard(string code, int expected) =>
        Assert.Equal(expected, Decode(VideoVlcTables.CodedBlockPattern, code));

    [Theory]
    [InlineData("1", 0)]
    [InlineData("010", 1)]
    [InlineData("011", -1)]
    [InlineData("0010", 2)]
    [InlineData("0011", -2)]
    [InlineData("00010", 3)]
    [InlineData("00011", -3)]
    [InlineData("0000110", 4)]
    [InlineData("0000111", -4)]
    [InlineData("00001010", 5)]
    [InlineData("00001000", 6)]
    [InlineData("00000110", 7)]
    [InlineData("0000010110", 8)]
    [InlineData("0000010100", 9)]
    [InlineData("0000010010", 10)]
    [InlineData("00000100010", 11)]
    [InlineData("00000100000", 12)]
    [InlineData("00000011110", 13)]
    [InlineData("00000011000", 16)]
    public void MotionCodeMatchesTheStandard(string code, int expected) =>
        Assert.Equal(expected, Decode(VideoVlcTables.MotionVector, code));

    [Theory]
    [InlineData("100", 0)]
    [InlineData("00", 1)]
    [InlineData("01", 2)]
    [InlineData("101", 3)]
    [InlineData("110", 4)]
    [InlineData("1110", 5)]
    [InlineData("11110", 6)]
    [InlineData("111110", 7)]
    [InlineData("1111110", 8)]
    public void LuminanceDcSizeMatchesTheStandard(string code, int expected) =>
        Assert.Equal(expected, Decode(VideoVlcTables.DctSizeFor(0), code));

    [Theory]
    [InlineData("00", 0)]
    [InlineData("01", 1)]
    [InlineData("10", 2)]
    [InlineData("110", 3)]
    [InlineData("1110", 4)]
    [InlineData("11110", 5)]
    [InlineData("111110", 6)]
    [InlineData("1111110", 7)]
    [InlineData("11111110", 8)]
    public void ChrominanceDcSizeMatchesTheStandard(string code, int expected) =>
        Assert.Equal(expected, Decode(VideoVlcTables.DctSizeFor(1), code));

    // The value packs the zero run in the high byte and the level in the low one; 0xffff is the
    // escape, and the code "1" doubles as the end of a block once a coefficient has been read.
    [Theory]
    [InlineData("1", 0x0001)]
    [InlineData("0100", 0x0002)]
    [InlineData("0101", 0x0201)]
    [InlineData("00101", 0x0003)]
    [InlineData("00111", 0x0301)]
    [InlineData("00110", 0x0401)]
    [InlineData("000100", 0x0701)]
    [InlineData("000101", 0x0601)]
    [InlineData("000110", 0x0102)]
    [InlineData("000111", 0x0501)]
    [InlineData("000001", 0xffff)]
    public void DctCoefficientMatchesTheStandard(string code, int expected) =>
        Assert.Equal(expected, Decode(VideoVlcTables.DctCoefficient, code));

    [Fact]
    public void EveryTableIsATreeWithNoUnreachableEntry()
    {
        foreach (var (name, table) in Tables())
        {
            Assert.Equal(0, table.Length % 2);
            var reached = new HashSet<int> { 0 };
            for (int pair = 0; pair * 2 < table.Length; pair += 2)
            {
                foreach (int bit in new[] { 0, 1 })
                {
                    int next = table[(pair + bit) * 2];
                    if (next == 0 || next == -1)
                    {
                        continue;
                    }

                    Assert.True(next > pair, $"{name}: entry {pair + bit} walks backwards to {next}");
                    Assert.True(next + 1 < table.Length / 2, $"{name}: entry {pair + bit} walks past the table");
                    reached.Add(next);
                }
            }

            Assert.Equal(table.Length / 4, reached.Count);
        }
    }

    private static int Decode(ReadOnlySpan<int> table, string code) =>
        new MpegBitReader(MpegTestStreams.Bits(code)).ReadVlc(table);

    // Every table by name, so a failure says which one; the two macroblock-type tables that are
    // reached through a picture type are included by their picture type.
    private static IEnumerable<(string Name, int[] Table)> Tables()
    {
        yield return ("MacroblockAddressIncrement", VideoVlcTables.MacroblockAddressIncrement.ToArray());
        yield return ("MacroblockTypeIntra", VideoVlcTables.MacroblockTypeFor(1).ToArray());
        yield return ("MacroblockTypePredictive", VideoVlcTables.MacroblockTypeFor(2).ToArray());
        yield return ("MacroblockTypeBidirectional", VideoVlcTables.MacroblockTypeFor(3).ToArray());
        yield return ("CodedBlockPattern", VideoVlcTables.CodedBlockPattern.ToArray());
        yield return ("MotionVector", VideoVlcTables.MotionVector.ToArray());
        yield return ("DctSizeLuminance", VideoVlcTables.DctSizeFor(0).ToArray());
        yield return ("DctSizeChrominance", VideoVlcTables.DctSizeFor(1).ToArray());
        yield return ("DctCoefficient", VideoVlcTables.DctCoefficient.ToArray());
    }
}
