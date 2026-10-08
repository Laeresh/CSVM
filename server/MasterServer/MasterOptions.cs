using System;
using System.Collections.Generic;

namespace CSVM.Master;

/// <summary>
/// The server's settings, bound from the <c>Master</c> configuration section, which an environment
/// variable sets as <c>Master__TurnSecret</c> and so on (server/README.md lists them all). Every
/// limit has a default that suits one small VPS; the ICE settings default to none, which leaves a
/// join to host candidates alone.
/// </summary>
public sealed class MasterOptions
{
    /// <summary>The STUN URLs handed to every negotiation, such as
    /// <c>stun:turn.example.org:3478</c>. Comma-separated in one setting.</summary>
    public string Stun { get; set; } = "";

    /// <summary>The TURN URLs handed with a minted credential, such as
    /// <c>turn:turn.example.org:3478?transport=udp</c>. Comma-separated in one setting. Ignored
    /// while <see cref="TurnSecret"/> is empty.</summary>
    public string Turn { get; set; } = "";

    /// <summary>coturn's <c>static-auth-secret</c>, the key a TURN credential is signed with. Empty
    /// hands out no TURN entry at all.</summary>
    public string TurnSecret { get; set; } = "";

    /// <summary>How long a minted TURN credential stays good, in minutes. coturn refuses a refresh
    /// past it, so it bounds the longest match a relayed guest can fly.</summary>
    public int TurnCredentialMinutes { get; set; } = 12 * 60;

    /// <summary>Whether to take the client address from the reverse proxy's
    /// <c>X-Forwarded-For</c>. ⚠ Only behind a proxy that overwrites it, with this server's own
    /// port closed to the internet; otherwise any client can name any address.</summary>
    public bool TrustProxy { get; set; }

    /// <summary>The most games listed at once.</summary>
    public int MaxGames { get; set; } = 500;

    /// <summary>The most games one address may host at once.</summary>
    public int MaxGamesPerAddress { get; set; } = 4;

    /// <summary>The most open sockets one address may hold.</summary>
    public int MaxSocketsPerAddress { get; set; } = 16;

    /// <summary>The most guests negotiating with one game at once.</summary>
    public int MaxPendingGuests { get; set; } = 16;

    /// <summary>The most guests from one address negotiating with one game at once, never below 1.
    /// Without it one address fills <see cref="MaxPendingGuests"/> and turns every other guest away.
    /// A guest stops counting once its link stands, since it then closes its socket, so players
    /// behind one NAT joining one after another are not held by it.</summary>
    public int MaxPendingGuestsPerAddress { get; set; } = 2;

    /// <summary>The TURN credentials one address may cause to be minted in an hour, never below the
    /// two one join takes: its own and the host's for it. Each holds up to coturn's
    /// <c>user-quota</c> relay ports until it expires, and the relay range is small, so an
    /// unbounded address could take them all.</summary>
    public int TurnMintsPerHour { get; set; } = 10;

    /// <summary>The messages a socket may send in a burst, its bucket's size.</summary>
    public int MessageBurst { get; set; } = 60;

    /// <summary>The messages a socket may send per second once its burst is spent.</summary>
    public double MessagesPerSecond { get; set; } = 5.0;

    /// <summary>The games list requests one address may make per minute.</summary>
    public int ListPerMinute { get; set; } = 60;

    /// <summary>The sockets one address may open per minute.</summary>
    public int SocketsPerMinute { get; set; } = 30;

    /// <summary>How long a guest's socket may stay open, in seconds. A negotiation takes a few, and
    /// a guest closes its socket once its link stands.</summary>
    public int GuestSocketSeconds { get; set; } = 120;

    /// <summary>The oldest <c>MasterWire.ProtocolVersion</c> the server serves. A host or join below it
    /// is refused with a request to update, and the games list names it so an older build says so.
    /// ⚠ Raise it only after a game release speaking the new version is out, or every current
    /// player is locked out; a build naming no version speaks 1.</summary>
    public int OldestProtocol { get; set; } = 1;

    /// <summary>The STUN URLs as a list.</summary>
    public IReadOnlyList<string> StunUrls => Split(Stun);

    /// <summary>The TURN URLs as a list.</summary>
    public IReadOnlyList<string> TurnUrls => Split(Turn);

    private static string[] Split(string setting) =>
        (setting ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
