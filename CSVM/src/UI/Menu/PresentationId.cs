using System;

namespace CSVM.UI.Menu;

/// <summary>
/// The identity a menu presentation registers under and <c>--presentation=</c> names: a stable
/// token, never a type name or a screen id. The shipped identity is <see cref="Original"/>. A later
/// presentation mints its own token and registers it. Comparison is ordinal and case-sensitive, so
/// the command-line word and the registered token must match exactly.
/// </summary>
public readonly record struct PresentationId
{
    /// <summary>The Original presentation over the player's extracted menu data, and the fallback.</summary>
    public static readonly PresentationId Original = new("original");

    /// <summary>Wraps a token. Null, empty or whitespace is a wiring error, not a value.</summary>
    public PresentationId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A presentation id needs a non-empty token.", nameof(value));
        }

        Value = value;
    }

    /// <summary>The token itself, as the command line names it.</summary>
    public string Value { get; }

    /// <summary>The token, so logs and errors read the registered spelling.</summary>
    public override string ToString() => Value ?? string.Empty;
}
