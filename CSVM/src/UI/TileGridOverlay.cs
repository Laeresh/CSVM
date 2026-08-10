using System;
using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.UI;

/// <summary>
/// The map-edge tile-grid overlay (key F14, flag <c>--debug-tilegrid</c>): tints every ground tile
/// — the map's own and <see cref="MapEdgeExtender"/>'s continuation cells alike — by which cell it
/// is and how it was folded, so the shape of the continuation can be read at the controls instead
/// of only out of a spatio-temporal strip.
///
/// <para><b>What the colours say.</b> Hue is the repetition band's parity
/// (<see cref="MapEdgeExtender.FoldBandIndex"/>), so a run of one hue is exactly
/// <see cref="MapEdgeExtender.BlockCells"/> cells wide and <b>the width of a band reads off the
/// block depth directly</b>. The two axes band independently, so a corner region is its own colour
/// rather than an ambiguous blend. Value alternates cell by cell inside a band, which is what turns
/// "wide-ish" into a countable number. In-map cells take a neutral tint, so the map boundary is
/// unmistakable.
/// <para>⚠ Keyed on the band, not on whether the copy is reflected. Under <c>repeat</c> — the
/// default, and what the original does — nothing is ever reflected, so a flip-keyed hue painted the
/// entire continuation one colour and made the block unreadable in the one mode that matters.</para></para>
///
/// <para><b>Why the strength is 0.2 and not <c>ClassOverlay</c>'s 0.5.</b> This overlay is read
/// while flying, over terrain that has to stay legible as terrain; the information is in where the
/// bands change, not in the colours themselves.</para>
///
/// <para><b>Two tinting paths, one appearance.</b> The map's own tiles are built once, so they are
/// walked once and cached like <c>ClassOverlay</c> does. Extension cells cannot be: they are
/// created and freed on every cell-boundary crossing, and a full window rebuild happens whenever
/// F15/F16 change the fold — a walk-once cache would go stale within seconds of flying. So the
/// extender tints its own cells as they are born and this overlay only flips the flag. Both paths
/// stamp <see cref="SceneBuilder.TintParam"/>, the world shaders' per-instance mix — never a
/// <c>MaterialOverride</c>/<c>MaterialOverlay</c>, which is a different shader entirely (see
/// <see cref="SceneBuilder.TintLine"/>).</para>
///
/// <para><b>What it settled.</b> F15/F16 answered the map-edge fold: A/B against the original at
/// the controls found that the continuation <b>repeats</b> — it does not mirror — over a block of
/// 2 cells on C1/C2/C4 and 1 on C5, with no seam gaps. Both are the defaults, and F16's `mirror` is
/// the mode kept only to look at. The keys stay because that question is re-checkable at the
/// controls in minutes, where measuring it off footage is neither fast nor reliable.</para>
///
/// <para>F14/F15/F16 sit in the F13–F24 range reserved for debug overlays (docs/controls.md);
/// F13 is the patrol nets.</para>
/// </summary>
public sealed partial class TileGridOverlay : Node
{
    // The block depths F15 walks: today's clamp, C4's measurement, C2's measurement, one past C2,
    // and whole-map (the constant-free alternative implementation, refuted by the airport
    // observation but one keypress away from being checked at the controls). --map-edge-block=
    // inserts its own value in sorted position, so a CLI choice is never stranded off the cycle.
    private static readonly int[] BaseCycle = { 1, 2, 3, 4, 12 };

    // Band-parity swatches. Named for what they ARE — alternating repetition bands — rather than
    // for the fold, because band parity is what they encode and "mirrored" would be a lie in the
    // default repeat mode.
    private static readonly string[] LegendLabels =
    {
        "band even", "band odd x", "band odd z", "band odd xz",
    };

    private readonly Node3D _world;
    private readonly MapEdgeExtender? _extender;

    // The map's own ground tiles, tinted once per show and restored on hide.
    private readonly List<GeometryInstance3D> _tinted = new();

    private bool _shown, _debugDone;
    private CanvasLayer? _hudLayer;
    private Label? _hud;
    private RichTextLabel? _legend;

    public TileGridOverlay(Node3D world, MapEdgeExtender? extender)
    {
        _world = world;
        _extender = extender;
        Name = "tilegrid_overlay";
    }

    /// <summary><c>--debug-tilegrid</c>: open the overlay on the first frame, the scripted
    /// stand-in for the F14 press.</summary>
    public bool DebugShow { get; init; }

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
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }
        switch (key.Keycode)
        {
            case Key.F14:
                Toggle();
                break;
            case Key.F15:
                StepBlock();
                break;
            case Key.F16:
                ToggleMode();
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    /// <summary>F14: show or hide the tile-grid tints.</summary>
    public void Toggle()
    {
        if (_extender == null)
        {
            // No continuation means no grid to colour — every mode that reaches this overlay
            // without one builds no partitioned world (--stage=empty, or a world with no
            // area/partition record). Say which, rather than showing an overlay that tints
            // nothing: that reads as "the grid is uniform" instead of "there is no grid".
            Log.Warn("world", $"tile-grid overlay: this world has no map-edge continuation to colour (no area/partition grid).");
            ShowNotice("NO MAP-EDGE GRID IN THIS WORLD\nThe chapter builds no area/partition grid, so there are no tiles to colour.");
            return;
        }
        if (_shown)
        {
            Restore();
            _extender.SetTileTint(false);
            _shown = false;
            HideNotice();
            Log.Info("world", $"tile-grid overlay off");
            return;
        }
        TintInMapTiles();
        _extender.SetTileTint(true);
        _shown = true;
        ShowNotice(StatusText(), showLegend: true);
        Log.Info("world", $"{StatusText()}");
    }

    /// <summary>F15: step the block depth to the next value in the cycle and rebuild the window.
    /// A no-op while the overlay is hidden — the whole point of the key is to watch the bands
    /// change, and silently rebuilding 121 cells for an invisible effect is a hitch for
    /// nothing.</summary>
    public void StepBlock()
    {
        if (_extender == null || !_shown)
        {
            return;
        }
        var cycle = BuildCycle(_extender.MaxBlockCells, _extender.BlockCells);
        int at = cycle.IndexOf(_extender.BlockCells);
        int next = cycle[(at + 1) % cycle.Count];
        _extender.SetContinuation(next, _extender.RepeatInsteadOfMirror);
        ShowNotice(StatusText(), showLegend: true);
        Log.Info("world", $"{StatusText()}");
    }

    /// <summary>F16: swap between the confirmed alternating reflection and plain repetition.
    /// Hidden-overlay no-op, for the same reason as <see cref="StepBlock"/>.</summary>
    public void ToggleMode()
    {
        if (_extender == null || !_shown)
        {
            return;
        }
        _extender.SetContinuation(_extender.BlockCells, !_extender.RepeatInsteadOfMirror);
        ShowNotice(StatusText(), showLegend: true);
        Log.Info("world", $"{StatusText()}");
    }

    /// <summary>The F15 cycle: the standing candidates, dropped to what this grid can hold, plus
    /// whatever <c>--map-edge-block=</c> asked for so a CLI value is always reachable again after
    /// stepping away from it.</summary>
    internal static List<int> BuildCycle(int maxBlockCells, int current)
    {
        var set = new SortedSet<int>();
        foreach (int n in BaseCycle)
        {
            if (n <= maxBlockCells)
            {
                set.Add(n);
            }
        }
        set.Add(Math.Clamp(current, 1, maxBlockCells));
        return new List<int>(set);
    }

    // One coloured word per parity, straight from the extender's own palette — a palette change is
    // the only edit that can move this out of sync with what is on screen.
    private static string BuildLegendText()
    {
        var parts = new List<string>(LegendLabels.Length + 1)
        {
            $"[color=#{MapEdgeExtender.InMapLegendColor.ToHtml(false)}]in-map[/color]",
        };
        var colors = MapEdgeExtender.ParityLegendColors;
        for (int i = 0; i < LegendLabels.Length; i++)
        {
            parts.Add($"[color=#{colors[i].ToHtml(false)}]{LegendLabels[i]}[/color]");
        }
        parts.Add("(one band = one block)");
        return string.Join("   ", parts);
    }

    // Shown in the HUD and echoed to the log, so a screenshot and a log line both say which
    // configuration produced them — four captures of a coastline are otherwise indistinguishable
    // a week later.
    private string StatusText()
    {
        var ext = _extender!;
        string mode = ext.RepeatInsteadOfMirror ? "repeat" : "mirror (not the original)";
        // The rebuild cost is only known after the first fold change, so it appears rather than
        // sitting at a meaningless zero.
        string cost = ext.LastRebuildMs > 0 ? $" · rebuild {ext.LastRebuildMs:F0} ms" : "";
        return $"map edge: block {ext.BlockCells} cell(s) · {mode} · {ext.LiveCellCount} ext cells{cost}"
            + $"   [F15 block · F16 mode]";
    }

    // The map's own ground tiles. Identified through AnimRuntime.IndexMeta — the gamez-node-index
    // stamp the built scene already carries, the same one ClassOverlay resolves through — and
    // binned by MapEdgeExtender, so the map's tiles and the extension's go through one piece of
    // grid maths rather than two that can disagree at the split half-tiles.
    //
    // ⚠ The extender's own subtree is skipped, and must be. Extension cells are built from the
    // SAME gamez nodes as the tiles they mirror, so they carry the same IndexMeta and would
    // resolve here to their SOURCE cell's in-map colour — quietly overwriting the parity tint the
    // extender just gave them and painting the whole continuation neutral, which is precisely the
    // thing the overlay exists to distinguish.
    private void TintInMapTiles()
    {
        void Walk(Node n)
        {
            if (ReferenceEquals(n, _extender))
            {
                return; // the continuation tints itself, per-cell, in BuildCell
            }
            if (n is Node3D marked && marked.HasMeta(SelectionService.OverlayMeta))
            {
                return; // another overlay's drawings
            }
            if (n is MeshInstance3D mi && mi.Name == "mesh" && mi.GetParent() is Node3D owner
                && owner.HasMeta(AnimRuntime.IndexMeta)
                && _extender!.InMapTintForNode((int)owner.GetMeta(AnimRuntime.IndexMeta)) is { } tint)
            {
                mi.SetInstanceShaderParameter(SceneBuilder.TintParam, tint);
                _tinted.Add(mi);
            }
            foreach (var child in n.GetChildren())
            {
                Walk(child);
            }
        }
        Walk(_world);
    }

    private void Restore()
    {
        foreach (var node in _tinted)
        {
            if (IsInstanceValid(node))
            {
                node.SetInstanceShaderParameter(SceneBuilder.TintParam, MapEdgeExtender.UntintedMix);
            }
        }
        _tinted.Clear();
    }

    private void ShowNotice(string text, bool showLegend = false)
    {
        if (_hudLayer == null)
        {
            _hudLayer = new CanvasLayer { Layer = HudLayers.Debug };
            var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
            root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _hud = new Label
            {
                // Below the class overlay's readout (X), which sits at y=200..240.
                Position = new Vector2(12, 260),
                Modulate = new Color(0.85f, 0.95f, 1f),
            };
            _hud.AddThemeFontSizeOverride("font_size", 13);
            root.AddChild(_hud);
            _legend = new RichTextLabel
            {
                Position = new Vector2(12, 280),
                Size = new Vector2(900, 24),
                BbcodeEnabled = true,
                FitContent = true,
                ScrollActive = false,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Text = BuildLegendText(),
            };
            _legend.AddThemeFontSizeOverride("normal_font_size", 13);
            _legend.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
            root.AddChild(_legend);
            _hudLayer.AddChild(root);
            AddChild(_hudLayer);
        }
        _hud!.Text = text;
        _legend!.Visible = showLegend;
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
