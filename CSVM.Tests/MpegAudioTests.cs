using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Video;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The layer II decoder end to end. The first half runs on frames built from the standard's own
/// syntax, so it gates a machine with no game install; the second half decodes the sound track
/// of each of the ten cinemas and is skipped where they are unreachable.
/// </summary>
public class MpegAudioTests
{
    private const int SampleRate = 44100;
    private const int SamplesPerFrame = 1152;

    // A frame is three parts, each with its own scale factor and each a third of the samples.
    private const int PartSamples = SamplesPerFrame / 3;

    [Fact]
    public void AMonoStreamDecodesToOneChannelOfSilence()
    {
        var decoder = new MpegAudioDecoder(MonoStream(4, excite: false));

        Assert.Equal(1, decoder.Channels);
        Assert.Equal(SampleRate, decoder.SampleRate);
        Assert.Equal(64, decoder.BitRate);

        int frames = 0;
        AudioFrame? frame;
        while ((frame = decoder.NextFrame()) != null)
        {
            Assert.Equal(SamplesPerFrame, frame.Samples.Length);
            Assert.All(frame.Samples, sample => Assert.Equal(0.0f, sample));
            frames++;
        }

        Assert.Equal(4, frames);
        Assert.Equal(0, decoder.ResyncCount);
    }

    [Fact]
    public void AStereoStreamDecodesToTwoInterleavedChannels()
    {
        var decoder = new MpegAudioDecoder(MpegTestStreams.Layer2Stream(
            3, MpegTestStreams.ModeStereo, MpegTestStreams.BitRateIndex128,
            MpegTestStreams.SampleRateIndex44100, 0, excite: false));

        Assert.Equal(2, decoder.Channels);
        Assert.Equal(128, decoder.BitRate);

        AudioFrame frame = Assert.IsType<AudioFrame>(decoder.NextFrame());
        Assert.Equal(SamplesPerFrame * 2, frame.Samples.Length);
        Assert.Equal(SamplesPerFrame, frame.SamplesPerChannel);
    }

    /// <summary>The one channel the fixture allocates a subband to carries sound and the other
    /// stays exactly silent, which is what proves the interleave and the per-channel filter
    /// bank are not writing over each other.</summary>
    [Fact]
    public void OnlyTheChannelTheFixtureAllocatesCarriesSound()
    {
        var decoder = new MpegAudioDecoder(MpegTestStreams.Layer2Stream(
            2, MpegTestStreams.ModeStereo, MpegTestStreams.BitRateIndex128,
            MpegTestStreams.SampleRateIndex44100, 0, excite: true));

        AudioFrame frame = Assert.IsType<AudioFrame>(decoder.NextFrame());
        float loudest = 0.0f;
        for (int sample = 0; sample < SamplesPerFrame; sample++)
        {
            loudest = Math.Max(loudest, Math.Abs(frame.Samples[sample * 2]));
            Assert.Equal(0.0f, frame.Samples[(sample * 2) + 1]);
        }

        // A loose bound: the constant the fixture feeds the lowest subband is full scale, and a
        // wrong window or divisor overshoots or undershoots this by orders of magnitude.
        Assert.InRange(loudest, 0.001f, 4.0f);
    }

    [Fact]
    public void AMonoStreamCarriesSoundInItsOneChannel()
    {
        var decoder = new MpegAudioDecoder(MonoStream(2, excite: true));

        AudioFrame frame = Assert.IsType<AudioFrame>(decoder.NextFrame());
        float loudest = 0.0f;
        foreach (float sample in frame.Samples)
        {
            loudest = Math.Max(loudest, Math.Abs(sample));
        }

        Assert.InRange(loudest, 0.001f, 4.0f);
    }

    [Fact]
    public void AJointStereoStreamSharesTheSubbandsAboveItsBound()
    {
        var decoder = new MpegAudioDecoder(MpegTestStreams.Layer2Stream(
            2, MpegTestStreams.ModeJointStereo, MpegTestStreams.BitRateIndex128,
            MpegTestStreams.SampleRateIndex44100, 1, excite: true));

        Assert.Equal(2, decoder.Channels);
        Assert.NotNull(decoder.NextFrame());
        Assert.NotNull(decoder.NextFrame());
        Assert.Equal(0, decoder.ResyncCount);
    }

    [Fact]
    public void FrameTimesAdvanceByTheFramesOwnDuration()
    {
        var decoder = new MpegAudioDecoder(MonoStream(3, excite: false), 1.5);

        var times = new List<double>();
        AudioFrame? frame;
        while ((frame = decoder.NextFrame()) != null)
        {
            times.Add(frame.Time);
            Assert.Equal(SamplesPerFrame / (double)SampleRate, frame.Duration, 9);
        }

        Assert.Equal(3, times.Count);
        Assert.Equal(1.5, times[0], 9);
        Assert.Equal(1.5 + (SamplesPerFrame / (double)SampleRate), times[1], 9);
        Assert.Equal(1.5 + (2.0 * SamplesPerFrame / SampleRate), times[2], 9);
    }

    /// <summary>The filter bank carries a thousand samples of history, so a replay that did not
    /// clear it would differ from the first pass over its opening frames.</summary>
    [Fact]
    public void RewindDecodesTheSameSamplesAgain()
    {
        var decoder = new MpegAudioDecoder(MonoStream(3, excite: true));

        float[] first = FirstFrameSamples(decoder);
        while (decoder.NextFrame() != null)
        {
        }

        decoder.Rewind();
        Assert.Equal(first, FirstFrameSamples(decoder));
    }

    /// <summary>ISO 11172-3's scale factors are a ladder each step of which is the one above it
    /// divided by the cube root of two, so three steps down is exactly half and index 63 is
    /// silence. The tables carry only the three bases that ladder is built from, and the shift
    /// that reaches the other sixty is arithmetic no table can pin, so it is measured here at
    /// the decoder's output where a wrong shift or a wrong base shows as the wrong
    /// amplitude.</summary>
    [Fact]
    public void TheScaleFactorLadderHalvesEveryThreeSteps()
    {
        float loudest0 = Loudest(ScaledStream(0));
        float loudest1 = Loudest(ScaledStream(1));
        float loudest3 = Loudest(ScaledStream(3));
        float loudest6 = Loudest(ScaledStream(6));

        Assert.InRange(loudest0, 0.001f, 4.0f);
        Assert.Equal(0.5, loudest3 / loudest0, 3);
        Assert.Equal(0.25, loudest6 / loudest0, 3);
        Assert.Equal(Math.Pow(2.0, -1.0 / 3.0), loudest1 / loudest0, 3);
        Assert.Equal(0.0f, Loudest(ScaledStream(63)));
    }

    /// <summary>The selection information says which of a frame's three parts share a scale
    /// factor and how many are transmitted: three for 0, the first two for 1, one for 2, and the
    /// last two for 3. Each is checked by decoding it beside the spelled-out triple it stands
    /// for, so the samples have to agree exactly rather than merely be plausible; the two indices
    /// differ by nine steps, an eightfold change of level, so a part taking the wrong one of them
    /// cannot pass.</summary>
    [Theory]
    [InlineData(1, new[] { 4, 13 }, new[] { 4, 4, 13 })]
    [InlineData(2, new[] { 4 }, new[] { 4, 4, 4 })]
    [InlineData(3, new[] { 4, 13 }, new[] { 4, 13, 13 })]
    public void TheSelectionInformationSpreadsTheFactorsTheStandardsWay(
        int select, int[] transmitted, int[] spelledOut)
    {
        float[] shared = AllSamples(ScaledStream(select, transmitted));
        float[] explicitly = AllSamples(ScaledStream(0, spelledOut));

        Assert.Equal(explicitly, shared);
    }

    /// <summary>The control for the equivalences above, which would hold just as well between
    /// two frames that were scaled once each. A frame is three parts of 384 samples; changing
    /// only the middle factor leaves the first part sample for sample identical, because nothing
    /// before it can differ, and changes everything after it. Nothing here asserts by how much:
    /// the filter bank carries sixteen blocks of history, which is longer than a part, so no
    /// sample inside the middle part is at the settled level of its own factor.</summary>
    [Fact]
    public void TheThreePartsOfAFrameTakeTheirOwnScaleFactor()
    {
        float[] flat = AllSamples(ScaledStream(0, new[] { 4, 4, 4 }));
        float[] dipped = AllSamples(ScaledStream(0, new[] { 4, 13, 4 }));

        Assert.Equal(SamplesPerFrame, flat.Length);
        Assert.Equal(flat.Take(PartSamples), dipped.Take(PartSamples));
        Assert.NotEqual(flat.Skip(PartSamples), dipped.Skip(PartSamples));
    }

    /// <summary>Bit rate index zero is the free format and index 15 is forbidden. Neither names
    /// an entry of the bit rate table, so a decoder that indexes with them reads outside it.</summary>
    [Theory]
    [InlineData(0x00)]
    [InlineData(0xf0)]
    public void ABitRateIndexOutsideTheTableIsRejected(int patched)
    {
        byte[] stream = MonoStream(1, excite: false);
        stream[2] = (byte)patched;

        Assert.Throws<InvalidDataException>(() => new MpegAudioDecoder(stream));
        Assert.Null(MpegAudioDecoder.TryOpen(stream));
    }

    [Fact]
    public void AStreamThatIsNotLayerTwoIsRejected()
    {
        byte[] stream = MonoStream(1, excite: false);
        stream[1] = 0xfb;

        Assert.Throws<InvalidDataException>(() => new MpegAudioDecoder(stream));
    }

    [Fact]
    public void BytesThatCarryNoFrameAtAllAreRejected() =>
        Assert.Throws<InvalidDataException>(() => new MpegAudioDecoder(new byte[512]));

    /// <summary>A frame that starts where the last one's declared size ends costs no resync,
    /// and a stream missing a byte in the middle costs exactly one.</summary>
    [Fact]
    public void OnlyALossOfBytesCostsAResync()
    {
        byte[] whole = MonoStream(4, excite: false);
        var decoder = new MpegAudioDecoder(whole);
        while (decoder.NextFrame() != null)
        {
        }

        Assert.Equal(4, decoder.FramesDecoded);
        Assert.Equal(0, decoder.ResyncCount);

        int frameSize = MpegTestStreams.Layer2FrameSize(
            MpegTestStreams.BitRateIndex64, MpegTestStreams.SampleRateIndex44100);
        var damaged = new byte[whole.Length - 1];
        Array.Copy(whole, damaged, frameSize);
        Array.Copy(whole, frameSize + 1, damaged, frameSize, damaged.Length - frameSize);

        var second = new MpegAudioDecoder(damaged);
        while (second.NextFrame() != null)
        {
        }

        Assert.Equal(1, second.ResyncCount);
    }

    /// <summary>The demultiplexer hands the sound track over as packets, and joining their
    /// payloads has to give back the elementary stream that went in.</summary>
    [Fact]
    public void TheAudioPacketsJoinBackIntoTheElementaryStream()
    {
        byte[] elementary = MonoStream(6, excite: true);
        byte[] file = MpegTestStreams.SystemStreamWithSplitAudio(
            MpegTestStreams.IntraPictures(32, 16, 5, 2), elementary, 5, 90000);

        MpegSystemStream stream = MpegSystemStream.Demux(file);

        Assert.Equal(5, stream.AudioPackets.Count);
        Assert.Equal(elementary, stream.AudioStream);
        Assert.Equal(1.0, stream.AudioStartTime, 9);

        var movie = MpegMovie.FromBytes(file);
        Assert.True(movie.HasAudio);
        Assert.Equal(1, movie.AudioChannels);
        Assert.Equal(SampleRate, movie.AudioSampleRate);
        Assert.Equal(1.0, Assert.IsType<AudioFrame>(movie.NextAudioFrame()).Time, 9);
    }

    [Fact]
    public void AMovieWithNoSoundTrackReportsNone()
    {
        var movie = MpegMovie.FromBytes(
            MpegTestStreams.SystemStream(MpegTestStreams.IntraPictures(32, 16, 5, 2), 0));

        Assert.False(movie.HasAudio);
        Assert.Equal(0, movie.AudioChannels);
        Assert.Null(movie.NextAudioFrame());
    }

    /// <summary>Every cinema's sound track decodes end to end at the sample rate, channel count
    /// and bit rate its own frame headers declare. The frame count is what a whole walk
    /// produces, since no header carries one, and the peak separates a track that carries sound
    /// from the one that is authored silent.</summary>
    [MovieDataTheory]
    [InlineData("msopen1.mpg", 2, 64, 518, false)]
    [InlineData("zipper.mpg", 2, 128, 776, false)]
    [InlineData("crimflag.mpg", 1, 64, 307, true)]
    [InlineData("chap0.mpg", 2, 128, 5549, false)]
    [InlineData("chap1.mpg", 2, 128, 5010, false)]
    [InlineData("chap2.mpg", 2, 128, 4719, false)]
    [InlineData("chap3.mpg", 2, 128, 3553, false)]
    [InlineData("chap4.mpg", 2, 128, 5263, false)]
    [InlineData("chap5.mpg", 2, 128, 3668, false)]
    [InlineData("final.mpg", 2, 128, 4250, false)]
    public void EveryCinemaDecodesItsDeclaredSoundTrack(
        string fileName, int channels, int bitRate, int frameCount, bool silent)
    {
        MpegSystemStream stream = MpegSystemStream.Demux(
            File.ReadAllBytes(Path.Combine(TestData.MovieRoot!, fileName)));
        var decoder = new MpegAudioDecoder(stream.AudioStream, stream.AudioStartTime);

        Assert.Equal(SampleRate, decoder.SampleRate);
        Assert.Equal(channels, decoder.Channels);
        Assert.Equal(bitRate, decoder.BitRate);

        int frames = 0;
        float loudest = 0.0f;
        double previous = double.NegativeInfinity;
        AudioFrame? frame;
        while ((frame = decoder.NextFrame()) != null)
        {
            Assert.Equal(channels * SamplesPerFrame, frame.Samples.Length);
            Assert.True(frame.Time > previous, $"frame {frames} at {frame.Time} follows {previous}");
            previous = frame.Time;
            foreach (float sample in frame.Samples)
            {
                loudest = Math.Max(loudest, Math.Abs(sample));
            }

            frames++;
        }

        Assert.Equal(frameCount, frames);
        Assert.Equal(0, decoder.ResyncCount);
        Assert.Equal(frameCount, decoder.FramesDecoded);
        if (silent)
        {
            Assert.Equal(0.0f, loudest);
        }
        else
        {
            Assert.InRange(loudest, 0.5f, 1.5f);
        }
    }

    private static byte[] MonoStream(int frameCount, bool excite) =>
        MpegTestStreams.Layer2Stream(
            frameCount, MpegTestStreams.ModeMono, MpegTestStreams.BitRateIndex64,
            MpegTestStreams.SampleRateIndex44100, 0, excite);

    // One mono frame whose single allocated subband carries the given scale factors under the
    // given selection information. The default selection is the one that shares a single factor
    // across the whole frame, which is what a plain amplitude check wants.
    private static byte[] ScaledStream(int scaleFactorIndex) =>
        ScaledStream(2, new[] { scaleFactorIndex });

    private static byte[] ScaledStream(int select, int[] scaleFactorIndices) =>
        MpegTestStreams.Layer2Stream(
            1, MpegTestStreams.ModeMono, MpegTestStreams.BitRateIndex64,
            MpegTestStreams.SampleRateIndex44100, 0, excite: true, select, scaleFactorIndices);

    private static float[] AllSamples(byte[] stream) =>
        (float[])Assert.IsType<AudioFrame>(new MpegAudioDecoder(stream).NextFrame()).Samples.Clone();

    private static float Loudest(byte[] stream) => Loudest(AllSamples(stream), 0, SamplesPerFrame);

    private static float Loudest(float[] samples, int at, int count)
    {
        float loudest = 0.0f;
        for (int sample = at; sample < at + count; sample++)
        {
            loudest = Math.Max(loudest, Math.Abs(samples[sample]));
        }

        return loudest;
    }

    private static float[] FirstFrameSamples(MpegAudioDecoder decoder) =>
        (float[])Assert.IsType<AudioFrame>(decoder.NextFrame()).Samples.Clone();
}
