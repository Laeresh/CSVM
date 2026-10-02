using System;
using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.UI.Boards;
using Godot;

namespace CSVM.Launch;

/// <summary>The flight's one shared projectile pool, one step of the session build. Every
/// player's guns fire into it. Its sinks route a hit to the world's destructibles and a CRATER
/// strike to the crater field and its Enhanced scorch. A wash goes to the struck pane. It reuses
/// the session archives and the world gamez, so rockets get their flyout bodies and trails.
/// Module entry: docs/architecture/Launch.md on src/Launch/ProjectileStage.cs.</summary>
internal static class ProjectileStage
{
    /// <summary>Builds the pool over the <see cref="BuildState"/>, binds every pane's camera
    /// and the world's point lights, and parents it under the inputs' world root.</summary>
    public static ProjectilePool Build(BuildState state, Inputs inputs)
    {
        var scorches = inputs.Scorches;
        // flyoutAnims: the world program also carries the rockets' FLYOUT MODEL_ANIMATION defs
        // (cam_anim / missile_puffers), from which the pool builds each type's smoke trail.
        // Null on the empty stage (no world program), rockets there fly trail-less, like the body.
        var projectiles = new ProjectilePool(state.Textures, state.Sounds, state.SoundDefs,
            flyoutGamez: state.Gamez, flyoutScene: state.WorldScene, flyoutAnims: state.CrashProgram,
            soundGroups: state.SoundGroups, ambience: inputs.Ambience)
        {
            // Route weapon hits to the world's destructibles. The pool's raycast reports the
            // struck collider; the runtime resolves it and spends the weapon's HEALTH_DAMAGE. Null runtime ⇒ impacts stay cosmetic.
            DamageSink = state.WorldRuntime != null ? state.WorldRuntime.DamageAt : null,
            ShooterDamageSink = state.WorldRuntime != null ? state.WorldRuntime.DamageAt : null,
            // Route a CRATER weapon's ground strike to the mission's crater field. The pool asks only
            // for a node carrying can_modify, which no shipped node does (Mech3.CraterField).
            CraterSink = state.Craters != null ? state.Craters.TryCarve : null,
            // And the scorch that layers over the carve under Enhanced (Effects.ScorchField).
            // Read through the supplier, since a live mode switch builds or frees the field.
            ScorchSink = state.Craters != null
                ? (at, normal, effectName, carved) =>
                {
                    if (scorches() is { } scorch)
                        RegisterScorch(scorch, at, normal, effectName, carved);
                }
            : null,
            // The same equal-power splitscreen factor FlightAudio's own-ship loops take, plus the
            // nearest-human snapshot shared with WorldSession and the world-effects runtime.
            MixGain = inputs.MixGain,
            PlayerPositions = inputs.PlayerPositions,
            // muzzle_burst's PLAYER_1ST_PERSON: the same closure the world runtime gets, so the
            // shot's lights pick the same testfp branch the anim data would.
            FirstPersonView = inputs.FirstPersonView,
            // The Cockpit view's own rule is per shooter, not per session. The pilot in the canopy
            // loses their own flash quads, and every other aeroplane keeps theirs.
            CockpitViewOfPilot = inputs.CockpitViewOfPilot,
            BeeperTags = inputs.BeeperTags,
            WashSink = inputs.ScreenFlash != null ? inputs.ScreenFlash.PlayBlend : null,
            EngineDeadBounds = TanglerChoke.EngineDeadBounds(inputs.WeaponDefs),
        };
        // ⚠ Bind EVERY pane's camera, never player 1's alone. The tracer pixel floor is a
        // screen-space rule over one shared world mesh. A single viewer sizes every round against
        // that pane and draws the same geometry oversized in all the others.
        projectiles.Viewers = inputs.Viewers;
        // The first-person muzzle pair joins the world's point lights, which is how it reaches
        // the cockpit interior. The empty stage has no set and keeps the omni flash.
        if (inputs.Lights != null)
            projectiles.BindPointLights(inputs.Lights);
        inputs.WorldRoot.AddChild(projectiles);
        return projectiles;
    }

    /// <summary>The enhanced scorch's one decision point, as RegisterBurstLight is the burst
    /// light's. A hit marks the ground it burned when it carved a bowl. It also marks it when its
    /// effect is a fireball EffectCatalogue names for the burst light. Every other impact, the gun hits
    /// among them, leaves the surface alone. One crater radius serves every weapon because all six
    /// CRATER carriers author the block bare (docs/org/craters.md). Public so the scorch suite
    /// decides as a session does rather than modelling it.</summary>
    public static void RegisterScorch(ScorchField scorches, Vector3 at, Vector3 normal,
        string? effectName, bool carved)
    {
        if (!carved && (effectName == null || !EffectCatalogue.IsBurstLight(effectName)))
            return;
        scorches.Mark(at, normal, CraterShape.RimRadius, carved);
    }

    /// <summary>What the pool is built over, each a session-owned reader or an earlier step's
    /// output.</summary>
    internal sealed class Inputs
    {
        public Node3D WorldRoot = null!;
        public EffectAmbience Ambience = null!;
        public WeaponDefs WeaponDefs = null!;
        public ViewerSet Viewers = null!;
        public WorldLights? Lights;
        // The live scorch field, which a graphics switch builds or frees.
        public Func<ScorchField?> Scorches = null!;
        public float MixGain;
        public Func<IReadOnlyList<Vector3>> PlayerPositions = null!;
        public Func<bool> FirstPersonView = null!;
        public Func<int, bool> CockpitViewOfPilot = null!;
        public BeeperTags<FlightController>? BeeperTags;
        public ScreenFlash? ScreenFlash;
    }
}
