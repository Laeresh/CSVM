using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CSVM.Net;
using CSVM.Utils;

namespace CSVM.Launch;

/// <summary>
/// The shipped way to the master server, over .NET's HTTP and WebSocket clients. It runs off the
/// frame, and a unit drives it against an in-memory server. The games list is one GET. The socket
/// is an <see cref="IMasterSocket"/> whose receive and send loops run on the thread pool. Queues
/// stand between them and the frame that polls it. No other game code connects to the master
/// server; it sits here because the network seam may name no socket API.
/// </summary>
public static class MasterServerLink
{
    /// <summary>How long a games list request may take, in seconds.</summary>
    public const double FetchSeconds = 5.0;

    /// <summary>How long a socket may take to open, in seconds.</summary>
    public const double ConnectSeconds = 10.0;

    /// <summary>How long the socket keeps a silent host listed on its own, in seconds. Long enough
    /// for a mission load, never forever for a game that hung.</summary>
    public const double KeepListedSeconds = 300.0;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(FetchSeconds) };

    /// <summary>Fetches the games list from <paramref name="server"/>, as text.</summary>
    public static Task<string> FetchGames(Uri server, CancellationToken cancel) => FetchGames(Http, server, cancel);

    /// <summary>Fetches the games list from <paramref name="server"/> through
    /// <paramref name="http"/>, which a unit points at an in-memory server.</summary>
    public static async Task<string> FetchGames(HttpClient http, Uri server, CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(http);
        using var answer = await http.GetAsync(MasterAddress.At(server, MasterWire.GamesPath), cancel).ConfigureAwait(false);
        answer.EnsureSuccessStatusCode();
        return await answer.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
    }

    /// <summary>Opens a socket to <paramref name="server"/>. It returns at once, connecting.</summary>
    public static IMasterSocket Open(Uri server)
    {
        var at = MasterAddress.At(server, MasterWire.SocketPath, socket: true);
        return Open(async cancel =>
        {
            var socket = new ClientWebSocket();
            socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            await socket.ConnectAsync(at, cancel).ConfigureAwait(false);
            return socket;
        });
    }

    /// <summary>Opens a socket through <paramref name="connect"/>, which a unit hands an in-memory
    /// server's WebSocket client.</summary>
    public static IMasterSocket Open(Func<CancellationToken, Task<WebSocket>> connect) => new Socket(connect);

    // One socket and its two loops. Everything the frame touches is a queue or a volatile word.
    private sealed class Socket : IMasterSocket
    {
        private readonly CancellationTokenSource _cancel = new();
        private readonly Channel<MasterMessage> _outbox = Channel.CreateUnbounded<MasterMessage>();
        private readonly ConcurrentQueue<MasterMessage> _inbox = new();
        private volatile MasterSocketState _state = MasterSocketState.Connecting;
        private volatile string _fault = "";

        public Socket(Func<CancellationToken, Task<WebSocket>> connect) => _ = Run(connect);

        public MasterSocketState State => _state;

        public string Fault => _fault;

        public void Send(MasterMessage message)
        {
            if (_state != MasterSocketState.Closed)
            {
                _outbox.Writer.TryWrite(message);
            }
        }

        public bool TryReceive(out MasterMessage message) => _inbox.TryDequeue(out message!);

        public void Dispose()
        {
            _outbox.Writer.TryComplete();
            _cancel.Cancel();
            _state = MasterSocketState.Closed;
        }

        private async Task Run(Func<CancellationToken, Task<WebSocket>> connect)
        {
            WebSocket? socket = null;
            try
            {
                using (var opening = CancellationTokenSource.CreateLinkedTokenSource(_cancel.Token))
                {
                    opening.CancelAfter(TimeSpan.FromSeconds(ConnectSeconds));
                    socket = await connect(opening.Token).ConfigureAwait(false);
                }

                if (_cancel.IsCancellationRequested)
                {
                    return;
                }

                _state = MasterSocketState.Open;
                var sending = SendLoop(socket);
                await ReceiveLoop(socket).ConfigureAwait(false);
                _outbox.Writer.TryComplete();
                await sending.ConfigureAwait(false);
            }
            catch (Exception e) when (e is WebSocketException or OperationCanceledException or HttpRequestException
                                       or InvalidOperationException or ArgumentException)
            {
                if (!_cancel.IsCancellationRequested)
                {
                    _fault = $"the master server is unreachable: {e.Message}";
                }
            }
            finally
            {
                _state = MasterSocketState.Closed;
                if (socket != null)
                {
                    await Shut(socket).ConfigureAwait(false);
                    socket.Dispose();
                }
            }
        }

        private async Task ReceiveLoop(WebSocket socket)
        {
            var buffer = new byte[MasterWire.MaxMessageBytes];
            while (socket.State == WebSocketState.Open && !_cancel.IsCancellationRequested)
            {
                int length = 0;
                WebSocketReceiveResult result;
                do
                {
                    if (length == buffer.Length)
                    {
                        _fault = "the master server sent a message past the size limit";
                        return;
                    }

                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), _cancel.Token)
                        .ConfigureAwait(false);
                    length += result.Count;
                }
                while (!result.EndOfMessage && result.MessageType != WebSocketMessageType.Close);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _fault = "the master server closed the connection";
                    return;
                }

                if (MasterWire.TryRead(Encoding.UTF8.GetString(buffer, 0, length), out var message))
                {
                    _inbox.Enqueue(message);
                }
            }
        }

        // A host's listing is repeated from here after a heartbeat with nothing from the frame.
        // That keeps the game listed through a mission load that stops the frame.
        private async Task SendLoop(WebSocket socket)
        {
            MasterMessage? listing = null;
            var quietSince = DateTime.UtcNow;
            var reader = _outbox.Reader;
            while (true)
            {
                using var beat = CancellationTokenSource.CreateLinkedTokenSource(_cancel.Token);
                beat.CancelAfter(TimeSpan.FromSeconds(MasterWire.HeartbeatSeconds));
                MasterMessage message;
                try
                {
                    if (!await reader.WaitToReadAsync(beat.Token).ConfigureAwait(false))
                    {
                        return;
                    }

                    if (!reader.TryRead(out message!))
                    {
                        continue;
                    }

                    quietSince = DateTime.UtcNow;
                    if (message.T is MasterWire.Host or MasterWire.Update)
                    {
                        listing = message;
                    }
                }
                catch (OperationCanceledException) when (!_cancel.IsCancellationRequested)
                {
                    if (listing == null || (DateTime.UtcNow - quietSince).TotalSeconds > KeepListedSeconds)
                    {
                        continue;
                    }

                    message = new MasterMessage { T = MasterWire.Update, Game = listing.Game, Protocol = listing.Protocol };
                }

                byte[] text = Encoding.UTF8.GetBytes(MasterWire.Write(message));
                await socket.SendAsync(text, WebSocketMessageType.Text, true, _cancel.Token).ConfigureAwait(false);
            }
        }

        private async Task Shut(WebSocket socket)
        {
            if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
            {
                return;
            }

            using var shut = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", shut.Token).ConfigureAwait(false);
            }
            catch (Exception e) when (e is WebSocketException or OperationCanceledException or InvalidOperationException)
            {
            }
        }
    }
}
