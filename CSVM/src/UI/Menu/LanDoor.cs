using System;
using System.Collections.Generic;
using CSVM.Net;

namespace CSVM.UI.Menu;

/// <summary>
/// The door's LAN discovery: the search a board runs for open doors on the local network, and the
/// responder a host answers searches with. The socket arrives as a delegate that binds an address
/// and a port; with none there is no search and a host answers none. The door hands it the bind
/// address, the build's version and the advert to answer with.
/// </summary>
public sealed class LanDoor
{
    /// <summary>What a LAN search asks at unless a suite points it elsewhere.</summary>
    public const string BroadcastAddress = LanBroadcast.Limited;

    private readonly Func<string, int, ILanSocket>? _lan;
    private LanResponder? _responder;
    private LanSearch? _search;

    /// <summary>Discovery over the sockets <paramref name="lan"/> binds, or none when it is null.
    /// </summary>
    public LanDoor(Func<string, int, ILanSocket>? lan) => _lan = lan;

    /// <summary>Where a LAN search sends its query: the broadcast address by default. A suite sets
    /// the loopback, since a broadcast on the loopback proves nothing on Windows.</summary>
    public string SearchAddress { get; set; } = BroadcastAddress;

    /// <summary>The IPv4 networks this machine sits on, as address and mask, read each round. A
    /// search at the broadcast address also asks at each one's directed broadcast. Null asks at
    /// <see cref="SearchAddress"/> alone. Set once, before the door opens.</summary>
    public Func<IReadOnlyList<(string Address, string Mask)>>? Networks { get; set; }

    /// <summary>How many rounds the open search has asked, 0 while none is open.</summary>
    public int SearchRounds => _search?.Rounds ?? 0;

    /// <summary>Why the LAN search would not open, or "" when it did.</summary>
    public string SearchFault { get; private set; } = "";

    /// <summary>Whether this host is answering LAN searches.</summary>
    public bool Answering => _responder != null;

    internal bool CanSearch => _lan != null;

    internal bool Searching => _search != null;

    internal IReadOnlyList<LanGame> Games => _search?.Games ?? (IReadOnlyList<LanGame>)Array.Empty<LanGame>();

    // The open search itself, which the door's reading compares by reference to see one open or close.
    internal LanSearch? Search => _search;

    // Opens the search on its first call, bound on bind; each call asks a new round.
    internal void Ask(string bind)
    {
        if (_lan == null)
        {
            return;
        }

        if (_search == null)
        {
            try
            {
                _search = new LanSearch(_lan(bind, 0), SearchTargets, NetPorts.Lan);
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException)
            {
                SearchFault = e.Message;
                return;
            }
        }

        SearchFault = "";
        _search.Ask();
    }

    internal void StopSearch()
    {
        _search?.Dispose();
        _search = null;
    }

    internal void ClearFault() => SearchFault = "";

    internal void Poll() => _search?.Poll();

    // A second door on this machine finds the discovery port taken. It still hosts; it only
    // goes unanswered on the LAN, and a guest can still type its address.
    internal void Answer(string bind, NetBuildVersion version)
    {
        if (_lan == null)
        {
            return;
        }

        try
        {
            _responder = new LanResponder(_lan(bind, NetPorts.Lan), version);
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        {
            _responder = null;
        }
    }

    internal void AnswerWith(SessionAdvertMessage advert, int port) => _responder?.Poll(advert, port);

    internal void StopAnswering()
    {
        _responder?.Dispose();
        _responder = null;
    }

    private IReadOnlyList<string> SearchTargets() =>
        SearchAddress == BroadcastAddress && Networks != null
            ? LanBroadcast.Targets(Networks())
            : new[] { SearchAddress };
}
