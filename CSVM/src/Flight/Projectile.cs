using System.Collections.Generic;
using CSVM.Effects;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Flight;

/// <summary>
/// The weapon-fire subsystem for a shared world: a pool of projectiles integrated with the data's
/// own ballistics (<c>VELOCITY</c>/<c>ACCELERATION</c>/<c>GRAVITY</c>, expiring at <c>RANGE</c>),
/// plus their visuals and impacts — tracer streaks, muzzle flashes, the per-surface <c>IMPACT</c>
/// sound, and the named IMPACT effect: its <c>ANIMATION</c> model is instanced at the hit point when
/// it is a chapter-gamez prototype (the water splash), else a stand-in spark sprite shows. Guns and
/// hardpoints feed it through <see cref="Spawn"/>; it runs itself
/// each physics frame. One pool serves every player (projectiles live in the shared world, so every
/// splitscreen pane sees them).
///
/// <para>Hit detection is a per-step world raycast (B15). The flying aircraft has no physics body,
/// so a round never hits its own launcher and the <c>player</c>/<c>enemy</c> IMPACT classes are
/// unreachable in M3 — only <c>default</c>/<c>water</c>/<c>buildings</c> occur, chosen from the
/// struck collider's <see cref="SceneBuilder.SurfaceMeta"/> tag.</para>
/// </summary>
public sealed partial class ProjectilePool : Node3D
{
    /// <summary>Where a hit's damage goes (C23): given the struck collider and the weapon's
    /// <c>HEALTH_DAMAGE</c>, apply it to the destructible that collider belongs to. Wired to
    /// <c>AnimRuntime.DamageAt</c> in flight; null when there is no destructible system (the static
    /// viewer, a chapter with no anim runtime), where impacts stay purely cosmetic.</summary>
    public System.Func<Node?, float, bool>? DamageSink;

    /// <summary>Plays a named IMPACT effect (its puffer half) at a hit point through the world-effects
    /// runtime (D32): the gun/rocket smoke and fireballs whose <c>ANIMATION</c> is an ON_CALL effect
    /// def rather than a gamez model. Null in views with no anim runtime. Gated to non-gun weapons —
    /// the <c>gunhit</c> smoke has no stop event, so a shared per-round emitter would collapse onto one
    /// jumping, ever-emitting puff (a documented guns follow-up); rockets/ordnance fire ≤1/s.</summary>
    public System.Action<string, Vector3>? EffectSink;

    internal const float WorldGravity = PhysicsConstants.NomGravity; // only the 5 GRAVITY rockets use it
                                                                     // (shared: FlightController's reticle integration reads it too)
    internal const float RocketSpeedScale = 1f; // dev scale for rocket flyout speed (weapons.rocketSpeedScale);
                                                // 1.0 = neutral. Rocket feel is a pending playtest A/B — scales
                                                // both launch velocity and acceleration together so the whole
                                                // profile stays proportional and the round still expires at RANGE.
                                                // C25 retune toward the reference shots' short discrete dashes (was 3m/0.156m, read as a long
                                                // glowing streak) — magnitudes are TUNE (BL-202), owed the cockpit A/B. C23 routes all three
                                                // through Config (weapons.tracerLength/tracerWidth/tracerBrightness) so the user can retune the
                                                // look without a rebuild; these consts are only the defaults now (Config.GetFloat falls through
                                                // to them verbatim with no config.json, so the defaults stay byte-identical for goldens).
    internal const float TracerLength = 1.0f;   // streak length behind the round, m — TUNE (BL-202)
    internal const float TracerWidth = 0.10f;   // m — TUNE (BL-202)
    // Additive blending with no glow/bloom pass caps a tracer at the texture's own pixel value, which
    // read visibly dimmer than the reference captures' near-white core — an overbright multiplier (>1,
    // clipped by the additive blend itself) is the only lever available without a bloom pipeline.
    // Uniform across channels so it brightens rather than recolours the per-ammo texture's own hue.
    internal const float TracerBrightness = 3.0f; // TUNE (BL-202)
    // C23 distance-visibility floor: the minimum screen footprint (px) a tracer's drawn width/length
    // are allowed to shrink below at range, so a round many hundred metres out still reads as a
    // fleck instead of vanishing into sub-pixel geometry (the original screenshots show distant fire
    // as visible streaks). 0 disables the floor outright. Off the default screenshots/goldens (none
    // fire a weapon) so this never moves a golden hash — magnitude is TUNE (BL-210), owed the
    // cockpit A/B via weapons.tracerMinPixels.
    internal const float TracerMinPixels = 2.0f;

    private const int MaxProjectiles = 1024;
    private const int MaxFlashes = 128;
    private const float RocketStreakScale = 2.4f; // fatter/longer streak, the fallback when a rocket has
                                                  // NO FLYOUT model (a chapter missing the prototype)
    private const float RocketExhaustScale = 0.5f; // a slim exhaust streak behind a rocket that HAS a
                                                   // MODEL body (B14): the body is the round, this is its trail
    private const float MuzzleSize = 0.5f;    // m
    private const float MuzzleLife = 0.05f;   // s
    // The authored flash is one `mb_spinflame` node the def rolls to a random Z angle each shot
    // (RANDOM_WEIGHT over 30/80/140 degrees, muzzle_burst.zrd.json) rather than a fixed pose — the
    // reference captures (MuzzleFlash1-3.png) read as a 3-lobed burst, so three quads 120 degrees
    // apart reproduce that shape, with the whole triad sharing one random per-shot roll (continuous,
    // not the def's 3-bucket discrete roll — TUNE if the discrete cadence reads better at the
    // controls).
    private const int MuzzleFlashCount = 3;
    private const float ImpactSize = 3.0f;    // m
    private const float ImpactLife = 0.14f;   // s
    private const float ImpactModelLife = 0.4f; // s the instanced IMPACT model shows before it is freed
    // The stand-in explosion burst a hardpoint weapon shows when no world-effects runtime is present
    // to render its real fireball (a scene-less pool: the weapon lab, a chapter with no world scene).
    private const int ExplosionSprites = 7;
    private const float ExplosionSize = 12f;  // m
    private const float ExplosionLife = 0.5f; // s
    private const float ExplosionSpread = 6f; // m — the cluster radius

    // The authored muzzle smoke (muzzle_burst's `muzzlepuffer`): smoke101–103, aft 20 m/s in the
    // muzzle frame, size 0.3–0.6 m, life 0.1–0.2 s, ±0.8 m/s random velocity, 5 cm deviation.
    // The data emits every 0.05 s over a 0.3 s window from the moving muzzle node; the per-shot
    // puff count here is the gloss of that window (TUNE) — the authored ranges are verbatim.
    private const int MuzzleSmokePuffs = 0;            // TUNE (authored window: 6 puffs / 0.3 s)
    private const float MuzzleSmokeAftSpeed = 20f;     // m/s, local_velocity z
    private const float MuzzleSmokeDeviation = 0.05f;  // m, deviation_distance
    private const float MuzzleSmokeRandVel = 0.8f;     // m/s, min/max_random_velocity
    private const float MuzzleSmokeSizeMin = 0.3f, MuzzleSmokeSizeMax = 0.6f;   // m
    private const float MuzzleSmokeLifeMin = 0.1f, MuzzleSmokeLifeMax = 0.2f;   // s

    // The casing's white puff cluster (the original ejects ~5–6 persisting, aft-drifting white
    // puffs with each shell — the reference captures). Unmatched to any shipped effect def after
    // a genuine search (only muzzle_burst references gunshell, and the gunshell def is motion-only,
    // no puffer), so this cluster is a hand-authored stand-in judged against the captures — TUNE.
    private const int EjectPuffs = 5;
    private const float EjectPuffSpread = 0.4f;   // m, cluster radius at spawn
    private const float EjectPuffDrift = 1.0f;    // m/s random drift
    private const float EjectPuffSink = 1.5f;     // m/s downward drift
    private const float EjectPuffSizeMin = 0.5f, EjectPuffSizeMax = 0.9f;  // m
    private const float EjectPuffLifeMin = 1.2f, EjectPuffLifeMax = 2.0f;  // s — the puffs persist

    private const int MaxSmoke = 256;   // the persisting eject puffs dominate this pool

    // The ejected shell casing (gunshell): one pooled chapter-gamez instance per shot, flying the
    // def's own OBJECT_MOTION. Per-shot instances so sustained fire never drops an ejection — a
    // shared anchor under AnimRuntime's already-live gate would swallow all but one per RUN_TIME.
    private const int MaxCasings = 128;

    // The muzzle light flash (muzzle_burst's `3rdperson_lts`): a real dynamic light per shot,
    // range/colour from the def's 3-way RandomWeight variants. The data deactivates it on the next
    // event tick, so the flash lives ~2 frames here; energy is ours to pick (TUNE).
    private const int MaxMuzzleLights = 8;
    private const float MuzzleLightLife = 0.03f;   // s
    private const float MuzzleLightEnergy = 2.5f;  // TUNE — the def carries range/colour only

    // A dirt impact's tumbling-debris burst: a few small chips that fly outward and arc under
    // gravity, in place of the single 3 m stand-in spark. Count/size/speed/spin are TUNE; the
    // life leans on the gunhit def's own debris OBJECT_MOTIONs (bit1 RUN_TIME 1 s, bit2/3/chunk
    // 2 s). Drawn on the authored chip textures below, alpha-blended — through the additive
    // muzzle-flash-textured impact pool they read as a small flame, not debris (BL-203).
    private const int DirtDebrisSprites = 5;
    private const float DirtDebrisSize = 0.45f;   // m
    private const float DirtDebrisLife = 0.9f;    // s
    private const float DirtDebrisSpeed = 4f;     // m/s launch speed
    private const float DirtDebrisSpreadDeg = 60f; // cone half-angle around the surface normal
    private const float DirtDebrisSpinMax = 25f;  // rad/s, random per-chip tumble rate

    // A gun hit on a buildings-classed surface: a ricochet spark burst. Both authored assets are
    // confirmed missing from the install (`bld_damage.flt` and the `rcochet1` EFFECT are 2 of the
    // 5 referenced-but-undefined names — weapon-effects.md), so this stand-in is judged by eye:
    // fast bright sparks flying off the wall plus the flash. Count/size/speed/life are TUNE.
    private const int RicochetSparks = 8;
    private const float RicochetSparkSize = 0.55f;  // m
    private const float RicochetSparkLife = 0.55f;  // s
    private const float RicochetSparkSpeed = 22f;   // m/s launch speed
    private const float RicochetSpreadDeg = 90f;    // cone half-angle around the surface normal

    // The authored water-splash playback (splash1.zrd.json / bsplsh.zrd.json — identical shapes):
    // the instanced model's `*_base` disc scales xz 1→2 over 0.2 s then eases back to 1.8 over
    // [1.0,2.0] s, while the `*_splash` column pops to its authored scale (1,100,1) and collapses
    // to zero over the 2 s run (OBJECT_MOTION SCALE initial+delta·u — MotionRuntime's decode).
    // Values verbatim from the defs; NOT rendered from them: the 0.05 s opacity fade-in / 1 s
    // fade-out (partial opacity on world materials needs AnimRuntime's opacity-twin path) and the
    // OBJECT_CYCLE_TEXTURE splash01→03 flipbook — the scale animation is what makes it visible.
    private const float SplashRunTime = 2.0f;      // s, the def's sequence length
    private const float SplashBaseGrowTime = 0.2f; // s, base 1→2
    private const float SplashBaseEaseStart = 1.0f; // s (0.2 + EVENT_OFFSET 0.8), 2→1.8 over 1 s
    private const float SplashBaseMax = 2.0f, SplashBaseEnd = 1.8f;
    private const float SplashColumnScale = 100f;  // the column's authored initial Y scale
    // TUNE: the authored splash1_splash quad is 5 cm wide — sub-pixel beyond ~30 m, it dilutes
    // to an invisible grey sliver however bright the texture. The reference's ticks measure
    // ~0.35 m wide (Water Splash.png), so the column's x/z are widened toward that; height
    // stays the authored curve.
    private const float SplashColumnWidthScale = 8f;

    private static readonly Color RicochetTint = new(1f, 0.95f, 0.6f); // white-hot spark yellow
    private static readonly Color DirtTint = new(1f, 1f, 1f); // the bit textures carry the colour
    private static readonly Color MuzzleSmokeTint = new(0.85f, 0.85f, 0.85f);
    private static readonly Color EjectPuffTint = new(1f, 1f, 1f);

    // Muzzle-flash sprite tint (C24) — unrelated to the tracer tint below, which is a separate,
    // uniform overbright multiplier so each ammo's own tracer texture colour shows through unshifted.
    private static readonly Color SlugTint = new(1.0f, 0.85f, 0.35f);   // warm yellow
    private static readonly Color RocketTint = new(1.0f, 0.6f, 0.25f);  // orange exhaust

    // The muzzle-flash ammo-type axis (weapon-effects.md "Muzzle & tracer textures"): each
    // chapter's texture archive carries a `{slug,dum,ap,mag}_muzzle1` per ammo type. The index into
    // this array is resolved once per weapon from its FIRE ANIMATION binding (MuzzleAmmoIndex) —
    // `muzzle_burst_slug`/`_dum`/`_ap`/`_mag` name the type directly; the base `muzzle_burst` /
    // heavy-mount `muzzle_burst2` carry no ammo suffix and default to slug, the common case.
    private static readonly string[] MuzzleAmmoTextures = { "slug_muzzle1", "dum_muzzle1", "ap_muzzle1", "mag_muzzle1" };

    // The tracer ammo-type axis (weapon-effects.md "Muzzle & tracer textures", C25): each chapter's
    // texture archive also carries a per-ammo tracer streak (`tracer_slug`/`_dumdum`/`_armorpierce`/
    // `_magnesium`), same four-way axis as the muzzle flash — TracerIndex reuses MuzzleAmmoIndex for
    // guns. Ordnance carries no FIRE ammo-type binding, so it falls back to the generic `tracer1`
    // (index 4), the last entry.
    private static readonly string[] TracerTextures =
        { "tracer_slug", "tracer_dumdum", "tracer_armorpierce", "tracer_magnesium", "tracer1" };

    // The dirt-debris chip textures: the gunhit def's flung-debris art. The def's bit1/bit2/bit3
    // gamez nodes carry no geometry in this install (0 vertices, measured C1/C2) — the bit0N
    // textures in every chapter archive are the chips themselves, so the burst draws them on
    // alpha-blended quads, one MultiMesh per texture (a MultiMesh's material is shared).
    private static readonly string[] DirtDebrisTextures = { "bit01", "bit02", "bit03", "bit04" };

    private readonly Proj[] _proj = new Proj[MaxProjectiles];
    // One sprite list per muzzle-flash ammo texture (MuzzleAmmoTextures) — a separate MultiMesh per
    // texture, since a MultiMesh's material (and so its texture) is shared across every instance.
    private readonly List<Sprite>[] _muzzle = { new(), new(), new(), new() };
    private readonly List<Sprite> _impact = new();
    private readonly List<Sprite> _smoke = new();   // muzzle smoke + eject puffs (alpha-blended)
    // One sprite list per dirt-debris chip texture (DirtDebrisTextures), same split as _muzzle.
    private readonly List<Sprite>[] _debris = { new(), new(), new(), new() };

    private readonly TextureArchive _textures;
    private readonly SoundArchive? _sounds;
    private readonly IReadOnlyDictionary<string, SoundDef>? _soundDefs;
    private readonly IReadOnlyDictionary<string, SoundGroup>? _soundGroups;
    // PlaySound resolves a SOUND_GROUPS name through this, same subsystem as _rng but its own
    // System.Random stream — SoundGroup.Pick's signature (docs/formats/sounds.md).
    private readonly System.Random _soundGroupRng = Rng.NewSystemRandom(Rng.Weapons);

    // The FLYOUT MODEL body (B14): rockets fly the original's own projectile mesh, instanced from a
    // chapter-gamez prototype root (`he_rocket`, `ap_rocket`, …) via the world's SceneBuilder — the
    // roots exist once per chapter and their geometry is nose-along-(-Z). Only rockets get a body:
    // guns fire ≤~10 rounds/s that live ~1 s each (dozens alive) and stay on the cheap MultiMesh
    // tracer quad, while a rocket lives ~0.8 s at 1/s (≤1 alive per player), so a full mesh per
    // rocket is cheap. Null archives (no world, or a chapter lacking the root) ⇒ streak-only fallback.
    private readonly GameZ? _flyoutGamez;
    private readonly SceneBuilder? _flyoutScene;
    private readonly Dictionary<string, GameZNode?> _flyoutNodes = new(); // model name → prototype (cached)

    // The FLYOUT MODEL_ANIMATION smoke trail (C21): each rocket type's def (`he_rocket`, `sonic`, …,
    // compiled in cam_anim, reader source missile_puffers.zrd.json) carries one or two
    // DISTANCE_INTERVAL PUFFER_STATEs AT_NODE the round itself — the authored thick trail with its
    // per-type colour ramp (HE orange→grey, flak orange→near-black, incendiary red→white, sonic
    // teal ×2). Resolved once per anim name from the world program; each live round drives its own
    // Puffer emitters via TrailAdvance, reused from a free-list once their smoke has decayed.
    private readonly AnimProgram? _flyoutAnims;
    private readonly Dictionary<string, TrailSpec?> _trailSpecs = new(); // anim name → parsed spec (null = none)
    private readonly List<TrailEmitter> _trailEmitters = new();          // reusable emitters, all states

    // Casing ejection (C22): each gun shot ejects the authored `gunshell` casing — the chapter-gamez
    // mesh (its child `g1` carries model 60, the shell1/shell2-textured shell) flying the gunshell
    // def's own OBJECT_MOTION verbatim (LOCAL gravity, ranged ballistic launch, forward-rotation
    // tumble over RUN_TIME 2 s). Instances are pooled and reused once a casing expires; the spec is
    // read once from the anim program the way the rocket trails are (see BuildCasingSpec).
    private readonly List<CasingSlot> _casings = new();

    // Pooled muzzle-light flashes (C22): real OmniLight3Ds, reused round-robin.
    private readonly List<LightFlash> _lights = new();

    // The named IMPACT effect models (D30): a per-surface IMPACT `ANIMATION` whose name is a chapter
    // gamez node (the water splash prototypes) is instanced at the hit point via the same flyout
    // GameZ/SceneBuilder above. Names that resolve to a reader/control def or nothing (the gun
    // `3040slug_gunhit` smoke, `he_ground_effect`, `bld_damage.flt`) stay on the stand-in spark —
    // their runtime PUFFER_STATE half is D32 (the puffer factory is torn down after the world build).
    private readonly Dictionary<string, GameZNode?> _impactNodes = new(); // impact anim name → prototype (cached)
    private readonly HashSet<string> _impactFxLogged = new();
    private readonly List<ImpactFx> _impactFx = new();
    // Unshaded override materials for the splash instances (authored lighting/fog: false), one
    // per distinct shared source material (OverrideUnlit).
    private readonly Dictionary<Material, StandardMaterial3D> _impactFxUnlit = new();

    private readonly PhysicsRayQueryParameters3D _ray = new(); // reused each step (no per-round alloc)
    private readonly List<AudioStreamPlayer> _sfxPool = new();
    // CANNON_SPREAD jitter and the stand-in fireball's sprite scatter. Held rather than resolved
    // per draw: two draws fire per round.
    private readonly RandomNumberGenerator _rng = Rng.Stream(Rng.Weapons);
    private readonly HashSet<string> _flyoutLogged = new();
    // One MultiMesh per muzzle-flash ammo texture (MuzzleAmmoTextures) — built in _Ready.
    private readonly MultiMesh[] _muzzleMm = new MultiMesh[MuzzleAmmoTextures.Length];
    // One MultiMesh per tracer texture (TracerTextures) — built in _Ready; one shared per-mesh
    // instance-count scratch array, cleared and refilled every frame in RenderTracers.
    private readonly MultiMesh[] _tracerMm = new MultiMesh[TracerTextures.Length];
    private readonly int[] _tracerCounts = new int[TracerTextures.Length];
    // One MultiMesh per dirt-debris chip texture (DirtDebrisTextures) — built in _Ready.
    private readonly MultiMesh[] _debrisMm = new MultiMesh[DirtDebrisTextures.Length];

    private int _projHigh;                     // highest slot ever used (bounds the scan)
    private Node3D _flyoutModels = null!;   // container for the live rocket-body instances
    private Node3D _impactFxModels = null!;  // container for the short-lived impact-effect instances
    private Node3D _casingModels = null!;    // container for the pooled shell-casing instances
    private MultiMesh _impactMm = null!;
    private MultiMesh _smokeMm = null!;
    private Camera3D? _listener;               // billboards align their streak to this camera
    private int _sfxNext;
    private bool _flyoutPoseLogged;
    private int _muzzleBasisLogs;
    private int _impactsLogged;
    private CasingSpec? _casingSpec;           // the gunshell OBJECT_MOTION, resolved once
    private bool _casingSpecResolved;
    private GameZNode? _casingProto;           // the gunshell gamez prototype, resolved once
    private bool _casingProtoResolved;
    private bool _casingLogged;

    public ProjectilePool(TextureArchive textures, SoundArchive? sounds,
        IReadOnlyDictionary<string, SoundDef>? soundDefs,
        GameZ? flyoutGamez = null, SceneBuilder? flyoutScene = null, AnimProgram? flyoutAnims = null,
        IReadOnlyDictionary<string, SoundGroup>? soundGroups = null)
    {
        _textures = textures;
        _sounds = sounds;
        _soundDefs = soundDefs;
        _soundGroups = soundGroups;
        _flyoutGamez = flyoutGamez;
        _flyoutScene = flyoutScene;
        _flyoutAnims = flyoutAnims;
        Name = "projectiles";
    }

    /// <summary>The camera a tracer streak orients its length toward (player 1's, in splitscreen).
    /// Tracers still render in every pane; only the streak's screen-space direction uses this.</summary>
    public Camera3D? Listener { get => _listener; set => _listener = value; }

    /// <summary>Which weapons.json IMPACT surface class a struck collider belongs to, from the
    /// per-mesh <see cref="SceneBuilder.SurfaceMeta"/> tag. The ONE surface classifier for the
    /// collision-consequence paths — the airframe's graze reaction
    /// (<c>FlightController.GrazeReaction</c>) picks its <c>touchdown_*</c> def from this same
    /// read, so a round and a wingtip never disagree about what they hit.</summary>
    public static SurfaceClass ClassifySurface(Node? collider)
    {
        if (collider != null && collider.HasMeta(SceneBuilder.SurfaceMeta))
        {
            return collider.GetMeta(SceneBuilder.SurfaceMeta).AsString() switch
            {
                "water" => SurfaceClass.Water,
                "buildings" => SurfaceClass.Buildings,
                _ => SurfaceClass.Default,
            };
        }
        return SurfaceClass.Default;
    }

    public override void _Ready()
    {
        // Tracers are velocity-aligned streaks (NOT billboarded — billboard would collapse the
        // long streak into a screen-vertical bar); muzzle/impact bursts are oriented quads too, each
        // carrying its own basis (Sprite.Orient) — the muzzle flash rolls in the firing plane's
        // basis, the impact spark faces the struck surface normal; neither is a fixed world plane.
        // One MultiMesh per tracer texture (TracerTextures) — its material is shared across every
        // instance it draws, same reason the muzzle flash is split per ammo texture.
        for (int i = 0; i < TracerTextures.Length; i++)
            _tracerMm[i] = AddMultiMesh(TracerTextures[i], MaxProjectiles, additive: true, billboard: false, out _);
        for (int i = 0; i < MuzzleAmmoTextures.Length; i++)
            _muzzleMm[i] = AddMultiMesh(MuzzleAmmoTextures[i], MaxFlashes, additive: true, billboard: false, out _);
        _impactMm = AddMultiMesh("slug_muzzle2", MaxFlashes, additive: true, billboard: false, out _);
        // Smoke (the muzzlepuffer puffs + the casing's eject-puff cluster): the authored puffer
        // textures (smoke101), alpha-blended rather than additive so the puffs read as smoke.
        _smokeMm = AddMultiMesh("smoke101", MaxSmoke, additive: false, billboard: false, out _);
        // Dirt-debris chips: the authored bit0N art, alpha-blended so the chips read as debris
        // rather than glowing through the additive flash pool (BL-203).
        for (int i = 0; i < DirtDebrisTextures.Length; i++)
            _debrisMm[i] = AddMultiMesh(DirtDebrisTextures[i], MaxFlashes, additive: false, billboard: false, out _);
        _flyoutModels = new Node3D { Name = "flyout" };
        AddChild(_flyoutModels);
        _impactFxModels = new Node3D { Name = "impact_fx" };
        AddChild(_impactFxModels);
        _casingModels = new Node3D { Name = "casings" };
        AddChild(_casingModels);
        for (int i = 0; i < 8; i++)
        {
            var p = new AudioStreamPlayer();
            AddChild(p);
            _sfxPool.Add(p);
        }
    }

    /// <summary>Fires one round of <paramref name="weapon"/> from the world muzzle transform,
    /// inheriting the launch platform's velocity, with a random offset inside the weapon's
    /// <c>CANNON_SPREAD</c> cone. Also flashes the muzzle. Silently drops the round if the pool is
    /// momentarily full (a soft cap, never a crash).</summary>
    public void Spawn(WeaponDef weapon, Transform3D muzzle, Vector3 inheritVel)
    {
        // The launch bark (BL-211): only rockets/ordnance carry a FIRE.SOUND — every cannon's is
        // null in the data (LOOPED_SOUND_NAME covers continuous gunfire instead), so this is a
        // one-shot with no double-up risk.
        if (weapon.Fire?.Sound is { } fireSnd)
            PlaySound(fireSnd);
        var forward = -muzzle.Basis.Z.Normalized();
        forward = ApplySpread(forward, weapon.CannonSpread ?? 0f);
        float speed = weapon.Velocity ?? 500f;
        float accel = weapon.Acceleration ?? 0f;
        // Rockets only: scale launch velocity and acceleration by the same dev factor. Guns stay
        // byte-identical (the impact reticle reads weapon.Velocity separately), and default 1.0
        // leaves rocket flight unchanged.
        if (weapon.IsRocket)
        {
            float scale = Config.GetFloat("weapons.rocketSpeedScale", RocketSpeedScale);
            speed *= scale;
            accel *= scale;
        }
        var tint = weapon.IsRocket ? RocketTint : SlugTint;
        // The tracer's own ammo-type axis (TracerTextures) — reuses MuzzleAmmoIndex for guns (same
        // FIRE-binding resolution as the muzzle flash); ordnance carries no ammo-type FIRE binding,
        // so it falls back to the generic tracer1 (the array's last entry).
        int tracerIdx = weapon.IsRocket ? TracerTextures.Length - 1 : MuzzleAmmoIndex(weapon);
        // Brightness is baked into the tint at spawn (C23, weapons.tracerBrightness) rather than read
        // per frame — a round's tint stands for its whole life, same as everything else in Proj.
        float brightness = Config.GetFloat("weapons.tracerBrightness", TracerBrightness);
        var tracerTint = new Color(brightness, brightness, brightness);

        int slot = -1;
        for (int i = 0; i < MaxProjectiles; i++)
        {
            if (!_proj[i].Alive)
            {
                slot = i;
                break;
            }
        }
        if (slot >= 0)
        {
            var vel = forward * speed + inheritVel;
            var model = weapon.IsRocket ? BuildFlyoutModel(weapon) : null;
            if (model != null)
                model.GlobalTransform = FlyoutPose(muzzle.Origin, vel);
            float rollRate = 0f;
            var trails = weapon.IsRocket ? AcquireTrails(weapon, muzzle.Origin, out rollRate) : null;
            _proj[slot] = new Proj
            {
                Alive = true,
                Pos = muzzle.Origin,
                Vel = vel,
                DistLeft = weapon.Range ?? 1000f,
                Accel = accel,
                Grav = (weapon.Gravity ?? 0f) * WorldGravity,
                Weapon = weapon,
                Tint = tracerTint,
                TracerIdx = tracerIdx,
                Model = model,
                Trails = trails,
                RollRate = rollRate,
            };
            if (slot >= _projHigh)
                _projHigh = slot + 1;
        }

        int ammoIdx = MuzzleAmmoIndex(weapon);
        var muzzleSprites = _muzzle[ammoIdx];
        if (muzzleSprites.Count + MuzzleFlashCount <= MaxFlashes)
        {
            // The flash triad rolls with the firing aircraft: its base orientation IS the muzzle's
            // world basis, which inherits the plane's roll/pitch/yaw — not a fixed world plane. Each
            // of the three quads is that basis rolled about its own facing normal (Z, unaffected by
            // the roll) by a shared per-shot random angle plus its 120-degree slot, reproducing the
            // reference captures' 3-lobed burst (MuzzleFlashCount).
            var planeBasis = muzzle.Basis.Orthonormalized();
            float baseAngle = _rng.Randf() * Mathf.Tau;
            for (int i = 0; i < MuzzleFlashCount; i++)
            {
                float angle = baseAngle + i * (Mathf.Tau / MuzzleFlashCount);
                var orient = RollAroundNormal(planeBasis, angle);
                muzzleSprites.Add(new Sprite { Pos = muzzle.Origin, Life = MuzzleLife, Size = MuzzleSize, Tint = tint, Orient = orient, AnchorLeft = true });
            }
            // Verification breadcrumbs (two, low-volume): the first flash reads a stored sprite's
            // facing normal back and confirms it IS the aircraft basis's at spawn (match≈1.000 — a
            // regression that stopped feeding Orient would read 0, and the roll leaves Z untouched);
            // a second sample once the plane has had a second to maneuver shows that basis rolled
            // with it, not locked to a world plane.
            double t = GameClock.Current?.Time ?? 0.0;
            if (_muzzleBasisLogs < 2 && (_muzzleBasisLogs == 0 || t >= 1.0))
            {
                _muzzleBasisLogs++;
                var stored = muzzleSprites[^1].Orient;
                float match = stored.Z.Dot(planeBasis.Z);
                GD.Print($"muzzle flash basis: z=({stored.Z.X:0.00},{stored.Z.Y:0.00},{stored.Z.Z:0.00}) " +
                         $"stored.Z==aircraft.Z match={match:0.000} ammo={ammoIdx} t={t:0.00}s");
            }
        }

        // The gun shot's authored secondaries (C22): the ejected casing with its white puff
        // cluster, the muzzlepuffer smoke, and the dynamic muzzle-light flash. Guns only — the
        // muzzle_burst def is bound by the guns; rockets carry their own FIRE effects.
        if (weapon.IsGun)
        {
            SpawnCasing(muzzle);
            SpawnMuzzleSmoke(muzzle);
            FlashMuzzleLight(muzzle.Origin);
        }
    }

    /// <summary>Instances a weapon's <c>FLYOUT</c> <c>MODEL</c> body — its <c>.flt</c> prototype root
    /// in the chapter gamez (<c>he_rocket</c>, <c>ap_rocket</c>, <c>sonic</c>, …) — as a fresh,
    /// collision-exempt <see cref="Node3D"/>, returned <b>un-parented</b> for the caller to place. The
    /// resolved prototype node is cached per model name; the geometry is authored nose-along-(-Z).
    /// Shared by the in-flight rocket body and the mounted pylon ordnance (<see cref="PylonOrdnance"/>,
    /// D44): the round hanging on the wing and the round that flies off it are the same asset. Null
    /// when no world scene is bound (viewer / headless dump), the weapon carries no <c>FLYOUT</c>
    /// <c>MODEL</c>, or the chapter gamez lacks the prototype root.</summary>
    public Node3D? BuildFlyoutBody(WeaponDef weapon)
    {
        if (_flyoutScene == null || _flyoutGamez == null || weapon.Flyout?.Model is not { } modelName)
            return null;
        if (!_flyoutNodes.TryGetValue(modelName, out var node))
        {
            node = _flyoutGamez.FindByName(modelName);
            _flyoutNodes[modelName] = node;
            if (node == null)
                GD.Print($"flyout model '{modelName}' ({weapon.Id}) absent from this chapter gamez — rocket flies streak-only");
        }
        if (node == null)
            return null;
        // Collision-exempt: a rocket carries no collider (it raycasts for its own hits and must not
        // obstruct another round or the world hit-test; mounted ordnance must not be shootable either),
        // and the world builder would otherwise attach one.
        var inst = _flyoutScene.BuildSubtree(node, skip: null, collisionSkip: _ => true);
        // Verification breadcrumb (once per model name): confirms the named prototype resolved and
        // instanced real geometry, without needing a lucky screenshot; then it goes quiet.
        if (inst != null && _flyoutLogged.Add(modelName))
            GD.Print($"flyout model '{modelName}' ({weapon.Id}) instanced: {CountMeshes(inst)} mesh(es)");
        return inst;
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

    /// <summary>One ballistics step: integrate every live round, raycast its segment, expire it at
    /// RANGE, and age the muzzle/impact sprites. Public because a non-realtime clock has the
    /// session call this instead of Godot's physics tick.</summary>
    public void SimStep(float dt)
    {
        var space = GetWorld3D()?.DirectSpaceState;
        for (int i = 0; i < _projHigh; i++)
        {
            ref var p = ref _proj[i];
            if (!p.Alive)
                continue;
            // Integrate (fixed physics step, frame-rate independent).
            if (p.Accel != 0f)
            {
                var dir = p.Vel.Normalized();
                p.Vel += dir * (p.Accel * dt);
            }
            if (p.Grav != 0f)
                p.Vel += Vector3.Down * (p.Grav * dt);
            var prev = p.Pos;
            var next = p.Pos + p.Vel * dt;
            float stepLen = (next - prev).Length();

            if (space != null && stepLen > 1e-5f)
            {
                _ray.From = prev;
                _ray.To = next;
                var hit = space.IntersectRay(_ray);
                if (hit.Count > 0)
                {
                    Impact(p.Weapon, (Vector3)hit["position"], hit["collider"].Obj as Node, (Vector3)hit["normal"]);
                    p.Alive = false;
                    KillModel(ref p);
                    ReleaseTrails(ref p);
                    continue;
                }
            }
            p.Pos = next;
            p.Age += dt;
            // The FLYOUT smoke trail rides the round: one authored puff per DISTANCE_INTERVAL
            // meters of flight, emitted in world space and left behind (C21).
            if (p.Trails != null)
            {
                foreach (var t in p.Trails)
                    t.Puffer.TrailAdvance(next);
            }
            p.DistLeft -= stepLen;
            if (p.DistLeft <= 0f)
            {
                // A hardpoint round detonates at max range — same impact path as a surface hit
                // (null collider ⇒ `default` IMPACT sound + named puffer effect, no damage). Gun
                // rounds must NOT: that would pop a spark/sound at ~1000 m on every bullet.
                if (p.Weapon.IsRocket)
                {
                    // Mid-air range-expiry detonation: no struck surface, so no normal — the impact
                    // sprite falls back to the world-facing quad (SurfaceBasis handles a zero normal).
                    Impact(p.Weapon, p.Pos, null, Vector3.Zero);
                }
                p.Alive = false;   // spent
                KillModel(ref p);
                ReleaseTrails(ref p);
            }
        }

        foreach (var m in _muzzle)
            AgeSprites(m, dt);
        AgeSprites(_impact, dt);
        AgeSprites(_smoke, dt);
        foreach (var d in _debris)
            AgeSprites(d, dt);
        AgeCasings(dt);
        AgeLights(dt);
        for (int i = _impactFx.Count - 1; i >= 0; i--)
        {
            var f = _impactFx[i];
            f.Age += dt;
            if (f.Age >= f.Life)
            {
                f.Model.QueueFree();
                _impactFx.RemoveAt(i);
            }
            else
            {
                AdvanceSplash(f);
                _impactFx[i] = f;
            }
        }
    }

    public override void _Process(double delta)
    {
        RenderTracers();
        for (int i = 0; i < _muzzle.Length; i++)
            RenderSprites(_muzzleMm[i], _muzzle[i]);
        RenderSprites(_impactMm, _impact);
        RenderSprites(_smokeMm, _smoke);
        for (int i = 0; i < _debris.Length; i++)
            RenderSprites(_debrisMm[i], _debris[i]);
    }

    /// <summary>Deactivates every live round (R / respawn: no tracers hang in the air).</summary>
    public void Clear()
    {
        for (int i = 0; i < _projHigh; i++)
        {
            KillModel(ref _proj[i]);
            ReleaseTrails(ref _proj[i]);
            _proj[i].Alive = false;
        }
        _projHigh = 0;
        foreach (var m in _muzzle)
            m.Clear();
        _impact.Clear();
        _smoke.Clear();
        foreach (var d in _debris)
            d.Clear();
        foreach (var c in _casings)
        {
            c.InUse = false;
            c.Node.Visible = false;
        }
        foreach (var l in _lights)
        {
            l.InUse = false;
            l.Light.Visible = false;
        }
        foreach (var f in _impactFx)
            f.Model.QueueFree();
        _impactFx.Clear();
    }

    private static int CountMeshes(Node n)
    {
        int c = n is MeshInstance3D ? 1 : 0;
        foreach (var child in n.GetChildren())
            c += CountMeshes(child);
        return c;
    }

    // Poses one live splash instance at its age along the authored curves (see the Splash*
    // constants): the base disc's xz ramp and the column's collapsing Y scale. Same scale
    // application as AnimRuntime.PoseScale (rest basis orthonormalized, then scaled). A model
    // with neither child (not a splash) has nothing to drive and its Life stayed the short
    // static one.
    private static void AdvanceSplash(ImpactFx f)
    {
        float t = f.Age;
        if (f.Base != null)
        {
            float s = t < SplashBaseGrowTime
                ? Mathf.Lerp(1f, SplashBaseMax, t / SplashBaseGrowTime)
                : t < SplashBaseEaseStart
                    ? SplashBaseMax
                    : Mathf.Lerp(SplashBaseMax, SplashBaseEnd,
                        Mathf.Min((t - SplashBaseEaseStart) / (SplashRunTime - SplashBaseEaseStart), 1f));
            f.Base.Transform = new Transform3D(
                f.BaseRest.Basis.Orthonormalized().Scaled(new Vector3(s, 1f, s)), f.BaseRest.Origin);
        }
        if (f.Splash != null)
        {
            // OBJECT_MOTION SCALE (1,100,1) + (0,-100,0)·u over the 2 s run: the column pops to
            // its full authored height and collapses to nothing.
            float y = Mathf.Max(SplashColumnScale * (1f - t / SplashRunTime), 0.001f);
            f.Splash.Transform = new Transform3D(
                f.SplashRest.Basis.Orthonormalized().Scaled(
                    new Vector3(SplashColumnWidthScale, y, SplashColumnWidthScale)), f.SplashRest.Origin);
        }
    }

    // The built subtree's nodes carry their gamez cs_name as NameMeta (Godot renames duplicate
    // siblings, WORLD-8) — resolve the splash children by that, never by Godot node name.
    private static Node3D? FindChildByMetaSuffix(Node node, string suffix)
    {
        if (node is Node3D n3d && node.HasMeta(AnimRuntime.NameMeta)
            && node.GetMeta(AnimRuntime.NameMeta).AsString().EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
            return n3d;
        foreach (var child in node.GetChildren())
        {
            if (FindChildByMetaSuffix(child, suffix) is { } found)
                return found;
        }
        return null;
    }

    // The flyout body's world pose: its geometry is authored nose-along-(-Z) (uniform across all 15
    // ROCKET models), so LookingAt(velDir) — which aims local -Z down the argument — points the nose
    // along the round's flight direction. `pos` is the tail (the model origin sits at the exhaust end).
    private static Transform3D FlyoutPose(Vector3 pos, Vector3 vel)
    {
        var dir = vel.LengthSquared() > 1e-6f ? vel.Normalized() : Vector3.Forward;
        var up = Mathf.Abs(dir.Dot(Vector3.Up)) > 0.99f ? Vector3.Right : Vector3.Up;
        return new Transform3D(Basis.LookingAt(dir, up), pos);
    }

    // The impact sprite's orientation: the quad faces the struck surface (local Z = the surface
    // normal), so a spark sits against the wall/ground it hit rather than in a fixed world plane.
    // A degenerate normal — a mid-air range-expiry detonation, which has no surface — falls back to
    // the original world-facing quad (Right/Up/Back), leaving that case byte-identical.
    private static Basis SurfaceBasis(Vector3 normal)
    {
        if (normal.LengthSquared() < 1e-6f)
            return new Basis(Vector3.Right, Vector3.Up, Vector3.Back);
        var z = normal.Normalized();
        var up = Mathf.Abs(z.Dot(Vector3.Up)) > 0.99f ? Vector3.Right : Vector3.Up;
        var x = up.Cross(z).Normalized();
        var y = z.Cross(x).Normalized();
        return new Basis(x, y, z);
    }

    private static void KillModel(ref Proj p)
    {
        if (p.Model != null)
        {
            p.Model.QueueFree();
            p.Model = null;
        }
    }

    private static void ReleaseTrails(ref Proj p)
    {
        if (p.Trails == null)
            return;
        foreach (var t in p.Trails)
        {
            t.Puffer.TrailEnd(); // live smoke decays naturally
            t.InUse = false;
        }
        p.Trails = null;
    }

    private static void AgeSprites(List<Sprite> sprites, float dt)
    {
        for (int i = sprites.Count - 1; i >= 0; i--)
        {
            var s = sprites[i];
            s.Age += dt;
            if (s.Age >= s.Life)
            {
                sprites.RemoveAt(i);
            }
            else
            {
                // Dirt debris carries velocity/spin; smoke puffs carry velocity without gravity;
                // every other sprite has both zeroed and is unaffected — the position/orientation
                // set at spawn stands for its whole life.
                if (s.SpinRate != 0f)
                    s.Orient = s.Orient.Rotated(s.SpinAxis, s.SpinRate * dt);
                if (s.Vel != Vector3.Zero)
                {
                    s.Pos += s.Vel * dt;
                    if (!s.NoGravity)
                        s.Vel.Y -= WorldGravity * dt;
                }
                sprites[i] = s;
            }
        }
    }

    // The muzzle-flash ammo-type index (into MuzzleAmmoTextures) resolved from the weapon's FIRE
    // ANIMATION binding: muzzle_burst_slug/_dum/_ap/_mag name the ammo type directly; the base
    // muzzle_burst and heavy-mount muzzle_burst2 carry no ammo suffix and read as slug.
    private static int MuzzleAmmoIndex(WeaponDef weapon)
    {
        var anim = weapon.Fire?.Animation;
        if (anim == null)
            return 0;
        if (anim.EndsWith("_dum", System.StringComparison.OrdinalIgnoreCase))
            return 1;
        if (anim.EndsWith("_ap", System.StringComparison.OrdinalIgnoreCase))
            return 2;
        if (anim.EndsWith("_mag", System.StringComparison.OrdinalIgnoreCase))
            return 3;
        return 0;
    }

    // Rolls a quad's basis about its own facing normal (Z) — the in-plane rotation the flash triad
    // uses to vary its look per shot without disturbing which way the quad faces.
    private static Basis RollAroundNormal(Basis b, float radians)
    {
        float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
        var x = b.X * c + b.Y * s;
        var y = b.Y * c - b.X * s;
        return new Basis(x, y, b.Z);
    }

    private static void RenderSprites(MultiMesh mm, List<Sprite> sprites)
    {
        int n = 0;
        foreach (var s in sprites)
        {
            float k = 1f - s.Age / s.Life;            // shrink + fade over life
            float size = s.Size * (0.6f + 0.4f * k);
            var basis = new Basis(s.Orient.X * size, s.Orient.Y * size, s.Orient.Z * size);
            // QuadMesh's default UV puts U=0 (the texture's left edge) at local X=-0.5 — anchoring
            // there keeps it pinned to Pos as the quad shrinks over its life.
            var pos = s.AnchorLeft ? s.Pos + s.Orient.X * (size * 0.5f) : s.Pos;
            mm.SetInstanceTransform(n, new Transform3D(basis, pos));
            var c = s.Tint;
            c.A = k;
            mm.SetInstanceColor(n, c);
            n++;
        }
        mm.VisibleInstanceCount = n;
    }

    private MultiMesh AddMultiMesh(string texture, int cap, bool additive, bool billboard, out MultiMeshInstance3D mmi)
    {
        var quad = new QuadMesh { Size = Vector2.One };
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoTexture = _textures.Find(texture),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
            BillboardMode = billboard ? BaseMaterial3D.BillboardModeEnum.Enabled : BaseMaterial3D.BillboardModeEnum.Disabled,
            BillboardKeepScale = true,
            VertexColorUseAsAlbedo = true,
            // None of these quads tile — every one draws exactly one whole texture. Left at the
            // engine default (true), bilinear filtering at the UV=0/1 edge blends in the OPPOSITE
            // edge (wrap), which is the tail artifact on a tracer streak (bright front bleeding into
            // the dark tail) — C25. (A `Uv1Scale.x=-1` mirror lived here from the same C25 tuning
            // pass with no matching `Uv1Offset` — with `TextureRepeat` off, that clamped every
            // sample to the texture's single U=0 column instead of mirroring it, so every sprite
            // drawn by this pool — tracer, muzzle flash, impact, smoke — rendered as a flat colour
            // stripe instead of its texture. C21 removed it; a texture that needs flipping now gets
            // that from its own geometry — e.g. RenderTracers rotates the streak quad 180° about its
            // own facing normal — never from another `Uv1Scale` mirror on this shared material.)
            TextureRepeat = false,
        };
        quad.Material = mat;
        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = quad,
            InstanceCount = cap,
            VisibleInstanceCount = 0,
        };
        mmi = new MultiMeshInstance3D
        {
            Multimesh = mm,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(mmi);
        return mm;
    }

    private Vector3 ApplySpread(Vector3 forward, float coneDeg)
    {
        if (coneDeg <= 0f)
            return forward;
        // A random direction inside the cone: a random azimuth around `forward`, and a polar angle
        // in [0, cone] biased for a roughly uniform disc so the pattern fills the cone, not its rim.
        float half = Mathf.DegToRad(coneDeg) * 0.5f;
        float polar = half * Mathf.Sqrt(_rng.Randf());
        float azimuth = _rng.Randf() * Mathf.Tau;
        // Build a basis with `forward` as -Z, then tilt.
        var basis = Basis.LookingAt(forward, Mathf.Abs(forward.Dot(Vector3.Up)) > 0.99f ? Vector3.Right : Vector3.Up);
        var tilted = basis * new Vector3(
            Mathf.Sin(polar) * Mathf.Cos(azimuth),
            Mathf.Sin(polar) * Mathf.Sin(azimuth),
            -Mathf.Cos(polar));
        return tilted.Normalized();
    }

    /// <summary>The in-flight rocket body: a <see cref="BuildFlyoutBody"/> instance parented under the
    /// pool's own container (the caller poses it down the round's velocity each frame). Null falls back
    /// to the exhaust streak.</summary>
    private Node3D? BuildFlyoutModel(WeaponDef weapon)
    {
        var inst = BuildFlyoutBody(weapon);
        if (inst != null)
            _flyoutModels.AddChild(inst);
        return inst;
    }

    /// <summary>Starts the round's FLYOUT smoke trail (C21): one <see cref="Puffer"/> per
    /// DISTANCE_INTERVAL <c>PUFFER_STATE</c> in the weapon's <c>MODEL_ANIMATION</c> def, taken
    /// from the free-list when an earlier round's emitter has fully decayed, else freshly built.
    /// <paramref name="rollRate"/> is the def's spinner rate (rad/s about the nose axis; the
    /// sonic), 0 for everything else. Null when there is no anim program (no world / the weapon
    /// lab), the weapon names no <c>MODEL_ANIMATION</c>, or its textures are absent.</summary>
    private TrailEmitter[]? AcquireTrails(WeaponDef weapon, Vector3 origin, out float rollRate)
    {
        rollRate = 0f;
        var spec = TrailSpecFor(weapon);
        if (spec == null || spec.States.Count == 0)
            return null;
        rollRate = spec.RollRate;
        var set = new List<TrailEmitter>(spec.States.Count);
        foreach (var state in spec.States)
        {
            TrailEmitter? emitter = null;
            foreach (var e in _trailEmitters)
            {
                // Reusable once its round died AND its smoke finished decaying — TrailAdvance
                // would otherwise graft a new rocket's trail onto the old one's live puffs.
                if (!e.InUse && e.State == state && e.Puffer.LiveCount == 0)
                {
                    emitter = e;
                    break;
                }
            }
            if (emitter == null)
            {
                var puffer = Puffer.Create(state, _textures);
                if (puffer == null)
                {
                    if (_flyoutLogged.Add("trail:" + state.Name))
                        GD.Print($"rocket trail puffer '{state.Name}' ({weapon.Id}) has no textures in this chapter — skipped");
                    continue;
                }
                AddChild(puffer);
                emitter = new TrailEmitter { Puffer = puffer, State = state };
                _trailEmitters.Add(emitter);
            }
            emitter.InUse = true;
            emitter.Puffer.TrailAdvance(origin); // first call homes the trail at the muzzle
            set.Add(emitter);
        }
        return set.Count > 0 ? set.ToArray() : null;
    }

    // Resolves a weapon's FLYOUT MODEL_ANIMATION name to its trail spec, cached (misses too).
    private TrailSpec? TrailSpecFor(WeaponDef weapon)
    {
        if (_flyoutAnims == null || weapon.Flyout?.ModelAnimation is not { } animName)
            return null;
        if (_trailSpecs.TryGetValue(animName, out var spec))
            return spec;
        spec = BuildTrailSpec(animName, weapon.Id);
        _trailSpecs[animName] = spec;
        return spec;
    }

    /// <summary>Reads a FLYOUT def's trail out of the anim program: every ACTIVE
    /// DISTANCE_INTERVAL <c>PUFFER_STATE</c> (the authored per-type smoke — colour ramp, size,
    /// lifetime, one puff per interval meters), plus the def's steady <c>ObjectMotion</c> spin
    /// rate if it carries one. Time-interval puffers (the torpedo's blast cloud) are left to a
    /// future pass — this path renders the trail the round leaves behind.</summary>
    private TrailSpec? BuildTrailSpec(string animName, string weaponId)
    {
        foreach (var def in _flyoutAnims!.ByAnimName(animName))
        {
            var spec = new TrailSpec();
            foreach (var seq in def.Sequences)
            {
                foreach (var ev in seq.Events)
                {
                    if (ev.Kind == "PufferState" && (ev.Data.Num("active_state") ?? 0f) > 0f)
                    {
                        var state = PufferState.FromAnimEvent(ev.Data);
                        if (state.DistanceInterval > 0f
                            && (state.Textures.Count > 0 || state.TextureSequence.Count > 0))
                            spec.States.Add(state);
                    }
                    else if (ev.Kind == "ObjectMotion"
                             && ev.Data.Obj("xyz_rotation")?.Vec3("initial") is { } rate
                             && !rate.IsZeroApprox())
                    {
                        spec.RollRate = rate.Z; // the spinners roll about the nose (z) axis
                    }
                }
            }
            if (spec.States.Count > 0)
            {
                GD.Print($"rocket trail '{animName}' ({weaponId}): {spec.States.Count} puffer state(s)"
                         + (spec.RollRate != 0f ? $", roll {spec.RollRate:0.##} rad/s" : ""));
                return spec;
            }
        }
        GD.Print($"rocket trail '{animName}' ({weaponId}): no DISTANCE_INTERVAL puffer in the anim program — no trail");
        return null;
    }

    /// <summary>Instances a named IMPACT effect's gamez MODEL prototype at the hit point, when the
    /// name resolves to a chapter-gamez node carrying geometry (the water splash <c>splash1.flt</c> /
    /// <c>bsplsh.flt</c>). Reuses the flyout <see cref="GameZ"/>/<see cref="SceneBuilder"/>, is
    /// collision-exempt, and sits upright at the point; the instance is tracked for a short life and
    /// freed. Returns false — leaving the stand-in spark to show — when there is no world scene, the
    /// name is a reader/control def or an unresolved binding (no such node), or the node built no
    /// mesh (an empty puffer-host root such as <c>gunhit</c>).</summary>
    private bool SpawnImpactModel(string animName, Vector3 point)
    {
        if (_flyoutScene == null || _flyoutGamez == null)
            return false;
        if (!_impactNodes.TryGetValue(animName, out var node))
        {
            node = _flyoutGamez.FindByName(animName);
            _impactNodes[animName] = node;
        }
        if (node == null)
            return false;
        var inst = _flyoutScene.BuildSubtree(node, skip: null, collisionSkip: _ => true);
        if (inst == null)
            return false;
        int meshes = CountMeshes(inst);
        if (meshes == 0)
        {
            // A geometry-less host (e.g. the `gunhit` puffer root): nothing would render — drop it
            // and keep the spark. Logged once so the data fact is visible, not silently swallowed.
            if (_impactFxLogged.Add(animName))
                GD.Print($"impact effect '{animName}' is a geometry-less node — spark stands in");
            inst.QueueFree();
            return false;
        }
        _impactFxModels.AddChild(inst);
        inst.GlobalTransform = new Transform3D(Basis.Identity, point); // splash geometry stands upright at the hit
        // A splash prototype (splash1.flt / bsplsh.flt) carries a `*_base` disc and a `*_splash`
        // column child; when either resolves, the instance plays the authored 2 s scale curves
        // (AdvanceSplash) instead of standing statically for the short stand-in life.
        var baseNode = FindChildByMetaSuffix(inst, "_base");
        var splashNode = FindChildByMetaSuffix(inst, "_splash");
        bool animated = baseNode != null || splashNode != null;
        if (animated)
        {
            // The splash models are authored `lighting: false` + `fog: false` (self-lit effect
            // geometry — the reference captures show white splashes at night), but the shared
            // world materials multiply the mission SUNLIGHT (`csky_world_light`) into every
            // surface, which dims the splash to invisibility on a night map. Honour the authored
            // flags on these short-lived instances with an unshaded override; the column is a
            // `Facade` (SphericalY) model, so it Y-billboards toward the camera.
            if (baseNode != null)
                OverrideUnlit(baseNode, billboardY: false);
            if (splashNode != null)
                OverrideUnlit(splashNode, billboardY: true);
        }
        var fx = new ImpactFx
        {
            Model = inst,
            Age = 0f,
            Life = animated ? SplashRunTime : ImpactModelLife,
            Base = baseNode,
            Splash = splashNode,
            BaseRest = baseNode?.Transform ?? Transform3D.Identity,
            SplashRest = splashNode?.Transform ?? Transform3D.Identity,
        };
        if (animated)
            AdvanceSplash(fx); // pose t=0 (the column at full authored scale) before the first tick
        _impactFx.Add(fx);
        if (_impactFxLogged.Add(animName))
            GD.Print($"impact effect '{animName}' instanced: {meshes} mesh(es)"
                     + (animated ? $" — splash curves driven (base {(baseNode != null ? "✓" : "–")}, column {(splashNode != null ? "✓" : "–")})" : ""));
        return true;
    }

    /// <summary>Replaces a splash child's shared world materials with unshaded (self-lit,
    /// unfogged) overrides per the models' authored <c>lighting/fog: false</c> flags, keeping each
    /// surface's own albedo texture. Overrides are cached per source material — the world's
    /// materials are themselves shared, so each distinct one maps to one override.</summary>
    private void OverrideUnlit(Node3D node, bool billboardY)
    {
        if (node is MeshInstance3D mi && mi.Mesh is { } mesh)
        {
            for (int s = 0; s < mesh.GetSurfaceCount(); s++)
            {
                var src = mi.GetActiveMaterial(s);
                if (src == null)
                    continue;
                if (!_impactFxUnlit.TryGetValue(src, out var over))
                {
                    over = new StandardMaterial3D
                    {
                        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                        AlbedoTexture = (src as ShaderMaterial)?.GetShaderParameter("albedo_tex").Obj as Texture2D,
                        VertexColorUseAsAlbedo = true,
                        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                        BillboardMode = billboardY ? BaseMaterial3D.BillboardModeEnum.FixedY : BaseMaterial3D.BillboardModeEnum.Disabled,
                        BillboardKeepScale = true, // the column's whole height is its node Y scale
                    };
                    _impactFxUnlit[src] = over;
                }
                mi.SetSurfaceOverrideMaterial(s, over);
            }
        }
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D c)
                OverrideUnlit(c, billboardY);
        }
    }

    private void Impact(WeaponDef weapon, Vector3 point, Node? collider, Vector3 normal)
    {
        var surface = ClassifySurface(collider);
        // The impact sprites face the struck surface (SurfaceBasis(normal)) rather than a fixed world
        // plane — a supplier distinct from the muzzle flash's plane basis (both feed Sprite.Orient).
        var orient = SurfaceBasis(normal);
        // The per-surface IMPACT binding: the struck surface's entry, else the weapon's `default`.
        if (!weapon.Impact.TryGetValue(surface, out var effect))
            weapon.Impact.TryGetValue(SurfaceClass.Default, out effect);

        // The named IMPACT effect (D30): when its `ANIMATION`/`SURFACE_ANIMATION` names a chapter
        // gamez node (the water splash prototypes), instance it at the hit point and skip the spark
        // — the authored model IS the effect. The gun/rocket smoke+fireball names resolve to reader
        // defs or nothing, so nothing instances and the spark stands in (their PUFFER_STATE is D32).
        var fxName = effect != null ? (effect.Animation ?? effect.SurfaceAnimation) : null;
        // Verification breadcrumb: the first few impacts confirm hit detection, surface
        // classification (B15) and which per-surface IMPACT entry the classification selected,
        // without needing a lucky screenshot; then it goes quiet. `fx=`/`snd=` are what makes a
        // "these two surfaces look the same" report answerable — the effect and sound the data
        // chose, before anything renders.
        if (_impactsLogged < 8)
        {
            _impactsLogged++;
            GD.Print($"impact: {weapon.Id} ({weapon.Name}) -> {surface} at " +
                     $"({point.X:0},{point.Y:0},{point.Z:0}) on {collider?.GetParent()?.Name}/{collider?.Name}" +
                     $" fx={fxName ?? "-"} snd={effect?.Sound ?? "-"}");
        }
        bool showedModel = fxName != null && SpawnImpactModel(fxName, point);
        // The puffer half (D32): when the effect is not a gamez model, hand its name to the
        // world-effects runtime, which builds the smoke/fireball at the hit. Rockets/ordnance only
        // (the gun `gunhit` smoke is a documented follow-up — see EffectSink). The runtime no-ops on
        // a name it does not carry (the inert `bld_damage.flt`/`f18sparks2`/…), so the spark below
        // still stands in for those.
        if (!showedModel && fxName != null && !weapon.IsGun)
            EffectSink?.Invoke(fxName, point);
        if (!showedModel && !weapon.IsGun && EffectSink == null)
        {
            // A hardpoint weapon with no world-effects runtime to render its real fireball: a
            // scene-less pool (the weapon lab). Show an explosion burst stand-in in place of the
            // single spark, so the blast is visible. Flight keeps its real puffer (EffectSink set).
            SpawnExplosion(point, orient);
        }
        else if (!showedModel && surface == SurfaceClass.Default)
        {
            // Dirt (unclassified terrain): a tumbling-debris burst, not one fat spark.
            SpawnDirtDebris(point, orient);
        }
        else if (!showedModel && surface == SurfaceClass.Buildings && weapon.IsGun)
        {
            // A gun round off a building: ricochet sparks + the flash. The authored assets for
            // this class (`bld_damage.flt`, the `rcochet1` EFFECT) are confirmed missing from the
            // install, so the stand-in has to carry the read on its own (BL-203).
            SpawnRicochet(point, orient);
        }
        else if (!showedModel && _impact.Count < MaxFlashes)
        {
            var tint = surface == SurfaceClass.Water ? new Color(0.8f, 0.9f, 1.0f) : new Color(1f, 0.9f, 0.5f);
            _impact.Add(new Sprite { Pos = point, Life = ImpactLife, Size = ImpactSize, Tint = tint, Orient = orient });
        }
        // Per-surface IMPACT sound (landed): the struck surface's SOUND, else the default's.
        if (effect?.Sound is { } snd)
            PlaySound(snd);
        // Apply the hit to whatever destructible was struck (C23) — a no-op for terrain/water/clutter.
        DamageSink?.Invoke(collider, weapon.HealthDamage ?? 0f);
    }

    /// <summary>A stand-in fireball: a cluster of large, bright, additive sprites at the hit point,
    /// varied in size/life/tint, so a hardpoint impact reads as an explosion where the real puffer
    /// effect cannot be built (no world-effects runtime).</summary>
    private void SpawnExplosion(Vector3 point, Basis orient)
    {
        for (int i = 0; i < ExplosionSprites && _impact.Count < MaxFlashes; i++)
        {
            var off = new Vector3(_rng.Randf() - 0.5f, _rng.Randf() - 0.5f, _rng.Randf() - 0.5f) * ExplosionSpread;
            float t = _rng.Randf();
            _impact.Add(new Sprite
            {
                Pos = point + off,
                Life = ExplosionLife * (0.6f + 0.6f * t),
                Size = ExplosionSize * (0.7f + 0.6f * t),
                Tint = new Color(1f, 0.45f + 0.4f * t, 0.12f * t), // deep orange → yellow core
                Orient = orient,
            });
        }
    }

    /// <summary>A dirt impact's stand-in: a few small, randomly-rotated chips launched outward
    /// from the surface normal and arcing under gravity, replacing the single 3 m spark so a
    /// gun/rocket round hitting terrain reads as scattered debris rather than one orange flash.
    /// Drawn on the gunhit def's own <c>bit0N</c> chip textures, alpha-blended (their own pools) —
    /// through the additive muzzle-flash-textured impact pool they read as a small flame (BL-203).</summary>
    private void SpawnDirtDebris(Vector3 point, Basis orient)
    {
        for (int i = 0; i < DirtDebrisSprites; i++)
        {
            var pool = _debris[i % _debris.Length]; // cycle the chip art without an extra RNG draw
            if (pool.Count >= MaxFlashes)
                continue;
            var dir = ApplySpread(orient.Z, DirtDebrisSpreadDeg);
            float speed = DirtDebrisSpeed * (0.5f + 0.5f * _rng.Randf());
            var spinAxis = new Vector3(
                _rng.Randf() - 0.5f, _rng.Randf() - 0.5f, _rng.Randf() - 0.5f).Normalized();
            // A random starting roll so the chips don't all share the impact's surface-facing
            // orientation before their own tumble (SpinRate) takes over.
            var startOrient = orient.Rotated(spinAxis, _rng.Randf() * Mathf.Tau);
            pool.Add(new Sprite
            {
                Pos = point,
                Life = DirtDebrisLife * (0.75f + 0.5f * _rng.Randf()),
                Size = DirtDebrisSize * (0.7f + 0.6f * _rng.Randf()),
                Tint = DirtTint,
                Orient = startOrient,
                Vel = dir * speed,
                SpinAxis = spinAxis,
                SpinRate = (_rng.Randf() * 2f - 1f) * DirtDebrisSpinMax,
            });
        }
    }

    /// <summary>A gun round ricocheting off a buildings-classed surface: fast, bright sparks
    /// flying off the wall (additive, on the impact pool) plus the brief hit flash. A stand-in —
    /// the authored `bld_damage.flt`/`rcochet1` assets do not exist in the install; magnitudes
    /// are TUNE (BL-203).</summary>
    private void SpawnRicochet(Vector3 point, Basis orient)
    {
        if (_impact.Count < MaxFlashes)
            _impact.Add(new Sprite { Pos = point, Life = ImpactLife, Size = ImpactSize, Tint = new Color(1f, 0.9f, 0.5f), Orient = orient });
        for (int i = 0; i < RicochetSparks && _impact.Count < MaxFlashes; i++)
        {
            var dir = ApplySpread(orient.Z, RicochetSpreadDeg);
            float speed = RicochetSparkSpeed * (0.5f + 0.5f * _rng.Randf());
            _impact.Add(new Sprite
            {
                Pos = point,
                Life = RicochetSparkLife * (0.7f + 0.6f * _rng.Randf()),
                Size = RicochetSparkSize * (0.7f + 0.6f * _rng.Randf()),
                Tint = RicochetTint,
                Orient = orient,
                Vel = dir * speed,
            });
        }
    }

    /// <summary>Ejects one shell casing (C22): a pooled instance of the chapter-gamez
    /// <c>gunshell</c> prototype (its <c>g1</c> child carries the shell mesh), launched with the
    /// gunshell def's own <c>OBJECT_MOTION</c> read verbatim from the anim program — the ranged
    /// ballistic drop and the forward-rotation tumble over its <c>RUN_TIME</c>, same semantics as
    /// <c>MotionRuntime</c>. Each casing rides its own transient node, so sustained fire ejects at
    /// gun rate — nothing shares the <c>gunshell</c> anchor. Alongside the casing goes its white
    /// puff cluster (a hand-authored stand-in; no shipped def matches it — see EjectPuffs). No-op
    /// without a world scene/anim program (the weapon lab, the empty stage).</summary>
    private void SpawnCasing(Transform3D muzzle)
    {
        var spec = CasingSpecResolve();
        if (spec == null)
            return;
        var slot = AcquireCasing();
        var puffVel = Vector3.Down * EjectPuffSink; // fallback drift when the casing pool is at cap
        if (slot != null)
        {
            // MotionRuntime's translation_range read, through its own expression so the two
            // cannot drift: `xz`/`y` are an azimuth/elevation in degrees and `initial` the launch
            // speed. The authored gunshell values are ±10° of bearing at −75…−85° of elevation and
            // 1.5–1.8 m/s — a casing dropping out of the gun port, which is why the direction is
            // taken in the MUZZLE's frame rather than the world's: a rolling plane throws its brass
            // out sideways, not at the ground.
            slot.Basis = muzzle.Basis.Orthonormalized();
            slot.Start = muzzle.Origin;
            slot.V0 = slot.Basis * (Mech3.Anim.MotionRuntime.RangeLaunchDirection(
                          RandRange(spec.XzMin, spec.XzMax), RandRange(spec.YMin, spec.YMax))
                      * RandRange(spec.SpeedMin, spec.SpeedMax));
            slot.Age = 0f;
            slot.InUse = true;
            slot.Node.Visible = true;
            slot.Node.GlobalTransform = new Transform3D(slot.Basis, slot.Start);
            puffVel = slot.V0; // the cluster rides with its casing (the reference captures show
                               // the brass speck inside each falling puff cluster)
        }

        // The white puff cluster the original shows with every casing: persisting puffs that fall
        // with the shell while the aircraft flies out of them.
        var orient = muzzle.Basis.Orthonormalized();
        for (int i = 0; i < EjectPuffs && _smoke.Count < MaxSmoke; i++)
        {
            var off = new Vector3(_rng.Randf() - 0.5f, _rng.Randf() - 0.5f, _rng.Randf() - 0.5f)
                      * (2f * EjectPuffSpread);
            var drift = puffVel + new Vector3(_rng.Randf() - 0.5f, _rng.Randf() - 0.5f, _rng.Randf() - 0.5f)
                        * (2f * EjectPuffDrift);
            _smoke.Add(new Sprite
            {
                Pos = muzzle.Origin + off,
                Life = RandRange(EjectPuffLifeMin, EjectPuffLifeMax),
                Size = RandRange(EjectPuffSizeMin, EjectPuffSizeMax),
                Tint = EjectPuffTint,
                Orient = orient,
                Vel = drift,
                NoGravity = true,
            });
        }
    }

    /// <summary>The authored muzzlepuffer smoke (C22): a few short-lived puffs at the muzzle with
    /// the def's aft velocity, size, lifetime and deviation — the aircraft flies out of them, so
    /// they read as the smoke the shot leaves behind.</summary>
    private void SpawnMuzzleSmoke(Transform3D muzzle)
    {
        var aft = muzzle.Basis.Z.Normalized(); // Godot forward is -Z; the puffer drifts aft
        var orient = muzzle.Basis.Orthonormalized();
        for (int i = 0; i < MuzzleSmokePuffs && _smoke.Count < MaxSmoke; i++)
        {
            var dev = new Vector3(_rng.Randf() - 0.5f, _rng.Randf() - 0.5f, _rng.Randf() - 0.5f)
                      * (2f * MuzzleSmokeDeviation);
            var vel = aft * MuzzleSmokeAftSpeed + new Vector3(
                _rng.Randf() - 0.5f, _rng.Randf() - 0.5f, _rng.Randf() - 0.5f) * (2f * MuzzleSmokeRandVel);
            _smoke.Add(new Sprite
            {
                Pos = muzzle.Origin + dev,
                Life = RandRange(MuzzleSmokeLifeMin, MuzzleSmokeLifeMax),
                Size = RandRange(MuzzleSmokeSizeMin, MuzzleSmokeSizeMax),
                Tint = MuzzleSmokeTint,
                Orient = orient,
                Vel = vel,
                NoGravity = true,
            });
        }
    }

    /// <summary>The dynamic muzzle-light flash (C22): a pooled <see cref="OmniLight3D"/> set to one
    /// of the muzzle_burst def's three third-person variants — the same 3-way RandomWeight over
    /// range and colour the data rolls — shown for a couple of frames at the muzzle.</summary>
    private void FlashMuzzleLight(Vector3 pos)
    {
        // A pre-tree volley (the weapon lab engages while still building) has no world transform
        // to place a light in — skip the flash rather than set GlobalPosition out of tree.
        if (!IsInsideTree())
            return;
        LightFlash? slot = null;
        foreach (var l in _lights)
        {
            if (!l.InUse)
            {
                slot = l;
                break;
            }
        }
        if (slot == null)
        {
            if (_lights.Count >= MaxMuzzleLights)
                return;
            slot = new LightFlash
            {
                Light = new OmniLight3D
                {
                    ShadowEnabled = false,
                    LightEnergy = MuzzleLightEnergy,
                    Visible = false,
                },
            };
            AddChild(slot.Light);
            _lights.Add(slot);
        }
        // The def's 3rdperson_lts variants: RANDOM_WEIGHT 0.333 / 0.333 / else, each a range band
        // and a colour; the range within the band is a random pick.
        float roll = _rng.Randf();
        float range;
        Color color;
        if (roll < 0.333f)
        {
            range = RandRange(1.0f, 2.0f);
            color = new Color(0.88f, 0.78f, 0.36f);
        }
        else if (roll < 0.667f)
        {
            range = RandRange(1.25f, 3.25f);
            color = new Color(0.93f, 0.78f, 0.36f);
        }
        else
        {
            range = RandRange(2.0f, 3.75f);
            color = new Color(0.93f, 0.78f, 0.36f);
        }
        slot.Light.OmniRange = range;
        slot.Light.LightColor = color;
        slot.Light.GlobalPosition = pos;
        slot.Light.Visible = true;
        slot.Age = 0f;
        slot.InUse = true;
    }

    private void AgeCasings(float dt)
    {
        var spec = _casingSpec;
        if (spec == null)
            return;
        foreach (var c in _casings)
        {
            if (!c.InUse)
                continue;
            c.Age += dt;
            if (c.Age >= spec.RunTime)
            {
                c.InUse = false;
                c.Node.Visible = false;
                continue;
            }
            // Closed-form ballistic + tumble, exactly MotionRuntime's Seek: origin = start + v0·t
            // + ½·g·t², basis rotated about its own local X by the tumble rate.
            float t = c.Age;
            var origin = c.Start + c.V0 * t + new Vector3(0f, 0.5f * spec.Gravity * t * t, 0f);
            var basis = c.Basis.Rotated(c.Basis.X.Normalized(), spec.TumbleRate * t);
            c.Node.GlobalTransform = new Transform3D(basis, origin);
        }
    }

    private void AgeLights(float dt)
    {
        foreach (var l in _lights)
        {
            if (!l.InUse)
                continue;
            l.Age += dt;
            if (l.Age >= MuzzleLightLife)
            {
                l.InUse = false;
                l.Light.Visible = false;
            }
        }
    }

    /// <summary>A free (or freshly built) pooled casing instance, or null when the pool is at cap
    /// or the chapter gamez lacks the <c>gunshell</c> prototype.</summary>
    private CasingSlot? AcquireCasing()
    {
        foreach (var c in _casings)
        {
            if (!c.InUse)
                return c;
        }
        if (_casings.Count >= MaxCasings)
            return null;
        if (!_casingProtoResolved)
        {
            _casingProtoResolved = true;
            _casingProto = _flyoutGamez!.FindByName("gunshell");
            if (_casingProto == null)
                GD.Print("gun casing 'gunshell' absent from this chapter gamez — no ejection");
        }
        if (_casingProto == null)
            return null;
        var inst = _flyoutScene!.BuildSubtree(_casingProto, skip: null, collisionSkip: _ => true);
        if (inst == null)
            return null;
        _casingModels.AddChild(inst);
        inst.Visible = false;
        // Verification breadcrumb (once): confirms the prototype's child mesh actually instanced —
        // the root itself is meshless (model_index -1); the shell rides one level below.
        if (!_casingLogged)
        {
            _casingLogged = true;
            GD.Print($"gun casing 'gunshell' instanced: {CountMeshes(inst)} mesh(es)");
        }
        var slot = new CasingSlot { Node = inst };
        _casings.Add(slot);
        // Verification breadcrumb (once): sustained fire keeps this many casings alive at once —
        // per-shot pooled anchors, so nothing is dropped by a shared-anchor already-live gate.
        if (_casings.Count == 12)
            GD.Print($"gun casings: 12 live simultaneously (gun-rate ejection, no shared-anchor gate)");
        return slot;
    }

    /// <summary>Resolves the gunshell def's OBJECT_MOTION out of the anim program, once — gravity,
    /// the ranged launch, the tumble (its <c>Time</c> value is a TOTAL angle over run_time, the
    /// MotionRuntime decode: 20.94 rad = 1200° over 2 s) and the run time. Null without a program
    /// or when the def is absent; the miss is cached and logged once.</summary>
    private CasingSpec? CasingSpecResolve()
    {
        if (_casingSpecResolved)
            return _casingSpec;
        _casingSpecResolved = true;
        if (_flyoutAnims == null || _flyoutGamez == null || _flyoutScene == null)
            return null;
        foreach (var def in _flyoutAnims.ByAnimName("gunshell"))
        {
            foreach (var seq in def.Sequences)
            {
                foreach (var ev in seq.Events)
                {
                    if (ev.Kind != "ObjectMotion" || ev.Data.Obj("translation_range") is not { } range)
                        continue;
                    float runTime = ev.Data.Num("run_time") ?? 2f;
                    float fwdTotal = ev.Data.Obj("forward_rotation")?.Obj("Time")?.Num("initial") ?? 0f;
                    _casingSpec = new CasingSpec
                    {
                        Gravity = ev.Data.Obj("gravity")?.Num("value") ?? 0f,
                        XzMin = range.Obj("xz")?.Num("min") ?? 0f,
                        XzMax = range.Obj("xz")?.Num("max") ?? 0f,
                        YMin = range.Obj("y")?.Num("min") ?? 0f,
                        YMax = range.Obj("y")?.Num("max") ?? 0f,
                        SpeedMin = range.Obj("initial")?.Num("min") ?? 0f,
                        SpeedMax = range.Obj("initial")?.Num("max") ?? 0f,
                        RunTime = runTime,
                        TumbleRate = runTime > 0f ? fwdTotal / runTime : 0f,
                    };
                    GD.Print($"gun casing spec: gravity {_casingSpec.Gravity}, azimuth [{_casingSpec.XzMin},{_casingSpec.XzMax}]deg, " +
                             $"elevation [{_casingSpec.YMin},{_casingSpec.YMax}]deg, speed [{_casingSpec.SpeedMin},{_casingSpec.SpeedMax}] m/s, " +
                             $"tumble {_casingSpec.TumbleRate:0.##} rad/s over {runTime} s");
                    return _casingSpec;
                }
            }
        }
        GD.Print("gun casing 'gunshell' def not in the anim program — no ejection");
        return null;
    }

    private float RandRange(float a, float b) => a + _rng.Randf() * (b - a);

    // A weapon's SOUND binding (FIRE/IMPACT) may name a SOUND_GROUPS entry (e.g. the incendiary
    // rocket's ground_mixed_exp_sg default impact) rather than a plain sounds.json SETS def —
    // resolve it through the group first, same as WorldSounds.PlayOneShot, or the lookup below
    // misses and the call silently no-ops (BL-211).
    private void PlaySound(string sndName)
    {
        if (_sounds == null || _soundDefs == null)
            return;
        string resolved = _soundGroups != null && _soundGroups.TryGetValue(sndName, out var group)
            ? group.Pick(_soundGroupRng) ?? sndName
            : sndName;
        if (!_soundDefs.TryGetValue(resolved, out var def))
            return;
        var stream = _sounds.Find(def.WavName, looped: false);
        if (stream == null)
            return;
        var player = _sfxPool[_sfxNext];
        _sfxNext = (_sfxNext + 1) % _sfxPool.Count;
        player.Stream = stream;
        player.VolumeDb = Mathf.LinearToDb(Mathf.Max(0.002f, def.Volume * 0.2f));
        player.Play();
    }

    // The minimum world-space size (m) that projects to `pixels` on screen at `distance` from the
    // listener camera (C23) — inverts Godot's default vertical (KEEP_HEIGHT) perspective projection:
    // screenPx = worldSize * viewportHeight / (2 * distance * tan(fov/2)). 0 with no bound camera or
    // a degenerate distance/viewport (the weapon lab, a headless dump with no listener).
    private float MinWorldSizeForPixels(float distance, float pixels)
    {
        if (_listener == null || distance <= 0f || pixels <= 0f)
            return 0f;
        float viewportHeight = _listener.GetViewport()?.GetVisibleRect().Size.Y ?? 0f;
        if (viewportHeight <= 0f)
            return 0f;
        float fovRad = Mathf.DegToRad(_listener.Fov);
        return pixels * 2f * distance * Mathf.Tan(fovRad * 0.5f) / viewportHeight;
    }

    private void RenderTracers()
    {
        // A velocity-aligned, camera-facing streak: the quad's local Y (its length) lies along the
        // flight direction, its local Z (the normal) points as near the camera as staying ⟂ to Y
        // allows, and local X is the width. Not billboarded, so the streak keeps its length instead
        // of collapsing to a screen-vertical bar. The quad trails behind the round by half its length.
        var eye = _listener?.GlobalPosition;
        // Config-driven look (C23): read once per frame, not per round — a session-wide setting,
        // not a per-shot one. Falls through to the in-code defaults verbatim with no config.json.
        float cfgLength = Config.GetFloat("weapons.tracerLength", TracerLength);
        float cfgWidth = Config.GetFloat("weapons.tracerWidth", TracerWidth);
        float minPixels = Config.GetFloat("weapons.tracerMinPixels", TracerMinPixels);
        System.Array.Clear(_tracerCounts, 0, _tracerCounts.Length);
        for (int i = 0; i < _projHigh; i++)
        {
            ref var p = ref _proj[i];
            if (!p.Alive)
                continue;
            // Carry the rocket body along with the round, nose down its velocity (B14).
            if (p.Model != null)
            {
                var pose = FlyoutPose(p.Pos, p.Vel);
                // The def's spinner (the sonic's ObjectMotion XYZ_ROTATION, 8.73 rad/s): a steady
                // roll about the round's own nose axis — pure roll, so the nose stays on velocity.
                if (p.RollRate != 0f)
                    pose = new Transform3D(pose.Basis * new Basis(Vector3.Back, p.RollRate * p.Age), pose.Origin);
                p.Model.GlobalTransform = pose;
                // Verification breadcrumb (once): read the APPLIED world basis back — through the
                // prototype's own parent chain — and confirm the body's nose (local -Z) actually
                // aligns with the round's flight direction. dot≈1 ⇒ nose-forward.
                if (!_flyoutPoseLogged)
                {
                    _flyoutPoseLogged = true;
                    var nose = -p.Model.GlobalTransform.Basis.Z.Normalized();
                    var vdir = p.Vel.Normalized();
                    GD.Print($"flyout orientation: nose·velocity = {nose.Dot(vdir):0.000} (1.000 = nose-forward)");
                }
            }
            var yAxis = p.Vel.Normalized();
            var toEyeVec = eye is { } e ? (e - p.Pos) : Vector3.Up;
            float eyeDist = toEyeVec.Length();
            var toEye = eyeDist > 1e-6f ? toEyeVec / eyeDist : Vector3.Up;
            var zAxis = (toEye - yAxis * toEye.Dot(yAxis)); // camera dir, projected ⟂ to the streak
            if (zAxis.LengthSquared() < 1e-6f)
                zAxis = yAxis.Cross(Vector3.Right);
            zAxis = zAxis.Normalized();
            var xAxis = yAxis.Cross(zAxis).Normalized();
            // A rocket with a MODEL body trails a slim exhaust; one without (chapter missing the
            // prototype) keeps the fatter stand-in streak so it still reads; guns stay at 1×.
            float scale = p.Model != null ? RocketExhaustScale
                : p.Weapon.IsRocket ? RocketStreakScale
                : 1f;
            // The distance-visibility floor (C23): the minimum world size that still covers
            // minPixels on screen at this round's distance from the listener camera — 0 with no
            // camera bound (the weapon lab). Raises the streak's baseline size before the muzzle-growth
            // cap below, so a round that has flown far enough to need it still gets to show it; a
            // fresh round can never exceed how far it has actually travelled, floor or not.
            float floorSize = MinWorldSizeForPixels(eyeDist, minPixels);
            float width = Mathf.Max(cfgWidth * scale, floorSize);
            // Cap the drawn streak to how far the round has actually flown, so it grows out of the
            // muzzle instead of pre-extending a full length behind it on the spawn frame.
            float traveled = Mathf.Max(0f, (p.Weapon.Range ?? 1000f) - p.DistLeft);
            float len = Mathf.Min(Mathf.Max(cfgLength * scale, floorSize), traveled);
            // The authored texture's head sits at the opposite end of its V axis from where this
            // quad's +local-Y (the round's current position, per the trailing offset below) lands —
            // rotate the quad 180° about its own facing normal (negate X and Y together, a proper
            // rotation, not a mirror) so the bright head reads at the round instead of the tail (C21).
            var basis = new Basis(-yAxis * len, -xAxis * width, zAxis);
            var mm = _tracerMm[p.TracerIdx];
            int n = _tracerCounts[p.TracerIdx]++;
            mm.SetInstanceTransform(n, new Transform3D(basis, p.Pos - yAxis * (len * 0.5f)));
            mm.SetInstanceColor(n, p.Tint);
        }
        for (int i = 0; i < _tracerMm.Length; i++)
            _tracerMm[i].VisibleInstanceCount = _tracerCounts[i];
    }

    private struct Proj
    {
        public bool Alive;
        public Vector3 Pos;
        public Vector3 Vel;      // m/s, world
        public float DistLeft;   // m until it expires at RANGE
        public float Accel;      // ACCELERATION along the velocity direction, m/s²
        public float Grav;       // GRAVITY scale × world gravity, m/s² (0 throughout this install)
        public WeaponDef Weapon;
        public Color Tint;       // tracer brightness multiplier (uniform, TracerTint) — C25
        public int TracerIdx;    // which TracerTextures entry/MultiMesh this round's streak draws into
        public Node3D? Model;    // the FLYOUT MODEL body (rockets only; null for gun tracers) — B14
        public TrailEmitter[]? Trails; // the FLYOUT MODEL_ANIMATION smoke-trail emitters (C21)
        public float RollRate;   // rad/s about the nose axis (the sonic spinner); 0 = no roll
        public float Age;        // s since launch — drives the roll angle
    }

    private struct Sprite
    {
        public Vector3 Pos;
        public float Age;
        public float Life;
        public float Size;
        public Color Tint;
        public Basis Orient;   // unit quad orientation: X width, Y height, Z the facing normal.
                               // Muzzle flashes roll in the firing plane's basis; impact sprites
                               // face the struck surface normal — a fixed world plane for neither.
        public Vector3 Vel;    // m/s, world; zero for every sprite but dirt debris and smoke
        public Vector3 SpinAxis; // unit axis the debris tumbles about; unused when SpinRate is 0
        public float SpinRate; // rad/s about SpinAxis; zero for every sprite but dirt debris
        public bool NoGravity; // smoke puffs drift on their spawn velocity; debris arcs (false)
        public bool AnchorLeft; // Pos is the texture's left edge (UV x=0), not the quad centre —
                                // the muzzle flash triad (C21); the centre is derived in RenderSprites
                                // from the *current* (shrinking) size so the anchor doesn't drift.
    }

    // A named IMPACT effect that resolved to a chapter-gamez MODEL prototype (the authored water
    // splash `splash1.flt`/`bsplsh.flt`), instanced at the hit point (D30). A splash model's
    // `*_base`/`*_splash` children are driven along the authored scale curves for the def's 2 s
    // run (A2); a model with neither child just shows briefly.
    private struct ImpactFx
    {
        public Node3D Model;
        public float Age;
        public float Life;
        public Node3D? Base;        // the `*_base` disc child (null: not a splash model)
        public Node3D? Splash;      // the `*_splash` column child
        public Transform3D BaseRest;
        public Transform3D SplashRest;
    }

    // One reusable trail emitter: a Puffer built for one authored PUFFER_STATE, owned by the pool
    // and lent to one live round at a time (see AcquireTrails).
    private sealed class TrailEmitter
    {
        public Puffer Puffer = null!;
        public PufferState State = null!;
        public bool InUse;
    }

    // A FLYOUT MODEL_ANIMATION def's renderable content: its trail puffer states + spin rate.
    private sealed class TrailSpec
    {
        public readonly List<PufferState> States = new();
        public float RollRate;
    }

    // The gunshell def's OBJECT_MOTION, read once from the anim program (C22): the authored
    // ranged ballistic launch + tumble every ejected casing flies.
    private sealed class CasingSpec
    {
        public float Gravity;     // m/s², negative (LOCAL -3.0)
        public float XzMin, XzMax; // launch AZIMUTH range, degrees (translation_range)
        public float YMin, YMax;   // launch ELEVATION range, degrees (negative = downward)
        public float SpeedMin, SpeedMax; // launch speed range, m/s (translation_range.initial)
        public float RunTime;     // s the casing lives
        public float TumbleRate;  // rad/s about local X (forward_rotation Time ÷ run_time)
    }

    // One pooled casing: a gunshell subtree instance lent to one ejection at a time.
    private sealed class CasingSlot
    {
        public Node3D Node = null!;
        public Vector3 Start;   // launch position (world)
        public Vector3 V0;      // launch velocity (world), m/s
        public Basis Basis;     // launch orientation — the tumble rotates it about its local X
        public float Age;
        public bool InUse;
    }

    // One pooled muzzle-light flash: a real OmniLight3D shown for a couple of frames per shot.
    private sealed class LightFlash
    {
        public OmniLight3D Light = null!;
        public float Age;
        public bool InUse;
    }
}
