using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CSVM.Mech3.Anim;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// The animation engine: binds an <see cref="AnimProgram"/> to a built world and executes
/// it. Replaces the start-state-only applier this project had before, whose
/// node resolution and INACTIVE semantics it keeps verbatim â€” those are user-verified and
/// were never the limitation.
///
/// At world build it performs the same four bootstrap passes as before:
///  1. every anchored definition's RESET_STATE (base states â€” hides the `destroyed`
///     building/vehicle variants the gamez stores overlaid on their healthy twins),
///  2. ACTIVATION ON_STARTUP definitions (zepstate.json â€” the per-mission object roster),
///  3. the mission's startanims.json NEW_GAME_START animations, in order,
///  4. a logged safety net hiding any still-visible `destroyed` subtree nothing covered.
///
/// The difference is that 2 and 3 now *run* rather than being posed at their end state: a
/// definition becomes a live instance with a clock, so hangar doors swing open over their
/// authored 9 s and the C1 train drives its 327 s SI-script track loop. Anything the engine
/// cannot yet act on is counted per event kind and reported once, never fatal â€” that is
/// what lets the remaining event types land incrementally without reshaping this class.
///
/// "Inactive" = hidden AND non-collidable (crashing into an invisible zeppelin would be
/// worse than the visual bug). Definitions resolve to built nodes by their ORIGINAL gamez
/// names (SceneBuilder stores them in the `cs_name` meta â€” Godot mangles duplicate sibling
/// names, so Node.Name is unreliable).
/// </summary>
public sealed partial class AnimRuntime : Node, ISequenceHost
{

    /// <summary>Original-name metadata key SceneBuilder stamps on every built Node3D.</summary>
    public const string NameMeta = "cs_name";

    /// <summary>Flat gamez node-index metadata key SceneBuilder stamps on every built
    /// Node3D â€” the exact binding compiled definitions reference (see
    /// <see cref="AnimDefinition.NodeRefs"/>).</summary>
    public const string IndexMeta = "cs_index";

    /// <summary>The reader's <c>ANIMATION_LOD HIGH</c> â€” see <see cref="QualityLod"/>.</summary>
    public const int HighLod = 2;

    /// <summary>Event kinds <c>Dispatch</c> acts on. Keep in step with its cases: a kind absent
    /// here is one the runtime counts as unhandled and does nothing for, which is what the node
    /// lab's coverage column reports.</summary>
    public static readonly IReadOnlyCollection<string> HandledEventKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        "ObjectActiveState", "ObjectTranslateState", "ObjectRotateState", "ObjectScaleState",
        "ObjectMotionFromTo", "ObjectOpacityState", "ObjectOpacityFromTo", "ObjectMotion",
        "ObjectMotionSiScript", "Loop", "If", "Elseif", "Else", "Endif", "CallSequence",
        "StopSequence", "CallAnimation", "StopAnimation", "InvalidateAnimation", "PufferState",
        "LightState", "LightAnimation", "SoundNode", "Sound", "ObjectAddChild",
    };

    /// <summary>Kinds with a handler that covers only part of what the event does â€” reported
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
    /// nothing, so this is inert when nobody asks for it.</summary>
    public bool ReportResolution;

    /// <summary>
    /// Refuse the <c>ANIMATION_ROOT_NAME</c> anchor lift, however few matches it finds. Set only by
    /// a caller that built part of the world, because <see cref="MaxRootLift"/>'s premise is a
    /// WHOLE-WORLD node population: 'healthy' appears 217Ã— in C1, so the cap rejects it there â€” and
    /// a single-subtree stage drops under the cap, at which point 95 unrelated definitions anchor
    /// onto whatever generic child the subtree happens to own (measured on C1's 20-node
    /// <c>ap_radiotwr</c>: 95 lifts and 91 phantom destructible instances). Suppressed lifts are
    /// counted and reported, never silently dropped.
    /// </summary>
    public bool SuppressRootLift;

    /// <summary>--debug-anim: log every live motion's target and pose once a second, so a
    /// headless run can verify that (say) the train actually drives its loop.</summary>
    public bool DebugMotions;

    /// <summary>
    /// Our answer to the data's <c>ANIMATION_LOD</c> condition â€” a project quality setting,
    /// not a fact about the world. The original hid detail on slow hardware; every
    /// LOD-gated branch in this install asks for the same tier (the reader spells it
    /// <c>HIGH</c>, which compiles to 2, and 2 is the only value that appears in all 8
    /// chapters), so the default passes them all. <c>--anim-lod=N</c> lowers it for A/B
    /// comparison.
    /// </summary>
    public int QualityLod = HighLod;

    /// <summary>Where the player is, for <c>PLAYER_RANGE</c> conditions. Supplied by the
    /// session (the flown aircraft, or the spectator camera); absent â†’ the viewport camera,
    /// and failing that the world origin. During the bootstrap passes there is no camera
    /// yet, which is harmless: every PLAYER_RANGE definition in this install re-polls from a
    /// <c>Loop{-1}</c>, so a bootstrap-time miss corrects on the next frame.</summary>
    public Func<Vector3>? PlayerPosition;

    /// <summary>Every player's position, for the EXECUTION_BY_RANGE proximity gate (nearest
    /// player wins). In flight this is the aircraft themselves — the chase camera trails far
    /// enough behind the plane to eat most of a 50 m radius. Null → the gate measures from
    /// <see cref="PlayerPosition"/>.</summary>
    public Func<IReadOnlyList<Vector3>>? PlayerPositions;

    /// <summary>Answers the data's <c>PLAYER_1ST_PERSON</c> condition. No cockpit view
    /// exists yet (backlog), so false.</summary>
    public bool FirstPerson;

    /// <summary>Whether <see cref="Bootstrap"/> runs the ambient-playback passes â€” pass 2
    /// (<c>ON_STARTUP</c> definitions) and pass 3 (the mission's startanims). True in every
    /// game/viewer/flight session: the world plays itself. The animation debugger sets it false
    /// for a <b>quiet stage</b> â€” passes 0 (mission setup), 1 (reset states) and 4 (the safety
    /// net) still run, so every base state and mission-entity setup is applied, but nothing starts
    /// animating until <see cref="StartAmbient"/> is called (the lab's ambient toggle). Set before
    /// <see cref="Bind"/>.</summary>
    public bool AutoStart = true;

    /// <summary>The mission's interp boot script (<c>support\&lt;chapter&gt;\&lt;mission&gt;.gw</c>),
    /// run as bootstrap pass 0. It is what decides which world entities this mission shows â€”
    /// see <see cref="MissionSetup"/>. Null when the mission ships no script, which is normal.
    /// Set before <see cref="Bind"/>.</summary>
    public MissionSetup? Setup;

    // ---- observability (the animation debugger's timeline; null = zero cost in the game) ----
    /// <summary>Raised as each sequence event fires at runtime. Null by default â†’ zero cost in the
    /// game; the debugger sets it to feed its timeline's fired marks straight from the runtime,
    /// rather than parsing --debug-anim log text. The interpreter reads it through the get-only
    /// <see cref="ISequenceHost.OnEventDispatched"/> seam member.</summary>
    public Action<EventDispatch>? OnEventDispatched;

    /// <summary>Raised when a definition becomes a live instance and when that instance finishes,
    /// each carrying the (def, anchor) identity. Null by default â†’ zero cost in the game; the
    /// debugger uses the pair to place a CALL_ANIMATION child def's timeline lane group at the
    /// playhead time it began, and to drop it when it ends.</summary>
    public Action<AnimDefinition, Node3D?>? OnInstanceStarted;

    public Action<AnimDefinition, Node3D?>? OnInstanceFinished;

    // ---- live execution ----
    /// <summary>True when something else owns the clock (the animation debugger, which feeds
    /// <see cref="Advance"/> in fixed 1/60 s steps): <see cref="_Process"/> stops advancing.
    /// A flag rather than <c>SetProcess(false)</c> because Godot re-enables processing at READY
    /// for any node whose script overrides <c>_Process</c> â€” and this node enters the tree
    /// (with the world root) after the lab mode is assembled, so a SetProcess call made before
    /// that is silently undone. Found by measurement, not by reading: the lab's world ran at 2Ã—
    /// (fixed steps + wall dt), visible as 20 logged sim-seconds in a 610-frame scripted run.</summary>
    public bool ManualAdvance;

    /// <summary>Whether a CALL_ANIMATION relocates its callee's effect-template root onto the call
    /// site (see <see cref="PlaceTemplateAt"/>). Off by default â€” the ambient world boot must stay
    /// byte-identical, and today's retarget only re-scopes name resolution. The animation debugger
    /// (and, later, the crash runtime) turn it on so a placeless effect template plays where it is
    /// staged instead of at its gamez origin.</summary>
    public bool PlaceCalledTemplates;

    /// <summary>Reveals an effect template's root while an effect plays on it, and hides it again
    /// when that effect is torn down. Set on the world-effects runtime, whose templates are staged
    /// hidden so nothing renders ambiently at the stage origin: without this the templates' own
    /// MESHES — the rocket's per-type explosion rings, the fireball facades, the splash models —
    /// never draw, only their puffers do (D31). Only the root's own visibility is touched; what
    /// shows inside it stays the data's decision (the rings are reset INACTIVE or opacity-OFF and
    /// their defs turn them on). Off everywhere else, where the stage is visible anyway.</summary>
    public bool ShowPlacedTemplates;

    /// <summary>Key puffer emitters by owning def as well as (name, host) â€” see the
    /// <see cref="_puffers"/> remark. Set on the world-effects runtime, where distinct effect defs
    /// declaring same-named puffers are distinct emitters (the damage-stage sputters); off on the
    /// world runtime, where the collapsed key de-dups same-name multi-def ambient stacks.</summary>
    public bool DefScopedPufferKeys;

    /// <summary>Makes this runtime resolve every node reference by NAME, ignoring the compiled
    /// gamez-index table (<see cref="_byIndex"/> is not populated â€” see <see cref="IndexWorld"/>).
    /// Off by default: the shared world MUST use the index, because name matching resolves C1's
    /// <c>caboose</c> to the real consist AND an unrelated <c>caboose.flt</c>. The per-player crash
    /// runtime turns it on for two reasons that both make the index wrong there: (1) the player
    /// crash def's node ptrs are non-portable â€” they index planes.zbd at slots this build never uses
    /// â€” so the compiled index resolves nothing; and (2) its scoped subtree MIXES two gamez index
    /// spaces (the plane model's plane-gamez indices and the effect templates' world-gamez indices),
    /// which COLLIDE (fly_trail1 is world-index 400, and the plane has a node at plane-index 400),
    /// so a shared <c>_byIndex</c> would misresolve. Its subtree has one node per name, so name
    /// resolution is both unambiguous and the only correct choice.</summary>
    public bool NameResolveFallback;

    /// <summary>Hands a named effect off to another runtime (the D32 world-effects runtime) instead
    /// of starting it locally. Set on the WORLD runtime: when a death sequence's CALL_ANIMATION names
    /// a destruction/impact effect the effects runtime handles, the world runtime â€” whose puffer
    /// factory is gone after the build â€” routes it there with the call-site world point, the resolved
    /// call-site NODE (the callee's INPUT_NODE, which the local path expresses by anchoring the
    /// callee on it), and returns true, so the local Start (which would render nothing) is skipped.
    /// Null on every other runtime, where CALL_ANIMATION behaves exactly as before.</summary>
    public Func<string, Vector3, Node3D?, bool>? ExternalEffect;

    /// <summary>Stops a named effect on the external runtime <see cref="ExternalEffect"/> routes to
    /// â€” the reverse channel, for undoing a routed effect the data has no stop event for:
    /// <see cref="ResetDestructible"/> heals an object whose damage-stage sputter loops for as long
    /// as its host stays active, so the reset itself must end it.</summary>
    public Action<string>? ExternalEffectStop;

    /// <summary>This runtime does not own audio â€” its SOUND / SOUND_NODE events are no-ops, not
    /// late-failure reports. Set on the D32 world-effects runtime: it renders an effect def's
    /// puffers, but the same effect's impact/death SOUND is already played by the projectile pool
    /// (D30) or the world runtime (D31), so playing it here too would double it, and with no audio
    /// session it would only spam "silent for the session" warnings.</summary>
    public bool SoundHandledElsewhere;

    /// <summary>How long a <see cref="PlayEffectAt"/> effect instance may run before this runtime
    /// stops it (seconds; 0 = never, the default). The world-effects runtime sets it so a stop-less
    /// sustained effect â€” <c>large_30sec_fire</c>'s <c>fire_n_smoke</c>, which has no ACTIVE_STATE 0
    /// and would otherwise emit for the rest of the session â€” is bounded. Only effects this runtime
    /// itself started via PlayEffectAt are tracked; ambient/crash runtimes leave it 0 and are
    /// untouched.</summary>
    public float EffectTtl;

    /// <summary>A world-space velocity added to every ballistic <see cref="MotionRuntime"/> launch
    /// (translation / translation_range), transformed into the launched node's parent frame. Zero by
    /// default. The crash sets it to a fraction of the plane's impact velocity so the wreck pieces
    /// carry the plane's momentum and scatter along its travel â€” the authored launch alone is a small
    /// relative pop (5â€“10 m/s straight up), which reads as "the pieces barely drift" against a plane
    /// that hit at 60â€“90 m/s. It is the physical part the def leaves to the engine (the original does
    /// the same); a TUNE on the fraction, not a decode.</summary>
    public Vector3 InheritedWorldVelocity;

    // ---- PUFFER_STATE ----
    /// <summary>
    /// Builds a <see cref="Effects.Puffer"/> for a state, or null. Supplied by GameSession and
    /// valid only DURING the world build: a puffer bakes its texture atlas at construction
    /// from the session's <see cref="TextureArchive"/>, which is disposed when the build ends.
    /// Cleared afterwards, so a later request is reported rather than silently faulting on a
    /// closed zip handle. In practice every PUFFER_STATE that matters fires during the
    /// bootstrap passes (measured on C1: the waterfall mist, the train's steam, two truck
    /// dust plumes â€” nothing else reaches one).
    /// </summary>
    public Func<Effects.PufferState, Effects.Puffer?>? PufferFactory;

    /// <summary>Where built puffers are parented (the session root, not the animated node â€”
    /// their particles live in world space and must not be dragged by the emitter's motion).</summary>
    public Node? PufferParent;

    /// <summary>How many PUFFER_STATE emitters this runtime has actually built (not just started
    /// the owning def). The D32 world-effects verify checks this rather than "the def ran" â€” a
    /// started effect whose factory is torn down or whose textures are missing builds nothing and
    /// renders nothing (verification.md WORLD-12).</summary>
    public int PuffersBuilt;

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

    /// <summary>ANIMATION_ROOT_NAME matches above this count are generic per-object roots
    /// ('healthy' appears 217Ã— in C1) â€” those defs belong to game objects (planes, zeppelin
    /// parts), not to world nodes. The genuine building templates lift â‰¤ 9 instances.</summary>
    private const int MaxRootLift = 16;

    private const int CensusCap = 12;

    private const int MaxStartDepth = 8;

    /// <summary>The magic sequence name a destructible's progressive-damage script carries in
    /// both the reader and compiled forms (docs/formats/destructibles.md).</summary>
    private const string DamageSequenceName = "DAMAGE_SEQUENCE";

    // A subtree faded to ~invisible must also drop its colliders: the opacity path only writes a
    // shader parameter, so without this a node faded to alpha 0 stays solid and the player hits
    // an invisible wall. (Most authored fade-to-0 targets — the spiderweb, debris pieces — are
    // intersect_surface=false and never build colliders at all; this covers any collidable
    // subtree a fade reaches.) Mirror the deactivation path's
    // "invisible â‡’ non-collidable" rule and restore colliders when it fades back above the
    // threshold (so a subtree still fading IN stays solid). Edge-triggered on the last collidable
    // state per subtree root â€” a fade re-writes opacity every tick, and re-walking the subtree to
    // (re)assert colliders each frame would thrash. Independent of SetSubtreeActive's own collider
    // toggle: the two drive separate channels (translucency vs visibility) and, like Visible vs
    // the opacity parameter themselves, the most recent event wins the collider flag.
    private const float OpacityCollisionEpsilon = 0.01f;

    // The deferred-EXECUTION_BY_RANGE sweep quantises the player position to this cell size and
    // re-checks the deferred list only on a cell crossing (the MapEdgeExtender cadence, so a
    // hovering camera costs one Vector3I compare per frame). Sized well under the smallest
    // authored radius in the install (50 m, the C3 spiderweb): the sweep can lag an approach by
    // at most one cell diagonal, and the spiderweb's 0.7 s fade needs the trigger to land close
    // to the authored 50 m at cruise speed.
    private const float RangeCheckCellSize = 8f;

    /// <summary>Smallest magnitude a pose-scale component may reach. The data legitimately
    /// animates scale to exactly 0 on one or more axes ("shrink away": hook retracts, the
    /// C2/C3 bridge fires, C4's zdome collapse), but a node flattened to a singular basis
    /// poisons every native consumer that inverts it — Godot's physics server spams
    /// `det == 0` (basis.cpp:47) syncing any StaticBody3D under the node. 1e-3 of a
    /// world-object's size is sub-pixel at gameplay distance, and the anims hide these
    /// nodes anyway (OBJECT_ACTIVE_STATE off / opacity fade).</summary>
    private const float MinPoseScale = 1e-3f;

    private readonly List<(Node3D Node, string SrcName)> _index = new();

    private readonly Dictionary<string, Func<string, bool>> _matcherCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<Node3D, Transform3D> _rest = new(); // authored pose per touched node

    private readonly List<AnimInstance> _instances = new();

    private readonly Dictionary<string, int> _unhandled = new(StringComparer.Ordinal);

    private readonly DestructibleRegistry _destructibles = new();

    private readonly HashSet<(string Name, string Anim)> _censusSeen = new();

    private readonly List<string> _censusUnanchoredNames = new();

    private readonly List<string> _censusLiftedNames = new();

    private readonly List<string> _censusSuppressedNames = new();

    private readonly List<string> _censusMissingTargets = new();

    private readonly Dictionary<int, Node3D> _byIndex = new();

    // One emitter per (puffer name, emitter node[, owning def]). Definitions re-assert their
    // PUFFER_STATE every loop iteration â€” C1's waterfall is [PufferState Ã—3, Loop{-1}] â€” so the
    // handler has to be idempotent: re-asserting an already-running emitter must be a no-op, not a
    // second emitter. On the world-effects runtime (<see cref="DefScopedPufferKeys"/>) the key
    // also carries the def, because two effect defs can declare same-named puffers on one host â€”
    // the two damage-stage sputters both call theirs `black_smoke`, and a shared key let the
    // stage-1 smoke emitter mask the stage-2 fire build. Safe by the data: all 2,774 compiled
    // PUFFER_STATE events reference only puffers their own def declares (measured install-wide).
    // The WORLD runtime deliberately keeps the def out of the key (Def = null): C5's six
    // `m_crane_go(#N)` twins all name-resolve `man_spark` onto one node, and def-scoped keys there
    // stacked six spark emitters on it (measured â€” it moved the c5-city-night golden); the
    // collapsed key doubles as the de-dup for that name-resolution artifact. The value carries the
    // owning (def, anchor) so Stop can tear down exactly the emitters a stopped instance created.
    private readonly Dictionary<(string Name, Node3D Node, AnimDefinition? Def), (Effects.Puffer Puffer, AnimDefinition Def, Node3D? Anchor)> _puffers = new();

    private readonly List<(Effects.Puffer Puffer, Node3D Node)> _activePuffers = new();

    // Each host node's emission point in its own frame (see VisualOriginOf) â€” zero for a node
    // whose origin sits inside its mesh bounds. Computed lazily on the first tick, never at
    // dispatch: the bootstrap dispatches PUFFER_STATE before the world enters the tree, where a
    // GlobalTransform read only returns identity and an error. Local-frame, so it stays valid
    // when a motion drives the node.
    private readonly Dictionary<Node3D, Vector3> _hostOffsets = new();

    // The call-site node a PlayEffectAt instance was invoked WITH â€” the callee's INPUT_NODE. The
    // local CALL_ANIMATION path expresses this by anchoring the callee on the site node; the
    // external path anchors on the staged template root instead, so the sentinel's referent is
    // carried here, keyed by the instance identity. Cleared with the instance.
    private readonly Dictionary<(AnimDefinition Def, Node3D? Anchor), Node3D> _inputNodes = new();

    // Which defs condition on their own INPUT_NODE's active state (a NodeActive sentinel in any
    // sequence) â€” the damage-stage sputters. Their lifetime is authored (loop while the host node
    // is active), so PlayEffectAt gives them the real site node and no TTL.
    private readonly Dictionary<AnimDefinition, bool> _inputGoverned = new();

    // Keyed by (sound name, anchor), like the lights and for the same reason: the anchor
    // identifies the *instance* of the definition, so C1's four firetrucks each get their own
    // siren rather than sharing one. It cannot be keyed by host node the way puffers are â€” in the
    // reader's triple the emitter is declared BEFORE anything says where it goes.
    private readonly Dictionary<(string Name, Node3D? Anchor), object> _soundEmitters = new();

    private readonly HashSet<string> _soundFailuresReported = new(StringComparer.OrdinalIgnoreCase);

    // Keyed by (light name, anchor). The anchor identifies the *instance* of the definition,
    // and a definition's `lights` array is its own symbol table â€” so two refineries each get
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

    // ---- active motions ----
    private readonly List<IAnimMotion> _motions = new();

    // Memoized for the life of the runtime: results go stale if a node is ever reparented
    // into or out of a world subtree at runtime, so nothing may do that (pooled sound
    // emitters and crash puffers live outside the world for this reason).
    private readonly Dictionary<(string Pattern, ulong Scope), List<Node3D>> _findCache = new();

    // Last opacity pushed to each subtree root. These events sit in `Loop{-1}` sequences â€”
    // C1's `cloudparent#` re-asserts its 0.6 every frame â€” so without this the whole subtree
    // would be re-walked and re-written ~31 times a frame to set values it already holds. Same
    // lesson as LightState's per-light host cache, which cost ~7 ms/frame before it existed.
    private readonly Dictionary<Node3D, float> _opacity = new();

    private readonly Dictionary<Node3D, bool> _opacityCollidable = new();

    // The fade twins a genuine partial opacity installs per instance (see EnsureOpacityPath):
    // source material -> its translucent twin (null = cannot be made translucent), the twin
    // set for recognising an override this runtime installed, and the shader-level cache so
    // materials sharing one generated shader share one twin shader.
    private readonly Dictionary<ShaderMaterial, ShaderMaterial?> _fadeTwinCache = new();

    private readonly HashSet<Material> _fadeTwins = new();

    private readonly Dictionary<Shader, Shader?> _fadeShaderCache = new();

    // ON_STARTUP defs carrying EXECUTION_BY_RANGE wait here instead of starting at bootstrap:
    // each (def, anchor) starts once, the first time the player is inside its distance band.
    // Checked on a cell-crossing cadence (see TickDeferredByRange), never per frame.
    private readonly List<(AnimDefinition Def, Node3D Anchor)> _rangeDeferred = new();

    private readonly List<Vector3I> _rangeCheckCells = new();

    private Node3D _root = null!;

    private AnimProgram _program = null!;

    private int _opsApplied, _opsUnresolved;

    private bool _censusOpen;

    private int _censusAnchored, _censusLifted, _censusSuppressed, _censusUnanchored, _censusMissing;

    // Whether the ambient passes have already run â€” set when Bootstrap runs them inline
    // (AutoStart=true) or when StartAmbient runs them on demand, so StartAmbient is idempotent
    // and a normal bootstrap's ambient toggle is a no-op rather than a second bootstrap.
    private bool _ambientStarted;

    private int? _seed;

    private int _startDepth;

    private int _soundsUnknown, _soundsAfterBuild;

    /// <summary>Set once <see cref="Bootstrap"/> has printed its emitter census. After this, a
    /// failed SOUND_NODE is invisible unless reported at the point of use â€” which is exactly how
    /// C1's police siren stayed silent undetected: the census is a bootstrap
    /// snapshot, so it cannot distinguish "never requested" from "requested later and failed".
    /// Reported once per name, not per event: snd_fire1 alone has 363 sites.</summary>
    private bool _soundCensusPrinted;

    private float _effectClock;

    private int _damagesLogged;

    // CALL_ANIMATION retargeting tallies. Deliberately NOT routed through Count(), which is
    // the "event kinds not yet acted on" channel â€” a retargeted call is acted on, and filing
    // it there would report a working feature as a missing one.
    private int _retargeted, _retargetUnresolved;

    private float _debugClock;

    /// <summary>Running count of ballistic <see cref="MotionRuntime"/> bodies launched â€” the debris
    /// pieces a death or crash flings (translation/translation_range/scale/forward_rotation over a
    /// run time). Zero at bootstrap (nothing ambient fires the ballistic path); the C26 harness
    /// samples the delta across a kill to prove the wreck actually tumbles.</summary>
    public int BallisticMotionsLaunched { get; private set; }

    /// <summary>Live animation instances currently running (diagnostics).</summary>
    public int ActiveInstances => _instances.Count;

    /// <summary>Per-kind counts of events (and puffer/sound sub-reasons) this runtime processed but
    /// could not act on â€” the same tally <c>ReportUnhandled</c> prints at bootstrap, exposed so a
    /// post-bootstrap harness (the D32 effects-test) can see WHY an effect built no puffer
    /// (<c>PufferState(no host node)</c>, <c>PufferState(no texture: â€¦)</c>).</summary>
    public IReadOnlyDictionary<string, int> UnhandledEventCounts => _unhandled;

    /// <summary>The live per-instance HP of every destructible node group in this world (C21).
    /// Built during the bootstrap; the source of the value <c>ANIM_HEALTH</c> conditions read.
    /// C23's weapon damage and C24's death sequence act through it.</summary>
    public DestructibleRegistry Destructibles => _destructibles;

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

    /// <summary>One-shot SOUND events that resolved to a stream and fired this session (the
    /// destruction/damage/impact audio). Exposed for the damage-test harness, which cannot
    /// screenshot audio: a nonzero delta across a kill is how "the death's explosion sounded" is
    /// verified headless.</summary>
    public int OneShotSoundsPlayed { get; private set; }

    /// <summary>Configures (but does not bind) the world-effects runtime's construction ritual: the
    /// four invariant flags a "renders effects at a call site, no ambience of its own" role always
    /// takes (<see cref="AutoStart"/>=false, <see cref="PlaceCalledTemplates"/>,
    /// <see cref="NameResolveFallback"/>, <see cref="SoundHandledElsewhere"/>). The caller still calls
    /// <see cref="Bind"/> + adds the returned node to the tree — this only hides the invariant block.
    /// <paramref name="pufferParent"/> must be the world root even for a per-player caller (the crash
    /// lesson: a PUFFER_STATE emitter goes TopLevel the moment it emits, so parenting it anywhere else
    /// leaves it drawn-but-unrendered).</summary>
    public static AnimRuntime ForEffects(int seed, Node3D pufferParent,
        Func<Effects.PufferState, Effects.Puffer?> pufferFactory, bool debugMotions, float effectTtl,
        Func<Vector3> playerPosition)
    {
        return new AnimRuntime
        {
            AutoStart = false,
            PlaceCalledTemplates = true,
            NameResolveFallback = true,
            SoundHandledElsewhere = true,
            DefScopedPufferKeys = true,
            DebugMotions = debugMotions,
            PufferParent = pufferParent,
            PufferFactory = pufferFactory,
            EffectTtl = effectTtl,
            Seed = seed,
            PlayerPosition = playerPosition,
        };
    }

    /// <summary>Configures (but does not bind) the per-player crash rig's construction ritual —
    /// the same four invariant flags as <see cref="ForEffects"/>, but with no <c>EffectTtl</c> or
    /// <c>PlayerPosition</c> (the crash def has no PUFFER_STATE that needs either). The caller still
    /// calls <see cref="Bind"/> + adds the returned node to the tree. <paramref name="pufferParent"/>
    /// must be the world root, never the per-player crash root (the crash lesson: a PUFFER_STATE
    /// emitter goes TopLevel the moment it emits, so parenting it under the controller subtree leaves
    /// it drawn-but-unrendered).</summary>
    public static AnimRuntime ForCrashRig(int seed, Node3D pufferParent,
        Func<Effects.PufferState, Effects.Puffer?> pufferFactory, bool debugMotions)
    {
        return new AnimRuntime
        {
            AutoStart = false,
            PlaceCalledTemplates = true,
            NameResolveFallback = true,
            SoundHandledElsewhere = true,
            DebugMotions = debugMotions,
            PufferParent = pufferParent,
            PufferFactory = pufferFactory,
            Seed = seed,
        };
    }

    /// <summary>The world nodes a definition anchors to, for the inspect tools â€” the runtime's own
    /// answer, so a readout shows what the bootstrap actually bound rather than a re-derivation.
    /// A null entry is a global (anchorless) instance. Read-only: the list is the cached one.</summary>
    public IReadOnlyList<Node3D?> AnchorsOf(AnimDefinition def) => Anchors(def);

    /// <summary>Every world node matching a NAME pattern, optionally restricted to one subtree â€”
    /// the same wildcard matching and the same memoized index the dispatch uses, exposed so an
    /// inspect tool asks the engine instead of re-implementing the matcher. Read-only.</summary>
    public IReadOnlyList<Node3D> FindNodes(string pattern, Node3D? scope = null) => FindAll(pattern, scope);

    /// <summary>Runs the bootstrap passes against a built world. A separate call (not folded into
    /// construction) so a caller can set build-time-only collaborators (notably
    /// <see cref="PufferFactory"/>, which depends on the session TextureArchive's lifetime)
    /// before the passes fire the events that need them. The world runtime binds directly; the
    /// <see cref="ForEffects"/>/<see cref="ForCrashRig"/> role factories return unbound for the same
    /// reason.</summary>
    public void Bind(Node3D worldRoot, AnimProgram program)
    {
        Name = "AnimRuntime";
        Bootstrap(worldRoot, program);
    }

    /// <summary>Re-pins the RNG to the constructed <see cref="Seed"/>, and clears the sound groups'
    /// last-picked memory with it â€” that recency state sits outside the RNG, so restoring only the
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

    /// <summary>
    /// The bind-time resolution census, ready to log â€” empty unless <see cref="ReportResolution"/>
    /// was set. It exists because <b>two different failures look identical from outside</b> a
    /// partial world: a definition that never instantiated (its NAME/ANIMATION_ROOT_NAME matches
    /// nothing here, so <i>no handler ever fires</i>) and a definition that IS running but whose
    /// event names a node <i>this subtree does not contain</i>. Both leave the object still. The
    /// lines name which happened, per definition.
    ///
    /// <para>The line nobody expects is <c>root_lift_suppressed</c>: an <c>ANIMATION_ROOT_NAME</c>
    /// lift is capped at 16 matches precisely so a generic root like <c>healthy</c> (217Ã— in C1)
    /// cannot anchor a definition onto every building â€” and a single-subtree stage drops under that
    /// cap, so defs that never anchor in the full world would anchor here, onto whatever generic
    /// child the subtree happens to own. <see cref="SuppressRootLift"/> refuses them and this
    /// reports the refusal, because a silently-different anchor set is the trap.</para>
    /// </summary>
    public IReadOnlyList<string> ResolutionLines()
    {
        var lines = new List<string>();
        if (!ReportResolution)
        {
            return lines;
        }
        int defs = _censusAnchored + _censusLifted + _censusSuppressed + _censusUnanchored;
        lines.Add($"bind census defs={defs} anchored_by_name={_censusAnchored} "
                  + $"anchored_by_root_lift={_censusLifted} root_lift_suppressed={_censusSuppressed} "
                  + $"unanchored={_censusUnanchored} target_missing_ops={_censusMissing}");
        if (_censusUnanchored > 0)
        {
            lines.Add($"bind unanchored={_censusUnanchored} â€” no handler ever fires for these: "
                      + Sample(_censusUnanchoredNames, _censusUnanchored));
        }
        if (_censusLifted > 0)
        {
            lines.Add($"bind root_lifted={_censusLifted} â€” anchored only because this subtree has "
                      + $"â‰¤{MaxRootLift} of the def's ANIMATION_ROOT_NAME, which the full world does not: "
                      + Sample(_censusLiftedNames, _censusLifted));
        }
        if (_censusSuppressed > 0)
        {
            lines.Add($"bind root_lift_suppressed={_censusSuppressed} â€” these WOULD have anchored on "
                      + $"this subtree's generic ANIMATION_ROOT_NAME children, which the full world's "
                      + $"node count rules out; refused so the stage shows only defs that name it: "
                      + Sample(_censusSuppressedNames, _censusSuppressed));
        }
        if (_censusMissing > 0)
        {
            lines.Add($"bind target_missing={_censusMissing} â€” the def IS running, the node is not in "
                      + $"this subtree: " + Sample(_censusMissingTargets, _censusMissing));
        }
        return lines;
    }

    /// <summary>Starts every definition carrying this ANIMATION_NAME, exactly the way bootstrap
    /// pass 3 starts a startanim: an anchored def starts once per anchor, an unanchored one gets
    /// a single global-resolution instance (null anchor), and each instance's RESET_STATE is
    /// re-applied first. Returns the (def, anchor) pairs started â€” empty when the name matches no
    /// definition in this program. This is the animation debugger's <c>--play-anim</c> path; its
    /// Restart is <see cref="Stop"/> â†’ <see cref="Reseed"/> â†’ Play.</summary>
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
                // anchor â€” the animation debugger's in-front-of-camera dummy â€” or null, which is
                // global resolution (the def names world nodes directly). Bootstrap passes null.
                anchors.Add(fallbackAnchor);
            }
            foreach (var anchor in anchors)
            {
                // The debugger re-poses the RESET_STATE before replaying (a startanim-style start).
                // The crash TRIGGER must not: player_crash_dirt's reset calls player_destruction_reset
                // (restores the healthy panels) and hides the wreck â€” the exact opposite of a crash â€”
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
    /// nothing resolves â€” the caller keeps its current framing.</summary>
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
    /// Idempotent â€” a second call is a no-op, and it is already a no-op after a normal
    /// (AutoStart=true) bootstrap â€” so the debugger can bind it to a toggle without stacking
    /// instances.</summary>
    public void StartAmbient()
    {
        if (_ambientStarted)
            return;
        _ambientStarted = true;
        var (startupRun, ran, missing) = RunAmbientPasses();
        GD.Print($"anim: ambient start â€” {startupRun} ON_STARTUP + {ran.Count} start anims running, "
                 + $"{_instances.Count} live instance(s), {_motions.Count} live motion(s)");
        if (ran.Count > 0 || missing.Count > 0)
            GD.Print($"anim: start anims [{string.Join(", ", ran)}]" +
                     (missing.Count > 0 ? $", undefined here: [{string.Join(", ", missing)}]" : ""));
    }

    /// <summary>Reverses <see cref="StartAmbient"/> â€” the animation debugger's ambient <b>off</b>
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
        _rangeDeferred.Clear(); // a quiet stage must not proximity-start ambient defs
        _ambientStarted = false;
        GD.Print($"anim: ambient stopped â€” {_instances.Count} live instance(s) kept, "
                 + $"{_motions.Count} live motion(s)");
    }

    /// <summary>Hard-stops EVERYTHING this runtime created and re-applies every anchored
    /// definition's RESET_STATE â€” the per-player crash runtime's respawn. After a crash the wreck is
    /// shown, the pieces flung and the fire burning; this puts the def back to its quiet base
    /// (destroyed hidden, effect templates hidden) so the next crash starts clean, and the caller
    /// re-homes any node the def MOVED but has no reset event for (the flung wreck pieces).
    ///
    /// <para>Clears the whole RESOURCE POOL, not just live instances: an effect def whose sequence
    /// has already ended (e.g. <c>large_10sec_fire</c>, whose 10 s particles outlive its instance)
    /// is gone from <c>_instances</c> yet its puffer is still emitting, so a per-instance teardown
    /// would leave the fire burning after respawn. And puffers are <c>Clear</c>ed (particles gone at
    /// once), not <c>SustainEnd</c>ed (which lets them finish their lifetimes) â€” respawn is
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
            entry.Puffer.QueueFree(); // the next crash builds fresh emitters â€” do not accumulate
        }
        _puffers.Clear();
        _activePuffers.Clear();
        _inputNodes.Clear();
        _hostOffsets.Clear();
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

    /// <summary>Indexes a subtree added to the world AFTER the bootstrap â€” the animation debugger's
    /// effect-template stage (the fireball/spark/trail/dirt roots <see cref="WorldBuilder"/>
    /// deliberately skips) â€” so its nodes resolve by name and index, then applies the RESET_STATE of
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
    /// (<see cref="Node.SetProcess"/>(false)) and drives this itself off a fixed-dt clock â€” pause =
    /// don't call, step = one fixed call, slow-mo = a scaled accumulator. Same classes and same
    /// code path either way, so the game's behaviour is untouched.</summary>
    public void Advance(float dt)
    {
        // Motions advance ONCE per frame, here â€” not from the sequence runners, which would
        // apply dt once per running sequence and run the train at 4Ã— speed.
        TickMotions(dt);
        TickPuffers(dt);
        TickLights(dt);
        Sounds?.Tick();
        SweepEffectTtls(dt);
        TickDeferredByRange();
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
                FinishInputGoverned(inst.Def, inst.Anchor);
                OnInstanceFinished?.Invoke(inst.Def, inst.Anchor);
            }
        }
    }

    /// <summary>Starts a definition on one anchor (null = resolve its node names globally),
    /// running every sequence that is not ACTIVATION ON_CALL. Starting a def that is already
    /// live on the same anchor RESTARTS it â€” CALL_ANIMATION deliberately does not take this
    /// path for a running animation (see its case in <see cref="Dispatch"/>).</summary>
    public void Start(AnimDefinition def, Node3D? anchor)
    {
        // CALL_ANIMATION chains are data, and the data can (and in some chapters does) form
        // cycles: A calls B calls A. Starting an instance fires its t=0 events immediately,
        // so an unguarded cycle recurses until the stack dies. Bound the depth instead â€”
        // legitimate chains in this install are 2â€“3 deep.
        if (_startDepth >= MaxStartDepth)
        {
            Count("CallAnimation(depth limit)");
            return;
        }
        // Restart: drop any existing instance of this def on this anchor, but LEAVE its live
        // resources so the new instance re-establishes them idempotently (motions replaced by
        // target in AddMotion, puffers/lights/sounds re-asserted as no-ops). Tearing them down
        // here would break that seamless restart and rebuild every resource â€” measured on C5's
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
        // Its t=0 events can finish the instance â€” or a t=0 STOP_ANIMATION can already have
        // removed it â€” so only notify a finish that actually removed something, keeping the
        // start/finish notifications balanced against the live count for the timeline.
        if (inst.Finished && _instances.Remove(inst))
        {
            FinishInputGoverned(def, anchor);
            OnInstanceFinished?.Invoke(def, anchor);
        }
    }

    /// <summary>Stops every live instance of an animation name (optionally only on one anchor) and
    /// tears down the live resources it created. Used by STOP_ANIMATION / INVALIDATE_ANIMATION, and
    /// â€” through the explicit Restart it enables (Stop â†’ re-apply RESET_STATE â†’ Start) â€” by the
    /// animation debugger and the crash plan's respawn. Removing the instance alone left the
    /// definition's motions driving nodes, its puffers emitting, its lights lit and its sounds
    /// playing; <see cref="TearDownResourcesOf"/> clears all four. Start's own restart deliberately
    /// does NOT come through here â€” it keeps the resources so re-assertion is a seamless no-op
    /// (see <see cref="Start"/>).</summary>
    public void Stop(string? animName, Node3D? anchor = null) =>
        RemoveInstances(animName, anchor, tearDown: true);

    // ---- world-effects runtime (D32) ----
    /// <summary>Does this runtime's program hold a definition for an effect animation name? The
    /// world runtime tests this before routing a death's CALL_ANIMATION here, so only the curated
    /// impact/destruction effects are handed off (doors and other calls fall through).</summary>
    public bool Handles(string animName) => _program.ByAnimName(animName).Count > 0;

    /// <summary>Stages the named effect at an absolute world point: relocates each matching effect
    /// template's root onto the point (so its puffers, which ride that root, emit there) and starts
    /// the definition, exactly as a CALL_ANIMATION would but with a synthetic site. Returns true if a
    /// definition matched â€” the world-effects runtime that <c>ProjectilePool</c> and the death path
    /// call. Idempotent per template: the shared root is relocated, not copied, so overlapping calls
    /// to the same effect collapse onto the latest site (the documented gun follow-up).
    ///
    /// <para><paramref name="inputNode"/> is the call-site NODE when the caller has one (the world
    /// runtime's routed CALL_ANIMATION resolves WITH_NODE/AT_NODE) â€” the callee's INPUT_NODE, which
    /// the local call path expresses by anchoring the callee on it. It resolves the def's
    /// INPUT_NODE references, so a damage-stage sputter emits on (and moves with) the damaged
    /// object itself and its <c>NodeActive</c> loop gate reads that object â€” the loop exits when
    /// the death swap deactivates the healthy subtree. A def that conditions on its input node's
    /// active state gets NO TTL (its lifetime is authored); every other effect keeps the
    /// <see cref="EffectTtl"/> bound. An input-governed def is Stop'd before restart so a second
    /// damaged object gets fresh emitters instead of orphaning the first object's (the same
    /// latest-site collapse the templates already have).</para></summary>
    public bool PlayEffectAt(string animName, Vector3 worldPoint, Node3D? inputNode = null)
    {
        bool matched = false;
        foreach (var def in _program.ByAnimName(animName))
        {
            PlaceTemplateAt(def, worldPoint);
            // Anchor the instance on the def's own template root when it resolves (its at_node/
            // motion targets live under that root); fall back to null (global name resolution).
            var anchor = Anchors(def).FirstOrDefault(a => a != null && IsInstanceValid(a));
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
            ShowTemplate(def, visible: true);
            matched = true;
            if (!governed && EffectTtl > 0f)
                _effectTtls.Add((def, anchor, _effectClock + EffectTtl));
        }
        return matched;
    }

    /// <summary>Stops every live instance and clears the effect-TTL list â€” a full reset of what
    /// PlayEffectAt started, so the D32 verify measures each effect in a clean window (these effects
    /// share puffer names/hosts, so a lingering one would contaminate the next). Not used in play.</summary>
    public void StopAll()
    {
        foreach (var name in _instances.Select(i => i.Def.AnimName).Distinct().ToList())
            Stop(name);
        _effectTtls.Clear();
    }

    /// <summary>Escalates a destructible's visible damage to the stage its current HP now sits
    /// in, running its <c>DAMAGE_SEQUENCE</c> so the progressive-damage effect for that stage
    /// fires â€” the water tower's black smoke at â‰¤36, fire smoke at â‰¤18. Call it after the
    /// instance's HP changes (C23's weapon hit, or a debug poke).
    ///
    /// <para>The stage is how many of the cascade's descending <c>ANIM_HEALTH</c> thresholds the
    /// live HP has fallen past; the method only ever <b>escalates</b> â€” it runs the script only
    /// when a new, deeper threshold is crossed, and the script (an IF/ELSEIF chain C21 evaluates
    /// against the live value via <see cref="HealthOf"/>) then fires the deepest active branch,
    /// exactly one effect. The stage gate is what makes "each stage once" hold for <b>every</b>
    /// effect kind: a sustained smoke would be spared re-firing by <c>CALL_ANIMATION</c>'s own
    /// live guard, but a one-shot effect that finishes (C5's <c>damage3_mp1zreng11</c>) is not,
    /// and without the gate would re-fire on every hit inside a band.</para>
    ///
    /// <para>Returns whether it escalated. False means no change: the HP has not crossed a new
    /// threshold, or the destructible carries no <c>DAMAGE_SEQUENCE</c> (many just die outright,
    /// with no progressive stages). It neither decrements HP (C23) nor runs the death sequence
    /// (C24).</para></summary>
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
        // a single zero-dt advance resolves the whole IF chain and dispatches the chosen
        // CALL_ANIMATION. The effect it starts becomes its own live instance; this host is
        // discarded.
        var host = new AnimInstance(inst.Def, inst.Anchor);
        host.Runners.Add(new SequenceRunner(seq));
        host.Advance(this, 0f);
        return true;
    }

    /// <summary>Applies weapon damage to whatever destructible a struck world node belongs to, and
    /// escalates its visible damage. <paramref name="struck"/> is the raycast-hit collider (B15) â€”
    /// or any node under a destructible â€” resolved to the owning instance by walking up to the
    /// nearest registered anchor (compiled def preferred, <see cref="DestructibleRegistry.Resolve"/>).
    /// World destructibles carry HEALTH only, so <paramref name="healthDamage"/> (the weapon's
    /// <c>HEALTH_DAMAGE</c>) is the whole model â€” there is no armour pool (docs/formats/
    /// destructibles.md). Subtracts it, runs the damage stages, and marks the instance
    /// <c>Destroyed</c> at zero; the death <b>sequence</b> (the healthyâ†’destroyed swap, debris,
    /// fireball) is C24, so today a killed object just holds its final smoking stage. Returns true
    /// when the hit landed on a destructible (false for terrain/water/clutter); a no-op once
    /// destroyed.</summary>
    public bool DamageAt(Node? struck, float healthDamage)
    {
        var inst = _destructibles.Resolve(struck);
        if (inst == null)
            return false;
        if (inst.Status == DestructibleRegistry.State.Destroyed)
            return true;   // already dead â€” the death sequence (C24) owns it from here
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
                     $"HP {before:0.##}â†’{inst.Health:0.##}" +
                     (destroyed ? " DESTROYED â€” death sequence run" : $" [stage {inst.DamageStage}]"));
        }
        return true;
    }

    /// <summary>A plane <b>collision</b> with a world node (C27). Only the 44
    /// <c>WeaponOrCollideHit</c> destructibles â€” the Hollywood facades, the warehouse windows and
    /// <c>agyrobus</c> â€” take collision damage; a <c>WeaponHit</c> object (water tower, gate) is left
    /// untouched, so ramming it kills the plane and the object stands (decision 6: the 0.01 health
    /// marks these as fly-through set dressing). Returns true when the struck node is a
    /// <c>WeaponOrCollideHit</c> destructible â€” the caller then flies the plane THROUGH it â€” and false
    /// for everything else (a <c>WeaponHit</c> object, plain geometry, terrain), which the caller
    /// treats as a solid crash/graze. The damage itself runs through <see cref="DamageAt"/>, identical
    /// to a weapon hit, so the object's death (swap, debris, collider removal) is the same.</summary>
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

    /// <summary>Returns a destroyed destructible to healthy (C28) â€” for the debug tools (F40/F41) and
    /// respawn. The inverse of the death: (1) <see cref="Stop"/> the def's live death, tearing down its
    /// motions/puffers/fires; (2) restore the authored pose of any node the death physically MOVED â€”
    /// the ballistic debris pieces, whose motion Stop removes but leaves wherever they flew, so a
    /// re-destroy would launch from the wrong place; (3) re-apply the def's <c>RESET_STATE</c>, whose
    /// <c>OBJECT_ACTIVE_STATE</c> base states make the <c>healthy</c> subtree visible+collidable again
    /// and hide the <c>destroyed</c> one (<see cref="SetSubtreeActive"/> restores colliders with
    /// visibility, C25), undoing both the death swap and the <see cref="ApplyDeathSwap"/> fallback; and
    /// (4) restore the live HP pool. Idempotent â€” destroyâ†’resetâ†’destroy produces the same result each
    /// time.</summary>
    public void ResetDestructible(DestructibleRegistry.Instance inst)
    {
        var def = inst.Def;
        Stop(def.AnimName, inst.Anchor);
        // The damage-stage effects live on the EXTERNAL runtime and loop for as long as the
        // healthy node stays active â€” which a heal never interrupts, so the reset itself must
        // stop them. The names come from the def's own DAMAGE_SEQUENCE calls, never a hardcoded
        // list. (The external stop is per-name: with the shared-template collapse there is at
        // most one live instance of each.)
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

    /// <summary>The node's authored pose, remembered the first time anything moves it, so
    /// every pose op stays an offset from the rest pose rather than compounding.</summary>
    internal Transform3D RestOf(Node3D node)
    {
        if (!_rest.TryGetValue(node, out var rest))
            _rest[node] = rest = node.Transform;
        return rest;
    }

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

        // Colliders default to enabled, so an untracked root is treated as currently collidable;
        // walk the subtree only on a genuine crossing of the invisibility threshold.
        bool collidable = alpha > OpacityCollisionEpsilon;
        bool wasCollidable = !_opacityCollidable.TryGetValue(node, out bool tracked) || tracked;
        if (wasCollidable != collidable)
        {
            SetCollidersEnabled(node, collidable);
            GD.Print($"anim: fade {(collidable ? "restored" : "dropped")} colliders under '{node.Name}'");
        }
        _opacityCollidable[node] = collidable;

        int applied = ApplyOpacity(node, alpha);
        if (applied == 0 && !Mathf.IsEqualApprox(alpha, 1f))
            Count("ObjectOpacityState(no alpha path)");
    }

    private static string Sample(List<string> shown, int total) =>
        string.Join(", ", shown) + (total > shown.Count ? $", â€¦ (+{total - shown.Count} more)" : "");

    /// <summary>How many of a <c>DAMAGE_SEQUENCE</c>'s health thresholds <paramref name="hp"/> has
    /// fallen at or below â€” the object's current damage stage. Monotonic in falling HP, so it is a
    /// safe escalation gate.</summary>
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

    /// <summary>The HP a health condition first becomes true at as HP falls: <c>ANIM_HEALTH</c>'s
    /// operand, or a range's upper bound (its lower bound is left to <see cref="EvaluateCondition"/>
    /// when the cascade actually runs). Null for a non-health condition, which does not stage.</summary>
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

    private static string Describe(object? value) => value switch
    {
        null => "",
        Dictionary<string, object?> d => string.Join(",", d.Select(kv => $"{kv.Key}={kv.Value}")),
        _ => value.ToString() ?? "",
    };

    private static string NameOf(Node3D n) =>
        n.HasMeta(NameMeta) ? n.GetMeta(NameMeta).AsString() : n.Name.ToString();

    /// <summary>Where a world node visually IS, for effect siting and puffer emission. An
    /// absolute-modelled gamez subtree carries its vertices in world space under an identity node
    /// transform (WORLD-15), so its origin is the map corner, kilometers from the object â€” the
    /// damage-stage smoke measurably emitted there. When the node's own origin lies outside its
    /// subtree's world mesh bounds, the bounds centre is the honest position; a node whose origin
    /// sits inside them (a real transform: the waterfall, the train, debris pieces) keeps it
    /// exactly, so every effect that rendered correctly before is untouched. Meshless nodes have
    /// no bounds and keep their origin.</summary>
    private static Vector3 VisualOriginOf(Node3D node)
    {
        var box = UI.SelectionService.SubtreeWorldAabb(node);
        if (box.Size.LengthSquared() <= 1e-9f)
            return node.GlobalPosition;
        return box.Grow(1f).HasPoint(node.GlobalPosition) ? node.GlobalPosition : box.GetCenter();
    }

    /// <summary>A node's world position, valid DURING the bootstrap too. The world subtree
    /// is still detached while the bootstrap passes run (GameSession parents it after the
    /// build), and Godot's <c>GlobalPosition</c> both returns identity and logs an error for
    /// a node outside the tree â€” one line per evaluation, which is thousands. Accumulate the
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

    private static Vector3I CheckCellOf(Vector3 pos) => new(
        Mathf.FloorToInt(pos.X / RangeCheckCellSize),
        Mathf.FloorToInt(pos.Y / RangeCheckCellSize),
        Mathf.FloorToInt(pos.Z / RangeCheckCellSize));

    /// <summary>A node's world transform, valid DURING the bootstrap too â€” the same detached-subtree
    /// problem <see cref="WorldPos"/> solves, but keeping the basis so an AT_NODE offset still rotates
    /// into place. <paramref name="composed"/> reports whether the ancestor chain had to be walked
    /// (the world root not yet parented), so the caller can log the fallback rather than let Godot
    /// spam <c>!is_inside_tree()</c> and return an origin transform.</summary>
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

    /// <summary>
    /// Resolves a condition's node reference. The compiled form is a **1-based index into the
    /// definition's <c>nodes</c> support array** (mech3ax resolves indices to names for every
    /// other event kind but leaves these raw); the reader form is the name itself.
    ///
    /// Two negative sentinels exist: -100 <c>MAIN_ROOT_NODE</c> and -200 <c>INPUT_NODE</c>,
    /// both meaning "the node this definition was invoked on" = our anchor. They arrive as
    /// u32 (4294967196 / 4294967096) and the JSON layer parses numbers as float32, which
    /// cannot tell those two apart â€” hence the magnitude test rather than an equality check.
    /// It does not matter here: both resolve to the anchor.
    /// </summary>
    /// <summary>The AT_NODE / condition-node sentinels for "the node this definition was invoked
    /// on" â€” both resolve to the anchor (see <see cref="ConditionNode"/> for the u32/-sentinel
    /// detail).</summary>
    private static bool IsSelfNodeRef(string name) =>
        string.Equals(name, "INPUT_NODE", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "MAIN_ROOT_NODE", StringComparison.OrdinalIgnoreCase);

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

    // Whether this mesh's shader reads the opacity parameter — and if it does not, whether it
    // can be made to. Setting an instance parameter a shader does not declare is silently a
    // no-op in Godot, so without the check the "no alpha path" tally could never fire and
    // would be a lie rather than a diagnostic.
    //
    // Opaque world variants deliberately have no alpha path (see SceneBuilder.OpacityTerm) —
    // correct for the opacity-1.0 writes the bootstrap sends, but a genuine fade landing on
    // one rendered nothing: the kkgate wreck pieces stayed fully opaque through their authored
    // 3–6 s fades and vanished at deactivate. So a partial opacity installs a per-surface
    // override on THIS instance wearing the fade twin of the shared material (materials and
    // meshes are cached and shared across nodes, so the swap must never edit them), and
    // opacity 1 removes it again, returning the surface to the opaque pass. A twin is a
    // Duplicate(), so it detaches from any TextureCycler flipbook for the fade's duration.
    //
    // âš  Tests for the USE (`SceneBuilder.OpacityTerm`, i.e. " * csky_opacity"), not the uniform
    // NAME. Those used to be equivalent â€” the uniform was declared exactly in the
    // variants that multiplied by it â€” but the declaration has since moved into the shared
    // ordered preamble (csky_instance_uniforms.gdshaderinc), so it is now present in shaders
    // with no alpha path at all. Testing the name would report true for every one of them.
    // Testing the include line would be worse still: the declaration is no longer textually in
    // `sh.Code`, so a name test would report FALSE everywhere and quietly invert this tally.
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
        // The census covers the bootstrap passes only â€” after this method a miss is a runtime
        // event with its own reporting, not a statement about what the bind could reach.
        _censusOpen = ReportResolution;
        IndexWorld(worldRoot);
        long indexMs = sw.ElapsedMilliseconds;

        // Pass 0: the engine's own per-mission world setup, before any animation state. The
        // chapter gamez holds every mission's content and this script switches off what this
        // mission does not show (C1/IA1: hk_zep, both MP zeppelins, the CTF props, â€¦). Runs
        // first so an animation state can still override it, which is the engine's load order.
        Setup?.Apply((name, scope) => FindAll(name, scope), SetSubtreeActive);

        // Pass 1: base states. Anchored defs only â€” a def whose NAME matches nothing in this
        // world (player-plane anims, cutscene rigs) must not stomp globally-resolved bare
        // names like 'destroyed'.
        //
        // This is also where the destructible registry (C21) is built: a def with HEALTH > 0 is
        // a destructible, and each node its NAME resolves to is an independent instance with its
        // own mutable HP. Nothing damages them yet (C23), so this only changes where ANIM_HEALTH
        // reads its value from, not the value â€” a fresh world is unchanged.
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
                     (program.MissionLibrarySkipped.Count > 8 ? ", â€¦" : ""));
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
                     string.Join(", ", netHidden.Take(10)) + (netHidden.Count > 10 ? ", â€¦" : ""));
        ReportConditions();
        ReportRetargets();
        ReportUnhandled();
        _censusOpen = false;
    }

    // One census entry per definition identity (anchor name + animation name â€” AnimProgram's own
    // dedupe key), because the bootstrap asks for a def's anchors on more than one pass.
    private void RecordAnchoring(AnimDefinition def, AnchorKind how)
    {
        if (!_censusOpen || !_censusSeen.Add((def.Name, def.AnimName ?? "")))
        {
            return;
        }
        string label = def.AnimName is { Length: > 0 } anim ? $"{anim}@{def.Name}" : def.Name;
        switch (how)
        {
            case AnchorKind.ByName:
                _censusAnchored++;
                break;
            case AnchorKind.ByRootLift:
                _censusLifted++;
                if (_censusLiftedNames.Count < CensusCap)
                {
                    _censusLiftedNames.Add($"{label}â†’{def.RootName}");
                }
                break;
            case AnchorKind.LiftSuppressed:
                _censusSuppressed++;
                if (_censusSuppressedNames.Count < CensusCap)
                {
                    _censusSuppressedNames.Add($"{label}â†’{def.RootName}");
                }
                break;
            default:
                _censusUnanchored++;
                if (_censusUnanchoredNames.Count < CensusCap)
                {
                    _censusUnanchoredNames.Add(label);
                }
                break;
        }
    }

    // An event that named a node the bind could not reach. `why` separates the two causes, which
    // otherwise read the same: the compiled symbol table bound the name to a gamez node this build
    // never created, versus name resolution finding no match at all.
    private void RecordMissingTarget(AnimDefinition def, string refName, string why)
    {
        if (!_censusOpen)
        {
            return;
        }
        _censusMissing++;
        if (_censusMissingTargets.Count < CensusCap)
        {
            string label = def.AnimName is { Length: > 0 } anim ? $"{anim}@{def.Name}" : def.Name;
            _censusMissingTargets.Add($"{label} ref={refName} why={why}");
        }
    }

    /// <summary>Runs the two ambient-playback bootstrap passes â€” pass 2 (ACTIVATION ON_STARTUP
    /// definitions) and pass 3 (the mission's startanims, by ANIMATION_NAME, in list order) â€”
    /// and returns their tallies for the census. Extracted verbatim from <see cref="Bootstrap"/>
    /// so a quiet-stage bootstrap can defer them to <see cref="StartAmbient"/>.</summary>
    private (int StartupRun, List<string> Ran, List<string> Missing) RunAmbientPasses()
    {
        // Pass 2: ON_STARTUP definitions run for real (zepstate's roster is instantaneous
        // ObjectActiveStates, so this still settles on frame 0 for those).
        // A def carrying EXECUTION_BY_RANGE is authored to execute only near the player
        // (the C3 spiderweb's 50 m fade), so it defers to the proximity check instead of
        // firing blind at t=0. An unanchored one has no position to measure from and starts
        // immediately, like before.
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

        // Pass 3: the mission's start animations, by ANIMATION_NAME, in list order â€” each
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

    /// <summary>Is this definition already running on this anchor? (Instance identity is
    /// (definition, anchor) throughout.)</summary>
    private bool IsLive(AnimDefinition def, Node3D? anchor) =>
        _instances.Any(i => i.Def == def && i.Anchor == anchor);

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
            {
                TearDownResourcesOf(inst.Def, inst.Anchor);
                _inputNodes.Remove((inst.Def, inst.Anchor));
                ShowTemplate(inst.Def, visible: false);
            }
            // Start's seamless restart (tearDown false) keeps the entry, exactly as it keeps
            // the resources the new instance re-asserts.
            OnInstanceFinished?.Invoke(inst.Def, inst.Anchor);
        }
    }

    /// <summary>An input-governed effect instance (one <see cref="PlayEffectAt"/> gave a real
    /// site node and whose def loops on that node's active state) ends by its own authored exit
    /// â€” the NodeActive gate going false â€” not by Stop, so its sustained emitters would keep
    /// emitting past the sequence's end. Tear them down with the instance. Scoped to instances
    /// carrying an input node: ambient defs that finish leaving a resource alive keep today's
    /// behaviour.</summary>
    private void FinishInputGoverned(AnimDefinition def, Node3D? anchor)
    {
        if (_inputNodes.Remove((def, anchor)))
            TearDownResourcesOf(def, anchor);
    }

    /// <summary>Tears down every live resource a stopped instance created â€” its motions, puffers,
    /// lights and sounds â€” so nothing of the definition keeps running after <see cref="Stop"/>.
    /// Motions and puffers are attributed to the exact <c>(def, anchor)</c> that registered them
    /// (see <see cref="AddMotion"/> and <see cref="_puffers"/>). Lights and sounds are keyed by
    /// <c>(name, anchor)</c>, so they are cleared by anchor â€” the anchor is the instance identity,
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

    /// <summary>Applies a list of events with no clock â€” the RESET_STATE path, where every
    /// op is a base state and timed motions collapse to their end pose.</summary>
    private void ApplyInstant(List<AnimEvent> events, AnimDefinition def, Node3D? anchor)
    {
        foreach (var ev in events)
            Dispatch(ev, def, anchor, instant: true, out _);
    }

    // ---- the dispatch table ----
    /// <summary>
    /// Executes one event. Returns the event's duration in seconds via
    /// <paramref name="duration"/> â€” 0 for an instantaneous state change, the run time for a
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
                // it â€” the reader spells the whole thing as a three-event triple (declare the
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
                    // translucency is ENABLED and `opacity` the alpha while it is â€” settled by the
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
                    // A timed translucency fade â€” the single biggest un-handled event kind (9,917
                    // events install-wide, all trigger-gated OnCall/WeaponHit, so none fire at
                    // bootstrap). Unlike OBJECT_OPACITY_STATE, the endpoint `state` flag does NOT
                    // invert the value: (state=false, opacity=0) fades to invisible and
                    // (state=false, opacity=1) fades to opaque â€” surveyed across all 9,917 events
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
                    // different jobs. Rotation-only events (XYZ_ROTATION alone) are steady spins â€”
                    // zeppelin nacelle props (`spin`/`counterspin`, âˆ“40Â°/30Â°/s counter-rotating) and
                    // rotating signage â€” and every one of the 590 OnStartup events install-wide is
                    // exactly that shape, so the lightweight SpinMotion path below stays byte-for-byte
                    // what the ambient world boots with. The rest pair motion with
                    // GRAVITY/TRANSLATION/SCALE/FORWARD_ROTATION: ballistic debris and dust thrown by
                    // a kill or a CRASH â€” reachable only from OnCall/WeaponHit (2,900+ events, zero at
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
                            {
                                motion.Seek(0f); // RESET_STATE / zero-length: pose the launch start (rest)
                            }
                            else
                            {
                                AddMotion(motion, def, anchor);
                                BallisticMotionsLaunched++;
                            }
                            _opsApplied++;
                        }
                        // BOUNCE_SEQUENCE (re-launch a piece on ground contact) is a Layer-1.5 follow-up
                        // â€” it needs do_intersections + a ground ray; the pieces read fine tumbling to
                        // rest without it. Report it so --debug-anim shows it is deferred, not missed.
                        if (ev.Data.Has("bounce_sequence"))
                            Count("ObjectMotion(bounce_sequence deferred)");
                        duration = instant ? 0f : ballTime;
                        return true;
                    }
                    // No motion channel: either a steady spin (below) or a bare GRAVITY/BOUNCE stub
                    // with nothing to drive (meaningless without translation â€” reported, not acted on).
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
                    // reachable events leave it zero, so it is reported, not guessed â€” the same
                    // call Object3DRotate's ambiguous angle unit got in MissionSetup.
                    if (!spin.Vec3("delta").IsZeroApprox())
                        Count("ObjectMotion(rotation delta)");
                    if (rate.IsZeroApprox())
                        return true;

                    float spinFor = ev.Data.Num("run_time") ?? 0f;
                    foreach (var t in Targets(ev, def, anchor))
                    {
                        // Re-assertion is idempotent. These sit inside `Loop{-1}` sequences, so an
                        // already-turning prop would otherwise be rebuilt every frame â€” each rebuild
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
                // NOT IMPLEMENTED, and not counted as unhandled either (BL-212). SequenceRunner has
                // no StopSequence handling, so all 73 authored sites are silent no-ops — which is
                // why the rocket fireball's authored 0.3 s stop never runs and it emits to its TTL.
                // The semantics need a decode first: the data uses this both to reach a stopper
                // sequence nothing else calls and to halt a running one.
                return true;

            case "CallAnimation":
                // A call does NOT restart an animation that is already live on this anchor.
                // The data's poll idiom is `If <condition> â†’ CallAnimation; Endif; Loop{-1}`,
                // which re-issues the call on EVERY frame the condition holds â€” C1/MP1's
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
                    // one at a specific site â€” `CallAnimation{huge_30sec_fire, WithNode:
                    // rc*_dbase1}` burns one ship section. 29,633 WITH_NODE + 7,640 AT_NODE +
                    // 179 OPERAND_NODE call sites carry a target; ignoring it ran every one of
                    // them on the CALLER's anchor instead of where the data put it.
                    var (siteNode, siteOffset) = CallTargetSite(ev, def, anchor);
                    var callAnchor = siteNode ?? anchor;
                    // The world runtime can't render an effect template (its puffer factory is
                    // torn down after the build, WORLD-12). When a death sequence calls one of the
                    // named destruction/impact effects, hand it to the world-effects runtime (D32),
                    // which keeps textures open, stages the templates and relocates them onto the
                    // call site â€” and skip the local Start that would only build nothing.
                    if (ExternalEffect != null && callAnchor != null && IsInstanceValid(callAnchor))
                    {
                        var siteXform = callAnchor.GlobalTransform;
                        // VisualOriginOf, not the raw origin: an absolute-modelled call target's
                        // node origin is the map corner (WORLD-15), which placed the routed
                        // effect kilometers off-site. The resolved site node rides along too: it
                        // is the callee's INPUT_NODE (the local path below expresses that by
                        // anchoring the callee on it), and the effects runtime needs it for defs
                        // whose lifecycle reads that node.
                        if (ExternalEffect(callName, VisualOriginOf(callAnchor) + siteXform.Basis * siteOffset, siteNode))
                            return true;
                    }
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

            case "Sound":
                HandleSound(ev, def, anchor);
                return true;

            case "ObjectAddChild":
                // Only the sound-emitter three-quarters of this event is acted on â€” see
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

    private void HandlePufferState(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        if (ev.Data.Str("name") is not { } pufferName)
            return;
        // ACTIVE_STATE: 1 = start emitting, 0 = stop. The attach point is AT_NODE; note it is
        // NOT the event's "name" (that is the puffer's own name, a different namespace).
        bool on = (ev.Data.Num("active_state") ?? 0f) >= 1f;
        // AT_NODE INPUT_NODE / MAIN_ROOT_NODE are the sentinels for "the node this def was invoked
        // on" = the anchor (same rule ConditionNode applies). A destruction fire authored as
        // `PufferState(fire_n_smoke, at=INPUT_NODE)` thus emits on the effect's own relocated root,
        // which is what puts it at the call/hit site (D32) rather than nowhere.
        var host = ev.Data.Str("at_node") is { } atNode
            ? (IsSelfNodeRef(atNode) ? InputNodeOf(def, anchor) ?? anchor : ResolveOne(atNode, def, anchor))
            : anchor;
        if (host == null)
        {
            Count("PufferState(no host node)");
            return;
        }

        var key = (pufferName, host, DefScopedPufferKeys ? def : null);
        if (!on)
        {
            if (_puffers.TryGetValue(key, out var running))
            {
                running.Puffer.SustainEnd();
                _activePuffers.RemoveAll(a => a.Puffer == running.Puffer);
            }
            return;
        }
        if (_puffers.TryGetValue(key, out var existing))
        {
            // Re-asserting a RUNNING emitter is a no-op (the loop idiom). A SustainEnd'ed one
            // REVIVES instead: the damage-stage `puffit` loop cycles ACTIVE_STATE 0/1 on a 50%
            // dice every pass, and reading "stopped" as "still running" collapsed the authored
            // sputter to at most one burst per stage.
            if (!_activePuffers.Any(a => a.Puffer == existing.Puffer))
                _activePuffers.Add((existing.Puffer, host));
            return;
        }

        if (PufferFactory == null)
        {
            Count("PufferState(after build)");
            return;
        }
        var state = Effects.PufferState.FromAnimEvent(ev.Data);
        // A PUFFER_STATE carrying no textures is an adjust/stop stub that re-asserts a puffer
        // some other event defines â€” the readers have the same idiom, which is why
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
        if (DebugMotions && DefScopedPufferKeys)
        {
            foreach (var other in _puffers)
                if (other.Key.Name == pufferName && other.Key.Node == host && other.Value.Def != def)
                    GD.Print($"anim: puffer '{pufferName}' on '{NameOf(host)}' builds beside "
                             + $"'{other.Value.Def.AnimName}''s emitter [def {def.AnimName}]");
        }
        _puffers[key] = (puffer, def, anchor);
        _activePuffers.Add((puffer, host));
        _opsApplied++;
        PuffersBuilt++;
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
            var offset = HostOffsetOf(node, xform);
            puffer.SustainAt(offset == Vector3.Zero ? xform.Origin : xform * offset, xform.Basis, dt);
        }
    }

    /// <summary>The host's emission point in its own frame, cached per node. Zero â€” and the
    /// emission point exactly the node origin, byte-identical with the pre-cache behaviour â€” for
    /// every node whose origin sits inside its mesh bounds; the offset to the bounds centre for
    /// absolute-modelled world subtrees, whose origin is the map corner (WORLD-15). Local-frame,
    /// so a motion-driven host carries its emission point along.</summary>
    private Vector3 HostOffsetOf(Node3D host, in Transform3D xform)
    {
        if (_hostOffsets.TryGetValue(host, out var offset))
            return offset;
        var visual = VisualOriginOf(host);
        offset = visual == xform.Origin ? Vector3.Zero : xform.AffineInverse() * visual;
        _hostOffsets[host] = offset;
        if (DebugMotions && offset != Vector3.Zero)
            GD.Print($"anim: puffer host '{NameOf(host)}' origin {xform.Origin} is outside its mesh "
                     + $"bounds â€” emitting at {visual}");
        return offset;
    }

    /// <summary>The stored call-site node for a live instance, when it is still valid.</summary>
    private Node3D? InputNodeOf(AnimDefinition def, Node3D? anchor) =>
        _inputNodes.TryGetValue((def, anchor), out var node) && IsInstanceValid(node) ? node : null;

    /// <summary>Whether any of the def's sequences conditions on the INPUT_NODE sentinel's active
    /// state â€” the authored "run while my host stands" lifecycle (the damage-stage sputters'
    /// <c>If NodeActive â†’ Loop</c>). Cached; the answer is a property of the data.</summary>
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
        GD.PushWarning($"anim: {kind} '{name}' requested after the world build and {why} â€” "
                       + "it will be silent for the rest of the session");
    }

    /// <summary>
    /// The emitter an event's NAME refers to, or null when the name isn't one this definition
    /// declared. This is what lets OBJECT_ACTIVE_STATE and OBJECT_ADD_CHILD â€” both perfectly
    /// ordinary node events elsewhere â€” address a sound emitter without either handler having to
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
    /// three-event triple â€” <c>SOUND_NODE{snd_waterfall}</c>, <c>OBJECT_ACTIVE_STATE{snd_waterfall,
    /// ACTIVE}</c>, <c>OBJECT_ADD_CHILD{waterfall01, snd_waterfall}</c> â€” so this event only
    /// declares, and the other two arrive as their own dispatches. A compiled event carries
    /// <c>active_state</c> and <c>translate</c> inline, but only sometimes: measured install-wide,
    /// <c>translate</c> is an <c>AtNode</c> on 379 events and null on exactly 865 â€” and 865 is also
    /// exactly the number of <c>OBJECT_ADD_CHILD</c> events that attach a sound definition. The two
    /// halves are one mechanism, which is why they land together.
    /// </summary>
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
            // alive with `[SOUND_NODE, â€¦, Loop{-1}]` exactly as it does for puffers.
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

        // AT_NODE â€” the compiled form's own placement. Absent in the reader form and in the 865
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
        // is numeric â€” reading it with Num() alone returns null for `true` and leaves every emitter
        // in the world switched off, which is exactly what it did until the --debug-anim log showed
        // 38 correctly-placed emitters all reading "off".
        if (ev.Data.Has("active_state"))
            Sounds.SetActive(handle, ev.Data.Bool("active_state") || ev.Data.Num("active_state") >= 1f);
    }

    /// <summary>
    /// A one-shot <c>SOUND</c> event â€” the fire-and-forget destruction/impact/damage audio a sequence
    /// emits (<c>air_mixed_exp_sg</c> when a building is struck, <c>snd_gasbagexp1</c> on a zeppelin
    /// kill). Distinct from <c>SOUND_NODE</c>'s pooled looping emitters: it plays once at a world
    /// point and disposes itself (<see cref="WorldSounds.PlayOneShot"/>).
    ///
    /// The event's NAME is a sounds.json definition or a <c>SOUND_GROUPS</c> name â€” NOT a gamez node
    /// (the recorded C3 gotcha: the lone reader-scope one-shot names <c>snd_waterfall</c>, a
    /// definition, and resolves zero node targets). The node, when present, is the AT_NODE that
    /// positions it; absent, it plays at the anchor.
    /// </summary>
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

    /// <summary>Where a one-shot SOUND plays: its AT_NODE's world pose plus the trailing offset, or
    /// the anchor's when it names no node. The compiled form nests AT_NODE as <c>{name, pos}</c>; the
    /// reader form (normalized in <see cref="AnimDefs"/>) carries a flat <c>at_node</c> name plus a
    /// <c>translate</c> offset.</summary>
    private Vector3 OneShotSoundPosition(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        Node3D? host = null;
        Vector3 offset = Vector3.Zero;
        if (ev.Data.Obj("at_node") is { } atObj)
        {
            if (atObj.Str("name") is { } hostName)
            {
                host = ResolveOne(hostName, def, anchor);
            }
            offset = atObj.Vec3("pos");
        }
        else if (ev.Data.Str("at_node") is { } atName)
        {
            host = ResolveOne(atName, def, anchor);
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

    /// <summary>
    /// The sound-emitter case of OBJECT_ADD_CHILD: attach a declared emitter to the world node
    /// that positions it. Returns false for every other use, which stays counted as unhandled.
    ///
    /// This is deliberately only the sound subset. Surveying all 1,152 <c>ObjectAddChild</c> events
    /// found 865 (75%) attach sound *definitions* rather than nodes (<c>snd_zepengine</c>â†’spin
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

    /// <summary>
    /// Applies one LIGHT_STATE. The load-bearing detail is that this is a **partial update**:
    /// the fire and refinery flickers are streams of <c>{name, range}</c> events 0.03â€“0.07 s
    /// apart that must leave position, colour and active state untouched (the compiled form
    /// spells the absent fields null â€” measured install-wide: translate null on 707 of 1468
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

        // AT_NODE arrives as translate:{AtNode:{name, pos}} â€” node plus a local offset, the same
        // shape (and the same frame) as a puffer's AT_NODE.
        if (ev.Data.Obj("translate")?.Obj("AtNode") is { } at)
        {
            // Resolve the host ONCE per light. These events are not occasional: a fire's flicker
            // re-issues its full LIGHT_STATE â€” AT_NODE and all â€” every loop iteration, measured
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
    /// <c>run_time</c>. They are deltas, not targets â€” C1B's <c>ap_light</c> pulse runs
    /// {min +50, max +160} over 0.1 s and then {min âˆ’50, max âˆ’160} over 0.05 s, and a negative
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

    /// <summary>Resolves a single node name for this definition â€” the compiled symbol table
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
    /// the call â€” that is the pre-change behaviour, so a node the builder skipped can't make
    /// an effect disappear â€” but it is counted, since silently mis-placing an effect is
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
            offset = atNode.Vec3("position"); // absent â†’ zero
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
        // its calls every frame, so an unconditional line here would bury the log â€” the same
        // reason condition logging prints only first-evaluation and verdict flips.
        if (DebugMotions && _retargetsLogged.Add($"{ev.Data.Str("name")}|{targetName}|{def.AnimName}"))
            GD.Print($"anim: retarget '{ev.Data.Str("name")}' onto '{targetName}' "
                     + $"({(resolved != null ? resolved.GetMeta(NameMeta).AsString() : "UNRESOLVED")})"
                     + $" [caller {def.AnimName}]");
        return (resolved, offset);
    }

    /// <summary>Moves an effect template's own root(s) to a call site so its puffers â€” which
    /// ride that root (<c>yellow_spark_01</c>, <c>fly_trailN</c>, â€¦), NOT the caller's anchor â€”
    /// emit there instead of at the template's gamez origin. This is the template-instancing the
    /// original does by copying the template mesh per call; here the single shared template is
    /// relocated, so overlapping calls to the same template collapse onto the last site (the
    /// staggered-cluster nuance is a fidelity follow-up). Only the world position is set â€” the
    /// puffers key off the host origin â€” and the offset is applied in the site's own frame.</summary>
    private void PlaceTemplateAt(AnimDefinition callee, Node3D site, Vector3 offset) =>
        PlaceTemplateAt(callee, site.GlobalTransform.Origin + site.GlobalTransform.Basis * offset);

    /// <summary>Moves an effect template's own root(s) to an absolute world point â€” the
    /// <see cref="PlayEffectAt"/> path, where the site is a hit/death coordinate rather than a
    /// world node.</summary>
    /// <summary>Shows or hides the effect-template root(s) a definition anchors on, when this
    /// runtime stages its templates hidden (<see cref="ShowPlacedTemplates"/>). Paired with the
    /// effect's life, not its instance: hiding on instance-finish would cut the ring off mid-flight,
    /// because the authored scale/opacity motions outlive the sequence that launched them.</summary>
    private void ShowTemplate(AnimDefinition def, bool visible)
    {
        if (!ShowPlacedTemplates)
            return;
        foreach (var root in Anchors(def))
            if (root != null && IsInstanceValid(root))
                root.Visible = visible;
    }

    private void PlaceTemplateAt(AnimDefinition callee, Vector3 origin)
    {
        foreach (var root in Anchors(callee))
        {
            if (root == null || !IsInstanceValid(root))
                continue;
            var xf = root.GlobalTransform;
            xf.Origin = origin;
            root.GlobalTransform = xf;
        }
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
            // Stop (not just drop the instance): tears down the sustained puffers this effect
            // created, so a stop-less emitter stops emitting and its live particles decay.
            Stop(_effectTtls[i].Def.AnimName, _effectTtls[i].Anchor);
            _effectTtls.RemoveAt(i);
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

    /// <summary>
    /// Evaluates one IF/ELSEIF condition. Every kind the data uses is answerable â€” an
    /// earlier reading held them to be opaque gameplay state and skipped every branch, which
    /// meant the refinery/dock/lighthouse light sequences never ran at all. Semantics and the
    /// evidence for each are in docs/formats/anim-definitions.md; the two units that bite are
    /// that compiled <c>PlayerRange</c> is metres SQUARED (reader 270 â†’ compiled 72900) and
    /// that compiled <c>AnimHealth</c> is a "damaged down to" threshold, so an undamaged
    /// object fails it.
    ///
    /// An unparseable or unknown condition returns false â€” the old skip-the-branch behaviour,
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
            // these stay uniformly false â€” the pre-C21 behaviour, unchanged.
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
        // time it is seen and thereafter only when its verdict FLIPS. The poll idiom â€”
        // `If â€¦ Endif Loop{-1}` â€” re-evaluates every frame, so logging every evaluation would
        // bury the log; logging the transitions is what you actually want to read (this is
        // how "the player came within 25 m and the rearm door fired" shows up).
        if (DebugMotions && Flipped(kind, anchor, result))
        {
            var at = anchor == null ? "<global>"
                : $"{NameOf(anchor)} {WorldPos(anchor).Snapped(Vector3.One)}";
            GD.Print($"anim/debug: cond {kind}({Describe(value)}) on {at} " +
                     $"[player {PlayerPos().Snapped(Vector3.One)}] â†’ {(result ? "TRUE" : "false")}");
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

    /// <summary>Restores every node a def's events touched to its authored rest pose (<see cref="_rest"/>,
    /// recorded the first time a motion disturbed it). The membership check confines this to nodes that
    /// actually MOVED â€” the ballistic debris and any <c>FROM_TO</c> movers â€” so healthy/destroyed
    /// visibility nodes (never transformed) are left to <c>RESET_STATE</c>.</summary>
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

    /// <summary>Runs a destructible's death sequence (C24) the instant its HP reaches zero: the
    /// healthyâ†’destroyed <c>OBJECT_ACTIVE_STATE</c> swap, the debris sequences and the puffer
    /// calls. Those ARE the definition's own Initial sequences â€” the def's <c>anim_name</c> is the
    /// destruction (<c>h2twr_destruction1</c>, <c>destroy_mp1zreng11</c>), so its animation is the
    /// death â€” which is why this plays them ALL through <see cref="Start"/> rather than trying to
    /// pick out "the death sequence": that swap lives in a sequence whose name varies wildly
    /// (<c>destroyit</c>, <c>destroy_h2twr</c>, or unnamed) and is NEVER reliably <c>unknown_seq</c>
    /// (docs/formats/destructibles.md), but is always <c>Initial</c>, so Start reaches every case.
    /// The <c>DAMAGE_SEQUENCE</c> among them just re-fires the final smoke stage idempotently (its
    /// effect is already live), which is also what a one-shot kill wants. Every event kind the death
    /// emits now runs â€” the swap, the debris ballistic <c>OBJECT_MOTION</c>, the puffer calls, and
    /// the one-shot <c>Sound</c> (<see cref="HandleSound"/>); the safety net that hides
    /// <c>destroyed</c> subtrees only runs at bootstrap, so it does not fight this.
    ///
    /// <para>Then <see cref="ApplyDeathSwap"/>: ~10% of destructibles (the C1 AA guns) carry the
    /// healthy/destroyed node pair but author NO swap in their sequences, so Start alone leaves
    /// them standing. The swap is derived from the def's own RESET_STATE â€” the base state that
    /// declared the pair â€” flipping the healthy/destroyed/dbase roles it named; idempotent for the
    /// 90% Start already swapped.</para></summary>
    private void RunDeathSequence(DestructibleRegistry.Instance inst)
    {
        Start(inst.Def, inst.Anchor);
        ApplyDeathSwap(inst);
    }

    /// <summary>The generic healthyâ†’destroyed swap, for destructibles that declare the pair but
    /// author no explicit swap sequence (the AA guns). Read off the def's own RESET_STATE
    /// <c>OBJECT_ACTIVE_STATE</c> targets â€” never a world-wide name scan (that is the
    /// <c>ref_tank_dest</c> bug in <see cref="HideUncoveredDestroyed"/>) â€” and applied only when
    /// RESET names a <c>destroyed</c> node, so an object with no destroyed variant (a mission gun
    /// that dies by effect alone) is left intact rather than blanked. Matches the exact role words,
    /// not a <c>_dest</c> suffix.</summary>
    private void ApplyDeathSwap(DestructibleRegistry.Instance inst)
    {
        if (inst.Def.ResetState is not { } reset)
            return;
        static string RoleName(AnimEvent ev) => ev.Data.Str("node") ?? ev.Data.Str("name") ?? "";
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

    private Node3D? ConditionNode(object? reference, AnimDefinition def, Node3D? anchor)
    {
        if (reference is string name)
            return IsSelfNodeRef(name) ? anchor : ResolveOne(name, def, anchor);
        if (AnimData.AsNum(reference) is not { } idx)
            return null;
        if (idx > 1e9f)
            return InputNodeOf(def, anchor) ?? anchor; // negative sentinel = INPUT_NODE
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
            .Select(kv => $"{kv.Key} {kv.Value.True}âœ“/{kv.Value.False}âœ—");
        GD.Print($"anim: conditions evaluated (lod {QualityLod}): {string.Join(", ", parts)}");
    }

    private void Count(string kind) =>
        _unhandled[kind] = _unhandled.TryGetValue(kind, out var n) ? n + 1 : 1;

    private void ReportUnhandled()
    {
        if (_unhandled.Count == 0)
            return;
        var top = _unhandled.OrderByDescending(kv => kv.Value).Take(12)
            .Select(kv => $"{kv.Key}Ã—{kv.Value}");
        GD.Print($"anim: {_unhandled.Count} event kind(s) not yet acted on: {string.Join(", ", top)}"
                 + (_unhandled.Count > 12 ? ", â€¦" : ""));
    }

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
            GD.Print($"anim/debug: â€¦ and {_motions.Count - 12} more");
    }

    /// <summary>Registers a motion, replacing any motion already driving the same node. An
    /// object has exactly one motion in the original, and the data relies on it: C1/IA1's
    /// startanims run `hangar3_doors` (doors to Â±50) and then `mp_hangar3_open` (the same
    /// doors to Â±25), where the later one is meant to win. Without this both tween the same
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

    // ---- node resolution (unchanged from the start-state applier) ----
    /// <summary>World nodes a definition anchors to: NAME matches (wildcards = one per
    /// building/vehicle instance); else ANIMATION_ROOT_NAME matches lifted to their parent
    /// (the instance root, so sibling healthy/destroyed both resolve locally â€” needed where
    /// instance roots have free names: `m_build**` instances are `apbuild01.flt`â€¦).</summary>
    private List<Node3D?> Anchors(AnimDefinition def)
    {
        // Multi-target NAME1 definitions (zeppelin nacelles/turrets) parse with an empty
        // NAME; they animate per-object sub-parts and are object-wiring scope â€” never anchor
        // them (their generic ROOT names would anchor them onto every building in the world).
        if (string.IsNullOrEmpty(def.Name))
            return new List<Node3D?>();
        var anchors = FindAll(def.Name, null).Cast<Node3D?>().ToList();
        var how = anchors.Count > 0 ? AnchorKind.ByName : AnchorKind.None;
        if (anchors.Count == 0 && def.RootName != null)
        {
            var roots = FindAll(def.RootName, null);
            if (roots.Count > 0 && roots.Count <= MaxRootLift)
            {
                if (SuppressRootLift)
                {
                    how = AnchorKind.LiftSuppressed;
                }
                else
                {
                    anchors = roots
                        .Select(n => n.GetParent() as Node3D)
                        .Where(p => p != null)
                        .Distinct()
                        .ToList();
                    how = anchors.Count > 0 ? AnchorKind.ByRootLift : AnchorKind.None;
                }
            }
        }
        RecordAnchoring(def, how);
        return anchors;
    }

    /// <summary>The world nodes one event targets. Reader-sourced events may carry a
    /// parentâ†’child path; compiled events name a single node (under "node" or "name",
    /// which upstream spells inconsistently per event type).</summary>
    private List<Node3D> Targets(AnimEvent ev, AnimDefinition def, Node3D? anchor)
    {
        // Compiled definitions carry a symbol table binding each referenced name to an exact
        // gamez node index â€” always prefer it. Name matching resolves C1's `caboose` to the
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
                // One narrow rescue first: a generic exploder template (genx12) is a parentless
                // root the world never builds, whose pt* symbol-table entries point at its own
                // meshless parameter nodes — placeholders for the same-named pieces under the
                // node a CALL_ANIMATION re-anchored it onto (kkgate's `destroyed` wreck). Resolve
                // strictly inside the anchor's subtree, never globally (the caboose ambiguity).
                if (anchor != null && FindAll(refName, anchor) is { Count: > 0 } scoped)
                    return scoped;
                _opsUnresolved++;
                RecordMissingTarget(def, refName, "index-not-built");
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
        {
            _opsUnresolved++;
            RecordMissingTarget(def, string.Join("/", path), "name-no-match");
        }
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
    /// subtree. A full scan of the node index â€” and the data calls it constantly, because the
    /// poll idiom (<c>If â€¦ CallAnimation; Endif; Loop{-1}</c>) re-dispatches its body every
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
            // gamez node 'ap_radiotwr.flt') â€” try both.
            var matches = match(srcName)
                || (srcName.EndsWith(".flt", StringComparison.OrdinalIgnoreCase) && match(srcName[..^4]));
            if (matches && (scope == null || node == scope || scope.IsAncestorOf(node)))
                result.Add(node);
        }
        _findCache[key] = result;
        return result;
    }

    // Wildcard NAME â†’ predicate: '*' (and the '**' template form) match any run of
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

    // The *_STATE poses use the same absolute-in-parent-frame convention as
    // OBJECT_MOTION_FROM_TO â€” see FromToMotion's remarks for the evidence. OBJECT_TRANSLATE_STATE
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
    // hide it and report â€” each name is a data-coverage gap (a def we failed to anchor).
    // Match 'destroyed' only: a '_dest' suffix rule proved WRONG â€” C1's `ref_tank_dest` is
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
