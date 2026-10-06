using System;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// One ship's calm zone on the wave ocean: a box on the water along the hull's heading. It holds
/// the hull's waterline and every wake sheet the hull trails. The shader keeps the waves flat inside
/// it and a margin past it, then fades them in. A zone grows from footprints in world space, so it
/// follows the wake sheets' own geometry and turns with the hull.
/// </summary>
internal struct OceanCalmZone
{
    private readonly Vector2 _origin;
    private readonly Vector2 _u;
    private readonly Vector2 _v;
    private float _minU;
    private float _maxU;
    private float _minV;
    private float _maxV;

    /// <summary>An empty zone on the water at <paramref name="origin"/> (world X/Z), its long axis
    /// along <paramref name="heading"/>. A heading with no horizontal length falls back to +X.</summary>
    public OceanCalmZone(Vector2 origin, Vector2 heading)
    {
        _origin = origin;
        _u = heading.LengthSquared() > 1e-8f ? heading.Normalized() : Vector2.Right;
        _v = new Vector2(-_u.Y, _u.X);
        _minU = _minV = float.MaxValue;
        _maxU = _maxV = float.MinValue;
    }

    /// <summary>Whether no footprint has been added yet.</summary>
    public readonly bool IsEmpty => _minU > _maxU;

    /// <summary>The unit axis along the heading, in world X/Z.</summary>
    public readonly Vector2 Axis => _u;

    /// <summary>The box's centre in world X/Z; the origin while the zone is empty.</summary>
    public readonly Vector2 Center => IsEmpty ? _origin
        : _origin + (_u * ((_minU + _maxU) * 0.5f)) + (_v * ((_minV + _maxV) * 0.5f));

    /// <summary>Half the box's length along the heading and half its width across it.</summary>
    public readonly Vector2 HalfExtents => IsEmpty ? Vector2.Zero
        : new Vector2((_maxU - _minU) * 0.5f, (_maxV - _minV) * 0.5f);

    /// <summary>The fraction of the wave height left at <paramref name="distance"/> metres from a
    /// zone: none within <paramref name="margin"/>, all of it <paramref name="fade"/> metres further,
    /// on the shader's smoothstep.</summary>
    public static float Calm(float distance, float margin, float fade)
    {
        float t = Math.Clamp((distance - margin) / Math.Max(fade, 1e-6f), 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    /// <summary>Grows the box over one world X/Z point.</summary>
    public void Add(Vector2 point)
    {
        var d = point - _origin;
        float u = d.Dot(_u), v = d.Dot(_v);
        _minU = Math.Min(_minU, u);
        _maxU = Math.Max(_maxU, u);
        _minV = Math.Min(_minV, v);
        _maxV = Math.Max(_maxV, v);
    }

    /// <summary>Grows the box over the horizontal shadow of a local box under a transform: a mesh's
    /// AABB and its global transform.</summary>
    public void Add(Aabb box, Transform3D xf)
    {
        var p = box.Position;
        var s = box.Size;
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3(p.X + ((i & 1) != 0 ? s.X : 0f), p.Y + ((i & 2) != 0 ? s.Y : 0f), p.Z + ((i & 4) != 0 ? s.Z : 0f));
            var w = xf * corner;
            Add(new Vector2(w.X, w.Z));
        }
    }

    /// <summary>The metres from <paramref name="point"/> to the box, zero inside it. The shader's
    /// <c>ship_calm</c> computes the same.</summary>
    public readonly float Distance(Vector2 point)
    {
        var d = point - Center;
        var half = HalfExtents;
        float ex = Math.Max(Math.Abs(d.Dot(_u)) - half.X, 0f);
        float ey = Math.Max(Math.Abs(d.Dot(_v)) - half.Y, 0f);
        return MathF.Sqrt((ex * ex) + (ey * ey));
    }
}
