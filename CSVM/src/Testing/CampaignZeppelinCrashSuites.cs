using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>C3/M01's cargo zeppelin goes down when its hydrogen tanks blow. The chain is authored
/// in animations alone: the tank's death burns four gasbag halves and calls
/// <c>cargozep1_crash</c> 35 s on, and each burn ends in a <c>finish_*</c> that switches its
/// gasbag's <c>panels</c> node off. That node is what the zeppelin's survivor walk reads, so the
/// hull dies without a single point of damage on its own pools, and a dead hull is off its net.
/// Drives the mission's own records, nets and definitions over its BUILT world.</summary>
internal static class CampaignZeppelinCrashSuites
{
    private const string Chapter = "C3";
    private const string Mission = "M01";
    private const string Hull = "cargozep1";
    private const string TankDeath = "hydrobombboom1";
    private const string Crash = "cargozep1_crash";
    private const float Tick = 1f / 30f;

    // The tank's call of the crash sits 35 s after 2.1 s of chained offsets; the burns' own
    // finish lands at 36 s. This outlasts both, the fall from the record's 300 m, and a stretch
    // of the wreck standing still afterwards.
    private const float RunSeconds = 90f;

    // A hull the net still flew would cover this in a few seconds at its 20 m/s max_speed; a
    // wreck under floatdown alone moves along no horizontal axis at all.
    private const float DriftCapM = 15f;

    [Suite("campaign-zeppelin-crash",
        "C3/M01's cargo zeppelin crashes when its hydrogen tanks blow: the tank's death starts, "
        + "the four gasbag burns switch panels off and the survivor walk kills the hull with its "
        + "own pools untouched, cargozep1_crash starts from the tank's own call, and from the "
        + "crash on the hull only sinks, it never moves off along its net again")]
    internal static void CampaignZeppelinCrash(TestContext ctx)
    {
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, Chapter, Mission);
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, Chapter);
        ctx.RequireData(missionZrdr, $"{Chapter}/{Mission} zrdr");
        ctx.RequireData(chapterZrdr, $"{Chapter} zrdr");

        var report = new StringBuilder();
        ctx.WithWorld(Chapter, collision: true, mission: Mission, world =>
        {
            var runtime = world.Runtime;
            uint maskWas = runtime.ContactMask;
            ZeppelinRuntime? zeps = null;
            try
            {
                // A suite world wires no contact mask, and the crash's altitude gate is a probe.
                runtime.ContactMask = CollisionLayers.World;
                runtime.SurfaceIsWater = body => ProjectilePool.SurfaceIsWater(body as Node);
                zeps = new ZeppelinRuntime(Zeppelins.Load(missionZrdr),
                    name => runtime.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null,
                    AiNets.Load(chapterZrdr), null, runtime.Motions.DrivesTransform);
                ctx.Host.AddChild(zeps);
                zeps.WireDamage(runtime);
                Run(ctx, runtime, zeps, report);
            }
            finally
            {
                runtime.ContactMask = maskWas;
                runtime.SurfaceIsWater = null;
                zeps?.Free();
            }
        });

        ctx.WriteArtifact("test-campaign-zeppelin-crash.txt", report.ToString());
    }

    private static void Run(TestContext ctx, AnimRuntime runtime, ZeppelinRuntime zeps,
        StringBuilder report)
    {
        var host = runtime.FindNodes(Hull).FirstOrDefault();
        var tank = runtime.Destructibles.All.FirstOrDefault(i =>
            string.Equals(i.Def.AnimName, TankDeath, StringComparison.OrdinalIgnoreCase));
        ctx.Check(host != null, $"'{Hull}' resolves in the {Chapter}/{Mission} world");
        ctx.Check(tank != null, $"the tank's death '{TankDeath}' registers a damage pool");
        if (host == null || tank == null)
        {
            return;
        }

        ctx.Check(zeps.Wake(Hull), $"'{Hull}' wakes, as OBJECTIVE39 wakes it in the mission");
        var bagPools = runtime.Destructibles.All
            .Where(i => i.Gasbag && string.Equals(i.Owner, Hull, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Cruise first, so a hull the net drives is seen moving before anything happens to it.
        for (int i = 0; i < 90; i++)
        {
            runtime.Advance(Tick);
            zeps.SimStep(Tick);
        }
        var cruiseFrom = host.GlobalPosition;
        for (int i = 0; i < 90; i++)
        {
            runtime.Advance(Tick);
            zeps.SimStep(Tick);
        }
        float cruise = Flat(host.GlobalPosition - cruiseFrom);
        report.AppendLine($"cruise: {cruise:0.0} m in 3 s before the tanks blow");
        ctx.Check(cruise > 1f, $"the net flies the hull before the tanks blow ({cruise:0.0} m in 3 s)");

        var fired = new List<string>();
        runtime.OnEventDispatched = d =>
        {
            if (d.Def.AnimName is Crash)
            {
                fired.Add($"{d.Sequence}:{d.EventKind}({d.EventName})");
            }
        };

        ctx.Check(runtime.DamageAt(tank.DamageNode, tank.MaxHealth + 1f),
            $"a lethal hit lands on the tank");
        float t = 0f, crashAt = -1f, deadAt = -1f;
        Vector3 crashPos = default;
        float maxDrift = 0f, lowest = host.GlobalPosition.Y;
        bool everBoom = false;
        for (int i = 0; i < (int)(RunSeconds / Tick); i++)
        {
            runtime.Advance(Tick);
            zeps.SimStep(Tick);
            t += Tick;
            everBoom |= runtime.AnimStateOf(TankDeath) is 2 or 3 or 4;
            if (deadAt < 0f && zeps.IsDead(Hull))
            {
                deadAt = t;
            }
            if (crashAt < 0f && runtime.AnimStateOf(Crash) == 2)
            {
                crashAt = t;
                crashPos = host.GlobalPosition;
            }
            if (crashAt >= 0f)
            {
                maxDrift = Mathf.Max(maxDrift, Flat(host.GlobalPosition - crashPos));
                lowest = Mathf.Min(lowest, host.GlobalPosition.Y);
            }
            if (i % 150 == 0)
            {
                var p = host.GlobalPosition;
                report.AppendLine($"t={t:0.0} hull ({p.X:0},{p.Y:0},{p.Z:0}) dead={zeps.IsDead(Hull)} " +
                    $"survivors={zeps.SurvivorsOf(Hull)} crash={runtime.AnimStateOf(Crash)}");
            }
        }
        runtime.OnEventDispatched = null;

        int poolsDestroyed = bagPools.Count(i => i.Status == DestructibleRegistry.State.Destroyed);
        report.AppendLine($"tank death ran={everBoom}; hull dead at {deadAt:0.0} s; crash started at " +
            $"{crashAt:0.0} s from ({crashPos.X:0},{crashPos.Y:0},{crashPos.Z:0}); max horizontal drift " +
            $"after it {maxDrift:0.0} m; lowest y {lowest:0}; gasbag pools destroyed {poolsDestroyed} of {bagPools.Count}");
        foreach (string line in fired.Distinct())
        {
            report.AppendLine($"{Crash} {line}");
        }

        ctx.Check(everBoom, $"the hit starts the tank's own death '{TankDeath}'");
        ctx.Check(deadAt > 0f,
            $"the burns' finish_* switch gasbag panels off and the survivor walk kills '{Hull}' at {deadAt:0.0} s");
        ctx.Same(0, poolsDestroyed,
            $"…with no gasbag pool of its own destroyed, so the kill is the node flag and not damage");
        ctx.Check(crashAt > 0f, $"the tank's delayed call starts '{Crash}' at {crashAt:0.0} s");
        ctx.Check(fired.Contains("floatdown:ObjectMotion(cargozep1)"),
            $"…whose floatdown takes the hull's own transform");
        ctx.Check(lowest < crashPos.Y - 50f,
            $"the wreck sinks under floatdown ({crashPos.Y:0} -> {lowest:0} m)");
        ctx.Check(maxDrift < DriftCapM,
            $"and never moves off along its net again: {maxDrift:0.0} m horizontal from the crash start, cap {DriftCapM:0}");
        ctx.Note($"'{Hull}' dead at {deadAt:0.0} s, crash at {crashAt:0.0} s, drift {maxDrift:0.0} m, sank to y={lowest:0}");
    }

    private static float Flat(Vector3 v) => new Vector2(v.X, v.Z).Length();
}
