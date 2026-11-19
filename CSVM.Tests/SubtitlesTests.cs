using System.IO;
using CSVM.Extraction;
using CSVM.Flight.Hud;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The subtitle table's folder rules and segment timing, and the caption's wording and wrap.
/// </summary>
public class SubtitlesTests
{
    [Fact]
    public void FilesLoadInNameOrderAndKeyLikeASound()
    {
        var root = TestData.TempDir();
        var dir = Directory.CreateDirectory(Path.Combine(root, "Subtitles")).FullName;
        File.WriteAllText(Path.Combine(dir, "a.json"), """
            {
              "VO_c1-HA-m1_Betty_12.wav": { "speaker": "Betty", "text": "first" },
              "VO_id12_HIT.wav": { "text": "bark" },
              "VO_empty.wav": { "speaker": "Nobody" }
            }
            """);
        File.WriteAllText(Path.Combine(dir, "b.json"), """{ "vo_c1-ha-m1_betty_12": { "speaker": "Betty", "text": "fixed" } }""");
        File.WriteAllText(Path.Combine(dir, "c.json"), "not json");

        var table = Subtitles.Open(root, "Subtitles");

        Assert.Equal(2, table.Count);
        Assert.Equal(new Subtitles.Line("Betty", "fixed"), table.At("VO_C1-HA-M1_Betty_12.WAV", 0));
        Assert.Equal(new Subtitles.Line("Betty", "fixed"), table.At("VO_c1-HA-m1_Betty_12.wav", 99));
        Assert.Equal(new Subtitles.Line(null, "bark"), table.At("VO_id12_HIT.wav", 0));
        Assert.Null(table.At("VO_empty.wav", 0));
        Assert.Null(table.At(null, 0));
        Assert.Equal(0, Subtitles.Open(root, "Missing").Count);
    }

    [Fact]
    public void ASegmentShowsUntilTheNextStartsOrItsHoldRunsOut()
    {
        var root = TestData.TempDir();
        var dir = Directory.CreateDirectory(Path.Combine(root, "Subtitles")).FullName;
        File.WriteAllText(Path.Combine(dir, "b.json"), """
            { "brief.wav": { "speaker": "Boss", "segments": [
                { "start": 5.0, "end": 6.0, "text": "two", "speaker": "Jack" },
                { "start": 1.0, "end": 2.0, "text": "one" },
                { "start": 6.5, "end": 8.0, "text": "three" } ] } }
            """);

        var table = Subtitles.Open(root, "Subtitles");

        Assert.Null(table.At("brief.wav", 0.5));
        Assert.Equal(new Subtitles.Line("Boss", "one"), table.At("brief.wav", 1.0));
        Assert.Equal("one", table.At("brief.wav", 2.9)?.Text);
        Assert.Null(table.At("brief.wav", 3.1));
        Assert.Equal(new Subtitles.Line("Jack", "two"), table.At("brief.wav", 6.4));
        Assert.Equal("three", table.At("brief.wav", 6.5)?.Text);
        Assert.Null(table.At("brief.wav", 9.1));
    }

    [Fact]
    public void ACaptionNamesItsSpeakerAndWrapsAtWords()
    {
        Assert.Equal("Betty: hello", SubtitleLine.Compose(new Subtitles.Line("Betty", "hello")));
        Assert.Equal("hello", SubtitleLine.Compose(new Subtitles.Line(null, "hello")));

        // One unit per character, so the width is the length.
        Assert.Equal(new[] { "aaa bb", "cccc", "dddddddddd", "e" },
            SubtitleLine.Wrap("aaa bb  cccc dddddddddd e", 6f, t => t.Length));
        Assert.Empty(SubtitleLine.Wrap("   ", 6f, t => t.Length));
    }
}
