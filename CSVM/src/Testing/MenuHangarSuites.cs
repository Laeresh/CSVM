using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSVM.Flight;
using CSVM.UI;
using CSVM.UI.Menu;
using CSVM.UI.Menu.BuiltIn;
using CSVM.UI.Menu.Original;

namespace CSVM.Testing;

/// <summary>
/// The hangar in both presentations. Built-in's journey, characterized: a real
/// <see cref="LaunchMenu"/> is driven through both doors, the nine screens in the flow's order
/// with the airframe-defaults ask, a commit under a scratch name, the edit and the delete of that
/// plane, the residue-free cancel, the aids and the campaign wallet door over the aid's scratch
/// profile; every check pins what the screens do today. Then Original's hangar through a real
/// <see cref="MenuHost"/> over the install's layout: the door, the name screen, the hub's tabs,
/// the totals page committing the same scratch plane into the shared roster, the inventory
/// selling it back, and the switch discarding an open build. The scratch plane is written into
/// the user's store under a name no player would type and removed before the suite ends.
/// </summary>
internal static class MenuHangarSuites
{
    private static readonly MenuCommands Accept = new() { Accept = true };
    private static readonly MenuCommands Back = new() { Back = true };
    private static readonly MenuCommands Down = new() { MoveY = 1 };
    private static readonly MenuCommands Up = new() { MoveY = -1 };
    private static readonly MenuCommands Right = new() { MoveX = 1 };

    [Suite("menu-hangar-journey",
        "Built-in's hangar journey pinned end to end: a real LaunchMenu opens the flow from the Mode "
        + "screen's Build Custom Plane row and from the Instant Action plane pick's trailing row, "
        + "walks plane selection, airframe (the first confirm picks and raises the defaults ask), "
        + "engine, armor, guns, hardpoints, paint and name to the purchase review, commits a scratch "
        + "plane that the pickers then list and select, edits it from the plane list and cancels "
        + "without touching its file, deletes it through the two-stage list, opens the --menu= aids "
        + "and the campaign wallet door over the aid's scratch profile, and drops an open build on a "
        + "presentation switch; every check is what the screens do today")]
    internal static void MenuHangarJourney(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var exits = new List<MenuExit>();
        var host = MenuSuiteHost.Bare(exits, ctx.DataRoot, out var seat);
        var menu = LaunchMenu.Build(ctx.ZrdrPath, ctx.DataRoot, host, seat.Input);
        ctx.Host.AddChild(menu);
        var store = CustomPlaneStore.UserPlanes();
        string scratch = ScratchName();
        ctx.Check(store.Load(scratch) == null, $"the scratch name {scratch} is free in the user's store before the run");
        try
        {
            ModeDoor(ctx, menu, host);
            PlaneSelection(ctx, menu, host);
            Airframe(ctx, menu);
            BuildScreens(ctx, menu);
            NameAndPurchase(ctx, menu, store, scratch);
            EditAndCancel(ctx, menu, store, scratch);
            PlaneScreenDoor(ctx, menu);
            Aids(ctx, menu);
            CampaignDoor(ctx, menu);
            Delete(ctx, menu, store, scratch);
        }
        finally
        {
            store.Delete(scratch);
            ctx.Host.RemoveChild(menu);
            menu.QueueFree();
        }

        ctx.Check(store.Load(scratch) == null, $"the scratch plane is gone from the user's store after the run");
    }

    [Suite("menu-original-hangar",
        "Original's hangar through the presentation boundary over the install's decoded layout: the "
        + "top level's BUILD PLANE door opens the decoded name screen, typed frames name the "
        + "plane and OK opens the Plane Construction hub on the default configuration, the tabs are "
        + "siblings a click and the keyboard reach out of order, a dropdown steps and picks through "
        + "the shared feature with the running total following, READY TO PURCHASE and Purchase Now "
        + "commit the scratch plane into the user's store and the shared roster, SELL PLANES opens "
        + "the inventory whose Sell asks and then removes it again, CANCEL and Back leave no residue, and a switch "
        + "discards an open build")]
    internal static void MenuOriginalHangar(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        var exits = new List<MenuExit>();
        var seat = new ScriptedSeat();
        var registry = new PresentationRegistry();
        registry.Register(PresentationId.BuiltIn, () => new BuiltInPresentation(
            ctx.Host, ctx.ZrdrPath, ctx.DataRoot, string.Empty, new MenuInput { Keyboard = true }));
        registry.Register(PresentationId.Original, () => new OriginalPresentation(
            ctx.Host, ctx.DataRoot, layout, string.Empty, new MenuInput { Keyboard = true }));
        var host = new MenuHost(registry, new MenuSuiteHost.SilentMenuAudio(), exits.Add);
        MenuSuiteHost.AddFeatures(host, ctx.DataRoot);
        host.AddSeat(seat);
        var store = CustomPlaneStore.UserPlanes();
        string scratch = ScratchName();
        ctx.Check(store.Load(scratch) == null, $"the scratch name {scratch} is free in the user's store before the run");
        try
        {
            host.Select(forceBuiltIn: false, cliOverride: "original", savedRequest: null);
            host.Show(MenuReturnDestination.TopLevel);
            var shell = (host.Active as OriginalPresentation)?.Shell;
            ctx.Check(shell != null, $"Show activates Original ({host.Active?.Id})");
            if (shell == null)
            {
                return;
            }

            var size = ctx.Host.GetViewport().GetVisibleRect().Size;
            var fit = BoardFit.For(size.X, size.Y);
            var hangar = host.Features.Get<HangarFeature>();
            var setup = host.Features.Get<PlayerSetupFeature>();
            OriginalNameScreen(ctx, host, seat, shell, fit, hangar, scratch);
            OriginalHub(ctx, host, seat, shell, fit, hangar);
            OriginalPurchase(ctx, host, seat, shell, fit, hangar, setup, store, scratch);
            OriginalInventory(ctx, host, seat, shell, fit, hangar, setup, store, scratch);
            OriginalSwitch(ctx, host, seat, shell, fit, hangar, store);
        }
        finally
        {
            store.Delete(scratch);
            host.Deactivate();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
        }

        ctx.Check(store.Load(scratch) == null, $"the scratch plane is gone from the user's store after the run");
    }

    // A name no player would type, distinct per run, inside the name screen's own character set.
    private static string ScratchName() => "Scratch " + Guid.NewGuid().ToString("N")[..8];

    private static void ModeDoor(TestContext ctx, LaunchMenu menu, MenuHost host)
    {
        menu.ShowMenu();
        WalkTo(menu, LaunchMenu.HangarRow);
        Is(ctx, "the Mode screen's fifth row is the hangar door", LaunchMenu.HangarRow, menu.ShownRowText);
        Is(ctx, "its description", "Build a plane in the hangar and fly it.", menu.ShownDetail);
        menu.Drive(Accept);
        Is(ctx, "Accept on the door opens the Hangar screen", "Hangar", menu.ShownScreen);
        Is(ctx, "on plane selection", "PLANE SELECTION", menu.ShownHeading);
        Is(ctx, "its breadcrumb", LaunchMenu.HangarRow + "  ›  PLANE SELECTION", menu.ShownBreadcrumb);
        ctx.Check(menu.Hangar is { Campaign: null, Screen: HangarScreen.PlaneSelection }, $"the flow is open, wallet-free, on its first screen");
        ctx.Check(menu.Hangar?.Feature.IsOpen == true && ReferenceEquals(menu.Hangar?.Feature, host.Features.Get<HangarFeature>()),
            $"over the host's shared hangar feature");
        menu.Drive(Back);
        ctx.Check(menu.ShownScreen == "Mode" && menu.ShownRowText == LaunchMenu.HangarRow && menu.Hangar == null,
            $"Back off plane selection cancels the flow and lands on the door ({menu.ShownScreen}, {menu.ShownRowText})");
    }

    private static void PlaneSelection(TestContext ctx, LaunchMenu menu, MenuHost host)
    {
        menu.Drive(Accept);
        Is(ctx, "the first row starts a new plane", "New Plane", menu.ShownRowText);
        Is(ctx, "its description", "Build a plane from a bare airframe", menu.ShownDetail);
        Has(ctx, "the footer names the select press", "Enter / A  Select", menu.ShownFooter);
        int saved = menu.Hangar!.Saved.Count;
        ctx.Check(menu.ShownRowCount == saved + (saved > 0 ? 2 : 1),
            $"the rows are New Plane, the store's {saved} saved planes and the delete row where any are saved ({menu.ShownRowCount})");
        ctx.Check(menu.ShownDetail.Length > 0 && menu.Hangar.TotalsLine.Length == 0,
            $"no totals line stands on the New Plane row ({menu.Hangar.TotalsLine})");

        // A presentation switch mid-flow drops the scratch build through the feature set.
        host.Features.DiscardTransient();
        ctx.Check(!menu.Hangar.Feature.IsOpen, $"discarding the features' transient state closes the open build");
        menu.ShowMenu();
        ctx.Check(menu.Hangar == null && menu.ShownScreen == "Mode", $"and a re-show stands on the Mode screen with no flow ({menu.ShownScreen})");
        WalkTo(menu, LaunchMenu.HangarRow);
        menu.Drive(Accept);
    }

    private static void Airframe(TestContext ctx, LaunchMenu menu)
    {
        menu.Drive(Accept);
        var flow = menu.Hangar!;
        ctx.Check(flow.Screen == HangarScreen.Airframe && menu.ShownHeading == flow.Page.Title,
            $"New Plane opens the airframe screen ({flow.Screen}, {menu.ShownHeading})");
        ctx.Check(menu.ShownRowCount == 11 && menu.ShownRow == 0, $"eleven airframes, cursor on the first ({menu.ShownRowCount}, {menu.ShownRow})");
        Is(ctx, "the first row is the first airframe, unticked", flow.AirframeName(0), menu.ShownRowText);
        ctx.Check(!flow.AirframeChosen, $"nothing is chosen before the pilot chooses");
        Has(ctx, "the footer names the double press", "Select, again to continue", menu.ShownFooter);
        Has(ctx, "the detail prices the focused airframe", "Capacity", menu.ShownDetail);
        ctx.Check(menu.Hangar!.TotalsLine.StartsWith("$", StringComparison.Ordinal), $"the totals line prices the scratch plane ({menu.Hangar.TotalsLine})");

        for (int i = 0; i < HangarFeature.DefaultAirframe; i++)
        {
            menu.Drive(Down);
        }

        Is(ctx, "five rows down is the Devastator", flow.AirframeName(HangarFeature.DefaultAirframe), menu.ShownRowText);
        menu.Drive(Accept);
        ctx.Check(flow.DefaultsAsk == HangarFeature.DefaultAirframe && menu.ShownRowCount == 2 && menu.ShownRow == 0,
            $"the first confirm picks and raises the defaults ask as two rows ({flow.DefaultsAsk}, {menu.ShownRowCount})");
        Is(ctx, "the first answer", "OK", menu.ShownRowText);
        Is(ctx, "the ask's own text stands in the detail", flow.DefaultsAskText, menu.ShownDetail);
        Has(ctx, "the footer names the answer press", "Answer", menu.ShownFooter);
        menu.Drive(Down);
        Is(ctx, "the second answer", "Cancel", menu.ShownRowText);
        menu.Drive(Accept);
        ctx.Check(flow.DefaultsAsk == null && menu.ShownRowCount == 11 && menu.ShownRow == HangarFeature.DefaultAirframe,
            $"Cancel keeps the picks and lands back on the chosen row ({flow.DefaultsAsk}, {menu.ShownRow})");
        ctx.Check(menu.ShownRowText.EndsWith("✓", StringComparison.Ordinal) && flow.Scratch.Engine == CustomPlaneDef.EngineNone,
            $"the row is ticked and no engine was loaded ({menu.ShownRowText}, engine {flow.Scratch.Engine})");
        menu.Drive(Accept);
    }

    private static void BuildScreens(TestContext ctx, LaunchMenu menu)
    {
        var flow = menu.Hangar!;
        // The previous step's confirm on the standing pick advanced instead of asking.
        ctx.Check(flow.Screen == HangarScreen.Engine, $"the second confirm on the pick advances to the engine screen ({flow.Screen})");
        ctx.Check(menu.ShownRowCount == CustomPlaneDef.EngineNone + 1 && menu.ShownRow == CustomPlaneDef.EngineNone,
            $"seven engine rows, opening on the standing pick, none ({menu.ShownRowCount}, {menu.ShownRow})");
        menu.Drive(Down);
        menu.Drive(Down);
        ctx.Check(menu.ShownRow == 1, $"Down wraps onto the first engine and one more onto the second ({menu.ShownRow})");
        menu.Drive(Accept);
        ctx.Check(flow.Scratch.Engine == 1 && flow.Screen == HangarScreen.Engine, $"Accept picks engine 1 and stays ({flow.Scratch.Engine}, {flow.Screen})");
        Has(ctx, "the detail prices the engine", "Power", menu.ShownDetail);
        menu.Drive(Accept);
        ctx.Check(flow.Screen == HangarScreen.Armour && menu.ShownRowCount == 4, $"Accept again advances to armor with four zones ({flow.Screen}, {menu.ShownRowCount})");
        menu.Drive(Right);
        ctx.Check(flow.Scratch.ArmourNose == 1 && menu.ShownRowText.Contains("5", StringComparison.Ordinal),
            $"Right buys one unit of nose armour, shown x5 ({flow.Scratch.ArmourNose}, {menu.ShownRowText})");
        Has(ctx, "the footer names the stepper", "←→  Change", menu.ShownFooter);
        menu.Drive(Accept);
        ctx.Check(flow.Screen == HangarScreen.Guns && menu.ShownRowCount == 4, $"guns: four slots ({flow.Screen}, {menu.ShownRowCount})");
        menu.Drive(Right);
        ctx.Check(flow.Scratch.Guns[0] == new GunChoice(0, false), $"Right mounts the first calibre single ({flow.Scratch.Guns[0]})");
        Has(ctx, "the row names it", flow.GunName(flow.Scratch.Guns[0]), menu.ShownRowText);
        menu.Drive(Accept);
        ctx.Check(flow.Screen == HangarScreen.Hardpoints && menu.ShownRowCount == 2, $"hardpoints: two wings ({flow.Screen}, {menu.ShownRowCount})");
        menu.Drive(Right);
        ctx.Check(flow.Scratch.LeftHardpoints == 1, $"Right adds a left-wing hardpoint ({flow.Scratch.LeftHardpoints})");
        menu.Drive(Accept);
        ctx.Check(flow.Screen == HangarScreen.Paint && menu.ShownRowCount == 10, $"paint: the pattern, three colour pairs and three decals ({flow.Screen}, {menu.ShownRowCount})");
        Has(ctx, "the pattern row", "Pattern:", menu.ShownRowText);
        int pattern = flow.Scratch.PaintPattern;
        menu.Drive(Right);
        ctx.Check(flow.Scratch.PaintPattern != pattern && HangarPaintTables.Default.Available(flow.Scratch.PaintPattern, flow.Scratch.Airframe),
            $"Right steps to the next pattern this airframe may wear ({pattern} -> {flow.Scratch.PaintPattern})");
        ctx.Check(flow.Page.Art != null, $"the paint screen composes a preview");
        menu.Drive(Back);
        ctx.Check(flow.Screen == HangarScreen.Hardpoints, $"Back returns to hardpoints ({flow.Screen})");
        menu.Drive(Accept);
    }

    private static void NameAndPurchase(TestContext ctx, LaunchMenu menu, CustomPlaneStore store, string scratch)
    {
        var flow = menu.Hangar!;
        menu.Drive(Accept);
        ctx.Check(flow.Screen == HangarScreen.Name && menu.ShownRowCount == 4, $"paint's Accept opens the name screen with four rows ({flow.Screen}, {menu.ShownRowCount})");
        Is(ctx, "its heading", flow.Page.Title, menu.ShownHeading);
        ctx.Check(menu.ShownRowText.StartsWith("Name: ", StringComparison.Ordinal) && flow.Scratch.Name.Length > 0,
            $"the screen rolled a name onto the nameless plane ({menu.ShownRowText})");
        Has(ctx, "the footer names typing", "Type / Backspace  Rename", menu.ShownFooter);
        string rolled = flow.Scratch.Name;
        menu.Drive(Down);
        menu.Drive(Right);
        ctx.Check(flow.Scratch.Name != rolled, $"stepping the adjective renames ({rolled} -> {flow.Scratch.Name})");

        // Typing arrives through the key events, not the semantic frame, so the page is typed at
        // directly: the same calls the launchscreen's own handler makes.
        var page = flow.Page as HangarNamePage;
        ctx.Check(page != null, $"the name screen's page is the name page");
        if (page == null)
        {
            return;
        }

        while (flow.Scratch.Name.Length > 0)
        {
            page.Backspace();
        }

        foreach (char c in scratch)
        {
            page.Type(c);
        }

        ctx.Check(flow.Scratch.Name == scratch && page.Freeform, $"typing sets a freeform name ({flow.Scratch.Name})");
        menu.Drive(Up);
        ctx.Check(menu.ShownDetail.Length == 0, $"a free name warns of no overwrite ({menu.ShownDetail})");
        menu.Drive(Accept);
        ctx.Check(flow.Screen == HangarScreen.Purchase, $"Accept on the name row opens the purchase review ({flow.Screen})");
        Is(ctx, "its heading", flow.Page.Title, menu.ShownHeading);
        ctx.Check(menu.ShownRowCount >= 6, $"one row per priced component, the totals and Purchase Now ({menu.ShownRowCount})");
        menu.Drive(Up);
        Is(ctx, "the last row is Purchase Now", flow.Strings.Text(1199, "Purchase Now"), menu.ShownRowText);
        menu.Drive(Up);
        ctx.Check(menu.ShownDetail == flow.TotalsLine && flow.TotalsLine.StartsWith("$", StringComparison.Ordinal),
            $"the totals row details the totals line ({menu.ShownDetail})");
        menu.Drive(Down);
        ctx.Check(store.Load(scratch) == null, $"nothing is in the store before the press");
        menu.Drive(Accept);
        ctx.Check(menu.ShownScreen == "Mode" && menu.Hangar == null, $"Purchase Now saves and returns to the door's screen ({menu.ShownScreen})");
        ctx.Check(menu.LastBuiltPlane == scratch && store.Load(scratch) is { Airframe: HangarFeature.DefaultAirframe, Engine: 1 },
            $"the store holds the scratch plane as built ({menu.LastBuiltPlane})");
        menu.ShowMenu("plane");
        Is(ctx, "the plane pick opens with the cursor on the new plane", scratch, menu.ShownRowText);
    }

    private static void EditAndCancel(TestContext ctx, LaunchMenu menu, CustomPlaneStore store, string scratch)
    {
        string before = File.ReadAllText(store.PathFor(scratch));
        menu.ShowMenu();
        WalkTo(menu, LaunchMenu.HangarRow);
        menu.Drive(Accept);
        WalkTo(menu, scratch);
        Is(ctx, "the saved plane lists on plane selection", scratch, menu.ShownRowText);
        Is(ctx, "described by its airframe", menu.Hangar!.AirframeName(HangarFeature.DefaultAirframe), menu.ShownDetail);
        ctx.Check(menu.Hangar.TotalsLine.StartsWith("$", StringComparison.Ordinal), $"priced on the totals line ({menu.Hangar.TotalsLine})");
        menu.Drive(Accept);
        var flow = menu.Hangar;
        ctx.Check(flow.Screen == HangarScreen.Airframe && flow.EditingName == scratch && flow.AirframeChosen && menu.ShownRow == HangarFeature.DefaultAirframe,
            $"Accept opens a copy to edit on its ticked airframe ({flow.Screen}, {flow.EditingName}, row {menu.ShownRow})");
        menu.Drive(Accept);
        menu.Drive(Down);
        menu.Drive(Accept);
        ctx.Check(flow.Scratch.Engine != 1, $"the engine is changed on the copy ({flow.Scratch.Engine})");
        menu.Drive(Back);
        menu.Drive(Back);
        ctx.Check(flow.Screen == HangarScreen.PlaneSelection, $"Back twice returns to plane selection ({flow.Screen})");
        menu.Drive(Back);
        ctx.Check(menu.ShownScreen == "Mode" && menu.Hangar == null, $"Back once more cancels ({menu.ShownScreen})");
        ctx.Check(File.ReadAllText(store.PathFor(scratch)) == before, $"and the file is byte for byte what it was");
    }

    private static void PlaneScreenDoor(TestContext ctx, LaunchMenu menu)
    {
        menu.ShowMenu("environment");
        menu.Drive(Accept);
        Is(ctx, "the first mission is the ace duel", "Dogfighting an Ace", menu.ShownRowText);
        menu.Drive(Accept);
        ctx.Check(menu.ShownScreen == "Plane", $"the ace duel opens the aircraft screen ({menu.ShownScreen})");
        Is(ctx, "whose trailing row is the hangar door", LaunchMenu.HangarRow, RowText(menu, menu.ShownRowCount - 1));
        WalkTo(menu, LaunchMenu.HangarRow);
        Is(ctx, "described as the door", "Build a plane in the hangar and fly it.", menu.ShownDetail);
        menu.Drive(Accept);
        ctx.Check(menu.ShownScreen == "Hangar" && menu.Hangar is { Campaign: null }, $"Accept opens the wallet-free flow ({menu.ShownScreen})");
        menu.Drive(Back);
        ctx.Check(menu.ShownScreen == "Plane", $"Back returns to the aircraft screen the door was pressed on ({menu.ShownScreen})");
    }

    private static void Aids(TestContext ctx, LaunchMenu menu)
    {
        menu.ShowMenu("hangar");
        ctx.Check(menu.ShownScreen == "Hangar" && menu.Hangar?.Screen == HangarScreen.PlaneSelection,
            $"--menu=hangar opens plane selection ({menu.ShownScreen}, {menu.Hangar?.Screen})");
        menu.ShowMenu("airframe");
        ctx.Check(menu.Hangar?.Screen == HangarScreen.Airframe && menu.Hangar.AirframeChosen == false && menu.ShownRow == 0,
            $"--menu=airframe opens the airframe list unticked ({menu.Hangar?.Screen}, row {menu.ShownRow})");
        menu.ShowMenu("defaults");
        ctx.Check(menu.Hangar?.Screen == HangarScreen.Airframe && menu.Hangar.DefaultsAsk == 0,
            $"--menu=defaults opens the same screen mid-ask over the first airframe ({menu.Hangar?.DefaultsAsk})");
        menu.ShowMenu("name");
        ctx.Check(menu.Hangar?.Screen == HangarScreen.Name && menu.Hangar.Scratch.Name.Length > 0,
            $"--menu=name opens the name screen with a rolled name ({menu.Hangar?.Scratch.Name})");
        menu.ShowMenu("paint");
        ctx.Check(menu.Hangar?.Screen == HangarScreen.Paint && menu.Hangar.Scratch is { Airframe: 7, PaintPattern: 4, NoseDecal: 40 } && menu.ShownRow == HangarPaintPage.NoseDecalRow,
            $"--menu=paint opens the paint screen on a Fury in Fortune Hunters colours with a nose decal ({menu.Hangar?.Scratch.Airframe}, {menu.Hangar?.Scratch.PaintPattern}, {menu.Hangar?.Scratch.NoseDecal})");
        ctx.Check(menu.Hangar?.Page.RowArt(menu.ShownRow) != null, $"with the decal's tile beside the rows");
    }

    private static void CampaignDoor(TestContext ctx, LaunchMenu menu)
    {
        menu.ShowMenu("campaign-hangar");
        // The aid makes the cabin's press; the flow's exit is consumed on the next frame, as a
        // player's own press would be.
        menu.Drive(MenuCommands.None);
        ctx.Check(menu.ShownScreen == "Hangar" && menu.Hangar is { Campaign: not null, Screen: HangarScreen.PlaneSelection },
            $"--menu=campaign-hangar opens the flow over the scratch profile's wallet ({menu.ShownScreen})");
        if (menu.Hangar is not { } flow)
        {
            return;
        }

        Is(ctx, "as the profile's inventory", flow.Strings.Text(1257, "INVENTORY"), menu.ShownHeading);
        Is(ctx, "whose first row buys", "Buy a New Plane", menu.ShownRowText);
        Has(ctx, "over the wallet", flow.Strings.Text(1149, "$$$ on Hand:"), menu.ShownDetail);
        ctx.Check(ReferenceEquals(flow.Feature.Wallet, flow.Campaign), $"the shared feature prices against the same wallet");
        int owned = flow.Saved.Count;
        ctx.Check(owned >= 2, $"the profile owns its starting planes ({owned})");
        menu.Drive(Up);
        Is(ctx, "the trailing row sells", "Sell a plane", menu.ShownRowText);
        menu.Drive(Accept);
        ctx.Check(menu.ShownRowCount == owned + 1, $"the sale stage lists every owned plane and Cancel ({menu.ShownRowCount})");
        ctx.Check(menu.ShownRowText.StartsWith("Sell ", StringComparison.Ordinal), $"each row names the plane it sells ({menu.ShownRowText})");
        menu.Drive(Back);
        ctx.Check(menu.ShownScreen == "Campaign" && menu.Hangar == null, $"Back cancels the flow and resumes the cabin behind it ({menu.ShownScreen})");
    }

    private static void Delete(TestContext ctx, LaunchMenu menu, CustomPlaneStore store, string scratch)
    {
        menu.ShowMenu();
        WalkTo(menu, LaunchMenu.HangarRow);
        menu.Drive(Accept);
        WalkTo(menu, "Delete a saved plane");
        Is(ctx, "the trailing row opens the delete stage", "Delete a saved plane", menu.ShownRowText);
        menu.Drive(Accept);
        ctx.Check(menu.ShownRow == 0 && menu.ShownRowText.StartsWith("Delete ", StringComparison.Ordinal),
            $"the stage lists every saved plane by name ({menu.ShownRowText})");
        WalkTo(menu, "Delete " + scratch);
        Is(ctx, "the scratch plane's own row", "Delete " + scratch, menu.ShownRowText);
        Has(ctx, "described as final", "for good", menu.ShownDetail);
        menu.Drive(Accept);
        ctx.Check(store.Load(scratch) == null, $"Accept removes the file");
        ctx.Check(menu.Hangar != null && menu.ShownScreen == "Hangar", $"and the flow stays open ({menu.ShownScreen})");
        menu.Drive(Back);
        ctx.Check(menu.ShownScreen == "Mode", $"Back leaves for the Mode screen ({menu.ShownScreen})");
    }

    private static void OriginalNameScreen(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, HangarFeature hangar, string scratch)
    {
        var door = Row(shell, OriginalShell.HangarKey);
        ctx.Check(door is { Enabled: true }, $"the top level's BUILD PLANE door is live");
        if (door == null)
        {
            return;
        }

        Press(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.PlaneName && hangar.IsOpen && hangar.Wallet == null,
            $"a click opens the decoded name screen over a wallet-free build ({shell.Screen})");
        ctx.Check(Row(shell, OriginalShell.NameFieldKey) is { X: 23f, Y: 40f, Width: 211f } && Row(shell, OriginalShell.NameOkKey) is { Enabled: false },
            $"the edit box stands at its authored line and OK waits for a name");
        ctx.Check(host.Seats[0].CapturingText, $"seat 0 captures text on the name screen");
        Press(host, seat, new MenuCommands { Typed = scratch });
        ctx.Check(shell.HangarName == scratch && Row(shell, OriginalShell.NameOkKey) is { Enabled: true },
            $"typed frames name the plane and OK stands ({shell.HangarName})");
        var ok = Row(shell, OriginalShell.NameOkKey)!;
        Press(host, seat, Pointer(fit, ok.X + 5f, ok.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.HangarAirframe && hangar.Scratch.Name == scratch,
            $"OK opens the hub on the airframe tab under the typed name ({shell.Screen}, {hangar.Scratch.Name})");
        ctx.Check(hangar.AirframeChosen && hangar.Scratch.Airframe == HangarFeature.DefaultAirframe && hangar.Scratch.Engine == 1,
            $"with the default configuration loaded ({hangar.Scratch.Airframe}, engine {hangar.Scratch.Engine})");
        ctx.Check(!host.Seats[0].CapturingText, $"and text capture ends with the name screen");
    }

    private static void OriginalHub(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, HangarFeature hangar)
    {
        ctx.Check(Row(shell, "PX_B_AIRFRAME") is { Enabled: false, X: 23f, Y: 524f } && Row(shell, "PX_B_PAINT") is { Enabled: true, X: 662f },
            $"the tab bar stands at its authored line with the standing tab disabled");
        var paint = Row(shell, "PX_B_PAINT")!;
        Press(host, seat, Pointer(fit, paint.X + 5f, paint.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.HangarPaint, $"a click on Paint opens the paint tab out of order ({shell.Screen})");
        ctx.Check(Row(shell, OriginalShell.PatternDropKey) is { X: 445f, Y: 120f, Width: 225f, Height: 15f },
            $"the pattern dropdown stands at its authored box ({Row(shell, OriginalShell.PatternDropKey)?.X})");
        ctx.Check(Row(shell, "PT_D_DECALS0") is { Height: 73f }, $"the decal boxes are their authored 73 high");
        var engineTab = Row(shell, "PX_B_ENGINE")!;
        Press(host, seat, Pointer(fit, engineTab.X + 5f, engineTab.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.HangarEngine && shell.FocusedKey == OriginalShell.EngineDropKey,
            $"a click on Engine opens the engine tab focused on its dropdown ({shell.Screen}, {shell.FocusedKey})");
        int cost = hangar.Bill.Total.Cost;
        Press(host, seat, Right);
        ctx.Check(hangar.Scratch.Engine == 2 && hangar.Bill.Total.Cost != cost,
            $"Right steps the engine and the running total follows ({hangar.Scratch.Engine}, ${cost} -> ${hangar.Bill.Total.Cost})");
        ctx.Check(Row(shell, OriginalShell.EngineDropKey)?.Label == hangar.EngineName(hangar.Scratch.Airframe, 2),
            $"the box shows the new engine ({Row(shell, OriginalShell.EngineDropKey)?.Label})");
        Press(host, seat, Accept);
        ctx.Check(shell.OpenHangarDropdown == OriginalShell.EngineDropKey && shell.Rows.Count == CustomPlaneDef.EngineNone + 1,
            $"Accept opens the seven-row engine list ({shell.OpenHangarDropdown}, {shell.Rows.Count})");
        Press(host, seat, Up);
        Press(host, seat, Accept);
        ctx.Check(shell.OpenHangarDropdown == null && hangar.Scratch.Engine == 1,
            $"Up and Accept pick the row above and close the list ({hangar.Scratch.Engine})");
        var board = shell.Compose();
        ctx.Check(board.Backdrop.Count == 1 && board.Backdrop[0].Art.Name == "PX_BackGround.jpg",
            $"the hub's background is the layout's own ({board.Backdrop.Count})");
        ctx.Check(board.Pictures.Any(p => p.Art.Name.StartsWith("PX_ICON_5_", StringComparison.Ordinal) && p.Tint != null),
            $"the plane is the paint composite of the Devastator's icon set on every tab but the airframe's");
        Press(host, seat, Down);
        ctx.Check(shell.FocusedKey == "PX_B_AIRFRAME", $"Down from the dropdown lands on the first tab ({shell.FocusedKey})");
        Press(host, seat, Right);
        ctx.Check(shell.FocusedKey == "PX_B_ARMOR", $"Right walks the bar past the standing tab ({shell.FocusedKey})");
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.HangarArmor && Row(shell, "AR_D_POINT0") is { X: 490f, Y: 133f, Width: 225f },
            $"Accept on a tab opens it with its dropdowns at their authored boxes ({shell.Screen})");
    }

    private static void OriginalPurchase(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, HangarFeature hangar, PlayerSetupFeature setup, CustomPlaneStore store, string scratch)
    {
        var ready = Row(shell, OriginalShell.ReadyKey);
        ctx.Check(ready is { Enabled: true, X: 400f, Y: 564f }, $"READY TO PURCHASE stands at its authored place");
        if (ready == null)
        {
            return;
        }

        Press(host, seat, Pointer(fit, ready.X + 5f, ready.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.HangarPurchase && Row(shell, OriginalShell.PurchaseNowKey) is { Enabled: true, X: 510f, Y: 465f },
            $"it opens the totals page with Purchase Now live ({shell.Screen})");
        var board = shell.Compose();
        ctx.Check(board.Lines.Any(l => l.Text == "$" + hangar.Bill.Total.Cost) && board.Lines.Any(l => l.Text == hangar.AirframeName(hangar.Scratch.Airframe)),
            $"the page lists the airframe and the total cost");
        Press(host, seat, Back);
        ctx.Check(shell.Screen == OriginalScreen.HangarArmor, $"Back returns to the tab the hub last showed ({shell.Screen})");
        Press(host, seat, Pointer(fit, ready.X + 5f, ready.Y + 5f, pressed: true, clicked: true));
        var purchase = Row(shell, OriginalShell.PurchaseNowKey)!;
        ctx.Check(store.Load(scratch) == null, $"nothing is in the store before the press");
        Press(host, seat, Pointer(fit, purchase.X + 5f, purchase.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.TopLevel && shell.LastBuiltPlane == scratch && !hangar.IsOpen,
            $"Purchase Now saves, drops the build and returns to the top level ({shell.Screen}, {shell.LastBuiltPlane})");
        ctx.Check(store.Load(scratch) is { Airframe: HangarFeature.DefaultAirframe, Engine: 1 }, $"the store holds the plane as built");
        ctx.Check(setup.Roster is var roster && Contains(roster, scratch), $"and the shared roster lists it without a return to the top level");
    }

    private static void OriginalInventory(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, HangarFeature hangar, PlayerSetupFeature setup, CustomPlaneStore store, string scratch)
    {
        var door = Row(shell, OriginalShell.HangarKey)!;
        Press(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
        Press(host, seat, new MenuCommands { Typed = "Other" });
        var ok = Row(shell, OriginalShell.NameOkKey)!;
        Press(host, seat, Pointer(fit, ok.X + 5f, ok.Y + 5f, pressed: true, clicked: true));
        var sell = Row(shell, OriginalShell.SellPlanesKey);
        ctx.Check(sell is { Enabled: true, X: 100f, Y: 564f }, $"SELL PLANES stands at its authored place");
        if (sell == null)
        {
            return;
        }

        Press(host, seat, Pointer(fit, sell.X + 5f, sell.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.HangarInventory && Row(shell, OriginalShell.InventoryPlanesKey) is { X: 138f, Y: 132f, Width: 271f },
            $"it opens the inventory with the plane dropdown at its authored box ({shell.Screen})");
        ctx.Check(Row(shell, OriginalShell.InventoryExportKey) is { Enabled: false }, $"Export draws disabled, the campaign's verb");
        int index = IndexOf(hangar.Saved, scratch);
        ctx.Check(index >= 0, $"the inventory lists the scratch plane ({hangar.Saved.Count} saved)");
        var planes = Row(shell, OriginalShell.InventoryPlanesKey)!;
        Press(host, seat, Pointer(fit, planes.X + 5f, planes.Y + 5f, pressed: true, clicked: true));
        var item = Row(shell, OriginalShell.InventoryPlanesKey + ":" + index);
        ctx.Check(item != null, $"the open list carries its row");
        if (item == null)
        {
            return;
        }

        if (!item.Visible)
        {
            // A long store scrolls the list; the keyboard walks onto the row and the window follows.
            for (int i = 0; i < hangar.Saved.Count && shell.FocusedKey != item.Key; i++)
            {
                Press(host, seat, Down);
            }

            Press(host, seat, Accept);
        }
        else
        {
            Press(host, seat, Pointer(fit, item.X + 5f, item.Y + 5f, pressed: true, clicked: true));
        }

        ctx.Check(shell.InventoryIndex == index && Row(shell, OriginalShell.InventoryPlanesKey)?.Label == scratch,
            $"picking it puts it in the box ({Row(shell, OriginalShell.InventoryPlanesKey)?.Label})");
        var sellButton = Row(shell, OriginalShell.InventorySellKey)!;
        Press(host, seat, Pointer(fit, sellButton.X + 5f, sellButton.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Dialog != null && shell.FocusedKey == OriginalShell.DialogYesKey && store.Load(scratch) != null,
            $"Sell asks first with the two-answer messagebox opening on Yes ({shell.Dialog?.Message})");
        var yes = Row(shell, OriginalShell.DialogYesKey);
        ctx.Check(yes != null, $"whose Yes stands at the messagebox's left row");
        if (yes == null)
        {
            return;
        }

        Press(host, seat, Pointer(fit, yes.X + 5f, yes.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Dialog == null && store.Load(scratch) == null && IndexOf(hangar.Saved, scratch) < 0,
            $"Yes removes the plane from the store and the roster");
        ctx.Check(!Contains(setup.Roster, scratch), $"and from the shared aircraft roster");
        ctx.Check(hangar.Scratch.Name == "Other" && hangar.IsOpen, $"the open build is untouched ({hangar.Scratch.Name})");
        var done = Row(shell, OriginalShell.InventoryDoneKey)!;
        Press(host, seat, Pointer(fit, done.X + 5f, done.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.HangarAirframe, $"Done returns to the tab ({shell.Screen})");
        var cancel = Row(shell, OriginalShell.CancelBuildKey)!;
        Press(host, seat, Pointer(fit, cancel.X + 5f, cancel.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.TopLevel && !hangar.IsOpen && store.Load("Other") == null,
            $"CANCEL drops the build and leaves no residue ({shell.Screen})");
    }

    private static void OriginalSwitch(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, HangarFeature hangar, CustomPlaneStore store)
    {
        var door = Row(shell, OriginalShell.HangarKey)!;
        Press(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
        Press(host, seat, new MenuCommands { Typed = "Dropped" });
        var ok = Row(shell, OriginalShell.NameOkKey)!;
        Press(host, seat, Pointer(fit, ok.X + 5f, ok.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.HangarAirframe && hangar.IsOpen, $"mid-build on the hub ({shell.Screen})");
        host.Deactivate();
        ctx.Check(!hangar.IsOpen && hangar.Scratch.Name.Length == 0 && store.Load("Dropped") == null,
            $"Deactivate discards the open build without committing ({hangar.Scratch.Name})");
        ctx.Check(!host.Seats[0].CapturingText, $"and seat 0 no longer captures text");
    }

    private static bool Contains(IReadOnlyList<MenuAircraft> roster, string name)
    {
        foreach (var aircraft in roster)
        {
            if (aircraft.Name == name)
            {
                return true;
            }
        }

        return false;
    }

    private static int IndexOf(IReadOnlyList<CustomPlaneDef> saved, string name)
    {
        for (int i = 0; i < saved.Count; i++)
        {
            if (saved[i].Name == name)
            {
                return i;
            }
        }

        return -1;
    }

    private static OriginalRow? Row(OriginalShell shell, string key)
    {
        foreach (var row in shell.Rows)
        {
            if (row.Key == key)
            {
                return row;
            }
        }

        return null;
    }

    private static MenuCommands Pointer(BoardFit fit, float authoredX, float authoredY, bool pressed = false, bool clicked = false) =>
        new() { Pointer = new MenuPointer(fit.X(authoredX), fit.Y(authoredY), pressed, clicked) };

    private static void Press(MenuHost host, ScriptedSeat seat, MenuCommands frame)
    {
        seat.Enqueue(frame);
        host.Tick(1f / 60f);
    }

    // Walks the cursor onto the row carrying a text, at most one lap; the cursor stays put when no
    // row carries it.
    private static void WalkTo(LaunchMenu menu, string text)
    {
        int count = menu.ShownRowCount;
        for (int i = 0; i < count && menu.ShownRowText != text; i++)
        {
            menu.Drive(Down);
        }
    }

    // The text of an absolute row: the cursor is walked there and back so the read-out is the
    // screen's own, and the cursor ends where it started.
    private static string RowText(LaunchMenu menu, int row)
    {
        int start = menu.ShownRow;
        int count = menu.ShownRowCount;
        int steps = ((row - start) % count + count) % count;
        for (int i = 0; i < steps; i++)
        {
            menu.Drive(Down);
        }

        string text = menu.ShownRowText;
        for (int i = 0; i < steps; i++)
        {
            menu.Drive(Up);
        }

        return text;
    }

    private static void Is(TestContext ctx, string what, string expected, string actual) =>
        ctx.Check(expected == actual, $"{what}: expected '{expected}', got '{actual}'");

    private static void Has(TestContext ctx, string what, string expected, string actual) =>
        ctx.Check(actual.Contains(expected, StringComparison.Ordinal),
            $"{what}: expected '{expected}' in '{actual}'");

    private sealed class ScriptedSeat : IMenuInputSource
    {
        private readonly Queue<MenuCommands> _frames = new();

        public string DeviceLabel => "scripted";

        public bool CapturingText { get; set; }

        public void Enqueue(MenuCommands frame) => _frames.Enqueue(frame);

        public MenuCommands Poll(float dt) => _frames.Count > 0 ? _frames.Dequeue() : MenuCommands.None;

        public void Prime()
        {
        }
    }
}
