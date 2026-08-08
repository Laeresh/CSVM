using System;
using System.Collections.Generic;
using System.Diagnostics;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Map-edge continuation: a rolling window of mirrored terrain tiles that
/// follows the plane past the map boundary, so the world continues indefinitely under the
/// fog — terrain over terrain edges, sea over sea — instead of ending in a void.
///
/// <para>The original does exactly this (user video "C1 IA1 Tile Loading.mp4": 10+ minutes
/// of continuous flight past the edge; the fog wall creeps closer for ~10 s, then the
/// engine re-centers its loaded tile grid around the plane and the visible radius jumps
/// back out — with clutter trees on the continued terrain). What it repeats is a block of
/// <b>border cells</b>, not the map: user-tested — flying east, the map interior (the
/// airport) never reappears. Each axis outside the map folds back into the nearest
/// <see cref="BlockCells"/>-deep band of border cells, repeated forever and <b>alternately
/// reflected</b> so every seam is a shared mirror plane (heights match exactly; straight
/// repetition would step). That also keeps the continuation type-matched to the local edge —
/// sea edge → sea forever, forest edge → forest — as user-observed in the original.
/// <b>The alternating reflection is CONFIRMED original behavior</b> — measured from
/// original-game footage: 67 s of straight flight south off C2's coast, read as a
/// spatio-temporal strip, shows reflection seams recurring every 240 ± 2 frames at
/// NCC 0.89–0.94, with a translational period of exactly twice that (471 frames,
/// NCC +0.90…+0.95). Do NOT make plain repetition the default — <see cref="RepeatInsteadOfMirror"/>
/// exists to look at the refuted hypothesis, not to ship it.</para>
///
/// <para>⚠ <b><see cref="BlockCells"/> is unsettled and its default of 1 is known wrong</b> —
/// see `BL-105`. The measured mirror unit is ~3.2 cells on C2 south and ~2.26 cells on C4
/// north, so it is neither one cell nor one universal constant, and C2's south border row is
/// nearly all water: clamping to it and repeating southward gives a coastline invariant in z,
/// which the footage contradicts. The default stays 1 only so that this prototype changes
/// nothing until the value is chosen; `--map-edge-block=` and F15 are how it gets chosen.
/// Two independent bounds agree on 2–3: the strip measurements, and the airport observation
/// above — the map-centre airfield sits ≥3 cells from any edge (`docs/formats/world-structure.md`),
/// so a block deeper than that would put the map interior back on screen, which 10+ minutes of
/// flight says never happened. That same bound is what refutes whole-map mirroring
/// (<see cref="BlockCells"/> = the grid size), the other implementation that needs no authored
/// constant.</para>
///
/// <para><b>Deliberately not offered: repetition that shares the map-edge vertex row</b>
/// (floated in `docs/formats/world-structure.md`), which would repeat without stepping. Mirror
/// and repeat are both pure per-cell transforms and cost a few lines each; sharing a vertex row
/// is mesh surgery — welding or re-stitching edge vertices in <c>BuildCell</c> — and plain
/// repeat's visible stepping already shows what it needs to show. Build it only if the step
/// turns out to be the sole thing making repeat look wrong.</para>
///
/// <para>The window covers all cells within <see cref="Rings"/> of the focus (the camera),
/// excluding in-map cells (the real world renders those). It re-diffs only when the focus
/// crosses a cell boundary: ~a dozen cells added/freed per crossing, each a cheap build
/// (ground leaves share SceneBuilder's mesh/shape caches; clutter shares the main build's
/// sprite mesh + material). Unlike the original's visible reload pop, the window is sized
/// one ring past the fog wall, so the creep never shows. Extension ground is collidable
/// exactly when the real world is; extension clutter SPRITES are never collidable, matching
/// the map's own (see ClutterBuilder), but extension <b>3D decorations are</b> —
/// attaching the kind's shared collision shape at each mirrored placement costs
/// one <c>BodyAddShape</c> call each, so the whole cell's city is solid for well under a
/// millisecond. This depends on collision being per-shape — do not merge it back into one
/// region trimesh, which would have to rebuild at a boundary crossing and hitch the frame
/// that crosses. Float precision is no concern at these ranges (a
/// 10-min flight ≈ 50 km; float keeps sub-centimeter precision past 100 km — no recenter
/// needed).</para>
/// </summary>
public sealed partial class MapEdgeExtender : Node3D
{
    /// <summary>The tile-grid overlay's mix strength. Deliberately weaker than
    /// <c>ClassOverlay</c>'s 0.5: this overlay is read while flying, over terrain that must stay
    /// legible as terrain, and the band boundaries — not the band colours — carry the
    /// information.</summary>
    internal const float TintStrength = 0.2f;

    // Window radius in cells around the focus. Sized to cover the *raw* weather fog-far
    // (C1 zone2: 4000 m ≈ 4 × 1024 m tiles) plus one margin ring, so the fog border never
    // creeps onto the void even mid-cell and regardless of the fogRangeFactor TUNE. TUNE.
    private const int Rings = 5;

    // Tile-grid overlay palette, indexed by fold parity: [0] neither axis flipped, [1] x flipped,
    // [2] z flipped, [3] both. Four strongly separated hues, because at TintStrength a subtle
    // palette is a grey smear over textured ground. In-map cells take InMapTint instead, so the
    // map boundary itself is never in doubt.
    private static readonly Color[] ParityTints =
    {
        new(0.20f, 0.85f, 1.00f),
        new(1.00f, 0.55f, 0.10f),
        new(0.45f, 1.00f, 0.30f),
        new(1.00f, 0.30f, 0.75f),
    };

    private static readonly Color InMapTint = new(0.80f, 0.80f, 0.85f);

    // Alpha 0 is the shader's identity mix — exactly the untinted world (same convention as
    // ClassOverlay).
    private static readonly Color Untinted = new(0f, 0f, 0f, 0f);

    private readonly GameZ _gamez;
    private readonly SceneBuilder _scene;
    private readonly float _x0, _z0, _tileX, _tileZ;
    private readonly int _cols, _rows;
    // Per source cell: its ground-tile nodes (with the accumulated ancestor transform —
    // identity for every observed tile, kept for correctness) and its clutter sprites.
    private readonly Dictionary<(int, int), List<(GameZNode Node, Transform3D ParentXf)>> _tiles = new();
    private readonly Dictionary<(int, int), List<(int Kind, Transform3D Xf)>> _sprites = new();
    // Gamez node index -> its map cell, for every in-map ground tile. Lets the tile-grid overlay
    // colour the real world's own tiles through the same grid maths the extension uses, keyed on
    // the same AnimRuntime.IndexMeta stamp the built scene already carries.
    private readonly Dictionary<int, (int, int)> _tileCell = new();
    private readonly IReadOnlyList<ClutterBuilder.KindExport>? _clutter;

    private readonly Dictionary<(int, int), Node3D> _live = new();
    // The focus cells the current window was built around — one per player (splitscreen serves
    // every pane from one window). Empty until the first Update.
    private readonly List<(int, int)> _centerCells = new();

    private int _blockCells = 1;
    private bool _repeat;
    private bool _tinted;
    // Starts true so the FIRST window build is timed too: it builds the whole window from nothing,
    // which is the same work an F15 fold change costs. That makes the hitch a measured number in
    // every session's log rather than something only discoverable by pressing the key.
    private bool _foldChanged = true;

    private MapEdgeExtender(GameZ gamez, SceneBuilder scene, GameZNode world,
        IReadOnlyList<ClutterBuilder.KindExport>? clutter)
    {
        _gamez = gamez;
        _scene = scene;
        _clutter = clutter;
        _x0 = world.AreaLeft;
        _z0 = world.AreaTop;
        _cols = world.PartitionCols;
        _rows = world.PartitionRows;
        _tileX = (world.AreaRight - world.AreaLeft) / _cols;
        _tileZ = (world.AreaBottom - world.AreaTop) / _rows;
        Name = "map_edge";
    }

    /// <summary>Extension cells currently instantiated (diagnostics).</summary>
    public int LiveCellCount => _live.Count;

    /// <summary>How many border cells deep the repeated block is. 1 = the historical clamp to a
    /// single border cell. ⚠ Unsettled — see the class doc and `BL-105`; 1 is the default only
    /// because it is what shipped, not because it is right.</summary>
    public int BlockCells => _blockCells;

    /// <summary>Translate the block instead of alternately reflecting it. <b>Refuted for the
    /// original</b> (`CAP-17`) and kept only so the hypothesis can be looked at: it butts the
    /// map's opposite edge heights together, so every seam steps.</summary>
    public bool RepeatInsteadOfMirror => _repeat;

    /// <summary>The widest block this world can take — a block deeper than the grid has no more
    /// cells to fold into. At this value the continuation mirrors the whole map.</summary>
    public int MaxBlockCells => Math.Max(1, Math.Min(_cols, _rows));

    /// <summary>Whether the tile-grid overlay's tints are currently applied.</summary>
    public bool TileTintShown => _tinted;

    /// <summary>Wall milliseconds the last fold-change rebuild took, 0 before the first one. Shown
    /// in the tile-grid overlay's HUD so the F15 hitch is a number on screen rather than a
    /// feeling.</summary>
    public double LastRebuildMs { get; private set; }

    /// <summary>The shader's identity mix — what the overlay stamps to clear a tint.</summary>
    internal static Color UntintedMix => Untinted;

    /// <summary>The four fold-parity colours, indexed <c>(x flipped ? 1 : 0) | (z flipped ? 2 : 0)</c>
    /// — for the overlay's legend, so a palette edit here cannot drift out of sync with the key on
    /// screen (the rule <c>ClassOverlay.BuildLegendText</c> follows).</summary>
    internal static IReadOnlyList<Color> ParityLegendColors => ParityTints;

    /// <summary>The in-map cells' legend colour. See <see cref="ParityLegendColors"/>.</summary>
    internal static Color InMapLegendColor => InMapTint;

    // ---------------------------------------------------------------- fold mapping

    /// <summary>Continuation along one axis: outside the map, the nearest <paramref name="block"/>
    /// border cells repeat forever, alternately reflected so every seam is a shared mirror plane
    /// (heights match exactly; plain repetition would step).
    ///
    /// <para>The fold. The block occupies source indices <c>[a, a+block)</c> — <c>a = n-block</c>
    /// past the high edge, <c>a = 0</c> before the low one. Offsets from <c>a</c> reduce modulo the
    /// period: the first half of a mirror period walks the block forward, the second half walks it
    /// back reflected. Repetition uses a period of <c>block</c> instead of <c>2·block</c>, which
    /// drops the reflected half and never flips.</para>
    ///
    /// <para>At <paramref name="block"/> = 1 with <paramref name="repeat"/> false this reduces
    /// exactly to the historical clamp-to-border-cell — the odd ring mirrored, the even ring a
    /// straight copy — which is what makes the generalization safe to land ahead of a decision on
    /// the value. <c>MapEdgeFoldTests</c> pins that equivalence.</para></summary>
    public static (int Src, bool Flip) FoldAxis(int i, int n, int block, bool repeat)
    {
        if (i >= 0 && i < n)
            return (i, false);
        int b = Math.Clamp(block, 1, n);
        int a = i >= n ? n - b : 0;
        int period = repeat ? b : 2 * b;
        // C# '%' keeps the sign of the dividend, and the low edge always has a negative offset.
        int m = (((i - a) % period) + period) % period;
        return m < b ? (a + m, false) : (a + ((2 * b) - 1 - m), true);
    }

    /// <summary>Re-points the continuation at a different block depth or fold mode, rebuilding the
    /// whole window. <b>Debug/prototype path only</b> — this frees and rebuilds every live cell in
    /// one frame (up to (2·<see cref="Rings"/>+1)² of them), roughly ten times a normal
    /// cell-crossing diff, so expect a visible hitch. Spreading it over frames was rejected: a
    /// half-old, half-new window is worse to judge than a stutter.</summary>
    public void SetContinuation(int blockCells, bool repeat)
    {
        blockCells = Math.Clamp(blockCells, 1, MaxBlockCells);
        if (blockCells == _blockCells && repeat == _repeat)
            return;
        _blockCells = blockCells;
        _repeat = repeat;
        _foldChanged = true;
        foreach (var (_, node) in _live)
            node.QueueFree();
        _live.Clear();
        // Forces the next Update past its no-op check, which rebuilds the window around the
        // unchanged focus.
        _centerCells.Clear();
    }

    /// <summary>Show or hide the tile-grid tints on the extension cells. Cheap — it re-stamps a
    /// per-instance shader parameter on what is already built and never rebuilds a cell. New cells
    /// pick the state up in <see cref="BuildCell"/> as they are born, which is why this lives here
    /// rather than in a walk-once overlay: extension cells churn on every boundary crossing, so a
    /// cached tint list would go stale within seconds of flying.</summary>
    public void SetTileTint(bool on)
    {
        _tinted = on;
        foreach (var (cell, node) in _live)
            ApplyTint(node, cell.Item1, cell.Item2);
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

        // A fold change (F15/F16) drops the whole window and rebuilds it here, which is ~10x a
        // normal crossing's diff. Time it and say so rather than leaving the stutter unexplained:
        // a debug key that hitches is fine, a debug key that hitches for an unknown reason is not.
        long startedAt = _foldChanged ? Stopwatch.GetTimestamp() : 0L;
        int built = 0;
        foreach (var cell in wanted)
            if (!_live.ContainsKey(cell))
            {
                var node = BuildCell(cell.Item1, cell.Item2);
                AddChild(node);
                _live[cell] = node;
                built++;
            }
        if (_foldChanged)
        {
            _foldChanged = false;
            double ms = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            LastRebuildMs = ms;
            string mode = _repeat ? "repeat" : "mirror";
            Log.Info("world", $"map edge: built {built} cells in {ms:F0} ms (block {_blockCells}, {mode})");
        }
    }

    /// <summary>Builds the extender for a world, or null when the world carries no usable
    /// area/partition grid. <paramref name="clutter"/> (the chapter's built ClutterBuilder,
    /// if any) lets the extension grow the same trees the map grows — the original shows
    /// clutter on the continued terrain (see the class doc video evidence).</summary>
    /// <param name="blockCells">Initial <see cref="BlockCells"/> (<c>--map-edge-block=</c>).
    /// Clamped to the grid, so the caller may pass anything.</param>
    /// <param name="repeat">Initial <see cref="RepeatInsteadOfMirror"/>
    /// (<c>--map-edge-mode=repeat</c>).</param>
    internal static MapEdgeExtender? Create(GameZ gamez, SceneBuilder scene, GameZNode world,
        ClutterBuilder? clutter, int blockCells = 1, bool repeat = false)
    {
        if (!world.HasArea || world.PartitionCols <= 0 || world.PartitionRows <= 0
            || world.AreaRight <= world.AreaLeft || world.AreaBottom <= world.AreaTop)
            return null;
        var ext = new MapEdgeExtender(gamez, scene, world, clutter?.ExportedKinds);
        ext._blockCells = Math.Clamp(blockCells, 1, ext.MaxBlockCells);
        ext._repeat = repeat;
        ext.ScanTiles(world);
        if (ext._tiles.Count == 0)
            return null;
        ext.BinClutter();
        return ext;
    }

    /// <summary>The tile-grid tint for a built <b>in-map</b> ground tile, identified by the gamez
    /// node index the built scene carries as <c>AnimRuntime.IndexMeta</c>; null when that node is
    /// not one of the map's ground tiles. Keeping this here rather than re-deriving a cell in the
    /// overlay means the map's own tiles and the extension's are binned by one piece of grid
    /// maths — including <see cref="ScanTiles"/>'s edge clamping, which an independent
    /// position-to-cell conversion in the overlay would get subtly wrong on the split half-tiles.
    /// <para>Includes the alpha, so the caller stamps it straight into
    /// <see cref="SceneBuilder.TintParam"/>.</para></summary>
    internal Color? InMapTintForNode(int gamezNodeIndex) =>
        _tileCell.TryGetValue(gamezNodeIndex, out var cell)
            ? new Color(TintFor(cell.Item1, cell.Item2), TintStrength)
            : null;

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

    // The partition-grid cell a world position falls in (may be outside the map — that is the
    // whole point; negative/oversize indices address extension cells).
    private (int, int) CellOf(Vector3 p) => (
        Mathf.FloorToInt((p.X - _x0) / _tileX),
        Mathf.FloorToInt((p.Z - _z0) / _tileZ));

    // ---------------------------------------------------------------- cell building

    // One extension cell: the mirrored ground leaves (mesh + collider, no child subtrees —
    // so edge buildings/props are never duplicated) under a reflection-transform holder,
    // plus the source cell's clutter sprites at mirrored positions (billboards re-face the
    // camera by shader, so mirroring a sprite is just mirroring its planted point).
    private Node3D BuildCell(int ix, int iz)
    {
        var (sx, fx) = FoldAxis(ix, _cols, _blockCells, _repeat);
        var (sz, fz) = FoldAxis(iz, _rows, _blockCells, _repeat);
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
        if (_tinted)
            ApplyTint(cell, ix, iz);
        return cell;
    }

    // ---------------------------------------------------------------- tile-grid overlay

    /// <summary>Stamps (or clears) one extension cell's tile-grid tint. Ground only — the clutter
    /// multimeshes are left alone deliberately: this overlay is read as a map of the terrain
    /// sheets, and tinting a forest's worth of sprites the same colour buries the tile boundaries
    /// the overlay exists to show.
    ///
    /// <para>The mix goes through <see cref="SceneBuilder.TintParam"/>, the world shaders' own
    /// per-instance parameter — never a <c>MaterialOverride</c>/<c>MaterialOverlay</c>, which is a
    /// different shader entirely (see <see cref="SceneBuilder.TintLine"/> for the three ways that
    /// has gone wrong before).</para></summary>
    private void ApplyTint(Node cell, int ix, int iz)
    {
        var color = _tinted ? new Color(TintFor(ix, iz), TintStrength) : Untinted;
        void Walk(Node n)
        {
            if (n is MeshInstance3D mi && mi.Name == "mesh")
                mi.SetInstanceShaderParameter(SceneBuilder.TintParam, color);
            foreach (var child in n.GetChildren())
                Walk(child);
        }
        Walk(cell);
    }

    /// <summary>The tile-grid colour for a cell. Hue is the fold parity, so one run of a single
    /// hue spans exactly <see cref="BlockCells"/> cells and the <b>width of a colour band reads
    /// off N directly</b>; the two axes get independent parities so corner regions — folded on
    /// both — stay unambiguous. Value alternates on a per-cell checker inside the band, which is
    /// what turns "wide-ish" into a countable number of cells. In-map cells take a neutral tint so
    /// the map boundary is never in doubt.</summary>
    private Color TintFor(int ix, int iz)
    {
        bool inMap = ix >= 0 && ix < _cols && iz >= 0 && iz < _rows;
        Color baseColor;
        if (inMap)
        {
            baseColor = InMapTint;
        }
        else
        {
            var (_, fx) = FoldAxis(ix, _cols, _blockCells, _repeat);
            var (_, fz) = FoldAxis(iz, _rows, _blockCells, _repeat);
            baseColor = ParityTints[(fx ? 1 : 0) | (fz ? 2 : 0)];
        }
        // Godot's '%' on ints keeps the dividend's sign, and extension indices go negative.
        bool dark = (((ix + iz) % 2) + 2) % 2 == 1;
        return dark ? baseColor.Darkened(0.35f) : baseColor;
    }

    // The source cell's decorations at mirrored placements. A sprite mirrors as its position
    // alone (the billboard shader re-faces it from the instance origin), but a 3D city block
    // has to carry the mirror's reflection in its basis or the continued city would face the
    // wrong way — which is why the export switched from positions to whole transforms.
    // The reflection flips winding; world geometry renders double-sided
    // and fullbright, so nothing reads the inverted normals.
    //
    // Extension 3D decorations ARE collidable. Cheap because ClutterBuilder shares ONE shape
    // per decoration mesh: making a cell solid is one PhysicsServer3D.BodyAddShape per
    // building against a shape that already exists — a few hundred pointer-sized calls, no
    // geometry work at all. (A merged per-region trimesh could not do this: rebuilding one on
    // the frame the camera crosses a cell boundary would hitch.) Sprites stay
    // pass-through, matching the map's own (a billboard has no side to hit).
    //
    // The shared shapes stay alive because this node holds `_clutter` (the KindExport list)
    // for its whole lifetime; the physics server itself holds only RIDs.
    private void AddCellClutter(Node3D cell, Transform3D mirror, List<(int Kind, Transform3D Xf)> sprites)
    {
        // Group the cell's decorations per kind (kept in kind order for determinism).
        var byKind = new Dictionary<int, List<Transform3D>>();
        foreach (var (kind, xf) in sprites)
        {
            if (!byKind.TryGetValue(kind, out var list))
                byKind[kind] = list = new List<Transform3D>();
            list.Add(_clutter![kind].Solid ? mirror * xf
                : new Transform3D(Basis.Identity, mirror * xf.Origin));
        }

        // One body for the whole cell's buildings, named so a crash log locates the cell.
        // Created lazily: most cells are sea or forest and have no solid decoration at all.
        StaticBody3D? solidBody = null;

        foreach (var (kindIndex, placements) in byKind)
        {
            var kind = _clutter![kindIndex];
            if (kind.Solid && kind.CollisionShape is { } shape)
            {
                if (solidBody == null)
                {
                    solidBody = new StaticBody3D { Name = "clutter_bld_ext" };
                    cell.AddChild(solidBody);
                }
                var bodyRid = solidBody.GetRid();
                var shapeRid = shape.GetRid();
                foreach (var xf in placements)
                    PhysicsServer3D.BodyAddShape(bodyRid, shapeRid, xf);
            }
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = kind.Mesh,
                InstanceCount = placements.Count,
            };
            for (int i = 0; i < placements.Count; i++)
                mm.SetInstanceTransform(i, placements[i]);
            var mmi = new MultiMeshInstance3D
            {
                Multimesh = mm,
                MaterialOverride = kind.Material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                ExtraCullMargin = kind.Width, // the billboard shader swings verts outside the AABB
                Name = $"clutter_{kindIndex}",
            };
            if (kind.Solid)
                mmi.SetInstanceShaderParameter("node_bias", kind.NodeBias);
            cell.AddChild(mmi);
        }
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
                _tileCell[idx] = (cx, cz);
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
            foreach (var xf in _clutter[k].Placements)
            {
                int cx = Mathf.FloorToInt((xf.Origin.X - _x0) / _tileX);
                int cz = Mathf.FloorToInt((xf.Origin.Z - _z0) / _tileZ);
                if (cx < 0 || cx >= _cols || cz < 0 || cz >= _rows)
                    continue;
                if (!_sprites.TryGetValue((cx, cz), out var list))
                    _sprites[(cx, cz)] = list = new List<(int, Transform3D)>();
                list.Add((k, xf));
            }
    }
}
