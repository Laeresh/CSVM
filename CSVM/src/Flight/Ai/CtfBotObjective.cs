using System;
using System.Collections.Generic;
using CSVM.Flight.Modes;
using Godot;

namespace CSVM.Flight.Ai;

/// <summary>The rule a Capture the Flag bot flies this step, in <see cref="CtfBotObjective"/>'s
/// priority order.</summary>
public enum CtfBotRole
{
    /// <summary>No flag rule applies, and the bot flies as a Deathmatch bot.</summary>
    None,

    /// <summary>It carries a flag and flies straight home with it.</summary>
    Carrier,

    /// <summary>An enemy carries its team's flag, and it chases the carrier.</summary>
    Chaser,

    /// <summary>A flag floats, and it flies straight for it.</summary>
    Retriever,

    /// <summary>It flies straight for the enemy flag at its base.</summary>
    Attacker,

    /// <summary>It stays over its own flag and dogfights there.</summary>
    Defender,
}

/// <summary>One seat as <see cref="CtfBotObjective"/> reads it: its lobby team, the aircraft a
/// gunner is handed and where it is. Then whether a bot flies it and whether it is in play.</summary>
public readonly record struct CtfPilot(int Seat, int Team, object Source, Vector3 Position, bool Bot, bool Live);

/// <summary>A bot's flag order: its rule, the point it flies to, the carrier it chases and the
/// rule's fixed reason.</summary>
public readonly record struct CtfBotOrder(CtfBotRole Role, Vector3? Goal, object? Target, string Why)
{
    /// <summary>Whether the bot flies straight at <see cref="Goal"/> whatever its quarry, firing
    /// only where its guns already bear.</summary>
    public bool Straight => Role is CtfBotRole.Carrier or CtfBotRole.Retriever or CtfBotRole.Attacker;
}

/// <summary>
/// A Capture the Flag bot's order each step, a remake-only rule, since the original seats no bots.
/// A carrier flies home. Otherwise the bot chases an enemy carrying its team's flag. On a team of
/// <see cref="DefenderTeamSize"/> or more pilots, the team's bot nearest its own flag then defends
/// it. Any other bot flies for a floating flag of either team, then for the enemy flag at its base.
/// Flags move only through <see cref="FlagMatch"/>'s asks (docs/org/multiplayer-ctf.md, "Remake-only
/// rule, bots").
/// </summary>
public static class CtfBotObjective
{
    /// <summary>The remake's own: the fewest pilots, people and bots together, a team needs before
    /// one of its bots stays home to defend.</summary>
    public const int DefenderTeamSize = 3;

    /// <summary>The remake's own: how far above a floating flag a run passes, metres, inside
    /// <see cref="FlagMatch.Reach"/>, since a flag at rest lies on the ground. A flag at its base is
    /// passed at its own point, since it stands in the base's hall under a low roof
    /// (docs/org/multiplayer-ctf.md).</summary>
    public const float FloatingPassAboveM = 10f;

    /// <summary>The remake's own: how far above its own flag a defender flies, metres.</summary>
    public const float DefendAboveM = 150f;

    /// <summary>The remake's own: how far from its own flag a defender may chase, metres, measured
    /// flat. Past it the defender breaks off and flies home (<see cref="AiPilot.Tether"/>).</summary>
    public const float DefendRadiusM = 1000f;

    /// <summary>The order for the bot at <paramref name="seat"/> among <paramref name="pilots"/>,
    /// read off the host's <paramref name="flags"/>. A bot on no team with a flag flies no rule.
    /// A defender leaves home only to chase its flag's carrier, never for a floating flag.</summary>
    public static CtfBotOrder Decide(int seat, FlagMatch flags, IReadOnlyList<CtfPilot> pilots)
    {
        ArgumentNullException.ThrowIfNull(flags);
        ArgumentNullException.ThrowIfNull(pilots);
        if (Find(pilots, seat) is not { Team: > 0 } bot || flags.RowOf(bot.Team) is not { } own)
        {
            return new CtfBotOrder(CtfBotRole.None, null, null, "has no flag of its own");
        }

        var home = flags.HomeOf(bot.Team);
        if (flags.Carried(seat) != 0)
        {
            return new CtfBotOrder(CtfBotRole.Carrier, home, null, "carries a flag home");
        }

        if (own.State == FlagState.Held && Find(pilots, own.Holder) is { Live: true } carrier && carrier.Team != bot.Team)
        {
            return new CtfBotOrder(CtfBotRole.Chaser, null, carrier.Source, "chases its flag's carrier");
        }

        var over = home + (Vector3.Up * DefendAboveM);
        if (Defends(bot, home, flags, pilots))
        {
            return new CtfBotOrder(CtfBotRole.Defender, over, null, "defends its flag");
        }

        if (Nearest(bot.Position, flags, floating: true, bot.Team) is { } loose)
        {
            return new CtfBotOrder(CtfBotRole.Retriever, loose + (Vector3.Up * FloatingPassAboveM), null, "goes for a floating flag");
        }

        // With the enemy flag carried by a teammate there is nothing to attack, so it waits at home.
        return Nearest(bot.Position, flags, floating: false, bot.Team) is { } enemy
            ? new CtfBotOrder(CtfBotRole.Attacker, enemy, null, "attacks the enemy flag")
            : new CtfBotOrder(CtfBotRole.Defender, over, null, "waits over its flag");
    }

    private static CtfPilot? Find(IReadOnlyList<CtfPilot> pilots, int seat)
    {
        for (int i = 0; i < pilots.Count; i++)
        {
            if (pilots[i].Seat == seat)
            {
                return pilots[i];
            }
        }

        return null;
    }

    // The nearest floating flag of any team, or the nearest other team's flag at its base.
    private static Vector3? Nearest(Vector3 from, FlagMatch flags, bool floating, int team)
    {
        Vector3? best = null;
        var rows = flags.Rows;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            Vector3? at = floating ? flags.FloatingAt(row.Team)
                : row.State == FlagState.Home && row.Team != team ? flags.HomeOf(row.Team)
                : null;
            if (at is { } point && (best is not { } held || from.DistanceSquaredTo(point) < from.DistanceSquaredTo(held)))
            {
                best = point;
            }
        }

        return best;
    }

    // On a big enough team, whether this bot is the one in play, carrying nothing, nearest its own
    // flag's base. A tie goes to the lower seat.
    private static bool Defends(CtfPilot bot, Vector3 home, FlagMatch flags, IReadOnlyList<CtfPilot> pilots)
    {
        int team = 0;
        CtfPilot? nearest = null;
        for (int i = 0; i < pilots.Count; i++)
        {
            var pilot = pilots[i];
            if (pilot.Team != bot.Team)
            {
                continue;
            }

            team++;
            if (pilot.Bot && pilot.Live && flags.Carried(pilot.Seat) == 0
                && (nearest is not { } held || home.DistanceSquaredTo(pilot.Position) < home.DistanceSquaredTo(held.Position)))
            {
                nearest = pilot;
            }
        }

        return team >= DefenderTeamSize && nearest?.Seat == bot.Seat;
    }
}
