using CSVM.Mech3;
using Godot;

namespace CSVM.Mech3.Anim;

// ---- LIGHT_STATE / LIGHT_ANIMATION ----

/// <summary>
/// One of the world's animated point lights. What the original did with these was modulate
/// the vertex lighting of nearby geometry; the visible flare at the light's own position is
/// separate gamez Facade geometry that already renders (C1's <c>docklight_flare</c> →
/// <c>dock_liteflare.tif</c>, <c>flame01</c> → <c>fire101.tif</c>). See
/// <see cref="WorldLights"/> for how the spill reaches the fullbright shader.
/// </summary>
internal sealed class AnimLight
{
    public Node3D? Host;          // AT_NODE target — the light rides its world pose

    public string? HostName;      // the AT_NODE name Host was resolved from (see HandleLightState)

    public Vector3 Offset;        // AT_NODE's trailing offset, in the host's own frame

    public Color Color = new(1f, 1f, 1f);

    public float RangeMin, RangeMax;

    public bool Active;

    // LIGHT_ANIMATION: signed deltas applied over run_time (see HandleLightAnimation).
    public float TweenLeft;

    public Color ColorRate;

    public float MinRate, MaxRate;
}
