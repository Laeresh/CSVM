using System;
using System.IO;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Session;
using CSVM.UI;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The Original campaign over the hand-authored layout fixture and a scratch profile store: the
/// Campaign row's door, the profile screen's box, roster rows, refusals and its two-answer delete,
/// the cabin's four plaques under the pointer and the keyboard, the briefing, the flight check's
/// launch, the ammo screen's way back, the hangar over the wallet with the cabin as its return,
/// the two flight returns' mapping, and every door out leaving no open campaign. Every rectangle
/// here is the fixture's invented geometry; the game's is read the same way.
/// </summary>
public class OriginalCampaignTests : IDisposable
{
    private static readonly MenuCommands Accept = new() { Accept = true };
    private static readonly MenuCommands Back = new() { Back = true };
    private static readonly MenuCommands Down = new() { MoveY = 1 };
    private static readonly MenuCommands Up = new() { MoveY = -1 };

    private readonly string _dir;
    private readonly CampaignProfileStore _store;
    private readonly CustomPlaneStore _planes;

    public OriginalCampaignTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "csvm-original-campaign-" + Guid.NewGuid().ToString("N"));
        _store = new CampaignProfileStore(Path.Combine(_dir, "Profiles"));
        _planes = new CustomPlaneStore(Path.Combine(_dir, "Planes"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void TheCampaignRowStandsOnlyOverAFeatureAndAStoreAndOpensTheProfileScreen()
    {
        var shell = Shell(out var campaign, out _);
        var row = shell.Rows.Single(r => r.Key == OriginalShell.CampaignKey);
        Assert.True(row.Enabled);

        var step = shell.Step(Pointer(row.X + 2f, row.Y + 2f, pressed: true, clicked: true));

        Assert.Equal(OriginalScreen.CampaignRoster, shell.Screen);
        Assert.True(campaign.IsOpen && shell.CampaignOpen);
        Assert.Contains(OriginalCues.Click, step.Cues);
        Assert.True(shell.CapturingText);
        Assert.Equal(new[] { "ROW:0", "Continue", "DeletePlayer", "CancelProfile" }, shell.Rows.Select(r => r.Key));
        Assert.Equal(0, shell.Focus);
        // The plaques sit at the fixture's own rows, CONTINUE keeping its pinned strip.
        var cancel = shell.Rows.Single(r => r.Key == "CancelProfile");
        Assert.Equal((450f, 540f, 240f, 50f), (cancel.X, cancel.Y, cancel.Width, cancel.Height));
        Assert.Equal("PM_B_Cancel.png", cancel.Art!.Name);
        var start = shell.Rows.Single(r => r.Key == "Continue");
        Assert.Equal((470f, 290f), (start.X, start.Y));
        Assert.Equal("CM_B_Start.png", start.Art!.Name);
    }

    [Fact]
    public void TypingFeedsTheNameBoxWithTheEditBoxCuesAndContinueCreatesAndSeatsThePlayer()
    {
        var shell = Shell(out var campaign, out _);
        OpenCampaign(shell);

        var step = shell.Step(new MenuCommands { Typed = "Zac/k" });
        Assert.Equal("Zack", shell.RosterName);
        Assert.Equal(new[] { OriginalCues.Text, OriginalCues.Text, OriginalCues.Text, OriginalCues.TextError, OriginalCues.Text }, step.Cues);
        Assert.Contains(shell.Compose().Lines, l => l.Row == 0 && l.Text == "Zack_");
        shell.Step(new MenuCommands { Erase = true });
        Assert.Equal("Zac", shell.RosterName);

        // Enter in the box is the script's own commit path.
        Assert.Null(_store.Load("Zac"));
        step = shell.Step(Accept);
        Assert.Equal(OriginalScreen.CampaignCabin, shell.Screen);
        Assert.Equal("Zac", campaign.Profile?.Name);
        Assert.NotNull(_store.Load("Zac"));
        Assert.Equal("Zac", _store.LastPlayed);
        Assert.Contains(OriginalCues.Click, step.Cues);
    }

    [Fact]
    public void ContinueWithNoNameRaisesTheRefusalAsADialogWhoseOkIsTheOnlyRow()
    {
        var shell = Shell(out var campaign, out _);
        OpenCampaign(shell);
        var start = shell.Rows.Single(r => r.Key == "Continue");

        shell.Step(Pointer(start.X + 2f, start.Y + 2f, pressed: true, clicked: true));

        Assert.NotNull(shell.Dialog);
        Assert.Equal(campaign.Strings.Text(200, "You must enter a player name."), shell.Dialog!.Message);
        Assert.False(shell.CapturingText);
        var ok = Assert.Single(shell.Rows);
        Assert.Equal(OriginalShell.DialogOkKey, ok.Key);
        // The messagebox button at its row inside the centred 410x300 art.
        Assert.Equal((365f, 400f, 240f, 50f), (ok.X, ok.Y, ok.Width, ok.Height));
        var panel = shell.Compose().Overlays.First(o => o.Lines.Count > 0);
        Assert.Contains(panel.Lines, l => l.Text == shell.Dialog.Message);
        Assert.Null(_store.Load(string.Empty));

        shell.Step(Pointer(ok.X + 2f, ok.Y + 2f, pressed: true, clicked: true));
        Assert.Null(shell.Dialog);
        Assert.Equal(OriginalScreen.CampaignRoster, shell.Screen);
        Assert.Equal(4, shell.Rows.Count);
    }

    [Fact]
    public void ARosterRowFillsTheBoxOnOneClickAndStartsOnTheSecondWithTheSelectionBarBehindIt()
    {
        _store.Save(CampaignProfileDef.NewProfile("Nathan"));
        _store.Save(CampaignProfileDef.NewProfile("Zachary"));
        var shell = Shell(out var campaign, out _);
        OpenCampaign(shell);
        Assert.Equal(6, shell.Rows.Count);
        var nathan = shell.Rows[1];
        Assert.Equal((250f, 360f, 300f, 24f), (nathan.X, nathan.Y, nathan.Width, nathan.Height));
        Assert.Equal(OriginalRowKind.ListRow, nathan.Kind);

        var step = shell.Step(Pointer(nathan.X + 4f, nathan.Y + 4f));
        Assert.Empty(step.Cues);
        Assert.Contains(shell.Compose().Fills, f => f.Border && f.X == nathan.X && f.Y == nathan.Y);
        step = shell.Step(Pointer(nathan.X + 4f, nathan.Y + 4f, pressed: true, clicked: true));
        Assert.Equal("Nathan", shell.RosterName);
        Assert.Equal(OriginalScreen.CampaignRoster, shell.Screen);
        Assert.Empty(step.Cues);
        Assert.Contains(shell.Compose().Fills, f => !f.Border && f.X == nathan.X && f.Y == nathan.Y && f.R == 0x80);
        Assert.DoesNotContain(shell.Compose().Lines, l => l.Text.StartsWith("✓", StringComparison.Ordinal));

        shell.Step(Pointer(nathan.X + 4f, nathan.Y + 4f, pressed: false, clicked: false));
        shell.Step(Pointer(nathan.X + 4f, nathan.Y + 4f, pressed: true, clicked: true));
        Assert.Equal(OriginalScreen.CampaignCabin, shell.Screen);
        Assert.Equal("Nathan", campaign.Profile?.Name);
    }

    [Fact]
    public void TheBoxOpensOnTheLastPlayerSeatedAndDeletePlayerAsksWithTwoAnswersOpeningOnYes()
    {
        _store.Save(CampaignProfileDef.NewProfile("Nathan"));
        _store.Save(CampaignProfileDef.NewProfile("Zachary"));
        _store.RecordLastPlayed("Zachary");
        var shell = Shell(out var campaign, out _);
        OpenCampaign(shell);
        Assert.Equal("Zachary", shell.RosterName);

        var delete = shell.Rows.Single(r => r.Key == "DeletePlayer");
        shell.Step(Pointer(delete.X + 2f, delete.Y + 2f, pressed: true, clicked: true));
        Assert.NotNull(shell.Dialog);
        Assert.Equal(new[] { OriginalShell.DialogYesKey, OriginalShell.DialogNoKey }, shell.Rows.Select(r => r.Key));
        Assert.Equal(OriginalShell.DialogYesKey, shell.FocusedKey);
        Assert.Equal((195f + 70f, 150f + 250f), (shell.Rows[0].X, shell.Rows[0].Y));

        shell.Step(Back);
        Assert.Null(shell.Dialog);
        Assert.NotNull(_store.Load("Zachary"));
        Assert.Equal("DeletePlayer", shell.FocusedKey);

        shell.Step(Accept);
        shell.Step(Down);
        Assert.Equal(OriginalShell.DialogNoKey, shell.FocusedKey);
        shell.Step(Up);
        Assert.Equal(OriginalShell.DialogYesKey, shell.FocusedKey);
        shell.Step(Accept);
        Assert.Null(_store.Load("Zachary"));
        Assert.NotNull(_store.Load("Nathan"));
        Assert.Equal(string.Empty, shell.RosterName);
        Assert.Equal(new[] { "Nathan" }, campaign.Roster);
        Assert.Equal(5, shell.Rows.Count);
    }

    [Fact]
    public void TheCabinsPlaquesTakeThePointerAndTheKeyboardAndReturnToMainMenuClosesTheCampaign()
    {
        var shell = Shell(out var campaign, out _);
        Seat(shell, "Zachary");
        Assert.Equal(new[] { "NextMission", "PreviousMissions", "PlaneConstruction", "ReturnToMainMenu" }, shell.Rows.Select(r => r.Key));
        Assert.Equal("NextMission", shell.FocusedKey);
        var previous = shell.Rows[1];
        Assert.Equal((360f, 540f, 240f, 50f), (previous.X, previous.Y, previous.Width, previous.Height));

        var step = shell.Step(Pointer(previous.X + 3f, previous.Y + 3f));
        Assert.Equal("PreviousMissions", shell.FocusedKey);
        Assert.Equal(new[] { OriginalCues.Rollover }, step.Cues);
        var plaque = shell.Compose().Plaques.Single(p => p.Art.Name == "PM_B_Previous.png");
        Assert.Equal(2, plaque.Frame);
        shell.Step(Pointer(previous.X + 3f, previous.Y + 3f, pressed: true));
        Assert.Equal(3, shell.Compose().Plaques.Single(p => p.Art.Name == "PM_B_Previous.png").Frame);

        shell.Step(Pointer(previous.X + 3f, previous.Y + 3f, pressed: true, clicked: true));
        Assert.Equal(OriginalScreen.CampaignPreviousMissions, shell.Screen);
        Assert.Equal("ViewMission", shell.FocusedKey);
        shell.Step(Back);
        Assert.Equal(OriginalScreen.CampaignCabin, shell.Screen);

        shell.Step(Down);
        shell.Step(Down);
        Assert.Equal("ReturnToMainMenu", shell.FocusedKey);
        shell.Step(Down);
        Assert.Equal("NextMission", shell.FocusedKey);
        shell.Step(Up);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.TopLevel, shell.Screen);
        Assert.False(campaign.IsOpen);
        Assert.False(shell.CampaignOpen);
    }

    [Fact]
    public void BackFromTheCabinReturnsToTheProfileScreenAndBackAgainLeaves()
    {
        var shell = Shell(out var campaign, out _);
        Seat(shell, "Zachary");

        shell.Step(Back);
        Assert.Equal(OriginalScreen.CampaignRoster, shell.Screen);
        Assert.True(campaign.IsOpen);
        Assert.Equal("Zachary", shell.RosterName);

        shell.Step(Back);
        Assert.Equal(OriginalScreen.TopLevel, shell.Screen);
        Assert.False(campaign.IsOpen);
    }

    [Fact]
    public void NextMissionOpensTheBriefingWhoseThreePlaquesLeadBackAndOnAndReplayRestartsTheReveal()
    {
        var shell = Shell(out var campaign, out _);
        Seat(shell, "Zachary");

        shell.Step(Accept);
        Assert.Equal(OriginalScreen.CampaignBriefing, shell.Screen);
        Assert.Equal(0, campaign.MissionSeq);
        Assert.Equal(new[] { "ReplayBriefing", "ReturnToCabin", "GoToFlightCheck" }, shell.Rows.Select(r => r.Key));
        var flightCheck = shell.Rows[2];
        Assert.Equal((597f, 560f, 196f, 32f), (flightCheck.X, flightCheck.Y, flightCheck.Width, flightCheck.Height));
        Assert.Equal(BoardArtLibrary.Rimage, flightCheck.Art!.Library);
        // No extraction here, so the briefing has no state; the plaques still stand.
        Assert.Equal(0, shell.NarrationStarts);
        Assert.False(shell.AdvanceBriefing(1.0 / 60.0));

        shell.Step(Down);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.CampaignCabin, shell.Screen);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.CampaignBriefing, shell.Screen);
        shell.Step(Back);
        Assert.Equal(OriginalScreen.CampaignCabin, shell.Screen);
    }

    [Fact]
    public void TheFlightCheckOpensAmmoAndFliesAsOneCampaignMissionExitWithTheProfileSaved()
    {
        var shell = Shell(out var campaign, out _);
        Seat(shell, "Zachary");
        shell.Step(Accept);
        var go = shell.Rows.Single(r => r.Key == "GoToFlightCheck");
        shell.Step(Pointer(go.X + 2f, go.Y + 2f, pressed: true, clicked: true));
        Assert.Equal(OriginalScreen.CampaignFlightCheck, shell.Screen);
        // The pilot heading is text the cursor steps over; CHANGE PLANE is barred under three planes.
        Assert.Equal(new[] { "ROW:0", "ChangeAmmo", "ReturnToBriefing", "FlyMission" }, shell.Rows.Select(r => r.Key));
        Assert.False(shell.Rows[0].Enabled);
        Assert.False(shell.Rows[0].Visible);
        Assert.Equal("ChangeAmmo", shell.FocusedKey);
        var ammo = shell.Rows[1];
        Assert.Equal((270f, 131f, 160f, 28f), (ammo.X, ammo.Y, ammo.Width, ammo.Height));

        shell.Step(Accept);
        Assert.Equal(OriginalScreen.CampaignAmmo, shell.Screen);
        Assert.Equal(0, campaign.AmmoSlot);
        Assert.Contains(shell.Rows, r => r.Key == "AcceptLoadout" && r.X == 340f && r.Y == 550f);
        shell.Step(Back);
        Assert.Equal(OriginalScreen.CampaignFlightCheck, shell.Screen);

        shell.Step(Down);
        Assert.Equal("ReturnToBriefing", shell.FocusedKey);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.CampaignBriefing, shell.Screen);
        shell.Step(Down);
        shell.Step(Down);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.CampaignFlightCheck, shell.Screen);

        var fly = shell.Rows.Single(r => r.Key == "FlyMission");
        var profile = campaign.Profile!;
        var step = shell.Step(Pointer(fly.X + 2f, fly.Y + 2f, pressed: true, clicked: true));
        var exit = Assert.IsType<CampaignMissionExit>(step.Exit);
        Assert.Equal(("Zachary", 0, 1), (exit.Profile, exit.MissionSeq, exit.Seats.Count));
        Assert.Equal("node5", exit.Seats[0].PlaneNode);
        Assert.Equal(CampaignProfileStore.Serialize(profile), File.ReadAllText(Path.Combine(_store.DirFor("Zachary"), "profile.json")));
        Assert.Contains(OriginalCues.Click, step.Cues);
    }

    [Fact]
    public void PlaneConstructionOpensTheHangarOverTheWalletWithTheCabinAsItsReturn()
    {
        var shell = Shell(out var campaign, out var hangar);
        Seat(shell, "Zachary");
        var door = shell.Rows.Single(r => r.Key == "PlaneConstruction");

        shell.Step(Pointer(door.X + 2f, door.Y + 2f, pressed: true, clicked: true));
        Assert.Equal(OriginalScreen.PlaneName, shell.Screen);
        Assert.True(hangar.IsOpen);
        Assert.NotNull(hangar.Wallet);
        Assert.Equal(campaign.Profile!.Funds, hangar.Wallet!.Funds);

        shell.Step(Back);
        Assert.Equal(OriginalScreen.CampaignCabin, shell.Screen);
        Assert.False(hangar.IsOpen);
        Assert.True(campaign.IsOpen);
        Assert.Equal("PlaneConstruction", shell.FocusedKey);
    }

    [Fact]
    public void TheTwoFlightReturnsLandOnTheCabinAndTheBookAndAReturnToTheTopLevelClosesTheCampaign()
    {
        _store.Save(CampaignProfileDef.NewProfile("Zachary"));
        var shell = Shell(out var campaign, out _);

        shell.OpenCampaignOver(_store);
        Assert.True(shell.ShowCabin("Zachary"));
        Assert.Equal(OriginalScreen.CampaignCabin, shell.Screen);
        Assert.Equal("Zachary", campaign.Profile?.Name);
        Assert.Equal("Zachary", _store.LastPlayed);

        shell.OpenCampaignOver(_store);
        Assert.True(shell.ShowScrapbook("Zachary", 0));
        Assert.Equal(OriginalScreen.CampaignScrapbook, shell.Screen);
        Assert.Equal(0, campaign.MissionSeq);
        Assert.Equal(1, campaign.ScrapbookEntry);
        Assert.Equal("ReturnToCabin", shell.FocusedKey);
        Assert.Contains(shell.Rows, r => r.Key == "ReturnToCabin" && r.X == 590f && r.Y == 560f);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.CampaignCabin, shell.Screen);

        shell.OpenCampaignOver(_store);
        Assert.False(shell.ShowCabin("Nobody"));
        Assert.Equal(OriginalScreen.CampaignRoster, shell.Screen);

        shell.ReturnToTopLevel();
        Assert.Equal(OriginalScreen.TopLevel, shell.Screen);
        Assert.False(campaign.IsOpen);
        Assert.False(shell.CampaignOpen);
    }

    [Fact]
    public void TheBookBacksToWhateverOpenedItAndTheContentsRowsAreHitAtTheirWindow()
    {
        var profile = CampaignProfileDef.NewProfile("Zachary");
        for (int seq = 0; seq < 2; seq++)
        {
            CampaignProgression.Record(profile, new MissionAttempt(seq, CampaignProgression.PrimaryObjectiveMask, 300_000, 400, 120, 5, "Gypsy Magic"));
        }

        _store.Save(profile);
        var shell = Shell(out var campaign, out _);
        Seat(shell, "Zachary");
        shell.Step(Down);
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.CampaignPreviousMissions, shell.Screen);
        // Two mission rows at the fixture's list box, then the buttons.
        Assert.Equal((410f, 150f, 330f, 60f), (shell.Rows[0].X, shell.Rows[0].Y, shell.Rows[0].Width, shell.Rows[0].Height));
        Assert.Equal((410f, 210f), (shell.Rows[1].X, shell.Rows[1].Y));
        Assert.Contains(shell.Rows, r => r.Key == "ViewMission" && r.X == 440f && r.Y == 500f);

        var second = shell.Rows[1];
        shell.Step(Pointer(second.X + 5f, second.Y + 5f, pressed: true, clicked: true));
        Assert.Equal(OriginalScreen.CampaignPreviousMissions, shell.Screen);
        Assert.Contains(shell.Compose().Fills, f => f.Y == second.Y && !f.Border);
        var view = shell.Rows.Single(r => r.Key == "ViewMission");
        shell.Step(Pointer(view.X + 2f, view.Y + 2f, pressed: true, clicked: true));
        Assert.Equal(OriginalScreen.CampaignScrapbook, shell.Screen);
        Assert.Equal(1, campaign.MissionSeq);
        shell.Step(Back);
        Assert.Equal(OriginalScreen.CampaignPreviousMissions, shell.Screen);

        var replay = shell.Rows.Single(r => r.Key == "ReplayMission");
        shell.Step(Pointer(replay.X + 2f, replay.Y + 2f, pressed: true, clicked: true));
        Assert.Equal(OriginalScreen.CampaignBriefing, shell.Screen);
        shell.Step(Back);
        Assert.Equal(OriginalScreen.CampaignPreviousMissions, shell.Screen);
        shell.Step(Back);
        Assert.Equal(OriginalScreen.CampaignCabin, shell.Screen);
    }

    [Fact]
    public void TheExportPressRaisesTheOneButtonBoxInItsOwnInkOverThePaperScreenAndWritesTheGivenStore()
    {
        _store.Save(CampaignProfileDef.NewProfile("Zachary"));
        var scratchPlanes = new CustomPlaneStore(Path.Combine(_dir, "AidPlanes"));
        var shell = Shell(out var campaign, out _);
        shell.OpenCampaignOver(_store, scratchPlanes);
        Assert.True(shell.ShowCabin("Zachary"));
        shell.ShowMissionScreen(OriginalScreen.CampaignPlaneSelection);
        Assert.Equal(OriginalScreen.CampaignPlaneSelection, shell.Screen);

        shell.PressExport();

        Assert.NotNull(shell.Dialog);
        var ok = Assert.Single(shell.Rows);
        Assert.Equal(OriginalShell.DialogOkKey, ok.Key);
        Assert.NotNull(scratchPlanes.Load(campaign.Profile!.Planes[0].Name));
        Assert.Null(_planes.Load(campaign.Profile.Planes[0].Name));

        // The focused OK on its rollover frame, in the box's white rather than the paper palette.
        var panel = shell.Compose().Overlays.First(o => o.Lines.Count > 0);
        var label = Assert.Single(panel.Lines, l => l.Text == ok.Label);
        Assert.Equal(BoardInk.Dialog, label.Ink);
        Assert.Contains(panel.Pictures, p => p.Art.Name == "PM_B_Small.png" && p.Frame == 2);

        // Held under the pointer it takes the depressed frame and the black that reads on it.
        shell.Step(Pointer(ok.X + 2f, ok.Y + 2f, pressed: true));
        panel = shell.Compose().Overlays.First(o => o.Lines.Count > 0);
        Assert.Equal(BoardInk.DialogPressed, Assert.Single(panel.Lines, l => l.Text == ok.Label).Ink);
        Assert.Contains(panel.Pictures, p => p.Art.Name == "PM_B_Small.png" && p.Frame == 3);

        // A second press off the screen does nothing: the dialog stands and the rows are still its own.
        shell.PressExport();
        Assert.NotNull(shell.Dialog);
    }

    private static void OpenCampaign(OriginalShell shell)
    {
        var row = shell.Rows.Single(r => r.Key == OriginalShell.CampaignKey);
        shell.Step(Pointer(row.X + 2f, row.Y + 2f, pressed: true, clicked: true));
    }

    // A player typed into the box and started, landing on the cabin.
    private static void Seat(OriginalShell shell, string name)
    {
        OpenCampaign(shell);
        shell.Step(new MenuCommands { Typed = name });
        shell.Step(Accept);
        Assert.Equal(OriginalScreen.CampaignCabin, shell.Screen);
    }

    // The fixture's strips: every button strip 240x200 (four 50-pixel frames), the paper plaque
    // 160x112 (four 28-pixel frames), everything else unmeasured.
    private static (int Width, int Height)? Measure(string art) => art switch
    {
        "PM_B_Paper.png" => (160, 112),
        _ when art.StartsWith("PM_B_", StringComparison.Ordinal) => (240, 200),
        _ => null,
    };

    private static MenuCommands Pointer(float x, float y, bool pressed = false, bool clicked = false) =>
        new() { Pointer = new MenuPointer(x, y, pressed, clicked) };

    // The shell over the fixture, a scripted seat, no extraction, and a private hangar and campaign
    // feature over this test's scratch stores.
    private OriginalShell Shell(out CampaignFeature campaign, out HangarFeature hangar)
    {
        var setup = new PlayerSetupFeature();
        setup.SetRoster(OriginalPresentation.Roster(Array.Empty<CustomPlaneDef>()));
        setup.Join(new ScriptedMenuSeat());
        hangar = new HangarFeature(UiStrings.Empty, PlanePickerRoster.AirframeNode);
        campaign = new CampaignFeature(UiStrings.Empty, airframe => $"node{airframe}");
        var store = _store;
        return new OriginalShell(MenuLayoutReaderTests.OriginalLayout(), new FreeFlightFeature(), setup, Measure,
            hangar: hangar, planes: _planes, campaign: campaign, profiles: () => store);
    }
}
