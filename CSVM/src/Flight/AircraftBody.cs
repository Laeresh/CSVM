using Godot;

namespace CSVM.Flight;

/// <summary>The flying aircraft's physics body: the shared <see cref="PlaneCollider"/> boxes as
/// real collision shapes on <see cref="CollisionLayers.Aircraft"/>, so a projectile's hit ray —
/// and another plane's airframe sweep — can strike this plane. A child of its
/// <see cref="FlightController"/>, riding exactly the transform the flight model writes: the
/// plane stays Node3D-moved, the physics engine never pushes it, and an impact with it routes
/// through the striking plane's own SurviveHit/Crash resolution, never a solver response.
/// The shapes are the SAME <see cref="BoxShape3D"/> resources the terrain sweep casts —
/// single-sourced from <see cref="PlaneCollider.Parts"/>, never a second derivation: a fidelity
/// upgrade edits <see cref="PlaneCollider"/>, not this class. The box
/// set also answers the proximity-fuse and blast geometry questions (<see cref="NearestShape"/>,
/// <see cref="SegmentDistance"/>) directly, so those passes measure against the exact same
/// hull the hit ray strikes.</summary>
public sealed partial class AircraftBody : AnimatableBody3D
{
    private readonly string[] _partNames;
    // The same boxes as the CollisionShape3D children, kept as plain geometry (local transform +
    // half extents, both unscaled by construction) so the fuse/blast math needs no physics query.
    private readonly Transform3D[] _locals;
    private readonly Vector3[] _halfExtents;
    private Godot.Collections.Array<Rid>? _excludeSelf;

    public AircraftBody(FlightController rig, PlaneCollider collider)
    {
        Rig = rig;
        Name = "airframe";
        CollisionLayer = CollisionLayers.Aircraft;
        CollisionMask = 0;     // a query target only — the body itself tests nothing
        SyncToPhysics = false; // no solver interaction to smooth; the node transform is the truth
        _partNames = new string[collider.Parts.Count];
        _locals = new Transform3D[collider.Parts.Count];
        _halfExtents = new Vector3[collider.Parts.Count];
        float bound = 0f;
        for (int i = 0; i < collider.Parts.Count; i++)
        {
            var p = collider.Parts[i];
            _partNames[i] = p.Name;
            _locals[i] = p.Local;
            _halfExtents[i] = p.Shape.Size * 0.5f;
            bound = Mathf.Max(bound, p.Local.Origin.Length() + _halfExtents[i].Length());
            AddChild(new CollisionShape3D { Shape = p.Shape, Transform = p.Local });
        }
        BoundRadius = bound;
    }

    /// <summary>The controller this body belongs to — how a struck body resolves to its plane.</summary>
    public FlightController Rig { get; }

    /// <summary>The identity of the rounds this plane fires (<see cref="FlightController.PlayerIndex"/>).</summary>
    public int PlayerIndex => Rig.PlayerIndex;

    /// <summary>Radius of a sphere at the body origin containing every collision box — the cheap
    /// reject a per-step proximity scan runs before any per-box distance work.</summary>
    public float BoundRadius { get; }

    /// <summary>This body's RID as a reusable one-entry exclusion list — what the owning plane's
    /// own queries (and its rounds' hit rays) pass so a plane never collides with itself.</summary>
    public Godot.Collections.Array<Rid> ExcludeSelf => _excludeSelf ??= new() { GetRid() };

    /// <summary>Maps a query's struck shape index back to its <see cref="PlaneCollider"/> part
    /// name — the shapes were added in <c>Parts</c> order; "center" for an out-of-range index
    /// (the ray backstop's own label for a shapeless hit).</summary>
    public string PartName(int shapeIdx) =>
        shapeIdx >= 0 && shapeIdx < _partNames.Length ? _partNames[shapeIdx] : "center";

    /// <summary>Turns the body on/off as a target: off while crashed (the airframe is hidden and
    /// gone — a wreck must not soak rounds or block a sweep), back on at respawn.</summary>
    public void SetHittable(bool on) => CollisionLayer = on ? CollisionLayers.Aircraft : 0;

    /// <summary>One projectile hit on this plane: resolve the struck shape to its collider part
    /// and hand the round — with the identity that fired it — to the controller's own damage path
    /// (<see cref="FlightController.TakeProjectileHit"/>). <paramref name="damageScale"/> is the
    /// blast falloff share for a splash hit; a direct round passes 1.</summary>
    public void TakeProjectileHit(WeaponDef weapon, Vector3 point, int shapeIdx, int shooter,
        float damageScale = 1f) =>
        Rig.TakeProjectileHit(weapon, point, PartName(shapeIdx), shooter, damageScale);

    /// <summary>The collision box nearest a world point: its shape index (for
    /// <see cref="PartName"/>), the distance to its surface (0 inside), and that nearest surface
    /// point — the blast pass's falloff geometry, measured to the box skin rather than any
    /// transform origin (the same rule the destructible blast applies). -1 with no
    /// boxes.</summary>
    public int NearestShape(Vector3 worldPoint, out float distance, out Vector3 nearestPoint)
    {
        var bodyXf = GlobalTransform;
        int best = -1;
        distance = float.PositiveInfinity;
        nearestPoint = worldPoint;
        for (int i = 0; i < _locals.Length; i++)
        {
            var xf = bodyXf * _locals[i];
            var clamped = (xf.AffineInverse() * worldPoint).Clamp(-_halfExtents[i], _halfExtents[i]);
            var onBox = xf * clamped;
            float d = onBox.DistanceTo(worldPoint);
            if (d < distance)
            {
                distance = d;
                nearestPoint = onBox;
                best = i;
            }
        }
        return best;
    }

    /// <summary>Closest approach of the swept segment <paramref name="from"/>→<paramref name="to"/>
    /// to this plane's collision boxes: the minimum distance, the segment fraction
    /// <paramref name="t"/> where it occurs, and the box surface point nearest that spot — the
    /// proximity fuse's geometry. Distance-to-a-box is convex along the segment, so a ternary
    /// search per box is exact; the whole segment is tested, never the endpoints (a rocket covers
    /// ~20 m per step — an endpoint test misses most passes outright).</summary>
    public float SegmentDistance(Vector3 from, Vector3 to, out float t, out Vector3 nearestPoint)
    {
        var bodyXf = GlobalTransform;
        float bestD = float.PositiveInfinity;
        t = 0f;
        nearestPoint = from;
        for (int i = 0; i < _locals.Length; i++)
        {
            var xf = bodyXf * _locals[i];
            var inv = xf.AffineInverse();
            var a = inv * from;
            var b = inv * to;
            var half = _halfExtents[i];
            float lo = 0f, hi = 1f;
            for (int it = 0; it < 32; it++)
            {
                float m1 = lo + (hi - lo) / 3f;
                float m2 = hi - (hi - lo) / 3f;
                if (BoxDistance(a.Lerp(b, m1), half) <= BoxDistance(a.Lerp(b, m2), half))
                    hi = m2;
                else
                    lo = m1;
            }
            float s = 0.5f * (lo + hi);
            var local = a.Lerp(b, s);
            float d = BoxDistance(local, half);
            if (d < bestD)
            {
                bestD = d;
                t = s;
                nearestPoint = xf * local.Clamp(-half, half);
            }
        }
        return bestD;
    }

    // Distance from a point to an origin-centred axis-aligned box, in the box's own
    // frame; 0 inside.
    private static float BoxDistance(Vector3 p, Vector3 half)
    {
        var q = p.Abs() - half;
        return new Vector3(Mathf.Max(q.X, 0f), Mathf.Max(q.Y, 0f), Mathf.Max(q.Z, 0f)).Length();
    }
}
