using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSVM.Extraction;
using CSVM.Mech3;
using CSVM.Session.World;
using Godot;

namespace CSVM.Testing;

/// <summary>CM20 (C4/M05) over its own world. The Black Hat zeppelin's record is
/// <c>deactivated</c>, so the hull stays out of the world until the mission wakes it. Dormancy is a
/// fade to opacity 0, which hides only what has an alpha path to read it. The hangar's
/// <c>cargobay</c> strip wears <c>pir_spinner</c>, which the install lacks. Its stand-in material
/// must fade like any other.</summary>
internal static class ZeppelinDormantDrawSuites
{
    private const string Chapter = "C4";
    private const string Mission = "M05";
    private const string Hull = "blackhatzep";

    [Suite("zeppelin-dormant-draws-nothing",
        "CM20's Black Hat zeppelin over C4/M05's real world: its record is deactivated, so every " +
        "mesh under the hull must read the fade that puts it out of the world, and nothing under " +
        "it may still draw at opacity 0")]
    internal static void ZeppelinDormantDrawsNothing(TestContext ctx)
    {
        string chapterZrdr = SessionPaths.ChapterZrdr(ctx.DataRoot, Chapter);
        string missionZrdr = SessionPaths.MissionZrdr(ctx.DataRoot, Chapter, Mission);
        ctx.RequireData(chapterZrdr, $"{Chapter} zrdr");
        ctx.RequireData(missionZrdr, $"{Chapter}/{Mission} zrdr");
        ctx.RequireData(SessionPaths.ChapterTextures(ctx.DataRoot, Chapter), $"{Chapter} textures");

        ZeppelinDef? def = null;
        foreach (var d in Zeppelins.Load(missionZrdr))
        {
            if (d.Node.Equals(Hull, StringComparison.OrdinalIgnoreCase))
            {
                def = d;
            }
        }

        ctx.Check(def is { Deactivated: true },
            $"{Chapter}/{Mission} authors a {Hull} record that is deactivated, so it starts out of the world");
        if (def == null)
        {
            return;
        }

        var nets = AiNets.Load(chapterZrdr);
        ctx.WithWorld(Chapter, collision: false, Mission, world => Drive(ctx, world, def, nets));
    }

    private static void Drive(TestContext ctx, TestWorld world, ZeppelinDef def,
        IReadOnlyList<AiNet> nets)
    {
        var host = world.Runtime.FindNodes(Hull, null) is { Count: > 0 } hits ? hits[0] : null;
        ctx.Check(host != null, $"the built world carries the {Hull} node");
        if (host == null)
        {
            return;
        }

        var report = new StringBuilder();
        ZeppelinRuntime? zeps = null;
        try
        {
            zeps = new ZeppelinRuntime(new[] { def },
                name => world.Runtime.FindNodes(name, null) is { Count: > 0 } h ? h[0] : null,
                nets, null, world.Runtime.Motions.DrivesTransform);
            ctx.Host.AddChild(zeps);
            zeps.WireDamage(world.Runtime);
            ctx.Check(zeps.IsDormant(Hull), $"'{Hull}' is dormant once the runtime has it");

            int drawn = 0;
            var leaks = new List<string>();
            Walk(host, host, ref drawn, leaks);
            foreach (var leak in leaks)
            {
                report.AppendLine(leak);
            }

            report.AppendLine($"{Hull}: {drawn} drawn instance(s), {leaks.Count} with no alpha path");
            ctx.Check(drawn > 0, $"the dormant hull still carries drawn instances to fade ({drawn})");
            ctx.Same(0, leaks.Count,
                $"no instance under the dormant '{Hull}' draws at opacity 0: {string.Join("; ", leaks)}");
        }
        finally
        {
            zeps?.Free();
            ctx.WriteArtifact("test-zeppelin-dormant-draws-nothing.txt", report.ToString());
        }
    }

    // Every visible instance under the hull, and the ones that ignore csky_opacity.
    private static void Walk(Node node, Node3D host, ref int drawn, List<string> leaks)
    {
        if (node is Node3D n3 && !n3.Visible)
        {
            return;
        }

        if (node is GeometryInstance3D vi)
        {
            drawn++;
            if (!ReadsOpacity(vi))
            {
                string materials = vi is MeshInstance3D { Mesh: { } m }
                    ? string.Join(",", Enumerable.Range(0, m.GetSurfaceCount()).Select(s => m.SurfaceGetMaterial(s)?.GetClass()))
                    : "-";
                leaks.Add($"{host.GetPathTo(vi)} ({vi.GetClass()}, {materials}) at {vi.GlobalPosition}");
            }
        }

        for (int i = 0; i < node.GetChildCount(); i++)
        {
            Walk(node.GetChild(i), host, ref drawn, leaks);
        }
    }

    // A mesh draws nothing at opacity 0 only if every surface's live material reads the term.
    private static bool ReadsOpacity(GeometryInstance3D vi)
    {
        if (vi is not MeshInstance3D mi || mi.Mesh is not { } mesh)
        {
            return false;
        }

        for (int i = 0; i < mesh.GetSurfaceCount(); i++)
        {
            var material = mi.GetSurfaceOverrideMaterial(i) ?? mi.MaterialOverride ?? mesh.SurfaceGetMaterial(i);
            if (material is not ShaderMaterial { Shader: { } shader }
                || !shader.Code.Contains(SceneBuilder.OpacityTerm, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
