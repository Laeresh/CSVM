using System.Collections.Generic;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Mech3;
using CSVM.Session.Roster;
using CSVM.Session.World;

namespace CSVM.Launch;

/// <summary>The kill and crash lines in each pane's message stack, one step of the session build.
/// A spent hull posts one line into every pane's stack, worded and coloured as that pane reads it
/// (docs/org/vehicleDamage.md). A hull flown into the world takes the crash notice in its own
/// pane. A Dogfight seat's death takes the match's death lines instead.
/// Module entry: docs/architecture/Launch.md on src/Launch/KillLines.cs.</summary>
internal static class KillLines
{
    /// <summary>Hooks every pane's aircraft and the roster's AI. ⚠ Keep the roster hook. A wave's
    /// aircraft never passes through the pane loop. <paramref name="pilotName"/> is the name the
    /// line prints when the player is the one shot down.</summary>
    public static void Wire(IReadOnlyList<PlayerRig> rigs, FlightRoster roster, Messages strings,
        VersusDirector? dogfight, string? pilotName)
    {
        void PostKillLine(FlightController victim, int victimId, int? killer)
        {
            // A seat's death in a match takes the Dogfight death lines, which post on every death,
            // crashes included. On the wire the host's notice posts them, never this report.
            if (dogfight?.TakeKillLine(victimId, killer) == true)
            {
                return;
            }

            if (!HudMessages.WordsKillLine(victim))
            {
                return;
            }

            foreach (var pane in rigs)
            {
                if (pane.Controller is not { MessageStack: { } stack } viewer)
                {
                    continue;
                }
                // An AI in a match keeps the single-player wording.
                HudMessages.PostKill(stack, strings, victim, viewer.Team,
                    ReferenceEquals(victim, viewer), pilotName);
            }
        }

        foreach (var rig in rigs)
        {
            if (rig.Controller is { } human)
            {
                human.Downed += (victimId, killer) => PostKillLine(human, victimId, killer);
                // The crash notice is the local player's own announcement, so it lands in the
                // crashing pane's stack alone rather than in every pane's.
                human.GroundImpact += crashed =>
                {
                    if (crashed.MessageStack is { } own)
                    {
                        HudMessages.PostCrash(own, strings);
                    }
                };
            }
        }

        roster.VehicleDowned += (victim, killer) =>
            PostKillLine(victim, victim.PlayerIndex, killer);
    }
}
