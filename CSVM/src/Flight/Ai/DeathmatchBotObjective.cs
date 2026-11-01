using System;
using System.Collections.Generic;
using CSVM.Flight.Weapons;
using Godot;

namespace CSVM.Flight.Ai;

/// <summary>One seat's aircraft as <see cref="DeathmatchBotObjective"/> reads it.</summary>
public readonly record struct RivalPlane(Vector3 Position, int Team, bool Live);

/// <summary>
/// Where a Deathmatch bot with no target flies, a remake-only rule: the original seats no
/// Deathmatch bot. Its gunner admits only what stands inside its attack radius. A bot spawned facing
/// away from every enemy would otherwise hold its spawn course for the whole match. It flies at the
/// nearest live hostile aircraft at any range instead, and its gunner's own sweep takes over once
/// that aircraft is in reach.
/// </summary>
public static class DeathmatchBotObjective
{
    /// <summary>The index in <paramref name="planes"/> of the nearest live aircraft hostile to
    /// <paramref name="team"/>, or -1 when none is. The bot's own entry is its own team, so never
    /// hostile.</summary>
    public static int Nearest(Vector3 position, int team, IReadOnlyList<RivalPlane> planes)
    {
        ArgumentNullException.ThrowIfNull(planes);
        int best = -1;
        float bestSq = float.MaxValue;
        for (int i = 0; i < planes.Count; i++)
        {
            var plane = planes[i];
            float d = position.DistanceSquaredTo(plane.Position);
            if (plane.Live && AimAssist.Hostile(team, plane.Team) && d < bestSq)
            {
                best = i;
                bestSq = d;
            }
        }

        return best;
    }
}
