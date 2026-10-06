using System.Collections.Generic;
using System.Globalization;
using CSVM.Extraction;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Launch;
using CSVM.Mech3;
using CSVM.Session.World;
using CSVM.Spec;
using Godot;

namespace CSVM.Testing;

/// <summary>The VR seat's rig on the <c>--xr-sim</c> stand-in interface, with no stereo viewport.
/// It reads where the rig puts the eye, how a per-plane eye offset moves it, the recenter, and the
/// sky following the head. Module entries: docs/architecture/Flight.md on XrRig,
/// docs/architecture/Launch.md on XrSimInterface.</summary>
internal static class XrRigSuites
{
    // Two millimetres: float32 world coordinates near 7 km step by half of one.
    private const float EyeTol = 0.002f;

    // A rotated pose far from the chapter origin, so no basis product is an exact 0 or 1.
    private static readonly Vector3 AircraftAt = new(-6350f, 300f, -4144f);
    private static readonly Basis Attitude = Basis.FromEuler(new Vector3(0.21f, -2.3f, 0.13f));

    /// <summary>Builds the flown plane's interior and the rig over a live stand-in interface. It then
    /// places, offsets, moves the head and recenters. Each step reads the head node, and the last
    /// the eyes the renderer would take.</summary>
    [Suite("xr-rig-seat",
        "the VR seat's eye stands on the cockpit_camera marker through XrCockpit.EyeInAircraft, a per-plane eye offset moves the eye and not the interior, a recenter puts an offset head back on that eye with its tilt kept, and the sky follows the tracked head while the headset flies the pane")]
    internal static void XrRigSeat(TestContext ctx)
    {
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");
        string zrdr = SessionPaths.MissionZrdr(ctx.DataRoot, "C1", "IA1");
        ctx.RequireData(zrdr, $"C1/IA1 mission zrdr");
        if (XRServer.PrimaryInterface != null)
        {
            ctx.Check(false, $"no XR interface is live before the suite, found {XRServer.PrimaryInterface.GetName()}");
            return;
        }

        var textures = new TextureArchive(texturesPath);
        var owned = new List<Node>();
        var sim = new XrSimInterface(XrSimSpec.Default);
        bool added = false;
        try
        {
            var builder = new PlaneBuilder(GameZ.Load(ctx.PlanesGamezPath), textures, spinningProps: true,
                cockpitInterior: true);
            var model = builder.Build(ctx.PlaneName);
            var aircraft = new Node3D { Name = "xr-rig-aircraft" };
            aircraft.AddChild(model);
            var flat = new Camera3D { Name = "xr-rig-flat", Current = false };
            var sun = new DirectionalLight3D { Name = "xr-rig-sun" };
            owned.AddRange(new Node[] { aircraft, flat, sun });
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
            var origin = new XROrigin3D { Name = "xr-rig-origin" };
            var head = new XRCamera3D { Name = "xr-rig-head", Current = false };
            origin.AddChild(head);
            ctx.Host.AddChild(origin);
            owned.Add(origin);
            var rig = new XrRig(origin, head, flat, pilot => pilot.Dressing.Stereo);

            var seated = new XrCockpit(interior);
            ctx.Check(seated.EyeInAircraft.IsEqualApprox(builder.CockpitCameraOffset),
                $"with no offset the eye in the aircraft is the cockpit_camera marker {seated.EyeInAircraft}");
            rig.Place(flat.GlobalTransform, aircraft, Attitude, seated);
            ctx.Check(head.GlobalPosition.DistanceTo(marker) < EyeTol,
                $"the seated head stands on the marker, {Mm(head.GlobalPosition.DistanceTo(marker))} off");
            ctx.Check(head.GlobalPosition.DistanceTo(flat.GlobalPosition) < EyeTol,
                $"which is the flat cockpit camera's eye, {Mm(head.GlobalPosition.DistanceTo(flat.GlobalPosition))} off");
            ctx.Check((origin.GlobalPosition + interior.Position).DistanceTo(interiorAt) < EyeTol,
                $"and the lifted interior stays where the airframe holds it, {Mm((origin.GlobalPosition + interior.Position).DistanceTo(interiorAt))} off");

            // Hidden, the interior goes back to its mount, so a second XrCockpit captures that mount.
            interior.Visible = false;
            seated.Follow(origin.GlobalTransform, aircraft);
            interior.Visible = true;
            var aft = new VrCockpit(1f, 0.06f).EyeOffsetInAircraft;
            var offset = new XrCockpit(interior, aft);
            Vector3 offsetEye = aircraft.GlobalTransform * (builder.CockpitCameraOffset + aft);
            rig.Place(flat.GlobalTransform, aircraft, Attitude, offset);
            ctx.Check(head.GlobalPosition.DistanceTo(offsetEye) < EyeTol,
                $"a 6 cm offset moves the head onto EyeInAircraft, {Mm(head.GlobalPosition.DistanceTo(offsetEye))} off");
            ctx.Check(Mathf.Abs(head.GlobalPosition.DistanceTo(marker) - 0.06f) < EyeTol,
                $"6 cm from the marker ({Mm(head.GlobalPosition.DistanceTo(marker))})");
            ctx.Check((origin.GlobalPosition + interior.Position).DistanceTo(interiorAt) < EyeTol,
                $"while the interior stays on the airframe, {Mm((origin.GlobalPosition + interior.Position).DistanceTo(interiorAt))} off");

            // The player shifts in the seat and turns: the head leaves the eye.
            sim.MoveHead(30f, -12f, new Vector3(0.05f, 0.03f, 0.10f));
            float away = head.GlobalPosition.DistanceTo(offsetEye);
            ctx.Check(away > 0.1f, $"the moved stand-in head carries the head node off the eye ({Mm(away)})");
            SkyFollowsHead(ctx, zrdr, rig, flat, sun);

            rig.Recenter("xr-rig-seat");
            ctx.Check(head.GlobalPosition.DistanceTo(offsetEye) < EyeTol,
                $"a recenter puts the head back on EyeInAircraft, {Mm(head.GlobalPosition.DistanceTo(offsetEye))} off");
            var look = -head.Transform.Basis.Z;
            float yaw = Mathf.RadToDeg(Mathf.Atan2(look.X, -look.Z));
            float pitch = Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(look.Y, -1f, 1f)));
            ctx.Check(Mathf.Abs(yaw) < 0.01f && Mathf.Abs(pitch + 12f) < 0.01f,
                $"facing the origin's heading with the head's tilt kept (yaw {yaw:0.###}°, pitch {pitch:0.###}°)");
            var left = sim._GetTransformForView(0, origin.GlobalTransform).Origin;
            var right = sim._GetTransformForView(1, origin.GlobalTransform).Origin;
            Vector3 between = (left + right) * 0.5f;
            ctx.Check(between.DistanceTo(offsetEye) < EyeTol,
                $"and the renderer's two eyes straddle it, {Mm(between.DistanceTo(offsetEye))} off, {Mm(left.DistanceTo(right))} apart");
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
        }
    }

    // The weather tick centres the pane's dome and deck on the tracked head while the rig flies the
    // pane. Once the rig lets the pane go, the flat camera takes over: the flat path's control.
    private static void SkyFollowsHead(TestContext ctx, string zrdr, XrRig rig, Camera3D flat, DirectionalLight3D sun)
    {
        var dome = new Node3D { Name = "xr-rig-dome" };
        var deck = new Node3D { Name = "xr-rig-deck" };
        ctx.Host.AddChild(dome);
        ctx.Host.AddChild(deck);
        try
        {
            var pane = new PlayerRig { Index = 0, Camera = flat, HudParent = ctx.Host, Horizon = dome, Deck = deck };
            var panes = new List<PlayerRig> { pane };
            var weather = new WeatherRig(SessionSpec.Parse(new[] { "--chapter=C1", "--mission=IA1" }), ctx.Host, sun);
            weather.Build(zrdr, panes, System.Array.Empty<HorizonZone>(), _ => { });
            rig.Bind(pane);
            weather.Tick(panes);
            Vector3 eyes = rig.Head.GlobalPosition;
            ctx.Check(dome.Position.DistanceTo(eyes) < EyeTol,
                $"the dome is centred on the tracked head, {Mm(dome.Position.DistanceTo(eyes))} off, {Mm(eyes.DistanceTo(flat.Position))} from the flat camera");
            bool deckFollows = deck.Position != Vector3.Zero;
            if (deckFollows)
            {
                ctx.Check(Mathf.Abs(deck.Position.X - eyes.X) < EyeTol && Mathf.Abs(deck.Position.Z - eyes.Z) < EyeTol,
                    $"and the deck follows the head across, at x {deck.Position.X:0.000} z {deck.Position.Z:0.000}");
            }
            else
            {
                ctx.Note($"C1/IA1 places no deck, so only the dome is read");
            }

            rig.Release(pane);
            weather.Tick(panes);
            ctx.Check(pane.Xr == null && dome.Position == flat.Position,
                $"a pane the headset does not fly centres its dome on the camera, as flat play does");
        }
        finally
        {
            FogViewTable.Clear();
            dome.QueueFree();
            deck.QueueFree();
        }
    }

    private static string Mm(float metres) =>
        (metres * 1000f).ToString("0.###", CultureInfo.InvariantCulture) + " mm";
}
