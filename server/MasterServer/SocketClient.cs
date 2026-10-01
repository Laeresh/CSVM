using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CSVM.Net;

namespace CSVM.Master;

/// <summary>
/// One accepted WebSocket and its two loops: the receive loop reads whole text messages up to
/// <see cref="MasterWire.MaxMessageBytes"/>, holds the sender to its message bucket and hands each
/// message to the hub; the send loop writes what the hub queued. Anything past a cap closes the
/// socket rather than being skipped, since a well-behaved game never sends it.
/// </summary>
public sealed class SocketClient : IMasterClient
{
    /// <summary>How many messages may wait to be sent before the socket is judged stuck.</summary>
    public const int OutboxDepth = 64;

    /// <summary>How long a socket may stay silent before it is closed, in seconds: twice a
    /// listing's expiry, so a host is dropped from the list first.</summary>
    public const double IdleSeconds = 2 * MasterWire.ExpirySeconds;

    private readonly WebSocket _socket;
    private readonly MasterHub _hub;
    private readonly MasterOptions _options;
    private readonly TimeProvider _time;
    private readonly Channel<string> _outbox = Channel.CreateBounded<string>(OutboxDepth);
    private readonly CancellationTokenSource _closing = new();
    private double _tokens;
    private DateTimeOffset _filled;

    private SocketClient(WebSocket socket, string address, MasterHub hub, MasterOptions options, TimeProvider time)
    {
        _socket = socket;
        Address = address;
        _hub = hub;
        _options = options;
        _time = time;
        _tokens = options.MessageBurst;
        _filled = time.GetUtcNow();
    }

    /// <inheritdoc/>
    public string Address { get; }

    /// <summary>Runs <paramref name="socket"/> until either end closes it, counting it against
    /// <paramref name="address"/> in <paramref name="open"/> meanwhile.</summary>
    public static async Task Run(
        WebSocket socket, string address, MasterHub hub, MasterOptions options, TimeProvider time,
        ConcurrentDictionary<string, int> open, CancellationToken stopping)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ArgumentNullException.ThrowIfNull(open);
        var client = new SocketClient(socket, address, hub, options, time);
        try
        {
            await client.Serve(stopping).ConfigureAwait(false);
        }
        finally
        {
            hub.Closed(client);
            open.AddOrUpdate(address, 0, (_, count) => Math.Max(0, count - 1));
        }
    }

    /// <inheritdoc/>
    public void Send(MasterMessage message)
    {
        if (!_outbox.Writer.TryWrite(MasterWire.Write(message)))
        {
            _closing.Cancel();
        }
    }

    /// <inheritdoc/>
    public void Close(string why)
    {
        _outbox.Writer.TryWrite(MasterWire.Write(new MasterMessage { T = MasterWire.Error, Why = why }));
        _outbox.Writer.TryComplete();
    }

    private async Task Serve(CancellationToken stopping)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stopping, _closing.Token);
        var sending = SendLoop(linked.Token);
        var (status, why) = (WebSocketCloseStatus.NormalClosure, "bye");
        try
        {
            (status, why) = await ReceiveLoop(linked.Token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is OperationCanceledException or WebSocketException)
        {
        }
        finally
        {
            _outbox.Writer.TryComplete();
            try
            {
                await sending.ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException or WebSocketException or InvalidOperationException)
            {
            }

            if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                using var shut = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try
                {
                    await _socket.CloseAsync(status, why, shut.Token).ConfigureAwait(false);
                }
                catch (Exception e) when (e is OperationCanceledException or WebSocketException)
                {
                }
            }
        }
    }

    private async Task<(WebSocketCloseStatus Status, string Why)> ReceiveLoop(CancellationToken token)
    {
        var buffer = new byte[MasterWire.MaxMessageBytes];
        while (!token.IsCancellationRequested && _socket.State == WebSocketState.Open)
        {
            int length = 0;
            WebSocketReceiveResult result;
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(token);
            idle.CancelAfter(TimeSpan.FromSeconds(IdleSeconds));
            do
            {
                if (length == buffer.Length)
                {
                    return (WebSocketCloseStatus.MessageTooBig, "too big");
                }

                result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), idle.Token)
                    .ConfigureAwait(false);
                length += result.Count;
            }
            while (!result.EndOfMessage && result.MessageType != WebSocketMessageType.Close);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return (WebSocketCloseStatus.NormalClosure, "bye");
            }

            if (result.MessageType != WebSocketMessageType.Text || !Spend())
            {
                return (WebSocketCloseStatus.PolicyViolation, "refused");
            }

            if (MasterWire.TryRead(Encoding.UTF8.GetString(buffer, 0, length), out var message))
            {
                _hub.Receive(this, message);
            }
            else
            {
                Send(new MasterMessage { T = MasterWire.Error, Why = "that is not a message" });
            }
        }

        return (WebSocketCloseStatus.NormalClosure, "bye");
    }

    private async Task SendLoop(CancellationToken token)
    {
        await foreach (string text in _outbox.Reader.ReadAllAsync(token).ConfigureAwait(false))
        {
            if (_socket.State != WebSocketState.Open)
            {
                return;
            }

            await _socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
        }

        // The outbox completes on Close; the socket follows once the last message has gone.
        _closing.Cancel();
    }

    // A token bucket: MessageBurst deep, refilled at MessagesPerSecond.
    private bool Spend()
    {
        var now = _time.GetUtcNow();
        _tokens = Math.Min(_options.MessageBurst, _tokens + ((now - _filled).TotalSeconds * _options.MessagesPerSecond));
        _filled = now;
        if (_tokens < 1.0)
        {
            return false;
        }

        _tokens -= 1.0;
        return true;
    }
}
