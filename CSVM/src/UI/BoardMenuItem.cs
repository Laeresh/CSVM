namespace CSVM.UI;

/// <summary>
/// The rows a board menu can offer. The board owning the menu decides which of these it carries
/// and what each one does: Resume appears only on the pause board, and Exit's label follows
/// whether the session can return to the launchscreen or only quit.
/// </summary>
public enum BoardMenuItem
{
    /// <summary>Drop the pause halt reason and fly on.</summary>
    Resume,

    /// <summary>Rerun: reset the running mode in place, same seed and same world.</summary>
    Restart,

    /// <summary>Leave the session, to the launchscreen or out of the game.</summary>
    Exit,
}
