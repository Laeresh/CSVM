using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Flight.Airframe;

/// <summary>Contact detection for one aircraft, over the world query. The airframe hull sweep, the
/// def's collision probes and the anti-tunnelling centre ray each fill one
/// <see cref="ContactReport"/>. A person's rule sweeps the hulls; world AI sweeps its probes. It
/// decides nothing about a contact. <see cref="AircraftContactResolver"/> does that, and
/// <see cref="FlightController"/> performs the outcome. It also answers the world ray a falling
/// wreck lands on, and draws the <c>--debug-collision</c> probe.</summary>
internal sealed class AircraftContactSweep
{
    private const float CollisionMargin = 6f;   // m of look-ahead past the nose (airframe half-length)

    private readonly Func<IWorldQuery> _world;
    private ImmediateMesh? _probe;              // debug collision-probe line

    /// <summary>Builds the sweep over <paramref name="world"/>, read at each call so a rig that
    /// binds its seam late still sweeps the bound one.</summary>
    public AircraftContactSweep(Func<IWorldQuery> world) => _world = world;

    /// <summary>Builds the debug probe's mesh under <paramref name="parent"/>, which
    /// <see cref="DrawProbe"/> then redraws every step.</summary>
    public void ShowProbe(Node3D parent)
    {
        _probe = new ImmediateMesh();
        parent.AddChild(new MeshInstance3D
        {
            Mesh = _probe,
            TopLevel = true, // vertices are in world space
            Name = "collision_probe",
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                NoDepthTest = true, // stays visible against/through terrain
            },
        });
    }

    /// <summary>One step's contact test from <paramref name="from"/> to <paramref name="to"/> at
    /// <paramref name="attitude"/>. The striker's rule picks the hulls or the probes, and the centre
    /// ray backs up a miss. The struck node comes back beside the report, never in it, so the
    /// decision side reads no node.</summary>
    public bool Detect(Vector3 from, Vector3 to, Basis attitude, in ContactConditions striker,
        out ContactReport contact, out Node? hitBody)
    {
        var step = to - from;
        float len = step.Length();
        bool hit = striker.TakesPersonsContactRule
            ? SweepAirframe(from, step, attitude, striker.Parts, striker.ExcludeSelf, out contact, out hitBody)
            : SweepProbes(from, step, attitude, striker.Stats?.CollisionProbes, striker.ExcludeSelf, out contact, out hitBody);
        if (!hit)
            hit = CenterRayContact(from, RayEnd(to, step, len, striker.Parts), step, len, striker.ExcludeSelf, out contact, out hitBody);
        return hit;
    }

    /// <summary>True if the segment crosses any solid collider: the static world, or another
    /// aircraft's body, never this plane's own. A hit answers the impact position (else the segment
    /// end) and names the collider as parent/body, as a terrain tile's "g27889/col" reads.</summary>
    public bool HitWorld(Vector3 from, Vector3 to, Godot.Collections.Array<Rid>? excludeSelf,
        out Vector3 point, out string hitName, out Node? hitBody)
    {
        point = to;
        hitName = "";
        hitBody = null;
        if (!_world().Ray(from, to, CollisionLayers.WorldAndAircraft, excludeSelf, out var report))
            return false;
        point = report.Position;
        if (report.Collider is { } body)
        {
            hitBody = body;
            hitName = $"{body.GetParent()?.Name}/{body.Name}";
        }
        return true;
    }

    /// <summary>Debug view of the contact test: the swept centre ray with a cross at its tip. The
    /// airframe hulls are drawn where this step's sweep stopped. Freezes red at the impact pose while
    /// crashed. Draws nothing unless <see cref="ShowProbe"/> built the mesh.</summary>
    public void DrawProbe(Vector3 from, Vector3 to, bool hit, float stopFraction, Basis attitude,
        IReadOnlyList<PlaneCollider.Part>? parts)
    {
        if (_probe == null)
            return;
        var step = to - from;
        var end = RayEnd(to, step, step.Length(), parts);
        var shapePos = from + step * (hit ? stopFraction : 1f);
        var color = hit ? new Color(1f, 0.15f, 0.1f) : new Color(0.2f, 1f, 0.3f);
        _probe.ClearSurfaces();
        _probe.SurfaceBegin(Mesh.PrimitiveType.Lines);
        _probe.SurfaceSetColor(color);
        _probe.SurfaceAddVertex(from);
        _probe.SurfaceAddVertex(end);
        const float s = 1.5f;
        foreach (var axis in stackalloc[] { Vector3.Right, Vector3.Up, Vector3.Back })
        {
            _probe.SurfaceAddVertex(end - axis * s);
            _probe.SurfaceAddVertex(end + axis * s);
        }
        if (parts != null)
        {
            var baseXf = new Transform3D(attitude, shapePos);
            foreach (var p in parts)
            {
                var xf = baseXf * p.Local;
                foreach (var (a, b) in p.Hull.Edges)
                {
                    _probe.SurfaceAddVertex(xf * p.Hull.Points[a]);
                    _probe.SurfaceAddVertex(xf * p.Hull.Points[b]);
                }
            }
        }
        _probe.SurfaceEnd();
    }

    // The centre ray's end. The airframe boxes sweep the motion itself, so only the shapeless
    // fallback keeps a nose margin on the ray.
    private static Vector3 RayEnd(Vector3 to, Vector3 step, float len,
        IReadOnlyList<PlaneCollider.Part>? parts)
    {
        float margin = parts == null ? CollisionMargin : 0f;
        return len > 1e-4f ? to + step / len * margin : to;
    }

    // Sweeps each airframe box along this frame's motion against every solid collider, world plus
    // other aircraft's bodies, own body excluded by RID. A mid-air resolves through the contact rules
    // like any other contact. Fills the earliest stop's report. False when
    // uncollidable or nothing is in the way.
    private bool SweepAirframe(Vector3 from, Vector3 motion, Basis attitude,
        IReadOnlyList<PlaneCollider.Part>? parts, Godot.Collections.Array<Rid>? excludeSelf,
        out ContactReport contact, out Node? hitBody)
    {
        contact = default;
        hitBody = null;
        if (parts == null)
            return false;
        var baseXf = new Transform3D(attitude, from);
        if (!_world().Sweep(parts, baseXf, motion, CollisionLayers.WorldAndAircraft,
            excludeSelf, out var report))
            return false;
        hitBody = report.Collider;
        contact = new ContactReport
        {
            Impact = report.Contact,
            Normal = report.Normal,
            Part = report.Part,
            ColliderName = report.ColliderName,
            StopFraction = report.StopFraction,
            StruckIsAircraft = (report.Collider as AircraftBody)?.Rig != null,
        };
        return true;
    }

    // The original's contact test (FUN_0048d7f0). Each of the def's collision probes is carried from
    // the sweep's start pose to this frame's pose. The earliest strike along the motion wins.
    // The player defs author six, every AI def basic_airplane's single origin probe, which threads
    // the CM13 racers through dzpath2's 9.7 m arch (docs/formats/vehicle.md "Collision probes").
    // A def with no probe list reports nothing, and the centre ray behind it stands.
    private bool SweepProbes(Vector3 from, Vector3 motion, Basis attitude,
        List<Vector3>? probes, Godot.Collections.Array<Rid>? excludeSelf,
        out ContactReport contact, out Node? hitBody)
    {
        contact = default;
        hitBody = null;
        if (probes is not { Count: > 0 })
            return false;
        float len = motion.Length();
        if (len < 1e-4f)
            return false;
        float best = float.MaxValue;
        foreach (var probe in probes)
        {
            var offset = attitude * probe;
            var start = from + offset;
            if (!_world().Ray(start, start + motion, CollisionLayers.WorldAndAircraft, excludeSelf, out var report))
                continue;
            float fraction = start.DistanceTo(report.Position) / len;
            if (fraction >= best)
                continue;
            best = fraction;
            hitBody = report.Collider;
            contact = new ContactReport
            {
                Impact = report.Position,
                Normal = report.Normal.LengthSquared() > 1e-6f ? report.Normal : -motion / len,
                Part = Mathf.Abs(probe.X) > 1f ? "wing" : "center",
                ColliderName = report.Collider is { } body ? $"{body.GetParent()?.Name}/{body.Name}" : "",
                StopFraction = fraction,
                StruckIsAircraft = (report.Collider as AircraftBody)?.Rig != null,
            };
        }
        return best < float.MaxValue;
    }

    // The anti-tunnelling backstop, filling the same report off the centre ray alone: no box
    // reached the obstacle but the swept centre did. With no struck box and no surface normal, the
    // part reads `center` and the normal is the reversed motion, which makes the contact head-on.
    // The stop fraction stays 1: a ray reports where it hit, not where the airframe would rest.
    private bool CenterRayContact(Vector3 from, Vector3 to, Vector3 motion, float motionLen,
        Godot.Collections.Array<Rid>? excludeSelf, out ContactReport contact, out Node? hitBody)
    {
        contact = default;
        if (!HitWorld(from, to, excludeSelf, out var point, out var hitName, out hitBody))
            return false;
        contact = new ContactReport
        {
            Impact = point,
            Normal = motionLen > 1e-4f ? -motion / motionLen : Vector3.Up,
            Part = "center",
            ColliderName = hitName,
            StopFraction = 1f,
            StruckIsAircraft = (hitBody as AircraftBody)?.Rig != null,
        };
        return true;
    }
}
