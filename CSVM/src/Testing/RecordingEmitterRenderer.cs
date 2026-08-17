using System.Collections.Generic;
using CSVM.Effects;
using Godot;

namespace CSVM.Testing;

/// <summary>A no-GPU stand-in for <c>MultiMeshEmitterRenderer</c>: it keeps the particles a
/// <see cref="Puffer"/> hands it instead of drawing them, so a suite can assert on the emitter's
/// three modes — burst, distance trail and sustain — with no atlas, no <c>TextureArchive</c> and no
/// <c>MultiMesh</c> anywhere in the path.
///
/// <para>The mirror of <see cref="CountingEmitterFactory"/> one seam lower: that fake stands in for
/// the whole emitter so <c>EmitterDirector</c>'s LIFETIME is assertable; this one stands in for the
/// draw so the emitter's own MODES are. Neither covers the other's job.</para></summary>
public sealed class RecordingEmitterRenderer : IEmitterRenderer
{
    private List<Particle> _last = new();
    private List<Particle> _pending = new();

    /// <summary>The node the renderer was attached under, or null if <see cref="Attach"/> never ran.</summary>
    public Node3D? Owner { get; private set; }

    /// <summary>The draw pool the emitter sized — <c>-1</c> until <see cref="Attach"/> runs.</summary>
    public int Capacity { get; private set; } = -1;

    /// <summary>The frustum padding the emitter asked for.</summary>
    public float CullMargin { get; private set; }

    /// <summary>Every particle written since the previous <see cref="Show"/> — one frame's worth.</summary>
    public IReadOnlyList<Particle> LastFrame => _last;

    /// <summary>The count the most recent <see cref="Show"/> published, or <c>-1</c> before the first.</summary>
    public int Shown { get; private set; } = -1;

    /// <summary>The largest count any <see cref="Show"/> has published — the emitter's high-water
    /// mark, which a per-frame read would miss.</summary>
    public int MaxShown { get; private set; }

    /// <summary>The largest slot index ever written. A value at or past <see cref="Capacity"/> means
    /// the emitter overran its own pool, which on the real renderer is an out-of-range draw call.</summary>
    public int MaxIndex { get; private set; } = -1;

    /// <summary>The largest atlas column ever written — the flipbook actually advancing.</summary>
    public float MaxFrame { get; private set; }

    public void Attach(Node3D owner, int capacity, float cullMargin)
    {
        Owner = owner;
        Capacity = capacity;
        CullMargin = cullMargin;
    }

    public void Grow(int capacity)
    {
        if (capacity > Capacity)
            Capacity = capacity;
    }

    public void Write(int index, Vector3 position, float size, float frame, float alpha, Color color)
    {
        _pending.Add(new Particle(index, position, size, frame, alpha, color));
        if (index > MaxIndex)
            MaxIndex = index;
        if (frame > MaxFrame)
            MaxFrame = frame;
    }

    public void Show(int liveCount)
    {
        Shown = liveCount;
        if (liveCount > MaxShown)
            MaxShown = liveCount;
        _last.Clear();
        (_last, _pending) = (_pending, _last);
    }

    /// <summary>One particle as the emitter presented it for drawing.</summary>
    public readonly record struct Particle(
        int Index, Vector3 Position, float Size, float Frame, float Alpha, Color Color);
}
