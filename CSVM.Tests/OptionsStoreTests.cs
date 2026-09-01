using System.IO;
using System.Text;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>The persistence contract: a missing file reads as empty, a valid file round-trips, an
/// unknown version or malformed JSON invalidates the whole file, an unknown presentation value is
/// dropped without invalidating the file, and a save is atomic against an interrupted write.</summary>
public class OptionsStoreTests
{
    [Fact]
    public void Load_MissingFile_ReadsAsEmpty()
    {
        var store = new OptionsStore(TestData.TempDir());

        Assert.Null(store.Load().MenuPresentation);
    }

    [Fact]
    public void RoundTrip_PreservesTheRequestedPresentation()
    {
        var store = new OptionsStore(TestData.TempDir());
        store.Save(new OptionsDef { MenuPresentation = "original" });

        Assert.Equal("original", store.Load().MenuPresentation);
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
