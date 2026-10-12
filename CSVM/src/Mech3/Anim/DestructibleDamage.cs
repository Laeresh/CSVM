using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3.Anim;

/// <summary>One runtime's destructible damage and death. It owns the HP spend a local and a
/// replicated hit share, the <c>DAMAGE_SEQUENCE</c> stages, the death burst and the carried pose.
/// It also owns the reset, a <c>CALL_ANIMATION</c> kill, and the pool sync an
/// <c>OBJECT_ACTIVE_STATE</c> swap drives. The public entries are documented on their
/// <see cref="AnimRuntime"/> forwards, which also keep the sinks callers configure. The runtime
/// reference reads those sinks and hosts a stage cascade; the registry and <c>_rest</c> stay
/// there. Decode: docs/formats/destructibles.md.</summary>
internal sealed class DestructibleDamage
{
    // The magic sequence name a destructible's progressive-damage script carries in both the reader
    // and compiled forms (docs/formats/destructibles.md).
    private const string DamageSequenceName = "DAMAGE_SEQUENCE";

    private readonly AnimRuntime _rt;

    private readonly DestructibleRegistry _registry;

    private readonly Func<AnimEvent, AnimDefinition, Node3D?, List<Node3D>> _targets;

    private readonly Action<AnimDefinition, Node3D> _resetInstance;

    private readonly Action<AnimDefinition, Node3D?> _runDeathSlot;

    private readonly Func<AnimDefinition, Node3D, Node3D> _damageNodeOf;

    // The destructibles whose death burst is on the call stack, innermost on top (deaths nest
    // through a chained CALL_ANIMATION). A death-triggered call landing on the dying instance's
    // own anchor registers on its LocalCallTargets, so a reset can Stop and restore it too.
    // Without that, a reset arriving before the called def's own motions finish leaves its pieces
    // flown.
    private readonly Stack<DestructibleRegistry.Instance> _dying = new();

    private int _damagesLogged;

    public DestructibleDamage(AnimRuntime rt, DestructibleRegistry registry,
        Func<AnimEvent, AnimDefinition, Node3D?, List<Node3D>> targets,
        Action<AnimDefinition, Node3D> resetInstance, Action<AnimDefinition, Node3D?> runDeathSlot,
        Func<AnimDefinition, Node3D, Node3D> damageNodeOf)
    {
        _rt = rt;
        _registry = registry;
        _targets = targets;
        _resetInstance = resetInstance;
        _runDeathSlot = runDeathSlot;
        _damageNodeOf = damageNodeOf;
    }

    /// <summary>True while a death's own Start burst is on the call stack. It lets a
    /// death-triggered call relocate its callee's effect-template root onto the struck node, the
    /// way <see cref="TemplateStage{TNode}.Places"/> does.
    /// ⚠ Never widen this to the ambient world boot or RESET_STATE. Those must leave a shared
    /// template at its gamez origin, and the goldens are byte-identical on that.</summary>
    internal bool InDeathCall => _dying.Count > 0;

    /// <summary>The innermost destructible whose death burst is dispatching, or null.</summary>
    internal DestructibleRegistry.Instance? Dying => _dying.Count > 0 ? _dying.Peek() : null;

    // The three role words, spelled only here. The `dbase` ground plate is a destroyed-side role,
    // so a caller wanting the whole destroyed side asks for both.
    internal static bool IsHealthyRole(string name) => name.Contains("healthy", StringComparison.OrdinalIgnoreCase);

    internal static bool IsDestroyedRole(string name) => name.Contains("destroyed", StringComparison.OrdinalIgnoreCase);

    internal static bool IsDbaseRole(string name) => name.Contains("dbase", StringComparison.OrdinalIgnoreCase);

    internal bool DamageAt(Node? struck, float healthDamage, int shooter)
    {
        var inst = _registry.Resolve(struck);
        if (inst == null)
            return false;
        // Read live, never captured: a pool dormant at mission start wakes later and must then
        // take damage. Checked before Destroyed so out-of-the-world wins unconditionally.
        if (inst.Dormant)
            return false;
        if (inst.Status == DestructibleRegistry.State.Destroyed)
            return true;   // already dead, the death sequence owns it from here
        if (_rt.DamageReplicated)
            return true;   // struck, but another machine decides what it cost
        inst.LastShooter = shooter;
        SpendHealth(inst, inst.Health - Math.Max(0f, healthDamage), healthDamage);
        return true;
    }

    internal bool ApplyReplicatedHealth(DestructibleRegistry.Instance inst, float health)
    {
        ArgumentNullException.ThrowIfNull(inst);
        if (inst.Dormant || inst.Status == DestructibleRegistry.State.Destroyed || health >= inst.Health)
            return false;
        SpendHealth(inst, health, inst.Health - health);
        return true;
    }

    internal bool CarryState(DestructibleRegistry.Instance inst, bool destroyed, float health)
    {
        if (inst.Status == DestructibleRegistry.State.Destroyed)
            return false;
        if (!destroyed && health >= inst.Health)
            return false;
        inst.Health = destroyed ? 0f : Math.Max(0f, health);
        if (DamageSequenceOf(inst.Def) is { } stages)
            inst.DamageStage = Math.Max(inst.DamageStage, DamageStageFor(stages, inst.Health));
        if (!destroyed)
        {
            inst.Status = DestructibleRegistry.State.Damaged;
            return true;
        }
        inst.Status = DestructibleRegistry.State.Destroyed;
        ApplyDeathPose(inst);
        return true;
    }

    internal bool CollideDamageAt(Node? struck, float healthDamage)
    {
        var inst = _registry.Resolve(struck);
        if (inst == null)
        {
            return false;
        }
        bool flyThrough =
            inst.Def.Activation.Equals("WeaponOrCollideHit", StringComparison.OrdinalIgnoreCase);
        if (_rt.CollideDamageGate != null && !_rt.CollideDamageGate(inst))
        {
            return flyThrough;   // refused the damage, not the contact
        }
        DamageAt(struck, healthDamage, -1);
        return flyThrough;
    }

    internal void ResetDestructible(DestructibleRegistry.Instance inst)
    {
        var def = inst.Def;
        _rt.Stop(def.AnimName, inst.Anchor);
        // ⚠ Re-apply a called def's OWN reset state, on the anchor its call actually ran on (a
        // pooled copy, not necessarily inst.Anchor). It is never inherited from the caller, so
        // without it an ACTIVE_STATE the call flipped on sits inert but still shown.
        if (inst.ChainedDeathDef is { } chained)
        {
            _resetInstance(chained, inst.Anchor);
            inst.ChainedDeathDef = null;
        }
        // Every CALL_ANIMATION target the death dispatched onto its own anchor resets the same way.
        // The result then does not depend on whether the called def's motions had finished.
        foreach (var (local, localAnchor) in inst.LocalCallTargets)
            _resetInstance(local, localAnchor);
        inst.LocalCallTargets.Clear();
        // ⚠ The reset must stop the damage-stage effects itself. They live on the external runtime
        // and loop while the healthy node stays active, which a heal never interrupts. Take the
        // names from the def's own DAMAGE_SEQUENCE calls, never from a hardcoded list.
        if (_rt.ExternalEffectStop != null && inst.DamageStage > 0 && DamageSequenceOf(def) is { } damageSeq)
        {
            foreach (var ev in damageSeq.Events)
                if (ev.Kind == "CallAnimation" && ev.Data.Str("name") is { } stageEffect)
                    _rt.ExternalEffectStop(stageEffect);
        }
        _resetInstance(def, inst.Anchor);
        inst.Health = inst.MaxHealth;
        inst.Status = DestructibleRegistry.State.Healthy;
        inst.DamageStage = 0;
    }

    internal bool ApplyDamageStages(DestructibleRegistry.Instance inst)
    {
        if (DamageSequenceOf(inst.Def) is not { } seq)
            return false;
        int stage = DamageStageFor(seq, inst.Health);
        if (stage <= inst.DamageStage)
            return false;
        inst.DamageStage = stage;
        if (inst.Status == DestructibleRegistry.State.Healthy)
            inst.Status = DestructibleRegistry.State.Damaged;
        // A one-shot selector, not a persistent instance: the cascade carries no timed events, so
        // one zero-dt advance resolves the whole IF chain. The effect it starts becomes its own
        // live instance and this host is discarded.
        var host = new AnimInstance(inst.Def, inst.Anchor);
        host.AddRunner(seq);
        host.Advance(_rt, 0f);
        return true;
    }

    // A CALL_ANIMATION naming a destructible's own death definition is that destructible's kill,
    // run through its pool on its own anchor. A gasbag burn "destroys" the ring's other cannons
    // this way. A plain Start on the CALLER's anchor hid their guns while their pools stayed healthy
    // and the broadside kept firing. Null when the def is no sole registered pool, so a template
    // with several copies stays a plain call. Untouched when already dead or out of the world.
    internal DestructibleRegistry.Instance? KillCalledDestructible(AnimDefinition target)
    {
        DestructibleRegistry.Instance? own = null;
        var all = _registry.All;
        for (int i = 0; i < all.Count; i++)
        {
            var inst = all[i];
            if (inst.Def != target)
                continue;
            if (own != null)
                return null;
            own = inst;
        }
        if (own == null)
            return null;
        if (own.Dormant || own.Status == DestructibleRegistry.State.Destroyed)
            return own;
        own.Health = 0f;
        own.Status = DestructibleRegistry.State.Destroyed;
        own.LastShooter = -1;
        // In the `damage:` family on purpose, since this kill spends no HP. Without a line of its
        // own, a sweep for what died reads a demolished part as one nobody ever touched.
        Log.Info("anim", $"damage: {AnimRuntime.NameOf(own.Anchor)} DESTROYED by a call to '{target.AnimName ?? target.Name}', death sequence run");
        if (HealthyNodeNameOf(own.Def) is { } healthyNode)
            _rt.DestructibleKilled?.Invoke(healthyNode);
        RunDeathSequence(own);
        return own;
    }

    // Keeps a destructible's HP pool in step with a healthy/destroyed OBJECT_ACTIVE_STATE swap
    // dispatched outside DamageAt's own kill. A start-state script authoring an object destroyed
    // early is one. Without this the pool stays Healthy at full HP while the node reads destroyed.
    // A later hit then replays the whole death on an object that already looks dead. A live kill
    // reaches this event after DamageAt set the pool, so the status guards make it a no-op there.
    internal void SyncDestructiblePool(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        if (_registry.Get(def, anchor) is not { } inst)
            return;
        var name = RoleName(ev);
        bool active = ev.Data.Bool("state");
        bool dbaseRole = IsDbaseRole(name);
        bool destroyedRole = dbaseRole || IsDestroyedRole(name);
        bool healthyRole = IsHealthyRole(name);
        // A RESET_STATE is the baseline of a whole object, so a `dbase` switched on there is the
        // ground under it and never half a death.
        if (active && dbaseRole && AuthoredInResetState(def, ev))
            return;
        if (active && dbaseRole && HealthyRootStands(inst))
            return;
        if ((active && destroyedRole) || (!active && healthyRole))
        {
            if (inst.Status != DestructibleRegistry.State.Destroyed)
            {
                inst.Health = 0f;
                inst.Status = DestructibleRegistry.State.Destroyed;
            }
        }
        else if ((active && healthyRole) || (!active && destroyedRole))
        {
            // Dying is not reviving. A wreck that clears itself away switches its own destroyed-role
            // nodes back off, so only a baseline or another script may put the pool back.
            if (inst.Status == DestructibleRegistry.State.Destroyed
                && !AuthoredInDeathChoreography(def, ev))
            {
                inst.Health = inst.MaxHealth;
                inst.Status = DestructibleRegistry.State.Healthy;
            }
        }
    }

    private static AnimSequence? DamageSequenceOf(AnimDefinition def) =>
        def.Sequences.FirstOrDefault(s =>
            string.Equals(s.Name, DamageSequenceName, StringComparison.OrdinalIgnoreCase));

    // How many of a DAMAGE_SEQUENCE's health thresholds hp has fallen at or below, the object's
    // current damage stage. Monotonic in falling HP, so it is a safe escalation gate.
    private static int DamageStageFor(AnimSequence seq, float hp)
    {
        int stage = 0;
        foreach (var ev in seq.Events)
        {
            if (ev.Kind != "If" && ev.Kind != "Elseif")
                continue;
            if (DamageThreshold(ev.Data.Obj("condition")) is { } t && hp <= t)
                stage++;
        }
        return stage;
    }

    // The HP a health condition first becomes true at as HP falls: ANIM_HEALTH's operand, or a
    // range's upper bound. EvaluateCondition tests the lower bound when the cascade runs. Null for
    // a non-health condition, which does not stage.
    private static float? DamageThreshold(AnimData? condition)
    {
        if (condition?.Union() is not { } union)
            return null;
        var (kind, value) = union;
        return kind switch
        {
            "AnimHealth" => AnimData.AsNum(value),
            "AnimHealthRange" => value is Dictionary<string, object?> fields
                                 ? new AnimData(fields).Num("max")
                                 : null,
            _ => null,
        };
    }

    // The node name an event targets, node for most compiled kinds (ObjectMotion), name for others
    // (ObjectActiveState's hand-authored shape, ObjectMotionFromTo,
    // ObjectOpacityFromTo/ObjectOpacityState). Matches the exact healthy/destroyed/dbase role words
    // where that matters, never a _dest suffix (docs/formats/destructibles.md).
    private static string RoleName(AnimEvent ev) => ev.Data.Str("node") ?? ev.Data.Str("name") ?? "";

    // Does this event come from the definition's RESET_STATE rather than one of its sequences?
    // Reference identity on the authored event, since a baseline and a death name the same roles.
    private static bool AuthoredInResetState(AnimDefinition def, AnimEvent ev) =>
        def.ResetState is { } reset && reset.Events.Contains(ev);

    // The name of def's own healthy-role node, for DestructibleKilled. It is the node an
    // OBJECT_ACTIVE_STATE switches off in def's own Initial sequences (the visible-death case). Else
    // it is the one RESET_STATE holds ACTIVE, for the swap ApplyDeathSwap plays instead. Null for
    // a def that authors no healthy/destroyed pair at all, which most destructibles do not.
    private static string? HealthyNodeNameOf(AnimDefinition def)
    {
        foreach (var seq in def.Sequences.Where(s => !s.OnCallOnly))
            foreach (var ev in seq.Events)
                if (ev.Kind == "ObjectActiveState" && !ev.Data.Bool("state") && IsHealthyRole(RoleName(ev)))
                    return RoleName(ev);
        if (def.ResetState is { } reset)
            foreach (var ev in reset.Events)
                if (ev.Kind == "ObjectActiveState" && IsHealthyRole(RoleName(ev)))
                    return RoleName(ev);
        return null;
    }

    // Does target's own sequences author the healthy/destroyed swap, an OBJECT_ACTIVE_STATE that
    // activates a destroyed/dbase-role node or deactivates a healthy-role one? Used by
    // ChainedSwapTarget to find a CALL_ANIMATION target that owns the swap the caller's own
    // RESET-derived fallback would otherwise fire early.
    private static bool AuthorsSwap(AnimDefinition target) =>
        target.Sequences.Any(seq => seq.Events.Any(ev =>
        {
            if (ev.Kind != "ObjectActiveState")
                return false;
            var name = RoleName(ev);
            bool active = ev.Data.Bool("state");
            return (active && (IsDestroyedRole(name) || IsDbaseRole(name)))
                || (!active && IsHealthyRole(name));
        }));

    // Does def's own Initial sequences author a visible death on a node outside the
    // healthy/destroyed/dbase role set: a piece moved, faded or switched off? ⚠ RunDeathSequence
    // uses this to withhold ApplyDeathSwap's RESET-derived rescue. A def whose death look is
    // authored that way must keep it. The rescue is only for a def with nothing but puffer calls,
    // where the kill would otherwise be invisible.
    private static bool AuthorsVisibleDeath(AnimDefinition def) =>
        def.Sequences.Where(s => !s.OnCallOnly).Any(seq => seq.Events.Any(ev =>
        {
            bool isDeathKind = ev.Kind is "ObjectMotionFromTo" or "ObjectMotion"
                or "ObjectOpacityFromTo" or "ObjectOpacityState"
                or "ObjectActiveState";
            if (!isDeathKind)
                return false;
            var name = RoleName(ev);
            if (name.Length == 0)
                return false;
            if (IsHealthyRole(name) || IsDestroyedRole(name) || IsDbaseRole(name))
                return false;
            // An ACTIVE_STATE only counts switching a piece OFF. Turning one ON authors nothing
            // visible on its own, and would flag every def with an unrelated startup toggle.
            return ev.Kind != "ObjectActiveState" || !ev.Data.Bool("state");
        }));

    // ⚠ Follow CALL_SEQUENCE into def's own ON_CALL sequences. Nothing replays a call in a pose
    // applied without choreography, so a piece hidden from a called sequence is left standing.
    // The `susp_bridge` def parks part1 and part5 in part1_fire_puffer and part5_fire_puffer.
    // Breadth-first over a seen set, because the authored call graph is not required to be acyclic.
    private static IEnumerable<AnimSequence> OwnDeathSequencesOf(AnimDefinition def)
    {
        var seen = new HashSet<AnimSequence>();
        var pending = new Queue<AnimSequence>();
        foreach (var seq in def.Sequences.Where(s => !s.OnCallOnly))
            if (seen.Add(seq))
                pending.Enqueue(seq);
        if (def.DeathSlot is { } slot && seen.Add(slot))
            pending.Enqueue(slot);
        while (pending.Count > 0)
        {
            var seq = pending.Dequeue();
            yield return seq;
            foreach (var ev in seq.Events)
            {
                if (ev.Kind != "CallSequence" || ev.Data.Str("name") is not { } called)
                    continue;
                foreach (var target in def.Sequences)
                    if (target.Name.Equals(called, StringComparison.OrdinalIgnoreCase)
                        && seen.Add(target))
                        pending.Enqueue(target);
            }
        }
    }

    // Does this event come from the definition's own death choreography, one of the sequences
    // OwnDeathSequencesOf names? Reference identity on the authored event, and a test of where it
    // was authored rather than of what is dying. A death's later events land minutes after its
    // burst has returned. ⚠ Read the chain through that one method. A wreck clearing itself away
    // from a called ON_CALL sequence is as much a death as one doing it from an Initial sequence.
    private static bool AuthoredInDeathChoreography(AnimDefinition def, AnimEvent ev) =>
        OwnDeathSequencesOf(def).Any(seq => seq.Events.Contains(ev));

    // The one place a destructible's pool is lowered, shared by a local hit and a replicated one
    // so the two cannot stage or die differently.
    private void SpendHealth(DestructibleRegistry.Instance inst, float health, float healthDamage)
    {
        float before = inst.Health;
        int stageBefore = inst.DamageStage;
        inst.Health = Math.Max(0f, health);
        ApplyDamageStages(inst);
        bool destroyed = inst.Health <= 0f;
        if (destroyed)
        {
            inst.Status = DestructibleRegistry.State.Destroyed;
            if (HealthyNodeNameOf(inst.Def) is { } healthyNode)
                _rt.DestructibleKilled?.Invoke(healthyNode);
            RunDeathSequence(inst);
        }
        // ⚠ Never let the ceiling swallow a death. It exists to keep a firefight's chip hits out of
        // the log, and which parts died is what a sortie is read back for.
        if (destroyed || _damagesLogged < 12)
        {
            if (!destroyed)
                _damagesLogged++;
            Log.Info("anim", $"damage: -{healthDamage:0.##} on {AnimRuntime.NameOf(inst.Anchor)} HP {before:0.##}→{inst.Health:0.##}{(destroyed ? " DESTROYED, death sequence run" : $" [stage {inst.DamageStage}]")}");
        }
        if (destroyed || inst.DamageStage != stageBefore)
            _rt.DestructibleDamaged?.Invoke(inst);
        else if (inst.Health < before)
            _rt.DestructibleChipped?.Invoke(inst);
    }

    // Runs a destructible's death the instant its HP reaches zero.
    // ⚠ Play ALL the def's Initial sequences through Start; never try to pick "the death sequence"
    // out by name. The swap sits in a sequence whose name varies and is only reliably Initial
    // (docs/formats/destructibles.md).
    // ⚠ Withhold the ApplyDeathSwap fallback when the swap is one CALL_ANIMATION down, or when the
    // def authors its own visible death. Firing it blanks a wreck early or swaps an archway.
    private void RunDeathSequence(DestructibleRegistry.Instance inst)
    {
        _dying.Push(inst);
        try
        {
            // The same span InDeathCall brackets as the whole burst.
            using (PerfSample.Scope(PerfSite.DebrisSpawn))
            {
                _rt.Start(inst.Def, inst.Anchor, protectSelfInvalidate: true);
                _runDeathSlot(inst.Def, inst.Anchor);
            }
        }
        finally
        {
            _dying.Pop();
        }
        if (ChainedSwapTarget(inst.Def) is { } chained)
        {
            inst.ChainedDeathDef = chained;
            return;
        }
        if (AuthorsVisibleDeath(inst.Def))
            return;
        ApplyDeathSwap(inst);
    }

    // The first CALL_ANIMATION target, one level down from def's own Initial sequences, whose OWN
    // sequences author the healthy/destroyed swap. That is an OBJECT_ACTIVE_STATE activating a
    // destroyed/dbase-role node or deactivating a healthy-role one. Resolved via the same
    // program lookup the CALL_ANIMATION dispatch itself uses. Null for the ~90%/~10% cases the
    // def's own sequences/RESET_STATE already cover (gate1, the AA guns, everything else).
    private AnimDefinition? ChainedSwapTarget(AnimDefinition def)
    {
        foreach (var seq in def.Sequences.Where(s => !s.OnCallOnly))
            foreach (var ev in seq.Events)
                if (ev.Kind == "CallAnimation" && ev.Data.Str("name") is { } callName)
                    foreach (var target in _rt.DefsFor(callName))
                        if (AuthorsSwap(target))
                            return target;
        return null;
    }

    // The generic healthy-to-destroyed swap, for destructibles that declare the pair but author no
    // explicit swap sequence. ⚠ Read it off the def's own RESET_STATE targets, never a world-wide
    // name scan. Apply it only when RESET names a destroyed node, since an object with no destroyed
    // variant must be left intact rather than blanked. Match the exact role words, not a suffix.
    private void ApplyDeathSwap(DestructibleRegistry.Instance inst)
    {
        if (inst.Def.ResetState is not { } reset)
            return;
        bool hasDestroyed = reset.Events.Any(ev => ev.Kind == "ObjectActiveState" && IsDestroyedRole(RoleName(ev)));
        if (!hasDestroyed)
            return;
        foreach (var ev in reset.Events)
        {
            if (ev.Kind != "ObjectActiveState")
                continue;
            var name = RoleName(ev);
            bool? active =
                IsHealthyRole(name) ? false
                : IsDestroyedRole(name) || IsDbaseRole(name) ? true
                : null;
            if (active is not { } state)
                continue;
            foreach (var node in _targets(ev, inst.Def, inst.Anchor))
                AnimRuntime.SetSubtreeActive(node, state);
        }
    }

    // The pose a death ends in, read off the sequences RunDeathSequence would play. It is every
    // OBJECT_ACTIVE_STATE switching a node off, and the destroyed/dbase role nodes switched on.
    // ⚠ Leave a non-role piece switched on mid-death off; it flies and is hidden, so switching it
    // on parks debris at its rest pose. A def with no role swap of its own takes the RESET-derived
    // one, under the same visible-death withholding RunDeathSequence applies.
    private void ApplyDeathPose(DestructibleRegistry.Instance inst)
    {
        bool swapped = false;
        foreach (var (def, seq) in DeathSequencesOf(inst.Def))
        {
            foreach (var ev in seq.Events)
            {
                if (ev.Kind != "ObjectActiveState")
                    continue;
                var name = RoleName(ev);
                bool active = ev.Data.Bool("state");
                bool destroyedRole = IsDestroyedRole(name) || IsDbaseRole(name);
                bool healthyRole = IsHealthyRole(name);
                if (active && !destroyedRole)
                    continue;
                foreach (var node in _targets(ev, def, inst.Anchor))
                {
                    _rt.SetTargetActive(node, active);
                    swapped |= destroyedRole || healthyRole;
                }
            }
        }
        if (!swapped && !AuthorsVisibleDeath(inst.Def))
            ApplyDeathSwap(inst);
    }

    // The sequences a death plays, with the def each resolves its targets through. They are the
    // def's own Initial sequences, its compiled destruction slot, and the ON_CALL sequences those
    // two reach through CALL_SEQUENCE. Every sequence of a chained swap target follows, since
    // AuthorsSwap accepts its swap in an ON_CALL sequence too.
    private IEnumerable<(AnimDefinition Def, AnimSequence Seq)> DeathSequencesOf(AnimDefinition def)
    {
        foreach (var seq in OwnDeathSequencesOf(def))
            yield return (def, seq);
        if (ChainedSwapTarget(def) is { } chained)
            foreach (var seq in chained.Sequences)
                yield return (chained, seq);
    }

    // Is this pool's own healthy geometry still standing? A `dbase` node is the wreck's ground
    // base, so switching it on is normally half of a death. The two 8-inch cannons author it ON in
    // their RESET_STATE beside `healthy` ACTIVE. There it is a concrete plinth under a live gun,
    // not a death (docs/formats/destructibles.md). Every genuine death that touches `dbase`
    // also switches its healthy-role node OFF, so the death still reads.
    private bool HealthyRootStands(DestructibleRegistry.Instance inst) =>
        inst.Def.RootName is { Length: > 0 } root
        && IsHealthyRole(root)
        && _damageNodeOf(inst.Def, inst.Anchor).Visible;
}
