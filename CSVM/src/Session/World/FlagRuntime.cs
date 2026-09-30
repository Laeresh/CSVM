using System;
using System.Collections.Generic;
using System.Globalization;
using CSVM.Flight.Airframe;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Mech3.Anim;
using CSVM.Net;
using CSVM.Utils;
using Godot;

namespace CSVM.Session.World;

/// <summary>What a <see cref="FlagRuntime"/> binds to: the wire, the seats and the match it scores.
/// Beside them stand the world whose flags it moves and the panes and radio it tells.</summary>
internal sealed class FlagRuntimeInputs
{
    public required NetSession Net { get; init; }

    public required IReadOnlyList<PlayerRig> SeatRigs { get; init; }

    public required IReadOnlyList<PlayerRig> Panes { get; init; }

    public required IReadOnlyList<int> SeatTeams { get; init; }

    public required Func<int, bool> IsLocal { get; init; }

    public required VersusMatch Match { get; init; }

    public required Action<int> SendScore { get; init; }

    public required bool FlagHomeToCapture { get; init; }

    public AnimRuntime? World { get; init; }

    public Node3D? WorldScene { get; init; }

    public Func<string, Node3D?>? BuildLoose { get; init; }

    public WorldLights? Lights { get; init; }

    public MissionRadio? Radio { get; init; }

    public Messages? Strings { get; init; }

    public Func<Vector3, float?>? GroundAt { get; init; }

    /// <summary>A seat's callsign, the name a carrier's tag and the away marker print.</summary>
    public Func<int, string>? CallsignOf { get; init; }
}

/// <summary>
/// Capture the Flag in a live network match: <see cref="FlagMatch"/>'s rules over the mission's
/// <c>cs_flag_n</c> flags. Each machine checks its own seats against the flags and asks its host.
/// The host decides, scores and sends its table. Every machine moves the flags, speaks the six
/// <c>snd_CTF</c> lines, posts the flag lines and labels the flag markers from the changes. A downed
/// or ejecting carrier's flag floats on every machine, and the host sends it home when its throw runs
/// out.
/// Decode: docs/org/multiplayer-ctf.md.
/// </summary>
internal sealed class FlagRuntime
{
    // The six lines by what each announces (docs/org/multiplayer-ctf.md, "Voice").
    private const string LostLine = "snd_CTFlost";
    private const string StolenLine = "snd_CTFstolen";
    private const string ReturnedLine = "snd_CTFscoreRec";
    private const string EnemyScoredLine = "snd_CTFscoreEnemy";
    private const string ScoredLine = "snd_CTFscoreFlag";
    private const string CaughtLine = "snd_CTFscoreCapt";

    // The node a carried flag hangs under on every airframe, player and AI alike.
    private const string CarryNode = "cf_light";

    // The throw's draws, the flg_throw_n motion's translation_range: elevation and launch speed.
    private const float ThrowElevationMin = 75f;
    private const float ThrowElevationMax = 85f;
    private const float ThrowSpeedMin = 35f;
    private const float ThrowSpeedMax = 40f;

    // The flg_glow_n light a flag away from home carries: white, range 4 to 14, ambient 0.3 plus
    // diffuse 1.
    private const float GlowRangeMin = 4f;
    private const float GlowRangeMax = 14f;
    private const float GlowScalar = 1.3f;

    private readonly FlagRuntimeInputs _in;
    private readonly FlagMatch _flags;
    private readonly Dictionary<int, FlagProps> _props = new();
    private readonly Random _rng = new(Rng.IntSeedFor(Rng.Flags));
    private readonly int _localTeam;

    private FlagRuntime(FlagRuntimeInputs inputs, FlagMatch flags, int localTeam)
    {
        _in = inputs;
        _flags = flags;
        _localTeam = localTeam;
    }

    /// <summary>The six voice lines the flags speak, loaded by <c>FUN_00495c40</c>.</summary>
    public static IReadOnlyList<string> VoiceLines { get; } = new[]
    {
        LostLine, StolenLine, ReturnedLine, EnemyScoredLine, ScoredLine, CaughtLine,
    };

    /// <summary>The rules as this machine has them, for a suite to read.</summary>
    public FlagMatch Flags => _flags;

    /// <summary>How many flag lines this machine's panes were posted, for a suite to read.</summary>
    public int LinesPosted { get; private set; }

    /// <summary>The voice lines this machine asked the radio for, in order, for a suite to read.
    /// </summary>
    public List<string> Spoken { get; } = new();

    /// <summary>Builds one flag per lobby team <c>n</c> whose <c>cs_flag_n</c> node the world
    /// holds, as <c>FUN_00495d40</c> does, and wires the wire. Null when the world holds no flag
    /// for any team flying, which leaves the match a plain team Deathmatch.</summary>
    public static FlagRuntime? Open(FlagRuntimeInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var props = new Dictionary<int, FlagProps>();
        var homes = new List<(int Team, Vector3 Home)>();
        var teams = new SortedSet<int>(inputs.SeatTeams);
        foreach (int team in teams)
        {
            string n = team.ToString(CultureInfo.InvariantCulture);
            if (team <= 0 || Find(inputs, "cs_flag_" + n) is not { } flag)
            {
                continue;
            }

            // The carried flag is a library root outside the world tree, which flg_on_n adds to
            // the carrier's cf_light, so this machine builds its own copy.
            var carried = Find(inputs, "cs_flg_light" + n) ?? inputs.BuildLoose?.Invoke("cs_flg_light" + n);
            props[team] = new FlagProps(flag, carried, carried?.GetParent(), "turnon_flite" + n, "turnoff_flite" + n);
            homes.Add((team, flag.GlobalPosition));
        }

        if (homes.Count == 0)
        {
            Log.Warn("flight", $"ctf: the mission holds no cs_flag_n for teams {string.Join(",", teams)}, flying a team Deathmatch");
            return null;
        }

        var seatTeams = inputs.SeatTeams;
        var match = new FlagMatch(homes, seat => seat >= 0 && seat < seatTeams.Count ? seatTeams[seat] : 0, inputs.FlagHomeToCapture);
        int localTeam = 0;
        for (int seat = 0; seat < seatTeams.Count && localTeam == 0; seat++)
        {
            localTeam = inputs.IsLocal(seat) ? seatTeams[seat] : 0;
        }

        var runtime = new FlagRuntime(inputs, match, localTeam);
        foreach (var (team, prop) in props)
        {
            runtime._props[team] = prop;
            runtime.ShowHome(team, prop);
        }

        runtime.Wire();
        inputs.Lights?.AddSource(runtime.SubmitGlow);
        Log.Info("flight", $"ctf: {homes.Count} flag(s) for teams {string.Join(",", props.Keys)}, {(inputs.Net.IsHost ? "host (deciding every ask)" : "guest (asking the host)")}{(inputs.FlagHomeToCapture ? ", own flag home to capture" : "")}");
        return runtime;
    }

    /// <summary>One match step, before the match's own. It moves the flags' clock and throw arcs and
    /// checks this machine's seats against the flags. The host sends home each throw that ran out.
    /// Nothing is asked once the match has ended.</summary>
    public void Step(float dt)
    {
        var ended = _flags.Advance(dt);
        foreach (var (team, prop) in _props)
        {
            if (_flags.FloatingAt(team) is { } at && prop.Carried != null)
            {
                prop.Carried.GlobalPosition = at;
            }
        }

        if (_in.Match.Completed)
        {
            return;
        }

        if (_in.Net.IsHost)
        {
            foreach (int team in ended)
            {
                Decided(_flags.Decide(team, FlagAsk.Home, FlagMatch.NoHolder));
            }
        }

        for (int seat = 0; seat < _in.SeatRigs.Count; seat++)
        {
            // A downed pilot's wreck takes nothing, the flag it just dropped included.
            if (_in.IsLocal(seat) && _in.SeatRigs[seat].Controller is { Crashed: false, Destroyed: false, Inert: false } pilot
                && _flags.Check(seat, pilot.WorldPosition) is { } ask)
            {
                Ask(seat, ask.Team, ask.Ask);
            }
        }
    }

    /// <summary>The node <paramref name="team"/>'s flag is carried and floats as, the mission's
    /// <c>cs_flg_lightn</c>, or null for a team with none. For a suite to read.</summary>
    public Node3D? CarriedFlag(int team) => _props.TryGetValue(team, out var prop) ? prop.Carried : null;

    /// <summary>How one of a flag's three target markers reads now, for the mission's site feed,
    /// labelled by side (<see cref="FlagMarkers"/>). Null for a key naming no marker of a flag
    /// this match built.</summary>
    public SiteSide? SideOf(string key)
    {
        if (FlagMarkers.MarkerOf(key) is not { } marker || !_props.TryGetValue(marker.Team, out var prop)
            || _flags.RowOf(marker.Team) is not { } row)
        {
            return null;
        }

        int team = marker.Team;
        Vector3? at = marker.Marker != FlagMarker.Away ? null
            : prop.Carried is { } flag && flag.IsInsideTree() ? flag.GlobalPosition
            : _flags.FloatingAt(team) ?? HolderAt(row.Holder) ?? _flags.HomeOf(team);
        return FlagMarkers.Side(marker.Marker, row, SideTeam(team), _in.Match.TeamName(team),
            CallsignOf(row.Holder), _in.Strings, at);
    }

    /// <summary>A seat went down on this machine's copy of the match: the flag it carried floats
    /// from where it hung. Every machine runs it for every seat, as <c>FUN_0049ab50</c> runs from
    /// the death handler.</summary>
    public void Downed(int seat) => Float(seat);

    /// <summary>A seat flown here lets its flag go, the console's <c>ejectflag</c>. A guest asks its
    /// host, which floats the flag and relays the eject, so every machine floats it as it does a
    /// downed carrier's. False when the seat carries nothing or the match has ended.</summary>
    public bool Eject(int seat)
    {
        int team = _flags.Carried(seat);
        if (team == 0 || !_in.IsLocal(seat) || _in.Match.Completed)
        {
            return false;
        }

        if (_in.Net.IsHost)
        {
            Ejected(seat, team);
        }
        else
        {
            _in.Net.Send(_in.Net.HostPeer, new FlagRequestMessage((byte)team, (byte)FlagAsk.Eject, (byte)seat), NetChannels.Events);
        }

        return true;
    }

    /// <summary>The host's rematch: every flag home, its table sent, and nothing scored.</summary>
    public void Restart()
    {
        if (!_in.Net.IsHost)
        {
            return;
        }

        foreach (var change in _flags.Reset())
        {
            Show(change);
        }

        SendTable();
    }

    // The built world's node of that name, else the animation runtime's, in the tree either way.
    private static Node3D? Find(FlagRuntimeInputs inputs, string name)
    {
        if (inputs.WorldScene is { } scene && FindUnder(scene, name) is { } built && built.IsInsideTree())
        {
            return built;
        }

        foreach (var node in inputs.World?.FindNodes(name) ?? Array.Empty<Node3D>())
        {
            if (GodotObject.IsInstanceValid(node) && node.IsInsideTree())
            {
                return node;
            }
        }

        return null;
    }

    private static Node3D? FindUnder(Node root, string name)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Node3D node && node.HasMeta(AnimRuntime.NameMeta)
                && node.GetMeta(AnimRuntime.NameMeta).AsString().Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            if (FindUnder(child, name) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // A flag's side as a hostility id: its lobby team, banded.
    private static int SideTeam(int team) => AimAssist.LobbyTeam(team) ?? AimAssist.NeutralTeam;

    // The host's eject: floated here, then the same ask to every guest, which floats it there.
    private void Ejected(int seat, int team)
    {
        Log.Info("flight", $"ctf: seat {seat} ejects team {team}'s flag");
        Float(seat);
        _in.Net.Broadcast(new FlagRequestMessage((byte)team, (byte)FlagAsk.Eject, (byte)seat), NetChannels.Events);
    }

    // FUN_0049ab50 on this machine: the seat's flag floats from where it hung.
    private void Float(int seat)
    {
        int team = _flags.Carried(seat);
        if (team == 0 || !_props.TryGetValue(team, out var prop))
        {
            return;
        }

        var at = prop.Carried is { } node && node.IsInsideTree() ? node.GlobalPosition
            : seat < _in.SeatRigs.Count && _in.SeatRigs[seat].Controller is { } pilot ? pilot.WorldPosition
            : _flags.HomeOf(team);
        float elevation = ThrowElevationMin + ((float)_rng.NextDouble() * (ThrowElevationMax - ThrowElevationMin));
        float speed = ThrowSpeedMin + ((float)_rng.NextDouble() * (ThrowSpeedMax - ThrowSpeedMin));
        var velocity = MotionRuntime.RangeLaunchDirection(0f, elevation) * speed;
        float floor = _in.GroundAt?.Invoke(at) ?? float.MinValue;
        if (_flags.Drop(seat, at, velocity, floor) is { } change)
        {
            Show(change);
        }
    }

    private void Wire()
    {
        var net = _in.Net;
        net.On<FlagTableMessage>((_, table) => TakeTable(table));
        net.On<FlagRequestMessage>(TakeRequest);
    }

    // One ask from a seat flown here. The host decides it at once. A guest asks its host, and takes a
    // flag ahead of the answer as FUN_0049a050 does.
    private void Ask(int seat, int team, FlagAsk ask)
    {
        if (_in.Net.IsHost)
        {
            Decided(_flags.Decide(team, ask, seat));
            return;
        }

        _in.Net.Send(_in.Net.HostPeer, new FlagRequestMessage((byte)team, (byte)ask, (byte)seat), NetChannels.Events);
        if (ask == FlagAsk.Take && _flags.TakeAhead(team, seat) is { } change)
        {
            Show(change);
        }
    }

    // A guest's ask on the host. The table goes back whatever the decision, which is what corrects
    // a guest that took ahead and lost (FUN_0049a170 re-sends the unchanged row). An eject reaches a
    // guest only as the host's relay.
    private void TakeRequest(int peer, FlagRequestMessage request)
    {
        if (request.Seat >= _in.SeatRigs.Count)
        {
            return;
        }

        if (request.Ask == (byte)FlagAsk.Eject)
        {
            TakeEject(peer, request);
            return;
        }

        if (!_in.Net.IsHost || request.Ask is not ((byte)FlagAsk.Take or (byte)FlagAsk.Home))
        {
            return;
        }

        var change = _in.Match.Completed ? null : _flags.Decide(request.Team, (FlagAsk)request.Ask, request.Seat);
        if (change is { } moved)
        {
            Show(moved);
            Score(moved);
        }

        SendTable();
    }

    // An eject on the wire. The host takes it only from the machine flying the seat, for the flag
    // that seat carries. A guest takes only its host's relay.
    private void TakeEject(int peer, FlagRequestMessage request)
    {
        if (!_in.Net.IsHost)
        {
            if (peer == _in.Net.HostPeer)
            {
                Float(request.Seat);
            }

            return;
        }

        if (_in.Net.PeerOfSeat(request.Seat) == peer && !_in.Match.Completed && request.Team != 0
            && _flags.Carried(request.Seat) == request.Team)
        {
            Ejected(request.Seat, request.Team);
        }
    }

    private void TakeTable(FlagTableMessage table)
    {
        if (_in.Net.IsHost)
        {
            return;
        }

        var rows = new List<FlagRow>(table.Rows.Count);
        foreach (var row in table.Rows)
        {
            rows.Add(new FlagRow(row.Team, (FlagState)row.State, row.Holder == NetMessage.NoSeat ? FlagMatch.NoHolder : row.Holder));
        }

        foreach (var change in _flags.Apply(rows))
        {
            Show(change);
        }
    }

    // A host decision: shown and scored here, then the table to every guest.
    private void Decided(FlagChange? change)
    {
        if (change is not { } moved)
        {
            return;
        }

        Show(moved);
        Score(moved);
        SendTable();
    }

    private void SendTable()
    {
        // Every flag as it stands. A guest applies no floating row, since each machine reaches that
        // state on its own.
        var rows = new List<NetFlagRow>();
        foreach (var row in _flags.Rows)
        {
            rows.Add(new NetFlagRow((byte)row.Team, (byte)row.State, row.Holder < 0 ? NetMessage.NoSeat : (byte)row.Holder));
        }

        _in.Net.Broadcast(new FlagTableMessage(rows), NetChannels.Events);
    }

    // The host's scoring of a flag brought home, and the carrier's line sent at once.
    private void Score(FlagChange change)
    {
        var teams = _in.SeatTeams;
        int points = FlagMatch.Points(change, seat => seat >= 0 && seat < teams.Count ? teams[seat] : 0, _in.Match.Scores);
        if (points == 0)
        {
            return;
        }

        _in.Match.AddScore(change.HolderBefore, points);
        _in.SendScore(change.HolderBefore);
        Log.Info("flight", $"ctf: seat {change.HolderBefore} brought team {change.Team}'s flag home, +{points}");
    }

    // One change as every machine shows it: the props, the voice and the HUD line.
    private void Show(FlagChange change)
    {
        if (!_props.TryGetValue(change.Team, out var prop))
        {
            return;
        }

        string team = _in.Match.TeamName(change.Team);
        if (change.HolderBefore != change.HolderAfter)
        {
            Tag(change.HolderBefore, 0);
        }

        switch (change.To)
        {
            case FlagState.Held:
                if (change.From == FlagState.Home)
                {
                    prop.Home.Visible = false;
                    Play(prop.LightOff);
                    Speak(change.Team == _localTeam ? LostLine : StolenLine);
                }
                else if (change.From == FlagState.Floating && change.Team == _localTeam)
                {
                    Speak(CaughtLine);
                }

                Carry(prop, change.HolderAfter);
                Tag(change.HolderAfter, change.Team);
                Post(HudMessages.FlagTakenKey, team);
                break;
            case FlagState.Home:
                Post(HudMessages.FlagHomeKey, team);
                if (change.From == FlagState.Held && change.HolderBefore >= 0)
                {
                    int carrierTeam = change.HolderBefore < _in.SeatTeams.Count ? _in.SeatTeams[change.HolderBefore] : 0;
                    if (carrierTeam == change.Team)
                    {
                        Speak(carrierTeam == _localTeam ? ReturnedLine : null);
                    }
                    else
                    {
                        Speak(carrierTeam == _localTeam ? ScoredLine : EnemyScoredLine);
                    }
                }

                ShowHome(change.Team, prop);
                break;
            case FlagState.Floating:
                Loose(prop);
                Post(HudMessages.FlagFloatingKey, team);
                break;
        }

        Log.Info("flight", $"ctf: team {change.Team}'s flag {change.From}->{change.To}, holder {change.HolderBefore}->{change.HolderAfter}");
    }

    // The flag at its base: the base flag shown, the carried one hidden and back in the world, and
    // the base's light on.
    private void ShowHome(int team, FlagProps prop)
    {
        prop.Home.Visible = true;
        Loose(prop);
        if (prop.Carried != null)
        {
            prop.Carried.Visible = false;
            prop.Carried.GlobalPosition = _flags.HomeOf(team);
        }

        Play(prop.LightOn);
    }

    // player-flg_on_n: the flag hung at the holder's cf_light, at its origin.
    private void Carry(FlagProps prop, int holder)
    {
        if (prop.Carried is not { } flag || holder < 0 || holder >= _in.SeatRigs.Count
            || _in.SeatRigs[holder].Controller is not { } pilot)
        {
            return;
        }

        Node3D mount = pilot.PlaneModel is { } model ? FindUnder(model, CarryNode) ?? model : pilot;
        flag.Reparent(mount, false);
        flag.Position = Vector3.Zero;
        flag.Visible = true;
    }

    // The carrier's name tag, row 198 by the reading pane's side. Team 0 puts the airframe's own
    // name back once the flag leaves it, as FUN_0049a300 and FUN_0049ab50 restore the pilot's.
    private void Tag(int seat, int team)
    {
        if (seat < 0 || seat >= _in.SeatRigs.Count || _in.SeatRigs[seat].Controller is not { } pilot)
        {
            return;
        }

        string holder = CallsignOf(seat);
        int side = SideTeam(team);
        pilot.MarkerName = team == 0 ? null : own => FlagMarkers.HolderTag(_in.Strings, holder, own, side);
    }

    // A seat's callsign, or the original's "Unknown" for a pilot never named.
    private string CallsignOf(int seat) =>
        seat >= 0 && _in.CallsignOf?.Invoke(seat) is { Length: > 0 } name ? name
        : _in.Strings?.Get(HudMessages.UnknownKey) ?? HudMessages.UnknownKey;

    private Vector3? HolderAt(int seat) =>
        seat >= 0 && seat < _in.SeatRigs.Count && _in.SeatRigs[seat].Controller is { } pilot
            ? pilot.WorldPosition
            : null;

    // player-flg_throw_n's reparent to the world, kept where it hung.
    private void Loose(FlagProps prop)
    {
        if (prop.Carried is not { } flag || prop.World is not { } world || flag.GetParent() == world)
        {
            return;
        }

        flag.Reparent(world, true);
    }

    private void Play(string anim)
    {
        if (_in.World is { } world && world.Handles(anim))
        {
            world.Play(anim);
        }
    }

    private void Speak(string? line)
    {
        if (line == null)
        {
            return;
        }

        Spoken.Add(line);
        _in.Radio?.Speak(line, _rng);
    }

    private void Post(string key, string team)
    {
        foreach (var pane in _in.Panes)
        {
            if (pane.Controller?.MessageStack is { } stack)
            {
                HudMessages.PostFlagLine(stack, _in.Strings, key, team);
                LinesPosted++;
            }
        }
    }

    // The flg_glow_n light on every flag away from its base.
    private void SubmitGlow(WorldLights lights)
    {
        foreach (var (_, prop) in _props)
        {
            if (prop.Carried is { Visible: true } flag && flag.IsInsideTree())
            {
                lights.Add(flag.GlobalPosition, Colors.White, GlowRangeMin, GlowRangeMax, GlowScalar);
            }
        }
    }

    // One team's flag props: the flag at its base, and the flag carried and floating with the world
    // node it lives under when loose. The base light's two animations stand beside them.
    private sealed record FlagProps(Node3D Home, Node3D? Carried, Node? World, string LightOn, string LightOff);
}
