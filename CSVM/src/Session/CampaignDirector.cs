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
    /// (<c>docs/formats/saved-games.md</c>, <c>docs/formats/campaign-screens.md</c>). Nothing
    /// spawns that aircraft yet; <see cref="WingmanNode"/> and <see cref="WingmanFit"/> are what a
    /// spawn would bind, resolved here because this is where the profile already lives.</summary>
    public const string WingmanName = "wingman_1";

    private readonly CampaignProfileDef _profile;
    private readonly CampaignProfileStore? _store;
    private readonly CampaignMission _mission;
    private readonly HashSet<string> _gapsLogged = new(StringComparer.Ordinal);
    private World? _world;
    private ScriptedPathVehicles? _paths;

    // The proximity scan's accumulator and whether the player's damage event is subscribed yet:
    // the player's aircraft is built after Attach runs, so the hookup is made on the first step
    // that finds one.
    private float _scanClock;
    private bool _damageWired;

    private CampaignDirector(
        ObjectiveScript script, CampaignMission mission,
        CampaignProfileDef profile, CampaignProfileStore? store)
    {
        Script = script;
        _mission = mission;
        _profile = profile;
        _store = store;
    }

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

    /// <summary>The mission's parsed choreography script.</summary>
    public ObjectiveScript Script { get; }

    /// <summary>The objectives runtime, null until <see cref="Attach"/> has run. D33 reads its
    /// <see cref="ObjectiveGraph.Rows"/> and subscribes to its wake/complete events.</summary>
    public ObjectiveGraph? Graph { get; private set; }

    /// <summary>The mission's scripted-path vehicles, null until <see cref="Attach"/> has run. The
    /// roster spawner places a vehicle carrying a <c>taxiPath</c> here; <c>START_TAXI</c> releases
    /// it. Empty while no session spawns the <c>aiv</c> roster.</summary>
    public ScriptedPathVehicles? Paths => _paths;

    /// <summary>The story position being flown.</summary>
    public int Seq => _mission.Seq;

    /// <summary>Raised when the mission has ended and its result is banked: the session layer's cue
    /// to leave the world and put the player back in the cabin. The cabin screen itself is C22's.</summary>
    public bool ReturnToCabin { get; private set; }

    /// <summary>The result of the flown mission, null until it ends.</summary>
    public CampaignMissionResult? Result { get; private set; }

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
        var director = new CampaignDirector(script, mission, profile, store);
        director.BindWingman();
        return director;
    }

    /// <summary>The suite/test entry: a director over an already-loaded script, mission and
    /// profile, with no profile file behind it unless one is handed in.</summary>
    internal static CampaignDirector Create(
        ObjectiveScript script, CampaignMission mission,
        CampaignProfileDef profile, CampaignProfileStore? store) =>
        new(script, mission, profile, store);

    /// <summary>The world phase: binds the graph to the built world's runtimes. Called by
    /// <c>GameSession</c> once every runtime the objectives can touch is up.</summary>
    internal void Attach(WorldInputs inputs)
    {
        _world = new World(this, inputs);
        _paths = inputs.Runtime is { } animRuntime
            ? new ScriptedPathVehicles(name => animRuntime.FindNodes(name))
            : null;
        Graph = new ObjectiveGraph(Script, _world);
        Graph.MissionEnded += OnMissionEnded;
        int chapter = _mission.Campaign;
        int applied = inputs.Runtime != null ? _profile.PersistLog.ApplyTo(inputs.Runtime, chapter) : 0;
        GD.Print($"campaign: {Graph.Count} objective(s) armed, {Graph.Rows.Count} display row(s), " +
                 $"{applied} object(s) restored from the chapter {chapter} persist log");
    }

    /// <summary>One sim step of the objectives graph, the scripted-path vehicles and the music
    /// channel's own battle detector. ⚠ Called from BOTH of <c>GameSession</c>'s drive paths, like
    /// the Instant Action sequencer: a realtime session never enters the stepped path.</summary>
    internal void Step(float dt)
    {
        Graph?.Step(dt);
        _paths?.Step(dt);
        StepMusic(dt);
    }

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

    // The wingman's aircraft and fit off the profile, for the mission that has one. Resolved here
    // rather than by the launch shell because the profile is already open here; nothing spawns the
    // aircraft yet, so this is a binding waiting for its consumer (D34), not a live wingman.
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
        GD.Print($"campaign: {WingmanName} flies '{plane.Name}' as {WingmanNode} — bound, not yet spawned");
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
        if (_world?.Runtime is { } runtime)
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

    /// <summary>The built world's runtimes, handed over as one value the way
    /// <c>InstantActionDirector</c>'s build inputs are. Every reference may be null: a mission
    /// built without one simply leaves the directives that need it unconsumed.</summary>
    internal sealed class WorldInputs
    {
        public AnimRuntime? Runtime;
        public TurretEmplacementRuntime? Turrets;
        public AiGeneratorRuntime? Generators;
        public WorldSounds? Sounds;
        public ProjectilePool? Projectiles;
        public Func<Vector3>? ListenerPosition;

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
            // The campaign's aiv roster is not spawned by any session yet, so there is no AI-group
            // population to count. Reporting null keeps every DEDG false instead of reading an
            // empty world as "the group is wiped out", which would win missions on the first tick.
            _owner.Gap("DEDG", $"group {group} has no spawned aiv roster to count");
            return null;
        }

        public bool? TravelersMet(TravelersSpec spec)
        {
            if (spec.Group != null || _in.ListenerPosition == null)
            {
                _owner.Gap("TRAVELERS", "the group form needs a spawned aiv roster");
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

            Vector3? reference = spec.WherePoint is { } p
                ? new Vector3(p[0], p[1], p[2])
                : Resolve(new[] { spec.WhereNode ?? string.Empty })?.GlobalPosition;
            if (reference == null)
            {
                return null;
            }

            bool inside = subject.DistanceSquaredTo(reference.Value) <= spec.Radius * spec.Radius;
            return spec.Approaching ? inside : !inside;
        }

        public void WakeupEnemies(IReadOnlyList<string> names)
        {
            if (names.Count > 0)
            {
                _owner.Gap("WAKEUP_ENEMIES", $"'{names[0]}' and {names.Count - 1} more");
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

            // The mission radio queue (1 s spacing, 15 s between speech lines) is D33's; this plays
            // the group straight through the existing resolution, at the listener.
            _in.Sounds.PlayOneShot(group, _in.ListenerPosition(), _in.Rng);
        }

        public void StopQueuedSounds(IReadOnlyList<string> names)
        {
            if (names.Count > 0)
            {
                _owner.Gap("STOP_QUEUED_SOUNDS", "there is no mission radio queue to cancel from yet");
            }
        }

        public void WarpVehicle(string vehicle, IReadOnlyList<WarpPoint> points) =>
            _owner.Gap("WARP_VEHICLE", $"'{vehicle}' is not a spawned mission vehicle");

        public void SetAiTeam(IReadOnlyList<(string Name, int Team)> entries)
        {
            if (entries.Count > 0)
            {
                _owner.Gap("SET_AI_TEAM", $"'{entries[0].Name}' is not a spawned mission vehicle");
            }
        }

        public void SetAiNet(IReadOnlyList<(string Name, string Net)> entries)
        {
            if (entries.Count > 0)
            {
                _owner.Gap("SET_AI_NET", $"'{entries[0].Name}' is not a spawned mission vehicle");
            }
        }

        public void SetAiAttackRadius(IReadOnlyList<(string Name, float Radius)> entries)
        {
            if (entries.Count > 0)
            {
                _owner.Gap("SET_AI_ATTACK_RADIUS", $"'{entries[0].Name}' is not a spawned mission vehicle");
            }
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
            if (entries.Count > 0)
            {
                _owner.Gap("COMPLETED_STOPPOINT", $"net '{entries[0].Net}' has no live stop-point state");
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
