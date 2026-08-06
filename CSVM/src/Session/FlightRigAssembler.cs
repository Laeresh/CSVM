using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>Assembles one player's flight rig (PLAN-planeviewer-split C10, moved verbatim off
/// <c>GameSession.BuildFlightRigs</c>): the painted plane model, the
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
    private readonly SpawnPicker _spawns;
    private readonly WorldEffectsFactory _worldEffects;
    private readonly Node3D _worldRoot;
    private readonly Inputs _in;

    public FlightRigAssembler(SessionSpec spec, LiveryResolver liveries, SpawnPicker spawns,
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
    /// Accumulated so the caller appends it in the same place the loop used to.</summary>
    public string WhatSuffix { get; private set; } = "";

    /// <summary>Builds player <paramref name="pi"/>'s aircraft into <paramref name="rig"/> and
    /// adds it to the session world. Call once per rig in ascending player order (see the class
    /// note on the shared rng streams).</summary>
    public void Assemble(int pi, PlayerRig rig)
    {
        bool verbose = pi == 0; // the per-plane detail lines are identical for every player
        string tag = _in.RigCount > 1 ? $"P{pi + 1} " : "";
        // Each player flies their own pick (the launchscreen's join flow / a --plane= list);
        // with one name given, that is the same plane for everyone as before.
        string planeName = PlaneRoster.PlaneFor(_spec, pi);
        var stats = _in.StatsFor(planeName);

        // Flight repaints the field on every map load: each player draws their
        // own random livery (colours + decals) unless --paint pins one.
        long mark = StartupProfile.Mark();
        var planeBuilder = new PlaneBuilder(_in.PlanesGamez, _in.Textures, spinningProps: true,
            scheme: _liveries.SchemeFor(pi, _in.ZrdrPath, randomByDefault: false, _in.PaintRng,
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
            // Set here, not in the stunt block below (where it used to live): the index is this
            // pilot's identity for the rounds they fire, so free flight needs it too.
            PlayerIndex = pi,
            PlaneModel = planeModel,
            Props = PropAnimator.Build(planeModel), // spin the propeller/rotor blur discs
            WingLights = WingLightBlinker.Build(planeBuilder.WingFlares, _spec.AnimLod), // blink the wingtip flares
            Surfaces = ControlSurfaceAnimator.Build(planeModel), // deflect ailerons/elevators/rudders
            Collider = PlaneCollider.Build(planeModel), // swept airframe boxes (wingtip/tail collision)
            // per-part HP from destroyable_parts — collisions below
            // the crash threshold damage the struck part instead of crashing
            Damage = stats.DestroyableParts.Count > 0 ? new PlaneDamage(stats.DestroyableParts) : null,
            // C27: flying into a WeaponOrCollideHit object (the 44 facades/windows/agyrobus)
            // breaks it and passes through; every other collision stays solid.
            CollideDamageSink = _in.WorldRuntime != null ? _in.WorldRuntime.CollideDamageAt : null,
            // B3: a survivable scrape plays touchdown.zrd's per-surface reaction (sparks/dust/
            // splash) at the contact point, through the same runtime a rocket impact uses.
            GrazeEffectSink = _in.WorldEffects is { } fx ? (name, pt) => fx.PlayEffectAt(name, pt) : null,
            // splitscreen: this player's own device(s), own pane for the HUD,
            // and no debug freeze (it would halt the shared world for everyone)
            PadDevices = _in.PadAssignment?[pi],
            UseKeyboard = pi == 0,
            PinnedView = _spec.View,
            HudParent = rig.Viewport,
            AllowPause = _in.RigCount == 1,
        };
        controller.AddChild(planeModel);

        // Guns/hardpoints: bind this plane's stock loadout (or the --loadout override) to
        // its built model — resolves markers to muzzle nodes + weapons to WeaponDefs.
        // Set before the controller enters the tree (its _Ready builds the fire state).
        var loadoutDefName = _spec.LoadoutOverride ?? stats.DefName;
        if (_in.StockLoadouts.For(loadoutDefName) is { } ldef)
        {
            try
            {
                // The weapon lab (B4/B5) flies the FULL-RIG loadout instead: every firepoint and
                // every pylon the airframe carries, seeded from this same stock fit — so the
                // panel can mount a weapon on a hardpoint the stock file never names.
                controller.Loadout = _spec.WeaponLab
                    ? Loadout.ForRig(planeModel, _in.WeaponDefs, ldef)
                    : Loadout.Bind(ldef, planeModel, _in.WeaponDefs);
                controller.Projectiles = _in.Projectiles;
                controller.InfiniteAmmo = _spec.InfiniteAmmo;
                controller.AmmoCapOverride = _spec.AmmoCap;
                controller.AutoFire = _spec.AutoFire;
                controller.AutoFireRockets = _spec.AutoFireRockets;
                controller.InitialGunSelect = _spec.GunSelect;
                // --rocket=<wep_id>: swap every hardpoint's ordnance before the model is
                // mounted and the ordnance-type list is built (controller._Ready). A
                // testing hook — all 11 stock loadouts carry HE (wep_06), so this is the
                // only way to prove the mounted model varies by rocket type.
                if (_spec.RocketOverride != null)
                {
                    Testing.ProbeRunner.ApplyRocketOverride(controller.Loadout, _in.WeaponDefs, _spec.RocketOverride, verbose);
                }
                // D44: hang the FLYOUT-model ordnance under the pylons — one body per pylon,
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

        // The selected-weapon text readout (E36): the gun group + rocket type and their
        // live ammo, drawn in the game's HUD font from the MSG_HUD_GUNGAUGE/MSG_HUD_MISSLES
        // templates. Built whenever the font loaded and the plane carries a loadout.
        if (_in.HudFont != null && controller.Loadout != null)
        {
            controller.WeaponReadout = WeaponReadout.Build(_in.HudFont, _in.WeaponMessages);
            if (verbose)
                GD.Print("weapon readout: MSG_HUD_GUNGAUGE/MSG_HUD_MISSLES via 5pointhud font");
        }

        // The gun aiming reticle (E37): the ballistic impact point of the selected gun
        // group at the convergence distance, drawn as the game's pipper — visibly
        // trailing the nose in a hard turn, on the rounds in steady flight.
        if (_in.ReticleTex != null && controller.Loadout != null)
        {
            controller.Reticle = ImpactReticle.Build(_in.ReticleTex, rig.Camera);
            if (verbose)
                GD.Print("gun reticle: ballistic impact point via impact_point.png");
        }

        // Visible damage: torn-skin panel flips + the authored damage-stage anims
        // (player-1.zrd.json's pdpanelN / player_fuelleak / player_damage_trail menu), played
        // through the rig runtime once it exists (the sink wiring below, after the runtime builds).
        // The healthy↔torn candidate sets come from the same defs (plane_reset's re-ACTIVE list +
        // the pdpanelN targets, BL-270); a missing program leaves DamageVisuals' geometric
        // fallback to engage, loudly.
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

        var (spawnPos, spawnLookAt) = _spawns.ChooseSpawn(_in.SpawnList, _in.MissionZrdrPath, _in.SpawnBase, pi, tag);
        controller.Setup(new FlightModel(stats), rig.Camera, _in.CamParamsFor(planeName),
            spawnPos, spawnLookAt);
        // --weapon-lab (A2): the lab is a flight session whose aircraft is pinned at the spawn pose
        // — everything else (world, pool, effects, the trigger itself) runs exactly as in free
        // flight. Set AFTER Setup, which places the plane: the pin is captured at the first held
        // sim step, so it takes the spawn pose Setup just wrote.
        if (_spec.WeaponLab)
        {
            controller.Held = true;
            if (verbose)
                GD.Print($"weapon lab: P{pi + 1} held at the spawn pose (world sim running)");
        }
        // The throttle-slam exhaust smoke (CAP-21 re-read): needs the plane's own exhaust marker
        // nodes plus the live throttle Setup just wrote, so it builds after Setup rather than
        // alongside Props/WingLights above.
        controller.ThrottleSmoke = ThrottleSlamSmoke.Build(planeModel, _in.ZrdrPath, _in.Textures,
            controller, controller.Throttle);

        // The incoming-fire near-miss cue (BL-087): this aircraft becomes a target every OTHER
        // pilot's rounds are measured against. After Setup — the target reads the live flight
        // model — and after PlayerIndex, the identity that excludes this pilot's own rounds.
        controller.AttachWarningShotCue(_in.Projectiles);
        controller.Name = $"player{pi + 1}";
        rig.Controller = controller;
        _worldRoot.AddChild(controller);

        // Data-driven crash (Layer 2): a per-player crash AnimRuntime that PLAYS
        // player_crash_dirt on a crash — the wreck breaking apart, the pieceN ballistics
        // and every authored effect, from the compiled def. Built here, once the
        // controller (and its plane model) are in the tree, so the crash def's reset
        // states resolve valid global transforms. The standard (and only) crash path.
        if (_in.CrashProgram != null && _in.WorldScene != null)
        {
            _worldEffects.BuildFlightCrashRuntime(controller, planeBuilder, planeName, _in.Gamez,
                _in.WorldScene, _in.Textures, _in.CrashProgram, verbose);
            // The start choreography for the very first spawn: Respawn() plays this same def on
            // every later respawn, but Setup() above called Respawn() before this runtime existed.
            controller.CrashRuntime?.Play("startprops", planeModel, applyReset: false);
            // That runtime also carries the authored damage-stage menu (BL-259) and the
            // <part>_damage_effects shims (B4), so a part crossing an injure_anims threshold plays
            // its authored def — panel burn, fuel leak, heavy prop1 trail, spark burst. Wired here
            // because the runtime is built after the controller joins the tree, later than
            // DamageVisuals itself.
            if (controller.Visuals != null && controller.CrashRuntime is { } rigRuntime)
            {
                controller.Visuals.DamageEffectSink = anim =>
                {
                    // applyReset:false for the same reason the crash trigger passes it — a reset
                    // here would re-pose nodes the damage state owns, not just the effect's. The
                    // plane model is the fallback anchor: the menu defs' NAME (player_pfighter)
                    // resolves on no other airframe, exactly the startprops shape (C7).
                    int started = rigRuntime.Play(anim, planeModel, applyReset: false).Count;
                    // ⚠ started is instances, NOT emitters — the def's PufferState events dispatch on
                    // the runtime's NEXT tick, so a puffer-count delta taken here reads 0 no matter
                    // what renders (verification.md WORLD-12 needs the count sampled later, which is
                    // what the rig's cumulative total below does).
                    Log.Info("anim", $"damage stage anim={anim} started={started} rig_puffers_total={rigRuntime.PuffersBuilt}");
                    // BL-287: player_fuelleak's ELSE branch deactivates wing_flare2 for the rest of
                    // the leak (the def never re-activates it) — hand that lamp to the leak so
                    // WingLightBlinker's 1.5 s cycle stops re-asserting the blink over it.
                    if (anim.Equals("player_fuelleak", StringComparison.OrdinalIgnoreCase))
                        controller.WingLights?.Suspend("wing_flare2");
                };
                // The stop must cover the CLOSURE, not the played roots alone: Stop("pdpanel1")
                // cannot reach the short_firetrail instance it CALLed onto the panel, and
                // player_damage_trail's trail sits on prop1, which stays visible — its authored
                // NODE_ACTIVE exit never fires. Derived from the program so no hand list can rot.
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
        /// The one shared projectile/effect pool every player's guns fire into.
        public ProjectilePool Projectiles = null!;
        /// The game's HUD bitmap font and the reticle pipper texture — null when absent, which
        /// simply omits the readout/reticle.
        public HudFont? HudFont;
        public Texture2D? ReticleTex;
        /// The mission's danger zones (--stunt), and the shared race when several pilots fly them.
        public StuntMission? StuntZones;
        public StuntRace? Race;

        // The build's archives and world outputs (BuildState's, unchanged).
        public TextureArchive Textures = null!;
        public string ZrdrPath = "", MissionZrdrPath = "";
        public GameZ Gamez = null!;
        public SceneBuilder? WorldScene;
        public AnimRuntime? WorldRuntime;
        /// The session's one world-effects runtime, so a graze plays its touchdown_* def (B3).
        /// Null on a world-less build — the scrape then keeps its sound and loses its effect.
        public AnimRuntime? WorldEffects;
        public AnimProgram? CrashProgram;
        public SoundArchive? Sounds;
        public Dictionary<string, SoundDef>? SoundDefs;
        /// The SOUND_GROUPS table, for the own-ship cues whose sound is a group name rather than a
        /// def — the near-miss warning (player.json warning_shot_sound = bullet_warning_sg).
        public Dictionary<string, SoundGroup>? SoundGroups;
        public bool DebugCollision;
    }
}
