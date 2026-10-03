using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Net;

namespace CSVM.UI.Menu;

/// <summary>Which device a host's copy hint names: the one the menu's seat last moved. A keyboard
/// copies on <see cref="CoopDoorText.CopyPress"/>; a pad and a pointer press the COPY control beside
/// the code, so their lines name no key.</summary>
public enum CopyWay
{
    /// <summary>The keyboard, whose hint is <see cref="CoopDoorText.CopyPress"/>.</summary>
    Keys,

    /// <summary>A pad, which walks to the COPY control and presses it.</summary>
    Pad,

    /// <summary>A mouse or a touch screen, which clicks or taps the code or its COPY control.</summary>
    Pointer,
}

/// <summary>
/// The words the campaign's network door is shown in, on both of its ends. A host's campaign
/// boards carry a band naming its join code, or without one the port and the address guests reach
/// it at. A guest's join board names the session a host advertised, and its waiting board says
/// what it is waiting for. Engine-free and built off the door alone, so a presentation draws the same words
/// a unit test reads. The mission's long name is the caller's, since only it holds the langui
/// table.
/// </summary>
public static class CoopDoorText
{
    /// <summary>The waiting board's heading.</summary>
    public const string WaitingHeading = "CAMPAIGN CO-OP";

    /// <summary>The waiting board's one row, which closes the link.</summary>
    public const string LeaveRow = "Leave the session";

    /// <summary>The Network board's last row once a campaign host has answered, in place of the
    /// Dogfight's way on to the map.</summary>
    public const string WaitRow = "Continue → Wait for the host";

    /// <summary>The press that opens and closes the door on a campaign board, as its footers name
    /// it.</summary>
    public const string TogglePress = "L / Y  Network";

    /// <summary>A guest's word when its host closed the session and said so.</summary>
    public const string HostClosed = "Host closed the game";

    /// <summary>A guest's word when its link to the host dropped without a close notice.</summary>
    public const string HostLeft = "Host left the game";

    /// <summary>A guest's word when the host had no seat left for it.</summary>
    public const string GameFull = "The game is full";

    /// <summary>A guest's word when the host booted it, or refused its return after a boot. The
    /// original's own notice (langui 10500) names the player to the others instead.</summary>
    public const string Booted = "You were booted from the game";

    /// <summary>A guest's word when the host refused its password: the original's messagebox text
    /// for a wrong password, MSG_DPERR_INVALIDPASSWORD (7041).</summary>
    public const string WrongPassword = "Invalid Password";

    /// <summary>A guest's word while a host that asks a password has not admitted it yet.</summary>
    public const string AwaitingAdmission = "Waiting for the host to accept the password ...";

    /// <summary>The games list's Status for a game that asks a password, the original's langui
    /// 10141.</summary>
    public const string NeedPassword = "Need Password";

    /// <summary>The co-op host's cabin plaque that boots a guest.</summary>
    public const string BootButton = "BOOT";

    /// <summary>The question a co-op guest's Back asks on the host's boards.</summary>
    public const string LeaveQuestion = "Leave the co-op session?";

    /// <summary>The cabin's door while it is shut.</summary>
    public const string HostCoopButton = "HOST CO-OP";

    /// <summary>The cabin's door while it is open.</summary>
    public const string CloseNetworkButton = "CLOSE NETWORK";

    /// <summary>The press that copies a host's address, as its boards name it.</summary>
    public const string CopyPress = "Ctrl+C";

    /// <summary>The Original presentation's control beside a host's code or address that copies it.
    /// </summary>
    public const string CopyButton = "COPY";

    /// <summary>A host's word when this machine holds no stable global IPv6 address.</summary>
    public const string NoIpv6 = "No global IPv6 address";

    /// <summary>The name a Dogfight host's pinned lines (<see cref="HostLobbyLines"/>) stand under
    /// in its lobby chat.</summary>
    public const string NoteName = "Network";

    /// <summary>Why a host with a master server set has no join code when its build cannot open
    /// the WebRTC carrier the code is reached over.</summary>
    public const string NoWebRtc = "WebRTC is missing or failed to start";

    /// <summary>Why a guest cannot join by code when no master server is set to look the code up.
    /// </summary>
    public const string NoMasterServer = "No master server is set";

    /// <summary>A host's word while the master server has not answered with a code yet.</summary>
    public const string AwaitingCode = "Asking the master server for a join code ...";

    /// <summary>Game Information's Public choice, which lists the game.</summary>
    public const string PublicWord = "Public";

    /// <summary>Game Information's Private choice, which keeps the game off the games list.</summary>
    public const string PrivateWord = "Private";

    // The longest line a host is told why it has no code in, so a long fault fits the band.
    private const int InternetLineLimit = 76;

    /// <summary>The games list's Game Name: the name the host's Game Information box gave it, or
    /// what it holds open when the advert names none.</summary>
    public static string GameName(SessionAdvertMessage advert)
    {
        if (advert.Host.Length > 0)
        {
            return advert.Host;
        }

        return advert.Kind switch
        {
            NetSessionKind.CampaignCoop => "Campaign",
            NetSessionKind.Dogfight => "Dogfight",
            _ => "Game",
        };
    }

    /// <summary>The games list's # of Players, as "2/4". An advert naming no cap takes its kind's.
    /// </summary>
    public static string PlayerCount(SessionAdvertMessage advert)
    {
        int cap = advert.Cap > 0 ? advert.Cap : NetPlayerInfo.PlayerCap(advert.Kind);
        return $"{advert.Players.ToString(CultureInfo.InvariantCulture)}/{cap.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>The games list's Mission Type.</summary>
    public static string MissionType(SessionAdvertMessage advert) => advert.Kind switch
    {
        NetSessionKind.CampaignCoop => "Campaign co-op",
        NetSessionKind.Dogfight => "Dogfight",
        _ => "Unknown",
    };

    /// <summary>The games list's Mission Environment: a campaign mission's long name through
    /// <paramref name="missionName"/>, or its shortcode, such as "C2/M03", when
    /// <paramref name="fits"/> says the name overflows the column. A Dogfight names its lobby's
    /// environment, and one from a host with no lobby names none.
    /// </summary>
    public static string Environment(SessionAdvertMessage advert, Func<int, string> missionName, Func<string, bool> fits)
    {
        ArgumentNullException.ThrowIfNull(missionName);
        ArgumentNullException.ThrowIfNull(fits);
        if (advert.Kind == NetSessionKind.Dogfight)
        {
            return DogfightLobby.EnvironmentName(advert.MissionSeq);
        }

        if (advert.Kind != NetSessionKind.CampaignCoop || !advert.HasMission)
        {
            return "";
        }

        string name = missionName(advert.MissionSeq);
        return fits(name) ? name : Shortcode(advert);
    }

    /// <summary>A campaign mission's shortcode, chapter and mission, as "C2/M03".</summary>
    public static string Shortcode(SessionAdvertMessage advert) =>
        $"C{advert.Chapter.ToString(CultureInfo.InvariantCulture)}/M{advert.MissionInChapter.ToString("00", CultureInfo.InvariantCulture)}";

    /// <summary>The games list's Status. A game that asks a password reads
    /// <see cref="NeedPassword"/> unless it is full, as the original's list marks it
    /// (FUN_00402f40).</summary>
    public static string Status(SessionAdvertMessage advert) => advert.Status switch
    {
        NetSessionStatus.Full => "Full",
        _ when advert.Password => NeedPassword,
        NetSessionStatus.Waiting => "Waiting",
        NetSessionStatus.InMission => "In mission",
        _ => "",
    };

    /// <summary>The games list's Status for a game heard on the LAN. A game of a version this build
    /// does not play with reads as that version, such as "Version 0.7", in place of its status.
    /// </summary>
    public static string Status(LanGame game, NetBuildVersion own) =>
        own.PlaysWith(game.Version) ? Status(game.Advert) : $"Version {game.Version}";

    /// <summary>Why a guest and a host of versions that do not play together were kept apart,
    /// naming both, as "Host runs 0.7, you run 0.6".</summary>
    public static string VersionMismatch(NetBuildVersion host, NetBuildVersion own) =>
        $"Host runs {host}, you run {own}";

    /// <summary>Whether a games list row may be picked: a session this build knows, with a seat.
    /// </summary>
    public static bool Joinable(SessionAdvertMessage advert) =>
        advert.Kind != NetSessionKind.Unknown && advert.Status is NetSessionStatus.Waiting or NetSessionStatus.InMission;

    /// <summary>What an advert names: the kind of session and, for a campaign, the chapter, the
    /// mission within it and the mission's long name through <paramref name="missionName"/>.
    /// </summary>
    public static string SessionName(SessionAdvertMessage advert, Func<int, string> missionName)
    {
        ArgumentNullException.ThrowIfNull(missionName);
        return advert.Kind switch
        {
            NetSessionKind.CampaignCoop when advert.HasMission =>
                $"Campaign co-op, chapter {advert.Chapter.ToString(CultureInfo.InvariantCulture)}, "
                + $"mission {advert.MissionInChapter.ToString(CultureInfo.InvariantCulture)}: {missionName(advert.MissionSeq)}",
            NetSessionKind.CampaignCoop => "Campaign co-op",
            NetSessionKind.Dogfight => "Dogfight",
            _ => "A session this build does not know",
        };
    }

    /// <summary>A player count as a phrase, "1 player" or "3 players".</summary>
    public static string Players(int count) =>
        count == 1 ? "1 player" : $"{count.ToString(CultureInfo.InvariantCulture)} players";

    /// <summary>The Network board's status once a join has landed. It names the session once the
    /// advert arrives.
    /// <paramref name="link"/> is the board's own link readout, appended after the address.
    /// </summary>
    public static string JoinedStatus(NetPlayFeature net, string link, Func<int, string> missionName)
    {
        ArgumentNullException.ThrowIfNull(net);
        string linked = $"Linked to {net.LinkedTo}{link}";
        if (net.Advert is not { } advert)
        {
            return net.HostStarted
                ? $"{linked}. The host has started: pick the host's map and fly."
                : $"{linked}. Waiting for the host to start.";
        }

        string session = $"{SessionName(advert, missionName)}, {GameCalled(advert)}{Players(advert.Players)}";
        return advert.Kind == NetSessionKind.CampaignCoop
            ? $"{linked}. {session}. Continue, and wait there for the host's launch."
            : net.HostStarted
                ? $"{linked}. {session}. The host has started: pick the host's map and fly."
                : $"{linked}. {session}. Waiting for the host to start.";
    }

    /// <summary>The waiting board's status line: the mission, the host, the field, and whether
    /// the host has launched yet.</summary>
    public static string WaitingStatus(NetPlayFeature net, Func<int, string> missionName)
    {
        ArgumentNullException.ThrowIfNull(net);
        if (net.Advert is not { } advert)
        {
            return $"Linked to {net.LinkedTo}. Waiting for the host to name its session.";
        }

        string state = net.HostStarted
            ? "The host has launched the mission."
            : "Waiting for the host to launch the mission.";
        return $"{SessionName(advert, missionName)}. {Capital(GameCalled(advert))}{Players(advert.Players)} at {net.LinkedTo}. {state}";
    }

    /// <summary>A campaign host's band. With a join code it names the guests, the code and the copy
    /// key <paramref name="way"/> offers, and on a second line whether the game is listed. Without
    /// one it names the port, the router's address and the guests, then
    /// <see cref="HostFallbackLines"/>. Empty while the door is not a campaign host.</summary>
    public static string HostBand(NetPlayFeature net, CopyWay way = CopyWay.Keys)
    {
        ArgumentNullException.ThrowIfNull(net);
        if (!net.IsCoopHost)
        {
            return "";
        }

        int guests = net.Peers;
        string joined = guests == 1 ? "1 guest" : $"{guests.ToString(CultureInfo.InvariantCulture)} guests";

        // A code reaches this host from anywhere, so the address a guest would type is not shown.
        if (net.JoinCode is { } code)
        {
            return $"NETWORK OPEN  {joined}  CODE {code}{CopyMark(net, code, way)}\n{Listing(net.Private)}";
        }

        string port = net.Port.ToString(CultureInfo.InvariantCulture);
        string where = net.Router.PortMap switch
        {
            { IsMapped: true } map => $"{map.ExternalAddress}:{map.Port.ToString(CultureInfo.InvariantCulture)}",
            { Outcome: UpnpPortMapOutcome.NoPublicAddress } => $"port {port}, LAN only: no public IPv4",
            not null => $"port {port}, this network only",
            null => $"port {port}",
        };
        var lines = new List<string> { $"NETWORK OPEN  {where}  {joined}" };
        lines.AddRange(HostFallbackLines(net, way));
        return string.Join("\n", lines);
    }

    /// <summary>What a host without a join code shows in its place. While the master server is still
    /// answering it is <see cref="AwaitingCode"/> alone. After that it is the address guests type
    /// (<see cref="HostAddressLine"/>), then why there is no code (<see cref="InternetLine"/>). Each
    /// is left out when empty. Empty with a code and while not hosting.</summary>
    public static IReadOnlyList<string> HostFallbackLines(NetPlayFeature net, CopyWay way = CopyWay.Keys)
    {
        ArgumentNullException.ThrowIfNull(net);
        if (!net.IsHost || net.JoinCode != null)
        {
            return Array.Empty<string>();
        }

        // ⚠ Do not name the address while the master server is answering. A code may still come,
        // and a host shows the address only when none will.
        if (net.AwaitingCode)
        {
            return new[] { AwaitingCode };
        }

        var lines = new List<string> { HostAddressLine(net, way), InternetLine(net) };
        lines.RemoveAll(line => line.Length == 0);
        return lines;
    }

    /// <summary>A Dogfight host's lines pinned over its lobby chat under <see cref="NoteName"/>. With
    /// a join code they are <see cref="HostCodeLine"/> alone, and without one
    /// <see cref="HostFallbackLines"/>, which wait for the master server's outcome. Empty while not
    /// hosting.</summary>
    public static IReadOnlyList<string> HostLobbyLines(NetPlayFeature net, CopyWay way = CopyWay.Keys)
    {
        ArgumentNullException.ThrowIfNull(net);
        return net.JoinCode != null ? new[] { HostCodeLine(net, way) } : HostFallbackLines(net, way);
    }

    /// <summary>What a host's COPY control copies, the text <see cref="NetPlayFeature.CopyForGuests"/>
    /// puts on the clipboard: the join code, else the address <see cref="HostAddressLine"/> names.
    /// Empty where no line of the host's carries a copy mark, and while not hosting.</summary>
    public static string CopyTarget(NetPlayFeature net)
    {
        ArgumentNullException.ThrowIfNull(net);
        if (!net.IsHost)
        {
            return "";
        }

        return net.JoinCode ?? (net.NamesHostAddress ? net.GuestAddress : "");
    }

    /// <summary>A Dogfight host's standing line about internet guests: its code, whether it is
    /// listed and the copy key <paramref name="way"/> offers, or why there is no code. Empty while
    /// the door is not hosting or no master server is set.</summary>
    public static string HostCodeLine(NetPlayFeature net, CopyWay way = CopyWay.Keys)
    {
        ArgumentNullException.ThrowIfNull(net);
        if (!net.IsHost)
        {
            return "";
        }

        if (net.JoinCode is not { } code)
        {
            return InternetLine(net);
        }

        string copy = net.Copied == code ? " It is copied." : way == CopyWay.Keys ? $" {CopyPress} copies it." : "";
        string listed = net.Private ? "private, not on the games list" : "public, on the games list";
        return $"Internet code {code}, {listed}.{copy}";
    }

    /// <summary>Why a host has no join code yet: still asking, or the fault, cut to one band line.
    /// Empty with a code, while not hosting, and when no master server is set.</summary>
    public static string InternetLine(NetPlayFeature net)
    {
        ArgumentNullException.ThrowIfNull(net);
        if (net.AwaitingCode)
        {
            return AwaitingCode;
        }

        if (net.InternetFault is not { Length: > 0 } why)
        {
            return "";
        }

        string line = $"No internet code: {why}";
        return line.Length <= InternetLineLimit ? line : $"{line[..(InternetLineLimit - 3)].TrimEnd()}...";
    }

    /// <summary>What a guest's way in by code says in place of its description while
    /// <paramref name="why"/> keeps it shut. One reads "No master server is set, so joining by code
    /// is unavailable."</summary>
    public static string CodeJoinUnavailable(string why) => $"{why}, so joining by code is unavailable.";

    /// <summary>Game Information's Public/Private row as it reads.</summary>
    public static string ListingWord(bool isPrivate) => isPrivate ? PrivateWord : PublicWord;

    /// <summary>A host band's second line: the IPv6 address a guest outside this network types,
    /// and the copy key <paramref name="way"/> offers. Without one it says so and names the LAN
    /// address. Empty while the door is not hosting or names no address.</summary>
    public static string HostAddressLine(NetPlayFeature net, CopyWay way = CopyWay.Keys)
    {
        ArgumentNullException.ThrowIfNull(net);
        if (!net.IsHost || !net.NamesHostAddress)
        {
            return "";
        }

        string copy = net.GuestAddress.Length == 0 ? "" : CopyMark(net, net.GuestAddress, way);
        if (net.HostIpv6 is { } v6)
        {
            return $"IPv6  {net.Dial(v6)}{copy}";
        }

        string lan = net.HostLanIpv4 is { } v4 ? $"  LAN {net.Dial(v4)}" : "";
        return $"{NoIpv6}{lan}{copy}";
    }

    /// <summary>The Network board's sentences about a host's address: what a guest types, on this
    /// network and outside it, and how to copy it. Empty while the door is not hosting or names no
    /// address.</summary>
    public static string HostAddressStatus(NetPlayFeature net)
    {
        ArgumentNullException.ThrowIfNull(net);
        if (!net.IsHost || !net.NamesHostAddress)
        {
            return "";
        }

        string lan = net.HostLanIpv4 is { } v4 ? net.Dial(v4) : "";
        string copy = net.GuestAddress.Length == 0 ? ""
            : net.Copied == net.GuestAddress ? $" {net.GuestAddress} is copied."
            : $" {CopyPress} copies {net.GuestAddress}.";
        if (net.HostIpv6 is { } v6)
        {
            string local = lan.Length > 0 ? $", or {lan} on this network" : "";
            return $"Guests type {net.Dial(v6)}{local}.{copy}";
        }

        string onLan = lan.Length > 0 ? $"; guests on this network type {lan}" : "";
        return $"This machine has no global IPv6 address{onLan}.{copy}";
    }

    /// <summary>What the router said about a host's port, as a sentence for the door's status
    /// line. Every outcome but a mapping says guests on this network still join.</summary>
    public static string RouterStatus(UpnpPortMapResult map)
    {
        string port = map.Port.ToString(CultureInfo.InvariantCulture);
        string local = "guests on this network still join";
        string kind = IgdAddress.Word(IgdAddress.Kind(map.ExternalAddress));
        return map.Outcome switch
        {
            UpnpPortMapOutcome.Mapped => $"Router mapped port {port}, reachable at {map.ExternalAddress}.",
            UpnpPortMapOutcome.NoPublicAddress =>
                $"The router answered, but this internet line has no public IPv4 address (the router's own, "
                + $"{map.ExternalAddress}, is {kind}). {Capital(local)}; guests outside need IPv6 with port {port} "
                + "opened on the router, or another player hosts.",
            UpnpPortMapOutcome.NoGateway => $"No UPnP router answered, so port {port} is not mapped; {local}.",
            UpnpPortMapOutcome.TimedOut => $"The router did not answer in time, so port {port} is not mapped; {local}.",
            _ => $"The router declined to map port {port}; {local}.",
        };
    }

    /// <summary>What the router said about a host's IPv6 pinhole, as one clause for the door's
    /// status line after <see cref="RouterStatus"/>.</summary>
    public static string PinholeStatus(UpnpPinholeResult pinhole)
    {
        string port = pinhole.Port.ToString(CultureInfo.InvariantCulture);
        return pinhole.Outcome switch
        {
            UpnpPinholeOutcome.Opened => $"IPv6: router opened UDP port {port} for {pinhole.Address}.",
            UpnpPinholeOutcome.FirewallOff => $"IPv6: the router's firewall is off, so UDP port {port} is open.",
            UpnpPinholeOutcome.Disallowed =>
                $"IPv6: the router does not let programs open ports; allow it there, or open UDP port {port} by hand.",
            UpnpPinholeOutcome.NoService => $"IPv6: the router cannot open ports on request; open UDP port {port} by hand.",
            UpnpPinholeOutcome.NoAddress => "IPv6: no stable address to open a port for.",
            _ => $"IPv6: opening UDP port {port} on the router failed ({pinhole.Detail}).",
        };
    }

    /// <summary>The door's pinhole clause, or empty when no pinhole was asked for. Where
    /// <see cref="HostAddressStatus"/> already names the address, the clause leaves it out, so the
    /// status line names it once.</summary>
    public static string HostPinholeStatus(NetPlayFeature net)
    {
        ArgumentNullException.ThrowIfNull(net);
        if (net.Router.Pinhole is not { } pinhole)
        {
            return "";
        }

        bool named = net.IsHost && net.NamesHostAddress;
        if (named && pinhole.Outcome == UpnpPinholeOutcome.Opened && pinhole.Address == net.HostIpv6)
        {
            return $"IPv6: router opened UDP port {pinhole.Port.ToString(CultureInfo.InvariantCulture)}.";
        }

        return named && net.HostIpv6 == null && pinhole.Outcome == UpnpPinholeOutcome.NoAddress ? "" : PinholeStatus(pinhole);
    }

    /// <summary>A co-op guest's band over the host's boards. It says whose campaign it follows and
    /// what the host is doing, or on the flight check what the guest still owes. Empty while the
    /// door is not a co-op guest's or the host has named no board yet.</summary>
    public static string GuestBand(NetPlayFeature net)
    {
        ArgumentNullException.ThrowIfNull(net);
        if (!net.IsCoopGuest || net.CoopFlow is not { } flow)
        {
            return "";
        }

        // The advert names the game rather than its host, so the band says "the host".
        string doing = flow.Screen switch
        {
            NetCoopScreen.Briefing => "The host is on the briefing",
            NetCoopScreen.FlightCheck => net.CoopSeatsReady
                ? "Ready, waiting for the host to launch"
                : "Pick your plane and ammo, then press Ready",
            NetCoopScreen.InMission => "The host is in a mission; you fly from the next briefing",
            NetCoopScreen.Debrief => flow.Won ? "Mission won" : "Mission failed",
            _ => "The host is in the cabin",
        };
        string shortLine = net.CoopSeatsShort;
        return shortLine.Length > 0 ? $"CO-OP  {doing}\n{shortLine}" : $"CO-OP  {doing}";
    }

    /// <summary>What a co-op guest is told when the host's cap gave its machine
    /// <paramref name="given"/> seats for its <paramref name="players"/> players.</summary>
    public static string SeatsShort(int given, int players)
    {
        string flies = given == 1 ? "P1 flies" : $"P1 to P{given.ToString(CultureInfo.InvariantCulture)} fly";
        string waits = players - given == 1
            ? $"P{players.ToString(CultureInfo.InvariantCulture)} sits out"
            : $"P{(given + 1).ToString(CultureInfo.InvariantCulture)} to P{players.ToString(CultureInfo.InvariantCulture)} sit out";
        return $"The game is full: {flies}, {waits}";
    }

    /// <summary>What every player is told when a guest's link drops mid-mission.</summary>
    public static string Left(string name) => $"{(name.Length > 0 ? name : "A guest")} left";

    /// <summary>The lobby chat's notice that a player was booted, the original's langui 10500.
    /// </summary>
    public static string BootedLine(string name) => $"[{(name.Length > 0 ? name : "A guest")} was booted from the game.]";

    /// <summary>The co-op host's question before its cabin's BOOT removes a guest.</summary>
    public static string BootQuestion(string name) => $"Boot {(name.Length > 0 ? name : "this guest")} from the game?";

    // A pad's and a pointer's way is the COPY control the screen draws beside the line, so they
    // name no key.
    private static string CopyMark(NetPlayFeature net, string shown, CopyWay way) =>
        net.Copied == shown ? "  copied" : way == CopyWay.Keys ? $"  {CopyPress}" : "";

    private static string Listing(bool isPrivate) =>
        isPrivate ? "PRIVATE  internet guests need the code" : "PUBLIC  on the games list";

    private static string GameCalled(SessionAdvertMessage advert) =>
        advert.Host.Length > 0 ? $"game {advert.Host}, " : "";

    private static string Capital(string text) =>
        text.Length > 0 ? char.ToUpperInvariant(text[0]) + text[1..] : text;
}
