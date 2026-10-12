using System;
using Godot;

namespace CSVM.Effects;

/// <summary>
/// One hull's track on the wave ocean, which the shader draws its Kelvin wake and foam trail from.
/// A sample falls on every whole multiple of <see cref="SampleSecs"/> of sim time. It is interpolated
/// between the two observations around it, so frame pacing moves no sample. A gap, time running back
/// or an impossible jump starts the track again, standing. The shader gets the samples through
/// <see cref="Write"/>, led by the hull's own heading. No engine object is touched.
/// </summary>
internal sealed class OceanWakeTrack
{
    /// <summary>Remake-only rule, set by the look. The sim seconds between two samples. The trail
    /// lasts this times two less than <see cref="OceanShader.WakeSamples"/>; a turn bends at the samples.</summary>
    public const float SampleSecs = 4f;

    /// <summary>The sim seconds a trail lasts: the shader fades it out by this age.</summary>
    public const float LifeSecs = SampleSecs * (OceanShader.WakeSamples - 2);

    /// <summary>A sample nearer than this to the newer point is left out, so every written segment
    /// keeps a direction. A track shorter than this is a hull standing still, which draws no wake.</summary>
    public const float MinSegment = 2f;

    /// <summary>The speed in m/s under which a hull has no heading of travel to lead its wake with.</summary>
    public const float HeadingSpeed = 0.5f;

    // Faster than any hull between two observations: a respawn or a placement, not a passage.
    private const float MaxSpeed = 80f;

    // The lead along the heading reaches at most this share of the way to the newest sample. The
    // ages along the track then keep their order.
    private const float LeadShare = 0.75f;

    // How far the wake reaches beside its track. The first is the tangent of the V's half angle,
    // rounded up; the second a margin past the beam.
    private const float ReachSpread = 0.36f;
    private const float ReachMargin = 4f;

    private readonly double[] _times = new double[OceanShader.WakeSamples - 2];
    private readonly Vector2[] _points = new Vector2[OceanShader.WakeSamples - 2];
    private int _count;
    private int _newest;
    private bool _seen;
    private double _lastTime;
    private Vector2 _lastPoint;
    private Vector2 _velocity;

    /// <summary>The bounds of a slot that draws no wake.</summary>
    public static Vector4 Unused => new(1e9f, 1e9f, 0f, 0f);

    /// <summary>The sample count held, the current position not included.</summary>
    public int Count => _count;

    /// <summary>Takes the hull's position <paramref name="point"/> (world X/Z) at sim time
    /// <paramref name="time"/>, adding a sample for every multiple of <see cref="SampleSecs"/> passed
    /// since the last observation.</summary>
    public void Observe(double time, Vector2 point)
    {
        double dt = time - _lastTime;
        float moved = point.DistanceTo(_lastPoint);
        if (!_seen || dt < 0.0 || dt > 2.0 * SampleSecs || moved > (MaxSpeed * dt) + MinSegment)
        {
            Restart(time, point);
            return;
        }
        if (dt > 0.0)
        {
            for (double n = Math.Floor(_lastTime / SampleSecs) + 1.0; n * SampleSecs <= time; n++)
            {
                double at = n * SampleSecs;
                Push(at, _lastPoint.Lerp(point, (float)((at - _lastTime) / dt)));
            }
            _velocity = (point - _lastPoint) / (float)dt;
        }
        _lastTime = time;
        _lastPoint = point;
    }

    /// <summary>Writes <see cref="OceanShader.WakeSamples"/> entries into <paramref name="track"/> from
    /// <paramref name="at"/>, newest first: X, Z, metres behind the hull, age. The current position
    /// leads, then a point back along the heading of travel, so the bow's V turns with the hull.
    /// The hull's half length and beam come in <paramref name="half"/>, its heading either way in
    /// <paramref name="axis"/>. Returns centre X/Z, reach and length; <see cref="Unused"/> when standing.</summary>
    public Vector4 Write(Vector4[] track, int at, Vector2 half, Vector2 axis)
    {
        var prev = _lastPoint;
        float arc = 0f;
        var min = prev;
        var max = prev;
        int n = 0;
        track[at + n++] = new Vector4(prev.X, prev.Y, 0f, 0f);
        float speed = _velocity.Length();
        if (speed >= HeadingSpeed && Newest(out var first, out float firstAge))
        {
            var travel = axis.Dot(_velocity) < 0f ? -axis : axis;
            float lead = Math.Min(2f * half.X, LeadShare * first.DistanceTo(prev));
            if (lead >= MinSegment)
            {
                prev -= travel * lead;
                arc = lead;
                min = min.Min(prev);
                max = max.Max(prev);
                track[at + n++] = new Vector4(prev.X, prev.Y, arc, Math.Min(lead / speed, LeadShare * firstAge));
            }
        }
        for (int i = 0; i < _count && n < OceanShader.WakeSamples; i++)
        {
            int k = (_newest - i + _points.Length) % _points.Length;
            var p = _points[k];
            float step = p.DistanceTo(prev);
            if (step < MinSegment)
                continue;
            arc += step;
            prev = p;
            min = min.Min(p);
            max = max.Max(p);
            track[at + n++] = new Vector4(p.X, p.Y, arc, (float)(_lastTime - _times[k]));
        }
        var last = track[at + n - 1];
        while (n < OceanShader.WakeSamples)
            track[at + n++] = last;
        if (arc < MinSegment)
            return Unused;
        var centre = (min + max) * 0.5f;
        float reach = ((max - min).Length() * 0.5f) + (ReachSpread * (arc + half.X)) + half.Y + ReachMargin;
        return new Vector4(centre.X, centre.Y, reach, arc);
    }

    // The newest sample a segment's length off the current position, and its age.
    private bool Newest(out Vector2 point, out float age)
    {
        for (int i = 0; i < _count; i++)
        {
            int k = (_newest - i + _points.Length) % _points.Length;
            if (_points[k].DistanceTo(_lastPoint) >= MinSegment)
            {
                point = _points[k];
                age = (float)(_lastTime - _times[k]);
                return true;
            }
        }
        point = _lastPoint;
        age = 0f;
        return false;
    }

    private void Restart(double time, Vector2 point)
    {
        _seen = true;
        _count = 0;
        _lastTime = time;
        _lastPoint = point;
        _velocity = Vector2.Zero;
    }

    private void Push(double time, Vector2 point)
    {
        _newest = (_newest + 1) % _points.Length;
        _times[_newest] = time;
        _points[_newest] = point;
        _count = Math.Min(_count + 1, _points.Length);
    }
}
