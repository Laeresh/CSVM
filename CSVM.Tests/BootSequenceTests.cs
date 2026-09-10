using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>Pins the boot sequence against the block <c>fmv.zrd</c> authors: the eight actions in
/// the reader's own order with the reader's own durations and film names, the skip set every film
/// takes, and one handoff at the end however the last film stopped.</summary>
[Trait("Tier", "Quick")]
public class BootSequenceTests
{
    /// <summary>The whole block, action for action. ⚠ These strings are the reader's, not a
    /// preference: <c>INTRO</c> is seven actions and <c>CHAP0</c> the eighth
    /// (docs/formats/cinemas.md).</summary>
    [Fact]
    public void TheBlockRunsInTheReadersOwnOrder()
    {
        var stage = new Recorder();
        new BootSequence(stage.Film, stage.Still).Run(stage.Handoff);

        Assert.Equal(
            new[]
            {
                "still Card 5",
                "film MSopen1.mpg",
                "still Wait 1",
                "still Fade 1",
                "film zipper.mpg",
                "still Wait 1",
                "film Chap0.mpg",
                "handoff",
            },
            stage.Steps);
    }

    /// <summary>Every film takes the boot set, any key or a left click, because a player at the
    /// first screen the game shows has been taught no key at all.</summary>
    [Fact]
    public void EveryFilmTakesTheBootSkipSet()
    {
        var stage = new Recorder();
        new BootSequence(stage.Film, stage.Still).Run(stage.Handoff);

        Assert.Equal(3, stage.Skips.Count);
        Assert.All(stage.Skips, skip => Assert.Equal(CinemaScreen.BootKeys, skip));
    }

    /// <summary>Nothing runs the handoff before the opening cinema, which is what a caller relies
    /// on when it puts the launchscreen behind it.</summary>
    [Fact]
    public void TheHandoffWaitsForTheLastFilm()
    {
        var stage = new Recorder(runContinuations: false);
        new BootSequence(stage.Film, stage.Still).Run(stage.Handoff);

        Assert.Equal(new[] { "still Card 5" }, stage.Steps);
        Assert.False(stage.HandedOff);
    }

    /// <summary>A film this install does not carry runs its continuation at once, which is the
    /// shape <c>Launcher.PlayCinema</c> takes for an unreadable file, so the block still reaches
    /// its end and the card is still put up first.</summary>
    [Fact]
    public void AnInstallMissingEveryFilmStillReachesTheHandoff()
    {
        var stage = new Recorder();
        new BootSequence((_, then, _) => then(), stage.Still).Run(stage.Handoff);

        Assert.Equal(
            new[] { "still Card 5", "still Wait 1", "still Fade 1", "still Wait 1", "handoff" },
            stage.Steps);
    }

    /// <summary>The card's shape, which holds with no install present: the splash art under two
    /// lines centred across the whole authored width, each drawn twice for its shadow.</summary>
    [Fact]
    public void TheCardIsTheSplashArtUnderTwoCentredLines()
    {
        var card = BootSequence.Card(TestData.TempDir());

        var art = Assert.Single(card.Backdrop);
        Assert.Equal(BoardArtLibrary.Ui, art.Art.Library);
        Assert.Equal("MM_SPLASHBACKGROUND.JPG", art.Art.Name);
        Assert.Equal(0f, art.X);
        Assert.Equal(0f, art.Y);

        Assert.Equal(4, card.Lines.Count);
        Assert.Equal(new[] { 551f, 550f, 566f, 565f }, card.Lines.Select(line => line.Y));
        Assert.Equal(new[] { 1f, 0f, 1f, 0f }, card.Lines.Select(line => line.X));
        Assert.All(card.Lines, line => Assert.Equal(BoardFit.AuthoredWidth, line.Width));
        Assert.All(card.Lines, line => Assert.Equal(BoardJustify.Center, line.Justify));
        Assert.All(card.Lines, line => Assert.Equal(12f, line.Size));
    }

    /// <summary>The three things the card needs are in the extraction rather than in this project:
    /// the art file, and the two <c>MSG_COPYRIGHT</c> keys the message table resolves.</summary>
    [ExtractedDataFact]
    public void TheCardsArtAndStringsComeOutOfTheExtraction()
    {
        string root = TestData.DataRoot!;
        Assert.True(File.Exists(Path.Combine(
            root, "extracted", "rof", "ASSETS", "GRAPHICS", BootSequence.CardArt)));

        var lines = BootSequence.Card(root);
        string first = lines.Lines[1].Text;
        string second = lines.Lines[3].Text;
        Assert.DoesNotContain("MSG_COPYRIGHT", first, StringComparison.Ordinal);
        Assert.DoesNotContain("MSG_COPYRIGHT", second, StringComparison.Ordinal);
        Assert.Contains("Microsoft Corporation", first, StringComparison.Ordinal);
        Assert.Contains("copyright laws", second, StringComparison.Ordinal);
        Assert.Equal(first, lines.Lines[0].Text);
        Assert.Equal(second, lines.Lines[2].Text);
    }

    private sealed class Recorder
    {
        private readonly bool _run;

        public Recorder(bool runContinuations = true) => _run = runContinuations;

        public List<string> Steps { get; } = new();

        public List<CinemaSkip> Skips { get; } = new();

        public bool HandedOff { get; private set; }

        public void Film(string name, Action then, CinemaSkip skip)
        {
            Steps.Add($"film {name}");
            Skips.Add(skip);
            if (_run)
            {
                then();
            }
        }

        public void Still(BootHold hold, double seconds, Action then)
        {
            Steps.Add($"still {hold} {seconds.ToString("0.###", CultureInfo.InvariantCulture)}");
            if (_run)
            {
                then();
            }
        }

        public void Handoff()
        {
            Steps.Add("handoff");
            HandedOff = true;
        }
    }
}
