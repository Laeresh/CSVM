using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using CSVM.Extraction;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="Messages"/> read from <c>strings.dll</c>: the <c>STRINGTABLE</c> values joined to the
/// <c>.data</c> key table (docs/formats/missions.md#message-table). The units build the DLL in code;
/// the data tests compare the install's DLL against today's <c>messages.json</c>.
/// </summary>
public class MessagesDllTests
{
    private const uint ImageBase = 0x10000000;
    private const uint DataRva = 0x8000;

    [Fact]
    public void EachKeyTakesTheStringItsIdNames()
    {
        var messages = Messages.FromStringsDll(StringsDll(("MSG_ZERO", 32), ("MSG_TWO", 34)));

        Assert.Equal(2, messages.Count);
        Assert.Equal("zero", messages.Get("MSG_ZERO"));
        Assert.Equal("two", messages.Get("msg_two"));
    }

    [Fact]
    public void AKeyWithoutItsStringIsRejected()
    {
        byte[] pe = StringsDll(("MSG_ZERO", 32), ("MSG_TWO", 34), ("MSG_ONE", 33));

        Assert.Throws<InvalidDataException>(() => Messages.FromStringsDll(pe));
    }

    [Fact]
    public void AStringNoKeyNamesIsRejected()
    {
        byte[] pe = StringsDll(("MSG_ZERO", 32));

        Assert.Throws<InvalidDataException>(() => Messages.FromStringsDll(pe));
    }

    [Fact]
    public void NoSourceIsAnEmptyTable()
    {
        var messages = Messages.Load((IGameFileSource?)null);

        Assert.Equal(0, messages.Count);
        Assert.Equal("MSG_ANYTHING", messages.Get("MSG_ANYTHING"));
    }

    [GameFilesTheory(AlsoExtracted = true)]
    [InlineData(Messages.GameFile)]
    public void TheDllBuildsTheModelTheExtractionBuilds(string file)
    {
        var expected = Messages.FromExtracted(Path.Combine(TestData.ExtractedRoot!, "messages.json"));
        var actual = Messages.Load(GameFilesData.Source);

        Assert.Equal(1023, actual.Count);
        Assert.True(ModelDiff.AssertEqual(expected, actual) > 0, file);
    }

    [GameFilesTheory(AlsoExtracted = true)]
    [InlineData("Crimson Skies %1")]
    public void OneChangedCharacterInTheDllIsReportedUnderItsKey(string value)
    {
        byte[] pe = GameFilesData.Source!.ReadAllBytes(Messages.GameFile);
        int at = Find(pe, Encoding.Unicode.GetBytes(value));
        pe[at] = (byte)'X';

        var expected = Messages.FromExtracted(Path.Combine(TestData.ExtractedRoot!, "messages.json"));
        string? difference = ModelDiff.FirstDifference(expected, Messages.FromStringsDll(pe));

        Assert.NotNull(difference);
        Assert.Contains("MSG_GAME_NAME_VER", difference);
    }

    // ExtractionFixtures' string-table PE, "zero" at id 32 and "two" at 34, plus a .data section.
    // That section holds 48 initializer bytes, the key pairs, then the names.
    private static byte[] StringsDll(params (string Key, int Id)[] keys)
    {
        byte[] rsrcOnly = ExtractionFixtures.StringTablePe(3, new[] { "zero", null, "two" });

        var data = new MemoryStream();
        data.Write(new byte[48]);
        int names = 48 + (8 * keys.Length);
        var pool = new MemoryStream();
        foreach (var (key, id) in keys)
        {
            WriteU32(data, ImageBase + DataRva + (uint)(names + pool.Length));
            WriteU32(data, (uint)id);
            pool.Write(Encoding.ASCII.GetBytes(key + "\0"));
        }

        data.Write(pool.ToArray());

        var pe = new byte[rsrcOnly.Length + data.Length];
        rsrcOnly.CopyTo(pe, 0);
        data.ToArray().CopyTo(pe, rsrcOnly.Length);
        int optional = 0x40 + 24;
        BinaryPrimitives.WriteUInt16LittleEndian(pe.AsSpan(0x40 + 6), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(pe.AsSpan(optional + 28), ImageBase);
        int section = optional + 224 + 40;
        Encoding.ASCII.GetBytes(".data").CopyTo(pe, section);
        BinaryPrimitives.WriteUInt32LittleEndian(pe.AsSpan(section + 8), (uint)data.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(pe.AsSpan(section + 12), DataRva);
        BinaryPrimitives.WriteUInt32LittleEndian(pe.AsSpan(section + 16), (uint)data.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(pe.AsSpan(section + 20), (uint)rsrcOnly.Length);
        return pe;
    }

    private static int Find(byte[] haystack, byte[] needle)
    {
        int at = haystack.AsSpan().IndexOf(needle);
        Assert.True(at >= 0, "string not found in strings.dll");
        return at;
    }

    private static void WriteU32(Stream s, uint value)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, value);
        s.Write(b);
    }
}
