using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Airframe;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Launch;
using Godot;

namespace CSVM.Testing;

internal static class BotSuiteHelper
{
    // How close to a table entry a placed aeroplane counts as standing on it, horizontally, in
    // metres. Read on the step it is placed, before it has flown.
    private const float EntryTolerance = 5f;

    /// <summary>Puts <paramref name="pilot"/> 500 m above where it flies, with a fresh collision
    /// window, by its owner's own respawn. A glide into the ground then never adds a death to a
    /// reading.</summary>
    internal static void Lift(FlightController pilot)
    {
        var at = pilot.WorldPosition + (Vector3.Up * 500f);
        pilot.RespawnAt(at, at + (Vector3.Right * 100f));
        pilot.ArmSpawnTimers();
    }

    /// <summary>Which entry of <paramref name="table"/> <paramref name="placed"/> stands on,
    /// horizontally, or -1.</summary>
    internal static int EntryAt(IReadOnlyList<SpawnPoint> table, FlightController placed)
    {
        var pos = placed.WorldPosition;
        for (int i = 0; i < table.Count; i++)
        {
            var d = table[i].Position - pos;
            if (Mathf.Abs(d.X) < EntryTolerance && Mathf.Abs(d.Z) < EntryTolerance)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The Dogfight's ranked board as one line, which is what a results screen draws from.</summary>
    internal static string Scoreboard(GameSession session) =>
        string.Join(" ", session.Dogfight!.Match.Standings()
            .Select(s => $"#{s.Rank}P{s.PlayerIndex + 1}:{s.Score}/{s.Kills}K/{s.Deaths}D"));

    /// <summary>Every slot of <paramref name="pilot"/>'s message stack, joined, for a check's detail.</summary>
    internal static string PaneLines(FlightController pilot) =>
        pilot.MessageStack is { } stack
            ? string.Join(" / ", Enumerable.Range(0, HudMessages.Slots).Select(stack.LineAt))
            : "no stack";
}
