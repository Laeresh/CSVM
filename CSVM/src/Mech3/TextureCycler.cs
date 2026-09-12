using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Runs the original's texture flipbooks: a material whose gamez <c>cycle</c> block lists frame
/// textures and a rate, advanced by swapping the shader's <c>albedo_tex</c>.
/// Two sources feed it: the gamez <c>cycle</c> block itself (water, surf, wakes, crowds), and
/// <see cref="EffectCycles"/>, which writes the fire flipbooks onto their target material before
/// the build. Both are material-keyed in the original, so a flipbook reaches every polygon on
/// that material, not just the node that named it.
/// Swapped via <c>SetShaderParameter</c> rather than a shader-side frame array: cheap, and needs
/// no new variant or atlas. Frames resolve at build time while <see cref="TextureArchive"/> is open.
/// </summary>
public sealed partial class TextureCycler : Node
{
    /// <summary>Which flipbooks are running, as "base→frames@fps", the build log line, and
    /// the only way to tell a registered cycle from one whose frames failed to resolve.</summary>
    public readonly List<string> Summary = new();

    /// <summary>--debug-anim: report each flipbook's frame once a second, so a headless run can
    /// prove the frames advance without hunting for a camera angle where the change is visible.
    /// The water is deliberately subtle in the original, 64x64 frames differing by ~2/255, so
    /// "I can't see it in a screenshot" is not evidence that it is not running.</summary>
    public bool Debug;

    private readonly List<Cycle> _cycles = new();
    private float _debugClock;
    private bool _frozen;

    /// <summary>How many flipbooks are running (diagnostics / the build log).</summary>
    public int Count => _cycles.Count;

    /// <summary>Registers a flipbook. Ignored unless it has at least two frames and a rate,
    /// a one-frame "cycle" is just a static texture, and a zero rate would divide by nothing.</summary>
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
        // Each flipbook frame is its own texture with its own drop-in colour, so a running cycle
        // would repaint the surface a different colour every few frames and the census would read
        // whichever one the shot caught. Frozen, the surface keeps its material's base texture.
        if (TextureDropIn.Active)
        {
            if (!_frozen)
            {
                _frozen = true;
                Log.Info("anim", $"texture cycles frozen for the texture drop-in cycles={_cycles.Count}");
            }
            return;
        }
        // Flipbook time is sim time: a halted or scaled clock must hold or scale the water.
        float dt = GameClock.Current?.FrameDt ?? (float)delta;
        if (Debug)
        {
            _debugClock += dt;
            if (_debugClock >= 1f)
            {
                _debugClock = 0f;
                var parts = new List<string>();
                for (int i = 0; i < _cycles.Count; i++)
                    parts.Add($"{(i < Summary.Count ? Summary[i] : "?")}=f{_cycles[i].Current}");
                Log.Debug("anim", $"texture cycles {string.Join(" ", parts)}");
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
            c.Material.SetShaderParameter(SceneBuilder.AlbedoTexParam, c.Frames[frame]);
        }
    }

    private sealed class Cycle
    {
        public ShaderMaterial Material = null!;
        public ImageTexture[] Frames = null!;
        public float Fps;
        public bool Looping;
        public float Clock;
        public int Current = -1;
    }
}
