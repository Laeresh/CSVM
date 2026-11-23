using System.Collections.Generic;
using Godot;

namespace CSVM.Session.Career;

/// <summary>When an enemy flight joins the sortie: at the start, or with the one reserve wave.</summary>
public enum EnemyWave
{
    Initial,
    Reserve,
}

/// <summary>What releases the reserve wave.</summary>
public enum ReserveTrigger
{
    /// <summary>A delay after the start, <see cref="MissionSpec.ReserveDelaySeconds"/>.</summary>
    TimeElapsed,

    /// <summary>The first placed ground target is destroyed.</summary>
    FirstTargetDestroyed,
}

/// <summary>What an objective asks.</summary>
public enum ObjectiveKind
{
    /// <summary>Destroy every enemy aircraft of one wave.</summary>
    DestroyWave,

    /// <summary>Destroy every placed ground target.</summary>
    DestroyTargets,
}

/// <summary>Pilot skill of an enemy flight, lowest first. The AI's parameters map from it on the
/// flight side; the generator uses it only to value the plane.</summary>
public enum SkillTier
{
    Rookie,
    Regular,
    Veteran,
    Ace,
}

/// <summary>One enemy aircraft: its stock airframe id, its pilot's skill, its start and heading,
/// its patrol route if any, and its wave. Its value is what it cost the budget.</summary>
public readonly record struct EnemyFlight(int Airframe, SkillTier Skill, Vector3 Position, float HeadingDeg, string? Route, EnemyWave Wave, int Value);

/// <summary>One ground target to place: a prefab id and the ground point under it. The placer
/// finds the height and refuses a point the built world rejects.</summary>
public readonly record struct TargetPlacement(string Prefab, float X, float Z, int Value);

/// <summary>One objective. A primary one must be met for the contract to pay; a secondary one
/// pays extra.</summary>
public readonly record struct MissionObjective(ObjectiveKind Kind, bool Primary, EnemyWave Wave = EnemyWave.Initial);

/// <summary>
/// A generated sortie, engine-free: the one input the session needs to build it. It names the
/// player's start, the wingman seats, every enemy aircraft, every ground target, the objectives
/// and the reserve wave's trigger. The same contract seed always yields the same spec.
/// </summary>
public sealed class MissionSpec
{
    public required JobType Job { get; init; }

    public required int Threat { get; init; }

    public required ulong Seed { get; init; }

    /// <summary>The area's centre: the route's centre on a patrol, the target cluster's on a strike.</summary>
    public required Vector3 Centre { get; init; }

    public required Vector3 PlayerStart { get; init; }

    public required float PlayerHeadingDeg { get; init; }

    /// <summary>Wingman seats the flight check may fill, zero to three.</summary>
    public required int WingmanSeats { get; init; }

    /// <summary>The patrol route the flight is sent along, or null on a strike.</summary>
    public string? Route { get; init; }

    public required IReadOnlyList<EnemyFlight> Enemies { get; init; }

    public required IReadOnlyList<TargetPlacement> Targets { get; init; }

    public required IReadOnlyList<MissionObjective> Objectives { get; init; }

    public required ReserveTrigger ReserveTrigger { get; init; }

    public required int ReserveDelaySeconds { get; init; }

    /// <summary>The opposition budget the threat rating gave, and how much of it was spent.</summary>
    public required int Budget { get; init; }

    public required int Spent { get; init; }
}
