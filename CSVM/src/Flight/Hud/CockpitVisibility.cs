using System;
using CSVM.Flight.Camera;
using CSVM.Mech3;
using CSVM.UI.Boards;
using Godot;

namespace CSVM.Flight.Hud;

/// <summary>
/// The per-mode hiding the original applies to the pilot's OWN aircraft in a first-person view
/// (docs/org/cameraViews.md, "Mode 6 = Cockpit" / "Mode 7 = Nose"). Cockpit draws the
/// <c>cockpit1</c> interior and hides the <c>healthy</c> body. Nose draws no interior and hides
/// the body, <c>markers</c> and <c>dontmove</c>. Every other view and aircraft is untouched.
/// The pure decision is <see cref="Rules"/>, and <see cref="Apply"/> writes it onto the four nodes
/// <see cref="Bind"/> found in one built plane model, for that pilot's own pane alone.
/// </summary>
public sealed class CockpitVisibility
{
    private readonly Node3D? _interior, _body, _markers, _dontmove;

    // The pilot's airframe stamp and the layer a hidden group trades it for.
    private readonly uint _own, _out;

    // Which airframe groups the last Apply moved out of the pilot's own view, so a frame that
    // changes nothing walks no subtree.
    private (bool Body, bool Markers, bool Dontmove) _hidden;

    private CockpitVisibility(Node3D? interior, Node3D? body, Node3D? markers, Node3D? dontmove,
        int pilot)
    {
        _interior = interior;
        _body = body;
        _markers = markers;
        _dontmove = dontmove;
        _own = SplitScreen.OwnAirframeLayer(pilot);
        _out = SplitScreen.FirstPersonLayer(pilot);
    }

    /// <summary>The decoded rule. <paramref name="firstPerson"/> is whether THIS FRAME's camera
    /// pose is a first-person one, not merely which view is selected: a held numpad key or the
    /// look-behind puts the camera outside the aircraft, and the original's gate is the live camera
    /// mode. So a held key restores the body for as long as it is down, the same way
    /// <see cref="CameraController.RestoreExternalFov"/> restores the external FOV.</summary>
    public static Shown Rules(PilotViewMode mode, bool firstPerson) => !firstPerson
        ? new Shown(Interior: false, Body: true, Markers: true, Dontmove: true)
        : mode == PilotViewMode.Nose
            ? new Shown(Interior: false, Body: false, Markers: false, Dontmove: false)
            : new Shown(Interior: true, Body: false, Markers: true, Dontmove: true);

    /// <summary>Finds the four groups in one built plane model. Null for a model with no interior,
    /// which is any build that did not ask <see cref="PlaneBuilder"/> for one.
    /// <paramref name="pilot"/> is the seat whose <see cref="SplitScreen.OwnAirframeLayer"/> the
    /// model wears.</summary>
    public static CockpitVisibility? Bind(Node3D? planeModel, Node3D? interior, int pilot)
    {
        if (planeModel == null || interior == null)
        {
            return null;
        }
        return new CockpitVisibility(interior, FindGroup(planeModel, "healthy"),
            FindGroup(planeModel, "markers"), FindGroup(planeModel, "dontmove"), pilot);
    }

    /// <summary>Write this frame's rule, every frame the rig owns its camera. The interior takes
    /// <c>Visible</c>. A hidden airframe group moves off <see cref="SplitScreen.OwnAirframeLayer"/>
    /// onto <see cref="SplitScreen.FirstPersonLayer"/>, which only this pilot's own pane drops.
    /// ⚠ Never hide those groups through <c>Visible</c>. Every pane shares one scene tree, so a
    /// hidden node is gone from every pilot's view.</summary>
    public void Apply(PilotViewMode mode, bool firstPerson)
    {
        var shown = Rules(mode, firstPerson);
        Set(_interior, shown.Interior);
        var hidden = (Body: !shown.Body, Markers: !shown.Markers, Dontmove: !shown.Dontmove);
        if (hidden == _hidden)
        {
            return;
        }

        // Everything back first, then each hidden group out, so a group nested in another hidden
        // one stays out of view whichever of the two changed.
        _hidden = hidden;
        Move(_body, _out, _own);
        Move(_markers, _out, _own);
        Move(_dontmove, _out, _own);
        if (hidden.Body)
            Move(_body, _own, _out);
        if (hidden.Markers)
            Move(_markers, _own, _out);
        if (hidden.Dontmove)
            Move(_dontmove, _own, _out);
    }

    private static void Set(Node3D? node, bool visible)
    {
        if (node != null && node.Visible != visible)
        {
            node.Visible = visible;
        }
    }

    // Trades one layer bit for another on every drawable in the subtree that carries it. Only the
    // airframe stamp's own bit moves, so a layer anything else gave a mesh is left as it was.
    private static void Move(Node? node, uint from, uint to)
    {
        if (node == null || !GodotObject.IsInstanceValid(node))
        {
            return;
        }
        if (node is VisualInstance3D instance && (instance.Layers & from) != 0)
        {
            instance.Layers = (instance.Layers & ~from) | to;
        }
        foreach (var child in node.GetChildren())
        {
            Move(child, from, to);
        }
    }

    // The plane's own top-level group of this name. ⚠ Breadth-first and interior-blind: the gauge
    // sub-assemblies inside `cockpit1` each carry their own `markers` child, and a depth-first walk
    // that descends into the interior can return one of those instead of the airframe's.
    private static Node3D? FindGroup(Node3D root, string name)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Node3D n3d
                && AnimRuntime.NameOf(n3d).Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return n3d;
            }
        }
        foreach (var child in root.GetChildren())
        {
            if (child is Node3D n3d && !IsInterior(n3d) && FindGroup(n3d, name) is { } hit)
            {
                return hit;
            }
        }
        return null;
    }

    private static bool IsInterior(Node3D node) =>
        AnimRuntime.NameOf(node).StartsWith("cockpit", StringComparison.OrdinalIgnoreCase);

    /// <summary>Which of the four groups render this frame. All four true is the built state,
    /// which is what every external view and every AI plane keeps.</summary>
    public readonly record struct Shown(bool Interior, bool Body, bool Markers, bool Dontmove);
}
