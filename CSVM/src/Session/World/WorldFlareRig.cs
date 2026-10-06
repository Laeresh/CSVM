using System.Collections.Generic;
using System.Globalization;
using CSVM.Flight.Camera;
using CSVM.Mech3;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.Session.World;

/// <summary>The sun's lens flare drawn in the world, plus the wash. The core and the three rings are
/// quads at sky distance, on the great circle from the sun to the view's forward direction.
/// The law is <see cref="LensFlareRig"/>'s carried into angles, so in a flat pane each element
/// lands on the screen rig's pixel at its size. The core is depth-tested, so a ridge
/// or an aircraft covers it gradually; the rings ignore everything past the seat. Stereo always
/// draws this rig, flat Enhanced only under <c>--world-flare</c>. Entry: docs/architecture/Session.md.</summary>
public sealed class WorldFlareRig
{
    // ⚠ Keep these equal to LensFlareRig's: they are the same footage measurement
    // (docs/org/weather.md), and WorldFlareTests pins the two copies together.
    internal const float RefHeight = 720f;
    internal const float WashMax = 0.66f;
    internal const float WashFullPx = 30f;
    internal const float WashSlopePerPx = 0.001651f;
    internal const float FadeOutSeconds = 0.125f;

    // The window shape a stereo flare's view cone takes, so it shows the sun over the same cone a
    // flat chase pane does. TUNE at the headset.
    private const float StereoAspect = 16f / 9f;

    // The elements' depth as a share of the sun node's own. The core then stands just in front of
    // the dome's sun disc, and behind any terrain or aircraft between.
    private const float DepthShare = 0.95f;

    private static readonly Color Clear = new(1f, 1f, 1f, 0f);

    private readonly SessionSpec _spec;
    private readonly Node3D? _xrHead;
    private readonly List<Instance> _instances = new();

    /// <summary>A rig over <paramref name="spec"/>. <paramref name="xrHead"/> is the stereo path's
    /// tracked head, the view a stereo flare is placed for; null in a flat session.</summary>
    public WorldFlareRig(SessionSpec spec, Node3D? xrHead)
    {
        _spec = spec;
        _xrHead = xrHead;
    }

    /// <summary>How many panes got a flare, 0 when the chapter authors none.</summary>
    public int InstanceCount => _instances.Count;

    /// <summary>Whether this rig places its elements for the stereo head rather than a flat camera.</summary>
    public bool Stereo => _xrHead != null;

    /// <summary>The measured elements, sun first: fraction along the sun-to-centre line, diameter in
    /// pixels at <see cref="RefHeight"/>, and intensity.</summary>
    internal static IReadOnlyList<(float Frac, float DiaPx, float Intensity)> Elements { get; } = new[]
    {
        (0f, 63f, 1.00f),
        (0.50f, 102f, 0.38f),
        (0.90f, 45f, 0.45f),
        (2.0f, 164f, 0.18f),
    };

    /// <summary>Where the sun stands in a view. The result says whether it is in the frame and gives
    /// its tangent-plane point (x right, y up, 1 at 45 degrees off). It also gives its depth along
    /// the axis and the screen rig's wash alpha. The frame is the vertical half-angle tangent
    /// <paramref name="tanHalfV"/> and the width over height <paramref name="aspect"/>.</summary>
    public static SunInView Place(Transform3D view, Vector3 sun, float tanHalfV, float aspect)
    {
        Vector3 s = view.AffineInverse() * sun;
        if (s.Z >= 0f)
        {
            return new SunInView(false, Vector2.Zero, 0f, 0f);
        }

        var t = new Vector2(s.X / -s.Z, s.Y / -s.Z);
        bool inFrame = Mathf.Abs(t.Y) <= tanHalfV && Mathf.Abs(t.X) <= tanHalfV * aspect;
        float distPx = t.Length() * (RefHeight * 0.5f) / tanHalfV;
        float wash = Mathf.Clamp(WashMax - ((distPx - WashFullPx) * WashSlopePerPx), 0f, WashMax);
        return new SunInView(inFrame, t, -s.Z, wash);
    }

    /// <summary>One element's centre in view space, where the shader places it. It sits
    /// <paramref name="frac"/> of the way from the sun's tangent point to the axis, at
    /// <paramref name="depth"/> metres along it.</summary>
    public static Vector3 ElementCentre(Vector2 sunTangent, float frac, float depth)
    {
        Vector2 c = sunTangent * (1f - frac);
        return new Vector3(c.X * depth, c.Y * depth, -depth);
    }

    /// <summary>Half an element's width as a tangent: <paramref name="diaPx"/> of a
    /// <see cref="RefHeight"/> frame whose half height is <paramref name="tanHalfV"/>.</summary>
    public static float HalfSize(float diaPx, float tanHalfV) => diaPx / RefHeight * tanHalfV;

    /// <summary>The tangent of half a camera's vertical field of view in a pane of
    /// <paramref name="pane"/> pixels, whichever axis the camera keeps.</summary>
    public static float TanHalfVertical(Camera3D camera, Vector2 pane)
    {
        float tan = Mathf.Tan(Mathf.DegToRad(camera.Fov) * 0.5f);
        return camera.KeepAspect == Camera3D.KeepAspectEnum.Width && pane.X > 0f ? tan * pane.Y / pane.X : tan;
    }

    /// <summary>Builds one flare per rig under <paramref name="worldRoot"/>, or nothing when either
    /// of <see cref="LensFlareRig"/>'s two chapter gates is closed or <c>--no-flare</c> asks.</summary>
    public void Build(IReadOnlyList<PlayerRig> rigs, TextureArchive textures,
        string interpPath, string chapter, Node3D worldRoot)
    {
        var names = LensFlareRig.FlareTextureNames(interpPath, chapter);
        bool anySun = false;
        foreach (var rig in rigs)
        {
            anySun |= FindSun(rig.Horizon) != null;
        }

        if (names.Count == 0 || !anySun)
        {
            if (names.Count > 0 != anySun)
            {
                Log.Warn("world", $"lens flare gates disagree chapter={chapter} textures={names.Count} sun_node={anySun} flare=off");
            }

            return;
        }

        if (_spec.NoFlare)
        {
            Log.Info("world", $"lens flare: --no-flare, {chapter} flare suppressed (slots {string.Join(",", names)})");
            return;
        }

        var core = new Shader { Code = ElementShader(ring: false) };
        var ring = new Shader { Code = ElementShader(ring: true) };
        foreach (var rig in rigs)
        {
            var sun = FindSun(rig.Horizon);
            if (sun == null)
            {
                continue;
            }

            // The overlay carries the flat wash, and its visibility is the cutscene's word on
            // whether the sky may show (PlayerRig.WorldOverlays).
            var overlay = new CanvasLayer { Layer = HudLayers.SunWash, Name = "sun_wash" };
            rig.HudParent.AddChild(overlay);
            rig.WorldOverlays.Add(overlay);
            var inst = new Instance(rig, sun, overlay);
            if (!Stereo)
            {
                inst.WashRect = new ColorRect { Color = Clear, MouseFilter = Control.MouseFilterEnum.Ignore };
                inst.WashRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                overlay.AddChild(inst.WashRect);
            }

            for (int i = 0; i < Elements.Count && i < names.Count; i++)
            {
                if (textures.Find(names[i]) is not { } tex)
                {
                    continue;
                }

                // ⚠ Keep one priority per element. The quads share their bounds, so equal priorities
                // leave their order to an unstable sort, and the HDR sum then rounds differently.
                var material = new ShaderMaterial
                {
                    Shader = i == 0 ? core : ring,
                    RenderPriority = HeadLockedQuads.WorldFlarePriority + i,
                };
                material.SetShaderParameter("flare_tex", tex);
                material.SetShaderParameter("frac", Elements[i].Frac);
                var mesh = new MeshInstance3D
                {
                    Name = $"world_flare{rig.Index + 1}_{i}",
                    Mesh = new QuadMesh { Size = new Vector2(2f, 2f) },
                    MaterialOverride = material,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    // The shader moves the quad onto the view, so no camera may cull it by bounds.
                    CustomAabb = new Aabb(new Vector3(-1e7f, -1e7f, -1e7f), new Vector3(2e7f, 2e7f, 2e7f)),
                    Visible = false,
                };
                if (rig.VisualLayer != 0)
                {
                    mesh.Layers = rig.VisualLayer;
                }

                worldRoot.AddChild(mesh);
                inst.Elements.Add(new Element(i, mesh, material));
            }

            _instances.Add(inst);
        }

        if (_instances.Count > 0)
        {
            Log.Info("world", $"lens flare [{chapter}]: world rig, {_instances.Count} rig(s), {(Stereo ? "stereo head" : "flat camera")}, slots {string.Join(",", names)}");
        }
    }

    /// <summary>The wash colour one rig's flare paints this frame, clear when it has none. A stereo
    /// session's head-locked sun-wash quad copies it.</summary>
    public Color WashFor(int rigIndex)
    {
        foreach (var inst in _instances)
        {
            if (inst.Rig.Index == rigIndex)
            {
                return inst.Overlay.Visible ? inst.Wash : Clear;
            }
        }

        return Clear;
    }

    /// <summary>Per frame: place, gate and fade every element. Called from the per-rig block of
    /// <c>GameSession._Process</c>, after the dome has been re-anchored.</summary>
    public void Tick(double delta)
    {
        foreach (var inst in _instances)
        {
            if (!View(inst, out Transform3D view, out float tanHalfV, out float aspect))
            {
                continue;
            }

            Vector3 sunGlobal = inst.Sun.GlobalPosition;
            var at = Place(view, sunGlobal, tanHalfV, aspect);

            // The rings keep the screen rig's one ray to the sun's centre. The core needs none: its
            // depth test hides exactly the part a ridge or a hull covers.
            bool losClear = true;
            if (at.InFrame)
            {
                var space = inst.Rig.Camera.GetWorld3D().DirectSpaceState;
                using var query = PhysicsRayQueryParameters3D.Create(view.Origin, sunGlobal);
                using var hit = space.IntersectRay(query);
                losClear = hit.Count == 0;
            }

            inst.CoreFade = Step(inst.CoreFade, at.InFrame, delta);
            inst.RingFade = Step(inst.RingFade, at.InFrame && losClear, delta);
            inst.WashFade = Step(inst.WashFade, at.InFrame, delta);
            inst.Wash = new Color(1f, 1f, 1f, at.Wash * inst.WashFade);
            if (inst.WashRect != null)
            {
                inst.WashRect.Color = inst.Wash;
            }

            bool shown = inst.Overlay.Visible;
            Vector3 sunDir = (sunGlobal - view.Origin).Normalized();
            float depth = at.Depth * DepthShare;
            foreach (var e in inst.Elements)
            {
                float fade = e.Index == 0 ? inst.CoreFade : inst.RingFade;
                e.Mesh.Visible = shown && fade > 0f;
                if (!e.Mesh.Visible)
                {
                    continue;
                }

                e.Material.SetShaderParameter("sun_dir", sunDir);
                e.Material.SetShaderParameter("depth_m", depth);
                e.Material.SetShaderParameter("half_size", HalfSize(Elements[e.Index].DiaPx, tanHalfV));
                e.Material.SetShaderParameter("gain", Elements[e.Index].Intensity * fade);
            }
        }
    }

    // The screen rig's measured envelope: instant attack, timed release.
    private static float Step(float current, bool on, double delta)
        => on ? 1f : Mathf.Max(0f, current - ((float)delta / FadeOutSeconds));

    // The `sun` node inside one rig's dome copy, scoped to the dome as LensFlareRig scopes it.
    private static Node3D? FindSun(Node3D? horizon)
        => horizon?.FindChild("sun", recursive: true, owned: false) as Node3D;

    // One element quad, in view space at depth_m. The canvas adds the texture's bytes to the
    // picture's. So the add is made in gamma space over the opaque picture, and the linear blend
    // takes the difference it makes. Rings skip pixels nearer than the seat clearance.
    private static string ElementShader(bool ring)
    {
        var inv = CultureInfo.InvariantCulture;
        string seatUniforms = ring ? $$"""
            uniform sampler2D depth_texture : hint_depth_texture, filter_nearest;
            const float SEAT_CLEAR_M = {{HeadLockedQuads.SeatClearanceM.ToString("0.0###", inv)}};
            """ : "";
        string seatDiscard = ring ? """
                float raw = texture(depth_texture, SCREEN_UV).r;
                vec4 v = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, raw, 1.0);
                if (-v.z / v.w < SEAT_CLEAR_M) {
                    discard;
                }
            """ : "";
        return $$"""
            shader_type spatial;
            render_mode blend_add, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled{{(ring ? ", depth_test_disabled" : "")}};
            #include "res://shaders/csky_srgb.gdshaderinc"

            uniform sampler2D flare_tex : filter_linear, repeat_disable;
            uniform vec3 sun_dir = vec3(0.0, 0.0, -1.0);
            uniform float frac = 0.0;
            uniform float half_size = 0.0;
            uniform float depth_m = 1000.0;
            uniform float gain = 0.0;
            uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;
            {{seatUniforms}}

            void vertex() {
                vec3 s = mat3(VIEW_MATRIX) * sun_dir;
                vec2 t = s.xy / max(-s.z, 1e-4);
                vec2 c = t * (1.0 - frac) + VERTEX.xy * half_size;
                POSITION = PROJECTION_MATRIX * vec4(c * depth_m, -depth_m, 1.0);
            }

            void fragment() {
            {{seatDiscard}}
                vec4 tex = texture(flare_tex, UV);
                vec3 dst_l = clamp(texture(screen_texture, SCREEN_UV).rgb, 0.0, 1.0);
                vec3 out_l = csky_srgb_to_linear(csky_linear_to_srgb(dst_l) + tex.rgb * tex.a * gain);
                ALBEDO = max(out_l - dst_l, vec3(0.0));
                ALPHA = 1.0;
            }
            """;
    }

    // The view a flare is placed for: the stereo head, or the rig's own camera.
    private bool View(Instance inst, out Transform3D view, out float tanHalfV, out float aspect)
    {
        if (_xrHead != null && GodotObject.IsInstanceValid(_xrHead))
        {
            view = _xrHead.GlobalTransform;
            tanHalfV = Mathf.Tan(Mathf.DegToRad(CameraController.ExternalFovDeg) * 0.5f);
            aspect = StereoAspect;
            return true;
        }

        var cam = inst.Rig.Camera;
        Vector2 pane = cam.GetViewport().GetVisibleRect().Size;
        view = cam.GlobalTransform;
        tanHalfV = TanHalfVertical(cam, pane);
        aspect = pane.Y > 0f ? pane.X / pane.Y : 0f;
        return pane.Y > 0f;
    }

    /// <summary>The sun in one view, as <see cref="Place"/> reads it.</summary>
    public readonly record struct SunInView(bool InFrame, Vector2 Tangent, float Depth, float Wash);

    private sealed record Element(int Index, MeshInstance3D Mesh, ShaderMaterial Material);

    private sealed class Instance
    {
        public Instance(PlayerRig rig, Node3D sun, CanvasLayer overlay)
        {
            Rig = rig;
            Sun = sun;
            Overlay = overlay;
        }

        public PlayerRig Rig { get; }

        public Node3D Sun { get; }

        public CanvasLayer Overlay { get; }

        public List<Element> Elements { get; } = new();

        public ColorRect? WashRect { get; set; }

        public Color Wash { get; set; } = new(1f, 1f, 1f, 0f);

        public float CoreFade { get; set; }

        public float RingFade { get; set; }

        public float WashFade { get; set; }
    }
}
