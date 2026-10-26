using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CSVM.Extraction;

/// <summary>A disc's game files. A path below <c>ZBD/&lt;chapter&gt;/</c> is answered from the
/// chapter's cabinet <c>ZBD/&lt;chapter&gt;.CAB</c> in the inner source. Every other path goes to
/// the inner source, an <see cref="IsoImage"/> or the disc's <c>GAME</c> folder. A cabinet's
/// file table is read on the first lookup into its chapter. Its folders are unpacked into memory
/// on the first open, and only the chapter opened last keeps them. The aliased texture tiers (<c>RTEXTU~1.ZBD</c>)
/// get their install names back (docs/formats/cabinet.md), and reads may run on any thread.</summary>
public sealed class CabinetSource : IGameFileSource
{
    private const string MemberPrefix = "GAME/ZBD/";

    private readonly ConcurrentDictionary<string, Lazy<Chapter?>> _chapters = new(StringComparer.OrdinalIgnoreCase);

    private readonly object _residentLock = new();

    private Chapter? _resident;

    /// <summary>A cabinet layer over <paramref name="inner"/>.</summary>
    public CabinetSource(IGameFileSource inner)
    {
        Inner = inner;
    }

    /// <summary>The source the cabinets and every other file are read from.</summary>
    public IGameFileSource Inner { get; }

    /// <inheritdoc/>
    public string Location => Inner.Location;

    /// <summary>The bytes the chapters unpacked so far hold in memory.</summary>
    public long UnpackedBytes => _chapters.Values
        .Where(c => c.IsValueCreated && c.Value != null)
        .SelectMany(c => c.Value!.Folders)
        .Where(f => f.IsValueCreated)
        .Sum(f => (long)f.Value.Length);

    /// <inheritdoc/>
    public bool Exists(string path) =>
        Find(path, out var chapter, out string member) ? chapter.Files.ContainsKey(member) : Inner.Exists(path);

    /// <inheritdoc/>
    public Stream? Open(string path)
    {
        if (!Find(path, out var chapter, out string member))
        {
            return Inner.Open(path);
        }

        if (!chapter.Files.TryGetValue(member, out var file))
        {
            return null;
        }

        lock (_residentLock)
        {
            // Only the chapter last read stays unpacked: a launch can visit all eight, about
            // 400 MB, and a session reads one. A stream already handed out keeps its bytes.
            if (_resident != chapter)
            {
                _resident?.Release();
                _resident = chapter;
            }
        }

        byte[] data = chapter.Folders[file.Folder].Value;
        if (file.Offset + file.Length > data.Length)
        {
            throw new InvalidDataException($"{chapter.Cabinet.Name}: '{file.Name}' runs past its folder's {data.Length} bytes");
        }

        return new MemoryStream(data, (int)file.Offset, (int)file.Length, writable: false);
    }

    // A path is a cabinet's when its chapter has one; the member is the rest of the path.
    private bool Find(string path, out Chapter chapter, out string member)
    {
        var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        chapter = null!;
        member = string.Empty;
        if (parts.Length < 3 || !parts[0].Equals("ZBD", StringComparison.OrdinalIgnoreCase)
            || _chapters.GetOrAdd(parts[1], name => new Lazy<Chapter?>(() => Chapter.Read(Inner, name))).Value is not { } found)
        {
            return false;
        }

        chapter = found;
        member = string.Join('/', parts.Skip(2));
        return true;
    }

    private sealed class Chapter
    {
        private readonly Func<int, Lazy<byte[]>> _unpack;

        private Chapter(Cabinet cabinet, Dictionary<string, CabinetFile> files, Func<int, Lazy<byte[]>> unpack)
        {
            Cabinet = cabinet;
            Files = files;
            _unpack = unpack;
            Folders = Enumerable.Range(0, cabinet.FolderCount).Select(unpack).ToArray();
        }

        public Cabinet Cabinet { get; }

        // Keyed by the path below the chapter folder, in the install's spelling.
        public Dictionary<string, CabinetFile> Files { get; }

        public Lazy<byte[]>[] Folders { get; }

        public static Chapter? Read(IGameFileSource inner, string chapter)
        {
            string path = $"ZBD/{chapter}.CAB";
            Cabinet cabinet;
            using (var stream = inner.Open(path))
            {
                if (stream == null)
                {
                    return null;
                }

                cabinet = Cabinet.Read(stream, $"{inner.Location}/{path}");
            }

            var renames = TierNames(cabinet, chapter);
            var files = new Dictionary<string, CabinetFile>(StringComparer.OrdinalIgnoreCase);
            string prefix = MemberPrefix + chapter + "/";
            foreach (var file in cabinet.Files)
            {
                string stored = file.Name.Replace('\\', '/');
                if (!stored.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"{cabinet.Name}: '{file.Name}' is not below GAME\\ZBD\\{chapter}");
                }

                string member = stored[prefix.Length..];
                if (renames.TryGetValue(file.Name, out string? name))
                {
                    member = member[..(member.LastIndexOf('/') + 1)] + name;
                }

                files.TryAdd(member, file);
            }

            return new Chapter(cabinet, files, i => new Lazy<byte[]>(() =>
            {
                using var stream = inner.Open(path) ?? throw new FileNotFoundException($"'{path}' left the game files at '{inner.Location}'", path);
                return cabinet.Unpack(stream, i);
            }));
        }

        // Drops the unpacked folders; the next open unpacks them again.
        public void Release()
        {
            for (int i = 0; i < Folders.Length; i++)
            {
                Folders[i] = _unpack(i);
            }
        }

        // The tiers' 8.3 aliases, in cabinet order, pair with the chapter's tier names sorted
        // ordinally. The pairing matches each tier's size and bytes in the English install.
        private static Dictionary<string, string> TierNames(Cabinet cabinet, string chapter)
        {
            var aliases = cabinet.Files.Select(f => f.Name).Where(IsAlias).ToList();
            var tiers = ZbdManifest.Expected
                .Where(p => p.StartsWith(chapter + "/", StringComparison.OrdinalIgnoreCase) && p.IndexOf('/', chapter.Length + 1) < 0)
                .Select(p => p[(chapter.Length + 1)..])
                .Where(n => n.StartsWith("rtexture", StringComparison.Ordinal))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            if (aliases.Count == 0)
            {
                return new Dictionary<string, string>();
            }

            if (aliases.Count != tiers.Count)
            {
                throw new InvalidDataException($"{cabinet.Name}: {aliases.Count} aliased texture tiers, expected {tiers.Count} for chapter {chapter}");
            }

            return aliases.Zip(tiers).ToDictionary(p => p.First, p => p.Second);
        }

        private static bool IsAlias(string name)
        {
            string file = name[(name.LastIndexOfAny(new[] { '/', '\\' }) + 1)..];
            return file.Contains('~') && file.StartsWith("RT", StringComparison.OrdinalIgnoreCase);
        }
    }
}
