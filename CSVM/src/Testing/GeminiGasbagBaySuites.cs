using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>CM14's only route to a win, read at the seam that carries it. C2B/M04's OBJECTIVE13 is
/// gated on five of the six <c>deploy_gmzep_lbroadNN</c> animations going INVALID, and the only
/// events that write those states are the bays' own destruction. The Gemini can be killed by its
/// gasbags alone (five zones, <c>num_healthy_required 3</c>), so two authored chains are what keep
/// that kill from stranding the mission: each torpedoed gasbag's own death destroys the four bays
/// of its section, and the hull death's <c>all_gmzep_gasbags</c> pops every section that was NOT
/// torpedoed. Only sections 1 to 3 carry bays, so a kill on gasbags 3, 4 and 5 leaves four of the
/// six bays to the second chain alone, which is why that ordering, and not the tidy one, is what
/// this suite is really about (BL-694). Nothing here is flown and no pixel is read: the zones die
/// through the damage sink a torpedo reaches, and the verdict is the state the objective
/// reads.</summary>
internal static class GeminiGasbagBaySuites
{
    private const string Chapter = "C2B";
    private const string Mission = "M04";
    private const string Hull = "geminizep";
    private const float Tick = 1f / 30f;

    // The primary this suite exists for: OBJECTIVE13, IDENTITY [PRIMARY, 3, MSG_BRF_HWM4_OBJ3].
    private const int ThirdPrimary = 13;

    // Long enough for both chains: the torpedo death's own 0.5 s offset, then the hull death's
    // all_gmzep_gasbags, whose twelve section calls are spaced 3 s apart, and the 6 s finish each
    // burn calls after them.
    private const float AfterKillSeconds = 60f;

    // The six left bays OBJECTIVE13 reads. The Gemini carries sections 1 to 3 and no others, so
    // gasbags 4 and 5 name lbroad41..52 that this hull does not have.
    private static readonly string[] LeftBays =
    {
        "lbroad11", "lbroad12", "lbroad21", "lbroad22", "lbroad31", "lbroad32",
    };

    [Suite("gemini-gasbag-bays",
        "killing C2B/M04's Gemini by its gasbags leaves every cannon bay destroyed, whichever " +
        "three gasbags went (BL-694). Each torpedoed gasbag's authored gmzep_gasbagtorpedoN " +
        "destroys the four bays of its own section; the hull death then plays all_gmzep_gasbags, " +
        "whose section burns pop the bays of every section that was not torpedoed. Gasbags 1-3 " +
        "carry all six left bays and gasbags 4-5 carry none, so three orderings are run: the tidy " +
        "one, the one where four of the six bays can only come from the hull death, and that one " +
        "again with a bay shot off a still-flying hull first. Every time all six " +
        "deploy_gmzep_lbroadNN go INVALID and OBJECTIVE13's own ANIM_STATE count is met")]
    internal static void GeminiGasbagBays(TestContext ctx)
    {
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, Chapter, Mission);
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, Chapter);
        ctx.RequireData(missionZrdr, $"{Chapter}/{Mission} zrdr");
        ctx.RequireData(chapterZrdr, $"{Chapter} zrdr");

        var report = new StringBuilder();
        var script = ObjectiveScript.Load(missionZrdr);

        // The three orderings, each on its own world: a mission-override world is never cached, so
        // every leg starts from an untouched hull.
        Leg(ctx, missionZrdr, chapterZrdr, script, report, new[] { 1, 2, 3 }, null,
            "the three sections that carry the bays");
        Leg(ctx, missionZrdr, chapterZrdr, script, report, new[] { 3, 4, 5 }, null,
            "two sections that carry none, so sections 1 and 2 fall to the hull death alone");
        Leg(ctx, missionZrdr, chapterZrdr, script, report, new[] { 3, 4, 5 }, "lbroad21",
            "a bay shot first, so its own section goes before the gasbags do");

        ctx.WriteArtifact("test-gemini-gasbag-bays.txt", report.ToString());
    }

    private static void Leg(TestContext ctx, string missionZrdr, string chapterZrdr,
        ObjectiveScript script, StringBuilder report, IReadOnlyList<int> killedBags,
        string? firstBay, string what)
    {
        report.AppendLine($"=== gasbags {string.Join(", ", killedBags)}" +
            (firstBay == null ? "" : $" after {firstBay}") + $": {what}");
        ctx.WithWorld(Chapter, collision: false, mission: Mission, world =>
        {
            var runtime = world.Runtime;
            var defs = Zeppelins.Load(missionZrdr);
            var nets = AiNets.Load(chapterZrdr);
            ZeppelinRuntime? zeps = null;
            try
            {
                zeps = new ZeppelinRuntime(defs,
                    name => runtime.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null, nets);
                zeps.WireDamage(runtime);
                Run(ctx, runtime, zeps, script, report, killedBags, firstBay, what);
            }
            finally
            {
                zeps?.Free();
            }
        });
        report.AppendLine();
    }

    private static void Run(TestContext ctx, AnimRuntime runtime, ZeppelinRuntime zeps,
        ObjectiveScript script, StringBuilder report, IReadOnlyList<int> killedBags,
        string? firstBay, string what)
    {
        string bags = string.Join(", ", killedBags);
        var hull = runtime.FindNodes(Hull).FirstOrDefault();
        ctx.Check(hull != null, $"the {Hull} world node resolves in the {Chapter}/{Mission} world");
        if (hull == null)
        {
            return;
        }

        // The record carries `deactivated 1`, so the mission script's WAKEUP_ENEMIES is what puts
        // its pools in play. Without it every zone pool is Dormant and refuses damage.
        ctx.Check(zeps.Wake(Hull),
            $"{Hull} starts dormant on its record and the mission wake puts it in play (gasbags {bags})");

        // Let the bootstrap's anchored RESET_STATEs settle: every bay is stowed from its deploy
        // definition's reset, which is the pose the kill has to reach through.
        for (int i = 0; i < 30; i++)
        {
            runtime.Advance(Tick);
        }

        var pools = new Dictionary<string, DestructibleRegistry.Instance>();
        foreach (string bay in LeftBays)
        {
            var node = runtime.FindNodes(bay, hull).FirstOrDefault();
            if (node != null && runtime.Destructibles.PoolsOn(node).FirstOrDefault() is { } pool)
            {
                pools[bay] = pool;
            }
        }

        ctx.Same(LeftBays.Length, pools.Count,
            $"each of the {Hull}'s six left bays carries its own HEALTH pool pooled={pools.Count} (gasbags {bags})");

        int invalidBefore = InvalidDeploys(runtime);
        ctx.Same(0, invalidBefore,
            $"no deploy_gmzep_lbroad definition is INVALID before the kill invalid={invalidBefore} (gasbags {bags})");

        // The reverse ordering: a bay destroyed while the Gemini still flies. It is deployed first
        // because a stowed bay takes nothing through its shut hatch (zeppelin-cannon-stowed), which
        // is also why a player only ever meets an open one.
        if (firstBay != null && pools.TryGetValue(firstBay, out var shot))
        {
            runtime.Play($"deploy_gmzep_{firstBay}", shot.Anchor);
            for (int i = 0; i < (int)(6f / Tick); i++)
            {
                runtime.Advance(Tick);
            }
            runtime.DamageAt(shot.Anchor, 10_000f);
            for (int i = 0; i < (int)(12f / Tick); i++)
            {
                runtime.Advance(Tick);
                zeps.SimStep(Tick);
            }
            report.AppendLine($"{firstBay} shot first: status={shot.Status}, " +
                $"{InvalidDeploys(runtime)} deploy definition(s) INVALID, {Hull} dead={zeps.IsDead(Hull)}");
            ctx.Check(shot.Status == DestructibleRegistry.State.Destroyed && !zeps.IsDead(Hull),
                $"{firstBay} is destroyed with the {Hull} still flying status={shot.Status}");
            ctx.Check(InvalidDeploys(runtime) >= 2,
                $"…and its own section's bays go with it invalid={InvalidDeploys(runtime)} of {LeftBays.Length}");
        }

        // The kill, through the same sink a torpedo's damage reaches: three gasbag zones to zero
        // leaves survivors 2 < the record's required 3.
        foreach (int bag in killedBags)
        {
            runtime.DamageAt(runtime.FindNodes($"gasbag{bag}", hull).FirstOrDefault(), 10_000f);
        }

        zeps.SimStep(Tick);
        runtime.Advance(Tick);
        ctx.Check(zeps.IsDead(Hull),
            $"gasbags {bags} down kills the {Hull} by survivor count ({zeps.SurvivorsOf(Hull)} left)");

        // Run to the cap, or to the moment the chain has finished, whichever comes first. A leg
        // that is going to fail pays the whole budget and a leg that passes does not.
        int steps = (int)(AfterKillSeconds / Tick);
        float ran = 0f;
        for (int i = 0; i < steps; i++)
        {
            runtime.Advance(Tick);
            zeps.SimStep(Tick);
            ran += Tick;
            // ⚠ The whole post-condition, never the INVALID count alone. A bay's destruction slot
            // invalidates BOTH deploys of its pair, so three bay deaths latch all six states while
            // their three sister POOLS are still waiting on the +1 s and +8 s section burns.
            if (Destroyed(pools) == LeftBays.Length && InvalidDeploys(runtime) == LeftBays.Length
                && runtime.AnimStateOf("killgmzep") != 0)
            {
                break;
            }
        }

        report.AppendLine($"chain settled at t={ran:0.0} s of the {AfterKillSeconds:0} s cap");

        // The hull death chain itself, which no suite reached before this one: both definitions
        // self-invalidate as they run, so a state of 0 is the one reading that means "never ran".
        int hullDeath = runtime.AnimStateOf("all_gmzep_gasbags");
        int kill = runtime.AnimStateOf("killgmzep");
        report.AppendLine($"all_gmzep_gasbags state={hullDeath}, killgmzep state={kill}");
        ctx.Check(hullDeath != 0,
            $"the kill plays the authored hull death all_gmzep_gasbags state={hullDeath} (gasbags {bags})");
        ctx.Check(kill != 0, $"…which calls killgmzep state={kill} (gasbags {bags})");

        int destroyed = Destroyed(pools);
        foreach (string bay in LeftBays)
        {
            var pool = pools.TryGetValue(bay, out var p) ? p : null;
            report.AppendLine($"{bay}: hp={pool?.Health ?? -1f:0.##} status={pool?.Status.ToString() ?? "unpooled"} " +
                $"deploy_gmzep_{bay}={runtime.AnimStateOf($"deploy_gmzep_{bay}")}");
        }

        ctx.Same(LeftBays.Length, destroyed,
            $"every left bay is destroyed by the two authored chains destroyed={destroyed} of {LeftBays.Length} (gasbags {bags})");

        int invalid = InvalidDeploys(runtime);
        ctx.Same(LeftBays.Length, invalid,
            $"…and each bay's destruction latches its deploy definition INVALID invalid={invalid} of {LeftBays.Length} (gasbags {bags})");

        // The objective's own authored condition, counted over the states above rather than over a
        // number written here: OBJECTIVE13 is the mission's only route to a win.
        var def = script.ByNumber(ThirdPrimary);
        ctx.Check(def is { AnimStates.Count: > 0 },
            $"OBJECTIVE{ThirdPrimary} carries the ANIM_STATE condition this suite is about");
        if (def is not { AnimStates.Count: > 0 })
        {
            return;
        }

        int matched = def.AnimStates.Count(e => runtime.AnimStateOf(e.Name) == e.State);
        int required = def.AnimStateCount ?? def.AnimStates.Count;
        int authored = def.AnimStates.Count;
        report.AppendLine($"OBJECTIVE{ThirdPrimary}: {matched} of {authored} authored states met, COMPLETION_COUNT {required}");
        ctx.Check(matched >= required,
            $"OBJECTIVE{ThirdPrimary}'s authored ANIM_STATE count is met by the gasbag kill alone matched={matched} required={required} of {authored} (gasbags {bags})");
        ctx.Note($"{Chapter}/{Mission} gasbags {bags} ({what}): {destroyed} left bay(s) destroyed, OBJECTIVE{ThirdPrimary} reads {matched} of its {required}");
    }

    private static int Destroyed(Dictionary<string, DestructibleRegistry.Instance> pools) =>
        pools.Values.Count(p => p.Status == DestructibleRegistry.State.Destroyed);

    private static int InvalidDeploys(AnimRuntime runtime) =>
        LeftBays.Count(bay => runtime.AnimStateOf($"deploy_gmzep_{bay}") == 4);
}
