using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight.Modes;

/// <summary>Which pilots a rearm base serves, by the lobby's mission type
/// (<c>FUN_0049b970</c> branches on mode <c>0x71c194</c>).</summary>
public enum RearmRule : byte
{
    /// <summary>Every pilot at every base: Deathmatch, with or without teams (modes 1 and 2).</summary>
    AnyBase = 0,

    /// <summary>Only the base whose team is the pilot's own lobby team: Capture the Flag and
    /// Zeppelin vs Zeppelin (modes 3 and 4).</summary>
    OwnTeam = 1,
}

/// <summary>One base as <c>FUN_00495980</c> lists it: the lobby team it belongs to, and where it
/// stands this step. A null position offers nothing, which is a lost hull's base.</summary>
public readonly record struct RearmBase(int Team, Vector3? Position);

/// <summary>
/// The multiplayer rearm: a pilot entering a base's radius is restored to full health, armour and
/// loadout, once per entry. It holds the rule, the radius and each seat's latch; the caller supplies
/// the bases and performs the restore. Decode: docs/org/multiplayer-rearm.md. ⚠ Keep it free of any
/// engine dependency beyond the vector struct, as <see cref="FlagMatch"/>: a unit suite runs it with
/// no engine.
/// </summary>
public sealed class RearmBases
{
    /// <summary>The squared radius in play when <c>player.zrd</c> authors no <c>rearm_rad</c>:
    /// <c>0x628f10</c>'s initialised 625.0, which the shipped file leaves alone.</summary>
    public const float InitialRadiusSquared = 625f;

    private readonly Dictionary<int, bool> _inside = new();

    /// <summary>A rule over bases of squared radius <paramref name="radiusSquared"/>.</summary>
    public RearmBases(RearmRule rule, float radiusSquared)
    {
        Rule = rule;
        RadiusSquared = radiusSquared;
    }

    /// <summary>Who the bases serve.</summary>
    public RearmRule Rule { get; }

    /// <summary>How close a pilot must come, in metres squared, the value compared at
    /// <c>0x49b9f4</c> and <c>0x49bb13</c>.</summary>
    public float RadiusSquared { get; }

    /// <summary>The rule for the lobby's mission type: a team's own base in Capture the Flag and
    /// Zeppelin vs Zeppelin, any base in either Deathmatch.</summary>
    public static RearmRule RuleFor(bool captureTheFlag, bool zeppelinVsZeppelin) =>
        captureTheFlag || zeppelinVsZeppelin ? RearmRule.OwnTeam : RearmRule.AnyBase;

    /// <summary>The node that stands for base <paramref name="index"/> (1-based):
    /// <c>zep_rearm_node_%d</c> on a Zeppelin vs Zeppelin map, else <c>rearm_node_%d</c>
    /// (<c>0x628f70</c>, <c>0x628f84</c>).</summary>
    public static string NodeName(int index, bool zeppelins) =>
        (zeppelins ? "zep_rearm_node_" : "rearm_node_") + index.ToString(CultureInfo.InvariantCulture);

    /// <summary><c>rearm_rad</c> squared, as <c>FUN_004735b0</c> stores it (<c>0x473fa1</c>), or
    /// <see cref="InitialRadiusSquared"/> for a file without the key.</summary>
    public static float ReadRadiusSquared(ZrdrDict player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.TryFloat("rearm_rad", out float radius) ? radius * radius : InitialRadiusSquared;
    }

    /// <summary><see cref="ReadRadiusSquared"/> of the <c>player.zrd</c> in
    /// <paramref name="zrdrPath"/>, else the initial value with the reason handed to
    /// <paramref name="warn"/>.</summary>
    public static float LoadRadiusSquared(string zrdrPath, Action<string>? warn = null)
    {
        try
        {
            if (Zrdr.LoadFile(zrdrPath, "player.json") is [List<object?> list, ..])
            {
                return ReadRadiusSquared(ZrdrDict.FromAlternating(list));
            }

            warn?.Invoke("player.zrd holds no root list");
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException)
        {
            warn?.Invoke(e.Message);
        }

        return InitialRadiusSquared;
    }

    /// <summary>Whether a base of <paramref name="baseTeam"/> serves a pilot of
    /// <paramref name="pilotTeam"/>. A pilot on no team (0) has no base under
    /// <see cref="RearmRule.OwnTeam"/>.</summary>
    public bool Serves(int baseTeam, int pilotTeam) =>
        Rule == RearmRule.AnyBase || (pilotTeam > 0 && baseTeam == pilotTeam);

    /// <summary>The nearest base that serves a pilot of <paramref name="pilotTeam"/> at
    /// <paramref name="at"/> and offers anything this step, or null when none does.</summary>
    public Vector3? NearestServing(Vector3 at, int pilotTeam, IReadOnlyList<RearmBase> bases)
    {
        ArgumentNullException.ThrowIfNull(bases);
        Vector3? nearest = null;
        float best = float.PositiveInfinity;
        foreach (var rearmBase in bases)
        {
            if (rearmBase.Position is { } position && Serves(rearmBase.Team, pilotTeam)
                && at.DistanceSquaredTo(position) < best)
            {
                best = at.DistanceSquaredTo(position);
                nearest = position;
            }
        }

        return nearest;
    }

    /// <summary>One living seat's step: true on the step it enters the radius of a base that serves
    /// it, which is when it rearms. It stays latched until it is outside every such base.</summary>
    public bool Enters(int seat, Vector3 at, int pilotTeam, IReadOnlyList<RearmBase> bases)
    {
        ArgumentNullException.ThrowIfNull(bases);
        bool inside = false;
        foreach (var rearmBase in bases)
        {
            inside |= rearmBase.Position is { } position && Serves(rearmBase.Team, pilotTeam)
                && at.DistanceSquaredTo(position) <= RadiusSquared;
        }

        bool was = _inside.TryGetValue(seat, out bool latched) && latched;
        _inside[seat] = inside;
        return inside && !was;
    }
}
