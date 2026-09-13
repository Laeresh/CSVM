using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The Original Instant Action module over the hand-authored layout fixture and a hand-written host:
/// the rows it composes on its opening state, a contents row applying its preset and View Story
/// writing the title, the contents window scrolling by its arrows, a dropdown opening as its list and
/// picking by click or by a sideways step (a barred environment skipped), the enemy pages, the ace
/// duel blanking every enemy control, the bands an open list and a closed box take under the pointer,
/// the radio pair, Weapon Loadout opening the loadout screen for the seat the radio names, and Fly
/// Mission leaving as the feature's exit with the pilot's fit on the seat. No <see cref="OriginalShell"/>
/// stands behind these; what the module asks of one (the screen, the focus, the pointer, the hangar
/// door, the seat walk) the host records. The top level's own door and the keyboard's column walk are
/// the shell's, in <see cref="OriginalShellTests"/>. Every rectangle is the fixture's own geometry.
/// </summary>
public class OriginalInstantActionTests
{
    [Fact]
    public void TheOpeningStateComposesTheContentsWindowThePilotDropdownsAndTheButtonsWithTheEnemyRowBlank()
    {
        var host = Open(out var ia);
        Assert.Equal(OriginalScreen.InstantAction, host.Screen);
        Assert.NotNull(ia.BaseDef);
        Assert.Equal("Test Ace", ia.BaseDef!.AceName);
        Assert.Equal($"{OriginalInstantActionScreen.ContentsKey}:0", host.FocusedKey);

        var keys = host.Rows.Select(r => r.Key).ToList();
        Assert.Equal(Enumerable.Range(0, 10).Select(i => $"{OriginalInstantActionScreen.ContentsKey}:{i}"), keys.Take(10));
        Assert.Equal(
            new[]
            {
                OriginalInstantActionScreen.ContentsUpKey, OriginalInstantActionScreen.ContentsDownKey,
                OriginalInstantActionScreen.ViewStoryKey, OriginalInstantActionScreen.BuildKey,
                OriginalInstantActionScreen.PlayerPlaneKey, OriginalInstantActionScreen.WingmenKey,
                OriginalInstantActionScreen.WingmanPlaneKey, OriginalInstantActionScreen.MissionKey,
                OriginalInstantActionScreen.EnvironmentKey, "IA_D_NENEMY0", "IA_D_EGROUP0", "IA_D_DIFFICULTY0", "IA_D_PLANEE0",
                OriginalInstantActionScreen.PageUpKey, OriginalInstantActionScreen.PageDownKey,
                OriginalInstantActionScreen.PlayerRadioKey, OriginalInstantActionScreen.WingmanRadioKey,
                OriginalInstantActionScreen.WeaponLoadoutKey,
                OriginalInstantActionScreen.FlyMissionKey, OriginalInstantActionScreen.ExitKey,
            },
            keys.Skip(10));

        // The opening preset is an ace duel with no wingmen, so every enemy box and the wingman
        // plane stand blank and inert, the count box included, and only the down button can page.
        foreach (string blank in new[] { OriginalInstantActionScreen.WingmanPlaneKey, "IA_D_NENEMY0", "IA_D_EGROUP0", "IA_D_DIFFICULTY0", "IA_D_PLANEE0" })
        {
            Assert.False(Row(host, blank).Enabled);
            Assert.Equal(string.Empty, Row(host, blank).Label);
        }

        Assert.False(Row(host, OriginalInstantActionScreen.PageUpKey).Enabled);
        Assert.True(Row(host, OriginalInstantActionScreen.PageDownKey).Enabled);

        // The scroll chrome stands inside the list's right edge, so the 280-wide box gives its
        // last 16 to the column and the rows run 264.
        var contents = host.Rows[0];
        Assert.Equal((70f, 170f, 264f, 20f), (contents.X, contents.Y, contents.Width, contents.Height));
        Assert.Equal("Girl Trouble", contents.Label);
        Assert.False(Row(host, OriginalInstantActionScreen.ContentsUpKey).Enabled);
        Assert.True(Row(host, OriginalInstantActionScreen.ContentsDownKey).Enabled);
        Assert.Equal((334f, 359f, 16f, 11f), Rect(Row(host, OriginalInstantActionScreen.ContentsDownKey)));
        Assert.Equal((334f, 170f, 16f, 11f), Rect(Row(host, OriginalInstantActionScreen.ContentsUpKey)));
        // Build stands only where the host says a plane can be made at all; Weapon Loadout needs
        // nothing beyond the feature.
        Assert.False(Row(host, OriginalInstantActionScreen.BuildKey).Enabled);
        Assert.True(Row(host, OriginalInstantActionScreen.WeaponLoadoutKey).Enabled);
        Assert.Equal((540f, 540f, 200f, 32f), Rect(Row(host, OriginalInstantActionScreen.ExitKey)));
        Assert.Equal((150f, 440f, 132f, 28f), Rect(Row(host, OriginalInstantActionScreen.ViewStoryKey)));
        Assert.Equal("View", Row(host, OriginalInstantActionScreen.ViewStoryKey).Label);

        var plane = Row(host, OriginalInstantActionScreen.PlayerPlaneKey);
        Assert.Equal(OriginalRowKind.Dropdown, plane.Kind);
        Assert.Equal((510f, 200f, 220f, 20f), Rect(plane));
        Assert.Equal("Stock Autogyro", plane.Label);
        Assert.Equal("0", Row(host, OriginalInstantActionScreen.WingmenKey).Label);
        Assert.Equal("Dogfighting an Ace", Row(host, OriginalInstantActionScreen.MissionKey).Label);
        Assert.Equal("an airfield", Row(host, OriginalInstantActionScreen.EnvironmentKey).Label);
        Assert.Equal(1, plane.Column);
        Assert.Equal(0, contents.Column);

        var board = Compose(host);
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
        var host = Open(out var ia);
        var luau = host.Rows[3];

        Click(host, 3);

        Assert.Equal(3, ia.PresetIndex);
        Assert.Equal("Hawaii", ia.Environment.Name);
        Assert.Equal("zeppelin_run", ia.MissionType.Key);
        Assert.Equal("Test Ace", ia.BaseDef!.AceName);
        Assert.Equal("Hellhound", Row(host, OriginalInstantActionScreen.WingmanPlaneKey).Label);
        Assert.Equal("4", Row(host, OriginalInstantActionScreen.WingmenKey).Label);
        Assert.Equal("6", Row(host, "IA_D_NENEMY0").Label);
        Assert.Equal("Fortune Hunter", Row(host, "IA_D_EGROUP0").Label);
        Assert.Equal("Ace", Row(host, "IA_D_DIFFICULTY0").Label);
        Assert.Equal("Devastator", Row(host, "IA_D_PLANEE0").Label);
        Assert.True(Row(host, OriginalInstantActionScreen.PageDownKey).Enabled);
        Assert.Equal(string.Empty, host.Module.StoryTitle);
        Assert.Contains(Compose(host).Fills, f => f.X == luau.X && f.Y == luau.Y && !f.Border);
        Assert.Contains(Compose(host).Lines, l => l.Text == "Enemy:" && l.Y == 330f);

        Click(host, OriginalInstantActionScreen.ViewStoryKey);

        Assert.Equal("The Angry Luau", host.Module.StoryTitle);
        Assert.Contains(Compose(host).Lines, l => l.Text == "The Angry Luau" && l.X == 420f && l.Y == 150f);
    }

    [Fact]
    public void TheContentsWindowScrollsByItsArrowsAndTheThumbFollows()
    {
        var host = Open(out _);
        float thumbAtTop = Thumb(host).Y;

        Click(host, OriginalInstantActionScreen.ContentsDownKey);
        Assert.Equal(1, host.Module.ContentsTop);
        Assert.Equal($"{OriginalInstantActionScreen.ContentsKey}:1", host.Rows[0].Key);
        Assert.Equal("Sour Grapes", host.Rows[0].Label);
        Assert.True(Row(host, OriginalInstantActionScreen.ContentsUpKey).Enabled);
        Assert.True(Thumb(host).Y > thumbAtTop);

        for (int i = 0; i < 20 && Row(host, OriginalInstantActionScreen.ContentsDownKey).Enabled; i++)
        {
            Click(host, OriginalInstantActionScreen.ContentsDownKey);
        }

        Assert.Equal(9, host.Module.ContentsTop);
        Assert.False(Row(host, OriginalInstantActionScreen.ContentsDownKey).Enabled);
        Assert.Equal("The Hollywood Brawl", host.Rows[9].Label);

        Click(host, OriginalInstantActionScreen.ContentsUpKey);
        Assert.Equal(8, host.Module.ContentsTop);
    }

    [Fact]
    public void TheContentsListIsTheScreensOwnScrollingWindowForThePointer()
    {
        var host = Open(out _);

        var list = Assert.Single(Lists(host));

        Assert.Equal(OriginalInstantActionScreen.ContentsKey, list.Key);
        Assert.Equal((70f, 170f), (list.Window.X, list.Window.Y));
        list.ScrollTo(4);
        Assert.Equal(4, host.Module.ContentsTop);

        // An open list takes the window from the contents, and a list that fits inside its own box
        // scrolls nothing at all, so the eleven stock airframes leave the screen with no window.
        Click(host, OriginalInstantActionScreen.PlayerPlaneKey);
        Assert.Empty(Lists(host));
    }

    [Fact]
    public void ADropdownOpensAsItsListUnderItsBoxAndAClickOnAnItemPicksAndCloses()
    {
        var host = Open(out var ia);

        Click(host, OriginalInstantActionScreen.MissionKey);

        Assert.Equal(OriginalInstantActionScreen.MissionKey, host.Module.OpenDropdown);
        Assert.Equal(4, host.Rows.Count);
        Assert.Equal($"{OriginalInstantActionScreen.MissionKey}:1", host.Rows[1].Key);
        Assert.Equal((520f, 300f, 210f, 20f), Rect(host.Rows[1]));
        Assert.Equal("Dogfighting a Squadron", host.Rows[1].Label);
        Assert.Equal(0, host.Focus);

        // The open list is the module's one overlay; the pointer is drawn by the shell over it.
        var board = Compose(host);
        var panel = Assert.Single(board.Overlays);
        Assert.Equal(4, panel.Lines.Count);
        Assert.Contains(panel.Fills, f => f.X == 520f && f.Y == 280f && f.Height == 80f && !f.Border);
        Assert.Contains(board.Fills, f => f.X == 520f && f.Y == 260f && !f.Border);

        Click(host, 1);

        Assert.Null(host.Module.OpenDropdown);
        Assert.Equal("dogfight_squadron", ia.MissionType.Key);
        Assert.Equal(OriginalInstantActionScreen.MissionKey, host.FocusedKey);
        Assert.Equal("Dogfighting a Squadron", Row(host, OriginalInstantActionScreen.MissionKey).Label);
        Assert.Contains(host.Rows, r => r.Key == "IA_D_NENEMY0");
        Assert.Contains(host.Rows, r => r.Key == OriginalInstantActionScreen.PageDownKey);
    }

    [Fact]
    public void ThePilotListPastItsWindowScrollsOnItsOwnBar()
    {
        var store = new CustomPlaneStore(TestData.TempDir());
        for (int i = 1; i <= 10; i++)
        {
            store.Save(new CustomPlaneDef { Name = "Built " + i, Airframe = 5, Engine = 1 });
        }

        var host = Host(out _, stock: false, out _, planes: store);
        host.Module.OpenInstantAction();
        Assert.Equal(21, host.Module.PilotRoster.Count);

        Click(host, OriginalInstantActionScreen.PlayerPlaneKey);

        // The fixture's window is twenty rows, so the twenty-first entry puts the list on its own
        // bar: an arrow at each end of the box's right edge, live only towards more list, and the
        // thumb between them.
        Assert.Equal(20, host.Rows.Count(r => r.Kind == OriginalRowKind.ListRow && r.Visible));
        Assert.False(Row(host, OriginalInstantActionScreen.PlayerPlaneKey + ":up").Enabled);
        Assert.True(Row(host, OriginalInstantActionScreen.PlayerPlaneKey + ":down").Enabled);
        Assert.Contains(Compose(host).Overlays[0].Pictures, p => p.Art.Name == "PI_B_ScrollBar.png");
    }

    [Fact]
    public void ClosingAnOpenListPicksNothingAndPutsTheFocusBackOnItsBox()
    {
        var host = Open(out var ia);
        Click(host, OriginalInstantActionScreen.EnvironmentKey);
        Assert.Equal(OriginalInstantActionScreen.EnvironmentKey, host.Module.OpenDropdown);

        Assert.True(host.Module.CloseDropdown());

        Assert.Null(host.Module.OpenDropdown);
        Assert.Equal(0, ia.EnvironmentIndex);
        Assert.Equal(OriginalInstantActionScreen.EnvironmentKey, host.FocusedKey);
        Assert.False(host.Module.CloseDropdown());
    }

    [Fact]
    public void ASidewaysStepOnADropdownPicksTheNextValueAndSkipsABarredEnvironment()
    {
        var host = Open(out var ia);
        Hover(host, OriginalInstantActionScreen.PlayerPlaneKey);
        Assert.Equal(OriginalInstantActionScreen.PlayerPlaneKey, host.FocusedKey);

        Assert.True(Step(host, 1));
        Assert.Equal("Hellhound", ia.PlayerPlane.Name);
        Step(host, -1);
        Step(host, -1);
        Assert.Equal("Warhawk", ia.PlayerPlane.Name);
        Assert.Equal(OriginalInstantActionScreen.PlayerPlaneKey, host.FocusedKey);

        Hover(host, OriginalInstantActionScreen.MissionKey);
        Step(host, 1);
        Step(host, 1);
        Assert.Equal("stunt_flying", ia.MissionType.Key);

        // Stunt flying bars the clouds, so the step over the environment skips row 1.
        Hover(host, OriginalInstantActionScreen.EnvironmentKey);
        Step(host, 1);
        Assert.Equal("Hawaii", ia.Environment.Name);
        Assert.Equal("Test Ace", ia.BaseDef!.AceName);
        Step(host, -1);
        Assert.Equal("an airfield", ia.Environment.Name);

        // With the environment open, the clouds row is listed but not live.
        Accept(host);
        Assert.Equal(OriginalInstantActionScreen.EnvironmentKey, host.Module.OpenDropdown);
        Assert.False(host.Rows[1].Enabled);
        Assert.True(host.Rows[2].Enabled);

        // A step off a row that is neither a dropdown nor a radio is not the module's, so the
        // shell's own column crossing takes it.
        host.Module.CloseDropdown();
        Hover(host, OriginalInstantActionScreen.ViewStoryKey);
        Assert.False(Step(host, 1));
    }

    [Fact]
    public void TheEnemyPagesFlipByTheirButtonsAndTheAceDuelBlanksEveryEnemyControl()
    {
        var host = Open(out var ia);
        Click(host, 0);
        Assert.Equal("dogfight_squadron", ia.MissionType.Key);
        Assert.Contains(host.Rows, r => r.Key == "IA_D_PLANEE0");

        Click(host, OriginalInstantActionScreen.PageDownKey);

        Assert.Equal(1, host.Module.EnemyPage);
        Assert.Equal(OriginalInstantActionScreen.PageUpKey, host.FocusedKey);
        Assert.DoesNotContain(host.Rows, r => r.Key == OriginalInstantActionScreen.PlayerPlaneKey);
        Assert.DoesNotContain(host.Rows, r => r.Key == "IA_D_NENEMY0");
        Assert.Equal("2", Row(host, "IA_D_NENEMY1").Label);
        Assert.Equal("Black Swan", Row(host, "IA_D_EGROUP1").Label);
        Assert.Equal("0", Row(host, "IA_D_NENEMY3").Label);
        // An empty wave keeps its count live and blanks the three boxes a militia would fill.
        Assert.True(Row(host, "IA_D_NENEMY3").Enabled);
        Assert.False(Row(host, "IA_D_EGROUP3").Enabled);
        Assert.Equal(string.Empty, Row(host, "IA_D_PLANEE3").Label);
        Assert.True(Row(host, "IA_D_EGROUP1").Enabled);
        Assert.False(Row(host, OriginalInstantActionScreen.PageDownKey).Enabled);
        Assert.Equal((490f, 200f, 40f, 20f), Rect(Row(host, "IA_D_NENEMY1")));
        var board = Compose(host);
        Assert.Contains(board.Lines, l => l.Text == "[back ...]" && l.Justify == BoardJustify.Right);
        Assert.Equal(3, board.Lines.Count(l => l.Text == "Enemy:"));
        Assert.DoesNotContain(board.Lines, l => l.Text == "Plane:");

        Click(host, OriginalInstantActionScreen.PageUpKey);
        Assert.Equal(0, host.Module.EnemyPage);
        Assert.Contains(host.Rows, r => r.Key == OriginalInstantActionScreen.PlayerPlaneKey);

        Click(host, OriginalInstantActionScreen.MissionKey);
        Click(host, 0);
        Assert.True(ia.IsAceDuel);

        // The duel keeps all four enemy boxes in place, blank and inert, the count among them,
        // and still pages: the down button and the [more ...] line stay live under them.
        foreach (string prefix in new[] { "IA_D_NENEMY", "IA_D_EGROUP", "IA_D_DIFFICULTY", "IA_D_PLANEE" })
        {
            Assert.False(Row(host, prefix + "0").Enabled);
            Assert.Equal(string.Empty, Row(host, prefix + "0").Label);
        }

        Assert.True(Row(host, OriginalInstantActionScreen.PageDownKey).Enabled);
        Assert.Contains(host.Rows, r => r.Key == OriginalInstantActionScreen.WingmenKey);
        var duel = Compose(host);
        Assert.Contains(duel.Lines, l => l.Text == "Enemy:");
        Assert.Contains(duel.Lines, l => l.Text == "[more ...]");
    }

    [Fact]
    public void AnOpenListBandsThePickedRowAndTheRowUnderThePointerAndAClosedBoxLightensUnderIt()
    {
        var host = Open(out _);
        var environment = Row(host, OriginalInstantActionScreen.EnvironmentKey);

        // A closed box under the pointer redraws its outline in the page's cream, with no wash
        // inside it; every other box keeps the printed black one.
        Hover(host, OriginalInstantActionScreen.EnvironmentKey);
        var board = Compose(host);
        Assert.Contains(board.Fills, f => f.X == environment.X && f.Y == environment.Y && f.Border && f.R == 246 && f.G == 237 && f.B == 214);
        Assert.DoesNotContain(board.Fills, f => f.X == environment.X && f.Y == environment.Y && !f.Border);
        var mission = Row(host, OriginalInstantActionScreen.MissionKey);
        Assert.Contains(board.Fills, f => f.X == mission.X && f.Y == mission.Y && f.Border && f.R == 0);

        Click(host, OriginalInstantActionScreen.EnvironmentKey);
        Assert.Equal(OriginalInstantActionScreen.EnvironmentKey, host.Module.OpenDropdown);
        Hover(host, 2);
        var panel = Compose(host).Overlays[0];

        // The picked row and the row under the pointer both band, in their own colours, and the
        // list's own paper stands under them.
        Assert.Contains(panel.Fills, f => f.Y == host.Rows[0].Y && !f.Border && f.R == 200 && f.G == 151 && f.B == 80);
        Assert.Contains(panel.Fills, f => f.Y == host.Rows[2].Y && !f.Border && f.R == 220 && f.G == 181 && f.B == 124);
        Assert.Contains(panel.Fills, f => !f.Border && f.R == 216 && f.G == 200 && f.B == 166);
        Assert.All(panel.Lines, l => Assert.True(l.Ink is BoardInk.Row or BoardInk.Detail));
    }

    [Fact]
    public void TheRadioPairMovesTheLoadoutTargetByClickOrSidewaysStepAndDrawsFromEightStates()
    {
        var host = Open(out _);
        Assert.Equal(0, host.Module.LoadoutTarget);
        var board = Compose(host);
        Assert.Equal(5, board.Plaques.Single(p => p.Art.Name == "PI_B_Radio.png" && p.X == 440f).Frame);
        Assert.Equal(1, board.Plaques.Single(p => p.Art.Name == "PI_B_Radio.png" && p.X == 520f).Frame);

        Click(host, OriginalInstantActionScreen.WingmanRadioKey);
        Assert.Equal(1, host.Module.LoadoutTarget);
        // The click frame holds the button down, so the marked wingman radio draws its pressed
        // frame; the release frame drops it to the marked rollover frame.
        Assert.Equal(7, Compose(host).Plaques.Single(p => p.Art.Name == "PI_B_Radio.png" && p.X == 520f).Frame);
        Hover(host, OriginalInstantActionScreen.WingmanRadioKey);
        board = Compose(host);
        Assert.Equal(1, board.Plaques.Single(p => p.Art.Name == "PI_B_Radio.png" && p.X == 440f).Frame);
        Assert.Equal(6, board.Plaques.Single(p => p.Art.Name == "PI_B_Radio.png" && p.X == 520f).Frame);

        Assert.True(Step(host, 1));
        Assert.Equal(0, host.Module.LoadoutTarget);
        Assert.Equal(OriginalInstantActionScreen.PlayerRadioKey, host.FocusedKey);
    }

    [Fact]
    public void BackClosesAnOpenListFirstAndLeavesTheScreensOwnExitToTheShell()
    {
        var host = Open(out _);
        Click(host, OriginalInstantActionScreen.WingmenKey);
        Assert.NotNull(host.Module.OpenDropdown);

        Assert.True(host.Module.Back());
        Assert.Null(host.Module.OpenDropdown);
        Assert.Equal(OriginalScreen.InstantAction, host.Screen);

        // The screen's own way out is the shell's, so a second Back is declined and the shell's
        // return takes it; the EXIT button is the module's and opens the top level itself.
        Assert.False(host.Module.Back());
        Assert.Equal(OriginalScreen.InstantAction, host.Screen);

        Click(host, OriginalInstantActionScreen.ExitKey);
        Assert.Equal(OriginalScreen.TopLevel, host.Screen);
    }

    [Fact]
    public void FlyMissionLeavesAsTheFeaturesExitForSeatZeroAndAsksTheHostForASecondSeatsWalk()
    {
        var host = Open(out var ia);
        Click(host, 1);
        Assert.Equal("stunt_flying", ia.MissionType.Key);

        var exit = Click(host, OriginalInstantActionScreen.FlyMissionKey);

        var launch = Assert.IsType<LaunchExit>(exit);
        Assert.Equal("C5", launch.Chapter);
        Assert.Equal(MenuMode.Stunt, launch.Mode);
        Assert.Equal("player_bhawk", Assert.Single(launch.Seats).PlaneNode);
        var def = launch.InstantAction!;
        Assert.Equal("stunt_flying", def.MissionType);
        Assert.Equal("Bloodhawk", def.PlayerPlane);
        Assert.Equal(0, def.NumWingmen);
        Assert.Equal(new InstantActionWave(4, "Blake Aviation Bloodhawk", "Bloodhawk", "veteran", -1), def.Waves[0]);
        Assert.Equal("Test Ace", def.AceName);

        // With a second pilot aboard the launch is the shell's per-seat walk, not the module's own
        // exit: the module hands it over and takes whatever the walk answers.
        var seated = Host(out _, stock: false, out var setup);
        setup.SetRoster(OriginalRosters.Roster(Array.Empty<CustomPlaneDef>()));
        setup.Join(new ScriptedMenuSeat());
        setup.Join(new ScriptedMenuSeat());
        seated.Module.OpenInstantAction();
        Assert.Null(Click(seated, OriginalInstantActionScreen.FlyMissionKey));
        Assert.Equal(1, seated.SeatWalks);
    }

    [Fact]
    public void TheBuildDoorStandsOnlyWhereAPlaneCanBeMadeAndOpensTheHangarThroughTheHost()
    {
        var host = Open(out _);
        Assert.False(Row(host, OriginalInstantActionScreen.BuildKey).Enabled);

        host.CanBuildPlane = true;
        Assert.True(Row(host, OriginalInstantActionScreen.BuildKey).Enabled);
        Click(host, OriginalInstantActionScreen.BuildKey);

        Assert.Equal(1, host.HangarOpens);
        Assert.Equal(OriginalScreen.InstantAction, host.Screen);
    }

    [Fact]
    public void WeaponLoadoutWithTheRadioOnWingmanEditsTheWingmenFitAndCancelRestoresIt()
    {
        var host = Open(out var ia, stock: true);
        Click(host, OriginalInstantActionScreen.WingmanRadioKey);
        Assert.Equal(1, host.Module.LoadoutTarget);

        Click(host, OriginalInstantActionScreen.WeaponLoadoutKey);

        Assert.Equal(OriginalScreen.InstantActionLoadout, host.Screen);
        Assert.Same(ia.WingmanFit, host.Module.LoadoutFit);
        Assert.Equal(ia.WingmanPlane.Node, host.Module.LoadoutNode);
        Assert.Equal(-1, host.Module.LoadoutSeat);
        Assert.False(host.Module.OnSeatLoadout);
        // The fixture's section authors the first ammunition field alone, which the Autogyro's
        // one gun slot takes; the buttons are the section's own strips.
        Assert.Equal(
            new[] { OriginalInstantActionScreen.LoadoutAmmoPrefix + "0", OriginalInstantActionScreen.LoadoutAcceptKey, OriginalInstantActionScreen.LoadoutCancelKey },
            host.Rows.Select(r => r.Key));
        var field = Row(host, OriginalInstantActionScreen.LoadoutAmmoPrefix + "0");
        Assert.Equal(OriginalRowKind.Dropdown, field.Kind);
        Assert.Equal((130f, 125f, 150f, 16f), Rect(field));
        Assert.Equal("Slug", field.Label);
        Assert.Contains(Compose(host).Plaques, p => p.Art.Name == "PM_B_AcceptLoadout.png");

        Hover(host, OriginalInstantActionScreen.LoadoutAmmoPrefix + "0");
        Step(host, 1);
        Assert.Equal("dumdum", ia.WingmanFit.GunAmmoFor(1));
        Assert.Equal("Dum-dum", Row(host, OriginalInstantActionScreen.LoadoutAmmoPrefix + "0").Label);

        Click(host, OriginalInstantActionScreen.LoadoutCancelKey);
        Assert.Equal(OriginalScreen.InstantAction, host.Screen);
        Assert.Equal(OriginalInstantActionScreen.WeaponLoadoutKey, host.FocusedKey);
        Assert.Null(host.Module.LoadoutFit);
        Assert.True(ia.WingmanFit.IsStock);

        Click(host, OriginalInstantActionScreen.WeaponLoadoutKey);
        Click(host, OriginalInstantActionScreen.LoadoutAmmoPrefix + "0");
        Assert.Equal(OriginalInstantActionScreen.LoadoutAmmoPrefix + "0", host.Module.OpenDropdown);
        Assert.Equal(5, host.Rows.Count);
        Assert.Equal("Armor-piercing", host.Rows[2].Label);
        Assert.True(host.Module.Back());
        Assert.Null(host.Module.OpenDropdown);
        Assert.Equal(OriginalScreen.InstantActionLoadout, host.Screen);
        Click(host, OriginalInstantActionScreen.LoadoutAmmoPrefix + "0");
        Click(host, 2);
        Assert.Equal("ap", ia.WingmanFit.GunAmmoFor(1));
        Click(host, OriginalInstantActionScreen.LoadoutAcceptKey);
        Assert.Equal(OriginalScreen.InstantAction, host.Screen);
        Assert.Equal("ap", ia.WingmanFit.GunAmmoFor(1));

        // The def carries the wingman fit only where wingmen fly: a squadron with one.
        ia.SelectMissionType(1);
        ia.SetWingmen(1);
        var exit = Click(host, OriginalInstantActionScreen.FlyMissionKey);
        var launch = Assert.IsType<LaunchExit>(exit);
        Assert.Same(ia.WingmanFit, launch.InstantAction!.WingmanLoadout);
        Assert.Null(Assert.Single(launch.Seats).Fit);
    }

    [Fact]
    public void WeaponLoadoutOnThePilotEditsSeatZerosFitWhichRidesTheLaunchAndDropsWithTheAirframe()
    {
        var host = OpenSeated(out var ia, out var setup);
        var seat = setup.Seats[0];

        Click(host, OriginalInstantActionScreen.WeaponLoadoutKey);

        Assert.Equal(OriginalScreen.InstantActionLoadout, host.Screen);
        Assert.Same(seat.Fit, host.Module.LoadoutFit);
        Assert.Same(seat.Fit, host.Module.PilotFit);
        Assert.Equal("player_autogyro", host.Module.LoadoutNode);
        Hover(host, OriginalInstantActionScreen.LoadoutAmmoPrefix + "0");
        Step(host, -1);
        Assert.Equal("none", seat.Fit.GunAmmoFor(1));
        Click(host, OriginalInstantActionScreen.LoadoutAcceptKey);

        var exit = Click(host, OriginalInstantActionScreen.FlyMissionKey);
        var launch = Assert.IsType<LaunchExit>(exit);
        Assert.Same(seat.Fit, Assert.Single(launch.Seats).Fit);
        Assert.True(ia.WingmanFit.IsStock);

        // A different airframe has no slot for the pick, so the fit goes back to stock.
        Hover(host, OriginalInstantActionScreen.PlayerPlaneKey);
        Step(host, 1);
        Assert.Equal("Hellhound", ia.PlayerPlane.Name);
        Assert.True(seat.Fit.IsStock);
    }

    [Fact]
    public void ThePerSeatPickersDoorStandsOverThatSeatsFitAndReturnsToThePicker()
    {
        var host = OpenSeated(out _, out var setup);
        var seat = setup.Seats[0];

        // A seat still browsing has no aeroplane for a fit to hang on, so its door is refused.
        host.Module.OpenSeatLoadout(seat);
        Assert.Equal(OriginalScreen.InstantAction, host.Screen);

        Assert.True(setup.Select(seat));
        host.Module.OpenSeatLoadout(seat);

        Assert.Equal(OriginalScreen.InstantActionLoadout, host.Screen);
        Assert.True(host.Module.OnSeatLoadout);
        Assert.Equal(0, host.Module.LoadoutSeat);
        Assert.Same(seat.Fit, host.Module.LoadoutFit);

        // The trip back lands on the picker rather than on Instant Action, which is what keeps the
        // shell's own walk alive, and the module lets the seat go on its way out.
        Click(host, OriginalInstantActionScreen.LoadoutAcceptKey);
        Assert.Equal(OriginalScreen.SeatPlane, host.Screen);
        Assert.False(host.Module.OnSeatLoadout);
        Assert.Equal(-1, host.Module.LoadoutSeat);
        Assert.Null(host.Module.LoadoutFit);

        // A walk that loses its seat mid-edit forgets the fit without deciding where to go, and a
        // screen the walk does not stand on drops the seat alone.
        host.Module.OpenSeatLoadout(seat);
        host.Module.ClearLoadoutSeat();
        Assert.False(host.Module.OnSeatLoadout);
        Assert.Same(seat.Fit, host.Module.LoadoutFit);
        host.Module.DropLoadout();
        Assert.Null(host.Module.LoadoutFit);
        Assert.Equal(OriginalScreen.InstantActionLoadout, host.Screen);
    }

    [Fact]
    public void BackOnTheLoadoutScreenRestoresThePicks()
    {
        var host = Open(out var ia, stock: true);
        Click(host, OriginalInstantActionScreen.WingmanRadioKey);
        Click(host, OriginalInstantActionScreen.WeaponLoadoutKey);
        Hover(host, OriginalInstantActionScreen.LoadoutAmmoPrefix + "0");
        Step(host, 1);
        Assert.False(ia.WingmanFit.IsStock);

        Assert.True(host.Module.Back());

        Assert.Equal(OriginalScreen.InstantAction, host.Screen);
        Assert.True(ia.WingmanFit.IsStock);
    }

    [Fact]
    public void TheInksReadOffTheScreensOwnRows()
    {
        var host = Host(out _, stock: false, out _);
        Assert.Equal(new MenuLayoutColor(0xFF, 0, 0, 0), host.Module.Inks.Text);
        Assert.Equal(new MenuLayoutColor(0xFF, 0, 0, 0), host.Module.Inks.LabelNormal);
        Assert.Equal(new MenuLayoutColor(0xFF, 0xFF, 0xFF, 0xFF), host.Module.Inks.LabelDepressed);
        var palette = OriginalPresentation.PaletteFor(host.Module.Inks);
        Assert.Equal(new Godot.Color(0f, 0f, 0f, 1f), palette.Row);
        Assert.Equal(new Godot.Color(1f, 1f, 1f, 1f), palette.LabelActivate);
    }

    [Fact]
    public void ALayoutWithNoInstantActionSectionOpensAnEmptyScreen()
    {
        var layout = MenuLayout.Parse(
            "{\"schema\":1,\"widgetTypes\":[],\"globals\":[],\"screens\":[{\"section\":\"MainMenu\",\"script\":\"\",\"widgets\":[]}],\"navigation\":[],\"externalAssets\":[]}");
        var host = new InstantActionHost();
        host.Module = new OriginalInstantActionScreen(
            new InstantActionFeature(_ => InstantActionFeatureTests.InstantActionDefFor("Test Ace")),
            new PlayerSetupFeature(), null, layout, _ => null, host);

        host.Module.OpenInstantAction();

        Assert.Equal(OriginalScreen.InstantAction, host.Screen);
        Assert.Empty(host.Rows);
        Assert.Empty(Compose(host).Backdrop);
        Assert.Empty(Lists(host));
        Assert.False(host.Module.Back());
    }

    // The module over a fresh feature and the layout fixture: with the committed stock table when
    // the loadout screen is under test, and over a build store where the Pilot Plane list is.
    private static InstantActionHost Host(out InstantActionFeature ia, bool stock, out PlayerSetupFeature setup, CustomPlaneStore? planes = null)
    {
        ia = new InstantActionFeature(_ => InstantActionFeatureTests.InstantActionDefFor("Test Ace"));
        setup = new PlayerSetupFeature();
        var table = stock ? StockLoadouts.Load(Path.Combine(TestData.RepoRoot, "CSVM", "data", "stock_loadouts.json")) : null;
        var host = new InstantActionHost();
        host.Module = new OriginalInstantActionScreen(
            ia, setup, planes, MenuLayoutReaderTests.OriginalLayout(), Measure, host,
            table != null ? () => table : null);
        return host;
    }

    private static InstantActionHost Open(out InstantActionFeature ia, bool stock = false)
    {
        var host = Host(out ia, stock, out _);
        host.Module.OpenInstantAction();
        return host;
    }

    // The same with seat 0 joined, which is what makes the pilot's fit a seat's own.
    private static InstantActionHost OpenSeated(out InstantActionFeature ia, out PlayerSetupFeature setup)
    {
        var host = Host(out ia, stock: true, out setup);
        setup.SetRoster(OriginalRosters.Roster(Array.Empty<CustomPlaneDef>()));
        setup.Join(new ScriptedMenuSeat());
        host.Module.OpenInstantAction();
        return host;
    }

    private static OriginalRow Row(InstantActionHost host, string key) => host.Rows.Single(r => r.Key == key);

    private static (float X, float Y, float Width, float Height) Rect(OriginalRow row) => (row.X, row.Y, row.Width, row.Height);

    private static BoardPicture Thumb(InstantActionHost host) => Compose(host).Pictures.Single(p => p.Art.Name == "PI_B_ScrollBar.png");

    // One click as the shell reads it: the press puts the pointer and the focus on the row, the
    // release on it activates, and the button is still held down on the frame that fires.
    private static MenuExit? Click(InstantActionHost host, int index)
    {
        var rows = host.Rows;
        Assert.True(index >= 0 && index < rows.Count, $"no row {index}");
        Assert.True(rows[index].Enabled, $"row {index} is disabled");
        host.PointAt(index, rows[index], pressed: true);
        return host.Module.Activate(rows[index]);
    }

    private static MenuExit? Click(InstantActionHost host, string key)
    {
        int index = host.Rows.ToList().FindIndex(r => r.Key == key);
        Assert.True(index >= 0, $"no row {key}");
        return Click(host, index);
    }

    // The pointer standing on a row with nothing pressed, which is what takes the focus there.
    private static void Hover(InstantActionHost host, int index)
    {
        var rows = host.Rows;
        host.PointAt(index, rows[index], pressed: false);
    }

    private static void Hover(InstantActionHost host, string key)
    {
        int index = host.Rows.ToList().FindIndex(r => r.Key == key);
        Assert.True(index >= 0, $"no row {key}");
        Hover(host, index);
    }

    private static bool Step(InstantActionHost host, int direction) =>
        host.Module.StepSideways(host.Rows, host.Focus, direction);

    private static MenuExit? Accept(InstantActionHost host) => host.Module.Activate(host.Rows[host.Focus]);

    private static IReadOnlyList<OriginalList> Lists(InstantActionHost host)
    {
        var lists = new List<OriginalList>();
        host.Module.Lists(lists);
        return lists;
    }

    // The screen as the module draws it, assembled the way the shell assembles its own board. The
    // shell's own layers (the pointer overlay and the strokes) are not the module's, and neither of
    // these two pages writes a note, so the note layer is handed over and comes back empty.
    private static ComposedBoard Compose(InstantActionHost host)
    {
        var rows = host.Rows;
        var backdrop = new List<BoardPicture>();
        var pictures = new List<BoardPicture>();
        var fills = new List<BoardFill>();
        var lines = new List<BoardLine>();
        var plaques = new List<BoardPlaque>();
        var notes = new List<BoardNote>();
        var overlays = new List<BoardPanel>();
        host.Module.Compose(rows, host.Focus, backdrop, pictures, fills, lines, plaques, notes, overlays);
        return new ComposedBoard(pictures, Array.Empty<BoardStroke>(), lines, plaques, notes,
            backdrop: backdrop, fills: fills, overlays: overlays);
    }

    // The fixture's strips: the two big buttons 200x128 (four 32-pixel frames), the paper button
    // 132x112 (four 28-pixel frames), the radio 18x144 (eight 18-pixel frames), the arrows 15x56,
    // the scroll arrows 16x44, the slider thumb 16x11, the loadout section's own strips as before.
    private static (int Width, int Height)? Measure(string art) => art switch
    {
        "PI_B_Exit.png" or "PI_B_Build.png" => (200, 128),
        "PI_B_Paper.png" => (132, 112),
        "PI_B_Radio.png" => (18, 144),
        "PI_B_Up.png" or "PI_B_Down.png" => (15, 56),
        "PI_B_ScrollUp.png" or "PI_B_ScrollDown.png" => (16, 44),
        "PI_B_ScrollBar.png" => (16, 11),
        "PM_B_Paper.png" => (160, 112),
        _ when art.StartsWith("PM_B_", StringComparison.Ordinal) => (240, 200),
        _ => null,
    };

    // The shell's side of the seam, hand-written: the screen showing, one focus per screen (the
    // first live row where none was set, as the shell's own EnsureFocus rules), the pointer's row
    // and position as a frame leaves them, and a count of each crossing into another family. The
    // rows are the module's own only while the screen showing is one of its two, which is where the
    // shell's own dispatch sends them.
    private sealed class InstantActionHost : IOriginalScreenHost
    {
        private readonly int[] _focus = new int[Enum.GetValues<OriginalScreen>().Length];
        private int _hover = -1;
        private int _pressed = -1;
        private (float X, float Y)? _pointer;

        internal InstantActionHost()
        {
            Array.Fill(_focus, -1);
        }

        public OriginalInstantActionScreen Module { get; set; } = null!;

        public OriginalScreen Screen { get; private set; } = OriginalScreen.TopLevel;

        public int SeatWalks { get; private set; }

        public int HangarOpens { get; private set; }

        public bool CanBuildPlane { get; set; }

        public bool DialogOpen => false;

        public int PressedRow => _pressed;

        public int HoveredRow => _hover;

        public (float X, float Y)? Pointer => _pointer;

        public CustomPlaneStore? CampaignPlanes => null;

        public UiStrings MenuStrings => UiStrings.Empty;

        public IReadOnlyList<OriginalRow> Rows
        {
            get
            {
                var rows = new List<OriginalRow>();
                if (Module.Owns(Screen))
                {
                    Module.BuildRows(rows);
                }

                return rows;
            }
        }

        public int Focus
        {
            get
            {
                var rows = Rows;
                int focus = _focus[(int)Screen];
                if (focus >= 0 && focus < rows.Count)
                {
                    return focus;
                }

                focus = rows.ToList().FindIndex(r => r.Enabled);
                _focus[(int)Screen] = focus;
                return focus;
            }
        }

        public string FocusedKey
        {
            get
            {
                int focus = Focus;
                return focus >= 0 ? Rows[focus].Key : string.Empty;
            }
        }

        public int FocusedRow
        {
            get => _focus[(int)Screen];
            set => _focus[(int)Screen] = value;
        }

        public void Open(OriginalScreen screen) => Screen = screen;

        public void FocusKey(string key)
        {
            var rows = Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Key == key)
                {
                    _focus[(int)Screen] = i;
                    return;
                }
            }
        }

        public void RaiseDialog(string message, DialogIcon icon, params OriginalDialogAnswer[] answers)
        {
        }

        public (int Width, int Height)? Measure(string art) => OriginalInstantActionTests.Measure(art);

        public void ResumeCampaign()
        {
        }

        public void RefreshInstantActionRoster() => Module.RefreshRoster();

        public void RefreshRosterFromStore()
        {
        }

        public void OpenHangar() => HangarOpens++;

        public MenuExit? BeginSeatWalk()
        {
            SeatWalks++;
            return null;
        }

        public BoardPanel? SeatPanel(bool onPaper) => null;

        public void ComposeGenericRow(
            OriginalRow row, bool focused, bool pressed, int index,
            List<BoardFill> fills, List<BoardLine> lines, List<BoardPlaque> plaques, List<BoardPicture> pictures)
        {
            lines.Add(new BoardLine(row.Label, row.X, row.Y, row.Width, 12f, BoardInk.Row, index));
        }

        // The pointer put on one row, as a frame under the cursor leaves the shell: the row is
        // hovered and, where it is live, focused, and a click frame holds it pressed until the
        // frame after the release.
        internal void PointAt(int index, OriginalRow row, bool pressed)
        {
            _hover = index;
            _pressed = pressed ? index : -1;
            _pointer = (row.X + 3f, row.Y + 3f);
            if (row.Enabled)
            {
                _focus[(int)Screen] = index;
            }
        }
    }
}
