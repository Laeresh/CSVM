using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Session.Campaign;
using CSVM.UI.Boards;
using CSVM.UI.Campaign;
using CSVM.UI.Hangar;
using CSVM.UI.Menu;
using CSVM.UI.Menu.Original;
using CSVM.UI.Screens;

namespace CSVM.Testing;

/// <summary>
/// The hangar through Original and a real <see cref="MenuHost"/> over the install's layout. The
/// suite description lists what it pins. The presentation reads and writes a scratch store of the
/// suite's own.
/// </summary>
internal static class MenuHangarSuites
{
    // The weight line the set-airframe swap writes, IDS_PX_OVERALLSPEED_TITLE, whose last word is
    // the one this suite reads back off the hub (docs/org/hangar.md, "The two red figures").
    private const int PendingWeightString = 1032;

    // The stand-in metric the description box's lines are counted with here: the box's own authored
    // pitch, and a character width close enough to the shipped face to wrap a body the same number
    // of times. Nothing on the screen is measured from these, only this suite's own arithmetic.
    private const float FlowedLine = 14f;
    private const float FlowedGlyph = 6f;

    private static readonly MenuCommands Accept = new() { Accept = true };
    private static readonly MenuCommands Back = new() { Back = true };
    private static readonly MenuCommands Down = new() { MoveY = 1 };
    private static readonly MenuCommands Up = new() { MoveY = -1 };
    private static readonly MenuCommands Right = new() { MoveX = 1 };
    private static readonly MenuCommands Left = new() { MoveX = -1 };

    [Suite("menu-original-hangar",
        "Original's hangar through the presentation boundary over the install's decoded layout: the "
        + "Instant Action screen's Build Custom Plane opens the decoded name screen wallet-free on its "
        + "centred pane, OK on the empty box raises the one-button refusal and puts the cursor back, typed "
        + "frames name the plane and OK opens the Plane Construction hub on the default configuration, "
        + "the tabs are siblings a click and the keyboard reach out of order with all six labels on one "
        + "baseline clear of the strips' bottom, a dropdown steps and picks "
        + "through the shared feature with the running total following, READY TO PURCHASE and the commit "
        + "(reading Export on this door, centred on its paper plaque) save the scratch plane into the "
        + "suite's scratch plane store and the shared roster and return to the "
        + "Instant Action screen with it in the Pilot Plane list, SELL PLANES opens the inventory, which "
        + "wallet-free builds no Export row and deletes instead of selling, standing the picked plane's "
        + "line centred in its dashed box and leaving its "
        + "Value row unwritten and asking the unpriced delete "
        + "question in the query box, CANCEL and Back return to the Instant Action screen with "
        + "no residue, the cabin's PLANE CONSTRUCTION draws the cash note on every tab and the totals "
        + "page with every combo row bare and an over-priced one still pickable while the wallet-free "
        + "door draws the same note over its export funds, that door's own inventory keeps Export live "
        + "beside the shipped Sell, writes that Value row and asks the priced sale question, an airframe swap asks nothing over "
        + "an unedited build and raises the three-button query box over an edited one, whose Cancel puts "
        + "the airframe back and whose Yes takes the stock build, the hub's PLANE COST is red past "
        + "the wallet and its CURRENT WEIGHT red past the airframe's capacity and both plain otherwise, "
        + "an open list's focused row is what the two figures price without taking it, a previewed "
        + "airframe row instead leaving that weight line on the shipped Pending word in the page's ink "
        + "where a hardpoint row over the same capacity keeps the figure and the red, a tab page's "
        + "description box follows the shipped figures with the heading that string ends with and the "
        + "component's own prose flowed inside the authored box, a body longer than that box standing "
        + "as a window the wheel takes to the stretch the unscrolled box cut and a changed body putting "
        + "it back at its head, a decal list opens as the page's "
        + "five-across two-down grid of the sheet's own tiles with its scrollbar counting rows of "
        + "five, and a switch discards an open build")]
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
        var store = MenuSuiteHost.ScratchPlanes(ctx, "menu-original-hangar");
        registry.Register(PresentationId.Original, () => new OriginalPresentation(
            ctx.Host, ctx.DataRoot, layout, string.Empty, new MenuInput { Keyboard = true }, data: ctx.Data)
        {
            Planes = store,
        });
        var host = new MenuHost(registry, new MenuSuiteHost.SilentMenuAudio(), exits.Add);
        MenuSuiteHost.AddFeatures(host, ctx.DataRoot, ctx.Data);
        host.AddSeat(seat);
        string scratch = ScratchName();
        ctx.Check(store.List().Count == 0, $"the suite's scratch store starts empty ({store.List().Count})");
        try
        {
            host.Select("original");
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
            OriginalNameScreen(ctx, host, seat, shell, fit, hangar, scratch, layout);
            OriginalHub(ctx, host, seat, shell, fit, hangar, layout);
            OriginalTabPageBoxes(ctx, host, seat, shell, fit, hangar, layout);
            OriginalDefaultsAsk(ctx, host, seat, shell, fit, hangar);
            OriginalPurchase(ctx, host, seat, shell, fit, hangar, setup, store, scratch, layout);
            OriginalInventory(ctx, host, seat, shell, fit, hangar, setup, store, scratch, layout);
            OriginalWallet(ctx, host, seat, shell, fit, hangar, layout);
            OriginalSwitch(ctx, host, seat, shell, fit, hangar, store);
        }
        finally
        {
            host.Deactivate();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            MenuSuiteHost.DropScratchPlanes(ctx, "menu-original-hangar");
        }
    }

    // A name no player would type, distinct per run, inside the name screen's own character set.
    private static string ScratchName() => "Scratch " + Guid.NewGuid().ToString("N")[..8];

    // OK pressed on an empty box: PLANENAME.SCRIPT's own refusal, the one-button messagebox on
    // langui 203 under the warning icon, drawn over the dialog and leaving the cursor in the box.
    private static void OriginalEmptyNameRefusal(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit)
    {
        var ok = Row(shell, OriginalHangarScreen.NameOkKey);
        if (ok == null)
        {
            return;
        }

        Click(host, seat, Pointer(fit, ok.X + 5f, ok.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Dialog is { Icon: DialogIcon.Warning } && shell.Screen == OriginalScreen.PlaneName,
            $"OK on an empty box raises the refusal over the dialog ({shell.Dialog?.Icon.ToString() ?? "none"})");
        ctx.Check(!host.Seats[0].CapturingText, $"and the box behind it takes no typing while it stands");
        var answer = Row(shell, OriginalShell.DialogOkKey);
        if (answer == null)
        {
            return;
        }

        Click(host, seat, Pointer(fit, answer.X + 5f, answer.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Dialog == null && shell.FocusedKey == OriginalHangarScreen.NameFieldKey,
            $"its one OK closes it and puts the cursor back in the box ({shell.FocusedKey})");
    }

    private static void OriginalNameScreen(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, HangarFeature hangar, string scratch, MenuLayout layout)
    {
        ctx.Check(Row(shell, "HANGAR") == null, $"the top level carries no hangar door of its own");
        var door = BuildDoor(host, seat, shell, fit);
        ctx.Check(shell.Screen == OriginalScreen.InstantAction && door is { Enabled: true },
            $"the Instant Action screen's Build Custom Plane is live over the hangar and the store ({shell.Screen})");
        if (door == null)
        {
            return;
        }

        Click(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.PlaneName && hangar.IsOpen && hangar.Wallet == null,
            $"a click opens the decoded name screen over a wallet-free build ({shell.Screen})");
        // The pane centres on the board, and the section's rows are drawn from that corner. On the
        // install the 264x177 pane lands at 268,211 and the edit box's authored 23,40 at 291,251.
        var names = layout.Screen(OriginalHangarScreen.PlaneNameSection);
        string pane = ArtOf(names, OriginalHangarScreen.PlaneNamePaneKey);
        var paneSize = Measure(shell, pane);
        var origin = ((float)Math.Floor((BoardFit.AuthoredWidth - paneSize.Width) / 2f), (float)Math.Floor((BoardFit.AuthoredHeight - paneSize.Height) / 2f));
        var field = Corner(names, OriginalHangarScreen.NameFieldKey);
        float fieldWidth = Field(names, OriginalHangarScreen.NameFieldKey, "Width");
        var okAt = Corner(names, OriginalHangarScreen.NameOkKey);
        ctx.CheckShipped(pane == "PX_PlaneNameBackground.Png" && origin == (268f, 211f)
                && (origin.Item1 + field.X, origin.Item2 + field.Y, fieldWidth) == (291f, 251f, 211f) && (origin.Item1 + okAt.X, origin.Item2 + okAt.Y) == (342f, 341f),
            $"on the install the pane lands at 268,211, the box at 291,251 and OK at 342,341 ({pane}, {origin})");
        ctx.Check(paneSize.Width > 0 && shell.Compose().Backdrop.Any(p => p.Art.Name == pane && (p.X, p.Y) == origin),
            $"the dialog's pane is centred on the board ({pane} at {origin})");
        ctx.Check(Row(shell, OriginalHangarScreen.NameFieldKey) is { } box && (box.X, box.Y, box.Width) == (origin.Item1 + field.X, origin.Item2 + field.Y, fieldWidth)
                && Row(shell, OriginalHangarScreen.NameOkKey) is { Enabled: true } okRow && (okRow.X, okRow.Y) == (origin.Item1 + okAt.X, origin.Item2 + okAt.Y),
            $"the edit box and OK ride the centred pane, OK live on an empty box");
        ctx.Check(host.Seats[0].CapturingText, $"seat 0 captures text on the name screen");
        OriginalEmptyNameRefusal(ctx, host, seat, shell, fit);
        Press(host, seat, new MenuCommands { Typed = scratch });
        ctx.Check(shell.Hangar!.HangarName == scratch && Row(shell, OriginalHangarScreen.NameOkKey) is { Enabled: true },
            $"typed frames name the plane and OK stands ({shell.Hangar!.HangarName})");
        var ok = Row(shell, OriginalHangarScreen.NameOkKey)!;
        Click(host, seat, Pointer(fit, ok.X + 5f, ok.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.HangarAirframe && hangar.Scratch.Name == scratch,
            $"OK opens the hub on the airframe tab under the typed name ({shell.Screen}, {hangar.Scratch.Name})");
        // The default configuration is the stock build of the airframe the door was opened over,
        // which on this door is the Instant Action screen's own Pilot Plane pick.
        int pilotAirframe = host.Features.Get<InstantActionFeature>().PlayerPlaneIndex;
        ctx.Check(hangar.AirframeChosen && hangar.Scratch.Airframe == pilotAirframe && hangar.Scratch.Engine == 1,
            $"with the default configuration loaded over the Pilot Plane pick ({hangar.Scratch.Airframe} of {pilotAirframe}, engine {hangar.Scratch.Engine})");
        ctx.Check(!host.Seats[0].CapturingText, $"and text capture ends with the name screen");
    }

    private static void OriginalHub(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, HangarFeature hangar, MenuLayout layout)
    {
        var hub = layout.Screen("PlaneConstruction");
        var first = Corner(hub, "PX_B_AIRFRAME");
        var last = Corner(hub, "PX_B_PAINT");
        ctx.CheckShipped(first == (23f, 524f) && last.X == 662f, $"on the install the tab bar runs from 23,524 to x 662 ({first}, {last})");
        ctx.Check(Row(shell, "PX_B_AIRFRAME") is { Enabled: true } airframeTab && (airframeTab.X, airframeTab.Y) == first
                && Row(shell, "PX_B_PAINT") is { Enabled: true } paintTab && paintTab.X == last.X,
            $"the tab bar stands at its authored line with no tab gated, the standing one included");
        var paint = Row(shell, "PX_B_PAINT")!;
        Click(host, seat, Pointer(fit, paint.X + 5f, paint.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.HangarPaint, $"a click on Paint opens the paint tab out of order ({shell.Screen})");
        var paintPage = layout.Screen("Paint");
        var pattern = Corner(paintPage, OriginalHangarScreen.PatternDropKey);
        var patternSize = (Field(paintPage, OriginalHangarScreen.PatternDropKey, "Width"), Field(paintPage, OriginalHangarScreen.PatternDropKey, "ItemHeight"));
        float decalHeight = Field(paintPage, "PT_D_DECALS0", "ItemHeight");
        ctx.CheckShipped(pattern == (445f, 120f) && patternSize == (225f, 15f) && decalHeight == 73f,
            $"on the install the pattern box is 445,120, 225 by 15, and the decal boxes 73 high ({pattern}, {patternSize}, {decalHeight})");
        ctx.Check(Row(shell, OriginalHangarScreen.PatternDropKey) is { } patternRow
                && (patternRow.X, patternRow.Y, patternRow.Width, patternRow.Height) == (pattern.X, pattern.Y, patternSize.Item1, patternSize.Item2),
            $"the pattern dropdown stands at its authored box ({Row(shell, OriginalHangarScreen.PatternDropKey)?.X})");
        ctx.Check(Row(shell, "PT_D_DECALS0")?.Height == decalHeight, $"the decal boxes are their authored height ({Row(shell, "PT_D_DECALS0")?.Height})");
        var engineTab = Row(shell, "PX_B_ENGINE")!;
        Click(host, seat, Pointer(fit, engineTab.X + 5f, engineTab.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.HangarEngine && shell.FocusedKey == OriginalHangarScreen.EngineDropKey,
            $"a click on Engine opens the engine tab focused on its dropdown ({shell.Screen}, {shell.FocusedKey})");
        int cost = hangar.Bill.Total.Cost;
        Press(host, seat, Right);
        ctx.Check(hangar.Scratch.Engine == 2 && hangar.Bill.Total.Cost != cost,
            $"Right steps the engine and the running total follows ({hangar.Scratch.Engine}, ${cost} -> ${hangar.Bill.Total.Cost})");
        ctx.Check(Row(shell, OriginalHangarScreen.EngineDropKey)?.Label == hangar.EngineName(hangar.Scratch.Airframe, 2),
            $"the box shows the new engine ({Row(shell, OriginalHangarScreen.EngineDropKey)?.Label})");
        Press(host, seat, Accept);
        ctx.Check(shell.Hangar!.OpenHangarDropdown == OriginalHangarScreen.EngineDropKey && shell.Rows.Count == CustomPlaneDef.EngineNone + 1,
            $"Accept opens the seven-row engine list ({shell.Hangar!.OpenHangarDropdown}, {shell.Rows.Count})");
        Press(host, seat, Up);
        Press(host, seat, Accept);
        ctx.Check(shell.Hangar!.OpenHangarDropdown == null && hangar.Scratch.Engine == 1,
            $"Up and Accept pick the row above and close the list ({hangar.Scratch.Engine})");
        var board = shell.Compose();
        string backdrop = ArtOf(hub, "PX_P_BACKGROUND");
        ctx.CheckShipped(backdrop == "PX_BackGround.jpg", $"on the install the hub's background is PX_BackGround.jpg ({backdrop})");
        ctx.Check(backdrop.Length > 0 && board.Backdrop.Count == 1 && board.Backdrop[0].Art.Name == backdrop,
            $"the hub's background is the layout's own ({board.Backdrop.Count})");
        string composite = $"PX_ICON_{hangar.Scratch.Airframe}_";
        ctx.Check(board.Pictures.Any(p => p.Art.Name.StartsWith(composite, StringComparison.Ordinal) && p.Tint != null),
            $"the plane is the paint composite of the built airframe's icon set on every tab but the airframe's ({composite})");
        // The wallet-free door wears the same note over the export funds its own script writes,
        // and checks no price against them at all.
        string cashTitle = hub?.Widget("PX_T_CASHTITLE")?.Text ?? string.Empty;
        ctx.CheckShipped(cashTitle == hangar.Strings.Text(1149, "$$$ on Hand:"),
            $"on the install the cash note's title is langui 1149 ({cashTitle})");
        ctx.Check(cashTitle.Length > 0 && board.Lines.Any(l => l.Text == cashTitle)
            && board.Lines.Any(l => l.Text == "$50000")
            && Row(shell, OriginalHangarScreen.EngineDropKey)?.Label == hangar.EngineName(hangar.Scratch.Airframe, 1),
            $"the export door's cash note reads $50000 over a bare engine row");
        // A tab's plaque is the bottom of its strip's frame. Its label stands eight pixels up from
        // the frame's foot rather than centred in it. The install's frame is 36 high.
        string tabArt = ArtOf(hub, "PX_B_AIRFRAME");
        float tabHeight = Row(shell, "PX_B_AIRFRAME")?.Height ?? -1f;
        ctx.CheckShipped(tabArt.Equals("PX_Tab.png", StringComparison.OrdinalIgnoreCase) && tabHeight == 36f,
            $"on the install the tabs are PX_Tab.png in 36-pixel frames ({tabArt}, {tabHeight})");
        var tabPlaques = board.Plaques.Where(p => p.Label.Length > 0
            && p.Art.Name.Equals(tabArt, StringComparison.OrdinalIgnoreCase)).ToList();
        ctx.Check(tabPlaques.Count == 6 && tabPlaques.TrueForAll(p => Math.Abs(p.LabelBaseline - (tabHeight - 8f)) < 0.01f),
            $"all six tab labels take one baseline eight pixels clear of the strip's bottom ({tabPlaques.Count} tabs, {tabPlaques.FirstOrDefault()?.LabelBaseline})");
        Press(host, seat, Down);
        ctx.Check(shell.FocusedKey == "PX_B_HARDPOINTS", $"Down from the dropdown lands on the tab drawn under it ({shell.FocusedKey})");
        Press(host, seat, Right);
        Press(host, seat, Right);
        ctx.Check(shell.FocusedKey == "PX_B_AIRFRAME", $"Right walks the bar and wraps at its end onto the first tab ({shell.FocusedKey})");
        Press(host, seat, Right);
        ctx.Check(shell.FocusedKey == "PX_B_ENGINE", $"Right walks the bar onto the standing tab, which is a sibling ({shell.FocusedKey})");
        Press(host, seat, Right);
        ctx.Check(shell.FocusedKey == "PX_B_ARMOR", $"and on to the next ({shell.FocusedKey})");
        Press(host, seat, Accept);
        var armour = layout.Screen("Armor");
        var nose = (Corner(armour, "AR_D_POINT0"), Field(armour, "AR_D_POINT0", "Width"));
        ctx.CheckShipped(nose == ((490f, 133f), 225f), $"on the install the nose box is at 490,133, 225 wide ({nose})");
        ctx.Check(shell.Screen == OriginalScreen.HangarArmor && Row(shell, "AR_D_POINT0") is { } noseRow
                && ((noseRow.X, noseRow.Y), noseRow.Width) == nose,
            $"Accept on a tab opens it with its dropdowns at their authored boxes ({shell.Screen})");
        OriginalArmourWings(ctx, host, seat, shell, fit, hangar);
    }

    // The tab pages' two shipped-layout answers: the description box, which follows its figures
    // with the heading its own info string ends with and the component's prose, and the decal
    // picker, which opens as the page's five-across grid rather than as a column of rows. Leaves
    // the screen on the tab it found so the pages after it read the same hub.
    private static void OriginalTabPageBoxes(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, HangarFeature hangar, MenuLayout layout)
    {
        var here = shell.Screen;
        var engineTab = Row(shell, "PX_B_ENGINE")!;
        Click(host, seat, Pointer(fit, engineTab.X + 5f, engineTab.Y + 5f, pressed: true, clicked: true));
        var board = shell.Compose();
        int engine = hangar.Scratch.Engine;
        var line = HangarEconomy.EngineLine(hangar.Scratch.Airframe, engine);
        var info = hangar.Strings.Text(1154).Split('\n');
        string heading = info.Length >= 2 ? info[^2] : string.Empty;
        string prose = hangar.Strings.Text(3240 + (hangar.Scratch.Airframe * 6) + engine);
        ctx.Check(board.Lines.Any(l => l.Text == "COST: $" + line.Cost.ToString(CultureInfo.InvariantCulture))
            && board.Lines.Any(l => l.Text.StartsWith("TOP SPEED: ", StringComparison.Ordinal))
            && board.Lines.Any(l => l.Text.StartsWith("NITRO-BOOST: ", StringComparison.Ordinal)),
            $"the engine box carries the shipped info string's own figure lines");
        ctx.Check(heading.Length > 0 && board.Lines.Any(l => l.Text == heading),
            $"with the heading that string ends with over the prose ({heading})");
        var note = board.Notes.Count == 1 ? board.Notes[0] : null;
        ctx.Check(note != null && prose.Length > 0 && note.Entries.Count == 1 && note.Entries[0] == prose,
            $"and the engine's own prose row flowed under it ({note?.Entries.Count}, {prose.Length} chars)");
        var enginePage = layout.Screen("Engine");
        float boxFoot = Field(enginePage, "EN_S_ENGINEDESC", "Y") + Field(enginePage, "EN_S_ENGINEDESC", "Height");
        ctx.CheckShipped(boxFoot == 334f + 165f, $"on the install the engine box ends at 334 + 165 ({boxFoot})");
        ctx.Check(note != null && note.Y + note.Height <= boxFoot && note.Cut,
            $"inside the authored box, cut where it runs out of room rather than growing it ({note?.Y}, {note?.Height})");
        OriginalDescriptionScroll(ctx, host, seat, shell, fit);

        // The decal picker: fifty tiles as a five-across, two-down grid on the page, wider than
        // the 87-pixel box it hangs from, its arrows and thumb inside its own right edge.
        var paintTab = Row(shell, "PX_B_PAINT")!;
        Click(host, seat, Pointer(fit, paintTab.X + 5f, paintTab.Y + 5f, pressed: true, clicked: true));
        var closed = Row(shell, "PT_D_DECALS0")!;
        Click(host, seat, Pointer(fit, closed.X + 5f, closed.Y + 5f, pressed: true, clicked: true));
        var cells = shell.Rows.Where(r => r.Visible && r.Kind == OriginalRowKind.ListRow).ToList();
        ctx.Check(cells.Count == 10 && cells.Select(c => c.X).Distinct().Count() == 5 && cells.Select(c => c.Y).Distinct().Count() == 2,
            $"ten tiles show, five across and two down ({cells.Count} cells)");
        if (cells.Count < 5)
        {
            return;
        }

        // A tile is one frame of the sheet PT_P_DECALS names, its frames stacked down the sheet.
        var paint = layout.Screen("Paint");
        string sheet = ArtOf(paint, "PT_P_DECALS");
        float frames = Math.Max(1f, paint?.Widget("PT_P_DECALS")?.Frames ?? 1);
        float tile = Measure(shell, sheet).Height / frames;
        ctx.CheckShipped(sheet.Equals("PX_P_Decals.tga", StringComparison.OrdinalIgnoreCase) && tile == 66f,
            $"on the install the decal sheet is PX_P_Decals.tga in 66-pixel tiles ({sheet}, {tile})");
        ctx.Check(tile > 0f && cells.TrueForAll(c => Math.Abs(c.Width - tile) < 0.01f && Math.Abs(c.Height - tile) < 0.01f),
            $"each cell the decal sheet's own tile ({cells.FirstOrDefault()?.Width} of {tile})");
        ctx.Check(cells[0].X == 407f && cells[0].Y == 375f && cells[4].X == 407f + (4f * tile),
            $"on the page's own rectangle rather than under the box ({cells[0].X}, {cells[0].Y}, {cells[4].X})");
        var window = GridWindow(shell, "PT_D_DECALS0");
        ctx.Check(window.Count == 10 && window.Rows == 2,
            $"the scrollbar counts the grid's ten rows with two showing ({window.Count}, {window.Rows})");
        ctx.CheckShipped(window.ThumbX + window.ThumbWidth <= 406f + 348f,
            $"on the install the thumb stands inside the 348-wide grid from x 406 ({window.ThumbX}, {window.ThumbWidth})");
        ctx.Check(window.ThumbX > cells[4].X && window.ThumbX + window.ThumbWidth <= cells[4].X + tile + window.ThumbWidth + 1f
            && Math.Abs(window.ThumbHeight - (window.TrackHeight * 2f / 10f)) < 1f,
            $"its thumb inside the grid's right edge and filling the track in proportion ({window.ThumbX}, {window.ThumbHeight})");
        var overlay = shell.Compose().Overlays.FirstOrDefault(o => o.Fills.Count > 0);
        ctx.Check(overlay != null && overlay.Lines.Count == 0
            && overlay.Pictures.Count(p => p.Art.Name.Equals(sheet, StringComparison.OrdinalIgnoreCase)) == 10,
            $"the panel draws ten tiles and no names ({overlay?.Pictures.Count}, {overlay?.Lines.Count})");
        int top = window.Top;
        Press(host, seat, new MenuCommands { Pointer = new MenuPointer(fit.X(500f), fit.Y(400f), false, false, 1) });
        var scrolled = GridWindow(shell, "PT_D_DECALS0");
        ctx.Check(scrolled.Top == Math.Min(top + 1, scrolled.LastTop),
            $"a wheel notch moves the window one row of five ({top} -> {scrolled.Top})");
        Press(host, seat, Back);
        ctx.Check(shell.Hangar!.OpenHangarDropdown == null, $"Back closes the grid ({shell.Hangar!.OpenHangarDropdown})");
        var tab = Row(shell, here == OriginalScreen.HangarArmor ? "PX_B_ARMOR" : "PX_B_AIRFRAME")!;
        Click(host, seat, Pointer(fit, tab.X + 5f, tab.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == here, $"and the hub is left on the tab this page found ({shell.Screen})");
    }

    // The armour tab's body runs past its box, which is what the authored S widget's slider and
    // arrows are for: the box becomes a window the wheel moves, and the stretch the unscrolled box
    // cut is drawn once it stands at the foot. Leaves the hub on the armour tab.
    private static void OriginalDescriptionScroll(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit)
    {
        var armourTab = Row(shell, "PX_B_ARMOR")!;
        Click(host, seat, Pointer(fit, armourTab.X + 5f, armourTab.Y + 5f, pressed: true, clicked: true));
        var body = shell.Compose().Notes.FirstOrDefault();
        ctx.Check(body?.Counted != null && body.Entries.Count == 1,
            $"the armour tab's box hands its own line counts back to the shell ({body?.Entries.Count})");
        if (body?.Counted is not { } counted)
        {
            return;
        }

        var rows = body.Rows(Flowed);
        ctx.Check(rows.Total > rows.Fits && rows.Fits > 0,
            $"whose body runs past the room the box holds ({rows.Total} lines for {rows.Fits})");
        string shown = body.Flow(Flowed).Count > 0 ? body.Flow(Flowed)[0].Text : string.Empty;
        ctx.Check(shown.Length > 0 && shown.Length < body.Entries[0].Length,
            $"so the unscrolled box draws a head and cuts the rest ({shown.Length} of {body.Entries[0].Length} chars)");
        // The counts reach the shell the way the renderer sends them, and the frame after is what
        // builds the window from them, which is the compose standing here.
        counted(rows.Total, rows.Fits);
        shell.Compose();
        var bar = GridWindow(shell, OriginalHangarScreen.DescriptionListKey);
        ctx.Check(bar.Scrolls && bar.Count == rows.Total && bar.Rows == rows.Fits,
            $"and the box stands as a window the pointer can move ({bar.Count} lines, {bar.Rows} showing)");
        Press(host, seat, new MenuCommands { Pointer = new MenuPointer(fit.X(bar.X + 10f), fit.Y(bar.Y + 10f), false, false, bar.LastTop) });
        var scrolled = shell.Compose().Notes.FirstOrDefault();
        ctx.Check(scrolled?.Skip == bar.LastTop && GridWindow(shell, OriginalHangarScreen.DescriptionListKey).Top == bar.LastTop,
            $"a wheel over it takes the window to its last line ({scrolled?.Skip} of {bar.LastTop})");
        string later = scrolled != null && scrolled.Flow(Flowed).Count > 0 ? scrolled.Flow(Flowed)[0].Text : string.Empty;
        int reached = later.Length > 0 ? body.Entries[0].IndexOf(later, StringComparison.Ordinal) + later.Length : 0;
        ctx.Check(reached > shown.Length, $"drawing a stretch the unscrolled box could not reach ({reached} past {shown.Length})");
        Press(host, seat, new MenuCommands { Pointer = new MenuPointer(fit.X(bar.X + 10f), fit.Y(bar.Y + 10f), false, false, -bar.LastTop) });
        var engineTab = Row(shell, "PX_B_ENGINE")!;
        Click(host, seat, Pointer(fit, engineTab.X + 5f, engineTab.Y + 5f, pressed: true, clicked: true));
        Click(host, seat, Pointer(fit, armourTab.X + 5f, armourTab.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Compose().Notes.FirstOrDefault()?.Skip == 0,
            $"and a changed body puts the window back at its head ({shell.Compose().Notes.FirstOrDefault()?.Skip})");
    }

    // The renderer's font measurement stood in for at a fixed six pixels a character, since a suite
    // that composes a board draws no frame and so has no face to measure against.
    private static float Flowed(string text, float width) =>
        FlowedLine * (float)Math.Ceiling(text.Length * FlowedGlyph / Math.Max(1f, width));

    // The airframe swap's own question on the shipped layout: the open list's rows draw bare, a
    // swap over an unedited build raises nothing, and one over an edited build raises the
    // three-button messagebox whose Cancel puts the airframe back. Leaves the hub on the tab it
    // found, over the airframe's stock build, which is what the commit below saves.
    private static void OriginalDefaultsAsk(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, HangarFeature hangar)
    {
        var here = shell.Screen;
        int airframe = hangar.Scratch.Airframe;
        int other = airframe == 0 ? 1 : 0;
        hangar.ApplyDefaultConfiguration(true);
        var tab = Row(shell, "PX_B_AIRFRAME")!;
        Click(host, seat, Pointer(fit, tab.X + 5f, tab.Y + 5f, pressed: true, clicked: true));
        var box = Row(shell, OriginalHangarScreen.AirframeDropKey)!;
        Click(host, seat, Pointer(fit, box.X + 5f, box.Y + 5f, pressed: true, clicked: true));
        var rows = shell.Rows.Where(r => r.Kind == OriginalRowKind.ListRow).ToList();
        bool bare = rows.Count == HangarEconomy.Airframes.Length;
        for (int i = 0; bare && i < rows.Count; i++)
        {
            bare = rows[i].Label == hangar.AirframeName(i);
        }

        ctx.Check(bare, $"the open airframe list draws every row bare, nothing before the name ({rows.Count}, {rows.FirstOrDefault()?.Label})");
        SwapAirframe(host, seat, shell, fit, other);
        ctx.Check(hangar.Scratch.Airframe == other && shell.Dialog == null && hangar.DefaultsAsk == null,
            $"a swap over an unedited build takes the name screen's own answer and asks nothing ({hangar.Scratch.Airframe}, {hangar.DefaultsAsk})");

        hangar.SetArmour(0, CustomPlaneDef.MaxArmourUnits);
        SwapAirframe(host, seat, shell, fit, airframe);
        var ask = shell.Dialog;
        ctx.Check(ask != null && hangar.DefaultsAsk == airframe && ask.Icon == DialogIcon.Query
            && ask.Message == hangar.DefaultsAskText,
            $"and one over an edited build raises the query box on string 206 ({hangar.DefaultsAsk}, {ask?.Icon})");
        var answers = shell.Rows;
        ctx.Check(answers.Count == 3
            && answers[0].Label == hangar.Strings.Text(102, "Yes")
            && answers[1].Label == hangar.Strings.Text(103, "No")
            && answers[2].Label == hangar.Strings.Text(101, "Cancel")
            && answers[0].X < answers[1].X && answers[1].X < answers[2].X,
            $"with the script's three words across its left, centre and right buttons ({string.Join("/", answers.Select(r => r.Label))})");
        var cancel = answers[2];
        Click(host, seat, Pointer(fit, cancel.X + 5f, cancel.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Dialog == null && hangar.DefaultsAsk == null && hangar.Scratch.Airframe == other
            && hangar.ArmourUnits(0) == CustomPlaneDef.MaxArmourUnits,
            $"Cancel puts the airframe back and keeps the edit ({hangar.Scratch.Airframe}, {hangar.ArmourUnits(0)} presses)");

        SwapAirframe(host, seat, shell, fit, airframe);
        var yes = shell.Rows[0];
        Click(host, seat, Pointer(fit, yes.X + 5f, yes.Y + 5f, pressed: true, clicked: true));
        ctx.Check(hangar.DefaultsAsk == null && hangar.Scratch.Airframe == airframe && hangar.Scratch.Engine == 1,
            $"and Yes takes the picked airframe's stock build ({hangar.Scratch.Airframe}, engine {hangar.Scratch.Engine})");
        var leave = Row(shell, here == OriginalScreen.HangarArmor ? "PX_B_ARMOR" : "PX_B_AIRFRAME")!;
        Click(host, seat, Pointer(fit, leave.X + 5f, leave.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == here, $"and the hub is left on the tab this page found ({shell.Screen})");
    }

    // The airframe tab's swap through the presses a pilot has: the closed box opens its list and
    // the row is clicked out of it.
    private static void SwapAirframe(MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, int airframe)
    {
        if (shell.Hangar!.OpenHangarDropdown == null && Row(shell, OriginalHangarScreen.AirframeDropKey) is { } box)
        {
            Click(host, seat, Pointer(fit, box.X + 5f, box.Y + 5f, pressed: true, clicked: true));
        }

        if (Row(shell, OriginalHangarScreen.AirframeDropKey + ":" + airframe.ToString(CultureInfo.InvariantCulture)) is { } row)
        {
            Click(host, seat, Pointer(fit, row.X + 2f, row.Y + 2f, pressed: true, clicked: true));
        }
    }

    // The ARMOR tab's two wing boxes, which move together: the screen draws one per wing and a
    // pick on either writes both zone dwords, so the second box redraws with the first.
    private static void OriginalArmourWings(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, HangarFeature hangar)
    {
        var wing = Row(shell, "AR_D_POINT2");
        if (wing == null)
        {
            ctx.Check(false, $"the left wing box stands on the armor tab");
            return;
        }

        Click(host, seat, Pointer(fit, wing.X + 5f, wing.Y + 5f, pressed: true, clicked: true));
        var third = Row(shell, "AR_D_POINT2:3");
        ctx.Check(third != null, $"a click on the left wing box opens its 13-row list ({shell.Rows.Count})");
        if (third == null)
        {
            return;
        }

        Click(host, seat, Pointer(fit, third.X + 2f, third.Y + 2f, pressed: true, clicked: true));
        ctx.Check(hangar.Scratch.ArmourLeftWing == 3 && hangar.Scratch.ArmourRightWing == 3
            && Row(shell, "AR_D_POINT3")?.Label == Row(shell, "AR_D_POINT2")?.Label,
            $"picking 15 units on the left wing arms both wings and both boxes read it ({hangar.Scratch.ArmourLeftWing}/{hangar.Scratch.ArmourRightWing}, {Row(shell, "AR_D_POINT3")?.Label})");
        var other = Row(shell, "AR_D_POINT3")!;
        Click(host, seat, Pointer(fit, other.X + 5f, other.Y + 5f, pressed: true, clicked: true));
        var none = Row(shell, "AR_D_POINT3:0");
        if (none != null)
        {
            Click(host, seat, Pointer(fit, none.X + 2f, none.Y + 2f, pressed: true, clicked: true));
        }

        ctx.Check(hangar.Scratch.ArmourLeftWing == 0 && hangar.Scratch.ArmourRightWing == 0,
            $"and None on the right wing clears both, leaving the build as the tab found it ({hangar.Scratch.ArmourLeftWing}/{hangar.Scratch.ArmourRightWing})");
    }

    private static void OriginalPurchase(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, HangarFeature hangar, PlayerSetupFeature setup,
        CustomPlaneStore store, string scratch, MenuLayout layout)
    {
        var readyAt = Corner(layout.Screen("PlaneConstruction"), OriginalHangarScreen.ReadyKey);
        var purchaseAt = Corner(layout.Screen("Purchase"), OriginalHangarScreen.PurchaseNowKey);
        ctx.CheckShipped(readyAt == (400f, 564f) && purchaseAt == (510f, 465f),
            $"on the install READY stands at 400,564 and the commit at 510,465 ({readyAt}, {purchaseAt})");
        var ready = Row(shell, OriginalHangarScreen.ReadyKey);
        ctx.Check(ready is { Enabled: true } && (ready.X, ready.Y) == readyAt, $"READY TO PURCHASE stands at its authored place");
        if (ready == null)
        {
            return;
        }

        Click(host, seat, Pointer(fit, ready.X + 5f, ready.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.HangarPurchase && Row(shell, OriginalHangarScreen.PurchaseNowKey) is { Enabled: true } commit
                && (commit.X, commit.Y) == purchaseAt,
            $"it opens the totals page with the commit live at its authored place ({shell.Screen}, {Row(shell, OriginalHangarScreen.PurchaseNowKey)?.X}, {Row(shell, OriginalHangarScreen.PurchaseNowKey)?.Y}, {Row(shell, OriginalHangarScreen.PurchaseNowKey)?.Enabled} of {purchaseAt}, {hangar.Refusal() ?? "no refusal"}, {hangar.Bill.Total.Weight} of {hangar.Bill.Capacity} lbs.)");
        string exportWord = hangar.Strings.Text(1139, "Export");
        ctx.Check(Row(shell, OriginalHangarScreen.PurchaseNowKey)?.Label == exportWord,
            $"reading Export off the inventory's own string on the wallet-free door ({Row(shell, OriginalHangarScreen.PurchaseNowKey)?.Label})");
        var board = shell.Compose();
        ctx.Check(board.Lines.Any(l => l.Text == "$" + hangar.Bill.Total.Cost) && board.Lines.Any(l => l.Text == hangar.AirframeName(hangar.Scratch.Airframe)),
            $"the page lists the airframe and the total cost");
        ctx.Check(board.Plaques.Any(p => p.Label == exportWord && p.LabelBaseline == 0f),
            $"and the paper commit keeps the centred label every plaque but a tab takes");
        Press(host, seat, Back);
        ctx.Check(shell.Screen == OriginalScreen.HangarArmor, $"Back returns to the tab the hub last showed ({shell.Screen})");
        Click(host, seat, Pointer(fit, ready.X + 5f, ready.Y + 5f, pressed: true, clicked: true));
        var purchase = Row(shell, OriginalHangarScreen.PurchaseNowKey)!;
        ctx.Check(store.Load(scratch) == null, $"nothing is in the store before the press");
        Click(host, seat, Pointer(fit, purchase.X + 5f, purchase.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.InstantAction && shell.Hangar!.LastBuiltPlane == scratch && !hangar.IsOpen,
            $"Purchase Now saves, drops the build and returns to the Instant Action screen ({shell.Screen}, {shell.Hangar!.LastBuiltPlane}, {shell.Dialog?.Message ?? "no dialog"}, {hangar.Message})");
        ctx.Check(shell.FocusedKey == OriginalInstantActionScreen.BuildKey, $"with the focus back on Build Custom Plane ({shell.FocusedKey})");
        int built = host.Features.Get<InstantActionFeature>().PlayerPlaneIndex;
        ctx.Check(store.Load(scratch) is { Engine: 1 } saved && saved.Airframe == built, $"the store holds the plane as built ({built})");
        ctx.Check(setup.Roster is var roster && Contains(roster, scratch), $"and the shared roster lists it without a return to the top level");
        ctx.Check(Contains(shell.InstantAction.PilotRoster, scratch), $"and the Pilot Plane list offers it, re-read on the way back");
    }

    private static void OriginalInventory(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, HangarFeature hangar, PlayerSetupFeature setup, CustomPlaneStore store, string scratch, MenuLayout layout)
    {
        var door = BuildDoor(host, seat, shell, fit)!;
        Click(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
        Press(host, seat, new MenuCommands { Typed = "Other" });
        var ok = Row(shell, OriginalHangarScreen.NameOkKey)!;
        Click(host, seat, Pointer(fit, ok.X + 5f, ok.Y + 5f, pressed: true, clicked: true));
        var sellAt = Corner(layout.Screen("PlaneConstruction"), OriginalHangarScreen.SellPlanesKey);
        ctx.CheckShipped(sellAt == (100f, 564f), $"on the install SELL PLANES stands at 100,564 ({sellAt})");
        var sell = Row(shell, OriginalHangarScreen.SellPlanesKey);
        ctx.Check(sell is { Enabled: true } && (sell.X, sell.Y) == sellAt, $"SELL PLANES stands at its authored place");
        if (sell == null)
        {
            return;
        }

        Click(host, seat, Pointer(fit, sell.X + 5f, sell.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.HangarInventory && Row(shell, OriginalHangarScreen.InventoryPlanesKey) is { X: 138f, Y: 132f, Width: 271f },
            $"it opens the inventory with the plane dropdown at its authored box ({shell.Screen})");
        ctx.Check(Row(shell, OriginalHangarScreen.InventoryExportKey) == null && Row(shell, OriginalHangarScreen.InventorySellKey)?.Label == "Delete",
            $"the wallet-free page builds no Export row and calls the removal Delete ({Row(shell, OriginalHangarScreen.InventorySellKey)?.Label})");
        var inventory = shell.Compose();
        ctx.Check(inventory.Lines.Any(l => l.Text == "Delete a Plane")
            && !inventory.Lines.Any(l => l.Text == hangar.Strings.Text(1256, "Sell or Export a Plane")),
            $"and its prompt names the one verb it offers rather than the campaign's two");
        int index = IndexOf(hangar.Saved, scratch);
        ctx.Check(index >= 0, $"the inventory lists the scratch plane ({hangar.Saved.Count} saved)");
        var planes = Row(shell, OriginalHangarScreen.InventoryPlanesKey)!;
        Click(host, seat, Pointer(fit, planes.X + 5f, planes.Y + 5f, pressed: true, clicked: true));
        var item = Row(shell, OriginalHangarScreen.InventoryPlanesKey + ":" + index);
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
            Click(host, seat, Pointer(fit, item.X + 5f, item.Y + 5f, pressed: true, clicked: true));
        }

        ctx.Check(shell.Hangar!.InventoryIndex == index && Row(shell, OriginalHangarScreen.InventoryPlanesKey)?.Label == scratch,
            $"picking it puts it in the box ({Row(shell, OriginalHangarScreen.InventoryPlanesKey)?.Label})");
        var picked = shell.Compose();
        ctx.Check(InventoryFigure(layout, picked, "HA_T_AGILITYP") != null && InventoryFigure(layout, picked, "HA_T_VALUEP") == null,
            $"whose figures stand without the Value row, this door pricing nothing ({InventoryFigure(layout, picked, "HA_T_VALUEP")?.Text})");
        var named = InventoryFigure(layout, picked, "HA_T_PLANE", OriginalHangarScreen.InventoryPlaneRow);
        ctx.Check(named != null && named.Text.StartsWith(scratch, StringComparison.Ordinal)
            && InventoryFigure(layout, picked, "HA_T_PILOTPLANE") == null,
            $"and the plane line on the row HANGAR.SCRIPT binds, the wide one unused ({named?.Text})");
        ctx.Check(named != null && named.Y + named.Size < OriginalHangarScreen.InventoryPlaneBoxBottom
            && InventoryFigure(layout, picked, "HA_T_PLANE") == null,
            $"centred in its dashed box, off the authored row whose baseline lands on the box's bottom rule ({named?.Y}, {named?.Size})");
        var sellButton = Row(shell, OriginalHangarScreen.InventorySellKey)!;
        Click(host, seat, Pointer(fit, sellButton.X + 5f, sellButton.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Dialog != null && shell.FocusedKey == OriginalShell.DialogYesKey && store.Load(scratch) != null,
            $"Delete asks first with the two-answer messagebox opening on Yes ({shell.Dialog?.Message})");
        ctx.Check(shell.Dialog is { Icon: DialogIcon.Query, Answers.Count: 2 } ask
            && ask.Message.Contains("delete it?", StringComparison.Ordinal) && !ask.Message.Contains('$'),
            $"asking the delete question in the query box, with no price on a plane that cost nothing ({shell.Dialog?.Message})");
        var yes = Row(shell, OriginalShell.DialogYesKey);
        ctx.Check(yes != null, $"whose Yes stands at the messagebox's left row");
        if (yes == null)
        {
            return;
        }

        Click(host, seat, Pointer(fit, yes.X + 5f, yes.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Dialog == null && store.Load(scratch) == null && IndexOf(hangar.Saved, scratch) < 0,
            $"Yes removes the plane from the store and the roster");
        ctx.Check(!Contains(setup.Roster, scratch), $"and from the shared aircraft roster");
        ctx.Check(hangar.Scratch.Name == "Other" && hangar.IsOpen, $"the open build is untouched ({hangar.Scratch.Name})");
        var done = Row(shell, OriginalHangarScreen.InventoryDoneKey)!;
        Click(host, seat, Pointer(fit, done.X + 5f, done.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.HangarAirframe, $"Done returns to the tab ({shell.Screen})");
        var cancel = Row(shell, OriginalHangarScreen.CancelBuildKey)!;
        Click(host, seat, Pointer(fit, cancel.X + 5f, cancel.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.InstantAction && !hangar.IsOpen && store.Load("Other") == null,
            $"CANCEL drops the build, returns to the Instant Action screen and leaves no residue ({shell.Screen})");
        ctx.Check(!Contains(shell.InstantAction.PilotRoster, scratch), $"whose Pilot Plane list no longer offers the sold plane");
    }

    // The cabin's PLANE CONSTRUCTION over the aid profile's wallet: the cash note on a tab and on
    // the totals page, an over-priced engine row marked in the open list and still picked.
    private static void OriginalWallet(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, HangarFeature hangar, MenuLayout layout)
    {
        shell.Campaign.OpenCampaignOver(CampaignAidProfiles.Store(seeded: true, progressed: true));
        ctx.Check(
            shell.Campaign.ShowCabin(CampaignAidProfiles.Pilot),
            $"the aid profile seats on the cabin ({shell.Screen})");
        var door = Row(shell, "PlaneConstruction");
        if (door == null)
        {
            ctx.Check(false, $"the cabin carries PLANE CONSTRUCTION");
            return;
        }

        Click(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
        Press(host, seat, new MenuCommands { Typed = "Wallet" });
        var ok = Row(shell, OriginalHangarScreen.NameOkKey)!;
        Click(host, seat, Pointer(fit, ok.X + 5f, ok.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.HangarAirframe && hangar.Wallet != null, $"OK opens the hub over the wallet ({shell.Screen})");
        if (hangar.Wallet is not { } wallet)
        {
            return;
        }

        string title = layout.Screen("PlaneConstruction")?.Widget("PX_T_CASHTITLE")?.Text ?? "(no cash title)";
        string figure = "$" + wallet.Funds.ToString(CultureInfo.InvariantCulture);
        var board = shell.Compose();
        ctx.Check(board.Lines.Any(l => l.Text == title) && board.Lines.Any(l => l.Text == figure),
            $"the airframe tab draws the cash note with the profile's funds ({figure})");
        // The rows stay bare whatever the funds: the decoded screen reports them at the purchase
        // alone and reddens the cost figure meanwhile (docs/org/hangar.md).
        ctx.Check(wallet.Funds < hangar.Bill.Total.Cost
            && Row(shell, OriginalHangarScreen.AirframeDropKey)?.Label == hangar.AirframeName(hangar.Scratch.Airframe),
            $"the airframe box reads bare over a build the wallet cannot cover ({Row(shell, OriginalHangarScreen.AirframeDropKey)?.Label})");
        // The script's own two arms: the cost line red past the wallet, the weight line in the
        // page's ink while the build is inside its capacity.
        ctx.Check(HubFigure(layout, board, "PX_T_PLANECOST")?.Ink == BoardInk.Alarm
            && HubFigure(layout, board, "PX_T_CURRENTWEIGHT")?.Ink == BoardInk.Dialog,
            $"and the cost line is red where the weight line is not ({HubFigure(layout, board, "PX_T_PLANECOST")?.Ink}, {HubFigure(layout, board, "PX_T_CURRENTWEIGHT")?.Ink})");
        var engineTab = Row(shell, "PX_B_ENGINE")!;
        Click(host, seat, Pointer(fit, engineTab.X + 5f, engineTab.Y + 5f, pressed: true, clicked: true));
        board = shell.Compose();
        ctx.Check(shell.Screen == OriginalScreen.HangarEngine && board.Lines.Any(l => l.Text == title) && board.Lines.Any(l => l.Text == figure),
            $"the engine tab draws the same note ({shell.Screen})");
        // The click leaves the focus on the tab bar; the keyboard walks it onto the page's one dropdown.
        OriginalWalk.WalkTo(shell, OriginalHangarScreen.EngineDropKey, frame => Press(host, seat, frame));

        int engine = hangar.Scratch.Engine;
        Press(host, seat, Right);
        ctx.Check(hangar.Scratch.Engine == engine + 1 && shell.FocusedKey == OriginalHangarScreen.EngineDropKey,
            $"Right steps the engine on the focused box ({engine} -> {hangar.Scratch.Engine}, {shell.FocusedKey})");
        Press(host, seat, Accept);
        var cheapest = Row(shell, OriginalHangarScreen.EngineDropKey + ":0");
        ctx.Check(shell.Hangar!.OpenHangarDropdown == OriginalHangarScreen.EngineDropKey && cheapest != null
            && cheapest.Label == hangar.EngineName(hangar.Scratch.Airframe, 0),
            $"the open list's rows are bare, over-priced or not ({shell.Hangar!.OpenHangarDropdown}, {shell.FocusedKey}, {cheapest?.Label})");
        Press(host, seat, Up);
        // The hub prices the row under the cursor as though it were taken, which is what the
        // description box and the blueprint already preview, and takes nothing.
        var previewed = hangar.BillWithEngine(engine);
        board = shell.Compose();
        ctx.CheckShipped(HubFigure(layout, board, "PX_T_PLANECOST")?.Text.Contains("$" + previewed.Total.Cost.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) == true,
            $"on the install the cost line writes its figure in dollars ({HubFigure(layout, board, "PX_T_PLANECOST")?.Text})");
        ctx.Check(hangar.Scratch.Engine != engine
            && HubFigure(layout, board, "PX_T_PLANECOST")?.Text.Contains(previewed.Total.Cost.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) == true
            && HubFigure(layout, board, "PX_T_CURRENTWEIGHT")?.Text.Contains(previewed.Total.Weight.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) == true,
            $"the focused row's build is what the two figures read while the list stands ({HubFigure(layout, board, "PX_T_PLANECOST")?.Text}, {HubFigure(layout, board, "PX_T_CURRENTWEIGHT")?.Text})");
        Press(host, seat, Accept);
        ctx.Check(shell.Hangar!.OpenHangarDropdown == null && hangar.Scratch.Engine == engine,
            $"a row past the funds still takes the pick ({hangar.Scratch.Engine}, {shell.FocusedKey})");
        board = shell.Compose();
        ctx.Check(HubFigure(layout, board, "PX_T_PLANECOST")?.Text.Contains(hangar.Bill.Total.Cost.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) == true,
            $"and the closed box leaves the figures on the build that stands ({HubFigure(layout, board, "PX_T_PLANECOST")?.Text})");
        var ready = Row(shell, OriginalHangarScreen.ReadyKey)!;
        Click(host, seat, Pointer(fit, ready.X + 5f, ready.Y + 5f, pressed: true, clicked: true));
        board = shell.Compose();
        ctx.Check(shell.Screen == OriginalScreen.HangarPurchase && board.Lines.Any(l => l.Text == figure)
            && board.Lines.Any(l => l.Text.Contains(hangar.Strings.Text(1226, "INSUFFICIENT FUNDS"), StringComparison.Ordinal)),
            $"the totals page keeps the note and names the shortfall in the original's words ({shell.Screen})");
        Press(host, seat, Back);
        ctx.Check(shell.Screen == OriginalScreen.HangarEngine, $"Back returns to the tab the hub last showed ({shell.Screen})");
        OriginalOverweight(ctx, host, seat, shell, fit, hangar, layout);
        WalletInventory(ctx, host, seat, shell, fit, hangar, layout);
        Press(host, seat, Back);
        ctx.Check(shell.Screen == OriginalScreen.CampaignCabin && !hangar.IsOpen, $"Back then cancels the build and resumes the cabin ({shell.Screen})");
        var leave = Row(shell, "ReturnToMainMenu")!;
        Click(host, seat, Pointer(fit, leave.X + 5f, leave.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.TopLevel, $"RETURN TO MAIN MENU leaves for the top level ({shell.Screen})");
    }

    // The weight line's own arm, over the picks that reach it: the lightest airframe under every
    // armour press and every hardpoint is past its capacity, so the line takes the red literal,
    // and the ink goes back with the presses. The build is dropped with the rest at the cancel.
    private static void OriginalOverweight(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, HangarFeature hangar, MenuLayout layout)
    {
        hangar.PickAirframe(0);
        hangar.AnswerDefaultsAsk(true);
        for (int zone = 0; zone < 4; zone++)
        {
            hangar.SetArmour(zone, CustomPlaneDef.MaxArmourUnits);
        }

        hangar.SetHardpoints(0, CustomPlaneDef.MaxHardpointsPerWing);
        hangar.SetHardpoints(1, CustomPlaneDef.MaxHardpointsPerWing);
        var board = shell.Compose();
        ctx.Check(hangar.Bill.Verdict == PurchaseVerdict.Overweight && HubFigure(layout, board, "PX_T_CURRENTWEIGHT")?.Ink == BoardInk.Alarm,
            $"the weight line is red over capacity ({hangar.Bill.Total.Weight} of {hangar.Bill.Capacity} lbs., {HubFigure(layout, board, "PX_T_CURRENTWEIGHT")?.Ink})");
        OriginalPendingWeight(ctx, host, seat, shell, fit, hangar, layout);
        for (int zone = 0; zone < 4; zone++)
        {
            hangar.SetArmour(zone, 0);
        }

        hangar.SetHardpoints(0, 0);
        hangar.SetHardpoints(1, 0);
        board = shell.Compose();
        ctx.Check(hangar.Bill.Verdict != PurchaseVerdict.Overweight && HubFigure(layout, board, "PX_T_CURRENTWEIGHT")?.Ink == BoardInk.Dialog,
            $"and back in the page's ink under it ({hangar.Bill.Total.Weight} of {hangar.Bill.Capacity} lbs., {HubFigure(layout, board, "PX_T_CURRENTWEIGHT")?.Ink})");
    }

    // The airframe row's own answer, which is not the answer every other row gives. The decoded
    // set-airframe swap writes the pending weight line and reports the build inside its capacity
    // whatever it weighs, so the same overweight build reads the shipped Pending word in the
    // page's ink under an open airframe list, and its own figure in the red under an open
    // hardpoint list. Leaves the hub on the engine tab it found.
    private static void OriginalPendingWeight(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, HangarFeature hangar, MenuLayout layout)
    {
        string word = hangar.Strings.Text(PendingWeightString, "CURRENT WEIGHT: Pending").Split('\n')[^1].Trim();
        var airframeTab = Row(shell, "PX_B_AIRFRAME")!;
        Click(host, seat, Pointer(fit, airframeTab.X + 5f, airframeTab.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Hangar!.OpenHangarDropdownOn(OriginalHangarScreen.AirframeDropKey) && shell.Screen == OriginalScreen.HangarAirframe,
            $"the airframe list opens over the overweight build ({shell.Screen}, {shell.Hangar!.OpenHangarDropdown})");
        var board = shell.Compose();
        var weight = HubFigure(layout, board, "PX_T_CURRENTWEIGHT");
        ctx.Check(word.Length > 0 && weight?.Text.EndsWith(word, StringComparison.Ordinal) == true
            && !weight.Text.Any(char.IsDigit) && weight.Ink == BoardInk.Dialog,
            $"the previewed airframe row weighs against nothing and never reddens ({weight?.Text}, {weight?.Ink})");
        ctx.Check(HubFigure(layout, board, "PX_T_PLANECOST")?.Text.Contains(
            hangar.BillWithAirframe(0).Total.Cost.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) == true,
            $"while the cost line beside it still prices the row ({HubFigure(layout, board, "PX_T_PLANECOST")?.Text})");
        Press(host, seat, Back);

        var hardpointTab = Row(shell, "PX_B_HARDPOINTS")!;
        Click(host, seat, Pointer(fit, hardpointTab.X + 5f, hardpointTab.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Hangar!.OpenHangarDropdownOn("HP_D_POINT0"), $"a hardpoint list opens over the same build ({shell.Hangar!.OpenHangarDropdown})");
        board = shell.Compose();
        weight = HubFigure(layout, board, "PX_T_CURRENTWEIGHT");
        var previewed = hangar.BillWithHardpoints(0, CustomPlaneDef.MaxHardpointsPerWing);
        ctx.Check(weight?.Text.Contains(previewed.Total.Weight.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) == true
            && weight.Ink == BoardInk.Alarm,
            $"where a hardpoint row keeps the comparison and the red ({weight?.Text}, {weight?.Ink})");
        Press(host, seat, Back);
        var engineTab = Row(shell, "PX_B_ENGINE")!;
        Click(host, seat, Pointer(fit, engineTab.X + 5f, engineTab.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.HangarEngine && shell.Hangar!.OpenHangarDropdown == null,
            $"and the hub is left on the tab this page found ({shell.Screen}, {shell.Hangar!.OpenHangarDropdown})");
    }

    // A hub figure by the authored box it stands in, which is what tells the cost line from the
    // cash note without this suite restating the shipped words.
    private static BoardLine? HubFigure(MenuLayout layout, ComposedBoard board, string key)
        => Figure(layout, OriginalHangarScreen.PlaneConstructionSection, board, key);

    // The same reading on the inventory page, where a row's absence is the claim being made and
    // matching English text would only find the row that was not drawn.
    private static BoardLine? InventoryFigure(MenuLayout layout, ComposedBoard board, string key, float? row = null)
        => Figure(layout, OriginalHangarScreen.InventorySection, board, key, row);

    // A row given its own line is looked for there. The authored Y stands only for rows the
    // composer leaves where the layout puts them.
    private static BoardLine? Figure(MenuLayout layout, string section, ComposedBoard board, string key, float? row = null)
    {
        if (layout.Screen(section)?.Widget(key) is not { } widget)
        {
            return null;
        }

        float x = widget.Int("X");
        float y = row ?? widget.Int("Y");
        return board.Lines.FirstOrDefault(l => l.X == x && l.Y == y);
    }

    // The wallet's own inventory: both shipped verbs on their buttons, the shipped prompt naming
    // both, and the sale question with the plane's value on it. Answered No, since the profile's
    // aircraft is not this suite's to remove.
    private static void WalletInventory(
        TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, HangarFeature hangar, MenuLayout layout)
    {
        var sell = Row(shell, OriginalHangarScreen.SellPlanesKey)!;
        Click(host, seat, Pointer(fit, sell.X + 5f, sell.Y + 5f, pressed: true, clicked: true));
        var board = shell.Compose();
        var inventory = layout.Screen(OriginalHangarScreen.InventorySection);
        string sellWord = inventory?.Widget(OriginalHangarScreen.InventorySellKey)?.Text ?? "(no sell word)";
        string prompt = inventory?.Widget("HA_T_PROMPT")?.Text ?? "(no prompt)";
        ctx.CheckShipped(sellWord == hangar.Strings.Text(1003, "Sell") && prompt == hangar.Strings.Text(1256, "Sell or Export a Plane"),
            $"on the install the Sell button and the prompt are langui 1003 and 1256 ({sellWord}, {prompt})");
        ctx.Check(shell.Screen == OriginalScreen.HangarInventory && hangar.Saved.Count > 0
            && Row(shell, OriginalHangarScreen.InventoryExportKey) is { Enabled: true }
            && Row(shell, OriginalHangarScreen.InventorySellKey)?.Label == sellWord,
            $"the wallet's inventory keeps Export live beside the shipped Sell ({shell.Screen}, {hangar.Saved.Count} owned)");
        ctx.Check(board.Lines.Any(l => l.Text == prompt),
            $"and the shipped prompt naming both verbs");
        ctx.Check(InventoryFigure(layout, board, "HA_T_VALUEP") is { } worth && worth.Text.Contains('$'),
            $"and the Value row the wallet can price ({InventoryFigure(layout, board, "HA_T_VALUEP")?.Text})");
        var sellButton = Row(shell, OriginalHangarScreen.InventorySellKey)!;
        Click(host, seat, Pointer(fit, sellButton.X + 5f, sellButton.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Dialog is { Icon: DialogIcon.Query, Answers.Count: 2 } ask && ask.Message.Contains("sell it?", StringComparison.Ordinal)
            && ask.Message.Contains('$'), $"whose Sell asks the priced sale question ({shell.Dialog?.Message})");
        var no = Row(shell, OriginalShell.DialogNoKey)!;
        Click(host, seat, Pointer(fit, no.X + 5f, no.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Dialog == null && hangar.Saved.Count > 0, $"and No keeps the plane ({hangar.Saved.Count} owned)");
        var done = Row(shell, OriginalHangarScreen.InventoryDoneKey)!;
        Click(host, seat, Pointer(fit, done.X + 5f, done.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.HangarEngine, $"Done returns to the tab ({shell.Screen})");
    }

    private static void OriginalSwitch(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, HangarFeature hangar, CustomPlaneStore store)
    {
        var door = BuildDoor(host, seat, shell, fit)!;
        Click(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
        Press(host, seat, new MenuCommands { Typed = "Dropped" });
        var ok = Row(shell, OriginalHangarScreen.NameOkKey)!;
        Click(host, seat, Pointer(fit, ok.X + 5f, ok.Y + 5f, pressed: true, clicked: true));
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

    // A widget's authored corner, one of its numeric fields, and its first art name. Each is read
    // off the layout so a check holds over the invented tree too.
    private static (float X, float Y) Corner(MenuLayoutScreen? section, string key) =>
        section?.Widget(key) is { } widget ? (widget.Int("X"), widget.Int("Y")) : (-1f, -1f);

    private static float Field(MenuLayoutScreen? section, string key, string field) =>
        section?.Widget(key)?.Int(field) ?? -1f;

    private static string ArtOf(MenuLayoutScreen? section, string key) =>
        section?.Widget(key)?.Art.FirstOrDefault() ?? string.Empty;

    // A picture's size as the shell measures it, zero when it has none.
    private static (float Width, float Height) Measure(OriginalShell shell, string art) =>
        ((IOriginalScreenHost)shell).Measure(art) is { } size ? (size.Width, size.Height) : (0f, 0f);

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

    // The open list's window as the pointer sees it, or a zeroed one where no list is open, which
    // the checks read rather than branching on a missing list.
    private static ListWindow GridWindow(OriginalShell shell, string key)
    {
        foreach (var list in shell.Lists)
        {
            if (list.Key == key)
            {
                return list.Window;
            }
        }

        return default;
    }

    private static MenuCommands Pointer(BoardFit fit, float authoredX, float authoredY, bool pressed = false, bool clicked = false) =>
        new() { Pointer = new MenuPointer(fit.X(authoredX), fit.Y(authoredY), pressed, clicked) };

    // One click as the Original shell reads it: the press arms the row it lands on and the
    // release still on that row is what fires, so a click is two frames rather than one.
    private static void Click(MenuHost host, ScriptedSeat seat, MenuCommands frame)
    {
        Press(host, seat, frame);
        Press(host, seat, frame with { Pointer = frame.Pointer!.Value with { Pressed = false, Clicked = false } });
    }

    private static void Press(MenuHost host, ScriptedSeat seat, MenuCommands frame)
    {
        seat.Enqueue(frame);
        host.Tick(1f / 60f);
    }

    // The way into the wallet-free hangar: the Instant Action screen's Build Custom Plane, the
    // screen entered through the top level's Instant Action row when it is not already showing.
    private static OriginalRow? BuildDoor(MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit)
    {
        if (shell.Screen == OriginalScreen.TopLevel && Row(shell, "MM_B_INSTANTACTION") is { } instantAction)
        {
            Click(host, seat, Pointer(fit, instantAction.X + 5f, instantAction.Y + 5f, pressed: true, clicked: true));
        }

        return Row(shell, OriginalInstantActionScreen.BuildKey);
    }

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
