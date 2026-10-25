using System.IO;

namespace CSVM.Extraction;

/// <summary>Where the original's files are read from: an install folder or a mounted drive. An
/// <c>.iso</c> or a disc's unpacked cabinets sit behind the same calls. A path is
/// game-relative, spelled as an install spells it (<c>ZBD/C1/zrdr.zbd</c>), split on either
/// slash and matched without regard to case. Obtain one through <see cref="GameFileSource"/>.</summary>
public interface IGameFileSource
{
    /// <summary>Where the files come from, for log lines and error messages.</summary>
    string Location { get; }

    /// <summary>Whether <paramref name="path"/> names a file in this source.</summary>
    bool Exists(string path);

    /// <summary>A readable, seekable stream over the file <paramref name="path"/> names, or null
    /// when it is absent. The caller disposes it.</summary>
    Stream? Open(string path);
}

/// <summary>Finds the session's game-file source, taking the first usable place in order. That
/// is the <see cref="InstallVariable"/> override, the copy under the data root, the remembered
/// install, then the locator's candidates. A path becomes a source only in
/// <see cref="Open(string)"/>, so a disc or <c>.iso</c> kind is added there without changing callers.
/// Engine-free, so a unit test locates the install by the same rule.</summary>
public static class GameFileSource
{
    /// <summary>The environment variable naming an install folder that beats every other place,
    /// for a worktree, CI or a second install.</summary>
    public const string InstallVariable = "CSVM_INSTALL_ROOT";

    /// <summary>The install folder's name under a data root, where a developer tree keeps its
    /// install and where a copy into the data root lands.</summary>
    public const string DataRootFolder = "CrimsonSkiesGame";

    /// <summary>A source over <paramref name="root"/>, or null when it is not an install
    /// (<see cref="InstallLocator.IsInstall"/>).</summary>
    public static IGameFileSource? Open(string root) =>
        Directory.Exists(root) && InstallLocator.IsInstall(root) ? new InstallFolderSource(Path.GetFullPath(root)) : null;

    /// <summary>The first install found, or null when there is none. <paramref name="installOverride"/>
    /// is the <see cref="InstallVariable"/> value, <paramref name="dataRoot"/> the session's data
    /// root, <paramref name="remembered"/> <see cref="RememberedInstall.Get()"/>; any may be null.
    /// A place that is not an install is passed over.</summary>
    public static IGameFileSource? Locate(string? installOverride, string? dataRoot, string? remembered, InstallSearchRoots roots)
    {
        foreach (string? place in new[] { installOverride, dataRoot == null ? null : Path.Combine(dataRoot, DataRootFolder) })
        {
            if (!string.IsNullOrWhiteSpace(place) && Open(place) is { } source)
            {
                return source;
            }
        }

        foreach (string candidate in InstallLocator.Candidates(roots, remembered))
        {
            if (Open(candidate) is { } source)
            {
                return source;
            }
        }

        return null;
    }

    /// <summary>The whole file <paramref name="path"/> names. Throws
    /// <see cref="FileNotFoundException"/> naming the source when it is absent.</summary>
    public static byte[] ReadAllBytes(this IGameFileSource source, string path)
    {
        using var stream = source.Open(path)
            ?? throw new FileNotFoundException($"'{path}' is not in the game files at '{source.Location}'", path);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}

/// <summary>An install laid out on disk, read in place: a copied install, the installer's
/// folder, or a mounted drive holding that layout. Every lookup is
/// <see cref="InstallLocator.ResolveFile"/>'s segment walk, which only matches names the folder
/// enumerates, so a <c>..</c> segment never leaves <see cref="Location"/>.</summary>
public sealed class InstallFolderSource : IGameFileSource
{
    /// <summary>A source over the folder <paramref name="root"/>, taken as given.</summary>
    public InstallFolderSource(string root)
    {
        Location = root;
    }

    /// <summary>The install folder.</summary>
    public string Location { get; }

    /// <inheritdoc/>
    public bool Exists(string path) => InstallLocator.ResolveFile(Location, path) != null;

    /// <inheritdoc/>
    public Stream? Open(string path) =>
        InstallLocator.ResolveFile(Location, path) is { } file
            ? new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read)
            : null;
}
