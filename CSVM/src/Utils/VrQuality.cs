using System.Collections.Generic;
using Godot;

namespace CSVM.Utils;

/// <summary>What a session flies at in place of the flat words. It names the profile that won, the
/// anti-aliasing method, the root viewport's MSAA, the sun's shadow level and the foveation switch.
/// A null word leaves the flat resolution alone.</summary>
public readonly record struct VrQualityPlan(
    string Profile, string? AntiAliasing, string? Msaa, string? ShadowQuality, bool Foveation);

/// <summary>
/// The VR quality profile: what a headset session renders at in place of the flat options. It
/// holds the headset's rate at the runtime's own eye size. Only a live headset or the
/// stand-in reads it, never the flat game. The <c>--xr-quality=flat</c> flag flies the headset on
/// the flat words, and a setting's own flag beats both. Engine-free, so the selection unit-tests.
/// What each default costs and buys: docs/architecture/Utils.md.
/// </summary>
public static class VrQuality
{
    /// <summary>The profile that replaces the flat words under a headset.</summary>
    public const string Vr = "vr";

    /// <summary>The profile that keeps the flat words under a headset.</summary>
    public const string Flat = "flat";

    /// <summary>The plan of a session with no headset: nothing replaced.</summary>
    public const string None = "none";

    /// <summary>The anti-aliasing method the profile flies, one of
    /// <see cref="DisplayWords.AntiAliasingChoices"/>. MSAA carries the edges, and the frame keeps
    /// no temporal history that a turning head would smear.</summary>
    public const string AntiAliasing = DisplayWords.AntiAliasingOff;

    /// <summary>The root viewport's MSAA under the profile, one of <see cref="MsaaWords"/>: the
    /// project's own 4x, the faithful look's edges.</summary>
    public const string Msaa = "4x";

    /// <summary>The Enhanced sun's shadow level under the profile. ⚠ Do not raise it to a level with
    /// a penumbra on Godot 4.7.0 or 4.7.1. Their soft sun shadow costs more the farther the camera is
    /// from the world origin (docs/architecture/Utils.md).</summary>
    public const string ShadowQuality = ShadowQualitySetting.Low;

    /// <summary>Whether the profile shades the eye's edges coarser. Off: it saves about 0.9 ms at
    /// 3072x3264 but lifts the night city's window textures toward their coarser mips there.</summary>
    public const bool Foveation = false;

    /// <summary>The foveation flag's words.</summary>
    public static readonly IReadOnlyList<string> SwitchWords = new[] { "off", "on" };

    /// <summary>The MSAA words, lowest first.</summary>
    public static readonly IReadOnlyList<string> MsaaWords = new[] { "off", "2x", "4x", "8x" };

    /// <summary>The profile flag's lookup. No saved word or config key is read.</summary>
    public static readonly WordSetting Lookup = new("xr.quality", new[] { Vr, Flat }, "--xr-quality");

    /// <summary>The MSAA flag's lookup, which sets the root viewport's 3D MSAA, flat or stereo.</summary>
    public static readonly WordSetting MsaaLookup = new("graphics.msaa", MsaaWords, "--msaa");

    /// <summary>The foveation flag's lookup, read under a headset only.</summary>
    public static readonly WordSetting FoveationLookup = new("xr.foveation", SwitchWords, "--xr-foveation");

    /// <summary>The plan the launcher resolved for this process, set once before any viewport is
    /// built. Every later resolve of a word the plan names reads it in place of the flag.</summary>
    public static VrQualityPlan Current { get; set; } = new(None, null, null, null, false);

    /// <summary>The plan a session flies at: the profile's words under <paramref name="headset"/>
    /// unless <paramref name="profileFlag"/> asks for <see cref="Flat"/>, nothing without one. A
    /// setting's own flag beats the profile's word for that setting, with or without a headset; the
    /// foveation flag needs one.</summary>
    public static VrQualityPlan Resolve(bool headset, string? profileFlag, string? antiAliasingFlag,
        string? msaaFlag, string? shadowFlag, string? foveationFlag)
    {
        bool on = headset && Lookup.Resolve(profileFlag, null, null, Vr).Word == Vr;
        bool foveation = headset && (foveationFlag != null ? foveationFlag == "on" : on && Foveation);
        return new VrQualityPlan(
            !headset ? None : on ? Vr : Flat,
            antiAliasingFlag ?? (on ? AntiAliasing : null),
            msaaFlag ?? (on ? Msaa : null),
            shadowFlag ?? (on ? ShadowQuality : null),
            foveation);
    }

    /// <summary>Godot's MSAA value for <paramref name="word"/>, null for a word outside
    /// <see cref="MsaaWords"/>.</summary>
    public static Viewport.Msaa? MsaaOf(string? word) => word switch
    {
        "off" => Viewport.Msaa.Disabled,
        "2x" => Viewport.Msaa.Msaa2X,
        "4x" => Viewport.Msaa.Msaa4X,
        "8x" => Viewport.Msaa.Msaa8X,
        _ => null,
    };

    /// <summary>The launch's log line, or null for a flat launch no flag moved.</summary>
    public static string? Describe(in VrQualityPlan plan) =>
        plan.Profile == None && plan.Msaa == null
            ? null
            : $"vr quality: profile={plan.Profile} anti_aliasing={plan.AntiAliasing ?? "flat"} msaa={plan.Msaa ?? "project"} shadow_quality={plan.ShadowQuality ?? "flat"} foveation={(plan.Foveation ? "on" : "off")}";
}
