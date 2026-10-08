namespace CSVM.UI.Boards;

/// <summary>
/// One rung of <see cref="ChromeType"/>'s size ladder, largest first, named for the role of the
/// text set at it. Each rung's size in frame units is <see cref="ChromeType.Size"/>'s.
/// </summary>
public enum ChromeSize
{
    /// <summary>A start count's figure, the one word standing alone in the middle of a pane.</summary>
    Count,

    /// <summary>A board's page heading.</summary>
    Heading,

    /// <summary>A second heading, standing beside or under the page heading.</summary>
    Title,

    /// <summary>The one figure a board leads with.</summary>
    Lead,

    /// <summary>An entry's own words, one row of a list or menu.</summary>
    Body,

    /// <summary>Running text.</summary>
    Text,

    /// <summary>Small print: a status, a subtitle or a legend under the text it belongs to.</summary>
    Caption,

    /// <summary>An in-flight banner.</summary>
    Note,

    /// <summary>An in-flight readout.</summary>
    Readout,

    /// <summary>An in-flight marker's label.</summary>
    Label,
}
