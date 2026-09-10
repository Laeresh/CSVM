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

    /// <summary>Printed by every skipped cinema test. The movies are not part of an extraction:
    /// they sit loose in the retail install, so they are found by their own env var or by the
    /// install path under a data root.</summary>
    public const string NoMoviesReason =
        "no cinema files: set CSVM_MPG_ROOT to the install's GOSDATA/ASSETS/GRAPHICS/MPG folder";

    /// <summary>Printed by every skipped whole-file walk, so a run that decoded ten prefixes
    /// instead of ten whole cinemas says so rather than reading as the stronger check.</summary>
    public const string NoFullWalkReason =
        "the whole-file video walk is opt-in and costs about a minute: run "
        + "$env:CSVM_MOVIE_WALK=1; .\\RunTests.ps1";

    // Where the ten .mpg cinemas sit inside the retail install, relative to a data root.
    private static readonly string[] MoviePathParts =
        { "CrimsonSkiesGame", "GOSDATA", "ASSETS", "GRAPHICS", "MPG" };

    static TestData()
    {
        RepoRoot = FindRepoRoot(AppContext.BaseDirectory);
        ExtractedRoot = FindExtracted();
        DataRoot = ExtractedRoot == null ? null : Directory.GetParent(ExtractedRoot)?.FullName;
        MovieRoot = FindMovies();
        FullMovieWalk = IsSet(Environment.GetEnvironmentVariable("CSVM_MOVIE_WALK"));
    }

    /// <summary>The repo checkout this assembly was built from.</summary>
    public static string RepoRoot { get; }

    /// <summary>The extraction tree (the folder holding <c>zrdr.zip</c>, <c>C1</c>, …), or null
    /// when no install is reachable.</summary>
    public static string? ExtractedRoot { get; }

    /// <summary>The folder <c>extracted/</c> sits in — the "data root" the engine's
    /// <see cref="CSVM.SessionPaths"/> takes. Null when <see cref="ExtractedRoot"/> is.</summary>
    public static string? DataRoot { get; }

    /// <summary>The install folder holding the ten <c>.mpg</c> cinemas, or null when none is
    /// reachable.</summary>
    public static string? MovieRoot { get; }

    /// <summary>Whether the whole-file video walk was asked for. Decoding all ten cinemas to the
    /// last frame costs about a minute of the unit stage's 30 second budget, which is why it is
    /// opt-in rather than on by default; what runs every time is the per-file header and prefix
    /// check, which is what catches a file whose profile is not what this project thinks it is.
    /// The opt-in is an environment variable rather than a test filter so that the ordinary
    /// <c>.\RunTests.ps1</c> command still runs it once the caller has set the variable.</summary>
    public static bool FullMovieWalk { get; }

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

    // CSVM_MPG_ROOT names the folder itself; every other candidate is a data root the install
    // path hangs under, which is the same shape the engine resolves game files through.
    private static string? FindMovies()
    {
        string? direct = Environment.GetEnvironmentVariable("CSVM_MPG_ROOT");
        if (!string.IsNullOrWhiteSpace(direct) && HasMovies(direct))
        {
            return Path.GetFullPath(direct);
        }

        foreach (var root in new[] { DataRoot, Environment.GetEnvironmentVariable("CSVM_DATA_ROOT"), RepoRoot })
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            string candidate = root;
            foreach (var part in MoviePathParts)
            {
                candidate = Path.Combine(candidate, part);
            }

            if (HasMovies(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    // The looping front-end background is in every install and named the same way in all of
    // them, so its presence is what says this folder is the cinema folder.
    private static bool HasMovies(string dir) =>
        Directory.Exists(dir) && File.Exists(Path.Combine(dir, "crimflag.mpg"));

    // An opt-in variable is on for any value but the two spellings of off, so that setting it to
    // 1, to yes or to the path of the tree all mean the same thing.
    private static bool IsSet(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && !string.Equals(value, "0", StringComparison.Ordinal)
        && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
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

/// <summary>A fact that needs the install's cinema files. They are not in an extraction, so
/// they have their own probe and their own reason; see <see cref="TestData.MovieRoot"/>.</summary>
public sealed class MovieDataFactAttribute : FactAttribute
{
    public MovieDataFactAttribute()
    {
        if (TestData.MovieRoot == null)
        {
            Skip = TestData.NoMoviesReason;
        }
    }
}

/// <summary>The data-driven twin of <see cref="MovieDataFactAttribute"/>.</summary>
public sealed class MovieDataTheoryAttribute : TheoryAttribute
{
    public MovieDataTheoryAttribute()
    {
        if (TestData.MovieRoot == null)
        {
            Skip = TestData.NoMoviesReason;
        }
    }
}

/// <summary>A theory that decodes whole cinemas rather than their opening frames. It needs the
/// install <see cref="MovieDataTheoryAttribute"/> needs and, on top of that,
/// <see cref="TestData.FullMovieWalk"/>, because the ten walks cost more than twice the unit
/// stage's whole wall-time budget. Skipping prints which of the two is missing, so a run that
/// took the cheap path cannot be read as having taken the expensive one.</summary>
public sealed class FullMovieWalkTheoryAttribute : TheoryAttribute
{
    public FullMovieWalkTheoryAttribute()
    {
        if (TestData.MovieRoot == null)
        {
            Skip = TestData.NoMoviesReason;
        }
        else if (!TestData.FullMovieWalk)
        {
            Skip = TestData.NoFullWalkReason;
        }
    }
}
