using System;
using System.Collections.Generic;
using CSVM.Extraction;
using static CSVM.Mech3.AnimZbdEvents;

namespace CSVM.Mech3;

/// <summary>
/// Reads a chapter's <c>cam_anim.zbd</c> or a mission's <c>mis_anim.zbd</c> into the
/// <see cref="AnimArchive"/> model, engine-free. Each definition and script is rebuilt as the
/// property tree the model's own <c>Parse</c> takes, so the model cannot drift from it. Every
/// script is decoded up front. Layout and rules: docs/formats/anim-definitions/compiled-archives.md.
/// </summary>
public static class AnimZbd
{
    private const uint Signature = 0x08170616;
    private const uint Version = 53;
    private const int InfoSize = 108;
    private const int DefSize = 272;
    private const int SequenceHeaderSize = 64;
    private const int ScriptHeaderSize = 28;
    private const int ChannelSize = 76;

    private static readonly string[] Activations = { "WeaponHit", "CollideHit", "WeaponOrCollideHit", "OnCall", "OnStartup" };

    /// <summary>The archive at <paramref name="path"/> (<c>ZBD/C1/cam_anim.zbd</c>,
    /// <c>ZBD/C1/M02/mis_anim.zbd</c>), or null without a source or without the file.</summary>
    public static AnimArchive? Load(IGameFileSource? game, string path, string label) =>
        game != null && game.Exists(path) ? Read(game.ReadAllBytes(path), label) : null;

    /// <summary>Decodes a whole archive. Throws <see cref="System.IO.InvalidDataException"/> on a
    /// file that is not one, or whose records do not tile it exactly.</summary>
    public static AnimArchive Read(byte[] data, string label)
    {
        var r = new AnimBytes(data, 0);
        Expect(data.Length >= 16 && r.U32(0) == Signature && r.U32(4) == Version, "header", 0);
        int pos = 16 + (r.I32(8) * 132) + (r.I32(12) * 84);
        var info = new AnimBytes(data, pos);
        int defCount = info.U16(10), scriptCount = info.I32(16);
        Expect(defCount > 0 && scriptCount >= 0, "anim info", pos);

        // The first record is the reserved placeholder, with no support arrays and no name.
        pos += InfoSize + DefSize;
        var defNames = new HashSet<string>(StringComparer.Ordinal);
        var defs = new List<AnimDefinition>(defCount);
        for (int i = 1; i < defCount; i++)
        {
            var (fileName, tree) = ReadDef(data, ref pos);
            defs.Add(AnimDefinition.Parse(new AnimData(tree), Rename(defNames, fileName)));
        }

        var scripts = ReadScripts(data, ref pos, scriptCount);
        Expect(pos == data.Length, "end of file", pos);
        return AnimArchive.FromDecoded(label, defs, scripts);
    }

    /// <summary>The file name a definition or script is keyed by: its own, or a repeat's
    /// <see cref="ZbdArchive.Rename"/>.</summary>
    internal static string Rename(HashSet<string> seen, string name) => ZbdArchive.Rename(seen, name) ?? name;

    /// <summary>A script's name in the archive's pool: its source path without the
    /// <c>.zan</c> and leading <c>..\</c>, backslashes as hyphens, then the object name.</summary>
    internal static string ScriptFileName(string scriptName, string objectName)
    {
        string stem = scriptName.EndsWith(".zan", StringComparison.Ordinal) ? scriptName[..^4] : scriptName;
        return $"{stem.TrimStart('.', '\\').Replace('\\', '-')}-{objectName}.zan";
    }

    private static (string FileName, Dictionary<string, object?> Tree) ReadDef(byte[] data, ref int pos)
    {
        var d = new AnimBytes(data, pos);
        Expect(pos + DefSize <= data.Length, "anim def", pos);
        pos += DefSize;
        string animName = d.Name(0, 32), name = d.Name(40, 32), rootName = d.Name(76, 32);
        uint flags = d.U32(156);
        int activation = d.U8(161);
        Expect(activation < Activations.Length, "activation", d.At + 161);

        var names = new AnimZbdNames();
        pos += d.I32(36) * 36; // the compiled NAME1 node path, which the model does not read
        var objects = Refs(data, ref pos, d.U8(217), 92, 0, 36, null);
        var nodes = Refs(data, ref pos, d.U8(218), 44, 4, 40, names.Nodes, numberRepeats: true);
        Refs(data, ref pos, d.U8(219), 44, 0, 36, names.Lights);
        Refs(data, ref pos, d.U8(220), 44, 0, 36, names.Puffers);
        Refs(data, ref pos, d.U8(221), 44, 0, 36, names.DynamicSounds);
        Refs(data, ref pos, d.U8(222), 40, 0, -1, names.StaticSounds);
        var prereqs = Prereqs(data, ref pos, d.U8(224));
        pos += d.U8(226) * 72; // the anim refs, which events name only by their index
        List<object?>? scriptIds = null;
        if (d.U8(227) > 0)
        {
            scriptIds = new List<object?>();
            for (int i = 0; i < d.U8(227); i++, pos += 4)
            {
                scriptIds.Add(N(new AnimBytes(data, pos).U32(0)));
            }
        }

        var tree = Bag(
            ("name", name),
            ("anim_name", animName),
            ("anim_root_name", rootName),
            ("activation", Activations[activation]),
            ("execution", (flags & 0x02) != 0 ? Bag(("ByRange", d.Range(164))) : "None"),
            ("health", F(d.F32(180))),
            ("activ_prereq_min_to_satisfy", N(d.U8(225))),
            ("si_script_ids", scriptIds),
            ("objects", objects),
            ("nodes", nodes),
            ("activ_prereqs", prereqs));
        if (d.U32(208) != 0)
        {
            tree["reset_state"] = Sequence(data, ref pos, names, named: false);
        }

        var sequences = new List<object?>();
        for (int i = 0; i < d.U8(216); i++)
        {
            sequences.Add(Sequence(data, ref pos, names));
        }

        tree["sequences"] = sequences;
        if (d.U32(212) != 0)
        {
            tree["unknown_seq"] = Sequence(data, ref pos, names);
        }

        static string Bare(string s) => s.EndsWith(".flt", StringComparison.Ordinal) ? s[..^4] : s;
        string fileName = Bare(name) != Bare(rootName)
            ? $"{Bare(name)}-{Bare(animName)}-{Bare(rootName)}"
            : $"{Bare(name)}-{Bare(animName)}";
        return (fileName, tree);
    }

    // A support array whose first record is an empty placeholder. Names land in `into`; with a
    // pointer offset the records also come back as {name, ptr}. A repeated node name is
    // suffixed ~2, ~3, as the extraction spells it, since events reach nodes by index.
    private static List<object?>? Refs(byte[] data, ref int pos, int count, int size, int nameAt, int ptrAt, List<string>? into, bool numberRepeats = false)
    {
        Expect(pos + (count * size) <= data.Length, "support array", pos);
        List<object?>? refs = count > 1 && ptrAt >= 0 ? new List<object?>() : null;
        var seen = new List<string>();
        for (int i = 1; i < count; i++)
        {
            var r = new AnimBytes(data, pos + (i * size));
            string name = r.Name(nameAt, ptrAt < 0 ? size : 32); // a static sound's name fills its record
            if (numberRepeats)
            {
                int repeats = seen.FindAll(n => n == name).Count;
                seen.Add(name);
                name = repeats > 0 ? $"{name}~{repeats + 1}" : name;
            }

            into?.Add(name);
            refs?.Add(Bag(("name", name), ("ptr", N(r.U32(ptrAt)))));
        }

        pos += count * size;
        return refs;
    }

    private static List<object?>? Prereqs(byte[] data, ref int pos, int count)
    {
        if (count == 0)
        {
            return null;
        }

        Expect(pos + (count * 48) <= data.Length, "activation prerequisites", pos);
        var list = new List<object?>(count);
        for (int i = 0; i < count; i++, pos += 48)
        {
            var p = new AnimBytes(data, pos);
            bool required = p.U32(0) == 0;
            uint kind = p.U32(4), active = p.U32(8);
            list.Add(kind switch
            {
                1 => Bag(("Animation", Bag(("name", p.Name(8, 32)), ("required", required)))),
                2 => Bag(("Object", Bag(("name", p.Name(12, 32)), ("required", required), ("active", active != 0), ("ptr", N(p.U32(44))), ("active_raw", N(active))))),
                3 => Bag(("Parent", Bag(("name", p.Name(12, 32)), ("required", required), ("active", active != 0), ("ptr", N(p.U32(44)))))),
                _ => throw new System.IO.InvalidDataException($"anim: bad activation prerequisite type {kind} at offset {pos + 4}"),
            });
        }

        return list;
    }

    // A 64-byte sequence header (name, state, event bytes at +60) and its events. The reset
    // sequence's fixed RESET_SEQUENCE header is not part of the model.
    private static Dictionary<string, object?> Sequence(byte[] data, ref int pos, AnimZbdNames names, bool named = true)
    {
        Expect(pos + SequenceHeaderSize <= data.Length, "sequence", pos);
        var h = new AnimBytes(data, pos);
        int size = h.I32(60);
        var events = AnimZbdEvents.Read(data, pos + SequenceHeaderSize, size, names);
        pos += SequenceHeaderSize + size;
        return named ? Bag(("name", h.Name(0, 32)), ("seq_state", h.U8(32) == 3 ? "OnCall" : "Initial"), ("events", events)) : Bag(("events", events));
    }

    private static List<(string Name, SiScript Script)> ReadScripts(byte[] data, ref int pos, int count)
    {
        int headers = pos;
        Expect(pos + (count * ScriptHeaderSize) <= data.Length, "script headers", pos);
        pos += count * ScriptHeaderSize;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var scripts = new List<(string, SiScript)>(count);
        for (int i = 0; i < count; i++)
        {
            var h = new AnimBytes(data, headers + (i * ScriptHeaderSize));
            int nameLength = h.U8(8), objectLength = h.U8(9), frameCount = h.I32(16), length = h.I32(20);
            Expect(pos + nameLength + objectLength + length <= data.Length, "script", pos);
            string scriptName = new AnimBytes(data, pos).Name(0, nameLength);
            string objectName = new AnimBytes(data, pos + nameLength).Name(0, objectLength);
            pos += nameLength + objectLength;
            int end = pos + length;
            var frames = new List<object?>(frameCount);
            for (int f = 0; f < frameCount; f++)
            {
                frames.Add(Frame(data, ref pos, end));
            }

            Expect(pos == end, "script length", pos);
            string fileName = Rename(seen, ScriptFileName(scriptName, objectName));
            var tree = Bag(("script_name", scriptName), ("object_name", objectName), ("spline_interp", h.U32(12) != 0), ("frames", frames));
            scripts.Add((fileName, SiScript.Parse(new AnimData(tree), fileName)));
        }

        return scripts;
    }

    // A frame: {flags, start, end}, then one 76-byte block per set flag (translate, rotate, scale).
    private static Dictionary<string, object?> Frame(byte[] data, ref int pos, int end)
    {
        Expect(pos + 12 <= end, "frame", pos);
        var f = new AnimBytes(data, pos);
        uint flags = f.U32(0);
        var frame = Bag(("start_time", F(f.F32(4))), ("end_time", F(f.F32(8))));
        pos += 12;
        foreach (var (key, flag) in new[] { ("translate", 1u), ("rotate", 2u), ("scale", 4u) })
        {
            if ((flags & flag) == 0)
            {
                continue;
            }

            Expect(pos + ChannelSize <= end, "frame channel", pos);
            var c = new AnimBytes(data, pos);
            var channel = Bag(
                ("base", flag == 2 ? Bag(("x", F(c.F32(0))), ("y", F(c.F32(4))), ("z", F(c.F32(8))), ("w", F(c.F32(12)))) : c.Vec(0)),
                ("delta", FiniteOrZero(c, 16)),
                ("spline_x", Convert.ToBase64String(data, pos + 28, 16)),
                ("spline_y", Convert.ToBase64String(data, pos + 44, 16)),
                ("spline_z", Convert.ToBase64String(data, pos + 60, 16)));
            frame[key] = channel;
            pos += ChannelSize;
        }

        return frame;
    }

    // ⚠ Do not keep a non-finite delta. The extraction moved such a vector aside and zeroed it,
    // so the model holds +0 in all three components (the six NaNs of C5/M02 carneypkup_cam).
    private static Dictionary<string, object?> FiniteOrZero(AnimBytes c, int at)
    {
        bool finite = float.IsFinite(c.F32(at)) && float.IsFinite(c.F32(at + 4)) && float.IsFinite(c.F32(at + 8));
        return finite ? c.Vec(at) : Bag(("x", 0f), ("y", 0f), ("z", 0f));
    }
}
