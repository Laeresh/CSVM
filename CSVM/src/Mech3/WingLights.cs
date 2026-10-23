using System;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Wingtip navigation lights: the flare-node predicate (<c>wing_flare1</c>/<c>wing_flare2</c>),
/// glow texture, blink colour and period, and point-light range.
/// Each constant below names its own <c>wing_light.json</c> source.
/// PlaneBuilder hides and re-skins the flares, additive and posed as the SphericalY facade the
/// data classifies them as. <c>Flight.Airframe.WingLightBlinker</c> flashes them and emits a
/// matching OmniLight3D per side.
/// </summary>
public static class WingLights
{
    /// <summary>The glow sprite the flare quads are skinned with. It is also used by a few
    /// real airframe meshes (lwingbend, piece1, …), so the additive-tint treatment is
    /// scoped to the flare nodes by name, not routed through this texture.</summary>
    public const string FlareTexture = "oil_liteflare";

    /// <summary>Blink cycle length, wing_light.json's LOOP SEQUENCE_OFFSET.</summary>
    public const float BlinkPeriod = 1.5f;

    /// <summary>Point-light falloff pair, wing_light.json's LIGHT_STATE RANGE. Godot's
    /// OmniLight3D has no inner radius, so both feed <c>WorldLights.OmniRange</c>.</summary>
    public const float FlareRangeMin = 0.5f;
    public const float FlareRangeMax = 1.25f;

    /// <summary>On a plane rig's flare node: the names of the original flares it replaced. A def
    /// darkening one of those by name darkens this node instead (<see cref="PlaneRigPatch"/>).</summary>
    public const string StandsForMeta = "csvm_flare_stands_for";

    /// <summary>Warm amber the original flashes the lights, wing_light.json's LIGHT_STATE COLOR.</summary>
    public static readonly Color FlareColor = new(0.88f, 0.78f, 0.36f);

    /// <summary>The wingtip flare sprite nodes toggled by the blink anim (wing_flare1/2).
    /// ⚠ Never read the side off the digit: <c>wing_flare1</c> is port on every airframe but the
    /// Warhawk, where it is starboard (docs/formats/model-packs.md, <c>rig.json</c>).</summary>
    public static bool IsFlare(string name) =>
        name.StartsWith("wing_flare", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="flare"/> answers to <paramref name="name"/>: its own node
    /// name, or an original flare a plane rig put it in place of.</summary>
    public static bool AnswersTo(Node3D flare, string name)
    {
        if (flare.Name.ToString().Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return flare.HasMeta(StandsForMeta)
            && Array.Exists(flare.GetMeta(StandsForMeta).AsStringArray(), n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
}
