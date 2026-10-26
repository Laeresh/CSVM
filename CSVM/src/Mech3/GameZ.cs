using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Godot;

namespace CSVM.Mech3;

/// <summary>One point-sprite light baked into a mesh (stars, nav beacons); see
/// <see cref="GameZMesh.Lights"/>. Every field is decoded off the original's own reader and draw
/// in docs/formats/world-structure.md; the record's remaining words are read by nothing there and
/// so are absent here.</summary>
public struct GameZLight
{
    public Vector3 Position;
    public Color Color;

    /// <summary>Seconds lit, then as many dark, from source <c>unk08</c> under its <c>unk04</c>
    /// gate. 0 is a steady light, which is what 659 of the install's 715 lights are.</summary>
    public float BlinkPeriod;

    /// <summary>Source <c>unk52</c>: the distance the light is fully gone at. 0 means the data
    /// authors no fade at all, not a zero-length one.</summary>
    public float FadeFar;

    /// <summary>Source <c>unk56</c> rescaled to 0..1: alpha per metre nearer than
    /// <see cref="FadeFar"/>. Authored as the reciprocal of the fade band, so the light is opaque
    /// from the camera out to the band's near edge without that edge being stored twice.</summary>
    public float FadeSlope;
}

/// <summary>
/// In-memory model of a GameZ archive, built from the game files by <see cref="GameZZbd"/> or, by
/// <see cref="Load"/>, from unzbd's nodes/models/materials/textures JSON in the fork's shape.
/// Field mapping and reader rules: docs/formats/gamez.md. Plumbing: this module's entry in
/// docs/architecture.md.
/// ⚠ <see cref="GameZNode.Index"/> is the flat list position, never the JSON's own 1-based,
/// duplicated <c>index</c>; child_indices are flat positions too.
/// ⚠ The transform's <c>scale</c> is deliberately ignored; it is unit on every node measured.
/// </summary>
public sealed class GameZ
{
    // The original's surface-type registry, restricted to the labels mech3ax's extraction has
    // ever emitted over the shipped install (analysis/surface-classification/FINDINGS.md,
    // 2026-08-11): the `soil` field is a bulk-read raw dword from the material record, and
    // mech3ax's own label for it is just its Soil enum variant name, not a Crimson Skies name.
    // An unseen label means the extractor's enum changed underneath us, not that the id is 0,
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

    // textures.json order; materials reference textures by index into it.
    private readonly List<string> _textureNames = new();

    private int[]? _parent; // flat index → parent flat index (−1 for roots), built lazily

    // Every node index WorldBuilder's own walk reaches, someone's child_indices, or a World
    // node's spatial-partition reference (the same two places WorldBuilder.cs's class doc says
    // "world content lives"). Built lazily; see IsLibraryRoot.
    private HashSet<int>? _placed;

    private bool[]? _markerGizmo; // mesh index → single flat-coloured triangle, built lazily

    public List<GameZNode> Nodes { get; } = new();
    public List<GameZMesh> Meshes { get; } = new();
    public List<GameZMaterial> Materials { get; } = new();

    /// <summary>The material texture table, filled by <see cref="GameZZbd"/>.</summary>
    internal List<string> TextureNames => _textureNames;

    /// <summary>The mission's own number inside its chapter, which is what indexes a node's
    /// ownership slots: the digits ending the mission folder name (<c>m05</c>, <c>mp3</c>,
    /// <c>ia1</c>), 1-based as the original's own table is. Falls back to the first mission for a
    /// build that names no mission, which is where the shipped data authors the same owner in
    /// every slot anyway.</summary>
    public static int MissionSlotOf(string? missionFolder)
    {
        int digits = 0, seen = 0;
        for (int i = (missionFolder ?? string.Empty).Length - 1; i >= 0 && seen < 2; i--)
        {
            char c = missionFolder![i];
            if (c < '0' || c > '9')
            {
                break;
            }

            digits += (c - '0') * (seen == 0 ? 1 : 10);
            seen++;
        }

        return digits >= 1 && digits <= GameZNode.MissionSlots ? digits : 1;
    }

    /// <summary>Loads unzbd's output, the fork's shape: a GameZ ZIP, or a directory of the same JSON
    /// files. The modding path; a session reads the game files through <see cref="GameZZbd"/>.</summary>
    public static GameZ Load(string path)
    {
        var gz = new GameZ();
        using var zip = Directory.Exists(path) ? null : ZipFile.OpenRead(path);
        Stream Open(string name) => zip == null
            ? File.OpenRead(Path.Combine(path, name))
            : (zip.GetEntry(name) ?? throw new FileNotFoundException($"{name} not present in {path}")).Open();
        using (var s = Open("nodes.json")) gz.ParseNodes(s);
        using (var s = Open("models.json")) gz.ParseMeshes(s);
        // Materials index into textures.json, so it must be read first.
        using (var s = Open("textures.json")) gz.ParseTextures(s);
        using (var s = Open("materials.json")) gz.ParseMaterials(s);
        return gz;
    }

    /// <summary>The mesh is a level-editor gizmo, not scenery: one flat-coloured triangle with no
    /// texture. The original never draws these, they are the authoring marks for AI/mission
    /// anchors (approach cones, landing spheres, puffer and sound emitters, look-at targets,
    /// flak/scatter trail origins). 142 nodes install-wide carry one, and every name in that set
    /// is a marker; nothing legible as scenery is a lone untextured triangle.
    /// The owning node still gets built, animations attach puffers and sounds to it by name.</summary>
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
    /// (176 → 120). 87 polygons install-wide, mostly skydome skirts. Decode: docs/formats/gamez.md.</summary>
    public bool VertexColorsRestateMaterialColor(GameZPolygon poly, int materialIndex)
    {
        if (materialIndex < 0 || materialIndex >= Materials.Count)
            return false;
        var mat = Materials[materialIndex];
        if (mat.TextureName != null || poly.VertexColors == null || poly.VertexColors.Count == 0)
            return false;
        // Both sides come from the same /255f decode, so this is an equality test with room for
        // float noise only, half a source byte.
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

    /// <summary>Is <paramref name="node"/> a staged PROP: unplaced <c>Object3d</c> content carrying
    /// geometry that ships switched OFF, which a definition hangs onto placed world content and
    /// switches on? <see cref="IsLibraryRoot"/> refuses these on its <c>Active</c> test, and they
    /// are addressed by name rather than instanced per call. Decode: docs/formats/mission-entities/
    /// enemy-generators.md, "The launch hook carries a staged aircraft".</summary>
    public bool IsStagedProp(GameZNode node)
    {
        EnsurePlaced();
        return node.Kind == "Object3d" && !node.Active && !_placed!.Contains(node.Index)
               && (node.Children.Count > 0 || node.MeshIndex >= 0);
    }

    /// <summary>Is <paramref name="node"/> placed world content, meaning someone's child or a
    /// <c>World</c> node's partition reference? The complement of the two staging tests above,
    /// and what an <c>OBJECT_ADD_CHILD</c> parent must be for its child to reach the world.</summary>
    public bool IsPlaced(GameZNode node)
    {
        EnsurePlaced();
        return _placed!.Contains(node.Index);
    }

    /// <summary>The team a node carries in the numbered mission: its own ownership slot, or the
    /// nearest ancestor's where it authors none, or neutral where no ancestor does either. The
    /// engine's own resolution for a world object, so a part inherits the group it hangs under.
    /// ⚠ The mission-structure constructor itself reads the flagged node's OWN slot with no walk;
    /// see docs/org/targeting.md, "What a mission structure's team is".</summary>
    public int WorldObjectTeam(GameZNode node, int mission)
    {
        EnsureParentMap();
        for (int i = node.Index; i >= 0; i = _parent![i])
        {
            // Slot 0 is "this node names no owner", which is what makes the walk continue; it is
            // also the neutral team id, which is what the walk ends on.
            int team = Nodes[i].MissionStructureTeam(mission);
            if (team != 0)
            {
                return team;
            }
        }

        return 0;
    }

    /// <summary>The node whose children list holds <paramref name="node"/>, or null for a root.</summary>
    public GameZNode? ParentOf(GameZNode node)
    {
        EnsureParentMap();
        int p = node.Index >= 0 && node.Index < _parent!.Length ? _parent[node.Index] : -1;
        return p >= 0 ? Nodes[p] : null;
    }

    /// <summary>The nodes <paramref name="node"/>'s children list names, in file order, skipping an index
    /// past the node table.</summary>
    public List<GameZNode> ChildrenOf(GameZNode node) =>
        node.Children.FindAll(c => c >= 0 && c < Nodes.Count).ConvertAll(c => Nodes[c]);

    /// <summary>The world-space transform of a node, accumulated up its parent chain (the
    /// nodes.json children lists, parent is a flat list position, so we invert those).
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

    private static Transform3D ParseTransform(JsonElement tf)
    {
        // Scale is unit on every node of every chapter and of planes.zbd (0 of 4181 non-unit), so
        // it is deliberately ignored.
        var tr = ParseVec3(tf.GetProperty("translate"));
        Basis basis;
        if (tf.TryGetProperty("original", out var m) && m.ValueKind == JsonValueKind.Object)
        {
            // Stored transposed: rows r00..r02, r10..r12, r20..r22 are the rotation's columns. The
            // translation follows as r30..r32, which duplicates "translate" and is not read.
            float M(string name) => m.GetProperty(name).GetSingle();
            basis = new Basis(
                new Vector3(M("r00"), M("r01"), M("r02")),
                new Vector3(M("r10"), M("r11"), M("r12")),
                new Vector3(M("r20"), M("r21"), M("r22")));
        }
        else
        {
            // Euler angles compose as R = Ry(y)·Rx(x)·Rz(z), Godot's YXZ order
            // (verified numerically against the 221 nodes that carry both forms).
            basis = Basis.FromEuler(ParseVec3(tf.GetProperty("rotate")), EulerOrder.Yxz);
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

    // The partition grid's geometry, off the FIRST cell's own bounds rather than off Area. Column
    // 0 starts at its low x edge, row 0 at its HIGH z edge: the second axis has a negative cell
    // size. Every later cell restates the same size. A cell without bounds
    // leaves the grid unusable and WorldPartitionGrid null.
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
            // A flat node with name/model_index/child_indices, and the variant-only fields (area,
            // range, transform) under "data" as {"Object3d": {...}}.
            var prop = FirstProperty(wrapper.GetProperty("data"));
            var body = prop.Value;
            var header = wrapper;
            // Not every kind has every field: Window/Camera/Light lack children.
            var node = new GameZNode
            {
                Kind = prop.Name,
                Name = header.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "",
                MeshIndex = header.TryGetProperty("model_index", out var mi) ? mi.GetInt32() : -1,
                Index = index++,
            };
            // Absent flags stay collidable.
            if (header.TryGetProperty("flags", out var fl) && fl.ValueKind == JsonValueKind.Object)
            {
                if (fl.TryGetProperty("intersect_surface", out var isf))
                    node.IntersectSurface = isf.ValueKind == JsonValueKind.True;
                if (fl.TryGetProperty("active", out var ac))
                    node.Active = ac.ValueKind == JsonValueKind.True;
                if (fl.TryGetProperty("can_modify", out var cm))
                    node.CanModify = cm.ValueKind == JsonValueKind.True;
            }
            // zone_id: -1 (absent too) means always draw. See ZoneGate and docs/formats/gamez.md.
            if (header.TryGetProperty("zone_id", out var zn) && zn.ValueKind == JsonValueKind.Number)
                node.ZoneId = zn.GetInt32();
            // The mission-target word. Serialized only where non-zero, so absent = no flags.
            if (header.TryGetProperty("field040", out var mt) && mt.ValueKind == JsonValueKind.Number)
                node.MissionTargetWord = mt.GetUInt32();
            // ⚠ Flat list positions, never the node's own "index" field (1-based, duplicated).
            // See docs/formats/gamez.md and GameZNode.Index's warning.
            if (header.TryGetProperty("child_indices", out var kids) && kids.ValueKind == JsonValueKind.Array)
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
                            // "values": [{node_index, …}] (its sibling "node_indices" is empty in
                            // every chapter, but is read too in case that ever changes).
                            if (cell.TryGetProperty("values", out var refs))
                                foreach (var nref in refs.EnumerateArray())
                                {
                                    var iv = nref.GetProperty("node_index");
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
            // "transform" is either the string "Initial" (no transform) or a
            // {"RotateTranslateScale": {…}} wrapper.
            if (body.TryGetProperty("transform", out var tf) && tf.ValueKind == JsonValueKind.Object)
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
                Meshes.Add(mesh); // null slot, keep it so mesh_index stays aligned
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
                poly.TriangleStrip = pf.TryGetProperty("tri_strip", out var ts) && ts.ValueKind == JsonValueKind.True;
                poly.ShowBackface = pf.TryGetProperty("show_backface", out var bf) && bf.ValueKind == JsonValueKind.True;
                // unk3 = no_clutter (docs/formats/gamez.md). Absent means false
                // (skip_serializing_if), so TryGetProperty's false default is required.
                poly.NoClutter = pf.TryGetProperty("unk3", out var sf) && sf.ValueKind == JsonValueKind.True;
                if (p.TryGetProperty("priority", out var pr))
                    poly.Priority = pr.GetInt32();
                // At most one value per polygon (docs/formats/world-structure.md census), so only
                // the first element is kept.
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
                    if (!l.TryGetProperty("vertices", out var extra)
                        || extra.ValueKind != JsonValueKind.Array || extra.GetArrayLength() == 0)
                        continue;
                    var c = l.GetProperty("color");
                    // Field meanings are decoded off the original's own draw; see GameZLight.
                    float F(string name) =>
                        l.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
                            ? v.GetSingle() : 0f;
                    // ⚠ unk08 is a period, never a size. It is read only inside the unk04 branch,
                    // so a light with unk04 clear carries a period nothing will ever elapse.
                    float blink = F("unk04") == 1f ? F("unk08") : 0f;
                    mesh.Lights.Add(new GameZLight
                    {
                        Position = ParseVec3(extra[0]),
                        Color = new Color(
                            c.GetProperty("r").GetSingle() / 255f,
                            c.GetProperty("g").GetSingle() / 255f,
                            c.GetProperty("b").GetSingle() / 255f),
                        BlinkPeriod = blink,
                        FadeFar = F("unk52"),
                        // The original's slope is in 0..255 alpha units per metre.
                        FadeSlope = F("unk56") / 255f,
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
            // of models and false on authored self-lit/unfogged geometry; absent, the defaults
            // keep the model lit and fogged.
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

    // textures.json, the material texture table: `{name}` entries, which materials reference by
    // index, so a duplicate entry needs no unique name.
    private void ParseTextures(Stream stream)
    {
        using var doc = JsonDocument.Parse(BufferAll(stream));
        foreach (var t in doc.RootElement.EnumerateArray())
            _textureNames.Add(t.GetProperty("name").GetString() ?? "");
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
                    $"Unknown material soil label '{soilLabel}', the extractor's surface-id " +
                    "registry may have changed; GameZ.SoilLabelToId needs a new entry.");
            if (prop.Name == "Textured")
            {
                if (body.TryGetProperty("texture_index", out var ti))
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
    /// <summary>How many missions of a chapter the ownership slots cover. Bits 22 and up are
    /// flags, so the eleven slots below them are the whole space a node can author.</summary>
    public const int MissionSlots = 11;

    public string Kind = "";   // "Object3d", "Lod", "World", "Display", "Window", "Camera", "Light"
    public string Name = "";
    public int MeshIndex = -1;
    // The original's per-node collision-participation flag (flags.intersect_surface). It is false
    // on geometry the engine never intersection-tests: spinning props, wreck/debris pieces, fire/
    // flake/ripple/splash effects, light glows, ropes, shadows, the C3 spiderweb. Absent flags
    // default to collidable.
    public bool IntersectSurface = true;
    // The build script's own NodeSetActive record (flags.active): false on a node the original
    // never builds visible. Absent flags default to active, matching every
    // other flags.* field here.
    public bool Active = true;
    /// <summary>The terrain-carve gate (<c>flags.can_modify</c>, node-flag bit 16): the original
    /// carves a crater only into a node carrying it, and no shipped node does
    /// (docs/org/craters.md). Absent flags default to false, the shipped value everywhere.</summary>
    public bool CanModify;
    /// <summary>The original's per-node visibility zone (<c>zone_id</c>): <b>-1</b> = always drawn.
    /// Otherwise the node draws only while that id is in the camera's armed zone set. The engine
    /// arms <c>{0, camera weather state}</c>, so <b>0</b> is also always, and 1/2/3 are the
    /// per-state buckets (docs/formats/gamez.md, docs/formats/weather.md's deck census). Absent,
    /// it defaults to -1 = ungated.</summary>
    public int ZoneId = -1;
    /// <summary>The node's mission-target word (<c>field040</c>, absent from the JSON when zero).
    /// Bit 31 marks the node as a mission structure, bit 22 marks a gasbag, and bits 0-21 hold
    /// eleven two-bit ownership slots, one per mission of the chapter, which is where the team
    /// comes from. Decoded in docs/org/targeting.md, "What a mission structure's team is".</summary>
    public uint MissionTargetWord;
    // Flat position in nodes.json. The file is a depth-first serialization of the tree,
    // so this is the original engine's draw order, the cross-node tie-break for
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
    // derived from Area, the two axes run opposite ways (docs/formats/world-structure.md).
    // OriginX is column 0's low x edge; OriginZ is row 0's HIGH z edge.
    public float PartitionOriginX, PartitionOriginZ, PartitionCellX, PartitionCellZ;
    // World nodes only: the map's ground-plane bounds (area = {left=xMin, right=xMax,
    // top=zMin, bottom=zMax}, Godot world coords) and its partition grid size. WorldBuilder
    // uses these to mirror the outermost border terrain outward past the map edge.
    public bool HasArea;
    public float AreaLeft, AreaTop, AreaRight, AreaBottom;
    public int PartitionCols, PartitionRows;

    public List<int> Children { get; } = new(); // indices into GameZ.Nodes (list positions, not node_index)

    /// <summary>Whether this node is one of the engine's mission structures.</summary>
    public bool IsMissionStructure => (MissionTargetWord & 0x80000000u) != 0;

    /// <summary>Whether the structure is a gasbag, which a turret's candidate pass excludes while
    /// the player's own selection still admits it.</summary>
    public bool IsGasbagStructure => (MissionTargetWord & 0x00400000u) != 0;

    /// <summary>The danger-zone difficulty a <c>dzpathN</c> node authors: the flag word shifted
    /// right by 23, unsigned and unmasked, so bits 23 to 31. Zero on a node authoring no word.
    /// Decode: docs/org/aiPilot.md, "Per zone".</summary>
    public int DangerZoneDifficulty => (int)(MissionTargetWord >> 23);

    /// <summary>The team the engine gives this node as a mission structure in the numbered mission
    /// of its chapter: the two-bit ownership slot at bit <c>2 * mission - 2</c>, read as a team id
    /// in the one team space (0 neutral, 1 the player's side, 2 and 3 the enemy indices). Zero
    /// where the node authors no owner for that mission, and on any unflagged node.</summary>
    public int MissionStructureTeam(int mission) =>
        mission < 1 || mission > MissionSlots
            ? 0
            : (int)((MissionTargetWord >> ((2 * mission) - 2)) & 3u);
}

public sealed class GameZMesh
{
    // Null where the JSON carries no model type. "Facade" is the original's own billboard-sprite classification;
    // FacadeMode names the rotation axis (SceneBuilder.GetCylindricalAxis/IsGlowSpriteMesh).
    public string? ModelType;
    public string? FacadeMode;
    // UV units/second (rare: 5 models in this install, a hangar glass-roof sky reflection,
    // an oil-dock texture, and three boat wake fronts; the waterfall's own falls/falls_edge
    // textures do NOT scroll, their motion in the original is the splash puffers, not a
    // UV animation).
    public Vector2 TextureScroll;
    // The model's own render flags. `lighting: false` = the original's D3D lighting is off for
    // this model: self-lit effect geometry, billboards, glows, clutter cards. It draws at full
    // texture × vertex-colour brightness, unmodulated by the mission SUNLIGHT (csky_world_light).
    // `fog: false` = exempt from distance fog. Absent → both true (drawn lit and fogged).
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

    /// <summary>The polygon's overlay passes, <c>materials[1..]</c>, each a second textured
    /// material drawn on these same triangles with its own UVs, on top of the base skin
    /// (<see cref="MaterialIndex"/>/<see cref="UvCoords"/>, which are <c>materials[0]</c>). Null
    /// for the overwhelming majority. Every overlay texture in this install carries an alpha
    /// channel, so the pass composites rather than replacing what is under it; SceneBuilder
    /// builds each as its own surface with its own draw-order bias.</summary>
    public List<GameZPolygonPass>? OverlayPasses;
    public List<Color>? VertexColors; // baked per-corner lighting, parallel to VertexIndices
    public int MaterialIndex = -1;
    public bool TriangleStrip;
    // SHOW_BACKFACE: render double-sided. Polygons without it are
    // backface-culled by the original engine, e.g. the autogyro's interior frame
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
    // Per-polygon weather-zone membership ("zone_set").
    // Null when the field is absent or the array is empty. Every
    // polygon in this install carries at most one value where present, matching the
    // node-level zone_id's -1/1/2/3 numbering (see docs/formats/world-structure.md's
    // census). Nothing reads this yet, which zone is active is not in any data file
    // (see zone_id), so this stays parse-only per the plan's ground rules.
    public int? ZoneSet;

    public List<int> VertexIndices { get; } = new();
}

/// <summary>One overlay pass of a polygon (an entry of <c>materials</c> past the first): the
/// material to skin the polygon's triangles with, and the UVs to skin them by. The UVs are
/// independent of the base pass's, a fog gradient runs its own 8×64 ramp across a face whose
/// base skin tiles a wall texture, which is why the pass cannot be folded into the base
/// surface and gets its own.</summary>
public sealed class GameZPolygonPass
{
    public int MaterialIndex = -1;
    public List<Vector2>? UvCoords;
}

public sealed class GameZMaterial
{
    /// <summary>The material's own texture flipbook (the gamez `cycle` block), frame names in
    /// order, empty for the overwhelming majority. Frame 0 repeats <see cref="TextureName"/>.
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
