using System;
using System.Collections.Generic;
using CrimsonSkies.Mech3;
using Godot;

namespace CrimsonSkies.Effects;

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

    /// <summary>Flipbook: texture name to show once the particle's age reaches Time (ascending).</summary>
    public IReadOnlyList<(float Time, string Texture)> TextureSequence = Array.Empty<(float, string)>();

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
    /// The reader also holds "stop" stubs that share the name but only flip ACTIVE_STATE
    /// (no NUMBER); those are skipped.</summary>
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
                    if (d.Has("NUMBER")
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
        };

        var seq = new List<(float, string)>();
        foreach (var item in d.List("TEXTURE_SEQUENCE") ?? new List<object?>())
            if (item is List<object?> pair && pair.Count >= 2
                && pair[0] is float t && pair[1] is string tex)
                seq.Add((t, tex));
        s.TextureSequence = seq;
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

    private const string ShaderCode = """
        shader_type spatial;
        render_mode blend_add, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

        uniform sampler2D atlas : source_color, filter_linear;
        uniform float frame_count = 1.0;

        varying flat float v_frame;
        varying flat float v_alpha;

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
        }

        void fragment() {
            float col = floor(v_frame + 0.5);
            vec2 uv = vec2((UV.x + col) / frame_count, UV.y);
            vec4 t = texture(atlas, uv);
            ALBEDO = t.rgb;
            ALPHA = t.a * v_alpha;
        }
        """;

    /// <summary>Builds an emitter for <paramref name="state"/>, loading its flipbook frames
    /// from <paramref name="textures"/> into an atlas. Null if no frame texture is found.
    /// <paramref name="activeDuration"/> is how long the puffer emits (the calling animation's
    /// STOP time; large_fireball stops its puffer at 0.3 s).</summary>
    public static Puffer? Create(PufferState state, TextureArchive textures, float activeDuration = 0.3f)
    {
        var atlas = BuildAtlas(state.TextureSequence, textures);
        if (atlas == null)
            return null;
        var puffer = new Puffer();
        puffer.Init(state, atlas, activeDuration);
        return puffer;
    }

    private void Init(PufferState state, ImageTexture atlas, float activeDuration)
    {
        _state = state;
        Name = "puffer_" + state.Name;
        // Emit at t = 0, TimeInterval, 2·TimeInterval, … while active.
        _burstsTotal = Mathf.Max(1, Mathf.FloorToInt(activeDuration / Mathf.Max(state.TimeInterval, 1e-3f) + 1e-3f) + 1);
        _particles = new Particle[state.Number * _burstsTotal];

        var mat = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
        mat.SetShaderParameter("atlas", atlas);
        mat.SetShaderParameter("frame_count", (float)state.TextureSequence.Count);

        _mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
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
        _active = false;
        _liveCount = 0;
        if (_mm != null)
            _mm.VisibleInstanceCount = 0;
        Visible = false;
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
            _mm.SetInstanceCustomData(i, new Color(FrameFor(p.Age), FadeFor(lifeFrac), 0f, 0f));
        }
        _mm.VisibleInstanceCount = _liveCount;

        if (_liveCount == 0 && !_emitting)
        {
            _active = false;
            Visible = false;
        }
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

    private static ImageTexture? BuildAtlas(IReadOnlyList<(float Time, string Texture)> seq, TextureArchive textures)
    {
        if (seq.Count == 0)
            return null;
        var frames = new Image[seq.Count];
        int fw = 0, fh = 0;
        for (int i = 0; i < seq.Count; i++)
        {
            var tex = textures.Find(seq[i].Texture);
            if (tex == null)
                return null;
            var img = tex.GetImage();
            img.Convert(Image.Format.Rgba8);
            frames[i] = img;
            fw = Mathf.Max(fw, img.GetWidth());
            fh = Mathf.Max(fh, img.GetHeight());
        }
        var atlas = Image.CreateEmpty(fw * seq.Count, fh, false, Image.Format.Rgba8);
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
