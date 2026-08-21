using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Session;

/// <summary>
/// The engine-side sequencing of one Instant Action mission: the mission's wave state, the ace,
/// and (as the deepening proceeds) the actor build phases, the sequencer tick and the end-condition
/// wiring. The decoded rules stay engine-free in <see cref="InstantActionRuntime"/> and
/// <see cref="InstantActionWaves"/>; this class is where they meet the engine. Held as
/// <c>GameSession</c>'s one nullable director field — null outside a mission, which is what keeps
/// every other session mode (free flight, Dogfight) untouched by its existence.
///
/// Not a Node: <c>GameSession</c> owns the tick order (its ⚠ on <c>DriveSimSteps</c>) and calls
/// the phases at its own decoded points, and every node built on this mission's behalf parents
/// under the session's <c>_worldRoot</c>, so the session's no-Teardown rule holds unchanged.
/// </summary>
public sealed class InstantActionDirector
{
    // E11's wave sequencer: null on dogfight_ace (every wave count is forced to 0).
    // WaveRosters[w] is wave w+1's built (inert until activated) members, indexed the same way;
    // the sequencer tick polls the current wave's alive count and activates whatever
    // InstantActionWaves.Step hands back — through the teleport arm, or (F12, zeppelin_run)
    // through the zeppelin generator's own wave credit.
    internal InstantActionWaves? Waves;
    internal List<FlightController>[]? WaveRosters;
    internal List<SpawnPoint>? WaveSpawnList;

    // F12: the wave whose parked members the objective zeppelin's generator is releasing — the
    // decoded group stamp (FUN_0045b9d0 writes the new counter to the generator's +0x64). 0
    // outside a zeppelin run, or before the first wave becomes current.
    internal int LaunchWave;

    // G14: the authored ace, kept past the build (not just a build-phase local) so
    // --debug-scoreboard can force it down from the drive path, well after every Downed
    // subscription the end-condition block wires is in place. Null outside dogfight_ace.
    internal FlightController? Ace;

    // --debug-scoreboard (IA): single-fire, same shape as GameSession's
    // _crashFired/_versusDebugKillFired.
    internal bool DebugForceFired;

    // The militia -> paint-pattern table, loaded once per mission for the wave liveries.
    private Dictionary<string, string>? _militiaPatterns;

    // The session's rigs and (zeppelin run only) its generator runtime, kept from the build
    // phases so the sequencer tick can activate a wave without reaching back into GameSession.
    private List<PlayerRig>? _rigs;
    private AiGeneratorRuntime? _generators;

    private InstantActionDirector(InstantActionRuntime runtime)
    {
        Runtime = runtime;
    }

    /// <summary>The authoring SpawnAiAircraft overload, handed in as a lambda for the same reason
    /// AiGeneratorRuntime takes one: the spawner and its roster stay GameSession's.</summary>
    internal delegate FlightController? SpawnAuthoredAircraft(string planeName, Vector3 pos,
        Vector3 lookAt, AiPilot pilot, PaintScheme? scheme, int? team, int? attackRating,
        bool inert, bool shippedSkins, Flight.LoadoutChoice? fit);

    /// <summary>GameSession.RegisterAiVoice: the voice runtime serves non-mission spawns too.</summary>
    internal delegate void RegisterAiVoice(FlightController? ai, int? accentId, int? ratingOverride);

    /// <summary>The engine-free mission runtime: the loaded def, the objective bookkeeping and
    /// the decoded actor rules. Never null on a built director.</summary>
    public InstantActionRuntime Runtime { get; }

    /// <summary>Every built wave member, across all four waves. 0 until BuildActors has run.</summary>
    internal int WaveEnemyCount => WaveRosters?.Sum(r => r.Count) ?? 0;

    // ⚠ Keep both producers (the wizard's SessionSpec.IaDef and --ia=<path>) converging on the
    // one InstantActionRuntime construction; two similar calls is the failure this avoids.
    // A failed --ia= load warns and flies without a mission rather than aborting the launch.
    public static InstantActionDirector? TryCreate(SessionSpec spec)
    {
        if (spec.IaDef is { } wizardDef)
        {
            GD.Print($"ia: wizard mission_type={wizardDef.MissionType} " +
                     $"player='{wizardDef.PlayerPlane}' ace='{wizardDef.AceName}' ({wizardDef.AcePlane})");
            return new InstantActionDirector(new InstantActionRuntime(wizardDef));
        }
        if (spec.IaPath != null)
        {
            try
            {
                var def = InstantAction.LoadFromJson(spec.IaPath);
                GD.Print($"ia: '{spec.IaPath}' mission_type={def.MissionType} " +
                         $"player='{def.PlayerPlane}' ace='{def.AceName}' ({def.AcePlane})");
                return new InstantActionDirector(new InstantActionRuntime(def));
            }
            catch (Exception e)
            {
                GD.PushWarning($"--ia={spec.IaPath}: cannot load ({e.Message}) — flying without a mission");
            }
        }
        return null;
    }

    /// <summary>The mission's actor build: the chapter's patrol net, the ace (dogfight_ace), the
    /// wingmen, and every wave's inert roster — one contiguous phase of GameSession's
    /// BuildFlightRigs, called at the same point in its build order. Returns the build-summary
    /// suffix ("+ IA ace" and kin) for the session's one-line report.</summary>
    internal string BuildActors(ActorBuildInputs inputs)
    {
        string what = "";
        _rigs = inputs.Rigs;
        // ⚠ Hand every Instant Action actor the chapter's FIRST patrol net, ace, wingmen and wave
        // members alike, and read "first" as neindex FILE order, never the lowest id
        // (docs/formats/instant-action.md).
        AiNet? iaPatrolNet = null;
        try
        {
            iaPatrolNet = AiNets.ChapterFirst(AiNets.Load(inputs.ChapterZrdrPath),
                inputs.ChapterZrdrPath);
        }
        catch (Exception e)
        {
            GD.PushWarning($"ia: cannot read {inputs.Chapter}'s patrol nets: {e.Message}");
        }
        if (iaPatrolNet is { Nodes.Count: 0 })
        {
            iaPatrolNet = null;
        }
        GD.Print(iaPatrolNet != null
            ? $"ia: actors patrol '{iaPatrolNet.Name}' (net {iaPatrolNet.Id}), the chapter's first"
              + (iaPatrolNet.Trailer is { NodeIndex: >= 0, Name: { } anchorName }
                  ? $", anchored to '{anchorName}' at node {iaPatrolNet.Trailer.Value.NodeIndex}"
                  : "")
            : $"ia: {inputs.Chapter} has no first patrol net, actors fly their spawn course");
        // The net IS the standing order. ⚠ Do not let it bring its own volumes: the original copies
        // the roster block's volumes over the net's afterwards, so ApplyActorVolumes has the last
        // word (docs/formats/instant-action.md).
        Action<AiPilot> armIaPatrol = pilot =>
        {
            if (iaPatrolNet == null)
                return;
            pilot.Patrol = new AiNetFollower(iaPatrolNet, Rng.NewSystemRandom(Rng.Ai),
                trailerTarget: inputs.NetTrailers.For(iaPatrolNet));
        };
        var ia = Runtime;
        if (string.Equals(ia.Def.MissionType, "dogfight_ace", StringComparison.OrdinalIgnoreCase))
        {
            string? aceNode = InstantAction.PlaneNodeFor(ia.Def.AcePlane);
            if (aceNode == null)
            {
                GD.PushWarning($"ia: ace plane '{ia.Def.AcePlane}' is not one of the eleven " +
                                "airframes — no ace spawned");
            }
            else if (SpawnPoints.LoadIa(inputs.MissionZrdrPath, ia.Def.MissionType) is not { Count: > 0 } aceSpawns)
            {
                GD.PushWarning($"ia: no '{ia.Def.MissionType}' spawn points for " +
                                $"{inputs.Chapter}/{inputs.Mission} — no ace spawned");
            }
            else
            {
                int playerSpawnIndex = inputs.SpawnBase % aceSpawns.Count;
                uint draw = Rng.Stream(Rng.Spawn).Randi();
                var (spIndex, sp) = InstantActionRuntime.ChooseAceSpawn(aceSpawns, playerSpawnIndex, draw);
                var fwd = new Basis(Vector3.Up, Mathf.DegToRad(sp.HeadingDeg)) * Vector3.Forward;
                var pilot = AiPilot.HoldingCourse(sp.Position, sp.Position + fwd);
                armIaPatrol(pilot);
                int rating = InstantActionRuntime.RepresentativeRating(ia.Def.AceStats);
                // shippedSkins for the same reason as a wave member below: the ace flies for an
                // enemy militia, so an ia.json without ace_pattern (hand-authored only) falls back
                // to its own textures, never the player militia's Fortune Hunters default.
                var ace = inputs.Spawn(aceNode, sp.Position, sp.Position + fwd, pilot,
                    ia.Def.AceLivery, InstantActionRuntime.EnemyTeam,
                    rating, inert: false, shippedSkins: true, fit: null);
                inputs.RegisterVoice(ace, ia.Def.AceAccentId, rating);
                Ace = ace;
                InstantActionRuntime.ApplyActorVolumes(pilot.Machine);
                if (ace != null)
                {
                    GD.Print($"ia: ace '{ia.Def.AceName}' ({aceNode}) rating={rating} " +
                              $"team={InstantActionRuntime.EnemyTeam} spawn #{spIndex} of {aceSpawns.Count}");
                    what += " + IA ace";
                }
            }
        }
        // The wingmen. NumWingmen is forced to 0 on dogfight_ace at parse time, so this and the
        // ace block above are exclusive without an extra mission-type test.
        if (ia.Def.NumWingmen > 0
            && inputs.Rigs.Count > 0 && inputs.Rigs[0].Controller is { } leadForWingmen)
        {
            string? wingmanNode = InstantAction.PlaneNodeFor(ia.Def.WingmanPlane);
            if (wingmanNode == null)
            {
                GD.PushWarning($"ia: wingman plane '{ia.Def.WingmanPlane}' is not one of " +
                                "the eleven airframes — no wingmen spawned");
            }
            else
            {
                int humans = inputs.Rigs.Count;
                int flown = InstantActionRuntime.FlownWingmen(ia.Def.NumWingmen, humans);
                if (flown < ia.Def.NumWingmen)
                {
                    GD.Print($"ia: wingmen clamped to {flown} of {ia.Def.NumWingmen} " +
                              $"configured ({humans} human(s), flight cap 6 — decision 8a)");
                }
                // player_fortune: the wingmen's shared livery. The colour and decal values ride the
                // setup screen in the original, not ia.json, so the catalog entry stands in
                // (docs/formats/paint.md).
                var wingmanScheme = inputs.LiveryResolver.PaintCatalog(inputs.ZrdrPath)
                    .Find(s => string.Equals(s.Pattern, LiveryResolver.DefaultPattern, StringComparison.OrdinalIgnoreCase));
                if (wingmanScheme == null)
                {
                    GD.PushWarning("ia: no 'player_fortune' entry in the paint catalog — wingmen " +
                                    "fly unpainted/random");
                }
                var wmBasis = leadForWingmen.GlobalTransform.Basis;
                var wmFwd = -wmBasis.Z;
                var wmLeadPos = leadForWingmen.WorldPosition;
                var wingmen = new FlightController?[flown];
                for (int i = 0; i < flown; i++)
                {
                    var slot = InstantActionRuntime.WingmanSlotFor(i);
                    var offsetDir = wmFwd.Rotated(Vector3.Up, Mathf.DegToRad(slot.OffsetDeg));
                    var pos = wmLeadPos + offsetDir * slot.MetresOut;
                    var pilot = AiPilot.HoldingCourse(pos, pos + wmFwd);
                    // The wingman takes the same net the ace and the waves do, which in the original
                    // demotes it out of wingman mode, so the escort chain below is a target
                    // assignment and not a flown formation (docs/org/aiPilot.md).
                    armIaPatrol(pilot);
                    // ⚠ Pass an explicit rating, never null: a wingman's Gunner and Machine are only
                    // built when one resolves, and null would arm them solely on a launch that
                    // happened to carry --ai-attack= (docs/formats/instant-action.md).

                    // The wizard's one wingman fit, covering the whole flight as the original's
                    // Player/Wingman radio does. Passed per spawn, never as a blanket default: the
                    // stock-table branch it lands in also catches enemies on player airframes.
                    var wingman = inputs.Spawn(wingmanNode, pos, pos + wmFwd, pilot,
                        wingmanScheme, AimAssist.PlayerTeam, attackRating: 5,
                        inert: false, shippedSkins: false, fit: ia.Def.WingmanLoadout);
                    wingmen[i] = wingman;
                    if (wingman == null)
                        continue;
                    // primary_target: 0, 1 and 3 escort the player; 2 and 4 escort
                    // wingmen 1 and 3 — FlightController.SelectRankedTarget's own by-name/"player"
                    // match, the same seam the D12 ranking already reads.
                    if (pilot.Gunner != null)
                    {
                        pilot.Gunner.PrimaryTargetName = slot.PrimaryTargetIsWingman is { } escortIdx
                            ? wingmen[escortIdx]?.Name.ToString()
                            : "player";
                    }
                    // An Instant Action actor's volumes are all authored far wider than the airframe
                    // defaults SpawnAiAircraft arms (docs/formats/instant-action.md).
                    InstantActionRuntime.ApplyActorVolumes(pilot.Machine);
                    inputs.RegisterVoice(wingman, slot.AccentId, null);
                }
                int wmSpawned = wingmen.Count(w => w != null);
                if (wmSpawned > 0)
                {
                    GD.Print($"ia: {wmSpawned} wingman(s) ({wingmanNode}) team={AimAssist.PlayerTeam}");
                    what += $" + {wmSpawned} IA wingmen";
                }
            }
        }
        // The wave sequencer. ⚠ Build EVERY wave inert here, wave 1 included, so one
        // build-then-activate path serves them all; the mission type decides a wave's ARRIVAL, not
        // whether it is built (docs/formats/instant-action.md).
        var waveSizes = ia.Def.Waves.Select(w => w.NumEnemies).ToArray();
        var rosters = new List<FlightController>[4];
        for (int w = 0; w < 4; w++)
        {
            var roster = new List<FlightController>();
            var wave = ia.Def.Waves[w];
            if (wave.NumEnemies > 0)
            {
                string? waveNode = InstantAction.PlaneNodeFor(wave.EnemyPlane);
                if (waveNode == null)
                {
                    GD.PushWarning($"ia: wave {w + 1} plane '{wave.EnemyPlane}' is not one " +
                                    "of the eleven airframes — no wave enemies spawned");
                }
                else
                {
                    for (int m = 0; m < wave.NumEnemies; m++)
                    {
                        // Built at the origin, inert — position is irrelevant until
                        // ActivateInstantActionWave teleports it in, same as the original's
                        // own "deactivated at the world origin".
                        var pilot = AiPilot.HoldingCourse(Vector3.Zero, Vector3.Forward);
                        // Armed at build, but the follower seats itself at its first update and
                        // Activate re-seats it, so a member patrols from where it arrives
                        // rather than from this parking pose.
                        armIaPatrol(pilot);
                        // ⚠ The militia paints it and nothing more: the original spawns a wave
                        // member from the PLAIN AI def of its aircraft. An unnamed militia keeps
                        // its own skins, never the Fortune Hunters default.
                        var waveScheme = WaveMilitiaScheme(inputs, wave.EnemyName);
                        int rating = InstantActionRuntime.RepresentativeRating(
                            InstantActionRuntime.RandomPilotStats(Rng.Stream(Rng.Ai).Randi()));
                        var enemy = inputs.Spawn(waveNode, Vector3.Zero, Vector3.Forward,
                            pilot, waveScheme, InstantActionRuntime.EnemyTeam,
                            rating, inert: true,
                            shippedSkins: waveScheme == null, fit: null);
                        if (enemy == null)
                        {
                            continue;
                        }
                        if (pilot.Gunner != null)
                        {
                            pilot.Gunner.PrimaryTargetName = "player";
                        }
                        // The same authored actor volumes as the ace and wingmen
                        // (docs/formats/instant-action.md).
                        InstantActionRuntime.ApplyActorVolumes(pilot.Machine);
                        int accentId = InstantActionRuntime.ResolveWaveAccentId(
                            wave.EnemyAccentId, Rng.Stream(Rng.Ai).Randi());
                        inputs.RegisterVoice(enemy, accentId, rating);
                        roster.Add(enemy);
                    }
                }
            }
            rosters[w] = roster;
        }
        WaveRosters = rosters;
        WaveSpawnList = inputs.SpawnList;
        Waves = new InstantActionWaves(waveSizes);
        // On zeppelin_run the first wave is started by ArmZeppelinRun after the generator block,
        // since "activating" it there means crediting the zeppelin's generator, which does not
        // exist yet at this point in the build.
        if (!ia.IsZeppelinRun)
        {
            int firstWave = Waves.Start();
            if (firstWave != 0)
            {
                ActivateWave(firstWave);
            }
        }
        int iaWaveEnemies = rosters.Sum(r => r.Count);
        if (iaWaveEnemies > 0)
        {
            GD.Print($"ia: {iaWaveEnemies} wave enemies across " +
                      $"{rosters.Count(r => r.Count > 0)} wave(s), built inert, " +
                      $"team={InstantActionRuntime.EnemyTeam}");
            what += $" + {iaWaveEnemies} IA wave enemies";
        }
        return what;
    }

    /// <summary>The mission's zeppelin switch: every distinct authored zeppelin node written
    /// Visible = objective (the decoded gwNodeSetActive), the non-objectives held. Returns the
    /// switched nodes for the turret arm, which GameSession runs inside its own emplacement block
    /// (⚠ there: BEFORE --wake-turrets). ⚠ Visible is the WHOLE write, since world colliders
    /// derive Disabled from it, and each switched node must reach that turret arm or the
    /// objective zeppelin flies unarmed.</summary>
    internal List<(Node3D Node, bool Objective, string Name)> SwitchZeppelins(
        AnimRuntime world, ZeppelinRuntime? zeppelins, string chapter)
    {
        var switched = new List<(Node3D Node, bool Objective, string Name)>();
        string selectedZep = InstantActionRuntime.SelectedZeppelinNode(Runtime.Def);
        var switchedZeps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string zepName in InstantActionRuntime.ZeppelinNodes(Runtime.Def))
        {
            if (!switchedZeps.Add(zepName))
            {
                continue;   // all 8 shipped chapters name the same node three times
            }
            bool objective = Runtime.IsZeppelinRun
                && zepName.Equals(selectedZep, StringComparison.OrdinalIgnoreCase);
            if (world.FindNodes(zepName) is not { Count: > 0 } zepNodes)
            {
                GD.Print($"ia: zeppelin '{zepName}' is not in {chapter}'s world — " +
                          "nothing to switch");
                continue;
            }
            foreach (var zepNode in zepNodes)
            {
                zepNode.Visible = objective;   // colliders derive from this (WorldCollision)
                switched.Add((zepNode, objective, zepName));
            }
            if (!objective)
            {
                zeppelins?.Hold(zepName);
            }
            GD.Print($"ia: zeppelin '{zepName}' " + (objective
                ? "ACTIVATED as this mission's objective (zeppelin_run)"
                : "deactivated by the Instant Action builder"));
        }
        return switched;
    }

    /// <summary>The zeppelin run's wave arm: the objective zeppelin's generator releases the waves
    /// built inert by BuildActors on a budget the sequencer credits wave by wave. ⚠ The first wave
    /// can only start here, since activating it means crediting a generator that did not exist at
    /// BuildActors time. A no-op on every other mission type.</summary>
    internal void ArmZeppelinRun(AiGeneratorRuntime? generators)
    {
        if (!Runtime.IsZeppelinRun || Waves is not { } waves)
        {
            return;
        }
        _generators = generators;
        string objectiveZep = InstantActionRuntime.SelectedZeppelinNode(Runtime.Def);
        int claimed = generators?.UseInstantActionLaunches(
            objectiveZep, ReleaseWaveMember) ?? 0;
        if (claimed == 0)
        {
            // ⚠ Do not invent a fallback spawn path: the original burns through every wave the
            // same way, its counter advancing whether or not the top-up lands
            // (docs/formats/instant-action.md).
            GD.PushWarning($"ia: zeppelin '{objectiveZep}' carries no egen generator — no " +
                            "wave will ever launch on this zeppelin run");
        }
        else
        {
            GD.Print($"ia: zeppelin run: '{objectiveZep}' launches every wave " +
                      $"({claimed} generator(s) on the wave-credit budget). BL-350 is open " +
                      "and in this mission's way: a drop is not gated on the doors opening.");
        }
        int firstZepWave = waves.Start();
        if (firstZepWave != 0)
        {
            ActivateWave(firstZepWave);
        }
    }

    /// <summary>One sim step of the mission: the wave sequencer's tick, the mission clock and the
    /// wave-cleared win signal. ⚠ GameSession calls this from BOTH drive paths, like the match
    /// clock: a realtime session never enters DriveSimSteps, so a sequencer stepped only there
    /// advances no wave at the controls.</summary>
    internal void Step(float dt)
    {
        var ia = Runtime;
        ia.Advance(dt);
        if (Waves is not { Finished: false, CurrentWave: >= 1 } waves)
        {
            return;
        }
        var waveRoster = WaveRosters![waves.CurrentWave - 1];
        // ⚠ A wave member still waiting in the zeppelin's bay COUNTS as present, as the decoded walk
        // counts a still-deactivated enemy, or a credited wave reads as cleared in the frames
        // before its first launch (docs/formats/instant-action.md).
        int alive = ia.IsZeppelinRun
            ? waveRoster.Count(fc => !fc.Crashed)
            : waveRoster.Count(fc => fc.InPlay);
        int next = waves.Step(alive);
        if (next != 0)
        {
            ActivateWave(next);
        }
        else if (waves.Finished)
        {
            // Every configured wave cleared — the squadron mode's win. Reported on every mode;
            // the runtime drops it on the ones that do not run on it (a zeppelin run's waves all
            // clear too, and the zeppelin is what decides that mission).
            ia.ReportObjective(InstantActionObjective.WavesCleared);
        }
    }

    // Teleports and activates waveNumber's built (inert) roster: a spawn drawn against every live
    // human's CURRENT position, then the fan pattern off that point's heading. A missing spawn list
    // leaves the wave parked inert with a warning rather than guessing a position.
    // ⚠ On zeppelin_run the generator arm REPLACES all of that, never adds to it: the wave is not
    // moved and no spawn is drawn, the objective zeppelin's generator is credited instead.
    private void ActivateWave(int waveNumber)
    {
        var roster = WaveRosters![waveNumber - 1];
        if (roster.Count == 0)
        {
            return; // InstantActionWaves.Start/Step never hand back an empty wave; stay defensive
        }
        if (Runtime is { IsZeppelinRun: true } iaZepRun)
        {
            LaunchWave = waveNumber;   // the decoded group stamp (the generator's +0x64)
            string objectiveZep = InstantActionRuntime.SelectedZeppelinNode(iaZepRun.Def);
            int fed = _generators?.GrantWaveCapacity(objectiveZep, roster.Count) ?? 0;
            GD.Print($"ia: wave {waveNumber} ({roster.Count} aircraft) credited to '" +
                      $"{objectiveZep}' ({fed} generator(s)) — they launch from the bay, " +
                      "not teleported");
            return;
        }
        if (WaveSpawnList is not { Count: > 0 } spawns)
        {
            GD.PushWarning($"ia: no spawn points for wave {waveNumber} — {roster.Count} " +
                            "aircraft stay parked inert");
            return;
        }
        var humanPositions = new List<Vector3>();
        foreach (var rig in _rigs!)
        {
            if (rig.Controller is { } human)
            {
                humanPositions.Add(human.WorldPosition);
            }
        }
        uint draw = Rng.Stream(Rng.Spawn).Randi();
        var (spIndex, sp) = InstantActionWaves.ChooseWaveSpawn(spawns, humanPositions, draw);
        var fwd = new Basis(Vector3.Up, Mathf.DegToRad(sp.HeadingDeg)) * Vector3.Forward;
        for (int m = 0; m < roster.Count; m++)
        {
            var (metres, offsetDeg) = InstantActionWaves.FanOffset(m);
            var dir = fwd.Rotated(Vector3.Up, Mathf.DegToRad(offsetDeg));
            var pos = sp.Position + dir * metres;
            roster[m].Activate(pos, pos + fwd);
        }
        GD.Print($"ia: wave {waveNumber} ({roster.Count} aircraft) activated at spawn #{spIndex} " +
                  $"of {spawns.Count}");
    }

    // The launch hook handed to the objective zeppelin's generator: releases the next still-parked
    // member of the CURRENT wave at the generator's own drop point and attitude. ⚠ Do not re-derive
    // that point here. Null once the wave has nothing parked left, which the generator accounts as
    // a failed spawn.
    private FlightController? ReleaseWaveMember(Vector3 pos, Vector3 lookAt,
        Vector3 launchVelocity)
    {
        if (WaveRosters is not { } rosters || LaunchWave is < 1 or > 4)
        {
            return null;
        }
        foreach (var member in rosters[LaunchWave - 1])
        {
            if (!member.Inert)
            {
                continue;
            }
            member.Activate(pos, lookAt, launchVelocity, carrierDrop: true);
            return member;
        }
        return null;
    }

    // The livery a wave flies in: its militia's pattern, in that pattern's shipped colours. The
    // original paints a wave member from the setup screen rather than from a vehicle def, so this
    // holds for a pair the install ships no def for (Sacred Trust's Warhawk) as much as for one it
    // does. Null when the militia is not named or names no pattern, and the member keeps its skins.
    private PaintScheme? WaveMilitiaScheme(ActorBuildInputs inputs, string enemyName)
    {
        if (MilitiaPaint.PatternForWave(MilitiaPatterns(inputs), enemyName) is not { } pattern)
            return null;
        foreach (var scheme in inputs.LiveryResolver.PaintCatalog(inputs.ZrdrPath))
            if (string.Equals(scheme.Pattern, pattern, StringComparison.OrdinalIgnoreCase))
                return scheme;
        return null;
    }

    private IReadOnlyDictionary<string, string> MilitiaPatterns(ActorBuildInputs inputs)
    {
        if (_militiaPatterns != null)
            return _militiaPatterns;
        try
        {
            _militiaPatterns = MilitiaPaint.PatternByMilitia(inputs.ZrdrPath,
                Messages.Load(inputs.MessagesPath));
        }
        catch (Exception e)
        {
            GD.PushWarning($"ia: cannot read the militia paint patterns: {e.Message}");
            _militiaPatterns = new Dictionary<string, string>();
        }
        return _militiaPatterns;
    }

    /// <summary>What BuildActors reads from the session's build, in the shape of
    /// HumanFlightAdapter.Inputs: stable references plus the two delegates GameSession keeps
    /// private behaviour behind (the spawner and the voice registration).</summary>
    internal sealed class ActorBuildInputs
    {
        public List<PlayerRig> Rigs = null!;
        public string Chapter = "";
        public string Mission = "";
        public string ChapterZrdrPath = "";
        public string MissionZrdrPath = "";
        public string ZrdrPath = "";
        public string MessagesPath = "";
        public List<SpawnPoint>? SpawnList;
        public int SpawnBase;
        public LiveryResolver LiveryResolver = null!;
        public NetTrailerTargets NetTrailers = null!;
        public SpawnAuthoredAircraft Spawn = null!;
        public RegisterAiVoice RegisterVoice = null!;
    }
}
