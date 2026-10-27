using System.Collections.Concurrent;
using System.IO;
using CSVM.Extraction;
using CSVM.Mech3;
using CSVM.Spec;

namespace CSVM.Tests;

/// <summary>
/// Reader scopes for the unit tests, read from the extraction or a fixture folder through
/// <see cref="ZrdrSet.FromExtracted"/>, the modding path. An extraction's scope is read once per
/// run and shared, which is safe because every read off a scope hands out a fresh copy. A test
/// that needs one carries <see cref="ExtractedDataFactAttribute"/>, as it did when it took a path.
/// </summary>
public static class TestZrdr
{
    private static readonly ConcurrentDictionary<(string? Chapter, string? Mission), ZrdrSet> Read = new();

    /// <summary>The extraction's root scope, <c>extracted/zrdr.zip</c> or its unpacked folder.</summary>
    public static ZrdrSet Root =>
        Read.GetOrAdd((null, null), _ => ZrdrSet.FromExtracted(SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "zrdr.zip"))));

    /// <summary>The extraction's scope of <paramref name="chapter"/>.</summary>
    public static ZrdrSet Chapter(string chapter) =>
        Read.GetOrAdd((chapter, null), _ => ZrdrSet.FromExtracted(SessionPaths.ChapterZrdr(TestData.DataRoot!, chapter)));

    /// <summary>The extraction's scope of <paramref name="chapter"/>'s <paramref name="mission"/>.</summary>
    public static ZrdrSet Mission(string chapter, string mission) =>
        Read.GetOrAdd((chapter, mission), _ => ZrdrSet.FromExtracted(SessionPaths.MissionZrdr(TestData.DataRoot!, chapter, mission)));

    /// <summary>The scope of a fixture or temp folder, read afresh, since a test may write to it.</summary>
    public static ZrdrSet Folder(string path) => ZrdrSet.FromExtracted(path);

    /// <summary>The scope of the committed fixture folder <paramref name="parts"/>.</summary>
    public static ZrdrSet Fixture(params string[] parts) => ZrdrSet.FromExtracted(TestData.Fixture(parts));

    /// <summary><paramref name="data"/> with the extraction's reader scopes in its root and chapter
    /// slots, for a screen test that reads them; its other models are kept.</summary>
    public static GameData Readers(GameData data) => new()
    {
        Interp = data.Interp,
        Messages = data.Messages,
        Rimage = data.Rimage,
        Planes = data.Planes,
        Sounds = data.Sounds,
        Zrdr = Root,
        LoadChapter = (chapter, mission) =>
        {
            var held = data.LoadChapter(chapter, mission);
            return new ChapterData
            {
                Chapter = chapter,
                Mission = mission,
                TexturesSource = held.TexturesSource,
                GamezSource = held.GamezSource,
                ChapterAnimSource = held.ChapterAnimSource,
                MissionAnimSource = held.MissionAnimSource,
                ChapterZrdrSource = new(() => Chapter(chapter)),
                MissionZrdrSource = new(() => Mission(chapter, mission)),
            };
        },
    };
}
