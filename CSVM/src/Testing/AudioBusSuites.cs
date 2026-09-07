using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.Utils;
using Godot;

namespace CSVM.Testing;

/// <summary>The mix guard: the four buses the project ships stand up, and every site that builds an
/// <see cref="AudioStreamPlayer"/> or <see cref="AudioStreamPlayer3D"/> puts it on a named child bus
/// rather than on Master. A player left on Master is silent about it, since Godot resolves an
/// unknown or unset bus name to Master with no error, so this is the only thing that can see the
/// omission. The suite builds each site itself and then walks the live scene tree, because a walk
/// alone would pass by seeing nothing.</summary>
internal static class AudioBusSuites
{
    // The whine slot no shipped airframe names, and a crash set an install may lack, make an exact
    // player count per site brittle. What every site must satisfy is "at least one, none on Master".
    private const int AtLeastOne = 1;

    // The dB an inaudible bus sits at, AudioMix's floor expressed as a volume. A repo run resolves
    // the developer volume to 0 and lands bus 0 here, which is what keeps a scripted run silent.
    private const float SilenceDb = -80f;

    // Slack for a dB comparison, well under the 6 dB a level step of that size moves a bus by.
    private const float DbSlack = 0.01f;

    [Suite("audio-buses",
        "the shipped bus layout, the mix written onto it, and the placement of every player: "
        + "Master carries Music, Effects and Voice as its children, each of the fourteen "
        + "player-construction sites (the music channel, the mission radio, the menu service's "
        + "narration and cue, FlightAudio's six, AiEngineAudio's loop factory, WorldSounds' emitter "
        + "and one-shot, and the projectile pool) builds its players on the bus its category names, "
        + "a walk of the whole live scene tree fails on any player left on Master, and the four "
        + "levels reach the three child buses at startup and on a live change while bus 0, which "
        + "carries the developer volume alone, is untouched by either")]
    internal static void AudioBusPlacement(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(ctx.SoundsPath, $"sound archive (soundsh)");
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        string texturesPath = SessionPaths.ChapterTextures(ctx.DataRoot, "C1");
        ctx.RequireData(texturesPath, $"C1 textures");

        var report = new StringBuilder();
        CheckLayout(ctx, report);
        CheckMix(ctx, report);

        var defs = SoundDefs.Load(ctx.ZrdrPath);
        var groups = SoundDefs.LoadGroups(ctx.ZrdrPath);
        var stats = PlaneStats.Load(ctx.ZrdrPath, ctx.PlaneName);
        string? gunLoop = WeaponDefs.Load(ctx.ZrdrPath, null).All
            .Select(w => w.LoopedSoundName)
            .FirstOrDefault(n => !string.IsNullOrEmpty(n));
        using var archive = new SoundArchive(ctx.SoundsPath);
        var textures = new TextureArchive(texturesPath);

        MusicPlayer? music = null;
        MissionRadio? radio = null;
        MenuAudioService? menu = null;
        FlightAudio? flight = null;
        AiEngineAudio? aiEngine = null;
        WorldSounds? world = null;
        ProjectilePool? pool = null;
        try
        {
            music = new MusicPlayer(defs, groups)
            {
                Name = "GuardMusic",
                Loader = (def, looped) => archive.Find(def.WavName, looped, warn: false),
            };
            ctx.Host.AddChild(music);

            radio = new MissionRadio(defs, groups, name => Stream(archive, defs, name))
            {
                Name = "GuardRadio",
            };
            ctx.Host.AddChild(radio);

            menu = new MenuAudioService(null, name => Stream(archive, defs, name))
            {
                Name = "GuardMenuAudio",
            };
            ctx.Host.AddChild(menu);

            flight = new FlightAudio { Name = "GuardFlightAudio" };
            flight.Setup(archive, defs, stats, groups);
            ctx.Host.AddChild(flight);
            if (gunLoop != null)
            {
                flight.StartGunLoop(gunLoop);
            }
            flight.StartNitroLoop();

            aiEngine = new AiEngineAudio { Name = "GuardAiEngineAudio" };
            ctx.Host.AddChild(aiEngine);
            aiEngine.Setup(archive, defs, stats);

            world = new WorldSounds(defs, groups)
            {
                Name = "GuardWorldSounds",
                Loader = (def, warn) => archive.Find(def.WavName, def.Looped, warn),
            };
            ctx.Host.AddChild(world);
            string? emitter = FirstEmitter(world, defs);
            int started = world.OneShotsStarted;
            string? oneShot = emitter == null
                ? null
                : world.PlayOneShot(emitter, Vector3.Zero, new Random(3));
            ctx.Check(emitter != null && oneShot != null && world.OneShotsStarted > started,
                $"an ambient emitter and a one-shot were both built name={emitter ?? "(none)"}");

            pool = new ProjectilePool(textures, archive, defs) { Name = "GuardProjectiles" };
            ctx.Host.AddChild(pool);

            CheckSite(ctx, music, "MusicPlayer._player", AudioBuses.Music, report);
            CheckSite(ctx, radio, "MissionRadio._player", AudioBuses.Voice, report);
            CheckSite(ctx, menu, "MenuAudioService narration + cue", AudioBuses.Effects,
                AudioBuses.Voice, report);
            CheckSite(ctx, flight, "FlightAudio's six", AudioBuses.Effects, report);
            CheckSite(ctx, aiEngine, "AiEngineAudio.MakeLoop", AudioBuses.Effects, report);
            CheckSite(ctx, world, "WorldSounds emitter + one-shot", AudioBuses.Effects, report);
            CheckSite(ctx, pool, "ProjectilePool's eight", AudioBuses.Effects, report);
            SweepTree(ctx, report);
        }
        finally
        {
            world?.FlushOneShots();
            pool?.Free();
            world?.Free();
            aiEngine?.Free();
            flight?.Free();
            menu?.Free();
            radio?.Free();
            music?.Stop();
            music?.Free();
            textures.Dispose();
        }

        ctx.WriteArtifact("test-audio-buses.txt", report.ToString());
    }

    // The layout half: the process runs four buses, and the three category buses send into Master,
    // so the developer gain and the focus mute still reach every sound.
    private static void CheckLayout(TestContext ctx, StringBuilder report)
    {
        int count = AudioServer.BusCount;
        report.AppendLine($"buses={count}");
        ctx.Same(4, count, $"the shipped layout stands up four buses");
        var expected = new[] { AudioBuses.Master, AudioBuses.Music, AudioBuses.Effects, AudioBuses.Voice };
        for (int i = 0; i < expected.Length && i < count; i++)
        {
            string name = AudioServer.GetBusName(i);
            string send = i == 0 ? "" : AudioServer.GetBusSend(i).ToString();
            report.AppendLine($"  bus {i} name={name} send={send} volume_db={AudioServer.GetBusVolumeDb(i):0.###}");
            ctx.Check(name == expected[i], $"bus {i} is named {expected[i]} actual={name}");
            ctx.Check(i == 0 || send == AudioBuses.Master,
                $"bus {i} ({name}) sends into Master actual={send}");
            if (i == 0)
            {
                // Bus 0 carries the developer volume and never a level, so it has no expected gain
                // to compare against; CheckMix is where it is held to being untouched.
                continue;
            }

            // A child bus carries the startup mix rather than a resting gain, and under --run-tests
            // that is the shipped defaults because OptionsStore reads an emptied scratch directory.
            ctx.Check(Math.Abs(AudioServer.GetBusVolumeDb(i) - StartupDb(name)) <= DbSlack,
                $"bus {i} ({name}) carries its shipped level db={AudioServer.GetBusVolumeDb(i):0.###} want={StartupDb(name):0.###}");
        }
    }

    // The gain the shipped defaults put a child bus at. Master is a multiplier over the other
    // three, so it is never a bus of its own and answers the resting gain here.
    private static float StartupDb(string bus) => bus switch
    {
        AudioBuses.Music => AudioMix.VolumeDb(AudioMix.DefaultMusic, AudioMix.DefaultMaster),
        AudioBuses.Effects => AudioMix.VolumeDb(AudioMix.DefaultEffects, AudioMix.DefaultMaster),
        AudioBuses.Voice => AudioMix.VolumeDb(AudioMix.DefaultVoice, AudioMix.DefaultMaster),
        _ => 0f,
    };

    // The mix half: a live change reaches all three child buses, the loudest possible mix leaves
    // every one of them at or below its resting gain, and bus 0 comes through both untouched. That
    // last pair is what makes a saved level unable to un-silence a scripted run.
    private static void CheckMix(TestContext ctx, StringBuilder report)
    {
        float developer = AudioServer.GetBusVolumeDb(0);
        report.AppendLine($"developer volume on bus 0 db={developer:0.###}, outside the mix");
        try
        {
            AudioMix.Apply(master: 50, music: 100, effects: 0, voice: 25);
            CheckBus(ctx, AudioBuses.Music, 100, 50, report);
            CheckBus(ctx, AudioBuses.Effects, 0, 50, report);
            CheckBus(ctx, AudioBuses.Voice, 25, 50, report);

            AudioMix.Apply(master: 100, music: 100, effects: 100, voice: 100);
            foreach (string bus in new[] { AudioBuses.Music, AudioBuses.Effects, AudioBuses.Voice })
            {
                float db = AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex(bus));
                ctx.Check(db <= DbSlack, $"the loudest mix leaves {bus} at or below its resting gain db={db:0.###}");
            }
            float after = AudioServer.GetBusVolumeDb(0);
            ctx.Check(Math.Abs(after - developer) <= DbSlack,
                $"the mix left bus 0 alone db={after:0.###} before={developer:0.###}");
            ctx.Check(developer > SilenceDb + DbSlack || after <= SilenceDb + DbSlack,
                $"a run silenced by --volume= stays silent at full levels db={after:0.###}");
            ctx.Note($"developer volume db={developer:0.###} unchanged by the mix");
        }
        finally
        {
            AudioMix.Apply();
        }

        CheckBus(ctx, AudioBuses.Music, AudioMix.DefaultMusic, AudioMix.DefaultMaster, report);
        CheckBus(ctx, AudioBuses.Effects, AudioMix.DefaultEffects, AudioMix.DefaultMaster, report);
        CheckBus(ctx, AudioBuses.Voice, AudioMix.DefaultVoice, AudioMix.DefaultMaster, report);
    }

    private static void CheckBus(TestContext ctx, string bus, int level, int master,
        StringBuilder report)
    {
        int index = AudioServer.GetBusIndex(bus);
        float db = index < 0 ? float.NaN : AudioServer.GetBusVolumeDb(index);
        float want = AudioMix.VolumeDb(level, master);
        report.AppendLine($"  {bus} level={level} master={master} db={db:0.###} want={want:0.###}");
        ctx.Check(index > 0 && Math.Abs(db - want) <= DbSlack,
            $"{bus} carries level {level} under master {master} db={db:0.###} want={want:0.###}");
    }

    private static void CheckSite(TestContext ctx, Node owner, string site, string bus,
        StringBuilder report) =>
        CheckSite(ctx, owner, site, bus, bus, report);

    // One construction site: at least one player under the node that owns it, and every one of them
    // on a bus this site is allowed to use. The "at least one" half is what stops a site that
    // silently built nothing from reading as a site that placed everything correctly.
    private static void CheckSite(TestContext ctx, Node owner, string site, string bus,
        string alsoBus, StringBuilder report)
    {
        var players = new List<(string Path, string Bus)>();
        Collect(owner, owner, players);
        report.AppendLine($"{site}: {players.Count} player(s)");
        foreach (var (path, actual) in players)
        {
            report.AppendLine($"  {path} bus={actual}");
        }
        ctx.Check(players.Count >= AtLeastOne, $"{site} built at least one player got={players.Count}");
        foreach (var (path, actual) in players)
        {
            ctx.Check(actual == bus || actual == alsoBus,
                $"{site} placed {path} on {bus}{(alsoBus == bus ? "" : $"/{alsoBus}")} actual={actual}");
        }
    }

    // The catch-all: every player anywhere in the live tree, including one this suite never named.
    // Reads Bus rather than a construction argument, so an unknown bus name reports as Master here
    // exactly as it would sound.
    private static void SweepTree(TestContext ctx, StringBuilder report)
    {
        var root = ctx.Host.GetTree()?.Root;
        if (root == null)
        {
            ctx.Check(false, $"the suite host is not in a live scene tree, so nothing was swept");
            return;
        }
        var players = new List<(string Path, string Bus)>();
        Collect(root, root, players);
        var onMaster = players.Where(p => p.Bus == AudioBuses.Master).ToList();
        report.AppendLine($"live tree: {players.Count} player(s), {onMaster.Count} on Master");
        foreach (var (path, bus) in players)
        {
            report.AppendLine($"  {path} bus={bus}");
        }
        ctx.Check(players.Count >= AtLeastOne, $"the tree walk found players got={players.Count}");
        foreach (var (path, _) in onMaster)
        {
            ctx.Check(false, $"player left on Master: {path}");
        }
        ctx.Note($"live players={players.Count} on_master={onMaster.Count}");
    }

    private static void Collect(Node from, Node root, List<(string Path, string Bus)> into)
    {
        string bus = from switch
        {
            AudioStreamPlayer p => p.Bus.ToString(),
            AudioStreamPlayer3D p => p.Bus.ToString(),
            _ => "",
        };
        if (bus.Length > 0)
        {
            into.Add((PathFrom(root, from), bus));
        }
        foreach (var child in from.GetChildren())
        {
            Collect(child, root, into);
        }
    }

    private static string PathFrom(Node root, Node node) =>
        ReferenceEquals(root, node) ? node.Name.ToString() : root.GetPathTo(node).ToString();

    private static AudioStreamWav? Stream(SoundArchive archive, IReadOnlyDictionary<string, SoundDef> defs,
        string name) =>
        defs.TryGetValue(name, out var def) ? archive.Find(def.WavName, def.Looped, warn: false) : null;

    // The first definition that actually resolves to an emitter, rather than a hard-coded name: an
    // install's sound archive decides which of them has a WAV behind it.
    private static string? FirstEmitter(WorldSounds sounds, IReadOnlyDictionary<string, SoundDef> defs)
    {
        foreach (string name in defs.Keys)
        {
            if (sounds.Create(name) != null)
            {
                return name;
            }
        }
        return null;
    }
}
