using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;

namespace CSVM.Session;

/// <summary>Owns one Instant Action mission's actor set, as it grows across the plan's later
/// waves (PLAN-instant-action.md: D9 wingmen, E10/E11 the wave sequencer, F12 the zeppelin arm).
/// This item (C8) wires only <c>dogfight_ace</c>: <c>GameSession.BuildFlightRigs</c> reads
/// <see cref="Def"/> directly to steer the player's own aircraft and spawn scenario, and calls
/// the two helpers below to place the authored ace. Environment→chapter resolution is the launch
/// MENU's job (H15), not this class's — a <c>--ia=&lt;path&gt;</c> CLI launch already names its
/// chapter via <c>--chapter=</c>.</summary>
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
}
