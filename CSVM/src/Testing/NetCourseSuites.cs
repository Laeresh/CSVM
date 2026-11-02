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

    [Suite("net-course-long-race",
        "a two-machine Stunt Race over a 100 ms loopback on a 30-entry course built in the scratch game "
        + "folder: both seats fly it whole, the guest with a touch penalty; every split of both racers "
        + "stands on the guest's board as the host decided it, the guest's penalty-inclusive time reads "
        + "the same on both machines, each machine records its own best under the course key and takes "
        + "no Danger Zone photograph; a 256-entry course is refused for a network race with the reason")]
    internal static void ALongCourseRunCrossesWhole(TestContext ctx)
    {
        var missionZrdr = ctx.MissionZrdr(Chapter, SessionSpec.StuntRaceMission);
        ctx.RequireData(ctx.Data.Planes, $"planes gamez");
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        ctx.RequireData(ctx.ChapterGamez(Chapter), $"{Chapter} gamez");
        ctx.RequireData(missionZrdr, $"{Chapter}/{SessionSpec.StuntRaceMission} zrdr");

        if (new CourseLibrary(CourseLibrary.ShippedRoot, null).Load(Chapter, Sample, out string error) is not { } sample)
        {
            ctx.Check(false, $"the shipped course {Chapter}/{Sample} loads ({error})");
            return;
        }

        string root = Path.Combine(ctx.ScratchDir, "net-course", "long");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        // The out-and-back flown six times over, written to the host's scratch game folder.
        string game = Path.Combine(root, CourseLibrary.GameFolderName);
        var authored = sample with { Name = "long_out_and_back", Sequence = Enumerable.Repeat(sample.Sequence, 6).SelectMany(s => s).ToList() };
        Directory.CreateDirectory(Path.Combine(game, Chapter));
        File.WriteAllText(Path.Combine(game, Chapter, authored.Name + ".json"), authored.ToJson());
        Directory.CreateDirectory(Path.Combine(root, "shipped"));
        var hostLibrary = new CourseLibrary(Path.Combine(root, "shipped"), game);
        if (hostLibrary.Load(Chapter, authored.Name, out error) is not { } course)
        {
            ctx.Check(false, $"the scratch course {Chapter}/{authored.Name} loads ({error})");
            return;
        }

        ctx.Check(course.Sequence.Count > 24 && course.Hash == authored.Hash,
            $"the scratch course loads with more than 24 entries ({course.Sequence.Count})");
        var ambient = NetCombatSuites.Ambient.Save();
        try
        {
            LongRace(ctx, root, course, hostLibrary);
            TooLong(ctx, root, sample);
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

    private static void LongRace(TestContext ctx, string root, Course course, CourseLibrary hostLibrary)
    {
        var word = DogfightLobby.WordFor(1, course);
        var specs = new[] { Spec(root, 0, course), Spec(root, 1, course) };
        var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(0.1, 0.0, 0.0), new Random(9605));
        var ends = new List<NetCombatSuites.Ends>();
        try
        {
            ends.Add(NetCombatSuites.Ends.Open(ctx, specs[0], mesh[0], isHost: true, HostSeed, NetCombatSuites.Roster(2, specs[0]),
                course: () => word, courses: hostLibrary));
            ends.Add(NetCombatSuites.Ends.Open(ctx, specs[1], mesh[1], isHost: false, HostSeed + 1, null,
                course: () => word, courses: new CourseLibrary(Path.Combine(root, "shipped"), Path.Combine(root, "guest"))));
            var peers = ends.Select(e => e.Session).ToArray();
            if (!ends.All(e => e.Built)
                || peers[0].SeatRigs[0].Controller is not { Stunt: { } hostRun, Race: { } hostRace } host
                || peers[1].SeatRigs[1].Controller is not { Stunt: { } guestRun, Race: { } guestRace } guest)
            {
                ctx.Check(false, $"two sessions build the course race and each seat races a course run ({string.Join(", ", ends.Select(e => e.Built))})");
                return;
            }

            host.UseKeyboard = false;
            host.PadDevices = Array.Empty<int>();
            bool opened = Until(peers, () => hostRace.MayStartRun);
            int hostCrossed = course.Sequence.Count(entry => Through(peers, host, hostRun, course, entry));
            FlyGuestRun(ctx, "long", peers, guest, guestRun, guestRace, course, penaltyAt: 10);
            bool back = Until(peers, () => guestRace.Of(0)?.BestTime != null);
            ctx.Check(opened && hostCrossed == course.Sequence.Count && back,
                $"the host's seat flies every entry and its run reaches the guest's board (window open {opened}, {hostCrossed} of {course.Sequence.Count}, back {back})");

            for (int seat = 0; seat < 2; seat++)
            {
                var hostSplits = hostRace.Of(seat)?.Line().Splits ?? Array.Empty<float?>();
                var guestSplits = guestRace.Of(seat)?.Line().Splits ?? Array.Empty<float?>();
                ctx.Check(guestSplits.Count == course.Sequence.Count && guestSplits.All(s => s != null) && guestSplits.SequenceEqual(hostSplits),
                    $"seat {seat}'s every split stands on the guest's board as the host holds it ({guestSplits.Count(s => s != null)} of {course.Sequence.Count}, last {Seconds(guestSplits.LastOrDefault())} against {Seconds(hostSplits.LastOrDefault())})");
            }

            float? onHost = hostRace.Of(1)?.BestTime;
            float? onGuest = guestRace.Of(1)?.BestTime;
            ctx.Check(guestRun.PenaltyCount == 1 && onHost == guestRun.Elapsed && onGuest == guestRun.Elapsed,
                $"the guest's time with its touch penalty reads the same on both machines ({guestRun.PenaltyCount} penalty, run {Seconds(guestRun.Elapsed)}, host board {Seconds(onHost)}, guest board {Seconds(onGuest)})");

            string prefix = $"course/{course.Chapter}/{course.Name}/{course.Hash}/";
            var keyed = new[] { (Race: hostRace, Seat: 0, Run: hostRun), (Race: guestRace, Seat: 1, Run: guestRun) };
            for (int machine = 0; machine < 2; machine++)
            {
                var (race, seat, run) = keyed[machine];
                string key = race.Of(seat)?.ScoreKey ?? "";
                float? best = ScoreStore.Load(Path.Combine(root, $"scores{machine}.json")).GetBest(key);
                ctx.Check(key.StartsWith(prefix, StringComparison.Ordinal) && best == run.Elapsed,
                    $"machine {machine} records its seat's best under the course key ('{key}', best {Seconds(best)} for a {Seconds(run.Elapsed)} run)");
            }

            int shots = (host.StuntShots?.Count ?? 0) + (guest.StuntShots?.Count ?? 0);
            ctx.Check(shots == 0, $"neither machine takes a Danger Zone photograph on the course ({shots})");
        }
        finally
        {
            foreach (var end in Enumerable.Reverse(ends))
            {
                end.Close();
            }
        }
    }

    // One entry past what a racer line carries: the network launch is refused with the reason.
    private static void TooLong(TestContext ctx, string root, Course sample)
    {
        int count = RaceStandingMessage.MaxZones + 1;
        var course = sample with
        {
            Name = "too_long",
            Sequence = Enumerable.Range(0, count).Select(i => sample.Sequence[i % sample.Sequence.Count]).ToList(),
        };
        var spec = Spec(root, 0, course);
        var mesh = LoopbackTransport.Mesh(2, new LoopbackConditions(0.1, 0.0, 0.0), new Random(9606));
        var end = NetCombatSuites.Ends.Open(ctx, spec, mesh[0], isHost: true, HostSeed, NetCombatSuites.Roster(2, spec),
            exitSession: () => { }, course: () => DogfightLobby.WordFor(1, course), courses: new CourseLibrary(CourseLibrary.ShippedRoot, null));
        try
        {
            string? refusal = end.Session.MenuRefusal;
            ctx.Check(!end.Built && refusal?.Contains($"its {count} sequence entries pass the {RaceStandingMessage.MaxZones} a network race carries", StringComparison.Ordinal) == true,
                $"a {count}-entry course is refused for a network race ({end.Built}: '{refusal}')");
        }
        finally
        {
            end.Close();
        }
    }

    // A lobby launch's Stunt Race on the course, each machine with its own scores file.
    private static SessionSpec Spec(string root, int machine, Course course) =>
        SessionSpec.FromMenu(SessionSpec.Parse(new[] { "--mute", "--no-pads", $"--scores={Path.Combine(root, $"scores{machine}.json")}" }),
            course.Chapter, new[] { Plane }, MenuMode.Stunt, vsTimeMinutes: 5, missionType: DogfightMissionType.StuntRace, course: course.Name);

    // The guest's run, every entry flown through the host's gate once the window opens, until the
    // host's line brings its best back. A touch penalty lands once penaltyAt entries are crossed.
    private static void FlyGuestRun(TestContext ctx, string cell, GameSession[] peers, FlightController guest, StuntMission run,
        StuntRace race, Course course, int penaltyAt = 0)
    {
        guest.UseKeyboard = false;
        guest.PadDevices = Array.Empty<int>();
        bool opened = Until(peers, () => race.MayStartRun);
        int crossed = 0;
        foreach (var entry in course.Sequence)
        {
            crossed += Through(peers, guest, run, course, entry) ? 1 : 0;
            if (penaltyAt > 0 && crossed == penaltyAt && run.PenaltyCount == 0)
            {
                run.AddPenalty(CourseElements.TouchPenaltySeconds);
            }
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
