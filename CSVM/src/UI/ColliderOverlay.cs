using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.UI;

/// <summary>
/// The collision wireframe overlay (key C): every built collider in the session drawn as coloured
/// lines, so "is this thing solid?" and "did that death take its collider away?" are questions you
/// can answer by looking instead of by reading a census.
///
/// <para><b>Its first job is to say when there is nothing to draw.</b> Collision is a flight-build
/// option — <c>--freecam</c>, <c>--anim-lab</c> and <c>--viewer</c> build no bodies at all — so an
/// empty overlay in those modes would read as "nothing here is collidable", which is the exact
/// false conclusion a collider census once nearly reached. Pressing C without <c>--collision</c>
/// therefore prints the reason and draws nothing.</para>
///
/// <para><b>Two kinds of collider, one overlay.</b> World and aircraft geometry hangs its shapes on
/// <see cref="CollisionShape3D"/> nodes; the solid clutter attaches its shared shapes straight to a
/// region body's RID with no node of their own, so those are read back through the physics server.
/// ⚠ Only the server-side <c>BodyGetShape*</c> getters are used on those bodies — any
/// <c>ShapeOwner*</c> call on one would make Godot rebuild it from the nodes it does not have and
/// silently empty it.</para>
///
/// <para><b>A stale overlay is a lie.</b> Killing a destructible switches its healthy collider off
/// and its wreck's on, so the drawing follows each shape's <c>Disabled</c> flag rather than being
/// baked once, and the on/off tallies are reported as two numbers — never as a net, which for the
/// C2 propane gate reads +8 and looks like death ADDING collision.</para>
/// </summary>
public sealed partial class ColliderOverlay : Node
{
    /// <summary>Trimeshes bigger than this draw as their bounding box instead of their triangles: a
    /// terrain tile's collider is tens of thousands of lines and tells you nothing an outline does
    /// not. The count of boxed shapes is reported, never hidden.</summary>
    private const int MaxShapeTris = 2000;

    /// <summary>And a total line budget across the whole overlay; past it every remaining shape is
    /// boxed. A C5 city is ~8k clutter placements.</summary>
    private const int MaxTotalLines = 400000;

    /// <summary>Wireframes sit exactly on the surface they outline, which z-fights into dashes.
    /// Scaling each shape a hair about its own local origin lifts the lines clear.</summary>
    private const float Inflate = 1.0025f;

    // How often the drawing re-reads the Disabled flags. Fast enough that a kill's swap is visibly
    // immediate, cheap enough that it is a list walk, not a tree walk.
    private const double SyncInterval = 0.15;

    // How many names a flip line prints before it says how many more there were. The COUNTS above
    // are never elided — a truncated name list must not read as a smaller change.
    private const int MaxNames = 12;

    private static readonly Color WorldColor = new(0.35f, 0.65f, 1f);
    private static readonly Color WaterColor = new(0.2f, 1f, 0.9f);
    private static readonly Color BuildingColor = new(1f, 0.55f, 0.15f);
    private static readonly Color ClutterColor = new(0.4f, 1f, 0.4f);
    private static readonly Color PlaneColor = new(1f, 0.9f, 0.25f);
    private static readonly Color OtherColor = new(1f, 0.3f, 1f);

    private static StandardMaterial3D? _lineMaterial;

    private readonly Node3D _world;
    private readonly bool _collisionBuilt;
    private readonly List<Entry> _entries = new();
    private readonly List<MeshInstance3D> _unswitched = new();

    private bool _built, _shown, _debugDone;
    private double _sinceSync;
    private int _lastOn = -1, _lastOff = -1;
    private CanvasLayer? _hudLayer;
    private Label? _hud;
    private string _summary = "";

    public ColliderOverlay(Node3D world, bool collisionBuilt)
    {
        _world = world;
        _collisionBuilt = collisionBuilt;
        Name = "collider_overlay";
    }

    /// <summary><c>--collision=show</c>: open the overlay on the first frame, the scripted stand-in
    /// for the C press.</summary>
    public bool DebugShow { get; init; }

    /// <summary>The flown aircraft and their swept airframe boxes. Those are shape resources the
    /// flight code casts with directly, not scene nodes, so they are passed in rather than found.
    /// <c>Frame</c> is the FlightController, not the plane model — <see cref="PlaneCollider"/>'s
    /// boxes are expressed in the model's PARENT frame, so drawing them under the model itself
    /// would apply its own local transform a second time.</summary>
    public IReadOnlyList<(Node3D Frame, PlaneCollider Collider)> Planes { get; init; } =
        Array.Empty<(Node3D, PlaneCollider)>();

    public override void _Process(double delta)
    {
        if (DebugShow && !_debugDone)
        {
            // Deferred one frame like every other --debug-* opener: the world subtree, the plane
            // and any --destroy kill are only final once the session has been built. Once — a
            // no-collision run would otherwise reprint its notice every frame.
            _debugDone = true;
            Toggle();
        }
        if (!_shown)
        {
            return;
        }
        _sinceSync += delta;
        if (_sinceSync >= SyncInterval)
        {
            _sinceSync = 0.0;
            SyncEnabled(report: true);
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.C })
        {
            return;
        }
        Toggle();
        GetViewport().SetInputAsHandled();
    }

    /// <summary>C: show or hide the wireframes. The first show walks the tree and builds them; every
    /// later one is a visibility flip.</summary>
    public void Toggle()
    {
        if (!_collisionBuilt)
        {
            // The whole reason this control exists: no bodies were built in this mode, which is not
            // the same fact as "nothing here is solid".
            Log.Warn("world", $"collider overlay: this mode builds NO collision at all — there is nothing to draw, which is not the same as 'nothing is collidable'. Relaunch with --collision (or fly the chapter) to see the colliders.");
            ShowNotice("NO COLLISION BUILT IN THIS MODE\n"
                       + "--freecam / --anim-lab build no bodies at all.\n"
                       + "Relaunch with --collision to build and draw them.");
            return;
        }
        if (!_built)
        {
            Build();
        }
        _shown = !_shown;
        foreach (var e in _entries)
        {
            e.Draw.Visible = _shown && !e.Shape.Disabled;
        }
        foreach (var draw in _unswitched)
        {
            draw.Visible = _shown;
        }
        if (_shown)
        {
            SyncEnabled(report: false);
            ShowNotice(_summary);
        }
        else
        {
            HideNotice();
        }
        // Two counts, never their difference: a death switches a healthy collider off and its
        // wreck's on, and the signed sum of that reads as death ADDING collision.
        Log.Info("world", $"collider overlay {(_shown ? "on" : "off")} — {_summary} · switched on {_lastOn} · switched off {_lastOff}");
    }

    private static StandardMaterial3D LineMaterial() => _lineMaterial ??= new StandardMaterial3D
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        DisableFog = true,
        RenderPriority = 15,
    };

    /// <summary>Which owner class this shape belongs to — the overlay's colour key. Water and
    /// building surfaces are already classified by SceneBuilder for the weapon-impact variants, so
    /// the same stamp answers "what am I looking at" here.</summary>
    private static string ClassOf(CollisionShape3D cs)
    {
        var body = cs.GetParent();
        if (body is StaticBody3D sb)
        {
            string name = sb.Name.ToString();
            if (name.StartsWith("clutter_bld", StringComparison.Ordinal))
            {
                return "clutter";
            }
            if (sb.HasMeta(SceneBuilder.SurfaceMeta))
            {
                string surface = sb.GetMeta(SceneBuilder.SurfaceMeta).AsString();
                return surface == "water" ? "water" : surface == "buildings" ? "buildings" : "world";
            }
            return name == "col" ? "world" : "other";
        }
        return "other";
    }

    private static Color ColorFor(string cls) => cls switch
    {
        "water" => WaterColor,
        "buildings" => BuildingColor,
        "clutter" => ClutterColor,
        "plane" => PlaneColor,
        "world" => WorldColor,
        _ => OtherColor,
    };

    // ---- shape emission ------------------------------------------------------------------------

    /// <summary>Draws one shape into the mesh. Returns true when it was drawn as a bounding box
    /// rather than in full — the caller counts those so the panel can say so.</summary>
    /// <summary>Whether this shape has anything to draw. Checked before opening a surface: an
    /// ImmediateMesh surface closed with no vertices in it is an engine error.</summary>
    private static bool HasGeometry(Shape3D shape) =>
        shape is not ConcavePolygonShape3D concave || concave.GetFaces().Length >= 3;

    private static bool EmitShape(ImmediateMesh mesh, Shape3D shape, Transform3D at, Color col, ref int lines)
    {
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        bool boxed = false;
        switch (shape)
        {
            case BoxShape3D box:
                EmitBox(mesh, at, box.Size, col);
                lines += 12;
                break;
            case SphereShape3D sphere:
                EmitSphere(mesh, at, sphere.Radius, col);
                lines += 72;
                break;
            case ConcavePolygonShape3D concave:
                boxed = EmitTriangles(mesh, concave.GetFaces(), at, col, ref lines);
                break;
            default:
                // Anything else (convex hulls, capsules): its own bounds, which is still an honest
                // "something solid is here" rather than nothing.
                EmitBox(mesh, at, Vector3.One, col);
                lines += 12;
                boxed = true;
                break;
        }
        mesh.SurfaceEnd();
        return boxed;
    }

    private static bool EmitTriangles(ImmediateMesh mesh, Vector3[] faces, Transform3D at, Color col,
        ref int lines)
    {
        int tris = faces.Length / 3;
        if (tris == 0)
        {
            return false;
        }
        if (tris > MaxShapeTris || lines > MaxTotalLines)
        {
            var bounds = new Aabb(faces[0], Vector3.Zero);
            foreach (var v in faces)
            {
                bounds = bounds.Expand(v);
            }
            var boxAt = at;
            boxAt.Origin = at * bounds.GetCenter();
            EmitBox(mesh, boxAt, bounds.Size * Inflate, col);
            lines += 12;
            return true;
        }
        // Each triangle's three edges, de-duplicated by quantised endpoint pair: a closed trimesh
        // shares every interior edge between two faces, so drawing them raw doubles the line count
        // for an identical picture.
        var seen = new HashSet<(long, long)>();
        mesh.SurfaceSetColor(col);
        for (int t = 0; t + 2 < faces.Length; t += 3)
        {
            for (int k = 0; k < 3; k++)
            {
                var a = faces[t + k];
                var b = faces[t + (k + 1) % 3];
                if (!seen.Add(EdgeKey(a, b)))
                {
                    continue;
                }
                mesh.SurfaceAddVertex(at * (a * Inflate));
                mesh.SurfaceAddVertex(at * (b * Inflate));
                lines++;
            }
        }
        return false;
    }

    private static (long, long) EdgeKey(Vector3 a, Vector3 b)
    {
        long ka = Quantise(a), kb = Quantise(b);
        return ka <= kb ? (ka, kb) : (kb, ka);
    }

    private static long Quantise(Vector3 v)
    {
        unchecked
        {
            long h = (long)Mathf.Round(v.X * 1000f);
            h = h * 1000003 + (long)Mathf.Round(v.Y * 1000f);
            h = h * 1000003 + (long)Mathf.Round(v.Z * 1000f);
            return h;
        }
    }

    private static void EmitBox(ImmediateMesh mesh, Transform3D xf, Vector3 size, Color col)
    {
        var h = size * 0.5f;
        Span<Vector3> corners = stackalloc Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            corners[i] = xf * new Vector3(
                (i & 1) == 0 ? -h.X : h.X,
                (i & 2) == 0 ? -h.Y : h.Y,
                (i & 4) == 0 ? -h.Z : h.Z);
        }
        ReadOnlySpan<int> pairs = stackalloc int[]
        {
            0, 1, 2, 3, 4, 5, 6, 7,
            0, 2, 1, 3, 4, 6, 5, 7,
            0, 4, 1, 5, 2, 6, 3, 7,
        };
        mesh.SurfaceSetColor(col);
        for (int i = 0; i < pairs.Length; i += 2)
        {
            mesh.SurfaceAddVertex(corners[pairs[i]]);
            mesh.SurfaceAddVertex(corners[pairs[i + 1]]);
        }
    }

    private static void EmitSphere(ImmediateMesh mesh, Transform3D xf, float radius, Color col)
    {
        const int segments = 24;
        mesh.SurfaceSetColor(col);
        for (int axis = 0; axis < 3; axis++)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = Mathf.Tau * i / segments, a1 = Mathf.Tau * (i + 1) / segments;
                mesh.SurfaceAddVertex(xf * OnCircle(axis, a0, radius));
                mesh.SurfaceAddVertex(xf * OnCircle(axis, a1, radius));
            }
        }
    }

    private static Vector3 OnCircle(int axis, float angle, float radius)
    {
        float c = Mathf.Cos(angle) * radius, s = Mathf.Sin(angle) * radius;
        return axis switch
        {
            0 => new Vector3(0f, c, s),
            1 => new Vector3(c, 0f, s),
            _ => new Vector3(c, s, 0f),
        };
    }

    /// <summary>The game-file name of the object a collider belongs to: the nearest ancestor
    /// carrying a <c>cs_name</c>, which is what a destructible is called in the data. A collider's
    /// own node is SceneBuilder's unnamed <c>col</c> body, which names nothing.</summary>
    private static string OwnerName(Node3D shape)
    {
        for (Node? n = shape; n != null; n = n.GetParent())
        {
            if (n is Node3D n3d && n3d.HasMeta(AnimRuntime.NameMeta))
            {
                return SelectionService.NameOf(n3d);
            }
        }
        return shape.Name.ToString();
    }

    private static string Names(List<string> names)
    {
        if (names.Count == 0)
        {
            return "(none)";
        }
        if (names.Count <= MaxNames)
        {
            return $"{names.Count}: {string.Join(", ", names)}";
        }
        return $"{names.Count}: {string.Join(", ", names.GetRange(0, MaxNames))}, … +{names.Count - MaxNames} more";
    }

    // ---- building ------------------------------------------------------------------------------

    private void Build()
    {
        _built = true;
        int lines = 0, boxed = 0, unknown = 0;
        var perClass = new Dictionary<string, int>();

        void Walk(Node n)
        {
            if (n is Node3D marked && marked.HasMeta(SelectionService.OverlayMeta))
            {
                return; // our own drawings
            }
            if (n is CollisionShape3D { Shape: { } shape } cs && HasGeometry(shape))
            {
                string cls = ClassOf(cs);
                var mesh = new ImmediateMesh();
                bool asBox = EmitShape(mesh, shape, Transform3D.Identity, ColorFor(cls), ref lines);
                if (asBox)
                {
                    boxed++;
                }
                var draw = MakeDraw(mesh, cs, $"col_wire_{cls}");
                _entries.Add(new Entry { Shape = cs, Draw = draw, WasEnabled = !cs.Disabled });
                perClass[cls] = perClass.GetValueOrDefault(cls) + 1;
            }
            else if (n is StaticBody3D body && body.GetChildCount() == 0)
            {
                // The clutter region bodies: shapes attached to the body RID, no child nodes.
                int drawn = EmitBodyShapes(body, ref lines, ref boxed);
                if (drawn > 0)
                {
                    perClass["clutter"] = perClass.GetValueOrDefault("clutter") + drawn;
                }
                else if (PhysicsServer3D.BodyGetShapeCount(body.GetRid()) > 0)
                {
                    unknown++;
                }
            }
            foreach (var child in n.GetChildren())
            {
                Walk(child);
            }
        }
        Walk(_world);

        foreach (var (frame, collider) in Planes)
        {
            var mesh = new ImmediateMesh();
            mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
            foreach (var part in collider.Parts)
            {
                EmitBox(mesh, part.Local, part.Shape.Size, PlaneColor);
                lines += 12;
            }
            mesh.SurfaceEnd();
            // The airframe boxes are cast per frame by the flight code rather than being switched
            // on and off, so they follow the overlay itself. Drawn under the FlightController
            // (frame), which is the plane model's parent and the frame Collider.Parts.Local is
            // already expressed in — parenting to the model itself would double its own transform.
            _unswitched.Add(MakeDraw(mesh, frame, "col_wire_plane"));
            perClass["plane"] = perClass.GetValueOrDefault("plane") + collider.Parts.Count;
        }

        var parts = new List<string>();
        foreach (var (cls, count) in perClass)
        {
            parts.Add($"{cls} {count}");
        }
        parts.Sort(StringComparer.Ordinal);
        _summary = $"colliders: {string.Join(" · ", parts)} · {lines} lines"
                   + (boxed > 0 ? $" · {boxed} drawn as bounding boxes (over {MaxShapeTris} tris or past the line budget)" : "");
        if (unknown > 0)
        {
            Log.Warn("world", $"collider overlay: {unknown} body(ies) carry server-side shapes this overlay cannot read back — they are NOT drawn");
        }
        Log.Info("world", $"collider overlay built: {_summary}");
    }

    private MeshInstance3D MakeDraw(ImmediateMesh mesh, Node3D parent, string name)
    {
        var draw = new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = LineMaterial(),
            Visible = false,
        };
        // Marked as a drawing so the shared selection neither picks it nor folds it into the box it
        // measures for the object it is parented onto.
        draw.SetMeta(SelectionService.OverlayMeta, true);
        parent.AddChild(draw);
        return draw;
    }

    /// <summary>Reads a body's shapes back off the physics server — the clutter case, whose shared
    /// shapes have no scene node. ⚠ Server getters only: a <c>ShapeOwner*</c> call here would make
    /// Godot rebuild the body from its (nonexistent) shape nodes and empty it.</summary>
    private int EmitBodyShapes(StaticBody3D body, ref int lines, ref int boxed)
    {
        var rid = body.GetRid();
        int count = PhysicsServer3D.BodyGetShapeCount(rid);
        if (count == 0)
        {
            return 0;
        }
        var placements = new List<(Vector3[] Faces, Transform3D At)>();
        for (int i = 0; i < count; i++)
        {
            var data = PhysicsServer3D.ShapeGetData(PhysicsServer3D.BodyGetShape(rid, i));
            if (data.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }
            var dict = data.AsGodotDictionary();
            if (!dict.TryGetValue("faces", out var faces))
            {
                continue;
            }
            var array = faces.AsVector3Array();
            if (array.Length >= 3)
            {
                placements.Add((array, PhysicsServer3D.BodyGetShapeTransform(rid, i)));
            }
        }
        if (placements.Count == 0)
        {
            return 0;
        }
        var mesh = new ImmediateMesh();
        // ⚠ One surface for the WHOLE body, not one per shape: a city region carries thousands of
        // placements and an ImmediateMesh caps at 256 surfaces (the excess errors per call and
        // draws nothing).
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        foreach (var (faces, xf) in placements)
        {
            if (EmitTriangles(mesh, faces, xf, ClutterColor, ref lines))
            {
                boxed++;
            }
        }
        mesh.SurfaceEnd();
        int drawn = placements.Count;
        // Clutter shapes are never switched off (no destructible owns one), so this drawing has no
        // Disabled flag to follow — it flips with the overlay itself.
        _unswitched.Add(MakeDraw(mesh, body, "col_wire_clutter"));
        return drawn;
    }

    // ---- live state ----------------------------------------------------------------------------

    /// <summary>Re-reads every tracked shape's <c>Disabled</c> flag and matches the drawing to it,
    /// then reports the two tallies whenever they move — the destructible swap, seen from outside.
    /// Reported as separate on and off counts: their signed difference hides the removal.</summary>
    private void SyncEnabled(bool report)
    {
        int on = 0, off = 0;
        var wentOff = new List<string>();
        var cameOn = new List<string>();
        foreach (var e in _entries)
        {
            if (!IsInstanceValid(e.Shape))
            {
                continue;
            }
            bool enabled = !e.Shape.Disabled;
            if (enabled)
            {
                on++;
            }
            else
            {
                off++;
            }
            if (enabled != e.WasEnabled)
            {
                (enabled ? cameOn : wentOff).Add(OwnerName(e.Shape));
                e.WasEnabled = enabled;
            }
            bool want = _shown && enabled;
            if (e.Draw.Visible != want)
            {
                e.Draw.Visible = want;
            }
        }
        if (on == _lastOn && off == _lastOff)
        {
            return;
        }
        if (report && _lastOn >= 0)
        {
            Log.Info("world", $"collider overlay: enabled {_lastOn} -> {on}, disabled {_lastOff} -> {off} (separate counts: a net would hide a removal)");
            Log.Info("world", $"collider overlay: switched OFF {Names(wentOff)}");
            Log.Info("world", $"collider overlay: switched ON  {Names(cameOn)}");
        }
        _lastOn = on;
        _lastOff = off;
        if (_hud != null && _shown)
        {
            _hud.Text = _summary + Log.Format($"\nswitched on {on} · switched off {off}");
        }
    }

    // ---- notice --------------------------------------------------------------------------------

    private void ShowNotice(string text)
    {
        if (_hudLayer == null)
        {
            _hudLayer = new CanvasLayer { Layer = 2 };
            var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _hud = new Label
            {
                // Under the freecam readout and the selection breadcrumb, both top-left.
                Position = new Vector2(12, 120),
                Modulate = new Color(0.6f, 1f, 0.7f),
            };
            _hud.AddThemeFontSizeOverride("font_size", 13);
            root.AddChild(_hud);
            _hudLayer.AddChild(root);
            AddChild(_hudLayer);
        }
        _hud!.Text = text;
        _hudLayer.Visible = true;
    }

    private void HideNotice()
    {
        if (_hudLayer != null)
        {
            _hudLayer.Visible = false;
        }
    }

    /// <summary>One node-backed shape and the wireframe drawn for it. The wireframe's visibility
    /// tracks the shape's own <c>Disabled</c> flag, which is what the destructible swap moves.</summary>
    private sealed class Entry
    {
        public required CollisionShape3D Shape;
        public required MeshInstance3D Draw;
        public required bool WasEnabled;
    }
}
