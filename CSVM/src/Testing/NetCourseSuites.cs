using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CSVM.Flight.Airframe;
using CSVM.Flight.Modes;
using CSVM.Launch;
using CSVM.Net;
using CSVM.Spec;
using CSVM.UI.Menu;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>A network Stunt Race on the lobby host's course, on <see cref="NetCombatSuites"/>'s rig.
/// Each machine's launch builds its run from the host's course word, never from a local file of the
/// same name. A guest's game folder is a scratch folder.</summary>
internal static class NetCourseSuites
{
    private const ulong HostSeed = 0xC0FFEE96UL;
    private const string Chapter = "C1";
    private const string Sample = "sample_out_and_back";
    private const string Plane = "player_pfighter";

    // How far short of a gate the aircraft is placed, and how many steps it may take to cross.
    private const float LeadM = 8f;
    private const int CrossSteps = 20;

    // How far the guest's own same-named file moves the first gate. A crossing of the host's gate
    // there could not count on the guest's file.
    private const float MovedM = 500f;

    // Generous against the start gate, the opening's five seconds and any retransmission.
    private const int StepLimit = 1200;

    [Suite("net-course-from-host",
        "a two-machine Stunt Race over a 100 ms loopback on the host's course C1/sample_out_and_back, "
        + "both launches built from the host's course word: a guest with no file of that name keeps a "
        + "copy in its scratch game folder, a guest holding a different file under that name leaves it "
        + "unchanged, and each crosses every entry at the host's gate and records its best under the "
        + "host course's hash; a guest's launch is refused, keeping nothing, on a word whose hash does "
        + "not match and on a word naming another course")]
    internal static void EachMachineFliesTheHostsCourse(TestContext ctx)
    {
        var missionZrdr = ctx.MissionZrdr(Chapter, SessionSpec.StuntRaceMission);
        ctx.RequireData(ctx.Data.Planes, $"planes gamez");
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        ctx.RequireData(ctx.ChapterGamez(Chapter), $"{Chapter} gamez");
        ctx.RequireData(missionZrdr, $"{Chapter}/{SessionSpec.StuntRaceMission} zrdr");

        if (new CourseLibrary(CourseLibrary.ShippedRoot, null).Load(Chapter, Sample, out string error) is not { } course)
        {
            ctx.Check(false, $"the shipped course {Chapter}/{Sample} loads ({error})");
            return;
        }

        var word = DogfightLobby.WordFor(1, course);
        var moved = course with { Gates = course.Gates.Select((g, i) => i == 0 ? g with { Position = g.Position + (Vector3.Right * MovedM) } : g).ToList() };
        var ambient = NetCombatSuites.Ambient.Save();
        try
        {
            Race(ctx, "absent", course, word, ownJson: null, seed: 9601);
            Race(ctx, "same-named", course, word, ownJson: moved.ToJson(), seed: 9602);
            Refused(ctx, "forged-hash", course, word with { Hash = moved.Hash }, "is refused: its gates hash to", seed: 9603);
            Refused(ctx, "other-course", course, word with { Name = "another_course" }, "the host's course is C1/another_course", seed: 9604);
        }
        finally
        {
            ambient.Restore();
        }
    }

    private static void Race(TestContext ctx, string cell, Course course, LobbyCourseMessage word, string? ownJson, int seed)
    {
        string root = Path.Combine(ctx.ScratchDir, "net-course", cell);
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        // The guest's library: no shipped course, and a game folder holding its own file or none.
        string shipped = Path.Combine(root, "shipped");
        string game = Path.Combine(root, CourseLibrary.GameFolderName);
        string ownFile = Path.Combine(game, course.Chapter, course.Name + ".json");
        Directory.CreateDirectory(shipped);
        if (ownJson != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ownFile)!);
            File.WriteAllText(ownFile, ownJson);
        }

        var guestLibrary = new CourseLibrary(shipped, game);
        var local = guestLibrary.Load(course.Chapter, course.Name, out _);
        ctx.Check(ownJson == null ? local == null : local != null && local.Hash != course.Hash,
            $"ABLE-TO-FAIL CONTROL: [{cell}] the guest's library alone flies something else ({(local == null ? "no course" : $"hash {local.Hash} against the host's {course.Hash}")})");

        var specs = new[] { Spec(root, 0, course), Spec(root, 1, course) };
        ctx.Check(specs.All(s => s.Course != null && s.CourseRefusal == null && s.Chapter == course.Chapter),
            $"[{cell}] both lobby launches name the course ({specs[0].Course}, {specs[0].CourseRefusal ?? "no refusal"})");
        var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(0.1, 0.0, 0.0), new Random(seed));
        var roster = NetCombatSuites.Roster(2, specs[0]);
        var ends = new List<NetCombatSuites.Ends>();
        try
        {
            ends.Add(NetCombatSuites.Ends.Open(ctx, specs[0], mesh[0], isHost: true, HostSeed, roster,
                course: () => word, courses: new CourseLibrary(CourseLibrary.ShippedRoot, null)));
            ends.Add(NetCombatSuites.Ends.Open(ctx, specs[1], mesh[1], isHost: false, HostSeed + 1, null,
                course: () => word, courses: guestLibrary));
            ctx.Check(ends.All(e => e.Built), $"[{cell}] two sessions build the course race ({string.Join(", ", ends.Select(e => e.Built))})");
            if (!ends.All(e => e.Built))
            {
                return;
            }

            var peers = ends.Select(e => e.Session).ToArray();
            var host = peers[0].SeatRigs[0].Controller!;
            var guest = peers[1].SeatRigs[1].Controller!;
            if (host.Stunt is not { Course: { } hostCourse } || guest.Stunt is not { Course: { } guestCourse } guestRun
                || guest.Race is not { } guestRace)
            {
                ctx.Check(false, $"[{cell}] each machine's own seat races a course run");
                return;
            }

            ctx.Check(hostCourse.Hash == course.Hash && guestCourse.Hash == course.Hash
                      && guestCourse.Gates.SequenceEqual(course.Gates) && guestRun.TotalCount == course.Sequence.Count,
                $"[{cell}] both machines fly the host's gates (host {hostCourse.Hash}, guest {guestCourse.Hash}, {guestRun.TotalCount} entries)");
            if (ownJson == null)
            {
                var kept = File.Exists(ownFile) ? Course.Parse(course.Chapter, course.Name, File.ReadAllText(ownFile), out _) : null;
                ctx.Check(kept?.Hash == course.Hash && kept.DisplayName == course.DisplayName,
                    $"[{cell}] the guest keeps the host's course in its game folder ({(kept == null ? "no file" : $"{ownFile}, hash {kept.Hash}")})");
            }
            else
            {
                ctx.Check(File.ReadAllText(ownFile) == ownJson, $"[{cell}] the guest's own file of that name is left unchanged");
            }

            FlyGuestRun(ctx, cell, peers, guest, guestRun, guestRace, course);
            // The plane in the key is the seat's roster airframe, so only the course part is fixed here.
            string key = guestRace.Of(1)?.ScoreKey ?? "";
            float? best = ScoreStore.Load(Path.Combine(root, "scores1.json")).GetBest(key);
            ctx.Check(key.StartsWith($"course/{course.Chapter}/{course.Name}/{course.Hash}/", StringComparison.Ordinal) && best is > 0f,
                $"[{cell}] the guest's best lands under the host course's hash ('{key}', best {Seconds(best)})");
        }
        finally
        {
            foreach (var end in Enumerable.Reverse(ends))
            {
                end.Close();
            }
        }
    }

    // The host builds on the right word. The guest's launch, handed a bad one, is refused with the
    // reason its menu shows, and it keeps no file.
    private static void Refused(TestContext ctx, string cell, Course course, LobbyCourseMessage bad, string reason, int seed)
    {
        string root = Path.Combine(ctx.ScratchDir, "net-course", cell);
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        string shipped = Path.Combine(root, "shipped");
        string game = Path.Combine(root, CourseLibrary.GameFolderName);
        Directory.CreateDirectory(shipped);
        var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(0.1, 0.0, 0.0), new Random(seed));
        var hostSpec = Spec(root, 0, course);
        var ends = new List<NetCombatSuites.Ends>();
        try
        {
            ends.Add(NetCombatSuites.Ends.Open(ctx, hostSpec, mesh[0], isHost: true, HostSeed, NetCombatSuites.Roster(2, hostSpec),
                course: () => DogfightLobby.WordFor(1, course), courses: new CourseLibrary(CourseLibrary.ShippedRoot, null)));
            ends.Add(NetCombatSuites.Ends.Open(ctx, Spec(root, 1, course), mesh[1], isHost: false, HostSeed + 1, null,
                exitSession: () => { }, course: () => bad, courses: new CourseLibrary(shipped, game)));
            string? refusal = ends[1].Session.MenuRefusal;
            ctx.Check(ends[0].Built && !ends[1].Built && refusal?.Contains(reason, StringComparison.Ordinal) == true,
                $"[{cell}] the host builds and the guest's launch is refused ({ends[0].Built}/{ends[1].Built}: '{refusal}')");
            ctx.Check(!Directory.Exists(game), $"[{cell}] and the guest keeps no course file");
        }
        finally
        {
            foreach (var end in Enumerable.Reverse(ends))
            {
                end.Close();
            }
        }
    }

    // A lobby launch's Stunt Race on the course, each machine with its own scores file.
    private static SessionSpec Spec(string root, int machine, Course course) =>
        SessionSpec.FromMenu(SessionSpec.Parse(new[] { "--mute", "--no-pads", $"--scores={Path.Combine(root, $"scores{machine}.json")}" }),
            course.Chapter, new[] { Plane }, MenuMode.Stunt, vsTimeMinutes: 5, missionType: DogfightMissionType.StuntRace, course: course.Name);

    // The guest's run, every entry flown through the host's gate once the window opens, until the
    // host's line brings its best back.
    private static void FlyGuestRun(TestContext ctx, string cell, GameSession[] peers, FlightController guest, StuntMission run,
        StuntRace race, Course course)
    {
        guest.UseKeyboard = false;
        guest.PadDevices = Array.Empty<int>();
        bool opened = Until(peers, () => race.MayStartRun);
        int crossed = 0;
        foreach (var entry in course.Sequence)
        {
            crossed += Through(peers, guest, run, course, entry) ? 1 : 0;
        }

        bool back = Until(peers, () => race.Of(1)?.BestTime != null);
        ctx.Check(opened && crossed == course.Sequence.Count && run.AllComplete && back,
            $"[{cell}] the guest crosses every entry at the host's gate and its run reaches the board (window open {opened}, {crossed} of {course.Sequence.Count} crossed, best {Seconds(race.Of(1)?.BestTime)})");
    }

    private static string Seconds(float? time) => time?.ToString("0.000", CultureInfo.InvariantCulture) ?? "none";

    // The aircraft placed short of the entry's gate centre and flown through it the entry's way, at
    // the gate's pose now, both machines stepping. True once the run counts a further entry.
    private static bool Through(GameSession[] peers, FlightController plane, StuntMission run, Course course, SequenceEntry entry)
    {
        var gate = course.Gates.First(x => x.Id == entry.GateId);
        var centre = CourseGateShape.OpeningCentre(gate);
        float lead = entry.Direction == GateDirection.Forward ? LeadM : -LeadM;
        var pose = CourseMotion.Pose(gate, run.SimSeconds());
        plane.RespawnAt(pose * (centre + (Vector3.Back * lead)), pose * (centre + (Vector3.Forward * lead)));
        int before = run.CompletedCount;
        for (int i = 0; i < CrossSteps && run.CompletedCount == before; i++)
        {
            Step(peers);
        }

        return run.CompletedCount > before;
    }

    private static bool Until(GameSession[] peers, Func<bool> condition)
    {
        for (int step = 0; step < StepLimit && !condition(); step++)
        {
            Step(peers);
        }

        return condition();
    }

    // Each session's own clock is framed first, as its rendered frame does.
    private static void Step(GameSession[] peers)
    {
        foreach (var session in peers)
        {
            session.SimClock?.BeginFrame(GameClock.FixedDt);
            session._PhysicsProcess(GameClock.FixedDt);
        }
    }
}
