using System;
using System.Collections.Generic;

namespace CSVM.Flight;

/// <summary>A firable gun group as <see cref="FireControl"/> sees it: the weapon (fire rate, loop
/// sound), a mutable per-group ammo counter, and how many muzzles the group alternates across.
/// <see cref="GunGroup"/> implements it; tests hand in fakes.</summary>
public interface IGunSlot
{
    WeaponDef Weapon { get; }
    int Ammo { get; set; }
    int Capacity { get; }
    int MuzzleCount { get; }
}

/// <summary>One rocket pylon as <see cref="FireControl"/> sees it: the ordnance weapon and a
/// mutable per-pylon ammo counter. <see cref="Hardpoint"/> implements it; tests hand in fakes.</summary>
public interface IPylonSlot
{
    WeaponDef Weapon { get; }
    int Ammo { get; set; }
    int Capacity { get; }
}

/// <summary>One sim tick's raw control state, polled by the caller. Held levels, not edges — all
/// edge detection (first-shot-on-press, one-rocket-per-pull, selector steps) lives inside
/// <see cref="FireControl"/>, where it is testable.</summary>
public struct FireInputs
{
    public bool FireHeld;          // gun trigger (Space / pad B; --fire ORs in adapter-side)
    public bool RocketHeld;        // rocket trigger (F / pad A)
    public bool GunSelectHeld;     // gun-group selector (G / D-pad Left)
    public bool RocketSelectHeld;  // hardpoint selector (H / D-pad Right)
}

/// <summary>What one <see cref="FireControl.Step"/> decided, for the caller to perform against the
/// engine. A single persistent instance is reused every tick (cleared on entry) so the 60 Hz path
/// allocates nothing — read it before the next Step.</summary>
public sealed class FireOutcome
{
    /// <summary>Rounds to spawn this tick, in firing order: gun-slot index + the muzzle index the
    /// group's rotation picked. Usually 0–1 entries; more when a slow frame spans several fire
    /// intervals. The caller resolves the muzzle node's transform and spawns.</summary>
    public List<(int Group, int Muzzle)> GunShots { get; } = new();

    /// <summary>The pylon index to launch a rocket from this tick, or -1 for none.</summary>
    public int RocketPylon { get; set; } = -1;

    /// <summary>The firing loop should be sounding this tick (the selected group is firing with
    /// ammo). Distinct from <see cref="GunLoopSound"/> because a gun without a looped sound still
    /// wants the loop state on — the caller's start call is then a no-op, exactly as before the
    /// extraction.</summary>
    public bool GunLoopWanted { get; set; }

    /// <summary>The looped sound the firing group carries, when <see cref="GunLoopWanted"/>.</summary>
    public string? GunLoopSound { get; set; }

    /// <summary>Sound the empty-clip cue: the selected gun ran dry with every group spent (once,
    /// re-armed by a real shot).</summary>
    public bool GunDryCue { get; set; }

    /// <summary>Sound the empty-clip cue: a rocket pull with every pylon empty (once, re-armed by
    /// a launch or a refill).</summary>
    public bool RocketDryCue { get; set; }

    internal void Clear()
    {
        GunShots.Clear();
        RocketPylon = -1;
        GunLoopWanted = false;
        GunLoopSound = null;
        GunDryCue = false;
        RocketDryCue = false;
    }
}

/// <summary>The fire-control state machine behind <see cref="FlightController"/>:
/// trigger edges, per-group fire-rate accumulators, muzzle rotation, ammo draw-down, the two weapon
/// selectors with their on-empty auto-advance, the rocket launch gate and both dry-clip cues — as a
/// plain engine-free class. <see cref="Step"/> consumes raw held inputs and returns spawn/sound
/// <b>decisions</b> (<see cref="FireOutcome"/>); the caller performs them (muzzle transforms,
/// <see cref="ProjectilePool"/>, <see cref="FlightAudio"/>). Ammo mutates through the slot views so
/// a decision and its state can never diverge mid-tick — the gauges keep reading the same
/// <see cref="Loadout"/> counters. <see cref="WeaponCursor"/> is this module's internal index math.
/// Proven in <c>CSVM.Tests/FireControlTests</c>; the in-engine <c>weapons-fire</c> suite covers only
/// the mounting census.</summary>
public sealed class FireControl
{
    private readonly IReadOnlyList<IGunSlot> _guns;
    private readonly IReadOnlyList<IPylonSlot> _pylons;
    private readonly GunState[] _states;      // per gun slot, in _guns order
    private readonly FireOutcome _outcome = new();

    private bool _firePrev;          // previous tick's gun trigger (immediate first shot on press)
    private bool _gunSelPrev;        // edge detection for the gun-selector button
    private bool _rocketSelPrev;     // edge detection for the hardpoint-selector button
    private bool _rocketFirePrev;    // previous tick's rocket trigger (one rocket per discrete pull)
    private bool _rocketDryWarned;   // the all-pylons-empty cue has already sounded
    private float _rocketCooldown;   // s until the next rocket may launch (FIRE_RATE gate)
    private int _gunSel;             // gun selector: 0-based slot that fires (only ONE at a time)
    private int _selectedPylon;      // the pylon H selects and rockets fire from

    public FireControl(IReadOnlyList<IGunSlot> guns, IReadOnlyList<IPylonSlot> pylons,
        bool autoFireRockets, bool infiniteAmmo, int initialGunSelect = 0)
    {
        _guns = guns;
        _pylons = pylons;
        AutoFireRockets = autoFireRockets;
        InfiniteAmmo = infiniteAmmo;
        _states = new GunState[guns.Count];
        for (int i = 0; i < _states.Length; i++)
        {
            _states[i] = new GunState();
        }
        _gunSel = guns.Count > 0 ? Math.Clamp(initialGunSelect, 0, guns.Count - 1) : 0;
    }

    /// <summary>The firing gun slot — what the trigger fires, the gauge arrow marks and the
    /// reticle aims for.</summary>
    public int GunSel => _gunSel;

    /// <summary>The selected pylon — what H points at and the rocket trigger launches from.</summary>
    public int SelectedPylon => _selectedPylon;

    /// <summary>--fire-rockets / the weapon lab's auto toggle: a held rocket trigger auto-repeats
    /// at the cooldown cap instead of one launch per discrete pull. Runtime-mutable — the lab
    /// flips it live when switching banks, so the adapter mirrors it in every Step.</summary>
    public bool AutoFireRockets { get; set; }

    /// <summary>--infinite-ammo / the lab's toggle: every slot always counts as armed and no
    /// counter depletes. Runtime-mutable, mirrored like <see cref="AutoFireRockets"/>.</summary>
    public bool InfiniteAmmo { get; set; }

    /// <summary>Point the gun selector at a slot (0-based, clamped) — the weapon lab's programmatic
    /// twin of G, which only cycles.</summary>
    public void SelectGunGroup(int index) =>
        _gunSel = _guns.Count > 0 ? Math.Clamp(index, 0, _guns.Count - 1) : 0;

    /// <summary>Point the hardpoint selector at a pylon (0-based, clamped). Unlike H this lands on
    /// an EMPTY pylon too (the lab picks a mount to look at, not a mount to fire); the firing
    /// path's own armed scan still advances off it on the next pull.</summary>
    public void SelectPylon(int index) =>
        _selectedPylon = _pylons.Count > 0 ? Math.Clamp(index, 0, _pylons.Count - 1) : 0;

    /// <summary>One sim tick: selector edges, then the gun fire clocks, then the rocket gate — the
    /// order the flight sim always ran them in. Mutates slot ammo; returns the reused
    /// <see cref="FireOutcome"/> with everything the caller must perform.</summary>
    public FireOutcome Step(float dt, in FireInputs input)
    {
        _outcome.Clear();
        StepSelectors(input);
        StepGuns(dt, input.FireHeld);
        StepRockets(dt, input.RocketHeld);
        return _outcome;
    }

    /// <summary>Full reload (respawn): every owned slot back to capacity, fire clocks and muzzle
    /// rotations reset, dry warnings re-armed, the pylon cursor back to pylon 0 (it doubles as the
    /// firing cursor). The gun pick deliberately persists across a respawn.</summary>
    public void Refill()
    {
        foreach (var g in _guns)
        {
            g.Ammo = g.Capacity;
        }
        foreach (var st in _states)
        {
            st.Accum = 0f;
            st.Warned = false;
            st.NextMuzzle = 0;
        }
        foreach (var p in _pylons)
        {
            p.Ammo = p.Capacity;
        }
        _rocketCooldown = 0f;
        _selectedPylon = 0;
        _rocketFirePrev = false;
        _rocketDryWarned = false;
    }

    // Advances each weapon selector on the rising edge of its button to the next armed
    // slot (one active at a time, skipping empties). Both cursors also auto-advance on their own
    // when the selected slot empties (in the gun/rocket steps).
    private void StepSelectors(in FireInputs input)
    {
        bool gunSel = input.GunSelectHeld;
        if (gunSel && !_gunSelPrev && _guns.Count > 1)
        {
            _gunSel = WeaponCursor.NextSelectable(
                _guns.Count, i => _guns[i].Ammo, _gunSel, InfiniteAmmo);
        }
        _gunSelPrev = gunSel;

        bool rocketSel = input.RocketSelectHeld;
        if (rocketSel && !_rocketSelPrev && _pylons.Count > 1)
        {
            _selectedPylon = WeaponCursor.NextSelectable(
                _pylons.Count, i => _pylons[i].Ammo, _selectedPylon, InfiniteAmmo);
        }
        _rocketSelPrev = rocketSel;
    }

    // Advances every gun slot's fire clock: while the trigger is held, the selected slot
    // emits shots at its `FIRE_RATE` (alternating muzzles so the slot's total rate equals it),
    // drawing from its own ammo counter. When the selected slot runs dry the selection
    // auto-advances to the next slot with ammo (the moment it empties); the empty-clip cue sounds
    // only once every slot is spent.
    private void StepGuns(float dt, bool fire)
    {
        bool wantLoop = false;
        string? loopSound = null;
        for (int gi = 0; gi < _guns.Count; gi++)
        {
            var g = _guns[gi];
            var st = _states[gi];
            // Only the selected gun slot fires — one at a time (the original's behaviour).
            bool selected = gi == _gunSel;
            if (!fire || !selected || g.Weapon.FireRate <= 0f || g.MuzzleCount == 0)
            {
                st.Accum = 0f;
                continue;
            }
            float interval = 1f / g.Weapon.FireRate;
            if (!_firePrev)
            {
                st.Accum = interval; // the first shot leaves the barrel the instant the trigger goes down
            }
            st.Accum += dt;
            if (g.Ammo > 0 || InfiniteAmmo)
            {
                wantLoop = true;
                loopSound ??= g.Weapon.LoopedSoundName;
            }
            while (st.Accum >= interval)
            {
                st.Accum -= interval;
                if (g.Ammo > 0 || InfiniteAmmo)
                {
                    _outcome.GunShots.Add((gi, st.NextMuzzle % g.MuzzleCount));
                    st.NextMuzzle++;
                    if (!InfiniteAmmo)
                    {
                        g.Ammo--;
                    }
                    st.Warned = false; // it fired a real round — re-arm the dry warning
                }
                else
                {
                    // The selected slot just ran dry — switch to the next slot that still has ammo
                    // the moment it empties, not on the next trigger pull. Only when no slot has
                    // ammo left does the dry cue sound.
                    int next = WeaponCursor.NextArmed(
                        _guns.Count, i => _guns[i].Ammo, _gunSel, InfiniteAmmo);
                    if (next >= 0)
                    {
                        _gunSel = next;
                    }
                    else if (!st.Warned)
                    {
                        st.Warned = true;
                        _outcome.GunDryCue = true;
                    }
                    st.Accum = 0f;
                    break;
                }
            }
        }
        _outcome.GunLoopWanted = wantLoop;
        _outcome.GunLoopSound = wantLoop ? loopSound : null;
        _firePrev = fire;
    }

    // The rocket gate: one launch per discrete trigger pull, drawn from the selected
    // pylon (it drains fully, then the cursor auto-advances to the next armed pylon the instant it
    // empties), gated by the weapon's `FIRE_RATE` cooldown. A pull with every pylon empty
    // sounds the dry cue once — the cue check runs BEFORE the cooldown gate, so a dry pull is
    // never swallowed by a hot cooldown.
    private void StepRockets(float dt, bool held)
    {
        if (_pylons.Count == 0)
        {
            return;
        }
        if (_rocketCooldown > 0f)
        {
            _rocketCooldown -= dt;
        }
        bool fire = AutoFireRockets || held;
        // A human pull fires one rocket; holding does not auto-repeat. Only --fire-rockets (soak
        // runs) auto-repeats — and either way the FIRE_RATE cooldown caps the launch rate.
        bool pull = AutoFireRockets ? fire : (fire && !_rocketFirePrev);
        _rocketFirePrev = fire;
        if (!pull)
        {
            return;
        }
        // The selected pylon while it still has ordnance, else the next armed pylon scanning from
        // it and wrapping — the cursor self-heals if it was somehow left on a spent pylon.
        int idx = WeaponCursor.NextArmed(
            _pylons.Count, i => _pylons[i].Ammo, _selectedPylon, InfiniteAmmo);
        if (idx < 0)
        {
            if (!_rocketDryWarned)
            {
                _rocketDryWarned = true;
                _outcome.RocketDryCue = true;
            }
            return;
        }
        _selectedPylon = idx;
        if (_rocketCooldown > 0f)
        {
            return;
        }
        _rocketDryWarned = false;
        var hp = _pylons[idx];
        _outcome.RocketPylon = idx;
        if (!InfiniteAmmo)
        {
            hp.Ammo--;
            // Advance the moment the selected pylon empties — not on the next trigger pull — so
            // the gauge arrow leaves the spent pylon straight away; -1 (all empty) leaves it put
            // so the next pull sounds the dry cue.
            int next = WeaponCursor.NextArmed(_pylons.Count, i => _pylons[i].Ammo, _selectedPylon, false);
            if (next >= 0)
            {
                _selectedPylon = next;
            }
        }
        _rocketCooldown = hp.Weapon.FireRate > 0f ? 1f / hp.Weapon.FireRate : 1f;
    }

    private sealed class GunState
    {
        public float Accum;      // fire-clock accumulator, s
        public int NextMuzzle;   // muzzle-rotation cursor (monotonic; modulo picks the muzzle)
        public bool Warned;      // the all-slots-spent cue has sounded for this slot's dry burst
    }
}
