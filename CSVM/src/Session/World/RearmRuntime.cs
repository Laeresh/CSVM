using System;
using System.Collections.Generic;
using CSVM.Flight.Camera;
using CSVM.Flight.Hud;
using CSVM.Flight.Modes;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.Session.World;

/// <summary>What a <see cref="RearmRuntime"/> binds to: the seats it checks and the world whose
/// rearm nodes it reads. Beside them stands the mission type, which picks the nodes and the rule.
/// </summary>
internal sealed class RearmRuntimeInputs
{
    public required IReadOnlyList<PlayerRig> SeatRigs { get; init; }

    public required Func<int, bool> IsLocal { get; init; }

    /// <summary>Each seat's lobby team, or null for a match with no teams.</summary>
    public IReadOnlyList<int>? SeatTeams { get; init; }

    public required AnimRuntime World { get; init; }

    public bool CaptureTheFlag { get; init; }

    /// <summary>The hulls' side rules, set for a Zeppelin vs Zeppelin match, whose bases are the
    /// hulls' own nodes.</summary>
    public ZeppelinVersusRuntime? Zeppelins { get; init; }

    public ZeppelinRuntime? Hulls { get; init; }

    /// <summary><c>player.zrd</c>'s <c>rearm_rad</c> squared (<see cref="RearmBases.LoadRadiusSquared"/>).</summary>
    public float RadiusSquared { get; init; } = RearmBases.InitialRadiusSquared;

    public Messages? Strings { get; init; }
}

/// <summary>
/// The multiplayer rearm bases in a live match. Every machine checks only the seats it flies, as
/// the original checks its own aircraft. A living seat that enters a base serving it is restored in
/// full (<see cref="Flight.Airframe.FlightController.Rearm"/>) and its own pane posts "Rearmed!",
/// once per entry. The rule and the latch are <see cref="RearmBases"/>'s.
/// Decode: docs/org/multiplayer-rearm.md.
/// </summary>
internal sealed class RearmRuntime
{
    private readonly RearmRuntimeInputs _in;
    private readonly RearmBases _rules;
    private readonly List<(Node3D Node, int Team, int Hull)> _nodes;
    private readonly List<RearmBase> _bases = new();

    private RearmRuntime(RearmRuntimeInputs inputs, RearmBases rules, List<(Node3D, int, int)> nodes)
    {
        _in = inputs;
        _rules = rules;
        _nodes = nodes;
    }

    /// <summary>The rule and the latches as this machine has them, for a suite to read.</summary>
    public RearmBases Rules => _rules;

    /// <summary>How many bases the world holds for this match.</summary>
    public int BaseCount => _nodes.Count;

    /// <summary>How many restores this machine has made, for a suite to read.</summary>
    public int Rearms { get; private set; }

    /// <summary>Lists the match's bases as <c>FUN_00495980</c> does, from 1 up to the first node the
    /// world lacks. Each <c>rearm_node_n</c> serves lobby team <c>n</c>. In Zeppelin vs Zeppelin each
    /// <c>zep_rearm_node_n</c> serves hull <c>n - 1</c>'s side. Null when there is none.</summary>
    public static RearmRuntime? Open(RearmRuntimeInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        bool zeppelins = inputs.Zeppelins != null;
        var nodes = new List<(Node3D, int, int)>();
        for (int index = 1; Find(inputs.World, RearmBases.NodeName(index, zeppelins)) is { } node; index++)
        {
            int team = zeppelins ? inputs.Zeppelins!.Rules.TeamOfHull(index - 1) : index;
            nodes.Add((node, team, zeppelins ? inputs.Zeppelins!.Rules.HullOf(team) : -1));
        }

        if (nodes.Count == 0)
        {
            return null;
        }

        var rule = RearmBases.RuleFor(inputs.CaptureTheFlag, zeppelins);
        var runtime = new RearmRuntime(inputs, new RearmBases(rule, inputs.RadiusSquared), nodes);
        Log.Info("flight", $"rearm: {nodes.Count} base(s), {rule}, radius {MathF.Sqrt(inputs.RadiusSquared):0.#} m");
        return runtime;
    }

    /// <summary>Where base <paramref name="index"/> (0-based) stands now and the lobby team it
    /// serves, or null past the list. For a suite to read.</summary>
    public (Vector3 Position, int Team)? BaseAt(int index) =>
        index >= 0 && index < _nodes.Count && GodotObject.IsInstanceValid(_nodes[index].Node)
            ? (_nodes[index].Node.GlobalPosition, _nodes[index].Team)
            : null;

    /// <summary>One match step: every living seat flown here is measured against the bases that
    /// serve it, and rearmed on the step it enters.</summary>
    public void Step()
    {
        _bases.Clear();
        foreach (var (node, team, hull) in _nodes)
        {
            bool offered = GodotObject.IsInstanceValid(node) && node.IsVisibleInTree() && HullLives(hull);
            _bases.Add(new RearmBase(team, offered ? node.GlobalPosition : null));
        }

        for (int seat = 0; seat < _in.SeatRigs.Count; seat++)
        {
            // A downed aircraft is not measured at all, as FUN_0049b970 returns on the dead byte.
            if (!_in.IsLocal(seat)
                || _in.SeatRigs[seat].Controller is not { Crashed: false, Destroyed: false, Inert: false } pilot)
            {
                continue;
            }

            int team = _in.SeatTeams is { } teams && seat < teams.Count ? teams[seat] : 0;
            if (!_rules.Enters(seat, pilot.WorldPosition, team, _bases))
            {
                continue;
            }

            pilot.Rearm();
            Rearms++;
            if (pilot.MessageStack is { } stack)
            {
                HudMessages.PostRearmed(stack, _in.Strings);
            }

            Log.Info("flight", $"rearm: seat {seat} (team {team}) rearmed");
        }
    }

    private static Node3D? Find(AnimRuntime world, string name)
    {
        foreach (var node in world.FindNodes(name))
        {
            if (GodotObject.IsInstanceValid(node) && node.IsInsideTree())
            {
                return node;
            }
        }

        return null;
    }

    // A hull's base offers nothing once that hull is dead (FUN_0049b920's +6 test). Not a hull's: -1.
    private bool HullLives(int hull) =>
        hull < 0 || (_in.Hulls?.NodeAt(hull) is { } node && !_in.Hulls.IsDead(node));
}
