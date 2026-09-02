using System;
using System.Collections.Generic;

namespace CSVM.UI.Menu;

/// <summary>One flyable chapter world: its terrain-database code, and whether its Instant Action
/// data ships Danger Zones, the fact the Stunt Flying roster filters on.</summary>
public readonly record struct MenuChapter(string Code, bool DangerZones);

/// <summary>
/// The eight chapter worlds every non-campaign mode picks from, in code order. The lettered codes
/// are separate terrain databases, not lighting variants (docs/formats/spawns.md). Danger Zones
/// marks the chapters whose <c>ia.json</c> ships a <c>dzones</c> list; Stunt Flying offers only
/// those, since a stunt run elsewhere would be an empty free flight. Shared so that no feature
/// and no presentation carries a second copy of the roster or of the flag.
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

    private static readonly MenuChapter[] WithDangerZones = Array.FindAll(Rows, c => c.DangerZones);

    /// <summary>Every chapter, in the order every roster offers them.</summary>
    public static IReadOnlyList<MenuChapter> All => Rows;

    /// <summary>The roster a mode offers: Stunt Flying only the chapters with Danger Zones, every
    /// other mode all eight.</summary>
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
}
