using System;
using System.Collections.Generic;
using System.Threading;
using Godot;

namespace CSVM.Flight.Camera;

/// <summary>The spyglass picture: a square viewport rendering the SHARED world through its own
/// camera. <see cref="Hud.TargetHud"/> draws it as a disc at the off-screen marker's anchor.
/// A headset shows it once for both eyes on its head ring (<see cref="Hud.XrSpyglassDisc"/>).
/// The world is inherited rather than owned, so the target is the one in play and wears the flown
/// zone's fog. The pane's camera lends its clip planes and cull mask, the picture being its view
/// less the pilot's own airframe (<see cref="DiscMask"/>).
/// One per pane, built with the HUD, rendering only while
/// <see cref="Aim"/> is being called. Where it looks and how wide is <see cref="Spyglass"/>'s.
/// </summary>
public sealed partial class SpyglassView : SubViewport
{
    /// <summary>The smallest internal side, in pixels, the picture renders at.
    /// ⚠ Do not lower it under 64. Godot's ambient-occlusion depth chain takes a quarter of this
    /// size with five mip levels, and a smaller buffer fails to allocate, which crashes the Deck.</summary>
    public const int MinInternalSide = 64;

    // Every picture in the tree, for the perf census.
    private static readonly List<SpyglassView> InTree = new();

    private Camera3D _camera = null!;
    private Camera3D? _pane;
    private uint _ownLayer;
    private bool _live;
    private Counters? _counters;

    /// <summary>Whether the picture is rendering this frame.</summary>
    public bool Live => _live;

    /// <summary>Where the picture's camera stands and how it is turned, as <see cref="Aim"/> last
    /// left it. Read by the suite, which is how the eye's source is pinned.</summary>
    public Transform3D Eye => _camera.Transform;

    /// <summary>The cull mask the picture's camera renders through, the pane's less the pilot's
    /// own airframe. Read by the suite.</summary>
    public uint DiscCullMask => _camera.CullMask;

    /// <summary>Whether a headset shows this picture, on the head ring rather than the pane. Set, the
    /// camera also leaves out what the headset draws over the view (<see cref="DiscMask"/>).</summary>
    public bool Headset { get; set; }

    /// <summary>The picture at <paramref name="pane"/>'s side: idle until the first
    /// <see cref="Aim"/>, and hung on the HUD control that draws it, which is what keeps it inside
    /// that pane's viewport and therefore in that pane's world. <paramref name="ownLayer"/> is the
    /// visual layer this pane's own aeroplane is drawn on, which the picture never shows.</summary>
    public static SpyglassView Build(Camera3D? pane, uint ownLayer)
    {
        int side = RenderSide(Mathf.RoundToInt(Spyglass.RefWindow));
        var view = new SpyglassView
        {
            Name = "spyglass_view",
            Size = new Vector2I(side, side),
            RenderTargetUpdateMode = UpdateMode.Disabled,
            HandleInputLocally = false,
            AudioListenerEnable3D = false,
            Msaa3D = (Msaa)(int)ProjectSettings.GetSetting(
                "rendering/anti_aliasing/quality/msaa_3d", 0).AsInt32(),
        };
        // The picture is the pane's own view through a longer lens, so it takes the mode's render
        // flags as the pane does rather than reading sharper or softer than the view around it.
        Utils.ViewportQuality.Apply(view);
        view._pane = pane;
        view._ownLayer = ownLayer;
        view._camera = new Camera3D { Name = "spyglass_camera", Current = true };
        view.AddChild(view._camera);
        return view;
    }

    /// <summary>The picture's cull mask: <paramref name="paneMask"/> less the pilot's own airframe's
    /// layer (<see cref="UI.Boards.SplitScreen.OwnAirframeLayer"/>) and the world flare's band. Under
    /// <paramref name="enhanced"/> it takes the shadowless sun (<see cref="SpyglassSun"/>) and the flat
    /// sea (<see cref="UI.Boards.SplitScreen.FlatSeaLayer"/>). <paramref name="headset"/> drops the
    /// headset's two layers, whose quads ride any camera. ⚠ NOT decoded, and do not put the airframe back on the
    /// decode's authority: the original's update touches no per-object visibility (docs/org/spyglass.md).</summary>
    public static uint DiscMask(uint paneMask, uint ownLayer, bool enhanced, bool headset = false)
    {
        uint mask = paneMask & ~UI.Boards.SplitScreen.WorldFlareBand;
        if (headset)
        {
            mask &= ~(UI.Boards.SplitScreen.HeadsetLayer | UI.Boards.SplitScreen.HeadOverlayLayer);
        }

        return enhanced
            ? (mask & ~ownLayer & ~UI.Boards.SplitScreen.SunLayer)
                | UI.Boards.SplitScreen.SpyglassSunLayer | UI.Boards.SplitScreen.FlatSeaLayer
            : mask & ~ownLayer;
    }

    /// <summary>The square the picture renders at for a disc <paramref name="side"/> pixels across.
    /// Under Enhanced Graphics it is raised so the internal buffer never falls under
    /// <see cref="MinInternalSide"/>; the disc samples by normalised UV, so that only supersamples
    /// it. The faithful path runs no occlusion pass and keeps the disc's own size.</summary>
    public static int RenderSide(int side) => Utils.GraphicsMode.Enhanced
        ? Mathf.Max(side, Mathf.CeilToInt(MinInternalSide / Utils.RenderScaleSetting.Scale))
        : side;

    /// <summary>The pictures rendering now, and the visible and shadow draw calls, the visible
    /// primitives and the GPU milliseconds the renderer last spent on them, summed. The
    /// <c>--perf</c> readout's spyglass line, since each disc is a viewport of its own whose share
    /// no frame-wide counter separates. The counts trail the frame by a draw (<see cref="Counters"/>).
    /// </summary>
    public static (int Live, long Draws, long ShadowDraws, long Prims, double GpuMs) Census()
    {
        int live = 0;
        long draws = 0, shadowDraws = 0, prims = 0;
        double gpuMs = 0;
        foreach (var view in InTree)
        {
            if (!view._live)
            {
                continue;
            }

            view._counters ??= new Counters(view.GetViewportRid());
            var read = view._counters.Read();
            live++;
            draws += read.Draws;
            shadowDraws += read.ShadowDraws;
            prims += read.Prims;
            gpuMs += read.GpuMs;
        }

        return (live, draws, shadowDraws, prims, gpuMs);
    }

    /// <summary>Point the picture and start it rendering. <paramref name="side"/> is the disc's
    /// drawn diameter in device pixels, so the texture is rasterised at the size it is shown at.
    /// </summary>
    public void Aim(Transform3D pose, float fovDeg, int side)
    {
        side = RenderSide(side);
        if (side > 0 && Size.X != side)
        {
            Size = new Vector2I(side, side);
        }

        _camera.Transform = pose;
        _camera.Fov = fovDeg;
        if (_pane != null && GodotObject.IsInstanceValid(_pane))
        {
            // The zone apply writes the pane camera's clip pair and the pane owns the cull mask a
            // splitscreen seat draws through; the original hands camera 2 both alongside camera 1.
            _camera.Near = _pane.Near;
            _camera.Far = _pane.Far;
            _camera.CullMask = DiscMask(_pane.CullMask, _ownLayer, Utils.GraphicsMode.Enhanced, Headset);
        }

        RenderTargetUpdateMode = UpdateMode.Always;
        _live = true;
    }

    /// <summary>Stop rendering. The gates are re-answered every frame, so this is called on every
    /// frame the disc is down and must stay cheap when it is already idle.</summary>
    public void Idle()
    {
        if (!_live)
        {
            return;
        }

        RenderTargetUpdateMode = UpdateMode.Disabled;
        _live = false;
    }

    /// <inheritdoc/>
    public override void _EnterTree() => InTree.Add(this);

    /// <inheritdoc/>
    public override void _ExitTree() => InTree.Remove(this);

    // One picture's census counters, published from the render thread as Utils.MeasuredRenderTime
    // publishes the root viewport's times. Built at the first census, so only a run that reads one
    // measures the picture's GPU time.
    // ⚠ Never call the viewport's render-info getter from the main thread. Under the separate render
    // thread each call waits for it, stalling every frame a disc renders.
    private sealed class Counters
    {
        private readonly Rid _viewport;
        private readonly Callable _publish;
        private long _draws;
        private long _shadowDraws;
        private long _prims;
        private long _gpuBits;

        public Counters(Rid viewport)
        {
            _viewport = viewport;
            RenderingServer.ViewportSetMeasureRenderTime(viewport, true);
            _publish = Callable.From(Publish);
        }

        public (long Draws, long ShadowDraws, long Prims, double GpuMs) Read()
        {
            if (RenderingServer.IsOnRenderThread())
            {
                Publish();
            }
            else
            {
                RenderingServer.CallOnRenderThread(_publish);
            }

            return (Interlocked.Read(ref _draws), Interlocked.Read(ref _shadowDraws), Interlocked.Read(ref _prims),
                BitConverter.Int64BitsToDouble(Interlocked.Read(ref _gpuBits)));
        }

        // Runs on the render thread, where every getter answers directly.
        private void Publish()
        {
            Interlocked.Exchange(ref _draws, RenderingServer.ViewportGetRenderInfo(_viewport,
                RenderingServer.ViewportRenderInfoType.Visible, RenderingServer.ViewportRenderInfo.DrawCallsInFrame));
            Interlocked.Exchange(ref _shadowDraws, RenderingServer.ViewportGetRenderInfo(_viewport,
                RenderingServer.ViewportRenderInfoType.Shadow, RenderingServer.ViewportRenderInfo.DrawCallsInFrame));
            Interlocked.Exchange(ref _prims, RenderingServer.ViewportGetRenderInfo(_viewport,
                RenderingServer.ViewportRenderInfoType.Visible, RenderingServer.ViewportRenderInfo.PrimitivesInFrame));
            Interlocked.Exchange(ref _gpuBits,
                BitConverter.DoubleToInt64Bits(RenderingServer.ViewportGetMeasuredRenderTimeGpu(_viewport)));
        }
    }
}
