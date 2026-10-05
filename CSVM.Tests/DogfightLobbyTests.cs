using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using CSVM.Net;
using CSVM.Spec;
using CSVM.UI.Menu;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The Dogfight lobby over a loopback mesh. The host owns the options and a greyed option cannot be
/// set. Launch waits for every pilot, and a pilot who leaves drops out of the count. A guest's pick
/// and chat reach the host.
/// </summary>
[Trait("Tier", "Quick")]
public sealed class DogfightLobbyTests
{
    private static readonly LoopbackConditions Clean = new(0.0, 0.0, 0.0);

    // Each host lobby's sockets, the host's first, so a settle steps every end.
    private static readonly Dictionary<DogfightLobby, List<NetLobby>> Steps = new();

    [Fact]
    public void AGuestsOptionSetIsRefusedAndItReadsTheHostsOptions()
    {
        var (host, guests, _) = Lobbies(2);
        var guest = guests[0];

        Assert.False(guest.SetEnvironment(3));
        Assert.False(guest.SetTimeMinutes(5));
        Assert.False(guest.SetLimitedLives(true));

        // ABLE-TO-FAIL CONTROL: the same calls on the host take, and the guest reads them.
        Assert.True(host.SetEnvironment(3));
        Assert.True(host.SetTimeMinutes(5));
        Assert.True(host.SetLimitedLives(true));
        Settle(host, guests);
        Assert.True(guest.HasOptions);
        Assert.Equal(3, guest.Options.Environment);
        Assert.Equal(5, guest.Options.TimeMinutes);
        Assert.True(guest.Options.LimitedLives);
    }

    [Fact]
    public void AGreyedOptionCannotBeSet()
    {
        var (host, _, _) = Lobbies(1);

        Assert.False(host.SetMissionType((DogfightMissionType)DogfightLobby.TypeCount));
        Assert.False(host.SetOutlawed(NetPlaneRules.Flags, true));
        Assert.Equal((byte)DogfightMissionType.Deathmatch, host.Options.MissionType);

        // ABLE-TO-FAIL CONTROL: the live type is accepted.
        Assert.True(host.SetMissionType(DogfightMissionType.Deathmatch));
    }

    [Fact]
    public void LaunchIsRefusedUntilEveryPilotIsReadyAndAnOptionChangeClearsTheMarks()
    {
        var (host, guests, _) = Lobbies(3);
        Settle(host, guests);
        Assert.False(host.CanLaunch);

        host.SetReady(true);
        guests[0].SetReady(true);
        Settle(host, guests);
        Assert.False(host.CanLaunch);
        Assert.Equal(new[] { true, true, false }, host.Players.Select(p => p.Ready));

        // ABLE-TO-FAIL CONTROL: the last mark lets the host launch, and a guest never can.
        guests[1].SetReady(true);
        Settle(host, guests);
        Assert.True(host.CanLaunch);
        Assert.False(guests[0].CanLaunch);
        Assert.True(guests[0].AllReady);

        // A new round from the host clears every mark, its own included.
        Assert.True(host.SetScore(12));
        Settle(host, guests);
        Assert.False(host.Ready);
        Assert.False(guests[0].Ready);
        Assert.False(host.CanLaunch);
    }

    [Fact]
    public void ADisconnectDropsThePeerOutOfTheCount()
    {
        var (host, guests, mesh) = Lobbies(3);
        Settle(host, guests);
        host.SetReady(true);
        guests[0].SetReady(true);
        Settle(host, guests);
        Assert.Equal(3, host.Players.Count);
        Assert.False(host.CanLaunch);

        mesh[0].Disconnect(2);
        Settle(host, guests);

        // The unready pilot left, so the two still here are every pilot.
        Assert.Equal(2, host.Players.Count);
        Assert.Equal(2, guests[0].Players.Count);
        Assert.True(host.CanLaunch);
    }

    [Fact]
    public void AGuestsPickReachesTheHostsListUnderItsName()
    {
        var (host, guests, _) = Lobbies(2, "Lucy");
        var fit = CoopFit.Of(new[] { 2, 1 }, new[] { 0, 3 });
        Assert.True(guests[0].Pick(1, fit));
        Assert.False(guests[0].Pick(DogfightLobby.AirframeCount, default));
        Settle(host, guests);

        Assert.Equal("Lucy", host.Players[1].Name);
        Assert.Equal(1, host.Players[1].Airframe);
        Assert.Equal(1, guests[0].You);
        Assert.Equal("Lucy", guests[0].Players[guests[0].You].Name);
    }

    [Fact]
    public void AReadyGuestsChangedPickReachesTheHostAndClearsItsReady()
    {
        var (host, guests, _) = Lobbies(2);
        Settle(host, guests);
        guests[0].SetReady(true);
        Settle(host, guests);
        Assert.True(host.Players[1].Ready);

        // Control: the same pick again is no change, so the guest stays Ready on both ends.
        Assert.True(guests[0].Pick(guests[0].Airframe, guests[0].Fit));
        Settle(host, guests);
        Assert.True(guests[0].Ready);
        Assert.True(host.Players[1].Ready);

        int other = (guests[0].Airframe + 1) % DogfightLobby.AirframeCount;
        Assert.True(guests[0].Pick(other, default));
        Settle(host, guests);

        Assert.False(guests[0].Ready);
        Assert.False(host.Players[1].Ready);
        Assert.Equal(other, host.Players[1].Airframe);
    }

    [Fact]
    public void AGuestNoLobbyScreenShowsSendsNoPick()
    {
        var mesh = LoopbackTransport.Mesh(2, Clean, new Random(7));
        var hostWire = new NetLobby(mesh[0]);
        var guestWire = new NetLobby(mesh[1]);
        var host = new DogfightLobby(hostWire, () => "Host");
        var guest = new DogfightLobby(guestWire, () => "Lucy", 0);
        Steps[host] = new List<NetLobby> { hostWire, guestWire };
        var guests = new List<DogfightLobby> { guest };
        guest.Pick(3, default);
        Settle(host, guests);
        Assert.Empty(hostWire.Picks);

        // ABLE-TO-FAIL CONTROL: once a lobby screen stands on it, the same guest's pick arrives.
        guest.Show();
        Settle(host, guests);
        Assert.Equal(3, host.Players[1].Airframe);
        Assert.Equal("Lucy", host.Players[1].Name);
    }

    [Fact]
    public void ABuiltInLaunchWaitsForAPickedGuestsReadyAndWritesItsMapAndRules()
    {
        var (host, guests, _) = Lobbies(2);
        Settle(host, guests);
        var rules = new VersusRules(0, 7, 2, false);

        // ABLE-TO-FAIL CONTROL: with no picked guest an unlisted map still goes, as it always has.
        var (alone, _, _) = Lobbies(1);
        Assert.Null(alone.CheckBuiltInLaunch("C2B", rules));

        Assert.Equal(DogfightLobby.GuestsNotReady, host.CheckBuiltInLaunch("C5", rules));
        guests[0].SetReady(true);
        Settle(host, guests);
        Assert.Equal(DogfightLobby.MapUnlisted, host.CheckBuiltInLaunch("C2B", rules));
        Assert.Null(host.CheckBuiltInLaunch("C5", rules));
        Settle(host, guests);

        var heard = guests[0].Options;
        Assert.Equal(DogfightLobby.EnvironmentOf("C5"), heard.Environment);
        Assert.Equal(DogfightVictory.Time, heard.Victory);
        Assert.Equal(7, heard.TimeMinutes);
        Assert.True(heard.LimitedLives);
        Assert.Equal(2, heard.Lives);
        Assert.False(heard.AutoRespawn);
        Assert.True(guests[0].Ready);
    }

    [Fact]
    public void ANewLobbyRefusesACustomPlaneAtReadyUntilTheHostAllowsThem()
    {
        var (host, guests, _) = Lobbies(2);
        var guest = guests[0];
        var build = new NetPlaneBuild { Name = "Bee", Airframe = 2, Engine = 1 };
        Assert.True(guest.PickCustom(build, default));
        Settle(host, guests);

        Assert.Equal(PlaneRefusal.CustomBarred, guest.Refusal);
        Assert.False(guest.SetReady(true));
        Assert.False(guest.Ready);
        Assert.False(guests[0].SetAllowCustomPlanes(true));

        Assert.True(host.SetAllowCustomPlanes(true));
        Settle(host, guests);
        Assert.True(guest.Rules.AllowCustom);
        Assert.True(guest.SetReady(true));
        Settle(host, guests);
        Assert.True(host.Players[1].Ready);
        Assert.Equal(build, ((NetLobby)Steps[host][0]).PickBuilds[1]);
    }

    [Fact]
    public void AShownHostOpensWithCustomPlanesAllowedAndAnUnshownOneDoesNot()
    {
        var (host, guests, _) = Lobbies(2);
        Assert.False(host.Rules.AllowCustom);

        host.Show();
        Settle(host, guests);
        Assert.True(host.Rules.AllowCustom);
        Assert.True(guests[0].Rules.AllowCustom);

        // ABLE-TO-FAIL CONTROL: a later showing does not tick the box the host cleared.
        Assert.True(host.SetAllowCustomPlanes(false));
        host.Show();
        Assert.False(host.Rules.AllowCustom);
    }

    [Fact]
    public void ARulesChangeClearsEveryReadyAndAnOutlawedPartIsRefused()
    {
        var (host, guests, _) = Lobbies(2);
        var guest = guests[0];
        host.SetAllowCustomPlanes(true);
        var build = new NetPlaneBuild { Name = "Bee", Airframe = 2, Engine = 4 };
        build.Guns[0] = 1;
        guest.PickCustom(build, default);
        Settle(host, guests);
        Assert.True(guest.SetReady(true));
        Settle(host, guests);
        Assert.True(host.Players[1].Ready);

        // Outlawing nitro with the list not in force changes nothing a pick is judged on.
        Assert.True(host.SetOutlawed(NetPlaneRules.NitroFlag, true));
        Settle(host, guests);
        Assert.Equal(PlaneRefusal.None, guest.Refusal);
        Assert.False(host.Players[1].Ready);

        Assert.True(host.SetOutlawComponents(true));
        Assert.True(host.SetOutlawed(NetPlaneRules.NitroFlag, true));
        Settle(host, guests);
        Assert.Equal(PlaneRefusal.Engine, guest.Refusal);
        Assert.False(guest.SetReady(true));

        // ABLE-TO-FAIL CONTROL: a stock engine under the same list is admitted.
        build.Engine = NetPlaneBuild.StockEngine;
        guest.PickCustom(build, default);
        Assert.Equal(PlaneRefusal.None, guest.Refusal);
        host.SetOutlawed(NetPlaneRules.GunFlag + 1, true);
        Settle(host, guests);
        Assert.Equal(PlaneRefusal.Gun, guest.Refusal);
    }

    [Fact]
    public void AnOutlawedAmmunitionRefusesReadyOnceAndResetsEveryGunToNone()
    {
        var (host, guests, _) = Lobbies(2);
        var guest = guests[0];
        var fit = CoopFit.Of(new[] { 3, 0 }, new[] { 5 });
        guest.Pick(1, fit);
        host.SetOutlawed(NetPlaneRules.AmmoFlag + 3, true);
        Settle(host, guests);
        Assert.Equal(fit, guest.LaunchFit);

        host.SetOutlawComponents(true);
        host.SetOutlawed(NetPlaneRules.AmmoFlag + 3, true);
        Settle(host, guests);
        Assert.Equal(NetPlaneRules.NoAmmo, guest.LaunchFit.AmmoAt(1));
        Assert.False(guest.SetReady(true));
        Assert.Equal(new[] { PlaneRefusal.Ammo }, guest.ReadyRefusals);
        Assert.Equal(NetPlaneRules.NoAmmo, guest.Fit.AmmoAt(0));
        Assert.Equal(5, guest.Fit.OrdnanceAt(0));

        // ABLE-TO-FAIL CONTROL: the reset fit is Ready at the second press, and the host sees it.
        Assert.True(guest.SetReady(true));
        Assert.Empty(guest.ReadyRefusals);
        Settle(host, guests);
        Assert.True(host.Players[1].Ready);
    }

    [Fact]
    public void TogglingOutlawComponentsEitherWayEmptiesTheListInOneRound()
    {
        RulesCounter? counter = null;
        var (host, guests, _) = Lobbies(2, hostCarrier: inner => counter = new RulesCounter(inner));
        var guest = guests[0];
        host.SetOutlawed(NetPlaneRules.AirframeFlag + 4, true);
        host.SetOutlawed(NetPlaneRules.NitroFlag, true);
        host.SetOutlawed(NetPlaneRules.AllAmmoFlag, true);
        Settle(host, guests);
        Assert.True(host.SetReady(true));
        Assert.True(guest.SetReady(true));
        Settle(host, guests);
        Assert.All(host.Players, p => Assert.True(p.Ready));
        int epoch = host.Options.Epoch;
        int sent = counter!.Sent;

        Assert.True(host.SetOutlawComponents(true));
        Settle(host, guests);
        Assert.Equal(new NetPlaneRules(false, true, 0), host.Rules);
        Assert.Equal(host.Rules, guest.Rules);
        Assert.Equal(epoch + 1, host.Options.Epoch);
        Assert.Equal(sent + 1, counter.Sent);
        Assert.False(host.Ready);
        Assert.False(guest.Ready);
        Assert.All(host.Players, p => Assert.False(p.Ready));

        // Clearing the tick empties a list set under it too.
        host.SetOutlawed(NetPlaneRules.GunFlag + 2, true);
        host.SetOutlawed(NetPlaneRules.RocketFlag + 5, true);
        Settle(host, guests);
        epoch = host.Options.Epoch;
        sent = counter.Sent;
        Assert.True(host.SetOutlawComponents(false));
        Settle(host, guests);
        Assert.Equal(default(NetPlaneRules), guest.Rules);
        Assert.Equal(epoch + 1, guest.Options.Epoch);
        Assert.Equal(sent + 1, counter.Sent);

        // ABLE-TO-FAIL CONTROL: setting the tick it already holds keeps the list and the round.
        host.SetOutlawed(NetPlaneRules.NitroFlag, true);
        epoch = host.Options.Epoch;
        Assert.True(host.SetOutlawComponents(false));
        Assert.True(host.Rules.Has(NetPlaneRules.NitroFlag));
        Assert.Equal(epoch, host.Options.Epoch);
    }

    [Fact]
    public void OutlawAllRocketsResetsThePylonsWithoutRefusingReady()
    {
        var (host, guests, _) = Lobbies(2);
        guests[0].Pick(1, CoopFit.Of(null, new[] { 5, 3 }));
        host.SetOutlawComponents(true);
        host.SetOutlawed(NetPlaneRules.AllRocketsFlag, true);
        Settle(host, guests);

        Assert.True(guests[0].SetReady(true));
        Assert.Equal(NetPlaneRules.NoRocket + 1, guests[0].Fit.OrdnanceAt(1));
    }

    [Fact]
    public void TheScoresNameEachSeatOffTheLaunchListBestFirst()
    {
        var match = new CSVM.Flight.Modes.VersusMatch(3, killTarget: 0, timeLimit: 60f);
        match.RegisterKill(2, 0);
        match.RegisterKill(2, 1);
        match.RegisterKill(1, 0);
        var names = new[] { "Zachary", "Nathan" };

        var scores = DogfightLobby.ScoresOf(match.Standings(), names);

        Assert.Equal(new[] { "P3", "Nathan", "Zachary" }, scores.Select(s => s.Name));
        Assert.Equal(new DogfightScore("P3", 2, 2, 0), scores[0]);
        Assert.Equal(2, scores[2].Deaths);
    }

    [Fact]
    public void LandingShowsTheScoresAndOpensANewRoundThatClearsEveryReady()
    {
        var (host, guests, _) = Lobbies(2);
        Settle(host, guests);
        host.SetReady(true);
        guests[0].SetReady(true);
        Settle(host, guests);
        Assert.True(host.CanLaunch);
        Assert.Empty(host.Scores);

        var scores = new[] { new DogfightScore("Host", 3, 3, 1) };
        host.Land(scores);
        guests[0].Land(scores);
        Settle(host, guests);

        Assert.Equal(scores, host.Scores);
        Assert.Equal(scores, guests[0].Scores);
        Assert.False(host.Ready);
        Assert.False(guests[0].Ready);
        Assert.Equal(new[] { false, false }, host.Players.Select(p => p.Ready));

        // ABLE-TO-FAIL CONTROL: both mark Ready under the new round and the host can launch again.
        host.SetReady(true);
        guests[0].SetReady(true);
        Settle(host, guests);
        Assert.True(host.CanLaunch);
    }

    [Fact]
    public void ARaceLandsItsTableOnGameScoresAndTheNextMatchReplacesIt()
    {
        var (host, _, _) = Lobbies(1);
        Assert.False(host.HasScores);
        var table = new[]
        {
            new RaceTableRow("1st  Blue", "Kestrel", "0:41.2", "", "2/3", Left: true),
            new RaceTableRow("2nd  Red", "Bloodhawk", "0:44.0", "+2.8", "1/1"),
        };
        host.Land(Array.Empty<DogfightScore>(), table);
        Assert.True(host.HasScores);
        Assert.Empty(host.Scores);
        Assert.Equal(table, host.RaceScores);
        Assert.False(host.Ready);

        // ABLE-TO-FAIL CONTROL: a Dogfight landed after it clears the race's table.
        host.Land(new[] { new DogfightScore("Host", 1, 1, 0) });
        Assert.Empty(host.RaceScores);
        Assert.True(host.HasScores);
    }

    [Fact]
    public void OneChatLineArrivesOnceOnEveryEnd()
    {
        var (host, guests, _) = Lobbies(3);
        Settle(host, guests);
        Assert.True(guests[0].Say("hello"));
        Settle(host, guests);
        Settle(host, guests);

        Assert.Single(host.Chat);
        Assert.Single(guests[0].Chat);
        Assert.Single(guests[1].Chat);
        Assert.Equal("hello", guests[1].Chat[0].Text);

        // ABLE-TO-FAIL CONTROL: an empty or overlong line is refused.
        Assert.False(host.Say("   "));
        Assert.False(host.Say(new string('x', LobbyChatMessage.MaxChars + 1)));
    }

    [Fact]
    public void TheRulesCarryOnlyTheChosenVictoryAndTheLivesWhenLimited()
    {
        var options = new DogfightOptionsMessage(1, 2, 1, DogfightVictory.Time, 5, 40, true, 2, false);
        var rules = DogfightLobby.RulesOf(options);
        Assert.Equal(0, rules.KillTarget);
        Assert.Equal(5, rules.TimeLimitMinutes);
        Assert.Equal(2, rules.Lives);
        Assert.False(rules.AutoRespawn);

        var scored = DogfightLobby.RulesOf(options with { Victory = DogfightVictory.Score, LimitedLives = false });
        Assert.Equal(40, scored.KillTarget);
        Assert.Equal(0, scored.TimeLimitMinutes);
        Assert.Equal(0, scored.Lives);
    }

    [Fact]
    public void TheLivesBoxIsGreyedUntilLimitedAndClampsToOneToNinetyNine()
    {
        var (host, _, _) = Lobbies(1);
        Assert.False(host.Options.LimitedLives);
        Assert.True(host.Options.AutoRespawn);
        Assert.False(host.SetLives(5));

        Assert.True(host.SetLimitedLives(true));
        Assert.Equal(DogfightLobby.DefaultLives, host.Options.Lives);
        Assert.True(host.SetLives(0));
        Assert.Equal(1, host.Options.Lives);
        Assert.True(host.SetLives(250));
        Assert.Equal(DogfightLobby.MaxLives, host.Options.Lives);
        Assert.Equal(1, DogfightLobby.RulesOf(host.Options with { Lives = 0 }).Lives);

        // ABLE-TO-FAIL CONTROL: a count inside the range is taken as typed.
        Assert.True(host.SetLives(7));
        Assert.Equal(7, host.Options.Lives);
    }

    [Fact]
    public void BothLimitsArmTogetherAndTheLastOneCannotBeCleared()
    {
        var options = new DogfightOptionsMessage(1, 2, 1, DogfightVictory.Both, 5, 40, false, 3, true);
        var rules = DogfightLobby.RulesOf(options);
        Assert.Equal(40, rules.KillTarget);
        Assert.Equal(5, rules.TimeLimitMinutes);

        Assert.Equal(DogfightVictory.Both, DogfightLobby.Toggled(DogfightVictory.Time, DogfightVictory.Score));
        Assert.Equal(DogfightVictory.Score, DogfightLobby.Toggled(DogfightVictory.Both, DogfightVictory.Time));
        Assert.Equal(DogfightVictory.Time, DogfightLobby.Toggled(DogfightVictory.Both, DogfightVictory.Score));

        // ABLE-TO-FAIL CONTROL: a press on the only armed limit leaves it armed.
        Assert.Equal(DogfightVictory.Time, DogfightLobby.Toggled(DogfightVictory.Time, DogfightVictory.Time));
        Assert.Equal(DogfightVictory.Score, DogfightLobby.Toggled(DogfightVictory.Score, DogfightVictory.Score));
    }

    [Fact]
    public void GuestsFormTeamsThroughTheHostAndEveryEndReadsThem()
    {
        var (host, guests, _) = Lobbies(3, "Lucy");
        Settle(host, guests);

        Assert.True(guests[0].CreateTeam("Red Skulls"));
        Settle(host, guests);
        var teams = guests[1].Teams;
        Assert.Single(teams);
        Assert.Equal("Red Skulls", teams[0].Name);
        Assert.Equal(teams[0].Number, guests[0].OwnTeam);
        Assert.True(host.Players[1].Captain);

        Assert.True(guests[1].JoinTeam(teams[0].Number));
        Assert.True(host.CreateTeam("Blue"));
        Settle(host, guests);
        Assert.Equal(teams[0].Number, guests[1].OwnTeam);
        Assert.Equal(2, host.Teams.Count);
        Assert.Equal(host.OwnTeam, guests[0].Players[0].Team);
        Assert.Contains(guests[0].Chat, line => line.Text == "[Lucy joined team Red Skulls.]");

        // ABLE-TO-FAIL CONTROL: a pilot on a team can neither create nor join another.
        Assert.False(guests[1].CreateTeam("Other"));
        Assert.False(guests[1].JoinTeam(host.OwnTeam));
    }

    [Fact]
    public void ACaptainsLeaveDisbandsItsTeamOnEveryEnd()
    {
        var (host, guests, _) = Lobbies(3, "Lucy");
        Settle(host, guests);
        guests[0].CreateTeam("Aces");
        Settle(host, guests);
        guests[1].JoinTeam(guests[0].OwnTeam);
        Settle(host, guests);
        Assert.Equal(2, host.Players.Count(p => p.Team != 0));

        Assert.True(guests[0].LeaveTeam());
        Settle(host, guests);
        Assert.Empty(guests[1].Teams);
        Assert.All(host.Players, p => Assert.Equal(0, p.Team));
        Assert.Contains(host.Chat, line => line.Text == "[Aces disbanded.]");
    }

    [Fact]
    public void ACaptainWhoLeavesTheGameTakesItsTeamWithIt()
    {
        var (host, guests, mesh) = Lobbies(3);
        Settle(host, guests);
        guests[1].CreateTeam("Gone");
        Settle(host, guests);
        guests[0].JoinTeam(guests[1].OwnTeam);
        Settle(host, guests);
        Assert.Single(host.Teams);

        mesh[0].Disconnect(2);
        Settle(host, guests);
        Assert.Empty(host.Teams);
        Assert.Equal(0, guests[0].OwnTeam);
    }

    [Fact]
    public void AReadyPilotCannotChangeItsTeam()
    {
        var (host, guests, _) = Lobbies(2);
        Settle(host, guests);
        host.SetReady(true);
        Assert.False(host.CreateTeam("Late"));
        host.SetReady(false);
        Assert.True(host.CreateTeam("Early"));
        host.SetReady(true);
        Assert.False(host.LeaveTeam());
    }

    [Fact]
    public void TheLaunchIsRefusedOnUnevenTeamsAndOnRestrict()
    {
        var (host, guests, _) = Lobbies(4);
        Settle(host, guests);
        Assert.Equal(TeamLaunchRefusal.None, host.LaunchRefusal);

        host.CreateTeam("Home");
        guests[0].CreateTeam("Away");
        Settle(host, guests);
        guests[1].JoinTeam(host.OwnTeam);
        guests[2].JoinTeam(host.OwnTeam);
        Settle(host, guests);
        Assert.Equal(TeamLaunchRefusal.Unbalanced, host.LaunchRefusal);

        guests[2].LeaveTeam();
        Settle(host, guests);
        Assert.Equal(TeamLaunchRefusal.Teamless, host.LaunchRefusal);

        guests[2].JoinTeam(guests[0].OwnTeam);
        Settle(host, guests);
        Assert.Equal(TeamLaunchRefusal.None, host.LaunchRefusal);

        // Restrict Number of Teams to three or more refuses two teams.
        Assert.False(host.SetMinTeams(3));
        Assert.True(host.SetRestrictTeams(true));
        Assert.True(host.SetMinTeams(3));
        Settle(host, guests);
        Assert.Equal(TeamLaunchRefusal.TooFewTeams, host.LaunchRefusal);
        Assert.Equal(3, guests[0].Options.MinTeams);
        Assert.True(guests[0].Options.RestrictTeams);
    }

    [Fact]
    public void TheTeamCountBoxesHoldTheMinimumAtOrBelowTheMaximum()
    {
        var (host, _, _) = Lobbies(1);
        Assert.True(host.SetRestrictTeams(true));
        Assert.Equal(DogfightOptionsMessage.DefaultMinTeams, host.Options.MinTeams);
        Assert.Equal(DogfightOptionsMessage.DefaultMaxTeams, host.Options.MaxTeams);
        Assert.True(host.SetMinTeams(9));
        Assert.Equal(host.Options.MaxTeams, host.Options.MinTeams);
        Assert.True(host.SetMaxTeams(1));
        Assert.Equal(host.Options.MinTeams, host.Options.MaxTeams);
        Assert.True(host.SetMaxTeams(99));
        Assert.Equal(DogfightLobby.MaxTeams, host.Options.MaxTeams);
    }

    [Fact]
    public void CaptureTheFlagFixesTwoTeamsAndGreysTheTwoEnvironmentsWithNoFlags()
    {
        var (host, guests, _) = Lobbies(2);
        Assert.True(host.SetEnvironment(0));

        Assert.True(host.SetMissionType(DogfightMissionType.CaptureTheFlag));
        Settle(host, guests);

        // Above the Clouds has no MP2 map, so the pick moves to the first environment with one.
        Assert.Equal(1, host.Options.Environment);
        Assert.False(host.SetEnvironment(0));
        Assert.False(host.SetEnvironment(5));
        Assert.True(host.SetEnvironment(4));
        Assert.True(host.Options.RestrictTeams);
        Assert.Equal(DogfightLobby.CtfTeams, host.Options.MinTeams);
        Assert.Equal(DogfightLobby.CtfTeams, host.Options.MaxTeams);
        Assert.False(host.SetRestrictTeams(false));
        Assert.False(host.SetMaxTeams(4));
        Assert.True(host.SetFlagHomeToCapture(true));
        Settle(host, guests);
        Assert.True(guests[0].Options.FlagHomeToCapture);
        var rules = DogfightLobby.RulesOf(guests[0].Options);
        Assert.Equal(DogfightMissionType.CaptureTheFlag, rules.MissionType);
        Assert.True(rules.FlagHomeToCapture);

        // ABLE-TO-FAIL CONTROL: back on Deathmatch every environment and the team boxes are live.
        Assert.True(host.SetMissionType(DogfightMissionType.Deathmatch));
        Assert.True(host.SetEnvironment(0));
        Assert.True(host.SetMaxTeams(4));
        Assert.False(host.SetFlagHomeToCapture(false));
        Assert.NotEqual(DogfightMissionType.CaptureTheFlag, DogfightLobby.RulesOf(host.Options).MissionType);
    }

    /// <summary>Each Environment row flies the world its row's number names (0x413c08). Above the
    /// Clouds is C1C, not Instant Action's C2B.</summary>
    [Fact]
    public void EachEnvironmentFliesTheChapterWhoseWorldNumberItsRowWrites()
    {
        Assert.Equal(
            new[] { "C1C", "C3", "C2", "C5", "C1", "C1B", "C4" },
            Enumerable.Range(0, DogfightLobby.EnvironmentCount).Select(DogfightLobby.ChapterOf));
        Assert.Equal(0, DogfightLobby.EnvironmentOf("C1C"));
        Assert.Equal(-1, DogfightLobby.EnvironmentOf("C2B"));
    }

    /// <summary>Every row's chapter ships MP1 for a Deathmatch and MP3 for Zeppelin vs Zeppelin. It
    /// ships MP2 exactly where the Type box offers the row to Capture the Flag.</summary>
    [ExtractedDataFact]
    public void EveryEnvironmentShipsTheMapsOfTheTypesItOffers()
    {
        static bool Ships(string chapter, string mission)
        {
            string path = CSVM.Extraction.SessionPaths.MissionZrdr(TestData.DataRoot!, chapter, mission);
            return System.IO.File.Exists(path) || System.IO.Directory.Exists(path);
        }

        for (int environment = 0; environment < DogfightLobby.EnvironmentCount; environment++)
        {
            string chapter = DogfightLobby.ChapterOf(environment);
            Assert.True(Ships(chapter, "MP1"), $"{chapter} ships MP1");
            Assert.True(Ships(chapter, "MP3"), $"{chapter} ships MP3");
            Assert.Equal(DogfightLobby.Offers(DogfightMissionType.CaptureTheFlag, environment), Ships(chapter, "MP2"));
        }
    }

    [Fact]
    public void CaptureTheFlagRefusesATeamNumberedPastTheTwoFlags()
    {
        var (host, guests, _) = Lobbies(3);
        Assert.True(host.SetMissionType(DogfightMissionType.CaptureTheFlag));
        host.CreateTeam("One");
        Settle(host, guests);
        guests[0].CreateTeam("Two");
        Settle(host, guests);
        guests[1].CreateTeam("Three");
        Settle(host, guests);
        Assert.Equal(TeamLaunchRefusal.TooManyTeams, host.LaunchRefusal);

        // Team 1 disbands, which leaves two teams standing, numbered 2 and 3.
        host.LeaveTeam();
        Settle(host, guests);
        host.JoinTeam(guests[0].OwnTeam);
        Settle(host, guests);
        Assert.Equal(new byte[] { 2, 2, 3 }, host.Players.Select(p => p.Team).OrderBy(t => t));
        Assert.Equal(TeamLaunchRefusal.TooFewTeams, host.LaunchRefusal);

        // ABLE-TO-FAIL CONTROL: the same two teams launch a Deathmatch.
        Assert.True(host.SetMissionType(DogfightMissionType.Deathmatch));
        Assert.True(host.SetRestrictTeams(false));
        Assert.Equal(TeamLaunchRefusal.None, host.LaunchRefusal);
    }

    [Fact]
    public void ZeppelinVsZeppelinFixesTwoTeamsOnEveryEnvironmentAndLaunchesAnyTwoNumbers()
    {
        var (host, guests, _) = Lobbies(3);
        Assert.True(host.SetEnvironment(0));

        Assert.True(host.SetMissionType(DogfightMissionType.ZeppelinVsZeppelin));
        Settle(host, guests);

        // Every chapter ships MP3, so no environment is greyed.
        Assert.Equal(0, host.Options.Environment);
        Assert.True(host.SetEnvironment(5));
        Assert.True(host.Options.RestrictTeams);
        Assert.Equal(DogfightLobby.CtfTeams, host.Options.MinTeams);
        Assert.Equal(DogfightLobby.CtfTeams, host.Options.MaxTeams);
        Assert.False(host.SetRestrictTeams(false));
        Assert.False(host.SetMinTeams(1));
        Assert.False(host.SetFlagHomeToCapture(true));
        var rules = DogfightLobby.RulesOf(guests[0].Options);
        Assert.Equal(DogfightMissionType.ZeppelinVsZeppelin, rules.MissionType);

        // Two teams numbered 2 and 3 launch: the sides follow lobby order, not the numbers.
        host.CreateTeam("One");
        Settle(host, guests);
        guests[0].CreateTeam("Two");
        Settle(host, guests);
        guests[1].CreateTeam("Three");
        Settle(host, guests);
        Assert.Equal(TeamLaunchRefusal.TooManyTeams, host.LaunchRefusal);
        host.LeaveTeam();
        Settle(host, guests);
        host.JoinTeam(guests[0].OwnTeam);
        Settle(host, guests);
        Assert.Equal(TeamLaunchRefusal.None, host.LaunchRefusal);

        // ABLE-TO-FAIL CONTROL: back on Deathmatch the rules name no zeppelins.
        Assert.True(host.SetMissionType(DogfightMissionType.Deathmatch));
        Assert.NotEqual(DogfightMissionType.ZeppelinVsZeppelin, DogfightLobby.RulesOf(host.Options).MissionType);
    }

    [Fact]
    public void AStuntRaceGreysAboveTheCloudsAndEveryOptionButTheTime()
    {
        var (host, guests, _) = Lobbies(2);
        Assert.True(host.SetEnvironment(0));
        Assert.True(host.SetTimeMinutes(12));
        Assert.True(host.SetVictory(DogfightVictory.Both));

        Assert.True(host.SetMissionType(DogfightMissionType.StuntRace));
        Settle(host, guests);

        // Above the Clouds has no course, so the pick moves to the next environment with one.
        var heard = guests[0].Options;
        Assert.Equal(DogfightMissionType.StuntRace, DogfightLobby.TypeOf(heard));
        Assert.Equal(1, heard.Environment);
        Assert.Equal(DogfightLobby.StuntRaceDefaultMinutes, heard.TimeMinutes);
        Assert.Equal(DogfightVictory.Time, heard.Victory);
        for (int environment = 0; environment < DogfightLobby.EnvironmentCount; environment++)
        {
            Assert.Equal(MenuChapters.DangerZonesFor(DogfightLobby.ChapterOf(environment)), DogfightLobby.Offers(DogfightMissionType.StuntRace, environment));
        }

        Assert.False(host.SetEnvironment(0));
        Assert.True(host.SetEnvironment(4));
        Assert.True(host.SetTimeMinutes(7));
        Assert.False(host.SetVictory(DogfightVictory.Score));
        Assert.False(host.SetScore(20));
        Assert.False(host.SetLimitedLives(true));
        Assert.False(host.SetAutoRespawn(false));
        Assert.False(host.SetRestrictTeams(true));
        Assert.False(host.SetAllowCustomPlanes(false));
        Assert.False(host.SetOutlawComponents(true));
        Assert.False(host.SetOutlawed(NetPlaneRules.NitroFlag, true));

        // Picking the race again keeps the typed window.
        Assert.True(host.SetMissionType(DogfightMissionType.StuntRace));
        Settle(host, guests);
        var rules = DogfightLobby.RulesOf(guests[0].Options);
        Assert.Equal(new VersusRules(0, 7, 0, true, DogfightMissionType.StuntRace), rules);
        Assert.Equal(MenuMode.Stunt, DogfightLobby.LaunchMode(guests[0].Options));
        Assert.Equal("C1", DogfightLobby.ChapterOf(guests[0].Options.Environment));

        // ABLE-TO-FAIL CONTROL: back on Deathmatch the options and Above the Clouds are live again.
        Assert.True(host.SetMissionType(DogfightMissionType.Deathmatch));
        Assert.Equal(MenuMode.Versus, DogfightLobby.LaunchMode(host.Options));
        Assert.True(host.SetEnvironment(0));
        Assert.True(host.SetVictory(DogfightVictory.Score));
        Assert.True(host.SetLimitedLives(true));
        Assert.NotEqual(DogfightMissionType.StuntRace, DogfightLobby.RulesOf(host.Options).MissionType);
    }

    [Fact]
    public void AStuntRaceLaunchesWhateverTeamsTheGreyedBoxesStoodOn()
    {
        var (host, guests, _) = Lobbies(3);
        Assert.True(host.SetMissionType(DogfightMissionType.CaptureTheFlag));
        host.CreateTeam("One");
        Settle(host, guests);
        Assert.NotEqual(TeamLaunchRefusal.None, host.LaunchRefusal);

        Assert.True(host.SetMissionType(DogfightMissionType.StuntRace));
        Assert.True(host.Options.RestrictTeams);
        Assert.Equal(TeamLaunchRefusal.None, host.LaunchRefusal);

        // ABLE-TO-FAIL CONTROL: the same lobby on Capture the Flag is refused again.
        Assert.True(host.SetMissionType(DogfightMissionType.CaptureTheFlag));
        Assert.NotEqual(TeamLaunchRefusal.None, host.LaunchRefusal);
    }

    [Fact]
    public void AStuntRaceCrossesTheWireAsTypeThree()
    {
        var sent = new DogfightOptionsMessage(4, 4, (byte)DogfightMissionType.StuntRace, DogfightVictory.Time, 7, 40, false, 3, true);
        var wire = new byte[DogfightOptionsMessage.Size];
        Assert.Equal(DogfightOptionsMessage.Size, sent.Write(wire));
        Assert.Equal(3, wire[NetMessage.HeaderBytes + 2]);
        Assert.True(DogfightOptionsMessage.TryRead(wire, out var heard));
        Assert.Equal(sent, heard);
        Assert.Equal(DogfightMissionType.StuntRace, DogfightLobby.TypeOf(heard));
        Assert.True(DogfightLobby.IsStuntRace(heard));

        // ABLE-TO-FAIL CONTROL: the byte past the box's four flies nothing.
        Assert.False(DogfightLobby.Flies(DogfightLobby.TypeOf(heard with { MissionType = DogfightLobby.TypeCount })));
        Assert.True(DogfightLobby.Flies(DogfightLobby.TypeOf(heard)));
    }

    [Fact]
    public void AStuntRaceLaunchFliesTheChaptersIa1WithTheLobbysWindow()
    {
        var cli = SessionSpec.Parse(new[] { "--mission=MP2" });
        var race = SessionSpec.FromMenu(cli, "C1", new[] { "player_bhawk" }, MenuMode.Stunt, vsTimeMinutes: 7,
            missionType: DogfightMissionType.StuntRace);
        Assert.Equal(DogfightMissionType.StuntRace, race.MissionType);
        Assert.Equal(SessionSpec.StuntRaceMission, race.Mission);
        Assert.True(race.Stunt);
        Assert.False(race.Versus);
        Assert.Equal(7, race.StuntRaceMinutes);
        Assert.Equal("stunt_flying", race.Scenario);
        Assert.Null(race.IaDef);

        // ABLE-TO-FAIL CONTROL: each other type flies its own map, and no other launch races.
        var types = new[] { DogfightMissionType.Deathmatch, DogfightMissionType.CaptureTheFlag, DogfightMissionType.ZeppelinVsZeppelin };
        var maps = new[] { SessionSpec.DeathmatchMission, SessionSpec.CtfMission, SessionSpec.ZvzMission };
        for (int i = 0; i < types.Length; i++)
        {
            var dogfight = SessionSpec.FromMenu(cli, "C1", new[] { "player_bhawk" }, MenuMode.Versus, vsTimeMinutes: 7, missionType: types[i]);
            Assert.Equal(types[i], dogfight.MissionType);
            Assert.Equal(maps[i], dogfight.Mission);
            Assert.Equal(InstantActionDef.DefaultRaceWindowMinutes, dogfight.StuntRaceMinutes);
        }

        var asVersus = SessionSpec.FromMenu(cli, "C1", new[] { "player_bhawk" }, MenuMode.Versus, missionType: DogfightMissionType.StuntRace);
        Assert.Equal(DogfightMissionType.Deathmatch, asVersus.MissionType);
        var solo = SessionSpec.FromMenu(cli, "C1", new[] { "player_bhawk" }, MenuMode.Stunt, vsTimeMinutes: 7);
        Assert.Equal(DogfightMissionType.Deathmatch, solo.MissionType);
        Assert.Equal("MP2", solo.Mission);
        Assert.Equal(InstantActionDef.DefaultRaceWindowMinutes, solo.StuntRaceMinutes);
    }

    private static (DogfightLobby Host, List<DogfightLobby> Guests, IReadOnlyList<LoopbackTransport> Mesh) Lobbies(
        int players, string guestName = "", Func<INetTransport, INetTransport>? hostCarrier = null)
    {
        var mesh = LoopbackTransport.Mesh(players, Clean, new Random(players));
        var hostWire = new NetLobby(hostCarrier?.Invoke(mesh[0]) ?? mesh[0]);
        var host = new DogfightLobby(hostWire, () => "Host");
        var guests = new List<DogfightLobby>();
        var wires = new List<NetLobby> { hostWire };
        for (int i = 1; i < players; i++)
        {
            var wire = new NetLobby(mesh[i]);
            wires.Add(wire);
            var guest = new DogfightLobby(wire, () => guestName, 0);
            guest.Show();
            guests.Add(guest);
        }

        Steps[host] = wires;
        return (host, guests, mesh);
    }

    // Every end steps its socket and then its lobby, four times over, which settles a relayed line.
    private static void Settle(DogfightLobby host, List<DogfightLobby> guests)
    {
        var wires = Steps[host];
        for (int round = 0; round < 4; round++)
        {
            wires[0].Step(0.016);
            host.Step();
            for (int i = 0; i < guests.Count; i++)
            {
                wires[i + 1].Step(0.016);
                guests[i].Step();
            }
        }
    }

    // The host's carrier, counting the plane rules messages it sends.
    private sealed class RulesCounter : INetTransport
    {
        private readonly INetTransport _inner;

        public RulesCounter(INetTransport inner) => _inner = inner;

        public int Sent { get; private set; }

        public int LocalPeer => _inner.LocalPeer;

        public IReadOnlyList<int> Peers => _inner.Peers;

        public void Bind(INetTransportListener listener) => _inner.Bind(listener);

        public void Send(int peer, ReadOnlySpan<byte> payload, NetReliability reliability, int channel = 0)
        {
            if (NetMessage.TryReadHeader(payload, out var type, out _) && type == NetMessageType.LobbyPlaneRules)
            {
                Sent++;
            }

            _inner.Send(peer, payload, reliability, channel);
        }

        public void Disconnect(int peer) => _inner.Disconnect(peer);

        public void Step(double dt) => _inner.Step(dt);
    }
}