using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CSVM.Utils;

namespace CSVM.Extraction;

/// <summary>
/// The UI half of the game files, read in place: both archives' members, a PNG pair for each
/// <c>.BM</c>, and the ten cinemas. CSVM derives <see cref="UiStringsFile"/> and
/// <see cref="MenuLayoutFile"/> from them. Paths are in <see cref="RofTree"/>'s case and keep the
/// layout <c>extracted/rof/</c> had, so an HD pack's <c>rof/</c> keys still match. The archive is
/// read on the first lookup and a member inflates on each read. A launch binds its view to its data
/// root (<see cref="Bind"/>), where a consumer holding only a data root finds it.
/// </summary>
public sealed class RofFiles
{
    /// <summary>The derived UI string table, the JSON rows <see cref="UiStringTable.ToJson"/> writes.</summary>
    public const string UiStringsFile = "ui_strings.json";

    /// <summary>The derived menu layout, the JSON <see cref="MenuLayoutDecoder.ToJson"/> writes.</summary>
    public const string MenuLayoutFile = "menu_layout.json";

    /// <summary>Where the patch archive's members sit in the view.</summary>
    public const string PatchFolder = "_crimptch";

    /// <summary>The cinemas' folder in the view, the base the original resolves a movie name under
    /// (docs/formats/cinemas.md).</summary>
    public const string CinemaFolder = "ASSETS/GRAPHICS/MPG";

    /// <summary>The install path of the UI archive.</summary>
    public const string ArchivePath = "GOSDATA/ASSETS/crimson.rof";

    /// <summary>The install path of the patch archive.</summary>
    public const string PatchPath = "GOSDATA/ASSETS/crimptch.rof";

    /// <summary>The install folder the cinemas sit loose in.</summary>
    public const string InstallCinemaFolder = "GOSDATA/ASSETS/GRAPHICS/MPG";

    /// <summary>The ten movies a complete install has, lower-case. They are not archive members:
    /// <c>crimson.rof</c> carries <see cref="CinemaFolder"/> empty and the files sit loose in the
    /// install's <c>GOSDATA/ASSETS/GRAPHICS/MPG</c>.</summary>
    public static readonly IReadOnlyList<string> Cinemas = new[]
    {
        "chap0.mpg", "chap1.mpg", "chap2.mpg", "chap3.mpg", "chap4.mpg", "chap5.mpg",
        "crimflag.mpg", "final.mpg", "msopen1.mpg", "zipper.mpg",
    };

    /// <summary>A view holding nothing: no install, or a data root no launch bound.</summary>
    public static readonly RofFiles Empty = Of(new Dictionary<string, byte[]>());

    private static readonly ConcurrentDictionary<string, RofFiles> Bound = new(StringComparer.OrdinalIgnoreCase);

    // langui.dll first: the first row for an id wins everywhere downstream, the order the original
    // loads the two in.
    private static readonly string[] StringDlls = { "langui", "language" };

    private readonly Func<Dictionary<string, Member>> _files;

    // Built once, on the first lookup, unless live: then on every lookup.
    private RofFiles(Func<Dictionary<string, Member>> build, bool live = false)
    {
        var once = new Lazy<Dictionary<string, Member>>(build);
        _files = live ? build : () => once.Value;
    }

    /// <summary>Every install path <see cref="Open"/> reads: both archives, the two string DLLs and
    /// the cinemas, as an install spells them. <see cref="GameFileCopy"/> copies these.</summary>
    public static IEnumerable<string> GamePaths =>
        new[] { ArchivePath, PatchPath }.Concat(StringDlls.Select(DllPath)).Concat(Cinemas.Select(m => InstallCinemaFolder + "/" + m));

    /// <summary>Every path the view holds, in no particular order.</summary>
    public IEnumerable<string> Files => _files().Keys;

    /// <summary>Whether the view holds nothing.</summary>
    public bool IsEmpty => _files().Count == 0;

    /// <summary>The view bound to <paramref name="dataRoot"/>, or <see cref="Empty"/>.</summary>
    public static RofFiles For(string? dataRoot) =>
        dataRoot != null && Bound.TryGetValue(Key(dataRoot), out var files) ? files : Empty;

    /// <summary>Makes <paramref name="files"/> the view every lookup under
    /// <paramref name="dataRoot"/> answers from, replacing any bound before.</summary>
    public static void Bind(string dataRoot, RofFiles files) => Bound[Key(dataRoot)] = files;

    /// <summary>A view over files already in memory, by path in any case: the synthetic tree's, or
    /// a unit test's.</summary>
    public static RofFiles Of(IReadOnlyDictionary<string, byte[]> files)
    {
        var held = files.ToDictionary(f => RofTree.Canonical(f.Key), f => f.Value);
        return new RofFiles(() => held.ToDictionary(f => f.Key, f => new Member(() => f.Value), StringComparer.Ordinal));
    }

    /// <summary>A view listed through <paramref name="list"/> and read through
    /// <paramref name="read"/> on every lookup, so it follows files that change: a unit test's
    /// folder. A later path that differs only in case replaces an earlier one.</summary>
    public static RofFiles Live(Func<IEnumerable<string>> list, Func<string, byte[]?> read) =>
        new(
            () =>
            {
                var files = new Dictionary<string, Member>(StringComparer.Ordinal);
                foreach (string path in list())
                {
                    files[RofTree.Canonical(path)] = new Member(() => read(path));
                }

                return files;
            },
            live: true);

    /// <summary>The view over <paramref name="game"/>, read on the first lookup; <see cref="Empty"/>
    /// without a source. An archive that does not read is logged and read as absent, so a damaged
    /// install still reaches the menu.</summary>
    public static RofFiles Open(IGameFileSource? game) => game == null ? Empty : new RofFiles(() => Load(game));

    /// <summary>The view path of the cinema a name means, with <c>.mpg</c> supplied when the name
    /// carries no extension. ⚠ Every caller that names a cinema resolves through here. The data
    /// names four of the ten in a case the install's files do not have.</summary>
    public static string Cinema(string name) =>
        RofTree.Canonical(CinemaFolder + "/" + (Path.GetExtension(name).Length == 0 ? name + ".mpg" : name));

    /// <summary>Whether the view holds <paramref name="relative"/>, in any case.</summary>
    public bool Exists(string relative) => _files().ContainsKey(RofTree.Canonical(relative));

    /// <summary>The bytes of <paramref name="relative"/>, or null when the view lacks it or it does
    /// not decode.</summary>
    public byte[]? Read(string relative) =>
        _files().TryGetValue(RofTree.Canonical(relative), out var member) ? Guarded(relative, member.Read) : null;

    /// <summary>The first <paramref name="count"/> bytes of <paramref name="relative"/> (all of it
    /// when shorter), read no further than that where the file allows; null when absent.</summary>
    public byte[]? Head(string relative, int count)
    {
        if (!_files().TryGetValue(RofTree.Canonical(relative), out var member))
        {
            return null;
        }

        return member.Head is { } head ? Guarded(relative, () => head(count))
            : Read(relative) is { } all ? all[..Math.Min(count, all.Length)] : null;
    }

    /// <summary>The text of <paramref name="relative"/> as <c>File.ReadAllText</c> reads a file: a
    /// byte order mark wins over <paramref name="encoding"/> (UTF-8 when null). Null when absent.</summary>
    public string? Text(string relative, Encoding? encoding = null) =>
        Read(relative) is { } bytes ? TextOf(bytes, encoding ?? Encoding.UTF8) : null;

    private static string TextOf(byte[] bytes, Encoding encoding)
    {
        using var reader = new StreamReader(new MemoryStream(bytes), encoding, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    // A file that does not read is logged and absent, never a throw: a menu draws without it.
    private static byte[]? Guarded(string relative, Func<byte[]?> read)
    {
        try
        {
            return read();
        }
        catch (Exception e) when (e is InvalidDataException or IOException)
        {
            Log.Warn("core", $"rof: {relative} does not read, drawing without it: {e.Message}");
            return null;
        }
    }

    private static string Key(string dataRoot) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot));

    private static Dictionary<string, Member> Load(IGameFileSource game)
    {
        var watch = Stopwatch.StartNew();
        var files = new Dictionary<string, Member>(StringComparer.Ordinal);
        AddArchive(game, ArchivePath, string.Empty, files);
        AddArchive(game, PatchPath, PatchFolder + "/", files);
        foreach (string movie in Cinemas)
        {
            string source = InstallCinemaFolder + "/" + movie;
            if (game.Exists(source))
            {
                files[Cinema(movie)] = new Member(() => game.ReadAllBytes(source), count => HeadOf(game, source, count));
            }
        }

        // Both derived files are made once, on first read, as the extraction wrote them.
        var view = new RofFiles(() => files);
        var rows = new Lazy<List<UiStringRow>>(() => Rows(game, view));
        if (StringDlls.Any(dll => game.Exists(DllPath(dll))))
        {
            var strings = new Lazy<byte[]?>(() => rows.Value.Count > 0 ? UiStringTable.ToJson(rows.Value) : null);
            files[RofTree.Canonical(UiStringsFile)] = new Member(() => strings.Value);
        }

        if (view.Exists("ASSETS/LAYOUT.CSV"))
        {
            var layout = new Lazy<byte[]>(() => Encoding.UTF8.GetBytes(
                MenuLayoutDecoder.ToJson(MenuLayoutDecoder.Decode(MenuLayoutDecoder.ReadTree(view, UiStringTable.TextById(rows.Value))))));
            files[RofTree.Canonical(MenuLayoutFile)] = new Member(() => layout.Value);
        }

        Log.Info("core", $"rof: {files.Count} files from {game.Location} in {watch.ElapsedMilliseconds} ms");
        return files;
    }

    // A .BM member also answers as the PNG pair the extraction wrote beside it, decoded on read.
    private static void AddArchive(IGameFileSource game, string path, string prefix, Dictionary<string, Member> files)
    {
        if (!game.Exists(path))
        {
            Log.Warn("core", $"rof: no {path} in {game.Location}");
            return;
        }

        try
        {
            byte[] archive = game.ReadAllBytes(path);
            foreach (var entry in RofArchive.Walk(archive).Where(e => !e.IsDirectory))
            {
                string member = RofTree.Canonical(prefix + entry.Path);
                files[member] = new Member(() => RofArchive.ReadMember(archive, entry), count => RofArchive.ReadHead(archive, entry, count));
                if (member.EndsWith(".BM", StringComparison.Ordinal))
                {
                    string stem = member[..^3];
                    files[stem + ".PNG"] = new Member(() => BmTexture.TryDecode(RofArchive.ReadMember(archive, entry)) is { } bm
                        ? PngWriter.EncodeRgb(bm.Width, bm.Height, bm.Shading) : null);
                    files[stem + "_MASK.PNG"] = new Member(() => BmTexture.TryDecode(RofArchive.ReadMember(archive, entry)) is { } bm
                        ? PngWriter.EncodeRgb(bm.Width, bm.Height, bm.Masks) : null);
                }
            }
        }
        catch (Exception e) when (e is InvalidDataException or IOException)
        {
            Log.Error("core", $"{path} in {game.Location} does not read, using none of it: {e.Message}");
        }
    }

    private static string DllPath(string dll) => $"GOSDATA/ASSETS/BINARIES/{dll}.dll";

    // In StringDlls' order. RESOURCE.H ships inside the base archive.
    private static List<UiStringRow> Rows(IGameFileSource game, RofFiles view)
    {
        var symbols = view.Text("ASSETS/SCRIPTS/RESOURCE.H", Encoding.Latin1) is { } header
            ? UiStringTable.ParseSymbols(header)
            : new Dictionary<int, string>();
        var rows = new List<UiStringRow>();
        foreach (string dll in StringDlls)
        {
            string path = DllPath(dll);
            if (!game.Exists(path))
            {
                continue;
            }

            try
            {
                rows.AddRange(UiStringTable.Rows(dll, PeStringTable.Read(game.ReadAllBytes(path)), symbols));
            }
            catch (Exception e) when (e is InvalidDataException or IOException)
            {
                Log.Error("core", $"{path} in {game.Location} does not read, using none of it: {e.Message}");
            }
        }

        return rows;
    }

    private static byte[] HeadOf(IGameFileSource game, string path, int count)
    {
        using var stream = game.Open(path) ?? throw new FileNotFoundException($"'{path}' is not in the game files at '{game.Location}'", path);
        var head = new byte[count];
        int read = 0;
        for (int got; read < count && (got = stream.Read(head, read, count - read)) > 0;)
        {
            read += got;
        }

        return head[..read];
    }

    // One file of the view, read whole or by its start. A deflated member or a cinema reads its start
    // alone; a null Head reads the whole and slices.
    private sealed record Member(Func<byte[]?> Read, Func<int, byte[]?>? Head = null);
}
