using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CSVM.Bindings;
using CSVM.Extraction;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Launch;
using CSVM.Mech3;
using CSVM.Spec;
using Godot;

namespace CSVM.Testing;

/// <summary>The <c>--xr-tune</c> lab over the flown plane's stereo interior on the <c>--xr-sim</c>
/// stand-in interface, with no stereo viewport. Scripted hand controllers drive it, and each nudge is
/// read where the rig puts the eye and where the interior stands. The save is read in a copy of the
/// committed file. Module entry: docs/architecture/Flight.md on XrCockpitTuner.</summary>
internal static class XrTuneSuites
{
    // Two millimetres: float32 world coordinates near 7 km step by half of one.
    private const float EyeTol = 0.002f;
    private const float Frame = 1f / 60f;

    // A rotated pose far from the chapter origin, so no basis product is an exact 0 or 1.
    private static readonly Vector3 AircraftAt = new(-6350f, 300f, -4144f);
    private static readonly Basis Attitude = Basis.FromEuler(new Vector3(0.21f, -2.3f, 0.13f));

    [Suite("xr-cockpit-tune",
        "the --xr-tune lab: with the right grip up nothing moves, with it held the right stick moves the seated eye forward and the interior's scale up in steps that reach the rig's eye and the lifted interior while the interior stays on the airframe, B raises the eye, the readout on the HUD panel names the plane and its values, the trigger writes the plane into the file and leaves the other entries as they were, the stick click resets to the file, the left stick asks for the next plane, a plane flown again takes the values it was left with, a pause holds every control, and F22 to F24 are the keyboard's twin")]
    internal static void XrCockpitTune(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        if (XRServer.PrimaryInterface != null)
        {
            ctx.Check(false, $"no XR interface is live before the suite, found {XRServer.PrimaryInterface.GetName()}");
            return;
        }

        string shipped = ProjectSettings.GlobalizePath(VrCockpits.DefaultPath);
        string dir = Path.Combine(Path.GetTempPath(), "csvm-tests", "xr-tune-" + Guid.NewGuid().ToString("N"));
        string file = Path.Combine(dir, "vr_cockpits.json");
        Directory.CreateDirectory(dir);
        File.Copy(shipped, file);
        byte[] original = File.ReadAllBytes(file);

        var textures = new TextureArchive(texturesPath);
        var owned = new List<Node>();
        var sim = new XrSimInterface(XrSimSpec.Default);
        bool added = false;
        try
        {
            var builder = new PlaneBuilder(GameZ.Load(ctx.PlanesGamezPath), textures, spinningProps: true, cockpitInterior: true);
            var model = builder.Build(ctx.PlaneName);
            var aircraft = new Node3D { Name = "xr-tune-aircraft" };
            aircraft.AddChild(model);
            var flat = new Camera3D { Name = "xr-tune-flat", Current = false };
            var origin = new XROrigin3D { Name = "xr-tune-origin" };
            var head = new XRCamera3D { Name = "xr-tune-head", Current = false };
            origin.AddChild(head);
            var panel = XrHudPanel.Build(() => origin.GlobalTransform, eyeFeed: false, ctx.Host.GetViewport());
            owned.AddRange(new Node[] { aircraft, flat, origin, panel });
            foreach (var node in owned)
            {
                ctx.Host.AddChild(node);
            }

            if (builder.CockpitInterior is not { } interior)
            {
                ctx.Check(false, $"{ctx.PlaneName} builds a cockpit1 interior");
                return;
            }

            aircraft.GlobalTransform = new Transform3D(Attitude, AircraftAt);
            interior.Visible = true;
            Vector3 interiorAt = interior.GlobalPosition;
            Vector3 marker = aircraft.GlobalTransform * builder.CockpitCameraOffset;
            var (eyeAt, eyeBasis) = CameraController.FirstPersonPose(AircraftAt, Attitude, builder.CockpitCameraOffset);
            flat.GlobalTransform = new Transform3D(eyeBasis, eyeAt);
            XRServer.AddInterface(sim);
            added = true;
            ctx.Check(sim.Initialize(), $"the stand-in interface initialises");
            var rig = new XrRig(origin, head, flat, _ => null);

            var hands = new ScriptedHands();
            var cockpit = new XrCockpit(interior);
            XrTuneSeat? seat = new XrTuneSeat(cockpit, ctx.PlaneName, false);
            var swaps = new List<string>();
            var tuner = XrCockpitTuner.Build(panel.Surface, () => seat, swaps.Add, new XrControllers(hands), file);
            tuner.ProcessMode = Node.ProcessModeEnum.Disabled;
            ctx.Host.AddChild(tuner);
            owned.Add(tuner);
            var names = VrCockpits.Parse(original, new List<string>()).Names;
            int index = names.ToList().IndexOf(ctx.PlaneName);
            ctx.Check(index >= 0, $"the committed file lists {ctx.PlaneName}");

            // Place the rig after each frame, as the session does; return the eye's offset from the
            // marker in the aircraft's frame.
            Vector3 Step(float dt = Frame)
            {
                tuner.Poll(dt);
                rig.Place(flat.GlobalTransform, aircraft, Attitude, cockpit);
                return Attitude.Inverse() * (head.GlobalPosition - marker);
            }

            var start = Step();
            ctx.Check(tuner.Plane == ctx.PlaneName && start.Length() < EyeTol,
                $"the lab takes the flown plane ({tuner.Plane}) with the eye on the marker ({Mm(start.Length())} off)");
            ctx.Check(XrHudPanel.Draws(panel.Surface) && tuner.Readout.Contains($"{ctx.PlaneName} ({index + 1} of {names.Count})", StringComparison.Ordinal),
                $"the readout shows the panel and names the plane: '{FirstLine(tuner.Readout)}'");

            // Control: the stick and the face buttons with the grip up.
            hands.Set(XrAxis.RightStickY, 1f);
            hands.Set(XrButton.RightUpperFace, true);
            var idle = Step(0.6f);
            ctx.Check(idle.Length() < EyeTol && tuner.Current == tuner.Saved(ctx.PlaneName),
                $"with the right grip up the stick and B move nothing ({Mm(idle.Length())})");
            ctx.Check(!tuner.Readout.Contains('\n') && tuner.Readout.Contains("[right grip]", StringComparison.Ordinal),
                $"and the readout is one line: '{tuner.Readout}'");
            hands.Set(XrButton.RightUpperFace, false);

            // The grip held, the stick forward: one step, then a repeat once held past the delay.
            hands.Set(XrButton.RightGrip, true);
            var fore = Step();
            ctx.Check(tuner.Readout.Contains("step 0.025", StringComparison.Ordinal) && tuner.Readout.Contains("trigger saves", StringComparison.Ordinal),
                $"the grip opens the full card with each control and step");
            var expected = new VrCockpit(1f, -0.01f).EyeOffsetInAircraft;
            ctx.Check(fore.DistanceTo(expected) < EyeTol && Mathf.IsEqualApprox(tuner.Current!.Value.EyeOffsetZ, -0.01f),
                $"grip and stick forward move the eye 10 mm forward along the sight line ({Vec(fore)} against {Vec(expected)})");
            var twice = Step(0.5f);
            ctx.Check(Mathf.IsEqualApprox(tuner.Current!.Value.EyeOffsetZ, -0.02f) && twice.DistanceTo(new VrCockpit(1f, -0.02f).EyeOffsetInAircraft) < EyeTol,
                $"held past the delay it repeats, {Mm(twice.Length())} forward");
            ctx.Check(InteriorOriginInWorld(origin, interior).DistanceTo(interiorAt) < EyeTol,
                $"while the interior stays where the airframe holds it ({Mm(InteriorOriginInWorld(origin, interior).DistanceTo(interiorAt))} off)");
            hands.Set(XrAxis.RightStickY, 0f);

            // B raises the eye along the mount's tilted Y.
            hands.Set(XrButton.RightUpperFace, true);
            var raised = Step();
            hands.Set(XrButton.RightUpperFace, false);
            var raisedWant = new VrCockpit(1f, -0.02f, 0.01f).EyeOffsetInAircraft;
            ctx.Check(raised.DistanceTo(raisedWant) < EyeTol,
                $"B raises the eye 10 mm ({Vec(raised)} against {Vec(raisedWant)})");

            // The stick right scales the interior about the authored eye.
            float before = interior.Transform.Basis.Scale.X;
            hands.Set(XrAxis.RightStickX, 1f);
            Step();
            hands.Set(XrAxis.RightStickX, 0f);
            Step();
            float after = interior.Transform.Basis.Scale.X;
            var interiorEye = InteriorOriginInWorld(origin, interior);
            ctx.Check(Mathf.Abs((after / before) - 1.025f) < 1e-4f && Mathf.IsEqualApprox(cockpit.MountScale, PlaneBuilder.InteriorScale * 1.025f),
                $"stick right scales the lifted interior by one step, {before:0.#####} to {after:0.#####} m per unit");
            ctx.Check(interiorEye.DistanceTo(marker) < EyeTol,
                $"about the authored eye, which stays on the marker ({Mm(interiorEye.DistanceTo(marker))})");
            ctx.Check(FirstGeometry(interior) is not { } geometry || geometry.CustomAabb == XrCockpit.Everywhere(Vector3.One * cockpit.MountScale),
                $"and the lifted geometry's reach follows the new scale");
            ctx.Check(tuner.Readout.Contains("scale 1.025 (41.0 mm per unit)", StringComparison.Ordinal) && tuner.Readout.Contains("unsaved", StringComparison.Ordinal),
                $"the readout shows the nudged values as unsaved");

            // The trigger saves into the file and leaves every other entry alone.
            var tuned = tuner.Current!.Value;
            hands.Set(XrButton.RightTrigger, true);
            Step();
            Step();
            hands.Set(XrButton.RightTrigger, false);
            Step();
            byte[] saved = File.ReadAllBytes(file);
            var problems = new List<string>();
            var table = VrCockpits.Parse(saved, problems);
            var was = VrCockpits.Parse(original, new List<string>());
            bool othersKept = problems.Count == 0 && table.Names.Count == was.Names.Count;
            foreach (var name in was.Names)
            {
                othersKept &= name == ctx.PlaneName || table.For(name) == was.For(name);
            }

            ctx.Check(table.For(ctx.PlaneName) == tuned && tuner.Saved(ctx.PlaneName) == tuned,
                $"the trigger saves {VrCockpitTuning.Describe(ctx.PlaneName, table.For(ctx.PlaneName))}");
            ctx.Check(othersKept && Encoding.UTF8.GetString(saved).Contains("\"_proposed\"", StringComparison.Ordinal)
                    && Encoding.UTF8.GetString(saved).Contains("\"_about\"", StringComparison.Ordinal),
                $"once, keeping the other ten entries and the notes");
            ctx.Check(!tuner.Readout.Contains("unsaved", StringComparison.Ordinal), $"the readout shows it saved");

            // A nudge, then the stick click: back to the file's value.
            hands.Set(XrAxis.RightStickX, -1f);
            Step();
            hands.Set(XrAxis.RightStickX, 0f);
            hands.Set(XrButton.RightStickClick, true);
            Step();
            hands.Set(XrButton.RightStickClick, false);
            Step();
            ctx.Check(tuner.Current == tuned && Mathf.IsEqualApprox(cockpit.MountScale, tuned.InteriorScale),
                $"the stick click resets a nudge to the file's value");

            // The left stick asks for the next plane in the file's order, once per push.
            hands.Set(XrAxis.LeftStickX, 1f);
            Step();
            Step(0.6f);
            hands.Set(XrAxis.LeftStickX, 0f);
            Step();
            string next = names[(index + 1) % names.Count];
            ctx.Check(swaps.Count == 1 && swaps[0] == next, $"the left stick asks for {next} once ({string.Join(",", swaps)})");

            // The swap, as the session makes it: another interior under the seat. The stand-in
            // reuses this one, lowered first, as a new airframe's would be.
            interior.Visible = false;
            cockpit.Follow(origin.GlobalTransform, aircraft);
            interior.Visible = true;
            var other = new XrCockpit(interior);
            seat = new XrTuneSeat(other, next, false);
            cockpit = other;
            Step();
            ctx.Check(tuner.Plane == next && Mathf.IsEqualApprox(other.MountScale, tuner.Saved(next).InteriorScale) && other.EyeOffset.IsEqualApprox(tuner.Saved(next).EyeOffsetInAircraft),
                $"a new plane takes its own values from the file, not the last plane's ({other.MountScale:0.#####} m per unit)");
            hands.Set(XrAxis.RightStickY, -1f);
            Step();
            hands.Set(XrAxis.RightStickY, 0f);
            Step();
            var nudged = tuner.Current!.Value;

            interior.Visible = false;
            cockpit.Follow(origin.GlobalTransform, aircraft);
            interior.Visible = true;
            cockpit = new XrCockpit(interior);
            seat = new XrTuneSeat(cockpit, ctx.PlaneName, false);
            Step();
            ctx.Check(tuner.Current == tuned && Mathf.IsEqualApprox(cockpit.MountScale, tuned.InteriorScale) && cockpit.EyeOffset.IsEqualApprox(tuned.EyeOffsetInAircraft),
                $"flown again, {ctx.PlaneName} takes its saved values");
            interior.Visible = false;
            cockpit.Follow(origin.GlobalTransform, aircraft);
            interior.Visible = true;
            cockpit = new XrCockpit(interior);
            seat = new XrTuneSeat(cockpit, next, false);
            Step();
            ctx.Check(tuner.Current == nudged && cockpit.EyeOffset.IsEqualApprox(nudged.EyeOffsetInAircraft),
                $"and {next} the unsaved nudge it was left with ({VrCockpitTuning.Describe(next, nudged)})");

            // A pause holds every control.
            seat = new XrTuneSeat(cockpit, next, true);
            hands.Set(XrAxis.RightStickX, 1f);
            Step();
            hands.Set(XrAxis.RightStickX, 0f);
            ctx.Check(tuner.Current == nudged, $"under a pause the grip and stick move nothing");
            hands.Set(XrButton.RightGrip, false);
            seat = new XrTuneSeat(cockpit, next, false);
            Step();

            // The keyboard's twin: F24 picks the row, F23 steps it, Shift+F24 saves.
            tuner._UnhandledInput(new InputEventKey { Keycode = Key.F24, Pressed = true });
            tuner._UnhandledInput(new InputEventKey { Keycode = Key.F23, Pressed = true });
            var keyed = tuner.Current!.Value;
            ctx.Check(Mathf.IsEqualApprox(keyed.EyeOffsetZ, nudged.EyeOffsetZ + VrCockpitTuning.OffsetStepM) && tuner.Readout.Contains("> eye aft", StringComparison.Ordinal),
                $"F24 then F23 steps the eye aft ({VrCockpitTuning.Format(keyed.EyeOffsetZ)} m)");
            tuner._UnhandledInput(new InputEventKey { Keycode = Key.F24, Pressed = true, ShiftPressed = true });
            ctx.Check(VrCockpits.Parse(File.ReadAllBytes(file), new List<string>()).For(next) == keyed && tuner.Saved(ctx.PlaneName) == tuned,
                $"Shift+F24 saves {next} and keeps {ctx.PlaneName}'s saved values");
        }
        finally
        {
            XRServer.ClearReferenceFrame();
            if (added)
            {
                sim.Uninitialize();
                XRServer.RemoveInterface(sim);
            }

            foreach (var node in owned)
            {
                node.QueueFree();
            }

            textures.Dispose();
            Directory.Delete(dir, true);
        }
    }

    // Where the lifted interior's own origin, the authored eye, stands in the world. It is held at the
    // world origin in the XR origin's axes, offset as from the XR origin.
    private static Vector3 InteriorOriginInWorld(XROrigin3D origin, Node3D interior) =>
        origin.GlobalPosition + interior.Transform.Origin;

    private static GeometryInstance3D? FirstGeometry(Node node)
    {
        if (node is GeometryInstance3D geometry)
        {
            return geometry;
        }

        foreach (var child in node.GetChildren())
        {
            if (FirstGeometry(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static string FirstLine(string text) => text.Split('\n')[0];

    private static string Mm(float metres) =>
        (metres * 1000f).ToString("0.###", CultureInfo.InvariantCulture) + " mm";

    private static string Vec(Vector3 v) =>
        string.Create(CultureInfo.InvariantCulture, $"({v.X * 1000f:0.##}, {v.Y * 1000f:0.##}, {v.Z * 1000f:0.##}) mm");

    // Hand controllers a suite holds: every control reads what it was last set to.
    private sealed class ScriptedHands : IXrHands
    {
        private readonly Dictionary<(XrHand, string, int), float> _values = new();

        public void Set(XrButton button, bool down)
        {
            var (hand, action) = XrControls.Button((int)button)!.Value;
            _values[(hand, action, -1)] = down ? 1f : 0f;
        }

        public void Set(XrAxis axis, float value)
        {
            var (hand, action, component) = XrControls.Axis((int)axis)!.Value;
            _values[(hand, action, component)] = value;
        }

        public bool Present(XrHand hand) => true;

        public float Read(XrHand hand, string action, int component) =>
            _values.TryGetValue((hand, action, component), out float value) ? value : 0f;

        public Transform3D? Aim(XrHand hand) => null;
    }
}
