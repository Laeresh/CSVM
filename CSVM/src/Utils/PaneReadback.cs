using System;
using System.Threading.Tasks;
using Godot;

namespace CSVM.Utils;

/// <summary>Asks for one Danger Zone photograph without waiting for it. Answers false, and never
/// calls <paramref name="landed"/>, when there is nothing to read; otherwise
/// <paramref name="landed"/> runs exactly once, on any thread, with the frame or with null when the
/// readback failed.</summary>
public delegate bool PaneRequest(Action<Image?> landed);

/// <summary>
/// A viewport's pixels read back off the frame path, behind every live <see cref="PaneRequest"/>.
/// The request copies the viewport's render target through
/// <see cref="RenderingDevice.TextureGetDataAsync"/>, which delivers the bytes a few frames later
/// (the device's frame queue), and the image is built and handed over on a worker thread, so the
/// frame that asks pays for neither the GPU stall nor the decode. A run with no rendering device
/// (the compatibility renderer, a headless run) or a render target in a format this does not
/// decode falls back to the synchronous <see cref="Texture2D.GetImage"/>.
/// </summary>
public static class PaneReadback
{
    /// <summary>Requests <paramref name="viewport"/>'s last rendered frame, the frame
    /// <see cref="Texture2D.GetImage"/> would have answered at this call. An opaque viewport's frame
    /// lands as <see cref="Image.Format.Rgb8"/>, which is what that call answers for it.</summary>
    public static bool Request(Viewport? viewport, Action<Image?> landed)
    {
        if (viewport == null || !GodotObject.IsInstanceValid(viewport))
        {
            return false;
        }

        bool opaque = !viewport.TransparentBg;
        if (RenderingServer.GetRenderingDevice() is { } device
            && RenderingServer.TextureGetRdTexture(RenderingServer.ViewportGetTexture(viewport.GetViewportRid())) is { IsValid: true } texture
            && device.TextureGetFormat(texture) is { } format
            && IsRgba8(format.Format))
        {
            int width = (int)format.Width;
            int height = (int)format.Height;
            var err = device.TextureGetDataAsync(texture, 0,
                Callable.From((byte[] data) => Develop(() => Build(data, width, height, opaque), landed)));
            if (err == Error.Ok)
            {
                return true;
            }

            Log.Warn("core", $"pane readback: async request refused ({err}), reading synchronously");
        }

        var image = viewport.GetTexture()?.GetImage();
        if (image == null || image.GetWidth() <= 0 || image.GetHeight() <= 0)
        {
            return false;
        }

        Develop(() => image, landed);
        return true;
    }

    // The render target formats the bytes can be taken as-is from. Anything else keeps Godot's
    // own conversion by reading synchronously.
    private static bool IsRgba8(RenderingDevice.DataFormat format) =>
        format is RenderingDevice.DataFormat.R8G8B8A8Unorm or RenderingDevice.DataFormat.R8G8B8A8Srgb;

    // ⚠ Never let an exception escape a worker: an unobserved task swallows it, and the caller's
    // record would wait forever for a frame that is never handed over.
    private static void Develop(Func<Image?> build, Action<Image?> landed) => Task.Run(() =>
    {
        Image? image = null;
        try
        {
            image = build();
        }
        catch (Exception e)
        {
            Log.Error("core", $"pane readback: could not build the frame ({e.Message})");
        }

        try
        {
            landed(image);
        }
        catch (Exception e)
        {
            Log.Error("core", $"pane readback: the landed frame's consumer failed ({e})");
        }
    });

    private static Image? Build(byte[] data, int width, int height, bool opaque)
    {
        int size = width * height * 4;
        if (width <= 0 || height <= 0 || data.Length < size)
        {
            Log.Warn("core", $"pane readback: {data.Length} bytes for a {width}x{height} frame, nothing landed");
            return null;
        }

        var image = Image.CreateFromData(width, height, false, Image.Format.Rgba8, data.AsSpan(0, size));
        if (opaque)
        {
            image.Convert(Image.Format.Rgb8);
        }

        return image;
    }
}
