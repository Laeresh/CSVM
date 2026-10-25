using System;

namespace CSVM.UI.Menu;

/// <summary>
/// One notice the launcher posts for the player to read at the menu's next show. VR mode posts
/// one for the flat game it fell back to or a restart that did not start (<c>Launch/XrRelaunch.cs</c>).
/// A menu-launched flight posts one per aircraft flown without its bad rig. Original raises it as
/// the one-button messagebox. A presentation takes it once, so a later return from flight shows
/// nothing again. A switch keeps a notice nothing has shown yet.
/// </summary>
public sealed class MenuNoticeFeature : IMenuFeature
{
    // Between two messages of one notice: the menu's text wrap starts a paragraph at each line break.
    private const string Paragraph = "\n\n";

    private string? _pending;

    /// <summary>The notice waiting for a presentation, or null.</summary>
    public string? Pending => _pending;

    /// <summary>Adds <paramref name="message"/> to the next show's notice as a paragraph of its own.
    /// A message the notice already holds is not repeated, since every build of one airframe posts its rig's.</summary>
    public void Post(string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        if (_pending == null)
        {
            _pending = message;
        }
        else if (Array.IndexOf(_pending.Split(Paragraph), message) < 0)
        {
            _pending += Paragraph + message;
        }
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
