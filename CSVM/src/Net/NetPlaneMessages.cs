using System;

namespace CSVM.Net;

/// <summary>Why a lobby refuses a plane, in the order the original's check tests it.</summary>
public enum PlaneRefusal : byte
{
    /// <summary>The plane is admitted.</summary>
    None = 0,

    /// <summary>A custom plane while the host does not allow them.</summary>
    CustomBarred = 1,

    /// <summary>The engine is missing or outlawed.</summary>
    Engine = 2,

    /// <summary>The airframe is outlawed or out of range.</summary>
    Airframe = 3,

    /// <summary>A mounted gun's calibre is outlawed.</summary>
    Gun = 4,

    /// <summary>A picked ammunition is outlawed; Ready resets every gun's to none.</summary>
    Ammo = 5,

    /// <summary>A picked rocket is outlawed; Ready resets every pylon's to none.</summary>
    Rockets = 6,
}

/// <summary>
/// One seat's plane build. A guest sends its own under <see cref="Mine"/> with its pick, and a host
/// sends every seat's to every guest at a launch, before the session's opener. A null
/// <see cref="Build"/> is a stock pick, which clears an earlier build. Kept in the lobby.
/// </summary>
public readonly record struct PlaneBuildMessage(byte Seat, NetPlaneBuild? Build) : INetMessage<PlaneBuildMessage>
{
    /// <summary>The seat a guest's own pick names, since a guest has no seat until launch.</summary>
    public const byte Mine = byte.MaxValue;

    /// <summary>The fixed width of the message, header included.</summary>
    public const int Size = NetMessage.HeaderBytes + 2 + NetPlaneBuild.Bytes + NetPlaneBuild.NameBytes;

    /// <inheritdoc/>
    public static NetMessageType Type => NetMessageType.PlaneBuild;

    /// <inheritdoc/>
    public static NetReliability Reliability => NetReliability.Reliable;

    /// <inheritdoc/>
    public static bool TryRead(ReadOnlySpan<byte> from, out PlaneBuildMessage message)
    {
        message = default;
        var reader = new NetMessageReader(from);
        if (!reader.Is(Size) || reader.Type != Type)
            return false;

        byte seat = reader.ReadByte();
        bool custom = (reader.ReadByte() & 1) != 0;
        var build = NetPlaneBuild.Read(ref reader);
        message = new PlaneBuildMessage(seat, custom ? build : null);
        return true;
    }

    /// <inheritdoc/>
    public int Write(Span<byte> into)
    {
        var writer = new NetMessageWriter(into, Type);
        writer.WriteByte(Seat);
        writer.WriteByte((byte)(Build != null ? 1 : 0));
        (Build ?? new NetPlaneBuild()).Write(ref writer);
        return writer.Close();
    }
}

/// <summary>
/// A Dogfight host's plane rules: Allow Custom Planes, Outlaw Components and the outlaw list, the
/// original's 34 flags in its own packing order. The list counts only while Outlaw Components is
/// ticked. Both ends apply the same rules, so an owner's plane and every copy of it agree.
/// </summary>
public readonly record struct NetPlaneRules(bool AllowCustom, bool Outlawing, ulong Outlawed)
{
    /// <summary>The first airframe flag; eleven follow in airframe order.</summary>
    public const int AirframeFlag = 0;

    /// <summary>The first gun calibre flag; five follow.</summary>
    public const int GunFlag = 11;

    /// <summary>The first ammunition flag; four follow.</summary>
    public const int AmmoFlag = 16;

    /// <summary>The first rocket flag; eleven follow in rocket-table order.</summary>
    public const int RocketFlag = 20;

    /// <summary>Outlaw All Rockets.</summary>
    public const int AllRocketsFlag = 31;

    /// <summary>Outlaw All Ammo.</summary>
    public const int AllAmmoFlag = 32;

    /// <summary>Outlaw Nitro-Boosted Engines.</summary>
    public const int NitroFlag = 33;

    /// <summary>How many flags the list holds.</summary>
    public const int Flags = 34;

    /// <summary>The bytes the list packs into.</summary>
    public const int ListBytes = 5;

    /// <summary>The ammunition index meaning none, always allowed.</summary>
    public const int NoAmmo = 4;

    /// <summary>The rocket-table row meaning none, always allowed.</summary>
    public const int NoRocket = 11;

    // The rocket-table row an unpicked pylon flies, the stock high-explosive load.
    private const int StockRocket = 1;

    /// <summary>Whether flag <paramref name="flag"/> is set on the list, ticked or not.</summary>
    public bool Has(int flag) => flag is >= 0 and < Flags && (Outlawed & (1UL << flag)) != 0;

    /// <summary>These rules with flag <paramref name="flag"/> set or cleared.</summary>
    public NetPlaneRules With(int flag, bool outlawed) => flag is < 0 or >= Flags
        ? this
        : this with { Outlawed = outlawed ? Outlawed | (1UL << flag) : Outlawed & ~(1UL << flag) };

    /// <summary>Whether an airframe may fly.</summary>
    public bool AirframeAllowed(int airframe) => airframe is >= 0 and <= 10 && !Counts(AirframeFlag + airframe);

    /// <summary>Whether an engine may fly. No engine never may, and the injector tiers not while
    /// nitro is outlawed.</summary>
    public bool EngineAllowed(int engine) =>
        engine is >= 0 and < NetPlaneBuild.NoEngine && !(engine is >= 3 and <= 5 && Counts(NitroFlag));

    /// <summary>Whether a gun slot's calibre may fly. An empty slot always may.</summary>
    public bool GunAllowed(int calibre) => calibre == NetPlaneBuild.EmptyGun || (calibre is >= 0 and <= 4 && !Counts(GunFlag + calibre));

    /// <summary>Whether an ammunition may fly. None always may.</summary>
    public bool AmmoAllowed(int ammo) => ammo == NoAmmo || (ammo is >= 0 and < NoAmmo && !Counts(AllAmmoFlag) && !Counts(AmmoFlag + ammo));

    /// <summary>Whether a rocket-table row may fly. None always may.</summary>
    public bool RocketAllowed(int row) => row == NoRocket || (row is >= 0 and < NoRocket && !Counts(AllRocketsFlag) && !Counts(RocketFlag + row));

    /// <summary>Why these rules refuse a plane, or <see cref="PlaneRefusal.None"/>. A null
    /// <paramref name="custom"/> is the stock build of <paramref name="airframe"/>.</summary>
    public PlaneRefusal Refuses(int airframe, NetPlaneBuild? custom)
    {
        if (custom != null && !AllowCustom)
        {
            return PlaneRefusal.CustomBarred;
        }

        var build = custom ?? NetPlaneBuild.Stock(airframe);
        if (!EngineAllowed(build.Engine))
        {
            return PlaneRefusal.Engine;
        }

        if (!AirframeAllowed(custom != null ? build.Airframe : airframe))
        {
            return PlaneRefusal.Airframe;
        }

        foreach (byte gun in build.Guns)
        {
            if (!GunAllowed(gun))
            {
                return PlaneRefusal.Gun;
            }
        }

        return PlaneRefusal.None;
    }

    /// <summary>Whether any gun slot's ammunition is outlawed. An unpicked slot reads as its stock
    /// slug.</summary>
    public bool AmmoOutlawed(CoopFit fit)
    {
        for (int slot = 0; slot < CoopFit.GunSlots; slot++)
        {
            int stored = fit.AmmoAt(slot);
            if (!AmmoAllowed(stored < 0 ? 0 : stored))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether any pylon's rocket is outlawed. An unpicked pylon reads as its stock high
    /// explosive.</summary>
    public bool RocketsOutlawed(CoopFit fit)
    {
        for (int cell = 0; cell < CoopFit.Cells; cell++)
        {
            int stored = fit.OrdnanceAt(cell);
            if (!RocketAllowed(stored > 0 ? stored - 1 : StockRocket))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The fit the original's Ready check leaves: every gun's ammunition set to none when
    /// any one is outlawed, and every pylon's rocket likewise.</summary>
    public CoopFit Enforce(CoopFit fit)
    {
        var ammo = new int[CoopFit.GunSlots];
        bool noAmmo = AmmoOutlawed(fit);
        for (int slot = 0; slot < ammo.Length; slot++)
        {
            ammo[slot] = noAmmo ? NoAmmo : fit.AmmoAt(slot);
        }

        var cells = new int[CoopFit.Cells];
        bool noRockets = RocketsOutlawed(fit);
        for (int cell = 0; cell < cells.Length; cell++)
        {
            cells[cell] = noRockets ? NoRocket + 1 : fit.OrdnanceAt(cell);
        }

        return noAmmo || noRockets ? CoopFit.Of(ammo, cells) : fit;
    }

    /// <summary>The list in the original's five-byte packing, lowest flag first.</summary>
    public void PackList(Span<byte> into)
    {
        for (int i = 0; i < ListBytes; i++)
        {
            into[i] = (byte)(Outlawed >> (8 * i));
        }
    }

    /// <summary>The list read back out of <see cref="PackList"/>'s bytes.</summary>
    public static ulong UnpackList(ReadOnlySpan<byte> from)
    {
        ulong list = 0;
        for (int i = 0; i < ListBytes; i++)
        {
            list |= (ulong)from[i] << (8 * i);
        }

        return list & ((1UL << Flags) - 1);
    }

    // A flag outlaws only while Outlaw Components is ticked.
    private bool Counts(int flag) => Outlawing && Has(flag);
}

/// <summary>
/// A Dogfight host's plane rules, sent to each guest beside its Mission Options whenever they
/// change. A separate message, so an older guest still reads the options it knows. Kept in the lobby.
/// </summary>
public readonly record struct LobbyPlaneRulesMessage(byte Epoch, NetPlaneRules Rules) : INetMessage<LobbyPlaneRulesMessage>
{
    /// <summary>The fixed width of the message, header included.</summary>
    public const int Size = NetMessage.HeaderBytes + 2 + NetPlaneRules.ListBytes + 1;

    /// <inheritdoc/>
    public static NetMessageType Type => NetMessageType.LobbyPlaneRules;

    /// <inheritdoc/>
    public static NetReliability Reliability => NetReliability.Reliable;

    /// <inheritdoc/>
    public static bool TryRead(ReadOnlySpan<byte> from, out LobbyPlaneRulesMessage message)
    {
        message = default;
        var reader = new NetMessageReader(from);
        if (!reader.Is(Size) || reader.Type != Type)
            return false;

        byte epoch = reader.ReadByte();
        byte flags = reader.ReadByte();
        Span<byte> list = stackalloc byte[NetPlaneRules.ListBytes];
        for (int i = 0; i < list.Length; i++)
        {
            list[i] = reader.ReadByte();
        }

        message = new LobbyPlaneRulesMessage(epoch,
            new NetPlaneRules((flags & 1) != 0, (flags & 2) != 0, NetPlaneRules.UnpackList(list)));
        return true;
    }

    /// <inheritdoc/>
    public int Write(Span<byte> into)
    {
        var writer = new NetMessageWriter(into, Type);
        writer.WriteByte(Epoch);
        writer.WriteByte((byte)((Rules.AllowCustom ? 1 : 0) | (Rules.Outlawing ? 2 : 0)));
        Span<byte> list = stackalloc byte[NetPlaneRules.ListBytes];
        Rules.PackList(list);
        for (int i = 0; i < list.Length; i++)
        {
            writer.WriteByte(list[i]);
        }

        writer.WriteByte(0);
        return writer.Close();
    }
}

/// <summary>
/// A custom plane as the wire carries it: the airframe, the components that decide how it flies
/// and takes hits, and the paint it wears. Each field is the saved record's own value, so every
/// machine resolves it through the same tables. Ammunition and ordnance are left out, since a
/// seat's <see cref="CoopFit"/> already carries them. Engine-free.
/// </summary>
public sealed class NetPlaneBuild : IEquatable<NetPlaneBuild>
{
    /// <summary>How many gun slots a build carries.</summary>
    public const int GunSlots = 4;

    /// <summary>How many paint slots a build carries.</summary>
    public const int PaintSlots = 3;

    /// <summary>The gun id of an empty slot, the saved record's own.</summary>
    public const byte EmptyGun = 5;

    /// <summary>The engine id meaning no engine, which the lobby refuses.</summary>
    public const byte NoEngine = 6;

    /// <summary>The engine every stock build carries, the plain middle tier.</summary>
    public const byte StockEngine = 1;

    /// <summary>A decal slot that keeps the aircraft's shipped placeholder.</summary>
    public const sbyte KeepDecal = -1;

    /// <summary>The bytes a build's fields take on the wire, its name aside.</summary>
    public const int Bytes = 26;

    /// <summary>How many name bytes a build carries.</summary>
    public const int NameBytes = SeatRosterMessage.CallsignBytes;

    // The stock builds' gun ids, slot by slot, read out of the template table at 0x00619f58.
    private static readonly byte[,] StockGuns =
    {
        { 0, 5, 5, 5 }, { 2, 1, 5, 2 }, { 2, 2, 0, 0 }, { 1, 0, 5, 5 }, { 3, 0, 5, 0 }, { 2, 1, 0, 5 },
        { 4, 0, 5, 0 }, { 4, 0, 5, 5 }, { 3, 2, 5, 1 }, { 2, 1, 5, 5 }, { 4, 2, 5, 5 },
    };

    /// <summary>The plane's name as its owner saved it.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The airframe id, 0 to 10.</summary>
    public byte Airframe { get; set; }

    /// <summary>The engine id, 0 to 6. Ids 3 to 5 carry the nitrous injector.</summary>
    public byte Engine { get; set; } = NoEngine;

    /// <summary>Armour presses by zone: nose, tail, left wing, right wing.</summary>
    public byte[] Armour { get; } = new byte[4];

    /// <summary>Hardpoints on the left wing.</summary>
    public byte LeftHardpoints { get; set; }

    /// <summary>Hardpoints on the right wing.</summary>
    public byte RightHardpoints { get; set; }

    /// <summary>Each slot's gun calibre, 0 to 4, or <see cref="EmptyGun"/>.</summary>
    public byte[] Guns { get; } = { EmptyGun, EmptyGun, EmptyGun, EmptyGun };

    /// <summary>The twin-mount bit of each gun slot, bit 0 for slot 0.</summary>
    public byte Twins { get; set; }

    /// <summary>The paint pattern index.</summary>
    public byte PaintPattern { get; set; }

    /// <summary>Each paint slot's swatch row.</summary>
    public byte[] Colours { get; } = new byte[PaintSlots];

    /// <summary>Each paint slot's shade within its swatch row.</summary>
    public byte[] Shades { get; } = new byte[PaintSlots];

    /// <summary>The nose, tail and wing decals, or <see cref="KeepDecal"/>.</summary>
    public sbyte[] Decals { get; } = { KeepDecal, KeepDecal, KeepDecal };

    /// <summary>The build a stock airframe flies: the stock engine and the template's guns. The
    /// lobby's gate reads a stock pick through this, as the original checks a stock record.</summary>
    public static NetPlaneBuild Stock(int airframe)
    {
        int row = Math.Clamp(airframe, 0, StockGuns.GetLength(0) - 1);
        var build = new NetPlaneBuild { Airframe = (byte)row, Engine = StockEngine };
        for (int slot = 0; slot < GunSlots; slot++)
        {
            build.Guns[slot] = StockGuns[row, slot];
        }

        return build;
    }

    /// <summary>Whether two builds carry the same fields and name.</summary>
    public bool Equals(NetPlaneBuild? other)
    {
        if (other is null)
        {
            return false;
        }

        Span<byte> mine = stackalloc byte[Bytes];
        Span<byte> theirs = stackalloc byte[Bytes];
        Pack(mine);
        other.Pack(theirs);
        return mine.SequenceEqual(theirs) && string.Equals(Name, other.Name, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as NetPlaneBuild);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Airframe, Engine, Guns[0], PaintPattern, Name);

    /// <summary>A field-for-field copy.</summary>
    public NetPlaneBuild Copy()
    {
        Span<byte> body = stackalloc byte[Bytes];
        Pack(body);
        var copy = Unpack(body);
        copy.Name = Name;
        return copy;
    }

    internal static NetPlaneBuild Read(ref NetMessageReader reader)
    {
        Span<byte> body = stackalloc byte[Bytes];
        for (int i = 0; i < Bytes; i++)
        {
            body[i] = reader.ReadByte();
        }

        var build = Unpack(body);
        build.Name = reader.ReadText(NameBytes);
        return build;
    }

    internal void Write(ref NetMessageWriter writer)
    {
        Span<byte> body = stackalloc byte[Bytes];
        Pack(body);
        for (int i = 0; i < Bytes; i++)
        {
            writer.WriteByte(body[i]);
        }

        writer.WriteText(Name ?? string.Empty, NameBytes);
    }

    private static NetPlaneBuild Unpack(ReadOnlySpan<byte> body)
    {
        var build = new NetPlaneBuild
        {
            Airframe = body[0],
            Engine = body[1],
            LeftHardpoints = body[6],
            RightHardpoints = body[7],
            Twins = body[12],
            PaintPattern = body[13],
        };
        for (int i = 0; i < 4; i++)
        {
            build.Armour[i] = body[2 + i];
            build.Guns[i] = body[8 + i];
        }

        for (int i = 0; i < PaintSlots; i++)
        {
            build.Colours[i] = body[14 + i];
            build.Shades[i] = body[17 + i];
            build.Decals[i] = (sbyte)body[20 + i];
        }

        return build;
    }

    // The fixed layout ends in three spare bytes. The rest: 0 airframe, 1 engine, 2..5 armour, 6..7
    // hardpoints, 8..11 guns, 12 twins, 13 pattern, 14..16 colours, 17..19 shades, 20..22 decals.
    private void Pack(Span<byte> body)
    {
        body.Clear();
        body[0] = Airframe;
        body[1] = Engine;
        body[6] = LeftHardpoints;
        body[7] = RightHardpoints;
        body[12] = Twins;
        body[13] = PaintPattern;
        for (int i = 0; i < 4; i++)
        {
            body[2 + i] = Armour[i];
            body[8 + i] = Guns[i];
        }

        for (int i = 0; i < PaintSlots; i++)
        {
            body[14 + i] = Colours[i];
            body[17 + i] = Shades[i];
            body[20 + i] = (byte)Decals[i];
        }
    }
}
