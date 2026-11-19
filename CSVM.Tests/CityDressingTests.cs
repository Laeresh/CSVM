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
        var files = Directory.GetFiles(Path.Combine(Folder, "ground"), "cblock*-*.json");
        // The diagonal strips, and cblock4's set standing across cblock2's street (Decision 28).
        foreach (string combination in new[] { "cblock2-", "cblock2-cblock4-" })
            Assert.Contains(files, f => Path.GetFileName(f).StartsWith(combination, StringComparison.Ordinal));
        foreach (string file in files)
        {
            string[] combination = Path.GetFileNameWithoutExtension(file).Split('-');
            var spec = CityGround.Layout.Parse(GroundJson).Paints[combination[0]];
            var layout = CityDressing.ParseVariant(File.ReadAllText(file));
            var lots = CityDressing.Lots(GroundJson, combination[1]);
            var slots = CityDressing.Slots(layout, lots, spec);
            var kinds = slots.Select(s => s.Kind).ToHashSet();
            Assert.True(new[] { "gd_fence", "gd_hedge", "gd_sedan", "gd_ashcans", CityDressing.Tree }.All(kinds.Contains),
                $"{file}: kinds {string.Join(", ", kinds)}");
            foreach (var slot in slots)
            {
                Assert.True(InCell(layout, slot.At), $"{file}: {slot.Kind} at {slot.At} in no cell");
                Assert.False(OnStreet(spec, slot.At), $"{file}: {slot.Kind} at {slot.At} on a street");
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

    /// <summary>Open ground stands no building, so its dressing holds nothing that reads as one: no wall and no shed. Its
    /// parks stand trees and benches, and its parking lots cars.</summary>
    [Fact]
    public void OpenGroundStandsNothingBuildingLike()
    {
        var files = Directory.GetFiles(Path.Combine(Folder, "ground"), "cblock*-none-*.json");
        Assert.Equal(12, files.Length);
        var paints = CityGround.Layout.Parse(GroundJson).Paints;
        foreach (string file in files)
        {
            var layout = CityDressing.ParseVariant(File.ReadAllText(file));
            var kinds = CityDressing.Slots(layout, CityDressing.Lots(GroundJson, "none"), paints[Path.GetFileName(file).Split('-')[0]])
                .Select(s => s.Kind).ToHashSet();
            Assert.False(kinds.Contains("gd_wall") || kinds.Contains("gd_shed"), $"{file}: kinds {string.Join(", ", kinds)}");
            Assert.True(new[] { CityDressing.Tree, "gd_bench", "gd_sedan" }.All(kinds.Contains), $"{file}: kinds {string.Join(", ", kinds)}");
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

    /// <summary>On a diagonal paint every layout frame straddles strips. A slot stands wherever the strip under it draws
    /// its variant. So a frame keeps the slots of every strip it holds, not only of the strip at its middle.</summary>
    [Fact]
    public void AFrameAcrossStripsKeepsEveryStripsSlots()
    {
        var spec = CityGround.Layout.Parse(GroundJson).Paints["cblock2"];
        // Three variants, each the same 8 x 8 grid of slots over the frame, kept on every block.
        var grid = Enumerable.Range(0, 64).Select(i => new Vector2(0.5f + (((i % 8) + 0.5f) / 8f), 0.5f + (((i / 8) + 0.5f) / 8f))).ToList();
        var variants = Enumerable.Range(0, 3)
            .Select(n => grid.Select(at => new CityDressing.Slot($"v{n}", at, 0f, 1f, 0, 1f)).ToList())
            .ToList<List<CityDressing.Slot>?>();
        var placed = new List<CityDressing.Placed>();
        CityDressing.Stand(Square(0f, 0f, 1024f, 0f), Vector2.Zero, spec, variants, null, placed);
        int expected = 0;
        for (int ku = -1; ku <= 4; ku++)
        {
            for (int kv = -1; kv <= 4; kv++)
                expected += grid.Count(g => (g.X + ku) * 256f is >= 0f and <= 1024f && (g.Y + kv) * 256f is >= 0f and <= 1024f);
        }
        Assert.Equal(expected, placed.Count);
        foreach (var p in placed)
        {
            var cell = CityGround.CellOf(spec, p.Transform.Origin.X, p.Transform.Origin.Z);
            Assert.Equal(cell, p.Cell);
            Assert.Equal($"v{CityGround.Pick(cell.I, cell.J) % 3u}", p.Kind);
        }
        Assert.True(placed.Select(p => p.Variant).Distinct().Count() == 3, "every variant stands somewhere");
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

    private static bool OnStreet(CityGround.PaintLayout spec, Vector2 p) => spec.Streets.Any(s =>
    {
        float c = s.Axis.Dot(p) - s.Centre;
        return MathF.Abs(c - MathF.Round(c)) <= s.Half;
    });

    // Whether a slot's UV point, or the same ground a whole tile over, lies in a layout cell, back in the layout's frame.
    private static bool InCell(CityDressing.VariantLayout layout, Vector2 uv)
    {
        for (int du = -3; du <= 3; du++)
        {
            for (int dv = -3; dv <= 3; dv++)
            {
                var q = uv + new Vector2(du, dv);
                var p = layout.Frame is { } f ? new Vector2((q - f.Origin).Dot(f.X), (q - f.Origin).Dot(f.Y)) : q;
                if (layout.Cells.Any(c => c.Rect.HasPoint(p)) && (layout.Frame != null || (du == 0 && dv == 0)))
                    return true;
            }
        }
        return false;
    }

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
