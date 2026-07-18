using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Godot;

namespace CrimsonSkies.Mech3;

/// <summary>
/// In-memory model of a mech3ax GameZ extraction (planes.zbd / gamez.zbd → ZIP of
/// nodes.json / meshes.json / materials.json). Only the fields the renderer needs.
/// </summary>
public sealed class GameZ
{
    public List<GameZNode> Nodes { get; } = new();
    public List<GameZMesh> Meshes { get; } = new();
    public List<GameZMaterial> Materials { get; } = new();

    /// <summary>Loads from a mech3ax output ZIP, or from a directory of the same JSON files.</summary>
    public static GameZ Load(string path)
    {
        var gz = new GameZ();
        if (Directory.Exists(path))
        {
            using var nodes = File.OpenRead(Path.Combine(path, "nodes.json"));
            gz.ParseNodes(nodes);
            using var meshes = File.OpenRead(Path.Combine(path, "meshes.json"));
            gz.ParseMeshes(meshes);
            using var mats = File.OpenRead(Path.Combine(path, "materials.json"));
            gz.ParseMaterials(mats);
        }
        else
        {
            using var zip = ZipFile.OpenRead(path);
            using (var s = OpenEntry(zip, "nodes.json")) gz.ParseNodes(s);
            using (var s = OpenEntry(zip, "meshes.json")) gz.ParseMeshes(s);
            using (var s = OpenEntry(zip, "materials.json")) gz.ParseMaterials(s);
        }
        return gz;
    }

    private static Stream OpenEntry(ZipArchive zip, string name) =>
        (zip.GetEntry(name) ?? throw new FileNotFoundException($"'{name}' missing from archive")).Open();

    public GameZNode? FindByName(string name)
    {
        foreach (var n in Nodes)
            if (string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase))
                return n;
        return null;
    }

    private void ParseNodes(Stream stream)
    {
        using var doc = JsonDocument.Parse(BufferAll(stream));
        int index = 0;
        foreach (var wrapper in doc.RootElement.EnumerateArray())
        {
            // Each node is an enum wrapper: {"Object3d": {...}} or {"Lod": {...}}
            var prop = FirstProperty(wrapper);
            var body = prop.Value;
            // Not every kind has every field: Display lacks name+children,
            // Window/Camera/Light lack children.
            var node = new GameZNode
            {
                Kind = prop.Name,
                Name = body.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "",
                MeshIndex = body.TryGetProperty("mesh_index", out var mi) ? mi.GetInt32() : -1,
                Index = index++,
            };
            if (body.TryGetProperty("children", out var kids) && kids.ValueKind == JsonValueKind.Array)
                foreach (var c in kids.EnumerateArray())
                    node.Children.Add(c.GetInt32());
            if (node.Kind == "Lod")
                node.LodRangeMin = body.GetProperty("range").GetProperty("min").GetSingle();
            if (node.Kind == "World")
            {
                // The map's ground-plane bounds: area = {left=xMin, right=xMax, top=zMin,
                // bottom=zMax} in Godot world coords. WorldBuilder mirrors the border terrain
                // outward across these edges (map-edge continuation).
                if (body.TryGetProperty("area", out var area) && area.ValueKind == JsonValueKind.Object)
                {
                    node.HasArea = true;
                    node.AreaLeft = area.GetProperty("left").GetSingle();
                    node.AreaTop = area.GetProperty("top").GetSingle();
                    node.AreaRight = area.GetProperty("right").GetSingle();
                    node.AreaBottom = area.GetProperty("bottom").GetSingle();
                }
                if (body.TryGetProperty("partitions", out var parts) && parts.ValueKind == JsonValueKind.Array)
                {
                    // The world's spatial grid references top-level (parentless) subtrees that
                    // are placed in the world but are NOT in the world's children list. Its
                    // dimensions (rows × cols of one-cell partitions) also give the tile size.
                    node.PartitionNodes = new List<int>();
                    node.PartitionRows = parts.GetArrayLength();
                    var seen = new HashSet<int>();
                    foreach (var row in parts.EnumerateArray())
                    {
                        node.PartitionCols = row.GetArrayLength();
                        foreach (var cell in row.EnumerateArray())
                            foreach (var nref in cell.GetProperty("nodes").EnumerateArray())
                            {
                                int idx = nref.GetProperty("index").GetInt32();
                                if (seen.Add(idx))
                                    node.PartitionNodes.Add(idx);
                            }
                    }
                }
            }
            if (body.TryGetProperty("transformation", out var tf) && tf.ValueKind == JsonValueKind.Object)
                node.Local = ParseTransform(tf);
            Nodes.Add(node);
        }
    }

    private static Transform3D ParseTransform(JsonElement tf)
    {
        var tr = ParseVec3(tf.GetProperty("translation"));
        Basis basis;
        if (tf.TryGetProperty("matrix", out var m) && m.ValueKind == JsonValueKind.Object)
        {
            // Stored transposed: the actual rotation matrix has columns (a,b,c), (d,e,f), (g,h,i).
            basis = new Basis(
                new Vector3(m.GetProperty("a").GetSingle(), m.GetProperty("b").GetSingle(), m.GetProperty("c").GetSingle()),
                new Vector3(m.GetProperty("d").GetSingle(), m.GetProperty("e").GetSingle(), m.GetProperty("f").GetSingle()),
                new Vector3(m.GetProperty("g").GetSingle(), m.GetProperty("h").GetSingle(), m.GetProperty("i").GetSingle()));
        }
        else
        {
            // Euler angles compose as R = Ry(y)·Rx(x)·Rz(z) — Godot's YXZ order
            // (verified numerically against the 221 nodes that carry both forms).
            basis = Basis.FromEuler(ParseVec3(tf.GetProperty("rotation")), EulerOrder.Yxz);
        }
        return new Transform3D(basis, tr);
    }

    private void ParseMeshes(Stream stream)
    {
        using var doc = JsonDocument.Parse(BufferAll(stream));
        foreach (var m in doc.RootElement.EnumerateArray())
        {
            var mesh = new GameZMesh();
            if (m.ValueKind != JsonValueKind.Object)
            {
                Meshes.Add(mesh); // null slot — keep it so mesh_index stays aligned
                continue;
            }
            foreach (var v in m.GetProperty("vertices").EnumerateArray())
                mesh.Vertices.Add(ParseVec3(v));
            foreach (var n in m.GetProperty("normals").EnumerateArray())
                mesh.Normals.Add(ParseVec3(n));
            foreach (var p in m.GetProperty("polygons").EnumerateArray())
            {
                var poly = new GameZPolygon();
                foreach (var vi in p.GetProperty("vertex_indices").EnumerateArray())
                    poly.VertexIndices.Add(vi.GetInt32());
                if (p.TryGetProperty("normal_indices", out var ni) && ni.ValueKind == JsonValueKind.Array)
                {
                    poly.NormalIndices = new List<int>();
                    foreach (var x in ni.EnumerateArray())
                        poly.NormalIndices.Add(x.GetInt32());
                }
                var pf = p.GetProperty("flags");
                poly.TriangleStrip = pf.TryGetProperty("triangle_strip", out var ts) && ts.GetBoolean();
                // Double-sided flag. mech3ax v0.6.1 emits it as "unk2"; upstream has since
                // identified it as SHOW_BACKFACE — accept both spellings.
                poly.ShowBackface = (pf.TryGetProperty("unk2", out var bf) || pf.TryGetProperty("show_backface", out bf))
                    && bf.ValueKind == JsonValueKind.True;
                // Draw-priority layer. mech3ax v0.6.1 emits it as "unk04"; upstream has
                // since identified and renamed it to "priority" — accept both spellings.
                if (p.TryGetProperty("unk04", out var pr) || p.TryGetProperty("priority", out pr))
                    poly.Priority = pr.GetInt32();
                if (p.TryGetProperty("vertex_colors", out var vcs) && vcs.ValueKind == JsonValueKind.Array)
                {
                    // Baked per-corner lighting (RGB 0-255); the original engine renders
                    // texture × vertex color with no dynamic world lighting.
                    poly.VertexColors = new List<Color>();
                    foreach (var vc in vcs.EnumerateArray())
                        poly.VertexColors.Add(new Color(
                            vc.GetProperty("r").GetSingle() / 255f,
                            vc.GetProperty("g").GetSingle() / 255f,
                            vc.GetProperty("b").GetSingle() / 255f));
                }
                if (p.TryGetProperty("materials", out var pms) && pms.GetArrayLength() > 0)
                {
                    var pm = pms[0];
                    poly.MaterialIndex = pm.GetProperty("material_index").GetInt32();
                    if (pm.TryGetProperty("uv_coords", out var uvs) && uvs.ValueKind == JsonValueKind.Array)
                    {
                        poly.UvCoords = new List<Vector2>();
                        foreach (var uv in uvs.EnumerateArray())
                            poly.UvCoords.Add(new Vector2(uv.GetProperty("u").GetSingle(), uv.GetProperty("v").GetSingle()));
                    }
                }
                mesh.Polygons.Add(poly);
            }
            // Point lights rendered as glowing sprites by the original engine: the night
            // sky's stars (64 on the horizon 'stars' mesh) and nav/tower beacons.
            if (m.TryGetProperty("lights", out var lights) && lights.ValueKind == JsonValueKind.Array)
            {
                foreach (var l in lights.EnumerateArray())
                {
                    if (!l.TryGetProperty("extra", out var extra) || extra.GetArrayLength() == 0)
                        continue;
                    var c = l.GetProperty("color");
                    mesh.Lights.Add(new GameZLight
                    {
                        Position = ParseVec3(extra[0]),
                        Color = new Color(
                            c.GetProperty("r").GetSingle() / 255f,
                            c.GetProperty("g").GetSingle() / 255f,
                            c.GetProperty("b").GetSingle() / 255f),
                    });
                }
            }
            Meshes.Add(mesh);
        }
    }

    private void ParseMaterials(Stream stream)
    {
        using var doc = JsonDocument.Parse(BufferAll(stream));
        foreach (var wrapper in doc.RootElement.EnumerateArray())
        {
            var prop = FirstProperty(wrapper);
            var body = prop.Value;
            var mat = new GameZMaterial();
            if (prop.Name == "Textured")
            {
                mat.TextureName = body.GetProperty("texture").GetString();
            }
            else // Colored
            {
                var c = body.GetProperty("color");
                mat.Color = new Color(
                    c.GetProperty("r").GetSingle() / 255f,
                    c.GetProperty("g").GetSingle() / 255f,
                    c.GetProperty("b").GetSingle() / 255f,
                    body.GetProperty("alpha").GetSingle() / 255f);
            }
            Materials.Add(mat);
        }
    }

    private static JsonProperty FirstProperty(JsonElement e)
    {
        foreach (var p in e.EnumerateObject())
            return p;
        throw new InvalidDataException("empty enum wrapper object");
    }

    private static Vector3 ParseVec3(JsonElement e) => new(
        e.GetProperty("x").GetSingle(),
        e.GetProperty("y").GetSingle(),
        e.GetProperty("z").GetSingle());

    private static byte[] BufferAll(Stream s)
    {
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}

public sealed class GameZNode
{
    public string Kind = "";   // "Object3d", "Lod", "World", "Display", "Window", "Camera", "Light"
    public string Name = "";
    public int MeshIndex = -1;
    // Flat position in nodes.json. The file is a depth-first serialization of the tree,
    // so this is the original engine's draw order — the cross-node tie-break for
    // coplanar surfaces of equal polygon priority (later node draws on top).
    public int Index;
    public List<int> Children { get; } = new(); // indices into GameZ.Nodes (list positions, not node_index)
    public Transform3D? Local;
    public float LodRangeMin = -1f; // Lod nodes only; 0 = nearest/highest detail
    public List<int>? PartitionNodes; // World nodes only: distinct subtree roots placed via the spatial grid
    // World nodes only: the map's ground-plane bounds (area = {left=xMin, right=xMax,
    // top=zMin, bottom=zMax}, Godot world coords) and its partition grid size. WorldBuilder
    // uses these to mirror the outermost border terrain outward past the map edge.
    public bool HasArea;
    public float AreaLeft, AreaTop, AreaRight, AreaBottom;
    public int PartitionCols, PartitionRows;
}

public sealed class GameZMesh
{
    public List<Vector3> Vertices { get; } = new();
    public List<Vector3> Normals { get; } = new();
    public List<GameZPolygon> Polygons { get; } = new();
    public List<GameZLight> Lights { get; } = new(); // point-sprite lights (stars, nav beacons)
}

public struct GameZLight
{
    public Vector3 Position;
    public Color Color;
}

public sealed class GameZPolygon
{
    public List<int> VertexIndices { get; } = new();
    public List<int>? NormalIndices;
    public List<Vector2>? UvCoords;
    public List<Color>? VertexColors; // baked per-corner lighting, parallel to VertexIndices
    public int MaterialIndex = -1;
    public bool TriangleStrip;
    // SHOW_BACKFACE ("unk2" in v0.6.1): render double-sided. Polygons without it are
    // backface-culled by the original engine — e.g. the autogyro's interior frame
    // lattice, which faces inward and must vanish from an outside camera.
    public bool ShowBackface;
    // Draw-priority layer for coplanar geometry: 0 = base surface, >0 drawn on top
    // (terrain-transition patches, road/shadow decals, plane logos, cockpit gauge
    // needles up to 49), <0 drawn behind (skydome walls -49, zeppelin gasbags -10).
    public int Priority;
}

public sealed class GameZMaterial
{
    public string? TextureName; // set for Textured materials (e.g. "bldhwk_cowling.tif", may be truncated to 20 chars)
    public Color Color = Colors.White; // set for Colored materials
}
