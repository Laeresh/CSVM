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

    /// <summary>A replacement surface has no priority, so <see cref="CockpitPlate.Kept"/> judges its
    /// untextured triangles by shape alone. Over each interior's untextured polygons, triangulated as the
    /// build draws them, that drops exactly the plate's triangles. No other untextured one has its shape.</summary>
    [ExtractedDataTheory]
    [MemberData(nameof(PlatePolygons))]
    public void TheShapeCutDropsExactlyThePlatesUntexturedTriangles(string plane, int expected)
    {
        var gamez = Planes.Value;
        var root = gamez.FindByName(plane) ?? throw new InvalidOperationException($"{plane} not in planes.zbd");
        var interior = gamez.Nodes[root.Children.Find(c => gamez.Nodes[c].Name.Equals("cockpit1", StringComparison.OrdinalIgnoreCase))];
        var plate = CockpitPlate.Find(gamez, interior);
        int platePolygons = 0, wrong = 0;
        void Walk(int index, Transform3D parent)
        {
            var node = gamez.Nodes[index];
            if (node.Kind == "Lod" && node.LodRangeMin != 0f)
            {
                return;
            }

            var at = parent * (node.Local ?? Transform3D.Identity);
            if (node.MeshIndex >= 0 && node.MeshIndex < gamez.Meshes.Count)
            {
                var mesh = gamez.Meshes[node.MeshIndex];
                foreach (var polygon in mesh.Polygons)
                {
                    if (polygon.MaterialIndex < 0 || polygon.MaterialIndex >= gamez.Materials.Count
                        || gamez.Materials[polygon.MaterialIndex].TextureName != null)
                    {
                        continue;
                    }

                    var vertices = Triangles(mesh, polygon);
                    int kept = CockpitPlate.Kept(vertices, null, at).Length;
                    bool isPlate = plate.Contains(polygon);
                    platePolygons += isPlate ? 1 : 0;
                    wrong += kept == (isPlate ? 0 : vertices.Count) ? 0 : 1;
                }
            }

            foreach (int child in node.Children)
            {
                Walk(child, at);
            }
        }

        foreach (int child in interior.Children)
        {
            Walk(child, Transform3D.Identity);
        }

        Assert.Equal(expected, platePolygons);
        Assert.Equal(0, wrong);
    }

    [Fact]
    public void TheShapeCutKeepsEveryTriangleButTheAftFacingOnes()
    {
        var quad = Quad(0.8f);
        // Two triangles of the plate quad, in its winding, then one of them reversed, facing the eye.
        var vertices = new List<Vector3> { quad[0], quad[1], quad[2], quad[0], quad[2], quad[3], quad[0], quad[2], quad[1] };
        Assert.Equal(new[] { 6, 7, 8 }, CockpitPlate.Kept(vertices, null, Transform3D.Identity));
        Assert.Equal(new[] { 0, 2, 1 }, CockpitPlate.Kept(quad, new[] { 0, 1, 2, 0, 2, 1 }, Transform3D.Identity));
        // Carried 10 units forward, the plate stands ahead of the eye and stays.
        Assert.Equal(6, CockpitPlate.Kept(quad, new[] { 0, 1, 2, 0, 2, 3 }, new Transform3D(Basis.Identity, new Vector3(0f, 0f, -10f))).Length);
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

    // A polygon's triangles as SceneBuilder.EmitPolygon draws them: a fan, or a strip of alternating winding.
    private static List<Vector3> Triangles(GameZMesh mesh, GameZPolygon polygon)
    {
        var v = polygon.VertexIndices;
        var corners = new List<Vector3>();
        for (int i = 0; i + 2 < v.Count; i++)
        {
            var (a, b, c) = !polygon.TriangleStrip ? (0, i + 1, i + 2) : (i & 1) == 0 ? (i, i + 1, i + 2) : (i, i + 2, i + 1);
            corners.Add(mesh.Vertices[v[a]]);
            corners.Add(mesh.Vertices[v[b]]);
            corners.Add(mesh.Vertices[v[c]]);
        }

        return corners;
    }

    private static List<Vector3> Reversed(List<Vector3> corners)
    {
        corners.Reverse();
        return corners;
    }

    private static GameZPolygon Polygon(int priority = CockpitPlate.Priority) => new() { Priority = priority };

    private static GameZMaterial Colour() => new() { Color = new Color(0.84f, 0.17f, 0f) };
}
