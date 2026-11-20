using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// The Enhanced-only work lamps inside C1's <c>hangar_3</c>, the hangar CM07's pilot takes the
/// Bloodhawk from (#371). The data authors no lamp there, and under global illumination SDFGI finds
/// no sky inside the roofed shell, so the interior drew nearly black. A grid of shadowed omnis
/// lights it. Their shadows keep the light off the ground outside the walls, since the floor is the
/// chapter's shared terrain tile. The faithful path draws the world unshaded, so the lamps are
/// hidden there.
/// </summary>
internal static class InteriorFill
{
    /// <summary>The hangar's world root in C1's gamez, the lamps' parent.</summary>
    public const string Hangar = "hangar_3";

    /// <summary>The hangar's interior model node, whose mesh places the lamps.</summary>
    public const string HangarInterior = "h3_interior";

    // TUNE: each lamp's energy and reach. Several overlap at any point, so one lamp is dim.
    private const float LampEnergy = 1.5f;
    private const float LampRangeM = 140f;

    // TUNE: the lamp grid, columns across the interior's width (x) by rows along its length (z),
    // hung at a fraction of the ceiling's height.
    private const int Columns = 2;
    private const int Rows = 3;
    private const float HeightFraction = 0.75f;

    // TUNE: past this camera distance the lamps fade out and leave the frame, shadow maps and all.
    // The light stays inside the walls, so nothing outside the hangar shows the fade.
    private const float FadeBeginM = 600f;
    private const float FadeLengthM = 100f;

    private static readonly Color LampColor = new(1.0f, 0.95f, 0.85f);

    /// <summary>Hangs the lamps in the built world when its gamez holds the hangar, or returns null.</summary>
    public static Node3D? Build(GameZ gamez, AnimRuntime runtime) =>
        gamez.FindByName(Hangar) is { } hangarNode
        && gamez.FindByName(HangarInterior) is { } interiorNode
        && runtime.FindNodeByIndex(hangarNode.Index) is { } hangar
        && runtime.FindNodeByIndex(interiorNode.Index) is { } interior
            ? Attach(hangar, interior) : null;

    /// <summary>Shows the lamps under Enhanced and hides them on the faithful path.</summary>
    public static void Follow(Node3D? lamps)
    {
        if (lamps != null && GodotObject.IsInstanceValid(lamps))
            lamps.Visible = GraphicsMode.Enhanced;
    }

    /// <summary>Hangs the lamps under <paramref name="hangar"/>, laid over <paramref name="interior"/>'s
    /// first mesh in that mesh's frame, or returns null when it carries none.</summary>
    private static Node3D? Attach(Node3D hangar, Node3D interior)
    {
        if (FirstMesh(interior) is not { } mesh)
            return null;
        // ⚠ Never parent the lamps under the mesh: its LOD node hides that branch, and the lamps with it.
        var meshInHangar = Transform3D.Identity;
        for (Node? n = mesh; n != null && n != hangar; n = n.GetParent())
        {
            if (n is Node3D n3d)
                meshInHangar = n3d.Transform * meshInHangar;
        }
        var box = mesh.GetAabb();
        var lamps = new Node3D
        {
            Name = "interior_lamps",
            Transform = meshInHangar,
            Visible = GraphicsMode.Enhanced,
        };
        for (int c = 0; c < Columns; c++)
        {
            for (int r = 0; r < Rows; r++)
            {
                lamps.AddChild(new OmniLight3D
                {
                    Position = box.Position + (box.Size * new Vector3((c + 0.5f) / Columns, HeightFraction, (r + 0.5f) / Rows)),
                    OmniRange = LampRangeM,
                    OmniAttenuation = WorldLights.OmniAttenuation,
                    LightEnergy = LampEnergy,
                    LightColor = LampColor,
                    ShadowEnabled = true,
                    DistanceFadeEnabled = true,
                    DistanceFadeBegin = FadeBeginM,
                    DistanceFadeLength = FadeLengthM,
                });
            }
        }
        hangar.AddChild(lamps);
        Log.Info("world", $"interior lamps: {interior.Name} {Columns * Rows} over {box.Size}");
        return lamps;
    }

    private static MeshInstance3D? FirstMesh(Node node)
    {
        if (node is MeshInstance3D { Mesh: not null } mi)
            return mi;
        int count = node.GetChildCount();
        for (int i = 0; i < count; i++)
        {
            if (FirstMesh(node.GetChild(i)) is { } hit)
                return hit;
        }
        return null;
    }
}
