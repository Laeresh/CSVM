using System;
using Godot;

namespace CSVM.Utils;

/// <summary>How a folder open ended.</summary>
public enum FolderOpenOutcome
{
    /// <summary>The system file browser was handed the folder.</summary>
    Opened,

    /// <summary>The folder could not be created or handed over; the log says which.</summary>
    Failed,

    /// <summary>Steam's Game Mode refused it before the shell was asked.</summary>
    Refused,
}

/// <summary>A folder open's outcome and the full path it was for.</summary>
public readonly record struct FolderOpenResult(FolderOpenOutcome Outcome, string Path);

/// <summary>
/// Shows a folder in the system file browser, creating it first. A directory path handed to
/// <see cref="OS.ShellOpen"/> opens the file browser on Windows and on Linux alike. Every caller that hands a player a folder goes through here, so each open
/// and each failure reads the same in the log. In Steam's Game Mode it refuses (<see cref="SteamOs.InGameMode"/>).
/// ⚠ Do not read Game Mode off the shell's answer. There it answers Ok and no window appears.
/// </summary>
public static class FolderOpener
{
    /// <summary>Hands a path to the system's handler. A seam, so a suite records the calls rather
    /// than opening a window.</summary>
    public static Func<string, Error> Shell { get; set; } = OS.ShellOpen;

    /// <summary>Creates <paramref name="path"/> if missing and opens it. <paramref name="what"/>
    /// names the folder in the log line every outcome writes.</summary>
    public static FolderOpenResult Open(string path, string what)
    {
        string full = System.IO.Path.GetFullPath(path);
        if (SteamOs.InGameMode)
        {
            Log.Info("core", $"{what} {full} not opened: refused in Game Mode, which shows no file browser");
            return new FolderOpenResult(FolderOpenOutcome.Refused, full);
        }

        var made = DirAccess.MakeDirRecursiveAbsolute(full);
        if (made != Error.Ok && !DirAccess.DirExistsAbsolute(full))
        {
            Log.Warn("core", $"{what} {full} could not be created: {made}");
            return new FolderOpenResult(FolderOpenOutcome.Failed, full);
        }

        var opened = Shell(full);
        if (opened != Error.Ok)
        {
            Log.Warn("core", $"{what} {full} could not be opened: {opened}");
            return new FolderOpenResult(FolderOpenOutcome.Failed, full);
        }

        Log.Info("core", $"{what} opened: {full}");
        return new FolderOpenResult(FolderOpenOutcome.Opened, full);
    }
}
