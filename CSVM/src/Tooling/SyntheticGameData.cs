using CSVM.Spec;

namespace CSVM.Tooling;

/// <summary>
/// The game data a <c>--synthetic-data</c> run reads in place of an install's, every model built in
/// memory from records written in code. There is no game-file source behind it, so nothing in the
/// run can reach a real install. A format switched to <see cref="GameData"/> adds its builder here.
/// Engine-free, so <c>CSVM.Tests</c> builds the same models the engine does.
/// </summary>
public static class SyntheticGameData
{
    /// <summary>The invented models. No boot scripts, as the tree never carried any.</summary>
    public static GameData Build() => new()
    {
        Messages = SyntheticMessages.Table(),
        Planes = SyntheticPlane.Gamez(),
        Sounds = SyntheticSounds.Zbd(),
        Zrdr = SyntheticZrdr.Root(),
        LoadChapter = (chapter, mission) => new ChapterData
        {
            Chapter = chapter,
            Mission = mission,
            TexturesSource = new(() => SyntheticTextures.For(chapter)),
            ChapterZrdrSource = new(() => SyntheticZrdr.Chapter(chapter)),
            MissionZrdrSource = new(() => SyntheticZrdr.Mission(chapter, mission)),
        },
    };
}
