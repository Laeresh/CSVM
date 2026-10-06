using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Launch;

/// <summary>
/// What the process command line and the project settings say about OpenXR, the inputs Godot
/// itself decides on. Godot consumes its engine arguments before <c>OS.GetCmdlineArgs</c> is
/// filled, so the process's own argument list is read instead. Engine-free, so it unit-tests.
/// </summary>
public static class XrLaunch
{
    /// <summary>The project setting a launch without <c>--xr-mode</c> follows.</summary>
    public const string EnabledSetting = "xr/openxr/enabled";

    /// <summary>The feature tag that turns Godot's multiview shader variants on for one launch:
    /// <c>project.godot</c> overrides <c>xr/shaders/enabled</c> under it. The editor binary reads
    /// custom tags from <see cref="FeatureVariable"/> before the renderer starts. No game code runs
    /// that early, so the launch scripts set it whenever the arguments carry <c>--xr-sim</c>.</summary>
    public const string SimFeature = "csvm_xr_sim";

    /// <summary>The environment variable Godot's editor binary reads custom feature tags from.</summary>
    public const string FeatureVariable = "GODOT_EDITOR_CUSTOM_FEATURES";

    /// <summary>Whether this launch asked Godot for OpenXR: the last <c>--xr-mode on|off</c> ahead
    /// of the user-argument separator, else <paramref name="projectEnabled"/>. The same rule as
    /// Godot's own <c>OpenXRAPI::openxr_is_enabled</c>, where <c>default</c> defers to the setting.</summary>
    public static bool Requested(IReadOnlyList<string> processArgs, bool projectEnabled)
    {
        bool? mode = null;
        for (int i = 0; i < processArgs.Count; i++)
        {
            string arg = processArgs[i];
            if (arg is "--" or "++")
            {
                break;
            }

            if (arg == "--xr-mode" && i + 1 < processArgs.Count)
            {
                i++;
                mode = processArgs[i].ToLowerInvariant() switch
                {
                    "on" => true,
                    "off" => false,
                    _ => null,
                };
            }
        }

        return mode ?? projectEnabled;
    }
}

/// <summary>
/// The stereo path of one process, built only for OpenXR (<c>--xr-mode on</c> ahead of <c>--</c>)
/// or the <c>--xr-sim</c> stand-in (<see cref="XrSimInterface"/>). Its <see cref="Boot"/> points
/// the main viewport at the interface and builds the one VR seat's <see cref="XrRig"/>: an
/// <see cref="XROrigin3D"/> with the tracked <see cref="XRCamera3D"/> under it. Each frame it has
/// the rig place itself, whichever interface draws, and recenters when the runtime asks. It logs
/// the boot, the OpenXR session states, and a frame-pacing line once a wall second.
/// </summary>
public partial class XrSession : Node
{
    // Runs after every writer of the flat camera, the cutscene director's 1000 included, so the
    // origin lands on the pose this frame draws.
    private const int AfterCameraWriters = 2000;

    private readonly XrFrameWindow _window = new();
    private XRInterface _xr = null!;
    private MeasuredRenderTime _renderTime = null!;
    private string _thread = "";
    // Why a recenter waits for the head's first tracked pose, or null when none waits.
    private string? _recenterPending;
    private long _lastStamp;
    private double _gpuSumMs;
    private int _gpuFrames;
    // The seat's position at the last frames line, null before the first.
    private Vector3? _lastPos;

    /// <summary>The one VR seat: the origin, the tracked head and the pane it flies.</summary>
    public XrRig Rig { get; private set; } = null!;

    /// <summary>True when a headset runtime paces the frames, so the desktop V-Sync and cap stay
    /// off (<see cref="VSyncSetting.Headset"/>). The stand-in has no runtime and keeps the flat
    /// game's pacing.</summary>
    public bool PacesFrames => _xr is OpenXRInterface;

    /// <summary>Starts the headset path if Godot brought OpenXR up, else the stand-in headset when
    /// <paramref name="sim"/> asks for it, else logs why not and returns null. The persistent flat
    /// camera <paramref name="eye"/> is the origin's fallback while no session binds a pane to
    /// <see cref="Rig"/>. The caller adds the returned node to the tree.</summary>
    public static XrSession? Boot(Viewport root, Camera3D eye, MeasuredRenderTime renderTime, XrSimSpec? sim)
    {
        bool requested = XrLaunch.Requested(System.Environment.GetCommandLineArgs(),
            ProjectSettings.GetSetting(XrLaunch.EnabledSetting).AsBool());
        if (XRServer.FindInterface("OpenXR") is OpenXRInterface xr && xr.IsInitialized())
        {
            if (sim != null)
            {
                Log.Warn("core", $"xr: OpenXR is live, ignoring --xr-sim");
            }

            return BootOpenXr(root, eye, renderTime, xr);
        }

        if (requested)
        {
            Log.Warn("core", $"xr: OpenXR was requested but did not start, flying flat; the runtime's own reason is in Godot's engine log");
        }

        if (sim != null)
        {
            return BootSim(root, eye, renderTime, sim);
        }

        if (!requested)
        {
            Log.Debug("core", $"xr: off (no --xr-mode on, {XrLaunch.EnabledSetting}=false, no --xr-sim)");
        }

        return null;
    }

    public override void _Process(double delta)
    {
        if (_recenterPending is { } why && HeadTracked())
        {
            _recenterPending = null;
            Rig.Recenter(why);
        }

        Rig.Place();

        var (_, gpuMs) = _renderTime.Read();
        _gpuSumMs += gpuMs;
        _gpuFrames++;

        long stamp = Stopwatch.GetTimestamp();
        if (_lastStamp != 0
            && _window.Add((stamp - _lastStamp) * 1000.0 / Stopwatch.Frequency))
        {
            var s = _window.Last;
            // The seat's place and speed put a reported stereo symptom against the map edge. A high
            // main_max_ms with a low gpu_ms is a main-thread stall.
            var pos = Rig.Origin.GlobalPosition;
            double mps = _lastPos is { } was ? pos.DistanceTo(was) * 1000.0 / Math.Max(1.0, s.Frames * s.MeanMs) : 0.0;
            _lastPos = pos;
            double mainMaxMs = 1000.0 * Performance.GetMonitor(Performance.Monitor.TimeProcess);
            // ⚠ Keep this at Debug: a console line costs about 2 ms on the frame path. That would
            // be a late frame of its own every second (docs/verification.md PERF-35).
            Log.Debug("perf", $"xr frames n={s.Frames} mean_ms={s.MeanMs:0.00} max_ms={s.MaxMs:0.00} late={s.Late} missed={s.Missed} refresh_hz={s.RefreshHz:0.#} gpu_ms={_gpuSumMs / Math.Max(1, _gpuFrames):0.00} main_max_ms={mainMaxMs:0.0} render_thread={_thread} pos=({pos.X:0},{pos.Y:0},{pos.Z:0}) mps={mps:0}");
            _gpuSumMs = 0.0;
            _gpuFrames = 0;
            if (_xr is OpenXRInterface openXr)
            {
                _window.SetRefreshHz(openXr.DisplayRefreshRate);
            }
        }

        _lastStamp = stamp;
    }

    // Whether the runtime reports the head as tracked, which is when a recenter has a pose to take.
    private static bool HeadTracked() =>
        XRServer.GetTracker("head") is XRPositionalTracker head
        && head.HasPose("default") && head.GetPose("default").HasTrackingData;

    private static XrSession BootOpenXr(Viewport root, Camera3D eye, MeasuredRenderTime renderTime, OpenXRInterface xr)
    {
        var session = Build(root, eye, renderTime, xr);
        VSyncSetting.Apply(VSyncSetting.Headset);
        session._window.SetRefreshHz(xr.DisplayRefreshRate);
        session.WireSessionLog(xr);

        var info = xr.GetSystemInfo();
        Vector2 eyePx = xr.GetRenderTargetSize();
        Log.Info("core", $"xr: OpenXR live runtime=\"{Field(info, "XRRuntimeName")}\" version={Field(info, "XRRuntimeVersion")} system=\"{Field(info, "OpenXRSystemName")}\" eye_px={eyePx.X:0}x{eyePx.Y:0} refresh_hz={xr.DisplayRefreshRate:0.#} render_thread={session._thread}");
        return session;
    }

    private static XrSession? BootSim(Viewport root, Camera3D eye, MeasuredRenderTime renderTime, XrSimSpec sim)
    {
        // ⚠ Do not turn UseXR on without the tag. The stereo render then has no multiview shader
        // variants to draw with, and Godot compiles them only at engine start.
        if (!OS.HasFeature(XrLaunch.SimFeature))
        {
            Log.Error("core", $"xr: --xr-sim needs Godot's multiview shader variants, which only the {XrLaunch.SimFeature} feature tag turns on at engine start; launch through RunTests.ps1, RunProbe.ps1 or RunGame.ps1, which set {XrLaunch.FeatureVariable} for it. Flying flat");
            return null;
        }

        var xr = new XrSimInterface(sim);
        XRServer.AddInterface(xr);
        if (!xr.Initialize())
        {
            Log.Error("core", $"xr: the --xr-sim interface did not initialise, flying flat");
            XRServer.RemoveInterface(xr);
            return null;
        }

        var session = Build(root, eye, renderTime, xr);
        var inv = CultureInfo.InvariantCulture;
        string head = string.Format(inv, "{0:0.###}/{1:0.###}/{2:0.###}", sim.Head.X, sim.Head.Y, sim.Head.Z);
        Log.Info("core", $"xr: sim live eye_px={sim.EyePx.X}x{sim.EyePx.Y} fov_deg={sim.FovDeg.ToString("0.##", inv)} ipd_mm={sim.IpdMm.ToString("0.##", inv)} yaw_deg={sim.YawDeg.ToString("0.##", inv)} pitch_deg={sim.PitchDeg.ToString("0.##", inv)} head_m={head} shot={sim.Shot.ToString().ToLowerInvariant()} render_thread={session._thread}");
        return session;
    }

    // The nodes and the main viewport's switch every interface shares.
    private static XrSession Build(Viewport root, Camera3D eye, MeasuredRenderTime renderTime, XRInterface xr)
    {
        var session = new XrSession
        {
            Name = "xr_session",
            ProcessPriority = AfterCameraWriters,
            _xr = xr,
            _renderTime = renderTime,
            // On the main thread this is true only when the render runs on it too.
            _thread = RenderingServer.IsOnRenderThread() ? "safe" : "separate",
        };
        var origin = new XROrigin3D { Name = "xr_origin" };
        var head = new XRCamera3D { Name = "xr_head", Current = false };
        origin.AddChild(head);
        session.AddChild(origin);
        session.Rig = new XrRig(origin, head, eye, pilot => pilot.Dressing.Stereo);
        // After the headset's own interface, so its pre-render reads the eyes that one just located.
        var feed = new XrEyeFeed();
        XRServer.AddInterface(feed);
        feed.Initialize();
        XrCockpit.DeclareEyeFeed();
        root.UseXR = true;
        // From here the hand controllers read, and the rebinding screens list them.
        Bindings.XrControllers.SessionLive = true;
        return session;
    }

    private static string Field(Godot.Collections.Dictionary info, string key) =>
        info.ContainsKey(key) ? info[key].AsString() : "?";

    // Besides the log, the two moments the seated eye goes back onto the cockpit eye by itself. On
    // focus, Local space has left the head wherever the runtime put it. The runtime's own recenter
    // moves Local space under the frame set earlier.
    private void WireSessionLog(OpenXRInterface xr)
    {
        xr.SessionBegun += () => Log.Info("core", $"xr: session begun refresh_hz={xr.DisplayRefreshRate:0.#}");
        xr.SessionVisible += () => Log.Info("core", $"xr: session visible");
        xr.SessionFocussed += () =>
        {
            Log.Info("core", $"xr: session focused");
            _recenterPending = "session focused";
        };
        xr.PoseRecentered += () => _recenterPending = "the runtime recentered";
        xr.SessionStopping += () => Log.Info("core", $"xr: session stopping");
        xr.SessionLossPending += () => Log.Warn("core", $"xr: session loss pending");
        xr.InstanceExiting += () => Log.Info("core", $"xr: instance exiting");
        xr.RefreshRateChanged += hz =>
        {
            _window.SetRefreshHz(hz);
            Log.Info("core", $"xr: refresh rate changed refresh_hz={hz:0.#}");
        };
    }
}
