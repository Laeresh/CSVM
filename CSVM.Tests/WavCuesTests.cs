using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>The RIFF cue chunk the briefing's <c>WaitForMarker</c> opcodes index. The bytes here
/// are hand-authored from the RIFF layout, so the ordering rule is pinned without an extraction.</summary>
public class WavCuesTests
{
    /// <summary>⚠ Cue ids are not the order. The shipped briefing wavs store points out of
    /// position order, and a marker number indexes them by ascending sample offset; reading the id
    /// as the index runs a reveal's beats backwards.</summary>
    [Fact]
    public void CuePointsComeBackInAscendingTimeNotInFileOrder()
    {
        var times = WavCues.Read(Wav(rate: 100, offsets: new uint[] { 300, 100, 200 }));

        Assert.Equal(new[] { 1d, 2d, 3d }, times);
    }

    [Fact]
    public void AWavWithNoCueChunkHasNoMarkers()
    {
        Assert.Empty(WavCues.Read(Wav(rate: 100, offsets: Array.Empty<uint>())));
    }

    [Fact]
    public void SomethingThatIsNotAWavIsNoMarkersRatherThanAThrow()
    {
        Assert.Empty(WavCues.Read(new byte[] { 1, 2, 3, 4 }));
        Assert.Empty(WavCues.Read(Array.Empty<byte>()));
    }

    [Fact]
    public void AnAbsentArchiveOrFileIsNoMarkers()
    {
        Assert.Empty(WavCues.ReadFrom(Path.Combine(TestData.TempDir(), "nothing"), "x.wav"));
        Assert.Empty(WavCues.ReadFrom(TestData.TempDir(), "absent_briefing.wav"));
    }

    [Fact]
    public void ItReadsAWavOutOfADirectory()
    {
        var dir = TestData.TempDir();
        File.WriteAllBytes(Path.Combine(dir, "narration.wav"), Wav(rate: 100, offsets: new uint[] { 250 }));

        Assert.Equal(new[] { 2.5d }, WavCues.ReadFrom(dir, "narration.wav"));
    }

    /// <summary>The real narration: every briefing wav still carries its cue chunk after
    /// extraction, and the state that plays it waits on exactly that many markers.</summary>
    [ExtractedDataFact]
    public void TheExtractedNarrationStillCarriesItsCuePoints()
    {
        var soundsh = CSVM.SessionPaths.PreferUnzipped(
            Path.Combine(TestData.ExtractedRoot!, "soundsh.zip"));

        var times = WavCues.ReadFrom(soundsh, "c1-HA-m1_briefing.wav");

        Assert.Equal(8, times.Count);
        for (int i = 1; i < times.Count; i++)
        {
            Assert.True(times[i] > times[i - 1], "cue times must come back ascending");
        }

        Assert.InRange(times[0], 14d, 15d);
        Assert.InRange(times[^1], 94d, 95d);
    }

    // A minimal WAVE: fmt, an optional cue chunk, and an empty data chunk. Only the sample rate
    // and the cue points' sample offsets matter to the reader under test.
    private static byte[] Wav(int rate, uint[] offsets)
    {
        var body = new List<byte>();
        body.AddRange(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        body.AddRange(Chunk("fmt ", Fmt(rate)));
        if (offsets.Length > 0)
        {
            body.AddRange(Chunk("cue ", Cue(offsets)));
        }

        body.AddRange(Chunk("data", Array.Empty<byte>()));
        var wav = new List<byte>();
        wav.AddRange(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        wav.AddRange(BitConverter.GetBytes(body.Count));
        wav.AddRange(body);
        return wav.ToArray();
    }

    private static byte[] Fmt(int rate)
    {
        var fmt = new List<byte>();
        fmt.AddRange(BitConverter.GetBytes((short)1));
        fmt.AddRange(BitConverter.GetBytes((short)1));
        fmt.AddRange(BitConverter.GetBytes(rate));
        fmt.AddRange(BitConverter.GetBytes(rate * 2));
        fmt.AddRange(BitConverter.GetBytes((short)2));
        fmt.AddRange(BitConverter.GetBytes((short)16));
        return fmt.ToArray();
    }

    // Each point is id, position, the chunk it indexes, that chunk's start, the block start and
    // the sample offset; the ids run 1..n in file order the way the shipped wavs write them.
    private static byte[] Cue(uint[] offsets)
    {
        var cue = new List<byte>();
        cue.AddRange(BitConverter.GetBytes((uint)offsets.Length));
        for (int i = 0; i < offsets.Length; i++)
        {
            cue.AddRange(BitConverter.GetBytes((uint)(i + 1)));
            cue.AddRange(BitConverter.GetBytes(offsets[i]));
            cue.AddRange(System.Text.Encoding.ASCII.GetBytes("data"));
            cue.AddRange(BitConverter.GetBytes(0u));
            cue.AddRange(BitConverter.GetBytes(0u));
            cue.AddRange(BitConverter.GetBytes(offsets[i]));
        }

        return cue.ToArray();
    }

    private static byte[] Chunk(string id, byte[] body)
    {
        var chunk = new List<byte>();
        chunk.AddRange(System.Text.Encoding.ASCII.GetBytes(id));
        chunk.AddRange(BitConverter.GetBytes(body.Length));
        chunk.AddRange(body);
        if ((body.Length & 1) != 0)
        {
            chunk.Add(0);
        }

        return chunk.ToArray();
    }
}
