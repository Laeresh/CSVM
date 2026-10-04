using System;
using System.Globalization;
using Godot;

namespace CSVM.UI.Boards;

/// <summary>
/// The type scale for chrome the original never painted, where its face, sizes and units live.
/// The face is the theme's default font, slanted or weighted as a variation and never another family.
/// A size is a rung of one ladder in frame units, the board's own authored pixel. That is
/// 1/<see cref="BoardFit.AuthoredHeight"/> of the frame's height, and a surface stated at
/// another reference height converts a rung through <see cref="InReference"/>.
/// A distance the chrome prints is whole metres (<see cref="Metres"/>).
/// ⚠ Do not put painted original artwork on this scale; its sizes are its layout's own.
/// </summary>
public static class ChromeType
{
    /// <summary>The height of the frame a frame unit divides, the board's authored height.</summary>
    public const float FrameHeight = BoardFit.AuthoredHeight;

    // The ladder in frame units, indexed by ChromeSize. Five rungs are the join board's draft sizes,
    // its 14 folded into 13. The rest carry the start count, the results total and the in-flight
    // text. The count's 72 is TUNE at the controls.
    private static readonly float[] Rungs = { 72f, 26f, 22f, 19f, 17f, 15f, 13f, 11f, 8f, 6f };

    /// <summary>The theme's default face, the one font choice every piece of this chrome draws in.
    /// A board's italic and bold are variations of it (<see cref="ComposedBoardView"/>).</summary>
    public static Font Face(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetThemeDefaultFont();
    }

    /// <summary><paramref name="rung"/>'s size in frame units, which is what a composed board's
    /// <see cref="BoardLine.Size"/> is authored in.</summary>
    public static float Size(ChromeSize rung) => Rungs[(int)rung];

    /// <summary><paramref name="rung"/>'s size in the pixels of a surface laid out against a
    /// frame <paramref name="referenceHeight"/> tall: 1440 for the flight HUD, 720 for a results
    /// board. Multiplied before it is divided, so a whole rung converts without float drift.</summary>
    public static float InReference(ChromeSize rung, float referenceHeight) =>
        Size(rung) * referenceHeight / FrameHeight;

    /// <summary>A distance as the chrome prints it: whole metres, rounded half to even like
    /// <see cref="Mathf.RoundToInt(float)"/>, then a space and <c>m</c>.</summary>
    public static string Metres(float metres) =>
        Mathf.RoundToInt(metres).ToString(CultureInfo.InvariantCulture) + " m";
}
