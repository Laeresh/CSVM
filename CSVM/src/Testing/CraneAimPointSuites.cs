using System.Text;
using CSVM.Flight.Airframe;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Objectives;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>CM15's three towing cranes over C2/M05's own built world. Each crane's pool stands on
/// <c>zcraneN</c>, whose origin is the joint box on the zeppelin. It takes its damage on the
/// <c>healthy</c> child, the yellow striped arm the target table names and the marker draws on.
/// The gun assist must solve to the marker's point. The original publishes one point per mission
/// structure, and every reader takes it (docs/org/targeting.md "Where a mission structure
/// is").</summary>
internal static class CraneAimPointSuites
{
    private const string Chapter = "C2";
    private const string Mission = "M05";

    // How far the assist's point may sit from the marker's. The two walk the same meshes, one in
    // the arm's frame and one in world space, so they differ only by float rounding.
    private const float Tolerance = 0.5f;

    // The striped arm hangs 23 m below its crane's origin in the authored scene. A point this
    // close to the origin is the joint box, not the arm.
    private const float ArmDrop = 10f;

    private static readonly string[] Cranes = { "zcrane0", "zcrane1", "zcrane2" };

    [Suite("cm15-crane-aim-point",
        "CM15's three towing cranes over C2/M05's built world: each crane's gun-assist structure "
        + "candidate stands where the mission's own marker for zcraneN/healthy stands, the centre "
        + "of the yellow striped arm, and not at the zcraneN origin in the joint box on the "
        + "zeppelin; the assist's scan from a gun pointed at the marker then fires along that line "
        + "with no lead offset, where the old origin point pulled it about 23 m high")]
    internal static void CraneAimPoint(TestContext ctx)
    {
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, Chapter, Mission);
        ctx.RequireData(missionZrdr, $"{Chapter}/{Mission} zrdr");
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, Chapter), $"{Chapter} textures");
        var report = new StringBuilder();
        ctx.WithWorld(Chapter, collision: false, Mission, world =>
        {
            int checkedCranes = 0;
            foreach (var crane in Cranes)
            {
                checkedCranes += CheckCrane(ctx, world, crane, report) ? 1 : 0;
            }

            ctx.Same(Cranes.Length, checkedCranes,
                $"all {Cranes.Length} cranes resolve to a striped arm and a pool standing on it");
        });
        ctx.WriteArtifact("test-cm15-crane-aim-point.txt", report.ToString());
        ctx.Note($"{Chapter}/{Mission}: the gun assist leads each crane's striped arm, where its marker is");
    }

    private static bool CheckCrane(TestContext ctx, TestWorld world, string crane, StringBuilder report)
    {
        var arm = ObjectiveSites.ResolveTarget(world.Runtime, ObjectiveTarget.Parse($"{crane}/healthy"));
        var pool = arm != null ? world.Runtime.Destructibles.Resolve(arm) : null;
        ctx.Check(arm != null && pool != null && ReferenceEquals(pool.DamageNode, arm),
            $"{crane}: '{crane}/healthy' resolves, and the pool a hit on it reaches takes its damage there");
        if (arm == null || pool == null)
        {
            return false;
        }

        var marker = ObjectiveSites.SiteAnchor(arm);
        var origin = pool.Anchor.GlobalPosition;
        var set = new AimCandidateSet();
        set.AddStructures(world.Runtime.Destructibles);
        AimCandidate? found = null;
        foreach (var c in set.Structures)
        {
            if (ReferenceEquals(c.Source, pool))
            {
                found = c;
            }
        }

        string at = found is { } f
            ? Log.Format($"({f.Position.X:0.0},{f.Position.Y:0.0},{f.Position.Z:0.0})")
            : "none";
        report.AppendLine(Log.Format($"{crane}: marker ({marker.X:0.0},{marker.Y:0.0},{marker.Z:0.0}), origin ({origin.X:0.0},{origin.Y:0.0},{origin.Z:0.0}), candidate {at}, team {pool.Team?.ToString() ?? "none"}"));
        ctx.Check(origin.DistanceTo(marker) > ArmDrop,
            $"CONTROL: {crane}'s origin stands {origin.DistanceTo(marker):0.0} m from its striped arm's marker, so the two points are told apart");
        ctx.Check(found != null, $"{crane}: its pool is offered as a structure candidate");
        if (found is not { } candidate)
        {
            return false;
        }

        ctx.Check(candidate.Position.DistanceTo(marker) < Tolerance,
            $"{crane}: the gun assist's point stands on the striped arm's marker, {candidate.Position.DistanceTo(marker):0.00} m off it and {candidate.Position.DistanceTo(origin):0.0} m from the joint origin");
        bool read = FlightController.TryTargetGeometry(pool, out var aiAt, out _, out _, out _);
        ctx.Check(read && aiAt.DistanceTo(marker) < Tolerance,
            $"{crane}: a gunner holding the pool as its target reads the same point, {aiAt.DistanceTo(marker):0.00} m off the marker");
        CheckScan(ctx, crane, candidate, marker, report);
        return true;
    }

    // A gun 300 m off the marker points at it, on a team hostile to the crane's. With only this
    // crane on the list, the winning direction is the line to the marker itself.
    private static void CheckScan(TestContext ctx, string crane, AimCandidate candidate,
        Vector3 marker, StringBuilder report)
    {
        var muzzle = marker + new Vector3(0f, 0f, 300f);
        var set = new AimCandidateSet();
        int team = candidate.Team == AimAssist.NeutralTeam ? 2 : candidate.Team;
        set.AddStructure(candidate.Position, team, live: true, candidate.Source);
        var scan = new AimScan
        {
            MuzzlePosition = muzzle,
            Forward = (marker - muzzle).Normalized(),
            Team = team == AimAssist.PlayerTeam ? 2 : AimAssist.PlayerTeam,
            Speed = 400f,
            RangeSquared = 1000f * 1000f,
            ConeCos = Mathf.Cos(Mathf.DegToRad(6f)),
        };
        bool hit = AimAssist.Scan(scan, set, out var best);
        float offBy = hit ? Mathf.RadToDeg(best.Direction.AngleTo(scan.Forward)) : float.NaN;
        report.AppendLine(Log.Format($"{crane}: scan found={hit} off the marker line by {offBy:0.000} deg"));
        ctx.Check(hit && offBy < 0.1f,
            $"{crane}: the assist fires along the line to the striped arm, {offBy:0.000} deg off it");
    }
}
