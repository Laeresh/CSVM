using CSVM.Extraction;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The engine half of <see cref="HdTextures"/>: decodes a replacement, and uploads 2D art at its
/// original's size. Godot measures a 2D draw's source rect against a texture's overridden size.
/// Every pixel rect, frame strip, crop and hitbox laid out in the original's pixels then holds.
/// </summary>
public static class HdArt
{
    /// <summary>A replacement's picture, PNG or JPEG by its signature, or null. Every fully
    /// transparent texel comes back black. The pack leaves its green-screen key under alpha 0, and
    /// filtering and the mip chain would bleed it onto the cutout's edges.</summary>
    public static Image? Decode(byte[]? bytes)
    {
        if (bytes == null || bytes.Length < 2)
        {
            return null;
        }

        var image = new Image();
        var read = bytes[0] == 0xFF && bytes[1] == 0xD8 ? image.LoadJpgFromBuffer(bytes) : image.LoadPngFromBuffer(bytes);
        if (read != Error.Ok || image.IsEmpty())
        {
            return null;
        }

        if (image.GetFormat() == Image.Format.Rgba8)
        {
            byte[] data = image.GetData();
            for (int i = 0; i < data.Length; i += 4)
            {
                if (data[i + 3] == 0)
                {
                    data[i] = data[i + 1] = data[i + 2] = 0;
                }
            }

            image.SetData(image.GetWidth(), image.GetHeight(), false, Image.Format.Rgba8, data);
        }

        return image;
    }

    /// <summary>The texture of the <c>rimage</c> art <paramref name="name"/>, from its replacement
    /// where one exists, or null when <paramref name="rimage"/> lacks the art.</summary>
    public static ImageTexture? Rimage(TextureSet rimage, string name) =>
        rimage.Find(name) is { } art
            ? Texture(Decode(HdTextures.Current.ForRimage(name)), TextureArchive.ImageOf(art))
            : null;

    /// <summary>The texture for <paramref name="original"/>, drawn from <paramref name="hd"/> when it
    /// is not null.</summary>
    public static ImageTexture Texture(Image? hd, Image original)
    {
        if (hd == null)
        {
            return TextureUpload.Create(original);
        }

        var size = original.GetSize();
        original.Dispose();
        var texture = TextureUpload.Create(hd);
        texture.SetSizeOverride(size);
        return texture;
    }
}
