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
    // The distance and split fractions a sun was given before the Shadow Distance stretch. A live
    // change or a copy reads them, never a stretched value.
    private static readonly StringName RangeMeta = "csvm_shadow_range";

    /// <summary>Gives <paramref name="sun"/> a shadow distance before the Shadow Distance stretch, then
    /// stretches it. The splits stay as last given.</summary>
    public static void WriteDistance(DirectionalLight3D sun, float maxDistance)
    {
        var range = RangeOf(sun);
        sun.SetMeta(RangeMeta, new Vector4(maxDistance, range.Y, range.Z, range.W));
        Stretch(sun);
    }

    /// <summary>Gives <paramref name="sun"/> its three split fractions before the Shadow Distance
    /// stretch, then stretches them. The distance stays as last given.</summary>
    public static void WriteSplits(DirectionalLight3D sun, float split1, float split2, float split3)
    {
        sun.SetMeta(RangeMeta, new Vector4(RangeOf(sun).X, split1, split2, split3));
        Stretch(sun);
    }

    /// <summary>Puts <paramref name="sun"/>'s given range under the standing
    /// <see cref="ShadowDistanceSetting.EnhancedScale"/>: the distance times it, each split over it.
    /// The three near cascades keep their metres and only the far one reaches further. A sun never
    /// given a range is left alone.</summary>
    public static void Stretch(DirectionalLight3D sun)
    {
        if (!sun.HasMeta(RangeMeta))
            return;
        var range = sun.GetMeta(RangeMeta).AsVector4();
        float scale = ShadowDistanceSetting.EnhancedScale();
        sun.DirectionalShadowMaxDistance = range.X * scale;
        sun.DirectionalShadowSplit1 = range.Y / scale;
        sun.DirectionalShadowSplit2 = range.Z / scale;
        sun.DirectionalShadowSplit3 = range.W / scale;
    }

    /// <summary>Drops <paramref name="sun"/>'s given range, for a sun put back to a fresh light's.</summary>
    public static void ClearRange(DirectionalLight3D sun) => sun.RemoveMeta(RangeMeta);

    /// <summary>Copy every shadow setting from <paramref name="from"/> to <paramref name="to"/>,
    /// the distance clamped to <paramref name="maxDistance"/>, the far plane of the camera
    /// <paramref name="to"/> serves. ⚠ Never copy the pancake size. The session sun's deep one
    /// raises the depth bias, which a short-range pass with centimetre casters cannot take.</summary>
    public static void Copy(DirectionalLight3D from, DirectionalLight3D to, float maxDistance)
    {
        to.ShadowEnabled = from.ShadowEnabled;
        to.DirectionalShadowMode = from.DirectionalShadowMode;
        to.DirectionalShadowFadeStart = from.DirectionalShadowFadeStart;
        // The given splits, unstretched: the far reach is the session sun's, not a short pass's.
        var range = RangeOf(from);
        to.DirectionalShadowSplit1 = range.Y;
        to.DirectionalShadowSplit2 = range.Z;
        to.DirectionalShadowSplit3 = range.W;
        to.DirectionalShadowBlendSplits = from.DirectionalShadowBlendSplits;
        to.ShadowBias = from.ShadowBias;
        to.ShadowNormalBias = from.ShadowNormalBias;
        to.LightAngularDistance = from.LightAngularDistance;
        to.ShadowBlur = from.ShadowBlur;
        to.DirectionalShadowMaxDistance = Mathf.Min(from.DirectionalShadowMaxDistance, maxDistance);
    }

    // The given range, or the sun's own values where none was given yet.
    private static Vector4 RangeOf(DirectionalLight3D sun) => sun.HasMeta(RangeMeta)
        ? sun.GetMeta(RangeMeta).AsVector4()
        : new Vector4(sun.DirectionalShadowMaxDistance, sun.DirectionalShadowSplit1, sun.DirectionalShadowSplit2, sun.DirectionalShadowSplit3);
}
