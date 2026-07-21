using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace CrimsonSkies.Mech3;

/// <summary>
/// The engine's per-mission world setup script — <c>support\&lt;chapter&gt;\&lt;mission&gt;.gw</c>
/// inside the interp extraction, one per mission folder (53 in this install).
///
/// This is the mechanism that decides <b>which world entities a mission shows</b>, and it is a
/// different one from the animation program: a chapter's gamez holds every mission's content,
/// the engine loads all of it, and then this script switches off what this mission does not
/// want. C1/IA1's script deactivates 29 nodes — the Hollywood Knights zeppelin
/// <c>hk_zep</c>, both multiplayer zeppelins, the CTF gate posts and flags, the nine
/// <c>lifesaver*</c> props, the AA guns, the rearm bay. C1/M04's does not deactivate
/// <c>hk_zep</c>, which is exactly why it is on the field in that mission (user reference
/// screenshot) and nowhere else. The CTF props are switched off by every mission except
/// <c>mp2.gw</c>, the Capture the Flag map — the gate the user described, stated outright by
/// the data.
///
/// This corrects the hypothesis this work was scheduled on. <c>aiv.zrd.json</c> is <b>not</b> a
/// spawn roster: it is the AI vehicle table, and its only mention of <c>hk_zep</c> anywhere is
/// inside a wingman's target-priority list in C1/M02. <c>zeppelins.zrd.json</c> is the flyable-
/// zeppelin gameplay config, and it never names <c>hk_zep</c> in the one mission that shows it.
/// Neither could have gated anything. Entities are present by default and switched off here —
/// the same polarity as <c>zepstate</c>, not the mirror image of it. See
/// <c>docs/formats/interp.md</c>.
///
/// <para>Ordering: this runs as bootstrap pass 0, before the animation passes, which is the
/// engine's own load order (world → .gw → anims) and lets an animation state override a script
/// state rather than the other way round.</para>
/// </summary>
public sealed class MissionSetup
{
    /// <summary>One parsed statement. <see cref="Target"/> is the <c>FindNode</c> selection in
    /// force, <see cref="Sub"/> the <c>FindSubNode</c> narrowing under it (null when none).</summary>
    public readonly record struct Op(string Verb, string? Target, string? Sub, string[] Args);

    private readonly List<Op> _ops = new();
    private readonly Dictionary<string, int> _unapplied = new(StringComparer.Ordinal);
    private readonly List<string> _unresolved = new();
    private int _activated, _deactivated;

    /// <summary>The interp script this was read from, for logging.</summary>
    public string ScriptName { get; private init; } = "";

    /// <summary>Statements parsed (every verb, including the ones we do not act on).</summary>
    public IReadOnlyList<Op> Ops => _ops;

    /// <summary>
    /// Reads <c>support\&lt;chapter&gt;\&lt;mission&gt;.gw</c> out of the interp extraction.
    /// Null when the file or the script is missing — a mission with no setup script is normal
    /// (the engine simply leaves the world as loaded), so callers treat null as "nothing to do".
    /// </summary>
    public static MissionSetup? Load(string interpPath, string chapter, string mission)
    {
        if (!File.Exists(interpPath))
            return null;
        var wanted = $"support\\{chapter.ToLowerInvariant()}\\{mission.ToLowerInvariant()}.gw";
        using var doc = JsonDocument.Parse(File.ReadAllBytes(interpPath));
        foreach (var script in doc.RootElement.EnumerateArray())
        {
            if (!script.TryGetProperty("name", out var n)
                || !string.Equals(n.GetString(), wanted, StringComparison.OrdinalIgnoreCase))
                continue;
            var setup = new MissionSetup { ScriptName = wanted };
            setup.Parse(script.GetProperty("lines"));
            return setup;
        }
        return null;
    }

    // The script is a flat command list with a stateful selector: FindNode picks a node by
    // name, FindSubNode narrows to a descendant, and the following verb acts on that selection.
    // Quit ends the script (it is always the last line, in C4's and C5's scripts).
    private void Parse(JsonElement lines)
    {
        string? target = null, sub = null;
        foreach (var line in lines.EnumerateArray())
        {
            var parts = (line.GetString() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;
            switch (parts[0])
            {
                case "FindNode":
                    target = parts.Length > 1 ? parts[1] : null;
                    sub = null;
                    break;
                // FindSubNodeNode is the same selector spelled differently (2 uses, C3).
                case "FindSubNode":
                case "FindSubNodeNode":
                    sub = parts.Length > 1 ? parts[1] : null;
                    break;
                case "Quit":
                    return;
                // DeleteTree names its own target rather than using the selection.
                case "DeleteTree":
                    if (parts.Length > 1)
                        _ops.Add(new Op("DeleteTree", parts[1], null, Array.Empty<string>()));
                    break;
                default:
                    _ops.Add(new Op(parts[0], target, sub, parts[1..]));
                    break;
            }
        }
    }

    /// <summary>
    /// Applies the script to a built world. <paramref name="resolve"/> maps a gamez name (plus an
    /// optional scope node for the FindSubNode form) to the built nodes; <paramref name="setActive"/>
    /// switches a subtree on or off. Both are supplied by <see cref="AnimRuntime"/> so this reuses
    /// the one proven name resolver rather than growing a second one.
    /// </summary>
    public void Apply(Func<string, Node3D?, IReadOnlyList<Node3D>> resolve, Action<Node3D, bool> setActive)
    {
        foreach (var op in _ops)
        {
            switch (op.Verb)
            {
                // The payload: 1,125 of the 1,215 statements across all 53 scripts.
                case "NodeSetActive":
                    SetActive(op, op.Args.Length > 0 && op.Args[0] == "on");
                    break;
                // Removal rather than deactivation, but nothing in any script re-activates a
                // deleted name (verified over all 53), so switching the subtree off is
                // indistinguishable here and keeps one code path. 12 uses, all C3.
                case "DeleteTree":
                    SetActive(op, false);
                    break;
                default:
                    // Everything else is parsed, counted and reported rather than guessed at —
                    // see the class docs and docs/formats/interp.md for what is left and why.
                    _unapplied[op.Verb] = _unapplied.GetValueOrDefault(op.Verb) + 1;
                    break;
            }
        }

        void SetActive(Op op, bool active)
        {
            if (op.Target == null)
                return;
            var hosts = resolve(op.Target, null);
            if (hosts.Count == 0)
            {
                // A FindNode that matches nothing is expected and never a warning. Two causes,
                // both benign: the shipped script names a node this chapter's gamez does not
                // have (C3's blackhatzep/blackswanzep), or the node exists but WorldBuilder
                // never built it because it is a parentless root no partition references
                // (C2B's limo, C3's britbalmoral_1..3 and cpilot_shadow — the same pool the
                // effect templates live in). Either way there is nothing here to switch off.
                _unresolved.Add(op.Sub == null ? op.Target : $"{op.Target}/{op.Sub}");
                return;
            }
            var targets = op.Sub == null
                ? hosts
                : hosts.SelectMany(h => resolve(op.Sub, h)).ToList();
            if (targets.Count == 0)
            {
                _unresolved.Add($"{op.Target}/{op.Sub}");
                return;
            }
            foreach (var t in targets)
                setActive(t, active);
            if (active)
                _activated += targets.Count;
            else
                _deactivated += targets.Count;
        }
    }

    /// <summary>One-line summary of what the script did, for the build log.</summary>
    public string Report()
    {
        var s = $"mission setup: {ScriptName} — {_deactivated} node(s) deactivated";
        if (_activated > 0)
            s += $", {_activated} activated";
        if (_unresolved.Count > 0)
            s += $"; {_unresolved.Count} name(s) not in the built world ("
                 + string.Join(", ", _unresolved.Distinct().Take(6))
                 + (_unresolved.Distinct().Count() > 6 ? ", …" : "") + ")";
        if (_unapplied.Count > 0)
            s += "; not acted on: "
                 + string.Join(", ", _unapplied.OrderByDescending(k => k.Value).Select(k => $"{k.Key}×{k.Value}"));
        return s;
    }
}
