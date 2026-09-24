using System;

namespace CSVM.Utils;

/// <summary>
/// Requested-versus-active menu presentation resolution: fixed precedence force-Built-in → CLI
/// override → <see cref="Default"/>, with availability checked separately so a temporary failure
/// never changes what was requested. ⚠ No saved word takes part: the options file may still carry
/// one from an older build, and reading it back would reopen a door to Built-in that only the two
/// command-line flags are meant to be. Presentation names stay plain strings here on purpose, the
/// presentation contract's identity type belongs to whichever module owns it.
/// </summary>
public static class PresentationResolution
{
    /// <summary>The name every unavailable request, and the force-Built-in override, falls back
    /// to: the asset-independent presentation, so a run always has a menu.</summary>
    public const string BuiltIn = "built-in";

    /// <summary>The name a run without an override requests: the original's own menus over the
    /// extracted data. A machine without that data still lands on <see cref="BuiltIn"/> through the
    /// availability check.</summary>
    public const string Default = "original";

    /// <summary>The requested presentation before availability is checked: the CLI override, else
    /// <see cref="Default"/>. Force-Built-in never reaches this method, <see cref="Resolve"/>
    /// answers it before the request is even read.</summary>
    public static string Requested(string? cliOverride) =>
        !string.IsNullOrEmpty(cliOverride) ? cliOverride : Default;

    /// <summary>The active presentation for one run, and a reason to show when it differs from the
    /// request. <paramref name="isAvailable"/> is the caller's availability check (an asset
    /// manifest, a presentation registry, ...), injected rather than owned here since this module
    /// carries no presentation contract types. Neither <paramref name="forceBuiltIn"/> nor a failed
    /// availability check changes what <see cref="Requested"/> would answer for the same input.</summary>
    public static (string Active, string? Reason) Resolve(
        bool forceBuiltIn,
        string? cliOverride,
        Func<string, bool> isAvailable)
    {
        if (forceBuiltIn)
        {
            return (BuiltIn, "the force-Built-in override is set");
        }

        string requested = Requested(cliOverride);
        if (requested == BuiltIn)
        {
            return (BuiltIn, null);
        }

        return isAvailable(requested)
            ? (requested, null)
            : (BuiltIn, $"'{requested}' is not available");
    }
}
