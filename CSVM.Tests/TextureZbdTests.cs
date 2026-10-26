using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CSVM.Extraction;
using CSVM.Mech3;
using CSVM.Tests.Oracle;
using Xunit;
using Xunit.Abstractions;
using AlphaClass = CSVM.Mech3.TextureArchive.AlphaClass;

namespace CSVM.Tests;

/// <summary>
/// <see cref="TextureZbd.Read"/> over hand-encoded archives (CI-safe), and over every texture
/// archive of the install against today's extraction. The oracle, <see cref="Oracle.TexturesLegacy"/>,
/// reads each texture's manifest fields and its PNG's pixels without the engine. Layout: docs/formats/texture-archive.md.
/// </summary>
public class TextureZbdTests
{
    private readonly ITestOutputHelper _output;

    public TextureZbdTests(ITestOutputHelper output) => _output = output;

    /// <summary>The 49 texture archives: each chapter's <c>texture</c> and five <c>rtextureN</c>
    /// tiers, and the root <c>rimage</c>.</summary>
    public static IEnumerable<object[]> TextureArchives() =>
        ZbdManifest.Expected.Where(IsTextureArchive).Select(path => new object[] { path });

    [Fact]
    public void ThereAreFortyNineTextureArchives()
    {
        Assert.Equal(49, ZbdManifest.Expected.Count(IsTextureArchive));
    }

    [Fact]
    public void SixteenBitTexturesWidenAndTakeTheirAlphaFromTheClass()
    {
        var textures = TextureZbd.Read(Encode(
            Rgb565("opaque", 0xA5, 0, 0xFFFF, 0x0000),
            Rgb565("simple", 0xA3, 3, 0x0000, 0xF800),
            Rgb565("full", 0xAB, 8, 0x07E0, 0x001F) with { Plane = new byte[] { 7, 200 } }));

        Assert.Equal(new[] { "opaque", "simple", "full" }, textures.Select(t => t.Key));
        Assert.Equal(new[] { AlphaClass.None, AlphaClass.Simple, AlphaClass.Full }, textures.Select(t => t.Alpha));
        Assert.Equal(new[] { 0, 3, 8 }, textures.Select(t => t.RenderFlags));
        Assert.All(textures, t => Assert.Equal((2, 1), (t.Width, t.Height)));

        Assert.False(textures[0].HasAlphaChannel);
        Assert.Equal(new byte[] { 255, 255, 255, 0, 0, 0 }, textures[0].Pixels);
        // Simple: only the all-zero texel is clear.
        Assert.Equal(new byte[] { 0, 0, 0, 0, 255, 0, 0, 255 }, textures[1].Pixels);
        Assert.Equal(new byte[] { 0, 255, 0, 7, 0, 0, 255, 200 }, textures[2].Pixels);
    }

    [Fact]
    public void FiveAndSixBitChannelsRoundToNearest()
    {
        // 16/31 and 32/63 of 255 are 131.6 and 129.5, which round to 132 and 130.
        var t = TextureZbd.Read(Encode(Rgb565("mid", 0xA5, 0, (16 << 11) | (32 << 5) | 1)))[0];
        Assert.Equal(new byte[] { 132, 130, 8 }, t.Pixels);
    }

    [Fact]
    public void PalettedTexturesReadALocalOrAGlobalPalette()
    {
        var global = new ushort[256];
        global[1] = 0x001F;
        var textures = TextureZbd.Read(Encode(
            new[] { global },
            Paletted("local", 0xAB, new byte[] { 1, 0 }, new ushort[] { 0xF800, 0x07E0 }) with { Plane = new byte[] { 9, 99 } },
            Paletted("global", 0xB5, new byte[] { 1, 1 }, null) with { GlobalPalette = 0, PaletteCount = 2 },
            Paletted("simple", 0xA3, new byte[] { 0, 0 }, new ushort[] { 0xFFFF })));

        Assert.Equal(new byte[] { 0, 255, 0, 9, 255, 0, 0, 99 }, textures[0].Pixels);
        Assert.Equal(new byte[] { 0, 0, 255, 0, 0, 255 }, textures[1].Pixels);
        // A paletted Simple texture carries its class but no alpha channel.
        Assert.Equal(AlphaClass.Simple, textures[2].Alpha);
        Assert.False(textures[2].HasAlphaChannel);
        Assert.Equal(new byte[] { 255, 255, 255, 255, 255, 255 }, textures[2].Pixels);
    }

    [Fact]
    public void ARepeatedNameIsRenamedAfterItsFirstOccurrence()
    {
        var textures = TextureZbd.Read(Encode(
            Rgb565("sky", 0xA5, 0, 1, 1),
            Rgb565("sky-1", 0xA5, 0, 1, 1),
            Rgb565("sky", 0xA5, 0, 1, 1),
            Rgb565("SKY", 0xA5, 0, 1, 1),
            Rgb565("a.tif", 0xA5, 0, 1, 1),
            Rgb565("a.tif", 0xA5, 0, 1, 1)));

        // The rename skips a name already taken, compares case-sensitively and splits at the last period.
        Assert.Equal(new string?[] { null, null, "sky-2", null, null, "a-1.tif" }, textures.Select(t => t.Rename));
        Assert.Equal("sky", textures[2].Name);
    }

    [Fact]
    public void ATextureThatDoesNotStartWhereTheLastEndedIsRefused()
    {
        var data = Encode(Rgb565("a", 0xA5, 0, 1, 1), Rgb565("b", 0xA5, 0, 1, 1));
        data[24 + 40 + 32]++;
        Assert.Contains("start of b", Assert.Throws<InvalidDataException>(() => TextureZbd.Read(data)).Message);
    }

    [Fact]
    public void BytesPastTheLastTextureAreRefused()
    {
        var data = Encode(Rgb565("a", 0xA5, 0, 1, 1)).Append((byte)0).ToArray();
        Assert.Contains("end of file", Assert.Throws<InvalidDataException>(() => TextureZbd.Read(data)).Message);
    }

    [Fact]
    public void AnIndexPastThePaletteIsRefused()
    {
        var data = Encode(Paletted("a", 0xA5, new byte[] { 0, 2 }, new ushort[] { 1, 2 }));
        Assert.Contains("palette index of a", Assert.Throws<InvalidDataException>(() => TextureZbd.Read(data)).Message);
    }

    [Fact]
    public void NoSourceReadsAsNoTextures()
    {
        Assert.Null(TextureZbd.Load(null, "ZBD/C1/texture.zbd"));
    }

    [GameFilesTheory(AlsoExtracted = true)]
    [MemberData(nameof(TextureArchives))]
    public void TheReaderBuildsTheTexturesTheExtractionBuilds(string path)
    {
        var bytes = GameFilesData.Source!.ReadAllBytes("ZBD/" + path);
        var clock = Stopwatch.StartNew();
        var actual = TextureZbd.Read(bytes);
        double decodeMs = clock.Elapsed.TotalMilliseconds;
        var expected = FromExtraction(path);

        int compared = ModelDiff.AssertEqual(expected, actual);
        long pixels = actual.Sum(t => (long)t.Width * t.Height);
        var classes = string.Join(", ", actual.GroupBy(t => t.Alpha).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}"));
        _output.WriteLine($"{path}: {actual.Count} textures, {pixels} pixels, {bytes.Length} bytes, decoded in {decodeMs:F1} ms; "
                          + $"{actual.Count(t => t.Rename != null)} renamed; {classes}; {compared} leaf values compared");
        Assert.True(compared > 0);
    }

    [GameFilesTheory(AlsoExtracted = true)]
    [InlineData("C1C/rtexture2.zbd")]
    public void OneChangedPixelInTheDirectModelIsFound(string path)
    {
        var expected = FromExtraction(path);
        var actual = TextureZbd.Read(GameFilesData.Source!.ReadAllBytes("ZBD/" + path));
        int k = actual.Count / 2;
        actual[k].Pixels[5] ^= 1;

        Assert.Contains($"[{k}].Pixels", ModelDiff.FirstDifference(expected, actual));
    }

    private static bool IsTextureArchive(string path) =>
        Path.GetFileName(path) is "texture.zbd" or "rimage.zbd"
        || (Path.GetFileName(path).StartsWith("rtexture", StringComparison.Ordinal) && path.EndsWith(".zbd", StringComparison.Ordinal));

    // Today's model, the extraction's zip read the way the engine used to read it.
    private static List<TextureImage> FromExtraction(string path) =>
        TexturesLegacy.Load(Path.Combine(TestData.ExtractedRoot!, Path.ChangeExtension(ZbdTree.Canonical(path), ".zip")));

    private static Spec Rgb565(string name, int flags, int renderFlags, params int[] texels) =>
        new(name, flags, renderFlags, texels.Length, texels.SelectMany(c => new[] { (byte)c, (byte)(c >> 8) }).ToArray(), 0, null, null, -1);

    private static Spec Paletted(string name, int flags, byte[] indices, ushort[]? palette) =>
        new(name, flags, 0, indices.Length, indices, palette?.Length ?? 0, palette, null, -1);

    private static byte[] Encode(params Spec[] textures) => Encode(Array.Empty<ushort[]>(), textures);

    // The layout of docs/formats/texture-archive.md: header, one 40-byte entry per texture, the
    // global palettes, then each texture's 16-byte header and data. Every texture is one row high.
    private static byte[] Encode(ushort[][] globals, params Spec[] textures)
    {
        var bodies = textures.Select(t =>
        {
            var body = new MemoryStream();
            var w = new BinaryWriter(body);
            w.Write(t.Flags);
            w.Write((ushort)t.Width);
            w.Write((ushort)1);
            w.Write(0);
            w.Write((ushort)t.PaletteCount);
            w.Write((ushort)t.RenderFlags);
            w.Write(t.Data);
            w.Write(t.Plane ?? Array.Empty<byte>());
            foreach (var c in t.Palette ?? Array.Empty<ushort>())
            {
                w.Write(c);
            }

            return body.ToArray();
        }).ToList();

        var file = new MemoryStream();
        var writer = new BinaryWriter(file);
        writer.Write(0);
        writer.Write(1);
        writer.Write(globals.Length);
        writer.Write(textures.Length);
        writer.Write(0);
        writer.Write(0);
        int start = 24 + (40 * textures.Length) + (512 * globals.Length);
        for (int i = 0; i < textures.Length; i++)
        {
            var name = new byte[32];
            Encoding.ASCII.GetBytes(textures[i].Name).CopyTo(name, 0);
            writer.Write(name);
            writer.Write(start);
            writer.Write(textures[i].GlobalPalette);
            start += bodies[i].Length;
        }

        foreach (var palette in globals)
        {
            foreach (var c in palette)
            {
                writer.Write(c);
            }
        }

        bodies.ForEach(writer.Write);
        return file.ToArray();
    }

    private sealed record Spec(string Name, int Flags, int RenderFlags, int Width, byte[] Data, int PaletteCount, ushort[]? Palette, byte[]? Plane, int GlobalPalette);
}
