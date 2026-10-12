using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Mech3;
using CSVM.Spec;
using CSVM.UI.Boards;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The Original Instant Action module over the hand-authored layout fixture and a hand-written
/// host. It drives the opening rows, the contents window and its presets, the dropdowns, the enemy
/// pages, the ace duel's blanking and the radio pair. Weapon Loadout opens the loadout screen for
/// the seat the radio names. Fly Mission is the feature's exit, leaving with the pilot's fit on the
/// seat. No <see cref="OriginalShell"/> stands behind these: the host records what the module asks
/// of one, the screen, focus, pointer, hangar door and seat walk. The top level's door and the
/// keyboard's column walk are the shell's, in <see cref="OriginalShellTests"/>; every rectangle
/// here is the fixture's geometry.
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
                OriginalInstantActionScreen.WingmanPlaneKey,
                OriginalInstantActionScreen.LivesKey, OriginalInstantActionScreen.MissionKey,
                OriginalInstantActionScreen.EnvironmentKey, "IA_D_NENEMY0", "IA_D_EGROUP0", "IA_D_DIFFICULTY0", "IA_D_PLANEE0",
                OriginalInstantActionScreen.RaceTimeKey,
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

        // An open list takes the window from the contents. A list that fits inside its own box
        // scrolls nothing, so the eleven stock airframes leave the screen with no window.
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
        // The lives box is no enemy control, so the blanking passes over it.
        Assert.True(Row(host, OriginalInstantActionScreen.LivesKey).Enabled);
        Assert.Equal("1", Row(host, OriginalInstantActionScreen.LivesKey).Label);
        var duel = Compose(host);
        Assert.Contains(duel.Lines, l => l.Text == "Enemy:");
        Assert.Contains(duel.Lines, l => l.Text == "[more ...]");
    }

    [Fact]
    public void TheLivesBoxStepsTheSharedFeatureAndAPresetLeavesItWherePlayerPutIt()
    {
        // The box takes the mission dropdown's left edge and height, and the first clear line the
        // setup stack leaves. In this layout that is the line between the environment row (Y 290,
        // 20 high) and the enemy block (Y 330). The shipped layout leaves one higher up.
        var host = Open(out var ia);
        var lives = Row(host, OriginalInstantActionScreen.LivesKey);
        Assert.Equal(OriginalRowKind.Dropdown, lives.Kind);
        Assert.Equal((520f, 310f, 90f, 20f), Rect(lives));
        Assert.Equal(1, lives.Column);
        Assert.Equal("1", lives.Label);
        Assert.Contains(Compose(host).Lines, l => l.Text == "Lives:" && l.X == 420f && l.Y == 310f);
        Assert.DoesNotContain(host.Rows, r => r.Key != OriginalInstantActionScreen.LivesKey && Overlaps(r, lives));

        // A sideways step on the box is the dropdown's own, so it wraps where the feature clamps.
        Hover(host, OriginalInstantActionScreen.LivesKey);
        Assert.Equal(OriginalInstantActionScreen.LivesKey, host.FocusedKey);
        Step(host, -1);
        Assert.Equal(0, ia.Lives);
        Assert.Equal("Unlimited", Row(host, OriginalInstantActionScreen.LivesKey).Label);
        Step(host, -1);
        Assert.Equal(InstantActionFeature.MaxLives, ia.Lives);
        Step(host, 1);
        Assert.Equal(0, ia.Lives);

        // The list is every count at once, the box having no authored window to read.
        Click(host, OriginalInstantActionScreen.LivesKey);
        Assert.Equal(OriginalInstantActionScreen.LivesKey, host.Module.OpenDropdown);
        Assert.Equal(InstantActionFeature.MaxLives + 1, host.Rows.Count);
        Assert.Equal("Unlimited", host.Rows[0].Label);
        Assert.Equal((520f, 330f, 90f, 20f), Rect(host.Rows[0]));
        Click(host, 3);
        Assert.Null(host.Module.OpenDropdown);
        Assert.Equal(3, ia.Lives);
        Assert.Equal("3", Row(host, OriginalInstantActionScreen.LivesKey).Label);

        // A contents row writes the preset over the page, and no preset carries a lives value.
        Click(host, 0);
        Assert.Equal(0, ia.PresetIndex);
        Assert.Equal(3, ia.Lives);
        Assert.Equal("3", Row(host, OriginalInstantActionScreen.LivesKey).Label);

        // The screenshot aid's pose stands the box at a count with the cursor on it.
        Assert.True(host.Module.PoseLives(5));
        Assert.Equal(5, ia.Lives);
        Assert.Equal(OriginalInstantActionScreen.LivesKey, host.FocusedKey);
    }

    [Fact]
    public void TheRaceTimeBoxShowsOnlyForAStuntRunWithASecondSeatAndKeepsItsPlaceInTheWalk()
    {
        var host = OpenSeated(out var ia, out var setup);
        Click(host, 1);
        Assert.Equal("stunt_flying", ia.MissionType.Key);

        // Solo, the box keeps its place in the rows unseen, unhit and in no column, and the page
        // draws neither it nor its title.
        int index = host.Rows.ToList().FindIndex(r => r.Key == OriginalInstantActionScreen.RaceTimeKey);
        var hidden = host.Rows[index];
        Assert.False(hidden.Visible);
        Assert.False(hidden.Enabled);
        Assert.Equal(-1, hidden.Column);
        Assert.Equal(string.Empty, hidden.Label);
        var solo = Compose(host);
        Assert.DoesNotContain(solo.Lines, l => l.Text == "Race Time:");
        Assert.DoesNotContain(solo.Fills, f => f.X == hidden.X && f.Y == hidden.Y);

        // A second seat shows it at the same index. The lives box takes this layout's one clear
        // line, so this box stands under the stack. Its place in the walk follows: after the last
        // enemy box and before the paging buttons.
        var guest = setup.Join(new ScriptedMenuSeat())!;
        Assert.Equal(index, host.Rows.ToList().FindIndex(r => r.Key == OriginalInstantActionScreen.RaceTimeKey));
        var shown = Row(host, OriginalInstantActionScreen.RaceTimeKey);
        Assert.True(shown.Visible);
        Assert.True(shown.Enabled);
        Assert.Equal(1, shown.Column);
        Assert.Equal(OriginalRowKind.Dropdown, shown.Kind);
        Assert.Equal((520f, 380f, 110f, 20f), Rect(shown));
        Assert.Equal("5 minutes", shown.Label);
        Assert.Equal("IA_D_PLANEE0", host.Rows[index - 1].Key);
        Assert.Equal(OriginalInstantActionScreen.PageUpKey, host.Rows[index + 1].Key);
        Assert.DoesNotContain(host.Rows, r => r.Key != OriginalInstantActionScreen.RaceTimeKey && r.Visible && Overlaps(r, shown));
        var board = Compose(host);
        Assert.Contains(board.Lines, l => l.Text == "Race Time:" && l.X == 420f && l.Y == 380f);
        Assert.Contains(board.Fills, f => f.X == 520f && f.Y == 380f && f.Border);
        // The closed box wears the authored dropdowns' own arrow strip, centred on its line.
        Assert.Contains(board.Pictures, p => p.Art.Name == "PI_B_Down.png" && p.X == 615f && p.Y == 383f);

        // A sideways step and a picked row both write the shared feature's window.
        Hover(host, OriginalInstantActionScreen.RaceTimeKey);
        Assert.True(Step(host, 1));
        Assert.Equal(10, ia.RaceWindowMinutes);
        Assert.Equal("10 minutes", Row(host, OriginalInstantActionScreen.RaceTimeKey).Label);
        Click(host, OriginalInstantActionScreen.RaceTimeKey);
        Assert.Equal(OriginalInstantActionScreen.RaceTimeKey, host.Module.OpenDropdown);
        Assert.Equal(new[] { "3 minutes", "5 minutes", "10 minutes", "15 minutes" }, host.Rows.Select(r => r.Label));
        Assert.Equal((520f, 400f, 110f, 20f), Rect(host.Rows[0]));
        Click(host, 3);
        Assert.Null(host.Module.OpenDropdown);
        Assert.Equal(15, ia.RaceWindowMinutes);
        Assert.Equal(OriginalInstantActionScreen.RaceTimeKey, host.FocusedKey);

        // Another mission type hides it again with the pick kept, and stunt flying brings it back.
        Hover(host, OriginalInstantActionScreen.MissionKey);
        Step(host, 1);
        Assert.Equal("zeppelin_run", ia.MissionType.Key);
        Assert.False(Row(host, OriginalInstantActionScreen.RaceTimeKey).Visible);
        Assert.DoesNotContain(Compose(host).Lines, l => l.Text == "Race Time:");
        Step(host, -1);
        Assert.Equal("15 minutes", Row(host, OriginalInstantActionScreen.RaceTimeKey).Label);

        // The guest leaving with the cursor on the box lifts the cursor to the live box above it.
        // It never stays on the hidden row, and never jumps to the far page's first row.
        Hover(host, OriginalInstantActionScreen.RaceTimeKey);
        Assert.True(setup.Unjoin(guest));
        Assert.Equal("IA_D_PLANEE0", host.FocusedKey);
        Assert.False(Row(host, OriginalInstantActionScreen.RaceTimeKey).Enabled);
        Assert.False(host.Module.OpenDropdownOn(OriginalInstantActionScreen.RaceTimeKey));
        Assert.Equal(15, ia.BuildDef(1).RaceWindowMinutes);
    }

    [Fact]
    public void TheRaceBlanksTheWingmanAndEnemyBoxesAndOneSeatBringsThemBack()
    {
        var host = OpenSeated(out var ia, out var setup);
        Click(host, 1);
        ia.SetWingmen(2);
        ia.SetWave(0, new InstantActionWaveSetup(3, 1, 0, 0));
        string[] keys = { OriginalInstantActionScreen.WingmenKey, OriginalInstantActionScreen.WingmanPlaneKey, "IA_D_NENEMY0", "IA_D_EGROUP0" };
        Assert.All(keys, k => Assert.True(Row(host, k).Enabled));

        var guest = setup.Join(new ScriptedMenuSeat())!;
        Assert.All(keys, k => Assert.False(Row(host, k).Enabled));
        Assert.All(keys, k => Assert.Equal(string.Empty, Row(host, k).Label));

        Assert.True(setup.Unjoin(guest));
        Assert.All(keys, k => Assert.True(Row(host, k).Enabled));
        Assert.Equal("2", Row(host, OriginalInstantActionScreen.WingmenKey).Label);
        Assert.Equal("3", Row(host, "IA_D_NENEMY0").Label);
    }

    [Fact]
    public void ASeatLeavingUnderTheOpenRaceTimeListClosesItAndTheRaceWindowRidesTheLaunch()
    {
        var host = OpenSeated(out var ia, out var setup);
        Click(host, 1);
        var guest = setup.Join(new ScriptedMenuSeat())!;
        Click(host, OriginalInstantActionScreen.RaceTimeKey);
        Click(host, 0);
        Assert.Equal(3, ia.RaceWindowMinutes);
        Click(host, OriginalInstantActionScreen.RaceTimeKey);
        Assert.Equal(OriginalInstantActionScreen.RaceTimeKey, host.Module.OpenDropdown);

        // The next build after the leave, which the shell makes every frame, closes the list.
        setup.Unjoin(guest);
        Assert.Contains(host.Rows, r => r.Key == OriginalInstantActionScreen.PlayerPlaneKey);
        Assert.Null(host.Module.OpenDropdown);
        Assert.Equal("IA_D_PLANEE0", host.FocusedKey);
        Assert.Empty(Compose(host).Overlays);

        // The window rides the built def whichever door launches, and the menu's session spec
        // carries it for the race to read. A launch with no def keeps the five-minute default.
        var launch = Assert.IsType<LaunchExit>(Click(host, OriginalInstantActionScreen.FlyMissionKey));
        Assert.Equal(3, launch.InstantAction!.RaceWindowMinutes);
        var cli = SessionSpec.Parse(Array.Empty<string>());
        Assert.Equal(InstantActionDef.DefaultRaceWindowMinutes, cli.StuntRaceMinutes);
        var spec = SessionSpec.FromMenu(cli, launch.Chapter, new[] { "player_bhawk", "player_fury" }, launch.Mode, launch.InstantAction);
        Assert.Equal(3, spec.StuntRaceMinutes);
        Assert.Equal(5, SessionSpec.FromMenu(cli, "C5", new[] { "player_bhawk", "player_fury" }, MenuMode.Stunt).StuntRaceMinutes);
    }

    [Fact]
    public void TheLivesBoxGivesWayToTheRaceTimeBoxAndComesBackWithItsCount()
    {
        var host = OpenSeated(out var ia, out var setup);
        Click(host, 1);
        Assert.Equal("stunt_flying", ia.MissionType.Key);
        Hover(host, OriginalInstantActionScreen.LivesKey);
        Step(host, 1);
        Assert.Equal(2, ia.Lives);

        // Solo, a stunt run spends lives, so the box shows with its title.
        int index = host.Rows.ToList().FindIndex(r => r.Key == OriginalInstantActionScreen.LivesKey);
        Assert.True(host.Rows[index].Visible);
        Assert.Equal(1, host.Rows[index].Column);
        Assert.Contains(Compose(host).Lines, l => l.Text == "Lives:");

        // A second seat makes the run a race, which spends none. The box keeps its index unseen,
        // unhit and in no column, with no title, and the cursor on it lifts to the live box above.
        var guest = setup.Join(new ScriptedMenuSeat())!;
        Assert.Equal(index, host.Rows.ToList().FindIndex(r => r.Key == OriginalInstantActionScreen.LivesKey));
        var hidden = host.Rows[index];
        Assert.False(hidden.Visible);
        Assert.False(hidden.Enabled);
        Assert.Equal(-1, hidden.Column);
        Assert.Equal(string.Empty, hidden.Label);
        var race = Compose(host);
        Assert.DoesNotContain(race.Lines, l => l.Text == "Lives:");
        Assert.Contains(race.Lines, l => l.Text == "Race Time:");
        Assert.DoesNotContain(race.Fills, f => f.X == hidden.X && f.Y == hidden.Y);
        int above = host.Rows.ToList().FindLastIndex(index - 1, r => r.Enabled && r.Column == 1);
        Assert.Equal(host.Rows[above].Key, host.FocusedKey);
        Assert.NotEqual(OriginalInstantActionScreen.LivesKey, host.FocusedKey);
        Assert.False(host.Module.OpenDropdownOn(OriginalInstantActionScreen.LivesKey));
        Assert.Equal(2, ia.Lives);
        Assert.Equal(2, ia.BuildDef(1).Lives);

        // Another mission type spends lives again, so the box returns with the count it hid.
        Hover(host, OriginalInstantActionScreen.MissionKey);
        Step(host, 1);
        Assert.Equal("zeppelin_run", ia.MissionType.Key);
        Assert.True(Row(host, OriginalInstantActionScreen.LivesKey).Visible);
        Assert.Equal("2", Row(host, OriginalInstantActionScreen.LivesKey).Label);
        Assert.Contains(Compose(host).Lines, l => l.Text == "Lives:");
        Step(host, -1);
        Assert.False(Row(host, OriginalInstantActionScreen.LivesKey).Visible);

        // The guest leaving makes the stunt run solo again, and the box is back.
        Assert.True(setup.Unjoin(guest));
        Assert.True(Row(host, OriginalInstantActionScreen.LivesKey).Enabled);
        Assert.Equal("2", Row(host, OriginalInstantActionScreen.LivesKey).Label);
    }

    [Fact]
    public void ASeatJoiningUnderTheOpenLivesListOfAStuntRunClosesIt()
    {
        var host = OpenSeated(out _, out var setup);
        Click(host, 1);
        Click(host, OriginalInstantActionScreen.LivesKey);
        Assert.Equal(OriginalInstantActionScreen.LivesKey, host.Module.OpenDropdown);

        // The next build after the join closes the list, and the shell makes one every frame. The
        // cursor lands on the live box above the hidden one.
        setup.Join(new ScriptedMenuSeat());
        Assert.Contains(host.Rows, r => r.Key == OriginalInstantActionScreen.PlayerPlaneKey);
        Assert.Null(host.Module.OpenDropdown);
        int index = host.Rows.ToList().FindIndex(r => r.Key == OriginalInstantActionScreen.LivesKey);
        int above = host.Rows.ToList().FindLastIndex(index - 1, r => r.Enabled && r.Column == 1);
        Assert.Equal(host.Rows[above].Key, host.FocusedKey);
        Assert.DoesNotContain(host.Rows, r => r.Key.StartsWith(OriginalInstantActionScreen.LivesKey + ":", StringComparison.Ordinal));
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
        // return takes it. The EXIT button is the module's and opens the top level itself.
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
        // 2 is Blake Aviation's own wave accent, which the militia pick carries.
        Assert.Equal(new InstantActionWave(4, "Blake Aviation Bloodhawk", "Bloodhawk", "veteran", 2), def.Waves[0]);
        Assert.Equal("Test Ace", def.AceName);

        // With a second pilot aboard the launch is the shell's per-seat walk, not the module's own
        // exit. The module hands it over and takes whatever the walk answers.
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
        // The fixture's section authors the first ammunition field, which the Autogyro's one gun
        // slot takes, and three of the eight rocket fields. The Autogyro's two stock pylons (1 and
        // 5) take the first and the fourth, and the buttons are the section's own strips.
        Assert.Equal(
            new[]
            {
                OriginalInstantActionScreen.LoadoutAmmoPrefix + "0",
                OriginalInstantActionScreen.LoadoutRocketPrefix + "0",
                OriginalInstantActionScreen.LoadoutRocketPrefix + "4",
                OriginalInstantActionScreen.LoadoutAcceptKey,
                OriginalInstantActionScreen.LoadoutCancelKey,
            },
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

        // The launch carries the wingman fit only where wingmen fly: a squadron with one.
        ia.SelectMissionType(1);
        ia.SetWingmen(1);
        var exit = Click(host, OriginalInstantActionScreen.FlyMissionKey);
        var launch = Assert.IsType<LaunchExit>(exit);
        Assert.Same(ia.WingmanFit, launch.WingmanLoadout);
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

        // The trip back lands on the picker rather than on Instant Action, which keeps the shell's
        // own walk alive. The module lets the seat go on its way out.
        Click(host, OriginalInstantActionScreen.LoadoutAcceptKey);
        Assert.Equal(OriginalScreen.SeatPlane, host.Screen);
        Assert.False(host.Module.OnSeatLoadout);
        Assert.Equal(-1, host.Module.LoadoutSeat);
        Assert.Null(host.Module.LoadoutFit);

        // A walk that loses its seat mid-edit forgets the fit without deciding where to go. A
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
    public void TheLoadoutScreenDrawsNoRocketFieldForAPylonTheBuildNeverBought()
    {
        var store = new CustomPlaneStore(TestData.TempDir());
        var built = new CustomPlaneDef
        {
            Name = "Blue Streak",
            Airframe = 3,
            Engine = 1,
            LeftHardpoints = 1,
            RightHardpoints = 1,
        };
        built.Guns[0] = new GunChoice(1, Twin: false);
        store.Save(built);
        var host = Host(out _, stock: true, out _, planes: store);
        host.Module.OpenInstantAction();

        // The stock Bloodhawk hangs three pylons, 1, 5 and 2 in fill order, so all three of the
        // fixture's rocket fields stand.
        Click(host, OriginalInstantActionScreen.PlayerPlaneKey);
        Click(host, 3);
        Assert.Equal("Bloodhawk", host.Module.PilotRoster[3].Name);
        Click(host, OriginalInstantActionScreen.WeaponLoadoutKey);
        Assert.Equal(
            new[]
            {
                OriginalInstantActionScreen.LoadoutAmmoPrefix + "0",
                OriginalInstantActionScreen.LoadoutRocketPrefix + "0",
                OriginalInstantActionScreen.LoadoutRocketPrefix + "1",
                OriginalInstantActionScreen.LoadoutRocketPrefix + "4",
                OriginalInstantActionScreen.LoadoutAcceptKey,
                OriginalInstantActionScreen.LoadoutCancelKey,
            },
            host.Rows.Select(r => r.Key));
        Click(host, OriginalInstantActionScreen.LoadoutCancelKey);

        // The same airframe bought one hardpoint a wing, which hangs pylons 1 and 2 and leaves
        // pylon 5's fill-order entry empty. That field is not drawn at all, and the two that are
        // keep their own authored boxes rather than closing up into the gap.
        Click(host, OriginalInstantActionScreen.PlayerPlaneKey);
        Click(host, 11);
        Assert.Equal("Blue Streak", host.Module.PilotRoster[11].Name);
        Click(host, OriginalInstantActionScreen.WeaponLoadoutKey);
        Assert.Equal(
            new[]
            {
                OriginalInstantActionScreen.LoadoutAmmoPrefix + "0",
                OriginalInstantActionScreen.LoadoutRocketPrefix + "0",
                OriginalInstantActionScreen.LoadoutRocketPrefix + "1",
                OriginalInstantActionScreen.LoadoutAcceptKey,
                OriginalInstantActionScreen.LoadoutCancelKey,
            },
            host.Rows.Select(r => r.Key));
        Assert.Equal((130f, 320f, 150f, 16f), Rect(Row(host, OriginalInstantActionScreen.LoadoutRocketPrefix + "0")));
        Assert.Equal((130f, 348f, 150f, 16f), Rect(Row(host, OriginalInstantActionScreen.LoadoutRocketPrefix + "1")));
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

    // The module over a fresh feature and the layout fixture. It takes the committed stock table
    // when the loadout screen is under test, and a build store where the Pilot Plane list is.
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

    private static bool Overlaps(OriginalRow a, OriginalRow b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;

    private static BoardPicture Thumb(InstantActionHost host) => Compose(host).Pictures.Single(p => p.Art.Name == "PI_B_ScrollBar.png");

    // One click as the shell reads it: the press puts the pointer and the focus on the row. The
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
    // pointer overlay is the shell's, and the pen the seam offers is the campaign scrapbook's
    // alone. Neither of these two pages writes a note, so both of those layers come back empty.
    private static ComposedBoard Compose(InstantActionHost host)
    {
        var rows = host.Rows;
        var layers = new BoardLayers();
        host.Module.Compose(rows, host.Focus, layers);
        return new ComposedBoard(layers.Pictures, layers.Strokes, layers.Lines, layers.Plaques, layers.Notes,
            backdrop: layers.Backdrop, fills: layers.Fills, overlays: layers.Overlays);
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

    // Instant Action's side of the seam, over the shared fake. It counts the seat walk, hands the
    // roster re-read back to the module, and puts the pointer on a row. A focus put on a row stays
    // there even once the row goes dark, which is what the paged enemy rows are read through.
    private sealed class InstantActionHost : OriginalTestHost<OriginalInstantActionScreen>
    {
        internal InstantActionHost()
            : base(OriginalScreen.TopLevel, OriginalInstantActionTests.Measure)
        {
        }

        public int SeatWalks { get; private set; }

        protected override bool FocusLeavesDeadRows => false;

        // The shell hands the re-read back to the module whose list it is.
        public override void RefreshInstantActionRoster()
        {
            base.RefreshInstantActionRoster();
            Module.RefreshRoster();
        }

        public override MenuExit? BeginSeatWalk()
        {
            SeatWalks++;
            return null;
        }

        // A row kind the module has no drawing of its own for is a line, standing in for the
        // shell's own plate rule.
        public override void ComposeGenericRow(
            OriginalRow row, bool focused, bool pressed, int index, BoardLayers layers)
        {
            layers.Lines.Add(new BoardLine(row.Label, row.X, row.Y, row.Width, 12f, BoardInk.Row, index));
        }

        // The pointer put on one row, as a frame under the cursor leaves the shell. The row is
        // hovered and, where it is live, focused. A click frame holds it pressed until the frame
        // after the release.
        internal void PointAt(int index, OriginalRow row, bool pressed)
        {
            HoveredRow = index;
            PressedRow = pressed ? index : -1;
            Pointer = (row.X + 3f, row.Y + 3f);
            if (row.Enabled)
            {
                FocusedRow = index;
            }
        }
    }
}
