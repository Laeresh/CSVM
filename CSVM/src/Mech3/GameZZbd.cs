using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Extraction;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Reads a chapter's <c>gamez.zbd</c> or the root <c>planes.zbd</c> into the <see cref="GameZ"/>
/// model, engine-free. Layout and the reader's rules: docs/formats/gamez-zbd.md.
/// ⚠ A node's mesh index is the live model's position among the live models, never its slot in
/// the file's sparse model array.
/// </summary>
public static class GameZZbd
{
    private const uint Signature = 0x02971222;
    private const uint Version = 42;
    private const int HeaderSize = 40;
    private const int TextureEntrySize = 44;
    private const int MaterialEntrySize = 44; // the 40-byte record and its two free-list links
    private const int ModelEntrySize = 104; // the 100-byte record and its data offset or free link
    private const int NodeEntrySize = 212; // the 208-byte record and its tagged node index

    private const int TextureFlagTextured = 0x01;
    private const uint ObjectTransformInitial = 0x08;
    private const uint ObjectUseMatrix = 0x10;

    // The soil ids GameZ.SoilLabelToId admits; on disk they are already the ids.
    private static readonly HashSet<uint> KnownSoils = new() { 0, 1, 5, 8, 11, 12, 13 };
    private static readonly string[] ModelTypes = { "Default", "Facade", "Points" };
    private static readonly string[] FacadeModes = { "CylindricalY", "SphericalY", "CylindricalX", "CylindricalZ" };

    /// <summary>The model of the archive at <paramref name="path"/> (<c>ZBD/C1/gamez.zbd</c>,
    /// <c>ZBD/planes.zbd</c>), or null without a source or without the file.</summary>
    public static GameZ? Load(IGameFileSource? game, string path) =>
        game != null && game.Exists(path) ? Read(game.ReadAllBytes(path)) : null;

    /// <summary>Decodes a whole archive. Throws <see cref="InvalidDataException"/> on a file that is
    /// not one, or whose regions do not meet where the header says.</summary>
    public static GameZ Read(byte[] data)
    {
        var r = new Cursor(data);
        Expect(data.Length >= HeaderSize && r.U32(0) == Signature && r.U32(4) == Version, "header", 0);
        int textureCount = r.I32(12);
        int materialsOffset = r.I32(20), modelsOffset = r.I32(24), nodesOffset = r.I32(36);
        int nodeCount = r.I32(28);
        Expect(r.I32(16) == HeaderSize && textureCount >= 0 && nodeCount > 0
               && materialsOffset > HeaderSize && modelsOffset > materialsOffset && nodesOffset > modelsOffset,
            "header", 12);

        var gz = new GameZ();
        r.Pos = HeaderSize;
        for (int i = 0; i < textureCount; i++)
        {
            gz.TextureNames.Add(TextureName(r.Take(TextureEntrySize).Slice(12, 20), r.Pos - TextureEntrySize));
        }

        r.ExpectAt(materialsOffset, "materials");
        ReadMaterials(r, gz);
        r.ExpectAt(modelsOffset, "models");
        int[] denseOfSlot = ReadModels(r, gz);
        r.ExpectAt(nodesOffset, "nodes");
        ReadNodes(r, gz, nodeCount, denseOfSlot);
        r.ExpectAt(data.Length, "end of file");
        return gz;
    }

    /// <summary>Whether the stored matrix is exactly the one the rotation, scale and translation
    /// recompose to, rotating Y, then X, then Z, in single precision. The extraction keeps the
    /// stored matrix only where it is not, and the model takes the rotation where it is.</summary>
    internal static bool Recomposes(Vector3 rotate, Vector3 scale, ReadOnlySpan<float> m)
    {
        float sx = MathF.Sin(rotate.X), cx = MathF.Cos(rotate.X);
        float sy = MathF.Sin(rotate.Y), cy = MathF.Cos(rotate.Y);
        float sz = MathF.Sin(rotate.Z), cz = MathF.Cos(rotate.Z);
        Span<float> e = stackalloc float[9]
        {
            (sy * sx * sz) + (cy * cz), cx * sz, (cy * sx * sz) - (sy * cz),
            (sy * sx * cz) - (cy * sz), cx * cz, (cy * sx * cz) + (sy * sz),
            sy * cx, -sx, cy * cx,
        };
        for (int row = 0; row < 3; row++)
        {
            float s = row == 0 ? scale.X : row == 1 ? scale.Y : scale.Z;
            for (int col = 0; col < 3; col++)
            {
                float v = s != 1f ? e[(row * 3) + col] * s : e[(row * 3) + col];
                if (v != m[(row * 3) + col])
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>A texture-table name, its first NUL read as a period when a suffix follows. So
    /// <c>cowling\0.tif\0</c> reads <c>cowling..tif</c> (docs/formats/gamez.md).</summary>
    internal static string TextureName(ReadOnlySpan<byte> raw, int offset)
    {
        int first = raw.IndexOf((byte)0);
        Expect(first >= 0, "texture name", offset);
        var rest = raw[(first + 1)..];
        int second = rest.IndexOf((byte)0);
        string prefix = Ascii(raw[..first], offset);
        if (second == 0 || rest.Length == 0)
        {
            return prefix;
        }

        return prefix + "." + Ascii(second < 0 ? rest : rest[..second], offset);
    }

    private static void ReadMaterials(Cursor r, GameZ gz)
    {
        int arraySize = r.I32(r.Pos), count = r.I32(r.Pos + 4);
        Expect(count >= 0 && count < arraySize, "material counts", r.Pos);
        r.Pos += 16;
        var cycled = new List<GameZMaterial>();
        for (int i = 0; i < count; i++)
        {
            int at = r.Pos;
            r.Take(MaterialEntrySize);
            var mat = new GameZMaterial();
            uint soil = r.U32(at + 32);
            if (!KnownSoils.Contains(soil))
            {
                throw new FormatException($"Unknown material soil id {soil} at offset {at + 32}; GameZ.SoilLabelToId needs an entry.");
            }

            mat.SoilId = (int)soil;
            if ((r.Data[at + 1] & TextureFlagTextured) != 0)
            {
                mat.TextureName = TextureAt(gz, r.I32(at + 16), at + 16);
                if (r.U32(at + 36) != 0)
                {
                    cycled.Add(mat);
                }
            }
            else
            {
                mat.Color = new Color(r.F32(at + 4) / 255f, r.F32(at + 8) / 255f, r.F32(at + 12) / 255f, r.Data[at] / 255f);
            }

            gz.Materials.Add(mat);
        }

        r.Take((arraySize - count) * MaterialEntrySize);

        // The flipbooks follow the whole array, in material order.
        foreach (var mat in cycled)
        {
            int at = r.Pos;
            r.Take(28);
            mat.CycleLooping = r.I32(at) != 0;
            mat.CycleSpeed = r.F32(at + 12);
            int frames = r.I32(at + 16);
            Expect(frames >= 0, "cycle frame count", at + 16);
            for (int f = 0; f < frames; f++)
            {
                mat.CycleTextures.Add(TextureAt(gz, r.I32(r.Pos), r.Pos));
                r.Pos += 4;
            }
        }
    }

    // The sparse model array: a record with no parents is a free slot.
    private static int[] ReadModels(Cursor r, GameZ gz)
    {
        int arraySize = r.I32(r.Pos), count = r.I32(r.Pos + 4);
        Expect(count >= 0 && arraySize >= count, "model counts", r.Pos);
        r.Pos += 12;
        var denseOfSlot = new int[arraySize];
        var live = new List<(int Record, int Data)>();
        for (int slot = 0; slot < arraySize; slot++)
        {
            int at = r.Pos;
            r.Take(ModelEntrySize);
            denseOfSlot[slot] = -1;
            if (r.U32(at + 12) > 0)
            {
                denseOfSlot[slot] = live.Count;
                live.Add((at, r.I32(at + 100)));
            }
        }

        Expect(live.Count == count, "live model count", r.Pos);
        foreach (var (record, dataAt) in live)
        {
            r.ExpectAt(dataAt, "model data");
            gz.Meshes.Add(ReadModel(r, record));
        }

        return denseOfSlot;
    }

    private static GameZMesh ReadModel(Cursor r, int at)
    {
        uint type = r.U32(at), facade = r.U32(at + 4), flags = r.U32(at + 8);
        Expect(type < ModelTypes.Length && facade < FacadeModes.Length, "model type", at);
        var mesh = new GameZMesh
        {
            ModelType = ModelTypes[type],
            FacadeMode = FacadeModes[facade],
            Lighting = (flags & 1) != 0,
            Fog = (flags & 2) != 0,
            TextureScroll = new Vector2(r.F32(at + 40), r.F32(at + 44)),
        };
        int polygons = r.Count(at + 16), vertices = r.Count(at + 20), normals = r.Count(at + 24);
        int morphs = r.Count(at + 28), lights = r.Count(at + 32), materialRefs = r.Count(at + 92);

        for (int i = 0; i < vertices; i++)
        {
            mesh.Vertices.Add(r.Vec3());
        }

        for (int i = 0; i < normals; i++)
        {
            mesh.Normals.Add(r.Vec3());
        }

        r.Take(morphs * 12);
        ReadLights(r, mesh, lights);

        int infos = r.Pos;
        r.Take(polygons * 40);
        for (int i = 0; i < polygons; i++)
        {
            mesh.Polygons.Add(ReadPolygon(r, infos + (i * 40)));
        }

        // Material references are derivable from the polygons, which is all the model keeps.
        r.Take(materialRefs * 12);
        return mesh;
    }

    private static void ReadLights(Cursor r, GameZMesh mesh, int count)
    {
        int records = r.Pos;
        r.Take(count * 76);
        for (int i = 0; i < count; i++)
        {
            int at = records + (i * 76);
            int points = r.I32(at + 12);
            Expect(points >= 0, "light vertex count", at + 12);
            var first = points > 0 ? r.Vec3() : default;
            r.Take(Math.Max(0, points - 1) * 12);
            if (points == 0)
            {
                continue;
            }

            // Field meanings are decoded off the original's own draw; see GameZLight.
            mesh.Lights.Add(new GameZLight
            {
                Position = first,
                Color = new Color(r.F32(at + 28) / 255f, r.F32(at + 32) / 255f, r.F32(at + 36) / 255f),
                BlinkPeriod = r.I32(at + 4) == 1 ? r.F32(at + 8) : 0f,
                FadeFar = r.F32(at + 52),
                FadeSlope = r.F32(at + 56) / 255f,
            });
        }
    }

    private static GameZPolygon ReadPolygon(Cursor r, int info)
    {
        uint flags = r.U32(info);
        int corners = (int)(flags & 0x3FF), passes = r.Count(info + 16);
        uint zoneSet = r.U32(info + 36);
        var poly = new GameZPolygon
        {
            ShowBackface = (flags & 0x0400) != 0,
            NoClutter = (flags & 0x0800) != 0,
            TriangleStrip = (flags & 0x2000) != 0,
            Priority = r.I32(info + 4),
            ZoneSet = (zoneSet & 0xFF) > 0 ? (sbyte)(zoneSet >> 8) : null,
            VertexColors = new List<Color>(corners),
        };
        for (int i = 0; i < corners; i++)
        {
            poly.VertexIndices.Add(r.I32(r.Pos));
            r.Pos += 4;
        }

        if (r.U32(info + 12) != 0)
        {
            poly.NormalIndices = new List<int>(corners);
            for (int i = 0; i < corners; i++)
            {
                poly.NormalIndices.Add(r.I32(r.Pos));
                r.Pos += 4;
            }
        }

        // The material indices come first, then each one's UVs: the base skin, then the overlays.
        int indices = r.Pos;
        r.Take(passes * 4);
        bool hasUvs = r.U32(info + 24) != 0;
        for (int pass = 0; pass < passes; pass++)
        {
            List<Vector2>? uv = null;
            if (hasUvs)
            {
                uv = new List<Vector2>(corners);
                for (int i = 0; i < corners; i++)
                {
                    uv.Add(new Vector2(r.F32(r.Pos), r.F32(r.Pos + 4)));
                    r.Pos += 8;
                }
            }

            int material = r.I32(indices + (pass * 4));
            if (pass == 0)
            {
                poly.MaterialIndex = material;
                poly.UvCoords = uv;
            }
            else
            {
                (poly.OverlayPasses ??= new List<GameZPolygonPass>()).Add(new GameZPolygonPass { MaterialIndex = material, UvCoords = uv });
            }
        }

        for (int i = 0; i < corners; i++)
        {
            var c = r.Vec3();
            poly.VertexColors.Add(new Color(c.X / 255f, c.Y / 255f, c.Z / 255f));
        }

        r.EnsureWithin();
        return poly;
    }

    private static void ReadNodes(Cursor r, GameZ gz, int count, int[] denseOfSlot)
    {
        int records = r.Pos;
        r.Take(count * NodeEntrySize);
        for (int i = 0; i < count; i++)
        {
            int at = records + (i * NodeEntrySize);
            uint flags = r.U32(at + 36);
            uint kind = r.U32(at + 52);
            int slot = r.I32(at + 60);
            var node = new GameZNode
            {
                Kind = KindName(kind, at + 52),
                Name = Ascii(NulTerminated(r.Data.AsSpan(at, 36), at), at),
                Index = i,
                IntersectSurface = (flags & 0x10) != 0,
                Active = (flags & 0x04) != 0,
                CanModify = (flags & 0x10000) != 0,
                ZoneId = (sbyte)r.Data[at + 48],
                MissionTargetWord = r.U32(at + 40),
            };
            if (slot >= 0)
            {
                Expect(slot < denseOfSlot.Length && denseOfSlot[slot] >= 0, "model index", at + 60);
                node.MeshIndex = denseOfSlot[slot];
            }

            ReadNodeData(r, node);

            int parents = r.U16(at + 84), children = r.U16(at + 86);
            r.Take(parents * 4);
            for (int c = 0; c < children; c++)
            {
                node.Children.Add(r.I32(r.Pos));
                r.Pos += 4;
            }

            r.EnsureWithin();
            gz.Nodes.Add(node);
        }
    }

    private static void ReadNodeData(Cursor r, GameZNode node)
    {
        int at = r.Pos;
        switch (node.Kind)
        {
            case "Camera":
                r.Take(488);
                break;
            case "Display":
                r.Take(28);
                break;
            case "Window":
                r.Take(248);
                break;
            case "Light":
                r.Take(256 + 4); // the light record and its one parent index
                break;
            case "Lod":
                r.Take(92);
                node.LodRangeMin = MathF.Sqrt(r.F32(at + 4)); // stored squared
                break;
            case "Object3d":
                r.Take(144);
                node.Local = ObjectTransform(r, at);
                break;
            case "World":
                ReadWorld(r, node);
                break;
        }
    }

    private static Transform3D? ObjectTransform(Cursor r, int at)
    {
        uint flags = r.U32(at);
        if ((flags & ObjectTransformInitial) != 0)
        {
            return null;
        }

        // ⚠ No shipped node uses the bare-matrix form, and today's JSON loader cannot read it.
        Expect((flags & ObjectUseMatrix) == 0, "object transform form", at);
        var rotate = r.Vec3At(at + 24);
        var scale = r.Vec3At(at + 36);
        Span<float> m = stackalloc float[12];
        for (int k = 0; k < 12; k++)
        {
            m[k] = r.F32(at + 48 + (k * 4));
        }

        // Stored transposed: the matrix's columns are its rows of three (docs/formats/gamez.md).
        var basis = Recomposes(rotate, scale, m)
            ? Basis.FromEuler(rotate, EulerOrder.Yxz)
            : new Basis(new Vector3(m[0], m[1], m[2]), new Vector3(m[3], m[4], m[5]), new Vector3(m[6], m[7], m[8]));
        return new Transform3D(basis, new Vector3(m[9], m[10], m[11]));
    }

    private static void ReadWorld(Cursor r, GameZNode node)
    {
        int at = r.Pos;
        r.Take(204 + 4); // the world record and its one child value
        node.HasArea = true;

        // Read through an integer, as the extraction writes the area, so a -0 reads +0.
        node.AreaLeft = (int)r.F32(at + 52);
        node.AreaBottom = (int)r.F32(at + 56);
        node.AreaRight = (int)r.F32(at + 68);
        node.AreaTop = (int)r.F32(at + 72);
        int cols = r.Count(at + 152), rows = r.Count(at + 156);

        // Row-major from the area's high z edge down, columns from its low x edge.
        node.PartitionRows = rows;
        node.PartitionNodes = new List<int>();
        node.PartitionCellNodes = new List<List<int>>();
        var seen = new HashSet<int>();
        for (int z = 0; z < rows; z++)
        {
            node.PartitionCols = cols;
            for (int x = 0; x < cols; x++)
            {
                int cell = r.Pos;
                r.Take(88);
                if (node.PartitionCellX == 0f)
                {
                    float x0 = r.F32(cell + 16), z0 = r.F32(cell + 24), x1 = r.F32(cell + 28), z1 = r.F32(cell + 36);
                    node.PartitionOriginX = x0;
                    node.PartitionOriginZ = z1;
                    node.PartitionCellX = Mathf.Abs(x1 - x0);
                    node.PartitionCellZ = Mathf.Abs(z1 - z0);
                }

                var members = new List<int>();
                node.PartitionCellNodes.Add(members);
                int values = r.U16(cell + 58);
                for (int v = 0; v < values; v++)
                {
                    int index = r.I32(r.Pos);
                    r.Take(12); // the node index, then the cell's y range for it
                    members.Add(index);
                    if (seen.Add(index))
                    {
                        node.PartitionNodes.Add(index);
                    }
                }
            }
        }
    }

    private static string KindName(uint kind, int offset) => kind switch
    {
        1 => "Camera",
        2 => "World",
        3 => "Window",
        4 => "Display",
        5 => "Object3d",
        6 => "Lod",
        9 => "Light",
        _ => throw new InvalidDataException($"gamez: bad node class {kind} at offset {offset}"),
    };

    private static string TextureAt(GameZ gz, int index, int offset)
    {
        Expect(index >= 0 && index < gz.TextureNames.Count, "texture index", offset);
        return gz.TextureNames[index];
    }

    private static ReadOnlySpan<byte> NulTerminated(ReadOnlySpan<byte> raw, int offset)
    {
        int end = raw.IndexOf((byte)0);
        Expect(end >= 0, "name terminator", offset);
        return raw[..end];
    }

    private static string Ascii(ReadOnlySpan<byte> bytes, int offset)
    {
        foreach (byte b in bytes)
        {
            Expect(b < 0x80, "name", offset);
        }

        return Encoding.ASCII.GetString(bytes);
    }

    private static void Expect(bool ok, string what, long offset)
    {
        if (!ok)
        {
            throw new InvalidDataException($"gamez: bad {what} at offset {offset}");
        }
    }

    // A position over the file whose every advance is bounds-checked; fixed-offset reads go
    // through the same checks.
    private sealed class Cursor
    {
        public Cursor(byte[] data) => Data = data;

        public byte[] Data { get; }

        public int Pos { get; set; }

        public ReadOnlySpan<byte> Take(int length)
        {
            Expect(length >= 0 && length <= Data.Length - Pos, "length past the end", Pos);
            var span = Data.AsSpan(Pos, length);
            Pos += length;
            return span;
        }

        public void ExpectAt(int offset, string what) => Expect(Pos == offset, $"start of {what}", Pos);

        public void EnsureWithin() => Expect(Pos <= Data.Length, "data past the end", Pos);

        public Vector3 Vec3()
        {
            var v = Vec3At(Pos);
            Pos += 12;
            return v;
        }

        public Vector3 Vec3At(int at) => new(F32(at), F32(at + 4), F32(at + 8));

        public int Count(int at)
        {
            int n = I32(at);
            Expect(n >= 0, "count", at);
            return n;
        }

        public int I32(int at) => BinaryPrimitives.ReadInt32LittleEndian(Span(at, 4));

        public uint U32(int at) => BinaryPrimitives.ReadUInt32LittleEndian(Span(at, 4));

        public int U16(int at) => BinaryPrimitives.ReadUInt16LittleEndian(Span(at, 2));

        public float F32(int at) => BinaryPrimitives.ReadSingleLittleEndian(Span(at, 4));

        private ReadOnlySpan<byte> Span(int at, int length)
        {
            Expect(at >= 0 && at <= Data.Length - length, "read past the end", at);
            return Data.AsSpan(at, length);
        }
    }
}
