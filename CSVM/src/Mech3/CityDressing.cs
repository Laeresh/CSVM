using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The low dressing on the Enhanced city's block floor (<see cref="CityGround"/>), drawn while <see cref="CityModels.On"/>
/// and never solid. Each floor variant's layout gives its slots: walls and fences on its lines, hedges and trees in its
/// gardens and lawns. Sheds, cars and ash cans stand in its yards, courts and alley, benches in its gardens. A block
/// stands the slots of the variant the world shader draws there (<see cref="CityGround.Pick"/>), each kept by a roll of
/// its block. The <c>gd_</c> kinds draw through <see cref="CityModels"/>' near material, the tree through
/// <see cref="VegetationModels"/>', placed and built on the first follow with the city on (docs/architecture/Mech3.md).
/// </summary>
public sealed class CityDressing
{
    /// <summary>TUNE. The edge of the square cells the instances are cut into, one node per cell and mesh. Every node
    /// costs cull time whether drawn or not, and the fades are short, so a cell holds a few blocks.</summary>
    public const float CellM = 512f;

    /// <summary>The tree's species name, read through <see cref="VegetationModels.ForMean"/>.</summary>
    public const string Tree = "citytree";

    // Metres per layout unit: one floor tile.
    private const float TileM = 256f;

    // TUNE. The keep shares of a slot per block: hedge runs (a whole garden edge), trees, sheds, cars, ash cans, benches.
    private const float HedgeKeep = 0.5f, TreeKeep = 0.6f, ShedKeep = 0.4f, CarKeep = 0.25f, CansKeep = 0.35f, BenchKeep = 0.4f;

    // TUNE. Slot spacing: trees on a jittered grid, cars along a parking side.
    private const float TreeGridM = 10f, TreeJitterM = 2.5f, TreeSpacingM = 7f, CarStepM = 7f;

    // TUNE. A tree trunk stands this far from every lot, so a crown of about 5 m clears the buildings.
    private const float TreeLotM = 5.5f;

    // TUNE. The share of car slots that roll a truck rather than a sedan.
    private const float TruckShare = 0.2f;

    // TUNE. A hedge leaves a garden edge that a fence or wall already runs along, or a path crosses.
    private const float HedgeLineM = 1.2f, HedgePathM = 1.0f;

    // TUNE. Each kind: its fade start and end in metres (under the clutter fade scale), and its footprint half extents
    // for the clearance tests. A line kind adds the run it lays end to end. Every fade ends inside the
    // Near band, so only near meshes draw; bench and ash cans fade earliest.
    private static readonly Kind[] KindTable =
    {
        new("gd_wall", 260f, 350f, 4f, 0.3f, 8f),
        new("gd_fence", 220f, 300f, 4f, 0.15f, 8f),
        new("gd_hedge", 220f, 300f, 1.5f, 0.4f, 3f),
        new("gd_shed", 260f, 350f, 1.7f, 1.4f),
        new("gd_sedan", 220f, 300f, 2.2f, 0.9f),
        new("gd_truck", 260f, 350f, 2.75f, 1.0f),
        new("gd_ashcans", 110f, 150f, 0.65f, 0.65f),
        new("gd_bench", 110f, 150f, 0.95f, 0.4f),
        new(Tree, 330f, 450f, 0.6f, 0.6f),
    };

    private readonly GameZ _gamez;
    private readonly CityGround _ground;
    private readonly string _chapter;
    private readonly Dictionary<string, Look> _looks;
    private bool _built;

    private CityDressing(GameZ gamez, CityGround ground, string chapter, Dictionary<string, Look> looks)
    {
        _gamez = gamez;
        _ground = ground;
        _chapter = chapter;
        _looks = looks;
    }

    /// <summary>The node every dressing cell stands under, shown exactly while <see cref="CityModels.On"/>.</summary>
    public Node3D Root { get; } = new() { Name = "city_dressing", Visible = false };

    /// <summary>The instances placed per kind, empty until the first build.</summary>
    public IReadOnlyDictionary<string, int> Counts { get; private set; } = new Dictionary<string, int>();

    /// <summary>Every instance placed, with the floor variant its block draws, empty until the first build.</summary>
    internal IReadOnlyList<Placed> Placements { get; private set; } = Array.Empty<Placed>();

    /// <summary>The kinds' models of <paramref name="chapter"/> over <paramref name="ground"/>, null with no ground or no
    /// model. Nothing is placed yet. <paramref name="textures"/> lends the facade the chapter's posters.</summary>
    public static CityDressing? Create(GameZ gamez, CityGround? ground, string chapter, Func<string, Image?>? textures)
    {
        if (ground == null)
            return null;
        var looks = new Dictionary<string, Look>(StringComparer.Ordinal);
        foreach (var kind in KindTable)
        {
            if (kind.Name == Tree)
            {
                if (VegetationModels.ForMean(chapter, Tree, null) is { } model)
                    looks[Tree] = new Look(kind, new Mesh[] { model.Mesh }, VegetationModels.Material(model, lit: true, fogged: true, cardWidth: 9f));
            }
            else if (CityModels.For(chapter, kind.Name, textures) is { Near.Count: > 0 } hull)
            {
                looks[kind.Name] = new Look(kind, hull.Near.Cast<Mesh>().ToArray(), hull.NearMaterial);
            }
        }
        if (looks.Count == 0)
            return null;
        Log.Info("world", $"city dressing: models for {string.Join(", ", looks.Keys)}");
        return new CityDressing(gamez, ground, chapter, looks);
    }

    /// <summary>Shows the dressing exactly while <see cref="CityModels.On"/>, placing and building it the first time, and
    /// puts the standing 3D Scenery band on its materials.</summary>
    public void Follow()
    {
        bool on = CityModels.On;
        if (on && !_built)
            Build();
        Root.Visible = on;
        foreach (var look in _looks.Values)
        {
            var material = (ShaderMaterial)look.Material;
            material.SetShaderParameter(VegetationModels.BandParam, VegetationModels.Band);
            // Plain metres, as the gd_ kinds fade: no 3D Scenery word pushes the tree out.
            if (look.Kind.Name == Tree)
                material.SetShaderParameter(VegetationModels.ReachParam, 1f);
        }
    }

    /// <summary>The layout of one floor variant, a file beside its image.</summary>
    internal static VariantLayout ParseVariant(string json)
    {
        var layout = new VariantLayout();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        foreach (var cell in root.GetProperty("cells").EnumerateArray())
        {
            var r = cell.GetProperty("rect").EnumerateArray().Select(v => v.GetSingle()).ToArray();
            layout.Cells.Add(new Cell(cell.GetProperty("kind").GetString() ?? "", cell.GetProperty("region").GetString() ?? "",
                new Rect2(r[0], r[1], r[2] - r[0], r[3] - r[1])));
        }
        foreach (var line in root.GetProperty("lines").EnumerateArray())
            layout.Lines.Add((line.GetProperty("kind").GetString() ?? "", Points(line.GetProperty("line"))));
        if (root.TryGetProperty("paths", out var paths))
        {
            foreach (var path in paths.EnumerateArray())
                layout.Paths.Add(Points(path.GetProperty("line")));
        }
        return layout;
    }

    /// <summary>The lot footprints of building set <paramref name="set"/> in <c>ground_layout.json</c>, convex polygons
    /// in texture UV. A set with no template (<c>none</c>) has none.</summary>
    internal static List<Vector2[]> Lots(string groundLayoutJson, string set)
    {
        var lots = new List<Vector2[]>();
        using var doc = JsonDocument.Parse(groundLayoutJson);
        if (!doc.RootElement.GetProperty("templates").TryGetProperty(set, out var template))
            return lots;
        foreach (var lot in template.GetProperty("lots").EnumerateArray())
        {
            foreach (var poly in lot.GetProperty("poly").EnumerateArray())
                lots.Add(Points(poly));
        }
        return lots;
    }

    /// <summary>The slots of one variant in its layout's frame, each inside a layout cell. Each is clear of the streets,
    /// every slot before it and the lots, each lot tested again a tile over.</summary>
    internal static List<Slot> Slots(VariantLayout layout, IReadOnlyList<Vector2[]> lots, CityGround.PaintLayout spec)
    {
        var free = new Ground(layout, lots, spec);
        var slots = new List<Slot>();
        var taken = new List<(Vector2 At, float R)>();
        int roll = 0;

        // Walls and fences stand on every line, laid end to end.
        foreach (var (kind, points) in layout.Lines)
        {
            if (Find("gd_" + kind) is not { } k)
                continue;
            bool flip = (Mix((uint)roll * 0x9E3779B1u) & 1u) == 1u;
            for (int s = 0; s + 1 < points.Length; s++)
            {
                foreach (var (at, yaw, stretch) in Run(points[s], points[s + 1], k.RunM))
                {
                    if (free.Clear(at, yaw, k.HalfLength * stretch * 0.9f, 0f, line: true))
                        slots.Add(new Slot(k.Name, at, flip ? yaw + MathF.PI : yaw, stretch, roll, 1f));
                }
            }
            roll++;
        }

        // Hedges round gardens and lawns where no fence, wall or path is, a whole edge kept or not.
        var hedge = Find("gd_hedge")!;
        foreach (var cell in layout.Cells.Where(c => c.Kind is "garden" or "lawn"))
        {
            var r = cell.Rect.Grow(-0.5f / TileM);
            var corners = new[] { r.Position, new Vector2(r.End.X, r.Position.Y), r.End, new Vector2(r.Position.X, r.End.Y) };
            for (int e = 0; e < 4; e++, roll++)
            {
                foreach (var (at, yaw, stretch) in Run(corners[e], corners[(e + 1) % 4], hedge.RunM))
                {
                    if (free.LineDistance(at) >= HedgeLineM / TileM && free.PathDistance(at) >= HedgePathM / TileM
                        && free.TopKind(at) is "garden" or "lawn" or "bed"
                        && free.Clear(at, yaw, hedge.HalfLength * stretch, hedge.HalfDepth, line: false))
                    {
                        slots.Add(new Slot(hedge.Name, at, yaw, stretch, roll, HedgeKeep));
                        taken.Add((at, hedge.HalfDepth / TileM));
                    }
                }
            }
        }

        // Pieces, largest first, each clear of the lines and of every piece before it.
        void Piece(Kind k, Vector2 at, float yaw, float keep, int id)
        {
            float reach = MathF.Max(k.HalfLength, k.HalfDepth) / TileM;
            if (taken.Any(t => t.At.DistanceTo(at) < t.R + reach) || !free.Clear(at, yaw, k.HalfLength, k.HalfDepth, line: false)
                || free.PathDistance(at) < (k.HalfDepth + 0.3f) / TileM || free.LineDistance(at) < (k.HalfDepth + 0.15f) / TileM)
                return;
            taken.Add((at, reach));
            slots.Add(new Slot(k.Name, at, yaw, 1f, id, keep));
        }
        var shed = Find("gd_shed")!;
        var sedan = Find("gd_sedan")!;
        var truck = Find("gd_truck")!;
        var cans = Find("gd_ashcans")!;
        var bench = Find("gd_bench")!;
        for (int c = 0; c < layout.Cells.Count; c++)
        {
            var cell = layout.Cells[c];
            var r = cell.Rect;
            float w = r.Size.X * TileM, h = r.Size.Y * TileM;
            bool alongU = w >= h;
            float yaw = alongU ? 0f : -MathF.PI / 2f;
            if (cell.Region == "yard" && cell.Kind != "bed" && w * h >= 60f && free.TopCell(r.GetCenter()) == c)
                Piece(shed, Inside(r, shed, alongU, Unit(c, 1), Unit(c, 2)), yaw, ShedKeep, roll++);
            if (cell.Kind is "asphalt" or "concrete" or "gravel" or "alley" && cell.Region is "core" or "yard" or "alley")
            {
                float length = (alongU ? w : h) - 2f;
                for (float along = 4f; along + 3f <= length; along += CarStepM, roll++)
                {
                    bool isTruck = Unit(roll, 3) < TruckShare;
                    var k = isTruck ? truck : sedan;
                    float side = Unit(roll, 4) < 0.5f ? 1.6f : (alongU ? h : w) - 1.6f;
                    var at = alongU ? new Vector2(r.Position.X + ((along + 1f) / TileM), r.Position.Y + (side / TileM))
                        : new Vector2(r.Position.X + (side / TileM), r.Position.Y + ((along + 1f) / TileM));
                    float turn = Unit(roll, 5) < 0.5f ? 0f : MathF.PI;
                    Piece(k, at, yaw + turn + ((Unit(roll, 6) - 0.5f) * 0.1f), CarKeep, roll);
                }
            }
            if (cell.Kind is "concrete" or "asphalt" or "gravel" or "flagstone" or "dirt" or "alley" && cell.Region is "yard" or "apron" or "core" or "alley")
                Piece(cans, Corner(r, 0.9f, Unit(c, 7)), yaw, CansKeep, roll++);
            if (cell.Kind is "garden" or "lawn" && w >= 6f && h >= 6f)
            {
                float across = Unit(c, 8) < 0.5f ? 2.2f : (alongU ? h : w) - 2.2f;
                var at = alongU ? new Vector2(r.GetCenter().X, r.Position.Y + (across / TileM)) : new Vector2(r.Position.X + (across / TileM), r.GetCenter().Y);
                // A bench faces +Z, so it turns to face into the garden from the edge it stands by.
                bool low = across < 3f;
                Piece(bench, at, yaw + (low == alongU ? 0f : MathF.PI), BenchKeep, roll++);
            }
        }

        // Trees in gardens and lawns on a jittered grid, clear of the lots by a crown.
        var tree = Find(Tree)!;
        var trees = new List<Vector2>();
        for (int c = 0; c < layout.Cells.Count; c++)
        {
            var cell = layout.Cells[c];
            if (cell.Kind is not ("garden" or "lawn"))
                continue;
            var r = cell.Rect.Grow(-2f / TileM);
            for (float y = r.Position.Y; y <= r.End.Y; y += TreeGridM / TileM)
            {
                for (float x = r.Position.X; x <= r.End.X; x += TreeGridM / TileM, roll++)
                {
                    var at = new Vector2(x + ((Unit(roll, 9) - 0.5f) * 2f * TreeJitterM / TileM), y + ((Unit(roll, 10) - 0.5f) * 2f * TreeJitterM / TileM));
                    if (!r.HasPoint(at) || free.TopKind(at) is not ("garden" or "lawn" or "bed") || free.LotDistance(at) < TreeLotM / TileM
                        || free.LineDistance(at) < 0.8f / TileM || free.PathDistance(at) < 1f / TileM
                        || trees.Any(t => t.DistanceTo(at) < TreeSpacingM / TileM) || taken.Any(t => t.At.DistanceTo(at) < t.R + (tree.HalfLength / TileM))
                        || !free.Clear(at, 0f, tree.HalfLength, tree.HalfDepth, line: false))
                        continue;
                    trees.Add(at);
                    slots.Add(new Slot(tree.Name, at, 0f, 1f, roll, TreeKeep));
                }
            }
        }
        return slots;
    }

    /// <summary>Stands the slots on one polygon placement, <paramref name="triangles"/> its world corners, three a
    /// triangle, its texture UV being world XZ over 256 plus <paramref name="offset"/>. A block takes the slots of the
    /// variant <see cref="CityGround.Pick"/> gives its cell, a slot only inside that cell and the polygon, and none
    /// <paramref name="covered"/> refuses. A slot stands by its block's roll against its keep share.</summary>
    internal static void Stand(IReadOnlyList<Vector3> triangles, Vector2 offset, CityGround.PaintLayout spec,
        IReadOnlyList<List<Slot>?> variants, Func<Vector3, bool>? covered, List<Placed> into)
    {
        if (triangles.Count < 3 || variants.Count == 0)
            return;
        float minX = triangles.Min(p => p.X), maxX = triangles.Max(p => p.X), minZ = triangles.Min(p => p.Z), maxZ = triangles.Max(p => p.Z);
        // Only the shifts whose frame, [k + 0.5, k + 1.5] in UV, meets the polygon's bounds.
        int u0 = (int)MathF.Ceiling((minX / TileM) + offset.X - 1.5f), u1 = (int)MathF.Floor((maxX / TileM) + offset.X - 0.5f);
        int v0 = (int)MathF.Ceiling((minZ / TileM) + offset.Y - 1.5f), v1 = (int)MathF.Floor((maxZ / TileM) + offset.Y - 0.5f);
        for (int ku = u0; ku <= u1; ku++)
        {
            for (int kv = v0; kv <= v1; kv++)
            {
                // The block of this tile shift: the cell at the layout frame's middle, which the slots must share.
                var mid = World(new Vector2(ku + 1f, kv + 1f), offset);
                var cell = CityGround.CellOf(spec, mid.X, mid.Y);
                uint block = CityGround.Pick(cell.I, cell.J);
                int variant = (int)(block % (uint)variants.Count);
                // ponytail: a slot off the middle's cell is dropped, so a frame straddling cells (the diagonal strips) loses it.
                if (variants[variant] is not { } slots)
                    continue;
                // The polygon's triangles over this frame alone, since a ground polygon can hold hundreds.
                var lo = World(new Vector2(ku + 0.5f, kv + 0.5f), offset);
                var hi = World(new Vector2(ku + 1.5f, kv + 1.5f), offset);
                var near = new List<Vector3>();
                for (int t = 0; t + 2 < triangles.Count; t += 3)
                {
                    Vector3 a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    if (MathF.Max(a.X, MathF.Max(b.X, c.X)) >= lo.X && MathF.Min(a.X, MathF.Min(b.X, c.X)) <= hi.X
                        && MathF.Max(a.Z, MathF.Max(b.Z, c.Z)) >= lo.Y && MathF.Min(a.Z, MathF.Min(b.Z, c.Z)) <= hi.Y)
                        near.AddRange(new[] { a, b, c });
                }
                if (near.Count == 0)
                    continue;
                foreach (var slot in slots)
                {
                    var at = World(slot.At + new Vector2(ku, kv), offset);
                    if (at.X < minX || at.X > maxX || at.Y < minZ || at.Y > maxZ || CityGround.CellOf(spec, at.X, at.Y) != cell
                        || Unit(Mix(block ^ slot.RollKey)) >= slot.Keep || Height(near, at) is not { } y)
                        continue;
                    var foot = new Vector3(at.X, y, at.Y);
                    if (covered?.Invoke(foot) == true)
                        continue;
                    into.Add(new Placed(slot.Kind, new Transform3D(slot.Turn, foot), variant, variants.Count, cell));
                }
            }
        }
    }

    // A variant's slots, read once per floor file: its layout beside the image, against its set's lots. Null where the
    // layout is missing or unreadable, so that variant's blocks stand nothing.
    private static List<Slot>? SlotsFor(string image, string set, CityGround.PaintLayout spec, string? groundJson,
        Dictionary<string, List<Slot>?> slotsOf)
    {
        if (slotsOf.TryGetValue(image, out var known))
            return known;
        string path = image.EndsWith(".png.bin", StringComparison.Ordinal) ? image[..^".png.bin".Length] + ".json" : image + ".json";
        List<Slot>? slots = null;
        try
        {
            if (groundJson != null && FileAccess.FileExists(path))
                slots = Slots(ParseVariant(FileAccess.GetFileAsString(path)), Lots(groundJson, set), spec);
            else
                Log.Warn("world", $"city dressing {path}: no layout, its blocks stand no dressing");
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            Log.Warn("world", $"city dressing {path}: {e.Message}, its blocks stand no dressing");
        }
        slotsOf[image] = slots;
        return slots;
    }

    // Every flagged textured triangle of the world, binned, which a clear polygon's dressing is kept off.
    private static Overlay Flagged(GameZ gamez)
    {
        var overlay = new Overlay();
        VegetationGround.WalkWorld(gamez, "world1", (_, mesh, xf) =>
        {
            foreach (var poly in mesh.Polygons)
            {
                if (!poly.NoClutter || poly.MaterialIndex < 0)
                    continue;
                for (int i = 0; i + 2 < poly.VertexIndices.Count; i++)
                {
                    var (a, b, c) = VegetationGround.Positions(poly, i);
                    overlay.Add(xf * mesh.Vertices[poly.VertexIndices[a]], xf * mesh.Vertices[poly.VertexIndices[b]], xf * mesh.Vertices[poly.VertexIndices[c]]);
                }
            }
        });
        return overlay;
    }

    private static Kind? Find(string name) => Array.Find(KindTable, k => k.Name == name);

    private static Vector2 World(Vector2 uv, Vector2 offset) => (uv - offset) * TileM;

    private static float? Height(IReadOnlyList<Vector3> triangles, Vector2 at)
    {
        var p = new Vector3(at.X, 0f, at.Y);
        for (int i = 0; i + 2 < triangles.Count; i += 3)
        {
            if (VegetationGround.HeightIn(triangles[i], triangles[i + 1], triangles[i + 2], p) is { } y)
                return y;
        }
        return null;
    }

    // Pieces of a run from a to b: as many as keep each within a quarter of its length, stretched to fit. A stub
    // under half a piece stands none.
    private static IEnumerable<(Vector2 At, float Yaw, float Stretch)> Run(Vector2 a, Vector2 b, float pieceM)
    {
        float length = a.DistanceTo(b) * TileM;
        if (length < pieceM * 0.5f)
            yield break;
        int n = Math.Max(1, (int)MathF.Ceiling(length / (pieceM * 1.25f)));
        var d = b - a;
        float yaw = MathF.Atan2(-d.Y, d.X);
        for (int i = 0; i < n; i++)
            yield return (a + (d * ((i + 0.5f) / n)), yaw, length / (n * pieceM));
    }

    // A piece's centre at a hashed spot inside the rect, its long axis along the rect's.
    private static Vector2 Inside(Rect2 r, Kind k, bool alongU, float s, float t)
    {
        float hx = (alongU ? k.HalfLength : k.HalfDepth) + 1f, hz = (alongU ? k.HalfDepth : k.HalfLength) + 1f;
        return new Vector2(Mathf.Lerp(r.Position.X + (hx / TileM), r.End.X - (hx / TileM), s), Mathf.Lerp(r.Position.Y + (hz / TileM), r.End.Y - (hz / TileM), t));
    }

    // One of the rect's corners, by the draw, inset by the margin in metres.
    private static Vector2 Corner(Rect2 r, float marginM, float draw)
    {
        var inner = r.Grow(-marginM / TileM);
        int q = (int)(draw * 4f) & 3;
        return new Vector2((q & 1) == 0 ? inner.Position.X : inner.End.X, (q & 2) == 0 ? inner.Position.Y : inner.End.Y);
    }

    private static Vector2[] Points(JsonElement flat)
    {
        var v = flat.EnumerateArray().Select(e => e.GetSingle()).ToArray();
        var points = new Vector2[v.Length / 2];
        for (int i = 0; i < points.Length; i++)
            points[i] = new Vector2(v[2 * i], v[(2 * i) + 1]);
        return points;
    }


    // A draw in [0, 1) from a slot's index and a salt, fixed by the layout alone.
    private static float Unit(int index, int salt) => Unit(Mix(unchecked(((uint)index * 0x9E3779B1u) ^ ((uint)salt * 0x85EBCA77u))));

    private static float Unit(uint h) => (h >> 8) * (1f / 16777216f);

    private static uint Mix(uint x)
    {
        unchecked
        {
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return x;
        }
    }

    // Places every slot on the floored polygons reached from world1 and builds one node per cell and mesh.
    private void Build()
    {
        _built = true;
        var clock = Stopwatch.StartNew();
        var placed = new List<Placed>();
        var slotsOf = new Dictionary<string, List<Slot>?>(StringComparer.Ordinal);
        string? groundJson = null;
        string layoutPath = $"{CityModels.Root}/{_chapter.ToUpperInvariant()}/ground_layout.json";
        if (FileAccess.FileExists(layoutPath))
            groundJson = FileAccess.GetFileAsString(layoutPath);
        var cover = Flagged(_gamez);
        double overlay = clock.Elapsed.TotalMilliseconds;
        VegetationGround.WalkWorld(_gamez, "world1", (_, mesh, xf) =>
        {
            foreach (var poly in mesh.Polygons)
            {
                if (_ground.FloorOf(poly) is not { } floor || poly.UvCoords is not { Count: >= 3 } uvs)
                    continue;
                var variants = floor.Files.Select(file => SlotsFor(file, floor.Set, floor.Spec, groundJson, slotsOf)).ToList();
                var corners = new List<Vector3>();
                for (int i = 0; i + 2 < poly.VertexIndices.Count; i++)
                {
                    var (a, b, c) = VegetationGround.Positions(poly, i);
                    corners.Add(xf * mesh.Vertices[poly.VertexIndices[a]]);
                    corners.Add(xf * mesh.Vertices[poly.VertexIndices[b]]);
                    corners.Add(xf * mesh.Vertices[poly.VertexIndices[c]]);
                }
                var p0 = xf * mesh.Vertices[poly.VertexIndices[0]];
                var offset = new Vector2(uvs[0].X - (p0.X / TileM), uvs[0].Y - (p0.Z / TileM));
                // The flagged layer draws over a clear one, so a clear polygon stands nothing where one lies over it.
                Stand(corners, offset, floor.Spec, variants, poly.NoClutter || !cover.Meets(corners) ? null : cover.Covers, placed);
            }
        });
        Placements = placed;
        Counts = placed.GroupBy(p => p.Kind).ToDictionary(g => g.Key, g => g.Count());
        double placing = clock.Elapsed.TotalMilliseconds;
        int nodes = Draw(placed);
        Log.Info("world", $"city dressing: {placed.Count} instances ({string.Join(", ", Counts.OrderBy(c => c.Key).Select(c => $"{c.Key} {c.Value}"))}) in {nodes} nodes; flagged ground in {overlay:0} ms, placed in {placing - overlay:0} ms, built in {clock.Elapsed.TotalMilliseconds - placing:0} ms");
    }

    // One node per cell and mesh, ranged at the kind's fade past the cell's bounds, on the models' layer, casting nothing.
    private int Draw(List<Placed> placed)
    {
        var bins = new Dictionary<(int, int, string, int), List<(Transform3D Xf, Color Custom)>>();
        foreach (var p in placed)
        {
            if (!_looks.TryGetValue(p.Kind, out var look))
                continue;
            var o = p.Transform.Origin;
            float seed = CityModels.Seed(o);
            int mesh = CityModels.VariantOf(seed, look.Meshes.Length);
            var key = ((int)MathF.Floor(o.X / CellM), (int)MathF.Floor(o.Z / CellM), p.Kind, mesh);
            if (!bins.TryGetValue(key, out var list))
                bins[key] = list = new();
            // The tree's shader mirrors by the basis' X sign and turns by its origin itself.
            var xf = p.Kind == Tree && ((uint)seed & 1u) == 1u ? new Transform3D(Basis.FromScale(new Vector3(-1f, 1f, 1f)), o) : p.Transform;
            list.Add((xf, new Color(look.Kind.Fade.X, look.Kind.Fade.Y, look.Kind.Fade.Z, seed)));
        }
        foreach (var ((_, _, kind, m), list) in bins)
        {
            var look = _looks[kind];
            // The origins' box grown by the mesh's reach, stretch included, bounds every instance however it turns.
            var meshBox = look.Meshes[m].GetAabb();
            float reach = 1.25f * MathF.Max(meshBox.Position.Length(), meshBox.End.Length());
            var bounds = new Aabb(list[0].Xf.Origin, Vector3.Zero);
            foreach (var (xf, _) in list)
                bounds = bounds.Expand(xf.Origin);
            bounds = bounds.Grow(reach);
            var centre = bounds.GetCenter();
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseCustomData = true,
                Mesh = look.Meshes[m],
                InstanceCount = list.Count,
            };
            // One upload per node: the basis by rows, each closed by an origin component, then the custom data.
            var buffer = new float[list.Count * 16];
            for (int i = 0; i < list.Count; i++)
            {
                var (b, o, c) = (list[i].Xf.Basis, list[i].Xf.Origin - centre, list[i].Custom);
                var row = buffer.AsSpan(i * 16, 16);
                (row[0], row[1], row[2], row[3]) = (b.X.X, b.Y.X, b.Z.X, o.X);
                (row[4], row[5], row[6], row[7]) = (b.X.Y, b.Y.Y, b.Z.Y, o.Y);
                (row[8], row[9], row[10], row[11]) = (b.X.Z, b.Y.Z, b.Z.Z, o.Z);
                (row[12], row[13], row[14], row[15]) = (c.R, c.G, c.B, c.A);
            }
            mm.Buffer = buffer;
            Root.AddChild(new MultiMeshInstance3D
            {
                Name = $"{kind}_{m}_{Root.GetChildCount()}",
                Multimesh = mm,
                MaterialOverride = look.Material,
                Position = centre,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Layers = VegetationModels.Layer,
                VisibilityRangeEnd = look.Kind.FadeFarM + (bounds.Size * 0.5f).Length(),
            });
        }
        return bins.Count;
    }

    /// <summary>A place a kind may stand in a variant's frame, with its yaw and stretch along X. A block draws its roll
    /// against the share of blocks that keep it.</summary>
    internal readonly record struct Slot(string Kind, Vector2 At, float Yaw, float Stretch, int Roll, float Keep)
    {
        /// <summary>Gets the turn and stretch every block stands it with.</summary>
        public Basis Turn { get; } = new Basis(Vector3.Up, Yaw) * Basis.FromScale(new Vector3(Stretch, 1f, 1f));

        /// <summary>Gets the roll's half of a block's key, the same on every block.</summary>
        public uint RollKey { get; } = Mix(unchecked((uint)Roll * 0x9E3779B1u));
    }

    /// <summary>One placed instance: its kind, world transform, the floor variant of its block of so many, and that block's cell.</summary>
    internal readonly record struct Placed(string Kind, Transform3D Transform, int Variant, int Variants, (int I, int J) Cell);

    /// <summary>A dressing kind: its model name, fade band in metres, footprint half extents and run length.</summary>
    internal sealed record Kind(string Name, float FadeNearM, float FadeFarM, float HalfLength, float HalfDepth, float RunM = 0f)
    {
        /// <summary>The fade as the clutter's custom data takes it: near², far² and their reciprocal span.</summary>
        public Vector3 Fade => ClutterKindProps.FadeThresholds(new Vector2(FadeNearM, FadeFarM), new Vector2(FadeNearM, FadeFarM), 0f);
    }

    /// <summary>One cell of a variant layout: its surface kind, its region and its rect in the layout's frame.</summary>
    internal sealed record Cell(string Kind, string Region, Rect2 Rect);

    /// <summary>A variant layout's cells in paint order, its fence and wall lines, and its paths.</summary>
    internal sealed class VariantLayout
    {
        public List<Cell> Cells { get; } = new();

        public List<(string Kind, Vector2[] Points)> Lines { get; } = new();

        public List<Vector2[]> Paths { get; } = new();
    }

    // A kind's meshes (its near variants) and the material drawing them.
    private sealed record Look(Kind Kind, Mesh[] Meshes, Material Material);

    // The free-ground tests in one variant's frame. Lengths are in layout units.
    private sealed class Ground
    {
        // How far inside a lot's edge a point must lie to count as under it: the float error of a wrapped point.
        private const float EdgeM = 0.02f;

        private readonly VariantLayout _layout;
        private readonly List<(Vector2[] Poly, Rect2 Box)> _lots = new();
        private readonly CityGround.PaintLayout _spec;
        private readonly List<(Vector2 A, Vector2 B)> _lines = new(), _paths = new();

        public Ground(VariantLayout layout, IReadOnlyList<Vector2[]> lots, CityGround.PaintLayout spec)
        {
            _layout = layout;
            _spec = spec;
            foreach (var poly in lots)
            {
                var box = new Rect2(poly[0], Vector2.Zero);
                foreach (var p in poly)
                    box = box.Expand(p);
                _lots.Add((poly, box));
            }
            foreach (var (_, points) in layout.Lines)
                Segments(points, _lines);
            foreach (var points in layout.Paths)
                Segments(points, _paths);
        }

        // The topmost cell holding the point, -1 in a sliver no cell covers.
        public int TopCell(Vector2 p)
        {
            for (int i = _layout.Cells.Count - 1; i >= 0; i--)
            {
                if (_layout.Cells[i].Rect.HasPoint(p))
                    return i;
            }
            return -1;
        }

        public string? TopKind(Vector2 p) => TopCell(p) is >= 0 and var i ? _layout.Cells[i].Kind : null;

        // Whether a footprint of the half extents at the point, turned by the yaw, stands on free ground. Every corner
        // and the centre lie in a cell, under no lot and on no street. A line piece tests its centre line only, since a
        // lot-line fence stands within a lot's half-metre growth.
        public bool Clear(Vector2 at, float yaw, float halfLengthM, float halfDepthM, bool line)
        {
            var x = new Vector2(MathF.Cos(yaw), -MathF.Sin(yaw)) * (halfLengthM / TileM);
            var z = new Vector2(MathF.Sin(yaw), MathF.Cos(yaw)) * (halfDepthM / TileM);
            Span<Vector2> points = stackalloc Vector2[] { at, at + x + z, at + x - z, at - x + z, at - x - z };
            foreach (var p in points)
            {
                if (TopCell(p) < 0 || OnStreet(p) || (!line && UnderLot(p)))
                    return false;
            }
            return !line || !UnderLot(at);
        }

        public float LotDistance(Vector2 p)
        {
            if (UnderLot(p))
                return 0f;
            var q = Wrap(p);
            float best = float.MaxValue;
            foreach (var (poly, _) in _lots)
            {
                for (int du = -1; du <= 1; du++)
                {
                    for (int dv = -1; dv <= 1; dv++)
                    {
                        var s = new Vector2(du, dv);
                        for (int i = 0; i < poly.Length; i++)
                            best = MathF.Min(best, SegmentDistance(q, poly[i] + s, poly[(i + 1) % poly.Length] + s));
                    }
                }
            }
            return best;
        }

        public float LineDistance(Vector2 p) => Nearest(p, _lines);

        public float PathDistance(Vector2 p) => Nearest(p, _paths);

        private static void Segments(Vector2[] points, List<(Vector2, Vector2)> into)
        {
            for (int i = 0; i + 1 < points.Length; i++)
                into.Add((points[i], points[i + 1]));
        }

        private static float Nearest(Vector2 p, List<(Vector2 A, Vector2 B)> segments)
        {
            float best = float.MaxValue;
            foreach (var (a, b) in segments)
                best = MathF.Min(best, SegmentDistance(p, a, b));
            return best;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var d = b - a;
            float t = d.LengthSquared() > 0f ? Math.Clamp((p - a).Dot(d) / d.LengthSquared(), 0f, 1f) : 0f;
            return p.DistanceTo(a + (d * t));
        }

        private static Vector2 Wrap(Vector2 p) => new(p.X - MathF.Floor(p.X), p.Y - MathF.Floor(p.Y));

        // Inside a convex polygon of either winding by more than EdgeM, so a lot-line fence on its grown edge stands outside.
        private static bool InConvex(Vector2[] poly, Vector2 p)
        {
            int sign = 0;
            for (int i = 0; i < poly.Length; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Length];
                float cross = ((b.X - a.X) * (p.Y - a.Y)) - ((b.Y - a.Y) * (p.X - a.X));
                if (MathF.Abs(cross) < EdgeM / TileM * (b - a).Length())
                    return false;
                int s = cross > 0f ? 1 : -1;
                if (sign != 0 && s != sign)
                    return false;
                sign = s;
            }
            return true;
        }

        // A lot's origin is wrapped into one tile and its polygons are not, so each is tested again a tile over.
        private bool UnderLot(Vector2 p)
        {
            var q = Wrap(p);
            foreach (var (poly, box) in _lots)
            {
                for (int du = -1; du <= 1; du++)
                {
                    for (int dv = -1; dv <= 1; dv++)
                    {
                        var r = q - new Vector2(du, dv);
                        if (box.HasPoint(r) && InConvex(poly, r))
                            return true;
                    }
                }
            }
            return false;
        }

        private bool OnStreet(Vector2 p)
        {
            foreach (var s in _spec.Streets)
            {
                float c = s.Axis.Dot(p) - s.Centre;
                if (MathF.Abs(c - MathF.Floor(c + 0.5f)) <= s.Half)
                    return true;
            }
            return false;
        }
    }

    // Flagged triangles binned by 64 m cell.
    private sealed class Overlay
    {
        private const float BinM = 64f;
        private readonly Dictionary<(int, int), List<(Vector3 A, Vector3 B, Vector3 C)>> _bins = new();

        public void Add(Vector3 a, Vector3 b, Vector3 c)
        {
            int x0 = (int)MathF.Floor(MathF.Min(a.X, MathF.Min(b.X, c.X)) / BinM), x1 = (int)MathF.Floor(MathF.Max(a.X, MathF.Max(b.X, c.X)) / BinM);
            int z0 = (int)MathF.Floor(MathF.Min(a.Z, MathF.Min(b.Z, c.Z)) / BinM), z1 = (int)MathF.Floor(MathF.Max(a.Z, MathF.Max(b.Z, c.Z)) / BinM);
            for (int x = x0; x <= x1; x++)
            {
                for (int z = z0; z <= z1; z++)
                {
                    if (!_bins.TryGetValue((x, z), out var list))
                        _bins[(x, z)] = list = new();
                    list.Add((a, b, c));
                }
            }
        }

        // Whether any flagged triangle shares a bin with the corners' bounds, so a polygon far from all of them skips the test.
        public bool Meets(IReadOnlyList<Vector3> corners)
        {
            int x0 = (int)MathF.Floor(corners.Min(p => p.X) / BinM), x1 = (int)MathF.Floor(corners.Max(p => p.X) / BinM);
            int z0 = (int)MathF.Floor(corners.Min(p => p.Z) / BinM), z1 = (int)MathF.Floor(corners.Max(p => p.Z) / BinM);
            for (int x = x0; x <= x1; x++)
            {
                for (int z = z0; z <= z1; z++)
                {
                    if (_bins.ContainsKey((x, z)))
                        return true;
                }
            }
            return false;
        }

        // Whether a flagged triangle lies over the foot, coplanar within half a metre.
        public bool Covers(Vector3 foot)
        {
            if (!_bins.TryGetValue(((int)MathF.Floor(foot.X / BinM), (int)MathF.Floor(foot.Z / BinM)), out var list))
                return false;
            foreach (var (a, b, c) in list)
            {
                if (VegetationGround.HeightIn(a, b, c, foot) is { } y && MathF.Abs(y - foot.Y) <= 0.5f)
                    return true;
            }
            return false;
        }
    }
}
