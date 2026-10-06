using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Extraction;
using CSVM.Flight.Hud;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The head-and-shoulders plate the stereo interior leaves out (<see cref="CockpitPlate"/>), and
/// the rule that keeps a stereo session's interior in the main world
/// (<see cref="CockpitOverlay.InteriorInPass"/>). The per-plane counts are the selection on the
/// player's own extraction: a rule that also took canopy structure, or missed a plate, moves them.
/// </summary>
public class CockpitPlateTests
{
    private static readonly Lazy<GameZ> Planes = new(() =>
        GameZ.Load(SessionPaths.PreferUnzipped(Path.Combine(TestData.ExtractedRoot!, "planes.zip"))));

    /// <summary>The polygons each player airframe's plate is drawn with. The Balmoral's is the one
    /// five-polygon plate, and the Warhawk's sits 6.8 units aft where the rest lie within 1.8.</summary>
    public static TheoryData<string, int> PlatePolygons => new()
    {
        { "player_bhawk", 7 },
        { "player_pfighter", 7 },
        { "player_fury", 7 },
        { "player_warhawk", 7 },
        { "player_autogyro", 7 },
        { "player_avenger", 7 },
        { "player_balmoral", 5 },
        { "player_brigand", 7 },
        { "player_fbrand", 7 },
        { "player_kestrel", 7 },
        { "player_peacemaker", 7 },
    };

    [ExtractedDataTheory]
    [MemberData(nameof(PlatePolygons))]
    public void EachInteriorsPlateIsSelectedWholeAndAlone(string plane, int expected)
    {
        var gamez = Planes.Value;
        var root = gamez.FindByName(plane) ?? throw new InvalidOperationException($"{plane} not in planes.zbd");
        GameZNode? interior = null;
        foreach (int child in root.Children)
        {
            if (gamez.Nodes[child].Name.Equals("cockpit1", StringComparison.OrdinalIgnoreCase))
            {
                interior = gamez.Nodes[child];
            }
        }

        Assert.NotNull(interior);
        Assert.Equal(expected, CockpitPlate.Find(gamez, interior!).Count);
    }

    [Fact]
    public void AnAftFacingColourQuadBehindTheEyeIsPlate()
    {
        Assert.True(CockpitPlate.IsPlate(Quad(0.8f), Polygon(), Colour()));
        Assert.True(CockpitPlate.IsPlate(Quad(CockpitPlate.MaxAft), Polygon(), Colour()));
    }

    [Fact]
    public void EachHalfOfTheRuleRejectsOnItsOwn()
    {
        Assert.False(CockpitPlate.IsPlate(Reversed(Quad(0.8f)), Polygon(), Colour()), "facing the eye");
        Assert.False(CockpitPlate.IsPlate(Quad(0f), Polygon(), Colour()), "on the eye's own plane");
        Assert.False(CockpitPlate.IsPlate(Quad(-0.8f), Polygon(), Colour()), "ahead of the eye");
        Assert.False(CockpitPlate.IsPlate(Quad(CockpitPlate.MaxAft + 0.5f), Polygon(), Colour()), "too far aft");
        Assert.False(CockpitPlate.IsPlate(Quad(0.8f), Polygon(priority: 4), Colour()), "another priority");
        Assert.False(CockpitPlate.IsPlate(Quad(0.8f), Polygon(), new GameZMaterial { TextureName = "panel.tif" }), "textured");
        Assert.False(CockpitPlate.IsPlate(Quad(0.8f), Polygon(), null), "no material");
        var tilted = Quad(0.8f);
        tilted[2] += new Vector3(0f, 0f, 0.05f);
        Assert.False(CockpitPlate.IsPlate(tilted, Polygon(), Colour()), "off one z plane");
    }

    [Fact]
    public void OnlyAFlatSessionThatAsksDrawsItsInteriorThroughThePass()
    {
        Assert.True(CockpitOverlay.InteriorInPass(asked: true, stereo: false));
        Assert.False(CockpitOverlay.InteriorInPass(asked: false, stereo: false));
        Assert.False(CockpitOverlay.InteriorInPass(asked: true, stereo: true));
        Assert.False(CockpitOverlay.InteriorInPass(asked: false, stereo: true));
    }

    // A unit quad wound counter-clockwise seen from +Z, which is a face normal pointing aft.
    private static List<Vector3> Quad(float z) => new()
    {
        new Vector3(0f, 0f, z), new Vector3(1f, 0f, z), new Vector3(1f, 1f, z), new Vector3(0f, 1f, z),
    };

    private static List<Vector3> Reversed(List<Vector3> corners)
    {
        corners.Reverse();
        return corners;
    }

    private static GameZPolygon Polygon(int priority = CockpitPlate.Priority) => new() { Priority = priority };

    private static GameZMaterial Colour() => new() { Color = new Color(0.84f, 0.17f, 0f) };
}
