using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>How a puffer's sprites composite. The authored data never says, so <see cref="Auto"/>
/// derives it: a COLORS ramp or a near-black dying sprite ⇒ <c>blend_mix</c>, else
/// <c>blend_add</c>. The explicit modes force the verdict.</summary>
public enum PufferBlend { Auto, Additive, Mix }

/// <summary>The three camera-distance switches plus the original's own far-band multiplier, forced
/// instead of read from <c>config.json</c>. Only <see cref="Puffer.CreateWith"/> — the test entry
/// point — accepts one; the real path always reads Config, whose keys default to exactly the
/// original's behaviour (<c>true</c>, <c>true</c>, <c>true</c>,
/// <see cref="Puffer.GlobalFadeFactorDefault"/>).</summary>
public readonly record struct PufferFadeSwitches(
    bool DistanceFade, bool FarCull, bool NearCull, float GlobalFadeFactor = 1f);

/// <summary>
/// The parameters of one <c>PUFFER_STATE</c> block from a zrdr effects reader
/// (flame_ball.json, fire.json, pufftrails.json, … all share this schema). A puffer
/// is the original engine's billboard-particle emitter: it spits <see cref="Number"/>
/// sprites per <see cref="TimeInterval"/>, each with a random velocity, size, and
/// lifetime, cycling a flipbook of textures over its age.
///
/// Only the fields we currently render are parsed; the reader carries more — added here as
/// they're needed. Distances/velocities are meters and seconds, matching the world;
/// TEXTURE_SEQUENCE times are the exception, being fractions of a particle's lifetime.
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

    /// <summary>Random particle birth age in seconds (<c>Rand(StartAgeMin, StartAgeMax)</c>), both
    /// default 0. ⚠ The engine's real birth age adds a sub-frame term computed in
    /// <see cref="SustainAt"/> and can skip creating a particle outright — see
    /// <c>docs/org/puffer.md</c>.</summary>
    public float StartAgeMin, StartAgeMax;

    /// <summary>How strongly this puffer's particles are carried by the world's wind
    /// (<see cref="WorldWind"/>). ⚠ Default is 1, not 0 — an unauthored puffer is FULLY
    /// wind-carried. Inert unless <see cref="Friction"/> is non-zero. See
    /// <c>docs/org/puffer.md</c>.</summary>
    public float WindFactor = 1f;

    /// <summary>Camera-distance bands, in metres of view-space depth — see
    /// <see cref="Puffer.DistanceAlpha"/> for their meaning.
    /// ⚠ Do not infer field order from the authored values; most near pairs are descending. See
    /// <c>docs/formats/effects.md</c> and <c>docs/org/puffer.md</c>.</summary>
    public float NearFadeStart, NearFadeEnd;

    /// <inheritdoc cref="NearFadeStart"/>
    public float FarFadeStart = float.MaxValue, FarFadeEnd = float.MaxValue;

    /// <summary>A per-puffer sprite-size nudge, almost certainly a depth-priority constant reused
    /// for this — the size use is the only one known. The original's draw scales the screen radius
    /// by <c>1 + K·PRIORITY</c>, folded into <see cref="Particle.BaseSize"/> at spawn instead
    /// (<see cref="Puffer.PriorityScaleDefault"/>). Default 0, so an unauthored puffer's factor is
    /// exactly 1 — 47 puffers in the install author a non-zero value, 192 compiled events.</summary>
    public float Priority;

    /// <summary>AT_NODE's optional trailing offset (AT_NODE is [nodeName, dx?, dy?, dz?]),
    /// in the host node's own frame — the same convention as <see cref="LocalVelocity"/>.
    /// This is what spreads a multi-emitter effect around its anchor instead of stacking
    /// every emitter on the anchor's exact origin: C1's waterfall attaches three splash
    /// puffers to the single node <c>waterfall01</c>, offset ±11 m sideways and 8 m up so
    /// the spray reads as the base of the falls rather than one point source.</summary>
    public Vector3 AtNodeOffset;

    /// <summary>Trail emission: emit one sprite per this many meters
    /// of the followed node's motion (the smoke/fire trail puffers in
    /// pufftrails.json's dense_firetrail). 0 = burst-style (NUMBER per TIME_INTERVAL).</summary>
    public float DistanceInterval;

    /// <summary>Flipbook: texture name to show once the particle has lived Time of its lifetime
    /// (ascending, 0–1 — a FRACTION of LIFETIME_RANGE, not seconds; see
    /// <see cref="Puffer.FrameFor"/>).</summary>
    public IReadOnlyList<(float Time, string Texture)> TextureSequence = Array.Empty<(float, string)>();

    /// <summary>Static texture pool (the TEXTURES key — smoke101/102/103): each
    /// particle picks one at random and keeps it. Alternative to TextureSequence.</summary>
    public IReadOnlyList<string> Textures = Array.Empty<string>();

    /// <summary>Color-over-age ramp (the COLORS key): (lifeFraction, color) entries,
    /// ascending; rgb are 0–255 in the reader (normalized here), alpha 0–1. The
    /// dense_firetrail smoke is born orange (255,164,90) and turns near-black.</summary>
    public IReadOnlyList<(float Frac, Color Color)> Colors = Array.Empty<(float, Color)>();

    /// <summary>Whether this state authors <c>START_AGE_RANGE</c> at all — gates the extra
    /// <c>Rand()</c> draw at spawn so the ~2,900 puffers that don't author it consume no extra
    /// draw and stay bit-identical.</summary>
    public bool HasStartAgeRange => StartAgeMin != 0f || StartAgeMax != 0f;

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

    /// <summary>Builds a state from a compiled animation's PUFFER_STATE event payload, as opposed
    /// to <see cref="Parse"/>'s reader form. ⚠ The emission interval, whether it's Time or
    /// Distance, and the growth-factor ramp all need special handling — see
    /// <c>docs/formats/anim-definitions.md</c> for PUFFER_STATE before changing this.</summary>
    public static PufferState FromAnimEvent(AnimData d)
    {
        Vector3 Vec(string key) => d.Vec3(key);
        float Range(string key, string end, float fallback) =>
            d.Obj(key)?.Num(end) ?? fallback;

        // The one interval number is seconds when Time, metres when Distance.
        var ig = d.Obj("interval_garbage");
        bool byDistance = string.Equals(ig?.Str("interval_type"), "Distance",
            StringComparison.OrdinalIgnoreCase);
        float intervalValue = d.Obj("interval")?.Num("value") ?? ig?.Num("interval_value") ?? 0.1f;

        var s = new PufferState
        {
            Name = d.Str("name") ?? "",
            TimeInterval = byDistance ? 0.1f : intervalValue,
            DistanceInterval = byDistance ? intervalValue : 0f,
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
            StartAgeMin = Range("start_age_range", "min", 0f),
            StartAgeMax = Range("start_age_range", "max", 0f),
            // Absent (null) falls back to the ctor's 1.0, never to 0 — see WindFactor's remark.
            WindFactor = d.Num("wind_factor") ?? 1f,
            // `unk_range` IS NEAR_FADE — see docs/formats/effects.md. Absent stays at the ctor's
            // no-fade, no-cull defaults.
            NearFadeStart = Range("unk_range", "min", 0f),
            NearFadeEnd = Range("unk_range", "max", 0f),
            FarFadeStart = Range("fade_range", "min", float.MaxValue),
            FarFadeEnd = Range("fade_range", "max", float.MaxValue),
            Priority = d.Num("priority") ?? 0f,
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

        // FADE_RANGE and FAR_FADE are two spellings of one block (the parser accepts both onto
        // the same flag bit). The install authors FADE_RANGE 574 times and
        // FAR_FADE exactly once — C3's volcanosmoke — so the alias is not hypothetical.
        string farKey = d.Has("FADE_RANGE") ? "FADE_RANGE" : "FAR_FADE";

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
            StartAgeMin = d.Float("START_AGE_RANGE", 0f, 0),
            StartAgeMax = d.Float("START_AGE_RANGE", 0f, 1),
            // Absent falls back to the ctor's 1.0, never to 0 — see WindFactor's remark. Only 5
            // reader blocks in the install author the key at all (3 at 0.3, 2 at 1.0).
            WindFactor = d.Float("WIND_FACTOR", 1f),
            // The two bands — see NearFadeStart for which end is which, and DO NOT read it off
            // these values: five of the six authored near pairs are descending.
            NearFadeStart = d.Float("NEAR_FADE", 0f, 0),
            NearFadeEnd = d.Float("NEAR_FADE", 0f, 1),
            FarFadeStart = d.Float(farKey, float.MaxValue, 0),
            FarFadeEnd = d.Float(farKey, float.MaxValue, 1),
            Priority = d.Float("PRIORITY"),
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

/// <summary>A running instance of a <see cref="PufferState"/>: a CPU-simulated burst of billboard
/// sprites, handed one frame at a time to an <see cref="IEmitterRenderer"/>. The CPU integration
/// honours the reader parameters directly — random velocity, friction, size growth, and the
/// non-uniform flipbook timing — which map awkwardly onto Godot's built-in particle material.
/// Everything past that integration is behind the renderer seam, so <see cref="CreateWith"/> can
/// reach all three emission modes with no GPU and no <c>TextureArchive</c>. See
/// <c>docs/architecture.md</c> for plumbing and <c>docs/org/puffer.md</c> for the decode.
/// </summary>
public sealed partial class Puffer : Node3D
{
    /// <summary>Default for config.json <c>puffer.*SizeScale</c> (per-spawn-path size multipliers).
    /// Not a tune: <c>SIZE_RANGE</c> is a screen-space half-extent, so the sprite spans
    /// <c>2 × SIZE_RANGE</c> — this is the decoded radius→diameter conversion. See
    /// <c>docs/org/puffer.md</c>.</summary>
    public const float SizeScaleDefault = 2f;

    // ⚠ Do not add a rise/lifetime multiplier for the fire family (`puffer.fireRiseScale`,
    // `puffer.fireLifetimeScale`). A prior tune of this kind measured 2.11x too tall against
    // footage. The authored numbers reach on their own — see docs/org/puffer.md, "The fire pair".

    /// <summary>Default for config.json <c>puffer.globalFadeFactor</c> (<c>PufferSetGlobalFadeFactor</c>
    /// in the original). Multiplies only the FAR band's measured distance; near comparisons stay
    /// unscaled. No authored writer in this install. See <c>docs/org/puffer.md</c>.</summary>
    public const float GlobalFadeFactorDefault = 1f;

    /// <summary>The <c>K</c> in the original's <c>1 + K·PRIORITY</c> screen-radius scale. The
    /// engine constant is <c>0.01</c> on the software path and <c>0.02</c> on the hardware one —
    /// this project has no software path (see <see cref="DistanceAlpha"/>'s remark on the same
    /// split), so this is the hardware value. Default <c>PRIORITY</c> is 0, so the factor is 1
    /// unless authored.</summary>
    public const float PriorityScaleDefault = 0.02f;

    // Alpha-weighted mean luminance (0–1) below which the sprite a particle dies on
    // counts as smoke, so the emitter alpha-blends instead of adding. The measured population
    // separates cleanly either side of it: fire_f06 0.018 and thickblksmoke 0.004 below,
    // nothing above it under 0.12 (fire101 0.12, exp_yel01 0.17, smoke101 0.22, fire_f01 0.34)
    // — so white smoke stays additive and only genuinely black sprites flip.
    private const float SmokeLuminance = 16f / 255f;

    // Life-fade envelope (a render nicety, not in the reader): ease the additive glow in
    // and out so particles don't pop at spawn/death. The flipbook itself already dims.
    private const float FadeIn = 0.12f, FadeOutStart = 0.6f;

    private const int TrailPool = 640; // starting pool for trail emitters, grown on demand
    // A sustained emitter never stops, so its pool is sized to the steady-state population
    // (Number per interval, each living up to LifetimeMax) rather than to a burst duration.
    private const int SustainPoolMin = 16, SustainPoolMax = 2048;
    // Where a continuous emitter's pool stops doubling (GrowPool). INVENTED, like every pool size
    // here: the engine has no cap. Sized above the smoke screen's steady state with headroom.
    private const int ContinuousPoolMax = 8192;
    // The original's teleport guard on the DISTANCE path: it
    // accumulates the frame's motion length into the emitter's interval counter only
    // `if (len &lt; 200.0)` — a respawned or pooled emitter that jumps across the world
    // lays no line of puffs along the jump. The time path has no equivalent test: there the
    // engine accumulates `dt` unconditionally.
    private const float TeleportGuardMeters = 200f;

    // Baked atlases per archive, keyed by the frame list: a state many emitters share (a crash
    // rig builds 28 lgpuffers ahead) bakes and measures its frames once. Keyed by the archive so
    // a chapter change can never serve another chapter's frames.
    private static readonly ConditionalWeakTable<TextureArchive, Dictionary<string, (ImageTexture Atlas, bool DiesDark)>> AtlasCache = new();

    // Particle spread/size/life/frame jitter. One stream per emitter, drawn off the master seed's
    // puffer stream, so a run repeats and two emitters still scatter independently.
    private readonly System.Random _rng = Rng.NewSystemRandom(Rng.Puffer);

    // Dev-tunable BaseSize multipliers, one per spawn path (burst = SpawnBatch, trail and sustain
    // = SpawnSustained by way of TrailAdvance / SustainAt). Read once per emitter at Init; --det drops
    // config overrides, so scripted captures stay a function of the committed tree.
    private float _burstSizeScale = SizeScaleDefault;
    private float _trailSizeScale = SizeScaleDefault;
    private float _sustainSizeScale = SizeScaleDefault;

    // 1 + K·PRIORITY, read once at Init off the authored state — not a config knob, since it
    // is a decoded engine constant rather than a tuning surface.
    private float _priorityFactor = 1f;

    // The three distance switches (config.json puffer.distanceFade / farCull / nearCull), read
    // once at Init. All default TRUE — every one of them reproduces the original, and a flag that
    // shipped off would be a silent divergence wearing a config key. See DistanceAlpha.
    private bool _distanceFade = true;
    private bool _farCull = true;
    private bool _nearCull = true;
    private float _globalFadeFactor = GlobalFadeFactorDefault;

    // 1/(end - start) per band, precomputed at Init exactly as the original's setters do at
    // set time — and, as they do, left as the raw difference (0) when the two ends are equal,
    // which is the unauthored far band's FLT_MAX/FLT_MAX case.
    private float _nearRecip;
    private float _farRecip;

    private PufferState _state = null!;
    private IEmitterRenderer _renderer = null!;
    // The world state this emitter reads but does not own (the wind and the camera position).
    // Still air unless a caller wired the session's own — see EffectAmbience.
    private EffectAmbience _ambience = EffectAmbience.Still;
    private Particle[] _particles = Array.Empty<Particle>();
    private int _liveCount;

    private bool _emitting;
    private float _sinceStart;
    private int _burstsSpawned, _burstsTotal;
    private bool _active;

    private bool _trailing;        // distance-interval trail mode (DISTANCE_INTERVAL states)
    private Vector3 _trailPrev;    // last emit-line end, world space
    private float _trailCarry;     // meters of motion carried into the next interval

    private bool _sustaining;      // continuous TIME_INTERVAL mode (AnimRuntime's PUFFER_STATE)
    private float _sustainCarry;   // seconds carried into the next emission interval
    private Vector3 _sustainPrevOrigin; // last frame's emitter origin, for sub-frame interpolation

    // A teleport is supposed to be rare, so the first occurrence per emitter
    // logs with its numbers and the rest are counted silently. The suppression is announced in
    // the line itself, so a reader never mistakes one line for one event.
    private int _teleportGuardCount;

    /// <summary>Diagnostics: how many frames this emitter's motion tripped the original's
    /// <see cref="TeleportGuardMeters"/> distance guard. Readable so a suite can assert the guard
    /// fired without parsing a log.</summary>
    public int TeleportGuardCount => _teleportGuardCount;

    /// <summary>Live particle count — diagnostics only (the <c>--debug-anim</c> puffer census, which
    /// is how a headless run confirms a crash's emitters are actually spawning).</summary>
    public int LiveCount => _liveCount;

    /// <summary>Builds an emitter for <paramref name="state"/>, loading its texture frames into an
    /// atlas; null if a frame is missing. <paramref name="activeDuration"/> is the burst duration
    /// (ignored by DISTANCE_INTERVAL states). <paramref name="sustained"/> selects continuous
    /// emission over a burst. <paramref name="blend"/> overrides the auto verdict (see
    /// <see cref="PufferBlend"/>); <paramref name="softParticles"/> null pairs the depth fade with
    /// it. <paramref name="ambience"/> null is still air.</summary>
    public static Puffer? Create(PufferState state, TextureArchive textures, float activeDuration = 0.3f,
        bool sustained = false, PufferBlend blend = PufferBlend.Auto, bool? softParticles = null,
        EffectAmbience? ambience = null)
    {
        bool sequenced = state.TextureSequence.Count > 0;
        var frameNames = sequenced
            ? state.TextureSequence.Select(f => f.Texture).ToList()
            : state.Textures.ToList();
        var (atlas, diesDark) = BuildAtlas(frameNames, textures, sequenced);
        if (atlas == null)
            return null;
        // Auto: a COLORS ramp still forces MIX (its own alpha ends at 0), and so does a sprite
        // set whose dying frame is near-black.
        var resolved = blend != PufferBlend.Auto ? blend
            : state.Colors.Count > 0 || diesDark ? PufferBlend.Mix
            : PufferBlend.Additive;
        var puffer = new Puffer();
        puffer.Init(state, new MultiMeshEmitterRenderer(atlas, frameNames.Count,
            resolved == PufferBlend.Mix, softParticles ?? resolved != PufferBlend.Mix),
            activeDuration, sustained, ambience);
        return puffer;
    }

    /// <summary>Builds an emitter over a supplied <paramref name="renderer"/> — the same modes and
    /// pool sizing as <see cref="Create"/>, with no atlas and no <c>TextureArchive</c>, so the
    /// three modes are assertable by a test. ⚠ Constructing a <c>Puffer</c> draws one RNG seed off
    /// <see cref="Rng.Puffer"/>; calling this on a capture path moves every puffer-bearing golden.
    /// <paramref name="fade"/> forces the three distance switches for a test; null (the real path)
    /// reads them from Config.</summary>
    public static Puffer CreateWith(PufferState state, IEmitterRenderer renderer,
        float activeDuration = 0.3f, bool sustained = false, EffectAmbience? ambience = null,
        PufferFadeSwitches? fade = null)
    {
        var puffer = new Puffer();
        puffer.Init(state, renderer, activeDuration, sustained, ambience, fade);
        return puffer;
    }

    /// <summary>Loads a named PUFFER_STATE from a zrdr effects reader and builds its
    /// emitter under <paramref name="parent"/>; null (logged by <see cref="PufferState.Load"/>) when
    /// the reader or its textures are missing. Shared by the flight assembly and the
    /// static damage lab.</summary>
    public static Puffer? MakePuffer(string zrdrPath, TextureArchive textures, Node parent,
        string file, string name, float duration = 0.3f, EffectAmbience? ambience = null)
    {
        var state = PufferState.Load(zrdrPath, file, name);
        var puffer = state != null ? Create(state, textures, duration, ambience: ambience) : null;
        if (puffer != null)
            parent.AddChild(puffer);
        return puffer;
    }

    /// <summary>Fire one burst at a fixed world position (decoupled from any moving parent).</summary>
    public void Burst(Vector3 worldPosition)
    {
        TopLevel = true; // ignore parent transform: the fireball stays put in world space
        // Toggling TopLevel PRESERVES the node's global transform (Godot 4), so the parent's
        // rotation at this moment would silently stick as this node's basis and skew every
        // local-space particle — set the whole transform, never just the position.
        GlobalTransform = new Transform3D(Basis.Identity, worldPosition);
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
        _renderer?.Show(0);
        Visible = false;
    }

    /// <summary>The one continuous drive: feed the host's world pose + dt every frame and the
    /// authored state picks the mode (see <c>docs/architecture.md</c>). A still host keeps the
    /// synthetic time cadence; a host that cannot move at all passes
    /// <paramref name="staticBurnMps"/>, spending virtual metres per second at the held pose.
    /// <see cref="Stop"/> ends the run; the next call re-homes rather than trailing from the old
    /// site.</summary>
    public void Emit(Vector3 worldPos, Basis worldBasis, float dt, float staticBurnMps = 0f)
    {
        if (_state.DistanceInterval <= 0f)
        {
            SustainAt(worldPos, worldBasis, dt);
            return;
        }
        // DISTANCE_INTERVAL follows the authored AT_NODE point, not the host origin. The still-host
        // fallback already applies this in SustainAt; applying it here keeps moving emissions at
        // that same point (speed_cue is player + local (0,0,-60), 60 m ahead of the aircraft).
        var trailPos = worldPos + worldBasis * _state.AtNodeOffset;
        bool moved = _trailing && (trailPos - _trailPrev).LengthSquared() > 1e-8f;
        if (!moved && staticBurnMps > 0f)
        {
            TrailBurnAt(trailPos, worldBasis, dt, staticBurnMps);
            return;
        }
        TrailAdvance(trailPos, worldBasis, dt);
        // An unmoved DISTANCE_INTERVAL state still runs one SustainAt batch: the synthetic
        // TIME_INTERVAL cadence sputters a single homing puff at the muzzle/exhaust.
        if (!moved)
            SustainAt(worldPos, worldBasis, dt);
    }

    /// <summary>Stops a continuous run: trail AND sustain together, unconditionally — the
    /// ghost-trail rule: a pooled slot is teleported
    /// between call sites, so a trail origin kept across the pause would draw a puff line from
    /// the previous site on revival. Idempotent; live particles finish their own lifetimes
    /// (<see cref="Clear"/> is the hard kill).</summary>
    public void Stop()
    {
        SustainEnd();
        TrailEnd();
    }

    public override void _Process(double delta)
    {
        if (!_active)
            return;
        // Particles are sim state: they freeze with a halted clock and scale with a scaled one.
        float dt = GameClock.Current?.FrameDt ?? (float)delta;
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
        // Friction damps toward the WIND, not toward rest, read once per frame like the original.
        // Zero wind is an algebraic no-op, which is what keeps a windless golden unmoved.
        var windTarget = _ambience.Wind * _state.WindFactor;
        // A DRAW rule, not a sim rule: a discarded particle keeps living and ageing, just unwritten
        // this frame. Empty until WeatherRig.Tick publishes the pane cameras (EffectAmbience.Viewers).
        var viewers = _ambience.Viewers;
        bool fading = viewers.Count > 0;
        // Burst mode keeps particle positions in the node's own frame (the burst point is the
        // node's translation); trail and sustain force the transform to identity and store world
        // positions. Every mode leaves the basis at identity, so one addition covers all three.
        var nodeOrigin = GlobalPosition;
        int drawn = 0;
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
            // ⚠ Do not reorder: pos advances on last frame's velocity, THEN acceleration, THEN
            // friction. Swapping lands a different position on frame one for any accelerated puffer.
            p.Pos += p.Vel * dt;
            p.Vel += _state.WorldAcceleration * dt;
            // ⚠ Do not drop this gate. Damping unconditionally is identity only while the target is
            // rest; with wind as the target a frictionless puffer would wrongly feel it.
            if (_state.Friction != 0f)
            {
                p.Vel = ((p.Vel - windTarget) * damp) + windTarget;
            }

            // A negative-age particle is still drawn on the frame it's born, pinned to ramp stop 0
            // — see docs/org/puffer.md. Distance gate before any draw work: discarded means unwritten.
            float distAlpha = 1f;
            if (fading && !NearestViewerAlpha(nodeOrigin + p.Pos, viewers, out distAlpha))
                continue;

            float lifeFrac = p.Age > 0f ? p.Age / p.Life : 0f;
            float size = p.BaseSize * Mathf.Lerp(1f, _state.GrowthFactor, lifeFrac);
            // ⚠ The distance alpha MULTIPLIES the COLORS ramp's alpha or the fade envelope's; it
            // replaces neither. Getting that precedence wrong makes every ramped puffer invisible.
            _renderer.Write(drawn++, p.Pos, size,
                flipbook ? FrameFor(lifeFrac) : p.Frame,
                (hasRamp ? 1f : FadeFor(lifeFrac)) * distAlpha,
                hasRamp ? RampColor(lifeFrac) : Colors.White);
        }
        _renderer.Show(drawn);

        if (_liveCount == 0 && !_emitting && !_trailing && !_sustaining)
        {
            _active = false;
            Visible = false;
        }
    }

    // A fade band's `1/width`, with the engine's own degenerate case: both of its
    // setters store the raw difference first and only invert it
    // when it is non-zero, so an equal-ended band keeps 0 rather than an infinity. The unauthored
    // far band (`FLT_MAX`, `FLT_MAX`) is exactly that case, and nothing ever reaches its
    // ramp anyway.
    private static float Reciprocal(float width) => width != 0f ? 1f / width : 0f;

    private static float FadeFor(float lifeFrac) =>
        lifeFrac < FadeIn ? lifeFrac / FadeIn
        : lifeFrac > FadeOutStart ? Mathf.Max(0f, 1f - (lifeFrac - FadeOutStart) / (1f - FadeOutStart))
        : 1f;

    // The cached form of BakeAtlas: one bake per (archive, frame list, sequenced).
    private static (ImageTexture? Atlas, bool DiesDark) BuildAtlas(IReadOnlyList<string> names,
        TextureArchive textures, bool sequenced)
    {
        var cache = AtlasCache.GetOrCreateValue(textures);
        string key = (sequenced ? "seq:" : "pool:") + string.Join("\n", names);
        if (cache.TryGetValue(key, out var hit))
            return hit;
        var baked = BakeAtlas(names, textures, sequenced);
        if (baked.Atlas is { } atlas)
            cache[key] = (atlas, baked.DiesDark);
        return baked;
    }

    // Packs the frames side by side into one atlas, and measures whether a particle DIES
    // on a dark sprite — the flipbook's last frame, or a static pool's mean luminance. This is what
    // distinguishes "fire_n_smoke", whose flipbook ends near-black despite starting bright; see
    // `docs/org/puffer.md`. Null atlas when a frame is missing.
    private static (ImageTexture? Atlas, bool DiesDark) BakeAtlas(IReadOnlyList<string> names,
        TextureArchive textures, bool sequenced)
    {
        if (names.Count == 0)
            return (null, false);
        var frames = new Image[names.Count];
        int fw = 0, fh = 0;
        for (int i = 0; i < names.Count; i++)
        {
            var tex = textures.Find(names[i]);
            if (tex == null)
                return (null, false);
            var img = tex.GetImage();
            img.Convert(Image.Format.Rgba8);
            frames[i] = img;
            fw = Mathf.Max(fw, img.GetWidth());
            fh = Mathf.Max(fh, img.GetHeight());
        }
        var atlas = Image.CreateEmpty(fw * names.Count, fh, false, Image.Format.Rgba8);
        float lastLum = 0f, meanLum = 0f;
        for (int i = 0; i < frames.Length; i++)
        {
            var f = frames[i];
            if (f.GetWidth() != fw || f.GetHeight() != fh)
                f.Resize(fw, fh);
            atlas.BlitRect(f, new Rect2I(0, 0, fw, fh), new Vector2I(i * fw, 0));
            lastLum = MeanLuminance(f);
            meanLum += lastLum / frames.Length;
        }
        return (ImageTexture.CreateFromImage(atlas),
            (sequenced ? lastLum : meanLum) < SmokeLuminance);
    }

    // A sprite's mean luminance weighted by its own alpha — what it actually
    // contributes when composited, rather than what its unmasked pixels contain.
    private static float MeanLuminance(Image img)
    {
        int w = img.GetWidth(), h = img.GetHeight();
        if (w == 0 || h == 0)
            return 0f;
        float sum = 0f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = img.GetPixel(x, y);
                sum += (0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B) * c.A;
            }
        return sum / (w * h);
    }

    // The distance alpha across every pane: keeps the most favourable
    // answer of DistanceAlpha evaluated per viewer, drawing if any pane would draw it.
    // ⚠ Must be per-viewer, not nearest-by-range: the fade is view-space depth along each camera's
    // own forward axis, and a pane facing away can still be the range-nearest one. See
    // `docs/architecture.md`.
    private bool NearestViewerAlpha(Vector3 worldPos,
        IReadOnlyList<ViewerSet.ViewerPose> viewers, out float alpha)
    {
        alpha = 0f;
        bool drawn = false;
        for (int v = 0; v < viewers.Count; v++)
        {
            var pose = viewers[v];
            if (!DistanceAlpha(worldPos, pose.Position, pose.Forward, out float paneAlpha))
                continue;
            drawn = true;
            if (paneAlpha > alpha)
                alpha = paneAlpha;
            if (alpha >= 1f)
                break;                      // nothing a further pane says can beat full alpha
        }
        return drawn;
    }

    // The camera-distance alpha for ONE viewer, decoded verbatim from the original's
    // per-particle draw; `false` means the particle is discarded this frame. The distance is
    // view-space DEPTH, not euclidean range. ⚠ The near ramp reads the FAR band's origin — not a
    // typo, verified in raw assembly, and must not be "repaired". `_distanceFade`/`_farCull`/
    // `_nearCull` are three separate mechanisms, not one switch. See
    // `docs/formats/effects.md` and `docs/org/puffer.md`.
    private bool DistanceAlpha(Vector3 worldPos, in Vector3 camPos, in Vector3 camFwd,
        out float alpha)
    {
        alpha = 1f;
        float d = camFwd.Dot(worldPos - camPos);
        float scaled = d * _globalFadeFactor;   // the far band only — see GlobalFadeFactorDefault
        if (_farCull && _state.FarFadeEnd <= scaled)
            return false;
        if (scaled <= _state.FarFadeStart)
        {
            // Inside the far band's full-alpha region, so the NEAR band decides. Unauthored, that
            // is (0, 0): nothing is nearer than depth 0 except what is behind the camera, which
            // the engine culls here and so do we.
            if (_nearCull && d <= _state.NearFadeStart)
                return false;
            if (_state.NearFadeEnd <= d || !_distanceFade)
                return true;                    // the engine's `goto`: alpha 1, past the >0 gate
            alpha = (d - _state.FarFadeStart) * _nearRecip;   // ⚠ FarFadeStart — the cross-wire
        }
        else
        {
            if (!_distanceFade)
                return true;
            alpha = (_state.FarFadeEnd - scaled) * _farRecip;
        }
        return alpha > 0f;
    }

    // Advances a DISTANCE_INTERVAL trail emitter to the followed node's new world position: the
    // engine's one emission accumulator fed with metres of motion instead of seconds, then the
    // same batch loop as SustainAt (NUMBER particles per batch, spread along this frame's motion,
    // born the sub-frame age, with LOCAL_VELOCITY in the host's frame). The first call starts the
    // trail; call every frame while the effect is on. See docs/org/puffer.md, "The emission
    // accumulator".
    private void TrailAdvance(Vector3 worldPos, Basis worldBasis, float dt)
    {
        if (_state.DistanceInterval <= 0f)
            return;
        if (!_trailing)
        {
            TopLevel = true;             // particles live in world space, left behind the plane
            // ⚠ Set the whole transform, not just position: TopLevel preserves the global transform
            // (Godot 4), so a flying parent's attitude would otherwise yaw world-space puffs off-origin.
            GlobalTransform = Transform3D.Identity;
            _trailing = true;
            _trailPrev = worldPos;
            _trailCarry = 0f;
            _active = true;
            Visible = true;
            return;
        }
        var prev = _trailPrev;
        float dist = (worldPos - prev).Length();
        _trailPrev = worldPos;
        if (dist < 1e-5f)
            return;
        // The original's teleport guard, verbatim: skip the whole emit past this distance so a
        // jump lays no puff line along itself. See docs/org/puffer.md.
        if (dist >= TeleportGuardMeters)
        {
            if (_teleportGuardCount++ == 0)
                Log.Info("world", $"puffer teleport guard tripped name={_state.Name} dist={dist:0.0}m guard={TeleportGuardMeters:0}m (first occurrence only; further ones counted silently)");
            return;
        }
        _trailCarry += dist;
        EmitBatches(prev, worldPos, worldBasis, dt, ref _trailCarry, _state.DistanceInterval, _trailSizeScale);
    }

    // Stops trail emission; live smoke decays naturally.
    private void TrailEnd() => _trailing = false;

    // Static-viewer variant of TrailAdvance: emits the trail's
    // per-meter puffs AT a fixed world point, spending `speedMps`
    // meters of virtual motion per second — the damage lab's parked plane, whose
    // panels burn in place (the puffs' own random velocity and growth make the
    // stacked emissions read as a flickering fire). Same carry, pool and spawn
    // path as the moving trail, with no motion to spread along and no sub-frame age.
    private void TrailBurnAt(Vector3 worldPos, Basis worldBasis, float dt, float speedMps)
    {
        if (_state.DistanceInterval <= 0f)
            return;
        if (!_trailing)
        {
            TopLevel = true;
            GlobalTransform = Transform3D.Identity; // see TrailAdvance: TopLevel keeps the global basis
            _trailing = true;
            _trailPrev = worldPos;
            _trailCarry = 0f;
            _active = true;
            Visible = true;
        }
        _trailCarry += speedMps * dt;
        EmitBatches(worldPos, worldPos, worldBasis, 0f, ref _trailCarry, _state.DistanceInterval, _trailSizeScale);
    }

    // The engine's batch loop, shared by the distance and time modes: the FULL
    // floor(accumulator / interval), uncapped, batch b at `frac = (b+1)·interval/accumulator`
    // along `prev → origin` and born `(1 − frac)·dt` old. ⚠ Do not cap the count and do not
    // clamp the age; both are invented divergences, see docs/org/puffer.md.
    private void EmitBatches(Vector3 prev, Vector3 origin, Basis worldBasis, float dt,
        ref float accumulator, float interval, float sizeScale)
    {
        interval = Mathf.Max(interval, 1e-3f);
        float total = accumulator;
        int batches = (int)(total / interval);
        for (int b = 0; b < batches; b++)
        {
            float frac = (b + 1) * interval / total;
            SpawnSustained(prev.Lerp(origin, frac), worldBasis, (1f - frac) * dt, sizeScale);
        }
        accumulator -= batches * interval;
    }

    // Continuous emission at a moving world point, spread along the emitter's own motion
    // since the previous call rather than stacked on today's pose (batch `k` at
    // `frac = (k+1)·interval/accumulator` along `prevOrigin → origin`). Call every frame
    // while the puffer is on; SustainEnd stops emission and lets particles decay.
    // ⚠ No per-frame batch cap — see `docs/org/puffer.md` for why a cap is actively wrong.
    private void SustainAt(Vector3 worldPos, Basis worldBasis, float dt)
    {
        var origin = worldPos + worldBasis * _state.AtNodeOffset;
        if (!_sustaining)
        {
            TopLevel = true;                 // world-space particles, like the trail mode
            GlobalTransform = Transform3D.Identity; // see TrailAdvance: TopLevel keeps the global basis
            _sustaining = true;
            _active = true;
            Visible = true;
            _sustainCarry = _state.TimeInterval; // emit on the very first frame
            // No prior pose to interpolate from — re-home here rather than trailing from a
            // stale point, exactly as TrailAdvance's own homing rule: without this a revive
            // after Stop() would draw a line of puffs from wherever the emitter last was.
            _sustainPrevOrigin = origin;
        }
        _sustainCarry += dt;
        // ⚠ The age offset rides the RAW dt, unclamped, exactly as the engine does. Clamping it to
        // keep long-hitch batches alive is an invented divergence — see docs/org/puffer.md.
        EmitBatches(_sustainPrevOrigin, origin, worldBasis, dt, ref _sustainCarry, _state.TimeInterval, _sustainSizeScale);
        _sustainPrevOrigin = origin;
    }

    // Stops sustained emission; live particles finish their lifetimes.
    private void SustainEnd() => _sustaining = false;

    private void Init(PufferState state, IEmitterRenderer renderer, float activeDuration,
        bool sustained, EffectAmbience? ambience = null, PufferFadeSwitches? fade = null)
    {
        _state = state;
        _renderer = renderer;
        _ambience = ambience ?? EffectAmbience.Still;
        Name = "puffer_" + state.Name;
        _burstSizeScale = Config.GetFloat("puffer.burstSizeScale", SizeScaleDefault);
        _trailSizeScale = Config.GetFloat("puffer.trailSizeScale", SizeScaleDefault);
        _sustainSizeScale = Config.GetFloat("puffer.sustainSizeScale", SizeScaleDefault);
        // Every distance-switch default reproduces the original; see the fields' remark and
        // DistanceAlpha.
        _distanceFade = fade?.DistanceFade ?? Config.GetBool("puffer.distanceFade", true);
        _farCull = fade?.FarCull ?? Config.GetBool("puffer.farCull", true);
        _nearCull = fade?.NearCull ?? Config.GetBool("puffer.nearCull", true);
        _globalFadeFactor = fade?.GlobalFadeFactor
            ?? Config.GetFloat("puffer.globalFadeFactor", GlobalFadeFactorDefault);
        _nearRecip = Reciprocal(state.NearFadeEnd - state.NearFadeStart);
        _farRecip = Reciprocal(state.FarFadeEnd - state.FarFadeStart);
        _priorityFactor = 1f + PriorityScaleDefault * state.Priority;
        if (state.DistanceInterval > 0f)
        {
            // Distance states use the trail pool even on the sustained path: the steady-state formula
            // below would floor them at 16, dropping ~85% of a flight-speed trail's authored emission.
            _particles = new Particle[TrailPool];
        }
        else if (sustained)
        {
            // Number per interval, each living up to LIFETIME_RANGE's max, plus one interval's
            // worth of headroom — the authored lifetime is the whole story.
            int steady = Mathf.CeilToInt(state.Number * state.LifetimeMax
                                         / Mathf.Max(state.TimeInterval, 1e-3f)) + state.Number;
            _particles = new Particle[Mathf.Clamp(steady, SustainPoolMin, SustainPoolMax)];
        }
        else
        {
            // Emit at t = 0, TimeInterval, 2·TimeInterval, … while active.
            _burstsTotal = Mathf.Max(1, Mathf.FloorToInt(activeDuration / Mathf.Max(state.TimeInterval, 1e-3f) + 1e-3f) + 1);
            _particles = new Particle[state.Number * _burstsTotal];
        }

        // The cull margin is a fact about the particles, not about the draw: it covers the largest
        // tuned size any spawn path here could produce, and the renderer only applies it.
        renderer.Attach(this, _particles.Length,
            Mathf.Max(4f, state.SizeMax * state.GrowthFactor * _priorityFactor
                * Mathf.Max(_burstSizeScale, Mathf.Max(_trailSizeScale, _sustainSizeScale))));
        Visible = false;
    }

    // One batch of a continuous emitter, trail or sustain. `origin` is the emitter's world point
    // for this batch, already AT_NODE-offset and motion-interpolated (EmitBatches owns both).
    // `ageOffset` is the sub-frame `(1 - frac)·dt` correction, added to the start age even for a
    // puffer with no `START_AGE_RANGE` authored.
    private void SpawnSustained(Vector3 origin, Basis worldBasis, float ageOffset, float sizeScale)
    {
        var min = _state.MinRandomVelocity;
        var max = _state.MaxRandomVelocity;
        // LOCAL_VELOCITY is in the emitter node's frame (the smokestack's "up", the smoke
        // screen's astern); WORLD_VELOCITY is not. Rotating the local part is what keeps a
        // banking/turning emitter correct.
        var baseVel = worldBasis * _state.LocalVelocity + _state.WorldVelocity;
        float d = _state.DeviationDistance;
        for (int k = 0; k < _state.Number; k++)
        {
            if (_liveCount >= _particles.Length && !GrowPool())
                break;
            // ⚠ The draw order (pos → vel → size → life) and the ±0.5·d half-width offset are both
            // load-bearing: changing either re-scatters every sustained emitter (c1-waterfall golden).
            var pos = origin + new Vector3(Rand(-0.5f * d, 0.5f * d), Rand(-0.5f * d, 0.5f * d), Rand(-0.5f * d, 0.5f * d));
            var vel = baseVel + new Vector3(Rand(min.X, max.X), Rand(min.Y, max.Y), Rand(min.Z, max.Z));
            float size = Rand(_state.SizeMin, _state.SizeMax) * sizeScale * _priorityFactor;
            float life = Rand(_state.LifetimeMin, _state.LifetimeMax);
            // Gated on HasStartAgeRange so the ~2,900 puffers without the key draw nothing extra and
            // stay bit-identical. ageOffset is computed, not drawn, so it costs no extra _rng call.
            float age = (_state.HasStartAgeRange ? Rand(_state.StartAgeMin, _state.StartAgeMax) : 0f) + ageOffset;
            float frame = _state.TextureSequence.Count > 0 ? 0f
                : Mathf.Min(_state.Textures.Count - 1,
                    Mathf.FloorToInt((float)_rng.NextDouble() * _state.Textures.Count));
            // The engine's born-dead skip: a particle born past its own lifetime is never created.
            // Every draw above still runs, so a skip changes what's stored, never the neighbours' scatter.
            if (age >= life)
                continue;
            _particles[_liveCount++] = new Particle
            {
                Pos = pos,
                Vel = vel,
                BaseSize = size,
                Life = life,
                Age = age,
                Frame = frame,
            };
        }
    }

    // A continuous emitter's pool grows to what its authored emission needs, up to a hard ceiling,
    // because the engine bounds its particles only by what can be born alive (docs/org/puffer.md):
    // the smoke screen's NUMBER 4 per 0.65 m at flight speed keeps ~2,000 puffs alive, three times
    // the trail pool it starts on. False once the ceiling is reached; the batch then drops the rest.
    private bool GrowPool()
    {
        if (_particles.Length >= ContinuousPoolMax)
            return false;
        int capacity = Mathf.Min(_particles.Length * 2, ContinuousPoolMax);
        Array.Resize(ref _particles, capacity);
        _renderer.Grow(capacity);
        return true;
    }

    // Interpolates the COLORS (lifeFrac, color) ramp.
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
            // ±0.5·d, not ±d — see SpawnSustained's comment.
            var pos = new Vector3(Rand(-0.5f * d, 0.5f * d), Rand(-0.5f * d, 0.5f * d), Rand(-0.5f * d, 0.5f * d));
            var vel = baseVel + new Vector3(Rand(min.X, max.X), Rand(min.Y, max.Y), Rand(min.Z, max.Z));
            float size = Rand(_state.SizeMin, _state.SizeMax) * _burstSizeScale * _priorityFactor;
            float life = Rand(_state.LifetimeMin, _state.LifetimeMax);
            // START_AGE_RANGE: see SpawnSustained's comment — gated draw, right after Life.
            float age = _state.HasStartAgeRange ? Rand(_state.StartAgeMin, _state.StartAgeMax) : 0f;
            // The engine's born-dead skip — see SpawnSustained. The burst path has no sub-frame
            // term either (its batches are keyed off _sinceStart, not a carried accumulator), so
            // as on the trail path only an authored START_AGE_RANGE can fire it.
            if (age >= life)
                continue;
            _particles[_liveCount++] = new Particle
            {
                Pos = pos,
                Vel = vel,
                BaseSize = size,
                Life = life,
                Age = age,
            };
        }
        _burstsSpawned++;
    }

    // Latest flipbook frame whose keyed time has been reached (times ascending). ⚠ The
    // key is a FRACTION of the particle's own lifetime, not seconds — reading it as seconds
    // collapsed `large_30sec_fire` into a static ball. See `docs/org/puffer.md`.
    private float FrameFor(float lifeFrac)
    {
        var seq = _state.TextureSequence;
        int frame = 0;
        for (int i = 0; i < seq.Count && seq[i].Time <= lifeFrac; i++)
            frame = i;
        return frame;
    }

    private float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

    private struct Particle
    {
        public Vector3 Pos, Vel;
        public float BaseSize, Age, Life;
        public float Frame; // static-TEXTURES pool: the randomly picked atlas column
    }
}
