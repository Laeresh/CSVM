using CSVM.Flight.Weapons;
using Godot;

namespace CSVM.Flight.Hud;

/// <summary>Which flat HUD's drawing a <see cref="WorldMark"/> takes.</summary>
public enum WorldMarkStyle
{
    /// <summary>The pilot's selected target: the bracket box while the gun reaches it, and the
    /// label block (<see cref="TargetHud"/>).</summary>
    Selected,

    /// <summary>A tag over an aircraft: the hostile fallback, a race pilot or a debug mark
    /// (<see cref="TargetHud"/>).</summary>
    Tagged,
}

/// <summary>One marker a flat HUD draws at a world point, as data: where, in which colour and
/// style, and what it says. The flat HUD projects it through its pane's camera, and
/// <see cref="XrMarkers"/> places it in the world. A selection's mark carries the
/// <paramref name="Target"/> it labels; any other carries the default.
/// <paramref name="Stagger"/> steps a tag along the edge when several share one bearing.</summary>
public readonly record struct WorldMark(WorldMarkStyle Style, Vector3 At, Color Color, string Tag,
    TargetRef Target, bool Boxed, int Stagger);
