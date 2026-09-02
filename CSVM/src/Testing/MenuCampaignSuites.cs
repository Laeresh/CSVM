using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Session;
using CSVM.UI;
using CSVM.UI.Menu;

namespace CSVM.Testing;

/// <summary>
/// Built-in's campaign journey, characterized: a real <see cref="LaunchMenu"/> is driven through
/// the Mode screen's Campaign door over a scratch profile store and its screens are read back
/// through the <c>Shown*</c> read-outs and the composed board. The roster's three states, a player
/// created and one deleted, the cabin, previous missions, the briefing and its reveal, the flight
/// check, ammo and plane selection with their writes into the store, the cabin's PLANE CONSTRUCTION
/// into the hangar and back, FLY MISSION as a <see cref="CampaignMissionExit"/>, the debrief return
/// landing on the scrapbook with its page turn back to the cabin, the guest check under a debug
/// join, and every scratch-profile aid. Every check pins what the screens do today. ⚠ Nothing here
/// touches <c>user://Profiles</c>: the door is pointed at a scratch store and the aids read their own.
/// </summary>
internal static class MenuCampaignSuites
{
    private const string Pilot = "Zachary";
    private const string Guest = "Nathan";
    private const int FlownBefore = 3;

    private static readonly MenuCommands Accept = new() { Accept = true };
    private static readonly MenuCommands Back = new() { Back = true };
    private static readonly MenuCommands Down = new() { MoveY = 1 };
    private static readonly MenuCommands Up = new() { MoveY = -1 };
    private static readonly MenuCommands Left = new() { MoveX = -1 };
    private static readonly MenuCommands Right = new() { MoveX = 1 };
    private static readonly MenuCommands Secondary = new() { Contents = true };

    [Suite("menu-campaign-journey",
        "Built-in's campaign journey pinned end to end over a scratch profile store: the Mode "
        + "screen's Campaign door opens the empty roster, a typed name creates a player and lands on "
        + "the cabin, the roster then stands on that player, a second player is created and deleted "
        + "through the confirm stage, previous missions lists a progressed profile's three flights and "
        + "opens the scrapbook, the briefing runs its reveal and restarts its narration on REPLAY, the "
        + "flight check opens ammo selection whose ACCEPT writes the pick into the profile and whose "
        + "CANCEL writes nothing, plane selection writes the pilot's pick on ACCEPT and keeps it on "
        + "CANCEL, PLANE CONSTRUCTION opens the hangar over the profile's wallet and Back resumes the "
        + "cabin, FLY MISSION leaves as one CampaignMissionExit with the profile saved, the debrief "
        + "return opens the scrapbook on the flown mission and turns back to the cabin, the guest "
        + "check walks a debug-joined field, and every scratch-profile aid opens its screen")]
    internal static void MenuCampaignJourney(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        var exits = new List<MenuExit>();
        var host = MenuSuiteHost.Bare(exits, ctx.DataRoot, out var seat);
        var menu = LaunchMenu.Build(ctx.ZrdrPath, ctx.DataRoot, host, seat.Input);
        ctx.Host.AddChild(menu);
        menu.SetProcess(false);
        string root = Path.Combine(ctx.ScratchDir, "menu-campaign-journey");
        string dir = Path.Combine(root, "Profiles");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        var store = new CampaignProfileStore(dir);
        menu.CampaignProfiles = store;
        try
        {
            EmptyRoster(ctx, menu);
            CreatePlayer(ctx, menu, store);
            SecondPlayerAndDelete(ctx, menu, store);
            FreshCabin(ctx, menu, store);
            Progress(store);
            PreviousMissions(ctx, menu, store);
            Briefing(ctx, menu);
            FlightCheckAndAmmo(ctx, menu, store);
            PlaneSelection(ctx, menu, store);
            HangarDoor(ctx, menu, store);
            FlyMission(ctx, menu, store, exits);
            DebriefReturn(ctx, menu, store);
            GuestCheck(ctx, menu);
            Aids(ctx, menu);
        }
        finally
        {
            ctx.Host.RemoveChild(menu);
            menu.QueueFree();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void EmptyRoster(TestContext ctx, LaunchMenu menu)
    {
        menu.ShowMenu();
        WalkTo(menu, LaunchMenu.CampaignRow);
        Is(ctx, "the Mode screen's fourth row is the campaign door", LaunchMenu.CampaignRow, menu.ShownRowText);
        menu.Drive(Accept);
        Is(ctx, "Accept on the door opens the Campaign screen", "Campaign", menu.ShownScreen);
        Is(ctx, "on the roster", "SELECT PLAYER", menu.ShownHeading);
        Is(ctx, "its breadcrumb", LaunchMenu.CampaignRow + "  ›  SELECT PLAYER", menu.ShownBreadcrumb);
        ctx.Check(menu.Campaign is { Screen: CampaignScreen.Roster, Profile: null } && menu.Campaign.Roster.Count == 0,
            $"the flow is open on the roster over an empty store with nobody seated");
        ctx.Check(menu.ShownRowCount == 4 && menu.ShownRow == 0, $"an empty roster is the name field and three buttons, cursor on the field ({menu.ShownRowCount}, {menu.ShownRow})");
        Is(ctx, "the field reads nobody", "Name:  (none)", menu.ShownRowText);
        Is(ctx, "described as the way to type", "Enter / A to type a player name", menu.ShownDetail);
        ctx.Check(menu.ShownBoard is { } board && board.Plaques.Count == 3, $"the board carries the three plaques ({menu.ShownBoard?.Plaques.Count})");
        WalkTo(menu, "CONTINUE");
        Is(ctx, "the CONTINUE row", "Creates the named player, or continues the one that exists", menu.ShownDetail);
        menu.Drive(Accept);
        Is(ctx, "CONTINUE with no name is refused in the original's words",
            menu.Campaign!.Strings.Text(200, "You must enter a player name."), menu.ShownDetail);
        ctx.Check(menu.Campaign.Screen == CampaignScreen.Roster, $"and the roster stands ({menu.Campaign.Screen})");
        WalkTo(menu, "CANCEL");
        Is(ctx, "the CANCEL row", "Back to the main menu", menu.ShownDetail);
        menu.Drive(Accept);
        ctx.Check(menu.ShownScreen == "Mode" && menu.Campaign == null, $"CANCEL closes the flow and lands on the Mode screen ({menu.ShownScreen})");
    }

    private static void CreatePlayer(TestContext ctx, LaunchMenu menu, CampaignProfileStore store)
    {
        WalkTo(menu, LaunchMenu.CampaignRow);
        menu.Drive(Accept);
        var flow = menu.Campaign!;
        menu.Drive(Accept);
        ctx.Check(flow.CapturesText, $"Accept on the field arms it for typing");
        Has(ctx, "the footer names typing", "Type a name", menu.ShownFooter);
        Is(ctx, "the armed field shows a caret", "Name:  _", menu.ShownRowText);
        flow.Type(Pilot);
        menu.Drive(MenuCommands.None);
        Is(ctx, "typed letters land in the field with the caret", "Name:  " + Pilot + "_", menu.ShownRowText);
        // A driven frame carries no pad axis, and the armed field takes its cursor axes from the
        // pad alone, so a Down here moves nothing: the field keeps the keyboard's letters.
        menu.Drive(Down);
        ctx.Check(menu.ShownRow == 0 && flow.CapturesText, $"a keyboard axis does not leave the armed field ({menu.ShownRow})");
        ctx.Check(store.Load(Pilot) == null, $"nothing is written before the commit");
        menu.Drive(Accept);
        Is(ctx, "Accept in the armed field continues onto the cabin", "CAMPAIGN CABIN", menu.ShownHeading);
        ctx.Check(flow.Screen == CampaignScreen.Cabin && flow.Profile?.Name == Pilot, $"the flow seats the new player ({flow.Profile?.Name})");
        string expected = CampaignProfileStore.Serialize(CampaignProfileDef.NewProfile(Pilot));
        ctx.Check(File.ReadAllText(Path.Combine(store.DirFor(Pilot), "profile.json")) == expected,
            $"the store holds exactly a fresh profile under the name");
        Is(ctx, "and remembers the player as last seated", Pilot, store.LastPlayed);
        ctx.Check(menu.ShownRowCount == 4 && menu.ShownRow == 0, $"the cabin's four rows, cursor on Next Mission ({menu.ShownRowCount}, {menu.ShownRow})");
        Is(ctx, "the first row", "Next Mission", menu.ShownRowText);
        Is(ctx, "described", "Opens the briefing for the next mission", menu.ShownDetail);
        menu.Drive(Down);
        Is(ctx, "a fresh profile has no previous missions", "No missions finished yet", menu.ShownDetail);
        menu.Drive(Back);
        ctx.Check(flow.Screen == CampaignScreen.Roster && menu.ShownRowCount == 5, $"Back from the cabin returns to the roster with one row ({flow.Screen}, {menu.ShownRowCount})");
        ctx.Check(menu.ShownRow == 1 && menu.ShownRowText == "✓ " + Pilot, $"the cursor opens on the remembered player's ticked row ({menu.ShownRow}, {menu.ShownRowText})");
        Is(ctx, "described as one press away", "Enter / A again to fly this player's campaign", menu.ShownDetail);
    }

    private static void SecondPlayerAndDelete(TestContext ctx, LaunchMenu menu, CampaignProfileStore store)
    {
        var flow = menu.Campaign!;
        menu.Drive(Up);
        Is(ctx, "the field still carries the name that continued", "Name:  " + Pilot, menu.ShownRowText);
        menu.Drive(Accept);
        Is(ctx, "arming it keeps that name under the caret", "Name:  " + Pilot + "_", menu.ShownRowText);
        while (flow.Backspace())
        {
        }

        flow.Type(Guest);
        menu.Drive(Back);
        ctx.Check(!flow.CapturesText && menu.ShownRowText == "Name:  " + Guest, $"Back disarms the field and keeps the name ({menu.ShownRowText})");
        WalkTo(menu, "CONTINUE");
        menu.Drive(Accept);
        ctx.Check(flow.Screen == CampaignScreen.Cabin && flow.Profile?.Name == Guest && store.Load(Guest) != null,
            $"CONTINUE creates the second player and seats them ({flow.Profile?.Name})");
        string pilotFile = File.ReadAllText(Path.Combine(store.DirFor(Pilot), "profile.json"));
        menu.Drive(Back);
        ctx.Check(menu.ShownRowCount == 6 && menu.ShownRowText == "✓ " + Guest, $"the roster lists both, standing on the newer ({menu.ShownRowCount}, {menu.ShownRowText})");
        WalkTo(menu, "DELETE PLAYER");
        Is(ctx, "the DELETE row", "Removes the named player and its campaign; hangar planes stay", menu.ShownDetail);
        menu.Drive(Accept);
        ctx.Check(menu.ShownRowCount == 2 && menu.ShownRow == 1, $"DELETE PLAYER opens the confirm stage with the cursor on Keep ({menu.ShownRowCount}, {menu.ShownRow})");
        Is(ctx, "the keep answer names the player", "Keep " + Guest, menu.ShownRowText);
        Is(ctx, "over the original's question", flow.Strings.Text(201, "Are you sure you want to delete this player and all associated saved games?"), menu.ShownDetail);
        Has(ctx, "the footer names the answer press", "Answer", menu.ShownFooter);
        menu.Drive(Back);
        ctx.Check(menu.ShownRowCount == 6 && menu.ShownRowText == "DELETE PLAYER" && store.Load(Guest) != null,
            $"Back keeps the player and lands on the delete row ({menu.ShownRowText})");
        menu.Drive(Accept);
        menu.Drive(Up);
        Is(ctx, "the delete answer", "Delete " + Guest, menu.ShownRowText);
        menu.Drive(Accept);
        ctx.Check(store.Load(Guest) == null && !Directory.Exists(store.DirFor(Guest)), $"Accept removes the profile's directory");
        ctx.Check(File.ReadAllText(Path.Combine(store.DirFor(Pilot), "profile.json")) == pilotFile, $"and leaves the other profile byte for byte");
        Is(ctx, "the deleted player was the last seated, so nobody is remembered", string.Empty, store.LastPlayed);
        ctx.Check(menu.ShownRowCount == 5 && menu.ShownRow == 0 && menu.ShownRowText == "Name:  (none)",
            $"the roster is back to one row with the field cleared ({menu.ShownRowCount}, {menu.ShownRowText})");
    }

    private static void FreshCabin(TestContext ctx, LaunchMenu menu, CampaignProfileStore store)
    {
        var flow = menu.Campaign!;
        WalkTo(menu, Pilot);
        menu.Drive(Accept);
        Is(ctx, "the first confirm on a roster row selects it", "✓ " + Pilot, menu.ShownRowText);
        ctx.Check(flow.Screen == CampaignScreen.Roster, $"and stays on the roster ({flow.Screen})");
        menu.Drive(Accept);
        ctx.Check(flow.Screen == CampaignScreen.Cabin && flow.Profile?.Name == Pilot, $"the second continues onto the cabin ({flow.Screen})");
        Is(ctx, "seating records the player again", Pilot, store.LastPlayed);
        menu.Drive(Down);
        menu.Drive(Accept);
        ctx.Check(flow.Screen == CampaignScreen.PreviousMissions && menu.ShownHeading == "PREVIOUS MISSIONS",
            $"Previous Missions opens the table of contents ({flow.Screen})");
        ctx.Check(menu.ShownRowCount == 4 && menu.ShownRowText == "VIEW SELECTED", $"with no mission rows, only the four buttons ({menu.ShownRowCount}, {menu.ShownRowText})");
        menu.Drive(Back);
        ctx.Check(flow.Screen == CampaignScreen.Cabin, $"Back returns to the cabin ({flow.Screen})");
        menu.Drive(Back);
        menu.Drive(Back);
        ctx.Check(menu.ShownScreen == "Mode" && menu.Campaign == null, $"Back twice more leaves the campaign ({menu.ShownScreen})");
    }

    // Three missions flown, the shape the progressed aid store has, written the way the session's
    // director writes a result: through the progression rules and one save.
    private static void Progress(CampaignProfileStore store)
    {
        var profile = store.Load(Pilot)!;
        for (int seq = 0; seq < FlownBefore; seq++)
        {
            CampaignProgression.Record(profile, new MissionAttempt(
                seq, CampaignProgression.PrimaryObjectiveMask, 300_000 + (seq * 20_000), 400, 120,
                profile.Planes[0].Airframe, profile.Planes[0].Name));
        }

        store.Save(profile);
    }

    private static void PreviousMissions(TestContext ctx, LaunchMenu menu, CampaignProfileStore store)
    {
        menu.ShowMenu();
        menu.OpenCampaignCabin(Pilot);
        var flow = menu.Campaign!;
        ctx.Check(menu.ShownScreen == "Campaign" && flow.Screen == CampaignScreen.Cabin && flow.Profile?.MissionsCompleted == FlownBefore,
            $"the cabin return re-reads the profile with its three flights ({flow.Profile?.MissionsCompleted})");
        menu.Drive(Down);
        Is(ctx, "previous missions now offers a review", "Review or replay a finished mission", menu.ShownDetail);
        menu.Drive(Accept);
        ctx.Check(menu.ShownRowCount == FlownBefore + 5 && menu.ShownRow == 0,
            $"three mission rows then VIEW SELECTED, REPLAY MISSION, the arrow, the bookmark and RETURN TO CABIN ({menu.ShownRowCount})");
        Is(ctx, "the first row is the first mission", flow.Strings.Text(3450, "Mission 1"), menu.ShownDetail);
        Has(ctx, "the footer names the view shortcut", "X  View", menu.ShownFooter);
        ctx.Check(menu.ShownBoard is { } board && board.Fills.Count >= 2, $"the focused row draws its wash and outline ({menu.ShownBoard?.Fills.Count})");
        menu.Drive(Accept);
        Is(ctx, "a confirm picks the row", "Selected. Confirm again to replay it", menu.ShownDetail);
        menu.Drive(Down);
        menu.Drive(Secondary);
        ctx.Check(flow.Screen == CampaignScreen.Scrapbook && flow.MissionSeq == 1 && menu.ShownHeading == "SCRAPBOOK",
            $"X on a mission row opens the book on that mission ({flow.Screen}, seq {flow.MissionSeq})");
        Is(ctx, "with the cursor on the way out", "RETURN TO CABIN", menu.ShownRowText);
        menu.Drive(Back);
        ctx.Check(flow.Screen == CampaignScreen.PreviousMissions, $"Back returns to the contents ({flow.Screen})");
        WalkTo(menu, "REPLAY MISSION");
        Is(ctx, "REPLAY MISSION names the picked mission", flow.Strings.Text(3451, "Mission 2"), menu.ShownDetail);
        menu.Drive(Accept);
        ctx.Check(flow.Screen == CampaignScreen.Briefing && flow.MissionSeq == 1, $"and opens its briefing ({flow.Screen}, seq {flow.MissionSeq})");
        menu.Drive(Back);
        ctx.Check(flow.Screen == CampaignScreen.PreviousMissions, $"Back from a replay's briefing returns to the contents ({flow.Screen})");
        WalkTo(menu, "RETURN TO CABIN");
        menu.Drive(Accept);
        ctx.Check(flow.Screen == CampaignScreen.Cabin, $"RETURN TO CABIN lands on the cabin ({flow.Screen})");
        ctx.Check(store.Load(Pilot)?.MissionsCompleted == FlownBefore, $"nothing browsed wrote the profile");
    }

    private static void Briefing(TestContext ctx, LaunchMenu menu)
    {
        var flow = menu.Campaign!;
        WalkTo(menu, "Next Mission");
        menu.Drive(Accept);
        ctx.Check(flow.Screen == CampaignScreen.Briefing && flow.MissionSeq == FlownBefore,
            $"Next Mission opens the briefing of the next story position ({flow.Screen}, seq {flow.MissionSeq})");
        Is(ctx, "headed by the mission's long name", flow.Strings.Text(3450 + FlownBefore, "MISSION BRIEFING"), menu.ShownHeading);
        ctx.Check(menu.ShownRowCount == 3 && menu.ShownRow == 0 && menu.ShownRowText == "REPLAY BRIEFING",
            $"three plaques, the cursor on REPLAY BRIEFING ({menu.ShownRowCount}, {menu.ShownRowText})");
        if (flow.Page is not CampaignBriefingPage page || page.Reveal == null)
        {
            ctx.Note($"the extraction carries no briefing for seq {FlownBefore}; the reveal checks did not run");
            return;
        }

        ctx.Check(page.State != null && page.NarrationWav.Length > 0, $"the mission's state and narration resolved ({page.NarrationWav})");
        int pictures = menu.ShownBoard?.Pictures.Count ?? 0;
        for (int frame = 0; frame < 600; frame++)
        {
            menu._Process(1.0 / 60.0);
        }

        ctx.Check(page.Reveal.Clock > 9.9 && page.NarrationStarts == 1,
            $"ten seconds of frames advance the reveal and start the narration once ({page.Reveal.Clock:0.0}s, {page.NarrationStarts})");
        ctx.Check((menu.ShownBoard?.Pictures.Count ?? 0) > pictures || page.Reveal.RevealedObjectives.Count > 0,
            $"and the reveal put something on the board ({pictures} pictures to {menu.ShownBoard?.Pictures.Count}, {page.Reveal.RevealedObjectives.Count} objectives)");
        menu.Drive(Accept);
        menu._Process(1.0 / 60.0);
        ctx.Check(page.Reveal.Clock < 1.0 && page.NarrationStarts == 2,
            $"REPLAY BRIEFING restarts the reveal and the narration ({page.Reveal.Clock:0.0}s, {page.NarrationStarts})");
        menu.Drive(Down);
        Is(ctx, "the second plaque", "RETURN TO CABIN", menu.ShownRowText);
        menu.Drive(Accept);
        ctx.Check(flow.Screen == CampaignScreen.Cabin, $"RETURN TO CABIN lands on the cabin that opened the briefing ({flow.Screen})");
        menu.Drive(Accept);
        ctx.Check(flow.Screen == CampaignScreen.Briefing && page.Reveal.Clock < 1.0 && page.NarrationStarts == 2,
            $"Next Mission on the same mission reopens the briefing with the reveal where REPLAY left it ({page.Reveal.Clock:0.0}s)");
    }

    private static void FlightCheckAndAmmo(TestContext ctx, LaunchMenu menu, CampaignProfileStore store)
    {
        var flow = menu.Campaign!;
        WalkTo(menu, "GO TO FLIGHT CHECK");
        menu.Drive(Accept);
        ctx.Check(flow.Screen == CampaignScreen.FlightCheck, $"GO TO FLIGHT CHECK opens the flight check ({flow.Screen})");
        Is(ctx, "headed by the mission's name", flow.Strings.Text(3450 + FlownBefore, $"Mission {FlownBefore + 1}"), menu.ShownHeading);
        int slots = flow.MissionHasWingman ? 2 : 1;
        ctx.Check(menu.ShownRowCount == (slots * 2) + 2, $"a heading and CHANGE AMMO per crew slot, no CHANGE PLANE under two planes, then the two plaques ({menu.ShownRowCount}, wingman {flow.MissionHasWingman})");
        ctx.Check(menu.ShownRow == 1 && menu.ShownRowText == "CHANGE AMMO", $"the cursor settles past the pilot heading onto CHANGE AMMO ({menu.ShownRow}, {menu.ShownRowText})");
        Has(ctx, "the pilot heading names the starter", "Gypsy Magic", RowText(menu, 0));
        menu.Drive(Accept);
        ctx.Check(flow.Screen == CampaignScreen.Ammo && flow.AmmoSlot == 0 && menu.ShownHeading == "AMMO SELECTION",
            $"CHANGE AMMO opens ammo selection on the pilot's aircraft ({flow.Screen}, slot {flow.AmmoSlot})");
        ctx.Check(menu.ShownRowCount == 14, $"four gun groups, eight pylons and the two plaques ({menu.ShownRowCount})");
        int group = -1;
        for (int row = 0; row < 4 && group < 0; row++)
        {
            if (flow.Page.Combo(row) != null)
            {
                group = row;
            }
        }

        ctx.Check(group >= 0, $"the starter mounts a gun in some group ({group})");
        if (group < 0)
        {
            return;
        }

        WalkTo(menu, flow.Page.RowText(group));
        Is(ctx, "the group's field reads the stock ammunition", flow.Strings.Text(3360, "Slug"), menu.ShownRowText);
        menu.Drive(Right);
        Is(ctx, "the stepper takes the next ammunition", flow.Strings.Text(3361, "Dum-dum"), menu.ShownRowText);
        ctx.Check(store.Load(Pilot)!.Planes[0].Ammo[group] == 0, $"nothing is written before ACCEPT");
        WalkTo(menu, "ACCEPT LOADOUT");
        menu.Drive(Accept);
        ctx.Check(flow.Screen == CampaignScreen.FlightCheck, $"ACCEPT LOADOUT returns to the flight check ({flow.Screen})");
        ctx.Check(store.Load(Pilot)!.Planes[0].Ammo[group] == 1, $"and the profile file carries the pick");
        string saved = File.ReadAllText(Path.Combine(store.DirFor(Pilot), "profile.json"));
        WalkTo(menu, "CHANGE AMMO");
        menu.Drive(Accept);
        WalkTo(menu, flow.Page.RowText(group));
        menu.Drive(Right);
        Is(ctx, "a second edit steps on", flow.Strings.Text(3362, "Armor-piercing"), menu.ShownRowText);
        WalkTo(menu, "CANCEL LOADOUT");
        menu.Drive(Accept);
        ctx.Check(flow.Screen == CampaignScreen.FlightCheck && File.ReadAllText(Path.Combine(store.DirFor(Pilot), "profile.json")) == saved,
            $"CANCEL LOADOUT returns with the file byte for byte as ACCEPT left it");
        ctx.Check(flow.Profile!.Planes[0].Ammo[group] == 1, $"and the seated profile still reads the accepted pick ({flow.Profile.Planes[0].Ammo[group]})");
    }

    private static void PlaneSelection(TestContext ctx, LaunchMenu menu, CampaignProfileStore store)
    {
        // A third aircraft, since the flight check bars CHANGE PLANE under three.
        var profile = store.Load(Pilot)!;
        profile.Planes.Add(new OwnedPlane { Name = "Test Bird", Airframe = 3 });
        store.Save(profile);
        menu.ShowMenu();
        menu.OpenCampaignCabin(Pilot);
        var flow = menu.Campaign!;
        menu.Drive(Accept);
        WalkTo(menu, "GO TO FLIGHT CHECK");
        menu.Drive(Accept);
        int slots = flow.MissionHasWingman ? 2 : 1;
        ctx.Check(flow.Screen == CampaignScreen.FlightCheck && menu.ShownRowCount == (slots * 3) + 2,
            $"with three planes each crew slot offers CHANGE PLANE too ({menu.ShownRowCount})");
        WalkTo(menu, "CHANGE PLANE");
        menu.Drive(Accept);
        ctx.Check(flow.Screen == CampaignScreen.PlaneSelection && flow.PlaneSlot == 0 && menu.ShownHeading == flow.Strings.Text(3450 + FlownBefore, $"Mission {FlownBefore + 1}"),
            $"CHANGE PLANE opens plane selection on the pilot's slot ({flow.Screen}, slot {flow.PlaneSlot})");
        ctx.Check(menu.ShownRow == 0 && menu.ShownRowCount == (slots * 2) + 2, $"a combo and EXPORT per slot then the two plaques, cursor on the pilot's combo ({menu.ShownRow}, {menu.ShownRowCount})");
        Has(ctx, "the combo reads the pilot's plane", "Gypsy Magic", menu.ShownRowText);
        menu.Drive(Left);
        Has(ctx, "Left wraps the pick onto the new plane", "Test Bird", menu.ShownRowText);
        ctx.Check(flow.Modal == null && store.Load(Pilot)!.SelectedPlane == 0, $"no refusal, and nothing written before ACCEPT");
        WalkTo(menu, "ACCEPT SELECTIONS");
        menu.Drive(Accept);
        ctx.Check(flow.Screen == CampaignScreen.FlightCheck && store.Load(Pilot)!.SelectedPlane == 2,
            $"ACCEPT SELECTIONS writes the pilot's pick into the profile ({store.Load(Pilot)!.SelectedPlane})");
        Has(ctx, "and the pilot heading now names it", "Test Bird", RowText(menu, 0));
        WalkTo(menu, "CHANGE PLANE");
        menu.Drive(Accept);
        menu.Drive(Right);
        Has(ctx, "Right wraps back onto the starter", "Gypsy Magic", menu.ShownRowText);
        WalkTo(menu, "CANCEL SELECTIONS");
        menu.Drive(Accept);
        ctx.Check(flow.Screen == CampaignScreen.FlightCheck && store.Load(Pilot)!.SelectedPlane == 2 && flow.Profile!.SelectedPlane == 2,
            $"CANCEL SELECTIONS keeps the accepted pick ({store.Load(Pilot)!.SelectedPlane})");
    }

    private static void HangarDoor(TestContext ctx, LaunchMenu menu, CampaignProfileStore store)
    {
        var flow = menu.Campaign!;
        WalkTo(menu, "RETURN TO BRIEFING");
        menu.Drive(Accept);
        ctx.Check(flow.Screen == CampaignScreen.Briefing, $"RETURN TO BRIEFING returns to the briefing ({flow.Screen})");
        WalkTo(menu, "RETURN TO CABIN");
        menu.Drive(Accept);
        WalkTo(menu, "Plane Construction");
        Is(ctx, "the cabin's third row", "Buy, sell and fit aircraft", menu.ShownDetail);
        menu.Drive(Accept);
        ctx.Check(menu.ShownScreen == "Hangar" && menu.Hangar is { Campaign: not null, Screen: HangarScreen.PlaneSelection },
            $"PLANE CONSTRUCTION opens the hangar over the profile's wallet ({menu.ShownScreen})");
        ctx.Check(menu.Hangar!.Campaign!.Funds == store.Load(Pilot)!.Funds && menu.Hangar.Saved.Count == 3,
            $"the wallet is the profile's and the roster its three planes ({menu.Hangar.Campaign.Funds}, {menu.Hangar.Saved.Count})");
        ctx.Check(ReferenceEquals(menu.Campaign, flow) && flow.Exit == CampaignExit.OpenHangar, $"the campaign flow stands behind the door ({flow.Exit})");
        menu.Drive(Back);
        ctx.Check(menu.ShownScreen == "Campaign" && menu.Hangar == null && ReferenceEquals(menu.Campaign, flow) && flow.Screen == CampaignScreen.Cabin && flow.Exit == CampaignExit.None,
            $"Back closes the hangar and resumes the cabin ({menu.ShownScreen}, {flow.Screen})");
        Is(ctx, "on the row that opened it", "Plane Construction", menu.ShownRowText);
    }

    private static void FlyMission(TestContext ctx, LaunchMenu menu, CampaignProfileStore store, List<MenuExit> exits)
    {
        var flow = menu.Campaign!;
        WalkTo(menu, "Next Mission");
        menu.Drive(Accept);
        WalkTo(menu, "GO TO FLIGHT CHECK");
        menu.Drive(Accept);
        WalkTo(menu, "FLY MISSION");
        var profile = flow.Profile!;
        menu.Drive(Accept);
        ctx.Check(exits.Count == 1 && exits[0] is CampaignMissionExit, $"FLY MISSION leaves as one campaign mission exit ({exits.Count})");
        if (exits.Count != 1 || exits[0] is not CampaignMissionExit exit)
        {
            return;
        }

        ctx.Check(exit.Profile == Pilot && exit.MissionSeq == FlownBefore && exit.Seats.Count == 1,
            $"for the seated profile at its next story position with one seat ({exit.Profile}, {exit.MissionSeq}, {exit.Seats.Count})");
        var seat = exit.Seats[0];
        ctx.Check(seat.PlaneNode == PlanePickerRoster.AirframeNode(3) && seat.Fit != null && seat.Custom == null && seat.Pads.Count == 0,
            $"seat 0 flies the picked Bloodhawk's node with its campaign fit, no build and no pad ({seat.PlaneNode}, fit {seat.Fit != null}, custom {seat.Custom != null})");
        ctx.Check(menu.Campaign == null && menu.ShownScreen == "Mode", $"the flow is closed and the screen stands on Mode for the return ({menu.ShownScreen})");
        ctx.Check(File.ReadAllText(Path.Combine(store.DirFor(Pilot), "profile.json")) == CampaignProfileStore.Serialize(profile),
            $"the profile was saved as it stood on the press");
    }

    private static void DebriefReturn(TestContext ctx, LaunchMenu menu, CampaignProfileStore store)
    {
        // The session's director records the flown mission and saves; the menu comes back on it.
        var profile = store.Load(Pilot)!;
        CampaignProgression.Record(profile, new MissionAttempt(
            FlownBefore, CampaignProgression.PrimaryObjectiveMask, 420_000, 200, 90, 3, "Test Bird"));
        store.Save(profile);
        menu.ShowMenu();
        menu.OpenCampaignScrapbook(Pilot, FlownBefore);
        var flow = menu.Campaign!;
        ctx.Check(menu.ShownScreen == "Campaign" && flow.Screen == CampaignScreen.Scrapbook && flow.MissionSeq == FlownBefore,
            $"the debrief return opens the book on the flown mission ({flow.Screen}, seq {flow.MissionSeq})");
        ctx.Check(flow.Profile?.MissionsCompleted == FlownBefore + 1, $"over the profile as the mission wrote it ({flow.Profile?.MissionsCompleted})");
        Is(ctx, "headed as the book", "SCRAPBOOK", menu.ShownHeading);
        Is(ctx, "with the cursor on the way out", "RETURN TO CABIN", menu.ShownRowText);
        ctx.Check(flow.Page.RowText(0) == "REPLAY MISSION", $"the results page offers REPLAY MISSION first ({flow.Page.RowText(0)})");
        ctx.Check(menu.ShownBoard is { } board && HasLine(board, "Mission Completed") && HasLine(board, "$" + profile.MissionResults[^1].Latest.Money),
            $"the results card reads the flown mission's outcome and cash");
        int prev = ButtonRow(flow, BoardButton.ScrapbookPrev);
        ctx.Check(prev >= 0, $"the page carries its back arrow ({prev})");
        if (prev >= 0)
        {
            WalkToRow(menu, prev);
            menu.Drive(Accept);
            bool hasSpreads = ScrapbookComposition.Items(ctx.DataRoot, FlownBefore, 1).Count > 0;
            if (hasSpreads)
            {
                ctx.Check(flow.Screen == CampaignScreen.Scrapbook && flow.Page.Button(menu.ShownRow).Button == BoardButton.ScrapbookPrev,
                    $"the arrow turns back a page and keeps the cursor on itself ({flow.Screen}, row {menu.ShownRow})");
                int next = ButtonRow(flow, BoardButton.ScrapbookNext);
                ctx.Check(next >= 0, $"and the forward arrow is offered again ({next})");
                WalkToRow(menu, next);
                menu.Drive(Accept);
                ctx.Check(flow.Page.RowText(0) == "REPLAY MISSION", $"forward returns to the results page ({flow.Page.RowText(0)})");
            }
            else
            {
                ctx.Check(flow.Screen == CampaignScreen.PreviousMissions, $"with no earlier spread the arrow falls into the mission overview ({flow.Screen})");
                WalkTo(menu, flow.Strings.Text(1200, "Current Mission"));
                menu.Drive(Accept);
            }
        }

        WalkTo(menu, "RETURN TO CABIN");
        menu.Drive(Accept);
        ctx.Check(flow.Screen == CampaignScreen.Cabin && menu.ShownHeading == "CAMPAIGN CABIN", $"RETURN TO CABIN lands on the cabin ({flow.Screen})");
        menu.Drive(Back);
        ctx.Check(flow.Screen == CampaignScreen.Roster, $"Back from the cabin behind the book returns to the roster ({flow.Screen})");
        menu.Drive(Back);
        ctx.Check(menu.ShownScreen == "Mode" && menu.Campaign == null, $"and Back once more leaves the campaign ({menu.ShownScreen})");
    }

    private static void GuestCheck(TestContext ctx, LaunchMenu menu)
    {
        menu.ShowMenu("campaign-guestcheck:2");
        menu.DebugJoin(3);
        var flow = menu.Campaign!;
        ctx.Check(flow.Screen == CampaignScreen.FlightCheck && flow.Field.Players == 4 && flow.Field.Current == 2 && flow.Field.Locked,
            $"the guest-check aid walks a four-player field to P3's check ({flow.Field.Players}, current {flow.Field.Current})");
        ctx.Check(menu.ShownBoard is { } board && HasLine(board, "FLIGHT CHECK P3"), $"headed for the guest");
        ctx.Check(menu.ShownRowCount == 5 && menu.ShownRow == 1 && menu.ShownRowText == "CHANGE AMMO",
            $"a guest's check is one PILOT block with CHANGE AMMO and CHANGE PLANE, then the two plaques ({menu.ShownRowCount}, {menu.ShownRowText})");
        menu.Drive(Back);
        ctx.Check(flow.Field.Current == 1 && flow.Screen == CampaignScreen.FlightCheck, $"Back retreats to P2's check ({flow.Field.Current})");
        ctx.Check(menu.ShownBoard is { } second && HasLine(second, "FLIGHT CHECK P2"), $"headed for that guest");
        WalkTo(menu, "FLY MISSION");
        menu.Drive(Accept);
        ctx.Check(flow.Field.Current == 2 && menu.ShownRowText == "CHANGE AMMO",
            $"FLY MISSION on a guest's check advances to the next, the cursor opening afresh ({flow.Field.Current}, {menu.ShownRowText})");
        WalkTo(menu, "FLY MISSION");
        menu.Drive(Accept);
        ctx.Check(flow.Field.Current == 3 && flow.Screen == CampaignScreen.FlightCheck, $"and again onto P4's ({flow.Field.Current})");
    }

    private static void Aids(TestContext ctx, LaunchMenu menu)
    {
        menu.ShowMenu("campaign-empty");
        ctx.Check(menu.Campaign is { Screen: CampaignScreen.Roster, Roster.Count: 0 } && menu.ShownRowCount == 4, $"--menu=campaign-empty opens an empty roster ({menu.ShownRowCount})");
        menu.ShowMenu("campaign-roster");
        // ⚠ Pinned as it behaves, not as the inventory describes it: the aid seeds two profiles and
        // then seats the first before branching on its name, so the screen it shows is the cabin.
        ctx.Check(menu.Campaign is { Screen: CampaignScreen.Cabin, Roster.Count: 2, Profile.Name: Pilot },
            $"--menu=campaign-roster seeds two profiles and lands on the first one's cabin ({menu.Campaign?.Screen}, {menu.Campaign?.Roster.Count})");
        menu.ShowMenu("campaign-entry");
        ctx.Check(menu.Campaign is { Screen: CampaignScreen.Roster, CapturesText: true } && menu.ShownRowText == "Name:  Zachary_", $"--menu=campaign-entry opens the roster mid-entry ({menu.ShownRowText})");
        menu.ShowMenu("campaign-cabin");
        ctx.Check(menu.Campaign is { Screen: CampaignScreen.Cabin } && menu.Campaign.Profile?.MissionsCompleted == FlownBefore, $"--menu=campaign-cabin opens the progressed cabin ({menu.Campaign?.Profile?.MissionsCompleted})");
        menu.ShowMenu("campaign-previous");
        ctx.Check(menu.Campaign is { Screen: CampaignScreen.PreviousMissions } && menu.ShownRowCount == FlownBefore + 5, $"--menu=campaign-previous opens the contents with three flights ({menu.ShownRowCount})");
        menu.ShowMenu("campaign-scrapbook");
        ctx.Check(menu.Campaign is { Screen: CampaignScreen.Scrapbook, MissionSeq: FlownBefore - 1 } && menu.ShownRowText == "RETURN TO CABIN", $"--menu=campaign-scrapbook opens the book on the last flown mission ({menu.Campaign?.MissionSeq})");
        menu.ShowMenu("campaign-briefing:24");
        ctx.Check(menu.Campaign is { Screen: CampaignScreen.Briefing, MissionSeq: FlownBefore } && menu.Campaign.Page is CampaignBriefingPage { Reveal: { Clock: > 23.9 } },
            $"--menu=campaign-briefing:24 opens the briefing advanced to 24 s ({(menu.Campaign?.Page as CampaignBriefingPage)?.Reveal?.Clock:0.0})");
        menu.ShowMenu("campaign-flightcheck");
        ctx.Check(menu.Campaign is { Screen: CampaignScreen.FlightCheck, MissionSeq: FlownBefore } && menu.ShownRowText == "CHANGE AMMO", $"--menu=campaign-flightcheck opens the flight check ({menu.ShownRowText})");
        menu.ShowMenu("campaign-ammo");
        ctx.Check(menu.Campaign is { Screen: CampaignScreen.Ammo, AmmoSlot: 0 } && menu.ShownHeading == "AMMO SELECTION", $"--menu=campaign-ammo opens ammo selection ({menu.ShownHeading})");
        menu.ShowMenu("campaign-planeselection");
        ctx.Check(menu.Campaign is { Screen: CampaignScreen.PlaneSelection, PlaneSlot: 0 } && menu.ShownRow == 0, $"--menu=campaign-planeselection opens plane selection on the pilot ({menu.ShownRow})");
        menu.ShowMenu("campaign-hangar");
        menu.Drive(MenuCommands.None);
        ctx.Check(menu.ShownScreen == "Hangar" && menu.Hangar is { Campaign: not null } && menu.Campaign is { Screen: CampaignScreen.Cabin },
            $"--menu=campaign-hangar opens the hangar over the scratch profile's wallet with the cabin behind it ({menu.ShownScreen})");
    }

    private static bool HasLine(ComposedBoard board, string text)
    {
        foreach (var line in board.Lines)
        {
            if (line.Text == text)
            {
                return true;
            }
        }

        return false;
    }

    private static int ButtonRow(CampaignFlow flow, BoardButton button)
    {
        for (int row = 0; row < flow.Page.RowCount; row++)
        {
            if (flow.Page.Button(row).Button == button)
            {
                return row;
            }
        }

        return -1;
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

    private static void WalkToRow(LaunchMenu menu, int row)
    {
        int count = menu.ShownRowCount;
        for (int i = 0; i < count && menu.ShownRow != row; i++)
        {
            menu.Drive(Down);
        }
    }

    // The text of an absolute row, read off the page: the heading rows are not focusable, so the
    // cursor cannot be walked onto them.
    private static string RowText(LaunchMenu menu, int row) => menu.Campaign?.Page.RowText(row) ?? string.Empty;

    private static void Is(TestContext ctx, string what, string expected, string actual) =>
        ctx.Check(expected == actual, $"{what}: expected '{expected}', got '{actual}'");

    private static void Has(TestContext ctx, string what, string expected, string actual) =>
        ctx.Check(actual.Contains(expected, StringComparison.Ordinal),
            $"{what}: expected '{expected}' in '{actual}'");
}
