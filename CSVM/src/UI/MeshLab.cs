using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight;
using Godot;

namespace CSVM.UI;

/// <summary>
/// The static viewer's mesh lab (<c>--viewer</c>, key M): geometry and shading
/// diagnostics for one aircraft — normal vectors, wireframe with smoothing seams, the
/// collision zone boxes, steerable lighting, and live overrides of the two render decisions
/// that shading artifacts usually trace back to.
///
/// It exists because "this patch looks wrong" is not diagnosable from a screenshot: the
/// candidate causes (a normal pointing the wrong way, a polygon single-sided that should be
/// double-sided, a polygon falling back to a flat face normal because the file carries none
/// for it, or simply a texture that was never painted) all look alike on a dark model. The
/// lab separates them by letting you change exactly one of those at a time and watch.
///
/// <para><b>Override fidelity.</b> The <see cref="CullOverride"/> / <see cref="NormalSource"/>
/// modes swap in the lab's own shader, which deliberately replicates SceneBuilder's vertex
/// stage verbatim — <c>skip_vertex_transform</c> plus the same <c>depth_bias</c>/<c>node_bias</c>
/// scale-toward-eye — so an override differs from the shipped render in the culling or the
/// normal and in nothing else. Without that the coplanar decals would start z-fighting the
/// moment you toggled anything and every comparison would be worthless.</para>
///
/// <para><b>Provenance is inferred, not plumbed.</b> SceneBuilder does not record which
/// normals came from the file and which came from its flat fallback, and threading that
/// through the shared world/aircraft builder for a debug view is not worth it. Instead a
/// triangle is called FLAT when its three corner normals are equal to each other and to the
/// winding normal — which is exactly what the fallback produces. A genuinely flat-shaded
/// polygon whose file normals happen to equal its face normal reads as FLAT too; that is a
/// tolerable false positive for a diagnostic and it is called out in the panel.</para>
///
/// Starts hidden like the other two labs, so an unadorned <c>--viewer</c> screenshot stays
/// byte-identical. M toggles it; DamageLab owns H and LiveryLab owns L, so all three can be
/// open at once.
/// </summary>
public sealed partial class MeshLab : Node
{
    // ---- modes ------------------------------------------------------------------------

    /// <summary>What the normal lines are coloured by. Provenance is the default because it
    /// is the one that maps onto a real question (is this patch flat-shaded?); direction is
    /// the conventional engine view; winding tests whether a normal opposes its own polygon.</summary>
    public enum NormalColorMode { Provenance, Direction, Winding }

    /// <summary>Off / one line per triangle at its centroid / one line per triangle corner.
    /// Per-corner is the honest picture (it shows split vertices) but it is ~3× the lines
    /// and reads as a hairball on a small aircraft, so per-face is the default.</summary>
    public enum NormalDensity { Off, PerFace, PerCorner }

    public enum WireMode { Off, Edges, EdgesAndSeams, SeamsOnly }

    /// <summary>Culling override. AsData honours the per-polygon SHOW_BACKFACE flag the
    /// builder read (unk2); Inverted is cull_back — i.e. the opposite of this project's
    /// "the visible side is the CCW loop" reading, so it tests that convention directly.</summary>
    public enum CullOverride { AsData, AllDoubleSided, AllSingleSided, Inverted }

    /// <summary>Normal override. AllFlat derives a geometric normal per fragment from screen
    /// derivatives; AllSmooth re-welds the mesh on the CPU and area-averages; Negated is the
    /// sanity check that shows what genuinely-reversed normals look like on this model.</summary>
    public enum NormalSource { AsData, AllFlat, AllSmooth, Negated }

    // ---- state ------------------------------------------------------------------------

    private readonly Node3D _planeRoot;
    private readonly PlaneCollider? _collider;
    private readonly DirectionalLight3D _sun;
    private readonly Godot.Environment? _env;
    private readonly Camera3D _camera;

    private NormalColorMode _colorMode = NormalColorMode.Provenance;
    private NormalDensity _density = NormalDensity.Off;
    private WireMode _wire = WireMode.Off;
    private CullOverride _cull = CullOverride.AsData;
    private NormalSource _normals = NormalSource.AsData;
    private bool _boxes, _headlight, _engineWireframe;

    private CanvasLayer _ui = null!;
    private Label _statusLabel = null!;
    private Label _countsLabel = null!;
    private MeshInstance3D _normalDraw = null!, _wireDraw = null!, _boxDraw = null!;
    private bool _suppressCallbacks;

    // The aircraft's geometry, read back once from the built ArrayMeshes and cached in the
    // plane's own frame. Rebuilding the overlays is then pure line emission.
    private readonly List<Surf> _surfaces = new();
    private int _triCount, _flatCount, _twoSidedTris;

    /// <summary>One committed surface, flattened into plane-local space. Indices are always
    /// materialised (SurfaceTool commits indexed, but an unindexed surface is legal).</summary>
    private sealed class Surf
    {
        public required MeshInstance3D Instance;
        public required int Index;
        public required Vector3[] Verts;
        public required Vector3[] Normals;
        public required int[] Tris;
        public required bool DoubleSided;
        public required Material? Original;
        public Mesh? OriginalMesh;
    }

    /// <summary>--debug-mesh[=spec]: open the panel at launch and preset modes, so one
    /// scripted screenshot exercises the overlays rather than an empty panel. Same role as
    /// --debug-livery / --debug-scoreboard. Null = flag absent.</summary>
    public string? DebugSpec { get; init; }

    public MeshLab(Node3D planeRoot, PlaneCollider? collider, DirectionalLight3D sun,
        Godot.Environment? env, Camera3D camera)
    {
        _planeRoot = planeRoot;
        _collider = collider;
        _sun = sun;
        _env = env;
        _camera = camera;
        Name = "mesh_lab";
    }

    public override void _Ready()
    {
        CollectGeometry();
        BuildDrawNodes();
        SeedLighting();
        BuildUi();
        if (DebugSpec != null)
        {
            ApplyDebugSpec(DebugSpec);
            _ui.Visible = true;
        }
        // Lighting is only written when the flag asked for it. Merely constructing the lab
        // must not re-aim the sun: it did in the first cut (a hardcoded default direction),
        // and the untouched --viewer screenshot stopped being byte-identical — caught by md5.
        RefreshAll(applyLighting: DebugSpec != null);

        for (int i = 0; i < _debugCycles; i++)
        {
            _normals = Cycle(_normals);
            ApplyOverrides();
            GD.Print($"[mesh] --debug-mesh cycle {i + 1}/{_debugCycles} → source={_normals}");
        }
        if (_debugCycles > 0)
        {
            SyncWidgets();
            UpdateStatus();
        }
    }

    private int _debugCycles;

    /// <summary>Adopts the viewer's existing lighting as the lab's starting state instead of
    /// inventing one, so the sliders open on what you are actually looking at and an untouched
    /// lab writes nothing.</summary>
    private void SeedLighting()
    {
        _sunDir = -_sun.GlobalTransform.Basis.Z;
        _sunEnergy = _sun.LightEnergy;
        if (_env != null)
        {
            _ambientEnergy = _env.AmbientLightEnergy;
            _ambientOn = _ambientEnergy > 0f;
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } k)
            return;
        switch (k.Keycode)
        {
            case Key.M: _ui.Visible = !_ui.Visible; return;
            case Key.G: _density = Cycle(_density); RebuildNormals(); break;
            case Key.N: _colorMode = Cycle(_colorMode); RebuildNormals(); break;
            case Key.W: _wire = Cycle(_wire); RebuildWireframe(); break;
            case Key.B: _boxes = !_boxes; RebuildBoxes(); break;
            case Key.C: _cull = Cycle(_cull); ApplyOverrides(); break;
            case Key.V: _normals = Cycle(_normals); ApplyOverrides(); break;
            default: return;
        }
        SyncWidgets();
        UpdateStatus();
    }

    public override void _Process(double delta)
    {
        if (!_headlight)
            return;
        // Headlight: the sun rides the camera, so every surface facing you is lit by
        // definition and one that stays dark while pointed at you has a bad normal. This is
        // the sharpest normals test the lab has, and it needs no reasoning about geometry.
        var fwd = -_camera.GlobalTransform.Basis.Z;
        AimSun(fwd);
    }

    private static T Cycle<T>(T value) where T : struct, Enum
    {
        var all = Enum.GetValues<T>();
        int i = Array.IndexOf(all, value);
        return all[(i + 1) % all.Length];
    }

    // ---- geometry read-back -------------------------------------------------------------

    /// <summary>Reads every built surface back out of its ArrayMesh into plane-local space.
    /// Done once: the parked aircraft never moves, and re-reading per toggle would make the
    /// cyclers feel sticky on the bigger models.</summary>
    private void CollectGeometry()
    {
        var toPlane = _planeRoot.GlobalTransform.AffineInverse();
        foreach (var inst in Descendants(_planeRoot).OfType<MeshInstance3D>())
        {
            if (inst.Mesh is not ArrayMesh am)
                continue;
            var xf = toPlane * inst.GlobalTransform;
            var nrm = xf.Basis.Inverse().Transposed(); // correct under any non-uniform scale
            for (int s = 0; s < am.GetSurfaceCount(); s++)
            {
                var arrays = am.SurfaceGetArrays(s);
                var vArr = arrays[(int)Mesh.ArrayType.Vertex];
                var nArr = arrays[(int)Mesh.ArrayType.Normal];
                if (vArr.VariantType == Variant.Type.Nil)
                    continue;
                var verts = vArr.AsVector3Array();
                var norms = nArr.VariantType == Variant.Type.Nil
                    ? Array.Empty<Vector3>() : nArr.AsVector3Array();
                var iArr = arrays[(int)Mesh.ArrayType.Index];
                var tris = iArr.VariantType == Variant.Type.Nil
                    ? Enumerable.Range(0, verts.Length).ToArray() : iArr.AsInt32Array();

                var mat = am.SurfaceGetMaterial(s);
                _surfaces.Add(new Surf
                {
                    Instance = inst,
                    Index = s,
                    Verts = verts.Select(v => xf * v).ToArray(),
                    Normals = norms.Select(n => (nrm * n).Normalized()).ToArray(),
                    Tris = tris,
                    // Sidedness is not exposed on the material, but SceneBuilder bakes it into
                    // the generated shader's render_mode — so read it back from the code. Hacky
                    // but honest, and it avoids widening SceneBuilder's API for a debug view.
                    DoubleSided = mat is ShaderMaterial sm && sm.Shader != null
                                  && sm.Shader.Code.Contains("cull_disabled"),
                    Original = mat,
                    OriginalMesh = am,
                });
            }
        }
        TallyTriangles();
    }

    private void TallyTriangles()
    {
        _triCount = _flatCount = _twoSidedTris = 0;
        foreach (var s in _surfaces)
            for (int t = 0; t + 2 < s.Tris.Length; t += 3)
            {
                _triCount++;
                if (s.DoubleSided)
                    _twoSidedTris++;
                if (IsFlatFallback(s, t))
                    _flatCount++;
            }
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var c in root.GetChildren())
        {
            yield return c;
            foreach (var d in Descendants(c))
                yield return d;
        }
    }

    // ---- provenance ---------------------------------------------------------------------

    private static Vector3 FaceNormal(Surf s, int t)
    {
        var a = s.Verts[s.Tris[t]];
        var b = s.Verts[s.Tris[t + 1]];
        var c = s.Verts[s.Tris[t + 2]];
        var n = (b - a).Cross(c - a);
        return n.LengthSquared() > 1e-14f ? n.Normalized() : Vector3.Up;
    }

    /// <summary>True when this triangle's three corner normals are equal to each other and to
    /// its winding normal — the exact signature of SceneBuilder's flat fallback. See the class
    /// doc for the false-positive case.</summary>
    private static bool IsFlatFallback(Surf s, int t)
    {
        if (s.Normals.Length == 0)
            return true;
        var n0 = s.Normals[s.Tris[t]];
        var n1 = s.Normals[s.Tris[t + 1]];
        var n2 = s.Normals[s.Tris[t + 2]];
        const float eps = 1e-3f;
        if ((n0 - n1).LengthSquared() > eps || (n0 - n2).LengthSquared() > eps)
            return false;
        return (n0 - FaceNormal(s, t)).LengthSquared() <= eps;
    }

    // ---- overlays -----------------------------------------------------------------------

    private void BuildDrawNodes()
    {
        _normalDraw = MakeDrawNode("mesh_lab_normals");
        _wireDraw = MakeDrawNode("mesh_lab_wire");
        _boxDraw = MakeDrawNode("mesh_lab_boxes");
    }

    private MeshInstance3D MakeDrawNode(string name)
    {
        var mi = new MeshInstance3D
        {
            Name = name,
            Mesh = new ImmediateMesh(),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
        };
        _planeRoot.AddChild(mi);
        return mi;
    }

    private void RefreshAll(bool applyLighting = true)
    {
        RebuildNormals();
        RebuildWireframe();
        RebuildBoxes();
        ApplyOverrides();
        if (applyLighting)
            ApplyLighting();
        SyncWidgets();
        UpdateStatus();
    }

    // Normal line length as a fraction of the aircraft's bounding radius, so the lines read
    // the same on the Balmoral and the autogyro. TUNE.
    private const float NormalLenFrac = 0.022f;

    private void RebuildNormals()
    {
        var im = (ImmediateMesh)_normalDraw.Mesh;
        im.ClearSurfaces();
        if (_density == NormalDensity.Off || _surfaces.Count == 0)
            return;

        float len = Mathf.Max(BoundingRadius() * NormalLenFrac, 0.01f);
        im.SurfaceBegin(Mesh.PrimitiveType.Lines);
        foreach (var s in _surfaces)
            for (int t = 0; t + 2 < s.Tris.Length; t += 3)
            {
                bool flat = IsFlatFallback(s, t);
                var face = FaceNormal(s, t);
                if (_density == NormalDensity.PerFace)
                {
                    var c = (s.Verts[s.Tris[t]] + s.Verts[s.Tris[t + 1]] + s.Verts[s.Tris[t + 2]]) / 3f;
                    var n = s.Normals.Length == 0 ? face
                        : (s.Normals[s.Tris[t]] + s.Normals[s.Tris[t + 1]] + s.Normals[s.Tris[t + 2]]).Normalized();
                    EmitNormal(im, c, n, face, len, flat, s.DoubleSided);
                }
                else
                {
                    for (int k = 0; k < 3; k++)
                    {
                        int vi = s.Tris[t + k];
                        var n = s.Normals.Length == 0 ? face : s.Normals[vi];
                        EmitNormal(im, s.Verts[vi], n, face, len, flat, s.DoubleSided);
                    }
                }
            }
        im.SurfaceEnd();
    }

    private void EmitNormal(ImmediateMesh im, Vector3 at, Vector3 n, Vector3 face,
        float len, bool flat, bool doubleSided)
    {
        var col = _colorMode switch
        {
            // Provenance: hue = where the normal came from, brightness = sidedness. This is
            // the encoding that answers "is this patch flat-shaded, and is it single-sided?".
            NormalColorMode.Provenance => flat
                ? (doubleSided ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.6f, 0.1f))
                : (doubleSided ? new Color(0.35f, 0.5f, 1f) : new Color(0.2f, 0.95f, 0.95f)),
            NormalColorMode.Direction => new Color(n.X * 0.5f + 0.5f, n.Y * 0.5f + 0.5f, n.Z * 0.5f + 0.5f),
            _ => n.Dot(face) < 0f ? new Color(1f, 0.15f, 0.15f) : new Color(0.2f, 1f, 0.3f),
        };
        // Root slightly brighter than the tip so the direction reads without arrowheads.
        im.SurfaceSetColor(col);
        im.SurfaceAddVertex(at);
        im.SurfaceSetColor(col with { A = 0.15f });
        im.SurfaceAddVertex(at + n * len);
    }

    private float BoundingRadius()
    {
        float r = 0f;
        foreach (var s in _surfaces)
            foreach (var v in s.Verts)
                r = Mathf.Max(r, v.Length());
        return r;
    }

    // ---- wireframe ----------------------------------------------------------------------

    private void RebuildWireframe()
    {
        var im = (ImmediateMesh)_wireDraw.Mesh;
        im.ClearSurfaces();
        if (_wire == WireMode.Off)
            return;

        var ordinary = new Color(0.55f, 0.55f, 0.6f, 0.5f);
        var boundary = new Color(1f, 1f, 1f, 0.85f);
        var seam = new Color(1f, 0.15f, 0.15f, 0.95f);

        im.SurfaceBegin(Mesh.PrimitiveType.Lines);
        foreach (var s in _surfaces)
        {
            // Edge → the (position-keyed) triangles touching it. Keyed by quantised position
            // rather than vertex index because SurfaceTool splits a vertex wherever the
            // normal differs — which is precisely the seam we are trying to find, so index
            // identity would make every seam invisible.
            var edges = new Dictionary<(long, long), List<(int Tri, int A, int B)>>();
            for (int t = 0; t + 2 < s.Tris.Length; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    int ia = s.Tris[t + k], ib = s.Tris[t + (k + 1) % 3];
                    var key = EdgeKey(s.Verts[ia], s.Verts[ib]);
                    if (!edges.TryGetValue(key, out var list))
                        edges[key] = list = new List<(int, int, int)>();
                    list.Add((t, ia, ib));
                }

            foreach (var (_, list) in edges)
            {
                var (_, ia, ib) = list[0];
                Color col;
                if (list.Count == 1)
                    col = boundary;
                else if (IsSeam(s, list))
                    col = seam;
                else
                    col = ordinary;

                bool isSeam = col == seam;
                if (_wire == WireMode.SeamsOnly && !isSeam)
                    continue;
                if (_wire == WireMode.Edges && isSeam)
                    col = ordinary; // plain edges only — no seam highlighting
                im.SurfaceSetColor(col);
                im.SurfaceAddVertex(s.Verts[ia]);
                im.SurfaceAddVertex(s.Verts[ib]);
            }
        }
        im.SurfaceEnd();
    }

    private static (long, long) EdgeKey(Vector3 a, Vector3 b)
    {
        long ka = Quantise(a), kb = Quantise(b);
        return ka <= kb ? (ka, kb) : (kb, ka);
    }

    private static long Quantise(Vector3 v)
    {
        // 0.1 mm buckets: far below any real feature on these models, far above float noise.
        unchecked
        {
            long h = (long)Mathf.Round(v.X * 10000f);
            h = h * 1000003 + (long)Mathf.Round(v.Y * 10000f);
            h = h * 1000003 + (long)Mathf.Round(v.Z * 10000f);
            return h;
        }
    }

    /// <summary>An edge is a hard smoothing seam when the triangles meeting on it disagree
    /// about the normal at a shared position — i.e. the shading breaks across this edge.
    /// These outline the flat/smooth patch boundaries directly.</summary>
    private static bool IsSeam(Surf s, List<(int Tri, int A, int B)> list)
    {
        if (s.Normals.Length == 0 || list.Count < 2)
            return false;
        var (_, a0, b0) = list[0];
        for (int i = 1; i < list.Count; i++)
        {
            var (_, a1, b1) = list[i];
            // the two triangles may name the edge in either order
            bool sameOrder = Quantise(s.Verts[a0]) == Quantise(s.Verts[a1]);
            var na = s.Normals[sameOrder ? a1 : b1];
            var nb = s.Normals[sameOrder ? b1 : a1];
            if ((s.Normals[a0] - na).LengthSquared() > 1e-4f
                || (s.Normals[b0] - nb).LengthSquared() > 1e-4f)
                return true;
        }
        return false;
    }

    // ---- zone boxes ---------------------------------------------------------------------

    private static readonly Dictionary<string, Color> PartColors = new()
    {
        ["nose"] = new Color(1f, 0.9f, 0.2f),
        ["tail"] = new Color(1f, 0.3f, 1f),
        ["leftwing"] = new Color(0.2f, 0.9f, 1f),
        ["rightwing"] = new Color(1f, 0.55f, 0.1f),
    };

    private void RebuildBoxes()
    {
        var im = (ImmediateMesh)_boxDraw.Mesh;
        im.ClearSurfaces();
        if (!_boxes || _collider == null)
            return;

        im.SurfaceBegin(Mesh.PrimitiveType.Lines);
        foreach (var part in _collider.Parts)
        {
            var size = part.Shape.Size;
            var centre = part.Local.Origin;
            // A wing slab straddling the centreline maps to leftwing OR rightwing depending on
            // where it is struck, so a single colour would be a lie. Split it at x=0 and draw
            // each half in its own part colour — the ambiguity becomes visible instead.
            bool straddles = (part.Name is "wing" or "canard")
                             && Mathf.Abs(centre.X) < size.X * 0.5f;
            if (!straddles)
            {
                var name = PlaneDamage.MapStruckPart(part.Name, centre);
                EmitBox(im, part.Local, size, ColorFor(name));
                continue;
            }
            for (int side = 0; side < 2; side++)
            {
                float lo = side == 0 ? centre.X - size.X * 0.5f : 0f;
                float hi = side == 0 ? 0f : centre.X + size.X * 0.5f;
                var half = new Vector3(hi - lo, size.Y, size.Z);
                var xf = part.Local;
                xf.Origin = new Vector3((lo + hi) * 0.5f, centre.Y, centre.Z);
                var probe = new Vector3(side == 0 ? -1f : 1f, centre.Y, centre.Z);
                EmitBox(im, xf, half, ColorFor(PlaneDamage.MapStruckPart(part.Name, probe)));
            }
        }
        im.SurfaceEnd();
    }

    private static Color ColorFor(string part) =>
        PartColors.TryGetValue(part, out var c) ? c : new Color(0.7f, 0.7f, 0.7f);

    private static void EmitBox(ImmediateMesh im, Transform3D xf, Vector3 size, Color col)
    {
        var h = size * 0.5f;
        Span<Vector3> corners = stackalloc Vector3[8];
        for (int i = 0; i < 8; i++)
            corners[i] = xf * new Vector3(
                (i & 1) == 0 ? -h.X : h.X,
                (i & 2) == 0 ? -h.Y : h.Y,
                (i & 4) == 0 ? -h.Z : h.Z);
        ReadOnlySpan<int> pairs = stackalloc int[]
        {
            0,1, 2,3, 4,5, 6,7,   // x
            0,2, 1,3, 4,6, 5,7,   // y
            0,4, 1,5, 2,6, 3,7,   // z
        };
        im.SurfaceSetColor(col);
        for (int i = 0; i < pairs.Length; i += 2)
        {
            im.SurfaceAddVertex(corners[pairs[i]]);
            im.SurfaceAddVertex(corners[pairs[i + 1]]);
        }
    }

    // ---- render overrides ----------------------------------------------------------------

    private readonly Dictionary<int, Shader> _overrideShaders = new();

    private void ApplyOverrides()
    {
        bool off = _cull == CullOverride.AsData && _normals == NormalSource.AsData;
        foreach (var s in _surfaces)
        {
            if (off)
            {
                SetOverride(s, null);
                RestoreMesh(s);
                continue;
            }
            if (_normals == NormalSource.AllSmooth)
                SmoothMesh(s);
            else
                RestoreMesh(s);
            SetOverride(s, OverrideMaterial(s));
        }
        // Engine wireframe is viewport-wide; the labs' panels are CanvasLayer 2D so they stay
        // readable, and only the 3D view goes to lines.
        GetViewport().DebugDraw = _engineWireframe
            ? Viewport.DebugDrawEnum.Wireframe : Viewport.DebugDrawEnum.Disabled;
    }

    private ShaderMaterial OverrideMaterial(Surf s)
    {
        bool textured = s.Original is ShaderMaterial osm
                        && osm.GetShaderParameter("albedo_tex").VariantType != Variant.Type.Nil;
        var mat = new ShaderMaterial { Shader = GetOverrideShader(CullFor(s), textured) };
        if (s.Original is ShaderMaterial src)
        {
            if (textured)
                mat.SetShaderParameter("albedo_tex", src.GetShaderParameter("albedo_tex"));
            else
                mat.SetShaderParameter("albedo_color", src.GetShaderParameter("albedo_color"));
            // Carry the depth bias so overridden surfaces keep their coplanar layering — an
            // override must differ in the thing under test and nothing else.
            mat.SetShaderParameter("depth_bias", src.GetShaderParameter("depth_bias"));
        }
        mat.SetShaderParameter("normal_mode", _normals switch
        {
            NormalSource.AllFlat => 1,
            NormalSource.Negated => 2,
            _ => 0, // AsData and AllSmooth both use the mesh's normals (smooth swaps the mesh)
        });
        return mat;
    }

    private BaseMaterial3D.CullModeEnum CullFor(Surf s) => _cull switch
    {
        CullOverride.AllDoubleSided => BaseMaterial3D.CullModeEnum.Disabled,
        CullOverride.AllSingleSided => BaseMaterial3D.CullModeEnum.Front,
        CullOverride.Inverted => BaseMaterial3D.CullModeEnum.Back,
        _ => s.DoubleSided ? BaseMaterial3D.CullModeEnum.Disabled : BaseMaterial3D.CullModeEnum.Front,
    };

    private Shader GetOverrideShader(BaseMaterial3D.CullModeEnum cull, bool textured)
    {
        int key = (int)cull * 2 + (textured ? 1 : 0);
        if (_overrideShaders.TryGetValue(key, out var cached))
            return cached;
        string mode = cull switch
        {
            BaseMaterial3D.CullModeEnum.Disabled => "cull_disabled",
            BaseMaterial3D.CullModeEnum.Back => "cull_back",
            _ => "cull_front",
        };
        // Vertex stage is SceneBuilder's, verbatim (see the class doc): same
        // skip_vertex_transform + scale-toward-eye depth bias, so only the tested thing differs.
        string code = $@"
shader_type spatial;
render_mode skip_vertex_transform, {mode};
uniform float depth_bias = 0.0;
// The shared ordered instance-uniform block — this shader reads only node_bias, but it must
// declare the canonical order like every other (see csky_instance_uniforms.gdshaderinc). A
// diagnostic that disagrees with the shipped renderer is worse than useless: MeshLab's whole
// contract is that its vertex stage is SceneBuilder's verbatim.
#include ""res://shaders/csky_instance_uniforms.gdshaderinc""
uniform int normal_mode = 0;
{(textured
    ? "uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;"
    : "uniform vec4 albedo_color : source_color = vec4(1.0);")}

void vertex() {{
    // Leading minus mirrors SceneBuilder's shaded path: it cancels the engine's back-face
    // normal flip, which cull_front makes universal on aircraft. AsData therefore matches
    // the shipped renderer, and Negated flips relative to that (= the old inside-out look),
    // which is what makes it useful as a sanity mode.
    VERTEX = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
    NORMAL = -normalize(MODELVIEW_NORMAL_MATRIX * NORMAL);
    VERTEX *= 1.0 - (depth_bias + node_bias);
}}

void fragment() {{
    vec4 col = COLOR * {(textured ? "texture(albedo_tex, UV)" : "albedo_color")};
    ALBEDO = col.rgb;
    ROUGHNESS = 0.85;
    METALLIC = 0.0;
    SPECULAR = 0.5;
    if (normal_mode == 1) {{
        // Geometric normal from screen derivatives of the view-space position. Sign follows
        // the rendered side (FRONT_FACING) so it stays predictable under any cull mode.
        vec3 g = normalize(cross(dFdx(VERTEX), dFdy(VERTEX)));
        NORMAL = FRONT_FACING ? g : -g;
    }} else if (normal_mode == 2) {{
        NORMAL = -NORMAL;
    }}
}}";
        var shader = new Shader { Code = code };
        _overrideShaders[key] = shader;
        return shader;
    }

    // ---- smooth-normal rebuild -------------------------------------------------------------

    private readonly Dictionary<Surf, ArrayMesh> _smoothed = new();

    /// <summary>Swaps in a copy of this surface's mesh with area-weighted vertex normals,
    /// welded by position — the "what if every polygon were smooth-shaded" comparison. Cached,
    /// so cycling back to it is instant.</summary>
    private void SmoothMesh(Surf s)
    {
        if (s.Instance.Mesh is ArrayMesh cur && _smoothed.TryGetValue(s, out var done) && cur == done)
            return;
        if (!_smoothed.TryGetValue(s, out var smooth))
        {
            var acc = new Dictionary<long, Vector3>();
            for (int t = 0; t + 2 < s.Tris.Length; t += 3)
            {
                var a = s.Verts[s.Tris[t]];
                var b = s.Verts[s.Tris[t + 1]];
                var c = s.Verts[s.Tris[t + 2]];
                var w = (b - a).Cross(c - a); // unnormalised == area-weighted
                foreach (var v in stackalloc[] { a, b, c })
                {
                    long k = Quantise(v);
                    acc[k] = acc.TryGetValue(k, out var prev) ? prev + w : w;
                }
            }
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);
            for (int t = 0; t + 2 < s.Tris.Length; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    int vi = s.Tris[t + k];
                    var v = s.Verts[vi];
                    var n = acc[Quantise(v)];
                    st.SetNormal(n.LengthSquared() > 1e-12f ? n.Normalized() : FaceNormal(s, t));
                    st.AddVertex(v);
                }
            smooth = st.Commit();
            _smoothed[s] = smooth;
        }
        // Only meaningful when the instance holds exactly this one surface; multi-surface
        // instances keep their original mesh (the shader-side modes still apply to them).
        // A triangle-less surface commits to a 0-surface mesh — swapping that in would leave
        // the instance with nothing to render or override.
        if (s.OriginalMesh is ArrayMesh am && am.GetSurfaceCount() == 1
            && smooth.GetSurfaceCount() == 1)
            s.Instance.Mesh = smooth;
    }

    private static bool _warnedOverrideGap;

    /// <summary>Sets (or clears) this surface's override material.
    ///
    /// <para>Godot bounds-checks the <b>instance's</b> <c>surface_override_materials</c> array,
    /// which is NOT the same number as the mesh's surface count: MeshInstance3D sizes that array
    /// when the mesh is assigned, so a mesh whose surfaces were committed <i>afterwards</i> —
    /// which is exactly what SceneBuilder does, committing into an already-assigned ArrayMesh —
    /// can leave it short. Checking the mesh instead was the first cut's bug and it errors per
    /// call (<c>p_surface = 0 is out of bounds (size() = 0)</c>). Re-assigning the mesh forces
    /// the resize.</para>
    ///
    /// <para>It recovers rather than skips because this is a diagnostic lab: a silently
    /// un-overridden surface means half an A/B, and a conclusion drawn from it would be
    /// wrong. Clearing (mat null) on a missing slot is genuinely a no-op, so that returns
    /// early and startup — where every surface is cleared — touches nothing.</para></summary>
    private static void SetOverride(Surf s, Material? mat)
    {
        var mesh = s.Instance.Mesh;
        if (mesh == null || s.Index >= mesh.GetSurfaceCount())
            return;
        if (mat == null && s.Instance.GetSurfaceOverrideMaterialCount() <= s.Index)
            return; // no slot ⇒ nothing was ever set there ⇒ nothing to clear
        if (s.Instance.GetSurfaceOverrideMaterialCount() <= s.Index)
        {
            s.Instance.Mesh = null;
            s.Instance.Mesh = mesh;
        }
        if (s.Instance.GetSurfaceOverrideMaterialCount() > s.Index)
        {
            s.Instance.SetSurfaceOverrideMaterial(s.Index, mat);
        }
        else if (!_warnedOverrideGap)
        {
            _warnedOverrideGap = true;
            GD.Print($"[mesh] '{s.Instance.Name}' surface {s.Index} has no override slot — "
                     + "override not applied there (results for that surface are not A/B'd)");
        }
    }

    private void RestoreMesh(Surf s)
    {
        if (s.OriginalMesh != null && s.Instance.Mesh != s.OriginalMesh)
            s.Instance.Mesh = s.OriginalMesh;
    }

    // ---- lighting -------------------------------------------------------------------------

    private Vector3 _sunDir = new(-0.5f, -0.7f, 0.5f);
    private float _sunEnergy = 1.6f, _ambientEnergy = 0.9f;
    private bool _ambientOn = true;

    private void ApplyLighting()
    {
        _sun.LightEnergy = _sunEnergy;
        if (!_headlight)
            AimSun(_sunDir);
        if (_env != null)
            _env.AmbientLightEnergy = _ambientOn ? _ambientEnergy : 0f;
    }

    private void AimSun(Vector3 dir)
    {
        if (dir.LengthSquared() < 1e-6f)
            return;
        dir = dir.Normalized();
        // LookAt degenerates when the direction is parallel to the up hint — pick another.
        var up = Mathf.Abs(dir.Dot(Vector3.Up)) > 0.999f ? Vector3.Forward : Vector3.Up;
        _sun.LookAtFromPosition(_sun.GlobalPosition, _sun.GlobalPosition + dir, up);
    }

    // ---- --debug-mesh spec ------------------------------------------------------------------

    /// <summary>Presets modes from the flag's comma-separated spec, e.g.
    /// <c>--debug-mesh=normals,wire=seams,cull=double</c>. Unknown tokens are reported rather
    /// than ignored, so a typo in a scripted screenshot does not silently shoot the wrong thing.</summary>
    private void ApplyDebugSpec(string spec)
    {
        foreach (var raw in spec.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim().ToLowerInvariant();
            string key = token, value = "";
            int eq = token.IndexOf('=');
            if (eq >= 0)
            {
                key = token[..eq];
                value = token[(eq + 1)..];
            }
            switch (key)
            {
                case "": break;
                case "normals": _density = value == "corners" ? NormalDensity.PerCorner : NormalDensity.PerFace; break;
                case "color": _colorMode = value switch
                {
                    "direction" => NormalColorMode.Direction,
                    "winding" => NormalColorMode.Winding,
                    _ => NormalColorMode.Provenance,
                }; break;
                case "wire": _wire = value switch
                {
                    "seams" => WireMode.EdgesAndSeams,
                    "seamsonly" => WireMode.SeamsOnly,
                    _ => WireMode.Edges,
                }; break;
                case "boxes": _boxes = true; break;
                case "headlight": _headlight = true; break;
                case "enginewire": _engineWireframe = true; break;
                case "cull": _cull = value switch
                {
                    "double" => CullOverride.AllDoubleSided,
                    "single" => CullOverride.AllSingleSided,
                    "inverted" => CullOverride.Inverted,
                    _ => CullOverride.AsData,
                }; break;
                case "source": _normals = value switch
                {
                    "flat" => NormalSource.AllFlat,
                    "smooth" => NormalSource.AllSmooth,
                    "negated" => NormalSource.Negated,
                    _ => NormalSource.AsData,
                }; break;
                case "ambient": _ambientOn = value != "off"; break;
                case "cycle":
                    // cycle=N — step the normal-source cycler N times at launch, the headless
                    // equivalent of clicking it (same convention as --debug-livery=N). Exists
                    // because the reported out-of-bounds crash only showed up on the BUTTON
                    // path, which a one-shot spec never exercised: it takes a second
                    // ApplyOverrides, after a mesh swap, to reach it.
                    if (int.TryParse(value, out int n))
                        _debugCycles = n;
                    break;
                case "dir":
                    // dir=x/y/z — the direction the light TRAVELS, so dir=0/-1/0 is straight
                    // down. Slashes, because commas already separate spec tokens.
                    var parts = value.Split('/');
                    if (parts.Length == 3
                        && float.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float dx)
                        && float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float dy)
                        && float.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float dz))
                        _sunDir = new Vector3(dx, dy, dz);
                    else
                        GD.Print($"[mesh] --debug-mesh: bad dir '{value}' (want dir=x/y/z)");
                    break;
                case "sun": if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float e)) _sunEnergy = e; break;
                default: GD.Print($"[mesh] --debug-mesh: unknown token '{raw.Trim()}'"); break;
            }
        }
        GD.Print($"[mesh] --debug-mesh: normals={_density}/{_colorMode} wire={_wire} boxes={_boxes} "
                 + $"cull={_cull} source={_normals} headlight={_headlight}");
    }

    // ---- UI -----------------------------------------------------------------------------------

    private Button _normalsBtn = null!, _colorBtn = null!, _wireBtn = null!,
        _cullBtn = null!, _sourceBtn = null!;
    private CheckButton _boxBtn = null!, _headlightBtn = null!, _ambientBtn = null!, _engineWireBtn = null!;

    private void BuildUi()
    {
        // Hidden by default, like the other two labs, so an unadorned --viewer screenshot is
        // unchanged. Anchored bottom-left: DamageLab owns top-left, LiveryLab top-right.
        _ui = new CanvasLayer { Layer = 1, Visible = false };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var panel = new PanelContainer { SelfModulate = new Color(1, 1, 1, 0.85f) };
        panel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        panel.GrowVertical = Control.GrowDirection.Begin;
        panel.Position = new Vector2(8, -8);

        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 10);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 3);

        box.AddChild(new Label { Text = "MESH LAB" });
        box.AddChild(Small("M hides this panel · H damage lab · L livery lab"));
        _countsLabel = Small("");
        box.AddChild(_countsLabel);
        box.AddChild(Separator());

        _normalsBtn = CycleRow(box, "normals  [G]", () => { _density = Cycle(_density); RebuildNormals(); });
        _colorBtn = CycleRow(box, "colour   [N]", () => { _colorMode = Cycle(_colorMode); RebuildNormals(); });
        box.AddChild(Small("cyan/blue = from file (1/2-sided)"));
        box.AddChild(Small("orange/red = FLAT FALLBACK (1/2-sided)"));
        _wireBtn = CycleRow(box, "wireframe [W]", () => { _wire = Cycle(_wire); RebuildWireframe(); });
        _boxBtn = CheckRow(box, "zone boxes  [B]", v => { _boxes = v; RebuildBoxes(); });
        _engineWireBtn = CheckRow(box, "engine wireframe", v => { _engineWireframe = v; ApplyOverrides(); });

        box.AddChild(Separator());
        box.AddChild(Small("OVERRIDES — A/B the render decisions"));
        _cullBtn = CycleRow(box, "cull     [C]", () => { _cull = Cycle(_cull); ApplyOverrides(); });
        _sourceBtn = CycleRow(box, "normal src [V]", () => { _normals = Cycle(_normals); ApplyOverrides(); });

        box.AddChild(Separator());
        box.AddChild(Small("LIGHTING"));
        _ambientBtn = CheckRow(box, "ambient", v => { _ambientOn = v; ApplyLighting(); });
        _headlightBtn = CheckRow(box, "headlight (light = camera)", v => { _headlight = v; ApplyLighting(); });
        SliderRow(box, "ambient energy", 0f, 3f, _ambientEnergy, v => { _ambientEnergy = v; ApplyLighting(); });
        SliderRow(box, "sun energy", 0f, 6f, _sunEnergy, v => { _sunEnergy = v; ApplyLighting(); });
        string[] axis = { "dir X", "dir Y", "dir Z" };
        for (int i = 0; i < 3; i++)
        {
            int ax = i;
            SliderRow(box, axis[i], -1f, 1f, _sunDir[i], v =>
            {
                var d = _sunDir;
                d[ax] = v;
                _sunDir = d;
                _headlight = false;
                SyncWidgets();
                ApplyLighting();
            });
        }

        box.AddChild(Separator());
        _statusLabel = Small("");
        box.AddChild(_statusLabel);

        margin.AddChild(box);
        panel.AddChild(margin);
        root.AddChild(panel);
        _ui.AddChild(root);
        AddChild(_ui);
    }

    private Button CycleRow(VBoxContainer parent, string label, Action onPress)
    {
        var row = new HBoxContainer();
        var l = new Label { Text = label, CustomMinimumSize = new Vector2(112, 0) };
        row.AddChild(l);
        var b = new Button { CustomMinimumSize = new Vector2(190, 0) };
        b.Pressed += () =>
        {
            onPress();
            SyncWidgets();
            UpdateStatus();
        };
        row.AddChild(b);
        parent.AddChild(row);
        return b;
    }

    private CheckButton CheckRow(VBoxContainer parent, string label, Action<bool> onToggle)
    {
        var c = new CheckButton { Text = label };
        c.Toggled += v =>
        {
            if (_suppressCallbacks)
                return;
            onToggle(v);
            UpdateStatus();
        };
        parent.AddChild(c);
        return c;
    }

    private void SliderRow(VBoxContainer parent, string label, float min, float max,
        float value, Action<float> onChange)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(112, 0) });
        var s = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = 0.01,
            Value = value,
            CustomMinimumSize = new Vector2(190, 0),
        };
        var read = Small(value.ToString("0.00"));
        s.ValueChanged += v =>
        {
            read.Text = ((float)v).ToString("0.00");
            if (!_suppressCallbacks)
                onChange((float)v);
        };
        row.AddChild(s);
        row.AddChild(read);
        parent.AddChild(row);
    }

    private void SyncWidgets()
    {
        _suppressCallbacks = true;
        _normalsBtn.Text = _density.ToString();
        _colorBtn.Text = _colorMode.ToString();
        _wireBtn.Text = _wire.ToString();
        _cullBtn.Text = _cull.ToString();
        _sourceBtn.Text = _normals.ToString();
        _boxBtn.ButtonPressed = _boxes;
        _headlightBtn.ButtonPressed = _headlight;
        _ambientBtn.ButtonPressed = _ambientOn;
        _engineWireBtn.ButtonPressed = _engineWireframe;
        _suppressCallbacks = false;
    }

    private void UpdateStatus()
    {
        _countsLabel.Text = $"{_surfaces.Count} surfaces · {_triCount} tris · "
                            + $"{_flatCount} flat ({Pct(_flatCount)}) · {_twoSidedTris} 2-sided ({Pct(_twoSidedTris)})";
        _statusLabel.Text = _cull == CullOverride.AsData && _normals == NormalSource.AsData
            ? "render: shipped path"
            : $"render: OVERRIDDEN ({_cull}, {_normals})";
    }

    private string Pct(int n) => _triCount > 0 ? $"{n * 100f / _triCount:0}%" : "—";

    private static Label Small(string text)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", 11);
        return l;
    }

    private static HSeparator Separator() => new();
}
