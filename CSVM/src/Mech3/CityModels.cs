using System;
using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The Enhanced city: a building model's script-built look-alikes, read from
/// <c>data/city_models/&lt;chapter&gt;/&lt;model&gt;.glb.bin</c>. A hull file holds a far box and the near variants of
/// one building model. The kind's own cells draw the box in place of the original mesh (<see cref="ClutterInstances"/>).
/// Inside the 3D Vegetation band a near variant takes over, by the vegetation models' dither. The original mesh keeps
/// its collision, and the stamp's custom data w carries the building's seed (Mech3.md).
/// </summary>
public static class CityModels
{
    /// <summary>The folder the shipped hull files stand in, one subfolder per chapter.</summary>
    public const string ShippedRoot = "res://data/city_models";

    /// <summary>TUNE. The edge of the square cells the near variants are cut into, each cell one node per variant.</summary>
    public const float CellM = 512f;

    private static readonly Dictionary<string, Hull?> Hulls = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<bool, Shader> Shaders = new();
    private static string _root = ShippedRoot;

    /// <summary>Whether the city is drawn: Rich vegetation under Enhanced, and a 3D Vegetation word other than Off.</summary>
    public static bool On => VegetationQualitySetting.RichOn && VegetationModels.On;

    /// <summary>Gets or sets the folder <see cref="For"/> reads, <see cref="ShippedRoot"/> outside a suite. A suite's
    /// fixture hulls stand in its scratch folder. Setting it forgets every hull read.</summary>
    internal static string Root
    {
        get => _root;
        set
        {
            _root = value;
            Hulls.Clear();
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
    /// <paramref name="chapter"/>, read once. No file gives null; a file that is not a hull is warned and gives null.</summary>
    public static Hull? For(string chapter, string model)
    {
        string key = $"{chapter.ToUpperInvariant()}/{model}";
        if (!Hulls.TryGetValue(key, out var hull))
            Hulls[key] = hull = Load($"{_root}/{key}.glb.bin");
        return hull;
    }

    // The mesh nodes `far` and `near0`, `near1`... by node name, each unmoved. The far mesh's baseColorFactor is the
    // grade, which Godot's import holds in sRGB. A file without `far` is not a hull.
    private static Hull? Load(string path)
    {
        if (!FileAccess.FileExists(path))
            return null;
        var state = new GltfState();
        ArrayMesh? box = null;
        var near = new SortedDictionary<int, ArrayMesh>();
        var grade = Colors.White;
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
                    grade = material.AlbedoColor.SrgbToLinear();
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
        var hull = new Hull(box!, variants, Material(false, grade), Material(true, grade));
        Log.Info("world", $"city model {path}: far {box!.GetFaces().Length / 3} triangles, {variants.Count} near variant(s), grade {grade.R:0.###} {grade.G:0.###} {grade.B:0.###}");
        return hull;
    }

    private static ShaderMaterial Material(bool near, Color grade)
    {
        if (!Shaders.TryGetValue(near, out var shader))
            Shaders[near] = shader = new Shader { Code = ShaderCode(near) };
        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("grade", new Vector3(grade.R, grade.G, grade.B));
        material.SetShaderParameter(VegetationModels.BandParam, VegetationModels.Band);
        return material;
    }

    // The clutter's Enhanced lit arm on a flat graded colour: the world light, the ambient fill, the terrain shadow and
    // the night darkening through FOG. The box fades out over the band by ModelShare. The near model keeps exactly the
    // dither cells the box gives up, so a stamp never draws both or neither. Both keep the kind's own far fade.
    // ⚠ Never include csky_instance_uniforms: every near cell would take 16 slots of Godot's global buffer.
    private static string ShaderCode(bool near)
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

            uniform vec3 grade = vec3(1.0);
            uniform vec2 {{VegetationModels.BandParam}};
            varying flat float v_far;
            varying flat float v_box;
            varying flat float v_dark;

            void vertex() {
                vec3 origin = MODEL_MATRIX[3].xyz;
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
                ALBEDO = grade;
                ROUGHNESS = 1.0;
                METALLIC = 0.0;
                SPECULAR = 0.0;
                EMISSION = ALBEDO * csky_gi_ambient_fill{{ao}};
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

    /// <summary>One building model's hull: its far box, its near variants in name order (none draws the box at every
    /// distance), and the materials drawing each.</summary>
    public sealed record Hull(ArrayMesh Box, IReadOnlyList<ArrayMesh> Near, ShaderMaterial BoxMaterial, ShaderMaterial NearMaterial);
}
