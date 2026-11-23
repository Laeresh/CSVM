using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight.Modes;
using CSVM.Mech3;
using Godot;

namespace CSVM.Session.Career;

/// <summary>A point under a route where a ground target may go. Only x and z are known on the
/// ground; <paramref name="Altitude"/> is the route's own flying height there. The placer finds the
/// ground height and refuses water, slopes and clutter in the built world.</summary>
public readonly record struct GroundCandidate(float X, float Z, float Altitude, string Net);

/// <summary>Where a zeppelin starts in some mission of the chapter, and the net it flies.</summary>
public readonly record struct ZeppelinSite(Vector3 Position, float YawDeg, string Net);

/// <summary>One patrol route the generator can fly a patrol along: a fixed net, its points and
/// its edges, and its total edge length in metres.</summary>
public sealed record RouteSite(string Net, int NetId, IReadOnlyList<Vector3> Points, IReadOnlyList<(int A, int B)> Edges, float Length)
{
    /// <summary>The mean of the route's points, which area queries measure from.</summary>
    public Vector3 Centre => Points.Aggregate(Vector3.Zero, (sum, p) => sum + p) / Points.Count;
}

/// <summary>
/// The places on one chapter world a generated sortie can use: fixed patrol nets, ground
/// candidates under them, mission spawns and zeppelin starts. It is harvested at runtime from
/// data the remake already reads, and never committed. Terrain is not judged here. Sea, land and flat ground are checked
/// when the placer puts a target into the built world.
/// </summary>
public sealed class SiteCatalogue
{
    /// <summary>Ground candidates closer than this, in metres, collapse into one.</summary>
    public const float CandidateSpacing = 400f;

    private SiteCatalogue(IReadOnlyList<RouteSite> routes, IReadOnlyList<GroundCandidate> candidates, IReadOnlyList<SpawnPoint> spawns, IReadOnlyList<ZeppelinSite> zeppelins)
    {
        Routes = routes;
        GroundCandidates = candidates;
        Spawns = spawns;
        Zeppelins = zeppelins;
    }

    public IReadOnlyList<RouteSite> Routes { get; }

    public IReadOnlyList<GroundCandidate> GroundCandidates { get; }

    public IReadOnlyList<SpawnPoint> Spawns { get; }

    public IReadOnlyList<ZeppelinSite> Zeppelins { get; }

    /// <summary>
    /// The catalogue of already-read data. A net with a trailer is dropped: its points are offsets
    /// carried around a moving target, not places. A net with fewer than two points is no route.
    /// Ground candidates are the fixed nets' points, one per <see cref="CandidateSpacing"/> cell.
    /// </summary>
    public static SiteCatalogue Build(IEnumerable<AiNet> nets, IEnumerable<SpawnPoint> spawns, IEnumerable<ZeppelinSite> zeppelins)
    {
        var routes = new List<RouteSite>();
        var candidates = new List<GroundCandidate>();
        var cells = new HashSet<(int, int)>();
        foreach (var net in nets.OrderBy(n => n.Id))
        {
            if (net.Trailer != null || net.Nodes.Count < 2)
            {
                continue;
            }

            var points = net.Nodes.Select(n => n.Position).ToList();
            float length = net.Edges
                .Where(e => e.A >= 0 && e.B >= 0 && e.A < points.Count && e.B < points.Count)
                .Sum(e => points[e.A].DistanceTo(points[e.B]));
            routes.Add(new RouteSite(net.Name, net.Id, points, net.Edges, length));
            foreach (var p in points)
            {
                var cell = ((int)MathF.Floor(p.X / CandidateSpacing), (int)MathF.Floor(p.Z / CandidateSpacing));
                if (cells.Add(cell))
                {
                    candidates.Add(new GroundCandidate(p.X, p.Z, p.Y, net.Name));
                }
            }
        }

        return new SiteCatalogue(routes, candidates, spawns.ToList(), zeppelins.ToList());
    }

    /// <summary>Harvests a chapter: its nets from <paramref name="chapter"/>, and from each
    /// mission scope its player start and its zeppelins. A mission scope missing a file adds
    /// nothing rather than failing the catalogue.</summary>
    public static SiteCatalogue Harvest(ZrdrSet chapter, IEnumerable<ZrdrSet> missions)
    {
        var spawns = new List<SpawnPoint>();
        var zeppelins = new List<ZeppelinSite>();
        foreach (var mission in missions)
        {
            if (SpawnPoints.LoadPlayerInit(mission) is { } start)
            {
                spawns.Add(start.Spawn);
            }

            try
            {
                zeppelins.AddRange(Mech3.Zeppelins.Load(mission).Select(z => new ZeppelinSite(z.Position, z.YawDeg, z.Net)));
            }
            catch (IOException)
            {
                // A mission scope without a zeppelins file has none to offer.
            }
        }

        return Build(AiNets.Load(chapter), spawns, zeppelins);
    }

    /// <summary>The routes whose centre lies within <paramref name="radius"/> metres of
    /// <paramref name="centre"/> on the ground plane.</summary>
    public IEnumerable<RouteSite> RoutesNear(Vector3 centre, float radius) =>
        Routes.Where(r => Flat(r.Centre, centre) <= radius * radius);

    /// <summary>The ground candidates within <paramref name="radius"/> metres of
    /// <paramref name="centre"/> on the ground plane.</summary>
    public IEnumerable<GroundCandidate> CandidatesNear(Vector3 centre, float radius) =>
        GroundCandidates.Where(c => Flat(new Vector3(c.X, 0, c.Z), centre) <= radius * radius);

    private static float Flat(Vector3 a, Vector3 b) => ((a.X - b.X) * (a.X - b.X)) + ((a.Z - b.Z) * (a.Z - b.Z));
}
