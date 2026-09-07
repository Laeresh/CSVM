using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Video;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The demultiplexer and the video decoder end to end. The first half runs on streams built
/// from the standard's syntax, so it gates a machine with no game install; the second half
/// plays the ten cinemas the install ships and is skipped where they are unreachable.
/// </summary>
public class MpegMovieTests
{
    // The rate code each file's sequence header carries, from docs/formats/cinemas.md. One of
    // the ten is 30000/1001 and the rest are 30, which is why nothing here assumes a profile.
    private const double BroadcastRate = 30000.0 / 1001.0;

    [Fact]
    public void ASyntheticIntraStreamDecodesToTheSamplesItCodes()
    {
        var movie = MpegMovie.FromBytes(
            MpegTestStreams.SystemStream(MpegTestStreams.IntraPictures(32, 16, 5, 3), 90000));

        Assert.Equal(32, movie.Width);
        Assert.Equal(16, movie.Height);
        Assert.Equal(30.0, movie.FrameRate, 6);
        Assert.Equal(1.0, movie.PixelAspectRatio, 6);

        var times = new List<double>();
        VideoFrame? frame;
        while ((frame = movie.NextFrame()) != null)
        {
            times.Add(frame.Time);
            Assert.All(frame.Luma, sample => Assert.Equal(MpegTestStreams.ExpectedLuma, sample));
            Assert.All(frame.Cb, sample => Assert.Equal(MpegTestStreams.ExpectedChroma, sample));
            Assert.All(frame.Cr, sample => Assert.Equal(MpegTestStreams.ExpectedChroma, sample));
        }

        Assert.Equal(3, times.Count);
        Assert.Equal(1.0, times[0], 6);
        Assert.Equal(1.0 + (1.0 / 30.0), times[1], 6);
        Assert.Equal(1.0 + (2.0 / 30.0), times[2], 6);
    }

    [Fact]
    public void APredictedPictureCopiesTheOneBeforeIt()
    {
        var movie = MpegMovie.FromBytes(
            MpegTestStreams.SystemStream(MpegTestStreams.IntraThenPredicted(32, 16, 4, 3), 0));

        Assert.Equal(BroadcastRate, movie.FrameRate, 6);
        int frames = 0;
        VideoFrame? frame;
        while ((frame = movie.NextFrame()) != null)
        {
            frames++;
            Assert.All(frame.Luma, sample => Assert.Equal(MpegTestStreams.ExpectedLuma, sample));
        }

        Assert.Equal(3, frames);
    }

    [Fact]
    public void RewindPlaysTheSameFramesAgain()
    {
        var movie = MpegMovie.FromBytes(
            MpegTestStreams.SystemStream(MpegTestStreams.IntraPictures(32, 16, 5, 3), 0));

        Assert.Equal(3, CountFrames(movie));
        movie.Rewind();
        Assert.Equal(3, CountFrames(movie));
    }

    [Fact]
    public void TheRgbaConversionIsTheSamplesTheFrameCarries()
    {
        var movie = MpegMovie.FromBytes(
            MpegTestStreams.SystemStream(MpegTestStreams.IntraPictures(32, 16, 5, 2), 0));

        VideoFrame frame = Assert.IsType<VideoFrame>(movie.NextFrame());
        byte[] pixels = frame.ToRgba();

        Assert.Equal(32 * 16 * 4, pixels.Length);
        int luma = ((MpegTestStreams.ExpectedLuma - 16) * 76309) >> 16;
        for (int at = 0; at < pixels.Length; at += 4)
        {
            Assert.Equal(luma, pixels[at]);
            Assert.Equal(luma, pixels[at + 1]);
            Assert.Equal(luma, pixels[at + 2]);
            Assert.Equal(255, pixels[at + 3]);
        }
    }

    [Fact]
    public void TheAudioStreamIsDemultiplexedWithItsTimestampAndNotDecoded()
    {
        var audio = new byte[] { 0xff, 0xfd, 0x40, 0x04, 0x11, 0x22 };
        var movie = MpegMovie.FromBytes(MpegTestStreams.SystemStream(
            MpegTestStreams.IntraPictures(32, 16, 5, 2), 90000, audio, 135000));

        MpegPacket packet = Assert.Single(movie.AudioPackets);
        Assert.Equal(MpegSystemStream.AudioStreamId, packet.StreamId);
        Assert.Equal(1.5, packet.Time, 6);
        Assert.Equal(audio, packet.Data.ToArray());
    }

    [Fact]
    public void BytesThatAreNotASystemStreamAreRejected() =>
        Assert.Throws<InvalidDataException>(() => MpegMovie.FromBytes(new byte[64]));

    /// <summary>Every cinema decodes end to end, and its dimensions, frame rate and frame count
    /// are what its own headers declare. The count is what a whole walk of the file produces,
    /// which is the only place it is stated: no header carries it.</summary>
    [MovieDataTheory]
    [InlineData("msopen1.mpg", true)]
    [InlineData("zipper.mpg", false)]
    [InlineData("crimflag.mpg", false)]
    [InlineData("chap0.mpg", false)]
    [InlineData("chap1.mpg", false)]
    [InlineData("chap2.mpg", false)]
    [InlineData("chap3.mpg", false)]
    [InlineData("chap4.mpg", false)]
    [InlineData("chap5.mpg", false)]
    [InlineData("final.mpg", false)]
    public void EveryCinemaDecodesToItsDeclaredShape(string fileName, bool broadcastRate)
    {
        var movie = MpegMovie.FromFile(Path.Combine(TestData.MovieRoot!, fileName));

        Assert.Equal(320, movie.Width);
        Assert.Equal(240, movie.Height);
        Assert.Equal(broadcastRate ? BroadcastRate : 30.0, movie.FrameRate, 6);
        Assert.Equal(1.0, movie.PixelAspectRatio, 6);
        Assert.NotEmpty(movie.AudioPackets);

        int frames = 0;
        double previous = double.NegativeInfinity;
        VideoFrame? frame;
        while ((frame = movie.NextFrame()) != null)
        {
            Assert.Equal(320 * 240, frame.Luma.Length);
            Assert.True(frame.Time > previous, $"frame {frames} at {frame.Time} follows {previous}");
            previous = frame.Time;
            frames++;
        }

        Assert.True(frames > 30, $"{fileName} yielded {frames} frames");
        Assert.Equal(frames, movie.FramesDecoded);
    }

    private static int CountFrames(MpegMovie movie)
    {
        int frames = 0;
        while (movie.NextFrame() != null)
        {
            frames++;
        }

        return frames;
    }
}
