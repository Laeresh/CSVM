using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>The aggregate facts the initial human-field build adds to the session summary.</summary>
public readonly record struct FlightRosterBuild(int MeshInstances, string SummarySuffix);

/// <summary>One AI aircraft's authored identity and launch facts. <c>AiDef</c> names the militia
/// variant it flies (<c>bhatwarhawk</c>); null takes the airframe's base def. <c>Fit</c> is a
/// menu-chosen loadout laid over the stock table's, set only by the wingman spawns: the stock-table
/// branch below also catches enemies flying player airframes, which must keep their own fit.
/// <c>Nitro</c> is the roster block's injector flag (<see cref="AiSkills.RosterNitro"/>).</summary>
public readonly record struct AiSpawn(string PlaneName, Vector3 Position, Vector3 LookAt, AiPilot Pilot,
    PaintScheme? Scheme = null, int? Team = null, bool Inert = false, bool ShippedSkins = false,
    string? AiDef = null, LoadoutChoice? Fit = null, bool Nitro = false);

/// <summary>The session's aircraft set: builds the human field in deterministic player order and
/// introduces AI aircraft later for missions, waves, and generators. The roster is the assembly
/// seam; callers receive finished controllers and never configure one piecemeal.</summary>
public sealed class FlightRoster
{
    /// <summary>The first AI shooter id. Outside every human player index and the match roster.</summary>
    public const int ShooterIdBase = 100;

    private readonly HumanFlightAdapter _players;
    private readonly SessionSpec _spec;
    private readonly LiveryResolver _liveries;
    private readonly WorldEffectsFactory _worldEffects;
    private readonly Node3D _worldRoot;
    private readonly HumanFlightAdapter.Inputs _in;
    private int _spawned;

    internal FlightRoster(SessionSpec spec, LiveryResolver liveries, WorldEffectsFactory worldEffects,
        Node3D worldRoot, HumanFlightAdapter.Inputs inputs, IFlightStarts? starts = null)
    {
        _spec = spec;
        _liveries = liveries;
        _worldEffects = worldEffects;
        _worldRoot = worldRoot;
        _in = inputs;
        _players = new HumanFlightAdapter(spec, liveries, starts ?? new SpawnPicker(spec), worldEffects,
            worldRoot, inputs);
    }

    /// <summary>Builds every human aircraft in ascending player order. That order is part of the
    /// interface: it fixes the shared livery stream and spawn-list wrap.</summary>
    public FlightRosterBuild BuildPlayers(IReadOnlyList<PlayerRig> rigs)
    {
        for (int pi = 0; pi < rigs.Count; pi++)
            _players.Assemble(pi, rigs[pi]);
        return new FlightRosterBuild(_players.MeshInstances, _players.WhatSuffix);
    }

    /// <summary>Introduces one fully configured AI aircraft into the running session.</summary>
    public FlightController SpawnAi(AiSpawn spawn)
    {
        int index = _spawned++;
        var stats = _in.AiStatsFor(spawn.PlaneName, spawn.AiDef).WithAiSpawnJitter(
            Rng.NewSystemRandom(Rng.Spawn, index, 0));
        if (spawn.Pilot.Machine is { } machine)
        {
            machine.AttackRange = stats.AiAttackRange;
            machine.ReturnRange = stats.AiReturnRange;
        }

        FlightController controller;
        Node3D planeModel;
        using (PerfSample.Scope(PerfSite.AiSpawn))
        {
            var planeBuilder = new PlaneBuilder(_in.PlanesGamez, _in.Textures, spinningProps: true,
                scheme: spawn.Scheme ?? MilitiaScheme(stats, spawn) ?? _liveries.SchemeFor(
                    _in.RigCount + index, _in.ZrdrPath,
                    _in.PaintRng, _liveries.PatternsForPlane(_in.PlanesGamez, spawn.PlaneName),
                    useDefaultPattern: !spawn.ShippedSkins),
                patterns: _liveries.Patterns);
            planeModel = planeBuilder.Build(spawn.PlaneName);

            controller = new FlightController();
            controller.Bind(new FlightControllerBuild
            {
                PlayerIndex = ShooterIdBase + index,
                IsHumanPiloted = false,
                Pilot = spawn.Pilot,
                PlaneModel = planeModel,
                Props = PropAnimator.Build(planeModel),
                WingLights = WingLightBlinker.Build(planeBuilder.WingFlares, _spec.AnimLod),
                Surfaces = ControlSurfaceAnimator.Build(planeModel),
                Collider = PlaneCollider.Build(planeModel),
                Damage = stats.DestroyableParts.Count > 0 || stats.VehicleHealth is > 0f
                    ? PlaneDamage.For(stats) : null,
                CollideDamageSink = _in.WorldRuntime != null ? _in.WorldRuntime.CollideDamageAt : null,
                GrazeEffectSink = _in.WorldEffects is { } fx ? (name, pt) => fx.PlayEffectAt(name, pt) : null,
                TouchdownDefs = _in.TouchdownDefs,
                Projectiles = _in.Projectiles,
                HumanPositions = _in.HumanPositions,
                PadDevices = Array.Empty<int>(),
                Inert = spawn.Inert,
                Team = spawn.Team,
                Shake = new PlaneShake(_in.Shakes),
            });
            // The roster block's nitro slot: the injector an AI's nitro_evade needs.
            controller.Nitro.Installed = spawn.Nitro;

            // The AI def's own fit when it authors one, the player stock table otherwise. Both
            // paths say so when they come up empty: an AI plane that flies unarmed is a bug that
            // looks exactly like a passive enemy from the cockpit.
            try
            {
                if (stats.AiWeapons.Count > 0)
                {
                    controller.Loadout = Loadout.BindAi(stats.AiWeapons,
                        stats.AiDefName ?? stats.DefName, planeModel, _in.WeaponDefs);
                }
                else if (_in.StockLoadouts.For(stats.DefName) is { } loadout)
                {
                    controller.Loadout = Loadout.Bind(
                        spawn.Fit is { } fit ? fit.ApplyTo(loadout) : loadout, planeModel, _in.WeaponDefs);
                }
                else
                {
                    string armed = stats.AiDefName ?? stats.DefName;
                    Log.Warn("weapons", $"ai: no weapons block on '{armed}' and no stock loadout for '{stats.DefName}' — this plane flies unarmed");
                }
                if (controller.Loadout != null)
                {
                    controller.Destructibles = _in.WorldRuntime?.Destructibles;
                    controller.Ordnance = PylonOrdnance.Build(controller.Loadout, _in.Projectiles, controller.InfiniteAmmo);
                }
            }
            catch (Exception e)
            {
                Log.Warn("weapons", $"ai: loadout bind failed for '{stats.DefName}' — this plane flies unarmed error={e.Message}");
            }

            // Visible damage, phase 1: the same object the player rig gets, built from this
            // airframe's own injure_anims. Phase 2 (the sink and the stops) is wired by
            // BuildFlightCrashRuntime below, once the runtime it plays into exists.
            if (controller.Damage != null)
            {
                controller.Visuals = HumanFlightAdapter.BuildDamageVisuals(
                    planeBuilder, planeModel, stats, _in.CrashProgram);
            }

            controller.Setup(new FlightModel(stats, aiForcePath: true), null, new CamParams(),
                spawn.Position, spawn.LookAt);
            controller.ArmSpawnTimers();
            controller.Name = $"ai{index + 1}_{spawn.PlaneName}";
            _worldRoot.AddChild(controller);
            // The engine loop, positional and culled at 2000 units. Attach no-ops to null when the
            // session found no sound archive; the own-ship FlightAudio is never built for an AI.
            controller.EngineAudio = AiEngineAudio.Attach(controller, _in.Sounds, _in.SoundDefs, stats);

            if (_in.CrashProgram != null && _in.WorldScene != null)
            {
                // The planes gamez goes in here exactly as it does on the player rig: the destroy
                // def's `chuteman` is a template root of planes.zbd, and an asymmetry here would
                // give the parachute to one kind of kill only.
                _worldEffects.BuildFlightCrashRuntime(controller, planeBuilder, spawn.PlaneName, _in.Gamez,
                    _in.WorldScene, _in.Textures, _in.CrashProgram, verbose: false,
                    worldSounds: _in.WorldRuntime?.Sounds, planesGamez: _in.PlanesGamez);
                controller.CrashRuntime?.Play("startprops", planeModel, applyReset: false);
            }
        }

        GD.Print($"ai: spawned '{spawn.PlaneName}' as {controller.Name} (shooter id " +
                 $"{controller.PlayerIndex}) pos=({spawn.Position.X:0},{spawn.Position.Y:0},{spawn.Position.Z:0}) " +
                 $"jitter=(fd {stats.FdSpeed:0.0} thrust {stats.EnginePower:0.000}) " +
                 (spawn.Inert ? "INERT " : "") +
                 (spawn.Pilot.Patrol is { } patrol
                     ? $"net='{patrol.Net.Name}#{patrol.Net.Id}' ({patrol.Net.Nodes.Count} nodes)"
                     : $"heading={spawn.Pilot.TargetHeadingDeg:0}° alt={spawn.Pilot.TargetAltitude:0} m"));
        return controller;
    }

    // The scheme the AI def authors for itself, which is what the original's spawn resolves against
    // (docs/org/paint.md). Yields to --paint= and to a caller asking for the bare shipped skins,
    // both of which are about this run rather than about who the plane is.
    private PaintScheme? MilitiaScheme(PlaneStats stats, AiSpawn spawn)
    {
        if (spawn.ShippedSkins || _liveries.PaintRequested || stats.AiDefName is not { } def)
            return null;
        return _liveries.DefScheme(_in.ZrdrPath, def);
    }
}
