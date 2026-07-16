using System;
using Godot;

namespace CrimsonSkies.Mech3;

/// <summary>
/// Wingtip navigation lights. Every player plane carries two sprite quads
/// <c>wing_flare1</c>/<c>wing_flare2</c> at the wingtips, skinned with the soft glow
/// <c>oil_liteflare.tif</c> (a white core fading to transparent black). The original
/// hides them at spawn and flashes them on a 1.5 s cycle: <c>wing_light.json</c>'s
/// <c>wing_lights_blink</c> anim (wired into most plane defs via <c>start_anims</c>) has
/// a RESET_STATE that deactivates both flares, and a looping <c>blink_lights</c> sequence
/// that activates them (plus two warm point lights, COLOR 0.88/0.78/0.36) for a single
/// frame every SEQUENCE_OFFSET 1.5 s — a brief blink.
///
/// PlaneBuilder hides the flares at build time (both viewers) and re-skins each as an
/// additive, camera-facing billboard (the source quads are one-sided, so they used to
/// show only from behind the plane); in flight a <see cref="Flight.WingLightBlinker"/>
/// flashes them on the cycle. The 1.25 m point lights the anim also toggles are
/// negligible at chase-cam distance, so no OmniLight3D is emitted.
/// </summary>
public static class WingLights
{
    /// <summary>The wingtip flare sprite nodes toggled by the blink anim (wing_flare1/2).</summary>
    public static bool IsFlare(string name) =>
        name.StartsWith("wing_flare", StringComparison.OrdinalIgnoreCase);

    /// <summary>The glow sprite the flare quads are skinned with. It is also used by a few
    /// real airframe meshes (lwingbend, piece1, …), so the additive-billboard treatment is
    /// scoped to the flare nodes by name, not routed through this texture.</summary>
    public const string FlareTexture = "oil_liteflare";

    /// <summary>Warm amber the original flashes the lights — wing_light.json's LIGHT_STATE COLOR.</summary>
    public static readonly Color FlareColor = new(0.88f, 0.78f, 0.36f);

    /// <summary>Blink cycle length — wing_light.json's LOOP SEQUENCE_OFFSET.</summary>
    public const float BlinkPeriod = 1.5f;
}
