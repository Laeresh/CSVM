using System;
using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>The generator credit rule over C4/M03's own world: the original never reads the
/// authored <c>capacity</c>, so <c>cargozep1</c> launches nothing until the docking film's beauty
/// shot raises callback 800, which the mission-script host answers as five launches on that one
/// generator by name. The remake hosts that code through <see cref="AiGeneratorRuntime"/>'s
/// place in the runtime's <c>CALLBACK</c> chain.</summary>
internal static class GeneratorCreditSuites
{
    private const string Chapter = "C4";
    private const string Mission = "M03";
    private const string Generator = "cargozep1";
    private const string BeautyShot = "cg_beauty_shot";
    private const int CreditCode = 800;
    private const int ReactivateCode = 801;

    private const float StepDt = 1f / 60f;
    private const float IdleS = 30f;
    private const float LaunchWindowS = 30f;

    [Suite("generator-callback-credit",
        "the decoded credit rule over C4/M03's BUILT world (BL-657): cargozep1's generator "
        + "launches nothing through 30 s at altitude on its authored capacity 0, cutscene "
        + "callback 800 raised through the runtime's host chain is answered and credits the "
        + "generator by name with five launches, which then drop through the opened hangar doors "
        + "one every 4 s (ind 3 + wave 1) and stop at five, while 801 is declined")]
    internal static void GeneratorCallbackCredit(TestContext ctx)
    {
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, Chapter, Mission);
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, Chapter);
        ctx.RequireData(missionZrdr, $"{Chapter}/{Mission} zrdr");
        ctx.RequireData(chapterZrdr, $"{Chapter} zrdr");

        var defs = EnemyGenerators.Load(missionZrdr);
        EnemyGeneratorDef? def = null;
        foreach (var d in defs)
        {
            if (d.Node.Equals(Generator, StringComparison.OrdinalIgnoreCase))
            {
                def = d;
            }
        }
        ctx.Check(def is { Capacity: 0, WaveSize: 1 },
            $"'{Generator}' authors capacity 0 and wave_size 1 (the shipped record)");
        if (def == null)
        {
            return;
        }
        float period = def.IndPeriod + def.WavePeriod;

        ctx.WithWorld(Chapter, collision: false, Mission, world =>
        {
            var runtime = world.Runtime;
            var savedHost = runtime.CallbackHost;
            AiGeneratorRuntime? generators = null;
            try
            {
                float clock = 0f;
                var spawns = new List<float>();
                var doorPlays = new List<string>();
                generators = new AiGeneratorRuntime(defs,
                    (name, scope) => runtime.FindNodes(name, scope) is { Count: > 0 } hits ? hits[0] : null,
                    AiNets.Load(chapterZrdr), ctx.PlaneName,
                    (_, _, _, _) =>
                    {
                        spawns.Add(clock);
                        return default(LaunchedVehicle);
                    },
                    (name, _) => { doorPlays.Add(name); return 1; },
                    (_, _) => { });
                ctx.Check(generators.LiveCount > 0, $"'{Generator}' is live over the built world");

                // The moored hull is a world node with no zeppelin record (nothing flies it), so
                // its built pose is what the cycle's 250 m gate sees.
                var host = runtime.FindNodes(Generator) is { Count: > 0 } hosts ? hosts[0] : null;
                ctx.Check(host != null, $"'{Generator}' is a world node");
                if (host == null)
                {
                    return;
                }
                float hostY = host.GlobalPosition.Y;
                ctx.Check(def.MinAltitude is not { } gate || hostY > gate,
                    $"the moored hull sits above the launch gate y={hostY:0}");

                runtime.CallbackHost = null;
                generators.BindCallbackHost(runtime);
                ctx.Check(runtime.CallbackHost != null, $"the generator runtime holds the CALLBACK host slot");

                void Sim(float seconds)
                {
                    for (float t = 0f; t < seconds; t += StepDt)
                    {
                        clock += StepDt;
                        generators.SimStep(StepDt);
                    }
                }

                Sim(IdleS);
                ctx.Same(0, spawns.Count, $"uncredited, nothing launches in {IdleS:0} s at altitude");
                ctx.Same(0, doorPlays.Count, $"…and the hangar doors stay shut");

                ctx.Check(!runtime.CallbackHost!(ReactivateCode, BeautyShot, null),
                    $"callback {ReactivateCode} is not a credit and is declined");
                ctx.Check(runtime.CallbackHost!(CreditCode, BeautyShot, null),
                    $"callback {CreditCode} from '{BeautyShot}' is answered");
                ctx.Same(0, spawns.Count, $"the credit itself launches nothing until the cycle steps");

                Sim(LaunchWindowS);
                ctx.Same(AiGeneratorRuntime.CallbackCredit, spawns.Count,
                    $"exactly {AiGeneratorRuntime.CallbackCredit} launches follow the credit in {LaunchWindowS:0} s");
                if (spawns.Count > 0)
                {
                    ctx.Check(spawns[0] - IdleS < 2f * StepDt,
                        $"the held first launch fires on the crediting step (t=+{spawns[0] - IdleS:0.00} s)");
                }
                for (int i = 1; i < spawns.Count; i++)
                {
                    float gap = spawns[i] - spawns[i - 1];
                    ctx.Check(Math.Abs(gap - period) < 0.1f,
                        $"launch {i + 1} follows {gap:0.00} s after the last (ind {def.IndPeriod:0} + wave {def.WavePeriod:0})");
                }
                ctx.Check(doorPlays.Count > 0 && doorPlays[0] == def.OpenAnim,
                    $"the hangar opened with the authored '{def.OpenAnim}' plays=[{string.Join(",", doorPlays)}]");
            }
            finally
            {
                runtime.CallbackHost = savedHost;
                generators?.Free();
            }
        });
    }
}
