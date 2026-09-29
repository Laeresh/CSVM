using System;
using System.Collections.Generic;
using CSVM.Net;
using CSVM.Utils;

namespace CSVM.UI.Menu;

/// <summary>One pilot voice the Player Information box's Voice list offers. It carries the string
/// naming it and the value the script gives its row (<c>WGA.LG[R].SF</c>).</summary>
public readonly record struct PilotVoice(int StringId, string Name, byte Speaker);

/// <summary>
/// The seven pilot voices of the original's Voice list, in its order
/// (<c>MULTIPLAYERPLAYERMODAL.SCRIPT</c>, strings 10039 to 10045). A voice travels as its place in
/// this list plus one, so both ends read it through the same table. A row's speaker value is a pilot
/// VO id, the number the voice clips and pools use. A player's aircraft speaks its combat lines as
/// that pilot (<c>docs/formats/combat-voice.md</c>).
/// </summary>
public static class PilotVoices
{
    /// <summary>The voice a player who never chose one has: the list's first row, which the
    /// script's list starts on.</summary>
    public const int Default = 0;

    /// <summary>The voice a campaign co-op host's first seat speaks in, whatever it chose: Nathan
    /// Zachary's, the list's first row. That seat is the scripted player, whose mission dialogue is
    /// Nathan Zachary's own. A remake-only rule, since the original flies no campaign across a link.</summary>
    public const int CoopHost = 0;

    private static readonly PilotVoice[] Table =
    {
        new(10039, "Nathan Zachary", 48),
        new(10040, "Jack", 2),
        new(10041, "Black Swan", 24),
        new(10042, "Paladin Blake", 29),
        new(10043, "Loyle Crawford", 44),
        new(10044, "Gruff Male", 26),
        new(10045, "Texan Male", 31),
    };

    /// <summary>Every voice, in the list's order.</summary>
    public static IReadOnlyList<PilotVoice> All => Table;

    /// <summary>A voice's place, or <see cref="Default"/> outside the list.</summary>
    public static int Clamp(int index) => index >= 0 && index < Table.Length ? index : Default;

    /// <summary>The pick's voice byte for the voice at <paramref name="index"/>, or
    /// <see cref="CoopPickMessage.NoVoice"/> outside the list.</summary>
    public static byte Wire(int index) =>
        index >= 0 && index < Table.Length ? (byte)(index + 1) : CoopPickMessage.NoVoice;

    /// <summary>The voice a pick's voice byte names, or -1 for none.</summary>
    public static int FromWire(byte voice) => voice >= 1 && voice <= Table.Length ? voice - 1 : -1;

    /// <summary>The pilot VO id a pick's or a seat's voice byte speaks as, or null for none.</summary>
    public static int? SpeakerFor(byte voice) => FromWire(voice) is var place and >= 0 ? Table[place].Speaker : null;
}

/// <summary>
/// What the original asks before a network game opens, engine-free so both presentations and a
/// unit test share it. Game Information names the game, its optional password and its seat cap,
/// and Player Information names the player's callsign and voice. The limits and the cap's range are
/// the scripts' own (<c>docs/org/multiplayer-messages.md</c>, "Game and Player Information"). The
/// callsign, the voice and the game name are remembered in the options for the next session.
/// </summary>
public sealed class NetPlayerInfo
{
    /// <summary>The longest callsign the Callsign box takes, the script's <c>UGA.FD</c>.</summary>
    public const int CallsignLimit = 12;

    /// <summary>The longest game name the Game Name box takes, the script's <c>SZ.FD</c>.</summary>
    public const int GameNameLimit = 14;

    /// <summary>The longest password the Password box takes. A remake-only rule: the script sets no
    /// limit on that box, and this is the Game Name box's.</summary>
    public const int PasswordLimit = GameNameLimit;

    /// <summary>The fewest players the Maximum spinner offers, the script's <c>VZ.VF</c>.</summary>
    public const int MinPlayers = 2;

    /// <summary>The spinner's value when the box opens, which both of the original's doors set
    /// (<c>VZ.YF = 8</c>).</summary>
    public const int DefaultPlayers = 8;

    /// <summary>The game's name as the games list shows it.</summary>
    public string GameName { get; set; } = "";

    /// <summary>The optional password: the one a host asks in Game Information, or the one a joining
    /// player answers in Player Information. It is never remembered.</summary>
    public string Password { get; set; } = "";

    /// <summary>The Maximum # of Players spinner's value, before any cap.</summary>
    public int MaxPlayers { get; set; } = DefaultPlayers;

    /// <summary>The name this player goes by in every list, line and roster.</summary>
    public string Callsign { get; set; } = "";

    /// <summary>The voice's place in <see cref="PilotVoices.All"/>.</summary>
    public int Voice { get; set; } = PilotVoices.Default;

    /// <summary>The most players a session of <paramref name="kind"/> seats: four humans for a
    /// campaign flown together, sixteen for a Dogfight.</summary>
    public static int PlayerCap(NetSessionKind kind) =>
        kind == NetSessionKind.CampaignCoop ? NetPlayFeature.CoopHumans : NetSeats.MaxPlayers;

    /// <summary><paramref name="players"/> held inside the spinner's range and the kind's cap.</summary>
    public static int ClampPlayers(NetSessionKind kind, int players) => Math.Clamp(players, MinPlayers, PlayerCap(kind));

    /// <summary>Whether a callsign or game name is one the original accepts: at least one character
    /// that is not a space or a control character. The test is FUN_00407060's.</summary>
    public static bool IsValidName(string? name)
    {
        foreach (char c in name ?? "")
        {
            if (!char.IsWhiteSpace(c) && !char.IsControl(c))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a box takes the typed character <paramref name="c"/> at all.</summary>
    public static bool Takes(char c) => !char.IsControl(c) && !char.IsSurrogate(c);

    /// <summary>The remembered answers, read from <paramref name="saved"/>. A player who never saved
    /// a callsign starts on <paramref name="pilotName"/>, the name they flew under last, cut to the
    /// box. The game name starts on the saved one, else on the callsign.</summary>
    public static NetPlayerInfo Remembered(OptionsDef saved, string? pilotName = null)
    {
        ArgumentNullException.ThrowIfNull(saved);
        string callsign = Cut(saved.NetCallsign ?? (pilotName ?? "").Trim(), CallsignLimit);
        return new NetPlayerInfo
        {
            Callsign = callsign,
            GameName = saved.NetGameName ?? Cut(callsign, GameNameLimit),
            Voice = PilotVoices.Clamp(saved.NetVoice ?? PilotVoices.Default),
        };
    }

    /// <summary>A copy of these answers, so a box can edit one and a Cancel leave the other.</summary>
    public NetPlayerInfo Copy() => (NetPlayerInfo)MemberwiseClone();

    /// <summary>Writes the callsign and the voice into <paramref name="def"/>, and the game name when
    /// <paramref name="game"/> says the Game Information box asked for it. A name the boxes would
    /// refuse is not written, so it never stands in for the pilot's own name next session.</summary>
    public void Remember(OptionsDef def, bool game)
    {
        ArgumentNullException.ThrowIfNull(def);
        if (IsValidName(Callsign))
        {
            def.NetCallsign = Cut(Callsign.Trim(), CallsignLimit);
        }

        def.NetVoice = PilotVoices.Clamp(Voice);
        if (game && IsValidName(GameName))
        {
            def.NetGameName = Cut(GameName.Trim(), GameNameLimit);
        }
    }

    private static string Cut(string text, int limit) => text.Length > limit ? text[..limit] : text;
}
