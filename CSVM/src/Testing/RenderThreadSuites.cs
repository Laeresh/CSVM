using System;
using System.Threading;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>What the game hands Godot's separate render thread and what it reads back from it.
/// Covered are the project's thread model and the render-time pair read without a round trip. So
/// are a pane read back through the rendering device, which answers only the render thread, and
/// texture uploads keeping their own pictures. So is the subtree copy that stands in for Duplicate(). A native error from any of them fails the run
/// through the harness's engine-error screen.</summary>
internal static class RenderThreadSuites
{
    // The project setting's value for Godot's separate render thread.
    private const int SeparateModel = 2;

    [Suite("render-thread-handoffs",
        "The project draws on Godot's separate render thread; the measured render CPU and GPU times "
        + "read off the frame path equal a synchronous read once the render thread has caught up; a "
        + "pane read back through the rendering device lands the same pixels a synchronous read "
        + "answers; a texture replaced many times over, its source bytes refilled after every "
        + "upload, holds the last picture uploaded; and a geometry subtree copied without "
        + "Duplicate() keeps its names, transforms, mesh, surface material, metadata and children")]
    internal static void RenderThreadHandoffs(TestContext ctx)
    {
        int model = ProjectSettings.GetSetting("rendering/driver/threads/thread_model").AsInt32();
        ctx.Check(model == SeparateModel, $"project.godot draws on the separate render thread model={model}");
        ctx.Note($"this run's main thread is the render thread: {RenderingServer.IsOnRenderThread()}");
        if (RenderingServer.GetRenderingDevice() == null)
        {
            throw new SuiteSkippedException("no rendering device, so nothing is drawn or read back");
        }

        // A pane that is all one clear colour, so the frame is known without drawing anything. The
        // harness's own window is hidden and never drawn, so the pane is what gets measured too.
        var pane = new SubViewport
        {
            Size = new Vector2I(64, 48),
            TransparentBg = false,
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(1f, 0f, 0f),
        };
        pane.AddChild(new Camera3D { Current = true, Environment = environment });
        ctx.Host.AddChild(pane);
        try
        {
            RenderTimes(ctx, pane);
            PaneReadsBack(ctx, pane);
        }
        finally
        {
            ctx.Host.RemoveChild(pane);
            pane.Free();
        }

        UploadsKeepTheirOwnPicture(ctx);
        CopiesAGeometrySubtree(ctx);
    }

    // Able to fail: a copy that loses the transform, the mesh, a surface's material, the metadata
    // animation resolves names by, or a child. Duplicate() in its place crashes this process.
    private static void CopiesAGeometrySubtree(TestContext ctx)
    {
        var material = new StandardMaterial3D();
        var root = new Node3D { Name = "copy_root", Position = new Vector3(1f, 2f, 3f) };
        var mesh = new MeshInstance3D { Name = "copy_mesh", Mesh = new BoxMesh(), Position = new Vector3(0f, 5f, 0f) };
        mesh.SetSurfaceOverrideMaterial(0, material);
        mesh.SetMeta(Mech3.AnimRuntime.NameMeta, "copy_cs_name");
        root.AddChild(mesh);
        mesh.AddChild(new Node3D { Name = "copy_leaf" });
        ctx.Host.AddChild(root);
        Node3D? copy = null;
        try
        {
            copy = SceneCopy.Of(root);
            var copied = copy.GetNodeOrNull<MeshInstance3D>("copy_mesh");
            ctx.Check(copy.Name == "copy_root" && copy.Position == root.Position,
                $"the copy keeps its root's name and transform name={copy.Name} at={copy.Position}");
            ctx.Check(copied != null && copied.Mesh == mesh.Mesh && copied.Position == mesh.Position
                && copied.GetSurfaceOverrideMaterial(0) == material
                && copied.GetMeta(Mech3.AnimRuntime.NameMeta).AsString() == "copy_cs_name"
                && copied.GetNodeOrNull("copy_leaf") != null,
                $"the copied geometry shares its mesh and surface material and keeps its transform, metadata and child");
        }
        finally
        {
            copy?.Free();
            ctx.Host.RemoveChild(root);
            root.Free();
        }
    }

    // Able to fail: a publish that never runs reads zeros, a swapped pair or a stale one differs.
    private static void RenderTimes(TestContext ctx, SubViewport pane)
    {
        var viewport = pane.GetViewportRid();
        RenderingServer.ViewportSetMeasureRenderTime(viewport, true);
        var measured = new MeasuredRenderTime(viewport);
        // The times come back from GPU timestamps some frames after the frame they measure.
        for (int i = 0; i < 6; i++)
        {
            RenderingServer.ForceDraw();
        }

        measured.Read();
        RenderingServer.ForceSync();
        var (cpu, gpu) = measured.Read();
        double cpuNow = RenderingServer.ViewportGetMeasuredRenderTimeCpu(viewport);
        double gpuNow = RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewport);
        ctx.Check(cpu == cpuNow && gpu == gpuNow && cpu > 0,
            $"the render times read off the frame path match a synchronous read cpu={cpu:0.###}/{cpuNow:0.###} gpu={gpu:0.###}/{gpuNow:0.###}");
    }

    // Able to fail: a request that never lands, lands null, or lands a different frame or format.
    private static void PaneReadsBack(TestContext ctx, SubViewport pane)
    {
        RenderingServer.ForceDraw();
        RenderingServer.ForceDraw();
        // Not disposed: a landing later than the wait below must still find it.
        var done = new ManualResetEventSlim(false);
        Image? landed = null;
        bool asked = PaneReadback.Request(pane, frame =>
        {
            landed = frame;
            done.Set();
        });

        // The device hands an asynchronous copy over a few of its own frames later.
        for (int i = 0; i < 4 && !done.IsSet; i++)
        {
            RenderingServer.ForceDraw();
            RenderingServer.ForceSync();
        }

        bool arrived = done.Wait(TimeSpan.FromSeconds(5));
        var direct = pane.GetTexture().GetImage();
        ctx.Check(asked && arrived && landed != null, $"the pane read back lands asked={asked} arrived={arrived} frame={landed != null}");
        if (landed == null)
        {
            return;
        }

        ctx.Check(landed.GetWidth() == 64 && landed.GetHeight() == 48 && landed.GetFormat() == Image.Format.Rgb8,
            $"the landed frame is the pane's size in the synchronous read's format size={landed.GetWidth()}x{landed.GetHeight()} format={landed.GetFormat()}");
        var centre = landed.GetPixel(32, 24);
        ctx.Check(centre.R8 >= 250 && centre.G8 <= 5 && centre.B8 <= 5,
            $"the landed frame is the pane's clear colour centre={centre.R8},{centre.G8},{centre.B8}");
        ctx.Check(direct != null && direct.GetFormat() == landed.GetFormat()
            && direct.GetData().AsSpan().SequenceEqual(landed.GetData()),
            $"the landed frame is the one a synchronous read answers");
    }

    // Able to fail: an upload reusing one Image lets a later refill reach an earlier update. A
    // queued update that reads an emptied Image logs a native error.
    private static void UploadsKeepTheirOwnPicture(TestContext ctx)
    {
        const int size = 64;
        const int uploads = 200;
        var pixels = new byte[size * size];
        var texture = TextureUpload.Create(size, size, Image.Format.L8, pixels);
        for (int n = 1; n <= uploads; n++)
        {
            Array.Fill(pixels, (byte)n);
            TextureUpload.Replace(texture, size, size, Image.Format.L8, pixels);
            Array.Fill(pixels, (byte)0);
        }

        RenderingServer.ForceSync();
        var read = texture.GetImage();
        byte last = unchecked((byte)uploads);
        bool whole = read != null && Array.TrueForAll(read.GetData(), b => b == last);
        ctx.Check(whole, $"a texture replaced {uploads} times holds the last picture uploaded, not the refilled bytes");
    }
}
