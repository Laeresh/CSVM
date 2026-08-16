using System;
using System.IO;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The two input sources these tests use. Fixtures are hand-authored bytes and JSON under
/// <c>fixtures/</c>, written from <c>docs/formats/</c>, never copied from an extraction — a
/// trimmed piece of real game data is still game data.
/// Extracted data is the player's own install, git-ignored and absent from a fresh checkout.
/// <see cref="ExtractedRoot"/> is null when it cannot be found, and tests that need it carry
/// <see cref="ExtractedDataFactAttribute"/> so they report skipped rather than passing on nothing.
/// </summary>
public static class TestData
{
    /// <summary>Printed by every skipped data test so an absent install never reads as a pass.</summary>
    public const string NoDataReason =
        "no extracted game data: set CSVM_DATA_ROOT to a checkout holding extracted/ (or to the extraction tree itself)";

    static TestData()
    {
        RepoRoot = FindRepoRoot(AppContext.BaseDirectory);
        ExtractedRoot = FindExtracted();
        DataRoot = ExtractedRoot == null ? null : Directory.GetParent(ExtractedRoot)?.FullName;
    }

    /// <summary>The repo checkout this assembly was built from.</summary>
    public static string RepoRoot { get; }

    /// <summary>The extraction tree (the folder holding <c>zrdr.zip</c>, <c>C1</c>, …), or null
    /// when no install is reachable.</summary>
    public static string? ExtractedRoot { get; }

    /// <summary>The folder <c>extracted/</c> sits in — the "data root" the engine's
    /// <see cref="CSVM.SessionPaths"/> takes. Null when <see cref="ExtractedRoot"/> is.</summary>
    public static string? DataRoot { get; }

    /// <summary>Path of a committed fixture, e.g. <c>Fixture("zrdr")</c>.</summary>
    public static string Fixture(params string[] parts)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures");
        foreach (var part in parts)
        {
            path = Path.Combine(path, part);
        }
        return path;
    }

    /// <summary>A fresh empty directory under the OS temp tree, for tests that must build their
    /// input (a zip, a directory of names) rather than commit it.</summary>
    public static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "csvm-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    // Walks up from the build output until the engine solution appears; falls back to the
    // output directory so a relocated assembly still yields a usable (if fruitless) root.
    private static string FindRepoRoot(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CSVM", "CSVM.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return start;
    }

    // CSVM_DATA_ROOT names a checkout holding extracted/ (the engine's convention, so one env var
    // serves game and tests alike). Pointing it straight at an extraction tree also works, since
    // both shapes are probed; the repo root is the last resort.
    private static string? FindExtracted()
    {
        foreach (var candidate in new[] { Environment.GetEnvironmentVariable("CSVM_DATA_ROOT"), RepoRoot })
        {
            if (string.IsNullOrWhiteSpace(candidate) || !Directory.Exists(candidate))
            {
                continue;
            }
            var nested = Path.Combine(candidate, "extracted");
            if (IsExtraction(nested))
            {
                return Path.GetFullPath(nested);
            }
            if (IsExtraction(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }
        return null;
    }

    // The shared zrdr scope is present in every extraction, zipped or unpacked, and in no other
    // folder this walk can reach — so it is the cheapest unambiguous marker.
    private static bool IsExtraction(string dir) =>
        Directory.Exists(dir)
        && (File.Exists(Path.Combine(dir, "zrdr.zip")) || Directory.Exists(Path.Combine(dir, "zrdr")));
}

/// <summary>
/// A fact that needs the player's extracted install. When no extraction is reachable the test is
/// skipped with a reason, which the runner reports separately from passes: a suite that goes green
/// because it checked nothing is worse than one that fails.
/// </summary>
public sealed class ExtractedDataFactAttribute : FactAttribute
{
    public ExtractedDataFactAttribute()
    {
        if (TestData.ExtractedRoot == null)
        {
            Skip = TestData.NoDataReason;
        }
    }
}

/// <summary>The data-driven twin of <see cref="ExtractedDataFactAttribute"/>. Setting
/// <c>Skip</c> on the theory skips the whole set without enumerating its rows, so an absent
/// install never turns into a row-level failure.</summary>
public sealed class ExtractedDataTheoryAttribute : TheoryAttribute
{
    public ExtractedDataTheoryAttribute()
    {
        if (TestData.ExtractedRoot == null)
        {
            Skip = TestData.NoDataReason;
        }
    }
}
