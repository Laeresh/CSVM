using System;
using System.Collections.Generic;
using CSVM.Bindings;
using CSVM.Extraction;
using CSVM.Flight;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The headlight from the control to the beam, on real <see cref="FlightController"/>s. A
/// headless run holds down no key and no pad button, so the suite supplies each frame's two device
/// sides. The seat's own reads and its own headlight step answer them. Each suite puts the
/// process-wide graphics mode and shadow quality back in a finally.</summary>
internal static class HeadlightSuites
{
    private const float StepDt = 1f / 60f;

    // Frames either side of TapHoldButton.PadHoldSeconds at 60 Hz (15 frames).
    private const int TapFrames = 6;
    private const int HoldFrames = 40;

    private const float SendInterval = 0.1f;
    private const float SpawnAltitudeM = 800f;

    [Suite("headlight-switch",
        "the headlight on bare seats with no game data: a new seat's is off; under Original graphics "
        + "H moves the switch and nothing draws; under Enhanced the beam draws, G to Original hides it "
        + "and back to Enhanced shows it again; a tap of d-pad down cycles the view on release and "
        + "leaves the switch, a hold switches it once and cycles nothing; an AI seat's H does "
        + "nothing; two splitscreen seats each switch their own; the beam casts no shadow with "
        + "Shadow Quality Off and casts one at Ultra")]
    internal static void HeadlightSwitch(TestContext ctx)
    {
        bool enhanced = GraphicsMode.Enhanced;
        string shadows = ShadowQualitySetting.Word;
        var made = new List<FlightController>();
        try
        {
            var seat = Seat(0, true, made);
            ctx.Check(!seat.Headlight.On && !seat.Headlight.Lit && seat.Headlight.Light == null,
                $"a new seat's headlight is off and builds no light");

            GraphicsMode.Set(false);
            Press(seat, Key.H);
            ctx.Check(seat.Headlight.On && !seat.Headlight.Lit,
                $"under Original graphics H moves the switch and nothing draws (on={seat.Headlight.On}, lit={seat.Headlight.Lit})");

            GraphicsMode.Set(true);
            Idle(seat);
            ctx.Check(seat.Headlight.Lit && seat.Headlight.Light!.Position.Z < 0f,
                $"G to Enhanced draws the beam the switch left on, ahead of the origin (z={seat.Headlight.Light?.Position.Z})");
            GraphicsMode.Set(false);
            Idle(seat);
            ctx.Check(seat.Headlight.On && !seat.Headlight.Lit,
                $"…G back to Original hides it and keeps the switch on");
            GraphicsMode.Set(true);
            Idle(seat);
            ctx.Check(seat.Headlight.Lit, $"…and Enhanced shows it again");

            Press(seat, Key.H);
            ctx.Check(!seat.Headlight.On && !seat.Headlight.Lit, $"a second H switches it off");

            PadSplit(ctx, seat);
            Shadows(ctx, seat);

            var ai = Seat(1, false, made);
            Press(ai, Key.H);
            Hold(ai, HoldFrames);
            ctx.Check(!ai.Headlight.On && !ai.Headlight.Lit,
                $"an AI seat's H and held d-pad down switch nothing (on={ai.Headlight.On})");

            var second = Seat(1, true, made);
            Press(seat, Key.H);
            ctx.Check(seat.Headlight.Lit && !second.Headlight.On,
                $"two splitscreen seats: the first seat's H lights its own beam and leaves the second's off");
            Hold(second, HoldFrames);
            ctx.Check(second.Headlight.Lit && seat.Headlight.Lit,
                $"…and the second seat's held d-pad down lights its own, both drawing at once");
        }
        finally
        {
            GraphicsMode.Set(enhanced);
            ShadowQualitySetting.Resolve(shadows, null, null);
            foreach (var rig in made)
            {
                if (GodotObject.IsInstanceValid(rig))
                    rig.Free();
            }
        }

        ctx.Note($"the headlight switches on H and a held d-pad down, and draws under Enhanced alone");
    }

    [Suite("headlight-respawn-remote",
        "the headlight on flown aircraft: a lit beam sits ahead of the hull's front, a crashed "
        + "aircraft draws none, a respawn keeps the switch on and the beam lights again; a remote "
        + "aircraft takes its owner's switch from the received samples, on and then off")]
    internal static void HeadlightRespawnRemote(TestContext ctx)
    {
        ctx.RequireData(ctx.Data.Planes, $"planes gamez");
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        TextureSet texturesPath = ctx.ChapterTextures(ctx.Chapter);
        ctx.RequireData(texturesPath, $"{ctx.Chapter} textures");

        bool enhanced = GraphicsMode.Enhanced;
        var textures = new TextureArchive(texturesPath);
        var pool = new ProjectilePool(textures, null, null);
        ctx.Host.AddChild(pool);
        var received = new RemotePoseBuffer(SendInterval);
        FlightController? local = null;
        FlightController? remote = null;
        try
        {
            GraphicsMode.Set(true);
            local = Rig(ctx, textures, pool, new Vector3(0f, SpawnAltitudeM, 0f), null, 0, "HeadlightLocal");
            remote = Rig(ctx, textures, pool, new Vector3(4000f, SpawnAltitudeM, 0f), received, 1, "HeadlightRemote");
            Respawn(ctx, local);
            Remote(ctx, remote, received);
        }
        finally
        {
            GraphicsMode.Set(enhanced);
            local?.Free();
            remote?.Free();
            pool.Free();
            textures.Dispose();
        }

        ctx.Note($"the headlight survives a respawn and crosses the wire");
    }

    private static void PadSplit(TestContext ctx, FlightController seat)
    {
        int cycles = 0;
        for (int i = 0; i < TapFrames; i++)
            cycles += Frame(seat, null, JoyButton.DpadDown).CyclePad ? 1 : 0;
        bool beforeRelease = cycles == 0;
        cycles += Frame(seat, null, null).CyclePad ? 1 : 0;
        ctx.Check(beforeRelease && cycles == 1 && !seat.Headlight.On,
            $"a tap of d-pad down cycles the view once, on release, and leaves the switch off (cycles {cycles})");

        cycles = Hold(seat, HoldFrames);
        ctx.Check(cycles == 0 && seat.Headlight.On && seat.Headlight.Lit,
            $"a held d-pad down switches the headlight on and cycles nothing (cycles {cycles})");
        Hold(seat, HoldFrames);
        ctx.Check(!seat.Headlight.On, $"…and a second hold switches it off");
        Press(seat, Key.H);
    }

    private static void Shadows(TestContext ctx, FlightController seat)
    {
        ShadowQualitySetting.Resolve(ShadowQualitySetting.Off, null, null);
        Idle(seat);
        bool off = seat.Headlight.Light!.ShadowEnabled;
        ShadowQualitySetting.Resolve(ShadowQualitySetting.Ultra, null, null);
        Idle(seat);
        ctx.Check(!off && seat.Headlight.Light.ShadowEnabled,
            $"the beam casts no shadow with Shadow Quality Off and one at Ultra (off={off}, ultra={seat.Headlight.Light.ShadowEnabled})");
        Press(seat, Key.H);
    }

    private static void Respawn(TestContext ctx, FlightController rig)
    {
        Press(rig, Key.H);
        var light = rig.Headlight.Light;
        float front = HullFront(rig);
        ctx.Check(rig.Headlight.Lit && light != null && light.Position.Z < front,
            $"H lights the beam ahead of the hull's front (light z={light?.Position.Z:0.00}, front z={front:0.00})");

        rig.DebugForceCrash();
        Idle(rig);
        ctx.Check(rig.Crashed && rig.Headlight.On && !rig.Headlight.Lit,
            $"a crashed aircraft draws no beam and keeps the switch");
        rig.HoldActionForTest(InputAction.Respawn, true);
        rig.SimStep(StepDt);
        rig.HoldActionForTest(InputAction.Respawn, false);
        Idle(rig);
        ctx.Check(!rig.Crashed && rig.Headlight.On && rig.Headlight.Lit,
            $"…and the respawned aircraft's switch is still on and its beam lit (crashed={rig.Crashed})");
    }

    private static void Remote(TestContext ctx, FlightController remote, RemotePoseBuffer received)
    {
        int sent = 0;
        var spawn = remote.WorldPosition;
        bool lit = Stream(remote, received, spawn, ref sent, true);
        ctx.Check(remote.Headlight.On && lit,
            $"a remote aircraft takes its owner's switch from the samples and lights its beam");
        bool still = Stream(remote, received, spawn, ref sent, false);
        ctx.Check(!remote.Headlight.On && !still,
            $"…and turns it off when the samples do");
    }

    // Two seconds of samples carrying one headlight state, the rig stepped through them. Answers
    // whether the beam drew on the last frame.
    private static bool Stream(FlightController remote, RemotePoseBuffer received, Vector3 spawn,
        ref int sent, bool headlight)
    {
        for (int step = 0; step < 120; step++)
        {
            while (sent * SendInterval <= received.Now + 1e-4)
            {
                double at = sent * SendInterval;
                received.Add(new AircraftStateMessage(1, (ushort)(sent + 1), spawn + new Vector3(0f, 0f, -100f * (float)at),
                    Quaternion.Identity, new Vector3(0f, 0f, -100f), 0.5f, 0f, 0f, 0f, false, headlight), at);
                sent++;
            }

            remote.SimStep(StepDt);
        }

        Idle(remote);
        return remote.Headlight.Lit;
    }

    // The front of the drawn hull in the rig's own frame, forward being -Z.
    private static float HullFront(FlightController rig)
    {
        var toRig = rig.GlobalTransform.AffineInverse();
        float front = float.MaxValue;
        foreach (var node in rig.PlaneModel!.FindChildren("*", nameof(MeshInstance3D), true, false))
        {
            if (node is MeshInstance3D { Mesh: not null } mesh && mesh.IsVisibleInTree())
                front = Mathf.Min(front, (toRig * mesh.GlobalTransform * mesh.GetAabb()).Position.Z);
        }

        return front;
    }

    // A seat the way a splitscreen roster builds one, out of the tree.
    private static FlightController Seat(int player, bool human, List<FlightController> made)
    {
        var rig = new FlightController
        {
            PlayerIndex = player,
            IsHumanPiloted = human,
            UseKeyboard = true,
            PadDevices = Array.Empty<int>(),
            Name = $"HeadlightSeat{made.Count}",
        };
        made.Add(rig);
        return rig;
    }

    private static FlightController Rig(TestContext ctx, TextureArchive textures, ProjectilePool pool,
        Vector3 spawn, RemotePoseBuffer? received, int index, string name)
    {
        var stats = PlaneStats.Load(ctx.Zrdr, ctx.PlaneName);
        var model = new PlaneBuilder(ctx.Planes, textures).Build(ctx.PlaneName);
        var rig = new FlightController
        {
            PlaneModel = model,
            Collider = PlaneCollider.Build(model),
            PlayerIndex = index,
            IsHumanPiloted = true,
            Projectiles = pool,
            RemotePoses = received,
            UseKeyboard = true,
            PadDevices = Array.Empty<int>(),
            AllowPause = false,
            Name = name,
        };
        rig.AddChild(model);
        rig.Setup(new FlightModel(stats), null, new CamParams(), spawn, spawn + Vector3.Forward);
        ctx.Host.AddChild(rig);
        return rig;
    }

    // One press and release of a key, a frame each.
    private static void Press(FlightController rig, Key key)
    {
        Frame(rig, key, null);
        Idle(rig);
    }

    // A held d-pad down for that many frames and its release. Answers the view cycles it read.
    private static int Hold(FlightController rig, int frames)
    {
        int cycles = 0;
        for (int i = 0; i < frames; i++)
            cycles += Frame(rig, null, JoyButton.DpadDown).CyclePad ? 1 : 0;
        return cycles + (Frame(rig, null, null).CyclePad ? 1 : 0);
    }

    private static void Idle(FlightController rig) => Frame(rig, null, null);

    // One rendered frame with at most one key and one pad button down.
    private static (bool CycleKey, bool CyclePad) Frame(FlightController rig, Key? key, JoyButton? button)
    {
        var keyboard = new OneSide();
        if (key is { } k)
            keyboard.Keys.Add((int)k);
        var pad = new OneSide();
        if (button is { } b)
            pad.Buttons.Add((DefaultBindings.AnyPad, (int)b));
        rig.ObserveDeviceForTest(keyboard, pad);
        return rig.StepHeadlightForTest(StepDt);
    }

    // One side's hardware for a frame the suite writes itself.
    private sealed class OneSide : IDeviceState
    {
        public HashSet<int> Keys { get; } = new();

        public HashSet<(DeviceId Device, int Index)> Buttons { get; } = new();

        public bool IsKeyDown(DeviceId device, int keyCode) =>
            device == DeviceId.Keyboard && Keys.Contains(keyCode);

        public bool IsButtonDown(DeviceId device, int button) => Buttons.Contains((device, button));

        public bool IsMouseButtonDown(DeviceId device, int button) => false;

        public float AxisValue(DeviceId device, int axis) => 0f;

        public HatDirection HatState(DeviceId device, int hat) => HatDirection.None;
    }
}
