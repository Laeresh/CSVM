using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using CSVM.Extraction;
using CSVM.Utils;

namespace CSVM.UI.Screens;

/// <summary>Which of its views the game-file screen shows.</summary>
public enum SourceView
{
    /// <summary>No usable game files: the folder or image field, Use these files and Quit.</summary>
    Asking,

    /// <summary>A pick works: play from it in place, or copy it into the data root first.</summary>
    Found,

    /// <summary>The copy's target folder exists: replace it, or go back.</summary>
    Replace,

    /// <summary>The copy into the data root is running: count, bar and the file being copied.</summary>
    Copying,

    /// <summary>The copy failed; its reason, with retry and a way on without it.</summary>
    CopyFailed,

    /// <summary>The data root holds an old <c>extracted</c> folder: keep it or delete it.</summary>
    Upgrade,

    /// <summary>The delete asks once more before it starts.</summary>
    ConfirmDelete,

    /// <summary>The old <c>extracted</c> folder is being deleted.</summary>
    Deleting,

    /// <summary>Nothing more to ask; the launcher takes over.</summary>
    Done,
}

/// <summary>One button of a view, in the order the screen lays them out; the first is focused.</summary>
public sealed record SourceChoice(string Label, Action Press);

/// <summary>
/// The game-file screen's state, engine-free so a unit drives it from pick to copy to the upgrade
/// question. The copy and the delete run on a worker; their progress and outcome cross
/// to the main thread only through <see cref="Tick"/>. Every answer the screen remembers goes to
/// the options store it was given, and to none when it was given none (a scripted run). Rules:
/// <c>docs/architecture/UI.md</c>.
/// </summary>
public sealed class SourceFlow
{
    /// <summary>The folder under a data root that the retired extraction wrote.</summary>
    public const string ExtractedFolder = "extracted";

    private readonly OptionsStore? _store;
    private readonly bool _ask;
    private readonly Action<Action> _start;
    private readonly object _gate = new();

    // Written by the worker under _gate, read by Tick.
    private GameFileCopyProgress? _progress;
    private Outcome? _outcome;

    private CancellationTokenSource? _cancel;
    private bool _upgradeAnswered;

    /// <summary>Builds the flow over <paramref name="dataRoot"/>. With <paramref name="located"/>
    /// it starts at the first question owed; without, at the field, which starts as
    /// <paramref name="preFill"/>. <paramref name="ask"/> false asks neither question (a scripted or
    /// deterministic run). Answers and a working pick are saved to <paramref name="store"/>, when
    /// there is one. The workers run through <paramref name="start"/>, a background thread by default.</summary>
    public SourceFlow(string dataRoot, IGameFileSource? located, string preFill, OptionsStore? store, bool ask, Action<Action>? start = null)
    {
        DataRoot = dataRoot;
        InstallPath = preFill;
        Source = located;
        _store = store;
        _ask = ask;
        _start = start ?? StartThread;
        if (located != null)
        {
            Advance();
        }
    }

    /// <summary>Gets the data root the copy writes under and the old extraction sits in.</summary>
    public string DataRoot { get; }

    /// <summary>Gets why the located game files were refused, which the asking view names; null
    /// when none were found at all.</summary>
    public string? Refused { get; init; }

    /// <summary>Gets or sets the folder or image the field holds, which Use reads.</summary>
    public string InstallPath { get; set; }

    /// <summary>Gets the view to show.</summary>
    public SourceView View { get; private set; } = SourceView.Asking;

    /// <summary>Gets the game files the session reads: the located ones, a working pick, or its copy.</summary>
    public IGameFileSource? Source { get; private set; }

    /// <summary>Gets whether <see cref="Source"/> came from this screen, so the launcher rebinds.</summary>
    public bool Picked { get; private set; }

    /// <summary>Gets the line under the field or the buttons, null when there is nothing to say.
    /// It names why a pick is refused, why a delete failed, or a cancelled copy.</summary>
    public string? Notice { get; private set; }

    /// <summary>Gets the failed copy's reason, empty otherwise.</summary>
    public string Failure { get; private set; } = string.Empty;

    /// <summary>Gets the copy's fraction, 0 to 1.</summary>
    public double Fraction { get; private set; }

    /// <summary>Gets the running worker's progress line.</summary>
    public string ProgressLine { get; private set; } = string.Empty;

    /// <summary>Gets the files a finished copy found missing in the source, which the launcher logs.</summary>
    public IReadOnlyList<string> Missing { get; private set; } = Array.Empty<string>();

    /// <summary>Gets whether a cancel was asked for and the copy has not stopped yet.</summary>
    public bool Cancelling => View == SourceView.Copying && _cancel is { IsCancellationRequested: true };

    /// <summary>Gets the old extraction's folder under <see cref="DataRoot"/>.</summary>
    public string Extracted => Path.Combine(DataRoot, ExtractedFolder);

    /// <summary>Gets the folder the copy lands in.</summary>
    public string CopyTarget => GameFileCopy.Target(DataRoot);

    /// <summary>Gets the title of the view shown.</summary>
    public string Title => View switch
    {
        SourceView.Asking when Refused != null => "The game files cannot be read",
        SourceView.Asking => "Crimson Skies game files not found",
        SourceView.Found => "Game files found",
        SourceView.Replace => "Replace the existing copy?",
        SourceView.Copying => "Copying the game files",
        SourceView.CopyFailed => "The copy failed",
        SourceView.Upgrade => "Old extracted game data found",
        SourceView.ConfirmDelete => "Delete the old game data?",
        SourceView.Deleting => "Deleting the old game data",
        _ => string.Empty,
    };

    /// <summary>Gets the body of the view shown.</summary>
    public string Body => View switch
    {
        SourceView.Asking when Refused != null =>
            $"The menus cannot read the game files at {Refused}. Choose another copy of the game: an install folder, the game disc's drive, or an image of the disc. CSVM reads the files in place and never changes them.",
        SourceView.Asking =>
            "CSVM plays Crimson Skies from your own copy of the game. Choose your install folder, the game disc's drive, or an image of the disc (.iso, .cue or .ccd). CSVM reads the files in place and never changes them.",
        SourceView.Found when CopyOffered =>
            $"CSVM plays from '{Source!.Location}'. You can also copy the game files into '{CopyTarget}' (about 830 MB), so the original need not stay in place.",
        SourceView.Found => $"CSVM plays from '{Source!.Location}'.",
        SourceView.Replace =>
            $"'{CopyTarget}' already exists. Replacing it deletes that folder and everything in it, then copies the game files in again.",
        SourceView.Copying => $"Copying from '{Source!.Location}' into '{CopyTarget}'. The source is only read, never changed.",
        SourceView.CopyFailed => $"{Failure} The data folder is as it was before the copy.",
        SourceView.Upgrade =>
            $"'{Extracted}' holds game data an older CSVM extracted. This version reads the game files directly and does not use it. "
            + "Keep it to mod the game with: it is read again only when you choose it as the modding folder, on the Modding Folder row of Options' GAME OPTIONS page or with --mod-folder=. "
            + "Or delete it to free the space.",
        SourceView.ConfirmDelete =>
            $"This deletes '{Extracted}' and everything in it, and cannot be undone. A linked folder inside it is unlinked, never emptied. Nothing outside it is touched.",
        SourceView.Deleting => $"Deleting '{Extracted}'.",
        _ => string.Empty,
    };

    /// <summary>Gets the view's buttons, the default first.</summary>
    public IReadOnlyList<SourceChoice> Choices => View switch
    {
        SourceView.Asking => new[] { new SourceChoice("Use these files", () => Use()) },
        SourceView.Found when CopyOffered => new[] { new SourceChoice("Play", Advance), new SourceChoice("Copy, then play", Copy), new SourceChoice("Choose others", Reset) },
        SourceView.Found => new[] { new SourceChoice("Play", Advance), new SourceChoice("Choose others", Reset) },
        SourceView.Replace => new[] { new SourceChoice("Back", Back), new SourceChoice("Replace it", () => StartCopy(replace: true)) },
        SourceView.Copying => new[] { new SourceChoice("Cancel", Cancel) },
        SourceView.CopyFailed => new[] { new SourceChoice("Try again", Copy), new SourceChoice("Play without copying", Advance) },
        SourceView.Upgrade => new[] { new SourceChoice("Keep it", KeepExtracted), new SourceChoice("Delete it...", () => Show(SourceView.ConfirmDelete)) },
        SourceView.ConfirmDelete => new[] { new SourceChoice("Back", () => Show(SourceView.Upgrade)), new SourceChoice("Delete", StartDelete) },
        _ => Array.Empty<SourceChoice>(),
    };

    // A copy onto the folder it reads from would replace its own source; a dev tree's install is it.
    private bool CopyOffered => Source != null
        && !string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Source.Location)), CopyTarget, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="dataRoot"/> holds an old <c>extracted</c> folder with
    /// anything in it, which the upgrade question offers to delete.</summary>
    public static bool HasExtracted(string dataRoot)
    {
        string dir = Path.Combine(dataRoot, ExtractedFolder);
        return Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any();
    }

    /// <summary>The game files <paramref name="picked"/> names, or null with the reason in
    /// <paramref name="refusal"/>. A folder or drive answers <see cref="InstallLocator.Check"/>'s
    /// message, an image its reader's.</summary>
    public static IGameFileSource? Validate(string picked, out string? refusal)
    {
        string path = picked.Trim().Trim('"').Trim();
        refusal = null;
        if (path.Length > 0 && File.Exists(path))
        {
            if (GameFileSource.Open(path) is { } image)
            {
                return image;
            }

            refusal = ImageRefusal(path);
            return null;
        }

        var check = InstallLocator.Check(path);
        if (!check.IsInstall)
        {
            refusal = check.Message;
            return null;
        }

        if (GameFileSource.Open(check.Folder) is { } source)
        {
            return source;
        }

        refusal = $"'{check.Folder}' looks like the game disc, but holds no ZBD and GOSDATA{Path.DirectorySeparatorChar}ASSETS folders under GAME.";
        return null;
    }

    /// <summary>Deletes <paramref name="folder"/> and everything in it without following a link:
    /// a junction or symbolic link, the folder itself included, is unlinked and its target left
    /// alone. A read-only file is deleted as well.</summary>
    public static void DeleteWithoutFollowingLinks(string folder)
    {
        var dir = new DirectoryInfo(folder);
        if (IsLink(dir))
        {
            dir.Delete();
            return;
        }

        foreach (var entry in dir.EnumerateFileSystemInfos())
        {
            if (entry is DirectoryInfo sub)
            {
                DeleteWithoutFollowingLinks(sub.FullName);
            }
            else
            {
                entry.Attributes = FileAttributes.Normal;
                entry.Delete();
            }
        }

        dir.Delete();
    }

    /// <summary>A path chosen in the picker. It goes into the field whatever it is; when it is not
    /// game files, <see cref="Notice"/> carries the reason. Returns whether it is.</summary>
    public bool Pick(string path)
    {
        if (View != SourceView.Asking)
        {
            return false;
        }

        InstallPath = path;
        bool works = Validate(path, out string? refusal) != null;
        Notice = refusal;
        return works;
    }

    /// <summary>The Use press: takes <see cref="InstallPath"/> when it is game files, remembers it
    /// and moves to <see cref="SourceView.Found"/>, else shows why not. Returns whether it worked.</summary>
    public bool Use()
    {
        if (View != SourceView.Asking)
        {
            return false;
        }

        if (Validate(InstallPath, out string? refusal) is not { } source)
        {
            Notice = refusal;
            return false;
        }

        Source = source;
        Picked = true;
        Notice = null;
        Save(options => options.InstallPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(InstallPath.Trim().Trim('"').Trim())));
        View = SourceView.Found;
        return true;
    }

    /// <summary>Asks a running copy to stop. <see cref="Tick"/> then returns to <see cref="SourceView.Found"/>.</summary>
    public void Cancel()
    {
        if (View == SourceView.Copying)
        {
            _cancel?.Cancel();
        }
    }

    /// <summary>Main thread, once a frame: takes the worker's latest progress and, when it has
    /// ended, its outcome.</summary>
    public void Tick()
    {
        GameFileCopyProgress? progress;
        Outcome? outcome;
        lock (_gate)
        {
            progress = _progress;
            outcome = _outcome;
            _outcome = null;
        }

        if (View == SourceView.Copying && progress != null)
        {
            Fraction = progress.Total == 0 ? 0 : Math.Clamp((double)progress.Done / progress.Total, 0, 1);
            ProgressLine = string.Create(CultureInfo.InvariantCulture,
                $"{progress.Done} of {progress.Total} files, {progress.Bytes / (1024 * 1024)} MB: {progress.File}");
        }

        if (outcome == null)
        {
            return;
        }

        _cancel?.Dispose();
        _cancel = null;
        if (View == SourceView.Copying)
        {
            FinishCopy(outcome);
        }
        else if (View == SourceView.Deleting)
        {
            FinishDelete(outcome);
        }
    }

    // Not the thread pool: a copy blocks a thread for seconds to minutes, the delete as long.
    private static void StartThread(Action work) =>
        new Thread(() => work()) { IsBackground = true, Name = "game-files" }.Start();

    private static bool IsLink(FileSystemInfo entry) =>
        entry.LinkTarget != null || (entry.Attributes & FileAttributes.ReparsePoint) != 0;

    // Why a file that is not game files was refused: its reader's reason, or that it reads but is
    // not this game's disc.
    private static string ImageRefusal(string path)
    {
        try
        {
            if (path.EndsWith(".iso", StringComparison.OrdinalIgnoreCase))
            {
                IsoImage.Read(path);
            }
            else if (RawCdImage.Descriptor(path) != null)
            {
                RawCdImage.Read(path);
            }
            else
            {
                return $"'{path}' is a file, not a folder or a disc image (.iso, .cue, .ccd, or a .bin or .img beside its sheet).";
            }
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return $"'{path}' could not be read as a disc image: {e.Message}";
        }

        return $"'{path}' is a disc image, but not of Crimson Skies: it holds no ZBD and GOSDATA{Path.DirectorySeparatorChar}ASSETS folders.";
    }

    private OptionsDef Saved() => _store?.Load() ?? new OptionsDef();

    private void Save(Action<OptionsDef> change)
    {
        if (_store == null)
        {
            return;
        }

        var options = _store.Load();
        change(options);
        _store.Save(options);
    }

    private void Show(SourceView view)
    {
        Notice = null;
        View = view;
    }

    private void Reset()
    {
        Source = null;
        Picked = false;
        Show(SourceView.Asking);
    }

    private void Back() => Show(SourceView.Found);

    // The next question owed, else done. Each is asked once: the saved answer, or this flow's own
    // when there is no store to save it in.
    private void Advance()
    {
        Notice = null;
        var saved = Saved();
        if (_ask && !_upgradeAnswered && saved.ExtractedPromptAnswered != true && HasExtracted(DataRoot))
        {
            View = SourceView.Upgrade;
        }
        else
        {
            View = SourceView.Done;
        }
    }

    private void KeepExtracted()
    {
        _upgradeAnswered = true;
        Save(options => options.ExtractedPromptAnswered = true);
        Advance();
    }

    private void Copy()
    {
        if (Directory.Exists(CopyTarget))
        {
            Show(SourceView.Replace);
            return;
        }

        StartCopy(replace: false);
    }

    private void StartCopy(bool replace)
    {
        var source = Source!;
        string dataRoot = DataRoot;
        Fraction = 0;
        ProgressLine = string.Empty;
        Failure = string.Empty;
        Begin(SourceView.Copying, cancel => GameFileCopy.Run(source, dataRoot, replace, Report, cancel));
    }

    private void StartDelete()
    {
        string extracted = Extracted;
        ProgressLine = string.Empty;
        Begin(SourceView.Deleting, _ =>
        {
            DeleteWithoutFollowingLinks(extracted);
            return null;
        });
    }

    private void Begin(SourceView view, Func<CancellationToken, object?> work)
    {
        lock (_gate)
        {
            _progress = null;
            _outcome = null;
        }

        var cancel = new CancellationTokenSource();
        _cancel = cancel;
        Show(view);
        _start(() => Work(work, cancel.Token));
    }

    private void FinishCopy(Outcome outcome)
    {
        if (outcome.Cancelled)
        {
            Show(SourceView.Found);
            Notice = "Copy cancelled. Nothing was written to the data folder.";
            return;
        }

        if (outcome.Error is { } error)
        {
            Failure = error.Message;
            Show(SourceView.CopyFailed);
            return;
        }

        var result = (GameFileCopyResult)outcome.Result!;
        Missing = result.Missing;
        Fraction = 1;
        Source = GameFileSource.Open(result.Folder) ?? Source;
        Picked = true;
        Advance();
    }

    // A delete that stops part way leaves the question open, so the next start asks again.
    private void FinishDelete(Outcome outcome)
    {
        if (outcome.Error is { } error)
        {
            Show(SourceView.Upgrade);
            Notice = $"The folder could not be deleted completely: {error.Message}";
            return;
        }

        _upgradeAnswered = true;
        Save(options => options.ExtractedPromptAnswered = true);
        Advance();
    }

    private void Work(Func<CancellationToken, object?> work, CancellationToken cancel)
    {
        Outcome outcome;
        try
        {
            outcome = new Outcome(work(cancel), false, null);
        }
        catch (OperationCanceledException)
        {
            outcome = new Outcome(null, true, null);
        }
        catch (Exception e)
        {
            outcome = new Outcome(null, false, e);
        }

        lock (_gate)
        {
            _outcome = outcome;
        }
    }

    private void Report(GameFileCopyProgress progress)
    {
        lock (_gate)
        {
            _progress = progress;
        }
    }

    private sealed record Outcome(object? Result, bool Cancelled, Exception? Error);
}
