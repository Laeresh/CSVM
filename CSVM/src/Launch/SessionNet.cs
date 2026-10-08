using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Session.Campaign;
using CSVM.Session.Objectives;
using CSVM.Session.Roster;
using CSVM.Session.World;
using CSVM.UI.Boards;
using CSVM.Utils;
using Godot;

namespace CSVM.Launch;

/// <summary>A session's end of the wire and everything wired onto it. It holds the join, the seat
/// list, the aircraft-state relay, combat, chat, the shared clock and the start gate. It wires the
/// director, world, positional-start and cutscene links. The session keeps the tick order.
/// Each wire step takes what it binds as arguments, and does nothing while <see cref="Link"/> is
/// null outside a network match. <see cref="WireLiveRespawn"/> alone also pins a local match.
/// Module entry: docs/architecture/Launch.md on src/Launch/SessionNet.cs.</summary>
internal sealed class SessionNet
{
    /// <summary>The owner the in-flight chat raises the on-screen keyboard under.</summary>
    internal const string ChatKeyboardOwner = "chat";

    // The chat's one line, the only field that owner holds.
    private const string ChatKeyboardField = "line";

    // How many transport steps a guest gives the host's answer before it gives up and fails the
    // build. Ten seconds of simulated link at the fixed step. ⚠ This advances the transport's own
    // clock, not the wall clock. It bounds simulated delivery time and is not a timeout; a socket
    // carrier gets the wall-clock wait below as well.
    private const int NetJoinSteps = 600;

    // The real wait a socket carrier is given, in wall seconds. A command-line guest links the
    // moment the host does, before the host's session exists to send the handshake. The steps
    // above then pass in microseconds with nothing on the wire yet.
    private const double NetJoinWallSeconds = 10.0;

    // The panes, and one rig per seat (the panes first, then one pane-less rig per guest). Both
    // are the session's lists, filled by its rig build and by BuildSeatRigs.
    private readonly List<PlayerRig> _rigs;
    private readonly List<PlayerRig> _seatRigs;
    private readonly Func<double> _clockTime;
    // Whether the wire delivers on real seconds (a socket) rather than when it is stepped.
    private readonly bool _realCarrier;
    // When the seats flown here go on the wire, and what sequence each sample carries.
    private readonly Net.AircraftStateCadence _stateCadence = new();
    // The wire index of every weapon by its id, and the per-seat counter the fire events carry.
    private readonly Dictionary<string, int> _weaponWire = new(StringComparer.Ordinal);
    private readonly ushort[] _fireSequence = new ushort[Net.NetSeats.SeatCapacity];
    // The seats whose guest left the session mid-mission, each out of play for the rest of it.
    private readonly HashSet<int> _seatsLeft = new();
    private readonly List<ChatPanel> _chatPanels = new();
    // The shaping this end's link runs under, as the trace's first line names it, or null unshaped.
    private readonly Net.LoopbackConditions? _shape;

    // Simulation steps traced so far, the trace's own clock, since a step is a fixed slice of
    // time. The wall stamp of a step run in a catch-up burst says when it ran, not what it covers.
    private long _tracedSteps;

    // The weapon catalogue a received fire or hit event is read against. Its file order IS the
    // wire index, so both ends resolve the same round from one byte and no name crosses.
    private WeaponDefs? _weaponDefs;
    private ProjectilePool? _projectiles;
    private VersusDirector? _dogfight;
    private AiVoiceRuntime? _voice;
    // Whether this machine's world is built, so a guest answers the host's hold word only then.
    private bool _startBuilt;
    // Whether a ledger whose zone count differs from its copy's has been logged, once a session.
    private bool _zoneMismatchLogged;

    /// <summary>Opens the wire the launch context names, or none. It opens at construction rather
    /// than at the build. A host must answer a join before its own world stands. <paramref name="clockTime"/> reads the session clock, zero before it exists.
    /// </summary>
    public SessionNet(LauncherContext ctx, ulong masterSeed, List<PlayerRig> rigs,
        List<PlayerRig> seatRigs, Func<double> clockTime)
    {
        _rigs = rigs;
        _seatRigs = seatRigs;
        _clockTime = clockTime;
        _realCarrier = ctx.NetTransport is Net.INetLink;
        _shape = (ctx.NetTransport as Net.ShapedTransport)?.Conditions;
        // Sorted once here, so a seat's position in this list IS its seat index. Everything
        // downstream then reads the roster with the index it reads the rigs with.
        Seats = ctx.NetSeats is { Count: > 0 } seats
            ? seats.OrderBy(s => s.SeatIndex).ToArray()
            : Array.Empty<Net.NetSeat>();
        SeatFit = ctx.NetSeatFit;
        SeatBuild = ctx.NetSeatBuild;
        TeamNames = ctx.NetTeamNames;
        // Every guest binds the wingman from its host's word, and one with no word says so loudly.
        CoopWingman = ctx.NetTransport != null && !ctx.NetHost ? ctx.NetCoopWingman ?? (() => null) : null;
        Clock = ctx.NetHandshake is { } handshake
            ? new Net.NetClockSlew(handshake.HostClock)
            : null;
        Link = ctx.NetTransport is { } transport
            ? ctx.NetHost
                ? Net.NetSession.Host(transport, Seats, masterSeed, clockTime, ctx.NetAirframes)
                : Net.NetSession.Guest(transport, ctx.NetAirframes)
            : null;
    }

    /// <summary>This session's end of the wire, null outside a network match. A suite reads its
    /// counters and its roster; a replication feature registers its handlers on it.</summary>
    public Net.NetSession? Link { get; }

    /// <summary>The whole match's seat roster in seat order. A local match with bots holds one
    /// with no <see cref="Link"/>; any other session without a wire holds none. A guest's arrives
    /// over the wire, between construction and the build.</summary>
    public IReadOnlyList<Net.NetSeat> Seats { get; private set; }

    /// <summary>Each seat's loadout and custom plane, from the lobby.</summary>
    public Func<int, LoadoutChoice?>? SeatFit { get; }

    /// <summary>Each seat's custom plane build, from the lobby.</summary>
    public Func<int, Flight.Hangar.CustomPlaneDef?>? SeatBuild { get; }

    /// <summary>A guest's word of the host's co-op wingman, null on a host and offline.</summary>
    public Func<Net.CoopWingmanMessage?>? CoopWingman { get; }

    /// <summary>A team Dogfight's team names by lobby team number, as this machine's lobby held
    /// them.</summary>
    public IReadOnlyDictionary<int, string>? TeamNames { get; }

    /// <summary>The host's master seed once a guest has joined, null before and on a host.</summary>
    public ulong? JoinedSeed { get; private set; }

    /// <summary>This guest's offset onto host time, null on a host and outside a network match.
    /// </summary>
    public Net.NetClockSlew? Clock { get; private set; }

    /// <summary>This end of the shared clock's round trip, null outside a network session.</summary>
    public Net.NetClockPing? Ping { get; private set; }

    /// <summary>The start barrier, null outside a network match. While it holds, the clock is
    /// start-held and only the wire is stepped.</summary>
    public Net.NetStartGate? StartGate { get; private set; }

    /// <summary>AI aircraft and world pools over the wire, null outside a network match.</summary>
    public NetWorldLink? World { get; private set; }

    /// <summary>The landing rows, ladder and range gates over the wire, null outside one.</summary>
    public NetPositionalStartLink? Starts { get; private set; }

    /// <summary>The in-flight chat over the wire, null outside a network match.</summary>
    public NetChatLink? Chat { get; private set; }

    /// <summary>The stunt race over the wire, null outside a network race.</summary>
    public NetRaceLink? Race { get; private set; }

    /// <summary>The chat panel each local pane draws, in pane order.</summary>
    public IReadOnlyList<ChatPanel> ChatPanels => _chatPanels;

    /// <summary>How many times a seat flown elsewhere read full again after being hurt, each a
    /// restore on the seat's own machine. For a suite to read.</summary>
    public int RepairsTaken { get; private set; }

    /// <summary>How many damage ledgers this machine mirrored into a seat flown elsewhere. For a
    /// suite to read.</summary>
    public int DamageTaken { get; private set; }

    /// <summary>Whether <see cref="TraceStep"/> writes, set by <c>--debug-net-trace</c>.</summary>
    public bool TraceSteps { get; init; }

    // Where no seat respawns in flight: a Dogfight match, local or on a wire, and every other
    // network session but a stunt race. ⚠ Read after WireCombat, which hands the match over.
    private bool PinsLiveRespawn => Race == null && (_dogfight != null || (Link != null && Seats.Count > 0));

    /// <summary>Whether a guest's seat left the mission and is out of play.</summary>
    public bool HasLeft(int seat) => _seatsLeft.Contains(seat);

    /// <summary>Phase two of a guest's start: pumps the transport until the host's handshake and
    /// roster arrive, before the build sizes anything by the field. True on a host, offline, or
    /// once joined; false when the host never answered.</summary>
    public bool AwaitJoin()
    {
        if (Link is not { IsHost: false } net)
        {
            return true;
        }

        // A host built first sends its hold at once, so it can land inside this pump.
        // ⚠ Claim the start words before pumping, or that hold is dropped as unknown.
        net.On<Net.StartGateMessage>(TakeStartWord);
        var waited = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; !net.Joined && (i < NetJoinSteps
                 || (_realCarrier && waited.Elapsed.TotalSeconds < NetJoinWallSeconds)); i++)
        {
            net.Step(GameClock.FixedDt);
            if (_realCarrier && !net.Joined)
            {
                System.Threading.Thread.Sleep(1);
            }
        }

        if (!net.Joined)
        {
            Log.Error("core", $"net: no handshake or roster from the host in {NetJoinSteps} transport steps, the join failed");
            return false;
        }

        JoinedSeed = net.Handshake.Seed;
        Seats = net.Seats.OrderBy(s => s.SeatIndex).ToArray();
        Clock = new Net.NetClockSlew(net.Handshake.HostClock);
        Log.Info("core", $"net: joined as seat {net.LocalSeat} of {Seats.Count}, master seed {JoinedSeed}");
        return true;
    }

    /// <summary>Fills the seat list from the panes: each pane at its own seat, plus one pane-less
    /// rig per seat flown elsewhere, parented under <paramref name="worldRoot"/>. Outside a match
    /// it is the pane list itself. Then claims the aircraft-state samples, once.</summary>
    public void BuildSeatRigs(Node3D worldRoot)
    {
        FillSeatRigs(worldRoot);
        // Once, at the build, as soon as the seats exist. A sample that lands before its
        // aeroplane is assembled reaches a seat with no buffer and is dropped there.
        Link?.On<Net.AircraftStateMessage>((_, sample) => TakeAircraftState(sample));
        // The star's first leg, armed with the handler. A guest is linked to the host alone, so
        // its samples reach the other guests only by being forwarded here, and only for its seats.
        if (Link is { IsHost: true } relayHost)
        {
            relayHost.RequireSeatOwner<Net.AircraftStateMessage>(sample => sample.Seat);
            relayHost.RelayToOthers<Net.AircraftStateMessage>();
        }
    }

    /// <summary>One transport step, then the clock's round trip. The session calls this before
    /// each simulation step, so a payload is applied on the step after it arrived.</summary>
    public void Step(double delta)
    {
        SendChangedDamage();
        Link?.Step(delta);
        Ping?.Step();
    }

    /// <summary>The wall-time half: a guest's offset walk onto host time and the chat's line
    /// timers, once per rendered frame.</summary>
    public void Advance(double delta)
    {
        Clock?.Advance(delta);
        Chat?.Chat.Advance((float)delta);
        ScreenKeyboard.Follow(ChatKeyboardOwner, Chat is { Chat.Typing: true } ? ChatKeyboardField : null);
    }

    /// <summary>The shared clock's round trip, for every kind of session: a guest asks the host's
    /// clock from its first step on, and the host answers. ⚠ Nothing is sent from here: the join
    /// stays the two payloads it is counted as.</summary>
    public void WireClock()
    {
        if (Link is not { } net || Seats.Count == 0)
        {
            return;
        }

        if (net.IsHost)
        {
            Ping = Net.NetClockPing.Answer(net, _clockTime);
        }
        else if (Clock is { } slew)
        {
            Ping = Net.NetClockPing.Follow(net, slew, _clockTime);
        }
    }

    /// <summary>The start barrier, armed before the build so no word is dropped as unknown. A host
    /// waits on every machine flying a seat that is still linked; a guest waits on its host.
    /// </summary>
    public void WireStartGate()
    {
        if (Link is not { } net || Seats.Count == 0)
        {
            return;
        }

        var linked = net.Peers;
        StartGate = net.IsHost
            ? Net.NetStartGate.Host(Seats.Where(s => !s.FlownHere && linked.Contains(s.PeerId))
                .Select(s => s.PeerId).Distinct())
            : StartGate ?? Net.NetStartGate.Guest(net.HostPeer);
        net.On<Net.StartGateMessage>(TakeStartWord);
        net.PeerLeft += peer => StartGate?.TakeLeft(peer);
    }

    /// <summary>The last act of the build. Holding here, rather than in the launcher, halts the
    /// mission clock, the AI and the world events along with the aeroplanes.</summary>
    public void HoldStart(GameClock? clock)
    {
        if (StartGate is not { } gate || clock == null || Link is not { } net)
        {
            return;
        }

        _startBuilt = true;
        clock.StartHeld = !gate.Open;
        if (net.IsHost)
        {
            foreach (int peer in gate.Waiting)
            {
                SendHold(peer);
            }
        }
        else if (!gate.Open)
        {
            SendLoaded();
        }

        Log.Info("core", $"net start: {(gate.Open ? $"nobody to wait for ({gate.Release})" : net.IsHost ? $"holding for {gate.Waiting.Count} machine(s) to load, round {gate.Round}" : "loaded, holding for the host's start")}");
    }

    /// <summary>One physics tick of a held start: the wire only. True when the barrier opened on
    /// this tick, so the caller runs the tick's simulation step too.</summary>
    public bool StepStartHold(GameClock clock, double delta)
    {
        RenderPoses.Restore();
        Step(delta);
        if (StartGate is not { } gate)
        {
            clock.StartHeld = false;
            return true;
        }

        gate.Step(delta);
        if (!gate.Open)
        {
            return false;
        }

        clock.StartHeld = false;
        if (Link is { IsHost: true } net)
        {
            net.Broadcast(new Net.StartGateMessage(Net.NetStartWord.Start, gate.Round), Net.NetChannels.Events);
        }

        Log.Info("core", $"net start: released ({gate.Release}) after {gate.WaitedSeconds:0.00} s");
        return true;
    }

    /// <summary>Combat over the wire, wired once the seats, the pool and the catalogue stand. It
    /// runs after the match, so a death report has a scorer to reach. An owner reports what its
    /// aeroplane fires; the shooter decides its rounds' hits for the victim's owner to apply
    /// (docs/architecture/Net.md's hit authority and star topology).</summary>
    public void WireCombat(WeaponDefs weaponDefs, ProjectilePool projectiles, VersusDirector? dogfight)
    {
        _projectiles = projectiles;
        _dogfight = dogfight;
        if (Link is not { } net || Seats.Count == 0)
        {
            return;
        }

        _weaponDefs = weaponDefs;
        for (int i = 0; i < weaponDefs.All.Count; i++)
        {
            _weaponWire[weaponDefs.All[i].Id] = i;
        }

        net.On<Net.FireMessage>((_, fire) => TakeFire(fire));
        net.On<Net.HitMessage>((_, hit) => TakeHit(hit));
        net.On<Net.DamageMessage>((_, damage) => TakeDamage(damage));
        net.On<Net.DeathMessage>((_, death) => TakeDeath(death));
        if (net.IsHost)
        {
            // Each report speaks for the seat its sender flies: an owner its own aeroplane's fire,
            // ledger and death, a shooter its own round's hit. Anything else is forged.
            net.RequireSeatOwner<Net.FireMessage>(fire => fire.Seat);
            net.RequireSeatOwner<Net.DamageMessage>(damage => damage.Seat);
            net.RequireSeatOwner<Net.DeathMessage>(death => death.VictimSeat);
            net.RequireSeatOwner<Net.HitMessage>(hit => hit.ShooterSeat);
            // A hit is addressed to one machine, everything else is news for the whole field.
            // The score needs no leg at all: the host is the only one that writes it.
            net.RelayToOthers<Net.FireMessage>();
            net.RelayToOthers<Net.DamageMessage>();
            net.RelayToOthers<Net.DeathMessage>();
            net.RelayToSeatOwner<Net.HitMessage>(hit => hit.VictimSeat);
        }
        else
        {
            // The host's decisions, taken from it alone. A host never handles one, since a guest's
            // would write the scoreboard it alone keeps.
            net.On<Net.ScoreMessage>((_, score) => _dogfight?.TakeScore(score));
            net.On<Net.DeathNoticeMessage>((_, notice) => _dogfight?.TakeDeathNotice(notice));
        }

        for (int i = 0; i < _seatRigs.Count && i < Seats.Count; i++)
        {
            WireSeatCombat(i);
        }

        Log.Info("core", $"net combat: {_seatRigs.Count} seats, {(net.IsHost ? "host (relaying fire, damage, death and every hit to its owner)" : "guest (talking to the host alone)")}");
    }

    /// <summary>The in-flight chat, in every network mode: one chat per machine, drawn in each
    /// local pane, typed into from the seat that reads the keyboard. Runs once every local seat
    /// has its aeroplane to take the keys from.</summary>
    public void WireChat(Messages? strings)
    {
        if (Link is not { } net || Seats.Count == 0)
        {
            return;
        }

        Chat = NetChatLink.Open(net, strings);
        foreach (var pane in _rigs)
        {
            var panel = ChatPanel.ForPane(Chat.Chat, pane);
            var layer = new CanvasLayer { Name = "chat", Layer = HudLayers.Hud };
            layer.AddChild(panel);
            pane.HudParent.AddChild(layer);
            _chatPanels.Add(panel);
            WireSeatChat(pane.Index);
        }

        Log.Info("core", $"net chat: {_rigs.Count} pane(s), {(net.IsHost ? "host (relaying an all-chat to every machine and a team line to the typist's team)" : "guest (sending its lines to the host)")}");
    }

    /// <summary>The respawn control on a living aeroplane, pinned off on every seat. That holds in
    /// a Dogfight match, split screen or wired, and in every other network session but a stunt
    /// race. A pilot there flies again only from a crash, asked of the host where a wired match
    /// grants returns. The one wire step that also acts with no link, since a local match counts
    /// returns as a wired one does. Runs once every seat has its aeroplane.</summary>
    public void WireLiveRespawn()
    {
        if (!PinsLiveRespawn)
        {
            return;
        }

        for (int i = 0; i < _seatRigs.Count; i++)
        {
            PinLiveRespawn(i);
        }

        string where = Link is { } net ? (net.IsHost ? "net host" : "net guest") : "local match";
        Log.Info("core", $"live respawn: {where}, {_seatRigs.Count} seat(s), the respawn control returns a pilot from a crash only, never in flight");
    }

    /// <summary>The stunt race over the wire, before the roster builds, since each local seat's run
    /// is fed through it. Each machine times its own seats; the host keeps the window, the board
    /// and the ending, and a guest's race replicates it. ⚠ Nothing is sent from here: the join stays
    /// the two payloads it is counted as.</summary>
    public NetRaceLink? WireRace(StuntRace race)
    {
        if (Link is not { } net || Seats.Count == 0)
        {
            return null;
        }

        Race = NetRaceLink.Open(net, race, _clockTime, Clock, Ping, GameClock.FixedDt);
        // A guest walking back to the lobby keeps its link, so its word is the only sign it left.
        Race.GuestLeft += TakeGuestLeft;
        Log.Info("core", $"net race: {(net.IsHost ? "host (timing its own seats, taking every guest's run reports, sending each changed racer's line and its clock)" : "guest (reporting its own seats' runs, its board and window replicated from the host's)")}");
        return Race;
    }

    /// <summary>An airframe swap's replacement on the wire again, its combat (unless
    /// <paramref name="skipCombat"/>, a suite's control) and its chat keys.</summary>
    public void RewireSeat(PlayerRig owner, bool skipCombat)
    {
        if (Link != null && _weaponDefs != null && !skipCombat)
        {
            WireSeatCombat(_seatRigs.IndexOf(owner));
        }

        WireSeatChat(_seatRigs.IndexOf(owner));
        if (PinsLiveRespawn)
        {
            PinLiveRespawn(_seatRigs.IndexOf(owner));
        }
    }

    /// <summary>The campaign's objectives over the wire, once the graph is armed. The host's graph
    /// runs the mission and says what it did; a guest's replays that and decides nothing.
    /// ⚠ Nothing is sent from here: the join stays the two payloads it is counted as.</summary>
    public void WireDirector(CampaignDirector? campaign, AnimRuntime? world)
    {
        if (Link is not { } net || Seats.Count == 0 || campaign?.Graph is not { } graph)
        {
            return;
        }

        if (net.IsHost)
        {
            NetDirectorLink.Publish(net, graph, _clockTime);
        }
        else
        {
            NetDirectorLink.Follow(net, graph, new NetDirectorCatchUp(
                () => Clock?.HostTime(_clockTime()) ?? 0.0, world, world?.Sounds));
        }

        Log.Info("core", $"net director: {(net.IsHost ? $"host (every transition of {graph.Count} objective(s), and the ending, as they happen)" : $"guest (replaying the host's transitions over {graph.Count} objective(s), evaluating none of its own)")}");
    }

    /// <summary>The host-owned world over the wire, once the pools and the combat catalogue stand.
    /// AI aircraft are admitted step by step from the roster, since waves and generators add them
    /// long after this runs.</summary>
    public void WireWorld(AnimRuntime? world, WorldLinkInputs inputs)
    {
        if (Link is not { } net || Seats.Count == 0)
        {
            return;
        }

        _voice = inputs.Voice;
        World = new NetWorldLink(net, new NetWorldSeats
        {
            SeatOfShooter = SeatOfShooter,
            IsLocal = seat => seat >= 0 && seat < Seats.Count && Seats[seat].FlownHere,
            ShooterOfSeat = seat => seat >= 0 && seat < _seatRigs.Count ? _seatRigs[seat].Controller?.PlayerIndex : null,
            WeaponIndex = weapon => _weaponWire.TryGetValue(weapon.Id, out int index) ? index : -1,
            WeaponAt = index => _weaponDefs is { } defs && index >= 0 && index < defs.All.Count ? defs.All[index] : null,
            Projectiles = _projectiles,
            SeatLeft = TakeSeatLeft,
        }, world);
        if (net.IsHost)
        {
            net.PeerLeft += OnPeerLeft;
        }

        if (inputs.Zeppelins != null)
        {
            World.FollowZeppelins(inputs.Zeppelins);
        }

        World.FollowVehicles(inputs.SurfaceVehicles, inputs.Campaign);
        if (inputs.Generators != null)
        {
            World.FollowGenerators(inputs.Generators, inputs.AiPlanes);
        }

        if (inputs.Voice != null)
        {
            World.FollowVoice(inputs.Voice);
        }

        Log.Info("core", $"net world: {(net.IsHost ? $"host (flying every AI and deciding every world hit, {world?.Destructibles.Count ?? 0} pool(s))" : "guest (AI replicated from the host, world pools spending nothing of their own)")}");
    }

    /// <summary>The landing rows, the ladder switch and the mission-code range gates over the
    /// wire. The host decides them off every seat, and a guest replays those decisions and
    /// reports its own auto-land button.</summary>
    public void WirePositionalStarts(LandingApproachRuntime? landings, LadderSwitchRuntime? ladder,
        AnimRuntime? world)
    {
        if (Link is not { } net || Seats.Count == 0 || (landings == null && ladder == null && world == null))
        {
            return;
        }

        Starts = NetPositionalStartLink.Open(net, () => _seatRigs, landings, ladder, world);
        Log.Info("core", $"net positional starts: {(net.IsHost ? $"host (landing rows, the ladder and mission-code range gates decided over {_seatRigs.Count} seats)" : "guest (replaying the host's row starts, holder and range gates, reporting its own auto-land button)")}");
    }

    /// <summary>The cutscene skip over the wire: any player's skip ends the shared episode on
    /// every machine, with the host deciding. Offline and splitscreen sessions never open it.
    /// </summary>
    public void WireCutscenes(CutsceneController? cutscene)
    {
        if (Link is not { } net || Seats.Count == 0 || cutscene == null)
        {
            return;
        }

        NetCutsceneLink.Open(net, cutscene, pane => pane < _rigs.Count ? _rigs[pane].Index : pane);
        Log.Info("core", $"net cutscenes: {(net.IsHost ? "host (a skip by any seat ends the episode, announced to every guest)" : "guest (a skip asks the host, the episode ends on its word)")}");
    }

    /// <summary>The own-aeroplane half of replication, run at the end of the human-aircraft phase
    /// so a sample is this step's settled pose. It is the SIM pose, never the render pose. A seat
    /// flown elsewhere sends nothing from here; its samples arrive.</summary>
    public void BroadcastAircraftState()
    {
        if (Link is not { } net || Seats.Count == 0 || !_stateCadence.StepSends())
        {
            return;
        }

        for (int i = 0; i < Seats.Count && i < _seatRigs.Count; i++)
        {
            if (!Seats[i].FlownHere || _seatRigs[i].Controller is not { } flown)
            {
                continue;
            }

            int seat = Seats[i].SeatIndex;
            var stick = flown.LastCommand;
            // On the seat's own channel. A relayed sample carries the host's peer id, so two
            // guests sharing one channel would discard each other by sequence number.
            net.Broadcast(
                new Net.AircraftStateMessage(
                    (byte)seat, _stateCadence.Next(seat), flown.WorldPosition,
                    flown.Attitude.GetRotationQuaternion(), flown.WorldVelocity, flown.Throttle,
                    stick.Roll, stick.Pitch, stick.Yaw, flown.Nitro.Boosting),
                Net.NetChannels.ForSeat(seat));
        }
    }

    /// <summary>One <c>--debug-net-trace</c> line per simulation step. It holds the step count,
    /// the wall, session and match clocks, a guest's offset and every seat's position. The wall
    /// clock is the one axis two machines' traces share. Laid over each other, two logs give the
    /// error between an owner's path and another machine's copy. A line naming this end's link
    /// shaping, or none, comes first.</summary>
    public void TraceStep()
    {
        if (!TraceSteps || Link is not { } net || Seats.Count == 0)
        {
            return;
        }

        var line = new System.Text.StringBuilder(160);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        // Once, ahead of the steps, so a reader of the log knows which link cell it was flown in.
        if (_tracedSteps == 0)
        {
            string shape = _shape is { } c
                ? string.Create(inv, $"latency {c.Latency:0.0000} jitter {c.Jitter:0.0000} loss {c.Loss:0.0000}")
                : "none";
            Log.Debug("core", $"net trace {(net.IsHost ? "host" : "guest")} shape {shape}");
        }

        double wall = (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
        double remain = _dogfight?.Match is { } match ? match.TimeRemaining : -1.0;
        line.Append(inv, $"net trace {(net.IsHost ? "host" : "guest")} step {_tracedSteps++} wall {wall:0.000000} sim {_clockTime():0.0000} remain {remain:0.000}");
        if (Clock is { } clock)
        {
            line.Append(inv, $" offset {clock.Offset:0.000000} target {clock.Target:0.000000} snaps {clock.Snaps} rtt {clock.RoundTrip:0.0000} trips {clock.RoundTrips} asked {Ping?.Asked ?? 0}");
        }

        for (int i = 0; i < Seats.Count && i < _seatRigs.Count; i++)
        {
            if (_seatRigs[i].Controller is { } plane)
            {
                var p = plane.WorldPosition;
                line.Append(inv, $" | seat {i} {(Seats[i].FlownHere ? "own" : "copy")} {p.X:0.000} {p.Y:0.000} {p.Z:0.000}");
            }
        }

        Log.Debug("core", $"{line}");
    }

    /// <summary>A guest this host flies with walked out of the mission while its link stays up,
    /// as its pause sheet's exit does. Its seats leave exactly as a dropped link's would. A peer
    /// that already left is a no-op, so the caller may repeat this every step.</summary>
    public void TakeGuestLeft(int peer)
    {
        if (Link is { IsHost: true })
        {
            OnPeerLeft(peer);
        }
    }

    /// <summary>A seat flown here died, in the order docs/org/multiplayer-scoring.md decodes: the
    /// dying pilot's own machine reports it, and the host scores it. A seat flown elsewhere
    /// reaches this through its wreck playing out locally, and reports nothing.</summary>
    public void ReportDeath(int seat, int? killer)
    {
        if (Link is not { } net || seat < 0 || seat >= Seats.Count || !Seats[seat].FlownHere)
        {
            return;
        }

        int killerSeat = killer is int shooter ? SeatOfShooter(shooter) : -1;
        int hull = killer is int fired ? ZeppelinVersus.HullOfShooter(fired) : -1;
        // Cause 2 is every death with no seat to charge, a roster AI's kill included; a bot is a
        // seat and is charged. The decode credits no last damager, so the pilot pays. Cause 3 is
        // a hull's broadside round, named by the hull's placement index.
        var death = new Net.DeathMessage(
            (byte)seat, killerSeat >= 0 ? (byte)killerSeat : Net.NetMessage.NoSeat,
            killerSeat >= 0 ? Net.NetDeathCause.Killer
            : hull >= 0 ? Net.NetDeathCause.ZeppelinPart
            : Net.NetDeathCause.Suicide,
            hull >= 0 ? (uint)hull : 0u);
        if (!net.IsHost)
        {
            net.Send(net.HostPeer, death, Net.NetChannels.Events);
            return;
        }

        net.Broadcast(death, Net.NetChannels.Events);
        _dogfight?.ScoreDeath(death);
    }

    /// <summary>A shooter id back to the seat that fired it, or -1 for a round no seat owns. Read
    /// off the rigs rather than assumed equal to the seat index, since only the roster decides
    /// that.</summary>
    public int SeatOfShooter(int shooter)
    {
        if (shooter == ProjectilePool.NoShooter)
        {
            return -1;
        }

        for (int i = 0; i < _seatRigs.Count; i++)
        {
            if (_seatRigs[i].Controller is { } rig && rig.PlayerIndex == shooter)
            {
                return i;
            }
        }

        return -1;
    }

    // A ledger as the fractions the damage report carries. A pool with no maximum reads as full.
    private static Net.DamagePools PoolsOf(PlaneDamage damage)
    {
        var pools = new Net.DamagePools(
            (byte)Math.Min(damage.Zones.Count, byte.MaxValue),
            Net.DamagePools.Word(Share(damage.WholeArmor, damage.WholeArmorMax)),
            Net.DamagePools.Word(Share(damage.WholeHealth, damage.WholeHealthMax)),
            0UL, 0UL);
        for (int i = 0; i < damage.Zones.Count; i++)
        {
            var zone = damage.Zones[i];
            pools = pools.WithZone(i, Share(zone.Armor, zone.Def.MaxArmor), Share(zone.Hp, zone.Def.MaxHp));
        }

        return pools;
    }

    private static float Share(float current, float max) => max > 0f ? current / max : 1f;

    // The seat list the roster, the spawn walk and the versus board are sized by. A pane-less rig
    // carries no camera and parents nothing into a pane, which is what makes HumanFlightAdapter
    // skip every view, device and listener for it. A bot seat flown here takes one too.
    private void FillSeatRigs(Node3D worldRoot)
    {
        _seatRigs.Clear();
        if (Seats.Count == 0)
        {
            _seatRigs.AddRange(_rigs);
            return;
        }

        Net.NetSeats.Validate(Seats);
        int locals = 0;
        foreach (var seat in Seats.OrderBy(s => s.SeatIndex))
        {
            // ⚠ A pane takes its SEAT's index, not its pane position. Seat index is the identity
            // the whole field agrees on, and a guest's own pane is rarely seat 0. Leaving the pane
            // number here would mark the wrong opponent and key the wrong score row.
            var rig = seat.HasPane && locals < _rigs.Count
                ? _rigs[locals++]
                : new PlayerRig { Camera = null!, HudParent = worldRoot, VisualLayer = 0 };
            rig.Index = seat.SeatIndex;
            _seatRigs.Add(rig);
        }

        if (locals < _rigs.Count)
        {
            Log.Warn("flight", $"net seats: {_rigs.Count} panes built for {locals} local seats; the extra panes fly nothing");
        }
        Log.Info("flight", $"net seats: {_seatRigs.Count} in the match, {locals} with a pane here");
    }

    // One received sample, handed to the seat it describes. ⚠ Only a seat with a buffer takes
    // one, which is exactly a seat flown elsewhere. An aeroplane flown here has its pose written
    // by its own simulation, and no arrival may overrule that. The buffer itself drops a sequence
    // at or below the newest it holds, so a reordered delivery needs no test here.
    private void TakeAircraftState(in Net.AircraftStateMessage sample)
    {
        if (sample.Seat >= _seatRigs.Count)
        {
            return;
        }

        _seatRigs[sample.Seat].Controller?.RemotePoses?.Receive(sample);
    }

    // One seat's aeroplane on the wire. Every copy routes the hits it takes, and the seat's own
    // machine reports what it fires, what it suffers and its death. Per controller, so an airframe
    // swap's replacement is wired here again or its reports stop crossing.
    private void WireSeatCombat(int seat)
    {
        if (seat < 0 || seat >= _seatRigs.Count || seat >= Seats.Count
            || _seatRigs[seat].Controller is not { } rig)
        {
            return;
        }

        rig.HitRouter = hit => RouteHit(seat, hit);
        if (!Seats[seat].FlownHere)
        {
            return;
        }

        rig.WeaponFired += (weapon, origin, direction) => SendFire(seat, weapon, origin, direction);
        // A match reports from its own Downed handler, which also keeps the last killer. Any
        // other mission reports here. A campaign's human field on the host counts a guest
        // down only when this report plays the wreck there.
        if (_dogfight == null)
        {
            rig.Downed += (_, killer) => ReportDeath(seat, killer);
        }
    }

    // ⚠ Off only, never back on, so a director's own pin stands. An in-flight respawn is a free
    // repair, restock and refuel the match never counts, and on a wire one the host never grants.
    private void PinLiveRespawn(int seat)
    {
        if (seat >= 0 && seat < _seatRigs.Count && _seatRigs[seat].Controller is { } pilot)
        {
            pilot.AllowLiveRespawn = false;
        }
    }

    // One local seat's keys into the chat. Per controller, like the combat wiring, so an airframe
    // swap's replacement is wired again.
    private void WireSeatChat(int seat)
    {
        if (Chat is not { } chat || seat < 0 || seat >= _seatRigs.Count
            || _seatRigs[seat].Controller is not { UseKeyboard: true } pilot)
        {
            return;
        }

        pilot.KeyboardHeld = () => chat.HoldsKeyboard;
        pilot.ChatAsked += team =>
        {
            chat.Open(seat, team);
            // The chat panel already draws the line at the top left, clear of the keyboard.
            ScreenKeyboard.Show(new ScreenKeyboardField(ChatKeyboardOwner, ChatKeyboardField, string.Empty,
                () => chat.Chat.Draft, Echoed: false));
        };
    }

    // One round this machine fired, told to the field so every other copy of the aeroplane
    // shoots too. The direction is the one the shooter's own assist chose, never re-derived
    // elsewhere. ⚠ Keep it off the seat's state channel: a sequenced carrier would discard a
    // burst behind a newer pose sample there.
    private void SendFire(int seat, WeaponDef weapon, Vector3 origin, Vector3 direction)
    {
        if (Link is not { } net || !_weaponWire.TryGetValue(weapon.Id, out int index) || index > byte.MaxValue)
        {
            return;
        }

        net.Broadcast(
            new Net.FireMessage((byte)seat, (byte)index, _fireSequence[seat]++, origin, direction,
                Net.NetMessage.NoSeat),
            Net.NetChannels.ForFire(seat));
    }

    // A round somebody else's aeroplane fired, spawned here from the event. ⚠ Only onto a seat
    // flown elsewhere. An aeroplane flown here already put that round in the world, and a
    // second one would double every burst.
    private void TakeFire(in Net.FireMessage fire)
    {
        if (_projectiles is not { } pool || _weaponDefs is not { } defs
            || fire.Seat >= _seatRigs.Count || fire.Weapon >= defs.All.Count
            || _seatRigs[fire.Seat].Controller is not { RemoteOwned: true } rig)
        {
            return;
        }

        // The muzzle basis is only a fallback for a missing aim vector, and the event always
        // carries one. A lock-on round steers after nothing here: the target is the shooter's
        // own pick and no seat is named on the wire.
        pool.Spawn(defs.All[fire.Weapon], new Transform3D(rig.Attitude, fire.Origin),
            rig.WorldVelocity, rig.PlayerIndex, null, fire.Direction, rig.Team);
    }

    // The hit-authority fork, asked of every strike on a seat before a point of damage is spent. True
    // means this machine does not decide this round. Either it was fired elsewhere, or it was
    // fired here at an aeroplane somebody else owns and the claim has just gone to them.
    private bool RouteHit(int victimSeat, in AircraftHit hit)
    {
        if (Link is not { } net || victimSeat >= Seats.Count)
        {
            return false;
        }

        int shooterSeat = SeatOfShooter(hit.Shooter);
        // Whoever owns the shooter decides, and the host stands in for every round no seat
        // fired (an AI, a world emplacement). Exactly one machine ever claims a hit.
        bool decidesHere = shooterSeat >= 0 && shooterSeat < Seats.Count
            ? Seats[shooterSeat].FlownHere
            : net.IsHost;
        if (!decidesHere)
        {
            return true;
        }

        if (Seats[victimSeat].FlownHere)
        {
            return false;
        }

        // The impact in the victim's own body space, off the SIM pose the victim's damage path
        // reads. It has flown on by the time the claim lands, and the zone must not fly with it.
        var pose = new Transform3D(hit.Victim.Attitude, hit.Victim.WorldPosition);
        int weapon = _weaponWire.TryGetValue(hit.Weapon.Id, out int index) ? index : 0;
        int hull = ZeppelinVersus.HullOfShooter(hit.Shooter);
        net.SendToSeat(
            victimSeat,
            new Net.HitMessage((byte)victimSeat,
                shooterSeat >= 0 ? (byte)shooterSeat : Net.NetMessage.NoSeat, (ushort)weapon,
                hit.DamageScale, (short)hit.ShapeIndex, pose.AffineInverse() * hit.Impact,
                hull is >= 0 and < Net.NetMessage.NoSeat ? (byte)hull : Net.NetMessage.NoSeat),
            Net.NetChannels.Events);
        return true;
    }

    // A shooter's claim on an aeroplane flown here, spent through the same damage path a local
    // round takes. ⚠ Straight at the controller, never through the body. The body would offer it
    // to the router again, and the router would bounce it back onto the wire.
    private void TakeHit(in Net.HitMessage hit)
    {
        if (_weaponDefs is not { } defs || hit.VictimSeat >= _seatRigs.Count
            || hit.Weapon >= defs.All.Count || !Seats[hit.VictimSeat].FlownHere
            || _seatRigs[hit.VictimSeat].Controller is not { } victim)
        {
            return;
        }

        int shooter = hit.ShooterSeat < _seatRigs.Count
            ? _seatRigs[hit.ShooterSeat].Controller?.PlayerIndex ?? ProjectilePool.NoShooter
            : hit.Hull != Net.NetMessage.NoSeat ? ZeppelinVersus.BroadsideShooter(hit.Hull)
            : ProjectilePool.NoShooter;
        // A negative share would hand the pools back. No ceiling: the debug kill key claims a 1e6 share.
        var pose = new Transform3D(victim.Attitude, victim.WorldPosition);
        victim.TakeProjectileHit(defs.All[hit.Weapon], pose * hit.LocalImpact,
            victim.Body?.PartName(hit.Part) ?? "center", shooter, Math.Max(0f, hit.Damage));
    }

    // Every seat flown here whose ledger moved since the last step, sent whole. Read off the seat's
    // current controller, so an airframe swap's replacement is covered with no rewiring.
    private void SendChangedDamage()
    {
        if (Link is not { } net)
        {
            return;
        }

        for (int seat = 0; seat < _seatRigs.Count && seat < Seats.Count; seat++)
        {
            if (Seats[seat].FlownHere && _seatRigs[seat].Controller is { Damage: { } damage } && damage.TakeChanged())
            {
                net.Broadcast(new Net.DamageMessage((byte)seat, PoolsOf(damage)), Net.NetChannels.Events);
            }
        }
    }

    // The owner's ledger, mirrored into this machine's copy, which then plays its stages off it as
    // the owner's intake did. The copy's own before-state decides a restore, so a respawn's full
    // ledger, which the copy's own respawn already matched, counts as nothing.
    private void TakeDamage(in Net.DamageMessage damage)
    {
        if (damage.Seat >= _seatRigs.Count
            || _seatRigs[damage.Seat].Controller is not { RemoteOwned: true, Damage: { } ledger } rig)
        {
            return;
        }

        bool wasFull = ledger.IsFull;
        bool zones = Mirror(ledger, damage.Pools, rig.Name);
        DamageTaken++;
        if (rig.ShowRemoteDamage(wasFull, zones))
        {
            RepairsTaken++;
        }

        _voice?.TakeRemotePlayerHull(rig, ledger.SummaryHealthFraction);
    }

    // Writes the pools into the copy's ledger and answers whether its zones were written. A count
    // unlike the copy's means the two machines briefly fly different airframes around a swap. Only
    // the whole pair is taken then, and the next ledger after the copy's own swap corrects it.
    private bool Mirror(PlaneDamage ledger, in Net.DamagePools pools, string name)
    {
        ledger.MirrorWhole(Net.DamagePools.Fraction(pools.WholeArmor), Net.DamagePools.Fraction(pools.WholeHealth));
        if (pools.Zones != ledger.Zones.Count || pools.Zones > Net.DamagePools.MaxZones)
        {
            if (!_zoneMismatchLogged)
            {
                _zoneMismatchLogged = true;
                Log.Info("flight", $"net damage: {name}'s owner counts {pools.Zones} zone(s) and this copy {ledger.Zones.Count}; the whole pair alone is mirrored");
            }

            return false;
        }

        for (int i = 0; i < pools.Zones; i++)
        {
            ledger.MirrorZone(i, Net.DamagePools.Fraction(pools.ArmorAt(i)), Net.DamagePools.Fraction(pools.HealthAt(i)));
        }

        return true;
    }

    // A death somebody else's machine reported: the wreck plays out here as it does there, and
    // on the host the same report moves the score.
    private void TakeDeath(in Net.DeathMessage death)
    {
        if (death.VictimSeat < _seatRigs.Count
            && _seatRigs[death.VictimSeat].Controller is { RemoteOwned: true } rig)
        {
            int? killer = death.KillerSeat < _seatRigs.Count
                ? _seatRigs[death.KillerSeat].Controller?.PlayerIndex
                : null;
            rig.TakeRemoteDeath(killer);
        }

        _dogfight?.ScoreDeath(death);
    }

    // A host answers a guest that loads after the start at once, so a late machine is never held.
    // A loaded word under another round opens nothing: it may be an earlier flight's on this link.
    // The host names its round in reply, and a guest that did not know it yet answers again.
    private void TakeStartWord(int peer, Net.StartGateMessage word)
    {
        // A guest's word can precede its roster, which names the host, so the sender stands in.
        if (Link is { IsHost: false })
        {
            StartGate ??= Net.NetStartGate.Guest(peer);
        }

        if (StartGate is not { } gate || Link is not { } net)
        {
            return;
        }

        if (net.IsHost && word.Word == Net.NetStartWord.Loaded)
        {
            if (!gate.Current(word.Round))
            {
                SendHold(peer);
            }
            else if (!gate.Open)
            {
                gate.TakeLoaded(peer, word.Round);
            }
            else
            {
                net.Send(peer, new Net.StartGateMessage(Net.NetStartWord.Start, gate.Round), Net.NetChannels.Events);
            }
        }
        else if (!net.IsHost && word.Word == Net.NetStartWord.Hold)
        {
            if (gate.TakeHold(word.Round) && _startBuilt)
            {
                SendLoaded();
            }
        }
        else if (!net.IsHost && word.Word == Net.NetStartWord.Start)
        {
            gate.TakeStart(word.Round);
        }
    }

    // Under the round this guest heard, 0 before any hold word reached it. The host takes only
    // its own round, so it tells this word from one an earlier flight on this link sent.
    private void SendLoaded()
    {
        if (Link is { } net && StartGate is { } gate)
        {
            net.Send(net.HostPeer, new Net.StartGateMessage(Net.NetStartWord.Loaded, gate.Round), Net.NetChannels.Events);
        }
    }

    private void SendHold(int peer)
    {
        if (Link is { } net && StartGate is { } gate)
        {
            net.Send(peer, new Net.StartGateMessage(Net.NetStartWord.Hold, gate.Round), Net.NetChannels.Events);
        }
    }

    // A guest's link dropped on the host. Each seat it flew leaves the mission, here and on every
    // other guest, and the mission goes on without it. A bot seat never leaves with a guest.
    private void OnPeerLeft(int peer)
    {
        foreach (int seat in Net.NetSeats.LeavingWith(Seats, peer))
        {
            if (TakeSeatLeft(seat))
            {
                World?.SendSeatLeft(seat);
            }
        }
    }

    // Takes one departed guest's seat out of play and names it in every pane's message stack. A
    // race keeps its record, marked left; the inert aeroplane takes its ghost and label with it.
    private bool TakeSeatLeft(int seat)
    {
        if (seat < 0 || seat >= Seats.Count || !Net.NetSeats.LeavesWithPeer(Seats[seat]) || !_seatsLeft.Add(seat))
        {
            return false;
        }

        if (seat < _seatRigs.Count && _seatRigs[seat].Controller is { } plane && GodotObject.IsInstanceValid(plane))
        {
            plane.Inert = true;
        }

        // A drop is the other thing that can leave a match without an opponent (reason 4). The
        // host's step sends that ending; a guest's replicated match only marks the seat. A flag the
        // seat carried floats, as a death's does (FUN_004995a0).
        _dogfight?.SeatLeft(seat);
        Race?.SeatLeft(seat);
        string line = UI.Menu.CoopDoorText.Left(Seats[seat].Callsign);
        foreach (var rig in _rigs)
        {
            rig.Controller?.MessageStack?.Post(line, HudMessages.Side.Neutral);
        }

        Log.Info("core", $"net: seat {seat} ({Seats[seat].Callsign}) left the mission");
        return true;
    }

    /// <summary>What the world link follows, each built by an earlier step of the session build.
    /// </summary>
    public sealed class WorldLinkInputs
    {
        public ZeppelinRuntime? Zeppelins { get; init; }
        public SurfaceVehicleRuntime? SurfaceVehicles { get; init; }
        public CampaignDirector? Campaign { get; init; }
        public AiGeneratorRuntime? Generators { get; init; }
        public Func<IReadOnlyList<FlightController>> AiPlanes { get; init; } = Array.Empty<FlightController>;
        public AiVoiceRuntime? Voice { get; init; }
    }
}
