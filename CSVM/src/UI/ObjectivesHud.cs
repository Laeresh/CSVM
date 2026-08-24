using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.UI;

/// <summary>One line of the in-flight objectives readout: a display row's own priority, its text
/// resolved through the message table, and whether it is completed.</summary>
public readonly record struct ObjectivesHudLine(int Priority, string Text, bool Completed);

/// <summary>
/// The in-flight objectives display (PLAN-M5-campaign.md D33): the flown campaign mission's
/// current objectives, fed by <see cref="CampaignDirector"/>'s read model
/// (<see cref="ObjectiveGraph.Rows"/>), text resolved through <see cref="Messages"/> with raw-key
/// fallback, completion struck rather than dropped. Every row is shown unconditionally, never
/// filtered by its own Awake flag: see docs/architecture.md's entry for why. No reference
/// screenshot covers the original's in-flight presentation, so this follows the existing HUD
/// idiom rather than a captured layout; PLAN-M5-campaign.md's D33 section names the still-owed
/// capture. Self-mounting, the shape <see cref="PerfHud"/> uses: nothing here reaches
/// <c>GameSession</c>, and mounting it is a one-line wiring contract that same section names,
/// since <c>GameSession.cs</c> is off limits while another item edits it.
/// </summary>
public sealed partial class ObjectivesHud : Node
{
    private const float RefFontSize = 20f;     // TUNE: no reference PNG covers this presentation
    private const float RefTopMargin = 64f;    // TUNE
    private const float RefLeftMargin = 24f;   // TUNE
    private const string DoneMark = "✓ "; // TUNE: a checkmark, not the original's own glyph

    private readonly CampaignDirector _director;
    private readonly Messages _messages;
    private CanvasLayer? _hudLayer;
    private Label? _label;
    private ObjectiveGraph? _graph;

    private ObjectivesHud(CampaignDirector director, Messages messages)
    {
        Name = "objectives_hud";
        _director = director;
        _messages = messages;
    }

    /// <summary>Builds the readout over a campaign director and the shared message table. The
    /// director's graph may not exist yet (<see cref="CampaignDirector.Attach"/> runs after this
    /// node would be added, per the wiring contract above), so binding is polled in
    /// <see cref="_Process"/> rather than assumed at construction.</summary>
    public static ObjectivesHud Build(CampaignDirector director, Messages messages) =>
        new(director, messages);

    /// <summary>The current display lines: one per <see cref="ObjectiveGraph.Rows"/> row, in the
    /// graph's own priority order, text resolved through the message table. Exposed separately
    /// from drawing so an in-engine suite can assert the readout's own content follows the graph,
    /// not just the graph's internal state.</summary>
    public IReadOnlyList<ObjectivesHudLine> BuildLines()
    {
        var lines = new List<ObjectivesHudLine>();
        if (_graph == null)
        {
            return lines;
        }

        foreach (var row in _graph.Rows)
        {
            lines.Add(new ObjectivesHudLine(row.Priority, _messages.Get(row.MessageKey), row.Completed));
        }

        return lines;
    }

    public override void _Ready() => EnsureBuilt();

    public override void _Process(double delta)
    {
        if (_graph == null && _director.Graph is { } graph)
        {
            Attach(graph);
        }

        Refresh();
    }

    private void Attach(ObjectiveGraph graph)
    {
        _graph = graph;
        graph.Woke += _ => Refresh();
        graph.Completed += _ => Refresh();
    }

    private void EnsureBuilt()
    {
        if (_hudLayer != null)
        {
            return;
        }

        _hudLayer = new CanvasLayer { Layer = HudLayers.Hud, Name = "objectives_hud_layer" };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _label = new Label { Text = "" };
        root.AddChild(_label);
        _hudLayer.AddChild(root);
        AddChild(_hudLayer);
    }

    private void Refresh()
    {
        if (_label == null)
        {
            return;
        }

        var lines = BuildLines();
        var rows = new string[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            rows[i] = lines[i].Completed ? DoneMark + lines[i].Text : lines[i].Text;
        }

        _label.Text = string.Join("\n", rows);
        float scale = WindowScale();
        _label.AddThemeFontSizeOverride("font_size", Mathf.Max(1, Mathf.RoundToInt(RefFontSize * scale)));
        _label.Position = new Vector2(RefLeftMargin * scale, RefTopMargin * scale);
    }

    // The window-height ratio, not HudMetrics.Scale's pane-damped form: this readout draws once
    // for the whole window regardless of splitscreen, the same reasoning PerfHud's own override
    // gives for taking the plain ratio instead.
    private float WindowScale()
    {
        float windowH = GetTree()?.Root?.Size.Y ?? Flight.HudMetrics.ReferenceHeight;
        return windowH / Flight.HudMetrics.ReferenceHeight;
    }
}
