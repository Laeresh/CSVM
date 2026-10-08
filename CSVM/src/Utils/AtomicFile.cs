using System.IO;
using System.Text;

namespace CSVM.Utils;

/// <summary>
/// Whole-file replacement for player data. The text lands in a sibling temp file, and one rename
/// puts it over the target. A kill or a full disk mid-write therefore leaves the previous file, or
/// none on a first save, never a truncated one. ⚠ Do not persist player data with
/// <c>File.WriteAllText</c> on the target itself: it truncates the file before writing.
/// </summary>
public static class AtomicFile
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Writes <paramref name="text"/> to <paramref name="path"/> as BOM-less UTF-8 through
    /// <c>path.tmp</c>. A temp file a killed write left behind is overwritten. The directory must
    /// exist; an IO failure reaches the caller, whose policy it is.</summary>
    public static void WriteAllText(string path, string text)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, text, Utf8);
        File.Move(temp, path, overwrite: true);
    }
}
