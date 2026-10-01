using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CSVM.Net;

/// <summary>
/// The master server's wire, shared by the server and the game. It holds the paths, the type
/// words, the limits both ends keep, the join code's form and the JSON. This file names no
/// engine type and no other engine file, because the server project compiles it on its own.
/// ⚠ A word or field renamed here breaks every build already in players' hands; add, never rename.
/// </summary>
public static class MasterWire
{
    /// <summary>The games list, an HTTP GET.</summary>
    public const string GamesPath = "/api/games";

    /// <summary>The health check, an HTTP GET answering the listed count.</summary>
    public const string HealthPath = "/api/health";

    /// <summary>The socket a host registers on and a join negotiates over.</summary>
    public const string SocketPath = "/ws";

    /// <summary>The longest message either end accepts, in UTF-8 bytes. An SDP offer with a few
    /// candidates is under 4 KiB, so this leaves room without letting a sender grow a buffer.</summary>
    public const int MaxMessageBytes = 16 * 1024;

    /// <summary>The longest game name a listing carries, in characters.</summary>
    public const int NameLimit = 32;

    /// <summary>The longest version text a listing carries.</summary>
    public const int VersionLimit = 16;

    /// <summary>The largest player count or cap a listing may name.</summary>
    public const int PlayerLimit = 16;

    /// <summary>How often a host repeats its listing, in seconds, whether or not it changed.</summary>
    public const double HeartbeatSeconds = 15.0;

    /// <summary>How long a listing stands without a word from its host before it is dropped. Three
    /// heartbeats, so one late message does not drop a game.</summary>
    public const double ExpirySeconds = 45.0;

    /// <summary>A host registers its game.</summary>
    public const string Host = "host";

    /// <summary>The server's answer to <see cref="Host"/>, with the code.</summary>
    public const string Hosted = "hosted";

    /// <summary>A host's repeated or changed listing, its heartbeat.</summary>
    public const string Update = "update";

    /// <summary>A guest asks to join the game a code names.</summary>
    public const string Join = "join";

    /// <summary>The server's answer to <see cref="Join"/>, with the guest's peer id.</summary>
    public const string Joined = "joined";

    /// <summary>The server tells a host a guest is negotiating, with its peer id and address.</summary>
    public const string Incoming = "incoming";

    /// <summary>An SDP description or ICE candidate for one other end, relayed by the server.</summary>
    public const string Signal = "signal";

    /// <summary>The server tells a host a negotiating guest's socket closed.</summary>
    public const string Left = "left";

    /// <summary>The server tells a guest its host's game is gone.</summary>
    public const string Closed = "closed";

    /// <summary>The server refuses something; <see cref="MasterMessage.Why"/> says what.</summary>
    public const string Error = "error";

    /// <summary>A signal carrying the offering end's SDP.</summary>
    public const string Offer = "offer";

    /// <summary>A signal carrying the answering end's SDP.</summary>
    public const string Answer = "answer";

    /// <summary>A signal carrying one ICE candidate.</summary>
    public const string Candidate = "candidate";

    /// <summary>The kind word of a Dogfight.</summary>
    public const string DogfightKind = "dogfight";

    /// <summary>The kind word of a campaign mission flown together.</summary>
    public const string CoopKind = "coop";

    /// <summary>The status word of a host on its boards.</summary>
    public const string Waiting = "waiting";

    /// <summary>The status word of a host flying its mission.</summary>
    public const string InMission = "inmission";

    /// <summary>The status word of a game with no seat left.</summary>
    public const string Full = "full";

    /// <summary>The characters a join code is written with: digits and capitals. The four a reader
    /// confuses (0, 1, I, O) are left out. 32 of them, so six carry 30 bits.</summary>
    public const string CodeAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    /// <summary>How many characters a code has, written as two halves around a dash.</summary>
    public const int CodeLength = 6;

    /// <summary>Every kind word a listing may carry.</summary>
    public static readonly IReadOnlyList<string> Kinds = new[] { DogfightKind, CoopKind };

    /// <summary>Every status word a listing may carry.</summary>
    public static readonly IReadOnlyList<string> Statuses = new[] { Waiting, InMission, Full };

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = false,
    };

    /// <summary>The JSON text of <paramref name="message"/>.</summary>
    public static string Write(MasterMessage message) => JsonSerializer.Serialize(message, Options);

    /// <summary>The JSON text of a games list.</summary>
    public static string Write(MasterGameList list) => JsonSerializer.Serialize(list, Options);

    /// <summary>Reads one message. False for text over <see cref="MaxMessageBytes"/>, text that is
    /// not a JSON object, and an object with no type word.</summary>
    public static bool TryRead(string? text, out MasterMessage message)
    {
        message = new MasterMessage();
        if (string.IsNullOrEmpty(text) || Encoding.UTF8.GetByteCount(text) > MaxMessageBytes)
        {
            return false;
        }

        try
        {
            if (JsonSerializer.Deserialize<MasterMessage>(text, Options) is not { T.Length: > 0 } read)
            {
                return false;
            }

            message = read;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Reads a games list, every game passed through <see cref="Clean"/> and one without a
    /// well-formed code left out. Null for text that is not a list.</summary>
    public static IReadOnlyList<MasterGame>? TryReadList(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        try
        {
            if (JsonSerializer.Deserialize<MasterGameList>(text, Options) is not { } list)
            {
                return null;
            }

            var games = new List<MasterGame>(list.Games?.Count ?? 0);
            foreach (var game in list.Games ?? new List<MasterGame>())
            {
                if (game != null && TryCode(game.Code, out string code))
                {
                    var clean = Clean(game);
                    clean.Code = code;
                    games.Add(clean);
                }
            }

            return games;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A copy of <paramref name="game"/> held to the wire's limits. The name and version are
    /// cut to length and the counts held to 0 to <see cref="PlayerLimit"/>. An unknown kind or
    /// status is emptied. The code is not copied; the server writes its own.</summary>
    public static MasterGame Clean(MasterGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return new MasterGame
        {
            Name = Cut(game.Name, NameLimit),
            Kind = Known(game.Kind, Kinds),
            Players = Math.Clamp(game.Players, 0, PlayerLimit),
            Cap = Math.Clamp(game.Cap, 0, PlayerLimit),
            Password = game.Password,
            Status = Known(game.Status, Statuses),
            Mission = game.Mission is >= 0 and < 256 ? game.Mission : -1,
            Version = Cut(game.Version, VersionLimit),
        };
    }

    /// <summary>Reads a join code as a player types it into its written form <c>ABC-DEF</c>. Either
    /// case reads, with or without the dash, around whitespace. False for anything else.</summary>
    public static bool TryCode(string? typed, out string code)
    {
        code = "";
        var letters = new StringBuilder(CodeLength);
        bool dashed = false;
        foreach (char c in (typed ?? "").Trim())
        {
            if (c == '-' && letters.Length == CodeLength / 2 && !dashed)
            {
                dashed = true;
                continue;
            }

            char upper = char.ToUpperInvariant(c);
            if (CodeAlphabet.IndexOf(upper) < 0 || letters.Length == CodeLength)
            {
                return false;
            }

            letters.Append(upper);
        }

        if (letters.Length != CodeLength)
        {
            return false;
        }

        code = Written(letters.ToString());
        return true;
    }

    /// <summary>A code of <see cref="CodeLength"/> alphabet characters written with its dash.</summary>
    public static string Written(string letters)
    {
        ArgumentNullException.ThrowIfNull(letters);
        return letters.Length == CodeLength ? $"{letters[..(CodeLength / 2)]}-{letters[(CodeLength / 2)..]}" : letters;
    }

    private static string Cut(string? text, int limit)
    {
        string value = (text ?? "").Trim();
        return value.Length <= limit ? value : value[..limit];
    }

    private static string Known(string? word, IReadOnlyList<string> words)
    {
        foreach (string known in words)
        {
            if (string.Equals(word, known, StringComparison.Ordinal))
            {
                return known;
            }
        }

        return "";
    }
}

/// <summary>One game as the master server lists it: what the games list shows, and the code a
/// guest joins it by. A host sends it without a code; the server fills the code in. Plain words and
/// numbers rather than this build's enums, since the server compiles this file without the rest of
/// the engine.</summary>
public sealed class MasterGame
{
    /// <summary>The join code the server gave the game, in <see cref="MasterWire.TryCode"/>'s form.
    /// Empty on a host's own send.</summary>
    public string Code { get; set; } = "";

    /// <summary>The game's name as the host's Game Information box gave it.</summary>
    public string Name { get; set; } = "";

    /// <summary>One of <see cref="MasterWire.Kinds"/>.</summary>
    public string Kind { get; set; } = "";

    /// <summary>The players seated, host included.</summary>
    public int Players { get; set; }

    /// <summary>The most players the game seats.</summary>
    public int Cap { get; set; }

    /// <summary>Whether the host asks a password before it admits a guest.</summary>
    public bool Password { get; set; }

    /// <summary>One of <see cref="MasterWire.Statuses"/>.</summary>
    public string Status { get; set; } = "";

    /// <summary>The campaign mission's sequence number, or -1 for none.</summary>
    public int Mission { get; set; } = -1;

    /// <summary>The host build's MAJOR.MINOR, as <c>NetBuildVersion</c> writes it.</summary>
    public string Version { get; set; } = "";
}

/// <summary>One STUN or TURN server as a WebRTC peer connection takes it. A TURN entry carries the
/// time-limited user name and credential the server minted for this one exchange.</summary>
public sealed class MasterIceServer
{
    /// <summary>The server's URLs, such as <c>stun:turn.example.org:3478</c>.</summary>
    public List<string> Urls { get; set; } = new();

    /// <summary>The TURN user name, null for a STUN entry.</summary>
    public string? Username { get; set; }

    /// <summary>The TURN credential, null for a STUN entry.</summary>
    public string? Credential { get; set; }
}

/// <summary>The answer to a games list request.</summary>
public sealed class MasterGameList
{
    /// <summary>Every game listed when the answer was written.</summary>
    public List<MasterGame> Games { get; set; } = new();
}

/// <summary>One message over the master server's socket, either way. <see cref="T"/> names it by
/// one of <see cref="MasterWire"/>'s type words, which says which other fields it carries. The rest
/// stay null and are left out of the text. The server writes a relayed signal's
/// <see cref="From"/>.</summary>
public sealed class MasterMessage
{
    /// <summary>The type word.</summary>
    public string T { get; set; } = "";

    /// <summary>A host's listing on <c>host</c> and <c>update</c>.</summary>
    public MasterGame? Game { get; set; }

    /// <summary>The join code: the one a host was given, or the one a guest asks for.</summary>
    public string? Code { get; set; }

    /// <summary>A guest's peer id: its own on <c>joined</c>, the arriving or leaving one on
    /// <c>incoming</c> and <c>left</c>.</summary>
    public int? Peer { get; set; }

    /// <summary>Where a signal goes: 1 for the host, a guest's peer id otherwise.</summary>
    public int? To { get; set; }

    /// <summary>Where a relayed signal came from, written by the server.</summary>
    public int? From { get; set; }

    /// <summary>A signal's kind: one of <see cref="MasterWire.Offer"/>,
    /// <see cref="MasterWire.Answer"/> and <see cref="MasterWire.Candidate"/>.</summary>
    public string? Kind { get; set; }

    /// <summary>A description's SDP text, or a candidate's own line.</summary>
    public string? Sdp { get; set; }

    /// <summary>A candidate's media id.</summary>
    public string? Mid { get; set; }

    /// <summary>A candidate's media line index.</summary>
    public int? Index { get; set; }

    /// <summary>The address a guest's socket came from, which a host's ban list keys on.</summary>
    public string? Addr { get; set; }

    /// <summary>Why the server refused or closed something, as a player reads it.</summary>
    public string? Why { get; set; }

    /// <summary>The asking build's MAJOR.MINOR on <c>join</c>.</summary>
    public string? Version { get; set; }

    /// <summary>The STUN and TURN servers for the link about to be negotiated.</summary>
    public List<MasterIceServer>? Ice { get; set; }
}
