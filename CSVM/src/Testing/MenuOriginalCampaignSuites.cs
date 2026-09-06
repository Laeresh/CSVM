using System;
using System.Collections.Generic;
using System.IO;
using CSVM.Flight;
using CSVM.Session;
using CSVM.UI;
using CSVM.UI.Menu;
using CSVM.UI.Menu.BuiltIn;
using CSVM.UI.Menu.Original;

namespace CSVM.Testing;

/// <summary>
/// The Original campaign through the presentation boundary over the install's own decoded layout
/// and a scratch profile store: a real <see cref="MenuHost"/> with Original selected, the Campaign
/// row clicked, a player typed and continued onto the cabin, the briefing's reveal driven on the
/// presentation's clock with its narration asserted through the host's audio (begun on entry,
/// begun again on REPLAY BRIEFING, ended on every door out), the flight check with two debug-joined
/// guests walked to FLY MISSION and the launch reaching the sink as one
/// <see cref="CampaignMissionExit"/>, the debrief return landing on the book and its way back to
/// the cabin, the cabin return, ammo selection and plane selection committing through the feature,
/// the hangar round trip over the profile's wallet, and Deactivate leaving no open campaign.
/// ⚠ Nothing here touches <c>user://Profiles</c>: the presentation is pointed at a scratch store.
/// </summary>
internal static class MenuOriginalCampaignSuites
{
    private const float Dt = 1f / 60f;
    private const string Pilot = "Zachary";

    private static readonly MenuCommands Accept = new() { Accept = true };
    private static readonly MenuCommands Back = new() { Back = true };
    private static readonly MenuCommands Down = new() { MoveY = 1 };
    private static readonly MenuCommands Right = new() { MoveX = 1 };
    private static readonly MenuCommands Left = new() { MoveX = -1 };

    [Suite("menu-original-campaign",
        "Original's campaign through the presentation boundary over the install's decoded layout and "
        + "a scratch profile store: the Campaign row opens the profile screen, a character outside "
        + "the name rule and one inside it at the cap both type nothing and cue the box's reject "
        + "sound, typed frames name a "
        + "player and Enter seats them on the cabin, the briefing runs its reveal on the presentation's "
        + "clock and starts its narration through the host's audio once, REPLAY BRIEFING starts it "
        + "again, RETURN TO CABIN ends it and lifts the duck, NEXT MISSION again reopens the briefing "
        + "from a blank map with the narration starting over, the flight check walks two debug-joined "
        + "guests and FLY MISSION leaves as one CampaignMissionExit with three seats, the debrief "
        + "return lands on the book with RETURN TO CABIN focused and starts no narration, a pointer "
        + "press on the book's unselected results tab switches the half the card reads and a press "
        + "on the one it swapped with reads Most Recent again, the cabin "
        + "return lands on the cabin, ammo selection and plane selection write their picks through "
        + "the feature, a wheel step over the scrapbook's contents page and over Plane Construction's "
        + "decal list moves each window one row and clamps at the head while a drag down each thumb's "
        + "track lands it on the last row and opens nothing, PLANE CONSTRUCTION opens the hangar over "
        + "the wallet and Back resumes the cabin, a plane built over the campaign's wallet is absent "
        + "from the sortie roster until one EXPORT press crosses it, and Deactivate leaves no open "
        + "campaign")]
    internal static void MenuOriginalCampaign(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        string root = Path.Combine(ctx.ScratchDir, "menu-original-campaign");
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }

        var store = new CampaignProfileStore(Path.Combine(root, "Profiles"));
        var exits = new List<MenuExit>();
        var audio = new RecordingAudio();
        var seat = new ScriptedSeat();
        var registry = new PresentationRegistry();
        registry.Register(PresentationId.BuiltIn, () => new BuiltInPresentation(
            ctx.Host, ctx.ZrdrPath, ctx.DataRoot, string.Empty, new MenuInput { Keyboard = true }));
        registry.Register(PresentationId.Original, () => new OriginalPresentation(
            ctx.Host, ctx.DataRoot, layout, string.Empty, new MenuInput { Keyboard = true })
        {
            CampaignProfiles = store,
        });
        var host = new MenuHost(registry, audio, exits.Add);
        MenuSuiteHost.AddFeatures(host, ctx.DataRoot);
        host.AddSeat(seat);
        var campaign = host.Features.Get<CampaignFeature>();
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
            Roster(ctx, host, seat, shell, fit, campaign, store, audio);
            Briefing(ctx, host, seat, shell, fit, campaign, audio);
            FlightCheckAndLaunch(ctx, host, seat, shell, fit, campaign, store, exits, audio);
            Returns(ctx, host, shell, fit, campaign, store, audio);
            AmmoAndPlanes(ctx, host, seat, shell, fit, campaign, store);
            Scrolling(ctx, host, seat, shell, fit, store);
            HangarRoundTrip(ctx, host, seat, shell, fit);
            ExportGate(ctx, host, seat, shell, fit, campaign, root);
        }
        finally
        {
            host.Deactivate();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }

        ctx.Check(host.Active == null && !campaign.IsOpen, $"Deactivate leaves no presentation and no open campaign (open={campaign.IsOpen})");
    }

    // The Campaign row under the pointer, the empty profile screen, a typed name, Enter onto the cabin.
    private static void Roster(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit,
        CampaignFeature campaign, CampaignProfileStore store, RecordingAudio audio)
    {
        var door = Row(shell, OriginalShell.CampaignKey);
        ctx.Check(door is { Enabled: true }, $"the top level's Campaign row is live over the feature and the store");
        if (door == null)
        {
            return;
        }

        audio.Cues.Clear();
        Press(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.CampaignRoster && campaign.IsOpen && ReferenceEquals(campaign.Store, store),
            $"a click on Campaign opens the profile screen over the scratch store ({shell.Screen}, open={campaign.IsOpen})");
        ctx.Check(audio.Cues.Contains(OriginalCues.Click), $"with a click cue ({string.Join(",", audio.Cues)})");
        ctx.Check(shell.Rows.Count == 4 && shell.FocusedKey == "ROW:0" && campaign.Roster.Count == 0,
            $"an empty roster is the name box and three plaques, focus on the box ({shell.Rows.Count}, {shell.FocusedKey})");
        ctx.Check(host.Seats[0].CapturingText, $"the seat captures text on the profile screen");
        RefusedCharacters(ctx, host, seat, shell, audio);
        audio.Cues.Clear();
        Press(host, seat, new MenuCommands { Typed = Pilot });
        ctx.Check(shell.RosterName == Pilot && audio.Cues.Count == Pilot.Length && audio.Cues[0] == OriginalCues.Text,
            $"typed frames fill the box and cue the keystroke sound per character ({shell.RosterName}, {audio.Cues.Count})");
        ctx.Check(store.Load(Pilot) == null, $"nothing is written before the commit");
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.CampaignCabin && campaign.Profile?.Name == Pilot,
            $"Enter in the box seats the new player on the cabin ({shell.Screen}, {campaign.Profile?.Name})");
        ctx.Check(File.ReadAllText(Path.Combine(store.DirFor(Pilot), "profile.json")) == CampaignProfileStore.Serialize(CampaignProfileDef.NewProfile(Pilot))
            && store.LastPlayed == Pilot, $"the store holds exactly a fresh profile and the last-played record");
        ctx.Check(!host.Seats[0].CapturingText, $"and the seat no longer captures text on the cabin");
        ctx.Check(shell.Rows.Count == 4 && shell.FocusedKey == "NextMission", $"the cabin's four plaques, focus on NEXT MISSION ({shell.FocusedKey})");
        Press(host, seat, Down);
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.CampaignPreviousMissions && shell.Rows.Count == 4,
            $"PREVIOUS MISSIONS opens the contents, four buttons with nothing flown ({shell.Screen}, {shell.Rows.Count})");
        Press(host, seat, Back);
        ctx.Check(shell.Screen == OriginalScreen.CampaignCabin, $"Back returns to the cabin ({shell.Screen})");
    }

    // Both routes to the box's reject cue, on the empty box so the screen is left as it was found:
    // a character outside the campaign's name rule, and one inside it arriving at a box at its cap.
    // The punctuation route needs the poller to type the character at all (MenuInput.TypeableKeys),
    // which is what a key that produced neither a letter nor a sound used to be missing.
    private static void RefusedCharacters(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, RecordingAudio audio)
    {
        audio.Cues.Clear();
        Press(host, seat, new MenuCommands { Typed = "/" });
        ctx.Check(shell.RosterName.Length == 0 && audio.Cues.Count == 1 && audio.Cues[0] == OriginalCues.TextError,
            $"a character the name rule refuses types nothing and cues the reject sound ({shell.RosterName}, {string.Join(",", audio.Cues)})");

        audio.Cues.Clear();
        Press(host, seat, new MenuCommands { Typed = new string('A', CampaignFeature.MaxNameLength + 1) });
        ctx.Check(shell.RosterName.Length == CampaignFeature.MaxNameLength
            && audio.Cues.Count == CampaignFeature.MaxNameLength + 1
            && audio.Cues[audio.Cues.Count - 1] == OriginalCues.TextError,
            $"an accepted character at the cap cues the same reject ({shell.RosterName.Length}, {audio.Cues.Count})");

        for (int i = 0; i < CampaignFeature.MaxNameLength; i++)
        {
            Press(host, seat, new MenuCommands { Erase = true });
        }

        ctx.Check(shell.RosterName.Length == 0, $"and Backspace empties the box again ({shell.RosterName})");
    }

    // The briefing: the reveal on the presentation's clock, the narration through the host's audio.
    private static void Briefing(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit,
        CampaignFeature campaign, RecordingAudio audio)
    {
        var next = Row(shell, "NextMission");
        if (next == null)
        {
            return;
        }

        audio.Reset();
        Press(host, seat, Pointer(fit, next.X + 5f, next.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.CampaignBriefing && campaign.MissionSeq == 0,
            $"NEXT MISSION opens the briefing of the next story position ({shell.Screen}, seq {campaign.MissionSeq})");
        ctx.Check(shell.Rows.Count == 3 && shell.FocusedKey == "ReplayBriefing", $"three plaques, focus on REPLAY BRIEFING ({shell.FocusedKey})");
        if (campaign.Briefing is not { Reveal: not null } briefing)
        {
            ctx.Note($"the extraction carries no briefing for seq 0; the reveal and narration checks did not run");
            return;
        }

        int pictures = shell.Compose().Pictures.Count;
        int freshElements = briefing.Reveal.Elements.Count;
        for (int frame = 0; frame < 600; frame++)
        {
            host.Tick(Dt);
        }

        ctx.Check(briefing.Reveal.Clock > 9.9 && briefing.NarrationStarts == 1,
            $"ten seconds of ticks advance the reveal and the script asks for its narration once ({briefing.Reveal.Clock:0.0}s, {briefing.NarrationStarts})");
        ctx.Check(audio.Begins == 1 && audio.LastWav == briefing.NarrationWav && audio.Ends == 0,
            $"the presentation began the narration once through the host's audio with the mission's wav and ended nothing ({audio.Begins}, {audio.LastWav}, {audio.Ends})");
        ctx.Check(shell.Compose().Pictures.Count > pictures || briefing.Reveal.RevealedObjectives.Count > 0 || !briefing.Complete,
            $"and the reveal is drawing: more pictures, a revealed objective, or still running ({pictures} pictures to {shell.Compose().Pictures.Count}, {briefing.Reveal.RevealedObjectives.Count} objectives, complete {briefing.Complete})");

        Press(host, seat, Accept);
        host.Tick(Dt);
        ctx.Check(briefing.Reveal.Clock < 1.0 && briefing.NarrationStarts == 2 && audio.Begins == 2,
            $"REPLAY BRIEFING restarts the reveal and the narration begins again ({briefing.Reveal.Clock:0.0}s, {briefing.NarrationStarts}, begins {audio.Begins})");

        // The reveal is driven on until the map is no longer blank, so the return below has
        // something to start over from; the first beat's time is the mission's own.
        int frames = 0;
        while (frames++ < 3600 && briefing.Reveal.Elements.Count == freshElements && briefing.Reveal.RevealedObjectives.Count == 0)
        {
            host.Tick(Dt);
        }

        ctx.Check(briefing.Reveal.Elements.Count > freshElements || briefing.Reveal.RevealedObjectives.Count > 0,
            $"a minute after REPLAY at most places more on the map than a fresh reveal or reveals an objective ({briefing.Reveal.Clock:0.0}s, {freshElements} to {briefing.Reveal.Elements.Count} elements, {briefing.Reveal.RevealedObjectives.Count} objectives)");

        Press(host, seat, Down);
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.CampaignCabin && audio.Ends == 1,
            $"RETURN TO CABIN mid-narration ends it, which lifts the duck ({shell.Screen}, ends {audio.Ends})");
        host.Tick(Dt);
        ctx.Check(audio.Ends == 1 && audio.Begins == 2, $"and a frame on the cabin starts nothing and ends nothing again ({audio.Begins}, {audio.Ends})");

        Press(host, seat, Accept);
        host.Tick(Dt);
        ctx.Check(shell.Screen == OriginalScreen.CampaignBriefing && briefing.Reveal.Clock < 1.0
            && briefing.Reveal.Elements.Count == freshElements && briefing.Reveal.RevealedObjectives.Count == 0
            && briefing.NarrationStarts == 3 && audio.Begins == 3,
            $"NEXT MISSION on the same mission reopens the briefing from a blank map, the script asking for its narration again and the voice beginning again ({briefing.Reveal.Clock:0.0}s, {briefing.Reveal.Elements.Count} elements, {briefing.Reveal.RevealedObjectives.Count} objectives, {briefing.NarrationStarts}, begins {audio.Begins})");
    }

    // GO TO FLIGHT CHECK ends the narration; two device-less guests join, FLY MISSION walks their
    // checks and the last press launches.
    private static void FlightCheckAndLaunch(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit,
        CampaignFeature campaign, CampaignProfileStore store, List<MenuExit> exits, RecordingAudio audio)
    {
        var go = Row(shell, "GoToFlightCheck");
        if (go == null)
        {
            ctx.Check(false, $"the briefing carries GO TO FLIGHT CHECK");
            return;
        }

        int endsBefore = audio.Ends;
        Press(host, seat, Pointer(fit, go.X + 5f, go.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.CampaignFlightCheck && audio.Ends == endsBefore + 1,
            $"GO TO FLIGHT CHECK opens the check and ends the narration ({shell.Screen}, ends {audio.Ends})");
        ctx.Check(shell.FocusedKey == "ChangeAmmo" && Row(shell, "FlyMission") != null,
            $"focus opens on CHANGE AMMO with FLY MISSION offered ({shell.FocusedKey})");

        var setup = host.Features.Get<PlayerSetupFeature>();
        var guest1 = setup.Join(new MenuIdleSource());
        var guest2 = setup.Join(new MenuIdleSource());
        host.Tick(Dt);
        ctx.Check(campaign.Field.Players == 3 && campaign.Field.Current == 0,
            $"two debug-joined seats put three humans on the field, the seated player's check showing ({campaign.Field.Players}, {campaign.Field.Current})");

        var fly = Row(shell, "FlyMission")!;
        Press(host, seat, Pointer(fit, fly.X + 5f, fly.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.CampaignFlightCheck && campaign.Field.Current == 1 && campaign.Field.Locked,
            $"FLY MISSION on the seated player's check advances to P2's ({campaign.Field.Current}, locked {campaign.Field.Locked})");
        ctx.Check(HasLine(shell.Compose(), "FLIGHT CHECK P2"), $"headed for the guest");
        Press(host, seat, Back);
        ctx.Check(campaign.Field.Current == 0, $"Back retreats to the seated player's check ({campaign.Field.Current})");
        fly = Row(shell, "FlyMission")!;
        Press(host, seat, Pointer(fit, fly.X + 5f, fly.Y + 5f, pressed: true, clicked: true));
        fly = Row(shell, "FlyMission")!;
        Press(host, seat, Pointer(fit, fly.X + 5f, fly.Y + 5f, pressed: false, clicked: false));
        Press(host, seat, Pointer(fit, fly.X + 5f, fly.Y + 5f, pressed: true, clicked: true));
        ctx.Check(campaign.Field.Current == 2, $"and again onto P3's ({campaign.Field.Current})");
        fly = Row(shell, "FlyMission")!;
        var profile = campaign.Profile!;
        Press(host, seat, Pointer(fit, fly.X + 5f, fly.Y + 5f, pressed: false, clicked: false));
        Press(host, seat, Pointer(fit, fly.X + 5f, fly.Y + 5f, pressed: true, clicked: true));
        ctx.Check(exits.Count == 1 && exits[0] is CampaignMissionExit, $"the last check's FLY MISSION leaves through the host as one CampaignMissionExit ({exits.Count})");
        if (exits.Count == 1 && exits[0] is CampaignMissionExit exit)
        {
            ctx.Check(exit.Profile == Pilot && exit.MissionSeq == 0 && exit.Seats.Count == 3,
                $"for the seated profile at seq 0 with three seats ({exit.Profile}, {exit.MissionSeq}, {exit.Seats.Count})");
            ctx.Check(exit.Seats[0].PlaneNode == PlanePickerRoster.AirframeNode(profile.Planes[0].Airframe) && exit.Seats[0].Fit != null,
                $"seat 0 flies the profile's plane with its campaign fit ({exit.Seats[0].PlaneNode})");
            ctx.Check(exit.Seats[1].Pads.Count == 0 && exit.Seats[2].Pads.Count == 0, $"the device-less guests carry no pads");
        }

        ctx.Check(!host.Shown && Godot.Input.MouseMode == Godot.Input.MouseModeEnum.Visible,
            $"the host hid the presentation on the exit and the OS pointer is back (shown={host.Shown})");
        ctx.Check(File.ReadAllText(Path.Combine(store.DirFor(Pilot), "profile.json")) == CampaignProfileStore.Serialize(profile),
            $"the profile was saved as it stood on the press");
        if (guest1 != null)
        {
            setup.Unjoin(guest1);
        }

        if (guest2 != null)
        {
            setup.Unjoin(guest2);
        }

        seat.Clear();
    }

    // The session's director records the flown mission; the two returns map onto the book and the cabin.
    private static void Returns(TestContext ctx, MenuHost host, OriginalShell shell, BoardFit fit, CampaignFeature campaign,
        CampaignProfileStore store, RecordingAudio audio)
    {
        var profile = store.Load(Pilot)!;
        CampaignProgression.Record(profile, new MissionAttempt(
            0, CampaignProgression.PrimaryObjectiveMask, 420_000, 200, 90, profile.Planes[0].Airframe, profile.Planes[0].Name));
        store.Save(profile);
        int begins = audio.Begins;
        host.Show(new DebriefReturn(Pilot, 0));
        host.Tick(Dt);
        ctx.Check(shell.Screen == OriginalScreen.CampaignScrapbook && campaign.MissionSeq == 0 && campaign.ScrapbookEntry == 1,
            $"the debrief return opens the book on the flown mission ({shell.Screen}, seq {campaign.MissionSeq})");
        ctx.Check(campaign.Profile?.MissionsCompleted == 1, $"over the profile as the mission wrote it ({campaign.Profile?.MissionsCompleted})");
        ctx.Check(shell.FocusedKey == "ReturnToCabin", $"with the focus on the way out ({shell.FocusedKey})");
        ctx.Check(HasLine(shell.Compose(), "Mission Completed"), $"the results card reads the flown mission's outcome");
        ctx.Check(audio.Begins == begins, $"a return from flight starts no narration ({audio.Begins})");
        var back = Row(shell, "ReturnToCabin");
        ctx.Check(back != null, $"the page carries RETURN TO CABIN");
        var seat0 = (ScriptedSeat)host.Seats[0];
        Tabs(ctx, host, seat0, shell, fit, back);
        Press(host, seat0, Accept);
        ctx.Check(shell.Screen == OriginalScreen.CampaignCabin, $"RETURN TO CABIN lands on the cabin ({shell.Screen})");
        Press(host, seat0, Back);
        ctx.Check(shell.Screen == OriginalScreen.CampaignRoster && campaign.IsOpen, $"Back from the cabin returns to the profile screen ({shell.Screen})");
        Press(host, seat0, Back);
        ctx.Check(shell.Screen == OriginalScreen.TopLevel && !campaign.IsOpen, $"and Back once more leaves the campaign ({shell.Screen}, open={campaign.IsOpen})");

        host.Show(new CabinReturn(Pilot));
        ctx.Check(shell.Screen == OriginalScreen.CampaignCabin && campaign.Profile?.Name == Pilot,
            $"the cabin return lands on the cabin with the profile seated ({shell.Screen}, {campaign.Profile?.Name})");
    }

    // The results card's two tabs under the pointer: the tab the card is not showing draws as a
    // picture rather than a plaque, so its rectangle is the page's own art. Pressing it switches
    // the half the card reads, pressing the one it swapped with puts the book back as the debrief
    // return left it, and the pointer over RETURN TO CABIN hands the focus back to the way out.
    private static void Tabs(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit, OriginalRow? back)
    {
        var tab = UnselectedTab(shell);
        ctx.Check(tab != null && tab.Kind == OriginalRowKind.ListRow && tab.Width > 0f && tab.Height > 0f,
            $"the book's unselected tab carries a rectangle ({tab?.Key}, {tab?.Width}x{tab?.Height})");
        if (tab == null || back == null)
        {
            return;
        }

        Press(host, seat, Pointer(fit, tab.X + 4f, tab.Y + 4f, pressed: true, clicked: true));
        ctx.Check(Row(shell, "BestTab") != null && Row(shell, "MostTab") == null,
            $"a press on it puts Best to Date on the card ({shell.FocusedKey})");
        if (UnselectedTab(shell) is { } other)
        {
            Press(host, seat, Pointer(fit, other.X + 4f, other.Y + 4f, pressed: true, clicked: true));
            ctx.Check(Row(shell, "MostTab") != null, $"and a press on the other reads Most Recent again");
        }

        Press(host, seat, Pointer(fit, back.X + 4f, back.Y + 4f));
        ctx.Check(shell.FocusedKey == "ReturnToCabin", $"the pointer over the way out takes the focus back ({shell.FocusedKey})");
    }

    // Whichever results tab the card is not showing: the two are offered next to each other, Best
    // to Date first, and only the shown one presses an authored button.
    private static OriginalRow? UnselectedTab(OriginalShell shell)
    {
        var rows = shell.Rows;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Key == "BestTab")
            {
                return i + 1 < rows.Count ? rows[i + 1] : null;
            }

            if (rows[i].Key == "MostTab")
            {
                return i > 0 ? rows[i - 1] : null;
            }
        }

        return null;
    }

    // Ammo selection's stepper and ACCEPT write the pick into the profile; a third plane makes
    // CHANGE PLANE stand, and plane selection's ACCEPT writes the pilot's pick.
    private static void AmmoAndPlanes(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit,
        CampaignFeature campaign, CampaignProfileStore store)
    {
        Press(host, seat, Accept);
        var go = Row(shell, "GoToFlightCheck");
        if (go == null)
        {
            ctx.Check(false, $"the briefing carries GO TO FLIGHT CHECK");
            return;
        }

        Press(host, seat, Pointer(fit, go.X + 5f, go.Y + 5f, pressed: true, clicked: true));
        Press(host, seat, Accept);
        ctx.Check(shell.Screen == OriginalScreen.CampaignAmmo && campaign.AmmoSlot == 0,
            $"CHANGE AMMO opens ammo selection on the pilot's aircraft ({shell.Screen}, slot {campaign.AmmoSlot})");
        int field = -1;
        var rows = shell.Rows;
        for (int i = 0; i < rows.Count && field < 0; i++)
        {
            if (rows[i].Kind == OriginalRowKind.Dropdown && rows[i].Enabled && rows[i].Key.StartsWith("FIELD:", StringComparison.Ordinal))
            {
                field = i;
            }
        }

        ctx.Check(field >= 0, $"the starter mounts a gun in some group ({field})");
        if (field < 0)
        {
            return;
        }

        int group = int.Parse(rows[field].Key[6..], System.Globalization.CultureInfo.InvariantCulture);
        Press(host, seat, Pointer(fit, rows[field].X + 4f, rows[field].Y + 4f));
        ctx.Check(shell.Focus == field, $"the pointer over the field takes the focus ({shell.Focus})");
        string before = shell.Rows[field].Label;
        Press(host, seat, Right);
        ctx.Check(shell.Rows[field].Label != before && shell.Rows[field].Label == campaign.Strings.Text(3361, "Dum-dum"),
            $"a sideways step takes the next ammunition ({before} -> {shell.Rows[field].Label})");
        ctx.Check(store.Load(Pilot)!.Planes[0].Ammo[group] == 0, $"nothing is written before ACCEPT");
        Press(host, seat, Accept);
        ctx.Check(shell.Rows.Count > rows.Count && shell.Focus == field, $"Accept on the field opens its list under the box, the focus staying on the field ({shell.Rows.Count} rows)");
        Press(host, seat, Back);
        ctx.Check(shell.Screen == OriginalScreen.CampaignAmmo && shell.Rows.Count == rows.Count, $"Back closes the list and stays ({shell.Rows.Count} rows)");
        var accept = Row(shell, "AcceptLoadout")!;
        Press(host, seat, Pointer(fit, accept.X + 5f, accept.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.CampaignFlightCheck && store.Load(Pilot)!.Planes[0].Ammo[group] == 1,
            $"ACCEPT LOADOUT returns to the check and the profile file carries the pick ({shell.Screen}, {store.Load(Pilot)!.Planes[0].Ammo[group]})");

        var profile = store.Load(Pilot)!;
        profile.Planes.Add(new OwnedPlane { Name = "Test Bird", Airframe = 3 });
        store.Save(profile);
        host.Show(new CabinReturn(Pilot));
        Press(host, seat, Accept);
        go = Row(shell, "GoToFlightCheck")!;
        Press(host, seat, Pointer(fit, go.X + 5f, go.Y + 5f, pressed: true, clicked: true));
        var change = Row(shell, "ChangePlane");
        ctx.Check(change != null, $"with three planes the check offers CHANGE PLANE");
        if (change == null)
        {
            return;
        }

        Press(host, seat, Pointer(fit, change.X + 5f, change.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.CampaignPlaneSelection && campaign.PlaneSlot == 0 && shell.FocusedKey == "FIELD:0",
            $"CHANGE PLANE opens plane selection on the pilot's combo ({shell.Screen}, {shell.FocusedKey})");
        Press(host, seat, Left);
        ctx.Check(shell.Rows[0].Label.Contains("Test Bird", StringComparison.Ordinal) && store.Load(Pilot)!.SelectedPlane == 0,
            $"Left wraps the pick onto the new plane and writes nothing yet ({shell.Rows[0].Label})");
        var acceptPlanes = Row(shell, "AcceptSelections")!;
        Press(host, seat, Pointer(fit, acceptPlanes.X + 5f, acceptPlanes.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.CampaignFlightCheck && store.Load(Pilot)!.SelectedPlane == 2,
            $"ACCEPT SELECTIONS writes the pilot's pick into the profile ({store.Load(Pilot)!.SelectedPlane})");
        var brief = Row(shell, "ReturnToBriefing")!;
        Press(host, seat, Pointer(fit, brief.X + 5f, brief.Y + 5f, pressed: true, clicked: true));
        var cabin = Row(shell, "ReturnToCabin")!;
        Press(host, seat, Pointer(fit, cabin.X + 5f, cabin.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.CampaignCabin, $"RETURN TO BRIEFING then RETURN TO CABIN land on the cabin ({shell.Screen})");
    }

    // The pointer's wheel and thumb over the scrapbook's contents page: six flown missions in a
    // four-row window, wheeled a row, dragged to the foot and left where it started.
    private static void Scrolling(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell,
        BoardFit fit, CampaignProfileStore store)
    {
        var profile = store.Load(Pilot)!;
        for (int seq = 0; seq < 6; seq++)
        {
            CampaignProgression.Record(profile, new MissionAttempt(
                seq, CampaignProgression.PrimaryObjectiveMask, 300_000 + (seq * 20_000), 200, 90,
                profile.Planes[0].Airframe, profile.Planes[0].Name));
        }

        store.Save(profile);
        host.Show(new CabinReturn(Pilot));
        shell.ShowMissionScreen(OriginalScreen.CampaignPreviousMissions);
        ctx.Check(shell.Screen == OriginalScreen.CampaignPreviousMissions,
            $"PREVIOUS MISSIONS opens the contents page over six flown missions ({shell.Screen})");
        WheelAndDrag(ctx, host, seat, shell, fit, "CONTENTS", "the scrapbook's contents page");
        Press(host, seat, Back);
        ctx.Check(shell.Screen == OriginalScreen.CampaignCabin, $"Back leaves the contents page for the cabin ({shell.Screen})");
    }

    // One list under the pointer: a wheel step moves its window by exactly one row, a step past the
    // head clamps there, and a thumb taken and dragged the length of its track lands the window on
    // its last row without activating whatever the click stood over.
    private static void WheelAndDrag(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell,
        BoardFit fit, string key, string what)
    {
        var list = List(shell, key);
        ctx.Check(list is { Window.Scrolls: true },
            $"{what} is a scrolling list the pointer can see ({list?.Window.Count ?? -1} rows in a window of {list?.Window.Rows ?? -1})");
        if (list is not { Window.Scrolls: true })
        {
            return;
        }

        var window = list.Window;
        var screen = shell.Screen;
        float x = window.X + (window.Width / 2f);
        float y = window.Y + (window.Height / 2f);
        Press(host, seat, Pointer(fit, x, y, wheel: 1));
        ctx.Check(List(shell, key)?.Window.Top == window.Top + 1,
            $"a wheel step over it moves the window one row ({window.Top} -> {List(shell, key)?.Window.Top})");
        Press(host, seat, Pointer(fit, x, y, wheel: -3));
        ctx.Check(List(shell, key)?.Window.Top == 0, $"and three steps back up clamp at the head ({List(shell, key)?.Window.Top})");

        var head = List(shell, key)!.Window;
        Press(host, seat, Pointer(fit, head.ThumbX + 1f, head.ThumbY + 1f, pressed: true, clicked: true));
        ctx.Check(shell.Dragging == key, $"a click on its thumb takes hold of it ({shell.Dragging ?? "nothing"})");
        Press(host, seat, Pointer(fit, head.ThumbX + 1f, head.ThumbY + 1f + head.TrackHeight - head.ThumbHeight, pressed: true));
        ctx.Check(List(shell, key)?.Window.Top == head.LastTop,
            $"and dragging it the length of its track lands the window on its last row ({List(shell, key)?.Window.Top} of {head.LastTop})");
        Press(host, seat, Pointer(fit, head.ThumbX + 1f, head.ThumbY + 1f));
        ctx.Check(shell.Dragging == null && shell.Screen == screen,
            $"letting the button go ends the drag, the click having opened nothing ({shell.Dragging ?? "nothing"}, {shell.Screen})");
        Press(host, seat, Pointer(fit, x, y, wheel: -head.Count));
        ctx.Check(List(shell, key)?.Window.Top == 0, $"and the wheel brings it home ({List(shell, key)?.Window.Top})");
    }

    // PLANE CONSTRUCTION opens the hangar over the profile's wallet, its decal list is windowed to
    // the authored two rows and takes the wheel, and Back resumes the cabin.
    private static void HangarRoundTrip(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit)
    {
        var door = Row(shell, "PlaneConstruction");
        if (door == null)
        {
            ctx.Check(false, $"the cabin carries PLANE CONSTRUCTION");
            return;
        }

        var hangar = host.Features.Get<HangarFeature>();
        Press(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
        ctx.Check(shell.Screen == OriginalScreen.PlaneName && hangar.IsOpen && hangar.Wallet != null,
            $"PLANE CONSTRUCTION opens the name screen over the profile's wallet ({shell.Screen}, wallet {hangar.Wallet != null})");

        Press(host, seat, new MenuCommands { Typed = "Decalled" });
        var ok = Row(shell, OriginalShell.NameOkKey)!;
        Press(host, seat, Pointer(fit, ok.X + 5f, ok.Y + 5f, pressed: true, clicked: true));
        var paint = Row(shell, "PX_B_PAINT")!;
        Press(host, seat, Pointer(fit, paint.X + 5f, paint.Y + 5f, pressed: true, clicked: true));
        var decals = Row(shell, "PT_D_DECALS0");
        ctx.Check(shell.Screen == OriginalScreen.HangarPaint && decals != null,
            $"the Paint tab carries the first decal box ({shell.Screen})");
        if (decals != null)
        {
            Press(host, seat, Pointer(fit, decals.X + 5f, decals.Y + 5f, pressed: true, clicked: true));
            ctx.Check(shell.OpenHangarDropdown == "PT_D_DECALS0", $"a click opens its list ({shell.OpenHangarDropdown ?? "none"})");
            WheelAndDrag(ctx, host, seat, shell, fit, "PT_D_DECALS0", "Plane Construction's decal list");
            Press(host, seat, Back);
        }

        Press(host, seat, Back);
        ctx.Check(shell.Screen == OriginalScreen.CampaignCabin && !hangar.IsOpen && shell.FocusedKey == "PlaneConstruction",
            $"Back cancels the build and resumes the cabin on the row that opened it ({shell.Screen}, {shell.FocusedKey})");
    }

    // The export crossing over a scratch profile store and a scratch build store of its own: a plane
    // built with the campaign's wallet is not in the sortie roster, one EXPORT press puts it there.
    private static void ExportGate(TestContext ctx, MenuHost host, ScriptedSeat seat, OriginalShell shell, BoardFit fit,
        CampaignFeature campaign, string root)
    {
        const string Built = "Export Bird";
        var profiles = new CampaignProfileStore(Path.Combine(root, "GateProfiles"));
        var planes = new CustomPlaneStore(Path.Combine(root, "GatePlanes"));
        shell.OpenCampaignOver(profiles, planes);
        Press(host, seat, new MenuCommands { Typed = Pilot });
        Press(host, seat, Accept);
        var door = Row(shell, "PlaneConstruction");
        if (campaign.Profile is not { } seated || shell.CampaignWallet is not { } seatedWallet || door == null)
        {
            ctx.Check(false, $"the gate walk seats a player over its own stores ({campaign.Profile?.Name})");
            return;
        }

        ctx.Check(seated.Planes.Count == 2 && seatedWallet.OwnedBuilds().Count == 2,
            $"the two profile-seeded starters resolve with nothing in the build store ({seated.Planes.Count}, {seatedWallet.OwnedBuilds().Count})");
        seated.Funds = 500_000;
        profiles.Save(seated);

        var hangar = host.Features.Get<HangarFeature>();
        Press(host, seat, Pointer(fit, door.X + 5f, door.Y + 5f, pressed: true, clicked: true));
        ctx.Check(ReferenceEquals(hangar.Store, planes) && hangar.Wallet != null,
            $"PLANE CONSTRUCTION builds into the campaign's own store, never a second one (wallet {hangar.Wallet != null})");
        Press(host, seat, Back);

        // Back through the cabin re-reads the profile, so the wallet the build is funded by has to
        // be taken after it or the purchase would land on an object nothing else is looking at.
        if (campaign.Profile is not { } profile || shell.CampaignWallet is not { } wallet)
        {
            ctx.Check(false, $"the cabin resumes with the profile seated");
            return;
        }

        hangar.Open(planes, wallet);
        hangar.StartDefaultPlane();
        hangar.Scratch.Name = Built;
        bool committed = hangar.Commit();
        hangar.Discard();
        ctx.Check(committed && planes.Load(Built) is { AwaitingExport: true },
            $"a build funded by the wallet is saved waiting for EXPORT ({committed}, {hangar.Message})");
        int stock = OriginalRosters.Airframes.Count;
        ctx.Check(OriginalRosters.Roster(planes.List()).Count == stock,
            $"and the sortie roster still offers the stock airframes alone ({OriginalRosters.Roster(planes.List()).Count} of {stock})");

        profile.SelectedPlane = profile.Planes.Count - 1;
        profiles.Save(profile);
        shell.ShowMissionScreen(OriginalScreen.CampaignPlaneSelection);
        ctx.Check(shell.Screen == OriginalScreen.CampaignPlaneSelection && shell.Rows[0].Label.Contains(Built, StringComparison.Ordinal),
            $"plane selection puts the pilot's combo on the built aeroplane ({shell.Screen}, {shell.Rows[0].Label})");
        shell.PressExport();
        ctx.Check(planes.Load(Built) is { AwaitingExport: false },
            $"the FIRST EXPORT press clears the marker in the stored record ({planes.Load(Built)?.AwaitingExport})");
        var roster = OriginalRosters.Roster(planes.List());
        ctx.Check(roster.Count == stock + 1 && roster[stock].Name == Built,
            $"which is what puts it in the sortie roster after the stock rows ({roster.Count}, {(roster.Count > stock ? roster[stock].Name : "-")})");
        ctx.Check(wallet.OwnedBuilds().Count == 3 && profile.Planes.Count == 3,
            $"and the campaign still owns all three, the two starters included ({wallet.OwnedBuilds().Count})");
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

    private static MenuCommands Pointer(BoardFit fit, float authoredX, float authoredY, bool pressed = false, bool clicked = false, int wheel = 0) =>
        new() { Pointer = new MenuPointer(fit.X(authoredX), fit.Y(authoredY), pressed, clicked, wheel) };

    // The screen's list under that key, or null. Read fresh after every frame, since a window that
    // moved is a new record.
    private static OriginalList? List(OriginalShell shell, string key)
    {
        foreach (var list in shell.Lists)
        {
            if (list.Key == key)
            {
                return list;
            }
        }

        return null;
    }

    private static void Press(MenuHost host, ScriptedSeat seat, MenuCommands frame)
    {
        seat.Enqueue(frame);
        host.Tick(Dt);
    }

    // The host's audio, recording what the presentation asked for.
    private sealed class RecordingAudio : IMenuAudio
    {
        public List<string> Cues { get; } = new();

        public int Begins { get; private set; }

        public int Ends { get; private set; }

        public string LastWav { get; private set; } = string.Empty;

        public void Reset()
        {
            Cues.Clear();
            Begins = 0;
            Ends = 0;
            LastWav = string.Empty;
        }

        public void Cue(MenuCue cue) => Cues.Add(cue.Name);

        public void BeginNarration(string wavName)
        {
            Begins++;
            LastWav = wavName;
        }

        public void EndNarration() => Ends++;
    }

    private sealed class ScriptedSeat : IMenuInputSource
    {
        private readonly Queue<MenuCommands> _frames = new();

        public string DeviceLabel => "scripted";

        public bool CapturingText { get; set; }

        public void Enqueue(MenuCommands frame) => _frames.Enqueue(frame);

        public void Clear() => _frames.Clear();

        public MenuCommands Poll(float dt) => _frames.Count > 0 ? _frames.Dequeue() : MenuCommands.None;

        public void Prime()
        {
        }
    }
}
