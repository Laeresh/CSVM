using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Godot;

namespace CrimsonSkies.Mech3;

/// <summary>
/// In-memory model of a mech3ax GameZ extraction (planes.zbd / gamez.zbd → ZIP of
/// nodes.json / models.json / materials.json / textures.json). Only the fields the
/// renderer needs.
///
/// <para><b>Two extraction shapes are accepted</b> (2026-07-21, revival-plan item 13).
/// The pinned mech3ax v0.6.1 binary emits the "legacy" shape; the fork (which restored
/// CS gamez support on top of upstream's unified API) emits a "unified" one. They carry
/// semantically identical data — verified field-for-field on C1 + planes: same node
/// count and order, same model_index values, same transforms, same partitions — but
/// spell it differently:</para>
/// <list type="bullet">
/// <item>nodes.json: <c>{"Object3d": {…}}</c> → a flat node with the variant under
/// <c>data</c>; <c>mesh_index</c> → <c>model_index</c>, <c>children</c> →
/// <c>child_indices</c>, <c>transformation</c> → <c>transform</c> (an "Initial" string
/// where the legacy shape wrote null, else a <c>RotateTranslateScale</c> whose
/// <c>original</c> is the legacy <c>matrix</c>).</item>
/// <item>meshes.json → models.json; polygon <c>unk04</c> → <c>priority</c>,
/// <c>triangle_strip</c> → <c>tri_strip</c>; mesh light <c>extra</c> →
/// <c>vertices</c>.</item>
/// <item>materials.json: <c>texture</c> (a name) → <c>texture_index</c> into
/// textures.json, whose entries went from <c>{original, renamed}</c> to
/// <c>{name}</c>.</item>
/// </list>
/// <para>Reading both keeps `tools/` rollback-able to v0.6.1 without a code revert —
/// the conservatism the revival plan asks for at this step — and let the port be proven
/// by rendering the same scene from both trees.</para>
/// </summary>
public sealed class GameZ
{
    public List<GameZNode> Nodes { get; } = new();
    public List<GameZMesh> Meshes { get; } = new();
    public List<GameZMaterial> Materials { get; } = new();

    // textures.json order. Only the unified shape needs it: its materials reference
    // textures by index, where the legacy shape inlined the name. Empty when legacy.
    private readonly List<string> _textureNames = new();

    private int[]? _parent; // flat index → parent flat index (−1 for roots), built lazily

    /// <summary>Loads from a mech3ax output ZIP, or from a directory of the same JSON files.</summary>
    public static GameZ Load(string path)
    {
        var gz = new GameZ();
        if (Directory.Exists(path))
        {
            using var nodes = File.OpenRead(Path.Combine(path, "nodes.json"));
            gz.ParseNodes(nodes);
            // "models.json" is the fork's name for what v0.6.1 called "meshes.json".
            var models = Path.Combine(path, "models.json");
            using (var s = File.OpenRead(File.Exists(models) ? models : Path.Combine(path, "meshes.json")))
                gz.ParseMeshes(s);
            // Unified-shape materials index into textures.json, so it must be read first.
            var textures = Path.Combine(path, "textures.json");
            if (File.Exists(textures))
                using (var s = File.OpenRead(textures)) gz.ParseTextures(s);
            using var mats = File.OpenRead(Path.Combine(path, "materials.json"));
            gz.ParseMaterials(mats);
        }
        else
        {
            using var zip = ZipFile.OpenRead(path);
            using (var s = OpenEntry(zip, "nodes.json")) gz.ParseNodes(s);
            using (var s = OpenEntry(zip, "models.json", "meshes.json")) gz.ParseMeshes(s);
            var tex = zip.GetEntry("textures.json");
            if (tex != null)
                using (var s = tex.Open()) gz.ParseTextures(s);
            using (var s = OpenEntry(zip, "materials.json")) gz.ParseMaterials(s);
        }
        return gz;
    }

    /// <summary>Opens the first of <paramref name="names"/> the archive actually has —
    /// how the models.json / meshes.json rename is absorbed.</summary>
    private static Stream OpenEntry(ZipArchive zip, params string[] names)
    {
        foreach (var name in names)
            if (zip.GetEntry(name) is { } entry)
                return entry.Open();
        throw new FileNotFoundException($"none of [{string.Join(", ", names)}] present in archive");
    }

    public GameZNode? FindByName(string name)
    {
        foreach (var n in Nodes)
            if (string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase))
                return n;
        return null;
    }

    /// <summary>The world-space transform of a node, accumulated up its parent chain (the
    /// nodes.json children lists — parent is a flat list position, so we invert those).
    /// Nodes without a transformation contribute identity. Robust to nesting, though most
    /// world markers (dz points, route ribbons) sit directly under the identity World root,
    /// so their stored translation is already world-space. Used to resolve objective-marker
    /// positions without building the (skipped) marker nodes into the scene.</summary>
    public Transform3D WorldTransformOf(GameZNode node)
    {
        EnsureParentMap();
        var xf = Transform3D.Identity;
        for (GameZNode? n = node; n != null;)
        {
            xf = (n.Local ?? Transform3D.Identity) * xf;
            int p = _parent![n.Index];
            n = p >= 0 ? Nodes[p] : null;
        }
        return xf;
    }

    private void EnsureParentMap()
    {
        if (_parent != null)
            return;
        var parent = new int[Nodes.Count];
        Array.Fill(parent, -1);
        foreach (var n in Nodes)
            foreach (var c in n.Children)
                if (c >= 0 && c < parent.Length)
                    parent[c] = n.Index;
        _parent = parent;
    }

    private void ParseNodes(Stream stream)
    {
        using var doc = JsonDocument.Parse(BufferAll(stream));
        int index = 0;
        foreach (var wrapper in doc.RootElement.EnumerateArray())
        {
            // Legacy: the node IS an enum wrapper, {"Object3d": {...}} / {"Lod": {...}},
            // with name/mesh_index/children inside the variant body.
            // Unified: a flat node carrying name/model_index/child_indices itself, with
            // only the variant-specific fields under "data".
            bool unified = wrapper.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object;
            var prop = FirstProperty(unified ? data : wrapper);
            var body = prop.Value;      // the variant-specific fields (area, range, transform…)
            var header = unified ? wrapper : body; // where name / mesh index / children live
            // Not every kind has every field: Display lacks name+children (legacy only —
            // the unified shape names it "display"), Window/Camera/Light lack children.
            var node = new GameZNode
            {
                Kind = prop.Name,
                Name = header.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "",
                MeshIndex = (header.TryGetProperty("model_index", out var mi)
                    || header.TryGetProperty("mesh_index", out mi)) ? mi.GetInt32() : -1,
                Index = index++,
            };
            // Both spellings are flat list positions, NOT the node's own "index" field
            // (which the unified shape also exposes, 1-based and with duplicates — the
            // legacy "node_index" by another name). Verified on C1: reading them as flat
            // positions is parent/child-consistent 6553 times, as index values 59.
            if ((header.TryGetProperty("child_indices", out var kids)
                 || header.TryGetProperty("children", out kids)) && kids.ValueKind == JsonValueKind.Array)
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
                        {
                            // Legacy: "nodes": [{index, …}]. Unified: "values":
                            // [{node_index, …}] (its sibling "node_indices" is empty in
                            // every chapter, but is read too in case that ever changes).
                            if (cell.TryGetProperty("nodes", out var refs) || cell.TryGetProperty("values", out refs))
                                foreach (var nref in refs.EnumerateArray())
                                {
                                    if (!nref.TryGetProperty("index", out var iv))
                                        iv = nref.GetProperty("node_index");
                                    if (seen.Add(iv.GetInt32()))
                                        node.PartitionNodes.Add(iv.GetInt32());
                                }
                            if (cell.TryGetProperty("node_indices", out var plain) && plain.ValueKind == JsonValueKind.Array)
                                foreach (var nref in plain.EnumerateArray())
                                    if (seen.Add(nref.GetInt32()))
                                        node.PartitionNodes.Add(nref.GetInt32());
                        }
                    }
                }
            }
            // Legacy "transformation" is an object or null; unified "transform" is either
            // the string "Initial" (exactly where legacy wrote null — 3675/3675 on C1) or
            // a {"RotateTranslateScale": {…}} wrapper.
            if (body.TryGetProperty("transformation", out var tf) && tf.ValueKind == JsonValueKind.Object)
                node.Local = ParseTransform(tf);
            else if (body.TryGetProperty("transform", out tf) && tf.ValueKind == JsonValueKind.Object)
                node.Local = ParseTransform(FirstProperty(tf).Value);
            Nodes.Add(node);
        }
    }

    private static Transform3D ParseTransform(JsonElement tf)
    {
        // translation/rotation/matrix (legacy) vs translate/rotate/original (unified).
        // Scale exists only in the unified shape and is unit on every node of every
        // chapter and of planes.zbd (0 of 4181 non-unit), so it is deliberately ignored.
        var tr = ParseVec3(tf.TryGetProperty("translation", out var t) ? t : tf.GetProperty("translate"));
        Basis basis;
        if ((tf.TryGetProperty("matrix", out var m) || tf.TryGetProperty("original", out m))
            && m.ValueKind == JsonValueKind.Object)
        {
            // Stored transposed: the actual rotation matrix has columns (a,b,c), (d,e,f), (g,h,i).
            // The unified shape spells those r00..r02, r10..r12, r20..r22 (and appends the
            // translation as r30..r32, which duplicates "translate" and is not read).
            float M(string legacy, string unified) =>
                (m.TryGetProperty(legacy, out var v) ? v : m.GetProperty(unified)).GetSingle();
            basis = new Basis(
                new Vector3(M("a", "r00"), M("b", "r01"), M("c", "r02")),
                new Vector3(M("d", "r10"), M("e", "r11"), M("f", "r12")),
                new Vector3(M("g", "r20"), M("h", "r21"), M("i", "r22")));
        }
        else
        {
            // Euler angles compose as R = Ry(y)·Rx(x)·Rz(z) — Godot's YXZ order
            // (verified numerically against the 221 nodes that carry both forms).
            basis = Basis.FromEuler(ParseVec3(tf.TryGetProperty("rotation", out var r) ? r : tf.GetProperty("rotate")),
                EulerOrder.Yxz);
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
                // v0.6.1 "triangle_strip"; the fork spells it "tri_strip".
                poly.TriangleStrip = (pf.TryGetProperty("triangle_strip", out var ts)
                    || pf.TryGetProperty("tri_strip", out ts)) && ts.ValueKind == JsonValueKind.True;
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
                    // v0.6.1 "extra"; the fork spells the same array "vertices".
                    if ((!l.TryGetProperty("extra", out var extra) && !l.TryGetProperty("vertices", out extra))
                        || extra.ValueKind != JsonValueKind.Array || extra.GetArrayLength() == 0)
                        continue;
                    var c = l.GetProperty("color");
                    // Per-light params (field meanings inferred from the C1 value survey,
                    // 2026-07-18): unk08 = size scale (0 default / 1 / 2 / 5 — the
                    // lighthouse), unk64 = max sprite size in px (30 everywhere it's set),
                    // unk52/unk68 = visibility range in m (1500 / 2500 / 4000).
                    float F(string name) =>
                        l.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
                            ? v.GetSingle() : 0f;
                    float range = F("unk68");
                    if (range <= 0f)
                        range = F("unk52");
                    mesh.Lights.Add(new GameZLight
                    {
                        Position = ParseVec3(extra[0]),
                        Color = new Color(
                            c.GetProperty("r").GetSingle() / 255f,
                            c.GetProperty("g").GetSingle() / 255f,
                            c.GetProperty("b").GetSingle() / 255f),
                        SizeScale = F("unk08"),
                        MaxSizePx = F("unk64"),
                        Range = range,
                    });
                }
            }
            Meshes.Add(mesh);
        }
    }

    /// <summary>textures.json — the unified shape's material texture table. Entries are
    /// <c>{name}</c>; v0.6.1 wrote <c>{original, renamed}</c>, where "renamed" carried a
    /// <c>name.-N</c> disambiguation for duplicate table entries. The fork drops that
    /// machinery entirely (materials reference textures by index, so duplicates need no
    /// unique name), which is why <see cref="TextureArchive"/>'s <c>.-N</c> fallback is
    /// legacy-only. Both spellings are read so either tree loads.</summary>
    private void ParseTextures(Stream stream)
    {
        using var doc = JsonDocument.Parse(BufferAll(stream));
        foreach (var t in doc.RootElement.EnumerateArray())
            _textureNames.Add(
                (t.TryGetProperty("name", out var n) ? n : t.GetProperty("original")).GetString() ?? "");
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
                // Legacy inlined the name; the fork stores an index into textures.json.
                if (body.TryGetProperty("texture", out var tn))
                    mat.TextureName = tn.GetString();
                else if (body.TryGetProperty("texture_index", out var ti))
                {
                    int i = ti.GetInt32();
                    mat.TextureName = i >= 0 && i < _textureNames.Count ? _textureNames[i] : null;
                }
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
    public float SizeScale;  // source unk08: 0 (default) / 1 / 2 / 5 — relative sprite size
    public float MaxSizePx;  // source unk64: max sprite size in pixels (30 where set)
    public float Range;      // source unk68 (else unk52): visibility range in metres, 0 = unset
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
