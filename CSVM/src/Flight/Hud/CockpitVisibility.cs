using System;
using System.Collections.Generic;
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
/// <see cref="Bind"/> found in one built plane model, for that pilot's own pane alone. In Cockpit a
/// model pack's body cockpit (<see cref="RigCockpit.FromBody"/>) keeps the body, its transplanted
/// panel shows, and the interior and the rig's hide list go.
/// </summary>
public sealed class CockpitVisibility
{
    private readonly Node3D? _interior, _body, _markers, _dontmove;

    // CockpitReseat's originals and the pylon stores, which leave the pilot's pane exactly while the
    // interior is drawn.
    private readonly List<Node3D> _reseated;

    // Every airframe node a body cockpit's `cockpit.hide` names, outside the interior.
    private readonly List<Node3D> _listed;

    // A body cockpit's transplanted instruments (PlaneBuilder.CockpitPanel).
    private readonly Node3D? _panel;

    // The pilot's airframe stamp and the layer a hidden group trades it for. The panel takes the
    // third, which only the pilot's own pane camera draws.
    private readonly uint _own, _out, _pane;

    // Which airframe groups the last Apply moved out of the pilot's own view, so a frame that
    // changes nothing walks no subtree.
    private (bool Body, bool Markers, bool Dontmove, bool Reseated, bool Listed) _hidden;

    private CockpitVisibility(Node3D? interior, Node3D? body, Node3D? markers, Node3D? dontmove,
        IReadOnlyList<Node3D> reseated, int pilot, bool bodyCockpit, List<Node3D> listed, Node3D? panel)
    {
        _interior = interior;
        _body = body;
        _markers = markers;
        _dontmove = dontmove;
        _reseated = new List<Node3D>(reseated);
        _own = SplitScreen.OwnAirframeLayer(pilot);
        _out = SplitScreen.FirstPersonLayer(pilot);
        // The flare band is the one no camera but this pilot's pane draws, pictures included.
        _pane = SplitScreen.WorldFlareLayer(pilot);
        BodyCockpit = bodyCockpit;
        _listed = listed;
        _panel = panel;
    }

    /// <summary>Whether the pilot's Cockpit view is a body cockpit, the rig's replaced airframe seen
    /// from inside, rather than <c>cockpit1</c>.</summary>
    public bool BodyCockpit { get; }

    /// <summary>The decoded rule. <paramref name="firstPerson"/> is THIS FRAME's camera pose, not the
    /// selected view. The original gates on the live camera mode, so a held numpad key or look-behind
    /// restores the body while down, as <see cref="CameraController.RestoreExternalFov"/> does the FOV.
    /// A body cockpit (<paramref name="bodyCockpit"/>) changes the Cockpit view alone: the body
    /// stays, the interior and the hide list go, and the transplanted panel shows.</summary>
    public static Shown Rules(PilotViewMode mode, bool firstPerson, bool bodyCockpit = false) => !firstPerson
        ? new Shown(Interior: false, Body: true, Markers: true, Dontmove: true, Listed: true, Transplant: false)
        : mode == PilotViewMode.Nose
            ? new Shown(Interior: false, Body: false, Markers: false, Dontmove: false, Listed: true, Transplant: false)
            : bodyCockpit
                ? new Shown(Interior: false, Body: true, Markers: true, Dontmove: true, Listed: false, Transplant: true)
                : new Shown(Interior: true, Body: false, Markers: true, Dontmove: true, Listed: true, Transplant: false);

    /// <summary>Finds the four groups in one built plane model; null for a model built no interior.
    /// <paramref name="pilot"/> is the seat whose <see cref="SplitScreen.OwnAirframeLayer"/> the model wears.</summary>
    /// <param name="reseated">The originals <see cref="CockpitReseat.Hidden"/> lists.</param>
    /// <param name="cockpit">The applied rig's section (<see cref="PlaneBuilder.Cockpit"/>); only a body one counts.</param>
    /// <param name="panel">A body cockpit's transplanted instruments (<see cref="PlaneBuilder.CockpitPanel"/>).</param>
    public static CockpitVisibility? Bind(Node3D? planeModel, Node3D? interior, int pilot,
        IReadOnlyList<Node3D>? reseated = null, RigCockpit? cockpit = null, Node3D? panel = null)
    {
        if (planeModel == null || interior == null)
        {
            return null;
        }
        bool body = cockpit is { FromBody: true };
        var listed = new List<Node3D>();
        if (body)
            Collect(planeModel, new HashSet<string>(cockpit!.Hide, StringComparer.OrdinalIgnoreCase), listed);
        return new CockpitVisibility(interior, FindGroup(planeModel, "healthy"),
            FindGroup(planeModel, "markers"), FindGroup(planeModel, "dontmove"),
            reseated ?? Array.Empty<Node3D>(), pilot, body, listed, body ? panel : null);
    }

    /// <summary>Adds an outside part whose stereo copy the interior carries
    /// (<see cref="XrCockpit.Hang"/>), so it leaves the pilot's pane with the reseated originals.</summary>
    public void Reseat(Node3D node)
    {
        _reseated.Add(node);
        // The next Apply moves every group again, the new part with them.
        _hidden = default;
    }

    /// <summary>Write this frame's rule, every frame the rig owns its camera. The interior takes
    /// <c>Visible</c>. A hidden airframe group moves off <see cref="SplitScreen.OwnAirframeLayer"/>
    /// onto <see cref="SplitScreen.FirstPersonLayer"/>, which only this pilot's own pane drops.
    /// ⚠ Never hide those groups through <c>Visible</c>. Every pane shares one scene tree, so a
    /// hidden node is gone from every pilot's view.</summary>
    public void Apply(PilotViewMode mode, bool firstPerson)
    {
        var shown = Rules(mode, firstPerson, BodyCockpit);
        Set(_interior, shown.Interior);
        ShowPanel(shown.Transplant);
        var hidden = (Body: !shown.Body, Markers: !shown.Markers, Dontmove: !shown.Dontmove,
            Reseated: shown.Interior, Listed: !shown.Listed);
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
        foreach (var node in _reseated)
            Move(node, _out, _own);
        foreach (var node in _listed)
            Move(node, _out, _own);
        if (hidden.Body)
            Move(_body, _own, _out);
        if (hidden.Markers)
            Move(_markers, _own, _out);
        if (hidden.Dontmove)
            Move(_dontmove, _own, _out);
        if (hidden.Reseated)
            foreach (var node in _reseated)
                Move(node, _own, _out);
        if (hidden.Listed)
            foreach (var node in _listed)
                Move(node, _own, _out);
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

    // Every node a hide name matches, since gamez names repeat, as every rig rule matches them.
    // Interior-blind like FindGroup: the rig's checks keep hide names off cockpit1.
    private static void Collect(Node3D node, HashSet<string> names, List<Node3D> hits)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is not Node3D n3d || IsInterior(n3d))
                continue;
            if (names.Contains(AnimRuntime.NameOf(n3d)))
                hits.Add(n3d);
            Collect(n3d, names, hits);
        }
    }

    private static bool IsInterior(Node3D node) =>
        AnimRuntime.NameOf(node).StartsWith("cockpit", StringComparison.OrdinalIgnoreCase);

    // Every pane shares the panel's Visible, so the panel also wears the pilot's pane layer alone. It
    // is stamped on each show: seating the airframe stamps the whole model after the bind.
    private void ShowPanel(bool shown)
    {
        if (_panel == null || !GodotObject.IsInstanceValid(_panel))
        {
            return;
        }
        if (shown && !_panel.Visible)
        {
            SplitScreen.SetVisualLayer(_panel, _pane);
        }
        Set(_panel, shown);
    }

    /// <summary>Which of the groups render this frame. Every external view and every AI plane keeps the
    /// built state, all true but the transplant. A body cockpit's <c>cockpit.hide</c> nodes are
    /// <see cref="Listed"/>. Its transplanted panel, <see cref="Transplant"/>, draws exactly where
    /// <c>cockpit1</c> would.</summary>
    public readonly record struct Shown(bool Interior, bool Body, bool Markers, bool Dontmove, bool Listed, bool Transplant);
}
