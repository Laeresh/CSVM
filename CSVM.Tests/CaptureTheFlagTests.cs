using System;
using System.Linq;
using CSVM.Flight.Modes;
using CSVM.Net;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// Capture the Flag off-engine. <see cref="FlagMatch"/> takes an enemy flag at its base or any
/// floating flag within 25 m. A carried flag goes home at the carrier's base, and the first asker
/// wins. A downed carrier's flag floats and its throw ends at 15 s. A capture scores 5 and a return
/// 1, and the two flag messages round-trip.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class CaptureTheFlagTests
{
    private static readonly Vector3 RedHome = new(-1000f, 50f, -1000f);
    private static readonly Vector3 BlueHome = new(-3000f, 50f, -1000f);

    // Seats 0 and 1 on team 1 (red), seat 2 on team 2 (blue).
    private static readonly int[] Teams = { 1, 1, 2 };

    [Fact]
    public void AnEnemyFlagIsTakenAtItsBaseWithinReachAndOwnFlagIsNot()
    {
        var flags = Match();

        Assert.Null(flags.Check(0, RedHome));
        Assert.Null(flags.Check(2, RedHome + new Vector3(FlagMatch.Reach + 1f, 0f, 0f)));

        // ABLE-TO-FAIL CONTROL: just inside the reach the enemy asks to take it.
        Assert.Equal((1, FlagAsk.Take), flags.Check(2, RedHome + new Vector3(FlagMatch.Reach - 1f, 0f, 0f)));
    }

    [Fact]
    public void TheFirstAskerWinsAndTheCooldownHoldsTheNextAsk()
    {
        var flags = Match();
        Assert.Equal((1, FlagAsk.Take), flags.Check(2, RedHome));

        Assert.NotNull(flags.Decide(1, FlagAsk.Take, 2));
        Assert.Null(flags.Decide(1, FlagAsk.Take, 1));
        Assert.Equal(new FlagRow(1, FlagState.Held, 2), flags.RowOf(1));

        // Inside the take's cooldown the carrier at its own base asks nothing.
        Assert.Null(flags.Check(2, BlueHome));
        flags.Advance(FlagMatch.HomeTakeCooldown - 0.1f);
        Assert.Null(flags.Check(2, BlueHome));

        // ABLE-TO-FAIL CONTROL: once it runs out the same place asks to bring the flag home.
        flags.Advance(0.2f);
        Assert.Equal((1, FlagAsk.Home), flags.Check(2, BlueHome));
    }

    [Fact]
    public void ACaptureScoresFiveAndAReturnScoresOneAndAFloatingReturnScoresNothing()
    {
        var flags = Match();
        flags.Decide(1, FlagAsk.Take, 2);
        var capture = flags.Decide(1, FlagAsk.Home, 2)!.Value;
        Assert.Equal(FlagMatch.CaptureScore, FlagMatch.Points(capture, TeamOf));

        flags.Decide(1, FlagAsk.Take, 2);
        flags.Drop(2, RedHome, Vector3.Up, RedHome.Y);
        var caught = flags.Decide(1, FlagAsk.Take, 0)!.Value;
        Assert.Equal(FlagState.Floating, caught.From);
        Assert.Equal(0, FlagMatch.Points(caught, TeamOf));
        var returned = flags.Decide(1, FlagAsk.Home, 0)!.Value;
        Assert.Equal(FlagMatch.ReturnScore, FlagMatch.Points(returned, TeamOf));

        flags.Decide(1, FlagAsk.Take, 2);
        flags.Drop(2, RedHome, Vector3.Up, RedHome.Y);
        var floated = flags.Decide(1, FlagAsk.Home, FlagMatch.NoHolder)!.Value;
        Assert.Equal(0, FlagMatch.Points(floated, TeamOf));
    }

    [Fact]
    public void ADroppedFlagFloatsAndIsCaughtByEitherTeamUntilItsThrowRunsOut()
    {
        var flags = Match();
        flags.Decide(1, FlagAsk.Take, 2);
        Assert.Null(flags.Drop(0, RedHome, Vector3.Up, 0f));
        var drop = flags.Drop(2, RedHome, new Vector3(0f, 30f, 0f), RedHome.Y);
        Assert.Equal(new FlagChange(1, FlagState.Held, 2, FlagState.Floating, FlagMatch.NoHolder), drop);

        // The flag's own team can catch it, where it now is.
        var at = flags.FloatingAt(1)!.Value;
        Assert.Equal((1, FlagAsk.Take), flags.Check(1, at));

        var ended = Enumerable.Range(0, 1000).SelectMany(_ => flags.Advance(1f / 60f)).ToList();
        Assert.Equal(new[] { 1 }, ended);
        Assert.True(flags.FloatingAt(1)!.Value.Y >= RedHome.Y, $"the flag rests on its floor ({flags.FloatingAt(1)})");
    }

    [Fact]
    public void TheHostsOptionHoldsACaptureWhileTheCarriersOwnFlagIsAway()
    {
        var flags = Match(ownFlagHome: true);
        flags.Decide(1, FlagAsk.Take, 2);
        flags.Decide(2, FlagAsk.Take, 0);
        flags.Advance(FlagMatch.HomeTakeCooldown + 1f);

        Assert.Null(flags.Check(2, BlueHome));
        Assert.Null(flags.Decide(1, FlagAsk.Home, 2));

        // ABLE-TO-FAIL CONTROL: without the option the original captures whatever its own flag does.
        var original = Match();
        original.Decide(1, FlagAsk.Take, 2);
        original.Decide(2, FlagAsk.Take, 0);
        original.Advance(FlagMatch.HomeTakeCooldown + 1f);
        Assert.Equal((1, FlagAsk.Home), original.Check(2, BlueHome));
    }

    [Fact]
    public void AGuestTakesAheadAndTheHostsTableCorrectsIt()
    {
        var guest = Match();
        Assert.NotNull(guest.TakeAhead(1, 2));

        var changes = guest.Apply(new[] { new FlagRow(1, FlagState.Held, 1), new FlagRow(2, FlagState.Floating, FlagMatch.NoHolder) });

        Assert.Equal(new[] { new FlagChange(1, FlagState.Held, 2, FlagState.Held, 1) }, changes);
        Assert.Equal(FlagState.Home, guest.RowOf(2)!.Value.State);
    }

    [Fact]
    public void TheFlagMessagesRoundTrip()
    {
        var bytes = new byte[FlagTableMessage.Size];
        var table = new FlagTableMessage(new[] { new NetFlagRow(1, 1, 2), new NetFlagRow(2, 2, NetMessage.NoSeat) });
        Assert.Equal(FlagTableMessage.Size, table.Write(bytes));
        Assert.True(FlagTableMessage.TryRead(bytes, out var read));
        Assert.Equal(table, read);
        Assert.False(FlagTableMessage.TryRead(bytes.AsSpan(0, FlagTableMessage.Size - 1), out _));

        var ask = new byte[FlagRequestMessage.Size];
        Assert.Equal(FlagRequestMessage.Size, new FlagRequestMessage(1, 2, 3).Write(ask));
        Assert.True(FlagRequestMessage.TryRead(ask, out var asked));
        Assert.Equal(new FlagRequestMessage(1, 2, 3), asked);
        Assert.Equal(0x63, (int)NetMessageType.FlagRequest);
        Assert.Equal(0x64, (int)NetMessageType.FlagTable);
        Assert.Equal(NetReliability.Reliable, NetMessage.ReliabilityOf(NetMessageType.FlagTable));

        var options = new DogfightOptionsMessage(3, 1, 0, DogfightVictory.Both, 10, 40, false, 3, true, true, 2, 2, true);
        var wire = new byte[DogfightOptionsMessage.Size];
        options.Write(wire);
        Assert.True(DogfightOptionsMessage.TryRead(wire, out var heard));
        Assert.Equal(options, heard);
    }

    [Fact]
    public void AFlagBroughtHomeAddsToTheMatchAndCanEndItOnTheTeamTotal()
    {
        var match = new VersusMatch(3, killTarget: 6, timeLimit: 0f);
        match.AssignTeams(Teams);
        match.RegisterKill(0, 2);
        Assert.False(match.Completed);

        match.AddScore(1, FlagMatch.CaptureScore);

        Assert.Equal(FlagMatch.CaptureScore, match.ScoreOf(1));
        Assert.Equal(0, match.KillsOf(1));
        Assert.True(match.Completed);
    }

    private static int TeamOf(int seat) => seat >= 0 && seat < Teams.Length ? Teams[seat] : 0;

    private static FlagMatch Match(bool ownFlagHome = false) =>
        new(new[] { (1, RedHome), (2, BlueHome) }, TeamOf, ownFlagHome);
}
