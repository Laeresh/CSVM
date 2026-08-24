using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.UI;

/// <summary>One objective site's marker: the world node it sits on, the two label lines the
/// original composes (the category line over the site's name), where it is, and the colour its
/// category earns. Exposed as a read model so a suite asserts the marker set without a
/// camera.</summary>
public readonly record struct ObjectiveMarker(
    string Node, string CategoryLine, string Name, Vector3 Position, Color Color);

/// <summary>
/// The flown campaign mission's objective markers, drawn in world over each site the mission
/// flags: the original's target marker in blue, which is the only thing that tells the player
/// where to go. The marker set is <c>targets.zrd</c>'s own <c>objective</c> entries, edited by
/// <c>objectives.zrd</c>'s <c>ADD_/REMOVE_OBJECTIVE_TARGET</c> as objectives complete, with the
/// label resolved through <see cref="Messages"/> and <c>SET_HELP_LABEL</c>. Composition and colour
/// are <see cref="TargetRef"/>'s decoded ones; placement is <see cref="EdgeMarker"/>'s and the
/// drawing is <see cref="MarkerDraw"/>'s, so this and the stunt marker draw the one look.
/// Self-mounting, the shape <see cref="ObjectivesHud"/> uses: mounting it is one line in
/// <c>GameSession</c> and nothing here reaches back into it.
/// </summary>
public sealed partial class ObjectiveMarkerHud : Node
{
    private const int RefMarkerFont = 15;   // MarkerHud's own 1440p marker-text size

    private readonly CampaignDirector _director;
    private readonly Messages _messages;
    private readonly MissionTargets _targets;
    private readonly AnimRuntime? _runtime;
    private readonly Func<Node3D?> _subject;
    private readonly List<ObjectiveMarker> _markers = new();
    private readonly List<string> _live = new();
    private ObjectiveGraph? _graph;
    private CanvasLayer? _hudLayer;
    private bool _dirty = true;

    private ObjectiveMarkerHud(CampaignDirector director, Messages messages, MissionTargets targets,
        AnimRuntime? runtime, Func<Node3D?> subject)
    {
        Name = "objective_marker_hud";
        _director = director;
        _messages = messages;
        _targets = targets;
        _runtime = runtime;
        _subject = subject;
    }

    /// <summary>The markers as drawn this frame, in the mission's own target order. Empty until the
    /// graph is bound, and empty for a mission that flags no objective target.</summary>
    public IReadOnlyList<ObjectiveMarker> Markers => _markers;

    /// <summary>Builds the markers over a campaign director, the mission's target table and the
    /// world's node index. <paramref name="subject"/> is the flown aircraft, read for the
    /// off-screen clock bearing. The director's graph may not exist yet, so binding is polled in
    /// <see cref="_Process"/> exactly as <see cref="ObjectivesHud"/> polls for it.</summary>
    public static ObjectiveMarkerHud Build(CampaignDirector director, Messages messages,
        MissionTargets targets, AnimRuntime? runtime, Func<Node3D?> subject) =>
        new(director, messages, targets, runtime, subject);

    /// <summary>The nodes carrying the objective-target flag right now: every
    /// <c>targets.zrd</c> entry the mission flags <c>objective</c> whose flag no completed
    /// objective has removed, plus everything <c>ADD_OBJECTIVE_TARGET</c> has since added.
    /// ⚠ Do not build this from <see cref="ObjectiveGraph.ObjectiveTargets"/> alone. That store
    /// starts empty, and a mission whose sites are only ever REMOVED shows no marker at all.</summary>
    public static void CollectTargets(ObjectiveScript script, ObjectiveGraph graph,
        MissionTargets targets, List<string> into)
    {
        foreach (var entry in targets.ByNode)
        {
            if (entry.Value.Objective && !RemovedByCompletion(script, graph, entry.Key))
            {
                into.Add(entry.Key);
            }
        }

        foreach (var name in graph.ObjectiveTargets)
        {
            if (!Holds(into, name))
            {
                into.Add(name);
            }
        }
    }

    /// <summary>Where a target's marker goes: the bare <c>TRAVELERS</c> point of the objective that
    /// edits that target, where the mission gives one, and null to fall back to the world node.
    /// ⚠ Prefer the point over the node. C3/M01's village target names a node standing at the world
    /// origin, 7.9 km from the point its own objective tests, so the node is not the site.</summary>
    public static Vector3? PointFor(ObjectiveScript script, string node)
    {
        foreach (var def in script.Objectives)
        {
            if (def.Travelers is { WherePoint: { } p }
                && (Holds(def.RemoveObjectiveTarget, node) || Holds(def.AddObjectiveTarget, node)))
            {
                return new Vector3(p[0], p[1], p[2]);
            }
        }

        return null;
    }

    /// <summary>One site's marker, with its label resolved: the category line over the site's name,
    /// through <see cref="TargetRef"/>'s decoded format strings. <c>SET_HELP_LABEL</c> beats the
    /// table's own <c>help_label</c>, and the blank string a mission writes to clear one is treated
    /// as no label rather than an empty bracket.</summary>
    public static ObjectiveMarker MarkerFor(string node, in MissionTarget info, Messages messages,
        IReadOnlyDictionary<string, string> helpLabels, Vector3 position)
    {
        string category = Text(messages, helpLabels.TryGetValue(node, out var written)
            ? written
            : info.HelpLabel);
        string typeLabel = Text(messages, info.CategoryLabel);
        var candidate = new AimCandidate
        {
            Position = position,
            Team = AimAssist.NeutralTeam,
            Live = true,
            Source = node,
        };
        var target = TargetRef.ForStructure(candidate, TargetClass.Enemy, node,
            typeLabel.Length > 0 ? typeLabel : null, category.Length > 0 ? category : null,
            objective: true);
        string name = Text(messages, info.Description);
        return new ObjectiveMarker(node, target.CategoryLine, name.Length > 0 ? name : node,
            position, ColorOf(target));
    }

    public override void _Ready() => EnsureBuilt();

    public override void _Process(double delta)
    {
        if (_graph == null && _director.Graph is { } graph)
        {
            Attach(graph);
        }

        if (_dirty)
        {
            Rebuild();
        }
    }

    public override void _ExitTree()
    {
        if (_graph is { } graph)
        {
            graph.TargetsChanged -= MarkDirty;
            graph.Completed -= OnCompleted;
        }
    }

    // A site with no category label of its own would fall through the decoded colour rule to its
    // team test, which is meaningless for a piece of scenery; the original draws it blue.
    private static Color ColorOf(in TargetRef target) =>
        target.Category is { Length: > 0 }
            ? TargetHud.MarkerColor(target, AimAssist.NeutralTeam)
            : MarkerDraw.HudBlue;

    private static bool RemovedByCompletion(ObjectiveScript script, ObjectiveGraph graph, string node)
    {
        foreach (var def in script.Objectives)
        {
            if (graph.CompletedOf(def.Number) && Holds(def.RemoveObjectiveTarget, node))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Holds(IReadOnlyList<string> names, string node)
    {
        foreach (var name in names)
        {
            if (string.Equals(name, node, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // A message key resolves to itself when unknown, which is right for a readout and wrong for a
    // marker; a whitespace-only value is how a mission clears a label, so both read as absent.
    private static string Text(Messages messages, string? key) =>
        string.IsNullOrWhiteSpace(key) ? "" : messages.Get(key).Trim();

    private void Attach(ObjectiveGraph graph)
    {
        _graph = graph;
        graph.TargetsChanged += MarkDirty;
        graph.Completed += OnCompleted;
        _dirty = true;
    }

    private void OnCompleted(ObjectiveCompleted _) => MarkDirty();

    private void MarkDirty() => _dirty = true;

    private void Rebuild()
    {
        _dirty = false;
        _markers.Clear();
        if (_graph is not { } graph)
        {
            return;
        }

        _live.Clear();
        CollectTargets(_director.Script, graph, _targets, _live);
        foreach (var node in _live)
        {
            if ((PointFor(_director.Script, node) ?? Resolve(node)?.GlobalPosition) is not { } at)
            {
                continue;
            }

            _markers.Add(MarkerFor(node, _targets.For(node), _messages, graph.HelpLabels, at));
        }
    }

    private Node3D? Resolve(string node)
    {
        if (_runtime == null)
        {
            return null;
        }

        var found = _runtime.FindNodes(node, null);
        return found.Count > 0 && found[0].IsInsideTree() ? found[0] : null;
    }

    private void EnsureBuilt()
    {
        if (_hudLayer != null)
        {
            return;
        }

        _hudLayer = new CanvasLayer { Layer = HudLayers.Hud, Name = "objective_marker_layer" };
        _hudLayer.AddChild(new MarkerCanvas
        {
            Hud = this,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            FocusMode = Control.FocusModeEnum.None,
        });
        AddChild(_hudLayer);
    }

    // The drawing half: a viewport-filling Control projecting each marker through the live camera
    // at _Draw time, so a marker never lags the chase camera. MarkerHud's own arrangement.
    private sealed partial class MarkerCanvas : Control
    {
        public ObjectiveMarkerHud Hud = null!;

        private readonly List<string> _lines = new();

        public override void _Process(double delta)
        {
            Position = Vector2.Zero;
            Size = GetViewportRect().Size;
            QueueRedraw();
        }

        public override void _Draw()
        {
            // A draw can land before _Process has sized us; Godot's font cache errors on zero size.
            float s = Size.Y <= 0f ? 0f : HudMetrics.Scale(this);
            if (s <= 0f || Hud.Markers.Count == 0 || GetViewport()?.GetCamera3D() is not { } camera)
            {
                return;
            }

            var font = GetThemeDefaultFont();
            int fontSize = Mathf.Max(1,
                Mathf.RoundToInt(RefMarkerFont * s * HudMetrics.MarkerTextScale));
            var subject = Hud._subject();
            var from = subject?.GlobalPosition ?? camera.GlobalPosition;
            float heading = HeadingOf(subject, camera);
            foreach (var marker in Hud.Markers)
            {
                DrawMarker(font, marker, from, heading, camera, s, fontSize);
            }
        }

        // Nose heading, 0 = north (-Z), off the flown aircraft where there is one and off the
        // camera otherwise, which is what a spectating pane has to steer the clock bearing by.
        private static float HeadingOf(Node3D? subject, Camera3D camera)
        {
            var nose = -(subject ?? camera).GlobalTransform.Basis.Z;
            return Mathf.PosMod(Mathf.RadToDeg(Mathf.Atan2(nose.X, -nose.Z)), 360f);
        }

        private void DrawMarker(Font font, in ObjectiveMarker marker, Vector3 from, float heading,
            Camera3D camera, float s, int fontSize)
        {
            bool behind = camera.IsPositionBehind(marker.Position);
            var sp = camera.UnprojectPosition(marker.Position);
            var placed = EdgeMarker.Resolve(sp, behind, Size, EdgeMarker.RefEdgeMargin * s);
            _lines.Clear();
            if (marker.CategoryLine.Length > 0)
            {
                _lines.Add(marker.CategoryLine);
            }

            _lines.Add(marker.Name);
            if (placed.OnScreen)
            {
                MarkerDraw.Reticle(this, sp, MarkerDraw.RefReticleR * s, marker.Color);
                MarkerDraw.Lines(this, font,
                    new Vector2(sp.X, sp.Y + (MarkerDraw.RefReticleR + MarkerDraw.RefTextGap) * s),
                    _lines, fontSize, marker.Color, topAnchored: true);
                return;
            }

            _lines.Add($"{EdgeMarker.ClockHour(from, heading, marker.Position)} o'clock");
            MarkerDraw.Arrow(this, placed.Anchor, placed.Dir, MarkerDraw.RefArrowLen * s,
                MarkerDraw.RefArrowHalf * s, s, marker.Color);
            MarkerDraw.LinesClamped(this, font,
                placed.Anchor - placed.Dir * (MarkerDraw.RefArrowLen + MarkerDraw.RefTextGap) * s,
                _lines, fontSize, marker.Color, Size, EdgeMarker.RefEdgeMargin * s);
        }
    }
}
