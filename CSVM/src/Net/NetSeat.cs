namespace CSVM.Net;

/// <summary>Who flies a seat: a person at some machine, or a computer pilot its host flies. The
/// original has no computer player, so a bot seat is a remake-only rule.</summary>
public enum NetPilot : byte
{
    /// <summary>A person, at the machine that owns the seat.</summary>
    Human = 0,

    /// <summary>A computer pilot, always flown on the host and never given a pane.</summary>
    Bot = 1,
}

/// <summary>A bot's skill tier, the Instant Action difficulty's three words (<c>IDS_IA_DIFFICULTY</c>,
/// docs/formats/instant-action.md). The numeric values are the roster's two wire bits.</summary>
public enum NetBotSkill : byte
{
    /// <summary>Two points off every rating.</summary>
    Novice = 0,

    /// <summary>The ratings as rolled.</summary>
    Veteran = 1,

    /// <summary>Two points on every rating.</summary>
    Ace = 2,
}

/// <summary>A bot as a host seats it. Its plane is resolved to a stock node and its callsign is
/// drawn. <paramref name="Team"/> is a lobby team number, 0 for none.</summary>
public readonly record struct SeatedBot(string Plane, NetBotSkill Skill, int Team, string Callsign);

/// <summary>
/// One pilot's place in a network match, shaped like the record the original allocates per player.
/// The peer it is addressed by, its team, who flies it and where, and its callsign. Then the
/// airframe and paint it chose, its seat index and its signed score. That index is the identity
/// every seat-indexed system already keys on. A remote pilot therefore indexes spawns, scores,
/// markers and colours exactly as a splitscreen pane does.
/// The record is what a session hands to and takes from the wire; the bytes are the vocabulary's.
/// </summary>
public sealed record NetSeat
{
    /// <summary>The transport peer this seat is reached through, the match key. A seat on this
    /// machine carries the local peer's own id, and a bot seat its host's, so one peer can own
    /// several seats.</summary>
    public int PeerId { get; init; }

    /// <summary>0-based seat number, below <see cref="NetSeats.SeatCapacity"/>. The original's own
    /// index is 1-based and its eighth pilot reads past the colour table; the remake's is not.
    /// </summary>
    public int SeatIndex { get; init; }

    /// <summary>The lobby team this pilot flies for, by its team number, 0 in a free-for-all. Teams
    /// are numbered from 1, which leaves block 0 of the spawn table to the un-teamed match. It is
    /// not a team id, so no hostility test reads it as one.</summary>
    public int TeamId { get; init; }

    /// <summary>Whether this machine simulates the seat: it sends the seat's state, fire, damage
    /// and death, and applies the hits on it. A bot seat is flown on its host. Whether the seat
    /// also gets a pane is <see cref="HasPane"/>, a separate claim.</summary>
    public bool FlownHere { get; init; }

    /// <summary>Who flies the seat. A bot seat belongs to the host's peer.</summary>
    public NetPilot Pilot { get; init; }

    /// <summary>A bot's skill tier. A human seat carries the default and nothing reads it.</summary>
    public NetBotSkill Skill { get; init; } = NetBotSkill.Veteran;

    /// <summary>Whether a computer pilot flies the seat.</summary>
    public bool IsBot => Pilot == NetPilot.Bot;

    /// <summary>Whether a person sits at this seat on this machine. Such a seat gets a pane, a
    /// camera, a HUD, a listener, an input device and a place in the menu's local picks. A seat
    /// flown here by a bot gets none of those.</summary>
    public bool HasPane => FlownHere && Pilot == NetPilot.Human;

    /// <summary>What the scoreboard and the kill line call this pilot.</summary>
    public string Callsign { get; init; } = "";

    /// <summary>Whether this seat is a machine's own player who gave no name, so
    /// <see cref="Callsign"/> is a stand-in. Its marker reads row 6007 "Unknown", the original's
    /// label for a nameless peer (docs/org/targeting.md, the network author). A machine's further
    /// splitscreen seats are not peers and keep their player number.</summary>
    public bool Unnamed { get; init; }

    /// <summary>The airframe node this seat flies, the same name a <c>--plane=</c> entry carries.
    /// </summary>
    public string PlaneNode { get; init; } = "";

    /// <summary>The paint this seat chose, empty where it takes the session's own livery draw.
    /// </summary>
    public string Livery { get; init; } = "";

    /// <summary>The pilot voice this seat's player chose, in the pick's form: its place in the
    /// Voice list plus one, 0 for none (<see cref="CoopPickMessage.Voice"/>). The roster carries
    /// it to every machine, which speaks the seat's in-flight lines in it.</summary>
    public byte Voice { get; init; }

    /// <summary>This pilot's match score, signed: a suicide costs a point, so it goes negative.
    /// </summary>
    public int Score { get; init; }

    /// <summary>This seat's identity colour as 0xRRGGBB, the marker and scoreboard key.</summary>
    public uint Color => NetSeats.SeatColor(SeatIndex);
}
