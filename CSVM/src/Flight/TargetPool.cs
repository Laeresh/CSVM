using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Session;
using Godot;

namespace CSVM.Flight;

/// <summary>The player's classed candidate pool: everything selectable right now, split into the
/// original's three cycles. Rebuilt from scratch on every <see cref="Rebuild"/> call, which is the
/// original's own contract, so a runtime spawn appears and a death disappears with no extra
/// plumbing. It reads an <see cref="AimCandidateSet"/> rather than growing a parallel structure:
/// the assist's four typed lists ARE the original's four pools and their collectors are already
/// written, so what this adds is the class split and <see cref="TargetRef"/>'s identity.
/// ⚠ Ordering does not belong here. The cycle order needs the selecting plane's own basis, which a
/// pool has no business holding; it is <see cref="TargetSelection"/>'s.</summary>
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

    /// <summary>Rebuilds all three cycles through <see cref="TargetRef.Classify"/>, dropping
    /// <paramref name="self"/> by reference. Structures reach it through
    /// <paramref name="subParts"/> and the mission's <c>targets.zrd</c> SITES through
    /// <paramref name="objectives"/>; a marker-carrying aeroplane arrives on its own vehicle
    /// candidate, never twice. ⚠ Never walk <see cref="AimCandidateSet.Structures"/>: every crate
    /// would land on a cycle. ⚠ <paramref name="ownTeam"/> is the <c>FlightController.Team</c> FIELD.</summary>
    public void Rebuild(AimCandidateSet scan, IReadOnlyList<AimCandidate>? subParts, int ownTeam,
        object? self, IReadOnlyList<AimCandidate>? objectives = null)
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

        // The fourth pool. Its collector lists every round the engine wraps, fused or TARGETABLE;
        // only a TARGETABLE one carries a source, so the admission byte is what admits it here.
        foreach (var c in scan.Ordnance)
        {
            Offer(c, AimTargetKind.Ordnance, ownTeam, self);
        }

        if (subParts != null)
        {
            foreach (var c in subParts)
            {
                Offer(c, AimTargetKind.Structure, ownTeam, self);
            }
        }

        if (objectives == null)
        {
            return;
        }

        foreach (var c in objectives)
        {
            Offer(c, AimTargetKind.Structure, ownTeam, self, objectiveTarget: true);
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

    /// <summary>The IDENTITY name for a source: the plain node/label name. What <c>--target=</c>
    /// matches and what the breadcrumbs print — see <see cref="TargetRef.DisplayName"/> for what the
    /// marker prints instead. Internal rather than private: <c>FlightController.SelectRankedTarget</c>
    /// (D12/D36) reuses this same identity for a turret/structure candidate's
    /// <c>rating_biases</c> name match, rather than growing a second name-of-source switch.</summary>
    internal static string NameOf(object? source) => source switch
    {
        FlightController fc => fc.Name,
        ObjectiveSite site => site.Node,
        TurretController t => t.Label,
        ProjectilePool.Flyout f => f.Name,
        DestructibleRegistry.Instance inst =>
            GodotObject.IsInstanceValid(inst.Anchor) ? inst.Anchor.Name : inst.Def.Name,
        _ => "",
    };

    /// <summary>The name of the entity a candidate is a PART of, or null where it is a whole thing
    /// in its own right. Only a zeppelin zone has one today. Kept beside <see cref="NameOf"/> so the
    /// ranker's <c>rating_biases</c> match has one source-type switch, not two.</summary>
    internal static string? OwnerOf(object? source) => source switch
    {
        DestructibleRegistry.Instance inst => inst.Owner,
        _ => null,
    };

    /// <summary>Whether a turret candidate stands in the world rather than being carried by an
    /// aircraft. Only emplacements are selectable: a carried gunner's host is already a target in
    /// its own right, and offering both would put two entries on one silhouette. An emplacement is
    /// the one with a placement <see cref="TurretController.Site"/>. Internal rather than private:
    /// <see cref="FlightController.AddRankedNonAircraft"/> shares this same guard for the AI's
    /// ranked pool, rather than growing a second carried/emplacement check.</summary>
    internal static bool IsEmplacement(object? source) =>
        source is TurretController { Site: not null };

    /// <summary>Wraps one classed candidate as a <see cref="TargetRef"/>. The KIND picks the shape
    /// (which is why an unrecognised source still lands in the right cycle with an empty name rather
    /// than vanishing); the source supplies only the strings and the health figures. This is the one
    /// place in the targeting path that reads a concrete source type at all.</summary>
    private static TargetRef Describe(AimCandidate c, AimTargetKind kind, TargetClass cls,
        bool objective)
    {
        string name = NameOf(c.Source);
        switch (kind)
        {
            case AimTargetKind.Vehicle:
                var plane = c.Source as FlightController;
                var dmg = plane?.Damage;
                // The MARKER prints the airframe's common name (plane type alone, decision 10),
                // not the node name the selection is held and pinned by. A rig with no flight model
                // bound has no airframe to name, and falls back to that node name.
                return TargetRef.ForAircraft(c, cls, name,
                    plane?.Stats is { } stats ? PlaneRoster.PlaneDisplayName(stats) : null,
                    dmg == null ? null : TargetRef.Fraction(dmg.WholeHealth, dmg.WholeHealthMax),
                    dmg == null ? null : TargetRef.Fraction(dmg.WholeArmor, dmg.WholeArmorMax),
                    objective, plane?.ObjectiveTypeLabel, plane?.ObjectiveCategory);
            case AimTargetKind.Turret:
                // No health figure exists for an emplacement: the retail loaders read no HEALTH key
                // and its aliveness is its healthy node's visibility.
                return TargetRef.ForTurret(c, cls, name);
            case AimTargetKind.Ordnance:
                var round = c.Source as ProjectilePool.Flyout;
                // The marker prints the weapon's own DESC. The original hard-codes message 0x2f6a
                // there (MSG_WEAP_AERIAL_TORPEDO) for every wrapper it builds, which is the same
                // string on the one entry that can reach this, and honest on any other.
                return TargetRef.ForOrdnance(c, cls, name, round?.Weapon.DisplayName,
                    round == null ? null : TargetRef.Fraction(round.Health, round.HealthMax));
            default:
                // An objective site is scenery the MISSION named, so its two label lines and its
                // own name come off the target table rather than off a health model it has none of.
                if (c.Source is ObjectiveSite site)
                {
                    return TargetRef.ForStructure(c, cls, name, site.TypeLabel, site.Category,
                        objective: true, displayName: site.DisplayName);
                }

                var inst = c.Source as DestructibleRegistry.Instance;
                return TargetRef.ForStructure(c, cls, name,
                    health: inst == null ? null : TargetRef.Fraction(inst.Health, inst.MaxHealth));
        }
    }

    private void Offer(AimCandidate c, AimTargetKind kind, int ownTeam, object? self,
        bool objectiveTarget = false)
    {
        if (c.Source == null || ReferenceEquals(c.Source, self))
        {
            return;
        }

        // The admission byte, read explicitly rather than inferred from a non-null source: a round
        // wrapped only because it is fused is on the same list and must not become selectable.
        if (kind == AimTargetKind.Ordnance
            && c.Source is not ProjectilePool.Flyout { Targetable: true, Live: true })
        {
            return;
        }

        // An aeroplane whose own roster block authors the flag is the mission's marker: one
        // candidate, ranked Objective ahead of every Enemy Target, rather than a synthetic site
        // beside the aeroplane it stands on (docs/org/targeting.md).
        bool objective = objectiveTarget
            || (kind == AimTargetKind.Vehicle && c.Source is FlightController { ObjectiveTarget: true });

        // ⚠ Plumb objectiveTarget, never fake it through otherTarget: that lands an objective site
        // on the Non-Aircraft cycle instead of the Enemy one. Only otherTarget is stood in for by
        // what the candidate is, a world emplacement and a sub-part being selectable.
        bool otherTarget = !objective && kind switch
        {
            AimTargetKind.Turret => IsEmplacement(c.Source),
            AimTargetKind.Structure => true,
            _ => false,
        };
        if (TargetRef.Classify(kind, c.Live, c.Team, ownTeam, otherTarget, objective)
            is not { } cls)
        {
            return;
        }

        Add(Describe(c, kind, cls, objective));
    }
}
