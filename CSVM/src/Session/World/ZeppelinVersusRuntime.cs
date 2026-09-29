using System;
using System.Collections.Generic;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Utils;
using Godot;

namespace CSVM.Session.World;

/// <summary>What a <see cref="ZeppelinVersusRuntime"/> binds to: the wire, the seats, the match it
/// scores and the zeppelins it scores off, with the panes and radio it tells.</summary>
internal sealed class ZeppelinVersusInputs
{
    public required NetSession Net { get; init; }

    public required IReadOnlyList<PlayerRig> SeatRigs { get; init; }

    public required IReadOnlyList<PlayerRig> Panes { get; init; }

    public required IReadOnlyList<int> SeatTeams { get; init; }

    public required Func<int, bool> IsLocal { get; init; }

    public required Func<int, int> SeatOfShooter { get; init; }

    public required VersusMatch Match { get; init; }

    public required ZeppelinRuntime Zeppelins { get; init; }

    public required Action<int> SendScore { get; init; }

    public MissionRadio? Radio { get; init; }

    public Messages? Strings { get; init; }

    public Func<Vector3, float?>? GroundAt { get; init; }

    /// <summary><c>player.zrd</c>'s <c>respawn_rad</c>, the return's distance out along its bearing.
    /// </summary>
    public float RespawnRadius { get; init; }

    /// <summary><c>player.zrd</c>'s <c>respawn_el</c>, the return's height over high ground.</summary>
    public float RespawnMargin { get; init; }
}

/// <summary>
/// Zeppelin vs Zeppelin in a live network match: <see cref="ZeppelinVersus"/>'s rules over the
/// mission's two hulls. Each hull takes its side's lobby team, so aim and friend-or-foe follow it.
/// The host scores each dead gas bag or cannon to the seat whose hit killed it, and ends the match
/// on a lost hull. Both hulls' broadsides are engaged, their rounds named for the hull
/// that fired. Every machine speaks the gas bag lines and posts the ending.
/// Decode: docs/org/multiplayer-zvz.md.
/// </summary>
internal sealed class ZeppelinVersusRuntime
{
    // FUN_00495c40's zeppelin lines: a gas bag of this machine's hull, one of the other's, and the
    // hull ends. The original plays only snd_Zep_dest, at end reason 3 (0x4997d8).
    private const string GasbagLostLine = "snd_Zep_GBlost";
    private const string GasbagDestroyedLine = "snd_Zep_GBdest";
    private const string HullDestroyedLine = "snd_Zep_dest";
    private const string HullLostLine = "snd_Zep_lost";

    private readonly ZeppelinVersusInputs _in;
    private readonly ZeppelinVersus _rules;
    private readonly int _localTeam;
    private readonly Random _rng = new(Rng.IntSeedFor(Rng.ZeppelinVersus));
    private bool _announced;

    private ZeppelinVersusRuntime(ZeppelinVersusInputs inputs, ZeppelinVersus rules, int localTeam)
    {
        _in = inputs;
        _rules = rules;
        _localTeam = localTeam;
    }

    /// <summary>The voice lines the zeppelins speak, prewarmed with the world's sounds.</summary>
    public static IReadOnlyList<string> VoiceLines { get; } = new[]
    {
        GasbagLostLine, GasbagDestroyedLine, HullDestroyedLine, HullLostLine,
    };

    /// <summary>The rules as this machine has them, for a suite to read.</summary>
    public ZeppelinVersus Rules => _rules;

    /// <summary>The voice lines this machine asked the radio for, in order, for a suite to read.
    /// </summary>
    public List<string> Spoken { get; } = new();

    /// <summary>How many ending lines this machine's panes were posted, for a suite to read.</summary>
    public int LinesPosted { get; private set; }

    /// <summary><c>player.zrd</c>'s <c>respawn_rad</c> and <c>respawn_el</c> (1200 and 100 shipped),
    /// read by <c>FUN_004735b0</c> into <c>0x628f08</c> and <c>0x628f0c</c>. A key the file lacks
    /// keeps the executable's initialised value.</summary>
    public static (float Radius, float Margin) LoadRespawnRing(string zrdrPath)
    {
        const float InitialRadius = 4000.4f;
        const float InitialMargin = 100.2f;
        try
        {
            if (Zrdr.LoadFile(zrdrPath, "player.json") is [List<object?> list, ..])
            {
                var player = ZrdrDict.FromAlternating(list);
                float radius = player.TryFloat("respawn_rad", out float r) ? r : InitialRadius;
                float margin = player.TryFloat("respawn_el", out float m) ? m : InitialMargin;
                return (radius, margin);
            }
        }
        catch (System.IO.IOException e)
        {
            Log.Warn("flight", $"zvz: player.zrd unreadable, the respawn ring keeps its initialised values: {e.Message}");
        }

        return (InitialRadius, InitialMargin);
    }

    /// <summary>Puts hull 0 on the first lobby team of the seat order and hull 1 on the second, and
    /// wires the scoring. Null with fewer than two teams or two hulls, which leaves the match a plain
    /// team Deathmatch.</summary>
    public static ZeppelinVersusRuntime? Open(ZeppelinVersusInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var (first, second) = ZeppelinVersus.Sides(inputs.SeatTeams);
        var zeppelins = inputs.Zeppelins;
        if (second == 0 || zeppelins.NodeAt(0) is not { } hull0 || zeppelins.NodeAt(1) is not { } hull1)
        {
            Log.Warn("flight", $"zvz: teams {first},{second} over {zeppelins.LiveCount} hull(s), two of each are needed, flying a team Deathmatch");
            return null;
        }

        var rules = new ZeppelinVersus(first, second, inputs.Match.Scores);
        zeppelins.SetTeam(hull0, AimAssist.LobbyTeam(first) ?? AimAssist.NeutralTeam);
        zeppelins.SetTeam(hull1, AimAssist.LobbyTeam(second) ?? AimAssist.NeutralTeam);

        // FUN_00496490 writes every hull's engage byte, so both broadsides fire on every machine.
        // Only the host's rounds spend anything: a guest's world pools and aircraft wait for its word.
        zeppelins.NamesBroadsideRounds = true;
        zeppelins.SetCannonsEngaged(hull0, true, "Zeppelin vs Zeppelin");
        zeppelins.SetCannonsEngaged(hull1, true, "Zeppelin vs Zeppelin");
        int localTeam = 0;
        for (int seat = 0; seat < inputs.SeatTeams.Count && localTeam == 0; seat++)
        {
            localTeam = inputs.IsLocal(seat) ? inputs.SeatTeams[seat] : 0;
        }

        var runtime = new ZeppelinVersusRuntime(inputs, rules, localTeam);
        zeppelins.PartDestroyed += runtime.PartDestroyed;
        if (inputs.Net.IsHost)
        {
            zeppelins.ZeppelinKilled += runtime.HullKilled;
        }

        Log.Info("flight", $"zvz: '{hull0}' flies team {first}, '{hull1}' team {second}, {(inputs.Net.IsHost ? "host (scoring every part and ending on a hull)" : "guest (hearing the host's scores and ending)")}");
        return runtime;
    }

    /// <summary>Where the host brings <paramref name="seat"/> back: halfway between the other
    /// aircraft and the seat's own hull (<see cref="ZeppelinVersus.RespawnPoint"/>). Null for a seat
    /// with no other aircraft in the match, which then takes the rotation's point.</summary>
    public SpawnPoint? RespawnPoint(int seat)
    {
        var others = new List<Vector3>();
        for (int i = 0; i < _in.SeatRigs.Count; i++)
        {
            if (i != seat && _in.SeatRigs[i].Controller is { Inert: false } other && GodotObject.IsInstanceValid(other))
            {
                others.Add(other.WorldPosition);
            }
        }

        int team = seat >= 0 && seat < _in.SeatTeams.Count ? _in.SeatTeams[seat] : 0;
        var hull = _in.Zeppelins.HullPositionAt(_rules.HullOf(team));
        return ZeppelinVersus.RespawnPoint(others, hull, seat + 1, _in.RespawnRadius, _in.RespawnMargin, _in.GroundAt);
    }

    /// <summary>How a hull's target marker reads, for the mission's site feed. It is named for its
    /// side's team and reads "Defend" to that side and "Destroy" to the other
    /// (<see cref="ZeppelinVersus.HullSide"/>). Null for a key that is no hull of this match.</summary>
    public SiteSide? SideOf(string key)
    {
        for (int hull = 0; hull < 2; hull++)
        {
            if (string.Equals(_in.Zeppelins.NodeAt(hull), key, StringComparison.OrdinalIgnoreCase))
            {
                int team = _rules.TeamOfHull(hull);
                return ZeppelinVersus.HullSide(AimAssist.LobbyTeam(team) ?? AimAssist.NeutralTeam,
                    _in.Match.TeamName(team), _in.Strings);
            }
        }

        return null;
    }

    /// <summary>A guest's copy of the host's objective ending, with the lobby team that won.</summary>
    public void TakeEnding(int winner) => _in.Match.EndOnHullLoss(_rules.OtherTeam(winner), winner);

    /// <summary>The ending on this machine, once: "Game Over:" and "Zeppelin Destroyed" in every
    /// pane. The lost hull's side hears its hull lost, the other side a hull destroyed.</summary>
    public void Announce()
    {
        if (_announced)
        {
            return;
        }

        _announced = true;
        foreach (var pane in _in.Panes)
        {
            if (pane.Controller?.MessageStack is { } stack)
            {
                HudMessages.PostHullLost(stack, _in.Strings);
                LinesPosted++;
            }
        }

        int winner = _in.Match.ObjectiveWinner;
        Speak(_localTeam > 0 && winner > 0 && _localTeam != winner && _rules.HullOf(_localTeam) >= 0 ? HullLostLine : HullDestroyedLine);
    }

    // Every machine hears a gas bag's scoring event. The host alone scores it, to the seat whose
    // hit killed the part. A part dying under the hull's own death plays nothing.
    private void PartDestroyed(int hull, string gasbag, bool cannon, int shooter)
    {
        if (_in.Match.Completed || _in.Zeppelins.NodeAt(hull) is not { } node || _in.Zeppelins.IsDead(node))
        {
            return;
        }

        int seat = _in.Net.IsHost ? _in.SeatOfShooter(shooter) : -1;
        var loss = new HullPartLoss(hull, gasbag, cannon, seat);
        if (!_rules.Counts(loss))
        {
            return;
        }

        Speak(_rules.TeamOfHull(hull) == _localTeam ? GasbagLostLine : GasbagDestroyedLine);
        if (seat < 0 || seat >= _in.SeatTeams.Count)
        {
            return;
        }

        int points = _rules.Points(loss, _in.SeatTeams[seat]);
        _in.Match.AddScore(seat, points);
        _in.SendScore(seat);
        Log.Info("flight", $"zvz: seat {seat} downed hull {hull}'s {(cannon ? "cannon on " : "")}{gasbag}, {points:+#;-#;0}");
    }

    // The host's end reason 3: the lost hull's side loses and the other wins.
    private void HullKilled(string node)
    {
        for (int hull = 0; hull < _in.Zeppelins.LiveCount; hull++)
        {
            if (string.Equals(_in.Zeppelins.NodeAt(hull), node, StringComparison.OrdinalIgnoreCase))
            {
                int losing = _rules.TeamOfHull(hull);
                int winning = _rules.OtherTeam(losing);
                _in.Match.EndOnHullLoss(losing, winning);
                Log.Info("flight", $"zvz: '{node}' lost, team {losing} loses and team {winning} wins");
                return;
            }
        }
    }

    private void Speak(string line)
    {
        Spoken.Add(line);
        _in.Radio?.Speak(line, _rng);
    }
}
