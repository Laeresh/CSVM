using System.Collections.Generic;
using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The `--screenshot=`/`--shots=`/`--frames=` capture state machine, plus F11/F12's
/// placement print and ad-hoc save: constructed once in `_Ready` from the launch spec (a
/// --screenshot burst is a process-scoped capture, never re-armed by a menu relaunch), then
/// `Tick()`ed from the tail of `_Process`. Reads camera/orbit/rigs — passed in per call, no
/// back-reference to the host node.</summary>
public sealed class CaptureDirector
{
    // The capture still owed, taken from the spec at launch and cleared once written — a --shots=
    // burst counts down through _shotIndex and this goes null when the last frame lands.
    private string? _pendingShot;
    private int _shotDelay;            // frames still to wait before the first capture
    private int _shotIndex;            // 0-based index of the shot being written
    private Transform3D? _shotBaseXform;  // camera pose captured at the first burst frame
    private Vector3 _shotPivot;           // micro-orbit centre (keeps the subject framed)

    public CaptureDirector(SessionSpec spec)
    {
        _pendingShot = spec.ScreenshotPath;
        _shotDelay = spec.ScreenshotFrames;
    }

    /// <summary>A capture is still owed — every other `--screenshot`-conditioned display choice
    /// (HUD/panel visibility, exit-on-build-failure) reads this instead of the raw field.</summary>
    public bool Pending => _pendingShot != null;

    /// <summary>Format a vector as the "x,y,z" argument value ParseVec3 reads back (invariant
    /// culture, trimmed to 3 decimals).</summary>
    public static string Vec3Arg(Vector3 v) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0:0.###},{1:0.###},{2:0.###}", v.X, v.Y, v.Z);

    /// <summary>Same, for a direction — normalized, and finer, since a unit vector's components
    /// are small enough that 3 decimals would quantise the aim to ~0.03°.</summary>
    public static string DirArg(Vector3 v) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0:0.#####},{1:0.#####},{2:0.#####}", v.X, v.Y, v.Z);

    /// <summary>Save the current frame to a timestamped PNG under the repo's Screenshots/
    /// folder (git-ignored — rendered frames are game-derived). Bound to F12 in both the
    /// orbit viewer and free flight; the full viewport is captured, HUD overlay included.</summary>
    public static void SaveScreenshot(Viewport viewport)
    {
        var projectDir = ProjectSettings.GlobalizePath("res://");
        var dir = Path.GetFullPath(Path.Combine(projectDir, "..", "Screenshots"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"crimsonskies_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.png");
        var img = viewport.GetTexture().GetImage();
        var err = img.SavePng(path);
        if (err == Error.Ok)
        {
            GD.Print($"screenshot saved: {path}");
        }
        else
        {
            GD.PrintErr($"screenshot failed ({err}): {path}");
        }
    }

    /// <summary>The capture block at the tail of `_Process`. Nothing built yet: only shoot once a
    /// session's plane exists — unless the launchscreen is up (--menu --screenshot captures the
    /// menu itself for layout verification).</summary>
    public void Tick(Viewport viewport, SceneTree tree, SessionSpec spec, GameClock? clock,
        OrbitCamera orbit, Camera3D camera, Node3D? plane, bool menuVisible)
    {
        if (_pendingShot == null)
        {
            return;
        }
        if (plane == null && !menuVisible)
        {
            return;
        }
        if (--_shotDelay > 0)             // still counting down the warm-up delay
        {
            return;
        }
        // Delay elapsed: grab one frame per _Process call for spec.ScreenshotShots frames,
        // then quit. A single shot keeps the original path verbatim; a burst (for z-fight
        // debugging, where flicker only shows across frames) writes indexed files. The
        // captured image is the PREVIOUS frame's render, so file _00 is the un-jittered
        // baseline and _01.. carry the dither applied below — all distinct, which is all
        // the flip-through needs.
        var img = viewport.GetTexture().GetImage();
        if (img == null)
        {
            // Backstop for any renderer-less path the arg-parse guard in Launcher._Ready doesn't
            // catch: quit loudly instead of NRE-looping forever on a null image every frame.
            Log.Error("core", $"screenshot capture failed: no image from the viewport (no GPU context?)");
            tree.Quit(1);
            return;
        }
        var path = spec.ScreenshotShots > 1 ? IndexedShotPath(_pendingShot, _shotIndex) : _pendingShot;
        var saveErr = img.SavePng(path);
        // The sim frame is part of what the capture IS: under the fixed clock one rendered frame is
        // exactly one sim step, so this number pins the moment the shot shows.
        long simFrame = clock?.Frame ?? 0;
        double simTime = clock?.Time ?? 0.0;
        if (saveErr == Error.Ok)
        {
            Log.Info("core", $"screenshot saved: {path} sim_frame={simFrame} sim_time={simTime:0.###}");
        }
        else
        {
            // A missing parent directory fails SavePng silently — say so instead of "saved".
            Log.Error("core", $"screenshot save FAILED ({saveErr}): {path} sim_frame={simFrame}");
        }
        // The golden-image tripwire's whole input: a hash of the RAW pixels (never the PNG, whose
        // encoded bytes differ between identical images), the size that hash is only valid at, and
        // the adapter that drew it. Emitted on every capture so any shot can become a golden.
        Log.Info("core", $"shot pixmd5={GoldenShot.PixelHash(img)} size={img.GetWidth()}x{img.GetHeight()} gpu={GoldenShot.Adapter()}");
        // No-op unless --tex-census: reads the frame just saved back as per-texture pixel counts.
        TextureDropIn.CountShot(img, path);
        if (++_shotIndex >= spec.ScreenshotShots)
        {
            _pendingShot = null;
            tree.Quit();
            return;
        }
        if (spec.JitterDeg > 0f && !spec.Fly)
        {
            ApplyShotJitter(orbit, camera, spec);
        }
    }

    /// <summary>Print the mode's SUBJECT placement as ready-to-paste arguments (F11, any mode) —
    /// the same pair that placed it, so a pose found by hand reproduces in a deterministic
    /// --screenshot run. In flight that subject is the PLANE (player 1's position and nose), not
    /// the chase camera, because that is what --pos/--direction place there. The orbit view prints
    /// --lookat rather than --direction: its framed point is a pivot, and only the point
    /// reproduces the orbit radius as well as the angle.</summary>
    public void PrintPlacement(SessionSpec spec, List<PlayerRig> rigs, Camera3D camera, OrbitCamera orbit)
    {
        if (spec.Fly && rigs.Count > 0 && rigs[0].Controller is { } controller)
        {
            var xform = controller.GlobalTransform;
            Log.Info("core", $"placement: --pos=\"{Vec3Arg(xform.Origin)}\" --direction=\"{DirArg(-xform.Basis.Z)}\"");
            return;
        }
        var pos = camera.GlobalPosition;
        if (spec.Freecam || spec.AnimLab || spec.Fly)
        {
            Log.Info("core", $"placement: --pos=\"{Vec3Arg(pos)}\" --direction=\"{DirArg(-camera.GlobalTransform.Basis.Z)}\"");
            return;
        }
        Log.Info("core", $"placement: --pos=\"{Vec3Arg(pos)}\" --lookat=\"{Vec3Arg(orbit.OrbitCenter)}\"");
    }

    // Insert a zero-padded frame index before the extension:
    // foo.png -> foo_00.png. Used for --shots=N burst capture.
    private static string IndexedShotPath(string path, int index)
    {
        var dir = Path.GetDirectoryName(path) ?? "";
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        return Path.Combine(dir, $"{stem}_{index:D2}{ext}");
    }

    // Rotate the burst camera a hair around the framed point each --shots frame so
    // coplanar surfaces re-decide the depth test and z-fighting flicker surfaces across the
    // sequence (a dead-still camera can render bit-identical frames). The eye micro-orbits
    // the pivot — depths change, but the camera keeps looking at the pivot so the subject
    // stays centred. Static mode only: in --fly the FlightController owns the camera each
    // frame (and the plane's own motion already surfaces the fight).
    private void ApplyShotJitter(OrbitCamera orbit, Camera3D camera, SessionSpec spec)
    {
        if (_shotBaseXform is not { } baseX)
        {
            baseX = camera.GlobalTransform;
            _shotBaseXform = baseX;
            _shotPivot = orbit.OrbitCenter;   // the point the orbit camera aims at
        }
        // Golden-angle spread so consecutive frames differ maximally.
        float mag = Mathf.DegToRad(spec.JitterDeg);
        float phase = _shotIndex * 2.399963f;
        var rot = new Basis(Vector3.Up, mag * Mathf.Cos(phase))
                * new Basis(baseX.Basis.X.Normalized(), mag * Mathf.Sin(phase));
        // Rigidly rotate the whole camera about the pivot: rotating both the eye offset and
        // the basis by the same rotation preserves the aim exactly, so framing is kept.
        var origin = _shotPivot + rot * (baseX.Origin - _shotPivot);
        camera.GlobalTransform = new Transform3D(rot * baseX.Basis, origin);
    }
}
