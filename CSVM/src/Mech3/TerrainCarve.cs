using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Cuts one crater bowl into one world node's terrain. The ring is subtracted from every ground
/// triangle it covers, and the hole's rim is re-laid on the ground it was clipped against. The
/// bowl is emitted as one further surface in that ground's own skin. The node gets a PRIVATE mesh
/// and private collision shapes, un-shared from <see cref="SceneBuilder"/>'s per-mesh-index
/// caches. A carve therefore never reaches the other instances of the same model. Decode:
/// docs/org/craters.md; called only by <see cref="CraterField"/>, which owns the refusal and the
/// record list.
/// </summary>
internal static class TerrainCarve
{
    // Areas and barycentric weights under this are noise. A source triangle that degenerates in XZ
    // has no invertible ground map, and a clipped piece this small emits nothing worth a draw.
    private const float AreaEpsilon = 1e-4f;

    // How far above and below the impact a collision face must sit to be cut away with the ground.
    // Without it a wall or a roof carried by the same node loses faces to a crater on the street.
    private const float CollisionBand = 2f;

    /// <summary>Carves <paramref name="shape"/> into <paramref name="owner"/>'s mesh, and into
    /// <paramref name="struck"/>'s trimesh when the round hit one. Null when the node carries no
    /// ArrayMesh or when the ring met no ground, which is the original's Clip Failed.</summary>
    internal static Cut? Carve(Node3D owner, CollisionObject3D? struck, in CraterShape shape)
    {
        if (owner.GetNodeOrNull<MeshInstance3D>("mesh") is not { Mesh: ArrayMesh src } mi)
        {
            return null;
        }

        var toWorld = mi.GlobalTransform;
        var toLocal = toWorld.AffineInverse();
        var ring = Ring(shape);
        var box = shape.Footprint;
        var samples = new Vtx?[ring.Length];
        Vtx? centre = null;
        float winding = 0f;

        var built = new ArrayMesh();
        int removed = 0, added = 0, bestCut = 0;
        Material? skin = null;
        for (int s = 0; s < src.GetSurfaceCount(); s++)
        {
            var arrays = src.SurfaceGetArrays(s);
            int cut = CutSurface(src, s, arrays, toWorld, toLocal, ring, box, samples, ref centre,
                ref winding, ref removed, ref added, out var rebuilt);
            built.AddSurfaceFromArrays(src.SurfaceGetPrimitiveType(s), rebuilt ?? arrays);
            built.SurfaceSetMaterial(built.GetSurfaceCount() - 1, src.SurfaceGetMaterial(s));
            if (cut > bestCut)
            {
                bestCut = cut;
                skin = src.SurfaceGetMaterial(s);
            }
        }

        if (bestCut == 0)
        {
            return null;
        }

        var bowl = Bowl(shape, ring, samples, centre, winding, toLocal, out int rimOnGround);
        built.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, bowl.Arrays);
        built.SurfaceSetMaterial(built.GetSurfaceCount() - 1, skin);
        mi.Mesh = built;
        int opened = CarveCollision(owner, struck, shape, bowl.Faces);
        return new Cut(removed, added, bowl.Faces.Length / 3, rimOnGround, opened);
    }

    // The ring in world XZ, wound counter-clockwise so every edge's inside is its left side. The
    // laid ring is convex, so one orientation test over the whole loop settles it.
    private static Vector2[] Ring(in CraterShape shape)
    {
        var rim = shape.Rim;
        var ring = new Vector2[rim.Count];
        for (int i = 0; i < rim.Count; i++)
        {
            ring[i] = new Vector2(rim[i].X, rim[i].Z);
        }
        if (SignedArea(ring) < 0f)
        {
            Array.Reverse(ring);
        }
        return ring;
    }

    // One surface. A triangle whose XZ box meets the ring's and whose ground faces up is replaced
    // by the convex pieces of itself outside the ring. Everything else is copied through. Returns
    // how many triangles were cut, and leaves `rebuilt` null when none were.
    private static int CutSurface(ArrayMesh src, int surface, Godot.Collections.Array arrays,
        Transform3D toWorld, Transform3D toLocal, Vector2[] ring, Rect2 box, Vtx?[] samples,
        ref Vtx? centre, ref float winding, ref int removed, ref int added,
        out Godot.Collections.Array? rebuilt)
    {
        rebuilt = null;
        if (src.SurfaceGetPrimitiveType(surface) != Mesh.PrimitiveType.Triangles)
        {
            return 0;
        }
        var source = Channels.Of(arrays, toWorld);
        if (source.Count < 3)
        {
            return 0;
        }

        var output = new Channels();
        int cut = 0;
        for (int t = 0; t + 2 < source.Count; t += 3)
        {
            var a = source.At(t);
            var b = source.At(t + 1);
            var c = source.At(t + 2);
            var pieces = Overlaps(a.Pos, b.Pos, c.Pos, box) && IsGround(a, b, c)
                ? Difference(a.Pos, b.Pos, c.Pos, ring, out _)
                : null;
            if (pieces == null)
            {
                output.Add(a);
                output.Add(b);
                output.Add(c);
                continue;
            }
            Sample(a, b, c, ring, samples, ref centre, ref winding);
            cut++;
            removed++;
            foreach (var piece in pieces)
            {
                for (int i = 1; i + 1 < piece.Count; i++)
                {
                    output.Add(Lift(a, b, c, piece[0]));
                    output.Add(Lift(a, b, c, piece[i]));
                    output.Add(Lift(a, b, c, piece[i + 1]));
                    added++;
                }
            }
        }

        if (cut > 0)
        {
            rebuilt = output.ToArrays(toLocal);
        }
        return cut;
    }

    private static bool Overlaps(Vector3 a, Vector3 b, Vector3 c, Rect2 box)
    {
        float minX = Mathf.Min(a.X, Mathf.Min(b.X, c.X));
        float maxX = Mathf.Max(a.X, Mathf.Max(b.X, c.X));
        float minZ = Mathf.Min(a.Z, Mathf.Min(b.Z, c.Z));
        float maxZ = Mathf.Max(a.Z, Mathf.Max(b.Z, c.Z));
        return maxX >= box.Position.X && minX <= box.End.X
            && maxZ >= box.Position.Y && minZ <= box.End.Y;
    }

    // Only upward ground takes a crater. The stored normal decides which side the source meant to
    // be the sky. The XZ area guard drops a wall, whose whole footprint is one line anyway.
    private static bool IsGround(in Vtx a, in Vtx b, in Vtx c)
    {
        var g = (b.Pos - a.Pos).Cross(c.Pos - a.Pos);
        return Mathf.Abs(g.Y) > AreaEpsilon && (a.Normal.Y + b.Normal.Y + c.Normal.Y) > 0f;
    }

    // The triangle minus the convex ring, as convex pieces. Incremental: each edge's outside half
    // is one piece, and what stays inside is clipped by the next edge. The pieces therefore tile
    // the difference exactly and share the ring's own edges with the bowl. Null means nothing was
    // cut.
    private static List<List<Vector2>>? Difference(Vector3 a, Vector3 b, Vector3 c, Vector2[] ring,
        out List<Vector2> inside)
    {
        var work = new List<Vector2>
        {
            new(a.X, a.Z),
            new(b.X, b.Z),
            new(c.X, c.Z),
        };
        var pieces = new List<List<Vector2>>();
        for (int i = 0; i < ring.Length && work.Count >= 3; i++)
        {
            var e0 = ring[i];
            var e1 = ring[(i + 1) % ring.Length];
            var outside = ClipHalf(work, e0, e1, keepInside: false);
            if (Area(outside) > AreaEpsilon)
            {
                pieces.Add(outside);
            }
            work = ClipHalf(work, e0, e1, keepInside: true);
        }
        inside = work;
        return Area(work) > AreaEpsilon ? pieces : null;
    }

    private static List<Vector2> ClipHalf(List<Vector2> poly, Vector2 e0, Vector2 e1, bool keepInside)
    {
        var clipped = new List<Vector2>(poly.Count + 2);
        for (int i = 0; i < poly.Count; i++)
        {
            var cur = poly[i];
            var next = poly[(i + 1) % poly.Count];
            float sc = Side(e0, e1, cur), sn = Side(e0, e1, next);
            if (!keepInside)
            {
                sc = -sc;
                sn = -sn;
            }
            if (sc >= 0f)
            {
                clipped.Add(cur);
            }
            if ((sc >= 0f) != (sn >= 0f) && sc != sn)
            {
                clipped.Add(cur.Lerp(next, sc / (sc - sn)));
            }
        }
        return clipped;
    }

    private static float Side(Vector2 a, Vector2 b, Vector2 p) =>
        ((b.X - a.X) * (p.Y - a.Y)) - ((b.Y - a.Y) * (p.X - a.X));

    private static float SignedArea(IReadOnlyList<Vector2> poly)
    {
        float twice = 0f;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            twice += (a.X * b.Y) - (b.X * a.Y);
        }
        return twice * 0.5f;
    }

    private static float Area(List<Vector2> poly) =>
        poly.Count < 3 ? 0f : Mathf.Abs(SignedArea(poly));

    // A new boundary vertex takes every channel from the triangle it was cut out of, by the
    // barycentric weights of its XZ position. The patched ground keeps its height and its skin.
    private static Vtx Lift(in Vtx a, in Vtx b, in Vtx c, Vector2 p)
    {
        Weights(a.Pos, b.Pos, c.Pos, p, out float wa, out float wb, out float wc);
        return new Vtx
        {
            Pos = new Vector3(p.X, (a.Pos.Y * wa) + (b.Pos.Y * wb) + (c.Pos.Y * wc), p.Y),
            Normal = ((a.Normal * wa) + (b.Normal * wb) + (c.Normal * wc)).Normalized(),
            Color = (a.Color * wa) + (b.Color * wb) + (c.Color * wc),
            Uv = (a.Uv * wa) + (b.Uv * wb) + (c.Uv * wc),
        };
    }

    private static void Weights(Vector3 a, Vector3 b, Vector3 c, Vector2 p,
        out float wa, out float wb, out float wc)
    {
        float d = ((b.Z - c.Z) * (a.X - c.X)) + ((c.X - b.X) * (a.Z - c.Z));
        if (Mathf.Abs(d) < AreaEpsilon)
        {
            wa = 1f;
            wb = 0f;
            wc = 0f;
            return;
        }
        wa = (((b.Z - c.Z) * (p.X - c.X)) + ((c.X - b.X) * (p.Y - c.Z))) / d;
        wb = (((c.Z - a.Z) * (p.X - c.X)) + ((a.X - c.X) * (p.Y - c.Z))) / d;
        wc = 1f - wa - wb;
    }

    // Records the ground under each rim vertex and under the centre. That is what makes the rim
    // follow the real terrain instead of a flat circle. Also settles, once, whether this mesh's
    // winding puts the face normal along or against the stored one, for the bowl to match.
    private static void Sample(in Vtx a, in Vtx b, in Vtx c, Vector2[] ring, Vtx?[] samples,
        ref Vtx? centre, ref float winding)
    {
        if (winding == 0f)
        {
            var g = (b.Pos - a.Pos).Cross(c.Pos - a.Pos);
            winding = g.Dot(a.Normal) >= 0f ? 1f : -1f;
        }
        for (int i = 0; i < ring.Length; i++)
        {
            if (samples[i] == null && Inside(a.Pos, b.Pos, c.Pos, ring[i]))
            {
                samples[i] = Lift(a, b, c, ring[i]);
            }
        }
        if (centre == null)
        {
            var middle = Centroid(ring);
            if (Inside(a.Pos, b.Pos, c.Pos, middle))
            {
                centre = Lift(a, b, c, middle);
            }
        }
    }

    private static Vector2 Centroid(Vector2[] ring)
    {
        var sum = Vector2.Zero;
        foreach (var p in ring)
        {
            sum += p;
        }
        return sum / ring.Length;
    }

    private static bool Inside(Vector3 a, Vector3 b, Vector3 c, Vector2 p)
    {
        Weights(a, b, c, p, out float wa, out float wb, out float wc);
        return wa >= -1e-3f && wb >= -1e-3f && wc >= -1e-3f;
    }

    // The bowl: the sampled rim, a mid ring halfway to a centre lowered by DEPTH and lowered again,
    // and an apex a further DEPTH down. Its base height is the LOWEST of the sampled rim and the
    // sampled centre. A crater on a slope then closes downward instead of turning inside out.
    private static (Godot.Collections.Array Arrays, Vector3[] Faces) Bowl(in CraterShape shape,
        Vector2[] ring, Vtx?[] samples, Vtx? centre, float winding, Transform3D toLocal,
        out int rimOnGround)
    {
        int n = ring.Length;
        var rim = new Vtx[n];
        var rimPos = new Vector3[n];
        rimOnGround = 0;
        float baseY = centre?.Pos.Y ?? shape.Impact.Y;
        for (int i = 0; i < n; i++)
        {
            rim[i] = samples[i] ?? new Vtx
            {
                Pos = new Vector3(ring[i].X, shape.Impact.Y, ring[i].Y),
                Normal = Vector3.Up,
                Color = Colors.White,
                Uv = Vector2.Zero,
            };
            if (samples[i] != null)
            {
                rimOnGround++;
            }
            rimPos[i] = rim[i].Pos;
            baseY = Mathf.Min(baseY, rim[i].Pos.Y);
        }

        var sunk = CraterShape.At(new Vector3(shape.Impact.X, baseY, shape.Impact.Z),
            n, shape.Radius, shape.Depth);
        var midPos = sunk.MidRing(rimPos);
        var hub = centre ?? rim[0];
        var faces = new List<Vector3>();
        var output = new Channels();
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            var midI = Blend(rim[i], hub, midPos[i], 0.5f);
            var midJ = Blend(rim[j], hub, midPos[j], 0.5f);
            var apex = Blend(hub, hub, sunk.Floor, 0f);
            Emit(output, faces, rim[i], rim[j], midJ, winding);
            Emit(output, faces, rim[i], midJ, midI, winding);
            Emit(output, faces, midI, midJ, apex, winding);
        }
        return (output.ToArrays(toLocal), faces.ToArray());
    }

    private static Vtx Blend(in Vtx a, in Vtx b, Vector3 at, float t) => new()
    {
        Pos = at,
        Normal = Vector3.Up,
        Color = a.Color.Lerp(b.Color, t),
        Uv = a.Uv.Lerp(b.Uv, t),
    };

    // One bowl triangle, wound so its outward face points up under this mesh's own convention, and
    // flat-shaded from that same normal. A bowl is concave, so every face of it does face upward.
    private static void Emit(Channels output, List<Vector3> faces, Vtx a, Vtx b, Vtx c, float winding)
    {
        var outward = (b.Pos - a.Pos).Cross(c.Pos - a.Pos) * (winding >= 0f ? 1f : -1f);
        if (outward.Y < 0f)
        {
            (b, c) = (c, b);
            outward = -outward;
        }
        var normal = outward.LengthSquared() > 0f ? outward.Normalized() : Vector3.Up;
        a.Normal = normal;
        b.Normal = normal;
        c.Normal = normal;
        output.Add(a);
        output.Add(b);
        output.Add(c);
        faces.Add(a.Pos);
        faces.Add(b.Pos);
        faces.Add(c.Pos);
    }

    // The struck body's own trimesh takes the SAME subtraction the skin took, and gains the bowl,
    // in a NEW shape. SceneBuilder caches one shape per mesh index and shares it across every
    // instance of that model. Writing the old one would hole every copy of the same hill.
    // ⚠ Do not drop a face by a centroid-in-ring test. The terrain's triangles are far larger than
    // the crater, so a centroid test removes nothing (docs/verification.md WORLD-46).
    private static int CarveCollision(Node3D owner, CollisionObject3D? struck, in CraterShape shape,
        Vector3[] bowlFaces)
    {
        if (struck is not StaticBody3D body || body.GetParent() != owner)
        {
            return 0;
        }
        var ring = Ring(shape);
        var box = shape.Footprint;
        var bodyToWorld = body.GlobalTransform;
        var bodyToLocal = bodyToWorld.AffineInverse();
        float band = (2f * shape.Depth) + CollisionBand;
        int opened = 0;
        bool bowlPlaced = false;
        for (int slotIndex = 0, count = body.GetChildCount(); slotIndex < count; slotIndex++)
        {
            if (body.GetChild(slotIndex) is not CollisionShape3D slot || slot.Shape is not ConcavePolygonShape3D trimesh)
            {
                continue;
            }
            var faces = trimesh.GetFaces();
            var kept = new List<Vector3>(faces.Length);
            int cut = 0;
            for (int t = 0; t + 2 < faces.Length; t += 3)
            {
                var a = bodyToWorld * faces[t];
                var b = bodyToWorld * faces[t + 1];
                var c = bodyToWorld * faces[t + 2];
                var inside = new List<Vector2>();
                var pieces = Overlaps(a, b, c, box) && Mathf.Abs((b - a).Cross(c - a).Y) > AreaEpsilon
                    ? Difference(a, b, c, ring, out inside)
                    : null;
                if (pieces != null && !InBand(a, b, c, inside, shape.Impact.Y, band))
                {
                    pieces = null;
                }
                if (pieces == null)
                {
                    kept.Add(faces[t]);
                    kept.Add(faces[t + 1]);
                    kept.Add(faces[t + 2]);
                    continue;
                }
                cut++;
                foreach (var piece in pieces)
                {
                    for (int i = 1; i + 1 < piece.Count; i++)
                    {
                        kept.Add(bodyToLocal * Raise(a, b, c, piece[0]));
                        kept.Add(bodyToLocal * Raise(a, b, c, piece[i]));
                        kept.Add(bodyToLocal * Raise(a, b, c, piece[i + 1]));
                    }
                }
            }
            if (cut == 0)
            {
                continue;
            }
            opened += cut;
            if (!bowlPlaced)
            {
                bowlPlaced = true;
                Line(kept, bowlFaces, bodyToLocal);
            }
            // ⚠ Hand the slot the EMPTY shape and fill it afterwards, or the body answers nothing
            // for the rest of the frame (docs/verification.md WORLD-45).
            var carved = new ConcavePolygonShape3D { BackfaceCollision = trimesh.BackfaceCollision };
            slot.Shape = carved;
            carved.SetFaces(kept.ToArray());
        }

        return opened;
    }

    // The bowl's walls added to the shape whose ground they replace, each face in both windings.
    // The second winding is why the slot's own sidedness need not be guessed.
    // ⚠ Do not give the bowl a CollisionShape3D of its own. A shape a node registers on entering
    // the tree answers no ray in the same frame. A carve and a probe in one frame would then read
    // straight through the hole.
    private static void Line(List<Vector3> kept, Vector3[] bowlFaces, Transform3D bodyToLocal)
    {
        for (int t = 0; t + 2 < bowlFaces.Length; t += 3)
        {
            var a = bodyToLocal * bowlFaces[t];
            var b = bodyToLocal * bowlFaces[t + 1];
            var c = bodyToLocal * bowlFaces[t + 2];
            kept.Add(a);
            kept.Add(b);
            kept.Add(c);
            kept.Add(a);
            kept.Add(c);
            kept.Add(b);
        }
    }

    // Whether the part of a face that falls inside the ring stands at the impact's own height. A
    // wall or a roof carried by the same body reaches over the ring without being the ground the
    // round struck. This band is what leaves it alone.
    private static bool InBand(Vector3 a, Vector3 b, Vector3 c, List<Vector2> inside, float y,
        float band)
    {
        foreach (var p in inside)
        {
            if (Mathf.Abs(Raise(a, b, c, p).Y - y) <= band)
            {
                return true;
            }
        }
        return false;
    }

    // An XZ point lifted onto a triangle's own plane. A clipped collision piece keeps the height of
    // the ground it was cut out of. The skin's Lift does this and the channels too.
    private static Vector3 Raise(Vector3 a, Vector3 b, Vector3 c, Vector2 p)
    {
        Weights(a, b, c, p, out float wa, out float wb, out float wc);
        return new Vector3(p.X, (a.Y * wa) + (b.Y * wb) + (c.Y * wc), p.Y);
    }

    /// <summary>What one carve did, for the suite and the log line. <c>Removed</c> and <c>Added</c>
    /// count the ground triangles the subtraction replaced in the skin. <c>RimOnGround</c> counts
    /// the rim vertices that found real ground under them rather than falling back to the impact's
    /// height. <c>Opened</c> counts the collision faces the same subtraction replaced.</summary>
    internal readonly record struct Cut(int Removed, int Added, int BowlTriangles, int RimOnGround,
        int Opened);

    private struct Vtx
    {
        public Vector3 Pos;
        public Vector3 Normal;
        public Color Color;
        public Vector2 Uv;
    }

    // One surface's channels as plain lists in WORLD space, read out of (and written back into) the
    // ArrayMesh's own array form. Non-indexed on the way out: a carve rewrites one node's mesh
    // once. The shared vertices it loses cost far less than a second pass to rebuild the index.
    private sealed class Channels
    {
        private readonly List<Vector3> _pos = new();
        private readonly List<Vector3> _normal = new();
        private readonly List<Color> _color = new();
        private readonly List<Vector2> _uv = new();

        internal int Count => _pos.Count;

        internal static Channels Of(Godot.Collections.Array arrays, Transform3D toWorld)
        {
            var pos = Has(arrays, Mesh.ArrayType.Vertex)
                ? arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array() : Array.Empty<Vector3>();
            var normal = Has(arrays, Mesh.ArrayType.Normal)
                ? arrays[(int)Mesh.ArrayType.Normal].AsVector3Array() : Array.Empty<Vector3>();
            var color = Has(arrays, Mesh.ArrayType.Color)
                ? arrays[(int)Mesh.ArrayType.Color].AsColorArray() : Array.Empty<Color>();
            var uv = Has(arrays, Mesh.ArrayType.TexUV)
                ? arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array() : Array.Empty<Vector2>();
            var index = Has(arrays, Mesh.ArrayType.Index)
                ? arrays[(int)Mesh.ArrayType.Index].AsInt32Array() : Array.Empty<int>();
            bool hasNormal = normal.Length == pos.Length;
            bool hasColor = color.Length == pos.Length;
            bool hasUv = uv.Length == pos.Length;
            var built = new Channels();
            int count = index.Length > 0 ? index.Length : pos.Length;
            for (int i = 0; i < count; i++)
            {
                int v = index.Length > 0 ? index[i] : i;
                built.Add(new Vtx
                {
                    Pos = toWorld * pos[v],
                    Normal = hasNormal ? (toWorld.Basis * normal[v]).Normalized() : Vector3.Up,
                    Color = hasColor ? color[v] : Colors.White,
                    Uv = hasUv ? uv[v] : Vector2.Zero,
                });
            }
            return built;
        }

        internal Vtx At(int i) => new()
        {
            Pos = _pos[i],
            Normal = _normal[i],
            Color = _color[i],
            Uv = _uv[i],
        };

        internal void Add(in Vtx v)
        {
            _pos.Add(v.Pos);
            _normal.Add(v.Normal);
            _color.Add(v.Color);
            _uv.Add(v.Uv);
        }

        internal Godot.Collections.Array ToArrays(Transform3D toLocal)
        {
            var pos = new Vector3[_pos.Count];
            var normal = new Vector3[_pos.Count];
            for (int i = 0; i < _pos.Count; i++)
            {
                pos[i] = toLocal * _pos[i];
                normal[i] = (toLocal.Basis * _normal[i]).Normalized();
            }
            var arrays = new Godot.Collections.Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = pos;
            arrays[(int)Mesh.ArrayType.Normal] = normal;
            arrays[(int)Mesh.ArrayType.Color] = _color.ToArray();
            arrays[(int)Mesh.ArrayType.TexUV] = _uv.ToArray();
            return arrays;
        }

        private static bool Has(Godot.Collections.Array arrays, Mesh.ArrayType channel) =>
            (int)channel < arrays.Count && arrays[(int)channel].VariantType != Variant.Type.Nil;
    }
}
