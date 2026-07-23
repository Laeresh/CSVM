using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using Godot;

namespace CSVM.Effects;

/// <summary>How a puffer's sprites composite. <see cref="Auto"/> keeps the historical rule
/// (a COLORS ramp ⇒ <c>blend_mix</c>, else <c>blend_add</c>); the explicit modes override it
/// for effects whose blend the data does not imply — the crash <c>large_black_smokeball</c>
/// carries <c>colors: null</c> yet must render as MIX, because additive black smoke adds ~0
/// and is invisible (polish-4 item 8, gap 1).</summary>
public enum PufferBlend { Auto, Additive, Mix }

/// <summary>
/// The parameters of one <c>PUFFER_STATE</c> block from a zrdr effects reader
/// (flame_ball.json, fire.json, pufftrails.json, … all share this schema). A puffer
/// is the original engine's billboard-particle emitter: it spits <see cref="Number"/>
/// sprites per <see cref="TimeInterval"/>, each with a random velocity, size, and
/// lifetime, cycling a flipbook of textures over its age.
///
/// Only the fields we currently render are parsed; the reader carries more
/// (WORLD_ACCELERATION, FADE_RANGE camera-distance fades, NEAR_FADE) — added here as
/// they're needed. Distances/velocities are meters and seconds, matching the world.
/// </summary>
public sealed class PufferState
{
    public string Name = "";
    public float TimeInterval = 0.1f;
    public Vector3 LocalVelocity, WorldVelocity;
    public Vector3 MinRandomVelocity, MaxRandomVelocity;
    public Vector3 WorldAcceleration;
    public float Friction;
    public float SizeMin = 1f, SizeMax = 1f;
    public float LifetimeMin = 1f, LifetimeMax = 1f;
    public float GrowthFactor = 1f;
    public float DeviationDistance;
    public int Number = 1;

    /// <summary>AT_NODE's optional trailing offset (AT_NODE is [nodeName, dx?, dy?, dz?]),
    /// in the host node's own frame — the same convention as <see cref="LocalVelocity"/>.
    /// This is what spreads a multi-emitter effect around its anchor instead of stacking
    /// every emitter on the anchor's exact origin: C1's waterfall attaches three splash
    /// puffers to the single node <c>waterfall01</c>, offset ±11 m sideways and 8 m up so
    /// the spray reads as the base of the falls rather than one point source.</summary>
    public Vector3 AtNodeOffset;

    /// <summary>Trail emission (Run-2 item 10c): emit one sprite per this many meters
    /// of the followed node's motion (the smoke/fire trail puffers in
    /// pufftrails.json's dense_firetrail). 0 = burst-style (NUMBER per TIME_INTERVAL).</summary>
    public float DistanceInterval;

    /// <summary>Flipbook: texture name to show once the particle's age reaches Time (ascending).</summary>
    public IReadOnlyList<(float Time, string Texture)> TextureSequence = Array.Empty<(float, string)>();

    /// <summary>Static texture pool (the TEXTURES key — smoke101/102/103): each
    /// particle picks one at random and keeps it. Alternative to TextureSequence.</summary>
    public IReadOnlyList<string> Textures = Array.Empty<string>();

    /// <summary>Color-over-age ramp (the COLORS key): (lifeFraction, color) entries,
    /// ascending; rgb are 0–255 in the reader (normalized here), alpha 0–1. The
    /// dense_firetrail smoke is born orange (255,164,90) and turns near-black.</summary>
    public IReadOnlyList<(float Frac, Color Color)> Colors = Array.Empty<(float, Color)>();

    /// <summary>Loads a reader file (e.g. "flame_ball.json") from a zrdr zip/dir and returns
    /// the fully-defined <c>PUFFER_STATE</c> named <paramref name="pufferName"/>, or null.</summary>
    public static PufferState? Load(string zrdrPath, string fileName, string pufferName)
    {
        try
        {
            return FindInReader(Zrdr.LoadFile(zrdrPath, fileName), pufferName);
        }
        catch (Exception e)
        {
            GD.PushWarning($"could not load puffer '{pufferName}' from {fileName}: {e.Message}");
            return null;
        }
    }

    /// <summary>Depth-first search for the fully-defined PUFFER_STATE with the given NAME.
    /// The reader also holds "stop" stubs that share the name but only flip ACTIVE_STATE;
    /// those are skipped. Fully-defined = has NUMBER (burst emitters) or
    /// DISTANCE_INTERVAL (trail emitters, e.g. dense_firetrail's smoke/fire).</summary>
    public static PufferState? FindInReader(List<object?> reader, string pufferName)
    {
        PufferState? found = null;
        void Walk(List<object?> list)
        {
            for (int i = 0; i < list.Count && found == null; i++)
            {
                if (list[i] is string key
                    && key.Equals("PUFFER_STATE", StringComparison.OrdinalIgnoreCase)
                    && i + 1 < list.Count && list[i + 1] is List<object?> body)
                {
                    var d = ZrdrDict.FromAlternating(body);
                    if ((d.Has("NUMBER") || d.Has("DISTANCE_INTERVAL"))
                        && string.Equals(d.Str("NAME"), pufferName, StringComparison.OrdinalIgnoreCase))
                    {
                        found = Parse(d);
                        return;
                    }
                }
                if (list[i] is List<object?> child)
                    Walk(child);
            }
        }
        Walk(reader);
        return found;
    }

    /// <summary>
    /// Builds a state from a **compiled** animation's PUFFER_STATE event payload (the
    /// `AnimEvent.Data` of an `AnimRuntime` dispatch), as opposed to <see cref="Parse"/>'s
    /// reader form. The two agree: cross-checked on the C1 train's `steampuffer`, every
    /// shared field is identical (interval 0.03, LOCAL_VELOCITY 0/15/0, SIZE_RANGE 0.8–1.5,
    /// LIFETIME_RANGE 0.5–4.5, friction 3, the five texture names, the three-stop colour ramp).
    ///
    /// Two shape differences worth knowing:
    /// - the emission interval is **always** in `interval_garbage.interval_value` — the
    ///   `interval` field itself is null in all 4,387 PUFFER_STATE events of this install;
    /// - `GROWTH_FACTOR` arrives as a two-entry `growth_factors` array whose **second entry's
    ///   max** is the reader's scalar (matches on 172 of 177 puffers whose name resolves to a
    ///   single reader definition; the five outliers are names defined differently in
    ///   different readers, not a mapping failure).
    /// </summary>
    public static PufferState FromAnimEvent(AnimData d)
    {
        Vector3 Vec(string key) => d.Vec3(key);
        float Range(string key, string end, float fallback) =>
            d.Obj(key)?.Num(end) ?? fallback;

        var s = new PufferState
        {
            Name = d.Str("name") ?? "",
            TimeInterval = d.Obj("interval")?.Num("value")
                           ?? d.Obj("interval_garbage")?.Num("interval_value") ?? 0.1f,
            LocalVelocity = Vec("local_velocity"),
            WorldVelocity = Vec("world_velocity"),
            MinRandomVelocity = Vec("min_random_velocity"),
            MaxRandomVelocity = Vec("max_random_velocity"),
            WorldAcceleration = Vec("world_acceleration"),
            Friction = d.Num("friction") ?? 0f,
            SizeMin = Range("size_range", "min", 1f),
            SizeMax = Range("size_range", "max", 1f),
            LifetimeMin = Range("lifetime_range", "min", 1f),
            LifetimeMax = Range("lifetime_range", "max", 1f),
            DeviationDistance = d.Num("deviation_distance") ?? 0f,
            Number = Mathf.Max(1, (int)(d.Num("number") ?? 1f)),
            AtNodeOffset = Vec("translate"),
        };

        var growth = new List<AnimData>(d.Objects("growth_factors"));
        s.GrowthFactor = growth.Count >= 2 ? growth[1].Num("max") ?? 1f
            : growth.Count == 1 ? growth[0].Num("max") ?? 1f : 1f;

        // textures[] entries carry an optional run_time: present ⇒ a timed flipbook,
        // absent ⇒ the static pool each particle picks one from.
        var pool = new List<string>();
        var seq = new List<(float, string)>();
        foreach (var t in d.Objects("textures"))
        {
            if (t.Str("name") is not { } texName)
                continue;
            if (t.Num("run_time") is { } runTime)
                seq.Add((runTime, texName));
            else
                pool.Add(texName);
        }
        s.Textures = pool;
        s.TextureSequence = seq;

        // colors[]: {unk00 = life fraction, color = rgb 0–255, unk16 = alpha 0–1}, the same
        // ramp the reader spells [frac, r, g, b, a].
        var colors = new List<(float, Color)>();
        foreach (var c in d.Objects("colors"))
        {
            var rgb = c.Obj("color");
            if (rgb == null)
                continue;
            float r = rgb.Num("r") ?? 0f, g = rgb.Num("g") ?? 0f, b = rgb.Num("b") ?? 0f;
            float scale = r > 1f || g > 1f || b > 1f ? 1f / 255f : 1f;
            colors.Add((c.Num("unk00") ?? 0f,
                new Color(r * scale, g * scale, b * scale, c.Num("unk16") ?? 1f)));
        }
        s.Colors = colors;
        return s;
    }

    private static PufferState Parse(ZrdrDict d)
    {
        Vector3 Vec(string key) =>
            new(d.Float(key, index: 0), d.Float(key, index: 1), d.Float(key, index: 2));

        var s = new PufferState
        {
            Name = d.Str("NAME") ?? "",
            TimeInterval = d.Float("TIME_INTERVAL", 0.1f),
            LocalVelocity = Vec("LOCAL_VELOCITY"),
            WorldVelocity = Vec("WORLD_VELOCITY"),
            MinRandomVelocity = Vec("MIN_RANDOM_VELOCITY"),
            MaxRandomVelocity = Vec("MAX_RANDOM_VELOCITY"),
            WorldAcceleration = Vec("WORLD_ACCELERATION"),
            Friction = d.Float("FRICTION"),
            SizeMin = d.Float("SIZE_RANGE", 1f, 0),
            SizeMax = d.Float("SIZE_RANGE", 1f, 1),
            LifetimeMin = d.Float("LIFETIME_RANGE", 1f, 0),
            LifetimeMax = d.Float("LIFETIME_RANGE", 1f, 1),
            GrowthFactor = d.Float("GROWTH_FACTOR", 1f),
            DeviationDistance = d.Float("DEVIATION_DISTANCE"),
            Number = (int)d.Float("NUMBER", 1f),
            DistanceInterval = d.Float("DISTANCE_INTERVAL"),
            // AT_NODE is [nodeName, dx?, dy?, dz?] — the offset starts at index 1, past the name.
            AtNodeOffset = new Vector3(d.Float("AT_NODE", 0f, 1), d.Float("AT_NODE", 0f, 2), d.Float("AT_NODE", 0f, 3)),
        };

        var seq = new List<(float, string)>();
        foreach (var item in d.List("TEXTURE_SEQUENCE") ?? new List<object?>())
            if (item is List<object?> pair && pair.Count >= 2
                && pair[0] is float t && pair[1] is string tex)
                seq.Add((t, tex));
        s.TextureSequence = seq;

        var texList = new List<string>();
        foreach (var item in d.List("TEXTURES") ?? new List<object?>())
            if (item is string name)
                texList.Add(name);
        s.Textures = texList;

        // COLORS entries are [lifeFrac, r, g, b, a] with rgb 0–255 (the same
        // integer-encoding rule as weather.json: any component > 1 ⇒ /255).
        var colors = new List<(float, Color)>();
        foreach (var item in d.List("COLORS") ?? new List<object?>())
            if (item is List<object?> { Count: >= 5 } c
                && c[0] is float frac && c[1] is float r && c[2] is float g
                && c[3] is float b && c[4] is float a)
            {
                float scale = r > 1f || g > 1f || b > 1f ? 1f / 255f : 1f;
                colors.Add((frac, new Color(r * scale, g * scale, b * scale, a)));
            }
        s.Colors = colors;
        return s;
    }
}

/// <summary>
/// A running instance of a <see cref="PufferState"/>: a CPU-simulated burst of
/// billboard sprites drawn as one <see cref="MultiMeshInstance3D"/> (one draw call).
/// The CPU integration honours the reader parameters directly — per-axis random
/// velocity, exponential friction, size growth, and the exact (non-uniform) flipbook
/// timing — which map awkwardly onto Godot's built-in particle process material.
///
/// A shared shader billboards each instance quad toward the camera, picks its flipbook
/// column from per-instance custom data, and additively blends the fire (the frames are
/// alpha-masked with black edges, so additive gives a soft glow and no quad edge).
///
/// Reusable across the effect readers; today it drives the crash fireball
/// (<c>flame_ball.json</c> → <c>fierypuffer</c>).
/// </summary>
public sealed partial class Puffer : Node3D
{
    private struct Particle
    {
        public Vector3 Pos, Vel;
        public float BaseSize, Age, Life;
        public float Frame; // static-TEXTURES pool: the randomly picked atlas column
    }

    // Life-fade envelope (a render nicety, not in the reader): ease the additive glow in
    // and out so particles don't pop at spawn/death. The flipbook itself already dims.
    private const float FadeIn = 0.12f, FadeOutStart = 0.6f;

    private PufferState _state = null!;
    private MultiMeshInstance3D _mmi = null!;
    private MultiMesh _mm = null!;
    private Particle[] _particles = Array.Empty<Particle>();
    private int _liveCount;
    private readonly System.Random _rng = new();

    private bool _emitting;
    private float _sinceStart;
    private int _burstsSpawned, _burstsTotal;
    private bool _active;

    private bool _trailing;        // distance-interval trail mode (DISTANCE_INTERVAL states)
    private Vector3 _trailPrev;    // last emit-line end, world space
    private float _trailCarry;     // meters of motion carried into the next interval

    private bool _sustaining;      // continuous TIME_INTERVAL mode (AnimRuntime's PUFFER_STATE)
    private float _sustainCarry;   // seconds carried into the next emission interval

    private const int TrailPool = 640; // live-particle cap for trail emitters
    // A sustained emitter never stops, so its pool is sized to the steady-state population
    // (Number per interval, each living up to LifetimeMax) rather than to a burst duration.
    private const int SustainPoolMin = 16, SustainPoolMax = 2048;
    // Cap catch-up after a long frame (or a paused window): without it a 5 s hitch would
    // spawn 5 s worth of particles in one frame and blow the pool.
    private const int MaxSustainBatchesPerFrame = 8;

    private const string ShaderCode = """
        shader_type spatial;
        render_mode BLEND_MODE, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

        uniform sampler2D atlas : source_color, filter_linear;
        uniform float frame_count = 1.0;
        uniform sampler2D depth_texture : hint_depth_texture, filter_nearest;

        varying flat float v_frame;
        varying flat float v_alpha;
        varying flat vec4 v_color;

        void vertex() {
            // Billboard the instance quad toward the camera, keeping its per-instance scale
            // (Godot's billboard_keep_scale, done by hand because this is a MultiMesh).
            MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
                INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
            MODELVIEW_MATRIX[0] *= length(MODEL_MATRIX[0].xyz);
            MODELVIEW_MATRIX[1] *= length(MODEL_MATRIX[1].xyz);
            MODELVIEW_MATRIX[2] *= length(MODEL_MATRIX[2].xyz);
            v_frame = INSTANCE_CUSTOM.x;
            v_alpha = INSTANCE_CUSTOM.y;
            v_color = COLOR;
        }

        void fragment() {
            float col = floor(v_frame + 0.5);
            vec2 uv = vec2((UV.x + col) / frame_count, UV.y);
            vec4 t = texture(atlas, uv);
            // fade to nothing at the quad rim: some source frames leak bright pixels
            // to their border (fire_f01 edge maxes at 247), which otherwise paints
            // faint additive rectangles over dark ground on large grown quads
            vec2 rim = smoothstep(vec2(0.0), vec2(0.12), UV)
                     * smoothstep(vec2(0.0), vec2(0.12), vec2(1.0) - UV);
            // soft particles: a billboard tilted by a high chase camera dips into the
            // terrain and the depth test cuts it with a hard straight line — fade
            // alpha out over the last ~1.5 m before the scene depth instead. Disabled
            // (SOFT_EXPR → 1.0) for the crash smokeball: it sits just above the ground,
            // so the fade zeroes the alpha of every fresh puff against the terrain right
            // behind it — a bright additive fire still leaks through, but MIX black smoke
            // faded to zero is simply invisible until it grows tall (item 8).
            float scene_raw = texture(depth_texture, SCREEN_UV).r;
            vec4 unproj = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, scene_raw, 1.0);
            float scene_z = unproj.z / unproj.w;
            float soft = SOFT_EXPR;
            ALBEDO = t.rgb * v_color.rgb;
            ALPHA = t.a * v_alpha * v_color.a * rim.x * rim.y * soft;
        }
        """;

    /// <summary>Builds an emitter for <paramref name="state"/>, loading its flipbook (or
    /// static TEXTURES pool) frames from <paramref name="textures"/> into an atlas. Null
    /// if no frame texture is found. <paramref name="activeDuration"/> is how long a
    /// burst puffer emits (the calling animation's STOP time; large_fireball stops its
    /// puffer at 0.3 s) — ignored by trail (DISTANCE_INTERVAL) states, which emit while
    /// driven via <see cref="TrailAdvance"/>. States with a COLORS ramp alpha-blend
    /// (black smoke is invisible additively); flipbook fire stays additive.</summary>
    /// <param name="sustained">Continuous emission driven by <see cref="SustainAt"/> — the
    /// animation system's ACTIVE_STATE 1 puffers (the C1 train's steam plume, the waterfall
    /// mist), which run indefinitely at their TIME_INTERVAL rather than for a burst duration.</param>
    /// <param name="blend">Composite mode (see <see cref="PufferBlend"/>). Default
    /// <see cref="PufferBlend.Auto"/> keeps the COLORS-ramp rule; the crash smokeball passes
    /// <see cref="PufferBlend.Mix"/> because its dark textures carry no ramp yet must not
    /// blend additively (which would make black smoke invisible).</param>
    /// <param name="softParticles">When true (default) the alpha fades over the last ~1.5 m
    /// before the scene depth, softening the hard line where a tilted billboard dips into
    /// terrain. The crash smokeball passes false: it emits just above the ground, and the
    /// fade would zero its alpha against the terrain right behind it until it grows tall.</param>
    public static Puffer? Create(PufferState state, TextureArchive textures, float activeDuration = 0.3f,
        bool sustained = false, PufferBlend blend = PufferBlend.Auto, bool softParticles = true)
    {
        var frameNames = state.TextureSequence.Count > 0
            ? state.TextureSequence.Select(f => f.Texture).ToList()
            : state.Textures.ToList();
        var atlas = BuildAtlas(frameNames, textures);
        if (atlas == null)
            return null;
        var puffer = new Puffer();
        puffer.Init(state, atlas, frameNames.Count, activeDuration, sustained, blend, softParticles);
        return puffer;
    }

    private void Init(PufferState state, ImageTexture atlas, int frameCount, float activeDuration,
        bool sustained = false, PufferBlend blend = PufferBlend.Auto, bool softParticles = true)
    {
        _state = state;
        Name = "puffer_" + state.Name;
        if (sustained)
        {
            int steady = Mathf.CeilToInt(state.Number * state.LifetimeMax
                                         / Mathf.Max(state.TimeInterval, 1e-3f)) + state.Number;
            _particles = new Particle[Mathf.Clamp(steady, SustainPoolMin, SustainPoolMax)];
        }
        else if (state.DistanceInterval > 0f)
        {
            _particles = new Particle[TrailPool];
        }
        else
        {
            // Emit at t = 0, TimeInterval, 2·TimeInterval, … while active.
            _burstsTotal = Mathf.Max(1, Mathf.FloorToInt(activeDuration / Mathf.Max(state.TimeInterval, 1e-3f) + 1e-3f) + 1);
            _particles = new Particle[state.Number * _burstsTotal];
        }

        // Auto keeps the COLORS-ramp rule (ramp ⇒ mix, else add); the explicit modes override
        // it — the crash smokeball is MIX despite carrying no ramp, because its textures are
        // near-black with a smoke-shaped alpha, which adds ~0 (invisible) but masks correctly.
        string blendMode = blend switch
        {
            PufferBlend.Additive => "blend_add",
            PufferBlend.Mix => "blend_mix",
            _ => state.Colors.Count > 0 ? "blend_mix" : "blend_add",
        };
        var code = ShaderCode
            .Replace("BLEND_MODE", blendMode)
            .Replace("SOFT_EXPR", softParticles ? "clamp((VERTEX.z - scene_z) / 1.5, 0.0, 1.0)" : "1.0");
        var mat = new ShaderMaterial { Shader = new Shader { Code = code } };
        mat.SetShaderParameter("atlas", atlas);
        mat.SetShaderParameter("frame_count", (float)frameCount);

        _mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            UseColors = true,
            Mesh = new QuadMesh { Size = Vector2.One },
            InstanceCount = _particles.Length,
            VisibleInstanceCount = 0,
        };
        _mmi = new MultiMeshInstance3D
        {
            Multimesh = _mm,
            MaterialOverride = mat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // billboarding moves verts off the MultiMesh's computed AABB — pad culling
            ExtraCullMargin = Mathf.Max(4f, state.SizeMax * state.GrowthFactor),
        };
        AddChild(_mmi);
        Visible = false;
    }

    /// <summary>Fire one burst at a fixed world position (decoupled from any moving parent).</summary>
    public void Burst(Vector3 worldPosition)
    {
        TopLevel = true; // ignore parent transform: the fireball stays put in world space
        GlobalPosition = worldPosition;
        _liveCount = 0;
        _sinceStart = 0f;
        _burstsSpawned = 0;
        _emitting = true;
        _active = true;
        Visible = true;
        SpawnBatch(); // t = 0 immediately, so a screenshot on the impact frame already shows fire
    }

    /// <summary>Kills the effect and clears every live particle (called at/*before* respawn).</summary>
    public void Clear()
    {
        _emitting = false;
        _trailing = false;
        _sustaining = false;
        _active = false;
        _liveCount = 0;
        if (_mm != null)
            _mm.VisibleInstanceCount = 0;
        Visible = false;
    }

    /// <summary>Advances a DISTANCE_INTERVAL trail emitter to the followed node's new
    /// world position, emitting one sprite per interval of motion (with carry across
    /// frames) — the dense_firetrail smoke/fire trailing a damaged plane. The first
    /// call starts the trail; call every frame while the effect is on.</summary>
    public void TrailAdvance(Vector3 worldPos)
    {
        if (_state.DistanceInterval <= 0f)
            return;
        if (!_trailing)
        {
            TopLevel = true;             // particles live in world space, left behind the plane
            GlobalPosition = Vector3.Zero;
            _trailing = true;
            _trailPrev = worldPos;
            _trailCarry = 0f;
            _active = true;
            Visible = true;
            return;
        }
        var delta = worldPos - _trailPrev;
        float dist = delta.Length();
        _trailPrev = worldPos;
        if (dist < 1e-5f)
            return;
        var dir = delta / dist;
        float interval = _state.DistanceInterval;
        _trailCarry += dist;
        // walk back from the current position so the newest puff sits at the plane
        var start = worldPos - dir * (_trailCarry - interval);
        int count = (int)(_trailCarry / interval);
        for (int k = 0; k < count; k++)
            SpawnTrailPuff(start + dir * (k * interval));
        _trailCarry -= count * interval;
    }

    /// <summary>Stops trail emission; live smoke decays naturally.</summary>
    public void TrailEnd() => _trailing = false;

    /// <summary>Static-viewer variant of <see cref="TrailAdvance"/>: emits the trail's
    /// per-meter puffs AT a fixed world point, spending <paramref name="speedMps"/>
    /// meters of virtual motion per second — the damage lab's parked plane, whose
    /// panels burn in place (the puffs' own random velocity and growth make the
    /// stacked emissions read as a flickering fire). Same carry, pool and spawn
    /// path as the moving trail.</summary>
    public void TrailBurnAt(Vector3 worldPos, float dt, float speedMps)
    {
        if (_state.DistanceInterval <= 0f)
            return;
        if (!_trailing)
        {
            TopLevel = true;
            GlobalPosition = Vector3.Zero;
            _trailing = true;
            _trailPrev = worldPos;
            _trailCarry = 0f;
            _active = true;
            Visible = true;
        }
        _trailCarry += speedMps * dt;
        int count = (int)(_trailCarry / _state.DistanceInterval);
        for (int k = 0; k < count; k++)
            SpawnTrailPuff(worldPos);
        _trailCarry -= count * _state.DistanceInterval;
    }

    /// <summary>
    /// Continuous emission at a moving world point — the third emission mode, alongside the
    /// one-shot <see cref="Burst"/> and the distance-driven <see cref="TrailAdvance"/>. This
    /// is what an animation's <c>PUFFER_STATE … ACTIVE_STATE 1</c> asks for: emit
    /// <c>NUMBER</c> sprites every <c>TIME_INTERVAL</c>, indefinitely, wherever the emitter
    /// node currently is (the C1 train's smokestack moves along the whole track loop).
    /// Particles live in world space, so they are left behind rather than dragged along.
    /// Call every frame while the puffer is on; <see cref="SustainEnd"/> stops emission and
    /// lets the live particles decay.
    /// </summary>
    public void SustainAt(Vector3 worldPos, Basis worldBasis, float dt)
    {
        if (!_sustaining)
        {
            TopLevel = true;                 // world-space particles, like the trail mode
            GlobalPosition = Vector3.Zero;
            _sustaining = true;
            _active = true;
            Visible = true;
            _sustainCarry = _state.TimeInterval; // emit on the very first frame
        }
        _sustainCarry += dt;
        float interval = Mathf.Max(_state.TimeInterval, 1e-3f);
        int batches = Mathf.Min((int)(_sustainCarry / interval), MaxSustainBatchesPerFrame);
        for (int b = 0; b < batches; b++)
            SpawnSustained(worldPos, worldBasis);
        _sustainCarry -= batches * interval;
    }

    /// <summary>Stops sustained emission; live particles finish their lifetimes.</summary>
    public void SustainEnd() => _sustaining = false;

    private void SpawnSustained(Vector3 worldPos, Basis worldBasis)
    {
        var min = _state.MinRandomVelocity;
        var max = _state.MaxRandomVelocity;
        // LOCAL_VELOCITY is in the emitter node's frame (the smokestack's "up"); WORLD_VELOCITY
        // is not. Rotating the local part is what keeps a banking/turning emitter correct.
        var baseVel = worldBasis * _state.LocalVelocity + _state.WorldVelocity;
        // AT_NODE's offset is likewise in the host's own frame — this is what spreads C1's
        // three waterfall splash puffers (±11 m sideways) instead of stacking them on the
        // shared anchor node's exact origin.
        var origin = worldPos + worldBasis * _state.AtNodeOffset;
        float d = _state.DeviationDistance;
        for (int k = 0; k < _state.Number && _liveCount < _particles.Length; k++)
        {
            _particles[_liveCount++] = new Particle
            {
                Pos = origin + new Vector3(Rand(-d, d), Rand(-d, d), Rand(-d, d)),
                Vel = baseVel + new Vector3(Rand(min.X, max.X), Rand(min.Y, max.Y), Rand(min.Z, max.Z)),
                BaseSize = Rand(_state.SizeMin, _state.SizeMax),
                Life = Rand(_state.LifetimeMin, _state.LifetimeMax),
                Age = 0f,
                Frame = _state.TextureSequence.Count > 0 ? 0f
                    : Mathf.Min(_state.Textures.Count - 1,
                        Mathf.FloorToInt((float)_rng.NextDouble() * _state.Textures.Count)),
            };
        }
    }

    private void SpawnTrailPuff(Vector3 worldPos)
    {
        if (_liveCount >= _particles.Length)
            return; // pool exhausted — oldest puffs finish before new ones spawn
        var min = _state.MinRandomVelocity;
        var max = _state.MaxRandomVelocity;
        float d = _state.DeviationDistance;
        _particles[_liveCount++] = new Particle
        {
            Pos = worldPos + new Vector3(Rand(-d, d), Rand(-d, d), Rand(-d, d)),
            Vel = _state.WorldVelocity + new Vector3(Rand(min.X, max.X), Rand(min.Y, max.Y), Rand(min.Z, max.Z)),
            BaseSize = Rand(_state.SizeMin, _state.SizeMax),
            Life = Rand(_state.LifetimeMin, _state.LifetimeMax),
            Age = 0f,
            Frame = _state.TextureSequence.Count > 0 ? 0f
                : Mathf.Min(_state.Textures.Count - 1, Mathf.FloorToInt((float)_rng.NextDouble() * _state.Textures.Count)),
        };
    }

    public override void _Process(double delta)
    {
        if (!_active)
            return;
        float dt = (float)delta;
        _sinceStart += dt;

        if (_emitting)
        {
            while (_burstsSpawned < _burstsTotal && _sinceStart >= _burstsSpawned * _state.TimeInterval)
                SpawnBatch();
            if (_burstsSpawned >= _burstsTotal)
                _emitting = false;
        }

        // integrate live particles, swap-removing the dead so survivors stay packed at the front
        bool flipbook = _state.TextureSequence.Count > 0;
        bool hasRamp = _state.Colors.Count > 0;
        float damp = Mathf.Exp(-_state.Friction * dt);
        for (int i = 0; i < _liveCount; i++)
        {
            ref var p = ref _particles[i];
            p.Age += dt;
            if (p.Age >= p.Life)
            {
                _particles[i] = _particles[--_liveCount];
                i--;
                continue;
            }
            p.Vel = p.Vel * damp + _state.WorldAcceleration * dt;
            p.Pos += p.Vel * dt;

            float lifeFrac = p.Age / p.Life;
            float size = p.BaseSize * Mathf.Lerp(1f, _state.GrowthFactor, lifeFrac);
            _mm.SetInstanceTransform(i, new Transform3D(Basis.Identity.Scaled(new Vector3(size, size, size)), p.Pos));
            // The COLORS ramp owns the fade when present (its alpha ends at 0);
            // otherwise the render-nicety envelope eases the additive glow in/out.
            _mm.SetInstanceCustomData(i, new Color(
                flipbook ? FrameFor(p.Age) : p.Frame,
                hasRamp ? 1f : FadeFor(lifeFrac), 0f, 0f));
            _mm.SetInstanceColor(i, hasRamp ? RampColor(lifeFrac) : Colors.White);
        }
        _mm.VisibleInstanceCount = _liveCount;

        if (_liveCount == 0 && !_emitting && !_trailing && !_sustaining)
        {
            _active = false;
            Visible = false;
        }
    }

    /// <summary>Interpolates the COLORS (lifeFrac, color) ramp.</summary>
    private Color RampColor(float lifeFrac)
    {
        var ramp = _state.Colors;
        if (lifeFrac <= ramp[0].Frac)
            return ramp[0].Color;
        for (int i = 1; i < ramp.Count; i++)
            if (lifeFrac <= ramp[i].Frac)
            {
                float span = ramp[i].Frac - ramp[i - 1].Frac;
                float t = span > 1e-6f ? (lifeFrac - ramp[i - 1].Frac) / span : 1f;
                return ramp[i - 1].Color.Lerp(ramp[i].Color, t);
            }
        return ramp[^1].Color;
    }

    private void SpawnBatch()
    {
        var min = _state.MinRandomVelocity;
        var max = _state.MaxRandomVelocity;
        var baseVel = _state.LocalVelocity + _state.WorldVelocity;
        float d = _state.DeviationDistance;
        for (int k = 0; k < _state.Number && _liveCount < _particles.Length; k++)
        {
            _particles[_liveCount++] = new Particle
            {
                Pos = new Vector3(Rand(-d, d), Rand(-d, d), Rand(-d, d)),
                Vel = baseVel + new Vector3(Rand(min.X, max.X), Rand(min.Y, max.Y), Rand(min.Z, max.Z)),
                BaseSize = Rand(_state.SizeMin, _state.SizeMax),
                Life = Rand(_state.LifetimeMin, _state.LifetimeMax),
                Age = 0f,
            };
        }
        _burstsSpawned++;
    }

    // Latest flipbook frame whose keyed time has been reached (sequence times ascending).
    private float FrameFor(float age)
    {
        var seq = _state.TextureSequence;
        int frame = 0;
        for (int i = 0; i < seq.Count && seq[i].Time <= age; i++)
            frame = i;
        return frame;
    }

    private static float FadeFor(float lifeFrac) =>
        lifeFrac < FadeIn ? lifeFrac / FadeIn
        : lifeFrac > FadeOutStart ? Mathf.Max(0f, 1f - (lifeFrac - FadeOutStart) / (1f - FadeOutStart))
        : 1f;

    private float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

    private static ImageTexture? BuildAtlas(IReadOnlyList<string> names, TextureArchive textures)
    {
        if (names.Count == 0)
            return null;
        var frames = new Image[names.Count];
        int fw = 0, fh = 0;
        for (int i = 0; i < names.Count; i++)
        {
            var tex = textures.Find(names[i]);
            if (tex == null)
                return null;
            var img = tex.GetImage();
            img.Convert(Image.Format.Rgba8);
            frames[i] = img;
            fw = Mathf.Max(fw, img.GetWidth());
            fh = Mathf.Max(fh, img.GetHeight());
        }
        var atlas = Image.CreateEmpty(fw * names.Count, fh, false, Image.Format.Rgba8);
        for (int i = 0; i < frames.Length; i++)
        {
            var f = frames[i];
            if (f.GetWidth() != fw || f.GetHeight() != fh)
                f.Resize(fw, fh);
            atlas.BlitRect(f, new Rect2I(0, 0, fw, fh), new Vector2I(i * fw, 0));
        }
        return ImageTexture.CreateFromImage(atlas);
    }
}
