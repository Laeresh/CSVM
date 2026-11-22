using System;
using System.Collections.Generic;
using System.Text;
using CSVM.Launch;
using CSVM.Mech3;

namespace CSVM.Testing;

/// <summary>Suites over the state-driven score: which track each game state cues, that a repeat of
/// the playing state never restarts it, that the objective stingers alternate, and that the battle
/// hold starts, sustains and fades battle music.</summary>
internal static class MusicSuites
{
    // Frame step the state machine is driven at. A fixed step keeps the hold arithmetic exact,
    // which is what the fade assertions read.
    private const float Step = 0.1f;

    [Suite("music-states",
        "the state-driven score (D37): each game state cues the track family its data names, "
        + "prebattle and battle loop while the stingers and the success tracks play once, "
        + "whether a state or a mission's bare group name cues them, "
        + "re-entering the playing state never restarts it, the objective stingers alternate "
        + "their two takes instead of drawing at random, and a combat ping cuts prebattle to "
        + "battle at silence, ramps it to full in a quarter second, holds it 20 s and fades it "
        + "out over four; and the briefing's narration ducks the channel to silence over that fade "
        + "without touching it, for as long as the screen shows, and its end brings it back; a cinema's "
        + "pause holds the track in place, a cue taken under it starts held, and the film's end "
        + "releases it")]
    internal static void MusicStates(TestContext ctx)
    {
        ctx.RequireData(ctx.Zrdr, $"zrdr archive");
        ctx.RequireData(ctx.Data.Sounds, $"sound archive (soundsh)");
        var defs = SoundDefs.Load(ctx.Zrdr);
        var groups = SoundDefs.LoadGroups(ctx.Zrdr);
        using var archive = new SoundArchive(ctx.Sounds);
        var report = new StringBuilder();
        var rng = new Random(7);

        var music = new MusicPlayer(defs, groups)
        {
            Loader = (def, looped) => archive.Find(def.WavName, looped, warn: false),
        };
        ctx.Host.AddChild(music);

        try
        {
            CheckStates(ctx, music, rng, report);
            CheckCuesByName(ctx, music, rng, report);
            CheckStingersAlternate(ctx, music, rng, report);
            CheckBattleHold(ctx, music, rng, report);
            CheckDuck(ctx, music, rng, report);
            CheckPause(ctx, music, rng, report);
            CheckOffThread(ctx, music, archive, rng);
        }
        finally
        {
            music.Stop();
            ctx.Host.RemoveChild(music);
            music.Free();
        }

        ctx.WriteArtifact("test-music-states.txt", report.ToString());
    }

    // The game's loader decodes off the main thread. A cue takes the channel at once and its track
    // starts on the first Tick after the decode. A Stop drops it; a cached track starts at once.
    private static void CheckOffThread(TestContext ctx, MusicPlayer music, SoundArchive archive, Random rng)
    {
        var gate = new System.Threading.Tasks.TaskCompletionSource();
        music.LoaderOffThread = async (def, looped) =>
        {
            await gate.Task;
            return await archive.FindOffThread(def.WavName, looped);
        };
        try
        {
            music.Stop();
            string? wav = music.Cue("snd_music_splash", rng);
            ctx.Check(wav != null && music.Current == wav && music.Decoding,
                $"a cue still decoding takes the channel and waits wav={wav} decoding={music.Decoding}");
            music.Tick(Step, rng);
            ctx.Check(music.Decoding, $"…and does not start while its decode runs");
            gate.SetResult();
            var until = DateTime.UtcNow.AddSeconds(10);
            while (music.Decoding && DateTime.UtcNow < until)
            {
                System.Threading.Thread.Sleep(5);
                music.Tick(Step, rng);
            }
            ctx.Check(!music.Decoding && music.Current == wav,
                $"…then starts on a Tick once decoded decoding={music.Decoding} current={music.Current}");

            music.Stop();
            music.LoaderOffThread = (def, looped) => archive.FindOffThread(def.WavName, looped);
            music.Cue("snd_music_splash", rng);
            ctx.Check(!music.Decoding, $"a decoded track cued again starts at once");

            music.Stop();
            var held = new System.Threading.Tasks.TaskCompletionSource<Godot.AudioStream?>();
            music.LoaderOffThread = (_, _) => held.Task;
            music.Cue("snd_music_splash", rng);
            music.Stop();
            held.SetResult(archive.Find("music_splash.wav", false, warn: false));
            music.Tick(Step, rng);
            ctx.Check(!music.Decoding && music.Current.Length == 0,
                $"a Stop drops a decoding track for good current={music.Current}");
        }
        finally
        {
            music.LoaderOffThread = null;
            music.Stop();
        }
    }

    private static void CheckStates(TestContext ctx, MusicPlayer music, Random rng, StringBuilder report)
    {
        var expected = new (MusicState State, string Prefix, bool Loops)[]
        {
            (MusicState.Menu, "music_splash", false),
            (MusicState.Prebattle, "music_prebattle", true),
            (MusicState.Battle, "music_battle", true),
            (MusicState.PrimaryObjective, "music_primaryobj", false),
            (MusicState.SecondaryObjective, "music_secondaryobj", false),
            (MusicState.TertiaryObjective, "music_tertiaryobj", false),
            (MusicState.BattleSuccess, "music_battlesuccess", false),
            (MusicState.MissionSuccess, "music_missionsuccess", false),
        };

        foreach (var (state, prefix, loops) in expected)
        {
            string? wav = music.Enter(state, rng);
            report.AppendLine($"{state} -> {wav ?? "(none)"} loop={music.Looping}");
            ctx.Check(wav != null && wav.StartsWith(prefix, StringComparison.OrdinalIgnoreCase),
                $"{state} plays a {prefix} track wav={wav ?? "(none)"}");
            ctx.Check(music.Looping == loops, $"{state} loops={loops} actual={music.Looping}");
            ctx.Check(music.Current == wav, $"{state} leaves that track on the channel");
        }

        // The engine ignores a cue for the track already playing, which is what keeps returning to
        // the cabin from restarting the splash track. Menu is the state with one fixed track.
        music.Enter(MusicState.Menu, rng);
        string? again = music.Enter(MusicState.Menu, rng);
        ctx.Check(again == null, $"re-entering the playing state is a no-op result={again ?? "(none)"}");

        music.Enter(MusicState.Silent, rng);
        ctx.Check(music.Current.Length == 0, $"Silent leaves the channel empty current={music.Current}");
    }

    // A mission's sound-group cue reaches the channel as a bare group name, never as a state.
    // The forced loop has to hold on that path too.
    private static void CheckCuesByName(TestContext ctx, MusicPlayer music, Random rng, StringBuilder report)
    {
        var expected = new (string Cue, bool Loops)[]
        {
            ("music_prebattle_sg", true),
            ("MUSIC_PREBATTLE_SG", true),
            ("music_battle_sg", true),
            ("music_primaryobj_sg", false),
            ("music_battlesuccess_sg", false),
            ("music_missionsuccess_sg", false),
        };

        foreach (var (cue, loops) in expected)
        {
            music.Stop();
            string? wav = music.Cue(cue, rng);
            report.AppendLine($"cue {cue} -> {wav ?? "(none)"} loop={music.Looping}");
            ctx.Check(wav != null, $"the cue {cue} plays a track");
            ctx.Check(music.Looping == loops, $"the cue {cue} loops={loops} actual={music.Looping}");
        }

        music.Stop();
    }

    private static void CheckStingersAlternate(
        TestContext ctx, MusicPlayer music, Random rng, StringBuilder report)
    {
        var seen = new List<string>();
        for (int i = 0; i < 4; i++)
        {
            music.Enter(MusicState.Silent, rng);
            string? wav = music.Enter(MusicState.PrimaryObjective, rng);
            seen.Add(wav ?? "(none)");
        }

        report.AppendLine("primaryobj sequence: " + string.Join(", ", seen));
        ctx.Check(seen[0] != seen[1], $"the first two primary stingers differ {seen[0]} {seen[1]}");
        ctx.Check(seen[0] == seen[2] && seen[1] == seen[3],
            $"the primary stingers alternate rather than repeat {string.Join(",", seen)}");
        music.Enter(MusicState.Silent, rng);
    }

    private static void CheckBattleHold(
        TestContext ctx, MusicPlayer music, Random rng, StringBuilder report)
    {
        ctx.Check(MusicPlayer.ScanPings(MusicPlayer.BattleScanMinNearby),
            $"the proximity scan pings at {MusicPlayer.BattleScanMinNearby} nearby vehicles");
        ctx.Check(!MusicPlayer.ScanPings(MusicPlayer.BattleScanMinNearby - 1),
            $"it stays quiet one vehicle below that");

        music.Enter(MusicState.Prebattle, rng);
        string prebattle = music.Current;
        music.NoteCombat();
        music.Tick(Step, rng);
        report.AppendLine($"prebattle={prebattle} after ping -> {music.Current} gain={music.Gain:0.###}");
        ctx.Check(music.State == MusicState.Battle, $"a combat ping cuts prebattle to battle state={music.State}");
        ctx.Check(music.Gain <= (MusicPlayer.FadeInPerSecond * Step) + 0.001f,
            $"battle music enters on the fade ramp, not at full gain={music.Gain:0.###}");

        // A quarter second of fade-in is the decoded rate; a couple of extra steps prove it
        // clamps at full rather than overshooting.
        for (int i = 0; i < 5; i++)
        {
            music.Tick(Step, rng);
        }

        ctx.Check(music.Gain >= 0.999f, $"it reaches full gain within a quarter second gain={music.Gain:0.###}");

        float held = music.BattleHold;
        music.NoteCombat();
        ctx.Check(music.BattleHold >= held, $"a second ping never shortens the hold {held:0.##} -> {music.BattleHold:0.##}");

        int guard = 0;
        while (music.BattleHold > 0f && guard++ < 1000)
        {
            music.Tick(Step, rng);
        }

        ctx.Check(music.State == MusicState.Battle, $"battle music plays out the whole hold state={music.State}");

        guard = 0;
        while (music.Current.Length > 0 && guard++ < 1000)
        {
            music.Tick(Step, rng);
        }

        report.AppendLine($"battle faded out after {guard} steps of {Step:0.##}s");
        ctx.Check(music.Current.Length == 0, $"the expired hold fades battle music to silence");
        ctx.Check(guard * Step > 3f, $"the fade takes the decoded four seconds, not a cut steps={guard}");
    }

    // Through the menu audio seam a briefing drives, so the check is what the briefing hears.
    private static void CheckDuck(TestContext ctx, MusicPlayer music, Random rng, StringBuilder report)
    {
        var menu = new MenuAudioService(music, _ => null) { Name = "DuckMenuAudio" };
        ctx.Host.AddChild(menu);
        try
        {
            music.Enter(MusicState.Menu, rng);
            menu.BeginNarration("brief01.wav");
            int guard = 0;
            while (music.Duck > 0f && guard++ < 1000)
            {
                music.Tick(Step, rng);
            }

            report.AppendLine($"duck reached {music.Duck:0.###} in {guard} steps, gain={music.Gain:0.###}");
            ctx.Check(music.Duck == 0f, $"a briefing holds the channel silent duck={music.Duck:0.###}");
            ctx.Check(guard * Step <= 1f, $"the silence arrives inside a second steps={guard}");

            // The duck sits on top of the fade, so the track keeps playing and keeps its place.
            ctx.Check(music.Current.Length > 0, $"the silenced track keeps playing current={music.Current}");
            ctx.Check(music.Gain >= 0.999f, $"ducking leaves the decoded fade gain alone gain={music.Gain:0.###}");

            menu.EndNarration();
            guard = 0;
            while (music.Duck < 1f && guard++ < 1000)
            {
                music.Tick(Step, rng);
            }

            ctx.Check(music.Duck >= 0.999f, $"leaving the briefing brings the music back duck={music.Duck:0.###}");
            music.Enter(MusicState.Silent, rng);
        }
        finally
        {
            ctx.Host.RemoveChild(menu);
            menu.Free();
        }
    }

    private static void CheckPause(TestContext ctx, MusicPlayer music, Random rng, StringBuilder report)
    {
        string? splash = music.Enter(MusicState.Menu, rng);
        music.Paused = true;
        for (int i = 0; i < 10; i++)
        {
            music.Tick(Step, rng);
        }

        report.AppendLine($"paused {splash ?? "(none)"} -> current={music.Current} paused={music.Paused}");
        ctx.Check(music.Paused, $"a cinema's pause holds the channel across ticks paused={music.Paused}");
        ctx.Check(music.Current == splash, $"the paused track keeps its place current={music.Current}");

        // A cue that lands while a cinema shows must not be heard over it either.
        music.Enter(MusicState.Prebattle, rng);
        ctx.Check(music.Paused, $"a cue taken during the pause starts held current={music.Current}");

        music.Paused = false;
        ctx.Check(!music.Paused && music.Current.Length > 0,
            $"the film's end releases the track current={music.Current}");
        music.Enter(MusicState.Silent, rng);
    }
}
