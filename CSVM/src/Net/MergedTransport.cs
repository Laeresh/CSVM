using System;
using System.Collections.Generic;

namespace CSVM.Net;

/// <summary>
/// Several host carriers as one: a host that takes direct and LAN guests over ENet and internet
/// guests over WebRTC runs one session over both. Each carrier numbers its own guests. This gives
/// every guest its own id from 2 up and keeps the map both ways; the host stays peer 1. The link,
/// the addresses and the listing are asked of whichever carrier has them.
/// ⚠ Hand in hosts' carriers only. A guest's carrier is one link to peer 1, which this would
/// announce as a second host.
/// </summary>
public sealed class MergedTransport : INetTransport, INetLink, INetPeerAddress, INetListing, IDisposable
{
    private readonly INetTransport[] _carriers;
    private readonly List<int> _peers = new();
    private readonly Dictionary<int, (int Carrier, int Inner)> _inner = new();
    private readonly Dictionary<(int Carrier, int Inner), int> _outer = new();
    private INetTransportListener? _listener;
    private int _next = 2;

    /// <summary>One host over <paramref name="carriers"/>, the first of which answers for the link.
    /// </summary>
    public MergedTransport(params INetTransport[] carriers)
    {
        ArgumentNullException.ThrowIfNull(carriers);
        if (carriers.Length == 0)
        {
            throw new ArgumentException("a merged host needs a carrier", nameof(carriers));
        }

        _carriers = carriers;
    }

    /// <inheritdoc/>
    public int LocalPeer => 1;

    /// <inheritdoc/>
    public IReadOnlyList<int> Peers => _peers;

    /// <inheritdoc/>
    public EnetLinkState LinkState => _carriers[0] is INetLink link ? link.LinkState : EnetLinkState.Up;

    /// <inheritdoc/>
    public int PendingPayloads
    {
        get
        {
            int held = 0;
            foreach (var carrier in _carriers)
            {
                held += carrier is INetLink link ? link.PendingPayloads : 0;
            }

            return held;
        }
    }

    /// <inheritdoc/>
    public string? JoinCode => Listing?.JoinCode;

    /// <inheritdoc/>
    public string ListingFault => Listing?.ListingFault ?? "";

    private INetListing? Listing
    {
        get
        {
            foreach (var carrier in _carriers)
            {
                if (carrier is INetListing listing)
                {
                    return listing;
                }
            }

            return null;
        }
    }

    /// <inheritdoc/>
    public void Bind(INetTransportListener listener)
    {
        if (_listener != null)
        {
            throw new InvalidOperationException("a merged host already has a listener bound");
        }

        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        for (int i = 0; i < _carriers.Length; i++)
        {
            _carriers[i].Bind(new Relay(this, i));
        }
    }

    /// <inheritdoc/>
    public void Send(int peer, ReadOnlySpan<byte> payload, NetReliability reliability, int channel = 0)
    {
        if (_inner.TryGetValue(peer, out var at))
        {
            _carriers[at.Carrier].Send(at.Inner, payload, reliability, channel);
        }
    }

    /// <inheritdoc/>
    public void Disconnect(int peer)
    {
        if (_inner.TryGetValue(peer, out var at))
        {
            _carriers[at.Carrier].Disconnect(at.Inner);
        }
    }

    /// <inheritdoc/>
    public void Step(double dt)
    {
        foreach (var carrier in _carriers)
        {
            carrier.Step(dt);
        }
    }

    /// <inheritdoc/>
    public string? AddressOf(int peer) =>
        _inner.TryGetValue(peer, out var at) && _carriers[at.Carrier] is INetPeerAddress addresses ? addresses.AddressOf(at.Inner) : null;

    /// <inheritdoc/>
    public void List(MasterGame listing) => Listing?.List(listing);

    /// <summary>Closes every carrier.</summary>
    public void Dispose()
    {
        foreach (var carrier in _carriers)
        {
            (carrier as IDisposable)?.Dispose();
        }

        _peers.Clear();
        _inner.Clear();
        _outer.Clear();
    }

    private void Joined(int carrier, int inner)
    {
        if (_outer.ContainsKey((carrier, inner)))
        {
            return;
        }

        while (_inner.ContainsKey(_next))
        {
            _next++;
        }

        int peer = _next++;
        _inner[peer] = (carrier, inner);
        _outer[(carrier, inner)] = peer;
        _peers.Add(peer);
        _listener?.OnPeerConnected(peer);
    }

    private void Left(int carrier, int inner)
    {
        if (!_outer.Remove((carrier, inner), out int peer))
        {
            return;
        }

        _inner.Remove(peer);
        _peers.Remove(peer);
        _listener?.OnPeerDisconnected(peer);
    }

    private void Arrived(int carrier, int inner, int channel, ReadOnlySpan<byte> payload)
    {
        if (_outer.TryGetValue((carrier, inner), out int peer))
        {
            _listener?.OnPayload(peer, channel, payload);
        }
    }

    // One carrier's listener, which renumbers what that carrier reports.
    private sealed class Relay : INetTransportListener
    {
        private readonly MergedTransport _owner;
        private readonly int _carrier;

        public Relay(MergedTransport owner, int carrier)
        {
            _owner = owner;
            _carrier = carrier;
        }

        public void OnPeerConnected(int peer) => _owner.Joined(_carrier, peer);

        public void OnPeerDisconnected(int peer) => _owner.Left(_carrier, peer);

        public void OnPayload(int peer, int channel, ReadOnlySpan<byte> payload) => _owner.Arrived(_carrier, peer, channel, payload);
    }
}
