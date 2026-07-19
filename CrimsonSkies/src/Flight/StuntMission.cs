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

    /// <summary>Messages key for the run-start intro line ("Fly through all the Danger Zones to
    /// win!") — the marker HUD's one-shot banner (item 2).</summary>
    private const string IntroMsgKey = "MSG_BRF_IASF_OBJ2";

    private readonly List<StuntZone> _zones;
    private int _active = -1;

    public bool AllComplete { get; private set; }
    public int CompletedCount { get; private set; }
    public int TotalCount => _zones.Count;
    public IReadOnlyList<StuntZone> Zones => _zones;

    /// <summary>Elapsed run time, seconds, advanced by <see cref="Tick"/> every physics frame —
    /// including through the crash freeze ("the clock never stops"), frozen only once the run is
    /// complete. Read by the marker HUD (item 2) and scoring (item 3). Not reset on respawn (a
    /// mid-run crash keeps the same clock, like the completed zones).</summary>
    public float Elapsed { get; private set; }

    /// <summary>The one-shot run-start line the marker HUD shows ("Fly through all the Danger
    /// Zones to win!"), resolved at load from the message table. Empty if the table is absent.</summary>
    public string IntroLine { get; private set; } = "";

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
        return new StuntMission(zones) { IntroLine = messages.Get(IntroMsgKey) };
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

    /// <summary>Advance the run clock one physics frame. Called every frame — including through
    /// the crash freeze so the clock never stops (item 3 rule) — and stops accumulating once the
    /// run is complete.</summary>
    public void Tick(float dt)
    {
        if (!AllComplete)
            Elapsed += dt;
    }

    private void Complete(StuntZone z)
    {
        z.Completed = true;
        CompletedCount++;
        GD.Print($"stunt: completed {z.DzName} — {z.MarkerText()} ({CompletedCount}/{TotalCount})");
        ZoneCompleted?.Invoke(z);
        // Keep pointing at the manually-cycled target (item 2) unless it was the zone just
        // completed; otherwise auto-advance to the next incomplete in list order.
        if (_active < 0 || _zones[_active].Completed)
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

    /// <summary>Manual target cycling (item 2): point the HUD at the next still-incomplete zone in
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

    /// <summary>Compact HUD status: "2/5 zones — Danger Zone [Fly Through] - Train Tunnel Mid",
    /// or "COMPLETE" once the run is done. (Item 1's placeholder-but-playable readout; item 2
    /// replaces it with the projected marker + edge arrow.)</summary>
    public string StatusLine() =>
        AllComplete
            ? $"STUNT {CompletedCount}/{TotalCount} — COMPLETE"
            : $"STUNT {CompletedCount}/{TotalCount} — {ActiveZone?.MarkerText() ?? ""}";
}
