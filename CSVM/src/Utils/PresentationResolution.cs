using System;

namespace CSVM.Utils;

/// <summary>
/// Requested-versus-active menu presentation resolution: fixed precedence force-Built-in → CLI
/// override → saved request → Built-in default, with availability checked separately so a
/// temporary failure never rewrites what <see cref="OptionsStore"/> has saved. Presentation names
/// stay plain strings here on purpose — the presentation contract's identity type belongs to
/// whichever module owns it.
/// </summary>
public static class PresentationResolution
{
    /// <summary>The name every fresh install, and every unavailable request, falls back to.</summary>
    public const string BuiltIn = "built-in";

    /// <summary>The requested presentation before availability is checked: the CLI override beats
    /// the saved request, which beats <see cref="BuiltIn"/>. Force-Built-in never reaches this
    /// method — <see cref="Resolve"/> answers it before the request is even read, so what a player
    /// sees back in Options still reflects what they last picked.</summary>
    public static string Requested(string? cliOverride, string? savedRequest) =>
        !string.IsNullOrEmpty(cliOverride) ? cliOverride
        : !string.IsNullOrEmpty(savedRequest) ? savedRequest
        : BuiltIn;

    /// <summary>The active presentation for one run, and a reason to show when it differs from the
    /// request. <paramref name="isAvailable"/> is the caller's availability check (an asset
    /// manifest, a presentation registry, ...), injected rather than owned here since this module
    /// carries no presentation contract types. Neither <paramref name="forceBuiltIn"/> nor a failed
    /// availability check changes what <see cref="Requested"/> would answer for the same inputs.</summary>
    public static (string Active, string? Reason) Resolve(
        bool forceBuiltIn,
        string? cliOverride,
        string? savedRequest,
        Func<string, bool> isAvailable)
    {
        if (forceBuiltIn)
        {
            return (BuiltIn, "the force-Built-in override is set");
        }

        string requested = Requested(cliOverride, savedRequest);
        if (requested == BuiltIn)
        {
            return (BuiltIn, null);
        }

        return isAvailable(requested)
            ? (requested, null)
            : (BuiltIn, $"'{requested}' is not available");
    }
}
