using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>The enhanced presentation's wind streaks: a sparse field of thin quads in a
/// camera-centred wrap box, aligned to the aircraft's world velocity, so speed and a hard turn are
/// felt in every view. Built only under <see cref="GraphicsMode.Enhanced"/>, one field per player
/// pane, and drawn OVER the authored speed cue rather than instead of it: the wisps are the
/// original's own cue (<see cref="Flight.SpeedCue"/>) and are untouched here.
/// Rendering follows <see cref="Precipitation"/>: one MultiMesh, each quad's position derived
/// in-shader from a per-instance seed and the camera position, with a near fade and a rim fade.
/// ⚠ Every alpha, density, length and G constant below is TUNE, judged at the controls.
/// </summary>
public sealed partial class WindStreaks : Node3D
{
    /// <summary>The master opacity the authored speed-cue wisps would be multiplied by when the
    /// streaks draw over them. 1 draws them unchanged, which is what ships: whether Enhanced dims
    /// them is the user's pick off this item's montage. Applying a value below 1 needs the puffer
    /// renderer's own master alpha, which does not exist yet and belongs to the wisps' own tuning
    /// item, so this constant records the decision rather than performing it.</summary>
    public const float AuthoredWispAlphaScale = 1f;

    // --- TUNE: the feel, pending the user's judgement at the controls ---
    private const float BoxHalf = 45f;        // camera-centred wrap-box half-extent (m)
    private const int Count = 1400;           // MultiMesh instances in that box (density)
    private const float StreakWidth = 0.15f;  // quad width (m)
    private const float StreakSeconds = 0.035f; // length = airspeed × this …
    private const float MinStreak = 1.5f;     // … but at least this (m)
    private const float MasterAlpha = 0.45f;   // peak opacity at full intensity
    private const float CruiseFraction = 0.8f; // no streaks at or below this fraction of fd_speed
    private const float CruiseBand = 0.03f;   // width of the on-ramp above it (speed fraction)
    private const float GFullScale = 5f;      // the load factor (G) the G term reaches full at
    private const float GAlphaGain = 0.6f;    // how much opacity a full-scale pull adds
    private const float StreamBoost = 0.25f;  // extra drift along the flight path, × airspeed
    private const float NearFade = 6f;        // fade streaks within this many m of the camera …
    private const float FadeStart = 0.55f;    // … and out beyond this fraction of the box radius

    // Self-animating in the same sense as the rain: POSITION is written from a per-instance seed
    // and CAMERA_POSITION_WORLD, so the field costs one draw call and a handful of uniforms.
    // INSTANCE_CUSTOM.xyz is the fixed base fraction of the box; .w is unused.
    // ⚠ Never use Godot's TIME here, and do not derive the drift from csky_time either: the drift
    // RATE changes with airspeed every frame, so a rate × time form teleports the whole field the
    // moment the throttle moves. The CPU accumulates the offset on the sim dt instead.
    private const string ShaderCode = """
        shader_type spatial;
        render_mode blend_mix, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

        uniform vec3 tint = vec3(0.86, 0.9, 1.0);   // already sRGB→linear (set on the CPU)
        uniform vec3 box_half = vec3(45.0);
        uniform vec3 drift = vec3(0.0);     // accumulated along the flight path (m)
        uniform vec3 stream_dir = vec3(0.0, 0.0, -1.0); // unit, the streak's long axis
        uniform float peak_alpha = 0.0;
        uniform float near_fade = 6.0;
        uniform float fade_start = 0.55;
        uniform vec2 streak_size = vec2(0.06, 2.0);    // (width, length) m

        varying float v_alpha;

        void vertex() {
            vec3 cell = box_half * 2.0;
            vec3 world = INSTANCE_CUSTOM.xyz * cell + drift;
            vec3 rel = mod(world - CAMERA_POSITION_WORLD + box_half, cell) - box_half;
            vec3 center = CAMERA_POSITION_WORLD + rel;

            // Fade in over the first near_fade metres so a streak sitting on the eye cannot smear
            // across the cockpit glass at the near plane, and fade out past fade_start of the box
            // radius so the wrap box has no visible face.
            float md = length(rel);
            float dn = length(rel / box_half);
            v_alpha = peak_alpha
                * smoothstep(0.0, near_fade, md)
                * (1.0 - smoothstep(fade_start, 1.0, dn));

            // Axial billboard: long axis along the flight path, thin axis facing the camera.
            vec3 along = stream_dir;
            vec3 to_cam = normalize(CAMERA_POSITION_WORLD - center);
            vec3 right = cross(along, to_cam);
            float rl = length(right);
            right = rl > 1e-4 ? right / rl : INV_VIEW_MATRIX[0].xyz;
            vec3 offset = right * VERTEX.x * streak_size.x + along * VERTEX.y * streak_size.y;
            POSITION = PROJECTION_MATRIX * (VIEW_MATRIX * vec4(center + offset, 1.0));
        }

        void fragment() {
            // A procedural streak: a thin gaussian across the quad, tapering to nothing at both
            // ends so the axial billboard's orientation does not matter.
            float dx = (UV.x - 0.5) / 0.22;
            float across = exp(-dx * dx);
            float along = sin(3.14159265 * UV.y);
            ALBEDO = tint;
            ALPHA = across * along * v_alpha;
        }
        """;

    // TUNE, with the TUNE block above: a pale, faintly cold streak, sRGB like the fog colour.
    private static readonly Color StreakTint = new(0.86f, 0.9f, 1f);

    private ShaderMaterial _mat = null!;
    private MultiMeshInstance3D _mmi = null!;
    private Vector3 _drift;

    /// <summary>The peak alpha written to the field this frame, zero while it is invisible.</summary>
    public float Alpha { get; private set; }

    /// <summary>The streak length (m) written this frame; it grows with airspeed.</summary>
    public float LengthMeters { get; private set; }

    internal ShaderMaterial Material => _mat;

    /// <summary>Builds the field, or null on the faithful path: this is a remake-only layer and
    /// the presentation switch is its one gate. Add the returned node beside the player's other
    /// world-space effects and drive it with <see cref="Update"/>.</summary>
    public static WindStreaks? Create()
    {
        if (!GraphicsMode.Enhanced)
            return null;
        var field = new WindStreaks();
        field.Init();
        return field;
    }

    /// <summary>Feeds this frame's flight state: <paramref name="velocity"/> is the aircraft's
    /// world velocity (m/s), <paramref name="speedFraction"/> its airspeed over the airframe's
    /// rated maximum (<c>fd_speed</c>), and <paramref name="loadFactorG"/> the demanded load
    /// factor in G, which is 1 in level flight. <paramref name="dt"/> is the sim step.</summary>
    public void Update(float dt, Vector3 velocity, float speedFraction, float loadFactorG)
    {
        float speed = velocity.Length();
        // Zero below the cruise fraction, then the speed ramp toward rated max plus what the pull
        // adds: a hard turn thickens the field without waiting for the throttle.
        float gate = Mathf.SmoothStep(CruiseFraction, CruiseFraction + CruiseBand, speedFraction);
        float speedTerm = Mathf.Clamp(
            (speedFraction - CruiseFraction) / Mathf.Max(1e-3f, 1f - CruiseFraction), 0f, 1f);
        float gTerm = GAlphaGain * Mathf.Clamp(
            (Mathf.Abs(loadFactorG) - 1f) / (GFullScale - 1f), 0f, 1f);
        Alpha = MasterAlpha * gate * Mathf.Clamp(speedTerm + gTerm, 0f, 1f);
        LengthMeters = Mathf.Max(speed * StreakSeconds, MinStreak);

        // The field is otherwise fixed in world space, so the aircraft's own motion past it is
        // most of the cue; the boost drifts it the other way to exaggerate that a little.
        float cell = BoxHalf * 2f;
        _drift -= velocity * (StreamBoost * dt);
        _drift = new Vector3(
            Mathf.PosMod(_drift.X, cell), Mathf.PosMod(_drift.Y, cell), Mathf.PosMod(_drift.Z, cell));

        var dir = speed > 1e-3f ? velocity / speed : Vector3.Forward;
        _mat.SetShaderParameter("stream_dir", dir);
        _mat.SetShaderParameter("drift", _drift);
        _mat.SetShaderParameter("peak_alpha", Alpha);
        _mat.SetShaderParameter("streak_size", new Vector2(StreakWidth, LengthMeters));
        _mmi.Visible = Alpha > 0f;
    }

    /// <summary>Crash/respawn lifecycle: hide the field and forget the drift, so a teleported
    /// aircraft does not arrive inside a streak pattern built around where it used to be.</summary>
    public void Reset()
    {
        _drift = Vector3.Zero;
        Alpha = 0f;
        _mat.SetShaderParameter("drift", _drift);
        _mat.SetShaderParameter("peak_alpha", 0f);
        _mmi.Visible = false;
    }

    private void Init()
    {
        Name = "wind_streaks";
        _mat = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
        var tint = StreakTint.SrgbToLinear();
        _mat.SetShaderParameter("tint", new Vector3(tint.R, tint.G, tint.B));
        _mat.SetShaderParameter("box_half", new Vector3(BoxHalf, BoxHalf, BoxHalf));
        _mat.SetShaderParameter("near_fade", NearFade);
        _mat.SetShaderParameter("fade_start", FadeStart);
        _mat.SetShaderParameter("peak_alpha", 0f);
        _mat.SetShaderParameter("streak_size", new Vector2(StreakWidth, MinStreak));

        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = new QuadMesh { Size = Vector2.One },
            InstanceCount = Count,
        };
        // Its own stream, not the rain's: this field exists only under Enhanced, and drawing it
        // from a shared stream would make the faithful path's draws depend on the graphics mode.
        var rng = Rng.NewSystemRandom(Rng.WindStreaks);
        for (int i = 0; i < Count; i++)
        {
            mm.SetInstanceTransform(i, Transform3D.Identity); // position comes from the shader
            mm.SetInstanceCustomData(i, new Color(
                (float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble(), 0f));
        }

        _mmi = new MultiMeshInstance3D
        {
            Multimesh = mm,
            MaterialOverride = _mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
            // The shader repositions every instance around the moving camera, far from this node's
            // origin, so a world-sized custom AABB is what keeps the one draw call from being
            // frustum-culled. Same reasoning as the precipitation field's.
            CustomAabb = new Aabb(new Vector3(-40000f, -40000f, -40000f), new Vector3(80000f, 80000f, 80000f)),
        };
        AddChild(_mmi);
        Log.Info("world", $"wind streaks: {Count} instances, box {BoxHalf:0} m, cruise gate {CruiseFraction:0.00} of rated max (enhanced only)");
    }
}
