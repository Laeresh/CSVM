using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Flight.Ai;
using CSVM.Flight.Airframe;
using CSVM.Flight.Audio;
using CSVM.Flight.Camera;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Campaign;
using CSVM.Session.Objectives;
using CSVM.Session.Roster;
using CSVM.Session.World;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Launch;

/// <summary>The flight's opposition outside the mission directors' own rosters, built in steps of
/// the session build. It covers the <c>--ai</c> squadrons and the mission's zeppelins. It also
/// covers the enemy generators with their wave aeroplanes, and the world AA emplacements. Each step reads the
/// <see cref="BuildState"/> and returns the runtime it built for the session to step in its own
/// order. The session calls the mode directors' phases between the steps.
/// Module entry: docs/architecture/Launch.md on src/Launch/OppositionStage.cs.</summary>
internal sealed class OppositionStage
{
    // How many aeroplanes one generator's wave is built ahead for, and how many one airframe and
    // livery holds however many generators order it. A wave arrives a second or more apart, and a
    // quiet frame refills one. A few deep covers a burst without lengthening the load screen.
    private const int WaveAirframeDepth = 4;
    private const int WaveAirframeCap = 8;

    // The empty stage's squadron ring, metres. Two opposed sides therefore start 2000 m apart,
    // AiModeMachine's decoded attack range, so an --ai= sortie engages without a dead approach.
    private const float SquadronRingRadiusM = 1000f;

    private readonly Inputs _in;
    private AiGeneratorRuntime? _generators;

    /// <summary>The steps over one flight's roster and world.</summary>
    public OppositionStage(Inputs inputs) => _in = inputs;

    /// <summary>Orders the aeroplanes this mission's generators will launch, off the roster blocks
    /// they launch from. The load screen builds them instead of the launch frame. Depth is the
    /// generator's own authored wave size: that is how many arrive before the cycle rests, and a
    /// quiet frame refills one. Public so the wave-launch hitch suite orders exactly as a launch
    /// does rather than modelling it.</summary>
    public static void OrderWaveAirframes(FlightRoster roster,
        IReadOnlyList<EnemyGeneratorDef> defs, IReadOnlyDictionary<string, RosterSpawnPlan> templates)
    {
        int ordered = 0;
        var pilot = AiPilot.HoldingCourse(Vector3.Zero, Vector3.Forward);
        foreach (var def in defs)
        {
            if (CampaignRosterPlan.ResolveGeneratorLaunch(templates, def.VehicleParams, out var plan)
                != GeneratorLaunch.Template || plan == null)
            {
                continue;
            }

            int depth = Math.Clamp(def.WaveSize, 1, WaveAirframeDepth);
            if (roster.OrderWaveAirframes(
                CampaignRosterPlan.SpawnFor(plan, Vector3.Zero, Vector3.Forward, pilot),
                depth, WaveAirframeCap))
            {
                ordered += depth;
            }
        }

        if (ordered > 0)
        {
            Log.Info("world", $"egen: {roster.OwedAirframes} wave aeroplane(s) ordered from {ordered} block slot(s); the load screen builds them so a launch binds one instead of building it");
        }
    }

    /// <summary>The <c>--ai</c> squadrons. Without a net: ahead of P1 on its own spawn heading,
    /// fanned right and left, holding that course. With one: on the net's first node, patrolling
    /// the graph. ⚠ The empty stage anchors each side to the grid origin instead, so a count sweep
    /// repeats one geometry.</summary>
    public void BuildSquadrons(BuildState state, IReadOnlyList<PlayerRig> rigs)
    {
        var spec = _in.Spec;
        var roster = _in.Roster;
        if (spec.AiPlanes is not { Count: > 0 } aiPlanes || rigs.Count == 0
            || rigs[0].Controller is not { } lead)
        {
            return;
        }

        var basis = lead.GlobalTransform.Basis;
        var fwd = -basis.Z;
        var right = basis.X;
        var anchors = spec.EmptyStage ? SquadronAnchors(aiPlanes) : null;
        List<AiNet>? nets = null;
        bool netsTried = false;
        int fanIndex = 0;
        int spawnedTotal = 0;
        for (int i = 0; i < aiPlanes.Count; i++)
        {
            var entry = aiPlanes[i];
            string planeName = entry.Plane;
            string? aiDef = entry.Def;
            int? accentId = entry.Accent;
            AiNet? net = null;
            if (entry.Net != null)
            {
                // The stage's built-in ring answers first, since --stage=empty has no chapter
                // index to name and its name is reserved against every shipped one. A chapter
                // net is still named the way it always was, on any stage.
                net = EmptyStage.ResolveNet(entry.Net);
                if (net == null && !netsTried)
                {
                    netsTried = true;
                    try
                    {
                        nets = AiNets.Load(_in.ChapterZrdrPath);
                    }
                    catch (Exception e)
                    {
                        GD.PushWarning($"--ai: cannot read {spec.Chapter}'s patrol nets: {e.Message}");
                    }
                }
                net ??= nets != null ? AiNets.Resolve(nets, entry.Net) : null;
                if (net == null)
                    GD.PushWarning($"--ai: net '{entry.Net}' is neither the built-in " +
                                   $"'{EmptyStage.PatrolNetName}' ring nor a net in " +
                                   $"{spec.Chapter}'s neindex; '{planeName}' spawns without a patrol");
            }
            for (int k = 0; k < entry.Count; k++)
            {
                // Per squadron on the ring, per session otherwise, so a command line of
                // one-plane entries keeps exactly the spread it had before n= existed.
                int fi = anchors != null ? k : fanIndex;
                fanIndex++;
                float lateral = 60f * ((fi + 1) / 2) * (fi % 2 == 0 ? 1f : -1f);
                if (net != null)
                {
                    // Spawned on the net itself, so a scripted run sees it patrolling within
                    // seconds. ⚠ Take node positions off the follower, not the record, or an
                    // anchored net puts the plane where the ring is not.
                    var follower = new AiNetFollower(net, Rng.NewSystemRandom(Rng.Ai),
                        trailerTarget: _in.NetTrailers.For(net));
                    var pos = follower.NodePosition(0) + right * lateral;
                    var look = net.Nodes.Count > 1 ? follower.NodePosition(1) : pos + fwd;
                    var pilot = AiPilot.HoldingCourse(pos, look);
                    pilot.Patrol = follower;
                    var spawnedOnNet = roster.SpawnAi(new AiSpawn(
                        planeName, pos, look, pilot, Team: entry.Team, AiDef: aiDef));
                    // The net's own volumes over the gates the assembler took from the airframe
                    // def, the same write a campaign net assignment makes. Without it a CLI
                    // plane flies the machine's decoded defaults whatever net it is given.
                    CampaignRosterPlan.ApplyVolumes(pilot.Machine, net.Volumes, _in.MinAiActiveDist());
                    _in.RegisterVoice(spawnedOnNet, accentId ?? StatsForSpawn(planeName, aiDef)?.AiAccentId);
                    ApplyAiHullPreset(spawnedOnNet);
                }
                else if (anchors != null)
                {
                    var (anchor, facing) = anchors[i];
                    var pos = anchor + facing.Cross(Vector3.Up).Normalized() * lateral;
                    var look = pos + facing;
                    var spawnedOnRing = roster.SpawnAi(new AiSpawn(planeName, pos, look,
                        AiPilot.HoldingCourse(pos, look), Team: entry.Team, AiDef: aiDef));
                    _in.RegisterVoice(spawnedOnRing, accentId ?? StatsForSpawn(planeName, aiDef)?.AiAccentId);
                    ApplyAiHullPreset(spawnedOnRing);
                }
                else
                {
                    var pos = lead.WorldPosition + fwd * 250f + right * lateral;
                    var spawnedAhead = roster.SpawnAi(new AiSpawn(planeName, pos, pos + fwd,
                        AiPilot.HoldingCourse(pos, pos + fwd), Team: entry.Team, AiDef: aiDef));
                    _in.RegisterVoice(spawnedAhead, accentId ?? StatsForSpawn(planeName, aiDef)?.AiAccentId);
                    ApplyAiHullPreset(spawnedAhead);
                }
                spawnedTotal++;
            }
        }
        state.What += $" + {spawnedTotal} AI";
    }

    /// <summary>The mission's zeppelin instances, placed at their authored pose and flown along
    /// their nets as kinematic world nodes. Their damage, broadside cannons and target sub-parts
    /// are wired. Null when the launch asks for none. ⚠ Build them before the generators, so
    /// a zeppelin generator's min_altitude gate reads the flown host's live Y from the first step.
    /// </summary>
    public ZeppelinRuntime? BuildZeppelins(BuildState state, bool iaZeppelinRun,
        ProjectilePool? projectiles, WeaponDefs weaponDefs)
    {
        var spec = _in.Spec;
        if (!(spec.Zeppelins || iaZeppelinRun || spec.Zep != null || spec.MissionType == DogfightMissionType.ZeppelinVsZeppelin))
        {
            return null;
        }

        List<ZeppelinDef> zepDefs;
        try
        {
            zepDefs = Zeppelins.Load(state.MissionZrdrPath);
        }
        catch (IOException e)
        {
            Log.Info("world", $"zep: no zeppelins file for {spec.Chapter}/{spec.Mission}: {e.Message}");
            zepDefs = new List<ZeppelinDef>();
        }
        IReadOnlyList<AiNet> zepNets;
        int? zepTeamOverride = null;
        Vector3? zepSeat = null;
        if (spec.Zep is { } graft)
        {
            (zepDefs, zepNets, zepSeat) = GraftedZeppelin(zepDefs, graft);
            zepTeamOverride = graft.Team;
        }
        else
        {
            zepNets = AiNets.Load(_in.ChapterZrdrPath);
        }
        var world = _in.World;
        var zeppelins = new ZeppelinRuntime(zepDefs,
            name => world?.FindNodes(name) is { Count: > 0 } hits ? hits[0] : null,
            zepNets, _in.NetTrailers.For,
            // The bootstrap has already run the start anims. A hull an SI script owns from its
            // first frame must not be placed at the record seat on top of it.
            host => world?.Motions.DrivesTransform(host) ?? false,
            zepTeamOverride, zepSeat);
        _in.WorldRoot.AddChild(zeppelins);
        // F18: the multi-zone damage half, per-part pools over the world registry, the
        // survivor-count kill, and the DAMAGES_ZEPPELIN gate on the shared pool.
        if (world is { } zepRuntime)
        {
            zeppelins.WireDamage(zepRuntime);
            if (projectiles != null)
            {
                projectiles.WorldDamageGate = zeppelins.GateWeaponDamage;
            }
        }
        // F19: the broadside cannons, real wep_28 rounds through the shared pool, the
        // authored deploy/retract anims, targets from the record. After WireDamage so
        // F18's cannon pools exist (a destroyed cannon thins the volley).
        if (projectiles != null)
        {
            zeppelins.WireCannons(projectiles, weaponDefs);
        }
        // B14: the zeppelin sub-parts are the one thing that makes a structure selectable. The
        // zeppelins are built AFTER the rigs, so the feed is bound here, not in the assembler. Every pane shares the one runtime; each fills its own list from it.
        _in.Roster.SetTargetSubParts(into => zeppelins.CollectTargetParts(into));
        Log.Info("world", $"zep: {zeppelins.LiveCount} of {zepDefs.Count} zeppelin(s) placed for {spec.Chapter}/{spec.Mission}");
        state.What += $" + {zeppelins.LiveCount} zeppelin(s)";
        return zeppelins;
    }

    /// <summary>The mission's egen enemy generators, spawning through the roster and loaded here
    /// because the drop rules need the built world. An Instant Action zeppelin run asks for them
    /// itself: its generator is the only way an enemy reaches the air. Null when none run.
    /// </summary>
    public AiGeneratorRuntime? BuildGenerators(BuildState state, bool iaZeppelinRun,
        ZeppelinRuntime? zeppelins)
    {
        var spec = _in.Spec;
        if (!(spec.Generators || iaZeppelinRun))
        {
            return null;
        }

        var roster = _in.Roster;
        var campaign = _in.Campaign;
        List<EnemyGeneratorDef> egenDefs;
        try
        {
            egenDefs = EnemyGenerators.Load(state.MissionZrdrPath);
        }
        catch (IOException e)
        {
            Log.Info("world", $"egen: no generator file for {spec.Chapter}/{spec.Mission}: {e.Message}");
            egenDefs = new List<EnemyGeneratorDef>();
        }
        var chapterNets = AiNets.Load(_in.ChapterZrdrPath);
        // The parameter blocks a generator's vehicle.params names are mission data, not campaign
        // state. A launch resolves its block on any run that turns the generators on. Otherwise
        // the aircraft flies a CLI airframe with none of the block's authored fields.
        var generatorTemplates = CampaignRosterPlan.GeneratorTemplates(
            state.MissionZrdrPath, VehicleDefs.Load(state.ZrdrPath), chapterNets);
        float generatorActiveDist = _in.MinAiActiveDist();
        var generatorSurface = _in.EnsureSurfaceVehicles();

        // The template's own fields, applied as the campaign roster applies them. Then its
        // accent, so a generated pilot is heard as the block the mission authored.
        LaunchedVehicle SpawnFromGenerator(EnemyGeneratorDef def, Vector3 pos, Vector3 look,
            AiPilot pilot)
        {
            // The decoded launch name: one counter across the mission's generators. A second
            // launch off the same template is then a distinct node rather than a rename.
            int ordinal = _generators?.LaunchOrdinal ?? 0;
            switch (CampaignRosterPlan.ResolveGeneratorLaunch(
                generatorTemplates, def.VehicleParams, out var plan))
            {
                case GeneratorLaunch.Empty:
                    // The decoded empty launch: a label naming no block builds nothing, and
                    // the runtime counts the launch anyway. Never an airframe in its place.
                    Log.Info("world", $"egen: '{def.Node}' params '{def.VehicleParams}' names no roster block: the launch builds nothing");
                    return default;
                case GeneratorLaunch.Surface:
                    // A hull off a ship generator: never an airframe in its place. With no
                    // surface runtime on this stage the launch is the counted empty one.
                    var hull = plan!;
                    if (generatorSurface == null)
                    {
                        Log.Info("world", $"egen: '{def.Node}' params '{def.VehicleParams}' names the hull '{hull.Def}', which this stage cannot build: the launch builds nothing");
                        return default;
                    }
                    string hullName = EnemyGenerators.LaunchName(EnemyGenerators.LaunchBase(hull.Name), ordinal);
                    var hullLaunch = new LaunchedVehicle(null, generatorSurface.Spawn(hull, pos, look - pos, hullName));
                    campaign?.RegisterGeneratorLaunch(hullName, hullLaunch, hull);
                    return hullLaunch;
                case GeneratorLaunch.Airframe when _generators is { RefusesOwnAircraft: true }:
                    return LaunchedVehicle.Refusal;
                case GeneratorLaunch.Airframe:
                    // ⚠ shippedSkins: a generated aircraft is the mission's enemy, so it
                    // keeps its own textures rather than the player militia's default.
                    return roster.SpawnAi(new AiSpawn(
                        spec.GeneratorsPlane, pos, look, pilot, ShippedSkins: true,
                        NodeName: EnemyGenerators.LaunchName(spec.GeneratorsPlane, ordinal)));
            }
            // A guest builds a generator aircraft only when the host's launch arrives, at the
            // admission ordinal the host gave it.
            if (_generators is { RefusesOwnAircraft: true })
                return LaunchedVehicle.Refusal;
            var template = plan!;
            string launchName = EnemyGenerators.LaunchName(EnemyGenerators.LaunchBase(template.Name), ordinal);
            var launched = roster.SpawnAi(CampaignRosterPlan.SpawnFor(template, pos, look, pilot, launchName));
            CampaignRosterPlan.ApplyPlan(pilot, template, generatorActiveDist);
            _in.RegisterVoice(launched, template.AccentId);
            // The mission script counts and commands the launch by this name. The campaign
            // roster must hold it, or a DEDG over its group reads the group as empty.
            campaign?.RegisterGeneratorLaunch(launchName, launched, template);
            return launched;
        }

        var wr = _in.World;
        var generators = _generators = new AiGeneratorRuntime(egenDefs,
            wr == null ? null
                : (name, scope) => wr.FindNodes(name, scope) is { Count: > 0 } hits ? hits[0] : null,
            chapterNets, spec.GeneratorsPlane, SpawnFromGenerator,
            wr == null ? null : (name, host) => wr.PlayWithin(host, name, applyReset: false).Count,
            wr == null ? null : (name, host) => wr.StopWithin(host, name),
            _in.NetTrailers.For);
        _in.WorldRoot.AddChild(generators);
        // Cutscene callback 800 credits through the runtime's host chain. Bound after the
        // ladder switch's last bind, which its chaining requires.
        if (wr != null)
        {
            generators.BindCallbackHost(wr);
        }
        // --wake-generators: the script's whole WAKEUP_GENERATOR credit granted at build.
        if (spec.WakeGenerators)
        {
            campaign?.WakeGenerators(generators);
        }

        // A dead zeppelin permanently disables its generator (the decoded rule; F18
        // supplies the death the B6 stub waited on).
        if (zeppelins != null)
        {
            zeppelins.ZeppelinKilled += node => generators.NotifyHostDied(node);
        }
        // A fixed installation (the submarine) has no destroyed flag of its own. Its death is its
        // healthy node going inactive, which DamageAt raises the same way.
        if (wr != null)
        {
            wr.DestructibleKilled += node => generators.NotifyHostDied(node);
        }
        Log.Info("world", $"egen: {generators.LiveCount} of {egenDefs.Count} generator(s) live for {spec.Chapter}/{spec.Mission}, spawning '{spec.GeneratorsPlane}'");
        state.What += $" + {generators.LiveCount} generator(s)";
        OrderWaveAirframes(roster, egenDefs, generatorTemplates);
        return generators;
    }

    /// <summary>World AA emplacements: the standalone ai.zrd family, placed against this chapter's
    /// built world unconditionally, like the original's own placement pass. Shipped ACTIVATED
    /// decides which are awake; <c>--wake-turrets</c> stands in for the mission script's
    /// WAKEUP_TURRETS. Null without a world or a turret table.</summary>
    public TurretEmplacementRuntime? PlaceEmplacements(BuildState state, TurretDefs? turretDefs,
        WeaponDefs weaponDefs, ProjectilePool projectiles, ZeppelinRuntime? zeppelins,
        IReadOnlyList<(Node3D Node, bool Objective, string Name)> zeppelinTurretSwitch)
    {
        var spec = _in.Spec;
        if (state.WorldRuntime is not { } placedRt || turretDefs == null)
        {
            return null;
        }

        // A world gun's voice hangs under the world's own sound node, never inside the subtree
        // it fires from. The animation runtime's node memoization holds only while nothing adds
        // to a world subtree at runtime. Null home leaves every gun silent, as --mute does.
        var gunVoices = placedRt.Sounds is { } soundHome
            ? new GunVoiceHome(soundHome, state.Sounds, state.SoundDefs, _in.PlayerPositions)
            : null;
        var turrets = new TurretEmplacementRuntime(turretDefs, weaponDefs,
            (pattern, scope) => placedRt.FindNodes(pattern, scope), projectiles,
            placedRt.WorldRoot, gunVoices);
        // ⚠ Into the tree AFTER the zeppelin runtime. The physics tick follows tree order, which
        // lets a slung mount read its ride's moved pose on a realtime clock.
        _in.WorldRoot.AddChild(turrets);
        // The rest of the zeppelin record's team fan: its guns, which do not exist until here.
        zeppelins?.FanTeamsOntoTurrets(turrets);
        int awakeByData = turrets.AwakeCount;
        // The Instant Action zeppelin turret arm. ⚠ Run it BEFORE --wake-turrets, which stands
        // in for a mission script and therefore wins, the same order the original has.
        foreach (var (zepNode, objective, zepName) in zeppelinTurretSwitch)
        {
            int touched = turrets.SetActivatedUnder(zepNode, objective);
            if (touched > 0)
            {
                string arm = objective ? "ACTIVATED with the objective" : "stowed with the hull";
                Log.Info("flight", $"ia: zeppelin '{zepName}' turrets: {touched} emplacement(s) {arm}");
            }
        }
        int woken = spec.WakeTurrets ? turrets.WakeAll() : 0;
        string wokenTail = woken > 0 ? $", {woken} woken by --wake-turrets" : string.Empty;
        // ⚠ Alive as well as awake: a census of the wake state alone reads identically
        // whether the guns can fire or not.
        Log.Info("flight", $"turrets: {turrets.Count} world emplacement(s) placed for {spec.Chapter} ({awakeByData} awake by data, {turrets.Count - awakeByData} dormant{wokenTail}); {turrets.AwakeCount} awake and {turrets.AliveCount} alive now");
        if (turrets.Count > 0)
        {
            state.What += $" + {turrets.Count} emplacement(s)";
        }
        return turrets;
    }

    // One placement slot per side on a ring about the grid origin, each squadron facing the centre.
    // Two sides therefore start 2 * SquadronRingRadiusM apart, which is AiModeMachine's decoded
    // engagement gate, so they are in contact from the first frames. A teamless entry is its own
    // side: a spawn naming no team= takes its own banded id (AimAssist.TeamOfPilot) regardless.
    // ⚠ Slot order is first appearance on the command line, not team id, so adding an entry does
    // not renumber the sides already there.
    private static List<(Vector3 Anchor, Vector3 Facing)> SquadronAnchors(IReadOnlyList<AiPlaneEntry> entries)
    {
        var slotOf = new Dictionary<int, int>();
        var keys = new int[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            keys[i] = entries[i].Team ?? int.MinValue + i;
            if (!slotOf.ContainsKey(keys[i]))
                slotOf[keys[i]] = slotOf.Count;
        }
        var anchors = new List<(Vector3, Vector3)>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            float angle = Mathf.Tau * slotOf[keys[i]] / slotOf.Count;
            var anchor = new Vector3(Mathf.Sin(angle) * SquadronRingRadiusM,
                EmptyStage.SpawnAltitude, -Mathf.Cos(angle) * SquadronRingRadiusM);
            if (entries[i].Pos is { } over)
                anchor = over;
            var toCentre = new Vector3(-anchor.X, 0f, -anchor.Z);
            anchors.Add((anchor, toCentre.LengthSquared() > 0.001f
                ? toCentre.Normalized() : Vector3.Forward));
        }
        return anchors;
    }

    // --zep=: the named record alone, on a one-node net at the stage seat unless net= names the
    // stage's built-in ring (EmptyStage.PatrolNet). The net is synthetic: a chapter net would fly
    // the hull to its own authored coordinates, thousands of metres from the squadron ring.
    // ⚠ It must have a net at all. A def whose net does not resolve is placed but held OUT of the
    // live list. Its zones then never wire, and nothing on it can be shot. The seat defaults to the grid origin at the record's own altitude, inside that ring.
    private static (List<ZeppelinDef> Defs, IReadOnlyList<AiNet> Nets, Vector3? Seat) GraftedZeppelin(
        IReadOnlyList<ZeppelinDef> all, ZepStageSpec graft)
    {
        var only = new List<ZeppelinDef>();
        foreach (var def in all)
        {
            if (def.Node.Equals(graft.Record, StringComparison.OrdinalIgnoreCase))
                only.Add(def);
        }
        if (only.Count == 0)
        {
            Log.Info("world", $"zep: '{graft.Record}' has no record in {graft.Chapter}/{graft.Mission}'s zeppelins.zrd.json, the hull is built but nothing is wired to it");
            return (only, System.Array.Empty<AiNet>(), null);
        }
        var seat = graft.Pos ?? new Vector3(0f, only[0].Position.Y, 0f);
        // net= puts the hull on the stage's built-in ring rather than the one-node net above. A
        // grafted airship then patrols the same graph an --ai= plane can be named onto. It is rebadged
        // with the record's own net name, since the runtime resolves a def's route BY that name.
        var ring = graft.Net != null ? EmptyStage.ResolveNet(graft.Net) : null;
        if (graft.Net != null && ring == null)
        {
            Log.Info("world", $"zep: net '{graft.Net}' is not the built-in '{EmptyStage.PatrolNetName}' ring; '{graft.Record}' station-keeps at its seat instead");
        }
        var nets = new List<AiNet>
        {
            new AiNet
            {
                Id = ring?.Id ?? 0,
                Name = only[0].Net,
                Nodes = ring is { } onRing
                    ? onRing.Nodes
                    : new[] { new AiNetNode(seat, System.Array.Empty<float>()) },
                Edges = ring?.Edges ?? System.Array.Empty<(int A, int B)>(),
                Volumes = ring?.Volumes ?? AiVolumeSet.None,
            },
        };
        Log.Info("world", $"zep: graft '{graft.Record}' from {graft.Chapter}/{graft.Mission} seated at ({seat.X:0},{seat.Y:0},{seat.Z:0}), {(ring != null ? $"flying the '{EmptyStage.PatrolNetName}' ring, " : "")}{(graft.Team is { } t ? $"team {t}" : "team as the record authors it")}");
        return (only, nets, seat);
    }

    // --ai-damage=: spends this AI plane's hull down to the ordered fraction at build, so a scripted
    // shot catches its injure_anims stages already up. Armour first and health second, in two exact
    // spends, the take-hit flow's order. An AI airframe resolves no zones, so both land in the
    // whole pair the ladder reads. ⚠ Never drives the pool to zero, a
    // preset that kills would leave a wreck where the point is a flying, burning aircraft.
    private void ApplyAiHullPreset(FlightController? controller)
    {
        if (_in.Spec.AiHullDamage is not { } fraction || controller?.Damage is not { } damage)
            return;
        if (damage.WholeArmor > 0f)
            damage.Apply("hull", 0f, damage.WholeArmor);
        float spend = damage.WholeHealth - (Mathf.Max(fraction, 0.01f) * damage.WholeHealthMax);
        if (spend > 0f)
            damage.Apply("hull", spend, 0f);
        controller.Visuals?.OnHullDamage(damage.SummaryHealthFraction);
        Log.Info("flight", $"ai damage preset: {controller.Name} hull at {damage.SummaryHealthFraction * 100f:0}% ({damage.WholeHealth:0.0}/{damage.WholeHealthMax:0})");
    }

    // The AI flavour of one airframe as the roster would load it, read for the def's pilot facts
    // at spawn. The roster reads the same cached object. Null when the def cannot resolve.
    private PlaneStats? StatsForSpawn(string planeName, string? aiDef)
    {
        try
        {
            return _in.AiStatsFor(planeName, aiDef);
        }
        catch (Exception e)
        {
            GD.PushWarning($"ai: cannot resolve '{aiDef ?? planeName}': {e.Message}");
            return null;
        }
    }

    /// <summary>What the opposition is built over: the roster the aircraft spawn through, the
    /// world it is placed against, and the session's voice and skills readers.</summary>
    internal sealed class Inputs
    {
        public SessionSpec Spec = null!;
        public Node3D WorldRoot = null!;
        public FlightRoster Roster = null!;
        public NetTrailerTargets NetTrailers = null!;
        public string ChapterZrdrPath = "";
        public AnimRuntime? World;
        public CampaignDirector? Campaign;
        // player.json's activation floor, read at each use as the session reads it.
        public Func<float> MinAiActiveDist = null!;
        // The roster's own AI stats reader, the same cache the roster builds from.
        public Func<string, string?, PlaneStats> AiStatsFor = null!;
        public Action<FlightController?, int?> RegisterVoice = null!;
        // The session's surface-vehicle runtime, built on first need.
        public Func<SurfaceVehicleRuntime?> EnsureSurfaceVehicles = null!;
        public Func<IReadOnlyList<Vector3>> PlayerPositions = null!;
    }
}
