using System;

namespace CSVM.Session.Career;

/// <summary>
/// The career's randomness. Every draw is a pure function of the career's seed, a stream name and
/// integer keys such as a day or a slot. A board or a shelf therefore rolls the same whatever else
/// was drawn. Kept apart from <see cref="Utils.Rng"/>, whose master changes per sortie. A career's
/// seed is saved with the career and must outlive any session.
/// ⚠ Never key a draw on <see cref="string.GetHashCode"/>: .NET randomises it per process.
/// </summary>
public readonly struct CareerDice
{
    private readonly ulong _state;

    private CareerDice(ulong state)
    {
        _state = state;
    }

    /// <summary>The dice for one stream of one career, keyed by up to three integers.</summary>
    public static CareerDice For(ulong careerSeed, string stream, long a = 0, long b = 0, long c = 0)
    {
        ulong h = Mix(careerSeed ^ Fnv1a(stream));
        h = Mix(h ^ (ulong)a);
        h = Mix(h ^ (ulong)b);
        h = Mix(h ^ (ulong)c);
        return new CareerDice(h);
    }

    /// <summary>The <paramref name="index"/>th 64-bit draw of these dice.</summary>
    public ulong Next(int index) => Mix(_state + ((ulong)(index + 1) * 0x9E3779B97F4A7C15UL));

    /// <summary>The <paramref name="index"/>th draw as an integer in [min, max], both ends included.</summary>
    public int Range(int index, int min, int max)
    {
        if (max < min)
        {
            throw new ArgumentOutOfRangeException(nameof(max), $"range [{min}, {max}] is empty");
        }

        ulong span = (ulong)((long)max - min + 1);
        return (int)(min + (long)(Next(index) % span));
    }

    /// <summary>The <paramref name="index"/>th draw as a double in [0, 1).</summary>
    public double Unit(int index) => (Next(index) >> 11) * (1.0 / (1UL << 53));

    private static ulong Mix(ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    // FNV-1a over the name's UTF-16 code units, the same shape Utils.Rng uses.
    private static ulong Fnv1a(string s)
    {
        ulong h = 14695981039346656037UL;
        foreach (char ch in s)
        {
            h ^= ch;
            h *= 1099511628211UL;
        }

        return h;
    }
}
