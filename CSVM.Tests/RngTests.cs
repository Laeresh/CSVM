using System.Collections.Generic;
using System.Numerics;
using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="Rng"/>'s per-CELL generator (<see cref="Rng.NewSystemRandom(string, int, int)"/>),
/// serving the map-edge continuation: a field extended past its authored bounds needs a seed keyed
/// by coordinate rather than by draw order, since the set of cells it covers is a runtime
/// computation. These pin the two properties that matter, deterministic per cell and independent of
/// every other cell, off-engine. They can be pinned here at all because this overload takes no
/// native Godot object, unlike <c>Rng.Reset</c> and the shared per-subsystem stream.
/// <see cref="Rng.SortieSeed"/>, the per-flight master step, is pure arithmetic for the same reason.
/// </summary>
public class RngTests
{
    [Fact]
    public void TheSameCellGivesTheSameSequenceEveryTime()
    {
        var a = Rng.NewSystemRandom(Rng.Clouds, 7, -3);
        var b = Rng.NewSystemRandom(Rng.Clouds, 7, -3);

        // Not "same seed", the same DRAWS: the property --det actually depends on.
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(a.NextDouble(), b.NextDouble());
        }
    }

    [Fact]
    public void DifferentCellsGiveDifferentSequences()
    {
        var here = Rng.NewSystemRandom(Rng.Clouds, 7, -3);
        var next = Rng.NewSystemRandom(Rng.Clouds, 8, -3);

        Assert.NotEqual(here.NextDouble(), next.NextDouble());
    }

    [Fact]
    public void TheSameCoordinatesOnADifferentSubsystemGiveADifferentSequence()
    {
        // Reusing (cellX, cellZ) as the ONLY key would let two unrelated subsystems collide on the
        // same coordinate pair; the subsystem name must still separate them, same as the plain
        // per-subsystem stream does.
        var clouds = Rng.NewSystemRandom(Rng.Clouds, 7, -3);
        var precip = Rng.NewSystemRandom(Rng.Precip, 7, -3);

        Assert.NotEqual(clouds.NextDouble(), precip.NextDouble());
    }

    [Fact]
    public void ASortieSeedIsAPureFunctionOfTheProcessSeedAndTheSortie()
    {
        // The whole replay story rests on this: the logged process seed plus "the third flight"
        // must reconstruct that mission, on any machine and at any point in the run.
        Assert.Equal(Rng.SortieSeed(0xC0FFEE, 3), Rng.SortieSeed(0xC0FFEE, 3));
        Assert.NotEqual(Rng.SortieSeed(0xC0FFEE, 3), Rng.SortieSeed(0xC0FFEF, 3));
    }

    [Fact]
    public void SuccessiveSortiesGiveDistinctMasters()
    {
        var seen = new HashSet<ulong>();
        for (int sortie = 1; sortie <= 64; sortie++)
        {
            Assert.True(seen.Add(Rng.SortieSeed(4242, sortie)), $"sortie {sortie} repeated a master");
        }
    }

    [Fact]
    public void ConsecutiveSortiesAreWellSeparated()
    {
        // The step is +1 through a mixer, not a bare +1: two masters a bit apart would hand every
        // subsystem a pair of near-identical seeds, and back-to-back flights would look alike in
        // exactly the way this change exists to prevent.
        for (int sortie = 1; sortie <= 16; sortie++)
        {
            int differing = BitOperations.PopCount(
                Rng.SortieSeed(4242, sortie) ^ Rng.SortieSeed(4242, sortie + 1));
            Assert.InRange(differing, 16, 48);
        }
    }
}
