using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

namespace CSVM.Utils;

/// <summary>The folder the player's own files live in: options, bindings, campaign profiles, custom
/// planes, stunt scores, stick profiles and stunt screenshots. It is Godot's user folder unless
/// portable mode is on. A <c>portable.txt</c> beside the executable or <c>--portable</c> turns it on,
/// and the files then go to a <c>userdata/</c> folder there. The launcher settles it once at boot,
/// before any store opens. Godot's own log and shader cache stay in Godot's user folder either way,
/// because the engine fixes that path before any script runs.</summary>
public static class UserData
{
    /// <summary>The user argument that turns portable mode on.</summary>
    public const string PortableFlag = "--portable";

    /// <summary>The file whose presence in the run's root turns portable mode on. Its content is ignored.</summary>
    public const string MarkerFile = "portable.txt";

    /// <summary>The portable folder's name, beside the executable.</summary>
    public const string PortableFolderName = "userdata";

    private static string? _root;

    /// <summary>Gets the folder as an OS path: the portable folder once <see cref="Settle"/> turned
    /// portable mode on, else Godot's user folder.</summary>
    public static string Root => _root ?? OS.GetUserDataDir();

    /// <summary>Gets a value indicating whether portable mode is on.</summary>
    public static bool Portable => _root != null;

    /// <summary>Turns portable mode on when <see cref="PortableFolder"/> asks for it and the folder
    /// can be created. A folder that cannot be created keeps Godot's, with a warning.</summary>
    public static void Settle(string runRoot, bool exported, IReadOnlyList<string> userArgs)
    {
        _root = null;
        if (PortableFolder(runRoot, exported, userArgs, File.Exists) is not { } folder)
        {
            return;
        }
        try
        {
            Directory.CreateDirectory(folder);
            _root = folder;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Warn("core", $"portable mode: cannot create {folder}, keeping user data in {Root}: {e.Message}");
        }
    }

    /// <summary>The portable folder for a run rooted at <paramref name="runRoot"/>, or null when
    /// neither <see cref="PortableFlag"/> nor <see cref="MarkerFile"/> asks for one. A repo run
    /// puts it under the git-ignored <c>.scratch/</c>, as <see cref="Log.DirectoryFor"/> does.</summary>
    public static string? PortableFolder(string runRoot, bool exported, IReadOnlyList<string> userArgs, Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(userArgs);
        ArgumentNullException.ThrowIfNull(fileExists);
        if (!userArgs.Contains(PortableFlag) && !fileExists(Path.Combine(runRoot, MarkerFile)))
        {
            return null;
        }
        return exported ? Path.Combine(runRoot, PortableFolderName) : Path.Combine(runRoot, ".scratch", PortableFolderName);
    }
}
