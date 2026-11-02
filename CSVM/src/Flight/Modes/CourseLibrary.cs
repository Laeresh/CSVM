using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Modes;

/// <summary>Which of a <see cref="CourseLibrary"/>'s two folders a course file sits in.</summary>
public enum CourseSource
{
    Shipped,
    GameFolder,
}

/// <summary>One course file a library lists: chapter, name (the file stem), source and path.</summary>
public readonly record struct CourseFile(string Chapter, string Name, CourseSource Source, string Path);

/// <summary>
/// The course files of a launch, one per course at <c>&lt;root&gt;/&lt;chapter&gt;/&lt;name&gt;.json</c>
/// under two roots. The shipped root is <see cref="ShippedRoot"/>, read through Godot since an exported
/// build keeps it in the pck. The game folder is <c>&lt;dataRoot&gt;/Courses</c>, and its file wins a
/// name clash in a chapter, matched without case. A root without <c>://</c> is an OS path read through
/// <see cref="System.IO"/>. A suite or a unit test hands in its own scratch folders that way.
/// </summary>
public sealed class CourseLibrary
{
    /// <summary>The shipped courses, committed under <c>CSVM/data/courses/</c> and exported through
    /// the preset's <c>data/*.json</c> include filter.</summary>
    public const string ShippedRoot = "res://data/courses";

    /// <summary>The game folder's course root, under the data root beside <c>HD</c> and <c>HDModels</c>.</summary>
    public const string GameFolderName = "Courses";

    private const string Extension = ".json";

    /// <summary>A library over <paramref name="shippedRoot"/> and <paramref name="gameFolder"/>, which
    /// is null when the launch reads no game folder.</summary>
    public CourseLibrary(string shippedRoot, string? gameFolder)
    {
        Shipped = shippedRoot;
        GameFolder = gameFolder;
    }

    /// <summary>The shipped root, <see cref="ShippedRoot"/> outside a test.</summary>
    public string Shipped { get; }

    /// <summary>The game folder's course root, or null where the launch reads none.</summary>
    public string? GameFolder { get; }

    /// <summary>The library a launch reads. ⚠ A <c>--det</c> run reads no game folder, as it reads no
    /// HD pack. No golden or suite may depend on courses one machine dropped in.</summary>
    public static CourseLibrary ForLaunch(string dataRoot, bool det) =>
        new(ShippedRoot, det ? null : System.IO.Path.Combine(dataRoot, GameFolderName));

    /// <summary>Every course of <paramref name="chapter"/> by name, the game folder's file standing in
    /// for a shipped one of the same name. Listing reads no file, so a broken one surfaces on
    /// <see cref="Load"/>.</summary>
    public IReadOnlyList<CourseFile> List(string chapter)
    {
        var byName = new Dictionary<string, CourseFile>(StringComparer.OrdinalIgnoreCase);
        foreach (var (source, root) in Roots())
        {
            string folder = $"{root}/{chapter}";
            foreach (string name in Names(folder))
            {
                byName[name] = new CourseFile(chapter, name, source, $"{folder}/{name}{Extension}");
            }
        }

        return byName.Values.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>The course <paramref name="chapter"/>/<paramref name="name"/> from the source that wins
    /// it, or null with the reason, its path included, in <paramref name="error"/>.</summary>
    public Course? Load(string chapter, string name, out string error)
    {
        var file = List(chapter).FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        if (file.Path is null)
        {
            error = $"no course {chapter}/{name}";
            return null;
        }

        if (ReadText(file.Path) is not { } text)
        {
            error = $"course {file.Path}: unreadable";
            return null;
        }

        var course = Course.Parse(chapter, file.Name, text, out error);
        if (course is null)
        {
            error = $"course {file.Path}: {error}";
        }

        return course;
    }

    /// <summary>What a screen calls the course <paramref name="chapter"/>/<paramref name="name"/>: its
    /// <see cref="Course.DisplayName"/>, or <paramref name="name"/> where the file is missing or broken.</summary>
    public string DisplayName(string chapter, string name) => Load(chapter, name, out _)?.DisplayName ?? name;

    /// <summary>Writes <paramref name="course"/> to <c>&lt;chapter&gt;/&lt;name&gt;.json</c> under the
    /// <paramref name="target"/> root, atomically, and returns the OS path written. A shipped root is
    /// resolved through <see cref="ProjectSettings.GlobalizePath"/>, which is the source tree only in a
    /// dev build. Throws on a target this library has no root for, or a chapter or name no file can carry.</summary>
    public string Save(Course course, CourseSource target)
    {
        ArgumentNullException.ThrowIfNull(course);
        string root = (target == CourseSource.Shipped ? Shipped : GameFolder)
            ?? throw new InvalidOperationException("this library reads no game folder");
        RequireFileNames(course);
        string folder = System.IO.Path.Combine(root.Contains("://") ? Globalize(root) : root, course.Chapter);
        Directory.CreateDirectory(folder);
        string path = System.IO.Path.Combine(folder, course.Name + Extension);
        AtomicFile.WriteAllText(path, course.ToJson());
        Log.Info("flight", $"course saved: {course.Chapter}/{course.Name} -> {path}");
        return path;
    }

    /// <summary>Keeps a host's <paramref name="course"/> in the game folder when neither root has a
    /// file of its name in its chapter, and returns the path written. Null when one stands, when
    /// the library reads no game folder, or when the write failed, which is logged.
    /// ⚠ A same-named file is never overwritten, whatever it holds.</summary>
    public string? SaveIfAbsent(Course course)
    {
        ArgumentNullException.ThrowIfNull(course);
        try
        {
            // ⚠ Checked before the listing, which would otherwise read outside the root for a host's "..".
            RequireFileNames(course);
            if (GameFolder is null || List(course.Chapter).Any(f => string.Equals(f.Name, course.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            return Save(course, CourseSource.GameFolder);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.Warn("flight", $"course {course.Chapter}/{course.Name}: not kept in the game folder ({e.Message})");
            return null;
        }
    }

    private static void RequireFileNames(Course course)
    {
        foreach (string part in new[] { course.Chapter, course.Name })
        {
            if (part.Length == 0 || part is "." or ".." || part.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException($"course chapter and name must be file names, got \"{part}\"");
            }
        }
    }

    private static IEnumerable<string> Names(string folder)
    {
        if (folder.Contains("://"))
        {
            return ResNames(folder);
        }

        return Directory.Exists(folder)
            ? Directory.GetFiles(folder, "*" + Extension).Select(f => System.IO.Path.GetFileNameWithoutExtension(f)!)
            : Array.Empty<string>();
    }

    private static string? ReadText(string path) => path.Contains("://") ? ResText(path) : AtomicFile.ReadAllText(path);

    // The Godot calls sit in their own members, so a unit test over OS paths never reaches the engine.
    private static IEnumerable<string> ResNames(string folder) =>
        DirAccess.DirExistsAbsolute(folder)
            ? DirAccess.GetFilesAt(folder).Where(f => f.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
                .Select(f => f[..^Extension.Length])
            : Array.Empty<string>();

    private static string? ResText(string path) =>
        Godot.FileAccess.FileExists(path) ? Godot.FileAccess.GetFileAsString(path) : null;

    private static string Globalize(string root) => ProjectSettings.GlobalizePath(root);

    // Shipped first, so the game folder's file replaces it on a clash.
    private IEnumerable<(CourseSource Source, string Root)> Roots()
    {
        yield return (CourseSource.Shipped, Shipped);
        if (GameFolder is not null)
        {
            yield return (CourseSource.GameFolder, GameFolder);
        }
    }
}
