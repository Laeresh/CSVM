using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using CSVM.Extraction;
using CSVM.Mech3;
using CSVM.Spec;
using Xunit;
using Xunit.Abstractions;

namespace CSVM.Tests;

/// <summary>
/// <see cref="GameFileCopy"/>, the copy of a source's game files into the data root. The units run
/// over a source held in memory. The install test copies the located install and reads every
/// session slot back from the copy. The raw-disc test copies the image
/// <see cref="RawCdImageDataTests.Variable"/> names and compares the copy with the install.
/// </summary>
public class GameFileCopyTests
{
    private static readonly InstallSearchRoots Nowhere = new(true, null, Array.Empty<string>(), Array.Empty<string>());

    private readonly ITestOutputHelper _output;

    public GameFileCopyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TheListHoldsEveryFileTheReadersOpen()
    {
        var files = new HashSet<string>(GameFileCopy.Files, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(GameFileCopy.Files.Count, files.Count);
        Assert.Equal(1 + ZbdManifest.Expected.Count + 2 + 2 + RofFiles.Cinemas.Count, files.Count);
        foreach (string path in new[] { Messages.GameFile, InterpScripts.GamePath, GameData.RimagePath, GameData.PlanesPath, RofFiles.ArchivePath, RofFiles.PatchPath })
        {
            Assert.Contains(path, files);
        }

        Assert.All(ZbdManifest.Expected, archive => Assert.Contains("ZBD/" + archive, files));
        Assert.All(RofFiles.Cinemas, movie => Assert.Contains("GOSDATA/ASSETS/GRAPHICS/MPG/" + movie, files));
        Assert.Contains("GOSDATA/ASSETS/BINARIES/langui.dll", files);
        Assert.Contains("GOSDATA/ASSETS/BINARIES/language.dll", files);
        Assert.DoesNotContain(files, f => f.EndsWith(".cab", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ACopyIsFoundAsTheInstallAndHoldsEveryFile()
    {
        var source = MemorySource.Complete();
        string dataRoot = TestData.TempDir();
        var steps = new List<GameFileCopyProgress>();

        var result = GameFileCopy.Run(source, dataRoot, progress: steps.Add);

        string target = GameFileCopy.Target(dataRoot);
        Assert.Equal(target, result.Folder);
        Assert.Equal(GameFileCopy.Files.Count, result.Files);
        Assert.Empty(result.Missing);
        Assert.Equal(source.Bytes, result.Bytes);
        Assert.Equal(new GameFileCopyProgress(GameFileCopy.Files.Count, GameFileCopy.Files.Count, source.Bytes, GameFileCopy.Files[^1]), steps[^1]);
        Assert.True(steps.Select(s => s.Bytes).SequenceEqual(steps.Select(s => s.Bytes).Order()));
        var copy = Assert.IsType<InstallFolderSource>(GameFileSource.Locate(null, dataRoot, null, Nowhere));
        Assert.Equal(target, copy.Location);
        Assert.All(GameFileCopy.Files, file => Assert.Equal(source.Content(file), copy.ReadAllBytes(file)));
        Assert.Equal(new[] { GameFileSource.DataRootFolder }, Directory.GetFileSystemEntries(dataRoot).Select(Path.GetFileName));
    }

    [Fact]
    public void AFileTheSourceLacksIsNamedAndTheRestAreCopied()
    {
        var source = MemorySource.Complete(without: RofFiles.PatchPath);
        string dataRoot = TestData.TempDir();
        GameFileCopyProgress? last = null;

        var result = GameFileCopy.Run(source, dataRoot, progress: step => last = step);

        Assert.Equal(new[] { RofFiles.PatchPath }, result.Missing);
        Assert.Equal(GameFileCopy.Files.Count - 1, result.Files);
        Assert.Equal(GameFileCopy.Files.Count, last?.Done);
        Assert.NotNull(GameFileSource.Open(GameFileCopy.Target(dataRoot)));
    }

    [Fact]
    public void AnExistingCopyIsLeftAloneUnlessReplaceIsAsked()
    {
        string dataRoot = TestData.TempDir();
        string marker = Marker(dataRoot);

        Assert.Throws<IOException>(() => GameFileCopy.Run(MemorySource.Complete(), dataRoot));
        Assert.True(File.Exists(marker));

        GameFileCopy.Run(MemorySource.Complete(), dataRoot, replace: true);
        Assert.False(File.Exists(marker));
        Assert.NotNull(GameFileSource.Open(GameFileCopy.Target(dataRoot)));
        Assert.Equal(new[] { GameFileSource.DataRootFolder }, Directory.GetFileSystemEntries(dataRoot).Select(Path.GetFileName));
    }

    [Fact]
    public void ACancelledCopyLeavesNoTreeAndTheOldCopyStanding()
    {
        string fresh = TestData.TempDir();
        using (var cancel = new CancellationTokenSource())
        {
            Assert.Throws<OperationCanceledException>(() => GameFileCopy.Run(MemorySource.Complete(), fresh, progress: step => cancel.Cancel(), cancel: cancel.Token));
        }

        Assert.Empty(Directory.GetFileSystemEntries(fresh));

        string replaced = TestData.TempDir();
        string marker = Marker(replaced);
        using (var cancel = new CancellationTokenSource())
        {
            Assert.Throws<OperationCanceledException>(() => GameFileCopy.Run(
                MemorySource.Complete(), replaced, replace: true, step => { if (step.Done == 3) { cancel.Cancel(); } }, cancel.Token));
        }

        Assert.True(File.Exists(marker));
        Assert.Equal(new[] { GameFileSource.DataRootFolder }, Directory.GetFileSystemEntries(replaced).Select(Path.GetFileName));
    }

    [Fact]
    public void AFailedReadLeavesNoTree()
    {
        string dataRoot = TestData.TempDir();
        var source = MemorySource.Complete(failing: "ZBD/C2/gamez.zbd");

        var thrown = Assert.Throws<IOException>(() => GameFileCopy.Run(source, dataRoot));

        Assert.Contains("C2/gamez.zbd", thrown.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFileSystemEntries(dataRoot));
    }

    [Fact]
    public void AStalePartialTreeIsReplacedAndNeverFound()
    {
        string dataRoot = TestData.TempDir();
        string partial = GameFileCopy.Target(dataRoot) + GameFileCopy.PartialSuffix;
        Directory.CreateDirectory(Path.Combine(partial, "ZBD"));
        Directory.CreateDirectory(Path.Combine(partial, "GOSDATA", "ASSETS"));
        File.WriteAllText(Path.Combine(partial, "ZBD", "stale.zbd"), "stale");
        Assert.Null(GameFileSource.Locate(null, dataRoot, null, Nowhere));

        GameFileCopy.Run(MemorySource.Complete(), dataRoot);

        Assert.False(Directory.Exists(partial));
        Assert.Null(InstallLocator.ResolveFile(GameFileCopy.Target(dataRoot), "ZBD/stale.zbd"));
    }

    [Fact]
    public void TheFlagRefusesASourceThatIsNotAnInstallAndAnExistingCopy()
    {
        string dataRoot = TestData.TempDir();
        Assert.False(GameFileCopy.RunToLog(Path.Combine(dataRoot, "absent"), dataRoot));
        Assert.Empty(Directory.GetFileSystemEntries(dataRoot));

        string install = TestData.TempDir();
        Directory.CreateDirectory(Path.Combine(install, "ZBD"));
        Directory.CreateDirectory(Path.Combine(install, "GOSDATA", "ASSETS"));
        File.WriteAllText(Path.Combine(install, "strings.dll"), "strings");
        Assert.True(GameFileCopy.RunToLog(install, dataRoot));
        Assert.Equal("strings", File.ReadAllText(Path.Combine(GameFileCopy.Target(dataRoot), "strings.dll")));
        Assert.False(GameFileCopy.RunToLog(install, dataRoot));
    }

    [GameFilesFact]
    public void ACopyOfTheInstallReadsLikeTheInstall()
    {
        var install = GameFilesData.Source!;
        string dataRoot = TestData.TempDir();
        try
        {
            var result = GameFileCopy.Run(install, dataRoot);
            _output.WriteLine($"{result.Files} files, {result.Bytes} bytes copied from {install.Location}");
            // An installed patch leaves nothing missing; a pressed disc lacks only crimptch.rof.
            Assert.Equal(GameFileCopy.Files.Where(file => !install.Exists(file)), result.Missing);
            var copy = GameFileSource.Locate(null, dataRoot, null, Nowhere);
            Assert.Equal(GameFileCopy.Target(dataRoot), copy?.Location);
            AssertReadsLike(install, copy!);
        }
        finally
        {
            Directory.Delete(dataRoot, recursive: true);
        }
    }

    [RealCueFact]
    public void ACopyOfTheRawDiscMatchesTheInstall()
    {
        var install = GameFilesData.Source ?? throw new InvalidOperationException(GameFilesData.NoSourceReason);
        var disc = GameFileSource.Open(Environment.GetEnvironmentVariable(RawCdImageDataTests.Variable)!);
        Assert.IsType<CabinetSource>(disc);
        string dataRoot = TestData.TempDir();
        try
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var result = GameFileCopy.Run(disc!, dataRoot);
            _output.WriteLine($"{result.Files} files, {result.Bytes} bytes copied from {disc!.Location} in {clock.ElapsedMilliseconds} ms");
            result.Missing.ToList().ForEach(file => _output.WriteLine($"not on the disc: {file}"));
            // The English pressing holds no crimptch.rof; an install's comes from the patch.
            Assert.Equal(GameFileCopy.Files.Where(file => !disc.Exists(file)), result.Missing);
            var copy = GameFileSource.Locate(null, dataRoot, null, Nowhere)!;
            Assert.IsType<InstallFolderSource>(copy);
            var differ = GameFileCopy.Files.Where(copy.Exists).Where(file => !install.ReadAllBytes(file).AsSpan().SequenceEqual(copy.ReadAllBytes(file))).ToList();
            Assert.Empty(differ);
            AssertReadsLike(disc, copy);
        }
        finally
        {
            Directory.Delete(dataRoot, recursive: true);
        }
    }

    private static string Marker(string dataRoot)
    {
        string target = GameFileCopy.Target(dataRoot);
        Directory.CreateDirectory(target);
        string marker = Path.Combine(target, "player.txt");
        File.WriteAllText(marker, "kept");
        return marker;
    }

    // Every slot a session fills reads from the copy, and the UI view holds what the source's does.
    private static void AssertReadsLike(IGameFileSource source, IGameFileSource copy)
    {
        Assert.All(GameFileCopy.Files, file => Assert.Equal(source.Exists(file), copy.Exists(file)));
        var data = GameData.Load(copy);
        // The launch-long sound archive holds its file open, which would block the copy's delete.
        using var sounds = data.Sounds;
        Assert.NotNull(sounds);
        Assert.NotNull(data.Interp);
        Assert.True(data.Messages.Count > 0);
        Assert.True(data.Rimage.Count > 0);
        Assert.NotNull(data.Planes);
        var chapter = data.LoadChapter("C1", "M02");
        Assert.True(chapter.Textures.Count > 0);
        Assert.NotNull(chapter.Gamez);
        var view = RofFiles.Open(copy);
        Assert.Equal(RofFiles.Open(source).Files.Order(StringComparer.Ordinal), view.Files.Order(StringComparer.Ordinal));
        Assert.NotNull(view.Read(RofFiles.UiStringsFile));
        Assert.NotNull(view.Read(RofFiles.MenuLayoutFile));
        Assert.All(RofFiles.Cinemas, movie => Assert.True(view.Exists(RofFiles.Cinema(movie)), movie));
    }

    // Every listed file with its own small content, in the case an install might spell it.
    private sealed class MemorySource : IGameFileSource
    {
        private readonly Dictionary<string, byte[]> _files;
        private readonly string? _failing;

        private MemorySource(Dictionary<string, byte[]> files, string? failing)
        {
            _files = files;
            _failing = failing;
        }

        public string Location => "memory";

        public long Bytes => _files.Values.Sum(f => (long)f.Length);

        public static MemorySource Complete(string? without = null, string? failing = null) => new(
            GameFileCopy.Files
                .Where(f => f != without)
                .ToDictionary(f => f.ToUpperInvariant(), f => Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(f, 1 + (f.Length % 5)))), StringComparer.OrdinalIgnoreCase),
            failing);

        public byte[] Content(string path) => _files[path];

        public bool Exists(string path) => _files.ContainsKey(path);

        public Stream? Open(string path) =>
            string.Equals(path, _failing, StringComparison.OrdinalIgnoreCase) ? throw new IOException($"'{path}' does not read")
            : _files.TryGetValue(path, out var bytes) ? new MemoryStream(bytes, writable: false) : null;
    }
}
