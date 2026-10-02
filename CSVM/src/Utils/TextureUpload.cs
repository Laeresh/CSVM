using System.Collections.Generic;
using Godot;

namespace CSVM.Utils;

/// <summary>
/// Every hand-over of an <see cref="Image"/> to a texture, its first picture or a repaint. Under the
/// separate render thread an upload is queued, and the render thread reads the Image later and drops
/// its reference there. A headless run queues a texture's creation too, since the dummy renderer
/// cannot create one on the caller's thread. An Image refilled in place with <c>SetData</c> swaps its
/// buffer under that reader, which then sees an empty or half-written picture.
/// ⚠ Never hand an Image to <see cref="ImageTexture.CreateFromImage"/> or
/// <see cref="ImageTexture.Update"/> directly, and never keep one to refill; go through this class.
/// </summary>
public static class TextureUpload
{
    // Each queued Image, its reference count before the upload was queued, and the one-element array
    // holding its second native reference. Keep says the caller still owns the wrapper.
    private static readonly List<(Image Image, int Held, Godot.Collections.Array Hold, bool Keep)> Pending = new();

    /// <summary>A texture holding <paramref name="pixels"/> as its first picture.</summary>
    public static ImageTexture Create(int width, int height, Image.Format format, byte[] pixels) =>
        Create(Image.CreateFromData(width, height, false, format, pixels));

    /// <summary>A texture holding <paramref name="image"/> as its first picture, mip chain included.
    /// The image is held here until the render thread lets go, then disposed on this thread unless
    /// <paramref name="callerKeeps"/>.
    /// ⚠ Without <paramref name="callerKeeps"/>, hand over a fresh Image and never touch it again.</summary>
    public static ImageTexture Create(Image image, bool callerKeeps = false)
    {
        Release();
        Park(image, callerKeeps);
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>Replaces <paramref name="texture"/>'s pixels, which must keep its size and format.
    /// The bytes are copied here, so the caller may refill <paramref name="pixels"/> at once.
    /// </summary>
    public static void Replace(ImageTexture texture, int width, int height, Image.Format format, byte[] pixels) =>
        Replace(texture, Image.CreateFromData(width, height, false, format, pixels));

    /// <summary>Replaces <paramref name="texture"/>'s pixels with <paramref name="image"/>, mip chain
    /// included, which must keep the texture's size, format and mips. The image is held here until
    /// the render thread lets go.
    /// ⚠ Hand over a fresh Image and never touch it again.</summary>
    public static void Replace(ImageTexture texture, Image image)
    {
        Release();
        Park(image, keep: false);
        texture.Update(image);
    }

    // ⚠ Keep the second reference until the render thread is done. Godot swaps a wrapper's GC handle,
    // unlocked, when the count crosses between 1 and 2. A render-thread release reaching 1 would make
    // that swap there, racing the main thread and the finalizer.
    private static void Park(Image image, bool keep)
    {
        var hold = new Godot.Collections.Array { image };
        Pending.Add((image, image.GetReferenceCount(), hold, keep));
    }

    // Frees, on this thread, every Image the render thread no longer references. Newest first, so
    // an Image a caller handed over twice drops its later hold before the earlier one is judged.
    private static void Release()
    {
        for (int i = Pending.Count - 1; i >= 0; i--)
        {
            var (image, held, hold, keep) = Pending[i];
            if (image.GetReferenceCount() > held)
            {
                continue;
            }

            Pending.RemoveAt(i);
            hold.Dispose();
            if (!keep)
            {
                image.Dispose();
            }
        }
    }
}
