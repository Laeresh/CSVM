using System.Collections.Generic;
using Godot;

namespace CSVM.Flight;

/// <summary>The only adapter over Godot's <c>DirectSpaceState</c>: resolves the live
/// <see cref="World3D"/> at each call rather than caching it, since the wrapped node may be bound
/// before it joins the tree.</summary>
public sealed class GodotWorldQuery : IWorldQuery
{
    private readonly Node3D _node;

    public GodotWorldQuery(Node3D node) => _node = node;

    public bool Sweep(IReadOnlyList<PlaneCollider.Part> parts, Transform3D baseTransform, Vector3 motion,
        uint mask, Godot.Collections.Array<Rid>? exclude, out SweepReport report)
    {
        report = default;
        var space = _node.GetWorld3D()?.DirectSpaceState;
        if (space == null)
            return false;

        float mLen = motion.Length();
        var normal = mLen > 1e-6f ? -motion / mLen : Vector3.Up;
        var contact = baseTransform.Origin + motion;
        float stopFrac = 1f;
        string part = "";
        string hitName = "";
        Node? hitBody = null;
        bool hit = false;
        foreach (var p in parts)
        {
            var query = new PhysicsShapeQueryParameters3D
            {
                Shape = p.Shape,
                Transform = baseTransform * p.Local,
                Motion = motion,
                CollisionMask = mask,
            };
            if (exclude != null)
                query.Exclude = exclude;
            var cast = space.CastMotion(query); // [safe, unsafe] fractions; [1,1] = clear
            if (cast[0] >= 1f || cast[0] >= stopFrac)
                continue;
            hit = true;
            stopFrac = cast[0];
            part = p.Name;
            // Slightly PAST the first-overlap pose: at exactly cast[1] GetRestInfo can come back
            // empty, leaving the wrong head-on fallback normal on what was really a shallow graze.
            query.Transform = query.Transform.Translated(
                motion * cast[1] + (mLen > 1e-6f ? motion / mLen * 0.05f : Vector3.Zero));
            query.Motion = Vector3.Zero;
            var rest = space.GetRestInfo(query);
            if (rest.Count > 0)
            {
                contact = (Vector3)rest["point"];
                normal = (Vector3)rest["normal"];
                hitBody = GodotObject.InstanceFromId((ulong)rest["collider_id"]) as Node;
                hitName = hitBody != null ? $"{hitBody.GetParent()?.Name}/{hitBody.Name}" : "world";
            }
            else
            {
                contact = (baseTransform * p.Local).Origin + motion * cast[1];
                normal = mLen > 1e-6f ? -motion / mLen : Vector3.Up;
                hitBody = null;
                hitName = "world";
            }
        }
        report = new SweepReport(stopFrac, contact, normal, part, hitBody, hitName);
        return hit;
    }

    public bool Ray(Vector3 from, Vector3 to, uint mask, Godot.Collections.Array<Rid>? exclude,
        out RayReport report)
    {
        report = default;
        var space = _node.GetWorld3D()?.DirectSpaceState;
        if (space == null)
            return false;
        var query = exclude != null
            ? PhysicsRayQueryParameters3D.Create(from, to, mask, exclude)
            : PhysicsRayQueryParameters3D.Create(from, to, mask);
        var hit = space.IntersectRay(query);
        if (hit.Count == 0)
            return false;
        report = new RayReport(
            (Vector3)hit["position"], (Vector3)hit["normal"], hit["collider"].Obj as Node);
        return true;
    }

    public bool Overlaps(IReadOnlyList<PlaneCollider.Part> parts, Transform3D pose, uint mask,
        Godot.Collections.Array<Rid>? exclude)
    {
        var space = _node.GetWorld3D()?.DirectSpaceState;
        if (space == null)
            return false;
        foreach (var p in parts)
        {
            var query = new PhysicsShapeQueryParameters3D
            {
                Shape = p.Shape,
                Transform = pose * p.Local,
                CollisionMask = mask,
            };
            if (exclude != null)
                query.Exclude = exclude;
            // One hit is enough — this only asks whether the box is free.
            if (space.IntersectShape(query, 1).Count > 0)
                return true;
        }

        return false;
    }
}
