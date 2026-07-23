using System.IO;

namespace CSVM;

/// <summary>
/// Where the extracted game data lives on disk: resolves the per-chapter and per-mission
/// extraction paths (gamez / texture / zrdr) under a data root, preferring an unpacked sibling
/// folder over its <c>.zip</c>. Extracted from <see cref="PlaneViewer"/> (2026-07-23,
/// PLAN-anim-debugger Wave 1 A2) so <c>--anim-lab</c> resolves the same paths a normal session
/// does. Pure path arithmetic — the only I/O is <see cref="PreferUnzipped"/>'s directory-exists
/// probe.
///
/// <para>The <c>--gamez=</c>/<c>--textures=</c>/<c>--zrdr=</c>/<c>--sounds=</c> CLI overrides are
/// the caller's policy and stay in <see cref="PlaneViewer"/>; this class only builds the default
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

    /// <summary>The chapter's texture archive (<c>extracted/&lt;chapter&gt;/texture.zip</c>) — the
    /// plane skins and world textures for that chapter.</summary>
    public static string ChapterTextures(string dataRoot, string chapter) =>
        PreferUnzipped(Path.Combine(dataRoot, "extracted", chapter, "texture.zip"));

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
