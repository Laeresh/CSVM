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
/// Constructed once per session with the session-wide flight data (<see cref="Inputs"/>: the
/// planes gamez, stats cache, pads, paint rng, spawn list, weapons/loadouts, HUD assets, the
/// shared projectile pool), then <see cref="Assemble"/>d once per rig, in player order.
/// ⚠ <b>Player order is load-bearing:</b> the paint rng and the spawn index wrap are shared
/// streams, so P1..P4 must draw in ascending order or every livery and spawn changes.</summary>
public sealed class FlightRigAssembler
{
    private readonly SessionSpec _spec;
    private readonly LiveryResolver _liveries;
    private readonly IFlightStarts _spawns;
    private readonly WorldEffectsFactory _worldEffects;
    private readonly Node3D _worldRoot;
    private readonly Inputs _in;

    // Every player's start, resolved in one call (see Assemble).
    private IReadOnlyList<FlightStart>? _starts;

    public FlightRigAssembler(SessionSpec spec, LiveryResolver liveries, IFlightStarts spawns,
        WorldEffectsFactory worldEffects, Node3D worldRoot, Inputs inputs)
    {
        _spec = spec;
        _liveries = liveries;
        _spawns = spawns;
        _worldEffects = worldEffects;
        _worldRoot = worldRoot;
        _in = inputs;
    }

    /// <summary>Mesh instances the assembled planes added, accumulated across the rigs — the
    /// caller folds this into the build's count.</summary>
    public int MeshInstances { get; private set; }

    /// <summary>What the assembled rigs add to the build summary line (the stunt zone count).
    /// Accumulated here so the caller can append it to its own summary.</summary>
    public string WhatSuffix { get; private set; } = "";

    /// <summary>Builds player <paramref name="pi"/>'s aircraft into <paramref name="rig"/> and
    /// adds it to the session world. Call once per rig in ascending player order (see the class
    /// note on the shared rng streams).</summary>
    public void Assemble(int pi, PlayerRig rig)
    {
        bool verbose = pi == 0; // the per-plane detail lines are identical for every player
        string tag = _in.RigCount > 1 ? $"P{pi + 1} " : "";
        // Each player flies their own pick; an active Instant Action mission overrides this for
        // every human alike, since the def carries one player_plane, not a per-player list.
        string planeName = _in.InstantActionPlayerPlaneNode ?? PlaneRoster.PlaneFor(_spec, pi);
        var stats = _in.StatsFor(planeName);

        // Every player flies the Fortune Hunters livery unless --paint says otherwise,
        // as the original's stock planes do.
        long mark = StartupProfile.Mark();
        var planeBuilder = new PlaneBuilder(_in.PlanesGamez, _in.Textures, spinningProps: true,
            scheme: _liveries.SchemeFor(pi, _in.ZrdrPath, _in.PaintRng,
                _liveries.PatternsForPlane(_in.PlanesGamez, planeName)),
            patterns: _liveries.Patterns);
        var planeModel = planeBuilder.Build(planeName);
        StartupProfile.Record("plane", mark);
        MeshInstances += planeBuilder.MeshInstanceCount;

        var controller = new FlightController
        {
            // one scripted sequence per player ('|'-separated); the last covers the rest
            HoldSegments = _spec.HoldSets == null ? null
                : _spec.HoldSets[Math.Min(pi, _spec.HoldSets.Length - 1)],
            DebugCollision = _in.DebugCollision,
            // Set here, not in the stunt block below: the index is this
            // pilot's identity for the rounds they fire, so free flight needs it too.
            PlayerIndex = pi,
            PlaneModel = planeModel,
            Props = PropAnimator.Build(planeModel), // spin the propeller/rotor blur discs
            WingLights = WingLightBlinker.Build(planeBuilder.WingFlares, _spec.AnimLod), // blink the wingtip flares
            Surfaces = ControlSurfaceAnimator.Build(planeModel), // deflect ailerons/elevators/rudders
            Collider = PlaneCollider.Build(planeModel), // swept airframe boxes (wingtip/tail collision)
            // per-part HP from destroyable_parts — collisions below
            // the crash threshold damage the struck part instead of crashing
            Damage = stats.DestroyableParts.Count > 0 ? PlaneDamage.For(stats) : null,
            // Flying into a WeaponOrCollideHit object (the 44 facades/windows/agyrobus)
            // breaks it and passes through; every other collision stays solid.
            CollideDamageSink = _in.WorldRuntime != null ? _in.WorldRuntime.CollideDamageAt : null,
            // A survivable scrape plays touchdown.zrd's per-surface reaction (sparks/dust/
            // splash) at the contact point, through the same runtime a rocket impact uses.
            GrazeEffectSink = _in.WorldEffects is { } fx ? (name, pt) => fx.PlayEffectAt(name, pt) : null,
            // The level's one touchdown vector (the original's global), not one per plane.
            TouchdownDefs = _in.TouchdownDefs,
            // splitscreen: this player's own device(s), own pane for the HUD. Start/P reads on
            // every rig; GameSession wires every PauseState to one shared instance.
            PadDevices = _in.PadAssignment?[pi],
            UseKeyboard = pi == 0,
            PinnedView = _spec.View,
            HudParent = rig.Viewport,
            AllowPause = true,
        };
        // Every human joins team 1 in an Instant Action mission, splitscreen included — the
        // per-pilot team fallback would otherwise collide with an enemy's. --coop asks the same
        // in plain flight; SessionSpec.Resolve already drops Coop when --vs is set.
        if (_in.InstantActionActive || _in.Coop)
            controller.Team = AimAssist.PlayerTeam;
        // The wobble pivot: the plane model and everything resolved inside it rides the shake,
        // while the controller's own transform (physics, aim, chase camera) never sees it
        // (docs/formats/shakes.md).
        controller.Shake = new PlaneShake(_in.Shakes);
        var shakePivot = new Node3D { Name = "ShakePivot" };
        controller.ShakePivot = shakePivot;
        controller.AddChild(shakePivot);
        shakePivot.AddChild(planeModel);

        // Guns/hardpoints: bind this plane's stock loadout (or the --loadout override) to
        // its built model — resolves markers to muzzle nodes + weapons to WeaponDefs.
        // Set before the controller enters the tree (its _Ready builds the fire state).
        var loadoutDefName = _spec.LoadoutOverride ?? stats.DefName;
        if (_in.StockLoadouts.For(loadoutDefName) is { } ldef)
        {
            try
            {
                // The weapon lab flies the FULL-RIG loadout instead: every firepoint and
                // every pylon the airframe carries, seeded from this same stock fit — so the
                // panel can mount a weapon on a hardpoint the stock file never names.
                controller.Loadout = _spec.WeaponLab
                    ? Loadout.ForRig(planeModel, _in.WeaponDefs, ldef)
                    : Loadout.Bind(ldef, planeModel, _in.WeaponDefs);
                controller.Projectiles = _in.Projectiles;
                // The gun aim assist's structure candidates (B4): the world's
                // destructibles, when this session built a world at all.
                controller.Destructibles = _in.WorldRuntime?.Destructibles;
                controller.InfiniteAmmo = _spec.InfiniteAmmo;
                controller.AmmoCapOverride = _spec.AmmoCap;
                controller.AutoFire = _spec.AutoFire;
                controller.AutoFireRockets = _spec.AutoFireRockets;
                controller.InitialGunSelect = _spec.GunSelect;
                // --rocket=<wep_id>: swap every hardpoint's ordnance before the model is mounted.
                // A testing hook — all 11 stock loadouts carry HE, so this is the only way to
                // prove the mounted model varies by rocket type.
                if (_spec.RocketOverride != null)
                {
                    Testing.ProbeRunner.ApplyRocketOverride(controller.Loadout, _in.WeaponDefs, _spec.RocketOverride, verbose);
                }
                // Hang the FLYOUT-model ordnance under the pylons — one body per pylon,
                // hidden as its ammo depletes. Uses the same gamez prototype the round flies.
                controller.Ordnance = PylonOrdnance.Build(controller.Loadout, _in.Projectiles);
                if (verbose)
                {
                    int groups = 0;
                    foreach (var _ in controller.Loadout.FirableGuns) { groups++; }
                    GD.Print($"weapons: {groups} gun group(s), {controller.Loadout.Hardpoints.Count} " +
                             $"hardpoint(s), guns=Space/pad-B rockets=F/pad-A, " +
                             $"select guns=G/dpad-L rockets=H/dpad-R" +
                             (_spec.GunSelect != 0 ? $" [gun-select={_spec.GunSelect}]" : "") +
                             (_spec.InfiniteAmmo ? " (infinite ammo)" : "") +
                             (_spec.AmmoCap != null ? $" (--ammo={_spec.AmmoCap})" : ""));
                    if (controller.Ordnance is { } ord)
                    {
                        GD.Print($"pylon ordnance: {ord.Count} mounted rocket model(s)" +
                                 (_spec.RocketOverride != null ? $" (--rocket={_spec.RocketOverride})" : ""));
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
        if (_in.TurretDefs is { } turretDefs && stats.TurretMounts.Count > 0)
        {
            controller.Turrets = TurretController.BuildCarried(
                turretDefs, stats, planeModel, _in.WeaponDefs, controller, _in.Projectiles);
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

        // The original's heading tape, rebuilt from the chapter's own HUD
        // textures (compassticks2/compasstxt ship in every chapter's archive).
        controller.Compass = CompassTape.Build(_in.Textures);
        if (verbose && controller.Compass != null)
            GD.Print("compass: heading tape from compassticks2/compasstxt");

        // The cockpit dials (altimeter / speedometer / damage display), rebuilt
        // from the plane's own gauges subtree in planes.zbd + the chapter's
        // HUD textures (needle/lowalt/stall/<plane>_damage/hilite/hatchptrn).
        controller.Gauges = GaugeCluster.Build(_in.PlanesGamez, planeName, _in.Textures,
            stats.DestroyableParts);
        if (controller.Gauges != null)
        {
            var damage = controller.Damage;
            if (damage != null)
                controller.Gauges.PartFraction = name =>
                    damage.Parts.TryGetValue(name, out var s) ? s.Fraction : 1f;
            if (verbose)
                GD.Print("gauges: altimeter/speedometer/damage dial from the plane's gauges subtree");
        }

        // The bitmap-font proof overlay: draw the sample string on this pane so a 1P view
        // and a 4P pane can be compared (--hud-font-test). Set before the controller
        // enters the tree — its _Ready adds this to the HUD canvas.
        if (_in.HudFont != null && _spec.HudFontTest)
        {
            controller.FontTest = new HudFontTest(_in.HudFont, _spec.HudFontTestText);
            if (verbose)
                GD.Print($"hud-font-test: '{_spec.HudFontTestText}' via 5pointhud font");
        }

        // The selected-weapon text readout: the gun group + rocket type and their
        // live ammo, drawn in the game's HUD font from the MSG_HUD_GUNGAUGE/MSG_HUD_MISSLES
        // templates. Built whenever the font loaded and the plane carries a loadout.
        if (_in.HudFont != null && controller.Loadout != null)
        {
            controller.WeaponReadout = WeaponReadout.Build(_in.HudFont, _in.WeaponMessages);
            if (verbose)
                GD.Print("weapon readout: MSG_HUD_GUNGAUGE/MSG_HUD_MISSLES via 5pointhud font");
        }

        // The gun aiming reticle: the ballistic impact point of the selected gun
        // group at the convergence distance, drawn as the game's pipper — visibly
        // trailing the nose in a hard turn, on the rounds in steady flight.
        if (_in.ReticleTex != null && controller.Loadout != null)
        {
            controller.Reticle = ImpactReticle.Build(_in.ReticleTex, rig.Camera);
            if (verbose)
                GD.Print("gun reticle: ballistic impact point via impact_point.png");
        }

        // Visible damage: torn-skin panel flips + the authored damage-stage anims, played through
        // the rig runtime once it exists (wired below, after the runtime builds). A missing
        // program leaves DamageVisuals' geometric fallback to engage, loudly.
        if (controller.Damage != null)
        {
            PanelPairing? pairing = null;
            if (_in.CrashProgram is { } program)
            {
                var pairingDefs = new List<AnimDefinition>(program.ByAnimName("plane_reset"));
                foreach (var n in EffectCatalogue.PlayerDamageStageAnims)
                    pairingDefs.AddRange(program.ByAnimName(n));
                pairing = DamageVisuals.PanelPairingSets(pairingDefs);
            }
            controller.Visuals = new DamageVisuals(planeBuilder.DamagePanels, planeModel, stats,
                defPairing: pairing);
            if (verbose)
                GD.Print($"damage visuals: {controller.Visuals.PanelCount} panels — " +
                         "authored stage anims via the rig runtime");
        }

        // The data-driven crash rig is built AFTER the controller enters the tree
        // (below), so the crash def's reset states read valid global transforms.

        if (_in.Sounds != null && _in.SoundDefs != null)
        {
            var audio = new FlightAudio { MixGain = _in.MixGain };
            audio.Setup(_in.Sounds, _in.SoundDefs, stats, _in.SoundGroups);
            controller.Audio = audio;
            controller.AddChild(audio);
            if (verbose)
                GD.Print($"audio: engine={stats.EngineSound} (dual voice, " +
                         $"{Config.GetFloat("flightAudio.engineDetuneRatio", FlightAudio.EngineDetuneRatio) * 100f:0.#}% detune) " +
                         $"whine={stats.WhineSound} rattle={stats.RattleSound}" +
                         (_in.MixGain < 1f ? $" (per-player mix gain {_in.MixGain:0.00})" : ""));
        }
        // This player's stunt run: player 1 flies the loaded instance, everyone else an
        // independent copy of the same zones — own progress, own clock.
        if (_in.StuntZones != null)
        {
            controller.Stunt = pi == 0 ? _in.StuntZones : _in.StuntZones.ForAnotherPlayer();
            controller.Stunt.LogTag = tag; // "P2 " in a race — one shared world, four runs
            controller.DebugCompleteStunt = _spec.DebugScoreboard;
            // The objective marker HUD, one per pane: projects that player's
            // active danger zone through THEIR camera, with the edge arrow + clock
            // bearing + run status.
            controller.Marker = MarkerHud.Build(controller.Stunt, rig.Camera);
            if (_in.Race is { } race)
            {
                // Racing: no per-player splits board — the shared ranked board
                // below covers the whole window when the last pilot is in. The marker
                // HUD shows this player's placing meanwhile.
                race.Add(pi, controller.Stunt, PlaneRoster.PlaneDisplayName(stats));
                controller.Race = race;
                controller.Marker.Race = race;
                controller.Marker.PlayerIndex = pi;
            }
            else
            {
                // Solo: the end-of-run scoreboard — per-zone splits + total +
                // persisted best time, keyed chapter/mission/plane in
                // user://stunt_scores.json (race totals are deliberately not recorded).
                var scoreKey = $"{_spec.Chapter}/{_spec.Mission}/{planeName}";
                controller.Scoreboard = StuntScoreboard.Build(controller.Stunt,
                    PlaneRoster.PlaneDisplayName(stats), $"{_spec.Chapter}   ·   {PlaneRoster.Humanize(_spec.Scenario)}",
                    ScoreStore.Load(), scoreKey);
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
        if (_in.VersusMatch is { } versus)
        {
            // Rigs is the SAME list GameSession keeps live for the whole session — every seat
            // already exists (BuildRigs ran before this loop), only .Controller fills in as each
            // player assembles, so by the time this pane draws, every opponent's is populated.
            controller.VersusHud = VersusHud.Build(versus, pi, rig.Camera);
            controller.VersusHud.Rigs = _in.Rigs;
            if (verbose)
                GD.Print("dogfight HUD: match timer/K-D/leader line + kill banner + opponent markers");
        }

        // The targeting HUD: one per human pane, in EVERY flight session — not only --vs, which
        // VersusHud is. Draws this pilot's own selected target (below), falling back to the
        // nearest AI hostile on a pane with no selection; built unconditionally because
        // generators spawn hostiles mid-session, and with nothing selected and none in the pool it
        // draws nothing. A --vs pane gets one alongside VersusHud, so an AI hostile spawned into a
        // dogfight is still marked.
        controller.TargetHud = TargetHud.Build(pi, rig.Camera, _in.Projectiles);
        if (verbose)
            GD.Print("targeting HUD: selected-target marker (brackets + label, edge arrow off screen)");

        // The player's target selection: one per human pane, each with its own pool — the cycles
        // are sorted against THIS plane's pose, so they cannot be shared. GameSession binds
        // TargetSubParts later, once the zeppelins exist.
        controller.Targeting = new TargetSelection();
        controller.InitialTarget = _spec.TargetSelect;   // --target=, the scripted twin

        // Bound on EVERY pane, not just under --debug-markers: it is what the HUD's team tests read
        // this pane's side off (TargetHud.OwnTeam). Deriving the side from the pilot index instead
        // is right for P1 by coincidence and wrong for P2-P4 in any session that sets teams
        // explicitly, which is what put a wingman in the marker.
        controller.TargetHud.Own = controller;

        // --debug-markers: the same HUD marks every live aircraft instead of one hostile. Own also
        // keeps it from marking the aircraft the camera is sitting on.
        if (_spec.DebugMarkers)
        {
            controller.TargetHud.MarkAll = true;
            if (verbose)
                GD.Print("--debug-markers: marking EVERY live aircraft (red hostile / blue own side)");
        }

        // Every player's start comes from ONE call: a grid start is not decomposable, since no
        // single pilot's answer exists until every slot is known. Resolved lazily on the first
        // rig, so the caller can keep constructing the assembler before the rigs are known.
        var (spawnPos, spawnLookAt) = (_starts ??= _spawns.ChooseStarts(
            _in.SpawnList, _in.MissionZrdrPath, _in.SpawnBase, _in.RigCount))[pi];
        // The plant's force path is chosen once, here, off who is flying — a person, so the
        // player path. FlightModel.UsesAiForcePath carries why this is a construction argument
        // rather than the original's own pointer-compare-against-the-player test.
        controller.Setup(new FlightModel(stats, aiForcePath: !controller.IsHumanPiloted),
            rig.Camera, _in.CamParamsFor(planeName), spawnPos, spawnLookAt);
        // --weapon-lab: a flight session whose aircraft is pinned at the spawn pose. Set after
        // Setup, so the pin, captured at the first held sim step, takes the pose Setup just wrote.
        if (_spec.WeaponLab)
        {
            controller.Held = true;
            if (verbose)
                GD.Print($"weapon lab: P{pi + 1} held at the spawn pose (world sim running)");
        }
        // The throttle-slam exhaust smoke: needs the plane's own exhaust marker
        // nodes plus the live throttle Setup just wrote, so it builds after Setup rather than
        // alongside Props/WingLights above.
        controller.ThrottleSmoke = ThrottleSlamSmoke.Build(planeModel, _in.ZrdrPath, _in.Textures,
            controller, controller.Throttle, _in.Ambience);

        // The ambient speed cue is chapter data, not an aircraft-model effect: one private copy
        // per player so splitscreen panes do not see another pilot's ahead-of-plane wisps.
        if (!_spec.EmptyStage)
        {
            controller.SpeedCue = SpeedCue.Build(_in.ChapterZrdrPath, _in.Textures, _worldRoot,
                _in.Ambience,
                rig.VisualLayer == 0 ? null : node => SplitScreen.SetVisualLayer(node, rig.VisualLayer));
        }

        // The incoming-fire near-miss cue: this aircraft becomes a target every OTHER
        // pilot's rounds are measured against. After Setup — the target reads the live flight
        // model — and after PlayerIndex, the identity that excludes this pilot's own rounds.
        controller.AttachWarningShotCue(_in.Projectiles);
        controller.Name = $"player{pi + 1}";
        rig.Controller = controller;
        _worldRoot.AddChild(controller);

        // Data-driven crash: a per-player crash AnimRuntime playing the compiled def. Built here,
        // once the controller (and its plane model) are in the tree, so the crash def's reset
        // states resolve valid global transforms.
        if (_in.CrashProgram != null && _in.WorldScene != null)
        {
            _worldEffects.BuildFlightCrashRuntime(controller, planeBuilder, planeName, _in.Gamez,
                _in.WorldScene, _in.Textures, _in.CrashProgram, verbose);
            // The start choreography for the very first spawn: Respawn() plays this same def on
            // every later respawn, but Setup() above called Respawn() before this runtime existed.
            controller.CrashRuntime?.Play("startprops", planeModel, applyReset: false);
            // That runtime also carries the damage-stage menu, so a part crossing an
            // injure_anims threshold plays its authored def. Wired here because the runtime is
            // built after the controller joins the tree, later than DamageVisuals itself.
            if (controller.Visuals != null && controller.CrashRuntime is { } rigRuntime)
            {
                // This closure also arbitrates node ownership against other per-frame systems:
                // add a future contested case here by name, not as a generic scan.
                controller.Visuals.DamageEffectSink = anim =>
                {
                    // applyReset:false as the crash trigger does — a reset would re-pose nodes
                    // the damage state owns, not just the effect's.
                    int started = rigRuntime.Play(anim, planeModel, applyReset: false).Count;
                    // ⚠ started is instances, not emitters — PufferState events dispatch on the
                    // runtime's next tick, so sample the puffer count later, not off this delta.
                    Log.Info("anim", $"damage stage anim={anim} started={started} rig_puffers_total={rigRuntime.PuffersBuilt}");
                    // player_fuelleak's ELSE branch deactivates wing_flare2 for the rest of
                    // the leak (the def never re-activates it) — hand that lamp to the leak so
                    // WingLightBlinker's 1.5 s cycle stops re-asserting the blink over it.
                    if (anim.Equals("player_fuelleak", StringComparison.OrdinalIgnoreCase))
                        controller.WingLights?.Suspend("wing_flare2");
                };
                // ⚠ The stop must cover the CALL closure, not the played roots alone, or a
                // called-onto instance never gets its NODE_ACTIVE exit. Derived from the program
                // so no hand list can rot.
                var stageClosure = new List<string>();
                foreach (var d in _in.CrashProgram.Subset(EffectCatalogue.PlayerDamageStageAnims).Defs)
                {
                    var n = d.AnimName ?? d.Name;
                    if (!string.IsNullOrEmpty(n) && !stageClosure.Contains(n))
                        stageClosure.Add(n);
                }
                controller.Visuals.DamageEffectStop = () =>
                {
                    foreach (var n in stageClosure)
                        rigRuntime.Stop(n);
                };
            }
        }
    }

    /// <summary>The session-wide flight data every rig reads — loaded once by
    /// <c>GameSession.BuildFlightRigs</c> and shared, in contrast to the per-player nodes
    /// <see cref="Assemble"/> builds. Set once at construction; never mutated per rig.</summary>
    public sealed class Inputs
    {
        /// The aircraft models' gamez (planes.zbd, or the session gamez on the empty stage).
        public GameZ PlanesGamez = null!;
        /// This plane's stats, loaded once per distinct aircraft (splitscreen players differ).
        public Func<string, PlaneStats> StatsFor = null!;
        /// The same aircraft as the AI flies it: the player chain for everything except the damage
        /// model, which comes from the AI def (PlaneStats.LoadForAi). Cached separately
        /// from <see cref="StatsFor"/> — the two flavours of one airframe are different objects,
        /// so a single name-keyed cache would hand whichever loaded first to both.
        public Func<string, PlaneStats> AiStatsFor = null!;
        /// This plane's camera tuning, cached the same way and for the same reason.
        public Func<string, CamParams> CamParamsFor = null!;
        /// How many rigs this session flies — drives the log tags, the verbose-once lines and the
        /// single-player-only controller affordances (pause/halt).
        public int RigCount;
        /// Splitscreen own-ship mix scale (equal power across the panes).
        public float MixGain = 1f;
        /// Per-player pad binding: the join flow's, or the connected roster's.
        public int[][]? PadAssignment;
        /// One livery stream for the session, so P1..P4 draw distinct colours from it.
        public RandomNumberGenerator PaintRng = null!;
        /// The session's spawn list and the index P1 takes (each player wraps on from there).
        public List<SpawnPoint>? SpawnList;
        public int SpawnBase;
        /// The weapons catalogue, its message strings and the stock loadouts.
        public WeaponDefs WeaponDefs = null!;
        public Messages WeaponMessages = null!;
        public StockLoadouts StockLoadouts = null!;
        /// The ai.zrd turret table — null when the archive lacks ai.zrd, which builds
        /// every plane turretless rather than failing the session.
        public TurretDefs? TurretDefs;
        /// The shake-oscillator sources (shakes.json) — one load, one PlaneShake per rig.
        public ShakeDefs Shakes = null!;
        /// The one shared projectile/effect pool every player's guns fire into.
        public ProjectilePool Projectiles = null!;
        /// The game's HUD bitmap font and the reticle pipper texture — null when absent, which
        /// simply omits the readout/reticle.
        public HudFont? HudFont;
        public Texture2D? ReticleTex;
        /// The mission's danger zones (--stunt), and the shared race when several pilots fly them.
        public StuntMission? StuntZones;
        public StuntRace? Race;
        /// The dogfight match (--vs), built before this loop runs so every pane's VersusHud binds
        /// to the same instance GameSession later feeds Downed reports into.
        public VersusMatch? VersusMatch;
        /// Every rig in the session (--vs opponent markers) — the same list GameSession
        /// keeps live for the whole session, not a snapshot; see the Assemble call site.
        public IReadOnlyList<PlayerRig>? Rigs;
        /// The active Instant Action mission's player_plane node,
        /// overriding --plane= for every human alike; null outside one.
        public string? InstantActionPlayerPlaneNode;
        /// Whether an Instant Action mission is active — every human takes team 1 (Decision 8)
        /// regardless of pilot index when this is set.
        public bool InstantActionActive;
        /// <c>--coop</c>: every human takes <see cref="AimAssist.PlayerTeam"/>
        /// in a plain flight session, same as Instant Action. <see cref="SessionSpec.Resolve"/>
        /// already drops this when <c>--vs</c> is also given, so the two never race here.
        public bool Coop;

        /// The session's wind and active camera (see Effects/WorldWind.cs), for the throttle-slam
        /// exhaust and speed-cue puffers assembled here. Still air unless the session hands its own over.
        public Effects.EffectAmbience Ambience = Effects.EffectAmbience.Still;

        // The build's archives and world outputs (BuildState's, unchanged).
        public TextureArchive Textures = null!;
        public string ZrdrPath = "", ChapterZrdrPath = "", MissionZrdrPath = "";
        public GameZ Gamez = null!;
        public SceneBuilder? WorldScene;
        public AnimRuntime? WorldRuntime;
        /// The session's one world-effects runtime, so a graze plays its touchdown_* def.
        /// Null on a world-less build — the scrape then keeps its sound and loses its effect.
        public AnimRuntime? WorldEffects;
        /// The level's <c>touchdown_*</c> def vector, built alongside that runtime. Every rig
        /// indexes the same one, as the original indexes one global.
        public SurfaceDefTable? TouchdownDefs;
        public AnimProgram? CrashProgram;
        public SoundArchive? Sounds;
        public Dictionary<string, SoundDef>? SoundDefs;
        /// The SOUND_GROUPS table, for the own-ship cues whose sound is a group name rather than a
        /// def — the near-miss warning (player.json warning_shot_sound = bullet_warning_sg).
        public Dictionary<string, SoundGroup>? SoundGroups;
        public bool DebugCollision;
    }
}
