using System;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>Spawns an AI-piloted aircraft into a running session, any time after the build: the
/// flight-essential subset of a player rig — painted plane, <see cref="FlightController"/> with
/// an <see cref="AiPilot"/>, no camera/HUD/input devices, hittability, per-part damage, stock
/// loadout, and the standard crash runtime — over the same <see cref="FlightRigAssembler.Inputs"/>
/// the player rigs used. <c>GameSession.DriveSimSteps</c> steps every spawned controller.
/// ⚠ Livery draws come from the shared paint stream after every player has drawn; keep that
/// ordering, or player liveries would shift when AI exists.
/// ⚠ The spawned subtree is deliberately not indexed into the world runtime's
/// <c>NameResolver</c>, like a player's plane: planes.zbd gamez indices collide with the
/// chapter's by-index map. A mission animation needing an AI plane by name goes through
/// <c>AnimRuntime.IndexStage</c> instead.</summary>
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

    /// <summary>Builds one AI aircraft at <paramref name="pos"/> facing <paramref name="lookAt"/>,
    /// flown by <paramref name="pilot"/>, and adds it to the world. <paramref name="scheme"/>/
    /// <paramref name="team"/>, given, are worn/set as-is — a given scheme short-circuits the
    /// resolver draw, so an authored livery consumes no RNG under <c>--det</c>.
    /// <paramref name="inert"/> builds straight into <see cref="FlightController.Inert"/>;
    /// <paramref name="shippedSkins"/> uses the plane's own shipped textures.</summary>
    public FlightController Spawn(string planeName, Vector3 pos, Vector3 lookAt, AiPilot pilot,
        PaintScheme? scheme = null, int? team = null, bool inert = false, bool shippedSkins = false)
    {
        int index = _spawned++;
        // Jittered airframe copy (WithAiSpawnJitter), keyed by spawn ordinal for --det replay.
        // ⚠ The original exempts its class-4 `w*` wingman family; this engine cannot — see
        // org/flightModel.md "The class-4 wingman exemption has no analogue here".
        var stats = _in.AiStatsFor(planeName).WithAiSpawnJitter(Rng.NewSystemRandom(Rng.Spawn, index, 0));
        if (pilot.Machine is { } machine)
        {
            // The airframe's shipped range gates (vehicle.json attack / return_range).
            machine.AttackRange = stats.AiAttackRange;
            machine.ReturnRange = stats.AiReturnRange;
        }
        FlightController controller;
        Node3D planeModel;
        // The whole build: model, controller, loadout, crash runtime, tree add. A nested
        // PlaneBuilder.Build scope is suppressed and folded into this one, per PerfSample's
        // flat-leaves rule.
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
                // Zones or an authored whole pair: an AI airframe resolves the pair and
                // no zones, so a parts-only test would leave it undamageable. PlaneDamage handles
                // the zone-less case natively.
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
                // never stepped, drawn or hittable for even one frame: both Setup's Respawn and
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

            // No camera rides an AI plane: Setup(null) skips it, CamParams gets the default. The
            // force path is chosen once here, off who is flying — nobody, so AiForcePath.
            // See FlightModel.UsesAiForcePath.
            controller.Setup(
                new FlightModel(stats, aiForcePath: !controller.IsHumanPiloted),
                null, new CamParams(), pos, lookAt);
            controller.Name = $"ai{index + 1}_{planeName}";
            _worldRoot.AddChild(controller);

            // Standard crash choreography, built after the controller joins the tree (its reset
            // states read global transforms). Keys onto ai_crash_<surface>, the original's AI
            // family, since IsHumanPiloted is false.
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
