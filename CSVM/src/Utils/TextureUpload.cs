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
        // Disposing drops only this wrapper's reference: a queued update keeps the Image alive.
        using var image = Image.CreateFromData(width, height, false, format, pixels);
        texture.Update(image);
    }
}
