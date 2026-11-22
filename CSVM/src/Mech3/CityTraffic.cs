using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Cars on the Enhanced city's painted streets, under <see cref="CityDressing"/>'s root so they show exactly with the
/// city. The lanes follow the street centrelines of every floored polygon (<see cref="CityGround.FloorOf"/>), and the
/// highway deck's own paint (<see cref="CityHighway"/>). Every car drives a closed path: round a block on the grid, or
/// down its street or the highway and back after a U-turn. Its pieces, none longer than <see cref="PieceM"/>, share one pattern of cars, so a car leaving a piece is the next piece's car
/// arriving. The shader moves each car on <c>csky_time</c>, so nothing steps on the CPU.
/// <c>CSVM_CITY_TRAFFIC=off</c> builds none, the A/B for a cost measurement.
/// </summary>
internal static class CityTraffic
{
    /// <summary>The longest piece of a path, and the shortest period of its car pattern.</summary>
    public const float PieceM = 256f;

    private const float TileM = 256f, StepM = 4f, MinRunM = 48f;

    // TUNE. A car's reach about its own stretch of street, the body's half length and a far lamp's glow both inside it.
    private const float ReachM = 12f;

    // TUNE. A street this wide each side of its centre or wider takes three lanes each way, a narrower one two. The lanes
    // split the half width less KerbM evenly, as cblock1's lines at 0, 5 and 11 m and kerb at 16 m do.
    private const float ThreeLaneHalfM = 14f, KerbM = 0.5f;

    // TUNE. The lane speeds and the share of trucks. Each path carries one car per period of its pattern.
    private const float SpeedMin = 8f, SpeedMax = 15f, TruckShare = 0.2f;

    // TUNE. The lamps: their nodes' reach, past the 1.6 km where csky_clutter_dark_lit has darkened them. Then their
    // gains, their glow radius and the least pixels it keeps at any distance.
    private const float LampFarM = 1700f, HeadGain = 4f, TailGain = 2.5f, LampM = 0.45f, LampPx = 3f;

    // TUNE. The corner radii of a right and a left turn round a block, and the largest loop's side in blocks.
    private const float RightTurnM = 7f, LeftTurnM = 11f;
    private const int LoopBlocksMax = 3;

    // TUNE. The edge of the square cells the cars are cut into by their piece's start, one node per kind and cell.
    private const float CellM = 1024f;

    // A car's place on its piece from INSTANCE_CUSTOM: length, packed speed and phase, period, signed turn radius.
    // The shift and turn are its offset and heading in the piece's frame. It shows while car_on is 1.
    private const string MotionCode = """
        float car_s = mod(mod(INSTANCE_CUSTOM.y, 8192.0) / 16.0 + floor(INSTANCE_CUSTOM.y / 8192.0) / 32.0 * csky_time, INSTANCE_CUSTOM.z);
        float car_on = step(car_s, INSTANCE_CUSTOM.x);
        float car_r = INSTANCE_CUSTOM.w;
        float car_a = abs(car_r) > 0.01 ? car_s / car_r : 0.0;
        vec3 car_shift = abs(car_r) > 0.01 ? vec3(car_r * sin(car_a), 0.0, car_r * (1.0 - cos(car_a))) : vec3(car_s, 0.0, 0.0);
        mat3 car_turn = mat3(vec3(cos(car_a), 0.0, sin(car_a)), vec3(0.0, 1.0, 0.0), vec3(-sin(car_a), 0.0, cos(car_a)));

        """;

    // TUNE. The sedans' paints, sRGB: black most, then the period's maroon, green, navy, grey, cream, cab yellow, blue-grey
    // and brown. A sedan draws one by its seed in place of its model's grade. Trucks keep theirs, which tints their signs.
    private static readonly Color[] Paints =
    {
        new(24 / 255f, 24 / 255f, 26 / 255f), new(30 / 255f, 28 / 255f, 28 / 255f), new(18 / 255f, 18 / 255f, 20 / 255f),
        new(86 / 255f, 30 / 255f, 30 / 255f), new(36 / 255f, 62 / 255f, 42 / 255f), new(32 / 255f, 40 / 255f, 74 / 255f),
        new(96 / 255f, 96 / 255f, 98 / 255f), new(176 / 255f, 158 / 255f, 118 / 255f), new(210 / 255f, 165 / 255f, 40 / 255f),
        new(62 / 255f, 72 / 255f, 88 / 255f), new(80 / 255f, 56 / 255f, 38 / 255f),
    };

    // The city shaders rewritten to drive, by the city shader and whether the car takes a paint.
    private static readonly Dictionary<(Shader City, bool Paint), Shader> DrivingShaders = new();

    private static ShaderMaterial? _lamps;

    /// <summary>False under <c>CSVM_CITY_TRAFFIC=off</c>.</summary>
    public static bool Wanted => !string.Equals(System.Environment.GetEnvironmentVariable("CSVM_CITY_TRAFFIC"), "off", StringComparison.OrdinalIgnoreCase);

    /// <summary>Builds the traffic under <paramref name="parent"/> from the floored street centrelines. Returns the body
    /// nodes for <see cref="CityDressing"/> to range as its own: the far flag, the fade's far end squared and the radius.
    /// The lamp nodes range themselves.</summary>
    public static List<(MultiMeshInstance3D Node, bool Far, float Far2, float Radius)> Build(GameZ gamez, CityGround ground,
        CityDressing.Look sedan, CityDressing.Look truck, Node3D parent)
    {
        var ranged = new List<(MultiMeshInstance3D, bool, float, float)>();
        var materials = new Dictionary<(bool Truck, bool Far), ShaderMaterial>();
        foreach (bool isTruck in new[] { false, true })
        {
            var look = isTruck ? truck : sedan;
            foreach (var (far, city) in new[] { (false, look.Material), (true, look.Far?.Material) })
            {
                if (city == null)
                    continue;
                if (city is not ShaderMaterial shaded || Driving(shaded, look.Kind.Fade, paint: !isTruck) is not { } driving)
                {
                    Log.Warn("world", $"city traffic: none, the {(isTruck ? "truck" : "sedan")}'s material is not the city shader this rewrites to drive");
                    return ranged;
                }
                materials[(isTruck, far)] = driving;
            }
        }

        var clock = Stopwatch.StartNew();
        var runs = Streets(gamez, ground);
        double walked = clock.Elapsed.TotalMilliseconds;
        var bins = new Dictionary<(int X, int Z, bool Truck), List<(Transform3D Xf, Color Custom)>>();
        int cars = 0, uTurns = 0, blocks = 0;
        float laneKm = 0f;
        uint seed = 0;
        void Drive(Path path)
        {
            seed++;
            laneKm += path.Length / 1000f;
            var at = path.Segs[0].Start;
            cars += Emit(path, CityDressing.Mix((seed * 0x9E3779B1u) ^ unchecked((uint)(int)(at.X * 7f)) ^ (unchecked((uint)(int)(at.Y * 13f)) << 7)), bins);
        }

        // On a grid street of three lanes each way the inner and kerb lanes circle blocks, turning left and right at the
        // corners. They U-turn along any stretch no block loop drives. Every other lane U-turns down its whole street.
        var grid = Grid(runs);
        var driven = new Dictionary<(Run Run, int Lane), List<(float From, float To)>>();
        foreach (bool right in new[] { true, false })
        {
            foreach (var path in BlockLoops(grid, right, driven))
            {
                Drive(path);
                blocks++;
            }
        }
        foreach (var run in runs)
        {
            float[] lanes = run.Lanes();
            bool gridded = lanes.Length == 3 && grid.Lines.Contains(run);
            for (int k = 0; k < lanes.Length; k++)
            {
                var stretches = gridded && k != 1
                    ? Undriven(run.Length, driven.GetValueOrDefault((run, k)), MathF.Max(MinRunM, (2f * lanes[k]) + 8f))
                    : new List<(float, float)> { (0f, run.Length) };
                foreach (var (from, to) in stretches)
                {
                    if (to - from < (2f * lanes[k]) + 8f)
                        continue;
                    Drive(UTurnLoop(from == 0f && to == run.Length ? run : run.Slice(from, to), lanes[k]));
                    uTurns++;
                }
            }
        }

        // The highway deck carries no floor, so its lanes come from its own paint, driven after the streets.
        var deckPolygons = CityHighway.Polygons(gamez);
        var chained = CityHighway.Chain(deckPolygons);
        var highways = CityHighway.Loops(chained.Decks, out int unclosed);
        highways.ForEach(Drive);
        if (chained.Polygons != deckPolygons.Count || unclosed > 0)
            Log.Warn("world", $"city traffic: the highway chains {chained.Polygons} of its {deckPolygons.Count} polygons ({chained.Orphans} orphan triangles or polygons) and leaves {unclosed} lane loops unclosed");
        int highway = highways.Count;

        long bytes = 0;
        int nodes = 0;
        var lampMeshes = new Dictionary<bool, ArrayMesh>();
        foreach (var ((cx, cz, isTruck), list) in bins)
        {
            var look = isTruck ? truck : sedan;
            string kind = isTruck ? "truck" : "sedan";
            var body = Add(parent, list, look.Meshes[0], materials[(isTruck, false)], $"car_near_{kind}_{cx}_{cz}");
            ranged.Add((body, false, look.Kind.Fade.Y, body.CustomAabb.Size.Length() * 0.5f));
            if (look.Far is { } far)
            {
                var box = Add(parent, list, far.Mesh, materials[(isTruck, true)], $"car_box_{kind}_{cx}_{cz}");
                ranged.Add((box, true, look.Kind.Fade.Y, box.CustomAabb.Size.Length() * 0.5f));
            }
            if (!lampMeshes.TryGetValue(isTruck, out var lampMesh))
                lampMeshes[isTruck] = lampMesh = LampMesh(look.Kind.HalfLength);
            var lamps = Add(parent, list, lampMesh, LampMaterial(sedan.Kind.Fade), $"car_lamps_{kind}_{cx}_{cz}");
            lamps.VisibilityRangeEnd = LampFarM + (lamps.CustomAabb.Size.Length() * 0.5f);
            int kinds = look.Far != null ? 3 : 2;
            bytes += (long)kinds * list.Count * 16 * sizeof(float);
            nodes += kinds;
        }
        Log.Info("world", $"city traffic: {runs.Count} street runs, {uTurns} U-turn loops, {blocks} block loops, {highway} highway loops over {chained.Decks.Count} decks of {chained.Polygons} polygons, {laneKm:0} lane km, {cars} cars in {nodes} nodes holding {bytes / 1048576.0:0.0} MB; streets walked in {walked:0} ms, built in {clock.Elapsed.TotalMilliseconds - walked:0} ms");
        return ranged;
    }

    /// <summary>The straight runs of painted street centreline over every floored polygon. Each line is sampled every
    /// <see cref="StepM"/> and cut where no floored ground lies under it.</summary>
    internal static List<Run> Streets(GameZ gamez, CityGround ground)
    {
        var lines = new Dictionary<(int Nx, int Nz, long P), (Vector2 N, float P, SortedDictionary<int, float> Ys)>();
        // A line's half width in metres, the narrowest paint along it, so its lanes stay inside every kerb.
        var halves = new Dictionary<(int Nx, int Nz, long P), float>();
        VegetationGround.WalkWorld(gamez, "world1", (node, mesh, xf) =>
        {
            foreach (var poly in mesh.Polygons)
            {
                if (ground.FloorOf(node, poly) is not { } floor || poly.UvCoords is not { Count: >= 3 } uvs)
                    continue;
                var tris = new List<Vector3>();
                for (int i = 0; i + 2 < poly.VertexIndices.Count; i++)
                {
                    var (a, b, c) = VegetationGround.Positions(poly, i);
                    tris.Add(xf * mesh.Vertices[poly.VertexIndices[a]]);
                    tris.Add(xf * mesh.Vertices[poly.VertexIndices[b]]);
                    tris.Add(xf * mesh.Vertices[poly.VertexIndices[c]]);
                }
                var p0 = xf * mesh.Vertices[poly.VertexIndices[0]];
                // A floored polygon is in frame, so its offset is whole within FrameSlack: rounded, every polygon agrees on a line.
                // A turned one's cross turns into itself, so it takes the world's frame.
                var offset = floor.Turned ? Vector2.Zero : new Vector2(MathF.Round(uvs[0].X - (p0.X / TileM)), MathF.Round(uvs[0].Y - (p0.Z / TileM)));
                foreach (var s in floor.Spec.Streets)
                {
                    float alen = s.Axis.Length();
                    var n = s.Axis / alen;
                    var t = new Vector2(-n.Y, n.X);
                    float pMin = float.MaxValue, pMax = float.MinValue, qMin = float.MaxValue, qMax = float.MinValue;
                    foreach (var v in tris)
                    {
                        var w = new Vector2(v.X, v.Z);
                        pMin = MathF.Min(pMin, n.Dot(w));
                        pMax = MathF.Max(pMax, n.Dot(w));
                        qMin = MathF.Min(qMin, t.Dot(w));
                        qMax = MathF.Max(qMax, t.Dot(w));
                    }
                    // From dot(axis, w / 256 + offset) = centre + m: p = 256 (centre + m - dot(axis, offset)) / |axis|.
                    float baseC = s.Centre - s.Axis.Dot(offset);
                    int m0 = (int)MathF.Ceiling((pMin * alen / TileM) - baseC), m1 = (int)MathF.Floor((pMax * alen / TileM) - baseC);
                    for (int m = m0; m <= m1; m++)
                    {
                        float p = TileM * (baseC + m) / alen;
                        var key = ((int)MathF.Round(n.X * 1000f), (int)MathF.Round(n.Y * 1000f), (long)MathF.Round(p * 4f));
                        if (!lines.TryGetValue(key, out var line))
                            lines[key] = line = (n, p, new SortedDictionary<int, float>());
                        float halfM = s.Half * TileM / alen;
                        halves[key] = halves.TryGetValue(key, out var hadHalf) ? MathF.Min(hadHalf, halfM) : halfM;
                        for (int i = (int)MathF.Ceiling(qMin / StepM); i <= (int)MathF.Floor(qMax / StepM); i++)
                        {
                            var w = (n * p) + (t * (i * StepM));
                            if (CityDressing.Height(tris, w) is { } y)
                                line.Ys[i] = line.Ys.TryGetValue(i, out var had) ? MathF.Max(had, y) : y;
                        }
                    }
                }
            }
        });

        var runs = new List<Run>();
        foreach (var (key, (n, p, ys)) in lines)
        {
            float half = halves[key];
            var t = new Vector2(-n.Y, n.X);
            var heights = new List<float>();
            int first = int.MinValue, prev = int.MinValue;
            void Close()
            {
                float length = (prev - first) * StepM;
                if (heights.Count >= 2 && length >= MinRunM)
                    runs.Add(new Run((n * p) + (t * (first * StepM)), t, n, length, half, heights.ToArray()));
                heights.Clear();
            }
            foreach (var (i, y) in ys)
            {
                if (prev != int.MinValue && i != prev + 1)
                    Close();
                if (heights.Count == 0)
                    first = i;
                heights.Add(y);
                prev = i;
            }
            Close();
        }
        return runs;
    }

    /// <summary>The axis-aligned runs as grid lines of one 256 m index each.</summary>
    internal static GridLines Grid(List<Run> runs)
    {
        var lines = new HashSet<Run>();
        var byLine = new Dictionary<(bool, int), List<Run>>();
        foreach (var run in runs)
        {
            if (MathF.Abs(run.Normal.X) < 0.999f && MathF.Abs(run.Normal.Y) < 0.999f)
                continue;
            bool x = MathF.Abs(run.Normal.X) > 0.999f;
            // The paints' street centres sit within a metre of mid-tile, so one street is one index across paints.
            int index = (int)MathF.Round(((x ? run.Start.X : run.Start.Y) - (TileM / 2f)) / TileM);
            lines.Add(run);
            if (!byLine.TryGetValue((x, index), out var list))
                byLine[(x, index)] = list = new();
            list.Add(run);
        }
        return new GridLines(lines, byLine);
    }

    /// <summary>One lane along the run and back in its twin the other way, U-turning left inside each end.</summary>
    internal static Path UTurnLoop(Run run, float off)
    {
        var path = new Path(run.Start + (run.Dir * off) - (run.Normal * off), run.Dir);
        var ground = Ground(run);
        path.Line(run.Length - (2f * off), ground);
        path.Turn(-off, MathF.PI);
        path.Line(run.Length - (2f * off), ground);
        path.Turn(-off, MathF.PI);
        return path;
    }

    /// <summary>Rectangles of 1 to <see cref="LoopBlocksMax"/> blocks a side tiling the grid, one loop each. A right
    /// loop drives the kerb lanes inside its rectangle, a left loop the inner lanes outside it. Each loop's sides go
    /// into <paramref name="driven"/> as stretches of their run's lane.</summary>
    internal static IEnumerable<Path> BlockLoops(GridLines grid, bool right, Dictionary<(Run Run, int Lane), List<(float From, float To)>> driven)
    {
        var xs = grid.Runs.Keys.Where(k => k.X).Select(k => k.Index).Distinct().OrderBy(i => i).ToList();
        var zs = grid.Runs.Keys.Where(k => !k.X).Select(k => k.Index).Distinct().OrderBy(i => i).ToList();
        var taken = new HashSet<(int, int)>();
        foreach (int i in xs)
        {
            foreach (int j in zs)
            {
                if (taken.Contains((i, j)))
                    continue;
                uint h = CityDressing.Mix(((uint)i * 73856093u) ^ ((uint)j * 19349663u) ^ (right ? 0x5bd1e995u : 0u));
                int wantA = 1 + (int)(Unit(h, 1) * LoopBlocksMax), wantB = 1 + (int)(Unit(h, 2) * LoopBlocksMax);
                Path? Pick()
                {
                    for (int a = wantA; a >= 1; a--)
                    {
                        for (int b = wantB; b >= 1; b--)
                        {
                            if (Block(grid, i, j, a, b, right, taken, driven) is { } path)
                                return path;
                        }
                    }
                    return null;
                }
                if (Pick() is { } picked)
                    yield return picked;
            }
        }
    }

    /// <summary>The stretches of [0, length] outside every driven one, each at least <paramref name="least"/> long.</summary>
    internal static List<(float From, float To)> Undriven(float length, List<(float From, float To)>? driven, float least)
    {
        var gaps = new List<(float, float)>();
        float at = 0f;
        foreach (var (from, to) in (driven ?? new()).OrderBy(d => d.From))
        {
            if (from - at >= least)
                gaps.Add((at, from));
            at = MathF.Max(at, to);
        }
        if (length - at >= least)
            gaps.Add((at, length));
        return gaps;
    }

    /// <summary>A path's car period: its length over the most whole periods of at least <see cref="PieceM"/>. No piece
    /// outruns it, and the stream closes on itself.</summary>
    internal static float Period(float length) => length / MathF.Max(1f, MathF.Floor(length / PieceM));

    /// <summary>Speed in 1/32 m/s over phase in 1/16 m below 8192, both whole so the float holds them exactly.</summary>
    internal static float Pack(float speed, float phase) => (MathF.Round(speed * 32f) * 8192f) + MathF.Min(MathF.Round(phase * 16f), 8191f);

    /// <summary>The city shader's code rewritten to drive a car from its custom data, or null where the text this
    /// rewrites has moved. A painted car draws a paint from <see cref="Paints"/> by its seed in place of the grade.</summary>
    internal static string? DrivingCode(string city, bool paint)
    {
        string count = Paints.Length.ToString(CultureInfo.InvariantCulture);
        string paints = string.Join(", ", Paints.Select(c => c.SrgbToLinear()).Select(c => $"vec3({F(c.R)}, {F(c.G)}, {F(c.B)})"));
        // The custom data is the motion, so the fade comes from a uniform. The seed comes from the path's speed and
        // period, the same on every piece, so a car keeps its paint along its path.
        var edits = new List<(string From, string To)>
        {
            ("shader_type spatial;", $"shader_type spatial;\nuniform vec4 car_fade;\nvarying flat vec3 v_car_paint;\nconst vec3 car_paints[{count}] = vec3[]({paints});"),
            ("uint(INSTANCE_CUSTOM.w + 0.5)", "car_seed"),
            (", INSTANCE_CUSTOM)", ", car_custom)"),
            ("vec3 origin = MODEL_MATRIX[3].xyz;", MotionCode + $$"""
                vec4 car_custom = vec4(car_fade.xyz, 0.0);
                uint car_seed = floatBitsToUint(INSTANCE_CUSTOM.z) ^ (uint(INSTANCE_CUSTOM.y) >> 13u);
                v_car_paint = car_paints[((car_seed * 2654435761u) >> 16u) % {{count}}u];
                vec3 origin = (MODEL_MATRIX * vec4(car_shift, 1.0)).xyz;
                """),
            ("v_dark = csky_clutter_dark_lit", "VERTEX = (car_turn * VERTEX + car_shift) * car_on;\n    NORMAL = car_turn * NORMAL;\n    v_dark = csky_clutter_dark_lit"),
        };
        if (paint)
            edits.Add(("csky_city_look(v_seed, UV, UV2, grade, glow)", "csky_city_look(v_seed, UV, UV2, v_car_paint, glow)"));
        foreach (var (from, to) in edits)
        {
            if (!city.Contains(from, StringComparison.Ordinal))
                return null;
            city = city.Replace(from, to, StringComparison.Ordinal);
        }
        return city;
    }

    // The run of a grid line covering it from lo to hi along, within the cross streets' half width, or null.
    private static Run? Covering(GridLines grid, bool x, int index, float lo, float hi)
    {
        if (!grid.Runs.TryGetValue((x, index), out var list))
            return null;
        foreach (var run in list)
        {
            var end = run.Start + (run.Dir * run.Length);
            float a = x ? run.Start.Y : run.Start.X, b = x ? end.Y : end.X;
            if (MathF.Min(a, b) <= lo + ThreeLaneHalfM && MathF.Max(a, b) >= hi - ThreeLaneHalfM)
                return run;
        }
        return null;
    }

    private static Func<Vector2, float> Ground(Run run) => w => run.HeightAt((w - run.Start).Dot(run.Dir));

    private static Path? Block(GridLines grid, int i, int j, int a, int b, bool right, HashSet<(int, int)> taken,
        Dictionary<(Run Run, int Lane), List<(float From, float To)>> driven)
    {
        for (int di = 0; di < a; di++)
        {
            for (int dj = 0; dj < b; dj++)
            {
                if (taken.Contains((i + di, j + dj)))
                    return null;
            }
        }
        float Centreline(int index) => (index * TileM) + (TileM / 2f);
        float x0 = Centreline(i), x1 = Centreline(i + a), z0 = Centreline(j), z1 = Centreline(j + b);
        if (Covering(grid, true, i, z0, z1) is not { } west || Covering(grid, true, i + a, z0, z1) is not { } east
            || Covering(grid, false, j, x0, x1) is not { } south || Covering(grid, false, j + b, x0, x1) is not { } north)
            return null;
        // The exact centrelines, each inset by its lane: into the block for a right loop, out of it for a left one.
        int lane = right ? 2 : 0;
        float Lane(Run run) => right ? run.Lanes()[lane] : -run.Lanes()[lane];
        float w = west.Start.X + Lane(west), e = east.Start.X - Lane(east), s = south.Start.Y + Lane(south), n = north.Start.Y - Lane(north);
        float r = right ? RightTurnM : LeftTurnM;
        if (e - w < (2f * r) + 8f || n - s < (2f * r) + 8f)
            return null;
        for (int di = 0; di < a; di++)
        {
            for (int dj = 0; dj < b; dj++)
                taken.Add((i + di, j + dj));
        }
        // A right loop runs east along the south side (+Z is a car's right heading +X), a left loop west.
        var path = right ? new Path(new Vector2(w + r, s), Vector2.Right) : new Path(new Vector2(e - r, s), Vector2.Left);
        float turn = right ? r : -r;
        foreach (var side in right ? new[] { south, east, north, west } : new[] { south, west, north, east })
        {
            bool alongX = side == south || side == north;
            var start = path.At;
            path.Line((alongX ? e - w : n - s) - (2f * r), Ground(side));
            // The side's stretch of its run's lane, its corners included.
            float p = (start - side.Start).Dot(side.Dir), q = (path.At - side.Start).Dot(side.Dir);
            if (!driven.TryGetValue((side, lane), out var stretches))
                driven[(side, lane)] = stretches = new();
            stretches.Add((MathF.Min(p, q) - r, MathF.Max(p, q) + r));
            path.Turn(turn, MathF.PI / 2f);
        }
        return path;
    }

    // Puts a path's car in its cells' bins: one car per period, which divides the path's length so the stream closes
    // on itself. Each piece shifts the pattern back by its distance along the path, so a car leaving one piece is the
    // next piece's car arriving. Returns the path's cars.
    private static int Emit(Path path, uint h, Dictionary<(int X, int Z, bool Truck), List<(Transform3D Xf, Color Custom)>> bins)
    {
        float period = Period(path.Length);
        float speed = Mathf.Lerp(SpeedMin, SpeedMax, Unit(h, 1));
        float phase = Unit(h, 10) * period;
        bool isTruck = Unit(h, 40) < TruckShare;
        float along = 0f;
        foreach (var seg in path.Segs)
        {
            var basis = new Basis(Vector3.Up, MathF.Atan2(-seg.Dir.Y, seg.Dir.X));
            // A straight's climb as a shear of its X, so the car's shift along X climbs with it.
            basis.X += new Vector3(0f, (seg.Y1 - seg.Y0) / MathF.Max(seg.Length, 1f), 0f);
            var xf = new Transform3D(basis, new Vector3(seg.Start.X, seg.Y0 + 0.05f, seg.Start.Y));
            var key = ((int)MathF.Floor(seg.Start.X / CellM), (int)MathF.Floor(seg.Start.Y / CellM), isTruck);
            if (!bins.TryGetValue(key, out var list))
                bins[key] = list = new();
            // Godot's instance custom data is a Color: length, packed speed and phase, period and turn radius.
            list.Add((xf, new Color(seg.Length, Pack(speed, Mathf.PosMod(phase - along, period)), period, seg.Radius)));
            along += seg.Length;
        }
        return (int)MathF.Round(path.Length / period);
    }

    private static MultiMeshInstance3D Add(Node3D parent, List<(Transform3D Xf, Color Custom)> list, Mesh mesh, Material material, string name)
    {
        // A car stays on its piece: a straight along its sheared X for its length (custom R). A turn of radius custom A
        // stays within twice that radius of its start.
        var bounds = new Aabb(list[0].Xf.Origin, Vector3.Zero);
        foreach (var (xf, c) in list)
        {
            var reach = new Vector3(2f * MathF.Abs(c.A), 0f, 2f * MathF.Abs(c.A));
            bounds = c.A == 0f
                ? bounds.Expand(xf.Origin).Expand(xf.Origin + (xf.Basis.X * c.R))
                : bounds.Expand(xf.Origin - reach).Expand(xf.Origin + reach);
        }
        bounds = bounds.Grow(ReachM);
        var centre = bounds.GetCenter();
        var node = new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseCustomData = true,
                Mesh = mesh,
                InstanceCount = list.Count,
                Buffer = CityDressing.Buffer(list, centre),
            },
            MaterialOverride = material,
            Position = centre,
            CustomAabb = new Aabb(bounds.Position - centre, bounds.Size),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Layers = VegetationModels.Layer,
        };
        parent.AddChild(node);
        return node;
    }

    private static ShaderMaterial? Driving(ShaderMaterial city, Vector3 fade, bool paint)
    {
        if (!DrivingShaders.TryGetValue((city.Shader, paint), out var shader))
        {
            if (DrivingCode(city.Shader.Code, paint) is not { } code)
                return null;
            DrivingShaders[(city.Shader, paint)] = shader = new Shader { Code = code };
        }
        var material = (ShaderMaterial)city.Duplicate();
        material.Shader = shader;
        material.SetShaderParameter("car_fade", new Vector4(fade.X, fade.Y, fade.Z, 0f));
        return material;
    }

    private static ShaderMaterial LampMaterial(Vector3 fade)
    {
        if (_lamps == null)
        {
            _lamps = NewLampMaterial();
            _lamps.SetShaderParameter("car_fade", new Vector4(fade.X, fade.Y, fade.Z, 0f));
        }
        return _lamps;
    }

    private static ShaderMaterial NewLampMaterial() => new ShaderMaterial
    {
        Shader = new Shader
        {
            Code = $$"""
                shader_type spatial;
                render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, skip_vertex_transform, fog_disabled;
                {{SceneBuilder.AtmosphereInclude}}
                {{SceneBuilder.ClutterFadeInclude}}
                {{SceneBuilder.TimeInclude}}

                uniform vec4 car_fade;
                varying vec3 v_lamp;
                varying vec2 v_corner;
                varying vec3 v_world;

                // Each lamp is a quad whose corners sit one metre off its centre in X and Y. The shader turns it to the
                // camera at its glow radius, lit by how far the lamp faces the camera.
                void vertex() {
                    {{MotionCode}}
                    vec3 centre = vec3(UV.x, UV2.x, UV.y);
                    v_corner = (VERTEX - centre).xy;
                    vec3 world_c = (MODEL_MATRIX * vec4(car_turn * centre + car_shift, 1.0)).xyz;
                    vec3 to_cam = CAMERA_POSITION_WORLD - world_c;
                    float d = length(to_cam);
                    // A pixel's width at that distance, so a far lamp stays a dot. Vulkan's projection flips Y: abs.
                    float px = 2.0 * d / (abs(PROJECTION_MATRIX[1][1]) * VIEWPORT_SIZE.y);
                    float r = max({{F(LampM)}}, {{F(LampPx)}} * px);
                    float head = step(0.5, COLOR.a);
                    float facing = dot(normalize(mat3(MODEL_MATRIX) * car_turn[0]), to_cam / max(d, 0.001)) * (head * 2.0 - 1.0);
                    // Darkened into the night as the city blocks are, so no lamp outshines the buildings around it.
                    float lit = smoothstep(-0.2, 0.5, facing) * car_on * (1.0 - csky_clutter_dark_lit(world_c, CAMERA_POSITION_WORLD, car_fade));
                    v_lamp = COLOR.rgb * mix({{F(TailGain)}}, {{F(HeadGain)}}, head) * lit;
                    v_world = world_c;
                    // Pulled its radius and a metre toward the camera, so neither its car nor the street cuts its glow.
                    VERTEX = ((VIEW_MATRIX * vec4(world_c, 1.0)).xyz + vec3(v_corner * r, r + 1.0)) * step(0.0001, lit);
                    NORMAL = vec3(0.0, 0.0, 1.0);
                }

                void fragment() {
                    float f = max(0.0, 1.0 - dot(v_corner, v_corner));
                    // Fogged out as the buildings are, so no lamp shows where the fog has hidden its street.
                    ALBEDO = v_lamp * (1.0 - csky_fog_amount(v_world, CAMERA_POSITION_WORLD));
                    ALPHA = f;
                }
                """,
        },
    };

    // Two head lamps at the +X end and two tail lamps at the -X end, each a quad of two triangles. Its centre rides in UV
    // (x, z) and UV2.x (y), its colour in COLOR and alpha 1 marks a head lamp. The gains stay in the shader, since a
    // vertex colour may be stored clamped to 1.
    private static ArrayMesh LampMesh(float halfLength)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var head = new Color(1f, 0.85f, 0.6f, 1f);
        var tail = new Color(1f, 0.08f, 0.04f, 0f);
        int at = 0;
        foreach (var (c, colour) in new[]
        {
            (new Vector3(halfLength + 0.05f, 0.75f, 0.55f), head), (new Vector3(halfLength + 0.05f, 0.75f, -0.55f), head),
            (new Vector3(-halfLength - 0.05f, 0.8f, 0.6f), tail), (new Vector3(-halfLength - 0.05f, 0.8f, -0.6f), tail),
        })
        {
            foreach (var corner in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f) })
            {
                st.SetColor(colour);
                st.SetUV(new Vector2(c.X, c.Z));
                st.SetUV2(new Vector2(c.Y, 0f));
                st.AddVertex(c + new Vector3(corner.X, corner.Y, 0f));
            }
            foreach (int k in new[] { 0, 1, 2, 0, 2, 3 })
                st.AddIndex(at + k);
            at += 4;
        }
        return st.Commit();
    }

    private static float Unit(uint h, int salt) => (CityDressing.Mix(h ^ ((uint)salt * 0x85EBCA77u)) >> 8) * (1f / 16777216f);

    private static string F(float v) => v.ToString("0.0#####", CultureInfo.InvariantCulture);

    /// <summary>One piece of a car's path: a straight climbing from Y0 to Y1, or a level turn of signed radius,
    /// positive to the right.</summary>
    internal readonly record struct Seg(Vector2 Start, Vector2 Dir, float Length, float Radius, float Y0, float Y1)
    {
        /// <summary>A car's place and heading <paramref name="s"/> along the piece, as the driving shader puts it.</summary>
        public (Vector2 At, Vector2 Dir) Along(float s)
        {
            if (Radius == 0f)
                return (Start + (Dir * s), Dir);
            float a = s / Radius;
            var right = new Vector2(-Dir.Y, Dir.X);
            return (Start + (Dir * (Radius * MathF.Sin(a))) + (right * (Radius * (1f - MathF.Cos(a)))), (Dir * MathF.Cos(a)) + (right * MathF.Sin(a)));
        }
    }

    /// <summary>A closed path built from its start by straights and turns, each straight cut into pieces no longer
    /// than <see cref="PieceM"/>.</summary>
    internal sealed class Path(Vector2 at, Vector2 dir)
    {
        public readonly List<Seg> Segs = new();
        public Vector2 At = at, Dir = dir;
        public float Length;

        public void Line(float length, Func<Vector2, float> ground)
        {
            for (float done = 0f; done < length - 0.01f; done += PieceM)
            {
                float len = MathF.Min(PieceM, length - done);
                var end = At + (Dir * len);
                Segs.Add(new Seg(At, Dir, len, 0f, ground(At), ground(end)));
                At = end;
                Length += len;
            }
        }

        public void Turn(float radius, float angle)
        {
            float len = MathF.Abs(radius) * angle, a = len / radius;
            float y = Segs.Count > 0 ? Segs[^1].Y1 : 0f;
            Segs.Add(new Seg(At, Dir, len, radius, y, y));
            // A car's right in XZ is (-dir.z, dir.x), which a positive radius turns toward.
            var right = new Vector2(-Dir.Y, Dir.X);
            At += (Dir * (radius * MathF.Sin(a))) + (right * (radius * (1f - MathF.Cos(a))));
            Dir = ((Dir * MathF.Cos(a)) + (right * MathF.Sin(a))).Normalized();
            Length += len;
        }
    }

    /// <summary>The axis-aligned runs, and each grid line's runs by its axis (true for a line of constant X) and index.</summary>
    internal sealed record GridLines(HashSet<Run> Lines, Dictionary<(bool X, int Index), List<Run>> Runs);

    /// <summary>One straight stretch of painted street: its start, unit direction along, length and the ground heights
    /// every <see cref="StepM"/> from the start.</summary>
    internal sealed record Run(Vector2 Start, Vector2 Dir, Vector2 Normal, float Length, float HalfM, float[] Heights)
    {
        /// <summary>The lane centres' distances from the centreline, one side.</summary>
        public float[] Lanes()
        {
            int n = HalfM >= ThreeLaneHalfM ? 3 : 2;
            return Enumerable.Range(0, n).Select(k => (k + 0.5f) / n * (HalfM - KerbM)).ToArray();
        }

        public float HeightAt(float along)
        {
            float f = Math.Clamp(along / StepM, 0f, Heights.Length - 1);
            int i = Math.Min((int)f, Heights.Length - 2);
            return i < 0 ? Heights[0] : Mathf.Lerp(Heights[i], Heights[i + 1], f - i);
        }

        /// <summary>The stretch from <paramref name="from"/> to <paramref name="to"/> along, on whole height samples.</summary>
        public Run Slice(float from, float to)
        {
            int first = (int)MathF.Ceiling(from / StepM), last = Math.Min((int)MathF.Floor(to / StepM), Heights.Length - 1);
            return this with
            {
                Start = Start + (Dir * (first * StepM)),
                Length = (last - first) * StepM,
                Heights = Heights[first..(last + 1)],
            };
        }
    }
}
