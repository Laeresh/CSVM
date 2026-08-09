namespace CSVM.Mech3;

/// <summary>The original's per-node visibility gate — <c>FUN_0056c430</c>, the one rule every
/// gamez <c>zone_id</c> obeys (<c>PLAN-weather-decompile-match</c> B12, docs/formats/gamez.md).
///
/// <para><b>The rule.</b> <c>FUN_004d62d0</c> arms the camera each frame with the zone set
/// <c>{0, camera weather state}</c> (the state machine's 1 = below the cloud deck / 2 = above it /
/// 3 = inside a <c>fog_zone</c> volume — <c>WeatherState.CameraWeatherState</c>); the walk then
/// draws a node iff its <c>zone_id</c> is <b>−1</b> (always), or is <b>in that set</b>. So −1 and
/// 0 are ungated, and 1/2/3 are three buckets of which exactly one is live at a time. Nothing else
/// is gated: the geometric point-in-zone fallback (<c>FUN_004c7630</c>) runs only when no explicit
/// state was armed, which the remake never does.</para>
///
/// <para><b>How the remake applies it: a visual layer per zone, and a cull-mask bit per
/// camera.</b> <see cref="LayerFor"/> hands each gated zone one of Godot's visual layers, which
/// <c>SceneBuilder.BuildSubtree</c> stamps onto every mesh instance it builds for a zoned node
/// (per NODE, never inherited down a subtree — the data has parents and children on different
/// zones, e.g. C1's <c>flaglite1</c>/<c>flaglite2</c> are −1 under a zone-1 parent, and only a
/// per-node stamp reproduces that). <c>Session.WeatherRig.Tick</c> then keeps exactly one of those
/// bits in each camera's cull mask.
///
/// <para>⚠ A cull mask, never <c>Node3D.Visible</c>, and that is a requirement rather than a
/// style: splitscreen panes can sit in different states at the same instant (visibility is a
/// property of the shared node, a cull mask a property of the pane's camera), AND several
/// subsystems read or write <c>Visible</c> on this very world content — the animation runtime's
/// <c>NodeActive</c> condition and its uncovered-<c>destroyed</c> sweep, <c>WorldBuilder</c>'s
/// unplaced-entity hide/restore pair, <c>DamageVisuals</c>' healthy/torn swap. A visibility-based
/// gate would silently answer all of their questions with "the camera is above the deck".</para>
///
/// <para>The two camera-anchored per-rig singletons — the cloud deck and the skydome — are the
/// exception, and they take <see cref="Draws"/> against <c>Node3D.Visible</c> on the rig's OWN
/// copy instead: a per-player copy is already private to one camera, and it carries that player's
/// visual layer (<c>SplitScreen.SetVisualLayer</c>), which a zone layer cannot share — a cull mask
/// ORs its bits, so it cannot express "this pane AND this zone".</para></summary>
public static class ZoneGate
{
    /// <summary>The highest <c>zone_id</c> the band carries. Every chapter in this install
    /// authors ids in −1/1/2/3 only (surveyed across all eight gamez node tables).</summary>
    public const int MaxZoneId = 3;

    /// <summary>Every bit <see cref="LayerFor"/> can return, as one mask. A camera holding all of
    /// them is UNGATED, which is what a fresh <see cref="Godot.Camera3D"/>, every
    /// <c>SplitScreen</c> mask and a <c>--no-zone-cull</c> run all are.</summary>
    public const uint LayerBand = 0x7u << LayerBit0;

    // Bits 13-15 (layers 14-16) of Godot's 20, immediately below UI.SplitScreen's reserved
    // per-player band at 16-19. The world builds everything else on the default layer 1 (bit 0).
    // Bit 15 alone was the single shared cloud-field layer this band replaced (A7's altitude gate
    // over the two ambient cloud populations, which is this gate's special case).
    private const int LayerBit0 = 13;

    /// <summary>The shared visual layer carrying every mesh built for a node of
    /// <paramref name="zoneId"/> — <b>0</b> for an id this gate never hides (−1, 0, or anything
    /// past <see cref="MaxZoneId"/>), meaning "leave it on the default layer".</summary>
    public static uint LayerFor(int zoneId) =>
        zoneId >= 1 && zoneId <= MaxZoneId ? 1u << (LayerBit0 + zoneId - 1) : 0u;

    /// <summary>The gate itself: does a node of <paramref name="zoneId"/> draw for a camera in
    /// <paramref name="cameraState"/>? −1 and 0 always; otherwise only its own state.</summary>
    public static bool Draws(int zoneId, int cameraState) =>
        zoneId <= 0 || zoneId == cameraState;

    /// <summary>One camera's cull mask with the zone band narrowed to
    /// <paramref name="cameraState"/> alone — every other bit is left exactly as it was, so this
    /// composes with the per-player band and with any mode-specific mask.</summary>
    public static uint CullMask(uint mask, int cameraState) =>
        (mask & ~LayerBand) | LayerFor(cameraState);

    /// <summary>One camera's cull mask with the whole zone band restored — the ungated camera
    /// (<c>--no-zone-cull</c>, and the reset a session-spanning camera needs before its first
    /// frame).</summary>
    public static uint OpenCullMask(uint mask) => mask | LayerBand;
}
