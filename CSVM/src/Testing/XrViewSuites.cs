using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CSVM.Bindings;
using CSVM.Extraction;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Launch;
using CSVM.Mech3;
using CSVM.Session.Roster;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The headset's views flown through a real human rig on the <c>--xr-sim</c> stand-in
/// interface. The VR seat's <see cref="XrRig"/> is placed after every frame, as the session places
/// it. Each origin is read in the aircraft's own frame over a banked turn, so the flight drops out.
/// Module entries: docs/architecture/Flight.md on XrMount, XrRig and CameraController.</summary>
internal static class XrViewSuites
{
    private const float StepDt = 1f / 60f;

    // Roll right for a second, then pull: a banked, climbing turn that moves every attitude axis.
    private const string TurnHold = "0,0.6,0,0.7@1;0.8,0,0,0.7";
    private const int TurnFrames = 240;

    // Two millimetres and a hundredth of a degree. Float32 world coordinates near 7 km step by half a
    // millimetre, so a pose riding the aircraft rigidly reads well inside both.
    private const float PoseTolM = 0.002f, PoseTolDeg = 0.01f;

    // A rotated pose far from the chapter origin, so no basis product is an exact 0 or 1.
    private static readonly Vector3 StartAt = new(-6350f, 600f, -4144f);
    private static readonly Vector3 StartDir = new(-0.743f, 0f, -0.669f);

    private static CultureInfo Inv => CultureInfo.InvariantCulture;

    /// <summary>The seated eye in Cockpit and in Nose over a banked turn. Then the view cycle walks
    /// the three views with a cut on each step.</summary>
    [Suite("xr-view-first-person",
        "under a headset the Cockpit and Nose origins stand on the cockpit_camera marker with the aircraft's own axes on every frame of a banked climbing turn, the Nose hides the interior, and the view cycle walks Cockpit, Nose, Chase and back with one cut on each press, the anchor following")]
    internal static void XrViewFirstPerson(TestContext ctx)
    {
        var report = new StringBuilder();
        foreach (var mode in new[] { PilotViewMode.Cockpit, PilotViewMode.Nose })
        {
            string view = PilotView.Name(mode);
            bool flown = WithHeadsetFlight(ctx, mode, TurnHold, XrChase.Rigid, (clock, plane, rig, marker) =>
            {
                var worst = Ride(clock, plane, rig, TurnFrames, new Transform3D(Basis.Identity, marker), out var moved);
                report.AppendLine($"{view}: worst {worst.Metres * 1000f:0.###} mm {worst.Degrees:0.####} deg off the marker over a turn of {moved:0.#} deg, anchor {rig.Anchor}");
                ctx.Check(moved > 30f, $"{view}: the hold turns the aircraft through {moved:0.#}°, so a frame that lags would show");
                ctx.Check(rig.Anchor == XrAnchor.Seat, $"{view}: the origin takes the seat (read {rig.Anchor})");
                ctx.Check(worst.Metres < PoseTolM && worst.Degrees < PoseTolDeg,
                    $"{view}: the origin stays on the marker with the aircraft's axes on every frame ({worst.Metres * 1000f:0.###} mm, {worst.Degrees:0.####}° worst)");
                if (mode == PilotViewMode.Nose)
                {
                    bool shown = plane.Dressing.Interior is { } interior && interior.IsVisibleInTree();
                    ctx.Check(!shown, $"nose: the interior is hidden");
                    return;
                }

                CycleSortie(ctx, clock, plane, rig, report);
            });
            ctx.Check(flown, $"{view}: the view builds an aircraft to fly");
        }

        ctx.WriteArtifact("test-xr-view-first-person.txt", report.ToString());
    }

    /// <summary>The rigid chase over the same turn, against the flat chase's lag as the control,
    /// and the offset every airframe's camera block gives it.</summary>
    [Suite("xr-view-chase",
        "under a headset the chase origin rides a fixed offset behind and above the aircraft in its own axes with zero lag over a banked climbing turn, where the flat chase on the same turn trails by its eased frames; the level variant keeps world up; every airframe's offset puts it a comfortable distance below the level gaze")]
    internal static void XrViewChase(TestContext ctx)
    {
        var report = new StringBuilder();
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var offset = CameraController.VrChaseOffset(CamParams.Load(ctx.ZrdrPath, ctx.PlaneName));
        var expected = new Transform3D(Basis.Identity, offset);
        bool flown = WithHeadsetFlight(ctx, PilotViewMode.Chase, TurnHold, XrChase.Rigid, (clock, plane, rig, _) =>
        {
            var worst = Ride(clock, plane, rig, TurnFrames, expected, out var moved);
            report.AppendLine($"rigid: worst {worst.Metres * 1000f:0.###} mm {worst.Degrees:0.####} deg off the fixed offset over a turn of {moved:0.#} deg");
            ctx.Check(moved > 30f, $"the hold turns the aircraft through {moved:0.#}°");
            ctx.Check(rig.Anchor == XrAnchor.Camera, $"the chase origin takes the rigid camera pose (read {rig.Anchor})");
            ctx.Check(worst.Metres < PoseTolM && worst.Degrees < PoseTolDeg,
                $"the rigid chase holds its offset on every frame, zero lag ({worst.Metres * 1000f:0.###} mm, {worst.Degrees:0.####}° worst)");
            var gaze = -rig.Head.GlobalBasis.Z;
            var toPlane = (plane.GlobalPosition - rig.Head.GlobalPosition).Normalized();
            float belowDeg = Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(-toPlane.Dot(plane.GlobalBasis.Y), -1f, 1f)));
            ctx.Check(Mathf.RadToDeg(gaze.AngleTo(toPlane)) < 15f && belowDeg > 3f,
                $"at the default seated head the aircraft sits {belowDeg:0.##}° below the gaze, {plane.GlobalPosition.DistanceTo(rig.Head.GlobalPosition):0.##} m out");
        });
        ctx.Check(flown, $"rigid: the chase builds an aircraft to fly");

        flown = WithHeadsetFlight(ctx, PilotViewMode.Chase, TurnHold, null, (clock, plane, _, _) =>
        {
            var lag = FlatLag(clock, plane, ctx.Camera, TurnFrames);
            report.AppendLine($"flat control: the chase camera leaves its settled pose by up to {lag.Metres:0.###} m {lag.Degrees:0.###} deg over the same turn");
            ctx.Check(lag.Metres > 0.5f || lag.Degrees > 1f,
                $"control: the flat chase on the same turn trails its settled pose ({lag.Metres:0.###} m, {lag.Degrees:0.###}°), so the zero above is the rigid law's");
        });
        ctx.Check(flown, $"control: the flat chase builds an aircraft to fly");

        flown = WithHeadsetFlight(ctx, PilotViewMode.Chase, TurnHold, XrChase.Level, (clock, plane, rig, _) =>
        {
            float worstTilt = 0f, worstHeight = 0f;
            for (int i = 0; i < TurnFrames; i++)
            {
                Step(clock, plane, rig);
                worstTilt = Mathf.Max(worstTilt, Mathf.RadToDeg(rig.Origin.GlobalBasis.Y.AngleTo(Vector3.Up)));
                worstHeight = Mathf.Max(worstHeight, Mathf.Abs(rig.Origin.GlobalPosition.Y - plane.GlobalPosition.Y - offset.Y));
            }

            report.AppendLine($"level: worst tilt {worstTilt:0.####} deg, height off {worstHeight * 1000f:0.###} mm");
            ctx.Check(worstTilt < PoseTolDeg && worstHeight < PoseTolM,
                $"the level chase keeps world up and its height over the turn ({worstTilt:0.####}°, {worstHeight * 1000f:0.###} mm)");
        });
        ctx.Check(flown, $"level: the chase builds an aircraft to fly");

        foreach (var (model, display) in MarkerRig.PlayerAirframes)
        {
            var each = CameraController.VrChaseOffset(CamParams.Load(ctx.ZrdrPath, model));
            float below = Mathf.RadToDeg(Mathf.Atan2(each.Y, each.Z));
            report.AppendLine(string.Format(Inv, "{0} ({1}): {2:0.##} m behind, {3:0.##} m up, {4:0.##} m out, {5:0.##} deg below the level gaze",
                display, model, each.Z, each.Y, each.Length(), below));
            ctx.Check(below > 4f && below < 15f && each.Length() > 8f,
                $"{display}: the chase sits {below:0.##}° below the level gaze, {each.Length():0.##} m out");
        }

        ctx.WriteArtifact("test-xr-view-chase.txt", report.ToString());
    }

    /// <summary>Every head-look input held over a flight in each view leaves the origin where it
    /// was. The flat rig is the control: the same inputs swing its camera.</summary>
    [Suite("xr-view-head-look",
        "under a headset the numpad snap, the look stick, free-look, the centre key, look-behind and the padlock key change neither the origin nor the head in Cockpit, Nose or Chase, while the flat rig's camera swings on the same inputs")]
    internal static void XrViewHeadLook(TestContext ctx)
    {
        var report = new StringBuilder();
        foreach (var mode in new[] { PilotViewMode.Cockpit, PilotViewMode.Nose, PilotViewMode.Chase })
        {
            string view = PilotView.Name(mode);
            bool flown = WithHeadsetFlight(ctx, mode, "0,0,0,0.6", XrChase.Rigid, (clock, plane, rig, _) =>
            {
                Settle(clock, plane, rig);
                var before = InPlane(rig.Origin.GlobalTransform, plane);
                var worst = (Metres: 0f, Degrees: 0f);
                Look(plane, true);
                for (int i = 0; i < 90; i++)
                {
                    Step(clock, plane, rig);
                    worst = Worse(worst, InPlane(rig.Origin.GlobalTransform, plane), before);
                }

                var head = plane.Head;
                report.AppendLine($"{view}: inputs held 90 frames, origin moved {worst.Metres * 1000f:0.###} mm {worst.Degrees:0.####} deg, head az {head?.Azimuth:0.####} el {head?.Elevation:0.####} mode {head?.Mode}");
                ctx.Check(worst.Metres < PoseTolM && worst.Degrees < PoseTolDeg,
                    $"{view}: every look input held leaves the origin put ({worst.Metres * 1000f:0.###} mm, {worst.Degrees:0.####}°)");
                ctx.Check(head is { Azimuth: 0f, Elevation: 0f, Mode: LookMode.Snap },
                    $"{view}: the head stays level, forward and in snap ({head?.Azimuth:0.####}, {head?.Elevation:0.####}, {head?.Mode})");
                Look(plane, false);

                // The control: the same inputs on the flat rig move the camera.
                plane.Headset = null;
                Settle(clock, plane, rig);
                var flatBefore = InPlane(ctx.Camera.GlobalTransform, plane);
                var flat = (Metres: 0f, Degrees: 0f);
                Look(plane, true, all: false);
                for (int i = 0; i < 90; i++)
                {
                    Step(clock, plane, rig);
                    flat = Worse(flat, InPlane(ctx.Camera.GlobalTransform, plane), flatBefore);
                }

                Look(plane, false);
                plane.Headset = XrChase.Rigid;
                report.AppendLine($"{view} flat control: camera moved {flat.Metres:0.###} m {flat.Degrees:0.###} deg");
                ctx.Check(flat.Degrees > 5f, $"{view}: control, the flat rig's camera turns {flat.Degrees:0.#}° on the same inputs");
            });
            ctx.Check(flown, $"{view}: the view builds an aircraft to fly");
        }

        ctx.WriteArtifact("test-xr-view-head-look.txt", report.ToString());
    }

    /// <summary>The cameras outside the three views under a headset. The flyby and the static cuts
    /// hold a level heading from their cut, and a camera another owner drives is taken by its
    /// heading. A headset death returns to the opening view.</summary>
    [Suite("xr-view-static",
        "under a headset each flyby spot sits behind the aircraft and cuts to a level heading facing it, then holds its spot and that heading while the aircraft flies away, for the drawn watch time plus a second, cutting once per re-site; the crash and death cuts hold a level heading where the flat camera re-aims; a camera another owner drives is taken by its heading alone, with a cut each way; and a headset death returns to the opening view where the flat one returns to chase")]
    internal static void XrViewStatic(TestContext ctx)
    {
        var report = new StringBuilder();
        bool flown = WithHeadsetFlight(ctx, PilotViewMode.Cockpit, "0,0,0,0.7", XrChase.Rigid, (clock, plane, rig, _) =>
        {
            FlybySortie(ctx, clock, plane, rig, report);
            OwnedSortie(ctx, clock, plane, rig, report);
        });
        ctx.Check(flown, $"the flyby builds an aircraft to fly");
        StaticCutSortie(ctx, report);
        ctx.WriteArtifact("test-xr-view-static.txt", report.ToString());
    }

    /// <summary>A presenting cutscene under a headset. The origin rides the authored camera's eye on
    /// a held level heading, since the cutscene plays on the theater screen.</summary>
    [Suite("xr-view-cutscene",
        "under a headset a presenting cutscene's camera stands the origin at its eye on the level heading the seat had as the cutscene took the camera, held to 0.01 degree while the authored camera turns and travels through three shots, with one cut as it takes the camera and none on the cutscene's own shots; another owner's camera with no cutscene is still taken by its heading, the control; and the hand-back returns the seat")]
    internal static void XrViewCutscene(TestContext ctx)
    {
        var report = new StringBuilder();
        bool flown = WithHeadsetFlight(ctx, PilotViewMode.Cockpit, TurnHold, XrChase.Rigid, (clock, plane, rig, _) =>
        {
            var cuts = new List<string>();
            void OnCut(string why) => cuts.Add(why);
            bool cutscene = true;
            rig.CutscenePlays = () => cutscene;
            rig.Cut += OnCut;
            try
            {
                Settle(clock, plane, rig);
                cuts.Clear();
                var seatHeading = XrMount.Level(rig.Origin.GlobalBasis);
                var shots = new[]
                {
                    new Transform3D(new Basis(Vector3.Up, 0.9f) * new Basis(Vector3.Right, -0.5f) * new Basis(Vector3.Back, 0.4f), new Vector3(40f, 15f, -30f)),
                    new Transform3D(new Basis(Vector3.Up, -1.7f) * new Basis(Vector3.Right, 0.3f), new Vector3(-60f, 5f, 20f)),
                    new Transform3D(new Basis(Vector3.Up, 2.8f) * new Basis(Vector3.Back, -0.7f), new Vector3(10f, 60f, 90f)),
                };
                plane.CameraOwned = true;
                float worstDeg = 0f, worstM = 0f;
                foreach (var shot in shots)
                {
                    var pose = new Transform3D(shot.Basis, plane.GlobalPosition + shot.Origin);
                    ctx.Camera.GlobalTransform = pose;
                    Step(clock, plane, rig);
                    worstDeg = Mathf.Max(worstDeg, TurnDeg(seatHeading, rig.Origin.GlobalBasis));
                    worstM = Mathf.Max(worstM, rig.Origin.GlobalPosition.DistanceTo(pose.Origin));
                }

                report.AppendLine($"cutscene: screening={rig.Screening}, heading off the seat's by {worstDeg:0.####} deg, eye off the camera by {worstM * 1000f:0.###} mm, cuts {string.Join("; ", cuts)}");
                ctx.Check(rig.Screening, $"the origin stands for the theater screen while the cutscene presents");
                ctx.Check(worstDeg < PoseTolDeg && worstM < PoseTolM,
                    $"at the authored camera's eye on the seat's level heading through every shot ({worstM * 1000f:0.###} mm, {worstDeg:0.####}° worst)");
                ctx.Check(cuts.Count == 1, $"with one cut as the cutscene takes the camera and none on its shots ({string.Join("; ", cuts)})");

                cutscene = false;
                var owned = new Transform3D(shots[1].Basis, plane.GlobalPosition + shots[1].Origin);
                ctx.Camera.GlobalTransform = owned;
                Step(clock, plane, rig);
                bool heading = rig.Origin.GlobalBasis.IsEqualApprox(XrMount.Level(owned.Basis));
                report.AppendLine($"control: screening={rig.Screening}, taken by its heading={heading}");
                ctx.Check(!rig.Screening && heading, $"control: another owner's camera with no cutscene is taken by its own heading");

                plane.CameraOwned = false;
                Step(clock, plane, rig);
                ctx.Check(!rig.Screening && rig.Anchor == XrAnchor.Seat, $"the hand-back returns the seat (read {rig.Anchor})");
            }
            finally
            {
                plane.CameraOwned = false;
                rig.Cut -= OnCut;
                rig.CutscenePlays = null;
            }
        });
        ctx.Check(flown, $"the cockpit view builds an aircraft to fly");
        ctx.WriteArtifact("test-xr-view-cutscene.txt", report.ToString());
    }

    /// <summary>One human flying <paramref name="hold"/> in <paramref name="mode"/> under the stand-in
    /// headset's VR seat, for another suite's <paramref name="sortie"/>. False when no aircraft built.</summary>
    internal static bool FlyWithHeadset(TestContext ctx, PilotViewMode mode, string hold, XrChase? headset,
        Action<GameClock, FlightController, XrRig, Vector3> sortie) =>
        WithHeadsetFlight(ctx, mode, hold, headset, sortie);

    // The cycle key three times from Cockpit: Nose, Chase, Cockpit, each press one cut.
    private static void CycleSortie(TestContext ctx, GameClock clock, FlightController plane, XrRig rig,
        StringBuilder report)
    {
        var cuts = new List<string>();
        void OnCut(string why) => cuts.Add(why);
        rig.Cut += OnCut;
        try
        {
            var walked = new List<string>();
            // Called directly. A held suite action stands on the key and the pad halves alike, and
            // each half cycles, so a press would walk two stops.
            foreach (var expect in new[] { PilotViewMode.Nose, PilotViewMode.Chase, PilotViewMode.Cockpit })
            {
                plane.CameraControl!.CycleCockpitViews();
                Step(clock, plane, rig);
                Step(clock, plane, rig);
                walked.Add($"{PilotView.Name(plane.ViewMode)}/{rig.Anchor}");
                var anchor = expect == PilotViewMode.Chase ? XrAnchor.Camera : XrAnchor.Seat;
                ctx.Check(plane.ViewMode == expect && rig.Anchor == anchor,
                    $"the cycle lands on {PilotView.Name(expect)} with the {anchor} anchor (read {PilotView.Name(plane.ViewMode)}, {rig.Anchor})");
            }

            report.AppendLine($"cycle: {string.Join(" -> ", walked)}, cuts {cuts.Count}: {string.Join("; ", cuts)}");
            ctx.Check(cuts.Count == 3, $"each of the three presses raises one cut for a fade ({cuts.Count}: {string.Join("; ", cuts)})");
        }
        finally
        {
            rig.Cut -= OnCut;
        }
    }

    // F7 into the flyby: each spot sits behind the aircraft and cuts to face it. The origin then
    // holds the spot and its level heading until the next re-site.
    private static void FlybySortie(TestContext ctx, GameClock clock, FlightController plane, XrRig rig,
        StringBuilder report)
    {
        Settle(clock, plane, rig);
        var cuts = 0;
        void OnCut(string why) => cuts++;
        rig.Cut += OnCut;
        try
        {
            int placements = plane.CameraControl?.Statics.Placements ?? 0;
            Transform3D held = rig.Origin.GlobalTransform;
            var sites = new List<(double At, float Bearing, float AwayDeg, float Metres, double Watch)>();
            int frames = 0;
            float worstTurn = 0f, worstMove = 0f, worstTilt = 0f, swept = 0f;

            // One frame read after its step: a new spot is a cut, read at once; else a held frame.
            void Read()
            {
                var now = rig.Origin.GlobalTransform;
                worstTilt = Mathf.Max(worstTilt, Mathf.RadToDeg(now.Basis.Y.AngleTo(Vector3.Up)));
                var statics = plane.CameraControl!.Statics;
                if (statics.Placements != placements)
                {
                    placements = statics.Placements;
                    held = now;
                    var away = plane.GlobalPosition - now.Origin;
                    float awayDeg = Mathf.RadToDeg(plane.WorldVelocity.AngleTo(away));
                    sites.Add((clock.Time, YawOffDeg(now, plane.GlobalPosition), awayDeg, away.Length(), statics.WatchUntil - clock.Time));
                    return;
                }

                frames++;
                worstTurn = Mathf.Max(worstTurn, TurnDeg(held.Basis, now.Basis));
                worstMove = Mathf.Max(worstMove, held.Origin.DistanceTo(now.Origin));
                swept = Mathf.Max(swept, YawOffDeg(now, plane.GlobalPosition));
            }

            plane.HoldActionForTest(InputAction.FlybyView, true);
            Step(clock, plane, rig);
            Read();
            plane.HoldActionForTest(InputAction.FlybyView, false);
            ctx.Check(plane.CameraControl?.FlybyActive == true, $"F7 enters the flyby");
            for (int i = 0; i < 1200; i++)
            {
                Step(clock, plane, rig);
                Read();
            }

            var cam = CamParams.Load(ctx.ZrdrPath, ctx.PlaneName);
            for (int k = 0; k < sites.Count; k++)
            {
                var s = sites[k];
                string interval = k + 1 < sites.Count ? $", next cut after {sites[k + 1].At - s.At:0.000} s" : "";
                report.AppendLine(string.Format(Inv, "flyby spot {0}: bearing {1:0.###} deg off the origin's forward, {2:0.#} m to the aircraft, its velocity {3:0.#} deg off the line away from the seat, watch deadline {4:0.000} s{5}",
                    k + 1, s.Bearing, s.Metres, s.AwayDeg, s.Watch, interval));
                ctx.Check(s.Bearing < 0.5f, $"spot {k + 1}: the cut turns the origin to face the aircraft ({s.Bearing:0.###}°)");
                ctx.Check(s.AwayDeg < 45f, $"spot {k + 1}: the spot is behind, the aircraft flies away from it ({s.AwayDeg:0.#}° off)");
                if (k + 1 < sites.Count)
                {
                    double gap = sites[k + 1].At - s.At;
                    ctx.Check(gap >= s.Watch - 1e-3 && gap <= s.Watch + (3 * StepDt)
                        && s.Watch >= cam.FlybyMinWatchTime + StaticCameras.VrExtraWatchS - 1e-3
                        && s.Watch <= cam.FlybyMaxWatchTime + StaticCameras.VrExtraWatchS + 1e-3,
                        $"spot {k + 1}: held {gap:0.000} s to a deadline of {s.Watch:0.000} s, the drawn {cam.FlybyMinWatchTime}..{cam.FlybyMaxWatchTime} s plus the headset's {StaticCameras.VrExtraWatchS} s");
                }
            }

            report.AppendLine($"flyby: {sites.Count} spots over 1200 frames, {cuts} cuts, held frames {frames}, worst turn {worstTurn:0.####} deg move {worstMove * 1000f:0.###} mm tilt {worstTilt:0.####} deg, the aircraft at most {swept:0.#} deg round from the held gaze");
            ctx.Check(sites.Count >= 3 && cuts >= sites.Count,
                $"the flyby re-sites ({sites.Count} spots) and each re-site raises a cut ({cuts})");
            ctx.Check(worstTurn < PoseTolDeg && worstMove < PoseTolM && worstTilt < PoseTolDeg,
                $"between re-sites the origin holds its spot and a level heading ({worstTurn:0.####}°, {worstMove * 1000f:0.###} mm, tilt {worstTilt:0.####}°)");
            ctx.Check(swept < 45f,
                $"the aircraft flies away ahead of the seat, at most {swept:0.#}° round from the held gaze");
            plane.CameraControl!.CycleCockpitViews();
            Step(clock, plane, rig);
            ctx.Check(plane.CameraControl.FlybyActive == false, $"the cycle leaves the flyby");
        }
        finally
        {
            rig.Cut -= OnCut;
        }
    }

    // A camera another owner drives (the spectator, photo mode) is taken by its heading alone.
    private static void OwnedSortie(TestContext ctx, GameClock clock, FlightController plane, XrRig rig,
        StringBuilder report)
    {
        var cuts = new List<string>();
        void OnCut(string why) => cuts.Add(why);
        rig.Cut += OnCut;
        try
        {
            Settle(clock, plane, rig);
            plane.CameraOwned = true;
            var tilted = new Transform3D(new Basis(Vector3.Up, 0.9f) * new Basis(Vector3.Right, -0.5f) * new Basis(Vector3.Back, 0.4f),
                plane.GlobalPosition + new Vector3(40f, 15f, -30f));
            ctx.Camera.GlobalTransform = tilted;
            Step(clock, plane, rig);
            var taken = rig.Origin.GlobalTransform;
            bool level = taken.Basis.IsEqualApprox(XrMount.Level(tilted.Basis)) && taken.Origin.IsEqualApprox(tilted.Origin);
            plane.CameraOwned = false;
            Step(clock, plane, rig);
            report.AppendLine($"owned: anchor level={level}, cuts {string.Join("; ", cuts)}, back on {rig.Anchor}");
            ctx.Check(level, $"a camera another owner drives is taken at its eye with its heading alone");
            ctx.Check(cuts.Count == 2 && rig.Anchor == XrAnchor.Seat,
                $"with a cut as the owner takes it and one as it hands it back to the seat ({string.Join("; ", cuts)}, {rig.Anchor})");
        }
        finally
        {
            plane.CameraOwned = false;
            rig.Cut -= OnCut;
        }
    }

    // The crash and death cuts on a bare controller, with the flat re-aim as the control. Then the
    // death reset's view under each.
    private static void StaticCutSortie(TestContext ctx, StringBuilder report)
    {
        var camera = new Camera3D { Name = "xr-view-static" };
        ctx.Host.AddChild(camera);
        try
        {
            foreach (var headset in new XrChase?[] { XrChase.Rigid, null })
            {
                string arm = headset == null ? "flat" : "headset";
                var cam = new CameraController(camera, new CamParams(), _ => false, 0, PilotViewMode.Cockpit) { Headset = headset };
                var attitude = new Basis(Vector3.Up, 0.6f) * new Basis(Vector3.Back, 0.8f);
                cam.Snap(StartAt, attitude, 90f, new Transform3D(attitude, StartAt));
                int shot = cam.Shot;
                cam.CrashView(StartAt, -attitude.Z);
                var crash = camera.Basis;
                ctx.Check(cam.TakeXrWrite() == XrAnchor.Camera && cam.Shot == shot + 1,
                    $"{arm}: the crash cut asks for the camera's pose and counts one cut");
                if (headset != null)
                {
                    ctx.Check(crash.Y.IsEqualApprox(Vector3.Up), $"headset: the crash cut faces a level heading");
                }

                cam.EnterDeathView();
                float worst = 0f;
                Basis? first = null;
                for (int i = 0; i < 60; i++)
                {
                    var at = StartAt + (-attitude.Z * 90f * StepDt * i) + (Vector3.Down * 0.3f * i);
                    cam.DeathView(new Transform3D(attitude, at), 90f, null, null);
                    first ??= camera.Basis;
                    worst = Mathf.Max(worst, TurnDeg(first.Value, camera.Basis));
                }

                report.AppendLine($"{arm}: death camera turned {worst:0.###} deg over 60 frames of a falling wreck, level up {camera.Basis.Y.AngleTo(Vector3.Up):0.####} rad");
                if (headset != null)
                {
                    ctx.Check(worst < PoseTolDeg && camera.Basis.Y.IsEqualApprox(Vector3.Up),
                        $"headset: the death camera holds the level heading it cut to ({worst:0.####}°)");
                }
                else
                {
                    ctx.Check(worst > 1f, $"flat: the death camera re-aims at the wreck ({worst:0.###}°), the control");
                }

                cam.CycleCockpitViews();
                cam.CycleCockpitViews();
                cam.ResetToChase();
                var reset = headset != null ? PilotViewMode.Cockpit : PilotViewMode.Chase;
                ctx.Check(cam.ViewMode == reset,
                    $"{arm}: the death reset returns to {PilotView.Name(reset)} (read {PilotView.Name(cam.ViewMode)})");
            }
        }
        finally
        {
            camera.QueueFree();
        }
    }

    // Steps a turn and reports the worst frame's distance from `expected`, the origin's pose in the
    // aircraft's frame, and how far the aircraft itself turned.
    private static (float Metres, float Degrees) Ride(GameClock clock, FlightController plane, XrRig rig,
        int frames, Transform3D expected, out float movedDeg)
    {
        Step(clock, plane, rig);
        var start = plane.GlobalBasis;
        var worst = (Metres: 0f, Degrees: 0f);
        movedDeg = 0f;
        for (int i = 0; i < frames; i++)
        {
            Step(clock, plane, rig);
            worst = Worse(worst, InPlane(rig.Origin.GlobalTransform, plane), expected);
            movedDeg = Mathf.Max(movedDeg, TurnDeg(start, plane.GlobalBasis));
        }

        return worst;
    }

    // The flat chase camera over a turn: how far it leaves the pose it settled at, in the plane frame.
    private static (float Metres, float Degrees) FlatLag(GameClock clock, FlightController plane, Camera3D camera,
        int frames)
    {
        Settle(clock, plane, null);
        var settled = InPlane(camera.GlobalTransform, plane);
        var worst = (Metres: 0f, Degrees: 0f);
        for (int i = 0; i < frames; i++)
        {
            Step(clock, plane, null);
            worst = Worse(worst, InPlane(camera.GlobalTransform, plane), settled);
        }

        return worst;
    }

    private static (float Metres, float Degrees) Worse((float Metres, float Degrees) worst, Transform3D pose,
        Transform3D expected) =>
        (Mathf.Max(worst.Metres, pose.Origin.DistanceTo(expected.Origin)),
         Mathf.Max(worst.Degrees, TurnDeg(expected.Basis, pose.Basis)));

    // The head-look inputs, held or released together: a numpad snap, the look stick, look-behind,
    // free-look and smooth mode. With `all` the centre and padlock keys join them. The flat control
    // leaves those two out, since either can hold its head still and pass it vacuously.
    private static void Look(FlightController plane, bool held, bool all = true)
    {
        plane.Look.PinnedView = held ? 4 : 0;
        plane.Look.PinnedLook = held ? new Vector2(0.6f, 0.4f) : Vector2.Zero;
        var actions = new List<InputAction> { InputAction.LookBack, InputAction.FreeLook, InputAction.SmoothLookMode };
        if (all || !held)
        {
            actions.Add(InputAction.LookCenter);
            actions.Add(InputAction.TrackTarget);
        }

        // A held action stays stored until released: the whole suite runs inside one engine frame.
        foreach (var action in actions)
        {
            plane.HoldActionForTest(action, held);
        }
    }

    // A pose in the aircraft's own frame.
    private static Transform3D InPlane(Transform3D pose, FlightController plane)
    {
        var toPlane = plane.GlobalBasis.Orthonormalized().Inverse();
        return new Transform3D(toPlane * pose.Basis.Orthonormalized(), toPlane * (pose.Origin - plane.GlobalPosition));
    }

    // How far round from a pose's level gaze a point lies, seen from above, in degrees.
    private static float YawOffDeg(Transform3D pose, Vector3 point)
    {
        var to = point - pose.Origin;
        var gaze = -pose.Basis.Z;
        return Mathf.RadToDeg(Mathf.Abs(new Vector2(gaze.X, gaze.Z).AngleTo(new Vector2(to.X, to.Z))));
    }

    private static float TurnDeg(Basis from, Basis to)
    {
        var q = (to.Orthonormalized() * from.Orthonormalized().Transposed()).Orthonormalized().GetRotationQuaternion();
        return Mathf.RadToDeg(2f * Mathf.Acos(Mathf.Clamp(Mathf.Abs(q.W), 0f, 1f)));
    }

    private static void Settle(GameClock clock, FlightController plane, XrRig? rig)
    {
        for (int i = 0; i < 30; i++)
        {
            Step(clock, plane, rig);
        }
    }

    // A live frame's order: the session's sim step, the camera step, then the VR seat after every
    // writer. The sim step is what flies the turn, so the views are read on a moving aircraft.
    private static void Step(GameClock clock, FlightController plane, XrRig? rig)
    {
        clock.BeginFrame(StepDt);
        plane.SimStep(StepDt);
        plane._Process(StepDt);
        rig?.Place();
    }

    // Builds one human flying the hold in the given view. The VR seat over the stand-in is bound to
    // its pane, and `headset` is set on the seat, null flying the flat rig. All is torn down after.
    private static bool WithHeadsetFlight(TestContext ctx, PilotViewMode mode, string hold, XrChase? headset,
        Action<GameClock, FlightController, XrRig, Vector3> sortie)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        ctx.RequireData(chapterZrdr, $"C1 zrdr");
        if (XRServer.PrimaryInterface != null)
        {
            ctx.Check(false, $"no XR interface is live before the suite, found {XRServer.PrimaryInterface.GetName()}");
            return false;
        }

        var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
        var textures = new TextureArchive(texturesPath);
        var pool = new ProjectilePool(textures, null, null);
        ctx.Host.AddChild(pool);
        var paneView = new SubViewport();
        ctx.Host.AddChild(paneView);
        var sim = new XrSimInterface(XrSimSpec.Default);
        XRServer.AddInterface(sim);
        var origin = new XROrigin3D { Name = "xr-view-origin" };
        var head = new XRCamera3D { Name = "xr-view-head", Current = false };
        origin.AddChild(head);
        ctx.Host.AddChild(origin);
        var savedClock = GameClock.Current;
        var pane = new PlayerRig { Index = 0, Camera = ctx.Camera, HudParent = paneView, Viewport = paneView };
        var rig = new XrRig(origin, head, ctx.Camera, pilot => pilot.Dressing.Stereo);
        FlightRoster? roster = null;
        try
        {
            ctx.Check(sim.Initialize(), $"the stand-in interface initialises");
            var spec = SessionSpec.Parse(new[]
            {
                "--fly", $"--plane={ctx.PlaneName}", $"--view={PilotView.Name(mode)}", $"--hold={hold}",
            });
            var rigs = new[] { pane };
            roster = BuildRoster(ctx, spec, planesGamez, textures, pool, chapterZrdr, rigs);
            roster.BuildPlayers(rigs);
            if (pane.Controller is not { } plane)
            {
                return false;
            }

            plane.Headset = headset;
            rig.Bind(pane);
            var clock = new GameClock { Mode = GameClock.RunMode.Realtime };
            GameClock.Current = clock;
            sortie(clock, plane, rig, MarkerRig.FindNamedMarker(planesGamez, ctx.PlaneName, "cockpit_camera"));
            return true;
        }
        finally
        {
            GameClock.Current = savedClock;
            rig.Release(pane);
            var live = pane.Controller;
            roster?.ClearMembership();
            live?.Free();
            XRServer.ClearReferenceFrame();
            sim.Uninitialize();
            XRServer.RemoveInterface(sim);
            origin.QueueFree();
            paneView.Free();
            pool.Free();
            textures.Dispose();
        }
    }

    private static FlightRoster BuildRoster(TestContext ctx, SessionSpec spec, GameZ planesGamez,
        TextureArchive textures, ProjectilePool pool, string chapterZrdr, IReadOnlyList<PlayerRig> rigs)
    {
        var resources = new AircraftAssemblyResources
        {
            PlanesGamez = planesGamez,
            StatsFor = plane => PlaneStats.Load(ctx.ZrdrPath, plane),
            AiStatsFor = (plane, aiDef) => PlaneStats.LoadForAi(ctx.ZrdrPath, plane, aiDef),
            CamParamsFor = plane => CamParams.Load(ctx.ZrdrPath, plane),
            PaintRng = new RandomNumberGenerator(),
            ZrdrPath = ctx.ZrdrPath,
            StockLoadouts = StockLoadouts.Load(),
            WeaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null),
            WeaponMessages = Messages.Load(ctx.MessagesPath),
            Textures = textures,
            Shakes = ShakeDefs.Load(ctx.ZrdrPath),
        };
        return new FlightRoster(FlightRosterPolicy.From(spec),
            new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof")),
            new WorldEffectsFactory(spec, ctx.Host, () => Vector3.Zero), ctx.Host, resources,
            new FlightWorldBindings
            {
                Projectiles = pool,
                Gamez = planesGamez,
                ChapterZrdrPath = chapterZrdr,
            },
            new HumanRosterBindings
            {
                RigCount = rigs.Count,
                Rigs = rigs,
                PauseState = new PauseState(),
            }, new RotatedStart());
    }

    // One aeroplane, airborne and level on a rotated heading far from the chapter origin.
    private sealed class RotatedStart : IFlightStarts
    {
        public IReadOnlyList<FlightStart> ChooseStarts(IReadOnlyList<SpawnPoint>? spawns,
            string missionZrdrPath, int spawnBase, int playerCount)
        {
            var starts = new FlightStart[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                var position = StartAt + new Vector3(i * 60f, 0f, 0f);
                starts[i] = new FlightStart(position, position + StartDir, 0.6f, 100f);
            }

            return starts;
        }
    }
}
