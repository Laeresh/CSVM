using System;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// The per-mode node hiding the original applies to the pilot's OWN aircraft while a first-person
/// view is on the screen (docs/org/cameraViews.md, "Mode 6 = Cockpit" / "Mode 7 = Nose"): Cockpit
/// draws the <c>cockpit1</c> interior, Nose draws none of it and additionally drops the
/// <c>markers</c> and <c>dontmove</c> groups, and both hide the <c>healthy</c> body. Every other
/// view, and every other aircraft, is untouched.
/// <see cref="Rules"/> is the decision and is pure, so it unit-tests without an engine;
/// <see cref="Apply"/> is the thin write of that decision onto the four nodes
/// <see cref="Bind"/> found in one built plane model.
/// </summary>
public sealed class CockpitVisibility
{
    private readonly Node3D? _interior, _body, _markers, _dontmove;

    private CockpitVisibility(Node3D? interior, Node3D? body, Node3D? markers, Node3D? dontmove)
    {
        _interior = interior;
        _body = body;
        _markers = markers;
        _dontmove = dontmove;
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

    /// <summary>Finds the four groups in one built plane model, or null when the model carries no
    /// interior at all — an AI plane, or any build that did not ask <see cref="PlaneBuilder"/> for
    /// one, which is every build outside a human rig.</summary>
    public static CockpitVisibility? Bind(Node3D? planeModel, Node3D? interior)
    {
        if (planeModel == null || interior == null)
        {
            return null;
        }
        return new CockpitVisibility(interior, FindGroup(planeModel, "healthy"),
            FindGroup(planeModel, "markers"), FindGroup(planeModel, "dontmove"));
    }

    /// <summary>Write this frame's rule onto the four nodes. Called every frame the rig owns its
    /// camera, so nothing else has to remember to undo a hide.
    /// ⚠ Splitscreen shares one scene tree: visibility is a property of the node, not of a
    /// viewport, so a pilot in the cockpit hides that plane's body in EVERY pane. Each rig owns its
    /// own plane model, which makes the rule per-pilot; a per-pane rule needs render layers.</summary>
    public void Apply(PilotViewMode mode, bool firstPerson)
    {
        var shown = Rules(mode, firstPerson);
        Set(_interior, shown.Interior);
        Set(_body, shown.Body);
        Set(_markers, shown.Markers);
        Set(_dontmove, shown.Dontmove);
    }

    private static void Set(Node3D? node, bool visible)
    {
        if (node != null && node.Visible != visible)
        {
            node.Visible = visible;
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
