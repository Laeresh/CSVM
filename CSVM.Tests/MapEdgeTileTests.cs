using System;
using System.Collections.Generic;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The map-edge continuation's ground-tile classifier
/// (<see cref="MapEdgeExtender.ClassifyGroundMesh"/>) and the completion-strip test
/// (<see cref="MapEdgeExtender.IsCompletionStrip"/>) that <c>AdoptComplements</c> is built on.
///
/// <para>Both were extracted from a private <c>IsGroundTile</c> for `BL-316`, which nothing could
/// reach by test: C5's continuation had a void strip through it because three flat water sheets —
/// each COMPLETING a border cell its base tile only partly covered — measure 256–384 m across and
/// so fell under the classifier's 0.4-cell floor. Every figure below is a measurement off
/// <c>--dump-tilegrid</c> on the shipped chapters, not an invention.</para>
/// </summary>
public class MapEdgeTileTests
{
    private const float Cell = 1024f; // C1/C2/C4/C5 and the rest: 1024 m partition cells.

    private static readonly string?[] NoTextures = Array.Empty<string?>();

    // ---- the classifier -------------------------------------------------------------------

    [Fact]
    public void ACellSizedSheetIsAGroundTile()
    {
        Assert.Equal(MapEdgeExtender.TileVerdict.Accepted, Classify(Sheet(Cell, Cell)));
    }

    /// <summary>The split half-tiles are real ground and must stay accepted — they are the whole
    /// reason the floor is 0.4 and not something tighter (C1 bins 147 tiles into 144 cells).</summary>
    [Fact]
    public void ASplitHalfTileIsStillAGroundTile()
    {
        Assert.Equal(MapEdgeExtender.TileVerdict.Accepted, Classify(Sheet(Cell / 2f, Cell)));
    }

    [Fact]
    public void AThinStripIsTooSmall()
    {
        // C5 cell (4,0): 384 x 1024 m of water, 0.375 of a cell across.
        Assert.Equal(MapEdgeExtender.TileVerdict.TooSmall, Classify(Sheet(384f, Cell)));
    }

    [Fact]
    public void AMultiCellSheetIsTooLarge()
    {
        Assert.Equal(MapEdgeExtender.TileVerdict.TooLarge, Classify(Sheet(2f * Cell, Cell)));
    }

    [Fact]
    public void AnEmptyMeshIsNotACandidate()
    {
        Assert.Equal(
            MapEdgeExtender.TileVerdict.NoMesh,
            MapEdgeExtender.ClassifyGroundMesh(
                Array.Empty<Vector3>(), Array.Empty<string?>(), Cell, Cell, out _, out _));
    }

    /// <summary>The cloudlayer deck tiles are cell-sized too, so only the texture tells them from
    /// ground — and the check runs before the span gate, so a cell-sized cloud sheet is rejected as
    /// sky rather than accepted as terrain.</summary>
    [Fact]
    public void ACellSizedCloudSheetIsRejectedAsSky()
    {
        Assert.Equal(
            MapEdgeExtender.TileVerdict.SkyOrCloud,
            MapEdgeExtender.ClassifyGroundMesh(
                Sheet(Cell, Cell), new string?[] { "cloudlayer1.tif" }, Cell, Cell, out _, out _));
    }

    /// <summary>The AABB and centroid are reported even when the verdict is a rejection — the
    /// census depends on it, and so does <c>AdoptComplements</c>, which only ever sees rejects.</summary>
    [Fact]
    public void ARejectedCandidateStillReportsItsFootprint()
    {
        MapEdgeExtender.ClassifyGroundMesh(
            Sheet(384f, Cell), NoTextures, Cell, Cell, out var center, out var span);
        Assert.Equal(384f, span.X, 3);
        Assert.Equal(Cell, span.Z, 3);
        Assert.Equal(0f, center.X, 3);
    }

    // ---- the completion-strip test --------------------------------------------------------

    /// <summary>C5's three void-causing strips, as measured. Each is perfectly flat and runs the
    /// full 1024 m of its cell on one axis while covering only a quarter to a third on the
    /// other.</summary>
    [Theory]
    [InlineData(384f, 0f, 1024f)]  // g4592, cell (4,0)
    [InlineData(1024f, 0f, 320f)]  // g4611, cell (15,2)
    [InlineData(1024f, 0f, 256f)]  // g4667, cell (0,11)
    public void TheMeasuredC5StripsAreCompletionStrips(float x, float y, float z)
    {
        Assert.True(MapEdgeExtender.IsCompletionStrip(new Vector3(x, y, z), Cell, Cell));
    }

    /// <summary>⚠ The regression this test exists for. Flatness ALONE adopted 89 nodes on C5 —
    /// hangar floors, city-block rooftops, wreck debris — because a building floor is flat too, and
    /// <c>cblock*</c> (the city GROUND texture) classifies as <c>buildings</c>, so the surface class
    /// cannot separate them either. The full-cell span is what does.</summary>
    [Theory]
    [InlineData(256f, 8f, 256f)]      // a hangar floor
    [InlineData(0.6f * 1024f, 2f, 0.4f * 1024f)]  // the widest building part measured, still short
    public void AFlatButShortBuildingPartIsNotACompletionStrip(float x, float y, float z)
    {
        Assert.False(MapEdgeExtender.IsCompletionStrip(new Vector3(x, y, z), Cell, Cell));
    }

    /// <summary>An upright object spanning a full cell in ONE axis — a fence line, a pier wall —
    /// is not a strip of ground. `ap_lightpole.flt` is the degenerate case: tens of metres tall on
    /// a footprint of centimetres.</summary>
    [Theory]
    [InlineData(1024f, 40f, 2f)]
    [InlineData(0.001f, 30f, 0f)]
    public void AnUprightObjectIsNotACompletionStrip(float x, float y, float z)
    {
        Assert.False(MapEdgeExtender.IsCompletionStrip(new Vector3(x, y, z), Cell, Cell));
    }

    /// <summary>A marker gizmo is 3 vertices with no extent at all; a strictly-flatter-than-wide
    /// test must not read 0 &lt; 0 as flat.</summary>
    [Fact]
    public void ADegenerateMeshIsNotACompletionStrip()
    {
        Assert.False(MapEdgeExtender.IsCompletionStrip(Vector3.Zero, Cell, Cell));
    }

    // ---- helpers --------------------------------------------------------------------------

    // A flat horizontal quad of the given extents, centred on its own origin — the shape every
    // ground tile in the install has.
    private static List<Vector3> Sheet(float x, float z) => new()
    {
        new Vector3(-x / 2f, 0f, -z / 2f),
        new Vector3(x / 2f, 0f, -z / 2f),
        new Vector3(x / 2f, 0f, z / 2f),
        new Vector3(-x / 2f, 0f, z / 2f),
    };

    private static MapEdgeExtender.TileVerdict Classify(List<Vector3> vertices) =>
        MapEdgeExtender.ClassifyGroundMesh(vertices, NoTextures, Cell, Cell, out _, out _);
}
