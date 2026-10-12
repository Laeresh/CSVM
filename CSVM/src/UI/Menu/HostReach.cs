using System;
using CSVM.Net;

namespace CSVM.UI.Menu;

/// <summary>
/// What a host hands its guests to reach it. That is the address a guest types, read from this
/// machine as a host opens, and the clipboard copy of it or the join code. The machine's addresses
/// and the clipboard arrive as delegates. The door tells it when a host opens and when it shuts;
/// the router and the internet door are the door's own.
/// </summary>
public sealed class HostReach
{
    private readonly RouterAccess _router;
    private readonly InternetDoor _internet;
    private readonly Func<int> _port;
    private readonly Func<bool> _hosting;

    /// <summary>Reach for a door whose router is <paramref name="router"/>, whose master server
    /// link is <paramref name="internet"/>, whose port <paramref name="port"/> reads, and which
    /// <paramref name="hosting"/> says is hosting.</summary>
    public HostReach(RouterAccess router, InternetDoor internet, Func<int> port, Func<bool> hosting)
    {
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _internet = internet ?? throw new ArgumentNullException(nameof(internet));
        _port = port ?? throw new ArgumentNullException(nameof(port));
        _hosting = hosting ?? throw new ArgumentNullException(nameof(hosting));
    }

    /// <summary>Reads this machine's stable global IPv6 address when a host opens, or null for a
    /// carrier that is not reached by address. With none, a board names no address at all. Set
    /// once, before the door opens.</summary>
    public Func<string?>? StableIpv6 { get; set; }

    /// <summary>Reads this machine's address on its local IPv4 network when a host opens. Set once,
    /// before the door opens.</summary>
    public Func<string?>? LanIpv4 { get; set; }

    /// <summary>Puts text on the system clipboard, the host's copy of its address. A seam, so a
    /// suite reads the copy without writing the pilot's own clipboard. Set once, before the door
    /// opens.</summary>
    public Action<string>? CopyText { get; set; }

    /// <summary>This host's stable global IPv6 address as read when it opened, or null.</summary>
    public string? HostIpv6 { get; private set; }

    /// <summary>This host's local IPv4 address as read when it opened, or null.</summary>
    public string? HostLanIpv4 { get; private set; }

    /// <summary>Whether this door can name its host's address, which a board shows only then.
    /// </summary>
    public bool NamesHostAddress => StableIpv6 != null;

    /// <summary>How many times this host's address or code was copied since it opened.</summary>
    public int Copies { get; private set; }

    /// <summary>The text this host last put on the clipboard since it opened, or "". A board marks
    /// a code or an address copied only while it is the one shown.</summary>
    public string Copied { get; private set; } = "";

    /// <summary>What a guest types to reach this host: the stable IPv6 address, else the router's
    /// mapped IPv4 address, else the LAN address. The port is written when it is not
    /// <see cref="NetPlayFeature.DefaultPort"/>, and always for a mapping. Empty while not hosting
    /// or when none is known.</summary>
    public string GuestAddress
    {
        get
        {
            if (!_hosting())
            {
                return "";
            }

            if (HostIpv6 is { } v6)
            {
                return Dial(v6);
            }

            if (_router.PortMap is { IsMapped: true } map)
            {
                return new NetEndpoint(map.ExternalAddress, map.Port).ToString();
            }

            return HostLanIpv4 is { } lan ? Dial(lan) : "";
        }
    }

    /// <summary><paramref name="host"/> as a guest types it for the door's port: bare on
    /// <see cref="NetPlayFeature.DefaultPort"/>, which a join fills in, and with the port otherwise.
    /// </summary>
    public string Dial(string host) =>
        _port() == NetPlayFeature.DefaultPort ? host : new NetEndpoint(host, _port()).ToString();

    /// <summary>Copies <see cref="GuestAddress"/> to the clipboard. False, and nothing copied,
    /// while there is no address to give or no clipboard to put it on.</summary>
    public bool CopyGuestAddress() => Copy(GuestAddress);

    /// <summary>Copies what a guest outside this network needs: the join code once the master server
    /// gave one, else <see cref="GuestAddress"/>. The copy key's action, so one key serves a host
    /// with a code and one without. Nothing while the code is awaited, when no line names the
    /// address.</summary>
    public bool CopyForGuests() =>
        _internet.JoinCode is { } code ? Copy(code) : !_internet.AwaitingCode && CopyGuestAddress();

    // Read once as the host opens, so a board shows the address the guests were told.
    internal void Open()
    {
        HostIpv6 = StableIpv6?.Invoke();
        HostLanIpv4 = LanIpv4?.Invoke();
        Copies = 0;
    }

    internal void Forget()
    {
        HostIpv6 = null;
        HostLanIpv4 = null;
        Copies = 0;
        Copied = "";
    }

    private bool Copy(string text)
    {
        if (!_hosting() || text.Length == 0 || CopyText == null)
        {
            return false;
        }

        CopyText(text);
        Copies++;
        Copied = text;
        return true;
    }
}
