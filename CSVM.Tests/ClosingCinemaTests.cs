using System;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>Pins the closing cinema's flow: that a finished campaign plays the film before the
/// scrapbook and an unfinished one reaches the book with no film, that it plays once, that the book
/// opens exactly once however many times the cinema reports stopping, and that the skip set is the
/// closing one's own.
/// </summary>
[Trait("Tier", "Quick")]
public class ClosingCinemaTests
{
    [Fact]
    public void TheFilmIsTheOneTheFinalCinemaLayoutRowNames()
    {
        Assert.Equal("Final.MPG", ClosingCinema.Name);
    }

    [Fact]
    public void AFinishedCampaignPlaysTheFilmBeforeTheBook()
    {
        var cinema = new Recorder();
        var flow = new ClosingCinema(cinema.Play);

        Assert.True(flow.OpenScrapbook(campaignComplete: true, cinema.OpenedBook));

        Assert.Equal("Final.MPG", cinema.Name);
        Assert.Equal(0, cinema.Books);
        cinema.Stop();
        Assert.Equal(1, cinema.Books);
    }

    // FINALCINEMA.SCRIPT's own false branch: the book runs directly and nothing is played.
    [Fact]
    public void AnUnfinishedCampaignGoesStraightToTheBook()
    {
        var cinema = new Recorder();

        Assert.False(new ClosingCinema(cinema.Play).OpenScrapbook(campaignComplete: false, cinema.OpenedBook));

        Assert.Null(cinema.Name);
        Assert.Equal(0, cinema.Plays);
        Assert.Equal(1, cinema.Books);
    }

    [Fact]
    public void TheFilmDoesNotPlayASecondTime()
    {
        var cinema = new Recorder();
        var flow = new ClosingCinema(cinema.Play);

        flow.OpenScrapbook(campaignComplete: true, cinema.OpenedBook);
        cinema.Stop();

        Assert.False(flow.OpenScrapbook(campaignComplete: true, cinema.OpenedBook));
        Assert.Equal(1, cinema.Plays);
        Assert.Equal(2, cinema.Books);
        Assert.True(flow.Played);
    }

    // The EC latch the original's own script holds: a skip landing on the frame the cinema plays
    // out ends the film twice, and the book must still open once.
    [Fact]
    public void TheBookOpensOnceWhenASkipLandsOnThePlayoutFrame()
    {
        var cinema = new Recorder();
        var flow = new ClosingCinema(cinema.Play);

        flow.OpenScrapbook(campaignComplete: true, cinema.OpenedBook);
        cinema.Stop();
        cinema.Stop();

        Assert.Equal(1, cinema.Books);
    }

    // The asymmetry with the chapter cinema is authored, not an oversight, so it is pinned from
    // both sides here as well as stated on CinemaSkip itself.
    [Fact]
    public void TheClosingCinemaTakesNeitherSpaceNorReturnWhereTheChapterOneDoes()
    {
        var cinema = new Recorder();
        new ClosingCinema(cinema.Play).OpenScrapbook(campaignComplete: true, cinema.OpenedBook);

        Assert.Equal(CinemaScreen.ClosingKeys, cinema.Skip);
        Assert.True(cinema.Skip.HasFlag(CinemaSkip.Escape) && cinema.Skip.HasFlag(CinemaSkip.LeftMouse));
        Assert.False(cinema.Skip.HasFlag(CinemaSkip.Space) || cinema.Skip.HasFlag(CinemaSkip.Return));
        Assert.True(CinemaScreen.ChapterKeys.HasFlag(CinemaSkip.Space));
        Assert.True(CinemaScreen.ChapterKeys.HasFlag(CinemaSkip.Return));
    }

    // The gate is the profile's own position and nothing else, so the last mission is what turns
    // the film on: 23 of 24 flown is still an unfinished campaign.
    [Fact]
    public void TheGateComesFromTheProfilesOwnPosition()
    {
        var cinema = new Recorder();
        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.MissionsCompleted = CampaignSequence.MissionCount - 1;

        Assert.False(new ClosingCinema(cinema.Play).OpenScrapbook(profile, cinema.OpenedBook));
        Assert.Equal(0, cinema.Plays);

        var finished = new Recorder();
        profile.MissionsCompleted = CampaignSequence.MissionCount;
        Assert.True(new ClosingCinema(finished.Play).OpenScrapbook(profile, finished.OpenedBook));
        Assert.Equal("Final.MPG", finished.Name);
    }

    // The stand-in for Launcher.PlayCinema: it records what it was asked for and hands the film's
    // end back to the caller, so a test decides when, and how often, the cinema stops.
    private sealed class Recorder
    {
        private Action? _then;

        public string? Name { get; private set; }

        public CinemaSkip Skip { get; private set; }

        public int Plays { get; private set; }

        public int Books { get; private set; }

        public void Play(string name, Action then, CinemaSkip skip)
        {
            Name = name;
            Skip = skip;
            Plays++;
            _then = then;
        }

        public void Stop() => _then?.Invoke();

        public void OpenedBook() => Books++;
    }
}
