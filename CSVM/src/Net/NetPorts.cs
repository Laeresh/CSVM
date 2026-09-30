using System;

namespace CSVM.Net;

/// <summary>
/// The ports this process opens by default. The game port is where a door hosts and a bare join
/// aims; the LAN discovery port stands one above it. Both stand at the shipped pair unless
/// <c>--net-port-base=</c> moves the pair, which is how concurrent test processes keep off each
/// other's sockets. The shipped constants stay for what names the port every build fills in.
/// ⚠ Set once at launch, before any door or suite opens a socket; nothing reads it for a change.
/// </summary>
public static class NetPorts
{
    /// <summary>The shipped game port. Unregistered and arbitrary: the original carried no port
    /// of its own, since DirectPlay chose one.</summary>
    public const int ShippedGame = 47500;

    /// <summary>How far above the game port the LAN discovery port stands.</summary>
    public const int LanOffset = 1;

    /// <summary>The ports a base reserves from itself upward. A test process's suites take theirs
    /// inside it, so two processes a block apart never meet.</summary>
    public const int Block = 100;

    /// <summary>The lowest base: the privileged ports below it are not a player's to open.</summary>
    public const int MinBase = 1024;

    /// <summary>The highest base whose whole block is still a port.</summary>
    public const int MaxBase = 65535 - Block + 1;

    /// <summary>The effective game port, <see cref="ShippedGame"/> unless <see cref="Use"/>
    /// moved it.</summary>
    public static int Base { get; private set; } = ShippedGame;

    /// <summary>The port a door hosts on until its port row is stepped, and the one a bare join
    /// aims at.</summary>
    public static int Game => Base;

    /// <summary>The port a LAN responder listens on and a LAN search asks at.</summary>
    public static int Lan => Base + LanOffset;

    /// <summary>Whether <paramref name="value"/> can be a base.</summary>
    public static bool IsBase(int value) => value is >= MinBase and <= MaxBase;

    /// <summary>Moves the pair to <paramref name="value"/>. Throws outside
    /// [<see cref="MinBase"/>, <see cref="MaxBase"/>].</summary>
    public static void Use(int value)
    {
        if (!IsBase(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, $"a port base lies in [{MinBase}, {MaxBase}]");
        }

        Base = value;
    }
}
