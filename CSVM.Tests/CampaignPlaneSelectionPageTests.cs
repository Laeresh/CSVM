using System.IO;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI;
using Xunit;

namespace CSVM.Tests;

/// <summary>The plane selection screen: its rows with and without a wingman, the combos filled from
/// the profile, the cursor opening on the slot that was pressed, an open list owning the cursor,
/// the refusal of a plane the other crew slot flies, ACCEPT writing both picks and CANCEL restoring
/// them.</summary>
public class CampaignPlaneSelectionPageTests
{
    [Fact]
    public void WithNoWingmanTheScreenDrawsOneSlotAndTheTwoCommitButtons()
    {
        var page = NewPage(out _, wingman: false);

        Assert.Equal(4, page.RowCount); // pick, export, accept, cancel
        Assert.Equal(1, page.ActiveSlots);
        Assert.NotNull(page.Combo(0));
        Assert.Equal(BoardButton.ExportPlane, page.Button(1).Button);
        Assert.Equal("ACCEPT SELECTIONS", page.RowText(2));
        Assert.Equal("CANCEL SELECTIONS", page.RowText(3));
    }

    [Fact]
    public void WithAWingmanBothSlotsGetACombiAndAnExport()
    {
        var page = NewPage(out _, wingman: true);

        Assert.Equal(6, page.RowCount);
        Assert.Equal(2, page.ActiveSlots);
        Assert.NotNull(page.Combo(0));
        Assert.NotNull(page.Combo(2));
        Assert.Equal(1, page.Button(3).Slot);
    }

    [Fact]
    public void TheCombosCarryEveryOwnedPlaneNamedWithItsAirframe()
    {
        var page = NewPage(out _, wingman: true);

        var combo = page.Combo(0)!;
        Assert.Equal(3, combo.Entries.Count);
        Assert.Equal("Gypsy Magic - Airframe 5", combo.Entries[0]);
        Assert.Equal("Blue Streak - Airframe 3", combo.Entries[2]);
    }

    /// <summary>The pilot's and the wingman's combos open on the profile's own two indices, which
    /// differ from the start (the pair's division of labour, CampaignProfileDef.NewProfile).</summary>
    [Fact]
    public void EachComboOpensOnItsOwnSlotsPlane()
    {
        var page = NewPage(out _, wingman: true);

        Assert.Equal(0, page.Combo(0)!.Selected);
        Assert.Equal(1, page.Combo(2)!.Selected);
    }

    [Fact]
    public void TheCursorOpensOnTheSlotWhosePressOpenedTheScreen()
    {
        var page = NewPage(out var flow, wingman: true);

        flow.SetPlaneSlot(1);
        Assert.Equal(2, page.OpeningRow);

        flow.SetPlaneSlot(0);
        Assert.Equal(0, page.OpeningRow);
    }

    [Fact]
    public void ConfirmOnAPickRowOpensTheListAndTheNextConfirmTakesTheRowUnderTheCursor()
    {
        var page = NewPage(out var flow, wingman: false);
        var combo = page.Combo(0)!;

        Assert.True(page.Accept(0));
        Assert.True(combo.Open);

        combo.Move(1);
        Assert.True(page.Accept(0));
        Assert.False(combo.Open);
        Assert.Equal(1, combo.Selected);
    }

    [Fact]
    public void AClosedFieldStillSteps()
    {
        var page = NewPage(out _, wingman: false);

        Assert.True(page.Step(0, 1));
        Assert.Equal(1, page.Combo(0)!.Selected);
    }

    [Fact]
    public void BackClosesAnOpenListBeforeItLeavesTheScreen()
    {
        var page = NewPage(out _, wingman: false);
        page.Accept(0);

        Assert.True(page.Back());          // consumed: the list closed
        Assert.False(page.Combo(0)!.Open);
        Assert.False(page.Back());         // nothing left to close, so the flow leaves
    }

    [Fact]
    public void AcceptWritesBothPicksIntoTheProfileAndSavesIt()
    {
        var page = NewPage(out var flow, wingman: true);
        page.Step(2, 1);  // wingman off plane 1 first, or the pilot's step onto it is refused
        page.Step(0, 1);  // pilot onto plane 1

        Assert.True(page.Accept(4));

        var profile = flow.Profile!;
        Assert.Equal(1, profile.SelectedPlane);
        Assert.Equal(2, profile.WingmanPlane);
        Assert.Equal(1, flow.Store.Load(profile.Name)!.SelectedPlane);
        Assert.Equal(CampaignScreen.FlightCheck, flow.Screen);
    }

    [Fact]
    public void CancelLeavesBothCrewSlotsFlyingWhatTheyFlew()
    {
        var page = NewPage(out var flow, wingman: true);
        page.Step(2, 1);
        page.Step(0, 1);

        Assert.True(page.Accept(5));

        Assert.Equal(0, flow.Profile!.SelectedPlane);
        Assert.Equal(1, flow.Profile!.WingmanPlane);
        Assert.Equal(0, page.Combo(0)!.Selected);
        Assert.Equal(1, page.Combo(2)!.Selected);
        Assert.Equal(CampaignScreen.FlightCheck, flow.Screen);
    }

    /// <summary>The script's own refusal on message 10015: the pilot cannot take the plane the
    /// wingman is flying, and neither pick moves.</summary>
    [Fact]
    public void APickTheOtherCrewSlotFliesIsRefusedAndNeitherPickMoves()
    {
        var page = NewPage(out var flow, wingman: true);

        Assert.True(page.Step(0, 1)); // onto plane 1, which the wingman flies

        Assert.Equal("Pilot and Wingman must fly different planes.", flow.Modal?.Message);
        Assert.Equal(0, page.Combo(0)!.Selected);
        Assert.Equal(1, page.Combo(2)!.Selected);
    }

    /// <summary>The script's other arm, <c>!POA || !QOA</c>: a screen with one active crew slot has
    /// nothing to clash with, so every pick is taken.</summary>
    [Fact]
    public void OnAWingmanlessMissionTheSamePickIsPermitted()
    {
        var page = NewPage(out var flow, wingman: false);

        Assert.True(page.Step(0, 1));

        Assert.Null(flow.Modal);
        Assert.Equal(1, page.Combo(0)!.Selected);
    }

    [Fact]
    public void ARefusedListPickLeavesTheComboClosedOnItsOldValueAfterTheDialogIsAnswered()
    {
        var page = NewPage(out var flow, wingman: true);
        var combo = page.Combo(0)!;
        page.Accept(0);
        combo.Move(1); // the cursor onto the wingman's plane

        Assert.True(page.Accept(0));
        Assert.NotNull(flow.Modal);

        Assert.True(flow.Accept()); // the dialog answered, not the screen underneath
        Assert.Null(flow.Modal);
        Assert.False(combo.Open);
        Assert.Equal(0, combo.Selected);
    }

    /// <summary>The one decoded rating, checked against both aircraft the reference screenshots
    /// show: Bloodhawk reads Excellent and Devastator Average.</summary>
    [Fact]
    public void TheAgilityRatingMatchesTheReferenceScreenshots()
    {
        Assert.Equal(4, PlaneRatings.Agility(3));
        Assert.Equal(2, PlaneRatings.Agility(5));
    }

    [Fact]
    public void TheScreenIsRegisteredAndComposesOverItsOwnBackground()
    {
        Assert.True(CampaignFlow.HasPage(CampaignScreen.PlaneSelection));
        var page = NewPage(out var flow, wingman: true);

        var board = CampaignBoards.For(page, flow.Row);

        Assert.Contains(board.Backdrop, art => art.Art.Name == "PS_BackGround.jpg");
        Assert.Contains(board.Lines, line => line.Text == "PLANE SELECTION");
        Assert.Contains(board.Lines, line => line.Text == "PILOT");
        Assert.Contains(board.Lines, line => line.Text == "WINGMAN");
        Assert.Contains(board.Lines, line => line.Text.StartsWith("AGILITY:"));
        Assert.Equal(2, System.Linq.Enumerable.Count(
            board.Plaques, p => p.Label == "Export"));
    }

    [Fact]
    public void AnOpenListDrawsAsAnOverlayOverTheScreen()
    {
        var page = NewPage(out var flow, wingman: false);
        page.Accept(0);

        var board = CampaignBoards.For(page, 0);

        var panel = Assert.Single(board.Overlays);
        Assert.Equal(3, panel.Lines.Count); // the three entries, no scrollbar at three of thirteen
    }

    private static CampaignPlaneSelectionPage NewPage(out CampaignFlow flow, bool wingman)
    {
        string dir = Path.Combine(TestData.TempDir(), "Profiles");
        var store = new CampaignProfileStore(dir);
        flow = new CampaignFlow(store, UiStrings.Empty);
        var profile = CampaignProfileDef.NewProfile("Zachary");
        profile.Planes.Add(new OwnedPlane { Name = "Blue Streak", Airframe = 3 });
        store.Save(profile);
        flow.SelectProfile(profile);
        flow.SetPlaneSlot(0);
        flow.GoTo(CampaignScreen.PlaneSelection);
        return new CampaignPlaneSelectionPage(flow, wingman);
    }
}
