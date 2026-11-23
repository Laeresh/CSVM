using System.IO;
using System.Linq;
using CSVM.Flight.Modes;
using CSVM.Mech3;
using CSVM.Session.Career;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>The site catalogue over the committed reader fixtures. Fixed nets become routes and
/// ground candidates, and a net carried by a trailer is dropped. A mission scope adds its player
/// start and its zeppelins, and a missing file adds nothing.</summary>
public class SiteCatalogueTests
{
    [Fact]
    public void Build_KeepsOnlyFixedNetsAsRoutes()
    {
        var catalogue = SiteCatalogue.Build(AiNets.Load(TestZrdr.Fixture("ainets")), new SpawnPoint[0], new ZeppelinSite[0]);

        // Nets 5, 11 and 13 carry a trailer (player, index-only, a zeppelin); 7 and 9 are fixed.
        Assert.Equal(new[] { 7, 9 }, catalogue.Routes.Select(r => r.NetId).ToArray());
        Assert.Equal("TestTrailerless", catalogue.Routes[0].Net);
        Assert.Equal(28.28f, catalogue.Routes[0].Length, 2);
        Assert.Equal(150f, catalogue.Routes[1].Length, 3);
    }

    [Fact]
    public void Build_CollapsesGroundCandidatesPerCell()
    {
        var catalogue = SiteCatalogue.Build(AiNets.Load(TestZrdr.Fixture("ainets")), new SpawnPoint[0], new ZeppelinSite[0]);

        // Every fixed-net point lies in the 400 m cell at the origin, so one candidate stands.
        var candidate = Assert.Single(catalogue.GroundCandidates);
        Assert.Equal(new GroundCandidate(10f, 20f, 250f, "TestTrailerless"), candidate);
    }

    [Fact]
    public void Queries_MeasureOnTheGroundPlane()
    {
        var catalogue = SiteCatalogue.Build(AiNets.Load(TestZrdr.Fixture("ainets")), new SpawnPoint[0], new ZeppelinSite[0]);

        // Height is ignored: a centre 5 km up still finds both routes within 200 m.
        Assert.Equal(2, catalogue.RoutesNear(new Vector3(30f, 5000f, 20f), 200f).Count());
        Assert.Empty(catalogue.RoutesNear(new Vector3(3000f, 0f, 0f), 200f));
        Assert.Single(catalogue.CandidatesNear(new Vector3(0f, 0f, 0f), 50f));
        Assert.Empty(catalogue.CandidatesNear(new Vector3(0f, 0f, 0f), 10f));
    }

    [Fact]
    public void Harvest_AddsMissionStartsAndZeppelins()
    {
        string mission = TestData.TempDir();
        File.WriteAllText(
            Path.Combine(mission, "objectives.zrd.json"),
            "[[\"PLAYER_INIT\", [0, [1000.0, 300.0, -2000.0], [0.0, 90.0, 0.0], 0.8, 300.0]]]");

        var catalogue = SiteCatalogue.Harvest(
            TestZrdr.Fixture("ainets"),
            new[] { TestZrdr.Folder(mission), TestZrdr.Fixture("zeppelins") });

        var spawn = Assert.Single(catalogue.Spawns);
        Assert.Equal(new Vector3(1000f, 300f, -2000f), spawn.Position);
        Assert.Equal(90f, spawn.HeadingDeg);
        Assert.Equal(2, catalogue.Zeppelins.Count);
        Assert.Equal(new Vector3(-100f, 450f, 200f), catalogue.Zeppelins[0].Position);
        Assert.Equal(2, catalogue.Routes.Count);
    }

    [Fact]
    public void Harvest_OfEmptyScopesIsEmpty()
    {
        var catalogue = SiteCatalogue.Harvest(TestZrdr.Folder(TestData.TempDir()), new[] { TestZrdr.Folder(TestData.TempDir()) });

        Assert.Empty(catalogue.Routes);
        Assert.Empty(catalogue.GroundCandidates);
        Assert.Empty(catalogue.Spawns);
        Assert.Empty(catalogue.Zeppelins);
    }
}
