using Godot;

namespace CSVM.Flight;

/// <summary>
/// Everything one player's *view* owns for a session. A single-player session has
/// exactly one rig wrapping GameSession's original main-viewport camera, so the 1P path is
/// unchanged; splitscreen has one per pane (see <see cref="UI.SplitScreen"/>).
///
/// <para>The rig exists because the flight view is not just a camera: the skydome, the cloud
/// deck and the cloud-band whiteout overlay are all anchored to <i>the</i> camera every frame, so
/// each player needs a private copy of each, on that player's visual layer. GameSession's
/// per-frame anchoring loops over rigs; everything else in the world (terrain, clutter, the
/// ambient cloud field, map-edge extension, precipitation, all aircraft) is shared and rendered
/// in every pane.</para>
/// </summary>
public sealed class PlayerRig
{
    /// <summary>0-based player index (player 1 = 0).</summary>
    public int Index;

    /// <summary>This player's camera. Single player: GameSession's own camera in the main
    /// viewport. Splitscreen: a camera parented to the player's SubViewport (whose local
    /// transform is therefore its world transform).</summary>
    public Camera3D Camera = null!;

    /// <summary>The player's SubViewport, or null in single player (the main viewport).</summary>
    public SubViewport? Viewport;

    /// <summary>Where this player's screen-space layers go (HUD canvas, whiteout overlay): the
    /// player's SubViewport in splitscreen, the session root in single player.</summary>
    public Node HudParent = null!;

    /// <summary>The visual layer carrying this player's private camera-anchored copies, or 0 in
    /// single player (everything stays on the default layer and no cull mask is narrowed).</summary>
    public uint VisualLayer;

    /// <summary>The player's aircraft, once the flight session builds it.</summary>
    public FlightController? Controller;

    /// <summary>This player's skydome copy, re-centered on <see cref="Camera"/> each frame.</summary>
    public Node3D? Horizon;

    /// <summary>This player's cloudlayer deck copy, re-anchored under the camera each frame.</summary>
    public Node3D? Deck;

    // No per-rig ambient cloud field: the authored fogvol clutter is world-anchored static
    // geometry every pane sees (see Effects/FogVolumeClutter), unlike the dome and the deck,
    // which follow a camera and therefore still need a copy each.

    /// <summary>This player's full-pane cloud-band whiteout overlay, faded by camera altitude.</summary>
    public ColorRect? Whiteout;
}
