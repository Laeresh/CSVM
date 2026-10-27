using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Extraction;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// A listed card species' 3D look-alike, drawn under Rich on its card's stamps near the camera. It dithers
/// over to the card beyond a band and is never solid. A model pack's file comes first, then the one shipped
/// beside the clump tile (<c>data/vegetation_models/&lt;chapter&gt;/&lt;card&gt;.glb.bin</c>). A species with
/// neither draws its card as before. The model takes the card's unlit shading, graded to the card's mean
/// colour, with the card's sun-side gain read off its normals. <see cref="ClutterInstances"/> draws it.
/// Mech3.md has the rules.
/// </summary>
public static class VegetationModels
{
    /// <summary>The Near word's band: inside <see cref="NearM"/> the model alone draws, past <see cref="FarM"/> the
    /// card alone. Plain metres, so no View Distance moves the cost. It stays inside C1's card fades (500 m at
    /// the nearest) and its clump handover (1000 m). A 300 m view's nearest stamp lies past it.</summary>
    public const float NearM = 150f, FarM = 250f;

    /// <summary>The Far word's band, which a 300 m view reaches. Still inside the clump handover.</summary>
    public const float FarNearM = 250f, FarFarM = 400f;

    /// <summary>The edge of the square cells the models are cut into, so a pane reaches a handful of them.</summary>
    public const float CellM = 256f;

    /// <summary>The shader parameter the card's Rich material and the model read the band from. It is zero on
    /// a card with no model, which then draws at every distance.</summary>
    public const string BandParam = "vegetation_model_band";

    /// <summary>The sample points per triangle edge <see cref="SurfaceMean"/> reads, 21 a triangle.</summary>
    public const int SurfaceSamples = 6;

    /// <summary>The share of a tree's height, from its top, both of the grade's means read. The crown stands in it on
    /// every species, and the trunk and C3's sand patch at a palm card's foot fall below it.</summary>
    public const float CrownShare = 0.5f;

    /// <summary>How far one channel's gain may stray from the luminance ratio, so the grade keeps the model's hue.</summary>
    public const float MaxChroma = 0.1f;

    // The grade's bounds: a model whose colours are that far off the card is a bad file, not a grade.
    private const float MinGrade = 0.25f, MaxGrade = 4f;

    private static readonly Dictionary<(bool Lit, bool Fogged, bool Canopy), Shader> Shaders = new();

    /// <summary>The band both sides read under the standing <see cref="VegetationModelsSetting"/> word: near and
    /// far in metres, zero for Off.</summary>
    public static Vector2 Band => BandFor(VegetationModelsSetting.Word);

    /// <summary>Whether the standing word draws models at all.</summary>
    public static bool On => Band.Y > 0f;

    /// <summary>The band of one <see cref="VegetationModelsSetting"/> word, zero for Off.</summary>
    public static Vector2 BandFor(string word) => word switch
    {
        VegetationModelsSetting.Near => new Vector2(NearM, FarM),
        VegetationModelsSetting.Far => new Vector2(FarNearM, FarFarM),
        _ => Vector2.Zero,
    };

    /// <summary>The model of <paramref name="species"/> (the card's model name without <c>.flt</c>) on
    /// <paramref name="chapter"/>: a model pack's under the chapter key, else the shipped one, else null. A file
    /// that fails to import is warned and passed over, so a broken replacement keeps the shipped model. The
    /// grade matches <paramref name="card"/>, the card's image.</summary>
    public static Model? For(string chapter, string species, Image? card)
    {
        card?.Convert(Image.Format.Rgba8);
        return ForMean(chapter, species, card != null ? MeanLinear(card.GetData(), card.GetWidth(), card.GetHeight(), CrownShare) : null);
    }

    /// <summary>The model as <see cref="For(string, string, Image?)"/> reads it, graded to <paramref name="cardMean"/>,
    /// a mean linear colour, where no one card image is what the model must meet.</summary>
    public static Model? ForMean(string chapter, string species, Vector3? cardMean)
    {
        string key = $"{chapter.ToUpperInvariant()}/{species}";
        string shipped = $"{CanopyClumps.ShippedRoot}/{key}.glb.bin";
        var sources = new[]
        {
            HdModels.Current.Vegetation(key),
            FileAccess.FileExists(shipped) ? (new HdModel(FileAccess.GetFileAsBytes(shipped), true, null), shipped) : null,
        };
        foreach (var source in sources)
        {
            if (source is { } s && Import(s.Model, s.Source, cardMean) is { } model)
                return model;
        }

        return null;
    }

    /// <summary>A material drawing <paramref name="model"/> as a card of the given render flags and width in
    /// metres draws. A <paramref name="canopy"/> model stands on no card, so no card fade bounds it. Its
    /// instance basis scales it, and its band hands it over to the crowns of <see cref="CanopyClumps"/>.</summary>
    public static ShaderMaterial Material(Model model, bool lit, bool fogged, float cardWidth, bool canopy = false)
    {
        if (!Shaders.TryGetValue((lit, fogged, canopy), out var shader))
            Shaders[(lit, fogged, canopy)] = shader = new Shader { Code = ShaderCode(lit, fogged, canopy) };
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("albedo_tex", model.Albedo);
        material.SetShaderParameter("grade", model.Grade);
        material.SetShaderParameter("half_width", Math.Max(cardWidth / 2f, 0.1f));
        material.SetShaderParameter(BandParam, Band);
        return material;
    }

    /// <summary>The gain putting the model's mean linear colour on the card's. It is the luminance ratio, each
    /// channel at most <see cref="MaxChroma"/> off it, all held to 0.25-4. The model keeps its own hue and
    /// meets the card in brightness. One where either mean is unknown. The card draws unlit, so its texture's
    /// mean is the colour the model must meet.</summary>
    public static Vector3 Grade(Vector3? card, Vector3? model)
    {
        if (card is not { } c || model is not { } m)
            return Vector3.One;
        float s = Math.Clamp(Luma(c) / Math.Max(Luma(m), 1e-6f), MinGrade, MaxGrade);
        float Channel(float a, float b) => b > 1e-6f ? Math.Clamp(s * Math.Clamp(a / b / s, 1f - MaxChroma, 1f + MaxChroma), MinGrade, MaxGrade) : s;
        return new Vector3(Channel(c.X, m.X), Channel(c.Y, m.Y), Channel(c.Z, m.Z));
    }

    /// <summary>The mean linear colour of an RGBA8 image's solid texels (alpha at least half) in the top
    /// <paramref name="crownShare"/> of the rows they span, null with none. Row 0 is the card's top.</summary>
    public static Vector3? MeanLinear(byte[] rgba, int width, int height, float crownShare = 1f)
    {
        int top = -1, bottom = -1;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (rgba[(((y * width) + x) * 4) + 3] < 128)
                    continue;
                top = top < 0 ? y : top;
                bottom = y;
                break;
            }
        }
        if (top < 0)
            return null;
        int last = top + (int)MathF.Ceiling((bottom - top + 1) * crownShare) - 1;
        var sum = Vector3.Zero;
        long n = 0;
        for (int i = top * width * 4; i < (last + 1) * width * 4; i += 4)
        {
            if (rgba[i + 3] < 128)
                continue;
            sum += new Vector3(Linear(rgba[i]), Linear(rgba[i + 1]), Linear(rgba[i + 2]));
            n++;
        }

        return n == 0 ? null : sum / n;
    }

    /// <summary>The mean linear colour of a mesh's solid surface: each triangle's texels on a fixed grid of
    /// <see cref="SurfaceSamples"/> points, weighted by its world area. A texture's unused texels count nothing,
    /// so an atlas region the mesh barely uses moves it little. Only triangles centred in the top
    /// <paramref name="crownShare"/> of the mesh's height count. Null with no solid sample.</summary>
    public static Vector3? SurfaceMean(byte[] rgba, int width, int height, Vector3[] positions, Vector2[] uvs, int[] indices,
        float crownShare = 1f)
    {
        const int n = SurfaceSamples;
        if (uvs.Length < positions.Length || rgba.Length < width * height * 4 || positions.Length == 0)
            return null;
        float minY = positions.Min(p => p.Y), maxY = positions.Max(p => p.Y);
        float floor = maxY - ((maxY - minY) * crownShare) - 1e-4f;
        var sum = Vector3.Zero;
        double weight = 0;
        for (int t = 0; t + 2 < indices.Length; t += 3)
        {
            Vector3 a = positions[indices[t]], b = positions[indices[t + 1]], c = positions[indices[t + 2]];
            if ((a.Y + b.Y + c.Y) / 3f < floor)
                continue;
            Vector2 ua = uvs[indices[t]], ub = uvs[indices[t + 1]], uc = uvs[indices[t + 2]];
            float share = 0.5f * (b - a).Cross(c - a).Length() / (n * (n + 1) / 2);
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; i + j < n; j++)
                {
                    var uv = ua + ((i + (1f / 3f)) / n * (ub - ua)) + ((j + (1f / 3f)) / n * (uc - ua));
                    int x = Math.Clamp((int)(Wrap(uv.X) * width), 0, width - 1), y = Math.Clamp((int)(Wrap(uv.Y) * height), 0, height - 1);
                    int at = ((y * width) + x) * 4;
                    if (rgba[at + 3] < 128)
                        continue;
                    sum += share * new Vector3(Linear(rgba[at]), Linear(rgba[at + 1]), Linear(rgba[at + 2]));
                    weight += share;
                }
            }
        }

        return weight > 0 ? sum / (float)weight : null;
    }

    // Rec. 709 luminance of a linear colour.
    private static float Luma(Vector3 v) => (0.2126f * v.X) + (0.7152f * v.Y) + (0.0722f * v.Z);

    private static float Wrap(float v) => v - MathF.Floor(v);

    private static float Linear(byte v)
    {
        double c = v / 255.0;
        return (float)(c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4));
    }

    // The first mesh of the file and its first surface's base-colour texture, mipmapped. A model is one mesh of
    // one material, as the pipeline writes it.
    private static Model? Import(HdModel file, string source, Vector3? cardMean)
    {
        var state = new GltfState();
        if (new GltfDocument().AppendFromBuffer(file.Bytes, file.GltfPath ?? string.Empty, state) != Error.Ok
            || state.GetMeshes() is not { Count: > 0 } meshes || meshes[0].Mesh is not { } importer
            || importer.GetSurfaceCount() == 0 || importer.GetSurfaceMaterial(0) is not BaseMaterial3D { AlbedoTexture: { } albedo })
        {
            Log.Warn("world", $"vegetation model {source} skipped: not one textured glTF mesh");
            return null;
        }
        var image = albedo.GetImage();
        // A clear texel's black would bleed into the foliage's mips and darken the model as it recedes.
        image.ClearMipmaps();
        image.Convert(Image.Format.Rgba8);
        CanopyClumps.Bleed(image);
        image.GenerateMipmaps();
        var mesh = importer.GetMesh();
        var arrays = mesh.SurfaceGetArrays(0);
        var grade = Grade(cardMean,
            SurfaceMean(image.GetData(), image.GetWidth(), image.GetHeight(), arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array(),
                arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array(), arrays[(int)Mesh.ArrayType.Index].AsInt32Array(), CrownShare));
        Log.Info("world", $"vegetation model {source}: {mesh.GetFaces().Length / 3} triangles, grade {grade.X:0.###} {grade.Y:0.###} {grade.Z:0.###}");
        return new Model(mesh, TextureUpload.Create(image), grade, source);
    }

    // The card's look on a mesh, under the card's own far fade (none on a canopy model). Over the band the model
    // keeps the dither cells the card or the crowns give up, so a stamp never draws both or neither. It turns by
    // a hash of its planted point, mirrors as its card does, leans in the card's breeze and gains on its sun half.
    // ⚠ Never light it with the scene's lights: the card is unlit, and a lit model shows at the band.
    // ⚠ Never give it an instance uniform: each model cell would take 16 slots of Godot's global buffer.
    private static string ShaderCode(bool lit, bool fogged, bool canopy) => $$"""
        shader_type spatial;
        render_mode skip_vertex_transform, unshaded, cull_disabled, shadows_disabled{{SceneBuilder.CoverageMode}};

        uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_enable;
        uniform vec3 grade = vec3(1.0);
        uniform vec2 {{BandParam}};

        #include "res://shaders/csky_atmosphere.gdshaderinc"
        #include "res://shaders/csky_srgb.gdshaderinc"
        #include "res://shaders/csky_clutter_fade.gdshaderinc"
        #include "res://shaders/csky_mip_bias.gdshaderinc"
        #include "res://shaders/csky_time.gdshaderinc"

        const float csky_fog_on = 1.0;
        uniform float half_width = 1.0;

        varying flat float v_far;
        varying flat float v_card;
        varying float v_side;
        varying flat float v_sun;

        void vertex() {
            vec3 origin = MODEL_MATRIX[3].xyz;
            v_far = {{(canopy ? "1.0" : "csky_clutter_fade_alpha(origin, CAMERA_POSITION_WORLD, INSTANCE_CUSTOM)")}};
            v_card = v_far * {{ClutterBuilder.ModelShare}};
            float live = step(1e-12, dot(MODEL_MATRIX[1].xyz, MODEL_MATRIX[1].xyz));
            float keep = live * step(0.004, v_far - v_card);
            uint h = (floatBitsToUint(origin.x) * 0x9E3779B1u) ^ (floatBitsToUint(origin.z) * 0x85EBCA77u);
            h ^= h >> 16u;
            h *= 0x7FEB352Du;
            h ^= h >> 15u;
            float yaw = float(h >> 8u) * (6.2831853 / 16777216.0);
            mat3 turn = mat3(vec3(cos(yaw), 0.0, -sin(yaw)), vec3(0.0, 1.0, 0.0), vec3(sin(yaw), 0.0, cos(yaw)));
            vec3 flip = vec3(MODEL_MATRIX[0].x < 0.0 ? -1.0 : 1.0, 1.0, 1.0);
            vec3 v = turn * (VERTEX * flip){{(canopy ? " * MODEL_MATRIX[1].y" : "")}};
        {{ClutterBuilder.FaceBasisLines(false)}}
            v_side = dot(v.xz, face[0].xz) / half_width;
        {{ClutterBuilder.SunLine}}{{ClutterBuilder.LeanLines}}    v += vec3(breeze.x, 0.0, breeze.y) * (lean * v.y);
            VERTEX = (VIEW_MATRIX * vec4(origin + v * keep, 1.0)).xyz;
        }

        void fragment() {
            if (!csky_clutter_dither_keep(FRAGCOORD.xy, v_far) || csky_clutter_dither_keep(FRAGCOORD.xy, v_card)) {
                discard;
            }
            vec4 col = vec4(csky_srgb_to_linear(COLOR.rgb), COLOR.a) * {{SceneBuilder.SampleAlbedo("UV")}};
            ALBEDO = col.rgb * grade{{(lit ? " * csky_world_light_at(CAMERA_POSITION_WORLD)" : "")}};
        {{ClutterBuilder.RichFragmentLines}}{{(fogged ? ClutterBuilder.FogLines : "")}}
            ALPHA = col.a;
            ALPHA_SCISSOR_THRESHOLD = 0.5;
        {{SceneBuilder.CoverageLines}}
        }
        """;

    /// <summary>One species' model: its mesh, its texture, its grade and where it was read from.</summary>
    public sealed record Model(ArrayMesh Mesh, Texture2D Albedo, Vector3 Grade, string Source);
}
