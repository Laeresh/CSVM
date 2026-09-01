namespace CSVM.UI.Menu.Original;

/// <summary>
/// The cue names the Original presentation asks the shared audio service for. The names are
/// semantic; which file each resolves to is the service's cue table, so the presentation never
/// names a wav. The two used here are the two the original's control library binds: a sound on
/// a button rollover and another on a button press.
/// </summary>
public static class OriginalCues
{
    /// <summary>The pointer entered a live button.</summary>
    public const string Rollover = "menu.rollover";

    /// <summary>A live button was pressed, by pointer or by the seat's accept.</summary>
    public const string Click = "menu.click";
}
