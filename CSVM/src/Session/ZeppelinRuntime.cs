using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>
/// Runs a mission's zeppelins (M4 F17 motion + F18 damage, behind <c>--zeppelins</c>): each
/// <see cref="ZeppelinDef"/> whose world node and net resolve gets a
/// <see cref="ZeppelinMotion"/> on B5's <see cref="AiNetFollower"/>, is placed at its authored
/// position/yaw/pitch, and the NODE is flown along the net under the record's limits — an
/// anim/world node moved kinematically, not a FlightController (zeppelins have no flight
/// model). Every place/skip/hold and every node capture prints a <c>zep:</c> line, which is
/// the flag's observability; <c>--debug-ainets=&lt;net&gt;</c> draws the route it flies.
///
/// <para><b>Damage (F18, wired by <see cref="WireDamage"/>):</b> per-part scalar pools in the
/// world's <see cref="DestructibleRegistry"/> — gasbags and <c>cannon_health</c> cannons seeded
/// from the mission record where authored (the record beats a def pool via
/// <c>Instance.Reseed</c>), everything else keeping its compiled def's own <c>HEALTH</c>
/// (engines 30–40, turrets 10, cannons 60 in this install). A healthy zone with neither an
/// authored hp nor a destructible def is NOT damageable and says so in a <c>zep:</c> line —
/// never an invented default. The per-zeppelin <see cref="ZeppelinDamage"/> aggregator owns the
/// kill (<c>survivors &lt; num_healthy_required</c>, the decoded polarity), engine deaths drive
/// <see cref="ZeppelinMotion.AliveEngines"/> (F17's sqrt curve) and, once the last one is gone,
/// <see cref="ZeppelinEnginesDisabled"/> — Instant Action's own win on <c>zeppelin_run</c>, which
/// the hull kill is only the second route to. The kill plays the
/// authored hull death — the def anchored on the zeppelin node whose ACTIVATION_PREREQUISITE
/// counts the gasbag finish anims (<c>all_pzep_gasbags</c>, which itself calls
/// <c>killpzep</c>). <see cref="GateWeaponDamage"/> is the <c>DAMAGES_ZEPPELIN</c> routing
/// gate the projectile pool consults: gasbag zones only.</para>
///
/// <para>⚠ Deliberately unwired, each named at its site: a <c>deactivated</c> record is placed
/// but HELD (mission script would activate it; out of M4's scope); stop nodes are NOT
/// implemented — the per-node tags ride along raw because their encoding is still undecoded
/// (F17's open item); a <c>cannon_health</c> record's gasbag binding (a hatch hit taking out
/// its section) is parsed and kept, but no damage transfer is decoded, so none is invented.
/// Effect templates snap to absolute world points and do not track a moving host, so a hit
/// effect on a flying zeppelin stays where the hit happened — the death choreography plays
/// where the hull died for the same reason; nothing here fights it.</para>
/// </summary>
public sealed partial class ZeppelinRuntime : Node
{
    private readonly List<LiveZeppelin> _live = new();
    private AnimRuntime? _runtime;
    private float _sinceLog;
    private int _gateLogged;

    /// <param name="trailerTarget">Where an anchored net's trailer target is (`BL-377`), per net;
    /// null flies every route at its authored coordinates. ⚠ Exactly two of the 222 nets are both
    /// zeppelin-flown and anchored, and neither is self-referential: C1C's <c>SwanZep1</c>
    /// (<c>blackswanzep</c>) rides <c>workersvoyagezep</c> and C2B's <c>Gemini2</c>
    /// (<c>geminizep</c>) rides <c>piratezep</c>, one zeppelin escorting another. Both records are
    /// <c>deactivated</c>, so this is unobservable until a script layer wakes them; it is wired
    /// because it is the decoded behaviour, not because anything flies it today.</param>
    public ZeppelinRuntime(IReadOnlyList<ZeppelinDef> defs, Func<string, Node3D?> resolveNode,
        IReadOnlyList<AiNet> chapterNets, Func<AiNet, Func<Vector3?>?>? trailerTarget = null)
    {
        Name = "zeppelins";
        foreach (var def in defs)
        {
            var host = resolveNode(def.Node);
            if (host == null)
            {
                GD.Print($"zep: '{def.Node}' skipped: world node unresolved");
                continue;
            }
            var net = AiNets.ByName(chapterNets, def.Net);
            if (net == null)
            {
                // Still placed: the authored pose is real even without a route (and B6's
                // generator altitude gate reads the node's live Y).
                Place(host, def.Position, Mathf.DegToRad(def.YawDeg), Mathf.DegToRad(def.PitchDeg));
                GD.Print($"zep: '{def.Node}' placed but held: net '{def.Net}' not in neindex");
                continue;
            }
            // The capture radius must clear the turning circle (v/ω plus headroom for the
            // rate ramp-in), or a slow wide zeppelin orbits a node forever; same invented-
            // radius caveat as AiNetFollower.DefaultArrivalRadius.
            float turnCircle = def.MaxSpeed / Mathf.Max(Mathf.DegToRad(def.MaxRateYawDeg), 1e-3f);
            float arrival = Mathf.Max(AiNetFollower.DefaultArrivalRadius, 1.5f * turnCircle);
            var follower = new AiNetFollower(net, Rng.NewSystemRandom(Rng.Ai), arrival,
                trailerTarget?.Invoke(net));
            var motion = new ZeppelinMotion(def, follower);
            Place(host, motion.Position, motion.YawRad, motion.PitchRad);
            _live.Add(new LiveZeppelin(def, motion, host));
            GD.Print($"zep: '{def.Node}' placed at ({def.Position.X:0},{def.Position.Y:0}," +
                     $"{def.Position.Z:0}) on net '{net.Name}' ({net.Nodes.Count} nodes), " +
                     $"max_speed {def.MaxSpeed:0.#} m/s, engines {motion.TotalEngines}" +
                     (def.Deactivated ? " — deactivated, holding" : ""));
        }
    }

    /// <summary>Raised once when a zeppelin's survivor count crosses the threshold — the
    /// generator runtime disables the dead host's generator off this (decoded rule).</summary>
    public event Action<string>? ZeppelinKilled;

    /// <summary>Raised once when a zeppelin's last live engine dies. This is Instant Action's own
    /// win signal on <c>zeppelin_run</c> (<c>FUN_0045b9d0</c> at <c>0x0045be0a</c> tests the live
    /// engine vector for empty BEFORE it tests the hull's death byte), which is why the mode's
    /// briefing says "Destroy the zeppelin's engines to win!" and its target panel reads "Disable
    /// Engines". The original has no separate flag for it: <c>FUN_004bf150</c> erases each dead
    /// nacelle from the vector the speed curve already reads, so an empty vector IS the signal.
    /// Here the same recount raises this, so nothing polls a second list.
    ///
    /// <para>A record authoring NO engines fires this on the first step, matching the original's
    /// empty-from-load vector. That is unobservable in the shipped data (all 58 records author 12
    /// or 14) but it is the decoded behaviour, not an accident.</para></summary>
    public event Action<string>? ZeppelinEnginesDisabled;

    /// <summary>Zeppelins placed on a resolved net (a held <c>deactivated</c> one counts — it
    /// is placed and would fly when a script layer wakes it).</summary>
    public int LiveCount => _live.Count;

    /// <summary>The live motions by node name, the F18 seam's lookup (damage writes
    /// <see cref="ZeppelinMotion.AliveEngines"/>).</summary>
    public ZeppelinMotion? MotionFor(string node) => Find(node)?.Motion;

    /// <summary>Whether this zeppelin's kill has fired (false for an unknown node).</summary>
    public bool IsDead(string node) => Find(node)?.Dead ?? false;

    /// <summary>Instant Action's builder holds a zeppelin it has switched off
    /// (<c>FUN_0045a390</c>'s tail): the record stays placed at its
    /// authored pose, but its motion, broadside and damage poll stop from here on. That is the
    /// CSVM stand-in for the builder's own <c>FUN_0045a2a0</c>, which tears down the vehicle/AI
    /// objects under the deactivated node — CSVM has no equivalent object graph to delete, and a
    /// merely hidden zeppelin would keep flying its net and firing invisible broadsides.
    /// Switching the NODE off is the caller's own act (the decoded <c>gwNodeSetActive</c>),
    /// because the builder's three <c>*_zeppelin</c> names need not be zeppelin records at all.
    /// Returns whether the name is a zeppelin of this mission; one-way, like the original's
    /// (nothing re-activates a held zeppelin).</summary>
    public bool Hold(string node)
    {
        if (Find(node) is not { } zep)
        {
            return false;
        }
        zep.Held = true;
        return true;
    }

    /// <summary>Current surviving healthy-entry count, or -1 for an unknown/unwired node.</summary>
    public int SurvivorsOf(string node) =>
        Find(node) is { Damage: { } damage } zep ? damage.Survivors(zep.ZoneAlive) : -1;

    /// <summary>Appends every live zeppelin's damage zones (gasbags, engines, cannons) to
    /// <paramref name="into"/>, one candidate per part, for the player-target pool
    /// (<c>PLAN-targeting.md</c> B12). Each part rides its hull, so it carries the zeppelin's own
    /// velocity (<c>Forward * Speed</c>) rather than zero; a destroyed zone is offered but not live,
    /// and a part whose anchor has left the tree is skipped rather than read (its global transform
    /// is meaningless there, the same rule <see cref="AimCandidateSet.AddStructures"/> follows).
    ///
    /// <para>A plain list, deliberately NOT an <see cref="AimCandidateSet"/>'s <c>Structures</c>:
    /// <see cref="TargetPool"/> never reads that list, so the world's destructible registry cannot
    /// reach the player's cycles even if a future caller feeds a shared scan. This is the only
    /// channel by which a structure becomes selectable.</para>
    ///
    /// <para>⚠ <b>This is a deliberate divergence from the original, not a port of it.</b> The
    /// decode found NO sub-part enumeration anywhere in the targeting path
    /// (<c>docs/org/targeting.md</c> "The class model"): a gasbag is selectable there only because
    /// the mission authored it as its own <c>MStruct</c> carrying <c>otherTarget</c> /
    /// <c>objectiveTarget</c>. CSVM has no mission flag data to read, so it enumerates the parts it
    /// already models as damageable instead. Decision 8 asked for this; do not "correct" it back by
    /// citing the decode.</para></summary>
    public void CollectTargetParts(List<AimCandidate> into, int team = AimAssist.WorldTeam)
    {
        foreach (var zep in _live)
        {
            var vel = zep.Motion.Forward * zep.Motion.Speed;
            foreach (var inst in zep.GasbagZones.Values)
            {
                AddPart(into, inst, team, vel);
            }

            foreach (var inst in zep.EngineZones.Values)
            {
                AddPart(into, inst, team, vel);
            }

            foreach (var cannon in zep.CannonZones)
            {
                AddPart(into, cannon.Instance, team, vel);
            }
        }
    }

    /// <summary>Builds every zeppelin's damage zones over the world's destructible registry
    /// (the class summary's seeding rules) and starts the per-step damage poll. Idempotent.</summary>
    public void WireDamage(AnimRuntime runtime)
    {
        if (_runtime != null)
        {
            return;
        }
        _runtime = runtime;
        foreach (var zep in _live)
        {
            WireZones(zep, runtime);
        }
    }

    /// <summary>The DAMAGES_ZEPPELIN routing gate (<c>ProjectilePool.WorldDamageGate</c>): a
    /// weapon without the flag cannot damage a GASBAG zone; every other target passes. The
    /// impact effect/sound still play — only the damage is refused.</summary>
    public bool GateWeaponDamage(Node? struck, WeaponDef weapon)
    {
        if (_runtime == null || ZeppelinDamage.MayDamageGasbag(weapon))
        {
            return true;
        }
        if (_runtime.Destructibles.Resolve(struck) is not { } inst)
        {
            return true;
        }
        foreach (var zep in _live)
        {
            if (zep.GasbagInstances.Contains(inst))
            {
                if (_gateLogged < 8)
                {
                    _gateLogged++;
                    GD.Print($"zep: gasbag hit by {weapon.Id} ({weapon.Name}) blocked — " +
                             $"no DAMAGES_ZEPPELIN");
                }
                return false;
            }
        }
        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = GameClock.Current?.PhysicsDt(delta) ?? (float)delta;
        if (dt <= 0f)
        {
            return;   // the session drives SimStep itself this frame (see GameClock.PhysicsDt)
        }
        SimStep(dt);
    }

    /// <summary>One step of every active motion, written onto the world nodes, then the damage
    /// poll. Public for the same reason the generators' is: a fixed or halted clock has the
    /// session drive it.</summary>
    public void SimStep(float dt)
    {
        _sinceLog += dt;
        bool log = _sinceLog >= 10f;
        if (log)
        {
            _sinceLog = 0f;
        }
        foreach (var zep in _live)
        {
            if (zep.Def.Deactivated || zep.Held)
            {
                // Placed, holding: either for a mission-script wake-up (the record's own
                // `deactivated`, out of M4 scope) or because Instant Action's builder switched
                // this zeppelin off (F12, see Hold).
                continue;
            }
            if (!zep.Dead)
            {
                int before = zep.Motion.Follower.CurrentIndex;
                zep.Motion.Step(dt);
                Place(zep.Host, zep.Motion.Position, zep.Motion.YawRad, zep.Motion.PitchRad);
                if (zep.Motion.Follower.CurrentIndex != before && before >= 0)
                {
                    GD.Print($"zep: '{zep.Def.Node}' captured node {before}, next " +
                             $"{zep.Motion.Follower.CurrentIndex} of '{zep.Motion.Follower.Net.Name}'");
                }
                if (log)
                {
                    var p = zep.Motion.Position;
                    GD.Print($"zep: '{zep.Def.Node}' at ({p.X:0},{p.Y:0},{p.Z:0}) " +
                             $"speed {zep.Motion.Speed:0.#}/{zep.Motion.EffectiveMaxSpeed:0.#} m/s " +
                             $"toward node {zep.Motion.Follower.CurrentIndex}");
                }
            }
            PollDamage(zep);
            if (!zep.Dead)
            {
                StepBroadside(zep, dt);   // F19: a dead zeppelin's cannons go quiet
            }
        }
    }

    // ---- the F18 damage half (static helpers first, per ordering rules) ----

    // Yaw about Y (mission convention, 0 = −Z) then pitch about X; zeppelins never bank.
    private static void Place(Node3D host, Vector3 position, float yawRad, float pitchRad)
    {
        host.GlobalTransform = new Transform3D(
            Basis.FromEuler(new Vector3(pitchRad, yawRad, 0f)), position);
    }

    // The zone-pool seeding rule: an authored record hp re-seeds an existing def pool or
    // registers a fresh one on the record's destroy-anim def; no record hp falls back to the
    // def pool alone; neither yields null (the caller logs it).
    private static DestructibleRegistry.Instance? ZonePool(AnimRuntime runtime, Node3D host,
        string nodeName, float? recordHp, string? destroyAnim)
    {
        var existing = ExistingPool(runtime, host, nodeName);
        if (recordHp is not { } hp)
        {
            return existing;
        }
        if (existing != null)
        {
            existing.Reseed(hp);
            return existing;
        }
        var node = ZoneNode(runtime, host, nodeName);
        if (node == null)
        {
            return null;
        }
        // The record's destroy anim def doubles as the pool's def, so DamageAt's zero-HP death
        // plays exactly the authored destruction (gasbag1's pzep_gasbagtorpedo1). Prefer the
        // compiled form, the same preference the registry's Resolve applies.
        AnimDefinition? poolDef = null;
        if (destroyAnim != null)
        {
            foreach (var candidate in runtime.DefsFor(destroyAnim))
            {
                if (poolDef == null || (poolDef.Archive == null && candidate.Archive != null))
                {
                    poolDef = candidate;
                }
            }
        }
        if (poolDef == null)
        {
            // The record authored hp but its anim resolves to no def (degraded extraction):
            // the pool still exists so the zone can die; the death plays nothing.
            GD.Print($"zep: zone '{nodeName}' destroy anim '{destroyAnim ?? "-"}' resolves to " +
                     $"no def — pool registered without choreography");
            poolDef = new AnimDefinition { Name = nodeName, AnimName = $"zep_zone_{nodeName}" };
        }
        return runtime.Destructibles.Register(poolDef, node, hp);
    }

    private static DestructibleRegistry.Instance? ExistingPool(AnimRuntime runtime, Node3D host,
        string nodeName)
    {
        var node = ZoneNode(runtime, host, nodeName);
        if (node == null)
        {
            return null;
        }
        DestructibleRegistry.Instance? best = null;
        foreach (var pool in runtime.Destructibles.PoolsOn(node))
        {
            if (best == null || (best.Def.Archive == null && pool.Def.Archive != null))
            {
                best = pool;   // compiled def preferred, same rule as Resolve
            }
        }
        return best;
    }

    private static Node3D? ZoneNode(AnimRuntime runtime, Node3D host, string nodeName) =>
        runtime.FindNodes(nodeName, host) is { Count: > 0 } hits ? hits[0] : null;

    private static void AddPart(List<AimCandidate> into, DestructibleRegistry.Instance? inst,
        int team, Vector3 velocity)
    {
        if (inst == null || !GodotObject.IsInstanceValid(inst.Anchor) || !inst.Anchor.IsInsideTree())
        {
            return;
        }

        into.Add(new AimCandidate
        {
            Position = inst.Anchor.GlobalPosition,
            Velocity = velocity,
            Team = team,
            Live = ZoneIsAlive(inst),
            ConeOverride = AimAssist.NoConeOverride,
            Source = inst,
        });
    }

    private static bool ZoneIsAlive(DestructibleRegistry.Instance? inst) =>
        inst == null || inst.Status != DestructibleRegistry.State.Destroyed;

    private LiveZeppelin? Find(string node)
    {
        foreach (var zep in _live)
        {
            if (zep.Def.Node.Equals(node, StringComparison.OrdinalIgnoreCase))
            {
                return zep;
            }
        }
        return null;
    }

    // Builds one zeppelin's zone pools: gasbags (record hp where authored), engines (def
    // pools), cannon_health cannons (record hp 200 over/instead of the def pool).
    private void WireZones(LiveZeppelin zep, AnimRuntime runtime)
    {
        var def = zep.Def;
        zep.Damage = new ZeppelinDamage(def);

        // Record hp per gasbag node, where authored (57 of 58 records).
        var recordHp = new Dictionary<string, ZeppelinGasbag>(StringComparer.OrdinalIgnoreCase);
        foreach (var bag in def.Gasbags)
        {
            recordHp[bag.Node] = bag;
        }

        // The critical zones: one pool per DISTINCT healthy node (duplicate healthy entries
        // share the node's one pool and are counted per entry by the aggregator).
        foreach (var zone in def.Healthy)
        {
            if (zep.GasbagZones.ContainsKey(zone.Node))
            {
                continue;
            }
            ZeppelinGasbag? bag = recordHp.TryGetValue(zone.Node, out var b) ? b : null;
            var inst = ZonePool(runtime, zep.Host, zone.Node, bag?.Hp, bag?.DestroyAnim);
            zep.GasbagZones[zone.Node] = inst;
            if (inst != null)
            {
                zep.GasbagInstances.Add(inst);
            }
            else
            {
                GD.Print($"zep: '{def.Node}' zone '{zone.Node}' has no authored hp and no " +
                         $"destructible def — not damageable");
            }
        }

        // Engines: def-pooled destructibles only (no record key authors engine hp); an engine
        // without a pool never dies and stays in the alive count.
        int pooled = 0;
        foreach (var engine in def.Engines)
        {
            var inst = ExistingPool(runtime, zep.Host, engine);
            zep.EngineZones[engine] = inst;
            if (inst != null)
            {
                pooled++;
            }
        }
        if (pooled < def.Engines.Count)
        {
            // Instant Action's zeppelin_run is won by emptying this list, so an engine that can
            // never die makes that mode unwinnable on its own objective. Say it outright rather
            // than leaving it to be read out of the census line below.
            GD.PushWarning($"zep: '{def.Node}' has {def.Engines.Count - pooled} engine(s) with no " +
                           $"destructible pool — they can never die, so an Instant Action " +
                           $"zeppelin run on this hull cannot be won on engines");
        }

        // cannon_health cannons: record hp beats the def pool (Reseed); the record's stages
        // (0.6/0.3) play through the aggregator, mirroring the def-pooled DAMAGE_SEQUENCE path.
        foreach (var cannon in def.CannonHealth)
        {
            var inst = ZonePool(runtime, zep.Host, cannon.Cannon, cannon.Hp, cannon.DestroyAnim);
            if (inst != null)
            {
                zep.CannonZones.Add(new CannonZone(cannon, inst));
            }
        }

        GD.Print($"zep: '{def.Node}' damage wired — {zep.GasbagInstances.Count}/" +
                 $"{zep.GasbagZones.Count} gasbag zones pooled, {pooled}/{def.Engines.Count} " +
                 $"engines, {zep.CannonZones.Count}/{def.CannonHealth.Count} cannons, " +
                 $"kill at survivors < {def.NumHealthyRequired} of {def.Healthy.Count}");
    }

    private void PollDamage(LiveZeppelin zep)
    {
        if (zep.Damage is not { } damage)
        {
            return;
        }

        // Engine deaths drive the decoded sqrt curve through F17's seam.
        int engines = damage.AliveEngines(
            name => zep.EngineZones.TryGetValue(name, out var e) ? ZoneIsAlive(e) : true);
        if (engines != zep.Motion.AliveEngines)
        {
            zep.Motion.AliveEngines = engines;
            GD.Print($"zep: '{zep.Def.Node}' engines {engines}/{zep.Motion.TotalEngines} — " +
                     $"max speed now {zep.Motion.EffectiveMaxSpeed:0.#} m/s");
        }

        // The engines gone, once: Instant Action's own win on zeppelin_run (see the event). The
        // denominator here is the RECORD's engine count, not ZeppelinMotion.TotalEngines, which
        // floors at 1 so a record with no engines still moves — that floor must not suppress the
        // decoded empty-vector win.
        // Gated on the hull, because the original's compaction only runs while alive: a death
        // sequence that takes the nacelles with it must not raise this after the fact.
        if (!zep.Dead && !zep.EnginesDisabled && engines == 0)
        {
            zep.EnginesDisabled = true;
            GD.Print($"zep: '{zep.Def.Node}' ENGINES DISABLED — 0 of {zep.Def.Engines.Count} " +
                     $"engine(s) live");
            ZeppelinEnginesDisabled?.Invoke(zep.Def.Node);
        }

        // Per-zone kill lines, once each.
        foreach (var (node, inst) in zep.GasbagZones)
        {
            if (inst is { Status: DestructibleRegistry.State.Destroyed } && zep.DeadZones.Add(node))
            {
                GD.Print($"zep: '{zep.Def.Node}' gasbag '{node}' destroyed — survivors " +
                         $"{damage.Survivors(zep.ZoneAlive)}/{damage.Required} required");
            }
        }

        // Record-authored cannon stages and kill lines (def-pooled cannons stage through
        // their own DAMAGE_SEQUENCE instead).
        foreach (var cannon in zep.CannonZones)
        {
            float fraction = cannon.Instance.MaxHealth > 0f
                ? cannon.Instance.Health / cannon.Instance.MaxHealth : 0f;
            int fired = cannon.StagesFired;
            foreach (var anim in ZeppelinDamage.CrossedStages(cannon.Record.Stages, fraction, ref fired))
            {
                _runtime?.Play(anim);
            }
            cannon.StagesFired = fired;
            if (cannon.Instance.Status == DestructibleRegistry.State.Destroyed
                && zep.DeadZones.Add(cannon.Record.Cannon))
            {
                GD.Print($"zep: '{zep.Def.Node}' cannon '{cannon.Record.Cannon}' destroyed" +
                         $" (bound gasbag '{cannon.Record.Gasbag}' — binding recorded, no " +
                         $"decoded damage transfer)");
            }
        }

        // The kill, once: survivors < num_healthy_required (the decoded polarity).
        if (!zep.Dead && damage.IsDead(zep.ZoneAlive))
        {
            zep.Dead = true;
            int survivors = damage.Survivors(zep.ZoneAlive);
            GD.Print($"zep: '{zep.Def.Node}' DESTROYED — survivors {survivors} < required " +
                     $"{damage.Required}");
            PlayHullDeath(zep);
            ZeppelinKilled?.Invoke(zep.Def.Node);
        }
    }

    // The authored hull death: the def anchored on the zeppelin node whose activation
    // prerequisite counts anims (all_pzep_gasbags — pops the remaining bags and calls
    // killpzep). Data-selected, never a hardcoded name; a zeppelin shipping none logs so.
    private void PlayHullDeath(LiveZeppelin zep)
    {
        if (_runtime == null)
        {
            return;
        }
        foreach (var def in _runtime.ProgramDefs)
        {
            if (def.PrereqAnims.Count > 0 && def.AnimName != null
                && def.Name.Equals(zep.Def.Node, StringComparison.OrdinalIgnoreCase))
            {
                GD.Print($"zep: '{zep.Def.Node}' death plays '{def.AnimName}'");
                _runtime.Play(def.AnimName);
                return;
            }
        }
        GD.Print($"zep: '{zep.Def.Node}' ships no prerequisite-gated hull death def — kill " +
                 $"recorded without choreography");
    }

    private sealed class LiveZeppelin
    {
        public LiveZeppelin(ZeppelinDef def, ZeppelinMotion motion, Node3D host)
        {
            Def = def;
            Motion = motion;
            Host = host;
            ZoneAlive = node => ZoneIsAlive(
                GasbagZones.TryGetValue(node, out var inst) ? inst : null);
        }

        public ZeppelinDef Def { get; }

        public ZeppelinMotion Motion { get; }

        public Node3D Host { get; }

        public ZeppelinDamage? Damage { get; set; }

        public bool Dead { get; set; }

        /// <summary>Whether the engines-gone signal has fired (one-way, like <see cref="Dead"/>).
        /// </summary>
        public bool EnginesDisabled { get; set; }

        /// <summary>Switched off by Instant Action's builder (F12, <see cref="Hold"/>) — placed
        /// but stepped no further, the runtime counterpart of the record's own
        /// <c>deactivated</c>.</summary>
        public bool Held { get; set; }

        /// <summary>Pool per distinct healthy node; null = zone not damageable (logged).</summary>
        public Dictionary<string, DestructibleRegistry.Instance?> GasbagZones { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The gasbag pools as a set — what the DAMAGES_ZEPPELIN gate protects.</summary>
        public HashSet<DestructibleRegistry.Instance> GasbagInstances { get; } = new();

        public Dictionary<string, DestructibleRegistry.Instance?> EngineZones { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public List<CannonZone> CannonZones { get; } = new();

        /// <summary>Zones whose kill line has printed (one line per zone).</summary>
        public HashSet<string> DeadZones { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The F19 broadside machine; null until <c>WireCannons</c>, or for a record
        /// without cannons.</summary>
        public ZeppelinBroadside? Broadside { get; set; }

        /// <summary>Cannon node name → its world node (the muzzle). Unresolved cannons are
        /// absent and out of the broadside.</summary>
        public Dictionary<string, Node3D> CannonNodes { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Cannon node name → its F18 zone pool; a null/absent pool never dies.</summary>
        public Dictionary<string, DestructibleRegistry.Instance?> CannonPools { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Gasbag aim nodes, resolved lazily when another zeppelin shoots at THIS one.</summary>
        public Dictionary<string, Node3D?> GasbagNodes { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The most recent volley's scattered fire directions (suite observability).</summary>
        public List<Vector3> LastVolleyDirs { get; } = new();

        public int BroadsideShots { get; set; }

        /// <summary>No-solution skip lines printed (rate-limited like the gate log).</summary>
        public int SkipLogged { get; set; }

        /// <summary>The aggregator's zone-aliveness view: a zone with no pool never dies.</summary>
        public Func<string, bool> ZoneAlive { get; }
    }

    private sealed class CannonZone
    {
        public CannonZone(ZeppelinCannonHealth record, DestructibleRegistry.Instance instance)
        {
            Record = record;
            Instance = instance;
        }

        public ZeppelinCannonHealth Record { get; }

        public DestructibleRegistry.Instance Instance { get; }

        public int StagesFired { get; set; }
    }
}
