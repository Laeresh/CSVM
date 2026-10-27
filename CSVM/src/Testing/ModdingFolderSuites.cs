using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using CSVM.Launch;
using CSVM.Mech3;
using CSVM.Spec;
using CSVM.Tooling;
using Godot;

namespace CSVM.Testing;

/// <summary>A launch naming a modding folder (<see cref="ModdingFolder"/>), as the launcher makes
/// one: <see cref="BasePaths.Resolve"/> over a spec carrying <c>--mod-folder=</c>, then a whole
/// flight session. The folder is scratch, holding an edited copy of one chapter's gamez output out
/// of the data root's extraction. Units pin each format's slot (<c>ModdingFolderTests</c>).</summary>
internal static class ModdingFolderSuites
{
    private const string Chapter = "C1";

    // A static C1 node no reader, anim or boot script names, so the rename moves nothing else.
    private const string Shipped = "battlement3";
    private const string Edited = "csvmmodbattlement3";

    [Suite("modding-folder",
        "a flight launched with --mod-folder= naming a folder that holds only an edited copy of C1's gamez "
        + "output builds C1 from that copy, its renamed node standing in the session's world, while the "
        + "planes and the readers come from the game files")]
    internal static void ModdingFolderLaunch(TestContext ctx)
    {
        string extracted = Path.Combine(ctx.DataRoot, "extracted", Chapter, "gamez.zip");
        if (ctx.Game == null || !File.Exists(extracted))
        {
            throw new SuiteSkippedException($"needs the game files and an extraction's {Chapter}/gamez.zip under {ctx.DataRoot}");
        }

        string folder = Directory.CreateDirectory(Path.Combine(ctx.ScratchDir, "modding-folder-" + Guid.NewGuid().ToString("N"))).FullName;
        GameSession? session = null;
        SubViewport? pane = null;
        try
        {
            string zip = Path.Combine(Directory.CreateDirectory(Path.Combine(folder, Chapter)).FullName, "gamez.zip");
            File.Copy(extracted, zip);
            Rename(zip);

            var spec = SessionSpec.Parse(new[] { $"--chapter={Chapter}", "--players=1", "--mute", "--no-pads", "--det", "--mod-folder=" + folder });
            var paths = BasePaths.Resolve(ctx.RepoRoot, ctx.DataRoot, spec, SyntheticGameData.Build);
            ctx.Check(paths.Data.ModFolder == Path.GetFullPath(folder), $"the launch's game data names the folder ({paths.Data.ModFolder})");

            (session, pane) = Open(ctx, spec, paths);
            ctx.Check(session.StartSession(), $"the session builds");
            var world = paths.Data.LoadChapter(Chapter, spec.Mission).Gamez;
            ctx.Check(world?.FindByName(Edited) != null && world.FindByName(Shipped) == null,
                $"the chapter world it built is the folder's copy, {Shipped} renamed {Edited}");
            ctx.Check(Find(session, Edited), $"and the session's world holds a node named {Edited}");
            ctx.Check(paths.Data.Planes != null && paths.Data.Zrdr.Archive == GameData.RootZrdrPath
                && paths.Data.LoadChapter(Chapter, spec.Mission).MissionZrdr.Archive.StartsWith("ZBD/", StringComparison.Ordinal),
                $"while the planes and the readers are the game files' ({paths.Data.Zrdr.Archive})");
        }
        finally
        {
            session?.Free();
            pane?.Free();
            Directory.Delete(folder, recursive: true);
        }
    }

    // The node's name in nodes.json, its one spelling there.
    private static void Rename(string zip)
    {
        using var archive = ZipFile.Open(zip, ZipArchiveMode.Update);
        var entry = archive.GetEntry("nodes.json") ?? throw new FileNotFoundException("nodes.json", zip);
        string text;
        using (var reader = new StreamReader(entry.Open(), Encoding.UTF8))
        {
            text = reader.ReadToEnd();
        }

        entry.Delete();
        using var writer = new StreamWriter(archive.CreateEntry("nodes.json").Open(), new UTF8Encoding(false));
        writer.Write(text.Replace($"\"name\": \"{Shipped}\"", $"\"name\": \"{Edited}\"", StringComparison.Ordinal));
    }

    private static bool Find(Node node, string name)
    {
        if (node.Name == name)
        {
            return true;
        }

        foreach (var child in node.GetChildren())
        {
            if (Find(child, name))
            {
                return true;
            }
        }

        return false;
    }

    // One flight session in its own pane, given its context as the launcher gives one. Its decode
    // cache is its own, so no chapter world the run already decoded stands in for the folder's.
    private static (GameSession Session, SubViewport Pane) Open(TestContext ctx, SessionSpec spec, BasePaths paths)
    {
        var pane = new SubViewport
        {
            Size = new Vector2I(320, 240),
            OwnWorld3D = true,
            World3D = new World3D(),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
        };
        var camera = new Camera3D { Fov = 60f, Far = 20000f };
        var sun = new DirectionalLight3D { ShadowEnabled = false };
        pane.AddChild(camera);
        pane.AddChild(sun);
        ctx.Host.AddChild(pane);
        var session = new GameSession(spec, new LauncherContext
        {
            Decode = new DecodeCache(),
            Paths = paths,
            ProbeRunner = new ProbeRunner(paths),
            CaptureDirector = new CaptureDirector(spec),
            MasterSeed = 1,
            Camera = camera,
            Orbit = new Flight.Camera.OrbitCamera(camera),
            Sun = sun,
            Env = new Godot.Environment(),
            MenuDriven = false,
            MenuPads = null,
            ExitSession = () => { },
            RestartSession = () => { },
        });
        pane.AddChild(session);
        return (session, pane);
    }
}
