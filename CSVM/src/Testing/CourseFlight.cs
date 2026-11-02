using System;
using System.Linq;
using CSVM.Flight.Airframe;
using CSVM.Flight.Modes;
using CSVM.Launch;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>A course flown by placement in built sessions: an aircraft set just short of an
/// entry's gate and stepped through it, every session stepping together.</summary>
internal static class CourseFlight
{
    // How far short of a gate the aircraft is placed, and how many steps it may take to cross.
    private const float LeadM = 8f;
    private const int CrossSteps = 20;

    // Generous against the start gate, the opening's five seconds and any retransmission.
    private const int StepLimit = 1200;

    /// <summary>The aircraft placed short of the entry's gate centre and flown through it the
    /// entry's way, at the gate's pose now. True once the run counts a further entry.</summary>
    internal static bool Through(GameSession[] peers, FlightController plane, StuntMission run, Course course, SequenceEntry entry)
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

    /// <summary>Steps every session until <paramref name="condition"/> holds or the step limit runs
    /// out, and answers it.</summary>
    internal static bool Until(GameSession[] peers, Func<bool> condition)
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
