using System;
using System.IO;

namespace CSVM.Video;

/// <summary>
/// An MPEG-1 audio layer II decoder (ISO 11172-3) over one complete elementary stream in memory.
/// The public surface is the stream's own parameters and <see cref="NextFrame"/>, which yields
/// 1152 PCM samples per channel at a time until the stream runs out. Every frame is found where
/// the previous frame's declared size says it is rather than by hunting for a sync word, so
/// <see cref="ResyncCount"/> counts real damage instead of ordinary ancillary data.
/// Nothing here reads a container: the caller joins the audio packets and hands over the bytes.
/// </summary>
public sealed class MpegAudioDecoder
{
    private const int FrameSync = 0x7ff;
    private const int VersionMpeg1 = 0x3;
    private const int LayerTwo = 0x2;
    private const int ModeJointStereo = 0x1;
    private const int ModeMono = 0x3;
    private const int HeaderBits = 32;
    private const int SubbandsPerBlock = AudioLayer2Tables.SubbandCount;
    private const int BlocksPerGranule = 3;
    private const int GranulesPerPart = 4;
    private const int ScaleFactorParts = 3;

    private readonly MpegBitReader _reader;
    private readonly double _startTime;
    private readonly Channel[] _channels;
    private readonly int[] _codeWords = new int[BlocksPerGranule];
    private readonly int[] _subband = new int[SubbandsPerBlock];
    private readonly float[] _block = new float[SubbandsPerBlock];
    private readonly AudioFrame _frame;

    private long _frameStartBit;
    private int _frameSize;
    private int _bitRateIndex = -1;
    private int _sampleRateIndex = -1;
    private int _mode = -1;
    private int _bound;
    private bool _hasCheckWord;
    private bool _hasHeader;
    private int _framesDecoded;
    private int _resyncCount;

    /// <summary>Reads the first frame header of <paramref name="elementaryStream"/> and throws
    /// <see cref="InvalidDataException"/> when it holds none. <paramref name="startTime"/> is
    /// the container timestamp the first sample is heard at, so sound and picture share a
    /// clock.</summary>
    public MpegAudioDecoder(byte[] elementaryStream, double startTime = 0.0)
    {
        _reader = new MpegBitReader(elementaryStream);
        _startTime = startTime;
        if (!MoveToFrame())
        {
            throw new InvalidDataException("no MPEG-1 audio layer II frame in the elementary stream");
        }

        _reader.SeekBits(_frameStartBit);
        Channels = _mode == ModeMono ? 1 : 2;
        SampleRate = AudioLayer2Tables.SampleRate(_sampleRateIndex);
        BitRate = AudioLayer2Tables.BitRate(_bitRateIndex);
        _channels = new Channel[Channels];
        for (int channel = 0; channel < Channels; channel++)
        {
            _channels[channel] = new Channel();
        }

        _frame = new AudioFrame(Channels, SampleRate);
    }

    /// <summary>Samples per second per channel, as the first frame header declares it.</summary>
    public int SampleRate { get; }

    /// <summary>Channels the stream carries: one for a mono file, two otherwise.</summary>
    public int Channels { get; }

    /// <summary>Bit rate in kbit/s of the first frame. A stream may change it at a frame
    /// boundary, which this decoder follows without reporting a new value here.</summary>
    public int BitRate { get; }

    /// <summary>How many frames have been handed out so far.</summary>
    public int FramesDecoded => _framesDecoded;

    /// <summary>How often the decoder had to skip bytes to reach a frame header, because one
    /// was not where the previous frame's declared size put it. Zero for a stream the container
    /// walk reassembled whole; anything else means bytes were lost or inserted. Bytes after the
    /// last frame that lead to no further one are the end of the stream, not a skip.</summary>
    public int ResyncCount => _resyncCount;

    /// <summary>Opens a decoder, or returns null when the bytes carry no layer II frame at all.
    /// That is the form a caller uses when a missing sound track is not an error.</summary>
    public static MpegAudioDecoder? TryOpen(byte[] elementaryStream, double startTime = 0.0)
    {
        try
        {
            return new MpegAudioDecoder(elementaryStream, startTime);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>Decodes the next frame, or returns null at the end of the stream. ⚠ The frame
    /// aliases a buffer this decoder writes again on the next call; copy or consume it before
    /// asking for another.</summary>
    public AudioFrame? NextFrame()
    {
        if (!MoveToFrame())
        {
            return null;
        }

        DecodeFrame();
        _reader.SeekBits(_frameStartBit + ((long)_frameSize << 3));
        _frame.Time = _startTime + ((double)_framesDecoded * AudioLayer2Tables.SamplesPerFrame / SampleRate);
        _framesDecoded++;
        return _frame;
    }

    /// <summary>Restarts at the first frame and clears the filter banks, so a second pass over
    /// the same stream produces the same samples the first one did.</summary>
    public void Rewind()
    {
        _reader.SeekBits(0);
        _framesDecoded = 0;
        _resyncCount = 0;
        foreach (Channel channel in _channels)
        {
            channel.Synthesis.Reset();
        }
    }

    // The scale factor index names one of three bases and a power of two to divide it by; index
    // 63 is the standard's smallest and stands for silence rather than for a scale.
    private static int ResolveScaleFactor(int index)
    {
        if (index >= 63)
        {
            return 0;
        }

        int shift = index / 3;
        return (AudioLayer2Tables.ScaleFactorBase[index % 3] + ((1 << shift) >> 1)) >> shift;
    }

    // Positions the reader on the next frame header, reading it into this decoder's state. A
    // header that is not where the last frame's size said it would be starts a byte-by-byte
    // hunt, which costs a resync only when it finds one. Nine of the ten cinemas pad the tail
    // of the sound track with zeroes, and that is the end of the stream rather than damage.
    private bool MoveToFrame()
    {
        _reader.Align();
        long start = _reader.BitPosition;
        if (!_reader.Has(HeaderBits))
        {
            return false;
        }

        if (TryReadHeaderAt(start, out bool truncated))
        {
            return true;
        }

        if (truncated)
        {
            return false;
        }

        for (long at = start + 8; ; at += 8)
        {
            _reader.SeekBits(at);
            if (_reader.BitPosition != at || !_reader.Has(HeaderBits))
            {
                return false;
            }

            if (TryReadHeaderAt(at, out truncated))
            {
                _resyncCount++;
                return true;
            }

            if (truncated)
            {
                return false;
            }
        }
    }

    // Leaves the reader just past the header and its optional check word on success, and back
    // where it started on failure, so the hunt above can step one byte and try again.
    // <paramref name="truncated"/> separates a header the stream has no room to follow through
    // from one that is not a header at all.
    private bool TryReadHeaderAt(long bitPosition, out bool truncated)
    {
        truncated = false;
        _reader.SeekBits(bitPosition);
        if (!ReadHeader())
        {
            _reader.SeekBits(bitPosition);
            return false;
        }

        _reader.SeekBits(bitPosition);
        if (!_reader.Has((long)_frameSize << 3))
        {
            truncated = true;
            return false;
        }

        _frameStartBit = bitPosition;
        _reader.SeekBits(bitPosition + HeaderBits + (_hasCheckWord ? 16 : 0));
        return true;
    }

    // ⚠ Bit rate index 0 is the free format and 15 is forbidden; both must be rejected here,
    // because neither names an entry of the bit rate table and a frame size cannot be computed
    // without one.
    private bool ReadHeader()
    {
        if (_reader.ReadBits(11) != FrameSync
            || _reader.ReadBits(2) != VersionMpeg1
            || _reader.ReadBits(2) != LayerTwo)
        {
            return false;
        }

        _hasCheckWord = _reader.ReadBits(1) == 0;
        int bitRateIndex = _reader.ReadBits(4) - 1;
        int sampleRateIndex = _reader.ReadBits(2);
        if (bitRateIndex < 0 || bitRateIndex > 13 || sampleRateIndex == 3)
        {
            return false;
        }

        int padding = _reader.ReadBits(1);
        _reader.Skip(1);
        int mode = _reader.ReadBits(2);
        int modeExtension = _reader.ReadBits(2);
        if (_hasHeader && (_sampleRateIndex != sampleRateIndex || _mode != mode))
        {
            return false;
        }

        _reader.Skip(4);
        _bitRateIndex = bitRateIndex;
        _sampleRateIndex = sampleRateIndex;
        _mode = mode;
        _hasHeader = true;
        _bound = mode == ModeJointStereo ? (modeExtension + 1) << 2
            : mode == ModeMono ? 0
            : SubbandsPerBlock;
        _frameSize = (144000 * AudioLayer2Tables.BitRate(bitRateIndex)
            / AudioLayer2Tables.SampleRate(sampleRateIndex)) + padding;
        return true;
    }

    private void DecodeFrame()
    {
        int bitRateClass = AudioLayer2Tables.BitRateClass(Channels, _bitRateIndex);
        int table = AudioLayer2Tables.AllocationTableFor(bitRateClass, _sampleRateIndex);
        int subbandLimit = AudioLayer2Tables.SubbandLimitFor(bitRateClass, _sampleRateIndex);
        int bound = Math.Min(_bound, subbandLimit);

        ReadAllocations(table, bound, subbandLimit);
        ReadScaleFactors(subbandLimit);

        int at = 0;
        for (int part = 0; part < ScaleFactorParts; part++)
        {
            for (int granule = 0; granule < GranulesPerPart; granule++)
            {
                ReadGranule(part, bound, subbandLimit);
                for (int block = 0; block < BlocksPerGranule; block++)
                {
                    SynthesizeBlock(block, at);
                    at += SubbandsPerBlock;
                }
            }
        }
    }

    // Below a joint stereo bound each channel allocates its subbands separately; above it the
    // two share one allocation field, and a mono stream has a bound of zero so all of its
    // subbands take that path.
    private void ReadAllocations(int table, int bound, int subbandLimit)
    {
        for (int subband = 0; subband < bound; subband++)
        {
            foreach (Channel channel in _channels)
            {
                channel.Allocation[subband] = ReadAllocation(table, subband);
            }
        }

        for (int subband = bound; subband < subbandLimit; subband++)
        {
            Layer2Quantizer shared = ReadAllocation(table, subband);
            foreach (Channel channel in _channels)
            {
                channel.Allocation[subband] = shared;
            }
        }
    }

    private Layer2Quantizer ReadAllocation(int table, int subband) =>
        AudioLayer2Tables.QuantizerFor(
            table, subband, _reader.ReadBits(AudioLayer2Tables.AllocationBits(table, subband)));

    // The selection information says which of the three parts of the frame share a scale
    // factor, and the factors themselves follow in the same order. Both are coded per channel
    // even where the allocation above them is shared.
    private void ReadScaleFactors(int subbandLimit)
    {
        for (int subband = 0; subband < subbandLimit; subband++)
        {
            foreach (Channel channel in _channels)
            {
                if (channel.Allocation[subband].IsAllocated)
                {
                    channel.ScaleFactorSelect[subband] = _reader.ReadBits(2);
                }
            }
        }

        for (int subband = 0; subband < subbandLimit; subband++)
        {
            foreach (Channel channel in _channels)
            {
                if (channel.Allocation[subband].IsAllocated)
                {
                    ReadScaleFactorTriple(channel, subband);
                }
            }
        }
    }

    private void ReadScaleFactorTriple(Channel channel, int subband)
    {
        int at = subband * ScaleFactorParts;
        switch (channel.ScaleFactorSelect[subband])
        {
            case 0:
                channel.ScaleFactor[at] = ResolveScaleFactor(_reader.ReadBits(6));
                channel.ScaleFactor[at + 1] = ResolveScaleFactor(_reader.ReadBits(6));
                channel.ScaleFactor[at + 2] = ResolveScaleFactor(_reader.ReadBits(6));
                break;
            case 1:
                channel.ScaleFactor[at] = ResolveScaleFactor(_reader.ReadBits(6));
                channel.ScaleFactor[at + 1] = channel.ScaleFactor[at];
                channel.ScaleFactor[at + 2] = ResolveScaleFactor(_reader.ReadBits(6));
                break;
            case 2:
                channel.ScaleFactor[at] = ResolveScaleFactor(_reader.ReadBits(6));
                channel.ScaleFactor[at + 1] = channel.ScaleFactor[at];
                channel.ScaleFactor[at + 2] = channel.ScaleFactor[at];
                break;
            default:
                channel.ScaleFactor[at] = ResolveScaleFactor(_reader.ReadBits(6));
                channel.ScaleFactor[at + 1] = ResolveScaleFactor(_reader.ReadBits(6));
                channel.ScaleFactor[at + 2] = channel.ScaleFactor[at + 1];
                break;
        }
    }

    private void ReadGranule(int part, int bound, int subbandLimit)
    {
        for (int subband = 0; subband < bound; subband++)
        {
            foreach (Channel channel in _channels)
            {
                ReadCodeWords(channel.Allocation[subband]);
                Requantize(channel, subband, part);
            }
        }

        for (int subband = bound; subband < subbandLimit; subband++)
        {
            ReadCodeWords(_channels[0].Allocation[subband]);
            foreach (Channel channel in _channels)
            {
                Requantize(channel, subband, part);
            }
        }

        for (int subband = subbandLimit; subband < SubbandsPerBlock; subband++)
        {
            foreach (Channel channel in _channels)
            {
                int at = subband * BlocksPerGranule;
                channel.Sample[at] = 0;
                channel.Sample[at + 1] = 0;
                channel.Sample[at + 2] = 0;
            }
        }
    }

    // One subband's three code words for one granule. A grouped quantiser packs all three into
    // a single word in base levels, which is what makes the coarse subbands cheap.
    private void ReadCodeWords(Layer2Quantizer quantizer)
    {
        if (!quantizer.IsAllocated)
        {
            _codeWords[0] = 0;
            _codeWords[1] = 0;
            _codeWords[2] = 0;
            return;
        }

        if (quantizer.Grouped)
        {
            int packed = _reader.ReadBits(quantizer.Bits);
            _codeWords[0] = packed % quantizer.Levels;
            packed /= quantizer.Levels;
            _codeWords[1] = packed % quantizer.Levels;
            _codeWords[2] = packed / quantizer.Levels;
            return;
        }

        _codeWords[0] = _reader.ReadBits(quantizer.Bits);
        _codeWords[1] = _reader.ReadBits(quantizer.Bits);
        _codeWords[2] = _reader.ReadBits(quantizer.Bits);
    }

    // ⚠ The scale factor is this channel's own, never the channel the code words were read
    // for. Above a joint stereo bound the two channels share the code words and scale them
    // apart, which is the whole point of coding them once.
    private void Requantize(Channel channel, int subband, int part)
    {
        int at = subband * BlocksPerGranule;
        Layer2Quantizer quantizer = channel.Allocation[subband];
        if (!quantizer.IsAllocated)
        {
            channel.Sample[at] = 0;
            channel.Sample[at + 1] = 0;
            channel.Sample[at + 2] = 0;
            return;
        }

        int scaleFactor = channel.ScaleFactor[(subband * ScaleFactorParts) + part];
        int scale = 65536 / (quantizer.Levels + 1);
        int middle = ((quantizer.Levels + 1) >> 1) - 1;
        for (int block = 0; block < BlocksPerGranule; block++)
        {
            int value = (middle - _codeWords[block]) * scale;
            int scaled = (value * (scaleFactor >> 12))
                + (((value * (scaleFactor & 4095)) + 2048) >> 12);
            channel.Sample[at + block] = scaled >> 12;
        }
    }

    private void SynthesizeBlock(int block, int at)
    {
        for (int index = 0; index < Channels; index++)
        {
            Channel channel = _channels[index];
            for (int subband = 0; subband < SubbandsPerBlock; subband++)
            {
                _subband[subband] = channel.Sample[(subband * BlocksPerGranule) + block];
            }

            channel.Synthesis.Synthesize(_subband, _block);
            for (int sample = 0; sample < SubbandsPerBlock; sample++)
            {
                _frame.Samples[((at + sample) * Channels) + index] = _block[sample];
            }
        }
    }

    // One channel's decoding state, carried across a frame because the allocation and the
    // scale factors are read once and used by all twelve granules.
    private sealed class Channel
    {
        public readonly Layer2Quantizer[] Allocation = new Layer2Quantizer[SubbandsPerBlock];
        public readonly int[] ScaleFactorSelect = new int[SubbandsPerBlock];
        public readonly int[] ScaleFactor = new int[SubbandsPerBlock * ScaleFactorParts];
        public readonly int[] Sample = new int[SubbandsPerBlock * BlocksPerGranule];
        public readonly AudioSubbandSynthesis Synthesis = new AudioSubbandSynthesis();
    }
}
