using System;

namespace CSVM.Net;

/// <summary>Where a socket to the master server stands.</summary>
public enum MasterSocketState
{
    /// <summary>Opening. A message sent now waits until it opens.</summary>
    Connecting,

    /// <summary>Open both ways.</summary>
    Open,

    /// <summary>Closed, refused, or never reached. Nothing will arrive again.</summary>
    Closed,
}

/// <summary>
/// The master server's socket as the carriers speak it: whole messages either way, polled from the
/// frame that steps the carrier. The shipped one is <c>Utils/MasterServerLink.cs</c>'s, over .NET's
/// WebSocket client, and a suite passes an in-process one. Nothing here names a socket API.
/// </summary>
public interface IMasterSocket : IDisposable
{
    /// <summary>Where the socket stands.</summary>
    MasterSocketState State { get; }

    /// <summary>Why the socket closed or never opened, as a player reads it, or "" while it has not.
    /// </summary>
    string Fault { get; }

    /// <summary>Queues one message, sent in order once the socket is open. Dropped once closed.</summary>
    void Send(MasterMessage message);

    /// <summary>The next message that arrived, or false when none waits.</summary>
    bool TryReceive(out MasterMessage message);
}
