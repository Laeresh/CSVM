using System;
using System.Collections.Generic;
using CSVM.Mech3;

namespace CSVM.Tooling;

/// <summary>
/// The synthetic run's chapter texture archive, built in memory. It holds one opaque record per
/// entry of <see cref="Entries"/> at that size, its pixels a checker of the name's colour.
/// A texture is added by adding an entry. Engine-free, so <c>CSVM.Tests</c> builds the same set.
/// </summary>
public static class SyntheticTextures
{
    /// <summary>The chapter the archive is built for, the one every plane-only path reads.</summary>
    public const string Chapter = "C1";

    /// <summary>The invented textures by name and size: the probe plane's skin and panel, its
    /// tracer, and the three smoke frames the invented effects name.</summary>
    public static readonly IReadOnlyList<(string Name, int Width, int Height)> Entries = new[]
    {
        ("probe_skin", 64, 64),
        ("probe_panel", 32, 32),
        ("probe_tracer", 16, 8),
        ("smoke101", 32, 32),
        ("smoke102", 32, 32),
        ("smoke103", 32, 32),
    };

    private static readonly Lazy<TextureSet> Set = new(Build);

    /// <summary>The archive of <paramref name="chapter"/>: the invented set for <see cref="Chapter"/>,
    /// empty for every other chapter, as the synthetic data carries no other.</summary>
    public static TextureSet For(string chapter) =>
        string.Equals(chapter, Chapter, StringComparison.OrdinalIgnoreCase) ? Set.Value : TextureSet.Empty;

    /// <summary>A checker of the name's own colour against mid grey, RGB with no padding. Distinct
    /// per name, so a capture shows which texture drew where.</summary>
    public static byte[] Checker(string name, int width, int height)
    {
        // The checker square's side in texels; every entry's size is a multiple of it.
        const int Square = 8;
        uint hash = 2166136261;
        foreach (char c in name)
        {
            hash = (hash ^ c) * 16777619;
        }

        byte r = (byte)(64 + (hash & 0x7F)), g = (byte)(64 + ((hash >> 8) & 0x7F)), b = (byte)(64 + ((hash >> 16) & 0x7F));
        var rgb = new byte[width * height * 3];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = ((y * width) + x) * 3;
                bool lit = ((x / Square) + (y / Square)) % 2 == 0;
                rgb[i] = lit ? r : (byte)128;
                rgb[i + 1] = lit ? g : (byte)128;
                rgb[i + 2] = lit ? b : (byte)128;
            }
        }

        return rgb;
    }

    private static TextureSet Build()
    {
        var images = new List<TextureImage>();
        foreach (var (name, width, height) in Entries)
        {
            images.Add(new TextureImage(name, null, width, height, TextureArchive.AlphaClass.None, 0, false, Checker(name, width, height)));
        }

        return new TextureSet("c1/texture", images);
    }
}
