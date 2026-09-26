using System;

namespace CSVM.Utils;

/// <summary>
/// The enhanced mode's view distance: how far out it pushes each zone's authored fog, as one of
/// four saved words. The faithful path ignores it and keeps the authored ranges, since the fog is
/// where the original's own view distance lives (docs/org/weather.md). Its home is
/// <see cref="OptionsStore"/>'s <c>viewDistance</c> field, which both Options screens write and an
/// apply switches live, like <see cref="GraphicsMode"/>.
/// </summary>
public static class ViewDistance
{
    /// <summary>The shipped word, the push enhanced mode always had.</summary>
    public const string Default = "normal";

    /// <summary>The words the option carries, nearest first.</summary>
    public static readonly string[] Words = { "normal", "far", "farther", "farthest" };

    // TUNE. Multiples of the authored fog range. Normal is enhanced mode's standing push; the rest
    // open the tightest zones (C5's 1500-2250 m night haze) out to several kilometres.
    private static readonly float[] Scales = { 2f, 3f, 4f, 6f };

    /// <summary>The fog push the current word resolves to, what <c>WeatherRig.FogRangeFor</c>
    /// multiplies an authored range by in enhanced mode.</summary>
    public static float FogScale { get; private set; } = Scales[0];

    /// <summary>The position of <paramref name="word"/> in <see cref="Words"/>, or the default's
    /// for a null or unknown word.</summary>
    public static int Index(string? word)
    {
        int i = word == null ? -1 : Array.IndexOf(Words, word);
        return i < 0 ? 0 : i;
    }

    /// <summary>The push <paramref name="word"/> names, the default's for a null or unknown word.</summary>
    public static float ScaleOf(string? word) => Scales[Index(word)];

    /// <summary>Resolve <see cref="FogScale"/> from a saved word, at launch and on every apply. The
    /// caller re-lights the zone after a change, since the fog globals are written per zone.</summary>
    public static void Set(string? word) => FogScale = ScaleOf(word);
}
