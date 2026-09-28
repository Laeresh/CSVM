using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Roster;
using CSVM.Session.World;
using CSVM.UI.Screens;
using Godot;

namespace CSVM.Testing;

/// <summary>An aircraft's propellers at a spawn. The original runs the silent, instant
/// <c>spinprops</c> as the tail of its <c>start_anims</c>, and never <c>startprops</c> or its
/// <c>snd_propstart</c> (docs/org/ordnanceTypes.md).</summary>
internal static class SpawnPropsSuites
{
    private const string PropStartSound = "snd_propstart";

    // The player's and an AI's first spawn through the session's own assemblers. The sound archive
    // is bound, so the own-ship audio is the one a session builds.
    [Suite("spawn-props-silent",
        "a session-built player and AI aircraft start with their engines already running, as in the original's Instant Action and CM01: the assemblers play the silent, instant spinprops, no definition that sounds snd_propstart (startprops) ever starts, and the player's own-ship audio carries no snd_propstart player while its engine loop sounds")]
    internal static void SpawnPropsSilent(TestContext ctx)
    {
        const string plane = "player_warhawk";
        ctx.RequireData(ctx.PlanesGamezPath, $"planes gamez");
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(ctx.SoundsPath, $"sound archive (soundsh)");
        ctx.WithWorld(ctx.Chapter, collision: false, world =>
        {
            var planesGamez = GameZ.Load(ctx.PlanesGamezPath);
            var textures = new TextureArchive(SessionPaths.ChapterTextures(ctx.DataRoot, world.Chapter));
            using var sounds = new SoundArchive(ctx.SoundsPath);
            var soundDefs = SoundDefs.Load(ctx.ZrdrPath);
            var pool = new ProjectilePool(textures, null, null);
            ctx.Host.AddChild(pool);
            var pane = new SubViewport();
            ctx.Host.AddChild(pane);
            var seat = new PlayerRig { Index = 0, Camera = ctx.Camera, HudParent = pane, Viewport = pane };
            FlightRoster? roster = null;
            var members = new List<FlightController>();
            try
            {
                var spec = SessionSpec.Parse(new[] { $"--plane={plane}" });
                var resources = new AircraftAssemblyResources
                {
                    PlanesGamez = planesGamez,
                    StatsFor = p => PlaneStats.Load(ctx.ZrdrPath, p),
                    AiStatsFor = (p, aiDef) => PlaneStats.LoadForAi(ctx.ZrdrPath, p, aiDef),
                    CamParamsFor = _ => new CamParams(),
                    PaintRng = new RandomNumberGenerator(),
                    ZrdrPath = ctx.ZrdrPath,
                    StockLoadouts = StockLoadouts.Load(),
                    WeaponDefs = WeaponDefs.Load(ctx.ZrdrPath, null),
                    WeaponMessages = Messages.Load(ctx.MessagesPath),
                    Textures = textures,
                    Shakes = ShakeDefs.Load(ctx.ZrdrPath),
                };
                roster = new FlightRoster(FlightRosterPolicy.From(spec),
                    new LiveryResolver(spec, Path.Combine(ctx.DataRoot, "extracted", "rof")),
                    new WorldEffectsFactory(spec, ctx.Host, () => Vector3.Zero), ctx.Host, resources,
                    new FlightWorldBindings
                    {
                        Projectiles = pool,
                        Gamez = world.Gamez,
                        WorldScene = world.Session.Builder.Scene,
                        CrashProgram = world.Session.Program,
                        Sounds = sounds,
                        SoundDefs = soundDefs,
                        SoundGroups = SoundDefs.LoadGroups(ctx.ZrdrPath),
                    },
                    new HumanRosterBindings
                    {
                        RigCount = 1,
                        Rigs = new[] { seat },
                        PauseState = new PauseState(),
                        MenuInputFor = _ => new MenuInput(),
                        ExitSession = () => { },
                    }, new HighStarts());
                roster.BuildPlayers(new[] { seat });
                var aiAt = new Vector3(1000f, 3000f, 0f);
                roster.SpawnAi(new AiSpawn(plane, aiAt, aiAt + Vector3.Forward,
                    AiPilot.HoldingCourse(aiAt, aiAt + Vector3.Forward)));
                members.AddRange(roster.AiAircraft);
                if (seat.Controller is not { } player || members.Count == 0)
                {
                    ctx.Check(false, $"the roster built a player ({seat.Controller != null}) and an AI ({members.Count})");
                    return;
                }

                foreach (var rig in new[] { player, members[0] })
                {
                    CheckSilentSpawn(ctx, rig, "the assembler's first spawn");
                    ctx.Check(!rig.PropsStopped && rig.CrashRuntime!.SuppressedMotionAnims.Contains("spinprops"),
                        $"{rig.Name}: …with the discs on the slot and PropAnimator the only writer on them");
                }

                CheckOwnShipSilent(ctx, player, sounds, soundDefs);
            }
            finally
            {
                var live = seat.Controller;
                roster?.ClearMembership();
                live?.Free();
                foreach (var ai in members)
                {
                    ai.Free();
                }

                pane.Free();
                pool.Free();
                textures.Dispose();
            }
        });
    }

    // A spawn's propellers as the original starts them: spinprops, and no definition that sounds
    // snd_propstart ever started on this rig. ⚠ Keep the count check: startprops must stay loaded,
    // or the per-definition check passes on nothing.
    internal static void CheckSilentSpawn(TestContext ctx, FlightController rig, string what)
    {
        if (rig.CrashRuntime is not { } runtime)
        {
            ctx.Check(false, $"{rig.Name}: {what} left a crash rig to read");
            return;
        }

        ctx.Check(runtime.AnimStateOf("spinprops") != 0, $"{rig.Name}: {what} spun the discs with spinprops");
        int sounding = 0;
        foreach (var def in runtime.ProgramDefs)
        {
            if (def.AnimName is not { } name || !SoundsPropStart(def))
            {
                continue;
            }

            sounding++;
            ctx.Same(0, runtime.AnimStateOf(name), $"{rig.Name}: {what} never started '{name}', which sounds {PropStartSound}");
        }

        ctx.Check(sounding > 0, $"{rig.Name}: the rig still loads a definition that sounds {PropStartSound}, so the check is not empty");
    }

    private static bool SoundsPropStart(AnimDefinition def)
    {
        foreach (var seq in def.Sequences)
        {
            foreach (var ev in seq.Events)
            {
                if (ev.Kind == "Sound"
                    && string.Equals(ev.Data.Str("name"), PropStartSound, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // The own-ship audio a human seat builds: the engine loop sounds, and no player it holds carries
    // the snd_propstart stream, which the install does carry.
    private static void CheckOwnShipSilent(TestContext ctx, FlightController player, SoundArchive sounds,
        Dictionary<string, SoundDef> defs)
    {
        if (player.Audio is not { } audio)
        {
            ctx.Check(false, $"{player.Name}: the seat built its own-ship audio");
            return;
        }

        var cue = defs.TryGetValue(PropStartSound, out var def) ? sounds.Find(def.WavName, looped: false) : null;
        ctx.Check(cue != null, $"the install carries {PropStartSound}, so its absence below is the port's choice");
        int carrying = 0;
        foreach (var child in audio.GetChildren())
        {
            if (child is AudioStreamPlayer p && cue != null && ReferenceEquals(p.Stream, cue))
            {
                carrying++;
            }
        }

        ctx.Same(0, carrying, $"{player.Name}: no own-ship player carries {PropStartSound}");
        ctx.Check(audio.EngineSounding, $"{player.Name}: …while the engine loop already sounds");
    }

    // One start, high over the stage, for the seat the suite builds.
    private sealed class HighStarts : IFlightStarts
    {
        public IReadOnlyList<FlightStart> ChooseStarts(IReadOnlyList<SpawnPoint>? spawns,
            string missionZrdrPath, int spawnBase, int playerCount)
        {
            var starts = new FlightStart[playerCount];
            for (int i = 0; i < playerCount; i++)
            {
                var position = new Vector3(i * 60f, 3000f, 0f);
                starts[i] = new FlightStart(position, position + Vector3.Forward, 0.5f, 90f);
            }

            return starts;
        }
    }
}
