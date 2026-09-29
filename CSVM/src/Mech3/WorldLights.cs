using System;
using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Packs the world's <c>LIGHT_STATE</c> point lights, and those of <see cref="AddSource"/>'s
/// runtimes (bursts, fires), into a data texture: the per-vertex point term on `lighting: true`
/// models. In original mode that term is the only consumer, since the unshaded world ignores a
/// real <see cref="OmniLight3D"/> (docs/formats/gotchas.md's fullbright entry). Given a parent in
/// enhanced mode, it also mirrors the same rank onto real omnis, up to the effects level's budget.
/// </summary>
public sealed class WorldLights : IDisposable
{
    /// <summary>Rows in the data texture, the most lights that can reach a vertex at once. The
    /// shader loops over <c>csky_light_count</c> per vertex, so this is a real per-vertex cost
    /// bound, not just an allocation. Measured peak in this install is well under it (logged
    /// at build as "world lights: … peak N"), so nearest-N never actually drops one.</summary>
    public const int MaxActive = 16;

    /// <summary>The falloff exponent of every point omni that stands for an authored light: this
    /// pool, the wing-tip flares and the muzzle flashes. Godot's default 1.0 adds an
    /// inverse-distance term that spends a 20 m lamp within a few metres, where the original's
    /// weight is still 1. At 0 only Godot's range window remains, which <see cref="OmniRange"/>
    /// lines up with the authored linear ramp.</summary>
    public const float OmniAttenuation = 0.0f;

    // TUNE: how far above the hit a burst light (AddBurst) stands, in metres. At the hit itself flat
    // ground takes it edge-on, where a lit surface's diffuse term is zero however bright the light.
    internal const float BurstLift = 30f;

    // The data's lights are SMALL, every startup light in this install has a range_max between
    // 2 and 22 m, so one seen from far enough away is a sub-pixel smudge that the mission's fog
    // has already washed out. Rather than hard-cull at a radius (which pops), the contribution
    // fades to nothing between these two distances and only then leaves the set. That is what
    // makes the MaxActive bound safe: the lights nearest-N drops are ones already at ~0.
    // Both TUNE, chosen against C1's spread (34 live, at most 13 within 600 m of each other).
    private const float FadeStart = 900f;
    private const float FadeEnd = 1500f;

    private const string DataParam = "csky_light_data";
    private const string CountParam = "csky_light_count";

    // 4 floats per texel, 2 texels per light.
    private const int FloatsPerLight = 8;

    // TUNE: multiplies the committed colour's peak channel into OmniLight3D.LightEnergy. The
    // shader's term is a factor on a vertex colour, not a physical unit with an energy scale to
    // copy, so this is anchored at the controls against a beacon lighting the fuselage without
    // blowing it out.
    private const float OmniEnergyScale = 4.0f;


    // ---- the enhanced-only burst light (AddBurst), one TUNE block ----
    // Each burst's colour, peak, reach and decay are its BurstShape (EffectCatalogue.BurstLightShapes).
    // What every burst shares is here. The inner radius is the authored `he_light` ramp's ignition
    // min in `he_ground_effect` (docs/org/ordnanceTypes.md).
    private const float BurstRangeMin = 4f;

    // The gain a burst is retired at. Below it the light moves no pixel and only holds a slot the
    // distance fade and the MaxActive rank would rather give a light that does.
    private const float BurstFloor = 0.05f;

    // The flicker's rate in Hz and its depth about the decay curve. Slow enough that a 60 Hz frame
    // samples several points per cycle, since a faster one aliases into noise, and shallow enough
    // never to reach zero, since a light that goes dark mid-burst reads as a strobe, not as fire.
    private const float BurstFlickerHz = 9f;
    private const float BurstFlickerDepth = 0.25f;

    // The second flicker sine's rate as a multiple of the first. Deliberately not a whole number,
    // so the pair does not repeat inside one burst and the flicker reads as fire rather than as a
    // regular pulse.
    private const float BurstFlickerBeat = 1.73f;

    // Cycles of flicker phase between one burst and the next. A salvo landing in the same frame
    // would otherwise pulse in lockstep and read as one light rather than several.
    private const float BurstPhaseStride = 0.618f;

    // ---- the enhanced-only fire light (AddFire), one TUNE block ----

    // The gain on FireColor at full strength, well under a burst's peak, since a fire lights its
    // surroundings for as long as it burns.
    private const float FireGain = 1.5f;

    // The reach per metre of mean fire sprite size, and the bounds on it. A fire's pool of light
    // grows with the fire, but a single sprite never lights a street and a trail puff never a hall.
    private const float FireReachPerSize = 2.5f;
    private const float FireRangeMin = 8f;
    private const float FireRangeMax = 45f;

    // The live fire particles at which a fire reaches full strength. A dying fire's last embers
    // dim its light rather than holding it at full until the final frame.
    private const float FireFullCount = 4f;

    // How far above the fire's centroid its light stands, as a fraction of its reach. The ground
    // around a fire then takes it at an angle rather than edge-on (see BurstLift).
    private const float FireLiftFraction = 0.25f;

    // A fire's weight in the rank against authored and burst lights. At equal significance the fire
    // loses, so a crowd of fires never takes a beacon's slot.
    private const float FireRankWeight = 0.25f;

    // The Enhanced omni budget at each effects level. The data texture keeps MaxActive whatever the
    // level, so the faithful shader and its goldens never move. Each omni is shaded per pixel.
    private const int OmniBudgetHigh = 64;
    private const int OmniBudgetMedium = 48;
    private const int OmniBudgetLow = 32;

    // TUNE: a fire light's colour in the data's sRGB, a warm orange a little redder than `he_light`.
    private static readonly Color FireColor = new(1f, 0.68f, 0.235f);

    // Where Godot's range window, (1 - (d/r)^4)^2 at exponent 0, falls to half, as a fraction of
    // the range. The authored ramp is at half midway between its near and far range.
    private static readonly float HalfWeightFraction = Mathf.Pow(1f - Mathf.Sqrt(0.5f), 0.25f);

    private readonly List<Entry> _pending = new();
    private readonly byte[] _buffer = new byte[MaxActive * FloatsPerLight * sizeof(float)];
    private readonly List<Vector3> _committedPositions = new();
    private readonly List<Color> _committedFactors = new();
    private readonly List<OmniLight3D> _omniPool = new();
    private readonly List<Burst> _bursts = new();

    // Runtimes that submit into this set without owning its frame: the world runtime owns
    // Begin/Commit, and a second Begin/Commit pair on the same set would erase its lights.
    private readonly List<Action<WorldLights>> _sources = new();

    // Non-null only in enhanced mode with a parent given; null keeps original mode's point term
    // the only consumer and creates not one node, per the mode's zero-footprint contract.
    private readonly Node3D? _omniParent;

    // The most omnis the Enhanced pool lights at once, never below MaxActive.
    private readonly int _omniBudget;

    private ImageTexture? _texture;
    private int _lastCount = -1;
    private int _loggedSubmitted = -1;
    private int _loggedOmnis = -1;
    private int _burstsRegistered;

    // Seconds of sim time since the world began, the fire lights' flicker clock.
    private float _fireClock;

    /// <summary>Enhanced mode only: mirrors the ranked set onto real <see cref="OmniLight3D"/> nodes
    /// under <paramref name="parent"/>, up to <paramref name="omniBudget"/> of them. Null takes the
    /// configured effects level's <see cref="OmniBudget"/>. Original mode ignores both and spawns
    /// nothing, leaving the data-texture point term exactly as it was.</summary>
    public WorldLights(Node3D? parent = null, int? omniBudget = null)
    {
        _omniParent = GraphicsMode.Enhanced ? parent : null;
        _omniBudget = Math.Max(MaxActive, omniBudget ?? OmniBudget(Config.GetString(EffectsLevel.Key, EffectsLevel.Default)));
    }

    /// <summary>Highest simultaneous count seen, reported so the MaxActive bound can be
    /// checked against real data rather than assumed.</summary>
    public int PeakCount { get; private set; }

    /// <summary>Lights active this frame before the fade and budget are applied (diagnostics).</summary>
    public int LiveCount { get; private set; }

    /// <summary>The positions actually packed into the shader texture by the last
    /// <see cref="Commit"/>, never read by anything that draws (the shader reads the texture,
    /// not this); it exists so the nearest-viewer budget can be asserted directly
    /// instead of decoding the packed texture back out.</summary>
    public IReadOnlyList<Vector3> CommittedPositions => _committedPositions;

    /// <summary>The factor colours packed beside <see cref="CommittedPositions"/>, index for
    /// index, after the distance fade: diagnostics only, like the positions.</summary>
    public IReadOnlyList<Color> CommittedFactors => _committedFactors;

    /// <summary>Enhanced mode: the omnis the last <see cref="Commit"/> lit, which can exceed
    /// <see cref="MaxActive"/> up to the effects level's budget. Diagnostics, like the positions.
    /// </summary>
    public int OmniCount { get; private set; }

    /// <summary>Registers the global shader parameters the world shader references. Called once
    /// per process, before any material using them is built, the defaults (no texture, count 0)
    /// are a no-op, so a session that never animates a light renders exactly as before.</summary>
    public static void RegisterGlobals()
    {
        RenderingServer.GlobalShaderParameterAdd(DataParam,
            RenderingServer.GlobalShaderParameterType.Sampler2D, default);
        RenderingServer.GlobalShaderParameterAdd(CountParam,
            RenderingServer.GlobalShaderParameterType.Int, 0);
    }

    /// <summary>The enhanced omni range that stands in for an authored near/far pair. Godot's
    /// window is at half weight where the authored linear ramp is, midway between the two. Inside
    /// the near range both are close to full weight.</summary>
    public static float OmniRange(float rangeMin, float rangeMax) =>
        0.5f * (rangeMin + rangeMax) / HalfWeightFraction;

    /// <summary>The Enhanced omni budget an effects level word sets (see
    /// <see cref="EffectsLevel"/>); a word the original does not know takes the default level's.
    /// </summary>
    public static int OmniBudget(string level) => level.Trim().ToLowerInvariant() switch
    {
        "medium" => OmniBudgetMedium,
        "low" => OmniBudgetLow,
        _ => OmniBudgetHigh,
    };

    /// <summary>Starts a frame's submission. Lights are re-submitted every frame because they
    /// ride moving hosts (a muzzle flash on a turret, the train's firebox). <paramref name="dt"/>
    /// is the sim step the live <see cref="AddBurst"/> lights age by; a caller with no burst
    /// lights may leave it 0.</summary>
    public void Begin(float dt = 0f)
    {
        _pending.Clear();
        _fireClock += dt;
        SubmitBursts(dt);
    }

    /// <summary>Enhanced mode only: a short-lived light at an explosion, brightest at ignition and
    /// flickering down to nothing over the TUNE envelope above. It is submitted like any other
    /// light, so the distance fade, the <see cref="MaxActive"/> rank and the omni pool treat it
    /// exactly as they treat a beacon. <paramref name="stillBurning"/> is the fireball's own
    /// liveness, read every frame: the light is dropped the frame it goes false, so no burst light
    /// can outlive the fireball that threw it.</summary>
    public void AddBurst(Vector3 pos, BurstShape shape, Func<bool> stillBurning)
    {
        if (!GraphicsMode.Enhanced)
            return;
        // Linearised once here, and the envelope scales the linear value: scaling the sRGB value
        // would bend both the decay and the flicker. The shader's term keeps the data's own value.
        _bursts.Add(new Burst(pos + (Vector3.Up * BurstLift), shape, shape.Color.SrgbToLinear(), stillBurning,
            _burstsRegistered++ * BurstPhaseStride));
    }

    /// <summary>Enhanced mode only: this frame's light for one burning fire emitter. A source submits
    /// it every frame the emitter burns. The light stands over the fire particles' centroid, reaches
    /// with their mean size and dims as their count falls. The phase keeps two fires out of step.
    /// It ranks below an authored or burst light of equal significance.</summary>
    public void AddFire(Vector3 centroid, float meanSize, int fireCount, float phase)
    {
        if (!GraphicsMode.Enhanced || fireCount <= 0)
            return;
        float reach = Mathf.Clamp(FireReachPerSize * meanSize, FireRangeMin, FireRangeMax);
        float gain = FireGain * Mathf.Min(1f, fireCount / FireFullCount) * Flicker(_fireClock, phase);
        _pending.Add(new Entry(centroid + (Vector3.Up * (FireLiftFraction * reach)), FireColor * gain,
            FireColor.SrgbToLinear() * gain, FireLiftFraction * reach, reach, reach, FireRankWeight));
    }

    /// <summary>Registers a submitter that <see cref="Commit"/> asks for its lights every frame,
    /// before the fade and the budget, so its lights rank against the owner's in one set.
    /// Registering the same delegate again is a no-op.</summary>
    public void AddSource(Action<WorldLights> submit)
    {
        if (!_sources.Contains(submit))
            _sources.Add(submit);
    }

    /// <summary>Submits one active light. <paramref name="color"/> is the data's own value and
    /// <paramref name="scalar"/> the light's ambient + diffuse (1 for a light authoring neither).
    /// The shader takes their product unconverted, as the factor the original adds to a vertex's
    /// light (docs/org/vertexLighting.md). The enhanced omnis take the colour linearised, times the
    /// same scalar, over <see cref="OmniRange"/>.</summary>
    public void Add(Vector3 pos, Color color, float rangeMin, float rangeMax, float scalar = 1f)
    {
        // A degenerate or inverted range would divide by zero in the shader's weight. The data
        // is well-formed (min < max everywhere surveyed), but LIGHT_ANIMATION tweens ranges by
        // signed deltas and can cross over mid-pulse.
        if (rangeMax <= rangeMin)
            rangeMax = rangeMin + 0.01f;
        _pending.Add(new Entry(pos, color * scalar, color.SrgbToLinear() * scalar, rangeMin, rangeMax,
            OmniRange(rangeMin, rangeMax), 1f));
    }

    /// <summary>Packs the frame's lights and uploads them. When more than
    /// <see cref="MaxActive"/> are live the nearest to any viewer win, the dropped ones are
    /// the farthest, whose pools are the smallest on screen in every pane.</summary>
    public void Commit(IReadOnlyList<Vector3> viewerPositions)
    {
        foreach (var submit in _sources)
            submit(this);
        LiveCount = _pending.Count;
        // Fade before sort, so the budget only ever drops lights already contributing nothing.
        // Distance is to the nearest viewer, never a single camera.
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            float fade = 1f - Mathf.SmoothStep(FadeStart, FadeEnd, NearestDistance(_pending[i].Pos, viewerPositions));
            if (fade <= 0f)
                _pending.RemoveAt(i);
            else if (fade < 1f)
                _pending[i] = _pending[i].Faded(fade);
        }
        int live = _pending.Count;
        if (live > PeakCount)
            PeakCount = live;
        if (live > MaxActive)
        {
            // Rank by angular size (range/distance), not raw distance, plain nearest-N would
            // drop a big flare in favor of an equally-far pinpoint. One sort decides both sets:
            // the texture takes its head, the Enhanced omnis a longer run of the same order.
            _pending.Sort((a, b) => Significance(b, viewerPositions).CompareTo(Significance(a, viewerPositions)));
        }
        int n = Math.Min(live, MaxActive);

        _committedPositions.Clear();
        _committedFactors.Clear();
        for (int i = 0; i < n; i++)
        {
            _committedPositions.Add(_pending[i].Pos);
            _committedFactors.Add(_pending[i].Factor);
        }

        if (_omniParent != null)
            UpdateOmnis(Math.Min(live, _omniBudget));

        if (n == 0)
        {
            // Nothing lit: leave the texture alone and zero the count, which short-circuits the
            // shader loop entirely.
            if (_lastCount != 0)
                RenderingServer.GlobalShaderParameterSet(CountParam, 0);
            _lastCount = 0;
            return;
        }

        Array.Clear(_buffer);
        for (int i = 0; i < n; i++)
        {
            var e = _pending[i];
            int o = i * FloatsPerLight * sizeof(float);
            Write(o, e.Pos.X); Write(o + 4, e.Pos.Y); Write(o + 8, e.Pos.Z); Write(o + 12, e.Max);
            Write(o + 16, e.Factor.R); Write(o + 20, e.Factor.G); Write(o + 24, e.Factor.B); Write(o + 28, e.Min);
        }

        // Packed by hand rather than via Image.SetPixel: positions are world metres (thousands,
        // and negative), and going through Godot's Color struct invites a clamp to 0..1.
        var image = Image.CreateFromData(2, MaxActive, false, Image.Format.Rgbaf, _buffer);
        if (_texture == null)
        {
            _texture = ImageTexture.CreateFromImage(image);
            RenderingServer.GlobalShaderParameterSet(DataParam, _texture);
        }
        else
        {
            _texture.Update(image);
        }
        if (_lastCount != n)
            RenderingServer.GlobalShaderParameterSet(CountParam, n);
        _lastCount = n;
    }

    /// <summary>--debug-anim: report the submitted/live counts when they change. A headless run
    /// reads from it whether the <see cref="MaxActive"/> bound is dropping a light near the camera.
    /// In enhanced mode it also reports <see cref="OmniCount"/>, the lit omnis, which run past the
    /// texture's count up to the effects level's budget.</summary>
    public void LogOnce()
    {
        if (_lastCount == _loggedSubmitted && OmniCount == _loggedOmnis)
            return;
        _loggedSubmitted = _lastCount;
        _loggedOmnis = OmniCount;
        Log.Info("world", $"anim/debug: world lights {_lastCount} rendered of {LiveCount} live{(_pending.Count > MaxActive ? $" (budget {MaxActive}; the rest are past the distance fade)" : "")}{(_omniParent != null ? $" (enhanced: {OmniCount} omni of budget {_omniBudget})" : "")}");
    }

    /// <summary>Drops the world's lights, called when a session is torn down, so the next
    /// world does not inherit the previous one's lights for a frame. Also frees every spawned
    /// omni: the harness shares one host across suites, so a leaked named node would break the
    /// next suite's spawn.</summary>
    public void Dispose()
    {
        _pending.Clear();
        _bursts.Clear();
        _sources.Clear();
        _committedPositions.Clear();
        _committedFactors.Clear();
        RenderingServer.GlobalShaderParameterSet(CountParam, 0);
        _lastCount = 0;
        OmniCount = 0;
        _texture = null;
        foreach (var omni in _omniPool)
            omni.Free();
        _omniPool.Clear();
    }

    // Enhanced mode: mirrors _pending[0..n) onto a pool of real OmniLight3D nodes. The pool grows
    // lazily up to the omni budget and hides the rest rather than respawning every frame. Each omni
    // takes its Entry's position, range and faded colour, so it dims out with the distance fade.
    private void UpdateOmnis(int n)
    {
        OmniCount = n;
        while (_omniPool.Count < n)
        {
            var omni = new OmniLight3D { ShadowEnabled = false, OmniAttenuation = OmniAttenuation };
            _omniParent!.AddChild(omni);
            _omniPool.Add(omni);
        }
        for (int i = 0; i < _omniPool.Count; i++)
        {
            var omni = _omniPool[i];
            if (i >= n)
            {
                omni.Visible = false;
                continue;
            }
            var e = _pending[i];
            float peak = Mathf.Max(e.Color.R, Mathf.Max(e.Color.G, e.Color.B));
            omni.GlobalPosition = e.Pos;
            omni.OmniRange = e.OmniRange;
            omni.LightColor = peak > 0f ? new Color(e.Color.R / peak, e.Color.G / peak, e.Color.B / peak) : Colors.White;
            omni.LightEnergy = OmniEnergyScale * peak;
            omni.Visible = true;
        }
    }

    // Ages the live burst lights, retires the ones whose fireball has ended or whose envelope is
    // spent, and submits the rest as ordinary lights. Done at Begin rather than at Commit so a
    // burst is in _pending before the frame's LIGHT_STATE lights are, i.e. it competes for the
    // MaxActive slots on the same terms instead of being appended after the budget is decided.
    private void SubmitBursts(float dt)
    {
        for (int i = _bursts.Count - 1; i >= 0; i--)
        {
            var burst = _bursts[i];
            burst.Age += dt;
            float gain = BurstGain(burst.Shape, burst.Age, burst.Phase);
            if (gain < BurstFloor || !burst.StillBurning())
            {
                _bursts.RemoveAt(i);
                continue;
            }
            // Straight into _pending rather than through Add: the colour is already linear and the
            // ranges are well-formed, so neither of Add's two conversions applies.
            _pending.Add(new Entry(burst.Pos, burst.Shape.Color * gain, burst.Color * gain,
                BurstRangeMin, burst.Shape.RangeMax, burst.Shape.RangeMax, 1f));
        }
    }

    // Kept beside Commit and UpdateOmnis, the two instance methods that call them, rather than
    // hoisted above every instance member for SA1204's sake (same local-suppression precedent as
    // CameraController.FirstPersonPose).
#pragma warning disable SA1204
    // The burst envelope: an ignition peak, an exponential decay and the flicker.
    private static float BurstGain(BurstShape shape, float age, float phase) =>
        shape.PeakGain * Mathf.Exp(-age / shape.Decay) * Flicker(age, phase);

    // A fire's flicker, shared by the bursts and the fire lights, never reaching zero. Two sines
    // keep it from reading as a regular pulse. Each light carries its own phase, so a salvo does
    // not flash in lockstep.
    private static float Flicker(float t, float phase)
    {
        float wave = Mathf.Tau * ((BurstFlickerHz * t) + phase);
        return 1f + (BurstFlickerDepth * 0.5f * (Mathf.Sin(wave) + Mathf.Sin(wave * BurstFlickerBeat)));
    }

    // Angular size of the light's pool, times its faded intensity and its rank weight. It is the
    // cheapest honest proxy for how much of the frame a light changes. Distance is to the nearest
    // viewer, matching Commit's own fade rule.
    private static float Significance(Entry e, IReadOnlyList<Vector3> viewerPositions) =>
        e.Max / Mathf.Max(NearestDistance(e.Pos, viewerPositions), 1f)
        * Mathf.Max(e.Color.R, Mathf.Max(e.Color.G, e.Color.B)) * e.Rank;

    // The closest of every live viewer, never the average or the first, so the fade and the
    // budget both answer to whichever pane is actually near, the same per-pane rule the puffer
    // fade also applies. Empty (no viewers) reads as "infinitely far", fading everything out.
    private static float NearestDistance(Vector3 pos, IReadOnlyList<Vector3> viewerPositions)
    {
        float best = float.MaxValue;
        for (int i = 0; i < viewerPositions.Count; i++)
        {
            float d = pos.DistanceTo(viewerPositions[i]);
            if (d < best)
                best = d;
        }
        return best;
    }
#pragma warning restore SA1204

    private void Write(int offset, float value) =>
        BitConverter.TryWriteBytes(_buffer.AsSpan(offset, sizeof(float)), value);

    /// <summary>One kind of burst light, Enhanced Graphics only. It holds the colour in the data's
    /// sRGB, the gain on it at ignition, the reach in metres and the decay's e-folding time in
    /// seconds. A remake envelope with no counterpart in the data; the values per effect are
    /// <c>EffectCatalogue.BurstLightShapes</c>.</summary>
    public readonly record struct BurstShape(Color Color, float PeakGain, float RangeMax, float Decay);

    private readonly struct Entry
    {
        public readonly Vector3 Pos;
        public readonly Color Factor; // the shader's term: authored colour x ambient + diffuse
        public readonly Color Color;  // linear, the enhanced omni's
        public readonly float Min, Max;
        public readonly float OmniRange; // the enhanced omni's
        public readonly float Rank;      // the weight on its significance, below 1 for a fire
        public Entry(Vector3 pos, Color factor, Color color, float min, float max, float omniRange, float rank)
        {
            Pos = pos; Factor = factor; Color = color; Min = min; Max = max; OmniRange = omniRange; Rank = rank;
        }

        /// <summary>The same light dimmed by the distance fade, the colour is the intensity,
        /// so scaling it is how a light leaves the set without popping.</summary>
        public Entry Faded(float f) => new(Pos, Factor * f, Color * f, Min, Max, OmniRange, Rank);
    }

    // One live burst light. A class rather than a struct because Age is written every frame, and
    // the omni's colour is held already linearised (see AddBurst).
    private sealed class Burst
    {
        public Burst(Vector3 pos, BurstShape shape, Color color, Func<bool> stillBurning, float phase)
        {
            Pos = pos; Shape = shape; Color = color; StillBurning = stillBurning; Phase = phase;
        }

        public Vector3 Pos { get; }

        public BurstShape Shape { get; }

        public Color Color { get; }

        public Func<bool> StillBurning { get; }

        public float Phase { get; }

        public float Age { get; set; }
    }
}
