using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CSVM.Net;

/// <summary>
/// A host's hold on its router: the UPnP IPv4 port mapping and the IGD v2 IPv6 pinhole. Each is a
/// finite lease, asked for and renewed on a thread of its own. The router calls arrive as delegates,
/// so a suite passes stubs and one with none asks nothing. A door asks it to <see cref="Open"/> a
/// port, reads each first answer through <see cref="Poll"/>, and gives everything back through
/// <see cref="Close"/>.
/// </summary>
public sealed class RouterAccess
{
    private readonly Func<int, UpnpPortMapResult>? _map;
    private readonly Action<int>? _unmap;
    private readonly Func<int, UpnpPinholeResult>? _openPinhole;
    private readonly Action<int>? _closePinhole;
    private readonly List<Task> _leases = new();
    private Task<UpnpPortMapResult>? _mapping;
    private Task<UpnpPinholeResult>? _pinholing;
    private CancellationTokenSource? _renewal;
    private int _mappedPort;
    private int _pinholePort;

    /// <summary>A router reached through <paramref name="map"/> and <paramref name="unmap"/> for
    /// the IPv4 mapping, and <paramref name="openPinhole"/> and <paramref name="closePinhole"/> for
    /// the IPv6 pinhole, each given the port. A null call asks for that lease not at all.</summary>
    public RouterAccess(
        Func<int, UpnpPortMapResult>? map = null,
        Action<int>? unmap = null,
        Func<int, UpnpPinholeResult>? openPinhole = null,
        Action<int>? closePinhole = null)
    {
        _map = map;
        _unmap = unmap;
        _openPinhole = openPinhole;
        _closePinhole = closePinhole;
    }

    /// <summary>The port mapping this host asked its router for, or null when none was asked for
    /// or the answer has not landed yet.</summary>
    public UpnpPortMapResult? PortMap { get; private set; }

    /// <summary>The pinhole this host asked its router for, or null when none was asked for or the
    /// answer has not landed yet.</summary>
    public UpnpPinholeResult? Pinhole { get; private set; }

    /// <summary>Asks the router for <paramref name="port"/>, the mapping and the pinhole each on a
    /// thread of its own. Returns at once; the answers land on a later <see cref="Poll"/>.</summary>
    /// <remarks>⚠ Each call blocks for a gateway search, so the mapping and the pinhole each run on a
    /// dedicated thread, never the pool, which starves. Each thread renews its own lease until
    /// <see cref="Close"/>.</remarks>
    public void Open(int port)
    {
        _mapping = _map is { } map ? Hold(map, r => (r.IsMapped, r.LeaseSeconds), port) : null;
        _pinholing = _openPinhole is { } open ? Hold(open, r => (r.IsOpen, r.LeaseSeconds), port) : null;
    }

    /// <summary>Takes whichever first answer has landed onto <see cref="PortMap"/> and
    /// <see cref="Pinhole"/>. A board shows each on the step that finds it.</summary>
    public void Poll()
    {
        if (_pinholing is { IsCompleted: true })
        {
            var pinhole = _pinholing.Result;
            _pinholing = null;
            Pinhole = pinhole;
            _pinholePort = pinhole.IsOpen ? pinhole.Port : 0;
        }

        if (_mapping is not { IsCompleted: true })
        {
            return;
        }

        var result = _mapping.Result;
        _mapping = null;
        PortMap = result;
        _mappedPort = result.IsMapped ? result.Port : 0;
    }

    /// <summary>Forgets both readouts, stops the renewals and gives the mapping and the pinhole
    /// back. ⚠ Not put on a thread: a mapping left behind is a door standing open in the player's
    /// own router. The wait falls on the way out of hosting, not in front of a player waiting to fly.
    /// </summary>
    public void Close()
    {
        PortMap = null;
        Pinhole = null;
        StopRenewal();
        Poll();
        if (_mapping != null)
        {
            var landed = _mapping.Result;
            _mapping = null;
            _mappedPort = landed.IsMapped ? landed.Port : 0;
        }

        if (_pinholing != null)
        {
            var landed = _pinholing.Result;
            _pinholing = null;
            _pinholePort = landed.IsOpen ? landed.Port : 0;
        }

        if (_mappedPort != 0 && _unmap != null)
        {
            _unmap(_mappedPort);
        }

        if (_pinholePort != 0 && _closePinhole != null)
        {
            _closePinhole(_pinholePort);
        }

        _mappedPort = 0;
        _pinholePort = 0;
    }

    // The mapping thread's whole life. It runs on after a launch takes the socket, since a match
    // never steps the door. A lease that lapsed mid-match would shut every guest out.
    // The pinhole's thread is the same shape, since both leases follow one set of rules.
    private static void HoldLease<T>(Func<int, T> ask, Func<T, (bool Held, int Seconds)> granted, int port,
        TaskCompletionSource<T> first, CancellationToken stop)
    {
        T latest;
        try
        {
            latest = ask(port);
        }
        catch (Exception e)
        {
            first.SetException(e);
            throw;
        }

        first.SetResult(latest);
        var (isHeld, seconds) = granted(latest);
        int held = isHeld ? seconds : 0;
        var wait = UpnpLease.NextRenewal(isHeld, seconds, held);
        while (wait != Timeout.InfiniteTimeSpan && !stop.WaitHandle.WaitOne(wait))
        {
            latest = ask(port);
            (isHeld, seconds) = granted(latest);
            held = isHeld ? seconds : held;
            wait = UpnpLease.NextRenewal(isHeld, seconds, held);
        }
    }

    // One lease on its own thread, stopped by the one renewal token.
    private Task<T> Hold<T>(Func<int, T> ask, Func<T, (bool Held, int Seconds)> granted, int port)
    {
        var first = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _renewal ??= new CancellationTokenSource();
        var stop = _renewal.Token;
        _leases.Add(Task.Factory.StartNew(() => HoldLease(ask, granted, port, first, stop), CancellationToken.None,
            TaskCreationOptions.LongRunning, TaskScheduler.Default));
        return first.Task;
    }

    // Stopped and waited for before the unmap, so a renewal in flight cannot put the mapping back.
    private void StopRenewal()
    {
        _renewal?.Cancel();
        foreach (var lease in _leases)
        {
            Task.WaitAny(lease);
        }

        _renewal?.Dispose();
        _renewal = null;
        _leases.Clear();
    }
}
