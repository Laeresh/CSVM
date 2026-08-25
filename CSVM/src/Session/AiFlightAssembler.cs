using System;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

internal sealed class AiFlightAssembler
{
    private readonly FlightRosterPolicy _policy;
    private readonly LiveryResolver _liveries;
    private readonly WorldEffectsFactory? _worldEffects;
    private readonly Node3D _worldRoot;
    private readonly AircraftAssemblyResources _aircraft;
    private readonly FlightWorldBindings _world;
    private readonly int _humanCount;
    private AiSkills? _aiSkills;
    private System.Collections.Generic.List<Maneuver>? _maneuvers;
    private bool _noAssistLogged;

    public AiFlightAssembler(FlightRosterPolicy policy, LiveryResolver liveries,
        WorldEffectsFactory? worldEffects, Node3D worldRoot, AircraftAssemblyResources aircraft,
        FlightWorldBindings world, int humanCount)
    {
        _policy = policy;
        _liveries = liveries;
        _worldEffects = worldEffects;
        _worldRoot = worldRoot;
        _aircraft = aircraft;
        _world = world;
        _humanCount = humanCount;
    }

    public AiSkills? Skills => _aiSkills;

    public AssemblyState CaptureState(AiSpawn spawn)
    {
        var machine = spawn.Pilot.Machine;
        return new AssemblyState(spawn.Pilot.Gunner, spawn.Pilot.Rocketeer, machine,
            machine?.AttackRange, machine?.ReturnRange, _aircraft.PaintRng.State,
            Rng.Stream(Rng.Ai).State, Rng.Stream(Rng.Spawn).State,
            _aiSkills, _maneuvers, _noAssistLogged);
    }

    public void RestoreState(AiSpawn spawn, AssemblyState state)
    {
        spawn.Pilot.Gunner = state.Gunner;
        spawn.Pilot.Rocketeer = state.Rocketeer;
        spawn.Pilot.Machine = state.Machine;
        if (state.Machine != null)
        {
            state.Machine.AttackRange = state.AttackRange!.Value;
            state.Machine.ReturnRange = state.ReturnRange!.Value;
        }
        _aircraft.PaintRng.State = state.PaintRngState;
        Rng.Stream(Rng.Ai).State = state.AiRngState;
        Rng.Stream(Rng.Spawn).State = state.SpawnRngState;
        _aiSkills = state.Skills;
        _maneuvers = state.Maneuvers;
        _noAssistLogged = state.NoAssistLogged;
    }

    public FlightController Assemble(AiSpawn spawn, int index, Action<FlightController> onCreated)
    {
        var baseStats = _aircraft.AiStatsFor(spawn.PlaneName, spawn.AiDef);
        PreparePilot(spawn, baseStats);
        var stats = baseStats.WithAiSpawnJitter(
            Rng.NewSystemRandom(Rng.Spawn, index, 0));
        // The name the targeting readout prints, resolved here because this is where the string
        // table and the loaded def meet; a rig with no table keeps the def-name derivation.
        if (stats.AiTitleKey is { } titleKey && _aircraft.WeaponMessages is { } messages)
        {
            string title = messages.Get(titleKey);
            stats.AiTitle = title.StartsWith("MSG_", StringComparison.Ordinal) ? null : title;
        }
        if (spawn.Pilot.Machine is { } machine)
        {
            machine.AttackRange = stats.AiAttackRange;
            machine.ReturnRange = stats.AiReturnRange;
        }

        FlightController controller;
        Node3D planeModel;
        using (PerfSample.Scope(PerfSite.AiSpawn))
        {
            var planeBuilder = new PlaneBuilder(_aircraft.PlanesGamez, _aircraft.Textures, spinningProps: true,
                scheme: spawn.Scheme ?? MilitiaScheme(stats, spawn) ?? _liveries.SchemeFor(
                    _humanCount + index, _aircraft.ZrdrPath,
                    _aircraft.PaintRng, _liveries.PatternsForPlane(_aircraft.PlanesGamez, spawn.PlaneName),
                    useDefaultPattern: !spawn.ShippedSkins),
                patterns: _liveries.Patterns);
            planeModel = planeBuilder.Build(spawn.PlaneName);

            controller = new FlightController();
            controller.Bind(new FlightControllerBuild
            {
                PlayerIndex = FlightRoster.ShooterIdBase + index,
                IsHumanPiloted = false,
                Pilot = spawn.Pilot,
                PlaneModel = planeModel,
                Props = PropAnimator.Build(planeModel),
                WingLights = WingLightBlinker.Build(planeBuilder.WingFlares, _policy.AnimLod),
                Surfaces = ControlSurfaceAnimator.Build(planeModel),
                Collider = PlaneCollider.Build(planeModel),
                Damage = stats.DestroyableParts.Count > 0 || stats.VehicleHealth is > 0f
                    ? PlaneDamage.For(stats) : null,
                CollideDamageSink = _world.WorldRuntime != null ? _world.WorldRuntime.CollideDamageAt : null,
                GrazeEffectSink = _world.WorldEffects is { } fx ? (name, pt) => fx.PlayEffectAt(name, pt) : null,
                TouchdownDefs = _world.TouchdownDefs,
                Projectiles = _world.Projectiles,
                HumanPositions = _world.HumanPositions,
                PadDevices = Array.Empty<int>(),
                Inert = spawn.Inert,
                Team = spawn.Team,
                Shake = new PlaneShake(_aircraft.Shakes),
            });
            // The roster block's nitro slot installs the injector used by an AI's nitro evade.
            controller.Nitro.Installed = spawn.Nitro;
            onCreated(controller);

            // The AI def's own fit when it authors one, the player stock table otherwise. Both
            // paths say so when they come up empty: an AI plane that flies unarmed is a bug that
            // looks exactly like a passive enemy from the cockpit.
            try
            {
                if (stats.AiWeapons.Count > 0)
                {
                    controller.Loadout = Loadout.BindAi(stats.AiWeapons,
                        stats.AiDefName ?? stats.DefName, planeModel, _aircraft.WeaponDefs);
                }
                else if (_aircraft.StockLoadouts.For(stats.DefName) is { } loadout)
                {
                    controller.Loadout = Loadout.Bind(
                        spawn.Fit is { } fit ? fit.ApplyTo(loadout) : loadout, planeModel, _aircraft.WeaponDefs);
                }
                else
                {
                    string armed = stats.AiDefName ?? stats.DefName;
                    Log.Warn("weapons", $"ai: no weapons block on '{armed}' and no stock loadout for '{stats.DefName}' — this plane flies unarmed");
                }
                if (controller.Loadout != null)
                {
                    controller.Destructibles = _world.WorldRuntime?.Destructibles;
                    controller.Ordnance = PylonOrdnance.Build(controller.Loadout, _world.Projectiles, controller.InfiniteAmmo);
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
                    planeBuilder, planeModel, stats, _world.CrashProgram);
            }

            controller.Setup(new FlightModel(stats, aiForcePath: true), null, new CamParams(),
                spawn.Position, spawn.LookAt);
            controller.ArmSpawnTimers();
            // The authored identity wins where the caller has one, because the ranking reads this
            // name against patterns written for it. The counter form is the fallback for the
            // spawners with no authored name (--ai, the Instant Action fan, the generators).
            controller.Name = spawn.NodeName is { Length: > 0 } authored
                ? authored
                : $"ai{index + 1}_{spawn.PlaneName}";
            _worldRoot.AddChild(controller);
            // The engine loop, positional and culled at 2000 units. Attach no-ops to null when the
            // session found no sound archive; the own-ship FlightAudio is never built for an AI.
            controller.EngineAudio = AiEngineAudio.Attach(controller, _world.Sounds, _world.SoundDefs, stats);

            if (_world.CrashProgram != null && _world.WorldScene != null)
            {
                if (_worldEffects == null)
                    throw new InvalidOperationException("crash-enabled AI assembly needs world effects");
                // The planes gamez goes in here exactly as it does on the player rig: the destroy
                // def's `chuteman` is a template root of planes.zbd, and an asymmetry here would
                // give the parachute to one kind of kill only.
                _worldEffects!.BuildFlightCrashRuntime(controller, planeBuilder, spawn.PlaneName, _world.Gamez,
                    _world.WorldScene, _aircraft.Textures, _world.CrashProgram, verbose: false,
                    worldSounds: _world.WorldRuntime?.Sounds, planesGamez: _aircraft.PlanesGamez);
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

    private void PreparePilot(AiSpawn spawn, PlaneStats defStats)
    {
        var pilot = spawn.Pilot;
        var defSkills = defStats.AiPilotSkills;
        var roster = spawn.RosterSkills ?? default;
        // The precedence the original's roster has: a block's own slot first, then the def's, then
        // the flat rating. --ai-attack= pins every slot (docs/formats/ai-rosters.md).
        int SkillFor(int? authored, int fallback, int? rosterSlot = null) =>
            rosterSlot ?? (_policy.AiAttackSkillExplicit
                    || (spawn.AttackRating != null && spawn.RosterSkills == null)
                ? fallback : authored ?? fallback);

        if ((spawn.AttackRating ?? _policy.AiAttackSkill) is { } skill && pilot.Gunner == null)
        {
            try
            {
                _aiSkills ??= AiSkills.Load(_aircraft.ZrdrPath);
                var rng = new RandomNumberGenerator
                { Seed = (ulong)(uint)Rng.NewIntSeed(Rng.Ai) };
                pilot.Gunner = new AiGunner(rng)
                {
                    DeadEyeAngleDeg = _aiSkills.DeadEyeAngleDeg(
                        SkillFor(defSkills.DeadEye, skill, roster.DeadEye)),
                    QuickDrawAngleDeg = _aiSkills.QuickDrawAngleDeg(
                        SkillFor(defSkills.QuickDraw, skill, roster.QuickDraw)),
                };
                var ordRng = new RandomNumberGenerator
                { Seed = (ulong)(uint)Rng.NewIntSeed(Rng.Ai) };
                int quickDraw = SkillFor(defSkills.QuickDraw, skill, roster.QuickDraw);
                pilot.Rocketeer = new AiRocketeer(ordRng.Randf)
                {
                    QuickDrawAngleDeg = _aiSkills.QuickDrawAngleDeg(quickDraw),
                    QuickDrawChance = _aiSkills.QuickDrawChance(quickDraw),
                };
                GD.Print("ai: gunner armed at dead-eye " +
                    $"{SkillFor(defSkills.DeadEye, skill, roster.DeadEye)} / quick-draw {quickDraw} " +
                    $"(dead-eye {pilot.Gunner.DeadEyeAngleDeg:0.00}°, " +
                    $"quick-draw {pilot.Gunner.QuickDrawAngleDeg:0}°, " +
                    $"ordnance roll {pilot.Rocketeer.QuickDrawChance:0.00} per " +
                    $"{pilot.Rocketeer.RefireSeconds:0} s)");
            }
            catch (Exception e)
            {
                GD.PushWarning($"--ai-attack: cannot load ai_skill_parameters: {e.Message}");
            }
        }

        if (pilot.Machine != null)
            return;
        try
        {
            _aiSkills ??= AiSkills.Load(_aircraft.ZrdrPath);
            _maneuvers ??= Maneuvers.Load(_aircraft.ZrdrPath);
            int rating = spawn.AttackRating ?? _policy.AiAttackSkill ?? 5;
            int sixthSense = SkillFor(defSkills.SixthSense, rating, roster.SixthSense);
            pilot.Machine = new AiModeMachine(Rng.NewSystemRandom(Rng.Ai))
            {
                ActivationRange = _aiSkills.MinAiActiveDist,
                SteadyHandChance = _aiSkills.At("steady_hand_chance",
                    SkillFor(defSkills.SteadyHand, rating, roster.SteadyHand)),
                SixthSenseChance = _aiSkills.At("sixth_sense_chance", sixthSense),
                SixthSenseFactor = _aiSkills.At("sixth_sense_factor", sixthSense),
                StunRecoveryIntervalS = _aiSkills.At("stun_recovery_interval",
                    SkillFor(defSkills.StunRecovery, rating, roster.StunRecovery)),
                NaturalTouch = SkillFor(defSkills.NaturalTouch, rating, roster.NaturalTouch),
                Library = _maneuvers,
                AssistEnabled = !_policy.NoAssist,
            };
            if (_policy.NoAssist && !_noAssistLogged)
            {
                _noAssistLogged = true;
                GD.Print("ai assist: off (--no-assist): lay off disabled, pursue only");
            }
        }
        catch (Exception e)
        {
            GD.PushWarning($"ai: no mode machine — cannot load skills/maneuvers: {e.Message}");
        }
    }

    // The scheme the AI def authors for itself, which is what the original's spawn resolves against
    // (docs/org/paint.md). Yields to --paint= and to a caller asking for the bare shipped skins,
    // both of which are about this run rather than about who the plane is.
    private PaintScheme? MilitiaScheme(PlaneStats stats, AiSpawn spawn)
    {
        if (spawn.ShippedSkins || _liveries.PaintRequested || stats.AiDefName is not { } def)
            return null;
        return _liveries.DefScheme(_aircraft.ZrdrPath, def);
    }

    public readonly record struct AssemblyState(AiGunner? Gunner, AiRocketeer? Rocketeer,
        AiModeMachine? Machine, float? AttackRange, float? ReturnRange, ulong PaintRngState,
        ulong AiRngState, ulong SpawnRngState, AiSkills? Skills,
        System.Collections.Generic.List<Maneuver>? Maneuvers, bool NoAssistLogged);
}
