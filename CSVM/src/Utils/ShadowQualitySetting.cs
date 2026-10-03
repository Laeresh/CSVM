using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CSVM.Utils;

/// <summary>What one shadow-quality level writes. The <paramref name="Cast"/> switch says whether
/// the sun casts at all. The <paramref name="AngularDistance"/> and <paramref name="Blur"/> go on
/// the sun. The <paramref name="Filter"/> and <paramref name="AtlasSize"/> go on the renderer,
/// which holds them for every directional light. A level that casts nothing leaves the renderer alone.</summary>
public readonly record struct SunShadowPlan(
    bool Cast, float AngularDistance, float Blur, RenderingServer.ShadowQuality Filter, int AtlasSize);

/// <summary>One resolved shadow quality: the word, what it writes, and the source that won. The
/// source is named so a log line can say which layer the run is obeying.</summary>
public readonly record struct ShadowQualityPlan(string Word, SunShadowPlan Sun, string Source);

/// <summary>
/// The sun's shadow quality under Enhanced Graphics, one of <see cref="Words"/>. The sources layer
/// as <see cref="GraphicsMode"/>'s do. <c>--shadow-quality=</c> beats the saved
/// <c>shadowQuality</c> option, which beats the <see cref="Key"/> config key, then the fallback.
/// That follows the GPU and the pane count (<see cref="FallbackFor"/>). The faithful path casts
/// no sun shadow, and nothing is written there.
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

    /// <summary>Every word, lowest first, the order both Options screens offer them.</summary>
    public static readonly IReadOnlyList<string> Words = new[] { Off, Low, Medium, High, Ultra };

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
    /// Options rows show it when nothing is saved.
    /// ⚠ The rows read this and never <see cref="MachineDefault"/>: a unit test has no renderer to ask.</summary>
    public static string Word { get; private set; } = Default;

    /// <summary>What the resolved word writes.</summary>
    public static SunShadowPlan Sun => PlanFor(Word);

    /// <summary>Counts the <see cref="ApplyTo"/> calls that moved a sun. A copy of the sun, the
    /// cockpit pass's interior light, re-reads the sun's shadow fields when this moves.</summary>
    public static int Revision { get; private set; }

    /// <summary>The panes the running session draws, which the fallback reads. 1 until
    /// <see cref="ResolveForPanes"/> names a splitscreen session's count.</summary>
    public static int Panes { get; private set; } = 1;

    /// <summary>The fallback word for this machine at the running session's <see cref="Panes"/>.
    /// The GPU is read once, since the device cannot change under a run.</summary>
    public static string MachineDefault => FallbackFor(_integrated ??= IsIntegratedGpu(), Panes);

    /// <summary>The fallback a launch resolves against. ⚠ <see cref="Default"/> under
    /// <paramref name="det"/>, so a scripted capture does not depend on the machine it runs on.</summary>
    public static string DefaultFor(bool det) => det ? Default : MachineDefault;

    /// <summary>The fallback on a GPU that is or is not <paramref name="integrated"/>, for a session
    /// drawing <paramref name="panes"/>. A discrete GPU runs <see cref="Default"/> at any count. An
    /// integrated one runs <see cref="IntegratedDefault"/>, or <see cref="IntegratedSplitDefault"/>
    /// from <see cref="SplitPanes"/> panes up.</summary>
    public static string FallbackFor(bool integrated, int panes) =>
        !integrated ? Default : panes >= SplitPanes ? IntegratedSplitDefault : IntegratedDefault;

    /// <summary>The level the sources resolve to, highest first: <paramref name="flagWord"/>, then
    /// <paramref name="savedWord"/>, then <paramref name="configWord"/>, then
    /// <paramref name="fallback"/>. A word this vocabulary does not know reads as never set and
    /// falls through. A config key spelling the fallback reads as the fallback. The absent key reads
    /// the same, so a caller hands the config read that fallback too. Stores the word as
    /// <see cref="Word"/>; <see cref="Pick"/> is the same ladder without the store.</summary>
    public static ShadowQualityPlan Resolve(string? flagWord, string? savedWord, string? configWord,
        string fallback = Default)
    {
        var plan = Pick(flagWord, savedWord, configWord, fallback);
        Word = plan.Word;
        return plan;
    }

    /// <summary><see cref="Resolve"/>'s ladder, leaving <see cref="Word"/> alone. The fallback's
    /// source names which rule chose it: <c>default</c>, <c>default_integrated_gpu</c>, or
    /// <c>default_integrated_gpu_panes</c> for the splitscreen rule.</summary>
    public static ShadowQualityPlan Pick(string? flagWord, string? savedWord, string? configWord,
        string fallback = Default)
    {
        if (IsWord(flagWord))
        {
            return Plan(flagWord!, "--shadow-quality");
        }

        if (IsWord(savedWord))
        {
            return Plan(savedWord!, "options.json");
        }

        if (configWord != fallback && IsWord(configWord))
        {
            return Plan(configWord!, Key);
        }

        if (configWord != null && configWord != fallback)
        {
            Log.Warn("world", $"config {Key}={configWord} is not one of {string.Join("/", Words)}; using {fallback}");
        }

        string source = fallback switch
        {
            IntegratedDefault => "default_integrated_gpu",
            IntegratedSplitDefault => "default_integrated_gpu_panes",
            _ => "default",
        };
        return Plan(fallback, source);
    }

    /// <summary>The launch's ladder again for a session drawing <paramref name="panes"/>, which
    /// becomes <see cref="Panes"/>. Only the fallback follows the count. The flag, the saved word
    /// and the config key win at every count, so a chosen level is never overruled.</summary>
    public static ShadowQualityPlan ResolveForPanes(int panes, string? flagWord, bool det)
    {
        Panes = Math.Max(1, panes);
        string fallback = DefaultFor(det);
        return Resolve(flagWord, SavedWord(det), Config.GetString(Key, fallback), fallback);
    }

    /// <summary>The saved word a launch reads, or null under <paramref name="det"/>.
    /// ⚠ A deterministic run reads no saved setting. The options file is one machine's state, and a
    /// golden shot is a <c>--det</c> run against the player's own directory.</summary>
    public static string? SavedWord(bool det) => det ? null : OptionsStore.UserOptions().Load().ShadowQuality;

    /// <summary>Whether <paramref name="word"/> is one of <see cref="Words"/>.</summary>
    public static bool IsWord(string? word) => word != null && Words.Contains(word);

    /// <summary>What <paramref name="word"/> writes, or <see cref="Default"/>'s for a word the
    /// vocabulary does not know.</summary>
    public static SunShadowPlan PlanFor(string? word)
    {
        int at = word == null ? -1 : IndexOf(word);
        return Plans[at >= 0 ? at : IndexOf(Default)];
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

    private static int IndexOf(string word)
    {
        for (int i = 0; i < Words.Count; i++)
        {
            if (Words[i] == word)
            {
                return i;
            }
        }

        return -1;
    }

    private static ShadowQualityPlan Plan(string word, string source) => new(word, PlanFor(word), source);
}
