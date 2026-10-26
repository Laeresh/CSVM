using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CSVM.Extraction;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Tooling;

/// <summary>
/// <c>--dump</c>: the game files read through the C# readers, their models written as JSON, PNG
/// and WAV for analysis scripts and agents. Each archive lands where <c>extracted/</c> kept it
/// (<c>ZBD/C1/M02/zrdr.zbd</c> in <c>C1/M02/zrdr/</c>); the shape is the models' own, laid out in
/// docs/architecture/Tooling.md. ⚠ Nothing in the engine may read a dump or fall back onto one.
/// It is a copy for people, and the readers are the only load path. Engine-free, so a unit test
/// dumps the install it finds.
/// </summary>
public static class DumpGameFiles
{
    /// <summary>The dump folder, relative to the run tree, when the flag names none.</summary>
    public const string DefaultFolder = ".scratch/dump";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        IncludeFields = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { DropArchiveBackReference } },
        Converters =
        {
            new JsonStringEnumConverter(),
            new Floats<Vector2>(v => new[] { v.X, v.Y }),
            new Floats<Vector3>(v => new[] { v.X, v.Y, v.Z }),
            new Floats<Quaternion>(q => new[] { q.X, q.Y, q.Z, q.W }),
            new Floats<Color>(c => new[] { c.R, c.G, c.B, c.A }),
            new Floats<Transform3D>(t => new[]
            {
                t.Basis.X.X, t.Basis.X.Y, t.Basis.X.Z, t.Basis.Y.X, t.Basis.Y.Y, t.Basis.Y.Z,
                t.Basis.Z.X, t.Basis.Z.Y, t.Basis.Z.Z, t.Origin.X, t.Origin.Y, t.Origin.Z,
            }),
            new AnimDataProps(),
        },
    };

    /// <summary>The game-relative files a whole dump reads: <c>strings.dll</c> and every archive of
    /// <see cref="ZbdManifest.Expected"/>, or with <paramref name="chapter"/> the root archives and
    /// that chapter's alone.</summary>
    public static IReadOnlyList<string> Files(string? chapter) =>
        new[] { Messages.GameFile }
            .Concat(ZbdManifest.Expected
                .Where(p => chapter == null || !p.Contains('/') || p.StartsWith(chapter + "/", StringComparison.OrdinalIgnoreCase))
                .Select(p => "ZBD/" + p))
            .ToList();

    /// <summary>Dumps every file of <see cref="Files"/> into <paramref name="outDir"/>. False with
    /// a logged reason without a source, for a chapter no archive names, or when
    /// <see cref="Write"/> is.</summary>
    public static bool Run(IGameFileSource? game, string outDir, string? chapter)
    {
        var files = Files(chapter);
        if (chapter != null && !files.Any(f => f.Contains('/' + chapter + '/', StringComparison.OrdinalIgnoreCase)))
        {
            Log.Warn("core", $"--dump: no archive belongs to chapter '{chapter}'; nothing written");
            return false;
        }

        return Write(game, outDir, files);
    }

    /// <summary>Dumps the game-relative <paramref name="files"/> into <paramref name="outDir"/>,
    /// replacing what is there under the same names. False with a logged reason without a source,
    /// or when a file is absent or refused; the others are dumped regardless.</summary>
    public static bool Write(IGameFileSource? game, string outDir, IEnumerable<string> files)
    {
        if (game == null)
        {
            Log.Warn("core", $"--dump: no game files (no install found, or a --synthetic-data run); nothing written");
            return false;
        }

        bool ok = true;
        foreach (string file in files)
        {
            if (!game.Exists(file))
            {
                Log.Warn("core", $"--dump: {file} is not in the game files at {game.Location}");
                ok = false;
                continue;
            }

            try
            {
                Log.Info("core", $"--dump: {file}, {WriteOne(game, file, outDir)} files");
            }
            catch (InvalidDataException e)
            {
                Log.Error("core", $"--dump: {file} does not read: {e.Message}");
                ok = false;
            }
        }

        Log.Info("core", $"--dump: wrote {outDir}");
        return ok;
    }

    /// <summary>A message table as its dump writes it, key to text in ordinal key order.</summary>
    internal static SortedDictionary<string, string> Table(Messages messages) =>
        new(messages.Entries.ToDictionary(e => e.Key, e => e.Value), StringComparer.Ordinal);

    /// <summary>A model as its dump's JSON writes it.</summary>
    internal static string ToJson<T>(T value) => JsonSerializer.Serialize(value, Json);

    private static int WriteOne(IGameFileSource game, string file, string outDir)
    {
        byte[] data = game.ReadAllBytes(file);
        if (file.Equals(Messages.GameFile, StringComparison.OrdinalIgnoreCase))
        {
            return WriteJson(Path.Combine(outDir, "messages.json"), Table(Messages.FromStringsDll(data)));
        }

        // ZBD/C1/M02/zrdr.zbd dumps into <out>/C1/M02/zrdr/, as extracted/ laid it out.
        string label = Path.ChangeExtension(file.StartsWith("ZBD/", StringComparison.OrdinalIgnoreCase) ? file[4..] : file, null);
        string folder = Path.Combine(outDir, label);
        switch (Path.GetFileName(label).ToLowerInvariant())
        {
            case "interp":
                return WriteJson(folder + ".json", InterpScripts.Read(data).Scripts);
            case "gamez":
            case "planes":
                var gamez = GameZZbd.Read(data);
                return WriteJson(Path.Combine(folder, "nodes.json"), gamez.Nodes)
                    + WriteJson(Path.Combine(folder, "meshes.json"), gamez.Meshes)
                    + WriteJson(Path.Combine(folder, "materials.json"), gamez.Materials)
                    + WriteJson(Path.Combine(folder, "textures.json"), gamez.TextureNames);
            case "cam_anim":
            case "mis_anim":
                var anim = AnimZbd.Read(data, label);
                return WriteJson(Path.Combine(folder, "definitions.json"), anim.Defs)
                    + WriteJson(Path.Combine(folder, "scripts.json"), Enumerable.Range(0, anim.ScriptCount).Select(anim.Script));
            case "zrdr":
                var readers = ZrdrZbd.Read(data);
                return readers.Sum(r => WriteJson(Path.Combine(folder, r.Key + ".json"), r.Root));
            case "soundsh":
            case "soundsl":
                using (var sounds = SoundZbd.Read(new MemoryStream(data), file))
                {
                    Directory.CreateDirectory(folder);
                    foreach (var sound in sounds.Sounds)
                    {
                        File.WriteAllBytes(Path.Combine(folder, sound.Key), sounds.ReadWavBytes(sound));
                    }

                    return sounds.Sounds.Count;
                }

            default:
                return WriteTextures(folder, TextureZbd.Read(data));
        }
    }

    private static int WriteTextures(string folder, IReadOnlyList<TextureImage> textures)
    {
        Directory.CreateDirectory(folder);
        foreach (var t in textures)
        {
            File.WriteAllBytes(
                Path.Combine(folder, t.Key + ".png"),
                t.HasAlphaChannel ? PngWriter.EncodeRgba(t.Width, t.Height, t.Pixels) : PngWriter.EncodeRgb(t.Width, t.Height, t.Pixels));
        }

        var manifest = textures.Select(t => new { t.Name, t.Rename, t.Key, t.Width, t.Height, t.Alpha, t.RenderFlags, t.HasAlphaChannel });
        return textures.Count + WriteJson(Path.Combine(folder, "manifest.json"), manifest);
    }

    private static int WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, value, Json);
        return 1;
    }

    // A definition points back at its archive, which holds the definition: a cycle.
    private static void DropArchiveBackReference(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        for (int i = info.Properties.Count - 1; i >= 0; i--)
        {
            if (info.Properties[i].PropertyType == typeof(AnimArchive))
            {
                info.Properties.RemoveAt(i);
            }
        }
    }

    // A Godot value as a flat array of its components. Write-only: the dump is never read back.
    private sealed class Floats<T> : JsonConverter<T>
    {
        private readonly Func<T, float[]> _parts;

        public Floats(Func<T, float[]> parts) => _parts = parts;

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException("a dump is never read back");

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, _parts(value), options);
    }

    // An event payload as the property bag it is.
    private sealed class AnimDataProps : JsonConverter<AnimData>
    {
        public override AnimData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException("a dump is never read back");

        public override void Write(Utf8JsonWriter writer, AnimData value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.Props, options);
    }
}
