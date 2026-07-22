using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Runs the original's texture flipbooks: a material whose gamez <c>cycle</c> block lists frame
/// textures and a rate, advanced by swapping the shader's <c>albedo_tex</c>.
///
/// Two sources feed it, and they are the same mechanism seen from different sides:
/// <list type="bullet">
/// <item>the per-material <c>cycle</c> block in the gamez (animated water, surf, boat wakes,
/// turbulence, splashes, the walking/running crowd sprites), and</item>
/// <item>the <c>EFFECTS</c> reader (<c>effects.zrd.json</c>), which binds a frame list to a
/// NODE instead — the shared <c>fire1</c>/<c>fire2</c> billboards every burning object
/// borrows.</item>
/// </list>
///
/// Swapping a texture from C# rather than indexing a <c>sampler2DArray</c> in the shader is
/// deliberate: a chapter has at most a handful of cycling materials (1 in C1, 5 in C1B), so the
/// per-frame cost is a few <c>SetShaderParameter</c> calls, and it needs no new shader variant,
/// no atlas build, and no assumption that every frame shares one size. Frames are resolved once
/// at build time, while the session's <see cref="TextureArchive"/> is still open.
/// </summary>
public sealed partial class TextureCycler : Node
{
    private sealed class Cycle
    {
        public ShaderMaterial Material = null!;
        public ImageTexture[] Frames = null!;
        public float Fps;
        public bool Looping;
        public float Clock;
        public int Current = -1;
    }

    private readonly List<Cycle> _cycles = new();

    /// <summary>--debug-anim: report each flipbook's frame once a second, so a headless run can
    /// prove the frames advance without hunting for a camera angle where the change is visible.
    /// The water is deliberately subtle in the original — 64x64 frames differing by ~2/255 — so
    /// "I can't see it in a screenshot" is not evidence that it is not running.</summary>
    public bool Debug;
    private float _debugClock;

    /// <summary>How many flipbooks are running (diagnostics / the build log).</summary>
    public int Count => _cycles.Count;

    /// <summary>Registers a flipbook. Ignored unless it has at least two frames and a rate —
    /// a one-frame "cycle" is just a static texture, and a zero rate would divide by nothing.</summary>
    /// <summary>Which flipbooks are running, as "base→frames@fps" — the build log line, and
    /// the only way to tell a registered cycle from one whose frames failed to resolve.</summary>
    public readonly List<string> Summary = new();

    public void Add(ShaderMaterial material, IReadOnlyList<ImageTexture> frames, float fps, bool looping, string label = "")
    {
        if (frames.Count < 2 || fps <= 0f)
            return;
        if (label.Length > 0)
            Summary.Add($"{label}×{frames.Count}@{fps:0.#}");
        var arr = new ImageTexture[frames.Count];
        for (int i = 0; i < frames.Count; i++)
            arr[i] = frames[i];
        _cycles.Add(new Cycle { Material = material, Frames = arr, Fps = fps, Looping = looping });
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (Debug)
        {
            _debugClock += dt;
            if (_debugClock >= 1f)
            {
                _debugClock = 0f;
                var parts = new List<string>();
                for (int i = 0; i < _cycles.Count; i++)
                    parts.Add($"{(i < Summary.Count ? Summary[i] : "?")}=f{_cycles[i].Current}");
                GD.Print("anim/debug: texture cycles " + string.Join(" ", parts));
            }
        }
        foreach (var c in _cycles)
        {
            c.Clock += dt;
            int frame = (int)(c.Clock * c.Fps);
            if (c.Looping)
                frame %= c.Frames.Length;
            else if (frame >= c.Frames.Length)
                frame = c.Frames.Length - 1;
            if (frame == c.Current)
                continue; // the rates here are 4-12 fps, so most frames change nothing
            c.Current = frame;
            c.Material.SetShaderParameter("albedo_tex", c.Frames[frame]);
        }
    }
}
