using System.Collections.Generic;
using CSVM;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="GameZ.VertexColorsRestateMaterialColor"/>: the untextured polygon whose vertex
/// colours only repeat its own material's colour. Two authored slots, one authored value —
/// multiplying them squares it, which is what turned every skydome's below-horizon skirt into a
/// hard band against the terrain's fog wall (<c>PLAN-overcast-match</c> B18).
///
/// <para>The census below is the tripwire for the rule's blast radius: 85 of the 87 non-white
/// cases install-wide are those skirts, and a future extraction that grows the number means
/// something other than a skydome started restating a flat colour.</para>
/// </summary>
public class FlatColorTests
{
    /// <summary>Per chapter: <c>restated-white|restated-non-white</c> over every polygon's BASE
    /// material. The white ones are identity (white × white), and are counted only so the split
    /// stays visible. The non-white ones are the skydome skirts — C4 also has two black
    /// <c>g206</c> polygons, which are inert either way (black × black is black) — and C5 has
    /// none at all, because its skirt is textured rather than <c>Colored</c>, which is why the
    /// C5 renders were bit-identical across this change.</summary>
    public static TheoryData<string, string> ChapterRestatedCounts => new()
    {
        { "C1", "43|14" },
        { "C1B", "26|13" },
        { "C1C", "115|15" },
        { "C2", "41|13" },
        { "C2B", "16|15" },
        { "C3", "28|13" },
        { "C4", "62|4" },
        { "C5", "35|0" },
    };

    [Fact]
    public void AFlatColourRestatedByEveryCornerIsOneValue()
    {
        var gamez = Build(Colored(new Color(176 / 255f, 176 / 255f, 176 / 255f)));
        var poly = Poly(new Color(176 / 255f, 176 / 255f, 176 / 255f));

        Assert.True(gamez.VertexColorsRestateMaterialColor(poly, 0));
    }

    [Fact]
    public void WhiteCornersOverAColouredMaterialStayAProduct()
    {
        // The install's common shape (677 polygons): the material carries the colour and the
        // corners are white, so the product already applies it exactly once.
        var gamez = Build(Colored(new Color(0.75f, 0.75f, 0.75f)));

        Assert.False(gamez.VertexColorsRestateMaterialColor(Poly(Colors.White), 0));
    }

    [Fact]
    public void ARealCornerGradientIsNeverTreatedAsARestatement()
    {
        // C1's `part3` debris pieces: material (94,94,94) with corners running 94 -> 255. That
        // gradient is authored shading and must survive.
        var grey = new Color(94 / 255f, 94 / 255f, 94 / 255f);
        var gamez = Build(Colored(grey));

        Assert.False(gamez.VertexColorsRestateMaterialColor(Poly(grey, grey, Colors.White), 0));
    }

    [Fact]
    public void ATexturedMaterialIsNeverARestatement()
    {
        // A textured surface's vertex colours are its baked lighting over the texture — the
        // material carries no colour to restate.
        var gamez = Build(new GameZMaterial { TextureName = "sky1.tif" });

        Assert.False(gamez.VertexColorsRestateMaterialColor(Poly(Colors.White), 0));
    }

    [Fact]
    public void AnAbsentMaterialOrAbsentCornersDecideNothing()
    {
        var gamez = Build(Colored(Colors.White));

        Assert.False(gamez.VertexColorsRestateMaterialColor(Poly(Colors.White), -1));
        Assert.False(gamez.VertexColorsRestateMaterialColor(Poly(Colors.White), 7));
        Assert.False(gamez.VertexColorsRestateMaterialColor(new GameZPolygon(), 0));
    }

    [ExtractedDataTheory]
    [MemberData(nameof(ChapterRestatedCounts))]
    public void TheRestatedFlatColoursAreTheSkydomeSkirtsAndNothingElse(string chapter, string expected)
    {
        var gamez = GameZ.Load(SessionPaths.ChapterGamez(TestData.DataRoot!, chapter));

        int white = 0;
        int coloured = 0;
        foreach (var mesh in gamez.Meshes)
        {
            if (mesh == null)
            {
                continue;
            }
            foreach (var poly in mesh.Polygons)
            {
                if (!gamez.VertexColorsRestateMaterialColor(poly, poly.MaterialIndex))
                {
                    continue;
                }
                if (gamez.Materials[poly.MaterialIndex].Color.IsEqualApprox(Colors.White))
                {
                    white++;
                }
                else
                {
                    coloured++;
                }
            }
        }

        Assert.Equal(expected, $"{white}|{coloured}");
    }

    private static GameZMaterial Colored(Color color) => new() { Color = color };

    private static GameZ Build(GameZMaterial material)
    {
        var gamez = new GameZ();
        gamez.Materials.Add(material);
        return gamez;
    }

    private static GameZPolygon Poly(params Color[] corners)
    {
        var poly = new GameZPolygon { MaterialIndex = 0, VertexColors = new List<Color>() };
        for (int i = 0; i < corners.Length; i++)
        {
            poly.VertexIndices.Add(i);
            poly.VertexColors.Add(corners[i]);
        }
        return poly;
    }
}
