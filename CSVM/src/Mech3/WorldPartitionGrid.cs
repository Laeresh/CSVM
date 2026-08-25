using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The world's spatial partition grid, answering one question the flat
/// <see cref="GameZNode.PartitionNodes"/> list cannot: which nodes does a world-space XZ rectangle
/// cover? That is what the mission script's <c>WorldPartitionSetActive</c> verb selects by, and the
/// only reader of this class. The grid is the gamez's own cell table, in the file's own order,
/// which is the order the engine indexes it by. Decode: <c>docs/formats/interp.md</c>,
/// "<c>WorldPartitionSetActive</c> — <c>NodeSetActive</c>, selected by area".
/// </summary>
public sealed class WorldPartitionGrid
{
    private readonly List<List<int>> _cells;
    private readonly float _originX, _originZ, _cellX, _cellZ;

    private WorldPartitionGrid(GameZNode world)
    {
        _cells = world.PartitionCellNodes!;
        _originX = world.PartitionOriginX;
        _originZ = world.PartitionOriginZ;
        _cellX = world.PartitionCellX;
        _cellZ = world.PartitionCellZ;
        Cols = world.PartitionCols;
        Rows = world.PartitionRows;
    }

    /// <summary>Cells across the x axis, one per column of the file's rows.</summary>
    public int Cols { get; }

    /// <summary>Rows of cells, the file's outer array. Row 0 is the HIGH z edge.</summary>
    public int Rows { get; }

    /// <summary>The grid of a built world node, or null when the extraction carries no usable cell
    /// table (a legacy extraction, or a world with no partitions at all). Null means the area verb
    /// has nothing to select from and is reported unapplied rather than guessed at.</summary>
    public static WorldPartitionGrid? Of(GameZNode? world)
    {
        if (world == null || world.PartitionCellNodes == null
            || world.PartitionRows <= 0 || world.PartitionCols <= 0
            || world.PartitionCellX <= 0f || world.PartitionCellZ <= 0f
            || world.PartitionCellNodes.Count != world.PartitionRows * world.PartitionCols)
        {
            return null;
        }

        return new WorldPartitionGrid(world);
    }

    /// <summary>The distinct gamez node indices the rectangle's cells hold, in cell order. Corner
    /// order does not matter: each coordinate becomes a cell index, is clamped into the grid, and
    /// the two are then sorted. ⚠ The rectangle is HALF-OPEN in cell space, so the maximum row and
    /// column are excluded and a rectangle inside one cell selects nothing.</summary>
    public IReadOnlyList<int> NodesIn(float x1, float z1, float x2, float z2)
    {
        var (colMin, colMax) = Span(CellX(x1), CellX(x2), Cols);
        var (rowMin, rowMax) = Span(CellZ(z1), CellZ(z2), Rows);
        var found = new List<int>();
        var seen = new HashSet<int>();
        for (int row = rowMin; row < rowMax; row++)
        {
            for (int col = colMin; col < colMax; col++)
            {
                foreach (int idx in _cells[(row * Cols) + col])
                {
                    if (seen.Add(idx))
                    {
                        found.Add(idx);
                    }
                }
            }
        }

        return found;
    }

    /// <summary>The half-open cell span a rectangle covers, as
    /// <c>(colMin, colMax, rowMin, rowMax)</c>. Exposed so a test can pin the index maths itself
    /// rather than only the node set it produces.</summary>
    public (int ColMin, int ColMax, int RowMin, int RowMax) CellsIn(
        float x1, float z1, float x2, float z2)
    {
        var (colMin, colMax) = Span(CellX(x1), CellX(x2), Cols);
        var (rowMin, rowMax) = Span(CellZ(z1), CellZ(z2), Rows);
        return (colMin, colMax, rowMin, rowMax);
    }

    private static (int Min, int Max) Span(int a, int b, int count)
    {
        int min = Mathf.Clamp(Mathf.Min(a, b), 0, count - 1);
        int max = Mathf.Clamp(Mathf.Max(a, b), 0, count - 1);
        return (min, max);
    }

    private int CellX(float x) => Mathf.FloorToInt((x - _originX) / _cellX);

    // The z axis runs the other way: its origin is the grid's HIGH edge and its authored cell size
    // is negative, so row 0 is the cell nearest z = origin and the index grows as z falls.
    private int CellZ(float z) => Mathf.FloorToInt((_originZ - z) / _cellZ);
}
