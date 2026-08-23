using System;
using System.Text;

namespace CSVM.UI;

/// <summary>
/// The PLANENAME screen: the name that is also the plane's identity in
/// <see cref="CSVM.Flight.CustomPlaneStore"/>, entered a character at a time. One row per
/// character, each stepped through the alphabet, plus a trailing length row whose stepper adds
/// and removes characters. Every control is the launchscreen's own live ←→ stepper, so the screen
/// is pad-friendly and needs no keyboard: the codebase has no text-entry idiom outside the debug
/// labs' `LineEdit`s, which are mouse-and-keyboard tools and not this surface. The alphabet holds
/// no character a filename cannot carry, so the store's sanitisation never has anything to do.
/// </summary>
public sealed class HangarNamePage : HangarPage
{
    /// <summary>The longest name the screen writes: the original's saved-plane name index is
    /// 33-byte records (docs/formats/paint.md), so 32 characters plus its terminator.</summary>
    public const int MaxLength = 32;

    // The characters a cell can hold. Upper case, digits, then the two separators a plane name
    // uses; every one of them is legal in a filename, which is what keeps the store's own
    // sanitisation from ever changing what the pilot typed.
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 -";

    /// <summary>Binds the page to its flow.</summary>
    public HangarNamePage(HangarFlow flow)
        : base(flow)
    {
    }

    /// <inheritdoc/>
    public override HangarScreen Screen => HangarScreen.Name;

    /// <summary>One row per character, then the length row that adds and removes them.</summary>
    public override int RowCount => Scratch.Name.Length + 1;

    /// <inheritdoc/>
    public override string RowText(int row) =>
        IsLengthRow(row)
            ? $"Letters: {Scratch.Name.Length}"
            : $"{row + 1}.  {Shown(Scratch.Name[row])}";

    /// <summary>The whole name with the focused character marked, so the vertical list of cells
    /// still reads as a word; the length row explains its own stepper instead.</summary>
    public override string Detail(int row)
    {
        if (IsLengthRow(row))
        {
            if (string.IsNullOrWhiteSpace(Scratch.Name))
            {
                // The commit is the gate (langui 203); saying so here is what stops a pilot
                // reaching the Build press and being refused for a name they never typed.
                return Flow.Strings.Text(203, "You must enter a name for your new plane.");
            }

            return Scratch.Name.Length >= MaxLength
                ? $"Full at {MaxLength} letters; step left to remove the last"
                : "Step right to add a letter, left to remove the last";
        }

        // Real characters here, not the rows' stand-in for a space: the brackets already make a
        // focused blank visible, and the point of the line is to read as the name itself.
        var text = new StringBuilder();
        for (int i = 0; i < Scratch.Name.Length; i++)
        {
            text.Append(i == row ? $"[{Scratch.Name[i]}]" : Scratch.Name[i].ToString());
        }

        return text.ToString();
    }

    /// <inheritdoc/>
    public override bool Step(int row, int dir)
    {
        if (dir == 0)
        {
            return false;
        }

        return IsLengthRow(row) ? StepLength(dir) : StepCharacter(row, dir);
    }

    // A space is a character like any other in a plane name, but a row showing one would look
    // empty, so it is drawn as the underscore that reads as a blank in every menu font.
    private static char Shown(char c) => c == ' ' ? '_' : c;

    private bool IsLengthRow(int row) => row >= Scratch.Name.Length;

    // Adding seats an 'A': a new cell has to hold something, and the pilot's cursor is already on
    // the row the new character takes over, so the next step edits it.
    private bool StepLength(int dir)
    {
        string name = Scratch.Name;
        if (dir > 0)
        {
            if (name.Length >= MaxLength)
            {
                return false;
            }

            Scratch.Name = name + Alphabet[0];
            return true;
        }

        if (name.Length == 0)
        {
            return false;
        }

        Scratch.Name = name[..^1];
        return true;
    }

    // A character the alphabet does not carry (an imported original save's lower case) is left
    // alone until the pilot steps that cell, which then lands on the alphabet's first entry
    // rather than on whatever happens to sort next to it.
    private bool StepCharacter(int row, int dir)
    {
        char current = Scratch.Name[row];
        int at = Alphabet.IndexOf(current, StringComparison.Ordinal);
        int next = at < 0
            ? (dir > 0 ? 0 : Alphabet.Length - 1)
            : ((at + dir) % Alphabet.Length + Alphabet.Length) % Alphabet.Length;
        if (Alphabet[next] == current)
        {
            return false;
        }

        var text = Scratch.Name.ToCharArray();
        text[row] = Alphabet[next];
        Scratch.Name = new string(text);
        return true;
    }
}
