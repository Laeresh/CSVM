using System.Collections.Generic;
using System.IO;
using CSVM.Mech3;
using Godot;

namespace CSVM.Tooling;

/// <summary>
/// The synthetic tree's stand-in aircraft, <see cref="Plane"/>, <see cref="Fighter"/>,
/// <see cref="Firebrand"/>, <see cref="Avenger"/> and six more under shipped airframe names the
/// suites fly. Their models are <see cref="Gamez"/>, box-built airframes each carrying the marker
/// rig, built in memory from <see cref="SyntheticPlaneRecords"/>. Their readers, the maneuver
/// library, the weapons, the turret table and the shake sources are <see cref="SyntheticZrdr"/>'s.
/// The weapon names are in <c>SyntheticMessages.cs</c>. Every record is hand-authored from
/// <c>docs/formats/</c>.
/// </summary>
public static class SyntheticPlane
{
    /// <summary>The stand-in's model root, and the <c>nodename</c> its player def names.</summary>
    public const string Plane = "probe_plane";

    /// <summary>The second stand-in's model root. ⚠ The name is the code's, not invented: the lobby
    /// door seats this starter airframe from <c>PlanePickerRoster.StockAirframes</c>. A suite that
    /// needs two distinct airframes finds it there. Everything under the name is invented.</summary>
    public const string Fighter = "player_pfighter";

    /// <summary>The custom-plane suites' host airframe (id 6), invented under the code's name like
    /// <see cref="Fighter"/>. A custom build composes the code's weapon ids (<c>wep_30</c>..<c>wep_70</c>,
    /// <c>wep_06</c>), so <c>weapons.json</c> carries invented records under those too.</summary>
    public const string Firebrand = "player_fbrand";

    /// <summary>The custom-plane suites' guest airframe (id 1), invented under the code's name.</summary>
    public const string Avenger = "player_avenger";


    // A box's six faces as indices into its eight corners, with each face's outward normal. Each
    // winds counter-clockwise seen from outside, so its Newell normal points out (docs/formats/gamez.md).
    private static readonly (int[] Corners, float X, float Y, float Z)[] Faces =
    {
        (new[] { 0, 3, 2, 1 }, 0f, 0f, -1f),
        (new[] { 4, 5, 6, 7 }, 0f, 0f, 1f),
        (new[] { 0, 4, 7, 3 }, -1f, 0f, 0f),
        (new[] { 1, 2, 6, 5 }, 1f, 0f, 0f),
        (new[] { 3, 7, 6, 2 }, 0f, 1f, 0f),
        (new[] { 0, 1, 5, 4 }, 0f, -1f, 0f),
    };

    /// <summary>The stand-ins' stock fits, which a run reads in place as
    /// <c>StockLoadouts.Supplement</c>. A loadout is engine config, not extracted data, so it stays
    /// out of the tree.</summary>
    public static string LoadoutsUnder(string fixturesRoot) =>
        Path.Combine(fixturesRoot, "synthetic", "stock_loadouts.json");


    /// <summary>The stand-ins' aircraft archive, built in memory from
    /// <see cref="SyntheticPlaneRecords"/>: what <c>planes.zbd</c> is to an install.</summary>
    public static GameZ Gamez()
    {
        var gz = new GameZ();
        gz.Materials.Add(new GameZMaterial { TextureName = "probe_skin" });
        gz.Materials.Add(Colored(150, 156, 166));
        gz.Materials.Add(Colored(40, 40, 44));
        gz.Materials.Add(new GameZMaterial { TextureName = "probe_panel" });
        foreach (var (r, g, b) in new[]
                 {
                     (170, 60, 40), (92, 108, 70), (52, 70, 128), (178, 152, 98), (150, 40, 46),
                     (206, 176, 60), (60, 132, 120), (120, 120, 150), (84, 70, 52), (110, 70, 136),
                 })
        {
            gz.Materials.Add(Colored(r, g, b));
        }

        foreach (var (lo, hi, material) in SyntheticPlaneRecords.Boxes)
        {
            AddBox(gz, lo, hi, material);
        }

        foreach (var row in SyntheticPlaneRecords.Nodes)
        {
            var node = AddNode(gz, row.Kind, row.Name, row.Mesh, new Vector3(row.X, row.Y, row.Z));
            node.Children.AddRange(row.Children);
        }

        return gz;
    }

    /// <summary>An opaque untextured material of the colour given in 0-255 channels.</summary>
    internal static GameZMaterial Colored(int r, int g, int b) => new() { Color = new Color(r / 255f, g / 255f, b / 255f, 1f) };

    /// <summary>Appends a node at the next flat index, translated by <paramref name="at"/>. A
    /// <c>Lod</c> node takes the nearest range, 0.</summary>
    internal static GameZNode AddNode(GameZ gz, string kind, string name, int mesh, Vector3 at)
    {
        var node = new GameZNode
        {
            Kind = kind,
            Name = name,
            MeshIndex = mesh,
            Index = gz.Nodes.Count,
            Local = new Transform3D(Basis.FromEuler(Vector3.Zero, EulerOrder.Yxz), at),
            LodRangeMin = kind == "Lod" ? 0f : -1f,
        };
        gz.Nodes.Add(node);
        return node;
    }

    /// <summary>Appends one closed box of six quads between corners <paramref name="lo"/> and
    /// <paramref name="hi"/>, and returns its mesh index. Every face takes
    /// <paramref name="material"/> and a whole-texture UV square.</summary>
    internal static int AddBox(GameZ gz, float[] lo, float[] hi, int material)
    {
        var mesh = new GameZMesh { ModelType = "Default" };
        for (int i = 0; i < 8; i++)
        {
            // Corners 0-3 run round the near (min z) face, 4-7 the far one in the same order.
            mesh.Vertices.Add(new Vector3((i & 3) is 1 or 2 ? hi[0] : lo[0], (i & 3) >= 2 ? hi[1] : lo[1], i >= 4 ? hi[2] : lo[2]));
        }

        for (int f = 0; f < Faces.Length; f++)
        {
            mesh.Normals.Add(new Vector3(Faces[f].X, Faces[f].Y, Faces[f].Z));
            var poly = new GameZPolygon
            {
                NormalIndices = new List<int> { f, f, f, f },
                MaterialIndex = material,
                UvCoords = new List<Vector2> { new(0f, 1f), new(1f, 1f), new(1f, 0f), new(0f, 0f) },
            };
            poly.VertexIndices.AddRange(Faces[f].Corners);
            mesh.Polygons.Add(poly);
        }

        gz.Meshes.Add(mesh);
        return gz.Meshes.Count - 1;
    }
}
