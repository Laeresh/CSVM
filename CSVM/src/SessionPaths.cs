using System.IO;

namespace CSVM;

/// <summary>
/// Where the extracted game data lives on disk: resolves the per-chapter and per-mission
/// extraction paths (gamez / texture / zrdr) under a data root, preferring an unpacked sibling
/// folder over its <c>.zip</c>. Extracted from <see cref="CSVM.Session.GameSession"/> so
/// <c>--anim-lab</c> resolves the same paths a normal session
/// does. Path arithmetic plus two directory probes: <see cref="PreferUnzipped"/>'s
/// directory-exists check and <see cref="ChapterTextures"/>'s scan for the top
/// <c>rtextureN</c> tier.
///
/// <para>The <c>--gamez=</c>/<c>--textures=</c>/<c>--zrdr=</c>/<c>--sounds=</c> CLI overrides are
/// the caller's policy and stay in <see cref="CSVM.Session.GameSession"/>; this class only builds the default
/// extraction-tree paths.</para>
/// </summary>
public static class SessionPaths
{
    /// <summary>Prefer the unpacked sibling folder from <c>ExtractAssets.ps1 -Unzip</c> when it
    /// exists (loose JSON/PNG/WAV: no zip decompression at load); else the <c>.zip</c> path
    /// verbatim. The loaders (`GameZ`/`TextureArchive`/`Zrdr`) read either shape.</summary>
    public static string PreferUnzipped(string zipPath)
    {
        var dir = Path.Combine(Path.GetDirectoryName(zipPath)!, Path.GetFileNameWithoutExtension(zipPath));
        return Directory.Exists(dir) ? dir : zipPath;
    }

    /// <summary>The chapter's texture archive — the plane skins and world textures. Prefers the
    /// chapter's top-budget <c>rtextureN</c> set (<c>N</c> ≈ the set's size in MB of late-90s
    /// texture memory; every chapter ships 2/4/6/8 plus one full-quality tier) over
    /// <c>texture.zip</c>: the tiers are what the original renders, and hundreds of same-name,
    /// same-size textures differ in content from the base archive — the gauge needles only carry
    /// their painted alpha silhouette there. A <c>&lt;tier&gt;_x4</c> sibling folder — the
    /// Real-ESRGAN output of <c>UpscaleTextures.ps1</c> — beats the tier itself when present;
    /// the <c>_x4</c> suffix is skipped by the tier scan (its name doesn't parse as
    /// <c>rtextureN</c>), so the folder never competes as a tier of its own.</summary>
    public static string ChapterTextures(string dataRoot, string chapter)
    {
        var dir = Path.Combine(dataRoot, "extracted", chapter);
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
        var upscaled = Path.Combine(dir, best + "_x4");
        if (Directory.Exists(upscaled)) { return upscaled; }
        return PreferUnzipped(Path.Combine(dir, best + ".zip"));
    }

    /// <summary>The chapter's world GameZ (<c>extracted/&lt;chapter&gt;/gamez.zip</c>) — the single
    /// <c>world1</c> node and everything under it.</summary>
    public static string ChapterGamez(string dataRoot, string chapter) =>
        PreferUnzipped(Path.Combine(dataRoot, "extracted", chapter, "gamez.zip"));

    /// <summary>The chapter's zrdr scope (<c>extracted/&lt;chapter&gt;/zrdr.zip</c>) — zepstate,
    /// startanims, and the chapter-wide anim defs.</summary>
    public static string ChapterZrdr(string dataRoot, string chapter) =>
        PreferUnzipped(Path.Combine(dataRoot, "extracted", chapter, "zrdr.zip"));

    /// <summary>The mission's zrdr scope (<c>extracted/&lt;chapter&gt;/&lt;mission&gt;/zrdr.zip</c>)
    /// — spawn points, danger zones, weather, objectives, and the mission's own anim defs.</summary>
    public static string MissionZrdr(string dataRoot, string chapter, string mission) =>
        PreferUnzipped(Path.Combine(dataRoot, "extracted", chapter, mission, "zrdr.zip"));
}
