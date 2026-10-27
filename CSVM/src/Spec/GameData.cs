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

    /// <summary>The install path of the aircraft archive.</summary>
    public const string PlanesPath = "ZBD/planes.zbd";

    /// <summary>The install path of the sound archive the engine plays, the high-quality one.</summary>
    public const string SoundsPath = "ZBD/soundsh.zbd";

    /// <summary>The install path of the root reader archive, the scope every chapter shares.</summary>
    public const string RootZrdrPath = "ZBD/zrdr.zbd";

    /// <summary>No install: no boot scripts, an empty message table, bare chapters.</summary>
    public static readonly GameData Empty = new();

    /// <summary>The boot scripts of <c>interp.zbd</c>, null without them.</summary>
    public InterpScripts? Interp { get; init; }

    /// <summary>The message table of <c>strings.dll</c>, empty without it.</summary>
    public Messages Messages { get; init; } = new();

    /// <summary>The HUD and menu art of <c>rimage.zbd</c>, empty without it.</summary>
    public TextureSet Rimage { get; init; } = TextureSet.Empty;

    /// <summary>The aircraft models of <c>planes.zbd</c>, null without it. Shared by every session
    /// of the launch, so read-only by the contract <see cref="DecodeCache"/> states.</summary>
    public GameZ? Planes { get; init; }

    /// <summary>The WAVs of <c>soundsh.zbd</c>, null without it. Opened once per launch and never
    /// closed; each session wraps it in its own <see cref="SoundArchive"/>, whose dispose leaves it
    /// open.</summary>
    public SoundZbd? Sounds { get; init; }

    /// <summary>The root readers of <c>zrdr.zbd</c>, <see cref="ZrdrSet.None"/> without it. Shared
    /// by every session; each read hands out a fresh copy.</summary>
    public ZrdrSet Zrdr { get; init; } = ZrdrSet.None;

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
        Planes = Read(game, PlanesPath, () => GameZZbd.Load(game, PlanesPath), null),
        Sounds = Read(game, SoundsPath, () => SoundZbd.Load(game, SoundsPath), null),
        Zrdr = Read(game, RootZrdrPath, () => ZrdrSet.Load(game, RootZrdrPath), ZrdrSet.None),
        LoadChapter = Chapters(game),
    };

    // The chapter loader of an install. The last chapter's textures, world and readers are held,
    // each decoded on its first read, so a caller pays only for what it reads.
    // ⚠ Both anim archives are held for the last mission only. AnimProgram.Load writes a reader
    // def's PERSIST_LOG onto its compiled twin, so a chapter archive shared across missions would
    // carry one mission's flags into the next. A reader scope copies on every read, so it is shared.
    private static Func<string, string, ChapterData> Chapters(IGameFileSource? game)
    {
        var gate = new object();
        string? heldChapter = null, heldMission = null;
        Lazy<TextureSet>? heldTextures = null;
        Lazy<GameZ?>? heldGamez = null;
        Lazy<AnimArchive?>? heldChapterAnim = null, heldMissionAnim = null;
        Lazy<ZrdrSet>? heldChapterZrdr = null, heldMissionZrdr = null;
        return (chapter, mission) =>
        {
            lock (gate)
            {
                bool newChapter = heldGamez == null || !string.Equals(heldChapter, chapter, StringComparison.OrdinalIgnoreCase);
                if (newChapter)
                {
                    heldTextures = new Lazy<TextureSet>(() => Read(game, $"ZBD/{chapter}", () => TextureSet.ForChapter(game, chapter), TextureSet.Empty));
                    string gamezPath = $"ZBD/{chapter}/gamez.zbd";
                    heldGamez = new Lazy<GameZ?>(() => Read(game, gamezPath, () => GameZZbd.Load(game, gamezPath), null));
                    heldChapterZrdr = ReaderScope(game, $"ZBD/{chapter}/zrdr.zbd");
                    heldChapter = chapter;
                }

                if (newChapter || !string.Equals(heldMission, mission, StringComparison.OrdinalIgnoreCase))
                {
                    heldChapterAnim = Anim(game, $"ZBD/{chapter}/cam_anim.zbd", "cam_anim");
                    heldMissionAnim = Anim(game, $"ZBD/{chapter}/{mission}/mis_anim.zbd", "mis_anim");
                    heldMissionZrdr = ReaderScope(game, $"ZBD/{chapter}/{mission}/zrdr.zbd");
                    heldMission = mission;
                }

                return new ChapterData
                {
                    Chapter = chapter,
                    Mission = mission,
                    TexturesSource = heldTextures,
                    GamezSource = heldGamez,
                    ChapterAnimSource = heldChapterAnim,
                    MissionAnimSource = heldMissionAnim,
                    ChapterZrdrSource = heldChapterZrdr,
                    MissionZrdrSource = heldMissionZrdr,
                };
            }
        };
    }

    // The label is the one AnimProgram.Load and the anim log lines know the archive by.
    private static Lazy<AnimArchive?> Anim(IGameFileSource? game, string path, string label) =>
        new(() => Read(game, path, () => AnimZbd.Load(game, path, label), null));

    private static Lazy<ZrdrSet> ReaderScope(IGameFileSource? game, string path) =>
        new(() => Read(game, path, () => ZrdrSet.Load(game, path), ZrdrSet.None));

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
    /// skins and world textures, decoded on first read; empty without one.</summary>
    public TextureSet Textures => TexturesSource?.Value ?? TextureSet.Empty;

    /// <summary>The decode behind <see cref="Textures"/>, one per chapter a loader holds.</summary>
    public Lazy<TextureSet>? TexturesSource { get; init; }

    /// <summary>The chapter's world, <c>gamez.zbd</c>, decoded on first read; null without one.
    /// Shared like <see cref="GameData.Planes"/>, so read-only.</summary>
    public GameZ? Gamez => GamezSource?.Value;

    /// <summary>The decode behind <see cref="Gamez"/>, one per chapter a loader holds; null for no world.</summary>
    public Lazy<GameZ?>? GamezSource { get; init; }

    /// <summary>The chapter's compiled anims, <c>cam_anim.zbd</c>, decoded on first read; null
    /// without one. Shared like <see cref="Gamez"/>, so read-only.</summary>
    public AnimArchive? ChapterAnim => ChapterAnimSource?.Value;

    /// <summary>The decode behind <see cref="ChapterAnim"/>, held for the last mission a loader read.</summary>
    public Lazy<AnimArchive?>? ChapterAnimSource { get; init; }

    /// <summary>The mission's compiled anims, <c>mis_anim.zbd</c>, decoded on first read; null for
    /// a mission without one. Shared like <see cref="Gamez"/>, so read-only.</summary>
    public AnimArchive? MissionAnim => MissionAnimSource?.Value;

    /// <summary>The decode behind <see cref="MissionAnim"/>, held for the last mission a loader read.</summary>
    public Lazy<AnimArchive?>? MissionAnimSource { get; init; }

    /// <summary>The chapter's readers, <c>ZBD/&lt;chapter&gt;/zrdr.zbd</c>, decoded on first read;
    /// <see cref="ZrdrSet.None"/> without one. Each read hands out a fresh copy.</summary>
    public ZrdrSet ChapterZrdr => ChapterZrdrSource?.Value ?? ZrdrSet.None;

    /// <summary>The decode behind <see cref="ChapterZrdr"/>, one per chapter a loader holds.</summary>
    public Lazy<ZrdrSet>? ChapterZrdrSource { get; init; }

    /// <summary>The mission's readers, <c>ZBD/&lt;chapter&gt;/&lt;mission&gt;/zrdr.zbd</c>, decoded
    /// on first read; <see cref="ZrdrSet.None"/> without one.</summary>
    public ZrdrSet MissionZrdr => MissionZrdrSource?.Value ?? ZrdrSet.None;

    /// <summary>The decode behind <see cref="MissionZrdr"/>, held for the last mission a loader read.</summary>
    public Lazy<ZrdrSet>? MissionZrdrSource { get; init; }

    /// <summary>A chapter part with no models loaded.</summary>
    public static ChapterData Bare(string chapter, string mission) => new() { Chapter = chapter, Mission = mission };
}
