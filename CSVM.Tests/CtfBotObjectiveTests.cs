using System.Collections.Generic;
using CSVM.Flight.Ai;
using CSVM.Flight.Modes;
using Godot;
using Xunit;

namespace CSVM.Tests;

public class CtfBotObjectiveTests
{
    private const int Bot = 0;
    private const int Mate = 1;
    private const int Enemy = 2;
    private const int Raider = 3;

    private static readonly Vector3 RedHome = new(0, 100, 0);
    private static readonly Vector3 BlueHome = new(0, 100, -3000);
    private static readonly Vector3 Up = Vector3.Up;

    [Fact]
    public void ABotAttacksTheEnemyFlagThenCarriesItHome()
    {
        var flags = Flags();
        Assert.Equal(new CtfBotOrder(CtfBotRole.Attacker, BlueHome, null, "attacks the enemy flag"),
            Decide(flags, Pilots()));

        // The take goes through the same decision a person's does, and the carrier flies home.
        Assert.NotNull(flags.Decide(2, FlagAsk.Take, Bot));
        var home = Decide(flags, Pilots());
        Assert.Equal(CtfBotRole.Carrier, home.Role);
        Assert.Equal(RedHome, home.Goal);
        Assert.True(home.Straight);

        // ABLE-TO-FAIL CONTROL: a bot on no team flies no rule.
        Assert.Equal(CtfBotRole.None, CtfBotObjective.Decide(Bot, flags, new[] { Pilot(Bot, 0, true) }).Role);
    }

    [Fact]
    public void ACarrierOfItsFlagIsChasedBeforeANearerEnemy()
    {
        var flags = Flags();
        var pilots = new List<CtfPilot>(Pilots()) { Pilot(Raider, 2, false, new Vector3(0, 100, -10)) };
        flags.Decide(1, FlagAsk.Take, Enemy);
        var chase = Decide(flags, pilots);
        Assert.Equal(CtfBotRole.Chaser, chase.Role);
        Assert.Same(pilots[Enemy].Source, chase.Target);
        Assert.False(chase.Straight);

        // A carrier goes home even with its own flag stolen.
        flags.Decide(2, FlagAsk.Take, Bot);
        Assert.Equal(CtfBotRole.Carrier, Decide(flags, pilots).Role);

        // ABLE-TO-FAIL CONTROL: a teammate carrying its flag home is no quarry.
        var returning = Flags();
        returning.Decide(1, FlagAsk.Take, Mate);
        Assert.Equal(CtfBotRole.Attacker, Decide(returning, pilots).Role);
    }

    [Fact]
    public void AFloatingFlagOfEitherTeamOutranksTheAttack()
    {
        foreach (int team in new[] { 1, 2 })
        {
            var flags = Flags();
            flags.Decide(team, FlagAsk.Take, team == 1 ? Enemy : Mate);
            var at = new Vector3(50, 120, -1500);
            flags.Drop(team == 1 ? Enemy : Mate, at, Vector3.Zero, 0f);
            var order = Decide(flags, Pilots());
            Assert.Equal(CtfBotRole.Retriever, order.Role);
            Assert.Equal(at + (Up * CtfBotObjective.FloatingPassAboveM), order.Goal);
        }

        // ABLE-TO-FAIL CONTROL: with both flags home the bot attacks instead.
        Assert.Equal(CtfBotRole.Attacker, Decide(Flags(), Pilots()).Role);
    }

    [Fact]
    public void OnATeamOfThreeTheBotNearestItsFlagDefends()
    {
        var flags = Flags();
        var near = new[] { Pilot(Bot, 1, true, RedHome), Pilot(Mate, 1, true, BlueHome), Pilot(4, 1, false), Pilot(Enemy, 2, false) };
        var order = Decide(flags, near);
        Assert.Equal(CtfBotRole.Defender, order.Role);
        Assert.Equal(RedHome + (Up * CtfBotObjective.DefendAboveM), order.Goal);
        Assert.False(order.Straight);
        Assert.Equal(CtfBotRole.Attacker, CtfBotObjective.Decide(Mate, flags, near).Role);

        // ABLE-TO-FAIL CONTROL: on a team of two the same bot attacks.
        Assert.Equal(CtfBotRole.Attacker, Decide(flags, new[] { near[0], near[1], near[3] }).Role);
    }

    [Fact]
    public void ADefenderStaysHomeForAFloatingFlagButChasesItsFlagsCarrier()
    {
        var team = new[] { Pilot(Bot, 1, true, RedHome), Pilot(Mate, 1, true, BlueHome), Pilot(4, 1, false), Pilot(Enemy, 2, false) };
        var floating = Flags();
        floating.Decide(2, FlagAsk.Take, Mate);
        floating.Drop(Mate, new Vector3(50, 120, -1500), Vector3.Zero, 0f);
        Assert.Equal(CtfBotRole.Defender, Decide(floating, team).Role);
        Assert.Equal(CtfBotRole.Retriever, CtfBotObjective.Decide(Mate, floating, team).Role);

        // ABLE-TO-FAIL CONTROL: on a team of two the same bot goes for the floating flag.
        Assert.Equal(CtfBotRole.Retriever, Decide(floating, new[] { team[0], team[1], team[3] }).Role);

        // A taken flag leaves nothing to defend, so the defender chases its carrier.
        var stolen = Flags();
        stolen.Decide(1, FlagAsk.Take, Enemy);
        Assert.Equal(CtfBotRole.Chaser, Decide(stolen, team).Role);
    }

    [Fact]
    public void ADefenderBreaksOffPastItsTether()
    {
        var tether = (RedHome + (Up * CtfBotObjective.DefendAboveM), CtfBotObjective.DefendRadiusM);
        Assert.True(AiPilot.BeyondTether(RedHome + new Vector3(CtfBotObjective.DefendRadiusM + 10f, 0, 0), tether));

        // ABLE-TO-FAIL CONTROL: inside the radius, at any height, it dogfights on; with no tether
        // it is never held off.
        Assert.False(AiPilot.BeyondTether(RedHome + new Vector3(CtfBotObjective.DefendRadiusM - 10f, 900, 0), tether));
        Assert.False(AiPilot.BeyondTether(RedHome + new Vector3(50000, 0, 0), null));
    }

    private static FlagMatch Flags() =>
        new(new[] { (1, RedHome), (2, BlueHome) }, seat => seat switch { Bot or Mate => 1, Enemy or Raider => 2, _ => 0 });

    private static CtfPilot Pilot(int seat, int team, bool bot, Vector3? at = null) =>
        new(seat, team, "pilot" + seat, at ?? new Vector3(0, 300, -1000), bot, true);

    private static CtfPilot[] Pilots() => new[] { Pilot(Bot, 1, true), Pilot(Mate, 1, false), Pilot(Enemy, 2, false) };

    private static CtfBotOrder Decide(FlagMatch flags, IReadOnlyList<CtfPilot> pilots) => CtfBotObjective.Decide(Bot, flags, pilots);
}
