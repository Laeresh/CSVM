using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The Enhanced sun's shadow quality between the VIDEO page and the renderer. Covered are
/// the order the sources resolve in and what each level writes on the sun and hands the renderer.
/// So are the <c>--no-soft-shadows</c> door, the faithful path writing nothing, and the
/// <c>--det</c> drop that keeps a saved word out of a golden.
/// ⚠ Every write lands on a sun built for the suite and kept out of the tree. The renderer-wide
/// pair goes to a recorder, so the run's own renderer state is never touched.</summary>
internal static class ShadowQualitySuites
{
    // What each level writes, lowest first. The fields are the cast switch, the sun's angular
    // distance and blur, the renderer's soft filter and the atlas edge.
    private static readonly (string Word, bool Cast, float Angle, float Blur, RenderingServer.ShadowQuality Filter, int Atlas)[] Levels =
    {
        (ShadowQualitySetting.Off, false, 0f, 0f, RenderingServer.ShadowQuality.Hard, 4096),
        (ShadowQualitySetting.Low, true, 0f, 1f, RenderingServer.ShadowQuality.SoftLow, 4096),
        (ShadowQualitySetting.Medium, true, 0.25f, 1f, RenderingServer.ShadowQuality.SoftMedium, 4096),
        (ShadowQualitySetting.High, true, 0.5f, 1f, RenderingServer.ShadowQuality.SoftHigh, 4096),
        (ShadowQualitySetting.Ultra, true, 1f, 1f, RenderingServer.ShadowQuality.SoftUltra, 8192),
    };

    [Suite("display-shadow-quality",
        "The Enhanced sun's shadow quality: --shadow-quality beats the saved word, which beats the "
        + "graphics.shadowQuality config key, which beats the default Ultra (High on an integrated "
        + "GPU, Off there at three or four panes under its own source, Ultra under --det everywhere), a key spelling the fallback reads as the default "
        + "and an unknown word at any layer falls through; each of Off, Low, "
        + "Medium, High and Ultra writes its pinned cast switch, angular distance and blur on the sun "
        + "and hands the renderer its pinned soft filter and atlas (4096 below Ultra, 8192 at Ultra), "
        + "Off handing the renderer nothing; no level's sun is wider than its filter rung holds "
        + "(Ultra 1.0, High 0.5, Medium 0.25 degrees, below that none); the --no-soft-shadows door "
        + "zeroes a casting level's penumbra and hands the renderer the hard filter; under the faithful "
        + "path no level writes anything on the sun or the renderer; a level applied again to a sun "
        + "whose cockpit pass is standing reaches that pass's light on its next follow, Off turning "
        + "its shadow off and a casting level back on; the command line parses the flag and refuses "
        + "an unknown word; and a --det launch reads no saved word while a plain one does")]
    internal static void DisplayShadowQuality(TestContext ctx)
    {
        string launched = ShadowQualitySetting.Word;
        try
        {
            Precedence(ctx);
            foreach (var level in Levels)
            {
                OneLevel(ctx, level);
            }

            OriginalIgnoresEveryLevel(ctx);
            CockpitFollowsALiveApply(ctx);
            CommandLine(ctx);
        }
        finally
        {
            // The run's own resolution, put back so a later suite reads the word this launch took.
            ShadowQualitySetting.Resolve(launched, null, null);
        }

        SavedWordDrop(ctx);
    }

    // The widest sun each filter rung may carry. Wider, its per-pixel sample disc weaves a pattern
    // over lit surfaces. Below SoftMedium only the zero-width sun is safe.
    private static float WidestSunFor(RenderingServer.ShadowQuality filter) => filter switch
    {
        RenderingServer.ShadowQuality.SoftUltra => 1f,
        RenderingServer.ShadowQuality.SoftHigh => 0.5f,
        RenderingServer.ShadowQuality.SoftMedium => 0.25f,
        _ => 0f,
    };

    private static void Precedence(TestContext ctx)
    {
        var flag = ShadowQualitySetting.Resolve(ShadowQualitySetting.Low, ShadowQualitySetting.High, ShadowQualitySetting.Medium);
        ctx.Check(flag.Word == ShadowQualitySetting.Low && flag.Source == SettingSource.Flag,
            $"the flag beats a saved word and the config key ({Describe(flag)})");
        var saved = ShadowQualitySetting.Resolve(null, ShadowQualitySetting.High, ShadowQualitySetting.Medium);
        ctx.Check(saved.Word == ShadowQualitySetting.High && saved.Source == SettingSource.Saved,
            $"the saved word beats the config key ({Describe(saved)})");
        var key = ShadowQualitySetting.Resolve(null, null, ShadowQualitySetting.Medium);
        ctx.Check(key.Word == ShadowQualitySetting.Medium && key.Source == SettingSource.Config,
            $"with nothing saved the {ShadowQualitySetting.Key} key decides ({Describe(key)})");
        var fallback = ShadowQualitySetting.Resolve(null, null, ShadowQualitySetting.Default);
        ctx.Check(fallback.Word == ShadowQualitySetting.Ultra && fallback.Source == SettingSource.Default,
            $"and a key spelling ultra reads as the default, the look Enhanced shipped with ({Describe(fallback)})");
        var unknown = ShadowQualitySetting.Resolve("epic", "cinematic", "potato");
        ctx.Check(unknown.Word == ShadowQualitySetting.Default && unknown.Source == SettingSource.Default,
            $"an unknown word at every layer falls through to the default ({Describe(unknown)})");
        var integrated = ShadowQualitySetting.Resolve(null, null, ShadowQualitySetting.IntegratedDefault, ShadowQualitySetting.IntegratedDefault);
        ctx.Check(integrated.Word == ShadowQualitySetting.High && integrated.Source == SettingSource.IntegratedGpuDefault,
            $"an integrated GPU with nothing set runs High ({Describe(integrated)})");
        var panes = ShadowQualitySetting.Resolve(null, null, ShadowQualitySetting.IntegratedSplitDefault, ShadowQualitySetting.IntegratedSplitDefault);
        ctx.Check(panes.Word == ShadowQualitySetting.Off && panes.Source == SettingSource.IntegratedGpuPanesDefault,
            $"an integrated GPU's three- or four-pane session with nothing set runs Off under its own source ({Describe(panes)})");
        var keyUltra = ShadowQualitySetting.Resolve(null, null, ShadowQualitySetting.Ultra, ShadowQualitySetting.IntegratedDefault);
        ctx.Check(keyUltra.Word == ShadowQualitySetting.Ultra && keyUltra.Source == SettingSource.Config,
            $"and a key spelling ultra still wins there, since ultra is not that machine's fallback ({Describe(keyUltra)})");
        ctx.Check(ShadowQualitySetting.DefaultFor(det: true) == ShadowQualitySetting.Default,
            $"a --det run falls back to Ultra whatever the GPU, so a capture does not depend on the machine");
    }

    private static void OneLevel(TestContext ctx, (string Word, bool Cast, float Angle, float Blur, RenderingServer.ShadowQuality Filter, int Atlas) level)
    {
        var plan = ShadowQualitySetting.PlanFor(level.Word);
        ctx.Check(plan.Cast == level.Cast && plan.AngularDistance == level.Angle && plan.Blur == level.Blur
            && plan.Filter == level.Filter && plan.AtlasSize == level.Atlas,
            $"{level.Word} is pinned: {Describe(plan)}");
        ctx.Check(plan.AngularDistance <= WidestSunFor(plan.Filter),
            $"{level.Word} runs no sun wider than its filter rung holds, {WidestSunFor(plan.Filter)} degrees ({Describe(plan)})");

        var calls = new List<(RenderingServer.ShadowQuality Filter, int Atlas)>();
        var sun = new DirectionalLight3D();
        try
        {
            ShadowQualitySetting.ApplyTo(sun, plan, enhanced: true, hard: false, (f, a) => calls.Add((f, a)));
            ctx.Check(sun.ShadowEnabled == level.Cast,
                $"{level.Word} leaves the sun casting={sun.ShadowEnabled}");
            if (!level.Cast)
            {
                ctx.Check(calls.Count == 0, $"and hands the renderer nothing ({calls.Count} calls)");
                return;
            }

            ctx.Check(sun.LightAngularDistance == level.Angle && sun.ShadowBlur == level.Blur,
                $"{level.Word} writes the sun's angular distance {sun.LightAngularDistance} and blur {sun.ShadowBlur}");
            ctx.Check(calls.Count == 1 && calls[0] == (level.Filter, level.Atlas),
                $"and hands the renderer {level.Filter} over the {level.Atlas} atlas ({Join(calls)})");

            calls.Clear();
            ShadowQualitySetting.ApplyTo(sun, plan, enhanced: true, hard: true, (f, a) => calls.Add((f, a)));
            ctx.Check(sun.ShadowEnabled && sun.LightAngularDistance == 0f && sun.ShadowBlur == 0f
                && calls.Count == 1 && calls[0] == (RenderingServer.ShadowQuality.Hard, level.Atlas),
                $"--no-soft-shadows keeps {level.Word}'s shadow with a hard edge ({sun.LightAngularDistance}, {sun.ShadowBlur}, {Join(calls)})");
        }
        finally
        {
            sun.Free();
        }
    }

    // A fresh sun is the faithful path's sun: no shadow, Godot's own penumbra defaults. No level
    // may move any of it, and none may reach the renderer.
    private static void OriginalIgnoresEveryLevel(TestContext ctx)
    {
        foreach (string word in ShadowQualitySetting.Words)
        {
            var calls = new List<(RenderingServer.ShadowQuality Filter, int Atlas)>();
            var sun = new DirectionalLight3D();
            var control = new DirectionalLight3D();
            try
            {
                ShadowQualitySetting.ApplyTo(sun, ShadowQualitySetting.PlanFor(word), enhanced: false, hard: false, (f, a) => calls.Add((f, a)));
                ctx.Check(!sun.ShadowEnabled && sun.LightAngularDistance == control.LightAngularDistance
                    && sun.ShadowBlur == control.ShadowBlur && calls.Count == 0,
                    $"under the faithful path {word} writes nothing (casting={sun.ShadowEnabled}, angle={sun.LightAngularDistance}, blur={sun.ShadowBlur}, {calls.Count} renderer calls)");
            }
            finally
            {
                sun.Free();
                control.Free();
            }
        }
    }

    // The live apply as an Options accept makes it. The level goes onto a sun whose cockpit pass
    // already stands, and the pass's light takes it on its next follow. The renderer pair is
    // discarded, so the run's own renderer is untouched.
    private static void CockpitFollowsALiveApply(TestContext ctx)
    {
        var host = new Node { Name = "shadow-quality-host" };
        var sun = new DirectionalLight3D { Name = "shadow-quality-sun" };
        var model = new Node3D { Name = "shadow-quality-model" };
        var panel = new Node3D { Name = "shadow-quality-panel" };
        ctx.Host.AddChild(host);
        host.AddChild(model);
        model.AddChild(panel);
        System.Action<RenderingServer.ShadowQuality, int> ignore = (_, _) => { };

        try
        {
            ShadowQualitySetting.ApplyTo(sun, ShadowQualitySetting.PlanFor(ShadowQualitySetting.Ultra), true, false, ignore);
            var pass = Flight.Hud.CockpitOverlay.Build(host, panel, sun, null);
            if (pass?.Sun is not { } light)
            {
                ctx.Check(false, $"the cockpit pass builds over the suite's sun");
                return;
            }

            ctx.Check(light.ShadowEnabled && light.LightAngularDistance == 1f,
                $"the pass's light is built on the sun's Ultra shadow ({light.ShadowEnabled}, {light.LightAngularDistance})");
            int before = ShadowQualitySetting.Revision;
            ShadowQualitySetting.ApplyTo(sun, ShadowQualitySetting.PlanFor(ShadowQualitySetting.Low), true, false, ignore);
            ctx.Check(ShadowQualitySetting.Revision == before + 1, $"a live apply moves the revision ({before} -> {ShadowQualitySetting.Revision})");
            pass.FollowSunShadow();
            ctx.Check(light.ShadowEnabled && light.LightAngularDistance == 0f,
                $"and the pass's light follows Low on its next follow ({light.ShadowEnabled}, {light.LightAngularDistance})");
            ShadowQualitySetting.ApplyTo(sun, ShadowQualitySetting.PlanFor(ShadowQualitySetting.Off), true, false, ignore);
            pass.FollowSunShadow();
            ctx.Check(!light.ShadowEnabled, $"Off turns the pass's shadow off with the sun's ({light.ShadowEnabled})");
            ShadowQualitySetting.ApplyTo(sun, ShadowQualitySetting.PlanFor(ShadowQualitySetting.High), true, false, ignore);
            pass.FollowSunShadow();
            ctx.Check(light.ShadowEnabled && light.LightAngularDistance == 0.5f,
                $"and High turns it back on at half a degree ({light.ShadowEnabled}, {light.LightAngularDistance})");
        }
        finally
        {
            host.QueueFree();
            sun.Free();
        }
    }

    private static void CommandLine(TestContext ctx)
    {
        var picked = SessionSpec.Parse(new[] { "--shadow-quality=medium" });
        ctx.Check(picked.ShadowQuality == ShadowQualitySetting.Medium,
            $"--shadow-quality=medium reaches the spec ({picked.ShadowQuality ?? "unset"})");
        var refused = SessionSpec.Parse(new[] { "--shadow-quality=epic" });
        ctx.Check(refused.ShadowQuality == null,
            $"and an unknown word is refused rather than kept ({refused.ShadowQuality ?? "unset"})");
    }

    private static void SavedWordDrop(TestContext ctx)
    {
        string dir = Path.Combine(ctx.ScratchDir, "display-shadow-quality");
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        Directory.CreateDirectory(dir);
        string? previous = OptionsStore.DirectoryOverride;
        OptionsStore.DirectoryOverride = dir;
        try
        {
            OptionsStore.UserOptions().Save(new OptionsDef { ShadowQuality = ShadowQualitySetting.Low });
            ctx.Check(ShadowQualitySetting.SavedWord(det: false) == ShadowQualitySetting.Low,
                $"a plain launch reads the saved word ({ShadowQualitySetting.SavedWord(det: false) ?? "unset"})");
            ctx.Check(ShadowQualitySetting.SavedWord(det: true) == null,
                $"and a --det launch reads no saved word at all ({ShadowQualitySetting.SavedWord(det: true) ?? "unset"})");
        }
        finally
        {
            OptionsStore.DirectoryOverride = previous;
        }
    }

    private static string Describe(ShadowQualityPlan plan) =>
        $"{plan.Word} source={ShadowQualitySetting.Lookup.SourceName(plan.Source)}";

    private static string Describe(SunShadowPlan plan) => string.Format(CultureInfo.InvariantCulture,
        "cast={0} angle={1} blur={2} filter={3} atlas={4}", plan.Cast, plan.AngularDistance, plan.Blur, plan.Filter, plan.AtlasSize);

    private static string Join(List<(RenderingServer.ShadowQuality Filter, int Atlas)> calls) =>
        calls.Count == 0 ? "no calls" : string.Join(" ", calls.ConvertAll(c => $"{c.Filter}/{c.Atlas}"));
}
