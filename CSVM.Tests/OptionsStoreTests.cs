using System.IO;
using System.Text;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The persistence contract: a missing file reads as empty, a valid file round-trips
/// every option, an unknown version or malformed JSON invalidates the whole file, an unknown value
/// in a field is dropped without invalidating the file, a file written before a field existed
/// still loads its other fields, and a save is atomic against an interrupted write.</summary>
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
}
