using System;
using System.Collections.Generic;
using System.Globalization;

namespace CSVM.Mech3.Anim;

/// <summary>
/// Which machine answers a <c>PLAYER_RANGE</c> gate. A gate is a mission decision when its
/// definition, or one it starts through <c>CALL_ANIMATION</c>, raises a <c>CALLBACK</c>. In network
/// co-op the host answers such a gate over its whole human field and a guest replays the verdict.
/// Every other gate, the ordnance washes among them, stays each machine's own read, since each
/// machine draws its own viewer's effects. Engine-free: <c>AnimRuntime</c> asks it from
/// its condition arm, and <c>Session/Campaign/NetPositionalStartLink.cs</c> carries the verdicts.
/// Shipped gates and wire kind: docs/org/multiplayer-messages.md, "Positional starts".
/// </summary>
public sealed class RangeGateAuthority
{
    /// <summary>Set on a deciding end: raised with a host-decided gate's name and its new verdict
    /// each time that verdict changes. A gate never passed has told nobody anything.</summary>
    public Action<string, bool>? Decided;

    /// <summary>Set on a network guest only: the host's verdict for a gate, by name. While it is
    /// set, a host-decided gate is never read against this machine's own field.</summary>
    public Func<string, bool>? HostVerdict;

    private readonly Dictionary<AnimDefinition, bool> _raisesCode = new();
    private readonly Dictionary<string, bool> _told = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _seen = new(StringComparer.OrdinalIgnoreCase);
    private Func<string, IReadOnlyList<AnimDefinition>> _byName = _ => Array.Empty<AnimDefinition>();

    /// <summary>A gate's name on every machine: the definition, its anchor and its radius in
    /// metres squared, so two gates of one definition stay apart.</summary>
    public static string GateName(AnimDefinition def, string anchorName, float radiusSq)
    {
        ArgumentNullException.ThrowIfNull(def);
        return string.Create(CultureInfo.InvariantCulture,
            $"{def.AnimName ?? def.Name}/{anchorName}/{MathF.Round(radiusSq)}");
    }

    /// <summary>Binds the program's name lookup, the one <c>CALL_ANIMATION</c> dispatch uses, and
    /// forgets every answer the last world gave.</summary>
    public void Bind(Func<string, IReadOnlyList<AnimDefinition>> byName)
    {
        _byName = byName ?? throw new ArgumentNullException(nameof(byName));
        _raisesCode.Clear();
        _told.Clear();
    }

    /// <summary>Does <paramref name="def"/>'s range gate belong to the host? True when a
    /// <c>CALLBACK</c> is reachable from its sequences through <c>CALL_ANIMATION</c>.</summary>
    public bool HostDecides(AnimDefinition def)
    {
        ArgumentNullException.ThrowIfNull(def);
        if (!_raisesCode.TryGetValue(def, out bool raises))
        {
            raises = RaisesCode(def);
            _raisesCode[def] = raises;
        }

        return raises;
    }

    /// <summary>Records this end's own verdict on a host-decided gate and answers it unchanged,
    /// raising <see cref="Decided"/> when it differs from the last one.</summary>
    public bool Decide(string gate, bool passed)
    {
        ArgumentNullException.ThrowIfNull(gate);
        _told.TryGetValue(gate, out bool last);
        if (passed != last)
        {
            _told[gate] = passed;
            Decided?.Invoke(gate, passed);
        }

        return passed;
    }

    // Reset states are left out: a RESET_STATE raises no CALLBACK, and it starts nothing.
    private bool RaisesCode(AnimDefinition root)
    {
        _seen.Clear();
        if (root.AnimName != null)
        {
            _seen.Add(root.AnimName);
        }

        var defs = new List<AnimDefinition> { root };
        for (int i = 0; i < defs.Count; i++)
        {
            foreach (var seq in defs[i].Sequences)
            {
                foreach (var ev in seq.Events)
                {
                    if (ev.Kind == "Callback")
                    {
                        return true;
                    }

                    if (ev.Kind == "CallAnimation" && ev.Data.Str("name") is { } callee && _seen.Add(callee))
                    {
                        defs.AddRange(_byName(callee));
                    }
                }
            }
        }

        return false;
    }
}
