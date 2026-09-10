using CSVM.Video;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The bit reader every MPEG-1 symbol is read through: fields that straddle byte boundaries,
/// alignment, the start-code scan, and the two lookahead forms the decoders rely on.
/// </summary>
public class MpegBitReaderTests
{
    [Fact]
    public void FieldsStraddleByteBoundaries()
    {
        var reader = new MpegBitReader(MpegTestStreams.Bits("110100111000101"));
        Assert.Equal(0b110, reader.ReadBits(3));
        Assert.Equal(0b1001110, reader.ReadBits(7));
        Assert.Equal(0b00101, reader.ReadBits(5));
        Assert.Equal(15, reader.BitPosition);
    }

    [Fact]
    public void AReadPastTheEndYieldsZeroAndDoesNotMove()
    {
        var reader = new MpegBitReader(new byte[] { 0xff });
        Assert.Equal(0xff, reader.ReadBits(8));
        Assert.True(reader.AtEnd);
        Assert.Equal(0, reader.ReadBits(1));
        Assert.Equal(8, reader.BitPosition);
    }

    [Fact]
    public void AlignAdvancesToTheNextByte()
    {
        var reader = new MpegBitReader(new byte[] { 0xaa, 0xbb });
        reader.ReadBits(3);
        reader.Align();
        Assert.Equal(8, reader.BitPosition);
        Assert.Equal(0xbb, reader.ReadBits(8));
    }

    [Fact]
    public void SkipBytesCountsOnlyTheRunItSkipped()
    {
        var reader = new MpegBitReader(new byte[] { 0xff, 0xff, 0xff, 0x07 });
        Assert.Equal(3, reader.SkipBytes(0xff));
        Assert.Equal(0x07, reader.ReadBits(8));
    }

    [Fact]
    public void TheStartCodeScanReturnsTheCodeAndStopsAfterIt()
    {
        var reader = new MpegBitReader(new byte[] { 0x12, 0x00, 0x00, 0x01, 0xb3, 0x45, 0x00, 0x00, 0x01, 0x00, 0x99 });
        Assert.Equal(0xb3, reader.NextStartCode());
        Assert.Equal(0x45, reader.ReadBits(8));
        Assert.Equal(0x00, reader.NextStartCode());
        Assert.Equal(0x99, reader.ReadBits(8));
    }

    [Fact]
    public void FindStartCodeSkipsEveryOtherCodeAndReportsTheEnd()
    {
        var reader = new MpegBitReader(new byte[] { 0x00, 0x00, 0x01, 0xb3, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0xb7 });
        Assert.Equal(0x00, reader.FindStartCode(0x00));
        Assert.Equal(MpegBitReader.NoStartCode, reader.FindStartCode(0x00));
    }

    [Fact]
    public void PeekNonZeroDoesNotConsume()
    {
        var reader = new MpegBitReader(MpegTestStreams.Bits("000000000000000000000001"));
        Assert.False(reader.PeekNonZero(23));
        Assert.Equal(0, reader.BitPosition);
        reader.Skip(1);
        Assert.True(reader.PeekNonZero(23));
        Assert.Equal(1, reader.BitPosition);
    }

    [Fact]
    public void SeekBitsRestartsTheStream()
    {
        var reader = new MpegBitReader(new byte[] { 0xa5, 0x5a });
        reader.ReadBits(16);
        reader.SeekBits(0);
        Assert.Equal(0xa5, reader.ReadBits(8));
    }
}
