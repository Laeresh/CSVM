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

    // The eye feed's globals are declared, which Godot refuses twice. Asking it outside the editor errors.
    private static bool _eyeGlobals;

    private readonly Node3D _interior;
    // Each geometry with the sorting offset it was built with, which lifting adds to.
    private readonly List<(GeometryInstance3D Geometry, float Own)> _geometry = new();
    // Each hung part with its true pose on the airframe, which a placement change re-places it from.
    private readonly List<(Node3D Part, Transform3D OnAirframe)> _hung = new();
    // Each airframe mesh with the twin under the interior that takes its pose and visibility.
    private readonly List<(Node3D Original, Node3D Twin)> _mirrored = new();
    // The authored eye in the interior's own units (PlaneBuilder.InteriorEye).
    private readonly Vector3 _eye;
    // A body cockpit, one metre per metre, which neither the tuning lab's scale nor store fields move.
    private readonly bool _realScale;
    // The airframe's mount, rescaled only by the tuning lab (SetMountScale).
    private Transform3D _mount;
    private Vector3 _storeOffset;
    private float _storeSpread;
    private bool _lifted;

    /// <summary>Follows <paramref name="interior"/>, as the plane builder mounted it under the
    /// airframe, with the seated eye <paramref name="eyeOffset"/> from the authored eye in the
    /// aircraft's frame (<see cref="VrCockpit.EyeOffsetInAircraft"/>). The interior must have been
    /// built for the stereo path (<c>xrInterior</c>), its authored eye at <paramref name="eyeInInterior"/>
    /// in its own units (<see cref="PlaneBuilder.InteriorEye"/>). Hung parts take
    /// <paramref name="storeOffset"/> and <paramref name="storeSpread"/> (<see cref="Placed"/>).</summary>
    public XrCockpit(Node3D interior, Vector3 eyeOffset = default, Vector3 eyeInInterior = default,
        Vector3 storeOffset = default, float storeSpread = 1f)
        : this(interior, eyeOffset, eyeInInterior, storeOffset, storeSpread, realScale: false)
    {
    }

    private XrCockpit(Node3D interior, Vector3 eyeOffset, Vector3 eyeInInterior, Vector3 storeOffset, float storeSpread,
        bool realScale)
    {
        _interior = interior;
        _mount = interior.Transform;
        _eye = eyeInInterior;
        EyeOffset = eyeOffset;
        _storeOffset = storeOffset;
        _storeSpread = storeSpread;
        _realScale = realScale;
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
        DeclareEyeGlobals();
        Live = true;
    }

    /// <summary>Declares the stereo shader's global uniforms, each once, without making the process
    /// stereo (<see cref="Live"/>): what a suite building a stereo interior needs.</summary>
    public static void DeclareEyeGlobals()
    {
        if (_eyeGlobals)
        {
            return;
        }

        _eyeGlobals = true;
        foreach (var name in new[] { SceneBuilder.XrEyeLeftGlobal, SceneBuilder.XrEyeRightGlobal, SceneBuilder.XrOriginGlobal })
        {
            RenderingServer.GlobalShaderParameterAdd(name, RenderingServer.GlobalShaderParameterType.Vec3, Vector3.Zero);
        }
    }

    /// <summary>A body cockpit's stereo interior: <paramref name="panel"/>
    /// (<see cref="PlaneBuilder.CockpitPanel"/>), in the plane frame at real scale, with the eye on
    /// <paramref name="eyeOnPlane"/>, the <c>cockpit_camera</c> marker, plus <paramref name="eyeOffset"/>.
    /// Stores hang at their true pylon poses; <see cref="Mirror"/> takes the airframe's twins.</summary>
    public static XrCockpit Body(Node3D panel, Vector3 eyeOffset, Vector3 eyeOnPlane) =>
        new(panel, eyeOffset, eyeOnPlane, Vector3.Zero, 1f, realScale: true);

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

    /// <summary>A store's pose <paramref name="onAirframe"/> placed for the interior: its distance
    /// from the centreline (X) times <paramref name="spread"/>, then moved by <paramref name="offset"/>,
    /// both in the aircraft's frame. The interior is not the airframe scaled, so each plane's
    /// stores are placed at the headset (<see cref="VrCockpit"/>). Engine-free, so it unit-tests.</summary>
    public static Transform3D Placed(in Transform3D onAirframe, Vector3 offset, float spread) =>
        new(onAirframe.Basis, new Vector3(onAirframe.Origin.X * spread, onAirframe.Origin.Y, onAirframe.Origin.Z) + offset);

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
        foreach (var (original, twin) in _mirrored)
        {
            if (GodotObject.IsInstanceValid(original) && GodotObject.IsInstanceValid(twin))
            {
                twin.Visible = original.IsVisibleInTree();
                if (twin.Visible && OnAirframe(original) is { } onAirframe)
                {
                    twin.Transform = InInterior(_mount, onAirframe);
                }
            }
        }
    }

    /// <summary>Has <paramref name="twin"/>, a stereo-built copy of <paramref name="original"/> that is a
    /// direct child of the interior, take the original's airframe pose and visibility on each
    /// <see cref="Follow"/>. A moving surface, a spinning prop or a torn panel then shows in it.</summary>
    public void Mirror(Node3D original, Node3D twin) => _mirrored.Add((original, twin));

    /// <summary>Rescales the mount about the authored eye to <paramref name="metresPerUnit"/>: the
    /// tuning lab's live scale. The next <see cref="Follow"/> places it. ⚠ The depth bias stays the
    /// one the build baked (SceneBuilder's <c>DepthBiasScale</c>); only a rebuild at the new scale is
    /// exact. A body cockpit is real scale and keeps its mount.</summary>
    public void SetMountScale(float metresPerUnit)
    {
        if (_realScale || !(metresPerUnit > 0f) || Mathf.IsEqualApprox(metresPerUnit, MountScale))
        {
            return;
        }

        _mount = Rescaled(_mount, _eye, metresPerUnit);
        if (!GodotObject.IsInstanceValid(_interior))
        {
            return;
        }

        // A rebuild at this scale hangs the stores on the airframe, so the lab shows them there.
        PlaceHung();

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

    /// <summary>Moves every hung part to <paramref name="offset"/> and <paramref name="spread"/>
    /// (<see cref="Placed"/>): the tuning lab's live store placement.</summary>
    public void PlaceStores(Vector3 offset, float spread)
    {
        // A body cockpit's stores are the real ones' copies, at their true pylon poses.
        if (_realScale)
        {
            return;
        }

        _storeOffset = offset;
        _storeSpread = spread;
        PlaceHung();
    }

    /// <summary>Hangs <paramref name="part"/>, built through the stereo interior's shader, under the
    /// interior at <paramref name="anchor"/>'s pose on the airframe, placed by the plane's store
    /// fields (<see cref="Placed"/>). It then moves with the interior and draws in its frame;
    /// <paramref name="anchor"/> itself never moves. False, and nothing hung, when the anchor is not
    /// under the node the interior is mounted on.</summary>
    public bool Hang(Node3D part, Node3D anchor)
    {
        if (OnAirframe(anchor) is not { } onAirframe)
        {
            return false;
        }

        part.Transform = InInterior(_mount, Placed(onAirframe, _storeOffset, _storeSpread));
        _hung.Add((part, onAirframe));
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

    // A node's pose on the node the interior is mounted on, from local transforms alone. Null when
    // the walk never meets it.
    private Transform3D? OnAirframe(Node3D node)
    {
        var onAirframe = Transform3D.Identity;
        for (Node? step = node; step != _interior.GetParent(); step = step.GetParent())
        {
            if (step is not Node3D at)
            {
                return null;
            }

            onAirframe = at.Transform * onAirframe;
        }

        return onAirframe;
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

    // A part freed since it was hung (PylonOrdnance.Unmount) leaves the list.
    private void PlaceHung()
    {
        _hung.RemoveAll(h => !GodotObject.IsInstanceValid(h.Part));
        foreach (var (part, onAirframe) in _hung)
        {
            part.Transform = InInterior(_mount, Placed(onAirframe, _storeOffset, _storeSpread));
        }
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
