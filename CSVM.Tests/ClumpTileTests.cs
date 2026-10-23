using System;
using System.Collections.Generic;
using System.Text;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Where a species' clump tile comes from (<see cref="ClumpTile"/>): a model pack, else the shipped file
/// beside the model, else none. Image, sidecar and side map come from one source. The sidecar's checks.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class ClumpTileTests
{
    private const string Sidecar = "{\"width_m\": 24, \"trees\": [3, 2, 4]}";

    /// <summary>The pack beats the shipped files, the shipped files beat none, and the side map comes from the
    /// source that gave the image.</summary>
    [Fact]
    public void APackBeatsTheShippedTileWhichBeatsComposition()
    {
        var pack = Source("pack", ("C1/firtree1.clump.png", "pack png"), ("C1/firtree1.clump.json", "{\"width_m\": 12, \"trees\": [2]}"));
        var shipped = Source("res", ("C1/firtree1.clump.png", "res png"), ("C1/firtree1.clump.json", Sidecar),
            ("C1/firtree1.clump.side.png", "res side"));

        var fromPack = ClumpTile.Resolve("C1", "firtree1", pack, shipped)!;
        Assert.Equal("pack:C1/firtree1.clump.png", fromPack.Source);
        Assert.Equal("pack png", Encoding.UTF8.GetString(fromPack.Image));
        Assert.Null(fromPack.Side);
        Assert.Equal(12f, fromPack.WidthM);
        Assert.Equal(new[] { 2 }, fromPack.Trees);

        var fromShipped = ClumpTile.Resolve("C1", "firtree1", Source("pack"), shipped)!;
        Assert.Equal("res:C1/firtree1.clump.png", fromShipped.Source);
        Assert.Equal("res side", Encoding.UTF8.GetString(fromShipped.Side!));
        Assert.Equal(new[] { 3, 2, 4 }, fromShipped.Trees);

        Assert.Null(ClumpTile.Resolve("C1", "firtree2", pack, shipped));
        Assert.Null(ClumpTile.Resolve("C4", "firtree1", pack, shipped));
    }

    /// <summary>A pack with only one of image and sidecar, or with a bad sidecar, keeps the shipped tile. The
    /// two sources never mix.</summary>
    [Fact]
    public void ABrokenReplacementKeepsTheShippedTile()
    {
        var shipped = Source("res", ("C1/firtree1.clump.png", "res png"), ("C1/firtree1.clump.json", Sidecar));
        foreach (var pack in new[]
        {
            Source("pack", ("C1/firtree1.clump.png", "pack png")),
            Source("pack", ("C1/firtree1.clump.json", Sidecar)),
            Source("pack", ("C1/firtree1.clump.png", "pack png"), ("C1/firtree1.clump.json", "{\"trees\": [3]}")),
        })
        {
            Assert.Equal("res:C1/firtree1.clump.png", ClumpTile.Resolve("C1", "firtree1", pack, shipped)!.Source);
        }
    }

    /// <summary>The sidecar takes a width and a count per tile, prose under <c>_</c> keys, and fails loudly on
    /// anything else, naming its source.</summary>
    [Theory]
    [InlineData("not json", "not readable JSON")]
    [InlineData("[1]", "not a JSON object")]
    [InlineData("{\"trees\": [3]}", "width_m is missing")]
    [InlineData("{\"width_m\": 24}", "trees is missing")]
    [InlineData("{\"width_m\": 0, \"trees\": [3]}", "width_m must be")]
    [InlineData("{\"width_m\": 25, \"trees\": [3]}", "width_m must be")]
    [InlineData("{\"width_m\": \"24\", \"trees\": [3]}", "width_m must be")]
    [InlineData("{\"width_m\": 24, \"trees\": []}", "trees must be")]
    [InlineData("{\"width_m\": 24, \"trees\": 3}", "trees must be")]
    [InlineData("{\"width_m\": 24, \"trees\": [3, 0]}", "trees holds 0")]
    [InlineData("{\"width_m\": 24, \"trees\": [2.5]}", "trees holds 2.5")]
    [InlineData("{\"width_m\": 24, \"trees\": [3], \"height_m\": 27}", "unknown key \"height_m\"")]
    public void ABadSidecarFailsWithItsReason(string json, string reason)
    {
        var e = Assert.Throws<FormatException>(() => ClumpTile.ParseSidecar(Encoding.UTF8.GetBytes(json), "x.clump.json"));
        Assert.StartsWith("x.clump.json: ", e.Message);
        Assert.Contains(reason, e.Message);
    }

    /// <summary>A good sidecar reads back as written; a key starting <c>_</c> is prose.</summary>
    [Fact]
    public void AGoodSidecarReads()
    {
        var (width, trees) = ClumpTile.ParseSidecar(Encoding.UTF8.GetBytes("{\"_about\": \"fir\", \"width_m\": 20.5, \"trees\": [4, 2]}"), "x");
        Assert.Equal(20.5f, width);
        Assert.Equal(new[] { 4, 2 }, trees);
    }

    // A source holding the given files, labelling each as name:path.
    private static Func<string, (byte[] Bytes, string Source)?> Source(string name, params (string Path, string Body)[] files)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, body) in files)
            map[path] = body;
        return path => map.TryGetValue(path, out var body) ? (Encoding.UTF8.GetBytes(body), $"{name}:{path}") : null;
    }
}
