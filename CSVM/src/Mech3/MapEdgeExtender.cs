using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Map-edge continuation: a rolling window of repeated terrain tiles that
/// follows the plane past the map boundary, so the world continues indefinitely under the
/// fog — terrain over terrain edges, sea over sea — instead of ending in a void.
///
/// <para>The original does exactly this (user video "C1 IA1 Tile Loading.mp4": 10+ minutes
/// of continuous flight past the edge; the fog wall creeps closer for ~10 s, then the
/// engine re-centers its loaded tile grid around the plane and the visible radius jumps
/// back out — with clutter trees on the continued terrain). What it repeats is a block of
/// <b>border cells</b>, not the map: user-tested — flying east, the map interior (the
/// airport) never reappears. Each axis outside the map folds back into the nearest
/// <see cref="BlockCells"/>-deep band of border cells, <b>translated</b>, and that block is
/// per chapter (<see cref="DefaultBlockCells"/>). That keeps the continuation type-matched to
/// the local edge — sea edge → sea forever, forest edge → forest — as user-observed.</para>
///
/// <para>⚠ <b>It REPEATS; it does not mirror. Settled 2026-08-08 by A/B against the original
/// at the controls</b> — `repeat` at the per-chapter block below matches the original exactly,
/// on C1, C2, C4 and C5, with no seam gaps. <see cref="RepeatInsteadOfMirror"/> defaults true
/// for that reason, and alternating reflection is the mode kept only to look at.</para>
///
/// <para>⚠ <b>That reversed the `CAP-17` strip analysis, whose DATA was always consistent with
/// repetition</b> — its consecutive translational periods matched <i>as-is</i> at +0.899…+0.949
/// against time-reversed −0.086…+0.080, which is what repetition predicts and reflection does
/// not. The mirroring claim came from a reflection scan finding "seams" at half that spacing,
/// which is the zigzag-coast symmetry that analysis itself documented as a trap, and a signal of
/// period P correlates at 2P for free. ⚠ Its metre figures were wrong too (~3.2 cells for a
/// C2 edge that measures 2): <b>do not size a map-edge block from a video-derived period</b> —
/// see `analysis/video-flight-calibration/FINDINGS.md`. Fly it and look.</para>
///
/// <para><b>Deliberately not offered: repetition that shares the map-edge vertex row</b>
/// (floated in `docs/formats/world-structure.md`), which would repeat without stepping. Mirror
/// and repeat are both pure per-cell transforms and cost a few lines each; sharing a vertex row
/// is mesh surgery — welding or re-stitching edge vertices in <c>BuildCell</c>. It has not been
/// needed: plain repeat at the per-chapter block shows no step and no gap at the controls, the
/// tile meshes evidently spanning their cells exactly.</para>
///
/// <para>⚠ <b>A cell's ground is not always ONE tile</b> — and that was `BL-316`. Three C5 border
/// cells are a base tile plus a flat water strip 256–384 m across that completes it, and the strip
/// falls under <see cref="ClassifyGroundMesh"/>'s 0.4-cell floor. Dropped from the scan, it left a
/// hole its own width in every copy: sky, no collision, wide enough to fly through, outward
/// forever. <see cref="AdoptComplements"/> is the fix — a cell short of ground adopts the
/// full-cell-spanning flat strips the classifier refused. ⚠ It is keyed on the CELL being short,
/// never on the strip alone: flatness by itself adopts hangar floors and city-block rooftops
/// (<c>cblock*</c>, the city ground texture, classifies as <c>buildings</c>, so the surface class
/// cannot separate them either). <c>--dump-tilegrid</c> is the census that settled all of this.</para>
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

    // Window radius in cells around the focus. Sized to cover the weather fog-far
    // (C1 zone2: 4000 m ≈ 4 × 1024 m tiles) plus one margin ring, so the fog border never
    // creeps onto the void even mid-cell. TUNE. ⚠ It used to say "regardless of the
    // fogRangeFactor TUNE" — that factor halved the range and is deleted (B15, 2026-08-08), so
    // this ring now sits against the FULL authored far, which is what it was already sized for.
    // The chapter with the longest authored far is C1B at 4,700 m, still inside 5 × 1024 m.
    private const int Rings = 5;

    // The ground-tile span gate, as fractions of a cell: the floor admits the split half-tiles,
    // the ceiling keeps multi-cell sheets out. ⚠ Named, not inlined, because they are the numbers
    // `BL-316` is about — a real C5 border strip falls under the floor and is dropped from the
    // continuation. Do not retune them without reading the --dump-tilegrid census first: lowering
    // the floor far enough to admit a strip also admits every prop mesh at the border, and
    // BuildCell would then copy those, which the design forbids.
    private const float MinSpanFraction = 0.4f;
    private const float MaxSpanFraction = 1.35f;

    // Tile-grid overlay palette, indexed by repetition-band parity: [0] both axes on an even band,
    // [1] x odd, [2] z odd, [3] both odd. Four strongly separated hues, because at TintStrength a
    // subtle palette is a grey smear over textured ground. In-map cells take InMapTint instead, so
    // the map boundary itself is never in doubt.
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
    // Flat sheets the classifier rejected for being thin, kept per cell as candidates for
    // AdoptComplements — the `BL-316` fix. Cleared once adoption has run.
    private readonly Dictionary<(int, int),
        List<(int Idx, GameZNode Node, Transform3D ParentXf, Vector3 WorldCenter, Vector3 Span)>> _spares = new();
    // Node indices adopted by AdoptComplements, so the census can say so rather than reporting
    // them as rejections that are, since the fix, nothing of the kind.
    private readonly HashSet<int> _adopted = new();
    // Per cell, the world-space X/Z bounds its ACCEPTED tiles reach between them. ⚠ A union of
    // AABBs, not a true union of footprints: two disjoint tiles that between them touch all four
    // sides read as covering, which no chapter does (measured, all eight).
    private readonly Dictionary<(int, int), (float MinX, float MaxX, float MinZ, float MaxZ)> _coverage = new();
    private readonly IReadOnlyList<ClutterBuilder.KindExport>? _clutter;

    private readonly Dictionary<(int, int), Node3D> _live = new();
    // The focus cells the current window was built around — one per player (splitscreen serves
    // every pane from one window). Empty until the first Update.
    private readonly List<(int, int)> _centerCells = new();

    // --dump-tilegrid's raw material: one row per node ScanTiles CONSIDERED — every Object3d with
    // a mesh it reached, accepted or rejected, with the reason. Null unless the census was asked
    // for. This is the only view in which a rejection is visible: the tile-grid overlay tints the
    // accepted set, so what it does NOT paint is exactly what the continuation will be missing.
    private List<CensusCandidate>? _census;

    private int _blockCells = 1;
    private bool _repeat = true;
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

    /// <summary>Why <see cref="ClassifyGroundMesh"/> did, or did not, take a node as a ground tile.
    /// Recorded per candidate by the <c>--dump-tilegrid</c> census — the tile-grid overlay shows
    /// only the accepted set, so a rejection is invisible at the controls except as the void it
    /// leaves past the map edge.</summary>
    public enum TileVerdict
    {
        /// <summary>Binned into its cell and copied by every extension cell that folds to it.</summary>
        Accepted,

        /// <summary>Not an <c>Object3d</c>, or its mesh index does not resolve, or it has no
        /// vertices — not a tile candidate at all.</summary>
        NoMesh,

        /// <summary>A polygon carries a cloud/sky texture. The cloudlayer deck tiles are cell-sized
        /// too, so this is what keeps the sky out of the ground.</summary>
        SkyOrCloud,

        /// <summary>Spans less than <see cref="MinSpanFraction"/> of the cell on X or Z. The
        /// `BL-316` suspect: a real border terrain strip, dropped for being thin.</summary>
        TooSmall,

        /// <summary>Spans more than <see cref="MaxSpanFraction"/> of the cell on X or Z — a
        /// multi-cell sheet, which the per-cell copy has no way to place.</summary>
        TooLarge,
    }

    /// <summary>Extension cells currently instantiated (diagnostics).</summary>
    public int LiveCellCount => _live.Count;

    /// <summary>How many border cells deep the repeated block is; per chapter, see
    /// <see cref="DefaultBlockCells"/>.</summary>
    public int BlockCells => _blockCells;

    /// <summary>Translate the block rather than alternately reflecting it. <b>True by default —
    /// this is what the original does</b>, A/B'd at the controls on every chapter (2026-08-08).
    /// False alternately reflects, which was the shipped behaviour until then and is now kept
    /// only to look at.</summary>
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

    /// <summary>The four band-parity colours, indexed <c>(x band odd ? 1 : 0) | (z band odd ? 2 : 0)</c>
    /// — for the overlay's legend, so a palette edit here cannot drift out of sync with the key on
    /// screen (the rule <c>ClassOverlay.BuildLegendText</c> follows).</summary>
    internal static IReadOnlyList<Color> ParityLegendColors => ParityTints;

    /// <summary>The in-map cells' legend colour. See <see cref="ParityLegendColors"/>.</summary>
    internal static Color InMapLegendColor => InMapTint;

    // ---------------------------------------------------------------- fold mapping

    /// <summary>How many border cells deep this chapter's repeated block is — all eight measured
    /// by A/B against the original at the controls (user, 2026-08-08).
    ///
    /// <para><b>C1, C2 and C4 are 2; everything else is 1.</b> C5 was read directly. The remaining
    /// four — C1B, C1C, C2B, C3 — carry <b>only water tiles at their borders</b> (measured), which
    /// both fixes them at 1 and makes the depth moot there: repeating one water cell and repeating
    /// two are the same picture.</para>
    ///
    /// <para>⚠ An unknown chapter falls back to 1 — right or indistinguishable on every chapter
    /// that exists, where 2 is right only on the three it was measured on.</para></summary>
    public static int DefaultBlockCells(string? chapter) => chapter?.ToUpperInvariant() switch
    {
        "C1" or "C2" or "C4" => 2,
        _ => 1,
    };

    /// <summary>Which repetition band an out-of-map index falls in: 0 inside the map, then ±1, ±2…
    /// outward, one step per <paramref name="block"/> cells. The tile-grid overlay colours by this
    /// rather than by <see cref="FoldAxis"/>'s flip, because <b>under repetition nothing ever
    /// flips</b> — keying the hue on the flip painted the entire continuation one colour in the
    /// mode that is now the default, and the whole point of the overlay is that one colour band is
    /// one block.
    /// <para>Under mirroring the two are the same thing (a band is odd exactly when its copy is
    /// reflected), so this changes nothing there — it is a strict generalization.</para></summary>
    public static int FoldBandIndex(int i, int n, int block)
    {
        if (i >= 0 && i < n)
            return 0;
        int b = Math.Clamp(block, 1, n);
        int t = i - (i >= n ? n - b : 0);
        // Floor division: C# truncates toward zero, and the low edge's offsets are all negative.
        return t >= 0 ? t / b : ((t + 1) / b) - 1;
    }

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

    /// <summary>The ground-tile decision, as a pure function of ONE mesh — its vertices, its
    /// polygons' texture names and the cell size. Extracted from <see cref="IsGroundTile"/> so it
    /// can be pinned by test the way <see cref="FoldAxis"/> is: this classifier decides what the
    /// world looks like past the map edge, and until `BL-316` no test could reach it.
    ///
    /// <para><b>What a rejection costs.</b> A rejected node is absent from the per-cell tile map,
    /// so <see cref="BuildCell"/> never copies it and every extension copy of its cell carries a
    /// hole exactly its footprint — outward, forever, with no collision. That is `BL-316` on C5.
    /// The verdict is returned rather than a bool precisely so the <c>--dump-tilegrid</c> census
    /// can say WHY, which the accept-only tile-grid overlay cannot.</para>
    ///
    /// <para><paramref name="center"/> is the mesh AABB centroid in node-local space and
    /// <paramref name="span"/> its full extent in metres — both filled on every verdict except
    /// <see cref="TileVerdict.NoMesh"/>, so a rejected candidate still reports its footprint. The Y
    /// extent is carried for the census's benefit, not the decision's: a dropped ground strip is
    /// flat and a dropped lamp post is not, which is the distinction a span floor cannot make.</para></summary>
    public static TileVerdict ClassifyGroundMesh(IReadOnlyList<Vector3> vertices,
        IEnumerable<string?> polygonTextures, float tileX, float tileZ,
        out Vector3 center, out Vector3 span)
    {
        center = Vector3.Zero;
        span = Vector3.Zero;
        if (vertices.Count == 0)
        {
            return TileVerdict.NoMesh;
        }
        Vector3 min = vertices[0], max = vertices[0];
        foreach (var v in vertices)
        {
            min = min.Min(v);
            max = max.Max(v);
        }
        center = (min + max) * 0.5f;
        span = max - min;
        float spanX = span.X, spanZ = span.Z;
        // Before the span gate, as it always was: the cloudlayer deck tiles are cell-sized too, so
        // size alone cannot tell them from ground. Node names are unreliable here (cloud layers
        // ship under generic names like 'g27517') — WorldBuilder.IsCloudOrSkyTexture is the rule.
        foreach (var tex in polygonTextures)
        {
            if (tex != null && WorldBuilder.IsCloudOrSkyTexture(tex))
            {
                return TileVerdict.SkyOrCloud;
            }
        }
        if (spanX < MinSpanFraction * tileX || spanZ < MinSpanFraction * tileZ)
        {
            return TileVerdict.TooSmall;
        }
        if (spanX > MaxSpanFraction * tileX || spanZ > MaxSpanFraction * tileZ)
        {
            return TileVerdict.TooLarge;
        }
        return TileVerdict.Accepted;
    }

    /// <summary>Whether a mesh the classifier dropped for being thin looks like a strip of ground
    /// that COMPLETES a cell — the `BL-316` shape. Two conditions, and both are needed:
    ///
    /// <para><b>Flat</b> — shorter than it is wide on both horizontal axes, which separates a strip
    /// of ground from an upright object without a size threshold. C5's void-causing water strips
    /// measure 0.0 m tall over 256–1024 m of ground; <c>ap_lightpole.flt</c> is tens of metres over
    /// a footprint of centimetres. Strict, so a degenerate 0x0x0 marker gizmo is not a sheet.</para>
    ///
    /// <para><b>Full-cell on one axis</b> — a strip that fills a cell's gap runs the whole way
    /// across it; a building floor, roof or awning does not. ⚠ This is the condition that carries
    /// the fix, and it was NOT obvious: flatness alone adopted 89 nodes on C5, most of them hangar
    /// floors and city-block rooftops, because <c>cblock*</c> — the city GROUND texture — classifies
    /// as <c>buildings</c>, so the surface class cannot separate them either. Measured across all
    /// eight chapters: every real completion strip spans a full cell on one axis, and no building
    /// part reaches 0.63 of one.</para>
    ///
    /// <para>A qualifying mesh is a CANDIDATE for <see cref="AdoptComplements"/>, never an
    /// acceptance on its own — whether a strip is owed a copy depends on whether its cell is short
    /// of ground, which one mesh cannot say.</para></summary>
    public static bool IsCompletionStrip(Vector3 span, float tileX, float tileZ) =>
        span.Y < span.X && span.Y < span.Z
        && (span.X >= 0.999f * tileX || span.Z >= 0.999f * tileZ);

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
    /// <param name="blockCells">Initial <see cref="BlockCells"/> — normally
    /// <see cref="DefaultBlockCells"/> for the chapter, overridden by <c>--map-edge-block=</c>.
    /// Clamped to the grid, so the caller may pass anything.</param>
    /// <param name="repeat">Initial <see cref="RepeatInsteadOfMirror"/>; true is the original's
    /// behaviour, <c>--map-edge-mode=mirror</c> is what turns it off.</param>
    /// <param name="census">Record every tile candidate and its <see cref="TileVerdict"/> for
    /// <see cref="WriteCensus"/> (<c>--dump-tilegrid</c>). Off in a normal session: it is a few
    /// thousand rows nothing would read.</param>
    internal static MapEdgeExtender? Create(GameZ gamez, SceneBuilder scene, GameZNode world,
        ClutterBuilder? clutter, int blockCells = 1, bool repeat = true, bool census = false)
    {
        if (!world.HasArea || world.PartitionCols <= 0 || world.PartitionRows <= 0
            || world.AreaRight <= world.AreaLeft || world.AreaBottom <= world.AreaTop)
            return null;
        var ext = new MapEdgeExtender(gamez, scene, world, clutter?.ExportedKinds);
        ext._blockCells = Math.Clamp(blockCells, 1, ext.MaxBlockCells);
        ext._repeat = repeat;
        if (census)
        {
            ext._census = new List<CensusCandidate>();
        }
        ext.ScanTiles(world);
        ext.AdoptComplements();
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

    /// <summary>The <c>--dump-tilegrid</c> report (`BL-316`): every tile candidate with its
    /// <see cref="TileVerdict"/>, plus a per-cell roll-up, as indented JSON.
    ///
    /// <para><b>It answers three questions at once</b>, deliberately, because the walk visits every
    /// node either way and a second run costs a rebuild: a cell with NO accepted tile is a missing
    /// cell; an accepted tile with <c>coverX</c>/<c>coverZ</c> below 1 is a short tile, whose copies
    /// leave a sliver of void at every step; and an accepted cell that ALSO has a rejected
    /// candidate is partial coverage — the copy builds something, just not all of it. The third is
    /// what C5 shows, and the only view that can see it.</para>
    ///
    /// <para><c>suspect</c> marks a border cell — inside the current <see cref="BlockCells"/> band,
    /// the only cells the continuation ever copies — that is missing, short or partial. A
    /// non-border cell can be all three harmlessly: the real world builds it and nothing repeats
    /// it.</para></summary>
    /// <param name="chapter">Stamped into the report, so eight files stay tellable apart.</param>
    /// <returns>The report text, or null when the extender was not built with a census.</returns>
    internal string? WriteCensus(string? chapter) => BuildCensusReport(chapter);

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
                // zoneGate: the mirrored tile is a copy of a real world tile, so it inherits that
                // tile's own zone_id and is culled with it (B12). Without this, the base map's
                // ground would vanish above the deck while its mirrored continuation kept
                // drawing — a seam the gate would have created on its own.
                var leaf = _scene.BuildSubtree(node, skip: n => !ReferenceEquals(n, node),
                    zoneGate: true);
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

    /// <summary>The tile-grid colour for a cell. Hue is the <b>repetition band</b>'s parity
    /// (<see cref="FoldBandIndex"/>), so one run of a single hue spans exactly
    /// <see cref="BlockCells"/> cells and the <b>width of a colour band reads off the block depth
    /// directly</b>; the two axes band independently so corner regions stay unambiguous. Value
    /// alternates on a per-cell checker inside the band, which is what turns "wide-ish" into a
    /// countable number of cells. In-map cells take a neutral tint so the map boundary is never in
    /// doubt.
    /// <para>⚠ Keyed on the band, <b>not</b> on <see cref="FoldAxis"/>'s flip. Under repetition —
    /// the default — nothing ever flips, and keying on the flip painted the whole continuation one
    /// colour, destroying the only thing the overlay is for.</para></summary>
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
            int bx = FoldBandIndex(ix, _cols, _blockCells);
            int bz = FoldBandIndex(iz, _rows, _blockCells);
            baseColor = ParityTints[(int)((uint)bx & 1) | ((int)((uint)bz & 1) << 1)];
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
            var verdict = ClassifyNode(node, out var center, out var span);
            if (verdict == TileVerdict.Accepted)
            {
                var worldCenter = xf * center;
                int cx = Mathf.Clamp(Mathf.FloorToInt((worldCenter.X - _x0) / _tileX), 0, _cols - 1);
                int cz = Mathf.Clamp(Mathf.FloorToInt((worldCenter.Z - _z0) / _tileZ), 0, _rows - 1);
                if (!_tiles.TryGetValue((cx, cz), out var list))
                    _tiles[(cx, cz)] = list = new List<(GameZNode, Transform3D)>();
                list.Add((node, parentXf));
                _tileCell[idx] = (cx, cz);
                AccumulateCoverage((cx, cz), worldCenter, span);
            }
            else if (verdict == TileVerdict.TooSmall && IsCompletionStrip(span, _tileX, _tileZ))
            {
                // A dropped strip of ground is the `BL-316` shape: a piece of terrain the
                // classifier refuses for being thin, whose absence is a hole in every copy. Held
                // here rather than accepted outright — whether it is owed a copy depends on its
                // cell, which is not known until the whole scan has run (AdoptComplements).
                var worldCenter = xf * center;
                int cx = Mathf.Clamp(Mathf.FloorToInt((worldCenter.X - _x0) / _tileX), 0, _cols - 1);
                int cz = Mathf.Clamp(Mathf.FloorToInt((worldCenter.Z - _z0) / _tileZ), 0, _rows - 1);
                if (!_spares.TryGetValue((cx, cz), out var spares))
                {
                    _spares[(cx, cz)] = spares = new List<(int, GameZNode, Transform3D, Vector3, Vector3)>();
                }
                spares.Add((idx, node, parentXf, worldCenter, span));
            }
            if (_census != null && verdict != TileVerdict.NoMesh)
            {
                RecordCandidate(idx, node, xf * center, verdict, span);
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

    // Widens a cell's accepted-coverage box by one tile's world-space footprint.
    private void AccumulateCoverage((int, int) cell, Vector3 worldCenter, Vector3 span)
    {
        float minX = worldCenter.X - (span.X * 0.5f), maxX = worldCenter.X + (span.X * 0.5f);
        float minZ = worldCenter.Z - (span.Z * 0.5f), maxZ = worldCenter.Z + (span.Z * 0.5f);
        _coverage[cell] = _coverage.TryGetValue(cell, out var c)
            ? (Math.Min(c.MinX, minX), Math.Max(c.MaxX, maxX), Math.Min(c.MinZ, minZ), Math.Max(c.MaxZ, maxZ))
            : (minX, maxX, minZ, maxZ);
    }

    // How much of a cell its ground box spans, per axis, as a fraction of the cell — the census's
    // coverX/coverZ. Clipped to the cell, so a tile overhanging its neighbour cannot report more
    // than full coverage.
    private void CoverageOf((int, int) cell, out float coverX, out float coverZ)
    {
        coverX = 0f;
        coverZ = 0f;
        if (!_coverage.TryGetValue(cell, out var c))
        {
            return;
        }
        float x0 = _x0 + (cell.Item1 * _tileX), z0 = _z0 + (cell.Item2 * _tileZ);
        coverX = Math.Max(0f, Math.Min(x0 + _tileX, c.MaxX) - Math.Max(x0, c.MinX)) / _tileX;
        coverZ = Math.Max(0f, Math.Min(z0 + _tileZ, c.MaxZ) - Math.Max(z0, c.MinZ)) / _tileZ;
    }

    // Whether a cell's accepted tiles reach all four of its sides. A metre of slack, because the
    // spans are float differences of authored vertices and an exactly-cell-sized tile lands a hair
    // either side of its bound.
    private bool CellIsCovered((int, int) cell)
    {
        if (!_coverage.TryGetValue(cell, out var c))
        {
            return false;
        }
        const float Slack = 1f;
        float x0 = _x0 + (cell.Item1 * _tileX), z0 = _z0 + (cell.Item2 * _tileZ);
        return c.MinX <= x0 + Slack && c.MaxX >= x0 + _tileX - Slack
            && c.MinZ <= z0 + Slack && c.MaxZ >= z0 + _tileZ - Slack;
    }

    // `BL-316`. A cell short of ground adopts the flat sheets the classifier dropped for being
    // thin: without this, every extension copy of that cell carries a hole exactly the dropped
    // sheet's footprint — sky, no collision, wide enough to fly through, outward forever.
    //
    // ⚠ The coverage test, not the sheet, is what decides. A dropped sheet in a cell the accepted
    // tiles ALREADY span is not a hole and is not adopted — on C5 that is the two bridge/pier
    // pieces at (9,0) and (10,0), which would otherwise repeat outward into open sea. Adoption is
    // deliberately NOT a widening of ClassifyGroundMesh: the classifier still answers "is this a
    // tile", and this answers the different question "is this cell missing ground".
    private void AdoptComplements()
    {
        foreach (var (cell, spares) in _spares)
        {
            if (CellIsCovered(cell))
            {
                continue;
            }
            if (!_tiles.TryGetValue(cell, out var list))
            {
                _tiles[cell] = list = new List<(GameZNode, Transform3D)>();
            }
            foreach (var spare in spares)
            {
                list.Add((spare.Node, spare.ParentXf));
                _tileCell[spare.Idx] = cell;
                _adopted.Add(spare.Idx);
                AccumulateCoverage(cell, spare.WorldCenter, spare.Span);
                Log.Info("world", $"map edge: cell ({cell.Item1},{cell.Item2}) is short of ground — adopting dropped sheet '{spare.Node.Name}' ({spare.Span.X:F0} x {spare.Span.Z:F0} m). BL-316.");
            }
        }
        _spares.Clear();
    }

    // A ground tile: an Object3d whose own mesh is a single ~one-cell terrain/water sheet
    // (the regular 1024-unit tiles plus split half-tiles), NOT the cloudlayer deck / sky
    // sheets (one-cell too). `center` = the mesh AABB centroid in node-local space.
    private bool IsGroundTile(GameZNode n, out Vector3 center) =>
        ClassifyNode(n, out center, out _) == TileVerdict.Accepted;

    // The node-level half of the classifier: the Object3d requirement and the mesh lookup that
    // ClassifyGroundMesh is deliberately free of, so the decision itself stays a pure function.
    private TileVerdict ClassifyNode(GameZNode n, out Vector3 center, out Vector3 span)
    {
        center = Vector3.Zero;
        span = Vector3.Zero;
        if (n.Kind != "Object3d" || n.MeshIndex < 0 || n.MeshIndex >= _gamez.Meshes.Count)
            return TileVerdict.NoMesh;
        var mesh = _gamez.Meshes[n.MeshIndex];
        return ClassifyGroundMesh(mesh.Vertices, PolygonTextures(mesh), _tileX, _tileZ,
            out center, out span);
    }

    // One texture name per polygon, null where the material index does not resolve — the shape
    // ClassifyGroundMesh and the census both read, so neither re-walks the material table.
    private IEnumerable<string?> PolygonTextures(GameZMesh mesh)
    {
        foreach (var poly in mesh.Polygons)
        {
            yield return poly.MaterialIndex >= 0 && poly.MaterialIndex < _gamez.Materials.Count
                ? _gamez.Materials[poly.MaterialIndex].TextureName
                : null;
        }
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

    // ---------------------------------------------------------------- census (--dump-tilegrid)

    // One census row, taken at the moment ScanTiles decided. The cell is the CLAMPED bin the scan
    // itself uses, so a candidate whose centre falls outside the grid reports the cell it would
    // have been forced into rather than an index no cell can hold.
    private void RecordCandidate(int idx, GameZNode node, Vector3 worldCenter, TileVerdict verdict,
        Vector3 span)
    {
        var textures = new SortedSet<string>(StringComparer.Ordinal);
        var surfaces = new SortedSet<string>(StringComparer.Ordinal);
        if (node.MeshIndex >= 0 && node.MeshIndex < _gamez.Meshes.Count)
        {
            foreach (var tex in PolygonTextures(_gamez.Meshes[node.MeshIndex]))
            {
                if (tex == null)
                {
                    continue;
                }
                textures.Add(tex);
                surfaces.Add(SceneBuilder.ClassifySurface(tex) ?? "default");
            }
        }
        _census!.Add(new CensusCandidate
        {
            Node = idx,
            Name = node.Name,
            Verdict = verdict.ToString(),
            Cx = Mathf.Clamp(Mathf.FloorToInt((worldCenter.X - _x0) / _tileX), 0, _cols - 1),
            Cz = Mathf.Clamp(Mathf.FloorToInt((worldCenter.Z - _z0) / _tileZ), 0, _rows - 1),
            CoverX = span.X / _tileX,
            CoverZ = span.Z / _tileZ,
            SpanX = span.X,
            SpanY = span.Y,
            SpanZ = span.Z,
            Textures = string.Join(" ", textures),
            Surfaces = string.Join(" ", surfaces),
        });
    }

    // The report itself, down here with the other census members; WriteCensus up top is the
    // documented entry point.
    private string? BuildCensusReport(string? chapter)
    {
        if (_census == null)
        {
            return null;
        }
        var accepted = new Dictionary<(int, int), int>();
        var rejected = new Dictionary<(int, int), List<string>>();
        var adoptions = new List<string>();
        foreach (var row in _census)
        {
            var key = (row.Cx, row.Cz);
            // An adopted sheet is reported as what it now is — part of the cell's ground — with
            // the classifier's original verdict kept in the row, so the fix stays legible.
            if (row.Verdict == nameof(TileVerdict.Accepted) || _adopted.Contains(row.Node))
            {
                accepted.TryGetValue(key, out int count);
                accepted[key] = count + 1;
            }
            else
            {
                if (!rejected.TryGetValue(key, out var list))
                {
                    rejected[key] = list = new List<string>();
                }
                list.Add($"{row.Verdict} {row.Name} {row.CoverX:F3}x{row.CoverZ:F3} [{row.Surfaces}]");
            }
            if (_adopted.Contains(row.Node))
            {
                adoptions.Add($"({row.Cx},{row.Cz}) {row.Name} {row.CoverX:F3}x{row.CoverZ:F3} [{row.Surfaces}]");
            }
        }
        var cells = new List<object>();
        int suspects = 0;
        for (int cz = 0; cz < _rows; cz++)
        {
            for (int cx = 0; cx < _cols; cx++)
            {
                var key = (cx, cz);
                bool border = cx < _blockCells || cx >= _cols - _blockCells
                    || cz < _blockCells || cz >= _rows - _blockCells;
                accepted.TryGetValue(key, out int count);
                rejected.TryGetValue(key, out var r);
                // Coverage is read off the cell's accumulated ground box, NOT off the widest single
                // tile: a cell's ground is routinely two split half-tiles that each span half of it
                // and together span all of it, and a per-tile maximum reports those as holes.
                CoverageOf(key, out float coverX, out float coverZ);
                bool covered = coverX >= 0.999f && coverZ >= 0.999f;
                bool suspect = border && (count == 0 || !covered);
                if (suspect)
                {
                    suspects++;
                }
                cells.Add(new
                {
                    cx,
                    cz,
                    border,
                    accepted = count,
                    coverX,
                    coverZ,
                    rejected = r?.Count ?? 0,
                    suspect,
                    why = r == null ? string.Empty : string.Join(" | ", r),
                });
            }
        }
        var report = new
        {
            chapter = chapter ?? string.Empty,
            grid = new { cols = _cols, rows = _rows, tileX = _tileX, tileZ = _tileZ, x0 = _x0, z0 = _z0 },
            blockCells = _blockCells,
            repeat = _repeat,
            candidateCount = _census.Count,
            acceptedCount = accepted.Count,
            adoptedCount = _adopted.Count,
            adopted = adoptions,
            suspectCells = suspects,
            cells,
            candidates = _census,
        };
        return JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    }

    // A census row. A class rather than a tuple because it is serialized straight to JSON, and the
    // property names ARE the report's column names.
    private sealed class CensusCandidate
    {
        /// <summary>The gamez node index — the same stamp the built scene carries as
        /// <c>AnimRuntime.IndexMeta</c>, so a row can be tied back to what is on screen.</summary>
        public int Node { get; init; }

        /// <summary>The gamez node name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary><see cref="TileVerdict"/> as text.</summary>
        public string Verdict { get; init; } = string.Empty;

        /// <summary>Grid column it binned into (clamped to the grid).</summary>
        public int Cx { get; init; }

        /// <summary>Grid row it binned into (clamped to the grid).</summary>
        public int Cz { get; init; }

        /// <summary>X span as a fraction of the cell — 1.0 is a tile that spans its cell exactly,
        /// and anything under <see cref="MinSpanFraction"/> is why a candidate was dropped.</summary>
        public float CoverX { get; init; }

        /// <summary>Z span as a fraction of the cell.</summary>
        public float CoverZ { get; init; }

        /// <summary>X span in metres.</summary>
        public float SpanX { get; init; }

        /// <summary>Y span in metres — the flat/upright test. A dropped ground strip is a sheet
        /// (a few metres of terrain relief); a dropped lamp post is tens of metres tall on a
        /// footprint of centimetres.</summary>
        public float SpanY { get; init; }

        /// <summary>Z span in metres.</summary>
        public float SpanZ { get; init; }

        /// <summary>Distinct texture names on its polygons, space-separated.</summary>
        public string Textures { get; init; } = string.Empty;

        /// <summary>Distinct <c>SceneBuilder.ClassifySurface</c> results, space-separated — the
        /// field that decides whether a rejected candidate is terrain that SHOULD be copied or a
        /// prop that must not be.</summary>
        public string Surfaces { get; init; } = string.Empty;
    }
}
