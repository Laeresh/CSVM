using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CSVM.Extraction;
using CSVM.UI.Screens;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="SourceFlow"/>, the game-file screen's state. Covered are what a pick is refused for,
/// the copy, the questions asked once, and the delete that never follows a link. Every install and
/// image here is built in a temp folder; workers run inline.
/// </summary>
public class SourceFlowTests
{
    private static readonly InstallSearchRoots Nowhere = new(true, null, Array.Empty<string>(), Array.Empty<string>());

    [Fact]
    public void APickThatIsNotGameFilesIsRefusedWithItsReason()
    {
        string root = TestData.TempDir();
        string documents = Directory.CreateDirectory(Path.Combine(root, "Documents")).FullName;
        Assert.Null(SourceFlow.Validate(documents, out string? refusal));
        Assert.Equal(InstallLocator.Check(documents).Message, refusal);

        string text = Path.Combine(root, "notes.txt");
        File.WriteAllText(text, "x");
        Assert.Null(SourceFlow.Validate(text, out refusal));
        Assert.Contains("is a file, not a folder or a disc image", refusal);

        string cue = Path.Combine(root, "game.cue");
        File.WriteAllText(cue, "FILE \"game.bin\" BINARY\n  TRACK 01 MODE1/2352\n    INDEX 01 00:00:00\n");
        Assert.Null(SourceFlow.Validate(cue, out refusal));
        Assert.Contains("could not be read as a disc image", refusal);
        Assert.Contains("game.bin", refusal);

        string iso = Path.Combine(root, "other.iso");
        File.WriteAllBytes(iso, IsoImageTests.Build(new[] { ("README.TXT;1", new byte[] { 1 }) }));
        Assert.Null(SourceFlow.Validate(iso, out refusal));
        Assert.Contains("is a disc image, but not of Crimson Skies", refusal);

        string empty = Path.Combine(root, "empty.iso");
        File.WriteAllBytes(empty, new byte[4096]);
        Assert.Null(SourceFlow.Validate(empty, out refusal));
        Assert.Contains("could not be read as a disc image", refusal);
    }

    [Fact]
    public void AnInstallAndADiscImageAreGameFilesAndARememberedImageIsLocated()
    {
        string root = TestData.TempDir();
        string install = Install(root);
        Assert.Equal(Path.GetFullPath(install), SourceFlow.Validate("  \"" + install + "\"  ", out string? refusal)!.Location);
        Assert.Null(refusal);

        string iso = Path.Combine(root, "game.iso");
        File.WriteAllBytes(iso, IsoImageTests.Build(new[] { ("GAME/ZBD/ZRDR.ZBD;1", new byte[] { 1 }), ("GAME/GOSDATA/ASSETS/CRIMSON.ROF;1", new byte[] { 2 }) }));
        Assert.NotNull(SourceFlow.Validate(iso, out refusal));
        Assert.Null(refusal);
        Assert.Equal(iso, GameFileSource.Locate(null, Path.Combine(root, "data"), iso, Nowhere)?.Location);
    }

    [Fact]
    public void APickIsRememberedAndAMissingPatchIsNotAskedAbout()
    {
        string root = TestData.TempDir();
        string install = Install(root);
        var store = new OptionsStore(Directory.CreateDirectory(Path.Combine(root, "user")).FullName);
        string data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        var flow = new SourceFlow(data, null, string.Empty, store, ask: true, work => work());
        Assert.Equal(SourceView.Asking, flow.View);
        Assert.False(flow.Use());
        Assert.NotNull(flow.Notice);

        Assert.True(flow.Pick(install));
        Assert.Null(flow.Notice);
        Assert.True(flow.Use());
        Assert.Equal(SourceView.Found, flow.View);
        Assert.True(flow.Picked);
        Assert.Equal(Path.GetFullPath(install), store.Load().InstallPath);
        Assert.Equal(new[] { "Play", "Copy, then play", "Choose others" }, flow.Choices.Select(c => c.Label));

        // The install holds no crimptch.rof: its one script is one CSVM never runs, so Play asks nothing.
        Choose(flow, "Play");
        Assert.Equal(SourceView.Done, flow.View);

        var again = new SourceFlow(data, flow.Source, string.Empty, store, ask: true, work => work());
        Assert.Equal(SourceView.Done, again.View);
    }

    [Fact]
    public void AnOldExtractionIsAskedAboutOnceAndKeptUntouched()
    {
        string root = TestData.TempDir();
        var source = new InstallFolderSource(Install(root));
        string data = DataRootWithExtracted(root);
        var store = new OptionsStore(Directory.CreateDirectory(Path.Combine(root, "user")).FullName);

        Assert.Equal(SourceView.Done, new SourceFlow(data, source, string.Empty, store, ask: false).View);
        var flow = new SourceFlow(data, source, string.Empty, store, ask: true);
        Assert.Equal(SourceView.Upgrade, flow.View);
        Assert.Contains("modding folder", flow.Body);
        Assert.Equal("Keep it", flow.Choices[0].Label);
        Choose(flow, "Keep it");
        Assert.Equal(SourceView.Done, flow.View);
        Assert.True(Directory.Exists(Path.Combine(data, SourceFlow.ExtractedFolder)));
        Assert.True(store.Load().ExtractedPromptAnswered);
        Assert.Equal(SourceView.Done, new SourceFlow(data, source, string.Empty, store, ask: true).View);
    }

    [Fact]
    public void ADeleteIsConfirmedFirstAndWithoutAStoreIsAskedOncePerFlow()
    {
        string root = TestData.TempDir();
        var source = new InstallFolderSource(Install(root));
        string data = DataRootWithExtracted(root);
        var flow = new SourceFlow(data, source, string.Empty, null, ask: true, work => work());
        Choose(flow, "Delete it...");
        Assert.Equal(SourceView.ConfirmDelete, flow.View);
        Assert.Equal("Back", flow.Choices[0].Label);
        Choose(flow, "Back");
        Assert.Equal(SourceView.Upgrade, flow.View);
        Assert.True(Directory.Exists(Path.Combine(data, SourceFlow.ExtractedFolder)));

        Choose(flow, "Delete it...");
        Choose(flow, "Delete");
        flow.Tick();
        Assert.Equal(SourceView.Done, flow.View);
        Assert.False(Directory.Exists(Path.Combine(data, SourceFlow.ExtractedFolder)));
        Assert.False(SourceFlow.HasExtracted(data));
    }

    [Fact]
    public void TheDeleteUnlinksAJunctionAndLeavesItsTargetAlone()
    {
        string root = TestData.TempDir();
        string outside = Directory.CreateDirectory(Path.Combine(root, "footage")).FullName;
        File.WriteAllText(Path.Combine(outside, "keep.txt"), "irreplaceable");
        string extracted = Directory.CreateDirectory(Path.Combine(root, "extracted", "C1")).Parent!.FullName;
        File.WriteAllText(Path.Combine(extracted, "C1", "gamez.zip"), "x");
        File.SetAttributes(Path.Combine(extracted, "C1", "gamez.zip"), FileAttributes.ReadOnly);
        string link = Path.Combine(extracted, "linked");
        Link(link, outside);
        Assert.True(File.Exists(Path.Combine(link, "keep.txt")));

        SourceFlow.DeleteWithoutFollowingLinks(extracted);
        Assert.False(Directory.Exists(extracted));
        Assert.True(File.Exists(Path.Combine(outside, "keep.txt")));

        // The folder itself a link: unlinked, its target kept whole.
        string linkedRoot = Path.Combine(root, "extracted2");
        Link(linkedRoot, outside);
        SourceFlow.DeleteWithoutFollowingLinks(linkedRoot);
        Assert.False(Directory.Exists(linkedRoot));
        Assert.True(File.Exists(Path.Combine(outside, "keep.txt")));
    }

    [Fact]
    public void ACopyRebindsToTheCopyAndAnExistingTargetAsksFirst()
    {
        string root = TestData.TempDir();
        string install = Install(root);
        string data = Directory.CreateDirectory(Path.Combine(root, "data")).FullName;
        var flow = new SourceFlow(data, null, install, null, ask: true, work => work());
        Assert.True(flow.Use());
        Choose(flow, "Copy, then play");
        Assert.Equal(SourceView.Copying, flow.View);
        flow.Tick();
        Assert.Equal(SourceView.Done, flow.View);
        Assert.True(flow.Picked);
        Assert.Equal(GameFileCopy.Target(data), flow.Source!.Location);
        Assert.Contains(CSVM.Mech3.Messages.GameFile, flow.Missing);

        // A copy target in place: a fresh pick asks before it replaces, and Back keeps it.
        var next = new SourceFlow(data, null, install, null, ask: true, work => work());
        Assert.True(next.Use());
        Choose(next, "Copy, then play");
        Assert.Equal(SourceView.Replace, next.View);
        Choose(next, "Back");
        Assert.Equal(SourceView.Found, next.View);

        // The copy itself picked offers no copy onto itself.
        var self = new SourceFlow(data, null, GameFileCopy.Target(data), null, ask: true, work => work());
        Assert.True(self.Use());
        Assert.Equal(new[] { "Play", "Choose others" }, self.Choices.Select(c => c.Label));
    }

    [Fact]
    public void TheOnceFlagRoundTripsThroughTheOptionsFile()
    {
        var def = new OptionsDef { ExtractedPromptAnswered = true };
        Assert.True(OptionsStore.Deserialize(OptionsStore.Serialize(def))!.ExtractedPromptAnswered);
        Assert.Null(OptionsStore.Deserialize(OptionsStore.Serialize(new OptionsDef()))!.ExtractedPromptAnswered);
    }

    private static void Choose(SourceFlow flow, string label) =>
        flow.Choices.Single(c => c.Label == label).Press();

    // An install layout with one chapter archive and no patch archive.
    private static string Install(string root)
    {
        string install = Path.Combine(root, "Crimson Skies");
        Directory.CreateDirectory(Path.Combine(install, "ZBD", "C1"));
        Directory.CreateDirectory(Path.Combine(install, "GOSDATA", "ASSETS"));
        File.WriteAllText(Path.Combine(install, "ZBD", "planes.zbd"), "x");
        File.WriteAllText(Path.Combine(install, "ZBD", "C1", "gamez.zbd"), "x");
        File.WriteAllText(Path.Combine(install, "GOSDATA", "ASSETS", "crimson.rof"), "x");
        return install;
    }

    private static string DataRootWithExtracted(string root)
    {
        string data = Path.Combine(root, "data");
        Directory.CreateDirectory(Path.Combine(data, SourceFlow.ExtractedFolder));
        File.WriteAllText(Path.Combine(data, SourceFlow.ExtractedFolder, "planes.zip"), "x");
        return data;
    }

    // A junction on Windows, which needs no privilege; a symbolic link elsewhere.
    private static void Link(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }

        using var mklink = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        })!;
        mklink.WaitForExit();
        Assert.Equal(0, mklink.ExitCode);
        Assert.True((new DirectoryInfo(link).Attributes & FileAttributes.ReparsePoint) != 0);
    }
}
