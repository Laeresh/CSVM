using System;
using System.Collections.Generic;
using Godot;

namespace CrimsonSkies.Mech3;

/// <summary>
/// Map-edge continuation (Run-2 item 7): a rolling window of mirrored terrain tiles that
/// follows the plane past the map boundary, so the world continues indefinitely under the
/// fog — terrain over terrain edges, sea over sea — instead of ending in a void.
///
/// <para>The original does exactly this (user video "C1 IA1 Tile Loading.mp4": 10+ minutes
/// of continuous flight past the edge; the fog wall creeps closer for ~10 s, then the
/// engine re-centers its loaded tile grid around the plane and the visible radius jumps
/// back out — with clutter trees on the continued terrain). What it repeats is the
/// <b>local border tile</b>, not the map: user-tested 2026-07-18, flying east shows the
/// same one-tile view every crossing (the video's ~10 s loop = one tile at that speed) and
/// the map interior — the airport — never reappears. So each axis outside the map clamps
/// to its border cell, repeated forever and <b>alternately reflected</b> so every seam is
/// a shared mirror plane (heights match exactly; straight repetition would step). That
/// also keeps the continuation type-matched to the local edge — sea edge → sea forever,
/// forest edge → forest — as user-observed in the original. NOTE: the alternating
/// reflection is OUR seam-free construction, not verified original behavior — the user
/// believes the original does NOT mirror (NOTES.md: plain repetition, possibly sharing the
/// map-edge vertex row); an in-game A/B of a recognizable asymmetric border feature would
/// settle it. Open fidelity question, cheap to swap (see MirrorAxis).</para>
///
/// <para>The window covers all cells within <see cref="Rings"/> of the focus (the camera),
/// excluding in-map cells (the real world renders those). It re-diffs only when the focus
/// crosses a cell boundary: ~a dozen cells added/freed per crossing, each a cheap build
/// (ground leaves share SceneBuilder's mesh/shape caches; clutter shares the main build's
/// sprite mesh + material). Unlike the original's visible reload pop, the window is sized
/// one ring past the fog wall, so the creep never shows. Ground and clutter are collidable
/// exactly when the real world is; float precision is no concern at these ranges (a 10-min
/// flight ≈ 50 km; float keeps sub-centimeter precision past 100 km — no recenter needed).</para>
/// </summary>
public sealed partial class MapEdgeExtender : Node3D
{
    // Window radius in cells around the focus. Sized to cover the *raw* weather fog-far
    // (C1 zone2: 4000 m ≈ 4 × 1024 m tiles) plus one margin ring, so the fog border never
    // creeps onto the void even mid-cell and regardless of the fogRangeFactor TUNE. TUNE.
    private const int Rings = 5;

    private readonly GameZ _gamez;
    private readonly SceneBuilder _scene;
    private readonly bool _collision;
    private readonly float _x0, _z0, _tileX, _tileZ;
    private readonly int _cols, _rows;
    // Per source cell: its ground-tile nodes (with the accumulated ancestor transform —
    // identity for every observed tile, kept for correctness) and its clutter sprites.
    private readonly Dictionary<(int, int), List<(GameZNode Node, Transform3D ParentXf)>> _tiles = new();
    private readonly Dictionary<(int, int), List<(int Kind, Vector3 Pos)>> _sprites = new();
    private readonly IReadOnlyList<ClutterBuilder.KindExport>? _clutter;

    private readonly Dictionary<(int, int), Node3D> _live = new();
    // The focus cells the current window was built around — one per player (splitscreen serves
    // every pane from one window). Empty until the first Update.
    private readonly List<(int, int)> _centerCells = new();

    /// <summary>Extension cells currently instantiated (diagnostics).</summary>
    public int LiveCellCount => _live.Count;

    private MapEdgeExtender(GameZ gamez, SceneBuilder scene, GameZNode world, bool collision,
        IReadOnlyList<ClutterBuilder.KindExport>? clutter)
    {
        _gamez = gamez;
        _scene = scene;
        _collision = collision;
        _clutter = clutter;
        _x0 = world.AreaLeft;
        _z0 = world.AreaTop;
        _cols = world.PartitionCols;
        _rows = world.PartitionRows;
        _tileX = (world.AreaRight - world.AreaLeft) / _cols;
        _tileZ = (world.AreaBottom - world.AreaTop) / _rows;
        Name = "map_edge";
    }

    /// <summary>Builds the extender for a world, or null when the world carries no usable
    /// area/partition grid. <paramref name="clutter"/> (the chapter's built ClutterBuilder,
    /// if any) lets the extension grow the same trees the map grows — the original shows
    /// clutter on the continued terrain (see the class doc video evidence).</summary>
    internal static MapEdgeExtender? Create(GameZ gamez, SceneBuilder scene, GameZNode world,
        bool collision, ClutterBuilder? clutter)
    {
        if (!world.HasArea || world.PartitionCols <= 0 || world.PartitionRows <= 0
            || world.AreaRight <= world.AreaLeft || world.AreaBottom <= world.AreaTop)
            return null;
        var ext = new MapEdgeExtender(gamez, scene, world, collision, clutter?.ExportedKinds);
        ext.ScanTiles(world);
        if (ext._tiles.Count == 0)
            return null;
        ext.BinClutter();
        return ext;
    }

    /// <summary>Single-focus convenience overload (the single-player flight camera).</summary>
    public void Update(Vector3 focus) => Update(new[] { focus });

    /// <summary>Re-centers the window on the focus points (one per player camera — splitscreen
    /// serves every pane from this one window, so the wanted set is the union of the rings around
    /// each). Cheap no-op until one of them crosses into another cell; then freed/built cells diff
    /// by ~one window row. Call once per frame.</summary>
    public void Update(IReadOnlyList<Vector3> focuses)
    {
        if (focuses.Count == 0)
            return;
        // No-op unless some focus changed cell (the common case, every frame).
        bool moved = focuses.Count != _centerCells.Count;
        for (int i = 0; !moved && i < focuses.Count; i++)
            moved = CellOf(focuses[i]) != _centerCells[i];
        if (!moved)
            return;
        _centerCells.Clear();
        foreach (var f in focuses)
            _centerCells.Add(CellOf(f));

        var wanted = new HashSet<(int, int)>();
        foreach (var (fx, fz) in _centerCells)
            for (int dx = -Rings; dx <= Rings; dx++)
                for (int dz = -Rings; dz <= Rings; dz++)
                {
                    var c = (fx + dx, fz + dz);
                    // In-map cells are the real world's — never duplicated by the extension.
                    if (c.Item1 >= 0 && c.Item1 < _cols && c.Item2 >= 0 && c.Item2 < _rows)
                        continue;
                    wanted.Add(c);
                }

        List<(int, int)>? drop = null;
        foreach (var (cell, node) in _live)
            if (!wanted.Contains(cell))
            {
                node.QueueFree();
                (drop ??= new List<(int, int)>()).Add(cell);
            }
        if (drop != null)
            foreach (var cell in drop)
                _live.Remove(cell);

        foreach (var cell in wanted)
            if (!_live.ContainsKey(cell))
            {
                var node = BuildCell(cell.Item1, cell.Item2);
                AddChild(node);
                _live[cell] = node;
            }
    }

    // The partition-grid cell a world position falls in (may be outside the map — that is the
    // whole point; negative/oversize indices address extension cells).
    private (int, int) CellOf(Vector3 p) => (
        Mathf.FloorToInt((p.X - _x0) / _tileX),
        Mathf.FloorToInt((p.Z - _z0) / _tileZ));

    // ---------------------------------------------------------------- mirror mapping

    // Continuation along one axis: outside the map, the LOCAL BORDER cell repeats forever,
    // alternately reflected so every seam is a shared mirror plane (heights match exactly;
    // plain repetition would step). NOT a whole-map tiling — user-tested in the original
    // (2026-07-18): flying east for 10+ minutes shows the same one-tile view every crossing
    // and the map interior (the airport) never reappears; the video's ~10 s reload loop is
    // exactly one tile crossing. The first ring (odd parity) is the border cell mirrored
    // across the boundary, the second its straight copy, and so on.
    private static (int Src, bool Flip) MirrorAxis(int i, int n)
    {
        if (i >= 0 && i < n)
            return (i, false);
        int edge = i >= n ? n - 1 : 0;
        int ring = i >= n ? i - (n - 1) : -i;
        return (edge, (ring & 1) == 1);
    }

    // World transform mapping source cell (sx, sz) geometry onto target cell (ix, iz):
    // pure translation on an unflipped axis, reflection about the shared mirror plane on a
    // flipped one (x' = 2a − x with a = the plane between the copies).
    private Transform3D MirrorTransform(int ix, int iz, int sx, bool fx, int sz, bool fz)
    {
        float ox = fx ? 2 * _x0 + (sx + ix + 1) * _tileX : (ix - sx) * _tileX;
        float oz = fz ? 2 * _z0 + (sz + iz + 1) * _tileZ : (iz - sz) * _tileZ;
        return new Transform3D(
            Basis.FromScale(new Vector3(fx ? -1 : 1, 1, fz ? -1 : 1)),
            new Vector3(ox, 0, oz));
    }

    // ---------------------------------------------------------------- cell building

    // One extension cell: the mirrored ground leaves (mesh + collider, no child subtrees —
    // so edge buildings/props are never duplicated) under a reflection-transform holder,
    // plus the source cell's clutter sprites at mirrored positions (billboards re-face the
    // camera by shader, so mirroring a sprite is just mirroring its planted point).
    private Node3D BuildCell(int ix, int iz)
    {
        var (sx, fx) = MirrorAxis(ix, _cols);
        var (sz, fz) = MirrorAxis(iz, _rows);
        var cell = new Node3D { Name = $"ext_{ix}_{iz}" };
        var mirror = MirrorTransform(ix, iz, sx, fx, sz, fz);

        if (_tiles.TryGetValue((sx, sz), out var tiles))
        {
            var ground = new Node3D { Name = "ground", Transform = mirror };
            foreach (var (node, parentXf) in tiles)
            {
                var leaf = _scene.BuildSubtree(node, skip: n => !ReferenceEquals(n, node));
                if (leaf == null)
                    continue;
                if (parentXf != Transform3D.Identity)
                {
                    var wrap = new Node3D { Transform = parentXf };
                    wrap.AddChild(leaf);
                    ground.AddChild(wrap);
                }
                else
                {
                    ground.AddChild(leaf);
                }
            }
            cell.AddChild(ground);
        }

        if (_clutter != null && _sprites.TryGetValue((sx, sz), out var sprites))
            AddCellClutter(cell, mirror, sprites);
        return cell;
    }

    private void AddCellClutter(Node3D cell, Transform3D mirror, List<(int Kind, Vector3 Pos)> sprites)
    {
        // Group the cell's sprites per kind (kept in kind order for determinism).
        var byKind = new Dictionary<int, List<Vector3>>();
        foreach (var (kind, pos) in sprites)
        {
            if (!byKind.TryGetValue(kind, out var list))
                byKind[kind] = list = new List<Vector3>();
            list.Add(mirror * pos);
        }

        List<Vector3>? faces = _collision ? new List<Vector3>() : null;
        foreach (var (kindIndex, positions) in byKind)
        {
            var kind = _clutter![kindIndex];
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = kind.Mesh,
                InstanceCount = positions.Count,
            };
            for (int i = 0; i < positions.Count; i++)
                mm.SetInstanceTransform(i, new Transform3D(Basis.Identity, positions[i]));
            cell.AddChild(new MultiMeshInstance3D
            {
                Multimesh = mm,
                MaterialOverride = kind.Material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                ExtraCullMargin = kind.Width, // the billboard shader swings verts outside the AABB
                Name = $"clutter_{kindIndex}",
            });
            if (faces != null)
            {
                float w = kind.Width * 0.5f, h = kind.Height;
                foreach (var pos in positions)
                {
                    AddQuad(faces, pos + new Vector3(-w, 0, 0), pos + new Vector3(w, 0, 0), h);
                    AddQuad(faces, pos + new Vector3(0, 0, -w), pos + new Vector3(0, 0, w), h);
                }
            }
        }

        if (faces is { Count: > 0 })
        {
            var body = new StaticBody3D { Name = "clutter_col" };
            body.AddChild(new CollisionShape3D
            {
                Shape = new ConcavePolygonShape3D { Data = faces.ToArray(), BackfaceCollision = true },
            });
            cell.AddChild(body);
        }
    }

    private static void AddQuad(List<Vector3> faces, Vector3 baseA, Vector3 baseB, float height)
    {
        var up = new Vector3(0, height, 0);
        faces.Add(baseA); faces.Add(baseB); faces.Add(baseB + up);
        faces.Add(baseA); faces.Add(baseB + up); faces.Add(baseA + up);
    }

    // ---------------------------------------------------------------- source scan

    // Collects the map's ground tiles per grid cell: every Object3d (recursively — the
    // map-center airfield tiles hang one level down under group nodes) whose own mesh is a
    // single ~cell-sized terrain/water sheet, excluding cloud/sky sheets (the cloudlayer
    // deck tiles are cell-sized too). Verified on C1: all 144 cells covered (147 tiles —
    // split half-tile strips and the coastal falls overlay bin alongside their cell's base
    // tile), zero rotated or translated ancestors.
    private void ScanTiles(GameZNode world)
    {
        void Walk(int idx, Transform3D parentXf)
        {
            if (idx < 0 || idx >= _gamez.Nodes.Count)
                return;
            var node = _gamez.Nodes[idx];
            if (WorldBuilder.SkipWorldNode(node))
                return;
            if (node.Kind == "Lod" && node.LodRangeMin != 0f)
                return;
            var xf = node.Local is { } local ? parentXf * local : parentXf;
            if (IsGroundTile(node, out var center))
            {
                var worldCenter = xf * center;
                int cx = Mathf.Clamp(Mathf.FloorToInt((worldCenter.X - _x0) / _tileX), 0, _cols - 1);
                int cz = Mathf.Clamp(Mathf.FloorToInt((worldCenter.Z - _z0) / _tileZ), 0, _rows - 1);
                if (!_tiles.TryGetValue((cx, cz), out var list))
                    _tiles[(cx, cz)] = list = new List<(GameZNode, Transform3D)>();
                list.Add((node, parentXf));
            }
            foreach (var c in node.Children)
                Walk(c, xf);
        }
        foreach (var c in world.Children)
            Walk(c, Transform3D.Identity);
        if (world.PartitionNodes != null)
            foreach (var idx in world.PartitionNodes)
                Walk(idx, Transform3D.Identity);
    }

    // A ground tile: an Object3d whose own mesh is a single ~one-cell terrain/water sheet
    // (the regular 1024-unit tiles plus split half-tiles), NOT the cloudlayer deck / sky
    // sheets (one-cell too). `center` = the mesh AABB centroid in node-local space.
    private bool IsGroundTile(GameZNode n, out Vector3 center)
    {
        center = Vector3.Zero;
        if (n.Kind != "Object3d" || n.MeshIndex < 0 || n.MeshIndex >= _gamez.Meshes.Count)
            return false;
        var mesh = _gamez.Meshes[n.MeshIndex];
        if (mesh.Vertices.Count == 0)
            return false;
        foreach (var poly in mesh.Polygons)
        {
            if (poly.MaterialIndex < 0 || poly.MaterialIndex >= _gamez.Materials.Count)
                continue;
            var tex = _gamez.Materials[poly.MaterialIndex].TextureName;
            if (tex != null && WorldBuilder.IsCloudOrSkyTexture(tex))
                return false;
        }
        Vector3 min = mesh.Vertices[0], max = mesh.Vertices[0];
        foreach (var v in mesh.Vertices)
        {
            min = min.Min(v);
            max = max.Max(v);
        }
        float dx = max.X - min.X, dz = max.Z - min.Z;
        if (dx < 0.4f * _tileX || dx > 1.35f * _tileX || dz < 0.4f * _tileZ || dz > 1.35f * _tileZ)
            return false;
        center = (min + max) * 0.5f;
        return true;
    }

    // Bins the chapter's clutter sprites (world positions from the main build) into their
    // map cells, so each extension cell can mirror exactly its source cell's trees.
    private void BinClutter()
    {
        if (_clutter == null)
            return;
        for (int k = 0; k < _clutter.Count; k++)
            foreach (var pos in _clutter[k].Positions)
            {
                int cx = Mathf.FloorToInt((pos.X - _x0) / _tileX);
                int cz = Mathf.FloorToInt((pos.Z - _z0) / _tileZ);
                if (cx < 0 || cx >= _cols || cz < 0 || cz >= _rows)
                    continue;
                if (!_sprites.TryGetValue((cx, cz), out var list))
                    _sprites[(cx, cz)] = list = new List<(int, Vector3)>();
                list.Add((k, pos));
            }
    }
}
