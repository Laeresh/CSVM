using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// The enhanced presentation's scorch marks: a pool of <see cref="Decal"/> nodes sharing one
/// procedural radial burn texture, dropped where ordnance burned the ground and faded out over
/// their own life. A remake-only layer OVER the carve, never instead of it: the bowl
/// (<see cref="CraterField"/>) is the original's own terrain surgery and is untouched here.
/// <see cref="Create"/> returns null on the faithful path, so that build holds no pool, no node
/// and no generated texture. A decal reaches only what the forward pass shades, which under
/// Enhanced is every world surface its source authored <c>lighting: true</c> (the bias shader in
/// <c>Mech3/SceneBuilder.cs</c>). ⚠ Every size, darkness and life constant below is TUNE, judged
/// at the controls.
/// </summary>
public sealed partial class ScorchField : Node3D
{
    // --- TUNE: the mark, pending the user's judgement at the controls ---

    // How many marks may stand at once. The crater field keeps NO count cap to copy: its record
    // list only grows and its bound is the refusal (docs/org/craters.md). The scorch borrows that
    // refusal instead (MergeFraction below) and caps the pool at twice the 8-round stock rocket
    // load, the most one aircraft can put on the ground in a single pass.
    private const int PoolCap = 16;

    private const float CraterRadiusScale = 1.1f;  // × the weapon's crater radius, when one carved
    private const float BurstRadiusScale = 0.455f; // … and when the weapon carves nothing
    private const float LifeSeconds = 90f;         // mark to gone
    private const float HoldFraction = 0.55f;      // full darkness for this much of the life
    private const float PeakAlpha = 0.95f;         // how much of the burn reaches the surface
    private const float MergeFraction = 0.5f;      // a hit this far inside a live mark refreshes it
    private const float BoxUp = 1.5f;              // projection box above the impact (m) …
    private const float BoxDown = 7f;              // … and below it, past the bowl's 2×DEPTH floor
    private const float NormalFadeTune = 0.5f;     // spare a wall standing inside the box
    private const float FarFadeBegin = 900f;       // stop drawing the mark past this (m) …
    private const float FarFadeLength = 300f;      // … over this much fade

    // The burn texture: a soft radial falloff with a ragged rim and patchy soot, generated once.
    private const int TextureSize = 128;
    private const float CoreFraction = 0.25f; // solid to this fraction of the disc, then falls off
    private const float NoiseFrequency = 3.5f;
    private const float RimRagged = 0.35f;  // how far the noise pushes the falloff radius around
    private const float SootMottle = 0.45f; // and how much it mottles the alpha inside

    // TUNE, with the block above: soot, warm rather than neutral black.
    private static readonly Color ScorchColor = new(0.06f, 0.05f, 0.045f);

    private static ImageTexture? _burnTexture;

    private readonly List<Burn> _burns = new();

    /// <summary>Marks standing in the world right now.</summary>
    public int LiveMarks { get; private set; }

    /// <summary>Decal nodes the pool has built, never more than its cap.</summary>
    internal int PooledNodes => _burns.Count;

    /// <summary>Builds the field, or null on the faithful path: this is a remake-only layer and the
    /// presentation switch is its one gate. Add the returned node under the world root; it ages its
    /// own marks and asks the session's simulation for no step.</summary>
    public static ScorchField? Create()
    {
        if (!GraphicsMode.Enhanced)
            return null;
        Log.Info("world", $"scorch field: pool cap {PoolCap}, life {LifeSeconds:0} s, decals over the crater carve (enhanced only)");
        return new ScorchField { Name = "scorch_field" };
    }

    /// <summary>Burns a mark into the ground at <paramref name="at"/>, projected along
    /// <paramref name="normal"/> (the struck surface's own). <paramref name="craterRadius"/> is the
    /// weapon's crater radius and <paramref name="carved"/> whether this hit cut a bowl, which is
    /// the difference between ringing a crater and scorching bare ground. A hit landing inside a
    /// live mark refreshes that mark instead of stacking a second decal on it, the crater field's
    /// own refusal borrowed.</summary>
    public void Mark(Vector3 at, Vector3 normal, float craterRadius, bool carved)
    {
        float radius = craterRadius * (carved ? CraterRadiusScale : BurstRadiusScale);
        if (radius <= 0f)
            return;
        var burn = Merged(at, radius) ?? FreeSlot() ?? Oldest();
        Place(burn, at, normal.LengthSquared() > 1e-6f ? normal.Normalized() : Vector3.Up, radius);
        CountLive();
    }

    /// <summary>Ages every live mark by one step, fading it out over the tail of its life and
    /// hiding it at the end. Driven from <see cref="_Process"/>; a suite drives it directly.</summary>
    public void Tick(float dt)
    {
        foreach (var burn in _burns)
        {
            if (!burn.Live)
                continue;
            burn.Age += dt;
            if (burn.Age >= LifeSeconds)
            {
                burn.Live = false;
                burn.Node.Visible = false;
                continue;
            }
            burn.Node.Modulate = Fade(burn.Age);
        }
        CountLive();
    }

    public override void _Process(double delta)
    {
        // A mark is sim state: it freezes with a halted clock and scales with a scaled one, the
        // same reading the puffer particles take.
        Tick(GameClock.Current?.FrameDt ?? (float)delta);
    }

    internal bool IsLive(int index) => _burns[index].Live;

    internal float RadiusOf(int index) => _burns[index].Radius;

    internal float AgeOf(int index) => _burns[index].Age;

    internal Decal NodeOf(int index) => _burns[index].Node;

    // The mark's darkness over its life: flat while the burn is fresh, then off over the tail.
    private static Color Fade(float age)
    {
        float t = Mathf.Clamp(age / LifeSeconds, 0f, 1f);
        return new Color(1f, 1f, 1f, PeakAlpha * (1f - Mathf.SmoothStep(HoldFraction, 1f, t)));
    }

    // One texture for the whole pool, built on first use and never on the faithful path, where
    // nothing holds a field to ask for it. Value noise off an integer hash rather than an RNG
    // stream: the image is then the same bytes in every run with no seed to pin.
    private static ImageTexture BurnTexture()
    {
        if (_burnTexture != null)
            return _burnTexture;
        var img = Image.CreateEmpty(TextureSize, TextureSize, false, Image.Format.Rgba8);
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float u = ((x + 0.5f) / TextureSize * 2f) - 1f;
                float v = ((y + 0.5f) / TextureSize * 2f) - 1f;
                float grain = (Noise(u * NoiseFrequency, v * NoiseFrequency) * 0.65f)
                    + (Noise(u * NoiseFrequency * 2.7f, v * NoiseFrequency * 2.7f) * 0.35f);
                float edge = Mathf.Sqrt((u * u) + (v * v)) * (1f + (RimRagged * (grain - 0.5f)));
                float alpha = (1f - Mathf.SmoothStep(CoreFraction, 1f, edge))
                    * (1f - (SootMottle * (grain - 0.5f)));
                img.SetPixel(x, y, new Color(ScorchColor.R, ScorchColor.G, ScorchColor.B,
                    Mathf.Clamp(alpha, 0f, 1f)));
            }
        }
        _burnTexture = TextureUpload.Create(img);
        return _burnTexture;
    }

    // Value noise on an integer lattice, smoothstep-interpolated.
    private static float Noise(float x, float y)
    {
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        float fx = x - x0;
        float fy = y - y0;
        fx = fx * fx * (3f - (2f * fx));
        fy = fy * fy * (3f - (2f * fy));
        float a = Mathf.Lerp(Hash(x0, y0), Hash(x0 + 1, y0), fx);
        float b = Mathf.Lerp(Hash(x0, y0 + 1), Hash(x0 + 1, y0 + 1), fx);
        return Mathf.Lerp(a, b, fy);
    }

    private static float Hash(int x, int y)
    {
        uint h = (uint)(x * 374761393) + (uint)(y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
    }

    // The projection axis is the struck surface's own normal, so a mark on a slope lies along the
    // ground instead of being smeared down a world-vertical box.
    private static Basis Facing(Vector3 up)
    {
        var reference = Mathf.Abs(up.Y) > 0.99f ? Vector3.Forward : Vector3.Up;
        var right = reference.Cross(up).Normalized();
        return new Basis(right, up, right.Cross(up).Normalized());
    }

    private Burn? Merged(Vector3 at, float radius)
    {
        foreach (var burn in _burns)
        {
            if (burn.Live && at.DistanceTo(burn.Centre) <= Mathf.Max(burn.Radius, radius) * MergeFraction)
                return burn;
        }
        return null;
    }

    private Burn? FreeSlot()
    {
        foreach (var burn in _burns)
        {
            if (!burn.Live)
                return burn;
        }
        if (_burns.Count >= PoolCap)
            return null;
        var decal = new Decal
        {
            TextureAlbedo = BurnTexture(),
            AlbedoMix = 1f,
            NormalFade = NormalFadeTune,
            DistanceFadeEnabled = true,
            DistanceFadeBegin = FarFadeBegin,
            DistanceFadeLength = FarFadeLength,
            Visible = false,
        };
        AddChild(decal);
        var made = new Burn { Node = decal };
        _burns.Add(made);
        return made;
    }

    private Burn Oldest()
    {
        var oldest = _burns[0];
        foreach (var burn in _burns)
        {
            if (burn.Age > oldest.Age)
                oldest = burn;
        }
        return oldest;
    }

    private void Place(Burn burn, Vector3 at, Vector3 up, float radius)
    {
        // The box straddles the surface and reaches further down than up, so a mark ringing a
        // crater still paints the bowl it was cut into without darkening an aircraft over it.
        burn.Centre = at;
        burn.Radius = radius;
        burn.Age = 0f;
        burn.Live = true;
        burn.Node.GlobalTransform = new Transform3D(Facing(up), at + (up * ((BoxUp - BoxDown) * 0.5f)));
        burn.Node.Size = new Vector3(radius * 2f, BoxUp + BoxDown, radius * 2f);
        burn.Node.Modulate = Fade(0f);
        burn.Node.Visible = true;
    }

    private void CountLive()
    {
        int live = 0;
        foreach (var burn in _burns)
        {
            if (burn.Live)
                live++;
        }
        LiveMarks = live;
    }

    private sealed class Burn
    {
        internal Decal Node = null!;
        internal Vector3 Centre;
        internal float Radius;
        internal float Age;
        internal bool Live;
    }
}
