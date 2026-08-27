using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

/// <summary>The suite over where a mid-mission cutscene leaves the pilot: the definition parks its
/// <c>player</c> node at an authored world pose and raises the re-placement callback, and the
/// handoff has to fly the aeroplane out of THAT pose rather than out of the one it was staged from.
/// Decode: docs/formats/anim-definitions/cutscenes.md.</summary>
internal static class DropoffPlacementSuites
{
    // The story position driven: C3/M01, whose tex_drop.zrd is the campaign's first re-placing
    // cutscene (the same position IntroAircraftSuites and DropoffChutemanSuites drive).
    private const int MissionSeq = 0;

    // The callback the definition re-places the pilot with. Its own decode lives on the const in
    // CutsceneController; the suite names it to find the authored definition by data rather than
    // by a hard-coded animation name.
    private const int ReplaceCode = 951;

    private const string PlaneNode = "player_bhawk";
    private const float StepDt = 1f / 60f;

    // The drop's own scripts run 4.4 s and its camera 4.45 s; the budget clears both several times
    // over, and the loop leaves as soon as the episode hands off.
    private const float DriveBudgetS = 30f;

    // How closely the released aeroplane has to sit on the authored placement.
    private const float PlacedToleranceM = 1f;

    // How far the placement has to be from where the pilot flew in, or the assertion above would
    // pass on a build that re-places nothing.
    private const float MovedMinM = 100f;

    /// <summary>Drives C3/M01's drop-off definition directly (never the approach cone, so nothing
    /// INSTR-20 warns about is armed) against its BUILT world, with the pilot flown in at the world
    /// origin: the definition's own re-placement callback puts the aeroplane on the pose its
    /// <c>player</c> node was parked at, facing the way that node faces, at the original's release
    /// speed.</summary>
    internal static void DropoffPlacement(TestContext ctx)
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
            ctx.WithWorld(chapter, collision: false, folder,
                world => Drive(ctx, world, missionZrdr, report));
        }
        finally
        {
            ctx.CutsceneRoots = false;
        }

        ctx.WriteArtifact($"test-dropoff-placement-{chapter}-{folder}.txt", report.ToString());
        ctx.Note($"drove {chapter}/{folder}'s re-placing cutscene over a built world");
    }

    private static void Drive(TestContext ctx, TestWorld world, string missionZrdr, StringBuilder report)
    {
        var cutscenes = MissionCutscenes.AnimNames(missionZrdr);
        report.AppendLine($"cutscene definitions: {string.Join(", ", cutscenes)}");
        string? anim = ReplacingAnimOf(world, cutscenes);
        report.AppendLine($"re-placing definition: '{anim ?? "-"}' (callback {ReplaceCode})");
        ctx.Check(anim != null,
            $"the mission's own cutscenes directory carries a definition raising callback {ReplaceCode}");
        if (anim == null)
        {
            return;
        }

        if (world.Session.Aircraft is not { PlayerMarker: { } marker })
        {
            ctx.Check(false, $"the world build staged the '{AircraftStage.PlayerNode}' marker the drop poses");
            return;
        }

        var cutscene = new CutsceneController();
        ctx.Host.AddChild(cutscene);
        var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, world.Chapter));
        var pool = new ProjectilePool(textures, null, null);
        ctx.Host.AddChild(pool);
        try
        {
            var rig = BuildRig(ctx, world, pool);
            cutscene.BindWorld(world.Runtime, world.Session.Aircraft);
            cutscene.BindRigs(
                new[]
                {
                    new PlayerRig
                    {
                        Index = 0,
                        Camera = ctx.Camera,
                        HudParent = ctx.Host,
                        Controller = rig,
                    },
                },
                () => Array.Empty<FlightController>());
            cutscene.HostDefinitions(new[] { anim });
            world.Runtime.CallbackHost = cutscene.Host;
            RunTheDrop(ctx, world, cutscene, rig, marker, anim, report);
        }
        finally
        {
            cutscene.Free();
            pool.Free();
            textures.Dispose();
        }
    }

    // The drive itself. The rig is never stepped, so wherever it ends up is the cutscene's doing and
    // nothing else's; the placement is read off the marker on the frame the callback lands, which is
    // the pose the original's own host reads out of that node.
    private static void RunTheDrop(TestContext ctx, TestWorld world, CutsceneController cutscene,
        FlightController rig, Node3D marker, string anim, StringBuilder report)
    {
        var flewInAt = rig.GlobalTransform;
        Transform3D? placement = null;
        float played = 0f;
        world.Runtime.Play(anim);
        for (float t = 0f; t < DriveBudgetS && (played == 0f || cutscene.Playing); t += StepDt)
        {
            world.Runtime.Advance(StepDt);
            cutscene.Tick();
            played += cutscene.Playing ? StepDt : 0f;
            if (placement == null && cutscene.Codes.Contains(ReplaceCode))
            {
                placement = AnimRuntime.WorldTransform(marker, out _);
            }
        }

        report.AppendLine($"'{anim}' episode ran {played:0.##} s, codes {string.Join("/", cutscene.Codes)}");
        ctx.Check(played > 0f && !cutscene.Playing,
            $"'{anim}' runs to its handoff with the cutscene host answering its codes");
        ctx.Check(placement != null, $"and raises callback {ReplaceCode} on the way");
        if (placement is not { } placed)
        {
            return;
        }

        float moved = placed.Origin.DistanceTo(flewInAt.Origin);
        float off = rig.WorldPosition.DistanceTo(placed.Origin);
        float facing = (-rig.Attitude.Z).Dot(-placed.Basis.Orthonormalized().Z);
        report.AppendLine($"flew in at {flewInAt.Origin}, authored placement {placed.Origin}, "
            + $"{moved:0.#} m apart");
        report.AppendLine($"released at {rig.WorldPosition}, {off:0.##} m off the placement, "
            + $"facing dot {facing:0.###}, speed {rig.WorldVelocity.Length():0.##} m/s");
        ctx.Check(moved > MovedMinM,
            $"the authored placement is {moved:0} m from where the pilot flew in, so this can fail");
        ctx.Check(off < PlacedToleranceM,
            $"the handoff flies the aeroplane out of the pose the drop parked '{AircraftStage.PlayerNode}' at");
        ctx.Check(facing > 0.999f,
            $"facing the way that node faces, whatever heading the pilot flew in on");
        ctx.Check(rig.WorldVelocity.Length() > 1f,
            $"and moving, the release the original's own callback writes into the vehicle");
    }

    // The mission's own cutscene definition that raises the re-placement callback. Read out of the
    // authored events rather than named, so the suite says WHERE the placement is authored.
    private static string? ReplacingAnimOf(TestWorld world, IReadOnlyList<string> cutscenes)
    {
        foreach (string name in cutscenes)
        {
            foreach (var def in world.Session.Program.ByAnimName(name))
            {
                foreach (var seq in def.Sequences)
                {
                    foreach (var ev in seq.Events)
                    {
                        if (ev.Kind == "Callback" && (int)(ev.Data.Num("value") ?? -1f) == ReplaceCode)
                        {
                            return name;
                        }
                    }
                }
            }
        }

        return null;
    }

    private static FlightController BuildRig(TestContext ctx, TestWorld world, ProjectilePool pool)
    {
        var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, world.Chapter));
        try
        {
            var model = new PlaneBuilder(GameZ.Load(ctx.PlanesGamezPath), textures).Build(PlaneNode);
            var rig = new FlightController
            {
                PlaneModel = model,
                Collider = PlaneCollider.Build(model),
                PlayerIndex = FlightRoster.ShooterIdBase,
                IsHumanPiloted = true,
                Projectiles = pool,
                UseKeyboard = false,
                PadDevices = Array.Empty<int>(),
                AllowPause = false,
                Team = AimAssist.PlayerTeam,
                Name = "DropoffPlacementPlayer",
            };
            rig.AddChild(model);
            ctx.Host.AddChild(rig);
            rig.Setup(new FlightModel(PlaneStats.Load(ctx.ZrdrPath, PlaneNode)), null, new CamParams(),
                Vector3.Zero, Vector3.Forward, 0f, 0f);
            return rig;
        }
        finally
        {
            textures.Dispose();
        }
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
