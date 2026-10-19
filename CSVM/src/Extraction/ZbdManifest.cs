using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CSVM.Extraction;

/// <summary>The 184 ZBD archives a complete install holds, the same on the English install and the
/// German disc. <see cref="Missing"/> is checked before the ZBD half runs, since the walk extracts
/// whatever it finds and a missing chapter or mission would otherwise fail only when it loads.</summary>
public static class ZbdManifest
{
    /// <summary>Every expected archive's path under <c>ZBD</c>, <c>/</c>-separated, in
    /// <see cref="ZbdTree"/>'s case.</summary>
    public static readonly IReadOnlyList<string> Expected = Build();

    /// <summary>The <see cref="Expected"/> archives absent from the install's <c>ZBD</c> folder
    /// <paramref name="zbdRoot"/>, each looked up without regard to case.</summary>
    public static IReadOnlyList<string> Missing(string zbdRoot) =>
        Expected.Where(path => InstallLocator.ResolveFile(zbdRoot, path) == null).ToList();

    private static List<string> Build()
    {
        // Chapter, its top texture tier, its mission folders. Every chapter also holds tiers 2 to 8.
        var chapters = new (string Chapter, int TopTier, string[] Missions)[]
        {
        ("C1", 15, new[] { "IA1", "M02", "M04", "M05", "MP1", "MP2", "MP3" }),
        ("C1B", 11, new[] { "IA1", "M03", "MP1", "MP3" }),
        ("C1C", 10, new[] { "IA1", "M01", "MP1", "MP3" }),
        ("C2", 14, new[] { "IA1", "M01", "M02", "M03", "M05", "MP1", "MP2", "MP3" }),
        ("C2B", 9, new[] { "IA1", "M04", "MP1", "MP3" }),
        ("C3", 12, new[] { "IA1", "M01", "M02", "M03", "M04", "M05", "MP1", "MP2", "MP3" }),
        ("C4", 14, new[] { "IA1", "M01", "M02", "M03", "M04", "M05", "MP1", "MP2", "MP3" }),
        ("C5", 14, new[] { "IA1", "M01", "M02", "M03", "M04", "MP1", "MP2", "MP3" }),
        };
        var paths = new List<string> { "interp.zbd", "planes.zbd", "rimage.zbd", "soundsh.zbd", "soundsl.zbd", "zrdr.zbd" };
        foreach (var (chapter, top, missions) in chapters)
        {
            paths.AddRange(new[] { "cam_anim", "gamez", "texture", "zrdr", "rtexture2", "rtexture4", "rtexture6", "rtexture8", "rtexture" + top.ToString(CultureInfo.InvariantCulture) }
                .Select(name => $"{chapter}/{name}.zbd"));
            paths.AddRange(missions.SelectMany(mission => new[] { $"{chapter}/{mission}/mis_anim.zbd", $"{chapter}/{mission}/zrdr.zbd" }));
        }

        return paths;
    }
}
