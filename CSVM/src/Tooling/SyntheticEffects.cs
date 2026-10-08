using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using CSVM.Extraction;

namespace CSVM.Tooling;

/// <summary>
/// The synthetic tree's anim and effect records, the <c>effects</c> family. Under
/// <see cref="Folder"/> it writes a template gamez, one box per meshed node of
/// <c>templates.json</c>. Beside it sits a compiled anim archive in the extraction's shape, split
/// out of <c>cam_anim.json</c>. Under <c>zrdr/</c> it copies the reader-form destructibles and the
/// two puffer readers. No session
/// loader reads <see cref="Folder"/>; a suite finds it through <see cref="GamezUnder"/> and
/// <see cref="AnimUnder"/>. Engine-free.
/// </summary>
public static class SyntheticEffects
{
    /// <summary>The family's own folder under <c>extracted/</c>, a name no extraction writes.</summary>
    public const string Folder = "probe_effects";

    private const string Records = "synthetic/";

    private const string Templates = Records + Folder + "/templates.json";

    private const string Anims = Records + Folder + "/cam_anim.json";

    private static readonly (string Fixture, string Target)[] ReaderRecords =
    {
        (Records + "zrdr/probe_world.json", "zrdr/probe_world.json"),
        (Records + "zrdr/flame_ball.json", "zrdr/flame_ball.json"),
        (Records + "zrdr/pufftrails.json", "zrdr/pufftrails.json"),
    };

    /// <summary>The template gamez under <paramref name="dataRoot"/>, an unpacked gamez folder.</summary>
    public static string GamezUnder(string dataRoot) =>
        Path.Combine(dataRoot, ExtractionRun.ExtractedFolder, Folder, "gamez");

    /// <summary>The compiled anim archive under <paramref name="dataRoot"/>, an unpacked
    /// <c>cam_anim</c> folder.</summary>
    public static string AnimUnder(string dataRoot) =>
        Path.Combine(dataRoot, ExtractionRun.ExtractedFolder, Folder, "cam_anim");

    /// <summary>Writes the reader records, the template gamez and the compiled archive.</summary>
    public static void Write(SyntheticTree tree)
    {
        foreach (var (fixture, target) in ReaderRecords)
        {
            tree.CopyFixture(fixture, target);
        }

        WriteGamez(tree);
        WriteAnims(tree);
    }

    // One legacy-shape node per entry, depth first, children by flat index. Each entry with a
    // `box` takes the next mesh index, so models.json lists the boxes in the order met.
    private static void WriteGamez(SyntheticTree tree)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(tree.Fixture(Templates)));
        var nodes = new List<JsonObject>();
        var boxes = new List<(float[] Lo, float[] Hi, int Material)>();
        foreach (var root in doc.RootElement.GetProperty("nodes").EnumerateArray())
        {
            Flatten(root, nodes, boxes);
        }

        var nodeArray = new JsonArray();
        foreach (var node in nodes)
        {
            nodeArray.Add(new JsonObject { ["Object3d"] = node });
        }

        string folder = Folder + "/gamez/";
        tree.WriteBytes(folder + "nodes.json", JsonSerializer.SerializeToUtf8Bytes(nodeArray));
        tree.WriteBytes(folder + "models.json", SyntheticPlane.Models(boxes));
        tree.WriteBytes(folder + "materials.json",
            JsonSerializer.SerializeToUtf8Bytes(doc.RootElement.GetProperty("materials")));
    }

    private static int Flatten(JsonElement entry, List<JsonObject> nodes,
        List<(float[] Lo, float[] Hi, int Material)> boxes)
    {
        int index = nodes.Count;
        var node = new JsonObject { ["name"] = entry.GetProperty("name").GetString() };
        nodes.Add(node);
        int mesh = -1;
        if (entry.TryGetProperty("box", out var box))
        {
            mesh = boxes.Count;
            boxes.Add((Vec3(box[0]), Vec3(box[1]), entry.GetProperty("material").GetInt32()));
        }

        var children = new JsonArray();
        if (entry.TryGetProperty("children", out var kids))
        {
            foreach (var kid in kids.EnumerateArray())
            {
                children.Add(Flatten(kid, nodes, boxes));
            }
        }

        var at = entry.TryGetProperty("at", out var a) ? Vec3(a) : new[] { 0f, 0f, 0f };
        node["mesh_index"] = mesh;
        node["children"] = children;
        node["transformation"] = new JsonObject
        {
            ["translation"] = new JsonObject { ["x"] = at[0], ["y"] = at[1], ["z"] = at[2] },
            ["rotation"] = new JsonObject { ["x"] = 0f, ["y"] = 0f, ["z"] = 0f },
        };
        return index;
    }

    private static float[] Vec3(JsonElement e) =>
        e.GetArrayLength() == 3
            ? new[] { e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle() }
            : throw new InvalidDataException(
                $"a template vector needs three numbers, has {e.GetArrayLength().ToString(CultureInfo.InvariantCulture)}");

    // The extraction's archive shape: metadata.json names every definition file, in order.
    private static void WriteAnims(SyntheticTree tree)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(tree.Fixture(Anims)));
        var names = new JsonArray();
        string folder = Folder + "/cam_anim/";
        foreach (var entry in doc.RootElement.GetProperty("defs").EnumerateArray())
        {
            string file = entry.GetProperty("file").GetString()
                ?? throw new InvalidDataException("an anim definition entry names no file");
            names.Add(file);
            tree.WriteBytes(folder + file + ".json", JsonSerializer.SerializeToUtf8Bytes(entry.GetProperty("def")));
        }

        var metadata = new JsonObject { ["anim_def_names"] = names, ["script_names"] = new JsonArray() };
        tree.WriteBytes(folder + "metadata.json", JsonSerializer.SerializeToUtf8Bytes(metadata));
    }
}
