using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSVM.Mech3;
using Godot;

namespace CSVM.Testing;

/// <summary>C2/M01's Spruce Goose taxis down a channel four tug-and-barges block. At each barge it
/// decelerates, and a 12 s test loop calls the next leg's accelerate. That call is gated on the
/// barge's <c>snkchk</c> going inactive, which its death writes once it has rolled over.</summary>
internal static class GooseBargeSuites
{
    private const float WaitDt = 0.25f;

    [Suite("goose-barge-chain",
        "C2/M01's Spruce Goose past all four barges: free_the_goose runs the first leg, the goose " +
        "stops at each tugandbarge0N (pathN+1_decelerate), the barge's DamageAt death switches " +
        "its snkchk off and pathN+1_accelerate starts and survives stopping its own caller, the " +
        "accel test loop (#367: it died with it and the goose never left the second barge), " +
        "until fly_the_goose")]
    internal static void GooseBargeChain(TestContext ctx) => Run(ctx, early: false);

    [Suite("goose-barge-early",
        "C2/M01's Spruce Goose with tugandbarge01 and 02 sunk before it reaches them: each fork " +
        "takes the continue leg (the tug's healthy node is off), and the goose stops at the live " +
        "tugandbarge03 (path4_decelerate)")]
    internal static void GooseBargeEarly(TestContext ctx) => Run(ctx, early: true);

    private static void Run(TestContext ctx, bool early)
    {
        ctx.RequireData(ctx.MissionZrdr("C2", "M01"), $"C2/M01 zrdr");
        var report = new StringBuilder();
        ctx.WithWorld("C2", collision: true, mission: "M01", world =>
        {
            var runtime = world.Runtime;
            var goose = runtime.FindNodes("sprucegoose").FirstOrDefault();
            ctx.Check(goose != null, $"sprucegoose resolves in the M01 world");
            if (goose == null)
                return;
            var startedAt = new List<string>();
            var started = new HashSet<string>();
            float clock = 0f;
            var saved = runtime.OnInstanceStarted;
            runtime.OnInstanceStarted = (d, _) =>
            {
                if (d.AnimName == null)
                    return;
                started.Add(d.AnimName);
                startedAt.Add($"{d.AnimName}@{clock:0.0}");
            };

            // Steps until the named animation has started, or the ceiling; true when it did.
            bool RunUntil(string anim, float ceiling)
            {
                for (float t = 0f; t < ceiling && !started.Contains(anim); t += WaitDt)
                {
                    clock += WaitDt;
                    runtime.Advance(WaitDt);
                }
                return started.Contains(anim);
            }

            void Sink(string barge)
            {
                var node = runtime.FindNodes(barge).FirstOrDefault();
                var pool = node == null ? null : runtime.Destructibles.PoolsOn(node).FirstOrDefault();
                ctx.Check(pool != null, $"{barge} carries a pool");
                if (node != null && pool != null)
                    runtime.DamageAt(node, pool.MaxHealth + 1f);
                report.AppendLine($"{barge} killed at {clock:0.0} goose at {goose.GlobalPosition}");
            }

            try
            {
                runtime.PlayMissionTrigger("free_the_goose");
                if (early)
                {
                    Sink("tugandbarge01");
                    Sink("tugandbarge02");
                    ctx.Check(RunUntil("path2_continue", 300f),
                        $"the goose passes the dead tugandbarge01 (path2_continue) at {clock:0.0} s");
                    ctx.Check(RunUntil("path3_continue", 300f),
                        $"the goose passes the dead tugandbarge02 (path3_continue) at {clock:0.0} s");
                    ctx.Check(RunUntil("path4_decelerate", 300f),
                        $"the goose stops at the live tugandbarge03 (path4_decelerate) at {clock:0.0} s");
                    return;
                }
                ctx.Check(RunUntil("path2_decelerate", 300f),
                    $"the goose stops at tugandbarge01 (path2_decelerate) at {clock:0.0} s");
                Sink("tugandbarge01");
                ctx.Check(RunUntil("path2_accelerate", 60f),
                    $"tugandbarge01's death lets path2_accelerate start at {clock:0.0} s");
                for (int leg = 3; leg <= 5; leg++)
                {
                    string barge = $"tugandbarge0{leg - 1}";
                    ctx.Check(RunUntil($"path{leg}_decelerate", 300f),
                        $"the goose stops at {barge} (path{leg}_decelerate) at {clock:0.0} s");
                    Sink(barge);
                    ctx.Check(RunUntil($"path{leg}_accelerate", 60f),
                        $"{barge}'s death lets path{leg}_accelerate start at {clock:0.0} s");
                }
                ctx.Check(RunUntil("fly_the_goose", 300f), $"the goose takes off (fly_the_goose) at {clock:0.0} s");
                report.AppendLine($"goose at {goose.GlobalPosition}");
            }
            finally
            {
                runtime.OnInstanceStarted = saved;
                report.AppendLine($"started: {string.Join(", ", startedAt)}");
            }
        });
        ctx.WriteArtifact(early ? "test-goose-barge-early.txt" : "test-goose-barge-chain.txt", report.ToString());
        ctx.Note($"C2/M01's goose: sinking each barge releases the next leg");
    }
}
