using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>Picks each player's flight spawn against a <see cref="SessionSpec"/>: the shared
/// spawn-list index (<c>--spawn=</c> or a random pick), the per-player point from that list or
/// objectives.json's PLAYER_INIT, and the <c>--spawn-at=</c>/<c>--pos=</c> debug override.
/// Constructed once per session. As an <see cref="IFlightStarts"/> it is the plain whole-field
/// answer: every pilot simply takes the next entry in the list.</summary>
public sealed class SpawnPicker : IFlightStarts
{
    /// <summary>An active Instant Action mission's own scenario,
    /// which <c>BuildFlightRigs</c> already draws <see cref="ChooseSpawn"/>'s spawn LIST from —
    /// this only keeps <see cref="LogSpawn"/>'s printed tag truthful about which list that was,
    /// rather than the stale <c>_spec.Scenario</c>. Null outside one.</summary>
    public string? ScenarioOverride;

    // Splitscreen: fan the players out abreast so they don't spawn inside each other.
    private const float SpawnAbreast = 60f;

    private readonly SessionSpec _spec;

    public SpawnPicker(SessionSpec spec)
    {
        _spec = spec;
    }

    /// <summary>The spawn index player 1 starts from: --spawn=N if given, else a random pick per
    /// launch like the original. Each further player takes the next index in list order (wrapping),
    /// so splitscreen players never share a spawn point.
    /// ⚠ The random draw is <c>Rng.Stream(Rng.Spawn)</c> — keep its position relative to the
    /// session's other RNG draws; reordering moves pinned --det goldens.</summary>
    public int ChooseSpawnBase(IReadOnlyList<SpawnPoint>? spawns)
    {
        if (spawns == null || spawns.Count == 0)
            return 0;
        return _spec.SpawnIndex >= 0
            ? Mathf.Clamp(_spec.SpawnIndex, 0, spawns.Count - 1)
            : (int)(Rng.Stream(Rng.Spawn).Randi() % (uint)spawns.Count);
    }

    /// <summary>The whole field, the plain way: each player picked independently, in ascending
    /// order, so P1 takes <paramref name="spawnBase"/> and everyone else the next list entry
    /// (wrapping). No player's start depends on another's, so the loop is the whole
    /// implementation.</summary>
    public IReadOnlyList<FlightStart> ChooseStarts(IReadOnlyList<SpawnPoint>? spawns,
        string missionZrdrPath, int spawnBase, int playerCount)
    {
        var (throttle, speed) = StartState(spawns, missionZrdrPath);
        var starts = new FlightStart[playerCount];
        for (int pi = 0; pi < playerCount; pi++)
        {
            // The log tag names the pane in splitscreen and is empty when flying alone.
            string tag = playerCount > 1 ? $"P{pi + 1} " : "";
            var (pos, lookAt) = ChooseSpawn(spawns, missionZrdrPath, spawnBase, pi, tag);
            starts[pi] = new FlightStart(pos, lookAt, throttle, speed);
        }
        return starts;
    }

    /// <summary>The throttle and speed every pilot in this mission starts on, out of the mission's
    /// own PLAYER_INIT (docs/formats/spawns.md). ⚠ Whole-field like <see cref="ChooseStarts"/>:
    /// the record is one per mission, so never make this per-player — that invites a per-player
    /// answer the original does not have. <see cref="RaceGrid"/> calls it rather than
    /// restating it.</summary>
    public (float ThrottleFrac, float SpeedMps) StartState(IReadOnlyList<SpawnPoint>? spawns,
        string missionZrdrPath)
    {
        // A present ia.json spawn list is what makes this an instant-action launch, the same test
        // ChooseSpawn already selects the position source on.
        bool instantAction = spawns is { Count: > 0 };
        if (SpawnPoints.LoadPlayerInit(missionZrdrPath) is not { } init)
        {
            // Silent with no mission to read (a lab, a headless test): that is not a data problem.
            // A mission that HAS a zrdr and still has no record is, so that one says so.
            if (!string.IsNullOrEmpty(missionZrdrPath))
                Log.Warn("flight", $"no PLAYER_INIT for {_spec.Chapter}/{_spec.Mission} — starting on the shipped-majority throttle/speed");
            return (instantAction ? SpawnPoints.InstantActionThrottleFrac : SpawnPoints.DefaultThrottleFrac,
                SpawnPoints.DefaultSpeedMps);
        }
        float throttle = instantAction ? SpawnPoints.InstantActionThrottleFrac : init.ThrottleFrac;
        Log.Info("flight", $"start [{_spec.Chapter}/{_spec.Mission}] throttle={throttle:0.00} speed={init.SpeedMps:0.#}m/s ({init.SpeedMps * 2.2369363f:0}mph)");
        return (throttle, init.SpeedMps);
    }

    /// <summary>Picks one player's flight spawn: a world position + a look-at point one unit
    /// ahead along the spawn heading. Instant-action missions (IA1) draw from ia.json's scenario
    /// spawn list at <paramref name="spawnBase"/> + the player index (wrapping). Story missions
    /// (M0x, no ia.json) fall back to objectives.json PLAYER_INIT. A fixed C1 spawn is the last
    /// resort if neither is present.</summary>
    public (Vector3 pos, Vector3 lookAt) ChooseSpawn(IReadOnlyList<SpawnPoint>? spawns,
        string missionZrdrPath, int spawnBase, int playerIndex, string tag)
    {
        // ⚠ Tested BEFORE the spawn list — that order is why --pos beats it, and RaceGrid
        // inherits the override for free by delegating here rather than reimplementing it.
        // Bypasses the list entirely: a scripted run starts short of a target, no maneuvering.
        if (_spec.SpawnAt is { } at)
        {
            var dir = _spec.SpawnDir ?? Vector3.Forward;
            if (dir.LengthSquared() < 1e-6f)
                dir = Vector3.Forward;
            dir = dir.Normalized();
            // Splitscreen: fan the players out abreast so they don't spawn inside each other.
            at += dir.Cross(Vector3.Up).Normalized() * (playerIndex * SpawnAbreast);
            Log.Info("flight", $"spawn [{tag}override] pos=({at.X:0},{at.Y:0},{at.Z:0}) dir=({dir.X:0.000},{dir.Y:0.000},{dir.Z:0.000})");
            return (at, at + dir);
        }

        if (spawns is { Count: > 0 })
        {
            int i = (spawnBase + playerIndex) % spawns.Count;
            return LogSpawn($"{tag}{ScenarioOverride ?? _spec.Scenario} #{i} of {spawns.Count}", spawns[i]);
        }
        // No instant-action spawns (only IA1 folders have ia.json) — use the story-mission
        // spawn from objectives.json PLAYER_INIT (position + heading; StartState takes the rest).
        if (SpawnPoints.LoadPlayerInit(missionZrdrPath) is { } init)
            return LogSpawn("PLAYER_INIT", init.Spawn);

        GD.PushWarning($"no ia.json / PLAYER_INIT spawn for {_spec.Chapter}/{_spec.Mission} — using fallback spawn");
        return (new Vector3(-6200, 500, -3300), new Vector3(-5700, 350, -6300));
    }

    // Turns a spawn (position + heading) into a (position, look-at) pair — the nose
    // (-Z) rotated by the heading (yaw about up) — and logs it for cross-checking the data.
    private (Vector3 pos, Vector3 lookAt) LogSpawn(string label, SpawnPoint s)
    {
        var forward = new Basis(Vector3.Up, Mathf.DegToRad(s.HeadingDeg)) * Vector3.Forward;
        Log.Info("flight", $"spawn [{_spec.Chapter}/{_spec.Mission} {label}] pos=({s.Position.X:0},{s.Position.Y:0},{s.Position.Z:0}) heading={s.HeadingDeg:0}°");
        return (s.Position, s.Position + forward);
    }
}
