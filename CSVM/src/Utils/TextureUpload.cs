using System.Collections.Generic;
using Godot;

namespace CSVM.Utils;

/// <summary>
/// Pixels for a texture the game repaints while it runs, one <see cref="Image"/> per upload. Under
/// the separate render thread an update is queued, and the render thread reads the Image it was
/// handed later. An Image refilled in place with <c>SetData</c> swaps its buffer under that reader,
/// which then sees an empty or half-written picture.
/// ⚠ Never keep an Image to refill for <see cref="ImageTexture.Update"/>; upload through
/// <see cref="Replace"/>.
/// </summary>
public static class TextureUpload
{
    // Each queued Image, held by a native reference as well as its C# wrapper, and freed here once
    // the render thread has let go. The references each Image had before its update was queued.
    private static readonly Godot.Collections.Array InFlight = new();
    private static readonly List<(Image Image, int Held)> Pending = new();

    /// <summary>A texture holding <paramref name="pixels"/> as its first picture.</summary>
    public static ImageTexture Create(int width, int height, Image.Format format, byte[] pixels)
    {
        using var image = Image.CreateFromData(width, height, false, format, pixels);
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>Replaces <paramref name="texture"/>'s pixels, which must keep its size and format.
    /// The bytes are copied here, so the caller may refill <paramref name="pixels"/> at once.
    /// </summary>
    public static void Replace(ImageTexture texture, int width, int height, Image.Format format, byte[] pixels)
    {
        Release();
        var image = Image.CreateFromData(width, height, false, format, pixels);

        // ⚠ Keep the second reference until the render thread is done. A release leaving the wrapper
        // alone makes Godot swap its GC handle on that thread. Racing the main thread, that corrupts
        // the managed heap.
        InFlight.Add(image);
        Pending.Add((image, image.GetReferenceCount()));
        texture.Update(image);
    }

    /// <summary>Replaces <paramref name="texture"/>'s pixels with <paramref name="image"/>, mip chain
    /// included, which must keep the texture's size, format and mips. The image is held here until
    /// the render thread lets go.
    /// ⚠ Hand over a fresh Image and never touch it again.</summary>
    public static void Replace(ImageTexture texture, Image image)
    {
        Release();
        InFlight.Add(image);
        Pending.Add((image, image.GetReferenceCount()));
        texture.Update(image);
    }

    // Frees, on this thread, every Image the render thread no longer references.
    private static void Release()
    {
        for (int i = Pending.Count - 1; i >= 0; i--)
        {
            var (image, held) = Pending[i];
            if (image.GetReferenceCount() > held)
            {
                continue;
            }

            InFlight.RemoveAt(i);
            Pending.RemoveAt(i);
            image.Dispose();
        }
    }
}
