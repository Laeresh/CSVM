using System.Collections.Generic;
using System.Text;
using CSVM.Mech3;
using Godot;

namespace CSVM.Testing;

/// <summary>The suite over the parachutist a mid-mission drop-off cutscene animates: reachable
/// through the drop's own cross-archive pointers the same way the intro's aircraft are (BL-540,
/// B8's precedent), and switched visible by the drop's own definition rather than staged already
/// on. Decode: docs/formats/anim-definitions/cutscenes.md.</summary>
internal static class DropoffChutemanSuites
{
    // The story position driven: C3/M01, the campaign's first mission (the same position
    // IntroAircraftSuites drives; its own tex_drop.zrd is the drop-off's reader).
    private const int MissionSeq = 0;

    // The def CALL_ANIMATION[tdchute] (authored inside player-texdrop) starts. Its own NAME is
    // "chuteman"; ANIMATION_NAME is what CALL_ANIMATION and AnimRuntime.Play address.
    private const string DropAnimName = "tdchute";

    private const float StepDt = 1f / 60f;

    // The reparent-and-activate sequence runs its first events on the frame Play starts it; a
    // couple of steps clears any one-frame ordering the runtime's newest-first walk introduces.
    private const int DriveSteps = 3;

    // The four cross-archive nodes the drop's compiled symbol table names, in staged-subtree order.
    private static readonly string[] ChuteObjects =
    {
        AircraftStage.ChuteNode, "chutemanparent", "pilot", "stamp",
    };

    /// <summary>Drives C3/M01's drop-off definition directly (never the approach cone itself,
    /// so this cannot arm anything INSTR-20 warns about) against its BUILT world: the aircraft
    /// archive's <c>chuteman</c> subtree joins the runtime's node table at the chapter's own
    /// cross-archive base, and calling the drop's own definition reparents it under the aiming
    /// node and switches it visible.</summary>
    // BL-540: the aircraft archive's 'chuteman' subtree never joined the world build, so a
    // mid-mission drop-off's own definition claimed a symbol with a null binding, the same
    // staging gap B8 fixed for the intro's two aircraft.
    [Suite("dropoff-chuteman-stage",
        "the parachutist a mid-mission drop-off animates, over C3/M01's BUILT world: the "
        + "aircraft archive's 'chuteman'/'chutemanparent'/'pilot'/'stamp' answer the compiled "
        + "drop's own cross-archive pointers in the runtime's node table, the subtree ships "
        + "switched off as the shared def's own base state, and calling the drop's definition "
        + "directly (never the approach cone) switches it visible")]
    internal static void DropoffChutemanStage(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var mission = MissionOf(CampaignSequence.Load(ctx.ZrdrPath), MissionSeq)
            ?? throw new SuiteSkippedException($"cm_sequence carries no story position {MissionSeq}");
        string chapter = mission.ChapterFolder.ToUpperInvariant();
        string folder = mission.MissionFolder.ToUpperInvariant();
        ctx.RequireData(SessionPaths.MissionZrdr(ctx.DataRoot, chapter, folder), $"{chapter}/{folder} zrdr");
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

        ctx.WriteArtifact($"test-dropoff-chuteman-{chapter}-{folder}.txt", report.ToString());
        ctx.Note($"drove {chapter}/{folder}'s '{DropAnimName}' over a built world with its parachutist staged");
    }

    private static void Drive(TestContext ctx, TestWorld world, StringBuilder report)
    {
        if (world.Session.Aircraft is not { Chuteman: { } chuteman } stage)
        {
            ctx.Check(false, $"the world build staged the drop-off's '{AircraftStage.ChuteNode}' subtree");
            return;
        }

        int expected = AircraftStage.PointerBaseOf(world.Gamez.Nodes.Count);
        report.AppendLine($"{world.Gamez.Nodes.Count} chapter nodes -> cross-archive base {expected}");

        var defs = world.Session.Program.ByAnimName(DropAnimName);
        if (defs.Count == 0)
        {
            throw new SuiteSkippedException($"{world.Chapter} carries no compiled '{DropAnimName}'");
        }

        foreach (string name in ChuteObjects)
        {
            if (!defs[0].NodeRefs.TryGetValue(name, out int ptr))
            {
                ctx.Check(false, $"'{DropAnimName}'s symbol table names '{name}'");
                continue;
            }

            var bound = world.Runtime.FindNodeByIndex(ptr);
            report.AppendLine($"'{name}' ptr {ptr} -> archive index {ptr - expected}, "
                + $"bound={(bound != null ? "yes" : "no")}");
            ctx.Check(ptr > world.Gamez.Nodes.Count,
                $"'{name}' ptr {ptr} sits past the chapter's own {world.Gamez.Nodes.Count} nodes");
            ctx.Check(bound != null, $"and the runtime's node table answers it with a staged node");
        }

        ctx.Check(!chuteman.IsVisibleInTree(),
            $"'{AircraftStage.ChuteNode}' ships switched off, the shared def's own base state");

        world.Runtime.Play(DropAnimName);
        for (int i = 0; i < DriveSteps; i++)
        {
            world.Runtime.Advance(StepDt);
        }

        report.AppendLine($"'{AircraftStage.ChuteNode}' visible={chuteman.IsVisibleInTree()} "
            + $"under_aim={IsUnder(chuteman, "do_direction")}");
        ctx.Check(chuteman.IsVisibleInTree(),
            $"and calling '{DropAnimName}' switches '{AircraftStage.ChuteNode}' on");
    }

    private static bool IsUnder(Node3D node, string ancestorName)
    {
        for (var p = node.GetParent() as Node3D; p != null; p = p.GetParent() as Node3D)
        {
            if (string.Equals(AnimRuntime.NameOf(p), ancestorName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
