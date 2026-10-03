namespace CSVM.UI.Boards;

/// <summary>
/// One rung of <see cref="ChromeType"/>'s size ladder, largest first. Each rung's size in frame
/// units is <see cref="ChromeType.Size"/>'s; the uses named here are where it stands today.
/// </summary>
public enum ChromeSize
{
    /// <summary>26: a board's page heading, and the dogfight, wrap-up and pause boards' headline.</summary>
    Heading,

    /// <summary>22: a second heading beside it, and the stunt results boards' title.</summary>
    Title,

    /// <summary>19: the one figure a results board leads with, its total, and Built-in's join board
    /// rows.</summary>
    Lead,

    /// <summary>17: an entry's own words: a seat on either join board, Built-in's board heading, a
    /// dogfight or wrap-up board's row, a board menu's row.</summary>
    Body,

    /// <summary>15: running text, a rule line, a stunt results table's rows, the pause board's
    /// owner line.</summary>
    Text,

    /// <summary>13: small print, a status under an entry, a subtitle, a context line, a board
    /// menu's legend, the launchscreen's join strip.</summary>
    Caption,

    /// <summary>11: an in-flight banner, and a results table's column heads.</summary>
    Note,

    /// <summary>8: an in-flight status readout, and the flight text block (speed, altitude, throttle).</summary>
    Readout,

    /// <summary>6: an in-flight marker's label.</summary>
    Label,
}
