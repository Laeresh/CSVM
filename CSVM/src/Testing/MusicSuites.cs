using System;
using System.Collections.Generic;
using System.Text;
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
        + "re-entering the playing state never restarts it, the objective stingers alternate "
        + "their two takes instead of drawing at random, and a combat ping cuts prebattle to "
        + "battle at silence, ramps it to full in a quarter second, holds it 20 s and fades it "
        + "out over four; and the briefing's duck rides over that fade without touching it, "
        + "holding the channel down while the screen shows and lifting when it goes")]
    internal static void MusicStates(TestContext ctx)
    {
        var defs = SoundDefs.Load(ctx.ZrdrPath);
        var groups = SoundDefs.LoadGroups(ctx.ZrdrPath);
        using var archive = new SoundArchive(ctx.SoundsPath);
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
            CheckStingersAlternate(ctx, music, rng, report);
            CheckBattleHold(ctx, music, rng, report);
            CheckDuck(ctx, music, rng, report);
        }
        finally
        {
            music.Stop();
            ctx.Host.RemoveChild(music);
            music.Free();
        }

        ctx.WriteArtifact("test-music-states.txt", report.ToString());
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

    private static void CheckDuck(TestContext ctx, MusicPlayer music, Random rng, StringBuilder report)
    {
        music.Enter(MusicState.Menu, rng);
        music.Ducked = true;
        int guard = 0;
        while (music.Duck > MusicPlayer.DuckLevel && guard++ < 1000)
        {
            music.Tick(Step, rng);
        }

        report.AppendLine($"duck reached {music.Duck:0.###} in {guard} steps, gain={music.Gain:0.###}");
        ctx.Check(music.Duck <= MusicPlayer.DuckLevel + 0.001f, $"a ducking screen holds the channel down duck={music.Duck:0.###}");
        ctx.Check(guard * Step <= 1f, $"the duck arrives inside a second steps={guard}");

        // The duck is a mix decision over the fade, so the track keeps playing and keeps its place.
        ctx.Check(music.Current.Length > 0, $"the ducked track keeps playing current={music.Current}");
        ctx.Check(music.Gain >= 0.999f, $"ducking leaves the decoded fade gain alone gain={music.Gain:0.###}");

        music.Ducked = false;
        guard = 0;
        while (music.Duck < 1f && guard++ < 1000)
        {
            music.Tick(Step, rng);
        }

        ctx.Check(music.Duck >= 0.999f, $"leaving the screen lifts the duck duck={music.Duck:0.###}");
        music.Enter(MusicState.Silent, rng);
    }
}
