using System.Collections.Generic;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.UI;

/// <summary>One line of the objectives readout: a display row's own priority, its text resolved
/// through the message table, and whether it is completed.</summary>
public readonly record struct ObjectivesHudLine(int Priority, string Text, bool Completed);

/// <summary>
/// The flown campaign mission's objectives readout, on the PAUSE screen alone (`BL-466`): the
/// original keeps its objectives on the pause screen's parchment and leaves the flight HUD to the
/// gauges, so this layer stays hidden until <see cref="PauseState.Paused"/>. Content is
/// <see cref="CampaignDirector"/>'s read model (<see cref="ObjectiveGraph.Rows"/>), text resolved
/// through <see cref="Messages"/>, completion marked rather than dropped. Every row is shown
/// unconditionally, never filtered by its own Awake flag: see docs/architecture.md's entry for
/// why. The reference frame fixes the corner and nothing else, so the glyphs/metrics below stay
/// TUNE. Self-mounting, but unlike <see cref="PerfHud"/>'s one-for-the-window instance, a
/// splitscreen session builds ONE PER RIG (B15) under that rig's own <c>HudParent</c>, so every
/// pane polls the shared <see cref="ObjectiveGraph"/> on its own.
/// </summary>
public sealed partial class ObjectivesHud : Node
{
    private const float RefFontSize = 20f;      // TUNE: the reference frame fixes no type size
    private const float RefHeadingSize = 24f;   // TUNE
    private const float RefTopMargin = 48f;     // TUNE
    private const float RefSideMargin = 48f;    // TUNE
    private const float RefPanelWidth = 460f;   // TUNE: the reference parchment's rough share
    private const float RefMarkWidth = 34f;     // the mark column, wide enough for the glyph below
    private const string DoneMark = "✓"; // TUNE: a checkmark, not the original's own glyph

    // A completed line is dimmed green rather than struck: the mark already says it is done, and
    // the text stays readable, which is the whole point of the line being there.
    private static readonly Color DoneColor = new(0.65f, 0.85f, 0.65f);

    private readonly CampaignDirector _director;
    private readonly Messages _messages;
    private readonly PauseState _pause;
    private CanvasLayer? _hudLayer;
    private Control? _root;
    private PanelContainer? _panel;
    private Label? _heading;
    private GridContainer? _rows;
    private ObjectiveGraph? _graph;
    private string _drawn = "";

    private ObjectivesHud(CampaignDirector director, Messages messages, PauseState pause)
    {
        Name = "objectives_hud";
        _director = director;
        _messages = messages;
        _pause = pause;
    }

    /// <summary>Builds the readout over a campaign director, the shared message table and the
    /// session's pause state. The director's graph may not exist yet
    /// (<see cref="CampaignDirector.Attach"/> runs after this node would be added), so binding is
    /// polled in <see cref="_Process"/> rather than assumed at construction.</summary>
    public static ObjectivesHud Build(CampaignDirector director, Messages messages, PauseState pause) =>
        new(director, messages, pause);

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

    /// <summary>The lines actually drawn: <see cref="BuildLines"/> minus every row the mission
    /// gives no message key, which resolves to no text at all (C3/M01 authors two such secondary
    /// rows). Drawn anyway, such a row is a mark against blank space, which is what stopped a
    /// player reading WHICH objective had completed; the original's pause screen lists the keyed
    /// rows alone.</summary>
    public IReadOnlyList<ObjectivesHudLine> DrawnLines()
    {
        var drawn = new List<ObjectivesHudLine>();
        foreach (var line in BuildLines())
        {
            if (!string.IsNullOrWhiteSpace(line.Text))
            {
                drawn.Add(line);
            }
        }

        return drawn;
    }

    public override void _Ready() => EnsureBuilt();

    public override void _Process(double delta)
    {
        if (_graph == null && _director.Graph is { } graph)
        {
            Attach(graph);
        }

        // Only the pause screen carries the readout, so nothing is drawn or rebuilt while flying.
        if (_hudLayer != null)
        {
            _hudLayer.Visible = _pause.Paused;
        }

        if (_pause.Paused)
        {
            Refresh();
        }
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

        // The board layer, not the HUD one: this is part of the pause screen, and the session adds
        // it after the pause board, so tree order puts it over that board's own backdrop.
        _hudLayer = new CanvasLayer { Layer = HudLayers.Board, Name = "objectives_hud_layer", Visible = false };
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _heading = new Label { Text = "Objectives" };
        _rows = new GridContainer { Columns = 2 };
        box.AddChild(_heading);
        box.AddChild(_rows);
        _panel.AddChild(box);
        _root.AddChild(_panel);
        _hudLayer.AddChild(_root);
        AddChild(_hudLayer);
    }

    // Redraws only when the content or the window scale actually changed: a pause holds for as
    // long as the player leaves it holding, and rebuilding label nodes every frame of that is
    // waste nobody can see.
    private void Refresh()
    {
        if (_rows == null || _panel == null || _heading == null || _root == null)
        {
            return;
        }

        var lines = DrawnLines();
        float scale = WindowScale();
        var signature = new StringBuilder().Append(scale.ToString("0.###"));
        foreach (var line in lines)
        {
            signature.Append(line.Completed ? "|1|" : "|0|").Append(line.Text);
        }

        if (signature.ToString() == _drawn)
        {
            return;
        }

        _drawn = signature.ToString();
        Draw(lines, scale);
    }

    private void Draw(IReadOnlyList<ObjectivesHudLine> lines, float scale)
    {
        int font = Mathf.Max(1, Mathf.RoundToInt(RefFontSize * scale));
        float textWidth = (RefPanelWidth - RefMarkWidth) * scale;
        foreach (var child in _rows!.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var line in lines)
        {
            // The mark takes a column of its own, so a completed line's text starts where every
            // other line's does: a tick prefixed into the text moved the line it was marking.
            var mark = new Label
            {
                Text = line.Completed ? DoneMark : "",
                CustomMinimumSize = new Vector2(RefMarkWidth * scale, 0f),
            };
            mark.AddThemeFontSizeOverride("font_size", font);
            var text = new Label
            {
                Text = line.Text,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(textWidth, 0f),
            };
            text.AddThemeFontSizeOverride("font_size", font);
            if (line.Completed)
            {
                mark.AddThemeColorOverride("font_color", DoneColor);
                text.AddThemeColorOverride("font_color", DoneColor);
            }

            _rows.AddChild(mark);
            _rows.AddChild(text);
        }

        _heading!.AddThemeFontSizeOverride("font_size", Mathf.Max(1, Mathf.RoundToInt(RefHeadingSize * scale)));
        _panel!.AddThemeStyleboxOverride("panel", PanelStyle(scale));
        _panel.Size = Vector2.Zero;      // a Control clamps up to its combined minimum size
        _panel.Position = new Vector2(
            Mathf.Max(0f, _root!.Size.X - ((RefPanelWidth + RefSideMargin) * scale)), RefTopMargin * scale);
    }

    private StyleBoxFlat PanelStyle(float scale) => new()
    {
        BgColor = new Color(0f, 0f, 0f, 0.55f),
        ContentMarginLeft = 16f * scale,
        ContentMarginRight = 16f * scale,
        ContentMarginTop = 12f * scale,
        ContentMarginBottom = 12f * scale,
    };

    // The window-height ratio, not HudMetrics.Scale's pane-damped form: this readout draws once
    // for the whole window regardless of splitscreen, the same reasoning PerfHud's own override
    // gives for taking the plain ratio instead.
    private float WindowScale()
    {
        float windowH = GetTree()?.Root?.Size.Y ?? Flight.HudMetrics.ReferenceHeight;
        return windowH / Flight.HudMetrics.ReferenceHeight;
    }
}
