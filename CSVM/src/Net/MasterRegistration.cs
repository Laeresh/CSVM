using System;

namespace CSVM.Net;

/// <summary>
/// A host's listing on the master server, engine-free: when to send it and what the server said
/// back. The first listing goes out as a host message once the socket opens. After that a changed
/// listing goes out at once, and an unchanged one every <see cref="MasterWire.HeartbeatSeconds"/>.
/// The repeat is what keeps the game on the list. The carrier hands it the messages meant for it
/// and steps it with its own step.
/// </summary>
public sealed class MasterRegistration
{
    private readonly IMasterSocket _socket;
    private MasterGame? _listing;
    private string _sent = "";
    private bool _registered;
    private double _sinceSent;
    private string _refused = "";

    /// <summary>A registration over <paramref name="socket"/>, which it does not own.</summary>
    public MasterRegistration(IMasterSocket socket) => _socket = socket ?? throw new ArgumentNullException(nameof(socket));

    /// <summary>The code the server listed the game under, or null before it answered.</summary>
    public string? Code { get; private set; }

    /// <summary>Why the game is not listed, as a player reads it, or "" while nothing went wrong.
    /// </summary>
    public string Fault => _socket.State == MasterSocketState.Closed && _socket.Fault.Length > 0 ? _socket.Fault : _refused;

    /// <summary>How many listings have gone to the server, the first included.</summary>
    public int Sent { get; private set; }

    /// <summary>The listing the game should carry from now on. Sent on the next step when it
    /// differs from the last one sent.</summary>
    public void List(MasterGame listing)
    {
        ArgumentNullException.ThrowIfNull(listing);
        _listing = MasterWire.Clean(listing);
    }

    /// <summary>Takes a message from the server. True when it was this registration's to take: the
    /// code, or an error refusing the game.</summary>
    public bool Take(MasterMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.T == MasterWire.Hosted && MasterWire.TryCode(message.Code, out string code))
        {
            Code = code;
            _refused = "";
            return true;
        }

        if (message.T == MasterWire.Error && Code == null)
        {
            _refused = message.Why ?? "the master server refused the game";
            return true;
        }

        return false;
    }

    /// <summary>Advances by <paramref name="dt"/> seconds and sends what is due.</summary>
    public void Step(double dt)
    {
        _sinceSent += dt;
        if (_listing == null || _socket.State != MasterSocketState.Open)
        {
            return;
        }

        string text = MasterWire.Write(new MasterMessage { T = MasterWire.Update, Game = _listing });
        if (_registered && text == _sent && _sinceSent < MasterWire.HeartbeatSeconds)
        {
            return;
        }

        _socket.Send(new MasterMessage { T = _registered ? MasterWire.Update : MasterWire.Host, Game = _listing });
        _registered = true;
        _sent = text;
        _sinceSent = 0.0;
        Sent++;
    }
}
