using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CSVM.Flight.Hangar;
using CSVM.Mech3;
using CSVM.Session.Campaign;
using CSVM.UI.Menu;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The campaign feature's contract with the profile store, pinned over a scratch directory: what a
/// created player writes and where, what the refusals leave untouched, what a fit, a crew pick, an
/// export, a purchase, a sale, a launch and a deletion write, what a flown mission's record reads
/// back as, and that a discard touches no file. Every file is compared as the store's own canonical
/// text, so a changed write order or field is a failed test rather than a changed save.
/// </summary>
public class CampaignFeatureTests
{
    private const string Pilot = "Zachary";

    // A hangar word's holder when no seat holds the plane.
    private const int Free = CSVM.Net.CoopHangarMessage.NoHolder;

    [Fact]
    public void ContinueOnANewNameWritesAFreshProfileAndRecordsThePlayer()
    {
        var (feature, store, dir) = Open();

        Assert.Null(feature.ContinuePlayer(Pilot));

        Assert.Equal(CampaignProfileStore.Serialize(CampaignProfileDef.NewProfile(Pilot)), ProfileText(store, Pilot));
        Assert.Equal(new[] { Pilot }, feature.Roster);
        Assert.Equal(Pilot, feature.Profile?.Name);
        Assert.Equal(Pilot, store.LastPlayed);
        Assert.Equal(new[] { "last-played.json" }, Names(Directory.GetFiles(dir)));
        Assert.Equal(new[] { Pilot }, Names(Directory.GetDirectories(dir)));
        using var last = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "last-played.json")));
        Assert.Equal(CampaignProfileStore.Version, last.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(Pilot, last.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void ContinueOnAnExistingPlayerSeatsWithoutRewritingTheProfile()
    {
        var (feature, store, _) = Open();
        var profile = CampaignProfileDef.NewProfile(Pilot);
        profile.Funds = 1234;
        store.Save(profile);
        string before = ProfileText(store, Pilot);
        feature.RefreshRoster();

        Assert.Null(feature.ContinuePlayer(Pilot));

        Assert.Equal(before, ProfileText(store, Pilot));
        Assert.Equal(1234, feature.Profile?.Funds);
        Assert.Equal(Pilot, store.LastPlayed);
    }

    [Fact]
    public void RefusedNamesWriteNothing()
    {
        var (feature, store, dir) = Open();

        Assert.Equal("You must enter a player name.", feature.ContinuePlayer("   "));
        Assert.Equal(
            "Your player name is limited to alphabetic and numeric characters and spaces.",
            feature.ContinuePlayer("Bad!Name"));
        Assert.Equal(
            $"Your player name is limited to {CampaignFeature.MaxNameLength} characters.",
            feature.ContinuePlayer(new string('a', CampaignFeature.MaxNameLength + 1)));

        Assert.False(Directory.Exists(dir));
        Assert.Null(feature.Profile);
        Assert.Equal(string.Empty, store.LastPlayed);
    }

    [Fact]
    public void AFullRosterRefusesANewPlayerAndStillContinuesAnExistingOne()
    {
        var (feature, store, _) = Open();
        for (int i = 0; i < CampaignFeature.MaxProfiles; i++)
        {
            store.Save(CampaignProfileDef.NewProfile($"Pilot {i:00}"));
        }

        feature.RefreshRoster();

        Assert.StartsWith("Crimson Skies supports only 24 active players", feature.ContinuePlayer("One More"));
        Assert.Equal(CampaignFeature.MaxProfiles, store.List().Count);
        Assert.Null(feature.ContinuePlayer("Pilot 03"));
        Assert.Equal("Pilot 03", feature.Profile?.Name);
    }

    [Fact]
    public void DeleteRemovesOnlyThatProfileAndClearsItsLastPlayedRecord()
    {
        var (feature, store, dir) = Open();
        feature.ContinuePlayer("Nathan");
        feature.ContinuePlayer(Pilot);
        string nathan = ProfileText(store, "Nathan");

        Assert.True(feature.DeletePlayer(Pilot));

        Assert.False(Directory.Exists(store.DirFor(Pilot)));
        Assert.Equal(nathan, ProfileText(store, "Nathan"));
        Assert.Equal(new[] { "Nathan" }, feature.Roster);
        Assert.Equal(string.Empty, store.LastPlayed);
        Assert.Equal(new[] { "Nathan" }, Names(Directory.GetDirectories(dir)));

        feature.ContinuePlayer("Nathan");
        Assert.False(feature.DeletePlayer("Nobody"));
        Assert.Equal("Nathan", store.LastPlayed);
    }

    [Fact]
    public void CommitLoadoutSavesTheSeatedPlanesFitAndNothingForAGuest()
    {
        var (feature, store, _) = Open();
        feature.ContinuePlayer(Pilot);
        var profile = feature.Profile!;
        feature.SetAmmoSlot(0);
        var target = feature.AmmoTarget()!;
        Assert.Same(profile.Planes[0], target);

        feature.CommitLoadout(target, new[] { 1, 2, 0, 3 }, new[] { 2, 0, 0, 0, 5, 0, 0, 0 });

        Assert.Equal(CampaignProfileStore.Serialize(profile), ProfileText(store, Pilot));
        var saved = store.Load(Pilot)!;
        Assert.Equal(new[] { 1, 2, 0, 3 }, saved.Planes[0].Ammo);
        Assert.Equal(new[] { 2, 0, 0, 0, 5, 0, 0, 0 }, saved.Planes[0].Ordnance);

        string before = ProfileText(store, Pilot);
        feature.Field.SetPlayers(2);
        Assert.True(feature.Field.Advance());
        var guestPlane = feature.AmmoTarget()!;
        Assert.Equal(profile.Planes[1].Name, guestPlane.Name);
        Assert.NotSame(profile.Planes[1], guestPlane);
        feature.CommitLoadout(guestPlane, new[] { 3, 3, 3, 3 }, new int[8]);

        Assert.Equal(new[] { 3, 3, 3, 3 }, guestPlane.Ammo);
        Assert.NotEqual(new[] { 3, 3, 3, 3 }, profile.Planes[1].Ammo);
        Assert.Equal(before, ProfileText(store, Pilot));
    }

    [Fact]
    public void CommitPlanesWritesThePicksAndLeavesTheWingmanWhenNoneIsGiven()
    {
        var (feature, store, _) = Open();
        feature.ContinuePlayer(Pilot);
        feature.Profile!.Planes.Add(new OwnedPlane { Name = "Test Bird", Airframe = 3 });

        feature.CommitPlanes(2, 0);
        var saved = store.Load(Pilot)!;
        Assert.Equal(2, saved.SelectedPlane);
        Assert.Equal(0, saved.WingmanPlane);
        Assert.Equal(3, saved.Planes.Count);

        feature.CommitPlanes(1, null);
        saved = store.Load(Pilot)!;
        Assert.Equal(1, saved.SelectedPlane);
        Assert.Equal(0, saved.WingmanPlane);
        Assert.Equal(CampaignProfileStore.Serialize(feature.Profile), ProfileText(store, Pilot));
    }

    [Fact]
    public void TheSeatedPairRuleComparesByName()
    {
        var (feature, _, _) = Open();
        feature.ContinuePlayer(Pilot);
        feature.Profile!.Planes.Add(new OwnedPlane { Name = "Gypsy Magic", Airframe = 3 });

        Assert.True(feature.SeatedPairClashes(0, 2));
        Assert.False(feature.SeatedPairClashes(0, 1));
        Assert.False(feature.SeatedPairClashes(0, 9));
    }

    [Fact]
    public void ExportWritesTheBuildStoreAndNeverTheProfile()
    {
        var planesDir = TestData.TempDir();
        var planes = new CustomPlaneStore(planesDir);
        var (feature, store, _) = Open(planes);
        feature.ContinuePlayer(Pilot);
        var plane = feature.Profile!.Planes[1];
        plane.Ammo = new[] { 2, 2, 2, 2 };
        string before = ProfileText(store, Pilot);

        Assert.True(feature.ExportPlane(plane));

        var built = planes.Load(plane.Name);
        Assert.NotNull(built);
        Assert.Equal(plane.Airframe, built!.Airframe);
        Assert.True(built.HasLoadout);
        Assert.Equal(before, ProfileText(store, Pilot));

        feature.Field.SetPlayers(2);
        feature.Field.Advance();
        Assert.True(feature.Field.Choose(1, feature.Field.Guests[0].StockChoice));
        var stock = feature.Field.Plane(1)!;
        Assert.False(feature.ExportPlane(stock));
        Assert.Null(planes.Load(stock.Name));
        Assert.False(feature.ExportPlane(new OwnedPlane { Name = " ", Airframe = 1 }));
    }

    [Fact]
    public void ExportIsRefusedWithoutABuildStore()
    {
        var (feature, _, _) = Open();
        feature.ContinuePlayer(Pilot);

        Assert.False(feature.ExportPlane(feature.Profile!.Planes[0]));
    }

    [Fact]
    public void TheWalletsPurchaseAndSaleWriteTheProfileAndTheSaleDeletesTheBuild()
    {
        var planes = new CustomPlaneStore(TestData.TempDir());
        var (feature, store, _) = Open(planes);
        feature.ContinuePlayer(Pilot);
        var profile = feature.Profile!;
        profile.Funds = 50_000;
        var wallet = feature.Wallet();
        Assert.NotNull(wallet);

        planes.Save(new CustomPlaneDef { Name = "Bought", Airframe = 5, Engine = 1 });
        wallet!.Purchase("Bought", 5, 12_000);

        var saved = store.Load(Pilot)!;
        Assert.Equal(38_000, saved.Funds);
        Assert.Equal(new[] { "Gypsy Magic", "The Knave", "Bought" }, saved.Planes.ConvertAll(p => p.Name));
        Assert.Equal(CampaignProfileStore.Serialize(profile), ProfileText(store, Pilot));

        int price = wallet.SellPrice("Bought");
        Assert.True(wallet.Sell("Bought"));

        saved = store.Load(Pilot)!;
        Assert.Equal(38_000 + price, saved.Funds);
        Assert.Equal(new[] { "Gypsy Magic", "The Knave" }, saved.Planes.ConvertAll(p => p.Name));
        Assert.Null(planes.Load("Bought"));
        Assert.Equal(CampaignProfileStore.Serialize(profile), ProfileText(store, Pilot));
    }

    [Fact]
    public void TheWalletNeedsASeatedProfileAndABuildStore()
    {
        var (feature, _, _) = Open();
        Assert.Null(feature.Wallet());
        feature.ContinuePlayer(Pilot);
        Assert.Null(feature.Wallet());
    }

    [Fact]
    public void FlyMissionSavesTheProfileAndCarriesOneSeatPerHuman()
    {
        var (feature, store, _) = Open();
        feature.ContinuePlayer(Pilot);
        var profile = feature.Profile!;
        profile.SelectedPlane = 1;
        feature.SetMission(3);
        feature.Field.SetPlayers(2);
        var guest = feature.Field.Plane(1)!;

        var exit = feature.BuildExit(new IReadOnlyList<int>[] { new[] { 0 }, new[] { 1, 2 } });

        Assert.NotNull(exit);
        Assert.Equal(Pilot, exit!.Profile);
        Assert.Equal(3, exit.MissionSeq);
        Assert.Equal(2, exit.Seats.Count);
        Assert.Equal("node5", exit.Seats[0].PlaneNode);
        Assert.Equal(new[] { 0 }, exit.Seats[0].Pads);
        Assert.NotNull(exit.Seats[0].Fit);
        Assert.Null(exit.Seats[0].Custom);
        Assert.Equal($"node{guest.Airframe}", exit.Seats[1].PlaneNode);
        Assert.Equal(new[] { 1, 2 }, exit.Seats[1].Pads);
        Assert.Equal(1, store.Load(Pilot)!.SelectedPlane);
        Assert.Equal(CampaignProfileStore.Serialize(profile), ProfileText(store, Pilot));
    }

    [Fact]
    public void FlyMissionWithNobodySeatedIsNothing()
    {
        var (feature, _, dir) = Open();

        Assert.Null(feature.BuildExit(new IReadOnlyList<int>[] { new int[0] }));
        Assert.False(Directory.Exists(dir));
    }

    /// <summary>A flown mission is written by the session's director through the progression rules
    /// and one save; what the menu reads back on the return is that file.</summary>
    [Fact]
    public void AFlownMissionsRecordIsWhatTheReturnReads()
    {
        var (feature, store, _) = Open();
        feature.ContinuePlayer(Pilot);
        var flown = store.Load(Pilot)!;
        var recorded = CampaignProgression.Record(flown, new MissionAttempt(
            0, CampaignProgression.PrimaryObjectiveMask, 300_000, 400, 120, flown.Planes[0].Airframe, flown.Planes[0].Name));
        store.Save(flown);
        Assert.True(recorded.Advanced);

        using var doc = JsonDocument.Parse(ProfileText(store, Pilot));
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("missionsCompleted").GetInt32());
        Assert.Equal(recorded.MoneyPaid, root.GetProperty("funds").GetInt32());
        var result = root.GetProperty("missionResults")[0];
        Assert.Equal(0, result.GetProperty("seq").GetInt32());
        Assert.Equal(0, result.GetProperty("attempts").GetInt32());
        Assert.Equal(300_000, result.GetProperty("latest").GetProperty("timeMs").GetInt32());
        Assert.Equal(CampaignProgression.PrimaryObjectiveMask, result.GetProperty("best").GetProperty("completedMask").GetInt32());

        Assert.Equal(0, feature.Profile!.MissionsCompleted);
        Assert.True(feature.SeatProfile(Pilot));
        Assert.Equal(1, feature.Profile!.MissionsCompleted);
        Assert.Equal(1, feature.NextMissionSeq);
        Assert.False(feature.SeatProfile("Nobody"));
        Assert.Equal(Pilot, feature.Profile?.Name);
    }

    [Fact]
    public void ResumeReReadsTheSeatedProfile()
    {
        var (feature, store, _) = Open();
        feature.ContinuePlayer(Pilot);
        var outside = store.Load(Pilot)!;
        outside.Funds = 777;
        store.Save(outside);

        feature.Resume();

        Assert.Equal(777, feature.Profile?.Funds);
    }

    [Fact]
    public void ChangePlaneIsAnsweredPerCrewSlotAgainstTheOwnedCountLessOneOnTheGrantMissions()
    {
        var (feature, _, _) = Open();
        feature.ContinuePlayer(Pilot);
        feature.SetMission(3);
        Assert.False(feature.ChangePlaneAllowed(0));
        Assert.False(feature.ChangePlaneAllowed(1));

        feature.Profile!.Planes.Add(new OwnedPlane { Name = "Third", Airframe = 2 });
        Assert.True(feature.ChangePlaneAllowed(0));
        Assert.True(feature.ChangePlaneAllowed(1));

        // Missions 13 and 17 bar the pilot's button outright and are counted one plane lower, so
        // three owned falls under the floor there and takes the wingman's button with it.
        feature.SetMission(12);
        Assert.False(feature.ChangePlaneAllowed(0));
        Assert.False(feature.ChangePlaneAllowed(1));

        feature.Profile.Planes.Add(new OwnedPlane { Name = "Fourth", Airframe = 2 });
        Assert.False(feature.ChangePlaneAllowed(0));
        Assert.True(feature.ChangePlaneAllowed(1));

        feature.SetMission(16);
        Assert.False(feature.ChangePlaneAllowed(0));
        Assert.True(feature.ChangePlaneAllowed(1));
        feature.SetMission(17);
        Assert.True(feature.ChangePlaneAllowed(0));
        Assert.True(feature.ChangePlaneAllowed(1));
    }

    [Fact]
    public void TheFlightCheckGrantsTheMissionsOwnAircraftOnceAndSelectsItOnEveryEntry()
    {
        var (feature, store, _) = Open();
        feature.ContinuePlayer(Pilot);
        feature.SetMission(3);
        Assert.False(feature.GrantMissionAircraft());
        Assert.Equal(2, feature.Profile!.Planes.Count);

        feature.SetMission(12);
        Assert.True(feature.GrantMissionAircraft());
        var granted = feature.Profile.Planes[2];
        Assert.Equal("Red Hot Spender", granted.Name);
        Assert.Equal(7, granted.Airframe);
        Assert.True(granted.Special);
        Assert.Equal(2, feature.Profile.SelectedPlane);
        Assert.Equal(new[] { 7 }, feature.Profile.GrantedAircraft);
        Assert.Equal(3, store.Load(Pilot)!.Planes.Count);

        // A second entry grants nothing and writes nothing, and one made after the plane screen
        // moved the pilot's pick puts the story aircraft back.
        Assert.False(feature.GrantMissionAircraft());
        feature.Profile.SelectedPlane = 0;
        Assert.True(feature.GrantMissionAircraft());
        Assert.Equal(2, feature.Profile.SelectedPlane);
        Assert.Equal(3, feature.Profile.Planes.Count);
    }

    [Fact]
    public void TheScrapbookEntryCountsEveryOpeningAndTheCaptureResolvesInTheProfileDirectory()
    {
        var (feature, store, _) = Open();
        feature.ContinuePlayer(Pilot);

        feature.EnterScrapbook(2);
        feature.EnterScrapbook(2);
        Assert.Equal(2, feature.MissionSeq);
        Assert.Equal(2, feature.ScrapbookEntry);

        Assert.Null(feature.CapturePath("Snap_01.png"));
        File.WriteAllText(Path.Combine(store.DirFor(Pilot), "Snap_01.png"), "not a png");
        Assert.Equal(Path.Combine(store.DirFor(Pilot), "Snap_01.png"), feature.CapturePath("Snap_01.png"));
        feature.SetScrapbookZoom(3, 1, 2);
        Assert.Equal((3, 1, 2), feature.ZoomTarget);
    }

    [Fact]
    public void WithoutADataRootTheMissionAndTheBriefingReadAsAbsent()
    {
        var (feature, _, _) = Open();
        feature.ContinuePlayer(Pilot);
        feature.SetMission(0);

        Assert.Null(feature.Mission);
        Assert.False(feature.MissionHasWingman);
        Assert.Null(feature.Briefing);
    }

    [Fact]
    public void DiscardDropsEverythingAndTouchesNoFile()
    {
        var (feature, store, dir) = Open();
        feature.ContinuePlayer(Pilot);
        feature.SetMission(4);
        feature.SetAmmoSlot(1);
        feature.SetPlaneSlot(1);
        feature.EnterScrapbook(1);
        feature.Field.SetPlayers(3);
        feature.Field.Advance();
        var files = Snapshot(dir);

        feature.Discard();

        Assert.False(feature.IsOpen);
        Assert.Null(feature.Store);
        Assert.Null(feature.Profile);
        Assert.Empty(feature.Roster);
        Assert.Equal(-1, feature.MissionSeq);
        Assert.Equal(0, feature.AmmoSlot);
        Assert.Equal(0, feature.PlaneSlot);
        Assert.Equal(0, feature.ScrapbookEntry);
        Assert.Equal(1, feature.Field.Players);
        Assert.Equal(0, feature.Field.Current);
        Assert.False(feature.Field.Locked);
        Assert.Equal(files, Snapshot(dir));
        Assert.Equal("No campaign is open.", feature.ContinuePlayer(Pilot));
        Assert.Equal(Pilot, store.LastPlayed);
    }

    [Fact]
    public void OpeningAgainDropsTheSeatedProfileAndReadsTheNewStore()
    {
        var (feature, _, _) = Open();
        feature.ContinuePlayer(Pilot);
        var other = new CampaignProfileStore(Path.Combine(TestData.TempDir(), "Profiles"));
        other.Save(CampaignProfileDef.NewProfile("Nathan"));

        feature.Open(other);

        Assert.Null(feature.Profile);
        Assert.Equal(new[] { "Nathan" }, feature.Roster);
        Assert.Same(other, feature.Store);
    }

    [Fact]
    public void AGuestFliesAPlaneOfTheHostsHangarAndWritesNothing()
    {
        var (feature, store, dir) = Open();
        feature.ContinuePlayer(Pilot);
        var files = Snapshot(dir);
        var hangar = Words(("Gypsy Magic", 5, 0), ("Kestrel", 7, Free), ("Osprey", 2, Free));

        // A guest with no pick yet opens on the first plane nobody holds.
        feature.OpenGuest("Zachary", 4, hangar, 1);
        Assert.True(feature.IsGuest);
        Assert.True(feature.IsOpen);
        Assert.Null(feature.Store);
        Assert.Equal(new[] { 5, 7, 2, CoopPlanePool.StockAirframe }, feature.Profile!.Planes.ConvertAll(p => p.Airframe));
        Assert.All(feature.Profile.Planes, plane => Assert.False(plane.Special));
        Assert.Equal((1, 7), (feature.GuestPlane, feature.GuestAirframe));
        Assert.Equal(4, feature.NextMissionSeq);
        Assert.True(feature.ChangePlaneAllowed(0));
        Assert.False(feature.ChangePlaneAllowed(1));

        feature.SetMission(12);
        Assert.False(feature.GrantMissionAircraft());
        feature.RecordGuestResult(12, null, 0b11, 900);
        var exit = feature.BuildExit(new IReadOnlyList<int>[] { new[] { 0 }, new[] { 1 } })!;
        Assert.Equal("", exit.Profile);
        Assert.Equal(12, exit.MissionSeq);
        Assert.Null(exit.Wingman);
        var seat = Assert.Single(exit.Seats);
        Assert.Equal("node7", seat.PlaneNode);
        Assert.Null(seat.Custom);

        var run = CampaignProgression.ResultOf(feature.Profile, 12)!.Latest!;
        Assert.Equal(0b11, run.CompletedMask);
        Assert.Equal(900, run.Money);

        // The host's hangar growing keeps the pick by name, and the results.
        var grown = Words(("Gypsy Magic", 5, 0), ("Kestrel", 7, 1), ("Osprey", 2, Free), ("Hawk", 3, Free));
        Assert.Equal(-1, feature.FollowHost(5, grown));
        Assert.Equal((1, 7, 5), (feature.GuestPlane, feature.GuestAirframe, feature.Profile.Planes.Count));
        Assert.NotNull(CampaignProgression.ResultOf(feature.Profile, 12));

        // A later seat holding the pick is the host not having heard it yet, so the pick stands.
        Assert.Equal(-1, feature.FollowHost(5, Words(("Gypsy Magic", 5, 0), ("Kestrel", 7, 2), ("Osprey", 2, Free), ("Hawk", 3, Free))));
        Assert.Equal(1, feature.GuestPlane);

        // An earlier seat holding it moves the guest onto the plane the host gave its seat, and says who.
        Assert.Equal(0, feature.FollowHost(5, Words(("Gypsy Magic", 5, Free), ("Kestrel", 7, 0), ("Osprey", 2, Free), ("Hawk", 3, 1))));
        Assert.Equal((3, 3), (feature.GuestPlane, feature.GuestAirframe));
        Assert.Equal("P1 is already flying this plane. Each player must fly a different plane.", feature.SeatRefusal(0));

        // Nothing a guest did reached the disk, and the host's roster still reads as it was.
        Assert.Equal(files, Snapshot(dir));
        Assert.Equal(Pilot, store.LastPlayed);

        // ABLE-TO-FAIL CONTROL: the same exit from a seated profile saves it and names it.
        var (seated, seatedStore, _) = Open();
        seated.ContinuePlayer(Pilot);
        seated.SetMission(3);
        seated.Profile!.Funds = 4321;
        Assert.Equal(Pilot, seated.BuildExit(new IReadOnlyList<int>[] { new[] { 0 } })!.Profile);
        Assert.Equal(4321, seatedStore.Load(Pilot)!.Funds);
    }

    [Fact]
    public void AGuestReopensOnItsRememberedPickUntilItChangesPlane()
    {
        var (feature, _, _) = Open();
        var stored = CSVM.Net.CoopFit.Of(new[] { 1, 1, 0, 0 }, null);
        var hangar = Words(("Gypsy Magic", 5, 0), ("Kestrel", 7, Free), ("Osprey", 2, Free));
        hangar[1] = hangar[1] with { Fit = stored };
        var fit = CSVM.Net.CoopFit.Of(new[] { 3, 2, 0, 0 }, new[] { 4, 0, 0, 0, 0, 0, 0, 4 });
        var fresh = new OwnedPlane();
        var stock = CSVM.Net.CoopFit.Of(fresh.Ammo, fresh.Ordnance);

        // ABLE-TO-FAIL CONTROL: a fresh join opens on the first free plane at its stored fit.
        feature.OpenGuest("Zachary", 4, hangar, 1);
        Assert.Equal((7, stored), (feature.GuestAirframe, feature.GuestCoopFit));

        feature.OpenGuest("Zachary", 4, hangar, 1, plane: 2, fit: fit);
        Assert.Equal((2, fit), (feature.GuestAirframe, feature.GuestCoopFit));
        Assert.Equal(new[] { 1, 1, 0, 0 }, feature.Profile!.Planes[1].Ammo);

        // The host buying another plane rebuilds the hangar and keeps the pick's fit.
        Assert.Equal(-1, feature.FollowHost(5, Words(("Gypsy Magic", 5, 0), ("Kestrel", 7, Free), ("Osprey", 2, 1), ("Hawk", 3, Free))));
        Assert.Equal((2, fit), (feature.GuestAirframe, feature.GuestCoopFit));

        // Changing onto the stock Devastator flies it at rest.
        feature.CommitPlanes(feature.Profile!.Planes.Count - 1, null);
        Assert.Equal((CoopPlanePool.Stock, CoopPlanePool.StockAirframe, stock), (feature.GuestPlane, feature.GuestAirframe, feature.GuestCoopFit));

        // A remembered plane an earlier seat now holds opens on a free one at its stored fit.
        feature.OpenGuest("Zachary", 4, Words(("Gypsy Magic", 5, 0), ("Kestrel", 7, Free), ("Osprey", 2, 0)), 1, plane: 2, fit: fit);
        Assert.Equal((1, 7, stock), (feature.GuestPlane, feature.GuestAirframe, feature.GuestCoopFit));
    }

    [Fact]
    public void AHostSettlesEveryPickInSeatOrderAndNamesEachPlanesHolder()
    {
        var planes = new CustomPlaneStore(Path.Combine(TestData.TempDir(), "Planes"));
        planes.Save(new CustomPlaneDef { Name = "B", Airframe = 7, PaintPattern = 9 });
        var (feature, _, _) = Open(planes);
        feature.ContinuePlayer(Pilot);
        var profile = feature.Profile!;
        profile.Planes.Clear();
        profile.Planes.Add(new OwnedPlane { Name = "A", Airframe = 5 });
        profile.Planes.Add(new OwnedPlane { Name = "B", Airframe = 7, Ammo = new[] { 3, 1, 0, 0 } });
        profile.Planes.Add(new OwnedPlane { Name = "C", Airframe = 2 });
        profile.Planes.Add(new OwnedPlane { Name = "D", Airframe = 9 });
        profile.SelectedPlane = 0;
        profile.WingmanPlane = 2;

        // Seat 2 lost B to seat 1, seat 3 has no pick, seat 4 flies stock, seat 5 finds nothing free.
        feature.SetRemotePicks(new[] { 1, 1, CoopPlanePool.Unpicked, CoopPlanePool.Stock, 0 });
        Assert.Equal(new[] { 0, 1, 2, 3, CoopPlanePool.Stock, CoopPlanePool.Stock }, feature.SeatPlanes);

        var words = feature.CoopHangar();
        Assert.Equal(new byte[] { 0, 1, 2, 3 }, words.Select(w => w.Holder));
        Assert.Equal(new[] { "A", "B", "C", "D" }, words.Select(w => w.Name));
        Assert.All(words, w => Assert.Equal(4, w.Count));
        Assert.Equal(CSVM.Net.CoopFit.Of(profile.Planes[1].Ammo, profile.Planes[1].Ordnance), words[1].Fit);
        Assert.Equal((byte)9, words[1].Build?.PaintPattern);
        Assert.Null(words[2].Build);

        // The seated player's picker refuses a plane a network seat holds, naming it.
        Assert.Equal(2, feature.Field.HolderOf(0, profile.Planes[2]));

        // A guest opened on those words flies the host's build, and the stock Devastator none.
        var (guest, _, _) = Open();
        guest.OpenGuest("Zachary", 4, words, 1, plane: 1);
        Assert.Equal(9, guest.GuestBuildOf(guest.Field.Plane(0)!)?.PaintPattern);
        Assert.Equal(9, guest.BuildExit(new IReadOnlyList<int>[] { new[] { 0 } })!.Seats[0].Custom?.PaintPattern);
        Assert.Null(guest.GuestBuildOf(guest.Profile!.Planes[^1]));

        // ABLE-TO-FAIL CONTROL: a guest's own campaign names no hangar.
        Assert.Empty(guest.CoopHangar());
    }

    [Fact]
    public void AHostOffersEveryAirframeItsHangarHoldsAndAlwaysTheStarter()
    {
        var profile = CampaignProfileDef.NewProfile(Pilot);
        profile.Planes.Clear();
        profile.Planes.Add(new OwnedPlane { Name = "A", Airframe = 2 });
        profile.Planes.Add(new OwnedPlane { Name = "B", Airframe = 9 });
        Assert.Equal((1 << 2) | (1 << 5) | (1 << 9), CampaignFeature.HangarAirframes(profile));

        // ABLE-TO-FAIL CONTROL: nobody seated offers the starter alone.
        Assert.Equal(1 << 5, CampaignFeature.HangarAirframes(null));
    }

    [Fact]
    public void TheNameRuleIsTheOriginals()
    {
        Assert.True(CampaignFeature.ValidName("Nathan Zachary 2"));
        Assert.False(CampaignFeature.ValidName(string.Empty));
        Assert.False(CampaignFeature.ValidName("Nathan-Zachary"));
        Assert.False(CampaignFeature.ValidName(new string('z', 33)));
        Assert.True(CampaignFeature.ValidName(new string('z', 32)));
    }

    // A co-op host's hangar as a guest hears it: each plane's name, airframe and holding seat.
    private static CSVM.Net.CoopHangarMessage[] Words(params (string Name, int Airframe, int Holder)[] planes) =>
        planes.Select((p, at) => new CSVM.Net.CoopHangarMessage(
            (byte)at, (byte)planes.Length, (byte)p.Holder, (byte)p.Airframe, Name: p.Name)).ToArray();

    private static (CampaignFeature Feature, CampaignProfileStore Store, string Dir) Open(CustomPlaneStore? planes = null)
    {
        string dir = Path.Combine(TestData.TempDir(), "Profiles");
        var store = new CampaignProfileStore(dir);
        var feature = new CampaignFeature(UiStrings.Empty, airframe => $"node{airframe}");
        feature.Open(store, planes);
        return (feature, store, dir);
    }

    private static string ProfileText(CampaignProfileStore store, string name) =>
        File.ReadAllText(Path.Combine(store.DirFor(name), "profile.json"));

    private static string[] Names(string[] paths)
    {
        var names = new List<string>(paths.Length);
        foreach (string path in paths)
        {
            names.Add(Path.GetFileName(path));
        }

        names.Sort(System.StringComparer.Ordinal);
        return names.ToArray();
    }

    // Every file under the store with its text, so a write anywhere in it shows up as a difference.
    private static Dictionary<string, string> Snapshot(string dir)
    {
        var files = new Dictionary<string, string>();
        foreach (string path in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
        {
            files[Path.GetRelativePath(dir, path)] = File.ReadAllText(path);
        }

        return files;
    }
}
