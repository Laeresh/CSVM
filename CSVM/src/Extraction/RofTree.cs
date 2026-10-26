namespace CSVM.Extraction;

/// <summary>
/// The one case every path in <see cref="RofFiles"/> is held in, upper case, and the mapping a
/// reader puts a name from game data through. The archive stores its members upper case. The
/// layout, the scripts and the install spell the same files in any case.
/// </summary>
public static class RofTree
{
    /// <summary>A path relative to the archive root, <c>/</c>-separated, in the view's case.
    /// ⚠ Do not map a name any other way; the view and every reader must agree.</summary>
    public static string Canonical(string relative) =>
        relative.Replace('\\', '/').ToUpperInvariant();
}
