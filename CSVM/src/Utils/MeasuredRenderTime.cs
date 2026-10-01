using System;
using System.Threading;
using Godot;

namespace CSVM.Utils;

/// <summary>
/// A viewport's measured render CPU and GPU times, read every frame without stalling it. Under
/// the separate render thread, Godot's two getters each wait for the previous frame's draw. Read in
/// the process pass, they made that pass wait for the render thread. So a call queued on the render
/// thread publishes the pair, and the frame takes the latest one, a draw older than a synchronous
/// read. On one thread the read is direct. docs/verification.md PERF-43.
/// </summary>
public sealed class MeasuredRenderTime
{
    private readonly Rid _viewport;
    private readonly Callable _publish;
    private long _cpuBits;
    private long _gpuBits;

    /// <summary>Reads <paramref name="viewport"/>, whose render time measuring the caller has
    /// already switched on.</summary>
    public MeasuredRenderTime(Rid viewport)
    {
        _viewport = viewport;
        _publish = Callable.From(Publish);
    }

    /// <summary>The render CPU and GPU times in milliseconds, the latest pair the render thread has
    /// measured.</summary>
    public (double CpuMs, double GpuMs) Read()
    {
        if (RenderingServer.IsOnRenderThread())
        {
            return (RenderingServer.ViewportGetMeasuredRenderTimeCpu(_viewport),
                RenderingServer.ViewportGetMeasuredRenderTimeGpu(_viewport));
        }

        // ⚠ Never read the getters here: on this thread each one waits for the render thread.
        RenderingServer.CallOnRenderThread(_publish);
        return (BitConverter.Int64BitsToDouble(Interlocked.Read(ref _cpuBits)),
            BitConverter.Int64BitsToDouble(Interlocked.Read(ref _gpuBits)));
    }

    // Runs on the render thread, where both getters answer directly.
    private void Publish()
    {
        Interlocked.Exchange(ref _cpuBits,
            BitConverter.DoubleToInt64Bits(RenderingServer.ViewportGetMeasuredRenderTimeCpu(_viewport)));
        Interlocked.Exchange(ref _gpuBits,
            BitConverter.DoubleToInt64Bits(RenderingServer.ViewportGetMeasuredRenderTimeGpu(_viewport)));
    }
}
