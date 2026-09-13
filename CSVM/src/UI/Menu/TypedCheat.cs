using System;

namespace CSVM.UI.Menu;

/// <summary>
/// One screen's typed-word latch, the rule three of the original's menu scripts share. A left
/// click inside the widget's authored region gives it the keyboard (<c>focus(this)</c>); every
/// character that arrives afterwards is appended to a buffer, and the first character that leaves
/// the target word's prefix empties the buffer outright rather than re-anchoring it, so a mistyped
/// word has to be started again from its first letter. The compare is case-sensitive, which is
/// what <c>left$</c> does. The words and the regions are in
/// <c>docs/formats/campaign-screens.md</c>; what each one fires is <see cref="CampaignCheats"/>.
/// </summary>
public sealed class TypedCheat
{
    private readonly string _word;

    /// <summary>Builds the latch for <paramref name="word"/>, the word the screen fires on.</summary>
    public TypedCheat(string word)
    {
        _word = word ?? throw new ArgumentNullException(nameof(word));
    }

    /// <summary>The word this latch waits for.</summary>
    public string Word => _word;

    /// <summary>Whether the widget holds the keyboard, which is what a click inside its region
    /// gives it and a click anywhere else takes away.</summary>
    public bool Armed { get; private set; }

    /// <summary>What has been typed since the buffer was last emptied.</summary>
    public string Buffer { get; private set; } = string.Empty;

    /// <summary>Whether anything stands in the buffer. The cabin's NEXT MISSION reads this and not
    /// the completed word, so a half-typed prefix already diverts it.</summary>
    public bool Typed => Buffer.Length > 0;

    /// <summary>Takes the keyboard. The buffer is not touched: the script's <c>focus</c> moves the
    /// caret and leaves its own string variable where it was.</summary>
    public bool Arm()
    {
        bool was = Armed;
        Armed = true;
        return !was;
    }

    /// <summary>Gives the keyboard up, the buffer surviving as above.</summary>
    public bool Disarm()
    {
        bool was = Armed;
        Armed = false;
        return was;
    }

    /// <summary>Empties the buffer, which is what the cabin's NEXT MISSION does after it has read
    /// it, and what leaving the screen does through <see cref="Reset"/>.</summary>
    public void ClearBuffer() => Buffer = string.Empty;

    /// <summary>Gives the keyboard up and empties the buffer: the screen is created afresh every
    /// time the original opens it.</summary>
    public void Reset()
    {
        Armed = false;
        Buffer = string.Empty;
    }

    /// <summary>Appends one typed character, answering whether the word has just been completed.
    /// A character that leaves the prefix empties the buffer instead of extending it.</summary>
    public bool Type(char c)
    {
        string next = Buffer + c;
        if (next.Length > _word.Length || !_word.StartsWith(next, StringComparison.Ordinal))
        {
            Buffer = string.Empty;
            return false;
        }

        Buffer = next;
        return next.Length == _word.Length;
    }
}
