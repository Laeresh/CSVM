namespace CSVM.UI.Boards;

/// <summary>
/// One rung of <see cref="ChromeType"/>'s size ladder, largest first. Each rung's size in frame
/// units is <see cref="ChromeType.Size"/>'s; the uses named here are where it stands today.
/// </summary>
public enum ChromeSize
{
    /// <summary>26: a board's page heading.</summary>
    Heading,

    /// <summary>22: a second heading beside it, and a results board's title.</summary>
    Title,

    /// <summary>19: the one figure a results board leads with, its total.</summary>
    Lead,

    /// <summary>17: an entry's own words, a device name on the join board.</summary>
    Body,

    /// <summary>15: running text, a rule line, a results table's rows.</summary>
    Text,

    /// <summary>13: small print, a status under an entry, a subtitle, a context line.</summary>
    Caption,

    /// <summary>11: an in-flight banner, and a results table's column heads.</summary>
    Note,

    /// <summary>8: an in-flight status readout.</summary>
    Readout,

    /// <summary>6: an in-flight marker's label.</summary>
    Label,
}
