using System;
using System.Collections.Generic;
using CSVM.Bindings;
using CSVM.Flight.Airframe;
using CSVM.Flight.Hud;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Weapons;

/// <summary>What one frame of <see cref="SeatTargeting.Step"/> reads off its aircraft. That is the
/// pools the scan walks, the team the selection is classed against, the sim pose and the selected
/// ordnance. It also says whether the pilot is in play, and names the pane's player index and the
/// targeting HUD whose spyglass the toggle flips.</summary>
public readonly record struct TargetingFrame(
    ProjectilePool? Pools,
    ISurfaceVehicles? Vehicles,
    int Team,
    Vector3 Position,
    Basis Attitude,
    WeaponDef? Ordnance,
    bool InPlay,
    int PlayerIndex,
    TargetHud? Hud);

/// <summary>One human seat's targeting input, run on the rendered frame. It scans the candidates
/// into the pilot's <see cref="TargetSelection"/> and prunes the attacker queue of the dead. It spends
/// <c>--target=</c>'s one application, then reads the eleven targeting keys, the pad's tap/hold
/// splitter and the spyglass toggle. Rebuild-then-input is the original's order, so a class change
/// reads one frame late and self-heals. Nothing in the sim step reads its state. Decode:
/// docs/org/targeting.md.</summary>
public sealed class SeatTargeting
{
    private const int InitialTargetGrace = 300;    // frames --target= waits for the pool to fill
    private const int MaxNamed = 24;    // a C1 session offers ~100; enough to recognise a typo, not a wall

    private readonly object _self;
    private readonly PlayerActions _seat;    // keyboard, mouse and pad together
    private readonly PlayerActions _keys;    // the keyboard and mouse half alone
    private readonly PlayerActions _pads;    // the pad half alone, flight sticks included
    private readonly AimCandidateSet _scan = new();   // the scan, rebuilt per frame
    private readonly List<AimCandidate> _parts = new(); // this frame's selectable sub-parts
    private readonly List<AimCandidate> _sites = new(); // this frame's objective sites
    private readonly bool[] _keyPrev = new bool[13];  // the eleven targeting keys, spyglass pair last
    // D-pad Up down longer than the shared threshold is a HOLD, not a tap. The two weapon
    // selectors' pad buttons split on the same number, inside FireControl.
    private readonly TapHoldButton _hold = new(TapHoldButton.PadHoldSeconds);
    private bool _initialDone;          // --target= has had its one chance
    private int _initialWaits;          // ...frames it has waited for a non-empty pool

    /// <summary>Builds the input for <paramref name="self"/>, the aircraft its selection never
    /// offers back to it, over that seat's whole keymap and its keyboard and pad halves.</summary>
    public SeatTargeting(object self, PlayerActions seat, PlayerActions keys, PlayerActions pads)
    {
        ArgumentNullException.ThrowIfNull(self);
        ArgumentNullException.ThrowIfNull(seat);
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(pads);
        _self = self;
        _seat = seat;
        _keys = keys;
        _pads = pads;
    }

    /// <summary>Appends the zeppelin sub-parts to the pool each frame,
    /// <c>ZeppelinRuntime.CollectTargetParts</c>, bound once the zeppelins exist. Null in a session
    /// with none, and the pool takes them only under the torpedo gate (<see cref="TargetPool.Rebuild"/>).
    /// A delegate because the zeppelins are built after the rigs.</summary>
    public Action<List<AimCandidate>>? SubParts { get; set; }

    /// <summary>Appends this pilot's live objective sites to the pool each frame. They are the
    /// campaign mission's (<c>ObjectiveSites.Collect</c>) or a stunt run's unflown Danger Zones
    /// (<c>StuntMission.CollectTargets</c>). Null in a session with neither. It is a channel apart
    /// from <see cref="SubParts"/>, since a site carries the mission's own flag onto a cycle. A
    /// sub-part is offered under a flag nothing authors for it.</summary>
    public Action<List<AimCandidate>>? Objectives { get; set; }

    /// <summary><c>--target=</c>'s spec, or null for an unscripted session. Applied ONCE, on the
    /// first frame the selection's pool has anything in it, and never consulted again. It sets the
    /// initial selection without holding it, so an interactive session still cycles normally.</summary>
    public string? InitialTarget { get; set; }

    /// <summary>The pad's tap/hold splitter: the next-enemy row's pad and stick half, read this
    /// frame.</summary>
    public bool SplitterDown => _pads.Held(InputAction.TargetNextEnemy);

    /// <summary>Whether one targeting row reads down this frame. ⚠ The next-enemy row reads the
    /// keyboard half alone, since its pad and stick controls reach the splitter, which would
    /// otherwise dispatch it twice. Every other row reads the whole seat, or a stick button bound to
    /// it would do nothing.</summary>
    public bool RowDown(InputAction action) =>
        action == InputAction.TargetNextEnemy ? _keys.Held(action) : _seat.Held(action);

    /// <summary>One frame: rebuild the pool and re-resolve, prune the attacker queue, apply
    /// <c>--target=</c>, then dispatch this frame's input into <paramref name="sel"/>. The input
    /// is read only while <see cref="TargetingFrame.InPlay"/>; the selection re-resolves
    /// regardless.</summary>
    public void Step(TargetSelection sel, in TargetingFrame frame, float dt)
    {
        ArgumentNullException.ThrowIfNull(sel);
        if (frame.Pools != null && sel.ActiveClass != null)
        {
            // Skipped with the selection cleared. `Target Nothing` zeroes the class flags and the
            // original then skips its whole collection pass, which keeps the clear cleared.
            _scan.Clear();
            frame.Pools.CollectAircraft(_scan);
            frame.Vehicles?.CollectVehicles(_scan);
            // No turret pass. A gun reaches the pilot's cycle only where a targets.zrd record
            // names its node, and then arrives on the site feed as any structure does.
            // No shipped table names one, so collecting them would be unread work.

            // The fourth pool: a TARGETABLE round in flight is selectable, which is why a torpedo
            // can be locked and shot at. The pool itself reads the admission byte.
            frame.Pools.CollectFusedOrdnance(_scan);
            _parts.Clear();
            SubParts?.Invoke(_parts);
            // The objective sites, rebuilt from their live sources every frame. A site under a
            // moving node is then marked where it now is rather than where it was.
            _sites.Clear();
            Objectives?.Invoke(_sites);
        }
        // The team is the frame's, read off the aircraft's own field. Deriving it from the player
        // index is wrong for every other pane once a mission sets teams (see TargetHud.OwnTeam).
        sel.Rebuild(_scan, _parts, frame.Team, _self, frame.Position, frame.Attitude, _sites,
            frame.Ordnance);

        // The death prune (FUN_004a64e0). There is no session-wide Downed broadcast outside --vs,
        // so this pane prunes its own queue: a shot-down attacker must not be offered again.
        for (int i = sel.Attackers.Count - 1; i >= 0; i--)
        {
            if (sel.Attackers[i] is FlightController { InPlay: false } dead)
                sel.ForgetTarget(dead);
        }

        // --target= comes before the input dispatch and the InPlay gate. A --det run pins its
        // selection with nobody pressing anything, and a real keypress on the same frame wins.
        if (InitialTarget != null && !_initialDone)
        {
            ApplyInitialTarget(sel, frame);
        }

        // ⚠ Gate the INPUT on InPlay, not the rebuild. A downed pilot's freecam binds `U`, so a
        // spectator's read re-targets a plane that is not there and fights the camera.
        if (!frame.InPlay)
        {
            _hold.Step(false, dt);   // let a button held through the crash resolve as nothing
            return;
        }

        // D-pad Up, the pad's one targeting button: tap steps the enemy cycle, hold selects the
        // target nearest the crosshair. TapHoldButton owns the timing and the release rule.
        switch (_hold.Step(SplitterDown, dt))
        {
            case TapHold.Hold:
                sel.NearestCrosshairs(frame.Position, frame.Attitude);
                break;
            case TapHold.Tap:
                sel.NextEnemy();
                break;
        }

        // The keyboard set: every one of the original's eleven targeting keys collides with our
        // flight scheme, so the shipped keys are this port's own. All eleven actions are here, one
        // per class per direction plus the two class-less ones, and each is rebindable.
        DispatchKey(0, InputAction.TargetNextEnemy, () => sel.NextEnemy());
        DispatchKey(1, InputAction.TargetNextAlly, () => sel.Next(TargetClass.Ally));
        DispatchKey(2, InputAction.TargetNextNonAircraft, () => sel.Next(TargetClass.NonAircraft));
        var position = frame.Position;
        var attitude = frame.Attitude;
        DispatchKey(3, InputAction.TargetNearest, () => sel.NearestCrosshairs(position, attitude));
        DispatchKey(4, InputAction.TargetClear, () => sel.Clear());
        DispatchKey(5, InputAction.TargetPreviousEnemy, () => sel.Previous(TargetClass.Enemy));
        DispatchKey(6, InputAction.TargetPreviousAlly, () => sel.Previous(TargetClass.Ally));
        DispatchKey(7, InputAction.TargetPreviousNonAircraft, () => sel.Previous(TargetClass.NonAircraft));
        DispatchKey(8, InputAction.TargetNearestEnemy, () => sel.Nearest(TargetClass.Enemy));
        DispatchKey(9, InputAction.TargetNearestAlly, () => sel.Nearest(TargetClass.Ally));
        DispatchKey(10, InputAction.TargetNearestNonAircraft, () => sel.Nearest(TargetClass.NonAircraft));

        // The spyglass toggle, both halves on their own slots. Its pad control is a button of its
        // own rather than the splitter, so a pad-only pilot reaches it without a double dispatch.
        if (Pressed(11, _keys.Held(InputAction.ToggleSpyglass)))
            ToggleSpyglass(frame.Hud, frame.PlayerIndex);
        if (Pressed(12, _pads.Held(InputAction.ToggleSpyglass)))
            ToggleSpyglass(frame.Hud, frame.PlayerIndex);
    }

    // The pilot's spyglass arm/disarm. Every other gate (off screen, the range band) is re-answered
    // every frame regardless, so this flips one flag and logs the transition.
    private static void ToggleSpyglass(TargetHud? hud, int playerIndex)
    {
        if (hud == null)
        {
            return;
        }

        hud.SpyglassOn = !hud.SpyglassOn;
        Log.Info("flight",
            $"targeting hud: P{playerIndex + 1} spyglass {(hud.SpyglassOn ? "on" : "off")}");
    }

    // Spends --target='s one application, waiting for a non-empty pool first. What it can name
    // (AI spawns, the zeppelins, a generator's first drop) is built after the rigs. `none` needs no
    // pool and does not wait.
    private void ApplyInitialTarget(TargetSelection sel, in TargetingFrame frame)
    {
        bool needsPool = !string.Equals(InitialTarget, "none", StringComparison.OrdinalIgnoreCase);
        if (needsPool && sel.Pool.Count == 0 && ++_initialWaits < InitialTargetGrace)
        {
            return;
        }

        _initialDone = true;      // spent whether or not it matched: one chance, then hands off
        if (sel.ApplyInitial(InitialTarget!, frame.Position, frame.Attitude))
        {
            string picked = sel.Current is { } t && t.Name.Length > 0 ? t.Name : "nothing";
            Log.Info("flight", $"--target={InitialTarget}: {picked} (class={sel.ActiveClass?.ToString() ?? "cleared"})");
            return;
        }

        // Naming what IS selectable is the whole diagnosis for a mistyped node name, and it is why
        // the flag needs no separate listing mode.
        var names = new List<string>();
        foreach (var cls in new[] { TargetClass.Enemy, TargetClass.Ally, TargetClass.NonAircraft })
        {
            foreach (var t in sel.Pool.Of(cls))
            {
                if (t.Name.Length > 0 && !names.Contains(t.Name))
                {
                    names.Add(t.Name);
                }
            }
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        int extra = names.Count - MaxNamed;
        if (extra > 0)
        {
            names.RemoveRange(MaxNamed, extra);
        }

        string listed = names.Count == 0 ? "(nothing)"
            : string.Join(", ", names) + (extra > 0 ? $", +{extra} more" : "");
        Log.Warn("core", $"--target={InitialTarget}: no match, selectable now: {listed}");
    }

    // Edge-detects one targeting key against its own slot and runs its action once per press.
    private void DispatchKey(int slot, InputAction action, Action act)
    {
        if (Pressed(slot, RowDown(action)))
        {
            act();
        }
    }

    // The edge rule for a control the caller reads, so one action can take a slot per device half.
    private bool Pressed(int slot, bool down)
    {
        bool pressed = down && !_keyPrev[slot];
        _keyPrev[slot] = down;
        return pressed;
    }
}
