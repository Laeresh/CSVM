using System;
using System.Collections.Generic;
using System.Text;
using CSVM.Extraction;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Roster;
using CSVM.Session.World;
using Godot;

namespace CSVM.Testing;

/// <summary>The suite over a carried passenger: the card a mission's <c>add_&lt;name&gt;</c> seats on
/// the flown aircraft's <c>cockpit1/pass_st</c>. Decode: docs/org/cameraViews.md, "The passenger
/// seat".</summary>
internal static class PassengerSeatSuites
{
    // CM01, whose startanims seat Jack with no pickup.
    private const int JackSeq = 0;
    private const string Group = "apassengers";
    private const string Seat = "pass_st";
    private const string Card = "p_jack";
    private const string PlaneNode = "player_bhawk";
    private const float StepDt = 1f / 60f;

    // call_add_jack calls add_jack 0.2 s in; one second covers it with room to spare.
    private const float DriveS = 1f;

    /// <summary>Drives CM01's start list over its built world with a rig whose interior sits outside
    /// its model, as the cockpit pass leaves it.</summary>
    // #267: no seat resolved and no group was built, so the rear seat stayed empty.
    [Suite("passenger-seat",
        "CM01's carried passenger over its BUILT world: the world holds 'apassengers' undrawn, the "
        + "flown aircraft's interior joins the node table although the cockpit pass lifted it out "
        + "of the model, and the start list's add_jack seats the group on that interior's "
        + "'pass_st' with 'p_jack' the one card drawn")]
    internal static void PassengerSeat(TestContext ctx)
    {
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        ctx.RequireZrdrEntry(ctx.Zrdr, CampaignSequence.FileName);
        var mission = MissionOf(CampaignSequence.Load(ctx.Zrdr), JackSeq)
            ?? throw new SuiteSkippedException($"cm_sequence carries no story position {JackSeq}");
        string chapter = mission.ChapterFolder.ToUpperInvariant();
        string folder = mission.MissionFolder.ToUpperInvariant();
        ctx.RequireData(ctx.MissionZrdr(chapter, folder), $"{chapter}/{folder} zrdr");
        ctx.RequireData(ctx.ChapterTextures(chapter), $"{chapter} textures");
        ctx.RequireData(ctx.Data.Planes, $"aircraft archive");

        var report = new StringBuilder();
        report.AppendLine($"seq {JackSeq} -> {chapter}/{folder}");
        ctx.CutsceneRoots = true;
        try
        {
            ctx.WithWorld(chapter, collision: false, folder, world => Drive(ctx, world, report));
        }
        finally
        {
            ctx.CutsceneRoots = false;
        }

        ctx.WriteArtifact($"test-passenger-seat-{chapter}-{folder}.txt", report.ToString());
        ctx.Note($"seated {chapter}/{folder}'s passenger over a built world");
    }

    private static void Drive(TestContext ctx, TestWorld world, StringBuilder report)
    {
        if (world.Session.Aircraft is not { } stage)
        {
            ctx.Check(false, $"the world build staged the mission's aircraft");
            return;
        }

        var group = Find(world.Stage, Group);
        ctx.Check(group != null && !group.IsVisibleInTree(),
            $"the world holds '{Group}' and draws none of it before a seat is named");
        var cutscene = new CutsceneController();
        ctx.Host.AddChild(cutscene);
        var textures = new TextureArchive(ctx.ChapterTextures(world.Chapter));
        var pool = new ProjectilePool(textures, null, null);
        ctx.Host.AddChild(pool);
        var pass = new Node3D { Name = "cockpit_pass_stand_in" };
        ctx.Host.AddChild(pass);
        try
        {
            var rig = BuildRig(ctx, world, pool, pass, out var interior);
            cutscene.BindWorld(world.Runtime, stage);
            cutscene.BindRigs(
                new[] { new PlayerRig { Index = 0, Camera = ctx.Camera, HudParent = ctx.Host, Controller = rig } },
                () => Array.Empty<FlightController>());
            world.Runtime.CallbackHost = cutscene.Host;
            for (float t = 0f; t < DriveS; t += StepDt)
            {
                world.Runtime.Advance(StepDt);
                cutscene.Tick();
            }

            CheckSeated(ctx, interior, group, report);
        }
        finally
        {
            cutscene.Free();
            pool.Free();
            pass.Free();
            textures.Dispose();
        }
    }

    private static void CheckSeated(TestContext ctx, Node3D interior, Node3D? group, StringBuilder report)
    {
        var seat = Find(interior, Seat);
        var parent = group?.GetParent() as Node3D;
        report.AppendLine($"seat {(seat != null ? "found" : "missing")} under the interior, group parent "
            + $"'{(parent != null ? AnimRuntime.NameOf(parent) : "none")}'");
        ctx.Check(seat != null, $"the flown airframe's interior carries '{Seat}'");
        ctx.Check(seat != null && ReferenceEquals(parent, seat),
            $"and add_jack seated '{Group}' on it, the interior the cockpit pass draws");
        if (group == null)
        {
            return;
        }

        var drawn = new List<string>();
        Collect(group, drawn);
        report.AppendLine($"cards drawn: {string.Join(", ", drawn)}");
        ctx.Same(1, drawn.Count, $"one card is drawn: {string.Join(", ", drawn)}");
        ctx.Check(drawn.Contains(Card), $"and it is '{Card}'");
    }

    // Every p_* card drawn with its own Visible and every ancestor's up to the group.
    private static void Collect(Node3D node, List<string> drawn)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is not Node3D n3d || !n3d.Visible)
            {
                continue;
            }

            string name = AnimRuntime.NameOf(n3d);
            if (name.StartsWith("p_", StringComparison.OrdinalIgnoreCase))
            {
                drawn.Add(name);
            }

            Collect(n3d, drawn);
        }
    }

    private static Node3D? Find(Node node, string name)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D n3d && string.Equals(AnimRuntime.NameOf(n3d), name, StringComparison.OrdinalIgnoreCase))
            {
                return n3d;
            }

            if (Find(child, name) is { } hit)
            {
                return hit;
            }
        }

        return null;
    }

    // A human rig with its interior moved off the model the way CockpitOverlay.Build moves it.
    private static FlightController BuildRig(TestContext ctx, TestWorld world, ProjectilePool pool,
        Node3D pass, out Node3D interior)
    {
        var textures = new TextureArchive(ctx.ChapterTextures(world.Chapter));
        try
        {
            var builder = new PlaneBuilder(ctx.Planes, textures, cockpitInterior: true);
            var model = builder.Build(PlaneNode);
            interior = builder.CockpitInterior
                ?? throw new SuiteSkippedException($"'{PlaneNode}' built no cockpit interior");
            interior.GetParent().RemoveChild(interior);
            pass.AddChild(interior);
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
                Name = "PassengerSeatPlayer",
            };
            rig.Dressing.Interior = interior;
            rig.AddChild(model);
            ctx.Host.AddChild(rig);
            rig.Setup(new FlightModel(PlaneStats.Load(ctx.Zrdr, PlaneNode)), null, new CamParams(),
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
