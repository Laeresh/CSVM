using System;
using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>The enhanced presentation's heat shimmer: the air over and behind a fireball refracts
/// while the fireball burns. One billboard quad per burst from a fixed pool, drawn as a single
/// MultiMesh so every live burst costs one draw call and ONE colour-buffer copy rather than one
/// each. The quad samples the screen behind it at a noise-driven offset, so it moves light that is
/// already there and adds none: it cannot lift a pixel over the glow threshold.
/// Built only under <see cref="GraphicsMode.Enhanced"/>; the faithful presentation builds no pool
/// and no node, and the decision is <c>WorldEffectsFactory.RegisterHeatShimmer</c>'s alone.
/// âš  Every size, rise, decay and amplitude constant below is TUNE, judged at the controls.
/// </summary>
public sealed partial class HeatShimmer : Node3D
{
    /// <summary>Live quads at once. A screen-texture read forces a copy of the colour buffer, so
    /// the pool is capped rather than grown: past this the oldest burst's quad is recycled, which
    /// costs the tail of a fading burst instead of the frame time of an unbounded salvo.</summary>
    public const int PoolCap = 8;

    // --- TUNE: the look, pending the user's judgement at the controls ---
    private const float StartWidth = 26f;     // quad width at ignition (m)
    private const float HeightRatio = 1.2f;   // height over width: hot air is a column, not a disc
    private const float GrowthRate = 10f;     // width added per second as the plume spreads (m/s)
    private const float Lift = 30f;           // centre above the burst point, clear of the flame (m)
    private const float RiseSpeed = 6f;       // and how fast that centre climbs (m/s)
    private const float DecaySeconds = 0.8f;  // e-folding time of the amplitude envelope
    private const float FloorStrength = 0.02f; // below this the quad moves no pixel; free the slot
    private const float Amplitude = 0.32f;    // peak offset as a fraction of the quad's half-width
    private const float MasterAlpha = 0.85f;  // peak coverage, see the transparent-pass note below
    private const float NoiseScale = 2.6f;    // noise cells across the quad
    private const float ScrollRate = 0.55f;   // how fast the cells climb (quad heights per second)
    private const float PhaseStride = 0.618f; // per-burst noise phase step, so a salvo does not sync

    // The screen sample is written back with no tint and no gain: a blend between two samples of
    // one buffer cannot exceed the brighter of them, so the quad lifts no pixel over the glow bar.
    // âš  The screen copy a transparent material reads is taken BEFORE the transparent pass, so it
    // holds the opaque world and NOT the fireball or its smoke, and no render priority changes
    // that, which is why the quad stands clear above the flame rather than over it.
    // âš  Never use Godot's TIME here; csky_time is the sim clock every animated shader reads.
    private const string ShaderCode = """
        shader_type spatial;
        render_mode blend_mix, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

        #include "res://shaders/csky_time.gdshaderinc"

        uniform sampler2D screen_tex : hint_screen_texture, repeat_disable, filter_linear;
        uniform float amplitude = 0.32;
        uniform float master_alpha = 0.85;
        uniform float noise_scale = 2.6;
        uniform float scroll_rate = 0.55;

        varying float v_strength;
        varying float v_phase;
        varying float v_span;

        float hash21(vec2 p) {
            p = fract(p * vec2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return fract(p.x * p.y);
        }

        float vnoise(vec2 p) {
            vec2 i = floor(p);
            vec2 f = fract(p);
            vec2 u = f * f * (3.0 - 2.0 * f);
            float a = hash21(i);
            float b = hash21(i + vec2(1.0, 0.0));
            float c = hash21(i + vec2(0.0, 1.0));
            float d = hash21(i + vec2(1.0, 1.0));
            return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
        }

        void vertex() {
            v_strength = INSTANCE_CUSTOM.x;
            v_phase = INSTANCE_CUSTOM.y;
            vec3 center = MODEL_MATRIX[3].xyz;
            float w = length(MODEL_MATRIX[0].xyz);
            float h = length(MODEL_MATRIX[1].xyz);
            vec3 right = INV_VIEW_MATRIX[0].xyz;
            vec3 up = INV_VIEW_MATRIX[1].xyz;
            vec3 world = center + right * VERTEX.x * w + up * VERTEX.y * h;

            // The offset below is a fraction of the quad's OWN width on screen, so a burst two
            // hundred metres off wobbles the few pixels it covers instead of the same screen
            // fraction a near one does.
            vec4 c0 = PROJECTION_MATRIX * (VIEW_MATRIX * vec4(center, 1.0));
            vec4 c1 = PROJECTION_MATRIX * (VIEW_MATRIX * vec4(center + right * w * 0.5, 1.0));
            v_span = abs(c1.x / max(c1.w, 1e-4) - c0.x / max(c0.w, 1e-4)) * 0.5;

            POSITION = PROJECTION_MATRIX * (VIEW_MATRIX * vec4(world, 1.0));
        }

        void fragment() {
            // UV.y runs downward, so p.y is +1 at the quad's bottom, which is the end that sits
            // over the flame. The taper there keeps the refraction in the rising air above it.
            vec2 p = (UV - 0.5) * 2.0;
            float mask = 1.0 - smoothstep(0.35, 1.0, length(p));
            mask *= mask * (1.0 - smoothstep(0.15, 0.85, p.y));
            if (mask * v_strength <= 0.0) {
                discard;
            }
            vec2 cell = p * noise_scale + vec2(v_phase, -csky_time * scroll_rate);
            float nx = vnoise(cell) - 0.5;
            float ny = vnoise(cell * 2.1 + vec2(v_phase * 1.7, 11.3)) - 0.5;
            vec2 off = vec2(nx, ny) * 2.0 * amplitude * v_span * mask * v_strength;
            ALBEDO = texture(screen_tex, clamp(SCREEN_UV + off, vec2(0.0), vec2(1.0))).rgb;
            ALPHA = mask * v_strength * master_alpha;
        }
        """;

    private readonly Slot[] _slots = new Slot[PoolCap];
    private ShaderMaterial _mat = null!;
    private MultiMeshInstance3D _mmi = null!;
    private MultiMesh _mm = null!;
    private int _spawned;
    private double _time;

    /// <summary>Quads refracting right now, the count the pool cap bounds.</summary>
    public int LiveCount { get; private set; }

    /// <summary>How often a spawn landed on a still-live slot because the cap was full, so the cap
    /// can be judged against real salvoes rather than assumed.</summary>
    public int Recycles { get; private set; }

    /// <summary>The sim time the pool last aged on, taken from the session clock at the attach and
    /// advanced every frame. A caller stepping the pool by hand starts from here rather than from
    /// zero, which would age every live quad by the whole session.</summary>
    internal double SimTime => _time;

    /// <summary>Whether the pool's one draw is submitted this frame. It is false with nothing
    /// burning, which is what keeps the colour-buffer copy a screen-texture read forces off the
    /// frame entirely rather than paying for it over an empty pool.</summary>
    internal bool Drawing => _mmi.Visible;

    /// <summary>Builds the pool under <paramref name="parent"/> and returns it. Enhanced only, and
    /// the caller owns that gate: this is called from the one place a fireball is decided to shimmer
    /// (<c>WorldEffectsFactory.RegisterHeatShimmer</c>), so the faithful path never reaches it and
    /// builds nothing.</summary>
    public static HeatShimmer Attach(Node3D parent)
    {
        var pool = new HeatShimmer();
        pool.Init();
        // Ages are measured against the session clock, so the pool starts on it rather than at
        // zero: a burst in the tenth minute would otherwise be born ten minutes old and retire
        // the frame after it lit.
        pool._time = GameClock.Current?.Time ?? 0.0;
        parent.AddChild(pool);
        return pool;
    }

    /// <summary>Lights a quad over the burst at <paramref name="at"/>. <paramref name="stillBurning"/>
    /// is the fireball's own liveness, the same closure the burst light reads, so the quad is dropped
    /// the frame the fireball ends rather than on a clock of its own.</summary>
    public void Spawn(Vector3 at, Func<bool> stillBurning)
    {
        int slot = FreeSlot();
        _slots[slot] = new Slot
        {
            Active = true,
            At = at,
            SpawnTime = _time,
            Phase = _spawned * PhaseStride,
            StillBurning = stillBurning,
        };
        _spawned++;
        Step(_time);
    }

    public override void _Process(double delta)
    {
        // The clock the effects run on. With no session clock (a lab, a teardown frame) the pool
        // holds where it is rather than ageing on the wall delta, since nothing can be burning.
        var clock = GameClock.Current;
        if (clock != null)
            Step(clock.Time);
    }

    /// <summary>Ages every live quad on the sim clock and writes this frame's instance data. Called
    /// once per rendered frame; <paramref name="simTime"/> is the session clock's time, never the
    /// wall clock, so a halted or fixed-step run shimmers by frame count.</summary>
    internal void Step(double simTime)
    {
        _time = simTime;
        int live = 0;
        for (int i = 0; i < PoolCap; i++)
        {
            if (!_slots[i].Active)
                continue;
            float strength = Strength(in _slots[i]);
            if (strength <= 0f)
            {
                Retire(i);
                continue;
            }
            float age = (float)(simTime - _slots[i].SpawnTime);
            float width = StartWidth + (age * GrowthRate);
            var center = _slots[i].At + (Vector3.Up * (Lift + (age * RiseSpeed)));
            _mm.SetInstanceTransform(i, new Transform3D(
                Basis.Identity.Scaled(new Vector3(width, width * HeightRatio, 1f)), center));
            _mm.SetInstanceCustomData(i, new Color(strength, _slots[i].Phase, 0f, 0f));
            live++;
        }
        LiveCount = live;
        _mmi.Visible = live > 0;
    }

    // Full at ignition and decaying from there: the air is hottest the instant the charge goes
    // off, and the quad appears in the same frame as the fireball it stands over, so there is
    // nothing for a ramp-in to hide.
    private float Strength(in Slot slot)
    {
        if (!slot.StillBurning())
            return 0f;
        float strength = Mathf.Exp(-(float)(_time - slot.SpawnTime) / DecaySeconds);
        return strength < FloorStrength ? 0f : strength;
    }

    private int FreeSlot()
    {
        int oldest = 0;
        for (int i = 0; i < PoolCap; i++)
        {
            if (!_slots[i].Active)
                return i;
            if (_slots[i].SpawnTime < _slots[oldest].SpawnTime)
                oldest = i;
        }
        Recycles++;
        return oldest;
    }

    private void Retire(int i)
    {
        _slots[i] = default;
        _mm.SetInstanceTransform(i, Transform3D.Identity.Scaled(Vector3.Zero));
        _mm.SetInstanceCustomData(i, new Color(0f, 0f, 0f, 0f));
    }

    private void Init()
    {
        Name = "heat_shimmer";
        _mat = new ShaderMaterial
        {
            Shader = new Shader { Code = ShaderCode },
            // Drawn BEFORE the other transparents, the fireball and its smoke included. The screen
            // copy below holds none of them, so a quad drawn last would paint the bare opaque world
            // over whatever flame or smoke it covers; drawn first, they composite over it instead.
            RenderPriority = -1,
        };
        _mat.SetShaderParameter("amplitude", Amplitude);
        _mat.SetShaderParameter("master_alpha", MasterAlpha);
        _mat.SetShaderParameter("noise_scale", NoiseScale);
        _mat.SetShaderParameter("scroll_rate", ScrollRate);

        _mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = new QuadMesh { Size = Vector2.One },
            InstanceCount = PoolCap,
        };
        for (int i = 0; i < PoolCap; i++)
            Retire(i);

        _mmi = new MultiMeshInstance3D
        {
            Multimesh = _mm,
            MaterialOverride = _mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
            // Quads stand wherever a burst went off, far from this node's origin, so a world-sized
            // custom AABB is what keeps the one draw call from being frustum-culled. Same reasoning
            // as the precipitation and wind-streak fields'.
            CustomAabb = new Aabb(new Vector3(-40000f, -40000f, -40000f), new Vector3(80000f, 80000f, 80000f)),
        };
        AddChild(_mmi);
        Log.Info("world", $"heat shimmer: pool {PoolCap} quads, {StartWidth:0} m at ignition, {DecaySeconds:0.00} s decay, {Amplitude:0.000} amplitude (enhanced only)");
    }

    private struct Slot
    {
        public bool Active;
        public Vector3 At;
        public double SpawnTime;
        public float Phase;
        public Func<bool> StillBurning;
    }
}




























