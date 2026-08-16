using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Flight;

/// <summary>The player's classed candidate pool (<c>PLAN-targeting.md</c> B12): everything
/// selectable right now, split into the original's three cycles. Rebuilt from scratch on every
/// <see cref="Rebuild"/> call, which is the original's own contract (<c>FUN_004b5fb0</c> releases
/// the previous frame's list before walking the pools again, and nothing about it persists), so a
/// runtime spawn appears and a death disappears with no extra plumbing.
///
/// <para>It reads an <see cref="AimCandidateSet"/> rather than growing a parallel structure. The
/// aim assist's four typed lists ARE the original's four pools, and the collectors that fill them
/// (<see cref="ProjectilePool.CollectAircraft"/>, <see cref="ProjectilePool.CollectTurrets"/>,
/// <c>ZeppelinRuntime.CollectTargetParts</c>) are already written; what this adds is the class
/// split and the per-source identity that <see cref="TargetRef"/> carries.</para>
///
/// <para>Ordering is deliberately NOT done here. The cycle order (objectives first, then the
/// ahead/behind/left/right sector sort with distance inside a sector) is B13's, because it needs the
/// selecting plane's own basis, which a pool has no business holding.</para></summary>
public sealed class TargetPool
{
    private readonly List<TargetRef> _enemy = new();
    private readonly List<TargetRef> _ally = new();
    private readonly List<TargetRef> _nonAircraft = new();

    /// <summary>The Enemy/Objective cycle: a different, non-zero team, plus every objective.</summary>
    public IReadOnlyList<TargetRef> Enemy => _enemy;

    /// <summary>The Ally cycle: the same team, or either side unaffiliated.</summary>
    public IReadOnlyList<TargetRef> Ally => _ally;

    /// <summary>The Non-Aircraft cycle: turret emplacements and zeppelin sub-parts.</summary>
    public IReadOnlyList<TargetRef> NonAircraft => _nonAircraft;

    /// <summary>Everything selectable, across all three cycles.</summary>
    public int Count => _enemy.Count + _ally.Count + _nonAircraft.Count;

    /// <summary>One cycle by name, so a caller stepping "the current class" needs no switch of its
    /// own.</summary>
    public IReadOnlyList<TargetRef> Of(TargetClass cls) => cls switch
    {
        TargetClass.Enemy => _enemy,
        TargetClass.Ally => _ally,
        _ => _nonAircraft,
    };

    public void Clear()
    {
        _enemy.Clear();
        _ally.Clear();
        _nonAircraft.Clear();
    }

    /// <summary>Rebuilds all three cycles, classing each candidate through
    /// <see cref="TargetRef.Classify"/> against <paramref name="ownTeam"/> and dropping
    /// <paramref name="self"/> (matched by reference, the same self-rejection
    /// <see cref="AimAssist.Scan"/> uses; the original excludes the player's own plane inside the
    /// class function itself).
    ///
    /// <para>Two of the assist's four lists are read and two are NOT.
    /// <see cref="AimCandidateSet.Structures"/> is never touched: it is the destructible registry,
    /// an approximation of the original's curated <c>targets.zrd</c> list, and walking it would put
    /// every crate and fence in the world on the Non-Aircraft cycle (decision 8). Selectable
    /// structures arrive through <paramref name="subParts"/> alone.
    /// <see cref="AimCandidateSet.Ordnance"/> is not walked either: the original offers a live fused
    /// round only when its <c>+0x6c</c> tracking byte is set, CSVM has no such per-round flag, and
    /// decision 8 scopes the pool to aircraft, sub-parts and emplacements. Adding it later is one
    /// loop here, not a redesign.</para></summary>
    /// <param name="scan">The assist's typed lists, already filled by their collectors. Only
    /// <see cref="AimCandidateSet.Vehicles"/> and <see cref="AimCandidateSet.Turrets"/> are read.</param>
    /// <param name="subParts">The selectable mission structures, from
    /// <c>ZeppelinRuntime.CollectTargetParts</c>. Null for a session with no zeppelins.</param>
    /// <param name="ownTeam">The selecting plane's <see cref="FlightController.Team"/>. Read the
    /// FIELD, never <see cref="AimAssist.TeamOfPilot"/> — deriving a team from a pilot index is what
    /// put a wingman in the marker (see this module's architecture.md entry).</param>
    /// <param name="self">The selecting plane, excluded from its own pool.</param>
    public void Rebuild(AimCandidateSet scan, IReadOnlyList<AimCandidate>? subParts, int ownTeam,
        object? self)
    {
        Clear();
        foreach (var c in scan.Vehicles)
        {
            Offer(c, AimTargetKind.Vehicle, ownTeam, self);
        }

        foreach (var c in scan.Turrets)
        {
            Offer(c, AimTargetKind.Turret, ownTeam, self);
        }

        if (subParts == null)
        {
            return;
        }

        foreach (var c in subParts)
        {
            Offer(c, AimTargetKind.Structure, ownTeam, self);
        }
    }

    /// <summary>Files one already-built ref under its own class. Public so a caller with a source
    /// this pool does not know about can still contribute one.</summary>
    public void Add(in TargetRef target)
    {
        switch (target.Class)
        {
            case TargetClass.Enemy:
                _enemy.Add(target);
                break;
            case TargetClass.Ally:
                _ally.Add(target);
                break;
            default:
                _nonAircraft.Add(target);
                break;
        }
    }

    /// <summary>Whether a turret candidate stands in the world rather than being carried by an
    /// aircraft. Only emplacements are selectable: a carried gunner's host is already a target in
    /// its own right, and offering both would put two entries on one silhouette. An emplacement is
    /// the one with a placement <see cref="TurretController.Site"/>.</summary>
    private static bool IsEmplacement(object? source) =>
        source is TurretController { Site: not null };

    /// <summary>The IDENTITY name for a source: the plain node/label name. What <c>--target=</c>
    /// matches and what the breadcrumbs print — see <see cref="TargetRef.DisplayName"/> for what the
    /// marker prints instead.</summary>
    private static string NameOf(object? source) => source switch
    {
        FlightController fc => fc.Name,
        TurretController t => t.Label,
        DestructibleRegistry.Instance inst =>
            GodotObject.IsInstanceValid(inst.Anchor) ? inst.Anchor.Name : inst.Def.Name,
        _ => "",
    };

    /// <summary>Wraps one classed candidate as a <see cref="TargetRef"/>. The KIND picks the shape
    /// (which is why an unrecognised source still lands in the right cycle with an empty name rather
    /// than vanishing); the source supplies only the strings and the health figures. This is the one
    /// place in the targeting path that reads a concrete source type at all.</summary>
    private static TargetRef Describe(AimCandidate c, AimTargetKind kind, TargetClass cls)
    {
        string name = NameOf(c.Source);
        switch (kind)
        {
            case AimTargetKind.Vehicle:
                var plane = c.Source as FlightController;
                var dmg = plane?.Damage;
                // The MARKER prints the airframe's common name (C22, decision 10: plane type alone),
                // not the node name the selection is held and pinned by. A rig with no flight model
                // bound has no airframe to name, and falls back to that node name.
                return TargetRef.ForAircraft(c, cls, name,
                    plane?.Stats is { } stats ? PlaneRoster.PlaneDisplayName(stats) : null,
                    dmg == null ? null : TargetRef.Fraction(dmg.WholeHealth, dmg.WholeHealthMax),
                    dmg == null ? null : TargetRef.Fraction(dmg.WholeArmor, dmg.WholeArmorMax));
            case AimTargetKind.Turret:
                // No health figure exists for an emplacement: the retail loaders read no HEALTH key
                // and its aliveness is its healthy node's visibility.
                return TargetRef.ForTurret(c, cls, name);
            default:
                var inst = c.Source as DestructibleRegistry.Instance;
                return TargetRef.ForStructure(c, cls, name,
                    health: inst == null ? null : TargetRef.Fraction(inst.Health, inst.MaxHealth));
        }
    }

    private void Offer(AimCandidate c, AimTargetKind kind, int ownTeam, object? self)
    {
        if (c.Source == null || ReferenceEquals(c.Source, self))
        {
            return;
        }

        // CSVM carries no mission otherTarget/objectiveTarget data, so the flag the original reads
        // per entity is stood in for by what the candidate IS: a world emplacement and a sub-part
        // are selectable, a carried gunner is not. Everything reaching the Structure branch came
        // through subParts, so it is selectable by construction.
        bool otherTarget = kind switch
        {
            AimTargetKind.Turret => IsEmplacement(c.Source),
            AimTargetKind.Structure => true,
            _ => false,
        };
        if (TargetRef.Classify(kind, c.Live, c.Team, ownTeam, otherTarget) is not { } cls)
        {
            return;
        }

        Add(Describe(c, kind, cls));
    }
}
