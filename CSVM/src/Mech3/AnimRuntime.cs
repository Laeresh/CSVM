using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The animation engine: binds an <see cref="AnimProgram"/> to a built world and executes
/// it. Replaces the start-state-only applier this project had before, whose
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
    private readonly DestructibleRegistry _destructibles = new();
    private Node3D _root = null!;
    private AnimProgram _program = null!;

    private int _opsApplied, _opsUnresolved;

    /// <summary>Live animation instances currently running (diagnostics).</summary>
    public int ActiveInstances => _instances.Count;

    /// <summary>The live per-instance HP of every destructible node group in this world (C21).
    /// Built during the bootstrap; the source of the value <c>ANIM_HEALTH</c> conditions read.
    /// C23's weapon damage and C24's death sequence act through it.</summary>
    public DestructibleRegistry Destructibles => _destructibles;

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

    /// <summary>Whether <see cref="Bootstrap"/> runs the ambient-playback passes — pass 2
    /// (<c>ON_STARTUP</c> definitions) and pass 3 (the mission's startanims). True in every
    /// game/viewer/flight session: the world plays itself. The animation debugger sets it false
    /// for a <b>quiet stage</b> — passes 0 (mission setup), 1 (reset states) and 4 (the safety
    /// net) still run, so every base state and mission-entity setup is applied, but nothing starts
    /// animating until <see cref="StartAmbient"/> is called (the lab's ambient toggle). Set before
    /// <see cref="Bind"/>.</summary>
    public bool AutoStart = true;

    // Whether the ambient passes have already run — set when Bootstrap runs them inline
    // (AutoStart=true) or when StartAmbient runs them on demand, so StartAmbient is idempotent
    // and a normal bootstrap's ambient toggle is a no-op rather than a second bootstrap.
    private bool _ambientStarted;

    // RANDOM_WEIGHT dice. A field rather than GD.Randf() so a seed can be pinned — the animation
    // debugger sets one (see Seed) for a reproducible fixed-dt run; the game leaves it unseeded
    // and so is unchanged. Any future WeaponHit/crash handler's randomness must route through this
    // same _rng, or a lab restart stops being identical the day the handler lands.
    private Random _rng = new();
    private int? _seed;

    /// <summary>Pins the runtime's RNG for a reproducible run (the debugger's deterministic
    /// clock). Null — the default — leaves it unseeded, so the game is unchanged. Set at
    /// construction through the object initializer, before <see cref="Bind"/>.</summary>
    public int? Seed
    {
        init
        {
            _seed = value;
            if (value is { } s)
                _rng = new Random(s);
        }
    }

    /// <summary>Re-pins the RNG to the constructed <see cref="Seed"/>. The animation debugger
    /// calls this on every Play/Restart so a seeded replay rolls the same dice as the launch —
    /// without it the RNG stream would just continue and a "restart" would diverge on the first
    /// RANDOM_WEIGHT. No-op when unseeded (the game, which never calls it either way).</summary>
    public void Reseed()
    {
        if (_seed is { } s)
        {
            _rng = new Random(s);
        }
    }

    /// <summary>The mission's interp boot script (<c>support\&lt;chapter&gt;\&lt;mission&gt;.gw</c>),
    /// run as bootstrap pass 0. It is what decides which world entities this mission shows —
    /// see <see cref="MissionSetup"/>. Null when the mission ships no script, which is normal.
    /// Set before <see cref="Bind"/>.</summary>
    public MissionSetup? Setup;

    private void Bootstrap(Node3D worldRoot, AnimProgram program)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _root = worldRoot;
        _program = program;
        IndexWorld(worldRoot);
        long indexMs = sw.ElapsedMilliseconds;

        // Pass 0: the engine's own per-mission world setup, before any animation state. The
        // chapter gamez holds every mission's content and this script switches off what this
        // mission does not show (C1/IA1: hk_zep, both MP zeppelins, the CTF props, …). Runs
        // first so an animation state can still override it, which is the engine's load order.
        Setup?.Apply((name, scope) => FindAll(name, scope), SetSubtreeActive);

        // Pass 1: base states. Anchored defs only — a def whose NAME matches nothing in this
        // world (player-plane anims, cutscene rigs) must not stomp globally-resolved bare
        // names like 'destroyed'.
        //
        // This is also where the destructible registry (C21) is built: a def with HEALTH > 0 is
        // a destructible, and each node its NAME resolves to is an independent instance with its
        // own mutable HP. Nothing damages them yet (C23), so this only changes where ANIM_HEALTH
        // reads its value from, not the value — a fresh world is unchanged.
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
            if (def.Destructible)
                foreach (var anchor in anchors)
                    if (anchor != null)
                        _destructibles.Register(def, anchor, def.Health);
        }
        long resetMs = sw.ElapsedMilliseconds;

        // Passes 2 and 3 START the world animating (ON_STARTUP defs, then the mission's
        // startanims). A quiet-stage bootstrap (AutoStart=false, the debugger) skips them and runs
        // them later through StartAmbient; the passes around them still run, so the stage is fully
        // set up, just still. When AutoStart is true this is byte-for-byte the inline passes 2/3,
        // in the same place, before the pass-4 safety net.
        int startupRun = 0;
        var ran = new List<string>();
        var missing = new List<string>();
        if (AutoStart)
        {
            _ambientStarted = true;
            (startupRun, ran, missing) = RunAmbientPasses();
        }

        // Pass 4: safety net for destroyed-variant subtrees no definition covered.
        var netHidden = HideUncoveredDestroyed();

        if (Setup != null)
            GD.Print(Setup.Report());
        GD.Print($"anim: {program.Defs.Count} defs ({program.CompiledCount} compiled, " +
                 $"{program.ReaderCount} reader), {program.ScriptPoolCount} SI scripts; " +
                 $"{anchored} anchored, {_opsApplied} state ops applied, {_opsUnresolved} unresolved");
        GD.Print($"anim: {startupRun} ON_STARTUP + {ran.Count} start anims running, " +
                 $"{_instances.Count} live instance(s), {_motions.Count} live motion(s) " +
                 $"[index {indexMs} ms, reset states {resetMs - indexMs} ms, " +
                 $"start {sw.ElapsedMilliseconds - resetMs} ms]");
        if (_destructibles.Count > 0)
            GD.Print($"anim: {_destructibles.Count} destructible instance(s) across " +
                     $"{_destructibles.DistinctAnchors} node group(s) registered " +
                     $"(mutable HP; inert until weapons land)");
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
        if (_lights.Count > 0)
        {
            int on = _lights.Values.Count(l => l.Active);
            GD.Print($"anim: {_lights.Count} point light(s), {on} lit at startup: " +
                     string.Join(", ", _lights.Keys.Select(k => k.Name).Distinct().Take(10)));
        }
        // Reported on their own line rather than through Count(), which is the "not yet acted on"
        // channel: filing a working feature there would report it as a missing one.
        if (_soundEmitters.Count > 0 || _soundsUnknown > 0 || _soundsAfterBuild > 0)
            GD.Print($"anim: {_soundEmitters.Count} ambient sound emitter(s): " +
                     string.Join(", ", Sounds?.Names ?? Enumerable.Empty<string>()) +
                     (_soundsUnknown > 0 ? $" [{_soundsUnknown} unknown to sounds.json]" : "") +
                     (_soundsAfterBuild > 0 ? $" [{_soundsAfterBuild} requested with no audio session]" : ""));
        // Everything above is a bootstrap snapshot; from here on a failure reports itself.
        _soundCensusPrinted = true;
        if (netHidden.Count > 0)
            GD.Print($"anim: safety net hid {netHidden.Count} uncovered destroyed subtree(s): " +
                     string.Join(", ", netHidden.Take(10)) + (netHidden.Count > 10 ? ", …" : ""));
        ReportConditions();
        ReportRetargets();
        ReportUnhandled();
    }

    /// <summary>Runs the two ambient-playback bootstrap passes — pass 2 (ACTIVATION ON_STARTUP
    /// definitions) and pass 3 (the mission's startanims, by ANIMATION_NAME, in list order) —
    /// and returns their tallies for the census. Extracted verbatim from <see cref="Bootstrap"/>
    /// so a quiet-stage bootstrap can defer them to <see cref="StartAmbient"/>.</summary>
    private (int StartupRun, List<string> Ran, List<string> Missing) RunAmbientPasses()
    {
        // Pass 2: ON_STARTUP definitions run for real (zepstate's roster is instantaneous
        // ObjectActiveStates, so this still settles on frame 0 for those).
        int startupRun = 0;
        foreach (var def in _program.Defs.Where(d => d.OnStartup))
            foreach (var anchor in Anchors(def))
            {
                Start(def, anchor);
                startupRun++;
            }

        // Pass 3: the mission's start animations, by ANIMATION_NAME, in list order — each
        // through Play, which is also the debugger's --play-anim/Restart path, so the lab
        // starts a definition exactly the way the bootstrap does.
        var ran = new List<string>();
        var missing = new List<string>();
        foreach (var animName in _program.StartAnims)
        {
            if (Play(animName).Count == 0)
            {
                missing.Add(animName);
            }
            else
            {
                ran.Add(animName);
            }
        }
        return (startupRun, ran, missing);
    }

    /// <summary>Starts every definition carrying this ANIMATION_NAME, exactly the way bootstrap
    /// pass 3 starts a startanim: an anchored def starts once per anchor, an unanchored one gets
    /// a single global-resolution instance (null anchor), and each instance's RESET_STATE is
    /// re-applied first. Returns the (def, anchor) pairs started — empty when the name matches no
    /// definition in this program. This is the animation debugger's <c>--play-anim</c> path; its
    /// Restart is <see cref="Stop"/> → <see cref="Reseed"/> → Play.</summary>
    public List<(AnimDefinition Def, Node3D? Anchor)> Play(string animName, Node3D? fallbackAnchor = null,
        bool applyReset = true)
    {
        var started = new List<(AnimDefinition Def, Node3D? Anchor)>();
        foreach (var def in _program.ByAnimName(animName))
        {
            var anchors = Anchors(def);
            if (anchors.Count == 0)
            {
                // A placeless def (its NAME resolves nothing): fall back to the caller's staging
                // anchor — the animation debugger's in-front-of-camera dummy — or null, which is
                // global resolution (the def names world nodes directly). Bootstrap passes null.
                anchors.Add(fallbackAnchor);
            }
            foreach (var anchor in anchors)
            {
                // The debugger re-poses the RESET_STATE before replaying (a startanim-style start).
                // The crash TRIGGER must not: player_crash_dirt's reset calls player_destruction_reset
                // (restores the healthy panels) and hides the wreck — the exact opposite of a crash —
                // and it already ran at bind and re-runs on respawn (ResetToBaseState). So the crash
                // passes applyReset:false and only the destruction sequences fire.
                if (applyReset && def.ResetState != null)
                {
                    ApplyInstant(def.ResetState.Events, def, anchor);
                }
                Start(def, anchor);
                started.Add((def, anchor));
            }
        }
        return started;
    }

    /// <summary>The world node the animation debugger frames its camera on for one started
    /// instance: the anchor itself when the instance has one, else the first of the def's own
    /// node names that resolves in this world (the unanchored global-resolution case). Null when
    /// nothing resolves — the caller keeps its current framing.</summary>
    public Node3D? FrameTarget(AnimDefinition def, Node3D? anchor)
    {
        if (anchor != null)
        {
            return anchor;
        }
        foreach (var name in def.NodeList)
        {
            if (!string.IsNullOrEmpty(name) && ResolveOne(name, def, null) is { } node)
            {
                return node;
            }
        }
        return null;
    }

    /// <summary>Runs the ambient-playback passes a quiet-stage bootstrap (<see cref="AutoStart"/>
    /// = false) skipped: the ON_STARTUP definitions and the mission's startanims begin playing.
    /// Idempotent — a second call is a no-op, and it is already a no-op after a normal
    /// (AutoStart=true) bootstrap — so the debugger can bind it to a toggle without stacking
    /// instances.</summary>
    public void StartAmbient()
    {
        if (_ambientStarted)
            return;
        _ambientStarted = true;
        var (startupRun, ran, missing) = RunAmbientPasses();
        GD.Print($"anim: ambient start — {startupRun} ON_STARTUP + {ran.Count} start anims running, "
                 + $"{_instances.Count} live instance(s), {_motions.Count} live motion(s)");
        if (ran.Count > 0 || missing.Count > 0)
            GD.Print($"anim: start anims [{string.Join(", ", ran)}]" +
                     (missing.Count > 0 ? $", undefined here: [{string.Join(", ", missing)}]" : ""));
    }

    /// <summary>Reverses <see cref="StartAmbient"/> — the animation debugger's ambient <b>off</b>
    /// half. Tears down every live instance and its resources <b>except</b> the one carrying
    /// <paramref name="keepAnim"/> (the def the lab is currently playing, which survives at its
    /// playhead), then re-applies the quiet stage's base states for every torn-down def, so the
    /// world returns to the quiet stage a <c>AutoStart=false</c> bootstrap leaves. A no-op unless
    /// ambient is running, so the toggle is symmetric with <see cref="StartAmbient"/>.</summary>
    public void StopAmbient(string? keepAnim = null)
    {
        if (!_ambientStarted)
            return;
        for (int i = _instances.Count - 1; i >= 0; i--)
        {
            var inst = _instances[i];
            if (keepAnim != null && string.Equals(inst.Def.AnimName, keepAnim, StringComparison.OrdinalIgnoreCase))
                continue;
            _instances.RemoveAt(i);
            TearDownResourcesOf(inst.Def, inst.Anchor);
            OnInstanceFinished?.Invoke(inst.Def, inst.Anchor);
        }
        // Re-quiet the torn-down defs' nodes: re-apply their RESET_STATE base poses (the same
        // pass 1 the bootstrap runs), skipping the kept def whose live motions still drive it.
        foreach (var def in _program.Defs)
        {
            if (keepAnim != null && string.Equals(def.AnimName, keepAnim, StringComparison.OrdinalIgnoreCase))
                continue;
            if (def.ResetState == null)
                continue;
            var anchors = Anchors(def);
            foreach (var anchor in anchors)
                ApplyInstant(def.ResetState.Events, def, anchor);
        }
        HideUncoveredDestroyed();
        _ambientStarted = false;
        GD.Print($"anim: ambient stopped — {_instances.Count} live instance(s) kept, "
                 + $"{_motions.Count} live motion(s)");
    }

    /// <summary>Hard-stops EVERYTHING this runtime created and re-applies every anchored
    /// definition's RESET_STATE — the per-player crash runtime's respawn. After a crash the wreck is
    /// shown, the pieces flung and the fire burning; this puts the def back to its quiet base
    /// (destroyed hidden, effect templates hidden) so the next crash starts clean, and the caller
    /// re-homes any node the def MOVED but has no reset event for (the flung wreck pieces).
    ///
    /// <para>Clears the whole RESOURCE POOL, not just live instances: an effect def whose sequence
    /// has already ended (e.g. <c>large_10sec_fire</c>, whose 10 s particles outlive its instance)
    /// is gone from <c>_instances</c> yet its puffer is still emitting, so a per-instance teardown
    /// would leave the fire burning after respawn. And puffers are <c>Clear</c>ed (particles gone at
    /// once), not <c>SustainEnd</c>ed (which lets them finish their lifetimes) — respawn is
    /// immediate. Safe to wipe the whole pool because this runtime is scoped to one plane's crash;
    /// the shared world runtime never calls this.</para></summary>
    public void ResetToBaseState()
    {
        _instances.Clear();
        _motions.Clear();
        foreach (var entry in _puffers.Values)
        {
            entry.Puffer.SustainEnd();
            entry.Puffer.Clear();     // drop live particles NOW; respawn must not leave fire burning
            entry.Puffer.QueueFree(); // the next crash builds fresh emitters — do not accumulate
        }
        _puffers.Clear();
        _activePuffers.Clear();
        _lights.Clear();
        if (Sounds != null)
        {
            foreach (var handle in _soundEmitters.Values)
                Sounds.SetActive(handle, false);
        }
        _soundEmitters.Clear();

        foreach (var def in _program.Defs)
        {
            if (def.ResetState == null)
                continue;
            foreach (var anchor in Anchors(def))
                if (anchor != null)
                    ApplyInstant(def.ResetState.Events, def, anchor);
        }
    }

    private readonly Dictionary<int, Node3D> _byIndex = new();

    private void IndexWorld(Node3D worldRoot)
    {
        void Walk(Node3D n)
        {
            var srcName = n.HasMeta(NameMeta) ? n.GetMeta(NameMeta).AsString() : n.Name.ToString();
            _index.Add((n, srcName));
            // The scoped crash runtime resolves purely by name (see NameResolveFallback): it mixes
            // two gamez index spaces that collide, so a shared _byIndex would misresolve. Leaving it
            // empty makes every index lookup miss and fall through to the unique name.
            if (!NameResolveFallback && n.HasMeta(IndexMeta))
                _byIndex.TryAdd((int)n.GetMeta(IndexMeta), n);
            foreach (var child in n.GetChildren())
                if (child is Node3D c)
                    Walk(c);
        }
        Walk(worldRoot);
    }

    /// <summary>Indexes a subtree added to the world AFTER the bootstrap — the animation debugger's
    /// effect-template stage (the fireball/spark/trail/dirt roots <see cref="WorldBuilder"/>
    /// deliberately skips) — so its nodes resolve by name and index, then applies the RESET_STATE of
    /// every definition now anchored within it, exactly the quiet-stage posing bootstrap pass 1 would
    /// have done had the subtree existed then (so the templates start hidden until a call stages
    /// them). The find cache is cleared because the bootstrap may have cached these names as
    /// resolving to nothing. Additive: nothing already running is disturbed, and it never re-runs
    /// mission setup the way a second <see cref="Bind"/> would.</summary>
    public void IndexStage(Node3D subtree)
    {
        IndexWorld(subtree);   // appends the subtree's nodes to _index / _byIndex
        _findCache.Clear();    // drop stale "resolves to nothing" results cached during bootstrap
        foreach (var def in _program.Defs)
        {
            if (def.ResetState == null)
                continue;
            foreach (var a in Anchors(def))
                if (a != null && (a == subtree || subtree.IsAncestorOf(a)))
                    ApplyInstant(def.ResetState.Events, def, a);
        }
    }

    // ---- observability (the animation debugger's timeline; null = zero cost in the game) ----

    /// <summary>One runtime event dispatch, reported to <see cref="OnEventDispatched"/>. Carries
    /// what the debugger's timeline needs to stamp a "fired" mark on the right authored block: the
    /// running definition and its anchor (the instance identity), the sequence lane, the event's
    /// index within that sequence, and its kind and target name. Raised only for real timed
    /// dispatches from a sequence runner, never for the instant RESET_STATE posing — so a mark
    /// always corresponds to something the clock actually reached.</summary>
    public readonly record struct EventDispatch(
        AnimDefinition Def, Node3D? Anchor, string Sequence, int EventIndex,
        string EventKind, string? EventName);

    /// <summary>Raised as each sequence event fires at runtime. Null by default → zero cost in the
    /// game; the debugger sets it to feed its timeline's fired marks straight from the runtime,
    /// rather than parsing --debug-anim log text.</summary>
    public Action<EventDispatch>? OnEventDispatched;

    /// <summary>Raised when a definition becomes a live instance and when that instance finishes,
    /// each carrying the (def, anchor) identity. Null by default → zero cost in the game; the
    /// debugger uses the pair to place a CALL_ANIMATION child def's timeline lane group at the
    /// playhead time it began, and to drop it when it ends.</summary>
    public Action<AnimDefinition, Node3D?>? OnInstanceStarted;
    public Action<AnimDefinition, Node3D?>? OnInstanceFinished;

    // Best-effort target/name an event carries, for the timeline mark's label — the different
    // event kinds spell it under different keys. Nothing load-bearing hangs off it; the event's
    // index within its sequence is what identifies the authored block.
    private static string? EventDisplayName(AnimEvent ev) =>
        ev.Data.Str("name") ?? ev.Data.Str("node") ?? ev.Data.Str("child");

    // ---- live execution ----

    /// <summary>True when something else owns the clock (the animation debugger, which feeds
    /// <see cref="Advance"/> in fixed 1/60 s steps): <see cref="_Process"/> stops advancing.
    /// A flag rather than <c>SetProcess(false)</c> because Godot re-enables processing at READY
    /// for any node whose script overrides <c>_Process</c> — and this node enters the tree
    /// (with the world root) after the lab mode is assembled, so a SetProcess call made before
    /// that is silently undone. Found by measurement, not by reading: the lab's world ran at 2×
    /// (fixed steps + wall dt), visible as 20 logged sim-seconds in a 610-frame scripted run.</summary>
    public bool ManualAdvance;

    /// <summary>Whether a CALL_ANIMATION relocates its callee's effect-template root onto the call
    /// site (see <see cref="PlaceTemplateAt"/>). Off by default — the ambient world boot must stay
    /// byte-identical, and today's retarget only re-scopes name resolution. The animation debugger
    /// (and, later, the crash runtime) turn it on so a placeless effect template plays where it is
    /// staged instead of at its gamez origin.</summary>
    public bool PlaceCalledTemplates;

    /// <summary>Makes this runtime resolve every node reference by NAME, ignoring the compiled
    /// gamez-index table (<see cref="_byIndex"/> is not populated — see <see cref="IndexWorld"/>).
    /// Off by default: the shared world MUST use the index, because name matching resolves C1's
    /// <c>caboose</c> to the real consist AND an unrelated <c>caboose.flt</c>. The per-player crash
    /// runtime turns it on for two reasons that both make the index wrong there: (1) the player
    /// crash def's node ptrs are non-portable — they index planes.zbd at slots this build never uses
    /// — so the compiled index resolves nothing; and (2) its scoped subtree MIXES two gamez index
    /// spaces (the plane model's plane-gamez indices and the effect templates' world-gamez indices),
    /// which COLLIDE (fly_trail1 is world-index 400, and the plane has a node at plane-index 400),
    /// so a shared <c>_byIndex</c> would misresolve. Its subtree has one node per name, so name
    /// resolution is both unambiguous and the only correct choice.</summary>
    public bool NameResolveFallback;

    /// <summary>A world-space velocity added to every ballistic <see cref="MotionRuntime"/> launch
    /// (translation / translation_range), transformed into the launched node's parent frame. Zero by
    /// default. The crash sets it to a fraction of the plane's impact velocity so the wreck pieces
    /// carry the plane's momentum and scatter along its travel — the authored launch alone is a small
    /// relative pop (5–10 m/s straight up), which reads as "the pieces barely drift" against a plane
    /// that hit at 60–90 m/s. It is the physical part the def leaves to the engine (the original does
    /// the same); a TUNE on the fraction, not a decode.</summary>
    public Vector3 InheritedWorldVelocity;

    public override void _Process(double delta)
    {
        if (ManualAdvance)
        {
            return;
        }
        Advance((float)delta);
    }

    /// <summary>Advances the whole runtime by <paramref name="dt"/> seconds: motions, puffers,
    /// lights and sounds, then every live instance's sequences. <see cref="_Process"/> calls this
    /// once a frame with the real frame delta; the animation debugger disables <c>_Process</c>
    /// (<see cref="Node.SetProcess"/>(false)) and drives this itself off a fixed-dt clock — pause =
    /// don't call, step = one fixed call, slow-mo = a scaled accumulator. Same classes and same
    /// code path either way, so the game's behaviour is untouched.</summary>
    public void Advance(float dt)
    {
        // Motions advance ONCE per frame, here — not from the sequence runners, which would
        // apply dt once per running sequence and run the train at 4× speed.
        TickMotions(dt);
        TickPuffers(dt);
        TickLights(dt);
        Sounds?.Tick();
        if (DebugMotions)
            LogMotions(dt);
        // Instances can finish (and CallAnimation can add) during the walk, so iterate a copy.
        for (int i = _instances.Count - 1; i >= 0; i--)
        {
            var inst = _instances[i];
            inst.Advance(this, dt);
            if (inst.Finished)
            {
                _instances.RemoveAt(i);
                OnInstanceFinished?.Invoke(inst.Def, inst.Anchor);
            }
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
        // Restart: drop any existing instance of this def on this anchor, but LEAVE its live
        // resources so the new instance re-establishes them idempotently (motions replaced by
        // target in AddMotion, puffers/lights/sounds re-asserted as no-ops). Tearing them down
        // here would break that seamless restart and rebuild every resource — measured on C5's
        // bootstrap, which restarts m_crane_go and please_go_spark. A caller that wants the
        // resources cleared (STOP_ANIMATION, the debugger/crash respawn) calls Stop directly.
        RemoveInstances(def.AnimName, anchor, tearDown: false);
        var inst = new AnimInstance(def, anchor);
        foreach (var seq in def.Sequences.Where(s => !s.OnCallOnly))
            inst.Runners.Add(new SequenceRunner(seq));
        if (inst.Runners.Count == 0)
            return;
        _instances.Add(inst);
        OnInstanceStarted?.Invoke(def, anchor);
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
        // Its t=0 events can finish the instance — or a t=0 STOP_ANIMATION can already have
        // removed it — so only notify a finish that actually removed something, keeping the
        // start/finish notifications balanced against the live count for the timeline.
        if (inst.Finished && _instances.Remove(inst))
            OnInstanceFinished?.Invoke(def, anchor);
    }

    private const int MaxStartDepth = 8;
    private int _startDepth;

    /// <summary>Is this definition already running on this anchor? (Instance identity is
    /// (definition, anchor) throughout.)</summary>
    private bool IsLive(AnimDefinition def, Node3D? anchor) =>
        _instances.Any(i => i.Def == def && i.Anchor == anchor);

    /// <summary>Stops every live instance of an animation name (optionally only on one anchor) and
    /// tears down the live resources it created. Used by STOP_ANIMATION / INVALIDATE_ANIMATION, and
    /// — through the explicit Restart it enables (Stop → re-apply RESET_STATE → Start) — by the
    /// animation debugger and the crash plan's respawn. Removing the instance alone left the
    /// definition's motions driving nodes, its puffers emitting, its lights lit and its sounds
    /// playing; <see cref="TearDownResourcesOf"/> clears all four. Start's own restart deliberately
    /// does NOT come through here — it keeps the resources so re-assertion is a seamless no-op
    /// (see <see cref="Start"/>).</summary>
    public void Stop(string? animName, Node3D? anchor = null) =>
        RemoveInstances(animName, anchor, tearDown: true);

    /// <summary>Removes matching live instances. <paramref name="tearDown"/> chooses whether to
    /// also clear each instance's motions/puffers/lights/sounds: true for an explicit Stop, false
    /// for Start's seamless restart, which leaves them for the new instance to re-assert.</summary>
    private void RemoveInstances(string? animName, Node3D? anchor, bool tearDown)
    {
        if (string.IsNullOrEmpty(animName))
            return;
        for (int i = _instances.Count - 1; i >= 0; i--)
        {
            var inst = _instances[i];
            if (!string.Equals(inst.Def.AnimName, animName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (anchor != null && inst.Anchor != anchor)
                continue;
            _instances.RemoveAt(i);
            if (tearDown)
                TearDownResourcesOf(inst.Def, inst.Anchor);
            OnInstanceFinished?.Invoke(inst.Def, inst.Anchor);
        }
    }

    /// <summary>Tears down every live resource a stopped instance created — its motions, puffers,
    /// lights and sounds — so nothing of the definition keeps running after <see cref="Stop"/>.
    /// Motions and puffers are attributed to the exact <c>(def, anchor)</c> that registered them
    /// (see <see cref="AddMotion"/> and <see cref="_puffers"/>). Lights and sounds are keyed by
    /// <c>(name, anchor)</c>, so they are cleared by anchor — the anchor is the instance identity,
    /// and a name colliding on one anchor across two defs already shares a single entry (last
    /// writer wins), so there is nothing finer to attribute to.</summary>
    private void TearDownResourcesOf(AnimDefinition def, Node3D? anchor)
    {
        _motions.RemoveAll(m => m.Owner.Def == def && m.Owner.Anchor == anchor);

        var pufferKeys = _puffers
            .Where(kv => kv.Value.Def == def && kv.Value.Anchor == anchor)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var key in pufferKeys)
        {
            var puffer = _puffers[key].Puffer;
            puffer.SustainEnd();
            _activePuffers.RemoveAll(a => a.Puffer == puffer);
            _puffers.Remove(key);
        }

        foreach (var key in _lights.Keys.Where(k => k.Anchor == anchor).ToList())
            _lights.Remove(key);

        if (Sounds != null)
            foreach (var key in _soundEmitters.Keys.Where(k => k.Anchor == anchor).ToList())
            {
                Sounds.SetActive(_soundEmitters[key], false);
                _soundEmitters.Remove(key);
            }
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
                // A SOUND_NODE emitter is switched on by an ordinary OBJECT_ACTIVE_STATE naming
                // it — the reader spells the whole thing as a three-event triple (declare the
                // emitter, activate it, attach it to a world node). The name is a sounds.json
                // definition, NOT a gamez node, so letting it fall through to Targets() would
                // scan the world for it, find nothing and book it as an unresolved op.
                if (SoundEmitter(ev, anchor) is { } emitterHandle)
                {
                    Sounds!.SetActive(emitterHandle, ev.Data.Bool("state"));
                    _opsApplied++;
                    return true;
                }
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
                        AddMotion(tween, def, anchor);
                    _opsApplied++;
                }
                duration = instant ? 0f : runTime;
                return true;
            }

            case "ObjectOpacityState":
            {
                // OBJECT_OPACITY_STATE is translucency, not visibility. `state` is whether
                // translucency is ENABLED and `opacity` the alpha while it is — settled by the
                // data, where state=false pairs with opacity=1.0 in all 136 compiled uses and
                // never with 0, so `false` means "render normally", not "disappear". (Hiding is
                // OBJECT_ACTIVE_STATE's job and the data uses it right alongside this.)
                if (ev.Data.Get("state") is not bool on)
                {
                    Count("ObjectOpacityState(no state)");
                    return true;
                }
                float alpha = on ? ev.Data.Num("opacity") ?? 1f : 1f;
                foreach (var t in Targets(ev, def, anchor))
                {
                    SetSubtreeOpacity(t, alpha);
                    _opsApplied++;
                }
                return true;
            }

            case "ObjectOpacityFromTo":
            {
                // A timed translucency fade — the single biggest un-handled event kind (9,917
                // events install-wide, all trigger-gated OnCall/WeaponHit, so none fire at
                // bootstrap). Unlike OBJECT_OPACITY_STATE, the endpoint `state` flag does NOT
                // invert the value: (state=false, opacity=0) fades to invisible and
                // (state=false, opacity=1) fades to opaque — surveyed across all 9,917 events
                // (the two dominant combos), so this is a literal lerp of the two opacity
                // numbers through SetSubtreeOpacity. `opacity_delta` is null in 100% of them
                // (the relative form, like FromToMotion's dead *_delta channels); report it if
                // one ever appears rather than silently ignoring it.
                float runTime = ev.Data.Num("run_time") ?? 0f;
                var from = ev.Data.Obj("opacity_from");
                var to = ev.Data.Obj("opacity_to");
                if (from == null || to == null)
                {
                    Count("ObjectOpacityFromTo(no endpoints)");
                    return true;
                }
                if (ev.Data.Has("opacity_delta"))
                    Count("ObjectOpacityFromTo(delta)");
                float o0 = from.Num("opacity") ?? 1f;
                float o1 = to.Num("opacity") ?? 1f;
                foreach (var t in Targets(ev, def, anchor))
                {
                    var fade = new OpacityFade(this, t, o0, o1, runTime);
                    if (instant || runTime <= 0f)
                        fade.Seek(runTime); // RESET_STATE / zero-length: land on the end opacity
                    else
                        AddMotion(fade, def, anchor);
                    _opsApplied++;
                }
                duration = instant ? 0f : runTime;
                return true;
            }

            case "ObjectMotion":
            {
                // OBJECT_MOTION is the original's rigid-body descriptor, and it spans two very
                // different jobs. Rotation-only events (XYZ_ROTATION alone) are steady spins —
                // zeppelin nacelle props (`spin`/`counterspin`, ∓40°/30°/s counter-rotating) and
                // rotating signage — and every one of the 590 OnStartup events install-wide is
                // exactly that shape, so the lightweight SpinMotion path below stays byte-for-byte
                // what the ambient world boots with. The rest pair motion with
                // GRAVITY/TRANSLATION/SCALE/FORWARD_ROTATION: ballistic debris and dust thrown by
                // a kill or a CRASH — reachable only from OnCall/WeaponHit (2,900+ events, zero at
                // bootstrap), which is why the MotionRuntime path here cannot regress the world.
                bool hasBallistic = ev.Data.Has("translation") || ev.Data.Has("translation_range")
                                    || ev.Data.Has("scale") || ev.Data.Has("forward_rotation");
                if (hasBallistic)
                {
                    // The full rigid-body simulation: a ballistic translate/launch, a scale ramp
                    // and a tumble (plus any steady XYZ_ROTATION), all on one node over run_time.
                    // See MotionRuntime for the semantics and the TUNE caveats.
                    float ballTime = ev.Data.Num("run_time") ?? 0f;
                    foreach (var t in Targets(ev, def, anchor))
                    {
                        var motion = MotionRuntime.Create(this, t, ev.Data, ballTime);
                        if (motion == null)
                            continue;
                        if (instant || ballTime <= 0f)
                            motion.Seek(0f); // RESET_STATE / zero-length: pose the launch start (rest)
                        else
                            AddMotion(motion, def, anchor);
                        _opsApplied++;
                    }
                    // BOUNCE_SEQUENCE (re-launch a piece on ground contact) is a Layer-1.5 follow-up
                    // — it needs do_intersections + a ground ray; the pieces read fine tumbling to
                    // rest without it. Report it so --debug-anim shows it is deferred, not missed.
                    if (ev.Data.Has("bounce_sequence"))
                        Count("ObjectMotion(bounce_sequence deferred)");
                    duration = instant ? 0f : ballTime;
                    return true;
                }
                // No motion channel: either a steady spin (below) or a bare GRAVITY/BOUNCE stub
                // with nothing to drive (meaningless without translation — reported, not acted on).
                if (ev.Data.Obj("xyz_rotation") is not { } spin)
                {
                    bool bareBallistic = ev.Data.Has("gravity") || ev.Data.Has("bounce_sequence");
                    Count(bareBallistic ? "ObjectMotion(ballistic)" : ev.Kind);
                    return true;
                }

                var rate = spin.Vec3("initial");
                // `delta` is a second rate triple whose meaning the data does not settle: it
                // reads as acceleration on a blown-up chassis and as a decelerating ramp on
                // `chuteman_sway`, and could equally be a random spread. 589 of the 590
                // reachable events leave it zero, so it is reported, not guessed — the same
                // call Object3DRotate's ambiguous angle unit got in MissionSetup.
                if (!spin.Vec3("delta").IsZeroApprox())
                    Count("ObjectMotion(rotation delta)");
                if (rate.IsZeroApprox())
                    return true;

                float spinFor = ev.Data.Num("run_time") ?? 0f;
                foreach (var t in Targets(ev, def, anchor))
                {
                    // Re-assertion is idempotent. These sit inside `Loop{-1}` sequences, so an
                    // already-turning prop would otherwise be rebuilt every frame — each rebuild
                    // re-reading rest from the current pose and restarting the clock at 0, which
                    // advances one frame's worth of angle and then throws it away. The prop would
                    // sit almost still while looking, in the logs, perfectly driven.
                    if (_motions.Any(m => m.Target == t && m is SpinMotion s && s.Matches(rate, spinFor)))
                        continue;
                    var motion = new SpinMotion(t, rate, spinFor);
                    if (instant)
                        motion.Seek(0f); // RESET_STATE poses the start; a spin starts unturned
                    else
                        AddMotion(motion, def, anchor);
                    _opsApplied++;
                }
                duration = instant ? 0f : spinFor;
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
                    var playback = new ScriptPlayback(this, t, script);
                    if (instant)
                        playback.Seek(0f); // pose at the script's first frame
                    else
                        AddMotion(playback, def, anchor);
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
                {
                    // A call may re-anchor the callee onto ANOTHER node. That is the data's
                    // template-instancing mechanism: the effect templates (the fire/firetrail/
                    // fireball roots) are parentless single copies, and the call is what puts
                    // one at a specific site — `CallAnimation{huge_30sec_fire, WithNode:
                    // rc*_dbase1}` burns one ship section. 29,633 WITH_NODE + 7,640 AT_NODE +
                    // 179 OPERAND_NODE call sites carry a target; ignoring it ran every one of
                    // them on the CALLER's anchor instead of where the data put it.
                    var (siteNode, siteOffset) = CallTargetSite(ev, def, anchor);
                    var callAnchor = siteNode ?? anchor;
                    foreach (var target in _program.ByAnimName(callName))
                        if (!IsLive(target, callAnchor))
                        {
                            // Re-anchoring alone is not enough for an effect template: its puffers
                            // ride the template's OWN root, so unless that root is MOVED to the call
                            // site the effect emits at its gamez origin. Relocate it here. Gated +
                            // non-instant so it touches only the animation debugger / crash runtime,
                            // never the ambient world boot (byte-identical regression) or RESET_STATE.
                            if (PlaceCalledTemplates && !instant && callAnchor != null)
                                PlaceTemplateAt(target, callAnchor, siteOffset);
                            Start(target, callAnchor);
                        }
                }
                return true;

            case "StopAnimation":
            case "InvalidateAnimation":
                Stop(ev.Data.Str("name"));
                return true;

            case "PufferState":
                HandlePufferState(ev, def, anchor);
                return true;

            case "LightState":
                HandleLightState(ev, def, anchor);
                return true;

            case "LightAnimation":
                HandleLightAnimation(ev, anchor, instant);
                return true;

            case "SoundNode":
                HandleSoundNode(ev, def, anchor);
                return true;

            case "ObjectAddChild":
                // Only the sound-emitter three-quarters of this event is acted on — see
                // HandleAddChild. Everything else it does still counts as unhandled.
                if (!HandleAddChild(ev, def, anchor))
                    Count(ev.Kind);
                return true;

            default:
                // One-shot Sound / opacity / texture-cycle / FBFX / camera and the
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
    // second emitter. The value carries the owning (def, anchor) so Stop can tear down exactly
    // the emitters a stopped instance created.
    private readonly Dictionary<(string Name, Node3D Node), (Effects.Puffer Puffer, AnimDefinition Def, Node3D? Anchor)> _puffers = new();
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
                running.Puffer.SustainEnd();
                _activePuffers.RemoveAll(a => a.Puffer == running.Puffer);
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
        _puffers[key] = (puffer, def, anchor);
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

    // ---- SOUND_NODE (+ the sound half of OBJECT_ADD_CHILD) ----

    /// <summary>The world's ambient 3D emitters. Null in a muted or soundless session, in which
    /// case SOUND_NODE is tracked and reported but nothing is built.</summary>
    public WorldSounds? Sounds;

    // Keyed by (sound name, anchor), like the lights and for the same reason: the anchor
    // identifies the *instance* of the definition, so C1's four firetrucks each get their own
    // siren rather than sharing one. It cannot be keyed by host node the way puffers are — in the
    // reader's triple the emitter is declared BEFORE anything says where it goes.
    private readonly Dictionary<(string Name, Node3D? Anchor), object> _soundEmitters = new();
    private int _soundsUnknown, _soundsAfterBuild;

    /// <summary>Set once <see cref="Bootstrap"/> has printed its emitter census. After this, a
    /// failed SOUND_NODE is invisible unless reported at the point of use — which is exactly how
    /// C1's police siren stayed silent undetected: the census is a bootstrap
    /// snapshot, so it cannot distinguish "never requested" from "requested later and failed".
    /// Reported once per name, not per event: snd_fire1 alone has 363 sites.</summary>
    private bool _soundCensusPrinted;
    private readonly HashSet<string> _soundFailuresReported = new(StringComparer.OrdinalIgnoreCase);

    private void ReportLateSoundFailure(string name, string why)
    {
        if (!_soundCensusPrinted || !_soundFailuresReported.Add(name))
        {
            return;
        }
        GD.PushWarning($"anim: SOUND_NODE '{name}' requested after the world build and {why} — "
                       + "it will be silent for the rest of the session");
    }

    /// <summary>
    /// The emitter an event's NAME refers to, or null when the name isn't one this definition
    /// declared. This is what lets OBJECT_ACTIVE_STATE and OBJECT_ADD_CHILD — both perfectly
    /// ordinary node events elsewhere — address a sound emitter without either handler having to
    /// guess from the name whether `snd_waterfall` is a node or a sound.
    /// </summary>
    private object? SoundEmitter(AnimEvent ev, Node3D? anchor)
    {
        if (Sounds == null || ev.Data.Str("name") is not { } name)
            return null;
        return _soundEmitters.TryGetValue((name, anchor), out var handle) ? handle : null;
    }

    /// <summary>
    /// Declares (and for the compiled form, places and starts) one ambient emitter.
    ///
    /// The two front-ends spell this differently and both are handled here. A reader def writes a
    /// three-event triple — <c>SOUND_NODE{snd_waterfall}</c>, <c>OBJECT_ACTIVE_STATE{snd_waterfall,
    /// ACTIVE}</c>, <c>OBJECT_ADD_CHILD{waterfall01, snd_waterfall}</c> — so this event only
    /// declares, and the other two arrive as their own dispatches. A compiled event carries
    /// <c>active_state</c> and <c>translate</c> inline, but only sometimes: measured install-wide,
    /// <c>translate</c> is an <c>AtNode</c> on 379 events and null on exactly 865 — and 865 is also
    /// exactly the number of <c>OBJECT_ADD_CHILD</c> events that attach a sound definition. The two
    /// halves are one mechanism, which is why they land together.
    /// </summary>
    private void HandleSoundNode(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        if (ev.Data.Str("name") is not { } name)
            return;
        if (Sounds == null)
        {
            _soundsAfterBuild++;
            ReportLateSoundFailure(name, "there is no audio session");
            return;
        }

        var key = (name, anchor);
        if (!_soundEmitters.TryGetValue(key, out var handle))
        {
            // Re-assertion must be a no-op, not a second emitter: the data keeps its definitions
            // alive with `[SOUND_NODE, …, Loop{-1}]` exactly as it does for puffers.
            if (Sounds.Create(name) is not { } created)
            {
                _soundsUnknown++;
                ReportLateSoundFailure(name, "no stream could be resolved for it "
                                             + "(unknown to sounds.json, or never prewarmed)");
                return;
            }
            handle = created;
            _soundEmitters[key] = handle;
            _opsApplied++;
        }

        // AT_NODE — the compiled form's own placement. Absent in the reader form and in the 865
        // compiled events that leave it to OBJECT_ADD_CHILD.
        if (ev.Data.Obj("translate")?.Union() is { Tag: "AtNode", Value: Dictionary<string, object?> at })
        {
            var atData = new AnimData(at);
            if (atData.Str("name") is { } hostName && ResolveOne(hostName, def, anchor) is { } host)
                Sounds.Attach(handle, host, atData.Vec3("pos"));
        }
        // The reader form carries no active_state at all (its ACTIVE comes as the next event), so
        // an absent field must mean "leave it alone" rather than the `?? 0` = OFF that silently
        // killed the C1 waterfall's puffers when PUFFER_STATE had no reader normalizer.
        //
        // Note the compiled field is a JSON **boolean** here, where PUFFER_STATE's same-named field
        // is numeric — reading it with Num() alone returns null for `true` and leaves every emitter
        // in the world switched off, which is exactly what it did until the --debug-anim log showed
        // 38 correctly-placed emitters all reading "off".
        if (ev.Data.Has("active_state"))
            Sounds.SetActive(handle, ev.Data.Bool("active_state") || ev.Data.Num("active_state") >= 1f);
    }

    /// <summary>
    /// The sound-emitter case of OBJECT_ADD_CHILD: attach a declared emitter to the world node
    /// that positions it. Returns false for every other use, which stays counted as unhandled.
    ///
    /// This is deliberately only the sound subset. Surveying all 1,152 <c>ObjectAddChild</c> events
    /// found 865 (75%) attach sound *definitions* rather than nodes (<c>snd_zepengine</c>→spin
    /// alone is 849), ~148 are cutscene machinery for cutscenes this project does not have, and the
    /// rest are mission-cutscene entities. So the general reparenting form has nothing to act on
    /// here, and the one form that does is this one.
    /// </summary>
    private bool HandleAddChild(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        if (Sounds == null || ev.Data.Str("child") is not { } child)
            return false;
        if (!_soundEmitters.TryGetValue((child, anchor), out var handle))
            return false;
        if (ev.Data.Str("parent") is not { } parentName)
            return false;
        if (ResolveOne(parentName, def, anchor) is not { } host)
            return false;
        Sounds.Attach(handle, host);
        _opsApplied++;
        return true;
    }

    // ---- LIGHT_STATE / LIGHT_ANIMATION ----

    /// <summary>
    /// One of the world's animated point lights. What the original did with these was modulate
    /// the vertex lighting of nearby geometry; the visible flare at the light's own position is
    /// separate gamez Facade geometry that already renders (C1's <c>docklight_flare</c> →
    /// <c>dock_liteflare.tif</c>, <c>flame01</c> → <c>fire101.tif</c>). See
    /// <see cref="WorldLights"/> for how the spill reaches the fullbright shader.
    /// </summary>
    private sealed class AnimLight
    {
        public Node3D? Host;          // AT_NODE target — the light rides its world pose
        public string? HostName;      // the AT_NODE name Host was resolved from (see HandleLightState)
        public Vector3 Offset;        // AT_NODE's trailing offset, in the host's own frame
        public Color Color = new(1f, 1f, 1f);
        public float RangeMin, RangeMax;
        public bool Active;

        // LIGHT_ANIMATION: signed deltas applied over run_time (see HandleLightAnimation).
        public float TweenLeft;
        public Color ColorRate;
        public float MinRate, MaxRate;
    }

    // Keyed by (light name, anchor). The anchor identifies the *instance* of the definition,
    // and a definition's `lights` array is its own symbol table — so two refineries each get
    // their own orange_light. It cannot be keyed by host node the way puffers are: the flicker
    // events are partial updates carrying only {name, range}, with no AT_NODE to resolve from.
    private readonly Dictionary<(string Name, Node3D? Anchor), AnimLight> _lights = new();

    /// <summary>Where the world's lights are delivered. Null outside a lit session, in which
    /// case LIGHT_STATE is tracked but never rendered.</summary>
    public WorldLights? Lights;

    /// <summary>
    /// Applies one LIGHT_STATE. The load-bearing detail is that this is a **partial update**:
    /// the fire and refinery flickers are streams of <c>{name, range}</c> events 0.03–0.07 s
    /// apart that must leave position, colour and active state untouched (the compiled form
    /// spells the absent fields null — measured install-wide: translate null on 707 of 1468
    /// events, colour null on 703, and range null on exactly the 321 that switch a light off).
    /// So every field is applied only when present, never defaulted.
    /// </summary>
    private void HandleLightState(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        if (ev.Data.Str("name") is not { } name)
            return;
        var key = (name, anchor);
        if (!_lights.TryGetValue(key, out var light))
            _lights[key] = light = new AnimLight { Host = anchor };

        // AT_NODE arrives as translate:{AtNode:{name, pos}} — node plus a local offset, the same
        // shape (and the same frame) as a puffer's AT_NODE.
        if (ev.Data.Obj("translate")?.Obj("AtNode") is { } at)
        {
            // Resolve the host ONCE per light. These events are not occasional: a fire's flicker
            // re-issues its full LIGHT_STATE — AT_NODE and all — every loop iteration, measured
            // at ~2,700 LIGHT_STATEs/second on C1, of which ~1,740 missed the compiled symbol
            // table and fell through to ResolveOne's full-world scan (7,064 nodes with a regex
            // matcher, so ~12M comparisons/second). That, not the shader, was the entire cost of
            // this feature: --perf put the whole viewport's GPU time at 0.26 ms while script time
            // sat near 50 ms. The name is what identifies the target, so re-resolving an
            // unchanged one can only produce the node we already have.
            if (at.Str("name") is { } hostName)
            {
                if (light.Host == null || !string.Equals(hostName, light.HostName, StringComparison.Ordinal))
                {
                    if (ResolveOne(hostName, def, anchor) is { } host)
                        light.Host = host;
                    light.HostName = hostName;
                }
            }
            light.Offset = at.Vec3("pos");
        }
        if (ev.Data.Obj("range") is { } range)
        {
            light.RangeMin = range.Num("min") ?? light.RangeMin;
            light.RangeMax = range.Num("max") ?? light.RangeMax;
        }
        if (ev.Data.Obj("color") is { } color)
            light.Color = new Color(color.Num("r") ?? 0f, color.Num("g") ?? 0f, color.Num("b") ?? 0f);
        if (ev.Data.Has("active_state"))
        {
            light.Active = ev.Data.Bool("active_state");
            light.TweenLeft = 0f; // switching a light re-arms it; a half-run pulse must not carry over
        }
        _opsApplied++;
    }

    /// <summary>
    /// Applies one LIGHT_ANIMATION: signed **deltas** to a light's range and colour, ramped over
    /// <c>run_time</c>. They are deltas, not targets — C1B's <c>ap_light</c> pulse runs
    /// {min +50, max +160} over 0.1 s and then {min −50, max −160} over 0.05 s, and a negative
    /// range is not a value a light can hold. Under <paramref name="instant"/> (a RESET_STATE)
    /// the delta lands whole, matching how timed motions collapse to their end pose there.
    /// </summary>
    private void HandleLightAnimation(AnimEvent ev, Node3D? anchor, bool instant)
    {
        if (ev.Data.Str("name") is not { } name
            || !_lights.TryGetValue((name, anchor), out var light))
        {
            Count("LightAnimation(no light)");
            return;
        }
        var range = ev.Data.Obj("range");
        var color = ev.Data.Obj("color");
        float dMin = range?.Num("min") ?? 0f, dMax = range?.Num("max") ?? 0f;
        var dColor = new Color(color?.Num("r") ?? 0f, color?.Num("g") ?? 0f, color?.Num("b") ?? 0f);
        float runTime = ev.Data.Num("run_time") ?? 0f;

        if (instant || runTime <= 0f)
        {
            light.RangeMin += dMin;
            light.RangeMax += dMax;
            light.Color += dColor;
            light.TweenLeft = 0f;
        }
        else
        {
            light.MinRate = dMin / runTime;
            light.MaxRate = dMax / runTime;
            light.ColorRate = dColor / runTime;
            light.TweenLeft = runTime;
        }
        _opsApplied++;
    }

    /// <summary>Advances light tweens and submits every active light at its host's current world
    /// pose. Per frame, because hosts move (a muzzle flash rides its turret).</summary>
    private void TickLights(float dt)
    {
        if (Lights == null)
            return;
        Lights.Begin();
        foreach (var light in _lights.Values)
        {
            if (light.TweenLeft > 0f)
            {
                float step = Mathf.Min(dt, light.TweenLeft);
                light.RangeMin += light.MinRate * step;
                light.RangeMax += light.MaxRate * step;
                light.Color += light.ColorRate * step;
                light.TweenLeft -= step;
            }
            if (!light.Active || light.RangeMax <= 0f)
                continue;
            // A light inside a subtree the mission deactivated is off, exactly like the flare
            // sprite sitting at the same place: the destroyed variant of a building must not
            // keep lighting the ground through its healthy twin.
            if (light.Host is not { } host || !IsInstanceValid(host) || !host.IsVisibleInTree())
                continue;
            Lights.Add(host.GlobalTransform * light.Offset, light.Color, light.RangeMin, light.RangeMax);
        }
        Lights.Commit(PlayerPos());
        if (DebugMotions)
            Lights.LogOnce();
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

    /// <summary>
    /// The site a CALL_ANIMATION hands its callee: the target node plus the AT_NODE trailing
    /// offset (in that node's frame). The node is null when the call names no target (then the
    /// caller's own anchor stands, which is what every call used to get); the offset is zero
    /// unless the call carries a <c>position</c>.
    ///
    /// The target is written in the CALLER's namespace, so it resolves through the caller's
    /// definition and scope. Three spellings reach here as two shapes: compiled events nest
    /// <c>WITH_NODE</c>/<c>AT_NODE</c> under <c>parameters</c> as a one-key union, which is
    /// also what the reader front-end normalizes to; <c>OPERAND_NODE</c> stays a bare name.
    ///
    /// A named-but-unresolvable target falls back to the caller's anchor rather than dropping
    /// the call — that is the pre-change behaviour, so a node the builder skipped can't make
    /// an effect disappear — but it is counted, since silently mis-placing an effect is
    /// exactly the failure this method exists to fix.
    /// </summary>
    private (Node3D? Node, Vector3 Offset) CallTargetSite(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        string? targetName = null;
        Vector3 offset = Vector3.Zero;
        if (ev.Data.Obj("parameters")?.Union() is { Value: Dictionary<string, object?> p })
        {
            var atNode = new AnimData(p);
            targetName = atNode.Str("node");
            offset = atNode.Vec3("position"); // absent → zero
        }
        targetName ??= ev.Data.Str("operand_node");
        if (targetName == null)
            return (null, Vector3.Zero);

        var resolved = ResolveOne(targetName, def, anchor);
        if (resolved != null)
            _retargeted++;
        else
            _retargetUnresolved++;
        // Once per distinct (callee, target, caller) triple. The data's poll idiom re-issues
        // its calls every frame, so an unconditional line here would bury the log — the same
        // reason condition logging prints only first-evaluation and verdict flips.
        if (DebugMotions && _retargetsLogged.Add($"{ev.Data.Str("name")}|{targetName}|{def.AnimName}"))
            GD.Print($"anim: retarget '{ev.Data.Str("name")}' onto '{targetName}' "
                     + $"({(resolved != null ? resolved.GetMeta(NameMeta).AsString() : "UNRESOLVED")})"
                     + $" [caller {def.AnimName}]");
        return (resolved, offset);
    }

    /// <summary>Moves an effect template's own root(s) to a call site so its puffers — which
    /// ride that root (<c>yellow_spark_01</c>, <c>fly_trailN</c>, …), NOT the caller's anchor —
    /// emit there instead of at the template's gamez origin. This is the template-instancing the
    /// original does by copying the template mesh per call; here the single shared template is
    /// relocated, so overlapping calls to the same template collapse onto the last site (the
    /// staggered-cluster nuance is a fidelity follow-up). Only the world position is set — the
    /// puffers key off the host origin — and the offset is applied in the site's own frame.</summary>
    private void PlaceTemplateAt(AnimDefinition callee, Node3D site, Vector3 offset)
    {
        var siteXform = site.GlobalTransform;
        var origin = siteXform.Origin + siteXform.Basis * offset;
        foreach (var root in Anchors(callee))
        {
            if (root == null || !IsInstanceValid(root))
                continue;
            var xf = root.GlobalTransform;
            xf.Origin = origin;
            root.GlobalTransform = xf;
        }
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
            // Read against the LIVE per-instance HP (C21), not the def's authored value, so a
            // tower damaged to 30 smokes while its undamaged siblings do not. Until C23 wires
            // weapon damage nothing decrements HP, so every instance sits at full health and
            // these stay uniformly false — the pre-C21 behaviour, unchanged.
            "AnimHealth" => HealthOf(def, anchor) <= num,
            "AnimHealthRange" => obj != null
                                 && HealthOf(def, anchor) >= (obj.Num("min") ?? 0f)
                                 && HealthOf(def, anchor) <= (obj.Num("max") ?? 0f),
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

    /// <summary>The live HP an <c>ANIM_HEALTH</c> threshold tests against: the registered
    /// destructible instance for this <c>(def, anchor)</c> pair, falling back to the def's
    /// authored value when the pair is not a registered destructible (an unanchored evaluation,
    /// or a def whose NAME resolved nothing at bootstrap). The fallback reproduces the exact
    /// pre-C21 read, so anything the registry does not cover behaves as it always did.</summary>
    private float HealthOf(AnimDefinition def, Node3D? anchor) =>
        _destructibles.Get(def, anchor)?.Health ?? def.Health;

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

    // CALL_ANIMATION retargeting tallies. Deliberately NOT routed through Count(), which is
    // the "event kinds not yet acted on" channel — a retargeted call is acted on, and filing
    // it there would report a working feature as a missing one.
    private int _retargeted, _retargetUnresolved;
    private readonly HashSet<string> _retargetsLogged = new(StringComparer.Ordinal);

    private void ReportRetargets()
    {
        if (_retargeted == 0 && _retargetUnresolved == 0)
            return;
        GD.Print($"anim: {_retargeted} call(s) retargeted onto a named node"
                 + (_retargetUnresolved > 0 ? $", {_retargetUnresolved} target(s) unresolved" : ""));
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

    /// <summary>Which of a node's channels a motion drives. A node carries at most ONE motion
    /// per channel (see <see cref="AddMotion"/>): the transform channel (translate/rotate/scale,
    /// all held in <c>Target.Transform</c>) and the opacity channel (the <c>csky_opacity</c>
    /// shader parameter). They are independent — the crash dust ramps its scale via a
    /// <see cref="MotionRuntime"/> while an <see cref="OpacityFade"/> fades it out — so a fade
    /// must not evict a live transform motion, nor a transform motion a live fade.</summary>
    private enum MotionChannel { Transform, Opacity }

    /// <summary>A motion that owns one of a node's channels over time (an SI script playback, a
    /// from→to tween, a ballistic body, or an opacity fade). The runtime ticks them centrally so
    /// a node driven by two motions on the SAME channel resolves to the later one
    /// deterministically.</summary>
    private interface IAnimMotion
    {
        Node3D Target { get; }
        // The (def, anchor) instance that registered this motion, so Stop can tear down exactly
        // the motions a stopped instance drives. Set by AddMotion; ownership transfers when a
        // later instance's motion replaces an earlier one on the same target.
        (AnimDefinition Def, Node3D? Anchor) Owner { get; set; }
        bool Finished { get; }
        // Almost every motion drives the transform; only OpacityFade overrides this. A default
        // interface member (C# 8) so the three pre-existing transform motions need no change.
        MotionChannel Channel => MotionChannel.Transform;
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
        if (_activePuffers.Count > 0)
        {
            int live = 0;
            foreach (var a in _activePuffers)
                live += a.Puffer.LiveCount;
            GD.Print($"anim/debug: {_activePuffers.Count} active puffer(s), {live} live particle(s)");
        }
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
            // Rotation as well as position: a spin (OBJECT_MOTION) turns a prop in place, so a
            // position-only line is identical every second whether or not it is actually
            // running. The euler triple is what makes that verifiable headlessly.
            var r = t.Transform.Basis.GetEuler() * (180f / Mathf.Pi);
            // Visibility matters as much as position here: a correctly-animated node inside a
            // subtree the mission deactivated moves perfectly and renders nothing.
            bool shown = t.IsVisibleInTree();
            GD.Print($"anim/debug: {name} at ({p.X:0.0}, {p.Y:0.0}, {p.Z:0.0}) "
                     + $"rot ({r.X:0.0}, {r.Y:0.0}, {r.Z:0.0}) {(shown ? "visible" : "HIDDEN")}");
        }
        if (_motions.Count > 12)
            GD.Print($"anim/debug: … and {_motions.Count - 12} more");
    }

    /// <summary>Registers a motion, replacing any motion already driving the same node. An
    /// object has exactly one motion in the original, and the data relies on it: C1/IA1's
    /// startanims run `hangar3_doors` (doors to ±50) and then `mp_hangar3_open` (the same
    /// doors to ±25), where the later one is meant to win. Without this both tween the same
    /// node every frame and the outcome depends on list order.</summary>
    private void AddMotion(IAnimMotion motion, AnimDefinition def, Node3D? anchor)
    {
        motion.Owner = (def, anchor);
        // Evict only a prior motion on the SAME channel: a node can carry one transform motion
        // AND one opacity fade at once (the crash dust scales via a MotionRuntime while an
        // OpacityFade fades it), and those write different data, so neither displaces the other.
        _motions.RemoveAll(m => m.Target == motion.Target && m.Channel == motion.Channel);
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
    /// Loops when the owning sequence loops — the runner restarts it.
    ///
    /// <para>Rotation, translation and scale are held as three SEPARATE running components
    /// seeded from the node's authored rest pose, not read back out of the live transform.
    /// Both halves of that matter. Reading the live basis made this the one transform writer
    /// that could compound: a frame carrying <c>scale</c> but no <c>rotate</c> multiplied its
    /// factor into an already-scaled basis every single frame, and a looping sequence
    /// re-registering the playback re-entered at the blown-up pose (207 such frames across 12
    /// scripts, all of them the C1 zeppelins). Keeping the components apart is what makes
    /// `Seek(t)` a pure function of `t` rather than of call history. But they must be
    /// components rather than one rest transform, because an ABSENT channel means "hold the
    /// last value this script wrote", not "return to rest": C1/M04's `piratezep` sets its
    /// orientation once in frame 0 and then ships 47 translate-only frames that must keep
    /// it.</para></summary>
    private sealed class ScriptPlayback : IAnimMotion
    {
        public Node3D Target { get; }
        public (AnimDefinition Def, Node3D? Anchor) Owner { get; set; }
        private readonly SiScript _script;
        private Basis _rot;      // orthonormal; the scale is kept out of it on purpose
        private Vector3 _scale;
        private Vector3 _origin;
        private float _t;

        public ScriptPlayback(AnimRuntime rt, Node3D target, SiScript script)
        {
            Target = target;
            _script = script;
            var rest = rt.RestOf(target);
            _rot = rest.Basis.Orthonormalized();
            _scale = rest.Basis.Scale;
            _origin = rest.Origin;
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
            if (frame.Rotate != null)
                _rot = new Basis(frame.Rotate.At(dt));
            if (frame.Translate != null)
                _origin = frame.Translate.At(dt);
            if (frame.Scale != null && frame.Scale.At(dt) is { } s && s.IsFinite() && s.LengthSquared() > 1e-9f)
                _scale = s;
            Target.Transform = new Transform3D(_rot.Scaled(_scale), _origin);
        }
    }

    /// <summary>A steady spin about the node's own axes at a fixed rate (OBJECT_MOTION's
    /// XYZ_ROTATION), which is what turns the zeppelin nacelle props and the rotating signs.
    /// Endless unless the event gave a RUN_TIME — 580 of the 590 reachable spins are endless,
    /// so `Finished` staying false forever is the normal case, not a leak. Rotation is applied
    /// about the LOCAL axes like every other prop in this project (PropAnimator does the same
    /// for the player's aircraft), and accumulated from a stored rest pose rather than
    /// integrated per frame so a long session cannot drift.</summary>
    private sealed class SpinMotion : IAnimMotion
    {
        public Node3D Target { get; }
        public (AnimDefinition Def, Node3D? Anchor) Owner { get; set; }
        private readonly Basis _rest;
        private readonly Vector3 _rate; // radians/second, local axes
        private readonly float _runTime; // 0 = endless
        private float _t;

        public SpinMotion(Node3D target, Vector3 rate, float runTime)
        {
            Target = target;
            _rest = target.Transform.Basis;
            _rate = rate;
            _runTime = runTime;
            Seek(0f);
        }

        public bool Finished => _runTime > 0f && _t >= _runTime;

        /// <summary>Whether an incoming registration is this same spin, so a looping sequence
        /// re-asserting it can be left alone instead of restarted.</summary>
        public bool Matches(Vector3 rate, float runTime) =>
            _rate.IsEqualApprox(rate) && Mathf.IsEqualApprox(_runTime, runTime);

        public void Tick(float dt) => Seek(_t + dt);

        public void Seek(float t)
        {
            _t = _runTime > 0f ? Mathf.Min(t, _runTime) : t;
            var a = _rate * _t;
            // Applied X→Y→Z about the local axes. Order is only observable when two axes spin
            // at once, which nothing reachable in this install does (every reached event is
            // single-axis); recheck this if a multi-axis spin ever turns up looking wrong.
            var b = _rest;
            if (a.X != 0f) b = b.Rotated(b.X.Normalized(), a.X);
            if (a.Y != 0f) b = b.Rotated(b.Y.Normalized(), a.Y);
            if (a.Z != 0f) b = b.Rotated(b.Z.Normalized(), a.Z);
            var xf = Target.Transform;
            xf.Basis = b;
            Target.Transform = xf;
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
    /// <para>An ABSENT channel means "HOLD the last value written to this node", NOT "return to
    /// the authored rest pose" — the same rule <c>ScriptPlayback</c> above documents, and for
    /// the same reason. The pose is therefore held as three separate components seeded from the
    /// node's LIVE transform at the moment the event fires, not from <c>RestOf</c>. Reading the
    /// rest pose instead is what made C1's traffic drive mis-headed: <c>police_chase</c> sets
    /// <c>suspect</c>/<c>police_car</c> to 45° with an OBJECT_ROTATE_STATE and then plays a
    /// translate-only 2 s leg, which snapped both cars back to 0° for the diagonal; the same
    /// def parks <c>suspect</c> for 27 s on a translate-only event that must hold -110°.
    /// Surveyed install-wide: 883 of 1,802 OBJECT_MOTION_FROM_TO events carry no rotate
    /// channel, and on 89 of them (26 nodes — the C1 traffic and firetrucks, C2's ten
    /// studebakers, its sailboats and yachts) the value to hold differs from the authored rest,
    /// up to <c>sailboat1</c>'s 300 s leg held 180° out. The components are kept SEPARATE
    /// rather than as one transform for `ScriptPlayback`'s reason: it keeps <c>Seek(t)</c> a
    /// pure function of <c>t</c>, and a rotate channel can no longer silently discard the
    /// node's scale. They are seeded ONCE per event rather than re-read per frame, so a tween
    /// cannot compound into itself.</para>
    ///
    /// The separate <c>*_delta</c> channels are the genuinely relative ones and compose on top
    /// of the held pose. ⚠ **They are unreachable today**: the compiled form ships all 26 of
    /// them (15 translate, 6 rotate, 5 scale) as a bare <c>{x,y,z}</c> vector, not as the
    /// <c>{from,to}</c> pair <c>Channel</c> looks for, so every one parses to (null, null) and
    /// is dropped; the reader front-end emits no delta channel at all. Fixing that is its own
    /// change with its own regression — see `backlog.md`.
    ///
    /// Rotations are RADIANS. The data's extremes settle it: the maximum is 15.708 = 5π,
    /// 99.93% of values are ≤ 2π, and 228 sit on exact π/2 multiples. Running them through
    /// DegToRad made every rotation ~57× too small, i.e. visually nothing turned.
    ///
    /// A missing FROM means "from where the node already is" — the held component, which is
    /// what <c>AnimDefs.AddFromTo</c> has always documented as the intent — or "from no offset"
    /// for the delta channels. Every absolute channel in the compiled data ships both ends
    /// (919/919 rotate, 401/401 translate, 663/663 scale), so this only bites the reader path.
    /// </summary>
    private sealed class FromToMotion : IAnimMotion
    {
        public Node3D Target { get; private init; } = null!;
        public (AnimDefinition Def, Node3D? Anchor) Owner { get; set; }
        // The pose this event starts from, as components: an absent channel carries its
        // component through untouched. Orthonormal rotation with the scale kept out of it,
        // exactly as ScriptPlayback holds them.
        private Basis _heldRot;
        private Vector3 _heldEuler;   // _heldRot as Yxz euler, for the missing-FROM fallback
        private Vector3 _heldScale;
        private Vector3 _heldOrigin;
        private Vector3? _tFrom, _tTo, _rFrom, _rTo, _sFrom, _sTo;
        private Vector3? _tdFrom, _tdTo, _rdFrom, _rdTo, _sdFrom, _sdTo;
        private float _t, _runTime;

        public bool Finished => _t >= _runTime;

        public static FromToMotion? Create(AnimRuntime rt, Node3D target, AnimData data, float runTime)
        {
            // RestOf is still called for its side effect — it records the authored pose the
            // first time anything touches the node, which PoseRotate/PoseScale read back — and
            // as the fallback when the live pose is unusable.
            var rest = rt.RestOf(target);
            var held = target.Transform;
            float det = held.Basis.Determinant();
            if (!float.IsFinite(det) || Mathf.Abs(det) < 1e-9f || !held.Origin.IsFinite())
            {
                // A blown-up or singular live basis would poison every later event on this
                // node; the authored pose is the only sane thing left to hold.
                held = rest;
            }
            var rot = held.Basis.Orthonormalized();
            var m = new FromToMotion
            {
                Target = target,
                _heldRot = rot,
                _heldEuler = rot.GetEuler(EulerOrder.Yxz),
                _heldScale = held.Basis.Scale,
                _heldOrigin = held.Origin,
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

            // Each channel either drives its component or leaves the held value alone; the
            // deltas then compose on top of whichever of the two won.
            var origin = _heldOrigin;
            if (_tTo is { } tTo)
                origin = (_tFrom ?? _heldOrigin).Lerp(tTo, u);
            if (_tdTo is { } tdTo)
                origin += (_tdFrom ?? Vector3.Zero).Lerp(tdTo, u);

            var rot = _heldRot;
            if (_rTo is { } rTo)
                rot = Euler((_rFrom ?? _heldEuler).Lerp(rTo, u));
            if (_rdTo is { } rdTo)
                rot *= Euler((_rdFrom ?? Vector3.Zero).Lerp(rdTo, u));

            var scale = _heldScale;
            if (_sTo is { } sTo)
                scale = (_sFrom ?? _heldScale).Lerp(sTo, u);
            if (_sdTo is { } sdTo)
                scale *= (_sdFrom ?? Vector3.One).Lerp(sdTo, u);

            Target.Transform = new Transform3D(rot.Scaled(scale), origin);
        }

        private static Basis Euler(Vector3 radians) => Basis.FromEuler(radians, EulerOrder.Yxz);
    }

    /// <summary>A timed translucency fade (OBJECT_OPACITY_FROM_TO): lerp the subtree's opacity
    /// from one value to another over the run time, through the same per-instance
    /// <c>csky_opacity</c> shader parameter <see cref="SetSubtreeOpacity"/> writes. This is the
    /// opacity channel — it does not touch the transform — so it coexists with a transform motion
    /// on the same node (see <see cref="MotionChannel"/>). The endpoints are literal opacity
    /// values (the endpoint `state` flag does not invert them; see the dispatch case), so no rest
    /// pose is needed: the two numbers fully determine the fade.</summary>
    private sealed class OpacityFade : IAnimMotion
    {
        public Node3D Target { get; }
        public (AnimDefinition Def, Node3D? Anchor) Owner { get; set; }
        public MotionChannel Channel => MotionChannel.Opacity;
        private readonly AnimRuntime _rt;
        private readonly float _from, _to, _runTime;
        private float _t;

        public OpacityFade(AnimRuntime rt, Node3D target, float from, float to, float runTime)
        {
            _rt = rt;
            Target = target;
            _from = from;
            _to = to;
            _runTime = Mathf.Max(runTime, 0f);
            Seek(0f);
        }

        public bool Finished => _t >= _runTime;

        public void Tick(float dt) => Seek(_t + dt);

        public void Seek(float t)
        {
            _t = t;
            float u = _runTime <= 0f ? 1f : Mathf.Clamp(t / _runTime, 0f, 1f);
            _rt.SetSubtreeOpacity(Target, Mathf.Lerp(_from, _to, u));
        }
    }

    /// <summary>
    /// The full OBJECT_MOTION rigid body: a ballistic translate/launch, a scale ramp and a
    /// tumble, driving one node over its run time, in the node's own parent frame (the same
    /// absolute-in-parent-frame convention as <see cref="FromToMotion"/>). Each channel is
    /// optional and an absent one holds the node's live value, seeded once at creation so the
    /// motion cannot compound into itself. This is the reachable half of OBJECT_MOTION that the
    /// old handler only counted — thrown by a crash or (in M3) a weapon hit; nothing ambient
    /// fires it.
    ///
    /// <para><b>Semantics</b> (established from <c>player_crash_dirt</c>'s pieces,
    /// <c>call_crash_trails</c> and <c>flydirt</c>; ⚠ several are TUNE, not a settled decode):
    /// <list type="bullet">
    /// <item><c>translation.initial</c> is the launch VELOCITY (a piece leaves at y=10 m/s);
    ///   <c>rnd_xz</c> a per-axis random spread added to it (through the runtime's seedable
    ///   <c>_rng</c>, so a lab replay is deterministic); <c>delta</c> a velocity ramp over the
    ///   run time (change from initial to initial+delta) — 0 on every reachable piece, so its
    ///   exact reading is near-invisible.</item>
    /// <item><c>translation_range</c> is a RANGED ballistic launch (the burning-debris arcs): a
    ///   random horizontal distance <c>xz</c> and vertical <c>y</c> travelled over the run time,
    ///   fired in a random azimuth — <c>vHoriz = xz/run_time</c>,
    ///   <c>vVert = y/run_time − ½·g·run_time</c> (so the arc reaches <c>y</c> at the end).
    ///   ⚠ undocumented and never simulated before; <c>initial</c>/<c>delta</c> are unmapped.</item>
    /// <item><c>gravity.value</c> (negative) accelerates the launch; folded straight into the
    ///   constant acceleration. <c>do_intersections</c> ground-rest and the <c>bounce_sequence</c>
    ///   re-launch are a Layer-1.5 follow-up (they need a physics ray) — the body integrates
    ///   freely over the run time and then finishes.</item>
    /// <item><c>forward_rotation.Time.initial</c> is a tumble RATE (rad/s) about the node's local
    ///   X axis (a piece = 15.708 = 900°/s). ⚠ the axis is a reasoned choice — the data carries a
    ///   scalar rate, not an axis — an end-over-end tumble about the local X reads well for
    ///   scattered wreckage.</item>
    /// <item><c>xyz_rotation.initial</c> a steady multi-axis spin (rad/s), composed like
    ///   <see cref="SpinMotion"/>; present only on the rare spin+ballistic events.</item>
    /// <item><c>scale.initial</c> a start scale and <c>scale.delta</c> the change over the run
    ///   time, a linear ramp (the dust: (3.5,10,3.5) → (2.5,5,2.5) over 6 s). Absolute, like
    ///   <see cref="PoseScale"/>, so it replaces the held scale rather than multiplying it.</item>
    /// </list></para>
    /// </summary>
    private sealed class MotionRuntime : IAnimMotion
    {
        public Node3D Target { get; private init; } = null!;
        public (AnimDefinition Def, Node3D? Anchor) Owner { get; set; }

        // Held pose, seeded once from the live transform: an absent channel carries it through.
        private Basis _heldRot;      // orthonormal; scale kept out
        private Vector3 _heldScale;
        private Vector3 _heldOrigin;

        // Ballistic: origin(t) = held + v0·t + ½·accel·t²  (accel folds gravity + any velocity ramp).
        private Vector3 _v0, _accel;
        private bool _hasBallistic;

        // Scale ramp: scale(t) = init + delta·u,  u = t/run_time clamped.
        private Vector3 _scaleInit, _scaleDelta;
        private bool _hasScale;

        private float _tumbleRate;   // rad/s about local X (forward_rotation)
        private Vector3 _spinRate;   // rad/s per local axis (xyz_rotation)

        private float _t, _runTime;

        public bool Finished => _t >= _runTime;

        public static MotionRuntime? Create(AnimRuntime rt, Node3D target, AnimData data, float runTime)
        {
            var rest = rt.RestOf(target); // records the authored pose; the fallback for a bad live basis
            var held = target.Transform;
            float det = held.Basis.Determinant();
            if (!float.IsFinite(det) || Mathf.Abs(det) < 1e-9f || !held.Origin.IsFinite())
                held = rest;

            float rtSafe = Mathf.Max(runTime, 0f);
            var m = new MotionRuntime
            {
                Target = target,
                _heldRot = held.Basis.Orthonormalized(),
                _heldScale = held.Basis.Scale,
                _heldOrigin = held.Origin,
                _runTime = rtSafe,
            };

            float RandSym() => (float)(rt._rng.NextDouble() * 2.0 - 1.0); // [-1, 1] via the seedable RNG
            float Rand(float a, float b) => a + (float)rt._rng.NextDouble() * (b - a);

            float gravity = data.Obj("gravity")?.Num("value") ?? 0f;

            // The plane's momentum (world-space), carried by the launched pieces so they scatter
            // along its travel instead of just popping up in place. Converted into the node's parent
            // frame, where the launch velocity lives (v0 drives Target.Transform, a local pose).
            Vector3 InheritedLocal()
            {
                if (rt.InheritedWorldVelocity == Vector3.Zero)
                    return Vector3.Zero;
                var parentBasis = (target.GetParent() as Node3D)?.GlobalTransform.Basis ?? Basis.Identity;
                return parentBasis.Inverse() * rt.InheritedWorldVelocity;
            }

            if (data.Obj("translation") is { } tr)
            {
                var v0 = tr.Vec3("initial");
                var rnd = tr.Vec3("rnd_xz");
                v0 += new Vector3(RandSym() * rnd.X, RandSym() * rnd.Y, RandSym() * rnd.Z);
                var delta = tr.Vec3("delta");
                // delta ramps velocity over run_time → a constant acceleration of delta/run_time.
                var rampAccel = rtSafe > 0f ? delta / rtSafe : Vector3.Zero;
                m._v0 = v0 + InheritedLocal();
                m._accel = rampAccel + new Vector3(0f, gravity, 0f);
                m._hasBallistic = true;
            }
            else if (data.Obj("translation_range") is { } range)
            {
                float horiz = Rand(range.Obj("xz")?.Num("min") ?? 0f, range.Obj("xz")?.Num("max") ?? 0f)
                              / Mathf.Max(rtSafe, 0.1f);
                float vVert = Rand(range.Obj("y")?.Num("min") ?? 0f, range.Obj("y")?.Num("max") ?? 0f)
                              / Mathf.Max(rtSafe, 0.1f)
                              - 0.5f * gravity * rtSafe; // gravity < 0 → the second term adds launch speed
                float azimuth = Rand(0f, Mathf.Tau);
                m._v0 = new Vector3(Mathf.Cos(azimuth) * horiz, vVert, Mathf.Sin(azimuth) * horiz)
                        + InheritedLocal();
                m._accel = new Vector3(0f, gravity, 0f);
                m._hasBallistic = true;
            }

            if (data.Obj("scale") is { } sc)
            {
                m._scaleInit = sc.Vec3("initial");
                m._scaleDelta = sc.Vec3("delta");
                m._hasScale = m._scaleInit.LengthSquared() > 1e-9f;
            }

            // forward_rotation.Time.initial is a TOTAL angle over run_time (the `Time`
            // parameterization), not a rate: the crash pieces carry 5π and 4.44π (clean multiples of
            // π), which read as a rate spin at ~15 rad/s (900°/s) — "spins like crazy" (user
            // playtest). ÷ run_time gives 5π over 6 s = 2.5 tumbles, the reference debris tumble.
            float fwdTotal = data.Obj("forward_rotation")?.Obj("Time")?.Num("initial") ?? 0f;
            m._tumbleRate = rtSafe > 0f ? fwdTotal / rtSafe : 0f;
            m._spinRate = data.Obj("xyz_rotation")?.Vec3("initial") ?? Vector3.Zero;

            // Nothing to drive → no motion (a bare gravity/bounce stub, handled by the caller).
            bool any = m._hasBallistic || m._hasScale || m._tumbleRate != 0f || !m._spinRate.IsZeroApprox();
            return any ? m : null;
        }

        public void Tick(float dt) => Seek(_t + dt);

        public void Seek(float t)
        {
            _t = t;
            float u = _runTime <= 0f ? 1f : Mathf.Clamp(t / _runTime, 0f, 1f);
            // Clamp the ballistic clock too so a finished body holds its last pose (it is removed
            // the frame it finishes, but Seek can be called past run_time by an instant land).
            float tb = _runTime > 0f ? Mathf.Min(t, _runTime) : t;

            var origin = _heldOrigin;
            if (_hasBallistic)
                origin = _heldOrigin + _v0 * tb + 0.5f * tb * tb * _accel;

            var basis = _heldRot;
            if (_tumbleRate != 0f)
                basis = basis.Rotated(basis.X.Normalized(), _tumbleRate * tb);
            if (!_spinRate.IsZeroApprox())
            {
                var a = _spinRate * tb;
                if (a.X != 0f) basis = basis.Rotated(basis.X.Normalized(), a.X);
                if (a.Y != 0f) basis = basis.Rotated(basis.Y.Normalized(), a.Y);
                if (a.Z != 0f) basis = basis.Rotated(basis.Z.Normalized(), a.Z);
            }

            var scale = _hasScale ? _scaleInit + _scaleDelta * u : _heldScale;

            Target.Transform = new Transform3D(basis.Scaled(scale), origin);
        }
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
        // The instant the CURRENT event's start offset is measured from: when the previous
        // event fired, plus that event's own run time. Control flow does not advance it.
        private float _base;
        // Did the current loop iteration schedule any time? Decides whether reaching the
        // LOOP starts the next iteration at once or yields to the next frame.
        private bool _iterScheduledTime;
        // One entry per open IF: has any branch of that chain already run? An ELSEIF/ELSE
        // reached with the flag set is the *fall-through* off the end of a taken branch and
        // must skip to the ENDIF; reached with it clear, it is the next candidate to test.
        private readonly List<bool> _branchTaken = new();

        public SequenceRunner(AnimSequence seq)
        {
            _seq = seq;
            // The first event's OWN offset gates it, so the opening gate is not
            // unconditionally zero — a sequence may legitimately start with a delay.
            SetDue();
        }

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
                    // The debugger timeline's "fired" mark: this event, in this sequence lane, at
                    // the playhead time the runner reached it. The null-conditional short-circuits
                    // the whole invocation — including the record construction — when no hook is
                    // attached, so this is zero cost in the game (it is on the hot dispatch path).
                    rt.OnEventDispatched?.Invoke(new EventDispatch(
                        inst.Def, inst.Anchor, _seq.Name, _pc, ev.Kind, EventDisplayName(ev)));
                    // This event has fired. The NEXT event's offset is measured from this
                    // moment plus this event's own run time — the offset belongs to the
                    // event that CARRIES it, not to its successor. See SetDue().
                    _base = _clock + duration;
                    if (duration > 0f)
                    {
                        _iterScheduledTime = true;
                    }
                    _pc++;
                    SetDue();
                    continue;
                }
                // Control flow.
                switch (ev.Kind)
                {
                    case "Loop":
                        if (_loopsLeft == -2)
                        {
                            int authored = (int)(ev.Data.Num("value") ?? CountOf(ev) ?? -1f);
                            // An AUTHORED count of 0 means INFINITE, not "stop immediately".
                            // Surveyed across the whole install: 26 Loop events
                            // in 25 defs ship Count 0, and every one of them is a ground-vehicle
                            // route (C1's police/mafia/black_car/truck traffic, C2's and C3/M02's
                            // studebakers) whose Loop is the LAST event of its sequence — the
                            // original drives these continuously. Nothing that must terminate
                            // uses it: no door, gate, one-shot, bomb or explosion def, and the
                            // reader/zrdr scope has 703 Loop events with zero Count 0. Reading it
                            // as "stop" made each car drive its route once and freeze.
                            // Normalise here rather than at the test below, so the test keeps
                            // meaning "a finite loop has run out" — that is the only way a
                            // positive count can ever terminate.
                            _loopsLeft = authored == 0 ? -1 : authored;
                        }
                        if (_loopsLeft == 0)
                        {
                            _done = true;
                            break;
                        }
                        if (_loopsLeft > 0)
                            _loopsLeft--;
                        // A loop over purely instantaneous events is the data's "keep this
                        // animation alive" idiom (C1's waterfall is [PufferState ×3, Loop{-1}],
                        // whose emitters run on their own TIME_INTERVAL; the poll idiom
                        // `If … CallAnimation; Endif; Loop{-1}` is the other shape). Left
                        // unchecked it spins as fast as the per-frame guard allows; yield to
                        // the next frame instead, so such a loop polls exactly once a frame.
                        // Loops whose body takes time are unaffected — they are already
                        // waiting on _due, and must start their next iteration immediately.
                        //
                        // The test is "did this iteration schedule any time?", NOT "is the
                        // clock zero": the clock is reset to 0 here, so on the NEXT frame it
                        // reads dt at this point and an instantaneous body ran a second time
                        // before yielding — every poll loop in the chapter costing double.
                        bool instantIteration = !_iterScheduledTime;
                        _pc = 0;
                        _clock = 0f;
                        _base = 0f;
                        _iterScheduledTime = false;
                        _branchTaken.Clear(); // a new iteration re-tests every condition
                        SetDue();
                        if (instantIteration)
                        {
                            return;
                        }
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
                // Control flow moved _pc without firing anything, so re-gate on whatever
                // event we landed on. Its own offset applies (LOOP included — the bowl
                // sign's trailing `Loop {Event 1.2}` is its inter-cycle pause, and that
                // offset used to be discarded).
                SetDue();
            }
            if (_pc >= _seq.Events.Count)
            {
                _done = true;
            }
        }

        private static float? CountOf(AnimEvent ev) => ev.Data.Num("Count");

        /// <summary>
        /// Gate the event at <see cref="_pc"/> on ITS OWN schedule.
        ///
        /// An event's START_TIME says when *that* event fires — see
        /// <see cref="AnimEvent.StartOffset"/>: "Event" = since the previous event fired,
        /// null = immediately after it. This used to be computed from the event just
        /// FIRED and applied to its successor, which shifted **every sequence in the
        /// install** by one slot: a timestamped event fired one slot early and its
        /// unstamped partner one slot late.
        ///
        /// C1's `bowl` sign is the clean demonstration. Its compiled
        /// sequence is nine strict `des_on`/`des_off` SWAP pairs plus an infinite Loop,
        /// and only the FIRST of each pair carries a timestamp — so the shift split every
        /// pair, leaving both variants lit at t=0 and then **nothing at all** for each
        /// gap. Measured face-on at the sign: 38.0% of frames completely blank before,
        /// 0% after. The user's report was "the bowl sign flashes in the original, but
        /// ours disables and re-enables it instead".
        ///
        /// Control-flow events (LOOP/IF/ELSEIF/…) do not advance <see cref="_base"/> —
        /// they take no time — but they ARE gated, which is what gives the sign's trailing
        /// `Loop {Event 1.2}` its inter-cycle pause. That offset was previously discarded
        /// outright, since the Loop branch hard-reset the gate to zero.
        /// </summary>
        private void SetDue()
        {
            if (_pc >= _seq.Events.Count)
            {
                _due = _base;
                return;
            }
            var ev = _seq.Events[_pc];
            _due = ev.StartOffset switch
            {
                // "Animation"/"Sequence" are absolute against their origin; this runner's
                // clock is the sequence clock, and an instance starts all its sequences
                // together, so the two coincide for every case in the shipped data.
                "Animation" or "Sequence" => ev.StartTime,
                _ => _base + ev.StartTime,
            };
            if (_due > _clock)
            {
                _iterScheduledTime = true;
            }
        }

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
            // drops, skipped subtrees). Not an error, and not something to name-match around
            // in the shared world (C1's `caboose` resolves to the consist AND a `caboose.flt`).
            // The scoped crash runtime is the exception (see NameResolveFallback): its index has
            // one node per name and the crash def's ptr is non-portable, so it falls through to
            // the unique name below.
            if (!NameResolveFallback)
            {
                _opsUnresolved++;
                return new List<Node3D>();
            }
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

    /// <summary>Every world node matching a NAME pattern, optionally restricted to one
    /// subtree. A full scan of the node index — and the data calls it constantly, because the
    /// poll idiom (<c>If … CallAnimation; Endif; Loop{-1}</c>) re-dispatches its body every
    /// frame, so C5's ~400 live poll loops asked for hundreds of resolutions per frame.
    ///
    /// The answer is memoized because it cannot change: <see cref="_index"/> is built once
    /// during the bootstrap and never added to, and the only runtime mutation of the world
    /// tree is <see cref="SetSubtreeActive"/>, which toggles visibility and colliders without
    /// reparenting or freeing anything. Callers must treat the returned list as read-only.
    /// </summary>
    private List<Node3D> FindAll(string pattern, Node3D? scope)
    {
        // Godot object identity is by native pointer, so key on the instance id rather than
        // relying on GodotObject equality semantics inside a tuple comparer.
        var key = (pattern, scope?.GetInstanceId() ?? 0UL);
        if (_findCache.TryGetValue(key, out var hit))
            return hit;
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
        _findCache[key] = result;
        return result;
    }

    private readonly Dictionary<(string Pattern, ulong Scope), List<Node3D>> _findCache = new();

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

    // Last opacity pushed to each subtree root. These events sit in `Loop{-1}` sequences —
    // C1's `cloudparent#` re-asserts its 0.6 every frame — so without this the whole subtree
    // would be re-walked and re-written ~31 times a frame to set values it already holds. Same
    // lesson as LightState's per-light host cache, which cost ~7 ms/frame before it existed.
    private readonly Dictionary<Node3D, float> _opacity = new();

    // OBJECT_OPACITY_STATE applies to the whole subtree, as a per-instance shader parameter
    // rather than a material edit: SceneBuilder's materials are cached and shared, so writing
    // alpha into one would fade every other node that happens to use it. Meshes whose shader
    // has no alpha path (opaque variants, where SceneBuilder deliberately omits the uniform)
    // silently ignore the parameter, which is correct — every opaque target the data touches
    // asks for 1.0 — but a genuine partial opacity landing on one is counted, not swallowed.
    private void SetSubtreeOpacity(Node3D node, float alpha)
    {
        if (_opacity.TryGetValue(node, out float prev) && Mathf.IsEqualApprox(prev, alpha))
            return;
        _opacity[node] = alpha;
        int applied = ApplyOpacity(node, alpha);
        if (applied == 0 && !Mathf.IsEqualApprox(alpha, 1f))
            Count("ObjectOpacityState(no alpha path)");
    }

    private static int ApplyOpacity(Node node, float alpha)
    {
        int n = 0;
        if (node is GeometryInstance3D g)
        {
            g.SetInstanceShaderParameter(SceneBuilder.OpacityParam, alpha);
            if (HasOpacityPath(g))
                n++;
        }
        foreach (var child in node.GetChildren())
            n += ApplyOpacity(child, alpha);
        return n;
    }

    // Whether this mesh's shader actually reads the opacity parameter. Setting an instance
    // parameter a shader does not declare is silently a no-op in Godot, so without this check
    // the "no alpha path" tally could never fire and would be a lie rather than a diagnostic.
    //
    // ⚠ Tests for the USE (`SceneBuilder.OpacityTerm`, i.e. " * csky_opacity"), not the uniform
    // NAME. Those used to be equivalent — the uniform was declared exactly in the
    // variants that multiplied by it — but the declaration has since moved into the shared
    // ordered preamble (csky_instance_uniforms.gdshaderinc), so it is now present in shaders
    // with no alpha path at all. Testing the name would report true for every one of them.
    // Testing the include line would be worse still: the declaration is no longer textually in
    // `sh.Code`, so a name test would report FALSE everywhere and quietly invert this tally.
    private static bool HasOpacityPath(GeometryInstance3D g)
    {
        if (g is not MeshInstance3D mi || mi.Mesh is not { } mesh)
            return false;
        for (int i = 0; i < mesh.GetSurfaceCount(); i++)
            if (mesh.SurfaceGetMaterial(i) is ShaderMaterial { Shader: { } sh }
                && sh.Code.Contains(SceneBuilder.OpacityTerm, StringComparison.Ordinal))
                return true;
        return false;
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
