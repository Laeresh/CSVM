using System;

namespace CSVM.Video;

/// <summary>
/// A bit-level reader over a complete MPEG-1 stream already in memory: fixed-width bit fields,
/// byte alignment, the start-code scan that finds every syntax element, and the variable-length
/// code walk over a <see cref="VideoVlcTables"/> table. A read that runs past the end yields
/// zero and leaves the position where it was, so a truncated file ends the decode instead of
/// throwing; callers detect that through <see cref="AtEnd"/> or a start-code search returning
/// <see cref="NoStartCode"/>.
/// </summary>
public sealed class MpegBitReader
{
    /// <summary>What a start-code search returns when the stream holds no further one.</summary>
    public const int NoStartCode = -1;

    private readonly byte[] _bytes;
    private readonly long _bitCount;
    private long _bitPosition;

    public MpegBitReader(byte[] bytes)
    {
        _bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
        _bitCount = (long)bytes.Length << 3;
    }

    /// <summary>Bit offset of the next bit to be read.</summary>
    public long BitPosition => _bitPosition;

    /// <summary>True once every bit has been consumed.</summary>
    public bool AtEnd => _bitPosition >= _bitCount;

    /// <summary>Whether the stream still holds that many unread bits.</summary>
    public bool Has(long bitCount) => _bitCount - _bitPosition >= bitCount;

    /// <summary>Reads up to 31 bits most significant bit first, or zero when the stream is
    /// short of them.</summary>
    public int ReadBits(int count)
    {
        if (!Has(count))
        {
            return 0;
        }

        int value = 0;
        while (count > 0)
        {
            int currentByte = _bytes[(int)(_bitPosition >> 3)];
            int remaining = 8 - (int)(_bitPosition & 7);
            int read = remaining < count ? remaining : count;
            int shift = remaining - read;
            int mask = 0xff >> (8 - read);
            value = (value << read) | ((currentByte & (mask << shift)) >> shift);
            _bitPosition += read;
            count -= read;
        }

        return value;
    }

    /// <summary>Advances past that many bits, or not at all when the stream is short of them.</summary>
    public void Skip(long bitCount)
    {
        if (Has(bitCount))
        {
            _bitPosition += bitCount;
        }
    }

    /// <summary>Moves to an absolute bit offset; the decoders use it to restart a stream.</summary>
    public void SeekBits(long bitPosition) => _bitPosition = Math.Clamp(bitPosition, 0, _bitCount);

    /// <summary>Advances to the next byte boundary.</summary>
    public void Align() => _bitPosition = ((_bitPosition + 7) >> 3) << 3;

    /// <summary>Aligns, then skips a run of that byte value, returning how many were skipped.
    /// Packet headers carry stuffing bytes this way.</summary>
    public int SkipBytes(byte value)
    {
        Align();
        int skipped = 0;
        while (Has(8) && _bytes[(int)(_bitPosition >> 3)] == value)
        {
            _bitPosition += 8;
            skipped++;
        }

        return skipped;
    }

    /// <summary>Scans forward to the next <c>00 00 01</c> prefix and returns the byte after it,
    /// leaving the position just past that byte.</summary>
    public int NextStartCode()
    {
        Align();
        while (Has(5 << 3))
        {
            int byteIndex = (int)(_bitPosition >> 3);
            if (_bytes[byteIndex] == 0x00 && _bytes[byteIndex + 1] == 0x00 && _bytes[byteIndex + 2] == 0x01)
            {
                _bitPosition = (long)(byteIndex + 4) << 3;
                return _bytes[byteIndex + 3];
            }

            _bitPosition += 8;
        }

        return NoStartCode;
    }

    /// <summary>Scans forward to the next start code with that value, skipping any other.</summary>
    public int FindStartCode(int code)
    {
        while (true)
        {
            int current = NextStartCode();
            if (current == code || current == NoStartCode)
            {
                return current;
            }
        }
    }

    /// <summary>Whether the next bits are not all zero, without consuming them. A slice ends
    /// where 23 zero bits begin, which is the only way to know it has no further macroblock.</summary>
    public bool PeekNonZero(int bitCount)
    {
        if (!Has(bitCount))
        {
            return false;
        }

        int value = ReadBits(bitCount);
        _bitPosition -= bitCount;
        return value != 0;
    }

    /// <summary>Walks a variable-length code table one bit at a time and returns the value at
    /// the leaf it lands on. An unassigned bit pattern yields that entry's value, which the
    /// tables spell as zero, rather than an error.</summary>
    public int ReadVlc(ReadOnlySpan<int> table)
    {
        int pair = 0;
        while (true)
        {
            int at = (pair + ReadBits(1)) * 2;
            int next = table[at];
            if (next <= 0)
            {
                return table[at + 1];
            }

            pair = next;
        }
    }
}
