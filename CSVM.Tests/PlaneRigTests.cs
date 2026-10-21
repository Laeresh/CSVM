using System.IO;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The per-airframe <c>rig.json</c> reader: every section optional with absence kept distinct from
/// a value, and each rejection loud, naming the file and the entry.
/// </summary>
public class PlaneRigTests
{
    private const string Full = """
        {
          "markers": { "firepoint1": [-0.42, 0.18, -2.10], "pylon1": [-3.20, -0.35, 0.40],
                       "pylon2": [3.20, -0.35, 0.40], "cockpit_camera": [0, 1.1, -0.5] },
          "props": [ { "node": "prop1", "axis": [0, 0, 1], "deg_per_sec": -220 },
                     { "node": "prop1b", "blur": true } ],
          "surfaces": [ { "node": "l_aileron1", "kind": "aileron", "side": "left",
                          "hinge": { "origin": [-2.8, 0.0, 0.6], "axis": [1, 0, 0.12] } },
                        { "node": "r_elevator1", "kind": "elevator", "side": "right" },
                        { "node": "l_rudder1", "kind": "rudder" } ],
          "flares": { "left": "wing_flare1", "right": "wing_flare2" }
        }
        """;

    [Fact]
    public void AFullFileParsesEverySection()
    {
        var rig = PlaneRig.Parse(Full, "full.json");

        Assert.Equal(4, rig.Markers.Count);
        Assert.Equal(new Vector3(-0.42f, 0.18f, -2.10f), rig.Markers["firepoint1"]);
        Assert.Equal(new Vector3(3.20f, -0.35f, 0.40f), rig.Markers["pylon2"]);

        Assert.Equal(new RigProp("prop1", new Vector3(0, 0, 1), -220f, null), rig.Props[0]);
        Assert.Equal(new RigProp("prop1b", null, null, true), rig.Props[1]);

        Assert.Equal(3, rig.Surfaces.Count);
        var aileron = rig.Surfaces[0];
        Assert.Equal(RigSurfaceKind.Aileron, aileron.Kind);
        Assert.Equal(RigSide.Left, aileron.Side);
        Assert.Equal(new Vector3(-2.8f, 0f, 0.6f), aileron.HingeOrigin);
        Assert.Equal(new Vector3(1f, 0f, 0.12f), aileron.HingeAxis);
        Assert.Equal(ControlSurfaces.Kind.AileronLeft, aileron.ClassifierKind);
        Assert.Null(rig.Surfaces[1].HingeOrigin);
        Assert.Equal(ControlSurfaces.Kind.ElevatorRight, rig.Surfaces[1].ClassifierKind);
        Assert.Null(rig.Surfaces[2].Side);
        Assert.Equal(ControlSurfaces.Kind.Rudder, rig.Surfaces[2].ClassifierKind);

        Assert.Equal("wing_flare1", rig.LeftFlare);
        Assert.Equal("wing_flare2", rig.RightFlare);
    }

    [Fact]
    public void APartialFileLeavesTheOtherSectionsAbsent()
    {
        var rig = PlaneRig.Parse("""{ "markers": { "pylon3": [-2.5, -0.3, 0.4] } }""", "partial.json");

        Assert.Equal(new Vector3(-2.5f, -0.3f, 0.4f), rig.Markers["pylon3"]);
        Assert.Empty(rig.Props);
        Assert.Empty(rig.Surfaces);
        Assert.Null(rig.LeftFlare);
        Assert.Null(rig.RightFlare);
    }

    [Fact]
    public void AnEmptyObjectChangesNothing()
    {
        var rig = PlaneRig.Parse("{}", "empty.json");

        Assert.Empty(rig.Markers);
        Assert.Empty(rig.Props);
        Assert.Empty(rig.Surfaces);
        Assert.Null(rig.LeftFlare);
        Assert.Null(rig.RightFlare);
    }

    [Fact]
    public void LoadNamesThePathInItsErrors()
    {
        var path = Path.Combine(TestData.TempDir(), "rig.json");
        File.WriteAllText(path, """{ "markers": { "pylon9": [-1, 0, 0] } }""");

        var e = Assert.Throws<InvalidDataException>(() => PlaneRig.Load(path));
        Assert.Contains(path, e.Message);
    }

    [Fact]
    public void AnUnknownSectionIsRejected() =>
        Rejects("""{ "guns": [] }""", "root", "'guns'");

    [Fact]
    public void AnUnknownKeyInAPropIsRejected() =>
        Rejects("""{ "props": [ { "node": "prop1", "caliber": 50 } ] }""", "props[0]", "'caliber'");

    [Fact]
    public void AnUnknownKeyInASurfaceIsRejected() =>
        Rejects("""{ "surfaces": [ { "node": "l_rudder1", "kind": "rudder", "angle": 30 } ] }""", "surfaces[0]", "'angle'");

    [Fact]
    public void AnUnknownKeyInAHingeIsRejected() =>
        Rejects(
            """{ "surfaces": [ { "node": "l_rudder1", "kind": "rudder", "hinge": { "pivot": [0, 0, 0] } } ] }""",
            "surfaces[0] (l_rudder1).hinge",
            "'pivot'");

    [Fact]
    public void AnUnknownFlareSideIsRejected() =>
        Rejects("""{ "flares": { "left": "wing_flare1", "tail": "tail_flare" } }""", "flares", "'tail'");

    [Theory]
    [InlineData("""[1, 2]""")]
    [InlineData("""[1, 2, 3, 4]""")]
    [InlineData("""[1, "2", 3]""")]
    [InlineData("""{ "x": 1 }""")]
    public void AMarkerThatIsNotThreeNumbersIsRejected(string value) =>
        Rejects($$"""{ "markers": { "firepoint1": {{value}} } }""", "markers.firepoint1", "");

    [Theory]
    [InlineData("""{ "markers": { "firepoint1": [1e39, 0, 0] } }""", "markers.firepoint1")]
    [InlineData("""{ "markers": { "firepoint1": [0, -1e400, 0] } }""", "markers.firepoint1")]
    [InlineData("""{ "props": [ { "node": "prop1", "deg_per_sec": 1e39 } ] }""", "props[0] (prop1).deg_per_sec")]
    public void ANonFiniteNumberIsRejected(string json, string entry) =>
        Rejects(json, entry, "finite");

    [Fact]
    public void AZeroLengthPropAxisIsRejected() =>
        Rejects("""{ "props": [ { "node": "prop1", "axis": [0, 0, 0] } ] }""", "props[0] (prop1).axis", "zero-length");

    [Fact]
    public void AZeroLengthHingeAxisIsRejected() =>
        Rejects(
            """{ "surfaces": [ { "node": "l_rudder1", "kind": "rudder", "hinge": { "axis": [0, 0, 0] } } ] }""",
            "surfaces[0] (l_rudder1).hinge.axis",
            "zero-length");

    [Theory]
    [InlineData("pylon0")]
    [InlineData("pylon9")]
    [InlineData("pylon10")]
    public void APylonOutsideOneToEightIsRejected(string name) =>
        Rejects($$"""{ "markers": { "{{name}}": [-1, 0, 0] } }""", "markers." + name, "1..8");

    [Theory]
    [InlineData("pylon1", 1.5)]
    [InlineData("pylon2", -1.5)]
    [InlineData("pylon7", 0.0)]
    [InlineData("pylon8", 0.0)]
    public void APylonOnTheWrongSideOrTheCentrelineIsRejected(string name, double x) =>
        Rejects(
            $$"""{ "markers": { "{{name}}": [{{x.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, 0, 0] } }""",
            "markers." + name,
            "needs x");

    [Fact]
    public void AnUnknownSurfaceKindIsRejected() =>
        Rejects("""{ "surfaces": [ { "node": "flap1", "kind": "flap", "side": "left" } ] }""", "surfaces[0] (flap1).kind", "'flap'");

    [Fact]
    public void AnUnknownSurfaceSideIsRejected() =>
        Rejects("""{ "surfaces": [ { "node": "l_aileron1", "kind": "aileron", "side": "centre" } ] }""", "surfaces[0] (l_aileron1).side", "'centre'");

    [Fact]
    public void AnAileronWithoutASideIsRejected() =>
        Rejects("""{ "surfaces": [ { "node": "l_aileron1", "kind": "aileron" } ] }""", "surfaces[0] (l_aileron1)", "needs a side");

    private static void Rejects(string json, string entry, string problem)
    {
        var e = Assert.Throws<InvalidDataException>(() => PlaneRig.Parse(json, "probe/rig.json"));
        Assert.Contains("'probe/rig.json'", e.Message);
        Assert.Contains(entry + ":", e.Message);
        Assert.Contains(problem, e.Message);
    }
}
