using Godot;

namespace CSVM.Utils;

/// <summary>
/// The shadow settings one <see cref="DirectionalLight3D"/> hands another. The cockpit pass's own
/// sun takes the session sun's this way, at build and on a live graphics-mode switch. The launcher
/// resets the session sun from a fresh light the same way. It lives below Flight and Session so
/// both reach one field list.
/// </summary>
public static class SunShadow
{
    /// <summary>Copy every shadow setting from <paramref name="from"/> to <paramref name="to"/>,
    /// the distance clamped to <paramref name="maxDistance"/>, the far plane of the camera
    /// <paramref name="to"/> serves.</summary>
    public static void Copy(DirectionalLight3D from, DirectionalLight3D to, float maxDistance)
    {
        to.ShadowEnabled = from.ShadowEnabled;
        to.DirectionalShadowMode = from.DirectionalShadowMode;
        to.DirectionalShadowFadeStart = from.DirectionalShadowFadeStart;
        to.DirectionalShadowSplit1 = from.DirectionalShadowSplit1;
        to.DirectionalShadowSplit2 = from.DirectionalShadowSplit2;
        to.DirectionalShadowSplit3 = from.DirectionalShadowSplit3;
        to.DirectionalShadowBlendSplits = from.DirectionalShadowBlendSplits;
        to.ShadowBias = from.ShadowBias;
        to.ShadowNormalBias = from.ShadowNormalBias;
        to.LightAngularDistance = from.LightAngularDistance;
        to.ShadowBlur = from.ShadowBlur;
        to.DirectionalShadowMaxDistance = Mathf.Min(from.DirectionalShadowMaxDistance, maxDistance);
    }
}
