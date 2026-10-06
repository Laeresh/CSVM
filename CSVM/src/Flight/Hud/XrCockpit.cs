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

    // The always-drawn effects' box (Effects/Precipitation.cs). Kept at the world origin, the
    // interior's own bounds would be culled by any eye kilometres from it.
    private static readonly Aabb Everywhere =
        new(new Vector3(-40000f, -40000f, -40000f), new Vector3(80000f, 80000f, 80000f));

    private readonly Node3D _interior;
    private readonly Transform3D _mount;
    private readonly List<GeometryInstance3D> _geometry = new();
    private bool _lifted;

    /// <summary>Follows <paramref name="interior"/>, as the plane builder mounted it under the
    /// airframe, with the seated eye <paramref name="eyeOffset"/> from the mount's origin in the
    /// aircraft's frame (<see cref="VrCockpit.EyeOffsetInAircraft"/>). The interior must have been
    /// built for the stereo path (<c>xrInterior</c>).</summary>
    public XrCockpit(Node3D interior, Vector3 eyeOffset = default)
    {
        _interior = interior;
        _mount = interior.Transform;
        EyeOffset = eyeOffset;
        Collect(interior, _geometry);
    }

    /// <summary>Whether this process draws through the stereo path, with the eye feed's globals
    /// declared. A plane build reads it to choose the interior's shader.</summary>
    public static bool Live { get; private set; }

    /// <summary>The interior this follows.</summary>
    public Node3D Interior => _interior;

    /// <summary>The seated eye's offset from the interior's own origin, in the aircraft's frame. The
    /// XR origin's placement adds the same offset, so the eye and this point move together.</summary>
    public Vector3 EyeOffset { get; }

    /// <summary>The aircraft-frame point the XR origin stands at: the interior's own origin, mounted
    /// at the <c>cockpit_camera</c> marker where the flat eye is, plus <see cref="EyeOffset"/>.</summary>
    public Vector3 EyeInAircraft => _mount.Origin + EyeOffset;

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

    private static void Collect(Node node, List<GeometryInstance3D> into)
    {
        if (node is GeometryInstance3D geometry)
        {
            into.Add(geometry);
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
        foreach (var geometry in _geometry)
        {
            if (GodotObject.IsInstanceValid(geometry))
            {
                geometry.CustomAabb = Everywhere;
                geometry.SortingOffset = SortAhead;
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
        foreach (var geometry in _geometry)
        {
            if (GodotObject.IsInstanceValid(geometry))
            {
                geometry.CustomAabb = default;
                geometry.SortingOffset = 0f;
            }
        }
    }
}
