using System;

namespace CSVM.Flight;

/// <summary>Selecting and auto-advancing across a weapon's ammo slots — rocket hardpoints (H) and
/// firable gun groups (G) alike — as pure index math. <see cref="FireControl"/>'s internal cursor:
/// nothing else calls it, and its behaviour is proven through FireControl's own interface
/// (FireControlTests). Slots are addressed by index; <c>ammoAt</c> reports a slot's remaining
/// rounds and <c>infinite</c> models <c>--infinite-ammo</c> (every slot always counts as armed).
/// Each slot is its own selectable position regardless of what it carries, so the selector steps
/// across slots, not ordnance/weapon types.</summary>
internal static class WeaponCursor
{
    /// <summary>The slot the next round fires from: <paramref name="from"/> itself while it still has
    /// rounds, else the next armed slot scanning forward and wrapping. <c>-1</c> when every slot is
    /// empty. This is the firing cursor — it stays on the selected slot until that slot drains, then
    /// advances to the next armed slot the moment it empties.</summary>
    public static int NextArmed(int count, Func<int, int> ammoAt, int from, bool infinite)
    {
        for (int k = 0; k < count; k++)
        {
            int idx = Wrap(from + k, count);
            if (infinite || ammoAt(idx) > 0)
            {
                return idx;
            }
        }
        return -1;
    }

    /// <summary>Where the manual selector (H / G) moves: the next armed slot strictly after
    /// <paramref name="from"/> (wrapping), or <paramref name="from"/> itself when no other slot is
    /// armed. Empty slots are skipped so the selector always lands on a firable one.</summary>
    public static int NextSelectable(int count, Func<int, int> ammoAt, int from, bool infinite)
    {
        if (count == 0)
        {
            return 0;
        }
        for (int k = 1; k <= count; k++)
        {
            int idx = Wrap(from + k, count);
            if (infinite || ammoAt(idx) > 0)
            {
                return idx;
            }
        }
        return Wrap(from, count);
    }

    private static int Wrap(int i, int count) => ((i % count) + count) % count;
}
