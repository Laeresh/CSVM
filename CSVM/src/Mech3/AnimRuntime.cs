using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3.Anim;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The animation engine: binds an <see cref="AnimProgram"/> to a built world and executes it.
/// Bootstrap runs its passes (mission setup, anchored RESET_STATEs, ON_STARTUP, the mission's
/// startanims, a safety net hiding still-visible `destroyed` subtrees), then events play through
/// the dispatch table. An event kind with no case is counted and reported, never fatal.
/// Passes 2 and 3 run rather than posing at the end state, so a hangar door swings open over its
/// authored run time. Module notes: docs/architecture.md.
/// ⚠ "Inactive" means hidden AND non-collidable. An invisible solid zeppelin is worse than a
/// visible one that should not be there.
/// ⚠ Resolve a definition to a node by its original gamez name (the <c>cs_name</c> meta), never by
/// <c>Node.Name</c>; Godot mangles duplicate sibling names.
/// </summary>
public sealed partial class AnimRuntime : Node, ISequenceHost
{

    /// <summary>Original-name metadata key SceneBuilder stamps on every built Node3D.</summary>
    public const string NameMeta = "cs_name";

    /// <summary>Flat gamez node-index metadata key SceneBuilder stamps on every built
    /// Node3D — the exact binding compiled definitions reference (see
    /// <see cref="AnimDefinition.NodeRefs"/>).</summary>
    public const string IndexMeta = "cs_index";

    /// <summary>Pool-slot metadata key the effect-template pool stamps on each slot container
    /// (<c>WorldEffectsFactory.BuildWorldEffectsRuntime</c>): every staged template copy lives
    /// under exactly one of them, and the slot is what keeps one call's copy of a template apart
    /// from another call's (see <see cref="TemplateStage{TNode}.Pooled"/>). Never on a template node itself,
    /// so name resolution is blind to it.</summary>
    public const string PoolSlotMeta = "cs_pool_slot";

    /// <summary>The reader's <c>ANIMATION_LOD HIGH</c> — see <see cref="QualityLod"/>.</summary>
    public const int HighLod = 2;

    /// <summary>Event kinds <c>Dispatch</c> acts on. Keep in step with its cases: a kind absent
    /// here is one the runtime counts as unhandled and does nothing for, which is what the node
    /// lab's coverage column reports.</summary>
    public static readonly IReadOnlyCollection<string> HandledEventKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        "ObjectActiveState", "ObjectTranslateState", "ObjectRotateState", "ObjectScaleState",
        "ObjectMotionFromTo", "ObjectOpacityState", "ObjectOpacityFromTo", "ObjectMotion",
        "ObjectMotionSiScript", "Loop", "If", "Elseif", "Else", "Endif", "CallSequence",
        "StopSequence", "CallAnimation", "StopAnimation", "InvalidateAnimation", "ResetAnimation",
        "PufferState",
        "LightState", "LightAnimation", "SoundNode", "Sound", "ObjectAddChild",
    };

    /// <summary>Kinds with a handler that covers only part of what the event does — reported
    /// apart from the unhandled ones, since "acted on" and "acted on fully" are different answers.
    /// <c>ObjectAddChild</c> handles its sound-emitter form and counts the rest as unhandled.</summary>
    public static readonly IReadOnlyCollection<string> PartialEventKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        "ObjectAddChild",
    };

    /// <summary>Collect a per-definition census of how the bind RESOLVED, and log it through
    /// <see cref="ResolutionLines"/>. Set before <see cref="Bind"/> by a caller that built only
    /// part of the world (the <c>--node=</c> stage), where "this def did nothing" is the normal
    /// case and needs to be told apart from a defect. Default false: a full-world session collects
    /// nothing, so this is inert when nobody asks for it. Copied into the resolver — which owns
    /// the census — when <see cref="Bind"/> runs, like the two flags below.</summary>
    public bool ReportResolution;

    /// <summary>Refuse the <c>ANIMATION_ROOT_NAME</c> anchor lift, however few matches it finds.
    /// ⚠ Set this only from a caller that built part of the world. The resolver's
    /// <c>MaxRootLift</c> cap assumes a whole-world node population, so a single-subtree stage
    /// drops under it and unrelated definitions anchor onto whatever generic child the subtree
    /// owns. Suppressed lifts are counted and reported, never silently dropped.</summary>
    public bool SuppressRootLift;

    /// <summary>--debug-anim: log every live motion's target and pose once a second, so a
    /// headless run can verify that (say) the train actually drives its loop.</summary>
    public bool DebugMotions;

    /// <summary>Our answer to the data's <c>ANIMATION_LOD</c> condition, a project quality setting
    /// rather than a fact about the world. Every LOD-gated branch in this install asks for
    /// <c>HIGH</c>, which compiles to 2, so the default passes them all. <c>--anim-lod=N</c> lowers
    /// it for A/B comparison.</summary>
    public int QualityLod = HighLod;

    /// <summary>Where the player is, for a <c>PLAYER_RANGE</c> condition with no
    /// <see cref="PlayerPositions"/> wired (a lab, a unit test). Supplied by the session (the
    /// flown aircraft, or the spectator camera); absent → the viewport camera, and failing
    /// that the world origin. During the bootstrap passes there is no camera yet, which is
    /// harmless: every PLAYER_RANGE definition in this install re-polls from a <c>Loop{-1}</c>,
    /// so a bootstrap-time miss corrects on the next frame.</summary>
    public Func<Vector3>? PlayerPosition;

    /// <summary>Every player's position, for the EXECUTION_BY_RANGE proximity gate and (
    /// `BL-365`) every <c>PLAYER_RANGE</c> condition — both measure from the nearest human, not
    /// one camera. In flight this is the aircraft themselves — the chase camera trails far
    /// enough behind the plane to eat most of a 50 m radius. Null → both fall back to
    /// <see cref="PlayerPosition"/> alone, keeping a runtime built without this seam (a lab, a
    /// test) on the pre-C21 single-camera behaviour.</summary>
    public Func<IReadOnlyList<Vector3>>? PlayerPositions;

    /// <summary>Every pane's camera position, for budgeting <see cref="Lights"/> (`BL-366`):
    /// a world light must not fade or lose its slot just because player 1 is far from it. This is
    /// the draw-rule seam (<c>ViewerSet.Positions</c>), not <see cref="PlayerPositions"/> (the
    /// gameplay one) — null or empty falls back to <see cref="PlayerPos"/> alone, keeping a
    /// runtime built without a viewer seam (a lab, a test) on today's single-camera behaviour.</summary>
    public Func<IReadOnlyList<Vector3>>? LightViewerPositions;

    /// <summary>Answers the data's <c>PLAYER_1ST_PERSON</c> condition. No cockpit view
    /// exists yet, so false.</summary>
    public bool FirstPerson;

    /// <summary>Whether <see cref="Bootstrap"/> runs the ambient-playback passes (ON_STARTUP defs
    /// and the mission's startanims). True in every game/viewer/flight session. False gives the
    /// animation debugger a quiet stage: base states and mission setup still apply, but nothing
    /// animates until <see cref="StartAmbient"/>. Set before <see cref="Bind"/>.</summary>
    public bool AutoStart = true;

    /// <summary>The mission's interp boot script (<c>support\&lt;chapter&gt;\&lt;mission&gt;.gw</c>),
    /// run as bootstrap pass 0. It is what decides which world entities this mission shows —
    /// see <see cref="MissionSetup"/>. Null when the mission ships no script, which is normal.
    /// Set before <see cref="Bind"/>.</summary>
    public MissionSetup? Setup;

    // ---- observability (the animation debugger's timeline; null = zero cost in the game) ----
    /// <summary>Raised as each sequence event fires at runtime. Null by default → zero cost in the
    /// game; the debugger sets it to feed its timeline's fired marks straight from the runtime,
    /// rather than parsing --debug-anim log text. The interpreter reads it through the get-only
    /// <see cref="ISequenceHost.OnEventDispatched"/> seam member.</summary>
    public Action<EventDispatch>? OnEventDispatched;

    /// <summary>Raised when a definition becomes a live instance and when that instance finishes,
    /// each carrying the (def, anchor) identity. Null by default → zero cost in the game; the
    /// debugger uses the pair to place a CALL_ANIMATION child def's timeline lane group at the
    /// playhead time it began, and to drop it when it ends.</summary>
    public Action<AnimDefinition, Node3D?>? OnInstanceStarted;

    public Action<AnimDefinition, Node3D?>? OnInstanceFinished;

    /// <summary>The collision mask a <c>do_intersections</c> body sweeps against; 0, the default,
    /// means this session does no ground contact (the labs, the goldens, the headless suites).
    /// ⚠ Set it to <c>CollisionLayers.World</c> only; debris must not be solid to aircraft. Handed
    /// in rather than read from <c>CollisionLayers</c> because that constant lives in the flight
    /// layer and the session is where the two meet.</summary>
    public uint ContactMask;

    /// <summary>Whether a struck collider is water, the only distinction a <c>BOUNCE_SEQUENCE</c>
    /// draws (docs/org/objectMotion.md). Null, the default, answers "not water" and every
    /// contact takes the <c>default</c> branch.
    /// ⚠ Bind this to <c>ProjectilePool.SurfaceIsWater</c>, never to a second surface read; a
    /// round, a wingtip graze and a landing piece must not disagree about what they hit.</summary>
    public Func<GodotObject?, bool>? SurfaceIsWater;

    // ---- live execution ----
    /// <summary>True when something else owns the clock (the animation debugger, which feeds
    /// <see cref="Advance"/> in fixed 1/60 s steps): <see cref="_Process"/> stops advancing.
    /// ⚠ Use this flag, not <c>SetProcess(false)</c>. Godot re-enables processing at READY for any
    /// node overriding <c>_Process</c>, and this node enters the tree after the lab is assembled,
    /// so an earlier SetProcess call is silently undone and the world runs at double speed.</summary>
    public bool ManualAdvance;

    /// <summary>Named CALL_ANIMATION callees whose placed root levels to world axes instead of the
    /// inherited parent rotation (<see cref="TemplateStage{TNode}.PlaceOn"/>), keyed by
    /// <c>AnimName ?? Name</c>. Set by <see cref="FlightController.Crash"/> for the crash def's
    /// surface-hugging sub-effects only, and cleared by <c>Respawn</c>. ⚠ Never make it blanket:
    /// leveling `fly_trail1-5` strips the co-rotation their debris scatter is authored in. The one
    /// list is <c>EffectCatalogue.CrashSurfaceLevelAnimNames</c>; see docs/architecture.md.</summary>
    public HashSet<string>? LevelPlacedTemplateNames;

    /// <summary>Key puffer emitters by owning def as well as (name, host) — see
    /// <see cref="EmitterDirector"/>'s keying remark, which carries the measurement behind each
    /// case. Set on the world-effects runtime, where distinct effect defs declaring same-named
    /// puffers are distinct emitters (the damage-stage sputters); off on the world runtime, where
    /// the collapsed key de-dups same-name multi-def ambient stacks. Read once, when
    /// <see cref="Emitters"/> is first built.</summary>
    public bool DefScopedPufferKeys;

    /// <summary>Resolve every node reference by NAME, leaving the resolver's by-index map empty
    /// (<see cref="IndexWorld"/>). ⚠ Never turn this on for the shared world: name matching
    /// resolves C1's <c>caboose</c> to the real consist and to an unrelated <c>caboose.flt</c>.
    /// The per-player crash runtime must have it on, because its def's node ptrs index a planes.zbd
    /// this build never loads and its subtree mixes two colliding gamez index spaces.</summary>
    public bool NameResolveFallback;

    /// <summary>Hands a named effect to the world-effects runtime instead of starting it locally,
    /// passing the call-site world point and the resolved call-site node (the callee's INPUT_NODE).
    /// Returns true when it took the effect, so the local Start is skipped. Set on the WORLD
    /// runtime, whose puffer factory is gone after the build and which would render nothing; null
    /// everywhere else, where CALL_ANIMATION starts the callee locally.</summary>
    public Func<string, Vector3, Node3D?, bool>? ExternalEffect;

    /// <summary>Stops a named effect on the external runtime <see cref="ExternalEffect"/> routes to
    /// — the reverse channel, for undoing a routed effect the data has no stop event for:
    /// <see cref="ResetDestructible"/> heals an object whose damage-stage sputter loops for as long
    /// as its host stays active, so the reset itself must end it.</summary>
    public Action<string>? ExternalEffectStop;

    /// <summary>Lazily builds, indexes and RESET_STATE-poses a pooled copy of a named library root
    /// (<see cref="GameZ.IsLibraryRoot"/>, docs/formats/gamez.md), returning the copy this exact
    /// caller owns; a different caller gets a fresh one until the pool wraps. Null when the name is
    /// no library root, and on every runtime that stages its templates eagerly instead.
    /// ⚠ It returns the node rather than a permission bool on purpose. Drive that one copy, never
    /// the def's name-wide <see cref="TemplateStage{TNode}.RootsFor"/> set.</summary>
    public Func<string, Node3D, Node3D?>? ResolveLibraryRoot;

    /// <summary>Washes the picture from one RGBA to another, driven by <c>FBFX_COLOR_FROM_TO</c>.
    /// The arguments are <c>(from, to, run_time, origin, radius²)</c>; the radius is metres SQUARED,
    /// the compiled <c>PLAYER_RANGE</c> convention, and 0 for a def that gates on nothing.
    /// ⚠ Keep this a sink, never an overlay node owned here. The overlay is screen-space and
    /// session-scoped, and this runtime is instanced per effect pool and per player crash rig.
    /// Which panes the wash reaches is the sink's decision, not this runtime's.</summary>
    public Action<Color, Color, float, Vector3, float>? ScreenFlash;

    /// <summary>This runtime does not own audio — its SOUND / SOUND_NODE events are no-ops, not
    /// late-failure reports. Set on the world-effects runtime: it renders an effect def's
    /// puffers, but the same effect's impact/death SOUND is already played by the projectile pool
    /// or the world runtime, so playing it here too would double it, and with no audio
    /// session it would only spam "silent for the session" warnings.</summary>
    public bool SoundHandledElsewhere;

    /// <summary>How long a <see cref="PlayEffectAt"/> effect instance may run before this runtime
    /// stops it (seconds; 0 = never, the default). The world-effects runtime sets it so a stop-less
    /// sustained effect — <c>large_30sec_fire</c>'s <c>fire_n_smoke</c>, which has no ACTIVE_STATE 0
    /// and would otherwise emit for the rest of the session — is bounded. Only effects this runtime
    /// itself started via PlayEffectAt are tracked; ambient/crash runtimes leave it 0 and are
    /// untouched.</summary>
    public float EffectTtl;

    /// <summary>A world-space velocity added to every ballistic <see cref="MotionRuntime"/> launch,
    /// transformed into the launched node's parent frame. Zero by default. The crash sets it to a
    /// fraction of the plane's impact velocity so wreck pieces carry its momentum; the fraction is
    /// a TUNE, not a decode.
    /// ⚠ Leave it zero on the world runtime. The original's world debris shows no directional bias
    /// with the attack heading, so a shot building's pieces must inherit nothing.</summary>
    public Vector3 InheritedWorldVelocity;

    /// <summary>Animation names <see cref="InheritedWorldVelocity"/> must not reach. The data shape
    /// alone cannot tell a launched piece from a ground-planted effect, so the caller names the
    /// exceptions. Null, the default, means every ballistic motion inherits. The crash rig sets it
    /// so the nudge that scatters wreck pieces does not drag the crash splash off with them.</summary>
    public HashSet<string>? InheritedVelocityExempt;

    // ---- PUFFER_STATE ----
    /// <summary>What <see cref="Emitters"/> builds through; null, the default, renders no emitters
    /// at all. ⚠ Valid only during the world build, because an emitter bakes its atlas from the
    /// session's <see cref="TextureArchive"/>. A caller whose archive dies with its build must call
    /// <see cref="EmitterDirector.RetireFactory"/> afterwards, so a later request is reported
    /// instead of faulting on a closed zip handle.</summary>
    public IEmitterFactory? EmitterFactory;

    // ---- SOUND_NODE (+ the sound half of OBJECT_ADD_CHILD) ----
    /// <summary>The world's ambient 3D emitters. Null in a muted or soundless session, in which
    /// case SOUND_NODE is tracked and reported but nothing is built.</summary>
    public WorldSounds? Sounds;

    /// <summary>Where the world's lights are delivered. Null outside a lit session, in which
    /// case LIGHT_STATE is tracked but never rendered.</summary>
    public WorldLights? Lights;

    // The runtime's dice: RANDOM_WEIGHT verdicts, SOUND_GROUPS one-shot picks, crash-debris
    // scatter. One field rather than scattered GD.Randf() calls so the session's master seed can
    // pin the whole sequence. Any future WeaponHit/crash handler's randomness must route through
    // this same _rng, or a replay stops being identical the day the handler lands.
    internal Random _rng = new();

    private const int MaxStartDepth = 8;

    // Backstop on a single WAIT_FOR_COMPLETION hold, in seconds, well clear of the longest hold the
    // data authors. "Completes" is our instance lifetime, not the authored one, so a callee held
    // open by something the data cannot predict would wedge the caller's sequence silently. Every
    // trip is counted and named (ReportUnhandled).
    private const float WaitCeilingS = 120f;

    // The magic sequence name a destructible's progressive-damage script carries in both the reader
    // and compiled forms (docs/formats/destructibles.md).
    private const string DamageSequenceName = "DAMAGE_SEQUENCE";

    // Below this alpha a faded subtree drops its colliders, mirroring the deactivation path's
    // "invisible implies non-collidable" rule, and regains them when it fades back above.
    // ⚠ Keep the write edge-triggered on the last collidable state per subtree root; a fade
    // re-writes opacity every tick and re-walking the subtree each frame would thrash.
    // Independent of SetSubtreeActive's collider toggle: separate channels, most recent event wins.
    private const float OpacityCollisionEpsilon = 0.01f;

    // The deferred-EXECUTION_BY_RANGE sweep quantises the player position to this cell size and
    // re-checks the deferred list only on a cell crossing (the MapEdgeExtender cadence).
    // ⚠ Keep it well under the smallest authored radius in the install, 50 m: the sweep lags an
    // approach by up to one cell diagonal, and the C3 spiderweb's 0.7 s fade needs the trigger to
    // land close to its authored range at cruise speed.
    private const float RangeCheckCellSize = 8f;

    // Smallest magnitude a pose-scale component may reach. ⚠ Never let a pose scale reach exactly
    // 0, however the data authors it: a singular basis makes Godot's physics server spam
    // `det == 0` for any StaticBody3D under the node. 1e-3 of a world object is sub-pixel at
    // gameplay distance, and the defs that shrink away hide the node anyway.
    private const float MinPoseScale = 1e-3f;

    // Name resolution — the index, wildcard matcher, memoized FindAll, and the three-tier scope
    // chain — lives in NameResolver.cs; identity is instance id, since Godot object equality is
    // unreliable inside a dictionary/tuple key across proxy instances of the same native node.
    // Constructed in the constructor below: the pool reaches the resolver ONLY as the ownRootsOf
    // hook (TemplateStage.RootsFor) — the slot arithmetic lives on the stage.
    private readonly NameResolver<Node3D> _resolver;

    // The effect-template pool + placement as a module: slot
    // arithmetic, the caller-slot claim, template placement, the copy-identity questions
    // and the reveal/retire/sweep ritual live in TemplateStage.cs; this class supplies the engine
    // and runtime hooks in the constructor and calls through. The flags are sealed at construction.
    private readonly TemplateStage<Node3D> _templateStage;

    private readonly Dictionary<Node3D, Transform3D> _rest = new(); // authored pose per touched node

    private readonly List<AnimInstance> _instances = new();

    // The destructible whose death is directly dispatching right now (a stack, since deaths nest).
    // Lets a death-triggered CALL_ANIMATION landing on the SAME anchor as the dying instance
    // register on its LocalCallTargets, so a reset can Stop and restore it too. Without that, a
    // reset arriving before the called def's own motions finish leaves its pieces flown.
    private readonly Stack<DestructibleRegistry.Instance> _dyingInstances = new();

    // The instance whose own t=0 burst is directly dispatching, one entry per nested CALL_ANIMATION
    // level, paired with whether this Start opted into self-invalidate protection. It stops a
    // same-name self STOP/INVALIDATE_ANIMATION authored early in that burst from tearing its own
    // still-unwinding instance down and orphaning the calls the burst just scheduled.
    // ⚠ Keep the guard opt-in and scoped to the top-of-stack instance. The ambient world boot needs
    // the tolerant behaviour for its startup anims (docs/formats/anim-definitions.md).
    private readonly Stack<(AnimInstance Inst, bool Protect)> _startingInstances = new();

    // Definitions an INVALIDATE_ANIMATION has latched off. ⚠ Treat the event as a one-shot latch,
    // never a stop, and clear it only from RESET_ANIMATION; the original's animation state byte
    // works that way (docs/formats/anim-definitions.md). Keyed by DEFINITION, not (def, anchor),
    // because the event resolves one animation record and never walks the per-node copies.
    private readonly HashSet<AnimDefinition> _invalidated = new();

    private readonly Dictionary<string, int> _unhandled = new(StringComparer.Ordinal);

    // WAIT_FOR_COMPLETION: callee name -> how many holds it took. NAMES, not just a total: a hold's
    // whole effect is to keep a caller's sequence (and therefore its instance) alive longer, so it
    // surfaces as a live-instance count that moved, and "which one" is the only question worth
    // asking about that. Bounded by the install's 38 distinct flagged callees.
    private readonly Dictionary<string, int> _waitsByCallee = new(StringComparer.OrdinalIgnoreCase);

    // Callees whose routed-away hold has already been named in the log — the print is once per
    // name, not once per call, because a gun's impact effect routes ten times a second.
    private readonly HashSet<string> _routedWaitsNamed = new(StringComparer.OrdinalIgnoreCase);

    // Same, for a flagged call that reached no live callee instance and so held nothing.
    private readonly HashSet<string> _inertWaitsNamed = new(StringComparer.OrdinalIgnoreCase);

    private readonly DestructibleRegistry _destructibles = new();

    // The call-site node a PlayEffectAt instance was invoked WITH — the callee's INPUT_NODE. The
    // local CALL_ANIMATION path expresses this by anchoring the callee on the site node; the
    // external path anchors on the staged template root instead, so the sentinel's referent is
    // carried here, keyed by the instance identity. Cleared with the instance.
    private readonly Dictionary<(AnimDefinition Def, Node3D? Anchor), Node3D> _inputNodes = new();

    // Which defs condition on their own INPUT_NODE's active state (a NodeActive sentinel in any
    // sequence) — the damage-stage sputters. Their lifetime is authored (loop while the host node
    // is active), so PlayEffectAt gives them the real site node and no TTL.
    private readonly Dictionary<AnimDefinition, bool> _inputGoverned = new();

    // Each def's own FBFX wash gate (WashGateRadiusSquared), scanned once. Four defs per chapter
    // carry a wash and a burst can fire several a second, so the scan is cached rather than repeated
    // per event.
    private readonly Dictionary<AnimDefinition, float> _washGates = new();

    // Keyed by (sound name, anchor), like the lights and for the same reason: the anchor
    // identifies the *instance* of the definition, so C1's four firetrucks each get their own
    // siren rather than sharing one. It cannot be keyed by host node the way puffers are — in the
    // reader's triple the emitter is declared BEFORE anything says where it goes.
    private readonly Dictionary<(string Name, Node3D? Anchor), object> _soundEmitters = new();

    private readonly HashSet<string> _soundFailuresReported = new(StringComparer.OrdinalIgnoreCase);

    // Keyed by (light name, anchor). The anchor identifies the *instance* of the definition,
    // and a definition's `lights` array is its own symbol table — so two refineries each get
    // their own orange_light. It cannot be keyed by host node the way puffers are: the flicker
    // events are partial updates carrying only {name, range}, with no AT_NODE to resolve from.
    private readonly Dictionary<(string Name, Node3D? Anchor), AnimLight> _lights = new();

    private readonly List<(AnimDefinition Def, Node3D? Anchor, float Deadline)> _effectTtls = new();

    // ---- IF/ELSEIF conditions ----
    // Per condition kind: how often it evaluated true / false. Reported after the bootstrap
    // passes, which is the headless proof that (say) the refinery's AnimationLod branch is
    // now TAKEN rather than skipped.
    private readonly Dictionary<string, (int True, int False)> _conditions = new(StringComparer.Ordinal);

    private readonly Dictionary<(string Kind, Node3D? Anchor), bool> _condLast = new();

    private readonly HashSet<string> _retargetsLogged = new(StringComparer.Ordinal);

    // Last opacity pushed to each subtree root. These events sit in `Loop{-1}` sequences —
    // C1's `cloudparent#` re-asserts its 0.6 every frame — so without this the whole subtree
    // would be re-walked and re-written ~31 times a frame to set values it already holds. Same
    // lesson as LightState's per-light host cache, which cost ~7 ms/frame before it existed.
    private readonly Dictionary<Node3D, float> _opacity = new();

    // The fade twins a genuine partial opacity installs per instance (see EnsureOpacityPath):
    // source material -> its translucent twin (null = cannot be made translucent), the twin
    // set for recognising an override this runtime installed, and the shader-level cache so
    // materials sharing one generated shader share one twin shader.
    private readonly Dictionary<ShaderMaterial, ShaderMaterial?> _fadeTwinCache = new();

    private readonly HashSet<Material> _fadeTwins = new();

    // Nodes that have just landed by contact, whose NEXT ballistic launch must start from where
    // they came to rest rather than the authored rest pose (MotionRuntime.Create's re-home rule).
    // A bounce is a continuation, and re-basing it teleports the piece back to the crash point.
    // ⚠ Keep this one-shot per node and consumed by the launch that follows. The re-home rule
    // itself is what stops pooled effect templates drifting across repeat explosions.
    private readonly HashSet<Node3D> _resumeFromLanding = new();

    private readonly Dictionary<Shader, Shader?> _fadeShaderCache = new();

    // ON_STARTUP defs carrying EXECUTION_BY_RANGE wait here instead of starting at bootstrap:
    // each (def, anchor) starts once, the first time the player is inside its distance band.
    // Checked on a cell-crossing cadence (see TickDeferredByRange), never per frame.
    private readonly List<(AnimDefinition Def, Node3D Anchor)> _rangeDeferred = new();

    private readonly List<Vector3I> _rangeCheckCells = new();

    private Node3D _root = null!;

    private AnimProgram _program = null!;

    private int _opsApplied, _opsUnresolved;

    // Whether the ambient passes have already run — set when Bootstrap runs them inline
    // (AutoStart=true) or when StartAmbient runs them on demand, so StartAmbient is idempotent
    // and a normal bootstrap's ambient toggle is a no-op rather than a second bootstrap.
    private bool _ambientStarted;

    private int? _seed;

    private int _startDepth;

    // Nonzero while a death's own Start burst (RunDeathSequence) is on the call stack, a counter
    // because a chained CALL_ANIMATION can call again. It lets a death-triggered call relocate its
    // callee's effect-template root onto the struck node, the way TemplateStage.Places does.
    // ⚠ Never widen this to the ambient world boot or RESET_STATE; those must leave a shared
    // template at its gamez origin, and the goldens are byte-identical on that.
    private int _deathCallDepth;

    private int _soundsUnknown, _soundsAfterBuild;

    // Set once Bootstrap has printed its emitter census. After it, a failed SOUND_NODE is invisible
    // unless reported at the point of use, because the census is a bootstrap snapshot and cannot
    // tell "never requested" from "requested later and failed". Report once per name, not per
    // event; a single sound name has hundreds of sites.
    private bool _soundCensusPrinted;

    private EmitterDirector? _emitters;

    private float _effectClock;

    private int _damagesLogged;

    // CALL_ANIMATION retargeting tallies. Deliberately NOT routed through Count(), which is
    // the "event kinds not yet acted on" channel — a retargeted call is acted on, and filing
    // it there would report a working feature as a missing one.
    private int _retargeted, _retargetUnresolved;

    private float _debugClock;

    // WAIT_FOR_COMPLETION. The completion test the CallAnimation case just installed,
    // handed to the sequence runner through ISequenceHost.PendingWait and read exactly once —
    // Dispatch clears it on entry, so it can never leak onto a later event. A closure over the
    // (target, anchor) pairs THIS call reached, because that is the only thing that identifies
    // them: re-asking by name would resolve a pooled template copy a second time and take
    // another slot. See the CallAnimation case and analysis/wait-for-completion/FINDINGS.md.
    private Func<bool>? _pendingWait;

    // Sim seconds this runtime has advanced — the clock a wait's ceiling is measured against.
    // Deliberately the runtime's own accumulation of Advance's dt, not a wall clock and not
    // GameClock: a wait is authored animation time and must not scale with the client.
    private float _elapsed;

    // How many waits were installed, and how many hit WaitCeilingS instead of their callee
    // finishing. The second number is the one that matters — it must be 0.
    private int _waitsInstalled, _waitsAbandoned, _waitsRouted, _waitsInert;

    /// <summary>The inert stage: nothing pooled, nothing staged hidden, no called template
    /// relocated. What the ambient world runtime and every plain testing runtime take —
    /// the three flags are sealed, so a runtime built this
    /// way cannot be talked into a template role after the fact.</summary>
    public AnimRuntime()
        : this(NewTemplateStage())
    {
    }

    /// <summary>Takes a SEALED template stage: the pool/reveal/relocate role is decided by whoever
    /// knows the runtime's job, before this runtime exists. The runtime hooks are wired here rather
    /// than passed to the stage's constructor, because the stage's <c>findAll</c> needs the
    /// resolver and the resolver's <c>ownRootsOf</c> is the stage's
    /// <see cref="TemplateStage{TNode}.RootsFor"/>; both are delegates, invoked after this returns.
    /// One stage per runtime. The resolver's own policy flags follow at <see cref="Bootstrap"/>.</summary>
    public AnimRuntime(TemplateStage<Node3D> stage)
    {
        _templateStage = stage;
        _resolver = new NameResolver<Node3D>(Node3DIdentity.Instance, _templateStage.RootsFor, IsInstanceValid);
        _templateStage.Wire(
            FindAll,
            Anchors,
            IsLive,
            () => _instances.Select(i => (i.Def, i.Anchor)),
            LevelsTemplate,
            TemplateStillAnimated,
            NameOf,
            s => IndexWorld(s, indexByPointer: false),
            () => _resolver.ClearFindCache(),
            ApplyResetStatesWithin);
    }

    /// <summary>How many <see cref="PlayEffectAt"/> calls took a pool slot whose previous instance
    /// was still live — the pool being smaller than the concurrency it met, so those two calls
    /// share one template copy (<see cref="TemplateStage{TNode}.Pooled"/>). Zero
    /// is "the pool covered everything asked of it"; a growing count is the number to size against
    /// (the pool size itself is a TUNE, `WorldEffectsFactory.EffectPoolSlots`). Forwards to
    /// <see cref="TemplateStage{TNode}.Recycles"/>, which counts both wrap flavours.</summary>
    public int PoolRecycles => _templateStage.Recycles;

    /// <summary>Running count of ballistic <see cref="MotionRuntime"/> bodies launched — the debris
    /// pieces a death or crash flings (translation/translation_range/scale/forward_rotation over a
    /// run time). Zero at bootstrap (nothing ambient fires the ballistic path); the damage harness
    /// samples the delta across a kill to prove the wreck actually tumbles.</summary>
    public int BallisticMotionsLaunched => Motions.LaunchCount;

    /// <summary>Live animation instances currently running (diagnostics).</summary>
    public int ActiveInstances => _instances.Count;

    /// <summary>Per-kind counts of events (and puffer/sound sub-reasons) this runtime processed but
    /// could not act on — the same tally <c>ReportUnhandled</c> prints at bootstrap, exposed so a
    /// post-bootstrap harness (the effects test) can see WHY an effect built no puffer
    /// (<c>PufferState(no host node)</c>, <c>PufferState(no texture: …)</c>).</summary>
    public IReadOnlyDictionary<string, int> UnhandledEventCounts => _unhandled;

    /// <summary>The live per-instance HP of every destructible node group in this world.
    /// Built during the bootstrap; the source of the value <c>ANIM_HEALTH</c> conditions read.
    /// Weapon damage and the death sequence act through it.</summary>
    public DestructibleRegistry Destructibles => _destructibles;

    /// <summary>Every definition of the bound program, read-only — for a consumer that selects
    /// defs by a rule <see cref="Play"/>'s name lookup cannot express (the zeppelin damage
    /// runtime finding the hull-death def by its activation prerequisite, M4 F18).</summary>
    public IReadOnlyList<AnimDefinition> ProgramDefs => _program.Defs;

    /// <summary>The built world's own root, the node every world subtree hangs under. Exposed so
    /// a caller can ask which top-level world object a node belongs to (a turret gunner's "which
    /// platform am I bolted to"); null before <see cref="Bind"/>.</summary>
    public Node3D? WorldRoot => _root;

    /// <summary>Pins the runtime's RNG for a reproducible run. Every session sets one, derived from
    /// the master seed (<see cref="Utils.Rng"/>); null leaves it drawn from .NET's own entropy. Set
    /// at construction through the object initializer, before <see cref="Bind"/>.</summary>
    public int? Seed
    {
        init
        {
            _seed = value;
            if (value is { } s)
                _rng = new Random(s);
        }
    }

    Action<EventDispatch>? ISequenceHost.OnEventDispatched => OnEventDispatched;

    Func<bool>? ISequenceHost.PendingWait => _pendingWait;

    /// <summary>How many <c>WAIT_FOR_COMPLETION</c> holds this runtime has armed, and how many of
    /// those ended at <see cref="WaitCeilingS"/> instead of at their callee. The second is the one
    /// that matters and is expected to stay 0; the `wait-for-completion` suite asserts both.
    /// </summary>
    public int WaitsInstalled => _waitsInstalled;

    public int WaitsAbandoned => _waitsAbandoned;

    /// <summary>Flagged calls this runtime handed to the world-effects runtime instead of holding
    /// on — the one scope boundary the wait has (see <see cref="NoteRoutedWait"/>).</summary>
    public int WaitsRouted => _waitsRouted;

    /// <summary>Flagged calls that reached no live callee instance, so held nothing.</summary>
    public int WaitsInert => _waitsInert;

    /// <summary>One-shot SOUND events that resolved to a stream and fired this session (the
    /// destruction/damage/impact audio). Exposed for the damage-test harness, which cannot
    /// screenshot audio: a nonzero delta across a kill is how "the death's explosion sounded" is
    /// verified headless.</summary>
    public int OneShotSoundsPlayed { get; private set; }

    /// <summary>How many PUFFER_STATE emitters this runtime has actually built (not just started
    /// the owning def). The world-effects verify checks this rather than "the def ran" — a
    /// started effect whose factory is retired or whose textures are missing builds nothing and
    /// renders nothing (verification.md).</summary>
    public int PuffersBuilt => Emitters.Built;

    /// <summary>Every PUFFER_STATE emitter's whole life on this runtime: start, the four stops,
    /// the follow, and the census a suite reads.
    /// ⚠ Built on FIRST USE, inside <see cref="Bind"/>, capturing <see cref="EmitterFactory"/>,
    /// <see cref="DefScopedPufferKeys"/> and <see cref="DebugMotions"/> as they stand then. Set all
    /// three before binding; flipping one afterwards does not reach the director.</summary>
    public EmitterDirector Emitters => _emitters ??= new EmitterDirector(
        EmitterFactory ?? new SpentEmitterFactory(), DefScopedPufferKeys, DebugMotions, Count);

    /// <summary>The live motion collection and its registration rules. `internal` so the
    /// `bounce-launch` suite can ask <c>OwesBounce</c>, which is the retirement hold's own
    /// mechanism.</summary>
    internal MotionSet Motions { get; } = new();

    /// <summary>Builds a sealed <see cref="TemplateStage{TNode}"/> over the Godot adapter. ⚠ Spell
    /// the engine hooks here and nowhere else: node identity, the pool-slot ancestry walk, the
    /// <c>TopLevel</c>+<c>GlobalTransform</c> placement write, the visibility write. A caller only
    /// decides the role. <paramref name="debugMotions"/> is baked because the stage exists before
    /// the runtime; it gates the pooled caller-slot log line alone.</summary>
    public static TemplateStage<Node3D> NewTemplateStage(bool pooled = false, bool shown = false,
        bool placesCalled = false, bool debugMotions = false,
        IEnumerable<string>? placeExempt = null)
    {
        return new TemplateStage<Node3D>(
            Node3DIdentity.Instance,
            SlotMarkOf,
            IsInstanceValid,
            n => n.GlobalTransform,
            PlaceNodeAt,
            (n, visible) => n.Visible = visible,
            s => GD.Print(s),
            () => debugMotions,
            pooled,
            shown,
            placesCalled,
            placeExempt);
    }

    /// <summary>Configures (but does not bind) the world-effects runtime: the three invariant flags
    /// a "renders effects at a call site, no ambience of its own" role always takes. The caller
    /// still calls <see cref="Bind"/> and adds the returned node to the tree.
    /// ⚠ <paramref name="stage"/> arrives sealed. Template policy is stage state, never runtime
    /// properties, so build the stage with the role it wants (<see cref="NewTemplateStage"/>).</summary>
    public static AnimRuntime ForEffects(TemplateStage<Node3D> stage, int seed,
        IEmitterFactory emitterFactory,
        bool debugMotions, float effectTtl, Func<Vector3> playerPosition)
    {
        return new AnimRuntime(stage)
        {
            AutoStart = false,
            NameResolveFallback = true,
            SoundHandledElsewhere = true,
            DefScopedPufferKeys = true,
            DebugMotions = debugMotions,
            EmitterFactory = emitterFactory,
            EffectTtl = effectTtl,
            Seed = seed,
            PlayerPosition = playerPosition,
        };
    }

    /// <summary>Configures (but does not bind) the per-player crash rig's construction ritual —
    /// the same three invariant flags and the same sealed <paramref name="stage"/> handover as
    /// <see cref="ForEffects"/>, but with no <c>EffectTtl</c> or
    /// <c>PlayerPosition</c> (the crash def has no PUFFER_STATE that needs either). The caller still
    /// calls <see cref="Bind"/> + adds the returned node to the tree.</summary>
    public static AnimRuntime ForCrashRig(TemplateStage<Node3D> stage, int seed,
        IEmitterFactory emitterFactory,
        bool debugMotions)
    {
        return new AnimRuntime(stage)
        {
            AutoStart = false,
            NameResolveFallback = true,
            SoundHandledElsewhere = true,
            DebugMotions = debugMotions,
            EmitterFactory = emitterFactory,
            Seed = seed,
        };
    }

    /// <summary>The world nodes a definition anchors to, for the inspect tools — the runtime's own
    /// answer, so a readout shows what the bootstrap actually bound rather than a re-derivation.
    /// A null entry is a global (anchorless) instance. Read-only: the list is the cached one.</summary>
    public IReadOnlyList<Node3D?> AnchorsOf(AnimDefinition def) => Anchors(def);

    /// <summary>Every world node matching a NAME pattern, optionally restricted to one subtree —
    /// the same wildcard matching and the same memoized index the dispatch uses, exposed so an
    /// inspect tool asks the engine instead of re-implementing the matcher. Read-only.</summary>
    public IReadOnlyList<Node3D> FindNodes(string pattern, Node3D? scope = null) => FindAll(pattern, scope);

    /// <summary>Runs the bootstrap passes against a built world. A separate call (not folded into
    /// construction) so a caller can set build-time-only collaborators (notably
    /// <see cref="EmitterFactory"/>, which depends on the session TextureArchive's lifetime)
    /// before the passes fire the events that need them. The world runtime binds directly; the
    /// <see cref="ForEffects"/>/<see cref="ForCrashRig"/> role factories return unbound for the same
    /// reason.</summary>
    public void Bind(Node3D worldRoot, AnimProgram program)
    {
        Name = "AnimRuntime";
        Bootstrap(worldRoot, program);
    }

    /// <summary>Re-pins the RNG to the constructed <see cref="Seed"/>, and clears the sound groups'
    /// last-picked memory with it — that recency state sits outside the RNG, so restoring only the
    /// dice would still diverge on the first weighted pick. The animation debugger calls this on
    /// every Play/Restart; without it the stream would continue and a "restart" would branch on the
    /// first RANDOM_WEIGHT.</summary>
    public void Reseed()
    {
        if (_seed is { } s)
        {
            _rng = new Random(s);
        }
        Sounds?.ResetGroupRecency();
    }

    /// <summary>The bind-time resolution census — resolver-owned data, projected here for the
    /// <c>--node=</c> stage's log and the node lab. Empty unless <see cref="ReportResolution"/>
    /// was set before <see cref="Bind"/>; see <see cref="NameResolver{TNode}.ResolutionLines"/>
    /// for what the lines mean and why they exist.</summary>
    public IReadOnlyList<string> ResolutionLines() => _resolver.ResolutionLines();

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
                // ⚠ Pass applyReset:false for the crash trigger; its reset restores the healthy
                // panels and hides the wreck. Re-posing IS the reset, so it clears the invalidation
                // latch too, or a self-invalidating def could only ever replay once per session.
                if (applyReset && def.ResetState != null)
                {
                    _invalidated.Remove(def);
                    ApplyInstant(def.ResetState.Events, def, anchor);
                }
                Start(def, anchor);
                started.Add((def, anchor));
            }
        }
        return started;
    }

    /// <summary><see cref="Play"/>, scoped to one world subtree: starts only the instances
    /// whose anchor sits at or under <paramref name="scope"/>. C1 carries three
    /// <c>hangerdoors</c> nodes — the zeppelin's and two ground hangars' — so a generator's
    /// door call must not swing every namesake in the chapter (the F20 case; ground hangar
    /// doors are mission-animation territory, BL-350).</summary>
    public List<(AnimDefinition Def, Node3D? Anchor)> PlayWithin(Node3D scope, string animName,
        bool applyReset = true)
    {
        var started = new List<(AnimDefinition Def, Node3D? Anchor)>();
        foreach (var def in _program.ByAnimName(animName))
        {
            foreach (var anchor in Anchors(def))
            {
                if (anchor == null || (anchor != scope && !scope.IsAncestorOf(anchor)))
                    continue;
                if (applyReset && def.ResetState != null)
                    ApplyInstant(def.ResetState.Events, def, anchor);
                Start(def, anchor);
                started.Add((def, anchor));
            }
        }
        return started;
    }

    /// <summary><see cref="Stop"/>, scoped like <see cref="PlayWithin"/>: tears down only the
    /// named instances anchored at or under <paramref name="scope"/>.</summary>
    public void StopWithin(Node3D scope, string animName)
    {
        // Collect first: RemoveInstances mutates _instances, one call per distinct anchor.
        var anchors = new HashSet<Node3D>();
        foreach (var inst in _instances)
        {
            if (string.Equals(inst.Def.AnimName, animName, StringComparison.OrdinalIgnoreCase)
                && inst.Anchor is { } anchor && (anchor == scope || scope.IsAncestorOf(anchor)))
                anchors.Add(anchor);
        }
        foreach (var anchor in anchors)
            RemoveInstances(animName, anchor, tearDown: true);
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
            if (!string.IsNullOrEmpty(name) && Resolve(name, def, null) is { } node)
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
                 + $"{_instances.Count} live instance(s), {Motions.Count} live motion(s)");
        if (ran.Count > 0 || missing.Count > 0)
            GD.Print($"anim: start anims [{string.Join(", ", ran)}]" +
                     (missing.Count > 0 ? $", undefined here: [{string.Join(", ", missing)}]" : ""));
    }

    /// <summary>Reverses <see cref="StartAmbient"/>, the animation debugger's ambient-off half.
    /// Tears down every live instance and its resources EXCEPT the one carrying
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
        _rangeDeferred.Clear(); // a quiet stage must not proximity-start ambient defs
        _ambientStarted = false;
        GD.Print($"anim: ambient stopped — {_instances.Count} live instance(s) kept, "
                 + $"{Motions.Count} live motion(s)");
    }

    /// <summary>Hard-stops everything this runtime created and re-applies every anchored
    /// definition's RESET_STATE: the per-player crash runtime's respawn. The caller re-homes any
    /// node the def moved but has no reset event for.
    /// ⚠ Clear the whole resource pool, not just live instances, and <c>Clear</c> puffers rather
    /// than sustain-ending them. An effect whose sequence already ended is off <c>_instances</c>
    /// with its puffer still emitting. ⚠ Never call this from the shared world runtime.</summary>
    public void ResetToBaseState()
    {
        _instances.Clear();
        Motions.Reset();
        Emitters.Reset();
        _inputNodes.Clear();
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

    /// <summary>Indexes a subtree added to the world AFTER the bootstrap (the effect-template stage
    /// <see cref="WorldBuilder"/> deliberately skips), then applies the RESET_STATE of every
    /// definition now anchored within it, exactly as bootstrap pass 1 would have. The find cache is
    /// cleared, since the bootstrap may have cached these names as resolving to nothing. Additive:
    /// nothing running is disturbed, and mission setup does not re-run.</summary>
    public void IndexStage(Node3D subtree)
    {
        IndexWorld(subtree);   // appends the subtree's nodes to the resolver (name + gamez index)
        _resolver.ClearFindCache();    // drop stale "resolves to nothing" results cached during bootstrap
        ApplyResetStatesWithin(subtree);
    }

    /// <summary>Indexes one pooled copy of a library-root call template: everything
    /// <see cref="IndexStage"/> does, except the resolver's by-index map.
    /// ⚠ Never add a pooled copy to that map. Every copy carries the same compiled node indices, so
    /// the map holds only the first claimant and a second copy's events would resolve onto the
    /// first copy's nodes. Anchor-scoped name resolution has no such collision, which is why a copy
    /// resolves against itself as long as it is passed as the anchor.</summary>
    public void IndexPooledCopy(Node3D subtree) => _templateStage.IndexPooledCopy(subtree);

    // ---- ISequenceHost: the sequence interpreter's 3-point view of this runtime, satisfied by
    // explicit interface implementation so Dispatch/EvaluateCondition stay off AnimRuntime's own
    // surface. See SequenceRunner.cs. ----
    bool ISequenceHost.Dispatch(AnimEvent ev, AnimDefinition def, Node3D? anchor, bool instant, out float duration) =>
        Dispatch(ev, def, anchor, instant, out duration);

    bool ISequenceHost.EvaluateCondition(AnimData? condition, AnimDefinition def, Node3D? anchor) =>
        EvaluateCondition(condition, def, anchor);

    public override void _Process(double delta)
    {
        if (ManualAdvance)
        {
            return;
        }
        // Each sub-step is advanced separately rather than summed: the event scheduler resolves
        // per step, so one 4/60 s call and four 1/60 s calls are not the same playback.
        if (GameClock.Current is { } clock)
        {
            for (int i = 0; i < clock.Steps; i++)
            {
                Advance(clock.Dt);
            }
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
        _elapsed += dt;
        // Motions advance ONCE per frame, here — not from the sequence runners, which would
        // apply dt once per running sequence and run the train at 4× speed.
        TickMotions(dt);
        Emitters.Tick(dt);
        TickLights(dt);
        Sounds?.Tick();
        SweepEffectTtls(dt);
        _templateStage.Sweep();
        TickDeferredByRange();
        if (DebugMotions)
            LogMotions(dt);
        // Instances can finish (and CallAnimation can add) during the walk, so iterate a copy.
        for (int i = _instances.Count - 1; i >= 0; i--)
        {
            var inst = _instances[i];
            inst.Advance(this, dt);
            if (Retirable(inst))
            {
                _instances.RemoveAt(i);
                FinishInputGoverned(inst.Def, inst.Anchor);
                FinishEffectInstance(inst.Def, inst.Anchor);
                _templateStage.RetireWhenIdle(inst.Def, inst.Anchor);
                OnInstanceFinished?.Invoke(inst.Def, inst.Anchor);
            }
        }
    }

    /// <summary>Starts a definition on one anchor (null resolves its node names globally), running
    /// every sequence that is not ACTIVATION ON_CALL. Starting a def already live on the same
    /// anchor restarts it; CALL_ANIMATION deliberately does not take this path for a running
    /// animation. <paramref name="protectSelfInvalidate"/> is the death path's opt-in against a
    /// same-name self stop tearing this burst's own instance down; keep it off elsewhere
    /// (<see cref="_startingInstances"/>).</summary>
    public void Start(AnimDefinition def, Node3D? anchor, bool protectSelfInvalidate = false)
    {
        // ⚠ Keep the depth bound. CALL_ANIMATION chains are data and some chapters author cycles,
        // which recurse until the stack dies, since a start fires its t=0 events immediately.
        if (_startDepth >= MaxStartDepth)
        {
            Count("CallAnimation(depth limit)");
            return;
        }
        // ⚠ Keep this refusal; it is where INVALIDATE_ANIMATION's whole effect lands. Without it a
        // def that invalidated itself would still re-run on the next call.
        if (_invalidated.Contains(def))
        {
            Count("Start(invalidated)");
            return;
        }
        // ⚠ Do not tear down the live resources on a restart; the new instance re-establishes them
        // idempotently, and tearing down rebuilds every one of them instead. A caller that wants
        // them cleared calls Stop directly.
        RemoveInstances(def.AnimName, anchor, tearDown: false);
        var inst = new AnimInstance(def, anchor);
        foreach (var seq in def.Sequences.Where(s => !s.OnCallOnly))
            inst.AddRunner(seq);
        if (inst.Runners.Count == 0)
            return;
        _instances.Add(inst);
        OnInstanceStarted?.Invoke(def, anchor);
        // Fire whatever is due at t=0 immediately, so instantaneous sequences (zepstate's
        // active-state roster) settle during the build rather than one frame later.
        _startDepth++;
        _startingInstances.Push((inst, protectSelfInvalidate));
        try
        {
            inst.Advance(this, 0f);
        }
        finally
        {
            _startingInstances.Pop();
            _startDepth--;
        }
        // The t=0 events can finish the instance, so notify only a finish that removed something:
        // the timeline needs start and finish notifications balanced against the live count.
        if (Retirable(inst) && _instances.Remove(inst))
        {
            FinishInputGoverned(def, anchor);
            OnInstanceFinished?.Invoke(def, anchor);
        }
    }

    /// <summary>Stops every live instance of an animation name (optionally only on one anchor) and
    /// tears down the resources it created. Removing the instance alone would leave its motions
    /// driving nodes, its puffers emitting, its lights lit and its sounds playing;
    /// <see cref="TearDownResourcesOf"/> clears all four. ⚠ <see cref="Start"/>'s own restart must
    /// not route through here; it keeps the resources so re-assertion is a no-op.</summary>
    public void Stop(string? animName, Node3D? anchor = null) =>
        RemoveInstances(animName, anchor, tearDown: true);

    /// <summary>INVALIDATE_ANIMATION: latches an animation off without touching what it is doing.
    /// Whatever is running keeps running to its own end; what changes is that nothing can start it
    /// again until a RESET_ANIMATION (or an explicit <see cref="Play"/>) clears the latch. See
    /// <see cref="_invalidated"/> for the state machine this stands in for.</summary>
    public void Invalidate(string? animName)
    {
        if (string.IsNullOrEmpty(animName))
            return;
        foreach (var def in _program.ByAnimName(animName))
            _invalidated.Add(def);
    }

    /// <summary>RESET_ANIMATION: clears the invalidation latch and re-poses the definition's
    /// RESET_STATE on each of its anchors. Does NOT start anything — the original's reset is
    /// state-plus-pose only, and the one authored user (`turnoff_fliteN`) issues its own
    /// STOP_ANIMATION first.</summary>
    public void ResetAnimation(string? animName)
    {
        if (string.IsNullOrEmpty(animName))
            return;
        foreach (var def in _program.ByAnimName(animName))
        {
            _invalidated.Remove(def);
            if (def.ResetState == null)
                continue;
            foreach (var anchor in Anchors(def))
                ApplyInstant(def.ResetState.Events, def, anchor);
        }
    }

    // ---- world-effects runtime ----
    /// <summary>Does this runtime's program hold a definition for an effect animation name? The
    /// world runtime tests this before routing a death's CALL_ANIMATION here, so only the curated
    /// impact/destruction effects are handed off (doors and other calls fall through).</summary>
    public bool Handles(string animName) => _program.ByAnimName(animName).Count > 0;

    /// <summary>The definitions carrying one ANIMATION_NAME — the same lookup
    /// <c>CALL_ANIMATION</c> dispatch uses, exposed so the zeppelin damage runtime can register
    /// a record's destroy anim as a destructible pool (M4 F18).</summary>
    public IReadOnlyList<AnimDefinition> DefsFor(string animName) => _program.ByAnimName(animName);

    /// <summary>Stages the named effect at an absolute world point: relocates each matching
    /// template root onto the point and starts the definition, as a CALL_ANIMATION would with a
    /// synthetic site. <paramref name="inputNode"/> is the callee's INPUT_NODE, so a damage-stage
    /// sputter emits on the damaged object and its <c>NodeActive</c> loop gate reads that object.
    /// ⚠ Give an input-governed def no TTL; its lifetime is authored. Everything else takes
    /// <paramref name="ttl"/>, or <see cref="EffectTtl"/> when that is 0.</summary>
    public bool PlayEffectAt(string animName, Vector3 worldPoint, Node3D? inputNode = null,
        float ttl = 0f)
    {
        float bound = ttl > 0f ? ttl : EffectTtl;
        bool matched = false;
        // The checkout (TakeNextSlot) plus the Start it feeds, coarse over
        // the (usually one) def this anim name resolves to — not per particle.
        using (PerfSample.Scope(PerfSite.EffectCheckout))
        {
            foreach (var def in _program.ByAnimName(animName))
            {
                // Anchor on the def's own template root when it resolves, since its at_node and
                // motion targets live under it; null falls back to global name resolution. Pooled,
                // that root is this call's own slot, and only that copy moves onto the site.
                var roots = _templateStage.TakeNextSlot(def);
                _templateStage.PlaceOn(roots, worldPoint, LevelsTemplate(def));
                var anchor = roots.FirstOrDefault();
                bool governed = inputNode != null && IsInstanceValid(inputNode)
                                && DefConditionsOnInputNode(def);
                if (governed)
                {
                    Stop(def.AnimName, anchor);
                    _inputNodes[(def, anchor)] = inputNode!;
                }
                Start(def, anchor);
                // After Start, not with the placement: a governed def's Stop above hides the root
                // again, and this must be the last word on it for the instance now running.
                _templateStage.Reveal(def, anchor, visible: true);
                matched = true;
                if (!governed && bound > 0f)
                {
                    // One deadline per (def, anchor): a replay restarts the instance, so an older
                    // entry left in place would stop the NEW instance at the OLD deadline — a second
                    // gun hit 0.2 s after the first would emit for 0.1 s.
                    _effectTtls.RemoveAll(t => t.Def == def && t.Anchor == anchor);
                    _effectTtls.Add((def, anchor, _effectClock + bound));
                }
            }
        }
        return matched;
    }

    /// <summary>Stops every live instance and clears the effect-TTL list — a full reset of what
    /// PlayEffectAt started, so the verify measures each effect in a clean window (these effects
    /// share puffer names/hosts, so a lingering one would contaminate the next). Not used in play.</summary>
    public void StopAll()
    {
        foreach (var name in _instances.Select(i => i.Def.AnimName).Distinct().ToList())
            Stop(name);
        _effectTtls.Clear();
    }

    /// <summary>Escalates a destructible's visible damage to the stage its current HP sits in,
    /// running its <c>DAMAGE_SEQUENCE</c> (docs/formats/destructibles.md). Call it after the HP
    /// changes; it neither decrements HP nor runs the death sequence. Returns false when no new
    /// threshold was crossed, or the def carries no such sequence.
    /// ⚠ Keep the stage gate. Without it a one-shot damage effect re-fires on every hit inside a
    /// band; only a sustained one is spared by CALL_ANIMATION's own live guard.</summary>
    public bool ApplyDamageStages(DestructibleRegistry.Instance inst)
    {
        var seq = inst.Def.Sequences.FirstOrDefault(s =>
            string.Equals(s.Name, DamageSequenceName, StringComparison.OrdinalIgnoreCase));
        if (seq == null)
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
        host.Advance(this, 0f);
        return true;
    }

    /// <summary>Applies weapon damage to whatever destructible a struck world node belongs to, and
    /// escalates its visible damage. <paramref name="struck"/> resolves to the owning instance by
    /// walking up to the nearest registered anchor. World destructibles carry HEALTH only, with no
    /// armour pool (docs/formats/destructibles.md). At zero it marks the instance destroyed and
    /// runs the death sequence. Returns true when the hit landed on a destructible.</summary>
    public bool DamageAt(Node? struck, float healthDamage)
    {
        var inst = _destructibles.Resolve(struck);
        if (inst == null)
            return false;
        if (inst.Status == DestructibleRegistry.State.Destroyed)
            return true;   // already dead — the death sequence owns it from here
        float before = inst.Health;
        inst.Health = Math.Max(0f, inst.Health - Math.Max(0f, healthDamage));
        ApplyDamageStages(inst);
        bool destroyed = inst.Health <= 0f;
        if (destroyed)
        {
            inst.Status = DestructibleRegistry.State.Destroyed;
            RunDeathSequence(inst);
        }
        if (_damagesLogged < 12)
        {
            _damagesLogged++;
            GD.Print($"damage: -{healthDamage:0.##} on {NameOf(inst.Anchor)} " +
                     $"HP {before:0.##}→{inst.Health:0.##}" +
                     (destroyed ? " DESTROYED — death sequence run" : $" [stage {inst.DamageStage}]"));
        }
        return true;
    }

    /// <summary>A plane collision with a world node. ⚠ Only a <c>WeaponOrCollideHit</c>
    /// destructible takes collision damage; a <c>WeaponHit</c> object must stand and kill the
    /// plane that rams it. Returns true for the former, so the caller flies the plane through it,
    /// and false for everything else, which the caller treats as a solid crash. The damage runs
    /// through <see cref="DamageAt"/>, so the death is identical to a weapon kill.</summary>
    public bool CollideDamageAt(Node? struck, float healthDamage)
    {
        var inst = _destructibles.Resolve(struck);
        if (inst == null
            || !inst.Def.Activation.Equals("WeaponOrCollideHit", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        DamageAt(struck, healthDamage);
        return true;
    }

    /// <summary>Returns a destroyed destructible to healthy, for the debug tools and respawn: stop
    /// the live death, restore the authored pose of every node it moved, re-apply the def's
    /// <c>RESET_STATE</c>, and restore the HP pool. ⚠ All four steps are needed for idempotence.
    /// Skipping the pose restore leaves the ballistic pieces wherever they flew, so a re-destroy
    /// launches from the wrong place, whether or not a chained death call has fired yet.</summary>
    public void ResetDestructible(DestructibleRegistry.Instance inst)
    {
        var def = inst.Def;
        Stop(def.AnimName, inst.Anchor);
        // ⚠ Re-apply a called def's OWN reset state, on the anchor its call actually ran on (a
        // pooled copy, not necessarily inst.Anchor). It is never inherited from the caller, so
        // without it an ACTIVE_STATE the call flipped on sits inert but still shown.
        void ResetCalled(AnimDefinition called, Node3D calledAnchor)
        {
            Stop(called.AnimName, calledAnchor);
            RestoreRestPoses(called, calledAnchor);
            if (called.ResetState != null)
                ApplyInstant(called.ResetState.Events, called, calledAnchor);
        }
        if (inst.ChainedDeathDef is { } chained)
        {
            ResetCalled(chained, inst.Anchor);
            inst.ChainedDeathDef = null;
        }
        // Every CALL_ANIMATION target the death dispatched onto its own anchor, reset the same way,
        // so the result does not depend on whether the called def's motions had finished.
        foreach (var (local, localAnchor) in inst.LocalCallTargets)
            ResetCalled(local, localAnchor);
        inst.LocalCallTargets.Clear();
        // ⚠ The reset must stop the damage-stage effects itself. They live on the external runtime
        // and loop while the healthy node stays active, which a heal never interrupts. Take the
        // names from the def's own DAMAGE_SEQUENCE calls, never from a hardcoded list.
        if (ExternalEffectStop != null
            && inst.DamageStage > 0
            && def.Sequences.FirstOrDefault(s =>
                string.Equals(s.Name, DamageSequenceName, StringComparison.OrdinalIgnoreCase)) is { } damageSeq)
        {
            foreach (var ev in damageSeq.Events)
                if (ev.Kind == "CallAnimation" && ev.Data.Str("name") is { } stageEffect)
                    ExternalEffectStop(stageEffect);
        }
        RestoreRestPoses(def, inst.Anchor);
        if (def.ResetState != null)
            ApplyInstant(def.ResetState.Events, def, inst.Anchor);
        inst.Health = inst.MaxHealth;
        inst.Status = DestructibleRegistry.State.Healthy;
        inst.DamageStage = 0;
    }

    // ---- state application ----
    /// <summary>Clamps each near-zero scale component to <see cref="MinPoseScale"/> (sign
    /// preserved) so an animated pose can never write a singular basis. Applied at every
    /// pose-scale write site: <c>PoseScale</c> and <c>FromToMotion.Seek</c>.</summary>
    internal static Vector3 NonSingularScale(Vector3 s) => new(
        Mathf.Abs(s.X) < MinPoseScale ? (s.X < 0f ? -MinPoseScale : MinPoseScale) : s.X,
        Mathf.Abs(s.Y) < MinPoseScale ? (s.Y < 0f ? -MinPoseScale : MinPoseScale) : s.Y,
        Mathf.Abs(s.Z) < MinPoseScale ? (s.Z < 0f ? -MinPoseScale : MinPoseScale) : s.Z);

    // `internal` (not `private`) so EmitterDirector's debug lines name their nodes the same way
    // every other anim log line does — same-assembly only, no wider exposure intended.
    internal static string NameOf(Node3D n) =>
        n.HasMeta(NameMeta) ? n.GetMeta(NameMeta).AsString() : n.Name.ToString();

    /// <summary>Where a world node visually IS, for effect siting and puffer emission. ⚠ Do not
    /// use <c>GlobalPosition</c> for this: an absolute-modelled gamez subtree holds its vertices in
    /// world space under an identity transform, so its origin is the map corner. When the origin
    /// lies outside the subtree's world mesh bounds this returns the bounds centre; a node with a
    /// real transform keeps its origin exactly, and a meshless node has no bounds.</summary>
    internal static Vector3 VisualOriginOf(Node3D node)
    {
        var box = UI.SelectionService.SubtreeWorldAabb(node);
        if (box.Size.LengthSquared() <= 1e-9f)
            return node.GlobalPosition;
        return box.Grow(1f).HasPoint(node.GlobalPosition) ? node.GlobalPosition : box.GetCenter();
    }

    /// <summary>The node's authored pose, remembered the first time anything moves it, so
    /// every pose op stays an offset from the rest pose rather than compounding.</summary>
    internal Transform3D RestOf(Node3D node)
    {
        if (!_rest.TryGetValue(node, out var rest))
            _rest[node] = rest = node.Transform;
        return rest;
    }

    /// <summary>Whether this node's next ballistic launch continues from where it landed, clearing
    /// the mark as it answers. See <see cref="_resumeFromLanding"/>.</summary>
    internal bool ConsumeLandingResume(Node3D target) => _resumeFromLanding.Remove(target);

    /// <summary>Marks a node as having just landed by contact — see
    /// <see cref="_resumeFromLanding"/>. Called on the dispatch path, and by the
    /// <c>ground-contact</c> suite, which drives a motion set directly.</summary>
    internal void MarkLandingResume(Node3D target) => _resumeFromLanding.Add(target);

    // OBJECT_OPACITY_STATE applies to the whole subtree, as a per-instance shader parameter
    // rather than a material edit: SceneBuilder's materials are cached and shared, so writing
    // alpha into one would fade every other node that happens to use it. A partial opacity
    // landing on an opaque-variant mesh (no alpha path in the shader) swaps that instance's
    // surfaces to a fade-capable twin material for the duration — see EnsureOpacityPath;
    // anything still without a path after that is counted, not swallowed.
    internal void SetSubtreeOpacity(Node3D node, float alpha)
    {
        if (_opacity.TryGetValue(node, out float prev) && Mathf.IsEqualApprox(prev, alpha))
            return;
        _opacity[node] = alpha;

        // The fade is a shader parameter, which visibility knows nothing about — so a subtree
        // faded to nothing is marked faded and its colliders derive from that too. Reports only
        // the crossing, not every tick of the fade.
        bool collidable = alpha > OpacityCollisionEpsilon;
        if (WorldCollision.SetFaded(node, !collidable))
        {
            GD.Print($"anim: fade {(collidable ? "restored" : "dropped")} colliders under '{node.Name}'");
        }

        int applied = ApplyOpacity(node, alpha);
        if (applied == 0 && !Mathf.IsEqualApprox(alpha, 1f))
            Count("ObjectOpacityState(no alpha path)");
    }

    // How many of a DAMAGE_SEQUENCE's health thresholds hp has fallen at or below — the object's
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
    // range's upper bound (its lower bound is left to EvaluateCondition when the cascade actually
    // runs). Null for a non-health condition, which does not stage.
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

    // The node name an event targets — node for most compiled kinds (ObjectMotion), name for others
    // (ObjectActiveState's hand-authored shape, ObjectMotionFromTo,
    // ObjectOpacityFromTo/ObjectOpacityState). Matches the exact healthy/destroyed/dbase role words
    // where that matters, never a _dest suffix (docs/formats/destructibles.md).
    private static string RoleName(AnimEvent ev) => ev.Data.Str("node") ?? ev.Data.Str("name") ?? "";

    // Does target's own sequences author the healthy/destroyed swap — an OBJECT_ACTIVE_STATE that
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
            return (active && (name.Contains("destroyed", StringComparison.OrdinalIgnoreCase)
                                || name.Contains("dbase", StringComparison.OrdinalIgnoreCase)))
                || (!active && name.Contains("healthy", StringComparison.OrdinalIgnoreCase));
        }));

    // Does def's own Initial sequences author a visible death on a node outside the
    // healthy/destroyed/dbase role set: a piece moved, faded or switched off? ⚠ RunDeathSequence
    // uses this to withhold ApplyDeathSwap's RESET-derived rescue. A def whose death look is
    // authored that way must keep it; the rescue is only for a def with nothing but puffer calls,
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
            bool isRoleNode = name.Contains("healthy", StringComparison.OrdinalIgnoreCase)
                || name.Contains("destroyed", StringComparison.OrdinalIgnoreCase)
                || name.Contains("dbase", StringComparison.OrdinalIgnoreCase);
            if (isRoleNode)
                return false;
            // An ACTIVE_STATE only counts switching a piece OFF — turning one ON authors
            // nothing visible on its own (and would otherwise flag every def with an
            // unrelated startup toggle).
            return ev.Kind != "ObjectActiveState" || !ev.Data.Bool("state");
        }));

    private static string Describe(object? value) => value switch
    {
        null => "",
        Dictionary<string, object?> d => string.Join(",", d.Select(kv => $"{kv.Key}={kv.Value}")),
        _ => value.ToString() ?? "",
    };

    // A node's world position, valid DURING the bootstrap too. The world subtree is still detached
    // while the bootstrap passes run (GameSession parents it after the build), and Godot's
    // GlobalPosition both returns identity and logs an error for a node outside the tree — one line
    // per evaluation, which is thousands. Accumulate the local transforms instead; the world node
    // itself rests at the origin, so the result is the same number either way.
    private static Vector3 WorldPos(Node3D node)
    {
        if (node.IsInsideTree())
            return node.GlobalPosition;
        var xform = node.Transform;
        for (var p = node.GetParent() as Node3D; p != null; p = p.GetParent() as Node3D)
            xform = p.Transform * xform;
        return xform.Origin;
    }

    private static Vector3I CheckCellOf(Vector3 pos) => new(
        Mathf.FloorToInt(pos.X / RangeCheckCellSize),
        Mathf.FloorToInt(pos.Y / RangeCheckCellSize),
        Mathf.FloorToInt(pos.Z / RangeCheckCellSize));

    // A node's world transform, valid DURING the bootstrap too — the same detached-subtree problem
    // WorldPos solves, but keeping the basis so an AT_NODE offset still rotates into place.
    // composed reports whether the ancestor chain had to be walked (the world root not yet
    // parented), so the caller can log the fallback rather than let Godot spam !is_inside_tree()
    // and return an origin transform.
    private static Transform3D WorldTransform(Node3D node, out bool composed)
    {
        composed = !node.IsInsideTree();
        if (!composed)
            return node.GlobalTransform;
        var xform = node.Transform;
        for (var p = node.GetParent() as Node3D; p != null; p = p.GetParent() as Node3D)
            xform = p.Transform * xform;
        return xform;
    }

    // The AT_NODE and condition-node sentinels for "the node this definition was invoked on"; both
    // resolve to the anchor. The compiled u32 form and why it is matched by magnitude rather than
    // equality: docs/formats/anim-definitions.md.
    private static bool IsSelfNodeRef(string name) =>
        string.Equals(name, "INPUT_NODE", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "MAIN_ROOT_NODE", StringComparison.OrdinalIgnoreCase);

    // INACTIVE = invisible and non-collidable, the whole subtree. Only visibility is written:
    // world colliders derive their Disabled flag from it (WorldCollision), which is what makes
    // an activation INSIDE an already-hidden subtree stay non-collidable.
    private static void SetSubtreeActive(Node3D node, bool active)
    {
        node.Visible = active;
    }

    // The stage's raw slot walk (the engine half of TemplateStage.SlotOf, which memoizes
    // it): the nearest ancestor carrying PoolSlotMeta, or -1.
    private static int SlotMarkOf(Node3D node)
    {
        for (Node? n = node; n != null; n = n.GetParent())
            if (n.HasMeta(PoolSlotMeta))
                return (int)n.GetMeta(PoolSlotMeta);
        return -1;
    }

    // The stage's one placement write (see TemplateStage.PlaceOn for why TopLevel — the
    // same family as the puffer TopLevel fix).
    private static void PlaceNodeAt(Node3D root, Transform3D xf)
    {
        root.TopLevel = true;
        root.GlobalTransform = xf;
    }

    // Whether this mesh's shader reads the opacity parameter, and if not, whether a fade twin can
    // give it one. A partial opacity installs a per-surface override on THIS instance only.
    // ⚠ Never edit the shared material or mesh; both are cached across nodes. Opacity 1 removes
    // the override again. ⚠ Test for the USE (SceneBuilder.OpacityTerm), never the uniform name or
    // the include line: the uniform is declared in the shared preamble, so a name test is true even
    // with no alpha path, and the declaration is not textually in sh.Code.
    private bool EnsureOpacityPath(GeometryInstance3D g, float alpha)
    {
        if (g is not MeshInstance3D mi || mi.Mesh is not { } mesh)
            return false;
        bool fading = !Mathf.IsEqualApprox(alpha, 1f);
        bool any = false;
        for (int i = 0; i < mesh.GetSurfaceCount(); i++)
        {
            if (mi.GetSurfaceOverrideMaterial(i) is { } installed && _fadeTwins.Contains(installed))
            {
                if (fading)
                    any = true;
                else
                    mi.SetSurfaceOverrideMaterial(i, null);
                continue;
            }
            if (mesh.SurfaceGetMaterial(i) is not ShaderMaterial { Shader: { } sh } sm)
                continue;
            if (sh.Code.Contains(SceneBuilder.OpacityTerm, StringComparison.Ordinal))
            {
                any = true;
                continue;
            }
            if (fading && FadeTwinOf(sm, sh) is { } twin)
            {
                mi.SetSurfaceOverrideMaterial(i, twin);
                any = true;
            }
        }
        return any;
    }

    private ShaderMaterial? FadeTwinOf(ShaderMaterial source, Shader shader)
    {
        if (_fadeTwinCache.TryGetValue(source, out var twin))
            return twin;
        if (!_fadeShaderCache.TryGetValue(shader, out var fadeShader))
            _fadeShaderCache[shader] = fadeShader = SceneBuilder.FadeShaderFor(shader);
        if (fadeShader != null)
        {
            twin = (ShaderMaterial)source.Duplicate();
            twin.Shader = fadeShader;
            _fadeTwins.Add(twin);
        }
        _fadeTwinCache[source] = twin;
        return twin;
    }

    private int ApplyOpacity(Node node, float alpha)
    {
        int n = 0;
        if (node is GeometryInstance3D g)
        {
            g.SetInstanceShaderParameter(SceneBuilder.OpacityParam, alpha);
            if (EnsureOpacityPath(g, alpha))
                n++;
        }
        foreach (var child in node.GetChildren())
            n += ApplyOpacity(child, alpha);
        return n;
    }

    private void Bootstrap(Node3D worldRoot, AnimProgram program)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _root = worldRoot;
        _program = program;
        // ⚠ Hand these flags over before the first Add/Anchors call; they are construction-time
        // facts about this runtime. The census covers the bootstrap passes only.
        _resolver.NameResolveFallback = NameResolveFallback;
        _resolver.SuppressRootLift = SuppressRootLift;
        _resolver.ReportResolution = ReportResolution;
        _resolver.OpenCensus();
        IndexWorld(worldRoot);
        long indexMs = sw.ElapsedMilliseconds;

        // Pass 0: the per-mission world setup, which switches off the chapter content this mission
        // does not show. ⚠ It must run before any animation state, so a state can still override
        // it and so RestOf records the mission's placed pose as rest.
        Setup?.Apply(
            (name, scope) => FindAll(name, scope),
            SetSubtreeActive,
            (t, pos) => PoseTranslate(t, pos, relative: false),
            PoseRotate);

        // Pass 1: base states, and the destructible registry. ⚠ Anchored defs only: a def whose
        // NAME matches nothing here must not stomp globally-resolved bare names like 'destroyed'.
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

        // Passes 2 and 3 start the world animating. A quiet-stage bootstrap skips them and runs
        // them later through StartAmbient; the passes around them still run.
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
                 $"{_instances.Count} live instance(s), {Motions.Count} live motion(s) " +
                 $"[index {indexMs} ms, reset states {resetMs - indexMs} ms, " +
                 $"start {sw.ElapsedMilliseconds - resetMs} ms]");
        if (_destructibles.Count > 0)
            GD.Print($"anim: {_destructibles.Count} destructible instance(s) across " +
                     $"{_destructibles.DistinctAnchors} node group(s) registered " +
                     $"(mutable HP; inert until weapons land)");
        if (program.MissionLibrarySkipped.Count > 0)
            GD.Print($"anim: {program.MissionLibrarySkipped.Count} reader def(s) superseded by " +
                     $"this mission's compiled manifest (mission-scope + NAME1), not instantiated: " +
                     string.Join(", ", program.MissionLibrarySkipped.Take(8)) +
                     (program.MissionLibrarySkipped.Count > 8 ? ", …" : ""));
        if (ran.Count > 0 || missing.Count > 0)
            GD.Print($"anim: start anims [{string.Join(", ", ran)}]" +
                     (missing.Count > 0 ? $", undefined here: [{string.Join(", ", missing)}]" : ""));
        var emitterCensus = Emitters.Census;
        if (emitterCensus.Count > 0)
            GD.Print($"anim: {emitterCensus.Count} puffer emitter(s): " +
                     string.Join(", ", emitterCensus.Select(r => r.Name).Distinct()));
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
        ReportWaits();
        ReportUnhandled();
        _resolver.CloseCensus();
    }

    // Runs the two ambient-playback bootstrap passes — pass 2 (ACTIVATION ON_STARTUP definitions)
    // and pass 3 (the mission's startanims, by ANIMATION_NAME, in list order) — and returns their
    // tallies for the census. Shared with Bootstrap, so a quiet-stage bootstrap can defer them to
    // StartAmbient.
    private (int StartupRun, List<string> Ran, List<string> Missing) RunAmbientPasses()
    {
        // Pass 2: ON_STARTUP definitions run for real. ⚠ A def carrying EXECUTION_BY_RANGE must
        // defer to the proximity check rather than fire blind at t=0; it is authored to execute
        // only near the player. An unanchored one has no position to measure from.
        int startupRun = 0;
        _rangeDeferred.Clear();
        foreach (var def in _program.Defs.Where(d => d.OnStartup))
            foreach (var anchor in Anchors(def))
            {
                if (def.ByRange && anchor != null)
                {
                    _rangeDeferred.Add((def, anchor));
                    continue;
                }
                Start(def, anchor);
                startupRun++;
            }
        _rangeCheckCells.Clear(); // force a sweep on the first Advance
        if (_rangeDeferred.Count > 0)
            GD.Print($"anim: {_rangeDeferred.Count} ON_STARTUP def(s) deferred by EXECUTION_BY_RANGE: " +
                     string.Join(", ", _rangeDeferred
                         .Select(e => $"{e.Def.AnimName ?? e.Def.Name}({Mathf.Sqrt(e.Def.RangeMax):0} m)")
                         .Distinct().Take(10)) +
                     (_rangeDeferred.Count > 10 ? ", ..." : ""));

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

    // Starts any deferred ON_STARTUP EXECUTION_BY_RANGE def whose anchor the player has come
    // within range of. One-shot per (def, anchor): once started, the def runs exactly as an
    // undeferred ON_STARTUP would (its own events decide what persists).
    private void TickDeferredByRange()
    {
        if (_rangeDeferred.Count == 0)
            return;
        var positions = PlayerPositions?.Invoke() ?? new[] { PlayerPos() };
        // No-op until some player crosses a check cell (the common case, every frame).
        bool moved = positions.Count != _rangeCheckCells.Count;
        for (int i = 0; !moved && i < positions.Count; i++)
            moved = CheckCellOf(positions[i]) != _rangeCheckCells[i];
        if (!moved)
            return;
        _rangeCheckCells.Clear();
        foreach (var p in positions)
            _rangeCheckCells.Add(CheckCellOf(p));
        for (int i = _rangeDeferred.Count - 1; i >= 0; i--)
        {
            var (def, anchor) = _rangeDeferred[i];
            if (!IsInstanceValid(anchor))
            {
                _rangeDeferred.RemoveAt(i);
                continue;
            }
            var anchorPos = WorldPos(anchor);
            float d2 = float.MaxValue;
            foreach (var p in positions)
                d2 = Mathf.Min(d2, anchorPos.DistanceSquaredTo(p));
            if (d2 < def.RangeMin || d2 > def.RangeMax)
                continue;
            _rangeDeferred.RemoveAt(i);
            GD.Print($"anim: EXECUTION_BY_RANGE reached - starting " +
                     $"{def.AnimName ?? def.Name} at {Mathf.Sqrt(d2):0} m (range {Mathf.Sqrt(def.RangeMax):0} m)");
            Start(def, anchor);
        }
    }

    // indexByPointer=false is IndexPooledCopy's own case: a second (third, …) copy of the SAME
    // source GameZNode carries the SAME compiled indices as the first, and an index-keyed map can
    // only ever hold one winner per index — see that method's remark for why skipping it is
    // correct, not a loss (Targets' own symbol-miss rescue is anchor-scoped by name instead). The
    // resolver applies the flag, together with its own NameResolveFallback refusal, inside Add.
    private void IndexWorld(Node3D worldRoot, bool indexByPointer = true)
    {
        void Walk(Node3D n, Node3D? parent)
        {
            var srcName = n.HasMeta(NameMeta) ? n.GetMeta(NameMeta).AsString() : n.Name.ToString();
            int? gamezIndex = n.HasMeta(IndexMeta) ? (int)n.GetMeta(IndexMeta) : null;
            _resolver.Add(n, srcName, parent, gamezIndex, indexByPointer);
            foreach (var child in n.GetChildren())
                if (child is Node3D c)
                    Walk(c, n);
        }
        Walk(worldRoot, worldRoot.GetParent() as Node3D);
    }

    // Re-runs the quiet-stage RESET_STATE posing pass (bootstrap pass 1) for whatever now anchors
    // within a subtree added after the fact — IndexStage's (and IndexPooledCopy's) own tail.
    private void ApplyResetStatesWithin(Node3D subtree)
    {
        foreach (var def in _program.Defs)
        {
            if (def.ResetState == null)
                continue;
            foreach (var a in Anchors(def))
                if (a != null && (a == subtree || subtree.IsAncestorOf(a)))
                    ApplyInstant(def.ResetState.Events, def, a);
        }
    }

    // Records a flagged call whose callee was handed to the world-effects runtime, where this
    // runtime has no instance to hold on. ⚠ Print it here, once per callee, never through Count:
    // every routed call is a death-time event and the bootstrap census has already printed, so a
    // counter raised afterwards is never seen and the dropped hold becomes a silent skip.
    private void NoteRoutedWait(string callName)
    {
        _waitsRouted++;
        if (_routedWaitsNamed.Add(callName))
        {
            GD.Print($"anim: WAIT_FOR_COMPLETION on '{callName}' not held — the callee is routed to "
                     + "the world-effects runtime, which this one cannot poll");
        }
    }

    // Arms this dispatch's WAIT_FOR_COMPLETION hold over the instances the call reached, as the
    // ISequenceHost.PendingWait the runner reads back. Nothing is installed when none of them is
    // live: a callee whose whole choreography fires at t=0 never becomes an instance.
    private void InstallWait(string callName, List<(AnimDefinition Def, Node3D? Anchor)> waitOn)
    {
        if (!waitOn.Any(w => IsLive(w.Def, w.Anchor)))
        {
            // ⚠ Keep this line. "Reached but holding nothing" is a different state from "never
            // dispatched", and without it a probe reports an inert mechanism as untested.
            _waitsInert++;
            if (_inertWaitsNamed.Add(callName))
                GD.Print($"anim: WAIT_FOR_COMPLETION on '{callName}' had nothing to hold — no live callee instance");
            return;
        }
        _waitsInstalled++;
        _waitsByCallee[callName] = _waitsByCallee.TryGetValue(callName, out int n) ? n + 1 : 1;
        float deadline = _elapsed + WaitCeilingS;
        _pendingWait = () =>
        {
            if (!waitOn.Any(w => IsLive(w.Def, w.Anchor)))
                return false;
            if (_elapsed < deadline)
                return true;
            _waitsAbandoned++;
            GD.Print($"anim: WAIT_FOR_COMPLETION on '{callName}' abandoned after "
                     + $"{WaitCeilingS:0} s — the callee never finished");
            return false;
        };
    }

    // Is this definition already running on this anchor? (Instance identity is (definition, anchor)
    // throughout.)
    private bool IsLive(AnimDefinition def, Node3D? anchor) =>
        _instances.Any(i => i.Def == def && i.Anchor == anchor);

    // Removes matching live instances. tearDown chooses whether to also clear each instance's
    // motions/puffers/lights/sounds: true for an explicit Stop, false for Start's seamless restart,
    // which leaves them for the new instance to re-assert.
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
            if (_startingInstances.Count > 0 && _startingInstances.Peek() is { Protect: true } top
                && ReferenceEquals(top.Inst, inst))
                continue;
            _instances.RemoveAt(i);
            if (tearDown)
            {
                TearDownResourcesOf(inst.Def, inst.Anchor);
                _inputNodes.Remove((inst.Def, inst.Anchor));
                _templateStage.Reveal(inst.Def, inst.Anchor, visible: false);
            }
            // Start's seamless restart (tearDown false) keeps the entry, exactly as it keeps
            // the resources the new instance re-asserts.
            OnInstanceFinished?.Invoke(inst.Def, inst.Anchor);
        }
    }

    // An input-governed effect instance (one PlayEffectAt gave a real site node and whose def loops
    // on that node's active state) ends by its own authored exit — the NodeActive gate going false
    // — not by Stop, so its sustained emitters would keep emitting past the sequence's end. Tear
    // them down with the instance. Scoped to instances carrying an input node: ambient defs that
    // finish leaving a resource alive keep today's behaviour.
    private void FinishInputGoverned(AnimDefinition def, Node3D? anchor)
    {
        if (_inputNodes.Remove((def, anchor)))
            TearDownResourcesOf(def, anchor);
    }

    // An instance ends its sustained emitters when its own sequences end: the authored stop for a
    // def that ships none, which would otherwise burn for the whole session.
    // ⚠ Keep this instance-scoped, never sequence-scoped. A lone PufferState in a sequence that
    // ends on the same tick would take the debris trails that work down with it.
    // ⚠ End emission (SustainEnd) rather than tearing the emitter down, so live particles finish
    // their authored LIFETIME_RANGE and a later replay on this pool slot revives the entry.
    private void FinishEffectInstance(AnimDefinition def, Node3D? anchor)
    {
        // Consumes the effects runtime's TTL entry when there is one (this got there first, so
        // the sweep has nothing left to do); a world instance simply carries none.
        _effectTtls.RemoveAll(t => t.Def == def && t.Anchor == anchor);
        Emitters.EndFor(def, anchor);
    }

    // Tears down every live resource a stopped instance created, so nothing of the definition keeps
    // running after Stop. Motions and puffers are attributed to the exact (def, anchor) that
    // registered them; lights and sounds are keyed by (name, anchor) and so clear by anchor, which
    // is the instance identity and the finest attribution available for them.
    private void TearDownResourcesOf(AnimDefinition def, Node3D? anchor)
    {
        Motions.DiscardFor(def, anchor);
        Emitters.Discard(def, anchor);

        foreach (var key in _lights.Keys.Where(k => k.Anchor == anchor).ToList())
            _lights.Remove(key);

        if (Sounds != null)
            foreach (var key in _soundEmitters.Keys.Where(k => k.Anchor == anchor).ToList())
            {
                Sounds.SetActive(_soundEmitters[key], false);
                _soundEmitters.Remove(key);
            }
    }

    // Applies a list of events with no clock — the RESET_STATE path, where every op is a base state
    // and timed motions collapse to their end pose.
    private void ApplyInstant(List<AnimEvent> events, AnimDefinition def, Node3D? anchor)
    {
        foreach (var ev in events)
            Dispatch(ev, def, anchor, instant: true, out _);
    }

    // ---- the dispatch table ----
    // Executes one event, reporting its duration in seconds so the sequence runner knows when the
    // next event is due: 0 for an instantaneous state change, the run time for a timed motion, the
    // script length for an SI script. Returns false only for control-flow events the runner
    // interprets itself.
    private bool Dispatch(AnimEvent ev, AnimDefinition def, Node3D? anchor, bool instant, out float duration)
    {
        duration = 0f;
        // A wait belongs to exactly the event that authored it. Cleared here rather than after
        // the runner reads it, so a flagged call that installs nothing (its callee never
        // resolved) cannot be answered by the previous call's still-live test.
        _pendingWait = null;
        switch (ev.Kind)
        {
            case "ObjectActiveState":
                // ⚠ Test for a SOUND_NODE emitter first. An ordinary OBJECT_ACTIVE_STATE switches
                // one on, and its name is a sounds.json definition rather than a gamez node, so
                // falling through to Targets() books it as an unresolved op.
                if (SoundEmitter(ev, anchor) is { } emitterHandle)
                {
                    Sounds!.SetActive(emitterHandle, ev.Data.Bool("state"));
                    _opsApplied++;
                    return true;
                }
                foreach (var t in Targets(ev, def, anchor))
                {
                    bool active = ev.Data.Bool("state");
                    SetSubtreeActive(t, active);
                    if (!active)
                        // A played deactivation spares an emitter started in this same
                        // instant (the splash idiom writes both halves and means the second); the
                        // RESET_STATE path does not, being base state where the last write wins.
                        Emitters.EndOn(t, sparingSameInstant: !instant);
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
                            Motions.Add(tween, def, anchor);
                        _opsApplied++;
                    }
                    duration = instant ? 0f : runTime;
                    return true;
                }

            case "ObjectOpacityState":
                {
                    // ⚠ Read `state` as "translucency enabled", never as visibility: false means
                    // render normally, not disappear (docs/formats/anim-definitions.md). Hiding is
                    // OBJECT_ACTIVE_STATE's job, and the data uses it right alongside this.
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
                    // ⚠ Do not let the endpoint `state` flag invert the value here, as it does on
                    // OBJECT_OPACITY_STATE; this is a literal lerp of the two opacity numbers.
                    // `opacity_delta` never ships a value, so report one rather than ignoring it.
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
                            Motions.Add(fade, def, anchor);
                        _opsApplied++;
                    }
                    duration = instant ? 0f : runTime;
                    return true;
                }

            case "ObjectMotion":
                {
                    // OBJECT_MOTION spans two jobs (docs/org/objectMotion.md): a rotation-only event
                    // is a steady spin, the shape every ON_STARTUP event takes; the rest pair
                    // motion with GRAVITY/TRANSLATION/SCALE/FORWARD_ROTATION for ballistic debris.
                    bool hasBallistic = ev.Data.Has("translation") || ev.Data.Has("translation_range")
                                        || ev.Data.Has("scale") || ev.Data.Has("forward_rotation");
                    if (hasBallistic)
                    {
                        // The full rigid-body simulation: a ballistic translate/launch, a scale ramp
                        // and a tumble (plus any steady XYZ_ROTATION), all on one node over run_time.
                        // See MotionRuntime for the semantics and the TUNE caveats.
                        float authored = ev.Data.Num("run_time") ?? 0f;
                        // ⚠ Read the flight back off each body rather than assuming it here; a
                        // launch with no authored time solves its own from the parabola it drew.
                        // The longest of them is what the sequence waits on.
                        float ballTime = authored;
                        bool bounceArmed = false;
                        // Once per event, not per target: every target of one event shares the def.
                        bool inheritVelocity = InheritedVelocityExempt == null
                            || !InheritedVelocityExempt.Contains(def.AnimName ?? def.Name);
                        foreach (var t in Targets(ev, def, anchor))
                        {
                            var motion = MotionRuntime.Create(this, t, ev.Data, authored, inheritVelocity);
                            if (motion == null)
                                continue;
                            float flight = motion.RunTime;
                            // ⚠ A body runs if it has a duration OR a contact tier to end it.
                            // Dropping the second test poses at rest every fall with no apex to
                            // solve, a shot-down zeppelin among them. Either reports 0 anyway.
                            if (instant || (flight <= 0f && !motion.TestsContact))
                            {
                                motion.Seek(0f); // RESET_STATE / zero-length: pose the launch start (rest)
                            }
                            else
                            {
                                Motions.Add(motion, def, anchor); // MotionSet.Add counts the launch
                                ballTime = Mathf.Max(ballTime, flight);
                                // A contact-tested body arms its bounce at contact, since the struck
                                // surface picks the branch; the test being on is enough here.
                                bounceArmed |= motion.PendingBounce != null || motion.TestsContact;
                            }
                            _opsApplied++;
                        }
                        // ⚠ Never file an ARMED bounce as unhandled; TickMotions dispatches it when
                        // the body lands, and counting it reports a working feature as a missing
                        // one. Only a fall that never arms is deferred (docs/org/objectMotion.md).
                        if (ev.Data.Has("bounce_sequence") && !bounceArmed)
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
                    // ⚠ Report `delta` rather than guessing at it. The data does not settle whether
                    // it is acceleration, a decelerating ramp or a random spread, and all but one
                    // reachable event leaves it zero.
                    if (!spin.Vec3("delta").IsZeroApprox())
                        Count("ObjectMotion(rotation delta)");
                    if (rate.IsZeroApprox())
                        return true;

                    float spinFor = ev.Data.Num("run_time") ?? 0f;
                    foreach (var t in Targets(ev, def, anchor))
                    {
                        // ⚠ Keep re-assertion idempotent. These sit in `Loop{-1}` sequences, and a
                        // rebuilt spin re-reads rest from the current pose and restarts its clock,
                        // so the prop sits almost still while the logs show it driven.
                        if (Motions.HasSpinOn(t, rate, spinFor))
                            continue;
                        var motion = new SpinMotion(t, rate, spinFor);
                        if (instant)
                            motion.Seek(0f); // RESET_STATE poses the start; a spin starts unturned
                        else
                            Motions.Add(motion, def, anchor);
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
                            Motions.Add(playback, def, anchor);
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
                // ⚠ Halt the named sequence's active runners and nothing else. Halting never
                // retracts what the sequence already launched; those lifetimes are authored
                // independently (docs/formats/anim-definitions.md).
                if (ev.Data.Str("name") is { } stopName)
                    StopSequence(def, anchor, stopName);
                return true;

            case "CallAnimation":
                // ⚠ A call must not restart an animation already live on this anchor. The data's
                // poll idiom re-issues the call every frame its condition holds, and restarting
                // pins a door at its first frame for as long as the player hovers.
                if (ev.Data.Str("name") is { } callName)
                {
                    // ⚠ Honour the call's own target node. That re-anchoring is the data's
                    // template-instancing mechanism, and ignoring it runs every call site on the
                    // CALLER's anchor instead of where the data put it.
                    var (siteNode, siteOffset) = CallTargetSite(ev, def, anchor);
                    var callAnchor = siteNode ?? anchor;
                    // The world runtime cannot render an effect template, its puffer factory being
                    // torn down after the build, so a death's effect call goes to the world-effects
                    // runtime instead and the local Start that would build nothing is skipped.
                    if (ExternalEffect != null && callAnchor != null && IsInstanceValid(callAnchor))
                    {
                        var siteXform = callAnchor.GlobalTransform;
                        // ⚠ Use VisualOriginOf, not the raw origin; an absolute-modelled target's
                        // node origin is the map corner. The site node rides along as the callee's
                        // INPUT_NODE, which defs whose lifecycle reads that node need.
                        if (ExternalEffect(callName, VisualOriginOf(callAnchor) + siteXform.Basis * siteOffset, siteNode))
                        {
                            // ⚠ Count the dropped hold rather than passing over it. A routed call
                            // leaves no instance here to wait on, and this is the one scope
                            // boundary the wait has.
                            if (ev.WaitsForCompletion && !instant)
                                NoteRoutedWait(callName);
                            return true;
                        }
                    }
                    // ⚠ An OPERAND_NODE call must never relocate or lazily build the callee's own
                    // root. It redirects the callee's node resolution onto the CALLER's subtree, so
                    // a just-built placeholder outranks that and the wreck goes undriven.
                    bool operandRedirect = ev.Data.Str("operand_node") != null;
                    // ⚠ Collect the wait set as the loop resolves it, never re-derive it after.
                    // ResolveLibraryRoot takes a pool slot, so a second ask hands the wait a
                    // different copy from the one running.
                    List<(AnimDefinition Def, Node3D? Anchor)>? waitOn =
                        ev.WaitsForCompletion && !instant ? new() : null;
                    foreach (var target in _program.ByAnimName(callName))
                    {
                        // ⚠ Keep the library-root test data-driven, never name-based, and gated on
                        // a non-instant death call. Most LOCAL_CHOREOGRAPHY targets sit at an
                        // authored position, and the ambient boot must relocate nothing.
                        Node3D? libraryCopy = null;
                        bool relocate = false;
                        if (!instant && callAnchor != null)
                        {
                            if (_templateStage.Places)
                            {
                                relocate = true;
                            }
                            else if (_deathCallDepth > 0 && !operandRedirect && ResolveLibraryRoot != null)
                            {
                                libraryCopy = ResolveLibraryRoot(
                                    string.IsNullOrEmpty(target.Name) ? target.RootName ?? "" : target.Name,
                                    callAnchor);
                                relocate = libraryCopy != null;
                            }
                        }
                        // ⚠ A pooled copy must BE Start's anchor, not the call site. Its symbol
                        // lookup misses the by-index map and falls to the name rescue scoped to its
                        // own subtree, which finds nothing unless the copy is the anchor.
                        var startAnchor = libraryCopy ?? callAnchor;
                        // Added whether or not the Start below fires: "wait until it completes"
                        // is about the named animation, not about which call started it.
                        waitOn?.Add((target, startAnchor));
                        // A relocating call anchored outside the pool claims its sticky slot before
                        // the placed-where test below asks for this call's copy. The library-copy
                        // path has its own pool, and a call on a pooled copy already has a slot.
                        if (relocate && libraryCopy == null)
                            _templateStage.AssignCallerSlot(target, callAnchor!);
                        Vector3 wantSite = default;
                        bool movedAway = false;
                        if (relocate)
                        {
                            wantSite = callAnchor!.GlobalTransform.Origin + callAnchor.GlobalTransform.Basis * siteOffset;
                            // ⚠ A placed template called at a DIFFERENT site restarts even while
                            // live; one copy can only be in one place, so the live guard would
                            // otherwise give the second call no effect at all.
                            movedAway = libraryCopy != null
                                ? libraryCopy.GlobalTransform.Origin.DistanceSquaredTo(wantSite)
                                  > TemplateStage<Node3D>.MoveToleranceSq
                                : !_templateStage.IsAt(target, callAnchor, siteOffset);
                        }
                        // Gated on the site actually having moved, so the data's poll idiom
                        // (`If … CallAnimation; Endif; Loop{-1}`) keeps hitting the guard and does
                        // not restart its callee every frame.
                        if (!IsLive(target, startAnchor) || movedAway)
                        {
                            // ⚠ Move the root, not just the anchor. An effect's puffers ride the
                            // template's own root, so re-anchoring alone emits at the gamez origin.
                            if (relocate)
                            {
                                if (libraryCopy != null)
                                    _templateStage.PlaceOn(new[] { (Node3D?)libraryCopy }, wantSite);
                                else
                                    _templateStage.PlaceAt(target, callAnchor!, siteOffset);
                            }
                            Start(target, startAnchor);
                            // ⚠ Reveal the mesh too, after Start. On a stage that hides its
                            // templates, an unrevealed callee shows its particles and none of its
                            // authored geometry, because puffers draw at world level.
                            if (relocate)
                                _templateStage.Reveal(target, startAnchor, visible: true);
                            // A relocated death call on the dying instance's own anchor is the
                            // caller's own choreography, so a reset must restore it too. ⚠ Scope
                            // this to the same test as relocation, or a shared template goes too.
                            if (relocate && startAnchor != null && _dyingInstances.Count > 0
                                && ReferenceEquals(_dyingInstances.Peek().Anchor, callAnchor))
                            {
                                _dyingInstances.Peek().LocalCallTargets.Add((target, startAnchor));
                            }
                        }
                    }
                    if (waitOn is { Count: > 0 })
                        InstallWait(callName, waitOn);
                }
                return true;

            case "StopAnimation":
                Stop(ev.Data.Str("name"));
                return true;

            case "InvalidateAnimation":
                // ⚠ Not a stop. It is the "once started, never again" latch every `*_start` route
                // relies on, and treating it as a stop kills the sequence at event 0
                // (docs/formats/anim-definitions.md, and _invalidated).
                Invalidate(ev.Data.Str("name"));
                return true;

            case "ResetAnimation":
                // Clears the invalidation latch and re-poses the def's RESET_STATE, which is what
                // lets a turned-off light loop be turned on again.
                ResetAnimation(ev.Data.Str("name"));
                return true;

            case "PufferState":
                HandlePufferState(ev, def, anchor);
                return true;

            case "LightState":
                HandleLightState(ev, def, anchor);
                return true;

            case "LightAnimation":
                // ⚠ Report the ramp as the event's DURATION so the next step of a pulse chain waits
                // for it. The light is tweened asynchronously here, so reporting 0 fires every step
                // in one instant and an authored flicker collapses to a single frame.
                HandleLightAnimation(ev, anchor, instant);
                // A RESET_STATE lands the delta whole (see the handler), so it takes no time.
                duration = instant ? 0f : ev.Data.Num("run_time") ?? 0f;
                return true;

            case "SoundNode":
                HandleSoundNode(ev, def, anchor);
                return true;

            case "Sound":
                HandleSound(ev, def, anchor);
                return true;

            case "FbfxColorFromTo":
                {
                    // A full-screen wash, linear RGBA from `from` to `to` over `run_time`.
                    // ⚠ Report the run time as this event's duration. The original holds the
                    // sequence for it, and without that a multi-step wash lands in one instant.
                    float runTime = ev.Data.Num("run_time") ?? 0f;
                    duration = runTime;
                    // A wash is a thing that happens, not a pose, so a RESET_STATE applies nothing.
                    if (!instant && ScreenFlash != null)
                    {
                        // Where the burst is and how far its def admits the wash, so the overlay
                        // paints the panes it reached instead of all four.
                        ScreenFlash(Rgba(ev.Data.Obj("from")), Rgba(ev.Data.Obj("to")), runTime,
                            anchor != null ? WorldPos(anchor) : Vector3.Zero,
                            anchor != null ? WashGateRadiusSquared(def) : 0f);
                        _opsApplied++;
                    }
                    return true;
                }

            case "ObjectAddChild":
                // Only the sound-emitter three-quarters of this event is acted on — see
                // HandleAddChild. Everything else it does still counts as unhandled.
                if (!HandleAddChild(ev, def, anchor))
                    Count(ev.Kind);
                return true;

            default:
                // One-shot Sound / opacity / texture-cycle / camera and the
                // rest: dispatched, counted, and reported once per kind. Adding a handler is
                // a case above and nothing else.
                Count(ev.Kind);
                return true;
        }
    }

    // How far from its anchor a definition's own `If PlayerRange` gate admits its wash, in metres
    // SQUARED (the compiled convention both sources normalise to), and 0 for a def that gates on
    // nothing. This is the routing radius for ScreenFlash, and it is the authored one.
    // ⚠ Take the largest gate, not the first, so a def with several cannot route a wash by
    // whichever happens to be listed first.
    private float WashGateRadiusSquared(AnimDefinition def)
    {
        if (_washGates.TryGetValue(def, out float cached))
            return cached;
        float radiusSq = 0f;
        foreach (var seq in def.Sequences)
            foreach (var ev in seq.Events)
            {
                if (ev.Kind is not ("If" or "Elseif"))
                    continue;
                if (ev.Data.Obj("condition")?.Num("PlayerRange") is { } r && r > radiusSq)
                    radiusSq = r;
            }
        _washGates[def] = radiusSq;
        return radiusSq;
    }

    // An {r,g,b,a} sub-object as a colour; absent → transparent black.
    private Color Rgba(AnimData? d) => d == null
        ? new Color(0f, 0f, 0f, 0f)
        : new Color(d.Num("r") ?? 0f, d.Num("g") ?? 0f, d.Num("b") ?? 0f, d.Num("a") ?? 0f);

    private void HandlePufferState(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        if (ev.Data.Str("name") is not { } pufferName)
            return;
        // ACTIVE_STATE: 1 = start emitting, 0 = stop. The attach point is AT_NODE; note it is
        // NOT the event's "name" (that is the puffer's own name, a different namespace).
        bool on = (ev.Data.Num("active_state") ?? 0f) >= 1f;
        // AT_NODE INPUT_NODE / MAIN_ROOT_NODE mean "the node this def was invoked on", so a
        // destruction fire emits on the effect's own relocated root and lands at the hit site.
        var host = ev.Data.Str("at_node") is { } atNode
            ? (IsSelfNodeRef(atNode) ? InputNodeOf(def, anchor) ?? anchor : Resolve(atNode, def, anchor))
            : anchor;
        if (host == null)
        {
            Count("PufferState(no host node)");
            return;
        }

        if (!on)
        {
            Emitters.End(pufferName, host, def, anchor);
            return;
        }
        // _opsApplied counts APPLIED ops, and only a build is one: a re-assert of a running
        // emitter is a no-op and a miss is already counted by name. Read off the director's own
        // total rather than re-deriving the verdict here.
        int built = Emitters.Built;
        Emitters.Assert(pufferName, host, def, anchor, ev.Data);
        if (Emitters.Built != built)
            _opsApplied++;
    }

    // The stored call-site node for a live instance, when it is still valid.
    private Node3D? InputNodeOf(AnimDefinition def, Node3D? anchor) =>
        _inputNodes.TryGetValue((def, anchor), out var node) && IsInstanceValid(node) ? node : null;

    // Whether any of the def's sequences conditions on the INPUT_NODE sentinel's active state — the
    // authored "run while my host stands" lifecycle (the damage-stage sputters' If NodeActive →
    // Loop). Cached; the answer is a property of the data.
    private bool DefConditionsOnInputNode(AnimDefinition def)
    {
        if (_inputGoverned.TryGetValue(def, out bool cached))
            return cached;
        bool governed = def.Sequences.Any(s => s.Events.Any(ev =>
            (ev.Kind == "If" || ev.Kind == "Elseif")
            && ev.Data.Obj("condition")?.Union() is { Tag: "NodeActive" } union
            && AnimData.AsNum(union.Value) is { } idx && idx > 1e9f));
        _inputGoverned[def] = governed;
        return governed;
    }

    private void ReportLateSoundFailure(string name, string why, string kind = "SOUND_NODE")
    {
        if (!_soundCensusPrinted || !_soundFailuresReported.Add(name))
        {
            return;
        }
        GD.PushWarning($"anim: {kind} '{name}' requested after the world build and {why} — "
                       + "it will be silent for the rest of the session");
    }

    // The emitter an event's NAME refers to, or null when the name isn't one this definition
    // declared. This is what lets OBJECT_ACTIVE_STATE and OBJECT_ADD_CHILD — both perfectly
    // ordinary node events elsewhere — address a sound emitter without either handler having to
    // guess from the name whether `snd_waterfall` is a node or a sound.
    private object? SoundEmitter(AnimEvent ev, Node3D? anchor)
    {
        if (Sounds == null || ev.Data.Str("name") is not { } name)
            return null;
        return _soundEmitters.TryGetValue((name, anchor), out var handle) ? handle : null;
    }

    // Declares (and for the compiled form, places and starts) one ambient emitter. The two
    // front-ends spell it differently and both land here: a reader def writes a three-event triple
    // where this event only declares, while a compiled event carries active_state and translate
    // inline, but only on the events that do not leave them to OBJECT_ADD_CHILD
    // (docs/formats/anim-definitions.md).
    private void HandleSoundNode(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        if (SoundHandledElsewhere)
            return;
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
            if (atData.Str("name") is { } hostName && Resolve(hostName, def, anchor) is { } host)
                Sounds.Attach(handle, host, atData.Vec3("pos"));
        }
        // ⚠ An absent active_state means "leave it alone", never OFF; the reader form's ACTIVE
        // arrives as the next event. ⚠ The compiled field is a JSON boolean here, where
        // PUFFER_STATE's same-named field is numeric, so Num() alone switches every emitter off.
        if (ev.Data.Has("active_state"))
            Sounds.SetActive(handle, ev.Data.Bool("active_state") || ev.Data.Num("active_state") >= 1f);
    }

    // A one-shot SOUND event: the destruction, impact and damage audio a sequence emits. Unlike
    // SOUND_NODE's pooled looping emitters it plays once at a world point and disposes itself.
    // ⚠ The event's NAME is a sounds.json definition or a SOUND_GROUPS name, never a gamez node.
    // The AT_NODE, when present, positions it; absent, it plays at the anchor.
    private void HandleSound(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        if (SoundHandledElsewhere)
        {
            return;
        }
        if (ev.Data.Str("name") is not { } name)
        {
            return;
        }
        if (Sounds == null)
        {
            _soundsAfterBuild++;
            ReportLateSoundFailure(name, "there is no audio session", "SOUND");
            return;
        }
        if (Sounds.PlayOneShot(name, OneShotSoundPosition(ev, def, anchor), _rng) != null)
        {
            OneShotSoundsPlayed++;
            _opsApplied++;
        }
        else
        {
            _soundsUnknown++;
            ReportLateSoundFailure(name, "no stream could be resolved for it (unknown to "
                                         + "sounds.json / SOUND_GROUPS, or never prewarmed)", "SOUND");
        }
    }

    // Where a one-shot SOUND plays: its AT_NODE's world pose plus the trailing offset, or the
    // anchor's when it names no node. The compiled form nests AT_NODE as {name, pos}; the reader
    // form (normalized in AnimDefs) carries a flat at_node name plus a translate offset.
    private Vector3 OneShotSoundPosition(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        Node3D? host = null;
        Vector3 offset = Vector3.Zero;
        if (ev.Data.Obj("at_node") is { } atObj)
        {
            if (atObj.Str("name") is { } hostName)
            {
                host = Resolve(hostName, def, anchor);
            }
            offset = atObj.Vec3("pos");
        }
        else if (ev.Data.Str("at_node") is { } atName)
        {
            host = Resolve(atName, def, anchor);
            offset = ev.Data.Vec3("translate");
        }
        host ??= anchor;
        if (host is not { } h || !IsInstanceValid(h))
            return Vector3.Zero;
        var pos = WorldTransform(h, out bool composed) * offset;
        if (composed)
            Log.Info("sound", $"one-shot SOUND '{ev.Data.Str("name")}' positioned by out-of-tree ancestor composition at {pos} (world root not parented at bootstrap)");
        return pos;
    }

    // The sound-emitter case of OBJECT_ADD_CHILD: attach a declared emitter to the world node that
    // positions it. Returns false for every other use, which stays counted as unhandled.
    // Deliberately only the sound subset; most of the event's other uses are cutscene machinery for
    // cutscenes this project does not have (docs/formats/anim-definitions.md).
    private bool HandleAddChild(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        if (Sounds == null || ev.Data.Str("child") is not { } child)
            return false;
        if (!_soundEmitters.TryGetValue((child, anchor), out var handle))
            return false;
        if (ev.Data.Str("parent") is not { } parentName)
            return false;
        if (Resolve(parentName, def, anchor) is not { } host)
            return false;
        Sounds.Attach(handle, host);
        _opsApplied++;
        return true;
    }

    // Applies one LIGHT_STATE. ⚠ Treat it as a PARTIAL update: apply every field only when present
    // and never default an absent one. A flicker is a stream of {name, range} events a few
    // hundredths of a second apart that must leave position, colour and active state untouched.
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
            // ⚠ Resolve the host once per light, never per event. A flicker re-issues its full
            // LIGHT_STATE every loop iteration, and the full-world scan behind an unmemoized
            // Resolve cost tens of milliseconds a frame.
            if (at.Str("name") is { } hostName)
            {
                if (light.Host == null || !string.Equals(hostName, light.HostName, StringComparison.Ordinal))
                {
                    if (Resolve(hostName, def, anchor) is { } host)
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

    // Applies one LIGHT_ANIMATION: signed deltas to a light's range and colour, ramped over
    // run_time. ⚠ They are deltas, never targets; a pulse authors a negative range on its way back,
    // which is not a value a light can hold. Under `instant` the delta lands whole, matching how
    // timed motions collapse to their end pose there.
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

    // Advances light tweens and submits every active light at its host's current world pose. Per
    // frame, because hosts move (a muzzle flash rides its turret).
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
            // ⚠ A light inside a deactivated subtree is off. A building's destroyed variant must
            // not keep lighting the ground through its healthy twin.
            if (light.Host is not { } host || !IsInstanceValid(host) || !host.IsVisibleInTree())
                continue;
            Lights.Add(host.GlobalTransform * light.Offset, light.Color, light.RangeMin, light.RangeMax);
        }
        var viewers = LightViewerPositions?.Invoke();
        Lights.Commit(viewers != null && viewers.Count > 0 ? viewers : new[] { PlayerPos() });
        if (DebugMotions)
            Lights.LogOnce();
    }

    // Resolves a single node name for this definition — the compiled symbol table first, then the
    // scoped tier chain. Forwards to NameResolver.Resolve, which owns the tier order (anchor
    // subtree, own template roots, global).
    private Node3D? Resolve(string name, AnimDefinition def, Node3D? anchor) =>
        _resolver.Resolve(name, def, anchor);

    // Resolves a name path for one definition, narrowest scope first. The tier order — call
    // anchor's subtree, then the DEFINITION'S OWN template root(s), then unless LOCAL_NODES_ONLY
    // the whole index — is resolver-owned and structural (see NameResolver.ResolveScoped):
    // this class holds no resolution primitive it could compose in a different order.
    private List<Node3D> ResolveScoped(List<string> path, AnimDefinition def, Node3D? anchor) =>
        _resolver.ResolveScoped(path, def, anchor);

    // The site a CALL_ANIMATION hands its callee: the target node plus the AT_NODE trailing offset
    // in that node's frame. Null when the call names no target, and the caller's anchor stands.
    // ⚠ Resolve the target in the CALLER's namespace; that is where it is written. A
    // named-but-unresolvable target falls back to the caller's anchor rather than dropping the
    // call, and is counted, because silently mis-placing an effect is what this method prevents.
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

        var resolved = Resolve(targetName, def, anchor);
        if (resolved != null)
            _retargeted++;
        else
            _retargetUnresolved++;
        // Once per distinct (callee, target, caller) triple: the poll idiom re-issues its calls
        // every frame, so an unconditional line here would bury the log.
        if (DebugMotions && _retargetsLogged.Add($"{ev.Data.Str("name")}|{targetName}|{def.AnimName}"))
            GD.Print($"anim: retarget '{ev.Data.Str("name")}' onto '{targetName}' "
                     + $"({(resolved != null ? resolved.GetMeta(NameMeta).AsString() : "UNRESOLVED")})"
                     + $" [caller {def.AnimName}]");
        return (resolved, offset);
    }

    // Whether def's placed root should level to world axes (LevelPlacedTemplateNames) rather than
    // inherit its caller's rotation.
    private bool LevelsTemplate(AnimDefinition def) =>
        LevelPlacedTemplateNames != null && LevelPlacedTemplateNames.Contains(def.AnimName ?? def.Name);

    // Whether any live motion is still driving something inside a template copy: the hold that
    // keeps a finished effect's root revealed, since a reveal is paired with the EFFECT's life and
    // not its instance's.
    // ⚠ Ask this of the ROOT, never of the def that just finished; a template's pieces are
    // routinely driven by a callee's motions, so a def-scoped hold hides the copy out from under
    // them. ⚠ Exclude an unbounded spin, which would pin the template revealed for the session.
    private bool TemplateStillAnimated(IReadOnlyList<Node3D?> roots)
    {
        foreach (var motion in Motions.Live)
        {
            if (motion is SpinMotion { Endless: true } || !IsInstanceValid(motion.Target))
                continue;
            foreach (var root in roots)
                if (root != null && (root == motion.Target || root.IsAncestorOf(motion.Target)))
                    return true;
        }

        return false;
    }

    private void SweepEffectTtls(float dt)
    {
        if (_effectTtls.Count == 0)
            return;
        _effectClock += dt;
        for (int i = _effectTtls.Count - 1; i >= 0; i--)
        {
            if (_effectClock < _effectTtls[i].Deadline)
                continue;
            var (def, anchor, _) = _effectTtls[i];
            _effectTtls.RemoveAt(i);
            // Stop, not just drop the instance, so a stop-less emitter stops emitting.
            Stop(def.AnimName, anchor);
            // ⚠ Keep the direct teardown too. Stop reaches resources only through a live instance,
            // and an effect whose sequences already ended has none, which would make this sweep a
            // silent no-op for every effect that outlives only its own emitters.
            TearDownResourcesOf(def, anchor);
        }
    }

    // Whether a finished instance may actually be retired — an instance-retirement question that
    // consults the motions, which is why it stays here rather than moving with them. The narrowness
    // of the hold, and the measurement behind it, are on OwesBounce.
    private bool Retirable(AnimInstance inst) =>
        inst.Finished && !Motions.OwesBounce(inst.Def, inst.Anchor);

    // Both sequence events act on the live instance of (def, anchor); on the instant/bootstrap
    // dispatch path no instance exists and both are no-ops.
    private AnimInstance? InstanceOf(AnimDefinition def, Node3D? anchor) =>
        _instances.FirstOrDefault(i => i.Def == def && i.Anchor == anchor);

    private void CallSequence(AnimDefinition def, Node3D? anchor, string name)
    {
        if (InstanceOf(def, anchor) is not { } inst)
            return;
        if (!inst.CallSequence(name))
            Count("CallSequence(missing)");
    }

    private void StopSequence(AnimDefinition def, Node3D? anchor, string name)
    {
        if (InstanceOf(def, anchor) is not { } inst)
            return;
        if (!inst.StopSequence(name))
            Count("StopSequence(missing)");
    }

    // Evaluates one IF/ELSEIF condition; every kind the data uses is answerable, and semantics per
    // kind are in docs/formats/anim-definitions.md.
    // ⚠ Two units bite: compiled PlayerRange is metres SQUARED, and compiled AnimHealth is a
    // "damaged down to" threshold, so an undamaged object fails it.
    // ⚠ An unparseable or unknown condition returns false. Skipping a branch is the safe
    // direction; a branch that should not have run poses objects wrongly.
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
            // The exe's roll is inclusive (`draw <= threshold`); NextDouble() is [0,1) so <=
            // matches it exactly (a strict < would silently exclude the num==0 case's draw==0.0).
            "RandomWeight" => _rng.NextDouble() <= num,
            "AnimationLod" => QualityLod >= (int)num,
            // The 4-byte value slot is unused for these two: the reader form takes no
            // argument (`IF HW_RENDER`) and every compiled instance stores 0, so the
            // condition is the runtime flag itself.
            "HwRender" => true,
            "PlayerFirstPerson" => FirstPerson,
            "PlayerRange" => anchor != null
                             && NearestPlayerDistanceSquared(WorldPos(anchor)) <= num,
            // ANIM_HEALTH gates damage effects: "if this object has been worn down to N".
            // Read against the LIVE per-instance HP, not the def's authored value, so a
            // tower damaged to 30 smokes while its undamaged siblings do not.
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
        // --debug-anim: each condition on first evaluation and thereafter only when its verdict
        // flips. The poll idiom re-evaluates every frame, so logging each one buries the log.
        if (DebugMotions && Flipped(kind, anchor, result))
        {
            var at = anchor == null ? "<global>"
                : $"{NameOf(anchor)} {WorldPos(anchor).Snapped(Vector3.One)}";
            GD.Print($"anim/debug: cond {kind}({Describe(value)}) on {at} " +
                     $"[player {PlayerPos().Snapped(Vector3.One)}] → {(result ? "TRUE" : "false")}");
        }
        return result;
    }

    // The live HP an ANIM_HEALTH threshold tests against: the registered destructible instance for
    // this (def, anchor) pair, falling back to the def's authored value when the pair is not a
    // registered destructible (an unanchored evaluation, or a def whose NAME resolved nothing at
    // bootstrap). The fallback is the def's own authored constant, so anything the registry does
    // not cover reads a fixed value.
    private float HealthOf(AnimDefinition def, Node3D? anchor) =>
        _destructibles.Get(def, anchor)?.Health ?? def.Health;

    // Restores every node a def's events touched to its authored rest pose (_rest, recorded the
    // first time a motion disturbed it). The membership check confines this to nodes that actually
    // MOVED — the ballistic debris and any FROM_TO movers — so healthy/destroyed visibility nodes
    // (never transformed) are left to RESET_STATE.
    private void RestoreRestPoses(AnimDefinition def, Node3D? anchor)
    {
        var seqs = def.ResetState != null ? def.Sequences.Append(def.ResetState) : def.Sequences;
        foreach (var seq in seqs)
        {
            foreach (var ev in seq.Events)
            {
                foreach (var node in Targets(ev, def, anchor))
                {
                    if (_rest.TryGetValue(node, out var rest))
                    {
                        node.Transform = rest;
                    }
                }
            }
        }
    }

    // Runs a destructible's death the instant its HP reaches zero.
    // ⚠ Play ALL the def's Initial sequences through Start; never try to pick "the death sequence"
    // out by name. The swap sits in a sequence whose name varies and is only reliably Initial
    // (docs/formats/destructibles.md).
    // ⚠ Withhold the ApplyDeathSwap fallback when the swap is one CALL_ANIMATION down, or when the
    // def authors its own visible death; firing it blanks a wreck early or swaps an archway.
    private void RunDeathSequence(DestructibleRegistry.Instance inst)
    {
        _deathCallDepth++;
        _dyingInstances.Push(inst);
        try
        {
            // The same span _deathCallDepth brackets as the whole burst.
            using (PerfSample.Scope(PerfSite.DebrisSpawn))
            {
                Start(inst.Def, inst.Anchor, protectSelfInvalidate: true);
                RunDeathSlot(inst.Def, inst.Anchor);
            }
        }
        finally
        {
            _dyingInstances.Pop();
            _deathCallDepth--;
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

    // Dispatches the def's compiled destruction slot alongside the Initial sequences Start just
    // ran; it carries most of the install's death calls, which the listed sequences never reach.
    // ⚠ Run it as a runner ON the live instance, so its timed events advance with the death and a
    // reset's Stop tears it down too. ⚠ Keep the zero advance inside the death bracket and under
    // Start's self-invalidate protection; slots use the consume-the-trigger idiom.
    private void RunDeathSlot(AnimDefinition def, Node3D? anchor)
    {
        if (def.DeathSlot is not { } slot)
            return;
        var live = InstanceOf(def, anchor);
        if (live == null)
        {
            live = new AnimInstance(def, anchor);
            _instances.Add(live);
            OnInstanceStarted?.Invoke(def, anchor);
        }
        live.AddRunner(slot);
        _startDepth++;
        _startingInstances.Push((live, true));
        try
        {
            live.Advance(this, 0f);
        }
        finally
        {
            _startingInstances.Pop();
            _startDepth--;
        }
        // A slot that drained in the zero advance is retired by the next Advance sweep, which
        // runs the full finish path (emitters, TTLs, template hides) — nothing special here.
    }

    // The first CALL_ANIMATION target, one level down from def's own Initial sequences, whose OWN
    // sequences author the healthy/destroyed swap — an OBJECT_ACTIVE_STATE that activates a
    // destroyed/dbase-role node or deactivates a healthy-role one. Resolved via the same
    // _program.ByAnimName the CALL_ANIMATION dispatch itself uses. Null for the ~90%/~10% cases the
    // def's own sequences/RESET_STATE already cover (gate1, the AA guns, everything else).
    private AnimDefinition? ChainedSwapTarget(AnimDefinition def)
    {
        foreach (var seq in def.Sequences.Where(s => !s.OnCallOnly))
            foreach (var ev in seq.Events)
                if (ev.Kind == "CallAnimation" && ev.Data.Str("name") is { } callName)
                    foreach (var target in _program.ByAnimName(callName))
                        if (AuthorsSwap(target))
                            return target;
        return null;
    }

    // The generic healthy-to-destroyed swap, for destructibles that declare the pair but author no
    // explicit swap sequence. ⚠ Read it off the def's own RESET_STATE targets, never a world-wide
    // name scan, and apply it only when RESET names a destroyed node; an object with no destroyed
    // variant must be left intact rather than blanked. Match the exact role words, not a suffix.
    private void ApplyDeathSwap(DestructibleRegistry.Instance inst)
    {
        if (inst.Def.ResetState is not { } reset)
            return;
        bool hasDestroyed = reset.Events.Any(ev => ev.Kind == "ObjectActiveState"
            && RoleName(ev).Contains("destroyed", StringComparison.OrdinalIgnoreCase));
        if (!hasDestroyed)
            return;
        foreach (var ev in reset.Events)
        {
            if (ev.Kind != "ObjectActiveState")
                continue;
            var name = RoleName(ev);
            bool? active =
                name.Contains("healthy", StringComparison.OrdinalIgnoreCase) ? false
                : name.Contains("destroyed", StringComparison.OrdinalIgnoreCase)
                  || name.Contains("dbase", StringComparison.OrdinalIgnoreCase) ? true
                : null;
            if (active is not { } state)
                continue;
            foreach (var node in Targets(ev, inst.Def, inst.Anchor))
                SetSubtreeActive(node, state);
        }
    }

    private bool Flipped(string kind, Node3D? anchor, bool result)
    {
        var key = (kind, anchor);
        if (_condLast.TryGetValue(key, out bool prev) && prev == result)
            return false;
        _condLast[key] = result;
        return true;
    }

    private Vector3 PlayerPos()
    {
        if (PlayerPosition != null)
            return PlayerPosition();
        return IsInsideTree() && GetViewport().GetCamera3D() is { } cam ? cam.GlobalPosition : Vector3.Zero;
    }

    // The nearest human's squared distance to a world point — a PLAYER_RANGE condition's own answer
    // (`BL-365`), the same nearest-of-every-player rule TickDeferredByRange's EXECUTION_BY_RANGE
    // gate already uses, so a wash or door gated by a burst near player 4 fires even while player 1
    // sits kilometres off. Falls back to PlayerPos when no PlayerPositions seam is wired.
    private float NearestPlayerDistanceSquared(Vector3 point)
    {
        if (PlayerPositions?.Invoke() is { Count: > 0 } positions)
        {
            float d2 = float.MaxValue;
            foreach (var p in positions)
                d2 = Mathf.Min(d2, point.DistanceSquaredTo(p));
            return d2;
        }
        return point.DistanceSquaredTo(PlayerPos());
    }

    private Node3D? ConditionNode(object? reference, AnimDefinition def, Node3D? anchor)
    {
        if (reference is string name)
            return IsSelfNodeRef(name) ? anchor : Resolve(name, def, anchor);
        if (AnimData.AsNum(reference) is not { } idx)
            return null;
        if (idx > 1e9f)
            return InputNodeOf(def, anchor) ?? anchor; // negative sentinel = INPUT_NODE
        int i = (int)idx;
        if (i < 1 || i > def.NodeList.Count)
            return null;
        return Resolve(def.NodeList[i - 1], def, anchor);
    }

    private void CountCondition(string kind, bool result)
    {
        var (t, f) = _conditions.TryGetValue(kind, out var c) ? c : (0, 0);
        _conditions[kind] = result ? (t + 1, f) : (t, f + 1);
    }

    private void ReportRetargets()
    {
        if (_retargeted == 0 && _retargetUnresolved == 0)
            return;
        GD.Print($"anim: {_retargeted} call(s) retargeted onto a named node"
                 + (_retargetUnresolved > 0 ? $", {_retargetUnresolved} target(s) unresolved" : ""));
    }

    // The WAIT_FOR_COMPLETION holds armed so far, by callee, printed with the bootstrap census.
    // Anything armed later is outside this print by construction; a probe reads WaitsInstalled
    // instead, and an abandoned hold reports itself where it happens.
    private void ReportWaits()
    {
        if (_waitsByCallee.Count == 0)
            return;
        var parts = _waitsByCallee.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} ×{kv.Value}");
        GD.Print($"anim: {_waitsInstalled} WAIT_FOR_COMPLETION hold(s) armed during bootstrap: "
                 + string.Join(", ", parts));
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

    // --debug-anim: one line per live motion per second. Headless verification that things
    // actually move (and by how much) without flying a camera at them.
    private void LogMotions(float dt)
    {
        _debugClock += dt;
        if (_debugClock < 1f)
            return;
        _debugClock = 0f;
        // ⚠ Split the contact tally by tier. All clock and no contact is the one outcome every
        // other line reports exactly as a working test, and one tier can carry the total alone.
        if (Motions.ContactLandings + Motions.ClockEndings > 0)
        {
            GD.Print($"anim/debug: contact-tested bodies ended: {Motions.ContactLandings} by contact, "
                     + $"{Motions.ClockEndings} on their run time"
                     + $" (column {Motions.ColumnLandings}/{Motions.ColumnClockEndings},"
                     + $" sweep {Motions.SweepLandings}/{Motions.SweepClockEndings})"
                     + (Motions.ContactLandings == 0 ? " — NO CONTACT AT ALL (is a mask wired?)" : ""));
        }

        var emitting = Emitters.Census.Where(r => r.Emitting).ToList();
        if (emitting.Count > 0)
        {
            int live = 0;
            foreach (var r in emitting)
                live += r.LiveParticles;
            // Name them: several runtimes print this line, so a bare count cannot say whose
            // emitters are running.
            GD.Print($"anim/debug: {emitting.Count} active puffer(s), {live} live particle(s)"
                     + $": {string.Join(", ", emitting.Select(r => r.Name).Distinct())}");
        }
        // ⚠ Totals before the list, which is capped at 12. A debris piece is routinely past the
        // cap, so reading "it never launched" out of the truncated list is unsound.
        GD.Print($"anim/debug: {Motions.Count} live motion(s), "
                 + $"{BallisticMotionsLaunched} ballistic launch(es) so far");
        if (Motions.Count == 0)
        {
            return;
        }
        foreach (var m in Motions.Live.Take(12))
        {
            var t = m.Target;
            var name = t.HasMeta(NameMeta) ? t.GetMeta(NameMeta).AsString() : t.Name.ToString();
            var p = t.GlobalPosition;
            // Rotation as well as position: a spin turns a prop in place, so a position-only line
            // reads the same every second whether or not it is running.
            var r = t.Transform.Basis.GetEuler() * (180f / Mathf.Pi);
            // Visibility matters as much as position here: a correctly-animated node inside a
            // subtree the mission deactivated moves perfectly and renders nothing.
            bool shown = t.IsVisibleInTree();
            GD.Print($"anim/debug: {name} at ({p.X:0.0}, {p.Y:0.0}, {p.Z:0.0}) "
                     + $"rot ({r.X:0.0}, {r.Y:0.0}, {r.Z:0.0}) {(shown ? "visible" : "HIDDEN")}");
        }
        if (Motions.Count > 12)
            GD.Print($"anim/debug: … and {Motions.Count - 12} more");
    }

    // Advances every live motion, then dispatches whatever landed. ⚠ The two halves stay in one
    // method, called from one statement in Advance, because the instance walk must not run between
    // them: an instance whose only hold is a landed piece would be Finished with nothing owed, so
    // it retires and FinishEffectInstance SustainEnds the piece's trail emitter mid-flight.
    private void TickMotions(float dt)
    {
        foreach (var landing in Motions.Tick(dt))
        {
            // ⚠ Count the miss here. A landing can outlive its own instance, and CallSequence then
            // has nothing to dispatch into and returns silently.
            bool live = InstanceOf(landing.Def, landing.Anchor) != null;
            // ⚠ Mark the resume BEFORE the dispatch; the sequence's own OBJECT_MOTION can fire in
            // the same instant, and a contact landing is a continuation, not a fresh throw.
            if (landing.ByContact)
                MarkLandingResume(landing.Target);
            if (live)
                CallSequence(landing.Def, landing.Anchor, landing.Bounce);
            else
                Count("ObjectMotion(bounce landed after its instance ended)");
            if (DebugMotions)
                GD.Print($"anim/debug: '{landing.Target.Name}' landed at {landing.Target.GlobalPosition} "
                         + $"— bounce sequence '{landing.Bounce}'"
                         + (live ? "" : " — NO LIVE INSTANCE, dispatched nothing"));
        }
    }

    // ---- node resolution — the rules live in NameResolver.cs; these are the forwards ----
    // World nodes a definition anchors to — NAME match, symbol narrowing, root lift, all
    // resolver-owned (see NameResolver.Anchors, which also records the once-per-def
    // anchoring census).
    private List<Node3D?> Anchors(AnimDefinition def) => _resolver.Anchors(def);

    // The world nodes one event targets. Reader-sourced events may carry a parent→child path;
    // compiled events name a single node (under "node" or "name", which upstream spells
    // inconsistently per event type).
    private List<Node3D> Targets(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        // ⚠ Resolve the self-reference sentinels here (IsSelfNodeRef). No node is named that and
        // the symbol table does not bind it, so without this a self-referencing event resolves to
        // nothing and is silently dropped, and a shot-down hull never falls.
        if ((ev.Data.Str("node") ?? ev.Data.Str("name")) is { } selfRef && IsSelfNodeRef(selfRef))
        {
            var host = InputNodeOf(def, anchor) ?? anchor;
            if (host != null)
                return new List<Node3D> { host };
            _opsUnresolved++;
            _resolver.RecordMissingTarget(def, selfRef, "self-ref-no-anchor");
            return new List<Node3D>();
        }

        // Compiled definitions carry a symbol table binding each referenced name to an exact
        // gamez node index — always prefer it. Name matching resolves C1's `caboose` to the
        // real consist AND to an unrelated `caboose.flt` in the rail yard, and drives both.
        if ((ev.Data.Str("node") ?? ev.Data.Str("name")) is { } refName
            && _resolver.SymbolClaims(def, refName, out var bound))
        {
            if (bound != null)
                return new List<Node3D> { bound };
            // ⚠ A claimed-but-unbuilt index is not an error, and must not be name-matched around
            // in the shared world; C1's `caboose` resolves to the consist and to a `caboose.flt`.
            // The scoped crash runtime is the exception, via NameResolveFallback.
            if (!NameResolveFallback)
            {
                // ⚠ One narrow rescue: a re-anchored exploder template's meshless parameter nodes
                // stand in for the call site's same-named pieces. Resolve strictly inside the
                // anchor's subtree, never globally.
                if (anchor != null && FindAll(refName, anchor) is { Count: > 0 } scoped)
                    return scoped;
                _opsUnresolved++;
                _resolver.RecordMissingTarget(def, refName, "index-not-built");
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
        var targets = ResolveScoped(path, def, anchor);
        if (targets.Count == 0)
        {
            _opsUnresolved++;
            _resolver.RecordMissingTarget(def, string.Join("/", path), "name-no-match");
        }
        return targets;
    }

    // Every world node matching a NAME pattern, optionally restricted to one subtree. A full scan
    // of the node index — and the data calls it constantly, because the poll idiom (If …
    // CallAnimation; Endif; Loop{-1}) re-dispatches its body every frame, so C5's ~400 live poll
    // loops asked for hundreds of resolutions per frame. Forwards to NameResolver, which
    // owns the index, the wildcard matcher, and the memoization. Callers must treat the returned
    // list as read-only.
    private List<Node3D> FindAll(string pattern, Node3D? scope) => _resolver.FindAll(pattern, scope);

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
        target.Basis = rest.Basis.Orthonormalized().Scaled(NonSingularScale(scale));
        _opsApplied++;
    }

    // Any still-visible node named like a destroyed variant that no definition touched:
    // hide it and report — each name is a data-coverage gap (a def we failed to anchor).
    // ⚠ Match 'destroyed' only: a '_dest' suffix rule is WRONG — C1's `ref_tank_dest` is
    // the parent GROUP of the five healthy harbor refuel tanks ("destructible", not
    // "destroyed"), and hiding it wipes the visible tanks.
    private List<string> HideUncoveredDestroyed()
    {
        var hidden = new List<string>();
        foreach (var (node, srcName) in _resolver.Rows)
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

    // Godot object identity is by native pointer, not the inherited Equals: two managed proxies
    // can wrap the same native node, so NameResolver's dictionary/tuple keys need this rather than
    // trusting Node3D's own equality.
    private sealed class Node3DIdentity : IEqualityComparer<Node3D>
    {
        public static readonly Node3DIdentity Instance = new();

        public bool Equals(Node3D? x, Node3D? y) => (x?.GetInstanceId() ?? 0) == (y?.GetInstanceId() ?? 0);

        public int GetHashCode(Node3D obj) => obj.GetInstanceId().GetHashCode();
    }
}
