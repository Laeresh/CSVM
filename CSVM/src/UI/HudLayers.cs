namespace CSVM.UI;

/// <summary>
/// The <see cref="Godot.CanvasLayer"/> ordering for everything drawn over the 3D view, in one
/// place. Godot sorts canvas layers by this number, and keeping them here rather than as bare
/// literals across nine files is what makes "does the collider overlay draw above the cloud
/// whiteout?" answerable by reading one file.
///
/// <para><b>The order below is measured, not chosen.</b> The original's stacking reads off the
/// footage as <c>world &lt; flare sprites &lt; HUD/cockpit &lt; sun wash</c>: the compass clips the
/// flare's core glow, while at maximum wash the compass strip itself goes (20,20,18) →
/// (178,180,177) — the same α as world pixels. That is why <see cref="SunWash"/> sits *above*
/// <see cref="Hud"/> and <see cref="FlareSprites"/> sits below it.</para>
///
/// <para><b>Why the debug bands sit above the wash.</b> The wash is a full-screen white composite
/// reaching α ≈ 0.66, and it survives terrain occlusion — so anything it covers becomes hard to
/// read whenever the sun is near screen centre. The original had no debug overlays and no
/// scoreboard, so nothing about fidelity says they should wash out; they are the instruments the
/// picture is *judged* with, and washing them out would make the effect an obstacle to verifying
/// itself. <see cref="Debug"/> and <see cref="Lab"/> therefore sit above <see cref="SunWash"/>.
/// Layer 3 is deliberately left free as headroom between the wash and the debug band.</para>
///
/// <para>The <c>--viewer</c> lab *panels* (MeshLab, LiveryLab, WeaponLab, DamageLab) stay on
/// <see cref="Hud"/>: they run in modes that have no flight HUD to contend with.</para>
/// </summary>
internal static class HudLayers
{
    /// <summary>Screen-space effects that belong to the world picture and sit *under* the HUD:
    /// the cloud whiteout (<c>Session/WeatherRig.cs</c>) and the splitscreen pane root.</summary>
    public const int WorldOverlay = 0;

    /// <summary>The lens-flare sprites — core glow and the three rings. Shares
    /// <see cref="WorldOverlay"/> with the cloud whiteout and is ordered under it in tree order,
    /// so flying into a cloud swallows the flare with everything else.</summary>
    public const int FlareSprites = WorldOverlay;

    /// <summary>The flight HUD and cockpit overlay — compass, gauges, reticle, readouts. Also the
    /// <c>--viewer</c> lab panels, which never coexist with it.</summary>
    public const int Hud = 1;

    /// <summary>The lens flare's full-screen white wash ("sun blindness"). Above
    /// <see cref="Hud"/> because the original's whitens the HUD itself at the same α as the
    /// world.</summary>
    public const int SunWash = 2;

    /// <summary>The debug overlays: node labels (T), class overlay, AI nets, markers (K),
    /// colliders (C), the selection gizmo and the tile grid.</summary>
    public const int Debug = 4;

    /// <summary>The interactive labs that own a full panel: the node lab (N) and the world damage
    /// lab (F5).</summary>
    public const int Lab = 5;

    /// <summary>Scoreboards and the launchscreen — always on top of everything.</summary>
    public const int Board = 10;

    /// <summary>The frame-cost readout (<c>F14</c>, PLAN-perf-hitches D10). Above
    /// <see cref="Board"/> on purpose: the launchscreen's background is a full-screen opaque
    /// <c>ColorRect</c> on that layer, and the readout has to read there too — fps/frame cost/GC
    /// are process-wide facts, not something a mode screen should be able to hide.</summary>
    public const int PerfReadout = 11;
}
