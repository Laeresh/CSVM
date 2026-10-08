using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Effects;
using CSVM.Extraction;
using CSVM.Mech3;
using CSVM.Mech3.Anim;
using CSVM.Session.World;
using CSVM.Tooling;
using Godot;

namespace CSVM.Testing;

/// <summary>What an effect or destructible suite stages from: an anim program, the gamez holding
/// its template roots, and their scene builder.</summary>
internal sealed record AnimSource(AnimProgram Program, GameZ Gamez, SceneBuilder Scene)
{
    internal static AnimSource Of(TestWorld world) =>
        new(world.Session.Program, world.Gamez, world.Session.Builder.Scene);
}

/// <summary>A world a destructible suite runs on: its root, the runtime bound over it, and the
/// program and scene builder it was built from.</summary>
internal sealed record AnimWorld(Node3D Root, AnimRuntime Runtime, AnimProgram Program, SceneBuilder Scene);

internal static class EffectStageSuiteHelper
{
    /// <summary>The run's chapter world on an extraction. Under <c>--synthetic-data</c>, the
    /// invented <c>probe_effects</c> gamez and archive plus the zrdr reader definitions. ⚠ Keep the
    /// extraction branch the chapter world: the real install's suites read its shipped program.</summary>
    internal static void WithAnimSource(TestContext ctx, Action<AnimSource> body) =>
        WithAnimSource(ctx, ctx.Chapter, body);

    /// <inheritdoc cref="WithAnimSource(TestContext, Action{AnimSource})"/>
    internal static void WithAnimSource(TestContext ctx, string chapter, Action<AnimSource> body)
    {
        if (!ctx.SyntheticData)
        {
            ctx.WithWorld(chapter, collision: false, world => body(AnimSource.Of(world)));
            return;
        }

        string gamezPath = SyntheticEffects.GamezUnder(ctx.DataRoot);
        ctx.RequireData(gamezPath, $"synthetic effect templates");
        var gamez = GameZ.Load(gamezPath);
        var defs = new List<AnimDefinition>(
            AnimArchive.Load(SyntheticEffects.AnimUnder(ctx.DataRoot), SyntheticEffects.Folder)?.Defs
            ?? new List<AnimDefinition>());
        defs.AddRange(AnimDefs.LoadArchive(ctx.ZrdrPath));
        using var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, SyntheticTextures.Chapter));
        body(new AnimSource(AnimProgram.FromDefinitions(defs),
            gamez, new SceneBuilder(gamez, textures, fullbright: true, cullBackfaces: true)));
    }

    /// <summary>The named chapter's world on an extraction. Under <c>--synthetic-data</c>, a private
    /// world of the invented program's destructible roots, bound to that program.</summary>
    internal static void WithAnimWorld(TestContext ctx, string chapter, Action<AnimWorld> body)
    {
        if (!ctx.SyntheticData)
        {
            ctx.WithWorld(chapter, collision: false, world => body(new AnimWorld(world.Session.Root,
                world.Runtime, world.Session.Program, world.Session.Builder.Scene)));
            return;
        }

        WithAnimSource(ctx, source =>
        {
            var root = new Node3D { Name = "SyntheticWorld" };
            foreach (var def in source.Program.Defs.Where(d => d.Destructible))
            {
                if (source.Gamez.FindByName(def.Name) is { } node && source.Scene.BuildSubtree(node) is { } built)
                {
                    root.AddChild(built);
                }
            }

            var runtime = new AnimRuntime { ManualAdvance = true };
            ctx.Host.AddChild(root);
            ctx.Host.AddChild(runtime);
            try
            {
                runtime.Bind(root, source.Program);
                body(new AnimWorld(root, runtime, source.Program, source.Scene));
            }
            finally
            {
                runtime.Free();
                root.Free();
            }
        });
    }

    /// <summary><paramref name="slots"/> stages the roots in that many pool copies, the shape the
    /// real world-effects stage is built in (<c>WorldEffectsFactory</c>, sized by
    /// <c>effect_pools.json</c>): a suite that asks whether overlapping calls keep their own sites
    /// needs the pool the session ships, not one copy.</summary>
    internal static void WithEffectStage(TestContext ctx, TestWorld world, string animName,
        IEnumerable<string> rootNames, Action<Node3D, AnimRuntime, Vector3> body,
        IEmitterFactory? factory = null, int slots = 1) =>
        WithEffectStage(ctx, AnimSource.Of(world), animName, rootNames, body, factory, slots);

    /// <inheritdoc cref="WithEffectStage(TestContext, TestWorld, string, IEnumerable{string}, Action{Node3D, AnimRuntime, Vector3}, IEmitterFactory?, int)"/>
    internal static void WithEffectStage(TestContext ctx, AnimSource source, string animName,
        IEnumerable<string> rootNames, Action<Node3D, AnimRuntime, Vector3> body,
        IEmitterFactory? factory = null, int slots = 1)
    {
        var stage = new Node3D { Name = $"EffectStage_{animName}" };
        int built = 0;
        try
        {
            for (int slot = 0; slot < slots; slot++)
            {
                var pool = new Node3D { Name = $"pool{slot}" };
                pool.SetMeta(AnimRuntime.PoolSlotMeta, slot);
                stage.AddChild(pool);
                built += WorldEffectsFactory.BuildEffectStage(source.Gamez, source.Scene, pool, rootNames);
                foreach (var child in pool.GetChildren())
                    if (child is Node3D root)
                        root.Visible = false;
            }
        }
        catch
        {
            // ⚠ Do not orphan the staged roots of earlier slots: their meshes would outlive the renderer.
            stage.Free();
            throw;
        }
        ctx.Check(built == rootNames.Count() * slots,
            $"{animName}: staged {built}/{rootNames.Count() * slots} template root(s) over {slots} slot(s)");

        var runtime = AnimRuntime.ForEffects(
            AnimRuntime.NewTemplateStage(pooled: true, shown: true, placesCalled: true),
            1, factory ?? new CountingEmitterFactory(), false, 32f, () => ctx.Camera.GlobalPosition);
        runtime.ManualAdvance = true;
        ctx.Host.AddChild(stage);
        ctx.Host.AddChild(runtime);
        try
        {
            runtime.Bind(stage, source.Program.Subset(animName));
            body(stage, runtime, ctx.Camera.GlobalPosition);
        }
        finally
        {
            runtime.Free();
            stage.Free();
        }
    }
}
