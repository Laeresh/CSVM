using System.Collections.Generic;
using CSVM.Mech3;
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
    private struct Proj
    {
        public bool Alive;
        public Vector3 Pos;
        public Vector3 Vel;      // m/s, world
        public float DistLeft;   // m until it expires at RANGE
        public float Accel;      // ACCELERATION along the velocity direction, m/s²
        public float Grav;       // GRAVITY scale × world gravity, m/s² (0 throughout this install)
        public WeaponDef Weapon;
        public Color Tint;
        public Node3D? Model;    // the FLYOUT MODEL body (rockets only; null for gun tracers) — B14
    }

    private struct Sprite
    {
        public Vector3 Pos;
        public float Age;
        public float Life;
        public float Size;
        public Color Tint;
    }

    // A named IMPACT effect that resolved to a chapter-gamez MODEL prototype (the authored water
    // splash `splash1.flt`/`bsplsh.flt`), instanced at the hit point and shown briefly (D30).
    private struct ImpactFx
    {
        public Node3D Model;
        public float Age;
        public float Life;
    }

    private const int MaxProjectiles = 1024;
    private const int MaxFlashes = 128;
    private const float TracerLength = 14f;   // streak length behind the round, m
    private const float TracerWidth = 0.7f;   // m
    private const float RocketStreakScale = 2.4f; // fatter/longer streak, the fallback when a rocket has
                                                  // NO FLYOUT model (a chapter missing the prototype)
    private const float RocketExhaustScale = 0.5f; // a slim exhaust streak behind a rocket that HAS a
                                                   // MODEL body (B14): the body is the round, this is its trail
    private const float MuzzleSize = 2.2f;    // m
    private const float MuzzleLife = 0.05f;   // s
    private const float ImpactSize = 3.0f;    // m
    private const float ImpactLife = 0.14f;   // s
    private const float ImpactModelLife = 0.4f; // s the instanced IMPACT model shows before it is freed
    internal const float WorldGravity = 20f;  // nom_gravity (player.json) — only the 5 GRAVITY rockets use it
                                              // (shared: FlightController's reticle integration reads it too)

    // Tracer colours per ammo type, keyed off the tracer texture name axis (slug/dum/ap/mag).
    private static readonly Color SlugTint = new(1.0f, 0.85f, 0.35f);   // warm yellow
    private static readonly Color RocketTint = new(1.0f, 0.6f, 0.25f);  // orange exhaust

    private readonly Proj[] _proj = new Proj[MaxProjectiles];
    private int _projHigh;                     // highest slot ever used (bounds the scan)
    private readonly List<Sprite> _muzzle = new();
    private readonly List<Sprite> _impact = new();

    private readonly TextureArchive _textures;
    private readonly SoundArchive? _sounds;
    private readonly IReadOnlyDictionary<string, SoundDef>? _soundDefs;

    // The FLYOUT MODEL body (B14): rockets fly the original's own projectile mesh, instanced from a
    // chapter-gamez prototype root (`he_rocket`, `ap_rocket`, …) via the world's SceneBuilder — the
    // roots exist once per chapter and their geometry is nose-along-(-Z). Only rockets get a body:
    // guns fire ≤~10 rounds/s that live ~1 s each (dozens alive) and stay on the cheap MultiMesh
    // tracer quad, while a rocket lives ~0.8 s at 1/s (≤1 alive per player), so a full mesh per
    // rocket is cheap. Null archives (no world, or a chapter lacking the root) ⇒ streak-only fallback.
    private readonly GameZ? _flyoutGamez;
    private readonly SceneBuilder? _flyoutScene;
    private readonly Dictionary<string, GameZNode?> _flyoutNodes = new(); // model name → prototype (cached)
    private Node3D _flyoutModels = null!;   // container for the live rocket-body instances

    // The named IMPACT effect models (D30): a per-surface IMPACT `ANIMATION` whose name is a chapter
    // gamez node (the water splash prototypes) is instanced at the hit point via the same flyout
    // GameZ/SceneBuilder above. Names that resolve to a reader/control def or nothing (the gun
    // `3040slug_gunhit` smoke, `he_ground_effect`, `bld_damage.flt`) stay on the stand-in spark —
    // their runtime PUFFER_STATE half is D32 (the puffer factory is torn down after the world build).
    private readonly Dictionary<string, GameZNode?> _impactNodes = new(); // impact anim name → prototype (cached)
    private readonly HashSet<string> _impactFxLogged = new();
    private readonly List<ImpactFx> _impactFx = new();
    private Node3D _impactFxModels = null!;  // container for the short-lived impact-effect instances

    private MultiMesh _tracerMm = null!;
    private MultiMesh _muzzleMm = null!;
    private MultiMesh _impactMm = null!;
    private readonly PhysicsRayQueryParameters3D _ray = new(); // reused each step (no per-round alloc)
    private Camera3D? _listener;               // billboards align their streak to this camera
    private readonly List<AudioStreamPlayer> _sfxPool = new();
    private int _sfxNext;

    public ProjectilePool(TextureArchive textures, SoundArchive? sounds,
        IReadOnlyDictionary<string, SoundDef>? soundDefs,
        GameZ? flyoutGamez = null, SceneBuilder? flyoutScene = null)
    {
        _textures = textures;
        _sounds = sounds;
        _soundDefs = soundDefs;
        _flyoutGamez = flyoutGamez;
        _flyoutScene = flyoutScene;
        Name = "projectiles";
    }

    /// <summary>The camera a tracer streak orients its length toward (player 1's, in splitscreen).
    /// Tracers still render in every pane; only the streak's screen-space direction uses this.</summary>
    public Camera3D? Listener { get => _listener; set => _listener = value; }

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

    public override void _Ready()
    {
        // Tracers are velocity-aligned streaks (NOT billboarded — billboard would collapse the
        // long streak into a screen-vertical bar); muzzle/impact bursts ARE round billboards.
        _tracerMm = AddMultiMesh("tracer1", MaxProjectiles, additive: true, billboard: false, out _);
        _muzzleMm = AddMultiMesh("slug_muzzle1", MaxFlashes, additive: true, billboard: true, out _);
        _impactMm = AddMultiMesh("slug_muzzle2", MaxFlashes, additive: true, billboard: true, out _);
        _flyoutModels = new Node3D { Name = "flyout" };
        AddChild(_flyoutModels);
        _impactFxModels = new Node3D { Name = "impact_fx" };
        AddChild(_impactFxModels);
        for (int i = 0; i < 8; i++)
        {
            var p = new AudioStreamPlayer();
            AddChild(p);
            _sfxPool.Add(p);
        }
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

    /// <summary>Fires one round of <paramref name="weapon"/> from the world muzzle transform,
    /// inheriting the launch platform's velocity, with a random offset inside the weapon's
    /// <c>CANNON_SPREAD</c> cone. Also flashes the muzzle. Silently drops the round if the pool is
    /// momentarily full (a soft cap, never a crash).</summary>
    public void Spawn(WeaponDef weapon, Transform3D muzzle, Vector3 inheritVel)
    {
        var forward = -muzzle.Basis.Z.Normalized();
        forward = ApplySpread(forward, weapon.CannonSpread ?? 0f);
        float speed = weapon.Velocity ?? 500f;
        var tint = weapon.IsRocket ? RocketTint : SlugTint;

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
            _proj[slot] = new Proj
            {
                Alive = true,
                Pos = muzzle.Origin,
                Vel = vel,
                DistLeft = weapon.Range ?? 1000f,
                Accel = weapon.Acceleration ?? 0f,
                Grav = (weapon.Gravity ?? 0f) * WorldGravity,
                Weapon = weapon,
                Tint = tint,
                Model = model,
            };
            if (slot >= _projHigh)
                _projHigh = slot + 1;
        }

        if (_muzzle.Count < MaxFlashes)
            _muzzle.Add(new Sprite { Pos = muzzle.Origin, Life = MuzzleLife, Size = MuzzleSize, Tint = tint });
    }

    private static Vector3 ApplySpread(Vector3 forward, float coneDeg)
    {
        if (coneDeg <= 0f)
            return forward;
        // A random direction inside the cone: a random azimuth around `forward`, and a polar angle
        // in [0, cone] biased for a roughly uniform disc so the pattern fills the cone, not its rim.
        float half = Mathf.DegToRad(coneDeg) * 0.5f;
        float polar = half * Mathf.Sqrt(GD.Randf());
        float azimuth = GD.Randf() * Mathf.Tau;
        // Build a basis with `forward` as -Z, then tilt.
        var basis = Basis.LookingAt(forward, Mathf.Abs(forward.Dot(Vector3.Up)) > 0.99f ? Vector3.Right : Vector3.Up);
        var tilted = basis * new Vector3(
            Mathf.Sin(polar) * Mathf.Cos(azimuth),
            Mathf.Sin(polar) * Mathf.Sin(azimuth),
            -Mathf.Cos(polar));
        return tilted.Normalized();
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
        _impactFx.Add(new ImpactFx { Model = inst, Age = 0f, Life = ImpactModelLife });
        if (_impactFxLogged.Add(animName))
            GD.Print($"impact effect '{animName}' instanced: {meshes} mesh(es)");
        return true;
    }

    private readonly HashSet<string> _flyoutLogged = new();
    private bool _flyoutPoseLogged;

    private static int CountMeshes(Node n)
    {
        int c = n is MeshInstance3D ? 1 : 0;
        foreach (var child in n.GetChildren())
            c += CountMeshes(child);
        return c;
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

    private static void KillModel(ref Proj p)
    {
        if (p.Model != null)
        {
            p.Model.QueueFree();
            p.Model = null;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
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
                    Impact(p.Weapon, (Vector3)hit["position"], hit["collider"].Obj as Node);
                    p.Alive = false;
                    KillModel(ref p);
                    continue;
                }
            }
            p.Pos = next;
            p.DistLeft -= stepLen;
            if (p.DistLeft <= 0f)
            {
                p.Alive = false;   // spent — expires without an impact
                KillModel(ref p);
            }
        }

        AgeSprites(_muzzle, dt);
        AgeSprites(_impact, dt);
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
                _impactFx[i] = f;
            }
        }
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
                sprites[i] = s;
            }
        }
    }

    private int _impactsLogged;

    private void Impact(WeaponDef weapon, Vector3 point, Node? collider)
    {
        var surface = ClassifySurface(collider);
        // Verification breadcrumb: the first few impacts confirm hit detection + surface
        // classification (B15) without needing a lucky screenshot; then it goes quiet.
        if (_impactsLogged < 8)
        {
            _impactsLogged++;
            GD.Print($"impact: {weapon.Id} ({weapon.Name}) -> {surface} at " +
                     $"({point.X:0},{point.Y:0},{point.Z:0}) on {collider?.GetParent()?.Name}/{collider?.Name}");
        }
        // The per-surface IMPACT binding: the struck surface's entry, else the weapon's `default`.
        if (!weapon.Impact.TryGetValue(surface, out var effect))
            weapon.Impact.TryGetValue(SurfaceClass.Default, out effect);

        // The named IMPACT effect (D30): when its `ANIMATION`/`SURFACE_ANIMATION` names a chapter
        // gamez node (the water splash prototypes), instance it at the hit point and skip the spark
        // — the authored model IS the effect. The gun/rocket smoke+fireball names resolve to reader
        // defs or nothing, so nothing instances and the spark stands in (their PUFFER_STATE is D32).
        var fxName = effect != null ? (effect.Animation ?? effect.SurfaceAnimation) : null;
        bool showedModel = fxName != null && SpawnImpactModel(fxName, point);
        // The puffer half (D32): when the effect is not a gamez model, hand its name to the
        // world-effects runtime, which builds the smoke/fireball at the hit. Rockets/ordnance only
        // (the gun `gunhit` smoke is a documented follow-up — see EffectSink). The runtime no-ops on
        // a name it does not carry (the inert `bld_damage.flt`/`f18sparks2`/…), so the spark below
        // still stands in for those.
        if (!showedModel && fxName != null && !weapon.IsGun)
            EffectSink?.Invoke(fxName, point);
        if (!showedModel && _impact.Count < MaxFlashes)
        {
            var tint = surface == SurfaceClass.Water ? new Color(0.8f, 0.9f, 1.0f) : new Color(1f, 0.9f, 0.5f);
            _impact.Add(new Sprite { Pos = point, Life = ImpactLife, Size = ImpactSize, Tint = tint });
        }
        // Per-surface IMPACT sound (landed): the struck surface's SOUND, else the default's.
        if (effect?.Sound is { } snd)
            PlaySound(snd);
        // Apply the hit to whatever destructible was struck (C23) — a no-op for terrain/water/clutter.
        DamageSink?.Invoke(collider, weapon.HealthDamage ?? 0f);
    }

    private static SurfaceClass ClassifySurface(Node? collider)
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

    private void PlaySound(string sndName)
    {
        if (_sounds == null || _soundDefs == null || !_soundDefs.TryGetValue(sndName, out var def))
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

    public override void _Process(double delta)
    {
        RenderTracers();
        RenderSprites(_muzzleMm, _muzzle);
        RenderSprites(_impactMm, _impact);
    }

    private void RenderTracers()
    {
        // A velocity-aligned, camera-facing streak: the quad's local Y (its length) lies along the
        // flight direction, its local Z (the normal) points as near the camera as staying ⟂ to Y
        // allows, and local X is the width. Not billboarded, so the streak keeps its length instead
        // of collapsing to a screen-vertical bar. The quad trails behind the round by half its length.
        var eye = _listener?.GlobalPosition;
        int n = 0;
        for (int i = 0; i < _projHigh; i++)
        {
            ref var p = ref _proj[i];
            if (!p.Alive)
                continue;
            // Carry the rocket body along with the round, nose down its velocity (B14).
            if (p.Model != null)
            {
                p.Model.GlobalTransform = FlyoutPose(p.Pos, p.Vel);
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
            var toEye = eye is { } e ? (e - p.Pos).Normalized() : Vector3.Up;
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
            var basis = new Basis(xAxis * (TracerWidth * scale), yAxis * (TracerLength * scale), zAxis);
            _tracerMm.SetInstanceTransform(n, new Transform3D(basis, p.Pos - yAxis * (TracerLength * scale * 0.5f)));
            _tracerMm.SetInstanceColor(n, p.Tint);
            n++;
        }
        _tracerMm.VisibleInstanceCount = n;
    }

    private static void RenderSprites(MultiMesh mm, List<Sprite> sprites)
    {
        int n = 0;
        foreach (var s in sprites)
        {
            float k = 1f - s.Age / s.Life;            // shrink + fade over life
            float size = s.Size * (0.6f + 0.4f * k);
            var basis = new Basis(Vector3.Right * size, Vector3.Up * size, Vector3.Back * size);
            mm.SetInstanceTransform(n, new Transform3D(basis, s.Pos));
            var c = s.Tint;
            c.A = k;
            mm.SetInstanceColor(n, c);
            n++;
        }
        mm.VisibleInstanceCount = n;
    }

    /// <summary>Deactivates every live round (R / respawn: no tracers hang in the air).</summary>
    public void Clear()
    {
        for (int i = 0; i < _projHigh; i++)
        {
            KillModel(ref _proj[i]);
            _proj[i].Alive = false;
        }
        _projHigh = 0;
        _muzzle.Clear();
        _impact.Clear();
        foreach (var f in _impactFx)
            f.Model.QueueFree();
        _impactFx.Clear();
    }
}
