using CSVM.Flight.Hangar;
using CSVM.Mech3;

namespace CSVM.UI.Hangar;

/// <summary>The part of the plane the hangar's live picture swings its camera to after an edit.</summary>
public enum HangarPreviewFocus
{
    /// <summary>No swing: the edit moves nothing the camera has to find.</summary>
    None,

    /// <summary>The left wing from below and in front, where its rockets hang.</summary>
    LeftWing,

    /// <summary>The right wing from below and in front.</summary>
    RightWing,

    /// <summary>The nose decal's side of the fuselage.</summary>
    Nose,

    /// <summary>The tail decal's side of the fin.</summary>
    Tail,

    /// <summary>The wing decal, from above.</summary>
    Wing,
}

/// <summary>
/// What the hangar's live plane picture shows, engine-free: the airframe, the two wings' hardpoint
/// counts and the paint. Guns, engine and armour are left out, since the flown model shows none of
/// them. Value equality lets the stage rebuild, rehang or repaint only on the field that moved, and
/// <see cref="FocusFor"/> names where the camera looks after an edit.
/// </summary>
public sealed record HangarPreviewPose(
    int Airframe, int LeftHardpoints, int RightHardpoints, int Pattern,
    PaintColour Colour1, PaintColour Colour2, PaintColour Colour3,
    int NoseDecal, int TailDecal, int WingDecal)
{
    /// <summary>The pose of <paramref name="build"/> drawn on <paramref name="airframe"/>, which the
    /// airframe list's focused row can differ from the build's own.</summary>
    public static HangarPreviewPose Of(CustomPlaneDef build, int airframe) => new(
        airframe, build.LeftHardpoints, build.RightHardpoints, build.PaintPattern,
        build.Colour1, build.Colour2, build.Colour3, build.NoseDecal, build.TailDecal, build.WingDecal);

    /// <summary>The livery the flown plane would wear, <see cref="CustomPlaneBuild.PaintFor(CustomPlaneDef, string)"/>'s.</summary>
    public PaintScheme Paint() => CustomPlaneBuild.PaintFor(
        CustomPlaneBuild.PatternName(Pattern), Colour1, Colour2, Colour3, NoseDecal, TailDecal, WingDecal);

    /// <summary>Whether <paramref name="other"/> wears the same paint, whatever its airframe and stores.</summary>
    public bool SamePaint(HangarPreviewPose other) =>
        this with { Airframe = other.Airframe, LeftHardpoints = other.LeftHardpoints, RightHardpoints = other.RightHardpoints } == other;

    /// <summary>Where the camera looks after <paramref name="before"/> became <paramref name="after"/>:
    /// the wing whose hardpoint count moved, else the area whose decal moved. A pattern or colour
    /// edit, an airframe change and the first pose ask for none.</summary>
    public static HangarPreviewFocus FocusFor(HangarPreviewPose? before, HangarPreviewPose after)
    {
        if (before == null || before.Airframe != after.Airframe)
        {
            return HangarPreviewFocus.None;
        }

        return before.LeftHardpoints != after.LeftHardpoints ? HangarPreviewFocus.LeftWing
            : before.RightHardpoints != after.RightHardpoints ? HangarPreviewFocus.RightWing
            : before.NoseDecal != after.NoseDecal ? HangarPreviewFocus.Nose
            : before.TailDecal != after.TailDecal ? HangarPreviewFocus.Tail
            : before.WingDecal != after.WingDecal ? HangarPreviewFocus.Wing
            : HangarPreviewFocus.None;
    }
}
