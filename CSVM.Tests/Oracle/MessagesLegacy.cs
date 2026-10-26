using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CSVM.Mech3;
using CSVM.Spec;

namespace CSVM.Tests.Oracle;

/// <summary>The engine's former read of the message table, the extraction's <c>messages.json</c>.
/// It is the oracle <see cref="Messages.FromStringsDll"/> is compared against, and the units that
/// state a table as JSON read through it. Test-only.</summary>
public static class MessagesLegacy
{
    /// <summary>The table of a <c>messages.json</c> file; a missing file is an empty table.</summary>
    public static Messages Load(string path) =>
        File.Exists(path) ? Read(JsonDocument.Parse(File.ReadAllBytes(path))) : new Messages();

    /// <summary>Game data holding the table extracted under <paramref name="dataRoot"/>, for a unit
    /// that hands a screen the same data root it reads everything else from. Null is no data.</summary>
    public static GameData Data(string? dataRoot) => dataRoot == null
        ? GameData.Empty
        : new GameData { Messages = Load(Path.Combine(dataRoot, "extracted", "messages.json")) };

    /// <summary>The same table out of JSON already in hand.</summary>
    public static Messages Parse(string json) => Read(JsonDocument.Parse(json));

    // Each entry's key and value, an entry missing either skipped, as the engine read them.
    private static Messages Read(JsonDocument doc)
    {
        var entries = new List<(string, string)>();
        using (doc)
        {
            if (doc.RootElement.TryGetProperty("entries", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in list.EnumerateArray())
                {
                    if (e.TryGetProperty("key", out var k) && k.GetString() is { } key
                        && e.TryGetProperty("value", out var v) && v.GetString() is { } value)
                    {
                        entries.Add((key, value));
                    }
                }
            }
        }

        return Messages.Of(entries);
    }
}
