using System;
using System.Collections.Generic;
using CSVM.Flight.Modes;
using CSVM.Net;
using Godot;

namespace CSVM.Flight.Ai;

/// <summary>What a stunt race bot does this step, in <see cref="StuntBotObjective"/>'s words.</summary>
public enum StuntBotLeg
{
    /// <summary>No zone is left that it can fly, and it holds its course.</summary>
    None,

    /// <summary>It steers freely for the lead-in point short of the next zone's entry.</summary>
    Steer,

    /// <summary>It flies the zone's ribbon, from the approach through the rail to the exit.</summary>
    Run,

    /// <summary>It fluffed the zone: it passes over the entry and comes round to fly it again.</summary>
    Fluff,
}

/// <summary>A stunt race bot's order: its leg, the course index of its zone and the point it steers
/// for. A run also names the ribbon end it enters from.</summary>
public readonly record struct StuntBotOrder(StuntBotLeg Leg, int Zone, Vector3 Point, bool FarEnd, string Why)
{
    /// <summary>Whether this step starts a run, which the pilot takes as its danger-zone run.</summary>
    public bool StartsRun { get; init; }
}

/// <summary>
/// One stunt race bot's way round the course, a remake-only rule, since the original seats no bots.
/// It takes the zones in course order. Between zones it steers freely for a lead-in point short of
/// the next zone's nearer ribbon end. Inside the approach cone it hands the zone to the pilot, which
/// runs it on its <c>dzpathN</c> ribbon as a campaign racer does. Its tier sets its cruise throttle
/// and the odds of fluffing a zone, passing over its entry and coming round again. Values:
/// docs/org/aiPilot.md "Stunt race bots".
/// </summary>
public sealed class StuntBotObjective
{
    /// <summary>The remake's own: how far short of a zone's entry, along its ribbon, the lead-in
    /// point stands, metres.</summary>
    public const float LeadInM = 600f;

    /// <summary>The remake's own: how far out from the entry the approach cone reaches, metres.</summary>
    public const float ConeReachM = 900f;

    /// <summary>The remake's own: the cone's half angle about the ribbon's line into the entry, degrees.
    /// The bot's track must also point within it of the entry.</summary>
    public const float ConeHalfAngleDeg = 30f;

    /// <summary>The remake's own: how far above the entry the bot cruises while far from the lead-in
    /// point, metres. It comes down to the entry's height over the last <see cref="LeadInM"/>.</summary>
    public const float CruiseAboveM = 150f;

    /// <summary>The remake's own: how far above a fluffed zone's entry the bot passes, metres.</summary>
    public const float FluffAboveM = 100f;

    private readonly Random _rng;
    private int _zone = -1;
    private bool _farEnd;
    private bool _fluffing;
    private bool _running;

    /// <summary>A bot of tier <paramref name="tier"/> whose fluffs draw on <paramref name="rng"/>.</summary>
    public StuntBotObjective(NetBotSkill tier, Random rng)
    {
        Tier = tier;
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
    }

    public NetBotSkill Tier { get; }

    /// <summary>The bot's lever between zones: the remake's own, full for an ace, 0.85 for a
    /// veteran and 0.7 for a novice.</summary>
    public float CruiseThrottle => Tier switch
    {
        NetBotSkill.Ace => 1f,
        NetBotSkill.Veteran => 0.85f,
        _ => 0.7f,
    };

    /// <summary>The odds of fluffing each try at a zone: the remake's own, 0.05 for an ace, 0.2 for a
    /// veteran and 0.35 for a novice.</summary>
    public float FluffChance => Tier switch
    {
        NetBotSkill.Ace => 0.05f,
        NetBotSkill.Veteran => 0.2f,
        _ => 0.35f,
    };

    /// <summary>The order for a bot at <paramref name="at"/> flying <paramref name="velocity"/> over
    /// <paramref name="zones"/>, its own run's. While <paramref name="zoneRun"/> says its pilot flies
    /// a ribbon, the order holds. A zone with no ribbon is passed over, so a bot
    /// never finishes a course that has one.</summary>
    public StuntBotOrder Decide(IReadOnlyList<StuntZone> zones, Vector3 at, Vector3 velocity, bool zoneRun)
    {
        ArgumentNullException.ThrowIfNull(zones);
        int next = -1;
        for (int i = 0; i < zones.Count && next < 0; i++)
        {
            if (!zones[i].Completed && zones[i].Route != null)
            {
                next = i;
            }
        }

        if (next < 0)
        {
            _zone = -1;
            return new StuntBotOrder(StuntBotLeg.None, -1, at, false, "has no zone left to fly");
        }

        if (zoneRun)
        {
            _running = true;
            return new StuntBotOrder(StuntBotLeg.Run, _zone, at, _farEnd, "flies the zone");
        }

        var ribbon = zones[next].Route!;
        // A new zone, or a try that ended, picks its entry afresh: the ribbon end nearer the bot.
        if (next != _zone || _running)
        {
            _zone = next;
            _running = false;
            _fluffing = false;
            _farEnd = at.DistanceSquaredTo(ribbon.End(true)) < at.DistanceSquaredTo(ribbon.End(false));
        }

        var entry = ribbon.End(_farEnd);
        var into = Into(ribbon, _farEnd);
        float along = Flat(entry - at).Dot(into);
        if (_fluffing)
        {
            if (along > 0f)
            {
                return new StuntBotOrder(StuntBotLeg.Fluff, next, entry + (Vector3.Up * FluffAboveM), _farEnd, "fluffs the zone");
            }

            // Past the entry: the next try, from whichever end is nearer now.
            _fluffing = false;
            _farEnd = at.DistanceSquaredTo(ribbon.End(true)) < at.DistanceSquaredTo(ribbon.End(false));
            entry = ribbon.End(_farEnd);
            into = Into(ribbon, _farEnd);
            along = Flat(entry - at).Dot(into);
        }

        if (InCone(entry, into, along, at, velocity))
        {
            if (_rng.NextDouble() < FluffChance)
            {
                _fluffing = true;
                return new StuntBotOrder(StuntBotLeg.Fluff, next, entry + (Vector3.Up * FluffAboveM), _farEnd, "fluffs the zone");
            }

            _running = true;
            return new StuntBotOrder(StuntBotLeg.Run, next, entry, _farEnd, "flies the zone") { StartsRun = true };
        }

        var leadIn = entry - (into * LeadInM);
        float above = CruiseAboveM * Mathf.Clamp(Flat(leadIn - at).Length() / LeadInM, 0f, 1f);
        return new StuntBotOrder(StuntBotLeg.Steer, next, leadIn + (Vector3.Up * above), _farEnd, "steers for the zone");
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0f, v.Z);

    private static Vector3 Into(DangerZoneRibbon ribbon, bool farEnd)
    {
        var into = Flat(ribbon.EndTangentInto(farEnd));
        return into.LengthSquared() > 1e-6f ? into.Normalized() : Vector3.Zero;
    }

    // Short of the entry inside the cone's reach and angle, and tracking toward it.
    private static bool InCone(Vector3 entry, Vector3 into, float along, Vector3 at, Vector3 velocity)
    {
        if (into.LengthSquared() < 1e-6f || along <= DangerZoneRibbon.LockRangeM || along > ConeReachM)
        {
            return false;
        }

        var toEntry = Flat(entry - at);
        float off = (toEntry - (into * along)).Length();
        float tan = Mathf.Tan(Mathf.DegToRad(ConeHalfAngleDeg));
        var track = Flat(velocity);
        return off <= along * tan
            && track.Dot(toEntry) >= Mathf.Cos(Mathf.DegToRad(ConeHalfAngleDeg)) * track.Length() * toEntry.Length();
    }
}
