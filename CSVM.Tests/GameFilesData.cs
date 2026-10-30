using System;
using CSVM.Extraction;
using Xunit;

namespace CSVM.Tests;

/// <summary>The original's files for tests that read them in place, found by
/// <see cref="GameFileSource.Locate"/> as the launcher finds them. The override is
/// <c>CSVM_INSTALL_ROOT</c>, the data root is <c>CSVM_DATA_ROOT</c> or the repo root, and the
/// machine's candidate places follow. There is no remembered install, which lives in the engine's
/// user folder. <see cref="Source"/> is null when nothing is found.</summary>
public static class GameFilesData
{
    /// <summary>Printed by every skipped game-file test so an absent install never reads as a pass.</summary>
    public const string NoSourceReason =
        "no game install: set CSVM_DATA_ROOT to a checkout holding CrimsonSkiesGame/, or CSVM_INSTALL_ROOT to the install";

    /// <summary>The located install, or null.</summary>
    public static IGameFileSource? Source { get; } = GameFileSource.Locate(
        Environment.GetEnvironmentVariable(GameFileSource.InstallVariable),
        Environment.GetEnvironmentVariable("CSVM_DATA_ROOT") is { Length: > 0 } dataRoot ? dataRoot : TestData.RepoRoot,
        remembered: null,
        InstallSearchRoots.ForThisMachine());
}

/// <summary>A fact that reads the original's files through <see cref="GameFilesData.Source"/>,
/// skipped with a reason when no install is found. The counterpart of
/// <see cref="ExtractedDataFactAttribute"/> for tests that read the install rather than an
/// extraction.</summary>
public sealed class GameFilesFactAttribute : FactAttribute
{
    public GameFilesFactAttribute()
    {
        if (GameFilesData.Source == null)
        {
            Skip = GameFilesData.NoSourceReason;
        }
    }
}

/// <summary>The data-driven twin of <see cref="GameFilesFactAttribute"/>; a skip covers the whole
/// set without enumerating its rows.</summary>
public sealed class GameFilesTheoryAttribute : TheoryAttribute
{
    public GameFilesTheoryAttribute()
    {
        if (GameFilesData.Source == null)
        {
            Skip = GameFilesData.NoSourceReason;
        }
    }

    /// <summary>Set for a theory that compares the install against today's extraction, which then
    /// skips without one too.</summary>
    public bool AlsoExtracted
    {
        get => TestData.ExtractedRoot != null;
        set
        {
            if (value && Skip == null && TestData.ExtractedRoot == null)
            {
                Skip = TestData.NoDataReason;
            }
        }
    }
}
