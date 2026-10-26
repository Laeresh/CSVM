using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Extraction;
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

    private static readonly Lazy<GameZ> Planes = new(() =>
        GameZ.Load(SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "planes.zip"))));

    /// <summary>Every assembly the data carries per player airframe, so a rig naming them passes. The
    /// Brigand's and Firebrand's sight glass is itself a child of <c>gauges</c>.</summary>
    public static TheoryData<string, string> Assemblies => new()
    {
        { "player_bhawk", "horizn,comp,altimeter,missilegauge,gungauge,speedometer,nitrogauge,damageindicator" },
        { "player_pfighter", "horiz,comp,altimeter,speedometer,damageindicator,missilegauge,gungauge,nitrogauge" },
        { "player_fury", "horizm,comp,speedometer,altimeter,missilegauge,gungauge,damageindicator,nitrogauge" },
        { "player_warhawk", "missilegauge,hparent,speedometer,altimeter,gungauge,damageindicator,comp,nitrogauge" },
        { "player_autogyro", "horizn,altimeter,speedometer,comp,gungauge,missilegauge,nitrogauge,damageindicator" },
        { "player_avenger", "horiz,comp,altimeter,speedometer,missilegauge,gungauge,nitrogauge,damageindicator" },
        { "player_balmoral", "horiz,comp,altimeter,speedometer,missilegauge,gungauge,nitrogauge,damageindicator" },
        { "player_brigand", "horizn,comp,g730,speedometer,altimeter,missilegauge,gungauge,damageindicator,nitrogauge" },
        { "player_fbrand", "hparent,comp,altimeter,speedometer,g184,gungauge,missilegauge,nitrogauge,damageindicator" },
        { "player_kestrel", "horiz,comp,altimeter,speedometer,damageindicator,missilegauge,gungauge,nitrogauge" },
        { "player_peacemaker", "horiz,comp,altimeter,speedometer,missilegauge,gungauge,nitrogauge,damageindicator" },
    };

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

    [Theory]
    [InlineData("""{ "markers": { "cockpit_camera": [0, 1, 0], "Cockpit_Camera": [0, 2, 0] } }""", "markers.Cockpit_Camera", "defined twice")]
    [InlineData("""{ "props": [ { "node": "prop1", "blur": true }, { "node": "PROP1", "blur": false } ] }""", "props (PROP1)", "listed twice")]
    [InlineData("""{ "surfaces": [ { "node": "l_rudder1", "kind": "rudder" }, { "node": "L_Rudder1", "kind": "rudder" } ] }""", "surfaces (L_Rudder1)", "listed twice")]
    public void ANameGivenTwiceInTwoCasesIsRejected(string json, string entry, string problem) =>
        Rejects(json, entry, problem);

    [Fact]
    public void AnUnknownSurfaceKindIsRejected() =>
        Rejects("""{ "surfaces": [ { "node": "flap1", "kind": "flap", "side": "left" } ] }""", "surfaces[0] (flap1).kind", "'flap'");

    [Fact]
    public void AnUnknownSurfaceSideIsRejected() =>
        Rejects("""{ "surfaces": [ { "node": "l_aileron1", "kind": "aileron", "side": "centre" } ] }""", "surfaces[0] (l_aileron1).side", "'centre'");

    [Fact]
    public void AnAileronWithoutASideIsRejected() =>
        Rejects("""{ "surfaces": [ { "node": "l_aileron1", "kind": "aileron" } ] }""", "surfaces[0] (l_aileron1)", "needs a side");

    [Fact]
    public void ABodyCockpitParsesWithAbsentPoseFieldsNull()
    {
        var rig = PlaneRig.Parse(
            """
            { "cockpit": { "from": "body", "hide": ["g12", "roofnode"],
                "gauges": { "speedometer": { "position": [0.1, 0.9, -1.2], "rotation": [-20, 0, 5], "scale": 0.05 },
                            "glass": { "position": [0, 1.1, -1.0] } } } }
            """,
            "cockpit.json");

        var cockpit = rig.Cockpit!;
        Assert.True(cockpit.FromBody);
        Assert.Equal(new[] { "g12", "roofnode" }, cockpit.Hide);
        Assert.Equal(new RigGaugePose(new Vector3(0.1f, 0.9f, -1.2f), new Vector3(-20f, 0f, 5f), 0.05f), cockpit.Gauges["SPEEDOMETER"]);
        Assert.Equal(new RigGaugePose(new Vector3(0f, 1.1f, -1.0f), null, null), cockpit.Gauges[RigCockpit.GlassKey]);
    }

    [Fact]
    public void ACockpitWithoutFromKeepsCockpit1AndNoSectionIsNull()
    {
        var rig = PlaneRig.Parse("""{ "cockpit": { "gauges": { "comp": { "position": [1, 2, 3] } } } }""", "c1.json");

        Assert.False(rig.Cockpit!.FromBody);
        Assert.Empty(rig.Cockpit.Hide);
        Assert.Single(rig.Cockpit.Gauges);
        Assert.Null(PlaneRig.Parse("{}", "empty.json").Cockpit);
    }

    [Theory]
    [InlineData("""{ "cockpit": [] }""", "cockpit", "not a JSON object")]
    [InlineData("""{ "cockpit": { "seat": [0, 1, 0] } }""", "cockpit", "'seat'")]
    [InlineData("""{ "cockpit": { "from": "cockpit1" } }""", "cockpit.from", "'cockpit1'")]
    [InlineData("""{ "cockpit": { "from": "" } }""", "cockpit.from", "non-empty")]
    [InlineData("""{ "cockpit": { "hide": ["g12"] } }""", "cockpit.hide", "needs \"from\": \"body\"")]
    [InlineData("""{ "cockpit": { "from": "body", "hide": "g12" } }""", "cockpit.hide", "not a JSON array")]
    [InlineData("""{ "cockpit": { "from": "body", "hide": ["g12", 4] } }""", "cockpit.hide[1]", "non-empty")]
    [InlineData("""{ "cockpit": { "from": "body", "hide": ["g12", "G12"] } }""", "cockpit.hide[1] (G12)", "listed twice")]
    [InlineData("""{ "cockpit": { "gauges": [] } }""", "cockpit.gauges", "not a JSON object")]
    [InlineData("""{ "cockpit": { "gauges": { "comp": { "position": [0, 0, 0] }, "Comp": { "position": [0, 0, 0] } } } }""", "cockpit.gauges.Comp", "defined twice")]
    [InlineData("""{ "cockpit": { "gauges": { "comp": [0, 0, 0] } } }""", "cockpit.gauges.comp", "not a JSON object")]
    [InlineData("""{ "cockpit": { "gauges": { "comp": { "rotation": [0, 0, 0] } } } }""", "cockpit.gauges.comp", "missing 'position'")]
    [InlineData("""{ "cockpit": { "gauges": { "comp": { "position": [0, 0, 0], "tilt": 3 } } } }""", "cockpit.gauges.comp", "'tilt'")]
    [InlineData("""{ "cockpit": { "gauges": { "comp": { "position": [0, 1e39, 0] } } } }""", "cockpit.gauges.comp.position", "finite")]
    [InlineData("""{ "cockpit": { "gauges": { "comp": { "position": [0, 0, 0], "rotation": [0, 90] } } } }""", "cockpit.gauges.comp.rotation", "3 numbers")]
    [InlineData("""{ "cockpit": { "gauges": { "comp": { "position": [0, 0, 0], "scale": 0 } } } }""", "cockpit.gauges.comp.scale", "positive")]
    [InlineData("""{ "cockpit": { "gauges": { "comp": { "position": [0, 0, 0], "scale": -2 } } } }""", "cockpit.gauges.comp.scale", "positive")]
    [InlineData("""{ "cockpit": { "gauges": { "comp": { "position": [0, 0, 0], "scale": 1e39 } } } }""", "cockpit.gauges.comp.scale", "finite")]
    public void AMalformedCockpitIsRejected(string json, string entry, string problem) =>
        Rejects(json, entry, problem);

    [Fact]
    public void CockpitNamesAreCheckedOnTheGamezTree()
    {
        var (gamez, root) = Airframe();
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "body", "roof" };

        PlaneRigPatch.CheckCockpit(Cockpit("""{ "from": "body", "hide": ["ROOF"], "gauges": { "Speedometer": { "position": [0, 0, 0] }, "glass": { "position": [0, 0, 0] } } }"""), gamez, root, known);
        CockpitRejects("""{ "from": "body", "hide": ["canopy"] }""", gamez, root, known, "cockpit.hide[0] (canopy)", "no node");
        CockpitRejects("""{ "from": "body", "hide": ["speedometer"] }""", gamez, root, known, "cockpit.hide[0] (speedometer)", "no node");
        CockpitRejects("""{ "gauges": { "horizn": { "position": [0, 0, 0] } } }""", gamez, root, known, "cockpit.gauges.horizn", "it has: speedometer");

        gamez.Materials[0].TextureName = "panel.tif";
        CockpitRejects("""{ "gauges": { "glass": { "position": [0, 0, 0] } } }""", gamez, root, known, "cockpit.gauges.glass", "no sight glass");
    }

    [ExtractedDataTheory]
    [MemberData(nameof(Assemblies))]
    public void EachPlayerAirframeAcceptsItsOwnAssembliesAndGlass(string plane, string assemblies)
    {
        var gamez = Planes.Value;
        var root = gamez.FindByName(plane)!;
        static string Entries(IEnumerable<string> names) =>
            string.Join(", ", names.Select(a => $"\"{a}\": {{ \"position\": [0, 0, 0] }}"));
        var names = assemblies.Split(',');
        // The Brigand's and Firebrand's glass is an assembly, which the glass key may not name twice.
        string? holder = Array.Find(names, n => n is "g730" or "g184");
        var withGlass = names.Where(n => n != holder).Append("glass");
        PlaneRigPatch.CheckCockpit(Cockpit($$"""{ "gauges": { {{Entries(names)}} } }"""), gamez, root, new HashSet<string>());
        PlaneRigPatch.CheckCockpit(Cockpit($$"""{ "gauges": { {{Entries(withGlass)}} } }"""), gamez, root, new HashSet<string>());
        if (holder != null)
        {
            CockpitRejects($$"""{ "gauges": { {{Entries(names.Append("glass"))}} } }""", gamez, root, new HashSet<string>(), "cockpit.gauges", $"'glass' and '{holder}' both place the sight glass");
        }

        CockpitRejects("""{ "gauges": { "compass": { "position": [0, 0, 0] } } }""", gamez, root, new HashSet<string>(), "cockpit.gauges.compass", "no assembly");
    }

    [Fact]
    public void TheGlassKeyAndTheAssemblyHoldingTheGlassMayNotBothBeNamed()
    {
        var (gamez, root) = Airframe();
        gamez.Nodes[4].MeshIndex = 0; // the speedometer now holds the glass polygon
        CockpitRejects("""{ "gauges": { "speedometer": { "position": [0, 0, 0] }, "glass": { "position": [0, 0, 0] } } }""",
            gamez, root, new HashSet<string>(), "cockpit.gauges", "'glass' and 'speedometer' both place the sight glass");
    }

    private static PlaneRig Cockpit(string section) => PlaneRig.Parse($$"""{ "cockpit": {{section}} }""", "probe/rig.json");

    private static void CockpitRejects(string section, GameZ gamez, GameZNode root, HashSet<string> known, string entry, string problem)
    {
        var e = Assert.Throws<InvalidDataException>(() => PlaneRigPatch.CheckCockpit(Cockpit(section), gamez, root, known));
        Assert.Contains("'probe/rig.json'", e.Message);
        Assert.Contains(entry + ":", e.Message);
        Assert.Contains(problem, e.Message);
    }

    // plane > body, cockpit1 > gauges > speedometer, cockpit1 > inside (the glass mesh).
    private static (GameZ Gamez, GameZNode Root) Airframe()
    {
        var gamez = new GameZ();
        string[] names = { "plane", "body", "cockpit1", "gauges", "speedometer", "inside" };
        foreach (string name in names)
        {
            gamez.Nodes.Add(new GameZNode { Name = name });
        }

        gamez.Nodes[0].Children.AddRange(new[] { 1, 2 });
        gamez.Nodes[2].Children.AddRange(new[] { 3, 5 });
        gamez.Nodes[3].Children.Add(4);
        gamez.Materials.Add(new GameZMaterial { TextureName = "cockpit6.tif" });
        var mesh = new GameZMesh();
        mesh.Polygons.Add(new GameZPolygon { MaterialIndex = 0 });
        gamez.Meshes.Add(mesh);
        gamez.Nodes[5].MeshIndex = 0;
        return (gamez, gamez.Nodes[0]);
    }

    private static void Rejects(string json, string entry, string problem)
    {
        var e = Assert.Throws<InvalidDataException>(() => PlaneRig.Parse(json, "probe/rig.json"));
        Assert.Contains("'probe/rig.json'", e.Message);
        Assert.Contains(entry + ":", e.Message);
        Assert.Contains(problem, e.Message);
    }
}
