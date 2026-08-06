using Godot;

namespace CSVM.Flight;

/// <summary>The flying aircraft's physics body: the shared <see cref="PlaneCollider"/> boxes as
/// real collision shapes on <see cref="CollisionLayers.Aircraft"/>, so a projectile's hit ray —
/// and another plane's airframe sweep — can strike this plane. A child of its
/// <see cref="FlightController"/>, riding exactly the transform the flight model writes: the
/// plane stays Node3D-moved, the physics engine never pushes it, and an impact with it routes
/// through the striking plane's own SurviveHit/Crash resolution, never a solver response.
/// The shapes are the SAME <see cref="BoxShape3D"/> resources the terrain sweep casts —
/// single-sourced from <see cref="PlaneCollider.Parts"/>, never a second derivation.</summary>
public sealed partial class AircraftBody : AnimatableBody3D
{
    private readonly string[] _partNames;
    private Godot.Collections.Array<Rid>? _excludeSelf;

    public AircraftBody(FlightController rig, PlaneCollider collider)
    {
        Rig = rig;
        Name = "airframe";
        CollisionLayer = CollisionLayers.Aircraft;
        CollisionMask = 0;     // a query target only — the body itself tests nothing
        SyncToPhysics = false; // no solver interaction to smooth; the node transform is the truth
        _partNames = new string[collider.Parts.Count];
        for (int i = 0; i < collider.Parts.Count; i++)
        {
            var p = collider.Parts[i];
            _partNames[i] = p.Name;
            AddChild(new CollisionShape3D { Shape = p.Shape, Transform = p.Local });
        }
    }

    /// <summary>The controller this body belongs to — how a struck body resolves to its plane.</summary>
    public FlightController Rig { get; }

    /// <summary>The identity of the rounds this plane fires (<see cref="FlightController.PlayerIndex"/>).</summary>
    public int PlayerIndex => Rig.PlayerIndex;

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
}
