using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

namespace CrimsonSkies.Mech3;

/// <summary>
/// The animation engine: binds an <see cref="AnimProgram"/> to a built world and executes
/// it. Replaces the start-state-only applier this project had before (Run-2 item 8), whose
/// node resolution and INACTIVE semantics it keeps verbatim — those are user-verified and
/// were never the limitation.
///
/// At world build it performs the same four bootstrap passes as before:
///  1. every anchored definition's RESET_STATE (base states — hides the `destroyed`
///     building/vehicle variants the gamez stores overlaid on their healthy twins),
///  2. ACTIVATION ON_STARTUP definitions (zepstate.json — the per-mission object roster),
///  3. the mission's startanims.json NEW_GAME_START animations, in order,
///  4. a logged safety net hiding any still-visible `destroyed` subtree nothing covered.
///
/// The difference is that 2 and 3 now *run* rather than being posed at their end state: a
/// definition becomes a live instance with a clock, so hangar doors swing open over their
/// authored 9 s and the C1 train drives its 327 s SI-script track loop. Anything the engine
/// cannot yet act on is counted per event kind and reported once, never fatal — that is
/// what lets the remaining event types land incrementally without reshaping this class.
///
/// "Inactive" = hidden AND non-collidable (crashing into an invisible zeppelin would be
/// worse than the visual bug). Definitions resolve to built nodes by their ORIGINAL gamez
/// names (SceneBuilder stores them in the `cs_name` meta — Godot mangles duplicate sibling
/// names, so Node.Name is unreliable).
/// </summary>
public sealed partial class AnimRuntime : Node
{
    /// <summary>Original-name metadata key SceneBuilder stamps on every built Node3D.</summary>
    public const string NameMeta = "cs_name";

    /// <summary>Flat gamez node-index metadata key SceneBuilder stamps on every built
    /// Node3D — the exact binding compiled definitions reference (see
    /// <see cref="AnimDefinition.NodeRefs"/>).</summary>
    public const string IndexMeta = "cs_index";

    /// <summary>ANIMATION_ROOT_NAME matches above this count are generic per-object roots
    /// ('healthy' appears 217× in C1) — those defs belong to game objects (planes, zeppelin
    /// parts), not to world nodes. The genuine building templates lift ≤ 9 instances.</summary>
    private const int MaxRootLift = 16;

    private readonly List<(Node3D Node, string SrcName)> _index = new();
    private readonly Dictionary<string, Func<string, bool>> _matcherCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Node3D, Transform3D> _rest = new(); // authored pose per touched node
    private readonly List<AnimInstance> _instances = new();
    private readonly Dictionary<string, int> _unhandled = new(StringComparer.Ordinal);
    private Node3D _root = null!;
    private AnimProgram _program = null!;

    private int _opsApplied, _opsUnresolved;

    /// <summary>Live animation instances currently running (diagnostics).</summary>
    public int ActiveInstances => _instances.Count;

    /// <summary>
    /// Binds a program to a built world, runs the bootstrap passes, and returns the runtime
    /// node to add to the scene tree (it advances live instances in _Process). Add it to the
    /// world root; it holds no state that survives a session teardown.
    /// </summary>
    public static AnimRuntime Apply(Node3D worldRoot, AnimProgram program)
    {
        var runtime = new AnimRuntime { Name = "AnimRuntime" };
        runtime.Bind(worldRoot, program);
        return runtime;
    }

    /// <summary>Runs the bootstrap passes against a built world. Separate from
    /// <see cref="Apply"/> so a caller can set build-time-only collaborators (notably
    /// <see cref="PufferFactory"/>, which depends on the session TextureArchive's lifetime)
    /// before the passes fire the events that need them.</summary>
    public void Bind(Node3D worldRoot, AnimProgram program)
    {
        Name = "AnimRuntime";
        Bootstrap(worldRoot, program);
    }

    /// <summary>--debug-anim: log every live motion's target and pose once a second, so a
    /// headless run can verify that (say) the train actually drives its loop.</summary>
    public bool DebugMotions;

    /// <summary>
    /// Our answer to the data's <c>ANIMATION_LOD</c> condition — a project quality setting,
    /// not a fact about the world. The original hid detail on slow hardware; every
    /// LOD-gated branch in this install asks for the same tier (the reader spells it
    /// <c>HIGH</c>, which compiles to 2, and 2 is the only value that appears in all 8
    /// chapters), so the default passes them all. <c>--anim-lod=N</c> lowers it for A/B
    /// comparison.
    /// </summary>
    public int QualityLod = HighLod;

    /// <summary>The reader's <c>ANIMATION_LOD HIGH</c> — see <see cref="QualityLod"/>.</summary>
    public const int HighLod = 2;

    /// <summary>Where the player is, for <c>PLAYER_RANGE</c> conditions. Supplied by the
    /// session (the flown aircraft, or the spectator camera); absent → the viewport camera,
    /// and failing that the world origin. During the bootstrap passes there is no camera
    /// yet, which is harmless: every PLAYER_RANGE definition in this install re-polls from a
    /// <c>Loop{-1}</c>, so a bootstrap-time miss corrects on the next frame.</summary>
    public Func<Vector3>? PlayerPosition;

    /// <summary>Answers the data's <c>PLAYER_1ST_PERSON</c> condition. No cockpit view
    /// exists yet (backlog), so false.</summary>
    public bool FirstPerson;

    // RANDOM_WEIGHT dice. A field rather than GD.Randf() so a seed can be pinned later if a
    // reproducible --debug-anim run is ever wanted.
    private readonly Random _rng = new();

    private void Bootstrap(Node3D worldRoot, AnimProgram program)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _root = worldRoot;
        _program = program;
        IndexWorld(worldRoot);
        long indexMs = sw.ElapsedMilliseconds;

        // Pass 1: base states. Anchored defs only — a def whose NAME matches nothing in this
        // world (player-plane anims, cutscene rigs) must not stomp globally-resolved bare
        // names like 'destroyed'.
        int anchored = 0;
        foreach (var def in program.Defs)
        {
            var anchors = Anchors(def);
            if (anchors.Count == 0)
                continue;
            anchored++;
            if (def.ResetState != null)
                foreach (var anchor in anchors)
                    ApplyInstant(def.ResetState.Events, def, anchor);
        }
        long resetMs = sw.ElapsedMilliseconds;

        // Pass 2: ON_STARTUP definitions run for real (zepstate's roster is instantaneous
        // ObjectActiveStates, so this still settles on frame 0 for those).
        int startupRun = 0;
        foreach (var def in program.Defs.Where(d => d.OnStartup))
            foreach (var anchor in Anchors(def))
            {
                Start(def, anchor);
                startupRun++;
            }

        // Pass 3: the mission's start animations, by ANIMATION_NAME, in list order.
        var ran = new List<string>();
        var missing = new List<string>();
        foreach (var animName in program.StartAnims)
        {
            var matches = program.ByAnimName(animName);
            if (matches.Count == 0)
            {
                missing.Add(animName);
                continue;
            }
            ran.Add(animName);
            foreach (var def in matches)
            {
                var anchors = Anchors(def);
                if (anchors.Count == 0)
                    anchors.Add(null); // global resolution: the def names world nodes directly
                foreach (var anchor in anchors)
                {
                    if (def.ResetState != null)
                        ApplyInstant(def.ResetState.Events, def, anchor);
                    Start(def, anchor);
                }
            }
        }

        // Pass 4: safety net for destroyed-variant subtrees no definition covered.
        var netHidden = HideUncoveredDestroyed();

        GD.Print($"anim: {program.Defs.Count} defs ({program.CompiledCount} compiled, " +
                 $"{program.ReaderCount} reader), {program.ScriptPoolCount} SI scripts; " +
                 $"{anchored} anchored, {_opsApplied} state ops applied, {_opsUnresolved} unresolved");
        GD.Print($"anim: {startupRun} ON_STARTUP + {ran.Count} start anims running, " +
                 $"{_instances.Count} live instance(s), {_motions.Count} live motion(s) " +
                 $"[index {indexMs} ms, reset states {resetMs - indexMs} ms, " +
                 $"start {sw.ElapsedMilliseconds - resetMs} ms]");
        if (program.MissionLibrarySkipped.Count > 0)
            GD.Print($"anim: {program.MissionLibrarySkipped.Count} mission-scope reader def(s) " +
                     $"not in this mission's compiled manifest, so not instantiated: " +
                     string.Join(", ", program.MissionLibrarySkipped.Take(8)) +
                     (program.MissionLibrarySkipped.Count > 8 ? ", …" : ""));
        if (ran.Count > 0 || missing.Count > 0)
            GD.Print($"anim: start anims [{string.Join(", ", ran)}]" +
                     (missing.Count > 0 ? $", undefined here: [{string.Join(", ", missing)}]" : ""));
        if (_puffers.Count > 0)
            GD.Print($"anim: {_puffers.Count} puffer emitter(s): " +
                     string.Join(", ", _puffers.Keys.Select(k => k.Name).Distinct()));
        if (netHidden.Count > 0)
            GD.Print($"anim: safety net hid {netHidden.Count} uncovered destroyed subtree(s): " +
                     string.Join(", ", netHidden.Take(10)) + (netHidden.Count > 10 ? ", …" : ""));
        ReportConditions();
        ReportUnhandled();
    }

    private readonly Dictionary<int, Node3D> _byIndex = new();

    private void IndexWorld(Node3D worldRoot)
    {
        void Walk(Node3D n)
        {
            var srcName = n.HasMeta(NameMeta) ? n.GetMeta(NameMeta).AsString() : n.Name.ToString();
            _index.Add((n, srcName));
            if (n.HasMeta(IndexMeta))
                _byIndex.TryAdd((int)n.GetMeta(IndexMeta), n);
            foreach (var child in n.GetChildren())
                if (child is Node3D c)
                    Walk(c);
        }
        Walk(worldRoot);
    }

    // ---- live execution ----

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        // Motions advance ONCE per frame, here — not from the sequence runners, which would
        // apply dt once per running sequence and run the train at 4× speed.
        TickMotions(dt);
        TickPuffers(dt);
        if (DebugMotions)
            LogMotions(dt);
        // Instances can finish (and CallAnimation can add) during the walk, so iterate a copy.
        for (int i = _instances.Count - 1; i >= 0; i--)
        {
            var inst = _instances[i];
            inst.Advance(this, dt);
            if (inst.Finished)
                _instances.RemoveAt(i);
        }
    }

    /// <summary>Starts a definition on one anchor (null = resolve its node names globally),
    /// running every sequence that is not ACTIVATION ON_CALL. Starting a def that is already
    /// live on the same anchor RESTARTS it — CALL_ANIMATION deliberately does not take this
    /// path for a running animation (see its case in <see cref="Dispatch"/>).</summary>
    public void Start(AnimDefinition def, Node3D? anchor)
    {
        // CALL_ANIMATION chains are data, and the data can (and in some chapters does) form
        // cycles: A calls B calls A. Starting an instance fires its t=0 events immediately,
        // so an unguarded cycle recurses until the stack dies. Bound the depth instead —
        // legitimate chains in this install are 2–3 deep.
        if (_startDepth >= MaxStartDepth)
        {
            Count("CallAnimation(depth limit)");
            return;
        }
        Stop(def.AnimName, anchor);
        var inst = new AnimInstance(def, anchor);
        foreach (var seq in def.Sequences.Where(s => !s.OnCallOnly))
            inst.Runners.Add(new SequenceRunner(seq));
        if (inst.Runners.Count == 0)
            return;
        _instances.Add(inst);
        // Fire whatever is due at t=0 immediately, so instantaneous sequences (zepstate's
        // active-state roster) settle during the build rather than one frame later.
        _startDepth++;
        try
        {
            inst.Advance(this, 0f);
        }
        finally
        {
            _startDepth--;
        }
        if (inst.Finished)
            _instances.Remove(inst);
    }

    private const int MaxStartDepth = 8;
    private int _startDepth;

    /// <summary>Is this definition already running on this anchor? (Instance identity is
    /// (definition, anchor) throughout.)</summary>
    private bool IsLive(AnimDefinition def, Node3D? anchor) =>
        _instances.Any(i => i.Def == def && i.Anchor == anchor);

    /// <summary>Stops every live instance of an animation name (optionally only on one
    /// anchor). Used by STOP_ANIMATION and by restart-on-call.</summary>
    public void Stop(string? animName, Node3D? anchor = null)
    {
        if (string.IsNullOrEmpty(animName))
            return;
        _instances.RemoveAll(i =>
            string.Equals(i.Def.AnimName, animName, StringComparison.OrdinalIgnoreCase)
            && (anchor == null || i.Anchor == anchor));
    }

    /// <summary>Applies a list of events with no clock — the RESET_STATE path, where every
    /// op is a base state and timed motions collapse to their end pose.</summary>
    private void ApplyInstant(List<AnimEvent> events, AnimDefinition def, Node3D? anchor)
    {
        foreach (var ev in events)
            Dispatch(ev, def, anchor, instant: true, out _);
    }

    // ---- the dispatch table ----

    /// <summary>
    /// Executes one event. Returns the event's duration in seconds via
    /// <paramref name="duration"/> — 0 for an instantaneous state change, the run time for a
    /// timed motion, the script length for an SI script. The sequence runner uses it to
    /// decide when the next event is due.
    /// Returns false only for control-flow events the runner must interpret itself.
    /// </summary>
    private bool Dispatch(AnimEvent ev, AnimDefinition def, Node3D? anchor, bool instant, out float duration)
    {
        duration = 0f;
        switch (ev.Kind)
        {
            case "ObjectActiveState":
                foreach (var t in Targets(ev, def, anchor))
                {
                    SetSubtreeActive(t, ev.Data.Bool("state"));
                    _opsApplied++;
                }
                return true;

            case "ObjectTranslateState":
                foreach (var t in Targets(ev, def, anchor))
                    PoseTranslate(t, ev.Data.Vec3("state"), ev.Data.Bool("relative"));
                return true;

            case "ObjectRotateState":
                foreach (var t in Targets(ev, def, anchor))
                    PoseRotate(t, ev.Data.Vec3("state"));
                return true;

            case "ObjectScaleState":
                foreach (var t in Targets(ev, def, anchor))
                    PoseScale(t, ev.Data.Vec3("state"));
                return true;

            case "ObjectMotionFromTo":
            {
                float runTime = ev.Data.Num("run_time") ?? 0f;
                foreach (var t in Targets(ev, def, anchor))
                {
                    var tween = FromToMotion.Create(this, t, ev.Data, runTime);
                    if (tween == null)
                        continue;
                    if (instant || runTime <= 0f)
                        tween.Seek(runTime); // RESET_STATE / zero-length: land on the end pose
                    else
                        AddMotion(tween);
                    _opsApplied++;
                }
                duration = instant ? 0f : runTime;
                return true;
            }

            case "ObjectMotionSiScript":
            {
                int slot = (int)(ev.Data.Num("index") ?? 0f);
                var script = _program.ScriptFor(def, slot);
                if (script == null)
                {
                    Count("ObjectMotionSiScript(no script)");
                    return true;
                }
                foreach (var t in Targets(ev, def, anchor))
                {
                    var playback = new ScriptPlayback(t, script);
                    if (instant)
                        playback.Seek(0f); // pose at the script's first frame
                    else
                        AddMotion(playback);
                    _opsApplied++;
                    duration = Mathf.Max(duration, script.Duration);
                }
                return true;
            }

            // Control flow is the runner's business, not the table's.
            case "Loop":
            case "If":
            case "Elseif":
            case "Else":
            case "Endif":
                return false;

            case "CallSequence":
                if (ev.Data.Str("name") is { } seqName)
                    CallSequence(def, anchor, seqName);
                return true;

            case "StopSequence":
                return true; // handled by the runner owning the sequence; nothing global to do

            case "CallAnimation":
                // A call does NOT restart an animation that is already live on this anchor.
                // The data's poll idiom is `If <condition> → CallAnimation; Endif; Loop{-1}`,
                // which re-issues the call on EVERY frame the condition holds — C1/MP1's
                // `rearm_node_1/call_door` fires `rearm_door_close` for as long as the player
                // stays within 25 m. Restarting there pins the 2 s door at its first frame for
                // as long as you hover, which is exactly backwards. (Nothing regresses: the
                // bootstrap passes call Start directly, and the hangar-door pair that relies
                // on "later registration wins" resolves through AddMotion, not through this.)
                if (ev.Data.Str("name") is { } callName)
                    foreach (var target in _program.ByAnimName(callName))
                        if (!IsLive(target, anchor))
                            Start(target, anchor);
                return true;

            case "StopAnimation":
            case "InvalidateAnimation":
                Stop(ev.Data.Str("name"));
                return true;

            case "PufferState":
                HandlePufferState(ev, def, anchor);
                return true;

            default:
                // Sound / light / opacity / texture-cycle / FBFX / camera and the
                // rest: dispatched, counted, and reported once per kind. Adding a handler is
                // a case above and nothing else.
                Count(ev.Kind);
                return true;
        }
    }

    // ---- PUFFER_STATE ----

    /// <summary>
    /// Builds a <see cref="Effects.Puffer"/> for a state, or null. Supplied by PlaneViewer and
    /// valid only DURING the world build: a puffer bakes its texture atlas at construction
    /// from the session's <see cref="TextureArchive"/>, which is disposed when the build ends.
    /// Cleared afterwards, so a later request is reported rather than silently faulting on a
    /// closed zip handle. In practice every PUFFER_STATE that matters fires during the
    /// bootstrap passes (measured on C1: the waterfall mist, the train's steam, two truck
    /// dust plumes — nothing else reaches one).
    /// </summary>
    public Func<Effects.PufferState, Effects.Puffer?>? PufferFactory;

    /// <summary>Where built puffers are parented (the session root, not the animated node —
    /// their particles live in world space and must not be dragged by the emitter's motion).</summary>
    public Node? PufferParent;

    // One emitter per (puffer name, emitter node). Definitions re-assert their PUFFER_STATE
    // every loop iteration — C1's waterfall is [PufferState ×3, Loop{-1}] — so the handler has
    // to be idempotent: re-asserting an already-running emitter must be a no-op, not a
    // second emitter.
    private readonly Dictionary<(string Name, Node3D Node), Effects.Puffer> _puffers = new();
    private readonly List<(Effects.Puffer Puffer, Node3D Node)> _activePuffers = new();

    private void HandlePufferState(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        if (ev.Data.Str("name") is not { } pufferName)
            return;
        // ACTIVE_STATE: 1 = start emitting, 0 = stop. The attach point is AT_NODE; note it is
        // NOT the event's "name" (that is the puffer's own name, a different namespace).
        bool on = (ev.Data.Num("active_state") ?? 0f) >= 1f;
        var host = ev.Data.Str("at_node") is { } atNode
            ? ResolveOne(atNode, def, anchor)
            : anchor;
        if (host == null)
        {
            Count("PufferState(no host node)");
            return;
        }

        var key = (pufferName, host);
        if (!on)
        {
            if (_puffers.TryGetValue(key, out var running))
            {
                running.SustainEnd();
                _activePuffers.RemoveAll(a => a.Puffer == running);
            }
            return;
        }
        if (_puffers.ContainsKey(key))
            return; // already running — the loop re-asserting it

        if (PufferFactory == null)
        {
            Count("PufferState(after build)");
            return;
        }
        var state = Effects.PufferState.FromAnimEvent(ev.Data);
        // A PUFFER_STATE carrying no textures is an adjust/stop stub that re-asserts a puffer
        // some other event defines — the readers have the same idiom, which is why
        // PufferState.FindInReader tests for a "fully defined" state. There is nothing to
        // build from it; C1's truck1dust_puffer and black_exhaust_puffer are the two here.
        if (state.Textures.Count == 0 && state.TextureSequence.Count == 0)
        {
            Count($"PufferState(stub, no textures: {state.Name})");
            return;
        }
        if (PufferFactory(state) is not { } puffer)
        {
            // Name it: a puffer whose textures are absent from this chapter's archive is a
            // data-coverage fact worth being able to look up, not an anonymous count.
            Count($"PufferState(no texture: {state.Name})");
            return;
        }
        (PufferParent ?? _root).AddChild(puffer);
        _puffers[key] = puffer;
        _activePuffers.Add((puffer, host));
        _opsApplied++;
    }

    // Drives every running emitter from its host node's current world pose. Emitters follow
    // moving nodes (the train's smokestack travels the whole track loop), so this is per frame.
    private void TickPuffers(float dt)
    {
        for (int i = _activePuffers.Count - 1; i >= 0; i--)
        {
            var (puffer, node) = _activePuffers[i];
            if (!IsInstanceValid(puffer) || !IsInstanceValid(node))
            {
                _activePuffers.RemoveAt(i);
                continue;
            }
            var xform = node.GlobalTransform;
            puffer.SustainAt(xform.Origin, xform.Basis, dt);
        }
    }

    /// <summary>Resolves a single node name for this definition — the compiled symbol table
    /// first, then the reader-style name/wildcard match within the anchor's scope.</summary>
    private Node3D? ResolveOne(string name, AnimDefinition def, Node3D? anchor)
    {
        if (def.NodeRefs.TryGetValue(name, out int idx) && _byIndex.TryGetValue(idx, out var bound))
            return bound;
        var found = ResolvePath(new List<string> { name }, anchor, def.LocalNodesOnly);
        return found.Count > 0 ? found[0] : null;
    }

    private void CallSequence(AnimDefinition def, Node3D? anchor, string name)
    {
        var seq = def.Sequences.FirstOrDefault(s =>
            string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (seq == null)
        {
            Count("CallSequence(missing)");
            return;
        }
        var inst = _instances.FirstOrDefault(i => i.Def == def && i.Anchor == anchor);
        if (inst == null)
            return;
        inst.Runners.Add(new SequenceRunner(seq));
    }

    // ---- IF/ELSEIF conditions ----

    // Per condition kind: how often it evaluated true / false. Reported after the bootstrap
    // passes, which is the headless proof that (say) the refinery's AnimationLod branch is
    // now TAKEN rather than skipped.
    private readonly Dictionary<string, (int True, int False)> _conditions = new(StringComparer.Ordinal);

    /// <summary>
    /// Evaluates one IF/ELSEIF condition. Every kind the data uses is answerable — an
    /// earlier reading held them to be opaque gameplay state and skipped every branch, which
    /// meant the refinery/dock/lighthouse light sequences never ran at all. Semantics and the
    /// evidence for each are in docs/formats/anim-definitions.md; the two units that bite are
    /// that compiled <c>PlayerRange</c> is metres SQUARED (reader 270 → compiled 72900) and
    /// that compiled <c>AnimHealth</c> is a "damaged down to" threshold, so an undamaged
    /// object fails it.
    ///
    /// An unparseable or unknown condition returns false — the old skip-the-branch behaviour,
    /// which is the safe direction: a branch that should not have run poses objects wrongly.
    /// </summary>
    private bool EvaluateCondition(AnimData? condition, AnimDefinition def, Node3D? anchor)
    {
        if (condition?.Union() is not { } union)
        {
            CountCondition("<absent>", false);
            return false;
        }
        var (kind, value) = union;
        var obj = value is Dictionary<string, object?> fields ? new AnimData(fields) : null;
        float num = AnimData.AsNum(value) ?? 0f;
        bool result = kind switch
        {
            "RandomWeight" => _rng.NextDouble() < num,
            "AnimationLod" => QualityLod >= (int)num,
            // The 4-byte value slot is unused for these two: the reader form takes no
            // argument (`IF HW_RENDER`) and every compiled instance stores 0, so the
            // condition is the runtime flag itself.
            "HwRender" => true,
            "PlayerFirstPerson" => FirstPerson,
            "PlayerRange" => anchor != null
                             && WorldPos(anchor).DistanceSquaredTo(PlayerPos()) <= num,
            // ANIM_HEALTH gates damage effects: "if this object has been worn down to N".
            // Nothing in a world build damages scenery, so every object sits at its
            // definition's full health and these are uniformly false — which is correct
            // (an undamaged AA gun does not smoke). Verified: no definition using an
            // AnimHealth condition ships health 0, so full health is never below a threshold.
            "AnimHealth" => def.Health <= num,
            "AnimHealthRange" => obj != null
                                 && def.Health >= (obj.Num("min") ?? 0f)
                                 && def.Health <= (obj.Num("max") ?? 0f),
            "NodeActive" => ConditionNode(value, def, anchor) is { } n && n.Visible,
            "NodeBelowAlt" => obj != null
                              && ConditionNode(obj.Get("node_index") ?? obj.Get("node"), def, anchor)
                                 is { } n2
                              && WorldPos(n2).Y < (obj.Num("altitude") ?? 0f),
            // NODE_UNDERCOVER (the reader spells it NODE_NEAR_GROUND) needs a ground/occlusion
            // probe this class has no access to. All 473 uses sit in ON_CALL definitions the
            // bootstrap never reaches, so a false stub costs nothing today.
            "NodeUndercover" => false,
            _ => false,
        };
        CountCondition(kind, result);
        // --debug-anim: each condition spelled out (kind, operand, anchor, verdict) the first
        // time it is seen and thereafter only when its verdict FLIPS. The poll idiom —
        // `If … Endif Loop{-1}` — re-evaluates every frame, so logging every evaluation would
        // bury the log; logging the transitions is what you actually want to read (this is
        // how "the player came within 25 m and the rearm door fired" shows up).
        if (DebugMotions && Flipped(kind, anchor, result))
        {
            var at = anchor == null ? "<global>"
                : $"{NameOf(anchor)} {WorldPos(anchor).Snapped(Vector3.One)}";
            GD.Print($"anim/debug: cond {kind}({Describe(value)}) on {at} " +
                     $"[player {PlayerPos().Snapped(Vector3.One)}] → {(result ? "TRUE" : "false")}");
        }
        return result;
    }

    private readonly Dictionary<(string Kind, Node3D? Anchor), bool> _condLast = new();

    private bool Flipped(string kind, Node3D? anchor, bool result)
    {
        var key = (kind, anchor);
        if (_condLast.TryGetValue(key, out bool prev) && prev == result)
            return false;
        _condLast[key] = result;
        return true;
    }

    private static string Describe(object? value) => value switch
    {
        null => "",
        Dictionary<string, object?> d => string.Join(",", d.Select(kv => $"{kv.Key}={kv.Value}")),
        _ => value.ToString() ?? "",
    };

    private static string NameOf(Node3D n) =>
        n.HasMeta(NameMeta) ? n.GetMeta(NameMeta).AsString() : n.Name.ToString();

    /// <summary>A node's world position, valid DURING the bootstrap too. The world subtree
    /// is still detached while the bootstrap passes run (PlaneViewer parents it after the
    /// build), and Godot's <c>GlobalPosition</c> both returns identity and logs an error for
    /// a node outside the tree — one line per evaluation, which is thousands. Accumulate the
    /// local transforms instead; the world node itself rests at the origin, so the result is
    /// the same number either way.</summary>
    private static Vector3 WorldPos(Node3D node)
    {
        if (node.IsInsideTree())
            return node.GlobalPosition;
        var xform = node.Transform;
        for (var p = node.GetParent() as Node3D; p != null; p = p.GetParent() as Node3D)
            xform = p.Transform * xform;
        return xform.Origin;
    }

    private Vector3 PlayerPos()
    {
        if (PlayerPosition != null)
            return PlayerPosition();
        return IsInsideTree() && GetViewport().GetCamera3D() is { } cam ? cam.GlobalPosition : Vector3.Zero;
    }

    /// <summary>
    /// Resolves a condition's node reference. The compiled form is a **1-based index into the
    /// definition's <c>nodes</c> support array** (mech3ax resolves indices to names for every
    /// other event kind but leaves these raw); the reader form is the name itself.
    ///
    /// Two negative sentinels exist: -100 <c>MAIN_ROOT_NODE</c> and -200 <c>INPUT_NODE</c>,
    /// both meaning "the node this definition was invoked on" = our anchor. They arrive as
    /// u32 (4294967196 / 4294967096) and the JSON layer parses numbers as float32, which
    /// cannot tell those two apart — hence the magnitude test rather than an equality check.
    /// It does not matter here: both resolve to the anchor.
    /// </summary>
    private Node3D? ConditionNode(object? reference, AnimDefinition def, Node3D? anchor)
    {
        if (reference is string name)
            return string.Equals(name, "INPUT_NODE", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "MAIN_ROOT_NODE", StringComparison.OrdinalIgnoreCase)
                ? anchor
                : ResolveOne(name, def, anchor);
        if (AnimData.AsNum(reference) is not { } idx)
            return null;
        if (idx > 1e9f)
            return anchor; // negative sentinel
        int i = (int)idx;
        if (i < 1 || i > def.NodeList.Count)
            return null;
        return ResolveOne(def.NodeList[i - 1], def, anchor);
    }

    private void CountCondition(string kind, bool result)
    {
        var (t, f) = _conditions.TryGetValue(kind, out var c) ? c : (0, 0);
        _conditions[kind] = result ? (t + 1, f) : (t, f + 1);
    }

    private void ReportConditions()
    {
        if (_conditions.Count == 0)
            return;
        var parts = _conditions.OrderByDescending(kv => kv.Value.True + kv.Value.False)
            .Select(kv => $"{kv.Key} {kv.Value.True}✓/{kv.Value.False}✗");
        GD.Print($"anim: conditions evaluated (lod {QualityLod}): {string.Join(", ", parts)}");
    }

    private void Count(string kind) =>
        _unhandled[kind] = _unhandled.TryGetValue(kind, out var n) ? n + 1 : 1;

    private void ReportUnhandled()
    {
        if (_unhandled.Count == 0)
            return;
        var top = _unhandled.OrderByDescending(kv => kv.Value).Take(12)
            .Select(kv => $"{kv.Key}×{kv.Value}");
        GD.Print($"anim: {_unhandled.Count} event kind(s) not yet acted on: {string.Join(", ", top)}"
                 + (_unhandled.Count > 12 ? ", …" : ""));
    }

    // ---- active motions ----

    private readonly List<IAnimMotion> _motions = new();

    /// <summary>A motion that owns a node's pose over time (an SI script playback or a
    /// from→to tween). The runtime ticks them centrally so a node driven by two motions
    /// resolves to the later one deterministically.</summary>
    private interface IAnimMotion
    {
        Node3D Target { get; }
        bool Finished { get; }
        void Tick(float dt);
        void Seek(float t);
    }

    private float _debugClock;

    // --debug-anim: one line per live motion per second. Headless verification that things
    // actually move (and by how much) without flying a camera at them.
    private void LogMotions(float dt)
    {
        _debugClock += dt;
        if (_debugClock < 1f)
            return;
        _debugClock = 0f;
        if (_motions.Count == 0)
        {
            GD.Print("anim/debug: no live motions");
            return;
        }
        foreach (var m in _motions.Take(12))
        {
            var t = m.Target;
            var name = t.HasMeta(NameMeta) ? t.GetMeta(NameMeta).AsString() : t.Name.ToString();
            var p = t.GlobalPosition;
            // Visibility matters as much as position here: a correctly-animated node inside a
            // subtree the mission deactivated moves perfectly and renders nothing.
            bool shown = t.IsVisibleInTree();
            GD.Print($"anim/debug: {name} at ({p.X:0.0}, {p.Y:0.0}, {p.Z:0.0}) {(shown ? "visible" : "HIDDEN")}");
        }
        if (_motions.Count > 12)
            GD.Print($"anim/debug: … and {_motions.Count - 12} more");
    }

    /// <summary>Registers a motion, replacing any motion already driving the same node. An
    /// object has exactly one motion in the original, and the data relies on it: C1/IA1's
    /// startanims run `hangar3_doors` (doors to ±50) and then `mp_hangar3_open` (the same
    /// doors to ±25), where the later one is meant to win. Without this both tween the same
    /// node every frame and the outcome depends on list order.</summary>
    private void AddMotion(IAnimMotion motion)
    {
        _motions.RemoveAll(m => m.Target == motion.Target);
        _motions.Add(motion);
    }

    /// <summary>Advances every live motion. Called from the instance walk each frame.</summary>
    private void TickMotions(float dt)
    {
        for (int i = _motions.Count - 1; i >= 0; i--)
        {
            _motions[i].Tick(dt);
            if (_motions[i].Finished)
                _motions.RemoveAt(i);
        }
    }

    /// <summary>Plays a compiled SI script onto a node: per-frame cubics for translation and
    /// the half-angle quaternion composition for rotation (docs/formats/anim-definitions.md).
    /// Loops when the owning sequence loops — the runner restarts it.</summary>
    private sealed class ScriptPlayback : IAnimMotion
    {
        public Node3D Target { get; }
        private readonly SiScript _script;
        private float _t;

        public ScriptPlayback(Node3D target, SiScript script)
        {
            Target = target;
            _script = script;
            Seek(0f);
        }

        public bool Finished => _t >= _script.Duration;

        public void Tick(float dt) => Seek(_t + dt);

        public void Seek(float t)
        {
            _t = t;
            if (_script.FrameAt(Mathf.Min(t, _script.Duration)) is not { } frame)
                return;
            float dt = Mathf.Max(0f, t - frame.StartTime);
            var basis = Target.Transform.Basis;
            var origin = Target.Transform.Origin;
            if (frame.Rotate != null)
                basis = new Basis(frame.Rotate.At(dt));
            if (frame.Translate != null)
                origin = frame.Translate.At(dt);
            if (frame.Scale != null)
                basis = basis.Scaled(frame.Scale.At(dt));
            Target.Transform = new Transform3D(basis, origin);
        }
    }

    /// <summary>
    /// An OBJECT_MOTION_FROM_TO tween: linear over the authored run time, in the node's own
    /// parent frame.
    ///
    /// The <c>translate</c>/<c>rotate</c>/<c>scale</c> channels are ABSOLUTE poses in that
    /// frame, not offsets from the rest pose — verified across all 8 chapters: C1's
    /// <c>mafia</c> car moves from (-6796, 128, -5958), which is its authored node translate
    /// to within a metre, and C5's <c>m_gerter</c> crane hook moves between (21.3, 19.9,
    /// -20.2) and (21.3, 7.9, -20.2) in its parent crane's frame. Adding these to the rest
    /// pose doubles every world-space position (the C1 traffic ended up 7 km off the map).
    /// Nodes whose animation is what places them — C2's <c>sailboat2</c> rests at its parent's
    /// origin — simply don't match their rest pose, which is why "does it match the rest pose"
    /// is a bad test and absolute-in-parent-frame is the rule.
    ///
    /// The separate <c>*_delta</c> channels are the genuinely relative ones (29 uses across
    /// the whole install) and are applied ON TOP of the rest pose.
    ///
    /// Rotations are RADIANS. The data's extremes settle it: the maximum is 15.708 = 5π,
    /// 99.93% of values are ≤ 2π, and 228 sit on exact π/2 multiples. Running them through
    /// DegToRad made every rotation ~57× too small, i.e. visually nothing turned.
    ///
    /// A missing FROM means "from the rest pose" (absolute channels) or "from no offset"
    /// (delta channels).
    /// </summary>
    private sealed class FromToMotion : IAnimMotion
    {
        public Node3D Target { get; private init; } = null!;
        private Transform3D _rest;
        private Vector3? _tFrom, _tTo, _rFrom, _rTo, _sFrom, _sTo;
        private Vector3? _tdFrom, _tdTo, _rdFrom, _rdTo, _sdFrom, _sdTo;
        private float _t, _runTime;

        public bool Finished => _t >= _runTime;

        public static FromToMotion? Create(AnimRuntime rt, Node3D target, AnimData data, float runTime)
        {
            var m = new FromToMotion
            {
                Target = target,
                _rest = rt.RestOf(target),
                _runTime = Mathf.Max(runTime, 0f),
            };
            (m._tFrom, m._tTo) = Channel(data, "translate");
            (m._rFrom, m._rTo) = Channel(data, "rotate");
            (m._sFrom, m._sTo) = Channel(data, "scale");
            (m._tdFrom, m._tdTo) = Channel(data, "translate_delta");
            (m._rdFrom, m._rdTo) = Channel(data, "rotate_delta");
            (m._sdFrom, m._sdTo) = Channel(data, "scale_delta");
            return m.HasAnyChannel ? m : null;
        }

        private bool HasAnyChannel =>
            _tTo != null || _rTo != null || _sTo != null ||
            _tdTo != null || _rdTo != null || _sdTo != null;

        private static (Vector3?, Vector3?) Channel(AnimData data, string name)
        {
            var ch = data.Obj(name);
            if (ch == null)
                return (null, null);
            return (ch.Has("from") ? ch.Vec3("from") : null,
                    ch.Has("to") ? ch.Vec3("to") : null);
        }

        public void Tick(float dt) => Seek(_t + dt);

        public void Seek(float t)
        {
            _t = t;
            float u = _runTime <= 0f ? 1f : Mathf.Clamp(t / _runTime, 0f, 1f);

            var origin = _rest.Origin;
            if (_tTo is { } tTo)
                origin = (_tFrom ?? _rest.Origin).Lerp(tTo, u);
            if (_tdTo is { } tdTo)
                origin += (_tdFrom ?? Vector3.Zero).Lerp(tdTo, u);

            // Rebuild the basis from the absolute euler when a rotate channel is present,
            // otherwise keep the rest orientation; deltas then compose on top of that.
            var basis = _rest.Basis;
            if (_rTo is { } rTo)
                basis = Euler((_rFrom ?? _rest.Basis.GetEuler(EulerOrder.Yxz)).Lerp(rTo, u));
            if (_rdTo is { } rdTo)
                basis *= Euler((_rdFrom ?? Vector3.Zero).Lerp(rdTo, u));

            if (_sTo is { } sTo)
                basis = basis.Orthonormalized().Scaled((_sFrom ?? Vector3.One).Lerp(sTo, u));
            if (_sdTo is { } sdTo)
                basis = basis.Scaled((_sdFrom ?? Vector3.One).Lerp(sdTo, u));

            Target.Transform = new Transform3D(basis, origin);
        }

        private static Basis Euler(Vector3 radians) => Basis.FromEuler(radians, EulerOrder.Yxz);
    }

    // ---- instances and sequence timing ----

    /// <summary>One running definition: its anchor plus a runner per active sequence. The
    /// sequences of a definition run CONCURRENTLY — the C1 train drives its four cars from
    /// four sibling sequences, each with its own SI script and its own loop.</summary>
    private sealed class AnimInstance
    {
        public readonly AnimDefinition Def;
        public readonly Node3D? Anchor;
        public readonly List<SequenceRunner> Runners = new();

        public AnimInstance(AnimDefinition def, Node3D? anchor)
        {
            Def = def;
            Anchor = anchor;
        }

        public bool Finished => Runners.Count == 0;

        public void Advance(AnimRuntime rt, float dt)
        {
            for (int i = Runners.Count - 1; i >= 0; i--)
            {
                Runners[i].Advance(rt, this, dt);
                if (Runners[i].Done)
                    Runners.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Executes one sequence's event list on a clock.
    ///
    /// Scheduling (inferred from the data, recorded in docs/formats/anim-definitions.md):
    /// an event's <c>start</c> gives an origin and a delay — "Animation" from the animation
    /// start, "Sequence" from this sequence's start, "Event" from the previous event's
    /// COMPLETION. An absent <c>start</c> is "Event + 0", i.e. as soon as the previous
    /// event finishes. That reading is what makes the C1 train work: its sequences are
    /// [ObjectMotionSiScript, Loop{-1}] with no start offsets, and only "after the previous
    /// event completes" turns that into the surveyed ~327 s track loop rather than a
    /// zero-length infinite loop.
    /// </summary>
    private sealed class SequenceRunner
    {
        private readonly AnimSequence _seq;
        private int _pc;              // next event index
        private float _clock;         // seconds since this sequence started
        private float _due;           // when the next event fires
        private bool _done;
        private int _loopsLeft = -2;  // -2 = no loop seen yet
        // One entry per open IF: has any branch of that chain already run? An ELSEIF/ELSE
        // reached with the flag set is the *fall-through* off the end of a taken branch and
        // must skip to the ENDIF; reached with it clear, it is the next candidate to test.
        private readonly List<bool> _branchTaken = new();

        public SequenceRunner(AnimSequence seq) => _seq = seq;

        public bool Done => _done;

        public void Advance(AnimRuntime rt, AnimInstance inst, float dt)
        {
            _clock += dt;
            // Guard against a zero-length sequence looping forever within one frame.
            int fired = 0;
            while (!_done && _pc < _seq.Events.Count && _clock >= _due && fired++ < 256)
            {
                var ev = _seq.Events[_pc];
                if (rt.Dispatch(ev, inst.Def, inst.Anchor, instant: false, out float duration))
                {
                    _pc++;
                    _due = NextDue(ev, duration);
                    continue;
                }
                // Control flow.
                switch (ev.Kind)
                {
                    case "Loop":
                        if (_loopsLeft == -2)
                            _loopsLeft = (int)(ev.Data.Num("value") ?? CountOf(ev) ?? -1f);
                        if (_loopsLeft == 0)
                        {
                            _done = true;
                            break;
                        }
                        if (_loopsLeft > 0)
                            _loopsLeft--;
                        bool instantIteration = _clock <= 0f;
                        _pc = 0;
                        _clock = 0f;
                        _due = 0f;
                        _branchTaken.Clear(); // a new iteration re-tests every condition
                        // A loop over purely instantaneous events is the data's "keep this
                        // animation alive" idiom (C1's waterfall is [PufferState ×3, Loop{-1}],
                        // whose emitters run on their own TIME_INTERVAL). Left unchecked it
                        // spins as fast as the per-frame guard allows; yield to the next frame
                        // instead. Loops whose body takes time are unaffected — they are
                        // already waiting on _due.
                        if (instantIteration)
                            return;
                        break;
                    case "If":
                        _branchTaken.Add(false);
                        goto case "Elseif";
                    case "Elseif":
                        if (Taken)
                        {
                            // Falling out of a branch that ran: everything left in the chain
                            // is dead, so jump to the ENDIF (which pops the frame).
                            _pc = SkipToEnd(_pc);
                            break;
                        }
                        if (rt.EvaluateCondition(ev.Data.Obj("condition"), inst.Def, inst.Anchor))
                        {
                            Taken = true;
                            _pc++;      // run the branch body
                        }
                        else
                        {
                            _pc = NextBranch(_pc); // the next ELSEIF/ELSE/ENDIF
                        }
                        break;
                    case "Else":
                        if (Taken)
                            _pc = SkipToEnd(_pc);
                        else
                        {
                            Taken = true;
                            _pc++;
                        }
                        break;
                    case "Endif":
                        if (_branchTaken.Count > 0)
                            _branchTaken.RemoveAt(_branchTaken.Count - 1);
                        _pc++;
                        break;
                    default:
                        _pc++;
                        break;
                }
            }
            if (_pc >= _seq.Events.Count)
                _done = true;
        }

        private static float? CountOf(AnimEvent ev) => ev.Data.Num("Count");

        private float NextDue(AnimEvent ev, float duration) => ev.StartOffset switch
        {
            // "Animation"/"Sequence" are absolute against their origin; this runner's clock
            // is the sequence clock, and an instance starts all its sequences together, so
            // the two coincide for every case in the shipped data.
            "Animation" or "Sequence" => ev.StartTime,
            _ => _clock + duration + ev.StartTime,
        };

        /// <summary>Has some branch of the innermost open IF chain already run? A malformed
        /// chain (an ELSE with no IF) reads as "not taken" and writes are dropped, so bad
        /// data degrades to running the branch instead of faulting.</summary>
        private bool Taken
        {
            get => _branchTaken.Count > 0 && _branchTaken[^1];
            set { if (_branchTaken.Count > 0) _branchTaken[^1] = value; }
        }

        // The next ELSEIF/ELSE/ENDIF of this chain (nesting-aware) — where a FAILED condition
        // continues. Lands ON the event, so the loop re-dispatches it as the next candidate.
        private int NextBranch(int from) => Scan(from, stopAtElse: true);

        // The chain's own ENDIF — where a branch that ran, or one skipped past its whole
        // chain, continues. Lands ON the ENDIF so it pops the frame.
        private int SkipToEnd(int from) => Scan(from, stopAtElse: false);

        private int Scan(int from, bool stopAtElse)
        {
            int depth = 0;
            for (int i = from + 1; i < _seq.Events.Count; i++)
            {
                switch (_seq.Events[i].Kind)
                {
                    case "If": depth++; break;
                    case "Endif":
                        if (depth == 0) return i;
                        depth--;
                        break;
                    case "Else":
                    case "Elseif":
                        if (depth == 0 && stopAtElse) return i;
                        break;
                }
            }
            return _seq.Events.Count;
        }
    }

    // ---- node resolution (unchanged from the start-state applier) ----

    /// <summary>World nodes a definition anchors to: NAME matches (wildcards = one per
    /// building/vehicle instance); else ANIMATION_ROOT_NAME matches lifted to their parent
    /// (the instance root, so sibling healthy/destroyed both resolve locally — needed where
    /// instance roots have free names: `m_build**` instances are `apbuild01.flt`…).</summary>
    private List<Node3D?> Anchors(AnimDefinition def)
    {
        // Multi-target NAME1 definitions (zeppelin nacelles/turrets) parse with an empty
        // NAME; they animate per-object sub-parts and are object-wiring scope — never anchor
        // them (their generic ROOT names would anchor them onto every building in the world).
        if (string.IsNullOrEmpty(def.Name))
            return new List<Node3D?>();
        var anchors = FindAll(def.Name, null).Cast<Node3D?>().ToList();
        if (anchors.Count == 0 && def.RootName != null)
        {
            var roots = FindAll(def.RootName, null);
            if (roots.Count > 0 && roots.Count <= MaxRootLift)
                anchors = roots
                    .Select(n => n.GetParent() as Node3D)
                    .Where(p => p != null)
                    .Distinct()
                    .ToList();
        }
        return anchors;
    }

    /// <summary>The world nodes one event targets. Reader-sourced events may carry a
    /// parent→child path; compiled events name a single node (under "node" or "name",
    /// which upstream spells inconsistently per event type).</summary>
    private List<Node3D> Targets(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        // Compiled definitions carry a symbol table binding each referenced name to an exact
        // gamez node index — always prefer it. Name matching resolves C1's `caboose` to the
        // real consist AND to an unrelated `caboose.flt` in the rail yard, and drives both.
        if ((ev.Data.Str("node") ?? ev.Data.Str("name")) is { } refName
            && def.NodeRefs.TryGetValue(refName, out int nodeIndex))
        {
            if (_byIndex.TryGetValue(nodeIndex, out var bound))
                return new List<Node3D> { bound };
            // The index is valid data but that node was not built (LOD levels the builder
            // drops, skipped subtrees). Not an error, and not something to name-match around.
            _opsUnresolved++;
            return new List<Node3D>();
        }

        var path = new List<string>();
        if (ev.Data.List("node_path") is { } nodePath)
        {
            foreach (var p in nodePath)
                if (p is string s)
                    path.Add(s);
        }
        else if ((ev.Data.Str("node") ?? ev.Data.Str("name")) is { } single)
        {
            path.Add(single);
        }
        if (path.Count == 0)
            return new List<Node3D>();
        var targets = ResolvePath(path, anchor, def.LocalNodesOnly);
        if (targets.Count == 0)
            _opsUnresolved++;
        return targets;
    }

    // NAME paths: resolve the first element in scope (falling back to global for
    // non-local defs), then each further element inside the previous matches.
    private List<Node3D> ResolvePath(List<string> path, Node3D? scope, bool localOnly)
    {
        var candidates = FindAll(path[0], scope);
        if (candidates.Count == 0 && scope != null && !localOnly)
            candidates = FindAll(path[0], null);
        for (int i = 1; i < path.Count && candidates.Count > 0; i++)
        {
            var next = new List<Node3D>();
            foreach (var c in candidates)
                next.AddRange(FindAll(path[i], c).Where(n => n != c));
            candidates = next;
        }
        return candidates;
    }

    private List<Node3D> FindAll(string pattern, Node3D? scope)
    {
        var match = Matcher(pattern);
        var result = new List<Node3D>();
        foreach (var (node, srcName) in _index)
        {
            // Defs may name a node without its model-file suffix ('ap_radiotwr' for the
            // gamez node 'ap_radiotwr.flt') — try both.
            var matches = match(srcName)
                || (srcName.EndsWith(".flt", StringComparison.OrdinalIgnoreCase) && match(srcName[..^4]));
            if (matches && (scope == null || node == scope || scope.IsAncestorOf(node)))
                result.Add(node);
        }
        return result;
    }

    // Wildcard NAME → predicate: '*' (and the '**' template form) match any run of
    // characters, '#' a run of digits ('air_gen#' covers 'air_gen'). Plain names compare
    // exactly (case-insensitive, like every reader name lookup).
    private Func<string, bool> Matcher(string pattern)
    {
        if (_matcherCache.TryGetValue(pattern, out var cached))
            return cached;
        Func<string, bool> match;
        if (pattern.Contains('*') || pattern.Contains('#'))
        {
            var re = new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\#", "[0-9]*") + "$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            match = re.IsMatch;
        }
        else
        {
            match = s => s.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }
        return _matcherCache[pattern] = match;
    }

    // ---- state application ----

    /// <summary>The node's authored pose, remembered the first time anything moves it, so
    /// every pose op stays an offset from the rest pose rather than compounding.</summary>
    private Transform3D RestOf(Node3D node)
    {
        if (!_rest.TryGetValue(node, out var rest))
            _rest[node] = rest = node.Transform;
        return rest;
    }

    // The *_STATE poses use the same absolute-in-parent-frame convention as
    // OBJECT_MOTION_FROM_TO — see FromToMotion's remarks for the evidence. OBJECT_TRANSLATE_STATE
    // carries an explicit RELATIVE flag (false in all 1143 uses in this install) and
    // OBJECT_ROTATE_STATE a BASIS of "Absolute" (6430 of ~6600), which is the data saying so
    // outright.

    private void PoseTranslate(Node3D target, Vector3 position, bool relative)
    {
        RestOf(target); // record the authored pose before we disturb it
        target.Position = relative ? target.Position + position : position;
        _opsApplied++;
    }

    private void PoseRotate(Node3D target, Vector3 radians)
    {
        var rest = RestOf(target);
        target.Basis = Basis.FromEuler(radians, EulerOrder.Yxz)
                            .Scaled(rest.Basis.Scale);
        _opsApplied++;
    }

    private void PoseScale(Node3D target, Vector3 scale)
    {
        if (scale.LengthSquared() < 1e-9f)
            return;
        var rest = RestOf(target);
        target.Basis = rest.Basis.Orthonormalized().Scaled(scale);
        _opsApplied++;
    }

    // INACTIVE = invisible and non-collidable, the whole subtree; ACTIVE re-enables both.
    private static void SetSubtreeActive(Node3D node, bool active)
    {
        node.Visible = active;
        SetCollidersEnabled(node, active);
    }

    private static void SetCollidersEnabled(Node node, bool enabled)
    {
        if (node is CollisionShape3D shape)
            shape.Disabled = !enabled;
        foreach (var child in node.GetChildren())
            SetCollidersEnabled(child, enabled);
    }

    // Any still-visible node named like a destroyed variant that no definition touched:
    // hide it and report — each name is a data-coverage gap (a def we failed to anchor).
    // Match 'destroyed' only: a '_dest' suffix rule proved WRONG — C1's `ref_tank_dest` is
    // the parent GROUP of the five healthy harbor refuel tanks ("destructible", not
    // "destroyed"), and hiding it wiped the visible tanks (user-reported).
    private List<string> HideUncoveredDestroyed()
    {
        var hidden = new List<string>();
        foreach (var (node, srcName) in _index)
        {
            if (!srcName.Contains("destroyed", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!node.Visible || HasHiddenAncestor(node))
                continue;
            SetSubtreeActive(node, false);
            hidden.Add(srcName);
        }
        return hidden;
    }

    private bool HasHiddenAncestor(Node3D node)
    {
        for (var p = node.GetParent() as Node3D; p != null && p != _root; p = p.GetParent() as Node3D)
            if (!p.Visible)
                return true;
        return false;
    }
}
