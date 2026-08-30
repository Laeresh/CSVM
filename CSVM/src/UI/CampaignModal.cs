using System;

namespace CSVM.UI;

/// <summary>
/// A dialog standing over a campaign screen, the original's <c>messagebox.script</c>: a message, an
/// icon and one button, holding every input until it is answered. The plane selection screen raises
/// two of them, langui 710 refusing a plane both crew slots would fly and langui 702 confirming an
/// export, and the script reaches both the same way, by setting a message id and running that
/// script over whatever screen was showing.
///
/// <para>It lives on <see cref="CampaignFlow"/> rather than on a page because it is a facility
/// every screen shares. ⚠ It is not <see cref="CampaignFlow.Message"/>, which is the one-line
/// refusal band any navigation clears: a refusal answered in both places says itself twice.</para>
/// </summary>
public sealed class CampaignModal
{
    private readonly Action? _confirmed;

    /// <summary>Builds a dialog over its words, its button's label and what its confirm runs.</summary>
    public CampaignModal(string message, string button, Action? confirmed = null)
    {
        Message = message;
        Button = button;
        _confirmed = confirmed;
    }

    /// <summary>What the dialog says, wrapped into the box's own text widget.</summary>
    public string Message { get; }

    /// <summary>The button's words, which is the only answer a one-button dialog takes.</summary>
    public string Button { get; }

    /// <summary>Answers the dialog, running whatever was to happen after it. The flow clears it.</summary>
    public void Confirm() => _confirmed?.Invoke();
}
