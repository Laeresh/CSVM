using System;
using System.IO;
using System.Linq;
using CSVM.Mech3;
using CSVM.Tooling;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The synthetic tree's anim and effect records. The compiled archive loads through the engine's
/// reader, and every definition anchors on a template root the gamez carries. The reader
/// destructibles and the two puffer readers parse. Staging and playing them needs Godot, so the
/// in-engine suites cover that half.
/// </summary>
public class SyntheticEffectsTests
{
    [Fact]
    public void EveryCompiledDefinitionAnchorsOnATemplateRoot()
    {
        string root = Built();
        var gamez = GameZ.Load(SyntheticEffects.GamezUnder(root));
        var archive = AnimArchive.Load(SyntheticEffects.AnimUnder(root), SyntheticEffects.Folder);

        Assert.NotNull(archive);
        Assert.Contains(archive.Defs, d => d.AnimName == "he_ground_effect");
        var children = gamez.Nodes.SelectMany(n => n.Children).ToHashSet();
        foreach (var def in archive.Defs.Where(d => d.Name != "player"))
        {
            var node = gamez.FindByName(def.Name);
            Assert.True(node != null && !children.Contains(node.Index), $"{def.AnimName} anchors on {def.Name}");
        }
    }

    [Fact]
    public void EveryMeshedTemplateNodeHasItsBox()
    {
        var gamez = GameZ.Load(SyntheticEffects.GamezUnder(Built()));

        var meshed = gamez.Nodes.Where(n => n.MeshIndex >= 0).ToList();
        Assert.NotEmpty(meshed);
        Assert.Equal(meshed.Count, gamez.Meshes.Count);
        Assert.All(meshed, n => Assert.InRange(n.MeshIndex, 0, gamez.Meshes.Count - 1));
    }

    [Fact]
    public void TheReaderDestructiblesAndPuffersParse()
    {
        string zrdr = Path.Combine(Built(), "extracted", "zrdr");
        var destructibles = AnimDefs.LoadFileDefs(zrdr, "probe_world.json");

        Assert.Equal(2, destructibles.Count);
        Assert.All(destructibles, d => Assert.True(d.Destructible && d.PersistLog && d.ResetState != null, d.Name));
        Assert.NotNull(PufferState.Load(zrdr, "flame_ball.json", "fierypuffer"));
        Assert.NotNull(PufferState.Load(zrdr, "pufftrails.json", "smokepuffer"));
        Assert.NotNull(PufferState.Load(zrdr, "pufftrails.json", "firepuffer"));
    }

    [Fact]
    public void AnArchiveEntryNamingNoFileFailsTheBuild()
    {
        string fixtures = TestData.TempDir();
        string folder = Path.Combine(fixtures, "synthetic", SyntheticEffects.Folder);
        Directory.CreateDirectory(folder);
        File.Copy(TestData.Fixture("synthetic", SyntheticEffects.Folder, "templates.json"), Path.Combine(folder, "templates.json"));
        File.WriteAllText(Path.Combine(folder, "cam_anim.json"), "{ \"defs\": [ { \"def\": {} } ] }");
        string zrdr = Path.Combine(fixtures, "synthetic", "zrdr");
        Directory.CreateDirectory(zrdr);
        foreach (string name in new[] { "probe_world.json", "flame_ball.json", "pufftrails.json" })
        {
            File.Copy(TestData.Fixture("synthetic", "zrdr", name), Path.Combine(zrdr, name));
        }

        var tree = new SyntheticTree(fixtures, Path.Combine(TestData.TempDir(), "root"));
        Assert.ThrowsAny<Exception>(() => SyntheticEffects.Write(tree));
    }

    private static string Built()
    {
        string root = Path.Combine(TestData.TempDir(), "root");
        SyntheticData.Build(TestData.Fixture(), root, SyntheticShell.TreeFamilies);
        return root;
    }
}
