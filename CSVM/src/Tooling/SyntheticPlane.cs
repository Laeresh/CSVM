using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace CSVM.Tooling;

/// <summary>
/// The synthetic tree's stand-in aircraft, <see cref="Plane"/>, in two families. The plane family
/// is its model under <c>planes/</c>, a box-built airframe carrying the marker rig. It also holds
/// the <c>vehicle.json</c>, <c>engines.json</c> and <c>player.json</c> records and the maneuver
/// library. The armament family is
/// one gun and one rocket in <c>weapons.json</c>, the shake sources and the message table. Every
/// record is hand-authored from <c>docs/formats/</c>; only <c>models.json</c> is generated, from
/// the box list beside the nodes.
/// </summary>
public static class SyntheticPlane
{
    /// <summary>The stand-in's model root, and the <c>nodename</c> its player def names.</summary>
    public const string Plane = "probe_plane";

    // A record written for the tree keeps its path under extracted/ beneath this fixtures folder.
    private const string Records = "synthetic/";

    // models.json is generated from this list: one box mesh per entry, in mesh-index order.
    private const string BoxList = Records + "planes/boxes.json";

    // Each record as (fixture, path under extracted/). The maneuver library is the unit tests'
    // own, which already holds every step shape the AI pilot's reader takes.
    private static readonly (string Fixture, string Target)[] PlaneRecords =
    {
        (Records + "planes/nodes.json", "planes/nodes.json"),
        (Records + "planes/materials.json", "planes/materials.json"),
        (Records + "zrdr/vehicle.json", "zrdr/vehicle.json"),
        (Records + "zrdr/engines.json", "zrdr/engines.json"),
        (Records + "zrdr/player.json", "zrdr/player.json"),
        ("zrdr/maneuvers.json", "zrdr/maneuvers.json"),
    };

    private static readonly (string Fixture, string Target)[] ArmamentRecords =
    {
        (Records + "zrdr/weapons.json", "zrdr/weapons.json"),
        (Records + "zrdr/shakes.json", "zrdr/shakes.json"),
        (Records + "messages.json", "messages.json"),
    };

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

    /// <summary>The stand-in's stock fit, which a run reads in place as
    /// <c>StockLoadouts.Supplement</c>. A loadout is engine config, not extracted data, so it stays
    /// out of the tree.</summary>
    public static string LoadoutsUnder(string fixturesRoot) =>
        Path.Combine(fixturesRoot, "synthetic", "stock_loadouts.json");

    /// <summary>Writes the model under <c>planes/</c> and the plane records under <c>zrdr/</c>.
    /// It also writes the empty C1 chapter scope a flight is handed for its speed cue.</summary>
    public static void WritePlane(SyntheticTree tree)
    {
        foreach (var (fixture, target) in PlaneRecords)
        {
            tree.CopyFixture(fixture, target);
        }

        tree.WriteBytes("planes/models.json", Models(tree.Fixture(BoxList)));
        tree.Folder(SyntheticTextures.Chapter + "/zrdr");
    }

    /// <summary>Writes the weapon catalogue, the shake sources and the message table.</summary>
    public static void WriteArmament(SyntheticTree tree)
    {
        foreach (var (fixture, target) in ArmamentRecords)
        {
            tree.CopyFixture(fixture, target);
        }
    }

    /// <summary>The legacy-shape <c>models.json</c> for a box list. Each entry's <c>min</c> and
    /// <c>max</c> corners become one closed box of six quads. Every face takes the entry's
    /// <c>material</c> and a whole-texture UV square.</summary>
    public static byte[] Models(string boxListPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(boxListPath));
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            json.WriteStartArray();
            foreach (var box in doc.RootElement.EnumerateArray())
            {
                WriteBox(json, Corner(box, "min"), Corner(box, "max"), box.GetProperty("material").GetInt32());
            }

            json.WriteEndArray();
        }

        return stream.ToArray();
    }

    private static float[] Corner(JsonElement box, string key)
    {
        var values = new List<float>();
        foreach (var v in box.GetProperty(key).EnumerateArray())
        {
            values.Add(v.GetSingle());
        }

        return values.Count == 3
            ? values.ToArray()
            : throw new InvalidDataException($"box '{key}' needs three coordinates, has {values.Count.ToString(CultureInfo.InvariantCulture)}");
    }

    private static void WriteBox(Utf8JsonWriter json, float[] lo, float[] hi, int material)
    {
        json.WriteStartObject();
        json.WriteStartArray("vertices");
        for (int i = 0; i < 8; i++)
        {
            // Corners 0-3 run round the near (min z) face, 4-7 the far one in the same order.
            WriteVec(json, (i & 3) is 1 or 2 ? hi[0] : lo[0], (i & 3) >= 2 ? hi[1] : lo[1], i >= 4 ? hi[2] : lo[2]);
        }

        json.WriteEndArray();
        json.WriteStartArray("normals");
        foreach (var face in Faces)
        {
            WriteVec(json, face.X, face.Y, face.Z);
        }

        json.WriteEndArray();
        json.WriteStartArray("polygons");
        for (int f = 0; f < Faces.Length; f++)
        {
            json.WriteStartObject();
            WriteInts(json, "vertex_indices", Faces[f].Corners);
            WriteInts(json, "normal_indices", new[] { f, f, f, f });
            json.WriteStartObject("flags");
            json.WriteBoolean("unk2", false);
            json.WriteBoolean("triangle_strip", false);
            json.WriteEndObject();
            json.WriteNumber("unk04", 0);
            json.WriteStartArray("materials");
            json.WriteStartObject();
            json.WriteNumber("material_index", material);
            json.WriteStartArray("uv_coords");
            foreach (var (u, v) in new[] { (0f, 1f), (1f, 1f), (1f, 0f), (0f, 0f) })
            {
                json.WriteStartObject();
                json.WriteNumber("u", u);
                json.WriteNumber("v", v);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteEndArray();
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteStartArray("lights");
        json.WriteEndArray();
        json.WriteString("model_type", "Default");
        json.WriteEndObject();
    }

    private static void WriteVec(Utf8JsonWriter json, float x, float y, float z)
    {
        json.WriteStartObject();
        json.WriteNumber("x", x);
        json.WriteNumber("y", y);
        json.WriteNumber("z", z);
        json.WriteEndObject();
    }

    private static void WriteInts(Utf8JsonWriter json, string name, int[] values)
    {
        json.WriteStartArray(name);
        foreach (int v in values)
        {
            json.WriteNumberValue(v);
        }

        json.WriteEndArray();
    }
}
