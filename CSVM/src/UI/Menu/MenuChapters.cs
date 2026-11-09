using System;
using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Spec;

namespace CSVM.UI.Menu;

/// <summary>One flyable chapter world: its terrain-database code, and whether its Instant Action
/// data ships Danger Zones, the fact the Stunt Flying roster filters on.</summary>
public readonly record struct MenuChapter(string Code, bool DangerZones);

/// <summary>
/// The eight chapter worlds every non-campaign mode picks from, in code order. The lettered codes
/// are separate terrain databases, not lighting variants (docs/formats/spawns.md). Danger Zones
/// marks the chapters whose <c>ia.json</c> ships a <c>dzones</c> list. Instant Action's Stunt Flying
/// also counts a chapter's authored courses. Shared so that no feature and no presentation carries
/// a second copy of the roster or of the flag.
/// </summary>
public static class MenuChapters
{
    private static readonly MenuChapter[] Rows =
    {
        new("C1", true),
        new("C1B", true),
        new("C1C", false),
        new("C2", true),
        new("C2B", false),
        new("C3", true),
        new("C4", true),
        new("C5", true),
    };

    // Each chapter's one English name in every mode, a remake-only rule: the original's two boxes
    // word the maps differently (docs/formats/instant-action.md#environment-mapping). The id is
    // the langui row a translated install reads: the Multiplayer Environment row (10558 + row), or
    // Instant Action's clouds row for C2B.
    private static readonly (string Code, string English, int StringId)[] Names =
    {
        ("C1", "Airfield", 10562),
        ("C1B", "The Ocean", 10563),
        ("C1C", "Above the Clouds", 10558),
        ("C2", "Hollywood", 10560),
        ("C2B", "The Clouds", 3651),
        ("C3", "Hawaii", 10559),
        ("C4", "Sky Haven", 10564),
        ("C5", "Manhattan", 10561),
    };

    private static readonly MenuChapter[] WithDangerZones = Array.FindAll(Rows, c => c.DangerZones);

    /// <summary>Every chapter, in the order every roster offers them.</summary>
    public static IReadOnlyList<MenuChapter> All => Rows;

    /// <summary>The roster a mode offers: Stunt Flying only the chapters with Danger Zones, every
    /// other mode all eight. Authored courses are the Instant Action feature's to add.</summary>
    public static IReadOnlyList<MenuChapter> For(MenuMode mode) =>
        mode == MenuMode.Stunt ? WithDangerZones : All;

    /// <summary>The chapter behind a code, or null when no chapter carries it.</summary>
    public static MenuChapter? Find(string code)
    {
        foreach (var chapter in All)
        {
            if (chapter.Code == code)
            {
                return chapter;
            }
        }

        return null;
    }

    /// <summary>Whether a chapter ships Danger Zones; false for a code no chapter carries.</summary>
    public static bool DangerZonesFor(string code) => Find(code)?.DangerZones ?? false;

    /// <summary>The name Free Flight, Instant Action and Multiplayer all show for a chapter. A
    /// translated <paramref name="strings"/> gives the game's own word, read through one row per
    /// chapter so the modes agree. Otherwise, and where that row is missing, the English name
    /// applies. A code no chapter carries is its own name.</summary>
    public static string Name(string code, UiStrings? strings = null)
    {
        foreach (var (chapter, english, id) in Names)
        {
            if (chapter == code)
            {
                // A leading ] marks some lobby rows and is not drawn (docs/formats/strings.md).
                string word = strings is { IsTranslation: true } ? strings.Text(id).Trim().TrimStart(']') : "";
                return word.Length > 0 ? word : english;
            }
        }

        return code;
    }
}
