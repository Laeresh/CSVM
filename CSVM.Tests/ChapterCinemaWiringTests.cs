using System;
using System.IO;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The chapter cinema as both presentations' campaigns actually reach it: the roster's CONTINUE and
/// the flight-return door in each, the cabin arriving on the frame the film stops, a position
/// inside a chapter opening the cabin with no film, and one instance serving both so a chapter
/// plays once. A campaign whose feature carries no cinema opens the cabin exactly as it always
/// did, which is what every suite and every golden gets.
/// </summary>
[Trait("Tier", "Quick")]
public class ChapterCinemaWiringTests : IDisposable
{
    private static readonly MenuCommands Accept = new() { Accept = true };

    private readonly string _dir;
    private readonly CampaignProfileStore _store;
    private readonly CustomPlaneStore _planes;

    public ChapterCinemaWiringTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "csvm-chapter-cinema-" + Guid.NewGuid().ToString("N"));
        _store = new CampaignProfileStore(Path.Combine(_dir, "Profiles"));
        _planes = new CustomPlaneStore(Path.Combine(_dir, "Planes"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void BuiltInsContinuePlaysTheChapterFilmAndTheCabinFollowsIt()
    {
        var cinema = new Recorder();
        var flow = Flow(cinema);

        flow.Accept();          // arm the name field
        flow.Type("Zachary");
        flow.Accept();          // CONTINUE

        Assert.Equal("chap1", cinema.Name);
        Assert.Equal(CampaignScreen.Roster, flow.Screen);
        cinema.Stop();
        Assert.Equal(CampaignScreen.Cabin, flow.Screen);
    }

    [Fact]
    public void BuiltInsFlightReturnDoorPlaysTheFilmOfTheChapterTheProfileHasReached()
    {
        var cinema = new Recorder();
        var flow = Flow(cinema);

        flow.SelectProfile(Progressed("Zachary", 5));

        Assert.Equal("chap2", cinema.Name);
        Assert.Equal(CampaignScreen.Roster, flow.Screen);
        cinema.Stop();
        Assert.Equal(CampaignScreen.Cabin, flow.Screen);
    }

    // The negative control the plan names, off engine: --menu=campaign-cabin seats a profile three
    // missions in, which is a position inside a chapter and plays nothing at all.
    [Fact]
    public void APositionInsideAChapterOpensBuiltInsCabinWithNoFilm()
    {
        var cinema = new Recorder();
        var flow = Flow(cinema);

        flow.SelectProfile(Progressed("Zachary", 3));

        Assert.Equal(0, cinema.Plays);
        Assert.Equal(CampaignScreen.Cabin, flow.Screen);
    }

    // What every engine suite and every golden gets: nothing hands those a cinema, so the cabin
    // door is the plain one it was before the wiring.
    [Fact]
    public void ACampaignWithNoCinemaOpensTheCabinStraightAway()
    {
        var feature = new CampaignFeature(UiStrings.Empty, Airframe);
        feature.Open(_store, _planes, null, null);
        var flow = new CampaignFlow(feature);

        flow.SelectProfile(CampaignProfileDef.NewProfile("Zachary"));

        Assert.Null(feature.ChapterCinema);
        Assert.Equal(CampaignScreen.Cabin, flow.Screen);
    }

    [Fact]
    public void OriginalsContinuePlaysTheChapterFilmAndTheCabinFollowsIt()
    {
        var cinema = new Recorder();
        var shell = Shell(cinema, out _);
        OpenCampaign(shell);

        shell.Step(new MenuCommands { Typed = "Zachary" });
        shell.Step(Accept);

        Assert.Equal("chap1", cinema.Name);
        Assert.Equal(OriginalScreen.CampaignRoster, shell.Screen);
        cinema.Stop();
        Assert.Equal(OriginalScreen.CampaignCabin, shell.Screen);
    }

    [Fact]
    public void OriginalsFlightReturnDoorPlaysTheFilmOfTheChapterTheProfileHasReached()
    {
        _store.Save(Progressed("Zachary", 10));
        var cinema = new Recorder();
        var shell = Shell(cinema, out _);
        shell.OpenCampaignOver(_store);

        Assert.True(shell.ShowCabin("Zachary"));

        Assert.Equal("chap3", cinema.Name);
        Assert.Equal(OriginalScreen.CampaignRoster, shell.Screen);
        cinema.Stop();
        Assert.Equal(OriginalScreen.CampaignCabin, shell.Screen);
    }

    [Fact]
    public void APositionInsideAChapterOpensOriginalsCabinWithNoFilm()
    {
        _store.Save(Progressed("Zachary", 3));
        var cinema = new Recorder();
        var shell = Shell(cinema, out _);
        shell.OpenCampaignOver(_store);

        Assert.True(shell.ShowCabin("Zachary"));

        Assert.Equal(0, cinema.Plays);
        Assert.Equal(OriginalScreen.CampaignCabin, shell.Screen);
    }

    // The decision the wiring owns: one instance for the process, so the chapter a player has
    // already watched does not play again when they leave the campaign and come back, whichever
    // presentation they come back through.
    [Fact]
    public void OneInstanceServesBothPresentationsAndAChapterPlaysOnce()
    {
        var cinema = new Recorder();
        var chapters = new ChapterCinema(cinema.Play);
        var feature = new CampaignFeature(UiStrings.Empty, Airframe, chapters);
        feature.Open(_store, _planes, null, null);
        var flow = new CampaignFlow(feature);
        flow.SelectProfile(Progressed("Zachary", 0));
        cinema.Stop();
        Assert.Equal(1, cinema.Plays);

        var shell = new OriginalShell(MenuLayoutReaderTests.OriginalLayout(), new FreeFlightFeature(), Setup(), Measure,
            planes: _planes, campaign: feature, profiles: () => _store);
        _store.Save(Progressed("Zachary", 0));
        shell.OpenCampaignOver(_store);
        Assert.True(shell.ShowCabin("Zachary"));

        Assert.Equal(1, cinema.Plays);
        Assert.Equal(OriginalScreen.CampaignCabin, shell.Screen);
    }

    private static string Airframe(int airframe) => $"node{airframe}";

    private static CampaignProfileDef Progressed(string name, int flown)
    {
        var profile = CampaignProfileDef.NewProfile(name);
        for (int seq = 0; seq < flown; seq++)
        {
            CampaignProgression.Record(profile, new MissionAttempt(
                seq, CampaignProgression.PrimaryObjectiveMask, 300_000, 400, 120, 5, "Gypsy Magic"));
        }

        return profile;
    }

    private static void OpenCampaign(OriginalShell shell)
    {
        var row = shell.Rows.Single(r => r.Key == OriginalShell.CampaignKey);
        shell.Step(new MenuCommands { Pointer = new MenuPointer(row.X + 2f, row.Y + 2f, true, true) });
    }

    private static PlayerSetupFeature Setup()
    {
        var setup = new PlayerSetupFeature();
        setup.SetRoster(OriginalPresentation.Roster(Array.Empty<CustomPlaneDef>()));
        setup.Join(new ScriptedMenuSeat());
        return setup;
    }

    // The fixture's strips, as OriginalCampaignTests measures them.
    private static (int Width, int Height)? Measure(string art) => art switch
    {
        "PM_B_Paper.png" => (160, 112),
        _ when art.StartsWith("PM_B_", StringComparison.Ordinal) => (240, 200),
        _ => null,
    };

    private CampaignFlow Flow(Recorder cinema)
    {
        var feature = new CampaignFeature(UiStrings.Empty, Airframe, new ChapterCinema(cinema.Play));
        feature.Open(_store, _planes, null, null);
        return new CampaignFlow(feature);
    }

    private OriginalShell Shell(Recorder cinema, out CampaignFeature campaign)
    {
        campaign = new CampaignFeature(UiStrings.Empty, Airframe, new ChapterCinema(cinema.Play));
        var store = _store;
        return new OriginalShell(MenuLayoutReaderTests.OriginalLayout(), new FreeFlightFeature(), Setup(), Measure,
            planes: _planes, campaign: campaign, profiles: () => store);
    }

    // The stand-in for Launcher.PlayCinema: it records what it was asked for and hands the film's
    // end back to the caller, so a test decides when the cinema stops.
    private sealed class Recorder
    {
        private Action? _then;

        public string? Name { get; private set; }

        public int Plays { get; private set; }

        public void Play(string name, Action then, CinemaSkip skip)
        {
            Name = name;
            Plays++;
            _then = then;
        }

        public void Stop() => _then?.Invoke();
    }
}
