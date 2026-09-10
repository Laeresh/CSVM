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

    // How far into each cinema the always-on check decodes: one group of pictures.
    private const int PrefixFrames = 15;

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

    /// <summary>The eight frame rates ISO 11172-2 assigns the sequence header's rate code. The
    /// three broadcast rates are exact ratios, not the decimals they are named by, and a decoder
    /// that rounded 30000/1001 to 29.97 would drift by a frame every eleven minutes.</summary>
    [Theory]
    [InlineData(1, 24000.0 / 1001.0)]
    [InlineData(2, 24.0)]
    [InlineData(3, 25.0)]
    [InlineData(4, 30000.0 / 1001.0)]
    [InlineData(5, 30.0)]
    [InlineData(6, 50.0)]
    [InlineData(7, 60000.0 / 1001.0)]
    [InlineData(8, 60.0)]
    public void TheFrameRateCodeNamesTheStandardsEightRates(int code, double expected) =>
        Assert.Equal(
            expected,
            MpegMovie.FromBytes(Header(MpegTestStreams.SquarePixelCode, code)).FrameRate,
            9);

    /// <summary>Code zero is forbidden and 9 to 15 are reserved; none of them names a rate, so
    /// the sequence cannot be played and opening it has to fail rather than run at zero.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(15)]
    public void AFrameRateCodeOutsideTheTableIsRejected(int code) =>
        Assert.Throws<InvalidDataException>(
            () => MpegMovie.FromBytes(Header(MpegTestStreams.SquarePixelCode, code)));

    /// <summary>The fourteen sample aspect ratios ISO 11172-2 assigns the sequence header's
    /// aspect code. Every cinema carries code 1, the square pixel, so the other thirteen are
    /// reachable only from a fixture.</summary>
    [Theory]
    [InlineData(1, 1.0000)]
    [InlineData(2, 0.6735)]
    [InlineData(3, 0.7031)]
    [InlineData(4, 0.7615)]
    [InlineData(5, 0.8055)]
    [InlineData(6, 0.8437)]
    [InlineData(7, 0.8935)]
    [InlineData(8, 0.9157)]
    [InlineData(9, 0.9815)]
    [InlineData(10, 1.0255)]
    [InlineData(11, 1.0695)]
    [InlineData(12, 1.0950)]
    [InlineData(13, 1.1575)]
    [InlineData(14, 1.2051)]
    public void TheAspectCodeNamesTheStandardsFourteenRatios(int code, double expected) =>
        Assert.Equal(expected, MpegMovie.FromBytes(Header(code, 5)).PixelAspectRatio, 6);

    /// <summary>Every cinema demultiplexes and opens to the shape its own headers declare, and
    /// decodes the opening frames of its first group of pictures. This is the check an ordinary
    /// run makes: it reads every one of the ten files whole, so a file whose profile is not what
    /// this project believes cannot pass, and it costs a prefix of the decode rather than all of
    /// it. Frame counts and the rest of each file are
    /// <see cref="EveryCinemaDecodesEveryFrameItCarries"/>.</summary>
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
    public void EveryCinemaOpensToItsDeclaredShape(string fileName, bool broadcastRate)
    {
        var movie = MpegMovie.FromFile(Path.Combine(TestData.MovieRoot!, fileName));

        Assert.Equal(320, movie.Width);
        Assert.Equal(240, movie.Height);
        Assert.Equal(broadcastRate ? BroadcastRate : 30.0, movie.FrameRate, 6);
        Assert.Equal(1.0, movie.PixelAspectRatio, 6);
        Assert.NotEmpty(movie.AudioPackets);

        // A group of pictures is fifteen frames in these files, so a prefix this long carries
        // intra, predicted and bidirectional pictures and the reordering between them.
        int frames = Walk(movie, PrefixFrames);

        Assert.Equal(PrefixFrames, frames);
    }

    /// <summary>Every cinema decodes to its last frame, and yields exactly the pictures a whole
    /// walk of the file produces. No header carries that count, so the number here is the one
    /// this decoder measured and is a tripwire against a picture silently dropped or repeated.
    /// Opt-in, because ten whole walks cost about twice the unit stage's own wall-time
    /// budget: see <see cref="TestData.FullMovieWalk"/>.</summary>
    [FullMovieWalkTheory]
    [InlineData("msopen1.mpg", 404)]
    [InlineData("zipper.mpg", 608)]
    [InlineData("crimflag.mpg", 240)]
    [InlineData("chap0.mpg", 4349)]
    [InlineData("chap1.mpg", 3926)]
    [InlineData("chap2.mpg", 3698)]
    [InlineData("chap3.mpg", 2784)]
    [InlineData("chap4.mpg", 4124)]
    [InlineData("chap5.mpg", 2874)]
    [InlineData("final.mpg", 3330)]
    public void EveryCinemaDecodesEveryFrameItCarries(string fileName, int frameCount)
    {
        var movie = MpegMovie.FromFile(Path.Combine(TestData.MovieRoot!, fileName));

        Assert.Equal(frameCount, Walk(movie, int.MaxValue));
        Assert.Equal(frameCount, movie.FramesDecoded);
    }

    // A one-picture stream carrying nothing but the sequence header codes under test.
    private static byte[] Header(int aspectCode, int frameRateCode) =>
        MpegTestStreams.SystemStream(MpegTestStreams.SequenceWithCodes(aspectCode, frameRateCode), 0);

    // Decodes up to that many frames, checking each one's size and that presentation times
    // increase strictly, and returns how many came out.
    private static int Walk(MpegMovie movie, int limit)
    {
        int frames = 0;
        double previous = double.NegativeInfinity;
        VideoFrame? frame;
        while (frames < limit && (frame = movie.NextFrame()) != null)
        {
            Assert.Equal(320 * 240, frame.Luma.Length);
            Assert.True(frame.Time > previous, $"frame {frames} at {frame.Time} follows {previous}");
            previous = frame.Time;
            frames++;
        }

        return frames;
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
