using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Mech3;
using CSVM.Spec;

namespace CSVM.Tests;

/// <summary>
/// The extraction's message table and menu art, read through the modding path's readers. For a
/// unit that hands a screen the data root it reads everything else from.
/// </summary>
public static class TestExtracted
{
    private static readonly Dictionary<string, TextureSet> Rimages = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Game data holding the table extracted under <paramref name="dataRoot"/>. Null is
    /// no data.</summary>
    public static GameData Data(string? dataRoot) => dataRoot == null
        ? GameData.Empty
        : new GameData { Messages = Messages.FromExtracted(Path.Combine(dataRoot, "extracted", "messages.json")) };

    /// <summary>The <c>rimage</c> art extracted under <paramref name="dataRoot"/>; empty for null or
    /// no extraction. Read once per root.</summary>
    public static TextureSet Rimage(string? dataRoot)
    {
        string? folder = dataRoot == null ? null : Path.Combine(dataRoot, "extracted", "rimage");
        if (folder == null || !Directory.Exists(folder))
        {
            return TextureSet.Empty;
        }

        lock (Rimages)
        {
            if (!Rimages.TryGetValue(folder, out var set))
            {
                set = TextureSet.FromExtracted(GameData.RimagePath, folder);
                Rimages[folder] = set;
            }

            return set;
        }
    }
}
