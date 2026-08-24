using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Godot;

namespace CSVM.Mech3;

/// <summary>One point-sprite light baked into a mesh (stars, nav beacons); see
/// <see cref="GameZMesh.Lights"/>.</summary>
public struct GameZLight
{
    public Vector3 Position;
    public Color Color;
    public float SizeScale;  // source unk08: 0 (default) / 1 / 2 / 5 — relative sprite size
    public float MaxSizePx;  // source unk64: max sprite size in pixels (30 where set)
    public float Range;      // source unk68 (else unk52): visibility range in metres, 0 = unset
}

/// <summary>
/// In-memory model of a mech3ax GameZ extraction: nodes/models/materials/textures JSON into
/// plain C# objects. Reads both the v0.6.1 "legacy" and the fork "unified" shapes.
/// Field mapping and reader rules: docs/formats/gamez.md. Plumbing: this module's entry in
/// docs/architecture.md.
/// ⚠ <see cref="GameZNode.Index"/> is the flat list position, never the unified JSON's own
/// 1-based, duplicated <c>index</c>; child_indices are flat positions too.
/// ⚠ The unified transform's <c>scale</c> is deliberately ignored; it is unit on every node
/// measured.
/// </summary>
public sealed class GameZ
{
    // The original's surface-type registry, restricted to the labels mech3ax's extraction has
    // ever emitted over the shipped install (analysis/surface-classification/FINDINGS.md,
    // 2026-08-11): the `soil` field is a bulk-read raw dword from the material record, and
    // mech3ax's own label for it is just its Soil enum variant name, not a Crimson Skies name.
    // An unseen label means the extractor's enum changed underneath us, not that the id is 0 —
    // fail loudly rather than silently misclassifying every material with the new label.
    private static readonly Dictionary<string, int> SoilLabelToId = new()
    {
        ["Default"] = 0,
        ["Water"] = 1,
        ["Fire"] = 5,
        ["Grass"] = 8,
        ["Mech"] = 11,
        ["Silt"] = 12,
        ["NoSlip"] = 13,
    };

    // textures.json order. Only the unified shape needs it: its materials reference
    // textures by index, where the legacy shape inlined the name. Empty when legacy.
    private readonly List<string> _textureNames = new();

    private int[]? _parent; // flat index → parent flat index (−1 for roots), built lazily

    // Every node index WorldBuilder's own walk reaches — someone's child_indices, or a World
    // node's spatial-partition reference (the same two places WorldBuilder.cs's class doc says
    // "world content lives"). Built lazily; see IsLibraryRoot.
    private HashSet<int>? _placed;

    private bool[]? _markerGizmo; // mesh index → single flat-coloured triangle, built lazily

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

    /// <summary>The mesh is a level-editor gizmo, not scenery: one flat-coloured triangle with no
    /// texture. The original never draws these — they are the authoring marks for AI/mission
    /// anchors (approach cones, landing spheres, puffer and sound emitters, look-at targets,
    /// flak/scatter trail origins). 142 nodes install-wide carry one, and every name in that set
    /// is a marker; nothing legible as scenery is a lone untextured triangle.
    /// The owning node still gets built — animations attach puffers and sounds to it by name.</summary>
    public bool IsMarkerGizmo(int meshIndex)
    {
        if (meshIndex < 0 || meshIndex >= Meshes.Count)
            return false;
        _markerGizmo ??= new bool[Meshes.Count];
        // Cheap enough to recompute on a miss; the array only memoizes the true answers.
        if (_markerGizmo[meshIndex])
            return true;
        var mesh = Meshes[meshIndex];
        if (mesh == null || mesh.Vertices.Count != 3 || mesh.Polygons.Count != 1)
            return false;
        var poly = mesh.Polygons[0];
        if (poly.VertexIndices.Count != 3)
            return false;
        if (poly.MaterialIndex < 0 || poly.MaterialIndex >= Materials.Count
            || Materials[poly.MaterialIndex].TextureName != null)
            return false;
        return _markerGizmo[meshIndex] = true;
    }

    /// <summary>An untextured polygon whose vertex colours all restate its own material colour:
    /// one authored value in two slots, not two terms to multiply. Multiplying squares the colour
    /// (176 → 120). 87 polygons install-wide, mostly skydome skirts. Decode: docs/formats/gamez.md
    /// and this module's docs/architecture.md entry.</summary>
    public bool VertexColorsRestateMaterialColor(GameZPolygon poly, int materialIndex)
    {
        if (materialIndex < 0 || materialIndex >= Materials.Count)
            return false;
        var mat = Materials[materialIndex];
        if (mat.TextureName != null || poly.VertexColors == null || poly.VertexColors.Count == 0)
            return false;
        // Both sides come from the same /255f decode, so this is an equality test with room for
        // float noise only — half a source byte.
        const float eps = 0.5f / 255f;
        foreach (var c in poly.VertexColors)
        {
            if (Mathf.Abs(c.R - mat.Color.R) > eps
                || Mathf.Abs(c.G - mat.Color.G) > eps
                || Mathf.Abs(c.B - mat.Color.B) > eps)
                return false;
        }
        return true;
    }

    public GameZNode? FindByName(string name)
    {
        foreach (var n in Nodes)
            if (string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase))
                return n;
        return null;
    }

    /// <summary>Is <paramref name="node"/> staged template-library content: built with the game
    /// but never placed, left inert until an animation calls it by name? Decode: docs/formats/
    /// gamez.md.
    /// ⚠ Tests placement (the load-bearing signal) plus <c>Kind == "Object3d"</c> and
    /// <c>Active</c>. <c>zone_id</c>/<c>signs</c> only corroborate; do not test them alone.</summary>
    public bool IsLibraryRoot(GameZNode node)
    {
        EnsurePlaced();
        return node.Kind == "Object3d" && node.Active && !_placed!.Contains(node.Index);
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

    // Opens the first of `names` the archive actually has —
    // how the models.json / meshes.json rename is absorbed.
    private static Stream OpenEntry(ZipArchive zip, params string[] names)
    {
        foreach (var name in names)
            if (zip.GetEntry(name) is { } entry)
                return entry.Open();
        throw new FileNotFoundException($"none of [{string.Join(", ", names)}] present in archive");
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

    // The partition grid's geometry, off the FIRST cell's own bounds rather than off Area: column
    // 0 starts at its low x edge, row 0 at its HIGH z edge, because the second axis is authored
    // with a negative cell size. Every later cell restates the same size. A legacy extraction
    // carries no bounds, which leaves the grid unusable and WorldPartitionGrid null.
    private static void ReadCellBounds(GameZNode node, JsonElement cell)
    {
        if (node.PartitionCellX != 0f
            || !cell.TryGetProperty("min", out var min) || min.ValueKind != JsonValueKind.Object
            || !cell.TryGetProperty("max", out var max) || max.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        float x0 = min.GetProperty("x").GetSingle(), x1 = max.GetProperty("x").GetSingle();
        float z0 = min.GetProperty("z").GetSingle(), z1 = max.GetProperty("z").GetSingle();
        node.PartitionOriginX = x0;
        node.PartitionOriginZ = z1;
        node.PartitionCellX = Mathf.Abs(x1 - x0);
        node.PartitionCellZ = Mathf.Abs(z1 - z0);
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

    private void EnsurePlaced()
    {
        if (_placed != null)
            return;
        var placed = new HashSet<int>();
        foreach (var n in Nodes)
        {
            foreach (var c in n.Children)
                placed.Add(c);
            if (n.Kind == "World" && n.PartitionNodes != null)
                foreach (var p in n.PartitionNodes)
                    placed.Add(p);
        }
        _placed = placed;
    }

    private void ParseNodes(Stream stream)
    {
        using var doc = JsonDocument.Parse(BufferAll(stream));
        int index = 0;
        foreach (var wrapper in doc.RootElement.EnumerateArray())
        {
            // Legacy wraps the variant as {"Object3d": {...}}; unified is a flat node with
            // name/model_index/child_indices, and the variant-only fields under "data".
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
            // Both shapes carry flags on the header level (unified: the flat node; legacy: the
            // variant body, which IS the header there). Absent → stay collidable.
            if (header.TryGetProperty("flags", out var fl) && fl.ValueKind == JsonValueKind.Object)
            {
                if (fl.TryGetProperty("intersect_surface", out var isf))
                    node.IntersectSurface = isf.ValueKind == JsonValueKind.True;
                if (fl.TryGetProperty("active", out var ac))
                    node.Active = ac.ValueKind == JsonValueKind.True;
            }
            // zone_id: -1 (absent too) means always draw. See ZoneGate and docs/formats/gamez.md.
            if (header.TryGetProperty("zone_id", out var zn) && zn.ValueKind == JsonValueKind.Number)
                node.ZoneId = zn.GetInt32();
            // ⚠ Flat list positions, never the node's own "index" field (1-based, duplicated).
            // See docs/formats/gamez.md and GameZNode.Index's warning.
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
                    node.PartitionCellNodes = new List<List<int>>();
                    node.PartitionRows = parts.GetArrayLength();
                    var seen = new HashSet<int>();
                    foreach (var row in parts.EnumerateArray())
                    {
                        node.PartitionCols = row.GetArrayLength();
                        foreach (var cell in row.EnumerateArray())
                        {
                            var members = new List<int>();
                            node.PartitionCellNodes.Add(members);
                            ReadCellBounds(node, cell);
                            // Legacy: "nodes": [{index, …}]. Unified: "values":
                            // [{node_index, …}] (its sibling "node_indices" is empty in
                            // every chapter, but is read too in case that ever changes).
                            if (cell.TryGetProperty("nodes", out var refs) || cell.TryGetProperty("values", out refs))
                                foreach (var nref in refs.EnumerateArray())
                                {
                                    if (!nref.TryGetProperty("index", out var iv))
                                        iv = nref.GetProperty("node_index");
                                    members.Add(iv.GetInt32());
                                    if (seen.Add(iv.GetInt32()))
                                        node.PartitionNodes.Add(iv.GetInt32());
                                }
                            if (cell.TryGetProperty("node_indices", out var plain) && plain.ValueKind == JsonValueKind.Array)
                                foreach (var nref in plain.EnumerateArray())
                                {
                                    members.Add(nref.GetInt32());
                                    if (seen.Add(nref.GetInt32()))
                                        node.PartitionNodes.Add(nref.GetInt32());
                                }
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
                // unk3 = no_clutter (docs/formats/gamez.md). Absent means false
                // (skip_serializing_if), so TryGetProperty's false default is required.
                poly.NoClutter = pf.TryGetProperty("unk3", out var sf) && sf.ValueKind == JsonValueKind.True;
                // Draw-priority layer. mech3ax v0.6.1 emits it as "unk04"; upstream has
                // since identified and renamed it to "priority" — accept both spellings.
                if (p.TryGetProperty("unk04", out var pr) || p.TryGetProperty("priority", out pr))
                    poly.Priority = pr.GetInt32();
                // Unified-only; null on legacy. At most one value per polygon (docs/formats/
                // world-structure.md census), so only the first element is kept.
                if (p.TryGetProperty("zone_set", out var zsArr) && zsArr.ValueKind == JsonValueKind.Array
                    && zsArr.GetArrayLength() > 0)
                    poly.ZoneSet = zsArr[0].GetInt32();
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
                // `materials` is a list: element 0 is the base skin, later entries are overlay
                // passes with their own UVs (docs/formats/gamez.md). 619 polygons carry a second.
                if (p.TryGetProperty("materials", out var pms))
                {
                    for (int pass = 0; pass < pms.GetArrayLength(); pass++)
                    {
                        var pm = pms[pass];
                        int material = pm.GetProperty("material_index").GetInt32();
                        List<Vector2>? uv = null;
                        if (pm.TryGetProperty("uv_coords", out var uvs) && uvs.ValueKind == JsonValueKind.Array)
                        {
                            uv = new List<Vector2>();
                            foreach (var c in uvs.EnumerateArray())
                                uv.Add(new Vector2(c.GetProperty("u").GetSingle(), c.GetProperty("v").GetSingle()));
                        }
                        if (pass == 0)
                        {
                            poly.MaterialIndex = material;
                            poly.UvCoords = uv;
                        }
                        else
                        {
                            (poly.OverlayPasses ??= new List<GameZPolygonPass>())
                                .Add(new GameZPolygonPass { MaterialIndex = material, UvCoords = uv });
                        }
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
                    // Field meanings per the C1 value survey; see GameZLight's field comments.
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
            // ⚠ ModelType, not FacadeMode alone, decides Facade vs Default (docs/formats/
            // gamez.md); a Default model can carry a stale FacadeMode and must not billboard.
            if (m.TryGetProperty("model_type", out var mt) && mt.ValueKind == JsonValueKind.String)
                mesh.ModelType = mt.GetString();
            if (m.TryGetProperty("facade_mode", out var fm) && fm.ValueKind == JsonValueKind.String)
                mesh.FacadeMode = fm.GetString();
            // The model's render flags. Both are serialized true on the overwhelming majority
            // of models and false on authored self-lit/unfogged geometry; absent on a legacy
            // tree, where the defaults keep every model lit and fogged.
            if (m.TryGetProperty("flags", out var mfl) && mfl.ValueKind == JsonValueKind.Object)
            {
                if (mfl.TryGetProperty("lighting", out var lit))
                    mesh.Lighting = lit.ValueKind != JsonValueKind.False;
                if (mfl.TryGetProperty("fog", out var fog))
                    mesh.Fog = fog.ValueKind != JsonValueKind.False;
            }
            if (m.TryGetProperty("texture_scroll", out var sc) && sc.ValueKind == JsonValueKind.Object)
                mesh.TextureScroll = new Vector2(
                    sc.TryGetProperty("u", out var su) ? su.GetSingle() : 0f,
                    sc.TryGetProperty("v", out var sv) ? sv.GetSingle() : 0f);
            Meshes.Add(mesh);
        }
    }

    // textures.json — the unified shape's material texture table. Entries are
    // `{name}`; v0.6.1 wrote `{original, renamed}`, where "renamed" carried a
    // `name.-N` disambiguation for duplicate table entries. The fork drops that
    // machinery entirely (materials reference textures by index, so duplicates need no
    // unique name), which is why TextureArchive's `.-N` fallback is
    // legacy-only. Both spellings are read so either tree loads.
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
            string soilLabel = body.GetProperty("soil").GetString()
                ?? throw new FormatException("Material 'soil' field is present but null.");
            mat.SoilId = SoilLabelToId.TryGetValue(soilLabel, out var soilId)
                ? soilId
                : throw new FormatException(
                    $"Unknown material soil label '{soilLabel}' — the extractor's surface-id " +
                    "registry may have changed; GameZ.SoilLabelToId needs a new entry.");
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
                // A material's own texture flipbook (docs/formats/effects.md); rare per chapter
                // but covers large surfaces like C1B's animated sea.
                if (body.TryGetProperty("cycle", out var cyc) && cyc.ValueKind == JsonValueKind.Object)
                {
                    if (cyc.TryGetProperty("texture_indices", out var idx))
                        foreach (var e in idx.EnumerateArray())
                        {
                            int i = e.GetInt32();
                            if (i >= 0 && i < _textureNames.Count)
                                mat.CycleTextures.Add(_textureNames[i]);
                        }
                    mat.CycleSpeed = cyc.TryGetProperty("speed", out var sp) ? sp.GetSingle() : 0f;
                    mat.CycleLooping = !cyc.TryGetProperty("looping", out var lp) || lp.GetBoolean();
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
}

public sealed class GameZNode
{
    public string Kind = "";   // "Object3d", "Lod", "World", "Display", "Window", "Camera", "Light"
    public string Name = "";
    public int MeshIndex = -1;
    // The original's per-node collision-participation flag (flags.intersect_surface): false on
    // geometry the engine never intersection-tests — spinning props, wreck/debris pieces, fire/
    // flake/ripple/splash effects, light glows, ropes, shadows, the C3 spiderweb. Absent flags
    // (legacy extraction) default to collidable.
    public bool IntersectSurface = true;
    // The build script's own NodeSetActive record (flags.active): false on a node the original
    // never builds visible. Absent flags (legacy extraction) default to active, matching every
    // other flags.* field here.
    public bool Active = true;
    /// <summary>The original's per-node visibility zone (<c>zone_id</c>): <b>-1</b> = always drawn;
    /// otherwise the node draws only while that id is in the camera's armed zone set, which
    /// the engine arms as <c>{0, camera weather state}</c> — so <b>0</b> is also always,
    /// and 1/2/3 are the per-state buckets (docs/formats/gamez.md, docs/formats/weather.md's deck
    /// census). Absent in a legacy extraction, which defaults to -1 = ungated.</summary>
    public int ZoneId = -1;
    // Flat position in nodes.json. The file is a depth-first serialization of the tree,
    // so this is the original engine's draw order — the cross-node tie-break for
    // coplanar surfaces of equal polygon priority (later node draws on top).
    public int Index;
    public Transform3D? Local;
    public float LodRangeMin = -1f; // Lod nodes only; 0 = nearest/highest detail
    public List<int>? PartitionNodes; // World nodes only: distinct subtree roots placed via the spatial grid
    // World nodes only: per-cell membership, row-major over PartitionRows x PartitionCols in the
    // file's own order, which is the engine's own cell indexing. Kept beside the flat
    // PartitionNodes because an area query needs the cell a node sits in, which the flat list
    // discards. See WorldPartitionGrid and docs/formats/interp.md.
    public List<List<int>>? PartitionCellNodes;
    // World nodes only: the cell grid's geometry, read off the cells' own bounds rather than
    // derived from Area — the two axes run opposite ways (docs/formats/world-structure.md).
    // OriginX is column 0's low x edge; OriginZ is row 0's HIGH z edge.
    public float PartitionOriginX, PartitionOriginZ, PartitionCellX, PartitionCellZ;
    // World nodes only: the map's ground-plane bounds (area = {left=xMin, right=xMax,
    // top=zMin, bottom=zMax}, Godot world coords) and its partition grid size. WorldBuilder
    // uses these to mirror the outermost border terrain outward past the map edge.
    public bool HasArea;
    public float AreaLeft, AreaTop, AreaRight, AreaBottom;
    public int PartitionCols, PartitionRows;

    public List<int> Children { get; } = new(); // indices into GameZ.Nodes (list positions, not node_index)
}

public sealed class GameZMesh
{
    // Null on a legacy (v0.6.1) extraction, which doesn't carry these fields — see the
    // ParseMeshes remark. "Facade" is the original's own billboard-sprite classification;
    // FacadeMode names the rotation axis (SceneBuilder.GetCylindricalAxis/IsGlowSpriteMesh).
    public string? ModelType;
    public string? FacadeMode;
    // UV units/second (rare: 5 models in this install — a hangar glass-roof sky reflection,
    // an oil-dock texture, and three boat wake fronts; the waterfall's own falls/falls_edge
    // textures do NOT scroll — their motion in the original is the splash puffers, not a
    // UV animation).
    public Vector2 TextureScroll;
    // The model's own render flags. `lighting: false` = the original's D3D lighting is off for
    // this model, so it draws at full texture × vertex-colour brightness instead of being
    // modulated by the mission SUNLIGHT (the remake's csky_world_light) — self-lit effect
    // geometry, billboards, glows, clutter cards. `fog: false` = exempt from distance fog.
    // Absent (legacy v0.6.1 tree) → both true (drawn lit and fogged).
    public bool Lighting = true;
    public bool Fog = true;

    public List<Vector3> Vertices { get; } = new();
    public List<Vector3> Normals { get; } = new();
    public List<GameZPolygon> Polygons { get; } = new();
    public List<GameZLight> Lights { get; } = new(); // point-sprite lights (stars, nav beacons)
}

public sealed class GameZPolygon
{
    public List<int>? NormalIndices;
    public List<Vector2>? UvCoords;

    /// <summary>The polygon's overlay passes — <c>materials[1..]</c>, each a second textured
    /// material drawn on these same triangles with its own UVs, on top of the base skin
    /// (<see cref="MaterialIndex"/>/<see cref="UvCoords"/>, which are <c>materials[0]</c>). Null
    /// for the overwhelming majority. Every overlay texture in this install carries an alpha
    /// channel, so the pass composites rather than replacing what is under it; SceneBuilder
    /// builds each as its own surface with its own draw-order bias.</summary>
    public List<GameZPolygonPass>? OverlayPasses;
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
    // no_clutter ("unk3"): node-name-authored marker gating ClutterBuilder's scatter, not the
    // OpenFlight SUBFACE bit it resembles. Decode: docs/formats/gamez.md.
    // ⚠ It selects which of two coplanar layers draws on top; it does not mean bare ground.
    public bool NoClutter;
    // Per-polygon weather-zone membership (unified-shape "zone_set").
    // Null when the field is absent (legacy tree) or the array is empty; every
    // polygon in this install carries at most one value where present, matching the
    // node-level zone_id's -1/1/2/3 numbering (see docs/formats/world-structure.md's
    // census). Nothing reads this yet — which zone is active is not in any data file
    // (see zone_id), so this stays parse-only per the plan's ground rules.
    public int? ZoneSet;

    public List<int> VertexIndices { get; } = new();
}

/// <summary>One overlay pass of a polygon (an entry of <c>materials</c> past the first): the
/// material to skin the polygon's triangles with, and the UVs to skin them by. The UVs are
/// independent of the base pass's — a fog gradient runs its own 8×64 ramp across a face whose
/// base skin tiles a wall texture — which is why the pass cannot be folded into the base
/// surface and gets its own.</summary>
public sealed class GameZPolygonPass
{
    public int MaterialIndex = -1;
    public List<Vector2>? UvCoords;
}

public sealed class GameZMaterial
{
    /// <summary>The material's own texture flipbook (the gamez `cycle` block), frame names in
    /// order — empty for the overwhelming majority. Frame 0 repeats <see cref="TextureName"/>.
    /// Driven by <see cref="TextureCycler"/>; the original's animated water/surf/wake/splash
    /// and the walking-crowd sprites are all this one mechanism.</summary>
    public readonly List<string> CycleTextures = new();

    public string? TextureName; // set for Textured materials (e.g. "bldhwk_cowling.tif", may be truncated to 20 chars)
    public Color Color = Colors.White; // set for Colored materials

    /// <summary>The original's numeric surface type id (<c>materials.json</c> <c>soil</c>,
    /// mapped through <see cref="GameZ.SoilLabelToId"/>): what <c>crimson.exe</c> indexes into
    /// its <c>player_crash_&lt;name&gt;</c>/<c>touchdown_&lt;name&gt;</c> vectors on impact
    /// (analysis/surface-classification/FINDINGS.md, 2026-08-11). Default 0 matches the
    /// engine's own fallback id and mech3ax's default Soil variant.</summary>
    public int SoilId;

    /// <summary>Flipbook rate in frames per second (gamez `speed`: 4–12 across this install).</summary>
    public float CycleSpeed;

    /// <summary>Whether the flipbook repeats (true for every cycle in this install).</summary>
    public bool CycleLooping = true;
}
