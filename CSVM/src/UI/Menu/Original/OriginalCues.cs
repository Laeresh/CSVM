namespace CSVM.UI.Menu.Original;

/// <summary>
/// The cue names the Original presentation asks the shared audio service for. The names are
/// semantic; which file each resolves to is the service's cue table, so the presentation never
/// names a wav. The four are the four the original's globals script binds: a sound on a button
/// rollover, another on a button press, and the edit box's keystroke and reject sounds.
/// </summary>
public static class OriginalCues
{
    /// <summary>The pointer entered a live button.</summary>
    public const string Rollover = "menu.rollover";

    /// <summary>A live button was pressed, by pointer or by the seat's accept.</summary>
    public const string Click = "menu.click";

    /// <summary>An edit box took a typed character.</summary>
    public const string Text = "menu.text";

    /// <summary>An edit box refused a typed character: outside its character set, or past its cap.</summary>
    public const string TextError = "menu.text-error";
}
