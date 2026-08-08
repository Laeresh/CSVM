using CSVM.Utils;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="Rng"/>'s per-CELL generator (<see cref="Rng.NewSystemRandom(string, int, int)"/>),
/// added for A5's map-edge continuation (docs/PLAN-overcast-match.md): a field extended past its
/// authored bounds needs a seed keyed by coordinate rather than by draw order, since the set of
/// cells it covers is itself a runtime computation (bounded by each kind's <c>far_fade</c>), not a
/// fixed walk over authored volumes. These pin the two properties that guarantee matters here —
/// deterministic per cell, and independent of every other cell/subsystem — off-engine, without
/// building a whole cloud field or touching <c>Rng.Reset</c>/the shared per-subsystem stream
/// (both go through native Godot objects — <c>GD.Seed</c>, <c>Godot.RandomNumberGenerator</c> —
/// that only run inside the engine; this overload deliberately does not, which is why it can be
/// pinned here at all).
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
}
