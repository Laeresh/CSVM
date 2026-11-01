using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Modes;

/// <summary>
/// A course's gates drawn from Godot primitives: two tapered, banded course pylons per pylon gate, a
/// striped torus under tethered balloons per ring. One child node
/// per pool gate, in pool order, stands at <see cref="CourseMotion"/>'s pose each frame. The pylons'
/// fabric ripple is drawn only, on <c>csky_time</c>. Everything is lit by the zone's per-vertex sun and
/// fogged as the faithful aircraft are, casts no shadow, and is sized and coloured to tune.
/// </summary>
public sealed partial class CourseVisuals : Node3D
{
    private const float StartLengthM = 6f;

    // A mesh is centred on its own origin, so a column runs from minus half its height to plus half.
    // The ripple function is the one place a vertex moves. The bands read the undisplaced v_local,
    // which a clip by height can read too.
    private const string ShaderCode = """
        shader_type spatial;
        render_mode unshaded, cull_back, shadows_disabled, fog_disabled;

        uniform vec3 base_srgb = vec3(1.0);
        uniform vec3 band_srgb = vec3(1.0);
        uniform float band_count = 0.0;
        uniform float band_by_angle = 0.0;
        uniform float height_m = 1.0;
        uniform float ripple_m = 0.0;

        #include "res://shaders/csky_atmosphere.gdshaderinc"
        #include "res://shaders/csky_srgb.gdshaderinc"
        #include "res://shaders/csky_time.gdshaderinc"

        varying vec3 v_local;
        varying vec3 v_lit;

        vec3 ripple(vec3 v, vec3 n) {
            float up = v.y + 0.5 * height_m;
            float rise = clamp(up / height_m, 0.0, 1.0);
            float wave = sin(up * 0.52 - csky_time * 2.5 + 2.0 * atan(v.z, v.x));
            return vec3(n.x, 0.0, n.z) * ripple_m * rise * wave;
        }

        void vertex() {
            v_local = VERTEX;
            VERTEX += ripple(VERTEX, NORMAL);
            vec3 sun_dir;
            vec3 ambient;
            vec3 diffuse;
            vec3 fill;
            csky_sun_rgb_at(CAMERA_POSITION_WORLD, sun_dir, ambient, diffuse, fill);
            v_lit = ambient + diffuse * max(dot(normalize(MODEL_NORMAL_MATRIX * NORMAL), sun_dir), 0.0);
        }

        void fragment() {
            float s = band_by_angle > 0.5 ? atan(v_local.z, v_local.x) / TAU + 0.5 : v_local.y / height_m + 0.5;
            bool band = band_count > 0.0 && fract(s * band_count) >= 0.5;
            vec3 lit = clamp((band ? band_srgb : base_srgb) * v_lit, 0.0, 1.0);
            vec3 fog_world = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
            ALBEDO = mix(csky_srgb_to_linear(lit), csky_fog_color_at(CAMERA_POSITION_WORLD),
                csky_fog_amount(fog_world, CAMERA_POSITION_WORLD));
        }
        """;

    // sRGB triples, handed over as vectors: a Color reaches a spatial uniform already linearised.
    private static readonly Vector3 Red = new(0.82f, 0.1f, 0.12f);
    private static readonly Vector3 White = new(0.95f, 0.95f, 0.92f);
    private static readonly Vector3 Silver = new(0.86f, 0.86f, 0.84f);
    private static readonly Vector3 Rope = new(0.2f, 0.19f, 0.17f);
    private static readonly Vector3 Yellow = new(0.95f, 0.78f, 0.1f);
    private static readonly Vector3 Black = new(0.08f, 0.08f, 0.08f);

    private readonly List<(CourseGate Gate, Node3D Node)> _gates = new();
    private double _unclocked;

    private CourseVisuals()
    {
        Name = "course_visuals";
    }

    /// <summary>One node per pool gate, in pool order, each at its gate's current pose.</summary>
    public IReadOnlyList<Node3D> GateNodes => _gates.ConvertAll(g => g.Node);

    /// <summary>The course's gates, posed at sim time 0, plus a cone at the start point along its
    /// heading when <paramref name="withStart"/>. The caller adds it to the world.</summary>
    public static CourseVisuals Build(Course course, bool withStart = false)
    {
        var visuals = new CourseVisuals();
        var ring = Material(Red, White, 8f, byAngle: true, 1f, 0f);
        var balloon = Material(Silver, Silver, 0f, byAngle: false, 1f, 0f);
        var tether = Material(Rope, Rope, 0f, byAngle: false, 1f, 0f);
        foreach (var gate in course.Gates)
        {
            var node = new Node3D { Name = $"gate_{gate.Id}" };
            if (gate.Kind == CourseGateKind.PylonGate)
            {
                AddPylons(node, gate);
            }
            else
            {
                AddRing(node, gate, ring, balloon, tether);
            }

            visuals.AddChild(node);
            visuals._gates.Add((gate, node));
        }

        if (withStart)
        {
            var cone = Part("start", new CylinderMesh { TopRadius = 0f, BottomRadius = StartLengthM / 3f, Height = StartLengthM },
                Material(Yellow, Black, 3f, byAngle: false, StartLengthM, 0f));
            cone.Transform = new Transform3D(new Basis(Vector3.Up, Mathf.DegToRad(course.Start.HeadingDeg)), course.Start.Position)
                * new Transform3D(new Basis(Vector3.Right, -Mathf.Pi / 2f), Vector3.Zero);
            visuals.AddChild(cone);
        }

        visuals.Pose(0.0);
        return visuals;
    }

    /// <summary>Every gate node to <see cref="CourseMotion.Pose"/> at <paramref name="seconds"/> of sim time.</summary>
    public void Pose(double seconds)
    {
        foreach (var (gate, node) in _gates)
        {
            node.Transform = CourseMotion.Pose(gate, seconds);
        }
    }

    public override void _Process(double delta) =>
        Pose(GameClock.Current?.Time ?? (_unclocked += delta));

    private static ShaderMaterial Material(Vector3 baseSrgb, Vector3 bandSrgb, float bands, bool byAngle, float height, float ripple)
    {
        var material = new ShaderMaterial { Shader = ShaderTwins.Pooled(ShaderCode, "course-visuals") };
        material.SetShaderParameter("base_srgb", baseSrgb);
        material.SetShaderParameter("band_srgb", bandSrgb);
        material.SetShaderParameter("band_count", bands);
        material.SetShaderParameter("band_by_angle", byAngle ? 1f : 0f);
        material.SetShaderParameter("height_m", height);
        material.SetShaderParameter("ripple_m", ripple);
        return material;
    }

    private static MeshInstance3D Part(string name, PrimitiveMesh mesh, ShaderMaterial material)
    {
        mesh.Material = material;
        return new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = CourseGateShape.RippleM,
        };
    }

    // Every dimension is CourseGateShape's, which a touch test reads too.
    private static void AddPylons(Node3D node, CourseGate gate)
    {
        var material = Material(Red, White, 3f, byAngle: false, gate.Height, CourseGateShape.RippleM);
        var mesh = new CylinderMesh
        {
            BottomRadius = CourseGateShape.PylonBaseRadius(gate),
            TopRadius = CourseGateShape.PylonTopRadius(gate),
            Height = gate.Height,
            RadialSegments = 24,
            Rings = 24,
        };
        for (int side = 0; side < 2; side++)
        {
            var pylon = Part(side == 0 ? "pylon_left" : "pylon_right", mesh, material);
            pylon.Position = CourseGateShape.PylonBase(gate, side) + (Vector3.Up * (0.5f * gate.Height));
            node.AddChild(pylon);
        }
    }

    // A torus in the gate's plane (local z = 0), turned up from Godot's flat one. Each tether and
    // balloon is turned so its own long axis runs along its way out from the ring.
    private static void AddRing(Node3D node, CourseGate gate, ShaderMaterial ring, ShaderMaterial balloon, ShaderMaterial tether)
    {
        float inner = 0.5f * gate.Diameter;
        float tube = CourseGateShape.TubeRadius(gate);
        var torus = Part("ring", new TorusMesh { InnerRadius = inner, OuterRadius = inner + (2f * tube), Rings = 64, RingSegments = 12 }, ring);
        torus.Basis = new Basis(Vector3.Right, Mathf.Pi / 2f);
        node.AddChild(torus);

        float radius = CourseGateShape.BalloonRadius(gate);
        float length = CourseGateShape.TetherLength(gate);
        float rope = CourseGateShape.TetherRadiusM;
        var line = new CylinderMesh { TopRadius = rope, BottomRadius = rope, Height = length, RadialSegments = 6, Rings = 1 };
        var ball = new SphereMesh { Radius = radius, Height = 2f * CourseGateShape.BalloonStretch * radius, RadialSegments = 24, Rings = 12 };
        for (int i = 0; i < CourseGateShape.BalloonAnglesDeg.Length; i++)
        {
            var turn = new Basis(Vector3.Back, -Mathf.DegToRad(CourseGateShape.BalloonAnglesDeg[i]));
            var way = CourseGateShape.BalloonWay(i);
            var tetherPart = Part($"tether_{i}", line, tether);
            tetherPart.Transform = new Transform3D(turn, CourseGateShape.TetherAnchor(gate, i) + (way * (0.5f * length)));
            node.AddChild(tetherPart);
            var body = Part($"balloon_{i}", ball, balloon);
            body.Transform = new Transform3D(turn, CourseGateShape.BalloonCentre(gate, i));
            node.AddChild(body);
        }
    }
}
