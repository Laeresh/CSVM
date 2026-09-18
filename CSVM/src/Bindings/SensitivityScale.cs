using System;

namespace CSVM.Bindings;

/// <summary>The mouse sensitivity a seat flies the Fly scheme at, as a multiplier on the captured
/// stick's travel: higher is more sensitive, so 2 reaches full deflection in half the hand movement.
/// The range and the slider scale a page shows it on are here so both presentations and the store
/// agree on one set of numbers; the arithmetic is in <c>docs/controls.md</c>, "Flying with the
/// mouse". Remake-only: the original's Mouse Sensitivity slider moved the desktop cursor.</summary>
public static class SensitivityScale
{
    /// <summary>The multiplier a seat flies at until its player moves it, and what a keymap file
    /// written without the field reads as: the captured stick unscaled.</summary>
    public const float Default = 1f;

    /// <summary>The least sensitive setting, four times the default hand travel.</summary>
    public const float Min = 0.25f;

    /// <summary>The most sensitive setting, a quarter of the default hand travel.</summary>
    public const float Max = 4f;

    /// <summary>The top of the slider scale; the bottom is 0 and the default stands at its middle.
    /// </summary>
    public const int MaxLevel = 100;

    /// <summary>One sideways step on either presentation, in slider levels.</summary>
    public const int LevelStep = 5;

    // Levels from the middle per doubling. Fifty levels each side of the default span a factor of
    // four, so the scale is even in ratio: a step multiplies the travel by the same amount anywhere.
    private const float LevelsPerDoubling = 25f;

    /// <summary><paramref name="value"/> inside the range, with anything that is not a finite number
    /// read as the default, so a hand-edited file cannot stop the stick moving.</summary>
    public static float Clamp(float value) =>
        float.IsFinite(value) ? Math.Clamp(value, Min, Max) : Default;

    /// <summary>The slider level that shows <paramref name="value"/>, rounded to the nearest one.
    /// </summary>
    public static int Level(float value) =>
        Math.Clamp((int)MathF.Round((MaxLevel / 2f) + (LevelsPerDoubling * MathF.Log2(Clamp(value)))), 0, MaxLevel);

    /// <summary>The multiplier a slider level stands for.</summary>
    public static float FromLevel(int level) =>
        Clamp(MathF.Pow(2f, (Math.Clamp(level, 0, MaxLevel) - (MaxLevel / 2f)) / LevelsPerDoubling));

    /// <summary><paramref name="value"/> moved <paramref name="direction"/> steps along the slider
    /// scale, clamped at both ends.</summary>
    public static float Step(float value, int direction) =>
        FromLevel(Level(value) + (direction * LevelStep));

    /// <summary>The multiplier as a page prints it, two decimals and an x.</summary>
    public static string Label(float value) =>
        Clamp(value).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "x";
}
