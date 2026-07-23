using Godot;

namespace CSVM.UI;

/// <summary>
/// The static inspection view's orbit-camera controller: LMB-drag orbit, mouse-wheel zoom, and
/// AABB-based framing of a subject. Extracted verbatim from <see cref="CSVM.PlaneViewer"/>
/// (2026-07-23, PLAN-anim-debugger Wave 1 A1) so the <c>--anim-lab</c> mode can drive the same
/// orbit camera without duplicating it — the first slice of the eventual PlaneViewer split.
///
/// <para>Owns the orbit state (center / distance / yaw / pitch / drag) and steers a
/// <see cref="Camera3D"/> it does not own. The host keeps ownership of <c>--campos</c>/<c>--lookat</c>
/// (fed into <see cref="Frame"/>) and F11/F12 (which read <see cref="OrbitCenter"/> back out), so
/// the pose/screenshot behaviour is unchanged — this is a pure refactor.</para>
/// </summary>
public sealed class OrbitCamera
{
    private readonly Camera3D _camera;
    private Vector3 _orbitCenter;
    private float _orbitDistance = 20f;
    private float _yaw = 2.5f, _pitch = 0.3f; // default: front-left three-quarter view (nose is -Z)
    private bool _dragging;

    public OrbitCamera(Camera3D camera)
    {
        _camera = camera;
    }

    /// <summary>The point the camera orbits and aims at — the framed pivot. F11 prints it as the
    /// orbit-mode <c>--lookat</c>, and <c>--shots</c> jitter micro-orbits about it.</summary>
    public Vector3 OrbitCenter => _orbitCenter;

    /// <summary>Initial view angles (radians) from <c>--yaw=</c>/<c>--pitch=</c>. Set before the
    /// first <see cref="Frame"/>; a framing with no <c>--campos</c> keeps them, one with
    /// <c>--campos</c> reconstructs them from the eye position.</summary>
    public float Yaw { get => _yaw; set => _yaw = value; }
    public float Pitch { get => _pitch; set => _pitch = value; }

    /// <summary>Frame the subject (its merged mesh <paramref name="aabb"/>). <paramref name="lookAt"/>
    /// (<c>--lookat</c>) overrides the pivot; <paramref name="camPos"/> (<c>--campos</c>), when set,
    /// places the eye there and reconstructs pitch/yaw from it, otherwise the distance is derived
    /// from the AABB radius and the camera FOV.</summary>
    public void Frame(Aabb aabb, Vector3? camPos, Vector3? lookAt)
    {
        _orbitCenter = lookAt ?? aabb.GetCenter();
        if (camPos is { } pos)
        {
            var offset = pos - _orbitCenter;
            _orbitDistance = offset.Length();
            if (_orbitDistance < 0.01f) { _orbitDistance = 1f; offset = Vector3.Back; }
            var dir = offset / _orbitDistance;
            _pitch = Mathf.Asin(Mathf.Clamp(dir.Y, -1f, 1f));
            _yaw = Mathf.Atan2(dir.X, dir.Z);
        }
        else
        {
            var radius = aabb.Size.Length() * 0.5f;
            if (radius < 0.01f) radius = 5f;
            _orbitDistance = radius / Mathf.Sin(Mathf.DegToRad(_camera.Fov) * 0.5f) * 0.8f;
        }
        Update();
    }

    /// <summary>Recompute the camera transform from the current orbit state.</summary>
    public void Update()
    {
        _pitch = Mathf.Clamp(_pitch, -1.5f, 1.5f);
        var dir = new Vector3(
            Mathf.Sin(_yaw) * Mathf.Cos(_pitch),
            Mathf.Sin(_pitch),
            Mathf.Cos(_yaw) * Mathf.Cos(_pitch));
        _camera.Position = _orbitCenter + dir * _orbitDistance;
        _camera.LookAt(_orbitCenter, Vector3.Up);
    }

    /// <summary>Merges every mesh AABB under <paramref name="root"/> into one world-space box —
    /// the subject box <see cref="Frame"/> takes. A subtree with no meshes returns a zero-size
    /// box at the origin, which callers must special-case (the anim lab substitutes a nominal
    /// box around the node's own position). Moved verbatim from PlaneViewer's ComputeAabb
    /// (PLAN-anim-debugger Wave 3) so the lab frames arbitrary world subtrees through the same
    /// code; the nodes must be in the scene tree (GlobalTransform on a detached node is
    /// identity, and Godot logs an error per call).</summary>
    public static Aabb MergedAabb(Node3D root)
    {
        Aabb merged = default;
        bool first = true;
        void Walk(Node node)
        {
            if (node is MeshInstance3D mi && mi.Mesh != null)
            {
                var box = mi.GlobalTransform * mi.Mesh.GetAabb();
                merged = first ? box : merged.Merge(box);
                first = false;
            }
            foreach (var child in node.GetChildren())
            {
                Walk(child);
            }
        }
        Walk(root);
        return merged;
    }

    /// <summary>Feed one input event: LMB toggles orbit-drag, the wheel zooms, and drag motion
    /// spins yaw/pitch. The host filters out the modes that own the camera (flight / freecam) and
    /// the global keys (Esc / F11 / F12) before delegating here.</summary>
    public void HandleInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                _dragging = mb.Pressed;
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }:
                _orbitDistance *= 0.9f;
                Update();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
                _orbitDistance *= 1.1f;
                Update();
                break;
            case InputEventMouseMotion motion when _dragging:
                _yaw -= motion.Relative.X * 0.008f;
                _pitch += motion.Relative.Y * 0.008f;
                Update();
                break;
        }
    }
}
