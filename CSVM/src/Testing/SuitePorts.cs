using CSVM.Net;

namespace CSVM.Testing;

/// <summary>
/// Where each suite that opens a real socket opens it: an offset into this process's port block,
/// <see cref="NetPorts.Base"/> up to <see cref="NetPorts.Block"/> ports above it. The test runner
/// hands every engine shard its own base, so two shards never bind the same port. The offsets here
/// keep one shard's suites apart, and a suite takes its range from this table, never a literal.
/// ⚠ Do not share a port on the strength of a walk. A busy port prints an engine error line even
/// when the walk then binds. The battery fails the shard on that line, so a walk guards against a
/// foreign process only.
/// </summary>
internal static class SuitePorts
{
    /// <summary>How many ports a suite's walk tries before it reports the socket would not open.
    /// </summary>
    internal const int Walk = 5;

    /// <summary>The Built-in board's port row, walked one Right at a time from the door's own
    /// default: offsets 1 to 24. The LAN port is offset 1, which none of these doors opens.
    /// </summary>
    internal const int DoorWalk = 24;

    /// <summary>enet-transport's host.</summary>
    internal const int Transport = 30;

    /// <summary>enet-load-stall's three hosts, one after another.</summary>
    internal const int Stall = 35;

    /// <summary>net-enet-join's host.</summary>
    internal const int EnetJoin = 40;

    /// <summary>menu-original-ipv6-address's host on the IPv6 loopback.</summary>
    internal const int Ipv6Join = 45;

    /// <summary>enet-dual-stack's two-socket host, and its IPv4-only control after it.</summary>
    internal const int DualStack = 50;

    /// <summary>enet-stable-ipv6-reply's shipped-shape host, and its wildcard control after it.
    /// </summary>
    internal const int StableReply = 60;

    /// <summary>enet-shaped-link's host.</summary>
    internal const int Shaped = 70;

    /// <summary>How far above its host's port a guest that names its own source port sends from.
    /// Lands the stable-reply guests on offsets 80 to 89.</summary>
    internal const int GuestSource = 20;

    /// <summary>The port at <paramref name="offset"/> in this process's block.</summary>
    internal static int At(int offset) => NetPorts.Base + offset;
}
