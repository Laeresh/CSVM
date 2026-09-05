using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.UI;
using CSVM.UI.Menu;
using CSVM.UI.Menu.BuiltIn;
using CSVM.UI.Menu.Original;

namespace CSVM.Testing;

/// <summary>
/// Instant Action in both presentations. Built-in's journey, characterized: a real
/// <see cref="LaunchMenu"/> is driven from the Mode screen through the five-step wizard
/// (Environment, Mission type with lives, Waves with the per-wave editor, Wingmen with the wingman
/// loadout, Aircraft) to the typed exit, backed out of at every step, opened through the Table of
/// Contents, re-entered the way a return from flight re-enters it, and opened through the aids the
/// screens carry; every check pins what the screens do today, quirks included. Then Original's
/// decoded screen through a real <see cref="MenuHost"/> over the install's layout, driving the
/// same shared feature to four representative launches.
/// </summary>
internal static class MenuInstantActionSuites
{
    private static readonly MenuCommands Accept = new() { Accept = true };
    private static readonly MenuCommands Back = new() { Back = true };
    private static readonly MenuCommands Loadout = new() { Loadout = true };
    private static readonly MenuCommands Contents = new() { Contents = true };
    private static readonly MenuCommands Down = new() { MoveY = 1 };
    private static readonly MenuCommands Up = new() { MoveY = -1 };
    private static readonly MenuCommands Left = new() { MoveX = -1 };
    private static readonly MenuCommands Right = new() { MoveX = 1 };

    [Suite("menu-instant-action-journey",
        "Built-in's Instant Action journey pinned end to end: a real LaunchMenu is driven Mode to "
        + "Environment, the Table of Contents applies a preset, Mission type steps the lives, the "
        + "ace duel skips Waves and Wingmen both ways, the wave editor edits a slot live and a new "
        + "militia resets its aircraft, Wingmen hides its aircraft row at zero and opens the "
        + "wingman loadout, the launch leaves as a LaunchExit carrying the built InstantActionDef, "
        + "the fields survive a return from flight, and the --menu= and --debug-* aids open their "
        + "states; every check is what the screens do today")]
    internal static void MenuInstantActionJourney(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var exits = new List<MenuExit>();
        var host = MenuSuiteHost.Bare(exits, ctx.DataRoot, out var seat);
        var menu = LaunchMenu.Build(ctx.ZrdrPath, ctx.DataRoot, host, seat.Input);
        ctx.Host.AddChild(menu);
        var launches = new Launches(exits);
        try
        {
            EnvironmentScreen(ctx, menu);
            TableOfContents(ctx, menu);
            MissionTypeScreen(ctx, menu);
            AceSkip(ctx, menu);
            WavesScreen(ctx, menu);
            WingmenScreen(ctx, menu);
            BackPaths(ctx, menu);
            Launch(ctx, menu, launches);
            Return(ctx, menu, launches);
            Aids(ctx, menu, launches);
        }
        finally
        {
            ctx.Host.RemoveChild(menu);
            menu.QueueFree();
        }
    }

    [Suite("menu-original-instant-action",
        "Original Instant Action through the presentation boundary over the install's decoded "
        + "layout: the top level's Instant Action row is live and a click opens the decoded screen "
        + "with the environment's def loaded, its rows are the layout's contents window, dropdowns "
        + "and buttons, keyboard frames cross to a dropdown and step and pick its value, four "
        + "representative presets (an ace duel, a squadron, a stunt run and a zeppelin run) each "
        + "leave through Fly Mission as one LaunchExit whose def derives the matching session spec, "
        + "the setup surviving each return to the top level, a build saved to the user's store "
        + "is offered in the Pilot Plane list after the stock rows under its own name, flies its "
        + "airframe's stock node with the def riding the seat, survives a return, and leaves the "
        + "list with its file (the wingman list stays stock), Weapon Loadout with the radio on "
        + "Wingman opens the decoded ammo chrome over the wingmen's shared fit whose CANCEL restores "
        + "and ACCEPT keeps a stepped pick, and Build Custom Plane opens the wallet-free hangar whose "
        + "Back and Purchase Now both return to the screen, the purchase's plane in the Pilot Plane list")]
    internal static void MenuOriginalInstantAction(TestContext ctx)
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
        string built = ScratchName();
        ctx.Check(store.Load(scratch) == null && store.Load(built) == null, $"the scratch names {scratch} and {built} are free in the user's store before the run");
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
            var ia = host.Features.Get<InstantActionFeature>();
            OriginalScreenOpens(ctx, host, seat, shell, fit, ia);
            OriginalKeyboard(ctx, host, seat, shell, ia);
            OriginalLaunches(ctx, host, seat, shell, fit, ia, exits);
            OriginalCustomPilot(ctx, host, seat, shell, fit, ia, exits, store, scratch);
            OriginalLoadout(ctx, host, seat, shell, fit, ia);
            OriginalBuild(ctx, host, seat, shell, fit, host.Features.Get<HangarFeature>(), store, built);
        }
        finally
        {
            store.Delete(scratch);
            store.Delete(built);
            host.Deactivate();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
        }

        ctx.Check(store.Load(scratch) == null && store.Load(built) == null, $"the scratch planes are gone from the user's store after the run");
    }

    // A name no player would type, distinct per run, inside the name screen's own character set.
    private static string ScratchName() => "Scratch " + Guid.NewGuid().ToString("N")[..8];

    private static void EnvironmentScreen(TestContext ctx, LaunchMenu menu)
    {
        menu.ShowMenu();
        menu.Drive(Down);
        Is(ctx, "the Mode screen's second row", "Instant Action", menu.ShownRowText);
        Is(ctx, "its description", "Pick an environment and a mission — ace, squadron, stunt or zeppelin.", menu.ShownDetail);
        menu.Drive(Accept);
        Is(ctx, "Accept on Instant Action opens the Environment screen", "Environment", menu.ShownScreen);
        Is(ctx, "its heading", "SELECT ENVIRONMENT", menu.ShownHeading);
        Is(ctx, "its breadcrumb", "Instant Action  ›  Environment  ›  Mission  ›  Aircraft", menu.ShownBreadcrumb);
        ctx.Check(menu.ShownRowCount == 7, $"seven environments ({menu.ShownRowCount})");
        Is(ctx, "the first row is the decoded dropdown's first", "an airfield", menu.ShownRowText);
        Is(ctx, "its description is the region code", "Region C1", menu.ShownDetail);
        Has(ctx, "the footer names the contents list", "P / X  Scenarios", menu.ShownFooter);
        Has(ctx, "and Back", "Esc / B  Back", menu.ShownFooter);

        menu.Drive(Up);
        ctx.Check(menu.ShownRow == 6 && menu.ShownRowText == "a movie studio",
            $"the cursor wraps to the last environment ({menu.ShownRow}, {menu.ShownRowText})");
        menu.Drive(Down);
        menu.Drive(Down);
        menu.Drive(Down);
        Is(ctx, "two rows down from the top", "Hawaii", menu.ShownRowText);
        Is(ctx, "whose region is C3", "Region C3", menu.ShownDetail);

        menu.Drive(Back);
        ctx.Check(menu.ShownScreen == "Mode" && menu.ShownRowText == "Instant Action",
            $"Back returns to the Mode screen on Instant Action ({menu.ShownScreen}, {menu.ShownRowText})");
        menu.Drive(Accept);
        ctx.Check(menu.ShownScreen == "Environment" && menu.ShownRowText == "Hawaii",
            $"and the environment cursor survives the trip out and back ({menu.ShownRowText})");
    }

    private static void TableOfContents(TestContext ctx, LaunchMenu menu)
    {
        menu.Drive(Contents);
        Is(ctx, "Contents on the Environment screen opens the Table of Contents", "Presets", menu.ShownScreen);
        Is(ctx, "its heading counts the window's position", "TABLE OF CONTENTS  (1/19)", menu.ShownHeading);
        Is(ctx, "its breadcrumb", "Instant Action  ›  Table of Contents", menu.ShownBreadcrumb);
        ctx.Check(menu.ShownRowCount == 19, $"nineteen presets ({menu.ShownRowCount})");
        Is(ctx, "the first preset", "Girl Trouble", menu.ShownRowText);
        Is(ctx, "its description", "Dogfighting a Squadron over Sky Haven   ·   Firebrand, 2 wingmen   ·   6 enemies", menu.ShownDetail);
        menu.Drive(Up);
        ctx.Check(menu.ShownRow == 18 && menu.ShownRowText == "The Hollywood Brawl" && menu.ShownHeading == "TABLE OF CONTENTS  (19/19)",
            $"Up wraps onto the last preset and the heading follows ({menu.ShownRowText}, {menu.ShownHeading})");
        menu.Drive(Down);
        Is(ctx, "Down wraps back to the first", "Girl Trouble", menu.ShownRowText);

        menu.Drive(Back);
        ctx.Check(menu.ShownScreen == "Environment" && menu.ShownRowText == "Hawaii" && !menu.ShownBreadcrumb.Contains("Girl Trouble", StringComparison.Ordinal),
            $"Back leaves the list without applying anything ({menu.ShownScreen}, {menu.ShownRowText}, {menu.ShownBreadcrumb})");
        menu.Drive(Contents);
        menu.Drive(Accept);
        ctx.Check(menu.ShownScreen == "Environment" && menu.ShownRowText == "Sky Haven",
            $"Accept applies the preset and lands on Environment with its pick ({menu.ShownScreen}, {menu.ShownRowText})");
        Is(ctx, "the preset's name rides the breadcrumb", "Instant Action  ›  Girl Trouble  ›  Environment  ›  Mission  ›  Aircraft", menu.ShownBreadcrumb);
        menu.Drive(Down);
        menu.Drive(Contents);
        ctx.Check(menu.ShownScreen == "Presets" && menu.ShownRow == 0,
            $"reopening the list lands on the applied preset ({menu.ShownScreen}, row {menu.ShownRow})");
        menu.Drive(Back);
        menu.Drive(Up);
        Is(ctx, "the environment cursor is back on the preset's pick", "Sky Haven", menu.ShownRowText);
    }

    private static void MissionTypeScreen(TestContext ctx, LaunchMenu menu)
    {
        menu.Drive(Accept);
        Is(ctx, "Accept on an environment opens the Mission screen", "MissionType", menu.ShownScreen);
        Is(ctx, "its heading", "SELECT MISSION", menu.ShownHeading);
        Is(ctx, "its breadcrumb names the environment", "Instant Action  ›  Girl Trouble  ›  Sky Haven  ›  Mission  ›  Aircraft", menu.ShownBreadcrumb);
        ctx.Check(menu.ShownRowCount == 4, $"Sky Haven offers all four mission types ({menu.ShownRowCount})");
        Is(ctx, "the cursor stands on the preset's mission", "Dogfighting a Squadron", menu.ShownRowText);
        Is(ctx, "the lives stepper stands in the description slot", "Lives   1        ◀ ▶  change", menu.ShownDetail);
        Has(ctx, "the footer names the stepper", "←→  Lives", menu.ShownFooter);
        menu.Drive(Right);
        Is(ctx, "Right raises the lives", "Lives   2        ◀ ▶  change", menu.ShownDetail);
        menu.Drive(Left);
        menu.Drive(Left);
        Is(ctx, "two Lefts reach unlimited", "Lives   Unlimited        ◀ ▶  change", menu.ShownDetail);
        menu.Drive(Left);
        Is(ctx, "and a third stays there", "Lives   Unlimited        ◀ ▶  change", menu.ShownDetail);
        menu.Drive(Right);
        Is(ctx, "Right steps back to one life", "Lives   1        ◀ ▶  change", menu.ShownDetail);
        menu.Drive(Back);
        ctx.Check(menu.ShownScreen == "Environment", $"Back returns to Environment ({menu.ShownScreen})");
        menu.Drive(Accept);
        Is(ctx, "and the mission cursor survives it", "Dogfighting a Squadron", menu.ShownRowText);
    }

    private static void AceSkip(TestContext ctx, LaunchMenu menu)
    {
        menu.Drive(Up);
        Is(ctx, "the first mission row is the ace duel", "Dogfighting an Ace", menu.ShownRowText);
        menu.Drive(Accept);
        ctx.Check(menu.ShownScreen == "Plane" && menu.ShownHeading == "SELECT AIRCRAFT",
            $"Accept on the ace duel skips Waves and Wingmen and opens the Aircraft screen ({menu.ShownScreen}, {menu.ShownHeading})");
        Is(ctx, "its breadcrumb names the mission", "Instant Action  ›  Girl Trouble  ›  Sky Haven  ›  Dogfighting an Ace  ›  Aircraft", menu.ShownBreadcrumb);
        Is(ctx, "the airframe cursor stands on the preset's player aircraft", "Firebrand", menu.ShownRowText);
        // The roster is the eleven stock airframes plus whatever the user:// store holds, so the
        // count is a floor and the door is read as the trailing row wherever it lands.
        ctx.Check(menu.ShownRowCount >= 12, $"the eleven airframes, the store's customs and the hangar door ({menu.ShownRowCount})");
        Is(ctx, "the trailing row is the hangar door", LaunchMenu.HangarRow, RowText(menu, menu.ShownRowCount - 1));
        menu.Drive(Back);
        ctx.Check(menu.ShownScreen == "MissionType" && menu.ShownRowText == "Dogfighting an Ace",
            $"Back from the Aircraft screen under the ace duel returns to Mission ({menu.ShownScreen}, {menu.ShownRowText})");
    }

    private static void WavesScreen(TestContext ctx, LaunchMenu menu)
    {
        menu.Drive(Down);
        menu.Drive(Accept);
        Is(ctx, "Accept on a squadron opens the Waves screen", "Waves", menu.ShownScreen);
        Is(ctx, "its heading", "CONFIGURE WAVES", menu.ShownHeading);
        Is(ctx, "its breadcrumb", "Instant Action  ›  Girl Trouble  ›  Sky Haven  ›  Dogfighting a Squadron  ›  Waves  ›  Aircraft", menu.ShownBreadcrumb);
        ctx.Check(menu.ShownRowCount == 5, $"four waves and the Continue row ({menu.ShownRowCount})");
        ctx.Check(menu.ShownRow == 4 && menu.ShownRowText == "Continue → Wingmen",
            $"the cursor opens on the Continue row ({menu.ShownRow}, {menu.ShownRowText})");
        Is(ctx, "its description", "Enter / A  on to the wingmen", menu.ShownDetail);
        menu.Drive(Up);
        Is(ctx, "the fourth wave is empty", "Wave 4 — empty", menu.ShownRowText);
        menu.Drive(Up);
        menu.Drive(Up);
        menu.Drive(Up);
        Is(ctx, "the first wave is the preset's", "Wave 1 — 4x Medusa Kestrel (Veteran)", menu.ShownRowText);
        Is(ctx, "its description", "Enter / A  edit a wave", menu.ShownDetail);

        menu.Drive(Accept);
        Is(ctx, "Accept on a wave opens its editor", "WaveEdit", menu.ShownScreen);
        Is(ctx, "its heading", "WAVE 1", menu.ShownHeading);
        ctx.Check(menu.ShownRowCount == 4, $"four fields ({menu.ShownRowCount})");
        Field(ctx, "the enemies field", "Enemies", "4", menu.ShownRowText);
        Has(ctx, "the footer names the stepper", "←→  Change", menu.ShownFooter);
        menu.Drive(Right);
        Field(ctx, "Right raises the count", "Enemies", "5", menu.ShownRowText);
        menu.Drive(Down);
        Field(ctx, "the militia field", "Militia", "Medusa", menu.ShownRowText);
        menu.Drive(Right);
        Field(ctx, "Right steps the militia", "Militia", "Russian", menu.ShownRowText);
        menu.Drive(Down);
        Field(ctx, "a new militia resets its aircraft to the first it flies", "Aircraft", "Devastator", menu.ShownRowText);
        menu.Drive(Right);
        Field(ctx, "a one-aircraft militia's stepper wraps onto itself", "Aircraft", "Devastator", menu.ShownRowText);
        menu.Drive(Down);
        Field(ctx, "the skill field", "Skill", "Veteran", menu.ShownRowText);
        menu.Drive(Right);
        Field(ctx, "Right steps the skill", "Skill", "Ace", menu.ShownRowText);
        menu.Drive(Down);
        Field(ctx, "the field cursor wraps", "Enemies", "5", menu.ShownRowText);
        menu.Drive(Back);
        ctx.Check(menu.ShownScreen == "Waves" && menu.ShownRowText == "Wave 1 — 5x Russian Devastator (Ace)",
            $"Back returns to the list with the wave edited live ({menu.ShownScreen}, {menu.ShownRowText})");
        menu.Drive(Down);
        Is(ctx, "the second wave is the preset's second", "Wave 2 — 2x Black Swan Fury (Ace)", menu.ShownRowText);
        menu.Drive(Accept);
        menu.Drive(Accept);
        ctx.Check(menu.ShownScreen == "Waves" && menu.ShownRow == 1,
            $"Accept in the editor means done and keeps the wave's row ({menu.ShownScreen}, {menu.ShownRow})");
    }

    private static void WingmenScreen(TestContext ctx, LaunchMenu menu)
    {
        menu.Drive(Down);
        menu.Drive(Down);
        menu.Drive(Down);
        Is(ctx, "back on the Continue row", "Continue → Wingmen", menu.ShownRowText);
        menu.Drive(Accept);
        Is(ctx, "Continue opens the Wingmen screen", "Wingmen", menu.ShownScreen);
        Is(ctx, "its heading", "WINGMEN", menu.ShownHeading);
        Is(ctx, "its breadcrumb", "Instant Action  ›  Girl Trouble  ›  Sky Haven  ›  Dogfighting a Squadron  ›  Wingmen  ›  Aircraft", menu.ShownBreadcrumb);
        ctx.Check(menu.ShownRowCount == 2, $"the count and the aircraft rows ({menu.ShownRowCount})");
        Field(ctx, "the count is the preset's", "Wingmen", "2", menu.ShownRowText);
        Has(ctx, "the footer offers the wingman loadout", "L / Y  Weapons", menu.ShownFooter);
        menu.Drive(Down);
        Field(ctx, "the aircraft is the preset's", "Aircraft", "Peacemaker", menu.ShownRowText);
        menu.Drive(Right);
        Field(ctx, "Right steps the wingman aircraft", "Aircraft", "Warhawk", menu.ShownRowText);
        menu.Drive(Up);
        menu.Drive(Left);
        menu.Drive(Left);
        ctx.Check(menu.ShownRowCount == 1 && menu.ShownRowText.StartsWith("Wingmen", StringComparison.Ordinal) && menu.ShownRowText.EndsWith(" 0", StringComparison.Ordinal),
            $"at zero wingmen the aircraft row is hidden ({menu.ShownRowCount}, {menu.ShownRowText})");
        ctx.Check(!menu.ShownFooter.Contains("L / Y  Weapons", StringComparison.Ordinal),
            $"and the footer drops the loadout ({menu.ShownFooter})");
        menu.Drive(Loadout);
        Is(ctx, "Loadout at zero wingmen does nothing", "Wingmen", menu.ShownScreen);
        menu.Drive(Left);
        Field(ctx, "the count does not go below zero", "Wingmen", "0", menu.ShownRowText);
        menu.Drive(Right);
        menu.Drive(Right);
        ctx.Check(menu.ShownRowCount == 2, $"two wingmen bring the aircraft row back ({menu.ShownRowCount})");
        menu.Drive(Loadout);
        Is(ctx, "Loadout with wingmen opens the wingman loadout", "WingmanLoadout", menu.ShownScreen);
        Is(ctx, "its heading names the wingman aircraft", "WINGMEN — AMMO SELECTION  (Warhawk)", menu.ShownHeading);
        menu.Drive(Back);
        Is(ctx, "Back leaves the loadout for Wingmen", "Wingmen", menu.ShownScreen);
        menu.Drive(Loadout);
        menu.Drive(Loadout);
        Is(ctx, "Loadout again also leaves it", "Wingmen", menu.ShownScreen);
    }

    private static void BackPaths(TestContext ctx, LaunchMenu menu)
    {
        menu.Drive(Back);
        Is(ctx, "Back from Wingmen returns to Waves", "Waves", menu.ShownScreen);
        menu.Drive(Back);
        Is(ctx, "Back from Waves returns to Mission", "MissionType", menu.ShownScreen);
        menu.Drive(Back);
        ctx.Check(menu.ShownScreen == "Environment" && menu.ShownRowText == "Sky Haven",
            $"Back from Mission returns to Environment on its pick ({menu.ShownScreen}, {menu.ShownRowText})");
        menu.Drive(Accept);
        menu.Drive(Accept);
        ctx.Check(menu.ShownScreen == "Waves" && menu.ShownRow == 4,
            $"the forward path re-parks the Waves cursor on Continue ({menu.ShownScreen}, row {menu.ShownRow})");
        menu.Drive(Accept);
        menu.Drive(Accept);
        ctx.Check(menu.ShownScreen == "Plane" && menu.ShownRowText == "Firebrand",
            $"Wingmen's Accept opens the Aircraft screen on the preset's aircraft ({menu.ShownScreen}, {menu.ShownRowText})");
        Is(ctx, "its breadcrumb", "Instant Action  ›  Girl Trouble  ›  Sky Haven  ›  Dogfighting a Squadron  ›  Aircraft", menu.ShownBreadcrumb);
        menu.Drive(Back);
        Is(ctx, "Back from the Aircraft screen under a squadron returns to Wingmen", "Wingmen", menu.ShownScreen);
        menu.Drive(Accept);
    }

    private static void Launch(TestContext ctx, LaunchMenu menu, Launches launches)
    {
        menu.Drive(Down);
        Is(ctx, "one row down the roster", "Fury", menu.ShownRowText);
        menu.Drive(Accept);
        Is(ctx, "the first Accept selects", "AIRCRAFT SELECTED", menu.ShownHeading);
        ctx.Check(launches.Count == 0, $"and nothing has launched ({launches.Count})");
        menu.Drive(Accept);
        ctx.Check(launches.Count == 1, $"the second Accept launches, once ({launches.Count})");
        if (launches.Count != 1)
        {
            return;
        }

        var launch = launches[0];
        Is(ctx, "the launch carries the environment's chapter", "C4", launch.Chapter);
        ctx.Check(launch.Mode == MenuMode.Stunt, $"in the Instant Action mode ({launch.Mode})");
        ctx.Check(launch.Seats.Count == 1 && launch.Seats[0].PlaneNode == "player_fury",
            $"for the one seat flying the selected airframe ({launch.Seats.Count}, {launch.Seats[0].PlaneNode})");
        var def = launch.InstantAction;
        ctx.Check(def != null, $"with an Instant Action def");
        if (def == null)
        {
            return;
        }

        Is(ctx, "the def's mission type", "dogfight_squadron", def.MissionType);
        Is(ctx, "its player aircraft is the seat's stock name", "Fury", def.PlayerPlane);
        ctx.Check(def.NumWingmen == 2, $"two wingmen ({def.NumWingmen})");
        Is(ctx, "flying the stepped aircraft", "Warhawk", def.WingmanPlane);
        ctx.Check(def.WingmanLoadout == null, $"with the stock fit");
        ctx.Check(def.Lives == 1, $"one life ({def.Lives})");
        ctx.Check(def.Waves.Count == 4, $"four wave slots ({def.Waves.Count})");
        ctx.Check(def.Waves[0] == new InstantActionWave(5, "Russian Devastator", "Devastator", "ace", -1),
            $"the first wave as edited ({def.Waves[0]})");
        ctx.Check(def.Waves[1] == new InstantActionWave(2, "Black Swan Fury", "Fury", "ace", -1),
            $"the second as the preset had it ({def.Waves[1]})");
        ctx.Check(def.Waves[2] == InstantAction.EmptyWave && def.Waves[3] == InstantAction.EmptyWave,
            $"the two unused slots as the empty wave");
        ctx.Check(def.AceName != "Marshall Bill Redmann" && def.AceName.Length > 0,
            $"the ace is the environment's own, not the built-in default ({def.AceName})");
        ctx.Check(menu.ShownScreen == "Plane" && menu.Visible,
            $"the menu keeps its state for the host to hide ({menu.ShownScreen})");
    }

    private static void Return(TestContext ctx, LaunchMenu menu, Launches launches)
    {
        menu.HideMenu();
        menu.ShowMenu();
        ctx.Check(menu.ShownScreen == "Mode" && menu.ShownRowText == "Instant Action",
            $"a return re-enters on the Mode screen with the mode cursor kept ({menu.ShownScreen}, {menu.ShownRowText})");
        menu.Drive(Accept);
        Is(ctx, "the environment survives the flight", "Sky Haven", menu.ShownRowText);
        Has(ctx, "and so does the applied preset's name", "Girl Trouble", menu.ShownBreadcrumb);
        menu.Drive(Accept);
        Is(ctx, "the mission survives it", "Dogfighting a Squadron", menu.ShownRowText);
        Is(ctx, "and the lives", "Lives   1        ◀ ▶  change", menu.ShownDetail);
        menu.Drive(Accept);
        menu.Drive(Up);
        menu.Drive(Up);
        menu.Drive(Up);
        menu.Drive(Up);
        Is(ctx, "and the edited wave", "Wave 1 — 5x Russian Devastator (Ace)", menu.ShownRowText);
        menu.Drive(Down);
        menu.Drive(Down);
        menu.Drive(Down);
        menu.Drive(Down);
        menu.Drive(Accept);
        Field(ctx, "and the wingmen", "Wingmen", "2", menu.ShownRowText);
        menu.Drive(Accept);
        ctx.Check(menu.ShownHeading == "SELECT AIRCRAFT" && menu.ShownRowText == "Fury",
            $"the airframe cursor survives it and the selection does not ({menu.ShownHeading}, {menu.ShownRowText})");
        ctx.Check(launches.Count == 1, $"and nothing relaunched on the way back in ({launches.Count})");
    }

    private static void Aids(TestContext ctx, LaunchMenu menu, Launches launches)
    {
        menu.ShowMenu("presets");
        ctx.Check(menu.ShownScreen == "Presets" && menu.ShownHeading == "TABLE OF CONTENTS  (1/19)",
            $"--menu=presets opens the Table of Contents on its cursor ({menu.ShownScreen}, {menu.ShownHeading})");
        menu.ShowMenu("environment");
        ctx.Check(menu.ShownScreen == "Environment" && menu.ShownRowText == "Sky Haven",
            $"--menu=environment opens the Environment screen on its cursor ({menu.ShownScreen}, {menu.ShownRowText})");
        menu.ShowMenu("missiontype");
        ctx.Check(menu.ShownScreen == "MissionType" && menu.ShownRowText == "Dogfighting a Squadron",
            $"--menu=missiontype opens the Mission screen ({menu.ShownScreen}, {menu.ShownRowText})");
        menu.ShowMenu("waves");
        ctx.Check(menu.ShownScreen == "Waves" && menu.ShownRow == 4,
            $"--menu=waves opens the Waves screen parked on Continue ({menu.ShownScreen}, row {menu.ShownRow})");
        menu.ShowMenu("wingmen");
        ctx.Check(menu.ShownScreen == "Wingmen" && menu.ShownRowCount == 2,
            $"--menu=wingmen opens the Wingmen screen ({menu.ShownScreen}, {menu.ShownRowCount})");
        menu.ShowMenu("wingmanloadout");
        ctx.Check(menu.ShownScreen == "WingmanLoadout" && menu.ShownHeading.StartsWith("WINGMEN — AMMO SELECTION", StringComparison.Ordinal),
            $"--menu=wingmanloadout opens the wingman loadout ({menu.ShownScreen}, {menu.ShownHeading})");

        // The aids force the Instant Action mode whatever the Mode screen last picked.
        menu.ShowMenu();
        menu.Drive(Up);
        menu.Drive(Accept);
        Is(ctx, "Free Flight picked on the Mode screen", "Free Flight  ›  Map  ›  Aircraft", menu.ShownBreadcrumb);
        menu.ShowMenu("environment");
        ctx.Check(menu.ShownBreadcrumb.StartsWith("Instant Action", StringComparison.Ordinal),
            $"--menu=environment forces the mode back to Instant Action ({menu.ShownBreadcrumb})");
        menu.Drive(Accept);
        menu.Drive(Contents);
        Is(ctx, "Contents anywhere but the Environment screen does nothing", "MissionType", menu.ShownScreen);

        // --debug-waves= and --debug-wingmen= fill the wizard's slots with their own loads.
        menu.DebugWaves(2);
        menu.ShowMenu("waves");
        Is(ctx, "--debug-waves=2 fills the first wave", "Wave 1 — 4x Black Hat Autogyro (Novice)", RowText(menu, 0));
        Is(ctx, "and the second", "Wave 2 — 4x Black Swan Fury (Veteran)", RowText(menu, 1));
        Is(ctx, "and leaves the third empty", "Wave 3 — empty", RowText(menu, 2));
        menu.DebugWingmen(3);
        menu.ShowMenu("wingmen");
        Field(ctx, "--debug-wingmen=3 sets the count", "Wingmen", "3", menu.ShownRowText);
        menu.Drive(Down);
        Field(ctx, "and the second airframe", "Aircraft", "Hellhound", menu.ShownRowText);

        // --debug-preset= applies a preset over both and opens on step 1; the ace preset then
        // launches a solo duel through the ace skip.
        menu.DebugPreset(2);
        menu.ShowMenu("environment");
        ctx.Check(menu.ShownRowText == "the ocean" && menu.ShownBreadcrumb.Contains("Me and My Big Mouth", StringComparison.Ordinal),
            $"--debug-preset=2 applies Me and My Big Mouth ({menu.ShownRowText}, {menu.ShownBreadcrumb})");
        menu.Drive(Accept);
        Is(ctx, "whose mission is the ace duel", "Dogfighting an Ace", menu.ShownRowText);
        menu.Drive(Accept);
        ctx.Check(menu.ShownScreen == "Plane" && menu.ShownRowText == "Autogyro",
            $"which skips to the Aircraft screen on the preset's Autogyro ({menu.ShownScreen}, {menu.ShownRowText})");
        menu.Drive(Accept);
        menu.Drive(Accept);
        ctx.Check(launches.Count == 2, $"and launches ({launches.Count})");
        if (launches.Count == 2 && launches[1].InstantAction is { } ace)
        {
            ctx.Check(launches[1].Chapter == "C1B" && ace.MissionType == "dogfight_ace" && ace.PlayerPlane == "Autogyro",
                $"the ace duel over the ocean in an Autogyro ({launches[1].Chapter}, {ace.MissionType}, {ace.PlayerPlane})");
            ctx.Check(ace.NumWingmen == 0 && ace.Waves[0] == InstantAction.EmptyWave && ace.Waves[1] == InstantAction.EmptyWave,
                $"with the wingmen and every wave forced empty ({ace.NumWingmen}, {ace.Waves[0].NumEnemies})");
        }

        // The clouds bar Stunt Flying, so the mission roster there is three rows.
        menu.ShowMenu("environment");
        menu.Drive(Up);
        menu.Drive(Up);
        menu.Drive(Up);
        Is(ctx, "three rows up from the ocean", "the clouds", menu.ShownRowText);
        menu.Drive(Accept);
        ctx.Check(menu.ShownRowCount == 3, $"the clouds offer three mission types ({menu.ShownRowCount})");
        ctx.Check(RowText(menu, 0) == "Dogfighting an Ace" && RowText(menu, 1) == "Dogfighting a Squadron" && RowText(menu, 2) == "Attacking a Zeppelin",
            $"with Stunt Flying withheld ({RowText(menu, 0)}, {RowText(menu, 1)}, {RowText(menu, 2)})");
    }

    private static void OriginalScreenOpens(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, InstantActionFeature ia)
    {
        var door = Row(shell, "MM_B_INSTANTACTION");
        ctx.Check(door is { Enabled: true }, $"the top level's Instant Action row is live");
        if (door == null)
        {
            return;
        }

        Press(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.InstantAction, $"a click on it opens the Instant Action screen ({shell.Screen})");
        ctx.Check(ia.BaseDef != null && ia.BaseDef.AceName != "Marshall Bill Redmann",
            $"with the first environment's own def loaded as the base ({ia.BaseDef?.AceName ?? "none"})");
        int contents = 0;
        foreach (var row in shell.Rows)
        {
            if (row.Kind == OriginalRowKind.ListRow && row.Key.StartsWith(OriginalShell.ContentsKey + ":", StringComparison.Ordinal))
            {
                contents++;
            }
        }

        ctx.Check(contents == 14, $"the contents window shows the layout's fourteen rows ({contents})");
        ctx.Check(Row(shell, OriginalShell.PlayerPlaneKey) is { Label: "Stock Autogyro", X: 511f, Y: 210f, Width: 224f, Height: 18f },
            $"the player plane dropdown stands at its authored line with the first airframe as its stock row ({Row(shell, OriginalShell.PlayerPlaneKey)?.Label})");
        ctx.Check(Row(shell, OriginalShell.MissionKey)?.Label == "Dogfighting an Ace" && Row(shell, "IA_D_NENEMY0") == null,
            $"the ace duel opens with no enemy row ({Row(shell, OriginalShell.MissionKey)?.Label})");
        ctx.Check(Row(shell, OriginalShell.ExitKey) is { Enabled: true, Width: 200f, Height: 32f },
            $"Exit is the measured four-frame strip ({Row(shell, OriginalShell.ExitKey)?.Width}x{Row(shell, OriginalShell.ExitKey)?.Height})");
        ctx.Check(Row(shell, OriginalShell.BuildKey) is { Enabled: true } && Row(shell, OriginalShell.WeaponLoadoutKey) is { Enabled: true },
            $"Build Custom Plane and Weapon Loadout are live");
        var board = shell.Compose();
        ctx.Check(board.Backdrop.Count == 1 && board.Backdrop[0].Art.Name == "IA_BackGround.jpg",
            $"the page's background is the layout's own ({board.Backdrop.Count})");
    }

    private static void OriginalKeyboard(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, InstantActionFeature ia)
    {
        ctx.Check(shell.FocusedKey == OriginalShell.ContentsKey + ":0", $"the focus opens on the first contents row ({shell.FocusedKey})");
        Press(host, seat, Right);
        ctx.Check(shell.FocusedKey == OriginalShell.PlayerPlaneKey, $"Right crosses to the right page's first dropdown ({shell.FocusedKey})");
        Press(host, seat, Right);
        ctx.Check(ia.PlayerPlane.Name == "Hellhound", $"Right on a dropdown steps its value ({ia.PlayerPlane.Name})");
        Press(host, seat, Accept);
        // The list is the eleven stock rows and then whatever the user's store holds, so the
        // count is a floor and the stock prefix is what the first rows are checked for.
        ctx.Check(shell.OpenDropdown == OriginalShell.PlayerPlaneKey && shell.Rows.Count >= 11 && StockRows(shell) == 11,
            $"Accept opens its list with the eleven stock rows first ({shell.OpenDropdown}, {shell.Rows.Count}, {StockRows(shell)} stock)");
        ctx.Check(shell.Rows[0].Label == "Stock Autogyro" && shell.Rows[1].Label == "Stock Hellhound" && shell.FocusedKey == OriginalShell.PlayerPlaneKey + ":1",
            $"named Stock <airframe> with the focus on the current row ({shell.Rows[0].Label}, {shell.Rows[1].Label}, {shell.FocusedKey})");
        Press(host, seat, Down);
        Press(host, seat, Accept);
        ctx.Check(shell.OpenDropdown == null && ia.PlayerPlane.Name == "Balmoral" && Row(shell, OriginalShell.PlayerPlaneKey)?.Label == "Stock Balmoral",
            $"Down and Accept pick the next row and close the list ({ia.PlayerPlane.Name}, {Row(shell, OriginalShell.PlayerPlaneKey)?.Label})");
        Press(host, seat, Down);
        ctx.Check(shell.FocusedKey == OriginalShell.WingmenKey, $"Down walks the right page ({shell.FocusedKey})");
        Press(host, seat, Right);
        ctx.Check(ia.NumWingmen == 1 && Row(shell, OriginalShell.WingmanPlaneKey) != null,
            $"one wingman shows the wingman plane dropdown ({ia.NumWingmen})");
        Press(host, seat, Left);
        ctx.Check(ia.NumWingmen == 0 && Row(shell, OriginalShell.WingmanPlaneKey) == null,
            $"and zero hides it again ({ia.NumWingmen})");
    }

    // Four presets, one per mission type, each flown through Fly Mission: the exit is the
    // feature's, its def derives the session spec the launcher would build, and the screen is
    // re-entered from the top level the way a return does.
    private static void OriginalLaunches(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, InstantActionFeature ia, List<MenuExit> exits)
    {
        var cli = SessionSpec.Parse(Array.Empty<string>());
        var cases = new (int Preset, string Chapter, string Mission, string Plane, string Node, int Wingmen, int Enemies)[]
        {
            (2, "C1B", "dogfight_ace", "Autogyro", "player_autogyro", 0, 0),
            (0, "C4", "dogfight_squadron", "Firebrand", "player_fbrand", 2, 4),
            (1, "C5", "stunt_flying", "Bloodhawk", "player_bhawk", 0, 4),
            (3, "C3", "zeppelin_run", "Fury", "player_fury", 4, 6),
        };
        foreach (var c in cases)
        {
            if (shell.Screen != OriginalScreen.InstantAction)
            {
                var door = Row(shell, "MM_B_INSTANTACTION");
                if (door == null)
                {
                    return;
                }

                Press(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
            }

            var row = Row(shell, $"{OriginalShell.ContentsKey}:{c.Preset}");
            var fly = Row(shell, OriginalShell.FlyMissionKey);
            ctx.Check(row != null && fly != null, $"preset {c.Preset} and Fly Mission are on screen");
            if (row == null || fly == null)
            {
                return;
            }

            int before = exits.Count;
            Press(host, seat, Pointer(fit, row.X + 5f, row.Y + 5f, pressed: true, clicked: true));
            ctx.Check(ia.PresetIndex == c.Preset && ia.Environment.Code == c.Chapter && ia.MissionType.Key == c.Mission,
                $"clicking contents row {c.Preset} applies it ({ia.PresetIndex}, {ia.Environment.Code}, {ia.MissionType.Key})");
            fly = Row(shell, OriginalShell.FlyMissionKey)!;
            Press(host, seat, Pointer(fit, fly.X + 5f, fly.Y + 5f, pressed: true, clicked: true));
            ctx.Check(exits.Count == before + 1 && exits[^1] is LaunchExit, $"Fly Mission leaves as one LaunchExit ({exits.Count - before})");
            if (exits[^1] is not LaunchExit launch)
            {
                return;
            }

            var def = launch.InstantAction;
            ctx.Check(launch.Chapter == c.Chapter && launch.Mode == MenuMode.Stunt && def != null,
                $"the {c.Mission} exit names {c.Chapter} in the Instant Action mode ({launch.Chapter}, {launch.Mode})");
            ctx.Check(launch.Seats.Count == 1 && launch.Seats[0].PlaneNode == c.Node && launch.Seats[0].Pads.Count == 0,
                $"for seat 0 alone in the preset's airframe with no pads ({launch.Seats[0].PlaneNode})");
            if (def == null)
            {
                return;
            }

            ctx.Check(def.MissionType == c.Mission && def.PlayerPlane == c.Plane && def.NumWingmen == c.Wingmen && def.Waves[0].NumEnemies == c.Enemies,
                $"the def is the preset's ({def.MissionType}, {def.PlayerPlane}, {def.NumWingmen} wingmen, {def.Waves[0].NumEnemies} in wave 1)");
            ctx.Check(def.AceName != "Marshall Bill Redmann",
                $"over the environment's own def ({def.AceName})");
            var spec = SessionSpec.FromMenu(cli, launch.Chapter, new[] { launch.Seats[0].PlaneNode }, launch.Mode, def);
            ctx.Check(spec.Chapter == c.Chapter && spec.Scenario == c.Mission && spec.Stunt == (c.Mission == "stunt_flying") && spec.IaDef == def,
                $"and derives the session spec the launcher builds ({spec.Chapter}, {spec.Scenario}, stunt={spec.Stunt})");
            ctx.Check(!host.Shown, $"the host hid the presentation on the exit");
            host.Show(MenuReturnDestination.TopLevel);
            ctx.Check(shell.Screen == OriginalScreen.TopLevel && ia.PresetIndex == c.Preset,
                $"the return re-enters the top level with the setup kept ({shell.Screen}, preset {ia.PresetIndex})");
        }
    }

    // A build saved to the user's store, entered from the top level: the Pilot Plane list offers
    // it after the stock rows under "<build name> <airframe>", a click on it flies the airframe's
    // stock node with the def on the seat, the pick survives a return, the wingman list never
    // lists it, the roster re-read offers a build saved while the screen shows, and deleting the
    // file drops the row and the pick back onto the airframe's stock row.
    private static void OriginalCustomPilot(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, InstantActionFeature ia, List<MenuExit> exits, CustomPlaneStore store, string scratch)
    {
        const int Fury = 7;
        string rowText = scratch + " Fury";
        store.Save(new CustomPlaneDef { Name = scratch, Airframe = Fury, Engine = 1 });
        if (!EnterInstantAction(ctx, host, seat, shell, fit))
        {
            return;
        }

        int row = PilotRowOf(shell, scratch);
        ctx.Check(row >= 11 && shell.PilotRoster[row].Node == "player_fury",
            $"entering the screen reads the store: the build sits after the eleven stock rows flying the Fury's node (row {row})");

        // The wingman list, opened beside it, is the stock table alone.
        if (ia.NumWingmen == 0)
        {
            ia.SetWingmen(1);
        }

        var wingmanDrop = Row(shell, OriginalShell.WingmanPlaneKey);
        ctx.Check(wingmanDrop != null, $"the Wingman Plane dropdown shows with a wingman ({ia.NumWingmen})");
        if (wingmanDrop != null)
        {
            Press(host, seat, Pointer(fit, wingmanDrop.X + 5f, wingmanDrop.Y + 5f, pressed: true, clicked: true));
            ctx.Check(shell.OpenDropdown == OriginalShell.WingmanPlaneKey && shell.Rows.Count == 11 && LabelRow(shell, rowText) < 0 && shell.Rows[7].Label == "Fury",
                $"the wingman list is the eleven stock names alone ({shell.Rows.Count}, {shell.Rows[7].Label})");
            Press(host, seat, Accept);
            ctx.Check(shell.OpenDropdown == null, $"Accept on its current row closes it");
        }

        var drop = Row(shell, OriginalShell.PlayerPlaneKey);
        ctx.Check(drop != null, $"the Pilot Plane dropdown is on screen");
        if (drop == null || row < 0)
        {
            return;
        }

        Press(host, seat, Pointer(fit, drop.X + 5f, drop.Y + 5f, pressed: true, clicked: true));
        var item = Row(shell, $"{OriginalShell.PlayerPlaneKey}:{row}");
        ctx.Check(shell.OpenDropdown == OriginalShell.PlayerPlaneKey && item != null && item.Label == rowText && StockRows(shell) == 11,
            $"a click opens the list with the build's row named for it after the stock rows ({item?.Label})");
        if (item == null)
        {
            return;
        }

        Press(host, seat, Pointer(fit, item.X + 5f, item.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.OpenDropdown == null && shell.PilotRow == row && Row(shell, OriginalShell.PlayerPlaneKey)?.Label == rowText,
            $"a click on it picks the build and closes the list ({shell.PilotRow}, {Row(shell, OriginalShell.PlayerPlaneKey)?.Label})");
        ctx.Check(ia.PlayerPlane.Name == "Fury", $"the feature's own pick moves onto the build's airframe ({ia.PlayerPlane.Name})");

        var fly = Row(shell, OriginalShell.FlyMissionKey)!;
        int before = exits.Count;
        Press(host, seat, Pointer(fit, fly.X + 5f, fly.Y + 5f, pressed: true, clicked: true));
        ctx.Check(exits.Count == before + 1 && exits[^1] is LaunchExit, $"Fly Mission leaves as one LaunchExit ({exits.Count - before})");
        if (exits[^1] is not LaunchExit launch)
        {
            return;
        }

        var choice = launch.Seats[0];
        ctx.Check(launch.Seats.Count == 1 && choice.PlaneNode == "player_fury" && choice.Custom?.Name == scratch,
            $"seat 0 flies the airframe's stock node with the build's def on the seat ({choice.PlaneNode}, {choice.Custom?.Name ?? "no def"})");
        ctx.Check(launch.InstantAction?.PlayerPlane == "Fury", $"the def's player plane is the airframe's stock name ({launch.InstantAction?.PlayerPlane})");
        var spec = SessionSpec.FromMenu(SessionSpec.Parse(Array.Empty<string>()), launch.Chapter, new[] { choice.PlaneNode },
            launch.Mode, launch.InstantAction, customPlanes: new[] { choice.Custom });
        ctx.Check(spec.PlaneNames.Count == 1 && spec.PlaneNames[0] == "player_fury" && spec.MenuCustomPlanes.Count == 1 && spec.MenuCustomPlanes[0]?.Name == scratch,
            $"and the session spec resolves to the stock node with the build's name riding it ({spec.PlaneNames[0]}, {spec.MenuCustomPlanes[0]?.Name ?? "no def"})");

        host.Show(MenuReturnDestination.TopLevel);
        if (!EnterInstantAction(ctx, host, seat, shell, fit))
        {
            return;
        }

        ctx.Check(shell.PilotRow == PilotRowOf(shell, scratch) && Row(shell, OriginalShell.PlayerPlaneKey)?.Label == rowText,
            $"the pick survives the return ({Row(shell, OriginalShell.PlayerPlaneKey)?.Label})");

        // The re-read the hangar's return calls: a build saved while the screen shows is offered
        // without leaving it, and a deleted file drops both its row and the pick.
        string second = ScratchName();
        store.Save(new CustomPlaneDef { Name = second, Airframe = 3, Engine = 1 });
        try
        {
            ctx.Check(PilotRowOf(shell, second) < 0, $"a build saved while the screen shows is not offered until the roster is re-read");
            shell.RefreshInstantActionRoster();
            int secondRow = PilotRowOf(shell, second);
            ctx.Check(secondRow >= 11 && shell.PilotRoster[secondRow].Node == "player_bhawk",
                $"RefreshInstantActionRoster offers it on the Bloodhawk's node (row {secondRow})");
            ctx.Check(Row(shell, OriginalShell.PlayerPlaneKey)?.Label == rowText, $"and the standing pick is kept ({Row(shell, OriginalShell.PlayerPlaneKey)?.Label})");
        }
        finally
        {
            store.Delete(second);
        }

        store.Delete(scratch);
        shell.RefreshInstantActionRoster();
        ctx.Check(PilotRowOf(shell, scratch) < 0 && PilotRowOf(shell, second) < 0 && shell.PilotRoster.Count >= 11,
            $"deleting the files drops their rows ({shell.PilotRoster.Count} rows)");
        ctx.Check(Row(shell, OriginalShell.PlayerPlaneKey)?.Label == "Stock Fury" && ia.PlayerPlane.Name == "Fury",
            $"and the pick falls back onto the airframe's stock row ({Row(shell, OriginalShell.PlayerPlaneKey)?.Label})");
        var exit = Row(shell, OriginalShell.ExitKey)!;
        Press(host, seat, Pointer(fit, exit.X + 5f, exit.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.TopLevel, $"Exit returns to the top level ({shell.Screen})");
    }

    // Weapon Loadout with the radio on Wingman: the decoded ammo chrome opens over the wingmen's
    // shared fit and the wingman airframe's own slots and pylons, a sideways step on a field
    // writes the fit, CANCEL restores what stood on entry, ACCEPT keeps it, and Fly Mission's def
    // carries the kept fit.
    private static void OriginalLoadout(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, InstantActionFeature ia)
    {
        if (!EnterInstantAction(ctx, host, seat, shell, fit))
        {
            return;
        }

        var radio = Row(shell, OriginalShell.WingmanRadioKey)!;
        Press(host, seat, Pointer(fit, radio.X + 5f, radio.Y + 5f, pressed: true, clicked: true));
        var loadout = Row(shell, OriginalShell.WeaponLoadoutKey);
        ctx.Check(shell.LoadoutTarget == 1 && loadout is { Enabled: true }, $"the Wingman radio takes the target and Weapon Loadout is live ({shell.LoadoutTarget})");
        if (loadout == null)
        {
            return;
        }

        Press(host, seat, Pointer(fit, loadout.X + 5f, loadout.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.InstantActionLoadout && ReferenceEquals(shell.LoadoutFit, ia.WingmanFit) && shell.LoadoutNode == ia.WingmanPlane.Node,
            $"a click opens the loadout screen on the wingmen's shared fit over their airframe ({shell.Screen}, {shell.LoadoutNode})");
        var def = StockLoadouts.Load().ForModel(ia.WingmanPlane.Node);
        int guns = 0;
        foreach (var gun in def?.Guns ?? new List<GunSpec>())
        {
            guns += gun.Turret ? 0 : 1;
        }

        int pylons = def?.Hardpoints?.Count ?? 0;
        ctx.Check(shell.Rows.Count == guns + pylons + 2, $"one field per firable gun slot and per pylon of the {ia.WingmanPlane.Name}, then ACCEPT and CANCEL ({shell.Rows.Count} rows, {guns} guns, {pylons} pylons)");
        ctx.Check(Row(shell, OriginalShell.LoadoutAmmoPrefix + "0") is { X: 136f, Y: 120f, Width: 148f, Height: 15f },
            $"the first ammunition field stands at its authored box ({Row(shell, OriginalShell.LoadoutAmmoPrefix + "0")?.X})");
        ctx.Check(Row(shell, OriginalShell.LoadoutAcceptKey) is { X: 341f, Y: 553f, Enabled: true } && Row(shell, OriginalShell.LoadoutCancelKey) is { X: 551f, Y: 553f, Enabled: true },
            $"ACCEPT and CANCEL LOADOUT stand at their authored places");
        var board = shell.Compose();
        ctx.Check(board.Backdrop.Count == 1 && board.Backdrop[0].Art.Name == "OL_BackGround.jpg", $"the screen's background is the section's own ({board.Backdrop.Count})");
        ctx.Check(board.Lines.Any(l => l.Text == ia.WingmanPlane.Name), $"and the fitted aircraft is named on it");

        OriginalRow? rocket = null;
        foreach (var row in shell.Rows)
        {
            if (row.Key.StartsWith(OriginalShell.LoadoutRocketPrefix, StringComparison.Ordinal))
            {
                rocket = row;
                break;
            }
        }

        ctx.Check(rocket != null && ia.WingmanFit.IsStock, $"the {ia.WingmanPlane.Name} carries a pylon and the fit opens stock");
        if (rocket == null)
        {
            return;
        }

        int pylon = int.Parse(rocket.Key[OriginalShell.LoadoutRocketPrefix.Length..], System.Globalization.CultureInfo.InvariantCulture) + 1;
        Press(host, seat, Pointer(fit, rocket.X + 5f, rocket.Y + 5f));
        Press(host, seat, Right);
        ctx.Check(!ia.WingmanFit.IsStock && ia.WingmanFit.PylonFor(pylon) != null && Row(shell, rocket.Key)?.Label != rocket.Label,
            $"Right on the pylon field steps its ordnance into the shared fit ({ia.WingmanFit.PylonFor(pylon)}, {Row(shell, rocket.Key)?.Label})");
        var cancel = Row(shell, OriginalShell.LoadoutCancelKey)!;
        Press(host, seat, Pointer(fit, cancel.X + 5f, cancel.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.InstantAction && ia.WingmanFit.IsStock && shell.FocusedKey == OriginalShell.WeaponLoadoutKey,
            $"CANCEL restores the stock fit and lands back on Weapon Loadout ({shell.Screen}, {shell.FocusedKey})");

        Press(host, seat, Pointer(fit, loadout.X + 5f, loadout.Y + 5f, pressed: true, clicked: true));
        Press(host, seat, Pointer(fit, rocket.X + 5f, rocket.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.OpenDropdown == rocket.Key && shell.Rows.Count == StockLoadouts.Load().Options.PylonOrdnance.Count,
            $"a click on the field opens its list over the ordnance roster ({shell.OpenDropdown}, {shell.Rows.Count})");
        Press(host, seat, Down);
        Press(host, seat, Accept);
        string? picked = ia.WingmanFit.PylonFor(pylon);
        ctx.Check(shell.OpenDropdown == null && picked != null, $"Down and Accept pick the next row and close the list ({picked})");
        var accept = Row(shell, OriginalShell.LoadoutAcceptKey)!;
        Press(host, seat, Pointer(fit, accept.X + 5f, accept.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.InstantAction && ia.WingmanFit.PylonFor(pylon) == picked,
            $"ACCEPT keeps the pick and returns to the screen ({shell.Screen}, {ia.WingmanFit.PylonFor(pylon)})");
        // The def carries the wingman fit only where wingmen fly.
        if (ia.IsAceDuel)
        {
            ia.SelectMissionType(1);
        }

        if (ia.NumWingmen == 0)
        {
            ia.SetWingmen(1);
        }

        ctx.Check(ReferenceEquals(ia.BuildDef().WingmanLoadout, ia.WingmanFit), $"and the built def carries the wingman fit ({ia.MissionType.Key}, {ia.NumWingmen} wingmen)");
        ia.ResetWingmanFit();
    }

    // Build Custom Plane: the wallet-free hangar opens on the name screen, Back returns to the
    // screen, and a purchase returns to it with the new build in the Pilot Plane list.
    private static void OriginalBuild(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, HangarFeature hangar, CustomPlaneStore store, string built)
    {
        var build = Row(shell, OriginalShell.BuildKey);
        ctx.Check(shell.Screen == OriginalScreen.InstantAction && build is { Enabled: true }, $"Build Custom Plane is live on the screen ({shell.Screen})");
        if (build == null)
        {
            return;
        }

        Press(host, seat, Pointer(fit, build.X + 5f, build.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.PlaneName && hangar.IsOpen && hangar.Wallet == null,
            $"a click opens the decoded name screen over a wallet-free build ({shell.Screen})");
        Press(host, seat, Back);
        ctx.Check(shell.Screen == OriginalScreen.InstantAction && !hangar.IsOpen && shell.FocusedKey == OriginalShell.BuildKey,
            $"Back drops the build and returns to the screen on its Build button ({shell.Screen}, {shell.FocusedKey})");

        Press(host, seat, Pointer(fit, build.X + 5f, build.Y + 5f, pressed: true, clicked: true));
        Press(host, seat, new MenuCommands { Typed = built });
        var ok = Row(shell, OriginalShell.NameOkKey);
        ctx.Check(ok is { Enabled: true } && shell.HangarName == built, $"typed frames name the plane ({shell.HangarName})");
        if (ok == null)
        {
            return;
        }

        Press(host, seat, Pointer(fit, ok.X + 5f, ok.Y + 5f, pressed: true, clicked: true));
        var ready = Row(shell, OriginalShell.ReadyKey);
        ctx.Check(shell.Screen == OriginalScreen.HangarAirframe && ready != null, $"OK opens the hub on the default configuration ({shell.Screen})");
        if (ready == null)
        {
            return;
        }

        Press(host, seat, Pointer(fit, ready.X + 5f, ready.Y + 5f, pressed: true, clicked: true));
        var purchase = Row(shell, OriginalShell.PurchaseNowKey);
        ctx.Check(shell.Screen == OriginalScreen.HangarPurchase && purchase is { Enabled: true }, $"READY opens the totals page with Purchase Now live ({shell.Screen})");
        if (purchase == null)
        {
            return;
        }

        Press(host, seat, Pointer(fit, purchase.X + 5f, purchase.Y + 5f, pressed: true, clicked: true));
        int row = PilotRowOf(shell, built);
        ctx.Check(shell.Screen == OriginalScreen.InstantAction && !hangar.IsOpen && store.Load(built) != null,
            $"Purchase Now saves the plane and returns to the screen ({shell.Screen})");
        ctx.Check(row >= 11 && shell.PilotRoster[row].Node == PlanePickerRoster.AirframeNode(HangarFeature.DefaultAirframe),
            $"whose Pilot Plane list offers the build after the stock rows on its airframe's node (row {row})");
        var drop = Row(shell, OriginalShell.PlayerPlaneKey);
        if (drop != null && row >= 0)
        {
            // The row's airframe word is the Instant Action table's, the stock rows' own vocabulary.
            string airframe = string.Empty;
            foreach (var stock in InstantActionFeature.Airframes)
            {
                if (stock.Node == shell.PilotRoster[row].Node)
                {
                    airframe = stock.Name;
                }
            }

            Press(host, seat, Pointer(fit, drop.X + 5f, drop.Y + 5f, pressed: true, clicked: true));
            ctx.Check(Row(shell, $"{OriginalShell.PlayerPlaneKey}:{row}")?.Label == built + " " + airframe,
                $"named for the build in the open list ({Row(shell, $"{OriginalShell.PlayerPlaneKey}:{row}")?.Label})");
            Press(host, seat, Back);
        }

        var exit = Row(shell, OriginalShell.ExitKey)!;
        Press(host, seat, Pointer(fit, exit.X + 5f, exit.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.TopLevel, $"Exit returns to the top level ({shell.Screen})");
    }

    private static bool EnterInstantAction(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit)
    {
        var door = Row(shell, "MM_B_INSTANTACTION");
        ctx.Check(door != null && shell.Screen == OriginalScreen.TopLevel, $"the top level's Instant Action row is on screen ({shell.Screen})");
        if (door == null)
        {
            return false;
        }

        Press(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
        return shell.Screen == OriginalScreen.InstantAction;
    }

    // The index of the open list's row carrying a label, or -1.
    private static int LabelRow(OriginalShell shell, string label)
    {
        var rows = shell.Rows;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Label == label)
            {
                return i;
            }
        }

        return -1;
    }

    // The row a build sits at in the pilot list, or -1: found by its own name among the custom
    // rows, never by the airframe it flies.
    private static int PilotRowOf(OriginalShell shell, string build)
    {
        for (int i = 0; i < shell.PilotRoster.Count; i++)
        {
            if (shell.PilotRoster[i].IsCustom && shell.PilotRoster[i].Name == build)
            {
                return i;
            }
        }

        return -1;
    }

    // How many rows from the top of an open list carry the stock prefix before the first that does not.
    private static int StockRows(OriginalShell shell)
    {
        int n = 0;
        foreach (var row in shell.Rows)
        {
            if (!row.Label.StartsWith("Stock ", StringComparison.Ordinal))
            {
                break;
            }

            n++;
        }

        return n;
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

    // A field row reads as its label, a run of spaces, then its value.
    private static void Field(TestContext ctx, string what, string label, string value, string actual) =>
        ctx.Check(actual.StartsWith(label, StringComparison.Ordinal) && actual.EndsWith(" " + value, StringComparison.Ordinal),
            $"{what}: expected '{label} ... {value}', got '{actual}'");

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

    // The launches among everything the host's sink recorded, read live.
    private sealed class Launches
    {
        private readonly List<MenuExit> _all;

        public Launches(List<MenuExit> all)
        {
            _all = all;
        }

        public int Count
        {
            get
            {
                int n = 0;
                foreach (var exit in _all)
                {
                    if (exit is LaunchExit)
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        public LaunchExit this[int index]
        {
            get
            {
                int seen = 0;
                foreach (var exit in _all)
                {
                    if (exit is LaunchExit match && seen++ == index)
                    {
                        return match;
                    }
                }

                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
}
