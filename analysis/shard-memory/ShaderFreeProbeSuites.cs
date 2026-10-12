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
