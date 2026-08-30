using System;
using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>
/// A campaign mission's own danger zones: the <c>dzpathN</c> names its <c>objectives.zrd</c>
/// authors inside <c>DANGER_ZONES_COMPLETED</c>, narrowed by the mission's own <c>dzones.zrd</c>
/// <c>disable</c> list, tested against the same entry/exit gate-crossing rule the <c>--stunt</c>
/// module uses (docs/formats/missions.md): a route ribbon plus a material-matched pair of gate
/// polygons, and a segment crossing inside each in either order. A separate implementation from
/// <c>Flight.StuntMission</c> by ownership, not by mechanism — the two read the same physical
/// <c>dzpathN</c> mesh, just from different authoring surfaces (a mission's own script names
/// versus <c>ia.json</c>'s zone list).
/// </summary>
internal sealed class CampaignDangerZones
{
    private readonly List<Zone> _zones;

    // One previous position per human, by field index: the crossing test is a segment, so each
    // human needs their own. A slot stays empty until that human's first Update, which is what
    // keeps a joining player from crossing a gate on the jump from nowhere to their aeroplane.
    private readonly List<Vector3?> _lastPos = new();

    private CampaignDangerZones(List<Zone> zones) => _zones = zones;

    /// <summary>Every gate this mission armed.</summary>
    public int Count => _zones.Count;

    /// <summary>Builds the tracker from the mission's <c>DANGER_ZONES_COMPLETED</c> names, minus
    /// <c>dzones.zrd</c>'s <c>disable</c> list, resolved against the world's gate geometry. Null
    /// if the mission names no zone, or none resolve.</summary>
    public static CampaignDangerZones? Load(ObjectiveScript script, GameZ gamez, string missionZrdrPath)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in script.Objectives)
            foreach (var name in def.DangerZones)
                if (seen.Add(name))
                    names.Add(name);
        if (names.Count == 0)
            return null;

        var disabled = ReadDisabled(missionZrdrPath);
        var zones = new List<Zone>();
        var skipped = new List<string>();
        foreach (var name in names)
        {
            if (disabled.Contains(name))
            {
                skipped.Add($"{name} (disabled)");
            }
            else if (TryReadGates(gamez, name, out var green, out var red))
            {
                zones.Add(new Zone { PathName = name, Green = green, Red = red });
            }
            else
            {
                skipped.Add($"{name} (no gate geometry)");
            }
        }

        if (skipped.Count > 0)
            Log.Info("campaign", $"danger zones: {skipped.Count} name(s) not armed: {string.Join(", ", skipped)}");
        if (zones.Count == 0)
            return null;
        Log.Info("campaign", $"danger zones: {zones.Count} gate(s) armed from objectives.zrd");
        return new CampaignDangerZones(zones);
    }

    /// <summary>One physics-frame test over the whole human field: any gate a human's movement
    /// segment crossed this frame, completing a zone once both its gates have been crossed. The
    /// crossed flags are per zone, so they UNION across the field and the pair may be split between
    /// two humans. Fires <paramref name="onCompleted"/> with
    /// the <c>dzpathN</c> name, the exact string a <c>DANGER_ZONES_COMPLETED</c> condition
    /// names.</summary>
    public void Update(IReadOnlyList<HumanState> humans, Action<string> onCompleted)
    {
        for (int i = 0; i < humans.Count; i++)
        {
            Vector3 now = humans[i].Position;
            while (_lastPos.Count <= i)
                _lastPos.Add(null);
            if (_lastPos[i] is not { } was)
            {
                _lastPos[i] = now;
                continue;
            }

            foreach (var z in _zones)
            {
                if (z.Completed)
                    continue;
                if (GateCrossing(was, now, z.Green))
                    z.GreenCrossed = true;
                if (GateCrossing(was, now, z.Red))
                    z.RedCrossed = true;
                if (z.GreenCrossed && z.RedCrossed)
                {
                    z.Completed = true;
                    Log.Info("campaign", $"danger zone '{z.PathName}' completed");
                    onCompleted(z.PathName);
                }
            }

            _lastPos[i] = now;
        }
    }

    /// <summary>The one-human form, for a caller with a position and no field: a suite driving a
    /// probe along a gate normal, which is how the shipped gate pairs are proved.</summary>
    public void Update(Vector3 humanPos, Action<string> onCompleted) =>
        Update(new[] { new HumanState(humanPos, null, false) }, onCompleted);

    /// <summary>The mission's own zone-set override (docs/formats/missions.md "Zone overrides"):
    /// a <c>dzpathN</c> named here is switched off for this mission, for the player's scoring and
    /// the AI's runs alike. Absent for most missions (no <c>dzones.zrd</c> at all), which reads as
    /// nothing disabled.</summary>
    internal static HashSet<string> ReadDisabled(string missionZrdrPath)
    {
        var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(missionZrdrPath))
            return disabled;
        List<object?> root;
        try
        {
            root = Zrdr.LoadFileOrEmpty(missionZrdrPath, "dzones.json");
        }
        catch (System.IO.IOException)
        {
            return disabled; // no dzones.zrd override for this mission
        }

        if (ZrdrDict.FromAlternating(root).List("disable") is { } list)
            foreach (var v in list)
                if (v is string s)
                    disabled.Add(s);
        return disabled;
    }

    /// <summary>Test seam: one armed zone's gate centres and outward normals, the same values
    /// <see cref="Update"/> tests against, so a suite can build a real crossing segment without
    /// duplicating the material-matched gate read.</summary>
    internal bool TryGateProbe(string pathName,
        out Vector3 greenCenter, out Vector3 greenNormal, out Vector3 redCenter, out Vector3 redNormal)
    {
        foreach (var z in _zones)
        {
            if (!string.Equals(z.PathName, pathName, StringComparison.OrdinalIgnoreCase))
                continue;
            greenCenter = z.Green.Center;
            greenNormal = z.Green.Normal;
            redCenter = z.Red.Center;
            redNormal = z.Red.Normal;
            return true;
        }
        greenCenter = redCenter = greenNormal = redNormal = default;
        return false;
    }

    // Same read as Flight.StuntMission.TryReadGates: a dzpathN mesh is always route ribbon plus
    // exactly two gate-outline polygons sharing one material, the route the odd one out.
    private static bool TryReadGates(GameZ gz, string pathName, out Gate green, out Gate red)
    {
        green = null!;
        red = null!;
        var node = gz.FindByName(pathName);
        if (node == null || node.MeshIndex < 0 || node.MeshIndex >= gz.Meshes.Count)
            return false;
        var mesh = gz.Meshes[node.MeshIndex];
        if (mesh.Polygons.Count != 3)
            return false;

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

        List<GameZPolygon>? gates = null;
        foreach (var group in byMaterial.Values)
            if (group.Count == 2)
                gates = group;
        if (gates == null
            || !TryMakeGate(gz, node, gates[0], out green)
            || !TryMakeGate(gz, node, gates[1], out red))
            return false;
        return true;
    }

    private static bool TryMakeGate(GameZ gz, GameZNode node, GameZPolygon poly, out Gate gate)
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
        gate = new Gate { Vertices = vertices, Center = center, Normal = normal.Normalized() };
        return true;
    }

    private static bool GateCrossing(Vector3 from, Vector3 to, Gate gate)
    {
        float fromDistance = gate.Normal.Dot(from - gate.Center);
        float toDistance = gate.Normal.Dot(to - gate.Center);
        if (fromDistance == 0f || toDistance == 0f || Mathf.Sign(fromDistance) == Mathf.Sign(toDistance))
            return false;
        float t = fromDistance / (fromDistance - toDistance);
        return PointInGate(from.Lerp(to, t), gate);
    }

    private static bool PointInGate(Vector3 point, Gate gate)
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

    private sealed class Gate
    {
        public required Vector3[] Vertices { get; init; }
        public required Vector3 Center { get; init; }
        public required Vector3 Normal { get; init; }
    }

    private sealed class Zone
    {
        public string PathName = "";
        public Gate Green = null!;
        public Gate Red = null!;
        public bool GreenCrossed;
        public bool RedCrossed;
        public bool Completed;
    }
}
