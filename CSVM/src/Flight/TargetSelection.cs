using System.Collections.Generic;
using Godot;

namespace CSVM.Flight;

/// <summary>One pilot's target selection: the sticky choice, the three cycles, the nearest queries
/// and the lifecycle (decoded in docs/org/targeting.md). One instance
/// per pane; it OWNS its <see cref="TargetPool"/>, since the pool is per-selector state and nothing
/// else needs one.
///
/// <para>The shape follows the original's exactly: an action handler only mutates state (the class
/// flags and the selection identity) and steps the list that ALREADY exists, while
/// <see cref="Resolve"/> is the per-frame pass that re-sorts and re-finds. Nothing here is
/// re-picked per frame beyond that re-find, which is what makes the selection sticky, and nothing
/// but death, an explicit clear and the pilot's own input ever changes it: there is no range,
/// line-of-sight or field-of-view gate anywhere in the decoded path, and inventing one would be a
/// port invention.</para>
///
/// <para>No Godot node dependency: <see cref="Resolve"/> takes the pose it needs. The suite drives
/// the whole lifecycle with no tree.</para></summary>
public sealed class TargetSelection
{
    /// <summary>The nearest-crosshairs cone, <c>cos(15°)</c> (<c>FUN_00488ce0</c>'s
    /// <c>0.9659258</c>): a hard 15° half-angle about the NOSE.</summary>
    public const float CrosshairConeCos = 0.9659258f;

    /// <summary>Nearest-crosshairs' hard maximum range, metres — the running best starts at 2000.0,
    /// so nothing past it can ever win.</summary>
    public const float CrosshairMaxRange = 2000f;

    /// <summary>Rebuilds to wait before reporting an empty pool in the count breadcrumb — a few
    /// seconds at 60 Hz, long enough for the AI spawner, the zeppelins and a generator's first drop
    /// to have happened.</summary>
    private const int EmptyPoolReport = 300;

    private readonly List<TargetRef> _ordered = new();  // the active cycle, in cycle order
    private readonly List<object> _attackers = new();   // the queue 0x24 walks backwards
    private object? _selected;                          // the SOURCE object, never a TargetRef
    private bool _countsLogged;                         // verification breadcrumb: the cycle sizes log once
    private int _emptyRebuilds;                         // rebuilds seen with an empty pool, for that breadcrumb

    /// <summary>The pool this selector cycles over, rebuilt by <see cref="Rebuild"/>.</summary>
    public TargetPool Pool { get; } = new();

    /// <summary>Which cycle Next/Previous currently step, or <b>null for cleared</b>. Null is not
    /// "no target yet" — it is `Target Nothing`, and it is why the clear STAYS cleared: with every
    /// class flag zero the original skips the collection pass entirely, so the auto-acquire cannot
    /// fire again until a class action is pressed. Starts at <see cref="TargetClass.Enemy"/>, which
    /// is the mission-start state (<c>0x00474a6b</c>) and therefore the auto-acquire.</summary>
    public TargetClass? ActiveClass { get; private set; } = TargetClass.Enemy;

    /// <summary>The selected target as resolved by the last <see cref="Resolve"/>, or null. A plain
    /// readable property on purpose (decision 13): Track Target's camera and any later AI-order
    /// consumer read this and nothing else.</summary>
    public TargetRef? Current { get; private set; }

    /// <summary>The active cycle in cycle order, as the last <see cref="Resolve"/> sorted it. What
    /// the HUD would page through and what the suite asserts an order against.</summary>
    public IReadOnlyList<TargetRef> Ordered => _ordered;

    /// <summary>The attacker queue, oldest first — see <see cref="NextEnemy"/>.</summary>
    public IReadOnlyList<object> Attackers => _attackers;

    /// <summary>The cycle's sort key for one candidate (<c>FUN_004bbd60</c>): −1 for an objective,
    /// otherwise the 90° sector the target sits in, measured against the plane's own basis.
    ///
    /// <para><paramref name="basis"/>'s X column is the engine's <c>row0</c> (the RIGHT axis) and its
    /// Z column is <c>row2</c> (the NEGATED forward axis) — Godot's own convention makes those the
    /// same two vectors, so no sign fixing is needed. The quadrant is taken after a π/4 rotation and
    /// then has its 0 and 3 swapped, which is what puts <b>ahead</b> first and <b>right</b>
    /// last.</para></summary>
    /// <returns>−1 objective, 0 ahead, 1 behind, 2 left, 3 right.</returns>
    public static int SectorKey(Vector3 toTarget, Basis basis, bool objective)
    {
        if (objective)
        {
            return -1;
        }

        float theta = Mathf.Atan2(toTarget.Dot(basis.Z), toTarget.Dot(basis.X)) + (Mathf.Pi / 4f);
        int q = (int)(Mathf.PosMod(theta, Mathf.Tau) * (2f / Mathf.Pi));
        return q == 0 ? 3 : q == 3 ? 0 : q;
    }

    /// <summary>Fills <see cref="Pool"/> and re-resolves, the whole per-frame pass in one call
    /// (<c>FUN_004b5fb0</c>). With the selection cleared the pool is left EMPTY rather than built and
    /// discarded, which is the original's own short-circuit and the mechanism behind the sticky
    /// clear.</summary>
    public void Rebuild(AimCandidateSet scan, IReadOnlyList<AimCandidate>? subParts, int ownTeam,
        object? self, Vector3 position, Basis basis)
    {
        if (ActiveClass == null)
        {
            Pool.Clear();
            Resolve(position, basis);
            return;
        }

        Pool.Rebuild(scan, subParts, ownTeam, self);
        Resolve(position, basis);
        // Verification breadcrumb, once per selector: WHICH cycles this session actually has
        // anything in. The gun assist prints the same shape for its own four lists
        // (FlightController.ApplyFireOutcome) and for the same reason — a pool that silently
        // collected nothing looks identical to one nobody built.
        //
        // It waits for the first NON-EMPTY pool rather than firing on frame one, because the things
        // that fill it (AI spawns, the zeppelins, a generator drop) are built after the rigs are:
        // a frame-one line would report zeroes in every session and say nothing. The empty case is
        // still reported, once, after EmptyPoolReport rebuilds, so "nothing was ever selectable" is
        // a line you can read rather than a line you have to notice is missing.
        if (!_countsLogged && (Pool.Count > 0 || ++_emptyRebuilds >= EmptyPoolReport))
        {
            _countsLogged = true;
            GD.Print($"target pool: enemy={Pool.Enemy.Count} ally={Pool.Ally.Count} " +
                     $"nonAircraft={Pool.NonAircraft.Count} class={ActiveClass} " +
                     $"acquired={(Current is { } t && t.Name.Length > 0 ? t.Name : "-")}");
        }
    }

    /// <summary>Re-sorts the active cycle against the plane's pose and re-finds the selection by
    /// ENTITY (<c>FUN_004b6490(current, 0)</c>): the matching entry, or the list HEAD when it is
    /// gone. That single rule is the whole lifecycle. A dead target, a target that left the class,
    /// and a class change all drop to the head of the current cycle — not to the dead entry's
    /// neighbour — because in every one of those cases the re-find simply fails.</summary>
    public void Resolve(Vector3 position, Basis basis)
    {
        _ordered.Clear();
        if (ActiveClass is not { } cls)
        {
            _selected = null;
            Current = null;
            return;
        }

        var live = Pool.Of(cls);
        for (int i = 0; i < live.Count; i++)
        {
            _ordered.Add(live[i]);
        }

        Sort(position, basis);
        int idx = IndexOfSelected();
        if (idx < 0 && _ordered.Count > 0)
        {
            idx = 0;    // the re-resolve's head fallback: this is the auto-acquire AND the death switch
        }

        Current = idx >= 0 ? _ordered[idx] : null;
        _selected = Current?.Source;
    }

    /// <summary>Next Enemy/Objective (<c>0x24</c>) — the one action that consults the attacker queue
    /// before the ordinary cycle, walking it BACKWARDS from the end so the most recent shooter comes
    /// first: not in the queue selects the last entry, in the queue selects the one before it, and
    /// the queue's first entry falls through to an ordinary <c>+1</c> step. An empty queue falls
    /// through too. The original plays its switch sound only on the fall-through.
    /// ⚠ CSVM ships every class action SILENT. <c>sg_switchtarget</c> is in the executable but in no
    /// shipped asset (not <c>sounds.zrd.json</c>, not the 2521 files under <c>soundsh/</c>); wiring
    /// <c>snd_select</c> to it would be an invention, not a port.</summary>
    public void NextEnemy()
    {
        if (ActiveClass != TargetClass.Enemy)
        {
            ActiveClass = TargetClass.Enemy;
            _selected = null;
            Step(1);
            return;
        }

        if (_attackers.Count > 0)
        {
            int idx = _selected == null ? -1 : _attackers.LastIndexOf(_selected);
            if (idx < 0)
            {
                _selected = _attackers[_attackers.Count - 1];
                return;
            }

            if (idx > 0)
            {
                _selected = _attackers[idx - 1];
                return;
            }
        }

        Step(1);
    }

    /// <summary>Next in <paramref name="cls"/> (<c>0x27</c>/<c>0x2a</c>, and <c>0x24</c>'s
    /// fall-through). Changing class clears the selection first, so the step lands on the head.</summary>
    public void Next(TargetClass cls) => StepIn(cls, 1);

    /// <summary>Previous in <paramref name="cls"/> (<c>0x25</c>/<c>0x28</c>/<c>0x2b</c>).</summary>
    public void Previous(TargetClass cls) => StepIn(cls, -1);

    /// <summary>Nearest in <paramref name="cls"/> (<c>0x26</c>/<c>0x29</c>/<c>0x2c</c>).
    ///
    /// <para>⚠ "Nearest" is the <b>head of the cycle</b>, not the nearest thing in space: the head is
    /// the nearest objective if any exists, otherwise the nearest candidate in the forward quadrant,
    /// otherwise behind, otherwise left, otherwise right. A target 200 m off the left wing loses to
    /// one 900 m ahead. Unlike Next/Previous it ALWAYS re-asserts its class and restarts the
    /// cycle.</para></summary>
    public void Nearest(TargetClass cls)
    {
        ActiveClass = cls;
        _selected = null;
        Step(0);
    }

    /// <summary>Select Target Nearest Crosshairs (<c>0x2d</c>, <c>FUN_00488db0</c>): its own scan
    /// over every class, ignoring the cycle entirely. Scores by plain slant range inside a 15°
    /// half-angle cone about the <b>NOSE</b> — not the gun pipper, which is a separate
    /// velocity-derived point nothing in this path reads — capped at
    /// <see cref="CrosshairMaxRange"/>. Friendlies are included, which is how one keypress reaches an
    /// ally. Having chosen, it writes the class back from what it found, so a following Next/Previous
    /// continues in that target's own cycle.</summary>
    /// <returns>False when nothing is in the cone, in which case the selection is left alone.</returns>
    public bool NearestCrosshairs(Vector3 position, Basis basis)
    {
        float best = CrosshairMaxRange;
        TargetRef? winner = null;
        foreach (var cls in new[] { TargetClass.Enemy, TargetClass.Ally, TargetClass.NonAircraft })
        {
            foreach (var t in Pool.Of(cls))
            {
                var v = t.Position - position;
                float d = v.Length();
                if (d <= 0f || v.Dot(basis.Z) > d * -CrosshairConeCos || d >= best)
                {
                    continue;
                }

                best = d;
                winner = t;
            }
        }

        if (winner is not { } pick)
        {
            return false;
        }

        ActiveClass = pick.Class;
        _selected = pick.Source;
        return true;
    }

    /// <summary>Target Nothing (<c>0x2e</c>): nulls the target AND every class flag. The second half
    /// is the point — it is what stops the auto-acquire firing again, so the clear STAYS cleared
    /// until a class action. Auto-acquire at start and the drop-to-head on death are the only two
    /// automatic transitions there are; a third ("nothing selected, so pick one") would silently
    /// break this action.</summary>
    public void Clear()
    {
        ActiveClass = null;
        _selected = null;
        Current = null;
        _ordered.Clear();
    }

    /// <summary>Point the selection at a named pool entry, matching <see cref="TargetRef.Name"/>
    /// case-insensitively across all three cycles and writing the class back from what it found
    /// (the same rule <see cref="NearestCrosshairs"/> uses, and the reason a sub-part pin is not
    /// dropped by the next <see cref="Resolve"/>). This is <c>--target=&lt;name&gt;</c>'s seam;
    /// nothing in the original has an equivalent, since the original has no scripted input at all.
    ///
    /// <para>Cycles are searched Enemy, Ally, Non-Aircraft, and within a cycle in the pool's own
    /// collector order — so a name carried by two entities (two zeppelins with identically named
    /// zones) resolves to the same one on every run, which is the whole point of the flag.</para></summary>
    /// <returns>False when no entry carries that name, in which case the selection is left alone.</returns>
    public bool Select(string name)
    {
        if (name.Length == 0)
        {
            return false;
        }

        foreach (var cls in new[] { TargetClass.Enemy, TargetClass.Ally, TargetClass.NonAircraft })
        {
            foreach (var t in Pool.Of(cls))
            {
                if (!string.Equals(t.Name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ActiveClass = t.Class;
                _selected = t.Source;
                return true;
            }
        }

        return false;
    }

    /// <summary>Apply one <c>--target=</c> spec: <c>nearest</c>, <c>crosshair</c>, <c>next</c>,
    /// <c>none</c>, or a target's name for <see cref="Select"/>. The four words run the ordinary
    /// actions rather than a scripted path of their own, so the flag can only reach a state the pilot
    /// could have reached by hand.
    ///
    /// <para>⚠ This sets the INITIAL selection and nothing more. It mutates the same two pieces of
    /// state a keypress does and then returns; the caller is responsible for calling it once, and
    /// from that point the selection cycles and re-resolves normally. A flag that held the selection
    /// against later input would freeze targeting in an interactive session started with it.</para>
    ///
    /// <para>⚠ <c>nearest</c> is the original's Nearest action, so it means the HEAD of the cycle,
    /// not the nearest thing in space — see <see cref="Nearest"/>.</para></summary>
    /// <returns>False when the spec selected nothing (an empty crosshair cone, an unknown name).</returns>
    public bool ApplyInitial(string spec, Vector3 position, Basis basis)
    {
        bool ok;
        switch (spec.ToLowerInvariant())
        {
            case "none":
                Clear();
                ok = true;
                break;
            case "nearest":
                Nearest(TargetClass.Enemy);
                ok = true;
                break;
            case "next":
                NextEnemy();
                ok = true;
                break;
            case "crosshair":
                ok = NearestCrosshairs(position, basis);
                break;
            default:
                ok = Select(spec);
                break;
        }

        // Re-resolve in the SAME frame rather than leaving the one-frame lag a keypress has: the
        // caller logs what was picked, and a --frames=N --screenshot run should not need one more
        // frame than it asked for to have the flag's target on screen.
        if (ok && ActiveClass != null)
        {
            Resolve(position, basis);
        }

        return ok;
    }

    /// <summary>Records a shooter that just hit this pilot, for <see cref="NextEnemy"/>'s queue
    /// (<c>FUN_004b9770</c>: an end insert, made only for a shooter on a different, non-zero team).
    /// The queue survives for the whole mission and is pruned only by death
    /// (<see cref="ForgetTarget"/>).
    ///
    /// <para>⚠ The de-duplication is an INFERENCE. The original calls <c>FUN_004bc1e0</c> on the
    /// wrapper immediately before the insert, which is probably a remove-if-present and would move a
    /// repeat attacker to the end rather than listing it twice; that was not traced. Deduping is the
    /// reading that makes the backwards walk useful, so it is what ships, marked as inference rather
    /// than as decode.</para></summary>
    public void RecordAttacker(object shooter)
    {
        _attackers.Remove(shooter);
        _attackers.Add(shooter);
    }

    /// <summary>The death/despawn hook (<c>FUN_004a64e0</c>): drops <paramref name="entity"/> from
    /// the attacker queue, and from the selection if it was the target. The next
    /// <see cref="Resolve"/> then picks the head of the current cycle, which is the original's
    /// switch-on-death. Calling this is optional for the switch itself — a dead candidate leaves the
    /// pool and the re-find fails anyway — but the queue prune is not.</summary>
    public void ForgetTarget(object entity)
    {
        _attackers.Remove(entity);
        if (ReferenceEquals(_selected, entity))
        {
            _selected = null;
            Current = null;
        }
    }

    private void StepIn(TargetClass cls, int dir)
    {
        if (ActiveClass != cls)
        {
            // The class handlers clear the target when the class actually changes, so the step lands
            // on a head rather than continuing from an entry in the cycle just left. ⚠ The list this
            // steps is still LAST frame's, built under the old class — that one-frame lag is the
            // original's (a synchronous rebuild inside the handler does not reproduce it), and it
            // self-heals on the next Resolve, which drops to the head of the new cycle.
            ActiveClass = cls;
            _selected = null;
        }

        Step(dir);
    }

    /// <summary><c>FUN_004b6490</c>: find the selection by entity, then step. Not found (or nothing
    /// selected) returns the FIRST entry whatever the direction; found wraps at both ends.</summary>
    private void Step(int dir)
    {
        if (_ordered.Count == 0)
        {
            _selected = null;
            return;
        }

        int idx = IndexOfSelected();
        if (idx < 0)
        {
            _selected = _ordered[0].Source;
            return;
        }

        if (dir == 0)
        {
            return;
        }

        idx = dir > 0 ? (idx + 1) % _ordered.Count
            : ((idx - 1) + _ordered.Count) % _ordered.Count;
        _selected = _ordered[idx].Source;
    }

    private int IndexOfSelected()
    {
        if (_selected == null)
        {
            return -1;
        }

        for (int i = 0; i < _ordered.Count; i++)
        {
            if (ReferenceEquals(_ordered[i].Source, _selected))
            {
                return i;
            }
        }

        return -1;
    }

    private void Sort(Vector3 position, Basis basis)
    {
        int n = _ordered.Count;
        if (n < 2)
        {
            return;
        }

        var keys = new (int Sector, float DistSq, int Index)[n];
        for (int i = 0; i < n; i++)
        {
            var v = _ordered[i].Position - position;
            keys[i] = (SectorKey(v, basis, _ordered[i].Objective), v.LengthSquared(), i);
        }

        var byKey = new TargetRef[n];
        for (int i = 0; i < n; i++)
        {
            byKey[i] = _ordered[i];
        }

        // Sector first, then nearest inside the sector. The third key is the pre-sort index: the
        // engine's own sort makes no promise about exact ties, and a total order here keeps a tie
        // (two parts of one zeppelin at the same range) from reordering between frames and walking
        // the cycle under the pilot.
        System.Array.Sort(keys, byKey, Comparer<(int Sector, float DistSq, int Index)>.Create(
            (x, y) => x.Sector != y.Sector ? x.Sector.CompareTo(y.Sector)
                : x.DistSq != y.DistSq ? x.DistSq.CompareTo(y.DistSq)
                : x.Index.CompareTo(y.Index)));
        _ordered.Clear();
        for (int i = 0; i < n; i++)
        {
            _ordered.Add(byKey[i]);
        }
    }
}
