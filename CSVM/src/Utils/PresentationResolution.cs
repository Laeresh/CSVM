using System;

namespace CSVM.Utils;

/// <summary>
/// Requested-versus-active menu presentation resolution: the CLI override, else
/// <see cref="Default"/>, with availability checked separately so a temporary failure never changes
/// what was requested. ⚠ No saved word takes part. An older build's options file may still carry
/// one, and only the command line is meant to choose a presentation. Presentation names stay plain
/// strings here on purpose, the presentation contract's identity type belongs to whichever module
/// owns it.
/// </summary>
public static class PresentationResolution
{
    /// <summary>The name a run without an override requests, and what an unavailable request falls
    /// back to: the original's own menus over the extracted data.</summary>
    public const string Default = "original";

    /// <summary>The requested presentation before availability is checked: the CLI override, else
    /// <see cref="Default"/>.</summary>
    public static string Requested(string? cliOverride) =>
        !string.IsNullOrEmpty(cliOverride) ? cliOverride : Default;

    /// <summary>The active presentation for one run, and a reason to show when the request is
    /// unavailable. The caller injects the check as <paramref name="isAvailable"/>, since this
    /// module carries no presentation contract types. An unavailable request falls back to
    /// <see cref="Default"/>, which stays active even when it is itself unavailable, with the
    /// reason said.</summary>
    public static (string Active, string? Reason) Resolve(string? cliOverride, Func<string, bool> isAvailable)
    {
        string requested = Requested(cliOverride);
        return isAvailable(requested)
            ? (requested, null)
            : (Default, $"'{requested}' is not available");
    }
}
