using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CSVM.Extraction;
using CSVM.Mech3;
using CSVM.Tooling;
using CSVM.Utils;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <see cref="DumpGameFiles"/>, the <c>--dump</c> writer. The CI-safe units cover its file list,
/// its refusals and the JSON shape of the models' values. The install test dumps a small set of
/// archives and reads every kind back against the readers.
/// </summary>
public class DumpGameFilesTests
{
    [Fact]
    public void TheFileListIsTheManifestPlusTheMessageTableAndAChapterNarrowsIt()
    {
        Assert.Equal(ZbdManifest.Expected.Count + 1, DumpGameFiles.Files(null).Count);

        var c1b = DumpGameFiles.Files("c1b");
        Assert.Contains("strings.dll", c1b);
        Assert.Contains("ZBD/zrdr.zbd", c1b);
        Assert.Contains("ZBD/C1B/M03/mis_anim.zbd", c1b);
        Assert.DoesNotContain(c1b, f => f.StartsWith("ZBD/C1/", StringComparison.Ordinal));
        Assert.Equal(1 + 6 + 9 + 8, c1b.Count);
    }

    [Fact]
    public void WithoutGameFilesItReportsThatAndWritesNothing()
    {
        string dir = Path.Combine(Path.GetTempPath(), "csvm-dump-" + Guid.NewGuid().ToString("N"));
        var lines = new List<string>();
        bool ok;
        using (Log.PushConsoleSink(lines.Add))
        {
            ok = DumpGameFiles.Run(null, dir, null);
        }

        Assert.False(ok);
        Assert.Contains(lines, l => l.Contains("no game files", StringComparison.Ordinal));
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public void AChapterNoArchiveBelongsToIsRefused()
    {
        var lines = new List<string>();
        using (Log.PushConsoleSink(lines.Add))
        {
            Assert.False(DumpGameFiles.Run(null, Path.GetTempPath(), "C9"));
        }

        Assert.Contains(lines, l => l.Contains("chapter 'C9'", StringComparison.Ordinal));
    }

    [Fact]
    public void ModelValuesWriteAsReadableJson()
    {
        using (var vector = JsonDocument.Parse(DumpGameFiles.ToJson(new Vector3(1, 2, 3))))
        {
            Assert.Equal(new[] { 1f, 2f, 3f }, vector.RootElement.EnumerateArray().Select(e => e.GetSingle()));
        }

        using (var transform = JsonDocument.Parse(DumpGameFiles.ToJson(new Transform3D(Basis.Identity, new Vector3(4, 5, 6)))))
        {
            Assert.Equal(new[] { 1f, 0, 0, 0, 1, 0, 0, 0, 1, 4, 5, 6 }, transform.RootElement.EnumerateArray().Select(e => e.GetSingle()));
        }

        Assert.Equal("\"NaN\"", DumpGameFiles.ToJson(float.NaN));

        var bag = new AnimData(new Dictionary<string, object?> { ["name"] = "door", ["at"] = new List<object?> { 1f, null } });
        using (var doc = JsonDocument.Parse(DumpGameFiles.ToJson(bag)))
        {
            Assert.Equal("door", doc.RootElement.GetProperty("name").GetString());
            Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("at")[1].ValueKind);
        }

        // The definition's back reference to its archive is a cycle the dump leaves out.
        var def = new AnimDefinition { Name = "door" };
        AnimArchive.FromDecoded("C1/cam_anim", new List<AnimDefinition> { def }, new List<(string, SiScript)>());
        using (var doc = JsonDocument.Parse(DumpGameFiles.ToJson(def)))
        {
            Assert.Equal("door", doc.RootElement.GetProperty("Name").GetString());
            Assert.False(doc.RootElement.TryGetProperty("Archive", out _));
        }

        var table = DumpGameFiles.Table(Messages.Of(new[] { ("MSG_B", "b"), ("MSG_A", "a") }));
        Assert.Equal(new[] { "MSG_A", "MSG_B" }, table.Keys);
    }

    [GameFilesFact]
    public void ASmallSetDumpsAndReadsBack()
    {
        var game = GameFilesData.Source!;
        string dir = Path.Combine(Path.GetTempPath(), "csvm-dump-" + Guid.NewGuid().ToString("N"));
        try
        {
            var files = new[]
            {
                "strings.dll", "ZBD/interp.zbd", "ZBD/zrdr.zbd", "ZBD/soundsl.zbd", "ZBD/C1B/gamez.zbd",
                "ZBD/C1B/cam_anim.zbd", "ZBD/C1B/M03/mis_anim.zbd", "ZBD/C1B/M03/zrdr.zbd", "ZBD/C1B/rtexture2.zbd",
            };
            Assert.True(DumpGameFiles.Write(game, dir, files));

            using (var messages = Json(dir, "messages.json"))
            {
                Assert.Equal(Messages.Load(game).Count, messages.RootElement.EnumerateObject().Count());
            }

            using (var interp = Json(dir, "interp.json"))
            {
                Assert.Equal(InterpScripts.Load(game)!.Scripts.Count, interp.RootElement.GetArrayLength());
            }

            var root = ZrdrZbd.Load(game, "ZBD/zrdr.zbd")!;
            Assert.Equal(root.Count, Directory.GetFiles(Path.Combine(dir, "zrdr"), "*.json").Length);
            using (var vehicle = Json(dir, "zrdr", "vehicle.zrd.json"))
            {
                Assert.Equal(ZrdrZbd.Find(root, "vehicle.zrd")!.Root.Count, vehicle.RootElement.GetArrayLength());
            }

            var gamez = GameZZbd.Load(game, "ZBD/C1B/gamez.zbd")!;
            using (var nodes = Json(dir, "C1B", "gamez", "nodes.json"))
            {
                Assert.Equal(gamez.Nodes.Count, nodes.RootElement.GetArrayLength());
                Assert.Equal(gamez.Nodes[0].Name, nodes.RootElement[0].GetProperty("Name").GetString());
            }

            using (var meshes = Json(dir, "C1B", "gamez", "meshes.json"))
            {
                Assert.Equal(gamez.Meshes.Count, meshes.RootElement.GetArrayLength());
            }

            foreach (string anim in new[] { "C1B/cam_anim", "C1B/M03/mis_anim" })
            {
                var archive = AnimZbd.Load(game, "ZBD/" + anim + ".zbd", anim)!;
                using var defs = Json(dir, anim, "definitions.json");
                using var scripts = Json(dir, anim, "scripts.json");
                Assert.Equal(archive.Defs.Count, defs.RootElement.GetArrayLength());
                Assert.Equal(archive.ScriptCount, scripts.RootElement.GetArrayLength());
            }

            var textures = TextureZbd.Load(game, "ZBD/C1B/rtexture2.zbd")!;
            using (var manifest = Json(dir, "C1B", "rtexture2", "manifest.json"))
            {
                Assert.Equal(textures.Count, manifest.RootElement.GetArrayLength());
            }

            foreach (var texture in new[] { textures[0], textures.FirstOrDefault(t => t.HasAlphaChannel) ?? textures[^1] })
            {
                var png = PngImage.Decode(File.ReadAllBytes(Path.Combine(dir, "C1B", "rtexture2", texture.Key + ".png")))!;
                Assert.Equal((texture.Width, texture.Height), (png.Width, png.Height));
                int channels = texture.HasAlphaChannel ? 4 : 3;
                for (int px = 0; px < texture.Width * texture.Height; px++)
                {
                    for (int c = 0; c < channels; c++)
                    {
                        Assert.Equal(texture.Pixels[(px * channels) + c], png.Rgba[(px * 4) + c]);
                    }
                }
            }

            using var sounds = SoundZbd.Load(game, "ZBD/soundsl.zbd")!;
            var first = sounds.Sounds[0];
            Assert.Equal(sounds.ReadWavBytes(first), File.ReadAllBytes(Path.Combine(dir, "soundsl", first.Key)));
            Assert.All(sounds.Sounds, s => Assert.True(File.Exists(Path.Combine(dir, "soundsl", s.Key)), s.Key));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    private static JsonDocument Json(params string[] path) =>
        JsonDocument.Parse(File.ReadAllBytes(Path.Combine(path)), new JsonDocumentOptions());
}
