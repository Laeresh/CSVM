using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Tooling;

/// <summary>
/// <c>--debug-shaders</c>: what the renderer compiles and when it stalls for it. It logs a census
/// of the distinct shaders the tree draws, by family, at a settled frame and after every live
/// switch. It logs the wall time and pipeline compilations of the frames after a switch. It logs
/// every frame over 33 ms with the pipelines it compiled. Off unless the flag is given.
/// </summary>
internal static class ShaderDiagnostics
{
    // The sim frame the settled census is taken at, past the load and the first draws.
    private const long CensusFrame = 240;

    // Frames logged after a switch, which is where a lazily compiled pipeline lands.
    private const int FramesAfterSwitch = 12;

    // The frame budget a first-use stall is counted against.
    private const double SlowFrameMs = 33.0;

    // The node kind a non-registered shader was found on, filled by Walk.
    private static readonly Dictionary<Shader, string> FamilyByOwner = new();

    // The latest pipeline counts the render thread published.
    private static readonly long[] Published = new long[4];

    private static readonly List<string> AfterSwitch = new();
    private static long _lastUsec;
    private static int _framesSinceSwitch = -1;
    private static long[] _switchMark = new long[4];
    private static bool _censusDone;
    private static int _frames;
    private static int _slow;
    private static double _slowMs;
    private static double _maxMs;

    /// <summary>Gets or sets a value indicating whether the flag is on.</summary>
    public static bool Enabled { get; set; }

    /// <summary>One frame of the running session, called by the launcher after its own work.</summary>
    public static void Tick(Node root, long simFrame)
    {
        if (!Enabled)
            return;
        long now = (long)Time.GetTicksUsec();
        double ms = _lastUsec == 0 ? 0 : (now - _lastUsec) / 1000.0;
        _lastUsec = now;
        var pipelines = Pipelines();
        if (simFrame > 0 && ms > 0)
        {
            _frames++;
            _maxMs = System.Math.Max(_maxMs, ms);
            if (ms > SlowFrameMs)
            {
                _slow++;
                _slowMs += ms;
                Log.Info("perf", $"shader-diag slow frame sim_frame={simFrame} ms={ms:0.0} pipelines={Print(pipelines)}");
            }
        }
        if (_framesSinceSwitch >= 0)
        {
            AfterSwitch.Add($"{ms:0.0}");
            if (++_framesSinceSwitch >= FramesAfterSwitch)
            {
                var delta = pipelines.Zip(_switchMark, (a, b) => a - b).ToArray();
                Log.Info("perf", $"shader-diag after switch frames_ms=[{string.Join(" ", AfterSwitch)}] pipelines_added={Print(delta)}");
                _framesSinceSwitch = -1;
                Census(root, "after switch");
            }
        }
        if (!_censusDone && simFrame >= CensusFrame)
        {
            _censusDone = true;
            Census(root, $"settled at sim frame {simFrame}");
        }
        if (simFrame > 0 && simFrame % 600 == 0)
            Summary();
    }

    /// <summary>A live switch is about to run. The marker line lets the engine's verbose lines
    /// between it and the after-switch line read as the switch's.</summary>
    public static void BeginSwitch()
    {
        if (Enabled)
            Log.Info("perf", $"shader-diag switch begins toward {(GraphicsMode.Enhanced ? "original" : "enhanced")}");
    }

    /// <summary>A live switch just ran; the frames after it are logged.</summary>
    public static void NoteSwitch()
    {
        if (!Enabled)
            return;
        AfterSwitch.Clear();
        _framesSinceSwitch = 0;
        _switchMark = Pipelines();
    }

    /// <summary>The running totals: frames seen, frames over budget and the worst one.</summary>
    public static void Summary()
    {
        if (!Enabled)
            return;
        Log.Info("perf", $"shader-diag summary frames={_frames} over_33ms={_slow} over_33ms_total_ms={_slowMs:0.0} max_ms={_maxMs:0.0} pipelines={Print(Pipelines())}");
    }

    // Distinct Shader objects, and distinct texts, the tree's materials hold, by family.
    private static void Census(Node root, string when)
    {
        var shaders = new HashSet<Shader>();
        Walk(root, shaders);
        var byFamily = new SortedDictionary<string, (int Shaders, HashSet<string> Texts)>(System.StringComparer.Ordinal);
        foreach (var (shader, family) in shaders.Select(s => (s, Family(s))))
        {
            if (!byFamily.TryGetValue(family, out var entry))
                entry = (0, new HashSet<string>(System.StringComparer.Ordinal));
            entry.Texts.Add(shader.Code);
            byFamily[family] = (entry.Shaders + 1, entry.Texts);
        }
        var registered = ShaderTwins.Registered().GroupBy(r => r.Family)
            .Select(g => $"{g.Key}:{g.Count()}");
        Log.Info("perf", $"shader-diag census {when} mode={(GraphicsMode.Enhanced ? "enhanced" : "original")} drawn={shaders.Count} texts={byFamily.Values.Sum(v => v.Texts.Count)} families=[{string.Join(" ", byFamily.Select(kv => $"{kv.Key}:{kv.Value.Shaders}/{kv.Value.Texts.Count}"))}] registered=[{string.Join(" ", registered)}] pipelines={Print(Pipelines())}");
        Log.Info("perf", $"shader-diag keys {ShaderTwins.SharedCensus()} made={ShaderTwins.Made} reused={ShaderTwins.Reused}");
    }

    private static string Family(Shader shader)
    {
        if (ShaderTwins.FamilyOf(shader) is { } family)
            return family;
        return FamilyByOwner.TryGetValue(shader, out var owner) ? owner : "other";
    }

    private static void Walk(Node node, HashSet<Shader> into)
    {
        if (node is GeometryInstance3D geometry)
        {
            string owner = OwnerFamily(geometry);
            foreach (var material in Materials(geometry))
            {
                for (var m = material; m != null; m = m.NextPass)
                {
                    if (m is ShaderMaterial { Shader: { } shader } && into.Add(shader) && ShaderTwins.FamilyOf(shader) == null)
                        FamilyByOwner[shader] = owner;
                }
            }
        }
        for (int i = 0, count = node.GetChildCount(); i < count; i++)
            Walk(node.GetChild(i), into);
    }

    // The nearest node in the chain that is one of this codebase's own types names the family.
    private static string OwnerFamily(Node node)
    {
        for (var at = node; at != null; at = at.GetParent())
        {
            string ns = at.GetType().Namespace ?? string.Empty;
            if (ns.StartsWith("CSVM", System.StringComparison.Ordinal))
                return at.GetType().Name;
            if (at.Name == "ground_shadows")
                return "ground_shadows";
        }
        return node.GetType().Name;
    }

    private static IEnumerable<Material?> Materials(GeometryInstance3D geometry)
    {
        yield return geometry.MaterialOverride;
        yield return geometry.MaterialOverlay;
        Mesh? mesh = geometry switch
        {
            MeshInstance3D mi => mi.Mesh,
            MultiMeshInstance3D mmi => mmi.Multimesh?.Mesh,
            _ => null,
        };
        if (mesh == null)
            yield break;
        for (int s = 0; s < mesh.GetSurfaceCount(); s++)
        {
            yield return geometry is MeshInstance3D m ? m.GetSurfaceOverrideMaterial(s) : null;
            yield return mesh.SurfaceGetMaterial(s);
        }
    }

    // The engine's pipeline compilations so far: mesh load, surface cache, draw, specialization.
    // ⚠ Never read the getter on the main thread under the separate render thread: it waits for the
    // previous draw. A call queued there publishes the counts, as MeasuredRenderTime does.
    private static long[] Pipelines()
    {
        if (RenderingServer.IsOnRenderThread())
        {
            PublishPipelines();
        }
        else
        {
            RenderingServer.CallOnRenderThread(Callable.From(PublishPipelines));
        }
        lock (Published)
        {
            return (long[])Published.Clone();
        }
    }

    private static void PublishPipelines()
    {
        var read = new[]
        {
            (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.PipelineCompilationsMesh),
            (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.PipelineCompilationsSurface),
            (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.PipelineCompilationsDraw),
            (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.PipelineCompilationsSpecialization),
        };
        lock (Published)
        {
            read.CopyTo(Published, 0);
        }
    }

    private static string Print(long[] p) => $"mesh:{p[0]},surface:{p[1]},draw:{p[2]},spec:{p[3]}";
}
