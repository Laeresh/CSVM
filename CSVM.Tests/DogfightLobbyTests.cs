using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Net;
using CSVM.Session.Roster;
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

        Assert.False(host.SetMissionType((DogfightMissionType)3));
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
        Assert.True(rules.CaptureTheFlag);
        Assert.True(rules.FlagHomeToCapture);

        // ABLE-TO-FAIL CONTROL: back on Deathmatch every environment and the team boxes are live.
        Assert.True(host.SetMissionType(DogfightMissionType.Deathmatch));
        Assert.True(host.SetEnvironment(0));
        Assert.True(host.SetMaxTeams(4));
        Assert.False(host.SetFlagHomeToCapture(false));
        Assert.False(DogfightLobby.RulesOf(host.Options).CaptureTheFlag);
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
        Assert.True(rules.ZeppelinVsZeppelin);
        Assert.False(rules.CaptureTheFlag);

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
        Assert.False(DogfightLobby.RulesOf(host.Options).ZeppelinVsZeppelin);
    }

    [Fact]
    public void AnAddedBotJoinsTheSmallestTeamAndStaysWhereTheHostPutsIt()
    {
        var (host, guests, _) = Lobbies(3);
        Settle(host, guests);

        // No team stands: the bot flies the free-for-all, on a Random plane at veteran, Ready.
        Assert.True(host.AddBot());
        var first = host.Bots[0];
        Assert.Equal((byte)0, first.Team);
        Assert.True(first.RandomPlane);
        Assert.Equal(NetBotSkill.Veteran, first.Skill);
        Assert.True(host.Players[3] is { IsBot: true, Ready: true });
        Assert.True(host.RemoveBot(first.Id));

        Assert.True(host.CreateTeam("Home"));
        Assert.True(guests[0].CreateTeam("Away"));
        Settle(host, guests);
        Assert.True(guests[1].JoinTeam(host.OwnTeam));
        Settle(host, guests);
        byte home = host.OwnTeam;
        byte away = host.Players[1].Team;

        // Home holds two and Away one, so the bot goes Away; then two and two, the first created wins.
        Assert.True(host.AddBot());
        Assert.Equal(away, host.Bots[0].Team);
        Assert.True(host.AddBot());
        Assert.Equal(home, host.Bots[1].Team);

        // The host moves a bot, and nothing moves it back when the teams change after.
        Assert.True(host.SetBotTeam(host.Bots[0].Id, home));
        Assert.True(guests[1].LeaveTeam());
        Settle(host, guests);
        Assert.Equal(home, host.Bots[0].Team);

        // ABLE-TO-FAIL CONTROL: a team that does not stand is refused. A disbanded team's bots go
        // teamless rather than joining whatever team takes its number next.
        Assert.False(host.SetBotTeam(host.Bots[0].Id, 9));
        Assert.True(host.LeaveTeam());
        Settle(host, guests);
        Assert.All(host.Bots, bot => Assert.NotEqual(home, bot.Team));
        Assert.True(host.CreateTeam("Again"));
        Assert.Equal(home, host.OwnTeam);
        Assert.All(host.Bots, bot => Assert.NotEqual(home, bot.Team));
    }

    [Fact]
    public void FillToStopsAtItsCountAndAtSixteenPilots()
    {
        var (host, guests, _) = Lobbies(3);
        host.CallsignPool = new[] { "Winthrop", "Crawford", "Steele", "Tex" };
        Settle(host, guests);

        Assert.Equal(3, host.FillTo(6));
        Assert.Equal(6, host.FieldSeats);
        Assert.Equal(0, host.FillTo(5));
        Assert.Equal(3, host.Bots.Count);

        // Sixteen pilots at most, however many the count asks, and Add stops there too.
        Assert.Equal(10, host.FillTo(40));
        Assert.Equal(NetSeats.MaxPlayers, host.FieldSeats);
        Assert.Equal(0, host.BotRoom);
        Assert.False(host.AddBot());

        // Four pool names, then "Bot n"; no two rows share a name.
        var names = host.Bots.Select(b => b.Callsign).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(4, names.Count(n => host.CallsignPool.Contains(n)));
        Assert.All(names, n => Assert.True(n.Length <= BotSeats.CallsignLimit));

        // ABLE-TO-FAIL CONTROL: a host flying two splitscreen seats has one bot fewer of room.
        var (split, _, _) = Lobbies(1, localSeats: 2);
        Assert.Equal(14, split.FillTo(NetSeats.MaxPlayers));
    }

    [Fact]
    public void ABotRowIsEditedAndRemovedOnTheHostAloneWhileItIsNotReady()
    {
        var (host, guests, _) = Lobbies(2, "Lucy");
        Settle(host, guests);
        host.FillTo(4);
        var (a, b) = (host.Bots[0].Id, host.Bots[1].Id);

        Assert.True(host.SetBotAirframe(a, 7));
        Assert.True(host.SetBotSkill(a, NetBotSkill.Ace));
        Assert.True(host.SetBotCallsign(a, "  The Red Baron Jr  "));
        Assert.Equal(new DogfightBot(a, "The Red Baro", 7, NetBotSkill.Ace, 0), host.BotById(a));
        Assert.Equal(a, host.BotAt(2));
        Assert.Equal(b, host.BotAt(3));
        Assert.Equal(-1, host.BotAt(1));

        // A blank name, a person's name, another bot's, a plane past the eleven and a guest's edit are refused.
        Assert.False(host.SetBotCallsign(a, "   "));
        Assert.False(host.SetBotCallsign(a, "lucy"));
        Assert.False(host.SetBotCallsign(b, "the red baro"));
        Assert.False(host.SetBotAirframe(a, DogfightLobby.AirframeCount));
        Assert.False(guests[0].AddBot());
        Assert.False(guests[0].RemoveBot(a));

        Assert.True(host.RemoveBot(a));
        Assert.Null(host.BotById(a));
        Assert.Equal(b, host.BotAt(2));
        Assert.False(host.RemoveBot(a));

        // ABLE-TO-FAIL CONTROL: a Ready host's bot rows are locked, as its options are.
        host.SetReady(true);
        Assert.False(host.AddBot());
        Assert.False(host.SetBotSkill(b, NetBotSkill.Novice));
        Assert.False(host.RemoveBot(b));
        host.SetReady(false);
        Assert.True(host.RemoveBot(b));
        Assert.Empty(host.Bots);
    }

    [Fact]
    public void BotRowsSurviveAMatchAndItsReturnToTheLobby()
    {
        var (host, guests, _) = Lobbies(2, "Lucy");
        Settle(host, guests);
        host.FillTo(4);
        Assert.True(host.SetBotAirframe(host.Bots[1].Id, 3));
        var kept = host.Bots.ToArray();

        host.Launched();
        Assert.Equal(new[] { "Host", "Lucy", kept[0].Callsign, kept[1].Callsign }, host.LaunchNames);
        host.Land(new[] { new DogfightScore("Lucy", 2, 2, 0) });
        Settle(host, guests);

        Assert.Equal(kept, host.Bots);
        Assert.Equal(4, guests[0].Players.Count);
        Assert.True(guests[0].Players[3] is { IsBot: true, Airframe: 3 });

        // ABLE-TO-FAIL CONTROL: the lobby's own round moved on, so the rows were not frozen with it.
        Assert.False(host.Ready);
        Assert.True(host.AddBot());
    }

    [Fact]
    public void AHostAndItsBotsOnOneTeamAreRefusedAsOneStandingTeam()
    {
        var (host, _, _) = Lobbies(1);
        Assert.True(host.CreateTeam("Solo"));
        Assert.Equal(TeamLaunchRefusal.NotEnoughPlayers, host.LaunchRefusal);

        // The bot joins the one team standing, which makes two players on one team: langui 10519.
        Assert.True(host.AddBot());
        Assert.Equal(host.OwnTeam, host.Bots[0].Team);
        Assert.Equal(TeamLaunchRefusal.TooFewTeams, host.LaunchRefusal);
        Assert.True(host.Teamed);

        // Off the team it is still one standing team, now with a teamless player beside it.
        Assert.True(host.SetBotTeam(host.Bots[0].Id, 0));
        Assert.Equal(TeamLaunchRefusal.TooFewTeams, host.LaunchRefusal);

        // ABLE-TO-FAIL CONTROL: with no team standing the two fly a free-for-all.
        Assert.True(host.LeaveTeam());
        Assert.False(host.Teamed);
        Assert.Equal(TeamLaunchRefusal.None, host.LaunchRefusal);

        // And bots on two people's teams balance them.
        var (mixed, others, _) = Lobbies(2);
        Settle(mixed, others);
        mixed.CreateTeam("Home");
        others[0].CreateTeam("Away");
        Settle(mixed, others);
        Assert.Equal(2, mixed.FillTo(4));
        Assert.Equal(TeamLaunchRefusal.None, mixed.LaunchRefusal);
        Assert.True(mixed.SetBotTeam(mixed.Bots[1].Id, mixed.OwnTeam));
        Assert.Equal(TeamLaunchRefusal.Unbalanced, mixed.LaunchRefusal);
    }

    [Fact]
    public void BotRowsFlyADeathmatchOnly()
    {
        var (host, _, _) = Lobbies(1);
        Assert.True(host.AddBot());
        Assert.False(host.BotsGrounded);
        Assert.Single(host.LaunchBots);

        // Capture the Flag has no flag-flying pilot to give a bot: no new row, and the launch is held.
        Assert.True(host.SetMissionType(DogfightMissionType.CaptureTheFlag));
        Assert.False(host.AddBot());
        Assert.True(host.BotsGrounded);
        Assert.Empty(host.LaunchBots);
        Assert.Single(host.Bots);

        // ABLE-TO-FAIL CONTROL: removing the row, or going back to Deathmatch, frees the launch.
        Assert.True(host.SetMissionType(DogfightMissionType.Deathmatch));
        Assert.False(host.BotsGrounded);
        Assert.True(host.SetMissionType(DogfightMissionType.ZeppelinVsZeppelin));
        Assert.True(host.RemoveBot(host.Bots[0].Id));
        Assert.False(host.BotsGrounded);
    }

    [Fact]
    public void AGuestReadsTheHostsBotRowsAndCannotTouchThem()
    {
        var (host, guests, _) = Lobbies(2, "Lucy");
        host.CreateTeam("Home");
        Settle(host, guests);
        host.FillTo(3);
        Assert.True(host.SetBotSkill(host.Bots[0].Id, NetBotSkill.Novice));
        Settle(host, guests);

        var guest = guests[0];
        var row = guest.Players[2];
        Assert.True(row.IsBot);
        Assert.Equal((host.Bots[0].Callsign, DogfightLobbySeat.RandomAirframe, NetBotSkill.Novice, host.OwnTeam, true),
            (row.Name, row.Airframe, row.Skill, row.Team, row.Ready));
        Assert.False(guest.Players[1].IsBot);
        Assert.Empty(guest.Bots);
        Assert.Equal(-1, guest.BotAt(2));

        // ABLE-TO-FAIL CONTROL: a bot edit on the host reaches the guest's row on the next step.
        Assert.True(host.SetBotAirframe(host.Bots[0].Id, 9));
        Settle(host, guests);
        Assert.Equal(9, guest.Players[2].Airframe);
    }

    [Fact]
    public void ALaunchDrawsRandomPlanesOnTheHostAndSeatsTheBotsAfterTheGuests()
    {
        var (host, guests, _) = Lobbies(3, "Lucy");
        Settle(host, guests);
        host.FillTo(6);
        Assert.True(host.SetBotAirframe(host.Bots[1].Id, 7));

        var entries = host.LaunchBots;
        Assert.Equal(new string?[] { null, StockAirframes.Node(7), null }, entries.Select(e => e.Plane));
        Assert.Equal(host.Bots.Select(b => b.Callsign), entries.Select(e => e.Callsign));

        // The host's field as the launch builds it: its seat, each guest's, then the bots.
        var people = new List<NetSeat>
        {
            new() { PeerId = 0, SeatIndex = 0, FlownHere = true, Callsign = "Host", PlaneNode = StockAirframes.Node(0) },
            new() { PeerId = 1, SeatIndex = 1, Callsign = "Lucy", PlaneNode = StockAirframes.Node(1) },
            new() { PeerId = 2, SeatIndex = 2, Callsign = "Lucy2", PlaneNode = StockAirframes.Node(2) },
        };
        var resolved = BotSeats.Resolve(entries, people.Select(s => s.Callsign), Array.Empty<string>(), new Random(5));
        Assert.Equal(0, NetSeats.AddBots(people, 0, resolved));
        NetSeats.Validate(people, 0);
        Assert.Equal(new[] { false, false, false, true, true, true }, people.Select(s => s.IsBot));
        Assert.All(people.Skip(3), seat => Assert.Contains(seat.PlaneNode, StockAirframes.Nodes));
        Assert.Equal(StockAirframes.Node(7), people[4].PlaneNode);
        Assert.Equal(host.Bots.Select(b => b.Callsign), people.Skip(3).Select(s => s.Callsign));

        // ABLE-TO-FAIL CONTROL: a guest's lobby hands the launch no bots of its own.
        Assert.Empty(guests[0].LaunchBots);
    }

    private static (DogfightLobby Host, List<DogfightLobby> Guests, IReadOnlyList<LoopbackTransport> Mesh) Lobbies(
        int players, string guestName = "", Func<INetTransport, INetTransport>? hostCarrier = null, int localSeats = 1)
    {
        var mesh = LoopbackTransport.Mesh(players, Clean, new Random(players));
        var hostWire = new NetLobby(hostCarrier?.Invoke(mesh[0]) ?? mesh[0]);
        var host = new DogfightLobby(hostWire, () => "Host") { LocalSeats = () => localSeats, BotDraws = new Random(players) };
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