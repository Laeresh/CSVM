using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CSVM.Mech3;
using CSVM.Utils;

namespace CSVM.Extraction;

/// <summary>
/// Copies the game files a session reads out of any source into
/// <c>&lt;data root&gt;/CrimsonSkiesGame</c>, where <see cref="GameFileSource.Locate"/> finds them as
/// an install. A disc's chapters come out of their cabinets as plain files. ⚠ The tree is written
/// under <see cref="PartialSuffix"/> and renamed into place once complete. A copy that stops part
/// way is therefore never found. Engine-free; blocks its thread.
/// </summary>
public static class GameFileCopy
{
    /// <summary>Appended to the target folder's name while a copy is written.</summary>
    public const string PartialSuffix = ".partial";

    /// <summary>Appended to a replaced copy's name between the two renames of a replace.</summary>
    public const string OldSuffix = ".old";

    private const int Chunk = 1 << 20;

    /// <summary>The game-relative files a copy holds: <c>strings.dll</c>, every
    /// <see cref="ZbdManifest.Expected"/> archive and <see cref="RofFiles.GamePaths"/>.</summary>
    public static IReadOnlyList<string> Files { get; } = new[] { Messages.GameFile }
        .Concat(ZbdManifest.Expected.Select(path => "ZBD/" + path))
        .Concat(RofFiles.GamePaths)
        .ToList();

    /// <summary>The folder a copy into <paramref name="dataRoot"/> lands in.</summary>
    public static string Target(string dataRoot) => Path.GetFullPath(Path.Combine(dataRoot, GameFileSource.DataRootFolder));

    /// <summary>Copies every <see cref="Files"/> entry <paramref name="source"/> holds into
    /// <see cref="Target"/>. An existing target throws <see cref="IOException"/> unless
    /// <paramref name="replace"/>. A replace swaps the new tree in and deletes the old one whole,
    /// files outside <see cref="Files"/> included. Each chunk and file is reported to
    /// <paramref name="progress"/> on this thread. A failure or cancellation (<see cref="OperationCanceledException"/>)
    /// deletes the partial tree and leaves any existing target as it was.</summary>
    public static GameFileCopyResult Run(
        IGameFileSource source,
        string dataRoot,
        bool replace = false,
        Action<GameFileCopyProgress>? progress = null,
        CancellationToken cancel = default)
    {
        string target = Target(dataRoot);
        if (!replace && Directory.Exists(target))
        {
            throw new IOException($"'{target}' already exists; a copy replaces it only when asked to");
        }

        string partial = target + PartialSuffix;
        if (Directory.Exists(partial))
        {
            Directory.Delete(partial, recursive: true);
        }

        var missing = new List<string>();
        long bytes = 0;
        int done = 0;
        var buffer = new byte[Chunk];
        try
        {
            foreach (string file in Files)
            {
                cancel.ThrowIfCancellationRequested();
                using (var from = source.Open(file))
                {
                    if (from == null)
                    {
                        missing.Add(file);
                        progress?.Invoke(new GameFileCopyProgress(++done, Files.Count, bytes, file));
                        continue;
                    }

                    string to = Path.Combine(partial, file);
                    Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                    using var write = new FileStream(to, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    for (int read; (read = from.Read(buffer, 0, buffer.Length)) > 0;)
                    {
                        cancel.ThrowIfCancellationRequested();
                        write.Write(buffer, 0, read);
                        bytes += read;
                        progress?.Invoke(new GameFileCopyProgress(done, Files.Count, bytes, file));
                    }
                }

                done++;
                progress?.Invoke(new GameFileCopyProgress(done, Files.Count, bytes, file));
            }

            cancel.ThrowIfCancellationRequested();
            Swap(partial, target);
        }
        catch
        {
            TryDelete(partial);
            throw;
        }

        Log.Info("core", $"game files: copied {done - missing.Count} files, {bytes} bytes, from {source.Location} to {target}; {missing.Count} not in the source");
        return new GameFileCopyResult(target, done - missing.Count, bytes, missing);
    }

    /// <summary><c>--copy-game-files</c>: copies the source <paramref name="sourcePath"/> names into
    /// <paramref name="dataRoot"/>, logging each file, and never replaces a copy already there.
    /// False with a logged reason when the source does not open or the copy fails.</summary>
    public static bool RunToLog(string sourcePath, string dataRoot)
    {
        if (GameFileSource.Open(sourcePath) is not { } source)
        {
            Log.Error("core", $"--copy-game-files: '{sourcePath}' is not a Crimson Skies install, disc folder or disc image");
            return false;
        }

        try
        {
            int logged = 0;
            var result = Run(source, dataRoot, progress: step =>
            {
                if (step.Done > logged)
                {
                    logged = step.Done;
                    Log.Info("core", $"--copy-game-files: {step.Done}/{step.Total} {step.File}");
                }
            });
            result.Missing.ToList().ForEach(file => Log.Warn("core", $"--copy-game-files: {file} is not in {source.Location}"));
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Log.Error("core", $"--copy-game-files: {e.Message}; nothing was copied");
            return false;
        }
    }

    // Between the two renames no copy exists, so a lookup in that moment falls through to the next
    // place rather than finding a partial tree. A failed second rename puts the old one back.
    private static void Swap(string partial, string target)
    {
        if (!Directory.Exists(target))
        {
            Directory.Move(partial, target);
            return;
        }

        string old = target + OldSuffix;
        if (Directory.Exists(old))
        {
            Directory.Delete(old, recursive: true);
        }

        Directory.Move(target, old);
        try
        {
            Directory.Move(partial, target);
        }
        catch
        {
            Directory.Move(old, target);
            throw;
        }

        TryDelete(old);
    }

    // Best effort, so cleanup never hides the failure that called it. Directory.Delete removes a
    // junction rather than following it.
    private static void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Warn("core", $"game files: could not delete {folder}: {e.Message}");
        }
    }
}

/// <summary>One step of <see cref="GameFileCopy.Run"/>: <paramref name="Done"/> of
/// <paramref name="Total"/> files finished or found absent, <paramref name="Bytes"/> written so far,
/// and the file being copied or just finished.</summary>
public sealed record GameFileCopyProgress(int Done, int Total, long Bytes, string File);

/// <summary>A finished copy: the folder it landed in, the files and bytes written, and the
/// <see cref="GameFileCopy.Files"/> entries the source did not hold.</summary>
public sealed record GameFileCopyResult(string Folder, int Files, long Bytes, IReadOnlyList<string> Missing);
