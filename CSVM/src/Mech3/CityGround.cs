using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The Enhanced city's block floor over a chapter's painted ground, drawn while <see cref="CityModels.On"/>.
/// A chapter folder's <c>ground_layout.json</c> names each paint's street bands, its variant cells and the
/// building sets standing on it. A polygon takes the floor of its paint and set (<see cref="Resolve"/>). The world
/// shader keeps the paint, authored levels and all, inside the street bands and draws a floor variant per cell of world
/// XZ elsewhere. Ground laid in another frame, a set standing across its paint's streets and a combination with no
/// floor image keep the paint. Module entry: docs/architecture/Mech3.md.
/// </summary>
public sealed class CityGround
{
    /// <summary>The global the floor shader reads: 1 while <see cref="CityModels.On"/>, else 0.</summary>
    public const string OnParam = "csky_city_ground";

    /// <summary>The fragment-stage declarations of the floor arm, after the mip bias include.</summary>
    internal const string Declarations = $$"""
        global uniform float {{OnParam}};
        uniform sampler2DArray city_floor_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
        uniform vec4 city_floor_street0 = vec4(0.0, 0.0, 0.0, -1.0);
        uniform vec4 city_floor_street1 = vec4(0.0, 0.0, 0.0, -1.0);
        uniform vec4 city_floor_cell = vec4(0.0);
        uniform vec2 city_floor_origin = vec2(0.0);
        varying vec3 v_city_floor_pos;

        uint csky_city_floor_hash(uint x) {
            x ^= x >> 16u;
            x *= 0x7feb352du;
            x ^= x >> 15u;
            x *= 0x846ca68bu;
            x ^= x >> 16u;
            return x;
        }

        // The paint's share at uv in one street band: 1 inside, 0 outside, the edge spread over a pixel.
        float csky_city_floor_street(vec4 s, vec2 uv) {
            float c = dot(s.xy, uv) - s.z;
            float d = abs(fract(c + 0.5) - 0.5) - s.w;
            return s.w < 0.0 ? 0.0 : clamp(0.5 - d / max(fwidth(c), 1e-6), 0.0, 1.0);
        }
        """;

    /// <summary>The vertex-stage line, before the view transform.</summary>
    internal const string VertexLine = "    v_city_floor_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;\n";

    /// <summary>The fragment lines after <c>base_col</c>. UV2 holds the combination's first layer and its count, 0 to
    /// keep the paint. ⚠ Keep every sample outside a branch: the street edge reads screen derivatives.</summary>
    internal const string FragmentLines = """
            vec2 city_floor_w = v_city_floor_pos.xz / 256.0;
            uint city_floor_pick = csky_city_floor_hash(uint(int(floor(dot(city_floor_cell.xy, city_floor_w) - city_floor_origin.x)))
                ^ csky_city_floor_hash(uint(int(floor(dot(city_floor_cell.zw, city_floor_w) - city_floor_origin.y))) + 0x9e3779b9u));
            float city_floor_layer = UV2.x + float(city_floor_pick % uint(max(UV2.y, 1.0) + 0.5));
            vec4 city_floor_col = texture(city_floor_tex, vec3(UV, city_floor_layer), csky_mip_bias);
            float city_street = max(csky_city_floor_street(city_floor_street0, UV), csky_city_floor_street(city_floor_street1, UV));
            base_col = mix(base_col, city_floor_col, (1.0 - city_street) * step(0.5, csky_city_ground) * step(0.5, UV2.y));
        """;

    // How far, in UV, a corner may sit off the U+X V+Z frame of one 256 m repeat with whole UVs on whole tiles.
    private const float FrameSlack = 0.01f;

    // A set with more of its footprint than this inside its paint's streets stands across them. A floor would cut
    // its buildings, so it keeps the paint.
    private const float OnStreetMax = 0.01f;

    // Coplanar layers lie within this of each other (VegetationGround's rule).
    private const float CoplanarM = 0.5f;

    private readonly Dictionary<string, Floor> _floors;
    private readonly Dictionary<GameZPolygon, Vector2> _uv2;

    private CityGround(Dictionary<string, Floor> floors, Dictionary<GameZPolygon, Vector2> uv2, List<string> missing)
    {
        _floors = floors;
        _uv2 = uv2;
        Missing = missing;
    }

    /// <summary>Gets or sets the folder the floor images are read from instead of <c>&lt;chapter folder&gt;/ground</c>
    /// (<c>--city-ground=</c>). Null reads the shipped folder.</summary>
    public static string? Folder { get; set; }

    /// <summary>The value last put on the global, which a headless renderer does not read back.</summary>
    public static float Published { get; private set; }

    /// <summary>The combinations, <c>paint-set</c>, that keep the paint for want of a floor image.</summary>
    public IReadOnlyList<string> Missing { get; }

    /// <summary>How many polygons draw a floor.</summary>
    public int Floored => _uv2.Values.Count(v => v.Y > 0f);

    /// <summary>Declares the global off. Must run before the first floor shader is built.</summary>
    public static void RegisterGlobal() =>
        RenderingServer.GlobalShaderParameterAdd(OnParam, RenderingServer.GlobalShaderParameterType.Float, 0f);

    /// <summary>Puts <see cref="CityModels.On"/> on the global, so the floor draws exactly with the city.</summary>
    public static void Follow()
    {
        Published = CityModels.On ? 1f : 0f;
        RenderingServer.GlobalShaderParameterSet(OnParam, Published);
    }

    /// <summary>The floor of <paramref name="chapter"/>'s world over <paramref name="gamez"/>. Null where the chapter
    /// folder holds no layout or no floor image at all, so that world builds as it always did.</summary>
    public static CityGround? Load(GameZ gamez, string chapter)
    {
        string folder = $"{CityModels.Root}/{chapter.ToUpperInvariant()}";
        string layoutPath = $"{folder}/ground_layout.json";
        if (!FileAccess.FileExists(layoutPath))
            return null;
        var clock = Stopwatch.StartNew();
        var layout = Layout.Parse(FileAccess.GetFileAsString(layoutPath));
        string dir = Folder is { Length: > 0 } f ? Rooted(f) : $"{folder}/ground";
        var index = FileAccess.FileExists($"{dir}/index.json") ? Files(FileAccess.GetFileAsString($"{dir}/index.json")) : new();
        var floors = new Dictionary<string, Floor>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        int variants = 0;
        foreach (var (paint, spec) in layout.Paints)
        {
            var images = new Godot.Collections.Array<Image>();
            var sets = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
            foreach (var (set, onStreet) in spec.Sets)
            {
                if (onStreet > OnStreetMax)
                    continue;
                int first = images.Count;
                foreach (string file in index.TryGetValue($"{paint}-{set}", out var named) ? named : new List<string>())
                {
                    if (ReadImage($"{dir}/{file}") is not { } image)
                    {
                        Log.Warn("world", $"city ground {dir}/{file}: unreadable, left out");
                        continue;
                    }
                    if (images.Count > 0 && (image.GetSize() != images[0].GetSize()))
                    {
                        Log.Warn("world", $"city ground {file}: {image.GetSize()} differs from the paint's first floor {images[0].GetSize()}, left out");
                        continue;
                    }
                    images.Add(image);
                }
                if (images.Count == first)
                    missing.Add($"{paint}-{set}");
                else
                    sets[set] = new Vector2(first, images.Count - first);
            }
            if (images.Count == 0)
                continue;
            var array = new Texture2DArray();
            if (array.CreateFromImages(images) != Error.Ok)
            {
                Log.Warn("world", $"city ground {paint}: the floor images differ in format, the paint is kept");
                continue;
            }
            variants += images.Count;
            floors[paint] = new Floor(array, spec, sets);
        }
        if (floors.Count == 0)
        {
            Log.Info("world", $"city ground: no floor image under {dir}, {chapter}'s paint is kept");
            return null;
        }
        var resolved = Resolve(gamez, layout, out var census);
        var uv2 = new Dictionary<GameZPolygon, Vector2>(ReferenceEqualityComparer.Instance);
        foreach (var (poly, (paint, set)) in resolved)
        {
            uv2[poly] = set != null && floors.TryGetValue(paint, out var floor) && floor.Sets.TryGetValue(set, out var at)
                ? at : Vector2.Zero;
        }
        var ground = new CityGround(floors, uv2, missing);
        Follow();
        Log.Info("world",
            $"city ground: {variants} floor variant(s) for {string.Join(", ", floors.Keys)} from {dir}; polygons floored={ground.Floored} other_frame={census.OtherFrame} two_frames={census.TwoFrames} kept={resolved.Count - ground.Floored - census.OtherFrame - census.TwoFrames}; paint kept for {(missing.Count == 0 ? "none" : string.Join(", ", missing))}; {clock.Elapsed.TotalMilliseconds:0} ms");
        return ground;
    }

    /// <summary>Whether polygons wearing <paramref name="texture"/> take the floor arm: a paint with a floor image.</summary>
    public bool Paints(string? texture) => texture != null && _floors.ContainsKey(Stem(texture));

    /// <summary>The UV2 a polygon of a paint carries: its combination's first layer and variant count, zero to keep
    /// the paint.</summary>
    public Vector2 Uv2Of(GameZPolygon poly) => _uv2.TryGetValue(poly, out var at) ? at : Vector2.Zero;

    /// <summary>Sets the paint's floor uniforms on <paramref name="mat"/>.</summary>
    public void Bind(ShaderMaterial mat, string texture)
    {
        if (!_floors.TryGetValue(Stem(texture), out var floor))
            return;
        var streets = floor.Spec.Streets;
        mat.SetShaderParameter("city_floor_tex", floor.Array);
        for (int i = 0; i < 2; i++)
        {
            mat.SetShaderParameter($"city_floor_street{i}", i < streets.Count
                ? new Vector4(streets[i].Axis.X, streets[i].Axis.Y, streets[i].Centre, streets[i].Half)
                : new Vector4(0f, 0f, 0f, -1f));
        }
        var cells = floor.Spec.Cells;
        mat.SetShaderParameter("city_floor_cell", new Vector4(cells[0].Axis.X, cells[0].Axis.Y,
            cells.Count > 1 ? cells[1].Axis.X : 0f, cells.Count > 1 ? cells[1].Axis.Y : 0f));
        mat.SetShaderParameter("city_floor_origin", new Vector2(cells[0].Origin, cells.Count > 1 ? cells[1].Origin : 0f));
    }

    /// <summary>Each polygon of a paint in <paramref name="layout"/> reached from <paramref name="worldName"/>, with the
    /// set standing on it. A clear one stands its own, a flagged one the clear layer beneath that takes stamps, else
    /// <c>none</c>. The set is null off the frame <see cref="InFrame"/> tests, or where two placements of its mesh
    /// disagree. Pure, so a hand-built world pins it.</summary>
    internal static Dictionary<GameZPolygon, (string Paint, string? Set)> Resolve(GameZ gamez, Layout layout, out Census census,
        string worldName = "world1")
    {
        var found = new Dictionary<GameZPolygon, (string Paint, string? Set)>(ReferenceEqualityComparer.Instance);
        var counted = new Census();
        string? TextureOf(GameZPolygon poly) =>
            poly.MaterialIndex >= 0 && poly.MaterialIndex < gamez.Materials.Count && gamez.Materials[poly.MaterialIndex].TextureName is { } t
                ? Stem(t) : null;

        // Every clear polygon of a layout texture, as world triangles: the layers a flagged paint can stand over.
        var known = new HashSet<string>(layout.Paints.Keys.Concat(layout.Paints.Values.SelectMany(p => p.Sets.Select(s => s.Set))), StringComparer.OrdinalIgnoreCase);
        var bases = new List<(Vector3 A, Vector3 B, Vector3 C, string Texture)>();
        VegetationGround.WalkWorld(gamez, worldName, (_, mesh, xf) =>
        {
            foreach (var poly in mesh.Polygons)
            {
                if (poly.NoClutter || TextureOf(poly) is not { } tex || !known.Contains(tex))
                    continue;
                for (int i = 0; i + 2 < poly.VertexIndices.Count; i++)
                {
                    var (a, b, c) = VegetationGround.Positions(poly, i);
                    bases.Add((xf * mesh.Vertices[poly.VertexIndices[a]], xf * mesh.Vertices[poly.VertexIndices[b]],
                        xf * mesh.Vertices[poly.VertexIndices[c]], tex));
                }
            }
        });

        VegetationGround.WalkWorld(gamez, worldName, (_, mesh, xf) =>
        {
            foreach (var poly in mesh.Polygons)
            {
                if (TextureOf(poly) is not { } paint || !layout.Paints.ContainsKey(paint))
                    continue;
                string? set = !InFrame(mesh, poly, xf) ? null
                    : !poly.NoClutter ? paint
                    : BaseUnder(Centre(mesh, poly, xf), bases) ?? "none";
                if (set == null)
                    counted.OtherFrame++;
                if (found.TryGetValue(poly, out var had) && had.Set != set)
                {
                    counted.TwoFrames++;
                    set = null;
                }
                found[poly] = (paint, set);
            }
        });
        census = counted;
        return found;
    }

    /// <summary>Whether every corner of <paramref name="poly"/> placed by <paramref name="xf"/> lies on the U+X V+Z frame
    /// of one 256 m repeat. Its whole UVs then fall on world multiples of 256 m: u = x / 256 + i, v = z / 256 + j.</summary>
    internal static bool InFrame(GameZMesh mesh, GameZPolygon poly, Transform3D xf)
    {
        var uvs = poly.UvCoords;
        int n = poly.VertexIndices.Count;
        if (uvs == null || uvs.Count < n || n < 3)
            return false;
        var p0 = xf * mesh.Vertices[poly.VertexIndices[0]];
        float iu = MathF.Round(uvs[0].X - (p0.X / 256f)), iv = MathF.Round(uvs[0].Y - (p0.Z / 256f));
        for (int k = 0; k < n; k++)
        {
            var p = xf * mesh.Vertices[poly.VertexIndices[k]];
            if (MathF.Abs(uvs[k].X - (p.X / 256f) - iu) > FrameSlack || MathF.Abs(uvs[k].Y - (p.Z / 256f) - iv) > FrameSlack)
                return false;
        }
        return true;
    }

    /// <summary>The file stem a texture name compares by: <c>cblock7.tif</c> is <c>cblock7</c>.</summary>
    internal static string Stem(string texture)
    {
        int dot = texture.IndexOf('.', StringComparison.Ordinal);
        return (dot < 0 ? texture : texture[..dot]).ToLowerInvariant();
    }

    /// <summary>The floor folder's <c>index.json</c>: each combination's file names, <c>combinations.&lt;paint-set&gt;.files</c>,
    /// in layer order.</summary>
    internal static Dictionary<string, List<string>> Files(string json)
    {
        var files = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(json);
        foreach (var combination in doc.RootElement.GetProperty("combinations").EnumerateObject())
        {
            if (combination.Value.TryGetProperty("files", out var named))
                files[combination.Name] = named.EnumerateArray().Select(n => n.GetString() ?? "").Where(n => n.Length > 0).ToList();
        }
        return files;
    }

    // The centre of the polygon's largest triangle in the world. A paint overlies its base wholly or not at all, so one
    // point inside it answers for the polygon.
    private static Vector3 Centre(GameZMesh mesh, GameZPolygon poly, Transform3D xf)
    {
        var best = Vector3.Zero;
        float area = -1f;
        for (int i = 0; i + 2 < poly.VertexIndices.Count; i++)
        {
            var (a, b, c) = VegetationGround.Positions(poly, i);
            Vector3 pa = xf * mesh.Vertices[poly.VertexIndices[a]], pb = xf * mesh.Vertices[poly.VertexIndices[b]], pc = xf * mesh.Vertices[poly.VertexIndices[c]];
            float twice = (pb - pa).Cross(pc - pa).Length();
            if (twice > area)
            {
                area = twice;
                best = (pa + pb + pc) / 3f;
            }
        }
        return best;
    }

    private static string? BaseUnder(Vector3 at, List<(Vector3 A, Vector3 B, Vector3 C, string Texture)> bases)
    {
        foreach (var (a, b, c, tex) in bases)
        {
            if (at.X < MathF.Min(a.X, MathF.Min(b.X, c.X)) || at.X > MathF.Max(a.X, MathF.Max(b.X, c.X))
                || at.Z < MathF.Min(a.Z, MathF.Min(b.Z, c.Z)) || at.Z > MathF.Max(a.Z, MathF.Max(b.Z, c.Z)))
                continue;
            if (VegetationGround.HeightIn(a, b, c, at) is { } y && MathF.Abs(y - at.Y) <= CoplanarM)
                return tex;
        }
        return null;
    }

    // One floor file, a PNG, as an sRGB RGB8 image with mipmaps. Null where it does not read.
    private static Image? ReadImage(string path)
    {
        var image = new Image();
        if (!FileAccess.FileExists(path) || image.LoadPngFromBuffer(FileAccess.GetFileAsBytes(path)) != Error.Ok)
            return null;
        image.Convert(Image.Format.Rgb8);
        image.GenerateMipmaps();
        return image;
    }

    private static string Rooted(string folder) =>
        folder.Contains("://", StringComparison.Ordinal) || System.IO.Path.IsPathRooted(folder) ? folder : System.IO.Path.GetFullPath(folder);

    /// <summary>A line family in UV: <c>c = dot(Axis, uv)</c>, with a street band's centre and half-width, or a variant
    /// cell's origin.</summary>
    internal readonly record struct Family(Vector2 Axis, float Centre, float Half, float Origin);

    /// <summary>The polygons <see cref="Resolve"/> kept on the paint: laid in another frame, or placed twice and
    /// disagreeing.</summary>
    internal sealed class Census
    {
        public int OtherFrame;
        public int TwoFrames;
    }

    /// <summary>One paint of the layout: its streets, its cell families and the sets standing on it. Each set carries
    /// the share of its footprint inside the streets.</summary>
    internal sealed record PaintLayout(List<Family> Streets, List<Family> Cells, List<(string Set, float OnStreet)> Sets);

    /// <summary><c>ground_layout.json</c>'s paints that carry sets; a base paint with none takes no floor.</summary>
    internal sealed class Layout
    {
        public Dictionary<string, PaintLayout> Paints { get; } = new(StringComparer.OrdinalIgnoreCase);

        public static Layout Parse(string json)
        {
            var layout = new Layout();
            using var doc = JsonDocument.Parse(json);
            foreach (var paint in doc.RootElement.GetProperty("paints").EnumerateObject())
            {
                if (!paint.Value.TryGetProperty("sets", out var sets))
                    continue;
                var streets = paint.Value.GetProperty("streets").EnumerateArray()
                    .Select(s => new Family(Axis(s.GetProperty("family").GetString()), s.GetProperty("centre").GetSingle(), s.GetProperty("half").GetSingle(), 0f))
                    .ToList();
                var cell = paint.Value.GetProperty("cell");
                var origins = cell.GetProperty("origin").EnumerateArray().Select(o => o.GetSingle()).ToList();
                var cells = cell.GetProperty("family").EnumerateArray().Select((f, i) => new Family(Axis(f.GetString()), 0f, 0f, origins[i])).ToList();
                var standing = sets.EnumerateArray().Select(s => (s.GetProperty("set").GetString() ?? "", s.GetProperty("on_street").GetSingle())).ToList();
                layout.Paints[paint.Name] = new PaintLayout(streets, cells, standing);
            }
            return layout;
        }

        private static Vector2 Axis(string? family) => family switch
        {
            "u" => new Vector2(1f, 0f),
            "v" => new Vector2(0f, 1f),
            "u+v" => new Vector2(1f, 1f),
            "u-v" => new Vector2(1f, -1f),
            _ => throw new FormatException($"ground layout: unknown family '{family}'"),
        };
    }

    private sealed record Floor(Texture2DArray Array, PaintLayout Spec, Dictionary<string, Vector2> Sets);
}
