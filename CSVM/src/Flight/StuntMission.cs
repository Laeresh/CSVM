using System;
using System.Collections.Generic;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight;

/// <summary>One Danger Zone of a stunt run: the <c>dzN</c> marker point's world position, its
/// route ribbon name (<c>dzpathN</c>, debug-only geometry), the resolved display strings, and
/// whether the player has flown through it yet.</summary>
public sealed class StuntZone
{
    public string DzName = "";        // dz1..dzN — hand-placed marker/HUD anchor
    public string PathName = "";      // dzpath1..dzpathN — route ribbon + gate pair
    public Vector3 Position;           // world-space HUD marker position
    public StuntGate GreenGate = null!;
    public StuntGate RedGate = null!;
    public string Description = "";   // resolved, e.g. "Train Tunnel Mid"
    public string Category = "";      // resolved, e.g. "Danger Zone"
    public string Help = "";          // resolved action, e.g. "Fly Through"
    public bool Completed;

    /// <summary>Run clock, seconds, at the moment this zone was flown through — its cumulative
    /// time from run start. 0 until completed; the scoreboard's per-zone split is
    /// the delta between consecutive completions in <see cref="CompletionOrder"/>.</summary>
    public float CompletedAt;

    /// <summary>0-based order in which this zone was cleared (the run is order-free, so this is
    /// the flown order, not the ia.json list order). −1 until completed.</summary>
    public int CompletionOrder = -1;

    /// <summary>The original's assembled marker text without the clock suffix (the marker HUD
    /// adds that): "Danger Zone [Fly Through] - Train Tunnel Mid". Degrades gracefully if any
    /// part is absent.</summary>
    public string MarkerText()
    {
        var head = Category.Length > 0 && Help.Length > 0 ? $"{Category} [{Help}]"
            : Help.Length > 0 ? $"[{Help}]"
            : Category;
        return head.Length > 0 && Description.Length > 0 ? $"{head} - {Description}"
            : Description.Length > 0 ? Description
            : head.Length > 0 ? head
            : DzName;
    }
}

/// <summary>An authored Danger Zone aperture: a planar polygon ring in world coordinates.</summary>
public sealed class StuntGate
{
    public required Vector3[] Vertices { get; init; }
    public required Vector3 Center { get; init; }
    public required Vector3 Normal { get; init; }
}

/// <summary>
/// The Stunt Flying instant-action mode. A stunt run's objective is to
/// fly through every Danger Zone by crossing both authored apertures, order-free within and
/// between zones, and the run ends when all are done.
///
/// The zone list is the mission ia.json's <c>dzones</c> (<c>[dzpathN, dzN]</c> pairs); each
/// <c>dzN</c>'s world position comes straight from the chapter gamez (a point marker under the
/// identity World root), and its display strings from targets.json → messages.json. Detection
/// and completion only — the marker HUD and timed scoring build on this.
/// </summary>
public sealed class StuntMission
{
    /// <summary>Radius about the <c>dzN</c> marker centre (user-tuned). Not a scoring value —
    /// completion crosses the authored gate pair; retained for the marker's non-scoring
    /// consumers.</summary>
    public const float DzRadius = 15f;

    /// <summary>Prefix for this run's log lines ("P2 " in a splitscreen race). Empty in a
    /// solo run, so single-player logs read exactly as before — but with four pilots clearing zones
    /// in one shared world the completion lines are otherwise indistinguishable.</summary>
    public string LogTag = "";

    /// <summary>Messages key for the run-start intro line ("Fly through all the Danger Zones to
    /// win!") — the marker HUD's one-shot banner.</summary>
    private const string IntroMsgKey = "MSG_BRF_IASF_OBJ2";

    private readonly List<StuntZone> _zones;
    private readonly HashSet<StuntGate> _crossedGates = new();
    private Vector3 _lastPlanePos;
    private bool _haveLastPlanePos;

    private int _active = -1;

    private StuntMission(List<StuntZone> zones)
    {
        _zones = zones;
        AdvanceActive();
    }

    /// <summary>Fired once per zone the moment it is flown through (scoring records a split).</summary>
    public event Action<StuntZone>? ZoneCompleted;

    /// <summary>Fired once when the last zone completes (stops the clock / shows the board).</summary>
    public event Action? RunCompleted;

    public bool AllComplete { get; private set; }

    public int CompletedCount { get; private set; }

    public int TotalCount => _zones.Count;

    public IReadOnlyList<StuntZone> Zones => _zones;

    /// <summary>Elapsed run time, seconds, advanced by <see cref="Tick"/> every physics frame —
    /// including through the crash freeze ("the clock never stops"), frozen only once the run is
    /// complete. Read by the marker HUD and scoring. Not reset on respawn (a
    /// mid-run crash keeps the same clock, like the completed zones).</summary>
    public float Elapsed { get; private set; }

    /// <summary>The one-shot run-start line the marker HUD shows ("Fly through all the Danger
    /// Zones to win!"), resolved at load from the message table. Empty if the table is absent.</summary>
    public string IntroLine { get; private set; } = "";

    /// <summary>The zone the HUD points at: the first still-incomplete zone in list order
    /// (manual cycling overrides the displayed one). Null once the run is complete.</summary>
    public StuntZone? ActiveZone => _active >= 0 && _active < _zones.Count ? _zones[_active] : null;

    /// <summary>Builds the stunt run for a mission: reads its ia.json <c>dzones</c>, resolves each
    /// <c>dzN</c>'s world position from <paramref name="worldGamez"/> and its display strings from
    /// the mission's targets.json (keys resolved via <paramref name="messages"/>). Null if the
    /// mission has no dzones or none of them resolve to a world node (not a stunt mission).</summary>
    public static StuntMission? Load(GameZ worldGamez, string missionZrdrPath, Messages messages)
    {
        List<object?> root;
        try
        {
            root = Zrdr.LoadFile(missionZrdrPath, "ia.json");
        }
        catch (System.IO.IOException)
        {
            return null; // no ia.json (story/multiplayer folder) — no stunt zones
        }

        var dzones = ZrdrDict.FromAlternating(root).List("dzones");
        if (dzones == null || dzones.Count == 0)
            return null;

        var targets = MissionTargets.Load(missionZrdrPath);
        var zones = new List<StuntZone>();
        var unresolved = new List<string>();
        foreach (var pairObj in dzones)
        {
            // each entry is [dzpathN, dzN]; the dzN point is what we test against
            if (pairObj is not List<object?> pair || pair.Count < 2
                || pair[0] is not string pathName || pair[1] is not string dzName)
                continue;
            var node = worldGamez.FindByName(dzName);
            if (node == null)
            {
                unresolved.Add(dzName);
                continue;
            }
            if (!TryReadGates(worldGamez, pathName, out var green, out var red))
            {
                unresolved.Add(pathName);
                continue;
            }
            var t = targets.For(dzName);
            zones.Add(new StuntZone
            {
                DzName = dzName,
                PathName = pathName,
                Position = GeometryAnchor(worldGamez, node) ?? worldGamez.WorldTransformOf(node).Origin,
                GreenGate = green,
                RedGate = red,
                Description = messages.Get(t.Description),
                Category = messages.Get(t.CategoryLabel),
                Help = messages.Get(t.HelpLabel),
            });
        }

        if (unresolved.Count > 0)
            GD.PushWarning($"stunt: {unresolved.Count} danger-zone marker(s) not in the world gamez: "
                + string.Join(", ", unresolved));
        if (zones.Count == 0)
            return null;

        GD.Print($"stunt: {zones.Count} danger zone(s) loaded");
        foreach (var z in zones)
            GD.Print($"  {z.DzName}: {z.MarkerText()} @ ({z.Position.X:0},{z.Position.Y:0},{z.Position.Z:0})");
        return new StuntMission(zones) { IntroLine = messages.Get(IntroMsgKey) };
    }

    /// <summary>"m:ss.t" run-clock formatting, shared by the marker HUD status line and the
    /// scoreboard. Invariant culture so the decimal is always a period regardless of the
    /// player's system locale (a game clock, and deterministic across screenshot runs).</summary>
    public static string FormatTime(float seconds)
    {
        int min = (int)(seconds / 60f);
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0}:{1:00.0}", min, seconds - min * 60f);
    }

    /// <summary>A second, independent run over the same Danger Zones (splitscreen
    /// racing): same zone list, positions and display strings, but its own completion flags, clock,
    /// active target and events. Copying beats calling <see cref="Load"/> once per player — the
    /// ia.json/targets.json/messages parse and the gamez lookups happen once for the session.</summary>
    public StuntMission ForAnotherPlayer()
    {
        var zones = new List<StuntZone>(_zones.Count);
        foreach (var z in _zones)
            zones.Add(new StuntZone
            {
                DzName = z.DzName,
                PathName = z.PathName,
                Position = z.Position,
                GreenGate = z.GreenGate,
                RedGate = z.RedGate,
                Description = z.Description,
                Category = z.Category,
                Help = z.Help,
            });
        return new StuntMission(zones) { IntroLine = IntroLine };
    }

    /// <summary>Physics-frame test: record any gate the plane's movement segment crossed this
    /// frame, and complete a zone once both its gates have been crossed (order-free — several
    /// can complete in one pass).</summary>
    public void Update(Vector3 planePos)
    {
        if (AllComplete)
            return;
        if (!_haveLastPlanePos)
        {
            _lastPlanePos = planePos;
            _haveLastPlanePos = true;
            return;
        }
        foreach (var z in _zones)
        {
            if (z.Completed)
                continue;
            if (GateCrossing(_lastPlanePos, planePos, z.GreenGate, out _))
                _crossedGates.Add(z.GreenGate);
            if (GateCrossing(_lastPlanePos, planePos, z.RedGate, out _))
                _crossedGates.Add(z.RedGate);
            if (_crossedGates.Contains(z.GreenGate) && _crossedGates.Contains(z.RedGate))
                Complete(z);
        }
        _lastPlanePos = planePos;
    }

    /// <summary>Advance the run clock one physics frame. Called every frame — including through
    /// the crash freeze so the clock never stops (a deliberate rule) — and stops accumulating once the
    /// run is complete.</summary>
    public void Tick(float dt)
    {
        if (!AllComplete)
            Elapsed += dt;
    }

    /// <summary>Manual target cycling: point the HUD at the next still-incomplete zone in
    /// list order (wrapping). No-op once the run is complete. Whichever zone ends up displayed is
    /// still auto-advanced when it (or the displayed one) completes.</summary>
    public void CycleTarget()
    {
        if (AllComplete || _active < 0)
            return;
        int n = _zones.Count;
        for (int k = 1; k <= n; k++)
        {
            int i = (int)Mathf.PosMod(_active + k, n);
            if (!_zones[i].Completed)
            {
                if (i != _active)
                    GD.Print($"stunt: target → {_zones[i].DzName} ({_zones[i].MarkerText()})");
                _active = i;
                return;
            }
        }
    }

    /// <summary>Start a fresh run (the scoreboard's "R — New Run"): every zone incomplete,
    /// the clock back to zero, the active target back to the first zone. Unlike a mid-run respawn
    /// this DOES clear progress and the clock — it is the deliberate opposite of the
    /// crash-keeps-everything rule.</summary>
    public void Reset()
    {
        foreach (var z in _zones)
        {
            z.Completed = false;
            z.CompletedAt = 0f;
            z.CompletionOrder = -1;
        }
        CompletedCount = 0;
        AllComplete = false;
        Elapsed = 0f;
        _crossedGates.Clear();
        _haveLastPlanePos = false;
        _active = -1;
        AdvanceActive();
    }

    /// <summary>Debug/testing only (--debug-scoreboard): instantly complete the whole run with
    /// synthetic, increasing split times so the end-of-run scoreboard renders deterministically for
    /// a screenshot / layout pass. Drives the real <see cref="Complete"/> path (fires the events,
    /// sets AllComplete). Not reachable in normal play. <paramref name="extraPerZone"/> pads every
    /// split (the splitscreen race passes the player index, so the synthetic board shows a real
    /// ranking instead of four identical totals).</summary>
    public void DebugCompleteAll(float extraPerZone = 0f)
    {
        for (int i = 0; i < _zones.Count; i++)
        {
            Elapsed += 8f + i * 9.5f + extraPerZone;
            if (!_zones[i].Completed)
                Complete(_zones[i]);
        }
    }

    /// <summary>The zones in the order they were flown through (for the scoreboard) — completed
    /// zones by <see cref="StuntZone.CompletionOrder"/>, any still-incomplete zones appended in
    /// list order.</summary>
    public IEnumerable<StuntZone> InCompletionOrder()
    {
        var done = new List<StuntZone>();
        foreach (var z in _zones)
            if (z.Completed)
                done.Add(z);
        done.Sort((a, b) => a.CompletionOrder.CompareTo(b.CompletionOrder));
        foreach (var z in done)
            yield return z;
        foreach (var z in _zones)
            if (!z.Completed)
                yield return z;
    }

    /// <summary>Compact HUD status: "2/5 zones — Danger Zone [Fly Through] - Train Tunnel Mid",
    /// or "COMPLETE" once the run is done. (A placeholder-but-playable readout; the marker HUD
    /// replaces it with the projected marker + edge arrow.)</summary>
    public string StatusLine() =>
        AllComplete
            ? $"STUNT {CompletedCount}/{TotalCount} — COMPLETE"
            : $"STUNT {CompletedCount}/{TotalCount} — {ActiveZone?.MarkerText() ?? ""}";

    /// <summary>The anchor point for a dzone whose ia.json record names a piece of world
    /// <em>geometry</em> instead of a <c>dzN</c> point marker, or null when the node draws nothing
    /// (every ordinary marker — the caller then keeps using the node's own world origin).
    ///
    /// A <c>dzN</c> marker is a childless <c>model_index -1</c> node whose
    /// <c>RotateTranslateScale.translate</c> IS the zone point, so it has no geometry and takes the
    /// null path. C2's first zone is instead <c>sghangar</c>, the Seaplane Hangar building itself,
    /// whose <c>transform</c> is the JSON string <c>"Initial"</c> → <see cref="GameZNode.Local"/>
    /// null → <see cref="GameZ.WorldTransformOf"/> resolves to (0,0,0), ~8 km from the hangar
    /// (the label was right and the point was wrong). Gamez origins are routinely nowhere near the
    /// geometry they draw — <c>NodeLabels</c> anchors on the mesh AABB centre for the same reason.
    ///
    /// <b>Which point on the structure.</b> These zones are all <c>MSG_OBJ_FLYTHROUGH</c>, so the
    /// zone means the structure's <em>aperture</em>, not its middle. Where the structure carries a
    /// matched pair of door leaves the gap between them is that aperture, so those are anchored on
    /// alone: the hangar's <c>sgh_door1</c>/<c>sgh_door2</c> are retracted to either side of the
    /// front wall leaving a 20 m slit centred on their union centre (−5770.4, 23.5, −5623.9), and
    /// <see cref="DzRadius"/> = 15 m about that point covers the whole slit.
    /// Independently corroborated by <c>dzpath1</c>, whose (otherwise unread)
    /// second polygon is the front aperture outline, centred 1.9 m away at (−5770.4, 23.5, −5622.0).
    /// The whole-structure centre would instead sit 129 m deep inside the hangar, reachable from the
    /// open rear but missable off-centre. Structures with no door pair fall back to that centre.</summary>
    private static Vector3? GeometryAnchor(GameZ gz, GameZNode node)
    {
        var worldXf = gz.WorldTransformOf(node);
        var origin = worldXf.Origin;
        var boxes = new List<(string Name, Aabb Box)>();
        CollectMeshBoxes(gz, node, worldXf, boxes);
        if (boxes.Count == 0)
        {
            return null; // an ordinary dzN point marker — all 53 in this install land here
        }

        int doorCount = 0;
        foreach (var b in boxes)
        {
            if (IsDoorLeaf(b.Name))
            {
                doorCount++;
            }
        }
        bool hasDoorPair = doorCount >= 2;

        Aabb? merged = null;
        foreach (var b in boxes)
        {
            if (hasDoorPair && !IsDoorLeaf(b.Name))
            {
                continue;
            }
            merged = merged == null ? b.Box : merged.Value.Merge(b.Box);
        }
        var anchor = merged!.Value.GetCenter();
        // InvariantCulture: a German locale renders these with comma decimals, which turns an
        // XYZ triple into six ambiguous numbers in the one log line that carries the evidence.
        GD.Print(string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "stunt: {0} is world geometry, not a dz marker — anchored on {1} at ({2:0.0}, {3:0.0}, {4:0.0}) "
            + "instead of its node origin ({5:0.0}, {6:0.0}, {7:0.0})",
            node.Name,
            hasDoorPair ? $"the {doorCount} door leaves of its {boxes.Count} meshes"
                        : $"all {boxes.Count} of its meshes",
            anchor.X, anchor.Y, anchor.Z,
            origin.X, origin.Y, origin.Z));
        return anchor;
    }

    /// <summary>The original's own node naming is the semantic layer here, as it is for props,
    /// control surfaces, wing flares and damage panels — a hangar/garage door leaf is named for
    /// what it is (<c>sgh_door1</c>, <c>sgh_door2</c>).</summary>
    private static bool IsDoorLeaf(string name) =>
        name.Contains("door", StringComparison.OrdinalIgnoreCase);

    /// <summary>World-space AABB of every mesh at or under <paramref name="node"/>, one entry per
    /// drawing node so the caller can pick a subset by name. <paramref name="worldXf"/> is this
    /// node's own world transform (children compose their <see cref="GameZNode.Local"/> onto it);
    /// a null Local contributes identity, which is exactly why the hangar's parts are already in
    /// world coordinates.</summary>
    private static void CollectMeshBoxes(GameZ gz, GameZNode node, Transform3D worldXf,
        List<(string Name, Aabb Box)> into)
    {
        if (node.MeshIndex >= 0 && node.MeshIndex < gz.Meshes.Count)
        {
            var verts = gz.Meshes[node.MeshIndex].Vertices;
            if (verts.Count > 0)
            {
                var box = new Aabb(worldXf * verts[0], Vector3.Zero);
                for (int i = 1; i < verts.Count; i++)
                {
                    box = box.Expand(worldXf * verts[i]);
                }
                into.Add((node.Name, box));
            }
        }
        foreach (var ci in node.Children)
        {
            if (ci >= 0 && ci < gz.Nodes.Count)
            {
                var child = gz.Nodes[ci];
                CollectMeshBoxes(gz, child, worldXf * (child.Local ?? Transform3D.Identity), into);
            }
        }
    }

    private static bool TryReadGates(GameZ gz, string pathName, out StuntGate green, out StuntGate red)
    {
        green = null!;
        red = null!;
        var node = gz.FindByName(pathName);
        if (node == null || node.MeshIndex < 0 || node.MeshIndex >= gz.Meshes.Count)
            return false;
        var mesh = gz.Meshes[node.MeshIndex];
        if (mesh.Polygons.Count != 3)
            return false;

        // The two aperture outlines share their material; the route ribbon has the odd material.
        var byMaterial = new Dictionary<int, List<GameZPolygon>>();
        foreach (var poly in mesh.Polygons)
        {
            if (!byMaterial.TryGetValue(poly.MaterialIndex, out var group))
            {
                group = new List<GameZPolygon>();
                byMaterial.Add(poly.MaterialIndex, group);
            }
            group.Add(poly);
        }
        GameZPolygon? route = null;
        List<GameZPolygon>? gates = null;
        foreach (var group in byMaterial.Values)
        {
            if (group.Count == 2)
                gates = group;
            else if (group.Count == 1)
                route = group[0];
        }
        if (gates == null || route == null
            || !TryMakeGate(gz, node, gates[0], out green)
            || !TryMakeGate(gz, node, gates[1], out red))
            return false;
        return true;
    }

    private static bool TryMakeGate(GameZ gz, GameZNode node, GameZPolygon poly, out StuntGate gate)
    {
        gate = null!;
        if (poly.VertexIndices.Count < 3)
            return false;
        var xf = gz.WorldTransformOf(node);
        var mesh = gz.Meshes[node.MeshIndex];
        var vertices = new Vector3[poly.VertexIndices.Count];
        Vector3 center = Vector3.Zero;
        for (int i = 0; i < vertices.Length; i++)
        {
            int index = poly.VertexIndices[i];
            if (index < 0 || index >= mesh.Vertices.Count)
                return false;
            center += vertices[i] = xf * mesh.Vertices[index];
        }
        center /= vertices.Length;
        Vector3 normal = Vector3.Zero;
        for (int i = 0; i < vertices.Length; i++)
        {
            var a = vertices[i] - center;
            var b = vertices[(i + 1) % vertices.Length] - center;
            normal += a.Cross(b);
        }
        if (normal.LengthSquared() < 1e-6f)
            return false;
        gate = new StuntGate { Vertices = vertices, Center = center, Normal = normal.Normalized() };
        return true;
    }

    private static bool GateCrossing(Vector3 from, Vector3 to, StuntGate gate, out float t)
    {
        t = 0f;
        float fromDistance = gate.Normal.Dot(from - gate.Center);
        float toDistance = gate.Normal.Dot(to - gate.Center);
        if (fromDistance == 0f || toDistance == 0f || Mathf.Sign(fromDistance) == Mathf.Sign(toDistance))
            return false;
        t = fromDistance / (fromDistance - toDistance);
        return PointInGate(from.Lerp(to, t), gate);
    }

    private static bool PointInGate(Vector3 point, StuntGate gate)
    {
        int dropAxis = Mathf.Abs(gate.Normal.X) > Mathf.Abs(gate.Normal.Y)
            ? (Mathf.Abs(gate.Normal.X) > Mathf.Abs(gate.Normal.Z) ? 0 : 2)
            : (Mathf.Abs(gate.Normal.Y) > Mathf.Abs(gate.Normal.Z) ? 1 : 2);
        Vector2 Project(Vector3 p) => dropAxis == 0 ? new Vector2(p.Y, p.Z)
            : dropAxis == 1 ? new Vector2(p.X, p.Z) : new Vector2(p.X, p.Y);
        var q = Project(point);
        bool inside = false;
        for (int i = 0, j = gate.Vertices.Length - 1; i < gate.Vertices.Length; j = i++)
        {
            var a = Project(gate.Vertices[i]);
            var b = Project(gate.Vertices[j]);
            if ((a.Y > q.Y) != (b.Y > q.Y)
                && q.X < (b.X - a.X) * (q.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    private void Complete(StuntZone z)
    {
        z.Completed = true;
        z.CompletedAt = Elapsed;      // cumulative run time — the scoreboard derives splits
        z.CompletionOrder = CompletedCount; // 0-based, before the increment below
        CompletedCount++;
        GD.Print($"stunt: {LogTag}completed {z.DzName} — {z.MarkerText()} ({CompletedCount}/{TotalCount})");
        ZoneCompleted?.Invoke(z);
        // Keep pointing at the manually-cycled target unless it was the zone just
        // completed; otherwise auto-advance to the next incomplete in list order.
        if (_active < 0 || _zones[_active].Completed)
            AdvanceActive();
        if (CompletedCount >= _zones.Count && !AllComplete)
        {
            AllComplete = true;
            GD.Print($"stunt: {LogTag}ALL DANGER ZONES COMPLETE");
            RunCompleted?.Invoke();
        }
        else if (ActiveZone is { } next)
        {
            GD.Print($"stunt: {LogTag}next target → {next.DzName} ({next.MarkerText()})");
        }
    }

    // The displayed target is the first still-incomplete zone in list order (auto-advance on
    // completion; CycleTarget layers manual cycling on top).
    private void AdvanceActive()
    {
        for (int i = 0; i < _zones.Count; i++)
            if (!_zones[i].Completed)
            {
                _active = i;
                return;
            }
        _active = -1;
    }
}
