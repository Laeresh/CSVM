using System.IO;

namespace CSVM.Extraction;

/// <summary>
/// The one case every path in unzbd's output tree takes, and the mapping a reader puts a chapter,
/// mission or archive name through. Folders are upper case (<c>C1B</c>, <c>M01</c>) and file names
/// lower case (<c>zrdr.zip</c>). That is how the original's install spells its <c>ZBD</c> tree. The
/// campaign sequence names the same folders <c>c3/m01</c>. A case-sensitive disk finds only the
/// spelling written. Layout: docs/formats/extraction.md.
/// </summary>
public static class ZbdTree
{
    /// <summary>A path relative to the tree's root, <c>/</c>-separated, in the tree's case: every
    /// folder upper case, the file name lower case. ⚠ Do not map a name any
    /// other way; the writer and every reader must agree or a case-sensitive disk loses the file.</summary>
    public static string Canonical(string relative)
    {
        string[] parts = relative.Replace('\\', '/').Split('/');
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i] = i == parts.Length - 1 ? parts[i].ToLowerInvariant() : parts[i].ToUpperInvariant();
        }

        return string.Join('/', parts);
    }

    /// <summary>Where a relative file path (<c>c3/m01/zrdr.zip</c>) sits under
    /// <paramref name="folder"/>, the root unzbd wrote the tree into.</summary>
    public static string Under(string folder, string relative) =>
        Path.Combine(folder, Canonical(relative).Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Where a chapter or mission folder (<c>c3</c>, <c>c3/m01</c>) sits under
    /// <paramref name="folder"/>, the root unzbd wrote the tree into.</summary>
    public static string Folder(string folder, string relative) =>
        Path.Combine(folder, relative.Replace('\\', '/').ToUpperInvariant().Replace('/', Path.DirectorySeparatorChar));
}
