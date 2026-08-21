using System;

namespace CSVM.Flight;

/// <summary>One paint slot's colour as the 0-255 sRGB triple the saved record stores. Not a Godot
/// <c>Color</c> so the model stays engine-free; <c>PaintScheme.FromBytes</c> is the conversion
/// when a build reaches the painter.</summary>
public readonly record struct PaintColour(byte R, byte G, byte B);

/// <summary>One gun slot's pick: the calibre dropdown row (0-4, thirty through seventy cal) or
/// empty, plus the twin bit that doubles the mount. Null calibre is the record's id 5, the
/// explicit empty slot.</summary>
public readonly record struct GunChoice(int? Calibre, bool Twin)
{
    /// <summary>Whether the slot mounts nothing (the record's gun id 5).</summary>
    public bool IsEmpty => Calibre is null;
}

/// <summary>
/// A custom-built plane as CSVM models it: exactly the decoded 204-byte record's chosen fields
/// (docs/formats/paint.md "Saved custom planes"), minus everything the original derives at commit
/// (+0x98 empties, +0xa8 display cells, the +0x28/+0x3c totals), which are recomputed, never
/// stored. Engine-free by design: screens edit it, the economy prices it, and
/// <see cref="CustomPlaneStore"/> persists it as CSVM's own JSON; the 204-byte format itself is
/// import-only.
/// </summary>
public sealed class CustomPlaneDef
{
    /// <summary>Airframe ids run 0-10, the stat table's 11 rows.</summary>
    public const int MaxAirframe = 10;

    /// <summary>Engine ids run 0-6; 6 is the explicit no-engine pick the purchase gate rejects.</summary>
    public const int EngineNone = 6;

    /// <summary>Armour is bought in units 0-12 per zone; the original record stores them
    /// premultiplied by 5, our JSON stores the units themselves.</summary>
    public const int MaxArmourUnits = 12;

    /// <summary>Every airframe has exactly four gun slots (the disproof in PLAN-hangar's
    /// wrong-claims table; slot titles vary, the count does not).</summary>
    public const int GunSlots = 4;

    /// <summary>Gun calibre dropdown rows 0-4.</summary>
    public const int MaxCalibre = 4;

    /// <summary>Hardpoints are counted per wing, 0-4.</summary>
    public const int MaxHardpointsPerWing = 4;

    /// <summary>Paint pattern indices run 0-13.</summary>
    public const int MaxPaintPattern = 13;

    /// <summary>The plane's name. Also its identity in the store: the original saves
    /// <c>Planes\%s</c> and overwrites by name, and our store keeps that contract.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Airframe id, 0-10 (record +0x2c).</summary>
    public int Airframe { get; set; }

    /// <summary>Engine id, 0-6 with 6 = none (defaults to none, as an unstarted build has no
    /// engine to fly on).</summary>
    public int Engine { get; set; } = EngineNone;

    /// <summary>Nose armour units, 0-12 (record +0x74, stored there x5).</summary>
    public int ArmourNose { get; set; }

    /// <summary>Tail armour units, 0-12 (record +0x78).</summary>
    public int ArmourTail { get; set; }

    /// <summary>Left wing armour units, 0-12 (record +0x7c).</summary>
    public int ArmourLeftWing { get; set; }

    /// <summary>Right wing armour units, 0-12 (record +0x80).</summary>
    public int ArmourRightWing { get; set; }

    /// <summary>The four gun slots (record +0x84 twin bits, +0x88-0x94 ids). Fixed length
    /// <see cref="GunSlots"/>; entries default to empty.</summary>
    public GunChoice[] Guns { get; } = new GunChoice[GunSlots];

    /// <summary>Left wing hardpoint count, 0-4 (record +0x34).</summary>
    public int LeftHardpoints { get; set; }

    /// <summary>Right wing hardpoint count, 0-4 (record +0x38).</summary>
    public int RightHardpoints { get; set; }

    /// <summary>Paint pattern index, 0-13 (record +0x40; 13 remaps to 11 for the icon, a display
    /// rule that stays out of the model).</summary>
    public int PaintPattern { get; set; }

    /// <summary>First composite paint pick, stored as the record stores it, <c>a*5 + b</c>
    /// (+0x5c). Decomposing it is the paint screen's business, not the model's.</summary>
    public int PaintPick1 { get; set; }

    /// <summary>Second composite paint pick, <c>a*5 + b</c> (record +0x60).</summary>
    public int PaintPick2 { get; set; }

    /// <summary>The third pick dword (record +0x64). No screen handler writes it and its meaning
    /// is undecoded, so it is carried opaquely, never interpreted.</summary>
    public int PaintPick3 { get; set; }

    /// <summary>Paint slot 1, the identity/body colour (record +0x68).</summary>
    public PaintColour Colour1 { get; set; }

    /// <summary>Paint slot 2, the dark trim.</summary>
    public PaintColour Colour2 { get; set; }

    /// <summary>Paint slot 3, the light trim.</summary>
    public PaintColour Colour3 { get; set; }

    /// <summary>Forces every field into its decoded range, in place, and returns this. The store
    /// runs it on both load and save, so an out-of-range value from a hand-edited file (or a
    /// screen bug) can never leave the model claiming an airframe or calibre that does not
    /// exist.</summary>
    public CustomPlaneDef Clamp()
    {
        Airframe = Math.Clamp(Airframe, 0, MaxAirframe);
        Engine = Math.Clamp(Engine, 0, EngineNone);
        ArmourNose = Math.Clamp(ArmourNose, 0, MaxArmourUnits);
        ArmourTail = Math.Clamp(ArmourTail, 0, MaxArmourUnits);
        ArmourLeftWing = Math.Clamp(ArmourLeftWing, 0, MaxArmourUnits);
        ArmourRightWing = Math.Clamp(ArmourRightWing, 0, MaxArmourUnits);
        for (int i = 0; i < Guns.Length; i++)
        {
            if (Guns[i].Calibre is { } c)
            {
                Guns[i] = Guns[i] with { Calibre = Math.Clamp(c, 0, MaxCalibre) };
            }
        }

        LeftHardpoints = Math.Clamp(LeftHardpoints, 0, MaxHardpointsPerWing);
        RightHardpoints = Math.Clamp(RightHardpoints, 0, MaxHardpointsPerWing);
        PaintPattern = Math.Clamp(PaintPattern, 0, MaxPaintPattern);
        return this;
    }
}
