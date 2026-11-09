using System.Collections.Generic;
using CSVM.Extraction;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using Godot;

namespace CSVM.Testing;

/// <summary>A two-player stunt race over C1/IA1's authored Danger Zones, driven through the same
/// per-pilot <see cref="TargetSelection"/> the pilot HUD reads. Each pane cycles its own zones and one
/// pane's step never moves the other's marker. A pilot who clears the course empties their own
/// cycle alone. Also pins the marker the zone draws, which is the objective marker every other
/// site takes rather than a style of its own. A race run's zone-first rule is pinned over a
/// two-gate course beside two AI racers, against a solo run that keeps the decoded cycle.</summary>
internal static class StuntTargetSuites
{
    private const string Chapter = "C1";
    private const string Mission = "IA1";

    // The AI racers' side, any team but the pilot's own.
    private const int Hostile = AimAssist.PlayerTeam + 1;

    // A non-destructive objective's own label, the colour class a Danger Zone falls in
    // (Target::GetColor, docs/org/targeting.md): anything but Destroy/Disable/Disable Engines/Damage.
    private const string ProtectLabel = "Protect";

    // Two poses over C1's map, one per pilot. They differ on purpose: the cycle is sorted against
    // each plane's own basis and position, so two pilots racing one course see two orders.
    private static readonly Vector3 P1Pose = new(-5000f, 300f, -6000f);
    private static readonly Vector3 P2Pose = new(-7000f, 300f, -2500f);

    // A two-gate course whose start pose faces -Z down it. Two AI racers sit on the Enemy cycle:
    // Lead 100 m ahead, Wing off the left wing. The decoded cycle is the gate, Lead, Wing.
    private static readonly Vector3 GridPose = new(0f, 15f, 300f);
    private static readonly string Lead = "Lead";
    private static readonly string Wing = "Wing";
    private static readonly CourseGate GateA = new("a", CourseGateKind.PylonGate, Vector3.Zero, 0f, 40f, 30f, 0f);
    private static readonly CourseGate GateB = new("b", CourseGateKind.BalloonRing, new Vector3(0f, 50f, -200f), 0f, 0f, 0f, 20f);

    [Suite("stunt-target-cycle",
        "a two-player stunt race over C1/IA1's danger zones on the pilots' own target cycles: "
        + "every unflown zone is an objective-class entry labelled 'Danger Zone [Fly Through] - "
        + "<name>' in the non-destructive objective's colour, each pane steps its own zones, one "
        + "pane's step leaves the other's marker alone, and clearing the course empties that "
        + "pilot's cycle and nobody else's")]
    internal static void StuntTargetCycle(TestContext ctx)
    {
        ZrdrSet missionZrdr = ctx.MissionZrdr(Chapter, Mission);
        ctx.RequireData(missionZrdr, $"{Chapter}/{Mission} zrdr");
        ctx.RequireMessages();

        ctx.WithWorld(Chapter, collision: false, Mission, world =>
        {
            var mine = StuntMission.Load(world.Gamez, missionZrdr, ctx.Data.Messages);
            ctx.Check(mine != null, $"{Chapter}/{Mission} ships danger zones for a stunt run");
            if (mine is not { } p1Run)
            {
                return;
            }

            var p2Run = p1Run.ForAnotherPlayer();
            var p1 = Selection(p1Run, P1Pose);
            var p2 = Selection(p2Run, P2Pose);

            ctx.Check(p1.Pool.Enemy.Count == p1Run.TotalCount
                      && p2.Pool.Enemy.Count == p2Run.TotalCount,
                $"each pilot's Enemy/Objective cycle holds all {p1Run.TotalCount} unflown zones");
            ctx.Check(AllObjectives(p1.Pool.Enemy) && AllObjectives(p2.Pool.Enemy),
                $"every zone rides the cycle as an OBJECTIVE, the class the original's marker is");
            CheckMarker(ctx, p1);

            // The auto-acquire: with the class flags non-zero from the start each pilot is already
            // pointed at the head of their own cycle, which is the nearest zone to them.
            var p1First = p1.Current;
            var p2First = p2.Current;
            ctx.Check(p1First is { } a && p2First is { } b && !a.IsSameTarget(b),
                $"each pane auto-acquires ITS OWN zone object: P1 '{Named(p1First)}', P2 '{Named(p2First)}'");

            // One pane's step. The other pilot's marker must not move: a stunt marker is per
            // player, and a unification that made every pane agree would have broken the feature.
            Step(p1, P1Pose);
            ctx.Check(p1.Current is { } stepped && p1First is { } was && !stepped.IsSameTarget(was),
                $"P1's step moves P1's marker: '{Named(p1First)}' to '{Named(p1.Current)}'");
            ctx.Check(p2.Current is { } held && p2First is { } start && held.IsSameTarget(start),
                $"…and leaves P2's marker on '{Named(p2.Current)}'");

            // Walking the whole cycle comes back to where it started, so the one action covers
            // every zone rather than needing a second key for the rest of them.
            for (int i = 1; i < p1Run.TotalCount; i++)
            {
                Step(p1, P1Pose);
            }

            ctx.Check(p1.Current is { } wrapped && p1First is { } head && wrapped.IsSameTarget(head),
                $"P1's step walks all {p1Run.TotalCount} zones and wraps back to '{Named(p1First)}'");

            // A cleared zone leaves the cycle. P2 clears the whole course; P1's is untouched.
            p2Run.DebugCompleteAll();
            Rebuild(p2, p2Run, P2Pose);
            Rebuild(p1, p1Run, P1Pose);
            ctx.Check(p2.Pool.Enemy.Count == 0 && p2.Current == null,
                $"P2's cleared course leaves P2 nothing to point at");
            ctx.Check(p1.Pool.Enemy.Count == p1Run.TotalCount && p1.Current != null,
                $"…and P1 still holds all {p1Run.TotalCount} zones with a marker on '{Named(p1.Current)}'");
            ctx.Note($"{Chapter}/{Mission}: {p1Run.TotalCount} zones on two independent cycles");
        });
    }

    [Suite("stunt-race-zone-first",
        "a race run's Next Enemy takes the next zone in one press from any plane, ahead of a plane "
        + "that just shot at the pilot, and from a cleared selection ahead of a nearer hostile round; "
        + "passing a zone selects the next one with a plane or the zone selected and Nearest After "
        + "Kill on or off; outside a race the decoded cycle, the attacker queue, the round at the "
        + "head and the kept or nearest pick on a passing keep their non-race rules")]
    internal static void StuntRaceZoneFirst(TestContext ctx)
    {
        foreach (bool race in new[] { true, false })
        {
            string mode = race ? "race" : "solo";
            var (run, sel) = NewRun(race, nearestAfterKill: false);
            object zone = run.Zones[0];
            ctx.Check(Is(sel, zone), $"[{mode}] the run starts on the first zone ({Picked(sel)})");

            Press(sel, run, previous: false);
            ctx.Check(Is(sel, Lead), $"[{mode}] Next on the zone steps on to the plane ahead ({Picked(sel)})");

            Press(sel, run, previous: false);
            object fromPlane = race ? zone : Wing;
            ctx.Check(Is(sel, fromPlane), $"[{mode}] Next from that plane takes {Label(fromPlane)} ({Picked(sel)})");

            if (race)
            {
                Press(sel, run, previous: true);
            }

            sel.RecordAttacker(Lead);
            Press(sel, run, previous: false);
            object afterShot = race ? zone : Lead;
            ctx.Check(Is(sel, afterShot), $"[{mode}] Next from Wing just after Lead shot at the pilot takes {Label(afterShot)} ({Picked(sel)})");

            // A hostile round nearer than the zone takes the decoded head, which tells the two apart.
            // The pool admits a round only by its TARGETABLE flyout, and a flyout needs a pool.
            var rounds = new ProjectilePool(null!, null, null);
            try
            {
                var round = new ProjectilePool.Flyout(new WeaponDef { Id = "wep_14", Targetable = true }, 0, rounds);
                var (clearRun, clearSel) = NewRun(race, nearestAfterKill: false);
                clearSel.Clear();
                RebuildRace(clearSel, clearRun, round);
                clearSel.NextEnemy();
                RebuildRace(clearSel, clearRun, round);
                object fromCleared = race ? clearRun.Zones[0] : round;
                ctx.Check(Is(clearSel, fromCleared), $"[{mode}] Next from a cleared selection takes {Label(fromCleared)} ({Picked(clearSel)})");
            }
            finally
            {
                rounds.Free();
            }

            foreach (bool nearest in new[] { false, true })
            {
                foreach (bool onPlane in new[] { false, true })
                {
                    var (passRun, passSel) = NewRun(race, nearest);
                    if (onPlane)
                    {
                        Press(passSel, passRun, previous: true);
                    }

                    PassFirstZone(passRun);
                    RebuildRace(passSel, passRun);
                    object next = passRun.Zones[1];
                    object expected = race ? next : onPlane ? Wing : nearest ? Lead : next;
                    ctx.Check(passRun.CompletedCount == 1 && Is(passSel, expected),
                        $"[{mode}] passing a zone with {(onPlane ? "Wing" : "the zone")} selected and Nearest After Kill {(nearest ? "on" : "off")} selects {Label(expected)} ({Picked(passSel)}, {passRun.CompletedCount} passed)");
                }
            }
        }
    }

    private static (StuntMission Run, TargetSelection Selection) NewRun(bool race, bool nearestAfterKill)
    {
        var course = new Course("c1", "race", new SpawnPoint(GridPose, 0f), new[] { GateA, GateB },
            new[] { new SequenceEntry(GateA.Id, GateDirection.Forward), new SequenceEntry(GateB.Id, GateDirection.Forward) });
        var run = StuntMission.ForCourse(course)!;
        var selection = new TargetSelection { ZoneFirst = race, NearestAfterKill = nearestAfterKill };
        RebuildRace(selection, run);
        return (run, selection);
    }

    private static void Press(TargetSelection selection, StuntMission run, bool previous)
    {
        if (previous)
        {
            selection.Previous(TargetClass.Enemy);
        }
        else
        {
            selection.NextEnemy();
        }

        RebuildRace(selection, run);
    }

    private static void RebuildRace(TargetSelection selection, StuntMission run, object? round = null)
    {
        var planes = new AimCandidateSet();
        if (round != null)
        {
            planes.AddOrdnance(GridPose + new Vector3(0f, 0f, -50f), Vector3.Zero, Hostile, round);
        }

        planes.AddVehicle(GridPose + new Vector3(0f, 0f, -100f), Vector3.Zero, Hostile, live: true, Lead);
        planes.AddVehicle(GridPose + new Vector3(-100f, 0f, 0f), Vector3.Zero, Hostile, live: true, Wing);
        var gates = new List<AimCandidate>();
        run.CollectTargets(gates);
        selection.Rebuild(planes, null, AimAssist.PlayerTeam, null, GridPose, Basis.Identity, gates);
    }

    // One step straight through the first zone's course gate, the way it is flown.
    private static void PassFirstZone(StuntMission run)
    {
        var centre = GateA.Position + new Vector3(0f, GateA.Height / 2f, 0f);
        var along = GateA.Forward * 5f;
        run.Relocated();
        run.Update(centre - along);
        run.Update(centre + along);
    }

    private static bool Is(TargetSelection selection, object source) =>
        ReferenceEquals(selection.Current?.Source, source);

    private static string Picked(TargetSelection selection) => Label(selection.Current?.Source);

    private static string Label(object? source) =>
        source switch
        {
            StuntZone zone => zone.Description,
            ProjectilePool.Flyout round => round.Name,
            _ => source?.ToString() ?? "nothing",
        };

    private static bool AllObjectives(IReadOnlyList<TargetRef> cycle)
    {
        foreach (var target in cycle)
        {
            if (!target.Objective || target.Kind != AimTargetKind.Structure)
            {
                return false;
            }
        }

        return cycle.Count > 0;
    }

    private static string Named(TargetRef? target) => target is { } t ? t.DisplayName : "nothing";

    // The marker the selected zone draws, through the same two statics TargetHud draws every
    // objective with: the original's three label lines and Target::GetColor's own answer.
    private static void CheckMarker(TestContext ctx, TargetSelection selection)
    {
        if (selection.Current is not { } target)
        {
            ctx.Check(false, $"the pilot has a zone selected to draw");
            return;
        }

        var lines = new List<string>(3);
        TargetHud.LabelLines(target, "7 o'clock", lines, keepSlots: false);
        ctx.Check(lines.Count == 3 && lines[0] == "Danger Zone [Fly Through] -"
                  && lines[1] == target.DisplayName && lines[2] == "7 o'clock",
            $"the zone's marker is the original's own three lines: [{string.Join(" | ", lines)}]");

        var protect = TargetRef.ForStructure(target.Candidate, TargetClass.Enemy, target.Name,
            "Zeppelin", ProtectLabel, objective: true);
        ctx.Check(TargetHud.MarkerColor(target, AimAssist.PlayerTeam)
                  == TargetHud.MarkerColor(protect, AimAssist.PlayerTeam),
            $"…in the non-destructive objective's colour, the one a {ProtectLabel} marker takes");
    }

    // One press of the Enemy/Objective step, then the re-resolve that publishes it. Separate calls
    // because the original's handler steps the list the last frame built and the next frame's pass
    // is what shows the result; the pose is the rebuild's, so the sort cannot move underneath.
    private static void Step(TargetSelection selection, Vector3 at)
    {
        selection.NextEnemy();
        selection.Resolve(at, Basis.Identity);
    }

    private static TargetSelection Selection(StuntMission run, Vector3 at)
    {
        var selection = new TargetSelection();
        Rebuild(selection, run, at);
        return selection;
    }

    private static void Rebuild(TargetSelection selection, StuntMission run, Vector3 at)
    {
        var zones = new List<AimCandidate>();
        run.CollectTargets(zones);
        selection.Rebuild(new AimCandidateSet(), null, AimAssist.PlayerTeam, null, at,
            Basis.Identity, zones);
    }
}
