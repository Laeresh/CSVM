using System;
using System.Collections.Generic;
using System.IO;

namespace CSVM.Spec;

/// <summary>
/// A folder of unzbd output a modder names, read in place of the game files archive by archive
/// (<see cref="GameData.Load"/>). Its layout is unzbd's: an install path without <c>ZBD/</c>, with
/// <c>.zip</c> for <c>.zbd</c>. An unpacked folder of the same name is preferred over its zip.
/// Folders are upper case, file names lower case (<c>Extraction.ZbdTree</c>,
/// <c>Extraction.SessionPaths</c>). The files read: docs/modding.md.
/// </summary>
public sealed class ModdingFolder
{
    /// <summary>The folder at <paramref name="root"/>, made absolute.</summary>
    public ModdingFolder(string root) => Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

    /// <summary>The folder, absolute.</summary>
    public string Root { get; }

    /// <summary>The folder a launch reads: <c>--mod-folder=</c> when given (empty names none), else
    /// the saved one, which a deterministic or synthetic run ignores. Null for none.</summary>
    public static string? Choose(SessionSpec spec, Func<string?> saved)
    {
        if (spec.SyntheticData)
        {
            return null;
        }

        string? named = spec.ModFolder ?? (spec.Det ? null : saved());
        return string.IsNullOrWhiteSpace(named) ? null : Path.GetFullPath(named);
    }

    /// <summary>The folders Game Options' Modding Folder row offers beside the saved one. They are
    /// <paramref name="launched"/> and an old <c>extracted</c> under <paramref name="dataRoot"/>,
    /// where each is a folder. Offered, never adopted.</summary>
    public static IReadOnlyList<string> Offered(string? dataRoot, string? launched)
    {
        var folders = new List<string>();
        foreach (string? folder in new[] { launched, dataRoot == null ? null : Path.Combine(dataRoot, "extracted") })
        {
            if (folder != null && Directory.Exists(folder))
            {
                folders.Add(new ModdingFolder(folder).Root);
            }
        }

        return folders;
    }

    /// <summary>The output standing in for the game file at <paramref name="installPath"/>
    /// (<c>ZBD/C1/gamez.zbd</c>, <c>strings.dll</c>), or null when the folder holds none.</summary>
    public string? Find(string installPath)
    {
        string path = Path.Combine(Root, OutputOf(installPath).Replace('/', Path.DirectorySeparatorChar));
        if (path.EndsWith(".json", StringComparison.Ordinal))
        {
            return File.Exists(path) ? path : null;
        }

        string found = Extraction.SessionPaths.PreferUnzipped(path);
        return Directory.Exists(found) || File.Exists(found) ? found : null;
    }

    /// <summary>Where unzbd puts the output of <paramref name="installPath"/>, relative and
    /// <c>/</c>-separated: <c>ZBD/c1/m02/zrdr.zbd</c> is <c>C1/M02/zrdr.zip</c>.</summary>
    internal static string OutputOf(string installPath)
    {
        string unix = installPath.Replace('\\', '/');
        if (unix.Equals("strings.dll", StringComparison.OrdinalIgnoreCase))
        {
            return "messages.json";
        }

        if (unix.StartsWith("ZBD/", StringComparison.OrdinalIgnoreCase))
        {
            unix = unix[4..];
        }

        return Extraction.ZbdTree.Canonical(Path.ChangeExtension(unix, unix.Equals("interp.zbd", StringComparison.OrdinalIgnoreCase) ? ".json" : ".zip"));
    }
}
