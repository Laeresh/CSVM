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
/// instead of a word list, so a malformed size or screen index is dropped like an unknown word,
/// and the four volume levels hold it through a range check, which is what lets a level of 0 mean
/// a mute while a level of -1 means never set.</summary>
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

    /// <summary>The four levels round-trip beside the seven fields already there, so an AUDIO page
    /// and a page that shows no slider write the same file: the def that went in comes back field
    /// for field.</summary>
    [Fact]
    public void RoundTrip_PreservesTheVolumeLevels()
    {
        var store = new OptionsStore(TestData.TempDir());
        store.Save(new OptionsDef
        {
            MenuPresentation = "original",
            Difficulty = "hard",
            VSync = "144",
            AudioMaster = 100,
            AudioMusic = 40,
            AudioEffects = 55,
            AudioVoice = 70,
        });
        var def = store.Load();

        Assert.Equal(100, def.AudioMaster);
        Assert.Equal(40, def.AudioMusic);
        Assert.Equal(55, def.AudioEffects);
        Assert.Equal(70, def.AudioVoice);
        Assert.Equal("original", def.MenuPresentation);
        Assert.Equal("hard", def.Difficulty);
        Assert.Equal("144", def.VSync);
        Assert.Null(def.GraphicsMode);
        Assert.Null(def.Resolution);
    }

    /// <summary>A level of 0 is the mute a player set, not a level they never set, and the two are
    /// different answers: null takes the shipped default and 0 takes silence. This is why the
    /// fields are <c>int?</c>; a plain <c>int</c> would make a saved mute unreachable.</summary>
    [Fact]
    public void RoundTrip_PreservesALevelOfZeroAsZeroAndNotAsNeverSet()
    {
        var store = new OptionsStore(TestData.TempDir());
        store.Save(new OptionsDef { AudioMaster = 0, AudioMusic = 0, AudioEffects = 0, AudioVoice = 0 });
        var def = store.Load();

        Assert.NotNull(def.AudioMaster);
        Assert.Equal(0, def.AudioMaster!.Value);
        Assert.NotNull(def.AudioMusic);
        Assert.Equal(0, def.AudioMusic!.Value);
        Assert.NotNull(def.AudioEffects);
        Assert.Equal(0, def.AudioEffects!.Value);
        Assert.NotNull(def.AudioVoice);
        Assert.Equal(0, def.AudioVoice!.Value);

        // Both ends of the range, so the check above is the value surviving rather than the field
        // being written as a constant.
        store.Save(new OptionsDef { AudioMaster = AudioMix.MinLevel, AudioMusic = AudioMix.MaxLevel });
        def = store.Load();
        Assert.Equal(AudioMix.MinLevel, def.AudioMaster);
        Assert.Equal(AudioMix.MaxLevel, def.AudioMusic);
        Assert.Null(def.AudioEffects);
    }

    /// <summary>A level is validated by range rather than by membership, and one outside it is
    /// dropped like an unknown word instead of invalidating the file. The good value at the end is
    /// what makes the nulls evidence (verification.md METHOD-10).</summary>
    [Fact]
    public void Load_OutOfRangeLevel_DropsOnlyThatField()
    {
        var dir = TestData.TempDir();
        foreach (string text in new[] { "-1", "101", "1000", "-2147483648", "2147483648" })
        {
            Assert.Null(LoadWithRaw(dir, "audioMusic", text).AudioMusic);
            Assert.Equal("original", LoadWithRaw(dir, "audioMusic", text).MenuPresentation);
        }

        Assert.Equal(0, LoadWithRaw(dir, "audioMusic", "0").AudioMusic);
        Assert.Equal(100, LoadWithRaw(dir, "audioMusic", "100").AudioMusic);
    }

    /// <summary>A level that is not a number at all: a quoted digit string, a word, a boolean, a
    /// fraction, an explicit null and a container each read as never set. The store proves the JSON
    /// kind as well as the range, so a hand-edited file cannot hand a bus something that is not a
    /// level.</summary>
    [Fact]
    public void Load_LevelOfTheWrongKind_DropsOnlyThatField()
    {
        var dir = TestData.TempDir();
        foreach (string text in new[] { "\"50\"", "\"loud\"", "true", "null", "50.5", "[50]", "{}", "\"\"" })
        {
            Assert.Null(LoadWithRaw(dir, "audioVoice", text).AudioVoice);
            Assert.Equal("original", LoadWithRaw(dir, "audioVoice", text).MenuPresentation);
        }

        Assert.Equal(50, LoadWithRaw(dir, "audioVoice", "50").AudioVoice);
    }

    /// <summary>A file written before the levels existed: the version does not move for fields
    /// added beside the others, so an older file still loads everything it carries and reads the
    /// four it does not as never set.</summary>
    [Fact]
    public void Load_FileWithoutTheVolumeFields_KeepsTheFieldsItHas()
    {
        var dir = TestData.TempDir();
        File.WriteAllText(Path.Combine(dir, "options.json"),
            "{\"version\": 1, \"menuPresentation\": \"original\", \"difficulty\": \"hard\", \"vsync\": \"144\"}",
            new UTF8Encoding(false));
        var def = new OptionsStore(dir).Load();

        Assert.Equal("original", def.MenuPresentation);
        Assert.Equal("hard", def.Difficulty);
        Assert.Equal("144", def.VSync);
        Assert.Null(def.AudioMaster);
        Assert.Null(def.AudioMusic);
        Assert.Null(def.AudioEffects);
        Assert.Null(def.AudioVoice);
    }

    /// <summary>The version gate rejects the levels with the rest of the file: a well-shaped level
    /// at an unknown version reads as never set rather than reaching a bus.</summary>
    [Fact]
    public void Load_UnknownVersion_RejectsTheVolumeLevelsToo()
    {
        var dir = TestData.TempDir();
        File.WriteAllText(Path.Combine(dir, "options.json"),
            "{\"version\": 2, \"audioMaster\": 100, \"audioMusic\": 50, \"audioEffects\": 50, \"audioVoice\": 50}",
            new UTF8Encoding(false));
        var def = new OptionsStore(dir).Load();

        Assert.Null(def.AudioMaster);
        Assert.Null(def.AudioMusic);
        Assert.Null(def.AudioEffects);
        Assert.Null(def.AudioVoice);
    }

    /// <summary>A level is written as a JSON number and a never-set one as an explicit null, the
    /// same contract the words hold: the file names every option the build knows, so a reader can
    /// tell "not set" from "written by an older build" by eye.</summary>
    [Fact]
    public void Serialize_WritesALevelAsANumberAndANeverSetOneAsNull()
    {
        string json = OptionsStore.Serialize(new OptionsDef { AudioMaster = 0, AudioVoice = 100 });

        Assert.Contains("\"audioMaster\": 0", json);
        Assert.Contains("\"audioVoice\": 100", json);
        Assert.Contains("\"audioMusic\": null", json);
        Assert.Contains("\"audioEffects\": null", json);
    }

    // One field's value written into an otherwise good file and loaded back. The presentation
    // beside it is the control: it says the reader read the file, so a null in the field under
    // test is that field being dropped rather than the whole file being thrown away.
    private static OptionsDef LoadWith(string dir, string name, string value) =>
        LoadWithRaw(dir, name, $"\"{value}\"");

    // The same, with the value written into the file exactly as given rather than quoted: a level
    // is a JSON number, and quoting one would test the wrong rejection.
    private static OptionsDef LoadWithRaw(string dir, string name, string json)
    {
        File.WriteAllText(Path.Combine(dir, "options.json"),
            $"{{\"version\": 1, \"menuPresentation\": \"original\", \"{name}\": {json}}}",
            new UTF8Encoding(false));
        return new OptionsStore(dir).Load();
    }
}
