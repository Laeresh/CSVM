using System;
using System.Collections.Generic;
using CSVM.Flight.Modes;
using CSVM.Net;
using CSVM.Utils;
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

    /// <summary>The ribbon a started run flies: the zone's route, with a lead-in before its entry where
    /// the zone needs one (<see cref="StuntBotObjective.LeadInFor"/>).</summary>
    public DangerZoneRibbon? Ribbon { get; init; }
}

/// <summary>
/// One stunt race bot's way round the course, a remake-only rule, since the original seats no bots.
/// It takes next the open zone whose lead-in point is nearest, and holds it until a try ends or a
/// return. Between zones it steers freely for that point, short of the zone's nearer ribbon end.
/// Inside the approach cone it hands the zone to the pilot, which runs it on its <c>dzpathN</c>
/// ribbon as a campaign racer does. Its tier sets its cruise throttle and the odds of fluffing a
/// zone, passing over its entry and coming round again. Values: docs/org/aiPilot.md "Stunt race bots".
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

    /// <summary>The remake's own: the step and first length of a bot's lead-in, metres. A lead-in is a
    /// straight before a zone's entry on its first chord's line (<see cref="LeadInFor"/>).</summary>
    public const float LeadInStepM = 50f;

    /// <summary>The remake's own: the longest straight a bot's run may add before an entry, metres.
    /// Rule: docs/org/aiPilot.md "Stunt race bots".</summary>
    public const float MaxLeadInM = 300f;

    /// <summary>The remake's own: the fastest lock state <see cref="RailClears"/> flies from, m/s. A bot
    /// must lock no faster, or it outruns the check. Rule: docs/org/aiPilot.md "Stunt race bots".</summary>
    public const float TopLockSpeedMps = 135f;

    // The remake's own: how far below level the diving lock state RailClears flies points, degrees.
    private const float LockPitchDeg = 20f;

    // How long one checked rail run may last before it counts as a miss, sim steps.
    private const int RailCheckSteps = 3600;

    // The remake's own lock states RailClears flies from. The bearings off the ribbon's line stay in
    // the cone's half angle.
    private static readonly float[] LockBearingsDeg = { -30f, -15f, 0f, 15f, 30f };
    private static readonly float[] LockSpeedsMps = { 60f, 90f, TopLockSpeedMps };

    private readonly Random _rng;
    private int _zone = -1;
    private bool _farEnd;
    private DangerZoneRibbon? _route;
    private bool _fluffing;
    private bool _running;
    private HashSet<(DangerZoneRibbon, bool)>? _unlisted;

    /// <summary>A bot of tier <paramref name="tier"/> whose fluffs draw on <paramref name="rng"/>.</summary>
    public StuntBotObjective(NetBotSkill tier, Random rng)
    {
        Tier = tier;
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
    }

    public NetBotSkill Tier { get; }

    /// <summary>The run from each zone end, <see cref="RunsFrom"/>'s table, shared by every bot of a
    /// session. Without one every end runs its own route.</summary>
    public RunTable? Runs { get; init; }

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

    /// <summary>The straight a bot's run on <paramref name="zone"/> adds before the entry, its last
    /// vertex when <paramref name="far"/> and else its first, metres. It is 0 when <see cref="RailClears"/> passes without
    /// one, else the shortest step that passes and <paramref name="blocked"/> leaves clear. It is null
    /// when none up to <see cref="MaxLeadInM"/> does, so that end is refused.</summary>
    public static float? LeadInFor(StuntZone zone, bool far, Func<Vector3, Vector3, bool>? blocked)
    {
        var ribbon = zone.Route ?? throw new ArgumentException($"'{zone.PathName}' has no ribbon", nameof(zone));
        if (RailClears(zone, ribbon, far))
        {
            return 0f;
        }

        for (float metres = LeadInStepM; metres <= MaxLeadInM; metres += LeadInStepM)
        {
            var led = ribbon.WithLeadIn(far, metres);
            if (RailClears(zone, led, far))
            {
                // A longer straight holds this one, so a blocked straight ends the search.
                return blocked?.Invoke(led.End(far), ribbon.End(far)) == true ? null : metres;
            }
        }

        return null;
    }

    /// <summary>The ribbon a run flies from each end of each routed zone in <paramref name="zones"/>.
    /// It is the route with <see cref="LeadInFor"/>'s lead-in, or null where that end is refused. The
    /// decision does not depend on the bot, so a session makes it once for every bot.</summary>
    public static RunTable RunsFrom(IEnumerable<StuntZone> zones, Func<Vector3, Vector3, bool>? blocked)
    {
        var ends = new RunTable();
        foreach (var zone in zones)
        {
            if (zone.Route is not { } ribbon)
            {
                continue;
            }

            foreach (bool far in new[] { false, true })
            {
                ends[(ribbon, far)] = LeadInFor(zone, far, blocked) switch
                {
                    null => null,
                    > 0f and var metres => ribbon.WithLeadIn(far, metres),
                    _ => ribbon,
                };
            }
        }

        return ends;
    }

    /// <summary>Whether the rail alone carries a pilot through both of <paramref name="zone"/>'s gates
    /// on <paramref name="ribbon"/> from the end <paramref name="far"/> names. It must from every lock
    /// state just inside the lock range: each bearing, level and diving, at each speed.</summary>
    public static bool RailClears(StuntZone zone, DangerZoneRibbon ribbon, bool far)
    {
        var entry = ribbon.End(far);
        var line = ribbon.EndTangentInto(far).Normalized();
        var level = Flat(line).LengthSquared() > 1e-6f ? Flat(line).Normalized() : line;
        float cos = Mathf.Cos(Mathf.DegToRad(LockPitchDeg));
        float sin = Mathf.Sin(Mathf.DegToRad(LockPitchDeg));
        var dive = (level * cos) + (Vector3.Down * sin);
        foreach (float bearing in LockBearingsDeg)
        {
            var turn = new Basis(Vector3.Up, Mathf.DegToRad(bearing));
            foreach (var pitch in new[] { dive, level })
            {
                var heading = (turn * pitch).Normalized();
                foreach (float speed in LockSpeedsMps)
                {
                    if (!RailCarries(zone, ribbon, far, entry - (heading * (DangerZoneRibbon.LockRangeM - 1f)), heading, speed))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>The order for a bot at <paramref name="at"/> flying <paramref name="velocity"/> over
    /// <paramref name="zones"/>, its own run's. While <paramref name="zoneRun"/> says its pilot flies
    /// a ribbon, the order holds. A zone with no ribbon is passed over, so a bot
    /// never finishes a course that has one.</summary>
    public StuntBotOrder Decide(IReadOnlyList<StuntZone> zones, Vector3 at, Vector3 velocity, bool zoneRun)
    {
        ArgumentNullException.ThrowIfNull(zones);

        // ⚠ Do not pick again while the held zone is open and no try at it ended. Two zones about as
        // near would flip the pick each step, and a fluff would hand its zone over.
        bool pick = _running || _zone < 0 || _zone >= zones.Count || zones[_zone].Completed;
        int next = pick ? Nearest(zones, at) : _zone;
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

        // Each pick, including one after a try ends, takes its entry afresh: the ribbon end nearer the bot.
        if (pick)
        {
            _zone = next;
            _running = false;
            _fluffing = false;
            PickEnd(zones[next], at);
        }

        var entry = _route!.End(_farEnd);
        var into = Into(_route, _farEnd);
        float along = Flat(entry - at).Dot(into);
        if (_fluffing)
        {
            if (along > 0f)
            {
                return new StuntBotOrder(StuntBotLeg.Fluff, next, entry + (Vector3.Up * FluffAboveM), _farEnd, "fluffs the zone");
            }

            // Past the entry: the next try, from whichever end is nearer now.
            _fluffing = false;
            PickEnd(zones[next], at);
            entry = _route.End(_farEnd);
            into = Into(_route, _farEnd);
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
            return new StuntBotOrder(StuntBotLeg.Run, next, entry, _farEnd, "flies the zone") { StartsRun = true, Ribbon = _route };
        }

        var leadIn = entry - (into * LeadInM);
        float above = CruiseAboveM * Mathf.Clamp(Flat(leadIn - at).Length() / LeadInM, 0f, 1f);
        return new StuntBotOrder(StuntBotLeg.Steer, next, leadIn + (Vector3.Up * above), _farEnd, "steers for the zone");
    }

    /// <summary>A crash's return, or a rerun's: the next order picks its zone afresh, after any ride
    /// the return starts.</summary>
    public void Returned() => _zone = -1;

    private static Vector3 Flat(Vector3 v) => new(v.X, 0f, v.Z);

    private static bool NearerEnd(DangerZoneRibbon ribbon, Vector3 at)
        => at.DistanceSquaredTo(ribbon.End(true)) < at.DistanceSquaredTo(ribbon.End(false));

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

    // One rail run from a lock at `at`, flying `heading`: true once both gates have been crossed.
    private static bool RailCarries(StuntZone zone, DangerZoneRibbon ribbon, bool far, Vector3 at, Vector3 heading, float speed)
    {
        var run = new DangerZoneRun(ribbon, far);
        var rail = new DangerZoneRail(run, at, Basis.LookingAt(heading, Vector3.Up), heading * speed, speed);
        bool green = false;
        bool red = false;
        var last = at;
        for (int step = 0; step < RailCheckSteps && !(green && red) && rail.Step(GameClock.FixedDt); step++)
        {
            green |= zone.GreenGate.CrossedBy(last, rail.Position, out _);
            red |= zone.RedGate.CrossedBy(last, rail.Position, out _);
            last = rail.Position;
        }

        run.Release();
        return green && red;
    }

    // The open zone whose lead-in point is nearest, on the end and run a pick of it would take.
    private int Nearest(IReadOnlyList<StuntZone> zones, Vector3 at)
    {
        int best = -1;
        float bestSq = float.PositiveInfinity;
        for (int i = 0; i < zones.Count; i++)
        {
            if (zones[i].Completed || zones[i].Route is not { } ribbon)
            {
                continue;
            }

            var (far, route) = EndFrom(ribbon, at);
            float sq = at.DistanceSquaredTo(route.End(far) - (Into(route, far) * LeadInM));
            if (sq < bestSq)
            {
                best = i;
                bestSq = sq;
            }
        }

        return best;
    }

    private void PickEnd(StuntZone zone, Vector3 at) => (_farEnd, _route) = EndFrom(zone.Route!, at);

    // The ribbon end nearer `at`, or the other when that one is refused, and the route its run flies
    // from there. With both refused it is the nearer end's own route.
    private (bool Far, DangerZoneRibbon Route) EndFrom(DangerZoneRibbon ribbon, Vector3 at)
    {
        bool far = NearerEnd(ribbon, at);
        if (RunFrom(ribbon, far) == null && RunFrom(ribbon, !far) != null)
        {
            far = !far;
        }

        return (far, RunFrom(ribbon, far) ?? ribbon);
    }

    // The run from one end, null where it is refused.
    private DangerZoneRibbon? RunFrom(DangerZoneRibbon ribbon, bool far)
    {
        if (Runs == null)
        {
            return ribbon;
        }

        if (Runs.TryGetValue((ribbon, far), out var run))
        {
            return run;
        }

        // ⚠ Do not let an unlisted end pass quietly. It is a wiring fault that drops the lead-in.
        _unlisted ??= new();
        if (_unlisted.Add((ribbon, far)))
        {
            Log.Warn("flight", $"stunt race bot: '{ribbon.Name}' {(far ? "far" : "near")} end is not in the session's run table, so it runs its own route");
        }

        return ribbon;
    }

    /// <summary>The run from each zone end, keyed by the zone's route and the end:
    /// <see cref="RunsFrom"/>'s table.</summary>
    public sealed class RunTable : Dictionary<(DangerZoneRibbon Route, bool Far), DangerZoneRibbon?>
    {
    }
}
