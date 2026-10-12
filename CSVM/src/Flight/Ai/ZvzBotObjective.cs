using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Flight.Ai;

/// <summary>The classes of hull part a bot attacks in Zeppelin vs Zeppelin. The first three are
/// its guns' order, and the gas bag is its torpedoes' target.</summary>
public enum HullPartKind
{
    Cannon,
    Turret,
    Engine,
    Gasbag,
}

/// <summary>One part of a hull as <see cref="ZvzBotObjective"/> reads it: the target to hand the
/// gunner, its class, where it stands and whether it still stands.</summary>
public readonly record struct HullPart(object Source, HullPartKind Kind, Vector3 Position, bool Live);

/// <summary>One enemy aircraft as <see cref="ZvzBotObjective"/> reads it, and whether it has been
/// shooting at the bot's own hull.</summary>
public readonly record struct EnemyPilot(object Source, Vector3 Position, Vector3 Velocity, bool AttacksOwnHull);

/// <summary>
/// A bot's target in Zeppelin vs Zeppelin each step, a remake-only rule: the original seats no bots.
/// The order is its own hull's attacker, an enemy on its tail inside its attack radius (the remake's
/// limit), then the other hull. Only a torpedo damages a gas bag, so a bot
/// carrying one stays on the nearest live gas bag, between launches too. Otherwise it takes the
/// broadside cannons, then the turrets, then the engines, nearest first. Broadside arcs, aimed at the
/// other hull, are not avoided.
/// </summary>
public static class ZvzBotObjective
{
    private static readonly (HullPartKind Kind, string Why)[] GunOrder =
    {
        (HullPartKind.Cannon, "attacks a cannon"),
        (HullPartKind.Turret, "attacks a turret"),
        (HullPartKind.Engine, "attacks an engine"),
    };

    /// <summary>The target for a bot at <paramref name="position"/> flying nose
    /// <paramref name="nose"/>, <paramref name="torpedoes"/> whether it carries one. Null leaves the
    /// gunner its own ranking. The bot keeps
    /// <paramref name="current"/> while the rule that chose it still offers it. A row of cannons is
    /// then fought one at a time. <paramref name="why"/> names the rule.</summary>
    public static object? Pick(Vector3 position, Vector3 nose, float attackRange, bool torpedoes,
        IReadOnlyList<EnemyPilot> enemies, IReadOnlyList<HullPart> enemyHull, object? current, out string why)
    {
        ArgumentNullException.ThrowIfNull(enemies);
        ArgumentNullException.ThrowIfNull(enemyHull);
        object? best = null;
        float bestSq = float.MaxValue;
        foreach (var enemy in enemies)
        {
            Closer(enemy.AttacksOwnHull, enemy.Source, enemy.Position);
        }

        why = "defends its hull";
        if (best != null)
        {
            return best;
        }

        foreach (var enemy in enemies)
        {
            Closer(position.DistanceSquaredTo(enemy.Position) <= attackRange * attackRange
                && AiModeMachine.IsPursuedBy(nose, position, enemy.Position, enemy.Velocity), enemy.Source, enemy.Position);
        }

        why = "turns on its pursuer";
        if (best != null)
        {
            return best;
        }

        if (torpedoes && Nearest(HullPartKind.Gasbag) is { } gasbag)
        {
            why = "torpedoes a gas bag";
            return gasbag;
        }

        foreach (var (kind, reason) in GunOrder)
        {
            if (Nearest(kind) is { } part)
            {
                why = reason;
                return part;
            }
        }

        why = "has no hull part left";
        return null;

        object? Nearest(HullPartKind kind)
        {
            foreach (var part in enemyHull)
            {
                Closer(part.Live && part.Kind == kind, part.Source, part.Position);
            }

            return best;
        }

        void Closer(bool eligible, object source, Vector3 at)
        {
            float d = ReferenceEquals(source, current) ? -1f : position.DistanceSquaredTo(at);
            if (eligible && d < bestSq)
            {
                best = source;
                bestSq = d;
            }
        }
    }
}
