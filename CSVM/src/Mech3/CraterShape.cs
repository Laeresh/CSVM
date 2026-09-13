using System;
using System.Collections.Generic;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// One crater as geometry: the rim ring laid at the impact, the two rings of the bowl under it, and
/// the 2D footprint the no-overlap rule compares. Every crater in the shipped game is this one
/// shape, because all six <c>CRATER</c> weapons author the block bare, which leaves the engine
/// template's three randomisation spans at zero. Decode: docs/org/craters.md. The field that holds
/// the carved ones is <see cref="CraterField"/>.
/// </summary>
public readonly struct CraterShape
{
    /// <summary>Rim vertices, the engine template's <c>POINTS</c> key.</summary>
    public const int RimPoints = 7;

    /// <summary>Rim radius in m, the engine template's <c>RADIUS</c> key.</summary>
    public const float RimRadius = 20f;

    /// <summary>The engine template's <c>DEPTH</c> key. The bowl is two rings deep, so its floor
    /// sits at twice this below the impact, not at this.</summary>
    public const float BowlDepth = 3f;

    /// <summary>How far a recorded footprint grows on all four sides before a new footprint is
    /// tested against it, so two craters can neither stack nor touch.</summary>
    public const float Clearance = 5f;

    private readonly Vector3[] _rim;

    private CraterShape(Vector3 impact, Vector3[] rim, float radius, float depth)
    {
        Impact = impact;
        _rim = rim;
        Radius = radius;
        Depth = depth;
    }

    /// <summary>The point the round struck, which is the height the rim ring is laid at.</summary>
    public Vector3 Impact { get; }

    /// <summary>The ring's radius in m.</summary>
    public float Radius { get; }

    /// <summary>One ring's drop. Both rings drop by it, hence a floor at twice it.</summary>
    public float Depth { get; }

    /// <summary>The rim ring, evenly spaced around the impact at <see cref="Radius"/> and at the
    /// impact's own height. A carve replaces these heights with the ground it clipped against;
    /// the ring recorded here is the one the footprint and the refusal read.</summary>
    public IReadOnlyList<Vector3> Rim => _rim;

    /// <summary>The centre the bowl is built about: the impact lowered by one <see cref="Depth"/>.
    /// The mid ring is measured to this point, not to the impact.</summary>
    public Vector3 BowlCentre => new(Impact.X, Impact.Y - Depth, Impact.Z);

    /// <summary>The bowl's apex, a further <see cref="Depth"/> under <see cref="BowlCentre"/>.</summary>
    public Vector3 Floor => new(Impact.X, Impact.Y - (2f * Depth), Impact.Z);

    /// <summary>The ring's 2D bounding box in world XZ, which is what the original records on the
    /// instance and what every later carve is refused against.</summary>
    public Rect2 Footprint
    {
        get
        {
            float minX = _rim[0].X, maxX = _rim[0].X, minZ = _rim[0].Z, maxZ = _rim[0].Z;
            foreach (var v in _rim)
            {
                minX = Mathf.Min(minX, v.X);
                maxX = Mathf.Max(maxX, v.X);
                minZ = Mathf.Min(minZ, v.Z);
                maxZ = Mathf.Max(maxZ, v.Z);
            }
            return new Rect2(minX, minZ, maxX - minX, maxZ - minZ);
        }
    }

    /// <summary>Lays the ring for an impact. The defaults are the shipped engine template; the
    /// parameters exist because the block can author min/span pairs, not because anything does.</summary>
    public static CraterShape At(Vector3 impact, int points = RimPoints, float radius = RimRadius,
        float depth = BowlDepth)
    {
        points = Math.Max(3, points);
        var rim = new Vector3[points];
        for (int i = 0; i < points; i++)
        {
            float angle = Mathf.Tau * i / points;
            rim[i] = new Vector3(impact.X + (radius * Mathf.Cos(angle)), impact.Y,
                impact.Z + (radius * Mathf.Sin(angle)));
        }
        return new CraterShape(impact, rim, radius, depth);
    }

    /// <summary>Whether a decoration standing here is inside the crater. A squared XZ distance
    /// against the radius, with no height test: the original compares origins alone.</summary>
    public bool Covers(Vector3 point)
    {
        float dx = point.X - Impact.X;
        float dz = point.Z - Impact.Z;
        return ((dx * dx) + (dz * dz)) <= Radius * Radius;
    }

    /// <summary>Whether this footprint stays clear of an already carved one. False is a refusal:
    /// the recorded box grows by <see cref="Clearance"/> on all four sides and any overlap with it
    /// loses, which is why bombing one spot twice produces one crater and then nothing.</summary>
    public bool Clears(in CraterShape carved) =>
        !carved.Footprint.Grow(Clearance).Intersects(Footprint);

    /// <summary>The bowl's mid ring for a rim, each vertex halfway to <see cref="BowlCentre"/> and
    /// then lowered by <see cref="Depth"/> again. Takes the rim rather than reading
    /// <see cref="Rim"/> so a carve can pass the ring it clipped against the ground.</summary>
    public Vector3[] MidRing(IReadOnlyList<Vector3> rim)
    {
        var centre = BowlCentre;
        var mid = new Vector3[rim.Count];
        for (int i = 0; i < rim.Count; i++)
        {
            var half = (rim[i] + centre) * 0.5f;
            mid[i] = new Vector3(half.X, half.Y - Depth, half.Z);
        }
        return mid;
    }
}
