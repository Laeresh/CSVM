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
    /// <summary>The install path of the HUD and menu art archive.</summary>
    public const string RimagePath = "ZBD/rimage.zbd";

    /// <summary>No install: no boot scripts, an empty message table, bare chapters.</summary>
    public static readonly GameData Empty = new();

    /// <summary>The boot scripts of <c>interp.zbd</c>, null without them.</summary>
    public InterpScripts? Interp { get; init; }

    /// <summary>The message table of <c>strings.dll</c>, empty without it.</summary>
    public Messages Messages { get; init; } = new();

    /// <summary>The HUD and menu art of <c>rimage.zbd</c>, empty without it.</summary>
    public TextureSet Rimage { get; init; } = TextureSet.Empty;

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
        Rimage = Read(game, RimagePath, () => TextureSet.Load(game, RimagePath), TextureSet.Empty),
        LoadChapter = Chapters(game),
    };

    // The chapter loader of an install. The last chapter's textures are held, so the sessions and
    // suites that rebuild one chapter decode its archive once.
    private static Func<string, string, ChapterData> Chapters(IGameFileSource? game)
    {
        var gate = new object();
        string? heldChapter = null;
        var held = TextureSet.Empty;
        return (chapter, mission) =>
        {
            lock (gate)
            {
                if (!string.Equals(heldChapter, chapter, StringComparison.OrdinalIgnoreCase))
                {
                    held = Read(game, $"ZBD/{chapter}", () => TextureSet.ForChapter(game, chapter), TextureSet.Empty);
                    heldChapter = chapter;
                }

                return new ChapterData { Chapter = chapter, Mission = mission, Textures = held };
            }
        };
    }

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
/// <see cref="GameData.LoadChapter"/>.</summary>
public sealed class ChapterData
{
    /// <summary>The chapter folder, <c>C1</c>.</summary>
    public required string Chapter { get; init; }

    /// <summary>The mission folder, <c>M01</c>.</summary>
    public required string Mission { get; init; }

    /// <summary>The chapter's texture archive (<see cref="TextureSet.ForChapter"/>), the plane
    /// skins and world textures; empty without one.</summary>
    public TextureSet Textures { get; init; } = TextureSet.Empty;

    /// <summary>A chapter part with no models loaded.</summary>
    public static ChapterData Bare(string chapter, string mission) => new() { Chapter = chapter, Mission = mission };
}
