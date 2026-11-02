using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Flight.Modes;
using CSVM.Net;
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
        Assert.Equal(0x6C, (int)NetMessageType.LobbyCourse);
        Assert.Equal(NetReliability.Reliable, NetMessage.ReliabilityOf(NetMessageType.LobbyCourse));

        var past = atLimit with { Json = atLimit.Json + "x" };
        Assert.Equal(LobbyCourseMessage.MaxSize + 1, past.Size);
        Assert.False(past.Fits);
        Assert.Throws<InvalidOperationException>(() => past.Write(new byte[LobbyCourseMessage.MaxSize + 16]));

        // A whole course and the Danger Zones word round trip too.
        foreach (var sent in new[] { DogfightLobby.WordFor(3, course), LobbyCourseMessage.None(4) })
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
        Assert.Null(guests[0].CourseWord);
        byte before = host.Options.Epoch;

        Assert.True(host.SetCourse(course));
        Settle(host, guests, wires);
        Assert.Equal(course.Hash, guests[0].Course?.Hash);
        Assert.Equal("Harbour Dash", guests[0].Course?.DisplayName);
        Assert.Equal(string.Empty, guests[0].CourseRefusal);
        Assert.Equal(host.CourseWord, guests[0].CourseWord);
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
        Assert.Null(host.CourseWord);
        Assert.Null(guests[0].Course);
        Assert.Null(guests[1].CourseWord);
    }

    [Fact]
    public void AnOversizeCourseOrAGuestsPickIsRefusedAndAForgedHashIsNotTaken()
    {
        var (host, guests, wires) = Lobbies(2, _ => true);
        var course = Harbour();
        var oversize = course with { Title = new string('t', LobbyCourseMessage.MaxSize) };

        Assert.False(DogfightLobby.CourseFits(oversize));
        Assert.False(host.SetCourse(oversize));
        Assert.False(guests[0].SetCourse(course));
        Assert.True(DogfightLobby.CourseFits(course));
        Assert.Null(host.CourseWord);

        // A word whose JSON does not hash as sent reaches the guest's lobby but flies nothing.
        wires[0].Tell(1, DogfightLobby.WordFor(1, course) with { Hash = "0000000000000000" });
        Settle(host, guests, wires);
        Assert.NotNull(guests[0].CourseWord);
        Assert.Null(guests[0].Course);
        Assert.Contains("C1/Harbour is refused", guests[0].CourseRefusal, StringComparison.Ordinal);
    }

    [Fact]
    public void ALaunchRefusesAMissingWordAnotherCourseAndAForgedHash()
    {
        var course = Harbour();
        var word = DogfightLobby.WordFor(1, course);
        var wanted = (course.Chapter, course.Name);

        Assert.Null(DogfightLobby.CourseOf(null, wanted, out string missing));
        Assert.Equal("the host's course has not arrived", missing);
        Assert.Null(DogfightLobby.CourseOf(word, ("C1", "Docks"), out string other));
        Assert.Equal("the host's course is C1/Harbour", other);
        Assert.Null(DogfightLobby.CourseOf(word with { Hash = "0000000000000000" }, wanted, out string forged));
        Assert.StartsWith("the host's course C1/Harbour is refused: its gates hash to", forged, StringComparison.Ordinal);

        // ABLE-TO-FAIL CONTROL: the right word for the launch's course, matched without case, is taken.
        Assert.Equal(course.Hash, DogfightLobby.CourseOf(word, ("c1", "HARBOUR"), out string none)?.Hash);
        Assert.Equal(string.Empty, none);
    }

    [Fact]
    public void AHostThatAdvertisesTakesNoCourseFromAGuest()
    {
        var (_, _, wires) = Lobbies(2, _ => true);
        var word = DogfightLobby.WordFor(1, Harbour());
        wires[0].Advertise(new SessionAdvertMessage(NetSessionKind.Dogfight, SessionAdvertMessage.NoMission, 1, ""));
        wires[1].Tell(0, word);
        wires[0].Step(0.016);
        Assert.Null(wires[0].Course);

        // ABLE-TO-FAIL CONTROL: the same word from the host reaches the guest's lobby.
        wires[0].Tell(1, word);
        wires[1].Step(0.016);
        Assert.Equal(word, wires[1].Course);
    }

    private static Course Harbour() => Parse(Json);

    private static Course Parse(string json) =>
        Course.Parse("C1", "Harbour", json, out string error) ?? throw new InvalidOperationException(error);

    private static (DogfightLobby Host, List<DogfightLobby> Guests, List<NetLobby> Wires) Lobbies(int players, Func<int, bool> seated)
    {
        var mesh = LoopbackTransport.Mesh(players, Clean, new Random(players));
        var wires = new List<NetLobby>();
        foreach (var end in mesh)
        {
            wires.Add(new NetLobby(end));
        }

        var host = new DogfightLobby(wires[0], () => "Host") { Seated = seated };
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
