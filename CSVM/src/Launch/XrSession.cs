using System;
using System.Collections.Generic;
using System.Diagnostics;
using CSVM.Flight.Camera;
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
/// The OpenXR path of one process, built only when Godot started OpenXR (<c>--xr-mode on</c> ahead
/// of <c>--</c>); otherwise nothing is built. Its <see cref="Boot"/> points the main viewport at the
/// headset and hangs an <see cref="XROrigin3D"/> with the tracked <see cref="XRCamera3D"/> under it.
/// Each frame the origin moves to the flown aircraft's eye (<see cref="XrMount"/>). It logs the
/// runtime at boot, its session states, and a frame-pacing line once a wall second. The flat camera
/// stays current, so its near and far planes, Environment and zone cull mask serve the stereo render
/// too. Godot takes only the eye poses from the origin and the headset.
/// </summary>
public partial class XrSession : Node
{
    // Runs after every writer of the flat camera, the cutscene director's 1000 included, so the
    // origin lands on the pose this frame draws.
    private const int AfterCameraWriters = 2000;

    private readonly XrFrameWindow _window = new();
    private OpenXRInterface _xr = null!;
    private Camera3D _eye = null!;
    private Func<IReadOnlyList<PlayerRig>> _rigs = null!;
    private MeasuredRenderTime _renderTime = null!;
    private XROrigin3D _origin = null!;
    private string _thread = "";
    private long _lastStamp;
    private double _gpuSumMs;
    private int _gpuFrames;

    /// <summary>The tracked head under the origin, where the player's eyes are in the world. Not the
    /// current camera; see the type's summary.</summary>
    public XRCamera3D Head { get; private set; } = null!;

    /// <summary>Starts the headset path if Godot brought OpenXR up, else logs why not and returns
    /// null. The persistent flat camera <paramref name="eye"/> is the origin's fallback while no
    /// flight is live. The live session's panes come from <paramref name="rigs"/>, empty at the
    /// menu. The caller adds the returned node to the tree.</summary>
    public static XrSession? Boot(Viewport root, Camera3D eye, Func<IReadOnlyList<PlayerRig>> rigs,
        MeasuredRenderTime renderTime)
    {
        bool requested = XrLaunch.Requested(System.Environment.GetCommandLineArgs(),
            ProjectSettings.GetSetting(XrLaunch.EnabledSetting).AsBool());
        if (XRServer.FindInterface("OpenXR") is not OpenXRInterface xr || !xr.IsInitialized())
        {
            if (requested)
            {
                Log.Warn("core", $"xr: OpenXR was requested but did not start, flying flat; the runtime's own reason is in Godot's engine log");
            }
            else
            {
                Log.Debug("core", $"xr: off (no --xr-mode on, {XrLaunch.EnabledSetting}=false)");
            }

            return null;
        }

        var session = new XrSession
        {
            Name = "xr_session",
            ProcessPriority = AfterCameraWriters,
            _xr = xr,
            _eye = eye,
            _rigs = rigs,
            _renderTime = renderTime,
            // On the main thread this is true only when the render runs on it too.
            _thread = RenderingServer.IsOnRenderThread() ? "safe" : "separate",
        };
        session._origin = new XROrigin3D { Name = "xr_origin" };
        session.Head = new XRCamera3D { Name = "xr_head", Current = false };
        session._origin.AddChild(session.Head);
        session.AddChild(session._origin);

        root.UseXR = true;
        VSyncSetting.Apply(VSyncSetting.Headset);
        session._window.SetRefreshHz(xr.DisplayRefreshRate);
        session.WireSessionLog();

        var info = xr.GetSystemInfo();
        Vector2 eyePx = xr.GetRenderTargetSize();
        Log.Info("core", $"xr: OpenXR live runtime=\"{Field(info, "XRRuntimeName")}\" version={Field(info, "XRRuntimeVersion")} system=\"{Field(info, "OpenXRSystemName")}\" eye_px={eyePx.X:0}x{eyePx.Y:0} refresh_hz={xr.DisplayRefreshRate:0.#} render_thread={session._thread}");
        return session;
    }

    public override void _Process(double delta)
    {
        _origin.GlobalTransform = CurrentMount();

        var (_, gpuMs) = _renderTime.Read();
        _gpuSumMs += gpuMs;
        _gpuFrames++;

        long stamp = Stopwatch.GetTimestamp();
        if (_lastStamp != 0
            && _window.Add((stamp - _lastStamp) * 1000.0 / Stopwatch.Frequency))
        {
            var s = _window.Last;
            // ⚠ Keep this at Debug: a console line costs about 2 ms on the frame path. That would
            // be a late frame of its own every second (docs/verification.md PERF-35).
            Log.Debug("perf", $"xr frames n={s.Frames} mean_ms={s.MeanMs:0.00} max_ms={s.MaxMs:0.00} late={s.Late} missed={s.Missed} refresh_hz={s.RefreshHz:0.#} gpu_ms={_gpuSumMs / Math.Max(1, _gpuFrames):0.00} render_thread={_thread}");
            _gpuSumMs = 0.0;
            _gpuFrames = 0;
            _window.SetRefreshHz(_xr.DisplayRefreshRate);
        }

        _lastStamp = stamp;
    }

    private static string Field(Godot.Collections.Dictionary info, string key) =>
        info.ContainsKey(key) ? info[key].AsString() : "?";

    // The first pane's flat camera and aircraft; the persistent camera while no flight is up.
    private Transform3D CurrentMount()
    {
        var rigs = _rigs();
        var rig = rigs.Count > 0 ? rigs[0] : null;
        var camera = rig != null && IsInstanceValid(rig.Camera) ? rig.Camera : _eye;
        Basis? attitude = rig?.Controller is { FirstPersonView: true, Crashed: false } pilot && IsInstanceValid(pilot)
            ? pilot.GlobalTransform.Basis
            : null;
        return XrMount.For(camera.GlobalTransform, attitude);
    }

    private void WireSessionLog()
    {
        _xr.SessionBegun += () => Log.Info("core", $"xr: session begun refresh_hz={_xr.DisplayRefreshRate:0.#}");
        _xr.SessionVisible += () => Log.Info("core", $"xr: session visible");
        _xr.SessionFocussed += () => Log.Info("core", $"xr: session focused");
        _xr.SessionStopping += () => Log.Info("core", $"xr: session stopping");
        _xr.SessionLossPending += () => Log.Warn("core", $"xr: session loss pending");
        _xr.InstanceExiting += () => Log.Info("core", $"xr: instance exiting");
        _xr.RefreshRateChanged += hz =>
        {
            _window.SetRefreshHz(hz);
            Log.Info("core", $"xr: refresh rate changed refresh_hz={hz:0.#}");
        };
    }
}
