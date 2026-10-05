using System.Globalization;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>The race ghost: how solid another race pilot's aircraft draws at a camera distance, and
/// the per-instance stamp that arms it. The shader half is <c>csky_race_ghost.gdshaderinc</c>. Only
/// a builder asked for it emits that (<see cref="SceneBuilder.RaceGhostShader"/>), so any other
/// session compiles and draws what it did. The shader measures from the drawing camera, so each
/// splitscreen pane fades every aircraft at its own distance. The ghost is a dither in the opaque
/// pass, not a blend. <see cref="Alpha"/> is the same law in C#, and <see cref="VertexLine"/>
/// carries its constants into the shader.</summary>
public static class RaceGhost
{
    /// <summary>Inside this many metres from the drawing camera the aircraft is a full ghost, at
    /// <see cref="GhostAlpha"/>. TUNE, judged at the controls.</summary>
    public const float GhostWithinM = 40f;

    /// <summary>Beyond this many metres the aircraft draws solid. TUNE, judged at the controls.</summary>
    public const float SolidBeyondM = 80f;

    /// <summary>The share of an aircraft's pixels a full ghost keeps under the original graphics,
    /// through the 4x4 ordered dither. TUNE, judged by eye.</summary>
    public const float GhostAlpha = 0.35f;

    /// <summary>The same share under Enhanced, higher because the lit, shadowed airframe dithered
    /// to <see cref="GhostAlpha"/> all but vanishes there. TUNE, judged by eye.</summary>
    public const float EnhancedGhostAlpha = 0.55f;

    /// <summary>The instance shader parameter the stamp writes: x the owning pilot's first-person
    /// layer bit, w 1 while armed. Declared last in <c>csky_instance_uniforms.gdshaderinc</c>.</summary>
    public static readonly StringName Param = "csky_ghost";

    internal const string Include = "#include \"res://shaders/csky_race_ghost.gdshaderinc\"";

    internal const string Varying = "varying flat float v_ghost_alpha;";

    /// <summary>The fragment stage's dither, the clutter fade's own ordered 4x4 keep.</summary>
    internal const string FragmentLine =
        "    if (!csky_clutter_dither_keep(FRAGCOORD.xy, v_ghost_alpha)) { discard; }";

    /// <summary><see cref="VertexLineFor"/> under the current graphics mode, which is what the bias
    /// shader generator writes.</summary>
    internal static string VertexLine => VertexLineFor(GraphicsMode.Enhanced);

    /// <summary>A full ghost's share of pixels under the given presentation.</summary>
    public static float FloorAlpha(bool enhanced) => enhanced ? EnhancedGhostAlpha : GhostAlpha;

    /// <summary>The share of the aircraft drawn at <paramref name="distanceM"/> from a camera that
    /// is not its own pilot's: <see cref="FloorAlpha"/> within <see cref="GhostWithinM"/>, 1 beyond
    /// <see cref="SolidBeyondM"/>, linear between. The shader's law, term for term.</summary>
    public static float Alpha(float distanceM, bool enhanced = false)
    {
        float t = Mathf.Clamp((distanceM - GhostWithinM) / (SolidBeyondM - GhostWithinM), 0f, 1f);
        return Mathf.Lerp(FloorAlpha(enhanced), 1f, t);
    }

    /// <summary>Arms every mesh instance under <paramref name="model"/> as the aircraft of the pilot
    /// whose own cameras drop <paramref name="ownerLayer"/>, and returns how many it stamped. Only
    /// a surface built with <see cref="SceneBuilder.RaceGhostShader"/> reads it, so the cockpit
    /// interior, built by another builder, ignores the stamp.</summary>
    public static int Stamp(Node model, uint ownerLayer)
    {
        var armed = new Vector4(ownerLayer, 0f, 0f, 1f);
        int stamped = 0;
        if (model is GeometryInstance3D instance)
        {
            instance.SetInstanceShaderParameter(Param, armed);
            stamped++;
        }

        foreach (var child in model.GetChildren())
        {
            stamped += Stamp(child, ownerLayer);
        }

        return stamped;
    }

    /// <summary>The vertex stage's one line: the instance's alpha for the drawing camera, flat per
    /// mesh instance. ⚠ Keep the constants coming from this class. The suite measures
    /// <see cref="Alpha"/>, and a literal typed into the shader alone would escape it.</summary>
    internal static string VertexLineFor(bool enhanced) =>
        "    v_ghost_alpha = csky_race_ghost_alpha(MODEL_MATRIX[3].xyz, CAMERA_POSITION_WORLD, "
        + "CAMERA_VISIBLE_LAYERS, csky_ghost, csky_photo_eye, "
        + $"{Literal(GhostWithinM)}, {Literal(SolidBeyondM)}, {Literal(FloorAlpha(enhanced))}, "
        + $"{Literal(SceneBuilder.PhotoEyeReach)});";

    private static string Literal(float value) => value.ToString("0.0###", CultureInfo.InvariantCulture);
}
