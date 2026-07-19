using System;
using System.Collections.Generic;
using CrimsonSkies.Mech3;
using Godot;

namespace CrimsonSkies.Flight;

/// <summary>One Danger Zone of a stunt run: the <c>dzN</c> marker point's world position, its
/// route ribbon name (<c>dzpathN</c>, debug-only geometry), the resolved display strings, and
/// whether the player has flown through it yet.</summary>
public sealed class StuntZone
{
    public string DzName = "";        // dz1..dzN — the point marker (mesh_index -1) the sphere tests against
    public string PathName = "";      // dzpath1..dzpathN — the AI/route ribbon (never rendered; --debug-dzpaths)
    public Vector3 Position;          // world-space centre of the completion sphere
    public string Description = "";   // resolved, e.g. "Train Tunnel Mid"
    public string Category = "";      // resolved, e.g. "Danger Zone"
    public string Help = "";          // resolved action, e.g. "Fly Through"
    public bool Completed;

    /// <summary>The original's assembled marker text without the clock suffix (item 2 adds
    /// that): "Danger Zone [Fly Through] - Train Tunnel Mid". Degrades gracefully if any
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

/// <summary>
/// The Stunt Flying instant-action mode (Milestone 2.5 item 1). A stunt run's objective is to
/// fly through every Danger Zone; each <c>dzN</c> marker completes when the plane passes within
/// <see cref="DzRadius"/> of its point, order-free, and the run ends when all are done.
///
/// The zone list is the mission ia.json's <c>dzones</c> (<c>[dzpathN, dzN]</c> pairs); each
/// <c>dzN</c>'s world position comes straight from the chapter gamez (a point marker under the
/// identity World root), and its display strings from targets.json → messages.json. Detection
/// and completion only — the marker HUD (item 2) and timed scoring (item 3) build on this.
/// </summary>
public sealed class StuntMission
{
    /// <summary>Sphere radius, m, for "flew through" a danger zone (TUNE — the original has no
    /// gate geometry; a single point + radius approximates the bridge/tunnel/hangar opening).</summary>
    public const float DzRadius = 60f;

    private readonly List<StuntZone> _zones;
    private int _active = -1;

    public bool AllComplete { get; private set; }
    public int CompletedCount { get; private set; }
    public int TotalCount => _zones.Count;
    public IReadOnlyList<StuntZone> Zones => _zones;

    /// <summary>The zone the HUD points at: the first still-incomplete zone in list order
    /// (item 2's cycling overrides the displayed one). Null once the run is complete.</summary>
    public StuntZone? ActiveZone => _active >= 0 && _active < _zones.Count ? _zones[_active] : null;

    /// <summary>Fired once per zone the moment it is flown through (item 3 records a split).</summary>
    public event Action<StuntZone>? ZoneCompleted;
    /// <summary>Fired once when the last zone completes (item 3 stops the clock / shows the board).</summary>
    public event Action? RunCompleted;

    private StuntMission(List<StuntZone> zones)
    {
        _zones = zones;
        AdvanceActive();
    }

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
            var t = targets.For(dzName);
            zones.Add(new StuntZone
            {
                DzName = dzName,
                PathName = pathName,
                Position = worldGamez.WorldTransformOf(node).Origin,
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
        return new StuntMission(zones);
    }

    /// <summary>Physics-frame test: complete any incomplete zone the plane is now within
    /// <see cref="DzRadius"/> of (order-free — several can complete in one pass).</summary>
    public void Update(Vector3 planePos)
    {
        if (AllComplete)
            return;
        float r2 = DzRadius * DzRadius;
        foreach (var z in _zones)
            if (!z.Completed && planePos.DistanceSquaredTo(z.Position) <= r2)
                Complete(z);
    }

    private void Complete(StuntZone z)
    {
        z.Completed = true;
        CompletedCount++;
        GD.Print($"stunt: completed {z.DzName} — {z.MarkerText()} ({CompletedCount}/{TotalCount})");
        ZoneCompleted?.Invoke(z);
        AdvanceActive();
        if (CompletedCount >= _zones.Count && !AllComplete)
        {
            AllComplete = true;
            GD.Print("stunt: ALL DANGER ZONES COMPLETE");
            RunCompleted?.Invoke();
        }
        else if (ActiveZone is { } next)
        {
            GD.Print($"stunt: next target → {next.DzName} ({next.MarkerText()})");
        }
    }

    // The displayed target is the first still-incomplete zone in list order (auto-advance on
    // completion; item 2 layers manual cycling on top).
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

    /// <summary>Compact HUD status: "2/5 zones — Danger Zone [Fly Through] - Train Tunnel Mid",
    /// or "COMPLETE" once the run is done. (Item 1's placeholder-but-playable readout; item 2
    /// replaces it with the projected marker + edge arrow.)</summary>
    public string StatusLine() =>
        AllComplete
            ? $"STUNT {CompletedCount}/{TotalCount} — COMPLETE"
            : $"STUNT {CompletedCount}/{TotalCount} — {ActiveZone?.MarkerText() ?? ""}";
}
