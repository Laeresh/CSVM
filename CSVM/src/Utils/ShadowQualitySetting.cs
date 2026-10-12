using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Utils;

/// <summary>What one shadow-quality level writes. The <paramref name="Cast"/> switch says whether
/// the sun casts at all. The <paramref name="AngularDistance"/> and <paramref name="Blur"/> go on
/// the sun. The <paramref name="Filter"/> and <paramref name="AtlasSize"/> go on the renderer,
/// which holds them for every directional light. A level that casts nothing leaves the renderer alone.</summary>
public readonly record struct SunShadowPlan(
    bool Cast, float AngularDistance, float Blur, RenderingServer.ShadowQuality Filter, int AtlasSize);

/// <summary>One resolved shadow quality: the word, what it writes, the source that won and the
/// fallback rule that chose a fallback word. A log line names them.</summary>
public readonly record struct ShadowQualityPlan(string Word, SunShadowPlan Sun, SettingSource Source, string? Rule = null);

/// <summary>
/// The sun's shadow quality under Enhanced Graphics, one of <see cref="Words"/>. In
/// <see cref="Lookup"/>, <c>--shadow-quality=</c> beats the saved <c>shadowQuality</c> option,
/// which beats the <see cref="Key"/> config key, then the fallback. That follows the GPU and the
/// pane count (<see cref="FallbackFor"/>), so a splitscreen session resolves it again
/// (<see cref="WordSettings.FollowPanes"/>). The faithful path casts no sun shadow.
/// The writer, <see cref="ApplyTo"/>, can run again at any time, which is how an Options apply
/// reaches a flight in progress. Why each level stands where it does:
/// analysis/screen-dither/FINDINGS.md.
/// </summary>
public static class ShadowQualitySetting
{
    /// <summary>The config key under the saved option, one of the five words.</summary>
    public const string Key = "graphics.shadowQuality";

    /// <summary>No sun shadow at all.</summary>
    public const string Off = "off";

    /// <summary>A hard-edged sun shadow on a four-tap filter.</summary>
    public const string Low = "low";

    /// <summary>A quarter-degree penumbra on the middle filter rung.</summary>
    public const string Medium = "medium";

    /// <summary>The real sun's half-degree penumbra on the second filter rung.</summary>
    public const string High = "high";

    /// <summary>The one-degree penumbra on the top filter rung, the enhanced mode's own look.</summary>
    public const string Ultra = "ultra";

    /// <summary>The word a launch with nothing saved runs on a discrete GPU, and under
    /// <c>--det</c> on every machine.</summary>
    public const string Default = Ultra;

    /// <summary>The word an integrated GPU (the Steam Deck's APU) runs when nothing is set. On the
    /// Deck, C5 at 67% with TAA ran 56 fps at Ultra and over 60 at High.</summary>
    public const string IntegratedDefault = High;

    /// <summary>The word an integrated GPU runs when nothing is set and the session draws
    /// <see cref="SplitPanes"/> or more panes. On the Deck the four-pane C3 AI fight at 67% with
    /// TAA spent 2.05 of its 16.13 ms frame on the High sun's shadow pass.</summary>
    public const string IntegratedSplitDefault = Off;

    /// <summary>The fewest panes that take <see cref="IntegratedSplitDefault"/>.</summary>
    public const int SplitPanes = 3;

    /// <summary>The log's name for the fallback rule that chose <see cref="IntegratedDefault"/>.</summary>
    public const string IntegratedRule = "default_integrated_gpu";

    /// <summary>The log's name for the fallback rule that chose <see cref="IntegratedSplitDefault"/>.</summary>
    public const string IntegratedSplitRule = "default_integrated_gpu_panes";

    /// <summary>Every word, lowest first, the order both Options screens offer them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { Off, Low, Medium, High, Ultra };

    /// <summary>The setting's registry entry, beaten by <c>--shadow-quality=</c>. Its fallback follows
    /// the GPU and the pane count, never the graphics mode.</summary>
    public static readonly WordSetting Lookup = Register();

    // ⚠ Do not lower a filter rung without narrowing the sun with it. The penumbra is sampled
    // through a disc rotated per screen pixel, and too few samples weave that rotation over every
    // lit surface. SoftUltra holds 1.0 degree, SoftHigh 0.5 and SoftMedium 0.25.
    // ⚠ Keep Ultra's atlas at 8192. It is the map the aircraft's own shadow was judged on under the
    // one-degree sun; the narrower levels hold that shadow on 4096.
    private static readonly SunShadowPlan[] Plans =
    {
        new(false, 0f, 0f, RenderingServer.ShadowQuality.Hard, 4096),
        new(true, 0f, 1f, RenderingServer.ShadowQuality.SoftLow, 4096),
        new(true, 0.25f, 1f, RenderingServer.ShadowQuality.SoftMedium, 4096),
        new(true, 0.5f, 1f, RenderingServer.ShadowQuality.SoftHigh, 4096),
        new(true, 1f, 1f, RenderingServer.ShadowQuality.SoftUltra, 8192),
    };

    private static bool? _integrated;

    /// <summary>The word the run resolved, <see cref="Default"/> until <see cref="Resolve"/> runs. The
    /// Options rows show it when nothing is saved.</summary>
    public static string Word { get; private set; } = Default;

    /// <summary>What the resolved word writes.</summary>
    public static SunShadowPlan Sun => PlanFor(Word);

    /// <summary>Counts the <see cref="ApplyTo"/> calls that moved a sun. A copy of the sun, the
    /// cockpit pass's interior light, re-reads the sun's shadow fields when this moves.</summary>
    public static int Revision { get; private set; }

    /// <summary>Gets or sets whether <c>--no-soft-shadows</c> keeps the shadow and drops its penumbra.
    /// The launcher sets it once from the spec, before the sun is dressed.</summary>
    public static bool HardShadows { get; set; }

    /// <summary>Whether the renderer runs on an integrated GPU, the Steam Deck's APU among them. Read
    /// once, since the device cannot change under a run. <see cref="WaterQualitySetting"/>'s fallback
    /// reads it too.</summary>
    public static bool IntegratedGpu => _integrated ??= IsIntegratedGpu();

    /// <summary>The fallback on a GPU that is or is not <paramref name="integrated"/>, for a session
    /// drawing <paramref name="panes"/>. A discrete GPU runs <see cref="Default"/> at any count. An
    /// integrated one runs <see cref="IntegratedDefault"/>, or <see cref="IntegratedSplitDefault"/>
    /// from <see cref="SplitPanes"/> panes up.</summary>
    public static string FallbackFor(bool integrated, int panes) =>
        !integrated ? Default : panes >= SplitPanes ? IntegratedSplitDefault : IntegratedDefault;

    /// <summary>The level <see cref="Pick"/> resolves the sources to, stored as
    /// <see cref="Word"/>.</summary>
    public static ShadowQualityPlan Resolve(string? flagWord, string? savedWord, string? configWord,
        string fallback = Default)
    {
        var plan = Pick(flagWord, savedWord, configWord, fallback);
        Word = plan.Word;
        return plan;
    }

    /// <summary>The level <see cref="Lookup"/> resolves <paramref name="flagWord"/>,
    /// <paramref name="savedWord"/> and <paramref name="configWord"/> to over
    /// <paramref name="fallback"/>, leaving <see cref="Word"/> alone. The fallback's source names
    /// which rule chose it, the integrated GPU's or its splitscreen rule.</summary>
    public static ShadowQualityPlan Pick(string? flagWord, string? savedWord, string? configWord,
        string fallback = Default)
    {
        var resolved = Lookup.Resolve(flagWord, savedWord, configWord, fallback, FallbackRule(fallback));
        return new ShadowQualityPlan(resolved.Word, PlanFor(resolved.Word), resolved.Source, resolved.Rule);
    }

    /// <summary>What <paramref name="word"/> writes, or <see cref="Default"/>'s for a word the
    /// vocabulary does not know.</summary>
    public static SunShadowPlan PlanFor(string? word)
    {
        int at = Lookup.IndexOf(word);
        return Plans[at >= 0 ? at : Lookup.IndexOf(Default)];
    }

    /// <summary>Put <paramref name="sun"/> and the renderer on <paramref name="plan"/>, now or again.
    /// Nothing is written unless <paramref name="enhanced"/>. The <paramref name="hard"/> door is
    /// <c>--no-soft-shadows</c>, which keeps the shadow and drops its penumbra. The
    /// <paramref name="renderer"/> recorder takes the renderer's pair in place of the server, which
    /// has no getter for either.</summary>
    public static void ApplyTo(DirectionalLight3D sun, SunShadowPlan plan, bool enhanced, bool hard,
        Action<RenderingServer.ShadowQuality, int>? renderer = null)
    {
        if (!enhanced)
        {
            return;
        }

        Revision++;
        sun.ShadowEnabled = plan.Cast;
        if (!plan.Cast)
        {
            return;
        }

        sun.LightAngularDistance = hard ? 0f : plan.AngularDistance;
        sun.ShadowBlur = hard ? 0f : plan.Blur;
        (renderer ?? WriteRenderer)(hard ? RenderingServer.ShadowQuality.Hard : plan.Filter, plan.AtlasSize);
    }

    // Renderer-wide rather than light properties. Set beside the width they carry, not in
    // project.godot, where the faithful path would inherit them.
    private static void WriteRenderer(RenderingServer.ShadowQuality filter, int atlasSize)
    {
        RenderingServer.DirectionalSoftShadowFilterSetQuality(filter);
        RenderingServer.DirectionalShadowAtlasSetSize(atlasSize, true);
    }

    // A headless host reports no adapter, which reads as discrete.
    private static bool IsIntegratedGpu() =>
        RenderingServer.GetVideoAdapterType() == RenderingDevice.DeviceType.IntegratedGpu;

    // The fallback word names the rule that chose it, since FallbackFor gives each rule its own word.
    private static string? FallbackRule(string fallback) => fallback switch
    {
        IntegratedDefault => IntegratedRule,
        IntegratedSplitDefault => IntegratedSplitRule,
        _ => null,
    };

    // Built in a method rather than inline. Inside a static field initializer, the nullable analysis
    // reads the Shown hook's static Word as maybe null and warns (CS8603).
    private static WordSetting Register() => new(Key, Words, "--shadow-quality")
    {
        SavedField = "shadowQuality",
        LogName = "shadow_quality",
        Title = "Shadow quality",
        Labels = new[] { "Off", "Low", "Medium", "High", "Ultra" },
        EnhancedOnly = true,
        Detail = static enhanced => enhanced
            ? "Select how the sun's shadows are drawn. Lower levels run faster. Seen once the choices are applied."
            : "Enhanced Graphics only. The original world casts no sun shadows.",
        // ⚠ The rows read the resolved word and never the machine's fallback: a unit test has no
        // renderer to ask.
        Shown = static _ => Word,
        Fallback = static context =>
        {
            string fallback = context.Det ? Default : FallbackFor(IntegratedGpu, context.Panes);
            return new ResolvedWord(fallback, SettingSource.Default, FallbackRule(fallback));
        },
        Take = static resolved =>
        {
            Word = resolved.Word;
            return resolved;
        },
        Apply = static world =>
        {
            if (world.Sun is { } sun)
                ApplyTo(sun, Sun, GraphicsMode.Enhanced, HardShadows);
        },
    };
}
