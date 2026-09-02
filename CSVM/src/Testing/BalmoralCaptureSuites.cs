using System.Collections.Generic;
using System.Text;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>The suite over CM15's capture cutscene: C2/M05's paratrooper drop is rooted on the
/// aircraft archive's <c>balmoral</c>, reparented onto the placed <c>cargozep2</c> for its own
/// shot and moved through it, so staging that node is what lets the cutscene camera frame the
/// aeroplane it is filmed around. Decode: docs/formats/anim-definitions/cutscenes.md.</summary>
internal static class BalmoralCaptureSuites
{
    private const int MissionSeq = 14; // CM15, C2/M05.
    private const string DropAnim = "drop_paratroopers";
    private const string DropZeppelin = "cargozep2";

    // C3/M01: a mission that stages an aircraft (its own chuteman drop-off needs one) but never
    // names 'balmoral' in any of its own definitions, the same position dropoff-chuteman-stage
    // and IntroAircraftSuites drive.
    private const int OtherMissionSeq = 0;

    // A few seconds of that mission's own bootstrap and start anims, long enough for anything
    // that might incidentally reach the node to have had its chance.
    private const int OtherMissionDriveFrames = 300;

    private const float StepDt = 1f / 60f;

    // The camera's own SI-script shot and the 6 s OBJECT_MOTION_FROM_TO run concurrently; the
    // budget clears both several times over.
    private const float DriveBudgetS = 15f;

    // The widest half-angle a shot can frame, IntroWingmanSuites' own reading: an authored FOV
    // narrower than this puts a posed prop off screen at any of them.
    private const float FrustumHalfAngleDeg = 60f;

    // A node still at the archive's own build origin has not been posed by anything.
    private const float PosedMinM = 10f;

    [Suite("campaign-cm15-capture",
        "CM15's capture cutscene over C2/M05's BUILT world: the paratrooper drop is rooted on "
        + "the aircraft archive's 'balmoral', and staging that node lets the drop's own "
        + "OBJECT_ADD_CHILD/OBJECT_MOTION_FROM_TO pair reparent it onto 'cargozep2', pose it and "
        + "draw it inside the cutscene camera's frustum through the shot, then hand it back to "
        + "the world root; the pilot's own re-placement is cutscene-handoff-unposed's, unchanged "
        + "here")]
    internal static void Cm15Capture(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var mission = MissionOf(CampaignSequence.Load(ctx.ZrdrPath), MissionSeq)
            ?? throw new SuiteSkippedException($"cm_sequence carries no story position {MissionSeq}");
        string chapter = mission.ChapterFolder.ToUpperInvariant();
        string folder = mission.MissionFolder.ToUpperInvariant();
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, chapter, folder);
        ctx.RequireData(missionZrdr, $"{chapter}/{folder} zrdr");
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, chapter), $"{chapter} textures");
        ctx.RequireData(ctx.PlanesGamezPath, $"aircraft archive");

        var report = new StringBuilder();
        report.AppendLine($"seq {MissionSeq} -> {chapter}/{folder}");
        ctx.CutsceneRoots = true;
        try
        {
            ctx.WithWorld(chapter, collision: false, folder, world => Drive(ctx, world, report));
        }
        finally
        {
            ctx.CutsceneRoots = false;
        }

        ctx.WriteArtifact($"test-cm15-capture-{chapter}-{folder}.txt", report.ToString());
        ctx.Note($"drove {chapter}/{folder}'s '{DropAnim}' and watched the Balmoral through the shot");
    }

    /// <summary>Drives C3/M01, a mission with no <c>balmoral</c> definition at all, over its BUILT
    /// world: the staged node ships ACTIVE, so it must sit under a switched-off holder rather than
    /// the world root, or it draws at the archive's own build origin in every mission that stages
    /// an aircraft, never posed and never asked for. Regression cover for a hash the
    /// unconditionally-visible staging moved once already.</summary>
    [Suite("campaign-balmoral-hidden",
        "nothing draws at the world origin when no definition asks for it, over C3/M01's BUILT "
        + "world: that mission stages an aircraft (its own chuteman drop-off needs one) but never "
        + "names 'balmoral', so the archive's own ACTIVE node must stay undrawn under its "
        + "switched-off holder through the mission's own bootstrap and start anims, at the "
        + "position it was built at")]
    internal static void Cm15BalmoralHidden(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var mission = MissionOf(CampaignSequence.Load(ctx.ZrdrPath), OtherMissionSeq)
            ?? throw new SuiteSkippedException($"cm_sequence carries no story position {OtherMissionSeq}");
        string chapter = mission.ChapterFolder.ToUpperInvariant();
        string folder = mission.MissionFolder.ToUpperInvariant();
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, chapter, folder);
        ctx.RequireData(missionZrdr, $"{chapter}/{folder} zrdr");
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, chapter), $"{chapter} textures");
        ctx.RequireData(ctx.PlanesGamezPath, $"aircraft archive");

        var report = new StringBuilder();
        report.AppendLine($"seq {OtherMissionSeq} -> {chapter}/{folder}");
        ctx.CutsceneRoots = true;
        try
        {
            ctx.WithWorld(chapter, collision: false, folder, world => DriveHidden(ctx, world, report));
        }
        finally
        {
            ctx.CutsceneRoots = false;
        }

        ctx.WriteArtifact($"test-cm15-balmoral-hidden-{chapter}-{folder}.txt", report.ToString());
        ctx.Note($"drove {chapter}/{folder} and confirmed 'balmoral' stays undrawn there");
    }

    private static void DriveHidden(TestContext ctx, TestWorld world, StringBuilder report)
    {
        if (world.Session.Aircraft is not { Balmoral: { } balmoral })
        {
            ctx.Check(false,
                $"the world build staged the archive's '{AircraftStage.BalmoralNode}' node here too");
            return;
        }

        ctx.Check(world.Session.Program.ByAnimName(DropAnim).Count == 0,
            $"{world.Chapter} compiles no '{DropAnim}' definition, so nothing here ever names '{AircraftStage.BalmoralNode}'");

        var startAt = balmoral.GlobalPosition;
        int drawn = 0;
        for (int i = 0; i < OtherMissionDriveFrames; i++)
        {
            world.Runtime.Advance(StepDt);
            if (balmoral.IsVisibleInTree())
            {
                drawn++;
            }
        }

        var endAt = balmoral.GlobalPosition;
        report.AppendLine($"'{AircraftStage.BalmoralNode}' over {OtherMissionDriveFrames} frame(s): "
            + $"drawn {drawn}, started at {startAt}, ended at {endAt}");

        ctx.Check(drawn == 0,
            $"'{AircraftStage.BalmoralNode}' is never drawn over the mission's own bootstrap and start anims (drawn {drawn} frame(s))");
        ctx.Check(startAt.IsEqualApprox(endAt),
            $"and stays exactly where it was built ({startAt} -> {endAt}), reparented by nothing");
    }

    private static void Drive(TestContext ctx, TestWorld world, StringBuilder report)
    {
        if (world.Session.Aircraft is not { Balmoral: { } balmoral })
        {
            ctx.Check(false,
                $"the world build staged the archive's '{AircraftStage.BalmoralNode}' node the drop poses");
            return;
        }

        ctx.Check(world.Session.Program.ByAnimName(DropAnim).Count > 0,
            $"{world.Chapter} compiles '{DropAnim}'");

        var camera = world.Runtime.FindNodes(CutsceneController.CameraNode);
        if (camera.Count == 0)
        {
            ctx.Check(false, $"the world build stood up '{CutsceneController.CameraNode}'");
            return;
        }

        var zeppelin = world.Runtime.FindNodes(DropZeppelin);
        if (zeppelin.Count == 0)
        {
            ctx.Check(false, $"{world.Chapter} places '{DropZeppelin}', the shot's own anchor");
            return;
        }

        var startAt = balmoral.GlobalPosition;
        int frames = 0, drawn = 0, posed = 0, framed = 0, onZeppelin = 0;
        float bestAngle = 999f;
        var bestAt = startAt;
        var lastAt = startAt;
        world.Runtime.PlayMissionTrigger(DropAnim);
        for (float t = 0f; t < DriveBudgetS; t += StepDt)
        {
            world.Runtime.Advance(StepDt);
            frames++;
            lastAt = balmoral.GlobalPosition;
            bool isDrawn = balmoral.IsVisibleInTree();
            bool isPosed = lastAt.DistanceTo(startAt) > PosedMinM;
            float angle = AngleOffAxis(camera[0], lastAt);
            if (isDrawn)
            {
                drawn++;
            }

            if (isDrawn && isPosed)
            {
                posed++;
            }

            if (isDrawn && isPosed && angle <= FrustumHalfAngleDeg)
            {
                framed++;
            }

            if (ReferenceEquals(balmoral.GetParent(), zeppelin[0]))
            {
                onZeppelin++;
            }

            if (angle < bestAngle)
            {
                bestAngle = angle;
                bestAt = lastAt;
            }
        }

        bool endsOffZeppelin = !ReferenceEquals(balmoral.GetParent(), zeppelin[0]);
        report.AppendLine($"'{DropAnim}' ran {frames} frame(s): drawn {drawn}, posed {posed}, "
            + $"framed {framed}, on '{DropZeppelin}' {onZeppelin}, ends off it {endsOffZeppelin}");
        report.AppendLine($"'{AircraftStage.BalmoralNode}' started at {startAt}, ended at {lastAt}, "
            + $"moved {lastAt.DistanceTo(startAt):0.#} m; best angle {bestAngle:0.#} deg at {bestAt}");

        ctx.Check(onZeppelin > 0,
            $"the drop's own OBJECT_ADD_CHILD reparents '{AircraftStage.BalmoralNode}' onto '{DropZeppelin}' for the shot");
        ctx.Check(posed > 0,
            $"and it is posed off the archive's own build origin, {lastAt.DistanceTo(startAt):0.#} m moved");
        ctx.Check(framed > frames / 2,
            $"drawn and inside the cutscene camera's frustum through most of the shot (best {bestAngle:0.#} deg off axis)");
        ctx.Check(endsOffZeppelin,
            $"and the drop's own OBJECT_DELETE_CHILD hands it back off '{DropZeppelin}' once the shot ends");
    }

    // The angle between the camera's forward axis and the ray to the point, 180 for a point
    // straight behind. Godot's camera looks down its local -Z.
    private static float AngleOffAxis(Node3D camera, Vector3 worldPoint)
    {
        var local = camera.GlobalTransform.AffineInverse() * worldPoint;
        if (local.Length() < 1e-3f)
        {
            return 0f;
        }

        return Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(-local.Normalized().Z, -1f, 1f)));
    }

    private static CampaignMission? MissionOf(IReadOnlyList<CampaignMission> missions, int seq)
    {
        foreach (var mission in missions)
        {
            if (mission.Seq == seq)
            {
                return mission;
            }
        }

        return null;
    }
}
