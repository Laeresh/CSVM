using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight;
using CSVM.Flight.Hangar;
using CSVM.Flight.Modes;
using CSVM.Flight.Weapons;
using CSVM.Net;
using CSVM.Spec;
using CSVM.UI;
using CSVM.UI.Boards;
using CSVM.UI.Menu;
using CSVM.UI.Menu.BuiltIn;
using CSVM.UI.Menu.Original;
using CSVM.UI.Screens;

namespace CSVM.Testing;

/// <summary>
/// The Original presentation's network doors, driven on three menu hosts at once. A host sits in
/// the campaign cabin and two guests stand on the top level. The games wire is the in-process
/// loopback and the LAN search runs over <see cref="LoopbackLan"/>, both bound on the loopback
/// address. The router is a stub that records what it was asked to give back. A second suite
/// stands the shipped discovery socket up on 127.0.0.1 and searches it by unicast.
/// </summary>
internal static class MenuOriginalConnectionSuites
{
    private const float Dt = 1f / 60f;
    private const string Loopback = "127.0.0.1";

    // The game the lobby's host names in GAME INFORMATION, and the cap it chooses there.
    private const string LobbyGame = "Friday Fliers";
    private const int LobbyCap = 6;

    // The password the boot suite's host asks.
    private const string LobbyPassword = "swordfish";

    // The suite whose scratch store holds the plane the Connection page builds.
    private const string BuildSuite = "menu-original-connection-build";

    [Suite("menu-original-connection",
        "The Original presentation's network doors over the loopback and an in-process LAN: the "
        + "cabin's HOST CO-OP asks GAME INFORMATION, which opens on Private, whose Cancel opens nothing and whose cap of "
        + "sixteen is held to four, and then opens the carrier, the router mapping and the LAN answer a "
        + "Private host still gives, and CLOSE "
        + "NETWORK gives all three back, the Multiplayer plaque opens the Connection page, which with no "
        + "master server greys its Join by code way and says why, its Connect over LAN TCP/IP lists the host as one row of five columns, Join Game lands the "
        + "guest on the host's cabin after PLAYER INFORMATION, a second guest joins, the host's chips "
        + "name both guests by their callsigns, a fourth human is seated and a fifth is "
        + "refused as full, a silent drop tells a guest the host left, and CLOSE NETWORK tells the "
        + "other the host closed the game and puts it back on the Connection page")]
    internal static void TheConnectionPage(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        // The first host wire serves the open-and-close control, the second the match. A loopback
        // end binds once, so every open needs its own. The mesh is connected from the start, so the
        // host's end is gated: a guest reaches it only when that guest joins.
        var lan = new LoopbackLan();
        var spare = LoopbackTransport.Mesh(1, LoopbackConditions.Perfect, new Random(3));
        var mesh = LoopbackTransport.Mesh(5, LoopbackConditions.Perfect, new Random(5));
        var gate = new ArrivalGate(mesh[0]);
        var hostWires = new Queue<INetTransport>(new INetTransport[] { spare[0], gate });
        var unmapped = new List<int>();
        var hostDoor = new NetPlayFeature(
            (_, _, _) => hostWires.Dequeue(),
            (_, _) => throw new InvalidOperationException("the host does not join"),
            new RouterAccess(
                port => new UpnpPortMapResult(UpnpPortMapOutcome.Mapped, port, NetDoorAid.ExternalAddress, "suite"),
                unmapped.Add),
            lan.Bind);
        var doors = new List<NetPlayFeature> { hostDoor };
        for (int i = 1; i < mesh.Count; i++)
        {
            int end = i;
            doors.Add(new NetPlayFeature(
                (_, _, _) => throw new InvalidOperationException("a guest does not host"),
                (_, _) =>
                {
                    gate.Arrive(mesh[end].LocalPeer);
                    return mesh[end];
                },
                lan: lan.Bind));
        }

        foreach (var door in doors)
        {
            door.BindAddress = Loopback;
            door.SearchAddress = Loopback;
        }

        var ends = new List<End>();
        string? options = MenuSuiteHost.ScratchOptions(ctx, "menu-original-connection");
        try
        {
            var host = Open(ctx, layout, doors[0], ends);
            var told = Open(ctx, layout, doors[1], ends);
            var dropped = Open(ctx, layout, doors[2], ends);
            if (host == null || told == null || dropped == null)
            {
                return;
            }

            host.Shell.Campaign.OpenCampaignOver(
                CampaignAidProfiles.Store(seeded: true, progressed: true), CampaignAidProfiles.Planes());
            host.Shell.Campaign.ShowCabin(CampaignAidProfiles.Pilot);
            OpenAndCloseControl(ctx, host, hostDoor, unmapped);
            OpenForTheMatch(ctx, host, hostDoor);
            JoinThroughTheList(ctx, told, ends, 1, "the first guest");
            JoinThroughTheList(ctx, dropped, ends, 2, "the second guest");
            Pump(ends.ToArray());
            ctx.Check(DrawsOver(host.Shell.Compose(), "Nathan" + LaunchMenu.RemoteChipMark) && DrawsOver(host.Shell.Compose(), "Sheila" + LaunchMenu.RemoteChipMark),
                $"the host's chips name each guest by its callsign ({string.Join(", ", hostDoor.CoopGuests.Select(g => g.Name))})");
            FillTheGame(ctx, ends, hostDoor, doors[3], doors[4]);
            DropOne(ctx, dropped, mesh[0], mesh[2].LocalPeer);
            CloseTheGame(ctx, host, told, ends, hostDoor, unmapped);
        }
        finally
        {
            foreach (var end in ends)
            {
                end.Host.Deactivate();
            }

            doors.ForEach(door => door.Discard());
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            CSVM.Utils.OptionsStore.DirectoryOverride = options;
        }
    }

    [Suite("menu-original-lobby",
        "The Multiplayer Lobby over the loopback: the Connection page's Host asks GAME INFORMATION, "
        + "whose typed name and a cap stepped to six are advertised and remembered, then PLAYER "
        + "INFORMATION, whose empty callsign greys OK and whose callsign of spaces is refused, and "
        + "opens the lobby as a Dogfight host under its callsign. The games list on a second session "
        + "reads that name, n/6, the Dogfight, its environment and Waiting. Join Game asks PLAYER "
        + "INFORMATION, and the guest's callsign stands in both lobby lists and its voice reaches the "
        + "host in its pick. A joined guest lands in the lobby, each of the three types is described under the Type box "
        + "on both ends by its own langui line, the host's map, Time 5 and Limited Lives reach the guest, a guest's option "
        + "set is refused, a Ready guest's plane box stays live and its changed pick clears its Ready "
        + "on both ends, the guest's second stock plane and a non-default shell build its seat "
        + "in the host's field, one chat line arrives once on each end, a guest's line past the "
        + "chat's depth repaints the host's lobby with no input at the host, LAUNCH waits for every "
        + "Ready, the guest launches behind the host on the same rules, a completed match lands "
        + "both ends on Game Scores with the same scores and every Ready cleared, a second LAUNCH "
        + "goes out with nobody rejoining, and Leave Game lands a second guest on the Connection "
        + "page, whose games list's Create Game opens a hosted lobby of its own")]
    internal static void TheLobby(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        var lan = new LoopbackLan();
        var mesh = LoopbackTransport.Mesh(3, LoopbackConditions.Perfect, new Random(11));
        var gate = new ArrivalGate(mesh[0]);
        var hostDoor = new NetPlayFeature(
            (_, _, _) => gate,
            (_, _) => throw new InvalidOperationException("the host does not join"),
            new RouterAccess(
                port => new UpnpPortMapResult(UpnpPortMapOutcome.Mapped, port, NetDoorAid.ExternalAddress, "suite"),
                _ => { }),
            lan.Bind);
        var doors = new List<NetPlayFeature> { hostDoor };
        for (int i = 1; i < mesh.Count; i++)
        {
            // A guest hosts only through the games list's Create Game, on a lone wire of its own.
            int end = i;
            doors.Add(new NetPlayFeature(
                (_, _, _) => LoopbackTransport.Mesh(1, LoopbackConditions.Perfect, new Random(end))[0],
                (_, _) =>
                {
                    gate.Arrive(mesh[end].LocalPeer);
                    return new Hangup(mesh[end]);
                },
                lan: lan.Bind));
        }

        foreach (var door in doors)
        {
            door.BindAddress = Loopback;
            door.SearchAddress = Loopback;
        }

        var ends = new List<End>();
        string? options = MenuSuiteHost.ScratchOptions(ctx, "menu-original-lobby");
        var hostExits = new List<MenuExit>();
        var guestExits = new List<MenuExit>();
        try
        {
            var host = Open(ctx, layout, doors[0], ends, hostExits);
            var guest = Open(ctx, layout, doors[1], ends, guestExits);
            var leaver = Open(ctx, layout, doors[2], ends);
            if (host == null || guest == null || leaver == null)
            {
                return;
            }

            if (!HostTheLobby(ctx, host) || !JoinTheLobby(ctx, guest, ends, mesh[1].LocalPeer, "Nathan", 5) || !JoinTheLobby(ctx, leaver, ends, -1, "Sheila"))
            {
                return;
            }

            LeaveTheLobby(ctx, host, leaver, ends);
            SetTheOptions(ctx, host, guest, ends);
            int slot = PickThePlane(ctx, host, guest, ends);
            Chat(ctx, host, guest, ends);
            ctx.Check(Row(host.Shell, OriginalLobbyScreen.ScoresTabKey) is { Enabled: false },
                $"ABLE-TO-FAIL CONTROL: Game Scores is greyed before any match has landed");
            var launch = LaunchTheMatch(ctx, host, guest, ends, hostExits);
            if (launch?.Net is { } wire)
            {
                var roster = HostField(ctx, launch, wire, slot);
                GuestLaunch(ctx, wire, roster, guest, guestExits);
                LandOnTheScores(ctx, host, guest, ends, mesh, guestExits);
                LaunchAgain(ctx, host, guest, ends, hostExits, guestExits);
            }
        }
        finally
        {
            foreach (var end in ends)
            {
                end.Host.Deactivate();
            }

            doors.ForEach(door => door.Discard());
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            CSVM.Utils.OptionsStore.DirectoryOverride = options;
        }
    }

    [Suite("menu-original-outlaw-list",
        "The Multiplayer Lobby's outlaw list over the loopback: Select... is greyed on both ends while "
        + "Outlaw Components is clear, and the host's list still reaches the guest then. Toggling the "
        + "tick either way empties the list on both ends in one round. Once ticked, "
        + "the host's Select... opens the list over the tab page with the tabs greyed, and one tick of "
        + "an airframe, a gun calibre, an ammunition, a rocket past the scroll, All Ammo, All Rockets and "
        + "nitro each reaches the guest with every Ready cleared. A rocket row under All Rockets stays "
        + "ticked and ignores a click, Cancel puts the opening list back and Accept keeps it, a Ready "
        + "host's list is read-only, and the guest's View... opens it read-only with no Accept")]
    internal static void TheOutlawList(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        var lan = new LoopbackLan();
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(71));
        var gate = new ArrivalGate(mesh[0]);
        var hostDoor = new NetPlayFeature(
            (_, _, _) => gate,
            (_, _) => throw new InvalidOperationException("the host does not join"),
            new RouterAccess(
                port => new UpnpPortMapResult(UpnpPortMapOutcome.Mapped, port, NetDoorAid.ExternalAddress, "suite"),
                _ => { }),
            lan.Bind);
        var guestDoor = new NetPlayFeature(
            (_, _, _) => throw new InvalidOperationException("the guest does not host"),
            (_, _) =>
            {
                gate.Arrive(mesh[1].LocalPeer);
                return new Hangup(mesh[1]);
            },
            lan: lan.Bind);
        foreach (var door in new[] { hostDoor, guestDoor })
        {
            door.BindAddress = Loopback;
            door.SearchAddress = Loopback;
        }

        var ends = new List<End>();
        string? options = MenuSuiteHost.ScratchOptions(ctx, "menu-original-outlaw-list");
        try
        {
            var host = Open(ctx, layout, hostDoor, ends);
            var guest = Open(ctx, layout, guestDoor, ends);
            if (host == null || guest == null || !HostTheLobby(ctx, host) || !JoinTheLobby(ctx, guest, ends))
            {
                return;
            }

            EditedWithTheTickClear(ctx, host, guest, ends);
            ToggleEmptiesTheList(ctx, host, guest, ends);
            ClickRow(ctx, host, OriginalLobbyScreen.OutlawKey);
            Pump(ends.ToArray());
            ClickRow(ctx, host, OriginalLobbyScreen.SelectKey);
            ctx.Check(host.Shell.Lobby is { OutlawListOpen: true, OutlawPage: OutlawPage.Airframes }
                      && Row(host.Shell, OriginalLobbyScreen.PlaneTabKey) is { Enabled: false },
                $"with the tick set the host's Select... opens the list on Airframes, the tabs greyed ({host.Shell.Lobby.OutlawListOpen}, {host.Shell.Lobby.OutlawPage})");
            TickEachKind(ctx, host, guest, ends);
            AllRocketsCoversItsRows(ctx, host, ends);
            CancelAndAccept(ctx, host, guest, ends);
            ViewedReadOnly(ctx, host, guest, ends);
        }
        finally
        {
            foreach (var end in ends)
            {
                end.Host.Deactivate();
            }

            hostDoor.Discard();
            guestDoor.Discard();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            CSVM.Utils.OptionsStore.DirectoryOverride = options;
        }
    }

    [Suite("menu-original-lobby-teams",
        "The Multiplayer Lobby's teams over the loopback: the host's Create Team stands the CREATE "
        + "TEAM box with OK greyed while empty, a name of spaces alone is refused, and a typed name "
        + "creates the team, which reaches the guest as a team row under the host's row. The guest "
        + "picks that row and its team button joins it, which every end's chat announces. Restrict "
        + "Number of Teams makes the count boxes live on the host and greyed on the guest, whose arrows "
        + "reach the guest's options. With one team LAUNCH! raises the original's refusal and hands "
        + "nothing out. The guest's Leave Team and a team of its own let the launch go, and the host's "
        + "field carries each seat's team")]
    internal static void TheLobbyTeams(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        var lan = new LoopbackLan();
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(84));
        var gate = new ArrivalGate(mesh[0]);
        var hostDoor = new NetPlayFeature(
            (_, _, _) => gate,
            (_, _) => throw new InvalidOperationException("the host does not join"),
            new RouterAccess(
                port => new UpnpPortMapResult(UpnpPortMapOutcome.Mapped, port, NetDoorAid.ExternalAddress, "suite"),
                _ => { }),
            lan.Bind);
        var guestDoor = new NetPlayFeature(
            (_, _, _) => throw new InvalidOperationException("the guest does not host"),
            (_, _) =>
            {
                gate.Arrive(mesh[1].LocalPeer);
                return new Hangup(mesh[1]);
            },
            lan: lan.Bind);
        foreach (var door in new[] { hostDoor, guestDoor })
        {
            door.BindAddress = Loopback;
            door.SearchAddress = Loopback;
        }

        var ends = new List<End>();
        var hostExits = new List<MenuExit>();
        string? options = MenuSuiteHost.ScratchOptions(ctx, "menu-original-lobby-teams");
        try
        {
            var host = Open(ctx, layout, hostDoor, ends, hostExits);
            var guest = Open(ctx, layout, guestDoor, ends);
            if (host == null || guest == null || !HostTheLobby(ctx, host) || !JoinTheLobby(ctx, guest, ends))
            {
                return;
            }

            byte team = CreateTheTeam(ctx, host, guest, ends);
            JoinTheTeam(ctx, host, guest, ends, team);
            RestrictTheTeams(ctx, host, guest, ends);
            LaunchOnTeams(ctx, host, guest, ends, hostExits, team);
        }
        finally
        {
            foreach (var end in ends)
            {
                end.Host.Deactivate();
            }

            hostDoor.Discard();
            guestDoor.Discard();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            CSVM.Utils.OptionsStore.DirectoryOverride = options;
        }
    }

    [Suite("menu-original-builtin-host",
        "An Original guest against a Built-in Dogfight host over the loopback: the host's door opens "
        + "with its lobby unshown, the guest finds it on the games list and lands in the lobby, its "
        + "third stock plane reaches the host, the host's launch waits for the guest's Ready and "
        + "then writes its map and time into the options, and the guest launches behind the host "
        + "on that map and time in its own pick")]
    internal static void TheBuiltInHost(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        // The host has no menu of its own here: the Built-in door is the NetPlayFeature it opens,
        // stepped by hand between the guest's frames.
        var lan = new LoopbackLan();
        var mesh = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(13));
        var gate = new ArrivalGate(mesh[0]);
        var hostDoor = new NetPlayFeature(
            (_, _, _) => gate,
            (_, _) => throw new InvalidOperationException("the host does not join"),
            new RouterAccess(
                port => new UpnpPortMapResult(UpnpPortMapOutcome.Mapped, port, NetDoorAid.ExternalAddress, "suite"),
                _ => { }),
            lan.Bind);
        var guestDoor = new NetPlayFeature(
            (_, _, _) => throw new InvalidOperationException("the guest does not host"),
            (_, _) =>
            {
                gate.Arrive(mesh[1].LocalPeer);
                return new Hangup(mesh[1]);
            },
            lan: lan.Bind);
        foreach (var door in new[] { hostDoor, guestDoor })
        {
            door.BindAddress = Loopback;
            door.SearchAddress = Loopback;
        }

        var ends = new List<End>();
        string? options = MenuSuiteHost.ScratchOptions(ctx, "menu-original-builtin-host");
        var guestExits = new List<MenuExit>();
        try
        {
            var guest = Open(ctx, layout, guestDoor, ends, guestExits);
            if (guest == null)
            {
                return;
            }

            hostDoor.OpenHost(NetSeats.MaxPlayers - 1);
            ctx.Check(hostDoor.Dogfight is { IsHost: true, Shown: false }, $"the Built-in host's door opens with its lobby unshown ({hostDoor.Stage})");
            ClickRow(ctx, guest, OriginalShell.MultiplayerKey);
            ClickRow(ctx, guest, OriginalConnectionScreen.ConnectKey);
            for (int frame = 0; frame < 6 && guest.Shell.Connection.Listed.Count == 0; frame++)
            {
                Frames(hostDoor, guest, 1);
            }

            if (guest.Shell.Connection.Listed.Count != 1)
            {
                ctx.Check(false, $"the search lists the Built-in host's game ({guest.Shell.Connection.Listed.Count})");
                return;
            }

            ClickRow(ctx, guest, OriginalConnectionScreen.GameKey(0));
            ClickRow(ctx, guest, OriginalConnectionScreen.JoinKey);
            Answer(ctx, guest, "Laeresh");
            Frames(hostDoor, guest, 6);
            ctx.Check(guest.Shell.Screen == OriginalScreen.Lobby && guestDoor.Dogfight is { IsHost: false, HasOptions: true },
                $"Join Game lands the guest in the Built-in host's lobby ({guest.Shell.Screen}, {guestDoor.Stage}, {guest.Shell.Dialog?.Message})");
            if (guest.Shell.Screen != OriginalScreen.Lobby)
            {
                return;
            }

            ClickRow(ctx, guest, OriginalLobbyScreen.PlaneTabKey);
            ClickRow(ctx, guest, OriginalLobbyScreen.PlaneKey);
            ClickRow(ctx, guest, OriginalLobbyScreen.PlaneKey + ":2");
            Frames(hostDoor, guest, 4);
            var lobby = hostDoor.Dogfight!;
            ctx.Check(lobby.Players.Count == 2 && lobby.Players[1].Airframe == 2,
                $"the guest's third stock plane reaches the Built-in host ({string.Join(",", lobby.Players.Select(p => p.Airframe))})");
            string chapter = DogfightLobby.ChapterOf(2);
            var rules = new VersusRules(0, 5);
            ctx.Check(lobby.CheckBuiltInLaunch(chapter, rules) == DogfightLobby.GuestsNotReady,
                $"ABLE-TO-FAIL CONTROL: the host's launch waits while the guest is not Ready");
            ClickRow(ctx, guest, OriginalLobbyScreen.ReadyKey);
            Frames(hostDoor, guest, 4);
            ctx.Check(lobby.CheckBuiltInLaunch(chapter, rules) == null,
                $"once the guest is Ready the launch may go ({lobby.Players[1].Ready})");
            Frames(hostDoor, guest, 4);
            var heard = guestDoor.Dogfight!.Options;
            ctx.Check(heard is { Environment: 2, TimeMinutes: 5, Victory: DogfightVictory.Time },
                $"and the launch's map and time reach the guest as the lobby's options ({heard.Environment}, {heard.TimeMinutes}, {heard.Victory})");
            HostTheBuiltInLaunch(ctx, hostDoor, guest, guestExits, chapter);
        }
        finally
        {
            foreach (var end in ends)
            {
                end.Host.Deactivate();
            }

            hostDoor.Discard();
            guestDoor.Discard();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            CSVM.Utils.OptionsStore.DirectoryOverride = options;
        }
    }

    [Suite("menu-original-version",
        "An Original guest of build 0.6 against a Built-in Dogfight host of 0.7 over the loopback: "
        + "the games list marks the host's row with its version in the Status column, Join Game "
        + "raises a box naming both versions and opens no socket, a join typed over Internet "
        + "TCP/IP is refused on the wire with the same words while the host seats nobody, and a "
        + "guest a patch apart from the host joins it")]
    internal static void TheVersionCheck(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        // The loopback links every end to every other, and a real guest links only to its host.
        var lan = new LoopbackLan();
        var mesh = LoopbackTransport.Mesh(3, LoopbackConditions.Perfect, new Random(17));
        mesh[1].Disconnect(mesh[2].LocalPeer);
        var gate = new ArrivalGate(mesh[0]);
        var hostDoor = new NetPlayFeature(
            (_, _, _) => gate,
            (_, _) => throw new InvalidOperationException("the host does not join"),
            new RouterAccess(
                port => new UpnpPortMapResult(UpnpPortMapOutcome.Mapped, port, NetDoorAid.ExternalAddress, "suite"),
                _ => { }),
            lan.Bind)
        { Version = NetBuildVersion.Parse("0.7.0") };
        int opened = 0;
        var guestDoor = new NetPlayFeature(
            (_, _, _) => throw new InvalidOperationException("the guest does not host"),
            (_, _) =>
            {
                opened++;
                gate.Arrive(mesh[1].LocalPeer);
                return new Hangup(mesh[1]);
            },
            lan: lan.Bind)
        { Version = NetBuildVersion.Parse("0.6.3") };
        var patched = new NetPlayFeature(
            (_, _, _) => throw new InvalidOperationException("the guest does not host"),
            (_, _) =>
            {
                gate.Arrive(mesh[2].LocalPeer);
                return mesh[2];
            })
        { Version = NetBuildVersion.Parse("0.7.9") };
        foreach (var door in new[] { hostDoor, guestDoor, patched })
        {
            door.BindAddress = Loopback;
            door.SearchAddress = Loopback;
        }

        var ends = new List<End>();
        string? options = MenuSuiteHost.ScratchOptions(ctx, "menu-original-version");
        try
        {
            var guest = Open(ctx, layout, guestDoor, ends);
            if (guest == null)
            {
                return;
            }

            hostDoor.OpenHost(NetSeats.MaxPlayers - 1);
            RefusedFromTheList(ctx, hostDoor, guest, () => opened);
            RefusedOnTheWire(ctx, hostDoor, guest, () => opened);
            patched.OpenJoin();
            for (int frame = 0; frame < 4; frame++)
            {
                hostDoor.Step(Dt);
                patched.Step(Dt);
            }

            ctx.Check(patched.Stage == NetDoorStage.Joined && hostDoor.Peers == 1,
                $"ABLE-TO-FAIL CONTROL: a guest a patch apart joins the same host ({patched.Stage}, {patched.Fault}, {hostDoor.Peers} joined)");
        }
        finally
        {
            foreach (var end in ends)
            {
                end.Host.Deactivate();
            }

            hostDoor.Discard();
            guestDoor.Discard();
            patched.Discard();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            CSVM.Utils.OptionsStore.DirectoryOverride = options;
        }
    }

    [Suite("menu-original-ipv6-address",
        "An IPv6 address typed into the Original Connection page's IP Address box as a German "
        + "keyboard sends it: every character is a real key event pushed through the viewport and "
        + "read back through a menu seat, so ':' (Shift and the period key) arrives as ':', the "
        + "full bracketed address with its port fits the box, the box draws its end with the "
        + "caret after it, and the door splits it into the bare host and the typed port. Ctrl+V "
        + "and Shift+Insert paste it from a stand-in clipboard, trimmed, a refused character left "
        + "out under the reject cue. Then "
        + "[::1] with a port is typed and joined over the shipped ENet carrier, and a bare "
        + "0:0:0:0:0:0:0:1 joins on the board's port with its last group left in the host")]
    internal static void AnIpv6AddressIsTypedAndJoined(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        var door = new NetPlayFeature(
            (_, _, _) => throw new InvalidOperationException("the guest does not host"),
            (address, port) => EnetTransport.Join(address, port));
        var bare = new NetPlayFeature(
            (_, _, _) => throw new InvalidOperationException("the guest does not host"),
            (address, port) => EnetTransport.Join(address, port));
        var reader = new BuiltInSeat(new MenuInput { Keyboard = true });
        var ends = new List<End>();
        string? options = MenuSuiteHost.ScratchOptions(ctx, "menu-original-ipv6-address");
        EnetTransport? host = null;
        try
        {
            var guest = Open(ctx, layout, door, ends);
            if (guest == null)
            {
                return;
            }

            ClickRow(ctx, guest, OriginalShell.MultiplayerKey);
            ClickRow(ctx, guest, OriginalConnectionScreen.InternetKey);
            ClickRow(ctx, guest, OriginalConnectionScreen.AddressKey);
            ctx.Check(guest.Shell.Connection.CapturingText,
                $"the IP Address box has the keyboard ({guest.Shell.Screen}, {guest.Shell.FocusedKey})");
            reader.Prime();
            TypeTheReportedAddress(ctx, guest, reader);
            PasteTheAddress(ctx, guest, reader);

            host = OpenIpv6Host(out int port, out string refused);
            if (host == null)
            {
                ctx.Check(false, $"ENet cannot host on [::1] in this process, so no IPv6 join can be shown: {refused}");
                return;
            }

            JoinTheLoopback(ctx, guest, reader, host, port);
            JoinTheBareLoopback(ctx, bare, host, port);
        }
        finally
        {
            foreach (var end in ends)
            {
                end.Host.Deactivate();
            }

            door.Discard();
            bare.Discard();
            host?.Dispose();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            CSVM.Utils.OptionsStore.DirectoryOverride = options;
        }
    }

    [Suite("menu-original-screen-keyboard",
        "Steam's on-screen keyboard on the Original Connection page, its URLs recorded: taps on "
        + "plaques raise nothing, a tap in the IP Address box raises it without connecting, the "
        + "echo strip repeats the box's words, the focus leaving the box lowers it and coming back "
        + "alone raises nothing, a pad's Accept in the box raises it without connecting, and a key's "
        + "Enter lowers it and is the box's own Connect")]
    internal static void TheOnScreenKeyboard(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        var door = new NetPlayFeature(
            (_, _, _) => throw new InvalidOperationException("the guest does not host"),
            (_, _) => throw new InvalidOperationException("an empty address is never joined"));
        var ends = new List<End>();
        string? options = MenuSuiteHost.ScratchOptions(ctx, "menu-original-screen-keyboard");
        var echo = new ScreenKeyboardEcho();
        ctx.Host.AddChild(echo);
        var keyboard = new ScreenKeyboardRecorder();
        try
        {
            var guest = Open(ctx, layout, door, ends);
            if (guest == null)
            {
                return;
            }

            ClickRow(ctx, guest, OriginalShell.MultiplayerKey);
            ClickRow(ctx, guest, OriginalConnectionScreen.InternetKey);
            ctx.Check(keyboard.Urls.Count == 0, $"ABLE-TO-FAIL CONTROL: taps on plaques raise nothing ({keyboard.Said})");
            ClickRow(ctx, guest, OriginalConnectionScreen.AddressKey);
            ctx.Check(keyboard.Said == CSVM.Utils.ScreenKeyboard.OpenUrl
                      && CSVM.Utils.ScreenKeyboard.Shown?.Id == OriginalConnectionScreen.AddressKey,
                $"a tap in the IP Address box raises the keyboard for it ({keyboard.Said}, {CSVM.Utils.ScreenKeyboard.Shown?.Id})");
            ctx.Check(guest.Shell.Screen == OriginalScreen.Connection && guest.Shell.Dialog == null && door.Stage == NetDoorStage.Shut,
                $"and does not connect ({guest.Shell.Screen}, {guest.Shell.Dialog?.Message}, {door.Stage})");
            echo._Process(0);
            ctx.Check(echo.Line == $"{door.Address}_", $"the echo strip repeats the box's words with a caret ({echo.Line})");

            Press(guest, new MenuCommands { MoveY = 1 });
            echo._Process(0);
            ctx.Check(keyboard.Urls.Count == 2 && keyboard.Urls[1] == CSVM.Utils.ScreenKeyboard.CloseUrl && echo.Line.Length == 0,
                $"the focus leaving the box lowers it and the strip goes ({guest.Shell.FocusedKey}, {keyboard.Said})");
            Press(guest, new MenuCommands { MoveY = -1 });
            ctx.Check(guest.Shell.FocusedKey == OriginalConnectionScreen.AddressKey && keyboard.Urls.Count == 2,
                $"coming back onto the box alone raises nothing ({guest.Shell.FocusedKey}, {keyboard.Said})");

            Press(guest, new MenuCommands { Accept = true, KeylessAccept = true });
            ctx.Check(keyboard.Urls.Count == 3 && guest.Shell.Dialog == null,
                $"a pad's Accept in the box raises it and does not connect ({keyboard.Said}, {guest.Shell.Dialog?.Message})");

            for (int i = 0; i < 64 && door.Address.Length > 0; i++)
            {
                Press(guest, new MenuCommands { Erase = true });
            }

            Press(guest, new MenuCommands { Accept = true });
            ctx.Check(keyboard.Urls.Count == 4 && keyboard.Urls[3] == CSVM.Utils.ScreenKeyboard.CloseUrl && guest.Shell.Dialog != null,
                $"a key's Enter lowers it and is the box's Connect, refusing the emptied address ({keyboard.Said}, {guest.Shell.Dialog?.Message})");
        }
        finally
        {
            keyboard.Dispose();
            foreach (var end in ends)
            {
                end.Host.Deactivate();
            }

            door.Discard();
            ctx.Host.RemoveChild(echo);
            echo.QueueFree();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            CSVM.Utils.OptionsStore.DirectoryOverride = options;
        }
    }

    [Suite("menu-original-connection-build",
        "The Original Connection page's Build Custom Plane over a scratch plane store: it is live, the "
        + "keyboard's walk reaches it under the IP Address box and Enter opens the wallet-free name "
        + "screen, Back and the hub's CANCEL each land back on the Connection page on the button with "
        + "nothing saved, and a purchase saves an exported build on the default airframe and lands back "
        + "there too. Host then opens a lobby with Allow Custom Planes ticked, whose Custom Planes tab "
        + "offers the plane and picks it, and the host's untick greys that tab again")]
    internal static void BuildCustomPlaneFromTheConnectionPage(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        var door = new NetPlayFeature(
            (_, _, _) => LoopbackTransport.Mesh(1, LoopbackConditions.Perfect, new Random(115))[0],
            (_, _) => throw new InvalidOperationException("the host does not join"));
        door.BindAddress = Loopback;
        door.SearchAddress = Loopback;
        var ends = new List<End>();
        var store = MenuSuiteHost.ScratchPlanes(ctx, BuildSuite);
        string? options = MenuSuiteHost.ScratchOptions(ctx, BuildSuite);
        try
        {
            var end = Open(ctx, layout, door, ends, planes: store);
            if (end == null || !BuildFromTheConnectionPage(ctx, end, store, "Lobby Hornet"))
            {
                return;
            }

            ClickRow(ctx, end, OriginalConnectionScreen.HostKey);
            Answer(ctx, end, "Zachary", LobbyGame);
            Pump(end);
            if (door.Dogfight is not { IsHost: true } lobby || end.Shell.Screen != OriginalScreen.Lobby)
            {
                ctx.Check(false, $"Host opens the lobby as a Dogfight's host ({end.Shell.Screen}, {door.Stage})");
                return;
            }

            ctx.Check(lobby.Rules.AllowCustom, $"the new lobby opens with Allow Custom Planes ticked, as the original's host open ticks it ({lobby.Rules})");
            ClickRow(ctx, end, OriginalLobbyScreen.PlaneTabKey);
            ClickRow(ctx, end, OriginalLobbyScreen.CustomTabKey);
            ClickRow(ctx, end, OriginalLobbyScreen.PlaneKey);
            var offered = end.Shell.Rows.FirstOrDefault(row => row.Key.StartsWith(OriginalLobbyScreen.PlaneKey + ":", StringComparison.Ordinal)
                                                               && row.Label == "Lobby Hornet");
            ctx.Check(offered != null, $"the Custom Planes tab offers the plane built from the Connection page ({string.Join(", ", end.Shell.Rows.Select(r => r.Label))})");
            if (offered != null)
            {
                ClickRow(ctx, end, offered.Key);
                ctx.Check(lobby.Build?.Name == "Lobby Hornet" && lobby.Refusal == PlaneRefusal.None,
                    $"and picks it, refused nothing ({lobby.Build?.Name ?? "stock"}, {lobby.Refusal})");
            }

            ClickRow(ctx, end, OriginalLobbyScreen.MissionTabKey);
            ClickRow(ctx, end, OriginalLobbyScreen.CustomPlanesKey);
            ClickRow(ctx, end, OriginalLobbyScreen.PlaneTabKey);
            ctx.Check(!lobby.Rules.AllowCustom && Row(end.Shell, OriginalLobbyScreen.CustomTabKey) is { Enabled: false },
                $"ABLE-TO-FAIL CONTROL: the host's untick of Allow Custom Planes greys the Custom Planes tab ({lobby.Rules})");
        }
        finally
        {
            foreach (var end in ends)
            {
                end.Host.Deactivate();
            }

            door.Discard();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            CSVM.Utils.OptionsStore.DirectoryOverride = options;
            MenuSuiteHost.DropScratchPlanes(ctx, BuildSuite);
        }
    }

    [Suite("lan-discovery",
        "The shipped LAN discovery socket on the loopback: a responder bound on the discovery port "
        + "answers a search sent to 127.0.0.1 by unicast with the advert and game port it was "
        + "handed, a datagram that is not a query is read and gets no answer, and a game that "
        + "stops answering survives one silent round and leaves on the next")]
    internal static void TheDiscoverySocket(TestContext ctx)
    {
        LanDiscoverySocket answer;
        LanDiscoverySocket ask;
        try
        {
            answer = LanDiscoverySocket.Bind(NetPorts.Lan, Loopback);
        }
        catch (InvalidOperationException e)
        {
            ctx.Check(false, $"the discovery port binds on the loopback ({e.Message})");
            return;
        }

        try
        {
            ask = LanDiscoverySocket.Bind(0, Loopback);
        }
        catch (InvalidOperationException e)
        {
            answer.Dispose();
            ctx.Check(false, $"a search socket binds a free port on the loopback ({e.Message})");
            return;
        }

        var responder = new LanResponder(answer);
        var search = new LanSearch(ask, Loopback, NetPorts.Lan);
        try
        {
            var advert = new SessionAdvertMessage(NetSessionKind.CampaignCoop, 3, 2, CampaignAidProfiles.Pilot, NetSessionStatus.Waiting, 4);
            search.Ask();
            Settle(() => responder.Poll(advert, NetPlayFeature.DefaultPort), search, () => search.Games.Count > 0);
            var games = search.Games;
            ctx.Check(games.Count == 1, $"the search hears the one responder ({games.Count})");
            if (games.Count == 1)
            {
                ctx.Check(games[0].Address == Loopback && games[0].Port == NetPlayFeature.DefaultPort && games[0].Advert == advert,
                    $"at the address it answered from, with the game port and advert it was handed ({games[0].Address}, {games[0].Port}, {games[0].Advert.Host})");
            }

            ctx.Check(responder.Answered == 1, $"the responder answered one query ({responder.Answered})");

            // ABLE-TO-FAIL CONTROL: a datagram that is not a query is read and never answered.
            ask.Send(Loopback, NetPorts.Lan, new byte[] { 0x43, 0x53, 0x56, 0x4D });
            Settle(() => responder.Poll(advert, NetPlayFeature.DefaultPort), search, () => false, seconds: 0.25);
            ctx.Check(responder.Answered == 1, $"a datagram that is not a query gets no answer ({responder.Answered})");

            search.Ask();
            Settle(() => { }, search, () => false, seconds: 0.25);
            ctx.Check(search.Games.Count == 1, $"a game that misses one round is still listed ({search.Games.Count})");
            search.Ask();
            Settle(() => { }, search, () => false, seconds: 0.25);
            ctx.Check(search.Games.Count == 0, $"and leaves once it misses a second ({search.Games.Count})");
        }
        finally
        {
            responder.Dispose();
            search.Dispose();
        }
    }

    [Suite("menu-original-boot",
        "The Multiplayer Lobby's password and Boot over the loopback: a host's GAME INFORMATION with no "
        + "master server greys its Listing chooser on Public, which neither a click nor a sideways step "
        + "flips; the host types a password into it and its "
        + "own PLAYER INFORMATION keeps the join's Password greyed. The games "
        + "list reads Need Password, and Join Game leaves PLAYER INFORMATION's Password live. A wrong "
        + "password is refused with Invalid Password on the Connection page before the host lists the "
        + "guest, and the right one lands it in the lobby. Boot is greyed until the host picks the "
        + "guest's row, then removes it: the guest lands on the Connection page told it was booted, "
        + "the host's chat reads the original's notice, and the guest's return from the same machine "
        + "is refused while a guest from another machine joins")]
    internal static void TheBootAndThePassword(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        // Nathan's three connections come from one machine: the wrong password, the right one, and
        // the return after the boot. Sheila's comes from another. A guest links only to its host.
        var lan = new LoopbackLan();
        var mesh = LoopbackTransport.Mesh(5, LoopbackConditions.Perfect, new Random(19));
        Unlink(mesh);
        mesh[4].Address = "192.168.1.23";
        var gate = new ArrivalGate(mesh[0]);
        var hostDoor = new NetPlayFeature(
            (_, _, _) => gate,
            (_, _) => throw new InvalidOperationException("the host does not join"),
            new RouterAccess(
                port => new UpnpPortMapResult(UpnpPortMapOutcome.Mapped, port, NetDoorAid.ExternalAddress, "suite"),
                _ => { }),
            lan.Bind);
        var nathanDoor = Arriving(gate, lan, mesh[1], mesh[2], mesh[3]);
        var sheilaDoor = Arriving(gate, lan, mesh[4]);
        var doors = new[] { hostDoor, nathanDoor, sheilaDoor };
        foreach (var door in doors)
        {
            door.BindAddress = Loopback;
            door.SearchAddress = Loopback;
        }

        var ends = new List<End>();
        string? options = MenuSuiteHost.ScratchOptions(ctx, "menu-original-boot");
        try
        {
            var host = Open(ctx, layout, hostDoor, ends);
            var nathan = Open(ctx, layout, nathanDoor, ends);
            var sheila = Open(ctx, layout, sheilaDoor, ends);
            if (host == null || nathan == null || sheila == null || !HostWithAPassword(ctx, host))
            {
                return;
            }

            var lobby = hostDoor.Dogfight!;
            int most = JoinWithPassword(ctx, nathan, ends, "Nathan", "sword", lobby);
            ctx.Check(nathan.Shell.Screen == OriginalScreen.Connection && nathan.Shell.Dialog?.Message == CoopDoorText.WrongPassword,
                $"a wrong password is refused with the original's words over the Connection page ({nathan.Shell.Screen}, {nathan.Shell.Dialog?.Message})");
            ctx.Check(most == 1 && hostDoor.Peers == 0,
                $"and the host never lists the refused guest ({most} rows at most, {hostDoor.Peers} guests)");
            ClickRow(ctx, nathan, OriginalShell.DialogOkKey);

            JoinWithPassword(ctx, nathan, ends, "Nathan", LobbyPassword, lobby);
            ctx.Check(nathan.Shell.Screen == OriginalScreen.Lobby && lobby.Players.Count == 2 && lobby.Players[1].Name == "Nathan",
                $"the right password lands the guest in the lobby ({nathan.Shell.Screen}, {lobby.Players.Count} rows, {nathan.Shell.Dialog?.Message})");
            BootFromTheLobby(ctx, host, nathan, ends);

            JoinWithPassword(ctx, nathan, ends, "Nathan", LobbyPassword, lobby);
            ctx.Check(nathan.Shell.Screen == OriginalScreen.Connection && nathan.Shell.Dialog?.Message == CoopDoorText.Booted && lobby.Players.Count == 1,
                $"the booted guest's return from the same machine is refused ({nathan.Shell.Screen}, {nathan.Shell.Dialog?.Message}, {lobby.Players.Count} rows)");
            JoinWithPassword(ctx, sheila, ends, "Sheila", LobbyPassword, lobby);
            ctx.Check(sheila.Shell.Screen == OriginalScreen.Lobby && lobby.Players.Count == 2 && lobby.Players[1].Name == "Sheila",
                $"ABLE-TO-FAIL CONTROL: a guest from another machine joins the same session ({sheila.Shell.Screen}, {lobby.Players.Count} rows)");
        }
        finally
        {
            foreach (var end in ends)
            {
                end.Host.Deactivate();
            }

            foreach (var door in doors)
            {
                door.Discard();
            }

            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            CSVM.Utils.OptionsStore.DirectoryOverride = options;
        }
    }

    [Suite("menu-original-coop-boot",
        "The cabin's BOOT over the loopback: the co-op host's BOOT is greyed with no guest seated, "
        + "then asks about each guest in player order. No moves to the next and boots nobody, and Yes "
        + "removes that guest, who lands on the Connection page told it was booted while the other "
        + "stays seated. The booted guest's return from the same machine is refused until CLOSE "
        + "NETWORK, after which a new session admits it")]
    internal static void TheCoopBoot(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        // The second host wire is the session opened after CLOSE NETWORK, with a mesh of its own.
        var lan = new LoopbackLan();
        var mesh = LoopbackTransport.Mesh(4, LoopbackConditions.Perfect, new Random(23));
        var next = LoopbackTransport.Mesh(2, LoopbackConditions.Perfect, new Random(29));
        Unlink(mesh);
        mesh[3].Address = "192.168.1.23";
        var gate = new ArrivalGate(mesh[0]);
        var nextGate = new ArrivalGate(next[0]);
        var hostWires = new Queue<INetTransport>(new INetTransport[] { gate, nextGate });
        var hostDoor = new NetPlayFeature(
            (_, _, _) => hostWires.Dequeue(),
            (_, _) => throw new InvalidOperationException("the host does not join"),
            new RouterAccess(
                port => new UpnpPortMapResult(UpnpPortMapOutcome.Mapped, port, NetDoorAid.ExternalAddress, "suite"),
                _ => { }),
            lan.Bind);
        var nathanWires = new Queue<(ArrivalGate Gate, LoopbackTransport End)>(new[] { (gate, mesh[1]), (gate, mesh[2]), (nextGate, next[1]) });
        var nathanDoor = new NetPlayFeature(
            (_, _, _) => throw new InvalidOperationException("a guest does not host"),
            (_, _) =>
            {
                var (to, end) = nathanWires.Dequeue();
                to.Arrive(end.LocalPeer);
                return new Hangup(end);
            },
            lan: lan.Bind);
        var sheilaDoor = Arriving(gate, lan, mesh[3]);
        var doors = new[] { hostDoor, nathanDoor, sheilaDoor };
        foreach (var door in doors)
        {
            door.BindAddress = Loopback;
            door.SearchAddress = Loopback;
        }

        var ends = new List<End>();
        string? options = MenuSuiteHost.ScratchOptions(ctx, "menu-original-coop-boot");
        try
        {
            var host = Open(ctx, layout, hostDoor, ends);
            var nathan = Open(ctx, layout, nathanDoor, ends);
            var sheila = Open(ctx, layout, sheilaDoor, ends);
            if (host == null || nathan == null || sheila == null)
            {
                return;
            }

            host.Shell.Campaign.OpenCampaignOver(
                CampaignAidProfiles.Store(seeded: true, progressed: true), CampaignAidProfiles.Planes());
            host.Shell.Campaign.ShowCabin(CampaignAidProfiles.Pilot);
            OpenForTheMatch(ctx, host, hostDoor);
            ctx.Check(Row(host.Shell, OriginalCampaignScreen.CoopBootKey) is { Enabled: false, Label: CoopDoorText.BootButton },
                $"ABLE-TO-FAIL CONTROL: the cabin's BOOT is greyed while no guest is seated ({Row(host.Shell, OriginalCampaignScreen.CoopBootKey)?.Enabled})");
            JoinThroughTheList(ctx, nathan, ends, 1, "the first guest");
            JoinThroughTheList(ctx, sheila, ends, 2, "the second guest");
            AskAndDecline(ctx, host, hostDoor, ends);
            ClickRow(ctx, host, OriginalCampaignScreen.CoopBootKey);
            ClickRow(ctx, host, OriginalShell.DialogYesKey);
            for (int frame = 0; frame < 4; frame++)
            {
                Pump(ends.ToArray());
            }

            ctx.Check(nathan.Shell.Screen == OriginalScreen.Connection && nathan.Shell.Dialog?.Message == CoopDoorText.Booted,
                $"Yes boots the first guest onto the Connection page, told why ({nathan.Shell.Screen}, {nathan.Shell.Dialog?.Message})");
            ctx.Check(hostDoor.CoopGuests.Count == 1 && hostDoor.CoopGuests[0].Name == "Sheila" && sheila.Door.IsCoopGuest
                      && !DrawsOver(host.Shell.Compose(), "Nathan" + LaunchMenu.RemoteChipMark),
                $"and the other guest stays seated while the host's chips drop the booted one ({string.Join(", ", hostDoor.CoopGuests.Select(g => g.Name))})");
            ClickRow(ctx, nathan, OriginalShell.DialogOkKey);
            Rejoin(ctx, nathan, ends);
            ctx.Check(nathan.Shell.Dialog?.Message == CoopDoorText.Booted && hostDoor.CoopGuests.Count == 1,
                $"the booted guest's return from the same machine is refused ({nathan.Shell.Dialog?.Message}, {hostDoor.CoopGuests.Count} guests)");
            ClickRow(ctx, nathan, OriginalShell.DialogOkKey);

            // CLOSE NETWORK ends the session and its ban list; the next one admits the same machine.
            ClickRow(ctx, host, OriginalCampaignScreen.CoopDoorKey);
            OpenForTheMatch(ctx, host, hostDoor);
            Rejoin(ctx, nathan, ends);
            ctx.Check(nathan.Door.IsCoopGuest && hostDoor.CoopGuests.Count == 1 && nathan.Shell.Dialog == null,
                $"a new session admits the machine the last one booted ({nathan.Door.Stage}, {hostDoor.CoopGuests.Count} guests, {nathan.Shell.Dialog?.Message})");
        }
        finally
        {
            foreach (var end in ends)
            {
                end.Host.Deactivate();
            }

            foreach (var door in doors)
            {
                door.Discard();
            }

            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            CSVM.Utils.OptionsStore.DirectoryOverride = options;
        }
    }

    [Suite("menu-original-master-list",
        "The games list with a master server set and no LAN socket: Connect over LAN TCP/IP opens the "
        + "list on the master server's one Dogfight, its row reads the listing's five columns, and "
        + "Join Game after PLAYER INFORMATION opens the join through the code opener with that game's "
        + "code rather than an address, landing the guest on the host's Dogfight. The host's GAME "
        + "INFORMATION Listing chooser is live and flips both ways, its lobby "
        + "pins the code its listed carrier was given, and the copy key copies the code. A second guest's "
        + "Connection page stands Join by code live as its third way, in the cursor's walk after the IP "
        + "Address box; Ctrl+V pastes the code into its box in small letters with no dash, and Enter there "
        + "asks PLAYER INFORMATION with its Password live and joins the code's written form through the "
        + "code opener, landing that guest on the Dogfight too. The server is a canned list and the wire "
        + "the loopback")]
    internal static void TheMasterServersGames(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        const string listed = "{\"games\":[{\"code\":\"K7Q-X3M\",\"name\":\"Pirates\",\"kind\":\"dogfight\",\"players\":1,"
            + "\"cap\":8,\"status\":\"waiting\",\"version\":\"unknown\"}]}";
        // The host's end is gated, so each guest reaches it only when that guest joins.
        var mesh = LoopbackTransport.Mesh(3, LoopbackConditions.Perfect, new Random(17));
        var gate = new ArrivalGate(mesh[0]);
        var copied = new List<string>();
        var hostDoor = new NetPlayFeature(
            (_, _, _) => NetDoorAid.Listed(gate), (_, _) => throw new InvalidOperationException("the host does not join"))
        {
            CopyText = copied.Add,
            Master = new MasterDirectory(_ => System.Threading.Tasks.Task.FromResult(listed)),
        };
        var opened = new List<string>();
        var guestDoor = CodeGuest(listed, opened, gate, mesh[1]);
        var typedOpened = new List<string>();
        var coderDoor = CodeGuest(listed, typedOpened, gate, mesh[2]);
        var ends = new List<End>();
        string? options = MenuSuiteHost.ScratchOptions(ctx, "menu-original-master-list");
        try
        {
            var host = Open(ctx, layout, hostDoor, ends);
            var guest = Open(ctx, layout, guestDoor, ends);
            var coder = Open(ctx, layout, coderDoor, ends);
            if (host == null || guest == null || coder == null)
            {
                return;
            }

            ShowTheCode(ctx, host, copied);

            var shell = guest.Shell;
            ClickRow(ctx, guest, OriginalShell.MultiplayerKey);
            ClickRow(ctx, guest, OriginalConnectionScreen.ConnectKey);
            ctx.Check(shell.Screen == OriginalScreen.ConnectionGames,
                $"Connect opens the games list with no LAN socket, the master server standing for the search ({shell.Screen})");
            for (int frame = 0; frame < 6 && shell.Connection.Listed.Count == 0; frame++)
            {
                hostDoor.Step(Dt);
                Pump(ends.ToArray());
            }

            var rows = shell.Connection.Listed;
            ctx.Check(rows.Count == 1 && rows[0].Code == "K7Q-X3M", $"the list carries the master server's one game ({rows.Count})");
            if (rows.Count != 1)
            {
                return;
            }

            var cells = shell.Connection.Cells(rows[0]);
            ctx.Check(cells.Count == 5 && cells[0] == "Pirates" && cells[1] == "1/8" && cells[2] == "Dogfight" && cells[4] == "Waiting",
                $"its row reads the listing's five columns ({string.Join(" | ", cells)})");
            ClickRow(ctx, guest, OriginalConnectionScreen.GameKey(0));
            ClickRow(ctx, guest, OriginalConnectionScreen.JoinKey);
            Answer(ctx, guest, "Nathan");
            for (int frame = 0; frame < 6; frame++)
            {
                hostDoor.Step(Dt);
                Pump(ends.ToArray());
            }

            ctx.Check(opened.SequenceEqual(new[] { "K7Q-X3M" }), $"Join Game opens the join by the game's code ({string.Join(", ", opened)})");
            ctx.Check(guestDoor.IsDogfightGuest && guestDoor.JoinName == "K7Q-X3M",
                $"and lands the guest on the host's Dogfight ({guestDoor.Stage}, {guestDoor.JoinName}, {guestDoor.Fault})");
            JoinByTheTypedCode(ctx, coder, ends, hostDoor, typedOpened);
        }
        finally
        {
            foreach (var end in ends)
            {
                end.Host.Deactivate();
            }

            guestDoor.Discard();
            coderDoor.Discard();
            hostDoor.Discard();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            CSVM.Utils.OptionsStore.DirectoryOverride = options;
        }
    }

    [Suite("menu-original-lobby-network",
        "A Dogfight host's lobby names its address only when it has no internet code. Three hosts open "
        + "through the Connection page's Host, each with a stable IPv6 address to name: one the master "
        + "server listed under a code, one with a master server set but no WebRTC carrier, and one with "
        + "no master server. The listed host's Network rows pin the code alone; the other two pin the "
        + "address, the WebRTC one with its reason under it. None of the three posts a chat note. The "
        + "server is a canned list and the wire the loopback")]
    internal static void TheLobbysNetworkRows(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        const string stable = "2001:db8::7";
        var coded = NamedHost(NetDoorAid.Listed, true, stable, 21);
        var offline = NamedHost(end => end, true, stable, 22);
        var lan = NamedHost(end => end, false, stable, 23);
        var ends = new List<End>();
        string? options = MenuSuiteHost.ScratchOptions(ctx, "menu-original-lobby-network");
        try
        {
            // Each host is opened by pointer clicks, so its lines name no copy key (CopyWay.Pointer).
            string[]? codeRows = HostTheLobby(ctx, layout, coded, ends);
            string code = $"Internet code {NetDoorAid.SampleCode}, public, on the games list.";
            ctx.Check(codeRows != null && codeRows.SequenceEqual(new[] { code }),
                $"with a code the Network rows pin the code alone ({Joined(codeRows)})");
            ctx.Check(codeRows != null && !codeRows.Any(row => row.Contains(stable, StringComparison.Ordinal)),
                $"and name no address");

            // The harness's port base is not the default port, so the address is written bracketed.
            string address = $"IPv6  [{stable}]:{offline.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            string[]? offlineRows = HostTheLobby(ctx, layout, offline, ends);
            ctx.Check(offlineRows != null && offlineRows.SequenceEqual(new[] { address, $"No internet code: {CoopDoorText.NoWebRtc}" }),
                $"without WebRTC they pin the address with the reason under it ({Joined(offlineRows)})");

            string[]? lanRows = HostTheLobby(ctx, layout, lan, ends);
            ctx.Check(lanRows != null && lanRows.SequenceEqual(new[] { address }),
                $"with no master server they pin the address ({Joined(lanRows)})");
        }
        finally
        {
            foreach (var end in ends)
            {
                end.Host.Deactivate();
            }

            coded.Discard();
            offline.Discard();
            lan.Discard();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            CSVM.Utils.OptionsStore.DirectoryOverride = options;
        }
    }

    [Suite("menu-original-copy-code",
        "A host copies its join code without a keyboard, in a Dogfight host's lobby and on a co-op host's "
        + "cabin band. The code's line carries a COPY control at its end. A click on the code copies it, a "
        + "tap with no hover before it copies it, and a pad's cursor walk reaches COPY, whose Accept copies "
        + "it. Each goes through the door's own copy and draws the line's copied state. The hint names "
        + "Ctrl+C after a key moved the cursor and no key after a pointer or a pad did. The listed carrier "
        + "stands for the master server and the wire is the loopback")]
    internal static void TheCopyControl(TestContext ctx)
    {
        ctx.RequireData(ctx.ZrdrPath, $"zrdr archive");
        ctx.RequireData(MenuLayout.PathUnder(ctx.DataRoot), $"decoded menu layout");
        var layout = OriginalAvailability.Load(ctx.DataRoot, out var why);
        ctx.Check(layout != null, $"the install's layout passes the availability check ({why ?? "ok"})");
        if (layout == null)
        {
            return;
        }

        var copied = new List<string>();
        var dogfightEnd = LoopbackTransport.Mesh(1, LoopbackConditions.Perfect, new Random(41))[0];
        var dogfight = new NetPlayFeature(
            (_, _, _) => NetDoorAid.Listed(dogfightEnd), (_, _) => throw new InvalidOperationException("the host does not join"))
        {
            CopyText = copied.Add,
            Master = new MasterDirectory(_ => System.Threading.Tasks.Task.FromResult("{\"games\":[]}")),
        };
        var coopEnd = LoopbackTransport.Mesh(1, LoopbackConditions.Perfect, new Random(42))[0];
        var coop = new NetPlayFeature(
            (_, _, _) => NetDoorAid.Listed(coopEnd),
            (_, _) => throw new InvalidOperationException("the host does not join"),
            new RouterAccess(
                port => new UpnpPortMapResult(UpnpPortMapOutcome.Mapped, port, NetDoorAid.ExternalAddress, "suite"),
                _ => { }))
        {
            CopyText = copied.Add,
        };
        var ends = new List<End>();
        string? options = MenuSuiteHost.ScratchOptions(ctx, "menu-original-copy-code");
        try
        {
            var lobby = Open(ctx, layout, dogfight, ends);
            var cabin = Open(ctx, layout, coop, ends);
            if (lobby == null || cabin == null)
            {
                return;
            }

            ClickRow(ctx, lobby, OriginalShell.MultiplayerKey);
            ClickRow(ctx, lobby, OriginalConnectionScreen.HostKey);
            Answer(ctx, lobby, "Zachary", "Pirates");
            Pump(lobby);
            ctx.Check(lobby.Shell.Screen == OriginalScreen.Lobby && dogfight.JoinCode == NetDoorAid.SampleCode,
                $"Host opens the lobby under the listed code ({lobby.Shell.Screen}, {dogfight.JoinCode})");
            string listed = $"Internet code {NetDoorAid.SampleCode}, public, on the games list.";
            CopyEveryWay(ctx, lobby, copied, OriginalLobbyScreen.CopyKey, "the lobby",
                $"{listed} {CoopDoorText.CopyPress} copies it.", listed, $"{listed} It is copied.");

            cabin.Shell.Campaign.OpenCampaignOver(CampaignAidProfiles.Store(seeded: true, progressed: true), CampaignAidProfiles.Planes());
            cabin.Shell.Campaign.ShowCabin(CampaignAidProfiles.Pilot);
            OpenForTheMatch(ctx, cabin, coop);
            string band = $"NETWORK OPEN  0 guests  CODE {NetDoorAid.SampleCode}";
            CopyEveryWay(ctx, cabin, copied, OriginalCampaignScreen.CoopCopyKey, "the cabin's band",
                $"{band}  {CoopDoorText.CopyPress}", band, $"{band}  copied");
        }
        finally
        {
            foreach (var end in ends)
            {
                end.Host.Deactivate();
            }

            dogfight.Discard();
            coop.Discard();
            Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
            CSVM.Utils.OptionsStore.DirectoryOverride = options;
        }
    }

    // One host's code line through every way. A key's step names Ctrl+C and a pointer's or a pad's
    // names none. The pad walks to COPY and accepts, then a click and a tap on the code's words each
    // copy again. Every copy is the door's, so the clipboard seam gains the code each time.
    private static void CopyEveryWay(
        TestContext ctx, End end, List<string> copied, string key, string where, string keysLine, string bareLine, string copiedLine)
    {
        string code = NetDoorAid.SampleCode;
        var row = Row(end.Shell, key);
        var neighbour = end.Shell.Rows.FirstOrDefault(other => row != null && other.Column == row.Column && other.Enabled && other.Visible && other.Key != key);
        ctx.Check(row != null && neighbour != null, $"{where} carries its {CoopDoorText.CopyButton} row beside another in its column ({neighbour?.Key})");
        if (row == null || neighbour == null)
        {
            return;
        }

        Press(end, new MenuCommands { MoveY = 1 });
        ctx.Check(end.Shell.CopyWay == CopyWay.Keys && DrawsExactly(end.Shell.Compose(), keysLine),
            $"after a key's step {where} names {CoopDoorText.CopyPress} ({end.Shell.CopyWay}, {Lines(end, code)})");

        // The pointer comes to rest on a row of the control's column, which is where the pad starts.
        Press(end, new MenuCommands { Pointer = Window(ctx, neighbour.X + (neighbour.Width / 2f), neighbour.Y + (neighbour.Height / 2f)) });
        ctx.Check(end.Shell.CopyWay == CopyWay.Pointer && DrawsExactly(end.Shell.Compose(), bareLine),
            $"after the pointer moved {where} names no key ({end.Shell.CopyWay}, {Lines(end, code)})");
        Press(end, new MenuCommands { MoveY = 1, OnPad = true });
        var board = end.Shell.Compose();
        ctx.Check(end.Shell.CopyWay == CopyWay.Pad && DrawsExactly(board, bareLine) && DrawsExactly(board, CoopDoorText.CopyButton),
            $"after a pad's step it names no key, its {CoopDoorText.CopyButton} control drawn ({end.Shell.CopyWay}, {Lines(end, code)})");

        // The walk leaves the control first when it starts there, so reaching it is the column's own.
        int steps = 0;
        int limit = end.Shell.Rows.Count + 2;
        do
        {
            Press(end, new MenuCommands { MoveY = 1, OnPad = true });
            steps++;
        }
        while (end.Shell.FocusedKey != key && steps < limit);
        ctx.Check(end.Shell.FocusedKey == key, $"the pad's cursor walk reaches {where}'s {CoopDoorText.CopyButton} ({end.Shell.FocusedKey}, {steps} steps)");
        int start = copied.Count;
        Press(end, new MenuCommands { Accept = true, KeylessAccept = true, OnPad = true });
        ctx.Check(copied.Count == start + 1 && copied[^1] == code && DrawsExactly(end.Shell.Compose(), copiedLine),
            $"the pad's Accept on it copies the code and the line says so ({string.Join(", ", copied)})");

        // A point on the drawn line's own words, read off the board rather than the row. The press
        // lands on the code itself, not only on the control.
        var words = end.Shell.Compose().Lines.FirstOrDefault(line => line.Text == copiedLine);
        ctx.Check(words != null, $"{where} draws the code's line ({Lines(end, code)})");
        if (words == null)
        {
            return;
        }

        // The board measures no text, so the code's place is its index at half an em per character.
        float wordsX = words.X + (words.Text.IndexOf(code, StringComparison.Ordinal) * words.Size * 0.5f) + words.Size;
        float wordsY = words.Y + (words.Size / 2f);
        int before = copied.Count;
        var at = Window(ctx, wordsX, wordsY);
        Press(end, new MenuCommands { Pointer = at });
        Press(end, new MenuCommands { Pointer = at with { Pressed = true, Clicked = true } });
        Press(end, new MenuCommands { Pointer = at });
        ctx.Check(copied.Count == before + 1 && copied[^1] == code && DrawsExactly(end.Shell.Compose(), copiedLine),
            $"a click on the code's words copies it ({copied.Count - before} copies, {Lines(end, code)})");

        Press(end, new MenuCommands { Pointer = Window(ctx, 2f, 598f) });
        var tap = Window(ctx, wordsX + 6f, wordsY);
        Press(end, new MenuCommands { Pointer = tap with { Pressed = true, Clicked = true } });
        Press(end, new MenuCommands { Pointer = tap });
        ctx.Check(copied.Count == before + 2 && copied[^1] == code && DrawsExactly(end.Shell.Compose(), copiedLine),
            $"a tap on them, the press landing where no hover stood, copies it again ({copied.Count - before} copies)");
    }

    // An authored point as the seat's pointer reports it, in window pixels.
    private static MenuPointer Window(TestContext ctx, float x, float y)
    {
        var size = ctx.Host.GetViewport().GetVisibleRect().Size;
        var fit = BoardFit.For(size.X, size.Y);
        return new MenuPointer(fit.X(x), fit.Y(y), false, false, 0);
    }

    private static bool DrawsExactly(ComposedBoard board, string text) => board.Lines.Any(line => line.Text == text);

    // The drawn lines naming the code, for a failure's message.
    private static string Lines(End end, string code) =>
        string.Join(" | ", end.Shell.Compose().Lines.Select(line => line.Text).Where(text => text.Contains(code, StringComparison.Ordinal)));

    // A Dogfight host door on its own loopback end, wrapped by carrier, naming a stable IPv6 address.
    // With master it has a master server set, an empty canned list.
    private static NetPlayFeature NamedHost(Func<INetTransport, INetTransport> carrier, bool master, string ipv6, int seed)
    {
        var end = LoopbackTransport.Mesh(1, LoopbackConditions.Perfect, new Random(seed))[0];
        return new NetPlayFeature((_, _, _) => carrier(end), (_, _) => throw new InvalidOperationException("the host does not join"))
        {
            Master = master ? new MasterDirectory(_ => System.Threading.Tasks.Task.FromResult("{\"games\":[]}")) : null,
            StableIpv6 = () => ipv6,
        };
    }

    // The Connection page's Host on its own Original end. Returns the lobby's pinned Network rows as
    // drawn, or null when no lobby opened.
    private static string[]? HostTheLobby(TestContext ctx, MenuLayout layout, NetPlayFeature door, List<End> ends)
    {
        var host = Open(ctx, layout, door, ends);
        if (host == null)
        {
            return null;
        }

        ClickRow(ctx, host, OriginalShell.MultiplayerKey);
        ClickRow(ctx, host, OriginalConnectionScreen.HostKey);
        Answer(ctx, host, "Zachary", "Pirates");
        Pump(host);
        Pump(host);
        var lobby = door.Dogfight;
        ctx.Check(host.Shell.Screen == OriginalScreen.Lobby && lobby != null, $"Host opens the lobby ({host.Shell.Screen}, {door.Stage})");
        if (lobby == null)
        {
            return null;
        }

        var notes = lobby.Chat.Where(line => line.Name == CoopDoorText.NoteName).Select(line => line.Text).ToList();
        ctx.Check(notes.Count == 0, $"the host's chat carries no {CoopDoorText.NoteName} note ({string.Join(" | ", notes)})");
        var board = host.Shell.Compose();
        var rows = host.Shell.Lobby.NetworkRows.ToArray();
        ctx.Check(rows.Length > 0 && board.Lines.Any(line => line.Text == CoopDoorText.NoteName) && rows.All(row => board.Lines.Any(line => line.Text == row)),
            $"the lobby draws its {CoopDoorText.NoteName} rows over the chat ({Joined(rows)})");
        ctx.Check(rows.Any(row => row.Contains(door.HostIpv6 ?? "-", StringComparison.Ordinal))
                  == board.Lines.Any(line => line.Text.Contains(door.HostIpv6 ?? "-", StringComparison.Ordinal)),
            $"and names the address nowhere else on the board");
        return rows;
    }

    private static string Joined(string[]? rows) => rows == null ? "no lobby" : string.Join(" | ", rows);

    // The Connection page's Host on a carrier the master server listed. The lobby pins the code over
    // its chat, and the copy key copies the code rather than an address.
    private static void ShowTheCode(TestContext ctx, End host, List<string> copied)
    {
        ClickRow(ctx, host, OriginalShell.MultiplayerKey);
        ClickRow(ctx, host, OriginalConnectionScreen.HostKey);
        var box = host.Shell.NetInfo;
        ctx.Check(Row(host.Shell, OriginalNetInfoBox.ListingKey) is { Enabled: true, Label: CoopDoorText.PublicWord },
            $"with a master server set the Listing chooser is live, on Public ({Row(host.Shell, OriginalNetInfoBox.ListingKey)?.Enabled})");
        ClickRow(ctx, host, OriginalNetInfoBox.ListingKey);
        bool flipped = box.Draft.Private == true && Row(host.Shell, OriginalNetInfoBox.PrivateKey) is { Enabled: false };
        ClickRow(ctx, host, OriginalNetInfoBox.PublicKey);
        ctx.Check(flipped && box.Draft.Private == false && Row(host.Shell, OriginalNetInfoBox.ListingKey)?.Label == CoopDoorText.PublicWord,
            $"it flips to Private and its up arrow back to Public ({flipped}, {box.Draft.Private})");
        Answer(ctx, host, "Zachary", "Pirates");
        Pump(host);
        var door = host.Door;
        // Opened by clicks, so the line names no copy key.
        string pinned = $"Internet code {NetDoorAid.SampleCode}, public, on the games list.";
        ctx.Check(host.Shell.Screen == OriginalScreen.Lobby && Draws(host.Shell.Compose(), pinned),
            $"the host's lobby pins its code over the chat ({host.Shell.Screen}, {CoopDoorText.HostCodeLine(door)})");
        bool took = door.CopyForGuests();
        Pump(host);
        ctx.Check(took && copied.SequenceEqual(new[] { NetDoorAid.SampleCode }) && Draws(host.Shell.Compose(), "It is copied."),
            $"the copy key copies the code and the line says so ({string.Join(", ", copied)})");
    }

    // A guest door over the canned master server whose code opener records the code and lets its
    // own end through the host's gate.
    private static NetPlayFeature CodeGuest(string listed, List<string> opened, ArrivalGate gate, INetTransport end) => new(
        (_, _, _) => throw new InvalidOperationException("a guest does not host"),
        (_, _) => throw new InvalidOperationException("a guest with a master server joins by code"))
    {
        Master = new MasterDirectory(_ => System.Threading.Tasks.Task.FromResult(listed)),
        OpenCode = code =>
        {
            opened.Add(code);
            gate.Arrive(end.LocalPeer);
            return end;
        },
    };

    // The third way, end to end. The cursor reaches it after the IP Address box. Ctrl+V pastes the
    // code without its dash, and Enter in the box joins the code's written form.
    private static void JoinByTheTypedCode(TestContext ctx, End guest, List<End> ends, NetPlayFeature hostDoor, List<string> opened)
    {
        var shell = guest.Shell;
        ClickRow(ctx, guest, OriginalShell.MultiplayerKey);
        ctx.Check(Row(shell, OriginalConnectionScreen.CodeKey) is { Enabled: true } && Row(shell, OriginalConnectionScreen.CodeBoxKey) is { Enabled: true }
                  && Draws(shell.Compose(), OriginalConnectionScreen.CodeWayDescription) && Draws(shell.Compose(), OriginalConnectionScreen.CodeWayName),
            $"with a master server set the Connection page stands Join by code live under its description ({shell.Screen}, {shell.Connection.CodeFault})");
        ClickRow(ctx, guest, OriginalConnectionScreen.LanKey);
        var walked = new List<string> { shell.FocusedKey };
        for (int step = 0; step < 4; step++)
        {
            TypeInto(guest, new MenuCommands { MoveY = 1 });
            walked.Add(shell.FocusedKey);
        }

        string[] order =
        {
            OriginalConnectionScreen.LanKey, OriginalConnectionScreen.InternetKey, OriginalConnectionScreen.AddressKey,
            OriginalConnectionScreen.CodeKey, OriginalConnectionScreen.CodeBoxKey,
        };
        ctx.Check(walked.SequenceEqual(order) && shell.Connection.CapturingText,
            $"the cursor walks LAN TCP/IP, Internet, its box, Join by code and into the code box, which takes the keyboard ({string.Join(", ", walked)})");

        var pilots = MenuInput.Clipboard;
        var cues = new List<string>();
        try
        {
            MenuInput.Clipboard = () => " k7qx3m\r\n";
            shell.Connection.TypeText(new MenuCommands { Paste = true }, cues);
        }
        finally
        {
            MenuInput.Clipboard = pilots;
        }

        ctx.Check(shell.Connection.TypedCode == "K7QX3M" && shell.Connection.Way == OriginalConnectionScreen.CodeKey
                  && cues.SequenceEqual(new[] { OriginalCues.Text }),
            $"Ctrl+V pastes the clipboard's code into the box, trimmed and in capitals, and picks the way ('{shell.Connection.TypedCode}', {shell.Connection.Way}, {string.Join(" ", cues)})");
        TypeInto(guest, new MenuCommands { Accept = true });
        ctx.Check(shell.NetInfo.Page == NetInfoPage.Player && Row(shell, OriginalNetInfoBox.PlayerPasswordKey) is { Enabled: true } && opened.Count == 0,
            $"Enter in the code box asks PLAYER INFORMATION first, its Password live, before anything opens ({shell.NetInfo.Page}, {opened.Count})");
        Answer(ctx, guest, "Sheila");
        for (int frame = 0; frame < 6; frame++)
        {
            hostDoor.Step(Dt);
            Pump(ends.ToArray());
        }

        var door = guest.Door;
        ctx.Check(opened.SequenceEqual(new[] { NetDoorAid.SampleCode }) && door.Address == NetPlayFeature.DefaultAddress,
            $"the join opens through the code opener under the code's written form, the IP Address box left as it was ({string.Join(", ", opened)}, '{door.Address}')");
        ctx.Check(door.IsDogfightGuest && door.JoinName == NetDoorAid.SampleCode && hostDoor.Peers == 2,
            $"and lands that guest on the host's Dogfight beside the first ({door.Stage}, {door.JoinName}, {door.Fault}, {hostDoor.Peers} guests)");
    }

    // The Connection page's Host with a password typed into GAME INFORMATION. The host's own PLAYER
    // INFORMATION keeps the join's Password greyed. True when the lobby opened.
    private static bool HostWithAPassword(TestContext ctx, End host)
    {
        ClickRow(ctx, host, OriginalShell.MultiplayerKey);
        ClickRow(ctx, host, OriginalConnectionScreen.HostKey);
        var box = host.Shell.NetInfo;
        box.Draft.GameName = LobbyGame;
        // No master server is set on this door, so the chooser stands greyed and takes nothing.
        var listing = Row(host.Shell, OriginalNetInfoBox.ListingKey);
        ctx.Check(listing is { Enabled: false, Label: CoopDoorText.PublicWord } && Row(host.Shell, OriginalNetInfoBox.PrivateKey) is { Enabled: false },
            $"a Dogfight's GAME INFORMATION with no master server greys its Listing chooser on Public ({listing?.Enabled}, {listing?.Label})");
        ClickRow(ctx, host, OriginalNetInfoBox.ListingKey);
        var rows = new List<OriginalRow>();
        box.Rows(rows);
        bool stepped = box.StepSideways(rows, rows.FindIndex(row => row.Key == OriginalNetInfoBox.ListingKey), 1);
        ctx.Check(box.Draft.Private == false && !stepped,
            $"and neither a click nor a sideways step flips it ({box.Draft.Private}, {stepped})");
        ClickRow(ctx, host, OriginalNetInfoBox.PasswordKey);
        TypeInto(host, new MenuCommands { Typed = LobbyPassword });
        ctx.Check(box.Draft.Password == LobbyPassword && Row(host.Shell, OriginalNetInfoBox.PasswordKey)?.Label == new string('*', LobbyPassword.Length),
            $"GAME INFORMATION's Password box takes the typed password and shows it masked ({Row(host.Shell, OriginalNetInfoBox.PasswordKey)?.Label})");
        ClickRow(ctx, host, OriginalNetInfoBox.OkKey);
        ctx.Check(box.Page == NetInfoPage.Player && Row(host.Shell, OriginalNetInfoBox.PlayerPasswordKey) is { Enabled: false },
            $"the host's own PLAYER INFORMATION keeps the join's Password greyed ({box.Page})");
        box.Draft.Callsign = "Zachary";
        ClickRow(ctx, host, OriginalNetInfoBox.OkKey);
        Pump(host);
        var door = host.Door;
        ctx.Check(host.Shell.Screen == OriginalScreen.Lobby && door.Advertising is { Password: true } && door.Password == LobbyPassword
                  && !door.Private,
            $"Host opens the lobby Public and its advert says it asks a password ({host.Shell.Screen}, {door.Advertising?.Password}, {door.Private})");
        return host.Shell.Screen == OriginalScreen.Lobby && door.Dogfight != null;
    }

    // A guest's walk to the password lobby: Connect, the list's Need Password, Join Game with the
    // password typed into PLAYER INFORMATION. Returns the most rows the host's list held meanwhile.
    private static int JoinWithPassword(TestContext ctx, End guest, List<End> ends, string callsign, string password, DogfightLobby hostLobby)
    {
        var shell = guest.Shell;
        if (shell.Screen != OriginalScreen.Connection)
        {
            ClickRow(ctx, guest, OriginalShell.MultiplayerKey);
        }

        ClickRow(ctx, guest, OriginalConnectionScreen.ConnectKey);
        for (int frame = 0; frame < 6 && shell.Connection.Listed.Count == 0; frame++)
        {
            Pump(ends.ToArray());
        }

        if (shell.Connection.Listed.Count != 1)
        {
            ctx.Check(false, $"the search lists the one open game ({shell.Connection.Listed.Count})");
            return 0;
        }

        var cells = shell.Connection.Cells(shell.Connection.Listed[0]);
        ctx.Check(cells.Count == 5 && cells[4] == CoopDoorText.NeedPassword,
            $"the games list marks the game Need Password ({string.Join(" | ", cells)})");
        ClickRow(ctx, guest, OriginalConnectionScreen.GameKey(0));
        ClickRow(ctx, guest, OriginalConnectionScreen.JoinKey);
        var box = shell.NetInfo;
        ctx.Check(box.Page == NetInfoPage.Player && Row(shell, OriginalNetInfoBox.PlayerPasswordKey) is { Enabled: true },
            $"Join Game stands PLAYER INFORMATION with its Password live ({box.Page})");
        box.Draft.Callsign = callsign;
        ClickRow(ctx, guest, OriginalNetInfoBox.PlayerPasswordKey);
        TypeInto(guest, new MenuCommands { Typed = password });
        ctx.Check(box.Draft.Password == password, $"its Password box takes the typed password ({box.Draft.Password.Length} characters)");
        ClickRow(ctx, guest, OriginalNetInfoBox.OkKey);
        int most = 0;
        for (int frame = 0; frame < 8; frame++)
        {
            Pump(ends.ToArray());
            most = Math.Max(most, hostLobby.Players.Count);
        }

        return most;
    }

    // The host picks the guest's row and presses Boot, which is greyed until the row is picked.
    private static void BootFromTheLobby(TestContext ctx, End host, End guest, List<End> ends)
    {
        var screen = host.Shell.Lobby;
        ctx.Check(Row(host.Shell, OriginalLobbyScreen.BootKey) is { Enabled: false } && Row(host.Shell, OriginalLobbyScreen.PlayerKey(0)) == null,
            $"ABLE-TO-FAIL CONTROL: Boot is greyed with no row picked, and the host's own row is not offered");
        ctx.Check(Row(guest.Shell, OriginalLobbyScreen.PlayerKey(1)) == null && Row(guest.Shell, OriginalLobbyScreen.BootKey) is { Enabled: false },
            $"a guest's list offers no row and its Boot stays greyed");
        ClickRow(ctx, host, OriginalLobbyScreen.PlayerKey(1));
        ctx.Check(screen.PickedPeer >= 0 && Row(host.Shell, OriginalLobbyScreen.BootKey) is { Enabled: true },
            $"picking the guest's row makes Boot live ({screen.PickedPeer})");
        ClickRow(ctx, host, OriginalLobbyScreen.BootKey);
        for (int frame = 0; frame < 6; frame++)
        {
            Pump(ends.ToArray());
        }

        var lobby = host.Door.Dogfight!;
        ctx.Check(guest.Shell.Screen == OriginalScreen.Connection && guest.Shell.Dialog?.Message == CoopDoorText.Booted,
            $"Boot puts the guest on the Connection page, told it was booted ({guest.Shell.Screen}, {guest.Shell.Dialog?.Message})");
        ctx.Check(lobby.Players.Count == 1 && lobby.Chat.Any(line => line.Text == "[Nathan was booted from the game.]")
                  && Draws(host.Shell.Compose(), "[Nathan was booted from the game.]") && screen.PickedPeer < 0,
            $"the host's list drops the guest and its chat reads the original's notice ({lobby.Players.Count} rows)");
        ClickRow(ctx, guest, OriginalShell.DialogOkKey);
    }

    // BOOT asks about the first guest with Yes, No and Cancel; No asks about the second with Yes and
    // No; No again boots nobody.
    private static void AskAndDecline(TestContext ctx, End host, NetPlayFeature hostDoor, List<End> ends)
    {
        ClickRow(ctx, host, OriginalCampaignScreen.CoopBootKey);
        ctx.Check(host.Shell.Dialog?.Message == CoopDoorText.BootQuestion("Nathan") && host.Shell.Dialog.Answers.Count == 3,
            $"BOOT asks about the first guest with three answers ({host.Shell.Dialog?.Message})");
        ClickRow(ctx, host, OriginalShell.DialogNoKey);
        ctx.Check(host.Shell.Dialog?.Message == CoopDoorText.BootQuestion("Sheila") && host.Shell.Dialog.Answers.Count == 2,
            $"No moves on to the last guest with two ({host.Shell.Dialog?.Message})");
        ClickRow(ctx, host, OriginalShell.DialogNoKey);
        Pump(ends.ToArray());
        ctx.Check(host.Shell.Dialog == null && hostDoor.CoopGuests.Count == 2,
            $"ABLE-TO-FAIL CONTROL: declining both boots nobody ({hostDoor.CoopGuests.Count} guests)");
    }

    // A co-op guest's plain rejoin through the list, followed until the host answers.
    private static void Rejoin(TestContext ctx, End guest, List<End> ends)
    {
        if (guest.Shell.Screen != OriginalScreen.Connection)
        {
            ClickRow(ctx, guest, OriginalShell.MultiplayerKey);
        }

        ClickRow(ctx, guest, OriginalConnectionScreen.ConnectKey);
        for (int frame = 0; frame < 6 && guest.Shell.Connection.Listed.Count == 0; frame++)
        {
            Pump(ends.ToArray());
        }

        ClickRow(ctx, guest, OriginalConnectionScreen.GameKey(0));
        ClickRow(ctx, guest, OriginalConnectionScreen.JoinKey);
        Answer(ctx, guest, "Nathan");
        for (int frame = 0; frame < 6; frame++)
        {
            Pump(ends.ToArray());
        }
    }

    // A guest door whose joins take the next of its wires, each arriving at the host's gate.
    private static NetPlayFeature Arriving(ArrivalGate gate, LoopbackLan lan, params LoopbackTransport[] wires)
    {
        var queue = new Queue<LoopbackTransport>(wires);
        return new NetPlayFeature(
            (_, _, _) => throw new InvalidOperationException("a guest does not host"),
            (_, _) =>
            {
                var end = queue.Dequeue();
                gate.Arrive(end.LocalPeer);
                return new Hangup(end);
            },
            lan: lan.Bind);
    }

    // The loopback links every end to every other; a real guest links only to its host, end 0.
    private static void Unlink(IReadOnlyList<LoopbackTransport> mesh)
    {
        for (int i = 1; i < mesh.Count; i++)
        {
            for (int j = i + 1; j < mesh.Count; j++)
            {
                mesh[i].Disconnect(mesh[j].LocalPeer);
            }
        }
    }

    // The Connection page's Host, its two boxes answered by keyboard and pointer, the door's advert
    // and the lobby it opens. True when it opened.
    private static bool HostTheLobby(TestContext ctx, End host)
    {
        ClickRow(ctx, host, OriginalShell.MultiplayerKey);
        ctx.Check(Row(host.Shell, OriginalConnectionScreen.HostKey) is { Enabled: true },
            $"Host is live on the Connection page over the network door ({host.Shell.Screen})");
        ClickRow(ctx, host, OriginalConnectionScreen.HostKey);
        var box = host.Shell.NetInfo;
        var door = host.Door;
        ctx.Check(box.Page == NetInfoPage.Game && host.Shell.Screen == OriginalScreen.Connection && door.Stage == NetDoorStage.Shut,
            $"Host stands GAME INFORMATION over the Connection page before any socket opens ({box.Page}, {host.Shell.Screen}, {door.Stage})");
        TypeInto(host, Erasing(NetPlayerInfo.GameNameLimit).Append(new MenuCommands { Typed = LobbyGame }).ToArray());
        ctx.Check(box.Draft.GameName == LobbyGame && box.Draft.MaxPlayers == NetPlayerInfo.DefaultPlayers,
            $"the Game Name box takes the typed name and the spinner opens on eight ({box.Draft.GameName}, {box.Draft.MaxPlayers})");
        ClickRow(ctx, host, OriginalNetInfoBox.FewerKey);
        ClickRow(ctx, host, OriginalNetInfoBox.FewerKey);
        ctx.Check(box.Draft.MaxPlayers == LobbyCap, $"two presses of the spinner's down arrow make six ({box.Draft.MaxPlayers})");
        ClickRow(ctx, host, OriginalNetInfoBox.OkKey);
        ctx.Check(box.Page == NetInfoPage.Player && door.Stage == NetDoorStage.Shut,
            $"OK hands on to PLAYER INFORMATION with the socket still shut ({box.Page}, {door.Stage})");
        RefuseTheEmptyCallsign(ctx, host, "Zachary");
        ClickRow(ctx, host, OriginalNetInfoBox.OkKey);
        Pump(host);
        ctx.Check(host.Shell.Screen == OriginalScreen.Lobby && door.Dogfight is { IsHost: true },
            $"Host opens the lobby as a Dogfight's host ({host.Shell.Screen}, {door.Stage})");
        ctx.Check(door.Advertising is { Kind: NetSessionKind.Dogfight, MissionSeq: 0, Status: NetSessionStatus.Waiting, Host: LobbyGame, Cap: LobbyCap },
            $"and advertises a waiting Dogfight on the first environment under its game's name and cap ({door.Advertising?.Kind}, {door.Advertising?.MissionSeq}, {door.Advertising?.Status}, {door.Advertising?.Host}, {door.Advertising?.Cap})");
        ctx.Check(door.Dogfight?.Players.FirstOrDefault().Name == "Zachary" && Draws(host.Shell.Compose(), $"of {LobbyCap})"),
            $"the host's own row goes by its callsign, and the list counts against the chosen cap ({door.Dogfight?.Players.FirstOrDefault().Name})");
        ctx.Check(CSVM.Utils.OptionsStore.UserOptions().Load() is { NetCallsign: "Zachary", NetGameName: LobbyGame },
            $"the callsign and the game's name are remembered for the next session ({CSVM.Utils.OptionsStore.UserOptions().Load().NetCallsign})");
        return host.Shell.Screen == OriginalScreen.Lobby;
    }

    // Build Custom Plane from the Connection page: the keyboard walk and Enter, Back, the hub's
    // CANCEL and a purchase, each landing back on the page. True once the plane is in the store.
    private static bool BuildFromTheConnectionPage(TestContext ctx, End end, CustomPlaneStore store, string name)
    {
        var shell = end.Shell;
        var hangar = end.Host.Features.Get<HangarFeature>();
        ClickRow(ctx, end, OriginalShell.MultiplayerKey);
        ctx.Check(shell.Screen == OriginalScreen.Connection && Row(shell, OriginalConnectionScreen.BuildKey) is { Enabled: true },
            $"Build Custom Plane is live on the Connection page ({shell.Screen}, {Row(shell, OriginalConnectionScreen.BuildKey)?.Enabled})");
        ClickRow(ctx, end, OriginalConnectionScreen.InternetKey);
        var walked = new List<string>();
        for (int step = 0; step < 4 && shell.FocusedKey != OriginalConnectionScreen.BuildKey; step++)
        {
            Press(end, new MenuCommands { MoveY = 1 });
            walked.Add(shell.FocusedKey ?? "none");
        }

        ctx.Check(shell.FocusedKey == OriginalConnectionScreen.BuildKey && walked.Contains(OriginalConnectionScreen.AddressKey),
            $"the keyboard's walk reaches it under the IP Address box ({string.Join(" > ", walked)})");
        Press(end, new MenuCommands { Accept = true });
        ctx.Check(shell.Screen == OriginalScreen.PlaneName && hangar.IsOpen && hangar.Wallet == null,
            $"Enter on it opens the name screen over a wallet-free build ({shell.Screen}, {hangar.IsOpen})");
        Press(end, new MenuCommands { Back = true });
        ctx.Check(shell.Screen == OriginalScreen.Connection && !hangar.IsOpen && shell.FocusedKey == OriginalConnectionScreen.BuildKey,
            $"Back drops the build and lands back on the Connection page on the button ({shell.Screen}, {shell.FocusedKey})");

        if (!NameTheBuild(ctx, end, name))
        {
            return false;
        }

        ClickRow(ctx, end, OriginalHangarScreen.CancelBuildKey);
        ctx.Check(shell.Screen == OriginalScreen.Connection && !hangar.IsOpen && store.List().Count == 0,
            $"the hub's CANCEL lands back on the Connection page with nothing saved ({shell.Screen}, {store.List().Count})");

        if (!NameTheBuild(ctx, end, name))
        {
            return false;
        }

        ClickRow(ctx, end, OriginalHangarScreen.ReadyKey);
        ClickRow(ctx, end, OriginalHangarScreen.PurchaseNowKey);
        var saved = store.Load(name);
        ctx.Check(shell.Screen == OriginalScreen.Connection && !hangar.IsOpen && shell.FocusedKey == OriginalConnectionScreen.BuildKey,
            $"a purchase lands back on the Connection page on the button ({shell.Screen}, {shell.FocusedKey})");
        ctx.Check(saved is { AwaitingExport: false } && saved.Airframe == HangarFeature.DefaultAirframe,
            $"and saves an exported build on the default airframe ({saved?.Airframe}, {saved?.AwaitingExport})");
        return saved != null;
    }

    // The Connection page's Build Custom Plane, a typed name and OK, standing on the hub.
    private static bool NameTheBuild(TestContext ctx, End end, string name)
    {
        ClickRow(ctx, end, OriginalConnectionScreen.BuildKey);
        Press(end, new MenuCommands { Typed = name });
        ClickRow(ctx, end, OriginalHangarScreen.NameOkKey);
        bool hub = end.Shell.Screen == OriginalScreen.HangarAirframe && end.Shell.Hangar?.HangarName == name;
        ctx.Check(hub, $"a click, a typed name and OK stand on the hub over that name ({end.Shell.Screen}, {end.Shell.Hangar?.HangarName})");
        return hub;
    }

    // PLAYER INFORMATION's OK is greyed while the callsign is empty, and a callsign of spaces alone
    // raises the original's refusal. Then the callsign is typed into its box.
    private static void RefuseTheEmptyCallsign(TestContext ctx, End end, string callsign)
    {
        var box = end.Shell.NetInfo;
        ClickRow(ctx, end, OriginalNetInfoBox.CallsignKey);
        TypeInto(end, Erasing(NetPlayerInfo.CallsignLimit).ToArray());
        ctx.Check(box.Draft.Callsign.Length == 0 && Row(end.Shell, OriginalNetInfoBox.OkKey) is { Enabled: false },
            $"an empty callsign greys OK ('{box.Draft.Callsign}')");
        TypeInto(end, new MenuCommands { Typed = "   " });
        ClickRow(ctx, end, OriginalNetInfoBox.OkKey);
        ctx.Check(end.Shell.Dialog?.Message == "Invalid Callsign." && box.Page == NetInfoPage.Player && box.Draft.Callsign.Length == 0,
            $"a callsign of spaces alone raises the refusal and empties the box ({end.Shell.Dialog?.Message}, '{box.Draft.Callsign}')");
        ClickRow(ctx, end, OriginalShell.DialogOkKey);
        ClickRow(ctx, end, OriginalNetInfoBox.CallsignKey);
        TypeInto(end, new MenuCommands { Typed = callsign });
        ctx.Check(box.Draft.Callsign == callsign && Row(end.Shell, OriginalNetInfoBox.OkKey) is { Enabled: true },
            $"a typed callsign makes OK live ('{box.Draft.Callsign}')");
    }

    private static IEnumerable<MenuCommands> Erasing(int count) =>
        Enumerable.Range(0, count).Select(_ => new MenuCommands { Erase = true });

    // Answers the network boxes a press stood over the page: Game Information when it stands, then
    // Player Information, each through its OK.
    private static void Answer(TestContext ctx, End end, string callsign, string? game = null)
    {
        var box = end.Shell.NetInfo;
        ctx.Check(box.IsOpen, $"the press stands a network box over the page ({end.Shell.Screen})");
        if (box.Page == NetInfoPage.Game)
        {
            box.Draft.GameName = game ?? callsign;
            ClickRow(ctx, end, OriginalNetInfoBox.OkKey);
        }

        if (box.Page == NetInfoPage.Player)
        {
            box.Draft.Callsign = callsign;
            ClickRow(ctx, end, OriginalNetInfoBox.OkKey);
        }
    }

    // A guest's walk to the lobby: the plaque, Connect, the one row that reads as a Dogfight, Join.
    // Player Information is answered with a callsign and a voice the host then reads.
    private static bool JoinTheLobby(TestContext ctx, End guest, List<End> ends, int peer = -1, string callsign = "Nathan", int voice = 5)
    {
        var shell = guest.Shell;
        ClickRow(ctx, guest, OriginalShell.MultiplayerKey);
        ClickRow(ctx, guest, OriginalConnectionScreen.ConnectKey);
        for (int frame = 0; frame < 6 && shell.Connection.Listed.Count == 0; frame++)
        {
            Pump(ends.ToArray());
        }

        if (shell.Connection.Listed.Count != 1)
        {
            ctx.Check(false, $"the search lists the one open game ({shell.Connection.Listed.Count})");
            return false;
        }

        var cells = shell.Connection.Cells(shell.Connection.Listed[0]);
        ctx.Check(cells.Count == 5 && cells[0] == LobbyGame && cells[2] == "Dogfight" && cells[3] == DogfightLobby.EnvironmentName(0)
                  && cells[4] == "Waiting" && cells[1].EndsWith($"/{LobbyCap}", StringComparison.Ordinal),
            $"the games list reads the host's game name, the Dogfight, its environment, n of the chosen cap and Waiting ({string.Join(" | ", cells)})");
        ClickRow(ctx, guest, OriginalConnectionScreen.GameKey(0));
        ClickRow(ctx, guest, OriginalConnectionScreen.JoinKey);
        var box = shell.NetInfo;
        ctx.Check(box.Page == NetInfoPage.Player && shell.Screen == OriginalScreen.ConnectionGames && guest.Door.Stage == NetDoorStage.Shut,
            $"Join Game stands PLAYER INFORMATION over the games list before the join ({box.Page}, {shell.Screen}, {guest.Door.Stage})");
        if (peer >= 0)
        {
            RefuseTheEmptyCallsign(ctx, guest, callsign);
            ClickRow(ctx, guest, OriginalNetInfoBox.VoiceKey);
            ctx.Check(box.VoiceListOpen && Row(shell, OriginalNetInfoBox.VoiceKey + ":6") is { Label: "Texan Male" },
                $"the Voice box opens the seven voices ({box.VoiceListOpen}, {Row(shell, OriginalNetInfoBox.VoiceKey + ":6")?.Label})");
            ClickRow(ctx, guest, OriginalNetInfoBox.VoiceKey + ":" + voice.ToString(System.Globalization.CultureInfo.InvariantCulture));
            ClickRow(ctx, guest, OriginalNetInfoBox.OkKey);
        }
        else
        {
            Answer(ctx, guest, callsign);
        }

        for (int frame = 0; frame < 6; frame++)
        {
            Pump(ends.ToArray());
        }

        ctx.Check(shell.Screen == OriginalScreen.Lobby && guest.Door.Dogfight is { IsHost: false, HasOptions: true },
            $"Join Game lands the guest in the lobby with the host's options ({shell.Screen}, {guest.Door.Stage}, {shell.Dialog?.Message})");
        var hostList = ends[0].Door.Dogfight?.Players ?? Array.Empty<DogfightLobbySeat>();
        ctx.Check(hostList.Any(row => row.Name == callsign) && guest.Door.Dogfight?.Players.Any(row => row.Name == callsign) == true,
            $"the guest's callsign stands in the lobby's player list on both ends ({string.Join(", ", hostList.Select(row => row.Name))})");
        if (peer >= 0)
        {
            ctx.Check(ends[0].Door.PickedVoice(peer) == voice,
                $"the guest's voice reaches the host in its pick ({ends[0].Door.PickedVoice(peer)} for {voice})");
        }

        return shell.Screen == OriginalScreen.Lobby;
    }

    // The second guest's Leave Game: back on the Connection page, and out of the host's count.
    private static void LeaveTheLobby(TestContext ctx, End host, End leaver, List<End> ends)
    {
        var lobby = host.Door.Dogfight!;
        ctx.Check(lobby.Players.Count == 3, $"the host lists three players ({lobby.Players.Count})");
        ClickRow(ctx, leaver, OriginalLobbyScreen.LeaveKey);
        ctx.Check(leaver.Shell.Screen == OriginalScreen.Connection && leaver.Door.Stage == NetDoorStage.Shut,
            $"Leave Game lands on the Connection page with the door shut ({leaver.Shell.Screen}, {leaver.Door.Stage})");
        for (int frame = 0; frame < 4; frame++)
        {
            Pump(ends.ToArray());
        }

        ctx.Check(lobby.Players.Count == 2, $"and the host's list drops the pilot who left ({lobby.Players.Count})");
        CreateFromTheList(ctx, leaver, ends);
    }

    // The games list's Create Game is the second door to a hosted lobby, left again at once.
    private static void CreateFromTheList(TestContext ctx, End leaver, List<End> ends)
    {
        var shell = leaver.Shell;
        ClickRow(ctx, leaver, OriginalConnectionScreen.ConnectKey);
        for (int frame = 0; frame < 6 && shell.Connection.Listed.Count == 0; frame++)
        {
            Pump(ends.ToArray());
        }

        ctx.Check(Row(shell, OriginalConnectionScreen.CreateKey) is { Enabled: true },
            $"Create Game is live on the games list over the network door ({shell.Screen}, {shell.Connection.Listed.Count} listed)");
        if (Row(shell, OriginalConnectionScreen.CreateKey) is not { Enabled: true })
        {
            return;
        }

        ClickRow(ctx, leaver, OriginalConnectionScreen.CreateKey);
        ctx.Check(shell.NetInfo.Page == NetInfoPage.Game, $"Create Game asks Game Information over the games list ({shell.NetInfo.Page})");
        Answer(ctx, leaver, "Sheila", "Sheila's game");
        Pump(leaver);
        ctx.Check(shell.Screen == OriginalScreen.Lobby && leaver.Door.Dogfight is { IsHost: true },
            $"Create Game opens the lobby as a Dogfight's host ({shell.Screen}, {leaver.Door.Stage}, {shell.Dialog?.Message})");
        if (shell.Screen == OriginalScreen.Lobby)
        {
            ClickRow(ctx, leaver, OriginalLobbyScreen.LeaveKey);
        }

        ctx.Check(shell.Screen == OriginalScreen.Connection && leaver.Door.Stage == NetDoorStage.Shut,
            $"and its Leave Game shuts the door again ({shell.Screen}, {leaver.Door.Stage})");
    }

    // The host's map, Time 5 and Limited Lives, read on the guest. The guest's own boxes are dead.
    private static void SetTheOptions(TestContext ctx, End host, End guest, List<End> ends)
    {
        ctx.Check(Row(guest.Shell, OriginalLobbyScreen.EnvironmentKey) is { Enabled: false }
                  && Row(guest.Shell, OriginalLobbyScreen.TimeRadioKey) is { Enabled: false }
                  && guest.Door.Dogfight!.SetEnvironment(3) == false,
            $"a guest's option controls are greyed and its option set is refused");
        ctx.Check(Row(host.Shell, OriginalLobbyScreen.TypeKey) is { Enabled: true } && host.Door.Dogfight!.SetMissionType((DogfightMissionType)3) == false,
            $"the host's Type box is live but a type past the box's three is refused");
        TypeDescriptions(ctx, host, guest, ends);
        ClickRow(ctx, host, OriginalLobbyScreen.EnvironmentKey);
        ctx.Check(host.Shell.Lobby.OpenDropdown == OriginalLobbyScreen.EnvironmentKey, $"the Environment box opens its list ({host.Shell.Lobby.OpenDropdown})");
        ClickRow(ctx, host, OriginalLobbyScreen.EnvironmentKey + ":3");
        ClickRow(ctx, host, OriginalLobbyScreen.TimeKey);
        TypeInto(host, new MenuCommands { Erase = true }, new MenuCommands { Erase = true }, new MenuCommands { Typed = "5" });
        ClickRow(ctx, host, OriginalLobbyScreen.LimitedLivesKey);
        for (int frame = 0; frame < 4; frame++)
        {
            Pump(ends.ToArray());
        }

        var heard = guest.Door.Dogfight!.Options;
        ctx.Check(heard is { Environment: 3, TimeMinutes: 5, LimitedLives: true, Victory: DogfightVictory.Time },
            $"the guest reads the host's map, Time 5 and Limited Lives ({heard.Environment}, {heard.TimeMinutes}, {heard.LimitedLives}, {heard.Victory})");
        ctx.Check(host.Door.Advertising?.MissionSeq == 3, $"and the advert names the new environment ({host.Door.Advertising?.MissionSeq})");
    }

    // The page under the Type box describes the type on both ends, as the mission script's type change
    // fills its text box. Langui 10123 is a Deathmatch, 10124 Capture the Flag and 10125 Zeppelin vs
    // Zeppelin. The host goes back to a Deathmatch without teams for the rest of the suite.
    private static void TypeDescriptions(TestContext ctx, End host, End guest, List<End> ends)
    {
        const string Deathmatch = "Dogfight to the death.";
        const string Ctf = "Steal the opposing squadron's flag";
        const string Zvz = "Protect your zeppelin";
        ctx.Check(Draws(host.Shell.Compose(), Deathmatch) && Draws(guest.Shell.Compose(), Deathmatch),
            $"both ends describe the Deathmatch under the Type box with langui 10123");
        foreach (var (type, want) in new[] { (DogfightMissionType.CaptureTheFlag, Ctf), (DogfightMissionType.ZeppelinVsZeppelin, Zvz) })
        {
            host.Door.Dogfight!.SetMissionType(type);
            for (int frame = 0; frame < 4; frame++)
            {
                Pump(ends.ToArray());
            }

            var boards = new[] { host.Shell.Compose(), guest.Shell.Compose() };
            ctx.Check(boards.All(b => Draws(b, want) && !Draws(b, Deathmatch)),
                $"{type} is described on both ends with its own line and not the Deathmatch's ({guest.Door.Dogfight!.Options.MissionType})");
        }

        host.Door.Dogfight!.SetMissionType(DogfightMissionType.Deathmatch);
        host.Door.Dogfight!.SetRestrictTeams(false);
        for (int frame = 0; frame < 4; frame++)
        {
            Pump(ends.ToArray());
        }

        var back = guest.Door.Dogfight!.Options;
        ctx.Check(back is { MissionType: (byte)DogfightMissionType.Deathmatch, RestrictTeams: false } && Draws(guest.Shell.Compose(), Deathmatch),
            $"and back on a Deathmatch without teams the guest reads 10123 again ({back.MissionType}, {back.RestrictTeams})");
    }

    // The guest's second stock plane and a shell of its own on its first gun, picked while Ready,
    // which the change clears on both ends. Returns that gun's slot.
    private static int PickThePlane(TestContext ctx, End host, End guest, List<End> ends)
    {
        var lobby = guest.Door.Dogfight!;
        ClickRow(ctx, guest, OriginalLobbyScreen.ReadyKey);
        for (int frame = 0; frame < 4; frame++)
        {
            Pump(ends.ToArray());
        }

        ctx.Check(lobby.Ready && host.Door.Dogfight!.Players[1].Ready,
            $"ABLE-TO-FAIL CONTROL: the guest's Ready reaches the host before any pick changes ({lobby.Ready}, {host.Door.Dogfight!.Players[1].Ready})");
        ClickRow(ctx, guest, OriginalLobbyScreen.PlaneTabKey);
        ctx.Check(Row(guest.Shell, OriginalLobbyScreen.PlaneKey) is { Enabled: true },
            $"a Ready guest's plane box stays live ({Row(guest.Shell, OriginalLobbyScreen.PlaneKey)?.Enabled})");
        ClickRow(ctx, guest, OriginalLobbyScreen.PlaneKey);
        ClickRow(ctx, guest, OriginalLobbyScreen.PlaneKey + ":1");
        ctx.Check(lobby.Airframe == 1, $"the Select Plane list picks the second stock plane ({lobby.Airframe})");
        for (int frame = 0; frame < 4; frame++)
        {
            Pump(ends.ToArray());
        }

        ctx.Check(!lobby.Ready && host.Door.Dogfight!.Players[1] is { Ready: false, Airframe: 1 },
            $"and the changed pick clears the guest's Ready on both ends ({lobby.Ready}, {host.Door.Dogfight!.Players[1].Ready}, {host.Door.Dogfight!.Players[1].Airframe})");
        ClickRow(ctx, guest, OriginalLobbyScreen.AmmoTabKey);
        var gun = guest.Shell.Rows.FirstOrDefault(r => r.Enabled && r.Kind == OriginalRowKind.Dropdown
                                                      && r.Key.StartsWith("MPL_D_GUN_", StringComparison.Ordinal));
        if (gun == null)
        {
            ctx.Check(false, $"the Select Ammo page carries a live gun box");
            return -1;
        }

        int slot = int.Parse(gun.Key["MPL_D_GUN_".Length..], System.Globalization.CultureInfo.InvariantCulture);
        ClickRow(ctx, guest, gun.Key);
        ClickRow(ctx, guest, gun.Key + ":2");
        ctx.Check(lobby.Fit.AmmoAt(slot) == 2 && !lobby.Fit.IsStock,
            $"its gun box stores armour-piercing on slot {slot} ({lobby.Fit.AmmoAt(slot)})");
        for (int frame = 0; frame < 4; frame++)
        {
            Pump(ends.ToArray());
        }

        return slot;
    }

    // One line typed in the guest's chat box and sent: it lands once on each end.
    private static void Chat(TestContext ctx, End host, End guest, List<End> ends)
    {
        ClickRow(ctx, guest, OriginalLobbyScreen.ChatKey);
        TypeInto(guest, new MenuCommands { Typed = "hello" });
        ctx.Check(guest.Shell.Lobby.ChatDraft == "hello", $"the chat box takes the typed line ({guest.Shell.Lobby.ChatDraft})");
        ClickRow(ctx, guest, OriginalLobbyScreen.SendKey);
        for (int frame = 0; frame < 6; frame++)
        {
            Pump(ends.ToArray());
        }

        var here = host.Door.Dogfight!.Chat;
        var there = guest.Door.Dogfight!.Chat;
        ctx.Check(here.Count == 1 && there.Count == 1 && here[0].Text == "hello" && there[0].Text == "hello",
            $"one chat line arrives once on each end (host {here.Count}, guest {there.Count})");
        ChatPastTheDepthRepaints(ctx, host, guest, ends);
    }

    // A line arriving on a full chat leaves its count where it was, so only the door's news can
    // tell the host's board to repaint. The host sends no input while it arrives.
    private static void ChatPastTheDepthRepaints(TestContext ctx, End host, End guest, List<End> ends)
    {
        var said = guest.Door.Dogfight!;
        for (int line = 0; line < DogfightLobby.ChatDepth; line++)
        {
            said.Say($"line {line.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            Pump(ends.ToArray());
        }

        Pump(ends.ToArray());
        var shown = (host.Host.Active as OriginalPresentation)!;
        var quiet = shown.ShownBoard;
        host.Host.Tick(Dt);
        ctx.Check(host.Door.Dogfight!.Chat.Count == DogfightLobby.ChatDepth && quiet != null && ReferenceEquals(shown.ShownBoard, quiet),
            $"ABLE-TO-FAIL CONTROL: a quiet frame on the host's full lobby chat composes no new board ({host.Door.Dogfight!.Chat.Count} lines)");
        said.Say("the newest line");
        int frames = 0;
        while (frames < 2 && ReferenceEquals(shown.ShownBoard, quiet))
        {
            host.Host.Tick(Dt);
            frames++;
        }

        ctx.Check(!ReferenceEquals(shown.ShownBoard, quiet) && host.Door.Dogfight!.Chat[^1].Text == "the newest line",
            $"a guest's line past the chat's depth repaints the host's lobby within {frames} frame(s) with no input at the host");
    }

    // LAUNCH! waits for both Ready marks, then hands the host's launch out.
    private static LaunchExit? LaunchTheMatch(TestContext ctx, End host, End guest, List<End> ends, List<MenuExit> exits)
    {
        ClickRow(ctx, host, OriginalLobbyScreen.MissionTabKey);
        ClickRow(ctx, host, OriginalLobbyScreen.ReadyKey);
        for (int frame = 0; frame < 4; frame++)
        {
            Pump(ends.ToArray());
        }

        ctx.Check(Row(host.Shell, OriginalLobbyScreen.LaunchKey) is { Enabled: false } && host.Door.Dogfight!.Ready,
            $"ABLE-TO-FAIL CONTROL: with the host Ready and the guest not, LAUNCH! is greyed");
        ClickRow(ctx, guest, OriginalLobbyScreen.ReadyKey);
        for (int frame = 0; frame < 4; frame++)
        {
            Pump(ends.ToArray());
        }

        ctx.Check(Row(host.Shell, OriginalLobbyScreen.LaunchKey) is { Enabled: true },
            $"once the guest is Ready too LAUNCH! is live ({string.Join(",", host.Door.Dogfight!.Players.Select(p => p.Ready))})");
        int before = exits.Count;
        ClickRow(ctx, host, OriginalLobbyScreen.LaunchKey);
        var launch = exits.Skip(before).OfType<LaunchExit>().FirstOrDefault();
        ctx.Check(launch is { Mode: MenuMode.Versus, Net.IsHost: true, Match: { TimeLimitMinutes: 5, KillTarget: 0, Lives: DogfightLobby.DefaultLives } }
                  && launch.Chapter == DogfightLobby.ChapterOf(3),
            $"LAUNCH! hands out a Versus launch on the lobby's chapter and rules ({launch?.Chapter}, {launch?.Match})");
        return launch;
    }

    // The host's field off its launch: the guest's seat on the guest's pick. The control reads the
    // same peers with the picks withheld.
    private static NetSeat[] HostField(TestContext ctx, LaunchExit launch, MenuNetLaunch wire, int slot)
    {
        var planes = launch.Seats.Select(s => s.PlaneNode).ToList();
        var fits = launch.Seats.Select(s => s.Fit).ToList();
        var stock = StockLoadouts.Load();
        var (roster, seatFits) = CSVM.Launch.Launcher.VersusLaunchField(wire.Transport, planes, fits, stock);
        ctx.Check(roster.Length == 2 && roster[1].PlaneNode == StockAirframes.Node(1)
                  && slot >= 0 && seatFits[1].AmmoAt(slot) == 2,
            $"the host's roster builds the guest's seat on its pick and fit ({string.Join(", ", roster.Select(s => s.PlaneNode))})");
        var bare = ((NetLobby)wire.Transport).Inner;
        var (withheld, _) = CSVM.Launch.Launcher.VersusLaunchField(bare, planes, fits, stock);
        ctx.Check(withheld.Length == 2 && withheld[1].PlaneNode == planes[0],
            $"ABLE-TO-FAIL CONTROL: with the pick withheld the seat takes the local airframe ({withheld.LastOrDefault()?.PlaneNode})");
        return roster;
    }

    // The host's session opener reaching the guest's door, which then launches the guest on the
    // same chapter and rules, in its own pick.
    private static void GuestLaunch(TestContext ctx, MenuNetLaunch wire, NetSeat[] roster, End guest, List<MenuExit> exits)
    {
        int before = exits.Count;
        _ = NetSession.Host((NetLobby)wire.Transport, roster, 7UL);
        for (int frame = 0; frame < 4 && exits.Count == before; frame++)
        {
            Pump(guest);
        }

        var launch = exits.Skip(before).OfType<LaunchExit>().FirstOrDefault();
        ctx.Check(launch is { Mode: MenuMode.Versus, Net.IsHost: false, Match: { TimeLimitMinutes: 5, Lives: DogfightLobby.DefaultLives } }
                  && launch.Chapter == DogfightLobby.ChapterOf(3) && launch.Seats.Count == 1
                  && launch.Seats[0].PlaneNode == StockAirframes.Node(1),
            $"the guest launches behind the host on the same chapter and rules in its own pick ({launch?.Chapter}, {launch?.Match}, {launch?.Seats.FirstOrDefault()?.PlaneNode})");
    }

    // A completed match's Exit on both ends, as the launcher runs it. Each door takes its wire back,
    // and the menu comes back on a LobbyReturn built off that end's own match.
    private static void LandOnTheScores(TestContext ctx, End host, End guest, List<End> ends, IReadOnlyList<LoopbackTransport> mesh, List<MenuExit> guestExits)
    {
        var played = new VersusMatch(2, killTarget: 0, timeLimit: 60f);
        played.RegisterKill(1, 0);
        played.RegisterKill(1, 0);
        played.RegisterDeath(1);
        var heard = new VersusMatch(2, killTarget: 0, timeLimit: 60f);
        heard.Replicate();
        foreach (var line in played.Standings())
        {
            heard.ApplyScore(line.PlayerIndex, line.Score, line.Kills, line.Deaths);
        }

        ctx.Check(CSVM.Launch.Launcher.LobbyLanding(true, host.Door.Dogfight, played) == null,
            $"ABLE-TO-FAIL CONTROL: a match still running lands nowhere near the lobby");
        played.Advance(60f);
        heard.ApplyState(0, 60f, 0f, ended: true);
        var hostLanding = CSVM.Launch.Launcher.LobbyLanding(true, host.Door.Dogfight, played);
        var guestLanding = CSVM.Launch.Launcher.LobbyLanding(true, guest.Door.Dogfight, heard);
        ctx.Check(hostLanding != null && guestLanding != null, $"a completed match lands both ends on their lobby");
        ctx.Check(host.Door.Reclaim() && guest.Door.Reclaim(), $"and both doors take their wire back");
        if (hostLanding == null || guestLanding == null)
        {
            return;
        }

        // The match's tail: a session payload the host sent before its own Exit, still in flight.
        int launched = guestExits.Count;
        mesh[0].Send(mesh[1].LocalPeer, new byte[] { 0xEE, 1, 2, 3 }, NetReliability.Reliable);
        host.Host.Show(hostLanding);
        guest.Host.Show(guestLanding);
        for (int frame = 0; frame < 6; frame++)
        {
            Pump(ends.ToArray());
        }

        var here = host.Door.Dogfight!;
        var there = guest.Door.Dogfight!;
        ctx.Check(host.Shell is { Screen: OriginalScreen.Lobby, Lobby.Tab: LobbyTab.Scores }
                  && guest.Shell is { Screen: OriginalScreen.Lobby, Lobby.Tab: LobbyTab.Scores },
            $"both ends stand in the lobby on Game Scores ({host.Shell.Screen}/{host.Shell.Lobby.Tab}, {guest.Shell.Screen}/{guest.Shell.Lobby.Tab})");
        string board = string.Join(" ", here.Scores.Select(s => $"{s.Name}:{s.Points}/{s.Kills}K/{s.Deaths}D"));
        ctx.Check(here.Scores.Count == 2 && here.Scores.SequenceEqual(there.Scores)
                  && here.Scores[0] is { Points: 1, Kills: 2, Deaths: 1 } && here.Scores[0].Name == here.LaunchNames[1],
            $"and both show the match's scores, the guest's seat first by name ({board} | {string.Join(" ", there.Scores.Select(s => s.Name))})");
        ctx.Check(Row(host.Shell, OriginalLobbyScreen.ScoresTabKey) is { Enabled: true }, $"Game Scores is live once a match has landed");
        ctx.Check(!here.Ready && !there.Ready && here.Players.All(p => !p.Ready),
            $"every Ready is cleared for the next round ({string.Join(",", here.Players.Select(p => p.Ready))})");
        ctx.Check(guestExits.Count == launched && !guest.Door.DogfightLaunchDue,
            $"and the payload left over from the match launches the guest into nothing ({guestExits.Count - launched} exit(s))");
    }

    // The next round off the landed lobby, with nobody rejoining. Both mark Ready, and LAUNCH!
    // hands out a second launch the guest follows on its kept pick.
    private static void LaunchAgain(TestContext ctx, End host, End guest, List<End> ends, List<MenuExit> hostExits, List<MenuExit> guestExits)
    {
        ClickRow(ctx, host, OriginalLobbyScreen.ReadyKey);
        ClickRow(ctx, guest, OriginalLobbyScreen.ReadyKey);
        for (int frame = 0; frame < 4; frame++)
        {
            Pump(ends.ToArray());
        }

        // LAUNCH! stands on the Mission Options page, as the host's first launch found it.
        ClickRow(ctx, host, OriginalLobbyScreen.MissionTabKey);
        ctx.Check(Row(host.Shell, OriginalLobbyScreen.LaunchKey) is { Enabled: true },
            $"both Ready again, LAUNCH! is live for a second round ({string.Join(",", host.Door.Dogfight!.Players.Select(p => p.Ready))})");
        int before = hostExits.Count;
        ClickRow(ctx, host, OriginalLobbyScreen.LaunchKey);
        var launch = hostExits.Skip(before).OfType<LaunchExit>().FirstOrDefault();
        ctx.Check(launch is { Mode: MenuMode.Versus, Net.IsHost: true } && launch.Chapter == DogfightLobby.ChapterOf(3),
            $"and it hands out a second Versus launch on the same lobby ({launch?.Chapter})");
        if (launch?.Net is not { } wire)
        {
            return;
        }

        var planes = launch.Seats.Select(s => s.PlaneNode).ToList();
        var fits = launch.Seats.Select(s => s.Fit).ToList();
        var (roster, _) = CSVM.Launch.Launcher.VersusLaunchField(wire.Transport, planes, fits, StockLoadouts.Load());
        GuestLaunch(ctx, wire, roster, guest, guestExits);
    }

    // The list changed on the host with Outlaw Components clear. Select... is greyed on both ends,
    // and the flag still reaches the guest and clears its Ready.
    private static void EditedWithTheTickClear(TestContext ctx, End host, End guest, List<End> ends)
    {
        ctx.Check(Row(host.Shell, OriginalLobbyScreen.SelectKey) is { Enabled: false } && Row(guest.Shell, OriginalLobbyScreen.SelectKey) is { Enabled: false },
            $"Select... is greyed on both ends while Outlaw Components is clear");
        ClickRow(ctx, host, OriginalLobbyScreen.SelectKey);
        ctx.Check(!host.Shell.Lobby.OutlawListOpen, $"ABLE-TO-FAIL CONTROL: and a click on the host's opens nothing");
        var here = host.Door.Dogfight!;
        var there = guest.Door.Dogfight!;
        ReadyGuest(ctx, host, guest, ends, "before the list changes with the tick clear");
        ctx.Check(here.SetOutlawed(NetPlaneRules.NitroFlag, true), $"the host's list takes a flag with the tick clear");
        SettleEnds(ends);
        ctx.Check(there.Rules is { Outlawing: false } && there.Rules.Has(NetPlaneRules.NitroFlag) && !there.Ready && here.Players.All(p => !p.Ready),
            $"and it reaches the guest with every Ready cleared ({there.Rules.Outlawing}, {there.Rules.Has(NetPlaneRules.NitroFlag)}, {string.Join(",", here.Players.Select(p => p.Ready))})");
        here.SetOutlawed(NetPlaneRules.NitroFlag, false);
        SettleEnds(ends);
    }

    // A click on Outlaw Components, setting it and then clearing it, empties the list on both ends.
    // Each click is one round and clears every Ready. The tick is left clear.
    private static void ToggleEmptiesTheList(TestContext ctx, End host, End guest, List<End> ends)
    {
        var here = host.Door.Dogfight!;
        var there = guest.Door.Dogfight!;
        var flags = Enumerable.Range(0, 11).Where(r => r != here.Airframe && r != there.Airframe).Take(2)
            .Select(r => NetPlaneRules.AirframeFlag + r).Append(NetPlaneRules.NitroFlag).ToArray();
        foreach (bool setting in new[] { true, false })
        {
            string way = setting ? "setting" : "clearing";
            foreach (int flag in flags)
            {
                here.SetOutlawed(flag, true);
            }

            SettleEnds(ends);
            ReadyGuest(ctx, host, guest, ends, $"before {way} Outlaw Components");
            ctx.Check(flags.All(there.Rules.Has), $"the guest holds the host's {flags.Length} flags before {way} the tick ({there.Rules.Outlawed:X})");
            int epoch = here.Options.Epoch;
            ClickRow(ctx, host, OriginalLobbyScreen.OutlawKey);
            SettleEnds(ends);
            ctx.Check(here.Rules is { Outlawed: 0 } && there.Rules is { Outlawed: 0 } && there.Rules.Outlawing == setting,
                $"{way} Outlaw Components empties the list on both ends ({here.Rules.Outlawed:X}, {there.Rules.Outlawed:X}, tick {there.Rules.Outlawing})");
            ctx.Check(here.Options.Epoch == (byte)(epoch + 1) && there.Options.Epoch == here.Options.Epoch
                      && !there.Ready && here.Players.All(p => !p.Ready),
                $"in one round that clears every Ready (epoch {epoch} to {here.Options.Epoch}, guest {there.Options.Epoch}, {string.Join(",", here.Players.Select(p => p.Ready))})");
        }
    }

    // One tick of each kind on the host's list. Before each the guest is Ready, and after it the
    // guest holds the flag and nobody is Ready. The rocket stands past the window's first four rows.
    private static void TickEachKind(TestContext ctx, End host, End guest, List<End> ends)
    {
        var here = host.Door.Dogfight!;
        var there = guest.Door.Dogfight!;
        var mounted = NetPlaneBuild.Stock(there.Airframe).Guns;
        int calibre = Enumerable.Range(0, 5).First(c => Array.IndexOf(mounted, (byte)c) < 0);
        int airframe = (there.Airframe + 5) % 11;
        var ticks = new (OutlawPage Page, string Key, int Flag, string What)[]
        {
            (OutlawPage.Airframes, OriginalOutlawList.RowKey(airframe), NetPlaneRules.AirframeFlag + airframe, "an airframe"),
            (OutlawPage.Guns, OriginalOutlawList.RowKey(calibre), NetPlaneRules.GunFlag + calibre, "a gun calibre"),
            (OutlawPage.Ammo, OriginalOutlawList.RowKey(3), NetPlaneRules.AmmoFlag + 3, "an ammunition"),
            (OutlawPage.Rockets, OriginalOutlawList.RowKey(4), NetPlaneRules.RocketFlag + 4, "a rocket past the scroll"),
            (OutlawPage.Ammo, OriginalOutlawList.AllKey, NetPlaneRules.AllAmmoFlag, "All Ammo"),
            (OutlawPage.Rockets, OriginalOutlawList.AllKey, NetPlaneRules.AllRocketsFlag, "All Rockets"),
            (OutlawPage.Engines, OriginalOutlawList.RowKey(0), NetPlaneRules.NitroFlag, "nitro"),
        };

        foreach (var (page, key, flag, what) in ticks)
        {
            ReadyGuest(ctx, host, guest, ends, $"before {what} is ticked");
            ClickRow(ctx, host, OriginalOutlawList.TabKey(page));
            for (int step = 0; step < 11 && Row(host.Shell, key) == null && Row(host.Shell, OriginalOutlawList.DownKey) is { Enabled: true }; step++)
            {
                ClickRow(ctx, host, OriginalOutlawList.DownKey);
            }

            ClickRow(ctx, host, key);
            SettleEnds(ends);
            ctx.Check(here.Rules.Has(flag) && there.Rules.Has(flag) && !there.Ready && here.Players.All(p => !p.Ready),
                $"{what} ticked on the host's list reaches the guest with every Ready cleared (flag {flag}, {there.Rules.Has(flag)}, {string.Join(",", here.Players.Select(p => p.Ready))})");
        }
    }

    // Under All Rockets every rocket row reads ticked, and a click on one writes nothing.
    private static void AllRocketsCoversItsRows(TestContext ctx, End host, List<End> ends)
    {
        var here = host.Door.Dogfight!;
        ClickRow(ctx, host, OriginalOutlawList.TabKey(OutlawPage.Rockets));
        int epoch = here.Options.Epoch;
        ClickRow(ctx, host, OriginalOutlawList.RowKey(0));
        SettleEnds(ends);
        ctx.Check(!here.Rules.Has(NetPlaneRules.RocketFlag) && here.Options.Epoch == epoch
                  && OutlawRows.Ticked(here.Rules, OutlawPage.Rockets, 0),
            $"under All Rockets a rocket row reads ticked and a click on it writes nothing ({here.Rules.Has(NetPlaneRules.RocketFlag)}, epoch {epoch} to {here.Options.Epoch})");
    }

    // Accept keeps the list, and Cancel puts back the one the host opened with, on both ends.
    private static void CancelAndAccept(TestContext ctx, End host, End guest, List<End> ends)
    {
        var here = host.Door.Dogfight!;
        var there = guest.Door.Dogfight!;
        ulong accepted = here.Rules.Outlawed;
        ClickRow(ctx, host, OriginalOutlawList.AcceptKey);
        ctx.Check(!host.Shell.Lobby.OutlawListOpen && here.Rules.Outlawed == accepted,
            $"Accept takes the list down and keeps it ({host.Shell.Lobby.OutlawListOpen})");
        int row = Enumerable.Range(0, OutlawRows.Window).First(r => r != here.Airframe && r != there.Airframe && !here.Rules.Has(NetPlaneRules.AirframeFlag + r));
        ClickRow(ctx, host, OriginalLobbyScreen.SelectKey);
        ClickRow(ctx, host, OriginalOutlawList.RowKey(row));
        SettleEnds(ends);
        ctx.Check(there.Rules.Has(NetPlaneRules.AirframeFlag + row), $"ABLE-TO-FAIL CONTROL: a reopened list's tick reaches the guest (airframe {row})");
        ClickRow(ctx, host, OriginalOutlawList.CancelKey);
        SettleEnds(ends);
        ctx.Check(!host.Shell.Lobby.OutlawListOpen && here.Rules.Outlawed == accepted && there.Rules.Outlawed == accepted,
            $"Cancel puts back the list the host opened with, on both ends ({here.Rules.Outlawed:X}, {there.Rules.Outlawed:X}, {accepted:X})");
    }

    // The guest's View... and a Ready host's Select... open the list read-only. The guest's has no
    // Accept, the host's draws it greyed, and Cancel closes either with the list as it stood.
    private static void ViewedReadOnly(TestContext ctx, End host, End guest, List<End> ends)
    {
        var here = host.Door.Dogfight!;
        ulong list = here.Rules.Outlawed;
        ctx.Check(Row(guest.Shell, OriginalLobbyScreen.SelectKey) is { Enabled: true } && Draws(guest.Shell.Compose(), "View..."),
            $"the guest's plaque reads View... and is live while the tick is set");
        ClickRow(ctx, guest, OriginalLobbyScreen.SelectKey);
        ctx.Check(guest.Shell.Lobby.OutlawListOpen && Row(guest.Shell, OriginalOutlawList.AcceptKey) == null
                  && Row(guest.Shell, OriginalOutlawList.RowKey(0)) is { Enabled: false } && Row(guest.Shell, OriginalOutlawList.CancelKey) is { Enabled: true },
            $"View... opens the list on the guest with its boxes greyed, no Accept and a live Cancel ({guest.Shell.Lobby.OutlawListOpen})");
        ClickRow(ctx, guest, OriginalOutlawList.CancelKey);
        ctx.Check(!guest.Shell.Lobby.OutlawListOpen, $"the guest's Cancel takes it down");

        for (int press = 0; press < 2 && !here.Ready; press++)
        {
            here.SetReady(true);
        }

        SettleEnds(ends);
        ClickRow(ctx, host, OriginalLobbyScreen.SelectKey);
        ctx.Check(here.Ready && host.Shell.Lobby.OutlawListOpen && Row(host.Shell, OriginalOutlawList.AcceptKey) is { Enabled: false }
                  && Row(host.Shell, OriginalOutlawList.RowKey(0)) is { Enabled: false },
            $"a Ready host's Select... opens the list read-only with Accept greyed ({here.Ready}, {host.Shell.Lobby.OutlawListOpen})");
        ClickRow(ctx, host, OriginalOutlawList.CancelKey);
        SettleEnds(ends);
        ctx.Check(!host.Shell.Lobby.OutlawListOpen && here.Rules.Outlawed == list && here.Ready,
            $"and its Cancel closes it with the list and the host's Ready as they stood ({here.Rules.Outlawed:X}, {here.Ready})");
    }

    // The guest marked Ready, pressed twice where an outlawed ammunition or rocket refuses once.
    // The host's Create Team: the box with OK greyed, a blank name refused, then a typed one, which
    // reaches the guest as a team row. Returns the team's number.
    private static byte CreateTheTeam(TestContext ctx, End host, End guest, List<End> ends)
    {
        ClickRow(ctx, host, OriginalLobbyScreen.TeamKey);
        ctx.Check(host.Shell.Lobby.TeamBoxOpen && Row(host.Shell, OriginalTeamBox.OkKey) is { Enabled: false },
            $"Create Team stands the CREATE TEAM box with OK greyed while the name is empty ({host.Shell.Lobby.TeamBoxOpen})");
        TypeInto(host, new MenuCommands { Typed = "   " });
        ClickRow(ctx, host, OriginalTeamBox.OkKey);
        ctx.Check(host.Shell.Dialog?.Message == "Invalid team name." && host.Shell.Lobby.TeamBoxOpen && host.Door.Dogfight!.Teams.Count == 0,
            $"a name of spaces alone raises the original's refusal and creates nothing ({host.Shell.Dialog?.Message})");
        ClickRow(ctx, host, OriginalShell.DialogOkKey);
        ClickRow(ctx, host, OriginalTeamBox.NameKey);
        TypeInto(host, new MenuCommands { Typed = "Aces" });
        ClickRow(ctx, host, OriginalTeamBox.OkKey);
        for (int frame = 0; frame < 4; frame++)
        {
            Pump(ends.ToArray());
        }

        var lobby = host.Door.Dogfight!;
        byte team = lobby.OwnTeam;
        ctx.Check(!host.Shell.Lobby.TeamBoxOpen && team != 0 && lobby.Players[0].Captain,
            $"OK creates the team with the host its captain ({team}, {lobby.Players[0].Captain})");
        var heard = guest.Door.Dogfight!.Teams;
        ctx.Check(heard.Count == 1 && heard[0].Name == "Aces" && Row(guest.Shell, OriginalLobbyScreen.TeamRowKey(team)) != null
                  && Draws(guest.Shell.Compose(), OriginalLobbyScreen.TeamRowText("Aces", 1)),
            $"the team reaches the guest as a pickable team row ({string.Join(", ", heard.Select(t => t.Name))})");
        return team;
    }

    // The guest picks the team row and its team button joins; a Ready guest's button is greyed.
    private static void JoinTheTeam(TestContext ctx, End host, End guest, List<End> ends, byte team)
    {
        ClickRow(ctx, guest, OriginalLobbyScreen.ReadyKey);
        ctx.Check(Row(guest.Shell, OriginalLobbyScreen.TeamKey) is { Enabled: false },
            $"ABLE-TO-FAIL CONTROL: a Ready guest's team button is greyed");
        ClickRow(ctx, guest, OriginalLobbyScreen.ReadyKey);
        ClickRow(ctx, guest, OriginalLobbyScreen.TeamRowKey(team));
        ctx.Check(guest.Shell.Lobby.PickedTeam == team, $"a press picks the team row ({guest.Shell.Lobby.PickedTeam})");
        ClickRow(ctx, guest, OriginalLobbyScreen.TeamKey);
        for (int frame = 0; frame < 4; frame++)
        {
            Pump(ends.ToArray());
        }

        var hostLobby = host.Door.Dogfight!;
        ctx.Check(guest.Door.Dogfight!.OwnTeam == team && hostLobby.Players[1].Team == team,
            $"Join Team puts the guest on the team on both ends ({guest.Door.Dogfight!.OwnTeam}, {hostLobby.Players[1].Team})");
        ctx.Check(hostLobby.Chat.Any(l => l.Text == "[Nathan joined team Aces.]") && guest.Door.Dogfight!.Chat.Any(l => l.Text == "[Nathan joined team Aces.]"),
            $"and every end's chat announces it");
    }

    // Restrict Number of Teams: the count boxes live on the host only, an arrow reaching the guest.
    private static void RestrictTheTeams(TestContext ctx, End host, End guest, List<End> ends)
    {
        ctx.Check(Row(host.Shell, OriginalLobbyScreen.MinTeamsKey) is { Enabled: false },
            $"ABLE-TO-FAIL CONTROL: the count boxes are greyed while the tick is clear");
        ClickRow(ctx, host, OriginalLobbyScreen.TeamsKey);
        ctx.Check(Row(host.Shell, OriginalLobbyScreen.MinTeamsKey) is { Enabled: true } && Row(host.Shell, OriginalLobbyScreen.MaxTeamsKey) is { Enabled: true },
            $"the tick makes both count boxes live on the host");
        ClickRow(ctx, host, OriginalLobbyScreen.TeamArrowPrefix + "MAX+");
        for (int frame = 0; frame < 4; frame++)
        {
            Pump(ends.ToArray());
        }

        var heard = guest.Door.Dogfight!.Options;
        ctx.Check(heard is { RestrictTeams: true, MinTeams: DogfightOptionsMessage.DefaultMinTeams, MaxTeams: DogfightOptionsMessage.DefaultMaxTeams + 1 }
                  && Row(guest.Shell, OriginalLobbyScreen.MinTeamsKey) is { Enabled: false },
            $"the tick and the raised maximum reach the guest, whose boxes stay greyed ({heard.RestrictTeams}, {heard.MinTeams}, {heard.MaxTeams})");
    }

    // One team refuses LAUNCH!; the guest's own team lets it go, each seat carrying its team.
    private static void LaunchOnTeams(TestContext ctx, End host, End guest, List<End> ends, List<MenuExit> exits, byte team)
    {
        ClickRow(ctx, host, OriginalLobbyScreen.ReadyKey);
        ClickRow(ctx, guest, OriginalLobbyScreen.ReadyKey);
        for (int frame = 0; frame < 4; frame++)
        {
            Pump(ends.ToArray());
        }

        int before = exits.Count;
        ClickRow(ctx, host, OriginalLobbyScreen.LaunchKey);
        ctx.Check(exits.Count == before && host.Shell.Dialog?.Message == "Each player must be on one of two teams to play.",
            $"with one team LAUNCH! raises the original's refusal and hands nothing out ({host.Shell.Dialog?.Message})");
        ClickRow(ctx, host, OriginalShell.DialogOkKey);

        ClickRow(ctx, guest, OriginalLobbyScreen.ReadyKey);
        ClickRow(ctx, guest, OriginalLobbyScreen.TeamKey);
        for (int frame = 0; frame < 4; frame++)
        {
            Pump(ends.ToArray());
        }

        ctx.Check(guest.Door.Dogfight!.OwnTeam == 0 && host.Door.Dogfight!.Players[0].Team == team,
            $"the guest's Leave Team leaves the captain's team standing ({guest.Door.Dogfight!.OwnTeam}, {host.Door.Dogfight!.Players[0].Team})");
        ClickRow(ctx, guest, OriginalLobbyScreen.TeamKey);
        TypeInto(guest, new MenuCommands { Typed = "Bandits" });
        ClickRow(ctx, guest, OriginalTeamBox.OkKey);
        for (int frame = 0; frame < 4; frame++)
        {
            Pump(ends.ToArray());
        }

        ClickRow(ctx, guest, OriginalLobbyScreen.ReadyKey);
        for (int frame = 0; frame < 4; frame++)
        {
            Pump(ends.ToArray());
        }

        byte theirs = guest.Door.Dogfight!.OwnTeam;
        ClickRow(ctx, host, OriginalLobbyScreen.LaunchKey);
        var launch = exits.Skip(before).OfType<LaunchExit>().FirstOrDefault();
        ctx.Check(launch?.Net != null && theirs != 0 && theirs != team,
            $"with two teams of one LAUNCH! hands out the launch ({launch?.Chapter}, {theirs})");
        if (launch?.Net is not { } wire)
        {
            return;
        }

        var planes = launch.Seats.Select(s => s.PlaneNode).ToList();
        var fits = launch.Seats.Select(s => s.Fit).ToList();
        var (roster, _) = CSVM.Launch.Launcher.VersusLaunchField(wire.Transport, planes, fits, StockLoadouts.Load(), null,
            host.Door.Dogfight!.TeamOfPeer);
        ctx.Check(roster.Length == 2 && roster[0].TeamId == team && roster[1].TeamId == theirs,
            $"the host's field carries each seat's team ({string.Join(", ", roster.Select(s => s.TeamId))})");
    }

    private static void ReadyGuest(TestContext ctx, End host, End guest, List<End> ends, string when)
    {
        var lobby = guest.Door.Dogfight!;
        for (int press = 0; press < 2 && !lobby.Ready; press++)
        {
            lobby.SetReady(true);
        }

        SettleEnds(ends);
        ctx.Check(lobby.Ready && host.Door.Dogfight!.Players[1].Ready,
            $"ABLE-TO-FAIL CONTROL: the guest's Ready reaches the host {when} ({lobby.Ready}, {string.Join(",", lobby.ReadyRefusals)})");
    }

    private static void SettleEnds(List<End> ends)
    {
        for (int frame = 0; frame < 4; frame++)
        {
            Pump(ends.ToArray());
        }
    }

    // The Built-in host's wire taken out and its field built off the guest's pick. The session
    // opener then launches the guest on the host's map and time in its own pick.
    private static void HostTheBuiltInLaunch(TestContext ctx, NetPlayFeature hostDoor, End guest, List<MenuExit> guestExits, string chapter)
    {
        var wire = hostDoor.BuildLaunch();
        if (wire == null)
        {
            ctx.Check(false, $"the Built-in host's door hands its wire out ({hostDoor.Stage})");
            return;
        }

        var planes = new[] { StockAirframes.Node(0) };
        var fits = new LoadoutChoice?[] { null };
        var (roster, _) = CSVM.Launch.Launcher.VersusLaunchField(wire.Transport, planes, fits, StockLoadouts.Load());
        ctx.Check(roster.Length == 2 && roster[1].PlaneNode == StockAirframes.Node(2),
            $"the host's roster builds the guest's seat on its pick ({string.Join(", ", roster.Select(s => s.PlaneNode))})");
        ctx.Check(roster.Length == 2 && roster[0].Callsign == SplitScreen.PlayerTag(0),
            $"a host whose advert names nobody is seated under its player tag ({roster.FirstOrDefault()?.Callsign})");
        int before = guestExits.Count;
        _ = NetSession.Host((NetLobby)wire.Transport, roster, 7UL);
        for (int frame = 0; frame < 4 && guestExits.Count == before; frame++)
        {
            Pump(guest);
        }

        var launch = guestExits.Skip(before).OfType<LaunchExit>().FirstOrDefault();
        ctx.Check(launch is { Mode: MenuMode.Versus, Net.IsHost: false, Match.TimeLimitMinutes: 5 }
                  && launch.Chapter == chapter && launch.Seats.Count == 1
                  && launch.Seats[0].PlaneNode == StockAirframes.Node(2),
            $"the guest launches behind the Built-in host on its map and time in its own pick ({launch?.Chapter}, {launch?.Match}, {launch?.Seats.FirstOrDefault()?.PlaneNode})");
    }

    // The games list names the host's version in place of its status, and Join Game refuses it
    // behind a box before any socket opens.
    private static void RefusedFromTheList(TestContext ctx, NetPlayFeature hostDoor, End guest, Func<int> opened)
    {
        var shell = guest.Shell;
        ClickRow(ctx, guest, OriginalShell.MultiplayerKey);
        ClickRow(ctx, guest, OriginalConnectionScreen.ConnectKey);
        for (int frame = 0; frame < 6 && shell.Connection.Listed.Count == 0; frame++)
        {
            Frames(hostDoor, guest, 1);
        }

        if (shell.Connection.Listed.Count != 1)
        {
            ctx.Check(false, $"the search lists the host of another version ({shell.Connection.Listed.Count})");
            return;
        }

        var cells = shell.Connection.Cells(shell.Connection.Listed[0]);
        ctx.Check(cells.Count == 5 && cells[2] == "Dogfight" && cells[4] == "Version 0.7",
            $"the games list marks the row with the host's version ({string.Join(" | ", cells)})");
        ClickRow(ctx, guest, OriginalConnectionScreen.GameKey(0));
        ClickRow(ctx, guest, OriginalConnectionScreen.JoinKey);
        Frames(hostDoor, guest, 2);
        ctx.Check(shell.Dialog?.Message == "Host runs 0.7, you run 0.6" && shell.Screen == OriginalScreen.ConnectionGames,
            $"Join Game raises a box naming both versions over the games list ({shell.Dialog?.Message}, {shell.Screen})");
        ctx.Check(opened() == 0 && guest.Door.Stage == NetDoorStage.Shut && hostDoor.Peers == 0,
            $"and opens no socket ({opened()} opened, {guest.Door.Stage}, {hostDoor.Peers} joined)");
        ClickRow(ctx, guest, OriginalShell.DialogOkKey);
        ClickRow(ctx, guest, OriginalConnectionScreen.GamesExitKey);
        ctx.Check(shell.Dialog == null && shell.Screen == OriginalScreen.Connection,
            $"OK takes the box down and Exit goes back to the Connection page ({shell.Dialog?.Message}, {shell.Screen})");
    }

    // A join typed over Internet TCP/IP reaches the host, which refuses it on the wire and says
    // why. The guest's box carries the same words as the list's.
    private static void RefusedOnTheWire(TestContext ctx, NetPlayFeature hostDoor, End guest, Func<int> opened)
    {
        var shell = guest.Shell;
        ClickRow(ctx, guest, OriginalConnectionScreen.InternetKey);
        ctx.Check(guest.Door.Address == Loopback, $"the address box holds the loopback the host listens on ({guest.Door.Address})");
        ClickRow(ctx, guest, OriginalConnectionScreen.ConnectKey);
        ctx.Check(opened() == 0 && shell.NetInfo.Page == NetInfoPage.Player,
            $"a typed join asks Player Information before any socket opens ({opened()} opened, {shell.NetInfo.Page})");
        Answer(ctx, guest, "Laeresh");
        Frames(hostDoor, guest, 6);
        ctx.Check(opened() == 1, $"the typed join opens its socket ({opened()} opened)");
        ctx.Check(guest.Door.Fault == "Host runs 0.7, you run 0.6" && shell.Dialog?.Message == guest.Door.Fault,
            $"the host's refusal names both versions on the guest's box ({guest.Door.Fault}, {shell.Dialog?.Message})");
        ctx.Check(hostDoor.Peers == 0 && hostDoor.Dogfight is { Players.Count: 1 },
            $"and the host seats nobody ({hostDoor.Peers} joined, {hostDoor.Dogfight?.Players.Count} in the lobby)");
        ClickRow(ctx, guest, OriginalShell.DialogOkKey);
        Frames(hostDoor, guest, 2);
        ctx.Check(shell.Dialog == null && guest.Door.Stage == NetDoorStage.Shut,
            $"OK takes the box down and hangs up ({shell.Dialog?.Message}, {guest.Door.Stage})");
    }

    // The address the pilot could not type, typed key by key. A seat reading US key positions turns
    // the German ':' into '>', and a box capped at 48 characters or refusing brackets stops short.
    private static void TypeTheReportedAddress(TestContext ctx, End guest, BuiltInSeat reader)
    {
        const string Reported = "[2a04:6ec0:232:6640:feb1:ff80:9ed7:dd90]:47500";
        const string Host = "2a04:6ec0:232:6640:feb1:ff80:9ed7:dd90";
        var door = guest.Door;
        EraseAddress(guest);
        string colon = TypeKeys(ctx, guest, reader, ":");
        ctx.Check(colon == ":",
            $"ABLE-TO-FAIL CONTROL: Shift and the period key on a German layout reach the seat as ':' ('{colon}')");
        EraseAddress(guest);
        string typed = TypeKeys(ctx, guest, reader, Reported);
        ctx.Check(typed == Reported && door.Address == Reported,
            $"ABLE-TO-FAIL CONTROL: the whole {Reported.Length}-character address arrives and the box keeps it ('{typed}' typed, '{door.Address}' kept)");
        ctx.Check(door.JoinTarget == new NetEndpoint(Host, 47500),
            $"the door joins the bare host on the typed port ({door.JoinTarget.Host}, {door.JoinTarget.Port})");

        var line = guest.Shell.Compose().Lines.FirstOrDefault(l => l.Text == door.Address);
        ctx.Check(line is { KeepEnd: true, Caret: not null },
            $"the box draws the address as an edit line with its caret ({line?.KeepEnd}, {line?.Caret != null})");
        if (line == null)
        {
            return;
        }

        // Measured as the view draws it: window pixels on this viewport's fit.
        var size = ctx.Host.GetViewport().GetVisibleRect().Size;
        var fit = BoardFit.For(size.X, size.Y);
        var view = new ComposedBoardView();
        try
        {
            var font = (line.Face is { } face ? view.Installed(face) : null) ?? view.GetThemeDefaultFont();
            int points = Math.Max(1, (int)Math.Round(fit.Length(line.Size)));
            float wide = font.GetStringSize(line.Text, Godot.HorizontalAlignment.Left, -1f, points).X;
            float box = fit.Length(line.Width);
            float caret = line.Caret is { } lit ? fit.Length(lit.Width) : 0f;
            float end = wide - ComposedBoardView.EndShift(font, line, points, box, caret);
            ctx.Check(wide > box,
                $"the address is wider than the box ({wide:0} px against {box:0}), so the box has to scroll to show it");
            ctx.Check(end + caret <= box + 0.5f && end + caret >= box - 1.5f,
                $"ABLE-TO-FAIL CONTROL: the box draws the address's last character inside it with the caret after it (text ends at {end:0.0} px, caret {caret:0} px, box {box:0} px)");
        }
        finally
        {
            view.Free();
        }
    }

    // The address pasted rather than typed, on both chords a Windows edit box pastes on. The
    // clipboard is the seam's, so the pilot's own is neither read nor written.
    private static void PasteTheAddress(TestContext ctx, End guest, BuiltInSeat reader)
    {
        const string Reported = "[2a04:6ec0:232:6640:feb1:ff80:9ed7:dd90]:47500";
        var door = guest.Door;
        var viewport = ctx.Host.GetViewport();
        var pilots = MenuInput.Clipboard;
        try
        {
            EraseAddress(guest);
            MenuInput.Clipboard = () => $" \t{Reported}\r\n";
            // Ctrl+V with the letter still on the event, as a layout may report it. A chord read as
            // typing would put a 'v' in the box.
            viewport.PushInput(new Godot.InputEventKey { Keycode = Godot.Key.V, PhysicalKeycode = Godot.Key.V, CtrlPressed = true, Unicode = 'v', Pressed = true });
            viewport.PushInput(new Godot.InputEventKey { Keycode = Godot.Key.V, PhysicalKeycode = Godot.Key.V, CtrlPressed = true, Pressed = false });
            var frame = reader.Poll(Dt);
            var cues = new List<string>();
            guest.Shell.Connection.TypeText(frame, cues);
            ctx.Check(frame.Paste && frame.Typed.Length == 0 && door.Address == Reported && cues.SequenceEqual(new[] { OriginalCues.Text }),
                $"ABLE-TO-FAIL CONTROL: Ctrl+V pastes the clipboard's address into the box, trimmed, with one keystroke cue ({frame.Paste}, '{frame.Typed}', '{door.Address}', {string.Join(" ", cues)})");

            EraseAddress(guest);
            MenuInput.Clipboard = () => "::1/128";
            viewport.PushInput(new Godot.InputEventKey { Keycode = Godot.Key.Insert, PhysicalKeycode = Godot.Key.Insert, ShiftPressed = true, Pressed = true });
            viewport.PushInput(new Godot.InputEventKey { Keycode = Godot.Key.Insert, PhysicalKeycode = Godot.Key.Insert, Pressed = false });
            frame = reader.Poll(Dt);
            cues.Clear();
            guest.Shell.Connection.TypeText(frame, cues);
            ctx.Check(frame.Paste && door.Address == "::1128" && cues.SequenceEqual(new[] { OriginalCues.TextError }),
                $"Shift+Insert pastes too, the '/' an address is never written with left out under the reject cue ('{door.Address}', {string.Join(" ", cues)})");
        }
        finally
        {
            MenuInput.Clipboard = pilots;
        }
    }

    // [::1] and a port typed into the box and joined over the shipped ENet carrier.
    private static void JoinTheLoopback(TestContext ctx, End guest, BuiltInSeat reader, EnetTransport host, int port)
    {
        var door = guest.Door;
        string typed = $"[::1]:{port.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        EraseAddress(guest);
        TypeKeys(ctx, guest, reader, typed);
        ClickRow(ctx, guest, OriginalConnectionScreen.ConnectKey);
        Answer(ctx, guest, "Laeresh");
        Pump(guest);
        ctx.Check(guest.Shell.Dialog?.Message == $"Connecting to {typed} ...",
            $"Connect follows the join on a box naming the typed host and port ({guest.Shell.Screen}, {door.Stage}, {guest.Shell.Dialog?.Message})");
        AwaitJoin(host, door, () => Pump(guest));
        ctx.Check(door.Stage == NetDoorStage.Joined && host.Peers.Count == 1,
            $"the bracketed IPv6 join reaches the ENet host on [::1]:{port} ({door.Stage}, {door.Fault}, {host.Peers.Count} joined)");
    }

    // A bare IPv6 address joins on the board's port, its last group read as part of the host.
    private static void JoinTheBareLoopback(TestContext ctx, NetPlayFeature door, EnetTransport host, int port)
    {
        const string Bare = "0:0:0:0:0:0:0:1";
        while (door.Address.Length > 0)
        {
            door.EraseAddress();
        }

        door.TypeAddress(Bare);
        door.StepPort(port - door.Port);
        ctx.Check(door.JoinTarget == new NetEndpoint(Bare, port),
            $"a bare IPv6 address keeps its last group in the host and joins on the board's port ({door.JoinTarget.Host}, {door.JoinTarget.Port})");
        int before = host.Peers.Count;
        door.OpenJoin();
        AwaitJoin(host, door, () => door.Step(Dt));
        ctx.Check(door.Stage == NetDoorStage.Joined && host.Peers.Count == before + 1,
            $"and that join reaches the ENet host too ({door.Stage}, {door.Fault}, {host.Peers.Count} joined)");
    }

    // A real socket connects on the wall clock, so the wait is on it rather than a frame count.
    private static void AwaitJoin(EnetTransport host, NetPlayFeature door, Action frame)
    {
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (door.Stage == NetDoorStage.Joining && waited.Elapsed.TotalSeconds < 5.0)
        {
            host.Step(0.001);
            frame();
            System.Threading.Thread.Sleep(1);
        }

        host.Step(0.001);
    }

    // An ENet host on the IPv6 loopback, on the first port of the suite's walk that binds.
    private static EnetTransport? OpenIpv6Host(out int port, out string why)
    {
        why = "no port tried";
        int first = SuitePorts.At(SuitePorts.Ipv6Join);
        for (port = first; port < first + SuitePorts.Walk; port++)
        {
            try
            {
                return EnetTransport.Host(port, maxPeers: 4, bindAddress: "::1");
            }
            catch (InvalidOperationException e)
            {
                why = e.Message;
            }
        }

        port = 0;
        return null;
    }

    // Backspace on the focused box until it is empty.
    private static void EraseAddress(End end)
    {
        for (int i = 0; i <= NetPlayFeature.AddressLimit && end.Door.Address.Length > 0; i++)
        {
            TypeInto(end, new MenuCommands { Erase = true });
        }
    }

    // Each character as the key event a German keyboard sends for it, one frame each. It goes
    // through the real viewport and back through a keyboard seat. Answers what the seat typed.
    private static string TypeKeys(TestContext ctx, End end, BuiltInSeat reader, string text)
    {
        var viewport = ctx.Host.GetViewport();
        var typed = new System.Text.StringBuilder();
        foreach (char c in text)
        {
            var (key, shift, altGr) = GermanKey(c);
            viewport.PushInput(new Godot.InputEventKey
            {
                Keycode = key,
                PhysicalKeycode = key,
                ShiftPressed = shift,
                CtrlPressed = altGr,
                AltPressed = altGr,
                Unicode = c,
                Pressed = true,
            });
            viewport.PushInput(new Godot.InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
            var frame = reader.Poll(Dt);
            typed.Append(frame.Typed);
            TypeInto(end, new MenuCommands { Typed = frame.Typed });
        }

        return typed.ToString();
    }

    // Where a German layout puts the characters an address is written with.
    private static (Godot.Key Key, bool Shift, bool AltGr) GermanKey(char c) => c switch
    {
        ':' => (Godot.Key.Period, true, false),
        '.' => (Godot.Key.Period, false, false),
        '[' => (Godot.Key.Key8, false, true),
        ']' => (Godot.Key.Key9, false, true),
        >= '0' and <= '9' => (Godot.Key.Key0 + (c - '0'), false, false),
        _ => ((Godot.Key)char.ToUpperInvariant(c), false, false),
    };

    // Frames of a Built-in host's door stepped by hand, each followed by one guest frame.
    private static void Frames(NetPlayFeature hostDoor, End guest, int count)
    {
        for (int frame = 0; frame < count; frame++)
        {
            hostDoor.Step(Dt);
            Pump(guest);
        }
    }

    // Frames of typing on the focused box, one command each.
    private static void TypeInto(End end, params MenuCommands[] frames)
    {
        foreach (var frame in frames)
        {
            end.Seat.Enqueue(frame);
            end.Host.Tick(Dt);
        }
    }

    // A real socket's datagrams land on the wall clock, so the wait is on it rather than a count.
    private static void Settle(Action answer, LanSearch search, Func<bool> done, double seconds = 5.0)
    {
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (waited.Elapsed.TotalSeconds < seconds)
        {
            answer();
            search.Poll();
            if (done())
            {
                return;
            }

            System.Threading.Thread.Sleep(1);
        }
    }

    // One Original presentation over its own host, door and scripted seat, shown on the top level.
    // A launch it hands out is added to exits when given, and its saved planes are planes' when given.
    private static End? Open(
        TestContext ctx, MenuLayout layout, NetPlayFeature door, List<End> ends, List<MenuExit>? exits = null, CustomPlaneStore? planes = null)
    {
        var seat = new ScriptedSeat();
        var registry = new PresentationRegistry();
        registry.Register(PresentationId.BuiltIn, () => new BuiltInPresentation(
            ctx.Host, ctx.ZrdrPath, ctx.DataRoot, string.Empty, new MenuInput { Keyboard = true }));
        registry.Register(PresentationId.Original, () => new OriginalPresentation(
            ctx.Host, ctx.DataRoot, layout, string.Empty, new MenuInput { Keyboard = true })
        {
            CampaignProfiles = CampaignAidProfiles.Store(seeded: true, progressed: true),
            Planes = planes,
        });
        var host = new MenuHost(registry, new MenuSuiteHost.SilentMenuAudio(), exit => exits?.Add(exit));
        MenuSuiteHost.AddFeatures(host, ctx.DataRoot, netDoor: door);
        host.AddSeat(seat);
        host.Select(forceBuiltIn: false, cliOverride: "original");
        host.Show(MenuReturnDestination.TopLevel);
        var shell = (host.Active as OriginalPresentation)?.Shell;
        ctx.Check(shell is { Screen: OriginalScreen.TopLevel }, $"each end shows Original on the top level ({shell?.Screen})");
        if (shell == null)
        {
            host.Deactivate();
            return null;
        }

        var end = new End(host, seat, shell);
        ends.Add(end);
        return end;
    }

    // ABLE-TO-FAIL CONTROL. The door closes what it opened. A second press that left the socket,
    // the mapping or the LAN answer up leaves a machine reachable with no band saying so.
    private static void OpenAndCloseControl(TestContext ctx, End host, NetPlayFeature door, List<int> unmapped)
    {
        ctx.Check(host.Shell.Screen == OriginalScreen.CampaignCabin, $"the host is seated in the cabin ({host.Shell.Screen})");
        var plaque = Row(host.Shell, OriginalCampaignScreen.CoopDoorKey);
        ctx.Check(plaque is { Enabled: true, Label: CoopDoorText.HostCoopButton },
            $"the cabin carries a live {CoopDoorText.HostCoopButton} plaque ({plaque?.Label})");
        ctx.Check(!Draws(host.Shell.Compose(), "NETWORK OPEN"), $"and draws no band while the door is shut");
        ClickRow(ctx, host, OriginalCampaignScreen.CoopDoorKey);
        ctx.Check(host.Shell.NetInfo.Page == NetInfoPage.Game && door.Stage == NetDoorStage.Shut,
            $"HOST CO-OP asks Game Information over the cabin before the door opens ({host.Shell.NetInfo.Page}, {door.Stage})");
        ctx.Check(Row(host.Shell, OriginalNetInfoBox.ListingKey)?.Label == CoopDoorText.PrivateWord,
            $"and its Listing chooser opens on Private, a campaign's default ({Row(host.Shell, OriginalNetInfoBox.ListingKey)?.Label})");
        ClickRow(ctx, host, OriginalNetInfoBox.CancelKey);
        ctx.Check(!host.Shell.NetInfo.IsOpen && door.Stage == NetDoorStage.Shut && host.Shell.Screen == OriginalScreen.CampaignCabin,
            $"ABLE-TO-FAIL CONTROL: its Cancel leaves the cabin with the door shut ({door.Stage}, {host.Shell.Screen})");
        ClickRow(ctx, host, OriginalCampaignScreen.CoopDoorKey);
        host.Shell.NetInfo.Draft.MaxPlayers = NetSeats.MaxPlayers;
        Answer(ctx, host, "Zachary", CampaignAidProfiles.Pilot);
        ctx.Check(door.IsCoopHost && door.Answering && door.Private,
            $"HOST CO-OP opens the carrier as a Private campaign host still answering the LAN ({door.Stage}, {door.Answering}, {door.Private})");
        ctx.Check(door.Advertising?.Cap == NetPlayFeature.CoopHumans,
            $"a cap of sixteen asked for a campaign is held to four humans ({door.Advertising?.Cap})");
        AwaitMapping(door);
        Pump(host);
        ctx.Check(Row(host.Shell, OriginalCampaignScreen.CoopDoorKey)?.Label == CoopDoorText.CloseNetworkButton
                  && Draws(host.Shell.Compose(), "NETWORK OPEN"),
            $"the plaque turns to {CoopDoorText.CloseNetworkButton} over the host's band");
        ClickRow(ctx, host, OriginalCampaignScreen.CoopDoorKey);
        ctx.Check(door.Stage == NetDoorStage.Shut && !door.Answering && unmapped.Count == 1,
            $"ABLE-TO-FAIL CONTROL: CLOSE NETWORK closes the carrier, the LAN answer and the mapping ({door.Stage}, {door.Answering}, {unmapped.Count} unmapped)");
    }

    // The open the match stands on: the advert names the cabin's next mission under the profile.
    private static void OpenForTheMatch(TestContext ctx, End host, NetPlayFeature door)
    {
        ClickRow(ctx, host, OriginalCampaignScreen.CoopDoorKey);
        host.Shell.NetInfo.Draft.MaxPlayers = NetPlayFeature.CoopHumans;
        Answer(ctx, host, "Zachary", CampaignAidProfiles.Pilot);
        AwaitMapping(door);
        Pump(host);
        var advert = door.Advertising;
        ctx.Check(advert is { Kind: NetSessionKind.CampaignCoop, Host: CampaignAidProfiles.Pilot, Players: 1 }
                  && advert.Value.MissionSeq == CampaignAidProfiles.MissionsFlown,
            $"the advert names a campaign, the profile, its next mission and one player ({advert?.Kind}, {advert?.Host}, {advert?.MissionSeq}, {advert?.Players})");
    }

    // A guest's walk: the plaque, the Connection page, Connect over LAN TCP/IP, the one row, Join.
    private static void JoinThroughTheList(TestContext ctx, End guest, List<End> ends, int players, string who)
    {
        var shell = guest.Shell;
        var plaque = Row(shell, OriginalShell.MultiplayerKey);
        ctx.Check(plaque is { Enabled: true }, $"{who}'s Multiplayer plaque is live over the network door");
        ClickRow(ctx, guest, OriginalShell.MultiplayerKey);
        ctx.Check(shell.Screen == OriginalScreen.Connection && shell.Connection.Way == OriginalConnectionScreen.LanKey,
            $"a click on it opens the Connection page on LAN TCP/IP ({shell.Screen}, {shell.Connection.Way})");
        if (players == 1)
        {
            CodeWayShut(ctx, guest);
        }

        ClickRow(ctx, guest, OriginalConnectionScreen.ConnectKey);
        ctx.Check(shell.Screen == OriginalScreen.ConnectionGames && Row(shell, OriginalConnectionScreen.CancelKey) != null,
            $"Connect opens the games list behind the Searching box ({shell.Screen})");
        for (int frame = 0; frame < 6 && shell.Connection.Listed.Count == 0; frame++)
        {
            Pump(ends.ToArray());
        }

        var listed = shell.Connection.Listed;
        ctx.Check(listed.Count == 1, $"the search lists the one open game ({listed.Count})");
        if (listed.Count != 1)
        {
            return;
        }

        var cells = shell.Connection.Cells(listed[0]);
        ctx.Check(
            cells.Count == 5 && cells[0] == CampaignAidProfiles.Pilot && cells[1] == $"{players}/4"
            && cells[2] == "Campaign co-op" && cells[3].Length > 0 && cells[4] == "Waiting",
            $"its row reads five columns ({string.Join(" | ", cells)})");
        ctx.Check(shell.FocusedKey == OriginalConnectionScreen.GameKey(0), $"the cursor lands on the first game ({shell.FocusedKey})");
        ctx.Check(Row(shell, OriginalConnectionScreen.JoinKey) is { Enabled: false }, $"Join Game is greyed until a row is picked");
        ClickRow(ctx, guest, OriginalConnectionScreen.GameKey(0));
        ctx.Check(Row(shell, OriginalConnectionScreen.JoinKey) is { Enabled: true }, $"and live once the row is picked");
        ClickRow(ctx, guest, OriginalConnectionScreen.JoinKey);
        Answer(ctx, guest, who == "the first guest" ? "Nathan" : "Sheila");
        for (int frame = 0; frame < 4; frame++)
        {
            Pump(ends.ToArray());
        }

        var door = guest.Door;
        ctx.Check(door.IsCoopGuest && door.Advert?.Host == CampaignAidProfiles.Pilot,
            $"Join Game lands {who} on the host's campaign ({door.Stage}, {door.Advert?.Host})");
        ctx.Check(shell.Dialog == null && shell.Screen == OriginalScreen.CampaignCabin && shell.Campaign.IsGuest,
            $"and stands it on the host's cabin as a guest ({shell.Screen}, {shell.Dialog?.Message})");
    }

    // A door with no master server greys Join by code and says why in its description line. The
    // cursor passes over it, and a click on its radio picks nothing.
    private static void CodeWayShut(TestContext ctx, End guest)
    {
        var shell = guest.Shell;
        string why = CoopDoorText.CodeJoinUnavailable(CoopDoorText.NoMasterServer);
        ctx.Check(Row(shell, OriginalConnectionScreen.CodeKey) is { Enabled: false } && Row(shell, OriginalConnectionScreen.CodeBoxKey) is { Enabled: false }
                  && Draws(shell.Compose(), why) && !Draws(shell.Compose(), OriginalConnectionScreen.CodeWayDescription),
            $"with no master server Join by code stands greyed and its description says why ({shell.Connection.CodeFault})");
        ClickRow(ctx, guest, OriginalConnectionScreen.CodeKey);
        ClickRow(ctx, guest, OriginalConnectionScreen.AddressKey);
        TypeInto(guest, new MenuCommands { MoveY = 1 });
        ctx.Check(shell.Connection.Way == OriginalConnectionScreen.LanKey && shell.FocusedKey == OriginalConnectionScreen.BuildKey,
            $"ABLE-TO-FAIL CONTROL: a click on its radio picks nothing, and the cursor steps from the IP Address box past it to Build Custom Plane ({shell.Connection.Way}, {shell.FocusedKey})");
    }

    // Two plain doors take the last seat and knock past it: four humans fit, the fifth hears why.
    private static void FillTheGame(TestContext ctx, List<End> ends, NetPlayFeature hostDoor, NetPlayFeature fourth, NetPlayFeature fifth)
    {
        fourth.OpenJoin();
        Pump(ends.ToArray());
        fourth.Step(Dt);
        fifth.OpenJoin();
        for (int frame = 0; frame < 4; frame++)
        {
            Pump(ends.ToArray());
            fourth.Step(Dt);
            fifth.Step(Dt);
        }

        ctx.Check(fourth.Stage == NetDoorStage.Joined && hostDoor.Peers == 3,
            $"a fourth human takes the last seat ({fourth.Stage}, {hostDoor.Peers} guests)");
        ctx.Check(hostDoor.Advertising is { Status: NetSessionStatus.Full, Players: 4 },
            $"and the advert reads full ({hostDoor.Advertising?.Status}, {hostDoor.Advertising?.Players})");
        ctx.Check(fifth.Stage == NetDoorStage.Failed && fifth.Fault == CoopDoorText.GameFull,
            $"a fifth human is refused as full ({fifth.Stage}, {fifth.Fault})");
    }

    // A link cut with no close notice reads as the host leaving. The page takes the guest back to
    // Connection under that word.
    private static void DropOne(TestContext ctx, End guest, INetTransport hostWire, int guestPeer)
    {
        hostWire.Disconnect(guestPeer);
        Pump(guest);
        ctx.Check(guest.Door.Fault == CoopDoorText.HostLeft && guest.Shell.Dialog?.Message == CoopDoorText.HostLeft,
            $"a silent drop tells the guest the host left ({guest.Door.Fault}, {guest.Shell.Dialog?.Message})");
        ctx.Check(guest.Shell.Screen == OriginalScreen.Connection, $"over the Connection page ({guest.Shell.Screen})");
    }

    // CLOSE NETWORK: the host's notice reaches the guest before its link goes, and OK hangs up.
    private static void CloseTheGame(TestContext ctx, End host, End guest, List<End> ends, NetPlayFeature hostDoor, List<int> unmapped)
    {
        ClickRow(ctx, host, OriginalCampaignScreen.CoopDoorKey);
        ctx.Check(hostDoor.Stage == NetDoorStage.Shut && unmapped.Count == 2,
            $"CLOSE NETWORK shuts the host and gives the port back ({hostDoor.Stage}, {unmapped.Count} unmapped)");
        for (int frame = 0; frame < 4; frame++)
        {
            Pump(ends.ToArray());
        }

        ctx.Check(guest.Door.Fault == CoopDoorText.HostClosed && guest.Shell.Dialog?.Message == CoopDoorText.HostClosed,
            $"the guest hears the host closed the game ({guest.Door.Fault}, {guest.Shell.Dialog?.Message})");
        ctx.Check(guest.Shell.Screen == OriginalScreen.Connection, $"over the Connection page ({guest.Shell.Screen})");
        ClickRow(ctx, guest, OriginalShell.DialogOkKey);
        ctx.Check(guest.Shell.Dialog == null && guest.Door.Stage == NetDoorStage.Shut,
            $"OK takes the box down and hangs up ({guest.Shell.Dialog?.Message}, {guest.Door.Stage})");
    }

    // The mapping lands on a worker thread, so the wait is on the wall clock rather than a count.
    private static void AwaitMapping(NetPlayFeature door)
    {
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (door.Router.PortMap == null && waited.Elapsed.TotalSeconds < 20.0)
        {
            door.Step(0.0);
            System.Threading.Thread.Sleep(1);
        }
    }

    // One idle frame on each end, in order: the frame is what steps each end's door.
    private static void Pump(params End[] ends)
    {
        foreach (var end in ends)
        {
            end.Host.Tick(Dt);
        }
    }

    // A pointer click on the row under that key, read fresh: the press arms it and the release fires.
    private static void ClickRow(TestContext ctx, End end, string key)
    {
        var row = Row(end.Shell, key);
        ctx.Check(row != null, $"the showing screen carries {key} ({end.Shell.Screen})");
        if (row == null)
        {
            return;
        }

        var size = ctx.Host.GetViewport().GetVisibleRect().Size;
        var fit = BoardFit.For(size.X, size.Y);
        float x = fit.X(row.X + Math.Min(5f, row.Width / 2f));
        float y = fit.Y(row.Y + Math.Min(5f, row.Height / 2f));
        end.Seat.Enqueue(new MenuCommands { Pointer = new MenuPointer(x, y, true, true, 0) });
        end.Host.Tick(Dt);
        end.Seat.Enqueue(new MenuCommands { Pointer = new MenuPointer(x, y, false, false, 0) });
        end.Host.Tick(Dt);
    }

    private static OriginalRow? Row(OriginalShell shell, string key) => shell.Rows.FirstOrDefault(row => row.Key == key);

    // One scripted frame on an end.
    private static void Press(End end, MenuCommands commands)
    {
        end.Seat.Enqueue(commands);
        end.Host.Tick(Dt);
    }

    private static bool Draws(ComposedBoard board, string text) =>
        board.Lines.Any(line => line.Text.Contains(text, StringComparison.Ordinal));

    // A line in one of the panels standing over the page, where the seat strip is drawn.
    private static bool DrawsOver(ComposedBoard board, string text) =>
        board.Overlays.SelectMany(panel => panel.Lines).Any(line => line.Text == text);

    // One end of the wire: its menu host, the seat the suite drives, and the shell it shows.
    private sealed record End(MenuHost Host, ScriptedSeat Seat, OriginalShell Shell)
    {
        public NetPlayFeature Door => Host.Features.Get<NetPlayFeature>();
    }

    // The host's end of a mesh whose guests arrive one at a time. A peer joins its roster, and its
    // payloads cross, only once that peer has arrived.
    private sealed class ArrivalGate : INetTransport, INetTransportListener, INetPeerAddress
    {
        private readonly INetTransport _inner;
        private readonly HashSet<int> _arrived = new();
        private INetTransportListener? _listener;

        public ArrivalGate(INetTransport inner) => _inner = inner;

        public int LocalPeer => _inner.LocalPeer;

        public IReadOnlyList<int> Peers => _inner.Peers.Where(_arrived.Contains).ToList();

        public string? AddressOf(int peer) => _arrived.Contains(peer) ? (_inner as INetPeerAddress)?.AddressOf(peer) : null;

        public void Arrive(int peer)
        {
            if (_arrived.Add(peer) && _inner.Peers.Contains(peer))
            {
                _listener?.OnPeerConnected(peer);
            }
        }

        public void Bind(INetTransportListener listener)
        {
            _listener = listener;
            _inner.Bind(this);
        }

        public void Send(int peer, ReadOnlySpan<byte> payload, NetReliability reliability, int channel = 0)
        {
            if (_arrived.Contains(peer))
            {
                _inner.Send(peer, payload, reliability, channel);
            }
        }

        public void Disconnect(int peer) => _inner.Disconnect(peer);

        public void Step(double dt) => _inner.Step(dt);

        public void OnPeerConnected(int peer)
        {
            if (_arrived.Contains(peer))
            {
                _listener?.OnPeerConnected(peer);
            }
        }

        public void OnPeerDisconnected(int peer)
        {
            if (_arrived.Contains(peer))
            {
                _listener?.OnPeerDisconnected(peer);
            }
        }

        public void OnPayload(int peer, int channel, ReadOnlySpan<byte> payload)
        {
            if (_arrived.Contains(peer))
            {
                _listener?.OnPayload(peer, channel, payload);
            }
        }
    }

    // A guest's loopback end that hangs up when its door closes, as a real socket does. The
    // loopback itself outlives a close, so a host would never hear the guest leave.
    private sealed class Hangup : INetTransport, IDisposable
    {
        private readonly INetTransport _inner;

        public Hangup(INetTransport inner) => _inner = inner;

        public int LocalPeer => _inner.LocalPeer;

        public IReadOnlyList<int> Peers => _inner.Peers;

        public void Bind(INetTransportListener listener) => _inner.Bind(listener);

        public void Send(int peer, ReadOnlySpan<byte> payload, NetReliability reliability, int channel = 0) =>
            _inner.Send(peer, payload, reliability, channel);

        public void Disconnect(int peer) => _inner.Disconnect(peer);

        public void Step(double dt) => _inner.Step(dt);

        public void Dispose()
        {
            foreach (int peer in _inner.Peers.ToList())
            {
                _inner.Disconnect(peer);
            }
        }
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
