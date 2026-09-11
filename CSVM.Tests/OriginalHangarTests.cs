using System;
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
/// The Original hangar over the hand-authored layout fixture: the door, the name screen's typing
/// and its two exits, the hub's tab bar as siblings, the dropdowns binding to the shared feature,
/// the defaults ask as a dialog, the totals page committing into a scratch store, the inventory
/// selling, and the cancel that leaves no residue. Every rectangle here is the fixture's invented
/// geometry; the game's is read the same way.
/// </summary>
public class OriginalHangarTests : IDisposable
{
    // The name screen's own refusal, langui 203, which the empty string table falls back to.
    private const string EmptyNameRefusal = "You must enter a name for your new plane.";

    private static readonly MenuCommands Accept = new() { Accept = true };
    private static readonly MenuCommands Back = new() { Back = true };
    private static readonly MenuCommands Down = new() { MoveY = 1 };
    private static readonly MenuCommands Up = new() { MoveY = -1 };
    private static readonly MenuCommands Right = new() { MoveX = 1 };

    private readonly string _dir;
    private readonly CustomPlaneStore _store;

    public OriginalHangarTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "csvm-original-hangar-" + Guid.NewGuid().ToString("N"));
        _store = new CustomPlaneStore(_dir);
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
    public void TheDoorOpensTheNameScreenWhoseOkAnswersAnEmptyBoxWithAMessageBox()
    {
        var shell = Shell(out var hangar, out _);
        Assert.DoesNotContain(shell.Rows, r => r.Key == "HANGAR");
        Click(shell, "MM_B_INSTANTACTION");
        var door = shell.Rows.Single(r => r.Key == OriginalShell.BuildKey);
        Assert.True(door.Enabled);

        Click(shell, door.X + 2f, door.Y + 2f);

        Assert.Equal(OriginalScreen.PlaneName, shell.Screen);
        Assert.True(hangar.IsOpen);
        Assert.True(shell.CapturingText);
        Assert.Equal(
            new[] { OriginalShell.NameFieldKey, OriginalShell.NameDefaultsKey, OriginalShell.NameOkKey, OriginalShell.NameCancelKey },
            shell.Rows.Select(r => r.Key));
        Assert.True(shell.LoadDefaultsChecked);

        // OK stands on an empty box and the refusal is the press's answer, not a standing line.
        Assert.True(shell.Rows.Single(r => r.Key == OriginalShell.NameOkKey).Enabled);
        Assert.DoesNotContain(shell.Compose().Lines, l => l.Text == EmptyNameRefusal);
        Click(shell, OriginalShell.NameOkKey);
        Assert.Equal(EmptyNameRefusal, shell.Dialog!.Message);
        Assert.Equal(DialogIcon.Warning, shell.Dialog!.Icon);
        Assert.Equal(new[] { OriginalShell.DialogOkKey }, shell.Rows.Select(r => r.Key));
        Assert.False(shell.CapturingText);
        Assert.Contains(shell.Compose().Overlays, o => o.Lines.Any(l => l.Text == EmptyNameRefusal));
        Assert.Equal(OriginalScreen.PlaneName, shell.Screen);

        // Its one OK puts the cursor back in the box, which is where the script leaves it.
        Click(shell, OriginalShell.DialogOkKey);
        Assert.Null(shell.Dialog);
        Assert.Equal(OriginalShell.NameFieldKey, shell.FocusedKey);
        Assert.True(shell.CapturingText);

        shell.Step(new MenuCommands { Typed = "Ace/1" });
        Assert.Equal("Ace1", shell.HangarName);
        shell.Step(new MenuCommands { Erase = true });
        Assert.Equal("Ace", shell.HangarName);
        Assert.Equal(8, shell.Rows.Single(r => r.Key == OriginalShell.NameDefaultsKey).Art!.Frames);

        // The box writes its text alone and hangs the caret off it in its own CursorColor.
        var box = shell.Rows.Single(r => r.Key == OriginalShell.NameFieldKey);
        var typed = shell.Compose().Lines.Single(l => l.Text == "Ace" && l.Caret != null);
        Assert.Equal(new BoardCaret(0xEF, 0x00, 0x10, 2f, box.Height - 2f), typed.Caret);
        Assert.DoesNotContain(shell.Compose().Lines, l => l.Text.EndsWith('_'));
    }

    [Fact]
    public void TheNameDialogAndItsRowsRideThePaneCentredOnTheBoard()
    {
        var shell = Shell(out _, out _);
        OpenName(shell, "Ace");

        // The fixture's pane is 260x180 on the 800x600 board, so it centres at 270,210 and every
        // row and text of the section is drawn from that corner rather than from the screen's.
        var board = shell.Compose();
        Assert.Contains(board.Backdrop, p => p.Art.Name == "PH_NamePanel.png" && p.X == 270f && p.Y == 210f);
        Assert.Contains(board.Backdrop, p => p.Art.Name == "PH_Back.jpg" && p.X == 0f && p.Y == 0f);
        var field = shell.Rows.Single(r => r.Key == OriginalShell.NameFieldKey);
        Assert.Equal((290f, 250f), (field.X, field.Y));
        var ok = shell.Rows.Single(r => r.Key == OriginalShell.NameOkKey);
        Assert.Equal((330f, 350f), (ok.X, ok.Y));
        var defaults = shell.Rows.Single(r => r.Key == OriginalShell.NameDefaultsKey);
        Assert.Equal((290f, 300f), (defaults.X, defaults.Y));
        Assert.Contains(board.Lines, l => l.X == 300f && l.Y == 230f);
    }

    [Fact]
    public void TheHubsNameBoxRenamesTheScratchPlaneInPlace()
    {
        var shell = Shell(out var hangar, out _);
        OpenHub(shell, "Ace");

        // The box runs from the title's end to the script's own 302, at the row's own line.
        var box = shell.Rows.Single(r => r.Key == OriginalShell.HubNameFieldKey);
        Assert.Equal(OriginalRowKind.TextField, box.Kind);
        Assert.Equal((120f, 12f, 182f, 17f), (box.X, box.Y, box.Width, box.Height));
        Assert.Equal("Ace", box.Label);
        Assert.False(shell.CapturingText);

        Click(shell, OriginalShell.HubNameFieldKey);
        Assert.Equal(OriginalShell.HubNameFieldKey, shell.FocusedKey);
        Assert.True(shell.CapturingText);
        shell.Step(new MenuCommands { Typed = "2" });

        Assert.Equal("Ace2", hangar.Scratch.Name);
        Assert.Null(hangar.DefaultsAsk);
        Assert.Empty(_store.List());

        // Emptying the box commits nothing: the totals page refuses a nameless plane in the
        // screen's own words, which is the refusal the feature already carried.
        for (int i = 0; i < 4; i++)
        {
            shell.Step(new MenuCommands { Erase = true });
        }

        Assert.Equal(string.Empty, hangar.Scratch.Name);
        Assert.False(hangar.CanCommit);
        shell.Step(new MenuCommands { Typed = "Ace2" });
        var board = shell.Compose();
        var line = board.Lines.Single(l => l.Text == "Ace2" && l.Caret != null);
        Assert.Equal(new BoardCaret(0xEF, 0x00, 0x10, 2f, 15f), line.Caret);
        Assert.Equal(BoardInk.Dialog, line.Ink);
        Assert.Contains(board.Fills, f => f.Border && f.R == 0x73 && f.G == 0x69 && f.B == 0x9C && f.X == box.X);

        // A box nobody is in draws no caret, on this tab or the next.
        Click(shell, "PX_B_ENGINE");
        Assert.False(shell.CapturingText);
        Assert.Equal("Ace2", shell.Rows.Single(r => r.Key == OriginalShell.HubNameFieldKey).Label);
        Assert.DoesNotContain(shell.Compose().Lines, l => l.Caret != null);
    }

    [Fact]
    public void OkWithTheBoxCheckedOpensTheHubOnADefaultConfiguration()
    {
        var shell = Shell(out var hangar, out _);
        OpenName(shell, "Ace");

        shell.Step(Down);
        shell.Step(Down);
        Assert.Equal(OriginalShell.NameOkKey, shell.FocusedKey);
        shell.Step(Accept);

        Assert.Equal(OriginalScreen.HangarAirframe, shell.Screen);
        Assert.Equal("Ace", hangar.Scratch.Name);
        Assert.True(hangar.AirframeChosen);
        Assert.Equal(HangarFeature.DefaultAirframe, hangar.Scratch.Airframe);
        Assert.Equal(1, hangar.Scratch.Engine);
        Assert.Equal(
            new[] { OriginalShell.AirframeDropKey, "PX_B_AIRFRAME", "PX_B_ENGINE", "PX_B_ARMOR", "PX_B_GUNS", "PX_B_HARDPOINTS", "PX_B_PAINT", OriginalShell.SellPlanesKey, OriginalShell.ReadyKey, OriginalShell.CancelBuildKey, OriginalShell.HubNameFieldKey },
            shell.Rows.Select(r => r.Key));
        Assert.True(shell.Rows.Single(r => r.Key == "PX_B_AIRFRAME").Enabled);
        Assert.Equal("Airframe 5", shell.Rows.Single(r => r.Key == OriginalShell.AirframeDropKey).Label);
    }

    [Fact]
    public void TheDefaultConfigurationTakesTheAirframeTheDoorWasOpenedOver()
    {
        var shell = Shell(out var hangar, out _, pilotPlane: 2);
        OpenHub(shell, "Ace");

        Assert.True(hangar.AirframeChosen);
        Assert.Equal(2, hangar.Scratch.Airframe);
        Assert.Equal(1, hangar.Scratch.Engine);
        Assert.Equal("Airframe 2", shell.Rows.Single(r => r.Key == OriginalShell.AirframeDropKey).Label);
    }

    [Fact]
    public void OkWithTheBoxClearedStartsBareAndTheFirstPickRaisesTheAskAsADialog()
    {
        var shell = Shell(out var hangar, out _);
        OpenName(shell, "Ace");
        shell.Step(Down);
        shell.Step(Accept);
        Assert.False(shell.LoadDefaultsChecked);
        shell.Step(Down);
        shell.Step(Accept);

        Assert.Equal(OriginalScreen.HangarAirframe, shell.Screen);
        Assert.False(hangar.AirframeChosen);
        Assert.Equal(string.Empty, shell.Rows.Single(r => r.Key == OriginalShell.AirframeDropKey).Label);
        Assert.Contains(shell.Compose().Pictures, p => p.Art.Name == "PX_0_BLUEPRINT.TGA" && p.X == 16f && p.Y == 44f);

        shell.Step(Accept);
        Assert.Equal(OriginalShell.AirframeDropKey, shell.OpenHangarDropdown);
        Assert.Equal(11, shell.Rows.Count);
        Assert.Equal(OriginalShell.AirframeDropKey + ":0", shell.FocusedKey);
        shell.Step(Down);
        shell.Step(Accept);

        Assert.Equal(1, hangar.DefaultsAsk);
        Assert.Equal(new[] { OriginalShell.AskOkKey, OriginalShell.AskCancelKey }, shell.Rows.Select(r => r.Key));
        var board = shell.Compose();
        Assert.Contains(board.Overlays.SelectMany(o => o.Lines), l => l.Text == hangar.DefaultsAskText);
        shell.Step(Accept);
        Assert.Null(hangar.DefaultsAsk);
        Assert.Equal(1, hangar.Scratch.Airframe);
        Assert.Equal(1, hangar.Scratch.Engine);
        Assert.Equal(OriginalShell.AirframeDropKey, shell.FocusedKey);
    }

    [Fact]
    public void TheTabsAreSiblingsAndASidewaysStepOnADropdownChangesItsValue()
    {
        var shell = Shell(out var hangar, out _);
        OpenHub(shell, "Ace");

        Click(shell, "PX_B_PAINT");
        Assert.Equal(OriginalScreen.HangarPaint, shell.Screen);
        Click(shell, "PX_B_ENGINE");
        Assert.Equal(OriginalScreen.HangarEngine, shell.Screen);
        Assert.Equal(OriginalShell.EngineDropKey, shell.FocusedKey);
        int engine = hangar.Scratch.Engine;
        shell.Step(Right);
        Assert.Equal(engine + 1, hangar.Scratch.Engine);

        shell.Step(Accept);
        Assert.Equal(CustomPlaneDef.EngineNone + 1, shell.Rows.Count);
        Assert.Equal(OriginalShell.EngineDropKey + ":" + (engine + 1), shell.FocusedKey);
        shell.Step(Back);
        Assert.Null(shell.OpenHangarDropdown);
        Assert.Equal(OriginalScreen.HangarEngine, shell.Screen);

        Click(shell, "PX_B_ARMOR");
        Click(shell, "AR_D_POINT1");
        Assert.Equal(CustomPlaneDef.MaxArmourUnits + 1, shell.Rows.Count);
        var three = shell.Rows.Single(r => r.Key == "AR_D_POINT1:3");
        Click(shell, three.X + 2f, three.Y + 2f);
        Assert.Equal(3, hangar.Scratch.ArmourTail);
        Assert.Equal("15 units", shell.Rows.Single(r => r.Key == "AR_D_POINT1").Label);

        Click(shell, "PX_B_HARDPOINTS");
        Click(shell, "HP_D_POINT0");
        Click(shell, "HP_D_POINT0:2");
        Assert.Equal(2, hangar.Scratch.LeftHardpoints);
        Click(shell, "PX_B_GUNS");
        Click(shell, "GN_D_GUN3");
        Click(shell, "GN_D_GUN3:6");
        Assert.Equal(new GunChoice(1, true), hangar.Scratch.Guns[3]);
    }

    [Fact]
    public void TheKeyboardWalksFromTheDropdownsOntoTheTabBarAndAlongIt()
    {
        var shell = Shell(out _, out _);
        OpenHub(shell, "Ace");

        // The standing tab is a sibling like the other five, so the walk steps onto it too.
        shell.Step(Down);
        Assert.Equal("PX_B_AIRFRAME", shell.FocusedKey);
        shell.Step(Right);
        Assert.Equal("PX_B_ENGINE", shell.FocusedKey);
        shell.Step(Right);
        shell.Step(Right);
        shell.Step(Right);
        shell.Step(Right);
        Assert.Equal("PX_B_PAINT", shell.FocusedKey);
        shell.Step(Right);
        Assert.Equal(OriginalShell.SellPlanesKey, shell.FocusedKey);
        shell.Step(Up);
        Assert.Equal("PX_B_PAINT", shell.FocusedKey);
    }

    [Fact]
    public void ThePaintTabWindowsItsColourListAndDrawsSwatchesAndTiles()
    {
        var shell = Shell(out var hangar, out _);
        OpenHub(shell, "Ace");
        Click(shell, "PX_B_PAINT");

        var colours = shell.Rows.Single(r => r.Key == "PT_D_COLORS0");
        Assert.Equal(string.Empty, colours.Label);
        var decal = shell.Rows.Single(r => r.Key == "PT_D_DECALS2");
        Assert.Equal(73f, decal.Height);
        Assert.Contains(shell.Compose().Fills, f => f.X == colours.X + 2f && f.Y == colours.Y + 2f && !f.Border);

        int start = hangar.Scratch.PaintColours[0];
        Click(shell, "PT_D_COLORS0");
        int swatches = HangarPaintTables.Default.Swatches.Count;
        Assert.Equal(swatches + 2, shell.Rows.Count);
        Assert.Equal(18, shell.Rows.Count(r => r.Visible && r.Kind == OriginalRowKind.ListRow));
        Assert.Equal("PT_D_COLORS0:" + start, shell.FocusedKey);
        Assert.True(shell.Rows.Single(r => r.Key == shell.FocusedKey).Visible);
        Assert.True(shell.Rows.Single(r => r.Key == "PT_D_COLORS0:up").Enabled || shell.Rows.Single(r => r.Key == "PT_D_COLORS0:down").Enabled);
        int last = swatches - 1;
        for (int i = 0; i < ((last - start) % swatches + swatches) % swatches; i++)
        {
            shell.Step(Down);
        }

        Assert.True(shell.Rows.Single(r => r.Key == "PT_D_COLORS0:" + last).Visible);
        Assert.False(shell.Rows.Single(r => r.Key == "PT_D_COLORS0:0").Visible);
        Assert.False(shell.Rows.Single(r => r.Key == "PT_D_COLORS0:down").Enabled);
        shell.Step(Accept);
        Assert.Equal(last, hangar.Scratch.PaintColours[0]);
        Assert.Null(shell.OpenHangarDropdown);

        Click(shell, "PT_D_DECALS0");
        Assert.Equal(2, shell.Rows.Count(r => r.Visible && r.Kind == OriginalRowKind.ListRow));
        Click(shell, "PT_D_DECALS0:1");
        Assert.Equal(1, hangar.Scratch.NoseDecal);
        Assert.Contains(shell.Compose().Pictures, p => p.Art.Name == "PH_Decals.tga" && p.Frame == 1);
        Assert.Contains(shell.Compose().Pictures, p => p.Art.Name.StartsWith("PX_ICON_5_", StringComparison.Ordinal) && p.Tint != null);
    }

    [Fact]
    public void ReadyOpensTheTotalsWhosePurchaseCommitsIntoTheStoreAndTheRoster()
    {
        var shell = Shell(out var hangar, out var setup);
        OpenHub(shell, "Ace");

        Click(shell, OriginalShell.ReadyKey);
        Assert.Equal(OriginalScreen.HangarPurchase, shell.Screen);
        var purchase = shell.Rows.Single(r => r.Key == OriginalShell.PurchaseNowKey);
        Assert.True(purchase.Enabled);
        Assert.False(shell.Rows.Single(r => r.Key == OriginalShell.ReadyKey).Enabled);
        var board = shell.Compose();
        Assert.Contains(board.Lines, l => l.Text == "Airframe 5");
        Assert.Contains(board.Lines, l => l.Text == "$" + hangar.Bill.Total.Cost);

        shell.Step(Back);
        Assert.Equal(OriginalScreen.HangarAirframe, shell.Screen);
        Click(shell, OriginalShell.ReadyKey);
        Click(shell, OriginalShell.PurchaseNowKey);

        // The commit returns to the screen the door was pressed on, the Instant Action screen,
        // with its Pilot Plane list re-read so the build is offered at once.
        Assert.Equal(OriginalScreen.InstantAction, shell.Screen);
        Assert.Equal(OriginalShell.BuildKey, shell.FocusedKey);
        Assert.Equal("Ace", shell.LastBuiltPlane);
        Assert.False(hangar.IsOpen);
        Assert.Equal(HangarFeature.DefaultAirframe, _store.Load("Ace")!.Airframe);
        Assert.Contains(setup.Roster, a => a.Name == "Ace" && a.IsCustom);
        Assert.Contains(shell.PilotRoster, a => a.Name == "Ace" && a.IsCustom);
    }

    [Fact]
    public void AnUnbuildablePlaneKeepsPurchaseNowDisabledWithTheReasonShowing()
    {
        var shell = Shell(out var hangar, out _);
        OpenHub(shell, "Ace");
        hangar.SetEngine(CustomPlaneDef.EngineNone);

        Click(shell, OriginalShell.ReadyKey);
        Assert.False(shell.Rows.Single(r => r.Key == OriginalShell.PurchaseNowKey).Enabled);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "CAN'T PURCHASE: No Engine Selected");
    }

    [Fact]
    public void CancelFromATabAndBackFromTheNameScreenLeaveNoResidue()
    {
        var shell = Shell(out var hangar, out _);
        OpenHub(shell, "Ace");
        Click(shell, "PX_B_GUNS");
        Click(shell, OriginalShell.CancelBuildKey);

        Assert.Equal(OriginalScreen.InstantAction, shell.Screen);
        Assert.False(hangar.IsOpen);
        Assert.Empty(_store.List());

        OpenName(shell, "Ace");
        shell.Step(Back);
        Assert.Equal(OriginalScreen.InstantAction, shell.Screen);
        Assert.False(hangar.IsOpen);
        Assert.Empty(_store.List());
    }

    [Fact]
    public void SellPlanesOpensTheInventoryWhoseSellRemovesTheSavedPlane()
    {
        _store.Save(new CustomPlaneDef { Name = "Old", Airframe = 2, Engine = 1 });
        _store.Save(new CustomPlaneDef { Name = "Spare", Airframe = 3, Engine = 1 });
        var shell = Shell(out var hangar, out var setup);
        OpenHub(shell, "Ace");

        Click(shell, OriginalShell.SellPlanesKey);
        Assert.Equal(OriginalScreen.HangarInventory, shell.Screen);
        Assert.Equal(
            new[] { OriginalShell.InventoryPlanesKey, OriginalShell.InventorySellKey, OriginalShell.InventoryExportKey, OriginalShell.InventoryDoneKey },
            shell.Rows.Select(r => r.Key));
        Assert.True(shell.Rows.Single(r => r.Key == OriginalShell.InventoryExportKey).Enabled);
        Assert.Equal("Old", shell.Rows[0].Label);
        Assert.Contains(shell.Compose().Pictures, p => p.Art.Name == "PH_PlaneIcons.png" && p.Frame == 2);

        shell.Step(Right);
        Assert.Equal(1, shell.InventoryIndex);
        Assert.Equal("Spare", shell.Rows[0].Label);
        // Sell asks first, the two-answer messagebox opening on its first answer; No keeps the
        // plane, Yes sells it, and Back declines.
        Click(shell, OriginalShell.InventorySellKey);
        Assert.NotNull(shell.Dialog);
        Assert.Equal(new[] { OriginalShell.DialogYesKey, OriginalShell.DialogNoKey }, shell.Rows.Select(r => r.Key));
        Assert.Equal(OriginalShell.DialogYesKey, shell.FocusedKey);
        Assert.Contains(shell.Compose().Overlays, o => o.Lines.Any(l => l.Text == shell.Dialog!.Message));
        // The sell question is HANGAR.SCRIPT's 0x4 mask, so it keeps the query icon.
        Assert.Equal(DialogIcon.Query, shell.Dialog!.Icon);
        Assert.Equal(
            (int)DialogIcon.Query,
            DialogIconTests.IconFrame(shell.Compose().Overlays.First(o => o.Lines.Count > 0)));
        shell.Step(Back);
        Assert.Null(shell.Dialog);
        Assert.NotNull(_store.Load("Spare"));
        Assert.Equal(OriginalShell.InventorySellKey, shell.FocusedKey);
        Click(shell, OriginalShell.InventorySellKey);
        Click(shell, OriginalShell.DialogYesKey);
        Assert.Null(shell.Dialog);
        Assert.Null(_store.Load("Spare"));
        Assert.Equal(new[] { "Old" }, hangar.Saved.Select(p => p.Name));
        Assert.DoesNotContain(setup.Roster, a => a.Name == "Spare");
        Assert.Equal("Ace", hangar.Scratch.Name);

        Click(shell, OriginalShell.InventoryDoneKey);
        Assert.Equal(OriginalScreen.HangarAirframe, shell.Screen);
    }

    [Fact]
    public void ExportAnswersWithTheScreensOwnConfirmationAndKeepsThePlane()
    {
        _store.Save(new CustomPlaneDef { Name = "Old", Airframe = 2, Engine = 1 });
        var shell = Shell(out var hangar, out _);
        OpenHub(shell, "Ace");
        Click(shell, OriginalShell.SellPlanesKey);

        Click(shell, OriginalShell.InventoryExportKey);
        Assert.NotNull(shell.Dialog);
        // The export box is the one-button 0x1 mask, so it keeps the notice icon.
        Assert.Equal(DialogIcon.Warning, shell.Dialog!.Icon);
        Assert.Equal(new[] { OriginalShell.DialogOkKey }, shell.Rows.Select(r => r.Key));
        Assert.Contains("exported", shell.Dialog!.Message, StringComparison.Ordinal);
        Click(shell, OriginalShell.DialogOkKey);

        Assert.Null(shell.Dialog);
        Assert.NotNull(_store.Load("Old"));
        Assert.Equal(new[] { "Old" }, hangar.Saved.Select(p => p.Name));
    }

    [Fact]
    public void TheHubComposesTheChromeTabsAndButtonsFromTheLayout()
    {
        var shell = Shell(out var hangar, out _);
        OpenHub(shell, "Ace");
        Click(shell, "PX_B_ENGINE");

        var board = shell.Compose();
        Assert.Equal("PH_Back.jpg", Assert.Single(board.Backdrop).Art.Name);
        // The standing tab is latched, not gated: the depressed frame in that frame's own ink,
        // against the normal frame the other five draw.
        var current = board.Plaques.Single(p => p.Label == "Engine");
        Assert.Equal(3, current.Frame);
        Assert.Equal(BoardInk.LabelActivate, current.Ink);
        Assert.Equal(1, board.Plaques.Single(p => p.Label == "Airframe").Frame);
        Assert.Equal((150f, 520f), (current.X, current.Y));
        Assert.Contains(board.Lines, l => l.Text == "PLANE COST:  $" + hangar.Bill.Total.Cost);
        Assert.Contains(board.Lines, l => l.Text == "AIRFRAME: Airframe 5");
        Assert.Contains(board.Lines, l => l.Text == "Ace" && l.Ink == BoardInk.Dialog);
        Assert.Contains(board.Plaques, p => p.Art.Name == "PH_B_Ready.png");
        // The cash note stands on the wallet-free door too, over the export funds the hub's own
        // script writes there.
        Assert.Contains(board.Lines, l => l.Text == "$$$ on Hand:");
        Assert.Contains(board.Lines, l => l.Text == "$50000" && l.Ink == BoardInk.Row);
    }

    [Fact]
    public void DiscardingTheFeatureMidBuildLeavesNothingBehind()
    {
        var shell = Shell(out var hangar, out _);
        OpenHub(shell, "Ace");
        hangar.Discard();

        Assert.False(hangar.IsOpen);
        Assert.Empty(_store.List());
        Assert.Equal(string.Empty, hangar.Scratch.Name);
    }

    private static void Click(OriginalShell shell, string key)
    {
        var row = shell.Rows.Single(r => r.Key == key);
        Click(shell, row.X + 2f, row.Y + 2f);
    }

    // The way in: the top level's Instant Action row, then the screen's Build Custom Plane.
    private static void OpenName(OriginalShell shell, string name)
    {
        shell.Open(OriginalScreen.TopLevel);
        Click(shell, "MM_B_INSTANTACTION");
        Click(shell, OriginalShell.BuildKey);
        shell.Step(new MenuCommands { Typed = name });
    }

    // The name screen's OK with the box checked: the default configuration under the typed name.
    private static void OpenHub(OriginalShell shell, string name)
    {
        OpenName(shell, name);
        Click(shell, OriginalShell.NameOkKey);
        Assert.Equal(OriginalScreen.HangarAirframe, shell.Screen);
    }

    // The fixture's strips: the tabs and buttons four frames each, the checkbox eight, the paper
    // plaque 160x112, the decal sheet fifty 66-pixel tiles, the blueprint and icon sets present,
    // the name dialog's pane smaller than the board and the full-page backgrounds unmeasured.
    private static (int Width, int Height)? Measure(string art) => art switch
    {
        "PH_NamePanel.png" => (260, 180),
        "PM_B_Paper.png" or "PH_B_Paper.png" => (160, 112),
        "PH_Tab.png" => (120, 120),
        "PH_B_OkCancel.png" => (80, 96),
        "PH_B_Check8.png" => (16, 128),
        "PH_B_DropUp.png" or "PH_B_DropDown.png" => (15, 56),
        "PH_Decals.tga" => (66, 3300),
        "PH_PlaneIcons.png" => (100, 1200),
        _ when art.StartsWith("PH_B_", StringComparison.Ordinal) => (200, 128),
        _ when art.StartsWith("PX_ICON_", StringComparison.Ordinal) => (358, 335),
        _ when art.StartsWith("PM_B_", StringComparison.Ordinal) => (240, 200),
        _ => null,
    };

    // One click as the shell reads it: the press arms the row and the release on it fires, so the
    // step that carries the activation is the second one.
    private static OriginalStep Click(OriginalShell shell, float x, float y)
    {
        shell.Step(new MenuCommands { Pointer = new MenuPointer(x, y, true, true) });
        return shell.Step(new MenuCommands { Pointer = new MenuPointer(x, y, false, false) });
    }

    // The Instant Action door's Pilot Plane pick is what a default-configuration build inherits, so
    // every shell here states the pick it opens the door from; the Devastator is the suite's.
    private OriginalShell Shell(out HangarFeature hangar, out PlayerSetupFeature setup, int pilotPlane = HangarFeature.DefaultAirframe)
    {
        setup = new PlayerSetupFeature();
        setup.SetRoster(OriginalRosters.Roster(Array.Empty<CustomPlaneDef>()));
        setup.Join(new ScriptedMenuSeat());
        hangar = new HangarFeature(UiStrings.Empty, PlanePickerRoster.AirframeNode);
        var instantAction = new InstantActionFeature(_ => InstantAction.Defaults());
        instantAction.SelectPlayerPlane(pilotPlane);
        return new OriginalShell(MenuLayoutReaderTests.OriginalLayout(), new FreeFlightFeature(), setup, Measure,
            instantAction: instantAction, hangar: hangar, planes: _store);
    }
}
