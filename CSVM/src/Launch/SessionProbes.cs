using System;
using System.Linq;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Campaign;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.Tooling;
using CSVM.Utils;
using Godot;

namespace CSVM.Launch;

/// <summary>The session build's scripted probes and build-time forces, each a step the session
/// calls at its own point in the build. The damage, effects, tile-grid and parked weapon probes
/// each report and end the process. The destroy and objective forces change the built world and
/// let the session fly on. Each reads the <see cref="BuildState"/>, and a probe that ran answers
/// true so the build stops. The reports themselves are <see cref="ProbeRunner"/>'s.
/// Module entry: docs/architecture/Launch.md on src/Launch/SessionProbes.cs.</summary>
internal sealed class SessionProbes
{
    private readonly SessionSpec _spec;
    private readonly ProbeRunner _runner;
    private readonly Node3D _worldRoot;
    private readonly Action<int> _quit;

    /// <summary>The probes of one session build. <paramref name="quit"/> ends the process with the
    /// given exit code, at the end of the frame as <see cref="SceneTree.Quit"/> does.</summary>
    public SessionProbes(SessionSpec spec, ProbeRunner runner, Node3D worldRoot, Action<int> quit)
    {
        _spec = spec;
        _runner = runner;
        _worldRoot = worldRoot;
        _quit = quit;
    }

    /// <summary><c>--damage-test</c> or <c>--effects-test</c> over the chapter world just built,
    /// before anything else joins it. True when one ran and the session ends.</summary>
    public bool RunWorldProbes(BuildState state, WorldSession world, WorldEffectsFactory effects,
        Camera3D camera)
    {
        // --damage-test: drive one destructible's HP through its DAMAGE_SEQUENCE stages and quit.
        // ⚠ The world subtree must be in the tree first (with ManualAdvance, so _Process does not
        // double-drive): ticking a death sequence reads global transforms.
        if (_spec.DamageTest)
        {
            _worldRoot.AddChild(world.Root);
            world.Runtime.ManualAdvance = true;
            _runner.RunDamageTest(_spec, world.Runtime);
            _quit(0);
            return true;
        }

        // --effects-test: play every impact/destruction effect and report which resolve and which
        // actually build a puffer, then quit. ⚠ Both subtrees must be in the tree first, or the
        // census counts nothing and the templates' global transforms are identity.
        if (_spec.EffectsTest && state.WorldScene != null)
        {
            _worldRoot.AddChild(world.Root);
            if (effects.EnsureWorldEffects(state.Gamez, state.WorldScene, state.Textures,
                    world.Program, world.Runtime) is { } runtime)
            {
                _runner.RunEffectsTest(_spec, camera, runtime,
                    EffectCatalogue.WorldEffectAnimNames(world.Program),
                    effects.EffectStage);
            }
            _quit(0);
            return true;
        }

        return false;
    }

    /// <summary><c>--dump-tilegrid</c>: the map edge's census, complete the moment the extender
    /// exists, written and the session ended. True when the flag asked for it.</summary>
    public bool DumpTileGrid(MapEdgeExtender? extender)
    {
        if (!_spec.DumpTileGrid)
            return false;

        // Nothing later in the build can change what was scanned, so no window is ever needed.
        string? report = extender?.WriteCensus(_spec.Chapter);
        if (report == null)
        {
            Log.Error("world", $"--dump-tilegrid: {_spec.Chapter} builds no map-edge continuation (no area/partition grid, or no recognizable ground tiles at all).");
            _quit(1);
            return true;
        }
        string name = _spec.DumpTileGridPath.Length > 0
            ? _spec.DumpTileGridPath
            : $"tilegrid_{_spec.Chapter}.json";
        _runner.WriteScratch(name, report);
        // WriteScratch resolves a relative name under ./.scratch/ and passes an absolute
        // one straight through, so the flag takes either.
        Log.Info("world", $"--dump-tilegrid: {_spec.Chapter} census → {name}");
        _quit(0);
        return true;
    }

    /// <summary><c>--weapon-test</c>: the whole-catalogue pass check on the parked plane. True when
    /// it ran and the session ends.</summary>
    public bool RunWeaponBench(BuildState state, Node3D? plane, Camera3D? camera)
    {
        // ⚠ Keep it on this cheap no-world path. It asks only whether every weapon mounts and
        // spawns without throwing. The interactive lab lives in flight, needing a real world.
        if (!_spec.WeaponTest || !_spec.Viewer || _spec.WorldMode || plane == null)
            return false;

        long mark = StartupProfile.Mark();
        var labWeapons = WeaponDefs.Load(state.ZrdrPath, Messages.Load(state.MessagesPath));
        StartupProfile.Record("zrdr", mark);
        // The bench fires the airframe's WHOLE rig, seeded from the plane's stock entry if any.
        // A weapon stock never mounts still gets a mount of its own class.
        LoadoutDef? stock = StockLoadouts.Load().All.Values.FirstOrDefault(d => d.Model == _spec.PlaneName);
        var benchLoadout = Loadout.ForRig(plane, labWeapons, stock);
        // The bench's own scene-less pool: rockets fly streak-only, impacts show stand-ins, and
        // there is no DamageSink. ⚠ Do not build a WeaponLab here; the pass check is the bench's.
        var benchPool = new ProjectilePool(state.Textures, null, null);
        _worldRoot.AddChild(benchPool);
        if (camera != null)
            benchPool.Viewers.Bind(new[] { camera });
        // Fire every weapon once per mount and report any that throw, then quit. The report is
        // synchronous, so no world tick is required.
        string report = WeaponBench.Run(plane, benchLoadout, labWeapons, benchPool).Report;
        Log.Raw(report);
        _runner.WriteScratch("weapon_test.txt", report);
        _quit(0);
        return true;
    }

    /// <summary><c>--destroy=&lt;name&gt;</c>: kills a named destructible at build, so a
    /// <c>--screenshot</c> captures its destruction with nobody at the controls. Reuses the
    /// weapon-damage path, so DamageAt runs the full death. A plane-less freecam asks for a
    /// world-effects runtime here, gated on the flag so a plain freecam builds nothing extra.
    /// </summary>
    public void Destroy(BuildState state, WorldEffectsFactory effects, SpectatorCamera? spectator)
    {
        if (_spec.DestroyName != null && state.WorldRuntime != null)
        {
            if (state.WorldScene != null)
            {
                effects.EnsureWorldEffects(state.Gamez, state.WorldScene, state.Textures, state.CrashProgram!, state.WorldRuntime);
            }
            int killed = ProbeRunner.TriggerDestroy(state.WorldRuntime, _spec.DestroyName, out var destroyBounds);
            state.What += killed > 0 ? $" + destroyed {killed}× '{_spec.DestroyName}'"
                               : $" + destroy '{_spec.DestroyName}' (no match)";
            // Frame the plane-less freecam on the kill unless --pos/--direction placed it. A bare
            // `--freecam --chapter=CX --destroy=name --screenshot=x.png` then frames itself.
            if (killed > 0 && spectator != null && _spec.CamPos == null && _spec.LookAt == null
                && _spec.CamDir == null && destroyBounds.Size.LengthSquared() > 0f)
            {
                spectator.Frame(destroyBounds);
            }
        }
        else if (_spec.DestroyName != null)
        {
            Log.Info("world", $"--destroy='{_spec.DestroyName}' ignored: no chapter world (pair it with --freecam/--fly + --chapter=)");
        }
    }

    /// <summary><c>--debug-objective=N</c>: the scripted twin of flying whatever completes campaign
    /// objective N. A <c>--screenshot</c> then shows a marked objectives line with nobody flying.
    /// It runs at build, before the graph's first step. That step then completes it off its own
    /// conditions rather than a mark faked into the display.</summary>
    public void ForceObjective(BuildState state, CampaignDirector? campaign)
    {
        if (_spec.DebugObjective is not int number || campaign == null)
        {
            return;
        }

        bool armed = ProbeRunner.ForceObjective(state.WorldRuntime, campaign, number);
        state.What += armed ? $" + forced OBJECTIVE{number}" : $" + OBJECTIVE{number} (not armed here)";
    }
}
