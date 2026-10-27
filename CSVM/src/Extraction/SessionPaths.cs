using System;
using System.IO;
using System.Linq;

namespace CSVM.Extraction;

/// <summary>
/// Where each chapter and mission archive's unzbd output sits under a folder holding that tree:
/// a modding folder, or the tests' oracle. An unpacked sibling folder beats its <c>.zip</c>.
/// Every chapter path maps through <see cref="ZbdTree"/>. Static and engine-free.
/// ⚠ The folder is the tree's own root, never a data root: nothing here implies an
/// <c>extracted/</c>.
/// </summary>
public static class SessionPaths
{
    /// <summary>The unpacked sibling folder a modder may expand a zip into, when it exists, since
    /// loose files skip zip decompression at load. Else the <c>.zip</c> path verbatim; the loaders
    /// read either shape.</summary>
    public static string PreferUnzipped(string zipPath)
    {
        var dir = Path.Combine(Path.GetDirectoryName(zipPath)!, Path.GetFileNameWithoutExtension(zipPath));
        return Directory.Exists(dir) ? dir : zipPath;
    }

    /// <summary>The chapter's texture archive, the plane skins and world textures. Prefers the
    /// chapter's top-budget <c>rtextureN</c> set (<c>N</c> ≈ the set's size in MB of late-90s
    /// texture memory; every chapter ships 2/4/6/8 plus one full-quality tier) over
    /// <c>texture.zip</c>: the tiers are what the original renders, and hundreds of same-name,
    /// same-size textures differ in content from the base archive, the gauge needles only carry
    /// their painted alpha silhouette there.</summary>
    public static string ChapterTextures(string folder, string chapter)
    {
        var dir = ZbdTree.Folder(folder, chapter);
        string best = "texture";
        int bestN = -1;
        if (Directory.Exists(dir))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(dir, "rtexture*"))
            {
                var name = Path.GetFileNameWithoutExtension(entry);
                if (int.TryParse(name.Substring("rtexture".Length), out int n) && n > bestN)
                {
                    bestN = n;
                    best = name;
                }
            }
        }
        return PreferUnzipped(Path.Combine(dir, best + ".zip"));
    }

    /// <summary>The chapter's world GameZ (<c>&lt;chapter&gt;/gamez.zip</c>), the single
    /// <c>world1</c> node and everything under it.</summary>
    public static string ChapterGamez(string folder, string chapter) =>
        PreferUnzipped(ZbdTree.Under(folder, chapter + "/gamez.zip"));

    /// <summary>The chapter's zrdr scope (<c>&lt;chapter&gt;/zrdr.zip</c>), zepstate,
    /// startanims, and the chapter-wide anim defs.</summary>
    public static string ChapterZrdr(string folder, string chapter) =>
        PreferUnzipped(ZbdTree.Under(folder, chapter + "/zrdr.zip"));

    /// <summary>The mission's zrdr scope (<c>&lt;chapter&gt;/&lt;mission&gt;/zrdr.zip</c>),
    /// spawn points, danger zones, weather, objectives, and the mission's own anim defs.
    /// ⚠ The chapter and mission may come in either case, and the campaign sequence names
    /// <c>c3</c>/<c>m01</c>.</summary>
    public static string MissionZrdr(string folder, string chapter, string mission) =>
        PreferUnzipped(ZbdTree.Under(folder, $"{chapter}/{mission}/zrdr.zip"));

    /// <summary>The chapter's compiled anim archive (<c>&lt;chapter&gt;/cam_anim.zip</c>).</summary>
    public static string ChapterAnim(string folder, string chapter) =>
        PreferUnzipped(ZbdTree.Under(folder, chapter + "/cam_anim.zip"));

    /// <summary>The mission's compiled anim archive
    /// (<c>&lt;chapter&gt;/&lt;mission&gt;/mis_anim.zip</c>).</summary>
    public static string MissionAnim(string folder, string chapter, string mission) =>
        PreferUnzipped(ZbdTree.Under(folder, $"{chapter}/{mission}/mis_anim.zip"));

    /// <summary>Whether <paramref name="game"/> holds any chapter's world GameZ
    /// (<c>ZBD/&lt;chapter&gt;/gamez.zbd</c>). False with no game files, and for a copy of a disc
    /// without its chapters, so every flight fails to build.</summary>
    public static bool HasAnyChapterWorld(IGameFileSource? game) =>
        game != null && ZbdManifest.Expected.Any(path => path.EndsWith("/gamez.zbd", StringComparison.Ordinal) && game.Exists("ZBD/" + path));
}
