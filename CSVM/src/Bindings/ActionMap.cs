using System.Collections.Generic;

namespace CSVM.Bindings;

/// <summary>One player's whole keymap: which controls fire which named action. A player owns a map,
/// so two players' maps are independent and the same control means whatever each of them says it
/// means. Resolution goes through <see cref="ResolveInto"/> once per tick rather than per read, so
/// two consumers asking the same question in one tick cannot disagree.
/// ⚠ A control belongs to at most one action. <see cref="Assign"/> takes it off its previous owner
/// and reports which action lost it, which is the original's conflict rule (`FUN_005371d0`,
/// `FUN_00535fb0`, `docs/org/input.md`) rather than a warning a screen may ignore. This type holds
/// no defaults; the shipped set and its file are the persistence item's.</summary>
public sealed class ActionMap
{
    private readonly Dictionary<InputAction, BindingSet> _sets = new();

    /// <summary>The actions that currently hold at least one binding, in no particular order. An
    /// action absent here is unbound and resolves to nothing.</summary>
    public IEnumerable<InputAction> BoundActions => _sets.Keys;

    /// <summary>Whether two bindings name the same physical control, ignoring an axis deadzone.
    /// Deadzone is a tuning number rather than part of the control's identity, so rebinding an axis
    /// that is already bound to the action adjusts it instead of stacking a second copy.</summary>
    public static bool SameControl(Binding left, Binding right)
    {
        if (left.Device != right.Device || left.Control.Kind != right.Control.Kind
            || left.Control.Index != right.Control.Index)
            return false;
        return left.Control.Kind switch
        {
            ControlKind.Axis => left.Control.Sign == right.Control.Sign,
            ControlKind.Hat => left.Control.Direction == right.Control.Direction,
            _ => true,
        };
    }

    /// <summary>Gives a control to an action, taking it off every action that held it, and returns
    /// those in enum order so a screen can name each loss rather than performing it silently.
    /// ⚠ Every owner, not the first. <see cref="Add"/> deliberately puts one control on two actions
    /// (a numpad snap-look diagonal, d-pad up in flight), and a steal stopping at the first owner
    /// would leave it on the other, which is the state the original forbids (`FUN_005371d0`,
    /// `FUN_00535fb0`).</summary>
    public IReadOnlyList<InputAction> Assign(InputAction action, Binding binding)
    {
        var stolenFrom = new List<InputAction>();
        foreach (var pair in _sets)
        {
            if (pair.Key != action && RemoveMatching(pair.Value, binding))
                stolenFrom.Add(pair.Key);
        }

        stolenFrom.Sort();
        var set = SetFor(action);
        RemoveMatching(set, binding);
        set.Add(binding);
        return stolenFrom;
    }

    /// <summary>Gives a control to an action without taking it off anyone. The shipped defaults and
    /// a loaded file both need this: a numpad snap-look diagonal is deliberately on two actions at
    /// once, and <see cref="Assign"/> would undo the second one.
    /// ⚠ Not for a rebinding screen. A control a player assigns goes through <see cref="Assign"/>,
    /// which is the only path that keeps the steal rule.</summary>
    public bool Add(InputAction action, Binding binding) => SetFor(action).Add(binding);

    /// <summary>Drops one control from one action, leaving every other action alone. This is the
    /// unbind a screen performs; it is not part of the steal rule.</summary>
    public bool Unassign(InputAction action, Binding binding) =>
        _sets.TryGetValue(action, out var set) && RemoveMatching(set, binding);

    public void Clear(InputAction action) => _sets.Remove(action);

    public void Clear() => _sets.Clear();

    /// <summary>The controls bound to that action, in the order they were added, which is the order
    /// a screen lists them. Empty for an unbound action.</summary>
    public IReadOnlyList<Binding> Bindings(InputAction action) =>
        _sets.TryGetValue(action, out var set) ? set.Bindings : System.Array.Empty<Binding>();

    /// <summary>Every action that currently holds that control, in enum order. A screen calls this
    /// before assigning so it can name the losers ahead of committing the steal.
    /// ⚠ A list rather than one action, and there is no single-owner form on purpose: two actions
    /// deliberately share several shipped controls, and a caller that took the first owner would
    /// report one loss and perform two.</summary>
    public IReadOnlyList<InputAction> OwnersOf(Binding binding)
    {
        var owners = new List<InputAction>();
        foreach (var pair in _sets)
        {
            foreach (var held in pair.Value.Bindings)
            {
                if (SameControl(held, binding))
                {
                    owners.Add(pair.Key);
                    break;
                }
            }
        }

        owners.Sort();
        return owners;
    }

    /// <summary>Replaces this map's contents with <paramref name="source"/>'s, in place, so every
    /// reader already holding this object reads the new keymap without being rebuilt. A polling site
    /// hands the same map to two or three <see cref="PlayerActions"/>, and swapping the reference
    /// would leave those readers on the map the seat was constructed with.
    /// ⚠ <see cref="Add"/> rather than <see cref="Assign"/>: the shipped set deliberately puts one
    /// control on two actions, and a steal on the way in would silently undo the second.</summary>
    public void Fill(ActionMap source)
    {
        System.ArgumentNullException.ThrowIfNull(source);
        if (ReferenceEquals(source, this))
            return;
        Clear();
        foreach (var pair in source._sets)
        {
            foreach (var binding in pair.Value.Bindings)
                Add(pair.Key, binding);
        }
    }

    /// <summary>An independent copy, for a screen whose edits may be cancelled.</summary>
    public ActionMap Clone()
    {
        var copy = new ActionMap();
        foreach (var pair in _sets)
            copy._sets[pair.Key] = pair.Value.Clone();
        return copy;
    }

    /// <summary>Reads every bound action out of this tick's hardware into the snapshot, reusing it
    /// rather than allocating one per tick. Unbound actions read as nothing.</summary>
    public void ResolveInto(ActionSnapshot snapshot, IDeviceState state)
    {
        snapshot.Reset();
        foreach (var pair in _sets)
            snapshot.Store(pair.Key, pair.Value.Resolve(state));
    }

    /// <summary>A fresh snapshot of this tick, for a caller that keeps no snapshot of its own.
    /// </summary>
    public ActionSnapshot Resolve(IDeviceState state)
    {
        var snapshot = new ActionSnapshot();
        ResolveInto(snapshot, state);
        return snapshot;
    }

    private static bool RemoveMatching(BindingSet set, Binding binding)
    {
        bool removed = false;
        for (int i = set.Bindings.Count - 1; i >= 0; i--)
        {
            var held = set.Bindings[i];
            if (SameControl(held, binding))
                removed |= set.Remove(held);
        }

        return removed;
    }

    private BindingSet SetFor(InputAction action)
    {
        if (!_sets.TryGetValue(action, out var set))
        {
            set = new BindingSet();
            _sets[action] = set;
        }

        return set;
    }
}
