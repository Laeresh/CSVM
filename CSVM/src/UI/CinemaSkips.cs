using Godot;

namespace CSVM.UI;

/// <summary>What a press is, as a skip set reads it. A screen reads the device event it was handed
/// into one of these and asks <see cref="CinemaSkips"/>, so the films and the boot stills answer
/// one rule instead of a predicate each.</summary>
public enum CinemaPress
{
    /// <summary>Nothing any set takes: a release, a key repeat, a stick, a pad that does not
    /// count.</summary>
    None,

    /// <summary>Escape.</summary>
    Escape,

    /// <summary>Space.</summary>
    Space,

    /// <summary>Return, the main one or the keypad's.</summary>
    Return,

    /// <summary>A left mouse press.</summary>
    LeftMouse,

    /// <summary>A key none of the named ones, which only an any-press set takes.</summary>
    OtherKey,

    /// <summary>A gamepad button.</summary>
    PadButton,
}

/// <summary>The one member that decides what skips what, and the reading of a device event that
/// feeds it. <see cref="Skips"/> holds the whole rule and takes no engine, so every set is pinned
/// off engine; <see cref="PressOf"/> is the engine's half and holds no policy of its own.</summary>
public static class CinemaSkips
{
    /// <summary>Whether <paramref name="press"/> ends a cinema whose set is <paramref name="skip"/>.
    /// <see cref="CinemaSkip.AnyPress"/> takes every press there is, a pad button included, because
    /// the screens carrying it have taught the player no key yet.</summary>
    public static bool Skips(this CinemaSkip skip, CinemaPress press) => press switch
    {
        CinemaPress.Escape => skip.HasFlag(CinemaSkip.Escape) || skip.HasFlag(CinemaSkip.AnyPress),
        CinemaPress.Space => skip.HasFlag(CinemaSkip.Space) || skip.HasFlag(CinemaSkip.AnyPress),
        CinemaPress.Return => skip.HasFlag(CinemaSkip.Return) || skip.HasFlag(CinemaSkip.AnyPress),
        CinemaPress.LeftMouse => skip.HasFlag(CinemaSkip.LeftMouse) || skip.HasFlag(CinemaSkip.AnyPress),
        CinemaPress.PadButton => skip.HasFlag(CinemaSkip.PadButton) || skip.HasFlag(CinemaSkip.AnyPress),
        CinemaPress.OtherKey => skip.HasFlag(CinemaSkip.AnyPress),
        _ => false,
    };

    /// <summary>What a device event is to a skip set. <paramref name="padsLive"/> is whether pad
    /// input counts at all: a button arriving while it does not is no press, since a pad reports
    /// its first button pressed as it connects and <c>--no-pads</c> would otherwise still see it.
    /// </summary>
    public static CinemaPress PressOf(InputEvent @event, bool padsLive) => @event switch
    {
        InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } => CinemaPress.LeftMouse,
        InputEventJoypadButton { Pressed: true } => padsLive ? CinemaPress.PadButton : CinemaPress.None,
        InputEventKey { Pressed: true, Echo: false } key => key.Keycode switch
        {
            Key.Escape => CinemaPress.Escape,
            Key.Space => CinemaPress.Space,
            Key.Enter or Key.KpEnter => CinemaPress.Return,
            _ => CinemaPress.OtherKey,
        },
        _ => CinemaPress.None,
    };
}
