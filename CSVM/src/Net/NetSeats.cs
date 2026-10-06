using System;
using System.Collections.Generic;

namespace CSVM.Net;

/// <summary>
/// The seat roster's own rules. How many pilots a match admits, how wide every seat-indexed table
/// is, each seat's identity colour, and what makes a roster well formed. The ceiling and the table
/// width are two numbers on purpose. Lowering the ceiling is then this one constant, and raising
/// the width is the data's block size, rather than a sweep of every array.
/// </summary>
public static class NetSeats
{
    /// <summary>Pilots one match admits. The original codes no cap at all; its lobby shows
    /// "Players (1 of 16)" and its data holds 16. Its authored colour table and respawn fan serve
    /// eight, so seats past that take derived colours and a wrapped fan
    /// (<c>docs/org/multiplayer-spawn.md</c>).</summary>
    public const int MaxPlayers = 16;

    /// <summary>The peer every seat of a local match's roster is owned by, its bots included. No
    /// transport hands it out, and with no wire nothing reads it but <see cref="Validate"/>.
    /// </summary>
    public const int OfflinePeer = NetSession.NoPeer;

    /// <summary>How wide every seat-indexed table is built, never below <see cref="MaxPlayers"/>.
    /// Sixteen is what the original's own data holds: 16-entry spawn blocks, a 16-entry lobby
    /// player array and a 16-entry team array.</summary>
    public const int SeatCapacity = 16;

    // The original's per-pilot colour table at 00628eb4, eight dwords, zeros from 00628ed4. Its one
    // reader takes the low byte as red, then green, then blue. So each entry is restated here as
    // 0xRRGGBB with its stored bytes in order (docs/org/multiplayer-spawn.md).
    private static readonly uint[] Authored =
    {
        0x812D2D, 0x2D2D81, 0x2D812D, 0x81812D, 0x812D64, 0x66812D, 0x457C81, 0x662D81,
    };

    private static readonly uint[] Table = BuildTable();

    /// <summary>Every seat's colour as 0xRRGGBB, <see cref="SeatCapacity"/> long. The first eight
    /// are the authored table; the rest are derived (see <see cref="SeatColor"/>).</summary>
    public static IReadOnlyList<uint> SeatColors => Table;

    /// <summary>Seat <paramref name="seat"/>'s identity colour as 0xRRGGBB. Seats 0 to 7 take the
    /// original's authored dwords in order. The remake's index is 0-based where the original's was
    /// 1-based, so its eighth pilot read past the table and ours does not. Seats 8 to 15 take the
    /// channel-wise complement of seat minus 8, a light twin of a dark authored colour. It collides
    /// with none of them. The derived eight are the remake's own; the original has no colour past
    /// its table.</summary>
    public static uint SeatColor(int seat)
    {
        if (seat < 0 || seat >= SeatCapacity)
        {
            throw new ArgumentOutOfRangeException(nameof(seat), seat, $"a seat index is 0 to {SeatCapacity - 1}");
        }

        return Table[seat];
    }

    /// <summary>Throws unless <paramref name="roster"/> is a match a session can be built from. One
    /// to <see cref="MaxPlayers"/> seats, numbered 0 upward with no gap, and at least one with a
    /// pane here. Every seat-indexed table is addressed by the number, so a leaver's seat stays in
    /// the roster until the match ends. Seat 0 is a human, and given <paramref name="hostPeer"/> it
    /// must be that peer's, since every P1 read takes seat 0 as the host. A bot seat is owned and
    /// flown where seat 0 is, which is the host on every machine's copy.</summary>
    public static void Validate(IReadOnlyList<NetSeat> roster, int? hostPeer = null)
    {
        ArgumentNullException.ThrowIfNull(roster);
        if (roster.Count == 0 || roster.Count > MaxPlayers)
        {
            throw new ArgumentException($"a match holds 1 to {MaxPlayers} seats, not {roster.Count}", nameof(roster));
        }

        var seen = new HashSet<int>();
        int panes = 0;
        NetSeat? first = null;
        foreach (var seat in roster)
        {
            if (seat.SeatIndex < 0 || seat.SeatIndex >= roster.Count)
            {
                throw new ArgumentException($"seat index {seat.SeatIndex} is outside 0 to {roster.Count - 1}", nameof(roster));
            }

            if (!seen.Add(seat.SeatIndex))
            {
                throw new ArgumentException($"two seats claim index {seat.SeatIndex}", nameof(roster));
            }

            if (seat.HasPane)
            {
                panes++;
            }

            if (seat.SeatIndex == 0)
            {
                first = seat;
            }

            if (seat.SeatIndex == 0 && hostPeer is { } host && seat.PeerId != host)
            {
                throw new ArgumentException($"seat 0 belongs to peer {seat.PeerId}, not the host's peer {host}", nameof(roster));
            }
        }

        if (panes == 0)
        {
            throw new ArgumentException("no seat in the roster has a pane on this machine", nameof(roster));
        }

        RequireHostBots(roster, first!);
    }

    /// <summary>Appends <paramref name="bots"/> to a host's roster as bot seats of
    /// <paramref name="hostPeer"/>, numbered on from the seats already in it. A bot past
    /// <see cref="MaxPlayers"/> is not seated, so a guest always outranks a bot. Answers how many
    /// were left out.</summary>
    public static int AddBots(List<NetSeat> roster,
        int hostPeer, IEnumerable<SeatedBot> bots)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(bots);
        int left = 0;
        foreach (var bot in bots)
        {
            if (roster.Count >= MaxPlayers)
            {
                left++;
                continue;
            }

            roster.Add(Bot(hostPeer, roster.Count, bot.Callsign, bot.Plane, bot.Skill, bot.Team));
        }

        return left;
    }

    /// <summary>Seat <paramref name="seat"/>'s callsign when a bot flies it, else null, so a caller
    /// names a person by player tag. Null too for a seat past the roster or with no roster.</summary>
    public static string? BotCallsign(IReadOnlyList<NetSeat>? roster, int seat) =>
        roster != null && seat >= 0 && seat < roster.Count && roster[seat].IsBot ? roster[seat].Callsign : null;

    /// <summary>A bot seat at <paramref name="seatIndex"/>, owned and flown by the host whose peer
    /// is <paramref name="hostPeer"/>. It has no pane and no menu pick. Its loadout and build reach
    /// the session by seat, as a guest's do. <paramref name="team"/> is a lobby team number.
    /// </summary>
    public static NetSeat Bot(int hostPeer, int seatIndex, string callsign, string planeNode,
        NetBotSkill skill = NetBotSkill.Veteran, int team = 0) => new()
        {
            PeerId = hostPeer,
            SeatIndex = seatIndex,
            TeamId = team,
            FlownHere = true,
            Pilot = NetPilot.Bot,
            Skill = skill,
            Callsign = SeatRosterMessage.Carried((callsign ?? "").Trim()),
            PlaneNode = planeNode ?? "",
        };

    /// <summary>The seats that leave the match when <paramref name="peer"/> drops off the host's
    /// wire: every human seat that peer flies elsewhere. A bot seat carries the host's own peer and
    /// is flown here, so no guest's leave ever takes one.</summary>
    public static IEnumerable<int> LeavingWith(IReadOnlyList<NetSeat> roster, int peer)
    {
        ArgumentNullException.ThrowIfNull(roster);
        for (int seat = 0; seat < roster.Count; seat++)
        {
            if (LeavesWithPeer(roster[seat]) && roster[seat].PeerId == peer)
            {
                yield return seat;
            }
        }
    }

    /// <summary>Whether <paramref name="seat"/> can leave the match on its own machine's word: a
    /// human flown on another machine. A bot seat never leaves; only the host's own exit ends it.
    /// </summary>
    public static bool LeavesWithPeer(NetSeat seat) =>
        seat is { FlownHere: false, Pilot: NetPilot.Human };

    /// <summary>A host's field: one seat per plane in <paramref name="localPlanes"/> flown here,
    /// then one per peer in <paramref name="peers"/> flying <paramref name="remotePlane"/>, cut at
    /// <see cref="MaxPlayers"/>. Local seats are called P1 upward and a remote one after its peer,
    /// the first local seat and every remote one <see cref="NetSeat.Unnamed"/>. A remote guest holds
    /// a seat and nothing else here: no pane, no pad, no input.</summary>
    public static NetSeat[] Field(
        int localPeer, IReadOnlyList<string> localPlanes, IReadOnlyList<int> peers, string remotePlane)
    {
        ArgumentNullException.ThrowIfNull(localPlanes);
        ArgumentNullException.ThrowIfNull(peers);
        var seats = new List<NetSeat>(localPlanes.Count + peers.Count);
        for (int i = 0; i < localPlanes.Count && seats.Count < MaxPlayers; i++)
        {
            seats.Add(new NetSeat
            {
                PeerId = localPeer,
                SeatIndex = seats.Count,
                FlownHere = true,
                Callsign = $"P{(i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                Unnamed = i == 0,
                PlaneNode = localPlanes[i],
            });
        }

        foreach (int peer in peers)
        {
            if (seats.Count >= MaxPlayers)
            {
                break;
            }

            seats.Add(new NetSeat
            {
                PeerId = peer,
                SeatIndex = seats.Count,
                Callsign = $"guest {peer.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                Unnamed = true,
                PlaneNode = remotePlane,
            });
        }

        Validate(seats, localPeer);
        return seats.ToArray();
    }

    /// <summary>A local match's panes as the first seats of its roster, P1 upward, owned by
    /// <see cref="OfflinePeer"/>. Each names no plane, so a pane flies its own pick as it does
    /// with no roster. Bots follow through <see cref="AddBots"/>.</summary>
    public static List<NetSeat> LocalPanes(int panes)
    {
        var seats = new List<NetSeat>(MaxPlayers);
        for (int i = 0; i < panes && seats.Count < MaxPlayers; i++)
        {
            seats.Add(new NetSeat
            {
                PeerId = OfflinePeer,
                SeatIndex = seats.Count,
                FlownHere = true,
                Callsign = $"P{(i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            });
        }

        return seats;
    }

    /// <summary>A co-op host's field: one seat per plane in <paramref name="localPlanes"/> flown
    /// here, then one per entry of <paramref name="guests"/> flying the plane it picked, cut at
    /// <see cref="MaxPlayers"/>. A guest flying several seats has one entry per seat, side by side.
    /// A guest's seat is called by its pick's player name, the first local seat by
    /// <paramref name="hostName"/>. A seat with no name is called by its player number, and is
    /// <see cref="NetSeat.Unnamed"/> when it is its machine's first.</summary>
    public static NetSeat[] CoopField(
        int localPeer, IReadOnlyList<string> localPlanes, IReadOnlyList<(int Peer, string Plane, string Name)> guests,
        string hostName = "")
    {
        ArgumentNullException.ThrowIfNull(localPlanes);
        ArgumentNullException.ThrowIfNull(guests);
        RequireRuns(guests);
        var seats = new List<NetSeat>(localPlanes.Count + guests.Count);
        for (int i = 0; i < localPlanes.Count + guests.Count && seats.Count < MaxPlayers; i++)
        {
            bool local = i < localPlanes.Count;
            string name = local ? (i == 0 ? (hostName ?? "").Trim() : "") : (guests[i - localPlanes.Count].Name ?? "").Trim();
            int guest = i - localPlanes.Count;
            bool first = local ? i == 0 : guest == 0 || guests[guest - 1].Peer != guests[guest].Peer;
            seats.Add(new NetSeat
            {
                PeerId = local ? localPeer : guests[guest].Peer,
                SeatIndex = seats.Count,
                FlownHere = local,
                Callsign = name.Length > 0
                    ? name
                    : $"P{(i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                Unnamed = name.Length == 0 && first,
                PlaneNode = local ? localPlanes[i] : guests[i - localPlanes.Count].Plane,
            });
        }

        Validate(seats, localPeer);
        return seats.ToArray();
    }

    /// <summary>Which of this machine's panes <paramref name="seat"/> is, counting from 0 in seat
    /// order. That is its index into this machine's players, as menu picks, pads and panes are
    /// listed. A seat with no pane here, a bot flown here included, answers -1. With no roster
    /// every seat is local, so it is its own.</summary>
    public static int LocalOrdinal(IReadOnlyList<NetSeat> roster, int seat)
    {
        ArgumentNullException.ThrowIfNull(roster);
        if (seat >= roster.Count)
        {
            return roster.Count == 0 ? seat : -1;
        }

        if (seat < 0 || !roster[seat].HasPane)
        {
            return -1;
        }

        int ordinal = 0;
        for (int i = 0; i < seat; i++)
        {
            ordinal += roster[i].HasPane ? 1 : 0;
        }

        return ordinal;
    }

    // ⚠ Do not seat a bot at 0 or on another machine. Every P1 read takes seat 0 as the host's own
    // player. Only the host flies a bot, so on every copy a bot shares seat 0's peer and place.
    private static void RequireHostBots(IReadOnlyList<NetSeat> roster, NetSeat first)
    {
        if (first.IsBot)
        {
            throw new ArgumentException("seat 0 is the host's own player, never a bot", nameof(roster));
        }

        foreach (var seat in roster)
        {
            if (seat.IsBot && (seat.PeerId != first.PeerId || seat.FlownHere != first.FlownHere))
            {
                throw new ArgumentException($"bot seat {seat.SeatIndex} is not the host's: peer {seat.PeerId}, flown here {seat.FlownHere}, where seat 0 is peer {first.PeerId}, flown here {first.FlownHere}", nameof(roster));
            }
        }
    }

    // ⚠ Do not let a guest's seats part. Its handshake names them as one run from its first seat,
    // so a seat outside the run would be flown by nobody.
    private static void RequireRuns(IReadOnlyList<(int Peer, string Plane, string Name)> guests)
    {
        var closed = new HashSet<int>();
        for (int i = 0; i < guests.Count; i++)
        {
            int peer = guests[i].Peer;
            if (closed.Contains(peer))
            {
                throw new ArgumentException($"peer {peer}'s seats are not side by side", nameof(guests));
            }

            if (i + 1 >= guests.Count || guests[i + 1].Peer != peer)
            {
                closed.Add(peer);
            }
        }
    }

    private static uint[] BuildTable()
    {
        var table = new uint[SeatCapacity];
        for (int i = 0; i < SeatCapacity; i++)
        {
            table[i] = i < Authored.Length ? Authored[i] : ~Authored[i - Authored.Length] & 0xFFFFFFu;
        }

        return table;
    }
}
