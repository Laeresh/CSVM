using System;
using System.Collections.Generic;
using System.Linq;
using CSVM;
using CSVM.Flight.Hangar;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.UI.Hangar;
using CSVM.UI.Menu;
using CSVM.UI.Screens;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The shared player setup off-engine: seats claimed by source identity, the two-stage pick and
/// its Back, the loadout list and the launch gate per mode. Then the seat choices, the typed exit
/// and the roster rule against the picker roster. Then the same-frame races: two claims, a lock and
/// an unjoin, a confirm on a seat that has left. Last the switch's discard and the host's seats.
/// </summary>
public class PlayerSetupFeatureTests
{
    private static readonly (string Name, string Node)[] Stock =
    {
        ("Autogyro", "player_autogyro"), ("Hellhound", "player_avenger"), ("Balmoral", "player_balmoral"),
    };

    [Fact]
    public void TheMaximumIsTheSplitscreenRigsAndAClaimIsOneSourceOneSeat()
    {
        var setup = Setup();
        var a = new ScriptedMenuSeat();
        var b = new ScriptedMenuSeat();

        Assert.Equal(SplitScreen.MaxPlayers, PlayerSetupFeature.MaxSeats);
        var seatA = setup.Join(a);
        var seatB = setup.Join(b);
        Assert.NotNull(seatA);
        Assert.NotNull(seatB);
        Assert.Same(a, seatA!.Source);
        Assert.Equal(new IMenuInputSource[] { a, b }, setup.Sources);
        Assert.Null(setup.Join(a));
        Assert.True(setup.IsClaimed(b));
        Assert.Same(seatB, setup.SeatOf(b));

        setup.Join(new ScriptedMenuSeat());
        setup.Join(new ScriptedMenuSeat());
        Assert.Equal(4, setup.Seats.Count);
        Assert.Null(setup.Join(new ScriptedMenuSeat()));
        Assert.Equal(4, setup.Revision);
    }

    [Fact]
    public void SeatZeroNeverLeavesAndAnUnjoinedSeatIsRefusedEveryOperation()
    {
        var setup = Setup();
        var first = setup.Join(new ScriptedMenuSeat())!;
        var second = setup.Join(new ScriptedMenuSeat())!;

        Assert.False(setup.Unjoin(first));
        Assert.True(setup.Unjoin(second));
        Assert.False(second.Joined);
        Assert.False(setup.Unjoin(second));
        Assert.False(setup.Browse(second, 1));
        Assert.False(setup.Select(second));
        Assert.False(setup.Confirm(second));
        Assert.Equal(SeatBack.Browsing, setup.Back(second));
        Assert.Equal(1, setup.Seats.Count);
    }

    [Fact]
    public void ThePickIsTwoStagesAndBackWalksThemDownOneAtATime()
    {
        var setup = Setup();
        var seat = setup.Join(new ScriptedMenuSeat())!;

        Assert.False(setup.Confirm(seat));
        Assert.True(setup.Select(seat));
        Assert.True(seat.Locked);
        Assert.False(setup.Select(seat));
        Assert.True(setup.Confirm(seat));
        Assert.True(seat.Confirmed);
        Assert.False(setup.Confirm(seat));

        Assert.Equal(SeatBack.Unconfirmed, setup.Back(seat));
        Assert.True(seat.Locked);
        Assert.Equal(SeatBack.Unselected, setup.Back(seat));
        Assert.False(seat.Locked);
        Assert.Equal(SeatBack.Browsing, setup.Back(seat));
    }

    [Fact]
    public void BrowsingMovesTheCursorAndResetsTheFitOnlyWhenTheAirframeChangesAndNeverWhileSelected()
    {
        var setup = Setup();
        var seat = setup.Join(new ScriptedMenuSeat())!;
        seat.Fit.SetPylon(1, "wep_14");

        Assert.False(setup.Browse(seat, 0));
        Assert.False(seat.Fit.IsStock);
        Assert.True(setup.Browse(seat, 2));
        Assert.Equal(2, seat.Cursor);
        Assert.True(seat.Fit.IsStock);

        setup.Select(seat);
        Assert.False(setup.Browse(seat, 1));
        Assert.Equal(2, seat.Cursor);
        Assert.False(setup.Browse(seat, -1));
    }

    [Fact]
    public void ACursorPastTheRosterCanBeParkedButNotSelected()
    {
        var setup = Setup();
        var seat = setup.Join(new ScriptedMenuSeat())!;

        Assert.True(setup.Browse(seat, Stock.Length));
        Assert.False(setup.Select(seat));
        Assert.Throws<InvalidOperationException>(() => setup.Choices(_ => Array.Empty<int>()));
    }

    [Fact]
    public void TheLoadoutListOpensOnlyForASelectedUnconfirmedSeatAndConfirmingClosesIt()
    {
        var setup = Setup();
        var seat = setup.Join(new ScriptedMenuSeat())!;

        Assert.False(setup.OpenLoadout(seat));
        setup.Select(seat);
        seat.FitRow = 3;
        Assert.True(setup.OpenLoadout(seat));
        Assert.True(seat.InLoadout);
        Assert.Equal(0, seat.FitRow);
        Assert.False(setup.OpenLoadout(seat));
        Assert.True(setup.CloseLoadout(seat));
        Assert.False(setup.CloseLoadout(seat));

        setup.OpenLoadout(seat);
        setup.Confirm(seat);
        Assert.False(seat.InLoadout);
        Assert.False(setup.OpenLoadout(seat));
    }

    [Fact]
    public void ResetPicksReturnsEverySeatToBrowsingKeepingCursorsAndOptionallyTheFits()
    {
        var setup = Setup();
        var a = setup.Join(new ScriptedMenuSeat())!;
        var b = setup.Join(new ScriptedMenuSeat())!;
        setup.Browse(a, 2);
        setup.Select(a);
        setup.OpenLoadout(a);
        a.Fit.SetPylon(1, "wep_14");
        setup.Select(b);
        setup.Confirm(b);

        setup.ResetPicks(fits: false);
        Assert.False(a.Locked || a.InLoadout || b.Locked || b.Confirmed);
        Assert.Equal(2, a.Cursor);
        Assert.False(a.Fit.IsStock);

        setup.ResetPicks(fits: true);
        Assert.True(a.Fit.IsStock);
    }

    /// <summary>The gate is the menu's rule: everyone joined has confirmed, and
    /// Dogfight needs two seats; Free Flight and Instant Action launch solo.</summary>
    [Theory]
    [InlineData(MenuMode.Free, 1, 1)]
    [InlineData(MenuMode.Free, 2, 1)]
    [InlineData(MenuMode.Free, 2, 2)]
    [InlineData(MenuMode.Stunt, 1, 1)]
    [InlineData(MenuMode.Stunt, 3, 2)]
    [InlineData(MenuMode.Versus, 1, 1)]
    [InlineData(MenuMode.Versus, 2, 1)]
    [InlineData(MenuMode.Versus, 2, 2)]
    [InlineData(MenuMode.Versus, 4, 4)]
    public void TheGateIsEveryoneConfirmedAndDogfightNeedsTwo(MenuMode mode, int joined, int confirmed)
    {
        var setup = Seated(joined, confirmed);

        Assert.Equal(confirmed == joined && (mode != MenuMode.Versus || joined >= 2), setup.CanLaunch(mode));
        Assert.Equal(mode == MenuMode.Versus ? 2 : 1, PlayerSetupFeature.MinimumSeats(mode));
    }

    [Fact]
    public void TheRefusalNamesWhatIsMissing()
    {
        Assert.Equal("no seat joined", Setup().Refusal(MenuMode.Free));
        Assert.Equal("Dogfight needs a second seat", Seated(1, 1).Refusal(MenuMode.Versus));
        Assert.Equal("1 of 2 seats not confirmed", Seated(2, 1).Refusal(MenuMode.Versus));
        Assert.Null(Seated(2, 2).Refusal(MenuMode.Versus));
        Assert.Equal(1, Seated(3, 1).ConfirmedCount);
    }

    /// <summary>VR mode's one seat refuses a second claim. A local Dogfight's gate names VR rather
    /// than inviting a second seat, and a network Dogfight still goes. A discard keeps the limit,
    /// which is the process's and not the setup's.</summary>
    [Fact]
    public void UnderVrOneSeatASecondClaimIsRefusedAndALocalDogfightSaysWhy()
    {
        var setup = Setup();
        setup.SeatLimit = 1;
        Assert.True(setup.OneSeatForVr);
        var lone = setup.Join(new ScriptedMenuSeat())!;
        Assert.NotNull(lone);
        Assert.Null(setup.Join(new ScriptedMenuSeat()));
        Assert.Single(setup.Seats);

        setup.Select(lone);
        setup.Confirm(lone);
        Assert.Equal(PlayerSetupFeature.VrOneSeat, setup.Refusal(MenuMode.Versus));
        Assert.Null(setup.Refusal(MenuMode.Versus, networked: true));
        Assert.Null(setup.Refusal(MenuMode.Free));

        setup.Discard();
        Assert.Equal(1, setup.SeatLimit);
        setup.SeatLimit = 9;
        Assert.Equal(PlayerSetupFeature.MaxSeats, setup.SeatLimit);
        Assert.False(setup.OneSeatForVr);
    }

    [Fact]
    public void TheChoicesCarryEachSeatsNodeDevicesFitAndCustomDef()
    {
        var custom = new CustomPlaneDef { Name = "Blue Streak", Airframe = 7 };
        var setup = new PlayerSetupFeature();
        setup.SetRoster(PlayerSetupFeature.BuildRoster(Stock, new[] { custom }, StockAirframes.Node));
        var a = setup.Join(new ScriptedMenuSeat())!;
        var b = setup.Join(new ScriptedMenuSeat())!;
        setup.Browse(a, 2);
        setup.Browse(b, 3);
        setup.Select(a);
        setup.OpenLoadout(a);
        a.Fit.SetPylon(1, "wep_14");
        setup.Confirm(a);
        setup.Select(b);
        setup.Confirm(b);

        var choices = setup.Choices(seat => ReferenceEquals(seat, b) ? new[] { 2 } : Array.Empty<int>());

        Assert.Equal(2, choices.Count);
        Assert.Equal("player_balmoral", choices[0].PlaneNode);
        Assert.Same(a.Fit, choices[0].Fit);
        Assert.Null(choices[0].Custom);
        Assert.Empty(choices[0].Pads);
        Assert.Equal(StockAirframes.Node(7), choices[1].PlaneNode);
        Assert.Null(choices[1].Fit);
        Assert.Same(custom, choices[1].Custom);
        Assert.Equal(new[] { 2 }, choices[1].Pads);
    }

    /// <summary>A local Dogfight opens on the network lobby's own rules: 10 minutes armed and 40 in
    /// the Score box. A local and a network Dogfight start in the same place.</summary>
    [Fact]
    public void TheMatchRulesOpenOnTheNetworkLobbysOwn()
    {
        var setup = Setup();

        Assert.Equal(DogfightLobby.OpeningOptions, setup.MatchOptions);
        Assert.Equal(new VersusRules(0, 10), DogfightLobby.RulesOf(setup.MatchOptions));
        Assert.Equal(40, setup.MatchOptions.Score);
    }
    /// <summary>Only a Dogfight exit carries match rules: they are that mode's, and a Free Flight
    /// exit carrying them would hand the launcher a rule nothing reads.</summary>
    [Fact]
    public void OnlyAVersusExitCarriesTheMatchRules()
    {
        var setup = Seated(2, 2);
        setup.MatchOptions = setup.MatchOptions with { Victory = CSVM.Net.DogfightVictory.Score, Score = 8 };

        var dogfight = setup.BuildExit("C4", MenuMode.Versus, _ => Array.Empty<int>());
        var free = setup.BuildExit("C4", MenuMode.Free, _ => Array.Empty<int>());

        Assert.Equal(new VersusRules(8, 0), dogfight.Match);
        Assert.Null(free.Match);
    }

    [Fact]
    public void TheExitCarriesTheChapterTheChoicesAndTheModeAndRefusesAClosedGate()
    {
        var setup = Seated(2, 2);

        var exit = setup.BuildExit("C4", MenuMode.Versus, _ => Array.Empty<int>());

        Assert.Equal("C4", exit.Chapter);
        Assert.Equal(MenuMode.Versus, exit.Mode);
        Assert.Null(exit.InstantAction);
        Assert.Equal(new VersusRules(0, DogfightLobby.DefaultTimeMinutes), exit.Match);
        Assert.Equal(2, exit.Seats.Count);
        Assert.Throws<InvalidOperationException>(() => Seated(1, 1).BuildExit("C4", MenuMode.Versus, _ => Array.Empty<int>()));
        Assert.Single(Seated(1, 1).BuildExit("C4", MenuMode.Versus, _ => Array.Empty<int>(), networked: true).Seats);
        Assert.Throws<InvalidOperationException>(() => Seated(2, 1).BuildExit("C4", MenuMode.Free, _ => Array.Empty<int>()));
        Assert.Throws<ArgumentException>(() => setup.BuildExit(string.Empty, MenuMode.Free, _ => Array.Empty<int>()));
    }

    /// <summary>A networked Dogfight's opponent sits at another machine, so one local seat builds
    /// the exit the gate opened for; a local one-seat Dogfight still refuses.</summary>
    [Fact]
    public void ANetworkedDogfightBuildsFromOneSeatAndALocalOneRefuses()
    {
        var setup = Seated(1, 1);

        Assert.True(setup.CanLaunch(MenuMode.Versus, networked: true));
        Assert.Single(setup.BuildExit("C4", MenuMode.Versus, _ => Array.Empty<int>(), networked: true).Seats);
        Assert.False(setup.CanLaunch(MenuMode.Versus));
        Assert.Throws<InvalidOperationException>(() => setup.BuildExit("C4", MenuMode.Versus, _ => Array.Empty<int>()));
    }

    [Fact]
    public void TheRosterRuleAgreesWithThePickerRosterRowForRow()
    {
        var customs = new[]
        {
            new CustomPlaneDef { Name = "Blue Streak", Airframe = 7 },
            new CustomPlaneDef { Name = "Minx", Airframe = 0 },
        };

        var rows = PlayerSetupFeature.BuildRoster(Stock, customs, StockAirframes.Node);
        var picker = PlanePickerRoster.Build(Stock, customs);

        Assert.Equal(picker.Select(p => p.Name), rows.Select(r => r.Name));
        Assert.Equal(picker.Select(p => p.Node), rows.Select(r => r.Node));
        Assert.Equal(picker.Select(p => p.IsCustom), rows.Select(r => r.IsCustom));
        Assert.Same(customs[0], rows[3].Custom);
        Assert.Same(customs[1], rows[4].Custom);
    }

    // ---- Same-frame races ---------------------------------------------------------------------

    [Fact]
    public void TwoSourcesClaimingOnTheSameFrameSeatInArrivalOrderAndARepeatClaimIsRefused()
    {
        var setup = Setup();
        setup.Join(new ScriptedMenuSeat());
        var pad3 = new ScriptedMenuSeat();
        var pad5 = new ScriptedMenuSeat();

        var first = setup.Join(pad3);
        var second = setup.Join(pad5);
        var again = setup.Join(pad3);

        Assert.Same(pad3, setup.Seats[1].Source);
        Assert.Same(pad5, setup.Seats[2].Source);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Null(again);
        Assert.Equal(3, setup.Seats.Count);
    }

    [Fact]
    public void ALockAndAnUnjoinOnTheSameFrameBothLandWhicheverOrderTheyArriveIn()
    {
        var setup = Setup();
        setup.Join(new ScriptedMenuSeat());
        var leaving = setup.Join(new ScriptedMenuSeat())!;
        var locking = setup.Join(new ScriptedMenuSeat())!;

        Assert.True(setup.Select(locking));
        Assert.True(setup.Unjoin(leaving));
        Assert.Equal(2, setup.Seats.Count);
        Assert.Same(locking, setup.Seats[1]);
        Assert.True(locking.Locked);

        // The other order: the seat that leaves cannot lock afterwards, and the gate counts only
        // who is still seated.
        var gone = setup.Join(new ScriptedMenuSeat())!;
        Assert.True(setup.Unjoin(gone));
        Assert.False(setup.Select(gone));
        setup.Confirm(locking);
        setup.Select(setup.Seats[0]);
        setup.Confirm(setup.Seats[0]);
        Assert.True(setup.CanLaunch(MenuMode.Versus));
        Assert.Equal(2, setup.Choices(_ => Array.Empty<int>()).Count);
    }

    [Fact]
    public void AConfirmFromASourceThatHasVanishedIsRefusedAndCountsForNothing()
    {
        var setup = Setup();
        setup.Join(new ScriptedMenuSeat());
        var vanished = setup.Join(new ScriptedMenuSeat())!;
        setup.Select(vanished);
        setup.Select(setup.Seats[0]);
        setup.Confirm(setup.Seats[0]);

        Assert.True(setup.Unjoin(vanished));
        Assert.False(setup.Confirm(vanished));
        Assert.False(vanished.Confirmed);
        Assert.Equal(1, setup.ConfirmedCount);
        Assert.Equal("Dogfight needs a second seat", setup.Refusal(MenuMode.Versus));
        Assert.True(setup.CanLaunch(MenuMode.Free));
        Assert.Single(setup.Choices(_ => Array.Empty<int>()));
    }

    // ---- The switch and the host ------------------------------------------------------------

    [Fact]
    public void DiscardDropsEverySeatButTheFirstAndItsPickAndKeepsTheRoster()
    {
        var setup = Setup();
        var first = setup.Join(new ScriptedMenuSeat())!;
        setup.Join(new ScriptedMenuSeat());
        setup.Join(new ScriptedMenuSeat());
        setup.Browse(first, 2);
        setup.Select(first);
        first.Fit.SetPylon(1, "wep_14");
        setup.MatchOptions = setup.MatchOptions with { TimeMinutes = 20 };
        setup.MatchChapter = "C3";
        int revision = setup.Revision;

        setup.Discard();

        Assert.Equal(DogfightLobby.OpeningOptions, setup.MatchOptions);
        Assert.Null(setup.MatchChapter);

        Assert.Single(setup.Seats);
        Assert.Same(first, setup.Seats[0]);
        Assert.False(first.Locked);
        Assert.Equal(0, first.Cursor);
        Assert.True(first.Fit.IsStock);
        Assert.Equal(Stock.Length, setup.Roster.Count);
        Assert.True(setup.Revision > revision);
        Assert.Equal(new[] { first }, setup.Seats);

        var features = new MenuFeatureSet();
        features.Add(setup);
        features.DiscardTransient();
        Assert.Single(setup.Seats);
    }

    [Fact]
    public void TheHostLendsTheFeaturesSeatListOnceTheFeatureIsRegistered()
    {
        var host = new MenuHost(new PresentationRegistry(), new RecordingAudio(), _ => { });
        var setup = new PlayerSetupFeature();
        host.Features.Add(setup);
        var seat0 = new ScriptedMenuSeat();

        host.AddSeat(seat0);
        Assert.Same(seat0, Assert.Single(host.Seats));
        Assert.Same(seat0, setup.Seats[0].Source);

        var joined = new ScriptedMenuSeat();
        setup.Join(joined);
        Assert.Equal(new IMenuInputSource[] { seat0, joined }, host.Seats);
        host.RemoveSeat(joined);
        Assert.Same(seat0, Assert.Single(host.Seats));
        host.RemoveSeat(seat0);
        Assert.Single(host.Seats);
        Assert.Throws<InvalidOperationException>(() => host.AddSeat(seat0));
    }

    [Fact]
    public void AnIdleSourceReadsIdleAndIsItsOwnClaim()
    {
        var setup = Setup();
        var idle = new MenuIdleSource();

        Assert.Equal("no device", idle.DeviceLabel);
        Assert.Same(MenuCommands.None, idle.Poll(1f / 60f));
        Assert.NotNull(setup.Join(idle));
        Assert.Null(setup.Join(idle));
        Assert.NotNull(setup.Join(new MenuIdleSource()));
    }

    private static PlayerSetupFeature Setup()
    {
        var setup = new PlayerSetupFeature();
        setup.SetRoster(PlayerSetupFeature.BuildRoster(Stock, Array.Empty<CustomPlaneDef>(), StockAirframes.Node));
        return setup;
    }

    // A setup with the given seats joined, the first `confirmed` of them confirmed.
    private static PlayerSetupFeature Seated(int joined, int confirmed)
    {
        var setup = Setup();
        for (int i = 0; i < joined; i++)
        {
            var seat = setup.Join(new ScriptedMenuSeat())!;
            if (i < confirmed)
            {
                setup.Select(seat);
                setup.Confirm(seat);
            }
        }

        return setup;
    }
}
