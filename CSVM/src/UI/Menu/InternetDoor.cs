using System;
using CSVM.Net;

namespace CSVM.UI.Menu;

/// <summary>
/// The door's link to the master server. A guest reads the internet games list from
/// <see cref="Master"/> and joins a listed game by its code through <see cref="OpenCode"/>. A host
/// hands its carrier's listing the advert and shows the code the server gave it, or why it has none.
/// The door tells it when a host opens with a listing and when the door shuts.
/// </summary>
public sealed class InternetDoor
{
    /// <summary>How long a join by code may stand at <see cref="NetDoorStage.Joining"/>. The
    /// carrier gives up first with its own reason; this bounds a carrier that never says.</summary>
    public const double CodeJoinTimeoutSeconds = 30.0;

    private readonly Func<bool> _hosting;
    private INetListing? _listing;

    /// <summary>The link of a door which <paramref name="hosting"/> says is hosting.</summary>
    public InternetDoor(Func<bool> hosting) =>
        _hosting = hosting ?? throw new ArgumentNullException(nameof(hosting));

    /// <summary>The master server's half of the games list, or null when no master server is set,
    /// which leaves the list to the LAN search alone. Set once, before the door opens.</summary>
    public MasterDirectory? Master { get; set; }

    /// <summary>Opens a join to the game the master server lists under a code, or null when no
    /// master server is set. A typed address in a code's form joins through this. Set once, before
    /// the door opens.</summary>
    public Func<string, INetTransport>? OpenCode { get; set; }

    /// <summary>Whether this build can open the WebRTC carrier a join by code rides. The launcher
    /// reads the library's presence; a suite's loopback code opener needs none. Set once, before the
    /// door opens.</summary>
    public bool WebRtcReady { get; set; } = true;

    /// <summary>Why a guest cannot join by code here, no master server set or no WebRTC carrier,
    /// or "" while it can.</summary>
    public string CodeFault => OpenCode == null ? CoopDoorText.NoMasterServer : !WebRtcReady ? CoopDoorText.NoWebRtc : "";

    /// <summary>The code the join under way was opened by, or null for a join by address.</summary>
    public string? GuestCode { get; private set; }

    /// <summary>The code the master server listed this host's game under, or null.</summary>
    public string? JoinCode => _hosting() ? _listing?.JoinCode : null;

    /// <summary>Why this host's game is not on the master server's list, or "" while it is or no
    /// master server is set.</summary>
    public string ListingFault => _hosting() ? _listing?.ListingFault ?? "" : "";

    /// <summary>Why internet guests cannot reach this host by code. It is "" while they can, while
    /// the code is on its way, and with no master server set. A host with a master server but no
    /// listing carrier is one whose WebRTC library is missing or would not start.</summary>
    public string InternetFault =>
        !_hosting() || JoinCode != null ? "" : _listing == null ? Master != null ? CoopDoorText.NoWebRtc : "" : ListingFault;

    /// <summary>Whether this host is listing on the master server and has no code or fault yet.
    /// </summary>
    public bool AwaitingCode => _hosting() && _listing != null && JoinCode == null && ListingFault.Length == 0;

    /// <summary>Whether the open master server list says the server no longer serves this build. Its
    /// games stay off the list until the game is updated.</summary>
    public bool MasterOutdated => Master is { Asking: true, Outdated: true };

    // A typed address in a code's written form, dash included, joins by code when a master server
    // is set. The dash is what keeps a six-letter host name from reading as a code.
    internal bool TypedCode(string address, out string code)
    {
        code = "";
        return OpenCode != null && address.Contains('-', StringComparison.Ordinal) && MasterWire.TryCode(address, out code);
    }

    // The Join code box's text, read as MasterWire.TryCode reads it, while a code opener is set.
    internal bool TryCode(string typed, out string code)
    {
        code = "";
        return OpenCode != null && MasterWire.TryCode(typed, out code);
    }

    // The carrier of a join to the game listed under code, which the join's name then gives.
    internal INetTransport Join(string code)
    {
        var carrier = OpenCode!(code);
        GuestCode = code;
        return carrier;
    }

    // The host's carrier, which lists the game when it can.
    internal void Host(INetTransport carrier) => _listing = carrier as INetListing;

    internal void List(SessionAdvertMessage advert, NetBuildVersion version, bool unlisted) =>
        _listing?.List(MasterDirectory.ListingOf(advert, version, unlisted));

    internal void Shut()
    {
        _listing = null;
        GuestCode = null;
    }
}
