using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The per-plane VR cockpit data (<c>CSVM/data/vr_cockpits.json</c>, <see cref="VrCockpits"/>).
/// The committed file names all eleven planes, each in range. A missing or malformed file or entry
/// falls back to the defaults. The offset is a move along the mount's tilted Z and Y, which the XR
/// origin's placement (<see cref="XrMount"/>) applies in first person alone.
/// </summary>
public class VrCockpitsTests
{
    private static readonly string Shipped = Path.Combine(TestData.RepoRoot, "CSVM", "data", "vr_cockpits.json");

    private static readonly string[] Planes =
    {
        "player_bhawk", "player_pfighter", "player_fury", "player_warhawk", "player_autogyro",
        "player_avenger", "player_balmoral", "player_brigand", "player_fbrand", "player_kestrel",
        "player_peacemaker",
    };

    [Fact]
    public void TheCommittedFileNamesAllElevenPlanesEachReadableInRange()
    {
        var problems = new List<string>();
        var table = VrCockpits.Parse(File.ReadAllBytes(Shipped), problems);

        Assert.Empty(problems);
        Assert.Equal(Planes.Length, table.Planes.Count);
        Assert.Equal(Planes, table.Names);
        foreach (var plane in Planes)
        {
            Assert.True(table.Planes.ContainsKey(plane), plane);
        }
    }

    [Fact]
    public void APlaneOrAFieldLeftOutTakesTheDefault()
    {
        var table = Parse("""{ "planes": { "player_fury": { "eyeOffsetZ": -0.1 } } }""", out var problems);

        Assert.Empty(problems);
        Assert.Equal(new VrCockpit(1f, -0.1f), table.For("player_fury"));
        Assert.Equal(0f, table.For("player_fury").EyeOffsetY);
        Assert.Equal(VrCockpit.Default, table.For("player_bhawk"));
    }

    [Fact]
    public void TheUpOffsetReadsInTheSameRangeAndRunsAlongTheMountsTiltedY()
    {
        var table = Parse("""{ "planes": { "player_fury": { "eyeOffsetY": 0.03 }, "player_bhawk": { "eyeOffsetY": -1.5 } } }""", out var problems);
        var up = table.For("player_fury").EyeOffsetInAircraft;
        float tilt = PlaneBuilder.HeadPitchOffsetRad;

        Assert.Single(problems);
        Assert.Contains("player_bhawk", problems[0]);
        Assert.Equal(new VrCockpit(1f, 0f, 0.03f), table.For("player_fury"));
        Assert.Equal(0f, up.X);
        Assert.Equal(0.03f * Mathf.Cos(tilt), up.Y, 6);
        Assert.Equal(0.03f * Mathf.Sin(tilt), up.Z, 6);
        Assert.Equal(new[] { "player_fury", "player_bhawk" }, table.Names);
    }

    [Fact]
    public void AnEntryReadsBothFieldsAndThePlaneNameIgnoresCase()
    {
        var table = Parse(
            """{ "planes": { "Player_Warhawk": { "interiorScaleFactor": 0.875, "eyeOffsetZ": 0.06, "_proposed": "prose" } } }""",
            out var problems);

        Assert.Empty(problems);
        Assert.Equal(new VrCockpit(0.875f, 0.06f), table.For("player_warhawk"));
    }

    [Theory]
    [InlineData("""{ "interiorScaleFactor": "big" }""")]
    [InlineData("""{ "interiorScaleFactor": 0 }""")]
    [InlineData("""{ "interiorScaleFactor": 40 }""")]
    [InlineData("""{ "eyeOffsetZ": 3 }""")]
    [InlineData("""{ "eyeOffsetZ": null }""")]
    [InlineData("""{ "interiorScaleFactor": 1e40 }""")]
    [InlineData("""{ "eyeOffsetY": -1e39 }""")]
    [InlineData("1.5")]
    public void AMalformedOrOutOfRangeEntryIsNamedAndTakesTheDefault(string entry)
    {
        var table = Parse($$"""{ "planes": { "player_fury": {{entry}}, "player_bhawk": { "eyeOffsetZ": 0.02 } } }""", out var problems);

        Assert.Single(problems);
        Assert.Contains("player_fury", problems[0]);
        Assert.Equal(VrCockpit.Default, table.For("player_fury"));
        Assert.Equal(new VrCockpit(1f, 0.02f), table.For("player_bhawk"));
    }

    [Fact]
    public void AFileWithNoPlanesObjectIsNamedAndEveryPlaneTakesTheDefault()
    {
        var table = Parse("""{ "player_fury": { "eyeOffsetZ": 0.1 } }""", out var problems);

        Assert.Single(problems);
        Assert.Empty(table.Planes);
        Assert.Equal(VrCockpit.Default, table.For("player_fury"));
    }

    [Fact]
    public void AMissingOrUnreadableFileWarnsAndYieldsTheDefaults()
    {
        string dir = Path.Combine(Path.GetTempPath(), "csvm-tests", "vr-cockpits-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string missing = Path.Combine(dir, "absent.json");
            string broken = Path.Combine(dir, "broken.json");
            File.WriteAllText(broken, "{ \"planes\": ", Encoding.UTF8);
            var lines = new List<string>();
            VrCockpits fromMissing, fromBroken;
            using (Log.PushConsoleSink(lines.Add))
            {
                fromMissing = VrCockpits.Load(missing);
                fromBroken = VrCockpits.Load(broken);
            }

            Assert.Same(VrCockpits.Defaults, fromMissing);
            Assert.Same(VrCockpits.Defaults, fromBroken);
            Assert.Contains(lines, l => l.Contains("not found"));
            Assert.Contains(lines, l => l.Contains("could not be read"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ANumberPastAFloatsRangeLoadsWithoutThrowingAndKeepsTheOtherPlanes()
    {
        string dir = Path.Combine(Path.GetTempPath(), "csvm-tests", "vr-cockpits-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string huge = Path.Combine(dir, "huge.json");
            File.WriteAllText(huge, """{ "planes": { "player_fury": { "interiorScaleFactor": 1e40 }, "player_bhawk": { "eyeOffsetZ": 0.02 } } }""");
            var lines = new List<string>();
            VrCockpits table;
            using (Log.PushConsoleSink(lines.Add))
            {
                table = VrCockpits.Load(huge);
            }

            Assert.Equal(VrCockpit.Default, table.For("player_fury"));
            Assert.Equal(new VrCockpit(1f, 0.02f), table.For("player_bhawk"));
            Assert.Contains(lines, l => l.Contains("player_fury") && l.Contains("out of range"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void TheFactorScalesTheFlatMount()
    {
        Assert.Equal(PlaneBuilder.InteriorScale, VrCockpit.Default.InteriorScale);
        Assert.Equal(0.035f, new VrCockpit(0.875f, 0f).InteriorScale, 6);
    }

    [Fact]
    public void TheEyeOffsetRunsAlongTheMountsTiltedZ()
    {
        var offset = new VrCockpit(1f, 0.06f).EyeOffsetInAircraft;
        float tilt = PlaneBuilder.HeadPitchOffsetRad;

        // Aft along the sight line, which the fixed head pitch tips nose-down: no lateral part.
        Assert.Equal(0f, offset.X);
        Assert.Equal(0.06f, offset.Length(), 6);
        Assert.Equal(0.06f * Mathf.Cos(tilt), offset.Z, 6);
        Assert.Equal(-0.06f * Mathf.Sin(tilt), offset.Y, 6);
        Assert.Equal(Vector3.Zero, VrCockpit.Default.EyeOffsetInAircraft);
    }

    [Fact]
    public void AFirstPersonOriginMovesByTheOffsetInTheAircraftsAxes()
    {
        var attitude = new Basis(Vector3.Up, 0.7f) * new Basis(Vector3.Right, 0.2f) * new Basis(Vector3.Back, 0.3f);
        var eye = new Transform3D(attitude * new Basis(Vector3.Right, -0.08203f), new Vector3(-6350f, 300f, -4144f));
        var offset = new VrCockpit(1f, 0.06f).EyeOffsetInAircraft;

        var mount = XrMount.For(eye, attitude, offset);

        // Within a millimetre: float32 coordinates near 6 km step by half of one.
        Assert.True(mount.Basis.IsEqualApprox(attitude));
        Assert.True((mount.Origin - eye.Origin).DistanceTo(attitude * offset) < 1e-3f);
        Assert.Equal(0.06f, mount.Origin.DistanceTo(XrMount.For(eye, attitude).Origin), 3);
    }

    [Fact]
    public void AnyOtherViewIgnoresTheOffset()
    {
        var eye = new Transform3D(new Basis(Vector3.Up, 1.1f), new Vector3(3f, 4f, 5f));
        var mount = XrMount.For(eye, null, new Vector3(0f, 0.2f, 0.5f));

        Assert.Equal(eye.Origin, mount.Origin);
    }

    private static VrCockpits Parse(string json, out List<string> problems)
    {
        problems = new List<string>();
        return VrCockpits.Parse(Encoding.UTF8.GetBytes(json), problems);
    }
}
