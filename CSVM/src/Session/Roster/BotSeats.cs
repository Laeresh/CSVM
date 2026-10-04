using System;
using System.Collections.Generic;
using CSVM.Flight.Hangar;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session.InstantAction;
using CSVM.Spec;

namespace CSVM.Session.Roster;

/// <summary>
/// What a bot seat is once its host seats it. A Random plane resolves to a stock airframe, and a
/// callsign is drawn from the shipped pilot names. Its pilot takes an Instant Action personality
/// shifted by its tier. Engine-free and host-only by its callers: a guest reads the roster the host
/// built and draws nothing. The names, the personalities and the offset
/// are the original's data; the rules joining them are remake-only. Decodes:
/// <c>docs/formats/missions.md</c> "Message table" and <c>docs/formats/instant-action.md</c>.
/// </summary>
public static class BotSeats
{
    /// <summary>The longest callsign a bot carries: the Callsign box's own 12 characters
    /// (<c>UGA.FD</c>, <c>docs/org/multiplayer-messages.md</c>), the limit a person's seat has.
    /// </summary>
    public const int CallsignLimit = 12;

    // The message table's character names (ids 13001-13036) that name a person. Rows naming an
    // aircraft or a role are left out: Getaway Plane, Bomber, Stunt Plane, the three regional aces.
    // So is MSG_PLAYER_NAME, the player's own character.
    private static readonly string[] PilotNameKeys =
    {
        "MSG_JACK_NAME", "MSG_BUCK_NAME", "MSG_BJOHN_NAME", "MSG_TEX_NAME", "MSG_BSWAN_NAME",
        "MSG_ADIXON_NAME", "MSG_PALBLAKE_NAME", "MSG_CSTEELE_NAME", "MSG_SSCRAWFORD_NAME",
        "MSG_HHUGHES_NAME", "MSG_BREDMANN_NAME", "MSG_GKHAN_NAME", "MSG_SIRWINTHROP_NAME",
        "MSG_UBLACKE_NAME", "MSG_RVARGAS", "MSG_BDPABLO_NAME", "MSG_BJ_HOWARD_NAME", "MSG_GKAYS_NAME",
        "MSG_DK_NAME", "MSG_CCHIM_NAME", "MSG_WITCH_NAME", "MSG_BETTY_NAME", "MSG_LMILES_NAME",
        "MSG_VVBECK_NAME", "MSG_CABBIE_NAME", "MSG_TINY_NAME", "MSG_ILSA_NAME", "MSG_JPEROT_NAME",
    };

    /// <summary>The message-table keys the callsign pool is read from, in id order.</summary>
    public static IReadOnlyList<string> PilotNames => PilotNameKeys;

    /// <summary>The callsigns a bot may draw: each pilot name the table resolves, cut by
    /// <see cref="CutName"/>, the first of any two the cut makes equal kept. Empty when the table
    /// is missing, and the caller then falls back to <see cref="SessionSpec.BotCallsign"/>.</summary>
    public static IReadOnlyList<string> CallsignPool(Messages messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var pool = new List<string>(PilotNameKeys.Length);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string key in PilotNameKeys)
        {
            string name = messages.Get(key);
            // An unresolved key comes back as itself, which is no name at all.
            if (name.StartsWith("MSG_", StringComparison.Ordinal) || CutName(name) is not { Length: > 0 } cut)
            {
                continue;
            }

            if (seen.Add(cut))
            {
                pool.Add(cut);
            }
        }

        return pool;
    }

    /// <summary>A pilot name brought within <see cref="CallsignLimit"/>: the whole words that fit,
    /// or the first <see cref="CallsignLimit"/> characters of a first word longer than that. A
    /// name that fits is returned trimmed.</summary>
    public static string CutName(string name)
    {
        string trimmed = (name ?? "").Trim();
        if (trimmed.Length <= CallsignLimit)
        {
            return trimmed;
        }

        int space = trimmed.LastIndexOf(' ', CallsignLimit);
        return space > 0 ? trimmed[..space].TrimEnd() : trimmed[..CallsignLimit];
    }

    /// <summary>A host-given callsign cut as the Callsign box cuts what is typed into it: at
    /// <see cref="CallsignLimit"/> characters, whatever the words.</summary>
    public static string ClipName(string name)
    {
        string trimmed = (name ?? "").Trim();
        return trimmed.Length <= CallsignLimit ? trimmed : trimmed[..CallsignLimit].TrimEnd();
    }

    /// <summary>The command line's bots as the host seats them. A null plane is Random, drawn with
    /// equal odds over <see cref="StockAirframes.Nodes"/>, so the roster carries a real airframe. A
    /// bot with no callsign draws one from <paramref name="pool"/>, shuffled once. It never takes a
    /// name drawn before, named by another bot or held in <paramref name="taken"/>. An exhausted
    /// pool falls back to <see cref="SessionSpec.BotCallsign"/>.</summary>
    public static IReadOnlyList<(string Plane, NetBotSkill Skill, int Team, string Callsign)> Resolve(
        IReadOnlyList<VsBotEntry> bots, IEnumerable<string> taken, IReadOnlyList<string> pool, Random rng)
    {
        ArgumentNullException.ThrowIfNull(bots);
        ArgumentNullException.ThrowIfNull(taken);
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(rng);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in taken)
        {
            used.Add((name ?? "").Trim());
        }

        foreach (var bot in bots)
        {
            if (ClipName(bot.Callsign) is { Length: > 0 } named)
            {
                used.Add(named);
            }
        }

        var order = new List<string>(pool);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        var nodes = StockAirframes.Nodes;
        var seated = new (string, NetBotSkill, int, string)[bots.Count];
        int next = 0;
        for (int i = 0; i < bots.Count; i++)
        {
            var bot = bots[i];
            string plane = bot.Plane ?? nodes[rng.Next(nodes.Count)];
            string callsign = ClipName(bot.Callsign);
            if (callsign.Length == 0)
            {
                while (next < order.Count && !used.Add(order[next]))
                {
                    next++;
                }

                callsign = next < order.Count ? order[next++] : Fallback(i + 1, used);
            }

            seated[i] = (plane, bot.Skill, bot.Team, callsign);
        }

        return seated;
    }

    /// <summary>The personality one draw selects, Instant Action's own per-aircraft roll. It is
    /// <c>draw % 5</c> over four authored rows and the flat row of fours, each at equal odds.
    /// Decode: docs/formats/instant-action.md "A wave enemy's nine pilot stats".</summary>
    public static AiSkillVector Personality(uint draw) => InstantActionRuntime.RandomPilotStats(draw);

    /// <summary>A bot's nine ratings: <paramref name="personality"/>'s, each shifted by its tier's
    /// <c>k</c> (-2, 0, +2) and clamped to 0-9. A bot seat's tier is <see cref="Difficulty"/>'s own
    /// number, and it scales no hull, so a bot is as hard to bring down as a person.</summary>
    public static AiSkillVector Ratings(AiSkillVector personality, NetBotSkill tier)
    {
        int difficulty = (int)tier;
        int? Shift(int? rating) => rating is { } r ? Difficulty.ShiftRating(r, difficulty) : null;
        return new AiSkillVector
        {
            DareDevil = Shift(personality.DareDevil),
            NaturalTouch = Shift(personality.NaturalTouch),
            SixthSense = Shift(personality.SixthSense),
            DeadEye = Shift(personality.DeadEye),
            QuickDraw = Shift(personality.QuickDraw),
            SteadyHand = Shift(personality.SteadyHand),
            StunRecovery = Shift(personality.StunRecovery),
            Talker = Shift(personality.Talker),
            Constitution = Shift(personality.Constitution),
        };
    }

    // "Bot <n>" by place, the first such name no other seat holds.
    private static string Fallback(int place, HashSet<string> used)
    {
        for (int n = place; ; n++)
        {
            string name = SessionSpec.BotCallsign(n);
            if (used.Add(name))
            {
                return name;
            }
        }
    }
}
