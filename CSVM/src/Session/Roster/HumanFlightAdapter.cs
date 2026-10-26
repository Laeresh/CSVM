using System;
using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Audio;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.World;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.Session.Roster;

/// <summary>The airframe and paint one human rig is flying, as its own assembly resolved them.
/// Read by the mission-script airframe hand-over, which gives the aeroplane the player is leaving
/// to another pilot and needs both to build it again.</summary>
public readonly record struct FlyingAirframe(string PlaneNode, PaintScheme? Scheme);

/// <summary>Assembles one player's flight rig: the painted plane model, the
/// <see cref="FlightController"/> and everything hung on it, loadout/ordnance, compass, gauges,
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
    // Arms a bot seat's AI pilot off the AI path's skill tables.
    private readonly AiFlightAssembler _botPilots;

    // What each rig is flying now, by rig index: a swap replaces the entry. The mission-script
    // hand-over gives wingman_4 the aeroplane the player is LEAVING, and nothing else records it.
    private readonly Dictionary<int, FlyingAirframe> _flying = new();

    // Every player's start, resolved in one call (see Assemble).
    private IReadOnlyList<FlightStart>? _starts;

    // Read on the first bot seat with a militia. Empty when it cannot be read, and such a bot flies
    // the stock fit in the session livery.
    private MilitiaFits? _militias;

    public HumanFlightAdapter(FlightRosterPolicy policy, LiveryResolver liveries, IFlightStarts spawns,
        WorldEffectsFactory worldEffects, Node3D worldRoot, AircraftAssemblyResources aircraft,
        FlightWorldBindings world, HumanRosterBindings human, AiFlightAssembler botPilots)
    {
        _policy = policy;
        _liveries = liveries;
        _spawns = spawns;
        _worldEffects = worldEffects;
        _worldRoot = worldRoot;
        _aircraft = aircraft;
        _world = world;
        _human = human;
        _botPilots = botPilots;
    }

    /// <summary>Mesh instances the assembled planes added, accumulated across the rigs, the
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

    /// <summary>Where every person in the field flies, this machine's panes and seats flown
    /// elsewhere alike. Every AI-flown aircraft picks its far-field plant on it, bots and roster AI
    /// alike. A bot is no person, nor is a guest who left. ⚠ Never the pane snapshot in a network
    /// match: an AI fighting a guest a kilometre from the host would fly the speed-hold plant.
    /// Outside one the panes are every person.</summary>
    public static Func<IReadOnlyList<Vector3>> PersonSeatPositions(FlightWorldBindings world, HumanRosterBindings human)
    {
        var seats = human.NetSeats;
        var rigs = human.Rigs;
        var positions = new List<Vector3>(seats.Count);
        return () =>
        {
            if (seats.Count == 0)
            {
                return world.HumanPositions?.Invoke() ?? Array.Empty<Vector3>();
            }

            positions.Clear();
            for (int i = 0; i < seats.Count && i < rigs.Count; i++)
            {
                if (!seats[i].IsBot && human.SeatLeft?.Invoke(i) != true
                    && rigs[i].Controller is { } person && GodotObject.IsInstanceValid(person))
                {
                    positions.Add(person.WorldPosition);
                }
            }

            return positions;
        };
    }

    /// <summary>What rig <paramref name="rigIndex"/> is flying, or null before its first assembly.
    /// </summary>
    public FlyingAirframe? Flying(int rigIndex) =>
        _flying.TryGetValue(rigIndex, out var flying) ? flying : null;

    /// <summary>Builds player <paramref name="pi"/>'s aircraft into <paramref name="rig"/> and
    /// adds it to the session world. Call once per rig in ascending player order (see the class
    /// note on the shared rng streams). <paramref name="swap"/> is a mid-mission airframe swap
    /// rather than the initial build: it names the airframe outright and carries the flight state
    /// of the aircraft being left, and it is the caller's job to have removed that aircraft first.
    /// </summary>
    public void Assemble(int pi, PlayerRig rig, Action<FlightController> onCreated,
        AirframeSwapRequest? swap = null)
    {
        bool verbose = pi == 0; // the per-plane detail lines are identical for every player
        string tag = _human.RigCount > 1 ? $"P{pi + 1} " : "";
        // A seat with no pane here: one flown on another machine, or a bot this host flies.
        // Everything hung on a camera, a pad or this window is skipped. The aeroplane, its paint,
        // its loadout, its damage and its spawn are built exactly as a local seat's.
        var seat = pi < _human.NetSeats.Count ? _human.NetSeats[pi] : null;
        bool paneless = seat is { HasPane: false };
        bool flownElsewhere = seat is { FlownHere: false };
        // A bot is AI-piloted on every machine, so each plays its hits, shakes and wreck alike.
        // Only the host, which flies it, gives it a pilot to steer it. Its contacts take a
        // person's rule, since its hull is a person's (FlightController.IsBotSeat).
        bool bot = seat is { IsBot: true };
        var botPilot = bot && !flownElsewhere ? new AiPilot() : null;
        // Each player flies their own pick; an Instant Action mission overrides it for every human
        // alike (HumanFieldPlanes.InstantActionOverride settles which do). A mission's own swap outranks
        // both. In a network match the roster's pick comes first: every peer builds the same field.
        string planeName = swap?.PlaneNode
            ?? (seat is { PlaneNode.Length: > 0 } ? seat.PlaneNode : null)
            ?? _human.InstantActionPlayerPlaneNode
            ?? (_policy.PlaneNames.Count == 0
                ? _policy.PlaneName
                : _policy.PlaneNames[Math.Min(pi, _policy.PlaneNames.Count - 1)]);
        var stats = _aircraft.StatsFor(planeName);
        // A bot's militia paints and arms it on every machine; its airframe is still a person's.
        var militia = bot ? Mech3.InstantAction.MilitiaAt(seat!.Militia) : null;
        string? militiaPattern = militia is { } painted ? Militias().PatternFor(painted.Name) : null;
        string? militiaDef = militia is { } armed ? Militias().DefFor(armed.Name, planeName) : null;

        // The plane this pilot BUILT, when they picked one: its guns, pylons, paint and armour
        // replace the airframe's stock ones below. Null on a stock pick, empty outside a
        // launchscreen launch; a swap brings its own or none (see AirframeSwapRequest).
        var custom = swap != null ? swap.Build : CustomPlaneFor(pi);
        string planeDisplay = custom?.Name is { Length: > 0 } customName
            ? customName
            : PlaneRoster.PlaneDisplayName(stats);

        // The engine pick, the original's registry override: the authored engines.json tier row
        // replaces the airframe's stock EnginePower on a COPY (the stats object is the shared
        // per-airframe cache). Stock pick (id 6) keeps the airframe's own row.
        if (custom != null
            && Flight.Hangar.CustomPlaneBuild.EnginePowerFor(_aircraft.ZrdrPath, custom) is { } enginePower)
        {
            stats = stats.WithEnginePower(enginePower);
        }

        // Every player flies the Fortune Hunters livery unless --paint says otherwise, as the
        // original's stock planes do. A custom plane wears the paint it was built with, and a bot its
        // militia's. A swap's ShippedSkins reading wins over all of them, null included.
        long mark = StartupProfile.Mark();
        // cockpitInterior: a human rig is the only one whose pilot can look out of a cockpit
        // FlightRoster's AI builder deliberately does not ask for one.
        var scheme = swap is { ShippedSkins: true } capturedSwap
            ? capturedSwap.Scheme
            : swap?.Scheme
                ?? (militia is { Name: not Mech3.InstantAction.PlayerMilitia } && !_liveries.PaintRequested
                    ? (militiaPattern == null ? null : _liveries.CatalogScheme(_aircraft.ZrdrPath, militiaPattern))
                    : custom != null && !_liveries.PaintRequested
                    ? Flight.Hangar.CustomPlaneBuild.PaintFor(custom, Flight.Hangar.CustomPlaneBuild.PatternName(custom.PaintPattern))
                    : _liveries.SchemeFor(pi, _aircraft.ZrdrPath, _aircraft.PaintRng,
                        _liveries.PatternsForPlane(_aircraft.PlanesGamez, planeName)));
        _flying[pi] = new FlyingAirframe(planeName, scheme);
        // The stereo interior's per-plane scale goes on the mount before XrCockpit captures it.
        bool xrInterior = _policy.XrInterior && !paneless;
        var vrCockpit = xrInterior ? _policy.XrCockpits.For(planeName) : VrCockpit.Default;
        bool ghostSelf = _policy.DebugRaceGhost && !paneless;
        var planeBuilder = new PlaneBuilder(_aircraft.PlanesGamez, _aircraft.Textures, spinningProps: true,
            scheme: scheme, patterns: _liveries.Patterns, cockpitInterior: true, dockingHook: true,
            raceGhost: _world.Racing || ghostSelf, xrInterior: xrInterior, interiorScaleFactor: vrCockpit.InteriorScaleFactor);
        var planeModel = planeBuilder.Build(planeName);
        var bodyCockpit = planeBuilder.Cockpit;
        // ponytail: none under the stereo path, whose lift draws the copies' flat shaders at the world
        // origin. None in a body cockpit, whose outside parts are the real ones in view. Bound before
        // the ghost stamp, so the copies wear their originals' stamp.
        var reseat = xrInterior || bodyCockpit is { FromBody: true } ? null
            : CockpitReseat.Bind(planeModel, planeBuilder.CockpitInterior, planeName);

        // A race pilot draws as a ghost to every camera but its own pilot's, the cameras that drop
        // its first-person layer. A seat flown elsewhere has no camera here, so it names a layer
        // every camera keeps; the four-wide band then serves this machine's seats alone.
        if (ghostSelf)
            RaceGhost.Stamp(planeModel, SplitScreen.EveryCameraLayer);
        else if (_world.Racing)
            RaceGhost.Stamp(planeModel, paneless ? SplitScreen.EveryCameraLayer : SplitScreen.FirstPersonLayer(pi));
        StartupProfile.Record("plane", mark);
        MeshInstances += planeBuilder.MeshInstanceCount;
        // A flat body cockpit's pass draws twins of the body. Made after the ghost stamp and before the
        // stores hang, so neither reaches them; they stay hidden unless a pass mirrors them.
        FlatBodyTwins? flatBody = null;
        if (!xrInterior && !paneless && planeBuilder.CockpitPanel is { } flatPanel)
            flatBody = new(planeBuilder.CockpitCameraOffset, planeBuilder.FlatBody(planeModel, flatPanel, bodyCockpit!.Hide));

        var controller = new FlightController
        {
            DebugCollision = _world.DebugCollision,
            PinnedViewMode = _policy.ViewMode,
            Look =
            {
                PinnedView = _policy.View,
                AutoHeadTurn = _policy.AutoHeadTurn,
                PinnedLook = _policy.PinnedLook,
            },
            HudParent = paneless ? null : rig.ChromeParent ?? rig.Viewport,
            // Each part is null when the airframe ships no cockpit1, and the rig then dresses nothing.
            Dressing =
            {
                Visibility = CockpitVisibility.Bind(planeModel, planeBuilder.CockpitInterior, pi, reseat?.Hidden, bodyCockpit,
                    planeBuilder.CockpitPanel),
                Interior = flatBody != null ? planeBuilder.CockpitPanel : planeBuilder.CockpitInterior,
                Body = flatBody,
                Reseat = reseat,
                Panel = CockpitGauges.Bind(planeBuilder),
                // A body cockpit lifts its panel, at real scale; the per-plane eye offset still seats the eye.
                Stereo = !xrInterior ? null
                    : planeBuilder.CockpitPanel is { } panel
                    ? XrCockpit.Body(panel, vrCockpit.EyeOffsetInAircraft, planeBuilder.CockpitCameraOffset)
                    : planeBuilder.CockpitInterior is { } interior
                    ? new XrCockpit(interior, vrCockpit.EyeOffsetInAircraft, planeBuilder.InteriorEye,
                        vrCockpit.StoreOffsetInAircraft, vrCockpit.StoreSpread)
                    : null,
            },
            Scheme = scheme,
            Painter = planeBuilder.Painter,
            ShippedSkins = swap is { ShippedSkins: true },
            Racing = _world.Racing,
        };
        if (verbose && controller.Dressing.Visibility is { BodyCockpit: true })
            Log.Info("flight", $"cockpit: '{planeName}' body cockpit, the model pack's airframe seen from the cockpit_camera marker, {bodyCockpit!.Hide.Count} hide name(s), {bodyCockpit.Gauges.Count} gauge table entr(ies), {flatBody?.Twins.Count ?? 0} airframe mesh(es) twinned for the cockpit pass{(controller.Dressing.Panel == null ? ", the screen-space dials kept" : string.Empty)}");
        else if (verbose && controller.Dressing.Visibility != null)
            Log.Info("flight", $"cockpit: '{planeName}' interior built hidden at the cockpit_camera marker");
        // A twinned body mesh leaves the pilot's pane while its stereo twin draws in its place.
        if (controller.Dressing is { Stereo: { } stereoBody, Visibility: { } seen } && planeBuilder.StereoBody.Count > 0)
        {
            foreach (var pair in planeBuilder.StereoBody)
            {
                stereoBody.Mirror(pair);
                seen.Reseat(pair.Original);
            }

            Log.Info("flight", $"cockpit: '{planeName}' stereo body cockpit at real scale, {planeBuilder.StereoBody.Count} airframe mesh(es) drawn from the XR origin's frame, eye {vrCockpit.EyeOffsetZ:0.###} m aft and {vrCockpit.EyeOffsetY:0.###} m up of the marker, gunsight reticle collimated ({planeBuilder.GunsightReticles})");
        }
        else if (controller.Dressing.Stereo != null)
            Log.Info("flight", $"cockpit: '{planeName}' stereo interior at {vrCockpit.InteriorScale:0.####} m per unit, eye {vrCockpit.EyeOffsetZ:0.###} m aft and {vrCockpit.EyeOffsetY:0.###} m up of the marker, head plate left out ({planeBuilder.InteriorOmittedPolygons} polygons), gunsight reticle collimated ({planeBuilder.GunsightReticles})");
        // The recenter action, for the seat the headset flies.
        controller.RecenterHeadset = paneless ? null : _human.RecenterHeadset;
        // The headset's own views, for the seat the headset flies.
        controller.Headset = xrInterior ? _policy.XrChase : null;
        // The engine pick's nitrous bit (ids 3-5) installs the injector, the original's veh+0x946.
        controller.Nitro.Installed = custom != null && Flight.Hangar.CustomPlaneBuild.HasNitrous(custom);
        // The graphics-mode action, for a seat somebody sits at here, like the pause key below.
        controller.ToggleGraphicsMode = paneless ? null : _human.ToggleGraphicsMode;
        // Every human joins team 1 in an Instant Action mission, splitscreen included, the
        // per-pilot team fallback would otherwise collide with an enemy's. --coop asks the same
        // in plain flight; SessionSpec.Resolve already drops Coop when --vs is set.
        controller.Bind(new FlightControllerBuild
        {
            PlayerIndex = pi,
            // The keymap file and the sticks are this machine's player's, not the roster seat's.
            LocalPlayer = MenuSeatOf(pi),
            IsHumanPiloted = !bot,
            IsBotSeat = bot,
            Pilot = botPilot,
            // A seat flown elsewhere takes its pose out of this history, not a flight model.
            // The buffer's presence IS that ownership, so it is built here and nowhere else.
            // The session fills it from the samples that seat's owner sends.
            RemotePoses = flownElsewhere ? new Net.RemotePoseBuffer() : null,
            // One scripted sequence per player ('|'-separated); the last covers the rest. A bot is
            // flown by its pilot, which a hold would replace.
            HoldSegments = bot || _policy.HoldSets == null ? null
                : _policy.HoldSets[Math.Min(pi, _policy.HoldSets.Length - 1)],
            LeverSteps = _policy.LeverSteps,
            PlaneModel = planeModel,
            Props = PropAnimator.Build(planeModel),
            WingLights = WingLightBlinker.Build(planeBuilder.WingFlares, _policy.AnimLod),
            Surfaces = ControlSurfaceAnimator.Build(planeModel),
            Collider = PlaneCollider.Build(planeModel),
            // A custom plane's four bought zones stand in for the airframe's stock ARMOUR pools,
            // on those pools' own scale; structure stays the def's and the vehicle totals stay
            // the sum over zones. Nothing scales a human rig's pools, so the player never is.
            Damage = stats.DestroyableParts.Count == 0 ? null
                : custom != null ? Flight.Hangar.CustomPlaneBuild.DamageFor(stats, custom)
                : PlaneDamage.For(stats),
            CollideDamageSink = _world.WorldRuntime != null ? _world.WorldRuntime.CollideDamageAt : null,
            GrazeEffectSink = _world.WorldEffects is { } fx ? (name, pt) => fx.PlayEffectAt(name, pt) : null,
            TouchdownDefs = _world.TouchdownDefs,
            Projectiles = _world.Projectiles,
            HumanPositions = botPilot != null ? _botPilots.PersonPositions : _world.HumanPositions,
            // ⚠ Pass the null through. Null and empty are DIFFERENT bindings to Pads.For: null
            // reads every connected pad (what AssignPads returns for one player), empty reads none.
            // Coalescing flew a single player pad-dead; a remote seat takes empty, it reads none.
            PadDevices = paneless ? Array.Empty<int>() : PadsOf(MenuSeatOf(pi)),
            // The keyboard is this machine's first seat's, which on a guest is not seat 0.
            UseKeyboard = !paneless && MenuSeatOf(pi) == 0,
            MouseCaptureAllowed = !paneless && _policy.MouseCaptureAllowed,
            // The whole message table, not just the weapon rows: the pilot HUD words its
            // auto-land prompt out of the same file.
            Strings = _aircraft.WeaponMessages,
            // A pause key belongs to a seat somebody is sitting at. A remote pilot's pause is
            // their own machine's business, and must not halt this one's simulation.
            AllowPause = !paneless,
            // A team Dogfight's seat flies its lobby team, banded clear of every authored id.
            Team = _human.InstantActionActive || _human.Coop ? AimAssist.PlayerTeam
                : AimAssist.LobbyTeam(seat?.TeamId ?? 0),
            Shake = new PlaneShake(_aircraft.Shakes),
        });
        // The two gun aids, read on a human seat alone: a bot's rounds take its gunner's lead.
        controller.LeadingAimAssist = _policy.LeadingAimAssist;
        controller.LeadMarker = _policy.LeadMarker;
        onCreated(controller);

        // Guns/hardpoints: bind this plane's stock loadout (or the --loadout override) to
        // its built model, resolves markers to muzzle nodes + weapons to WeaponDefs.
        // Set before the controller enters the tree (its _Ready builds the fire state).
        var loadoutDefName = _policy.LoadoutOverride ?? stats.DefName;
        var militiaStats = militiaDef != null && !_world.Racing && _policy.LoadoutOverride == null
            ? _aircraft.AiStatsFor(planeName, militiaDef) : null;
        var arms = BotSeats.ArmsFor(_world.Racing, _policy.LoadoutOverride != null, militiaStats is { AiWeapons.Count: > 0 });
        // A race seat flies the stock branch below. Its rounds reach structures alone, since no
        // aircraft in a race is in the pool's reach (FlightController.Shootable).
        if (arms == SeatArms.MilitiaDef)
        {
            // A bot carries its militia def's own weapons block, as that militia's campaign aircraft do.
            try
            {
                controller.InfiniteAmmo = _policy.InfiniteAmmo;
                controller.Loadout = Loadout.BindAi(militiaStats!.AiWeapons, militiaDef!, planeModel, _aircraft.WeaponDefs);
                AiFlightAssembler.HangLoadout(controller, _world);
                Log.Info("flight", $"{tag}weapons: bot seat {pi} flies {militia!.Value.Name}'s '{militiaDef}' fit");
            }
            catch (Exception e)
            {
                Log.Warn("weapons", $"weapons: loadout bind failed for '{militiaDef}': {e.Message}");
            }
        }
        else if (_aircraft.StockLoadouts.For(loadoutDefName) is { } stockDef)
        {
            // A custom plane's guns and hardpoints replace the stock ones and the Ammo Selection
            // layer composes over THAT (the built def leaves WeaponId null so a picked ammo still
            // resolves). --loadout= names a def outright, so it takes the whole fit either way.
            var baseDef = custom != null && _policy.LoadoutOverride == null
                ? Flight.Hangar.CustomPlaneBuild.LoadoutFor(custom, stockDef)
                : stockDef;
            var ldef = MenuFitFor(pi, swap) is { } choice ? choice.ApplyTo(baseDef) : baseDef;
            try
            {
                // The weapon lab flies the FULL-RIG loadout instead: every firepoint and
                // every pylon the airframe carries, seeded from this same stock fit, so the
                // panel can mount a weapon on a hardpoint the stock file never names.
                controller.Loadout = _policy.WeaponLab
                    ? Loadout.ForRig(planeModel, _aircraft.WeaponDefs, ldef)
                    : Loadout.Bind(ldef, planeModel, _aircraft.WeaponDefs);
                // The gun aim assist's structure candidates (B4): the world's
                // destructibles, when this session built a world at all.
                controller.Destructibles = _world.WorldRuntime?.Destructibles;
                // The HUD bracket's and the gun aim assist's vehicle candidates, on the same
                // list the aircraft roster feeds: the mission's surface vehicles, when this
                // session built any.
                controller.SurfaceVehicles = _world.SurfaceVehicles;
                controller.InfiniteAmmo = _policy.InfiniteAmmo;
                controller.AmmoCapOverride = _policy.AmmoCap;
                controller.AutoFire = _policy.AutoFire;
                controller.AutoFireRockets = _policy.AutoFireRockets;
                controller.InitialGunSelect = _policy.GunSelect;
                // --rocket=<wep_id>: swap every hardpoint's ordnance before the model is mounted.
                // A testing hook, all 11 stock loadouts carry HE, so this is the only way to
                // prove the mounted model varies by rocket type.
                if (_policy.RocketOverride != null)
                {
                    controller.Loadout.ApplyRocketOverride(_aircraft.WeaponDefs, _policy.RocketOverride, verbose);
                }
                // Hang the FLYOUT-model ordnance under the pylons, one body per pylon,
                // hidden as its ammo depletes. Uses the same gamez prototype the round flies.
                controller.Ordnance = PylonOrdnance.Build(controller.Loadout, _world.Projectiles, controller.InfiniteAmmo,
                    controller.Dressing.Stereo, controller.Dressing.Visibility);
                if (verbose)
                {
                    int groups = 0;
                    foreach (var _ in controller.Loadout.FirableGuns) { groups++; }
                    Log.Info("flight", $"weapons: {groups} gun group(s), {controller.Loadout.Hardpoints.Count} hardpoint(s), guns=Space/pad-B rockets=F/pad-A, select guns=G/dpad-R rockets=H/dpad-L{(_policy.GunSelect != 0 ? $" [gun-select={_policy.GunSelect}]" : "")}{(_policy.InfiniteAmmo ? " (infinite ammo)" : "")}{(_policy.AmmoCap != null ? $" (--ammo={_policy.AmmoCap})" : "")}");
                    if (controller.Ordnance is { } ord)
                    {
                        Log.Info("flight", $"pylon ordnance: {ord.Count} mounted rocket model(s){(_policy.RocketOverride != null ? $" (--rocket={_policy.RocketOverride})" : "")}");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("weapons", $"weapons: loadout bind failed for '{loadoutDefName}': {e.Message}");
            }
        }
        else if (verbose)
        {
            Log.Info("flight", $"weapons: no stock loadout for '{loadoutDefName}', unarmed");
        }

        // The carried turret gunners: the vehicle def's thirdp turrets block resolved
        // by TITLE against ai.zrd and by node against this built model. Independent of the
        // stock loadout, the gunner's weapon comes from its ai.zrd row, not from a gun slot.
        if (!_world.Racing && _aircraft.TurretDefs is { } turretDefs && stats.TurretMounts.Count > 0)
        {
            // ⚠ A human's carried gunner IS positional, unlike the pilot's own forward guns: the
            // original's turret path hands its sound slot a world position whoever owns the mount.
            controller.Turrets = TurretController.BuildCarried(
                turretDefs, stats, planeModel, _aircraft.WeaponDefs, controller, _world.Projectiles,
                new GunVoiceHome(controller, _world.Sounds, _world.SoundDefs, _world.HumanPositions));
            if (verbose && controller.Turrets.Length > 0)
            {
                var descs = new List<string>();
                foreach (var t in controller.Turrets)
                    descs.Add($"{t.Def.Title} ({t.Weapon.Id}, {t.Firepoints.Length} muzzle(s))");
                Log.Info("flight", $"turrets: {string.Join(", ", descs)}");
            }
        }
        if (verbose && controller.Props != null)
            Log.Info("flight", $"props: {controller.Props.Count} spinning blur nodes");
        if (verbose && controller.WingLights != null)
            Log.Info("flight", $"wing lights: {controller.WingLights.Count} blinking flares");
        if (verbose && controller.Surfaces != null)
            Log.Info("flight", $"control surfaces: {controller.Surfaces.Count} deflecting nodes");
        if (controller.Collider != null)
        {
            if (verbose)
                Log.Info("flight", $"plane collider: {controller.Collider.Summary}");
        }
        else
        {
            Log.Warn("flight", $"no airframe collision boxes, falling back to the center ray");
        }
        if (verbose && controller.Damage != null)
        {
            var partDescs = new List<string>();
            foreach (var p in stats.DestroyableParts)
                partDescs.Add(Log.Format($"{p.Name} {p.MaxHp:0}hp{(p.Critical ? "*" : "")}{(p.Engine ? " engine" : "")}"));
            Log.Info("flight", $"damage parts: {string.Join(", ", partDescs)} (* = critical)");
        }
        if (custom != null)
        {
            // Names what the build reached.
            Log.Info("flight", $"{tag}custom plane: '{custom.Name}' on {planeName}, armour {custom.ArmourNose}/{custom.ArmourTail}/{custom.ArmourLeftWing}/{custom.ArmourRightWing} units x{Flight.Hangar.CustomPlaneBuild.ArmourUnitScale}, hardpoints {custom.LeftHardpoints}+{custom.RightHardpoints}, engine {custom.Engine} thrust={stats.EnginePower:0.###}{(controller.Nitro.Installed ? " (nitrous injector)" : "")}");
        }

        // Every readout this pane draws for its pilot belongs to the controller's own FlightHud,
        // which owns the per-frame feed; nothing here writes one after assembly. None of it is
        // built for a remote seat: there is no pane to draw it in and no camera to aim it by.
        var pilotHud = controller.PilotHud;
        if (!paneless)
        {
            // The original's heading tape, rebuilt from the chapter's own HUD
            // textures (compassticks2/compasstxt ship in every chapter's archive).
            pilotHud.Compass = CompassTape.Build(_aircraft.Textures);
            if (verbose && pilotHud.Compass != null)
                Log.Info("flight", $"compass: heading tape from compassticks2/compasstxt");

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
                    Log.Info("flight", $"gauges: altimeter/speedometer/damage dial from the plane's gauges subtree");
            }

            // The bitmap-font proof overlay: draw the sample string on this pane so a 1P view
            // and a 4P pane can be compared (--hud-font-test). Set before the controller
            // enters the tree, its _Ready adds this to the HUD canvas.
            if (_aircraft.HudFont != null && _policy.HudFontTest)
            {
                pilotHud.FontTest = new HudFontTest(_aircraft.HudFont, _policy.HudFontTestText);
                if (verbose)
                    Log.Info("flight", $"hud-font-test: '{_policy.HudFontTestText}' via 5pointhud font");
            }

            // The gun aiming reticle: the ballistic impact point of the selected gun
            // group at the convergence distance, drawn as the game's pipper. It visibly
            // trails the nose in a hard turn, and sits on the rounds in steady flight.
            if (_aircraft.ReticleTex != null && controller.Loadout != null)
            {
                pilotHud.Reticle = ImpactReticle.Build(_aircraft.ReticleTex, rig.Camera);
                if (verbose)
                    Log.Info("flight", $"gun reticle: ballistic impact point via impact_point.png");
            }
        }

        // Visible damage, phase 1: the object, unconditionally. Phase 2 (the sink and the stops)
        // is wired by WorldEffectsFactory.BuildFlightCrashRuntime, since only that half needs a
        // live rig runtime.
        if (controller.Damage != null)
        {
            controller.Visuals = BuildDamageVisuals(planeBuilder, planeModel, stats, _world.CrashProgram);
            if (verbose)
                Log.Info("flight",
                    $"damage visuals: {controller.Visuals.PanelCount} panels, authored stage anims via the rig runtime");
        }

        // The data-driven crash rig is built AFTER the controller enters the tree
        // (below), so the crash def's reset states read valid global transforms.

        // Never on a remote seat. This stack is the sound of the aeroplane you are sitting in, and
        // that pilot is sitting in theirs, on their own machine.
        if (!paneless && _world.Sounds != null && _world.SoundDefs != null)
        {
            var audio = new FlightAudio { MixGain = _human.MixGain, VoiceDuck = _world.VoiceDuck };
            audio.Setup(_world.Sounds, _world.SoundDefs, stats, _aircraft.WeaponDefs, _world.SoundGroups);
            controller.Audio = audio;
            controller.AddChild(audio);
            if (verbose)
                Log.Info("flight", $"audio: engine={stats.EngineSound} damaged={stats.DamagedEngineSound ?? "none"} whine={stats.WhineSound ?? "none (no def names prop_sound)"} rattle={stats.RattleSound}{(_human.MixGain < 1f ? $" (per-player mix gain {_human.MixGain:0.00})" : "")}");
        }
        // Each seat's own run, player 1 on the loaded instance. Never on a swap, which would stack a
        // second run HUD (AirframeSwapRequest). Never on a remote seat: its own machine times it.
        if (swap == null && !paneless && _human.StuntZones != null)
        {
            var run = pi == 0 ? _human.StuntZones : _human.StuntZones.ForAnotherPlayer();
            controller.Stunt = run;
            run.LogTag = tag; // "P2 " in a race, one shared world, four runs
            controller.DebugCompleteStunt = _policy.DebugScoreboard;
            // This pilot's own unflown zones, on their own target cycle. Bound per controller
            // rather than through FlightRoster's roster-wide channel: each pane races its own copy
            // of the run, and a shared feed would put one pilot's cleared zones on another's HUD.
            controller.TargetObjectives = into => run.CollectTargets(into);
            // This pane's Danger Zone camera, photographing through the pilot's posed eye. A run
            // with no window to draw it in reads this seat's own pane instead, so in splitscreen
            // that shot is the SubViewport that crossed the marker, never the window.
            var pane = rig.Viewport;
            var capture = new StuntCapture(run, _policy.Chapter,
                landed => controller.Photograph is { } eye && DangerZonePhotograph.Drawable
                    ? eye.Request(landed)
                    : PaneReadback.Request(pane ?? (controller.IsInsideTree() ? controller.GetViewport() : null), landed));
            capture.Sting = () => controller.Audio?.OnDangerZoneCamera();
            controller.StuntShots = capture;

            // The run HUD, one per pane: clock, zones cleared, banners. The zone MARKER is the
            // targeting HUD's, since a zone is an objective like any other.
            var runHud = StuntRunHud.Build(run);
            runHud.Count = controller.StartCount;
            pilotHud.StuntRun = runHud;
            // A run starts, and reruns, behind the count; the first one begins once Setup below
            // has placed the spawn it walks to.
            if (_human.RerunCount is { } count)
            {
                controller.RerunCount = count;
                controller.Audio?.BindStartCount(_human.MenuSounds);
            }
            var scoreKey = $"{_policy.Chapter}/{_policy.Mission}/{custom?.Name ?? planeName}";
            if (_human.Race is { } race)
            {
                // Racing: no per-player splits board. The race's shared board covers the whole
                // window when it ends, in Instant Action too, and the run HUD carries the live
                // leaderboard meanwhile. The race hears this run's own clock, zones and finish.
                JoinRace(race, pi, planeDisplay, seat, run, scoreKey);
                runHud.Race = race;
                runHud.PlayerIndex = pi;
                // The pane's held scores stand across the top, so the two top lines step aside.
                runHud.StatusHiddenWhile = () => controller.ScoresShown;
                controller.Race = race;
            }
            else if (_human.InstantActionActive)
            {
                // Instant Action carries the splits on its own wrap-up board instead, so the two
                // results boards cannot wake on the same event and stack (BL-358).
            }
            else if (_human.StuntBoard is { } stuntBoard)
            {
                // Solo: the end-of-run scoreboard, per-zone splits + total + persisted best time,
                // keyed chapter/mission/plane in user://stunt_scores.json, the key a race run
                // records under too.
                controller.Scoreboard = stuntBoard(controller.Stunt, planeDisplay,
                    $"{_policy.Chapter}   ·   {PlaneRoster.Humanize(_policy.Scenario)}",
                    scoreKey, controller.Rerun, capture);
                Log.Info("flight", $"stunt scoreboard: splits + best time (key '{scoreKey}')");
            }
            if (verbose)
            {
                Log.Info("flight", $"stunt run HUD: clock + zones cleared + banners; zones ride the target cycle");
                WhatSuffix += $" [stunt: {controller.Stunt.TotalCount} zones]";
            }
        }
        else if (swap == null && botPilot != null && _human.StuntZones != null && _human.Race is { } botRace)
        {
            // A bot this machine flies runs its own copy of the course, timed here as a local seat's
            // run is, behind the same count. It records no best of its own.
            var run = _human.StuntZones.ForAnotherPlayer();
            run.LogTag = tag;
            controller.Stunt = run;
            controller.Race = botRace;
            controller.RerunCount = _human.RerunCount;
            // Nobody presses R for it, so a crash returns it to its last zone unattended.
            controller.AutoRespawnAfter = AircraftLifecycle.AutoRespawnDelay;
            JoinRace(botRace, pi, planeDisplay, seat, run);
        }
        else if (swap == null && paneless && _human.StuntZones != null && _human.Race is { } remoteRace)
        {
            // A seat flown elsewhere still races on every board here. It runs no course on this
            // machine: its own machine times it, and the host's line feeds this record.
            JoinRace(remoteRace, pi, planeDisplay, seat, run: null);
        }

        // Every versus mode (--vs): the per-pane match timer/K-D/leader line, bound to the match
        // GameSession built before this loop ran.
        if (!paneless && _human.VersusMatch is { } versus)
        {
            controller.VersusStatusLine = VersusStatusLine.Build(versus, pi);
            controller.VersusStatusLine.StatusHiddenWhile = () => controller.ScoresShown;
            var seats = _human.NetSeats;
            controller.VersusStatusLine.BotName = s => Net.NetSeats.BotCallsign(seats, s);
            if (verbose)
                Log.Info("flight", $"versus HUD: match timer/K-D/leader line");
        }

        // One per human pane, in EVERY flight session: built unconditionally because generators
        // spawn hostiles mid-session, and it draws nothing with an empty pool.
        // A remote seat picks its own targets on its own machine, so it takes neither.
        if (!paneless)
        {
            var targetHud = TargetHud.Build(pi, rig.Camera, _world.Projectiles);
            pilotHud.TargetHud = targetHud;
            targetHud.SpyglassOn = _policy.SpyglassOn;
            // The spyglass's range gate reads this pane's own fog band; null on a bare rig.
            var fogRange = _world.FogRange;
            int paneIndex = rig.Index;
            targetHud.FogRange = fogRange == null ? null : () => fogRange(paneIndex);
            if (verbose)
                Log.Info("flight", $"targeting HUD: selected-target marker (brackets + label, edge arrow off screen)");

            // The player's target selection: one per human pane, each with its own pool. The
            // cycles are sorted against THIS plane's pose, so they cannot be shared. GameSession
            // binds the sub-part feed later, once the zeppelins exist.
            controller.Targeting = new TargetSelection { NearestAfterKill = _policy.NearestAfterKill };
            controller.InitialTarget = _policy.TargetSelect;   // --target=, the scripted twin

            // ⚠ Bind on EVERY pane, not only under --debug-markers. It is what TargetHud.OwnTeam
            // reads this pane's side off, and the pilot-index derivation it falls back to is the
            // wingman-in-the-marker bug.
            targetHud.Own = controller;
            // A race labels every other pilot with the marker's name line, none of them a target.
            targetHud.RaceMarks = _world.Racing;

            // --debug-markers: the same HUD marks every live aircraft instead of one hostile. Own also
            // keeps it from marking the aircraft the camera is sitting on.
            if (_policy.DebugMarkers)
            {
                targetHud.MarkAll = true;
                if (verbose)
                    Log.Info("flight", $"--debug-markers: marking EVERY live aircraft (red hostile / blue own side)");
            }
        }

        // Every player's start comes from ONE call: a grid start is not decomposable, since no
        // single pilot's answer exists until every slot is known. Resolved lazily on the first rig,
        // so the caller can construct the assembler first. A swap brings its own instead.
        var start = swap?.Start ?? (_starts ??= _spawns.ChooseStarts(
            _human.SpawnList, _world.MissionZrdrPath, _human.SpawnBase, _human.RigCount))[pi];
        // The plant's force path is chosen once, here, off who is flying: a person takes the player
        // path and a bot the AI one. FlightModel.UsesAiForcePath carries why this is a construction
        // argument rather than the original's own pointer-compare-against-the-player test.
        var camParams = _aircraft.CamParamsFor(planeName);
        // A remote seat passes no camera, the same null an AI rig passes. The chase rig, the head
        // look and every camera write inside Setup are then not built at all.
        controller.Setup(new FlightModel(stats, aiForcePath: !controller.IsHumanPiloted),
            paneless ? null : rig.Camera, camParams, start.Pos, start.LookAt,
            start.ThrottleFrac, start.SpeedMps, cockpitCameraOffset: planeBuilder.CockpitCameraOffset);
        // A bot holds the course it spawned on until its gunner finds a quarry. Armed before the
        // tree takes it, since its first step reads the gunner and the mode machine.
        if (botPilot != null)
        {
            botPilot.TargetHeadingDeg = AiPilot.HeadingDegOf(start.LookAt - start.Pos);
            botPilot.TargetAltitude = start.Pos.Y;
            _botPilots.ArmSeatPilot(botPilot, stats, planeName, controller.Team, seat!.Skill, arms);
            // A world AI's roster logs every mode change and a bot's logs none. The lay-off assist
            // is logged alone, the one change a sortie's log is read for: whether bots ease off people.
            if (botPilot.Machine is { } modes)
            {
                string callsign = seat.Callsign;
                modes.ModeChanged += (from, to, why) =>
                {
                    if (from == AiMode.LayOff || to == AiMode.LayOff)
                        Log.Info("flight", $"bot lay-off: seat {pi} '{callsign}':{AiModeMachine.NameOf(from)} -> {AiModeMachine.NameOf(to)} ({why}) t={GameClock.Current?.Time ?? 0.0:0.00}");
                };
            }
            // Out of rockets or low on hull, it breaks off to a base; the match's base runtime runs the order.
            botPilot.RearmOrder = new AiRearmOrder(controller.WorldBlocksLine);
            controller.ArmSpawnTimers();
            // Every later return, whatever placed it, starts the pilot over on the new placement's
            // course and lever. No quarry, mode or course outlives the aeroplane it lost, and the
            // airframe takes the collision window its first spawn took.
            controller.Respawned = () =>
            {
                var at = controller.WorldPosition;
                botPilot.ResetForSpawn(at, at + controller.NoseDirection, controller.Throttle);
                controller.ArmSpawnTimers();
            };
        }
        if (controller.RerunCount != null && _human.FirstStartCount is { } firstCount)
            controller.BeginStartCount(firstCount); // a run's first start is a start like any other
        // The Danger Zone eye, framed off the airframe's own chase distance and aimed at the pose
        // the controller draws, which is the controller node's own transform.
        if (!paneless)
        {
            var scatter = Rng.Stream(Rng.Photograph);
            controller.Photograph = DangerZonePhotograph.Build(rig.Camera,
                () => controller.GlobalTransform, camParams.Dist, scatter.Randf, airframe: controller);
            controller.AddChild(controller.Photograph);
        }
        // --weapon-lab: a flight session whose aircraft is pinned at the spawn pose. Set after
        // Setup, so the pin, captured at the first held sim step, takes the pose Setup just wrote.
        if (_policy.WeaponLab)
        {
            controller.Held = true;
            if (verbose)
                Log.Info("flight", $"weapon lab: P{pi + 1} held at the spawn pose (world sim running)");
        }
        // The exhaust smoke hangs off the plane's own exhaust marker nodes, one trail per marker.
        controller.ExhaustSmoke = ExhaustSmoke.Build(planeModel, _aircraft.Textures, controller,
            _world.Ambience);

        // The ambient speed cue is chapter data, not an aircraft-model effect. One private copy
        // per player, so splitscreen panes do not see another pilot's ahead-of-plane wisps. It is
        // drawn ahead of a camera, so a remote seat, which has none, takes no copy.
        if (!paneless && !_policy.EmptyStage)
        {
            controller.SpeedCue = SpeedCue.Build(_world.ChapterZrdrPath, _aircraft.Textures, _worldRoot,
                _world.Ambience,
                rig.VisualLayer == 0 ? null : node => SplitScreen.SetVisualLayer(node, rig.VisualLayer));
        }

        // The enhanced streak field rides the same per-pane path for the same reason, and it
        // centres on whichever camera renders it, so each pilot needs a private one. It builds on
        // the empty stage too, being remake-only rather than chapter data.
        if (WindStreaks.Create() is { } streaks)
        {
            if (rig.VisualLayer != 0)
                SplitScreen.SetVisualLayer(streaks, rig.VisualLayer);
            _worldRoot.AddChild(streaks);
            controller.WindStreaks = streaks;
        }

        controller.Name = $"player{pi + 1}";
        // The original names a peer's aircraft by its callsign (FUN_00497990), and co-op follows it.
        // A nameless player reads the record's starting "Unknown", not the roster's stand-in.
        controller.PilotName = seat is not { Callsign.Length: > 0 } ? null
            : seat.Unnamed ? _aircraft.WeaponMessages?.Get(HudMessages.UnknownKey) ?? HudMessages.UnknownKey
            : seat.Callsign;
        rig.Controller = controller;
        _worldRoot.AddChild(controller);
        // ⚠ Hide a remote seat's pilot HUD the frame it enters the tree. A human-piloted rig
        // builds its own canvases in _Ready, and an empty one still carries the telemetry line
        // and the message stack. A seat with no pane would paint those over the local view.
        if (paneless)
        {
            controller.SetPilotHudVisible(false);
        }

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
            // The very first spawn's propellers: Respawn() spins them on every later respawn, but
            // Setup() above called Respawn() before this runtime existed.
            controller.SpinPropsAtSpawn();
        }

        // This pilot's own airframe onto its own visual layer, LAST, so everything the lines above
        // hung on the model travels with it. This pane's spyglass drops that layer; the pane drops
        // the one a first-person view moves the body onto (docs/architecture/Session.md).
        if (!paneless)
        {
            // Never a remote seat: no camera here looks out of that aeroplane. The band is four
            // layers wide, so a seat past the fourth would wrap onto a pane's own.
            SplitScreen.SeatAirframe(planeModel, rig.Camera, pi);
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
    /// here because it is applied after the bind and wins by arriving later. ⚠ A swap takes it
    /// away too: the sortie's picks belong to the aeroplane the pilot left, and the airframe a
    /// mission hands over carries its own fit (see <see cref="AirframeSwapRequest"/>).</summary>
    private LoadoutChoice? MenuFitFor(int pi, AirframeSwapRequest? swap)
    {
        int menu = MenuSeatOf(pi);
        if (swap != null || _policy.LoadoutOverride != null)
        {
            return null;
        }

        // A seat flown elsewhere carries its own pilot's fit, so every machine builds it alike.
        if (menu < 0)
        {
            return _human.SeatFit?.Invoke(pi);
        }

        if (menu >= _policy.MenuLoadouts.Count)
        {
            return null;
        }

        return _policy.MenuLoadouts[menu];
    }

    /// <summary>Pane <paramref name="pi"/>'s custom-built plane, or null to fly the stock
    /// airframe. Empty on every launch that did not come off the launchscreen, so the scripted
    /// paths (<c>--plane=</c>, <c>--det</c>) never see one. A seat flown elsewhere carries its own
    /// pilot's build, so every machine builds it alike.</summary>
    private Flight.Hangar.CustomPlaneDef? CustomPlaneFor(int pi) => MenuSeatOf(pi) switch
    {
        < 0 => _human.SeatBuild?.Invoke(pi),
        int menu when menu < _policy.MenuCustomPlanes.Count => _policy.MenuCustomPlanes[menu],
        _ => null,
    };

    // Each militia's pattern and the def it flies an airframe as, read on the first need.
    private MilitiaFits Militias()
    {
        if (_militias == null)
        {
            try
            {
                _militias = MilitiaFits.Load(_aircraft.ZrdrPath, _aircraft.WeaponMessages);
            }
            catch (Exception e)
            {
                Log.Warn("core", $"bot: cannot read the militias' patterns and defs, so no bot flies its militia's: {e.Message}");
                _militias = MilitiaFits.Empty;
            }
        }

        return _militias;
    }

    // Which of this machine's menu seats flies seat pi. The menu lists only the local seats, and a
    // guest's own seat stands behind its host's in the roster. A seat flown elsewhere has none.
    private int MenuSeatOf(int pi) => Net.NetSeats.LocalOrdinal(_human.NetSeats, pi);

    // The pads a local player was given. The assignment lists this machine's players only, so it is
    // indexed by the local player and never by the roster seat. Null passes through (every pad).
    private int[]? PadsOf(int localPlayer) => _human.PadAssignment is not { } assignment
        ? null
        : localPlayer >= 0 && localPlayer < assignment.Length ? assignment[localPlayer] : Array.Empty<int>();

    // A seat's racer on this machine's race, named by its network callsign where it has one. A run
    // flown here feeds it, through the race link on a network race; a seat flown elsewhere has none.
    private void JoinRace(StuntRace race, int pi, string planeDisplay, Net.NetSeat? seat, StuntMission? run,
        string scoreKey = "")
    {
        var racer = race.Add(pi, planeDisplay, scoreKey);
        if (seat is { Callsign.Length: > 0 })
            racer.Callsign = seat.Callsign;
        if (run == null)
            return;
        if (_human.RaceFeed is { } feed)
            feed(pi, run);
        else
            race.Follow(pi, run);
    }

    public readonly record struct AssemblyState(int MeshInstances, string WhatSuffix,
        ulong PaintRngState, IReadOnlyList<FlightStart>? Starts);

}
