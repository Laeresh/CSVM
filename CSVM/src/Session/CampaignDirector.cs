using System;
using System.Collections.Generic;
using CSVM.Flight;
using CSVM.Mech3;
using Godot;

namespace CSVM.Session;

/// <summary>What one flown campaign mission ended as: the outcome the objectives graph derived,
/// the attempt recorded into the profile, and what recording it changed.</summary>
public readonly record struct CampaignMissionResult(
    MissionOutcome Outcome, MissionAttempt Attempt, MissionRecorded Recorded);

/// <summary>
/// The engine-side runtime of one campaign mission, behind <c>GameSession</c>'s one nullable
/// <c>_campaign</c> field and the sibling of <see cref="InstantActionDirector"/>. A plain sealed
/// class, not a Node: <c>GameSession</c> owns the tick order and calls the phases at its pinned
/// points, and nothing here builds a node of its own. The decoded rules stay engine-free in
/// <see cref="ObjectiveScript"/> and <see cref="ObjectiveGraph"/>; this class is where they meet
/// the engine, and it owns every "campaign:" log line. Mission end records the attempt through
/// <see cref="CampaignProgression"/>, folds the destruction log into the profile and raises
/// <see cref="ReturnToCabin"/> for the session layer to act on.
/// </summary>
public sealed class CampaignDirector
{
    /// <summary>The name the original registers the campaign wingman's aircraft under
    /// (<c>docs/formats/saved-games.md</c>, <c>docs/formats/campaign-screens.md</c>), and the
    /// roster block <see cref="BuildRoster"/> flies the profile's <see cref="WingmanNode"/> and
    /// <see cref="WingmanFit"/> as.</summary>
    public const string WingmanName = "wingman_1";

    private readonly CampaignProfileDef _profile;
    private readonly CampaignProfileStore? _store;
    private readonly CampaignMission _mission;
    private readonly string _missionZrdrPath;
    private readonly HashSet<string> _gapsLogged = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FlightController> _roster = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RosterSpawnPlan> _rosterPlans = new(StringComparer.OrdinalIgnoreCase);
    private World? _world;
    private ScriptedPathVehicles? _paths;

    // What the three SET_AI_* directives need after the roster phase has run: the chapter's nets
    // by name, the trailer resolver a re-commanded net needs to ride its target, and the floor
    // every activation radius takes. Kept because a mission re-commands its aircraft long after
    // BuildRoster's inputs have gone out of scope.
    private IReadOnlyList<AiNet> _chapterNets = Array.Empty<AiNet>();
    private NetTrailerTargets? _netTrailers;
    private float _minAiActiveDist = 2000f;
    private CampaignDangerZones? _dangerZones;
    private bool _cutsceneHold;

    // The proximity scan's accumulator and whether the player's damage event is subscribed yet:
    // the player's aircraft is built after Attach runs, so the hookup is made on the first step
    // that finds one.
    private float _scanClock;
    private bool _damageWired;
    private bool _deathWired;
    private bool _playerLost;

    private CampaignDirector(
        ObjectiveScript script, CampaignMission mission,
        CampaignProfileDef profile, CampaignProfileStore? store, string missionZrdrPath)
    {
        Script = script;
        _mission = mission;
        _profile = profile;
        _store = store;
        _missionZrdrPath = missionZrdrPath;
    }

    /// <summary>The authored-aircraft spawner, handed in as a delegate for the same reason
    /// <c>InstantActionDirector</c>'s is: the spawner and its roster stay <c>GameSession</c>'s.</summary>
    internal delegate FlightController? SpawnRosterAircraft(RosterSpawnPlan plan, Vector3 pos,
        Vector3 lookAt, AiPilot pilot);

    /// <summary>Fired once the mission has ended and the profile has been written.</summary>
    public event Action<CampaignMissionResult>? MissionEnded;

    /// <summary>The process's music channel, or null when the session was built without one. The
    /// mission's own <c>WAKEUP_SOUND_GROUP music_*_sg</c> is what cues prebattle, the stingers and
    /// both success tracks, so nothing here hard-codes a state (<c>docs/org/music.md</c>).</summary>
    public MusicPlayer? Music { get; set; }

    /// <summary>The stock node the wingman's aircraft would fly as, or null when this mission has
    /// no wingman. Read with <see cref="WingmanFit"/> by whoever spawns it.</summary>
    public string? WingmanNode { get; private set; }

    /// <summary>The wingman aircraft's ammunition and ordnance picks off the profile, or null when
    /// this mission has no wingman.</summary>
    public LoadoutChoice? WingmanFit { get; private set; }

    /// <summary>Whether losing the player's aircraft ends the mission, which is the original's rule
    /// and the default here. <c>--no-crash-loss</c> clears it so a session being debugged can fly
    /// on past a crash; <c>GameSession</c> is the only writer.</summary>
    public bool EndsOnPlayerDeath { get; set; } = true;

    /// <summary>The mission's parsed choreography script.</summary>
    public ObjectiveScript Script { get; }

    /// <summary>The objectives runtime, null until <see cref="Attach"/> has run. D33 reads its
    /// <see cref="ObjectiveGraph.Rows"/> and subscribes to its wake/complete events.</summary>
    public ObjectiveGraph? Graph { get; private set; }

    /// <summary>The mission's scripted-path vehicles, null until <see cref="BuildRoster"/> or
    /// <see cref="Attach"/> has run. <see cref="BuildRoster"/> places a vehicle carrying a
    /// <c>taxiPath</c> here; <c>START_TAXI</c> releases it.</summary>
    public ScriptedPathVehicles? Paths => _paths;

    /// <summary>The spawned roster, by block name, empty until <see cref="BuildRoster"/> has run.
    /// The player's own block is not in it.</summary>
    public IReadOnlyDictionary<string, FlightController> Roster => _roster;

    /// <summary>The story position being flown.</summary>
    public int Seq => _mission.Seq;

    /// <summary>Raised when the mission has ended and its result is banked: the session layer's cue
    /// to leave the world and put the player back in the cabin. The cabin screen itself is C22's.</summary>
    public bool ReturnToCabin { get; private set; }

    /// <summary>The result of the flown mission, null until it ends.</summary>
    public CampaignMissionResult? Result { get; private set; }

    /// <summary>How many danger-zone gates <see cref="Attach"/> armed from a real
    /// <see cref="WorldInputs.Gamez"/>. 0 before <see cref="Attach"/>, or when the mission
    /// names no <c>DANGER_ZONES_COMPLETED</c> zone, or none resolved.</summary>
    internal int ArmedDangerZones => _dangerZones?.Count ?? 0;

    /// <summary>Resolves a <c>--campaign=&lt;profile&gt;:&lt;seq&gt;</c> launch's chapter and
    /// mission out of <c>cm_sequence.zrd</c>, so the rest of the build sees an ordinary
    /// chapter/mission session. Returns the spec unchanged when no campaign mission was asked for,
    /// or when the story position is not one the sequence carries.</summary>
    public static SessionSpec ResolveSpec(SessionSpec spec, string zrdrPath)
    {
        if (spec.CampaignProfile == null || spec.CampaignMissionSeq is not { } seq)
        {
            return spec;
        }

        if (MissionFor(zrdrPath, seq) is not { } mission || mission.ChapterFolder.Length == 0)
        {
            GD.PushWarning($"--campaign: seq {seq} is not in cm_sequence — flying the CLI chapter instead");
            return spec;
        }

        GD.Print($"campaign: seq {seq} '{mission.Desc}' -> {mission.ChapterFolder}/{mission.MissionFolder}");
        return spec.WithCampaignMission(
            mission.ChapterFolder.ToUpperInvariant(), mission.MissionFolder.ToUpperInvariant());
    }

    /// <summary>Construction, the shape <see cref="InstantActionDirector.TryCreate"/> has: null
    /// outside a campaign launch, and a profile that cannot be loaded warns and flies without a
    /// director rather than aborting the launch.</summary>
    public static CampaignDirector? TryCreate(SessionSpec spec, string zrdrPath, string missionZrdrPath)
    {
        if (spec.CampaignProfile == null || spec.CampaignMissionSeq is not { } seq)
        {
            return null;
        }

        var store = CampaignProfileStore.UserProfiles();
        if (store.Load(spec.CampaignProfile) is not { } profile)
        {
            GD.PushWarning($"--campaign={spec.CampaignProfile}: no such profile — flying without a mission");
            return null;
        }

        if (MissionFor(zrdrPath, seq) is not { } mission)
        {
            return null;
        }

        var script = ObjectiveScript.Load(missionZrdrPath);
        GD.Print($"campaign: '{profile.Name}' flying {mission.ChapterFolder}/{mission.MissionFolder}, " +
                 $"{script.Objectives.Count} objective(s)");
        var director = new CampaignDirector(script, mission, profile, store, missionZrdrPath);
        director.BindWingman();
        return director;
    }

    /// <summary>The suite/test entry: a director over an already-loaded script, mission and
    /// profile, with no profile file behind it unless one is handed in. <paramref
    /// name="missionZrdrPath"/> is only needed to arm danger zones (its <c>dzones.zrd</c>
    /// disable list); omitted, nothing is disabled.</summary>
    internal static CampaignDirector Create(
        ObjectiveScript script, CampaignMission mission,
        CampaignProfileDef profile, CampaignProfileStore? store, string missionZrdrPath = "") =>
        new(script, mission, profile, store, missionZrdrPath);

    /// <summary>The roster phase: spawns every non-player block of the mission's <c>aiv</c>
    /// roster, at the point of <c>GameSession</c>'s build where the human rigs exist. The plan is
    /// <see cref="CampaignRosterPlan"/>; this is the placement, the leader pass and the log.
    /// Returns the build-summary suffix, the shape <c>InstantActionDirector.BuildActors</c> has.</summary>
    internal string BuildRoster(RosterInputs inputs)
    {
        List<(string Name, List<object?> Fields)> blocks;
        List<AiNet> nets;
        VehicleDefs defs;
        try
        {
            blocks = AiSkills.LoadRoster(inputs.MissionZrdrPath);
            nets = AiNets.Load(inputs.ChapterZrdrPath);
            defs = VehicleDefs.Load(inputs.ZrdrPath);
        }
        catch (Exception e)
        {
            GD.PushWarning($"campaign: cannot read the roster ({e.Message}): no roster spawned");
            return "";
        }

        _chapterNets = nets;
        _netTrailers = inputs.NetTrailers;
        _minAiActiveDist = inputs.MinAiActiveDist;

        // wingman_4 flies the player's own aeroplane in the two missions the swap hands it over in,
        // and its own def's everywhere else.
        var handover = AirframeHandover.Resolves(_mission.ChapterFolder, _mission.MissionFolder)
            ? inputs.PlayerAirframe
            : null;
        var plan = CampaignRosterPlan.Build(blocks, defs, nets, WingmanNode, WingmanFit,
            netDraw: count => inputs.Rng.Next(count), handover: handover);
        foreach (var (name, why) in plan.Skipped)
        {
            if (!name.Equals(CampaignRosterPlan.PlayerBlock, StringComparison.OrdinalIgnoreCase))
            {
                GD.Print($"campaign: roster '{name}' not spawned: {why}");
            }
        }

        if (_paths == null && inputs.FindNodes is { } findNodes)
        {
            _paths = new ScriptedPathVehicles(findNodes);
        }

        float minActive = inputs.MinAiActiveDist;
        foreach (var spawn in plan.Spawns)
        {
            // The block's own coordinates and yaw, as the original's roster spawn places. A world
            // node of the block's name is preferred where one exists, so a mission that animates
            // the aircraft into place agrees with the roster; most blocks have none.
            var pos = spawn.Position;
            var fwd = spawn.Forward;
            if (inputs.FindNodes?.Invoke(spawn.Name) is { Count: > 0 } nodes
                && nodes[0].IsInsideTree())
            {
                pos = nodes[0].GlobalPosition;
                fwd = -nodes[0].GlobalTransform.Basis.Z;
            }

            var pilot = AiPilot.HoldingCourse(pos, pos + fwd);
            if (spawn.Net is { } net)
            {
                pilot.Patrol = new AiNetFollower(net, Utils.Rng.NewSystemRandom(Utils.Rng.Ai),
                    trailerTarget: inputs.NetTrailers?.For(net));
            }
            var rig = inputs.Spawn(spawn, pos, pos + fwd, pilot);
            if (rig == null)
            {
                continue;
            }
            _roster[spawn.Name] = rig;
            _rosterPlans[spawn.Name] = spawn;
            // The chapter's own copy of this vehicle is never placed, so anything it authors past
            // the shared airframe is grafted onto the rig here, while the rig is the plane the
            // block named and is already in the tree.
            inputs.AttachMarkers?.Invoke(spawn.Name, rig);

            CampaignRosterPlan.ApplyPlan(pilot, spawn, minActive);
            inputs.RegisterVoice(rig, spawn.AccentId, spawn.Skills.Talker, spawn.Skills.Constitution);

            bool placed = false;
            if (spawn.TaxiPath is { } taxi && _paths != null)
            {
                placed = PlaceOnPath(rig, spawn.Name, taxi);
                if (!placed)
                {
                    GD.Print($"campaign: roster '{spawn.Name}' authors taxi path '{taxi}', which this world does not carry: it flies");
                }
            }

            // A downward probe against the built terrain collision, at the placed position: names
            // whether a roster spawn actually lands above ground (an under-ground spawn report is
            // settled off this line), sharing the World mask the flight model's ground-blow probe uses.
            string groundNote = new GodotWorldQuery(rig).Ray(pos + Vector3.Up * 3000f,
                pos - Vector3.Up * 3000f, CollisionLayers.World, null, out var groundHit)
                ? $" terrain={groundHit.Position.Y:0} ({pos.Y - groundHit.Position.Y:+0;-0} above it)"
                : " terrain=(no hit)";

            GD.Print($"campaign: roster '{spawn.Name}' ({spawn.Def} as {spawn.PlaneNode}, {spawn.Mode}) " +
                     $"team={spawn.Team?.ToString() ?? "-"} group={spawn.Group} " +
                     (spawn.Net is { } n ? $"net='{n.Name}#{n.Id}'"
                         : spawn.MissingNetId is { } missing ? $"net #{missing} MISSING from this chapter"
                         : spawn.Escorts ? $"escorts '{spawn.LeaderName ?? "(no leader)"}'"
                         : "no net") +
                     (spawn.Inert ? " DEACTIVATED" : "") +
                     (placed ? $" on path '{spawn.TaxiPath}'" : "") +
                     (spawn.Volumes.IsAuthored ? $" volumes act={spawn.Volumes.Activation.Radius:0} att={spawn.Volumes.Attack.Radius:0} ret={spawn.Volumes.Return.Radius:0}" : "") +
                     $" spawn=({pos.X:0},{pos.Y:0},{pos.Z:0}){groundNote}");
        }

        // The leader pass, once every rig exists: primary_target names a block that may be
        // spawned after its follower (wingman_2 before devastator_2 in the shipped order).
        int escorts = 0;
        foreach (var (name, spawn) in _rosterPlans)
        {
            if (!spawn.Escorts || _roster[name].Pilot is not { } pilot)
            {
                continue;
            }
            var leader = CampaignRosterPlan.ResolveLeader(spawn.LeaderName, _roster, inputs.Player());
            if (leader == null)
            {
                GD.Print($"campaign: roster '{name}' escorts '{spawn.LeaderName ?? ""}', which is not spawned: it holds its course");
                continue;
            }
            pilot.Escort = new AiEscort { Leader = leader };
            escorts++;
        }

        if (_roster.Count == 0)
        {
            return "";
        }
        int inert = 0;
        foreach (var spawn in _rosterPlans.Values)
        {
            inert += spawn.Inert ? 1 : 0;
        }
        GD.Print($"campaign: roster spawned {_roster.Count} of {blocks.Count} block(s): " +
                 $"{escorts} escort(s), {inert} deactivated, {_paths?.Count ?? 0} on a path");
        return $" + {_roster.Count} roster aircraft";
    }

    /// <summary>The world phase: binds the graph to the built world's runtimes. Called by
    /// <c>GameSession</c> once every runtime the objectives can touch is up.</summary>
    internal void Attach(WorldInputs inputs)
    {
        _world = new World(this, inputs);
        _paths ??= inputs.Runtime is { } animRuntime
            ? new ScriptedPathVehicles(name => animRuntime.FindNodes(name))
            : null;
        Graph = new ObjectiveGraph(Script, _world);
        Graph.MissionEnded += OnMissionEnded;
        _dangerZones = inputs.Gamez is { } gamez
            ? CampaignDangerZones.Load(Script, gamez, _missionZrdrPath)
            : null;
        int chapter = _mission.Campaign;
        int applied = inputs.Runtime != null ? _profile.PersistLog.ApplyTo(inputs.Runtime, chapter) : 0;
        GD.Print($"campaign: {Graph.Count} objective(s) armed, {Graph.Rows.Count} display row(s), " +
                 $"{applied} object(s) restored from the chapter {chapter} persist log" +
                 (_dangerZones is { } dz ? $", {dz.Count} danger zone(s) armed" : ""));
    }

    /// <summary>One sim step of the objectives graph, the scripted-path vehicles and the music
    /// channel's own battle detector. ⚠ Called from BOTH of <c>GameSession</c>'s drive paths, like
    /// the Instant Action sequencer: a realtime session never enters the stepped path. Under a
    /// cutscene hold nothing advances, which is callback 20's objectives half.</summary>
    internal void Step(float dt)
    {
        WirePlayerDeath();
        if (_cutsceneHold)
        {
            return;
        }

        StepPlayerLost();
        Graph?.Step(dt);
        if (_dangerZones != null && _world?.Player() is { } player)
        {
            _dangerZones.Update(player.WorldPosition, NotifyDangerZoneCompleted);
        }
        _paths?.Step(dt);
        StepMusic(dt);
    }

    /// <summary>The cutscene hold: while a cutscene owns the session the objectives update stops
    /// with the rest of the world, which is what callback 20 does in the original
    /// (docs/formats/anim-definitions/cutscenes.md). Dormancy timers and reminder fuses do not
    /// advance under the movie.</summary>
    internal void HoldForCutscene(bool held) => _cutsceneHold = held;

    /// <summary>A danger zone the player completed, routed into the graph's awake objectives.</summary>
    internal void NotifyDangerZoneCompleted(string zone) => Graph?.NotifyDangerZoneCompleted(zone);

    private static CampaignMission? MissionFor(string zrdrPath, int seq)
    {
        foreach (var mission in CampaignSequence.Load(zrdrPath))
        {
            if (mission.Seq == seq)
            {
                return mission;
            }
        }

        return null;
    }

    // The player's own death, subscribed on the first step that finds an aircraft: the player rig
    // is built after Attach has run, the same reason the music channel's damage ping waits.
    private void WirePlayerDeath()
    {
        if (_deathWired || _world?.Player() is not { } player)
        {
            return;
        }

        _deathWired = true;
        player.Downed += (_, _) => OnPlayerDown();
    }

    // Losing the aircraft loses the mission, in the original's two stages: the death stops the
    // objectives, and the wreck reaching the ground reaches the debrief. ⚠ Read the aircraft's own
    // Downed report, which is raised once per real death; the under-map backstop teleports without
    // one, so an altitude test here would end missions nobody lost (docs/verification.md INSTR-22).
    private void OnPlayerDown()
    {
        if (!EndsOnPlayerDeath || Graph is not { } graph || !graph.NotifyPlayerLost())
        {
            return;
        }

        _playerLost = true;
        GD.Print("campaign: the player's aircraft is lost — the objectives stop, and the mission ends where the wreck does");
    }

    // The second stage: a hull that is still falling has not landed yet, which is the whole of the
    // delay between the kill and the debrief.
    private void StepPlayerLost()
    {
        if (!_playerLost || Graph is not { } graph
            || _world?.Player() is { WreckFalling: true })
        {
            return;
        }

        _playerLost = false;
        graph.EndAfterPlayerLost();
    }

    // A path-driven aircraft: held (no flight integration) and re-pinned to the follower's pose
    // every tick, which is the original's exclusive movement-law switch; the handoff un-holds it
    // and re-activates it at the speed the path left it (docs/org/flightModel.md "The
    // scripted-path follower").
    private bool PlaceOnPath(FlightController rig, string name, string taxi)
    {
        bool placed = _paths!.Place(name, taxi, rig,
            onComplete: speed =>
            {
                rig.Held = false;
                var nose = rig.NoseDirection;
                rig.Activate(rig.WorldPosition, rig.WorldPosition + nose, nose * speed);
            },
            setPose: (p, heading) =>
            {
                var nose = new Basis(Vector3.Up, heading) * Vector3.Forward;
                rig.PlaceHeld(p, p + nose);
            });
        if (placed)
        {
            rig.Held = true;
        }
        return placed;
    }

    // The wingman's aircraft and fit off the profile, for the mission that has one. Resolved here
    // rather than by the launch shell because the profile is already open here; BuildRoster is
    // the consumer.
    private void BindWingman()
    {
        if (!_mission.Wingman)
        {
            return;
        }

        int at = _profile.WingmanPlane;
        if (at < 0 || at >= _profile.Planes.Count)
        {
            GD.PushWarning($"campaign: wingman plane index {at} is not one '{_profile.Name}' owns — no wingman fit bound");
            return;
        }

        var plane = _profile.Planes[at];
        WingmanNode = UI.PlanePickerRoster.AirframeNode(plane.Airframe);
        WingmanFit = CampaignLoadout.For(plane, StockLoadouts.Load());
        GD.Print($"campaign: {WingmanName} flies '{plane.Name}' as {WingmanNode}, bound for the roster spawn");
    }

    // The music channel's battle detector: the decoded five-second proximity scan, which runs only
    // while battle music is silent, plus the player-damage ping. The player's aircraft is built
    // after Attach, so the damage hookup waits for the first step that finds one.
    private void StepMusic(float dt)
    {
        if (Music == null || _world == null)
        {
            return;
        }

        if (!_damageWired && _world.Player() is { } player)
        {
            _damageWired = true;
            player.DamageApplied += _ => Music?.NoteCombat();
        }

        if (Music.State == MusicState.Battle)
        {
            return;
        }

        _scanClock += dt;
        if (_scanClock < MusicPlayer.BattleScanSeconds)
        {
            return;
        }

        _scanClock = 0f;
        if (MusicPlayer.ScanPings(_world.NearbyVehicles()))
        {
            Music.NoteCombat();
        }
    }

    private void OnMissionEnded(MissionOutcome outcome)
    {
        var graph = Graph!;
        var plane = _profile.SelectedPlane >= 0 && _profile.SelectedPlane < _profile.Planes.Count
            ? _profile.Planes[_profile.SelectedPlane]
            : null;
        // The money an attempt banks is the hangar economy's per-objective reward table, not this
        // director's: it reports zero and CampaignProgression banks what it is told.
        var attempt = new MissionAttempt(
            _mission.Seq,
            outcome == MissionOutcome.Won ? graph.CompletedMask : 0,
            (int)(graph.Elapsed * 1000f),
            _world?.Shots ?? 0,
            _world?.Hits ?? 0,
            0,
            plane?.Airframe ?? 0,
            plane?.Name ?? string.Empty);
        if (_world?.Runtime is { } runtime && CampaignPersistLog.CommitsOn(outcome))
        {
            _profile.PersistLog.Merge(_mission.Campaign, CampaignPersistLog.Capture(runtime));
        }

        var recorded = CampaignProgression.Record(_profile, attempt);
        _store?.Save(_profile);
        Result = new CampaignMissionResult(outcome, attempt, recorded);
        ReturnToCabin = true;
        GD.Print($"campaign: mission {_mission.Ordinal} {outcome} — mask 0x{attempt.CompletedMask:x}, " +
                 $"{attempt.TimeMs / 1000}s, primary={recorded.PrimaryCompleted}, " +
                 $"advanced={recorded.Advanced}, log {_profile.PersistLog.Count} object(s)");
        MissionEnded?.Invoke(Result.Value);
    }

    // A directive whose consumer this session has no seam for. Logged once per kind so a mission
    // that fires it every reminder loop does not flood the sink.
    private void Gap(string directive, string detail)
    {
        if (_gapsLogged.Add(directive))
        {
            GD.Print($"campaign: {directive} has no consumer in this session yet ({detail})");
        }
    }

    // The rig a mission clause's vehicle name addresses, or null when no roster block of that name
    // was spawned. The three SET_AI_* directives are one lookup with three different writes, so
    // they share this; a miss is reported by the caller rather than swallowed, because a mission
    // naming an aircraft that is not there is a real signal.
    private FlightController? Commanded(string name) =>
        _roster.TryGetValue(name, out var rig) ? rig : null;

    /// <summary>What <see cref="BuildRoster"/> reads from the session's build: the data paths,
    /// the first human, the net trailer resolver, the world's node lookup, and the two delegates
    /// <c>GameSession</c> keeps private behaviour behind (the spawner and the voice
    /// registration), the shape <c>InstantActionDirector.ActorBuildInputs</c> has.</summary>
    internal sealed class RosterInputs
    {
        public string ChapterZrdrPath = "";
        public string MissionZrdrPath = "";
        public string ZrdrPath = "";

        /// <summary>player.json's <c>min_ai_active_dist</c>, the floor under every activation
        /// radius.</summary>
        public float MinAiActiveDist = 2000f;

        /// <summary>What player 1 is flying, for the airframe hand-over's own roster block. Null
        /// leaves that block on its own def, which is what every other mission wants.</summary>
        public FlyingAirframe? PlayerAirframe;

        public Func<FlightController?> Player = () => null;
        public NetTrailerTargets? NetTrailers;
        public Func<string, IReadOnlyList<Node3D>>? FindNodes;
        public SpawnRosterAircraft Spawn = null!;

        /// <summary>Grafts the block's authored marker scaffolding onto the rig it just spawned
        /// (<c>Mech3/RosterMarkers.cs</c>). Null in a build with no world runtime, which leaves a
        /// chapter's additions to a vehicle unreachable exactly as they were before.</summary>
        public Action<string, Node3D>? AttachMarkers;

        public Action<FlightController?, int?, int?, int?> RegisterVoice = (_, _, _, _) => { };
        public Random Rng = new();
    }

    /// <summary>The built world's runtimes, handed over as one value the way
    /// <c>InstantActionDirector</c>'s build inputs are. Every reference may be null: a mission
    /// built without one simply leaves the directives that need it unconsumed.</summary>
    internal sealed class WorldInputs
    {
        public AnimRuntime? Runtime;
        public TurretEmplacementRuntime? Turrets;
        public AiGeneratorRuntime? Generators;
        public ZeppelinRuntime? Zeppelins;
        public WorldSounds? Sounds;
        public ProjectilePool? Projectiles;
        public Func<Vector3>? ListenerPosition;

        /// <summary>The chapter's parsed world geometry, for <see cref="CampaignDangerZones"/>'s
        /// gate read. Null leaves the mission's <c>DANGER_ZONES_COMPLETED</c> conditions
        /// unarmed, the same "no seam yet" shape as every other null here.</summary>
        public GameZ? Gamez;

        /// <summary>The player's aircraft once one exists, for the music channel's damage ping.
        /// A delegate rather than a value because the flight rigs are built after the world.</summary>
        public Func<FlightController?>? PlayerAircraft;

        /// <summary>Every aircraft in the session, read fresh: the music channel's proximity scan
        /// counts the ones near the player.</summary>
        public Func<IReadOnlyList<FlightController>>? Aircraft;

        public Random Rng = new();
    }

    // The engine side of IObjectiveWorld: the conditions the graph cannot answer itself, and the
    // world-touching actions. Every directive with no seam in this session is a NAMED no-op, never
    // an invented behaviour (docs/formats/objectives.md is the contract).
    private sealed class World : IObjectiveWorld
    {
        private readonly CampaignDirector _owner;
        private readonly WorldInputs _in;

        public World(CampaignDirector owner, WorldInputs inputs)
        {
            _owner = owner;
            _in = inputs;
        }

        public AnimRuntime? Runtime => _in.Runtime;

        public int Shots => _in.Projectiles?.CannonRoundsFired ?? 0;

        public int Hits => _in.Projectiles?.CannonHits ?? 0;

        public bool? NodeInactive(IReadOnlyList<string> path)
        {
            var node = Resolve(path);
            return node == null ? null : !node.Visible;
        }

        public int AnimState(string anim) => _in.Runtime?.AnimStateOf(anim) ?? 0;

        public int? GroupLiveCount(int group, string? generator)
        {
            // Null while no roster is spawned keeps every DEDG false rather than reading an empty
            // world as "the group is wiped out", which would win a mission on its first tick. A
            // deactivated member counts as alive, as the decoded walk counts a parked one.
            if (_owner._roster.Count == 0)
            {
                _owner.Gap("DEDG", $"group {group} has no spawned aiv roster to count");
                return null;
            }
            int alive = 0;
            foreach (var (name, plan) in _owner._rosterPlans)
            {
                if (plan.Group == group && !_owner._roster[name].Crashed)
                {
                    alive++;
                }
            }
            if (generator != null)
            {
                _owner.Gap("DEDG", $"the generator form ('{generator}') adds no remaining capacity yet");
            }
            return alive;
        }

        public bool? TravelersMet(TravelersSpec spec)
        {
            Vector3? reference = spec.WherePoint is { } p
                ? new Vector3(p[0], p[1], p[2])
                : Resolve(new[] { spec.WhereNode ?? string.Empty })?.GlobalPosition;
            if (reference == null)
            {
                return null;
            }

            // The group form: count live, non-inert members of the named aiv roster group inside
            // the radius, the same roster walk GroupLiveCount uses for DEDG. Null (not yet
            // decidable) while no roster is spawned, so an empty world never wins the tally early.
            if (spec.Group is { } group)
            {
                if (_owner._rosterPlans.Count == 0)
                {
                    _owner.Gap("TRAVELERS", $"group {group} has no spawned aiv roster to count");
                    return null;
                }

                int matching = 0;
                foreach (var (name, plan) in _owner._rosterPlans)
                {
                    if (plan.Group != group || !_owner._roster.TryGetValue(name, out var rig) || rig.Inert)
                    {
                        continue;
                    }

                    bool memberInside = rig.WorldPosition.DistanceSquaredTo(reference.Value) <= spec.Radius * spec.Radius;
                    if (memberInside == spec.Approaching)
                    {
                        matching++;
                    }
                }

                return matching >= spec.Count;
            }

            if (_in.ListenerPosition == null)
            {
                return null;
            }

            Vector3 subject = _in.ListenerPosition();
            if (!string.Equals(spec.Who, "player", StringComparison.OrdinalIgnoreCase))
            {
                if (Resolve(new[] { spec.Who }) is not { } who)
                {
                    return null;
                }

                subject = who.GlobalPosition;
            }

            bool inside = subject.DistanceSquaredTo(reference.Value) <= spec.Radius * spec.Radius;
            return spec.Approaching ? inside : !inside;
        }

        public void WakeupEnemies(IReadOnlyList<string> names)
        {
            // The partner of BOTH deactivated flags (docs/formats/objectives.md): the roster's,
            // which puts an inert aircraft back in play at its spawn pose, and a zeppelin record's,
            // which puts a hidden airship into the world. A name is one or the other, never both.
            int aircraft = 0, zeppelins = 0;
            foreach (var name in names)
            {
                if (_owner._roster.TryGetValue(name, out var rig) && _owner._rosterPlans.TryGetValue(name, out var plan)
                    && rig.Inert)
                {
                    rig.Activate(plan.Position, plan.Position + plan.Forward);
                    aircraft++;
                }
                else if (_in.Zeppelins?.Wake(name) == true)
                {
                    zeppelins++;
                }
            }
            if (aircraft + zeppelins > 0)
            {
                GD.Print($"campaign: WAKEUP_ENEMIES activated {aircraft} of {names.Count} named " +
                         $"aircraft and woke {zeppelins} zeppelin(s)");
            }
            else if (names.Count > 0)
            {
                _owner.Gap("WAKEUP_ENEMIES", $"'{names[0]}' and {names.Count - 1} more name no deactivated roster aircraft or zeppelin");
            }
        }

        public void WakeupTurrets(IReadOnlyList<string> patterns)
        {
            if (_in.Turrets == null || _in.Runtime == null)
            {
                return;
            }

            int armed = 0;
            foreach (var pattern in patterns)
            {
                foreach (var node in _in.Runtime.FindNodes(pattern))
                {
                    armed += _in.Turrets.SetActivatedUnder(node, true);
                }
            }

            if (armed > 0)
            {
                GD.Print($"campaign: WAKEUP_TURRETS armed {armed} emplacement(s)");
            }
        }

        public void WakeupZepTurrets(IReadOnlyList<string> nodes) => WakeupTurrets(nodes);

        public void WakeupGenerator(string name, int count)
        {
            int granted = _in.Generators?.GrantWaveCapacity(name, count) ?? 0;
            GD.Print($"campaign: WAKEUP_GENERATOR '{name}' +{count} (granted {granted})");
        }

        public void WakeAnim(string anim, string? node)
        {
            var anchor = node != null ? Resolve(new[] { node }) : null;
            int started = _in.Runtime?.Play(anim, anchor).Count ?? 0;
            GD.Print($"campaign: WAKE_ANIM '{anim}' started {started} definition(s)");
        }

        public FlightController? Player() => _in.PlayerAircraft?.Invoke();

        /// <summary>How many other live aircraft sit inside the decoded scan radius of the player.
        /// Wrecks are excluded; whether the original's own skip predicate excludes more than that
        /// is a named gap in <c>docs/org/music.md</c>.</summary>
        public int NearbyVehicles()
        {
            if (Player() is not { } player || _in.Aircraft == null)
            {
                return 0;
            }

            int near = 0;
            float radius = MusicPlayer.BattleScanRadiusM;
            foreach (var craft in _in.Aircraft())
            {
                if (!ReferenceEquals(craft, player) && !craft.Destroyed
                    && craft.WorldPosition.DistanceSquaredTo(player.WorldPosition) <= radius * radius)
                {
                    near++;
                }
            }

            return near;
        }

        public void PlaySoundGroup(string group)
        {
            // The music channel is a routing flag on the name, not a subsystem the data asks for:
            // a mu* group goes to the one streaming channel and never to a positional emitter
            // (docs/org/music.md, FUN_00593590).
            if (group.StartsWith("mu", StringComparison.OrdinalIgnoreCase))
            {
                _owner.Music?.Cue(group, _in.Rng);
                return;
            }

            if (_in.Sounds == null || _in.ListenerPosition == null)
            {
                return;
            }

            // A callout's definition carries QUEUE and never 3D, so it belongs on the radio queue
            // and not at a point in the world (docs/formats/sounds.md). A cue the radio does not
            // own returns 0 and falls through to the positional path that does.
            if (_in.Sounds.Radio is { } radio && radio.Cue(group, _in.Rng) > 0)
            {
                return;
            }

            _in.Sounds.PlayOneShot(group, _in.ListenerPosition(), _in.Rng);
        }

        public void StopQueuedSounds(IReadOnlyList<string> names)
        {
            if (names.Count == 0)
            {
                return;
            }

            if (_in.Sounds?.Radio is { } radio)
            {
                radio.Cancel(names);
                return;
            }

            _owner.Gap("STOP_QUEUED_SOUNDS", "this session built no mission radio queue");
        }

        public void WarpVehicle(string vehicle, IReadOnlyList<WarpPoint> points) =>
            _owner.Gap("WARP_VEHICLE", $"'{vehicle}' is not a spawned mission vehicle");

        /// <summary>The vehicle arm of <c>SET_AI_TEAM</c> (<c>FUN_00469e20</c>): the script's raw
        /// integer becomes the vehicle's team id with no conversion, in the one space the roster
        /// block's own <c>team</c> slot writes, and the setter drops the current target with it
        /// (docs/org/targeting.md).</summary>
        public void SetAiTeam(IReadOnlyList<(string Name, int Team)> entries)
        {
            int set = 0;
            var unmatched = new List<string>();
            foreach (var (name, team) in entries)
            {
                if (_owner.Commanded(name) is not { } rig)
                {
                    unmatched.Add(name);
                    continue;
                }

                rig.Team = team;
                if (rig.Pilot?.Gunner is { } gunner)
                {
                    gunner.Target = null;
                    gunner.GroundTarget = null;
                }
                set++;
            }
            Report("SET_AI_TEAM", set, entries.Count, unmatched);
        }

        /// <summary>Moves each named aircraft onto the named patrol net, the vehicle arm of
        /// <c>SET_AI_NET</c> (<c>FUN_00475f30</c> into <c>FUN_00475fc0</c>, docs/org/aiPilot.md
        /// "Net assignment"). A chapter that carries no net of that name leaves the aircraft on
        /// its current route, which is the engine's own no-match branch.</summary>
        public void SetAiNet(IReadOnlyList<(string Name, string Net)> entries)
        {
            int moved = 0;
            var unmatched = new List<string>();
            foreach (var (name, netName) in entries)
            {
                if (_owner.Commanded(name) is not { } rig || rig.Pilot is not { } pilot)
                {
                    unmatched.Add(name);
                    continue;
                }

                if (AiNets.ByName(_owner._chapterNets, netName) is not { } net)
                {
                    GD.Print($"campaign: SET_AI_NET '{netName}' is not a net this chapter carries: " +
                             $"'{name}' keeps the route it is on");
                    continue;
                }

                // A net outranks wingman mode, so the escort buffer goes with the assignment.
                pilot.Escort = null;
                // A fresh walk seats itself at the node nearest wherever the aeroplane IS, which is
                // what makes a mid-flight swap capture the new route instead of restarting it.
                pilot.Patrol = new AiNetFollower(net, Utils.Rng.NewSystemRandom(Utils.Rng.Ai),
                    trailerTarget: _owner._netTrailers?.For(net));
                // ⚠ Re-baseline on the vehicle's own ranges FIRST: ApplyVolumes skips a radius the
                // net authors as zero, so a second net authoring none leaves the previous one's
                // gates standing and an escort off a bomb-run net never leaves patrol (BL-504).
                if (pilot.Machine is { } gates && rig.Stats is { } defs)
                {
                    gates.AttackRange = defs.AiAttackRange;
                    gates.ReturnRange = defs.AiReturnRange;
                }

                // The net's own volumes overwrite the vehicle's where it authors them. Only the
                // spawn runs the roster block's copy afterwards, so here the net wins outright.
                CampaignRosterPlan.ApplyVolumes(pilot.Machine, net.Volumes, _owner._minAiActiveDist);
                moved++;
                GD.Print($"campaign: SET_AI_NET '{name}' onto '{net.Name}#{net.Id}'");
            }
            Report("SET_AI_NET", moved, entries.Count, unmatched);
        }

        /// <summary>Writes each named aircraft's attack volume radius (<c>FUN_00469f70</c>, vehicle
        /// <c>+0x328</c>). The altitude band the same write carries has no consumer here, the same
        /// place <see cref="CampaignRosterPlan.ApplyVolumes"/> leaves it. No shipped mission
        /// authors this directive, so only a modified script reaches it.</summary>
        public void SetAiAttackRadius(IReadOnlyList<(string Name, float Radius)> entries)
        {
            int set = 0;
            var unmatched = new List<string>();
            foreach (var (name, radius) in entries)
            {
                if (_owner.Commanded(name) is not { } rig || rig.Pilot?.Machine is not { } machine)
                {
                    unmatched.Add(name);
                    continue;
                }

                machine.AttackRange = radius;
                set++;
            }
            Report("SET_AI_ATTACK_RADIUS", set, entries.Count, unmatched);
        }

        public void CompletedZepcannons(IReadOnlyList<(string Zeppelin, int Flag)> entries)
        {
            if (entries.Count > 0)
            {
                _owner.Gap("COMPLETED_ZEPCANNONS", "which behaviour reads zeppelin byte +0xc is untraced");
            }
        }

        public void CompletedStoppoint(IReadOnlyList<(string Net, int Stop, int Flag)> entries)
        {
            if (entries.Count == 0)
            {
                return;
            }

            if (_in.Zeppelins is not { } zeppelins)
            {
                _owner.Gap("COMPLETED_STOPPOINT", "no zeppelin runtime in this session");
                return;
            }

            foreach (var (net, stop, flag) in entries)
            {
                zeppelins.SetStopPoint(net, stop, flag != 0);
            }
        }

        public void StartTaxi(IReadOnlyList<string> names)
        {
            int released = 0;
            foreach (var name in names)
            {
                if (_owner._paths?.Release(name) == true)
                {
                    released++;
                }
            }

            if (released > 0)
            {
                GD.Print($"campaign: START_TAXI released {released} vehicle(s) onto their paths");
            }
            else if (names.Count > 0)
            {
                _owner.Gap("START_TAXI", $"'{names[0]}' is not a spawned mission vehicle");
            }
        }

        // The shared tail of the three SET_AI_* directives: one line for what landed, one Gap for
        // what did not. ⚠ SET_AI_NET and SET_AI_TEAM also take a ZEPPELIN name (six clauses over
        // four missions), an arm with no seam here, so an unmatched name is always reported.
        private void Report(string directive, int applied, int total, List<string> unmatched)
        {
            if (applied > 0)
            {
                GD.Print($"campaign: {directive} applied to {applied} of {total} named vehicle(s)");
            }

            if (unmatched.Count > 0)
            {
                _owner.Gap(directive, $"'{unmatched[0]}' and {unmatched.Count - 1} more name no " +
                                      "spawned roster aircraft (a zeppelin is one such name)");
            }
        }

        // A node path: the first name resolved globally, then each named child inside the one
        // before it, which is how INACTIVEn walks ["piratezep","leng22","healthy"].
        private Node3D? Resolve(IReadOnlyList<string> path)
        {
            if (_in.Runtime == null || path.Count == 0)
            {
                return null;
            }

            Node3D? node = null;
            for (int i = 0; i < path.Count; i++)
            {
                var found = _in.Runtime.FindNodes(path[i], node);
                if (found.Count == 0)
                {
                    return null;
                }

                node = found[0];
            }

            return node;
        }
    }
}
