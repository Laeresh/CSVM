using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Modes;

/// <summary>
/// A course's gates drawn from Godot primitives: two tapered, banded course pylons per pylon gate, a
/// striped torus under tethered balloons per ring. One child node
/// per pool gate, in pool order, stands at <see cref="CourseMotion"/>'s pose each frame. The pylons'
/// fabric ripple is drawn only, on <c>csky_time</c>. A touched course pylon is cut and regrows
/// (<see cref="CoursePylonCut"/>) through its own materials' uniforms. Everything is lit by the zone's
/// per-vertex sun and fogged as the faithful aircraft are, casts no shadow, and is sized and coloured to tune.
/// </summary>
public sealed partial class CourseVisuals : Node3D
{
    private const float StartLengthM = 6f;

    // A mesh is centred on its own origin, so a column runs from minus half its height to plus half.
    // The ripple function is the one place a vertex moves: wave, a cut stump's sag, a falling top's
    // tip and drop. The bands and the cut's clip window read the undisplaced v_local. Both faces draw,
    // the inside darker, so a cut pylon reads as a hollow tube.
    private const string ShaderCode = """
        shader_type spatial;
        render_mode unshaded, cull_disabled, shadows_disabled, fog_disabled;

        uniform vec3 base_srgb = vec3(1.0);
        uniform vec3 band_srgb = vec3(1.0);
        uniform float band_count = 0.0;
        uniform float band_by_angle = 0.0;
        uniform float height_m = 1.0;
        uniform float ripple_m = 0.0;
        uniform float clip_lo_m = -1.0e6;
        uniform float clip_hi_m = 1.0e6;
        uniform float sag_m = 0.0;
        uniform float drop_m = 0.0;
        uniform float tilt_rad = 0.0;
        uniform vec3 tilt_pivot = vec3(0.0);

        #include "res://shaders/csky_atmosphere.gdshaderinc"
        #include "res://shaders/csky_srgb.gdshaderinc"
        #include "res://shaders/csky_time.gdshaderinc"

        varying vec3 v_local;
        varying vec3 v_lit;

        vec3 ripple(vec3 v, vec3 n) {
            float up = v.y + 0.5 * height_m;
            float rise = clamp(up / height_m, 0.0, 1.0);
            float wave = sin(up * 0.52 - csky_time * 2.5 + 2.0 * atan(v.z, v.x));
            float w = clamp(up / max(clip_hi_m, 0.01), 0.0, 1.0);
            float slump = sag_m * w * w;
            vec3 p = v - tilt_pivot;
            float c = cos(tilt_rad);
            float s = sin(tilt_rad);
            vec3 tip = vec3(c * p.x - s * p.y, s * p.x + c * p.y, p.z) - p;
            return vec3(n.x, 0.0, n.z) * (ripple_m * rise * wave + 0.3 * slump) + tip - vec3(0.0, slump + drop_m, 0.0);
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
            float up = v_local.y + 0.5 * height_m;
            if (up < clip_lo_m || up > clip_hi_m) {
                discard;
            }
            float s = band_by_angle > 0.5 ? atan(v_local.z, v_local.x) / TAU + 0.5 : v_local.y / height_m + 0.5;
            bool band = band_count > 0.0 && fract(s * band_count) >= 0.5;
            vec3 lit = clamp((band ? band_srgb : base_srgb) * v_lit * (FRONT_FACING ? 1.0 : 0.5), 0.0, 1.0);
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

    private readonly List<(CourseGate Gate, Node3D Node, CutPylon[]? Pylons)> _gates = new();
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
            CutPylon[]? pylons = null;
            if (gate.Kind == CourseGateKind.PylonGate)
            {
                pylons = AddPylons(node, gate);
            }
            else
            {
                AddRing(node, gate, ring, balloon, tether);
            }

            visuals.AddChild(node);
            visuals._gates.Add((gate, node, pylons));
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

    /// <summary>Every gate node to <see cref="CourseMotion.Pose"/>, and every course pylon to its
    /// <see cref="CoursePylonCut"/> look, at <paramref name="seconds"/> of sim time.</summary>
    public void Pose(double seconds)
    {
        foreach (var (gate, node, pylons) in _gates)
        {
            node.Transform = CourseMotion.Pose(gate, seconds);
            foreach (var pylon in pylons ?? System.Array.Empty<CutPylon>())
            {
                pylon.Show(seconds);
            }
        }
    }

    /// <summary>Cuts a course pylon on each of <paramref name="run"/>'s touches of one, at the run's sim
    /// time, and stands every pylon whole on its reset. Both only record, and <see cref="Pose"/> draws,
    /// so a run outliving this node does no harm. Answers this node.</summary>
    public CourseVisuals Follow(StuntMission run)
    {
        run.ElementTouched += touch => Cut(touch.Gate, touch.Element, touch.PylonHeight, run.SimSeconds());
        run.RunReset += Restore;
        return this;
    }

    /// <summary>Cuts pool gate <paramref name="gate"/>'s course pylon <paramref name="element"/> at
    /// <paramref name="heightM"/> above its base, at <paramref name="seconds"/> of sim time. Any other
    /// element, a rim or a balloon, cuts nothing.</summary>
    public void Cut(int gate, CourseElement element, float heightM, double seconds)
    {
        if (element is CourseElement.LeftCoursePylon or CourseElement.RightCoursePylon && _gates[gate].Pylons is { } pylons)
        {
            pylons[element == CourseElement.LeftCoursePylon ? 0 : 1].Cut(heightM, seconds);
        }
    }

    /// <summary>Every course pylon whole from the next <see cref="Pose"/> on, mid-regrow or not.</summary>
    public void Restore()
    {
        foreach (var (_, _, pylons) in _gates)
        {
            foreach (var pylon in pylons ?? System.Array.Empty<CutPylon>())
            {
                pylon.Restore();
            }
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

    // Every dimension is CourseGateShape's, which a touch test reads too. Each course pylon has its own
    // stump material and a hidden child on the same mesh for the falling top. So one cut moves one
    // pylon. The top's margin covers it tipped flat.
    private static CutPylon[] AddPylons(Node3D node, CourseGate gate)
    {
        var mesh = new CylinderMesh
        {
            BottomRadius = CourseGateShape.PylonBaseRadius(gate),
            TopRadius = CourseGateShape.PylonTopRadius(gate),
            Height = gate.Height,
            RadialSegments = 24,
            Rings = 24,
        };
        var pylons = new CutPylon[2];
        for (int side = 0; side < 2; side++)
        {
            var stump = Material(Red, White, 3f, byAngle: false, gate.Height, CourseGateShape.RippleM);
            var falling = Material(Red, White, 3f, byAngle: false, gate.Height, CourseGateShape.RippleM);
            var pylon = Part(side == 0 ? "pylon_left" : "pylon_right", mesh, stump);
            pylon.MaterialOverride = stump;
            pylon.ExtraCullMargin = CourseGateShape.RippleM + (CoursePylonCut.SagShare * gate.Height);
            pylon.Position = CourseGateShape.PylonBase(gate, side) + (Vector3.Up * (0.5f * gate.Height));
            var top = Part("top", mesh, falling);
            top.MaterialOverride = falling;
            top.ExtraCullMargin = gate.Height;
            top.Visible = false;
            pylon.AddChild(top);
            node.AddChild(pylon);
            pylons[side] = new CutPylon(gate, side, stump, falling, top);
        }

        return pylons;
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

    // One course pylon's cut: what was cut and when, written to its two materials only on a change.
    // The top tips outward from the gate, away from the aperture the plane flies through.
    private sealed class CutPylon(CourseGate gate, int side, ShaderMaterial stump, ShaderMaterial falling, MeshInstance3D top)
    {
        private double _at = double.NaN;
        private float _cut;
        private float _top;
        private PylonCutLook? _drawn;

        public void Cut(float heightM, double seconds)
        {
            (_cut, _top) = CoursePylonCut.Recut(gate.Height, heightM, Look(seconds));
            _at = seconds;
        }

        public void Restore() => _at = double.NaN;

        public void Show(double seconds)
        {
            var look = Look(seconds);
            if (look == _drawn)
            {
                return;
            }

            _drawn = look;
            float outward = side == 0 ? -1f : 1f;
            float cutRadius = Mathf.Lerp(CourseGateShape.PylonBaseRadius(gate), CourseGateShape.PylonTopRadius(gate), look.CutM / gate.Height);
            stump.SetShaderParameter("clip_hi_m", look.StumpTopM);
            stump.SetShaderParameter("sag_m", look.SagM);
            falling.SetShaderParameter("clip_lo_m", look.CutM);
            falling.SetShaderParameter("clip_hi_m", look.TopM);
            falling.SetShaderParameter("drop_m", look.DropM);
            falling.SetShaderParameter("tilt_rad", -outward * look.TiltRad);
            falling.SetShaderParameter("tilt_pivot", new Vector3(outward * cutRadius, look.CutM - (0.5f * gate.Height), 0f));
            top.Visible = look.TopShown;
        }

        private PylonCutLook Look(double seconds) =>
            double.IsNaN(_at) ? CoursePylonCut.Whole : CoursePylonCut.At(gate.Height, _cut, _top, seconds - _at);
    }
}
