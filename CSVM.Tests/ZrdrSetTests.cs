using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The reader scope every engine consumer reads. Its lookups keep the contracts of the zip and
/// folder entry points of <see cref="Zrdr"/>, and every read hands out a fresh copy. The scope is
/// built in code from invented records.
/// </summary>
[Trait("Tier", "Quick")]
public class ZrdrSetTests
{
    [Fact]
    public void AReaderIsFoundUnderEverySpellingTheEngineUses()
    {
        var set = Scope();
        foreach (string name in new[] { "probe.json", "probe.zrd.json", "probe.zrd", "PROBE.JSON", "repeat-1.json" })
        {
            Assert.True(set.HasFile(name), name);
        }

        Assert.False(set.HasFile("absent.json"));
        Assert.Equal("first", set.LoadFile("repeat.json")[0]);
    }

    [Fact]
    public void AnAbsentReaderThrowsAndAnEmptyOneIsAnErrorOnlyForLoadFile()
    {
        var set = Scope();
        Assert.Throws<FileNotFoundException>(() => set.LoadFile("absent.json"));
        Assert.Throws<FileNotFoundException>(() => set.LoadFileOrEmpty("absent.json"));
        Assert.Throws<InvalidDataException>(() => set.LoadFile("empty.json"));
        Assert.Empty(set.LoadFileOrEmpty("empty.json"));
    }

    [Fact]
    public void EveryReadIsAFreshCopy()
    {
        var set = Scope();
        var first = set.LoadFile("probe.json");
        ((List<object?>)first[1]!).Add("edited");
        first.Clear();

        var second = set.LoadFile("probe.json");
        Assert.Equal(new object?[] { "KEY", new List<object?> { 1f, "deep" } }, second);
        Assert.NotSame(second, set.LoadMatchingFiles("deep").Single().Root);
    }

    [Fact]
    public void TheEnumerationsSkipEmptyReadersAndNameEachAsItsZipEntry()
    {
        var set = Scope();
        Assert.Equal(new[] { "probe.zrd.json" }, set.LoadMatchingFiles("deep").Select(r => r.Name));
        Assert.Equal(new[] { "probe.zrd.json", "repeat.zrd.json", "repeat-1.zrd.json", "numbers.zrd.json" },
            set.LoadMatchingFiles(string.Empty).Select(r => r.Name));
        Assert.Equal(new[] { "repeat.zrd.json", "repeat-1.zrd.json" },
            set.LoadFilesNamed(n => n.StartsWith("repeat", StringComparison.Ordinal)).Select(r => r.Name));
        Assert.Empty(set.LoadFilesNamed(n => n.StartsWith("empty", StringComparison.Ordinal)));
    }

    [Fact]
    public void NoScopeHoldsNothingAndSaysSo()
    {
        Assert.False(ZrdrSet.None.Exists);
        Assert.False(ZrdrSet.None.HasFile("probe.json"));
        Assert.Throws<FileNotFoundException>(() => ZrdrSet.None.LoadFile("probe.json"));
        Assert.Empty(ZrdrSet.None.LoadMatchingFiles(string.Empty));
        Assert.True(new ZrdrSet("empty", Array.Empty<ZrdrFile>()).Exists);
        Assert.Same(ZrdrSet.None, ZrdrSet.Load(null, "ZBD/zrdr.zbd"));
        Assert.Same(ZrdrSet.None, ZrdrSet.FromExtracted(Path.Combine(TestData.TempDir(), "absent")));
    }

    [Fact]
    public void AFolderIsReadUnderTheForksEntryNamesOnly()
    {
        string dir = TestData.TempDir();
        File.WriteAllText(Path.Combine(dir, "probe.zrd.json"), "[[\"KEY\", [1]]]");
        File.WriteAllText(Path.Combine(dir, "empty.zrd.json"), "null");
        File.WriteAllText(Path.Combine(dir, "plain.json"), "[[\"KEY\", [2]]]");

        var set = ZrdrSet.FromExtracted(dir);
        Assert.Equal(new[] { "empty.zrd", "probe.zrd" }, set.Files.Select(f => f.Key).OrderBy(k => k, StringComparer.Ordinal));
        Assert.Empty(set.LoadFileOrEmpty("empty.json"));
        Assert.False(set.HasFile("plain.json"));
    }

    private static ZrdrSet Scope() => new("invented", new[]
    {
        new ZrdrFile("probe.zrd", null, new List<object?> { "KEY", new List<object?> { 1f, "deep" } }),
        new ZrdrFile("repeat.zrd", null, new List<object?> { "first" }),
        new ZrdrFile("repeat.zrd", "repeat-1.zrd", new List<object?> { "second" }),
        new ZrdrFile("empty.zrd", null, new List<object?>()),
        new ZrdrFile("numbers.zrd", null, new List<object?> { 1f, 2f }),
    });
}
