using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Modes;
using Godot;

namespace CSVM.UI.Labs;

/// <summary>
/// The course the course lab is editing, its selected gate and sequence entry. Every edit goes through here and raises
/// <see cref="Changed"/>, so the lab's drawing, panel, gizmo and sequence overlay follow one
/// source. Engine-free: the lab hands in ground points and camera bases, and this decides what they become.
/// </summary>
public sealed class CourseDraft
{
    /// <summary>A new pylon gate's width and height, metres. TUNE.</summary>
    public const float PylonWidthM = 40f;

    public const float PylonHeightM = 30f;

    /// <summary>A new ring's diameter, and how far its centre stands above the ground clicked, metres. TUNE.</summary>
    public const float RingDiameterM = 40f;

    public const float RingAboveGroundM = 50f;

    /// <summary>How far above the ground clicked the start point stands, metres. TUNE.</summary>
    public const float StartAboveGroundM = 50f;

    /// <summary>A draft of <paramref name="course"/>, nothing selected.</summary>
    public CourseDraft(Course course)
    {
        Course = course;
    }

    /// <summary>Raised after every edit and every change of selection.</summary>
    public event Action? Changed;

    /// <summary>The course as edited so far.</summary>
    public Course Course { get; private set; }

    /// <summary>The selected pool gate's id, or null.</summary>
    public string? SelectedId { get; private set; }

    /// <summary>The selected pool gate, or null.</summary>
    public CourseGate? Selected => Course.Gates.FirstOrDefault(g => g.Id == SelectedId);

    /// <summary>The selected sequence entry's index, or -1. It is the insertion point: a placed gate's entry and
    /// a gate added again go in after it. Selecting a gate alone leaves it where it was.</summary>
    public int SelectedEntry { get; private set; } = -1;

    // Where a new entry goes: after the selected one, else at the end.
    private int InsertAt => SelectedEntry >= 0 ? SelectedEntry + 1 : Course.Sequence.Count;

    /// <summary>A new, empty course of <paramref name="chapter"/> named <paramref name="name"/>, starting at
    /// <paramref name="start"/>.</summary>
    public static Course Empty(string chapter, string name, SpawnPoint start) =>
        new(chapter, name, start, Array.Empty<CourseGate>(), Array.Empty<SequenceEntry>());

    /// <summary>The next free id of <paramref name="kind"/>: <c>p</c> or <c>r</c> and one past the highest
    /// number that prefix carries, so it is unique within the course.</summary>
    public static string NextId(IEnumerable<CourseGate> gates, CourseGateKind kind)
    {
        string prefix = kind == CourseGateKind.PylonGate ? "p" : "r";
        int highest = 0;
        foreach (var gate in gates)
        {
            if (gate.Id.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(gate.Id.AsSpan(prefix.Length), out int n))
            {
                highest = Math.Max(highest, n);
            }
        }

        return prefix + (highest + 1);
    }

    /// <summary>The heading, in <see cref="SpawnPoint"/> degrees, of a camera with basis <paramref name="camera"/>:
    /// the yaw of its view. Looking straight down, the screen's up stands in for the view.</summary>
    public static float HeadingOf(Basis camera)
    {
        var look = -camera.Z with { Y = 0f };
        if (look.LengthSquared() < 1e-6f)
        {
            look = camera.Y with { Y = 0f };
        }

        return Mathf.RadToDeg(Mathf.Atan2(-look.X, -look.Z)) + 0f;
    }

    /// <summary>Where a save goes: the shipped root from the editor build, else the game folder. Null when an
    /// exported build reads no game folder, as under <c>--det</c>.</summary>
    public static CourseSource? SaveTarget(bool editorBuild, bool hasGameFolder) =>
        editorBuild ? CourseSource.Shipped : hasGameFolder ? CourseSource.GameFolder : null;

    /// <summary>Where a save of a course opened from <paramref name="openedFrom"/> goes. A game-folder course
    /// goes back to the game folder, null when the launch reads none. A shipped or new (null) one goes to
    /// <paramref name="buildTarget"/>, the build's <see cref="SaveTarget(bool, bool)"/>.</summary>
    public static CourseSource? SaveTarget(CourseSource? buildTarget, CourseSource? openedFrom, bool hasGameFolder) =>
        openedFrom == CourseSource.GameFolder ? (hasGameFolder ? CourseSource.GameFolder : null) : buildTarget;

    /// <summary><paramref name="gate"/> moved to stand over <paramref name="ground"/>. A pylon gate stands on it;
    /// a ring keeps its height above the ground, which was <paramref name="groundUnderOld"/> where it stood.</summary>
    public static CourseGate MovedTo(CourseGate gate, Vector3 ground, float groundUnderOld) =>
        gate with
        {
            Position = gate.Kind == CourseGateKind.PylonGate
                ? ground
                : ground with { Y = ground.Y + gate.Position.Y - groundUnderOld },
        };

    /// <summary><paramref name="ring"/> with its centre <paramref name="aboveM"/> over ground at
    /// <paramref name="groundY"/>. A ring stores world Y, so the height above ground is read back as the difference.</summary>
    public static CourseGate WithAboveGround(CourseGate ring, float aboveM, float groundY) =>
        ring with { Position = ring.Position with { Y = groundY + aboveM } };

    /// <summary>The middle of <paramref name="gate"/>'s opening at rest, in world space. The heading turns about
    /// the vertical the centre stands on, so it is the position plus <see cref="CourseGateShape.OpeningCentre"/>.</summary>
    public static Vector3 Centre(CourseGate gate) => gate.Position + CourseGateShape.OpeningCentre(gate);

    /// <summary>The horizontal way <paramref name="entry"/> flies through <paramref name="gate"/>.</summary>
    public static Vector3 FlownWay(CourseGate gate, SequenceEntry entry) =>
        entry.Direction == GateDirection.Forward ? gate.Forward : -gate.Forward;

    /// <summary>The 1-based numbers of the sequence entries naming <paramref name="gateId"/>, as "2, 5"; empty
    /// when none does.</summary>
    public static string EntryNumbers(Course course, string gateId) =>
        string.Join(", ", Enumerable.Range(0, course.Sequence.Count).Where(i => course.Sequence[i].GateId == gateId).Select(i => i + 1));

    /// <summary>Replaces the course whole, as Open and New do, and clears the selection.</summary>
    public void Load(Course course)
    {
        Course = course;
        SelectedId = null;
        SelectedEntry = -1;
        Changed?.Invoke();
    }

    /// <summary>Selects sequence entry <paramref name="index"/> and its gate, or nothing when it is out of range.</summary>
    public void SelectEntry(int index)
    {
        bool valid = index >= 0 && index < Course.Sequence.Count;
        SelectedEntry = valid ? index : -1;
        SelectedId = valid ? Course.Sequence[index].GateId : null;
        Changed?.Invoke();
    }

    /// <summary>Inserts an entry flying pool gate <paramref name="gateId"/> <paramref name="direction"/> after the
    /// selected entry (at the end with none) and selects it. A course turns back this way. Returns false
    /// when the pool lacks the gate.</summary>
    public bool AddEntry(string gateId, GateDirection direction)
    {
        if (Course.Gates.All(g => g.Id != gateId))
        {
            return false;
        }

        int at = InsertAt;
        var sequence = Course.Sequence.ToList();
        sequence.Insert(at, new SequenceEntry(gateId, direction));
        Course = Course with { Sequence = sequence };
        SelectedEntry = at;
        SelectedId = gateId;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Moves entry <paramref name="index"/> one place earlier (<paramref name="by"/> -1) or later (+1) and
    /// selects it there. Returns false past either end.</summary>
    public bool MoveEntry(int index, int by)
    {
        int to = index + by;
        if (Math.Abs(by) != 1 || index < 0 || index >= Course.Sequence.Count || to < 0 || to >= Course.Sequence.Count)
        {
            return false;
        }

        var sequence = Course.Sequence.ToList();
        (sequence[index], sequence[to]) = (sequence[to], sequence[index]);
        Course = Course with { Sequence = sequence };
        SelectedEntry = to;
        SelectedId = sequence[to].GateId;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Turns entry <paramref name="index"/> to fly its gate the other way and selects it. Returns false out
    /// of range.</summary>
    public bool Flip(int index)
    {
        if (index < 0 || index >= Course.Sequence.Count)
        {
            return false;
        }

        var sequence = Course.Sequence.ToList();
        var entry = sequence[index];
        sequence[index] = entry with { Direction = entry.Direction == GateDirection.Forward ? GateDirection.Reverse : GateDirection.Forward };
        Course = Course with { Sequence = sequence };
        SelectedEntry = index;
        SelectedId = entry.GateId;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Removes entry <paramref name="index"/>; its gate stays in the pool. The entry now at that place (the
    /// last, at the end) is selected with its gate, so several can be removed in a row. Returns false out of range.</summary>
    public bool RemoveEntry(int index)
    {
        if (index < 0 || index >= Course.Sequence.Count)
        {
            return false;
        }

        var sequence = Course.Sequence.ToList();
        sequence.RemoveAt(index);
        Course = Course with { Sequence = sequence };
        SelectedEntry = Math.Min(index, sequence.Count - 1);
        if (SelectedEntry >= 0)
        {
            SelectedId = sequence[SelectedEntry].GateId;
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>Selects pool gate <paramref name="id"/>, or nothing for null or an id the pool lacks.</summary>
    public void Select(string? id)
    {
        SelectedId = Course.Gates.Any(g => g.Id == id) ? id : null;
        Changed?.Invoke();
    }

    /// <summary>A new gate of <paramref name="kind"/> over <paramref name="ground"/> at <paramref name="heading"/>,
    /// default-sized, appended to the pool and selected. Its forward entry goes after the selected one (at the end
    /// with none) and is selected. A ring stands <see cref="RingAboveGroundM"/> up.</summary>
    public CourseGate Place(CourseGateKind kind, Vector3 ground, float heading)
    {
        string id = NextId(Course.Gates, kind);
        var gate = kind == CourseGateKind.PylonGate
            ? new CourseGate(id, kind, ground, heading, PylonWidthM, PylonHeightM, 0f)
            : new CourseGate(id, kind, ground + (Vector3.Up * RingAboveGroundM), heading, 0f, 0f, RingDiameterM);
        int at = InsertAt;
        var sequence = Course.Sequence.ToList();
        sequence.Insert(at, new SequenceEntry(id, GateDirection.Forward));
        Course = Course with { Gates = Course.Gates.Append(gate).ToList(), Sequence = sequence };
        SelectedId = id;
        SelectedEntry = at;
        Changed?.Invoke();
        return gate;
    }

    /// <summary>The start point <see cref="StartAboveGroundM"/> over <paramref name="ground"/> at <paramref name="heading"/>.</summary>
    public void PlaceStart(Vector3 ground, float heading)
    {
        SetStart(new SpawnPoint(ground + (Vector3.Up * StartAboveGroundM), heading));
    }

    /// <summary>The start point exactly at <paramref name="start"/>.</summary>
    public void SetStart(SpawnPoint start)
    {
        Course = Course with { Start = start };
        Changed?.Invoke();
    }

    /// <summary>Stands <paramref name="gate"/> in place of the pool gate with its id. Returns false when none has it.</summary>
    public bool Update(CourseGate gate)
    {
        int i = Course.Gates.ToList().FindIndex(g => g.Id == gate.Id);
        if (i < 0)
        {
            return false;
        }

        var gates = Course.Gates.ToList();
        gates[i] = gate;
        Course = Course with { Gates = gates };
        Changed?.Invoke();
        return true;
    }

    /// <summary>Removes pool gate <paramref name="id"/> and every sequence entry naming it. Returns false when none has it.</summary>
    public bool Delete(string id)
    {
        if (Course.Gates.All(g => g.Id != id))
        {
            return false;
        }

        // The selected entry keeps its place among the entries that stay; it goes with the gate it names.
        int keptBefore = Course.Sequence.Take(Math.Max(SelectedEntry, 0)).Count(e => e.GateId != id);
        bool entryGoes = SelectedEntry >= 0 && Course.Sequence[SelectedEntry].GateId == id;
        Course = Course with
        {
            Gates = Course.Gates.Where(g => g.Id != id).ToList(),
            Sequence = Course.Sequence.Where(e => e.GateId != id).ToList(),
        };
        SelectedEntry = SelectedEntry < 0 || entryGoes ? -1 : keptBefore;
        if (SelectedId == id)
        {
            SelectedId = null;
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>Renames the course, which is its file name on the next save.</summary>
    public void Rename(string name)
    {
        Course = Course with { Name = name };
        Changed?.Invoke();
    }

    /// <summary>Sets the title a player sees; blank shows the name. The file name and bests stay.</summary>
    public void Retitle(string title)
    {
        Course = Course with { Title = title };
        Changed?.Invoke();
    }
}
