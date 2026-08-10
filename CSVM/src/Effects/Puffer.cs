using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>How a puffer's sprites composite. The authored data never says, so <see cref="Auto"/>
/// derives it: a COLORS ramp or a near-black dying sprite ⇒ <c>blend_mix</c>, else
/// <c>blend_add</c>. The explicit modes force the verdict.</summary>
public enum PufferBlend { Auto, Additive, Mix }

/// <summary>C7's three distance switches plus the original's own far-band multiplier, forced
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

    /// <summary>Random birth age (seconds): a particle is born at
    /// <c>Rand(StartAgeMin, StartAgeMax)</c> rather than at age 0. Both default to 0, so an
    /// unauthored puffer draws nothing extra and seeds <c>Particle.Age = 0f</c> exactly as before.
    /// Only 4 puffers in the install author this key, one of them (<c>fire_at_zepskin3</c>) with a
    /// negative minimum (−1.0) — ~91% of its particles are therefore born already aged.
    ///
    /// <para>⚠ This field is NOT the whole of the engine's birth age, and B4 originally mis-scoped
    /// the born-dead skip on that mistake. <c>FUN_0054f8b0</c>'s <c>age0</c> is
    /// <c>START_AGE_RANGE draw + (1 - frac)·dt</c> — the sub-frame term B5 added in
    /// <see cref="SustainAt"/> — and the engine skips creating the particle outright when
    /// <c>age0 >= life</c>. B4 closed that skip as unreachable by comparing the authored key alone
    /// (max start age 0.1 s vs. min lifetime 1.0 s across the four authoring puffers). That
    /// comparison is right about the key and wrong about the guard: the sub-frame term makes the
    /// skip reachable on any long frame, for ANY puffer, with no <c>START_AGE_RANGE</c> authored at
    /// all — a 5 s hitch hands the earliest catch-up batch an offset of ~4.8 s, which is past every
    /// lifetime in the install. The skip is implemented in all three spawn paths.</para></summary>
    public float StartAgeMin, StartAgeMax;

    /// <summary>How strongly this puffer's particles are carried by the world's wind
    /// (<see cref="WorldWind"/>): <c>FRICTION</c> damps each particle's velocity toward
    /// <c>wind × WindFactor</c> rather than toward rest (<c>FUN_0054ee10</c>, object <c>+0x6c</c>,
    /// particle <c>[0x14]</c>, parser flag <c>0x100000</c>).
    ///
    /// <para>⚠ <b>The default is 1, not 0.</b> The puffer object's constructor
    /// (<c>FUN_00550100</c>) writes <c>0x3f800000</c> to <c>+0x6c</c> and the applier
    /// (<c>FUN_004e7e40</c>) only overwrites it when the authoring flag is set, so a puffer that
    /// says nothing about wind is FULLY carried by it. Reading an absent key as 0 would silently
    /// becalm 2,802 of the install's 2,863 friction-bearing events. Absent and explicit-zero must
    /// therefore stay distinguishable, and they are: the compiled surface writes
    /// <c>wind_factor: null</c> when unauthored and <c>0.0</c> when a puffer deliberately opts out
    /// (6 names, 12 events — the <c>subdoors_puffer</c> family).</para>
    ///
    /// <para>Inert unless <see cref="Friction"/> is non-zero: the engine's whole damp-toward-wind
    /// block sits inside <c>if (friction != 0)</c>, so a frictionless puffer feels no wind at any
    /// factor.</para></summary>
    public float WindFactor = 1f;

    /// <summary>The camera-distance bands (C7), in metres of VIEW-SPACE DEPTH — see
    /// <see cref="Puffer.DistanceAlpha"/>, which is the one place their meaning lives.
    ///
    /// <para><b>Near</b> (<c>NEAR_FADE</c>, compiled <c>unk_range</c>, object <c>+0xd0</c>):
    /// <see cref="NearFadeStart"/> is the HARD DISCARD cutoff — a particle nearer than it is not
    /// drawn — and <see cref="NearFadeEnd"/> is where alpha would reach 1. <b>Far</b>
    /// (<c>FADE_RANGE</c>/<c>FAR_FADE</c>, compiled <c>fade_range</c>, object <c>+0xdc</c>):
    /// <see cref="FarFadeStart"/> is the last full-alpha distance and <see cref="FarFadeEnd"/> the
    /// hard cutoff.</para>
    ///
    /// <para>⚠ <b>Do not infer that order from the authored values.</b> Five of the install's six
    /// near pairs are DESCENDING (<c>70,30</c>; <c>70,20</c>; <c>70,50</c>; <c>40,5</c>;
    /// <c>30,10</c>) and read backwards at a glance. The order is settled at six independent points
    /// in <c>crimson.exe</c> — parser, applier, the two setters <c>FUN_00550500</c>/
    /// <c>FUN_00550550</c>, the ctor defaults, the spawn copy and the raw x87 comparisons in
    /// <c>FUN_0054e6e0</c> — and the ctor defaults settle which block is which, being coherent only
    /// this way round: near <c>(0, 0)</c>, far <c>(FLT_MAX, FLT_MAX)</c>, i.e. no fade and no cull
    /// unless authored, which is exactly the defaults below.</para></summary>
    public float NearFadeStart, NearFadeEnd;

    /// <inheritdoc cref="NearFadeStart"/>
    public float FarFadeStart = float.MaxValue, FarFadeEnd = float.MaxValue;

    /// <summary>A per-puffer sprite-size nudge, almost certainly a depth-priority constant reused
    /// for this — this pass found only the size use (object <c>+0x70</c>, particle <c>[0x15]</c>,
    /// parser flag <c>0x400000</c>). <c>FUN_0054e6e0</c> scales the drawn screen radius by
    /// <c>1 + K·PRIORITY</c>, folded into <see cref="Particle.BaseSize"/> at spawn instead
    /// (<see cref="Puffer.PriorityScaleDefault"/>). Default 0, so an unauthored puffer's factor is
    /// exactly 1 — 47 puffers in the install author a non-zero value, 192 compiled events (C8).</summary>
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

    /// <summary>
    /// Builds a state from a **compiled** animation's PUFFER_STATE event payload (the
    /// `AnimEvent.Data` of an `AnimRuntime` dispatch), as opposed to <see cref="Parse"/>'s
    /// reader form. The two agree: cross-checked on the C1 train's `steampuffer`, every
    /// shared field is identical (interval 0.03, LOCAL_VELOCITY 0/15/0, SIZE_RANGE 0.8–1.5,
    /// LIFETIME_RANGE 0.5–4.5, friction 3, the five texture names, the three-stop colour ramp).
    ///
    /// Three shape differences worth knowing:
    /// - the emission interval is **always** in `interval_garbage.interval_value` — the
    ///   `interval` field itself is null in all 4,387 PUFFER_STATE events of this install;
    /// - that interval is in SECONDS for a burst/sustained emitter but in METERS for a
    ///   DISTANCE_INTERVAL trail (the crash-debris `spurtpuffer`s), told apart by
    ///   `interval_garbage.interval_type` (`"Time"` vs `"Distance"`). ⚠ For a Distance
    ///   puffer the flag shape is INVERTED — `has_interval_type` is true but
    ///   `has_interval_value` is **false**, yet `interval_value` still holds the real
    ///   distance — so key off `interval_type`, never off `has_interval_value`;
    /// - `growth_factors` is the **`SCALE_SEQUENCE` age→scale ramp**, not a growth range: entry
    ///   `i` is `(age_i, scale_i)` carried under the `min`/`max` field labels. `GROWTH_FACTOR` is
    ///   simply its two-stop spelling — `FUN_004f7120` synthesises `(0,1), (1,G)` when
    ///   `SCALE_SEQUENCE` is absent, which it is in every reader in this install — so reading
    ///   `growth[1].max` yields G. ⚠ It is **not** a `(min,max)` pair: 216 events author a second
    ///   entry whose "max" is below its "min" (down to `(1.0, −0.2)`), coherent as a stop and
    ///   incoherent as a range — never "repair" one by swapping. No puffer in the install ships
    ///   more than two stops, so the `1 → G` lerp below is correct for all of them. The former
    ///   "matches 172 of 177 puffers, five name collisions" note here was **withdrawn** — that
    ///   survey does not reproduce (zero real mismatches; the unexplained names were wildcard
    ///   reader names expanding at compile time). See `docs/formats/anim-definitions.md`.
    /// </summary>
    public static PufferState FromAnimEvent(AnimData d)
    {
        Vector3 Vec(string key) => d.Vec3(key);
        float Range(string key, string end, float fallback) =>
            d.Obj(key)?.Num(end) ?? fallback;

        // The one interval number is seconds (Time) or meters (Distance) — see the remark above.
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
            // C7's two bands. `unk_range` IS NEAR_FADE — confirmed against the reader surface on
            // large_black_smokeball, whose compiled unk_range is {70, 20} and whose reader block
            // authors NEAR_FADE [70, 20]. Absent stays at the ctor's FLT_MAX far band / (0,0)
            // near band, i.e. no fade and no cull.
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

        // FADE_RANGE and FAR_FADE are two spellings of one block (the parser FUN_004f7120 accepts
        // both onto the same flag bit 0x1000). The install authors FADE_RANGE 574 times and
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
            // C7's two bands — see NearFadeStart for which end is which, and DO NOT read it off
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

/// <summary>
/// A running instance of a <see cref="PufferState"/>: a CPU-simulated burst of
/// billboard sprites, handed one frame at a time to an <see cref="IEmitterRenderer"/>.
/// The CPU integration honours the reader parameters directly — per-axis random
/// velocity, exponential friction, size growth, and the exact (non-uniform) flipbook
/// timing — which map awkwardly onto Godot's built-in particle process material.
///
/// Everything past that integration — the atlas, the shader and the <c>MultiMesh</c> —
/// is behind the renderer seam, so the three emission modes are reachable by a test with
/// no GPU and no <c>TextureArchive</c> (see <see cref="CreateWith"/>).
///
/// Reusable across the effect readers; today it drives the crash fireball
/// (<c>flame_ball.json</c> → <c>fierypuffer</c>).
/// </summary>
public sealed partial class Puffer : Node3D
{
    /// <summary>Default for the three per-spawn-path size multipliers (config.json <c>puffer</c>
    /// block). Not a judgement call: <c>FUN_0054e6e0</c> hands <c>SIZE_RANGE</c> straight to
    /// <c>FUN_0057c5c0</c> as a screen-space HALF-extent (<c>*param_1 - param_2</c> …
    /// <c>param_2 + *param_1</c> in both axes, confirmed by the <c>0.5 / param_2</c> UV-clip
    /// term in the same function), so the authored value is a radius and the sprite spans
    /// <c>2 × SIZE_RANGE</c> — the decoded radius→diameter conversion
    /// (`docs/PLAN-puffer-engine-deltas.md` A1). The three <c>puffer.*SizeScale</c> config keys
    /// remain as knobs for deliberate per-path tuning, now defaulting to the decode rather than
    /// a guess. Referenced by <see cref="Utils.Config.WarmTuningRegistry"/> so
    /// <c>--dump-config</c> documents the keys.</summary>
    public const float SizeScaleDefault = 2f;

    /// <summary>TUNE defaults (config.json <c>puffer.fireRiseScale</c> /
    /// <c>puffer.fireLifetimeScale</c>): multiply the fire puffer's world-vertical spawn velocity
    /// and its per-puff lifetime. INVENTED against the original's footage, not decoded — the
    /// authored numbers integrate to a ~10–12 m column for the 30 s fire under FRICTION 0.6,
    /// while the original's tank/destruction fire columns read as an unbroken ~3+
    /// building-height plume (`OriginalScreenshots/C1 IA1 Burning Fuel Tanks.png`, the
    /// `C1 IA1 Destruction.mp4` t≈176 s columns). 2.5×/1.5× puts the 30 s fire's apex at
    /// ~30–45 m and lets particles live to it.</summary>
    public const float FireRiseScaleDefault = 2.5f;
    public const float FireLifetimeScaleDefault = 1.5f;

    /// <summary>Default for config.json <c>puffer.globalFadeFactor</c> — the original's own
    /// <c>PufferSetGlobalFadeFactor</c> (<c>00637a94</c>), which multiplies the distance the FAR
    /// band is measured at and nothing else (both near comparisons use the plain, unscaled depth).
    /// Below 1 it pushes the far fade and its cutoff outward; above 1 it pulls them in. It is a
    /// script/debug-console knob with no authored writer in this install, so it ships at its own
    /// default of 1.0 — exposed rather than hard-coded because it is the original's distance
    /// multiplier, and reaching for one of our own instead is exactly what
    /// <c>PLAN-puffer-engine-deltas.md</c> C7 says not to do.</summary>
    public const float GlobalFadeFactorDefault = 1f;

    /// <summary>C8's <c>K</c>: <c>FUN_0054e6e0</c> scales the drawn screen radius by
    /// <c>1 + K·PRIORITY</c>. <c>_DAT_00a06fb0</c>, written by <c>FUN_0054d9c0</c>, is <c>0.01</c>
    /// on the software path and <c>0.02</c> on the hardware one — this project has no software
    /// path (see <see cref="DistanceAlpha"/>'s remark on the same split), so this is the hardware
    /// value. Default <c>PRIORITY</c> is 0, so the factor is 1 unless authored.</summary>
    public const float PriorityScaleDefault = 0.02f;

    /// <summary>The authored fire-column family — the crash fire (<c>large_10sec_fire</c>), the
    /// destruction fires (<c>large_30sec_fire</c>/<c>huge_30sec_fire</c>) and the burning fuel
    /// tanks all emit a sustained puffer of exactly this name, and it is the family playtesting
    /// judged "climbs but not as high as the original". The two scales
    /// above apply only to it, so gun smoke, trails, sputters and the ambient
    /// steam/torch/waterfall emitters keep their authored numbers.</summary>
    private const string FirePufferName = "fire_n_smoke";

    /// <summary>Alpha-weighted mean luminance (0–1) below which the sprite a particle dies on
    /// counts as smoke, so the emitter alpha-blends instead of adding. The measured population
    /// separates cleanly either side of it: fire_f06 0.018 and thickblksmoke 0.004 below,
    /// nothing above it under 0.12 (fire101 0.12, exp_yel01 0.17, smoke101 0.22, fire_f01 0.34)
    /// — so white smoke stays additive and only genuinely black sprites flip.</summary>
    private const float SmokeLuminance = 16f / 255f;

    // Life-fade envelope (a render nicety, not in the reader): ease the additive glow in
    // and out so particles don't pop at spawn/death. The flipbook itself already dims.
    private const float FadeIn = 0.12f, FadeOutStart = 0.6f;

    private const int TrailPool = 640; // live-particle cap for trail emitters
    // A sustained emitter never stops, so its pool is sized to the steady-state population
    // (Number per interval, each living up to LifetimeMax) rather than to a burst duration.
    private const int SustainPoolMin = 16, SustainPoolMax = 2048;
    /// <summary>C9: the original's teleport guard on the DISTANCE path. <c>FUN_0054f8b0</c>
    /// accumulates the frame's motion length into the emitter's interval counter only
    /// <c>if (len &lt; 200.0)</c> — a respawned or pooled emitter that jumps across the world
    /// lays no line of puffs along the jump. The time path has no equivalent test: there the
    /// engine accumulates <c>dt</c> unconditionally.</summary>
    private const float TeleportGuardMeters = 200f;

    // Particle spread/size/life/frame jitter. One stream per emitter, drawn off the master seed's
    // puffer stream, so a run repeats and two emitters still scatter independently.
    private readonly System.Random _rng = Rng.NewSystemRandom(Rng.Puffer);

    // Dev-tunable BaseSize multipliers, one per spawn path (burst = SpawnBatch, trail =
    // SpawnTrailPuff, sustain = SpawnSustained). Read once per emitter at Init; --det drops
    // config overrides (DET-7), so scripted captures stay a function of the committed tree.
    private float _burstSizeScale = SizeScaleDefault;
    private float _trailSizeScale = SizeScaleDefault;
    private float _sustainSizeScale = SizeScaleDefault;

    // C8: 1 + K·PRIORITY, read once at Init off the authored state — not a config knob, since it
    // is a decoded engine constant rather than a tuning surface.
    private float _priorityFactor = 1f;

    // The fire-column scales — 1 (inert) unless this emitter IS the fire puffer family
    // (FirePufferName), which only ever spawns on the sustained path.
    private float _fireRiseScale = 1f;
    private float _fireLifeScale = 1f;

    // C7's three switches (config.json puffer.distanceFade / farCull / nearCull), read once at
    // Init. All default TRUE — every one of them reproduces the original, and a flag that shipped
    // off would be a silent divergence wearing a config key. See DistanceAlpha.
    private bool _distanceFade = true;
    private bool _farCull = true;
    private bool _nearCull = true;
    private float _globalFadeFactor = GlobalFadeFactorDefault;

    // 1/(end - start) per band, precomputed at Init exactly as FUN_00550500/FUN_00550550 do at
    // set time — and, as they do, left as the raw difference (0) when the two ends are equal,
    // which is the unauthored far band's FLT_MAX/FLT_MAX case.
    private float _nearRecip;
    private float _farRecip;

    private PufferState _state = null!;
    private IEmitterRenderer _renderer = null!;
    // The world state this emitter reads but does not own (B6's wind; C7's camera position next).
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
    private Vector3 _sustainPrevOrigin; // last frame's emitter origin, for sub-frame interpolation (B5)

    // C9 instrumentation. A teleport is supposed to be rare, so the first occurrence per emitter
    // logs with its numbers and the rest are counted silently — LOG-5: the suppression is
    // announced in the line itself, so a reader never mistakes one line for one event.
    private int _teleportGuardCount;

    /// <summary>C9 diagnostics: how many frames this emitter's motion tripped the original's
    /// <see cref="TeleportGuardMeters"/> distance guard. Readable so a suite can assert the guard
    /// fired without parsing a log.</summary>
    public int TeleportGuardCount => _teleportGuardCount;

    /// <summary>Live particle count — diagnostics only (the <c>--debug-anim</c> puffer census, which
    /// is how a headless run confirms a crash's emitters are actually spawning).</summary>
    public int LiveCount => _liveCount;

    /// <summary>Builds an emitter for <paramref name="state"/>, loading its flipbook (or
    /// static TEXTURES pool) frames from <paramref name="textures"/> into an atlas. Null
    /// if no frame texture is found. <paramref name="activeDuration"/> is how long a
    /// burst puffer emits (the calling animation's STOP time; large_fireball stops its
    /// puffer at 0.3 s) — ignored by trail (DISTANCE_INTERVAL) states, which emit while
    /// driven via <see cref="TrailAdvance"/>. States that end on a dark sprite alpha-blend
    /// (black smoke is invisible additively); everything else stays additive.</summary>
    /// <param name="sustained">Continuous emission driven by <see cref="SustainAt"/> — the
    /// animation system's ACTIVE_STATE 1 puffers (the C1 train's steam plume, the waterfall
    /// mist), which run indefinitely at their TIME_INTERVAL rather than for a burst duration.</param>
    /// <param name="blend">Composite mode (see <see cref="PufferBlend"/>). Default
    /// <see cref="PufferBlend.Auto"/> measures the sprite a particle dies on (see
    /// <see cref="SmokeLuminance"/>); the explicit modes override that verdict.</param>
    /// <param name="softParticles">The depth fade over the last ~1.5 m before the scene depth,
    /// which softens the hard line where a tilted billboard dips into terrain. Null (default)
    /// pairs it with the blend: off for MIX, whose dark sprites emit at ground-level sites where
    /// the fade would zero them against the terrain right behind them, on for additive, which
    /// leaks through the fade anyway.</param>
    /// <param name="ambience">The session's world state (B6's wind). Null — the default — is
    /// still air, which is the right answer for a caller with no session (the unit suites, the
    /// plane viewer) and the wrong one for anything in a flown mission.</param>
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

    /// <summary>Builds an emitter over a supplied <paramref name="renderer"/> — the same modes, the
    /// same pool sizing and the same spawn paths as <see cref="Create"/>, with no atlas, no
    /// <c>TextureArchive</c> and no <see cref="MultiMesh"/> in the path. This is what makes the
    /// three modes assertable; the real path never calls it.
    ///
    /// <para>⚠ Constructing a <c>Puffer</c> draws one seed off the shared <see cref="Rng.Puffer"/>
    /// stream, so every emitter built after it scatters differently. That is why this is a test
    /// entry point and not a general one: calling it on a capture path would move every
    /// puffer-bearing golden.</para></summary>
    /// <param name="fade">C7's three switches, forced rather than read from <c>config.json</c>.
    /// Null — the default, and the only thing the real path ever passes — reads them from Config as
    /// usual. This exists because <see cref="Utils.Config"/> is file-backed with no setter by
    /// design (DET-7: a scripted capture stays a function of the committed tree), so a suite that
    /// wants to see the switches actually SWITCH has nowhere else to say so.</param>
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

    /// <summary>The one continuous drive: feed the host's current world pose + dt every frame and
    /// the authored state picks the mode — a TIME_INTERVAL state sustains, a DISTANCE_INTERVAL
    /// state emits per interval of the host's actual motion (<see cref="TrailAdvance"/> — the
    /// density the original's wing-burn footage shows, ~160 puffs/s at flight speed against the
    /// 10/s the time cadence would produce), and a host that stands still keeps the time cadence: the
    /// damaged building's sputter is a distance state whose authored interval can never elapse on
    /// a static object, and it has always emitted on time there (every distance state carries the
    /// parsers' synthetic 0.1 s TIME_INTERVAL, so that cadence always exists). A caller whose host
    /// CANNOT move (the damage lab's parked plane) passes <paramref name="staticBurnMps"/>:
    /// metres of virtual motion per second spent at the held pose (<see cref="TrailBurnAt"/>'s
    /// in-place burn) in place of the time cadence. <see cref="Stop"/> ends the run; the next
    /// call after a stop re-homes rather than trailing from the old site.</summary>
    public void Emit(Vector3 worldPos, Basis worldBasis, float dt, float staticBurnMps = 0f)
    {
        if (_state.DistanceInterval <= 0f)
        {
            SustainAt(worldPos, worldBasis, dt);
            return;
        }
        bool moved = _trailing && (worldPos - _trailPrev).LengthSquared() > 1e-8f;
        if (!moved && staticBurnMps > 0f)
        {
            TrailBurnAt(worldPos, dt, staticBurnMps);
            return;
        }
        TrailAdvance(worldPos);
        if (!moved)
            SustainAt(worldPos, worldBasis, dt);
    }

    /// <summary>Stops a continuous run: trail AND sustain together, unconditionally — the
    /// ghost-trail rule (the rocket-explosion fix, commit 450131a): a pooled slot is teleported
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
        // B6: friction damps toward the WIND, not toward rest. The target is read once per frame
        // because the original's is: FUN_0054ee10 re-derives the one global wind vector at the
        // head of the tick and every particle in the world integrates against that same value.
        // Zero wind (no session, no weather.json, or WIND_FACTOR 0) makes the whole thing an
        // algebraic no-op — (v - 0)·damp + 0 == v·damp — which is what lets it land without
        // moving a windless golden.
        var windTarget = _ambience.Wind * _state.WindFactor;
        // C7 — the camera-distance fade is a DRAW rule, not a sim rule: the original evaluates it
        // at the head of FUN_0054e6e0 (the per-particle draw) while FUN_0054ee10 (the sim) knows
        // nothing about it, so a discarded particle keeps living, moving and ageing and simply is
        // not written this frame. That is why the renderer gets its own cursor below: its contract
        // is "indices 0…n-1, packed and ascending, then Show(n)", and n is the DRAWN count, which
        // is at most _liveCount.
        // False until WeatherRig.Tick has published a camera — see EffectAmbience.HasCamera. In a
        // live session that is true from the first tick on; an emitter that draws on the very
        // first frame of a session can beat it by one frame and draw unfaded, which is one frame
        // of full alpha at session start and is left alone rather than deferred.
        bool fading = _ambience.HasCamera;
        var camPos = _ambience.CameraPosition;
        var camFwd = _ambience.CameraForward;
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
            // B6 — the engine's integration ORDER, verbatim from FUN_0054ee10 (raw x87 at
            // 0054efd0…0054f083): position advances on the velocity it had at the top of the
            // frame, THEN acceleration is added, and only then does friction damp. Ours used to
            // do all three the other way round (`v = v*damp + a*dt; pos += v*dt`), which lands a
            // different position on the very first frame of any accelerated puffer and a
            // different steady state for every damped one.
            p.Pos += p.Vel * dt;
            p.Vel += _state.WorldAcceleration * dt;
            // The engine gates the whole damp block on `friction != 0` (0054f016 FCOMP against
            // 0.0, JNZ past it), so a frictionless puffer feels no wind at any WIND_FACTOR. Ours
            // used to apply damp unconditionally, which was identity at friction 0 — it is NOT
            // identity once the wind is in it, so the gate is load-bearing now.
            if (_state.Friction != 0f)
            {
                p.Vel = ((p.Vel - windTarget) * damp) + windTarget;
            }

            // A negative-age particle (START_AGE_RANGE authoring a negative minimum, e.g.
            // fire_at_zepskin3's -1.0) is still drawn on the frame it's born — FUN_0054e6e0 clamps
            // the ramp parameter (`if (0.0 < age) t = age/life; else t = 0.0f`) rather than
            // skipping the draw, so it is pinned to stop 0 of every ramp/envelope while p.Age
            // itself keeps integrating and reaping normally above.
            // C7's distance gate, before any draw work: discarded means not written at all.
            float distAlpha = 1f;
            if (fading && !DistanceAlpha(nodeOrigin + p.Pos, camPos, camFwd, out distAlpha))
                continue;

            float lifeFrac = p.Age > 0f ? p.Age / p.Life : 0f;
            float size = p.BaseSize * Mathf.Lerp(1f, _state.GrowthFactor, lifeFrac);
            // The COLORS ramp owns the fade when present (its alpha ends at 0);
            // otherwise the render-nicety envelope eases the additive glow in/out. C7's distance
            // alpha MULTIPLIES whichever of the two owns it and replaces neither — the engine does
            // the same, folding its distance alpha into the ramp's own alpha (`local_2c * fVar5`)
            // or into the ramp-less envelope (`local_2c * (1 - ageFrac)`) at 0054e6e0. Getting
            // that precedence wrong makes every ramped puffer invisible. Both land in the shader's
            // `v_alpha`, which already multiplies by `v_color.a`, so the ramp path keeps carrying
            // its own alpha in the colour.
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

    /// <summary>A fade band's <c>1/width</c>, with the engine's own degenerate case: both setters
    /// (<c>FUN_00550500</c>, <c>FUN_00550550</c>) store the raw difference first and only invert it
    /// when it is non-zero, so an equal-ended band keeps 0 rather than an infinity. The unauthored
    /// far band (<c>FLT_MAX</c>, <c>FLT_MAX</c>) is exactly that case, and nothing ever reaches its
    /// ramp anyway.</summary>
    private static float Reciprocal(float width) => width != 0f ? 1f / width : 0f;

    private static float FadeFor(float lifeFrac) =>
        lifeFrac < FadeIn ? lifeFrac / FadeIn
        : lifeFrac > FadeOutStart ? Mathf.Max(0f, 1f - (lifeFrac - FadeOutStart) / (1f - FadeOutStart))
        : 1f;

    /// <summary>Packs the frames side by side into one atlas, and measures whether a particle
    /// DIES on a dark sprite — the last frame of a flipbook, or the mean of a static pool, since
    /// a pool sprite is picked once and held. The measurement is what tells "fire_n_smoke", whose
    /// flipbook really does run bright flame (fire_f01, 88) → near-black smoke (fire_f06, 4.6),
    /// apart from a ramp-less flash; the authored data says nothing about compositing, and the
    /// old ramp-presence rule read both as additive, which turned every dying smoke sprite into
    /// more glow. Null atlas when a frame is missing from the archive.</summary>
    private static (ImageTexture? Atlas, bool DiesDark) BuildAtlas(IReadOnlyList<string> names,
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

    /// <summary>A sprite's mean luminance weighted by its own alpha — what it actually
    /// contributes when composited, rather than what its unmasked pixels contain.</summary>
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

    /// <summary>C7 — the camera-distance alpha, decoded verbatim from the head of
    /// <c>FUN_0054e6e0</c>. Returns <c>false</c> when the particle is discarded for this frame;
    /// otherwise <paramref name="alpha"/> is the factor its drawn alpha is multiplied by.
    ///
    /// <para><b>The distance is view-space DEPTH, not euclidean range.</b> The engine computes
    /// <c>local_8 = m20·x + m21·y + m22·z + m23</c> off the world→view matrix
    /// <c>FUN_0054ed10</c> built, then scales it by <c>_DAT_009fd5c0</c>. That matrix's third
    /// column was itself pre-scaled by <c>_DAT_009fd5d0</c>, and on the hardware path
    /// <c>FUN_0053c110</c> sets <c>_DAT_009fd5d0 = 1/_DAT_009fd5c0</c> — the two cancel exactly, so
    /// the fade distance is plain metres along the camera's forward axis. (On the software path
    /// <c>_DAT_009fd5d0</c> is 1.0 instead and the distances come out scaled by that constant; we
    /// have no software path, the same call this project already makes for <c>PRIORITY</c>'s
    /// <c>K</c>.) Measuring euclidean range instead would fade a puffer at the screen edge sooner
    /// than one dead ahead, which the original does not do.</para>
    ///
    /// <para><b>⚠ The near ramp reads the FAR band's origin, and that is not a typo here.</b>
    /// <c>0054e7b5</c> is <c>FSUB [ESI + 0x3c]</c> — particle <c>[0xf]</c>, i.e.
    /// <see cref="PufferState.FarFadeStart"/> — against the NEAR reciprocal at <c>ESI + 0x44</c>.
    /// Verified in raw assembly, not a decompiler artefact, and consistent with
    /// <c>FADE_RANGE</c> being the original field (flag <c>0x1000</c>) and <c>NEAR_FADE</c> bolted
    /// on later (flag <c>0x80000</c>) by copy-pasting the far-band line. It is reproduced rather
    /// than repaired: "fixing" it would make C3's <c>volcanosmoke</c> — the one puffer in the
    /// install that reaches this branch at all — fade in over 1→75 m where the original pops it in
    /// at 75 m, which is a silent divergence dressed as a bug fix.</para>
    ///
    /// <para><b>With the shipped data the near band is a CULL, never a partial alpha.</b> All five
    /// descending pairs make the ramp branch unreachable (a particle past
    /// <see cref="PufferState.NearFadeStart"/> is already past <see cref="PufferState.NearFadeEnd"/>
    /// too, so it takes the alpha-1 exit), and <c>volcanosmoke</c>'s lone ascending
    /// <c>(1, 75)</c> reaches the ramp only for the cross-wire above to drive its alpha negative —
    /// which the <c>alpha &gt; 0</c> gate then culls. That is why <c>puffer.nearCull</c> is named a
    /// cull and defaults on: it is an artifact guard against a screen-filling billboard when the
    /// camera flies through an emitter, not a performance budget.</para>
    ///
    /// <para>The three switches are three mechanisms, not one boolean: <c>_distanceFade</c> is the
    /// authored far ramp, <c>_farCull</c> the hard discard past the band, <c>_nearCull</c> the near
    /// discard. ⚠ Note that turning <c>_farCull</c> off ALONE changes nothing visible — the
    /// authored ramp reaches zero at exactly the cutoff distance, so the two are the same line and
    /// the <c>alpha &gt; 0</c> gate removes what the cull would have. Keeping distant puffers drawn
    /// on modern hardware takes <c>distanceFade:false</c> AND <c>farCull:false</c>
    /// together.</para></summary>
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

    /// <summary>Advances a DISTANCE_INTERVAL trail emitter to the followed node's new
    /// world position, emitting one sprite per interval of motion (with carry across
    /// frames) — the dense_firetrail smoke/fire trailing a damaged plane. The first
    /// call starts the trail; call every frame while the effect is on.</summary>
    private void TrailAdvance(Vector3 worldPos)
    {
        if (_state.DistanceInterval <= 0f)
            return;
        if (!_trailing)
        {
            TopLevel = true;             // particles live in world space, left behind the plane
            // Toggling TopLevel PRESERVES the global transform (Godot 4): a flying parent's
            // attitude would stick as this node's basis and yaw every "world-space" puff
            // around the world origin — kilometres off at a far-from-origin mission spawn
            // (the fly-mode damage-trail bug). Identity transform, not just zero position.
            GlobalTransform = Transform3D.Identity;
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
        // C9 — the original's teleport guard, verbatim: FUN_0054f8b0 adds the frame's motion
        // length to the interval accumulator only `if (len < 200.0)`, so a jump lays no puff line
        // along itself. The carried remainder is left alone and is by construction below one
        // interval, so the engine's `floor(accumulator/interval)` is 0 on that frame — which is
        // why skipping the whole emit here is the same behaviour and not a stronger rule.
        if (dist >= TeleportGuardMeters)
        {
            if (_teleportGuardCount++ == 0)
                Log.Info("world", $"puffer teleport guard tripped name={_state.Name} dist={dist:0.0}m guard={TeleportGuardMeters:0}m (first occurrence only; further ones counted silently)");
            return;
        }
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
    private void TrailEnd() => _trailing = false;

    /// <summary>Static-viewer variant of <see cref="TrailAdvance"/>: emits the trail's
    /// per-meter puffs AT a fixed world point, spending <paramref name="speedMps"/>
    /// meters of virtual motion per second — the damage lab's parked plane, whose
    /// panels burn in place (the puffs' own random velocity and growth make the
    /// stacked emissions read as a flickering fire). Same carry, pool and spawn
    /// path as the moving trail.</summary>
    private void TrailBurnAt(Vector3 worldPos, float dt, float speedMps)
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
        int count = (int)(_trailCarry / _state.DistanceInterval);
        for (int k = 0; k < count; k++)
            SpawnTrailPuff(worldPos);
        _trailCarry -= count * _state.DistanceInterval;
    }

    /// <summary>
    /// Continuous emission at a moving world point — the third emission mode, alongside the
    /// one-shot <see cref="Burst"/> and the distance-driven <see cref="TrailAdvance"/>. This
    /// is what an animation's <c>PUFFER_STATE … ACTIVE_STATE 1</c> asks for: emit
    /// <c>NUMBER</c> sprites every <c>TIME_INTERVAL</c>, indefinitely, spread along the
    /// emitter's own motion since the previous call rather than stacked on today's pose — a
    /// fast host (the C1 train's smokestack) lays a continuous line instead of a clump per
    /// frame (<c>FUN_0054f8b0</c>: batch <c>k</c> of <c>count</c> spawns at
    /// <c>frac = (k+1)·interval / accumulator</c> along <c>prevOrigin → origin</c>, with the
    /// matching <c>(1 - frac)·dt</c> added to the drawn start age — B4's plumbing). Particles
    /// live in world space, so they are left behind rather than dragged along. Call every frame
    /// while the puffer is on; <see cref="SustainEnd"/> stops emission and lets the live
    /// particles decay.
    ///
    /// <para><b>No per-frame batch cap</b> (C9). <c>FUN_0054f8b0</c>'s batch loop runs the full
    /// <c>floor(accumulator / interval)</c>; a <c>MaxSustainBatchesPerFrame = 8</c> used to sit
    /// here, and it came out for three measured reasons. (1) It was not a hitch guard in practice:
    /// it never fired once across the 8-chapter regression, while it bound EVERY FRAME at 60 fps
    /// on the one puffer in the install that authors a 1 ms interval (<c>torpufferblast</c>, the
    /// torpedo trail, 8 compiled events) — where the engine emits 16 batches a frame and the cap
    /// emitted 8, a permanent halving wearing a safeguard's name. (2) On a real hitch it was
    /// actively WRONG: capped, a 5 s frame emits nothing and then delivers a full pool over the
    /// following frames, whereas the engine's 5 s frame emits exactly the tail of batches young
    /// enough to still be alive and returns to normal next frame. (3) The pool blowout it was
    /// written against is prevented twice over without it — the born-dead skip in
    /// <see cref="SpawnSustained"/> discards a hitch's older catch-up batches before they take a
    /// slot, and the spawn path clamps at the pool. The trade it leaves: the loop's ITERATION
    /// count is now unbounded in <c>dt</c> (the OUTPUT is still bounded by the pool), so a
    /// pathological frame delta buys a proportional one-frame cost. Bounding it faithfully is
    /// possible — a batch whose age offset alone already exceeds <c>LIFETIME_RANGE</c> max can
    /// never produce a live particle — but hoisting the born-dead test out of the spawn would
    /// change which draws the <c>_rng</c> stream makes, which is pinned (see the class remark), so
    /// it is left undone until a hitch measurably costs something.</para>
    /// </summary>
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
            // stale point, exactly as TrailAdvance's own homing rule (the rocket-explosion
            // ghost-trail fix, commit 450131a): without this a revive after Stop() would draw
            // a line of puffs from wherever the emitter last was.
            _sustainPrevOrigin = origin;
        }
        _sustainCarry += dt;
        float interval = Mathf.Max(_state.TimeInterval, 1e-3f);
        float accumulator = _sustainCarry;
        // C9: the FULL floor(accumulator / interval), uncapped, exactly as FUN_0054f8b0's batch
        // loop runs it. There used to be a MaxSustainBatchesPerFrame = 8 here, defending against a
        // hitch filling the pool; it is gone, and the reasons are in this method's remark.
        int batches = (int)(accumulator / interval);
        // The age offset rides the RAW dt, exactly as FUN_0054f8b0 does — deliberately unclamped.
        // On a long frame the early batches are handed an offset of seconds, which is physically
        // what they are: a batch whose virtual emission moment was 4.8 s ago really is 4.8 s old,
        // and a puffer with a 1 s LIFETIME_RANGE really did emit and bury it inside the hitch.
        // That is precisely why the engine carries the born-dead skip in its spawn (see
        // SpawnSustained), and clamping the term here to hold such batches alive would be an
        // invented divergence dressed as a bound.
        for (int b = 0; b < batches; b++)
        {
            float frac = (b + 1) * interval / accumulator;
            var spawnOrigin = _sustainPrevOrigin.Lerp(origin, frac);
            SpawnSustained(spawnOrigin, worldBasis, (1f - frac) * dt);
        }
        _sustainCarry -= batches * interval;
        _sustainPrevOrigin = origin;
    }

    /// <summary>Stops sustained emission; live particles finish their lifetimes.</summary>
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
        // C7. Every default reproduces the original; see the fields' remark and DistanceAlpha.
        _distanceFade = fade?.DistanceFade ?? Config.GetBool("puffer.distanceFade", true);
        _farCull = fade?.FarCull ?? Config.GetBool("puffer.farCull", true);
        _nearCull = fade?.NearCull ?? Config.GetBool("puffer.nearCull", true);
        _globalFadeFactor = fade?.GlobalFadeFactor
            ?? Config.GetFloat("puffer.globalFadeFactor", GlobalFadeFactorDefault);
        _nearRecip = Reciprocal(state.NearFadeEnd - state.NearFadeStart);
        _farRecip = Reciprocal(state.FarFadeEnd - state.FarFadeStart);
        _priorityFactor = 1f + PriorityScaleDefault * state.Priority;
        if (string.Equals(state.Name, FirePufferName, StringComparison.OrdinalIgnoreCase))
        {
            _fireRiseScale = Config.GetFloat("puffer.fireRiseScale", FireRiseScaleDefault);
            _fireLifeScale = Config.GetFloat("puffer.fireLifetimeScale", FireLifetimeScaleDefault);
        }
        if (state.DistanceInterval > 0f)
        {
            // Distance states use the trail pool even on the sustained (runtime) path: their
            // population is speed × lifetime / interval — ~112 live for short_firetrail's
            // shortpuffer1 at flight speed — and the time-cadence steady-state formula below
            // sized them at its 16-particle floor, silently dropping ~85% of the authored
            // emission (Emit routes them through SpawnTrailPuff, which stops at the pool).
            _particles = new Particle[TrailPool];
        }
        else if (sustained)
        {
            // The scaled lifetime raises the steady-state population with it, or the tuned fire
            // would silently drop its extra puffs at the old pool's edge.
            int steady = Mathf.CeilToInt(state.Number * state.LifetimeMax * _fireLifeScale
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

    /// <summary><paramref name="origin"/> is already the emitter's world point for this batch —
    /// the AT_NODE offset applied and, since B5, interpolated along the emitter's motion since
    /// the previous frame — <see cref="SustainAt"/> owns both. <paramref name="ageOffset"/> is
    /// B5's <c>(1 - frac) · dt</c> sub-frame correction: added to the drawn start age regardless
    /// of whether this state authors <c>START_AGE_RANGE</c>, since the term is independent of
    /// that key (a puffer with no start-age range still gets it, seeding <c>Particle.Age</c>
    /// with the offset alone instead of the literal 0f B4 replaced).</summary>
    private void SpawnSustained(Vector3 origin, Basis worldBasis, float ageOffset)
    {
        var min = _state.MinRandomVelocity;
        var max = _state.MaxRandomVelocity;
        // LOCAL_VELOCITY is in the emitter node's frame (the smokestack's "up"); WORLD_VELOCITY
        // is not. Rotating the local part is what keeps a banking/turning emitter correct.
        var baseVel = worldBasis * _state.LocalVelocity + _state.WorldVelocity;
        float d = _state.DeviationDistance;
        for (int k = 0; k < _state.Number && _liveCount < _particles.Length; k++)
        {
            // Draw order (pos → vel → size → life) is deliberately the historical one: every
            // sustained emitter shares it, and reordering the draws re-scatters ALL of them —
            // measured as the c1-waterfall golden moving with the fire tune inert there.
            // ±0.5·d, not ±d: FUN_0054f8b0 spawns at prev + delta*frac + (rand01 - 0.5)*d, i.e.
            // rand01 in [0,1) recentred on 0 gives a HALF-width offset (A2). Keep one Rand() draw
            // per axis — changing the draw count re-scatters every sustained emitter too.
            var pos = origin + new Vector3(Rand(-0.5f * d, 0.5f * d), Rand(-0.5f * d, 0.5f * d), Rand(-0.5f * d, 0.5f * d));
            var vel = baseVel + new Vector3(Rand(min.X, max.X), Rand(min.Y, max.Y), Rand(min.Z, max.Z));
            // The fire tune: scale the world-vertical rise (and the puff's lifetime below)
            // of the fire family only — 1 for every other emitter, so this is the identity there.
            vel.Y *= _fireRiseScale;
            float size = Rand(_state.SizeMin, _state.SizeMax) * _sustainSizeScale * _priorityFactor;
            float life = Rand(_state.LifetimeMin, _state.LifetimeMax) * _fireLifeScale;
            // START_AGE_RANGE (B4): FUN_0054f8b0 draws lifetime, then start age, both before
            // its position draws. Ours draws pos/vel first (A2's order, load-bearing for every
            // other puffer), so the closest match is start age immediately after life, right
            // where it already sat as the literal 0f this replaces. Gated on HasStartAgeRange
            // so the ~2,900 puffers that don't author the key draw nothing extra here and stay
            // bit-identical; only the 4 that do (their own particles re-scatter from here on,
            // which is expected). B5 adds ageOffset on top, unconditionally — it is computed,
            // not drawn, so it costs no extra _rng call and applies even to the ~2,900 puffers
            // with no START_AGE_RANGE at all.
            float age = (_state.HasStartAgeRange ? Rand(_state.StartAgeMin, _state.StartAgeMax) : 0f) + ageOffset;
            float frame = _state.TextureSequence.Count > 0 ? 0f
                : Mathf.Min(_state.Textures.Count - 1,
                    Mathf.FloorToInt((float)_rng.NextDouble() * _state.Textures.Count));
            // The engine's born-dead skip (FUN_0054f8b0: `if (age0 >= life)` → no particle is
            // created at all). Every draw above has already been made, so a skipped particle
            // consumes the same _rng stream a created one would — the skip changes what is
            // stored, never the scatter of its neighbours. It consumes no pool slot either:
            // _liveCount is not advanced, and the k-loop goes on to try the rest of NUMBER.
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

    private void SpawnTrailPuff(Vector3 worldPos)
    {
        if (_liveCount >= _particles.Length)
            return; // pool exhausted — oldest puffs finish before new ones spawn
        var min = _state.MinRandomVelocity;
        var max = _state.MaxRandomVelocity;
        float d = _state.DeviationDistance;
        // ±0.5·d, not ±d (A2) — see SpawnSustained's comment.
        var pos = worldPos + new Vector3(Rand(-0.5f * d, 0.5f * d), Rand(-0.5f * d, 0.5f * d), Rand(-0.5f * d, 0.5f * d));
        var vel = _state.WorldVelocity + new Vector3(Rand(min.X, max.X), Rand(min.Y, max.Y), Rand(min.Z, max.Z));
        float size = Rand(_state.SizeMin, _state.SizeMax) * _trailSizeScale * _priorityFactor;
        float life = Rand(_state.LifetimeMin, _state.LifetimeMax);
        // START_AGE_RANGE (B4): see SpawnSustained's comment — gated draw, right after Life.
        float age = _state.HasStartAgeRange ? Rand(_state.StartAgeMin, _state.StartAgeMax) : 0f;
        float frame = _state.TextureSequence.Count > 0 ? 0f
            : Mathf.Min(_state.Textures.Count - 1, Mathf.FloorToInt((float)_rng.NextDouble() * _state.Textures.Count));
        // The engine's born-dead skip — see SpawnSustained. There is no sub-frame term on this
        // path (the trail walks distance, not virtual time), so here it can only fire on an
        // authored START_AGE_RANGE reaching a drawn lifetime. Reproduced anyway: the guard is
        // the engine's, not a property of today's data.
        if (age >= life)
            return;
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
            // ±0.5·d, not ±d (A2) — see SpawnSustained's comment.
            var pos = new Vector3(Rand(-0.5f * d, 0.5f * d), Rand(-0.5f * d, 0.5f * d), Rand(-0.5f * d, 0.5f * d));
            var vel = baseVel + new Vector3(Rand(min.X, max.X), Rand(min.Y, max.Y), Rand(min.Z, max.Z));
            float size = Rand(_state.SizeMin, _state.SizeMax) * _burstSizeScale * _priorityFactor;
            float life = Rand(_state.LifetimeMin, _state.LifetimeMax);
            // START_AGE_RANGE (B4): see SpawnSustained's comment — gated draw, right after Life.
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

    /// <summary>Latest flipbook frame whose keyed time has been reached (times ascending).
    /// The key is a FRACTION of the particle's own lifetime, not a second count: no
    /// TEXTURE_SEQUENCE in the install keys a frame past 0.8, across lifetimes from 0.2 s to
    /// 5.5 s, and the mag_gunhit firepuffers key frames out to 0.5 with a 0.1–0.2 s lifetime —
    /// which under a seconds reading could never draw at all. Read as seconds, a 5 s
    /// fire_n_smoke particle burned fire_f01→f06 in a quarter second and then held the near-black
    /// smoke frame for its remaining 95%, which is what collapsed large_30sec_fire into a
    /// stationary ball instead of a climbing flame.</summary>
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
