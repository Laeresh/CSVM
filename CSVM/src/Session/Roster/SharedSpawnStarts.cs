using System.Collections.Generic;
using CSVM.Flight.Modes;
using CSVM.Utils;

namespace CSVM.Session.Roster;

/// <summary>A time-attack race's start: every pilot on the one spawn player 1 takes. Position,
/// heading, throttle and speed are shared, so every run starts in the same state. Anchor selection
/// stays with <see cref="SpawnPicker"/>, which keeps the mission list, <c>PLAYER_INIT</c> and
/// <c>--pos</c>. Pilots in a race do not collide, so sharing the point is safe.
/// ⚠ Not built under <c>--det</c>: the caller picks the plain <see cref="SpawnPicker"/> there, so
/// a scripted run stays byte-identical. Never add a bypass branch inside this class.</summary>
public sealed class SharedSpawnStarts : IFlightStarts
{
    private readonly SpawnPicker _picker;

    /// <param name="picker">Resolves the one spawn; this class reimplements no part of it.</param>
    public SharedSpawnStarts(SpawnPicker picker) => _picker = picker;

    public IReadOnlyList<FlightStart> ChooseStarts(IReadOnlyList<SpawnPoint>? spawns,
        string missionZrdrPath, int spawnBase, int playerCount)
    {
        var (pos, lookAt) = _picker.ChooseSpawn(spawns, missionZrdrPath, spawnBase, 0, "race start ");
        var (throttle, speed) = _picker.StartState(spawns, missionZrdrPath);
        var start = new FlightStart(pos, lookAt, throttle, speed);
        var starts = new FlightStart[playerCount];
        for (int i = 0; i < playerCount; i++)
            starts[i] = start;
        Log.Info("flight", $"spawn [race] all {playerCount} pilots start from the one point ({pos.X:0},{pos.Y:0},{pos.Z:0})");
        return starts;
    }
}
