using System.Linq;
using CSVM.Flight.Camera;
using CSVM.Mech3;
using CSVM.Session.World;
using CSVM.Spec;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="StuntRaceDirector.TryCreate"/>'s decision whether a stunt run races, and the window
/// it opens. The race suites cover the rest of the director through the session.
/// </summary>
public class StuntRaceDirectorTests
{
    private const int Zones = 5;

    [Fact]
    public void ARunFlownAloneDoesNotRace()
    {
        var spec = SessionSpec.Parse(new[] { "--stunt" });
        Assert.Null(StuntRaceDirector.TryCreate(spec, Zones, Seats(1), _ => null));
    }

    [Fact]
    public void ASecondSeatRacesOverTheMissionsZonesInTheAskedWindow()
    {
        var spec = SessionSpec.Parse(new[] { "--stunt" });
        var director = StuntRaceDirector.TryCreate(spec, Zones, Seats(2), _ => null);
        Assert.NotNull(director);
        Assert.Equal(Zones, director!.Race.ZoneCount);
        Assert.Equal(InstantActionDef.DefaultRaceWindowMinutes * 60f, director.Race.WindowSeconds);
        Assert.Null(director.Link);
    }

    [Fact]
    public void ALobbyStuntRaceRacesEvenAHostLeftAlone()
    {
        var cli = SessionSpec.Parse(new[] { "--mission=MP2" });
        var spec = SessionSpec.FromMenu(cli, "C1", new[] { "player_bhawk" }, MenuMode.Stunt, vsTimeMinutes: 7,
            missionType: DogfightMissionType.StuntRace);
        var director = StuntRaceDirector.TryCreate(spec, Zones, Seats(1), _ => null);
        Assert.Equal(7 * 60f, director?.Race.WindowSeconds);
    }

    [Fact]
    public void DebugScoreboardClosesTheWindowAtOnce()
    {
        var spec = SessionSpec.Parse(new[] { "--stunt", "--debug-scoreboard" });
        Assert.Equal(0f, StuntRaceDirector.TryCreate(spec, Zones, Seats(2), _ => null)?.Race.WindowSeconds);
    }

    [Fact]
    public void TheLinkIsOpenedOnTheDirectorsOwnRace()
    {
        var spec = SessionSpec.Parse(new[] { "--stunt" });
        Flight.Modes.StuntRace? opened = null;
        var director = StuntRaceDirector.TryCreate(spec, Zones, Seats(2), race =>
        {
            opened = race;
            return null;
        });
        Assert.Same(director?.Race, opened);
    }

    private static StuntRaceDirector.Field Seats(int count)
    {
        var rigs = Enumerable.Range(0, count).Select(i => new PlayerRig { Index = i }).ToList();
        return new StuntRaceDirector.Field { SeatRigs = rigs, Panes = rigs };
    }
}
