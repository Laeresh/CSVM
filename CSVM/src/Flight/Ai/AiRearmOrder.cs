using System;
using System.Collections.Generic;
using CSVM.Flight.Weapons;
using Godot;

namespace CSVM.Flight.Ai;

/// <summary>Where a rearm run stands: none, flying to the approach gate, on the final leg through
/// the base, or restored and flying clear of it.</summary>
public enum AiRearmLeg : byte
{
    None = 0,
    Gate = 1,
    Final = 2,
    Clear = 3,
}

/// <summary>
/// A bot's rearm standing order: out of rockets or badly damaged, it breaks off to the nearest base
/// serving it. Once restored and clear of the base it hands back to combat. A base is a fly-through
/// bay, so the run flies a gate out on the bay's open side, then a final leg through the node. The
/// base runtime feeds <see cref="Update"/> and <see cref="AiPilot"/> flies <see cref="Aim"/>. Approach
/// and the measured bay: docs/org/multiplayer-rearm.md, "Bots at a base"; engine-free beyond the vector struct.
/// </summary>
public sealed class AiRearmOrder
{
    /// <summary>TUNE, a remake value: the original has no computer pilots. The share of whole-vehicle
    /// health, the pool the death test reads, at or below which a run starts.</summary>
    public const float DamagedHullShare = 0.35f;

    /// <summary>How far out along the open bearing the gate stands, metres: the final leg's length.</summary>
    public const float FinalLegM = 2500f;

    /// <summary>The last stretch of the final leg, metres, flown level at the node's height. The
    /// avoid-crash lookahead, about 500 m at cruise, reads any descent there as the bay's floor.</summary>
    public const float LevelLegM = 700f;

    /// <summary>How far above the node the gate stands, metres; the leg descends to the node's height
    /// before <see cref="LevelLegM"/>.</summary>
    public const float GateAboveM = 120f;

    /// <summary>How far ahead along the leg the pilot aims, metres. It closes on the line without the
    /// hunting a point aim gives the control law's sign-relay roll.</summary>
    public const float LeadM = 400f;

    /// <summary>How far from the node, horizontally, a restored pilot hands back to combat, metres.</summary>
    public const float ClearM = 150f;

    /// <summary>How far past the node a restored pilot holds the node's height before it climbs out,
    /// metres. The bay's roof stands about 28 m over the node.</summary>
    public const float ClearLevelM = 60f;

    /// <summary>How far above the node a restored pilot climbs out to, metres.</summary>
    public const float ClearClimbM = 150f;

    /// <summary>How far past the node an unrestored pilot may fly before the run is planned afresh,
    /// metres: a pass that missed the radius.</summary>
    public const float MissM = 150f;

    /// <summary>How many bearings the open-side search tries, evenly round the compass.</summary>
    public const int Bearings = 24;

    // The corridor a pilot joins the final leg in: inside 10 degrees of the leg either side, capped
    // at this many metres. It reaches no further out than the gate plus the cap.
    private const float CorridorSlope = 0.176f;
    private const float CorridorCapM = 250f;

    // How near the inbound course a pilot's track must lie to join the final leg: within 60 degrees.
    private const float InboundCos = 0.5f;

    // A base that moved this far is planned afresh. The radius of a base, so a pass is still aimed.
    private const float ReplanM = 25f;

    private readonly Func<Vector3, Vector3, bool>? _blocks;

    /// <summary>An order whose open-side search asks <paramref name="worldBlocks"/> whether static
    /// world blocks a segment. Without one, the run approaches along the pilot's own bearing.</summary>
    public AiRearmOrder(Func<Vector3, Vector3, bool>? worldBlocks = null)
    {
        _blocks = worldBlocks;
    }

    /// <summary>Where the run stands.</summary>
    public AiRearmLeg Leg { get; private set; }

    /// <summary>Whether a run stands: the pilot flies <see cref="Aim"/> and its gunner takes no quarry.</summary>
    public bool Flying => Leg != AiRearmLeg.None;

    /// <summary>The node the run flies to.</summary>
    public Vector3 Base { get; private set; }

    /// <summary>The horizontal unit bearing from the node out along the open side the run comes in on.</summary>
    public Vector3 Approach { get; private set; }

    /// <summary>Why the standing run started, for the log.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Whether the supplies call for a run: every rocket pylon empty, or the hull at or under
    /// <see cref="DamagedHullShare"/>. The guns never call for one, since a bot's guns outlast its
    /// rockets by far and a person flies back only for rockets (docs/org/multiplayer-rearm.md).</summary>
    public static bool Wants(bool rocketsOut, float hullShare) =>
        rocketsOut || hullShare <= DamagedHullShare;

    /// <summary>Whether every pylon is empty, whatever it carries: the game names all pylon ordnance
    /// rockets, and a bot launches every pylon alike. A pylon with no capacity carries nothing and is
    /// skipped. False with no loaded pylon at all, or with infinite ammunition, since neither runs out.</summary>
    public static bool RocketsOut(IEnumerable<IAmmoSlot> pylons, bool infinite)
    {
        ArgumentNullException.ThrowIfNull(pylons);
        if (infinite)
        {
            return false;
        }

        bool carries = false;
        foreach (var pylon in pylons)
        {
            if (pylon.Capacity <= 0)
            {
                continue;
            }

            if (pylon.Ammo > 0)
            {
                return false;
            }

            carries = true;
        }

        return carries;
    }

    /// <summary>Bearing <paramref name="index"/> of <paramref name="count"/>, as a horizontal unit
    /// vector: the nose of a heading of <c>index * 360 / count</c> degrees, the mission-data convention.
    /// A fractional index lies between two bearings.</summary>
    public static Vector3 BearingDir(float index, int count)
    {
        float rad = Mathf.DegToRad(index * 360f / count);
        return new Vector3(-Mathf.Sin(rad), 0f, -Mathf.Cos(rad));
    }

    /// <summary>The open side a run comes in on. It starts from the bearing with a clear
    /// <paramref name="leg"/> nearest <paramref name="want"/>. It moves to the middle of the unbroken
    /// arc of <paramref name="bay"/> bearings around it, the bay's axis, when that middle's leg is
    /// clear too. With no clear leg it is <paramref name="want"/> itself.</summary>
    public static Vector3 OpenBearing(IReadOnlyList<bool> bay, IReadOnlyList<bool> leg, Vector3 want)
    {
        ArgumentNullException.ThrowIfNull(bay);
        ArgumentNullException.ThrowIfNull(leg);
        var flat = new Vector3(want.X, 0f, want.Z);
        flat = flat.LengthSquared() > 1e-6f ? flat.Normalized() : Vector3.Back;
        int n = Math.Min(bay.Count, leg.Count);
        int best = -1;
        float bestDot = float.NegativeInfinity;
        int open = 0;
        for (int i = 0; i < n; i++)
        {
            open += bay[i] ? 1 : 0;
            float dot = BearingDir(i, n).Dot(flat);
            if (bay[i] && leg[i] && dot > bestDot)
            {
                bestDot = dot;
                best = i;
            }
        }

        if (best < 0)
        {
            return flat;
        }

        if (open == n)
        {
            return BearingDir(best, n);
        }

        int lo = 0;
        while (bay[(best - lo - 1 + n) % n])
        {
            lo++;
        }

        int hi = 0;
        while (bay[(best + hi + 1) % n])
        {
            hi++;
        }

        // Half-steps, so an arc of an even count of bearings centres between two of them.
        float middle = best + ((hi - lo) / 2f);
        int below = ((int)MathF.Floor(middle) + n) % n;
        int above = ((int)MathF.Ceiling(middle) + n) % n;
        if (!leg[below] || !leg[above])
        {
            return BearingDir(best, n);
        }

        return BearingDir(middle, n);
    }

    /// <summary>One step's decision, fed by the base runtime. The pilot stands at
    /// <paramref name="at"/> flying <paramref name="velocity"/>, the nearest base serving it is
    /// <paramref name="nearestBase"/> (null when none offers), and <paramref name="restored"/> says
    /// a base restored it on this step.</summary>
    public void Update(Vector3 at, Vector3 velocity, bool rocketsOut, float hullShare, Vector3? nearestBase,
        bool restored)
    {
        switch (Leg)
        {
            case AiRearmLeg.None:
                if (nearestBase is { } found && Wants(rocketsOut, hullShare))
                {
                    Reason = Why(rocketsOut, hullShare);
                    Plan(at, velocity, found);
                }

                break;

            case AiRearmLeg.Gate:
            case AiRearmLeg.Final:
                if (restored)
                {
                    Leg = AiRearmLeg.Clear;
                }
                else if (nearestBase is not { } standing)
                {
                    Leg = AiRearmLeg.None;
                }
                else if (standing.DistanceTo(Base) > ReplanM || (Leg == AiRearmLeg.Final && Along(at) < -MissM))
                {
                    Plan(at, velocity, standing);
                }
                else if (Leg == AiRearmLeg.Gate && InCorridor(at, velocity))
                {
                    Leg = AiRearmLeg.Final;
                }

                break;

            case AiRearmLeg.Clear:
                if (Horizontal(at - Base).Length() > ClearM)
                {
                    Leg = AiRearmLeg.None;
                }

                break;
        }
    }

    /// <summary>Drops any run outright, the return to a fresh airframe.</summary>
    public void Reset()
    {
        Leg = AiRearmLeg.None;
        Reason = string.Empty;
    }

    /// <summary>The point the pilot at <paramref name="at"/> flies at: the gate, or a point
    /// <see cref="LeadM"/> ahead along the leg, through the node and on past it.</summary>
    public Vector3 Aim(Vector3 at)
    {
        if (Leg == AiRearmLeg.None)
        {
            return at;
        }

        if (Leg == AiRearmLeg.Gate)
        {
            return Base + (Approach * FinalLegM) + (Vector3.Up * GateAboveM);
        }

        float along = Along(at);
        float ahead = along - LeadM;
        var aim = Base + (Approach * ahead);
        aim.Y = Leg == AiRearmLeg.Clear
            ? Base.Y + (along > -ClearLevelM ? 0f : ClearClimbM)
            : Base.Y + (GateAboveM * Mathf.Clamp((ahead - LevelLegM) / (FinalLegM - LevelLegM), 0f, 1f));
        return aim;
    }

    private static Vector3 Horizontal(Vector3 v) => new(v.X, 0f, v.Z);

    // The trigger that fired, both when both did; the hull's share only when it is the reason.
    private static string Why(bool rocketsOut, float hullShare)
    {
        string hull = FormattableString.Invariant($"hull {hullShare:0.00}");
        if (!rocketsOut)
        {
            return hull;
        }

        return hullShare <= DamagedHullShare ? "rockets out, " + hull : "rockets out";
    }

    // A new run, or the same one planned afresh: the open side nearest the pilot, then the gate.
    // A pilot already in the final leg's corridor joins the leg at once. The bay is the level
    // stretch from the node; the leg adds the climb from there up to the gate.
    private void Plan(Vector3 at, Vector3 velocity, Vector3 node)
    {
        Base = node;
        var want = Horizontal(at - node);
        var bay = new bool[Bearings];
        var leg = new bool[Bearings];
        for (int i = 0; _blocks is { } blocks && i < Bearings; i++)
        {
            var dir = BearingDir(i, Bearings);
            var level = node + (dir * LevelLegM);
            bay[i] = !blocks(node, level);
            leg[i] = bay[i] && !blocks(level, node + (dir * FinalLegM) + (Vector3.Up * GateAboveM));
        }

        Approach = OpenBearing(bay, leg, want);
        Leg = InCorridor(at, velocity) ? AiRearmLeg.Final : AiRearmLeg.Gate;
    }

    // Metres along the leg from the node, positive on the approach side.
    private float Along(Vector3 at) => Horizontal(at - Base).Dot(Approach);

    // Inside the corridor and flying inbound, so a pilot crossing it outbound turns back to the gate.
    private bool InCorridor(Vector3 at, Vector3 velocity)
    {
        float along = Along(at);
        float off = (Horizontal(at - Base) - (Approach * along)).Length();
        var track = Horizontal(velocity);
        return along > 0f && along <= FinalLegM + CorridorCapM
            && off <= Mathf.Min(along * CorridorSlope, CorridorCapM)
            && track.Dot(-Approach) >= InboundCos * track.Length();
    }
}
