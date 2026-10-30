using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using CSVM.Extraction;

namespace CSVM.Mech3;

/// <summary>One engine boot script of <c>interp.zbd</c>: its build-tree name
/// (<c>support\c1\ia1.gw</c>) and its command lines in execution order.</summary>
public sealed record InterpScript(string Name, IReadOnlyList<string> Lines);

/// <summary>
/// The engine boot scripts of <c>interp.zbd</c>, read from the game files. Every interp consumer
/// (<see cref="MissionSetup"/>, the clutter registry, the lens flare slots, the mip bias) looks
/// its script up here by name. Layout and script families: docs/formats/interp.md.
/// </summary>
public sealed class InterpScripts
{
    /// <summary>Where the file lives in an install.</summary>
    public const string GamePath = "ZBD/interp.zbd";

    private const uint Signature = 0x08971119;
    private const uint Version = 7;
    private const int NameLength = 120;
    private const int EntrySize = NameLength + 8;

    private readonly List<InterpScript> _scripts;

    public InterpScripts(List<InterpScript> scripts) => _scripts = scripts;

    /// <summary>Every script in file order.</summary>
    public IReadOnlyList<InterpScript> Scripts => _scripts;

    /// <summary>The scripts of <paramref name="game"/>'s <c>interp.zbd</c>, or null without a
    /// source or without the file, which every consumer reads as "nothing authored".</summary>
    public static InterpScripts? Load(IGameFileSource? game) =>
        game != null && game.Exists(GamePath) ? Read(game.ReadAllBytes(GamePath)) : null;

    /// <summary>The scripts of unzbd's <c>interp.json</c>, the modding path: each object's
    /// <c>name</c> and <c>lines</c>, in file order.</summary>
    public static InterpScripts FromExtracted(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
        var scripts = new List<InterpScript>();
        foreach (var script in doc.RootElement.EnumerateArray())
        {
            var lines = new List<string>();
            foreach (var line in script.GetProperty("lines").EnumerateArray())
            {
                lines.Add(line.GetString() ?? "");
            }

            scripts.Add(new InterpScript(script.GetProperty("name").GetString() ?? "", lines));
        }

        return new InterpScripts(scripts);
    }

    /// <summary>Decodes a whole <c>interp.zbd</c>. Throws <see cref="InvalidDataException"/> on a
    /// file that is not one, or whose script table and bodies do not tile it exactly.</summary>
    public static InterpScripts Read(byte[] data)
    {
        int pos = 0;
        Expect(U32(data, ref pos) == Signature, "signature", 0);
        Expect(U32(data, ref pos) == Version, "version", 4);
        uint count = U32(data, ref pos);
        Expect(count <= (data.Length - 12) / EntrySize, "script count", 8);

        var entries = new List<(string Name, uint Start)>((int)count);
        for (int i = 0; i < count; i++)
        {
            var raw = data.AsSpan(pos, NameLength);
            int end = raw.IndexOf((byte)0);
            string name = Encoding.UTF8.GetString(end < 0 ? raw : raw[..end]);
            pos += NameLength + 4; // the timestamp no consumer reads
            entries.Add((name, U32(data, ref pos)));
        }

        var scripts = new List<InterpScript>(entries.Count);
        foreach (var (name, start) in entries)
        {
            Expect(start == pos, $"start of {name}", pos);
            var lines = new List<string>();
            for (uint size; (size = U32(data, ref pos)) != 0;)
            {
                uint args = U32(data, ref pos);
                Expect(size <= data.Length - pos, "line size", pos - 8);
                var bytes = data.AsSpan(pos, (int)size);
                pos += (int)size;

                // Arguments are null-separated and the line null-terminated, so the nulls count the
                // arguments; the JSON extraction spelled each separator as a space.
                Expect(bytes.Count((byte)0) == args && bytes[^1] == 0, "argument count", pos - (int)size);
                lines.Add(Encoding.UTF8.GetString(bytes[..^1]).Replace('\0', ' '));
            }

            scripts.Add(new InterpScript(name, lines));
        }

        Expect(pos == data.Length, "end of file", pos);
        return new InterpScripts(scripts);
    }

    /// <summary>The lines of the script named <paramref name="name"/>, matched without regard to
    /// case, or null when the install has no such script.</summary>
    public IReadOnlyList<string>? Lines(string name)
    {
        foreach (var script in _scripts)
        {
            if (string.Equals(script.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return script.Lines;
            }
        }

        return null;
    }

    private static uint U32(byte[] data, ref int pos)
    {
        Expect(pos + 4 <= data.Length, "a field past the end", pos);
        uint v = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos));
        pos += 4;
        return v;
    }

    private static void Expect(bool ok, string what, int offset)
    {
        if (!ok)
        {
            throw new InvalidDataException($"interp.zbd: bad {what} at offset {offset}");
        }
    }
}
