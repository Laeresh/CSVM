using System.Collections.Generic;

namespace CSVM.UI.Menu.Original;

/// <summary>
/// The Original shell's continuous control: a pointer's hold-and-move over a slider row, and the
/// sideways step that moves one from the keyboard or the pad. It is the shell's second
/// hold-and-move, the list thumb's being the first, and it keeps a hold of its own for that
/// reason. The shell gives the thumb first refusal each frame and consults this one only when no
/// list has hold, so a drag that took hold of a scrollbar cannot finish on a slider nor a
/// slider's on a scrollbar. A row declares its slider through <see cref="OriginalSlider"/>; this
/// class knows the track and the value and nothing about the setting behind them.
/// </summary>
public sealed class SliderControl
{
    /// <summary>How far one sideways press moves a slider: twenty presses cross the whole range,
    /// fine enough to land on a considered level and coarse enough to reach either end without
    /// holding the key. The original's page is mouse-only and settles nothing here.</summary>
    public const int KeyStep = 5;

    /// <summary>The key of the slider row the pointer has hold of, or null.</summary>
    public string? Held { get; private set; }

    /// <summary>A sideways step on a focused slider row, clamped at both ends where every other
    /// stepped row on this shell wraps: a step from silence must not land on full volume. True
    /// when the focused row was a slider, whether or not the value moved, so a step at an end is
    /// spent on the slider rather than crossing columns.</summary>
    public static bool StepValue(IReadOnlyList<OriginalRow> rows, int focus, int direction)
    {
        if (focus < 0 || focus >= rows.Count || rows[focus].Slider is not { } slider)
        {
            return false;
        }

        Write(slider, slider.Track.Stepped(slider.Value, direction, KeyStep));
        return true;
    }

    /// <summary>One frame of the pointer against a screen's rows: a click inside a slider row
    /// takes hold of it and sets its value to the pressed point, a held pointer tracks it, and
    /// letting the button go lets the slider go. True while a slider holds the pointer, which is
    /// what tells the shell the click was spent on the slider so nothing under it is
    /// activated.</summary>
    public bool Drive(IReadOnlyList<OriginalRow> rows, MenuPointer pointer, out bool changed)
    {
        changed = false;
        if (Held is { } held)
        {
            if (!pointer.Pressed)
            {
                Held = null;
                changed = true;
                return false;
            }

            if (Find(rows, held) is not { } slider)
            {
                // The row the drag took hold of is gone with its screen.
                Held = null;
                return false;
            }

            changed = Write(slider, slider.Track.ValueAt(pointer.X));
            return true;
        }

        if (!pointer.Clicked)
        {
            return false;
        }

        // Later rows draw over earlier ones, so the last one hit wins, as the shell's own hit
        // test reads them.
        for (int i = rows.Count - 1; i >= 0; i--)
        {
            var row = rows[i];
            if (row.Slider is not { } slider || !row.Enabled || !row.Visible || !row.Contains(pointer.X, pointer.Y))
            {
                continue;
            }

            Held = row.Key;
            changed = Write(slider, slider.Track.ValueAt(pointer.X));
            return true;
        }

        return false;
    }

    /// <summary>Lets go of whatever the pointer held, which opening a screen does.</summary>
    public void LetGo() => Held = null;

    private static bool Write(OriginalSlider slider, int value)
    {
        if (value == slider.Value)
        {
            return false;
        }

        slider.SetValue(value);
        return true;
    }

    private static OriginalSlider? Find(IReadOnlyList<OriginalRow> rows, string key)
    {
        foreach (var row in rows)
        {
            if (row.Key == key && row.Slider != null)
            {
                return row.Slider;
            }
        }

        return null;
    }
}
