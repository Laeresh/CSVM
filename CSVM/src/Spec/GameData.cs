using System;
using System.IO;
using CSVM.Extraction;
using CSVM.Mech3;
using CSVM.Utils;

namespace CSVM.Spec;

/// <summary>
/// The models a launch reads out of the original's files, handed to every session, screen, suite
/// and probe in place of an extracted path. This chapter-independent part loads once per launch,
/// in <see cref="BasePaths.Resolve"/>. The per-chapter part loads per session through
/// <see cref="LoadChapter"/>. <see cref="Load"/> builds both from the install, and
/// <c>Tooling/SyntheticGameData.cs</c> builds them in memory for a synthetic run.
/// Module notes: docs/architecture/Spec.md.
/// </summary>
public sealed class GameData
{
    /// <summary>No install: no boot scripts, an empty message table, bare chapters.</summary>
    public static readonly GameData Empty = new();

    /// <summary>The boot scripts of <c>interp.zbd</c>, null without them.</summary>
    public InterpScripts? Interp { get; init; }

    /// <summary>The message table of <c>strings.dll</c>, empty without it.</summary>
    public Messages Messages { get; init; } = new();

    /// <summary>Builds the per-chapter part for a chapter and mission folder (<c>C1</c>,
    /// <c>M01</c>). Each factory sets its own.</summary>
    public Func<string, string, ChapterData> LoadChapter { get; init; } = ChapterData.Bare;

    /// <summary>The chapter-independent part read through <paramref name="game"/>; a null source
    /// gives what <see cref="Empty"/> holds. A file that does not decode is logged and read as
    /// absent, so a damaged install still reaches the menu.</summary>
    public static GameData Load(IGameFileSource? game) => new()
    {
        Interp = Read(game, InterpScripts.GamePath, () => InterpScripts.Load(game), null),
        Messages = Read(game, Messages.GameFile, () => Messages.Load(game), new Messages()),
    };

    private static T Read<T>(IGameFileSource? game, string path, Func<T> load, T absent)
    {
        try
        {
            return load();
        }
        catch (Exception e) when (e is InvalidDataException or IOException)
        {
            Log.Error("core", $"{path} in {game?.Location} does not read, using none: {e.Message}");
            return absent;
        }
    }
}

/// <summary>The models one session's chapter and mission read, built by
/// <see cref="GameData.LoadChapter"/>. It names its scope only until a per-chapter format adds
/// its slot.</summary>
public sealed class ChapterData
{
    /// <summary>The chapter folder, <c>C1</c>.</summary>
    public required string Chapter { get; init; }

    /// <summary>The mission folder, <c>M01</c>.</summary>
    public required string Mission { get; init; }

    /// <summary>A chapter part with no models loaded.</summary>
    public static ChapterData Bare(string chapter, string mission) => new() { Chapter = chapter, Mission = mission };
}
