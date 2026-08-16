using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Packs the animated world's <c>LIGHT_STATE</c> point lights into a data texture the fullbright
/// world shader reads as spill onto nearby geometry — the flare itself is separate gamez geometry
/// <see cref="SceneBuilder"/> already draws.
/// ⚠ Not real <see cref="OmniLight3D"/> nodes: the world renders unshaded, so a dynamic light
/// contributes nothing to it (docs/formats/gotchas.md's fullbright entry).
/// Packing layout, the multi-viewer fade rule and <c>BL-366</c>: this module's docs/architecture.md
/// entry.
/// </summary>
public sealed class WorldLights : IDisposable
{
    /// <summary>Rows in the data texture — the most lights that can spill at once. The shader
    /// loops over <c>csky_light_count</c> fragments-wide, so this is a real per-fragment cost
    /// bound, not just an allocation. Measured peak in this install is well under it (logged
    /// at build as "world lights: … peak N"), so nearest-N never actually drops one.</summary>
    public const int MaxActive = 16;

    // The data's lights are SMALL — every startup light in this install has a range_max between
    // 2 and 22 m — so one seen from far enough away is a sub-pixel smudge that the mission's fog
    // has already washed out. Rather than hard-cull at a radius (which pops), the contribution
    // fades to nothing between these two distances and only then leaves the set. That is what
    // makes the MaxActive bound safe: the lights nearest-N drops are ones already at ~0.
    // Both TUNE — chosen against C1's spread (34 live, at most 13 within 600 m of each other).
    private const float FadeStart = 900f;
    private const float FadeEnd = 1500f;

    private const string DataParam = "csky_light_data";
    private const string CountParam = "csky_light_count";

    // 4 floats per texel, 2 texels per light.
    private const int FloatsPerLight = 8;

    private readonly List<Entry> _pending = new();
    private readonly byte[] _buffer = new byte[MaxActive * FloatsPerLight * sizeof(float)];
    private readonly List<Vector3> _committedPositions = new();
    private ImageTexture? _texture;
    private int _lastCount = -1;
    private int _loggedSubmitted = -1;

    /// <summary>Highest simultaneous count seen — reported so the MaxActive bound can be
    /// checked against real data rather than assumed.</summary>
    public int PeakCount { get; private set; }

    /// <summary>Lights active this frame before the fade and budget are applied (diagnostics).</summary>
    public int LiveCount { get; private set; }

    /// <summary>The positions actually packed into the shader texture by the last
    /// <see cref="Commit"/> — never read by anything that draws (the shader reads the texture,
    /// not this); it exists so the nearest-viewer budget (`BL-366`) can be asserted directly
    /// instead of decoding the packed texture back out.</summary>
    public IReadOnlyList<Vector3> CommittedPositions => _committedPositions;

    /// <summary>Registers the global shader parameters the world shader references. Called once
    /// per process, before any material using them is built — the defaults (no texture, count 0)
    /// are a no-op, so a session that never animates a light renders exactly as before.</summary>
    public static void RegisterGlobals()
    {
        RenderingServer.GlobalShaderParameterAdd(DataParam,
            RenderingServer.GlobalShaderParameterType.Sampler2D, default);
        RenderingServer.GlobalShaderParameterAdd(CountParam,
            RenderingServer.GlobalShaderParameterType.Int, 0);
    }

    /// <summary>Starts a frame's submission. Lights are re-submitted every frame because they
    /// ride moving hosts (a muzzle flash on a turret, the train's firebox).</summary>
    public void Begin() => _pending.Clear();

    /// <summary>Submits one active light. <paramref name="color"/> is the data's own sRGB
    /// value; it is linearised here, since the world shader works in linear space (the same
    /// conversion GameSession applies to FOG_COLOR).</summary>
    public void Add(Vector3 pos, Color color, float rangeMin, float rangeMax)
    {
        // A degenerate or inverted range would make the shader's smoothstep undefined. The data
        // is well-formed (min < max everywhere surveyed), but LIGHT_ANIMATION tweens ranges by
        // signed deltas and can cross over mid-pulse.
        if (rangeMax <= rangeMin)
            rangeMax = rangeMin + 0.01f;
        _pending.Add(new Entry(pos, color.SrgbToLinear(), rangeMin, rangeMax));
    }

    /// <summary>Packs the frame's lights and uploads them. When more than
    /// <see cref="MaxActive"/> are live the nearest to any viewer win — the dropped ones are
    /// the farthest, whose pools are the smallest on screen in every pane.</summary>
    public void Commit(IReadOnlyList<Vector3> viewerPositions)
    {
        LiveCount = _pending.Count;
        // Fade before sort, so the budget only ever drops lights already contributing nothing.
        // Distance is to the nearest viewer (BL-366), never a single camera.
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            float fade = 1f - Mathf.SmoothStep(FadeStart, FadeEnd, NearestDistance(_pending[i].Pos, viewerPositions));
            if (fade <= 0f)
                _pending.RemoveAt(i);
            else if (fade < 1f)
                _pending[i] = _pending[i].Faded(fade);
        }
        int n = _pending.Count;
        if (n > PeakCount)
            PeakCount = n;
        if (n > MaxActive)
        {
            // Rank by angular size (range/distance), not raw distance — plain nearest-N would
            // drop a big flare in favor of an equally-far pinpoint.
            _pending.Sort((a, b) => Significance(b, viewerPositions).CompareTo(Significance(a, viewerPositions)));
            n = MaxActive;
        }

        _committedPositions.Clear();
        for (int i = 0; i < n; i++)
            _committedPositions.Add(_pending[i].Pos);

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
            Write(o + 16, e.Color.R); Write(o + 20, e.Color.G); Write(o + 24, e.Color.B); Write(o + 28, e.Min);
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

    /// <summary>--debug-anim: report the submitted/live counts when they change. This is how a
    /// headless run shows whether the <see cref="MaxActive"/> bound is actually binding (i.e.
    /// whether any light near the camera is being dropped) rather than assuming it isn't.</summary>
    public void LogOnce()
    {
        if (_lastCount == _loggedSubmitted)
            return;
        _loggedSubmitted = _lastCount;
        GD.Print($"anim/debug: world lights {_lastCount} rendered of {LiveCount} live"
                 + (_pending.Count > MaxActive ? $" (budget {MaxActive}; the rest are past the distance fade)" : ""));
    }

    /// <summary>Drops the world's lights — called when a session is torn down, so the next
    /// world does not inherit the previous one's spill for a frame.</summary>
    public void Dispose()
    {
        _pending.Clear();
        _committedPositions.Clear();
        RenderingServer.GlobalShaderParameterSet(CountParam, 0);
        _lastCount = 0;
        _texture = null;
    }

    // Angular size of the light's pool, scaled by its (already fade-applied) intensity — the
    // cheapest honest proxy for "how much of this frame does it change". Distance is to the
    // nearest viewer, matching Commit's own fade rule.
    private static float Significance(Entry e, IReadOnlyList<Vector3> viewerPositions) =>
        e.Max / Mathf.Max(NearestDistance(e.Pos, viewerPositions), 1f)
        * Mathf.Max(e.Color.R, Mathf.Max(e.Color.G, e.Color.B));

    // The closest of every live viewer — never the average or the first — so the fade and the
    // budget both answer to whichever pane is actually near, the same per-pane rule B11 applies
    // to the puffer fade. Empty (no viewers) reads as "infinitely far", fading everything out.
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

    private void Write(int offset, float value) =>
        BitConverter.TryWriteBytes(_buffer.AsSpan(offset, sizeof(float)), value);

    private readonly struct Entry
    {
        public readonly Vector3 Pos;
        public readonly Color Color;
        public readonly float Min, Max;
        public Entry(Vector3 pos, Color color, float min, float max)
        {
            Pos = pos; Color = color; Min = min; Max = max;
        }

        /// <summary>The same light dimmed by the distance fade — the colour is the intensity,
        /// so scaling it is how a light leaves the set without popping.</summary>
        public Entry Faded(float f) => new(Pos, Color * f, Min, Max);
    }
}
