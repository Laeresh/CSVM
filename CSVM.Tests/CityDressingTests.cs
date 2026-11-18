using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Where the city's block dressing stands (<see cref="CityDressing"/>). Each shipped floor variant's slots lie on its free
/// ground. A block stands the slots of the variant <see cref="CityGround.Pick"/> gives its cell.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class CityDressingTests
{
    private static readonly string Folder = Path.Combine(TestData.RepoRoot, "CSVM", "data", "city_models", "C5");

    private static string GroundJson => File.ReadAllText(Path.Combine(Folder, "ground_layout.json"));

    private static CityGround.PaintLayout Cblock7 => CityGround.Layout.Parse(GroundJson).Paints["cblock7"];

    /// <summary>Every slot of every shipped variant stands in a layout cell, off the streets and outside every lot drawn
    /// again a tile over. A tree stands a crown clear of the lots, and a run stretches by at most a quarter.</summary>
    [Fact]
    public void ShippedSlotsStandOnFreeGround()
    {
        var files = Directory.GetFiles(Path.Combine(Folder, "ground"), "cblock7-*.json");
        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            string set = Path.GetFileNameWithoutExtension(file).Split('-')[1];
            var layout = CityDressing.ParseVariant(File.ReadAllText(file));
            var lots = CityDressing.Lots(GroundJson, set);
            var slots = CityDressing.Slots(layout, lots, Cblock7);
            var kinds = slots.Select(s => s.Kind).ToHashSet();
            Assert.True(new[] { "gd_fence", "gd_wall", "gd_hedge", "gd_sedan", "gd_ashcans", "gd_bench", CityDressing.Tree }.All(kinds.Contains),
                $"{file}: kinds {string.Join(", ", kinds)}");
            foreach (var slot in slots)
            {
                Assert.True(layout.Cells.Any(c => c.Rect.HasPoint(slot.At)), $"{file}: {slot.Kind} at {slot.At} in no cell");
                Assert.False(OnStreet(slot.At), $"{file}: {slot.Kind} at {slot.At} on a street");
                Assert.False(UnderLot(lots, slot.At), $"{file}: {slot.Kind} at {slot.At} under a lot");
                Assert.InRange(slot.Stretch, 0.5f, 1.25f);
                Assert.InRange(slot.Keep, 0.01f, 1f);
                if (slot.Kind == CityDressing.Tree)
                {
                    for (int a = 0; a < 16; a++)
                    {
                        var round = slot.At + (new Vector2(MathF.Cos(a * MathF.Tau / 16f), MathF.Sin(a * MathF.Tau / 16f)) * (5.4f / 256f));
                        Assert.False(UnderLot(lots, round), $"{file}: tree at {slot.At} within a crown of a lot");
                    }
                }
            }
        }
    }

    /// <summary>On a polygon over many blocks, at an offset UV, each instance carries the variant the shader's pick gives
    /// the cell under it. Every block stands only its own variant's slot.</summary>
    [Fact]
    public void EachBlockStandsTheVariantTheFloorDraws()
    {
        var spec = Cblock7;
        // Four variants, each one slot at the layout frame's middle, kept on every block.
        var variants = Enumerable.Range(0, 4)
            .Select(n => new List<CityDressing.Slot> { new($"v{n}", new Vector2(1f, 1f), 0f, 1f, 0, 1f) })
            .ToList<List<CityDressing.Slot>?>();
        foreach (var offset in new[] { Vector2.Zero, new Vector2(3f, -7f), new Vector2(0.004f, -0.006f) })
        {
            var placed = new List<CityDressing.Placed>();
            CityDressing.Stand(Square(-2560f, -1280f, 2048f, 5f), offset, spec, variants, null, placed);
            Assert.True(placed.Count >= 49, $"offset {offset}: {placed.Count} placed");
            foreach (var p in placed)
            {
                var cell = CityGround.CellOf(spec, p.Transform.Origin.X, p.Transform.Origin.Z);
                Assert.Equal(cell, p.Cell);
                Assert.Equal((int)(CityGround.Pick(cell.I, cell.J) % 4u), p.Variant);
                Assert.Equal($"v{p.Variant}", p.Kind);
                Assert.Equal(5f, p.Transform.Origin.Y, 3);
            }
            Assert.Equal(placed.Count, placed.Select(p => p.Cell).Distinct().Count());
            Assert.True(placed.Select(p => p.Variant).Distinct().Count() == 4, "every variant stands somewhere");
        }
    }

    /// <summary>A slot kept on half the blocks stands on about half of them, the same ones on every run. It stands on
    /// none where the cover refuses its foot.</summary>
    [Fact]
    public void ABlockRollsItsSlotsAndTheCoverRefuses()
    {
        var spec = Cblock7;
        var variants = new List<List<CityDressing.Slot>?> { new() { new("half", new Vector2(1f, 1f), 0f, 1f, 7, 0.5f) } };
        var square = Square(0f, 0f, 2560f, 0f);
        var first = new List<CityDressing.Placed>();
        var again = new List<CityDressing.Placed>();
        CityDressing.Stand(square, Vector2.Zero, spec, variants, null, first);
        CityDressing.Stand(square, Vector2.Zero, spec, variants, null, again);
        Assert.InRange(first.Count, 30, 70);
        Assert.Equal(first.Select(p => p.Cell), again.Select(p => p.Cell));
        var none = new List<CityDressing.Placed>();
        CityDressing.Stand(square, Vector2.Zero, spec, variants, _ => true, none);
        Assert.Empty(none);
    }

    // A flat square from (x, z) of the given edge at height y, as two triangles.
    private static List<Vector3> Square(float x, float z, float edge, float y) => new()
    {
        new(x, y, z), new(x + edge, y, z), new(x + edge, y, z + edge),
        new(x, y, z), new(x + edge, y, z + edge), new(x, y, z + edge),
    };

    private static bool OnStreet(Vector2 p) => Cblock7.Streets.Any(s =>
    {
        float c = s.Axis.Dot(p) - s.Centre;
        return MathF.Abs(c - MathF.Round(c)) <= s.Half;
    });

    private static bool UnderLot(List<Vector2[]> lots, Vector2 p)
    {
        var q = new Vector2(p.X - MathF.Floor(p.X), p.Y - MathF.Floor(p.Y));
        foreach (var poly in lots)
        {
            for (int du = -1; du <= 1; du++)
            {
                for (int dv = -1; dv <= 1; dv++)
                {
                    var r = q - new Vector2(du, dv);
                    var signs = Enumerable.Range(0, poly.Length).Select(i =>
                    {
                        var a = poly[i];
                        var b = poly[(i + 1) % poly.Length];
                        float inside = (((b.X - a.X) * (r.Y - a.Y)) - ((b.Y - a.Y) * (r.X - a.X))) / (b - a).Length();
                        return MathF.Abs(inside) < 0.03f / 256f ? 0 : MathF.Sign(inside);
                    }).Distinct().ToList();
                    if (signs.Count == 1 && signs[0] != 0)
                        return true;
                }
            }
        }
        return false;
    }
}
