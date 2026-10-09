using System;
using CSVM.Net;

namespace CSVM.UI.Menu;

/// <summary>
/// Who this end is in a network game and what its session asks, as the Game and Player Information
/// boxes answered. That is the player's callsign and voice, and a host's game name, player cap,
/// password and Public or Private listing. The door owns one and names its hosted kind through a
/// delegate, since the listing's default is that kind's.
/// </summary>
public sealed class NetIdentity
{
    private readonly Func<NetSessionKind> _kind;
    private bool? _private;

    /// <summary>Answers for a door whose hosted kind <paramref name="kind"/> reads.</summary>
    public NetIdentity(Func<NetSessionKind> kind) => _kind = kind ?? throw new ArgumentNullException(nameof(kind));

    /// <summary>The callsign this end's player goes by. A guest's pick carries it to the host's
    /// roster, and a host's own first seat takes it. Empty when the player has none, and the roster
    /// then uses the player number.</summary>
    public string PlayerName { get; set; } = "";

    /// <summary>The pilot voice this end's player chose, as its place in
    /// <see cref="PilotVoices.All"/>, or -1 for none. It rides every pick this end sends.</summary>
    public int Voice { get; set; } = -1;

    /// <summary>The name this host's advert gives its game. Empty advertises the host's own name,
    /// as a door opened without the Game Information box does.</summary>
    public string GameName { get; set; } = "";

    /// <summary>The Maximum # of Players this host chose, 0 for its kind's cap. It is held to
    /// <see cref="NetPlayerInfo.ClampPlayers"/> whenever it is read.</summary>
    public int MaxPlayers { get; set; }

    /// <summary>The optional password: the one a host asks of every guest before admitting it, or
    /// the one a guest answers a host that asks. Empty asks nothing. A host's advert says only that
    /// it asks one, and the password itself leaves only in a guest's answer.</summary>
    public string Password { get; set; } = "";

    /// <summary>Whether this host's game stays off the master server's games list, so internet guests
    /// reach it by its join code alone. LAN searches and typed addresses reach it either way. Until a
    /// Game Information answer or a board sets it, it is the hosted kind's
    /// <see cref="NetPlayerInfo.DefaultPrivate"/>.</summary>
    public bool Private
    {
        get => _private ?? NetPlayerInfo.DefaultPrivate(_kind());
        set => _private = value;
    }

    /// <summary>Takes what the Game and Player Information boxes answered. The callsign, the voice
    /// and the password are always taken: a host's from Game Information, a joining player's from
    /// Player Information. The game's name, cap and Public or Private choice are taken when
    /// <paramref name="game"/> says the host's box was shown. The password and the choice last
    /// until the session they open ends.</summary>
    public void Take(NetPlayerInfo info, bool game)
    {
        ArgumentNullException.ThrowIfNull(info);
        PlayerName = info.Callsign.Trim();
        Voice = PilotVoices.Clamp(info.Voice);
        Password = info.Password;
        if (game)
        {
            GameName = info.GameName.Trim();
            MaxPlayers = info.MaxPlayers;
            _private = info.Private;
        }
    }

    /// <summary>Drops the password and the Public or Private choice, so the next open asks no
    /// password and takes its kind's default listing. A session's end calls it, and so does a door
    /// that opens with no box to confirm them. ⚠ Do not keep either past its session. Nothing on
    /// screen shows a leftover, which would gate a later host or join the player never gated.
    /// </summary>
    public void ForgetAnswers()
    {
        Password = "";
        _private = null;
    }
}
