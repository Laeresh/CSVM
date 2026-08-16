using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The WAV parser and the MS ADPCM decoder (<c>docs/formats/sounds.md</c>: the game ships fmt
/// tag 2, 4-bit, 22050 Hz). Every input here is assembled field by field below — no file from
/// the install is read, and each expected sample is derived in the comment above it from the
/// decoder's published arithmetic, so a wrong answer is visible without running anything.
/// </summary>
public class WavFileTests
{
    private const int Rate = 22050;

    [Fact]
    public void Pcm16PassesThroughUnchanged()
    {
        short[] samples = { 1000, -1000, short.MaxValue, short.MinValue };
        var wav = WavFile.Parse(Riff(
            ("fmt ", PcmFmt(bits: 16, channels: 1)),
            ("data", Shorts(samples))));

        Assert.Equal(1, wav.Channels);
        Assert.Equal(Rate, wav.SampleRate);
        Assert.Equal(samples, wav.Samples);
        Assert.Equal(4, wav.Frames);
    }

    [Fact]
    public void Pcm8IsRecentredAndScaledToPcm16()
    {
        // unsigned 8-bit: (byte - 128) << 8.
        var wav = WavFile.Parse(Riff(
            ("fmt ", PcmFmt(bits: 8, channels: 1)),
            ("data", new byte[] { 0, 128, 255 })));

        Assert.Equal(new short[] { -32768, 0, 32512 }, wav.Samples);
    }

    [Fact]
    public void MsAdpcmDecodesTheBlockHeaderAndItsNibbles()
    {
        // Coefficient set {256, 0} collapses the predictor to s1 + nibble*delta, so every step
        // (header predictor 0, delta 16, s1 100, s2 50, data 0x1F 0x80) is checkable by hand
        // against MS ADPCM's standard nibble/delta update.
        var wav = WavFile.Parse(AdpcmMono(new byte[] { 0x1F, 0x80 }, factSamples: 0));

        Assert.Equal(new short[] { 50, 100, 116, 100, -28, -28 }, wav.Samples);
        Assert.Equal(1, wav.Channels);
        Assert.Equal(Rate, wav.SampleRate);
    }

    [Fact]
    public void TheFactChunkTruncatesTheDecodedTail()
    {
        // A block always decodes a whole samplesPerBlock; 'fact' says how many of them are real.
        var wav = WavFile.Parse(AdpcmMono(new byte[] { 0x1F, 0x80 }, factSamples: 5));

        Assert.Equal(new short[] { 50, 100, 116, 100, -28 }, wav.Samples);
    }

    [Fact]
    public void StereoNibblesAlternateBetweenChannels()
    {
        // Same coefficients as the mono case; left starts at s2=50/s1=100, right at s2=150/s1=200.
        var wav = WavFile.Parse(AdpcmStereo(new byte[] { 0x1F, 0x80 }));

        Assert.Equal(2, wav.Channels);
        Assert.Equal(new short[] { 50, 150, 100, 200, 116, 184, -12, 184 }, wav.Samples);
        Assert.Equal(4, wav.Frames);
    }

    [Fact]
    public void AnOddSizedChunkIsFollowedByItsPadByte()
    {
        // RIFF chunks are word-aligned; a walker that forgets the pad byte lands mid-header
        // and loses every chunk after the odd one.
        var wav = WavFile.Parse(Riff(
            ("junk", new byte[] { 1, 2, 3 }),
            ("fmt ", PcmFmt(bits: 16, channels: 1)),
            ("data", Shorts(new short[] { 7, -7 }))));

        Assert.Equal(new short[] { 7, -7 }, wav.Samples);
    }

    [Fact]
    public void SomethingThatIsNotRiffWaveThrows()
    {
        Assert.Throws<InvalidDataException>(() => WavFile.Parse(Encoding.ASCII.GetBytes("NOTARIFFFILE")));
    }

    [Fact]
    public void AWavWithoutADataChunkThrows()
    {
        Assert.Throws<InvalidDataException>(() =>
            WavFile.Parse(Riff(("fmt ", PcmFmt(bits: 16, channels: 1)))));
    }

    [Fact]
    public void AnUnsupportedCodecThrowsRatherThanReturningSilence()
    {
        var fmt = PcmFmt(bits: 32, channels: 1);
        fmt[0] = 3; // IEEE float
        Assert.Throws<NotSupportedException>(() =>
            WavFile.Parse(Riff(("fmt ", fmt), ("data", new byte[8]))));
    }

    // ---- fixture builders: every byte below is written here, none is read from the install ----

    private static byte[] Riff(params (string Id, byte[] Body)[] chunks)
    {
        var body = new List<byte>();
        body.AddRange(Encoding.ASCII.GetBytes("WAVE"));
        foreach (var (id, chunk) in chunks)
        {
            body.AddRange(Encoding.ASCII.GetBytes(id));
            body.AddRange(BitConverter.GetBytes(chunk.Length));
            body.AddRange(chunk);
            if ((chunk.Length & 1) == 1)
            {
                body.Add(0); // word alignment
            }
        }
        var file = new List<byte>();
        file.AddRange(Encoding.ASCII.GetBytes("RIFF"));
        file.AddRange(BitConverter.GetBytes(body.Count));
        file.AddRange(body);
        return file.ToArray();
    }

    // The 16-byte canonical fmt chunk: tag, channels, rate, byte rate, block align, bits.
    private static byte[] PcmFmt(int bits, int channels)
    {
        int blockAlign = channels * bits / 8;
        var f = new List<byte>();
        f.AddRange(BitConverter.GetBytes((ushort)1));
        f.AddRange(BitConverter.GetBytes((ushort)channels));
        f.AddRange(BitConverter.GetBytes(Rate));
        f.AddRange(BitConverter.GetBytes(Rate * blockAlign));
        f.AddRange(BitConverter.GetBytes((ushort)blockAlign));
        f.AddRange(BitConverter.GetBytes((ushort)bits));
        return f.ToArray();
    }

    // The MS ADPCM fmt chunk: the canonical 16 bytes then cbSize, samplesPerBlock, the
    // coefficient count and the coefficient pairs.
    private static byte[] AdpcmFmt(int channels, int blockAlign, int samplesPerBlock,
        short coef1, short coef2)
    {
        var f = new List<byte>();
        f.AddRange(BitConverter.GetBytes((ushort)2));
        f.AddRange(BitConverter.GetBytes((ushort)channels));
        f.AddRange(BitConverter.GetBytes(Rate));
        f.AddRange(BitConverter.GetBytes(Rate * blockAlign / samplesPerBlock));
        f.AddRange(BitConverter.GetBytes((ushort)blockAlign));
        f.AddRange(BitConverter.GetBytes((ushort)4));
        f.AddRange(BitConverter.GetBytes((ushort)8));  // cbSize: samplesPerBlock + numCoef + 1 pair
        f.AddRange(BitConverter.GetBytes((ushort)samplesPerBlock));
        f.AddRange(BitConverter.GetBytes((ushort)1));  // one coefficient set
        f.AddRange(BitConverter.GetBytes(coef1));
        f.AddRange(BitConverter.GetBytes(coef2));
        return f.ToArray();
    }

    private static byte[] AdpcmMono(byte[] nibbleBytes, int factSamples)
    {
        int blockAlign = 7 + nibbleBytes.Length;          // 1 predictor + 3 int16 + payload
        int samplesPerBlock = 2 + nibbleBytes.Length * 2; // the two header samples + one per nibble
        var block = new List<byte> { 0 };                 // predictor index -> coefficient set 0
        block.AddRange(BitConverter.GetBytes((short)16)); // delta
        block.AddRange(BitConverter.GetBytes((short)100)); // sample 1
        block.AddRange(BitConverter.GetBytes((short)50));  // sample 2 (the older one)
        block.AddRange(nibbleBytes);

        var chunks = new List<(string, byte[])>
        {
            ("fmt ", AdpcmFmt(1, blockAlign, samplesPerBlock, 256, 0)),
        };
        if (factSamples > 0)
        {
            chunks.Add(("fact", BitConverter.GetBytes(factSamples)));
        }
        chunks.Add(("data", block.ToArray()));
        return Riff(chunks.ToArray());
    }

    private static byte[] AdpcmStereo(byte[] nibbleBytes)
    {
        int blockAlign = 14 + nibbleBytes.Length;     // 2 predictors + 6 int16 + payload
        int samplesPerBlock = 2 + nibbleBytes.Length; // two nibbles = one stereo frame
        var block = new List<byte> { 0, 0 };
        block.AddRange(BitConverter.GetBytes((short)16));  // delta L
        block.AddRange(BitConverter.GetBytes((short)16));  // delta R
        block.AddRange(BitConverter.GetBytes((short)100)); // sample 1 L
        block.AddRange(BitConverter.GetBytes((short)200)); // sample 1 R
        block.AddRange(BitConverter.GetBytes((short)50));  // sample 2 L
        block.AddRange(BitConverter.GetBytes((short)150)); // sample 2 R
        block.AddRange(nibbleBytes);

        return Riff(
            ("fmt ", AdpcmFmt(2, blockAlign, samplesPerBlock, 256, 0)),
            ("data", block.ToArray()));
    }

    private static byte[] Shorts(short[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
