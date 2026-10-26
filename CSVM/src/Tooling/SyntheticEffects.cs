using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using CSVM.Extraction;
using CSVM.Mech3;
using Godot;

namespace CSVM.Tooling;

/// <summary>
/// The synthetic tree's anim and effect records, the <c>effects</c> family. Under
/// <see cref="Folder"/> it writes a compiled anim archive in the extraction's shape, split out of
/// <c>cam_anim.json</c>. Under <c>zrdr/</c> it copies the reader-form destructibles and the two
/// puffer readers. The template roots the archive stages are <see cref="Gamez"/>, built in memory.
/// No session loader reads either; a suite finds them through <see cref="Gamez"/> and
/// <see cref="AnimUnder"/>. Engine-free.
/// </summary>
public static class SyntheticEffects
{
    /// <summary>The family's own folder under <c>extracted/</c>, a name no extraction writes.</summary>
    public const string Folder = "probe_effects";

    private const string Records = "synthetic/";

    private const string Anims = Records + Folder + "/cam_anim.json";

    private static readonly (string Fixture, string Target)[] ReaderRecords =
    {
        (Records + "zrdr/probe_world.json", "zrdr/probe_world.json"),
        (Records + "zrdr/flame_ball.json", "zrdr/flame_ball.json"),
        (Records + "zrdr/pufftrails.json", "zrdr/pufftrails.json"),
    };

    /// <summary>The compiled anim archive under <paramref name="dataRoot"/>, an unpacked
    /// <c>cam_anim</c> folder.</summary>
    public static string AnimUnder(string dataRoot) =>
        Path.Combine(dataRoot, ExtractionRun.ExtractedFolder, Folder, "cam_anim");

    /// <summary>Writes the reader records and the compiled archive.</summary>
    public static void Write(SyntheticTree tree)
    {
        foreach (var (fixture, target) in ReaderRecords)
        {
            tree.CopyFixture(fixture, target);
        }

        WriteAnims(tree);
    }

    /// <summary>The template gamez, built in memory: one node per template, depth first, children by
    /// flat index. Each node that names a box gets its mesh in the order met.</summary>
    public static GameZ Gamez()
    {
        var gz = new GameZ();
        foreach (var (r, g, b) in new[] { (230, 150, 60), (90, 90, 96), (200, 210, 230), (120, 96, 70), (40, 36, 32) })
        {
            gz.Materials.Add(SyntheticPlane.Colored(r, g, b));
        }

        GameZNode Add(GameZNode? parent, string name, Vector3 at = default, float[]? box = null, int material = 0)
        {
            var node = SyntheticPlane.AddNode(gz, "Object3d", name, -1, at);
            parent?.Children.Add(node.Index);
            if (box != null)
            {
                node.MeshIndex = SyntheticPlane.AddBox(gz, box[..3], box[3..], material);
            }

            return node;
        }

        float[] small = { -0.3f, -0.3f, -0.3f, 0.3f, 0.3f, 0.3f };
        Add(Add(null, "he_ring"), "he_ringer", box: new[] { -3.5f, 0.0f, -3.5f, 3.5f, 0.25f, 3.5f });
        Add(Add(null, "he_ring1"), "he_ringer1", box: new[] { -2.5f, -2.5f, -0.2f, 2.5f, 2.5f, 0.2f });
        var trails = Add(null, "he_trails");
        Add(trails, "fly_trail1", new Vector3(1.0f, 0.5f, 0.0f), small, 1);
        Add(trails, "fly_trail2", new Vector3(-1.0f, 0.5f, 0.0f), small, 1);
        Add(trails, "fly_trail3", new Vector3(0.0f, 0.5f, 1.0f), small, 1);
        Add(trails, "fly_trail4", new Vector3(0.0f, 0.5f, -1.0f), small, 1);
        Add(trails, "fly_trail5", new Vector3(0.0f, 1.2f, 0.0f), small, 1);
        Add(Add(null, "ballflare.flt"), "flare_disc", box: new[] { -0.5f, -0.5f, -0.05f, 0.5f, 0.5f, 0.05f }, material: 2);
        Add(Add(null, "dum_gunhit"), "gunhit_chunk", box: new[] { -0.4f, 0.0f, -0.4f, 0.4f, 0.6f, 0.4f }, material: 1);
        var flakes = Add(null, "planeflakes");
        Add(flakes, "flake1", new Vector3(0.3f, 0.0f, 0.0f), new[] { -0.15f, -0.02f, -0.1f, 0.15f, 0.02f, 0.1f }, 1);
        Add(flakes, "flake2", new Vector3(-0.3f, 0.1f, 0.0f), new[] { -0.12f, -0.02f, -0.12f, 0.12f, 0.02f, 0.12f }, 1);
        Add(flakes, "flake3", new Vector3(0.0f, -0.1f, 0.2f), new[] { -0.1f, -0.02f, -0.15f, 0.1f, 0.02f, 0.15f }, 1);
        Add(Add(null, "sonic_effect"), "sonic_emit1", new Vector3(0.0f, 0.5f, 0.0f));
        Add(Add(null, "sonic_ring1"), "sonic_ring", box: new[] { -2.0f, 0.0f, -2.0f, 2.0f, 0.15f, 2.0f }, material: 2);
        Add(Add(null, "sonic_ring2"), "sonic_ring", box: new[] { -1.5f, 0.0f, -1.5f, 1.5f, 0.15f, 1.5f }, material: 2);
        var torpedo = Add(null, "probe_torpbody");
        Add(torpedo, "atbody", box: new[] { -0.3f, -0.3f, -1.6f, 0.3f, 0.3f, 1.6f }, material: 1);
        Add(torpedo, "rightwing", new Vector3(0.6f, 0.0f, 0.4f), new[] { -0.3f, -0.03f, -0.2f, 0.3f, 0.03f, 0.2f }, 1);
        Add(torpedo, "leftwing", new Vector3(-0.6f, 0.0f, 0.4f), new[] { -0.3f, -0.03f, -0.2f, 0.3f, 0.03f, 0.2f }, 1);
        Add(torpedo, "atprop", new Vector3(0.0f, 0.0f, 1.7f), new[] { -0.35f, -0.35f, -0.02f, 0.35f, 0.35f, 0.02f }, 4);
        var splash = Add(null, "huge_splash_model");
        Add(splash, "splash_polys", box: new[] { -3.0f, 0.0f, -3.0f, 3.0f, 2.0f, 3.0f }, material: 2);
        Add(splash, "sp_1", new Vector3(0.0f, 0.5f, 0.0f));
        Add(splash, "ripple1", box: new[] { -4.0f, 0.0f, -4.0f, 4.0f, 0.05f, 4.0f }, material: 2);
        Add(null, "m_bld_healthy", box: new[] { -5.0f, 0.0f, -5.0f, 5.0f, 6.0f, 5.0f }, material: 3);
        Add(null, "carnage_trails");
        Add(null, "large_fire");
        Add(null, "white_water_impact");
        var shed = Add(null, "probe_shed", new Vector3(40.0f, 0.0f, -30.0f));
        Add(shed, "healthy", box: new[] { -4.0f, 0.0f, -3.0f, 4.0f, 5.0f, 3.0f }, material: 3);
        Add(shed, "destroyed", box: new[] { -4.5f, 0.0f, -3.5f, 4.5f, 1.2f, 3.5f }, material: 4);
        var tank = Add(null, "probe_tank", new Vector3(-40.0f, 0.0f, 30.0f));
        Add(tank, "healthy", box: new[] { -2.5f, 0.0f, -2.5f, 2.5f, 7.0f, 2.5f }, material: 3);
        Add(tank, "destroyed", box: new[] { -3.0f, 0.0f, -3.0f, 3.0f, 1.0f, 3.0f }, material: 4);
        return gz;
    }

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
