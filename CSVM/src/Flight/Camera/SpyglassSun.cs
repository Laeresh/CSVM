using Godot;

namespace CSVM.Flight.Camera;

/// <summary>The spyglass picture's own sun under Enhanced Graphics: a shadowless copy of the world
/// sun on <see cref="UI.Boards.SplitScreen.SpyglassSunLayer"/>, which only the disc's camera draws.
/// The world sun sits on <see cref="UI.Boards.SplitScreen.SunLayer"/>, which that camera leaves out
/// (<see cref="SpyglassView.DiscMask"/>). So the disc is lit the same and renders no shadow pass of
/// its own. One per flight session, shared by every pane's disc; Original builds none. It takes the
/// sun again every frame (<see cref="Mirror"/>), since a zone apply, an Options apply and a live
/// graphics switch all write the sun.</summary>
public sealed partial class SpyglassSun : DirectionalLight3D
{
    private DirectionalLight3D _sun = null!;

    /// <summary>The world sun this copy follows.</summary>
    public DirectionalLight3D Source => _sun;

    /// <summary>A copy of <paramref name="sun"/> with its shadow off and on the disc's own layer,
    /// already matching it. Add it to the session's world.</summary>
    public static SpyglassSun Build(DirectionalLight3D sun)
    {
        var copy = new SpyglassSun
        {
            Name = "spyglass_sun",
            ShadowEnabled = false,
            Layers = UI.Boards.SplitScreen.SpyglassSunLayer,
            // A pause or a switch cover holds the session; the sun can still be written under one.
            ProcessMode = ProcessModeEnum.Always,
            _sun = sun,
        };
        Mirror(sun, copy);
        return copy;
    }

    /// <summary>Puts <paramref name="to"/> on <paramref name="from"/>'s bearing, visibility and every
    /// term of its light, the shadow excepted. Each is written only on a change, since this runs
    /// every frame. Public so the suite can hold the rule against a hand-written sun.</summary>
    public static void Mirror(DirectionalLight3D from, DirectionalLight3D to)
    {
        if (!GodotObject.IsInstanceValid(from))
        {
            return;
        }

        var basis = from.IsInsideTree() ? from.GlobalBasis : from.Basis;
        var current = to.IsInsideTree() ? to.GlobalBasis : to.Basis;
        if (!current.IsEqualApprox(basis))
        {
            if (to.IsInsideTree())
                to.GlobalBasis = basis;
            else
                to.Basis = basis;
        }

        if (to.Visible != from.Visible)
            to.Visible = from.Visible;
        if (to.LightColor != from.LightColor)
            to.LightColor = from.LightColor;
        if (to.LightEnergy != from.LightEnergy)
            to.LightEnergy = from.LightEnergy;
        if (to.LightIndirectEnergy != from.LightIndirectEnergy)
            to.LightIndirectEnergy = from.LightIndirectEnergy;
        if (to.LightVolumetricFogEnergy != from.LightVolumetricFogEnergy)
            to.LightVolumetricFogEnergy = from.LightVolumetricFogEnergy;
        if (to.LightSpecular != from.LightSpecular)
            to.LightSpecular = from.LightSpecular;
        if (to.LightAngularDistance != from.LightAngularDistance)
            to.LightAngularDistance = from.LightAngularDistance;
        if (to.LightNegative != from.LightNegative)
            to.LightNegative = from.LightNegative;
        if (to.LightCullMask != from.LightCullMask)
            to.LightCullMask = from.LightCullMask;
        if (to.SkyMode != from.SkyMode)
            to.SkyMode = from.SkyMode;
    }

    /// <summary>Whether <paramref name="copy"/> lights as <paramref name="sun"/> does with no shadow
    /// of its own, on the disc's layer alone. The suite's verdict.</summary>
    public static bool Matches(DirectionalLight3D sun, DirectionalLight3D copy) =>
        copy.GlobalBasis.IsEqualApprox(sun.GlobalBasis)
        && copy.Visible == sun.Visible
        && copy.LightColor.IsEqualApprox(sun.LightColor)
        && Mathf.IsEqualApprox(copy.LightEnergy, sun.LightEnergy)
        && Mathf.IsEqualApprox(copy.LightSpecular, sun.LightSpecular)
        && Mathf.IsEqualApprox(copy.LightAngularDistance, sun.LightAngularDistance)
        && !copy.ShadowEnabled
        && copy.Layers == UI.Boards.SplitScreen.SpyglassSunLayer;

    /// <inheritdoc/>
    public override void _Process(double delta) => Mirror(_sun, this);
}
