using System.IO;

namespace CSVM.Mech3;

/// <summary>
/// The one door menu art is loaded through: a file in, a decoded <see cref="TgaImage"/> or null
/// out, with the decoder picked from the extension. A screen names the file the game ships and
/// stops caring what format it is. That let the hangar's art seam stay TGA-only while half the art
/// beside it shipped as PNG.
///
/// <para>An extension with no decoder returns null (<c>.JPG</c>, <c>.BM</c> and <c>.TIF</c> in the
/// UI archive's <c>ASSETS/GRAPHICS</c>). So does a file its decoder cannot read. Null is the
/// correct answer: a stand-in picture on a fidelity screen reads as a verdict about the original,
/// so nothing here ever substitutes one.</para>
/// </summary>
public static class ArtImage
{
    /// <summary>Reads and decodes one art file, or null when it is absent, unreadable, or in a
    /// format no decoder here covers.
    /// ⚠ Do not add a JPEG decoder or an extract-time PNG sidecar for the 26 JPEG pictures: the
    /// shell's own loader reads all of them, so a screen that wants one names it as a board
    /// picture instead, through <c>UI/Boards/ComposedBoardView.cs</c>.</summary>
    public static TgaImage? TryLoad(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => PngImage.TryLoad(path),
            ".tga" => TgaImage.TryLoad(path),
            _ => null,
        };

    /// <summary>The same decode over bytes already read, by the extension of
    /// <paramref name="name"/>; null for null bytes.</summary>
    public static TgaImage? TryDecode(byte[]? bytes, string name) =>
        bytes == null ? null : Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".png" => PngImage.Decode(bytes),
            ".tga" => TgaImage.Decode(bytes),
            _ => null,
        };
}
