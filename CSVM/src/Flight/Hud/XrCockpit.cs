using System.Collections.Generic;
using CSVM.Flight.Camera;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight.Hud;

/// <summary>
/// The cockpit interior in the main world under the stereo path, drawn the flat cockpit pass's way.
/// It sits at zero translation, so every transform Godot composes for it is small. While it shows,
/// it is top level at the world origin, in the XR origin's axes, offset as the airframe holds it.
/// Its shader, <c>Mech3/SceneBuilder.cs</c>'s stereo interior variant, places each vertex from its
/// offset to the drawing eye, which <c>Launch/XrEyeFeed.cs</c> writes from the render thread.
/// Hidden, it goes back under the airframe, so nothing reading its world position finds it at the
/// origin. <c>Flight/Camera/XrRig.cs</c> calls <see cref="Follow"/> every frame.
/// </summary>
public sealed class XrCockpit : IXrInterior
{
    // Sorts the interior's blended surfaces after every world one. At the origin its own depth
    // would sort the glass among world objects as far away as the chapter origin.
    private const float SortAhead = 100000f;

    // The always-drawn effects' reach (Effects/Precipitation.cs), in world metres. Kept at the world
    // origin, the interior's own bounds would be culled by any eye kilometres from it.
    // ⚠ Divide it by the mount's scale. A custom AABB is in the geometry's own frame, so at 0.04 an
    // unscaled box reaches 1.6 km. An eye past that, facing away from the origin, culls the interior.
    private const float EverywhereM = 40000f;

    private readonly Node3D _interior;
    // Each geometry with the sorting offset it was built with, which lifting adds to.
    private readonly List<(GeometryInstance3D Geometry, float Own)> _geometry = new();
    // The authored eye in the interior's own units (PlaneBuilder.InteriorEye).
    private readonly Vector3 _eye;
    // The airframe's mount, rescaled only by the tuning lab (SetMountScale).
    private Transform3D _mount;
    private bool _lifted;

    /// <summary>Follows <paramref name="interior"/>, as the plane builder mounted it under the
    /// airframe, with the seated eye <paramref name="eyeOffset"/> from the authored eye in the
    /// aircraft's frame (<see cref="VrCockpit.EyeOffsetInAircraft"/>). The interior must have been
    /// built for the stereo path (<c>xrInterior</c>), its authored eye at <paramref name="eyeInInterior"/>
    /// in its own units (<see cref="PlaneBuilder.InteriorEye"/>).</summary>
    public XrCockpit(Node3D interior, Vector3 eyeOffset = default, Vector3 eyeInInterior = default)
    {
        _interior = interior;
        _mount = interior.Transform;
        _eye = eyeInInterior;
        EyeOffset = eyeOffset;
        Collect(interior, _geometry);
    }

    /// <summary>Whether this process draws through the stereo path, with the eye feed's globals
    /// declared. A plane build reads it to choose the interior's shader.</summary>
    public static bool Live { get; private set; }

    /// <summary>The interior this follows.</summary>
    public Node3D Interior => _interior;

    /// <summary>The seated eye's offset from the interior's authored eye, in the aircraft's frame. The
    /// XR origin's placement adds the same offset, so the eye and this point move together. The
    /// tuning lab (<see cref="XrCockpitTuner"/>) sets it live; the next placement takes it.</summary>
    public Vector3 EyeOffset { get; set; }

    /// <summary>The aircraft-frame point the XR origin stands at: the interior's authored eye, mounted
    /// on the <c>cockpit_camera</c> marker where the flat eye is, plus <see cref="EyeOffset"/>.</summary>
    public Vector3 EyeInAircraft => (_mount * _eye) + EyeOffset;

    /// <summary>The mount's scale in metres per interior unit, as built or as last set.</summary>
    public float MountScale => _mount.Basis.Scale.X;

    /// <summary>Declares the global uniforms the stereo interior's shader reads. Once per process,
    /// before any plane is built, only when the stereo path is live.</summary>
    public static void DeclareEyeFeed()
    {
        foreach (var name in new[] { SceneBuilder.XrEyeLeftGlobal, SceneBuilder.XrEyeRightGlobal, SceneBuilder.XrOriginGlobal })
        {
            RenderingServer.GlobalShaderParameterAdd(name, RenderingServer.GlobalShaderParameterType.Vec3, Vector3.Zero);
        }

        Live = true;
    }

    /// <summary>The interior's pose relative to the XR origin, in the origin's axes. The origin
    /// stands at <paramref name="eyeInAircraft"/> with the aircraft's attitude, and the interior at
    /// <paramref name="interiorInAircraft"/>, wobble included. Both are small, so this is exact
    /// wherever the aircraft flies. Engine-free, so it unit-tests.</summary>
    public static Transform3D InOrigin(in Transform3D interiorInAircraft, Vector3 eyeInAircraft) =>
        new(interiorInAircraft.Basis, interiorInAircraft.Origin - eyeInAircraft);

    /// <summary>The custom AABB each lifted interior geometry carries, in its own frame. The interior
    /// is mounted at <paramref name="mountScale"/>, and the box still reaches 40 km once that scale
    /// shrinks it. Engine-free, so it unit-tests.</summary>
    public static Aabb Everywhere(Vector3 mountScale)
    {
        float reach = EverywhereM / Mathf.Min(mountScale.X, Mathf.Min(mountScale.Y, mountScale.Z));
        return new Aabb(-reach * Vector3.One, 2f * reach * Vector3.One);
    }

    /// <summary>The pose, in the interior's own frame, of a part at <paramref name="onAirframe"/> on
    /// the node the interior is mounted on with <paramref name="mount"/>. Mounted as built, the part
    /// lands exactly where the airframe holds the anchor. Engine-free, so it unit-tests.</summary>
    public static Transform3D InInterior(in Transform3D mount, in Transform3D onAirframe) =>
        mount.AffineInverse() * onAirframe;

    /// <summary><paramref name="mount"/> rescaled to <paramref name="metresPerUnit"/> about the
    /// interior's point <paramref name="eyeInInterior"/>, which keeps its place on the airframe.
    /// Engine-free, so it unit-tests.</summary>
    public static Transform3D Rescaled(in Transform3D mount, Vector3 eyeInInterior, float metresPerUnit)
    {
        var basis = mount.Basis.Orthonormalized().Scaled(Vector3.One * metresPerUnit);
        return new Transform3D(basis, (mount * eyeInInterior) - (basis * eyeInInterior));
    }

    /// <summary>Places the interior for this frame under the XR origin at <paramref name="origin"/>,
    /// which must stand at <see cref="EyeInAircraft"/> on <paramref name="aircraft"/> with its
    /// attitude. A hidden interior, or one no longer under that aircraft, is put back in place.</summary>
    public void Follow(in Transform3D origin, Node3D aircraft)
    {
        if (!GodotObject.IsInstanceValid(_interior))
        {
            return;
        }

        if (!_interior.IsVisibleInTree() || InAircraft(aircraft) is not { } held)
        {
            Lower();
            return;
        }

        Lift();
        _interior.Transform = new Transform3D(origin.Basis, Vector3.Zero) * InOrigin(held, EyeInAircraft);
    }

    /// <summary>Rescales the mount about the authored eye to <paramref name="metresPerUnit"/>: the
    /// tuning lab's live scale. The next <see cref="Follow"/> places it. ⚠ The depth bias stays the
    /// one the build baked (SceneBuilder's <c>DepthBiasScale</c>); only a rebuild at the new scale is
    /// exact.</summary>
    public void SetMountScale(float metresPerUnit)
    {
        if (!(metresPerUnit > 0f) || Mathf.IsEqualApprox(metresPerUnit, MountScale))
        {
            return;
        }

        _mount = Rescaled(_mount, _eye, metresPerUnit);
        if (!GodotObject.IsInstanceValid(_interior))
        {
            return;
        }

        if (_lifted)
        {
            var everywhere = Everywhere(_mount.Basis.Scale);
            foreach (var (geometry, _) in _geometry)
            {
                if (GodotObject.IsInstanceValid(geometry))
                {
                    geometry.CustomAabb = everywhere;
                }
            }
        }
        else
        {
            _interior.Transform = _mount;
        }
    }

    /// <summary>Hangs <paramref name="part"/>, built through the stereo interior's shader, under the
    /// interior at <paramref name="anchor"/>'s pose on the airframe. It then moves with the interior,
    /// rescales with its mount and draws in its frame; <paramref name="anchor"/> itself never moves.
    /// False, and nothing hung, when the anchor is not under the node the interior is mounted on.</summary>
    public bool Hang(Node3D part, Node3D anchor)
    {
        var onAirframe = Transform3D.Identity;
        for (Node? node = anchor; node != _interior.GetParent(); node = node.GetParent())
        {
            if (node is not Node3D step)
            {
                return false;
            }

            onAirframe = step.Transform * onAirframe;
        }

        part.Transform = InInterior(_mount, onAirframe);
        _interior.AddChild(part);
        int from = _geometry.Count;
        Collect(part, _geometry);
        if (_lifted)
        {
            var everywhere = Everywhere(_mount.Basis.Scale);
            for (int i = from; i < _geometry.Count; i++)
            {
                _geometry[i].Geometry.CustomAabb = everywhere;
                _geometry[i].Geometry.SortingOffset = SortAhead + _geometry[i].Own;
            }
        }

        return true;
    }

    private static void Collect(Node node, List<(GeometryInstance3D, float)> into)
    {
        if (node is GeometryInstance3D geometry)
        {
            into.Add((geometry, geometry.SortingOffset));
        }

        foreach (var child in node.GetChildren())
        {
            Collect(child, into);
        }
    }

    // The interior's mount composed up to the aircraft from local transforms alone: the wobble
    // pivot and the model root. Null when the walk never meets the aircraft.
    private Transform3D? InAircraft(Node3D aircraft)
    {
        var held = _mount;
        for (var node = _interior.GetParent(); node != aircraft; node = node.GetParent())
        {
            if (node is not Node3D step)
            {
                return null;
            }

            held = step.Transform * held;
        }

        return held;
    }

    private void Lift()
    {
        if (_lifted)
        {
            return;
        }

        _lifted = true;
        _interior.TopLevel = true;
        var everywhere = Everywhere(_mount.Basis.Scale);
        foreach (var (geometry, own) in _geometry)
        {
            if (GodotObject.IsInstanceValid(geometry))
            {
                geometry.CustomAabb = everywhere;
                geometry.SortingOffset = SortAhead + own;
            }
        }
    }

    private void Lower()
    {
        if (!_lifted)
        {
            return;
        }

        _lifted = false;
        _interior.TopLevel = false;
        _interior.Transform = _mount;
        foreach (var (geometry, own) in _geometry)
        {
            if (GodotObject.IsInstanceValid(geometry))
            {
                geometry.CustomAabb = default;
                geometry.SortingOffset = own;
            }
        }
    }
}
