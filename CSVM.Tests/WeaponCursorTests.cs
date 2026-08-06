using System.Collections.Generic;
using CSVM.Flight;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The weapon slot-selection cursor shared by rocket hardpoints (H) and gun groups (G) — BL-025 plus
/// the on-empty auto-advance the user asked for: the selected slot drains, and the cursor steps to
/// the next armed slot the instant it empties (not on the next trigger pull). The cursor logic is
/// engine-free, so it is proven here rather than in a live node — the deterministic stand-in for the
/// "scripted H-press run" the plan calls for. Because guns and rockets share <see cref="WeaponCursor"/>
/// the same assertions cover both (guns just carry thousands of rounds per slot).
/// </summary>
public class WeaponCursorTests
{
    [Fact]
    public void HStepsThroughEverySlotOnAUniformAmmoLoadout()
    {
        // Four pylons, all the same ordnance, all loaded — the case that never cycled before BL-025.
        int[] ammo = { 3, 3, 3, 3 };
        int sel = 0;
        var steps = new List<int> { sel };
        for (int press = 0; press < 5; press++)
        {
            sel = WeaponCursor.NextSelectable(ammo.Length, i => ammo[i], sel, infinite: false);
            steps.Add(sel);
        }
        // 0 → 1 → 2 → 3 → wrap to 0 → 1: each press advances exactly one slot, wrapping.
        Assert.Equal(new[] { 0, 1, 2, 3, 0, 1 }, steps);
    }

    [Fact]
    public void ManualSelectSkipsEmptySlotsAndLandsOnTheNextArmedOne()
    {
        // Slots 1 and 2 are spent; from slot 0, the selector jumps straight to the armed slot 3.
        int[] ammo = { 2, 0, 0, 2 };
        int sel = WeaponCursor.NextSelectable(ammo.Length, i => ammo[i], from: 0, infinite: false);
        Assert.Equal(3, sel);
        // From 3, wrapping past the empties returns to 0.
        Assert.Equal(0, WeaponCursor.NextSelectable(ammo.Length, i => ammo[i], from: 3, infinite: false));
    }

    [Fact]
    public void ManualSelectStaysPutWhenNoOtherSlotIsArmed()
    {
        int[] ammo = { 0, 3, 0, 0 };
        Assert.Equal(1, WeaponCursor.NextSelectable(ammo.Length, i => ammo[i], from: 1, infinite: false));
    }

    [Fact]
    public void FiringDrainsTheSelectedSlotBeforeAdvancing()
    {
        // Select slot 2, then fire until dry: the first three shots all come from slot 2, and only
        // when it empties does firing advance — to slot 3 (the next armed after 2).
        int[] ammo = { 3, 3, 3, 3 };
        var fired = DrainAll(ammo, start: 2);
        Assert.Equal(new[] { 2, 2, 2, 3, 3, 3, 0, 0, 0, 1, 1, 1 }, fired);
    }

    [Fact]
    public void TheCursorAdvancesOnTheEmptyingShotNotTheNextPull()
    {
        // Two slots of two rounds each, starting on slot 0. The cursor (gauge Selected) after each
        // shot: slot 0 while it still has a round, then slot 1 the instant slot 0's second shot
        // empties it — never lingering on the spent slot 0.
        int[] ammo = { 2, 2 };
        var trace = CursorAfterEachShot(ammo, start: 0, shots: 4);
        Assert.Equal(new[] { 0, 1, 1, 1 }, trace);
    }

    [Fact]
    public void EmptyingTheSelectedSlotAutoAdvancesExactlyOneSlot()
    {
        // One round left on the selected slot: the shot that empties it is the last from that slot,
        // and the very next shot comes from the next armed slot — a single-slot advance.
        int[] ammo = { 1, 5, 0, 0 };
        var fired = DrainAll(ammo, start: 0);
        Assert.Equal(0, fired[0]);   // the emptying shot
        Assert.Equal(1, fired[1]);   // exactly one auto-advance, skipping the empty slots 2 and 3
    }

    [Fact]
    public void AllEmptyReturnsNoArmedSlotSoTheDryCueCanFire()
    {
        // NextArmed returns -1 when every slot is spent — the signal FlightController turns into the
        // empty-clip cue (A2). Confirms the all-empty path stays intact.
        int[] ammo = { 0, 0, 0, 0 };
        Assert.Equal(-1, WeaponCursor.NextArmed(ammo.Length, i => ammo[i], from: 0, infinite: false));
    }

    [Fact]
    public void InfiniteAmmoKeepsTheSelectedSlotAndLetsTheSelectorStepFreely()
    {
        int[] ammo = { 3, 3, 3 };
        // NextArmed never advances off the selected slot (it never empties).
        Assert.Equal(1, WeaponCursor.NextArmed(ammo.Length, i => ammo[i], from: 1, infinite: true));
        // The manual selector still steps to the next slot, wrapping.
        Assert.Equal(2, WeaponCursor.NextSelectable(ammo.Length, i => ammo[i], from: 1, infinite: true));
        Assert.Equal(0, WeaponCursor.NextSelectable(ammo.Length, i => ammo[i], from: 2, infinite: true));
    }

    [Fact]
    public void ASingleSlotNeverMovesUnderTheManualSelector()
    {
        int[] ammo = { 3 };
        Assert.Equal(0, WeaponCursor.NextSelectable(ammo.Length, i => ammo[i], from: 0, infinite: false));
    }

    // A model of the firing loop as UpdateRockets/UpdateGuns run it: NextArmed picks the slot, the
    // caller decrements it, then advances the cursor on-empty (the post-decrement NextArmed). Returns
    // the slot each shot fired from.
    private static List<int> DrainAll(int[] ammo, int start, bool infinite = false, int maxShots = 1000)
    {
        var fired = new List<int>();
        int cursor = start;
        for (int shot = 0; shot < maxShots; shot++)
        {
            int idx = WeaponCursor.NextArmed(ammo.Length, i => ammo[i], cursor, infinite);
            if (idx < 0)
            {
                break; // every slot empty — the dry-cue path
            }
            cursor = idx;
            fired.Add(idx);
            if (infinite)
            {
                break; // infinite never depletes; one representative shot is enough
            }
            ammo[idx]--;
            // On-empty advance: the cursor leaves the slot the moment it drains.
            int next = WeaponCursor.NextArmed(ammo.Length, i => ammo[i], cursor, infinite);
            if (next >= 0)
            {
                cursor = next;
            }
        }
        return fired;
    }

    // The cursor (== the gauge's Selected) sampled after each shot, to prove the on-empty advance.
    private static List<int> CursorAfterEachShot(int[] ammo, int start, int shots)
    {
        var trace = new List<int>();
        int cursor = start;
        for (int s = 0; s < shots; s++)
        {
            int idx = WeaponCursor.NextArmed(ammo.Length, i => ammo[i], cursor, infinite: false);
            if (idx < 0)
            {
                trace.Add(-1);
                break;
            }
            cursor = idx;
            ammo[idx]--;
            int next = WeaponCursor.NextArmed(ammo.Length, i => ammo[i], cursor, infinite: false);
            if (next >= 0)
            {
                cursor = next;
            }
            trace.Add(cursor);
        }
        return trace;
    }
}
