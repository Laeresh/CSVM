using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using Godot;

namespace CSVM.Session;

/// <summary>One campaign roster block, planned: everything the placement step needs, resolved
/// from the block's fields, the vehicle def table and the chapter's nets, and nothing read from
/// the engine. <see cref="Escorts"/> and <see cref="Net"/> are the decoded fork and are never
/// both set (docs/org/aiPilot.md "A net demotes a wingman").</summary>
public sealed class RosterSpawnPlan
{
    public required string Name { get; init; }

    /// <summary>The vehicle def the block resolves to (<c>blakepeace_2</c> for
    /// <c>blakepeace_2_1</c>).</summary>
    public required string Def { get; init; }

    /// <summary>The player airframe node the model is built from.</summary>
    public required string PlaneNode { get; init; }

    /// <summary>The AI def the stats resolve down, or null when <see cref="Def"/> does not derive
    /// from the airframe's base def and the plain base def flies instead.</summary>
    public string? AiDef { get; init; }

    /// <summary>The resolved def's <c>mode</c>.</summary>
    public required string Mode { get; init; }

    public Vector3 Position { get; init; }

    public float YawDeg { get; init; }

    public int? Team { get; init; }

    public int Group { get; init; }

    /// <summary>The block's <c>deactivated</c> flag: built inert, waiting for
    /// <c>WAKEUP_ENEMIES</c> or a generator launch.</summary>
    public bool Inert { get; init; }

    /// <summary>The patrol net the block authored and the chapter carries, or null.</summary>
    public AiNet? Net { get; init; }

    /// <summary>The net id the block authored that the chapter does not carry, or null.</summary>
    public int? MissingNetId { get; init; }

    /// <summary>The decoded fork: <c>mode wingman</c> with no authored net. The pilot flies
    /// <see cref="AiEscort"/> on <see cref="LeaderName"/> and no patrol net.</summary>
    public bool Escorts { get; init; }

    /// <summary>The <c>primary_target</c> name: the formation leader of an escorting block, the
    /// gunner's assignment on any other; null when unset.</summary>
    public string? LeaderName { get; init; }

    /// <summary>The volumes in the decoded order: the net's non-zero values, the block's own
    /// over them. The <c>min_ai_active_dist</c> floor is applied at placement.</summary>
    public AiVolumeSet Volumes { get; init; }

    public AiSkillVector Skills { get; init; }

    public long SignatureMask { get; init; }

    public IReadOnlyList<AiRatingBias> Biases { get; init; } = Array.Empty<AiRatingBias>();

    public int? AccentId { get; init; }

    public bool Nitro { get; init; }

    public string? TaxiPath { get; init; }

    public float? PrefEngageAlt { get; init; }

    public string? Title { get; init; }

    /// <summary>The menu-chosen loadout laid over the stock table, the campaign wingman's only.</summary>
    public LoadoutChoice? Fit { get; init; }

    /// <summary>The spawn nose, from the block's yaw in the mission-data convention.</summary>
    public Vector3 Forward => new Basis(Vector3.Up, Mathf.DegToRad(YawDeg)) * Vector3.Forward;
}

/// <summary>
/// The engine-free planning step of the campaign roster spawner: every block of a mission's
/// <c>aiv</c> roster resolved to a <see cref="RosterSpawnPlan"/>, with the decoded net-versus-
/// escort fork applied and the volumes merged in the original's order. The placement half is
/// <c>CampaignDirector.BuildRoster</c>. The player's own block and every block with no airframe
/// (a surface vehicle, an unknown def) are reported in <see cref="Skipped"/>, never spawned.
/// </summary>
public sealed class CampaignRosterPlan
{
    /// <summary>The block name the roster gives the human player's aircraft.</summary>
    public const string PlayerBlock = "player";

    private CampaignRosterPlan()
    {
    }

    public IReadOnlyList<RosterSpawnPlan> Spawns { get; private set; } = Array.Empty<RosterSpawnPlan>();

    /// <summary>Blocks that plan to nothing, with the reason.</summary>
    public IReadOnlyList<(string Name, string Why)> Skipped { get; private set; } = Array.Empty<(string, string)>();

    /// <summary>Plans the roster. <paramref name="wingmanNode"/> is the profile's wingman
    /// airframe for the block named <paramref name="wingmanName"/>, or null to fly the block's
    /// own def; <paramref name="netDraw"/> is the <c>rand() % count</c> draw a multi-entry
    /// <c>netids</c> takes, given the count.</summary>
    public static CampaignRosterPlan Build(
        IReadOnlyList<(string Name, List<object?> Fields)> blocks,
        VehicleDefs defs,
        IReadOnlyList<AiNet> nets,
        string? wingmanNode = null,
        LoadoutChoice? wingmanFit = null,
        string wingmanName = CampaignDirector.WingmanName,
        Func<int, int>? netDraw = null)
    {
        var spawns = new List<RosterSpawnPlan>();
        var skipped = new List<(string, string)>();
        foreach (var (name, fields) in blocks)
        {
            if (name.Equals(PlayerBlock, StringComparison.OrdinalIgnoreCase))
            {
                skipped.Add((name, "the human player's own block"));
                continue;
            }
            if (defs.DefForBlock(name) is not { } def)
            {
                skipped.Add((name, "no vehicle def matches the block name"));
                continue;
            }
            if (AiSkills.RosterSpawnPose(fields) is not { } pose)
            {
                skipped.Add((name, "no spawn position authored"));
                continue;
            }

            // The airframe: the profile's plane for the named wingman when the cabin bound one,
            // the block's own def otherwise. Either way the AI def must derive from the airframe's
            // base def or PlaneStats cannot resolve it, and the plain base def flies instead.
            string? planeNode = null;
            string? baseDef = null;
            if (wingmanNode != null && name.Equals(wingmanName, StringComparison.OrdinalIgnoreCase))
            {
                planeNode = wingmanNode;
                baseDef = defs.BaseDefForPlayerNode(wingmanNode);
            }
            if (planeNode == null || baseDef == null)
            {
                if (defs.AirframeFor(def) is not { } airframe)
                {
                    skipped.Add((name, $"'{def}' ({defs.ModeOf(def)}) has no player airframe"));
                    continue;
                }
                (planeNode, baseDef) = airframe;
            }
            // The block's own def decides the mode. The AI def only carries stats and livery: the
            // profile's airframe takes its w<plane> twin, a def that is no variant of its airframe
            // takes the plain base def.
            string? aiDef = defs.DerivesFrom(def, baseDef) ? def
                : planeNode == wingmanNode && defs.Has("w" + baseDef) && defs.DerivesFrom("w" + baseDef, baseDef)
                    ? "w" + baseDef
                    : null;
            string mode = defs.ModeOf(def) ?? VehicleDefs.JetMode;

            var netIds = AiSkills.RosterNetIds(fields);
            AiNet? net = null;
            int? missingNet = null;
            if (netIds.Count > 0)
            {
                int pick = netIds.Count == 1 ? 0 : (netDraw?.Invoke(netIds.Count) ?? 0) % netIds.Count;
                net = AiNets.ById(nets, netIds[pick]);
                if (net is null or { Nodes.Count: 0 })
                {
                    missingNet = netIds[pick];
                    net = null;
                }
            }
            // The decoded fork: a netless wingman escorts; ANY authored net demotes it to a jet
            // that walks the graph, so a block never carries both.
            bool escorts = netIds.Count == 0
                && mode.Equals(VehicleDefs.WingmanMode, StringComparison.OrdinalIgnoreCase);

            spawns.Add(new RosterSpawnPlan
            {
                Name = name,
                Def = def,
                PlaneNode = planeNode,
                AiDef = aiDef,
                Mode = mode,
                Position = pose.Position,
                YawDeg = pose.YawDeg,
                Team = AiSkills.RosterTeam(fields),
                Group = AiSkills.RosterGroup(fields),
                Inert = AiSkills.RosterDeactivated(fields),
                Net = net,
                MissingNetId = missingNet,
                Escorts = escorts,
                LeaderName = AiSkills.RosterPrimaryTarget(fields),
                Volumes = (net?.Volumes ?? AiVolumeSet.None).Overlaid(AiVolumeSet.FromRosterSlots(fields)),
                Skills = AiSkills.RosterSkills(fields),
                SignatureMask = AiSkills.RosterSignatureMask(fields),
                Biases = AiSkills.RosterRatingBiases(fields),
                AccentId = AiSkills.RosterAccentId(fields),
                Nitro = AiSkills.RosterNitro(fields),
                TaxiPath = AiSkills.RosterTaxiPath(fields),
                PrefEngageAlt = AiSkills.RosterPrefEngageAlt(fields),
                Title = AiSkills.RosterTitle(fields),
                Fit = planeNode == wingmanNode ? wingmanFit : null,
            });
        }
        return new CampaignRosterPlan { Spawns = spawns, Skipped = skipped };
    }

    /// <summary>Writes a plan's volumes onto a spawned pilot's range gates: each authored
    /// radius over the def's, then the activation radius floored to
    /// <paramref name="minAiActiveDist"/> (docs/org/aiPilot.md "No activation volume is smaller
    /// than min_ai_active_dist"). The altitude bands have no consumer yet and are not applied.
    /// A null machine is a pilot the spawner armed nothing on.</summary>
    public static void ApplyVolumes(AiModeMachine? machine, AiVolumeSet volumes, float minAiActiveDist)
    {
        if (machine == null)
            return;
        if (volumes.Activation.Radius != 0f)
            machine.ActivationRange = volumes.Activation.Radius;
        if (volumes.Attack.Radius != 0f)
            machine.AttackRange = volumes.Attack.Radius;
        if (volumes.Return.Radius != 0f)
            machine.ReturnRange = volumes.Return.Radius;
        machine.ActivationRange = Mathf.Max(machine.ActivationRange, minAiActiveDist);
    }

    /// <summary>The spawn record a planned block launches as. The block's own representative
    /// rating arms the gunner and machine; its authored slots then outrank the def's inside the
    /// spawner. A campaign enemy keeps its militia's skins; the player's side takes the default
    /// pattern like an Instant Action wingman. ⚠ <c>NodeName</c> is the block's own name and not
    /// the assembler's counter form, because <c>primary_target</c> and <c>rating_biases</c> are
    /// authored against it (BL-401).</summary>
    public static AiSpawn SpawnFor(RosterSpawnPlan plan, Vector3 pos, Vector3 lookAt, AiPilot pilot) =>
        new(plan.PlaneNode, pos, lookAt, pilot, Scheme: null, Team: plan.Team,
            Inert: plan.Inert, ShippedSkins: plan.Team != AimAssist.PlayerTeam,
            AiDef: plan.AiDef, Fit: plan.Fit,
            AttackRating: InstantActionRuntime.RepresentativeRating(plan.Skills),
            Nitro: plan.Nitro, RosterSkills: plan.Skills, NodeName: plan.Name);

    /// <summary>The formation leader a plan's <see cref="RosterSpawnPlan.LeaderName"/> names,
    /// out of the spawned rigs: the literal <c>player</c> is the first human, anything else a
    /// spawned block by name. Null when unset or not spawned (a dead leader is the pilot's own
    /// fallback, not this lookup's).</summary>
    public static TRig? ResolveLeader<TRig>(string? leaderName,
        IReadOnlyDictionary<string, TRig> rigsByName, TRig? player)
        where TRig : class
    {
        if (leaderName == null)
            return null;
        if (leaderName.Equals(PlayerBlock, StringComparison.OrdinalIgnoreCase))
            return player;
        return rigsByName.TryGetValue(leaderName, out var rig) ? rig : null;
    }
}
