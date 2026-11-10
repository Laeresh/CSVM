namespace CSVM.Mech3;

/// <summary>The original's per-node visibility gate: a node draws iff its <c>zone_id</c> is −1
/// (always), or is in the set the camera arms each frame from its weather state
/// (<c>{0, cameraState}</c>). Decode: docs/formats/gamez.md, docs/formats/world-structure.md.
/// ⚠ Gate with a cull mask, never <c>Node3D.Visible</c>. Splitscreen panes sit in different states
/// at once, and the anim runtime, the unplaced-entity hide and the damage visuals all read and
/// write <c>Visible</c> on that same shared content.
/// ⚠ Apply it per node, never inherited down a subtree: the data puts parents and children on
/// different zones. Camera-anchored singletons are the one exception, in docs/architecture.md.</summary>
public static class ZoneGate
{
    /// <summary>The highest <c>zone_id</c> the band carries. Every chapter in this install
    /// authors ids in −1/1/2/3 only (surveyed across all eight gamez node tables).</summary>
    public const int MaxZoneId = 3;

    /// <summary>Every bit <see cref="LayerFor"/> can return, as one mask. A camera holding all of
    /// them is UNGATED, which is what a fresh <see cref="Godot.Camera3D"/>, every
    /// <c>SplitScreen</c> mask and a <c>--no-zone-cull</c> run all are.</summary>
    public const uint LayerBand = 0x7u << LayerBit0;

    // Bits 13-15 (layers 14-16) of Godot's 20, immediately below UI.Boards.SplitScreen's reserved
    // per-player band at 16-19. The world builds everything else on the default layer 1 (bit 0).
    // The altitude gate over the two ambient cloud populations is a special case of this band,
    // not a separate mechanism. Every cull mask the engine builds starts with all three bits
    // set, so CullMask only ever narrows: a mode, chapter or camera that never calls it renders
    // every zone.
    private const int LayerBit0 = 13;

    /// <summary>The shared visual layer carrying every mesh built for a node of
    /// <paramref name="zoneId"/>, <b>0</b> for an id this gate never hides (−1, 0, or anything
    /// past <see cref="MaxZoneId"/>), meaning "leave it on the default layer".</summary>
    public static uint LayerFor(int zoneId) =>
        zoneId >= 1 && zoneId <= MaxZoneId ? 1u << (LayerBit0 + zoneId - 1) : 0u;

    /// <summary>The gate itself: does a node of <paramref name="zoneId"/> draw for a camera in
    /// <paramref name="cameraState"/>? −1 and 0 always; otherwise only its own state.</summary>
    public static bool Draws(int zoneId, int cameraState) =>
        zoneId <= 0 || zoneId == cameraState;

    /// <summary>The zone of a point effect (a puffer particle, a gun round) at height
    /// <paramref name="y"/>. It is 1 below the band's <paramref name="midpoint"/> and 2 at or above
    /// it. A mission with no band (null) leaves it −1, ungated. This is the original's particle
    /// callback <c>0x473580</c>; decode: docs/formats/weather/atmosphere.md.</summary>
    public static int PointZone(float y, float? midpoint) =>
        midpoint is not { } mid ? -1 : y < mid ? 1 : 2;

    /// <summary>The visual layers a point effect of <paramref name="zone"/> draws on. A zoned point
    /// also carries zone 3's bit. The callback answers −1 for a camera in state 3, so a camera
    /// inside a fog volume draws every particle. An ungated point keeps layer 1.</summary>
    public static uint PointLayers(int zone) =>
        zone is 1 or 2 ? LayerFor(zone) | LayerFor(3) : 1u;

    /// <summary>One camera's cull mask with the zone band narrowed to
    /// <paramref name="cameraState"/> alone, every other bit is left exactly as it was, so this
    /// composes with the per-player band and with any mode-specific mask.</summary>
    public static uint CullMask(uint mask, int cameraState) =>
        (mask & ~LayerBand) | LayerFor(cameraState);

    /// <summary>One camera's cull mask while the volumetric clouds hold <paramref name="cameraState"/>:
    /// zones 1 and 2 both draw, since no whiteout hides the band's switch any more. Zone 3 keeps the
    /// held state's gate, because C5's zone-3 streets are in-volume alternates of its zone-1 ground.
    /// Census: docs/org/weather.md.</summary>
    public static uint HeldCullMask(uint mask, int cameraState) =>
        CullMask(mask, cameraState) | (cameraState == 3 ? 0u : LayerFor(1) | LayerFor(2));

    /// <summary>One camera's cull mask with the whole zone band restored, the ungated camera
    /// (<c>--no-zone-cull</c>, and the reset a session-spanning camera needs before its first
    /// frame).</summary>
    public static uint OpenCullMask(uint mask) => mask | LayerBand;
}
