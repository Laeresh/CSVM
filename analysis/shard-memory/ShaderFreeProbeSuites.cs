using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>Throwaway probe: does freeing a drawn Shader return its compiled variants' private
/// bytes? Rounds of N world-bias shaders, each with a unique text, drawn on their own quads and then
/// either freed or kept unworn. Environment: CSVM_PROBE_FREE (1 frees, 0 keeps),
/// CSVM_PROBE_SHADERS, CSVM_PROBE_ROUNDS, CSVM_PROBE_TAA (1 draws the pane with TAA). Not compiled
/// here: copy it into CSVM/src/Testing, build, and run
/// <c>.\RunProbe.ps1 --run-tests=probe-shader-free</c> (windowed; headless builds no variant).</summary>
internal static class ShaderFreeProbeSuites
{

    [Suite("probe-shader-free", "throwaway: private bytes across rounds of shaders drawn, then freed or kept unworn")]
    internal static void Probe(TestContext ctx)
    {
        if (RenderingServer.GetRenderingDevice() == null)
            throw new SuiteSkippedException("no rendering device, so no shader variant is built");
        bool free = Env("CSVM_PROBE_FREE", 1) != 0;
        int count = Env("CSVM_PROBE_SHADERS", 128);
        int rounds = Env("CSVM_PROBE_ROUNDS", 3);
        bool taa = Env("CSVM_PROBE_TAA", 0) != 0;
        var host = new Node { Name = "shader_free_probe" };
        ctx.Host.AddChild(host);
        var kept = new List<Shader>();
        try
        {
            var view = new SubViewport
            {
                Size = new Vector2I(64, 64),
                OwnWorld3D = true,
                World3D = new World3D(),
                UseTaa = taa,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                RenderTargetClearMode = SubViewport.ClearMode.Always,
            };
            view.AddChild(new Camera3D { Current = true });
            host.AddChild(view);
            Settle();
            Read(ctx, free, "start", 0, 0);
            for (int round = 0; round < rounds; round++)
            {
                var quads = new List<MeshInstance3D>();
                var materials = new List<ShaderMaterial>();
                var shaders = new List<Shader>();
                for (int i = 0; i < count; i++)
                {
                    var shader = new Shader { Code = Text(round, i) };
                    var material = new ShaderMaterial { Shader = shader };
                    var quad = new MeshInstance3D
                    {
                        Mesh = new QuadMesh { Size = new Vector2(4f, 4f) },
                        Position = new Vector3(0f, 0f, -1f - (0.01f * i)),
                        MaterialOverride = material,
                    };
                    view.AddChild(quad);
                    quads.Add(quad);
                    materials.Add(material);
                    shaders.Add(shader);
                }
                int frames = DrawUntilCompiled();
                Read(ctx, free, "drawn", round, frames);
                foreach (var quad in quads)
                {
                    view.RemoveChild(quad);
                    quad.Free();
                }
                foreach (var material in materials)
                    material.Dispose();
                if (free)
                {
                    foreach (var shader in shaders)
                        shader.Dispose();
                }
                else
                {
                    kept.AddRange(shaders);
                }
                Settle();
                Read(ctx, free, free ? "freed" : "unworn", round, 0);
            }
        }
        finally
        {
            host.Free();
            foreach (var shader in kept)
                shader.Dispose();
        }
        ctx.Check(true, $"probe ran free={free} shaders={count} rounds={rounds} taa={taa}");
    }

    /// <summary>Throwaway probe: graphics-retext-compiles step for step, with its in-place rewrite
    /// swapped for the harness's release when CSVM_PROBE_ARM is release. Arm code runs the suite
    /// itself with ShaderTwins.Rewrite cut to a plain Code assignment, the control that prints
    /// free_rid lines: wrap Rewrite's empty-text line in
    /// <c>if (Environment.GetEnvironmentVariable("CSVM_PROBE_PLAIN") != "1")</c> and run it with
    /// CSVM_PROBE_PLAIN=1. Arm empty runs it as shipped. CSVM_PROBE_SHADERS keys, CSVM_PROBE_SLEEP
    /// ms per sleeping task (two per core). Run with <c>-EngineArgs '--log-file',&lt;path&gt;</c>;
    /// windowed, since headless builds no variant.</summary>
    [Suite("probe-shader-free-retext", "throwaway: the rewrite suite's race, with the release in place of the rewrite")]
    internal static void ProbeRetext(TestContext ctx)
    {
        if (RenderingServer.GetRenderingDevice() == null)
            throw new SuiteSkippedException("no rendering device, so no pipeline is compiled");
        string arm = System.Environment.GetEnvironmentVariable("CSVM_PROBE_ARM") ?? "release";
        int count = Env("CSVM_PROBE_SHADERS", 1);
        int sleep = Env("CSVM_PROBE_SLEEP", 150);
        bool wasEnhanced = GraphicsMode.Enhanced;
        bool wasDrawn = Mech3.ShaderTwins.EnhancedDrawn;
        var host = new Node { Name = "retext_probe" };
        ctx.Host.AddChild(host);
        var busy = new List<long>();
        try
        {
            GraphicsMode.Set(false);
            Mech3.ShaderTwins.Regenerate();
            Mech3.ShaderTwins.EnhancedDrawn = false;
            RenderingServer.ForceSync();
            int? errorsBefore = TestHarness.EngineErrorsSoFar();
            var keys = Enumerable.Range(0, count).Select(i => Mech3.ShaderTwins.Make(
                () => ProbeText(i, GraphicsMode.Enhanced), "retext-probe", $"retext-probe-{i}")).ToList();
            var view = new SubViewport
            {
                Size = new Vector2I(64, 64),
                OwnWorld3D = true,
                World3D = new World3D(),
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
                RenderTargetClearMode = SubViewport.ClearMode.Always,
            };
            view.AddChild(new Camera3D { Current = true });
            var quads = keys.Select(_ => new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(4f, 4f) }, Position = new Vector3(0f, 0f, -1f) }).ToList();
            foreach (var quad in quads)
                view.AddChild(quad);
            host.AddChild(view);
            foreach (var quad in quads)
                quad.ForceUpdateTransform();
            var worn = keys.Select(k => Mech3.ShaderTwins.Follow(new ShaderMaterial(), k)).ToList();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            busy = Enumerable.Range(0, 2 * OS.GetProcessorCount())
                .Select(_ => WorkerThreadPool.AddTask(Callable.From(() => System.Threading.Thread.Sleep(sleep)))).ToList();
            for (int i = 0; i < count; i++)
                quads[i].MaterialOverride = worn[i];
            RenderingServer.ForceDraw();
            RenderingServer.ForceDraw();
            _ = view.GetTexture()?.GetImage();
            double drawnMs = watch.Elapsed.TotalMilliseconds;
            foreach (var quad in quads)
                quad.MaterialOverride = null;
            foreach (var material in worn)
                material.Dispose();
            int released = 0;
            double freeSyncMs = 0;
            if (arm == "release")
            {
                // The harness's sequence: collect, sync, release, collect.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                RenderingServer.ForceSync();
                released = Mech3.ShaderTwins.ReleaseUnworn();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                double before = watch.Elapsed.TotalMilliseconds;
                RenderingServer.ForceSync();
                freeSyncMs = watch.Elapsed.TotalMilliseconds - before;
            }
            GraphicsMode.Set(true);
            var stats = Mech3.ShaderTwins.Regenerate();
            double actedMs = watch.Elapsed.TotalMilliseconds;
            int busyDone = busy.Count(WorkerThreadPool.IsTaskCompleted);
            var taa = new SubViewport
            {
                Size = new Vector2I(256, 256),
                OwnWorld3D = true,
                World3D = new World3D(),
                UseTaa = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            };
            taa.AddChild(new Camera3D { Current = true });
            host.AddChild(taa);
            RenderingServer.ForceDraw();
            taa.Free();
            var fresh = keys.Select(k => Mech3.ShaderTwins.Follow(new ShaderMaterial(), k)).ToList();
            for (int i = 0; i < count; i++)
                quads[i].MaterialOverride = fresh[i];
            RenderingServer.ForceDraw();
            RenderingServer.ForceDraw();
            var image = view.GetTexture()?.GetImage();
            var pixel = image == null || image.IsEmpty() ? new Color(0f, 0f, 0f, 0f) : image.GetPixel(32, 32);
            RenderingServer.ForceSync();
            int? errorsAtDraw = TestHarness.EngineErrorsSoFar();
            int busyDoneAtDraw = busy.Count(WorkerThreadPool.IsTaskCompleted);
            foreach (long task in busy)
                WorkerThreadPool.WaitForTaskCompletion(task);
            busy.Clear();
            Settle();
            System.Threading.Thread.Sleep(500);
            int? errorsAfter = TestHarness.EngineErrorsSoFar();
            string line = $"probe-retext arm={arm} plain={System.Environment.GetEnvironmentVariable("CSVM_PROBE_PLAIN") ?? "0"} keys={count} sleep_ms={sleep} tasks={2 * OS.GetProcessorCount()} "
                + $"drawn_ms={drawnMs:0} acted_ms={actedMs:0} free_sync_ms={freeSyncMs:0} busy_done_at_act={busyDone} busy_done_at_draw={busyDoneAtDraw} "
                + $"released={released} retexted={stats.Retexted} pixel={pixel} "
                + $"errors_at_draw={(errorsAtDraw ?? -1) - (errorsBefore ?? 0)} errors_end={(errorsAfter ?? -1) - (errorsBefore ?? 0)} log={(errorsBefore != null ? "read" : "missing")}";
            ctx.Note($"{line}");
            Log.Info("test", $"{line}");
            foreach (var material in fresh)
                material.Dispose();
        }
        finally
        {
            foreach (long task in busy)
                WorkerThreadPool.WaitForTaskCompletion(task);
            host.Free();
            Mech3.ShaderTwins.EnhancedDrawn = wasDrawn;
            GraphicsMode.Set(wasEnhanced);
            Mech3.ShaderTwins.Regenerate();
        }
        ctx.Check(true, $"probe ran arm={arm}");
    }

    // The rewrite suite's probe texts, Original (red, three uniforms) or Enhanced (green, one), each
    // with a comment of its own so every key holds its own shader.
    private static string ProbeText(int n, bool after) =>
        "shader_type spatial;\nrender_mode unshaded, cull_disabled;\n// free-busy " + n.ToString(CultureInfo.InvariantCulture) + "\n" + (after
            ? "uniform vec4 probe_a = vec4(0.0);\nvoid fragment() {\n    ALBEDO = vec3(0.0, 1.0, 0.0) + probe_a.rgb * 0.0;\n}\n"
            : "uniform vec4 probe_a = vec4(0.0);\nuniform vec4 probe_b = vec4(0.0);\nuniform vec4 probe_c = vec4(0.0);\n"
                + "void fragment() {\n    ALBEDO = vec3(1.0, 0.0, 0.0) + (probe_a.rgb + probe_b.rgb + probe_c.rgb) * 0.0;\n}\n");

    // A world-bias shader text as the world builder writes it, with a uniform of its own so every
    // text compiles to its own code.
    private static string Text(int round, int i)
    {
        var method = typeof(Mech3.SceneBuilder).GetMethod("BiasShaderCode", BindingFlags.NonPublic | BindingFlags.Static)!;
        var args = method.GetParameters().Select(p => p.ParameterType == typeof(bool)
            ? (object)(p.Name is "textured" or "lit" or "fogged")
            : Activator.CreateInstance(p.ParameterType)!).ToArray();
        string code = (string)method.Invoke(null, args)!;
        int head = code.IndexOf("\nvoid ", StringComparison.Ordinal) + 1;
        return code[..head] + $"uniform float csky_probe_r{round}_s{i} = 0.0;\n" + code[head..];
    }

    // Draws until Godot's pipeline compile count holds for five frames, so the worker threads'
    // compiles have landed before a reading.
    private static int DrawUntilCompiled()
    {
        double last = -1;
        int still = 0;
        int frames = 0;
        while (still < 5 && frames < 600)
        {
            RenderingServer.ForceDraw();
            RenderingServer.ForceSync();
            System.Threading.Thread.Sleep(20);
            frames++;
            double now = Pipelines();
            still = now == last ? still + 1 : 0;
            last = now;
        }
        return frames;
    }

    private static void Settle()
    {
        for (int i = 0; i < 4; i++)
        {
            RenderingServer.ForceDraw();
            RenderingServer.ForceSync();
            System.Threading.Thread.Sleep(50);
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        RenderingServer.ForceSync();
    }

    private static void Read(TestContext ctx, bool free, string step, int round, int frames)
    {
        string line = MemoryCensus.Line(0);
        ctx.Note($"probe free={free} round={round} step={step} frames={frames} {line}");
        Log.Info("test", $"probe free={free} round={round} step={step} frames={frames} {line}");
    }

    private static double Pipelines() =>
        Performance.GetMonitor(Performance.Monitor.PipelineCompilationsCanvas)
        + Performance.GetMonitor(Performance.Monitor.PipelineCompilationsMesh)
        + Performance.GetMonitor(Performance.Monitor.PipelineCompilationsSurface)
        + Performance.GetMonitor(Performance.Monitor.PipelineCompilationsDraw)
        + Performance.GetMonitor(Performance.Monitor.PipelineCompilationsSpecialization);

    private static int Env(string name, int fallback) =>
        int.TryParse(System.Environment.GetEnvironmentVariable(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;
}
