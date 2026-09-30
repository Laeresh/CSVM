using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        WithSessionRoster(ctx, plane, (roster, player, sounds, soundDefs) =>
        {
            var ai = SpawnAi(roster, plane, null, new Vector3(1000f, 3000f, 0f));
            if (ai == null)
            {
                ctx.Check(false, $"the roster built an AI {plane}");
                return;
            }

            foreach (var rig in new[] { player, ai })
            {
                CheckSilentSpawn(ctx, rig, "the assembler's first spawn");
                ctx.Check(!rig.PropsStopped && rig.CrashRuntime!.SuppressedMotionAnims.Contains(rig.SpinPropsAnim),
                    $"{rig.Name}: …with the discs on the slot and PropAnimator the only writer on them");
            }

            CheckOwnShipSilent(ctx, player, sounds, soundDefs);
        });
    }

    // The autogyro's own spin definition beside an ordinary aeroplane's, through the session's own
    // assemblers. The rigs are the flown autogyro, the Cabbie's AI def and an AI Warhawk. Then a
    // choke round trip, since agyro_rotors turns the propeller back on only through its
    // RESET_STATE, which the stop definition's cross-fade has switched off.
    [Suite("spawn-props-by-def",
        "the propeller definitions an aircraft plays are the ones its vehicle def names: a session-built player autogyro and CM21's Cabbie (AI def autogyro) start agyro_rotors and never spinprops, an AI Warhawk starts spinprops and never agyro_rotors, and a choke on each winds down with stopprops and restarts on its own spin definition with the propeller and, on the autogyro, the rotor back on screen")]
    internal static void SpawnPropsByDef(TestContext ctx)
    {
        const string autogyro = "player_autogyro", warhawk = "player_warhawk";
        const string cabbieDef = "autogyro", rotorAnim = "agyro_rotors";
        const float Dt = 1f / 60f;
        WithSessionRoster(ctx, autogyro, (roster, player, _, _) =>
        {
            var cabbie = SpawnAi(roster, autogyro, cabbieDef, new Vector3(1000f, 3000f, 0f));
            var ordinary = SpawnAi(roster, warhawk, null, new Vector3(-1000f, 3000f, 0f));
            if (cabbie == null || ordinary == null)
            {
                ctx.Check(false, $"the roster built the Cabbie ({cabbie != null}) and a Warhawk ({ordinary != null})");
                return;
            }

            var gyros = new[] { player, cabbie };
            foreach (var rig in gyros)
            {
                var runtime = rig.CrashRuntime!;
                ctx.Check(rig.SpinPropsAnim == rotorAnim,
                    $"{rig.Name}: the autogyro's def names its own spin definition spin={rig.SpinPropsAnim}");
                ctx.Check(runtime.AnimStateOf(rotorAnim) != 0 && runtime.AnimStateOf(EffectCatalogue.DefaultSpinPropsAnim) == 0,
                    $"{rig.Name}: the spawn started {rotorAnim} and not spinprops ({rotorAnim}={runtime.AnimStateOf(rotorAnim)} spinprops={runtime.AnimStateOf(EffectCatalogue.DefaultSpinPropsAnim)})");
                ctx.Check(runtime.SuppressedMotionAnims.Contains(rotorAnim),
                    $"{rig.Name}: …with its motion suppressed, so PropAnimator stays the only writer on the discs");
            }

            var plain = ordinary.CrashRuntime!;
            ctx.Check(ordinary.SpinPropsAnim == EffectCatalogue.DefaultSpinPropsAnim,
                $"{ordinary.Name}: the Warhawk's def names spinprops spin={ordinary.SpinPropsAnim}");
            ctx.Check(plain.AnimStateOf(EffectCatalogue.DefaultSpinPropsAnim) != 0 && plain.AnimStateOf(rotorAnim) == 0,
                $"{ordinary.Name}: the spawn started spinprops and not {rotorAnim} (spinprops={plain.AnimStateOf(EffectCatalogue.DefaultSpinPropsAnim)} {rotorAnim}={plain.AnimStateOf(rotorAnim)})");

            // The AI pair only: the human seat's flight step reads a person's controls.
            var flown = new[] { cabbie, ordinary };
            foreach (var rig in flown)
            {
                rig.CrashRuntime!.ManualAdvance = true;
                ctx.Check(rig.TryChokeEngine(3f) && rig.PropsStopped
                          && rig.CrashRuntime.AnimStateOf(rig.StopPropsAnim) != 0,
                    $"{rig.Name}: the choke started {rig.StopPropsAnim}");
            }

            for (float t = 0f; t < 6f && flown.Any(r => r.EngineDeadRemainingS > 0f || r.PropsStopped); t += Dt)
            {
                foreach (var rig in flown)
                {
                    rig.SimStep(Dt);
                    rig.CrashRuntime!.Advance(Dt);
                }
            }

            foreach (var rig in flown)
            {
                rig.CrashRuntime!.Advance(Dt);
                bool rotor = !ReferenceEquals(rig, cabbie) || Shown(rig, "rotor1");
                ctx.Check(!rig.PropsStopped && rig.CrashRuntime.AnimStateOf(rig.SpinPropsAnim) != 0
                          && Shown(rig, "prop1") && !Shown(rig, "staticprop1") && rotor,
                    $"{rig.Name}: the restart ran {rig.SpinPropsAnim} and put the discs back stopped={rig.PropsStopped} prop1={Shown(rig, "prop1")} staticprop1={Shown(rig, "staticprop1")} rotor1={rotor}");
            }
        });
    }

    // A spawn's propellers as the original starts them: the def's own spin definition, and no
    // definition that sounds snd_propstart ever started on this rig. ⚠ Keep the count check:
    // startprops must stay loaded, or the per-definition check passes on nothing.
    internal static void CheckSilentSpawn(TestContext ctx, FlightController rig, string what)
    {
        if (rig.CrashRuntime is not { } runtime)
        {
            ctx.Check(false, $"{rig.Name}: {what} left a crash rig to read");
            return;
        }

        string spin = rig.SpinPropsAnim;
        ctx.Check(runtime.AnimStateOf(spin) != 0, $"{rig.Name}: {what} spun the discs with {spin}");
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

    // One AI aircraft through the roster, high over the stage and holding its course.
    private static FlightController? SpawnAi(FlightRoster roster, string plane, string? aiDef, Vector3 at)
    {
        int before = roster.AiAircraft.Count;
        roster.SpawnAi(new AiSpawn(plane, at, at + Vector3.Forward, AiPilot.HoldingCourse(at, at + Vector3.Forward),
            AiDef: aiDef));
        return roster.AiAircraft.Count > before ? roster.AiAircraft[^1] : null;
    }

    // Whether a named node under the aeroplane is on screen: visible, and not faded to nothing. An
    // unfaded subtree reads -1 and counts as solid.
    private static bool Shown(FlightController rig, string node) =>
        FindNamed(rig.PlaneModel!, node) is { Visible: true } found && Alpha(found) is < 0f or > 0.01f;

    private static Node3D? FindNamed(Node root, string name)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Node3D n3d && AnimRuntime.NameOf(n3d).Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return n3d;
            }

            if (FindNamed(child, name) is { } hit)
            {
                return hit;
            }
        }

        return null;
    }

    // The strongest opacity a fade has left in the subtree, -1 where nothing has faded it.
    private static float Alpha(Node node)
    {
        float best = -1f;
        if (node is GeometryInstance3D g
            && g.GetInstanceShaderParameter(SceneBuilder.OpacityParam) is { VariantType: not Variant.Type.Nil } value)
        {
            best = value.AsSingle();
        }

        foreach (var child in node.GetChildren())
        {
            best = Mathf.Max(best, Alpha(child));
        }

        return best;
    }

    // The session's own FlightRoster over the suite's world, one human seat flying
    // <paramref name="playerPlane"/> high over the stage, the sound archive bound. Frees every
    // aircraft the body spawned.
    private static void WithSessionRoster(TestContext ctx, string playerPlane,
        Action<FlightRoster, FlightController, SoundArchive, Dictionary<string, SoundDef>> body)
    {
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
            try
            {
                var spec = SessionSpec.Parse(new[] { $"--plane={playerPlane}" });
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
                if (seat.Controller is not { } player)
                {
                    ctx.Check(false, $"the roster built a player {playerPlane}");
                    return;
                }

                body(roster, player, sounds, soundDefs);
            }
            finally
            {
                var live = seat.Controller;
                var members = roster != null ? new List<FlightController>(roster.AiAircraft) : new List<FlightController>();
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
