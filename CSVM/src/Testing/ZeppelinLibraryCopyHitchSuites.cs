using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CSVM.Mech3;
using Godot;
using Clock = CSVM.Testing.AiWaveLaunchHitchSuites.ThreadCpuClock;

namespace CSVM.Testing;

/// <summary>What a zeppelin's first fire and falling-engine effects cost the frame they land on,
/// over C4/M05's built world. Each is a library-root callee, built and indexed on its first call
/// mid-flight. That build is timed with the name lookups that follow it. A cleared find cache
/// makes each of those lookups pay a whole-table scan.</summary>
internal static class ZeppelinLibraryCopyHitchSuites
{
    private const string Chapter = "C4";
    private const string Mission = "M05";
    private const string Zep = "cargozep3";

    // Thread-CPU regression bars. Readings: docs/verification.md PERF-37.
    private const double CopyBarMs = 40.0;
    private const double LookupBarMs = 5.0;

    // How many of the program's definition names are asked again after each copy.
    private const int LookupCount = 200;

    // The callees a gasbag loss and the engine losses after it build first, in the order they land.
    private static readonly string[] Callees =
    {
        "zep_skin_fire1", "partial_damage", "cgleng_destroyed.flt", "zep_eng_boom",
        "cgreng_destroyed.flt", "zep_skin_fire4",
    };

    [Suite("zeppelin-library-copy-hitch",
        "what a zeppelin's first fire and falling-engine effect costs the frame it lands on: over "
        + "C4/M05's built world each library-root callee a gasbag or engine loss builds is made "
        + "through the session's own lazy builder, on the stepping thread's CPU clock. Then two "
        + "hundred already-answered definition names are asked again. Each copy and each re-ask is held "
        + "under a regression bar, so a copy that rescans the node table per definition, or that "
        + "voids every memoized lookup, fails here")]
    internal static void ZeppelinLibraryCopyHitch(TestContext ctx)
    {
        var report = new StringBuilder();
        ctx.WithWorld(Chapter, collision: false, Mission, world => Drive(ctx, world, report));
        ctx.WriteArtifact($"test-zeppelin-library-copy-hitch-{Chapter}-{Mission}.txt", report.ToString());
    }

    private static void Drive(TestContext ctx, TestWorld world, StringBuilder report)
    {
        var runtime = world.Runtime;
        var build = runtime.ResolveLibraryRoot;
        var anchor = runtime.FindNodes(Zep).FirstOrDefault();
        ctx.Check(build != null && anchor != null,
            $"the {Chapter}/{Mission} world has a lazy library builder and '{Zep}' resolves");
        if (build == null || anchor == null)
        {
            return;
        }

        var names = world.Session.Program.Defs
            .Select(d => d.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(LookupCount)
            .ToList();
        foreach (string name in names)
        {
            runtime.FindNodes(name);
        }

        double worstCopy = 0.0, worstLookup = 0.0;
        foreach (string callee in Callees)
        {
            long mark = Clock.Now();
            var copy = build(callee, anchor, new object());
            double copyMs = Clock.Ms(Clock.Now() - mark);
            ctx.Check(copy != null, $"library root '{callee}' builds a copy");

            mark = Clock.Now();
            foreach (string name in names)
            {
                runtime.FindNodes(name);
            }
            double lookupMs = Clock.Ms(Clock.Now() - mark);
            worstCopy = Math.Max(worstCopy, copyMs);
            worstLookup = Math.Max(worstLookup, lookupMs);
            report.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{callee}: copy {copyMs:0.0} ms, {names.Count} lookups after it {lookupMs:0.0} ms"));
        }

        ctx.Check(worstCopy < CopyBarMs,
            $"the worst library copy holds its bar worst={worstCopy:0.0} ms bar={CopyBarMs:0} ms");
        ctx.Check(worstLookup < LookupBarMs,
            $"the lookups after a copy hold their bar worst={worstLookup:0.0} ms bar={LookupBarMs:0} ms over {names.Count} names");
        ctx.Note($"on-thread CPU over {Callees.Length} callees: worst copy {worstCopy:0.0} ms, worst {names.Count}-name re-ask {worstLookup:0.0} ms, bars {CopyBarMs:0}/{LookupBarMs:0} ms, clock {Clock.Source}");
    }
}
