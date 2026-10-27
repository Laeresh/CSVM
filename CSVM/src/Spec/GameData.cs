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

    /// <summary>The UI half, <c>crimson.rof</c> and its siblings, read in place on first lookup;
    /// <see cref="RofFiles.Empty"/> without it. Never read from a modding folder.</summary>
    public RofFiles Rof { get; init; } = RofFiles.Empty;

    /// <summary>Builds the per-chapter part for a chapter and mission folder (<c>C1</c>,
    /// <c>M01</c>). Each factory sets its own.</summary>
    public Func<string, string, ChapterData> LoadChapter { get; init; } = ChapterData.Bare;

    /// <summary>The modding folder <see cref="Load"/> read archives from, absolute; null for none.</summary>
    public string? ModFolder { get; init; }

    /// <summary>The chapter-independent part read through <paramref name="game"/>; a null source
    /// gives what <see cref="Empty"/> holds. Where <paramref name="modFolder"/> holds an archive's
    /// unzbd output, that output is read instead (<see cref="ModdingFolder"/>). A file that does not
    /// decode is logged and read as absent, so a damaged install still reaches the menu.</summary>
    public static GameData Load(IGameFileSource? game, string? modFolder = null)
    {
        var mods = Mods(modFolder);
        return new()
        {
            Interp = Read(game, mods, InterpScripts.GamePath, () => InterpScripts.Load(game), InterpScripts.FromExtracted, null),
            Messages = Read(game, mods, Messages.GameFile, () => Messages.Load(game), Messages.FromExtracted, new Messages()),
            Rimage = Read(game, mods, RimagePath, () => TextureSet.Load(game, RimagePath), o => TextureSet.FromExtracted(RimagePath, o), TextureSet.Empty),
            Planes = Read(game, mods, PlanesPath, () => GameZZbd.Load(game, PlanesPath), GameZ.Load, null),
            Sounds = Read(game, mods, SoundsPath, () => SoundZbd.Load(game, SoundsPath), SoundZbd.FromExtracted, null),
            Zrdr = ReaderScope(game, mods, RootZrdrPath).Value,
            Rof = RofFiles.Open(game),
            LoadChapter = Chapters(game, mods),
            ModFolder = mods?.Root,
        };
    }

    private static ModdingFolder? Mods(string? modFolder)
    {
        if (modFolder == null)
        {
            return null;
        }

        var mods = new ModdingFolder(modFolder);
        if (Directory.Exists(mods.Root))
        {
            Log.Info("core", $"modding folder {mods.Root}: its unzbd output replaces the game files archive by archive");
        }
        else
        {
            Log.Warn("core", $"modding folder {mods.Root} does not exist, reading the game files alone");
        }

        return mods;
    }

    // The chapter loader of an install. The last chapter's textures, world and readers are held,
    // each decoded on its first read, so a caller pays only for what it reads.
    // ⚠ Both anim archives are held for the last mission only. AnimProgram.Load writes a reader
    // def's PERSIST_LOG onto its compiled twin, so a chapter archive shared across missions would
    // carry one mission's flags into the next. A reader scope copies on every read, so it is shared.
    private static Func<string, string, ChapterData> Chapters(IGameFileSource? game, ModdingFolder? mods)
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
                    heldTextures = new Lazy<TextureSet>(() => ChapterTextures(game, mods, chapter));
                    string gamezPath = $"ZBD/{chapter}/gamez.zbd";
                    heldGamez = new Lazy<GameZ?>(() => Read(game, mods, gamezPath, () => GameZZbd.Load(game, gamezPath), GameZ.Load, null));
                    heldChapterZrdr = ReaderScope(game, mods, $"ZBD/{chapter}/zrdr.zbd");
                    heldChapter = chapter;
                }

                if (newChapter || !string.Equals(heldMission, mission, StringComparison.OrdinalIgnoreCase))
                {
                    heldChapterAnim = Anim(game, mods, $"ZBD/{chapter}/cam_anim.zbd", "cam_anim");
                    heldMissionAnim = Anim(game, mods, $"ZBD/{chapter}/{mission}/mis_anim.zbd", "mis_anim");
                    heldMissionZrdr = ReaderScope(game, mods, $"ZBD/{chapter}/{mission}/zrdr.zbd");
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
    private static Lazy<AnimArchive?> Anim(IGameFileSource? game, ModdingFolder? mods, string path, string label) =>
        new(() => Read(
            game,
            mods,
            path,
            () => AnimZbd.Load(game, path, label),
            output => AnimArchive.Load(output, label) ?? throw new InvalidDataException("the archive does not read"),
            null));

    private static Lazy<ZrdrSet> ReaderScope(IGameFileSource? game, ModdingFolder? mods, string path) =>
        new(() => Read(game, mods, path, () => ZrdrSet.Load(game, path), ZrdrSet.FromExtracted, ZrdrSet.None));

    // The tier the game files pick (TextureSet.ForChapter), so a modded chapter keeps the original's.
    private static TextureSet ChapterTextures(IGameFileSource? game, ModdingFolder? mods, string chapter) =>
        TextureSet.ChapterArchive(game, chapter) is { } path
            ? Read(game, mods, path, () => TextureSet.Load(game, path), output => TextureSet.FromExtracted(path, output), TextureSet.Empty)
            : TextureSet.Empty;

    // The modding folder's output for the path when it holds one and it reads, else the game file.
    // ⚠ The output is a modder's own files: any failure falls back, never takes the launch down.
    private static T Read<T>(IGameFileSource? game, ModdingFolder? mods, string path, Func<T> load, Func<string, T> modded, T absent)
    {
        if (mods?.Find(path) is { } output)
        {
            try
            {
                var model = modded(output);
                Log.Info("core", $"modding folder: {path} read from {output}");
                return model;
            }
            catch (Exception e)
            {
                Log.Error("core", $"modding folder: {output} does not read, using the game file {path}: {e.GetType().Name}: {e.Message}");
            }
        }

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
