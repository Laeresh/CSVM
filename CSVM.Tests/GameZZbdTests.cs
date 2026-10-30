using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CSVM.Extraction;
using CSVM.Mech3;
using Godot;
using Xunit;
using Xunit.Abstractions;

namespace CSVM.Tests;

/// <summary>
/// <see cref="GameZZbd.Read"/> over a hand-encoded archive (CI-safe), and over every chapter's
/// <c>gamez.zbd</c> and the root <c>planes.zbd</c> against the model today's extraction loads.
/// Layout: docs/formats/gamez-zbd.md.
/// </summary>
public class GameZZbdTests
{
    private readonly ITestOutputHelper _output;

    public GameZZbdTests(ITestOutputHelper output) => _output = output;

    /// <summary>The nine archives: each chapter's world and the planes.</summary>
    public static IEnumerable<object[]> GameZArchives() =>
        ZbdManifest.Expected.Where(IsGameZ).Select(path => new object[] { path });

    [Fact]
    public void ThereAreNineGameZArchives()
    {
        Assert.Equal(9, ZbdManifest.Expected.Count(IsGameZ));
    }

    [Theory]
    [InlineData("skin\0tif\0", "skin.tif")]
    [InlineData("cowling\0.tif\0", "cowling..tif")]
    [InlineData("bare\0\0", "bare")]
    [InlineData("full\0", "full")]
    [InlineData("abcdefghijklmn\0wxyz", "abcdefghijklmn.wxyz")]
    public void ATextureNameRestoresItsPeriodAtTheFirstNul(string stored, string expected)
    {
        var raw = new byte[20];
        Encoding.ASCII.GetBytes(stored).CopyTo(raw, 0);
        Assert.Equal(expected, GameZZbd.TextureName(raw, 0));
    }

    [Fact]
    public void OnlyAnExactlyRecomposedMatrixGivesWayToTheRotation()
    {
        float[] identity = { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
        Assert.True(GameZZbd.Recomposes(Vector3.Zero, Vector3.One, identity));

        float[] scaled = { 2, 0, 0, 0, 1, 0, 0, 0, 1 };
        Assert.True(GameZZbd.Recomposes(Vector3.Zero, new Vector3(2, 1, 1), scaled));
        Assert.False(GameZZbd.Recomposes(Vector3.Zero, Vector3.One, scaled));

        identity[4] = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(1f) + 1);
        Assert.False(GameZZbd.Recomposes(Vector3.Zero, Vector3.One, identity));
    }

    [Fact]
    public void AHandEncodedArchiveReadsIntoTheModel()
    {
        var gz = GameZZbd.Read(TinyArchive());

        Assert.Equal(new[] { "skin.tif" }, gz.TextureNames);
        var textured = gz.Materials[0];
        Assert.Equal(("skin.tif", 1, 6f, true), (textured.TextureName, textured.SoilId, textured.CycleSpeed, textured.CycleLooping));
        Assert.Equal(new[] { "skin.tif", "skin.tif" }, textured.CycleTextures);
        Assert.Equal(new Color(1f, 0f, 51 / 255f, 128 / 255f), gz.Materials[1].Color);
        Assert.Null(gz.Materials[1].TextureName);

        // The one live model sits in slot 1, and the node naming that slot gets mesh 0.
        var mesh = Assert.Single(gz.Meshes);
        Assert.Equal(("Facade", "SphericalY", true, false), (mesh.ModelType, mesh.FacadeMode, mesh.Lighting, mesh.Fog));
        Assert.Equal(new Vector2(0.25f, -1f), mesh.TextureScroll);
        Assert.Equal(3, mesh.Vertices.Count);
        var light = Assert.Single(mesh.Lights);
        Assert.Equal((new Vector3(7, 8, 9), 2f, 1500f, 0.17f / 255f), (light.Position, light.BlinkPeriod, light.FadeFar, light.FadeSlope));

        var poly = Assert.Single(mesh.Polygons);
        Assert.Equal(new[] { 2, 1, 0 }, poly.VertexIndices);
        Assert.Null(poly.NormalIndices);
        Assert.Equal((true, true, false, -3, (int?)2), (poly.ShowBackface, poly.TriangleStrip, poly.NoClutter, poly.Priority, poly.ZoneSet));
        Assert.Equal(0, poly.MaterialIndex);
        Assert.Equal(new Vector2(0.5f, 0f), poly.UvCoords![1]);
        var overlay = Assert.Single(poly.OverlayPasses!);
        Assert.Equal((1, new Vector2(1f, 0.5f)), (overlay.MaterialIndex, overlay.UvCoords![1]));
        Assert.Equal(new Color(1f, 1f, 1f), poly.VertexColors![2]);

        Assert.Equal(new[] { "Display", "Object3d", "Object3d" }, gz.Nodes.Select(n => n.Kind));
        var box = gz.Nodes[1];
        Assert.Equal(("box", 0, 2, 1, true, false), (box.Name, box.MeshIndex, box.ZoneId, box.Index, box.Active, box.IntersectSurface));
        Assert.Null(box.Local);
        var arm = gz.Nodes[2];
        Assert.Equal(-1, arm.MeshIndex);
        Assert.Equal(new[] { 1 }, arm.Children);
        Assert.Equal(new Vector3(2, 0, 0), arm.Local!.Value.Basis.Column0);
        Assert.Equal(new Vector3(1, 2, 3), arm.Local.Value.Origin);
    }

    [Fact]
    public void ATrailingByteIsRefused()
    {
        var data = TinyArchive().Append((byte)0).ToArray();
        Assert.Contains("end of file", Assert.Throws<InvalidDataException>(() => GameZZbd.Read(data)).Message);
    }

    [Fact]
    public void ANodeNamingAFreeModelSlotIsRefused()
    {
        var data = TinyArchive();
        int nodes = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(36));
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(nodes + 212 + 60), 0);
        Assert.Contains("model index", Assert.Throws<InvalidDataException>(() => GameZZbd.Read(data)).Message);
    }

    [Fact]
    public void NoSourceReadsAsNoModel()
    {
        Assert.Null(GameZZbd.Load(null, "ZBD/C1/gamez.zbd"));
    }

    [GameFilesTheory(AlsoExtracted = true)]
    [MemberData(nameof(GameZArchives))]
    public void TheReaderBuildsTheModelTheExtractionBuilds(string path)
    {
        var bytes = GameFilesData.Source!.ReadAllBytes("ZBD/" + path);
        var clock = Stopwatch.StartNew();
        var actual = GameZZbd.Read(bytes);
        double decodeMs = clock.Elapsed.TotalMilliseconds;
        var expected = FromExtraction(path);

        int compared = ModelDiff.AssertEqual(expected, actual);
        _output.WriteLine($"{path}: {actual.Nodes.Count} nodes, {actual.Meshes.Count} meshes, {actual.Materials.Count} materials, "
                          + $"{bytes.Length} bytes, decoded in {decodeMs:F1} ms; {compared} leaf values compared");
        Assert.True(compared > 0);
    }

    [GameFilesTheory(AlsoExtracted = true)]
    [InlineData("C2B/gamez.zbd")]
    public void OneChangedFieldInTheDirectModelIsFound(string path)
    {
        var expected = FromExtraction(path);
        var actual = GameZZbd.Read(GameFilesData.Source!.ReadAllBytes("ZBD/" + path));
        int m = actual.Meshes.FindLastIndex(x => x.Polygons.Count > 3);
        actual.Meshes[m].Polygons[2].Priority ^= 1;

        Assert.StartsWith($"GameZ.Meshes[{m}].Polygons[2].Priority: expected ", ModelDiff.FirstDifference(expected, actual));
    }

    private static bool IsGameZ(string path) => Path.GetFileName(path) is "gamez.zbd" or "planes.zbd";

    private static GameZ FromExtraction(string path) => GameZ.Load(path == "planes.zbd"
        ? SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "planes.zip"))
        : SessionPaths.ChapterGamez(TestData.ExtractedRoot!, Path.GetDirectoryName(path)!));

    // A cycled and a coloured material, one live model in slot 1, and three nodes. The parent
    // Object3d's stored matrix does not recompose from its rotation.
    private static byte[] TinyArchive()
    {
        var w = new Writer();
        w.Fill(40);
        int textures = w.Pos;
        w.U32(1).U32(0).U32(0).Bytes(Encoding.ASCII.GetBytes("skin\0tif\0"), 20).U32(1).U32(0).I32(-1);

        int materials = w.Pos;
        w.I32(3).I32(2).I32(2).I32(1);
        w.Material(alpha: 255, flags: 0x11, 255, 255, 255, texture: 0, soil: 1, cycle: 1).I16(1).I16(-1);
        w.Material(alpha: 128, flags: 0x10, 255, 0, 51, texture: 0, soil: 0, cycle: 0).I16(-1).I16(0);
        w.Fill(40).I16(-1).I16(-1);
        w.I32(1).I32(0).F32(0).F32(6).I32(2).I32(2).U32(1).I32(0).I32(0);

        int models = w.Pos;
        w.I32(2).I32(1).I32(2);
        w.Fill(100).I32(-1);
        int record = w.Pos;
        w.Fill(104);

        int modelData = w.Pos;
        w.F32(0).F32(0).F32(0).F32(1).F32(0).F32(0).F32(0).F32(1).F32(0);
        w.I32(0).I32(1).F32(2).I32(1).I32(0).I32(0).U32(0).F32(255).F32(0).F32(0).U32(0).U32(1)
            .F32(0).F32(1500).F32(0.17f).I32(0).F32(0).F32(0).F32(0);
        w.F32(7).F32(8).F32(9);
        w.U32(3 | 0x0400 | 0x2000).I32(-3).U32(1).U32(0).U32(2).U32(1).U32(1).U32(1).U32(1).U32(1 | (2 << 8) | 0xFFFF0000);
        w.U32(2).U32(1).U32(0).I32(0).I32(1);
        w.F32(0).F32(0).F32(0.5f).F32(0).F32(1).F32(1);
        w.F32(0).F32(0).F32(1).F32(0.5f).F32(1).F32(1);
        for (int i = 0; i < 3; i++)
        {
            w.F32(255).F32(255).F32(255);
        }

        w.I32(0).U32(1).U32(1);

        int nodes = w.Pos;
        w.Node("display", kind: 4, flags: 0x1080014, zone: -1, slot: -1, parents: 0, children: 0, index: 2);
        w.Node("box", kind: 5, flags: 0x3080004, zone: 2, slot: 1, parents: 1, children: 0, index: 7);
        w.Node("arm", kind: 5, flags: 0x3080014, zone: -1, slot: -1, parents: 0, children: 1, index: 8);
        w.Fill(28);
        w.U32(0x28).Fill(140).I32(2);
        w.U32(0x20).Fill(20).F32(0).F32(0).F32(0).F32(1).F32(1).F32(1)
            .F32(2).F32(0).F32(0).F32(0).F32(1).F32(0).F32(0).F32(0).F32(1).F32(1).F32(2).F32(3).Fill(48).I32(1);

        var data = w.ToArray();
        void Put(int at, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(at), value);
        Put(0, 0x02971222);
        Put(4, 42);
        Put(12, 1);
        Put(16, textures);
        Put(20, materials);
        Put(24, models);
        Put(28, 3);
        Put(36, nodes);

        // The live model record: Facade, SphericalY, lit; 1 polygon, 3 vertices, 1 light, 1 reference.
        Put(record, 1);
        Put(record + 4, 1);
        Put(record + 8, 1);
        Put(record + 12, 1);
        Put(record + 16, 1);
        Put(record + 20, 3);
        Put(record + 32, 1);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(record + 40), 0.25f);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(record + 44), -1f);
        Put(record + 92, 1);
        Put(record + 100, modelData);
        return data;
    }

    private sealed class Writer
    {
        private readonly List<byte> _bytes = new();

        public int Pos => _bytes.Count;

        public byte[] ToArray() => _bytes.ToArray();

        public Writer Fill(int n)
        {
            _bytes.AddRange(new byte[n]);
            return this;
        }

        public Writer Bytes(byte[] b, int width)
        {
            _bytes.AddRange(b);
            return Fill(width - b.Length);
        }

        public Writer I16(short v) => Raw(BitConverter.GetBytes(v));

        public Writer I32(int v) => Raw(BitConverter.GetBytes(v));

        public Writer U32(uint v) => Raw(BitConverter.GetBytes(v));

        public Writer F32(float v) => Raw(BitConverter.GetBytes(v));

        public Writer Material(byte alpha, byte flags, float r, float g, float b, int texture, uint soil, uint cycle)
        {
            _bytes.Add(alpha);
            _bytes.Add(flags);
            return I16(0).F32(r).F32(g).F32(b).I32(texture).F32(0).F32(0.5f).F32(0.5f).U32(soil).U32(cycle);
        }

        public Writer Node(string name, uint kind, uint flags, sbyte zone, int slot, short parents, short children, int index)
        {
            Bytes(Encoding.ASCII.GetBytes(name + "\0"), 36).U32(flags).U32(0).U32(1);
            _bytes.AddRange(new[] { (byte)zone, (byte)0, (byte)0, (byte)0 });
            U32(kind).U32(1).I32(slot).Fill(20).I16(parents).I16(children).Fill(120);
            return I32(0x02000000 | index);
        }

        private Writer Raw(byte[] b)
        {
            _bytes.AddRange(b);
            return this;
        }
    }
}
