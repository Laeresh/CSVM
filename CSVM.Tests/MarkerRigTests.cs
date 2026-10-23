using System.Collections.Generic;
using System.Linq;
using CSVM.Extraction;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The aircraft weapon-marker rig (<c>docs/formats/markers.md</c>): which node names count as
/// markers, and how their plane-frame positions accumulate. Input is
/// <c>fixtures/gamez-plane/</c>.
/// </summary>
public class MarkerRigTests
{
    private static readonly System.Lazy<GameZ> Planes = new(() =>
        GameZ.Load(SessionPaths.PreferUnzipped(System.IO.Path.Combine(TestData.ExtractedRoot!, "planes.zip"))));

    [Theory]
    [InlineData("firepoint1", MarkerRig.MarkerKind.Firepoint, 1)]
    [InlineData("FIREPOINT8", MarkerRig.MarkerKind.Firepoint, 8)]
    [InlineData("pylon3", MarkerRig.MarkerKind.Pylon, 3)]
    [InlineData("target", MarkerRig.MarkerKind.Target, 0)]
    public void NumberedMarkersAndTheTargetAreClassified(string name, MarkerRig.MarkerKind kind, int ordinal)
    {
        Assert.True(MarkerRig.Classify(name, out var got, out int gotOrdinal));
        Assert.Equal(kind, got);
        Assert.Equal(ordinal, gotOrdinal);
    }

    [Theory]
    [InlineData("firepoint")]   // the AI airframes' bare, unnumbered marker
    [InlineData("pylon")]
    [InlineData("firepoint0")]  // ordinals are 1-based
    [InlineData("lpylon1")]     // the AI naming, not the player rig
    [InlineData("cockpit_camera")]
    [InlineData("ground_level")]
    public void EverythingElseInTheMarkersGroupIsRejected(string name)
    {
        Assert.False(MarkerRig.Classify(name, out _, out _));
    }

    [Fact]
    public void CoLocationGroupsOnlyPointsWithinTolerance()
    {
        var positions = new List<Vector3>
        {
            new(1f, 0f, 0f),
            new(1f, 0f, 0.001f),  // same mount
            new(-1f, 0f, 0f),
            new(1f, 0f, 0f),      // same mount
        };
        var groups = MarkerRig.GroupCoLocated(positions, MarkerRig.CoLocateTolerance);
        Assert.Single(groups);
        Assert.Equal(new[] { 0, 1, 3 }, groups[0]);
    }

    [Fact]
    public void DistinctMountsProduceNoGroups()
    {
        var positions = new List<Vector3> { new(1f, 0f, 0f), new(-1f, 0f, 0f) };
        Assert.Empty(MarkerRig.GroupCoLocated(positions, MarkerRig.CoLocateTolerance));
    }

    [Fact]
    public void PositionsAreAccumulatedInThePlaneFrameNotTheScene()
    {
        // The plane root's own transform places the airframe in a world; markers.md quotes
        // positions relative to the airframe origin, so the walk starts BELOW the root.
        var rig = MarkerRig.Extract(GameZ.Load(TestData.Fixture("gamez-plane")), "probe_plane")!;
        Assert.Equal("probe_plane", rig.PlaneRoot);
        Assert.Equal(4, rig.Markers.Count); // cockpit_camera is not a marker

        var fp1 = Find(rig, "firepoint1");
        Assert.Equal(new Vector3(-2f, 1f, -3f), fp1.Local); // markers group's +1 y, root's +100 x dropped
        Assert.Equal(new Vector3(2f, 1f, -3f), Find(rig, "firepoint2").Local);
        Assert.Equal(new Vector3(0f, 1f, 0f), Find(rig, "target").Local);
    }

    [Fact]
    public void MarkersAreSortedByKindThenOrdinal()
    {
        var rig = MarkerRig.Extract(GameZ.Load(TestData.Fixture("gamez-plane")), "probe_plane")!;
        Assert.Equal(
            new[] { "firepoint1", "firepoint2", "pylon1", "target" },
            Names(rig));
    }

    [Fact]
    public void TwoMarkersOnOneMountAreReportedAsShared()
    {
        var rig = MarkerRig.Extract(GameZ.Load(TestData.Fixture("gamez-plane")), "probe_plane")!;
        var group = Assert.Single(rig.CoLocated);
        var shared = new List<string>();
        foreach (int i in group)
        {
            shared.Add(rig.Markers[i].Name);
        }
        shared.Sort();
        Assert.Equal(new[] { "firepoint1", "pylon1" }, shared);
    }

    [Fact]
    public void AnAbsentPlaneRootYieldsNullRatherThanAnEmptyRig()
    {
        Assert.Null(MarkerRig.Extract(GameZ.Load(TestData.Fixture("gamez-plane")), "player_not_here"));
    }

    [Fact]
    public void FindNamedMarkerAccumulatesFromBelowTheRootAndSkipsAlternateStateSubtrees()
    {
        // "cockpit1" nests its own decoy cockpit_camera (9,9,9) as the FIRST child under
        // "markers", so a naive first-match walk would find it before the authored one at
        // markers' +1 y plus cockpit_camera's local +0.5 y.
        var offset = MarkerRig.FindNamedMarker(
            GameZ.Load(TestData.Fixture("gamez-plane")), "probe_plane", "cockpit_camera");
        Assert.Equal(new Vector3(0f, 1.5f, 0f), offset);
    }

    [Fact]
    public void FindNamedMarkerFallsBackWhenTheNodeIsAbsent()
    {
        var fallback = new Vector3(1f, 2f, 3f);
        var gamez = GameZ.Load(TestData.Fixture("gamez-plane"));
        Assert.Equal(fallback, MarkerRig.FindNamedMarker(gamez, "probe_plane", "no_such_node", fallback));
        Assert.Equal(fallback, MarkerRig.FindNamedMarker(gamez, "player_not_here", "cockpit_camera", fallback));
        Assert.Equal(Vector3.Zero, MarkerRig.FindNamedMarker(gamez, "player_not_here", "cockpit_camera"));
    }

    [Fact]
    public void TheRigTemplateParsesAsAMarkersOnlyRigHoldingEveryMarker()
    {
        var rig = MarkerRig.Extract(GameZ.Load(TestData.Fixture("gamez-plane")), "probe_plane")!;
        var parsed = PlaneRig.Parse(rig.FormatRigTemplate(), "template");
        Assert.Equal(rig.Markers.Count, parsed.Markers.Count);
        foreach (var m in rig.Markers)
        {
            Assert.Equal(m.Local, parsed.Markers[m.Name]);
        }
        Assert.Empty(parsed.Props);
        Assert.Empty(parsed.Surfaces);
        Assert.Null(parsed.LeftFlare);
        Assert.Null(parsed.RightFlare);
        Assert.Null(parsed.Cockpit); // the fixture's cockpit1 is no child of the root
    }

    [Fact]
    public void TheRigTemplateReadsBackEveryFloatExactly()
    {
        // 0.1f and 1e-7f have no short decimal form; a fixed-point print would move them.
        var markers = new[]
        {
            new MarkerRig.Marker("firepoint1", MarkerRig.MarkerKind.Firepoint, 1, new Vector3(0.1f, -1e-7f, -2.3456789f)),
            new MarkerRig.Marker("pylon2", MarkerRig.MarkerKind.Pylon, 2, new Vector3(3.3333333f, -0f, 1234.5678f)),
        };
        var parsed = PlaneRig.Parse(MarkerRig.RigTemplate(markers), "template");
        Assert.Equal(markers[0].Local, parsed.Markers["firepoint1"]);
        Assert.Equal(markers[1].Local, parsed.Markers["pylon2"]);
    }

    [Fact]
    public void ARepeatedMarkerNameKeepsItsFirstPositionSoTheTemplateStillParses()
    {
        var markers = new[]
        {
            new MarkerRig.Marker("target", MarkerRig.MarkerKind.Target, 0, new Vector3(0f, 1f, 0f)),
            new MarkerRig.Marker("TARGET", MarkerRig.MarkerKind.Target, 0, new Vector3(9f, 9f, 9f)),
        };
        var parsed = PlaneRig.Parse(MarkerRig.RigTemplate(markers), "template");
        Assert.Equal(new Vector3(0f, 1f, 0f), Assert.Single(parsed.Markers).Value);
    }

    [Fact]
    public void AnEmptyRigTemplateStillParses()
    {
        var parsed = PlaneRig.Parse(MarkerRig.RigTemplate(System.Array.Empty<MarkerRig.Marker>()), "template");
        Assert.Empty(parsed.Markers);
        Assert.Null(parsed.Cockpit);
    }

    [Fact]
    public void TheCockpitTemplateCarriesEachAssemblyAndTheGlassThroughTheFlatMount()
    {
        var (gamez, root) = Cockpit(glassInSpeedometer: false);
        var rig = MarkerRig.Extract(gamez, "plane")!;
        var marker = new Vector3(0f, 1f, -0.5f);
        // cockpit1's own local is left out, since the built mount replaces it.
        var speedo = new Transform3D(Basis.Identity, new Vector3(1f, 2f, 3f))
            * new Transform3D(new Basis(Vector3.Up, 0.5f), new Vector3(-2f, 0f, 4f));
        var glassCentre = new Vector3(0.5f, 0.5f, 10f);

        Assert.NotNull(rig.Cockpit);
        Assert.Equal(new[] { "speedometer", "altimeter", RigCockpit.GlassKey }, rig.Cockpit!.Select(g => g.Name));
        Assert.True(rig.Cockpit[0].Pose.IsEqualApprox(PlaneBuilder.InteriorToPlane(speedo, marker)));
        Assert.True(rig.Cockpit[2].Pose.IsEqualApprox(PlaneBuilder.InteriorToPlane(new Transform3D(Basis.Identity, glassCentre), marker)));

        var parsed = PlaneRig.Parse(rig.FormatRigTemplate(), "template");
        Assert.True(parsed.Cockpit is { FromBody: true, Hide.Count: 0 });
        PlaneRigPatch.CheckCockpit(parsed, gamez, root, new HashSet<string>());
        foreach (var g in rig.Cockpit)
        {
            var pose = parsed.Cockpit!.Gauges[g.Name];
            Assert.NotNull(pose.RotationDeg);
            Assert.NotNull(pose.Scale);
            Assert.True(CockpitTransplant.Posed(g.Pose, pose).IsEqualApprox(g.Pose), g.Name);
        }
    }

    [Fact]
    public void AnAssemblyHoldingTheGlassStandsInForTheGlassKey()
    {
        var (gamez, root) = Cockpit(glassInSpeedometer: true);
        var rig = MarkerRig.Extract(gamez, "plane")!;
        Assert.Equal(new[] { "speedometer", "altimeter" }, rig.Cockpit!.Select(g => g.Name));
        PlaneRigPatch.CheckCockpit(PlaneRig.Parse(rig.FormatRigTemplate(), "template"), gamez, root, new HashSet<string>());
    }

    [ExtractedDataTheory]
    [MemberData(nameof(PlaneRigTests.Assemblies), MemberType = typeof(PlaneRigTests))]
    public void EachPlayerAirframesTemplatePassesItsOwnChecksAndReadsBackItsPoses(string plane, string assemblies)
    {
        var gamez = Planes.Value;
        var root = gamez.FindByName(plane)!;
        var rig = MarkerRig.Extract(gamez, plane)!;
        var names = assemblies.Split(',');
        string? holder = System.Array.Find(names, n => n is "g730" or "g184");
        var want = holder == null ? names.Append(RigCockpit.GlassKey) : names;
        Assert.Equal(want.OrderBy(n => n), rig.Cockpit!.Select(g => g.Name).OrderBy(n => n));

        var parsed = PlaneRig.Parse(rig.FormatRigTemplate(), "template");
        PlaneRigPatch.CheckCockpit(parsed, gamez, root, new HashSet<string>());
        foreach (var g in rig.Cockpit!)
        {
            // A rig writes one uniform scale and a rotation, so the pose must be a rotation times a scale.
            var s = g.Pose.Basis.Scale;
            Assert.True(Mathf.Abs(s.X - s.Y) < 1e-6f && Mathf.Abs(s.X - s.Z) < 1e-6f && g.Pose.Basis.Determinant() > 0f, $"{g.Name} scale {s}");
            var back = CockpitTransplant.Posed(g.Pose, parsed.Cockpit!.Gauges[g.Name]);
            Assert.True(back.Origin.DistanceTo(g.Pose.Origin) < 1e-5f && (back.Basis.X - g.Pose.Basis.X).Length() < 1e-6f
                && (back.Basis.Y - g.Pose.Basis.Y).Length() < 1e-6f && (back.Basis.Z - g.Pose.Basis.Z).Length() < 1e-6f, g.Name);
        }

        if (holder != null)
        {
            // The holder draws every glass polygon cockpit1 has, so naming it places the whole glass.
            var interior = gamez.Nodes[root.Children.Find(c => gamez.Nodes[c].Name == "cockpit1")];
            // Names repeat inside cockpit1, so the holder is read under gauges.
            var gauges = Descendants(gamez, interior).First(n => n.Name == "gauges");
            var held = gauges.Children.Select(c => gamez.Nodes[c]).First(n => n.Name == holder);
            int all = GlassPolygons(gamez, interior);
            Assert.True(all > 0 && GlassPolygons(gamez, held) == all, $"{holder} holds {GlassPolygons(gamez, held)} of {all} glass polygons");
        }
    }

    private static int GlassPolygons(GameZ gamez, GameZNode node) =>
        Descendants(gamez, node).Where(n => n.MeshIndex >= 0 && n.MeshIndex < gamez.Meshes.Count)
            .Sum(n => gamez.Meshes[n.MeshIndex].Polygons.Count(p => p.MaterialIndex >= 0 && p.MaterialIndex < gamez.Materials.Count
                && gamez.Materials[p.MaterialIndex].TextureName is { } t && GunsightReticle.IsGlass(t)));

    private static IEnumerable<GameZNode> Descendants(GameZ gamez, GameZNode node) =>
        node.Children.SelectMany(c => Descendants(gamez, gamez.Nodes[c])).Prepend(node);

    // plane > markers > cockpit_camera; plane > cockpit1 > dash > gauges > speedometer, altimeter;
    // cockpit1 > inside. The glass square sits on inside or the speedometer.
    private static (GameZ Gamez, GameZNode Root) Cockpit(bool glassInSpeedometer)
    {
        var gamez = new GameZ();
        var locals = new (string Name, Transform3D Local)[]
        {
            ("plane", new Transform3D(Basis.Identity, new Vector3(100f, 0f, 0f))),
            ("markers", Transform3D.Identity),
            ("cockpit_camera", new Transform3D(Basis.Identity, new Vector3(0f, 1f, -0.5f))),
            ("cockpit1", new Transform3D(new Basis(Vector3.Right, 1f), new Vector3(5f, 5f, 5f))),
            ("dash", new Transform3D(Basis.Identity, new Vector3(1f, 2f, 3f))),
            ("gauges", Transform3D.Identity),
            ("speedometer", new Transform3D(new Basis(Vector3.Up, 0.5f), new Vector3(-2f, 0f, 4f))),
            ("altimeter", new Transform3D(Basis.Identity, new Vector3(2f, 0f, 4f))),
            ("inside", new Transform3D(Basis.Identity, new Vector3(0f, 0f, 10f))),
        };
        foreach (var (name, local) in locals)
        {
            gamez.Nodes.Add(new GameZNode { Name = name, Local = local });
        }

        gamez.Nodes[0].Children.AddRange(new[] { 1, 3 });
        gamez.Nodes[1].Children.Add(2);
        gamez.Nodes[3].Children.AddRange(new[] { 4, 8 });
        gamez.Nodes[4].Children.Add(5);
        gamez.Nodes[5].Children.AddRange(new[] { 6, 7 });
        gamez.Materials.Add(new GameZMaterial { TextureName = "cockpit6.tif" });
        var mesh = new GameZMesh();
        mesh.Vertices.AddRange(new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(1f, 1f, 0f), new Vector3(0f, 1f, 0f) });
        var square = new GameZPolygon { MaterialIndex = 0, UvCoords = new List<Vector2> { Vector2.Zero, Vector2.Right, Vector2.One, Vector2.Down } };
        square.VertexIndices.AddRange(new[] { 0, 1, 2, 3 });
        mesh.Polygons.Add(square);
        gamez.Meshes.Add(mesh);
        gamez.Nodes[glassInSpeedometer ? 6 : 8].MeshIndex = 0;
        return (gamez, gamez.Nodes[0]);
    }

    private static MarkerRig.Marker Find(MarkerRig rig, string name)
    {
        foreach (var m in rig.Markers)
        {
            if (m.Name == name)
            {
                return m;
            }
        }
        throw new Xunit.Sdk.XunitException($"marker '{name}' not in the rig");
    }

    private static string[] Names(MarkerRig rig)
    {
        var names = new string[rig.Markers.Count];
        for (int i = 0; i < rig.Markers.Count; i++)
        {
            names[i] = rig.Markers[i].Name;
        }
        return names;
    }
}
