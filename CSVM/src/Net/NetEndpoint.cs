using System;
using System.Globalization;

namespace CSVM.Net;

/// <summary>A host and the port a join opens on. It is parsed from an address as a player types it
/// or a command line names it. <see cref="ToString"/> writes it back the way a player would.</summary>
public readonly record struct NetEndpoint(string Host, int Port)
{
    /// <summary>Splits an address into the host and the port it names. An IPv6 address names its
    /// port after a closing bracket. A bare address with more than one colon is all host, so its
    /// last group is never read as a port. One colon splits a host from its port.
    /// A port that is missing or out of range takes <paramref name="fallbackPort"/>.</summary>
    public static NetEndpoint Parse(string? value, int fallbackPort)
    {
        string text = value ?? "";
        int PortOf(string field) =>
            int.TryParse(field, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port)
            && port is > 0 and < 65536
                ? port
                : fallbackPort;

        int bracket = text.IndexOf("]:", StringComparison.Ordinal);
        if (text.StartsWith('[') && bracket > 0)
        {
            return new NetEndpoint(text[1..bracket], PortOf(text[(bracket + 2)..]));
        }

        int colon = text.LastIndexOf(':');
        if (colon > 0 && text.IndexOf(':') == colon)
        {
            return new NetEndpoint(text[..colon], PortOf(text[(colon + 1)..]));
        }

        return new NetEndpoint(text.Trim('[', ']'), fallbackPort);
    }

    /// <summary>The host and port written back as one address, bracketing a host with a colon in it.
    /// </summary>
    public override string ToString()
    {
        string host = Host ?? "";
        string number = Port.ToString(CultureInfo.InvariantCulture);
        return host.Contains(':', StringComparison.Ordinal) ? $"[{host}]:{number}" : $"{host}:{number}";
    }
}
