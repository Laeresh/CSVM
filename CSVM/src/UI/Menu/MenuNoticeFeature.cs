using System;

namespace CSVM.UI.Menu;

/// <summary>
/// One notice the launcher posts for the player to read at the menu's next show, whichever
/// presentation is active. VR mode falling back to the flat game posts the only one. Built-in writes
/// it on its error line and Original raises it as the one-button messagebox. A presentation takes it
/// once, so a later return from flight shows nothing again. A switch keeps a notice nothing has
/// shown yet.
/// </summary>
public sealed class MenuNoticeFeature : IMenuFeature
{
    private string? _pending;

    /// <summary>The notice waiting for a presentation, or null.</summary>
    public string? Pending => _pending;

    /// <summary>Stands <paramref name="message"/> for the next show, replacing any earlier one.</summary>
    public void Post(string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        _pending = message;
    }

    /// <summary>The notice for a presentation to show now, or null; taking it clears it.</summary>
    public string? Take()
    {
        string? message = _pending;
        _pending = null;
        return message;
    }

    /// <summary>Keeps the notice: it is the launcher's word to the player, not setup state.</summary>
    public void Discard()
    {
    }
}
