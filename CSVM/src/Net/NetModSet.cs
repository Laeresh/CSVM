using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;

namespace CSVM.Net;

/// <summary>One plane mod as a session names it: its id, its SHA-256 content hash in lower-case hex
/// and its parent's stock airframe id.</summary>
public readonly record struct NetMod(string Id, string Hash, byte Parent);

/// <summary>
/// A host's <see cref="NetModSet"/>, sent by its lobby to every peer on connect and by its session
/// at each join, before the handshake. Only a host with a mod sends it, so a stock session's wire is
/// unchanged. A count byte, three reserved, then per mod its parent, its id in
/// <see cref="IdBytes"/> and its 32-byte hash.
/// </summary>
public readonly struct ModSetMessage : INetMessage<ModSetMessage>
{
    /// <summary>How many id bytes a mod carries, UTF-8 and zero padded. It is the seat roster's plane
    /// width, so a mod id that fits one fits the other.</summary>
    public const int IdBytes = SeatRosterMessage.PlaneBytes;

    /// <summary>The bytes of a SHA-256 hash.</summary>
    public const int HashBytes = 32;

    /// <summary>The most mods the message carries.</summary>
    // ponytail: a host past it offers its guests only its first 64 mods. Widen the cap if players
    // install more.
    public const int MaxMods = 64;

    /// <summary>The bytes before the first mod: the header, the count and three reserved.</summary>
    public const int PrefixSize = NetMessage.HeaderBytes + 4;

    /// <summary>The bytes one mod takes.</summary>
    public const int EntrySize = 1 + IdBytes + HashBytes;

    /// <summary>The widest message, <see cref="MaxMods"/> mods.</summary>
    public const int MaxSize = PrefixSize + (EntrySize * MaxMods);

    /// <summary>The message naming <paramref name="set"/>'s first <see cref="MaxMods"/> mods.</summary>
    public ModSetMessage(NetModSet set) =>
        Set = set.Mods.Count <= MaxMods ? set : new NetModSet(set.Mods.Take(MaxMods).ToArray());

    /// <inheritdoc/>
    public static NetMessageType Type => NetMessageType.ModSet;

    /// <inheritdoc/>
    public static NetReliability Reliability => NetReliability.Reliable;

    /// <summary>The host's mods.</summary>
    public NetModSet Set { get; }

    /// <summary>How wide a message of <paramref name="mods"/> mods is.</summary>
    public static int SizeFor(int mods) => PrefixSize + (EntrySize * mods);

    /// <inheritdoc/>
    public static bool TryRead(ReadOnlySpan<byte> from, out ModSetMessage message)
    {
        message = default;
        var reader = new NetMessageReader(from);
        if (!reader.Valid || reader.Type != Type || reader.Length < PrefixSize)
        {
            return false;
        }

        int count = reader.ReadByte();
        _ = reader.ReadByte();
        _ = reader.ReadUInt16();
        if (count > MaxMods || reader.Length != SizeFor(count))
        {
            return false;
        }

        var mods = new NetMod[count];
        Span<byte> hash = stackalloc byte[HashBytes];
        for (int i = 0; i < count; i++)
        {
            byte parent = reader.ReadByte();
            string id = reader.ReadText(IdBytes);
            for (int b = 0; b < HashBytes; b++)
            {
                hash[b] = reader.ReadByte();
            }

            // A mod stands on a stock parent and has a name; anything else is no set a host writes.
            if (parent >= NetModSet.StockRows || id.Length == 0)
            {
                return false;
            }

            mods[i] = new NetMod(id, Convert.ToHexString(hash).ToLowerInvariant(), parent);
        }

        message = new ModSetMessage(new NetModSet(mods));
        return true;
    }

    /// <inheritdoc/>
    public int Write(Span<byte> into)
    {
        var mods = (Set ?? NetModSet.None).Mods;
        var writer = new NetMessageWriter(into, Type);
        writer.WriteByte((byte)mods.Count);
        writer.WriteByte(0);
        writer.WriteUInt16(0);
        foreach (var mod in mods)
        {
            writer.WriteByte(mod.Parent);
            writer.WriteText(mod.Id, IdBytes);
            byte[] hash = mod.Hash?.Length == HashBytes * 2 ? Convert.FromHexString(mod.Hash) : new byte[HashBytes];
            foreach (byte b in hash)
            {
                writer.WriteByte(b);
            }
        }

        return writer.Close();
    }
}

/// <summary>
/// A host's plane mods in row order, and the session's airframe rows. Rows 0 to 10 are the stock
/// airframes in id order, row 11 + i the i-th mod. Every lobby airframe byte is such a row, read
/// against the host's set. The byte keeps its width, and a stock row reads as it always did. A
/// guest flies a session only while it holds every mod of the host's set under the same hash
/// (<see cref="MissingFrom"/>). Engine-free. docs/org/multiplayer-messages.md, "Plane mods".
/// </summary>
public sealed class NetModSet
{
    /// <summary>The set of a host with no plane mod.</summary>
    public static readonly NetModSet None = new(Array.Empty<NetMod>());

    /// <summary>A set of <paramref name="mods"/> in row order.</summary>
    public NetModSet(IReadOnlyList<NetMod> mods) => Mods = mods ?? throw new ArgumentNullException(nameof(mods));

    /// <summary>How many rows the stock airframes take, the first mod's row.</summary>
    public static int StockRows => AirframeRegistry.Stock.Count;

    /// <summary>The mods in row order.</summary>
    public IReadOnlyList<NetMod> Mods { get; }

    /// <summary>How many airframe rows the session offers, stock and mods.</summary>
    public int Rows => StockRows + Mods.Count;

    /// <summary>The airframe node of <paramref name="row"/>: a stock node, a mod's id, or empty past
    /// the rows.</summary>
    public string NodeOf(int row) =>
        row >= 0 && row < StockRows ? AirframeRegistry.Stock[row].Node : Mod(row)?.Id ?? "";

    /// <summary>The stock airframe id <paramref name="row"/> flies as: itself, a mod's parent, or -1
    /// past the rows. The plane rules, the stat table and the art read a mod by it.</summary>
    public int StockOf(int row) => row >= 0 && row < StockRows ? row : Mod(row)?.Parent ?? -1;

    /// <summary>The row of <paramref name="node"/>, a stock node or a mod id of this set, else -1.
    /// </summary>
    public int RowOf(string? node)
    {
        for (int row = 0; row < Rows; row++)
        {
            if (string.Equals(NodeOf(row), node, StringComparison.Ordinal))
            {
                return row;
            }
        }

        return -1;
    }

    /// <summary>This set's mods that <paramref name="own"/> lacks or holds under another hash, in row
    /// order: what a guest holding <paramref name="own"/> must install to fly this host's session.
    /// </summary>
    public IReadOnlyList<NetMod> MissingFrom(NetModSet own)
    {
        ArgumentNullException.ThrowIfNull(own);
        return Mods.Where(mod => !own.Mods.Any(have => string.Equals(have.Id, mod.Id, StringComparison.Ordinal)
            && string.Equals(have.Hash, mod.Hash, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    private NetMod? Mod(int row) => row - StockRows is var at && at >= 0 && at < Mods.Count ? Mods[at] : null;
}
