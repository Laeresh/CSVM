using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// A plane mod's livery tint over the materials it names. Its mask's R, G and B take the scheme's colours
/// 1, 2 and 3. A greyscale detail map shades them. The engine-free per-texel rule is
/// <see cref="Composite"/>. Format: docs/formats/mod-planes.md, "Paint".
/// </summary>
public sealed class ModTint
{
    private readonly HashSet<string> _materials;
    private readonly int _maskWidth;
    private readonly int _maskHeight;
    private readonly byte[] _mask;
    private readonly int _detailWidth;
    private readonly int _detailHeight;
    private readonly byte[] _detail;

    /// <param name="mask">RGB bytes, three per texel, rows top-down.</param>
    /// <param name="detail">Grey bytes, one per texel, rows top-down.</param>
    public ModTint(IEnumerable<string> materials, int maskWidth, int maskHeight, byte[] mask, int detailWidth, int detailHeight, byte[] detail)
    {
        if (maskWidth <= 0 || maskHeight <= 0 || mask.Length < maskWidth * maskHeight * 3
            || detailWidth <= 0 || detailHeight <= 0 || detail.Length < detailWidth * detailHeight)
        {
            throw new InvalidDataException("the tint images are empty or shorter than their size");
        }

        _materials = new HashSet<string>(materials, StringComparer.OrdinalIgnoreCase);
        (_maskWidth, _maskHeight, _mask) = (maskWidth, maskHeight, mask);
        (_detailWidth, _detailHeight, _detail) = (detailWidth, detailHeight, detail);
    }

    /// <summary>The tint of <paramref name="materials"/> from two PNG files' bytes. Throws
    /// <see cref="InvalidDataException"/> for a file that does not decode.</summary>
    public static ModTint Decode(IEnumerable<string> materials, byte[] maskPng, byte[] detailPng)
    {
        var mask = Png(maskPng, Image.Format.Rgb8, "mask");
        var detail = Png(detailPng, Image.Format.L8, "detail");
        return new ModTint(materials, mask.GetWidth(), mask.GetHeight(), mask.GetData(), detail.GetWidth(), detail.GetHeight(), detail.GetData());
    }

    /// <summary>Whether the material <paramref name="name"/> wears the tint, matched without case.</summary>
    public bool Covers(string name) => _materials.Contains(name);

    /// <summary>Tints the base texture <paramref name="rgba"/> (four bytes per texel, top-down) in place.
    /// Each texel becomes <c>base * (1 - cover) + detail / 128 * (r * c1 + g * c2 + b * c3)</c>. Cover is
    /// the mask's channel sum capped at 1, and a larger sum is shared out by channel. Alpha stays the
    /// base's. The mask and detail are sampled nearest at the base's size.</summary>
    public void Composite(byte[] rgba, int width, int height, Color c1, Color c2, Color c3)
    {
        for (int y = 0; y < height; y++)
        {
            int my = y * _maskHeight / height;
            int dy = y * _detailHeight / height;
            for (int x = 0; x < width; x++)
            {
                int m = ((my * _maskWidth) + (x * _maskWidth / width)) * 3;
                float r = _mask[m] / 255f, g = _mask[m + 1] / 255f, b = _mask[m + 2] / 255f;
                float sum = r + g + b;
                if (sum <= 0f)
                {
                    continue;
                }

                float cover = Math.Min(sum, 1f);
                float share = cover / sum;
                // 128 is the neutral grey, so a flat detail map shows the swatch colour itself.
                float d = _detail[(dy * _detailWidth) + (x * _detailWidth / width)] / 128f * share * 255f;
                int at = ((y * width) + x) * 4;
                rgba[at] = Mix(rgba[at], d * ((r * c1.R) + (g * c2.R) + (b * c3.R)), cover);
                rgba[at + 1] = Mix(rgba[at + 1], d * ((r * c1.G) + (g * c2.G) + (b * c3.G)), cover);
                rgba[at + 2] = Mix(rgba[at + 2], d * ((r * c1.B) + (g * c2.B) + (b * c3.B)), cover);
            }
        }
    }

    private static byte Mix(byte under, float paint, float cover) =>
        (byte)Math.Clamp((int)((under * (1f - cover)) + paint + 0.5f), 0, 255);

    private static Image Png(byte[] bytes, Image.Format format, string what)
    {
        var image = new Image();
        if (image.LoadPngFromBuffer(bytes) != Error.Ok || image.IsEmpty())
        {
            throw new InvalidDataException($"its tint {what} does not decode as PNG");
        }

        image.Convert(format);
        return image;
    }
}
