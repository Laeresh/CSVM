using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Modes;

/// <summary>
/// A course's gates drawn from Godot primitives: two tapered, banded course pylons per pylon gate, a
/// striped torus under tethered balloons per ring. One child node
/// per pool gate, in pool order, stands at <see cref="CourseMotion"/>'s pose each frame. The course pylons'
/// fabric ripple is drawn only, on <c>csky_time</c>. A touched course pylon is cut and regrows
/// (<see cref="CoursePylonCut"/>) through its own materials' uniforms. A followed run's next gate
/// carries a red cross while the aircraft is on its wrong side, the gate after it a dim fill. Everything is lit by the zone's
/// per-vertex sun and fogged as the faithful aircraft are, casts no shadow, and is sized and coloured to tune.
/// </summary>
public sealed partial class CourseVisuals : Node3D
{
    private const float StartLengthM = 6f;

    // The run's cues, all TUNE. The cross's bar width is a share of its aperture's lesser side, and
    // its bars are this deep. The fade is how much of the following gate's dim fill the dither drops.
    private const float CrossBarShare = 0.03f;
    private const float CrossDepthM = 0.3f;
    private const float HighlightFade = 0.75f;
    private const float HighlightSlabM = 0.2f;

    // The wrong-side cross's gates, TUNE. It shows this far past the gate's plane and hides as far
    // back before it, so a pass along the plane cannot strobe it. The range gate keeps it near the
    // gate, with the same kind of band.
    private const float WrongSideM = 2f;
    private const float CrossShowRangeM = 800f;
    private const float CrossHideRangeM = 1000f;

    // A mesh is centred on its own origin, so a column runs from minus half its height to plus half.
    // The ripple function is the one place a vertex moves: wave, a cut stump's sag, a falling top's
    // tip and drop. The bands and the cut's clip window read the undisplaced v_local. Both faces draw,
    // the inside darker, so a cut course pylon reads as a hollow tube.
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
    private static readonly Vector3 CrossRed = new(1f, 0.08f, 0.06f); // the wrong-side cross, TUNE

    private readonly List<(CourseGate Gate, Node3D Node, CutPylon[]? Pylons)> _gates = new();
    private Course _course = null!;
    private int[] _entryGates = System.Array.Empty<int>();
    private StuntMission? _run;
    private (Mesh Mesh, Transform3D Local)[] _fills = System.Array.Empty<(Mesh, Transform3D)>();
    private Mesh[] _crosses = System.Array.Empty<Mesh>();
    private int _crossEntry = -1;
    private bool _wrongSide;
    private double _unclocked;

    private CourseVisuals()
    {
        Name = "course_visuals";
    }

    /// <summary>One node per pool gate, in pool order, each at its gate's current pose.</summary>
    public IReadOnlyList<Node3D> GateNodes => _gates.ConvertAll(g => g.Node);

    /// <summary>The followed run's red cross across its next sequence entry's clear opening, shown only
    /// while the aircraft is on that entry's wrong side (<see cref="WrongSide"/>). Its node's origin is
    /// the aperture's middle. Null until <see cref="ShowCues"/>.</summary>
    public MeshInstance3D? Cross { get; private set; }

    /// <summary>The dim fill across the aperture of the gate after the next entry's. Hidden with no
    /// entry after the next; null until <see cref="ShowCues"/>.</summary>
    public MeshInstance3D? Highlight { get; private set; }

    /// <summary>Whether the cross shows, given whether it showed. <paramref name="beyond"/> is how far
    /// the aircraft is past the gate's plane along the entry's flown way. It is negative on the side
    /// the entry is flown from. <paramref name="range"/> is the aircraft's distance from the aperture's
    /// middle. Each test has a band, so neither strobes at its edge.</summary>
    public static bool WrongSide(bool shown, float beyond, float range) =>
        range <= (shown ? CrossHideRangeM : CrossShowRangeM) && beyond > (shown ? -WrongSideM : WrongSideM);

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

        if (_run is { } run && Cross is { } cross && Highlight is { } highlight)
        {
            int next = run.AllComplete ? -1 : run.CompletedCount;
            PoseCross(cross, next, seconds);
            PoseHighlight(highlight, next >= 0 && next + 1 < _entryGates.Length ? next + 1 : -1, seconds);
        }
    }

    /// <summary>A solo run's whole binding: <see cref="TrackCuts"/> and <see cref="ShowCues"/> on the
    /// one run. Answers this node.</summary>
    public CourseVisuals Follow(StuntMission run) => TrackCuts(run).ShowCues(run);

    /// <summary>Cuts a course pylon on each of <paramref name="run"/>'s touches of one, at the run's sim
    /// time. Its rerun stands whole every course pylon it cut last. Both only record, and <see cref="Pose"/>
    /// draws, so a run outliving this node does no harm. Called once per run this machine flies, every
    /// pane's and every bot's. Answers this node.</summary>
    public CourseVisuals TrackCuts(StuntMission run)
    {
        run.ElementTouched += touch => Cut(touch.Gate, touch.Element, touch.PylonHeight, run.SimSeconds(), run);
        run.RunReset += () => Restore(run);
        return this;
    }

    /// <summary>The <see cref="Cross"/> and <see cref="Highlight"/>, which <see cref="Pose"/> moves
    /// with <paramref name="run"/>'s next entry. A later call moves them to another run. Answers this node.</summary>
    public CourseVisuals ShowCues(StuntMission run)
    {
        _run = run;
        if (Cross != null)
        {
            return this;
        }

        _fills = _course.Gates.Select(Fill).ToArray();
        _crosses = _course.Gates.Select(CrossMesh).ToArray();
        Cross = Cue("wrong_side_cross", null, CrossRed, 0f);
        Highlight = Cue("after_highlight", null, Yellow, HighlightFade);
        AddChild(Cross);
        AddChild(Highlight);
        return this;
    }

    /// <summary>Cuts pool gate <paramref name="gate"/>'s course pylon <paramref name="element"/> at
    /// <paramref name="heightM"/> above its base, at <paramref name="seconds"/> of sim time. Any other
    /// element, a rim or a balloon, cuts nothing. The cut is <paramref name="by"/>'s until another run
    /// cuts that course pylon again.</summary>
    public void Cut(int gate, CourseElement element, float heightM, double seconds, StuntMission by)
    {
        if (element is CourseElement.LeftCoursePylon or CourseElement.RightCoursePylon && _gates[gate].Pylons is { } pylons)
        {
            pylons[element == CourseElement.LeftCoursePylon ? 0 : 1].Cut(heightM, seconds, by);
        }
    }

    /// <summary>Every course pylon <paramref name="by"/> cut last whole from the next <see cref="Pose"/>
    /// on, mid-regrow or not.</summary>
    public void Restore(StuntMission by)
    {
        foreach (var (_, _, pylons) in _gates)
        {
            foreach (var pylon in pylons ?? System.Array.Empty<CutPylon>())
            {
                pylon.Restore(by);
            }
        }
    }

    public override void _Process(double delta) =>
        Pose(GameClock.Current?.Time ?? (_unclocked += delta));

    // A thin slab across the aperture, its clear opening only: a pylon gate's rectangle, a ring's disc.
    private static (Mesh Mesh, Transform3D Local) Fill(CourseGate gate) =>
        gate.Kind == CourseGateKind.PylonGate
            ? (new BoxMesh { Size = new Vector3(gate.Width, gate.Height, HighlightSlabM) }, new Transform3D(Basis.Identity, CourseGateShape.OpeningCentre(gate)))
            : (new CylinderMesh { TopRadius = 0.5f * gate.Diameter, BottomRadius = 0.5f * gate.Diameter, Height = HighlightSlabM, RadialSegments = 48, Rings = 1 },
                new Transform3D(new Basis(Vector3.Right, Mathf.Pi / 2f), Vector3.Zero));

    // A cue on the course shader, so no new shader compiles mid-flight.
    private static MeshInstance3D Cue(string name, Mesh? mesh, Vector3 srgb, float fade)
    {
        var material = Material(srgb, srgb, 0f, byAngle: false, 1f, 0f);
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

    // Two bars corner to corner across the clear opening, about its middle. A pylon gate's span its
    // rectangle between the course pylons' centrelines, a ring's cross its disc at 45 degrees. Closed
    // boxes, so either side draws front faces.
    private static Mesh CrossMesh(CourseGate gate)
    {
        bool pylons = gate.Kind == CourseGateKind.PylonGate;
        float bar = CrossBarShare * (pylons ? Mathf.Min(gate.Width, gate.Height) : gate.Diameter);
        float tilt = pylons ? Mathf.Atan2(gate.Height, gate.Width) : Mathf.Pi / 4f;
        var (sin, cos) = Mathf.SinCos(tilt);
        // The longest tilted bar whose corners stay inside the rectangle, or the ring's diameter less its width.
        float length = pylons
            ? Mathf.Min((gate.Width - (bar * sin)) / cos, (gate.Height - (bar * cos)) / sin)
            : gate.Diameter - bar;
        var box = new BoxMesh { Size = new Vector3(length, bar, CrossDepthM) };
        var st = new SurfaceTool();
        foreach (float turn in new[] { tilt, -tilt })
        {
            st.AppendFrom(box, 0, new Transform3D(new Basis(Vector3.Back, turn), Vector3.Zero));
        }

        return st.Commit();
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
    // course pylon. The top's margin covers it tipped flat.
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

    // A new entry starts hidden. Forward entries are flown along the gate's -Z.
    private void PoseCross(MeshInstance3D cross, int entry, double seconds)
    {
        if (entry != _crossEntry)
        {
            (_crossEntry, _wrongSide) = (entry, false);
        }

        if (entry < 0 || _run?.PlanePosition is not { } plane)
        {
            cross.Visible = _wrongSide = false;
            return;
        }

        int g = _entryGates[entry];
        var gate = _course.Gates[g];
        var pose = CourseMotion.Pose(gate, seconds);
        var from = (pose.AffineInverse() * plane) - CourseGateShape.OpeningCentre(gate);
        bool reverse = _course.Sequence[entry].Direction == GateDirection.Reverse;
        _wrongSide = WrongSide(_wrongSide, reverse ? from.Z : -from.Z, from.Length());
        cross.Visible = _wrongSide;
        cross.Mesh = _crosses[g];
        cross.Transform = pose * new Transform3D(Basis.Identity, CourseGateShape.OpeningCentre(gate));
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
        private StuntMission? _by;

        public void Cut(float heightM, double seconds, StuntMission by)
        {
            (_cut, _top) = CoursePylonCut.Recut(gate.Height, heightM, Look(seconds));
            _at = seconds;
            _by = by;
        }

        public void Restore(StuntMission by)
        {
            if (by == _by)
            {
                _at = double.NaN;
            }
        }

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
