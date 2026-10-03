namespace CSVM.Net;

/// <summary>Where one end's link stands. Named here rather than taken from the engine, so a
/// board can show it without learning what carries it.</summary>
public enum NetLinkState
{
    /// <summary>A join whose handshake has not finished. Nothing can be sent yet.</summary>
    Connecting,

    /// <summary>Open, and sending works.</summary>
    Up,

    /// <summary>Closed, refused, or given up on. Nothing will arrive again.</summary>
    Down,
}

/// <summary>A carrier that can say where its link stands. The seam itself has no word for that,
/// and a join board shows it. Only a real socket implements this; a board asks for it and falls
/// back to the peer roster when a carrier has none.</summary>
public interface INetLink
{
    /// <summary>How many payloads a link holds for a listener that has not bound yet. Deep enough
    /// for a join answer and the openers behind it, shallow enough that a carrier nobody ever binds
    /// cannot grow without bound. Past it the oldest held payload is dropped and logged.</summary>
    public const int HeldPayloads = 64;

    /// <summary>Where this end's link stands right now.</summary>
    NetLinkState LinkState { get; }

    /// <summary>Payloads taken off the socket while no listener was bound, at most
    /// <see cref="HeldPayloads"/>; see <see cref="INetTransport.Bind"/>, which replays them.</summary>
    int PendingPayloads { get; }

    /// <summary>Why the link went down, as a player reads it, or "" when the carrier has no word
    /// for it. A board shows it in place of a bare refusal.</summary>
    string LinkFault => "";
}
