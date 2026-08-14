using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;

namespace CSVM.Session;

/// <summary>Owns one Instant Action mission's actor set, as it grows across the plan's later
/// waves (PLAN-instant-action.md: E10/E11 the wave sequencer, F12 the zeppelin arm). C8 wired
/// <c>dogfight_ace</c>'s authored ace; D9 adds the wingmen. <c>GameSession.BuildFlightRigs</c>
/// reads <see cref="Def"/> directly to steer the player's own aircraft and spawn scenario, and
/// calls the helpers below to place the ace and the wingmen. Environment→chapter resolution is
/// the launch MENU's job (H15), not this class's — a <c>--ia=&lt;path&gt;</c> CLI launch already
/// names its chapter via <c>--chapter=</c>.</summary>
public sealed class InstantActionRuntime
{
    /// <summary>Every Instant Action enemy's team (PLAN-instant-action.md Decision 4: "the
    /// decoded turret convention... every Instant Action enemy is team 2"). Waves (E11) are
    /// cohorts inside this one team, not teams of their own.</summary>
    public const int EnemyTeam = AimAssist.PlayerTeam + 1;

    public InstantActionRuntime(InstantActionDef def)
    {
        Def = def;
    }

    public InstantActionDef Def { get; }

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

    /// <summary>One wingman's standing order (docs/formats/instant-action.md "The player and the
    /// wingmen", PLAN-instant-action.md D9): its fan placement off the player's spawn heading —
    /// <c>100 · ((i &gt;&gt; 1) + 1)</c> metres out, at ±45°, the same 100 m/45° pattern the wave
    /// sequencer uses — its <see cref="PrimaryTargetIsWingman"/> escort chain (0, 1 and 3 escort
    /// the player; 2 and 4 escort wingmen 1 and 3), and its authored accent id.</summary>
    public readonly record struct WingmanSlot(float MetresOut, float OffsetDeg, int? PrimaryTargetIsWingman, int AccentId);
}
