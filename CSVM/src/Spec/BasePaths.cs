using CSVM.Extraction;

namespace CSVM.Spec;

/// <summary>
/// The chapter-independent places a launch reads: the repo root, the data root, the game-file
/// source and the game data read through it. Each session receives them through the launcher's
/// context and loads its chapter's part from <see cref="Data"/>. The probe runner holds the same
/// value. Module notes: docs/architecture/Spec.md.
/// </summary>
public sealed record BasePaths(string RepoRoot, string DataRoot)
{
    /// <summary>Where the original's own files are read from, or null when no install was found.</summary>
    public IGameFileSource? Game { get; init; }

    /// <summary>The models read from <see cref="Game"/>, or built in memory for a synthetic run.
    /// Every consumer of a switched format reads its model here.</summary>
    public GameData Data { get; init; } = GameData.Empty;

    /// <summary>The game-file source by <see cref="GameFileSource.Locate"/>'s order, and the game
    /// data read through it. A synthetic run's data comes from <paramref name="synthetic"/>, a
    /// family above this one.</summary>
    public static BasePaths Resolve(string repoRoot, string dataRoot, SessionSpec spec, System.Func<GameData> synthetic)
    {
        // A deterministic run ignores saved options, the remembered install among them. A
        // synthetic run reads no install at all, or its result would depend on the machine.
        var game = spec.SyntheticData ? null : GameFileSource.Locate(
            System.Environment.GetEnvironmentVariable(GameFileSource.InstallVariable),
            dataRoot,
            spec.Det ? null : RememberedInstall.Get(),
            InstallSearchRoots.ForThisMachine());
        return new BasePaths(repoRoot, dataRoot)
        {
            Game = game,
            Data = spec.SyntheticData ? synthetic() : GameData.Load(game, ModdingFolder.Choose(spec, static () => Utils.OptionsStore.UserOptions().Load().ModFolder)),
        };
    }
}
