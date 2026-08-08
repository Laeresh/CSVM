using System.Collections.Generic;
using CSVM.Flight;
using Godot;

namespace CSVM.Session;

/// <summary>Where every pilot in a session starts: one call answering for the <b>whole field</b>,
/// not one call per player.
///
/// <para>The whole-field shape is the point of the seam. An implementation that fans the players
/// out abreast has to centre them, which needs the player count before any slot is known, and has
/// to lift the field as one by whatever the worst slot needs to clear the ground, which needs every
/// slot probed before <i>any</i> answer is final. A per-player signature would force such an
/// implementation to accumulate state across four calls and leave player 1's answer wrong until
/// player 4 had asked.</para>
///
/// <para><see cref="SpawnPicker"/> implements it directly, by looping its own per-player
/// <c>ChooseSpawn</c>: that is the plain behaviour — each pilot takes the next entry in the
/// mission's spawn list — and it stays the implementation for solo flight, Dogfight and any
/// scripted run.</para></summary>
public interface IFlightStarts
{
    /// <summary>Resolves one start per player, for players <c>0 … playerCount-1</c> in ascending
    /// order. Ascending is a contract, not an implementation detail: the spawn-list index wraps on
    /// from <paramref name="spawnBase"/> per player, and the <c>spawn [...]</c> log lines are read
    /// in player order.</summary>
    /// <param name="spawns">The mission's instant-action spawn list, or null on a story mission.</param>
    /// <param name="missionZrdrPath">The mission folder, for the objectives.json PLAYER_INIT fallback.</param>
    /// <param name="spawnBase">The list index player 1 starts from (<c>--spawn=</c> or a random pick).</param>
    /// <param name="playerCount">How many rigs this session flies.</param>
    IReadOnlyList<FlightStart> ChooseStarts(IReadOnlyList<SpawnPoint>? spawns, string missionZrdrPath,
        int spawnBase, int playerCount);
}

/// <summary>One pilot's start: a world position and a look-at point one unit ahead along the spawn
/// heading — the pair <see cref="FlightController.Setup"/> already takes, carried unchanged so no
/// call site has to reinterpret it.</summary>
public readonly record struct FlightStart(Vector3 Pos, Vector3 LookAt);
