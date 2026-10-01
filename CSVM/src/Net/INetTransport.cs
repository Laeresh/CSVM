using System;
using System.Collections.Generic;

namespace CSVM.Net;

/// <summary>
/// How hard a transport tries to land one payload. Unreliable is sent once and may be lost or
/// overtaken. UnreliableSequenced is also sent once and may be lost. A payload older than the
/// newest already delivered on its channel is discarded rather than handed over behind it.
/// Reliable is never dropped and arrives in the order it was sent.
/// </summary>
public enum NetReliability
{
    /// <summary>Sent once; may be lost, may arrive out of order.</summary>
    Unreliable,

    /// <summary>Sent once; may be lost, and is never delivered behind a newer payload on its channel.</summary>
    UnreliableSequenced,

    /// <summary>Delivered, in send order.</summary>
    Reliable,
}

/// <summary>
/// What a transport tells its owner: the two roster changes, and every payload that lands. The
/// payload is a window onto the transport's own buffer, valid only for the call. A handler that
/// wants to keep the bytes copies them.
/// </summary>
public interface INetTransportListener
{
    /// <summary>A peer joined the roster and can now be sent to.</summary>
    void OnPeerConnected(int peer);

    /// <summary>A peer left the roster. Anything still in flight either way is gone with it.</summary>
    void OnPeerDisconnected(int peer);

    /// <summary>One payload that arrived from <paramref name="peer"/> on <paramref name="channel"/>.
    /// Which reliability class carried it is not reported, because the guarantees are already
    /// applied by the time this runs.</summary>
    void OnPayload(int peer, int channel, ReadOnlySpan<byte> payload);
}

/// <summary>
/// A carrier that can name the network address a peer reached it from, the key a host's ban list
/// holds a booted guest by. A carrier without addresses leaves a boot unable to refuse a return.
/// </summary>
public interface INetPeerAddress
{
    /// <summary>The address <paramref name="peer"/> connected from, without a port, or null when
    /// the carrier does not know it.</summary>
    string? AddressOf(int peer);
}

/// <summary>
/// A host carrier that lists its game on the master server. The door hands it the listing on every
/// step. The carrier sends it when it changes and on the heartbeat. It reads back the code a guest
/// joins by. A carrier with no master server has none of this.
/// </summary>
public interface INetListing
{
    /// <summary>The code the master server listed the game under, or null while it has not.</summary>
    string? JoinCode { get; }

    /// <summary>Why the game is not listed, as a player reads it, or "" while nothing went wrong.
    /// </summary>
    string ListingFault { get; }

    /// <summary>The listing the game carries from now on.</summary>
    void List(MasterGame listing);
}

/// <summary>
/// The carrier a session sends bytes over, with no idea what the bytes mean. It offers the peer
/// roster, one send per payload with its reliability class, and a listener the arrivals and
/// roster changes are reported to. Payloads are byte spans, so the message vocabulary sits
/// entirely above this seam. No member here names an engine type or a socket, so the same session
/// runs over the loopback and over a real carrier.
/// ⚠ Nothing arrives until <see cref="Step"/> runs; a caller that never steps receives nothing.
/// </summary>
public interface INetTransport
{
    /// <summary>This end's own peer id, which the other peers address it by.</summary>
    int LocalPeer { get; }

    /// <summary>Every peer that can be sent to, this end excluded.</summary>
    IReadOnlyList<int> Peers { get; }

    /// <summary>Attaches the one listener everything this transport reports goes to. The peers
    /// already on the roster are announced to it, so binding order changes nothing. Called once
    /// per transport.</summary>
    void Bind(INetTransportListener listener);

    /// <summary>Queues one payload for <paramref name="peer"/> under
    /// <paramref name="reliability"/>, sequenced against the other payloads on
    /// <paramref name="channel"/>. The bytes are copied, so the caller's buffer is free on return,
    /// and a send to a peer that has left the roster is discarded.</summary>
    void Send(int peer, ReadOnlySpan<byte> payload, NetReliability reliability, int channel = 0);

    /// <summary>Hangs up on <paramref name="peer"/>, which leaves both rosters and is reported to
    /// both listeners.</summary>
    void Disconnect(int peer);

    /// <summary>Advances this end by <paramref name="dt"/> seconds and reports everything now due
    /// to the listener. The one place a payload is delivered.</summary>
    void Step(double dt);
}
