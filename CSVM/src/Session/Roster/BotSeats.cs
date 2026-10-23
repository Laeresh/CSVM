using System;
using System.Collections.Generic;
using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Session.InstantAction;
using CSVM.Spec;

namespace CSVM.Session.Roster;

/// <summary>Where a seat's weapons come from (<see cref="BotSeats.ArmsFor"/>).</summary>
public enum SeatArms
{
    /// <summary>A race seat: the stock or picked fit, for a course that needs a structure shot
    /// open (C2's hangar gate). No round reaches an aircraft in a race, and a bot's gunner takes
    /// only the structure its race orders.</summary>
    Race,

    /// <summary>A bot's militia def's own weapons block.</summary>
    MilitiaDef,

    /// <summary>The airframe's stock fit, or the one its pilot picked or built.</summary>
    Stock,
}

/// <summary>
/// What a bot seat is once its host seats it. It draws a militia and the airframe that militia
/// flies from the Instant Action militia table, and a callsign from the shipped pilot names. Its
/// pilot takes an Instant Action personality shifted by its tier. Engine-free and host-only by its
/// callers: a guest reads the roster the host built and draws nothing. The table, the names, the
/// personalities and the offset are the original's data; the rules joining them are remake-only.
/// Decodes:
/// <c>docs/formats/missions.md</c> "Message table" and <c>docs/formats/instant-action.md</c>.
/// </summary>
public static class BotSeats
{
    /// <summary>The longest callsign a bot carries: the Callsign box's own 12 characters
    /// (<c>UGA.FD</c>, <c>docs/org/multiplayer-messages.md</c>), the limit a person's seat has.
    /// </summary>
    public const int CallsignLimit = 12;

    // The torpedo pair's two halves, as the militia table names them.
    private const string TorpedoMilitia = "Black Hat";
    private const string TorpedoAirframe = "Warhawk";

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

    private static readonly (byte Militia, string Plane)[] PairRows = BuildPairs();

    /// <summary>The message-table keys the callsign pool is read from, in id order.</summary>
    public static IReadOnlyList<string> PilotNames => PilotNameKeys;

    /// <summary>Every militia/airframe pair the militia table allows, as a militia row and a stock
    /// node, in the table's order (docs/formats/instant-action.md "Militias and aircraft").</summary>
    public static IReadOnlyList<(byte Militia, string Plane)> Pairs => PairRows;

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

    /// <summary><see cref="CallsignPool(Messages)"/> over the message table extracted under
    /// <paramref name="dataRoot"/>, the pool a menu page offers its bot rows.</summary>
    public static IReadOnlyList<string> LoadCallsignPool(string dataRoot) =>
        CallsignPool(Messages.Load(System.IO.Path.Combine(dataRoot, "extracted", "messages.json")));

    /// <summary>A pilot name brought within <see cref="CallsignLimit"/> by dropping whole words from
    /// the front. A long name keeps its surname: "Sir Charles Emmett Winthrop" is "Winthrop". A
    /// last word longer than the limit is cut to it, and a name that fits is returned trimmed.
    /// </summary>
    public static string CutName(string name)
    {
        string rest = (name ?? "").Trim();
        while (rest.Length > CallsignLimit && rest.IndexOf(' ') is int space and > 0)
        {
            rest = rest[(space + 1)..].TrimStart();
        }

        return rest.Length <= CallsignLimit ? rest : rest[..CallsignLimit];
    }

    /// <summary>A host-given callsign cut as the Callsign box cuts what is typed into it: at
    /// <see cref="CallsignLimit"/> characters, whatever the words.</summary>
    public static string ClipName(string name)
    {
        string trimmed = (name ?? "").Trim();
        return trimmed.Length <= CallsignLimit ? trimmed : trimmed[..CallsignLimit].TrimEnd();
    }

    /// <summary>The bots as the host seats them, each on a militia/airframe pair (<see cref="DrawPair"/>).
    /// A bot with no callsign draws one from <paramref name="pool"/>, shuffled once, never a name drawn
    /// before, named by another bot or in <paramref name="taken"/>. A spent pool falls back to
    /// <see cref="SessionSpec.BotCallsign"/>. Zeppelin vs Zeppelin caps torpedoes
    /// (<paramref name="oneTorpedoPerTeam"/>). A plane <paramref name="rules"/> refuse is drawn as a
    /// Random one. A bot they leave no airframe for is left out, as a full field leaves one out.</summary>
    public static IReadOnlyList<SeatedBot> Resolve(
        IReadOnlyList<VsBotEntry> bots, IEnumerable<string> taken, IReadOnlyList<string> pool, Random rng,
        bool oneTorpedoPerTeam = false, NetPlaneRules? rules = null, Func<byte, string, bool>? armsAdmitted = null)
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

        var seated = new List<SeatedBot>(bots.Count);
        var torpedoTeams = new HashSet<int>();
        int next = 0;
        for (int i = 0; i < bots.Count; i++)
        {
            var bot = bots[i];
            string? asked = bot.Plane != null && rules is { } outlaws && !AirframeAdmitted(outlaws, bot.Plane) ? null : bot.Plane;
            var pair = DrawPair(asked, oneTorpedoPerTeam && torpedoTeams.Contains(bot.Team), rng, rules, armsAdmitted);
            if (pair == null && asked == null)
            {
                continue;
            }

            if (pair is { } drawn && IsTorpedoPair(drawn.Militia, drawn.Plane))
            {
                torpedoTeams.Add(bot.Team);
            }

            string plane = pair?.Plane ?? bot.Plane!;
            byte militia = pair?.Militia ?? NetSeat.Unset;
            byte accent = pair is { } p
                ? (byte)InstantActionRuntime.ResolveWaveAccentId(Mech3.InstantAction.Militias[p.Militia].AccentId, (uint)rng.Next())
                : NetSeat.Unset;
            string callsign = ClipName(bot.Callsign);
            if (callsign.Length == 0)
            {
                while (next < order.Count && !used.Add(order[next]))
                {
                    next++;
                }

                callsign = next < order.Count ? order[next++] : Fallback(i + 1, used);
            }

            seated.Add(new SeatedBot(plane, bot.Skill, bot.Team, callsign, militia, accent));
        }

        return seated;
    }

    /// <summary>A pair at equal odds over <see cref="Pairs"/>: any pair for a Random plane (null),
    /// else one whose militia flies the plane. A torpedo cap leaves the torpedo pair out, the same
    /// odds as re-rolling it. Null for a plane no militia flies. <paramref name="rules"/> leave out
    /// each airframe they refuse, and <paramref name="armsAdmitted"/> each pair whose def they strip.
    /// Null for a Random plane when the rules refuse all eleven airframes.</summary>
    public static (byte Militia, string Plane)? DrawPair(string? plane, bool noTorpedo, Random rng,
        NetPlaneRules? rules = null, Func<byte, string, bool>? armsAdmitted = null)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var candidates = new List<(byte Militia, string Plane)>();
        foreach (var pair in PairRows)
        {
            if ((plane == null || string.Equals(pair.Plane, plane, StringComparison.OrdinalIgnoreCase))
                && !(noTorpedo && IsTorpedoPair(pair.Militia, pair.Plane))
                && (rules is not { } outlaws || AirframeAdmitted(outlaws, pair.Plane))
                && armsAdmitted?.Invoke(pair.Militia, pair.Plane) != false)
            {
                candidates.Add(pair);
            }
        }

        return candidates.Count == 0 ? null : candidates[rng.Next(candidates.Count)];
    }

    /// <summary>Remake-only rule (docs/org/multiplayer-messages.md, the bot outlaw paragraph).
    /// Whether <paramref name="rules"/> admit a bot on stock node <paramref name="plane"/>. They are
    /// asked as for a person's stock pick (<see cref="NetPlaneRules.Refuses"/>): the airframe, the
    /// stock engine and the stock guns' calibres. A node outside the eleven passes.</summary>
    public static bool AirframeAdmitted(NetPlaneRules rules, string plane) =>
        StockAirframes.IdOf(plane) is not { } airframe || rules.AdmitsStock(airframe);

    /// <summary>Remake-only rule (docs/org/multiplayer-messages.md, the bot outlaw paragraph).
    /// The fit a bot on its airframe's stock arms flies under <paramref name="rules"/>. It is
    /// the stock fit where they allow it. Otherwise every gun takes the first ammunition they allow
    /// and every pylon the first rocket, none only where they allow none. Enforce clears to none,
    /// and a bot has no second Ready to pick again.</summary>
    public static CoopFit LegalFit(NetPlaneRules rules)
    {
        int ammo = rules.AmmoOutlawed(default) ? FirstAllowed(rules.AmmoAllowed, NetPlaneRules.NoAmmo) : -1;
        int row = rules.RocketsOutlawed(default) ? FirstAllowed(rules.RocketAllowed, NetPlaneRules.NoRocket) : -1;
        var ammoPicks = new int[CoopFit.GunSlots];
        Array.Fill(ammoPicks, ammo);
        var cells = new int[CoopFit.Cells];
        Array.Fill(cells, row + 1);
        return CoopFit.Of(ammoPicks, cells);
    }

    /// <summary>Remake-only rule (docs/org/multiplayer-messages.md, the bot outlaw paragraph).
    /// Whether <paramref name="rules"/> allow every weapon a militia def's block carries. An
    /// AI gun fires slug, so it is asked as its calibre's slug. An ordnance id is asked as its row of
    /// <paramref name="rocketTable"/>; one off the table answers to Outlaw All Rockets alone.</summary>
    public static bool ArmsAdmitted(NetPlaneRules rules, IEnumerable<AiWeaponSlot> weapons, WeaponDefs defs,
        IReadOnlyList<LoadoutOption> rocketTable)
    {
        foreach (var slot in weapons)
        {
            int row = -1;
            for (int i = 0; i < rocketTable.Count && row < 0; i++)
            {
                row = string.Equals(rocketTable[i].Id, slot.WeaponId, StringComparison.OrdinalIgnoreCase) ? i : -1;
            }

            bool allowed = defs.Get(slot.WeaponId) is { Caliber: int caliber }
                ? rules.GunAllowed(Math.Clamp((caliber - 30) / 10, 0, 4)) && rules.AmmoAllowed(0)
                : row >= 0 ? rules.RocketAllowed(row) : !(rules.Outlawing && rules.Has(NetPlaneRules.AllRocketsFlag));
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Where a seat's weapons come from. A race seat takes the race's arms, whatever its
    /// militia. Otherwise a named <c>--loadout=</c> wins, then a bot's militia def with a weapons
    /// block, then the airframe's stock or picked fit.</summary>
    public static SeatArms ArmsFor(bool racing, bool loadoutNamed, bool militiaWeapons) =>
        racing ? SeatArms.Race : !loadoutNamed && militiaWeapons ? SeatArms.MilitiaDef : SeatArms.Stock;

    /// <summary>Whether a pair carries torpedoes. Only Black Hat's Warhawk does: its def,
    /// <c>bhatwarhawk</c>, fits <c>wep_14</c> (docs/org/aiPilot/aiWeapons.md).</summary>
    public static bool IsTorpedoPair(byte militia, string plane) =>
        Mech3.InstantAction.MilitiaAt(militia)?.Name == TorpedoMilitia
        && string.Equals(plane, Mech3.InstantAction.PlaneNodeFor(TorpedoAirframe), StringComparison.OrdinalIgnoreCase);

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

    private static (byte Militia, string Plane)[] BuildPairs()
    {
        var pairs = new List<(byte, string)>();
        for (int m = 0; m < Mech3.InstantAction.Militias.Count; m++)
        {
            foreach (string aircraft in Mech3.InstantAction.Militias[m].Aircraft)
            {
                pairs.Add(((byte)m, Mech3.InstantAction.PlaneNodeFor(aircraft)!));
            }
        }

        return pairs.ToArray();
    }

    // The lowest index below none that the list allows, else none.
    private static int FirstAllowed(Func<int, bool> allowed, int none)
    {
        for (int i = 0; i < none; i++)
        {
            if (allowed(i))
            {
                return i;
            }
        }

        return none;
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
