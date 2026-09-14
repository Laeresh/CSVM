namespace CSVM.UI;

/// <summary>
/// The <see cref="Godot.CanvasLayer"/> ordering for everything drawn over the 3D view, in one
/// place. Godot sorts canvas layers by this number, and keeping them here rather than as bare
/// literals across nine files is what makes "does the collider overlay draw above the cloud
/// whiteout?" answerable by reading one file. Order is measured off the original's footage, not
/// chosen: decode in docs/org/weather.md, wash-over-HUD evidence under BL-165 in the git log.
/// ⚠ <see cref="Debug"/> and <see cref="Lab"/> sit above <see cref="SunWash"/> on purpose: they
/// are the instruments the picture is judged with, and the original never had them, so nothing
/// about fidelity says they should wash out with the rest of the HUD.
/// </summary>
internal static class HudLayers
{
    /// <summary>The cockpit interior's own render pass (<c>Flight/CockpitOverlay</c>), under everything
    /// else: 3D always draws before any canvas layer, so a negative layer still composites over
    /// the world while the whiteout, the screen wash and the HUD keep drawing over the panel,
    /// exactly as they do when the interior is in the main world.</summary>
    public const int CockpitPass = -1;

    /// <summary>Screen-space effects that belong to the world picture and sit *under* the HUD:
    /// the cloud whiteout (<c>Session/WeatherRig.cs</c>) and the splitscreen pane root.</summary>
    public const int WorldOverlay = 0;

    /// <summary>The lens-flare sprites, core glow and the three rings. Shares
    /// <see cref="WorldOverlay"/> with the cloud whiteout and is ordered under it in tree order,
    /// so flying into a cloud swallows the flare with everything else.</summary>
    public const int FlareSprites = WorldOverlay;

    /// <summary>The flight HUD and cockpit overlay, compass, gauges, reticle, readouts. Also the
    /// <c>--viewer</c> lab panels, which never coexist with it.</summary>
    public const int Hud = 1;

    /// <summary>The lens flare's full-screen white wash ("sun blindness"). Above
    /// <see cref="Hud"/> because the original's whitens the HUD itself at the same α as the
    /// world.</summary>
    public const int SunWash = 2;

    /// <summary>The mission-end black-out (<c>UI.MissionEndFade</c>). Above <see cref="SunWash"/>
    /// so the fade darkens the HUD and the wash exactly as the original's copied framebuffer does,
    /// but under <see cref="Debug"/> and <see cref="Lab"/> for the same reason those sit above
    /// <see cref="SunWash"/>: the instruments the picture is judged with stay readable through it.</summary>
    public const int MissionEndFade = 3;

    /// <summary>The cover a session starts under (<c>UI.SessionStartFade</c>). Shares
    /// <see cref="MissionEndFade"/>'s tier, and for the same reasons: one opens a mission and the
    /// other ends one, so they never coexist, and both have to darken the HUD and the wash while
    /// leaving the instruments above them readable. Under <see cref="Board"/>, so the load screen
    /// it takes over from keeps drawing on top of it.</summary>
    public const int SessionStartFade = MissionEndFade;

    /// <summary>The debug overlays: node labels (T), class overlay, AI nets, markers (K),
    /// colliders (C), the selection gizmo and the tile grid.</summary>
    public const int Debug = 4;

    /// <summary>The interactive labs that own a full panel: the node lab (N) and the world damage
    /// lab (F19).</summary>
    public const int Lab = 5;

    /// <summary>Scoreboards and the launchscreen, always on top of everything.</summary>
    public const int Board = 10;

    /// <summary>The frame-cost readout (<c>F14</c>). Above
    /// <see cref="Board"/> on purpose: the launchscreen's background is a full-screen opaque
    /// <c>ColorRect</c> on that layer, and the readout has to read there too, fps/frame cost/GC
    /// are process-wide facts, not something a mode screen should be able to hide.</summary>
    public const int PerfReadout = 11;

    /// <summary>The build's version stamp on the menu (<c>UI.BuildStamp</c>). Shares
    /// <see cref="PerfReadout"/>'s tier for the same reason: it has to read over whatever the
    /// active presentation drew on <see cref="Board"/>, opaque backdrop or decoded artwork.</summary>
    public const int BuildStamp = PerfReadout;

    /// <summary>A cinema (<c>UI.CinemaScreen</c>). Above <see cref="Board"/> because a cinema plays
    /// over the screen it hands off to and has to cover it, and above the two readouts at
    /// <see cref="PerfReadout"/> because a cinema is the picture being judged rather than a mode
    /// screen hiding a process-wide fact.</summary>
    public const int Cinema = 12;
}
