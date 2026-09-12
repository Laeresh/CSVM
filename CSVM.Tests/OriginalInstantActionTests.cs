using System.IO;
using System.Linq;
using CSVM.Flight;
using CSVM.UI;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The Original Instant Action screen over the hand-authored layout fixture: the top level's row
/// opening it, the rows it composes on its opening state, a contents row applying its preset and
/// View Story writing the title, the contents window scrolling by its arrows, a dropdown opening
/// as its list and picking by click or by a sideways step (a barred environment skipped), the enemy
/// pages, the ace duel blanking every enemy control, the bands an open list and a closed box take
/// under the pointer, the radio pair, Weapon Loadout opening the loadout screen for the seat the
/// radio names, Back and Exit, and Fly Mission leaving as the feature's exit with the pilot's fit
/// on the seat. Every rectangle is the fixture's invented geometry.
/// </summary>
public class OriginalInstantActionTests
{
    private static readonly MenuCommands Accept = new() { Accept = true };
    private static readonly MenuCommands Back = new() { Back = true };
    private static readonly MenuCommands Down = new() { MoveY = 1 };
    private static readonly MenuCommands Left = new() { MoveX = -1 };
    private static readonly MenuCommands Right = new() { MoveX = 1 };

    [Fact]
    public void TheTopLevelsInstantActionRowIsLiveAndOpensTheScreenWithTheEnvironmentConfirmed()
    {
        var shell = Shell(out var ia);
        Assert.True(shell.Rows.Single(r => r.Key == "MM_B_INSTANTACTION").Enabled);

        shell.Step(Down);
        shell.Step(Down);
        Assert.Equal("MM_B_INSTANTACTION", shell.FocusedKey);
        var step = shell.Step(Accept);

        Assert.Equal(OriginalScreen.InstantAction, shell.Screen);
        Assert.Contains(OriginalCues.Click, step.Cues);
        Assert.NotNull(ia.BaseDef);
        Assert.Equal("Test Ace", ia.BaseDef!.AceName);
        Assert.Equal($"{OriginalShell.ContentsKey}:0", shell.FocusedKey);
    }

    [Fact]
    public void TheOpeningStateComposesTheContentsWindowThePilotDropdownsAndTheButtonsWithTheEnemyRowBlank()
    {
        var shell = Open(out _);

        var keys = shell.Rows.Select(r => r.Key).ToList();
        Assert.Equal(Enumerable.Range(0, 10).Select(i => $"{OriginalShell.ContentsKey}:{i}"), keys.Take(10));
        Assert.Equal(
            new[]
            {
                OriginalShell.ContentsUpKey, OriginalShell.ContentsDownKey, OriginalShell.ViewStoryKey, OriginalShell.BuildKey,
                OriginalShell.PlayerPlaneKey, OriginalShell.WingmenKey, OriginalShell.WingmanPlaneKey, OriginalShell.MissionKey,
                OriginalShell.EnvironmentKey, "IA_D_NENEMY0", "IA_D_EGROUP0", "IA_D_DIFFICULTY0", "IA_D_PLANEE0",
                OriginalShell.PageUpKey, OriginalShell.PageDownKey,
                OriginalShell.PlayerRadioKey, OriginalShell.WingmanRadioKey, OriginalShell.WeaponLoadoutKey,
                OriginalShell.FlyMissionKey, OriginalShell.ExitKey,
            },
            keys.Skip(10));

        // The opening preset is an ace duel with no wingmen, so every enemy box and the wingman
        // plane stand blank and inert, the count box included, and only the down button can page.
        foreach (string blank in new[] { OriginalShell.WingmanPlaneKey, "IA_D_NENEMY0", "IA_D_EGROUP0", "IA_D_DIFFICULTY0", "IA_D_PLANEE0" })
        {
            Assert.False(Row(shell, blank).Enabled);
            Assert.Equal(string.Empty, Row(shell, blank).Label);
        }

        Assert.False(Row(shell, OriginalShell.PageUpKey).Enabled);
        Assert.True(Row(shell, OriginalShell.PageDownKey).Enabled);

        // The scroll chrome stands inside the list's right edge, so the 280-wide box gives its
        // last 16 to the column and the rows run 264.
        var contents = shell.Rows[0];
        Assert.Equal((70f, 170f, 264f, 20f), (contents.X, contents.Y, contents.Width, contents.Height));
        Assert.Equal("Girl Trouble", contents.Label);
        Assert.False(Row(shell, OriginalShell.ContentsUpKey).Enabled);
        Assert.True(Row(shell, OriginalShell.ContentsDownKey).Enabled);
        Assert.Equal((334f, 359f, 16f, 11f), Rect(Row(shell, OriginalShell.ContentsDownKey)));
        Assert.Equal((334f, 170f, 16f, 11f), Rect(Row(shell, OriginalShell.ContentsUpKey)));
        // Build stands only over a hangar and a store, which this shell has neither of; Weapon
        // Loadout needs nothing beyond the feature.
        Assert.False(Row(shell, OriginalShell.BuildKey).Enabled);
        Assert.True(Row(shell, OriginalShell.WeaponLoadoutKey).Enabled);
        Assert.Equal((540f, 540f, 200f, 32f), Rect(Row(shell, OriginalShell.ExitKey)));
        Assert.Equal((150f, 440f, 132f, 28f), Rect(Row(shell, OriginalShell.ViewStoryKey)));
        Assert.Equal("View", Row(shell, OriginalShell.ViewStoryKey).Label);

        var plane = Row(shell, OriginalShell.PlayerPlaneKey);
        Assert.Equal(OriginalRowKind.Dropdown, plane.Kind);
        Assert.Equal((510f, 200f, 220f, 20f), Rect(plane));
        Assert.Equal("Stock Autogyro", plane.Label);
        Assert.Equal("0", Row(shell, OriginalShell.WingmenKey).Label);
        Assert.Equal("Dogfighting an Ace", Row(shell, OriginalShell.MissionKey).Label);
        Assert.Equal("an airfield", Row(shell, OriginalShell.EnvironmentKey).Label);
        Assert.Equal(1, plane.Column);
        Assert.Equal(0, contents.Column);

        var board = shell.Compose();
        Assert.Equal("PI_IA_Back.jpg", Assert.Single(board.Backdrop).Art.Name);
        Assert.Contains(board.Lines, l => l.Text == "Contents" && l.X == 150f && l.Y == 90f);
        Assert.Contains(board.Lines, l => l.Text == "Set the details below." && l.Ink == BoardInk.Dialog);
        Assert.Contains(board.Lines, l => l.Text == "Enemy:");
        Assert.Contains(board.Lines, l => l.Text == "[more ...]");
        Assert.Equal(0, board.Plaques.Single(p => p.Art.Name == "PI_B_Build.png").Frame);
        Assert.Equal(1, board.Plaques.Single(p => p.Art.Name == "PI_B_Exit.png").Frame);
        Assert.Contains(board.Fills, f => f.X == 70f && f.Y == 170f && f.Border);
        Assert.Contains(board.Pictures, p => p.Art.Name == "PI_B_ScrollBar.png");
        Assert.Empty(board.Overlays);
    }

    [Fact]
    public void SelectingAContentsRowAppliesItsPresetAndViewStoryWritesItsName()
    {
        var shell = Open(out var ia);
        var luau = shell.Rows[3];

        var step = Click(shell, luau);

        Assert.Empty(step.Cues);
        Assert.Equal(3, ia.PresetIndex);
        Assert.Equal("Hawaii", ia.Environment.Name);
        Assert.Equal("zeppelin_run", ia.MissionType.Key);
        Assert.Equal("Test Ace", ia.BaseDef!.AceName);
        Assert.Equal("Hellhound", Row(shell, OriginalShell.WingmanPlaneKey).Label);
        Assert.Equal("4", Row(shell, OriginalShell.WingmenKey).Label);
        Assert.Equal("6", Row(shell, "IA_D_NENEMY0").Label);
        Assert.Equal("Fortune Hunter", Row(shell, "IA_D_EGROUP0").Label);
        Assert.Equal("Ace", Row(shell, "IA_D_DIFFICULTY0").Label);
        Assert.Equal("Devastator", Row(shell, "IA_D_PLANEE0").Label);
        Assert.True(Row(shell, OriginalShell.PageDownKey).Enabled);
        Assert.Equal(string.Empty, shell.StoryTitle);
        Assert.Contains(shell.Compose().Fills, f => f.X == luau.X && f.Y == luau.Y && !f.Border);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "Enemy:" && l.Y == 330f);

        Click(shell, Row(shell, OriginalShell.ViewStoryKey));

        Assert.Equal("The Angry Luau", shell.StoryTitle);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "The Angry Luau" && l.X == 420f && l.Y == 150f);
    }

    [Fact]
    public void TheContentsWindowScrollsByItsArrowsAndTheThumbFollows()
    {
        var shell = Open(out _);
        float thumbAtTop = Thumb(shell).Y;

        Click(shell, Row(shell, OriginalShell.ContentsDownKey));
        Assert.Equal(1, shell.ContentsTop);
        Assert.Equal($"{OriginalShell.ContentsKey}:1", shell.Rows[0].Key);
        Assert.Equal("Sour Grapes", shell.Rows[0].Label);
        Assert.True(Row(shell, OriginalShell.ContentsUpKey).Enabled);
        Assert.True(Thumb(shell).Y > thumbAtTop);

        for (int i = 0; i < 20; i++)
        {
            Click(shell, Row(shell, OriginalShell.ContentsDownKey));
        }

        Assert.Equal(9, shell.ContentsTop);
        Assert.False(Row(shell, OriginalShell.ContentsDownKey).Enabled);
        Assert.Equal("The Hollywood Brawl", shell.Rows[9].Label);

        Click(shell, Row(shell, OriginalShell.ContentsUpKey));
        Assert.Equal(8, shell.ContentsTop);
    }

    [Fact]
    public void ADropdownOpensAsItsListUnderItsBoxAndAClickOnAnItemPicksAndCloses()
    {
        var shell = Open(out var ia);
        var mission = Row(shell, OriginalShell.MissionKey);

        var step = Click(shell, mission);

        Assert.Contains(OriginalCues.Click, step.Cues);
        Assert.Equal(OriginalShell.MissionKey, shell.OpenDropdown);
        Assert.Equal(4, shell.Rows.Count);
        Assert.Equal($"{OriginalShell.MissionKey}:1", shell.Rows[1].Key);
        Assert.Equal((520f, 300f, 210f, 20f), Rect(shell.Rows[1]));
        Assert.Equal("Dogfighting a Squadron", shell.Rows[1].Label);
        Assert.Equal(0, shell.Focus);
        // The open list is the first overlay; the pointer, drawn last, is the second.
        var board = shell.Compose();
        Assert.Equal(2, board.Overlays.Count);
        var panel = board.Overlays[0];
        Assert.Equal(4, panel.Lines.Count);
        Assert.Contains(panel.Fills, f => f.X == 520f && f.Y == 280f && f.Height == 80f && !f.Border);
        Assert.Contains(board.Fills, f => f.X == 520f && f.Y == 260f && !f.Border);

        step = Click(shell, shell.Rows[1]);

        Assert.Empty(step.Cues);
        Assert.Null(shell.OpenDropdown);
        Assert.Equal("dogfight_squadron", ia.MissionType.Key);
        Assert.Equal(OriginalShell.MissionKey, shell.FocusedKey);
        Assert.Equal("Dogfighting a Squadron", Row(shell, OriginalShell.MissionKey).Label);
        Assert.Contains(shell.Rows, r => r.Key == "IA_D_NENEMY0");
        Assert.Contains(shell.Rows, r => r.Key == OriginalShell.PageDownKey);
    }

    [Fact]
    public void ThePilotListPastItsWindowScrollsOnItsOwnBar()
    {
        var store = new CustomPlaneStore(TestData.TempDir());
        for (int i = 1; i <= 10; i++)
        {
            store.Save(new CustomPlaneDef { Name = "Built " + i, Airframe = 5, Engine = 1 });
        }

        var ia = new InstantActionFeature(_ => InstantActionFeatureTests.InstantActionDefFor("Test Ace"));
        var shell = new OriginalShell(MenuLayoutReaderTests.OriginalLayout(), new FreeFlightFeature(), new PlayerSetupFeature(), Measure,
            instantAction: ia, planes: store);
        shell.OpenInstantAction();
        Assert.Equal(21, shell.PilotRoster.Count);

        Click(shell, Row(shell, OriginalShell.PlayerPlaneKey));

        // The fixture's window is twenty rows, so the twenty-first entry puts the list on its own
        // bar: an arrow at each end of the box's right edge, live only towards more list, and the
        // thumb between them.
        Assert.Equal(20, shell.Rows.Count(r => r.Kind == OriginalRowKind.ListRow && r.Visible));
        Assert.False(Row(shell, OriginalShell.PlayerPlaneKey + ":up").Enabled);
        Assert.True(Row(shell, OriginalShell.PlayerPlaneKey + ":down").Enabled);
        Assert.Contains(shell.Compose().Overlays[0].Pictures, p => p.Art.Name == "PI_B_ScrollBar.png");
    }

    [Fact]
    public void AClickOffAnOpenListClosesItWithoutPicking()
    {
        var shell = Open(out var ia);
        Click(shell, Row(shell, OriginalShell.EnvironmentKey));
        Assert.Equal(OriginalShell.EnvironmentKey, shell.OpenDropdown);

        shell.Step(Pointer(10f, 10f, pressed: true, clicked: true));

        Assert.Null(shell.OpenDropdown);
        Assert.Equal(0, ia.EnvironmentIndex);
        Assert.Equal(OriginalShell.EnvironmentKey, shell.FocusedKey);
    }

    [Fact]
    public void ASidewaysStepOnADropdownPicksTheNextValueAndSkipsABarredEnvironment()
    {
        var shell = Open(out var ia);
        shell.Step(Hover(Row(shell, OriginalShell.PlayerPlaneKey)));
        Assert.Equal(OriginalShell.PlayerPlaneKey, shell.FocusedKey);

        shell.Step(Right);
        Assert.Equal("Hellhound", ia.PlayerPlane.Name);
        shell.Step(Left);
        shell.Step(Left);
        Assert.Equal("Warhawk", ia.PlayerPlane.Name);
        Assert.Equal(OriginalShell.PlayerPlaneKey, shell.FocusedKey);

        shell.Step(Hover(Row(shell, OriginalShell.MissionKey)));
        shell.Step(Right);
        shell.Step(Right);
        Assert.Equal("stunt_flying", ia.MissionType.Key);

        // Stunt flying bars the clouds, so the step over the environment skips row 1.
        shell.Step(Hover(Row(shell, OriginalShell.EnvironmentKey)));
        shell.Step(Right);
        Assert.Equal("Hawaii", ia.Environment.Name);
        Assert.Equal("Test Ace", ia.BaseDef!.AceName);
        shell.Step(Left);
        Assert.Equal("an airfield", ia.Environment.Name);

        // With the environment open, the clouds row is listed but not live.
        shell.Step(Accept);
        Assert.Equal(OriginalShell.EnvironmentKey, shell.OpenDropdown);
        Assert.False(shell.Rows[1].Enabled);
        Assert.True(shell.Rows[2].Enabled);
    }

    [Fact]
    public void TheEnemyPagesFlipByTheirButtonsAndTheAceDuelBlanksEveryEnemyControl()
    {
        var shell = Open(out var ia);
        Click(shell, shell.Rows[0]);
        Assert.Equal("dogfight_squadron", ia.MissionType.Key);
        Assert.Contains(shell.Rows, r => r.Key == "IA_D_PLANEE0");

        Click(shell, Row(shell, OriginalShell.PageDownKey));

        Assert.Equal(1, shell.EnemyPage);
        Assert.Equal(OriginalShell.PageUpKey, shell.FocusedKey);
        Assert.DoesNotContain(shell.Rows, r => r.Key == OriginalShell.PlayerPlaneKey);
        Assert.DoesNotContain(shell.Rows, r => r.Key == "IA_D_NENEMY0");
        Assert.Equal("2", Row(shell, "IA_D_NENEMY1").Label);
        Assert.Equal("Black Swan", Row(shell, "IA_D_EGROUP1").Label);
        Assert.Equal("0", Row(shell, "IA_D_NENEMY3").Label);
        // An empty wave keeps its count live and blanks the three boxes a militia would fill.
        Assert.True(Row(shell, "IA_D_NENEMY3").Enabled);
        Assert.False(Row(shell, "IA_D_EGROUP3").Enabled);
        Assert.Equal(string.Empty, Row(shell, "IA_D_PLANEE3").Label);
        Assert.True(Row(shell, "IA_D_EGROUP1").Enabled);
        Assert.False(Row(shell, OriginalShell.PageDownKey).Enabled);
        Assert.Equal((490f, 200f, 40f, 20f), Rect(Row(shell, "IA_D_NENEMY1")));
        var board = shell.Compose();
        Assert.Contains(board.Lines, l => l.Text == "[back ...]" && l.Justify == BoardJustify.Right);
        Assert.Equal(3, board.Lines.Count(l => l.Text == "Enemy:"));
        Assert.DoesNotContain(board.Lines, l => l.Text == "Plane:");

        Click(shell, Row(shell, OriginalShell.PageUpKey));
        Assert.Equal(0, shell.EnemyPage);
        Assert.Contains(shell.Rows, r => r.Key == OriginalShell.PlayerPlaneKey);

        Click(shell, Row(shell, OriginalShell.MissionKey));
        Click(shell, shell.Rows[0]);
        Assert.True(ia.IsAceDuel);

        // The duel keeps all four enemy boxes in place, blank and inert, the count among them,
        // and still pages: the down button and the [more ...] line stay live under them.
        foreach (string prefix in new[] { "IA_D_NENEMY", "IA_D_EGROUP", "IA_D_DIFFICULTY", "IA_D_PLANEE" })
        {
            Assert.False(Row(shell, prefix + "0").Enabled);
            Assert.Equal(string.Empty, Row(shell, prefix + "0").Label);
        }

        Assert.True(Row(shell, OriginalShell.PageDownKey).Enabled);
        Assert.Contains(shell.Rows, r => r.Key == OriginalShell.WingmenKey);
        var duel = shell.Compose();
        Assert.Contains(duel.Lines, l => l.Text == "Enemy:");
        Assert.Contains(duel.Lines, l => l.Text == "[more ...]");
    }

    [Fact]
    public void AnOpenListBandsThePickedRowAndTheRowUnderThePointerAndAClosedBoxLightensUnderIt()
    {
        var shell = Open(out _);
        var environment = Row(shell, OriginalShell.EnvironmentKey);

        // A closed box under the pointer redraws its outline in the page's cream, with no wash
        // inside it; every other box keeps the printed black one.
        shell.Step(Hover(environment));
        var board = shell.Compose();
        Assert.Contains(board.Fills, f => f.X == environment.X && f.Y == environment.Y && f.Border && f.R == 246 && f.G == 237 && f.B == 214);
        Assert.DoesNotContain(board.Fills, f => f.X == environment.X && f.Y == environment.Y && !f.Border);
        var mission = Row(shell, OriginalShell.MissionKey);
        Assert.Contains(board.Fills, f => f.X == mission.X && f.Y == mission.Y && f.Border && f.R == 0);

        Click(shell, environment);
        Assert.Equal(OriginalShell.EnvironmentKey, shell.OpenDropdown);
        shell.Step(Hover(shell.Rows[2]));
        var panel = shell.Compose().Overlays[0];

        // The picked row and the row under the pointer both band, in their own colours, and the
        // list's own paper stands under them.
        Assert.Contains(panel.Fills, f => f.Y == shell.Rows[0].Y && !f.Border && f.R == 200 && f.G == 151 && f.B == 80);
        Assert.Contains(panel.Fills, f => f.Y == shell.Rows[2].Y && !f.Border && f.R == 220 && f.G == 181 && f.B == 124);
        Assert.Contains(panel.Fills, f => !f.Border && f.R == 216 && f.G == 200 && f.B == 166);
        Assert.All(panel.Lines, l => Assert.True(l.Ink is BoardInk.Row or BoardInk.Detail));
    }

    [Fact]
    public void TheRadioPairMovesTheLoadoutTargetByClickOrSidewaysStepAndDrawsFromEightStates()
    {
        var shell = Open(out _);
        Assert.Equal(0, shell.LoadoutTarget);
        var board = shell.Compose();
        Assert.Equal(5, board.Plaques.Single(p => p.Art.Name == "PI_B_Radio.png" && p.X == 440f).Frame);
        Assert.Equal(1, board.Plaques.Single(p => p.Art.Name == "PI_B_Radio.png" && p.X == 520f).Frame);

        Click(shell, Row(shell, OriginalShell.WingmanRadioKey));
        Assert.Equal(1, shell.LoadoutTarget);
        // The click frame holds the button down, so the marked wingman radio draws its pressed
        // frame; the release frame drops it to the marked rollover frame.
        Assert.Equal(7, shell.Compose().Plaques.Single(p => p.Art.Name == "PI_B_Radio.png" && p.X == 520f).Frame);
        shell.Step(Hover(Row(shell, OriginalShell.WingmanRadioKey)));
        board = shell.Compose();
        Assert.Equal(1, board.Plaques.Single(p => p.Art.Name == "PI_B_Radio.png" && p.X == 440f).Frame);
        Assert.Equal(6, board.Plaques.Single(p => p.Art.Name == "PI_B_Radio.png" && p.X == 520f).Frame);

        shell.Step(Right);
        Assert.Equal(0, shell.LoadoutTarget);
        Assert.Equal(OriginalShell.PlayerRadioKey, shell.FocusedKey);
    }

    [Fact]
    public void BackClosesAnOpenListFirstAndExitOrASecondBackReturnsToTheTopLevel()
    {
        var shell = Open(out _);
        Click(shell, Row(shell, OriginalShell.WingmenKey));
        Assert.NotNull(shell.OpenDropdown);

        var step = shell.Step(Back);
        Assert.Null(step.Exit);
        Assert.Null(shell.OpenDropdown);
        Assert.Equal(OriginalScreen.InstantAction, shell.Screen);

        step = shell.Step(Back);
        Assert.Null(step.Exit);
        Assert.Equal(OriginalScreen.TopLevel, shell.Screen);

        shell.OpenInstantAction();
        Click(shell, Row(shell, OriginalShell.ExitKey));
        Assert.Equal(OriginalScreen.TopLevel, shell.Screen);
    }

    [Fact]
    public void FlyMissionLeavesAsTheFeaturesExitForSeatZero()
    {
        var shell = Open(out var ia);
        Click(shell, shell.Rows[1]);
        Assert.Equal("stunt_flying", ia.MissionType.Key);

        var step = Click(shell, Row(shell, OriginalShell.FlyMissionKey));

        Assert.Contains(OriginalCues.Click, step.Cues);
        var launch = Assert.IsType<LaunchExit>(step.Exit);
        Assert.Equal("C5", launch.Chapter);
        Assert.Equal(MenuMode.Stunt, launch.Mode);
        Assert.Equal("player_bhawk", Assert.Single(launch.Seats).PlaneNode);
        var def = launch.InstantAction!;
        Assert.Equal("stunt_flying", def.MissionType);
        Assert.Equal("Bloodhawk", def.PlayerPlane);
        Assert.Equal(0, def.NumWingmen);
        Assert.Equal(new Mech3.InstantActionWave(4, "Blake Aviation Bloodhawk", "Bloodhawk", "veteran", -1), def.Waves[0]);
        Assert.Equal("Test Ace", def.AceName);
    }

    [Fact]
    public void WeaponLoadoutWithTheRadioOnWingmanEditsTheWingmenFitAndCancelRestoresIt()
    {
        var shell = Open(out var ia, stock: true);
        Click(shell, Row(shell, OriginalShell.WingmanRadioKey));
        Assert.Equal(1, shell.LoadoutTarget);

        var step = Click(shell, Row(shell, OriginalShell.WeaponLoadoutKey));

        Assert.Contains(OriginalCues.Click, step.Cues);
        Assert.Equal(OriginalScreen.InstantActionLoadout, shell.Screen);
        Assert.Same(ia.WingmanFit, shell.LoadoutFit);
        Assert.Equal(ia.WingmanPlane.Node, shell.LoadoutNode);
        // The fixture's section authors the first ammunition field alone, which the Autogyro's
        // one gun slot takes; the buttons are the section's own strips.
        Assert.Equal(
            new[] { OriginalShell.LoadoutAmmoPrefix + "0", OriginalShell.LoadoutAcceptKey, OriginalShell.LoadoutCancelKey },
            shell.Rows.Select(r => r.Key));
        var field = Row(shell, OriginalShell.LoadoutAmmoPrefix + "0");
        Assert.Equal(OriginalRowKind.Dropdown, field.Kind);
        Assert.Equal((130f, 125f, 150f, 16f), Rect(field));
        Assert.Equal("Slug", field.Label);
        Assert.Contains(shell.Compose().Plaques, p => p.Art.Name == "PM_B_AcceptLoadout.png");

        shell.Step(Hover(field));
        shell.Step(Right);
        Assert.Equal("dumdum", ia.WingmanFit.GunAmmoFor(1));
        Assert.Equal("Dum-dum", Row(shell, OriginalShell.LoadoutAmmoPrefix + "0").Label);

        Click(shell, Row(shell, OriginalShell.LoadoutCancelKey));
        Assert.Equal(OriginalScreen.InstantAction, shell.Screen);
        Assert.Equal(OriginalShell.WeaponLoadoutKey, shell.FocusedKey);
        Assert.Null(shell.LoadoutFit);
        Assert.True(ia.WingmanFit.IsStock);

        Click(shell, Row(shell, OriginalShell.WeaponLoadoutKey));
        Click(shell, Row(shell, OriginalShell.LoadoutAmmoPrefix + "0"));
        Assert.Equal(OriginalShell.LoadoutAmmoPrefix + "0", shell.OpenDropdown);
        Assert.Equal(5, shell.Rows.Count);
        Assert.Equal("Armor-piercing", shell.Rows[2].Label);
        step = shell.Step(Back);
        Assert.Null(shell.OpenDropdown);
        Assert.Equal(OriginalScreen.InstantActionLoadout, shell.Screen);
        Click(shell, Row(shell, OriginalShell.LoadoutAmmoPrefix + "0"));
        Click(shell, shell.Rows[2]);
        Assert.Equal("ap", ia.WingmanFit.GunAmmoFor(1));
        Click(shell, Row(shell, OriginalShell.LoadoutAcceptKey));
        Assert.Equal(OriginalScreen.InstantAction, shell.Screen);
        Assert.Equal("ap", ia.WingmanFit.GunAmmoFor(1));

        // The def carries the wingman fit only where wingmen fly: a squadron with one.
        ia.SelectMissionType(1);
        ia.SetWingmen(1);
        step = Click(shell, Row(shell, OriginalShell.FlyMissionKey));
        var launch = Assert.IsType<LaunchExit>(step.Exit);
        Assert.Same(ia.WingmanFit, launch.InstantAction!.WingmanLoadout);
        Assert.Null(Assert.Single(launch.Seats).Fit);
    }

    [Fact]
    public void WeaponLoadoutOnThePilotEditsSeatZerosFitWhichRidesTheLaunchAndDropsWithTheAirframe()
    {
        var shell = Open(out var ia, stock: true, seated: out var setup);
        var seat = setup.Seats[0];

        Click(shell, Row(shell, OriginalShell.WeaponLoadoutKey));

        Assert.Equal(OriginalScreen.InstantActionLoadout, shell.Screen);
        Assert.Same(seat.Fit, shell.LoadoutFit);
        Assert.Equal("player_autogyro", shell.LoadoutNode);
        shell.Step(Hover(Row(shell, OriginalShell.LoadoutAmmoPrefix + "0")));
        shell.Step(Left);
        Assert.Equal("none", seat.Fit.GunAmmoFor(1));
        Click(shell, Row(shell, OriginalShell.LoadoutAcceptKey));

        var step = Click(shell, Row(shell, OriginalShell.FlyMissionKey));
        var launch = Assert.IsType<LaunchExit>(step.Exit);
        Assert.Same(seat.Fit, Assert.Single(launch.Seats).Fit);
        Assert.True(ia.WingmanFit.IsStock);

        // A different airframe has no slot for the pick, so the fit goes back to stock.
        shell.Step(Hover(Row(shell, OriginalShell.PlayerPlaneKey)));
        shell.Step(Right);
        Assert.Equal("Hellhound", ia.PlayerPlane.Name);
        Assert.True(seat.Fit.IsStock);
    }

    [Fact]
    public void BackOnTheLoadoutScreenRestoresThePicksAndBuildIsDisabledWithoutAStore()
    {
        var shell = Open(out var ia, stock: true);
        Click(shell, Row(shell, OriginalShell.WingmanRadioKey));
        Click(shell, Row(shell, OriginalShell.WeaponLoadoutKey));
        shell.Step(Hover(Row(shell, OriginalShell.LoadoutAmmoPrefix + "0")));
        shell.Step(Right);
        Assert.False(ia.WingmanFit.IsStock);

        var step = shell.Step(Back);

        Assert.Null(step.Exit);
        Assert.Equal(OriginalScreen.InstantAction, shell.Screen);
        Assert.True(ia.WingmanFit.IsStock);
        Assert.False(Row(shell, OriginalShell.BuildKey).Enabled);
    }

    [Fact]
    public void TheInksReadOffTheScreensOwnRows()
    {
        var shell = Shell(out _);
        Assert.Equal(new MenuLayoutColor(0xFF, 0, 0, 0), shell.InstantActionInks.Text);
        Assert.Equal(new MenuLayoutColor(0xFF, 0, 0, 0), shell.InstantActionInks.LabelNormal);
        Assert.Equal(new MenuLayoutColor(0xFF, 0xFF, 0xFF, 0xFF), shell.InstantActionInks.LabelDepressed);
        var palette = OriginalPresentation.PaletteFor(shell.InstantActionInks);
        Assert.Equal(new Godot.Color(0f, 0f, 0f, 1f), palette.Row);
        Assert.Equal(new Godot.Color(1f, 1f, 1f, 1f), palette.LabelActivate);
    }

    [Fact]
    public void ALayoutWithNoInstantActionSectionOpensAnEmptyScreenThatBackLeaves()
    {
        var layout = MenuLayout.Parse(
            "{\"schema\":1,\"widgetTypes\":[],\"globals\":[],\"screens\":[{\"section\":\"MainMenu\",\"script\":\"\",\"widgets\":[]}],\"navigation\":[],\"externalAssets\":[]}");
        var shell = new OriginalShell(layout, new FreeFlightFeature(), new PlayerSetupFeature(), _ => null);

        shell.OpenInstantAction();

        Assert.Equal(OriginalScreen.InstantAction, shell.Screen);
        Assert.Empty(shell.Rows);
        Assert.Empty(shell.Compose().Backdrop);
        shell.Step(Back);
        Assert.Equal(OriginalScreen.TopLevel, shell.Screen);
    }

    private static OriginalShell Shell(out InstantActionFeature ia) => Shell(out ia, false, out _);

    // A shell over the fixture; with the committed stock table when the loadout screen is under
    // test, and with seat 0 joined when its fit is.
    private static OriginalShell Shell(out InstantActionFeature ia, bool stock, out PlayerSetupFeature setup)
    {
        ia = new InstantActionFeature(_ => InstantActionFeatureTests.InstantActionDefFor("Test Ace"));
        setup = new PlayerSetupFeature();
        var table = stock ? StockLoadouts.Load(Path.Combine(TestData.RepoRoot, "CSVM", "data", "stock_loadouts.json")) : null;
        return new OriginalShell(MenuLayoutReaderTests.OriginalLayout(), new FreeFlightFeature(), setup, Measure,
            instantAction: ia, stock: table != null ? () => table : null);
    }

    private static OriginalShell Open(out InstantActionFeature ia, bool stock = false)
    {
        var shell = Shell(out ia, stock, out _);
        shell.OpenInstantAction();
        return shell;
    }

    private static OriginalShell Open(out InstantActionFeature ia, bool stock, out PlayerSetupFeature seated)
    {
        var shell = Shell(out ia, stock, out seated);
        seated.SetRoster(OriginalRosters.Roster(System.Array.Empty<CustomPlaneDef>()));
        seated.Join(new ScriptedMenuSeat());
        shell.OpenInstantAction();
        return shell;
    }

    private static OriginalRow Row(OriginalShell shell, string key) => shell.Rows.Single(r => r.Key == key);

    private static (float X, float Y, float Width, float Height) Rect(OriginalRow row) => (row.X, row.Y, row.Width, row.Height);

    private static BoardPicture Thumb(OriginalShell shell) => shell.Compose().Pictures.Single(p => p.Art.Name == "PI_B_ScrollBar.png");

    // The fixture's strips: the two big buttons 200x128 (four 32-pixel frames), the paper button
    // 132x112 (four 28-pixel frames), the radio 18x144 (eight 18-pixel frames), the arrows 15x56,
    // the scroll arrows 16x44, the slider thumb 16x11, the top level's own strips as before.
    private static (int Width, int Height)? Measure(string art) => art switch
    {
        "PI_B_Exit.png" or "PI_B_Build.png" => (200, 128),
        "PI_B_Paper.png" => (132, 112),
        "PI_B_Radio.png" => (18, 144),
        "PI_B_Up.png" or "PI_B_Down.png" => (15, 56),
        "PI_B_ScrollUp.png" or "PI_B_ScrollDown.png" => (16, 44),
        "PI_B_ScrollBar.png" => (16, 11),
        "PM_B_Paper.png" => (160, 112),
        _ when art.StartsWith("PM_B_", System.StringComparison.Ordinal) => (240, 200),
        _ => null,
    };

    private static MenuCommands Pointer(float x, float y, bool pressed = false, bool clicked = false) =>
        new() { Pointer = new MenuPointer(x, y, pressed, clicked) };

    private static MenuCommands Hover(OriginalRow row) => Pointer(row.X + 3f, row.Y + 3f);

    // One click as the shell reads it: the press arms the row and the release on it fires, so the
    // step that carries the activation is the second one.
    private static OriginalStep Click(OriginalShell shell, OriginalRow row)
    {
        shell.Step(Pointer(row.X + 3f, row.Y + 3f, pressed: true, clicked: true));
        return shell.Step(Pointer(row.X + 3f, row.Y + 3f));
    }
}
