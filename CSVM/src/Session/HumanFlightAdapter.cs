using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>Assembles one player's flight rig: the painted plane model, the
/// <see cref="FlightController"/> and everything hung on it — loadout/ordnance, compass, gauges,
/// HUD readout/reticle, damage visuals, audio, this player's stunt run, the spawn placement, and
/// the crash runtime built once the controller is in the tree.
///
/// Constructed once per session with the session-wide flight data (the
/// planes gamez, stats cache, pads, paint rng, spawn list, weapons/loadouts, HUD assets, the
/// shared projectile pool), then <see cref="Assemble"/>d once per rig, in player order.
/// ⚠ <b>Player order is load-bearing:</b> the paint rng and the spawn index wrap are shared
/// streams, so P1..P4 must draw in ascending order or every livery and spawn changes.</summary>
internal sealed class HumanFlightAdapter
{
    private readonly FlightRosterPolicy _policy;
    private readonly LiveryResolver _liveries;
    private readonly IFlightStarts _spawns;
    private readonly WorldEffectsFactory _worldEffects;
    private readonly Node3D _worldRoot;
    private readonly AircraftAssemblyResources _aircraft;
    private readonly FlightWorldBindings _world;
    private readonly HumanRosterBindings _human;

    // Every player's start, resolved in one call (see Assemble).
    private IReadOnlyList<FlightStart>? _starts;

    public HumanFlightAdapter(FlightRosterPolicy policy, LiveryResolver liveries, IFlightStarts spawns,
        WorldEffectsFactory worldEffects, Node3D worldRoot, AircraftAssemblyResources aircraft,
        FlightWorldBindings world, HumanRosterBindings human)
    {
        _policy = policy;
        _liveries = liveries;
        _spawns = spawns;
        _worldEffects = worldEffects;
        _worldRoot = worldRoot;
        _aircraft = aircraft;
        _world = world;
        _human = human;
    }

    /// <summary>Mesh instances the assembled planes added, accumulated across the rigs — the
    /// caller folds this into the build's count.</summary>
    public int MeshInstances { get; private set; }

    /// <summary>What the assembled rigs add to the build summary line (the stunt zone count).
    /// Accumulated here so the caller can append it to its own summary.</summary>
    public string WhatSuffix { get; private set; } = "";

    /// <summary>Phase 1 of the damage-visuals setup: the object itself, from the built model and
    /// the airframe's own stats. Both spawners call this unconditionally, before any crash runtime
    /// exists, because a rig without a crash program still flips torn panels through a null sink
    /// (see <see cref="DamageVisuals.DamageEffectSink"/>). Phase 2, the sink and the stops, is
    /// wired by <see cref="WorldEffectsFactory.BuildFlightCrashRuntime"/>: only that half needs a
    /// live runtime, and folding this half in with it would leave the parked damage lab blank.</summary>
    public static DamageVisuals BuildDamageVisuals(PlaneBuilder planeBuilder, Node3D planeModel,
        PlaneStats stats, AnimProgram? crashProgram)
    {
        PanelPairing? pairing = null;
        if (crashProgram is { } program)
        {
            var pairingDefs = new List<AnimDefinition>(program.ByAnimName("plane_reset"));
            foreach (var n in EffectCatalogue.PlayerDamageStageAnims)
                pairingDefs.AddRange(program.ByAnimName(n));
            pairing = DamageVisuals.PanelPairingSets(pairingDefs);
        }
        return new DamageVisuals(planeBuilder.DamagePanels, planeModel, stats, defPairing: pairing,
            cockpitPanels: planeBuilder.CockpitDamagePanels);
    }

    /// <summary>Builds player <paramref name="pi"/>'s aircraft into <paramref name="rig"/> and
    /// adds it to the session world. Call once per rig in ascending player order (see the class
    /// note on the shared rng streams).</summary>
    public void Assemble(int pi, PlayerRig rig, Action<FlightController> onCreated)
    {
        bool verbose = pi == 0; // the per-plane detail lines are identical for every player
        string tag = _human.RigCount > 1 ? $"P{pi + 1} " : "";
        // Each player flies their own pick; an active Instant Action mission overrides this for
        // every human alike, since the def carries one player_plane, not a per-player list.
        string planeName = _human.InstantActionPlayerPlaneNode
            ?? (_policy.PlaneNames.Count == 0
                ? _policy.PlaneName
                : _policy.PlaneNames[Math.Min(pi, _policy.PlaneNames.Count - 1)]);
        var stats = _aircraft.StatsFor(planeName);

        // The plane this pilot BUILT, when they picked one: its guns, pylons, paint and armour
        // replace the airframe's stock ones below. Null on every stock pick, and empty outside a
        // launchscreen launch, so no scripted path ever sees one.
        var custom = CustomPlaneFor(pi);
        string planeDisplay = custom?.Name is { Length: > 0 } customName
            ? customName
            : PlaneRoster.PlaneDisplayName(stats);

        // The engine pick, the original's registry override: the authored engines.json tier row
        // replaces the airframe's stock EnginePower on a COPY (the stats object is the shared
        // per-airframe cache). Stock pick (id 6) keeps the airframe's own row.
        if (custom != null
            && Flight.CustomPlaneBuild.EnginePowerFor(_aircraft.ZrdrPath, custom) is { } enginePower)
        {
            stats = stats.WithEnginePower(enginePower);
        }

        // Every player flies the Fortune Hunters livery unless --paint says otherwise, as the
        // original's stock planes do; a custom plane wears the paint it was built with instead,
        // through the same substitution path the livery lab drives. --paint still wins over both.
        long mark = StartupProfile.Mark();
        // cockpitInterior: a human rig is the only one whose pilot can look out of a cockpit
        // (PLAN-cockpit-view, B11) — FlightRoster's AI builder deliberately does not ask for one.
        var planeBuilder = new PlaneBuilder(_aircraft.PlanesGamez, _aircraft.Textures, spinningProps: true,
            scheme: custom != null && !_liveries.PaintRequested
                ? Flight.CustomPlaneBuild.PaintFor(custom, UI.HangarPaintPage.PatternName(custom.PaintPattern))
                : _liveries.SchemeFor(pi, _aircraft.ZrdrPath, _aircraft.PaintRng,
                    _liveries.PatternsForPlane(_aircraft.PlanesGamez, planeName)),
            patterns: _liveries.Patterns, cockpitInterior: true);
        var planeModel = planeBuilder.Build(planeName);
        StartupProfile.Record("plane", mark);
        MeshInstances += planeBuilder.MeshInstanceCount;

        var controller = new FlightController
        {
            DebugCollision = _world.DebugCollision,
            PinnedView = _policy.View,
            PinnedViewMode = _policy.ViewMode,
            HudParent = rig.Viewport,
            // Null when the airframe ships no cockpit1 — the rig then hides nothing, as before B11.
            Cockpit = CockpitVisibility.Bind(planeModel, planeBuilder.CockpitInterior),
        };
        if (verbose && controller.Cockpit != null)
            GD.Print($"cockpit: '{planeName}' interior built hidden at the cockpit_camera marker");
        // The engine pick's nitrous bit (ids 3-5) installs the injector, the original's veh+0x946.
        controller.Nitro.Installed = custom != null && Flight.CustomPlaneBuild.HasNitrous(custom);
        // Every human joins team 1 in an Instant Action mission, splitscreen included — the
        // per-pilot team fallback would otherwise collide with an enemy's. --coop asks the same
        // in plain flight; SessionSpec.Resolve already drops Coop when --vs is set.
        controller.Bind(new FlightControllerBuild
        {
            PlayerIndex = pi,
            IsHumanPiloted = true,
            // one scripted sequence per player ('|'-separated); the last covers the rest
            HoldSegments = _policy.HoldSets == null ? null
                : _policy.HoldSets[Math.Min(pi, _policy.HoldSets.Length - 1)],
            PlaneModel = planeModel,
            Props = PropAnimator.Build(planeModel),
            WingLights = WingLightBlinker.Build(planeBuilder.WingFlares, _policy.AnimLod),
            Surfaces = ControlSurfaceAnimator.Build(planeModel),
            Collider = PlaneCollider.Build(planeModel),
            // A custom plane's four bought zones stand in for the airframe's stock ARMOUR pools,
            // on those pools' own scale; structure stays the def's and the vehicle totals stay
            // the sum over zones. Nothing scales a human rig's pools, so the player never is.
            Damage = stats.DestroyableParts.Count == 0 ? null
                : custom != null ? Flight.CustomPlaneBuild.DamageFor(stats, custom)
                : PlaneDamage.For(stats),
            CollideDamageSink = _world.WorldRuntime != null ? _world.WorldRuntime.CollideDamageAt : null,
            GrazeEffectSink = _world.WorldEffects is { } fx ? (name, pt) => fx.PlayEffectAt(name, pt) : null,
            TouchdownDefs = _world.TouchdownDefs,
            Projectiles = _world.Projectiles,
            HumanPositions = _world.HumanPositions,
            // ⚠ Pass the null through. Null and empty are DIFFERENT bindings to Pads.For: null
            // reads every connected pad (the single-player default, which AssignPads returns for
            // one player), empty reads none. Coalescing here flew a single player pad-dead.
            PadDevices = _human.PadAssignment?[pi],
            UseKeyboard = pi == 0,
            AllowPause = true,
            Team = _human.InstantActionActive || _human.Coop ? AimAssist.PlayerTeam : null,
            Shake = new PlaneShake(_aircraft.Shakes),
        });
        onCreated(controller);

        // Guns/hardpoints: bind this plane's stock loadout (or the --loadout override) to
        // its built model — resolves markers to muzzle nodes + weapons to WeaponDefs.
        // Set before the controller enters the tree (its _Ready builds the fire state).
        var loadoutDefName = _policy.LoadoutOverride ?? stats.DefName;
        if (_aircraft.StockLoadouts.For(loadoutDefName) is { } stockDef)
        {
            // A custom plane's guns and hardpoints replace the stock ones and the Ammo Selection
            // layer composes over THAT (the built def leaves WeaponId null so a picked ammo still
            // resolves). --loadout= names a def outright, so it takes the whole fit either way.
            var baseDef = custom != null && _policy.LoadoutOverride == null
                ? Flight.CustomPlaneBuild.LoadoutFor(custom, stockDef)
                : stockDef;
            var ldef = MenuFitFor(pi) is { } choice ? choice.ApplyTo(baseDef) : baseDef;
            try
            {
                // The weapon lab flies the FULL-RIG loadout instead: every firepoint and
                // every pylon the airframe carries, seeded from this same stock fit — so the
                // panel can mount a weapon on a hardpoint the stock file never names.
                controller.Loadout = _policy.WeaponLab
                    ? Loadout.ForRig(planeModel, _aircraft.WeaponDefs, ldef)
                    : Loadout.Bind(ldef, planeModel, _aircraft.WeaponDefs);
                // The gun aim assist's structure candidates (B4): the world's
                // destructibles, when this session built a world at all.
                controller.Destructibles = _world.WorldRuntime?.Destructibles;
                controller.InfiniteAmmo = _policy.InfiniteAmmo;
                controller.AmmoCapOverride = _policy.AmmoCap;
                controller.AutoFire = _policy.AutoFire;
                controller.AutoFireRockets = _policy.AutoFireRockets;
                controller.InitialGunSelect = _policy.GunSelect;
                // --rocket=<wep_id>: swap every hardpoint's ordnance before the model is mounted.
                // A testing hook — all 11 stock loadouts carry HE, so this is the only way to
                // prove the mounted model varies by rocket type.
                if (_policy.RocketOverride != null)
                {
                    Testing.ProbeRunner.ApplyRocketOverride(controller.Loadout, _aircraft.WeaponDefs, _policy.RocketOverride, verbose);
                }
                // Hang the FLYOUT-model ordnance under the pylons — one body per pylon,
                // hidden as its ammo depletes. Uses the same gamez prototype the round flies.
                controller.Ordnance = PylonOrdnance.Build(controller.Loadout, _world.Projectiles, controller.InfiniteAmmo);
                if (verbose)
                {
                    int groups = 0;
                    foreach (var _ in controller.Loadout.FirableGuns) { groups++; }
                    GD.Print($"weapons: {groups} gun group(s), {controller.Loadout.Hardpoints.Count} " +
                             $"hardpoint(s), guns=Space/pad-B rockets=F/pad-A, " +
                             $"select guns=G/dpad-L rockets=H/dpad-R" +
                             (_policy.GunSelect != 0 ? $" [gun-select={_policy.GunSelect}]" : "") +
                             (_policy.InfiniteAmmo ? " (infinite ammo)" : "") +
                             (_policy.AmmoCap != null ? $" (--ammo={_policy.AmmoCap})" : ""));
                    if (controller.Ordnance is { } ord)
                    {
                        GD.Print($"pylon ordnance: {ord.Count} mounted rocket model(s)" +
                                 (_policy.RocketOverride != null ? $" (--rocket={_policy.RocketOverride})" : ""));
                    }
                }
            }
            catch (Exception e)
            {
                GD.PushWarning($"weapons: loadout bind failed for '{loadoutDefName}': {e.Message}");
            }
        }
        else if (verbose)
        {
            GD.Print($"weapons: no stock loadout for '{loadoutDefName}' — unarmed");
        }

        // The carried turret gunners: the vehicle def's thirdp turrets block resolved
        // by TITLE against ai.zrd and by node against this built model. Independent of the
        // stock loadout — the gunner's weapon comes from its ai.zrd row, not from a gun slot.
        if (_aircraft.TurretDefs is { } turretDefs && stats.TurretMounts.Count > 0)
        {
            controller.Turrets = TurretController.BuildCarried(
                turretDefs, stats, planeModel, _aircraft.WeaponDefs, controller, _world.Projectiles);
            if (verbose && controller.Turrets.Length > 0)
            {
                var descs = new List<string>();
                foreach (var t in controller.Turrets)
                    descs.Add($"{t.Def.Title} ({t.Weapon.Id}, {t.Firepoints.Length} muzzle(s))");
                GD.Print($"turrets: {string.Join(", ", descs)}");
            }
        }
        if (verbose && controller.Props != null)
            GD.Print($"props: {controller.Props.Count} spinning blur nodes");
        if (verbose && controller.WingLights != null)
            GD.Print($"wing lights: {controller.WingLights.Count} blinking flares");
        if (verbose && controller.Surfaces != null)
            GD.Print($"control surfaces: {controller.Surfaces.Count} deflecting nodes");
        if (controller.Collider != null)
        {
            if (verbose)
                GD.Print($"plane collider: {controller.Collider.Summary}");
        }
        else
        {
            GD.PushWarning("no airframe collision boxes — falling back to the center ray");
        }
        if (verbose && controller.Damage != null)
        {
            var partDescs = new List<string>();
            foreach (var p in stats.DestroyableParts)
                partDescs.Add($"{p.Name} {p.MaxHp:0}hp{(p.Critical ? "*" : "")}{(p.Engine ? " engine" : "")}");
            GD.Print($"damage parts: {string.Join(", ", partDescs)} (* = critical)");
        }
        if (custom != null)
        {
            // Names what the build reached.
            GD.Print($"{tag}custom plane: '{custom.Name}' on {planeName}, armour " +
                     $"{custom.ArmourNose}/{custom.ArmourTail}/{custom.ArmourLeftWing}/" +
                     $"{custom.ArmourRightWing} units x{Flight.CustomPlaneBuild.ArmourUnitScale}, " +
                     $"hardpoints {custom.LeftHardpoints}+{custom.RightHardpoints}, " +
                     $"engine {custom.Engine} thrust={stats.EnginePower:0.###}" +
                     (controller.Nitro.Installed ? " (nitrous injector)" : ""));
        }

        // Every readout this pane draws for its pilot belongs to the controller's own FlightHud,
        // which owns the per-frame feed; nothing here writes one after assembly.
        var pilotHud = controller.PilotHud;

        // The original's heading tape, rebuilt from the chapter's own HUD
        // textures (compassticks2/compasstxt ship in every chapter's archive).
        pilotHud.Compass = CompassTape.Build(_aircraft.Textures);
        if (verbose && pilotHud.Compass != null)
            GD.Print("compass: heading tape from compassticks2/compasstxt");

        // The cockpit dials (altimeter / speedometer / damage display), rebuilt
        // from the plane's own gauges subtree in planes.zbd + the chapter's
        // HUD textures (needle/lowalt/stall/<plane>_damage/hilite/hatchptrn).
        pilotHud.Gauges = GaugeCluster.Build(_aircraft.PlanesGamez, planeName, _aircraft.Textures,
            stats.DestroyableParts);
        if (pilotHud.Gauges is { } gauges)
        {
            var damage = controller.Damage;
            if (damage != null)
                gauges.PartFraction = name =>
                    damage.Parts.TryGetValue(name, out var s) ? s.Fraction : 1f;
            if (verbose)
                GD.Print("gauges: altimeter/speedometer/damage dial from the plane's gauges subtree");
        }

        // The bitmap-font proof overlay: draw the sample string on this pane so a 1P view
        // and a 4P pane can be compared (--hud-font-test). Set before the controller
        // enters the tree — its _Ready adds this to the HUD canvas.
        if (_aircraft.HudFont != null && _policy.HudFontTest)
        {
            pilotHud.FontTest = new HudFontTest(_aircraft.HudFont, _policy.HudFontTestText);
            if (verbose)
                GD.Print($"hud-font-test: '{_policy.HudFontTestText}' via 5pointhud font");
        }

        // The selected-weapon text readout: the gun group + rocket type and their
        // live ammo, drawn in the game's HUD font from the MSG_HUD_GUNGAUGE/MSG_HUD_MISSLES
        // templates. Built whenever the font loaded and the plane carries a loadout.
        if (_aircraft.HudFont != null && controller.Loadout != null)
        {
            pilotHud.WeaponReadout = WeaponReadout.Build(_aircraft.HudFont, _aircraft.WeaponMessages);
            if (verbose)
                GD.Print("weapon readout: MSG_HUD_GUNGAUGE/MSG_HUD_MISSLES via 5pointhud font");
        }

        // The gun aiming reticle: the ballistic impact point of the selected gun
        // group at the convergence distance, drawn as the game's pipper — visibly
        // trailing the nose in a hard turn, on the rounds in steady flight.
        if (_aircraft.ReticleTex != null && controller.Loadout != null)
        {
            pilotHud.Reticle = ImpactReticle.Build(_aircraft.ReticleTex, rig.Camera);
            if (verbose)
                GD.Print("gun reticle: ballistic impact point via impact_point.png");
        }

        // Visible damage, phase 1: the object, unconditionally. Phase 2 (the sink and the stops)
        // is wired by WorldEffectsFactory.BuildFlightCrashRuntime, since only that half needs a
        // live rig runtime.
        if (controller.Damage != null)
        {
            controller.Visuals = BuildDamageVisuals(planeBuilder, planeModel, stats, _world.CrashProgram);
            if (verbose)
                Log.Info("flight",
                    $"damage visuals: {controller.Visuals.PanelCount} panels — authored stage anims via the rig runtime");
        }

        // The data-driven crash rig is built AFTER the controller enters the tree
        // (below), so the crash def's reset states read valid global transforms.

        if (_world.Sounds != null && _world.SoundDefs != null)
        {
            var audio = new FlightAudio { MixGain = _human.MixGain };
            audio.Setup(_world.Sounds, _world.SoundDefs, stats, _world.SoundGroups);
            controller.Audio = audio;
            controller.AddChild(audio);
            if (verbose)
                GD.Print($"audio: engine={stats.EngineSound} " +
                         $"damaged={stats.DamagedEngineSound ?? "none"} " +
                         $"whine={stats.WhineSound ?? "none (no def names prop_sound)"} " +
                         $"rattle={stats.RattleSound}" +
                         (_human.MixGain < 1f ? $" (per-player mix gain {_human.MixGain:0.00})" : ""));
        }
        // This player's stunt run: player 1 flies the loaded instance, everyone else an
        // independent copy of the same zones — own progress, own clock.
        if (_human.StuntZones != null)
        {
            controller.Stunt = pi == 0 ? _human.StuntZones : _human.StuntZones.ForAnotherPlayer();
            controller.Stunt.LogTag = tag; // "P2 " in a race — one shared world, four runs
            controller.DebugCompleteStunt = _policy.DebugScoreboard;
            // The objective marker HUD, one per pane: projects that player's
            // active danger zone through THEIR camera, with the edge arrow + clock
            // bearing + run status.
            var marker = MarkerHud.Build(controller.Stunt, rig.Camera);
            pilotHud.Marker = marker;
            if (_human.Race is { } race)
            {
                // Racing: no per-player splits board — the shared ranked board
                // below covers the whole window when the last pilot is in. The marker
                // HUD shows this player's placing meanwhile.
                race.Add(pi, controller.Stunt, planeDisplay);
                controller.Race = race;
                marker.Race = race;
                marker.PlayerIndex = pi;
            }
            else if (_human.InstantActionActive)
            {
                // Instant Action carries the splits on its own wrap-up board instead, so the two
                // results boards cannot wake on the same event and stack (BL-358).
            }
            else
            {
                // Solo: the end-of-run scoreboard — per-zone splits + total +
                // persisted best time, keyed chapter/mission/plane in
                // user://stunt_scores.json (race totals are deliberately not recorded).
                var scoreKey = $"{_policy.Chapter}/{_policy.Mission}/{custom?.Name ?? planeName}";
                var scoreboard = StuntScoreboard.Build(controller.Stunt,
                    planeDisplay, $"{_policy.Chapter}   ·   {PlaneRoster.Humanize(_policy.Scenario)}",
                    ScoreStore.Load(), scoreKey, _human.ExitsToMenu, _human.PauseState, _human.MenuInputFor);
                scoreboard.Restart = controller.Rerun;
                scoreboard.Exit = _human.ExitSession;
                controller.Scoreboard = scoreboard;
                GD.Print($"stunt scoreboard: splits + best time (key '{scoreKey}')");
            }
            if (verbose)
            {
                GD.Print("stunt marker HUD: projected marker + edge arrow + clock bearing");
                WhatSuffix += $" [stunt: {controller.Stunt.TotalCount} zones]";
            }
        }

        // Dogfight (--vs): the per-pane match timer/K-D/leader line + kill banner, bound to the
        // match GameSession built before this loop ran; kill facts arrive later via Downed.
        if (_human.VersusMatch is { } versus)
        {
            // Rigs is the SAME list GameSession keeps live for the whole session — every seat
            // already exists (BuildRigs ran before this loop), only .Controller fills in as each
            // player assembles, so by the time this pane draws, every opponent's is populated.
            controller.VersusHud = VersusHud.Build(versus, pi, rig.Camera);
            controller.VersusHud.Rigs = _human.Rigs;
            if (verbose)
                GD.Print("dogfight HUD: match timer/K-D/leader line + kill banner + opponent markers");
        }

        // One per human pane, in EVERY flight session unlike VersusHud: built unconditionally
        // because generators spawn hostiles mid-session, and it draws nothing with an empty pool.
        var targetHud = TargetHud.Build(pi, rig.Camera, _world.Projectiles);
        pilotHud.TargetHud = targetHud;
        if (verbose)
            GD.Print("targeting HUD: selected-target marker (brackets + label, edge arrow off screen)");

        // The player's target selection: one per human pane, each with its own pool — the cycles
        // are sorted against THIS plane's pose, so they cannot be shared. GameSession binds
        // TargetSubParts later, once the zeppelins exist.
        controller.Targeting = new TargetSelection();
        controller.InitialTarget = _policy.TargetSelect;   // --target=, the scripted twin

        // ⚠ Bind on EVERY pane, not only under --debug-markers: it is what TargetHud.OwnTeam reads
        // this pane's side off, and the pilot-index derivation it falls back to is the
        // wingman-in-the-marker bug.
        targetHud.Own = controller;

        // --debug-markers: the same HUD marks every live aircraft instead of one hostile. Own also
        // keeps it from marking the aircraft the camera is sitting on.
        if (_policy.DebugMarkers)
        {
            targetHud.MarkAll = true;
            if (verbose)
                GD.Print("--debug-markers: marking EVERY live aircraft (red hostile / blue own side)");
        }

        // Every player's start comes from ONE call: a grid start is not decomposable, since no
        // single pilot's answer exists until every slot is known. Resolved lazily on the first
        // rig, so the caller can keep constructing the assembler before the rigs are known.
        var start = (_starts ??= _spawns.ChooseStarts(
            _human.SpawnList, _world.MissionZrdrPath, _human.SpawnBase, _human.RigCount))[pi];
        // The plant's force path is chosen once, here, off who is flying — a person, so the
        // player path. FlightModel.UsesAiForcePath carries why this is a construction argument
        // rather than the original's own pointer-compare-against-the-player test.
        controller.Setup(new FlightModel(stats, aiForcePath: !controller.IsHumanPiloted),
            rig.Camera, _aircraft.CamParamsFor(planeName), start.Pos, start.LookAt,
            start.ThrottleFrac, start.SpeedMps, cockpitCameraOffset: planeBuilder.CockpitCameraOffset);
        // --weapon-lab: a flight session whose aircraft is pinned at the spawn pose. Set after
        // Setup, so the pin, captured at the first held sim step, takes the pose Setup just wrote.
        if (_policy.WeaponLab)
        {
            controller.Held = true;
            if (verbose)
                GD.Print($"weapon lab: P{pi + 1} held at the spawn pose (world sim running)");
        }
        // The throttle-slam exhaust smoke: needs the plane's own exhaust marker
        // nodes plus the live throttle Setup just wrote, so it builds after Setup rather than
        // alongside Props/WingLights above.
        controller.ThrottleSmoke = ThrottleSlamSmoke.Build(planeModel, _aircraft.ZrdrPath, _aircraft.Textures,
            controller, controller.Throttle, _world.Ambience);

        // The ambient speed cue is chapter data, not an aircraft-model effect: one private copy
        // per player so splitscreen panes do not see another pilot's ahead-of-plane wisps.
        if (!_policy.EmptyStage)
        {
            controller.SpeedCue = SpeedCue.Build(_world.ChapterZrdrPath, _aircraft.Textures, _worldRoot,
                _world.Ambience,
                rig.VisualLayer == 0 ? null : node => SplitScreen.SetVisualLayer(node, rig.VisualLayer));
        }

        // The incoming-fire near-miss cue: this aircraft becomes a target every OTHER
        // pilot's rounds are measured against. After Setup — the target reads the live flight
        // model — and after PlayerIndex, the identity that excludes this pilot's own rounds.
        controller.AttachWarningShotCue(_world.Projectiles);
        controller.Name = $"player{pi + 1}";
        rig.Controller = controller;
        _worldRoot.AddChild(controller);

        // Data-driven crash: a per-player crash AnimRuntime playing the compiled def. Built here,
        // once the controller (and its plane model) are in the tree, so the crash def's reset
        // states resolve valid global transforms.
        if (_world.CrashProgram != null && _world.WorldScene != null)
        {
            // WorldSounds goes in on every rig alike; BuildFlightCrashRuntime drops it for a human
            // one, so that asymmetry is stated once, there. The planes gamez goes in on every rig
            // too, or a kill would drop a parachute for one spawner and not the other.
            _worldEffects.BuildFlightCrashRuntime(controller, planeBuilder, planeName, _world.Gamez,
                _world.WorldScene, _aircraft.Textures, _world.CrashProgram, verbose,
                worldSounds: _world.WorldRuntime?.Sounds, planesGamez: _aircraft.PlanesGamez);
            // The start choreography for the very first spawn: Respawn() plays this same def on
            // every later respawn, but Setup() above called Respawn() before this runtime existed.
            controller.CrashRuntime?.Play("startprops", planeModel, applyReset: false);
        }
    }

    public AssemblyState CaptureState() =>
        new(MeshInstances, WhatSuffix, _aircraft.PaintRng.State, _starts);

    public void RestoreState(AssemblyState state)
    {
        MeshInstances = state.MeshInstances;
        WhatSuffix = state.WhatSuffix;
        _aircraft.PaintRng.State = state.PaintRngState;
        _starts = state.Starts;
    }

    /// <summary>Pane <paramref name="pi"/>'s menu-chosen fit, or null to fly the stock one. An
    /// explicit <c>--loadout=</c> takes the whole choice away rather than merging with it, so the
    /// flag names the fit outright the way a playtest row needs; <c>--rocket=</c> needs no test
    /// here because it is applied after the bind and wins by arriving later.</summary>
    private LoadoutChoice? MenuFitFor(int pi)
    {
        if (_policy.LoadoutOverride != null || pi < 0 || pi >= _policy.MenuLoadouts.Count)
        {
            return null;
        }

        return _policy.MenuLoadouts[pi];
    }

    /// <summary>Pane <paramref name="pi"/>'s custom-built plane, or null to fly the stock
    /// airframe. Empty on every launch that did not come off the launchscreen, so the scripted
    /// paths (<c>--plane=</c>, <c>--det</c>) never see one.</summary>
    private Flight.CustomPlaneDef? CustomPlaneFor(int pi) =>
        pi >= 0 && pi < _policy.MenuCustomPlanes.Count ? _policy.MenuCustomPlanes[pi] : null;

    public readonly record struct AssemblyState(int MeshInstances, string WhatSuffix,
        ulong PaintRngState, IReadOnlyList<FlightStart>? Starts);

}
