using System;
using System.Collections.Generic;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using Godot;

namespace CSVM.Flight.Modes;

/// <summary>One zeppelin part that died in Zeppelin vs Zeppelin. It names the hull by placement index
/// and the gas bag the loss counts toward. It says whether a broadside cannon died rather than the
/// bag, and which seat's hit killed it, -1 for none.</summary>
public readonly record struct HullPartLoss(int Hull, string Gasbag, bool Cannon, int Seat);

/// <summary>
/// Zeppelin vs Zeppelin's rules, engine-free: the two sides, each flying one hull, and what a dead
/// part scores. Hull 0 flies the first lobby team of the seat order and hull 1 the second, as
/// <c>FUN_00496490</c> reads <c>0x71c7b4</c>/<c>0x71c7b8</c>. A gas bag scores once, at its own death or
/// at the first death of a broadside cannon bound to it. The respawn is <c>FUN_004969b0</c>'s mode 4
/// branch. Decode: docs/org/multiplayer-zvz.md.
/// </summary>
public sealed class ZeppelinVersus
{
    /// <summary>The respawn's altitude floor, and its whole altitude over lower ground
    /// (<c>0x603730</c>, the flat 900 m at <c>0x496b75</c>).</summary>
    public const float RespawnFloor = 900f;

    /// <summary>The bearing step per pilot index, the float at <c>0x608168</c>.</summary>
    public const float BearingStep = 0.7854f;

    /// <summary>The respawn's yaw is this less the bearing, the double at <c>0x608160</c>.</summary>
    public const float HeadingBase = 1.5708f;

    private readonly int[] _teams;
    private readonly MatchScores _scores;
    private readonly HashSet<(int Hull, string Gasbag)> _scored = new();

    /// <summary>A match whose hull 0 flies <paramref name="firstTeam"/> and hull 1
    /// <paramref name="secondTeam"/>, lobby team numbers, its gas bags worth what
    /// <paramref name="scores"/> says (<see cref="MatchScores.Fallback"/> when none is given).</summary>
    public ZeppelinVersus(int firstTeam, int secondTeam, MatchScores? scores = null)
    {
        _teams = new[] { Math.Max(0, firstTeam), Math.Max(0, secondTeam) };
        _scores = scores ?? MatchScores.Fallback;
    }

    /// <summary>The shooter id a hull's broadside rounds carry in a match, so a pilot they down is
    /// charged to that hull's side. Below <see cref="ProjectilePool.NoShooter"/>, clear of every
    /// seat and AI id, and by placement index.</summary>
    public static int BroadsideShooter(int hull) => ProjectilePool.NoShooter - 1 - hull;

    /// <summary>The hull whose broadside fired a round carrying <paramref name="shooter"/>, or -1 for
    /// any other shooter id.</summary>
    public static int HullOfShooter(int shooter) =>
        shooter < ProjectilePool.NoShooter ? ProjectilePool.NoShooter - 1 - shooter : -1;

    /// <summary>The first two distinct lobby teams in seat order, 0 where fewer are flying. Any
    /// further team flies without a hull.</summary>
    public static (int First, int Second) Sides(IReadOnlyList<int> seatTeams)
    {
        ArgumentNullException.ThrowIfNull(seatTeams);
        int first = 0;
        foreach (int team in seatTeams)
        {
            if (team <= 0 || team == first)
            {
                continue;
            }

            if (first == 0)
            {
                first = team;
            }
            else
            {
                return (first, team);
            }
        }

        return (first, 0);
    }

    /// <summary>The <c>net.zrd</c> block each seat opens and returns in. The first side takes block 1,
    /// the second block 2, and a seat on neither the shared block 0. The map lays block N round hull
    /// N-1.</summary>
    public static int[] SpawnBlocks(IReadOnlyList<int> seatTeams)
    {
        ArgumentNullException.ThrowIfNull(seatTeams);
        var (first, second) = Sides(seatTeams);
        var blocks = new int[seatTeams.Count];
        for (int seat = 0; seat < blocks.Length; seat++)
        {
            int team = seatTeams[seat];
            blocks[seat] = team <= 0 ? 0 : team == first ? 1 : team == second ? 2 : 0;
        }

        return blocks;
    }

    /// <summary>Where a downed pilot comes back: halfway between the other aircraft's centroid and its
    /// own hull. It moves out along the pilot index's bearing by <paramref name="radius"/> and faces
    /// <see cref="HeadingBase"/> less that bearing. It stands <paramref name="margin"/> over ground
    /// above <see cref="RespawnFloor"/>, else at the floor. No hull means the centroid alone, and no
    /// other aircraft means no point.</summary>
    public static SpawnPoint? RespawnPoint(IReadOnlyList<Vector3> others, Vector3? hull, int pilotIndex,
        float radius, float margin, Func<Vector3, float?>? groundAt)
    {
        ArgumentNullException.ThrowIfNull(others);
        if (others.Count == 0)
        {
            return null;
        }

        float x = 0f, z = 0f;
        foreach (var at in others)
        {
            x += at.X;
            z += at.Z;
        }

        float count = others.Count;
        if (hull is { } own)
        {
            x += count * own.X;
            z += count * own.Z;
            count *= 2f;
        }

        float bearing = pilotIndex * BearingStep;
        x = (x / count) + (Mathf.Cos(bearing) * radius);
        z = (z / count) + (Mathf.Sin(bearing) * radius);
        float y = groundAt?.Invoke(new Vector3(x, 0f, z)) is float ground && ground > RespawnFloor
            ? ground + margin
            : RespawnFloor;
        return new SpawnPoint(new Vector3(x, y, z), Mathf.RadToDeg(HeadingBase - bearing));
    }

    /// <summary>A hull's target marker by side, <c>FUN_0049b4c0</c>. The name line is its team's name.
    /// The category reads "Defend" (row 8001) to its own side and "Destroy" (row 8002) to the other.
    /// It replaces the <c>targets.zrd</c> lines, which call one hull the enemy on every machine. So a
    /// hull is blue to its side and red to the other. <paramref name="sideTeam"/> is its hostility id.
    /// </summary>
    public static SiteSide HullSide(int sideTeam, string teamName, Messages? strings) =>
        new(sideTeam, teamName, strings?.Get("MSG_OBJ_DEFEND") ?? "MSG_OBJ_DEFEND",
            strings?.Get("MSG_OBJ_DESTROY") ?? "MSG_OBJ_DESTROY");

    /// <summary>The lobby team hull <paramref name="hull"/> flies, 0 for none. A third hull would
    /// wrap onto the sides, as the original's <c>i &amp; 1</c> does.</summary>
    public int TeamOfHull(int hull) => hull >= 0 ? _teams[hull & 1] : 0;

    /// <summary>The hull <paramref name="team"/> flies, -1 for a team on neither side.</summary>
    public int HullOf(int team) => team <= 0 ? -1 : team == _teams[0] ? 0 : team == _teams[1] ? 1 : -1;

    /// <summary>The other side's team, 0 for a team on neither side.</summary>
    public int OtherTeam(int team) => HullOf(team) is var hull and >= 0 ? _teams[1 - hull] : 0;

    /// <summary>Whether <paramref name="loss"/> is its gas bag's scoring event, the first death that
    /// counts toward that bag, and marks it spent. The gas bag record's <c>+0x11</c> byte, set by the
    /// bag's own death (<c>0x4c074a</c>) and by its cannon's (<c>0x4c099f</c>).</summary>
    public bool Counts(HullPartLoss loss)
    {
        ArgumentNullException.ThrowIfNull(loss.Gasbag);
        return _scored.Add((loss.Hull, loss.Gasbag.ToLowerInvariant()));
    }

    /// <summary>What a counting loss scores its killer on <paramref name="killerTeam"/>: its own
    /// hull's bag costs, any other scores (<c>FUN_0049b740</c>). Nothing without a killer.</summary>
    public int Points(HullPartLoss loss, int killerTeam) =>
        loss.Seat < 0 ? 0
        : killerTeam > 0 && killerTeam == TeamOfHull(loss.Hull) ? _scores.OwnGasbagKill
        : _scores.GasbagKill;
}
