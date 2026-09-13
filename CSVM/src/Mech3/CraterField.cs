using System;
using System.Collections.Generic;
using CSVM.Utils;
using Godot;

namespace CSVM.Mech3;

/// <summary>
/// Every crater one mission has carved, and the rule that decides whether it may carve another. A
/// crater is permanent: nothing ages one out, nothing recycles one, and the carve is merged into the
/// terrain rather than pooled. What holds the count down is the refusal, a footprint coming within
/// <see cref="CraterShape.Clearance"/> of a carved one loses outright, so a mission accumulates a
/// bounded scatter of non-overlapping bowls. Decode: docs/org/craters.md. The geometry is
/// <see cref="CraterShape"/>, the mesh surgery <see cref="TerrainCarve"/>.
/// </summary>
public sealed class CraterField
{
    private readonly Node3D _world;
    private readonly List<CraterShape> _craters = new();

    /// <param name="world">The built world root, under which the terrain nodes and the
    /// <c>clutter</c> subtree stand.</param>
    public CraterField(Node3D world)
    {
        _world = world;
    }

    /// <summary>Why a carve request ended, mirroring the original's one success and its Build and
    /// Clip failures. Only <see cref="Result.Carved"/> suppresses the weapon's impact animation.</summary>
    public enum Result
    {
        /// <summary>The bowl is in the terrain and the decorations inside it are gone.</summary>
        Carved,

        /// <summary>The footprint came within the clearance of an already carved one.</summary>
        Overlaps,

        /// <summary>The round struck nothing that carries terrain geometry.</summary>
        NoTerrain,

        /// <summary>The ring met no ground triangle on the node that was struck.</summary>
        ClipFailed,
    }

    /// <summary>The craters carved so far, oldest first. Never shortened.</summary>
    public IReadOnlyList<CraterShape> Craters => _craters;

    /// <summary>Decorations destroyed by every carve so far.</summary>
    public int DecorationsDestroyed { get; private set; }

    /// <summary>Whether an already carved crater refuses this one. The rule is per crater and never
    /// expires, so a list that only grows is what bounds a mission's crater count.</summary>
    public static bool Refused(IReadOnlyList<CraterShape> carved, in CraterShape next)
    {
        foreach (var one in carved)
        {
            if (!next.Clears(one))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Requests a crater at a round's impact point on the body it struck, and says what
    /// happened. The caller is <c>ProjectilePool</c>'s crater sink.</summary>
    public Result Request(Vector3 impact, Node? struck)
    {
        var shape = CraterShape.At(impact);
        if (Refused(_craters, shape))
        {
            return Result.Overlaps;
        }
        if (struck is not CollisionObject3D body || WorldCollision.OwnerOf(body) is not Node3D owner)
        {
            return Result.NoTerrain;
        }
        // Both halves are timed on the log line: a carve runs inside the projectile tick, and the
        // line is the only reading of what one costs a flight.
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        if (TerrainCarve.Carve(owner, body, shape) is not { } cut)
        {
            return Result.ClipFailed;
        }

        long carved = System.Diagnostics.Stopwatch.GetTimestamp();
        _craters.Add(shape);
        int flattened = ClutterCull.Destroy(_world.GetNodeOrNull<Node3D>("clutter"), shape);
        long culled = System.Diagnostics.Stopwatch.GetTimestamp();
        DecorationsDestroyed += flattened;
        double perMs = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        string where = FormattableString.Invariant($"({impact.X:0},{impact.Y:0},{impact.Z:0}) on {owner.Name}");
        string ground = FormattableString.Invariant($"{cut.Removed} ground triangles replaced by {cut.Added}");
        string bowl = FormattableString.Invariant($"{cut.BowlTriangles} bowl triangles over {cut.Opened} opened collision faces, {cut.RimOnGround}/{shape.Rim.Count} rim on ground");
        string cost = FormattableString.Invariant($"carve_ms={(carved - start) * perMs:0.0} cull_ms={(culled - carved) * perMs:0.0}");
        Log.Info("world", $"crater {_craters.Count} at {where}: {ground}, {bowl}, {flattened} decorations destroyed, {cost}");
        return Result.Carved;
    }

    /// <summary>The sink shape <c>ProjectilePool</c> holds: true only when the carve landed, which
    /// is what the original ANDs into its animation-suppression flag.</summary>
    public bool TryCarve(Vector3 impact, Node? struck) => Request(impact, struck) == Result.Carved;
}
