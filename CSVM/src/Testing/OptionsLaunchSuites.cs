using System.IO;
using CSVM.Flight;
using CSVM.Session;
using CSVM.Utils;

namespace CSVM.Testing;

/// <summary>The saved difficulty at launch, through the real options store: a saved word reaches
/// the roster policy a plain launch builds, the <c>--difficulty=</c> flag outranks it, a
/// <c>--det</c> run never reads it, and a word the store refuses leaves the default standing.
/// ⚠ The store is pointed at this suite's own scratch directory and restored in a finally, so
/// nothing here reads or writes the options saved at this machine's controls.</summary>
internal static class OptionsLaunchSuites
{
    [Suite("options-difficulty-launch",
        "The saved difficulty at launch: a saved hard reaches FlightRosterPolicy.Difficulty on a plain "
        + "launch, --difficulty=normal on the command line keeps Normal over the same saved word, a "
        + "--det run reads no saved option, a saved hardest reaches the policy as Hardest, and a word "
        + "outside the store's set loads as never set so the launch keeps the default")]
    internal static void OptionsDifficultyLaunch(TestContext ctx)
    {
        string dir = Path.Combine(ctx.ScratchDir, "options-difficulty-launch");
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        Directory.CreateDirectory(dir);
        string? previous = OptionsStore.DirectoryOverride;
        OptionsStore.DirectoryOverride = dir;
        try
        {
            OptionsStore.UserOptions().Save(new OptionsDef { Difficulty = Difficulty.Word(Difficulty.Hard) });
            ctx.Check(Launched(System.Array.Empty<string>()) == Difficulty.Hard,
                $"a saved hard reaches the roster policy on a plain launch ({Launched(System.Array.Empty<string>())})");
            ctx.Check(Launched(new[] { "--difficulty=normal" }) == Difficulty.Normal,
                $"--difficulty=normal keeps Normal over the saved hard ({Launched(new[] { "--difficulty=normal" })})");
            ctx.Check(Launched(new[] { "--difficulty=hardest" }) == Difficulty.Hardest,
                $"and --difficulty=hardest keeps Hardest over it ({Launched(new[] { "--difficulty=hardest" })})");
            ctx.Check(Launched(new[] { "--det" }) == Difficulty.Normal,
                $"a --det run reads no saved option and flies Normal ({Launched(new[] { "--det" })})");

            OptionsStore.UserOptions().Save(new OptionsDef { Difficulty = Difficulty.Word(Difficulty.Hardest) });
            ctx.Check(Launched(System.Array.Empty<string>()) == Difficulty.Hardest,
                $"a saved hardest reaches the policy as Hardest ({Launched(System.Array.Empty<string>())})");

            // Instant Action's name for the same tier is the flag's convenience, not a word the file
            // carries: written by hand, it loads as never set and the launch keeps the default.
            File.WriteAllText(Path.Combine(dir, "options.json"),
                "{\"version\": 1, \"difficulty\": \"veteran\"}", new System.Text.UTF8Encoding(false));
            ctx.Check(OptionsStore.UserOptions().Load().Difficulty == null && Launched(System.Array.Empty<string>()) == Difficulty.Normal,
                $"a word outside the store's set loads as never set and the launch keeps Normal ({Launched(System.Array.Empty<string>())})");
        }
        finally
        {
            OptionsStore.DirectoryOverride = previous;
        }
    }

    // The launch's own read: the spec a command line parses to, the saved word folded in the way
    // Launcher.LaunchSession folds it, then the policy the roster is built from.
    private static int Launched(string[] args)
    {
        var spec = SessionSpec.Parse(args).WithSavedDifficulty(OptionsStore.UserOptions().Load().Difficulty);
        return FlightRosterPolicy.From(spec).Difficulty;
    }
}
