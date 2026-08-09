using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Utils;

/// <summary>
/// The session's randomness policy: one master seed, and a separate named generator per subsystem
/// derived from it. A subsystem's seed is <c>mix(master ^ hash(name))</c>, so the streams are
/// <b>independent of each other and of call order between them</b> — adding a draw to the weapons
/// code cannot shift what the liveries roll. Within one subsystem the sequence still depends on its
/// own call order, which the fixed <see cref="GameClock"/> makes reproducible.
///
/// <para>The master is pinned (1 by default) for a deterministic run and time-seeded otherwise, so
/// the shipped game keeps its variety: a bare <c>--fly</c> still gets a random spawn, random
/// liveries and a random gun-spread pattern. The resolved value is logged, so any interesting
/// unpinned run can be replayed with <c>--seed=</c>.</para>
///
/// <para>⚠ The hash is written here on purpose: <c>string.GetHashCode()</c> is randomized per
/// process in .NET, so deriving seeds from it would produce a different set of streams on every
/// launch — exactly the property this class exists to remove.</para>
/// </summary>
public static class Rng
{
    /// <summary>The master a pinned session uses when no <c>--seed=</c> is given.</summary>
    public const ulong DefaultSeed = 1;

    // Subsystem names. Constants rather than literals at the call sites: a typo would silently
    // open a second, differently-seeded stream instead of failing.
    public const string Weapons = "weapons";
    public const string FlightAudio = "flightaudio";
    public const string Spawn = "spawn";
    public const string Paint = "paint";
    public const string Anim = "anim";
    public const string Crash = "crash";
    public const string Effects = "effects";
    public const string Puffer = "puffer";
    public const string Clouds = "clouds";
    public const string Precip = "precip";

    private static readonly Dictionary<string, RandomNumberGenerator> Streams = new(StringComparer.Ordinal);

    /// <summary>The seed every subsystem stream derives from.</summary>
    public static ulong Master { get; private set; }

    /// <summary>True when the master was chosen rather than drawn from the clock — a deterministic
    /// run. Purely informational; nothing branches on it.</summary>
    public static bool Pinned { get; private set; }

    /// <summary>Re-derives every subsystem stream from <paramref name="master"/>. Called once per
    /// session build, before anything draws, so an in-process session restart (menu → fly → menu →
    /// fly) repeats the previous run exactly rather than continuing its sequence.</summary>
    public static void Reset(ulong master, bool pinned)
    {
        Master = master;
        Pinned = pinned;
        Streams.Clear();
        // The net for any draw this class does not own: Godot's global RNG behind GD.Randf/Randi.
        GD.Seed(master);
    }

    /// <summary>A master drawn from the clock — the unpinned default.</summary>
    public static ulong TimeSeed()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        return rng.Seed;
    }

    /// <summary>The named subsystem's seed. Pure: the same name and master always give the same
    /// value, whatever else has drawn.</summary>
    public static ulong SeedFor(string subsystem) => Mix(Master ^ Fnv1a(subsystem));

    /// <summary>The named subsystem's seed as a <see cref="System.Random"/> constructor takes it.</summary>
    public static int IntSeedFor(string subsystem)
    {
        ulong s = SeedFor(subsystem);
        return (int)(s ^ (s >> 32));
    }

    /// <summary>The named subsystem's shared generator, created on first use. Callers that draw
    /// from a hot path should hold the reference rather than re-resolving per draw.</summary>
    public static RandomNumberGenerator Stream(string subsystem)
    {
        if (!Streams.TryGetValue(subsystem, out var rng))
        {
            rng = new RandomNumberGenerator { Seed = SeedFor(subsystem) };
            Streams[subsystem] = rng;
        }
        return rng;
    }

    /// <summary>A fresh seed off the named subsystem's stream, for a per-instance generator (one
    /// puffer, one cloud field, one crash rig). Successive calls differ, and the whole series is a
    /// function of the master.</summary>
    public static int NewIntSeed(string subsystem) => (int)Stream(subsystem).Randi();

    /// <summary>A per-instance <see cref="System.Random"/> off the named subsystem's stream — the
    /// shape the effects code already uses.</summary>
    public static Random NewSystemRandom(string subsystem) => new(NewIntSeed(subsystem));

    /// <summary>A per-CELL <see cref="System.Random"/>, keyed by coordinate rather than draw
    /// order: the seed is a pure function of the subsystem name, the master seed and the two
    /// coordinates alone, so it does not depend on how many cells exist, what order they are
    /// built in, or on any other subsystem's draws. Use this instead of
    /// <see cref="NewSystemRandom(string)"/> when the set of things being drawn is itself a
    /// runtime computation over a coordinate space rather than a fixed walk over authored data —
    /// <c>FogVolumeClutter</c>'s map-edge continuation (A5,
    /// docs/plans/PLAN-overcast-match.md) is the first caller: the extension ring's cell count depends
    /// on each kind's authored <c>far_fade</c>, so a shared sequential stream would silently
    /// reroll every surviving cell's placement if that bound, or the enumeration order, ever
    /// changed.</summary>
    public static Random NewSystemRandom(string subsystem, int cellX, int cellZ)
    {
        ulong h = Mix(SeedFor(subsystem) ^ Fnv1a($"{cellX},{cellZ}"));
        return new Random((int)(h ^ (h >> 32)));
    }

    // FNV-1a over the name's UTF-16 code units, byte at a time.
    private static ulong Fnv1a(string s)
    {
        ulong h = 14695981039346656037UL;
        foreach (char c in s)
        {
            h = (h ^ (byte)c) * 1099511628211UL;
            h = (h ^ (byte)(c >> 8)) * 1099511628211UL;
        }
        return h;
    }

    // SplitMix64's finalizer: without it, masters 1 and 2 would hand every subsystem a pair of
    // seeds differing in one bit, and a weak generator can start those two streams close together.
    private static ulong Mix(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
