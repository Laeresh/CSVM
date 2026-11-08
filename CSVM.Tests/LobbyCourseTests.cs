using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight.Modes;
using CSVM.Net;
using CSVM.Session.Roster;
using CSVM.Spec;
using CSVM.UI.Menu;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// A Stunt Race host's course on the lobby's wire: the message at its size limit and the guest's
/// hash check. The game folder copy never overwrites a file. Over a loopback mesh, the host's pick
/// reaches every guest, a late one included.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class LobbyCourseTests
{
    private const string Json = """
        {
          "version": 1,
          "title": "Harbour Dash",
          "start": { "pos": [10, 120, -40.5], "heading": 90 },
          "gates": [
            { "id": "a", "kind": "pylons", "pos": [0, 12, -200], "heading": 0, "width": 40, "height": 30 },
            { "id": "b", "kind": "ring", "pos": [150.25, 80, -400], "heading": 45, "diameter": 25 }
          ],
          "sequence": [
            { "gate": "a", "dir": "forward" },
            { "gate": "b", "dir": "reverse" }
          ]
        }
        """;

    private static readonly LoopbackConditions Clean = new(0.0, 0.0, 0.0);

    private readonly string _shipped = TestData.TempDir();
    private readonly string _game = TestData.TempDir();

    [Fact]
    public void AMessageAtTheLimitRoundTripsAndOneBytePastItIsNeverWritten()
    {
        var course = Harbour();
        var empty = new LobbyCourseMessage(9, course.Chapter, course.Name, course.Hash, "");
        var atLimit = empty with { Json = new string('x', LobbyCourseMessage.MaxSize - empty.Size) };
        var buffer = new byte[LobbyCourseMessage.MaxSize];

        Assert.Equal(LobbyCourseMessage.MaxSize, atLimit.Size);
        Assert.True(atLimit.Fits);
        Assert.Equal(LobbyCourseMessage.MaxSize, atLimit.Write(buffer));
        Assert.True(LobbyCourseMessage.TryRead(buffer, out var got));
        Assert.Equal(atLimit, got);
        Assert.Equal(0x6D, (int)NetMessageType.LobbyCourse);
        Assert.Equal(NetReliability.Reliable, NetMessage.ReliabilityOf(NetMessageType.LobbyCourse));

        var past = atLimit with { Json = atLimit.Json + "x" };
        Assert.Equal(LobbyCourseMessage.MaxSize + 1, past.Size);
        Assert.False(past.Fits);
        Assert.Throws<InvalidOperationException>(() => past.Write(new byte[LobbyCourseMessage.MaxSize + 16]));

        // A whole course and the Danger Zones word round trip too.
        foreach (var sent in new[] { Course.WordFor(3, course), LobbyCourseMessage.None(4) })
        {
            int length = sent.Write(buffer);
            Assert.Equal(sent.Size, length);
            Assert.True(LobbyCourseMessage.TryRead(buffer.AsSpan(0, length), out var back));
            Assert.Equal(sent, back);
            Assert.Equal(sent.IsNone, back.IsNone);
        }
    }

    [Fact]
    public void AMessageWhoseCountsRunPastItsLengthIsRefused()
    {
        var sent = new LobbyCourseMessage(1, "C1", "Harbour", "0123456789abcdef", Json);
        var buffer = new byte[sent.Size];
        sent.Write(buffer);

        // The JSON's count, the last field's, claims one byte more than the message holds.
        int jsonCount = 4 + 1 + (2 + 2) + (2 + 7) + (2 + 16);
        buffer[jsonCount]++;
        Assert.False(LobbyCourseMessage.TryRead(buffer, out _));

        // ABLE-TO-FAIL CONTROL: the count put back reads again, and a short count leaves bytes over.
        buffer[jsonCount]--;
        Assert.True(LobbyCourseMessage.TryRead(buffer, out _));
        buffer[jsonCount]--;
        Assert.False(LobbyCourseMessage.TryRead(buffer, out _));
    }

    [Fact]
    public void AGuestTakesTheHostsCourseOnlyWhenItsGatesHashAsSent()
    {
        var course = Harbour();

        var taken = Course.FromWire(course.Chapter, course.Name, course.Hash, course.ToJson(), out string none);
        Assert.NotNull(taken);
        Assert.Equal(string.Empty, none);
        Assert.Equal(course.Hash, taken!.Hash);
        Assert.Equal("Harbour Dash", taken.DisplayName);

        var moved = Course.FromWire(course.Chapter, course.Name, course.Hash, Json.Replace("\"heading\": 90", "\"heading\": 91", StringComparison.Ordinal), out string why);
        Assert.Null(moved);
        Assert.Contains(course.Hash, why, StringComparison.Ordinal);

        Assert.Null(Course.FromWire(course.Chapter, course.Name, course.Hash, "{ \"start\": 3 }", out string broken));
        Assert.NotEmpty(broken);
    }

    [Fact]
    public void TheHostsCourseIsKeptOnlyWhereNoFileOfItsNameStands()
    {
        var library = new CourseLibrary(_shipped, _game);
        var course = Harbour();

        string? kept = library.SaveIfAbsent(course);
        Assert.Equal(Path.Combine(_game, "C1", "Harbour.json"), kept);
        Assert.Equal(course.Hash, library.Load("C1", "Harbour", out _)!.Hash);

        // A same-named file stands now, whatever it holds, so a different course leaves it alone.
        string own = Path.Combine(_game, "C1", "Harbour.json");
        string before = File.ReadAllText(own);
        var other = Parse(Json.Replace("\"heading\": 90", "\"heading\": 180", StringComparison.Ordinal));
        Assert.Null(library.SaveIfAbsent(other with { Name = "harbour" }));
        Assert.Equal(before, File.ReadAllText(own));

        // A shipped file of the name stands for it too, and a library with no game folder keeps nothing.
        Directory.CreateDirectory(Path.Combine(_shipped, "C2"));
        File.WriteAllText(Path.Combine(_shipped, "C2", "Docks.json"), Json);
        Assert.Null(library.SaveIfAbsent(course with { Chapter = "C2", Name = "Docks" }));
        Assert.False(File.Exists(Path.Combine(_game, "C2", "Docks.json")));
        Assert.Null(new CourseLibrary(_shipped, null).SaveIfAbsent(course with { Name = "Elsewhere" }));

        // A chapter a host could aim outside the folder with is refused, and nothing is written.
        Assert.Null(library.SaveIfAbsent(course with { Chapter = "..", Name = "Escaped" }));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_game)!, "Escaped.json")));
    }

    [Fact]
    public void TheHostsPickReachesEveryGuestALateOneIncludedAndStartsANewRound()
    {
        bool lateSeated = false;
        var (host, guests, wires) = Lobbies(3, peer => peer != 2 || lateSeated);
        var course = Harbour();
        Settle(host, guests, wires);
        host.SetReady(true);
        guests[0].SetReady(true);
        Settle(host, guests, wires);
        Assert.Null(guests[0].Course);
        Assert.Null(guests[0].LobbyCourse);
        byte before = host.Options.Epoch;

        Assert.True(host.SetCourse(course));
        Settle(host, guests, wires);
        Assert.Equal(course.Hash, guests[0].Course?.Hash);
        Assert.Equal("Harbour Dash", guests[0].Course?.DisplayName);
        Assert.Equal(string.Empty, guests[0].CourseRefusal);
        Assert.Equal(host.LobbyCourse, guests[0].LobbyCourse);
        Assert.NotEqual(before, host.Options.Epoch);
        Assert.False(host.Ready);
        Assert.False(guests[0].Ready);

        // The guest seated after the pick hears it on its first step as a guest.
        Assert.Null(guests[1].Course);
        lateSeated = true;
        Settle(host, guests, wires);
        Assert.Equal(course.Hash, guests[1].Course?.Hash);

        // ABLE-TO-FAIL CONTROL: the same pick again is no new round, and the Danger Zones clear it.
        byte picked = host.Options.Epoch;
        Assert.True(host.SetCourse(Harbour()));
        Assert.Equal(picked, host.Options.Epoch);
        Assert.True(host.SetCourse(null));
        Settle(host, guests, wires);
        Assert.Null(host.LobbyCourse);
        Assert.Null(guests[0].Course);
        Assert.Null(guests[1].LobbyCourse);
    }

    [Fact]
    public void AnOversizeCourseOrAGuestsPickIsRefusedAndAForgedHashIsNotTaken()
    {
        var (host, guests, wires) = Lobbies(2, _ => true);
        var course = Harbour();
        var oversize = course with { Title = new string('t', LobbyCourseMessage.MaxSize) };

        Assert.False(Course.FitsWire(oversize));
        Assert.False(host.SetCourse(oversize));
        Assert.False(guests[0].SetCourse(course));
        Assert.True(Course.FitsWire(course));
        Assert.Null(host.LobbyCourse);

        // A run longer than a racer line's splits is refused; the longest one fits.
        var entry = course.Sequence[0];
        Assert.False(host.SetCourse(course with { Sequence = Enumerable.Repeat(entry, RaceStandingMessage.MaxZones + 1).ToList() }));
        Assert.True(Course.FitsWire(course with { Sequence = Enumerable.Repeat(entry, RaceStandingMessage.MaxZones).ToList() }));

        // A word whose JSON does not hash as sent reaches the guest's lobby but flies nothing.
        wires[0].Tell(1, Course.WordFor(1, course) with { Hash = "0000000000000000" });
        Settle(host, guests, wires);
        Assert.NotNull(guests[0].LobbyCourse);
        Assert.Null(guests[0].Course);
        Assert.Contains("C1/Harbour is refused", guests[0].CourseRefusal, StringComparison.Ordinal);

        // Its launch would be refused, so it may not stand Ready while that course stands.
        Assert.True(guests[0].RefusesCourse);
        Assert.False(guests[0].SetReady(true));
        Assert.False(guests[0].Ready);

        // ABLE-TO-FAIL CONTROL: once the host's Danger Zones word arrives, the same Ready takes.
        wires[0].Tell(1, LobbyCourseMessage.None(host.Options.Epoch));
        Settle(host, guests, wires);
        Assert.False(guests[0].RefusesCourse);
        Assert.True(guests[0].SetReady(true));
    }

    [Fact]
    public void ALaunchRefusesAMissingWordAnotherCourseAndAForgedHash()
    {
        var course = Harbour();
        var word = Course.WordFor(1, course);
        var wanted = (course.Chapter, course.Name);

        Assert.Null(Course.FromWord(null, wanted, out string missing));
        Assert.Equal("the host's course has not arrived", missing);
        Assert.Null(Course.FromWord(word, ("C1", "Docks"), out string other));
        Assert.Equal("the host's course is C1/Harbour", other);
        Assert.Null(Course.FromWord(word with { Hash = "0000000000000000" }, wanted, out string forged));
        Assert.StartsWith("the host's course C1/Harbour is refused: its gates hash to", forged, StringComparison.Ordinal);

        // ABLE-TO-FAIL CONTROL: the right word for the launch's course, matched without case, is taken.
        Assert.Equal(course.Hash, Course.FromWord(word, ("c1", "HARBOUR"), out string none)?.Hash);
        Assert.Equal(string.Empty, none);
    }

    [Fact]
    public void TheCourseBoxOffersEveryCourseThatFitsByTitle()
    {
        Directory.CreateDirectory(Path.Combine(_shipped, "C1"));
        Directory.CreateDirectory(Path.Combine(_game, "C1"));
        File.WriteAllText(Path.Combine(_shipped, "C1", "Harbour.json"), Json);
        File.WriteAllText(Path.Combine(_game, "C1", "Zed.json"), Json.Replace("Harbour Dash", "Airfield Loop", StringComparison.Ordinal));
        var entry = Harbour().Sequence[0];
        var tooLong = Harbour() with { Name = "Long", Sequence = Enumerable.Repeat(entry, RaceStandingMessage.MaxZones + 1).ToList() };
        File.WriteAllText(Path.Combine(_game, "C1", "Long.json"), tooLong.ToJson());
        File.WriteAllText(Path.Combine(_game, "C1", "Broken.json"), "{ \"start\": 3 }");

        var choices = new CourseLibrary(_shipped, _game).Choices("C1");

        Assert.Equal(new[] { "Zed", "Harbour" }, choices.Select(c => c.Name));
        Assert.Equal(new[] { "Airfield Loop", "Harbour Dash" }, choices.Select(c => c.DisplayName));
        Assert.Empty(new CourseLibrary(_shipped, _game).Choices("C2"));
    }

    [Fact]
    public void ALaunchFliesTheHostsCourseInItsChaptersStuntRaceAndAMoveOffEitherTakesTheDangerZones()
    {
        var (host, guests, wires) = Lobbies(2, _ => true);
        int c1 = DogfightLobby.EnvironmentOf("C1");
        Assert.True(host.SetMissionType(DogfightMissionType.StuntRace));
        Assert.True(host.SetEnvironment(c1));
        Assert.True(host.SetCourse(Harbour()));
        Settle(host, guests, wires);
        Assert.Equal("Harbour", host.LaunchCourse);
        Assert.Equal("Harbour", guests[0].LaunchCourse);

        // Another chapter's race takes the Danger Zones on every machine.
        Assert.True(host.SetEnvironment(DogfightLobby.EnvironmentOf("C3")));
        Settle(host, guests, wires);
        Assert.Null(host.Course);
        Assert.Null(host.LaunchCourse);
        Assert.Null(guests[0].LobbyCourse);
        Assert.Null(guests[0].LaunchCourse);
        Assert.False(host.SetCourse(Harbour()));

        // ABLE-TO-FAIL CONTROL: back on the course's chapter it flies again, until a Deathmatch drops it.
        Assert.True(host.SetEnvironment(c1));
        Assert.True(host.SetCourse(Harbour()));
        Settle(host, guests, wires);
        Assert.Equal("Harbour", guests[0].LaunchCourse);
        Assert.True(host.SetMissionType(DogfightMissionType.Deathmatch));
        Settle(host, guests, wires);
        Assert.Null(host.Course);
        Assert.Null(guests[0].LaunchCourse);
        Assert.False(host.SetCourse(Harbour()));

        // Capture the Flag drops it too, through the same options change.
        Assert.True(host.SetMissionType(DogfightMissionType.StuntRace));
        Assert.True(host.SetEnvironment(c1));
        Assert.True(host.SetCourse(Harbour()));
        Assert.True(host.SetMissionType(DogfightMissionType.CaptureTheFlag));
        Assert.Null(host.Course);
    }

    [Fact]
    public void ALobbyLaunchOnACourseComesBackToTheTopLevelNotInstantAction()
    {
        var mesh = LoopbackTransport.Mesh(2, Clean, new Random(2));
        var seats = Array.Empty<MenuSeatChoice>();
        var lobby = new LaunchExit("C1", seats, MenuMode.Stunt, Net: new MenuNetLaunch(mesh[0], true), Course: "Harbour");
        Assert.Same(MenuReturnDestination.TopLevel, MenuReturnDestination.ForLaunch(lobby));

        // ABLE-TO-FAIL CONTROL: the same course from Instant Action, with no wire, goes back there.
        Assert.Same(MenuReturnDestination.InstantAction, MenuReturnDestination.ForLaunch(lobby with { Net = null }));
    }

    [Fact]
    public void AHostThatAdvertisesTakesNoCourseFromAGuest()
    {
        var (_, _, wires) = Lobbies(2, _ => true);
        var word = Course.WordFor(1, Harbour());
        wires[0].Advertise(new SessionAdvertMessage(NetSessionKind.Dogfight, SessionAdvertMessage.NoMission, 1, ""));
        wires[1].Tell(0, word);
        wires[0].Step(0.016);
        Assert.Null(wires[0].LobbyCourse);

        // ABLE-TO-FAIL CONTROL: the same word from the host reaches the guest's lobby.
        wires[0].Tell(1, word);
        wires[1].Step(0.016);
        Assert.Equal(word, wires[1].LobbyCourse);
    }

    [Fact]
    public void AStuntRaceIsOfferedInAChapterWithCoursesAndNoDangerZonesAndFliesItsFirstCourse()
    {
        var aloft = Harbour() with { Chapter = "C1C" };
        int clouds = DogfightLobby.EnvironmentOf("C1C");
        IReadOnlyList<Course> Listed(string chapter) => chapter == "C1C" ? new[] { aloft } : Array.Empty<Course>();
        Assert.True(DogfightLobby.Offers(DogfightMissionType.StuntRace, clouds, Listed));

        // ABLE-TO-FAIL CONTROL: a chapter with neither Danger Zones nor a course stays greyed.
        Assert.False(MenuChapters.DangerZonesFor("C1C"));
        Assert.False(DogfightLobby.Offers(DogfightMissionType.StuntRace, clouds));

        var (host, guests, wires) = Lobbies(2, _ => true, Listed);
        Assert.True(host.SetEnvironment(clouds));
        Settle(host, guests, wires);
        Assert.Equal(aloft.Hash, host.Course?.Hash);
        Assert.Equal("Harbour", guests[0].LaunchCourse);

        // No Danger Zones to fly there, so the host may not pick them; another chapter takes them back.
        Assert.False(host.SetCourse(null));
        Assert.True(host.SetEnvironment(DogfightLobby.EnvironmentOf("C1")));
        Assert.Null(host.Course);
    }

    [Fact]
    public void ACourseThatRefusesAPlaneBarsItForPeopleAndBotsAndLeavingItLeavesNoBar()
    {
        const int balmoral = 2;
        const int fury = 7;
        var (host, guests, wires) = Lobbies(2, _ => true);

        // A Dogfight's rules stand into the race: customs allowed, nitro on the list, the list not in force.
        Assert.True(host.SetMissionType(DogfightMissionType.Deathmatch));
        Assert.True(host.SetAllowCustomPlanes(true));
        Assert.True(host.SetOutlawed(NetPlaneRules.NitroFlag, true));
        Assert.True(host.SetMissionType(DogfightMissionType.StuntRace));
        Assert.True(host.SetEnvironment(DogfightLobby.EnvironmentOf("C1")));
        var standing = host.Rules;
        Assert.True(host.AddBot());
        int bot = host.Bots[0].Id;
        Assert.True(host.SetBotAirframe(bot, balmoral));
        guests[0].Pick(balmoral, default);
        Settle(host, guests, wires);
        Assert.True(guests[0].SetReady(true));
        Settle(host, guests, wires);
        Assert.True(host.Players[1].Ready);

        var noBalmoral = Harbour() with { Planes = Enumerable.Range(0, 11).Where(a => a != balmoral).ToList() };
        Assert.True(host.SetCourse(noBalmoral));
        Settle(host, guests, wires);

        // The guest ready in a Balmoral loses its Ready, and its next Ready names the airframe.
        Assert.False(guests[0].Ready);
        Assert.False(host.Players[1].Ready);
        Assert.False(guests[0].SetReady(true));
        Assert.Equal(new[] { PlaneRefusal.Airframe }, guests[0].ReadyRefusals);
        Assert.Equal(1 << balmoral, guests[0].Rules.CourseBarred);
        Assert.Equal((standing.AllowCustom, standing.Outlawing, standing.Outlawed), (guests[0].Rules.AllowCustom, guests[0].Rules.Outlawing, guests[0].Rules.Outlawed));

        // The bot row on a Balmoral is back on Random, which never draws one, and the row's list greys it.
        Assert.True(host.Bots[0].RandomPlane);
        Assert.False(host.AdmitsBotAirframe(balmoral));
        Assert.False(host.SetBotAirframe(bot, balmoral));
        Assert.DoesNotContain(Enumerable.Range(0, 500), seed => BotSeats.DrawPair(null, false, new Random(seed), host.Rules)?.Plane == "player_balmoral");

        // ABLE-TO-FAIL CONTROL: another plane takes Ready, and the Danger Zones leave no bar behind.
        guests[0].Pick(fury, default);
        Assert.True(guests[0].SetReady(true));
        Assert.True(host.SetCourse(null));
        Settle(host, guests, wires);
        Assert.Equal(standing, host.Rules);
        Assert.Equal(0, guests[0].Rules.CourseBarred);
        Assert.True(host.AdmitsBotAirframe(balmoral));
        guests[0].Pick(balmoral, default);
        Assert.True(guests[0].SetReady(true));
        Assert.Contains(Enumerable.Range(0, 500), seed => BotSeats.DrawPair(null, false, new Random(seed), host.Rules)?.Plane == "player_balmoral");
    }

    [Fact]
    public void AGuestThatReadsNoPlanesListMayReadyInARefusedPlaneButTheHostNeverCountsIt()
    {
        const int balmoral = 2;
        const int fury = 7;
        var (host, guests, wires) = Lobbies(2, _ => true);
        var course = Harbour() with { Planes = new[] { fury } };
        Assert.True(host.SetCourse(course));
        Settle(host, guests, wires);

        // An older build's guest parses the same word without the list, which hashes alike, so it sees no bar.
        wires[0].Tell(1, Course.WordFor(host.Options.Epoch, course with { Planes = null }));
        Settle(host, guests, wires);
        Assert.Equal(course.Hash, guests[0].Course?.Hash);
        Assert.Equal(0, guests[0].Rules.CourseBarred);
        guests[0].Pick(balmoral, default);
        Assert.True(guests[0].SetReady(true));
        Settle(host, guests, wires);
        Assert.True(guests[0].Ready);
        Assert.False(host.Players[1].Ready);
        Assert.False(guests[0].Players[guests[0].You].Ready);

        // ABLE-TO-FAIL CONTROL: the plane the host's course admits counts.
        guests[0].Pick(fury, default);
        Assert.True(guests[0].SetReady(true));
        Settle(host, guests, wires);
        Assert.True(host.Players[1].Ready);
    }

    [Fact]
    public void ARaceHeldToOnePlaneSeatsOnlyThatPlaneACustomBuildOfItIncluded()
    {
        const int fury = 7;
        var (host, guests, wires) = Lobbies(2, _ => true);
        host.Show();
        Assert.True(host.SetCourse(Harbour() with { Planes = new[] { fury } }));
        Settle(host, guests, wires);

        Assert.All(Enumerable.Range(0, 11).Where(a => a != fury), a => Assert.False(host.AdmitsBotAirframe(a)));
        Assert.All(Enumerable.Range(0, 200), seed => Assert.Equal("player_fury", BotSeats.DrawPair(null, false, new Random(seed), host.Rules)?.Plane));
        host.Pick(0, default);
        Assert.False(host.SetReady(true));
        Assert.True(guests[0].PickCustom(new NetPlaneBuild { Name = "Wrong", Airframe = 3, Engine = NetPlaneBuild.StockEngine }, default));
        Assert.False(guests[0].SetReady(true));
        Assert.Equal(new[] { PlaneRefusal.Airframe }, guests[0].ReadyRefusals);

        // ABLE-TO-FAIL CONTROL: the Fury takes Ready, stock or a custom build of it.
        host.Pick(fury, default);
        Assert.True(host.SetReady(true));
        Assert.True(guests[0].PickCustom(new NetPlaneBuild { Name = "Mine", Airframe = fury, Engine = NetPlaneBuild.StockEngine }, default));
        Assert.True(guests[0].SetReady(true));
        Settle(host, guests, wires);
        Assert.True(host.AllReady);
    }

    private static Course Harbour() => Parse(Json);

    private static Course Parse(string json) =>
        Course.Parse("C1", "Harbour", json, out string error) ?? throw new InvalidOperationException(error);

    private static (DogfightLobby Host, List<DogfightLobby> Guests, List<NetLobby> Wires) Lobbies(int players, Func<int, bool> seated,
        Func<string, IReadOnlyList<Course>>? courses = null)
    {
        var mesh = LoopbackTransport.Mesh(players, Clean, new Random(players));
        var wires = new List<NetLobby>();
        foreach (var end in mesh)
        {
            wires.Add(new NetLobby(end));
        }

        var host = new DogfightLobby(wires[0], () => "Host") { Seated = seated, Courses = courses };
        // A course stands only in its own chapter's Stunt Race.
        host.SetMissionType(DogfightMissionType.StuntRace);
        host.SetEnvironment(DogfightLobby.EnvironmentOf("C1"));
        var guests = new List<DogfightLobby>();
        for (int i = 1; i < players; i++)
        {
            var guest = new DogfightLobby(wires[i], () => "Guest", 0);
            guest.Show();
            guests.Add(guest);
        }

        return (host, guests, wires);
    }

    private static void Settle(DogfightLobby host, List<DogfightLobby> guests, List<NetLobby> wires)
    {
        for (int round = 0; round < 4; round++)
        {
            wires[0].Step(0.016);
            host.Step();
            for (int i = 0; i < guests.Count; i++)
            {
                wires[i + 1].Step(0.016);
                guests[i].Step();
            }
        }
    }
}
