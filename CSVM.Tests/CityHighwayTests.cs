using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The highway lanes <see cref="CityHighway"/> builds from a deck's own paint. Pieces chain by the ends they share, a split
/// deck's carriageways each carry one way, and every lane loop closes on the deck's surface.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class CityHighwayTests
{
    private const float Half = 13.4f;

    /// <summary>A straight quad, a mitred 45 degree bend painted the other way across, a climb and a level piece. They chain
    /// into one deck of five ends, each right corner right of travel. Both last pieces are two triangles. A climbing and a level
    /// triangle share the joint's edge but not its UVs, so they never pair. Each lane drives a closed loop on the
    /// deck's surface, out on its share of the width right of the centre.</summary>
    [Fact]
    public void LanesStayOnTheDeck()
    {
        var up = new Vector2(1f, 1f).Normalized();
        // The bend's mitre bisects the 45 degrees, so its edge is Half / cos(22.5 degrees) each side of the centre.
        var mitre = new Vector2(MathF.Cos(MathF.PI * 5f / 8f), MathF.Sin(MathF.PI * 5f / 8f)) * (Half / MathF.Cos(MathF.PI / 8f));
        var joint = new Vector2(300f, 0f);
        var bendEnd = joint + (up * 300f);
        var side = new Vector2(-up.Y, up.X) * Half;
        var top = bendEnd + (up * 200f);
        var far = top + (up * 100f);
        Vector3 Climb(Vector2 xz) => At(xz, (xz - bendEnd).Dot(up) > 1f ? 10f : 0f);
        var polygons = new List<(Vector3[] Corners, Vector2[] Uvs)>
        {
            (new[] { At(new(0f, -Half)), At(joint - mitre), At(joint + mitre), At(new(0f, Half)) }, new[] { Uv(0f, 0f), Uv(11f, 0f), Uv(11f, 1f), Uv(0f, 1f) }),
            (new[] { At(joint - mitre), At(bendEnd - side), At(bendEnd + side), At(joint + mitre) }, new[] { Uv(0f, 1f), Uv(11f, 1f), Uv(11f, 0f), Uv(0f, 0f) }),
            // The climb's triangle on the joint, then the level triangle on it, so a pairing by position alone takes them.
            (new[] { Climb(top - side), Climb(top + side), Climb(bendEnd + side) }, new[] { Uv(7.5f, 0f), Uv(7.5f, 1f), Uv(0f, 1f) }),
            (new[] { At(top - side, 10f), At(far + side, 10f), At(top + side, 10f) }, new[] { Uv(0f, 0f), Uv(3.7f, 1f), Uv(0f, 1f) }),
            (new[] { Climb(bendEnd - side), Climb(top - side), Climb(bendEnd + side) }, new[] { Uv(0f, 0f), Uv(7.5f, 0f), Uv(0f, 1f) }),
            (new[] { At(top - side, 10f), At(far - side, 10f), At(far + side, 10f) }, new[] { Uv(0f, 0f), Uv(3.7f, 0f), Uv(3.7f, 1f) }),
        };
        var chained = CityHighway.Chain(polygons);
        var deck = Assert.Single(chained.Decks);
        Assert.Equal((polygons.Count, 0), (chained.Polygons, chained.Orphans));
        Assert.Equal(5, deck.Count);
        for (int k = 0; k < deck.Count; k++)
        {
            var along = k + 1 < deck.Count ? Mid(deck[k + 1]) - Mid(deck[k]) : Mid(deck[k]) - Mid(deck[k - 1]);
            Assert.True(Flat(deck[k].R - deck[k].L).Dot(new Vector2(-along.Y, along.X)) > 0f, $"end {k}'s right corner lies left of travel");
        }

        var loops = CityHighway.Loops(chained.Decks, out int unclosed);
        Assert.Equal((CityHighway.Lanes.Count, 0), (loops.Count, unclosed));
        for (int i = 0; i < loops.Count; i++)
        {
            CityTrafficTests.AssertClosed(loops[i]);
            // Out along +X, the lane lies right of the centre, toward +Z.
            Assert.Equal(CityHighway.Lanes[i] * 2f * Half, loops[i].Segs[0].Start.Y, 2);
            Assert.Contains(loops[i].Segs, s => s.Radius > 0f && MathF.Abs((s.Length / s.Radius) - (MathF.PI / 4f)) < 0.001f);
            AssertOnDeck(loops[i], polygons);
        }
    }

    /// <summary>A two-way deck, two carriageways side by side, then a two-way deck again. Each lane runs end to end through
    /// the carriageway on its right and back through the other. It U-turns only at the two open ends.</summary>
    [Fact]
    public void ASplitTakesTheCarriagewayOnTheRight()
    {
        (Vector3[] Corners, Vector2[] Uvs) Piece(float x0, float x1, float z0, float z1) =>
            (new[] { At(new(x0, z0)), At(new(x1, z0)), At(new(x1, z1)), At(new(x0, z1)) }, new[] { Uv(0f, 0f), Uv(11f, 0f), Uv(11f, 1f), Uv(0f, 1f) });
        var polygons = new List<(Vector3[] Corners, Vector2[] Uvs)>
        {
            Piece(0f, 300f, -Half, Half), Piece(300f, 600f, 0f, Half), Piece(300f, 600f, -Half, 0f), Piece(600f, 900f, -Half, Half),
        };
        var chained = CityHighway.Chain(polygons);
        Assert.Equal((4, 4, 0), (chained.Decks.Count, chained.Polygons, chained.Orphans));
        var loops = CityHighway.Loops(chained.Decks, out int unclosed);
        Assert.Equal((CityHighway.Lanes.Count, 0), (loops.Count, unclosed));
        for (int i = 0; i < loops.Count; i++)
        {
            CityTrafficTests.AssertClosed(loops[i]);
            AssertOnDeck(loops[i], polygons);
            Assert.Equal(2, loops[i].Segs.Count(s => s.Radius != 0f && MathF.Abs(MathF.Abs(s.Length / s.Radius) - MathF.PI) < 0.01f));
            // Heading +X a car's right is +Z, so it takes the +Z carriageway, at its lane's place on the two-way deck.
            var split = loops[i].Segs.Where(s => s.Radius == 0f && s.Start.X > 310f && s.Start.X < 590f).ToList();
            Assert.NotEmpty(split);
            foreach (var seg in split)
            {
                float lane = CityHighway.Lanes[i] * 2f * Half * MathF.Sign(seg.Dir.X);
                Assert.Equal(lane, seg.Start.Y, 2);
            }
        }
    }

    private static Vector3 At(Vector2 xz, float y = 0f) => new(xz.X, y, xz.Y);

    private static Vector2 Uv(float u, float v) => new(u, v);

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    private static Vector2 Mid((Vector3 L, Vector3 R) end) => Flat((end.L + end.R) / 2f);

    // Every piece's samples stand on the deck's triangles at the deck's height.
    private static void AssertOnDeck(CityTraffic.Path path, List<(Vector3[] Corners, Vector2[] Uvs)> polygons)
    {
        var triangles = new List<Vector3>();
        foreach (var (corners, _) in polygons)
        {
            for (int i = 1; i + 1 < corners.Length; i++)
                triangles.AddRange(new[] { corners[0], corners[i], corners[i + 1] });
        }
        foreach (var seg in path.Segs)
        {
            for (int j = 0; j < 8; j++)
            {
                float s = seg.Length * (j + 0.5f) / 8f;
                var (at, _) = seg.Along(s);
                float y = seg.Radius == 0f ? Mathf.Lerp(seg.Y0, seg.Y1, s / seg.Length) : seg.Y0;
                Assert.True(CityDressing.Height(triangles, at) is { } deckY && MathF.Abs(deckY - y) < 0.01f,
                    $"a car {s:0.0} m along a piece from {seg.Start} stands at {at}, {y:0.00} m, off the deck");
            }
        }
    }
}
