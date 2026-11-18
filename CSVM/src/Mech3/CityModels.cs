using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The Enhanced city: a building model's script-built look-alikes, read from
/// <c>data/city_models/&lt;chapter&gt;/&lt;model&gt;.glb.bin</c>. A hull file holds a far box and the near variants of
/// one building model. The kind's own cells draw the box in place of the original mesh (<see cref="ClutterInstances"/>).
/// Inside the 3D Scenery band a near variant takes over, by the vegetation models' dither. The original mesh keeps
/// its collision, and the stamp's custom data w carries the building's seed (Mech3.md).
/// </summary>
public static class CityModels
{
    /// <summary>The folder the shipped hull files stand in, one subfolder per chapter.</summary>
    public const string ShippedRoot = "res://data/city_models";

    /// <summary>TUNE. The edge of the square cells the near variants are cut into, each cell one node per variant.
    /// Every node costs process time whether drawn or not, so the cells are large. The shader hands over per building,
    /// so the edge moves no hand-over, only how many off-band stamps a drawn cell submits.</summary>
    public const float CellM = 2048f;

    /// <summary>The material parameter that tones a hull's boards, rgb a gain and w their saturation. Only a hull put on
    /// <see cref="Tone"/> reads it.</summary>
    public const string BoardToneParam = "board_tone";

    /// <summary>The toned shader's one addition, before a board returns: its colour desaturated to
    /// <see cref="BoardToneParam"/>'s w and scaled by its rgb. Only a hull put on <see cref="Tone"/> takes it, so the
    /// buildings keep their shader text.</summary>
    internal const string ToneLine =
        "board = mix(vec3(dot(board, vec3(0.299, 0.587, 0.114))), board, " + BoardToneParam + ".w) * " + BoardToneParam + ".rgb;\n                    ";

    // The facade atlas's layout, city_atlas.py's. Each family is a row of cells: its facade rows, its roof, its metal.
    // A cell is a tile inside a wrapped gutter, and a tile is two floors and about eight windows wide.
    private const int Atlas = 1024, Cell = 160, Gutter = 16, Tile = 128, Variants = 4, Floors = 2, WindowsPerTile = 8;

    // TUNE. The share of its windows a building lights, rolled per building between the two.
    private const float LitShareMin = 0.15f, LitShareMax = 0.70f;

    // TUNE. How far a building's tint leans warm or cool: a gain on red and its opposite on blue, rolled within
    // plus or minus this.
    private const float TintSpread = 0.10f;

    // TUNE. How far a building's brightness strays from its grade, rolled within plus or minus this share.
    private const float BrightnessSpread = 0.20f;

    // TUNE. The share of a building's floors lit whole, over its single windows.
    private const float LitFloorShare = 0.12f;

    // TUNE. The emission of clear lit glass, before its light colour. FOG darkens it with the rest of the building.
    private const float WindowGain = 0.4f;

    // TUNE. The brightening of surface class 3, a walk-up's cornice line on the ungraded light metal cell. It reads
    // near-white like the original's dentil line at night. An albedo gain: it emits nothing.
    private const float CorniceGain = 2.5f;

    // city_atlas.py's board strips. Landscape posters run along the bottom. Portrait posters, the plank back and the
    // brick gable run down the right.
    private const int StripY = 960, StripX = 960, StripGutter = 8, LandW = 96, LandH = 48, PortW = 48, PortH = 72;
    private const int LandPosters = 6, PortPosters = 4;

    // The chapter's own posters, copied at run time from the textures the game loads into empty strip cells. The
    // landscape ones follow ours; the portrait one stands at PortExtra, after the back, gable and lattice.
    private const int PortExtra = PortPosters + 3;

    // TUNE. The albedo gain of a poster, class 4's formats 0 and 1. Its raw luma on a night frame matches the
    // original's lit poster at the same pose. Like CorniceGain it emits nothing.
    private const float PosterGain = 1.25f;

    // TUNE. Where single windows fall under a pixel, the wall lights points instead, cells about FarPointPx pixels
    // wide. A cell lights by a roll against the lit share times FarPointShare, at FarPointGain of a lit window.
    // An even mean glow there read as a beige wash, since night exposure pushes a resolved window into the tonemap.
    private const float FarPointPx = 1.5f, FarPointShare = 0.05f, FarPointGain = 0.4f;

    // TUNE. Under FilterPx pixels a window, the single windows average FilterTaps by FilterTaps taps across the pixel.
    // A nearest window id is a step, and under TAA's jitter a step a few pixels wide flickers.
    private const float FilterPx = 4f;
    private const int FilterTaps = 3;

    // TUNE. The single windows fade to the far points from HandOverPx + 1 pixels a window down to HandOverPx. Filtered,
    // they carry their whole mean glow, so a lower hand-over brightens the far towers toward the beige wash.
    private const float HandOverPx = 0.75f;

    // TUNE. The seconds between a window's chances to change, and a floor's. Each divides ShaderTime.RolloverSecs,
    // so the clock's wrap changes nothing.
    private const int WindowPeriodS = 300, FloorPeriodS = 360;

    // The first family with a tile roof (city_atlas.py's TILE_ROOF); every family from it up has one.
    private const int TileRoofFamily = 4;

    // TUNE. A lattice texel darker than this linear luma is a hole: half the light metal bars city_atlas.py paints on black.
    private const float CutLuma = 0.075f;

    // The chapter's own posters by texture name, landscape and portrait, in the order their cells take them.
    private static readonly string[] LandOriginals = { "poster02", "poster04" }, PortOriginals = { "poster03" };

    // TUNE. The lights a lit window takes, linear, each with its share of the windows: warm yellow mostly.
    private static readonly (Vector3 Rgb, float Share)[] Lights =
    {
        (new Vector3(1.00f, 0.70f, 0.36f), 0.50f),
        (new Vector3(1.00f, 0.55f, 0.24f), 0.20f),
        (new Vector3(0.95f, 0.88f, 0.72f), 0.20f),
        (new Vector3(0.70f, 0.80f, 1.00f), 0.10f),
    };

    // TUNE. The tile roofs' colours with their shares, sRGB as they show beside the first, the atlas's weathered brown.
    // Then terracotta, dark slate, blue-grey slate, green-grey and near-black tar. A roof takes its colour's ratio to
    // the brown, so the tiles and the hull's grade hold.
    private static readonly (Vector3 Rgb, float Share)[] Roofs =
    {
        (new Vector3(126f, 98f, 76f) / 255f, 0.30f),
        (new Vector3(134f, 88f, 68f) / 255f, 0.20f),
        (new Vector3(80f, 80f, 84f) / 255f, 0.15f),
        (new Vector3(92f, 98f, 108f) / 255f, 0.15f),
        (new Vector3(94f, 102f, 88f) / 255f, 0.10f),
        (new Vector3(52f, 50f, 48f) / 255f, 0.10f),
    };

    private static readonly Dictionary<string, Hull?> Hulls = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Facade?> Facades = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<(bool Near, bool Cut, bool Toned), Shader> Shaders = new();
    private static string _root = ShippedRoot;

    /// <summary>Whether the city is drawn: Rich vegetation under Enhanced, and a 3D Scenery word other than Off.</summary>
    public static bool On => VegetationQualitySetting.RichOn && VegetationModels.On;

    /// <summary>Gets or sets the folder <see cref="For"/> reads, <see cref="ShippedRoot"/> outside a suite. A suite's
    /// fixture hulls stand in its scratch folder. Setting it forgets every hull and facade read.</summary>
    internal static string Root
    {
        get => _root;
        set
        {
            _root = value;
            Hulls.Clear();
            Facades.Clear();
        }
    }

    /// <summary>A building's seed: a 24-bit hash of its stamped point's X and Z bits, an integer a float holds
    /// exactly. The near variant, the far box and the map edge's copies all read it from the stamp's custom data.</summary>
    public static float Seed(Vector3 point)
    {
        uint h = (BitConverter.SingleToUInt32Bits(point.X) * 0x9E3779B1u) ^ (BitConverter.SingleToUInt32Bits(point.Z) * 0x85EBCA77u);
        h ^= h >> 16;
        h *= 0x7FEB352Du;
        h ^= h >> 15;
        return h >> 8;
    }

    /// <summary>Which of <paramref name="count"/> near variants the building of <paramref name="seed"/> draws.</summary>
    public static int VariantOf(float seed, int count) => count > 1 ? (int)((uint)seed % (uint)count) : 0;

    /// <summary>The hull of <paramref name="model"/> (the decoration's name without <c>.flt</c>) on
    /// <paramref name="chapter"/>, read once. No file gives null; a file that is not a hull is warned and gives null.
    /// The chapter's texture lookup <paramref name="textures"/> lends the facade its own posters.</summary>
    public static Hull? For(string chapter, string model, Func<string, Image?>? textures = null)
    {
        string folder = $"{_root}/{chapter.ToUpperInvariant()}";
        string key = $"{folder}/{model}";
        if (!Hulls.TryGetValue(key, out var hull))
        {
            if (!Facades.TryGetValue(folder, out var facade) && FileAccess.FileExists($"{key}.glb.bin"))
                Facades[folder] = facade = LoadFacade(folder, textures);
            Hulls[key] = hull = Load($"{key}.glb.bin", facade);
        }
        return hull;
    }

    /// <summary>Puts <paramref name="hull"/>'s two materials on the toned twin of their shaders and sets
    /// <see cref="BoardToneParam"/> to <paramref name="tone"/>: rgb a gain on its boards, w their saturation.</summary>
    public static void Tone(Hull hull, Vector4 tone)
    {
        foreach (var (material, near) in new[] { (hull.BoxMaterial, false), (hull.NearMaterial, true) })
        {
            bool cut = material.Shader == ShaderFor(near, true, false) || material.Shader == ShaderFor(near, true, true);
            material.Shader = ShaderFor(near, cut, true);
            material.SetShaderParameter(BoardToneParam, tone);
        }
    }

    /// <summary>The cut-out shader's one addition: a board of the lattice format (class 4, format 4 and up) drops a
    /// texel darker than <see cref="CutLuma"/>. Only a hull whose material is named <c>*.cutout</c> takes it, so the
    /// buildings keep their shader text.</summary>
    internal static string CutCode() =>
        $"if (UV2.y > 3.5 && UV2.x > 3.5 && dot(ALBEDO, vec3(0.299, 0.587, 0.114)) < {F(CutLuma)}) {{ discard; }}";

    /// <summary>The GLSL that expands a building's seed into its look. The box and the near model share it word for
    /// word, so neither changes at the hand-over. A <paramref name="toned"/> look tones its boards.</summary>
    internal static string LookCode(bool toned = false)
    {
        var brown = Roofs[0].Rgb;
        var roofGains = Array.ConvertAll(Roofs, r => (Linear(r.Rgb) / Linear(brown), r.Share));
        return $$"""
            const float LIT_FLOORS = {{F(LitFloorShare)}};

            uint csky_city_hash(uint x) {
                x ^= x >> 16u;
                x *= 0x7feb352du;
                x ^= x >> 15u;
                x *= 0x846ca68bu;
                x ^= x >> 16u;
                return x;
            }

            float csky_city_rand(uint x) {
                return float(csky_city_hash(x) >> 8u) * (1.0 / 16777216.0);
            }

            {{Pick("csky_city_light", Lights)}}
            {{Pick("csky_city_roof", roofGains)}}

            // One level of the far points: cells of 1 m square doubled per level, in tile coordinates, so each point
            // stays on its building. A cell lights whole, a point of about FarPointPx pixels.
            vec3 csky_city_points(vec2 uv, float level, uint salt, float share) {
                vec2 cell = floor(uv / ({{F(1f / WindowsPerTile)}} * exp2(level)));
                uint h = csky_city_hash(salt ^ csky_city_hash(uint(int(cell.x)) ^ csky_city_hash(uint(int(cell.y)) ^ (uint(level) << 28u))));
                return step(csky_city_rand(h), share * {{F(FarPointShare)}}) * csky_city_light(csky_city_rand(h + 1u));
            }

            // The single windows at one point of a facade cell: the window under it, lit or dark by its roll.
            vec3 csky_city_window(vec2 uv, vec2 cell, uint salt, float share) {
                vec4 ids = textureLod(facade_ids, (cell + vec2(fract(uv.x), 1.0 - fract(uv.y)) * {{Tile}}.0) / {{Atlas}}.0, 0.0);
                uint level = uint(int(floor(uv.y)) * {{Floors}} + int(ids.g * 255.0 + 0.5));
                uint level_key = csky_city_hash(salt ^ (level + 0x9e3779b9u));
                uint win = csky_city_hash(salt ^ csky_city_hash(uint(int(floor(uv.x))) ^ csky_city_hash(uint(ids.r * 255.0 + 0.5) ^ (level << 8u))));
                // Each window and each floor rolls again once a period on the clock, at an offset of its own.
                uint win_epoch = uint(floor(csky_time / {{WindowPeriodS}}.0 + csky_city_rand(win + 3u))) % {{(int)ShaderTime.RolloverSecs / WindowPeriodS}}u;
                uint floor_epoch = uint(floor(csky_time / {{FloorPeriodS}}.0 + csky_city_rand(level_key + 3u))) % {{(int)ShaderTime.RolloverSecs / FloorPeriodS}}u;
                float on = max(step(csky_city_rand(win ^ csky_city_hash(win_epoch)), share),
                    step(csky_city_rand(level_key ^ csky_city_hash(floor_epoch + 977u)), LIT_FLOORS));
                return csky_city_light(csky_city_rand(win + 1u)) * (0.7 + 0.6 * csky_city_rand(win + 2u)) * ids.b * on;
            }

            // The building's facade from the atlas (city_atlas.py's layout) and its lit glass, from the seed.
            vec3 csky_city_look(uint seed, vec2 uv, vec2 uv2, vec3 grade_rgb, out vec3 glow) {
                uint s = seed * 8u;
                float row = floor(csky_city_rand(s + 1u) * {{Variants}}.0);
                float family = floor(uv2.x + 0.5);
                bool roof = uv2.y > 0.5;
                // Class 2 is light metal, the cell after the roof, ungraded so a frame stays pale on a dark building.
                bool metal = uv2.y > 1.5;
                vec2 cell = vec2(roof ? {{Variants}}.0 + float(metal) : row, family) * {{Cell}}.0 + {{Gutter}}.0;
                vec2 st = (cell + vec2(fract(uv.x), 1.0 - fract(uv.y)) * {{Tile}}.0) / {{Atlas}}.0;
                vec2 k = vec2({{Tile}}.0, -{{Tile}}.0) / {{Atlas}}.0;
                vec2 du = dFdx(uv), dv = dFdy(uv);
                vec3 albedo = textureGrad(facade_tex, st, du * k, dv * k).rgb;
                // Class 4 is a board, its format in TEXCOORD_1.x. Formats 0 and 1 are a poster the seed picks, shown
                // whole; 2 a board's back and 3 a brick gable, tiled. Ungraded, it keeps its colour on any building.
                if (uv2.y > 3.5) {
                    float design = floor(csky_city_rand(s + 7u) * (family < 0.5 ? poster_counts.x : poster_counts.y));
                    // A portrait design past ours is the chapter's own poster, in its cell after the lattice.
                    float index = family < 1.5
                        ? (family > 0.5 && design > {{PortPosters}}.0 - 0.5 ? {{PortExtra}}.0 : design)
                        : {{PortPosters}}.0 + family - 2.0;
                    vec2 size = family < 0.5 ? vec2({{LandW}}.0, {{LandH}}.0) : vec2({{PortW}}.0, {{PortH}}.0);
                    vec2 origin = family < 0.5
                        ? vec2(index * {{LandW + 2 * StripGutter}}.0 + {{StripGutter}}.0, {{StripY + StripGutter}}.0)
                        : vec2({{StripX + StripGutter}}.0, index * {{PortH + 2 * StripGutter}}.0 + {{StripGutter}}.0);
                    vec2 f = family < 1.5 ? clamp(uv, 0.0, 1.0) : fract(uv);
                    vec2 bk = vec2(size.x, -size.y) / {{Atlas}}.0;
                    glow = vec3(0.0);
                    vec3 board = textureGrad(facade_tex, (origin + vec2(f.x, 1.0 - f.y) * size) / {{Atlas}}.0, du * bk, dv * bk).rgb;
                    {{(toned ? ToneLine : "")}}return board * (family < 1.5 ? {{F(PosterGain)}} : 1.0);
                }
                if (roof && !metal && family > {{F(TileRoofFamily - 0.5f)}}) {
                    albedo *= csky_city_roof(csky_city_rand(s + 6u));
                }
                // Class 3, a walk-up's cornice line, is the metal cell brightened.
                if (uv2.y > 2.5) {
                    albedo *= {{F(CorniceGain)}};
                }
                float lean = {{F(TintSpread)}} * (2.0 * csky_city_rand(s + 2u) - 1.0);
                float gain = 1.0 + {{F(BrightnessSpread)}} * (2.0 * csky_city_rand(s + 3u) - 1.0);
                glow = vec3(0.0);
                if (!roof) {
                    float share = mix({{F(LitShareMin)}}, {{F(LitShareMax)}}, csky_city_rand(s + 4u));
                    uint salt = csky_city_hash(s + 5u);
                    // Below about a pixel a window's roll would sparkle, so the wall hands over to the far points.
                    // Two levels blend by the pixel's footprint, so a level change fades instead of popping.
                    float footprint = max(max(length(du), length(dv)), 1e-6);
                    float px = {{F(1f / WindowsPerTile)}} / footprint;
                    float detail = smoothstep({{F(HandOverPx)}}, {{F(HandOverPx + 1f)}}, px);
                    int taps = px < {{F(FilterPx)}} ? {{FilterTaps}} : 1;
                    float far_level = clamp(log2({{F(FarPointPx * WindowsPerTile)}} * footprint), 0.0, 3.0);
                    float base = floor(far_level);
                    vec3 far = mix(csky_city_points(uv, base, salt, share), csky_city_points(uv, base + 1.0, salt, share), far_level - base);
                    // A window a few pixels wide averages a grid of taps across the pixel. The frame's jitter then
                    // moves a lit window's share of the pixel a little, not all or nothing.
                    vec3 near = vec3(0.0);
                    for (int j = 0; j < taps && detail > 0.0; j++) {
                        for (int i = 0; i < taps; i++) {
                            vec2 o = (vec2(float(i), float(j)) + 0.5) / float(taps) - 0.5;
                            near += csky_city_window(uv + du * o.x + dv * o.y, cell, salt, share);
                        }
                    }
                    near /= float(taps * taps);
                    glow = mix(far * {{F(FarPointGain)}}, near, detail) * {{F(WindowGain)}};
                }
                return (metal ? vec3(1.0) : grade_rgb) * albedo * gain * vec3(1.0 + lean, 1.0, 1.0 - lean);
            }
            """;
    }

    // The clutter's Enhanced lit arm on the building's look: the world light, the ambient fill, the terrain shadow and
    // the night darkening through FOG. FOG darkens the lit windows too. The box fades out over the band by
    // ModelShare, and the near model keeps exactly the dither cells the box gives up. Both keep the kind's far fade.
    // ⚠ Never include csky_instance_uniforms: every near cell would take 16 slots of Godot's global buffer.
    internal static string ShaderCode(bool near, bool cut = false, bool toned = false)
    {
        bool shadow = ShadowQualitySetting.TerrainShadows;
        string keep = near ? "step(0.004, v_far - v_box)" : "step(0.004, v_box)";
        string discard = near
            ? "!csky_clutter_dither_keep(FRAGCOORD.xy, v_far) || csky_clutter_dither_keep(FRAGCOORD.xy, v_box)"
            : "!csky_clutter_dither_keep(FRAGCOORD.xy, v_box)";
        string ao = shadow ? " * csky_cloud_ao(v_terrain_pos)" : "";
        return $$"""
            shader_type spatial;

            {{SceneBuilder.AtmosphereInclude}}
            {{SceneBuilder.ClutterFadeInclude}}
            {{(shadow ? SceneBuilder.TerrainShadowInclude + "\nvarying vec3 v_terrain_pos;" : "")}}
            {{SceneBuilder.TimeInclude}}

            uniform vec3 grade = vec3(1.0);
            uniform vec2 {{VegetationModels.BandParam}};
            uniform sampler2D facade_tex : source_color, filter_linear_mipmap, repeat_enable;
            uniform sampler2D facade_ids : hint_default_black, filter_nearest, repeat_enable;
            uniform vec2 poster_counts = vec2({{LandPosters}}.0, {{PortPosters}}.0);{{(toned ? $"\n            uniform vec4 {BoardToneParam} = vec4(1.0);" : "")}}
            varying flat float v_far;
            varying flat float v_box;
            varying flat float v_dark;
            varying flat uint v_seed;

            {{LookCode(toned)}}

            void vertex() {
                vec3 origin = MODEL_MATRIX[3].xyz;
                v_seed = uint(INSTANCE_CUSTOM.w + 0.5);
                v_far = csky_clutter_fade_alpha(origin, CAMERA_POSITION_WORLD, INSTANCE_CUSTOM);
                v_box = v_far;
                if ({{VegetationModels.BandParam}}.y > 0.0) {
                    v_box *= {{ClutterBuilder.ModelShare}};
                }
                VERTEX *= {{keep}};
                v_dark = csky_clutter_dark_lit(origin, CAMERA_POSITION_WORLD, INSTANCE_CUSTOM);
                {{(shadow ? "v_terrain_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;" : "")}}
            }

            void fragment() {
                if ({{discard}}) {
                    discard;
                }
                vec3 glow;
                ALBEDO = csky_city_look(v_seed, UV, UV2, grade, glow);{{(cut ? " " + CutCode() : "")}}
                ROUGHNESS = 1.0;
                METALLIC = 0.0;
                SPECULAR = 0.0;
                EMISSION = ALBEDO * csky_gi_ambient_fill{{ao}} + glow;
                vec3 fog_world = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
                float fog_amt = csky_fog_amount(fog_world, CAMERA_POSITION_WORLD);
                if (csky_night_fog(csky_fog_color_at(CAMERA_POSITION_WORLD))) {
                    fog_amt = max(fog_amt, v_dark);
                }
                FOG = vec4(csky_fog_color_at(CAMERA_POSITION_WORLD), fog_amt);
                {{(shadow ? "AO = csky_cloud_ao(v_terrain_pos);" : "")}}
            }
            {{(shadow ? SceneBuilder.TerrainShadowLight("csky_terrain_sun(v_terrain_pos)", 0f, "v_terrain_pos") : "")}}
            """;
    }

    private static string F(float v) => v.ToString("0.0#####", CultureInfo.InvariantCulture);

    private static string Vec(Vector3 v) => $"vec3({F(v.X)}, {F(v.Y)}, {F(v.Z)})";

    private static Vector3 Linear(Vector3 srgb) => new(Mathf.Pow(srgb.X, 2.2f), Mathf.Pow(srgb.Y, 2.2f), Mathf.Pow(srgb.Z, 2.2f));

    // A GLSL function of a roll p in [0, 1) that picks a palette entry by its share.
    private static string Pick(string name, (Vector3 Rgb, float Share)[] palette)
    {
        var code = new System.Text.StringBuilder($"vec3 {name}(float p) {{\n");
        float upTo = 0f;
        foreach (var (rgb, share) in palette)
        {
            upTo += share;
            code.Append(CultureInfo.InvariantCulture, $"    if (p < {upTo:0.0###}) {{ return {Vec(rgb)}; }}\n");
        }
        return code.Append(CultureInfo.InvariantCulture, $"    return {Vec(palette[^1].Rgb)};\n}}").ToString();
    }

    // A chapter folder's facade atlas (sRGB, mipmapped) and its window ids (linear, no mipmaps). Null, warned, where
    // either file is missing. The chapter's own posters are copied into the atlas image before its mipmaps, never into
    // a file. A poster the lookup does not give is left out of the draw.
    private static Facade? LoadFacade(string folder, Func<string, Image?>? textures)
    {
        var atlas = new Image();
        var ids = new Image();
        string atlasPath = $"{folder}/facade.png.bin", idsPath = $"{folder}/facade_ids.png.bin";
        if (!FileAccess.FileExists(atlasPath) || atlas.LoadPngFromBuffer(FileAccess.GetFileAsBytes(atlasPath)) != Error.Ok
            || !FileAccess.FileExists(idsPath) || ids.LoadPngFromBuffer(FileAccess.GetFileAsBytes(idsPath)) != Error.Ok)
        {
            Log.Warn("world", $"city facade {folder}: no readable facade.png.bin and facade_ids.png.bin, the buildings draw their flat grade");
            return null;
        }
        int land = 0, port = 0;
        foreach (string name in textures == null ? Array.Empty<string>() : LandOriginals)
        {
            if (Paste(atlas, textures!(name), (LandPosters + land) * (LandW + 2 * StripGutter), StripY, LandW, LandH))
                land++;
        }
        foreach (string name in textures == null ? Array.Empty<string>() : PortOriginals)
        {
            if (Paste(atlas, textures!(name), StripX, (PortExtra + port) * (PortH + 2 * StripGutter), PortW, PortH))
                port++;
        }
        atlas.GenerateMipmaps();
        ids.Convert(Image.Format.Rgba8);
        Log.Info("world", $"city facade {folder}: atlas {atlas.GetWidth()}x{atlas.GetHeight()}, ids {ids.GetWidth()}x{ids.GetHeight()}, {land + port} of the chapter's own poster(s)");
        return new Facade(TextureUpload.Create(atlas), TextureUpload.Create(ids), new Vector2(LandPosters + land, PortPosters + port));
    }

    // A poster stretched to a strip cell's w x h tile at (x, y), its edge texels repeated into the gutter as ours are.
    private static bool Paste(Image atlas, Image? poster, int x, int y, int w, int h)
    {
        if (poster == null || poster.IsEmpty())
            return false;
        poster.Decompress();
        poster.ClearMipmaps();
        poster.Convert(atlas.GetFormat());
        poster.Resize(w, h, Image.Interpolation.Bilinear);
        int g = StripGutter;
        for (int dy = -g; dy < h + g; dy++)
        {
            for (int dx = -g; dx < w + g; dx++)
                atlas.SetPixel(x + g + dx, y + g + dy, poster.GetPixel(Math.Clamp(dx, 0, w - 1), Math.Clamp(dy, 0, h - 1)));
        }
        return true;
    }

    // The mesh nodes `far` and `near0`, `near1`... by node name, each unmoved. The far mesh's baseColorFactor is the
    // grade, which Godot's import holds in sRGB. A file without `far` is not a hull.
    private static Hull? Load(string path, Facade? facade)
    {
        if (!FileAccess.FileExists(path))
            return null;
        var state = new GltfState();
        ArrayMesh? box = null;
        var near = new SortedDictionary<int, ArrayMesh>();
        var grade = Colors.White;
        bool cut = false;
        string? fault = new GltfDocument().AppendFromBuffer(FileAccess.GetFileAsBytes(path), string.Empty, state) != Error.Ok
            ? "not a glTF binary" : null;
        var nodes = state.GetNodes();
        var meshes = state.GetMeshes();
        foreach (var node in fault == null ? nodes : new Godot.Collections.Array<GltfNode>())
        {
            string name = node.OriginalName;
            bool far = name == "far";
            if ((!far && !(name.StartsWith("near", StringComparison.Ordinal) && int.TryParse(name.AsSpan(4), out _)))
                || node.Mesh < 0 || node.Mesh >= meshes.Count || meshes[node.Mesh].Mesh is not { } importer)
                continue;
            for (var at = node; at != null; at = at.Parent >= 0 ? nodes[at.Parent] : null)
            {
                if (!at.Xform.IsEqualApprox(Transform3D.Identity))
                    fault = $"node {name} is moved, and a hull stands in its model's frame";
            }
            var mesh = importer.GetMesh();
            if (far)
            {
                box = mesh;
                if (importer.GetSurfaceCount() > 0 && importer.GetSurfaceMaterial(0) is BaseMaterial3D material)
                {
                    grade = material.AlbedoColor.SrgbToLinear();
                    cut = material.ResourceName.EndsWith(".cutout", StringComparison.Ordinal);
                }
            }
            else
            {
                near[int.Parse(name.AsSpan(4), System.Globalization.CultureInfo.InvariantCulture)] = mesh;
            }
        }
        fault ??= box == null ? "no far node" : null;
        if (fault != null)
        {
            Log.Warn("world", $"city model {path} skipped: {fault}");
            return null;
        }
        var variants = new List<ArrayMesh>(near.Values);
        var hull = new Hull(box!, variants, Material(false, grade, facade, cut), Material(true, grade, facade, cut));
        Log.Info("world", $"city model {path}: far {box!.GetFaces().Length / 3} triangles, {variants.Count} near variant(s), grade {grade.R:0.###} {grade.G:0.###} {grade.B:0.###}{(cut ? ", cut out" : "")}");
        return hull;
    }

    private static Shader ShaderFor(bool near, bool cut, bool toned)
    {
        if (!Shaders.TryGetValue((near, cut, toned), out var shader))
            Shaders[(near, cut, toned)] = shader = new Shader { Code = ShaderCode(near, cut, toned) };
        return shader;
    }

    // Without a facade the textures stay unset: the atlas reads white, so the flat grade draws, and the ids black, so
    // no window lights.
    private static ShaderMaterial Material(bool near, Color grade, Facade? facade, bool cut)
    {
        // ⚠ Keep a building on the untoned text: its shader text is what the city's goldens pin.
        var shader = ShaderFor(near, cut, false);
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("grade", new Vector3(grade.R, grade.G, grade.B));
        material.SetShaderParameter(VegetationModels.BandParam, VegetationModels.Band);
        if (facade != null)
        {
            material.SetShaderParameter("facade_tex", facade.Albedo);
            material.SetShaderParameter("facade_ids", facade.Ids);
            material.SetShaderParameter("poster_counts", facade.PosterCounts);
        }
        return material;
    }

    /// <summary>One building model's hull: its far box, its near variants in name order (none draws the box at every
    /// distance), and the materials drawing each.</summary>
    public sealed record Hull(ArrayMesh Box, IReadOnlyList<ArrayMesh> Near, ShaderMaterial BoxMaterial, ShaderMaterial NearMaterial);

    // A chapter's facade textures.
    private sealed record Facade(Texture2D Albedo, Texture2D Ids, Vector2 PosterCounts);
}
