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
    public string DzName = "";        // dz1..dzN — the point marker (mesh_index -1) the sphere tests against
    public string PathName = "";      // dzpath1..dzpathN — the AI/route ribbon (never rendered; --debug-dzpaths)
    public Vector3 Position;          // world-space centre of the completion sphere
    public string Description = "";   // resolved, e.g. "Train Tunnel Mid"
    public string Category = "";      // resolved, e.g. "Danger Zone"
    public string Help = "";          // resolved action, e.g. "Fly Through"
    public bool Completed;

    /// <summary>Run clock, seconds, at the moment this zone was flown through — its cumulative
    /// time from run start (item 3 scoring). 0 until completed; the scoreboard's per-zone split is
    /// the delta between consecutive completions in <see cref="CompletionOrder"/>.</summary>
    public float CompletedAt;

    /// <summary>0-based order in which this zone was cleared (the run is order-free, so this is
    /// the flown order, not the ia.json list order). −1 until completed.</summary>
    public int CompletionOrder = -1;

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
    public const float DzRadius = 15f;

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

    /// <summary>Prefix for this run's log lines ("P2 " in a splitscreen race, item 7). Empty in a
    /// solo run, so single-player logs read exactly as before — but with four pilots clearing zones
    /// in one shared world the completion lines are otherwise indistinguishable.</summary>
    public string LogTag = "";

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

    /// <summary>A second, independent run over the same Danger Zones (M2.5 item 7, splitscreen
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
                Description = z.Description,
                Category = z.Category,
                Help = z.Help,
            });
        return new StuntMission(zones) { IntroLine = IntroLine };
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
        z.CompletedAt = Elapsed;      // cumulative run time — the scoreboard derives splits (item 3)
        z.CompletionOrder = CompletedCount; // 0-based, before the increment below
        CompletedCount++;
        GD.Print($"stunt: {LogTag}completed {z.DzName} — {z.MarkerText()} ({CompletedCount}/{TotalCount})");
        ZoneCompleted?.Invoke(z);
        // Keep pointing at the manually-cycled target (item 2) unless it was the zone just
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

    /// <summary>Start a fresh run (item 3, the scoreboard's "R — New Run"): every zone incomplete,
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

    /// <summary>The zones in the order they were flown through (item 3 scoreboard) — completed
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

    /// <summary>"m:ss.t" run-clock formatting, shared by the marker HUD status line and the
    /// scoreboard (item 3). Invariant culture so the decimal is always a period regardless of the
    /// player's system locale (a game clock, and deterministic across screenshot runs).</summary>
    public static string FormatTime(float seconds)
    {
        int min = (int)(seconds / 60f);
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0}:{1:00.0}", min, seconds - min * 60f);
    }

    /// <summary>Compact HUD status: "2/5 zones — Danger Zone [Fly Through] - Train Tunnel Mid",
    /// or "COMPLETE" once the run is done. (Item 1's placeholder-but-playable readout; item 2
    /// replaces it with the projected marker + edge arrow.)</summary>
    public string StatusLine() =>
        AllComplete
            ? $"STUNT {CompletedCount}/{TotalCount} — COMPLETE"
            : $"STUNT {CompletedCount}/{TotalCount} — {ActiveZone?.MarkerText() ?? ""}";
}
