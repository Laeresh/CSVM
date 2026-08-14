using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;

namespace CSVM.Session;

/// <summary>Owns one Instant Action mission's actor set, as it grows across the plan's later
/// waves (PLAN-instant-action.md: E11 the wave sequencer, F12 the zeppelin arm). C8 wired
/// <c>dogfight_ace</c>'s authored ace; D9 added the wingmen; E11 adds the two per-wave-member
/// draws (<see cref="RandomPilotStats"/>, <see cref="ResolveWaveAccentId"/>) that the actual
/// selection/trigger/geometry logic (<see cref="InstantActionWaves"/>, a separate engine-free
/// class) does not own; F12 adds the objective-zeppelin selection
/// (<see cref="IsZeppelinRun"/>, <see cref="SelectedZeppelinNode"/>).
/// <c>GameSession.BuildFlightRigs</c> reads <see cref="Def"/> directly to
/// steer the player's own aircraft and spawn scenario, and calls the helpers below to place the
/// ace, the wingmen and (with <see cref="InstantActionWaves"/>) each wave. Environment→chapter
/// resolution is the launch MENU's job (H15), not this class's — a <c>--ia=&lt;path&gt;</c> CLI
/// launch already names its chapter via <c>--chapter=</c>.</summary>
public sealed class InstantActionRuntime
{
    /// <summary>Every Instant Action enemy's team (PLAN-instant-action.md Decision 4: "the
    /// decoded turret convention... every Instant Action enemy is team 2"). Waves (E11) are
    /// cohorts inside this one team, not teams of their own.</summary>
    public const int EnemyTeam = AimAssist.PlayerTeam + 1;

    /// <summary><c>mission_type</c> id 2 (docs/formats/instant-action.md "The mission-type ids"),
    /// the one mode whose enemies reach the air out of the objective zeppelin's bay rather than
    /// through the wave teleport (F12).</summary>
    public const string ZeppelinRunMissionType = "zeppelin_run";

    /// <summary>The five hand-authored pilot personalities a wave member's nine-stat vector is
    /// rolled from, <c>row = draw % 5</c> per aircraft (docs/formats/instant-action.md "A wave
    /// enemy's nine pilot stats are drawn at random from a table of five, not from its skill" —
    /// <c>FUN_0045a280</c>, <c>0x00607a3c</c>). Same field order/scale as
    /// <see cref="InstantActionDef.AceStats"/>: <c>dare_devil, natural_touch, sixth_sense,
    /// dead_eye, quick_draw, steady_hand, stun_recovery, talker, constitution</c>. Row 4 is a
    /// flat 4 across every stat; the other four are hand-authored personalities.</summary>
    private static readonly int[][] PilotPersonalities =
    {
        new[] { 5, 7, 5, 3, 2, 1, 4, 4, 4 },
        new[] { 4, 3, 4, 5, 7, 5, 3, 5, 5 },
        new[] { 3, 4, 3, 7, 3, 6, 4, 5, 4 },
        new[] { 7, 5, 6, 3, 3, 2, 4, 4, 4 },
        new[] { 4, 4, 4, 4, 4, 4, 4, 4, 4 },
    };

    public InstantActionRuntime(InstantActionDef def)
    {
        Def = def;
    }

    public InstantActionDef Def { get; }

    /// <summary>Whether this mission is the zeppelin run (F12). The mode is exclusive in the
    /// decode rather than additive: its waves take the generator arm and the teleport arm never
    /// runs (<see cref="InstantActionWaves"/>), and its objective zeppelin is the one world node
    /// the builder switches ON rather than off (<see cref="SelectedZeppelinNode"/>).</summary>
    public bool IsZeppelinRun =>
        string.Equals(Def.MissionType, ZeppelinRunMissionType, StringComparison.OrdinalIgnoreCase);

    /// <summary>The three <c>*_zeppelin</c> node names in <c>zeppelin_type</c> order — 0 cargo,
    /// 1 passenger, 2 military (docs/formats/instant-action.md "Which zeppelin, and which spawn
    /// list"). All eight shipped chapters author the same node three times
    /// (<c>multiplayer1zep</c>), so the list is one distinct name in every real case; a
    /// hand-authored <c>--ia=</c> file may name three.</summary>
    public static IReadOnlyList<string> ZeppelinNodes(InstantActionDef def) =>
        new[] { def.CargoZeppelinNode, def.PassengerZeppelinNode, def.MilitaryZeppelinNode };

    /// <summary><c>zeppelin_type</c>'s own index into <see cref="ZeppelinNodes"/>:
    /// <c>cargo</c> 0, <c>passenger</c> 1, <c>military</c> 2. ⚠ Anything else — including an
    /// unauthored key — is <b>0</b>, not an error: <c>FUN_00458f60</c> maps an unrecognised
    /// string to 3 and REJECTS it rather than storing it, over a record whose reset wrote
    /// <c>+0x254 = 0</c> (<c>FUN_00458ff0</c>, <c>param_1[0x95] = 0</c>), so cargo is the
    /// decoded fallback.</summary>
    public static int ZeppelinTypeIndex(string? zeppelinType) =>
        string.Equals(zeppelinType, "passenger", StringComparison.OrdinalIgnoreCase) ? 1
        : string.Equals(zeppelinType, "military", StringComparison.OrdinalIgnoreCase) ? 2
        : 0;

    /// <summary>The objective zeppelin's world node: the <see cref="ZeppelinNodes"/> entry
    /// <c>zeppelin_type</c> selects. On <c>zeppelin_run</c> this is the node the builder
    /// ACTIVATES and whose generator the wave sequencer credits; on every other mode it is
    /// switched off along with the other two.</summary>
    public static string SelectedZeppelinNode(InstantActionDef def) =>
        ZeppelinNodes(def)[ZeppelinTypeIndex(def.ZeppelinType)];

    /// <summary>The ace's own spawn draw (docs/formats/instant-action.md "The ace and the
    /// waves"): <c>rand() % (count - 1)</c> over the scenario's own spawn list, with the LITERAL
    /// last index substituted — not a re-roll — when that draw collides with the player's own
    /// chosen index. Pure over its inputs (<paramref name="draw"/> is the caller's own
    /// <c>rand()</c> pull, so a <c>--det</c> run stays reproducible without this class touching
    /// an RNG itself). <paramref name="spawns"/> must be non-empty.</summary>
    public static (int Index, SpawnPoint Point) ChooseAceSpawn(
        IReadOnlyList<SpawnPoint> spawns, int playerSpawnIndex, uint draw)
    {
        if (spawns.Count == 1)
            return (0, spawns[0]);
        int idx = (int)(draw % (uint)(spawns.Count - 1));
        if (idx == playerSpawnIndex)
            idx = spawns.Count - 1;
        return (idx, spawns[idx]);
    }

    /// <summary>Collapses an <see cref="AiSkillVector"/> into the one flat 1–9 rating CSVM's own
    /// AI tuning takes (<c>AiSkills.At(key, rating)</c>, the same scale <c>--ai-attack=</c>
    /// drives) — the average of whichever slots are set, rounded and clamped. Every shipped
    /// chapter's <c>ace_stats</c> is a uniform 9 (docs/formats/spawns.md "ace_stats": the data
    /// cannot even distinguish the nine slots' order), so this reduces to exactly 9 in every real
    /// case; a hand-authored <c>--ia=</c> file with mixed values still gets a sensible single
    /// number rather than nine independently-wired curves nothing in the shipped data can verify.
    /// 5 (the same default <c>--ai-attack=</c> takes) when every slot is unset.</summary>
    public static int RepresentativeRating(AiSkillVector v)
    {
        int sum = 0, n = 0;
        foreach (int? s in new[]
                 {
                     v.DareDevil, v.NaturalTouch, v.SixthSense, v.DeadEye, v.QuickDraw,
                     v.SteadyHand, v.StunRecovery, v.Talker, v.Constitution,
                 })
        {
            if (s is { } val)
            {
                sum += val;
                n++;
            }
        }
        return n > 0 ? System.Math.Clamp((int)System.MathF.Round(sum / (float)n), 1, 9) : 5;
    }

    /// <summary>The five wingman slots' standing orders, 0-based. <paramref name="i"/> must be
    /// 0–4 (<see cref="InstantActionDef.NumWingmen"/>'s own 0–5 range, minus the clamp a flight of
    /// 6 imposes — see <see cref="FlownWingmen"/>).</summary>
    public static WingmanSlot WingmanSlotFor(int i)
    {
        float metres = 100f * ((i >> 1) + 1);
        float sign = (i & 3) is 1 or 2 ? 1f : -1f;
        int? escorts = i switch { 2 => 1, 4 => 3, _ => null };
        int accentId = new[] { 12, 14, 15, 13, 16 }[i];
        return new WingmanSlot(metres, sign * 45f, escorts, accentId);
    }

    /// <summary>Decision 8a's flight-size cap: the friendly flight is capped at 6 aircraft (1
    /// pilot + 5 wingmen, the data's own maximum), and wingmen are the ones that give —
    /// <c>min(configured, 6 - humans)</c>, never negative. Splitscreen humans add to the flight
    /// rather than consuming the wingman budget (Decision 8); below the cap the configured count
    /// is returned untouched. The clamp rule itself is INVENTED (decision 8a) and its caller must
    /// report it rather than apply it silently.</summary>
    public static int FlownWingmen(int configured, int humans) =>
        System.Math.Max(0, System.Math.Min(configured, 6 - humans));

    /// <summary>One wave member's nine pilot stats — the wave sequencer's own per-aircraft roll
    /// (A3/A4): <c>row = draw % 5</c> over <see cref="PilotPersonalities"/>. Pure over the
    /// caller's own <c>rand()</c> pull, same shape as <see cref="ChooseAceSpawn"/>; feed the
    /// result to <see cref="RepresentativeRating"/> for the one flat rating
    /// <c>AiAircraftSpawner.Spawn</c>'s <c>attackRating</c> takes, CSVM's AI tuning having no
    /// per-stat curves of its own to hang the full vector on.</summary>
    public static AiSkillVector RandomPilotStats(uint draw)
    {
        var row = PilotPersonalities[draw % 5];
        return new AiSkillVector
        {
            DareDevil = row[0],
            NaturalTouch = row[1],
            SixthSense = row[2],
            DeadEye = row[3],
            QuickDraw = row[4],
            SteadyHand = row[5],
            StunRecovery = row[6],
            Talker = row[7],
            Constitution = row[8],
        };
    }

    /// <summary>The wingman accent range's own re-roll (docs/formats/instant-action.md "an
    /// accentID of exactly 12 is re-rolled as 12 + rand() % 5"), applied to a wave member's
    /// <see cref="InstantActionWave.EnemyAccentId"/> at spawn time — never to the ace's or a
    /// wingman's own accent, both of which are already decided elsewhere. Pure over the caller's
    /// own <c>rand()</c> pull; any other accent id (including the built-in -1 default) passes
    /// through unchanged.</summary>
    public static int ResolveWaveAccentId(int accentId, uint draw) =>
        accentId == 12 ? 12 + (int)(draw % 5) : accentId;

    /// <summary>One wingman's standing order (docs/formats/instant-action.md "The player and the
    /// wingmen", PLAN-instant-action.md D9): its fan placement off the player's spawn heading —
    /// <c>100 · ((i &gt;&gt; 1) + 1)</c> metres out, at ±45°, the same 100 m/45° pattern the wave
    /// sequencer uses — its <see cref="PrimaryTargetIsWingman"/> escort chain (0, 1 and 3 escort
    /// the player; 2 and 4 escort wingmen 1 and 3), and its authored accent id.</summary>
    public readonly record struct WingmanSlot(float MetresOut, float OffsetDeg, int? PrimaryTargetIsWingman, int AccentId);
}
