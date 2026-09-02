using System.Collections.Generic;
using CSVM.Mech3;

namespace CSVM.UI.Menu;

/// <summary>One line of the briefing's parchment note: the mission objective a flag pin stands
/// for. The text carries its own numbering ("1) Find the main treasure site."), so a renderer
/// numbers nothing itself.</summary>
public sealed record BriefingObjective(int Priority, string Key, string Text);

/// <summary>
/// The objectives note a briefing state binds its flag pins to, read from the mission's own
/// <c>objectives.zrd</c>: every <c>IDENTITY [class, priority, MSG_BRF_*]</c> that carries a
/// message key, ordered by priority. An <c>Objective id index</c> opcode indexes this list 0-based
/// (docs/formats/briefing.md, docs/formats/objectives.md).
///
/// <para>Two readings are ruled out by the data. Keyless <c>IDENTITY</c> entries are not lines:
/// counting them puts an empty line first on <c>C5/M04</c>, whose one bound entry is "1) Payback
/// time!". File order is not the order: it would read "Dock with the PANDORA" before a mission's
/// middle objectives, while priority order reproduces the numbering the strings carry.</para>
/// </summary>
public static class BriefingObjectives
{
    /// <summary>Loads a mission's note lines. A missing or unreadable reader yields an empty list,
    /// and an unresolved key yields the raw <c>MSG_*</c> key as its own text: the screen shows
    /// what it has rather than refusing to open.</summary>
    public static IReadOnlyList<BriefingObjective> Load(List<object?> objectivesReader, Messages messages)
    {
        var found = new List<(int Priority, int At, BriefingObjective Line)>();
        if (objectivesReader.Count == 0 || objectivesReader[0] is not List<object?> blocks)
        {
            return System.Array.Empty<BriefingObjective>();
        }

        // ⚠ The reader's top level is not strict key/value pairs: a bare flag between two blocks
        // shifts every pair after it, so step by what is there rather than by two.
        for (int i = 0; i < blocks.Count; i++)
        {
            if (blocks[i] is not string name || i + 1 >= blocks.Count
                || blocks[i + 1] is not List<object?> body)
            {
                continue;
            }

            i++;
            if (!name.StartsWith("OBJECTIVE", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Collect(body, messages, found);
        }

        // File position breaks a priority tie, so the order is the reader's own where the data
        // leaves it open rather than whatever the sort happens to do.
        found.Sort((a, b) => a.Priority == b.Priority
            ? a.At.CompareTo(b.At)
            : a.Priority.CompareTo(b.Priority));
        var lines = new List<BriefingObjective>(found.Count);
        foreach (var entry in found)
        {
            lines.Add(entry.Line);
        }

        return lines;
    }

    // ⚠ Every IDENTITY in the block, not the block's IDENTITY: C4/M05's OBJECTIVE23 authors two,
    // and a keyed view of the block keeps only the last, losing that mission's third note line.
    private static void Collect(
        List<object?> body, Messages messages, List<(int Priority, int At, BriefingObjective Line)> found)
    {
        for (int i = 0; i + 1 < body.Count; i++)
        {
            if (body[i] is not string field || body[i + 1] is not List<object?> value)
            {
                continue;
            }

            i++;
            if (field.Equals("IDENTITY", System.StringComparison.OrdinalIgnoreCase)
                && value.Count >= 3 && value[1] is float priority
                && value[2] is string key && key.Length > 0)
            {
                found.Add(((int)priority, found.Count,
                    new BriefingObjective((int)priority, key, messages.Get(key))));
            }
        }
    }
}
