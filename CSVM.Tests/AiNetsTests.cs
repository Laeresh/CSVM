using System.Collections.Generic;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The patrol-net reader (docs/formats/ai-nets.md): fixture units for the record grammar and
/// the four trailer shapes, plus golden counts measured over the install — asserted so a
/// reader or extraction change moves a test instead of silently drifting.
/// </summary>
public class AiNetsTests
{
    private static readonly string[] Chapters = { "C1", "C1B", "C1C", "C2", "C2B", "C3", "C4", "C5" };

    /// <summary>Net files per chapter — measured census of the install.</summary>
    public static TheoryData<string, int> ChapterNetCounts => new()
    {
        { "C1", 29 },
        { "C1B", 22 },
        { "C1C", 20 },
        { "C2", 34 },
        { "C2B", 18 },
        { "C3", 30 },
        { "C4", 29 },
        { "C5", 40 },
    };

    [Fact]
    public void LoadsEveryFixtureNetSortedByIdWithItsIndexName()
    {
        var nets = Fixtures();
        Assert.Equal(new[] { 5, 7, 9, 11, 13 }, nets.ConvertAll(n => n.Id));
        Assert.Equal(
            new[] { "TestLoop", "TestTrailerless", "TestNoTarget", "TestIndexOnly", "TestTargetNoNode" },
            nets.ConvertAll(n => n.Name));
    }

    [Fact]
    public void NodesCarryPositionsAndRawTagsVerbatim()
    {
        var loop = Fixtures()[0];
        Assert.Equal(3, loop.Nodes.Count);
        Assert.Equal(new Godot.Vector3(-100f, 400f, -200f), loop.Nodes[0].Position);
        Assert.Empty(loop.Nodes[0].Tags);
        Assert.Equal(new[] { 2f, 6f }, loop.Nodes[1].Tags);

        // The 4-tag variant (12 nodes in the install carry it).
        var noTarget = Fixtures()[2];
        Assert.Equal(new[] { 1f, 2f, 3f, 4f }, noTarget.Nodes[0].Tags);
    }

    [Fact]
    public void EdgesAreTheExplicitPairListNeverAnAssumedSequence()
    {
        var nets = Fixtures();
        // TestLoop closes 0-1-2-0; TestNoTarget branches at node 1. Both arrive verbatim.
        Assert.Equal(new[] { (0, 1), (1, 2), (0, 2) }, nets[0].Edges);
        Assert.Equal(new[] { (0, 1), (1, 2), (1, 3) }, nets[2].Edges);
    }

    [Fact]
    public void TheFiveTrailerShapesParseAsDocumented()
    {
        var nets = Fixtures();
        Assert.Equal(new AiNetTrailer(1, "player"), nets[0].Trailer); // [nodeIndex, "name"]
        Assert.Null(nets[1].Trailer);                                 // 13-element record, omitted
        Assert.Null(nets[2].Trailer);                                 // bare [-1]
        Assert.Equal(new AiNetTrailer(1, null), nets[3].Trailer);     // index-only (C2 net 33)
        Assert.Equal(new AiNetTrailer(-1, "testzep"), nets[4].Trailer); // [-1, "name"] — target, no node
    }

    [Fact]
    public void TheIndexFirstElementIsNotThePairCount()
    {
        // The fixture index opens with 9 over 5 pairs, mirroring the install (C1: 46 over 29).
        // An implementation that trusts it as a count truncates or over-reads the list.
        var names = AiNets.LoadIndex(TestData.Fixture("ainets"));
        Assert.Equal(5, names.Count);
        Assert.Equal("TestIndexOnly", names[11]);
    }

    // ---- Goldens over the install ---------------------------------------------------------------

    [ExtractedDataTheory]
    [MemberData(nameof(ChapterNetCounts))]
    public void EachChapterShipsItsNetsAndEveryOneResolvesAnIndexName(string chapter, int expected)
    {
        var nets = AiNets.Load(SessionPaths.ChapterZrdr(TestData.DataRoot!, chapter));
        Assert.Equal(expected, nets.Count);
        // The 1:1 both ways: every file has a name, every index id a file.
        Assert.Equal(expected, AiNets.LoadIndex(SessionPaths.ChapterZrdr(TestData.DataRoot!, chapter)).Count);
        foreach (var net in nets)
        {
            Assert.NotEqual("", net.Name);
        }
    }

    [ExtractedDataFact]
    public void TheInstallWideStructureMatchesTheScopingCensus()
    {
        int nets = 0, nodes = 0, edges = 0, tagged2 = 0, tagged4 = 0;
        int anchoredTrailers = 0, unanchoredTrailers = 0, indexOnlyTrailers = 0;
        foreach (var chapter in Chapters)
        {
            foreach (var net in AiNets.Load(SessionPaths.ChapterZrdr(TestData.DataRoot!, chapter)))
            {
                nets++;
                nodes += net.Nodes.Count;
                edges += net.Edges.Count;
                foreach (var node in net.Nodes)
                {
                    if (node.Tags.Count == 2) tagged2++;
                    if (node.Tags.Count == 4) tagged4++;
                }
                foreach (var (a, b) in net.Edges)
                {
                    Assert.InRange(a, 0, net.Nodes.Count - 1);
                    Assert.InRange(b, 0, net.Nodes.Count - 1);
                }
                if (net.Trailer is { } t)
                {
                    if (t.Name == null) indexOnlyTrailers++;
                    else if (t.NodeIndex >= 0) anchoredTrailers++;
                    else unanchoredTrailers++;
                    Assert.InRange(t.NodeIndex, -1, net.Nodes.Count - 1);
                }
            }
        }
        Assert.Equal(222, nets);
        Assert.Equal(2268, nodes);
        Assert.Equal(2149, edges);
        Assert.Equal(81, tagged2);
        Assert.Equal(12, tagged4);
        Assert.Equal(76, anchoredTrailers);   // [nodeIndex, "name"]
        Assert.Equal(4, unanchoredTrailers);  // [-1, "name"] — the piratezep/player/dantezep four
        Assert.Equal(1, indexOnlyTrailers);   // C2 net 33's [3] — the shape the scoping doc missed
    }

    [ExtractedDataFact]
    public void TheWorkedExampleNetReadsExactlyAsTheScopingDocMeasuredIt()
    {
        // C1 net 10 = M4ReinfAce: 11 nodes at y=400, 10 edges closing a loop over nodes 0–9,
        // node 10 off the ring as the trailer's attach point, trailer [10, "player"].
        var nets = AiNets.Load(SessionPaths.ChapterZrdr(TestData.DataRoot!, "C1"));
        var net = nets.Find(n => n.Id == 10)!;
        Assert.Equal("M4ReinfAce", net.Name);
        Assert.Equal(11, net.Nodes.Count);
        foreach (var node in net.Nodes)
        {
            Assert.Equal(400f, node.Position.Y);
        }
        Assert.Equal(10, net.Edges.Count);
        Assert.Contains((0, 9), net.Edges);
        Assert.Equal(new AiNetTrailer(10, "player"), net.Trailer);
    }

    private static List<AiNet> Fixtures() => AiNets.Load(TestData.Fixture("ainets"));
}
