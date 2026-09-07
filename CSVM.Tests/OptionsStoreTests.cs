using System.Globalization;
using System.IO;
using System.Text;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The persistence contract: a missing file reads as empty, a valid file round-trips
/// every option, an unknown version or malformed JSON invalidates the whole file, an unknown value
/// in a field is dropped without invalidating the file, a file written before a field existed
/// still loads its other fields, and a save is atomic against an interrupted write. The two
/// display settings that are not vocabulary words hold the same contract through a shape check
/// instead of a word list, so a malformed size or screen index is dropped like an unknown word.</summary>
public class OptionsStoreTests
{
    [Fact]
    public void Load_MissingFile_ReadsAsEmpty()
    {
        var store = new OptionsStore(TestData.TempDir());
        var def = store.Load();

        Assert.Null(def.MenuPresentation);
        Assert.Null(def.GraphicsMode);
    }

    [Fact]
    public void RoundTrip_PreservesTheRequestedPresentation()
    {
        var store = new OptionsStore(TestData.TempDir());
        store.Save(new OptionsDef { MenuPresentation = "original" });

        Assert.Equal("original", store.Load().MenuPresentation);
    }

    [Fact]
    public void RoundTrip_PreservesBothOptionsIndependently()
    {
        var store = new OptionsStore(TestData.TempDir());
        store.Save(new OptionsDef { MenuPresentation = "built-in", GraphicsMode = "enhanced" });
        var def = store.Load();

        Assert.Equal("built-in", def.MenuPresentation);
        Assert.Equal("enhanced", def.GraphicsMode);

        // The graphics word alone, with the presentation never set: the two fields do not depend
        // on each other, so an options file can carry either one.
        store.Save(new OptionsDef { GraphicsMode = "original" });
        def = store.Load();

        Assert.Null(def.MenuPresentation);
        Assert.Equal("original", def.GraphicsMode);
    }

    [Fact]
    public void RoundTrip_PreservesTheDifficultyWord()
    {
        var store = new OptionsStore(TestData.TempDir());
        store.Save(new OptionsDef { Difficulty = "hard" });
        var def = store.Load();

        Assert.Equal("hard", def.Difficulty);
        Assert.Null(def.MenuPresentation);
        Assert.Null(def.GraphicsMode);

        store.Save(new OptionsDef { Difficulty = "hardest", MenuPresentation = "original" });
        def = store.Load();
        Assert.Equal("hardest", def.Difficulty);
        Assert.Equal("original", def.MenuPresentation);
    }

    /// <summary>The file carries the three campaign words alone: Instant Action's names and the
    /// bare digits the flag also takes are a command line's convenience, and a word outside the set
    /// is dropped like a missing one rather than invalidating the file.</summary>
    [Fact]
    public void Load_UnknownDifficultyWord_DropsOnlyThatField()
    {
        var dir = TestData.TempDir();
        foreach (string word in new[] { "brutal", "veteran", "1", "Hard" })
        {
            File.WriteAllText(Path.Combine(dir, "options.json"),
                $"{{\"version\": 1, \"menuPresentation\": \"original\", \"difficulty\": \"{word}\"}}",
                new UTF8Encoding(false));
            var def = new OptionsStore(dir).Load();

            Assert.Equal("original", def.MenuPresentation);
            Assert.Null(def.Difficulty);
        }
    }

    [Fact]
    public void Load_UnknownGraphicsValue_DropsOnlyThatField()
    {
        var dir = TestData.TempDir();
        File.WriteAllText(Path.Combine(dir, "options.json"),
            "{\"version\": 1, \"menuPresentation\": \"original\", \"graphicsMode\": \"raytraced\"}",
            new UTF8Encoding(false));
        var def = new OptionsStore(dir).Load();

        Assert.Equal("original", def.MenuPresentation);
        Assert.Null(def.GraphicsMode);
    }

    /// <summary>A file written before the graphics field existed: the version does not move for a
    /// field added beside the others, so the older file still loads and reads the missing field as
    /// never set rather than being thrown away whole.</summary>
    [Fact]
    public void Load_FileWithoutTheGraphicsField_KeepsTheFieldsItHas()
    {
        var dir = TestData.TempDir();
        File.WriteAllText(Path.Combine(dir, "options.json"),
            "{\"version\": 1, \"menuPresentation\": \"original\"}", new UTF8Encoding(false));
        var def = new OptionsStore(dir).Load();

        Assert.Equal("original", def.MenuPresentation);
        Assert.Null(def.GraphicsMode);
        Assert.Null(def.Difficulty);
    }

    [Fact]
    public void Load_UnknownVersion_ReadsAsEmpty()
    {
        var dir = TestData.TempDir();
        File.WriteAllText(Path.Combine(dir, "options.json"),
            "{\"version\": 99, \"menuPresentation\": \"original\"}", new UTF8Encoding(false));

        Assert.Null(new OptionsStore(dir).Load().MenuPresentation);
    }

    [Fact]
    public void Load_UnknownPresentationValue_DropsOnlyThatField()
    {
        var dir = TestData.TempDir();
        File.WriteAllText(Path.Combine(dir, "options.json"),
            "{\"version\": 1, \"menuPresentation\": \"nonsense\"}", new UTF8Encoding(false));

        Assert.Null(new OptionsStore(dir).Load().MenuPresentation);
    }

    [Fact]
    public void Load_MalformedJson_ReadsAsEmpty()
    {
        var dir = TestData.TempDir();
        File.WriteAllText(Path.Combine(dir, "options.json"), "{ not json", new UTF8Encoding(false));

        Assert.Null(new OptionsStore(dir).Load().MenuPresentation);
    }

    /// <summary>The four display settings round-trip beside the three words, so a page that hands
    /// back a setting it does not show cannot lose it: the def that went in comes back field for
    /// field, canonical resolution and all.</summary>
    [Fact]
    public void RoundTrip_PreservesTheDisplaySettings()
    {
        var store = new OptionsStore(TestData.TempDir());
        store.Save(new OptionsDef
        {
            MenuPresentation = "original",
            GraphicsMode = "enhanced",
            Difficulty = "hard",
            MonitorIndex = "1",
            Resolution = OptionsStore.FormatResolution(2560, 1440),
            DisplayMode = DisplayWords.Borderless,
            VSync = "144",
        });
        var def = store.Load();

        Assert.Equal("1", def.MonitorIndex);
        Assert.Equal("2560x1440", def.Resolution);
        Assert.Equal(DisplayWords.Borderless, def.DisplayMode);
        Assert.Equal("144", def.VSync);
        Assert.Equal("original", def.MenuPresentation);
        Assert.Equal("enhanced", def.GraphicsMode);
        Assert.Equal("hard", def.Difficulty);
    }

    /// <summary>Every word either display vocabulary offers survives the file, so a picker built
    /// from <see cref="DisplayWords"/> cannot offer a value its own store would drop.</summary>
    [Fact]
    public void RoundTrip_PreservesEveryDisplayWord()
    {
        var store = new OptionsStore(TestData.TempDir());
        foreach (string word in DisplayWords.DisplayModes)
        {
            store.Save(new OptionsDef { DisplayMode = word });
            Assert.Equal(word, store.Load().DisplayMode);
        }

        foreach (string word in DisplayWords.VSyncChoices)
        {
            store.Save(new OptionsDef { VSync = word });
            Assert.Equal(word, store.Load().VSync);
        }
    }

    /// <summary>A resolution is validated by shape rather than by membership, and every spelling
    /// that is not the canonical one is dropped like an unknown word. The good value at the end is
    /// what makes the nulls above evidence: "reads as null" passes just as well on a field the
    /// reader never looks at (verification.md METHOD-10).</summary>
    [Fact]
    public void Load_OutOfShapeResolution_DropsOnlyThatField()
    {
        var dir = TestData.TempDir();
        string[] malformed =
        {
            "1920 x 1080", "1920x", "x1080", "1920X1080", "1920x1080x60", "01920x1080",
            "-1x1080", "0x0", "1920*1080", "40000x1080", "1920x1080 ", "",
        };
        foreach (string text in malformed)
        {
            Assert.Null(LoadWith(dir, "resolution", text).Resolution);
            Assert.Equal("original", LoadWith(dir, "resolution", text).MenuPresentation);
        }

        Assert.Equal("1280x720", LoadWith(dir, "resolution", "1280x720").Resolution);
    }

    /// <summary>The monitor is an index rendered decimal, and a padded, signed or out-of-range one
    /// is dropped. A well-shaped index is not a promise that the screen is plugged in: that check
    /// belongs to the caller holding an engine, which is why the store proves the shape alone.</summary>
    [Fact]
    public void Load_OutOfShapeMonitorIndex_DropsOnlyThatField()
    {
        var dir = TestData.TempDir();
        foreach (string text in new[] { "-1", "01", "1.0", "primary", " 1", "64", "99999999", "" })
        {
            Assert.Null(LoadWith(dir, "monitorIndex", text).MonitorIndex);
            Assert.Equal("original", LoadWith(dir, "monitorIndex", text).MenuPresentation);
        }

        Assert.Equal("0", LoadWith(dir, "monitorIndex", "0").MonitorIndex);
        Assert.Equal("2", LoadWith(dir, "monitorIndex", "2").MonitorIndex);
    }

    [Fact]
    public void Load_UnknownDisplayWord_DropsOnlyThatField()
    {
        var dir = TestData.TempDir();
        foreach (string text in new[] { "portrait", "FULLSCREEN", "window", "" })
        {
            Assert.Null(LoadWith(dir, "displayMode", text).DisplayMode);
        }

        foreach (string text in new[] { "true", "30", "on ", "" })
        {
            Assert.Null(LoadWith(dir, "vsync", text).VSync);
        }

        Assert.Equal(DisplayWords.Fullscreen, LoadWith(dir, "displayMode", DisplayWords.Fullscreen).DisplayMode);
        Assert.Equal(DisplayWords.VSyncOn, LoadWith(dir, "vsync", DisplayWords.VSyncOn).VSync);
    }

    /// <summary>A file written before the display settings existed: the version does not move for
    /// fields added beside the others, so the older file still loads its three words and reads the
    /// four it does not carry as never set.</summary>
    [Fact]
    public void Load_FileWithoutTheDisplayFields_KeepsTheFieldsItHas()
    {
        var dir = TestData.TempDir();
        File.WriteAllText(Path.Combine(dir, "options.json"),
            "{\"version\": 1, \"menuPresentation\": \"original\", \"graphicsMode\": \"enhanced\", \"difficulty\": \"hard\"}",
            new UTF8Encoding(false));
        var def = new OptionsStore(dir).Load();

        Assert.Equal("original", def.MenuPresentation);
        Assert.Equal("enhanced", def.GraphicsMode);
        Assert.Equal("hard", def.Difficulty);
        Assert.Null(def.MonitorIndex);
        Assert.Null(def.Resolution);
        Assert.Null(def.DisplayMode);
        Assert.Null(def.VSync);
    }

    /// <summary>The version gate is the one check that rejects a whole file, and it rejects the
    /// display settings with the rest: a well-shaped resolution at an unknown version reads as
    /// never set rather than reaching a window.</summary>
    [Fact]
    public void Load_UnknownVersion_RejectsTheDisplaySettingsToo()
    {
        var dir = TestData.TempDir();
        File.WriteAllText(Path.Combine(dir, "options.json"),
            "{\"version\": 2, \"resolution\": \"1280x720\", \"displayMode\": \"windowed\", \"vsync\": \"on\", \"monitorIndex\": \"0\"}",
            new UTF8Encoding(false));
        var def = new OptionsStore(dir).Load();

        Assert.Null(def.Resolution);
        Assert.Null(def.DisplayMode);
        Assert.Null(def.VSync);
        Assert.Null(def.MonitorIndex);
    }

    /// <summary>The canonical form and the shape check are one thing: what
    /// <see cref="OptionsStore.FormatResolution"/> writes is what
    /// <see cref="OptionsStore.TryParseResolution"/> reads back, so a caller that builds a
    /// resolution cannot build one the store would drop.</summary>
    [Fact]
    public void ShapeParsers_AgreeWithTheCanonicalForm()
    {
        Assert.Equal("1920x1080", OptionsStore.FormatResolution(1920, 1080));
        Assert.True(OptionsStore.TryParseResolution(OptionsStore.FormatResolution(1280, 720), out int width, out int height));
        Assert.Equal(1280, width);
        Assert.Equal(720, height);
        Assert.False(OptionsStore.TryParseResolution(null, out _, out _));
        Assert.False(OptionsStore.TryParseResolution("1280", out _, out _));

        Assert.True(OptionsStore.TryParseMonitorIndex("0", out int index));
        Assert.Equal(0, index);
        Assert.True(OptionsStore.TryParseMonitorIndex(OptionsStore.MaxMonitorIndex.ToString(CultureInfo.InvariantCulture), out _));
        Assert.False(OptionsStore.TryParseMonitorIndex(null, out _));
    }

    /// <summary>A crash between the temp write and the rename leaves only the temp file behind;
    /// <see cref="OptionsStore.Load"/> never reads it, so an interrupted write is invisible rather
    /// than corrupting the reader's answer.</summary>
    [Fact]
    public void Load_LeftoverTempFile_IsIgnored()
    {
        var dir = TestData.TempDir();
        File.WriteAllText(Path.Combine(dir, "options.json.tmp"), "{ half-writ", new UTF8Encoding(false));

        Assert.Null(new OptionsStore(dir).Load().MenuPresentation);
    }

    /// <summary>The same leftover temp file beside a real, already-saved file: the reader still
    /// answers from the real file, proving the interrupted write could not have clobbered it.</summary>
    [Fact]
    public void Load_LeftoverTempFileBesideARealSave_ReadsTheRealSave()
    {
        var dir = TestData.TempDir();
        var store = new OptionsStore(dir);
        store.Save(new OptionsDef { MenuPresentation = "original" });
        File.WriteAllText(Path.Combine(dir, "options.json.tmp"), "{ half-writ", new UTF8Encoding(false));

        Assert.Equal("original", new OptionsStore(dir).Load().MenuPresentation);
    }

    // One field's value written into an otherwise good file and loaded back. The presentation
    // beside it is the control: it says the reader read the file, so a null in the field under
    // test is that field being dropped rather than the whole file being thrown away.
    private static OptionsDef LoadWith(string dir, string name, string value)
    {
        File.WriteAllText(Path.Combine(dir, "options.json"),
            $"{{\"version\": 1, \"menuPresentation\": \"original\", \"{name}\": \"{value}\"}}",
            new UTF8Encoding(false));
        return new OptionsStore(dir).Load();
    }
}
