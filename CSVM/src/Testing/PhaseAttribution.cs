using System;
using System.Collections.Generic;

namespace CSVM.Testing;

/// <summary>
/// Buckets a <see cref="Utils.StartupProfile"/>'s raw phase names (<c>gamez</c>, <c>textures</c>,
/// <c>sounds</c>, <c>zrdr</c>, <c>world</c>, <c>clutter</c>, <c>anim</c>, <c>bind</c>,
/// <c>prewarm</c> — <c>WorldSession.Build</c>/<c>SessionArchives.OpenFor</c>) into the three
/// categories a build-cost decision is made from: archive/decode, sound preparation, and
/// runtime/world construction. Godot-free and pure, so the categorization and its closure
/// arithmetic are unit-testable without the engine (<c>TestHarness</c> is where it is actually fed
/// a live profile). ⚠ A phase name absent from every category below lands in
/// <see cref="Categorized.OtherMs"/> rather than being silently dropped — the same rule
/// <c>StartupProfile.Format</c>'s own <c>rest</c> follows, so a future phase never vanishes from the
/// closure it is trying to prove.
/// </summary>
public static class PhaseAttribution
{
    // gamez/textures: the chapter's own archives. anim: AnimProgram.Load, the compiled cam_anim/
    // mis_anim + zrdr merge. zrdr: MissionSetup.Load (WorldSession.Build) and the sound-def/group
    // load (SessionArchives.OpenFor) share this name by design (StartupProfile accumulates same-
    // name calls) — both are decode, so the shared bucket is correct either way.
    private static readonly HashSet<string> ArchiveDecodePhases =
        new(StringComparer.Ordinal) { "gamez", "textures", "anim", "zrdr" };

    // sounds: opening the sound archive. prewarm: decoding every SOUND_NODE/one-shot/voice clip
    // the program can reach before the archive closes, which is what a mute A/B measures.
    private static readonly HashSet<string> SoundPrepPhases =
        new(StringComparer.Ordinal) { "sounds", "prewarm" };

    // world: WorldBuilder.Build, the scene geometry. clutter: ClutterBuilder over that scene.
    // bind: AnimRuntime.Bind, wiring the compiled program onto the built tree.
    private static readonly HashSet<string> RuntimeConstructionPhases =
        new(StringComparer.Ordinal) { "world", "clutter", "bind" };

    /// <summary>Sorts <paramref name="phases"/> into the three named categories; whatever they do
    /// not cover against <paramref name="buildMs"/> lands in <see cref="Categorized.OtherMs"/>.
    /// Pass the caller's own wall-clock build time, not a profile-internal figure, so
    /// <c>Categorized.BuildMs</c> always equals what the caller attributed to the build.</summary>
    public static Categorized Categorize(IReadOnlyDictionary<string, double> phases, double buildMs)
    {
        double archive = 0, sound = 0, runtime = 0, known = 0;
        foreach (var (name, ms) in phases)
        {
            if (ArchiveDecodePhases.Contains(name))
            {
                archive += ms;
                known += ms;
            }
            else if (SoundPrepPhases.Contains(name))
            {
                sound += ms;
                known += ms;
            }
            else if (RuntimeConstructionPhases.Contains(name))
            {
                runtime += ms;
                known += ms;
            }
            // An unrecognised phase name is left out of `known` on purpose: its own recorded time
            // then falls out through `other` below, exactly like an uninstrumented gap, rather
            // than vanishing (it was still spent inside buildMs; nothing here invents new time).
        }
        double other = Math.Max(0, buildMs - known);
        return new Categorized(archive, sound, runtime, other);
    }

    /// <summary>The suite-body time that is neither a world build nor disposing one: manual
    /// simulation plus assertion work. Never negative — the three are independent stopwatches over
    /// the same wall-clock span, so a tiny overrun is stopwatch/GC noise, not a real deficit, and is
    /// folded into <c>rest</c> rather than reported as one.</summary>
    public static double Rest(double suiteWallSeconds, double buildSeconds, double disposalSeconds) =>
        Math.Max(0, suiteWallSeconds - buildSeconds - disposalSeconds);

    /// <summary>How far <c>buildSeconds + disposalSeconds</c> overran the suite's own wall time —
    /// zero when the three stopwatches close cleanly. Report this, not just the clamped
    /// <see cref="Rest"/>, or an overrun reads as a healthy zero instead of a measurement defect.</summary>
    public static double Overrun(double suiteWallSeconds, double buildSeconds, double disposalSeconds) =>
        Math.Max(0, buildSeconds + disposalSeconds - suiteWallSeconds);

    /// <summary>One build's phase time, bucketed. <c>ArchiveDecodeMs + SoundPrepMs +
    /// RuntimeConstructionMs + OtherMs == BuildMs</c> exactly — see <see cref="Categorize"/>.</summary>
    public readonly record struct Categorized(
        double ArchiveDecodeMs, double SoundPrepMs, double RuntimeConstructionMs, double OtherMs)
    {
        public double BuildMs => ArchiveDecodeMs + SoundPrepMs + RuntimeConstructionMs + OtherMs;

        public static Categorized operator +(Categorized a, Categorized b) => new(
            a.ArchiveDecodeMs + b.ArchiveDecodeMs,
            a.SoundPrepMs + b.SoundPrepMs,
            a.RuntimeConstructionMs + b.RuntimeConstructionMs,
            a.OtherMs + b.OtherMs);
    }
}
