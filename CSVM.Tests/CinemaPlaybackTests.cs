using System;
using System.IO;
using CSVM.Video;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The cinema's A/V sync: which picture the sound the device has played puts on screen, what the
/// two streams' own start times do to that, and what clamping and the end of the track do. The
/// synthetic half runs on streams built from the standards' syntax and gates a machine with no
/// game install; the whole-file half plays the two cinemas whose parameters differ from the other
/// eight and is skipped where they are unreachable.
/// </summary>
public class CinemaPlaybackTests
{
    // Samples per channel in one layer II frame, which is what a device is fed in.
    private const int SoundFrame = 1152;

    // Sample and bit rate indices of the ten cinemas' own tracks: 44100 Hz, and the stereo bit
    // rate the eight plain files carry.
    private const int Rate44100 = 0;
    private const int BitRate128 = 8;

    /// <summary>The picture follows the sound the device has played and nothing else. Driving the
    /// same stream with a stalled device and a running one puts different pictures on screen at
    /// the same wall time, which is the whole reason the sound is the clock.</summary>
    [Fact]
    public void ThePlayedSoundIsTheClock()
    {
        var cinema = Cinema(pictures: 40, soundFrames: 40);
        var buffer = new float[SoundFrame * 2];
        for (int at = 0; at < 40; at++)
        {
            cinema.ReadSound(buffer, SoundFrame);
        }

        // A whole second of wall time, and a device that has played none of it. The picture is
        // still the first one, which is what a clock read off the frame delta could not report.
        cinema.Advance(1.0, 0);
        Assert.Equal(1, cinema.FramesShown);

        // 0.21 s played and no wall time at all: at 30 fps the seventh picture is due at 0.2 and
        // the eighth at 0.2333. The moment sits off a picture boundary deliberately, since one
        // landing on a boundary is decided by the last bit of two divisions.
        cinema.Advance(0.0, 44100 * 21 / 100);
        Assert.Equal(7, cinema.FramesShown);
    }

    /// <summary>A sound track starting after the picture is paid for in silence, so the samples
    /// the device plays still measure the time since the first picture. A tenth of a second of
    /// offset is 4410 frames of silence, and the first sample after them is inside the layer II
    /// frame that follows.</summary>
    [Fact]
    public void ASoundTrackStartingLaterOpensWithSilence()
    {
        var cinema = Cinema(pictures: 40, soundFrames: 40, videoClock: 9000, audioClock: 18000);
        var buffer = new float[SoundFrame * 2];

        int got = cinema.ReadSound(buffer, SoundFrame);

        Assert.Equal(SoundFrame, got);
        Assert.All(buffer, sample => Assert.Equal(0f, sample));
        Assert.InRange(FirstSound(cinema, buffer), 4410, 4410 + SoundFrame - 1);
    }

    /// <summary>A sound track starting before the picture keeps every sample and lags the picture
    /// clock instead. Nothing is discarded: the sound is the shorter stream in all ten files, and
    /// <c>msopen1.mpg</c> is the one that starts it first.</summary>
    [Fact]
    public void ASoundTrackStartingFirstIsNotDiscarded()
    {
        var cinema = Cinema(pictures: 40, soundFrames: 40, videoClock: 18000, audioClock: 9000);
        var buffer = new float[SoundFrame * 2];

        // The track opens on its own first sample rather than on 4410 of silence, which is what
        // separates a lagged picture clock from a discarded tenth of a second.
        Assert.Equal(SoundFrame, cinema.ReadSound(buffer, SoundFrame));
        Assert.InRange(FirstSound(cinema, buffer), 0, SoundFrame - 1);

        // A tenth of a second of sound played is still picture one, because the picture's clock
        // stands a tenth of a second behind the sound's.
        cinema.Advance(0.0, 4410);
        Assert.Equal(1, cinema.FramesShown);

        // And two frame intervals past that offset it is picture three.
        cinema.Advance(0.0, 4410 + (int)(2.5 * 44100 / 30));
        Assert.Equal(3, cinema.FramesShown);
    }

    /// <summary>Every sample handed out is inside plus or minus one, on a track whose decoder
    /// output is not: six of the ten cinemas peak above 1.0, up to 1.092, which fixed-point output
    /// would wrap or clip audibly. The same stream read through the decoder alone reaches 1.35, so
    /// this case can fail (METHOD-9).</summary>
    [Fact]
    public void EverySampleIsClamped()
    {
        var raw = Movie(pictures: 8, soundFrames: 8);
        float peak = 0f;
        while (raw.NextAudioFrame() is { } sound)
        {
            foreach (float sample in sound.Samples)
            {
                peak = Math.Max(peak, Math.Abs(sample));
            }
        }

        Assert.True(peak > 1f, $"the fixture must overshoot for this to check anything; it peaks at {peak}");

        var cinema = new CinemaPlayback(Movie(pictures: 8, soundFrames: 8));
        var buffer = new float[SoundFrame * 2];

        Assert.Equal(SoundFrame, cinema.ReadSound(buffer, SoundFrame));
        Assert.All(buffer, sample => Assert.InRange(sample, -1f, 1f));
        Assert.Contains(buffer, sample => sample == 1f || sample == -1f);
    }

    /// <summary>A cinema whose sound runs out before the last picture's own interval expires still
    /// finishes. Two of the ten do exactly that, and a clock left on a drained device would leave
    /// them playing for ever.</summary>
    [Fact]
    public void ACinemaWhoseSoundEndsFirstStillFinishes()
    {
        var cinema = Cinema(pictures: 40, soundFrames: 4);
        var buffer = new float[SoundFrame * 2];
        long played = 0;

        for (int at = 0; at < 400 && !cinema.Finished; at++)
        {
            played += cinema.ReadSound(buffer, SoundFrame);
            cinema.Advance(0.05, played);
        }

        Assert.True(cinema.Finished);
        Assert.Equal(40, cinema.FramesShown);
    }

    /// <summary>A movie with no sound track at all plays on the caller's own step, and asks its
    /// caller for no samples.</summary>
    [Fact]
    public void AMovieWithNoSoundTrackPlaysOnTheCallersStep()
    {
        var cinema = new CinemaPlayback(MpegMovie.FromBytes(
            MpegTestStreams.SystemStream(MpegTestStreams.IntraPictures(32, 16, 5, 8), 90000)));

        Assert.False(cinema.HasSound);
        Assert.Equal(0, cinema.ReadSound(new float[16], 8));

        cinema.Advance(0.11, 0);
        Assert.Equal(4, cinema.FramesShown);
    }

    /// <summary>The two cinemas whose parameters differ from the other eight, read from the real
    /// files: <c>msopen1.mpg</c> starts its sound 0.0667 s before its picture and is stereo at
    /// 29.97 fps, where <c>crimflag.mpg</c> starts both together and is mono. A player assuming one
    /// origin, or two channels, gets one of these two rows wrong.</summary>
    [MovieDataTheory]
    [InlineData("msopen1.mpg", 2, -0.0667)]
    [InlineData("crimflag.mpg", 1, 0.0)]
    public void TheCinemasCarryTheirOwnStreamOrigins(string fileName, int channels, double offset)
    {
        var movie = MpegMovie.FromFile(Path.Combine(TestData.MovieRoot!, fileName));

        Assert.Equal(44100, movie.AudioSampleRate);
        Assert.Equal(channels, movie.AudioChannels);
        Assert.Equal(offset, movie.AudioStartTime - movie.VideoStartTime, 3);
    }

    /// <summary>A real cinema played end to end lands its last picture and finishes, with the
    /// picture's clock tracking the sound handed over to a millisecond across the whole file. Drift
    /// accumulates, so the check is the whole file and not its first seconds.</summary>
    [FullMovieWalkTheory]
    [InlineData("crimflag.mpg")]
    [InlineData("zipper.mpg")]
    public void APlayedCinemaTracksItsSoundToTheLastPicture(string fileName)
    {
        var movie = MpegMovie.FromFile(Path.Combine(TestData.MovieRoot!, fileName));
        double shift = Math.Min(movie.AudioStartTime - movie.VideoStartTime, 0.0);
        var cinema = new CinemaPlayback(movie);
        var buffer = new float[SoundFrame * movie.AudioChannels];
        long played = 0;
        double worst = 0.0;

        for (int guard = 0; guard < 20000 && !cinema.Finished; guard++)
        {
            int got = cinema.ReadSound(buffer, SoundFrame);
            played += got;
            cinema.Advance(SoundFrame / 44100.0, played);
            if (got > 0)
            {
                worst = Math.Max(worst, Math.Abs(cinema.Clock - ((played / 44100.0) + shift)));
            }
        }

        Assert.True(cinema.Finished);
        Assert.InRange(worst, 0.0, 0.001);
        Assert.Equal(fileName == "crimflag.mpg" ? 240 : 608, cinema.FramesShown);
    }

    // A system stream of identical intra pictures at 30 fps with a layer II track under it, both
    // starting at the given moments on the container's own 90 kHz clock.
    private static MpegMovie Movie(
        int pictures, int soundFrames, long videoClock = 9000, long audioClock = 9000)
    {
        byte[] video = MpegTestStreams.IntraPictures(32, 16, 5, pictures);
        byte[] sound = MpegTestStreams.Layer2Stream(
            soundFrames, MpegTestStreams.ModeStereo, BitRate128, Rate44100, 0, excite: true);
        return MpegMovie.FromBytes(MpegTestStreams.SystemStream(video, videoClock, sound, audioClock));
    }

    private static CinemaPlayback Cinema(
        int pictures, int soundFrames, long videoClock = 9000, long audioClock = 9000) =>
        new(Movie(pictures, soundFrames, videoClock, audioClock));

    // Where the first sample that is not silence sits, counted per channel from the start of the
    // track. The caller has already read the first block into the buffer, and the walk carries on
    // over the second that follows it.
    private static int FirstSound(CinemaPlayback cinema, float[] buffer)
    {
        int reached = 0;
        int got = buffer.Length / cinema.Channels;
        while (got > 0 && reached < 40 * SoundFrame)
        {
            for (int at = 0; at < got; at++)
            {
                if (buffer[at * cinema.Channels] != 0f)
                {
                    return reached + at;
                }
            }

            reached += got;
            got = cinema.ReadSound(buffer, SoundFrame);
        }

        return -1;
    }
}
