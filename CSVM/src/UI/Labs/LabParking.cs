using CSVM.Flight.Airframe;
using Godot;

namespace CSVM.UI.Labs;

/// <summary>
/// Where a lab may park an aircraft along a line without burying it. The weapon lab backs up its aim
/// line by the stand-off, and the lighting lab parks ahead of the camera. Both ask the same ray, so a
/// hillside or a warehouse shortens either the same way. Call it from a physics step.
/// </summary>
public static class LabParking
{
    /// <summary>Metres off the start point the probe begins, so a face the start sits on is not the obstruction.</summary>
    public const float ClearanceProbeStart = 0.5f;

    /// <summary>Metres kept clear of whatever the probe struck.</summary>
    public const float ClearanceMargin = 2f;

    /// <summary>How far along <paramref name="dir"/> from <paramref name="from"/> is clear of world
    /// geometry, up to <paramref name="want"/>. A struck collider shortens it to
    /// <see cref="ClearanceMargin"/> short of the hit, never under <paramref name="min"/>.
    /// <c>Blocked</c> is the hit's distance, null when nothing was struck.</summary>
    public static (float Reach, float? Blocked) ClearReach(PhysicsDirectSpaceState3D space, Vector3 from, Vector3 dir,
        float want, float min)
    {
        var query = PhysicsRayQueryParameters3D.Create(from + (dir * ClearanceProbeStart), from + (dir * want), CollisionLayers.World);
        var hit = space.IntersectRay(query);
        if (hit.Count == 0)
            return (want, null);
        float blocked = (hit["position"].AsVector3() - from).Length();
        return (Mathf.Max(min, blocked - ClearanceMargin), blocked);
    }
}
