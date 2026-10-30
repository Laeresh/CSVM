using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The remake's added stamps on a picked ground texture: one scatter per texture repeat. It walks the
/// triangle's own UV lattice as the original walks a template, with the strict edge test and the dedup.
/// The scatter is a fixed order of points on the unit repeat. Each is the farthest of <see cref="Tries"/>
/// draws from those before it, so any prefix is spread. A triangle takes twice the stamps its set's density
/// gives one repeat's ground, and a neutral mask keeps half (<see cref="VegetationMask.Keeps"/>).
/// ⚠ Never add a world grid: a candidate is a texture coordinate, so it moves with the painted ground.
/// </summary>
public static class VegetationScatter
{
    /// <summary>Draws one added stamp takes on the added stream whatever its values: the variation and the fade.</summary>
    public const int DrawsPerStamp = VegetationVariation.DrawsPerStamp + 1;

    /// <summary>Candidates one repeat may take before the triangle is refused and counted. It is a tripwire
    /// for a stretched UV map: 100 a hectare over C1's 256 m repeat is 1,311.</summary>
    public const int MaxPerRepeat = 4096;

    /// <summary>Draws per scatter point beyond its member draw; the farthest from the points before it wins.</summary>
    public const int Tries = 8;

    // The added stream's and the scatter's labels beside the Rich seed, so neither replays the other or the Rich stream.
    private const int AddedStream = 0x51A7C0DE;
    private const int ScatterStream = 0x3D2B8F11;

    private const double HectareM2 = 10000.0;

    /// <summary>The set a picked texture stamps, null when it stamps nothing: no pick, the reserved
    /// <see cref="CanopyMask.CanopySet"/> (painted forest takes no card), no member or no density.</summary>
    public static VegetationSet? Stamped(VegetationEntry entry, string texture) =>
        entry.SetFor(texture) is { Models.Count: > 0, Density: > 0f } set && !CanopyMask.IsCanopy(entry, texture) ? set : null;

    /// <summary>The added stream's seed from the Rich stream's (<see cref="VegetationVariation.Seed"/>).</summary>
    public static int StreamSeed(int richSeed) => richSeed ^ AddedStream;

    /// <summary>A set's scatter seed from the Rich stream's and its name, so an edit to one set moves no other.
    /// FNV-1a over the lower-case name, never <see cref="string.GetHashCode()"/>, which changes per process.</summary>
    public static int ScatterSeed(int richSeed, string set)
    {
        uint hash = 2166136261;
        foreach (char ch in set.ToLowerInvariant())
            hash = (hash ^ ch) * 16777619;
        return unchecked(richSeed ^ ScatterStream ^ (int)hash);
    }

    /// <summary>Candidates a repeat takes on <paramref name="tri"/> for a set of <paramref name="density"/> a hectare:
    /// twice the stamps its ground holds, since a neutral mask keeps half. Fractional; the last candidate is weighted.</summary>
    public static double PerRepeat(float density, ClutterBuilder.UvTriangle tri) => 2.0 * density * tri.RepeatArea / HectareM2;

    /// <summary>Every candidate <paramref name="tri"/> holds, in lattice order. Each integer repeat its UVs span
    /// takes the first <paramref name="perRepeat"/> of <paramref name="points"/>, a set's <see cref="Scatter"/>.
    /// A point stands where the strict edge test holds it. The dedup set refuses one within a quarter metre of a
    /// stamp of its member's key, and the count of those comes back.</summary>
    public static List<Candidate> OnTriangle(ClutterBuilder.UvTriangle tri, IReadOnlyList<Point> points, double perRepeat,
        IReadOnlyList<int> keys, HashSet<(int, int, int)> seen, ref int deduped)
    {
        var found = new List<Candidate>();
        int take = Math.Min((int)Math.Ceiling(perRepeat), points.Count);
        for (int uInt = tri.MinU; uInt <= tri.MaxU; uInt++)
        {
            for (int vInt = tri.MinV; vInt <= tri.MaxV; vInt++)
            {
                for (int j = 0; j < take; j++)
                {
                    double cu = uInt + points[j].U, cv = vInt + points[j].V;
                    if (!tri.Contains(cu, cv))
                        continue;
                    int member = points[j].Member(keys.Count);
                    var p = tri.World(cu, cv);
                    if (!seen.Add((keys[member], Mathf.RoundToInt(p.X * 4f), Mathf.RoundToInt(p.Z * 4f))))
                    {
                        deduped++;
                        continue;
                    }
                    found.Add(new Candidate(cu, cv, member, (float)Math.Min(1.0, perRepeat - j)));
                }
            }
        }

        return found;
    }

    /// <summary>Whether the brush mask keeps a candidate: <paramref name="density"/> is the mask there, 0 clears,
    /// 1 keeps half and 2 nearly all, and <paramref name="draw"/> the stamp's keep draw.</summary>
    public static bool Keeps(Candidate candidate, float density, double draw) =>
        VegetationMask.Keeps(density * candidate.Weight, draw, added: true);

    /// <summary>One candidate: its texture coordinate, its member's index in the set and its weight, below 1
    /// for the fractional last of a repeat.</summary>
    public readonly record struct Candidate(double U, double V, int Member, float Weight);

    /// <summary>One point of a scatter on the unit repeat, with the draw its member is read from.</summary>
    public readonly record struct Point(double U, double V, double Pick)
    {
        /// <summary>The member among <paramref name="members"/> this point stamps. A count edit moves no point.</summary>
        public int Member(int members) => Math.Min((int)(Pick * members), members - 1);
    }

    /// <summary>A set's points on the unit repeat in their fixed order, grown on demand. Each takes
    /// <see cref="Tries"/> position draws and a member draw, so a prefix never depends on how far it grew.</summary>
    public sealed class Scatter
    {
        private readonly Random _rng;
        private readonly List<Point> _points = new();

        public Scatter(int seed)
        {
            _rng = new Random(seed);
        }

        /// <summary>The first <paramref name="count"/> points.</summary>
        public IReadOnlyList<Point> Take(int count)
        {
            while (_points.Count < count)
                _points.Add(Next());
            return _points;
        }

        // The farthest of the tries from the points before it, measured across the repeat's wrap, since it tiles.
        private Point Next()
        {
            double bestU = 0, bestV = 0, bestD = -1;
            for (int t = 0; t < Tries; t++)
            {
                double u = _rng.NextDouble(), v = _rng.NextDouble();
                double d = double.MaxValue;
                foreach (var p in _points)
                {
                    double du = Math.Abs(u - p.U), dv = Math.Abs(v - p.V);
                    du = Math.Min(du, 1.0 - du);
                    dv = Math.Min(dv, 1.0 - dv);
                    d = Math.Min(d, (du * du) + (dv * dv));
                }
                if (d > bestD)
                {
                    bestD = d;
                    bestU = u;
                    bestV = v;
                }
            }

            return new Point(bestU, bestV, _rng.NextDouble());
        }
    }
}
