using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSVM.Extraction;
using CSVM.Flight.Hangar;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The 204-byte importer. No original save is committed as a fixture (they are the player's
/// files), so every record here is built from the decoded layout in docs/formats/paint.md.
/// </summary>
public class CustomPlaneRecordTests
{
    [Fact]
    public void Read_TooShort_IsNull()
    {
        Assert.Null(CustomPlaneRecord.Read(Array.Empty<byte>()));
        Assert.Null(CustomPlaneRecord.Read(new byte[CustomPlaneRecord.Length - 1]));
    }

    [Fact]
    public void Read_EmptyName_IsNull()
    {
        Assert.Null(CustomPlaneRecord.Read(new byte[CustomPlaneRecord.Length]));
    }

    [Fact]
    public void ReadFile_MissingFile_IsNull()
    {
        Assert.Null(CustomPlaneRecord.ReadFile(Path.Combine(TestData.TempDir(), "No Such Plane")));
    }

    [Fact]
    public void ImportInto_MissingFolder_ImportsNothing()
    {
        var store = new CustomPlaneStore(TestData.TempDir());
        var result = CustomPlaneRecord.ImportInto(store, One(Path.Combine(TestData.TempDir(), "absent-subdir")));
        Assert.Empty(result.Imported);
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void SaveFolders_RelativePath_Throws()
    {
        Assert.Throws<ArgumentException>(() => CustomPlaneRecord.SaveFolders("Crimson Skies", null));
    }

    /// <summary>Every chosen field of a synthetic record survives the read, the import and the
    /// store's own JSON. The twin bits carry a stray high bit, as one genuine save did.</summary>
    [Fact]
    public void ImportInto_RoundTripsEveryDecodedField()
    {
        string planes = TestData.TempDir();
        File.WriteAllBytes(Path.Combine(planes, "Blue Streak"), Record("Blue Streak"));
        var store = new CustomPlaneStore(TestData.TempDir());

        var result = CustomPlaneRecord.ImportInto(store, One(planes));

        Assert.Equal(new[] { "Blue Streak" }, result.Imported);
        var def = store.Load("Blue Streak");
        Assert.NotNull(def);
        Assert.Equal(7, def!.Airframe);
        Assert.Equal(3, def.Engine);
        Assert.Equal((2, 1), (def.LeftHardpoints, def.RightHardpoints));
        Assert.Equal((3, 4, 5, 6), (def.ArmourNose, def.ArmourTail, def.ArmourLeftWing, def.ArmourRightWing));
        Assert.Equal(
            new[] { new GunChoice(2, true), new GunChoice(2, true), new GunChoice(0, false), new GunChoice(null, false) },
            def.Guns);
        Assert.Equal(4, def.PaintPattern);
        Assert.Equal(new[] { 5, 12, 20 }, def.PaintColours);
        Assert.Equal(new[] { 1, 2, 3 }, def.PaintShades);
        Assert.Equal((40, 8, 7), (def.NoseDecal, def.TailDecal, def.WingDecal));
        Assert.False(def.HasLoadout);
        Assert.False(def.AwaitingExport);
    }

    /// <summary>A stored plane of the same name is kept, and the import lands beside it with a
    /// suffix. A second import of the same files writes nothing new.</summary>
    [Fact]
    public void ImportInto_NameClash_KeepsBothAndReimportAddsNothing()
    {
        string planes = TestData.TempDir();
        File.WriteAllBytes(Path.Combine(planes, "Blue Streak"), Record("Blue Streak"));
        var store = new CustomPlaneStore(TestData.TempDir());
        store.Save(new CustomPlaneDef { Name = "Blue Streak", Airframe = 2, Engine = 1 });

        var first = CustomPlaneRecord.ImportInto(store, One(planes));
        var second = CustomPlaneRecord.ImportInto(store, One(planes));

        Assert.Equal(new[] { "Blue Streak (2)" }, first.Renamed);
        Assert.Equal(2, store.Load("Blue Streak")!.Airframe);
        Assert.Equal(7, store.Load("Blue Streak (2)")!.Airframe);
        Assert.Empty(second.Imported);
        Assert.Equal(new[] { "Blue Streak (2)" }, second.AlreadyThere);
        Assert.Equal(2, store.List().Count);
    }

    /// <summary>A short file and one carrying an out-of-range airframe are skipped and named; the
    /// good file beside them still imports.</summary>
    [Fact]
    public void ImportInto_BadFiles_AreSkippedAndNamed()
    {
        string planes = TestData.TempDir();
        File.WriteAllBytes(Path.Combine(planes, "Good"), Record("Good"));
        File.WriteAllBytes(Path.Combine(planes, "Short"), new byte[12]);
        var wrong = Record("Wrong");
        BinaryPrimitives.WriteInt32LittleEndian(wrong.AsSpan(0x2c), 99);
        File.WriteAllBytes(Path.Combine(planes, "Wrong"), wrong);
        var store = new CustomPlaneStore(TestData.TempDir());

        var result = CustomPlaneRecord.ImportInto(store, One(planes));

        Assert.Equal(new[] { "Good" }, result.Imported);
        Assert.Equal(new[] { "Short", "Wrong" }, result.Skipped);
        Assert.Equal(new[] { "Good" }, store.List().Select(p => p.Name));
    }

    /// <summary>The VirtualStore copy is read first and shadows the install's file of the same
    /// name, as Windows shows it to the game.</summary>
    [Fact]
    public void SaveFolders_VirtualStoreFirstAndShadowsTheInstall()
    {
        string install = Path.Combine(TestData.TempDir(), "Microsoft Games", "Crimson Skies");
        string local = TestData.TempDir();
        var folders = CustomPlaneRecord.SaveFolders(install, local);
        Assert.Equal(2, folders.Count);
        Assert.StartsWith(Path.Combine(local, "VirtualStore"), folders[0], StringComparison.Ordinal);
        Assert.EndsWith(Path.Combine("Microsoft Games", "Crimson Skies", "Planes"), folders[0], StringComparison.Ordinal);
        Assert.Equal(Path.Combine(install, "Planes"), folders[1]);

        Directory.CreateDirectory(folders[0]);
        Directory.CreateDirectory(folders[1]);
        var shadow = Record("Ace");
        BinaryPrimitives.WriteInt32LittleEndian(shadow.AsSpan(0x2c), 2);
        File.WriteAllBytes(Path.Combine(folders[0], "Ace"), shadow);
        File.WriteAllBytes(Path.Combine(folders[1], "Ace"), Record("Ace"));
        var store = new CustomPlaneStore(TestData.TempDir());

        CustomPlaneRecord.ImportInto(store, new[] { folders });

        Assert.Equal(new[] { 2 }, store.List().Select(p => p.Airframe));
    }

    /// <summary>A suffix trims the name so the whole stays inside the 32-character cap.</summary>
    [Fact]
    public void ImportInto_SuffixKeepsTheNameInsideTheCap()
    {
        string name = new('A', CustomPlaneDef.MaxNameLength);
        string planes = TestData.TempDir();
        File.WriteAllBytes(Path.Combine(planes, "Long"), Record(name));
        var store = new CustomPlaneStore(TestData.TempDir());
        store.Save(new CustomPlaneDef { Name = name, Airframe = 2, Engine = 1 });

        var result = CustomPlaneRecord.ImportInto(store, One(planes));

        Assert.Equal(new[] { new string('A', CustomPlaneDef.MaxNameLength - 4) + " (2)" }, result.Renamed);
        Assert.Equal(CustomPlaneDef.MaxNameLength, result.Renamed[0].Length);
    }

    /// <summary>A remembered disc image has no saves, so the search goes on to the installs the
    /// locator finds on the machine's drives.</summary>
    [Fact]
    public void FindSaves_FindsAnInstallTheLocatorSeesPastARememberedImage()
    {
        string drive = TestData.TempDir();
        string install = Path.Combine(drive, "Crimson Skies");
        Directory.CreateDirectory(Path.Combine(install, "ZBD"));
        Directory.CreateDirectory(Path.Combine(install, "GOSDATA", "ASSETS"));
        Directory.CreateDirectory(Path.Combine(install, "Planes"));
        var roots = new InstallSearchRoots(true, null, Array.Empty<string>(), new[] { drive });

        var found = CustomPlaneRecord.FindSaves(new[] { Path.Combine(drive, "CrimsonSkies.iso") }, roots, null);

        Assert.Equal(new[] { new[] { Path.Combine(install, "Planes") } }, found);
    }

    /// <summary>An uninstalled game leaves its VirtualStore saves behind: the installer's default
    /// folder under Program Files is gone, and its twin is still found.</summary>
    [Fact]
    public void FindSaves_FindsTheVirtualStoreSavesOfAnAbsentInstall()
    {
        string programFiles = Path.Combine(TestData.TempDir(), "Program Files (x86)");
        string local = TestData.TempDir();
        string twin = CustomPlaneRecord.SaveFolders(Path.Combine(programFiles, "Microsoft Games", "Crimson Skies"), local)[0];
        Directory.CreateDirectory(twin);
        File.WriteAllBytes(Path.Combine(twin, "Ace"), Record("Ace"));
        var roots = new InstallSearchRoots(true, null, new[] { programFiles }, Array.Empty<string>());

        var found = CustomPlaneRecord.FindSaves(Array.Empty<string?>(), roots, local);
        var store = new CustomPlaneStore(TestData.TempDir());

        Assert.Equal(new[] { new[] { twin } }, found);
        Assert.Equal(new[] { "Ace" }, CustomPlaneRecord.ImportInto(store, found).Imported);
    }

    /// <summary>Two installs holding the same file name are two planes: the second lands beside the
    /// first rather than being passed over as a shadowed copy.</summary>
    [Fact]
    public void ImportInto_SameFileNameInTwoInstallsKeepsBoth()
    {
        string first = TestData.TempDir();
        string second = TestData.TempDir();
        File.WriteAllBytes(Path.Combine(first, "Ace"), Record("Ace"));
        var other = Record("Ace");
        BinaryPrimitives.WriteInt32LittleEndian(other.AsSpan(0x2c), 2);
        File.WriteAllBytes(Path.Combine(second, "Ace"), other);
        var store = new CustomPlaneStore(TestData.TempDir());

        var result = CustomPlaneRecord.ImportInto(store, new[] { new[] { first }, new[] { second } });

        Assert.Equal(new[] { "Ace", "Ace (2)" }, result.Imported);
    }

    // A record built from the decoded layout (docs/formats/paint.md "Saved custom planes"). The
    // derived fields stay zero, which the reader ignores.
    internal static byte[] Record(string name)
    {
        var bytes = new byte[CustomPlaneRecord.Length];
        Encoding.Latin1.GetBytes(name).CopyTo(bytes, 0x04);
        void Dword(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
        Dword(0x2c, 7);
        Dword(0x30, 3);
        Dword(0x34, 2);
        Dword(0x38, 1);
        Dword(0x40, 4);
        Dword(0x44, 5);
        Dword(0x48, 12);
        Dword(0x4c, 20);
        Dword(0x50, 1);
        Dword(0x54, 2);
        Dword(0x58, 3);
        Dword(0x5c, 40);
        Dword(0x60, 8);
        Dword(0x64, 7);
        Dword(0x74, 15);
        Dword(0x78, 20);
        Dword(0x7c, 25);
        Dword(0x80, 30);
        Dword(0x84, 0xc3);
        Dword(0x88, 2);
        Dword(0x8c, 2);
        Dword(0x90, 0);
        Dword(0x94, 5);
        return bytes;
    }

    // One install of one folder.
    private static IReadOnlyList<string>[] One(string folder) => new[] { (IReadOnlyList<string>)new[] { folder } };
}
