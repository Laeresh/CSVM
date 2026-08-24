using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The engine's per-mission world setup script: <c>support\&lt;chapter&gt;\&lt;mission&gt;.gw</c>
/// from the interp extraction, one per mission (53 in this install). Decides which world
/// entities a mission shows: a chapter's gamez holds every mission's content, and this switches
/// off what the current mission does not want. Full decode: <c>docs/formats/interp.md</c>.
/// ⚠ Entities are present by default and switched off here, the same polarity as <c>zepstate</c>;
/// <c>aiv.zrd.json</c> and <c>zeppelins.zrd.json</c> are not spawn rosters and gate nothing.
/// Runs as bootstrap pass 0, before the animation passes, so an animation state can override a
/// script state.
/// </summary>
public sealed class MissionSetup
{
    private readonly List<Op> _ops = new();
    private readonly Dictionary<string, int> _unapplied = new(StringComparer.Ordinal);
    private readonly List<string> _unresolved = new();
    private readonly List<string> _scrollUnresolved = new();
    private readonly Dictionary<int, IReadOnlyList<int>> _areaTargets = new();
    private int _activated, _deactivated, _scrollOps, _translated, _rotated;
    private int _areaOps, _areaOn, _areaOff;
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

    /// <summary>The script's <c>Object3DSetScroll</c> statements, resolved to gamez model index →
    /// UV scroll rate. Handed to the world build rather than applied in <see cref="Apply"/>,
    /// because the rate must be known while the material is created. Decode:
    /// <c>docs/formats/interp.md</c>.
    /// ⚠ Only the plain <c>FindNode</c> form resolves; a modelless group selection matches
    /// nothing, which this install never observes.</summary>
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

    /// <summary>Resolves the script's <c>WorldPartitionSetActive</c> rectangles against the world's
    /// partition grid, keeping the gamez node indices each one selects. Called with the gamez in
    /// hand, because <see cref="Apply"/> runs inside the animation bootstrap, which has none.
    /// Without it the verb is counted unapplied, exactly as it was before it was consumed.</summary>
    public void BindPartitions(GameZ gamez)
    {
        if (WorldPartitionGrid.Of(gamez.FindByName("world1")) is not { } grid)
        {
            return;
        }

        for (int i = 0; i < _ops.Count; i++)
        {
            if (_ops[i].Verb != "WorldPartitionSetActive" || ParseRect(_ops[i].Args) is not { } rect)
            {
                continue;
            }

            _areaTargets[i] = grid.NodesIn(rect.X1, rect.Z1, rect.X2, rect.Z2);
        }
    }

    /// <summary>Applies the script to a built world. <paramref name="resolve"/> maps a gamez name
    /// (plus an optional <c>FindSubNode</c> scope) to built nodes; <paramref name="setActive"/>,
    /// <paramref name="translate"/> and <paramref name="rotate"/> act on the selection. All four
    /// come from <see cref="AnimRuntime"/>, reusing its one name resolver.
    /// <paramref name="setActiveByIndex"/> switches a gamez node index, which is how the area verb
    /// reaches its selection; null leaves that verb counted and unapplied.</summary>
    public void Apply(
        Func<string, Node3D?, IReadOnlyList<Node3D>> resolve,
        Action<Node3D, bool> setActive,
        Action<Node3D, Vector3> translate,
        Action<Node3D, Vector3> rotate,
        Action<int, bool>? setActiveByIndex = null)
    {
        for (int i = 0; i < _ops.Count; i++)
        {
            var op = _ops[i];
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
                // The same toggle NodeSetActive performs, reached by AREA instead of by name, and
                // ignoring the selection entirely. All 25 uses switch C3's three story areas.
                case "WorldPartitionSetActive":
                    SetAreaActive(i, op, setActiveByIndex);
                    break;
                default:
                    // Everything else is parsed, counted and reported rather than guessed at —
                    // see the class docs and docs/formats/interp.md for what is left and why.
                    _unapplied[op.Verb] = _unapplied.GetValueOrDefault(op.Verb) + 1;
                    break;
            }
        }

        void SetAreaActive(int index, Op op, Action<int, bool>? byIndex)
        {
            if (byIndex == null || !_areaTargets.TryGetValue(index, out var targets))
            {
                _unapplied[op.Verb] = _unapplied.GetValueOrDefault(op.Verb) + 1;
                return;
            }

            bool active = op.Args.Length > 0 && op.Args[0] == "on";
            foreach (int idx in targets)
                byIndex(idx, active);
            _areaOps++;
            if (active)
                _areaOn += targets.Count;
            else
                _areaOff += targets.Count;
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
                // Expected, not a warning: the name may be absent from this chapter's gamez, or
                // present but never built (docs/formats/interp.md).
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
        if (_areaOps > 0)
            s += $", {_areaOps} area toggle(s): {_areaOff} node(s) off, {_areaOn} on";
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

    // `on|off <x1> <z1> <x2> <z2>` — the rectangle in world XZ, corners in either order.
    private static (float X1, float Z1, float X2, float Z2)? ParseRect(string[] args)
    {
        if (args.Length < 5
            || !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x1)
            || !float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z1)
            || !float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float x2)
            || !float.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float z2))
            return null;
        return (x1, z1, x2, z2);
    }

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

    // Per-script by magnitude: any component beyond 2pi marks the whole script as degrees.
    // Docs: docs/formats/interp.md ("Object3DRotate's angle unit is ambiguous").
    // ⚠ OBJECT_3D_ROTATE elsewhere has the same ambiguity; resolve it the same way there.
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
