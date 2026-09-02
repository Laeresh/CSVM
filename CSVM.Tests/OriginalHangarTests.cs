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
    public void TheDoorOpensTheNameScreenWhoseOkWaitsForAName()
    {
        var shell = Shell(out var hangar, out _);
        var door = shell.Rows.Single(r => r.Key == OriginalShell.HangarKey);
        Assert.True(door.Enabled);

        shell.Step(new MenuCommands { Pointer = new MenuPointer(door.X + 2f, door.Y + 2f, true, true) });

        Assert.Equal(OriginalScreen.PlaneName, shell.Screen);
        Assert.True(hangar.IsOpen);
        Assert.True(shell.CapturingText);
        Assert.Equal(
            new[] { OriginalShell.NameFieldKey, OriginalShell.NameDefaultsKey, OriginalShell.NameOkKey, OriginalShell.NameCancelKey },
            shell.Rows.Select(r => r.Key));
        Assert.False(shell.Rows.Single(r => r.Key == OriginalShell.NameOkKey).Enabled);
        Assert.True(shell.LoadDefaultsChecked);
        Assert.Contains(shell.Compose().Lines, l => l.Text == "You must enter a name for your new plane.");

        shell.Step(new MenuCommands { Typed = "Ace/1" });
        Assert.Equal("Ace1", shell.HangarName);
        shell.Step(new MenuCommands { Erase = true });
        Assert.Equal("Ace", shell.HangarName);
        Assert.True(shell.Rows.Single(r => r.Key == OriginalShell.NameOkKey).Enabled);
        Assert.Equal(8, shell.Rows.Single(r => r.Key == OriginalShell.NameDefaultsKey).Art!.Frames);
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
            new[] { OriginalShell.AirframeDropKey, "PX_B_AIRFRAME", "PX_B_ENGINE", "PX_B_ARMOR", "PX_B_GUNS", "PX_B_HARDPOINTS", "PX_B_PAINT", OriginalShell.SellPlanesKey, OriginalShell.ReadyKey, OriginalShell.CancelBuildKey },
            shell.Rows.Select(r => r.Key));
        Assert.False(shell.Rows.Single(r => r.Key == "PX_B_AIRFRAME").Enabled);
        Assert.Equal("Airframe 5", shell.Rows.Single(r => r.Key == OriginalShell.AirframeDropKey).Label);
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
        shell.Step(new MenuCommands { Pointer = new MenuPointer(three.X + 2f, three.Y + 2f, true, true) });
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

        shell.Step(Down);
        Assert.Equal("PX_B_ENGINE", shell.FocusedKey);
        shell.Step(Right);
        Assert.Equal("PX_B_ARMOR", shell.FocusedKey);
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

        Assert.Equal(OriginalScreen.TopLevel, shell.Screen);
        Assert.Equal("Ace", shell.LastBuiltPlane);
        Assert.False(hangar.IsOpen);
        Assert.Equal(HangarFeature.DefaultAirframe, _store.Load("Ace")!.Airframe);
        Assert.Contains(setup.Roster, a => a.Name == "Ace" && a.IsCustom);
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

        Assert.Equal(OriginalScreen.TopLevel, shell.Screen);
        Assert.False(hangar.IsOpen);
        Assert.Empty(_store.List());

        OpenName(shell, "Ace");
        shell.Step(Back);
        Assert.Equal(OriginalScreen.TopLevel, shell.Screen);
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
        Assert.False(shell.Rows.Single(r => r.Key == OriginalShell.InventoryExportKey).Enabled);
        Assert.Equal("Old", shell.Rows[0].Label);
        Assert.Contains(shell.Compose().Pictures, p => p.Art.Name == "PH_PlaneIcons.png" && p.Frame == 2);

        shell.Step(Right);
        Assert.Equal(1, shell.InventoryIndex);
        Assert.Equal("Spare", shell.Rows[0].Label);
        Click(shell, OriginalShell.InventorySellKey);
        Assert.Null(_store.Load("Spare"));
        Assert.Equal(new[] { "Old" }, hangar.Saved.Select(p => p.Name));
        Assert.DoesNotContain(setup.Roster, a => a.Name == "Spare");
        Assert.Equal("Ace", hangar.Scratch.Name);

        Click(shell, OriginalShell.InventoryDoneKey);
        Assert.Equal(OriginalScreen.HangarAirframe, shell.Screen);
    }

    [Fact]
    public void TheHubComposesTheChromeTabsAndButtonsFromTheLayout()
    {
        var shell = Shell(out var hangar, out _);
        OpenHub(shell, "Ace");
        Click(shell, "PX_B_ENGINE");

        var board = shell.Compose();
        Assert.Equal("PH_Back.jpg", Assert.Single(board.Backdrop).Art.Name);
        var current = board.Plaques.Single(p => p.Label == "Engine");
        Assert.Equal(0, current.Frame);
        Assert.Equal(BoardInk.Detail, current.Ink);
        Assert.Equal(1, board.Plaques.Single(p => p.Label == "Airframe").Frame);
        Assert.Equal((150f, 520f), (current.X, current.Y));
        Assert.Contains(board.Lines, l => l.Text == "PLANE COST:  $" + hangar.Bill.Total.Cost);
        Assert.Contains(board.Lines, l => l.Text == "AIRFRAME: Airframe 5");
        Assert.Contains(board.Lines, l => l.Text == "Ace" && l.Ink == BoardInk.Dialog);
        Assert.Contains(board.Plaques, p => p.Art.Name == "PH_B_Ready.png");
        Assert.DoesNotContain(board.Lines, l => l.Text == "$$$ on Hand:");
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
        shell.Step(new MenuCommands { Pointer = new MenuPointer(row.X + 2f, row.Y + 2f, true, true) });
    }

    private static void OpenName(OriginalShell shell, string name)
    {
        shell.Open(OriginalScreen.TopLevel);
        Click(shell, OriginalShell.HangarKey);
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
    // the pages unmeasured.
    private static (int Width, int Height)? Measure(string art) => art switch
    {
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

    private OriginalShell Shell(out HangarFeature hangar, out PlayerSetupFeature setup)
    {
        setup = new PlayerSetupFeature();
        setup.SetRoster(OriginalRosters.Roster(Array.Empty<CustomPlaneDef>()));
        setup.Join(new ScriptedMenuSeat());
        hangar = new HangarFeature(UiStrings.Empty, PlanePickerRoster.AirframeNode);
        return new OriginalShell(MenuLayoutReaderTests.OriginalLayout(), new FreeFlightFeature(), setup, Measure,
            hangar: hangar, planes: _store);
    }
}
