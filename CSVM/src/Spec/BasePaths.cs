using System.IO;
using CSVM.Extraction;

namespace CSVM.Spec;

/// <summary>
/// The eight chapter-independent paths a launch reads: the repo root, the data root, and the six
/// archives and files under the data root's <c>extracted/</c>. Each session receives them through
/// the launcher's context and derives its chapter's gamez, texture and mission paths from its spec.
/// The probe runner holds the same value. Module notes: docs/architecture/Spec.md.
/// </summary>
public sealed record BasePaths(
    string RepoRoot,
    string DataRoot,
    string PlanesGamezPath,
    string ZrdrPath,
    string SoundsPath,
    string InterpPath,
    string MessagesPath,
    string RofPath)
{
    /// <summary>The paths under <paramref name="dataRoot"/>. A spec override is used verbatim, and
    /// an archive the spec does not name prefers its unpacked sibling folder.
    /// ⚠ Do not call it before <see cref="SessionPaths.ForceZipped"/> is settled; that preference
    /// reads it.</summary>
    public static BasePaths Resolve(string repoRoot, string dataRoot, SessionSpec spec) => new(
        repoRoot,
        dataRoot,
        SessionPaths.PreferUnzipped(Path.Combine(dataRoot, "extracted", "planes.zip")),
        spec.Zrdr ?? SessionPaths.PreferUnzipped(Path.Combine(dataRoot, "extracted", "zrdr.zip")),
        spec.Sounds ?? SessionPaths.PreferUnzipped(Path.Combine(dataRoot, "extracted", "soundsh.zip")),
        spec.Interp ?? Path.Combine(dataRoot, "extracted", "interp.json"),
        spec.Messages ?? Path.Combine(dataRoot, "extracted", "messages.json"),
        spec.Rof ?? Path.Combine(dataRoot, "extracted", "rof"));
}
