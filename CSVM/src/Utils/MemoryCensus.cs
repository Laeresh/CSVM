using System;
using System.Diagnostics;
using Godot;

namespace CSVM.Utils;

/// <summary>The <c>--debug-mem</c> readout: one line splitting the process's memory by holder. The
/// managed heap, Godot's static allocations, textures and buffers sit beside the private bytes.
/// What each figure covers is this module's docs/architecture entry.</summary>
public static class MemoryCensus
{
    private const double Mb = 1024.0 * 1024.0;

    /// <summary>The figures as <c>key=value</c> terms in megabytes, ending with
    /// <paramref name="shaders"/>, the shader cache's count, which this layer cannot read. Opens a
    /// process handle, so it is for a suite boundary or a once-a-second tick, never every frame.</summary>
    public static string Line(int shaders)
    {
        using var self = Process.GetCurrentProcess();
        // Peak paged bytes is the peak commit charge, the peak private bytes a ledger reserves.
        // The working set is reported only to show how much Windows trimmed.
        return $"priv_mb={self.PrivateMemorySize64 / Mb:0} peak_priv_mb={self.PeakPagedMemorySize64 / Mb:0}"
            + $" ws_mb={self.WorkingSet64 / Mb:0} gc_mb={GC.GetTotalMemory(false) / Mb:0}"
            + $" gc_committed_mb={GC.GetGCMemoryInfo().TotalCommittedBytes / Mb:0}"
            + $" static_mb={Read(Performance.Monitor.MemoryStatic) / Mb:0}"
            + $" tex_mb={Read(Performance.Monitor.RenderTextureMemUsed) / Mb:0}"
            + $" buf_mb={Read(Performance.Monitor.RenderBufferMemUsed) / Mb:0}"
            + $" vid_mb={Read(Performance.Monitor.RenderVideoMemUsed) / Mb:0}"
            + $" objects={Read(Performance.Monitor.ObjectCount):0}"
            + $" resources={Read(Performance.Monitor.ObjectResourceCount):0}"
            + $" nodes={Read(Performance.Monitor.ObjectNodeCount):0}"
            + $" pipelines={Pipelines():0} shaders={shaders}";
    }

    // Every pipeline Godot has compiled so far, beside the shader cache's count of compiled texts:
    // private bytes beyond the named holders track the second (analysis/shard-memory/FINDINGS.md).
    private static double Pipelines() =>
        Read(Performance.Monitor.PipelineCompilationsCanvas)
        + Read(Performance.Monitor.PipelineCompilationsMesh)
        + Read(Performance.Monitor.PipelineCompilationsSurface)
        + Read(Performance.Monitor.PipelineCompilationsDraw)
        + Read(Performance.Monitor.PipelineCompilationsSpecialization);

    private static double Read(Performance.Monitor monitor) => Performance.GetMonitor(monitor);
}
