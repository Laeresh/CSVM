using System.Collections.Generic;
using System.IO;
using CSVM.Bindings;
using CSVM.Flight.Camera;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The Keep Camera Zoom option against a live <see cref="CameraController"/> writing a real
/// <see cref="Camera3D"/>, with no game data. The options file is a scratch store the suite seeds.
/// The original's three resets are camera init, entering the chase view from another and the
/// free-look centre key (docs/org/cameraViews.md, "The external camera's distance").</summary>
internal static class CameraZoomSuites
{
    private const float Dt = 1f / 60f;
    private const float Speed = 60f;

    [Suite("camera-zoom-keep",
        "the Keep Camera Zoom option (CameraZoomMemory): on, a zoom set with Zoom Out survives a switch "
        + "to the cockpit and back, the flyby and back, the free-look centre key and the death camera's "
        + "return, it is written to the options file once, on the frame the target settles and not "
        + "while the key is held, and a re-seeded controller (a reload or the next mission) opens on it "
        + "with the camera that much further out; and off, a view switch and a death in the cockpit "
        + "snap the zoom to the near end, the centre key resets its target and lets the shown value "
        + "ease, and a death with chase already selected keeps it")]
    internal static void CameraZoomKeep(TestContext ctx)
    {
        var camera = new Camera3D { Name = "camera-zoom-keep" };
        ctx.Host.AddChild(camera);
        try
        {
            Kept(ctx, camera, Store(ctx, "on", new OptionsDef()));
            Off(ctx, camera, Store(ctx, "off", new OptionsDef { KeepCameraZoom = false }));
        }
        finally
        {
            camera.QueueFree();
        }
    }

    private static void Kept(TestContext ctx, Camera3D camera, OptionsStore store)
    {
        var memory = new CameraZoomMemory(store);
        var held = new HashSet<InputAction>();
        var cam = Build(camera, held, memory);
        float nearDistance = camera.Position.Length();
        ctx.Check(cam.ZoomAxis == CameraZoomMemory.NearEnd, $"on, a fresh file opens the camera at the near end ({cam.ZoomAxis})");

        held.Add(InputAction.ZoomOut);
        for (int i = 0; i < 15; i++)
        {
            cam.UpdateZoom(Dt);
        }

        ctx.Check(store.Load().CameraZoom == null, $"nothing is written while Zoom Out is held ({store.Load().CameraZoom})");
        held.Clear();
        cam.UpdateZoom(Dt);
        float zoom = cam.ZoomAxis;
        ctx.Check(zoom > 0.4f && store.Load().CameraZoom == zoom,
            $"the frame after the release writes the settled zoom ({zoom}, file {store.Load().CameraZoom})");

        cam.CycleCockpitViews();
        cam.SelectChase();
        ctx.Check(cam.ZoomAxis == zoom, $"a switch to the cockpit and back keeps it ({cam.ZoomAxis})");
        cam.EnterFlyby();
        cam.SelectChase();
        ctx.Check(cam.ZoomAxis == zoom, $"so does the flyby and back ({cam.ZoomAxis})");
        Centre(cam);
        ctx.Check(cam.Head.Mode == LookMode.FreeLook && cam.ZoomAxis == zoom,
            $"and the centre key in free-look ({cam.Head.Mode}, {cam.ZoomAxis})");
        cam.ResetToChase();
        ctx.Check(cam.ZoomAxis == zoom, $"and the death camera's return to the chase view ({cam.ZoomAxis})");

        var next = Build(camera, held, new CameraZoomMemory(store));
        ctx.Check(next.ZoomAxis == zoom, $"a re-seeded controller opens on the saved zoom ({next.ZoomAxis})");
        float further = camera.Position.Length() - nearDistance;
        ctx.Check(further > 0.9f * zoom * 10f, $"with its camera {further} m further out than the near end's");
    }

    // The resets are the controller's alone, so they are walked here with the option off.
    private static void Off(TestContext ctx, Camera3D camera, OptionsStore store)
    {
        var held = new HashSet<InputAction>();
        var cam = Build(camera, held, new CameraZoomMemory(store));
        ZoomOut(cam, held);
        ctx.Check(cam.ZoomAxis > 0.4f, $"off, Zoom Out still moves the axis ({cam.ZoomAxis})");
        cam.CycleCockpitViews();
        cam.SelectChase();
        ctx.Check(cam.ZoomAxis == CameraZoomMemory.NearEnd && cam.ZoomShown == CameraZoomMemory.NearEnd,
            $"a switch to the cockpit and back snaps it to the near end, shown value too, as 0042c33d writes both ({cam.ZoomAxis}, shown {cam.ZoomShown})");
        ZoomOut(cam, held);
        cam.EnterFlyby();
        cam.SelectChase();
        ctx.Check(cam.ZoomAxis == CameraZoomMemory.NearEnd && cam.ZoomShown == CameraZoomMemory.NearEnd,
            $"so does the flyby and back ({cam.ZoomAxis}, shown {cam.ZoomShown})");
        ZoomOut(cam, held);
        float shown = cam.ZoomShown;
        Centre(cam);
        ctx.Check(cam.ZoomAxis == CameraZoomMemory.NearEnd && shown > 0f && cam.ZoomShown == shown,
            $"the centre key in free-look resets the target alone, as 0042d566 writes, so the shown value eases after it ({cam.ZoomAxis}, shown {cam.ZoomShown})");
        ZoomOut(cam, held);
        float inChase = cam.ZoomAxis;
        cam.ResetToChase();
        ctx.Check(cam.ZoomAxis == inChase && inChase > 0.4f,
            $"a death with chase already selected keeps it, since FUN_0042c280 returns at once on the mode it holds ({cam.ZoomAxis} of {inChase})");
        cam.CycleCockpitViews();
        ZoomOut(cam, held);
        cam.ResetToChase();
        ctx.Check(cam.ZoomAxis == CameraZoomMemory.NearEnd && cam.ZoomShown == CameraZoomMemory.NearEnd,
            $"and a death in the cockpit snaps it with the return to the chase view ({cam.ZoomAxis}, shown {cam.ZoomShown})");
    }

    // A controller as the session builds it for this machine's first player: remembering, snapped.
    private static CameraController Build(Camera3D camera, HashSet<InputAction> held, CameraZoomMemory memory)
    {
        var cam = new CameraController(camera, new CamParams(), held.Contains, 0, zoomMemory: memory);
        cam.RememberZoom();
        var attitude = Basis.Identity;
        cam.Snap(Vector3.Zero, attitude, Speed, new Transform3D(attitude, Vector3.Zero));
        return cam;
    }

    // A quarter second of Zoom Out, then the settle frame.
    private static void ZoomOut(CameraController cam, HashSet<InputAction> held)
    {
        held.Add(InputAction.ZoomOut);
        for (int i = 0; i < 15; i++)
        {
            cam.UpdateZoom(Dt);
        }

        held.Clear();
        cam.UpdateZoom(Dt);
    }

    // Free-look entered on its key, then one frame of the centre key.
    private static void Centre(CameraController cam)
    {
        cam.StepHead(Dt, new HeadLookInput(0f, 0f, 0f, 0f, false, SelectFreeLookMode: true), HeadLook.ChaseElevationFloor);
        cam.StepHead(Dt, new HeadLookInput(0f, 0f, 0f, 0f, false), HeadLook.ChaseElevationFloor);
        cam.StepHead(Dt, new HeadLookInput(0f, 0f, 0f, 0f, true), HeadLook.ChaseElevationFloor);
    }

    // A scratch options store holding only what the case sets.
    private static OptionsStore Store(TestContext ctx, string name, OptionsDef def)
    {
        string dir = Path.Combine(ctx.ScratchDir, "camera-zoom-keep", name);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        var store = new OptionsStore(Path.GetFullPath(dir));
        store.Save(def);
        return store;
    }
}
