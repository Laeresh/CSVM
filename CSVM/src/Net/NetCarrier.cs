using System;
using CSVM.Utils;

namespace CSVM.Net;

/// <summary>
/// Which carrier a match runs over, chosen once. The menu door the launcher registers and the
/// command line's own open both come through here, so no edit above the seam changes carrier. A
/// match runs over ENet by direct IP, and a master server adds WebRTC beside it for the door.
/// ⚠ Nothing above the seam may ask which carrier it has. A carrier that will not open throws,
/// and a board shows that the way it shows a taken port.
/// </summary>
public static class NetCarrier
{
    private static readonly Func<int, UpnpPortMapResult> MapPort = port => UpnpPortMap.Map(port);
    private static readonly Action<int> UnmapPort = port => UpnpPortMap.Unmap(port);
    private static readonly Func<string, int, ILanSocket> BindLan = (bind, port) => LanDiscoverySocket.Bind(port, bind);
    private static readonly Action<int> ClosePinhole = port => UpnpPinholeMap.Close(port);

    /// <summary>The selected carrier as one word, for a log line and a board.</summary>
    public static string Name => "enet";

    /// <summary>The router door a direct-IP host asks for, or null for a carrier that is reachable
    /// without one. A door handed null simply shows no mapping.</summary>
    public static Func<int, UpnpPortMapResult>? PortMap => MapPort;

    /// <summary>The way that mapping comes back down, or null when none was asked for.</summary>
    public static Action<int>? PortUnmap => UnmapPort;

    /// <summary>The way a pinhole from <see cref="Pinhole"/> comes back down, or null when none was
    /// asked for.</summary>
    public static Action<int>? PinholeClose => ClosePinhole;

    /// <summary>The LAN search's socket, bound on an address and a port, or null for a carrier
    /// that finds its games another way. A door handed null offers no search.</summary>
    public static Func<string, int, ILanSocket>? Lan => BindLan;

    /// <summary>Reads the stable global IPv6 address a guest dials this host at, or null for a
    /// carrier not reached by address. A door handed null names no address.</summary>
    public static Func<string?>? StableIpv6 => HostAddress.StableGlobalIPv6;

    /// <summary>Reads this host's local IPv4 address, or null for a carrier not reached by address.
    /// </summary>
    public static Func<string?>? LanIpv4 => HostAddress.LanIPv4;

    /// <summary>The router's IPv6 pinhole a direct-IP host asks for, opened for the address
    /// <paramref name="address"/> names on each call, or null for a carrier reachable without one.
    /// </summary>
    public static Func<int, UpnpPinholeResult>? Pinhole(Func<string?> address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return port => UpnpPinholeMap.Open(port, address());
    }

    /// <summary>Opens a listen server for <paramref name="maxGuests"/> guests on
    /// <paramref name="port"/> over the selected carrier. <paramref name="bindAddress"/> is the
    /// direct-IP interface. Throws what that carrier throws when the socket will not open.</summary>
    public static INetTransport Host(int port, int maxGuests, string bindAddress = "*") =>
        EnetTransport.Host(port, maxGuests, bindAddress);

    /// <summary>Starts a join to <paramref name="address"/> on <paramref name="port"/> over the
    /// selected carrier.</summary>
    public static INetTransport Join(string address, int port) => EnetTransport.Join(address, port);

    /// <summary>The menu door's host: <see cref="Host"/>, and beside it as one carrier a listed
    /// WebRTC host over sockets <paramref name="master"/> opens. That needs a master server and the
    /// WebRTC extension; without either it is <see cref="Host"/> alone and nothing else changes.
    /// </summary>
    public static INetTransport HostListed(int port, int maxGuests, string bindAddress, Func<IMasterSocket>? master)
    {
        var direct = Host(port, maxGuests, bindAddress);
        if (master == null)
        {
            return direct;
        }

        if (!WebRtcTransport.Available)
        {
            Log.Warn("core", $"net: a master server is set but this build has no WebRTC library; hosting for LAN and direct guests only");
            return direct;
        }

        try
        {
            return new MergedTransport(direct, WebRtcTransport.Host(master, maxGuests));
        }
        catch (InvalidOperationException e)
        {
            Log.Warn("core", $"net: the WebRTC host would not open ({e.Message}); hosting for LAN and direct guests only");
            return direct;
        }
    }

    /// <summary>Starts a join to the game the master server lists under <paramref name="code"/>,
    /// over a socket <paramref name="master"/> opens. Throws when this build has no WebRTC library
    /// or the code is not one.</summary>
    public static INetTransport JoinCode(Func<IMasterSocket> master, string code, NetBuildVersion version)
    {
        ArgumentNullException.ThrowIfNull(master);
        if (!WebRtcTransport.Available)
        {
            throw new InvalidOperationException("joining by code needs the WebRTC library, which this build lacks (run InstallWebRtc.ps1)");
        }

        return WebRtcTransport.Join(master(), code, version);
    }
}
