using System;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>Spawns an AI-piloted aircraft into a running session, any time after the build: the
/// painted plane model, a <see cref="FlightController"/> driven by an <see cref="AiPilot"/>
/// (<c>IsHumanPiloted</c> false, no camera, no HUD, no input devices), the shared
/// <see cref="PlaneCollider"/>/<see cref="AircraftBody"/> hittability, per-part damage, its stock
/// loadout, and the standard per-plane crash runtime — the same pieces
/// <see cref="FlightRigAssembler"/> hangs on a player, minus everything that serves a person at
/// the controls. Constructed once by <c>GameSession.BuildFlightRigs</c> over the same
/// <see cref="FlightRigAssembler.Inputs"/> the player rigs used; the session steps every spawned
/// controller in <c>DriveSimSteps</c> (a realtime clock lets them tick themselves, like a rig).
///
/// <para>Livery draws come from the session's shared paint stream AFTER every player has drawn
/// (players draw at build, AI at spawn), so player liveries are unchanged by AI existing.</para>
///
/// <para>⚠ The spawned subtree is deliberately NOT indexed into the world runtime's
/// <c>NameResolver</c> — the same rule as a player's plane. The plane carries planes.zbd gamez
/// indices, a different index space than the chapter world's, which is exactly the by-index
/// collision <c>AnimRuntime.NameResolveFallback</c> exists for; the per-plane crash runtime
/// (name-resolve fallback, scoped to this subtree) is where its animations live. A later item
/// that must make AI aircraft addressable by world/mission animations goes through
/// <c>AnimRuntime.IndexStage</c>, which owns the find-cache invalidation.</para></summary>
public sealed class AiAircraftSpawner
{
    /// <summary>The first AI shooter id. Outside every player index (0–3, and
    /// <see cref="AimAssist.TeamOfPilot"/> maps it to a team hostile to all of them) and below
    /// <see cref="IncomingFire.ShooterId"/>; a kill by an AI reaches a <c>VersusMatch</c> as a
    /// plain death because the id sits outside the match roster.</summary>
    public const int ShooterIdBase = 100;

    private readonly SessionSpec _spec;
    private readonly LiveryResolver _liveries;
    private readonly WorldEffectsFactory _worldEffects;
    private readonly Node3D _worldRoot;
    private readonly FlightRigAssembler.Inputs _in;
    private int _spawned;

    public AiAircraftSpawner(SessionSpec spec, LiveryResolver liveries,
        WorldEffectsFactory worldEffects, Node3D worldRoot, FlightRigAssembler.Inputs inputs)
    {
        _spec = spec;
        _liveries = liveries;
        _worldEffects = worldEffects;
        _worldRoot = worldRoot;
        _in = inputs;
    }

    /// <summary>Builds one AI aircraft at <paramref name="pos"/> with its nose on
    /// <paramref name="lookAt"/>, flown by <paramref name="pilot"/>, and adds it to the world.
    /// Callable at any point in the session's life; the returned controller is live (ticking,
    /// hittable, damageable) as soon as its <c>_Ready</c> has run. <paramref name="scheme"/>,
    /// given, is worn AS-IS instead of a resolver draw — no RNG consumed, so an authored livery
    /// (an Instant Action ace's) never shifts another spawn's pinned paint under
    /// <c>--det</c>. <paramref name="team"/>, given, overrides
    /// <see cref="FlightController.Team"/>'s pilot-index-derived default (an Instant Action
    /// actor's side is authored, not derived from its shooter id).
    /// <paramref name="inert"/> builds the aircraft straight into
    /// <see cref="FlightController.Inert"/> — complete but held out of
    /// the session, so it never has a live frame between construction and its own activation; the
    /// caller puts it in play with <see cref="FlightController.Activate"/>.
    /// <paramref name="shippedSkins"/> builds the aircraft in its own shipped textures instead of
    /// the Fortune Hunters default an unauthored livery otherwise resolves to — for an actor that
    /// flies for another militia (an Instant Action wave enemy) whose pattern is not decidable
    /// from the mission data; --paint= still overrides it.</summary>
    public FlightController Spawn(string planeName, Vector3 pos, Vector3 lookAt, AiPilot pilot,
        PaintScheme? scheme = null, int? team = null, bool inert = false, bool shippedSkins = false)
    {
        int index = _spawned++;
        // C26: the original's per-spawn ±5 % spread (PlaneStats.WithAiSpawnJitter), on a copy of the
        // session's shared airframe cache. Its "not the player" test is read off IsHumanPiloted,
        // C21's recorded divergence; its other two gates hold by construction here (no network play,
        // and every airframe this spawner can fly resolves the class token `mode jet` = 0).
        // ⚠ The original exempts its own class-4 `w*` wingman family and this engine cannot: an
        // Instant Action wingman flies a player airframe. Divergence recorded in org/flightModel.md.
        // Keyed by spawn ordinal rather than drawn off the shared spawn stream, so a --det replay
        // reproduces it and no other subsystem's sequence moves.
        // BL-386: the AI flavour of the airframe — the player chain for dynamics, loadout key,
        // turrets and model, the AI def's own chain for the damage model (an authored whole pair,
        // no zones). Nobody is at these controls, so every aircraft this spawner builds gets it.
        var stats = _in.AiStatsFor(planeName).WithAiSpawnJitter(Rng.NewSystemRandom(Rng.Spawn, index, 0));
        if (pilot.Machine is { } machine)
        {
            // The airframe's shipped range gates (vehicle.json attack / return_range).
            machine.AttackRange = stats.AiAttackRange;
            machine.ReturnRange = stats.AiReturnRange;
        }
        FlightController controller;
        Node3D planeModel;
        // The whole build — model, controller, loadout, crash runtime and
        // adding it to the tree. Includes the plane's own decal paint (ResourceLoad), reached
        // deeper in PlaneBuilder.Build — a nested scope there is suppressed and folded into this
        // one, per PerfSample's flat-leaves rule.
        using (PerfSample.Scope(PerfSite.AiSpawn))
        {
            var planeBuilder = new PlaneBuilder(_in.PlanesGamez, _in.Textures, spinningProps: true,
                scheme: scheme ?? _liveries.SchemeFor(_in.RigCount + index, _in.ZrdrPath,
                    _in.PaintRng, _liveries.PatternsForPlane(_in.PlanesGamez, planeName),
                    useDefaultPattern: !shippedSkins),
                patterns: _liveries.Patterns);
            planeModel = planeBuilder.Build(planeName);

            controller = new FlightController
            {
                PlayerIndex = ShooterIdBase + index,
                IsHumanPiloted = false,
                Pilot = pilot,
                PlaneModel = planeModel,
                Props = PropAnimator.Build(planeModel),
                WingLights = WingLightBlinker.Build(planeBuilder.WingFlares, _spec.AnimLod),
                Surfaces = ControlSurfaceAnimator.Build(planeModel),
                Collider = PlaneCollider.Build(planeModel),
                // Zones OR an authored whole pair: an AI airframe resolves the pair and no zones
                // at all, and a parts-only test would leave it undamageable (BL-386). PlaneDamage
                // handles the zone-less case natively — the resolver returns no part and the hit
                // spends against the whole pair, which is the decoded zone-less route.
                Damage = stats.DestroyableParts.Count > 0 || stats.VehicleHealth is > 0f
                    ? PlaneDamage.For(stats) : null,
                CollideDamageSink = _in.WorldRuntime != null ? _in.WorldRuntime.CollideDamageAt : null,
                GrazeEffectSink = _in.WorldEffects is { } fx ? (name, pt) => fx.PlayEffectAt(name, pt) : null,
                TouchdownDefs = _in.TouchdownDefs,
                // Set outside the loadout bind below: the pool registration in _Ready is what makes
                // this plane a hit target, loadout or not.
                Projectiles = _in.Projectiles,
                // No person at these controls: no keyboard, no pads (an empty list, not null — null
                // means "every connected pad"), no pause, and no HUD (gated on IsHumanPiloted).
                UseKeyboard = false,
                PadDevices = Array.Empty<int>(),
                AllowPause = false,
                // Set here, before Setup and before the node joins the tree, so an inert airframe is
                // never stepped, drawn or hittable for even one frame (E10): both Setup's Respawn and
                // _Ready re-assert the state as the pieces that carry it come into existence.
                Inert = inert,
            };
            if (team is { } t)
                controller.Team = t;
            controller.Shake = new PlaneShake(_in.Shakes);
            var shakePivot = new Node3D { Name = "ShakePivot" };
            controller.ShakePivot = shakePivot;
            controller.AddChild(shakePivot);
            shakePivot.AddChild(planeModel);

            // The stock fit, so the airframe carries its real guns/ordnance (mounted rocket bodies
            // included) — nothing pulls a trigger until a later wave gives the pilot one.
            if (_in.StockLoadouts.For(stats.DefName) is { } ldef)
            {
                try
                {
                    controller.Loadout = Loadout.Bind(ldef, planeModel, _in.WeaponDefs);
                    controller.Destructibles = _in.WorldRuntime?.Destructibles;
                    controller.Ordnance = PylonOrdnance.Build(controller.Loadout, _in.Projectiles);
                }
                catch (Exception e)
                {
                    GD.PushWarning($"ai: loadout bind failed for '{stats.DefName}': {e.Message}");
                }
            }

            // No camera rides an AI plane — Setup(null) skips the whole camera half — and CamParams
            // is camera tuning, so the default is passed rather than loading the plane's block.
            // The plant's force path is chosen once, here, off who is flying (C21): nobody, so the
            // AI path. See FlightModel.UsesAiForcePath.
            controller.Setup(
                new FlightModel(stats, aiForcePath: !controller.IsHumanPiloted),
                null, new CamParams(), pos, lookAt);
            controller.Name = $"ai{index + 1}_{planeName}";
            _worldRoot.AddChild(controller);

            // The standard per-plane crash choreography, built after the controller joins the tree
            // (its reset states read global transforms) — same call as a player rig; the factory keys
            // this controller (IsHumanPiloted false) onto the ai_crash_<surface> vector, the
            // original's own AI family.
            if (_in.CrashProgram != null && _in.WorldScene != null)
            {
                _worldEffects.BuildFlightCrashRuntime(controller, planeBuilder, planeName, _in.Gamez,
                    _in.WorldScene, _in.Textures, _in.CrashProgram, verbose: false);
                controller.CrashRuntime?.Play("startprops", planeModel, applyReset: false);
            }
        }

        GD.Print($"ai: spawned '{planeName}' as {controller.Name} (shooter id " +
                 $"{controller.PlayerIndex}) pos=({pos.X:0},{pos.Y:0},{pos.Z:0}) " +
                 $"jitter=(fd {stats.FdSpeed:0.0} thrust {stats.EnginePower:0.000}) " +
                 (inert ? "INERT " : "") +
                 (pilot.Patrol is { } patrol
                     ? $"net='{patrol.Net.Name}#{patrol.Net.Id}' ({patrol.Net.Nodes.Count} nodes)"
                     : $"heading={pilot.TargetHeadingDeg:0}° alt={pilot.TargetAltitude:0} m"));
        return controller;
    }
}
