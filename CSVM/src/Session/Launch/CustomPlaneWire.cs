using CSVM.Flight.Hangar;
using CSVM.Net;

namespace CSVM.Session.Launch;

/// <summary>
/// The bridge between a saved custom plane and the build the wire carries. Each field that shapes
/// its flight, hits or looks crosses as the saved value, so every machine reads the same tables. The loadout picks stay behind: the seat's fit carries them.
/// </summary>
public static class CustomPlaneWire
{
    /// <summary>The build <paramref name="def"/> sends, or null for no plane.</summary>
    public static NetPlaneBuild? Build(CustomPlaneDef? def)
    {
        if (def == null)
        {
            return null;
        }

        var build = new NetPlaneBuild
        {
            Name = def.Name ?? string.Empty,
            Airframe = Byte(def.Airframe),
            Engine = Byte(def.Engine),
            LeftHardpoints = Byte(def.LeftHardpoints),
            RightHardpoints = Byte(def.RightHardpoints),
            PaintPattern = Byte(def.PaintPattern),
        };
        build.Armour[0] = Byte(def.ArmourNose);
        build.Armour[1] = Byte(def.ArmourTail);
        build.Armour[2] = Byte(def.ArmourLeftWing);
        build.Armour[3] = Byte(def.ArmourRightWing);
        for (int slot = 0; slot < CustomPlaneDef.GunSlots; slot++)
        {
            build.Guns[slot] = def.Guns[slot].Calibre is { } calibre ? Byte(calibre) : NetPlaneBuild.EmptyGun;
            build.Twins |= (byte)(def.Guns[slot].Twin ? 1 << slot : 0);
        }

        for (int slot = 0; slot < NetPlaneBuild.PaintSlots; slot++)
        {
            build.Colours[slot] = Byte(def.PaintColours[slot]);
            build.Shades[slot] = Byte(def.PaintShades[slot]);
        }

        build.Decals[0] = Decal(def.NoseDecal);
        build.Decals[1] = Decal(def.TailDecal);
        build.Decals[2] = Decal(def.WingDecal);
        return build;
    }

    /// <summary>The plane <paramref name="build"/> names, held to the decoded ranges, or null for a
    /// stock seat.</summary>
    public static CustomPlaneDef? Def(NetPlaneBuild? build)
    {
        if (build == null)
        {
            return null;
        }

        var def = new CustomPlaneDef
        {
            Name = build.Name,
            Airframe = build.Airframe,
            Engine = build.Engine,
            ArmourNose = build.Armour[0],
            ArmourTail = build.Armour[1],
            ArmourLeftWing = build.Armour[2],
            ArmourRightWing = build.Armour[3],
            LeftHardpoints = build.LeftHardpoints,
            RightHardpoints = build.RightHardpoints,
            PaintPattern = build.PaintPattern,
            NoseDecal = build.Decals[0],
            TailDecal = build.Decals[1],
            WingDecal = build.Decals[2],
        };
        for (int slot = 0; slot < CustomPlaneDef.GunSlots; slot++)
        {
            byte gun = build.Guns[slot];
            def.Guns[slot] = new GunChoice(gun == NetPlaneBuild.EmptyGun ? null : gun, (build.Twins & (1 << slot)) != 0);
        }

        for (int slot = 0; slot < NetPlaneBuild.PaintSlots; slot++)
        {
            def.PaintColours[slot] = build.Colours[slot];
            def.PaintShades[slot] = build.Shades[slot];
        }

        return def.Clamp();
    }

    private static byte Byte(int value) => (byte)System.Math.Clamp(value, 0, byte.MaxValue);

    private static sbyte Decal(int decal) =>
        decal is >= 0 and <= CustomPlaneDef.MaxDecal ? (sbyte)decal : NetPlaneBuild.KeepDecal;
}
