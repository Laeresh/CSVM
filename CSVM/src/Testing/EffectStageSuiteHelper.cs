using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using CSVM.Mech3.Anim;
using CSVM.Session;
using Godot;

namespace CSVM.Testing;

internal static class EffectStageSuiteHelper
{
    internal static void WithEffectStage(TestContext ctx, TestWorld world, string animName,
        IEnumerable<string> rootNames, Action<Node3D, AnimRuntime, Vector3> body)
    {
        var stage = new Node3D { Name = $"EffectStage_{animName}" };
        var pool = new Node3D { Name = "pool0" };
        pool.SetMeta(AnimRuntime.PoolSlotMeta, 0);
        stage.AddChild(pool);
        int built = WorldEffectsFactory.BuildEffectStage(world.Gamez, world.Session.Builder.Scene, pool, rootNames);
        ctx.Check(built == rootNames.Count(), $"{animName}: staged {built}/{rootNames.Count()} template root(s)");
        foreach (var child in pool.GetChildren())
            if (child is Node3D root)
                root.Visible = false;

        var runtime = AnimRuntime.ForEffects(
            AnimRuntime.NewTemplateStage(pooled: true, shown: true, placesCalled: true),
            1, new CountingEmitterFactory(), false, 32f, () => ctx.Camera.GlobalPosition);
        runtime.ManualAdvance = true;
        ctx.Host.AddChild(stage);
        ctx.Host.AddChild(runtime);
        try
        {
            runtime.Bind(stage, world.Session.Program.Subset(animName));
            body(stage, runtime, ctx.Camera.GlobalPosition);
        }
        finally
        {
            runtime.Free();
            stage.Free();
        }
    }
}
