using System;
using System.IO;
using CSVM.Flight.Hangar;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The 204-byte importer's rejection paths. No original save is committed as a fixture, so the
/// decoded field layout is pinned by docs/formats/paint.md rather than by a test here.
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
    public void ImportDirectory_MissingDirectory_IsEmpty()
    {
        Assert.Empty(CustomPlaneRecord.ImportDirectory(Path.Combine(TestData.TempDir(), "absent-subdir")));
    }

    [Fact]
    public void ImportDirectory_RelativePath_Throws()
    {
        Assert.Throws<ArgumentException>(() => CustomPlaneRecord.ImportDirectory("Planes"));
    }
}
