using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CSVM.Mech3;

/// <summary>Little-endian reads at offsets relative to one record, bounds-checked by the array.</summary>
internal readonly struct AnimBytes
{
    public readonly byte[] Data;
    public readonly int At;

    public AnimBytes(byte[] data, int at)
    {
        Data = data;
        At = at;
    }

    public int U8(int offset) => Data[At + offset];

    public int U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan(At + offset));

    public int I16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(Data.AsSpan(At + offset));

    public int I32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Data.AsSpan(At + offset));

    public uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(At + offset));

    public float F32(int offset) => BinaryPrimitives.ReadSingleLittleEndian(Data.AsSpan(At + offset));

    /// <summary>A fixed-width name field up to its first NUL; whatever follows the NUL is padding.</summary>
    public string Name(int offset, int width)
    {
        var raw = Data.AsSpan(At + offset, width);
        int nul = raw.IndexOf((byte)0);
        AnimZbdEvents.Expect(nul >= 0, "unterminated name", At + offset);
        foreach (byte b in raw[..nul])
        {
            AnimZbdEvents.Expect(b < 0x80, "name", At + offset);
        }

        return Encoding.ASCII.GetString(raw[..nul]);
    }

    public Dictionary<string, object?> Vec(int offset) => AnimZbdEvents.Bag(
        ("x", AnimZbdEvents.F(F32(offset))), ("y", AnimZbdEvents.F(F32(offset + 4))), ("z", AnimZbdEvents.F(F32(offset + 8))));

    public Dictionary<string, object?> Range(int offset) => AnimZbdEvents.Bag(
        ("min", AnimZbdEvents.F(F32(offset))), ("max", AnimZbdEvents.F(F32(offset + 4))));

    public Dictionary<string, object?> Color(int offset) => AnimZbdEvents.Bag(
        ("r", AnimZbdEvents.F(F32(offset))), ("g", AnimZbdEvents.F(F32(offset + 4))), ("b", AnimZbdEvents.F(F32(offset + 8))));
}

/// <summary>
/// The sequence events of a compiled anim archive, decoded into the payload trees that
/// <see cref="AnimEvent.Data"/> holds. Every field of a record is present, an absent option is
/// null, and a union is a one-key bag. Node, light, puffer and sound indices resolve
/// through the definition's support arrays (<see cref="AnimZbdNames"/>). Layouts:
/// docs/formats/anim-definitions/compiled-archives.md.
/// </summary>
internal static class AnimZbdEvents
{
    internal const string InputNode = "INPUT_NODE";
    internal const string MainRootNode = "MAIN_ROOT_NODE";

    private const int HeaderSize = 12;
    private static readonly string[] StartOffsets = { "", "Animation", "Sequence", "Event" };
    private static readonly byte[] DefaultFogName = Encoding.ASCII.GetBytes("default_fog_name\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0");

    // Each event type's union tag and payload size; -1 is a payload kept as raw bytes.
    private static readonly Dictionary<int, (string Kind, int Size)> Kinds = new()
    {
        [1] = ("Sound", 16),
        [2] = ("SoundNode", 60),
        [4] = ("LightState", 140),
        [5] = ("LightAnimation", 100),
        [6] = ("ObjectActiveState", 8),
        [7] = ("ObjectTranslateState", 20),
        [8] = ("ObjectScaleState", 16),
        [9] = ("ObjectRotateState", 20),
        [10] = ("ObjectMotion", 320),
        [11] = ("ObjectMotionFromTo", 132),
        [12] = ("ObjectMotionSiScript", 64),
        [13] = ("ObjectOpacityState", 12),
        [14] = ("ObjectOpacityFromTo", 24),
        [15] = ("ObjectAddChild", 4),
        [16] = ("ObjectDeleteChild", 4),
        [17] = ("ObjectCycleTexture", 8),
        [20] = ("CameraState", 36),
        [22] = ("CallSequence", 36),
        [23] = ("StopSequence", 36),
        [24] = ("CallAnimation", 68),
        [25] = ("StopAnimation", 36),
        [26] = ("ResetAnimation", 36),
        [27] = ("InvalidateAnimation", 36),
        [28] = ("FogState", 68),
        [30] = ("Loop", 8),
        [31] = ("If", 16),
        [32] = ("Else", 0),
        [33] = ("Elseif", 16),
        [34] = ("Endif", 0),
        [35] = ("Callback", 4),
        [36] = ("FbfxColorFromTo", 52),
        [41] = ("DetonateWeapon", 24),
        [42] = ("PufferState", 584),
        [46] = ("SoundAdjust", -1),
        [47] = (AnimDefinition.AllNamesKind, -1),
    };

    /// <summary>The events of one sequence, <paramref name="length"/> bytes at
    /// <paramref name="at"/>, as the list a sequence bag's <c>events</c> holds.</summary>
    public static List<object?> Read(byte[] data, int at, int length, AnimZbdNames names)
    {
        var events = new List<object?>();
        int end = at + length;
        Expect(end <= data.Length, "sequence length", at);
        while (at < end)
        {
            Expect(at + HeaderSize <= end, "event header", at);
            var h = new AnimBytes(data, at);
            int type = h.U8(0), offset = h.U8(1), size = h.I32(4);
            float time = h.F32(8);
            Expect(offset is >= 1 and <= 3 && h.U16(2) == 0 && size >= HeaderSize && at + size <= end, "event header", at);
            Expect(Kinds.TryGetValue(type, out var kind), "event type " + type, at);
            Expect(kind.Size < 0 || kind.Size == size - HeaderSize, kind.Kind + " size", at);
            var payload = Payload(type, new AnimBytes(data, at + HeaderSize), size - HeaderSize, names);

            // A start at the animation's own time zero is the default and carries no start.
            object? start = offset == 1 && time == 0f ? null : Bag(("offset", StartOffsets[offset]), ("time", F(time)));
            events.Add(Bag(("start", start), ("data", Bag((kind.Kind, payload)))));
            at += size;
        }

        return events;
    }

    internal static Dictionary<string, object?> Bag(params (string Key, object? Value)[] fields)
    {
        var bag = new Dictionary<string, object?>(fields.Length, StringComparer.Ordinal);
        foreach (var (key, value) in fields)
        {
            bag[key] = value;
        }

        return bag;
    }

    /// <summary>A float as the extraction's JSON carries it: a non-finite value has no JSON form
    /// and reads back as null.</summary>
    internal static object? F(float value) => float.IsFinite(value) ? value : null;

    internal static object N(long value) => (float)value;

    internal static void Expect(bool ok, string what, long offset)
    {
        if (!ok)
        {
            throw new InvalidDataException($"anim: bad {what} at offset {offset}");
        }
    }

    private static Dictionary<string, object?> Payload(int type, AnimBytes p, int size, AnimZbdNames n)
    {
        switch (type)
        {
            case 1:
                return Bag(("name", n.StaticSound(p.I16(0))), ("at_node", p.I16(2) == 0 ? null : AtNode(p, 2, 4, n)));
            case 2:
                return Bag(("name", p.Name(0, 32)), ("active_state", p.U32(40) != 0), ("translate", Translate(p, p.U32(36), 44, 48, n)));
            case 4:
                return LightState(p, n);
            case 5:
                return Bag(("name", p.Name(0, 32)), ("range", p.Range(36)), ("color", p.Color(60)), ("run_time", F(p.F32(96))),
                    ("range_alt", p.F32(44) == 0f && p.F32(48) == 0f ? null : p.Range(44)));
            case 6:
                int active = p.I16(4);

                // The compiler's two fixed spellings, kept by the extraction as these names.
                string node = active == -1 ? "pltes" : active == -100 ? InputNode : n.Node(active);
                return Bag(("node", node), ("state", p.U32(0) != 0));
            case 7:
                int at = p.I16(18);
                return Bag(("node", n.Node(p.I16(16))), ("relative", p.U32(0) != 0), ("state", p.Vec(4)),
                    ("at_node", at == -200 ? InputNode : at > 0 ? n.Node(at) : null));
            case 8:
                return Bag(("name", n.Node(p.I32(12))), ("state", p.Vec(0)));
            case 9:
                return ObjectRotateState(p, n);
            case 10:
                return ObjectMotion(p, n);
            case 11:
                return ObjectMotionFromTo(p, n);
            case 12:
                return Bag(("name", n.Node(p.I32(4))), ("index", N(p.U32(8))));
            case 13:
                int opaque = p.I16(8);
                return Bag(("name", opaque == -100 ? InputNode : n.Node(opaque)), ("state", p.U16(0) != 0),
                    ("opacity", p.U16(2) != 0 ? F(p.F32(4)) : null));
            case 14:
                return ObjectOpacityFromTo(p, n);
            case 15:
            case 16:
                return Bag(("parent", n.Node(p.I16(0))), ("child", n.Node(p.I16(2))));
            case 17:
                return Bag(("name", n.Node(p.I16(4))), ("reset", N(p.U16(6))));
            case 20:
                uint camera = p.U32(0);
                object? Lens(uint flag, int offset) => (camera & flag) != 0 ? F(p.F32(offset)) : null;
                return Bag(("name", n.Node(p.I32(4))), ("clip_near", Lens(0x01, 8)), ("clip_far", Lens(0x02, 12)),
                    ("lod_multiplier", Lens(0x04, 16)), ("fov_h", Lens(0x08, 20)), ("fov_v", Lens(0x10, 24)),
                    ("zoom_h", Lens(0x20, 28)), ("zoom_v", Lens(0x40, 32)));
            case 22:
            case 23:
            case 25:
            case 26:
                return Bag(("name", p.Name(0, 32)));
            case 24:
                return CallAnimation(p, n);
            case 27:
                return Bag(("name", p.Name(0, 32)), ("anim_def_index", p.I32(32) == 0 ? null : N(p.I32(32))));
            case 28:
                return FogState(p);
            case 30:
                return (p.U32(0) & 1) != 0 ? Bag(("Count", N(p.I16(4)))) : Bag(("RunTime", F(p.F32(4))));
            case 31:
            case 33:
                return Bag(("condition", Condition(p)));
            case 32:
            case 34:
                return Bag();
            case 35:
                return Bag(("value", N(p.U32(0))));
            case 36:
                return FbfxColorFromTo(p);
            case 41:
                return Bag(("weapon", p.Name(0, 10)), ("at_node", AtNode(p, 10, 12, n)));
            case 42:
                return PufferState(p, n);
            case 46:
            case 47:
                return Bag(("data", Convert.ToBase64String(p.Data, p.At, size)));
            default:
                throw new InvalidDataException($"anim: unknown event type {type} at offset {p.At - HeaderSize}");
        }
    }

    // An AT_NODE pair: an INPUT_NODE sentinel or a node index, and a position.
    private static Dictionary<string, object?> AtNode(AnimBytes p, int nodeAt, int posAt, AnimZbdNames n)
    {
        int node = p.I16(nodeAt);
        return Bag(("name", node == -200 ? InputNode : n.Node(node)), ("pos", p.Vec(posAt)));
    }

    // The sound-node and light translate union: AT_NODE (flag 2) over an absolute position (flag 1).
    private static object? Translate(AnimBytes p, uint flags, int nodeAt, int posAt, AnimZbdNames n)
    {
        if ((flags & 2) != 0)
        {
            int node = p.I32(nodeAt);
            return Bag(("AtNode", Bag(("name", node == -200 ? InputNode : n.Node(node)), ("pos", p.Vec(posAt)))));
        }

        return (flags & 1) != 0 ? Bag(("Absolute", p.Vec(posAt))) : null;
    }

    private static Dictionary<string, object?> LightState(AnimBytes p, AnimZbdNames n)
    {
        uint flags = p.U32(36);
        object? Flag(uint flag, int offset) => (flags & flag) != 0 ? p.U32(offset) != 0 : null;
        return Bag(
            ("name", p.Name(0, 32)),
            ("active_state", p.U32(40) != 0),
            ("type_", p.U32(44) == 0 ? "Directed" : "PointSource"),
            ("translate", Translate(p, flags, 72, 76, n)),
            ("directional", Flag(0x80, 48)),
            ("saturated", Flag(0x100, 52)),
            ("subdivide", Flag(0x200, 56)),
            ("lightmap", Flag(0x2000, 60)),
            ("static_", Flag(0x400, 64)),
            ("bicolored", Flag(0x1000, 68)),
            ("orientation", (flags & 0x4) != 0 ? p.Vec(88) : null),
            ("range", (flags & 0x8) != 0 ? p.Range(100) : null),
            ("color", (flags & 0x10) != 0 ? p.Color(108) : null),
            ("ambient_color", (flags & 0x800) != 0 ? p.Color(120) : null),
            ("ambient", (flags & 0x20) != 0 ? F(p.F32(132)) : null),
            ("diffuse", (flags & 0x40) != 0 ? F(p.F32(136)) : null));
    }

    private static Dictionary<string, object?> ObjectRotateState(AnimBytes p, AnimZbdNames n)
    {
        uint flags = p.U32(0);
        int at = p.I16(18);
        string AtName() => at == -200 ? InputNode : n.Node(at);
        object basis = (flags & 2) != 0 ? Bag(("AtNodeXYZ", AtName()))
            : (flags & 4) != 0 ? Bag(("AtNodeMatrix", AtName()))
            : (flags & 1) != 0 ? "Relative" : "Absolute";
        return Bag(("name", n.Node(p.I16(16))), ("state", p.Vec(4)), ("basis", basis));
    }

    private static Dictionary<string, object?> ObjectMotion(AnimBytes p, AnimZbdNames n)
    {
        uint flags = p.U32(0);
        bool Has(uint flag) => (flags & flag) != 0;

        // Only the MIN bit set: the compiler's form of a range, read as the whole range.
        bool minOnly = Has(0x08) && !Has(0x10);
        object? BounceSequence(int offset) => p.U8(offset) == 0 ? null : p.Name(offset, 32);
        object? BounceSound(int indexAt, int volumeAt) =>
            p.I16(indexAt) > 0 ? Bag(("name", n.StaticSound(p.I16(indexAt))), ("volume", F(p.F32(volumeAt)))) : null;
        object? forward = Has(0x80) ? Bag(("Time", Bag(("initial", F(p.F32(112))), ("delta", F(p.F32(116))))))
            : Has(0x40) ? Bag(("Distance", Bag(("initial", F(p.F32(112)))))) : null;
        return Bag(
            ("node", n.Node(p.I16(4))),
            ("impact_force", Has(0x02)),
            ("morph", Has(0x200) ? F(p.F32(16)) : null),
            ("gravity", Has(0x01) ? Bag(("value", F(p.F32(12))), ("complex", Has(0x2000)), ("no_altitude", Has(0x4000)), ("do_intersections", Has(0x8000))) : null),
            ("translation_range", Has(0x08) || Has(0x10)
                ? Bag(("xz", p.Range(20)), ("y", p.Range(28)), ("initial", p.Range(36)), ("delta", p.Range(44)))
                : null),
            ("translation_range_min_only", minOnly),
            ("translation", Has(0x04) ? Bag(("initial", p.Vec(52)), ("delta", p.Vec(64)), ("rnd_xz", p.Vec(100))) : null),
            ("forward_rotation", forward),
            ("xyz_rotation", Has(0x20) ? Bag(("initial", p.Vec(124)), ("delta", p.Vec(136))) : null),
            ("scale", Has(0x100) ? Bag(("initial", p.Vec(160)), ("delta", p.Vec(172))) : null),
            ("bounce_sequence", Has(0x800) ? Bag(("default", BounceSequence(196)), ("water", BounceSequence(236)), ("lava", BounceSequence(276))) : null),
            ("bounce_sound", Has(0x1000) ? Bag(("default", BounceSound(230, 232)), ("water", BounceSound(270, 272)), ("lava", BounceSound(310, 312))) : null),
            ("run_time", Has(0x400) ? F(p.F32(316)) : null));
    }

    private static Dictionary<string, object?> ObjectMotionFromTo(AnimBytes p, AnimZbdNames n)
    {
        uint flags = p.U32(0);
        float runTime = p.F32(128);
        var bag = Bag(
            ("name", n.Node(p.I32(4))),
            ("run_time", F(runTime)),
            ("morph", (flags & 8) != 0 ? Bag(("from", F(p.F32(8))), ("to", F(p.F32(12)))) : null));

        // Each channel keeps its stored delta only where it is not the one from-to recomputes.
        foreach (var (key, flag, at) in new[] { ("translate", 1u, 20), ("rotate", 2u, 56), ("scale", 4u, 92) })
        {
            bool on = (flags & flag) != 0;
            bag[key] = on ? Bag(("from", p.Vec(at)), ("to", p.Vec(at + 12))) : null;
            bool recomputed = true;
            for (int c = 0; c < 3; c++)
            {
                recomputed &= p.F32(at + 24 + (c * 4)) == Delta(p.F32(at + (c * 4)), p.F32(at + 12 + (c * 4)), runTime);
            }

            bag[key + "_delta"] = on && !recomputed ? p.Vec(at + 24) : null;
        }

        return bag;
    }

    private static Dictionary<string, object?> ObjectOpacityFromTo(AnimBytes p, AnimZbdNames n)
    {
        float from = p.F32(8), to = p.F32(12), delta = p.F32(16), runTime = p.F32(20);
        object? State(int offset) => p.I16(offset) switch { -1 => null, 0 => false, _ => true };

        // "DELTA VAL FAIL" in the extraction's log: the stored rate is kept beside from and to.
        return Bag(
            ("name", n.Node(p.I32(0))),
            ("opacity_from", Bag(("opacity", F(from)), ("state", State(4)))),
            ("opacity_to", Bag(("opacity", F(to)), ("state", State(6)))),
            ("run_time", F(runTime)),
            ("opacity_delta", delta == Delta(from, to, runTime) ? null : F(delta)));
    }

    private static Dictionary<string, object?> CallAnimation(AnimBytes p, AnimZbdNames n)
    {
        int operand = p.I16(32), flags = p.U16(34), wait = p.I16(38), node = p.I16(40);
        object? position = (flags & 0x02) != 0 ? p.Vec(44) : null;
        object? translate = (flags & 0x04) != 0 ? p.Vec(56) : null;
        string NodeName() => node == -200 ? InputNode : n.Node(node);
        object? parameters = (flags & 0x01) != 0 ? Bag(("AtNode", Bag(("node", NodeName()), ("position", position), ("translate", translate))))
            : (flags & 0x08) != 0 ? Bag(("WithNode", Bag(("node", NodeName()), ("position", position))))
            : null;
        bool waits = (flags & 0x10) != 0;
        return Bag(
            ("name", p.Name(0, 32)),
            ("operand_node", operand == 0 ? null : n.Node(operand)),
            ("wait_for_completion", waits ? N(wait) : null),
            ("parameters", parameters),
            ("wait_for_raw", !waits && wait != -1 ? N(wait) : null));
    }

    private static Dictionary<string, object?> FogState(AnimBytes p)
    {
        uint flags = p.U32(32);
        bool isDefault = p.Data.AsSpan(p.At, 32).SequenceEqual(DefaultFogName);
        return Bag(
            ("name", isDefault ? null : p.Name(0, 32)),
            ("type_", (flags & 1) != 0 ? p.U32(36) switch { 0 => "Off", 1 => "Linear", _ => "Exponential" } : null),
            ("color", (flags & 2) != 0 ? p.Color(40) : null),
            ("altitude", (flags & 4) != 0 ? p.Range(52) : null),
            ("range", (flags & 8) != 0 ? p.Range(60) : null));
    }

    private static Dictionary<string, object?> Condition(AnimBytes p)
    {
        uint node = p.U32(4);
        return p.U32(0) switch
        {
            1 => Bag(("RandomWeight", F(p.F32(8)))),
            2 => Bag(("PlayerRange", F(p.F32(8)))),
            4 => Bag(("AnimationLod", N(p.U32(8)))),
            16 => Bag(("NodeUndercover", Bag(("node_index", N(node)), ("distance", N(p.U32(8)))))),
            32 => Bag(("HwRender", p.U32(8) != 0)),
            64 => Bag(("PlayerFirstPerson", p.U32(8) != 0)),
            256 => Bag(("NodeBelowAlt", Bag(("node_index", N(node)), ("altitude", F(p.F32(8)))))),
            2048 => Bag(("AnimHealth", F(p.F32(8)))),
            4096 => Bag(("AnimHealthRange", Bag(("min", F(p.F32(8))), ("max", F(p.F32(12)))))),
            8192 => Bag(("NodeActive", N(node))),
            var other => throw new InvalidDataException($"anim: unknown condition {other} at offset {p.At}"),
        };
    }

    private static Dictionary<string, object?> FbfxColorFromTo(AnimBytes p)
    {
        float runTime = p.F32(48), alphaFrom = p.F32(36), alphaTo = p.F32(40), alphaDelta = p.F32(44);
        Dictionary<string, object?> Rgba(int offset) =>
            Bag(("r", F(p.F32(offset))), ("g", F(p.F32(offset + 12))), ("b", F(p.F32(offset + 24))), ("a", F(p.F32(offset + 36))));

        // "DELTA VAL FAIL" in the extraction's log: only alpha keeps a rate from-to does not recompute.
        return Bag(("from", Rgba(0)), ("to", Rgba(4)), ("run_time", F(runTime)),
            ("alpha_delta", alphaDelta == Delta(alphaFrom, alphaTo, runTime) ? null : F(alphaDelta)));
    }

    private static Dictionary<string, object?> PufferState(AnimBytes p, AnimZbdNames n)
    {
        uint flags = p.U32(36);
        bool Has(uint flag) => (flags & flag) != 0;
        object? Vec(uint flag, int offset) => Has(flag) ? p.Vec(offset) : null;
        object? Rng(uint flag, int offset) => Has(flag) ? p.Range(offset) : null;
        object? Num(uint flag, int offset) => Has(flag) ? F(p.F32(offset)) : null;
        int node = p.I32(40);
        string intervalType = p.U32(120) != 0 ? "Distance" : "Time";
        bool interval = Has(0x80) && Has(0x100);

        // "INTERVAL VAL FAIL" in the extraction's log: a half-set interval is kept as its raw parts.
        object? garbage = interval ? null : Bag(("interval_type", intervalType), ("has_interval_type", Has(0x80)),
            ("interval_value", F(p.F32(124))), ("has_interval_value", Has(0x100)));
        return Bag(
            ("name", p.Name(0, 32)),
            ("active_state", Has(0x04) ? N(p.U32(44)) : null),
            ("translate", Vec(0x01, 48)),
            ("at_node", Has(0x02) ? (node == -200 ? InputNode : n.Node(node)) : null),
            ("local_velocity", Vec(0x08, 60)),
            ("world_velocity", Vec(0x10, 72)),
            ("min_random_velocity", Vec(0x20, 84)),
            ("max_random_velocity", Vec(0x40, 96)),
            ("world_acceleration", Vec(0x10000, 108)),
            ("interval", interval ? Bag(("interval_type", intervalType), ("interval_value", F(p.F32(124)))) : null),
            ("size_range", Rng(0x200, 128)),
            ("lifetime_range", Rng(0x400, 136)),
            ("start_age_range", Rng(0x8000, 144)),
            ("deviation_distance", Num(0x800, 152)),
            ("unk_range", Rng(0x80000, 156)),
            ("fade_range", Rng(0x1000, 164)),
            ("friction", Num(0x20000, 172)),
            ("wind_factor", Num(0x100000, 176)),
            ("priority", Num(0x400000, 180)),
            ("number", Has(0x200000) ? N(p.U32(184)) : null),
            ("textures", Has(0x4000) ? PufferTextures(p) : null),
            ("colors", Has(0x40000) ? PufferColors(p) : null),
            ("growth_factors", Has(0x2000) ? PufferGrowths(p) : null),
            ("interval_garbage", garbage));
    }

    // Six 36-byte {run_time, name[32]} slots after a has-run-time word; the list runs to the last named slot.
    private static List<object?> PufferTextures(AnimBytes p)
    {
        bool timed = p.U32(188) != 0;
        int count = 6;
        while (count > 0 && p.U8(192 + ((count - 1) * 36) + 4) == 0)
        {
            count--;
        }

        var list = new List<object?>(count);
        for (int i = 0; i < count; i++)
        {
            int at = 192 + (i * 36);
            list.Add(Bag(("name", p.Name(at + 4, 32)), ("run_time", timed ? F(p.F32(at)) : null)));
        }

        return list;
    }

    private static List<object?> PufferColors(AnimBytes p)
    {
        var list = new List<object?>();
        for (int i = 0; i < p.I32(408); i++)
        {
            int at = 412 + (i * 20);
            list.Add(Bag(("unk00", F(p.F32(at))), ("color", p.Color(at + 4)), ("unk16", F(p.F32(at + 16)))));
        }

        return list;
    }

    private static List<object?> PufferGrowths(AnimBytes p)
    {
        var list = new List<object?>();
        for (int i = 0; i < p.I32(532); i++)
        {
            list.Add(p.Range(536 + (i * 8)));
        }

        return list;
    }

    // The compiler's per-second rate of a from-to, which the record stores beside the two ends.
    private static float Delta(float from, float to, float runTime) =>
        to == from || runTime == 0f ? 0f : (to - from) / runTime;
}

/// <summary>A definition's support-array names, which event records index 1-based.</summary>
internal sealed class AnimZbdNames
{
    public readonly List<string> Nodes = new();
    public readonly List<string> Lights = new();
    public readonly List<string> Puffers = new();
    public readonly List<string> DynamicSounds = new();
    public readonly List<string> StaticSounds = new();

    /// <summary>The node at a 1-based index; -100 is the definition's own root.</summary>
    public string Node(int index) =>
        index == -100 ? AnimZbdEvents.MainRootNode : At(Nodes, index, "node index");

    public string StaticSound(int index) => At(StaticSounds, index, "static sound index");

    private static string At(List<string> names, int index, string what)
    {
        AnimZbdEvents.Expect(index >= 1 && index <= names.Count, what, index);
        return names[index - 1];
    }
}
