using System;
using System.Buffers.Binary;
using System.IO;

namespace CSVM.Extraction;

/// <summary>Decodes one LZX-compressed cabinet folder into its whole output
/// (docs/formats/cabinet.md). The input is the folder's data blocks joined, a bitstream of 16-bit
/// little-endian words read from the high bit. Verbatim, aligned-offset and uncompressed blocks
/// are decoded over a window of 2^15 to 2^21 bytes. The output splits into frames of 32,768
/// bytes; the input re-aligns to a word after each, and no match crosses one. The E8 call translation runs over the finished output, frame by frame. Written from
/// Microsoft's published LZX and cabinet specifications; any malformed stream throws
/// <see cref="InvalidDataException"/>.</summary>
internal sealed class LzxDecoder
{
    /// <summary>The output bytes per frame, which every data block but the last holds.</summary>
    public const int FrameSize = 32768;

    private const int Literals = 256;
    private const int MinMatch = 2;
    private const int LengthSymbols = 249;
    private const int PretreeSymbols = 20;
    private const int AlignedSymbols = 8;

    // The E8 translation covers only the first 32,768 frames of a stream.
    private const int TranslatedFrames = 32768;

    private static readonly int[] ExtraBits = BuildExtraBits();
    private static readonly int[] PositionBase = BuildPositionBase();

    private readonly byte[] _input;
    private readonly byte[] _output;
    private readonly int _windowSize;
    private readonly byte[] _mainLengths;
    private readonly byte[] _lengthLengths = new byte[LengthSymbols];
    private readonly byte[] _alignedLengths = new byte[AlignedSymbols];
    private readonly byte[] _pretreeLengths = new byte[PretreeSymbols];
    private readonly Huffman _main;
    private readonly Huffman _length = new(LengthSymbols);
    private readonly Huffman _aligned = new(AlignedSymbols);
    private readonly Huffman _pretree = new(PretreeSymbols);
    private int _pos;
    private uint _bits;
    private int _bitCount;
    private int _outPos;
    private int _blockType;
    private int _blockRemaining;
    private int _blockLength;
    private uint _r0 = 1;
    private uint _r1 = 1;
    private uint _r2 = 1;

    private LzxDecoder(byte[] input, int windowBits, int outputLength)
    {
        int slots = windowBits switch
        {
            15 => 30,
            16 => 32,
            17 => 34,
            18 => 36,
            19 => 38,
            20 => 42,
            21 => 50,
            _ => throw new InvalidDataException($"LZX window of 2^{windowBits} bytes, expected 2^15 to 2^21"),
        };
        _input = input;
        _output = new byte[outputLength];
        _windowSize = 1 << windowBits;
        _mainLengths = new byte[Literals + (8 * slots)];
        _main = new Huffman(_mainLengths.Length);
    }

    /// <summary>The <paramref name="outputLength"/> bytes the LZX stream <paramref name="input"/>
    /// holds, over a window of 2^<paramref name="windowBits"/> bytes.</summary>
    public static byte[] Decode(byte[] input, int windowBits, int outputLength)
    {
        var decoder = new LzxDecoder(input, windowBits, outputLength);
        int translation = decoder.Run();
        Translate(decoder._output, translation);
        return decoder._output;
    }

    private static int[] BuildExtraBits()
    {
        var bits = new int[51];
        for (int i = 4; i < bits.Length; i++)
        {
            bits[i] = Math.Min((i - 2) / 2, 17);
        }

        return bits;
    }

    private static int[] BuildPositionBase()
    {
        var bases = new int[51];
        for (int i = 1; i < bases.Length; i++)
        {
            bases[i] = bases[i - 1] + (1 << ExtraBits[i - 1]);
        }

        return bases;
    }

    // A call's operand becomes relative again only when it pointed inside the translation size.
    private static void Translate(byte[] data, int size)
    {
        if (size == 0)
        {
            return;
        }

        for (int frame = 0, start = 0; start < data.Length && frame < TranslatedFrames; frame++, start += FrameSize)
        {
            int end = start + Math.Min(FrameSize, data.Length - start) - 10;
            for (int i = start; i < end;)
            {
                if (data[i] != 0xE8)
                {
                    i++;
                    continue;
                }

                var operand = data.AsSpan(i + 1, 4);
                int absolute = BinaryPrimitives.ReadInt32LittleEndian(operand);
                if (absolute >= -i && absolute < size)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(operand, absolute >= 0 ? absolute - i : absolute + size);
                }

                i += 5;
            }
        }
    }

    // Returns the E8 translation size the stream's header names, 0 when translation is off.
    private int Run()
    {
        int translation = 0;
        if (ReadBits(1) == 1)
        {
            translation = (ReadBits(16) << 16) | ReadBits(16);
        }

        int frameEnd = Math.Min(FrameSize, _output.Length);
        while (_outPos < _output.Length)
        {
            if (_blockRemaining == 0)
            {
                StartBlock();
            }

            int start = _outPos;
            int end = _outPos + Math.Min(_blockRemaining, frameEnd - _outPos);
            if (_blockType == 3)
            {
                CopyRaw(end - _outPos);
            }
            else
            {
                DecodeRun(end, _blockType == 2);
            }

            _blockRemaining -= _outPos - start;
            if (_outPos == frameEnd)
            {
                if (_bitCount > 0)
                {
                    Ensure(16);
                }

                Remove(_bitCount & 15);
                frameEnd = Math.Min(frameEnd + FrameSize, _output.Length);
            }
        }

        if (((long)_pos * 8) - _bitCount > (long)_input.Length * 8)
        {
            throw new InvalidDataException($"LZX stream ends {_pos - _input.Length} bytes early");
        }

        return translation;
    }

    private void StartBlock()
    {
        // An uncompressed block of odd length is followed by one pad byte.
        if (_blockType == 3 && (_blockLength & 1) != 0)
        {
            _pos++;
        }

        _blockType = ReadBits(3);
        _blockLength = _blockRemaining = (ReadBits(16) << 8) | ReadBits(8);
        if (_blockLength == 0)
        {
            throw new InvalidDataException($"LZX block of length 0 at output byte {_outPos}");
        }

        switch (_blockType)
        {
            case 2:
                for (int i = 0; i < AlignedSymbols; i++)
                {
                    _alignedLengths[i] = (byte)ReadBits(3);
                }

                _aligned.Build(_alignedLengths);
                goto case 1;
            case 1:
                ReadLengths(_mainLengths, 0, Literals);
                ReadLengths(_mainLengths, Literals, _mainLengths.Length);
                _main.Build(_mainLengths);
                ReadLengths(_lengthLengths, 0, LengthSymbols);
                _length.Build(_lengthLengths);
                break;
            case 3:
                // Padding of 1 to 16 bits aligns to a word, then R0, R1 and R2 follow as raw bytes.
                Ensure(16);
                if (_bitCount > 16)
                {
                    _pos -= 2;
                }

                _bits = 0;
                _bitCount = 0;
                _r0 = ReadRawUInt32();
                _r1 = ReadRawUInt32();
                _r2 = ReadRawUInt32();
                break;
            default:
                throw new InvalidDataException($"LZX block type {_blockType} at output byte {_outPos}");
        }
    }

    // Each length is coded through the pretree as a change from the previous block's length.
    private void ReadLengths(byte[] lengths, int first, int last)
    {
        for (int i = 0; i < PretreeSymbols; i++)
        {
            _pretreeLengths[i] = (byte)ReadBits(4);
        }

        _pretree.Build(_pretreeLengths);
        for (int x = first; x < last;)
        {
            int code = _pretree.Decode(this);
            int run = 1;
            byte value;
            if (code == 17)
            {
                run = ReadBits(4) + 4;
                value = 0;
            }
            else if (code == 18)
            {
                run = ReadBits(5) + 20;
                value = 0;
            }
            else
            {
                if (code == 19)
                {
                    run = ReadBits(1) + 4;
                    code = _pretree.Decode(this);
                    if (code > 16)
                    {
                        throw new InvalidDataException($"LZX pretree code {code} after a run code");
                    }
                }

                value = (byte)((lengths[x] - code + 17) % 17);
            }

            if (x + run > last)
            {
                throw new InvalidDataException($"LZX code lengths run past symbol {last}");
            }

            lengths.AsSpan(x, run).Fill(value);
            x += run;
        }
    }

    private void DecodeRun(int end, bool aligned)
    {
        byte[] output = _output;
        while (_outPos < end)
        {
            int symbol = _main.Decode(this);
            if (symbol < Literals)
            {
                output[_outPos++] = (byte)symbol;
                continue;
            }

            symbol -= Literals;
            int length = symbol & 7;
            if (length == 7)
            {
                length += _length.Decode(this);
            }

            length += MinMatch;
            uint offset = MatchOffset(symbol >> 3, aligned);
            if (_outPos + length > end)
            {
                throw new InvalidDataException($"LZX match at output byte {_outPos} crosses its block or frame");
            }

            if (offset == 0 || offset > _outPos || offset > _windowSize)
            {
                throw new InvalidDataException($"LZX match at output byte {_outPos} reaches back {offset} bytes");
            }

            int from = _outPos - (int)offset;
            for (int i = 0; i < length; i++)
            {
                output[_outPos + i] = output[from + i];
            }

            _outPos += length;
        }
    }

    // Slots 0 to 2 reuse a recent offset; a higher slot names a new one, which shifts R0 to R1 to R2.
    private uint MatchOffset(int slot, bool aligned)
    {
        uint offset;
        switch (slot)
        {
            case 0:
                return _r0;
            case 1:
                offset = _r1;
                _r1 = _r0;
                break;
            case 2:
                offset = _r2;
                _r2 = _r0;
                break;
            default:
                int extra = ExtraBits[slot];
                offset = (uint)(PositionBase[slot] - 2);
                if (aligned && extra >= 3)
                {
                    if (extra > 3)
                    {
                        offset += (uint)ReadBits(extra - 3) << 3;
                    }

                    offset += (uint)_aligned.Decode(this);
                }
                else if (extra > 0)
                {
                    offset += (uint)ReadBits(extra);
                }

                _r2 = _r1;
                _r1 = _r0;
                break;
        }

        _r0 = offset;
        return offset;
    }

    private void CopyRaw(int count)
    {
        if (_pos + count > _input.Length)
        {
            throw new InvalidDataException($"LZX uncompressed block at output byte {_outPos} runs past the input");
        }

        Buffer.BlockCopy(_input, _pos, _output, _outPos, count);
        _pos += count;
        _outPos += count;
    }

    private uint ReadRawUInt32()
    {
        if (_pos + 4 > _input.Length)
        {
            throw new InvalidDataException("LZX uncompressed block header runs past the input");
        }

        uint value = BinaryPrimitives.ReadUInt32LittleEndian(_input.AsSpan(_pos));
        _pos += 4;
        return value;
    }

    // Past the input the stream reads as zeros; Run checks afterwards that none were consumed.
    private void Ensure(int count)
    {
        while (_bitCount < count)
        {
            uint word = _pos + 1 < _input.Length ? (uint)(_input[_pos] | (_input[_pos + 1] << 8)) : _pos < _input.Length ? _input[_pos] : 0u;
            _pos += 2;
            _bits |= word << (16 - _bitCount);
            _bitCount += 16;
        }
    }

    private void Remove(int count)
    {
        _bits <<= count;
        _bitCount -= count;
    }

    // At most 17 bits, which Ensure can always hold.
    private int ReadBits(int count)
    {
        Ensure(count);
        int value = (int)(_bits >> (32 - count));
        Remove(count);
        return value;
    }

    // A canonical Huffman code of at most 16 bits, read from the high bit. Codes up to TableBits
    // long decode by one lookup; longer ones are found by walking the per-length code ranges.
    private sealed class Huffman
    {
        private const int TableBits = 10;
        private const int MaxLength = 16;

        // Each entry is (symbol << 5) | code length; 0 marks a code longer than TableBits.
        private readonly ushort[] _table = new ushort[1 << TableBits];
        private readonly int[] _count = new int[MaxLength + 1];
        private readonly int[] _first = new int[MaxLength + 1];
        private readonly int[] _index = new int[MaxLength + 1];
        private readonly ushort[] _sorted;

        public Huffman(int symbols)
        {
            _sorted = new ushort[symbols];
        }

        public void Build(byte[] lengths)
        {
            Array.Clear(_count);
            foreach (byte length in lengths)
            {
                if (length > MaxLength)
                {
                    throw new InvalidDataException($"LZX code length {length}");
                }

                _count[length]++;
            }

            _count[0] = 0;
            int code = 0;
            int index = 0;
            int left = 1;
            Span<int> next = stackalloc int[MaxLength + 1];
            for (int length = 1; length <= MaxLength; length++)
            {
                left = (left << 1) - _count[length];
                if (left < 0)
                {
                    throw new InvalidDataException("LZX Huffman code lengths are over-subscribed");
                }

                _first[length] = code;
                _index[length] = index;
                next[length] = code;
                code = (code + _count[length]) << 1;
                index += _count[length];
            }

            Array.Clear(_table);
            Span<int> slot = stackalloc int[MaxLength + 1];
            _index.AsSpan().CopyTo(slot);
            for (int symbol = 0; symbol < lengths.Length; symbol++)
            {
                int length = lengths[symbol];
                if (length == 0)
                {
                    continue;
                }

                _sorted[slot[length]++] = (ushort)symbol;
                int assigned = next[length]++;
                if (length <= TableBits)
                {
                    int from = assigned << (TableBits - length);
                    _table.AsSpan(from, 1 << (TableBits - length)).Fill((ushort)((symbol << 5) | length));
                }
            }
        }

        public int Decode(LzxDecoder reader)
        {
            reader.Ensure(MaxLength);
            uint peek = reader._bits >> (32 - MaxLength);
            int entry = _table[peek >> (MaxLength - TableBits)];
            if (entry != 0)
            {
                reader.Remove(entry & 31);
                return entry >> 5;
            }

            for (int length = TableBits + 1; length <= MaxLength; length++)
            {
                int offset = (int)(peek >> (MaxLength - length)) - _first[length];
                if (offset >= 0 && offset < _count[length])
                {
                    reader.Remove(length);
                    return _sorted[_index[length] + offset];
                }
            }

            throw new InvalidDataException("LZX bitstream holds a code its Huffman table does not");
        }
    }
}
