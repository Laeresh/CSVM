using System.Collections.Generic;
using Godot;

namespace CSVM.UI;

/// <summary>
/// The splitscreen rendering rig: N panes, each a
/// <see cref="SubViewportContainer"/> + <see cref="SubViewport"/> with its own
/// <see cref="Camera3D"/>, all rendering the SAME <see cref="World3D"/> as the main viewport —
/// one shared world, N views into it. Built only for 2+ players; a single player keeps
/// GameSession's original main-viewport camera untouched (so the 1P render path is unchanged).
///
/// <para><b>Layout</b> (the plan's decision): 2P = a horizontal split, one pane above the other;
/// 3P and 4P = a 2×2 grid, with 3P leaving the last quadrant empty (black). Panes are laid out
/// manually on every resize rather than through a Container so the split stays exact and the
/// gutter width is ours; the backdrop rect paints the gutters and the empty 3P quadrant.</para>
///
/// <para><b>Per-player visibility layers.</b> Most of the world is shared geometry every camera
/// sees. But the skydome, the cloud deck and the whiteout overlay are *camera-anchored*
/// singletons (GameSession re-centers them on "the camera" each frame) — with several players
/// they must exist once per player and each camera must see only its own copy. So each player
/// owns one visual layer out of a reserved band at the top of Godot's 20 (<see cref="PlayerLayerBit0"/>
/// = layers 17–20): the player's private copies are moved onto that layer
/// (<see cref="SetVisualLayer"/>) and the player's camera culls the whole band except its own bit
/// (<see cref="PlayerCullMask"/>). Everything the world builds stays on the default layer 1 and
/// is therefore visible in every pane — including the other players' aircraft.</para>
///
/// <para><b>Every pane is a 3D audio listener</b> (<see cref="Viewport.AudioListenerEnable3D"/>
/// on each SubViewport). Godot's <c>AudioStreamPlayer3D</c> takes the per-channel MAXIMUM over all
/// listener-enabled viewports of its World3D, so an emitter is heard at its NEAREST pane's volume
/// with no N-fold buildup and no manual attenuation; the price is that panning is unioned across
/// panes, since the panes share one stereo output. Without this a splitscreen session has NO
/// listener at all — the main camera stands down below, and a camera only joins the World3D
/// listener set while it is current — and every 3D emitter in the world goes silent.</para>
///
/// <para><b>The zone-gate band</b> (<c>Mech3.ZoneGate.LayerBand</c>, layers 14–16) is the other
/// named allocation out of the same 20, and it is not per player either: three SHARED layers, one
/// per gamez <c>zone_id</c> 1/2/3, carrying every mesh built for a node of that zone — the placed
/// <c>cloudparent</c> clusters, the <c>fvol</c> clutter field, the zone-1 mission targets and
/// ground world. <c>SceneBuilder</c> stamps them at build time and <c>WeatherRig.Tick</c> keeps
/// exactly one of the three bits in each camera's cull mask — that camera's own weather state —
/// which is the original's own zone gate, applied per CAMERA. Every cull mask built here
/// includes all three bits, so a chapter, a mode or a <c>--no-zone-cull</c> run that never applies
/// the gate renders every zone exactly as an ungated build does.</para>
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

    private const int Gutter = 2;   // px between panes (TUNE)

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

    /// <summary>Places the panes over the current window rect: 2P stacked top/bottom, 3–4P in a
    /// 2×2 grid (3P's fourth quadrant stays backdrop-black). Runs on every resize.</summary>
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
