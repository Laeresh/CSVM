using System;
using System.IO;
using CSVM.Video;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The playback clock: which picture is due when, and what happens at the end of a pass. The
/// first half runs on streams built from the standard's syntax and gates a machine with no game
/// install; the second half plays the two cinemas whose rates differ and is skipped where they
/// are unreachable. Every check pins the moment a picture becomes due from both sides, because a
/// count taken over a short window cannot tell 30000/1001 from 30 (METHOD-1).
/// </summary>
public class MoviePlaybackTests
{
    // The step a check drives the clock in. It is a fraction of the gap the two rates the ten
    // cinemas carry open between them, so the moment a picture becomes due separates the rates
    // rather than being lost in the stepping.
    private const double Step = 0.0001;

    // Which picture the synthetic checks pin. Far enough in that the three rates put it a
    // hundredth of a second apart, which is a hundred steps.
    private const int SyntheticFrame = 300;

    // Which picture the whole-file checks pin. One group of pictures plus one, so a cinema is
    // read half a second in rather than to the end.
    private const int CinemaFrame = 16;

    /// <summary>The moment picture 300 becomes due is the stream's own rate and nothing else: at
    /// 25 fps 11.96 s, at 30000/1001 9.976 s and at 30 fps 9.966 s, each pinned from both sides
    /// to a tenth of a millisecond. A clock that took one rate for all three passes that row and
    /// fails the other two, the two broadcast-rate rows being ten milliseconds apart.</summary>
    [Theory]
    [InlineData(3, 11.96)]
    [InlineData(4, 299.0 * 1001.0 / 30000.0)]
    [InlineData(5, 299.0 / 30.0)]
    public void ThePictureBecomesDueAtTheStreamsOwnRate(int frameRateCode, double dueSeconds)
    {
        var playback = new MoviePlayback(Synthetic(frameRateCode, SyntheticFrame + 4), 1);

        Drive(playback, dueSeconds - Step);
        Assert.Equal(SyntheticFrame - 1, playback.FramesShown);

        Drive(playback, 2.0 * Step);
        Assert.Equal(SyntheticFrame, playback.FramesShown);
    }

    /// <summary>A play count of zero is endless: the movie rewinds and keeps going, and its
    /// pictures still decode to the samples they code, which a rewind that left the decoder half
    /// reset would not.</summary>
    [Fact]
    public void AZeroPlayCountRewindsForever()
    {
        var playback = new MoviePlayback(Predicted(5, 3), 0);

        Drive(playback, 0.95);

        Assert.Equal(9, playback.PassesPlayed);
        Assert.Equal(29, playback.FramesShown);
        Assert.False(playback.Finished);
        AssertPicture(playback);
    }

    /// <summary>A play count runs that many passes and then stops, one frame interval after the
    /// last picture rather than on the instant it is handed out.</summary>
    [Fact]
    public void APlayCountStopsWhenItIsUsedUp()
    {
        var playback = new MoviePlayback(Predicted(5, 3), 2);

        Drive(playback, (6.0 / 30.0) - Step);
        Assert.Equal(6, playback.FramesShown);
        Assert.False(playback.Finished);

        Drive(playback, 2.0 * Step);
        Assert.True(playback.Finished);
        Assert.Equal(6, playback.FramesShown);
    }

    /// <summary>A movie carrying no sound track plays anyway. The looping background is silent,
    /// so nothing in the clock may reach for an audio stream to drive itself from.</summary>
    [Fact]
    public void AMovieWithNoSoundTrackStillPlays()
    {
        var movie = MpegMovie.FromBytes(
            MpegTestStreams.SystemStream(MpegTestStreams.IntraPictures(32, 16, 5, 4), 90000));
        var playback = new MoviePlayback(movie, 1);

        Drive(playback, 0.11);

        Assert.Empty(movie.AudioPackets);
        Assert.Equal(4, playback.FramesShown);
        AssertPicture(playback);
    }

    /// <summary>A step longer than the cap advances the clock by the cap. A window that was not
    /// drawing for a minute must not come back by decoding a minute of picture nobody sees.</summary>
    [Fact]
    public void AStepLongerThanTheCapIsTakenAsTheCap()
    {
        var playback = new MoviePlayback(Synthetic(5, 40), 0);

        playback.Advance(60.0);

        Assert.Equal(0.25, playback.Clock, 6);
        Assert.Equal(8, playback.FramesShown);
    }

    /// <summary>The two cinemas whose rates differ, pinned the same way: <c>msopen1.mpg</c> shows
    /// its sixteenth picture at 0.5005 s and not at 0.5 s, where <c>crimflag.mpg</c> shows its own
    /// at 0.5 s. A clock running either file at the other's rate fails one of these rows by half a
    /// millisecond, which is five steps.</summary>
    [MovieDataTheory]
    [InlineData("msopen1.mpg", 15.0 * 1001.0 / 30000.0)]
    [InlineData("crimflag.mpg", 0.5)]
    public void TheCinemaClockRunsAtTheFilesOwnRate(string fileName, double dueSeconds)
    {
        var playback = new MoviePlayback(
            MpegMovie.FromFile(Path.Combine(TestData.MovieRoot!, fileName)), 0);

        Drive(playback, dueSeconds - Step);
        Assert.Equal(CinemaFrame - 1, playback.FramesShown);

        Drive(playback, 2.0 * Step);
        Assert.Equal(CinemaFrame, playback.FramesShown);
        Assert.Equal(320 * 240 * 4, playback.Pixels.Length);
    }

    // A stream of identical intra pictures at that rate code, carrying an audio packet nothing
    // here opens: the clock is the video timestamps whether a sound track is present or not.
    private static MpegMovie Synthetic(int frameRateCode, int pictures) =>
        MpegMovie.FromBytes(MpegTestStreams.SystemStream(
            MpegTestStreams.IntraPictures(32, 16, frameRateCode, pictures), 90000, new byte[] { 0 }));

    // A stream whose pictures after the first predict it, so a pass that started from a decoder
    // holding no reference picture would decode to something other than the samples it codes.
    private static MpegMovie Predicted(int frameRateCode, int pictures) =>
        MpegMovie.FromBytes(MpegTestStreams.SystemStream(
            MpegTestStreams.IntraThenPredicted(32, 16, frameRateCode, pictures), 0));

    // Drives the clock in fine steps, the way a caller does, so nothing is credited to a picture
    // handed out before its own moment.
    private static void Drive(MoviePlayback playback, double seconds)
    {
        for (double at = 0.0; at < seconds;)
        {
            double step = Math.Min(Step, seconds - at);
            playback.Advance(step);
            at += step;
        }
    }

    private static void AssertPicture(MoviePlayback playback)
    {
        int luma = ((MpegTestStreams.ExpectedLuma - 16) * 76309) >> 16;
        for (int at = 0; at < playback.Pixels.Length; at += 4)
        {
            Assert.Equal(luma, playback.Pixels[at]);
            Assert.Equal(255, playback.Pixels[at + 3]);
        }
    }
}
