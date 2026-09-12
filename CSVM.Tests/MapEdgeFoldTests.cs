using System.Collections.Generic;
using CSVM.Mech3;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The map-edge continuation's per-axis fold (<see cref="MapEdgeExtender.FoldAxis"/>): which source
/// cell an out-of-map index maps to, and whether that copy is reflected.
///
/// <para><see cref="BlockOfOneMirrorIsTheHistoricalClamp"/> pins the generalization against the
/// closed form it generalizes, rather than against hand-copied expectations. That equivalence is
/// not the shipping path, the original <b>repeats</b> rather than mirrors, A/B'd at the controls,
/// but it stays as the fold's algebraic anchor: mirror at block 1 is the one case with an
/// independent reference implementation.</para>
/// </summary>
public class MapEdgeFoldTests
{
    private const int Grid = 12; // C1/C2/C4/C5 are all 12x12 x 1024 m (docs/formats/world-structure.md)

    // ---- block=1, mirror: bit-identical to the closed-form clamp, on both edges and past them ----

    [Fact]
    public void BlockOfOneMirrorIsTheHistoricalClamp()
    {
        // Five map widths either side, far enough that any period error would have drifted.
        for (int i = -5 * Grid; i < 6 * Grid; i++)
        {
            Assert.Equal(HistoricalClamp(i, Grid), MapEdgeExtender.FoldAxis(i, Grid, 1, repeat: false));
        }
    }

    // ---- in-map indices are never folded, whatever the block depth or mode ----

    [Theory]
    [InlineData(1, false)]
    [InlineData(3, false)]
    [InlineData(Grid, false)]
    [InlineData(3, true)]
    public void InMapIndicesPassThroughUnflipped(int block, bool repeat)
    {
        for (int i = 0; i < Grid; i++)
        {
            Assert.Equal((i, false), MapEdgeExtender.FoldAxis(i, Grid, block, repeat));
        }
    }

    // ---- mirror, block=3: the block runs out reversed, then forward, then reversed ----

    [Fact]
    public void MirrorWalksTheBlockBackwardsThenForwards()
    {
        // Past the high edge of a 12-cell axis, the block is cells 9..11.
        (int Src, bool Flip)[] expected =
        {
            (11, true), (10, true), (9, true),    // first period: reflected, so nearest-first
            (9, false), (10, false), (11, false), // second: a straight copy of the block
            (11, true), (10, true), (9, true),    // and back to reflected
        };
        for (int k = 0; k < expected.Length; k++)
        {
            Assert.Equal(expected[k], MapEdgeExtender.FoldAxis(Grid + k, Grid, 3, repeat: false));
        }
    }

    [Fact]
    public void MirrorFoldsTheLowEdgeSymmetrically()
    {
        // Before the low edge the block is cells 0..2, and index -1 is the cell adjacent to 0.
        (int Src, bool Flip)[] expected =
        {
            (0, true), (1, true), (2, true),
            (2, false), (1, false), (0, false),
        };
        for (int k = 0; k < expected.Length; k++)
        {
            Assert.Equal(expected[k], MapEdgeExtender.FoldAxis(-1 - k, Grid, 3, repeat: false));
        }
    }

    // ---- every seam is a shared mirror plane: the two cells either side of it are the same
    // source cell, which is exactly what makes heights match across it ----

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void EverySeamRepeatsItsSourceCellAcrossTheBoundary(int block)
    {
        // Seams sit every `block` cells starting at the map edge. Straddle several of them.
        for (int seam = 0; seam < 6; seam++)
        {
            int at = Grid + (seam * block);
            var inner = MapEdgeExtender.FoldAxis(at - 1, Grid, block, repeat: false);
            var outer = MapEdgeExtender.FoldAxis(at, Grid, block, repeat: false);
            Assert.Equal(inner.Src, outer.Src);
            Assert.NotEqual(inner.Flip, outer.Flip);
        }
    }

    // ---- repeat: the same block, translated, never reflected, so seams step ----

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    public void RepeatNeverFlipsAndCyclesForward(int block)
    {
        for (int k = 0; k < 3 * block; k++)
        {
            var (src, flip) = MapEdgeExtender.FoldAxis(Grid + k, Grid, block, repeat: true);
            Assert.False(flip);
            Assert.Equal(Grid - block + (k % block), src);
        }
    }

    [Fact]
    public void RepeatTilesTheLowEdgeContinuously()
    {
        // Reading outward from the map, block 0..2 tiles as ... 0,1,2 | 0,1,2 | [map].
        int[] expected = { 2, 1, 0, 2, 1, 0 };
        for (int k = 0; k < expected.Length; k++)
        {
            Assert.Equal((expected[k], false), MapEdgeExtender.FoldAxis(-1 - k, Grid, 3, repeat: true));
        }
    }

    // ---- the block is clamped to the grid, so a caller may pass anything ----

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void BlockBelowOneClampsToOne(int block)
    {
        for (int i = -Grid; i < 2 * Grid; i++)
        {
            Assert.Equal(MapEdgeExtender.FoldAxis(i, Grid, 1, repeat: false),
                MapEdgeExtender.FoldAxis(i, Grid, block, repeat: false));
        }
    }

    [Fact]
    public void BlockWiderThanTheGridClampsToWholeMapMirroring()
    {
        for (int i = -Grid; i < 2 * Grid; i++)
        {
            Assert.Equal(MapEdgeExtender.FoldAxis(i, Grid, Grid, repeat: false),
                MapEdgeExtender.FoldAxis(i, Grid, 99, repeat: false));
        }
    }

    // ---- whole-map mirroring brings the map interior back, which is what refutes it against
    // the original (the airport never reappears in 10+ minutes of flight past the edge) ----

    [Fact]
    public void WholeMapMirroringReachesEveryInteriorCell()
    {
        var seen = new HashSet<int>();
        for (int i = Grid; i < 3 * Grid; i++)
        {
            seen.Add(MapEdgeExtender.FoldAxis(i, Grid, Grid, repeat: false).Src);
        }
        Assert.Equal(Grid, seen.Count);
    }

    // ---- a block shallower than the map centre never reaches it, which is the bound the
    // airport observation puts on the block depth (the airfield sits >=3 cells from any edge) ----

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ShallowBlocksNeverReachTheMapCentre(int block)
    {
        for (int i = Grid; i < 6 * Grid; i++)
        {
            int src = MapEdgeExtender.FoldAxis(i, Grid, block, repeat: false).Src;
            Assert.True(src >= Grid - block, $"index {i} folded to {src}, inside the map's interior");
        }
    }

    // ---- the overlay's band index: constant across a block, alternating between them ----

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void BandIndexIsConstantAcrossABlockAndStepsBetweenThem(int block)
    {
        // High edge: bands are [n, n+block), [n+block, n+2*block), ...
        for (int band = 1; band <= 4; band++)
        {
            for (int k = 0; k < block; k++)
            {
                Assert.Equal(band, MapEdgeExtender.FoldBandIndex(Grid + ((band - 1) * block) + k, Grid, block));
            }
        }
        // Low edge mirrors that outward, so index -1 is the block adjacent to the map.
        for (int band = 1; band <= 4; band++)
        {
            for (int k = 0; k < block; k++)
            {
                Assert.Equal(-band, MapEdgeExtender.FoldBandIndex(-1 - ((band - 1) * block) - k, Grid, block));
            }
        }
    }

    [Fact]
    public void BandIndexIsZeroInsideTheMap()
    {
        for (int i = 0; i < Grid; i++)
        {
            Assert.Equal(0, MapEdgeExtender.FoldBandIndex(i, Grid, 3));
        }
    }

    // Under MIRRORING the band parity and the flip are the same thing, which is what makes keying
    // the overlay on the band a strict generalization rather than a change of appearance.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void BandParityEqualsMirrorFlip(int block)
    {
        for (int i = -4 * Grid; i < 5 * Grid; i++)
        {
            bool flip = MapEdgeExtender.FoldAxis(i, Grid, block, repeat: false).Flip;
            bool oddBand = (MapEdgeExtender.FoldBandIndex(i, Grid, block) & 1) != 0;
            Assert.Equal(flip, oddBand);
        }
    }

    // ---- the per-chapter block depths, A/B'd against the original ----

    [Theory]
    [InlineData("C1", 2)]
    [InlineData("C2", 2)]
    [InlineData("C4", 2)]
    [InlineData("C5", 1)]
    [InlineData("C1B", 1)]
    [InlineData("C1C", 1)]
    [InlineData("C2B", 1)]
    [InlineData("C3", 1)]
    public void ChapterBlockDepthsAreTheMeasuredOnes(string chapter, int expected)
    {
        Assert.Equal(expected, MapEdgeExtender.DefaultBlockCells(chapter));
        Assert.Equal(expected, MapEdgeExtender.DefaultBlockCells(chapter.ToLowerInvariant()));
    }

    // An unknown or absent chapter must fall back to 1, right or indistinguishable everywhere it
    // has been looked at, never to 2, which is only known right where it was measured.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("C9")]
    public void UnknownChapterFallsBackToOne(string? chapter)
    {
        Assert.Equal(1, MapEdgeExtender.DefaultBlockCells(chapter));
    }

    // The implementation this replaced, verbatim: clamp to the border cell, reflect on odd rings.
    private static (int Src, bool Flip) HistoricalClamp(int i, int n)
    {
        if (i >= 0 && i < n)
            return (i, false);
        int edge = i >= n ? n - 1 : 0;
        int ring = i >= n ? i - (n - 1) : -i;
        return (edge, (ring & 1) == 1);
    }
}
