using System;
using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// The Enhanced cloud shadows' one owner: a node under the world root that writes the
/// <c>csky_cloud_shadow*</c> globals <c>csky_cloud_shadow.gdshaderinc</c> declares. Every arm that
/// scales its sun by <c>csky_terrain_sun</c> reads them: ground, water, buildings, vegetation. One
/// global moves every pane at once, and no aircraft arm reads it. The pattern drifts on the shader clock
/// (<see cref="CloudShadowPattern"/>). It draws only under Enhanced, with the Cloud Shadows word on,
/// in a cloudy mission, outside a night zone. Leaving the tree puts the off state back.
/// </summary>
internal sealed partial class CloudShadow : Node
{
    // The globals the include declares: offset X/Z, one over the cell, darkness; the edge band; the
    // lab's tint; and the ambient share.
    private static readonly StringName Param = "csky_cloud_shadow";
    private static readonly StringName EdgeParam = "csky_cloud_shadow_edge";
    private static readonly StringName TintParam = "csky_cloud_shadow_tint";
    private static readonly StringName AmbientParam = "csky_cloud_shadow_ambient";

    private static bool _registered;
    private static Vector2 _publishedEdge;
    private static Vector3 _publishedTint;
    private static float _publishedAmbient;

    private readonly bool _cloudy;
    private bool _night;
    private Vector2 _drift;

    // Added to the drift's own offset, so a drift change mid-session moves on from where the pattern stands.
    private double _shiftX, _shiftZ;

    private CloudShadow(Vector2 drift, CloudShadowPattern.DriftSource source, bool cloudy, bool night)
    {
        Name = "CloudShadow";
        _drift = drift;
        Source = source;
        _cloudy = cloudy;
        _night = night;
    }

    /// <summary>The node whose state the globals carry, null while the off state stands.</summary>
    public static CloudShadow? Live { get; private set; }

    /// <summary>The value the main global holds with nothing drawn: a darkness of 0 at the default cell.</summary>
    public static Vector4 OffValue => new(0f, 0f, 1f / CloudShadowPattern.PatternSize, 0f);

    /// <summary>The main global's last written value. A mirror, since the renderer refuses to read a
    /// global back outside the editor; it is what a suite asserts by.</summary>
    public static Vector4 Published { get; private set; } = OffValue;

    /// <summary>The pattern's drift in metres per second along X and Z. Setting it keeps the pattern
    /// where it stands and moves it on at the new rate.</summary>
    public Vector2 Drift
    {
        get => _drift;
        set
        {
            double t = ShaderTime.Unwrapped;
            _shiftX += (value.X - _drift.X) * t;
            _shiftZ += (value.Y - _drift.Y) * t;
            _drift = value;
        }
    }

    /// <summary>The tunes this session draws: the chapter's, until the cloud lab edits them.</summary>
    public CloudShadowPattern.Tune Tune { get; set; } = CloudShadowPattern.Tune.Default;

    /// <summary>The cloud lab's tint, the light added under a shadow's core. Black adds nothing.</summary>
    public Vector3 Tint { get; set; }

    /// <summary>Where <see cref="Drift"/> came from.</summary>
    public CloudShadowPattern.DriftSource Source { get; }

    /// <summary>Whether the shadows draw now: Enhanced, the word on, a cloudy mission, a day zone.</summary>
    public bool Draws => GraphicsMode.Enhanced && CloudShadowSetting.Enabled && _cloudy && !_night;

    /// <summary>Declares the globals with the off state. Must run before the first shader that reads
    /// them is built, beside <see cref="TerrainShadow.RegisterGlobals"/>.</summary>
    public static void RegisterGlobals()
    {
        RenderingServer.GlobalShaderParameterAdd(Param, RenderingServer.GlobalShaderParameterType.Vec4, OffValue);
        RenderingServer.GlobalShaderParameterAdd(EdgeParam, RenderingServer.GlobalShaderParameterType.Vec2, CloudShadowPattern.Edge);
        RenderingServer.GlobalShaderParameterAdd(TintParam, RenderingServer.GlobalShaderParameterType.Vec3, Vector3.Zero);
        RenderingServer.GlobalShaderParameterAdd(AmbientParam, RenderingServer.GlobalShaderParameterType.Float, 0f);
        // The volumetric clouds' ground shadow (VolumetricClouds.PublishGround); a zero deck share leaves it off.
        RenderingServer.GlobalShaderParameterAdd("csky_pc_ground", RenderingServer.GlobalShaderParameterType.Vec4, Vector4.Zero);
        RenderingServer.GlobalShaderParameterAdd("csky_pc_ground_sun", RenderingServer.GlobalShaderParameterType.Vec4, new Vector4(0f, 1f, 0f, 0f));
        RenderingServer.GlobalShaderParameterAdd("csky_pc_heads_shape", RenderingServer.GlobalShaderParameterType.Vec4, Vector4.Zero);
        RenderingServer.GlobalShaderParameterAdd("csky_pc_heads_layer", RenderingServer.GlobalShaderParameterType.Vec4, Vector4.Zero);
        RenderingServer.GlobalShaderParameterAdd("csky_pc_heads_density", RenderingServer.GlobalShaderParameterType.Vec4, Vector4.Zero);
        RenderingServer.GlobalShaderParameterAdd("csky_pc_veil", RenderingServer.GlobalShaderParameterType.Vec4, Vector4.Zero);
        _registered = true;
        Published = OffValue;
        _publishedEdge = CloudShadowPattern.Edge;
        _publishedTint = Vector3.Zero;
        _publishedAmbient = 0f;
    }

    /// <summary>Puts the off state back, so every sun term reads exactly 1 for the clouds.</summary>
    public static void Withdraw()
    {
        Live = null;
        Write(OffValue, CloudShadowPattern.Edge, Vector3.Zero, 0f);
    }

    /// <summary>The shadows for a mission drifting at <paramref name="drift"/>, drawn while
    /// <paramref name="cloudy"/> and the zone is not <paramref name="night"/>.</summary>
    public static CloudShadow Create(Vector2 drift, CloudShadowPattern.DriftSource source, bool cloudy, bool night) =>
        new(drift, source, cloudy, night);

    /// <summary>The drift of <paramref name="dome"/>'s scrolling surfaces (<see cref="CloudShadowPattern.CeilingDrift"/>),
    /// their ceiling put at <paramref name="altitude"/>, or null where none scrolls. The arrays come
    /// through <paramref name="scene"/>, the builder that drew the dome.</summary>
    public static Vector2? CeilingDriftOf(Node3D dome, SceneBuilder scene, float altitude)
    {
        var corners = new List<Vector3>();
        var uvs = new List<Vector2>();
        var scroll = Vector2.Zero;
        Collect(dome, dome, scene, corners, uvs, ref scroll);
        // ⚠ Do not drop this collection. Reading MeshInstance3D.Mesh mints a managed wrapper per mesh
        // (see OceanMask.Bake), and a short run that quits first aborts on Godot's exit check.
        GC.Collect();
        return corners.Count == 0 ? null : CloudShadowPattern.CeilingDrift(corners, uvs, scroll, altitude);
    }

    /// <summary>The offset the pattern is read at now, in metres along X and Z. It wraps at the tuned
    /// lattice's period, so the repeat is seamless at any size.</summary>
    public Vector2 Offset() =>
        CloudShadowPattern.Offset(_drift, ShaderTime.Unwrapped, Tune.Clamped().PatternSize, _shiftX, _shiftZ);

    /// <summary>Takes a zone apply's day or night, written at once.</summary>
    public void FollowZone(bool night)
    {
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

    // The word, the graphics mode and the clock are read every frame. An Options apply or a mode
    // switch reaches the globals with no hook, and a halted clock holds the pattern still.
    public override void _Process(double delta) => Publish();

    // The dome-frame triangles and UVs of every surface under `node` whose material scrolls. The
    // scroll is one model's rate, so the last surface's stands for all.
    private static void Collect(Node node, Node3D dome, SceneBuilder scene, List<Vector3> corners, List<Vector2> uvs,
        ref Vector2 scroll)
    {
        if (node is MeshInstance3D { Mesh: ArrayMesh mesh } mi)
        {
            var xf = DomeLocal(mi, dome);
            for (int s = 0; s < mesh.GetSurfaceCount(); s++)
            {
                if (mi.GetActiveMaterial(s) is not ShaderMaterial material
                    || material.GetShaderParameter("scroll_rate") is not { VariantType: Variant.Type.Vector2 } rate
                    || rate.AsVector2() == Vector2.Zero)
                {
                    continue;
                }
                var arrays = scene.SurfaceArrays(mesh, s);
                if (arrays[(int)Mesh.ArrayType.TexUV].VariantType == Variant.Type.Nil)
                    continue;
                scroll = rate.AsVector2();
                var v = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var uv = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
                var ix = arrays[(int)Mesh.ArrayType.Index].VariantType == Variant.Type.Nil
                    ? Array.Empty<int>() : arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                int count = ix.Length > 0 ? ix.Length : v.Length;
                for (int i = 0; i < count; i++)
                {
                    int at = ix.Length > 0 ? ix[i] : i;
                    corners.Add(xf * v[at]);
                    uvs.Add(uv[at]);
                }
            }
        }
        foreach (var child in node.GetChildren())
            Collect(child, dome, scene, corners, uvs, ref scroll);
    }

    // The node's transform in the dome's own frame, the dome's scale left out (it cancels).
    private static Transform3D DomeLocal(Node3D node, Node3D dome)
    {
        var xf = Transform3D.Identity;
        for (Node? n = node; n != null && n != dome; n = n.GetParent())
        {
            if (n is Node3D n3)
                xf = n3.Transform * xf;
        }
        return xf;
    }

    private static void Write(Vector4 value, Vector2 edge, Vector3 tint, float ambient)
    {
        if (!_registered)
            return;
        if (edge != _publishedEdge)
        {
            _publishedEdge = edge;
            RenderingServer.GlobalShaderParameterSet(EdgeParam, edge);
        }
        if (tint != _publishedTint)
        {
            _publishedTint = tint;
            RenderingServer.GlobalShaderParameterSet(TintParam, tint);
        }
        if (ambient != _publishedAmbient)
        {
            _publishedAmbient = ambient;
            RenderingServer.GlobalShaderParameterSet(AmbientParam, ambient);
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
            Write(OffValue, CloudShadowPattern.Edge, Vector3.Zero, 0f);
            return;
        }
        var tunes = Tune.Clamped();
        var offset = Offset();
        Write(new Vector4(offset.X, offset.Y, 1f / tunes.PatternSize, tunes.Darkness), tunes.Edge, Tint, tunes.Ambient);
    }
}
