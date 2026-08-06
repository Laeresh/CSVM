using System;
using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.UI;

/// <summary>
/// The AI patrol-net overlay (key F13, flag <c>--debug-ainets</c>): draws the chapter's
/// <c>ne0NNNNN</c> waypoint graphs (docs/formats/ai-nets.md) — data the original never
/// renders. Every net gets one stable id-derived colour; edges are drawn as individual
/// segments from the explicit edge list, <b>never</b> as a closed polygon or a node-order
/// polyline — the graph branches, and assuming a loop draws fiction (the mistake the dzpath
/// "polygon" already invited, BL-249-era). Nodes get markers (tagged nodes bigger — the raw
/// undecoded stop/valve candidates), each net a fixed-size name label with its trailer
/// (<c>M4ReinfAce → player</c>). Depth-tested on purpose: an x-ray view lies about where a
/// route threads terrain.
///
/// <para>F13 is the first tenant of the F13–F24 range reserved for debug overlays
/// (docs/controls.md); the older overlay keys (C/X/T/…) migrate there later.</para>
/// </summary>
public sealed partial class AiNetsOverlay : Node
{
    private const float NodeRadius = 6f;
    private const float TaggedNodeRadius = 12f;

    private readonly string _chapterZrdrPath;
    private readonly string _chapter;

    private List<AiNet>? _nets;
    private Node3D? _holder;
    private bool _shown, _debugDone;
    private CanvasLayer? _hudLayer;
    private Label? _hud;
    private string _summary = "";

    public AiNetsOverlay(string chapterZrdrPath, string chapter)
    {
        _chapterZrdrPath = chapterZrdrPath;
        _chapter = chapter;
        Name = "ainets_overlay";
    }

    /// <summary><c>--debug-ainets</c>: open the overlay on the first frame, the scripted
    /// stand-in for the F13 press.</summary>
    public bool DebugShow { get; init; }

    /// <summary>Comma-separated net names to build (<c>--debug-ainets=&lt;name,…&gt;</c>);
    /// empty = every net. Matching is exact, case-insensitive — the <c>--node=</c> manner:
    /// a miss logs the candidates containing the token rather than failing silently.</summary>
    public string Filter { get; init; } = "";

    public override void _Process(double delta)
    {
        if (DebugShow && !_debugDone)
        {
            // Deferred one frame like every other --debug-* opener: the world subtree is only
            // final once the session has finished building.
            _debugDone = true;
            Toggle();
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.F13 })
        {
            return;
        }
        Toggle();
        GetViewport().SetInputAsHandled();
    }

    /// <summary>F13: show or hide the chapter's patrol nets. The nets are loaded once on
    /// first use (the reader is pure file I/O; a session that never opens the overlay never
    /// pays for it) and the geometry is rebuilt per show — it is static data, but rebuild
    /// keeps the filter and a future live-reload honest.</summary>
    public void Toggle()
    {
        if (_shown)
        {
            _holder?.QueueFree();
            _holder = null;
            _shown = false;
            HideNotice();
            Log.Info("world", $"ai nets overlay off");
            return;
        }
        _nets ??= LoadAndCensus();
        if (_nets.Count == 0)
        {
            Log.Warn("world", $"ai nets overlay: {_chapter} ships no ne0* patrol nets ({_chapterZrdrPath})");
            ShowNotice($"NO PATROL NETS IN {_chapter}");
            _shown = true; // the notice is the overlay; F13 again clears it
            return;
        }
        Build(Selected(_nets));
        _shown = true;
        ShowNotice(_summary);
    }

    // Golden-ratio hue spacing: consecutive ids land far apart, and the colour is a pure
    // function of the id — the same net reads the same colour in every session.
    private static Color ColorOf(int id) =>
        Color.FromHsv(id * 0.618034f % 1f, 0.8f, 1f);

    private static Node3D BuildNet(AiNet net, Color color)
    {
        var root = new Node3D { Name = $"net_{net.Id}_{net.Name}" };
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };

        // Edges: individual segments, straight off the edge list.
        var lines = new SurfaceTool();
        lines.Begin(Mesh.PrimitiveType.Lines);
        foreach (var (a, b) in net.Edges)
        {
            if (a < 0 || b < 0 || a >= net.Nodes.Count || b >= net.Nodes.Count)
            {
                continue; // unseen in this install; never draw a fictitious segment
            }
            lines.AddVertex(net.Nodes[a].Position);
            lines.AddVertex(net.Nodes[b].Position);
        }
        lines.SetMaterial(material);
        var edgeMesh = new ArrayMesh();
        lines.Commit(edgeMesh);
        root.AddChild(new MeshInstance3D
        {
            Name = "edges",
            Mesh = edgeMesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        // Node markers; a tagged node (raw undecoded extras) draws bigger.
        var plain = new SphereMesh { Radius = NodeRadius, Height = NodeRadius * 2f, Material = material };
        var tagged = new SphereMesh { Radius = TaggedNodeRadius, Height = TaggedNodeRadius * 2f, Material = material };
        for (int i = 0; i < net.Nodes.Count; i++)
        {
            root.AddChild(new MeshInstance3D
            {
                Name = $"node{i}",
                Mesh = net.Nodes[i].Tags.Count > 0 ? tagged : plain,
                Position = net.Nodes[i].Position,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }

        // One fixed-size billboarded label per net at its first node — the name is the join
        // key aiv/egen/zeppelins use, so it goes on screen verbatim, trailer appended.
        string text = $"{net.Name}#{net.Id}"
            + (net.Trailer is { } t ? $" → {t.Name ?? $"node{t.NodeIndex}"}" : "");
        if (net.Nodes.Count > 0)
        {
            root.AddChild(new Label3D
            {
                Text = text,
                Position = net.Nodes[0].Position + new Vector3(0f, TaggedNodeRadius * 2f, 0f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FixedSize = true,
                PixelSize = 0.0007f,
                FontSize = 40,
                OutlineSize = 12,
                Modulate = color,
            });
        }
        return root;
    }

    // The census: one greppable line per net beside a scripted run's screenshot.
    private List<AiNet> LoadAndCensus()
    {
        List<AiNet> nets;
        try
        {
            nets = AiNets.Load(_chapterZrdrPath);
        }
        catch (Exception e) when (e is System.IO.IOException or System.IO.InvalidDataException)
        {
            Log.Warn("world", $"ai nets overlay: cannot read {_chapterZrdrPath}: {e.Message}");
            return new List<AiNet>();
        }
        Log.Info("world", $"ai nets: {_chapter} ships {nets.Count} patrol net(s)");
        foreach (var net in nets)
        {
            string trailer = net.Trailer is { } t
                ? $" trailer={(t.Name ?? "?")}{(t.NodeIndex >= 0 ? $"@node{t.NodeIndex}" : "")}"
                : "";
            Log.Info("world", $"ai nets: #{net.Id} '{net.Name}' nodes={net.Nodes.Count} edges={net.Edges.Count}{trailer}");
        }
        return nets;
    }

    private List<AiNet> Selected(List<AiNet> nets)
    {
        if (Filter.Length == 0)
        {
            return nets;
        }
        var picked = new List<AiNet>();
        foreach (var token in Filter.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var hit = nets.Find(n => string.Equals(n.Name, token, StringComparison.OrdinalIgnoreCase));
            if (hit != null)
            {
                if (!picked.Contains(hit))
                {
                    picked.Add(hit);
                }
                continue;
            }
            var near = nets.FindAll(n => n.Name.Contains(token, StringComparison.OrdinalIgnoreCase))
                .ConvertAll(n => n.Name);
            if (near.Count > 0)
            {
                Log.Warn("world", $"--debug-ainets: no net named '{token}' in {_chapter}; candidates containing it: {string.Join(", ", near)}");
            }
            else
            {
                Log.Warn("world", $"--debug-ainets: no net named '{token}' in {_chapter} — the census lines above list all {nets.Count}");
            }
        }
        return picked;
    }

    private void Build(List<AiNet> nets)
    {
        _holder = new Node3D { Name = "ainets_draw" };
        _holder.SetMeta(SelectionService.OverlayMeta, true);
        int nodes = 0, edges = 0;
        foreach (var net in nets)
        {
            var color = ColorOf(net.Id);
            _holder.AddChild(BuildNet(net, color));
            nodes += net.Nodes.Count;
            edges += net.Edges.Count;
        }
        AddChild(_holder);
        int skipped = (_nets?.Count ?? 0) - nets.Count;
        _summary = $"ai nets: {nets.Count} net(s), {nodes} nodes, {edges} edges"
            + (skipped > 0 ? $" ({skipped} filtered out)" : "");
        Log.Info("world", $"{_summary}");
    }

    private void ShowNotice(string text)
    {
        if (_hudLayer == null)
        {
            _hudLayer = new CanvasLayer { Layer = 2 };
            var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _hud = new Label
            {
                // Below the class overlay's readout (X), which sits at y=200..220.
                Position = new Vector2(12, 260),
                Modulate = new Color(0.7f, 1f, 0.8f),
            };
            _hud.AddThemeFontSizeOverride("font_size", 13);
            root.AddChild(_hud);
            _hudLayer.AddChild(root);
            AddChild(_hudLayer);
        }
        _hud!.Text = text;
        _hudLayer.Visible = true;
    }

    private void HideNotice()
    {
        if (_hudLayer != null)
        {
            _hudLayer.Visible = false;
        }
    }
}
