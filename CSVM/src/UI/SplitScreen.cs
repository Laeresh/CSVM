using System.Collections.Generic;
using Godot;

namespace CSVM.UI;

/// <summary>
/// The splitscreen rendering rig: N panes, each a <see cref="SubViewportContainer"/> +
/// <see cref="SubViewport"/> with its own <see cref="Camera3D"/>, all rendering the same
/// <see cref="World3D"/> as the main viewport. Built only for 2+ players; a single player keeps
/// GameSession's original main-viewport camera untouched. Layout: 2P is a horizontal split, 3P
/// and 4P a 2x2 grid with 3P's last quadrant black. Each player owns one visual layer out of a
/// reserved band (<see cref="PlayerLayerBit0"/>) for camera-anchored singletons like the skydome;
/// <see cref="SetVisualLayer"/> moves the copies, <see cref="PlayerCullMask"/> culls the rest of
/// the band. Every pane is a 3D audio listener, or a splitscreen session has no listener at all.
/// The zone-gate band (<c>Mech3.ZoneGate.LayerBand</c>) is a separate, shared allocation of the
/// same 20 layers. The listener model: this module's entry in docs/architecture.md.
/// </summary>
public sealed partial class SplitScreen : CanvasLayer
{
    /// <summary>Panes the rig supports — the reserved visual-layer band is this wide.</summary>
    public const int MaxPlayers = 4;

    // First visual layer of the reserved per-player band. Godot has 20 layers (bits 0–19); the
    // world builds everything on layer 1 (bit 0), so taking the top four leaves the whole middle
    // range free for future use.
    private const int PlayerLayerBit0 = 16;
    private const uint AllLayers = 0xFFFFF;              // Godot's 20 visual layers
    private const uint PlayerBand = 0xFu << PlayerLayerBit0;

    // The shared zone-gate band is Mech3.ZoneGate.LayerBand (bits 13–15 = layers 14–16, zone_id
    // 1/2/3), taken immediately below the per-player band. Outside PlayerBand on purpose: every
    // cull mask below starts with all three INCLUDED, so the gate is something WeatherRig switches
    // OFF, never something a new camera has to remember to switch on. It is allocated in Mech3
    // rather than here because SceneBuilder stamps it at build time, node by node.

    private const int Gutter = 2;   // px between panes — confirmed at the controls

    // Per-player identity colours: the launchscreen's join strip and plane-select
    // cursors, and later the race HUD/scoreboard rows, all key off these so a player
    // recognises "their" colour from the menu through to the results. P1 keeps the launchscreen's
    // existing gold focus colour so a single-player menu looks exactly as it did. TUNE.
    private static readonly Color[] Colors4 =
    {
        new(1f, 0.86f, 0.38f),   // P1 gold
        new(0.45f, 0.83f, 1f),   // P2 sky blue
        new(0.55f, 0.95f, 0.55f),// P3 green
        new(1f, 0.60f, 0.85f),   // P4 pink
    };

    private readonly List<SubViewport> _views = new();
    private readonly List<SubViewportContainer> _panes = new();
    private Control _root = null!;

    /// <summary>One SubViewport per player, in player order. Add the player's camera (and its
    /// HUD canvases) to it.</summary>
    public IReadOnlyList<SubViewport> Views => _views;

    /// <summary>Where player <paramref name="index"/>'s pane sits in a <paramref name="size"/>
    /// area shared by <paramref name="players"/> players: 2P stacked top/bottom, 3–4P a 2×2 grid
    /// (3P's fourth quadrant unused). Static and public because the launchscreen's splitscreen
    /// plane select lays its panes out with the very same call — so you pick your aircraft in the
    /// pane you will then fly in.</summary>
    public static Rect2 PaneRect(int index, int players, Vector2 size)
    {
        int cols = players <= 2 ? 1 : 2, rows = 2;
        float paneW = (size.X - (cols - 1) * Gutter) / cols;
        float paneH = (size.Y - (rows - 1) * Gutter) / rows;
        int col = index % cols, row = index / cols;
        return new Rect2(col * (paneW + Gutter), row * (paneH + Gutter), paneW, paneH);
    }

    /// <summary>The private visual layer of player <paramref name="index"/> — put that player's
    /// camera-anchored copies (skydome / cloud deck) on it. The ambient cloud field is NOT one of
    /// them — the authored fogvol clutter is world-anchored and shared (see FogVolumeClutter).</summary>
    public static uint PlayerVisualLayer(int index) => 1u << (PlayerLayerBit0 + index);

    /// <summary>Player <paramref name="index"/>'s identity colour (menu cursor, HUD tags).</summary>
    public static Color PlayerColor(int index) => Colors4[Mathf.PosMod(index, Colors4.Length)];

    /// <summary>Player <paramref name="index"/>'s short tag ("P1"), used wherever several players
    /// share one list or scoreboard.</summary>
    public static string PlayerTag(int index) => $"P{index + 1}";

    /// <summary>Cull mask for player <paramref name="index"/>'s camera: everything outside the
    /// reserved per-player band (the shared world, all aircraft) plus only this player's own bit.</summary>
    public static uint PlayerCullMask(int index) => (AllLayers & ~PlayerBand) | PlayerVisualLayer(index);

    /// <summary>Moves a whole subtree onto one visual layer (recursively, every
    /// VisualInstance3D) — used on each player's private skydome / deck / puff copies.</summary>
    public static void SetVisualLayer(Node node, uint layer)
    {
        if (node is VisualInstance3D vi)
            vi.Layers = layer;
        foreach (var child in node.GetChildren())
            SetVisualLayer(child, layer);
    }

    /// <summary>Builds the pane rig for <paramref name="players"/> players (2–4), each SubViewport
    /// sharing <paramref name="mainViewport"/>'s World3D. Add the returned node to the session
    /// root; the panes re-lay out themselves on every window resize.</summary>
    public static SplitScreen Build(int players, Viewport mainViewport)
    {
        players = Mathf.Clamp(players, 2, MaxPlayers);
        var split = new SplitScreen { Name = "splitscreen", Layer = HudLayers.WorldOverlay };
        split.Init(players, mainViewport);
        return split;
    }

    private void Init(int players, Viewport mainViewport)
    {
        _root = new Control { Name = "panes", MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        // Backdrop: paints the gutters between panes and (at 3P) the empty fourth quadrant.
        // Without it the main viewport's environment sky shows through those pixels.
        var backdrop = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(backdrop);

        // The panes render the SAME world as the main viewport. A SubViewport nested in the tree
        // inherits its parent viewport's World3D by default; setting it explicitly documents that
        // and survives any future own_world_3d change.
        var world = mainViewport.World3D;
        // SubViewports don't pick up the project's msaa_3d setting (that applies to the root
        // viewport only) — mirror it so the panes anti-alias like the single-player view.
        var msaa = (Viewport.Msaa)(int)(ProjectSettings.GetSetting(
            "rendering/anti_aliasing/quality/msaa_3d", 0).AsInt32());

        for (int i = 0; i < players; i++)
        {
            var pane = new SubViewportContainer
            {
                Name = $"pane{i + 1}",
                Stretch = true,           // the SubViewport resizes with the pane
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            var view = new SubViewport
            {
                Name = $"view{i + 1}",
                World3D = world,
                Msaa3D = msaa,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                HandleInputLocally = false, // input is polled per device, never routed per pane
                // This pane's camera is a 3D audio listener: the world is heard from whichever
                // pane is nearest it (Godot maxes the listeners per channel), not from P1.
                AudioListenerEnable3D = true,
            };
            pane.AddChild(view);
            _root.AddChild(pane);
            _panes.Add(pane);
            _views.Add(view);
        }

        _root.Resized += Relayout;
        Relayout();
    }

    // Places the panes over the current window rect: 2P stacked top/bottom, 3–4P in a
    // 2×2 grid (3P's fourth quadrant stays backdrop-black). Runs on every resize.
    private void Relayout()
    {
        var size = _root.Size;
        if (size.X < 1f || size.Y < 1f)
            return;
        for (int i = 0; i < _panes.Count; i++)
        {
            var rect = PaneRect(i, _panes.Count, size);
            _panes[i].Position = rect.Position;
            _panes[i].Size = rect.Size;
        }
    }
}
