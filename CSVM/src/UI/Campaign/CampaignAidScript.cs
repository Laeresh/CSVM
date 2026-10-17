using System;
using System.Collections.Generic;
using CSVM.UI.Boards;
using CSVM.UI.Menu;
using CSVM.Utils;

namespace CSVM.UI.Campaign;

/// <summary>
/// The presses a campaign screenshot aid's colon argument spells, replayed on the campaign screen
/// once the walk has reached it. A confirm lets an aid leave a drop-down standing open. A segment
/// is a run of verbs with optional repeat counts, or a <see cref="BoardButton"/> word to focus and
/// confirm. The verbs are <c>d</c> down, <c>u</c> up, <c>l</c> and <c>r</c> the stepper, <c>a</c>
/// confirm, <c>b</c> back and <c>x</c> the secondary press; <c>-</c> joins segments. A bare count is
/// a run of downs, and <c>export</c> names <see cref="BoardButton.ExportPlane"/>. So <c>4da</c> and
/// <c>4d-a</c> both move down four rows and open the list standing there.
/// </summary>
public static class CampaignAidScript
{
    /// <summary>The verbs the grammar reads.</summary>
    public const string Verbs = "dulrabx";

    /// <summary>The verbs a presentation binding no secondary press can reach. Original selects a
    /// row by clicking it and binds no X at all, so nobody at its controls can press <c>x</c>. The
    /// grammar keeps the verb and the replay refuses it.</summary>
    public const string VerbsWithoutSecondary = "dulrab";

    /// <summary>What joins two segments, so a button word can stand beside a run of verbs.</summary>
    public const char Separator = '-';

    // The one button word the aids' own commands already spell. Every other button is named by
    // its own member name, so the vocabulary needs no entry per button.
    private static readonly Dictionary<string, BoardButton> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        [CampaignAidProfiles.ExportArgument] = BoardButton.ExportPlane,
    };

    /// <summary>Reads an argument as a script. Empty for an argument that spells no press at all,
    /// which is every word neither a verb run nor a button: an unreadable argument leaves the flow
    /// where the walk left it, the way an unparseable step count did.</summary>
    public static IReadOnlyList<Step> Parse(string argument)
    {
        var presses = new List<Step>();
        foreach (string segment in (argument ?? string.Empty).Split(Separator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryButton(segment, out BoardButton button))
            {
                presses.Add(new Step('\0', 1, button));
            }
            else if (!TryRun(segment, presses))
            {
                return Array.Empty<Step>();
            }
        }

        return presses;
    }

    /// <summary>The presses an argument spells, or null when it spells one <paramref name="verbs"/>
    /// cannot reach: a presentation lacking a press refuses the whole script rather than replaying
    /// the half it reaches and handing back a shot of a screen that looks like it did not respond.
    /// <paramref name="controls"/> names the refuser. ⚠ A caller that gets null ends the run: the
    /// message says so, and a probe that shot the screen anyway would read as an answer.</summary>
    public static IReadOnlyList<Step>? Presses(string argument, string verbs, string controls)
    {
        IReadOnlyList<Step> script = Parse(argument);
        foreach (Step press in script)
        {
            if (press.Button == BoardButton.None && (verbs ?? string.Empty).IndexOf(press.Verb) < 0)
            {
                Log.Error("ui", $"--menu= aid refused: {controls} has no '{press.Verb}' press, so '{argument}' spells nothing it can replay and the run ends without a shot (verbs it takes: {verbs})");
                return null;
            }
        }

        return script;
    }

    // A button word: an alias first, then the button's own member name. Checked before the verbs
    // so a word is read whole; no button's name is spelled out of the verb letters alone.
    private static bool TryButton(string segment, out BoardButton button)
    {
        if (Aliases.TryGetValue(segment, out button))
        {
            return true;
        }

        return Enum.TryParse(segment, ignoreCase: true, out button)
            && button != BoardButton.None
            && !int.TryParse(segment, out _); // Enum.TryParse takes a number as its own value
    }

    // One run of counted verbs. False for a segment carrying anything else, which voids the whole
    // script rather than replaying the half of it that read.
    private static bool TryRun(string segment, List<Step> presses)
    {
        int? count = null;
        bool fraction = false;
        foreach (char raw in segment)
        {
            char c = char.ToLowerInvariant(raw);
            if (c is >= '0' and <= '9')
            {
                count = fraction ? count : ((count ?? 0) * 10) + (c - '0');
            }
            else if (c == '.' && count.HasValue && !fraction)
            {
                // A step count arrived as a float once and its fraction was truncated, so a
                // command still spelling one steps the same whole number of rows.
                fraction = true;
            }
            else if (Verbs.IndexOf(c) < 0)
            {
                return false;
            }
            else
            {
                presses.Add(new Step(c, count ?? 1));
                count = null;
                fraction = false;
            }
        }

        if (count.HasValue)
        {
            presses.Add(new Step('d', count.Value)); // the bare step count
        }

        return true;
    }

    /// <summary>One step of a script: a cursor verb repeated <paramref name="Count"/> times, or,
    /// when <paramref name="Button"/> is not <see cref="BoardButton.None"/>, that button's own
    /// press, whose <paramref name="Verb"/> is unread.</summary>
    public readonly record struct Step(char Verb, int Count, BoardButton Button = BoardButton.None);
}
