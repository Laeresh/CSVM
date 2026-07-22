using System;
using System.IO;

namespace CSVM.Mech3;

/// <summary>
/// Minimal WAV reader for the game's sound extractions (soundsh/soundsl.zbd → WAVs).
/// The game ships MS ADPCM (format 2, 4-bit, 22050 Hz) which Godot's AudioStreamWav
/// cannot load, so we decode to 16-bit PCM here; plain PCM 8/16-bit passes through.
/// Pure C# (no Godot types) so the decoder can be exercised standalone.
/// </summary>
public sealed class WavFile
{
    public short[] Samples = Array.Empty<short>(); // interleaved PCM16
    public int Channels;
    public int SampleRate;
    public int Frames => Channels == 0 ? 0 : Samples.Length / Channels;

    public static WavFile Parse(byte[] bytes)
    {
        if (bytes.Length < 12 || BitConverter.ToUInt32(bytes, 0) != 0x46464952 /*RIFF*/
            || BitConverter.ToUInt32(bytes, 8) != 0x45564157 /*WAVE*/)
            throw new InvalidDataException("not a RIFF/WAVE file");

        int fmtTag = 0, channels = 0, rate = 0, blockAlign = 0, bits = 0;
        int samplesPerBlock = 0, factSamples = 0;
        short[] coef1 = Array.Empty<short>(), coef2 = Array.Empty<short>();
        int dataOffset = -1, dataLength = 0;

        for (int pos = 12; pos + 8 <= bytes.Length;)
        {
            uint id = BitConverter.ToUInt32(bytes, pos);
            int size = BitConverter.ToInt32(bytes, pos + 4);
            int body = pos + 8;
            switch (id)
            {
                case 0x20746d66: // 'fmt '
                    fmtTag = BitConverter.ToUInt16(bytes, body);
                    channels = BitConverter.ToUInt16(bytes, body + 2);
                    rate = BitConverter.ToInt32(bytes, body + 4);
                    blockAlign = BitConverter.ToUInt16(bytes, body + 12);
                    bits = BitConverter.ToUInt16(bytes, body + 14);
                    if (fmtTag == 2 && size >= 22)
                    {
                        samplesPerBlock = BitConverter.ToUInt16(bytes, body + 18);
                        int numCoef = BitConverter.ToUInt16(bytes, body + 20);
                        coef1 = new short[numCoef];
                        coef2 = new short[numCoef];
                        for (int i = 0; i < numCoef && body + 22 + i * 4 + 4 <= body + size; i++)
                        {
                            coef1[i] = BitConverter.ToInt16(bytes, body + 22 + i * 4);
                            coef2[i] = BitConverter.ToInt16(bytes, body + 24 + i * 4);
                        }
                    }
                    break;
                case 0x74636166: // 'fact' — decoded sample count per channel
                    factSamples = BitConverter.ToInt32(bytes, body);
                    break;
                case 0x61746164: // 'data'
                    dataOffset = body;
                    dataLength = Math.Min(size, bytes.Length - body);
                    break;
            }
            pos = body + size + (size & 1);
        }
        if (dataOffset < 0 || channels == 0)
            throw new InvalidDataException("WAV missing fmt/data chunk");

        var wav = new WavFile { Channels = channels, SampleRate = rate };
        switch (fmtTag)
        {
            case 1 when bits == 16:
                wav.Samples = new short[dataLength / 2];
                Buffer.BlockCopy(bytes, dataOffset, wav.Samples, 0, wav.Samples.Length * 2);
                break;
            case 1 when bits == 8: // unsigned 8-bit
                wav.Samples = new short[dataLength];
                for (int i = 0; i < dataLength; i++)
                    wav.Samples[i] = (short)((bytes[dataOffset + i] - 128) << 8);
                break;
            case 2:
                wav.Samples = DecodeMsAdpcm(bytes, dataOffset, dataLength,
                    channels, blockAlign, samplesPerBlock, coef1, coef2, factSamples);
                break;
            default:
                throw new NotSupportedException($"WAV format {fmtTag}/{bits}-bit not supported");
        }
        return wav;
    }

    private static readonly int[] AdaptTable =
    {
        230, 230, 230, 230, 307, 409, 512, 614,
        768, 614, 512, 409, 307, 230, 230, 230,
    };

    private static short[] DecodeMsAdpcm(byte[] d, int offset, int length, int channels,
        int blockAlign, int samplesPerBlock, short[] coef1, short[] coef2, int factSamples)
    {
        int blocks = length / blockAlign;
        int totalFrames = blocks * samplesPerBlock;
        if (factSamples > 0)
            totalFrames = Math.Min(totalFrames, factSamples);
        var outBuf = new short[totalFrames * channels];
        int outFrame = 0;

        var pred = new int[channels];
        var delta = new int[channels];
        var s1 = new int[channels];
        var s2 = new int[channels];

        for (int b = 0; b < blocks && outFrame < totalFrames; b++)
        {
            int p = offset + b * blockAlign;
            for (int c = 0; c < channels; c++)
                pred[c] = Math.Min((int)d[p++], coef1.Length - 1);
            for (int c = 0; c < channels; c++, p += 2)
                delta[c] = BitConverter.ToInt16(d, p);
            for (int c = 0; c < channels; c++, p += 2)
                s1[c] = BitConverter.ToInt16(d, p);
            for (int c = 0; c < channels; c++, p += 2)
                s2[c] = BitConverter.ToInt16(d, p);

            // the two header samples are the first two output frames (s2 is older)
            for (int c = 0; c < channels; c++)
                outBuf[outFrame * channels + c] = (short)s2[c];
            if (++outFrame >= totalFrames) break;
            for (int c = 0; c < channels; c++)
                outBuf[outFrame * channels + c] = (short)s1[c];
            if (++outFrame >= totalFrames) break;

            int blockEnd = offset + Math.Min((b + 1) * blockAlign, length);
            int nibbleIndex = 0; // even = high nibble; channels alternate per nibble
            int framesLeft = Math.Min(samplesPerBlock, totalFrames - outFrame + 2) - 2;
            for (int i = 0; i < framesLeft * channels && p < blockEnd; i++)
            {
                int nibble = (nibbleIndex++ & 1) == 0 ? d[p] >> 4 : d[p++] & 0xF;
                int c = i % channels;
                int signed = nibble >= 8 ? nibble - 16 : nibble;
                int predicted = ((s1[c] * coef1[pred[c]] + s2[c] * coef2[pred[c]]) >> 8)
                                + signed * delta[c];
                predicted = Math.Clamp(predicted, short.MinValue, short.MaxValue);
                s2[c] = s1[c];
                s1[c] = predicted;
                delta[c] = Math.Max((AdaptTable[nibble] * delta[c]) >> 8, 16);
                outBuf[outFrame * channels + c] = (short)predicted;
                if (c == channels - 1 && ++outFrame >= totalFrames)
                    break;
            }
        }
        if (outFrame < totalFrames)
            Array.Resize(ref outBuf, outFrame * channels);
        return outBuf;
    }
}
