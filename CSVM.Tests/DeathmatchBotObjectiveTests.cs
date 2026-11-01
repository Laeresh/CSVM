using System;
using CSVM.Flight.Ai;
using Godot;
using Xunit;

namespace CSVM.Tests;

public class DeathmatchBotObjectiveTests
{
    private const int Own = 2;

    [Fact]
    public void TakesTheNearestLiveEnemyAtAnyRange()
    {
        var planes = new[]
        {
            new RivalPlane(Vector3.Zero, Own, true),                     // the bot itself
            new RivalPlane(new Vector3(0, 0, 50000), 3, true),
            new RivalPlane(new Vector3(0, 900, 30000), 4, true),
        };
        Assert.Equal(2, DeathmatchBotObjective.Nearest(Vector3.Zero, Own, planes));
    }

    [Fact]
    public void SkipsTheDeadItsOwnSideAndNeutrals()
    {
        var planes = new[]
        {
            new RivalPlane(new Vector3(0, 0, 100), 3, false),           // down
            new RivalPlane(new Vector3(0, 0, 200), Own, true),           // a wingman
            new RivalPlane(new Vector3(0, 0, 300), 0, true),             // neutral
            new RivalPlane(new Vector3(0, 0, 9000), 3, true),
        };
        Assert.Equal(3, DeathmatchBotObjective.Nearest(Vector3.Zero, Own, planes));

        // ABLE-TO-FAIL CONTROL: with the one live enemy gone there is nothing to fly at.
        Assert.Equal(-1, DeathmatchBotObjective.Nearest(Vector3.Zero, Own, planes[..3]));
        Assert.Equal(-1, DeathmatchBotObjective.Nearest(Vector3.Zero, Own, Array.Empty<RivalPlane>()));
    }
}
