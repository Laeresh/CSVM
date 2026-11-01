using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Modes;
using Godot;

namespace CSVM.UI.Labs;

/// <summary>
/// The course the course lab is editing, and its selected gate. Every edit goes through here and raises
/// <see cref="Changed"/>, so the lab's drawing and panel, and any later tool on the selection, follow one
/// source. Engine-free: the lab hands in ground points and camera bases, and this decides what they become.
/// </summary>
public sealed class CourseDraft
{
    /// <summary>A new pylon gate's width and height, metres. TUNE: B11's montage sizes.</summary>
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

    /// <summary>Replaces the course whole, as Open and New do, and clears the selection.</summary>
    public void Load(Course course)
    {
        Course = course;
        SelectedId = null;
        Changed?.Invoke();
    }

    /// <summary>Selects pool gate <paramref name="id"/>, or nothing for null or an id the pool lacks.</summary>
    public void Select(string? id)
    {
        SelectedId = Course.Gates.Any(g => g.Id == id) ? id : null;
        Changed?.Invoke();
    }

    /// <summary>A new gate of <paramref name="kind"/> over <paramref name="ground"/> at <paramref name="heading"/>,
    /// default-sized, appended to the pool and flown forward at the end of the sequence, and selected. A ring
    /// stands <see cref="RingAboveGroundM"/> up.</summary>
    public CourseGate Place(CourseGateKind kind, Vector3 ground, float heading)
    {
        string id = NextId(Course.Gates, kind);
        var gate = kind == CourseGateKind.PylonGate
            ? new CourseGate(id, kind, ground, heading, PylonWidthM, PylonHeightM, 0f)
            : new CourseGate(id, kind, ground + (Vector3.Up * RingAboveGroundM), heading, 0f, 0f, RingDiameterM);
        Course = Course with
        {
            Gates = Course.Gates.Append(gate).ToList(),
            Sequence = Course.Sequence.Append(new SequenceEntry(id, GateDirection.Forward)).ToList(),
        };
        SelectedId = id;
        Changed?.Invoke();
        return gate;
    }

    /// <summary>The start point <see cref="StartAboveGroundM"/> over <paramref name="ground"/> at <paramref name="heading"/>.</summary>
    public void PlaceStart(Vector3 ground, float heading)
    {
        Course = Course with { Start = new SpawnPoint(ground + (Vector3.Up * StartAboveGroundM), heading) };
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

        Course = Course with
        {
            Gates = Course.Gates.Where(g => g.Id != id).ToList(),
            Sequence = Course.Sequence.Where(e => e.GateId != id).ToList(),
        };
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
}
