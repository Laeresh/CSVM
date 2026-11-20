using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// The Enhanced-only work lamps inside C1's <c>hangar_3</c>, the hangar CM07's pilot takes the
/// Bloodhawk from (#371). The data authors no lamp there, and under global illumination SDFGI finds
/// no sky inside the roofed shell, so the interior drew nearly black. A grid of shadowed spots hangs
/// under the main hall's roof pointing down. Their light lands on the floor and never reaches the
/// roof gaps or the glass annex. The faithful path draws the world unshaded, so the lamps are
/// hidden there.
/// </summary>
internal static class InteriorFill
{
    /// <summary>The hangar's world root in C1's gamez, whose frame the lamps are placed in.</summary>
    public const string Hangar = "hangar_3";

    // The lamps hang this high in hangar_3's frame, under the main hall's roof (54 m at its edges).
    private const float HangM = 50f;

    // TUNE: each spot's energy, its cone's half-angle and its reach. The cone covers a grid cell's
    // corner at the floor and ends well inside the walls.
    private const float LampEnergy = 4.5f;
    private const float ConeDeg = 46f;
    private const float LampRangeM = 80f;

    // TUNE: past this camera distance the lamps fade out and leave the frame, shadow maps and all.
    // The shell draws out to 2 km, so the lamps last nearly as far.
    private const float FadeBeginM = 1900f;
    private const float FadeLengthM = 100f;

    // The grid in hangar_3's frame, read off the shell g306. Its main hall spans x -57 to 57 and
    // z -192 to 192; the side aisles run out to the walls at x +-128.
    private static readonly float[] ColumnsX = { -38f, 0f, 38f };
    private static readonly float[] RowsZ = { -144f, -48f, 48f, 144f };

    private static readonly Color LampColor = new(1.0f, 0.95f, 0.85f);

    /// <summary>Gets the number of lamps <see cref="Build"/> hangs.</summary>
    public static int LampCount => ColumnsX.Length * RowsZ.Length;

    /// <summary>Hangs the lamps in the built world when its gamez holds the hangar, or returns null.</summary>
    public static Node3D? Build(Mech3.GameZ gamez, Mech3.AnimRuntime runtime) =>
        gamez.FindByName(Hangar) is { } node && runtime.FindNodeByIndex(node.Index) is { } hangar
            ? Attach(hangar) : null;

    /// <summary>Shows the lamps under Enhanced and hides them on the faithful path.</summary>
    public static void Follow(Node3D? lamps)
    {
        if (lamps != null && GodotObject.IsInstanceValid(lamps))
            lamps.Visible = GraphicsMode.Enhanced;
    }

    private static Node3D Attach(Node3D hangar)
    {
        var lamps = new Node3D { Name = "interior_lamps", Visible = GraphicsMode.Enhanced };
        foreach (float x in ColumnsX)
        {
            foreach (float z in RowsZ)
            {
                lamps.AddChild(new SpotLight3D
                {
                    Position = new Vector3(x, HangM, z),
                    // A spot shines along its -Z; this turns that straight down.
                    RotationDegrees = new Vector3(-90f, 0f, 0f),
                    SpotRange = LampRangeM,
                    SpotAngle = ConeDeg,
                    SpotAttenuation = Mech3.WorldLights.OmniAttenuation,
                    LightEnergy = LampEnergy,
                    LightColor = LampColor,
                    ShadowEnabled = true,
                    // The floor faces the lamps; drawing back faces keeps it from shadowing itself.
                    ShadowReverseCullFace = true,
                    DistanceFadeEnabled = true,
                    DistanceFadeBegin = FadeBeginM,
                    // ⚠ Godot fades the shadow at its own distance, 50 m by default, and an
                    // unshadowed lamp lights the ground outside through the glass.
                    DistanceFadeShadow = FadeBeginM,
                    DistanceFadeLength = FadeLengthM,
                });
            }
        }
        hangar.AddChild(lamps);
        Log.Info("world", $"interior lamps: {hangar.Name} {LampCount} spots");
        return lamps;
    }
}
