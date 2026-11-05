using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// The Enhanced cloud shadows' one owner: a node under the world root that writes the
/// <c>csky_cloud_shadow*</c> globals <c>csky_cloud_shadow.gdshaderinc</c> declares. Every arm that
/// scales its sun by <c>csky_terrain_sun</c> reads them (ground, water, buildings, vegetation), and no
/// aircraft arm does. The mission's own clouds (<see cref="CastersOf"/>) are cast along every zone sun
/// while the world loads (<see cref="Bake"/>). A zone change or a mode switch then only writes globals.
/// It draws only under Enhanced, with the Cloud Shadows word on, in a day zone whose sun casts.
/// Leaving the tree puts the off state back.
/// </summary>
internal sealed partial class CloudShadow : Node
{
    // The most texels along a mask's side once averaged down. A card covers a handful of map texels, so
    // more detail would not reach the map.
    private const int MaskSide = 32;

    // The globals the include declares: the map, its rect on the sea-level plane, and the sun's run
    // with the darkness.
    private static readonly StringName MapParam = "csky_cloud_shadow_map";
    private static readonly StringName RectParam = "csky_cloud_shadow_rect";
    private static readonly StringName Param = "csky_cloud_shadow";

    // What the map global holds with nothing drawn: one texel letting the whole sun through.
    private static ImageTexture? _clear;
    private static bool _registered;

    // Each zone sun's upload, null for a sun that casts nothing. The float map is dropped once uploaded.
    private readonly Dictionary<Vector3, Baked?> _baked = new();
    private Vector3 _orientation;
    private bool _night;

    private CloudShadow(Vector3 orientation, bool night)
    {
        Name = "CloudShadow";
        _orientation = orientation;
        _night = night;
    }

    /// <summary>The node whose state the globals carry, null while the off state stands.</summary>
    public static CloudShadow? Live { get; private set; }

    /// <summary>The value the main global holds with nothing drawn: no run and a darkness of 0.</summary>
    public static Vector4 OffValue => Vector4.Zero;

    /// <summary>The main global's last written value. A mirror, since the renderer refuses to read a
    /// global back outside the editor; it is what a suite asserts by.</summary>
    public static Vector4 Published { get; private set; } = OffValue;

    /// <summary>The map the globals carry, null while the off state stands.</summary>
    public static Baked? PublishedMap { get; private set; }

    /// <summary>The bake's size and milliseconds per sun, for the build log line.</summary>
    public string Timing { get; private set; } = "";

    /// <summary>Whether the shadows draw now: Enhanced, the word on, a day zone, a sun whose map holds a
    /// cloud. A lookup only; every map was cast at load.</summary>
    public bool Draws => GraphicsMode.Enhanced && CloudShadowSetting.Enabled && !_night
        && _baked.TryGetValue(_orientation, out var baked) && baked != null;

    /// <summary>Declares the globals with the off state. Must run before the first shader that reads
    /// them is built, beside <see cref="TerrainShadow.RegisterGlobals"/>.</summary>
    public static void RegisterGlobals()
    {
        _clear ??= TextureUpload.Create(1, 1, Image.Format.R8, new byte[] { 255 });
        RenderingServer.GlobalShaderParameterAdd(MapParam, RenderingServer.GlobalShaderParameterType.Sampler2D, _clear);
        RenderingServer.GlobalShaderParameterAdd(RectParam, RenderingServer.GlobalShaderParameterType.Vec4, new Vector4(0f, 0f, 1f, 1f));
        RenderingServer.GlobalShaderParameterAdd(Param, RenderingServer.GlobalShaderParameterType.Vec4, OffValue);
        _registered = true;
        Published = OffValue;
        PublishedMap = null;
    }

    /// <summary>Puts the off state back, so every sun term reads exactly 1 for the clouds.</summary>
    public static void Withdraw()
    {
        Live = null;
        Write(OffValue, null);
    }

    /// <summary>Casts and uploads <paramref name="casters"/>' map under every distinct sun Euler
    /// rotation in <paramref name="orientations"/>, in either mode, so nothing is cast after load. It
    /// starts on <paramref name="orientation"/>, drawn while the zone is not <paramref name="night"/>.</summary>
    public static CloudShadow Bake(IReadOnlyList<CloudShadowRaster.Caster> casters, IEnumerable<Vector3> orientations,
        Vector3 orientation, bool night)
    {
        var result = new CloudShadow(orientation, night);
        var timing = new System.Text.StringBuilder();
        foreach (var o in orientations)
        {
            if (result._baked.ContainsKey(o))
                continue;
            long t0 = Stopwatch.GetTimestamp();
            var sun = SunOf(o);
            var map = CloudShadowRaster.Cast(casters, sun);
            result._baked[o] = map == null ? null : Upload(map, CloudShadowRaster.Run(sun));
            timing.Append(map == null
                ? string.Create(CultureInfo.InvariantCulture, $" [{Mathf.RadToDeg(o.X):0}/{Mathf.RadToDeg(o.Y):0}: none]")
                : string.Create(CultureInfo.InvariantCulture,
                    $" [{Mathf.RadToDeg(o.X):0}/{Mathf.RadToDeg(o.Y):0}: {map.Width}x{map.Height} at {map.Cell:0.#} m, {Stopwatch.GetElapsedTime(t0).TotalMilliseconds:0} ms]"));
        }
        result.Timing = timing.ToString();
        return result;
    }

    /// <summary>The world's clouds as casters: every placed cloud sprite under <paramref name="world"/>
    /// that stands visible, then every card of the cloud <paramref name="field"/>. Each takes its authored
    /// size, opacity and mask. Nothing under <paramref name="skip"/> casts: the camera-anchored domes.</summary>
    public static List<CloudShadowRaster.Caster> CastersOf(Node world, SceneBuilder scene, FogVolumeClutter? field,
        TextureArchive textures, IEnumerable<Node> skip)
    {
        var masks = new Dictionary<string, CloudShadowRaster.Mask>(StringComparer.OrdinalIgnoreCase);
        var placed = new Dictionary<Material, string>();
        foreach (var (material, texture) in scene.PlacedCloudMaterials)
            placed[material] = texture;
        var casters = new List<CloudShadowRaster.Caster>();
        if (placed.Count > 0)
        {
            Collect(world, new HashSet<Node>(skip), scene, placed, textures, masks, casters);
            // ⚠ Do not drop this collection. Reading MeshInstance3D.Mesh mints a managed wrapper per mesh
            // (see OceanMask.Bake), and a short run that quits first aborts on Godot's exit check.
            GC.Collect();
        }
        if (field != null)
        {
            foreach (var card in field.Cards())
                casters.Add(new CloudShadowRaster.Caster(card.Centre, card.Size, card.Opacity, MaskOf(card.Mask, textures, masks)));
        }
        return casters;
    }

    /// <summary>Takes a zone apply's sun orientation and day or night, written at once.</summary>
    public void FollowZone(Vector3 orientation, bool night)
    {
        _orientation = orientation;
        _night = night;
        if (Live == this)
            Publish();
    }

    public override void _EnterTree()
    {
        Live = this;
        Publish();
    }

    public override void _ExitTree()
    {
        if (Live == this)
            Withdraw();
    }

    // The word and the graphics mode are read every frame, so an Options apply or a mode switch
    // reaches the globals with no hook. An unchanged value writes nothing.
    public override void _Process(double delta) => Publish();

    // The light shines down its -Z, so +Z points at the sun, as TerrainShadow.Bake reads it.
    private static Vector3 SunOf(Vector3 orientation) => Basis.FromEuler(orientation).Z.Normalized();

    // Every mesh instance under `node` drawing a placed cloud's material, one caster per instance, its
    // card the local box's largest side. ⚠ Visibility is read once, at load: a sprite hidden then never
    // casts. No mission is known to toggle a placed cloud sprite.
    private static void Collect(Node node, HashSet<Node> skip, SceneBuilder scene, Dictionary<Material, string> placed,
        TextureArchive textures, Dictionary<string, CloudShadowRaster.Mask> masks, List<CloudShadowRaster.Caster> casters)
    {
        if (skip.Contains(node))
            return;
        if (node is MeshInstance3D mi && mi.IsVisibleInTree())
        {
            for (int s = 0; s < mi.GetSurfaceOverrideMaterialCount(); s++)
            {
                if (mi.GetActiveMaterial(s) is not { } material || !placed.TryGetValue(material, out var texture))
                    continue;
                var box = mi.GetAabb();
                var xf = mi.GlobalTransform;
                var scale = xf.Basis.Scale;
                float size = MathF.Max(box.Size.X * scale.X, MathF.Max(box.Size.Y * scale.Y, box.Size.Z * scale.Z));
                float opacity = OpacityOf(mi) * VertexAlphaOf(mi, s, scene);
                casters.Add(new CloudShadowRaster.Caster(xf * box.GetCenter(), size, opacity, MaskOf(texture, textures, masks)));
                break;
            }
        }
        foreach (var child in node.GetChildren())
            Collect(child, skip, scene, placed, textures, masks, casters);
    }

    // The animation runtime's authored OBJECT_OPACITY_STATE on the instance, 1 where none is set.
    private static float OpacityOf(MeshInstance3D mi) =>
        mi.GetInstanceShaderParameter(SceneBuilder.OpacityParam) is { VariantType: Variant.Type.Float } opacity
            ? (float)opacity.AsDouble() : 1f;

    // The surface's authored vertex alpha, averaged. The sprite shader multiplies its mask by COLOR.a, as
    // the field's card shader does. 1 where the surface carries no colours.
    private static float VertexAlphaOf(MeshInstance3D mi, int surface, SceneBuilder scene)
    {
        if (mi.Mesh is not ArrayMesh mesh || surface >= mesh.GetSurfaceCount())
            return 1f;
        var colors = scene.SurfaceArrays(mesh, surface)[(int)Mesh.ArrayType.Color];
        if (colors.VariantType != Variant.Type.PackedColorArray)
            return 1f;
        var values = colors.AsColorArray();
        if (values.Length == 0)
            return 1f;
        float sum = 0f;
        foreach (var c in values)
            sum += c.A;
        return sum / values.Length;
    }

    // The authored mask's alpha, averaged down to at most MaskSide texels a side, read once per texture.
    // A texture the archive cannot find casts as a solid card.
    private static CloudShadowRaster.Mask MaskOf(string texture, TextureArchive textures, Dictionary<string, CloudShadowRaster.Mask> masks)
    {
        if (masks.TryGetValue(texture, out var known))
            return known;
        var mask = CloudShadowRaster.Mask.Solid;
        if (textures.FindImage(texture, authored: true) is { } image && !image.IsEmpty())
        {
            if (image.IsCompressed())
                image.Decompress();
            int w = Math.Min(image.GetWidth(), MaskSide), h = Math.Min(image.GetHeight(), MaskSide);
            var alpha = new float[w * h];
            var counts = new int[w * h];
            for (int y = 0; y < image.GetHeight(); y++)
            {
                for (int x = 0; x < image.GetWidth(); x++)
                {
                    int at = (y * h / image.GetHeight() * w) + (x * w / image.GetWidth());
                    alpha[at] += image.GetPixel(x, y).A;
                    counts[at]++;
                }
            }
            for (int i = 0; i < alpha.Length; i++)
                alpha[i] = counts[i] > 0 ? alpha[i] / counts[i] : 0f;
            mask = new CloudShadowRaster.Mask(w, h, alpha);
        }
        masks[texture] = mask;
        return mask;
    }

    // The map's texels as R8 bytes. The float map and the bytes are dropped once uploaded.
    private static Baked Upload(CloudShadowRaster.Result map, Vector2 run)
    {
        var bytes = new byte[map.Through.Length];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = (byte)MathF.Round(Math.Clamp(map.Through[i], 0f, 1f) * 255f);
        var texture = TextureUpload.Create(map.Width, map.Height, Image.Format.R8, bytes);
        var rect = new Vector4(map.Origin.X, map.Origin.Y, 1f / map.Size.X, 1f / map.Size.Y);
        return new Baked(texture, rect, run);
    }

    private static void Write(Vector4 value, Baked? map)
    {
        if (!_registered)
            return;
        if (!ReferenceEquals(map, PublishedMap))
        {
            PublishedMap = map;
            RenderingServer.GlobalShaderParameterSet(MapParam, map?.Texture ?? _clear!);
            if (map != null)
                RenderingServer.GlobalShaderParameterSet(RectParam, map.Rect);
        }
        if (value == Published)
            return;
        Published = value;
        RenderingServer.GlobalShaderParameterSet(Param, value);
    }

    private void Publish()
    {
        if (!Draws)
        {
            Write(OffValue, null);
            return;
        }
        var baked = _baked[_orientation]!;
        Write(new Vector4(baked.Run.X, baked.Run.Y, CloudShadowRaster.Darkness, 0f), baked);
    }

    /// <summary>One zone sun's uploaded map: the texture, its rect as the shader reads it, and the sun's run.</summary>
    public sealed record Baked(ImageTexture Texture, Vector4 Rect, Vector2 Run)
    {
        /// <summary>The share the map lets through at <paramref name="world"/>, nearest texel read back from
        /// the texture, 1 off the map. A suite's read-back; the shader reads bilinear.</summary>
        public float ThroughAt(Vector3 world)
        {
            var foot = CloudShadowRaster.Foot(world, Run);
            float u = (foot.X - Rect.X) * Rect.Z, v = (foot.Y - Rect.Y) * Rect.W;
            if (u < 0f || v < 0f || u > 1f || v > 1f)
                return 1f;
            var image = Texture.GetImage();
            int x = Math.Min((int)(u * image.GetWidth()), image.GetWidth() - 1);
            int y = Math.Min((int)(v * image.GetHeight()), image.GetHeight() - 1);
            return image.GetPixel(x, y).R;
        }
    }
}
