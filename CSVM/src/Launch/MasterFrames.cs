using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CSVM.Net;

namespace CSVM.Launch;

/// <summary>How one read of a whole message off a master server socket ended.</summary>
public enum MasterFrameEnd
{
    /// <summary>The message arrived whole.</summary>
    Whole,

    /// <summary>The other end closed the socket.</summary>
    Closed,

    /// <summary>The message ran past <see cref="MasterWire.MaxMessageBytes"/>.</summary>
    TooBig,
}

/// <summary>One message read off a master server socket by <see cref="MasterFrames.ReceiveAsync"/>.
/// <see cref="Message"/> is null unless a whole message read as one, whatever its
/// <see cref="Type"/>. Each end decides for itself whether a binary message counts.</summary>
public readonly record struct MasterFrame(MasterFrameEnd End, WebSocketMessageType Type, MasterMessage? Message);

/// <summary>
/// The framed read of a master server socket, the same at both ends: the game's
/// <c>MasterServerLink</c> and the server's own socket loop. The server project compiles this file
/// beside <c>MasterProtocol.cs</c>. It lives outside <c>CSVM.Net</c> because the transport seam
/// names no socket type. It names no engine type, which is what lets the server build it.
/// </summary>
public static class MasterFrames
{
    /// <summary>Reads one whole message off <paramref name="socket"/> into <paramref name="buffer"/>,
    /// which holds <see cref="MasterWire.MaxMessageBytes"/>. A message that fills the buffer before
    /// its end reads as <see cref="MasterFrameEnd.TooBig"/> with its rest unread. The caller then
    /// closes the socket rather than reading on.</summary>
    public static async Task<MasterFrame> ReceiveAsync(WebSocket socket, byte[] buffer, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(buffer);
        int length = 0;
        WebSocketReceiveResult result;
        do
        {
            if (length == buffer.Length)
            {
                return new MasterFrame(MasterFrameEnd.TooBig, WebSocketMessageType.Text, null);
            }

            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), token)
                .ConfigureAwait(false);
            length += result.Count;
        }
        while (!result.EndOfMessage && result.MessageType != WebSocketMessageType.Close);

        if (result.MessageType == WebSocketMessageType.Close)
        {
            return new MasterFrame(MasterFrameEnd.Closed, result.MessageType, null);
        }

        return new MasterFrame(MasterFrameEnd.Whole, result.MessageType,
            MasterWire.TryRead(Encoding.UTF8.GetString(buffer, 0, length), out var message) ? message : null);
    }
}
