using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Modes;

/// <summary>
/// A course's gates drawn from Godot primitives: two tapered, banded course pylons per pylon gate, a
/// striped torus under tethered balloons per ring. One child node
/// per pool gate, in pool order, stands at <see cref="CourseMotion"/>'s pose each frame. The pylons'
/// fabric ripple is drawn only, on <c>csky_time</c>. A touched course pylon is cut and regrows
/// (<see cref="CoursePylonCut"/>) through its own materials' uniforms. A followed run's next gate
/// carries a direction chevron and the gate after it a dim fill. Everything is lit by the zone's
/// per-vertex sun and fogged as the faithful aircraft are, casts no shadow, and is sized and coloured to tune.
/// </summary>
public sealed partial class CourseVisuals : Node3D
{
    private const float StartLengthM = 6f;

    // The run's cues, all TUNE. The chevron's length is a share of its gate's aperture. Its reach
    // across, bar width, block depth and slab thickness are shares of that length. The fade is how much
    // of the following gate's dim fill the dither drops.
    private const float ChevronShare = 0.25f;
    private const float ChevronSpread = 0.7f;
    private const float ChevronBar = 0.35f;
    private const float ChevronDepth = 0.3f;
    private const float ChevronSlab = 0.04f;
    private const float HighlightFade = 0.75f;
    private const float HighlightSlabM = 0.2f;

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
        uniform float fade = 0.0;

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
            float dither = fract(52.9829189 * fract(dot(FRAGCOORD.xy, vec2(0.06711056, 0.00583715))));
            if (up < clip_lo_m || up > clip_hi_m || dither < fade) {
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
    private Course _course = null!;
    private int[] _entryGates = System.Array.Empty<int>();
    private StuntMission? _run;
    private (Mesh Mesh, Transform3D Local)[] _fills = System.Array.Empty<(Mesh, Transform3D)>();
    private double _unclocked;

    private CourseVisuals()
    {
        Name = "course_visuals";
    }

    /// <summary>One node per pool gate, in pool order, each at its gate's current pose.</summary>
    public IReadOnlyList<Node3D> GateNodes => _gates.ConvertAll(g => g.Node);

    /// <summary>The followed run's direction chevron on its next sequence entry's gate. Its apex, along
    /// its local -Z, points the way that entry is flown. Hidden with nothing left to fly; null until
    /// <see cref="Follow"/>.</summary>
    public MeshInstance3D? Chevron { get; private set; }

    /// <summary>The dim fill across the aperture of the gate after the next entry's. Hidden with no
    /// entry after the next; null until <see cref="Follow"/>.</summary>
    public MeshInstance3D? Highlight { get; private set; }

    /// <summary>The course's gates, posed at sim time 0, plus a cone at the start point along its
    /// heading when <paramref name="withStart"/>. The caller adds it to the world.</summary>
    public static CourseVisuals Build(Course course, bool withStart = false)
    {
        var visuals = new CourseVisuals { _course = course, _entryGates = new int[course.Sequence.Count] };
        for (int i = 0; i < course.Sequence.Count; i++)
        {
            while (course.Gates[visuals._entryGates[i]].Id != course.Sequence[i].GateId)
                visuals._entryGates[i]++; // Course.Parse refuses an entry naming no gate
        }

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

        if (_run is { } run && Chevron is { } chevron && Highlight is { } highlight)
        {
            int next = run.AllComplete ? -1 : run.CompletedCount;
            PoseChevron(chevron, next, seconds);
            PoseHighlight(highlight, next >= 0 && next + 1 < _entryGates.Length ? next + 1 : -1, seconds);
        }
    }

    /// <summary>Cuts a course pylon on each of <paramref name="run"/>'s touches of one, at the run's sim
    /// time, and stands every pylon whole on its reset. Both only record, and <see cref="Pose"/> draws,
    /// so a run outliving this node does no harm. It also adds the run's <see cref="Chevron"/> and
    /// <see cref="Highlight"/>, which <see cref="Pose"/> moves with the run's next entry. Answers this node.</summary>
    public CourseVisuals Follow(StuntMission run)
    {
        run.ElementTouched += touch => Cut(touch.Gate, touch.Element, touch.PylonHeight, run.SimSeconds());
        run.RunReset += Restore;
        _run = run;
        _fills = _course.Gates.Select(Fill).ToArray();
        Chevron = Cue("next_chevron", ChevronMesh(), 0f);
        Highlight = Cue("after_highlight", _fills.Length > 0 ? _fills[0].Mesh : null, HighlightFade);
        AddChild(Chevron);
        AddChild(Highlight);
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

    // The aperture's middle in the gate frame: a pylon gate's half height, a ring's centre.
    private static Vector3 ApertureCentre(CourseGate gate) =>
        gate.Kind == CourseGateKind.PylonGate ? new Vector3(0f, 0.5f * gate.Height, 0f) : Vector3.Zero;

    // A thin slab across the aperture, its clear opening only: a pylon gate's rectangle, a ring's disc.
    private static (Mesh Mesh, Transform3D Local) Fill(CourseGate gate) =>
        gate.Kind == CourseGateKind.PylonGate
            ? (new BoxMesh { Size = new Vector3(gate.Width, gate.Height, HighlightSlabM) }, new Transform3D(Basis.Identity, ApertureCentre(gate)))
            : (new CylinderMesh { TopRadius = 0.5f * gate.Diameter, BottomRadius = 0.5f * gate.Diameter, Height = HighlightSlabM, RadialSegments = 48, Rings = 1 },
                new Transform3D(new Basis(Vector3.Right, Mathf.Pi / 2f), Vector3.Zero));

    // A cue on the course shader, so no new shader compiles mid-flight. Yellow, as the start cone.
    private static MeshInstance3D Cue(string name, Mesh? mesh, float fade)
    {
        var material = Material(Yellow, Yellow, 0f, byAngle: false, 1f, 0f);
        material.SetShaderParameter("fade", fade);
        return new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
    }

    // A unit chevron: a flat V block and an upright V slab sharing the axis. The block's inner walls
    // face an eye on the approach, its top the eye above; the slab's sides face an eye beside it.
    // Its arms end on z = 0 and its apex is at z = -(1 + ChevronBar).
    private static ArrayMesh ChevronMesh()
    {
        Vector2[] outline =
        {
            new(1f, 0f), new(0f, ChevronSpread), new(-ChevronBar, ChevronSpread),
            new(1f - ChevronBar, 0f), new(-ChevronBar, -ChevronSpread), new(0f, -ChevronSpread),
        };
        int[] triangles = { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5 };
        float turn = 0f; // twice the signed area, which says which side of each edge is outside
        for (int i = 0; i < outline.Length; i++)
        {
            turn += outline[i].Cross(outline[(i + 1) % outline.Length]);
        }

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var (normal, depth) in new[] { (Vector3.Up, ChevronDepth), (Vector3.Right, ChevronSlab) })
        {
            var across = normal == Vector3.Up ? Vector3.Right : Vector3.Up;
            Vector3 At(int i, float side) =>
                (across * outline[i].Y) + (Vector3.Forward * (outline[i].X + ChevronBar)) + (normal * (0.5f * depth * side));
            foreach (float side in new[] { 1f, -1f })
            {
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Face(st, normal * side, At(triangles[t], side), At(triangles[t + 1], side), At(triangles[t + 2], side));
                }
            }

            for (int i = 0; i < outline.Length; i++)
            {
                int j = (i + 1) % outline.Length;
                var edge = outline[j] - outline[i];
                var outside = ((Vector3.Forward * edge.Y) - (across * edge.X)) * Mathf.Sign(turn);
                Face(st, outside, At(i, 1f), At(j, 1f), At(j, -1f));
                Face(st, outside, At(i, 1f), At(j, -1f), At(i, -1f));
            }
        }

        return st.Commit();
    }

    // One triangle facing `normal`: Godot's front faces wind clockwise seen from their normal's side.
    private static void Face(SurfaceTool st, Vector3 normal, Vector3 a, Vector3 b, Vector3 c)
    {
        normal = normal.Normalized();
        if ((b - a).Cross(c - a).Dot(normal) > 0f)
        {
            (b, c) = (c, b);
        }

        foreach (var v in new[] { a, b, c })
        {
            st.SetNormal(normal);
            st.AddVertex(v);
        }
    }

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

    // The chevron lies wholly past the gate's plane on the flown side. The crossing then moves it on
    // before an eye at the aircraft reaches it.
    private void PoseChevron(MeshInstance3D chevron, int entry, double seconds)
    {
        chevron.Visible = entry >= 0;
        if (entry < 0)
        {
            return;
        }

        var gate = _course.Gates[_entryGates[entry]];
        float size = ChevronShare * (gate.Kind == CourseGateKind.PylonGate ? Mathf.Min(gate.Width, gate.Height) : gate.Diameter);
        float turn = _course.Sequence[entry].Direction == GateDirection.Reverse ? Mathf.Pi : 0f;
        var local = new Transform3D(new Basis(Vector3.Up, turn).Scaled(Vector3.One * size), ApertureCentre(gate));
        chevron.Transform = CourseMotion.Pose(gate, seconds) * local;
    }

    private void PoseHighlight(MeshInstance3D highlight, int entry, double seconds)
    {
        highlight.Visible = entry >= 0;
        if (entry < 0)
        {
            return;
        }

        int g = _entryGates[entry];
        highlight.Mesh = _fills[g].Mesh;
        highlight.Transform = CourseMotion.Pose(_course.Gates[g], seconds) * _fills[g].Local;
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
