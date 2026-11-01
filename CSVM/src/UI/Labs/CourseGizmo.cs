using System;
using System.Collections.Generic;
using CSVM.Flight.Modes;
using CSVM.UI.Screens;
using Godot;

namespace CSVM.UI.Labs;

/// <summary>
/// The course lab's move and turn tool at the selected gate's opening: world X, Y, Z arrows (red, green, blue)
/// and a magenta heading ring. It keeps one size on screen, draws unshaded through the ground, and lights the
/// handle under the cursor. A press within a few pixels of a handle grabs it. An arrow moves the gate to the
/// axis point nearest the cursor's ray; the ring turns it by the angle swept round the middle. A pylon gate has
/// no Y arrow and stands on the ground as it moves. Every drag goes through <see cref="CourseDraft.Update"/>.
/// </summary>
public sealed partial class CourseGizmo : Node3D
{
    /// <summary>Degrees a snapped turn rounds the heading to.</summary>
    public const float SnapDeg = 15f;

    // On screen, TUNE at the controls: an arrow's length, and how far off a handle a press still grabs it.
    private const float ArrowPx = 110f;
    private const float PickPx = 10f;

    // In arrow lengths: the ring's radius, the arrow part nearest the middle that grabs nothing, and the
    // shaft, head and ring thickness drawn.
    private const float RingShare = 0.8f;
    private const float PickFrom = 0.2f;
    private const float ShaftRadius = 0.02f;
    private const float HeadRadius = 0.05f;
    private const float HeadLength = 0.18f;
    private const float RingHalfWidth = 0.01f;
    private const int RingPoints = 48;

    private static readonly Vector3[] Axes = { Vector3.Right, Vector3.Up, Vector3.Back };
    private static readonly Color[] Colours =
    {
        new(0.95f, 0.25f, 0.25f), new(0.35f, 0.9f, 0.35f), new(0.3f, 0.5f, 1f), new(1f, 0.45f, 0.95f),
    };

    private static readonly Color Hot = new(1f, 1f, 0.7f);

    private readonly CourseDraft _draft;
    private readonly Func<Vector3, float?> _groundBelow;
    private readonly Node3D[] _handles = new Node3D[4];
    private readonly StandardMaterial3D[] _materials = new StandardMaterial3D[4];
    private CourseGate? _grabGate;
    private Vector3 _grabOrigin;
    private float _grabAt;

    /// <summary>A gizmo on <paramref name="draft"/>'s selected gate. <paramref name="groundBelow"/> is the
    /// ground's height under a point, which a moved pylon gate stands on.</summary>
    public CourseGizmo(CourseDraft draft, Func<Vector3, float?> groundBelow)
    {
        _draft = draft;
        _groundBelow = groundBelow;
        Name = "course_gizmo";
        Visible = false;
        SetMeta(SelectionService.OverlayMeta, true);
        for (int h = 0; h < 4; h++)
        {
            _materials[h] = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = Colours[h],
                NoDepthTest = true,
                DisableFog = true,
                RenderPriority = 30,
            };
            _handles[h] = new Node3D { Name = ((Handle)h).ToString() };
            AddChild(_handles[h]);
        }

        for (int h = 0; h < 3; h++)
        {
            _handles[h].Basis = h == 1 ? Basis.Identity : new Basis(new Quaternion(Vector3.Up, Axes[h]));
            Part(h, new CylinderMesh { TopRadius = ShaftRadius, BottomRadius = ShaftRadius, Height = 1f - HeadLength }, 0.5f * (1f - HeadLength));
            Part(h, new CylinderMesh { TopRadius = 0f, BottomRadius = HeadRadius, Height = HeadLength }, 1f - (0.5f * HeadLength));
        }

        Part(3, new TorusMesh { InnerRadius = RingShare - RingHalfWidth, OuterRadius = RingShare + RingHalfWidth, Rings = 64 }, 0f);
    }

    /// <summary>The gizmo's handles: an arrow per world axis, then the heading ring.</summary>
    public enum Handle
    {
        None = -1,

        X,

        Y,

        Z,

        Yaw,
    }

    /// <summary>Shown while the lab is open; the lab sets it.</summary>
    public bool Active { get; set; }

    /// <summary>The handle under the cursor while nothing is grabbed.</summary>
    public Handle Hovered { get; private set; } = Handle.None;

    /// <summary>The handle a press holds until release.</summary>
    public Handle Grabbed { get; private set; } = Handle.None;

    /// <summary>An arrow's length in metres at the last fit.</summary>
    public float Size { get; private set; }

    /// <summary>Metres an arrow <paramref name="px"/> pixels long spans at <paramref name="depth"/> metres along the
    /// view of a camera with vertical field <paramref name="fovDeg"/> over <paramref name="viewportPx"/> rows.</summary>
    public static float ScreenScale(float depth, float fovDeg, float viewportPx, float px) =>
        depth * 2f * Mathf.Tan(Mathf.DegToRad(fovDeg) * 0.5f) * px / viewportPx;

    /// <summary>How far along the axis line through <paramref name="origin"/> the point nearest the ray lies, in
    /// units of <paramref name="axis"/>. Null when the ray runs within about half a degree of the axis.</summary>
    public static float? AlongAxis(Vector3 origin, Vector3 axis, Vector3 rayFrom, Vector3 rayDir)
    {
        var w = origin - rayFrom;
        float a = axis.Dot(axis), b = axis.Dot(rayDir), c = rayDir.Dot(rayDir);
        float denom = (a * c) - (b * b);
        return denom < 1e-4f * a * c ? null : ((b * rayDir.Dot(w)) - (c * axis.Dot(w))) / denom;
    }

    /// <summary>The heading, in <see cref="SpawnPoint"/> degrees, from <paramref name="centre"/> to where the ray
    /// meets its horizontal plane. Null when the ray misses the plane or meets it at the centre.</summary>
    public static float? AngleAround(Vector3 centre, Vector3 rayFrom, Vector3 rayDir)
    {
        float t = Mathf.Abs(rayDir.Y) < 1e-4f ? -1f : (centre.Y - rayFrom.Y) / rayDir.Y;
        var off = rayFrom + (rayDir * t) - centre;
        return t <= 0f || off.LengthSquared() < 1e-6f ? null : Mathf.RadToDeg(Mathf.Atan2(-off.X, -off.Z));
    }

    /// <summary><paramref name="deg"/> in [-180, 180).</summary>
    public static float WrapDeg(float deg) => Mathf.PosMod(deg + 180f, 360f) - 180f;

    /// <summary><paramref name="heading"/> turned by the sweep from <paramref name="fromDeg"/> to <paramref name="toDeg"/>
    /// the short way round, wrapped, and rounded to <see cref="SnapDeg"/> when <paramref name="snap"/>.</summary>
    public static float Turned(float heading, float fromDeg, float toDeg, bool snap)
    {
        float turned = heading + WrapDeg(toDeg - fromDeg);
        return WrapDeg(snap ? Mathf.Round(turned / SnapDeg) * SnapDeg : turned);
    }

    /// <summary>The handle whose screen polyline passes nearest <paramref name="mouse"/> within
    /// <paramref name="tolerancePx"/>, the lower handle on a tie; <see cref="Handle.None"/> past it. An empty
    /// polyline is a hidden handle.</summary>
    public static Handle Pick(Vector2 mouse, IReadOnlyList<Vector2[]> polylines, float tolerancePx)
    {
        var best = Handle.None;
        float bestPx = float.MaxValue;
        for (int h = 0; h < polylines.Count; h++)
        {
            var line = polylines[h];
            for (int i = 0; i + 1 < line.Length; i++)
            {
                var ab = line[i + 1] - line[i];
                float t = ab.LengthSquared() < 1e-9f ? 0f : Mathf.Clamp((mouse - line[i]).Dot(ab) / ab.LengthSquared(), 0f, 1f);
                float px = mouse.DistanceTo(line[i] + (ab * t));
                if (px <= tolerancePx && px < bestPx)
                {
                    bestPx = px;
                    best = (Handle)h;
                }
            }
        }

        return best;
    }

    public override void _Process(double delta)
    {
        if (GetViewport().GetCamera3D() is { } camera)
        {
            Fit(camera);
        }
    }

    /// <summary>Stands the gizmo on the selected gate at one screen size for <paramref name="camera"/>. Returns
    /// false, hidden, while inactive, with nothing selected, or with the gate behind the camera.</summary>
    public bool Fit(Camera3D camera)
    {
        var gate = _draft.Selected;
        var origin = gate == null ? default : CourseDraft.Centre(gate);
        float depth = (origin - camera.GlobalPosition).Dot(-camera.GlobalBasis.Z);
        Visible = Active && gate != null && depth > camera.Near;
        if (!Visible)
        {
            return false;
        }

        Size = ScreenScale(depth, camera.Fov, camera.GetViewport().GetVisibleRect().Size.Y, ArrowPx);
        Transform = new Transform3D(Basis.Identity.Scaled(Vector3.One * Size), origin);
        _handles[(int)Handle.Y].Visible = gate!.Kind == CourseGateKind.BalloonRing;
        return true;
    }

    /// <summary>The world point a press grabs <paramref name="handle"/> at, as last fitted. It is most of the way
    /// along an arrow, or on the ring between the arrows' far ends.</summary>
    public Vector3 Grip(Handle handle) => handle == Handle.Yaw
        ? Position + (new Vector3(Mathf.Sqrt(0.5f), 0f, -Mathf.Sqrt(0.5f)) * (RingShare * Size))
        : Position + (Axes[(int)handle] * (0.7f * Size));

    /// <summary>The handle under <paramref name="screen"/>, or none with the gizmo hidden.</summary>
    public Handle HandleAt(Camera3D camera, Vector2 screen)
    {
        if (!Fit(camera))
        {
            return Handle.None;
        }

        var lines = new Vector2[4][];
        for (int h = 0; h < 3; h++)
        {
            lines[h] = _handles[h].Visible
                ? new[] { camera.UnprojectPosition(Position + (Axes[h] * (PickFrom * Size))), camera.UnprojectPosition(Position + (Axes[h] * Size)) }
                : Array.Empty<Vector2>();
        }

        lines[3] = new Vector2[RingPoints + 1];
        for (int i = 0; i <= RingPoints; i++)
        {
            float a = Mathf.Tau * i / RingPoints;
            lines[3][i] = camera.UnprojectPosition(Position + (new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (RingShare * Size)));
        }

        return Pick(screen, lines, PickPx);
    }

    /// <summary>Grabs the handle under <paramref name="screen"/>. Returns true, claiming the click, on a handle.</summary>
    public bool Press(Camera3D camera, Vector2 screen)
    {
        var handle = HandleAt(camera, screen);
        if (handle == Handle.None)
        {
            return false;
        }

        _grabGate = _draft.Selected;
        _grabOrigin = Position;
        var from = camera.ProjectRayOrigin(screen);
        var dir = camera.ProjectRayNormal(screen);
        float? at = handle == Handle.Yaw ? AngleAround(_grabOrigin, from, dir) : AlongAxis(_grabOrigin, Axes[(int)handle], from, dir);
        Grabbed = at == null ? Handle.None : handle;
        _grabAt = at ?? 0f;
        Recolour();
        return true;
    }

    /// <summary>Moves or turns the grabbed gate after the cursor at <paramref name="screen"/>. Returns false with
    /// nothing grabbed or where the cursor's ray gives no answer.</summary>
    public bool DragTo(Camera3D camera, Vector2 screen, bool snap)
    {
        if (Grabbed == Handle.None || _grabGate is not { } gate)
        {
            return false;
        }

        var from = camera.ProjectRayOrigin(screen);
        var dir = camera.ProjectRayNormal(screen);
        if (Grabbed == Handle.Yaw)
        {
            return AngleAround(_grabOrigin, from, dir) is { } angle && _draft.Update(gate with { Heading = Turned(gate.Heading, _grabAt, angle, snap) });
        }

        if (AlongAxis(_grabOrigin, Axes[(int)Grabbed], from, dir) is not { } along)
        {
            return false;
        }

        var to = gate.Position + (Axes[(int)Grabbed] * (along - _grabAt));
        if (gate.Kind == CourseGateKind.PylonGate)
        {
            to.Y = _groundBelow(to) ?? gate.Position.Y;
        }

        return _draft.Update(gate with { Position = to });
    }

    /// <summary>Lets go of the grabbed handle. Returns whether one was held.</summary>
    public bool Release()
    {
        bool held = Grabbed != Handle.None;
        Grabbed = Handle.None;
        _grabGate = null;
        Recolour();
        return held;
    }

    /// <summary>Lights the handle under <paramref name="screen"/>; nothing changes while one is grabbed.</summary>
    public void Hover(Camera3D camera, Vector2 screen)
    {
        if (Grabbed == Handle.None)
        {
            Hovered = HandleAt(camera, screen);
            Recolour();
        }
    }

    private void Part(int handle, PrimitiveMesh mesh, float up)
    {
        mesh.Material = _materials[handle];
        _handles[handle].AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            Position = Vector3.Up * up,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    private void Recolour()
    {
        var lit = Grabbed != Handle.None ? Grabbed : Hovered;
        for (int h = 0; h < 4; h++)
        {
            _materials[h].AlbedoColor = (Handle)h == lit ? Hot : Colours[h];
        }
    }
}
