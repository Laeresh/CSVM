using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace CSVM.Mech3;

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
    private readonly List<Op> _ops = new();
    private readonly Dictionary<string, int> _unapplied = new(StringComparer.Ordinal);
    private readonly List<string> _unresolved = new();
    private readonly List<string> _scrollUnresolved = new();
    private int _activated, _deactivated, _scrollOps, _translated, _rotated;
    private bool? _rotateDegrees;

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

    /// <summary>
    /// The script's <c>Object3DSetScroll</c> statements, resolved to gamez <b>model</b> indices →
    /// UV scroll rate in units/second. Handed to the world build (see
    /// <see cref="SceneBuilder"/>'s <c>scrollOverrides</c>) rather than applied in
    /// <see cref="Apply"/>, because a scroll rate has to be known while the material is created:
    /// a scrolling model can share its material with static geometry, so the rate is part of the
    /// material cache key.
    ///
    /// <para>Per <b>model</b>, not per node, because that is where the engine keeps it: the same
    /// rates the chapter-level <c>tex_fx.gw</c> sets are already baked into the shipped gamez
    /// models' own <c>texture_scroll</c> field (C1's <c>h_zone1scroll</c> 0.07, C1B's wakefronts
    /// 0.7/1.0, its <c>con_scroll</c> −1.0 — all identical in both places), while the per-mission
    /// ones are not, which is exactly what a verb that writes the model's field would produce.
    /// Every scroll target in this install is a model used by exactly one node, so the two
    /// granularities cannot disagree here.</para>
    ///
    /// <para>Only the plain <c>FindNode</c> form is resolved. A modelless group selection
    /// (C4's <c>tex_fx.gw</c> names <c>waterfall01</c>, which has no model of its own) matches
    /// nothing and is counted — unobservable either way, since every C4 mission script then sets
    /// that waterfall's two leaves directly.</para>
    /// </summary>
    public IReadOnlyDictionary<int, Vector2> ScrollByModel(GameZ gamez)
    {
        var map = new Dictionary<int, Vector2>();
        foreach (var op in _ops)
        {
            if (op.Verb != "Object3DSetScroll" || op.Target == null)
                continue;
            _scrollOps++;
            // `on|off <u> <v>`; off means "this model does not scroll", which still needs an
            // entry so it overrides the model's own field. (All 75 uses in this install are on.)
            var rate = Vector2.Zero;
            if (op.Args.Length >= 3 && op.Args[0] == "on"
                && float.TryParse(op.Args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float u)
                && float.TryParse(op.Args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                rate = new Vector2(u, v);
            int hits = 0;
            if (op.Sub == null)
                foreach (var node in gamez.Nodes)
                {
                    if (node.MeshIndex < 0 || !NameMatches(node.Name, op.Target))
                        continue;
                    map[node.MeshIndex] = rate; // order matters, last write wins
                    hits++;
                }
            if (hits == 0)
                _scrollUnresolved.Add(op.Sub == null ? op.Target : $"{op.Target}/{op.Sub}");
        }
        return map;
    }

    /// <summary>
    /// Applies the script to a built world. <paramref name="resolve"/> maps a gamez name (plus an
    /// optional scope node for the FindSubNode form) to the built nodes; <paramref name="setActive"/>
    /// switches a subtree on or off; <paramref name="translate"/> sets the selection's position
    /// (absolute, parent frame — the same convention <c>OBJECT_TRANSLATE_STATE</c> uses); <paramref
    /// name="rotate"/> sets its orientation from a radians Euler triple, already unit-converted (see
    /// <see cref="RotateAsRadians"/>). All four are supplied by <see cref="AnimRuntime"/> so this
    /// reuses the one proven name resolver rather than growing a second one.
    /// </summary>
    public void Apply(
        Func<string, Node3D?, IReadOnlyList<Node3D>> resolve,
        Action<Node3D, bool> setActive,
        Action<Node3D, Vector3> translate,
        Action<Node3D, Vector3> rotate)
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
                // Places the selection (14 uses: C1/M05's boats/redcross/workersvoyagezep,
                // C3/MP1-2's cargozep1, C5/MP1's rearm_node_2) — see docs/formats/interp.md,
                // "Vehicles load unplaced".
                case "Object3DTranslate":
                    if (ParseVector3(op.Args) is { } pos)
                        foreach (var t in ResolveTargets(op))
                        {
                            translate(t, pos);
                            _translated++;
                        }
                    break;
                // Re-orients the selection (11 uses). The data's angle unit is ambiguous and
                // decided per script — see RotateAsRadians and docs/formats/interp.md.
                case "Object3DRotate":
                    if (ParseVector3(op.Args) is { } euler)
                    {
                        var radians = RotateAsRadians(euler);
                        foreach (var t in ResolveTargets(op))
                        {
                            rotate(t, radians);
                            _rotated++;
                        }
                    }
                    break;
                // Acted on, but at world-build time rather than here — the rate is part of the
                // material cache key, so it has to be known before the material exists. See
                // ScrollByModel.
                case "Object3DSetScroll":
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
            var targets = ResolveTargets(op);
            if (targets.Count == 0)
                return;
            foreach (var t in targets)
                setActive(t, active);
            if (active)
                _activated += targets.Count;
            else
                _deactivated += targets.Count;
        }

        List<Node3D> ResolveTargets(Op op)
        {
            if (op.Target == null)
                return new List<Node3D>();
            var hosts = resolve(op.Target, null);
            if (hosts.Count == 0)
            {
                // A FindNode that matches nothing is expected and never a warning. Two causes,
                // both benign: the shipped script names a node this chapter's gamez does not
                // have (C3's blackhatzep/blackswanzep), or the node exists but WorldBuilder
                // never built it because it is a parentless root no partition references
                // (C2B's limo, C3's britbalmoral_1..3 and cpilot_shadow — the same pool the
                // effect templates live in). Either way there is nothing here to act on.
                _unresolved.Add(op.Sub == null ? op.Target : $"{op.Target}/{op.Sub}");
                return new List<Node3D>();
            }
            var targets = op.Sub == null
                ? hosts.ToList()
                : hosts.SelectMany(h => resolve(op.Sub, h)).ToList();
            if (targets.Count == 0)
                _unresolved.Add($"{op.Target}/{op.Sub}");
            return targets;
        }
    }

    /// <summary>One-line summary of what the script did, for the build log.</summary>
    public string Report()
    {
        var s = $"mission setup: {ScriptName} — {_deactivated} node(s) deactivated";
        if (_activated > 0)
            s += $", {_activated} activated";
        if (_translated > 0)
            s += $", {_translated} translated";
        if (_rotated > 0)
            s += $", {_rotated} rotated ({(_rotateDegrees == true ? "deg" : "rad")})";
        if (_scrollOps > 0)
            s += $", {_scrollOps} texture scroll(s) set at build"
                 + (_scrollUnresolved.Count > 0
                     ? $" ({_scrollUnresolved.Count} matched no model: {string.Join(", ", _scrollUnresolved.Distinct())})"
                     : "");
        if (_unresolved.Count > 0)
            s += $"; {_unresolved.Count} name(s) not in the built world ("
                 + string.Join(", ", _unresolved.Distinct().Take(6))
                 + (_unresolved.Distinct().Count() > 6 ? ", …" : "") + ")";
        if (_unapplied.Count > 0)
            s += "; not acted on: "
                 + string.Join(", ", _unapplied.OrderByDescending(k => k.Value).Select(k => $"{k.Key}×{k.Value}"));
        return s;
    }

    // A script name matches a gamez node name case-insensitively, with the model-file suffix
    // optional on either side — the same rule AnimRuntime's node lookup uses ('ap_radiotwr' for
    // the node 'ap_radiotwr.flt'), and interp.md records the scripts using both spellings.
    private static bool NameMatches(string nodeName, string scriptName) =>
        nodeName.Equals(scriptName, StringComparison.OrdinalIgnoreCase)
        || Strip(nodeName).Equals(Strip(scriptName), StringComparison.OrdinalIgnoreCase);

    private static Vector3? ParseVector3(string[] args)
    {
        if (args.Length < 3
            || !float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
            || !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
            || !float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            return null;
        return new Vector3(x, y, z);
    }

    private static string Strip(string s) =>
        s.EndsWith(".flt", StringComparison.OrdinalIgnoreCase) ? s[..^4] : s;

    // BL-249: the shipped data does not use one unit consistently — C1/M05's boat rotations are
    // small integers that only make sense as degrees (0 45 0, 0 172 0), C3/MP1-2's cargozep1 is a
    // high-precision radians triple (-0.000010 -3.144009 -0.000000, i.e. pi on Y) — and the two
    // families are cleanly separable by magnitude, so the decision is made once per script rather
    // than guessed globally (docs/formats/interp.md's "Object3DRotate's angle unit is ambiguous").
    // Cross-ref BL-034: the same question over OBJECT_3D_ROTATE data must not be resolved
    // differently there.
    private Vector3 RotateAsRadians(Vector3 euler)
    {
        _rotateDegrees ??= _ops.Any(o => o.Verb == "Object3DRotate"
            && o.Args.Any(a => float.TryParse(a, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
                                && MathF.Abs(v) > MathF.Tau));
        return _rotateDegrees.Value
            ? new Vector3(Mathf.DegToRad(euler.X), Mathf.DegToRad(euler.Y), Mathf.DegToRad(euler.Z))
            : euler;
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

    /// <summary>One parsed statement. <see cref="Target"/> is the <c>FindNode</c> selection in
    /// force, <see cref="Sub"/> the <c>FindSubNode</c> narrowing under it (null when none).</summary>
    public readonly record struct Op(string Verb, string? Target, string? Sub, string[] Args);
}
