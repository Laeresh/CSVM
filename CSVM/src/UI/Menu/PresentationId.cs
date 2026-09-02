using System;

namespace CSVM.UI.Menu;

/// <summary>
/// The identity a menu presentation registers under and Options persist: a stable token, never a
/// type name or a screen id. <see cref="BuiltIn"/> and <see cref="Original"/> are the shipped
/// identities; a later presentation mints its own token and registers it. Comparison is ordinal
/// and case-sensitive, so the persisted string and the registered token must match exactly.
/// </summary>
public readonly record struct PresentationId
{
    /// <summary>The Built-in presentation: permanent, asset-independent, the fallback.</summary>
    public static readonly PresentationId BuiltIn = new("built-in");

    /// <summary>The Original presentation over the player's extracted menu data.</summary>
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

    /// <summary>The token itself, as Options persist it.</summary>
    public string Value { get; }

    /// <summary>The token, so logs and errors read the persisted spelling.</summary>
    public override string ToString() => Value ?? string.Empty;
}
