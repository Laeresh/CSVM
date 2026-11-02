using System;
using System.Collections.Generic;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight.Modes;

/// <summary>One Danger Zone of a stunt run: the <c>dzN</c> marker point's world position, its
/// route ribbon name (<c>dzpathN</c>, debug-only geometry), the resolved display strings, and
/// whether the player has flown through it yet.</summary>
public sealed class StuntZone
{
    public string DzName = "";        // dz1..dzN, hand-placed marker/HUD anchor
    public string PathName = "";      // dzpath1..dzpathN, route ribbon + gate pair
    public Vector3 Position;           // world-space HUD marker position
    public StuntGate GreenGate = null!;
    public StuntGate RedGate = null!;
    public string Description = "";   // resolved, e.g. "Train Tunnel Mid"
    public string Category = "";      // resolved, e.g. "Danger Zone"
    public string Help = "";          // resolved action, e.g. "Fly Through"
    public bool Completed;

    /// <summary>Run clock, seconds, at the moment this zone was flown through, its cumulative
    /// time from run start. 0 until completed; the scoreboard's per-zone split is
    /// the delta between consecutive completions in <see cref="CompletionOrder"/>.</summary>
    public float CompletedAt;

    /// <summary>0-based order in which this zone was cleared (the run is order-free, so this is
    /// the flown order, not the ia.json list order). −1 until completed.</summary>
    public int CompletionOrder = -1;

    /// <summary>The <c>dzpathN</c> route ribbon, the original's own line through the zone, which a
    /// return to this zone heads the pilot along. Null when the route polygon builds no ribbon. A course
    /// entry's is a remake-only line through its gate (<see cref="DangerZoneRibbon.ForEntry"/>).</summary>
    public DangerZoneRibbon? Route;

    /// <summary>The gate whose crossing completed this zone, the one the pilot left it through.
    /// Null until the zone is flown, and after a debug completion, which crosses nothing.</summary>
    public StuntGate? ExitGate;

    /// <summary>Unit direction of the pilot's travel across <see cref="ExitGate"/>. The data
    /// authors no direction for a zone, so the pilot's own crossing is the only one there is.</summary>
    public Vector3 ExitTravel;

    /// <summary>The original's assembled marker text without the clock suffix (the marker HUD
    /// adds that): "Danger Zone [Fly Through] - Train Tunnel Mid". Degrades gracefully if any
    /// part is absent.</summary>
    public string MarkerText()
    {
        var head = Category.Length > 0 && Help.Length > 0 ? $"{Category} [{Help}]"
            : Help.Length > 0 ? $"[{Help}]"
            : Category;
        return head.Length > 0 && Description.Length > 0 ? $"{head} - {Description}"
            : Description.Length > 0 ? Description
            : head.Length > 0 ? head
            : DzName;
    }
}

/// <summary>An authored Danger Zone aperture: a planar polygon ring in world coordinates.</summary>
public sealed class StuntGate
{
    public required Vector3[] Vertices { get; init; }
    public required Vector3 Center { get; init; }
    public required Vector3 Normal { get; init; }

    /// <summary>Whether the segment from <paramref name="from"/> to <paramref name="to"/> passes
    /// through the ring, the run's own gate test; <paramref name="t"/> is how far along it crosses.</summary>
    public bool CrossedBy(Vector3 from, Vector3 to, out float t)
    {
        t = 0f;
        float fromDistance = Normal.Dot(from - Center);
        float toDistance = Normal.Dot(to - Center);
        if (fromDistance == 0f || toDistance == 0f || Mathf.Sign(fromDistance) == Mathf.Sign(toDistance))
            return false;
        t = fromDistance / (fromDistance - toDistance);
        return PointInGate(from.Lerp(to, t), this);
    }

    private static bool PointInGate(Vector3 point, StuntGate gate)
    {
        int dropAxis = Mathf.Abs(gate.Normal.X) > Mathf.Abs(gate.Normal.Y)
            ? (Mathf.Abs(gate.Normal.X) > Mathf.Abs(gate.Normal.Z) ? 0 : 2)
            : (Mathf.Abs(gate.Normal.Y) > Mathf.Abs(gate.Normal.Z) ? 1 : 2);
        Vector2 Project(Vector3 p) => dropAxis == 0 ? new Vector2(p.Y, p.Z)
            : dropAxis == 1 ? new Vector2(p.X, p.Z) : new Vector2(p.X, p.Y);
        var q = Project(point);
        bool inside = false;
        for (int i = 0, j = gate.Vertices.Length - 1; i < gate.Vertices.Length; j = i++)
        {
            var a = Project(gate.Vertices[i]);
            var b = Project(gate.Vertices[j]);
            if ((a.Y > q.Y) != (b.Y > q.Y)
                && q.X < (b.X - a.X) * (q.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }
}

/// <summary>
/// The Stunt Flying instant-action mode: a run crosses both authored apertures of every Danger
/// Zone, in any order, and ends when all are done.
///
/// The zone list is the mission ia.json's <c>dzones</c> (<c>[dzpathN, dzN]</c> pairs). Each
/// <c>dzN</c>'s world position is a point marker in the chapter gamez, under the identity World
/// root. Its display strings come from targets.json through the message table. Detection,
/// completion and the objective-target feed (<see cref="CollectTargets"/>); the run HUD and timed
/// scoring build on this. A course run is built by <see cref="ForCourse"/> instead, in strict
/// sequence order with one zone per sequence entry.
/// </summary>
public sealed class StuntMission
{
    /// <summary>Radius about the <c>dzN</c> marker centre (user-tuned). Not a scoring value,
    /// completion crosses the authored gate pair; retained for the marker's non-scoring
    /// consumers.</summary>
    public const float DzRadius = 15f;

    /// <summary>Remake-only rule, not a game value: how far short of a course gate's opening its
    /// entry's line starts, metres (<see cref="StuntZone.Route"/>). A race bot locks on near that
    /// start, and the rail closes its offset before the opening.</summary>
    public const float EntryLeadInM = 250f;

    /// <summary>Remake-only rule, not a game value: how far past the opening an entry's line runs on,
    /// metres. The rail's pose trails its cursor, so this carries the pose through the opening.</summary>
    public const float EntryRunOutM = 60f;

    /// <summary>Prefix for this run's log lines ("P2 " in a splitscreen race). Empty in a
    /// solo run, so single-player logs read exactly as before, but with four pilots clearing zones
    /// in one shared world the completion lines are otherwise indistinguishable.</summary>
    public string LogTag = "";

    // Messages key for the run-start intro line ("Fly through all the Danger Zones to
    // win!"), the marker HUD's one-shot banner.
    private const string IntroMsgKey = "MSG_BRF_IASF_OBJ2";

    private readonly List<StuntZone> _zones;
    private readonly HashSet<StuntGate> _crossedGates = new();

    // A course run's apertures, one per pool gate, and each sequence entry's pool index. Empty on a
    // Danger Zone run, which is how every course branch below knows which rule it runs.
    private readonly CourseAperture[] _apertures = Array.Empty<CourseAperture>();
    private readonly int[] _entryGates = Array.Empty<int>();

    // Per pool gate, its elements' bounding sphere and where they start in _touching. That holds
    // whether the plane was inside each element's volume on the last step.
    private readonly (Vector3 Centre, float Radius)[] _gateBounds = Array.Empty<(Vector3, float)>();
    private readonly int[] _firstElement = Array.Empty<int>();
    private readonly bool[] _touching = Array.Empty<bool>();
    private Vector3 _lastPlanePos;
    private bool _haveLastPlanePos;
    private bool _clockStarted;

    private StuntMission(List<StuntZone> zones) => _zones = zones;

    private StuntMission(List<StuntZone> zones, Course course, CourseAperture[] apertures, int[] entryGates)
    {
        _zones = zones;
        Course = course;
        _apertures = apertures;
        _entryGates = entryGates;
        _gateBounds = new (Vector3, float)[course.Gates.Count];
        _firstElement = new int[course.Gates.Count];
        int elements = 0;
        for (int g = 0; g < course.Gates.Count; g++)
        {
            _gateBounds[g] = CourseElements.Bounds(course.Gates[g]);
            _firstElement[g] = elements;
            elements += CourseElements.Count(course.Gates[g]);
        }
        _touching = new bool[elements];

        // Each run lays its own lines, so each reads its own GateMotion and SimSeconds.
        for (int i = 0; i < zones.Count; i++)
            zones[i].Route = EntryLine(i);
    }

    /// <summary>Fired once per zone the moment it is flown through (scoring records a split).</summary>
    public event Action<StuntZone>? ZoneCompleted;

    /// <summary>Fired once when the last zone completes (stops the clock / shows the board).</summary>
    public event Action? RunCompleted;

    /// <summary>Fired on the step a run's clock starts: its first <see cref="Tick"/>, or a course
    /// run's crossing of its first entry.</summary>
    public event Action? RunStarted;

    /// <summary>Fired by <see cref="Reset"/>: the run so far is thrown away.</summary>
    public event Action? RunReset;

    /// <summary>Fired by <see cref="Stop"/>: the run flies no further, and what it reached stands.</summary>
    public event Action? RunStopped;

    /// <summary>Fired on a course run's step the plane enters a course element's touch volume
    /// (<see cref="CourseElements"/>), once until it leaves that element. It fires before the first
    /// entry and on the finishing step too, with no time added, so a cut still plays. A finished run
    /// tests nothing.</summary>
    public event Action<CourseTouch>? ElementTouched;

    public bool AllComplete { get; private set; }

    /// <summary>Whether <see cref="Stop"/> ended the run short of completion. Its clock stands.</summary>
    public bool Stopped { get; private set; }

    public int CompletedCount { get; private set; }

    public int TotalCount => _zones.Count;

    public IReadOnlyList<StuntZone> Zones => _zones;

    /// <summary>Elapsed run time, seconds. It starts at GO: a run behind a <see cref="StartCount"/>
    /// reads 0 until the count hands over. Once started the clock never stops: <see cref="Tick"/>
    /// advances it every physics frame, through the crash freeze, until the run is complete.
    /// A tapped respawn keeps it, like the zones; a held one calls <see cref="Reset"/>. A course
    /// run's clock starts on crossing its first entry instead.</summary>
    public float Elapsed { get; private set; }

    /// <summary>The one-shot run-start line the marker HUD shows ("Fly through all the Danger
    /// Zones to win!"), resolved at load from the message table. Empty if the table is absent.</summary>
    public string IntroLine { get; private set; } = "";

    /// <summary>The authored course this run flies in sequence order, or null on a Danger Zone run.
    /// Each sequence entry is one <see cref="Zones"/> entry, so the next entry is
    /// <see cref="CompletedCount"/>.</summary>
    public Course? Course { get; }

    /// <summary>Penalties added by <see cref="AddPenalty"/> this run; their time is in <see cref="Elapsed"/>.</summary>
    public int PenaltyCount { get; private set; }

    /// <summary>Where <see cref="Update"/> last had the plane, or null before its first step and after
    /// <see cref="Relocated"/>. A course's wrong-side cross reads it.</summary>
    public Vector3? PlanePosition => _haveLastPlanePos ? _lastPlanePos : null;

    /// <summary>A course gate's idle motion in its own frame: pool index into
    /// <see cref="Course.Gates"/> and sim seconds in, the <c>motion</c> of
    /// <see cref="CourseAperture.CrossedBy"/> out. The drawn gate must use the same function at the
    /// same time. The session sets it to <see cref="CourseMotion.Offset"/>; the default is the
    /// identity, a still gate.</summary>
    public Func<int, double, Transform3D> GateMotion { get; set; } = static (_, _) => Transform3D.Identity;

    /// <summary>The sim seconds <see cref="GateMotion"/> is asked at: the session clock, never wall time.</summary>
    public Func<double> SimSeconds { get; set; } = static () => GameClock.Current?.Time ?? 0.0;

    /// <summary>Builds the stunt run for a mission: reads its ia.json <c>dzones</c>, resolves each
    /// <c>dzN</c>'s world position from <paramref name="worldGamez"/> and its display strings from
    /// the mission's targets.json (keys resolved via <paramref name="messages"/>). Null if the
    /// mission has no dzones or none of them resolve to a world node (not a stunt mission).</summary>
    public static StuntMission? Load(GameZ worldGamez, ZrdrSet missionZrdr, Messages messages)
    {
        List<object?> root;
        try
        {
            root = missionZrdr.LoadFile("ia.json");
        }
        catch (System.IO.IOException)
        {
            return null; // no ia.json (story/multiplayer folder), no stunt zones
        }

        var dzones = ZrdrDict.FromAlternating(root).List("dzones");
        if (dzones == null || dzones.Count == 0)
            return null;

        var targets = MissionTargets.Load(missionZrdr);
        var zones = new List<StuntZone>();
        var unresolved = new List<string>();
        foreach (var pairObj in dzones)
        {
            // each entry is [dzpathN, dzN]; the dzN point is what we test against
            if (pairObj is not List<object?> pair || pair.Count < 2
                || pair[0] is not string pathName || pair[1] is not string dzName)
                continue;
            var node = worldGamez.FindByName(dzName);
            if (node == null)
            {
                unresolved.Add(dzName);
                continue;
            }
            if (!TryReadGates(worldGamez, pathName, out var green, out var red, out var route))
            {
                unresolved.Add(pathName);
                continue;
            }
            var t = targets.For(dzName);
            zones.Add(new StuntZone
            {
                DzName = dzName,
                PathName = pathName,
                Position = GeometryAnchor(worldGamez, node) ?? worldGamez.WorldTransformOf(node).Origin,
                GreenGate = green,
                RedGate = red,
                Route = route,
                Description = messages.Get(t.Description),
                Category = messages.Get(t.CategoryLabel),
                Help = messages.Get(t.HelpLabel),
            });
        }

        if (unresolved.Count > 0)
            Log.Warn("flight", $"stunt: {unresolved.Count} danger-zone marker(s) not in the world gamez: {string.Join(", ", unresolved)}");
        if (zones.Count == 0)
            return null;

        Log.Info("flight", $"stunt: {zones.Count} danger zone(s) loaded");
        foreach (var z in zones)
            Log.Info("flight", $"  {z.DzName}: {z.MarkerText()} @ ({z.Position.X:0},{z.Position.Y:0},{z.Position.Z:0})");
        return new StuntMission(zones) { IntroLine = messages.Get(IntroMsgKey) };
    }

    /// <summary>The strict-order run over an authored course. Only the next sequence entry
    /// registers, crossed through its gate the way the entry names. The clock starts on crossing
    /// the first entry and stops on the last. Null, with the reason logged, for a course with an
    /// empty sequence, which the editor may save but nobody can fly.</summary>
    public static StuntMission? ForCourse(Course course)
    {
        if (course.Sequence.Count == 0)
        {
            Log.Warn("flight", $"course {course.Chapter}/{course.Name}: the sequence is empty, nothing to fly");
            return null;
        }

        var apertures = new CourseAperture[course.Gates.Count];
        for (int g = 0; g < apertures.Length; g++)
            apertures[g] = new CourseAperture(course.Gates[g]);
        var zones = new List<StuntZone>(course.Sequence.Count);
        var entryGates = new int[course.Sequence.Count];
        for (int i = 0; i < entryGates.Length; i++)
        {
            var entry = course.Sequence[i];
            int g = 0;
            while (course.Gates[g].Id != entry.GateId)
                g++; // Course.Parse refuses an entry naming no gate
            entryGates[i] = g;
            var gate = course.Gates[g];
            var centre = apertures[g].Rest * CourseGateShape.OpeningCentre(gate);
            // A one-point polygon nothing can cross: scoring is the aperture's, and this only gives
            // ReturnPose the gate's centre to find on the entry's line.
            var marker = new StuntGate { Vertices = new[] { centre }, Center = centre, Normal = gate.Forward };
            zones.Add(new StuntZone
            {
                DzName = $"gate{i + 1}",
                PathName = gate.Id,
                Position = centre,
                GreenGate = marker,
                RedGate = marker,
                Description = $"Gate {i + 1}",
            });
        }

        Log.Info("flight", $"course {course.Chapter}/{course.Name}: {zones.Count} sequence entries over {apertures.Length} gate(s)");
        return new StuntMission(zones, course, apertures, entryGates);
    }

    /// <summary>"m:ss.t" run-clock formatting, shared by the marker HUD status line and the
    /// scoreboard. Invariant culture so the decimal is always a period regardless of the
    /// player's system locale (a game clock, and deterministic across screenshot runs).</summary>
    public static string FormatTime(float seconds)
    {
        int min = (int)(seconds / 60f);
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{0}:{1:00.0}", min, seconds - min * 60f);
    }

    /// <summary>A second, independent run over the same zones (splitscreen racing). It keeps the zone
    /// list, positions and strings, with its own completion flags, clock, target and events. Copying beats calling <see cref="Load"/> once per player, the
    /// ia.json/targets.json/messages parse and the gamez lookups happen once for the session. A course
    /// copy lays its own entry lines, read at its own <see cref="GateMotion"/>.</summary>
    public StuntMission ForAnotherPlayer()
    {
        var zones = new List<StuntZone>(_zones.Count);
        foreach (var z in _zones)
            zones.Add(new StuntZone
            {
                DzName = z.DzName,
                PathName = z.PathName,
                Position = z.Position,
                GreenGate = z.GreenGate,
                RedGate = z.RedGate,
                Route = z.Route,
                Description = z.Description,
                Category = z.Category,
                Help = z.Help,
            });
        return Course != null
            ? new StuntMission(zones, Course, _apertures, _entryGates) { GateMotion = GateMotion, SimSeconds = SimSeconds }
            : new StuntMission(zones) { IntroLine = IntroLine };
    }

    /// <summary>Physics-frame test: record any gate the plane's movement segment crossed this
    /// frame. A zone completes once both its gates have been crossed, in either order, and several
    /// can complete in one pass. A course run also tests the touch volumes against a sphere of
    /// <paramref name="planeRadius"/> about the plane, the airframe's <c>AircraftBody.BoundRadius</c>.</summary>
    public void Update(Vector3 planePos, float planeRadius = 0f)
    {
        if (AllComplete)
            return;
        if (!_haveLastPlanePos)
        {
            _lastPlanePos = planePos;
            _haveLastPlanePos = true;
            return;
        }
        if (Course != null)
        {
            CrossNextEntry(planePos);
            TouchElements(planePos, planeRadius);
            _lastPlanePos = planePos;
            return;
        }
        foreach (var z in _zones)
        {
            if (z.Completed)
                continue;
            bool green = z.GreenGate.CrossedBy(_lastPlanePos, planePos, out float greenT);
            bool red = z.RedGate.CrossedBy(_lastPlanePos, planePos, out float redT);
            if (green)
                _crossedGates.Add(z.GreenGate);
            if (red)
                _crossedGates.Add(z.RedGate);
            if (_crossedGates.Contains(z.GreenGate) && _crossedGates.Contains(z.RedGate))
            {
                // The completing frame crossed at least one gate; of two, the later along the segment.
                z.ExitGate = red && (!green || redT >= greenT) ? z.RedGate : z.GreenGate;
                z.ExitTravel = (planePos - _lastPlanePos).Normalized();
                Complete(z);
            }
        }
        _lastPlanePos = planePos;
    }

    /// <summary>The pilot was placed rather than flown there, a respawn, so the next
    /// <see cref="Update"/> starts a fresh segment instead of testing the jump against every gate.
    /// The plane has left every touch volume, so an element entered again counts.</summary>
    public void Relocated()
    {
        _haveLastPlanePos = false;
        Array.Clear(_touching);
    }

    /// <summary>Where a tap of respawn puts the pilot: on the route ribbon abeam the exit gate of
    /// the zone cleared last, the highest <see cref="StuntZone.CompletionOrder"/>. It heads along
    /// the ribbon the way the pilot crossed that gate. Null with no zone cleared, which is the
    /// start. Falls back to the exit ring's own centre and axis where the zone has no ribbon.</summary>
    public (Vector3 Position, Vector3 Heading)? ReturnPose()
    {
        StuntZone? last = null;
        foreach (var z in _zones)
        {
            if (z.Completed && (last == null || z.CompletionOrder > last.CompletionOrder))
                last = z;
        }
        if (last?.ExitGate is not { } gate)
            return null;
        if (last.Route is { } route)
        {
            var (segment, t) = route.NearestTo(gate.Center);
            var tangent = route.TangentAt(segment, t);
            if (tangent.LengthSquared() > 1e-8f)
            {
                tangent = tangent.Normalized();
                return (route.PointAt(segment, t), tangent.Dot(last.ExitTravel) < 0f ? -tangent : tangent);
            }
        }
        return (gate.Center, gate.Normal.Dot(last.ExitTravel) < 0f ? -gate.Normal : gate.Normal);
    }

    /// <summary>Advance the run clock one physics frame. Not called while a start count runs,
    /// because the clock starts at GO. Once started it is called every frame, through the crash
    /// freeze. That is a remake-only rule: a crash costs the run the seconds it spends frozen. It
    /// stops only at completion or <see cref="Stop"/>.</summary>
    public void Tick(float dt)
    {
        if (AllComplete || Stopped)
            return;
        if (!_clockStarted)
        {
            if (Course != null)
                return; // a course clock starts on crossing the first entry, in Update
            _clockStarted = true;
            RunStarted?.Invoke();
        }
        Elapsed += dt;
    }

    /// <summary>Appends this run's still-unflown zones as objective-flagged candidates, the feed
    /// the pilot's own <see cref="TargetSelection"/> files on its Enemy/Objective cycle. A cleared
    /// zone is not offered again, so the re-resolve fails to find it and drops to the cycle's head.
    /// ⚠ Per RUN, never per session. Each pilot flies their own <see cref="ForAnotherPlayer"/> copy
    /// and its own <see cref="StuntZone"/> objects, and the selection is held by source identity,
    /// so one pane's pick cannot reach another's.</summary>
    public void CollectTargets(List<AimCandidate> into)
    {
        for (int i = 0; i < _zones.Count; i++)
        {
            var z = _zones[i];
            // A course offers only its next entry, the one gate that can register.
            if (z.Completed || (Course != null && i != CompletedCount))
                continue;
            into.Add(new AimCandidate
            {
                Position = z.Position,
                // Neutral, as the original builds an object for a targets.zrd record standing on an
                // ordinary node (docs/org/targeting.md). The objective flag is what makes it
                // selectable; the team only decides who may shoot at it, and nobody shoots a zone.
                Team = AimAssist.NeutralTeam,
                Live = true,
                ConeOverride = AimAssist.NoConeOverride,
                Source = z,
            });
        }
    }

    /// <summary>Start a fresh run: a results board's Restart item, R on it, or a held respawn.
    /// Every zone is incomplete, the clock back to zero, the active target back to the first zone.
    /// Unlike a tapped respawn this DOES clear progress and the clock, it is the deliberate opposite
    /// of the crash-keeps-everything rule.</summary>
    public void Reset()
    {
        foreach (var z in _zones)
        {
            z.Completed = false;
            z.CompletedAt = 0f;
            z.CompletionOrder = -1;
            z.ExitGate = null;
            z.ExitTravel = Vector3.Zero;
        }
        CompletedCount = 0;
        AllComplete = false;
        Elapsed = 0f;
        PenaltyCount = 0;
        _clockStarted = false;
        _crossedGates.Clear();
        Relocated();
        Stopped = false;
        RunReset?.Invoke();
    }

    /// <summary>The pilot's aeroplane is lost where no respawn is offered, a race's final run: the
    /// run started flies no further. Its zones and clock stand as they were. Once per run; a run
    /// not started or already complete has nothing to stop.</summary>
    public void Stop()
    {
        if (Stopped || AllComplete || !_clockStarted)
            return;
        Stopped = true;
        RunStopped?.Invoke();
    }

    /// <summary>Adds <paramref name="seconds"/> to a running clock and counts one penalty. A run
    /// not started, complete or stopped takes none.</summary>
    public void AddPenalty(float seconds)
    {
        if (!_clockStarted || AllComplete || Stopped)
            return;
        Elapsed += seconds;
        PenaltyCount++;
    }

    /// <summary>Debug/testing only (--debug-scoreboard): instantly complete the whole run with
    /// synthetic, increasing split times so the end-of-run scoreboard renders deterministically for
    /// a screenshot / layout pass. Drives the real <see cref="Complete"/> path (fires the events,
    /// sets AllComplete). Not reachable in normal play. <paramref name="extraPerZone"/> pads every
    /// split (the splitscreen race passes the player index, so the synthetic board shows a real
    /// ranking instead of four identical totals).</summary>
    public void DebugCompleteAll(float extraPerZone = 0f)
    {
        for (int i = 0; i < _zones.Count; i++)
        {
            Elapsed += 8f + i * 9.5f + extraPerZone;
            if (!_zones[i].Completed)
                Complete(_zones[i]);
        }
    }

    /// <summary>The zones in the order they were flown through (for the scoreboard), completed
    /// zones by <see cref="StuntZone.CompletionOrder"/>, any still-incomplete zones appended in
    /// list order.</summary>
    public IEnumerable<StuntZone> InCompletionOrder()
    {
        var done = new List<StuntZone>();
        foreach (var z in _zones)
            if (z.Completed)
                done.Add(z);
        done.Sort((a, b) => a.CompletionOrder.CompareTo(b.CompletionOrder));
        foreach (var z in done)
            yield return z;
        foreach (var z in _zones)
            if (!z.Completed)
                yield return z;
    }

    /// <summary>Compact HUD status: "STUNT 2/5", or "COMPLETE" once the run is done. The fallback
    /// for a pane with no run HUD; which zone the pilot is pointed at is their own target selection
    /// and is not a property of the run.</summary>
    public string StatusLine() =>
        AllComplete
            ? $"STUNT {CompletedCount}/{TotalCount} COMPLETE"
            : $"STUNT {CompletedCount}/{TotalCount}";

    // The anchor point for a dzone whose ia.json record names world geometry rather than a `dzN`
    // point marker, or null for an ordinary marker (docs/formats/missions.md, the `sghangar` case).
    // Anchors on the union centre of a matched `door`-named leaf pair when one exists, since these
    // zones are all `MSG_OBJ_FLYTHROUGH` and mean the aperture, not the structure's middle.
    private static Vector3? GeometryAnchor(GameZ gz, GameZNode node)
    {
        var worldXf = gz.WorldTransformOf(node);
        var origin = worldXf.Origin;
        var boxes = new List<(string Name, Aabb Box)>();
        CollectMeshBoxes(gz, node, worldXf, boxes);
        if (boxes.Count == 0)
        {
            return null; // an ordinary dzN point marker, all 53 in this install land here
        }

        int doorCount = 0;
        foreach (var b in boxes)
        {
            if (IsDoorLeaf(b.Name))
            {
                doorCount++;
            }
        }
        bool hasDoorPair = doorCount >= 2;

        Aabb? merged = null;
        foreach (var b in boxes)
        {
            if (hasDoorPair && !IsDoorLeaf(b.Name))
            {
                continue;
            }
            merged = merged == null ? b.Box : merged.Value.Merge(b.Box);
        }
        var anchor = merged!.Value.GetCenter();
        string meshesDesc = hasDoorPair ? $"the {doorCount} door leaves of its {boxes.Count} meshes"
                                         : $"all {boxes.Count} of its meshes";
        Log.Info("flight", $"stunt: {node.Name} is world geometry, not a dz marker, anchored on {meshesDesc} at ({anchor.X:0.0}, {anchor.Y:0.0}, {anchor.Z:0.0}) instead of its node origin ({origin.X:0.0}, {origin.Y:0.0}, {origin.Z:0.0})");
        return anchor;
    }

    // The original's own node naming is the semantic layer here, as it is for props,
    // control surfaces, wing flares and damage panels, a hangar/garage door leaf is named for
    // what it is (`sgh_door1`, `sgh_door2`).
    private static bool IsDoorLeaf(string name) =>
        name.Contains("door", StringComparison.OrdinalIgnoreCase);

    // World-space AABB of every mesh at or under `node`, one entry per
    // drawing node so the caller can pick a subset by name. `worldXf` is this
    // node's own world transform (children compose their GameZNode.Local onto it);
    // a null Local contributes identity, which is exactly why the hangar's parts are already in
    // world coordinates.
    private static void CollectMeshBoxes(GameZ gz, GameZNode node, Transform3D worldXf,
        List<(string Name, Aabb Box)> into)
    {
        if (node.MeshIndex >= 0 && node.MeshIndex < gz.Meshes.Count)
        {
            var verts = gz.Meshes[node.MeshIndex].Vertices;
            if (verts.Count > 0)
            {
                var box = new Aabb(worldXf * verts[0], Vector3.Zero);
                for (int i = 1; i < verts.Count; i++)
                {
                    box = box.Expand(worldXf * verts[i]);
                }
                into.Add((node.Name, box));
            }
        }
        foreach (var ci in node.Children)
        {
            if (ci >= 0 && ci < gz.Nodes.Count)
            {
                var child = gz.Nodes[ci];
                CollectMeshBoxes(gz, child, worldXf * (child.Local ?? Transform3D.Identity), into);
            }
        }
    }

    private static bool TryReadGates(GameZ gz, string pathName, out StuntGate green, out StuntGate red,
        out DangerZoneRibbon? routeRibbon)
    {
        green = null!;
        red = null!;
        routeRibbon = null;
        var node = gz.FindByName(pathName);
        if (node == null || node.MeshIndex < 0 || node.MeshIndex >= gz.Meshes.Count)
            return false;
        var mesh = gz.Meshes[node.MeshIndex];
        if (mesh.Polygons.Count != 3)
            return false;

        // The two aperture outlines share their material; the route ribbon has the odd material.
        var byMaterial = new Dictionary<int, List<GameZPolygon>>();
        foreach (var poly in mesh.Polygons)
        {
            if (!byMaterial.TryGetValue(poly.MaterialIndex, out var group))
            {
                group = new List<GameZPolygon>();
                byMaterial.Add(poly.MaterialIndex, group);
            }
            group.Add(poly);
        }
        GameZPolygon? route = null;
        List<GameZPolygon>? gates = null;
        foreach (var group in byMaterial.Values)
        {
            if (group.Count == 2)
                gates = group;
            else if (group.Count == 1)
                route = group[0];
        }
        if (gates == null || route == null
            || !TryMakeGate(gz, node, gates[0], out green)
            || !TryMakeGate(gz, node, gates[1], out red))
            return false;
        routeRibbon = RouteRibbon(gz, node, route);
        return true;
    }

    // The route polygon as the ribbon the original's AI flies through the zone, docs/org/aiPilot.md
    // "The danger-zone run". Null for a polygon too short or too degenerate to make one.
    private static DangerZoneRibbon? RouteRibbon(GameZ gz, GameZNode node, GameZPolygon route)
    {
        var xf = gz.WorldTransformOf(node);
        var mesh = gz.Meshes[node.MeshIndex];
        var vertices = new List<Vector3>(route.VertexIndices.Count);
        foreach (int index in route.VertexIndices)
        {
            if (index >= 0 && index < mesh.Vertices.Count)
                vertices.Add(xf * mesh.Vertices[index]);
        }
        if (vertices.Count < 2)
            return null;
        const string Prefix = "dzpath";
        int number = node.Name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(node.Name.AsSpan(Prefix.Length), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out int parsed) ? parsed : 0;
        try
        {
            return DangerZoneRibbon.FromPolyline(node.Name, number, vertices);
        }
        catch (ArgumentException)
        {
            return null; // every vertex coincides: no line through the zone to follow
        }
    }

    private static bool TryMakeGate(GameZ gz, GameZNode node, GameZPolygon poly, out StuntGate gate)
    {
        gate = null!;
        if (poly.VertexIndices.Count < 3)
            return false;
        var xf = gz.WorldTransformOf(node);
        var mesh = gz.Meshes[node.MeshIndex];
        var vertices = new Vector3[poly.VertexIndices.Count];
        Vector3 center = Vector3.Zero;
        for (int i = 0; i < vertices.Length; i++)
        {
            int index = poly.VertexIndices[i];
            if (index < 0 || index >= mesh.Vertices.Count)
                return false;
            center += vertices[i] = xf * mesh.Vertices[index];
        }
        center /= vertices.Length;
        Vector3 normal = Vector3.Zero;
        for (int i = 0; i < vertices.Length; i++)
        {
            var a = vertices[i] - center;
            var b = vertices[(i + 1) % vertices.Length] - center;
            normal += a.Cross(b);
        }
        if (normal.LengthSquared() < 1e-6f)
            return false;
        gate = new StuntGate { Vertices = vertices, Center = center, Normal = normal.Normalized() };
        return true;
    }

    // Entry i's line, through its gate's opening the way the entry is flown. It follows the gate's
    // motion at the sim time, as the aperture does. That motion never tilts the gate's axis, so the
    // opening centre's travel moves the whole line.
    private DangerZoneRibbon EntryLine(int i)
    {
        int g = _entryGates[i];
        var gate = Course!.Gates[g];
        var local = CourseGateShape.OpeningCentre(gate);
        var rest = _apertures[g].Rest;
        var centre = rest * local;
        var way = gate.FlownWay(Course.Sequence[i].Direction);
        return DangerZoneRibbon.ForEntry(gate.Id, i + 1, centre, way, EntryLeadInM, EntryRunOutM,
            () => (rest * GateMotion(g, SimSeconds()) * local) - centre);
    }

    // A course step tests the next entry alone. One segment thus advances at most one entry, and a
    // repeated gate never counts twice from one crossing.
    private void CrossNextEntry(Vector3 planePos)
    {
        int next = CompletedCount;
        int g = _entryGates[next];
        var direction = Course!.Sequence[next].Direction;
        if (!_apertures[g].CrossedBy(_lastPlanePos, planePos, direction, GateMotion(g, SimSeconds()), out _))
            return;
        var z = _zones[next];
        z.ExitGate = z.GreenGate;
        z.ExitTravel = (planePos - _lastPlanePos).Normalized();
        if (!_clockStarted)
        {
            _clockStarted = true;
            RunStarted?.Invoke();
        }
        Complete(z);
    }

    // An element counts on the step the plane's sphere enters its volume, and again only once it has
    // left. Volumes are tested in each gate's frame at the motion it is drawn with.
    private void TouchElements(Vector3 planePos, float planeRadius)
    {
        double now = SimSeconds();
        for (int g = 0; g < _apertures.Length; g++)
        {
            var gate = _apertures[g].Gate;
            var frame = _apertures[g].Rest * GateMotion(g, now);
            var local = frame.AffineInverse() * planePos;
            var bounds = _gateBounds[g];
            bool near = local.DistanceTo(bounds.Centre) <= bounds.Radius + planeRadius;
            for (int e = 0, count = CourseElements.Count(gate); e < count; e++)
            {
                int slot = _firstElement[g] + e;
                bool was = _touching[slot];
                var contact = near ? CourseElements.Nearest(gate, e, local) : Vector3.Zero;
                _touching[slot] = near && local.DistanceSquaredTo(contact) <= planeRadius * planeRadius;
                if (_touching[slot] && !was)
                    Touch(g, e, frame * contact, contact.Y);
            }
        }
    }

    private void Touch(int g, int e, Vector3 point, float height)
    {
        var gate = Course!.Gates[g];
        bool pylons = gate.Kind == CourseGateKind.PylonGate;
        var element = pylons ? (e == 0 ? CourseElement.LeftCoursePylon : CourseElement.RightCoursePylon)
            : e == 0 ? CourseElement.RingRim : CourseElement.Balloon;
        int before = PenaltyCount;
        AddPenalty(CourseElements.TouchPenaltySeconds);
        bool added = PenaltyCount > before;
        Log.Info("flight", $"stunt: {LogTag}touched {gate.Id} {element}{(e > 0 && !pylons ? $" {e - 1}" : "")}{(added ? ", time added" : "")}");
        ElementTouched?.Invoke(new CourseTouch(g, element, pylons ? 0 : Math.Max(e - 1, 0), point, pylons ? height : 0f, added));
    }

    private void Complete(StuntZone z)
    {
        z.Completed = true;
        z.CompletedAt = Elapsed;      // cumulative run time, the scoreboard derives splits
        z.CompletionOrder = CompletedCount; // 0-based, before the increment below
        CompletedCount++;
        Log.Info("flight", $"stunt: {LogTag}completed {z.DzName}, {z.MarkerText()} ({CompletedCount}/{TotalCount})");
        ZoneCompleted?.Invoke(z);
        if (CompletedCount >= _zones.Count && !AllComplete)
        {
            AllComplete = true;
            Log.Info("flight", $"stunt: {LogTag}ALL DANGER ZONES COMPLETE");
            RunCompleted?.Invoke();
        }
    }
}
