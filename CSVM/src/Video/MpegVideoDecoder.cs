using System;
using System.IO;

namespace CSVM.Video;

/// <summary>
/// An MPEG-1 video decoder (ISO 11172-2) over one complete elementary stream in memory. The
/// public surface is the sequence's own parameters and <see cref="NextFrame"/>, which yields
/// pictures in display order: bidirectional pictures come out where they belong, so a reference
/// picture is held back one call and the last one falls out at end of stream.
/// Every parameter is read from this stream's own headers, since the ten cinemas do not share
/// one profile. There is no reference decoder to check output against and the standard permits
/// transform mismatch between conformant decoders, so correctness is settled by the tables and
/// the pure pieces in <c>CSVM.Tests</c>, never by comparing pixels with another decoder.
/// </summary>
public sealed class MpegVideoDecoder
{
    private const int StartPicture = 0x00;
    private const int StartSliceFirst = 0x01;
    private const int StartSliceLast = 0xAF;
    private const int StartUserData = 0xB2;
    private const int StartSequence = 0xB3;
    private const int StartExtension = 0xB5;

    private const int PictureIntra = 1;
    private const int PicturePredictive = 2;
    private const int PictureBidirectional = 3;

    // Frame-rate codes 1 to 8 of the sequence header. The three broadcast rates are exactly
    // 24000/1001, 30000/1001 and 60000/1001; the decimal spellings are only their names.
    private static readonly double[] PictureRates =
    {
        0.0, 24000.0 / 1001.0, 24.0, 25.0, 30000.0 / 1001.0, 30.0, 50.0, 60000.0 / 1001.0,
        60.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0,
    };

    // Pixel aspect ratio codes 1 to 14. Code 1 is the square pixel every cinema here uses.
    private static readonly double[] PixelAspectRatios =
    {
        1.0000, 0.6735, 0.7031, 0.7615, 0.8055, 0.8437, 0.8935,
        0.9157, 0.9815, 1.0255, 1.0695, 1.0950, 1.1575, 1.2051,
    };

    private readonly MpegBitReader _reader;
    private readonly double _startTime;
    private readonly int[] _blockData = new int[DctBlock.Size];
    private readonly byte[] _intraQuantMatrix = new byte[DctBlock.Size];
    private readonly byte[] _nonIntraQuantMatrix = new byte[DctBlock.Size];
    private readonly int[] _dcPredictor = new int[3];

    private int _mbWidth;
    private int _mbHeight;
    private int _mbSize;
    private int _lumaWidth;
    private int _chromaWidth;

    private VideoFrame _frameCurrent = null!;
    private VideoFrame _frameForward = null!;
    private VideoFrame _frameBackward = null!;

    private Motion _forward;
    private Motion _backward;

    private int _startCode = MpegBitReader.NoStartCode;
    private int _pictureType;
    private int _quantizerScale;
    private bool _sliceBegin;
    private int _macroblockAddress;
    private int _mbRow;
    private int _mbCol;
    private int _macroblockType;
    private bool _macroblockIntra;
    private bool _hasReferenceFrame;
    private int _framesDecoded;

    /// <summary>Reads the sequence header at the front of <paramref name="elementaryStream"/>.
    /// <paramref name="startTime"/> is the container timestamp the first picture is shown at, so
    /// video and audio share one clock.</summary>
    public MpegVideoDecoder(byte[] elementaryStream, double startTime = 0.0)
    {
        _reader = new MpegBitReader(elementaryStream);
        _startTime = startTime;
        if (_reader.FindStartCode(StartSequence) == MpegBitReader.NoStartCode)
        {
            throw new InvalidDataException("no MPEG-1 sequence header in the video elementary stream");
        }

        ReadSequenceHeader();
    }

    /// <summary>Picture width in samples, from the sequence header.</summary>
    public int Width { get; private set; }

    /// <summary>Picture height in samples, from the sequence header.</summary>
    public int Height { get; private set; }

    /// <summary>Pictures per second, from the sequence header's own rate code.</summary>
    public double FrameRate { get; private set; }

    /// <summary>Sample aspect ratio, from the sequence header. One means square pixels.</summary>
    public double PixelAspectRatio { get; private set; }

    /// <summary>How many pictures have been handed out so far.</summary>
    public int FramesDecoded => _framesDecoded;

    /// <summary>Decodes up to the next picture in display order, or returns null at the end of
    /// the stream. ⚠ The frame aliases buffers this decoder writes again on the next call; copy
    /// or convert it before asking for another.</summary>
    public VideoFrame? NextFrame()
    {
        VideoFrame? frame = null;
        do
        {
            if (_startCode != StartPicture)
            {
                _startCode = _reader.FindStartCode(StartPicture);
                if (_startCode == MpegBitReader.NoStartCode)
                {
                    return LastReferenceFrame();
                }
            }

            if (!DecodePicture())
            {
                // The picture header was one this decoder cannot use. Dropping the start code
                // sends the next pass looking for another picture rather than re-reading this
                // one's payload as a header, which would not terminate.
                _startCode = MpegBitReader.NoStartCode;
                continue;
            }

            if (_pictureType == PictureBidirectional)
            {
                frame = _frameCurrent;
            }
            else if (_hasReferenceFrame)
            {
                frame = _frameForward;
            }
            else
            {
                _hasReferenceFrame = true;
            }
        }
        while (frame == null);

        return Present(frame);
    }

    /// <summary>Restarts at the first picture, which is what a looping playback needs.</summary>
    public void Rewind()
    {
        _reader.SeekBits(0);
        _startCode = MpegBitReader.NoStartCode;
        _hasReferenceFrame = false;
        _framesDecoded = 0;
    }

    private static byte Clamp(int value) => (byte)(value < 0 ? 0 : value > 255 ? 255 : value);

    private static bool IsSlice(int startCode) =>
        startCode >= StartSliceFirst && startCode <= StartSliceLast;

    private static void WriteBlockSamples(byte[] plane, int at, int stride, ReadOnlySpan<int> samples, bool add)
    {
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                int index = at + (y * stride) + x;
                plane[index] = Clamp(add ? plane[index] + samples[(y * 8) + x] : samples[(y * 8) + x]);
            }
        }
    }

    private static void WriteBlockConstant(byte[] plane, int at, int stride, int value, bool add)
    {
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                int index = at + (y * stride) + x;
                plane[index] = Clamp(add ? plane[index] + value : value);
            }
        }
    }

    // The last reference picture is still unshown when the stream runs out, because a reference
    // picture is always held back one call so a following bidirectional one can precede it.
    private VideoFrame? LastReferenceFrame()
    {
        if (!_hasReferenceFrame || (_pictureType != PictureIntra && _pictureType != PicturePredictive))
        {
            return null;
        }

        _hasReferenceFrame = false;
        return Present(_frameBackward);
    }

    // A picture's time is its ordinal over the sequence's own rate, not the container timestamp
    // of the packet it arrived in: presentation order is what the reordering above establishes.
    private VideoFrame Present(VideoFrame frame)
    {
        frame.Time = _startTime + (_framesDecoded / FrameRate);
        _framesDecoded++;
        return frame;
    }

    private void ReadSequenceHeader()
    {
        Width = _reader.ReadBits(12);
        Height = _reader.ReadBits(12);
        if (Width <= 0 || Height <= 0)
        {
            throw new InvalidDataException($"sequence header declares a {Width}x{Height} picture");
        }

        int aspectCode = Math.Clamp(_reader.ReadBits(4) - 1, 0, PixelAspectRatios.Length - 1);
        PixelAspectRatio = PixelAspectRatios[aspectCode];
        FrameRate = PictureRates[_reader.ReadBits(4)];
        if (FrameRate <= 0.0)
        {
            throw new InvalidDataException("sequence header declares no frame rate");
        }

        _reader.Skip(18 + 1 + 10 + 1);
        ReadQuantMatrix(_intraQuantMatrix, DctBlock.IntraQuantMatrix);
        ReadQuantMatrix(_nonIntraQuantMatrix, DctBlock.NonIntraQuantMatrix);

        _mbWidth = (Width + 15) >> 4;
        _mbHeight = (Height + 15) >> 4;
        _mbSize = _mbWidth * _mbHeight;
        _lumaWidth = _mbWidth << 4;
        _chromaWidth = _mbWidth << 3;

        _frameCurrent = NewFrame();
        _frameForward = NewFrame();
        _frameBackward = NewFrame();
    }

    private VideoFrame NewFrame() =>
        new VideoFrame(Width, Height, _lumaWidth, _mbHeight << 4, _chromaWidth, _mbHeight << 3);

    // A sequence may carry its own quantiser matrices, in zig-zag order; otherwise the
    // standard's defaults stand.
    private void ReadQuantMatrix(byte[] matrix, ReadOnlySpan<byte> fallback)
    {
        if (_reader.ReadBits(1) == 0)
        {
            fallback.CopyTo(matrix);
            return;
        }

        for (int i = 0; i < DctBlock.Size; i++)
        {
            matrix[DctBlock.ZigZag[i]] = (byte)_reader.ReadBits(8);
        }
    }

    // False when the picture header names a coding type this decoder does not carry, which is
    // a D picture or a damaged header, or codes no motion range where one is required.
    private bool DecodePicture()
    {
        _reader.Skip(10);
        _pictureType = _reader.ReadBits(3);
        _reader.Skip(16);
        if (_pictureType <= 0 || _pictureType > PictureBidirectional)
        {
            return false;
        }

        if (_pictureType != PictureIntra && !ReadMotionRange(ref _forward))
        {
            return false;
        }

        if (_pictureType == PictureBidirectional && !ReadMotionRange(ref _backward))
        {
            return false;
        }

        VideoFrame spare = _frameForward;
        bool isReference = _pictureType != PictureBidirectional;
        if (isReference)
        {
            _frameForward = _frameBackward;
        }

        do
        {
            _startCode = _reader.NextStartCode();
        }
        while (_startCode == StartExtension || _startCode == StartUserData);

        while (IsSlice(_startCode))
        {
            DecodeSlice(_startCode & 0xff);
            if (_macroblockAddress >= _mbSize - 1)
            {
                break;
            }

            _startCode = _reader.NextStartCode();
        }

        if (isReference)
        {
            _frameBackward = _frameCurrent;
            _frameCurrent = spare;
        }

        return true;
    }

    // The full-pel flag and the vector range a predicted picture codes its motion in. A zero
    // range is the standard's "no motion coded" marker, and the picture is skipped.
    private bool ReadMotionRange(ref Motion motion)
    {
        motion.FullPel = _reader.ReadBits(1) == 1;
        int code = _reader.ReadBits(3);
        motion.RangeBits = code - 1;
        return code != 0;
    }

    private void DecodeSlice(int slice)
    {
        _sliceBegin = true;
        _macroblockAddress = ((slice - 1) * _mbWidth) - 1;
        _forward.Horizontal = 0;
        _forward.Vertical = 0;
        _backward.Horizontal = 0;
        _backward.Vertical = 0;
        ResetDcPredictors();

        _quantizerScale = _reader.ReadBits(5);
        while (_reader.ReadBits(1) == 1)
        {
            _reader.Skip(8);
        }

        do
        {
            DecodeMacroblock();
        }
        while (_macroblockAddress < _mbSize - 1 && _reader.PeekNonZero(23));
    }

    private void ResetDcPredictors()
    {
        _dcPredictor[0] = 128;
        _dcPredictor[1] = 128;
        _dcPredictor[2] = 128;
    }

    private void DecodeMacroblock()
    {
        if (!AdvanceToMacroblock())
        {
            return;
        }

        _macroblockType = _reader.ReadVlc(VideoVlcTables.MacroblockTypeFor(_pictureType));
        _macroblockIntra = (_macroblockType & 0x01) != 0;
        _forward.IsSet = (_macroblockType & 0x08) != 0;
        _backward.IsSet = (_macroblockType & 0x04) != 0;
        if ((_macroblockType & 0x10) != 0)
        {
            _quantizerScale = _reader.ReadBits(5);
        }

        if (_macroblockIntra)
        {
            _forward.Horizontal = 0;
            _forward.Vertical = 0;
            _backward.Horizontal = 0;
            _backward.Vertical = 0;
        }
        else
        {
            ResetDcPredictors();
            DecodeMotionVectors();
            PredictMacroblock();
        }

        int pattern = (_macroblockType & 0x02) != 0
            ? _reader.ReadVlc(VideoVlcTables.CodedBlockPattern)
            : (_macroblockIntra ? 0x3f : 0);
        for (int block = 0, mask = 0x20; block < 6; block++, mask >>= 1)
        {
            if ((pattern & mask) != 0)
            {
                DecodeBlock(block);
            }
        }
    }

    // The address increment, its stuffing and escape codes, and the run of skipped macroblocks
    // it implies. Returns false when the stream puts the address outside the picture.
    private bool AdvanceToMacroblock()
    {
        int increment = 0;
        int step = _reader.ReadVlc(VideoVlcTables.MacroblockAddressIncrement);
        while (step == 34)
        {
            step = _reader.ReadVlc(VideoVlcTables.MacroblockAddressIncrement);
        }

        while (step == 35)
        {
            increment += 33;
            step = _reader.ReadVlc(VideoVlcTables.MacroblockAddressIncrement);
        }

        increment += step;

        if (_sliceBegin)
        {
            _sliceBegin = false;
            _macroblockAddress += increment;
        }
        else
        {
            if (_macroblockAddress + increment >= _mbSize)
            {
                return false;
            }

            if (increment > 1)
            {
                ResetDcPredictors();
                if (_pictureType == PicturePredictive)
                {
                    _forward.Horizontal = 0;
                    _forward.Vertical = 0;
                }
            }

            while (increment > 1)
            {
                _macroblockAddress++;
                _mbRow = _macroblockAddress / _mbWidth;
                _mbCol = _macroblockAddress % _mbWidth;
                PredictMacroblock();
                increment--;
            }

            _macroblockAddress++;
        }

        _mbRow = _macroblockAddress / _mbWidth;
        _mbCol = _macroblockAddress % _mbWidth;
        return _mbCol < _mbWidth && _mbRow < _mbHeight && _macroblockAddress >= 0;
    }

    private void DecodeMotionVectors()
    {
        if (_forward.IsSet)
        {
            _forward.Horizontal = DecodeMotionVector(_forward.RangeBits, _forward.Horizontal);
            _forward.Vertical = DecodeMotionVector(_forward.RangeBits, _forward.Vertical);
        }
        else if (_pictureType == PicturePredictive)
        {
            _forward.Horizontal = 0;
            _forward.Vertical = 0;
        }

        if (_backward.IsSet)
        {
            _backward.Horizontal = DecodeMotionVector(_backward.RangeBits, _backward.Horizontal);
            _backward.Vertical = DecodeMotionVector(_backward.RangeBits, _backward.Vertical);
        }
    }

    // A vector component is coded as a difference against the previous macroblock's, and wraps
    // within the range the picture header declared rather than saturating.
    private int DecodeMotionVector(int rangeBits, int motion)
    {
        int scale = 1 << rangeBits;
        int code = _reader.ReadVlc(VideoVlcTables.MotionVector);
        int delta;
        if (code != 0 && scale != 1)
        {
            int residual = _reader.ReadBits(rangeBits);
            delta = ((Math.Abs(code) - 1) << rangeBits) + residual + 1;
            if (code < 0)
            {
                delta = -delta;
            }
        }
        else
        {
            delta = code;
        }

        motion += delta;
        if (motion > (scale << 4) - 1)
        {
            motion -= scale << 5;
        }
        else if (motion < -(scale << 4))
        {
            motion += scale << 5;
        }

        return motion;
    }

    private void PredictMacroblock()
    {
        int forwardHorizontal = _forward.FullPel ? _forward.Horizontal << 1 : _forward.Horizontal;
        int forwardVertical = _forward.FullPel ? _forward.Vertical << 1 : _forward.Vertical;
        if (_pictureType != PictureBidirectional)
        {
            CopyMacroblock(_frameForward, forwardHorizontal, forwardVertical, false);
            return;
        }

        int backwardHorizontal = _backward.FullPel ? _backward.Horizontal << 1 : _backward.Horizontal;
        int backwardVertical = _backward.FullPel ? _backward.Vertical << 1 : _backward.Vertical;
        if (!_forward.IsSet)
        {
            CopyMacroblock(_frameBackward, backwardHorizontal, backwardVertical, false);
            return;
        }

        CopyMacroblock(_frameForward, forwardHorizontal, forwardVertical, false);
        if (_backward.IsSet)
        {
            CopyMacroblock(_frameBackward, backwardHorizontal, backwardVertical, true);
        }
    }

    private void CopyMacroblock(VideoFrame source, int motionHorizontal, int motionVertical, bool interpolate)
    {
        MotionCompensation.Predict(
            source.Luma, _frameCurrent.Luma, _lumaWidth,
            _mbRow, _mbCol, 16, motionHorizontal, motionVertical, interpolate);
        MotionCompensation.Predict(
            source.Cr, _frameCurrent.Cr, _chromaWidth,
            _mbRow, _mbCol, 8, motionHorizontal / 2, motionVertical / 2, interpolate);
        MotionCompensation.Predict(
            source.Cb, _frameCurrent.Cb, _chromaWidth,
            _mbRow, _mbCol, 8, motionHorizontal / 2, motionVertical / 2, interpolate);
    }

    private void DecodeBlock(int block)
    {
        int count = 0;
        byte[] quantMatrix = _macroblockIntra ? _intraQuantMatrix : _nonIntraQuantMatrix;
        if (_macroblockIntra)
        {
            count = 1;
            ReadDcCoefficient(block);
        }

        if (!ReadAcCoefficients(quantMatrix, ref count))
        {
            // ⚠ Do not drop the clear; a half-filled block left behind leaks into every later
            // block of the picture rather than only losing this one.
            Array.Clear(_blockData, 0, _blockData.Length);
            return;
        }

        StoreBlock(block, count);
    }

    // An intra block's DC is a difference against the previous block of the same plane, and the
    // shift is the dequantise and premultiply the AC path does per coefficient.
    private void ReadDcCoefficient(int block)
    {
        int planeIndex = block > 3 ? block - 3 : 0;
        int predictor = _dcPredictor[planeIndex];
        int size = _reader.ReadVlc(VideoVlcTables.DctSizeFor(planeIndex));
        if (size > 0)
        {
            int differential = _reader.ReadBits(size);
            _blockData[0] = (differential & (1 << (size - 1))) != 0
                ? predictor + differential
                : predictor + (-(1 << size) | (differential + 1));
        }
        else
        {
            _blockData[0] = predictor;
        }

        _dcPredictor[planeIndex] = _blockData[0];
        _blockData[0] <<= 3 + 5;
    }

    // Returns false when the run puts a coefficient outside the block, which only a corrupt
    // stream does; the block is then left as it was.
    private bool ReadAcCoefficients(byte[] quantMatrix, ref int count)
    {
        while (true)
        {
            int run;
            int level;
            int coefficient = _reader.ReadVlc(VideoVlcTables.DctCoefficient);
            if (coefficient == 0x0001 && count > 0 && _reader.ReadBits(1) == 0)
            {
                return true;
            }

            if (coefficient == 0xffff)
            {
                run = _reader.ReadBits(6);
                level = ReadEscapeLevel();
            }
            else
            {
                run = coefficient >> 8;
                level = coefficient & 0xff;
                if (_reader.ReadBits(1) == 1)
                {
                    level = -level;
                }
            }

            count += run;
            if (count < 0 || count >= DctBlock.Size)
            {
                return false;
            }

            int index = DctBlock.ZigZag[count];
            count++;
            level = DctBlock.Dequantize(level, _quantizerScale, quantMatrix[index], _macroblockIntra);
            _blockData[index] = level * DctBlock.Premultiplier[index];
        }
    }

    // The escape form spells the level out in eight bits, with two values reserved for a second
    // byte that reaches the ends of the range.
    private int ReadEscapeLevel()
    {
        int level = _reader.ReadBits(8);
        if (level == 0)
        {
            return _reader.ReadBits(8);
        }

        if (level == 128)
        {
            return _reader.ReadBits(8) - 256;
        }

        return level > 128 ? level - 256 : level;
    }

    private void StoreBlock(int block, int count)
    {
        byte[] plane;
        int stride;
        int at;
        if (block < 4)
        {
            plane = _frameCurrent.Luma;
            stride = _lumaWidth;
            at = ((_mbRow * _lumaWidth) + _mbCol) << 4;
            at += (block & 1) != 0 ? 8 : 0;
            at += (block & 2) != 0 ? _lumaWidth << 3 : 0;
        }
        else
        {
            plane = block == 4 ? _frameCurrent.Cb : _frameCurrent.Cr;
            stride = _chromaWidth;
            at = ((_mbRow * _lumaWidth) << 2) + (_mbCol << 3);
        }

        // A block whose only coefficient is the DC is a flat patch, and the transform of one
        // would come out flat anyway; taking it directly is the common case in a still shot.
        if (count == 1)
        {
            WriteBlockConstant(plane, at, stride, (_blockData[0] + 128) >> 8, !_macroblockIntra);
            _blockData[0] = 0;
            return;
        }

        DctBlock.InverseTransform(_blockData);
        WriteBlockSamples(plane, at, stride, _blockData, !_macroblockIntra);
        Array.Clear(_blockData, 0, _blockData.Length);
    }

    // One direction's motion state, carried across macroblocks because each vector is coded as
    // a difference against the last.
    private struct Motion
    {
        public bool FullPel;
        public bool IsSet;
        public int RangeBits;
        public int Horizontal;
        public int Vertical;
    }
}
