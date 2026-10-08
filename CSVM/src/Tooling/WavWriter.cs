using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CSVM.Tooling;

/// <summary>
/// Encodes mono 16-bit samples as a RIFF/WAVE file, plain PCM or MS ADPCM, managed and
/// dependency-free like <see cref="Extraction.PngWriter"/>. The ADPCM layout is the one
/// <see cref="Mech3.WavFile"/> decodes, and the one <c>docs/formats/sounds.md</c> says the game ships.
/// That is fmt tag 2, 4 bits a sample and the seven standard coefficient pairs. A <c>fact</c> chunk
/// carries the true sample count. Engine-free, so the unit tests round-trip it through the reader.
/// </summary>
public static class WavWriter
{
    /// <summary>The bytes of one ADPCM block. 256 holds 500 samples a block, mono.</summary>
    public const int AdpcmBlockAlign = 256;

    // The standard coefficient pairs, in the order every MS ADPCM fmt chunk lists them.
    private static readonly (short C1, short C2)[] Coefficients =
    {
        (256, 0), (512, -256), (0, 0), (192, 64), (240, 0), (460, -208), (392, -232),
    };

    // The step adaptation the decoder applies per nibble, indexed by the unsigned nibble.
    private static readonly int[] AdaptTable =
    {
        230, 230, 230, 230, 307, 409, 512, 614,
        768, 614, 512, 409, 307, 230, 230, 230,
    };

    /// <summary>The samples a mono ADPCM block of <see cref="AdpcmBlockAlign"/> bytes carries: the
    /// two header samples and two per remaining byte.</summary>
    public static int AdpcmSamplesPerBlock => ((AdpcmBlockAlign - 7) * 2) + 2;

    /// <summary>A whole PCM WAV (fmt tag 1, 16 bits, mono) at <paramref name="rate"/> Hz.</summary>
    public static byte[] Pcm16(ReadOnlySpan<short> samples, int rate)
    {
        var fmt = new List<byte>();
        AddCanonicalFmt(fmt, tag: 1, rate, byteRate: rate * 2, blockAlign: 2, bits: 16);
        var data = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            BitConverter.TryWriteBytes(data.AsSpan(i * 2), samples[i]);
        }

        return Riff(("fmt ", fmt.ToArray()), ("data", data));
    }

    /// <summary>A whole MS ADPCM WAV (fmt tag 2, 4 bits, mono) at <paramref name="rate"/> Hz. The
    /// last block is padded to full size and the <c>fact</c> chunk trims the decode back to
    /// <paramref name="samples"/>' own length, as the reader expects.</summary>
    public static byte[] MsAdpcm(ReadOnlySpan<short> samples, int rate)
    {
        if (samples.Length < 2)
        {
            throw new ArgumentException("an ADPCM block opens on two header samples", nameof(samples));
        }

        int perBlock = AdpcmSamplesPerBlock;
        int blocks = (samples.Length + perBlock - 1) / perBlock;
        var data = new byte[blocks * AdpcmBlockAlign];
        for (int b = 0; b < blocks; b++)
        {
            int start = b * perBlock;
            var block = new short[perBlock];
            for (int i = 0; i < perBlock; i++)
            {
                // The padded tail repeats the last sample, so it costs the predictor nothing.
                block[i] = samples[Math.Min(start + i, samples.Length - 1)];
            }

            EncodeBlock(block, data.AsSpan(b * AdpcmBlockAlign, AdpcmBlockAlign));
        }

        var fmt = new List<byte>();
        AddCanonicalFmt(fmt, tag: 2, rate, byteRate: rate * AdpcmBlockAlign / perBlock, blockAlign: AdpcmBlockAlign, bits: 4);
        AddShort(fmt, (short)(4 + (Coefficients.Length * 4)));
        AddShort(fmt, (short)perBlock);
        AddShort(fmt, (short)Coefficients.Length);
        foreach (var (c1, c2) in Coefficients)
        {
            AddShort(fmt, c1);
            AddShort(fmt, c2);
        }

        return Riff(("fmt ", fmt.ToArray()), ("fact", BitConverter.GetBytes(samples.Length)), ("data", data));
    }

    // Encodes one block under each coefficient pair and keeps the one that tracks the input best.
    private static void EncodeBlock(short[] block, Span<byte> into)
    {
        long bestError = long.MaxValue;
        byte[]? best = null;
        for (int pair = 0; pair < Coefficients.Length; pair++)
        {
            var bytes = new byte[into.Length];
            long error = EncodeWith(block, pair, bytes);
            if (error < bestError)
            {
                bestError = error;
                best = bytes;
            }
        }

        best!.CopyTo(into);
    }

    // One block under one coefficient pair, tracked exactly as the decoder replays it. The header
    // is pair, step, s1 and s2; then comes one nibble per sample, high nibble first.
    private static long EncodeWith(short[] block, int pair, byte[] into)
    {
        var (c1, c2) = Coefficients[pair];
        int s2 = block[0], s1 = block[1];
        int firstPrediction = ((s1 * c1) + (s2 * c2)) >> 8;
        int delta = Math.Max(Math.Abs(block[2] - firstPrediction) / 2, 16);
        into[0] = (byte)pair;
        BitConverter.TryWriteBytes(into.AsSpan(1), (short)Math.Min(delta, short.MaxValue));
        BitConverter.TryWriteBytes(into.AsSpan(3), (short)s1);
        BitConverter.TryWriteBytes(into.AsSpan(5), (short)s2);
        delta = Math.Min(delta, short.MaxValue);

        long error = 0;
        for (int i = 2; i < block.Length; i++)
        {
            int predicted = ((s1 * c1) + (s2 * c2)) >> 8;
            int signed = Math.Clamp((int)Math.Round((block[i] - predicted) / (double)delta), -8, 7);
            int decoded = Math.Clamp(predicted + (signed * delta), short.MinValue, short.MaxValue);
            int nibble = signed & 0xF;
            int at = 7 + ((i - 2) / 2);
            into[at] = (i & 1) == 0 ? (byte)(nibble << 4) : (byte)(into[at] | nibble);
            error += (long)(decoded - block[i]) * (decoded - block[i]);
            s2 = s1;
            s1 = decoded;
            delta = Math.Max((AdaptTable[nibble] * delta) >> 8, 16);
        }

        return error;
    }

    // The 16-byte fmt body every WAV opens with: tag, channels, rate, byte rate, block align, bits.
    private static void AddCanonicalFmt(List<byte> fmt, short tag, int rate, int byteRate, int blockAlign, int bits)
    {
        AddShort(fmt, tag);
        AddShort(fmt, 1);
        fmt.AddRange(BitConverter.GetBytes(rate));
        fmt.AddRange(BitConverter.GetBytes(byteRate));
        AddShort(fmt, (short)blockAlign);
        AddShort(fmt, (short)bits);
    }

    private static void AddShort(List<byte> into, short value) => into.AddRange(BitConverter.GetBytes(value));

    private static byte[] Riff(params (string Id, byte[] Body)[] chunks)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(0);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            foreach (var (id, body) in chunks)
            {
                writer.Write(Encoding.ASCII.GetBytes(id));
                writer.Write(body.Length);
                writer.Write(body);
                if ((body.Length & 1) == 1)
                {
                    writer.Write((byte)0);
                }
            }
        }

        byte[] file = stream.ToArray();
        BitConverter.TryWriteBytes(file.AsSpan(4), file.Length - 8);
        return file;
    }
}
